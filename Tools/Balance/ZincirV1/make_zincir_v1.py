# Run50_ZincirV1 üreticisi (Bölüm 3.7.6). Sayılar zincir_v1_params.py içinde.
# Yazdıkları: Zincir Hasat ödülü ve aşamalı havuz (BossRewards/ZincirV1/), profil (RunProfiles/Run50_ZincirV1.asset).
# Havuz, Kırılma Erişimi V1 havuzunun kopyasıdır; YALNIZ güçlü aşamaya Zincir Hasat eklenir. Profil, Kırılma Erişimi V1
# profilinin kopyasıdır; yalnız ad, zafer başlığı ve ödül havuzu farklıdır. Eski asset'lere ve havuzlara dokunulmaz.
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_zincir_v1.py --check    → salt okunur kontrol
#   python make_zincir_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
ERISIM = os.path.abspath(os.path.join(HERE, '..', 'KirilmaErisimiV1'))
for folder in (DENGE, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import zincir_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
RW_DIR = 'ScriptableObjects/BossRewards/ZincirV1'
REWARD = RW_DIR + '/%s.asset' % P.CHAIN['file']
POOL = RW_DIR + '/BossRewardPool_ZincirV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_ZincirV1.asset'
PATTERNS = (RW_DIR + '/*', PROFILE + '*')
BASE_POOL = 'ScriptableObjects/BossRewards/KirilmaErisimiV1/BossRewardPool_KirilmaErisimiV1.asset'
BASE_PROFILE = 'ScriptableObjects/RunProfiles/Run50_KirilmaErisimiV1.asset'
ERISIM_PATTERNS = ('ScriptableObjects/BossRewards/KirilmaErisimiV1/*', 'ScriptableObjects/RunProfiles/Run50_KirilmaErisimiV1.asset*')


def validate():
    """Veri düzeltilmez; hatalı parametrede yazmadan durur."""
    c = P.CHAIN
    n = c['generations']
    if not 1 <= n <= 4: raise SystemExit('Ek nesil sayısı 1–4 olmalı.')
    if len(c['chance']) != n or len(c['damage']) != n: raise SystemExit('Şans ve hasar çarpanı sayısı ek nesil sayısına eşit olmalı.')
    if any(not 0 <= v <= 1 for v in c['chance']) or any(v < 0 for v in c['damage']): raise SystemExit('Çarpanlar geçersiz.')
    if c['root_budget'] < 1 or c['jobs_per_frame'] < 1: raise SystemExit('Bütçeler en az 1 olmalı.')
    source = io.open(os.path.join(A, 'Scripts', 'Managers', 'HarvestChain.cs'), encoding='utf-8').read()
    limit = int(re.search(r'MaxGenerations = (\d+)', source).group(1))
    if n > limit: raise SystemExit('Kod en çok %d ek nesil taşır.' % limit)


validate()
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'zincir_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# Kopyalanan asset'ler incelenmiş hâllerinde olmalı: Kırılma Erişimi V1 (havuz ve profil).
generation_guard.verify(A, os.path.join(ERISIM, 'generated_manifest.json'), ERISIM_PATTERNS, 'kirilma_erisimi_v1_params.py')


def guid_for(key):
    return hashlib.md5(('ClickerGame/ZincirV1/' + key).encode('utf-8')).hexdigest()


def script_guid(name, folder='Scripts/ScriptableObjects'):
    for line in io.open(os.path.join(A, folder, name + '.cs.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(name)


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


def read(rel):
    return io.open(os.path.join(A, rel), encoding='utf-8').read().replace('\r\n', '\n')


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g


def floats(values): return ''.join('  - %s\n' % fmt(v) for v in values)


def replace(text, field, value, indent='  '):
    line = '%s%s: %s' % (indent, field, value)
    text, n = re.subn(r'^%s%s: .*$' % (indent, field), lambda _: line, text, flags=re.M)
    assert n == 1, field
    return text


# ---------------------------------------------------------------- Zincir Hasat ödülü
c = P.CHAIN
ensure_folder(RW_DIR)
reward_body = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
               '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
               '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
               + '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: Assembly-CSharp::BossRewardSO\n'
               % (script_guid('BossRewardSO'), c['file']))
reward_body += ('  id: %s\n  displayName: %s\n  effectLabel: %s\n  inverseLabel: \n  note: %s\n  modifiersPerStack: []\n'
                '  directDamageMultiplier: 1\n  rareDirectMultiplier: 1\n  rareDirectFrom: 2\n  behaviorDamageMultiplier: 1\n'
                '  echo: 0\n  echoDelay: 0.2\n  echoDamage: 0.7\n  echoRadius: 1.25\n  rhythmHarvests: 0\n  rhythmDamage: 1.5\n  rhythmRadius: 1.5\n'
                '  maxStacks: %d\n  weight: %s\n  condition: %d\n  conditionStat: 0\n  conditionThreshold: 0.7\n  minRound: 0\n  maxRound: 0\n'
                '  levelChoiceDelta: 0\n  exclusiveGroup: \n  echoReach: 0\n'
                '  chain: 1\n  chainGenerations: %d\n  chainChance:\n%s  chainDamage:\n%s  chainRootBudget: %d\n  chainJobsPerFrame: %d\n'
                % (c['id'], yaml_str(c['display_name']), yaml_str(c['effect_label']), yaml_str(c['note']),
                   c['max_stacks'], fmt(c['weight']), c['condition'], c['generations'], floats(c['chance']), floats(c['damage']),
                   c['root_budget'], c['jobs_per_frame']))
reward = write_raw(REWARD, reward_body)

# ---------------------------------------------------------------- havuz: Kırılma Erişimi V1 havuzu + güçlü aşamada Zincir Hasat
pool_text = replace(read(BASE_POOL), 'm_Name', 'BossRewardPool_ZincirV1')
stage = re.search(r'^  - id: %s\n(?:    .*\n|      .*\n)*' % c['stage'], pool_text, flags=re.M)
assert stage, 'güçlü aşama bulunamadı'
entry = '    - reward: %s\n      note: %s\n' % (ref(reward), yaml_str(c['note']))
pool_text = pool_text[:stage.end()] + entry + pool_text[stage.end():]
pool = write_raw(POOL, pool_text)

# ---------------------------------------------------------------- profil: Kırılma Erişimi V1 ile aynı; yalnız ad, başlık, havuz
profile_text = read(BASE_PROFILE)
profile_text = replace(profile_text, 'm_Name', 'Run50_ZincirV1')
profile_text = replace(profile_text, 'displayName', yaml_str(P.DISPLAY_NAME))
profile_text = replace(profile_text, 'victoryTitle', yaml_str(P.VICTORY_TITLE))
profile_text = replace(profile_text, 'bossRewards', ref(pool))
profile = write_raw(PROFILE, profile_text)

print('ZincirV1: profil %s, havuz %s, odul %s (%d ek nesil, sans %s, hasar %s, kok butcesi %d, kare %d)'
      % (profile, pool, reward, c['generations'], c['chance'], c['damage'], c['root_budget'], c['jobs_per_frame']))
generation_guard.record(A, MANIFEST, PATTERNS)
