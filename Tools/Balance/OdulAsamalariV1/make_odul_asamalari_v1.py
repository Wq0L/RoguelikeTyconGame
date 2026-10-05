# Run50_OdulAsamalariV1 üreticisi (Bölüm 3.7.3). Sayılar odul_asamalari_v1_params.py içinde.
# Yazdıkları: aşamalı ödül havuzu (BossRewards/OdulAsamalariV1/) ve profil (RunProfiles/Run50_OdulAsamalariV1.asset).
# Ödül asset'leri (DengeV1'in 13 ödülü, Kırılma V1'in 3 ödülü), denge seti ve boss havuzu kopyalanmaz: referans verilir.
# Takvim, kota ve boss hedefleri Takvim V1 parametre dosyasından okunur: profil, ödül havuzu dışında Takvim V1 ile aynıdır.
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_odul_asamalari_v1.py --check    → salt okunur kontrol
#   python make_odul_asamalari_v1.py --report   → aşama tablosu (Markdown), dosya yazmaz
#   python make_odul_asamalari_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
KIRILMA = os.path.abspath(os.path.join(HERE, '..', 'KirilmaV1'))
TAKVIM = os.path.abspath(os.path.join(HERE, '..', 'TakvimV1'))
for folder in (DENGE, KIRILMA, TAKVIM, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt, unescape
import generation_guard
import denge_v1_params as D
import kirilma_v1_params as K
import takvim_v1_params as T
import odul_asamalari_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
POOL_DIR = 'ScriptableObjects/BossRewards/OdulAsamalariV1'
POOL = POOL_DIR + '/BossRewardPool_OdulAsamalariV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_OdulAsamalariV1.asset'
PATTERNS = (POOL_DIR + '/*', PROFILE + '*')


def reward_path(source, name): return 'ScriptableObjects/BossRewards/%s/%s.asset' % (source, name)


def reward_field(source, name, field):
    text = io.open(os.path.join(A, reward_path(source, name)), encoding='utf-8').read()
    return unescape(re.search(r'^  %s: (.*)$' % field, text, flags=re.M).group(1))


def validate():
    """Oyundaki BossRewardPoolSO.Validate ile aynı kurallar. Veri düzeltilmez."""
    if P.STAGES[0]['first_round'] != 1: raise SystemExit('İlk aşama Round 1\'de başlamalı.')
    seen, ids = {}, {}
    for i, stage in enumerate(P.STAGES + [P.ENDLESS]):
        endless = i >= len(P.STAGES)
        if not endless:
            if i and stage['first_round'] <= P.STAGES[i - 1]['first_round']:
                raise SystemExit('Aşamalar sıralı değil: %s Round %d\'de başlıyor.' % (stage['name'], stage['first_round']))
            if stage['first_round'] > T.RUN_LENGTH:
                raise SystemExit('%s aşaması Round %d\'de başlıyor; run %d round.' % (stage['name'], stage['first_round'], T.RUN_LENGTH))
        for source, name in stage['rewards']:
            if not os.path.exists(os.path.join(A, reward_path(source, name))): raise SystemExit('Ödül asset\'i yok: %s/%s' % (source, name))
            if name in seen: raise SystemExit('\'%s\' iki aşamada yazılmış (%s ve %s): bir ödül yalnız bir aşamada olabilir.' % (name, seen[name], stage['name']))
            seen[name] = stage['name']
            rid = reward_field(source, name, 'id')
            if rid in ids: raise SystemExit('\'%s\' kimlikli ödül iki ayrı asset olarak yazılmış (%s ve %s).' % (rid, ids[rid], name))
            ids[rid] = name
    if len(P.DISTANCE_WEIGHTS) < len(P.STAGES) or any(w < 0 for w in P.DISTANCE_WEIGHTS):
        raise SystemExit('Aşama uzaklığı ağırlıkları her aşama için bir değer taşımalı ve negatif olmamalı.')
    for name in P.NOTES:
        if name not in seen: raise SystemExit('NOTES içinde havuzda olmayan ödül: %s' % name)
    # Kartta görünecek açıklamalar eski profilin sunulma zamanını anlatmamalı (zaman aşamadan üretilir).
    for stage in P.STAGES + [P.ENDLESS]:
        for source, name in stage['rewards']:
            note = P.NOTES.get(name) or reward_field(source, name, 'note')
            if re.search(r'Round \d+', note): raise SystemExit('%s açıklaması bir round sayısı anıyor ("%s"); NOTES içine zamansız bir metin yaz.' % (name, note))


def stage_bounds():
    out = []
    for i, stage in enumerate(P.STAGES):
        last = P.STAGES[i + 1]['first_round'] - 1 if i + 1 < len(P.STAGES) else T.RUN_LENGTH
        out.append((stage['first_round'], last, [r for r in T.BOSS_ROUNDS if stage['first_round'] <= r <= last]))
    return out


def report():
    print('| Aşama | Round\'lar | Boss round\'ları | Ödüller |')
    print('|---|---|---|---|')
    for stage, (first, last, bosses) in zip(P.STAGES, stage_bounds()):
        names = ', '.join(reward_field(s, n, 'displayName') for s, n in stage['rewards'])
        print('| %s | %d–%d | %s | %s |' % (stage['name'], first, last, ', '.join('R%d' % b for b in bosses), names))
    print('\nUzaklık ağırlıkları: ' + ' / '.join(('%.2f' % w).replace('.', ',') for w in P.DISTANCE_WEIGHTS)
          + ' · mevcut aşamaya slot: ' + ('evet' if P.RESERVE_CURRENT_STAGE_SLOT else 'hayır'))


validate()
if '--report' in sys.argv:
    report()
    sys.exit(0)
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'odul_asamalari_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# Referans verilen asset'ler incelenmiş hâllerinde olmalı: DengeV1 (13 ödül, boss havuzu), Kırılma V1 (3 ödül, denge seti).
generation_guard.verify(A, os.path.join(DENGE, 'generated_manifest.json'))
generation_guard.verify(A, os.path.join(KIRILMA, 'generated_manifest.json'),
                        ('ScriptableObjects/Balance/*KirilmaV1.asset*', 'ScriptableObjects/BossRewards/KirilmaV1/*',
                         'ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset*'), 'kirilma_v1_params.py')


def guid_for(key):
    return hashlib.md5(('ClickerGame/OdulAsamalariV1/' + key).encode('utf-8')).hexdigest()


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


def stage_yaml(stage, indent, first_round):
    pad = ' ' * indent
    lines = ['id: %s' % stage['id'], 'displayName: %s' % yaml_str(stage['name']), 'firstRound: %d' % first_round,
             'color: {r: %s, g: %s, b: %s, a: 1}' % tuple(fmt(c) for c in stage['color'])]
    if stage['rewards']:
        lines.append('rewards:')
        for source, name in stage['rewards']:
            lines.append('- reward: %s' % ref(asset_guid(reward_path(source, name))))
            lines.append('  note: %s' % (yaml_str(P.NOTES[name]) if name in P.NOTES else ''))
    else:
        lines.append('rewards: []')
    return lines, pad


# ---------------------------------------------------------------- aşamalı ödül havuzu
ensure_folder(POOL_DIR)
pool_body = '  rewards: []\n  breakthroughs: []\n  choices: %d\n  stages:\n' % D.REWARD_CHOICES
for stage in P.STAGES:
    lines, _ = stage_yaml(stage, 4, stage['first_round'])
    pool_body += '  - ' + lines[0] + '\n' + ''.join('    %s\n' % l for l in lines[1:])
pool_body += '  stageDistanceWeights:\n' + ''.join('  - %s\n' % fmt(w) for w in P.DISTANCE_WEIGHTS)
pool_body += '  reserveCurrentStageSlot: %d\n' % (1 if P.RESERVE_CURRENT_STAGE_SLOT else 0)
lines, _ = stage_yaml(P.ENDLESS, 4, 1)
pool_body += '  endlessStage:\n' + ''.join('    %s\n' % l for l in lines)
pool = write_asset(POOL, 'BossRewardPoolSO', 'BossRewardPool_OdulAsamalariV1', pool_body)

# ---------------------------------------------------------------- profil: Takvim V1 ile aynı; yalnız ödül havuzu farklı
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
profile = write_asset(PROFILE, 'RunProfileSO', 'Run50_OdulAsamalariV1', profile_body)

print('OdulAsamalariV1: profil %s, havuz %s, %d asama, %d odul' % (profile, pool, len(P.STAGES), sum(len(s['rewards']) for s in P.STAGES)))
generation_guard.record(A, MANIFEST, PATTERNS)
