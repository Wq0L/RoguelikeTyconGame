# Run50_KirilmaV1 veri üreticisi (Bölüm 3.6). Sayılar kirilma_v1_params.py içinde; bu betik yalnız Kırılma V1 asset'lerini yazar.
# DengeV1'in denge asset'leri (temel stat, can, XP, ağaç, boss'lar, 13 ödül) kopyalanmaz: yeni profil onlara referans verir.
# Round süresi, kota ve boss hedefleri ile başlangıç bütçesi DengeV1 parametre dosyasından okunur (tek kaynak).
# GUID'ler ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_kirilma_v1.py --check   → salt okunur kontrol
#   python make_kirilma_v1.py           → kontrol, üretim, manifest kaydı
import hashlib, io, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
sys.path.insert(0, DENGE)
sys.path.insert(0, HERE)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import denge_v1_params as D
import kirilma_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
PATTERNS = (
    'ScriptableObjects/Balance/*KirilmaV1.asset*',
    'ScriptableObjects/BossRewards/KirilmaV1/*',
    'ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset*',
)
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'kirilma_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# DengeV1 çıktıları da kendi manifest'iyle aynı olmalı: referans verilen asset'ler incelenmiş hâlinde.
generation_guard.verify(A, os.path.join(DENGE, 'generated_manifest.json'))


def guid_for(key):
    return hashlib.md5(('ClickerGame/KirilmaV1/' + key).encode('utf-8')).hexdigest()


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


def ints(name, values):
    return '  %s: []\n' % name if not values else '  %s:\n' % name + ''.join('  - %d\n' % v for v in values)


S = lambda n: treelib.STAT.index(n)

# ---------------------------------------------------------------- denge seti: DengeV1'in verisi + erken davranış erişimi
dv = 'ScriptableObjects/Balance/'
balance_body = ('  coreStats: %s\n  plantHealth: %s\n  progression: %s\n  skillTree: %s\n  rarityXpMultipliers:\n'
                % (ref(asset_guid(dv + 'CoreStat_DengeV1.asset')), ref(asset_guid(dv + 'PlantHealth_DengeV1.asset')),
                   ref(asset_guid(dv + 'Progression_DengeV1.asset')), ref(asset_guid(dv + 'SkillTreeSet_DengeV1.asset')))
                + ''.join('  - %s\n' % fmt(v) for v in D.RARITY_XP)
                + ('  planterBaseStats:\n' + ''.join('  - statType: %d\n    value: %s\n' % (S(k), fmt(v)) for k, v in D.PLANTER_BASE.items()) if D.PLANTER_BASE else '  planterBaseStats: []\n')
                + ints('startingUnlocks', P.STARTING_UNLOCKS) + ints('firstBehaviorOffer', P.FIRST_BEHAVIOR_OFFER))
assert not D.PLANTER_PRICES, 'DengeV1 saksı fiyatı tanımladıysa burada da yazılmalı'
balance_body += '  planterPrices: []\n'
ensure_folder('ScriptableObjects/Balance')
balance = write_asset(dv + 'RunBalance_KirilmaV1.asset', 'RunBalanceSO', 'RunBalance_KirilmaV1', balance_body)

# ---------------------------------------------------------------- kırılma ödülleri ve havuz
RW_DIR = 'ScriptableObjects/BossRewards/KirilmaV1'
ensure_folder(RW_DIR)
breakthrough_guids = []
for r in P.BREAKTHROUGHS:
    echo = r.get('echo', 0)
    body = ('  id: %s\n  displayName: %s\n  effectLabel: %s\n  inverseLabel: ""\n  note: %s\n  modifiersPerStack: []\n'
            '  directDamageMultiplier: 1\n  rareDirectMultiplier: 1\n  rareDirectFrom: 2\n  behaviorDamageMultiplier: 1\n'
            '  echo: %d\n  echoDelay: %s\n  echoDamage: %s\n  echoRadius: %s\n  rhythmHarvests: %d\n  rhythmDamage: %s\n  rhythmRadius: %s\n'
            '  maxStacks: 1\n  weight: 1\n  condition: %d\n  conditionStat: %d\n  conditionThreshold: 0.7\n  minRound: %d\n  maxRound: 0\n'
            % (r['id'], yaml_str(r['name']), yaml_str(r['label']), yaml_str(r['note']),
               echo, fmt(r.get('delay', 0.2)), fmt(r.get('damage', 0.7)), fmt(r.get('radius', 1.25)),
               r.get('rhythm', 0), fmt(r.get('rhythm_damage', 1.5)), fmt(r.get('rhythm_radius', 1.5)),
               4 if echo else 0, r.get('condition_stat', 0), P.BREAKTHROUGH_MIN_ROUND))
    breakthrough_guids.append(write_asset('%s/%s.asset' % (RW_DIR, r['file']), 'BossRewardSO', r['file'], body))

# Küçük stat ödülleri: DengeV1'in 13 ödülü, aynı asset'ler (kopya değil).
small = [asset_guid('ScriptableObjects/BossRewards/DengeV1/%s.asset' % r['file']) for r in D.REWARDS]
pool = write_asset(RW_DIR + '/BossRewardPool_KirilmaV1.asset', 'BossRewardPoolSO', 'BossRewardPool_KirilmaV1',
    '  rewards:\n' + ''.join('  - %s\n' % ref(g) for g in small)
    + '  breakthroughs:\n' + ''.join('  - %s\n' % ref(g) for g in breakthrough_guids) + '  choices: %d\n' % D.REWARD_CHOICES)

# ---------------------------------------------------------------- profil: DengeV1'in süresi, kotası, boss'ları ve bütçesi
profile_body = ('  displayName: %s\n  runLength: 50\n  segmentRounds: 5\n  quotaStart: 10\n  quotaGrowth: 1.45\n  segmentTargets:\n%s'
                '  events: []\n  specializationAfterSegment: 0\n  specializationOptions: []\n  bossPool: %s\n  bossTargets:\n%s'
                '  bossRewards: %s\n  bossSeed: 0\n  balance: %s\n  choicesPerLevel: %d\n  startingGold: %d\n  startingIron: 0\n  startingStone: 0\n  debugBudget: 0\n  electricMode: 0\n'
                '  electricCharge:\n    referenceSeconds: 10\n    referenceChance: 0.5\n    minimumSeconds: 3\n    quickChargeMultiplier: 1\n'
                '  fixedRoundDuration: %s\n  victoryTitle: %s\n'
                % (yaml_str('Run50 Kırılma V1 · 50 round'), ''.join('  - %d\n' % t for t in D.SEGMENT_TARGETS),
                   ref(asset_guid('ScriptableObjects/Bosses/BossPool_DengeV1.asset')), ''.join('  - %d\n' % t for t in D.BOSS_TARGETS),
                   ref(pool), ref(balance), P.CHOICES_PER_LEVEL, D.START_GOLD, fmt(D.ROUND_SECONDS), yaml_str('RUN TAMAMLANDI · KIRILMA V1')))
profile = write_asset('ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset', 'RunProfileSO', 'Run50_KirilmaV1', profile_body)

print('KirilmaV1: profil %s · denge seti %s · %d kirilma odulu + %d kucuk odul · level basina %d secim'
      % (profile, balance, len(breakthrough_guids), len(small), P.CHOICES_PER_LEVEL))
generation_guard.record(A, MANIFEST, PATTERNS)
