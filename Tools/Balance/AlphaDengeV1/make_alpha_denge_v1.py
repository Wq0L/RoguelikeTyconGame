# Run50_AlphaDengeV1 üreticisi (Bölüm 3.7.8). Sayılar alpha_denge_v1_params.py içinde.
# Yazdıkları:
#   - Balance/AlphaDengeV1/PlantHealth_AlphaDengeV1.asset  bitki can eğrisi (bu profilin kendi kopyası)
#   - Balance/AlphaDengeV1/RunBalance_AlphaDengeV1.asset   denge seti: RunBalance_XPV1'in kopyası; can eğrisi (ve varsa XP tablosu) farklı
#   - Balance/AlphaDengeV1/Progression_AlphaDengeV1.asset  YALNIZ XP_TABLE_OVERRIDES doluysa (boşsa XP V1 tablosuna referans verilir)
#   - BossRewards/AlphaDengeV1/…                           YALNIZ bir ödülün değeri değiştiyse: değişen ödüllerin kopyası + havuz kopyası
#   - RunProfiles/Run50_AlphaDengeV1.asset                 profil: Run50_XPV1'in kopyası; süre tablosu, hedefler, denge seti (ve havuz) farklı
# Eski asset'lere dokunulmaz. GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan
# durur); kopyalanan asset'lerin kendi manifest'leri de doğrulanır.
#   python make_alpha_denge_v1.py --check    → salt okunur kontrol
#   python make_alpha_denge_v1.py            → kontrol, üretim, manifest kaydı
import glob, hashlib, io, json, math, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
for folder in (DENGE, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import alpha_denge_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
BAL_DIR = 'ScriptableObjects/Balance/AlphaDengeV1'
RW_DIR = 'ScriptableObjects/BossRewards/AlphaDengeV1'
HEALTH = BAL_DIR + '/PlantHealth_AlphaDengeV1.asset'
BALANCE = BAL_DIR + '/RunBalance_AlphaDengeV1.asset'
PROGRESSION = BAL_DIR + '/Progression_AlphaDengeV1.asset'
POOL = RW_DIR + '/BossRewardPool_AlphaDengeV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_AlphaDengeV1.asset'
PATTERNS = (BAL_DIR + '/*', RW_DIR + '/*', PROFILE + '*')
BASE_PROFILE = 'ScriptableObjects/RunProfiles/Run50_XPV1.asset'
BASE_BALANCE = 'ScriptableObjects/Balance/XPV1/RunBalance_XPV1.asset'
BASE_PROGRESSION = 'ScriptableObjects/Balance/XPV1/Progression_XPV1.asset'
BASE_HEALTH = 'ScriptableObjects/Balance/PlantHealth_DengeV1.asset'
BASE_POOL = 'ScriptableObjects/BossRewards/ZincirV1/BossRewardPool_ZincirV1.asset'
BASE_CHAIN = 'ScriptableObjects/BossRewards/ZincirV1/ZincirHasat_Z1.asset'
CHAIN_ID = 'zincir_hasat'
# Kopyalanan asset'lerin sahibi olan üreticiler: (klasör, parametre dosyası, kopyalanan dosya).
SOURCES = (
    ('XPV1', 'xp_v1_params.py', BASE_PROFILE),
    ('XPV1', 'xp_v1_params.py', BASE_BALANCE),
    ('XPV1', 'xp_v1_params.py', BASE_PROGRESSION),
    ('DengeV1', 'denge_v1_params.py', BASE_HEALTH),
    ('ZincirV1', 'zincir_v1_params.py', BASE_POOL),
    ('ZincirV1', 'zincir_v1_params.py', BASE_CHAIN),
)
PERIODS = 15


def read(rel):
    return io.open(os.path.join(A, rel), encoding='utf-8').read().replace('\r\n', '\n')


def code_const(file, name):
    source = io.open(os.path.join(A, 'Scripts', file), encoding='utf-8').read()
    return float(re.search(r'%s = ([\d.]+)f' % name, source).group(1))


def number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool) and not math.isnan(v) and not math.isinf(v)


