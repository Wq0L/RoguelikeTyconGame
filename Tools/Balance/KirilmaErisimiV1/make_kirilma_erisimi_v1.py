# Run50_KirilmaErisimiV1 üreticisi (Bölüm 3.7.5). Sayılar kirilma_erisimi_v1_params.py içinde.
# Yazdıkları: kırılma ödüllerinin bu profile ait varyantları ve aşamalı havuz (BossRewards/KirilmaErisimiV1/), profil
# (RunProfiles/Run50_KirilmaErisimiV1.asset).
# Varyant, Kırılma V1'deki ödül asset'inin kopyasıdır; YALNIZ erişim alanı değişir (Artçı: echoRadius, Çifte Akım: echoReach).
# Hasar oranı, gecikme, tetik koşulu, ad ve açıklama taban asset'ten aynen gelir. Eski asset'lere dokunulmaz.
# Diğer her şey Bedelli Ödüller V1 ile aynıdır: aşamalar, 16 ödülün dağılımı, iki bedelli ödül, takvim, hedefler, denge seti.
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_kirilma_erisimi_v1.py --check    → salt okunur kontrol
#   python make_kirilma_erisimi_v1.py --report   → erişim tablosu (Markdown), dosya yazmaz
#   python make_kirilma_erisimi_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
KIRILMA = os.path.abspath(os.path.join(HERE, '..', 'KirilmaV1'))
TAKVIM = os.path.abspath(os.path.join(HERE, '..', 'TakvimV1'))
ODUL = os.path.abspath(os.path.join(HERE, '..', 'OdulAsamalariV1'))
BEDELLI = os.path.abspath(os.path.join(HERE, '..', 'BedelliOdullerV1'))
for folder in (DENGE, KIRILMA, TAKVIM, ODUL, BEDELLI, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import denge_v1_params as D
import kirilma_v1_params as K
import takvim_v1_params as T
import odul_asamalari_v1_params as O
import bedelli_oduller_v1_params as B
import kirilma_erisimi_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
RW_DIR = 'ScriptableObjects/BossRewards/KirilmaErisimiV1'
POOL = RW_DIR + '/BossRewardPool_KirilmaErisimiV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_KirilmaErisimiV1.asset'
PATTERNS = (RW_DIR + '/*', PROFILE + '*')
BEDELLI_PATTERNS = ('ScriptableObjects/BossRewards/BedelliOdullerV1/*', 'ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset*')


def reward_path(source, name): return 'ScriptableObjects/BossRewards/%s/%s.asset' % (source, name)


def multiplier(cells):
    """Artçının yarıçapı veride ilk patlamanın yarıçapına ORAN olarak durur (echoRadius)."""
    return cells / P.EXPLOSION_BASE_RADIUS_CELLS


def validate():
    """Veri düzeltilmez; hatalı parametrede yazmadan durur."""
    source = io.open(os.path.join(A, 'Scripts', 'Behaviors', 'HarvestBehaviorGeometry.cs'), encoding='utf-8').read()
    base = float(re.search(r'ExplosionRadiusCells = ([\d.]+)f', source).group(1))
    reach = int(re.search(r'ElectricReachCells = (\d+)', source).group(1))
    if abs(base - P.EXPLOSION_BASE_RADIUS_CELLS) > 1e-6:
        raise SystemExit('Patlamanın taban yarıçapı kodda %s hücre, parametrede %s.' % (base, P.EXPLOSION_BASE_RADIUS_CELLS))
    if reach != P.ELECTRIC_BASE_REACH_CELLS:
        raise SystemExit('Elektriğin taban erişimi kodda %d hücre, parametrede %d.' % (reach, P.ELECTRIC_BASE_REACH_CELLS))
    if P.ARTCI['radius_cells'] < P.EXPLOSION_BASE_RADIUS_CELLS: raise SystemExit('Artçı yarıçapı ilk patlamanınkinden küçük olamaz.')
    if P.CIFTE['in_pool'] and not P.ELECTRIC_BASE_REACH_CELLS <= P.CIFTE['reach_cells'] <= 6:
        raise SystemExit('Çifte Akım erişimi %d–6 hücre arasında olmalı.' % P.ELECTRIC_BASE_REACH_CELLS)
    replaced = {name for stage in O.STAGES for _, name in stage['rewards']}
    for variant in (P.ARTCI, P.CIFTE):
        if variant['base'][1] not in replaced: raise SystemExit('Taban ödül havuzda yok: %s' % variant['base'][1])


def report():
    print('| Ödül | Taban asset | Değişen alan | Kırılma V1 (eski profiller) | Bu profil |')
    print('|---|---|---|---|---|')
    print('| Artçı Patlama | %s | ikinci darbenin yarıçapı | %s hücre (×%s) | %s hücre (×%s) |' % (
        P.ARTCI['base'][1], ('%.2f' % (P.EXPLOSION_BASE_RADIUS_CELLS * 1.25)).replace('.', ','), '1,25',
        ('%.2f' % P.ARTCI['radius_cells']).replace('.', ','), ('%.3f' % multiplier(P.ARTCI['radius_cells'])).replace('.', ',')))
    print('| Çifte Akım | %s | ikinci dalganın çapraz erişimi | %d hücre | %s |' % (
        P.CIFTE['base'][1], P.ELECTRIC_BASE_REACH_CELLS, '%d hücre' % P.CIFTE['reach_cells'] if P.CIFTE['in_pool'] else 'havuzda yok'))


validate()
if '--report' in sys.argv:
    report()
    sys.exit(0)
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'kirilma_erisimi_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# Referans verilen asset'ler incelenmiş hâllerinde olmalı: DengeV1, Kırılma V1 (taban ödüller) ve Bedelli Ödüller V1.
generation_guard.verify(A, os.path.join(DENGE, 'generated_manifest.json'))
generation_guard.verify(A, os.path.join(KIRILMA, 'generated_manifest.json'),
                        ('ScriptableObjects/Balance/*KirilmaV1.asset*', 'ScriptableObjects/BossRewards/KirilmaV1/*',
                         'ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset*'), 'kirilma_v1_params.py')
generation_guard.verify(A, os.path.join(BEDELLI, 'generated_manifest.json'), BEDELLI_PATTERNS, 'bedelli_oduller_v1_params.py')


def guid_for(key):
    return hashlib.md5(('ClickerGame/KirilmaErisimiV1/' + key).encode('utf-8')).hexdigest()


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


def write_raw(rel, text):
    path = os.path.join(A, rel)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)
    g = guid_for(rel)
    io.open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(treelib.meta_yaml(g))
    return g


