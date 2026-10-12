# Run50_GecOyunV1 üreticisi (Bölüm 3.8). Sayılar gec_oyun_v1_params.py içinde.
# Yazdıkları:
#   - RunEffects/GecOyunV1/<Ad>Effect_G1.asset          yeni ödüllerin çalışma zamanı etkileri (kural sınıfı + prototip sayılar)
#   - BossRewards/GecOyunV1/<Ad>_G1.asset               yeni ödüller (kart metni, aşama dışı alanlar varsayılan; etki ve ön koşul bağlı)
#   - BossRewards/GecOyunV1/BossRewardPool_GecOyunV1.asset  havuz: Alpha Denge V1 havuzunun kopyası + yeni ödüller aşamalarının SONUNA
#   - FieldEvents/GecOyunV1/FieldEventPlan_GecOyunV1.asset  tarla olayı planı
#   - RunProfiles/Run50_GecOyunV1.asset                 profil: Run50_AlphaDengeV1'in kopyası; havuz, olay planı ve adlar farklı
# Eski asset'lere dokunulmaz: mevcut 19 ödül, Alpha havuzundaki asset'lerin KENDİSİDİR (kopyalanmaz), sıraları ve notları aynen kalır.
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur); kopyalanan Alpha
# asset'lerinin kendi manifest'i de doğrulanır. Script GUID'leri .cs.meta dosyalarından okunur.
#   python make_gec_oyun_v1.py --check    → salt okunur kontrol
#   python make_gec_oyun_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, json, math, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
for folder in (DENGE, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import gec_oyun_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
FX_DIR = 'ScriptableObjects/RunEffects/GecOyunV1'
RW_DIR = 'ScriptableObjects/BossRewards/GecOyunV1'
EV_DIR = 'ScriptableObjects/FieldEvents/GecOyunV1'
POOL = RW_DIR + '/BossRewardPool_GecOyunV1.asset'
PLAN = EV_DIR + '/FieldEventPlan_GecOyunV1.asset'
PROFILE = 'ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset'
PATTERNS = (FX_DIR + '/*', RW_DIR + '/*', EV_DIR + '/*', PROFILE + '*')
BASE_PROFILE = 'ScriptableObjects/RunProfiles/Run50_AlphaDengeV1.asset'
BASE_POOL = 'ScriptableObjects/BossRewards/AlphaDengeV1/BossRewardPool_AlphaDengeV1.asset'
STAGES = ('erken', 'orta', 'guclu')
EFFECT_SCRIPTS = 'Scripts/RunEffects/Effects/%s.cs'
REWARD_SCRIPT = 'Scripts/ScriptableObjects/BossRewardSO.cs'
PLAN_SCRIPT = 'Scripts/FieldEvents/FieldEventPlanSO.cs'


def read(rel):
    return io.open(os.path.join(A, rel), encoding='utf-8').read().replace('\r\n', '\n')


def number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool) and not math.isnan(v) and not math.isinf(v)


def validate():
    """Veri düzeltilmez; hatalı parametrede yazmadan durur."""
    ids = [r['id'] for r in P.REWARDS]
    if len(set(ids)) != len(ids): raise SystemExit('REWARDS: aynı kimlik iki kez yazılmış.')
    if len(set(r['asset'] for r in P.REWARDS)) != len(ids): raise SystemExit('REWARDS: aynı asset adı iki kez yazılmış.')
    stage_of = {r['id']: STAGES.index(r['stage']) for r in P.REWARDS if r['stage'] in STAGES}
    for r in P.REWARDS:
        if r['stage'] not in STAGES: raise SystemExit('%s: aşama erken / orta / guclu olmalı.' % r['id'])
        if r['condition'] not in (P.ALWAYS, P.ANY_BEHAVIOR_PLANTER): raise SystemExit('%s: koşul 0 ya da 2 olmalı.' % r['id'])
        if r['requires'] is not None:
            if r['requires'] not in stage_of: raise SystemExit('%s: ön koşul %s bu listede yok.' % (r['id'], r['requires']))
            if stage_of[r['requires']] > stage_of[r['id']]: raise SystemExit('%s: ön koşul daha geç aşamada.' % r['id'])
        if not r['fields'] or any(not number(v) or v < 0 for v in r['fields'].values()):
            raise SystemExit('%s: etki alanları sonlu ve negatif olmayan sayı olmalı.' % r['id'])
        for text in (r['name'], r['label'], r['note']):
            if not isinstance(text, str) or not text.strip(): raise SystemExit('%s: ad, etiket ve açıklama boş olamaz.' % r['id'])
        if not os.path.exists(os.path.join(A, EFFECT_SCRIPTS % r['effect'])): raise SystemExit('%s: etki sınıfı yok: %s' % (r['id'], r['effect']))
    E = P.FIELD_EVENTS
    if not isinstance(E['firstRound'], int) or not 26 <= E['firstRound'] <= 50: raise SystemExit('firstRound 26–50 olmalı (olaylar R26\'dan önce açılmaz).')
    if not number(E['startDelay']) or E['startDelay'] < 0: raise SystemExit('startDelay geçersiz.')
    if not number(E['normalRoundChance']) or not 0 <= E['normalRoundChance'] <= 1: raise SystemExit('normalRoundChance 0–1 olmalı.')
    for name in ('bossRounds', 'normalRounds'):
        for kind, weight in E[name]:
            if kind not in (0, 1, 2, 3) or not number(weight) or weight < 0: raise SystemExit('%s: (olay 0–3, ağırlık >= 0).' % name)
    for round_, kind in E['pinned']:
        if not isinstance(round_, int) or round_ < E['firstRound'] or kind not in (0, 1, 2, 3): raise SystemExit('pinned: (round >= firstRound, olay 0–3).')
    for name in ('thornSiege', 'invasiveSprout', 'graftedShell', 'wanderingBounty'):
        for key, value in E[name].items():
            values = value.values() if key == 'timing' else (value,)
            if any(not number(v) or v < 0 for v in values): raise SystemExit('%s.%s geçersiz.' % (name, key))
    if E['invasiveSprout']['slowMultiplier'] < 1: raise SystemExit('slowMultiplier 1\'den küçük olamaz.')