def validate():
    """Veri düzeltilmez; hatalı parametrede yazmadan durur."""
    low, high = code_const('Managers/RoundDurations.cs', 'MinSeconds'), code_const('Managers/RoundDurations.cs', 'MaxSeconds')
    bands = P.ROUND_DURATIONS
    if not bands or bands[0][0] != 1: raise SystemExit('ROUND_DURATIONS: ilk satır Round 1 olmalı.')
    for i, (start, seconds) in enumerate(bands):
        if not isinstance(start, int) or (i and start <= bands[i - 1][0]) or start > 50: raise SystemExit('ROUND_DURATIONS: başlangıç round\'ları artan ve 1–50 olmalı.')
        if not number(seconds) or not low <= seconds <= high: raise SystemExit('ROUND_DURATIONS: süre %g–%g sn olmalı (oyun kodu: RoundDurations).' % (low, high))
    total = sum(seconds_for(r) for r in range(1, 51))
    if total != P.TOTAL_ACTIVE_SECONDS: raise SystemExit('Süre toplamı %g sn; TOTAL_ACTIVE_SECONDS %g.' % (total, P.TOTAL_ACTIVE_SECONDS))
    anchors = P.HP_ANCHORS
    if anchors[0][0] != 1 or anchors[-1][0] != 50: raise SystemExit('HP_ANCHORS: ilk nokta Round 1, son nokta Round 50 olmalı.')
    for i, (r, hp) in enumerate(anchors):
        if not isinstance(r, int) or not number(hp) or hp < 1 or (i and (r <= anchors[i - 1][0] or hp < anchors[i - 1][1])):
            raise SystemExit('HP_ANCHORS: round\'lar artan, can ≥ 1 ve azalmayan olmalı.')
    if len(P.HP_RARITY_MULTIPLIERS) != 5 or any(not number(v) or v <= 0 for v in P.HP_RARITY_MULTIPLIERS): raise SystemExit('HP_RARITY_MULTIPLIERS: 5 pozitif değer.')
    if len(P.QUOTA_TARGETS) != PERIODS or any(not isinstance(v, int) or v <= 0 for v in P.QUOTA_TARGETS): raise SystemExit('QUOTA_TARGETS: %d pozitif tam sayı.' % PERIODS)
    if len(P.BOSS_TARGETS) != PERIODS or any(not isinstance(v, int) or v < 0 for v in P.BOSS_TARGETS): raise SystemExit('BOSS_TARGETS: %d tam sayı (0: hedef yok).' % PERIODS)
    for name, values, (lo, hi) in (('CHAIN_CHANCE', P.CHAIN_CHANCE, P.CHAIN_CHANCE_RANGE), ('CHAIN_DAMAGE', P.CHAIN_DAMAGE, P.CHAIN_DAMAGE_RANGE)):
        if len(values) != 2 or any(not number(v) or not lo[i] <= v <= hi[i] for i, v in enumerate(values)):
            raise SystemExit('%s izinli aralığın dışında (%s … %s).' % (name, lo, hi))
    for level, cost in P.XP_TABLE_OVERRIDES.items():
        if not isinstance(level, int) or level < 1 or not number(cost) or cost <= 0: raise SystemExit('XP_TABLE_OVERRIDES: {seviye >= 1: maliyet > 0} olmalı.')
    for reward, fields in P.REWARD_OVERRIDES.items():
        if reward == CHAIN_ID and any(f.startswith('chain') for f in fields): raise SystemExit('Zincir çarpanları CHAIN_CHANCE / CHAIN_DAMAGE ile yazılır.')
        if any(not number(v) for v in fields.values()): raise SystemExit('REWARD_OVERRIDES: yalnız sayısal alanlar.')


def seconds_for(round_):
    seconds = P.ROUND_DURATIONS[0][1]
    for start, value in P.ROUND_DURATIONS:
        if start <= round_: seconds = value
    return seconds


validate()
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'alpha_denge_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)


def verify_source(folder, source, rel):
    """Kopyalanan asset, sahibi olan üreticinin incelenmiş (manifest'teki) hâlinde olmalı."""
    manifest = json.load(io.open(os.path.join(HERE, '..', folder, 'generated_manifest.json'), encoding='utf-8'))
    current = generation_guard.snapshot(A, (rel,))
    if rel not in manifest or current.get(rel) != manifest[rel]:
        raise SystemExit('Kopyalanacak veri üreticisinin dışında değişmiş; dosya yazılmadı (%s): %s' % (source, rel))


for folder, source, rel in SOURCES: verify_source(folder, source, rel)

written = set()


def guid_for(key):
    return hashlib.md5(('ClickerGame/AlphaDengeV1/' + key).encode('utf-8')).hexdigest()