def write_asset(rel, script, name, body):
    head = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
            '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            + '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: Assembly-CSharp::%s\n'
            % (script_guid(script), yaml_str(name), script))
    return write_raw(rel, head + body)


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g


def ints(values): return ''.join('  - %d\n' % v for v in values)


def variant(spec, changes, extra):
    """Taban asset'in metni + değişen alanlar. Taban asset'te olmayan yeni alanlar (varsayılanlarıyla) sona eklenir."""
    source, base = spec['base']
    text = io.open(os.path.join(A, reward_path(source, base)), encoding='utf-8').read().replace('\r\n', '\n')
    text, n = re.subn(r'^  m_Name: .*$', '  m_Name: %s' % spec['file'], text, flags=re.M)
    assert n == 1
    for field, value in changes.items():
        text, n = re.subn(r'^  %s: .*$' % field, '  %s: %s' % (field, value), text, flags=re.M)
        assert n == 1, field
    if not text.endswith('\n'): text += '\n'
    for field, value in extra:
        assert not re.search(r'^  %s:' % field, text, flags=re.M), field
        text += '  %s: %s\n' % (field, value)
    return write_raw('%s/%s.asset' % (RW_DIR, spec['file']), text)


# ---------------------------------------------------------------- varyantlar
ensure_folder(RW_DIR)
replacement = {}
replacement[P.ARTCI['base'][1]] = variant(P.ARTCI, {'echoRadius': fmt(multiplier(P.ARTCI['radius_cells']))},
                                          [('levelChoiceDelta', '0'), ('exclusiveGroup', ''), ('echoReach', '0')])
if P.CIFTE['in_pool']:
    replacement[P.CIFTE['base'][1]] = variant(P.CIFTE, {}, [('levelChoiceDelta', '0'), ('exclusiveGroup', ''), ('echoReach', '%d' % P.CIFTE['reach_cells'])])
else:
    replacement[P.CIFTE['base'][1]] = None   # yalnız bu profilin havuzundan çıkar; eski asset ve eski havuzlar durur

own = {r['file']: asset_guid(reward_path('BedelliOdullerV1', r['file'])) for r in B.REWARDS}


def stage_rewards(stage):
    rewards = list(stage['rewards']) + [('BedelliOdullerV1', r['file']) for r in B.REWARDS if r['stage'] == stage['id']]
    out = []
    for source, name in rewards:
        if name in replacement:
            if replacement[name] is not None: out.append((replacement[name], name))
        elif name in own: out.append((own[name], name))
        else: out.append((asset_guid(reward_path(source, name)), name))
    return out


def stage_yaml(stage, first_round, rewards):
    lines = ['id: %s' % stage['id'], 'displayName: %s' % yaml_str(stage['name']), 'firstRound: %d' % first_round,
             'color: {r: %s, g: %s, b: %s, a: 1}' % tuple(fmt(c) for c in stage['color'])]
    if rewards:
        lines.append('rewards:')
        for guid, name in rewards:
            lines.append('- reward: %s' % ref(guid))
            lines.append('  note: %s' % (yaml_str(O.NOTES[name]) if name in O.NOTES else ''))
    else:
        lines.append('rewards: []')
    return lines


# ---------------------------------------------------------------- aşamalı ödül havuzu
pool_body = '  rewards: []\n  breakthroughs: []\n  choices: %d\n  stages:\n' % D.REWARD_CHOICES
count = 0
for stage in O.STAGES:
    rewards = stage_rewards(stage)
    count += len(rewards)
    lines = stage_yaml(stage, stage['first_round'], rewards)
    pool_body += '  - ' + lines[0] + '\n' + ''.join('    %s\n' % l for l in lines[1:])
pool_body += '  stageDistanceWeights:\n' + ''.join('  - %s\n' % fmt(w) for w in O.DISTANCE_WEIGHTS)
pool_body += '  reserveCurrentStageSlot: %d\n' % (1 if O.RESERVE_CURRENT_STAGE_SLOT else 0)
pool_body += '  endlessStage:\n' + ''.join('    %s\n' % l for l in stage_yaml(O.ENDLESS, 1, []))
pool = write_asset(POOL, 'BossRewardPoolSO', 'BossRewardPool_KirilmaErisimiV1', pool_body)

# ---------------------------------------------------------------- profil: Bedelli Ödüller V1 ile aynı; yalnız ödül havuzu farklı
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
profile = write_asset(PROFILE, 'RunProfileSO', 'Run50_KirilmaErisimiV1', profile_body)

print('KirilmaErisimiV1: profil %s, havuz %s, %d odul; Artci %.2f hucre (x%.3f), Cifte Akim %s'
      % (profile, pool, count, P.ARTCI['radius_cells'], multiplier(P.ARTCI['radius_cells']),
         '%d hucre' % P.CIFTE['reach_cells'] if P.CIFTE['in_pool'] else 'havuzda yok'))
generation_guard.record(A, MANIFEST, PATTERNS)
