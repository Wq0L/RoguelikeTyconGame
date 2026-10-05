# Run50_XPV1 üreticisi (Bölüm 3.7.7). Sayılar xp_v1_params.py içinde.
# Yazdıkları: XP tablosu ve denge seti (Balance/XPV1/), profil (RunProfiles/Run50_XPV1.asset).
#   - XP tablosu: Progression_DengeV1'in kopyası + tablo sonrası büyüme katsayısı (tailGrowth).
#   - Denge seti: RunBalance_KirilmaV1'in kopyası; yalnız XP tablosu ve temel güç kartı kuralları farklı.
#   - Profil: Run50_ZincirV1'in kopyası; yalnız ad, zafer başlığı ve denge seti farklı.
# Eski asset'lere dokunulmaz. GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa
# yazmadan durur); kopyalanan asset'lerin kendi manifest'leri de doğrulanır.
#   python make_xp_v1.py --check    → salt okunur kontrol
#   python make_xp_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, math, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
for folder in (DENGE, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import xp_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
DIR = 'ScriptableObjects/Balance/XPV1'
PROGRESSION = DIR + '/Progression_XPV1.asset'
BALANCE = DIR + '/RunBalance_XPV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_XPV1.asset'
PATTERNS = (DIR + '/*', PROFILE + '*')
BASE_PROGRESSION = 'ScriptableObjects/Balance/Progression_DengeV1.asset'
BASE_BALANCE = 'ScriptableObjects/Balance/RunBalance_KirilmaV1.asset'
BASE_PROFILE = 'ScriptableObjects/RunProfiles/Run50_ZincirV1.asset'
# Kopyalanan asset'lerin sahibi olan üreticiler: (klasör, parametre dosyası, kopyalanan dosya).
SOURCES = (
    ('DengeV1', 'denge_v1_params.py', BASE_PROGRESSION),
    ('KirilmaV1', 'kirilma_v1_params.py', BASE_BALANCE),
    ('ZincirV1', 'zincir_v1_params.py', BASE_PROFILE),
)


def validate():
    """Veri düzeltilmez; hatalı parametrede yazmadan durur."""
    s = P.TAIL_GROWTH
    if not isinstance(s, (int, float)) or math.isnan(s) or math.isinf(s) or s < 0:
        raise SystemExit('TAIL_GROWTH sonlu ve >= 0 olmalı (0: tablo sonunda sabit maliyet).')
    for level, cost in P.TABLE_OVERRIDES.items():
        if not isinstance(level, int) or level < 1 or not cost > 0 or math.isinf(cost):
            raise SystemExit('TABLE_OVERRIDES: {seviye >= 1: maliyet > 0} olmalı.')


validate()
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'xp_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)


def verify_source(folder, source, rel):
    """Kopyalanan asset, sahibi olan üreticinin incelenmiş (manifest'teki) hâlinde olmalı."""
    import json
    manifest = json.load(io.open(os.path.join(HERE, '..', folder, 'generated_manifest.json'), encoding='utf-8'))
    current = generation_guard.snapshot(A, (rel,))
    if rel not in manifest or current.get(rel) != manifest[rel]:
        raise SystemExit('Kopyalanacak veri üreticisinin dışında değişmiş; dosya yazılmadı (%s): %s' % (source, rel))


for folder, source, rel in SOURCES: verify_source(folder, source, rel)


def guid_for(key):
    return hashlib.md5(('ClickerGame/XPV1/' + key).encode('utf-8')).hexdigest()


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


def replace(text, field, value, indent='  '):
    line = '%s%s: %s' % (indent, field, value)
    text, n = re.subn(r'^%s%s: .*$' % (indent, field), lambda _: line, text, flags=re.M)
    assert n == 1, field
    return text


ensure_folder(DIR)

# ---------------------------------------------------------------- XP tablosu: DengeV1 tablosu + tablo sonrası büyüme
progression_text = replace(read(BASE_PROGRESSION), 'm_Name', 'Progression_XPV1')
head, table = progression_text.split('  xpRequirements:\n')
costs = [float(v) for v in re.findall(r'^  - ([-\d.e+]+)$', table, flags=re.M)]
assert len(costs) >= 2 and table.strip().count('\n') + 1 == len(costs), 'XP tablosu okunamadı'
for level, cost in sorted(P.TABLE_OVERRIDES.items()):
    if level > len(costs): raise SystemExit('TABLE_OVERRIDES: tablo %d seviye; %d yok.' % (len(costs), level))
    costs[level - 1] = cost
progression_text = head + '  xpRequirements:\n' + ''.join('  - %s\n' % fmt(c) for c in costs) + '  tailGrowth: %s\n' % fmt(P.TAIL_GROWTH)
progression = write_raw(PROGRESSION, progression_text)

# ---------------------------------------------------------------- denge seti: Kırılma V1 seti; yalnız XP tablosu ve temel güç kartı kuralları
balance_text = replace(read(BASE_BALANCE), 'm_Name', 'RunBalance_XPV1')
balance_text = replace(balance_text, 'progression', ref(progression))
rules = '  additiveBaseXpCards: %d\n  hideFlooredBaseStats: %d\n' % (int(P.ADDITIVE_BASE_XP_CARDS), int(P.HIDE_FLOORED_BASE_STATS))
assert 'additiveBaseXpCards' not in balance_text and balance_text.count('\n  planterPrices:') == 1
balance_text = balance_text.replace('  planterPrices:', rules + '  planterPrices:')
balance = write_raw(BALANCE, balance_text)

# ---------------------------------------------------------------- profil: Zincir V1 ile aynı; yalnız ad, başlık, denge seti
profile_text = read(BASE_PROFILE)
profile_text = replace(profile_text, 'm_Name', 'Run50_XPV1')
profile_text = replace(profile_text, 'displayName', yaml_str(P.DISPLAY_NAME))
profile_text = replace(profile_text, 'victoryTitle', yaml_str(P.VICTORY_TITLE))
profile_text = replace(profile_text, 'balance', ref(balance))
profile = write_raw(PROFILE, profile_text)

n = len(costs)
print('XPV1: profil %s, denge %s, xp tablosu %s (N = %d, C(N) = %s, s = %s; C(N+1) = %s, C(N+100) = %s; toplanan XP karti %d; islevsiz kart suzgeci %d)'
      % (profile, balance, progression, n, fmt(costs[-1]), fmt(P.TAIL_GROWTH), fmt(costs[-1] * (1 + P.TAIL_GROWTH)), fmt(costs[-1] * (1 + P.TAIL_GROWTH * 100)),
         int(P.ADDITIVE_BASE_XP_CARDS), int(P.HIDE_FLOORED_BASE_STATS)))
generation_guard.record(A, MANIFEST, PATTERNS)