def guid_of(rel):
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
    written.update((rel, rel + '.meta'))
    return g


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g


def replace(text, field, value, indent='  '):
    line = '%s%s: %s' % (indent, field, value)
    text, n = re.subn(r'^%s%s: .*$' % (indent, re.escape(field)), lambda _: line, text, flags=re.M)
    assert n == 1, field
    return text


def replace_list(text, field, items, item_lines):
    """Üst düzey bir liste alanını (alan satırı + '  - ' ile başlayan öğeler ve girintili devam satırları) yeniden yazar."""
    pattern = r'^  %s:(?: \[\])?\n(?:  - .*\n(?:    .*\n)*)*' % re.escape(field)
    body = '  %s:\n' % field + ''.join(item_lines(item) for item in items) if items else '  %s: []\n' % field
    text, n = re.subn(pattern, lambda _: body, text, flags=re.M)
    assert n == 1, field
    return text


ensure_folder(BAL_DIR)

# ---------------------------------------------------------------- can eğrisi (bu profilin kendi kopyası)
health_text = replace(read(BASE_HEALTH), 'm_Name', 'PlantHealth_AlphaDengeV1')
health_text = replace_list(health_text, 'anchors', P.HP_ANCHORS, lambda a: '  - round: %d\n    commonHealth: %s\n' % (a[0], fmt(a[1])))
health_text = replace_list(health_text, 'rarityMultipliers', P.HP_RARITY_MULTIPLIERS, lambda v: '  - %s\n' % fmt(v))
health = write_raw(HEALTH, health_text)

# ---------------------------------------------------------------- XP tablosu: yalnız değişiklik varsa kopya
progression = guid_of(BASE_PROGRESSION)
if P.XP_TABLE_OVERRIDES:
    progression_text = replace(read(BASE_PROGRESSION), 'm_Name', 'Progression_AlphaDengeV1')
    head, rest = progression_text.split('  xpRequirements:\n')
    costs = [float(v) for v in re.findall(r'^  - ([-\d.e+]+)$', rest, flags=re.M)]
    tail = rest[rest.index('  tailGrowth:'):]
    for level, cost in sorted(P.XP_TABLE_OVERRIDES.items()):
        if level > len(costs): raise SystemExit('XP_TABLE_OVERRIDES: tablo %d seviye; %d yok.' % (len(costs), level))
        costs[level - 1] = cost
    progression = write_raw(PROGRESSION, head + '  xpRequirements:\n' + ''.join('  - %s\n' % fmt(c) for c in costs) + tail)

# ---------------------------------------------------------------- denge seti: XP V1 seti; can eğrisi (ve varsa XP tablosu) bu profilin
balance_text = replace(read(BASE_BALANCE), 'm_Name', 'RunBalance_AlphaDengeV1')
balance_text = replace(balance_text, 'plantHealth', ref(health))
balance_text = replace(balance_text, 'progression', ref(progression))
balance = write_raw(BALANCE, balance_text)

# ---------------------------------------------------------------- ödüller: yalnız değeri değişen ödülün kopyası + havuz kopyası
pool = guid_of(BASE_POOL)
chain_text = read(BASE_CHAIN)
base_chance = [float(v) for v in re.search(r'  chainChance:\n((?:  - .*\n)+)', chain_text).group(1).replace('  - ', '').split()]
base_damage = [float(v) for v in re.search(r'  chainDamage:\n((?:  - .*\n)+)', chain_text).group(1).replace('  - ', '').split()]
chain_changed = list(map(float, P.CHAIN_CHANCE)) != base_chance or list(map(float, P.CHAIN_DAMAGE)) != base_damage
copies = []
if chain_changed or P.REWARD_OVERRIDES:
    ensure_folder(RW_DIR)
    pool_text = replace(read(BASE_POOL), 'm_Name', 'BossRewardPool_AlphaDengeV1')
    by_guid = {}
    for meta in glob.glob(os.path.join(A, 'ScriptableObjects', 'BossRewards', '**', '*.asset.meta'), recursive=True):
        rel = os.path.relpath(meta[:-5], A).replace('\\', '/')
        by_guid[guid_of(rel)] = rel
    wanted = dict(P.REWARD_OVERRIDES)
    if chain_changed: wanted.setdefault(CHAIN_ID, {})
    for g in re.findall(r'reward: \{fileID: 11400000, guid: ([0-9a-f]+), type: 2\}', pool_text):
        text = read(by_guid[g])
        reward_id = re.search(r'^  id: (.*)$', text, flags=re.M).group(1).strip()
        if reward_id not in wanted: continue
        name = re.search(r'^  m_Name: (.*)$', text, flags=re.M).group(1).strip().rsplit('_', 1)[0] + '_A1'
        text = replace(text, 'm_Name', name)
        for field, value in wanted.pop(reward_id).items(): text = replace(text, field, fmt(value))
        if reward_id == CHAIN_ID:
            text = replace_list(text, 'chainChance', P.CHAIN_CHANCE, lambda v: '  - %s\n' % fmt(v))
            text = replace_list(text, 'chainDamage', P.CHAIN_DAMAGE, lambda v: '  - %s\n' % fmt(v))
        copy = write_raw('%s/%s.asset' % (RW_DIR, name), text)
        assert pool_text.count(g) == 1, reward_id
        pool_text = pool_text.replace(g, copy)
        copies.append(reward_id)
    if wanted: raise SystemExit('REWARD_OVERRIDES: havuzda olmayan ödül: %s' % ', '.join(sorted(wanted)))
    pool = write_raw(POOL, pool_text)