validate()
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'gec_oyun_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)

# Kopyalanan Alpha asset'leri, Alpha üreticisinin incelenmiş (manifest'teki) hâlinde olmalı.
alpha_manifest = json.load(io.open(os.path.join(HERE, '..', 'AlphaDengeV1', 'generated_manifest.json'), encoding='utf-8'))
for rel in (BASE_PROFILE, BASE_POOL):
    if generation_guard.snapshot(A, (rel,)).get(rel) != alpha_manifest.get(rel):
        raise SystemExit('Kopyalanacak veri Alpha Denge V1 üreticisinin dışında değişmiş; dosya yazılmadı: ' + rel)

written = set()


def guid_for(key):
    return hashlib.md5(('ClickerGame/GecOyunV1/' + key).encode('utf-8')).hexdigest()


def guid_of(rel):
    for line in io.open(os.path.join(A, rel + '.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(rel)


def ensure_folder(rel):
    path = os.path.join(A, rel)
    parent = os.path.dirname(rel)
    if parent and not os.path.isdir(os.path.join(A, parent)): ensure_folder(parent)
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


def header(script_rel, name, cls):
    return ('%%YAML 1.1\n%%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
            '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: Assembly-CSharp::%s\n'
            % (guid_of(script_rel), name, cls))


def replace(text, field, value, indent='  '):
    line = '%s%s: %s' % (indent, field, value)
    text, n = re.subn(r'^%s%s: .*$' % (indent, re.escape(field)), lambda _: line, text, flags=re.M)
    assert n == 1, field
    return text


for folder in (FX_DIR, RW_DIR, EV_DIR): ensure_folder(folder)

# ---------------------------------------------------------------- etkiler ve ödüller
effect_guid, reward_guid = {}, {}
for r in P.REWARDS:
    name = r['asset'] + 'Effect_G1'
    text = header(EFFECT_SCRIPTS % r['effect'], name, r['effect'])
    text += ''.join('  %s: %s\n' % (field, fmt(value)) for field, value in r['fields'].items())
    effect_guid[r['id']] = write_raw('%s/%s.asset' % (FX_DIR, name), text)
# Ödül GUID'leri ön koşul bağlanmadan önce bilinmelidir.
for r in P.REWARDS: reward_guid[r['id']] = guid_for('%s/%s_G1.asset' % (RW_DIR, r['asset']))
for r in P.REWARDS:
    name = r['asset'] + '_G1'
    text = header(REWARD_SCRIPT, name, 'BossRewardSO')
    text += ('  id: %s\n  displayName: %s\n  effectLabel: %s\n  inverseLabel: \n  note: %s\n  modifiersPerStack: []\n'
             '  directDamageMultiplier: 1\n  rareDirectMultiplier: 1\n  rareDirectFrom: 2\n  behaviorDamageMultiplier: 1\n'
             '  echo: 0\n  echoDelay: 0.2\n  echoDamage: 0.7\n  echoRadius: 1.25\n  rhythmHarvests: 0\n  rhythmDamage: 1.5\n  rhythmRadius: 1.5\n'
             '  maxStacks: 1\n  weight: 1\n  condition: %d\n  conditionStat: 0\n  conditionThreshold: 0.7\n  minRound: 0\n  maxRound: 0\n'
             '  levelChoiceDelta: 0\n  exclusiveGroup: \n  fullGameExclusiveGroup: 0\n  movingBehaviorLimitBonus: 0\n  echoReach: 0\n  chain: 0\n'
             '  effect: %s\n  requires: %s\n'
             % (r['id'], yaml_str(r['name']), yaml_str(r['label']), yaml_str(r['note']), r['condition'],
                ref(effect_guid[r['id']]), ref(reward_guid[r['requires']]) if r['requires'] else '{fileID: 0}'))
    assert write_raw('%s/%s.asset' % (RW_DIR, name), text) == reward_guid[r['id']]

# ---------------------------------------------------------------- havuz: Alpha havuzu + yeni ödüller aşamalarının sonuna
lines = read(BASE_POOL).split('\n')
lines = [l.replace('m_Name: BossRewardPool_AlphaDengeV1', 'm_Name: BossRewardPool_GecOyunV1') if l.startswith('  m_Name:') else l for l in lines]
starts = {m.group(1): i for i, l in enumerate(lines) for m in [re.match(r'^  - id: (\w+)$', l)] if m}
assert all(s in starts for s in STAGES), starts
end_of = {}
order = sorted(starts.values())
tail = next(i for i, l in enumerate(lines) if l.startswith('  stageDistanceWeights:'))
for stage in STAGES:
    later = [i for i in order if i > starts[stage]]
    end_of[stage] = min(later) if later else tail
# Sondan başa eklenir: önceki aşamaların satır numaraları kaymaz.
for stage in sorted(STAGES, key=lambda s: -starts[s]):
    entries = []
    for r in P.REWARDS:
        if r['stage'] == stage: entries += ['    - reward: %s' % ref(reward_guid[r['id']]), '      note: ']
    lines[end_of[stage]:end_of[stage]] = entries
pool = write_raw(POOL, '\n'.join(lines))

# ---------------------------------------------------------------- tarla olayı planı
E = P.FIELD_EVENTS


def weights(field, items):
    if not items: return '  %s: []\n' % field
    return '  %s:\n' % field + ''.join('  - kind: %d\n    weight: %s\n' % (kind, fmt(weight)) for kind, weight in items)


def block(field, data):
    out = '  %s:\n' % field
    for key, value in data.items():
        if key == 'timing': out += '    timing:\n' + ''.join('      %s: %s\n' % (k, fmt(v)) for k, v in value.items())
        else: out += '    %s: %s\n' % (key, fmt(value))
    return out


plan_text = header(PLAN_SCRIPT, 'FieldEventPlan_GecOyunV1', 'FieldEventPlanSO')
plan_text += '  firstRound: %d\n  startDelay: %s\n' % (E['firstRound'], fmt(E['startDelay']))
plan_text += weights('bossRounds', E['bossRounds'])
plan_text += '  normalRoundChance: %s\n' % fmt(E['normalRoundChance'])
plan_text += weights('normalRounds', E['normalRounds'])
plan_text += ('  pinned:\n' + ''.join('  - round: %d\n    kind: %d\n' % p for p in E['pinned'])) if E['pinned'] else '  pinned: []\n'
for name in ('thornSiege', 'invasiveSprout', 'graftedShell', 'wanderingBounty'): plan_text += block(name, E[name])
plan = write_raw(PLAN, plan_text)

# ---------------------------------------------------------------- profil: Alpha profili; havuz, olay planı ve adlar farklı
profile_text = read(BASE_PROFILE)
profile_text = replace(profile_text, 'm_Name', 'Run50_GecOyunV1')
profile_text = replace(profile_text, 'displayName', yaml_str(P.DISPLAY_NAME))
profile_text = replace(profile_text, 'victoryTitle', yaml_str(P.VICTORY_TITLE))
profile_text = replace(profile_text, 'bossRewards', ref(pool))
assert 'fieldEvents' not in profile_text
profile_text, n = re.subn(r'^  victoryTitle: ', lambda _: '  fieldEvents: %s\n  victoryTitle: ' % ref(plan), profile_text, flags=re.M)
assert n == 1
profile = write_raw(PROFILE, profile_text)

# Bu üretimde yazılmayan eski çıktılar silinir (ör. listeden çıkarılan bir ödül).
for rel in sorted(generation_guard.snapshot(A, PATTERNS)):
    if rel not in written:
        os.remove(os.path.join(A, rel))
        print('silindi (artik uretilmiyor): ' + rel)

# Konsol çıktısı ASCII tutulur (Windows konsol kodlaması).
print('GecOyunV1: profil %s, havuz %s, plan %s' % (profile, pool, plan))
for stage in STAGES: print('  %s: +%s' % (stage, ', '.join(r['id'] for r in P.REWARDS if r['stage'] == stage)))
print('  tarla olaylari: ilk round %d, boss %s, normal %s (olasilik %g)' % (E['firstRound'], E['bossRounds'], E['normalRounds'], E['normalRoundChance']))
generation_guard.record(A, MANIFEST, PATTERNS)
