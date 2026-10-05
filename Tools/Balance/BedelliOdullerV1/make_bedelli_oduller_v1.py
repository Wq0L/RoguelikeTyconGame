# Run50_BedelliOdullerV1 üreticisi (Bölüm 3.7.4). Sayılar bedelli_oduller_v1_params.py içinde.
# Yazdıkları: iki bedelli ödül ve aşamalı havuz (BossRewards/BedelliOdullerV1/), profil (RunProfiles/Run50_BedelliOdullerV1.asset).
# Mevcut 16 ödül asset'i, denge seti ve boss havuzu kopyalanmaz: referans verilir. Aşamalar, 16 ödülün dağılımı ve teklif politikası
# Ödül Aşamaları V1'in, takvim ve hedefler Takvim V1'in parametre dosyasından okunur: profil, ödül havuzu dışında onlarla aynıdır.
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_bedelli_oduller_v1.py --check    → salt okunur kontrol
#   python make_bedelli_oduller_v1.py --report   → ödül tablosu (Markdown), dosya yazmaz
#   python make_bedelli_oduller_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
KIRILMA = os.path.abspath(os.path.join(HERE, '..', 'KirilmaV1'))
TAKVIM = os.path.abspath(os.path.join(HERE, '..', 'TakvimV1'))
ODUL = os.path.abspath(os.path.join(HERE, '..', 'OdulAsamalariV1'))
for folder in (DENGE, KIRILMA, TAKVIM, ODUL, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt, unescape
import generation_guard
import denge_v1_params as D
import kirilma_v1_params as K
import takvim_v1_params as T
import odul_asamalari_v1_params as O
import bedelli_oduller_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
RW_DIR = 'ScriptableObjects/BossRewards/BedelliOdullerV1'
POOL = RW_DIR + '/BossRewardPool_BedelliOdullerV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset'
PATTERNS = (RW_DIR + '/*', PROFILE + '*')
OWN = {r['file'] for r in P.REWARDS}


def reward_path(source, name): return 'ScriptableObjects/BossRewards/%s/%s.asset' % (source, name)


def reward_field(source, name, field):
    text = io.open(os.path.join(A, reward_path(source, name)), encoding='utf-8').read()
    return unescape(re.search(r'^  %s: (.*)$' % field, text, flags=re.M).group(1))


def stages():
    """Ödül Aşamaları V1'in aşamaları + bu profilin ödülleri (kendi aşamalarının sonuna)."""
    out = []
    for stage in O.STAGES:
        rewards = list(stage['rewards']) + [('BedelliOdullerV1', r['file']) for r in P.REWARDS if r['stage'] == stage['id']]
        out.append(dict(stage, rewards=rewards))
    return out


def validate():
    """Oyundaki kurallarla aynı denetim (BossRewardPoolSO.Validate + seçim hakkı aralığı). Veri düzeltilmez."""
    stage_ids = [s['id'] for s in O.STAGES]
    low, high = P.LEVEL_CHOICE_RANGE
    ids = {}
    for stage in O.STAGES + [O.ENDLESS]:
        for source, name in stage['rewards']: ids[reward_field(source, name, 'id')] = name
    files = set()
    for r in P.REWARDS:
        if r['stage'] not in stage_ids: raise SystemExit('%s: bilinmeyen aşama "%s" (%s).' % (r['name'], r['stage'], ', '.join(stage_ids)))
        if r['id'] in ids: raise SystemExit('\'%s\' kimliği iki ödülde (%s ve %s).' % (r['id'], ids[r['id']], r['file']))
        if r['file'] in files: raise SystemExit('Aynı dosya adı iki ödülde: %s' % r['file'])
        ids[r['id']] = r['file']; files.add(r['file'])
        if r['max_stacks'] < 1 or r['weight'] < 0 or r['direct'] <= 0 or r['behavior'] <= 0: raise SystemExit('%s: geçersiz sayı.' % r['name'])
        # Tek başına bile aralığı aşan ödül oyunda hiç sunulmaz (kırpılmaz): veri hatası sayılır.
        result = K.CHOICES_PER_LEVEL + r['level_choices']
        if not low <= result <= high:
            raise SystemExit('%s: seçim hakkı %d → %d olur; geçerli aralık %d–%d (ödül hiç sunulamaz).' % (r['name'], K.CHOICES_PER_LEVEL, result, low, high))
        if r['level_choices'] == 0 and r['direct'] == 1 and r['behavior'] == 1: raise SystemExit('%s: etkisi yok.' % r['name'])
        if re.search(r'Round \d+', r['note']): raise SystemExit('%s açıklaması bir round sayısı anıyor; sunulma zamanı aşamadan üretilir.' % r['name'])
    for group in {r['group'] for r in P.REWARDS if r['group']}:
        if sum(1 for r in P.REWARDS if r['group'] == group) < 2: raise SystemExit('"%s" dışlama grubunda tek ödül var.' % group)


def report():
    bosses = {s['id']: [b for b in T.BOSS_ROUNDS if b >= s['first_round']][0] for s in O.STAGES}
    cond = {0: 'koşulsuz', 2: 'davranış şansı olan saksı varsa'}
    print('| Ödül | Aşama (ilk boss) | Seçim hakkı | Doğrudan | Davranış | En çok | Ağırlık | Koşul | Dışlama grubu |')
    print('|---|---|---|---|---|---|---|---|---|')
    for r in P.REWARDS:
        print('| %s | %s (R%d) | %+d | ×%s | ×%s | %d | %s | %s | %s |' % (
            r['name'], r['stage'], bosses[r['stage']], r['level_choices'], ('%.2f' % r['direct']).replace('.', ','),
            ('%.2f' % r['behavior']).replace('.', ','), r['max_stacks'], fmt(r['weight']), cond[r['condition']], r['group'] or '—'))


validate()
if '--report' in sys.argv:
    report()
    sys.exit(0)
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'bedelli_oduller_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# Referans verilen asset'ler incelenmiş hâllerinde olmalı: DengeV1 (13 ödül, boss havuzu), Kırılma V1 (3 ödül, denge seti).
generation_guard.verify(A, os.path.join(DENGE, 'generated_manifest.json'))
generation_guard.verify(A, os.path.join(KIRILMA, 'generated_manifest.json'),
                        ('ScriptableObjects/Balance/*KirilmaV1.asset*', 'ScriptableObjects/BossRewards/KirilmaV1/*',
                         'ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset*'), 'kirilma_v1_params.py')


def guid_for(key):
    return hashlib.md5(('ClickerGame/BedelliOdullerV1/' + key).encode('utf-8')).hexdigest()


def script_guid(name, folder='Scripts/ScriptableObjects'):
    for line in io.open(os.path.join(A, folder, name + '.cs.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(name)


def asset_guid(rel):
    for line in io.open(os.path.join(A, rel + '.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(rel)


def ensure_folder(rel):
    path = os.path.join(A, rel)
    if not os.path.isdir(path): os.makedirs(path)
    meta = path + '.meta'
    if not os.path.exists(meta):
        io.open(meta, 'w', encoding='utf-8', newline='\n').write(
            'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid_for('folder/' + rel))


def write_asset(rel, script, name, body):
    head = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
            '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            + '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: Assembly-CSharp::%s\n'
            % (script_guid(script), yaml_str(name), script))
    path = os.path.join(A, rel)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(head + body)
    g = guid_for(rel)
    io.open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(treelib.meta_yaml(g))
    return g


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g


def ints(values): return ''.join('  - %d\n' % v for v in values)


# ---------------------------------------------------------------- bedelli ödüller
# Alan sırası BossRewardSO'daki sırayla aynıdır. minRound / maxRound aşamalı havuzda okunmaz (0 bırakılır).
ensure_folder(RW_DIR)
own_guids = {}
for r in P.REWARDS:
    body = ('  id: %s\n  displayName: %s\n  effectLabel: \n  inverseLabel: \n  note: %s\n  modifiersPerStack: []\n'
            '  directDamageMultiplier: %s\n  rareDirectMultiplier: 1\n  rareDirectFrom: 2\n  behaviorDamageMultiplier: %s\n'
            '  echo: 0\n  echoDelay: 0.2\n  echoDamage: 0.7\n  echoRadius: 1.25\n  rhythmHarvests: 0\n  rhythmDamage: 1.5\n  rhythmRadius: 1.5\n'
            '  maxStacks: %d\n  weight: %s\n  condition: %d\n  conditionStat: 0\n  conditionThreshold: 0.7\n  minRound: 0\n  maxRound: 0\n'
            '  levelChoiceDelta: %d\n  exclusiveGroup: %s\n'
            % (r['id'], yaml_str(r['name']), yaml_str(r['note']), fmt(r['direct']), fmt(r['behavior']),
               r['max_stacks'], fmt(r['weight']), r['condition'], r['level_choices'], r['group']))
    own_guids[r['file']] = write_asset('%s/%s.asset' % (RW_DIR, r['file']), 'BossRewardSO', r['file'], body)


def reward_guid(source, name):
    return own_guids[name] if name in OWN else asset_guid(reward_path(source, name))


def stage_yaml(stage, first_round):
    lines = ['id: %s' % stage['id'], 'displayName: %s' % yaml_str(stage['name']), 'firstRound: %d' % first_round,
             'color: {r: %s, g: %s, b: %s, a: 1}' % tuple(fmt(c) for c in stage['color'])]
    if stage['rewards']:
        lines.append('rewards:')
        for source, name in stage['rewards']:
            lines.append('- reward: %s' % ref(reward_guid(source, name)))
            lines.append('  note: %s' % (yaml_str(O.NOTES[name]) if name in O.NOTES else ''))
    else:
        lines.append('rewards: []')
    return lines


# ---------------------------------------------------------------- aşamalı ödül havuzu: Ödül Aşamaları V1 + iki bedelli ödül
pool_body = '  rewards: []\n  breakthroughs: []\n  choices: %d\n  stages:\n' % D.REWARD_CHOICES
for stage in stages():
    lines = stage_yaml(stage, stage['first_round'])
    pool_body += '  - ' + lines[0] + '\n' + ''.join('    %s\n' % l for l in lines[1:])
pool_body += '  stageDistanceWeights:\n' + ''.join('  - %s\n' % fmt(w) for w in O.DISTANCE_WEIGHTS)
pool_body += '  reserveCurrentStageSlot: %d\n' % (1 if O.RESERVE_CURRENT_STAGE_SLOT else 0)
pool_body += '  endlessStage:\n' + ''.join('    %s\n' % l for l in stage_yaml(O.ENDLESS, 1))
pool = write_asset(POOL, 'BossRewardPoolSO', 'BossRewardPool_BedelliOdullerV1', pool_body)

# ---------------------------------------------------------------- profil: Ödül Aşamaları V1 ile aynı; yalnız ödül havuzu farklı
calendar = ''.join('  - round: %d\n    final: %d\n    boss: {fileID: 0}\n' % (r, 1 if r == T.FINAL_ROUND else 0) for r in T.BOSS_ROUNDS)
profile_body = ('  displayName: %s\n  runLength: %d\n  segmentRounds: 5\n  quotaStart: 10\n  quotaGrowth: 1.45\n  segmentTargets:\n%s'
                '  events: []\n  specializationAfterSegment: 0\n  specializationOptions: []\n  bossPool: %s\n  bossTargets:\n%s'
                '  bossRewards: %s\n  bossSeed: 0\n  bossCalendar:\n%s  balance: %s\n  choicesPerLevel: %d\n'
                '  startingGold: %d\n  startingIron: 0\n  startingStone: 0\n  debugBudget: 0\n  electricMode: 0\n'
                '  electricCharge:\n    referenceSeconds: 10\n    referenceChance: 0.5\n    minimumSeconds: 3\n    quickChargeMultiplier: 1\n'
                '  fixedRoundDuration: %s\n  victoryTitle: %s\n'
                % (yaml_str(P.DISPLAY_NAME), T.RUN_LENGTH, ints(T.QUOTA_TARGETS), ref(asset_guid('ScriptableObjects/Bosses/BossPool_DengeV1.asset')),
                   ints(T.BOSS_TARGETS), ref(pool), calendar, ref(asset_guid('ScriptableObjects/Balance/RunBalance_KirilmaV1.asset')),
                   K.CHOICES_PER_LEVEL, D.START_GOLD, fmt(D.ROUND_SECONDS), yaml_str(P.VICTORY_TITLE)))
profile = write_asset(PROFILE, 'RunProfileSO', 'Run50_BedelliOdullerV1', profile_body)

print('BedelliOdullerV1: profil %s, havuz %s, %d bedelli odul + %d mevcut odul'
      % (profile, pool, len(P.REWARDS), sum(len(s['rewards']) for s in O.STAGES)))
generation_guard.record(A, MANIFEST, PATTERNS)