# ---------------------------------------------------------------- profil: XP V1 profili; süre tablosu, hedefler, denge seti (ve havuz)
profile_text = read(BASE_PROFILE)
profile_text = replace(profile_text, 'm_Name', 'Run50_AlphaDengeV1')
profile_text = replace(profile_text, 'displayName', yaml_str(P.DISPLAY_NAME))
profile_text = replace(profile_text, 'victoryTitle', yaml_str(P.VICTORY_TITLE))
profile_text = replace(profile_text, 'balance', ref(balance))
profile_text = replace(profile_text, 'bossRewards', ref(pool))
profile_text = replace_list(profile_text, 'segmentTargets', P.QUOTA_TARGETS, lambda v: '  - %d\n' % v)
profile_text = replace_list(profile_text, 'bossTargets', P.BOSS_TARGETS, lambda v: '  - %d\n' % v)
# Süre tek yerden: sabit süre kapanır, tablo yazılır.
durations = '  roundDurations:\n' + ''.join('  - fromRound: %d\n    seconds: %s\n' % (start, fmt(seconds)) for start, seconds in P.ROUND_DURATIONS)
assert 'roundDurations' not in profile_text
profile_text = replace(profile_text, 'fixedRoundDuration', '0')
profile_text = profile_text.replace('  fixedRoundDuration: 0\n', '  fixedRoundDuration: 0\n' + durations)
assert profile_text.count('  roundDurations:\n') == 1
profile = write_raw(PROFILE, profile_text)

# Bu üretimde yazılmayan eski çıktılar silinir (ör. değeri geri alınan bir ödülün kopyası).
for rel in sorted(generation_guard.snapshot(A, PATTERNS)):
    if rel not in written:
        os.remove(os.path.join(A, rel))
        print('silindi (artik uretilmiyor): ' + rel)
for folder in (RW_DIR,):
    path = os.path.join(A, folder)
    if os.path.isdir(path) and not os.listdir(path):
        os.rmdir(path)
        if os.path.exists(path + '.meta'): os.remove(path + '.meta')

# Konsol çıktısı ASCII tutulur (Windows konsol kodlaması).
print('AlphaDengeV1: profil %s, denge %s, can %s, xp %s, havuz %s'
      % (profile, balance, health, 'kendi kopyasi' if P.XP_TABLE_OVERRIDES else 'XP V1 tablosu', 'kendi kopyasi (%s)' % ', '.join(copies) if copies else 'Zincir V1 havuzu'))
print('  sure: %s; toplam %d sn' % (', '.join('R%d+ %gs' % b for b in P.ROUND_DURATIONS), P.TOTAL_ACTIVE_SECONDS))
print('  can (Common): ' + ', '.join('R%d %g' % a for a in P.HP_ANCHORS))
print('  kota: %s' % P.QUOTA_TARGETS)
print('  boss: %s' % P.BOSS_TARGETS)
print('  zincir: sans %s, hasar %s' % (P.CHAIN_CHANCE, P.CHAIN_DAMAGE))
generation_guard.record(A, MANIFEST, PATTERNS)
