# Run50_DengeV1 veri üreticisi (Bölüm 3.5). Bütün denge sayıları denge_v1_params.py içinde; bu betik asset'leri yazar.
# GUID'ler ad üzerinden sabittir: yeniden üretmek referansları bozmaz. Ortak asset'lere (eski profiller) dokunmaz.
import io, os, sys, json, hashlib, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import treelib
from treelib import yaml_str, fmt
import denge_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
import generation_guard
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated_manifest.json')
# Validate every existing output before ensure_folder/write/delete can modify anything.
generation_guard.verify(A, MANIFEST)
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
S = lambda n: treelib.STAT.index(n)
PLAYER, PLANTER, ALL, GRID, MUTATION = 1, 2, 0, 4, 5
FLAT, ADD, MORE, SET = 0, 1, 2, 3
STONE, IRON, GOLD = 0, 1, 2
CUR = {'G': GOLD, 'I': IRON, 'S': STONE}


def guid_for(key):
    return hashlib.md5(('ClickerGame/DengeV1/' + key).encode('utf-8')).hexdigest()


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


def write_asset(rel, script, name, body, folder='Scripts/ScriptableObjects'):
    head = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
            '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            + '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: Assembly-CSharp::%s\n'
            % (script_guid(script if script != 'CoreStatsSO' else 'CoreStatSO', folder), yaml_str(name), script))
    path = os.path.join(A, rel)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(head + body)
    g = guid_for(rel)
    io.open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(treelib.meta_yaml(g))
    return g


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g if g else '{fileID: 0}'


def nice(v):
    v = float(v)
    if v < 30: return max(1, int(round(v)))
    if v < 200: return int(round(v / 5.0) * 5)
    if v < 1000: return int(round(v / 10.0) * 10)
    step = 10 ** (int(math.floor(math.log10(v))) - 1)
    return int(round(v / step) * step)


# ---------------------------------------------------------------- tree
src = treelib.read_nodes()
by_name = {n['name']: n for n in src.values()}
TREE_DIR = 'ScriptableObjects/Skill Tree Upgrades/DengeV1'
ensure_folder(TREE_DIR)
new_guid = lambda name: guid_for('tree/' + name)

nodes_out = {}   # name -> dict(pos, prereq[(name, level)], target, tiers, unlock)
summary = []


def tier_costs(total, count, growth, scale):
    weights = [1 + growth * k for k in range(count)]
    return [nice(total * scale * w / sum(weights)) for w in weights]


def family(fam):
    names = fam['nodes']
    tier_counts = fam.get('tiers', [3] + [2] * (len(names) - 1))
    n_tiers = sum(tier_counts)
    cur = fam['cost'][-1]
    total_cost = float(fam['cost'][:-1])
    costs = tier_costs(total_cost, n_tiers, fam.get('growth', P.COST_GROWTH), P.COST_SCALE[cur])
    k = 0
    for i, name in enumerate(names):
        tiers = []
        for j in range(1, tier_counts[i] + 1):
            effects = []
            for stat, target, op, total in fam['effects']:
                if op == MORE:
                    value = math.pow(total, j / float(n_tiers)) - 1.0   # total: toplam çarpan (ör. 0.75)
                else:
                    value = total * j / float(n_tiers)
                effects.append((S(stat), target, op, value))
            tiers.append({'costType': CUR[cur], 'cost': costs[k], 'effects': effects})
            k += 1
        if i == 0:
            prereq = fam.get('prereq', [])
        else:
            prereq = [(names[i - 1], tier_counts[i - 1])]
        pos = fam.get('pos', {}).get(name, by_name[name]['pos'])
        nodes_out[name] = dict(pos=pos, prereq=prereq, target=fam['window'], tiers=tiers, unlock=0)
    summary.append(dict(family=fam['family'], nodes=len(names), tiers=n_tiers, cost='%d%s' % (sum(costs), cur), window=fam['window'],
                        effects=[(s, total) for s, _, _, total in fam['effects']], prereq=fam.get('prereq', [])))


def single(name, tiers, prereq, window, unlock=0, pos=None, label=None):
    out = []
    for cost, effects in tiers:
        cur = cost[-1]
        out.append({'costType': CUR[cur], 'cost': nice(float(cost[:-1]) * P.COST_SCALE[cur]),
                    'effects': [(S(s), t, o, v) for s, t, o, v in effects]})
    nodes_out[name] = dict(pos=pos or by_name[name]['pos'], prereq=prereq, target=window, tiers=out, unlock=unlock)
    summary.append(dict(family=label or name, nodes=1, tiers=len(out), cost=' + '.join('%d%s' % (t['cost'], 'SIG'[t['costType']]) for t in out),
                        window=window, effects=[(s, v) for _, effs in tiers for s, _, _, v in effs], prereq=prereq))


for fam in P.FAMILIES: family(fam)
for s in P.SINGLES: single(**s)

# önkoşul adları bu setin içinde olmalı
for name, n in nodes_out.items():
    for pre, level in n['prereq']:
        assert pre in nodes_out, (name, pre)
        assert level <= len(nodes_out[pre]['tiers']), (name, pre, level)
    assert name in by_name, name   # her V1 düğümü bir arayüz yuvasına (ortak düğüm) oturur

for name, n in nodes_out.items():
    text = treelib.node_yaml(name, name, n['pos'], [(new_guid(p), l) for p, l in n['prereq']], n['target'], n['tiers'], n['unlock'])
    path = os.path.join(A, TREE_DIR, by_name[name]['file'])
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)
    io.open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(treelib.meta_yaml(new_guid(name)))

# setten çıkan eski dosyaları temizle (yeniden üretimde düğüm kaldırıldıysa)
keep = {by_name[n]['file'] for n in nodes_out}
for f in os.listdir(os.path.join(A, TREE_DIR)):
    if f.endswith('.asset') and f not in keep:
        os.remove(os.path.join(A, TREE_DIR, f)); os.remove(os.path.join(A, TREE_DIR, f + '.meta'))

slots = ''.join('  - source: %s\n    node: %s\n' % (ref(n['guid']), ref(new_guid(n['name'])) if n['name'] in nodes_out else '{fileID: 0}')
                for n in sorted(src.values(), key=lambda x: x['name']))
ensure_folder('ScriptableObjects/Balance')
tree_set = write_asset('ScriptableObjects/Balance/SkillTreeSet_DengeV1.asset', 'SkillTreeSetSO', 'SkillTreeSet_DengeV1', '  slots:\n' + slots)

# ---------------------------------------------------------------- core stats, health, xp
core_src = io.open(os.path.join(A, 'ScriptableObjects/Stats/CoreStat/CoreStat.asset'), encoding='utf-8').read()
import re
entries = re.findall(r'- statType: (\d+)\s+value: ([-\d.e]+)', core_src)
core_body = '  stats:\n'
for st, val in entries:
    name = treelib.STAT[int(st)]
    v = P.CORE.get(name, float(val))
    core_body += '  - statType: %s\n    value: %s\n' % (st, fmt(v))
core = write_asset('ScriptableObjects/Balance/CoreStat_DengeV1.asset', 'CoreStatsSO', 'CoreStat_DengeV1', core_body)

hp_body = '  referenceHealth: 10\n  anchors:\n' + ''.join('  - round: %d\n    commonHealth: %s\n' % (r, fmt(h)) for r, h in P.HP_ANCHORS)
hp_body += '  rarityMultipliers:\n' + ''.join('  - %s\n' % fmt(1 + P.HP_RARITY_K * n) for n in range(5))
health = write_asset('ScriptableObjects/Balance/PlantHealth_DengeV1.asset', 'PlantHealthScalingSO', 'PlantHealth_DengeV1', hp_body)

xp = [nice(P.xp_requirement(level)) for level in range(1, P.XP_LEVELS + 1)]
xp_body = '  baseXP: 5\n  xpMultiplier: 1.2\n  useAuthoredRequirements: 1\n  xpRequirements:\n' + ''.join('  - %d\n' % v for v in xp)
progression = write_asset('ScriptableObjects/Balance/Progression_DengeV1.asset', 'ProgressionSO', 'Progression_DengeV1', xp_body)

prices = ''
for planter, (cost, cur) in P.PLANTER_PRICES.items():
    prices += '  - planter: %s\n    costType: %d\n    cost: %d\n' % (ref(asset_guid('ScriptableObjects/Planters/%s.asset' % planter)), CUR[cur], cost)
balance_body = ('  coreStats: %s\n  plantHealth: %s\n  progression: %s\n  skillTree: %s\n  rarityXpMultipliers:\n' % (ref(core), ref(health), ref(progression), ref(tree_set))
                + ''.join('  - %s\n' % fmt(v) for v in P.RARITY_XP)
                + ('  planterBaseStats:\n' + ''.join('  - statType: %d\n    value: %s\n' % (S(k), fmt(v)) for k, v in P.PLANTER_BASE.items()) if P.PLANTER_BASE else '  planterBaseStats: []\n')
                + ('  planterPrices:\n' + prices if prices else '  planterPrices: []\n'))
balance = write_asset('ScriptableObjects/Balance/RunBalance_DengeV1.asset', 'RunBalanceSO', 'RunBalance_DengeV1', balance_body)

# ---------------------------------------------------------------- bosses
BOSS_DIR = 'ScriptableObjects/SegmentEvents/DengeV1'
ensure_folder(BOSS_DIR)
b = P.BOSSES
don = write_asset(BOSS_DIR + '/DonCephesi_V1.asset', 'FrostFrontSO', 'DonCephesi_V1',
    '  displayName: %s\n  ruleText: %s\n  color: {r: 0.55, g: 0.82, b: 1, a: 1}\n  spawnIntervalMultiplier: %s\n  coverage: %s\n  seed: 0\n'
    % (yaml_str('DON CEPHESİ'), yaml_str('Mavi şeritteki üretim noktaları daha yavaş üretir. Bitki, saksı ve tile silinmez.'), fmt(b['don_multiplier']), fmt(b['don_coverage'])))
shell = write_asset(BOSS_DIR + '/SertKabuk_V1.asset', 'HardShellSO', 'SertKabuk_V1',
    '  displayName: %s\n  ruleText: %s\n  color: {r: 1, g: 0.62, b: 0.25, a: 1}\n  healthMultiplier: %s\n  coverage: %s\n'
    % (yaml_str('SERT KABUK'), yaml_str("Turuncu şeritte bitkiler daha canlıdır: boss round'unda doğanlar ve o an yaşayanlar. Boss bitince normale döner."),
       fmt(b['shell_multiplier']), fmt(b['shell_coverage'])))
fog = write_asset(BOSS_DIR + '/Sis_V1.asset', 'FogSO', 'Sis_V1',
    '  displayName: %s\n  ruleText: %s\n  color: {r: %s, g: %s, b: %s, a: 1}\n  radiusMultiplier: %s\n  minPlayerRadius: %s\n%s'
    % (yaml_str('SİS'), yaml_str(b['fog_rule']), fmt(b['fog_color'][0]), fmt(b['fog_color'][1]), fmt(b['fog_color'][2]), fmt(b['fog_multiplier']), fmt(b['fog_min_radius']), '  stepDown: %d\n' % (1 if b.get('fog_step_down') else 0)))
ensure_folder('ScriptableObjects/Bosses')
boss_pool = write_asset('ScriptableObjects/Bosses/BossPool_DengeV1.asset', 'BossPoolSO', 'BossPool_DengeV1',
    '  entries:\n' + ''.join('  - boss: %s\n    weight: %s\n' % (ref(g), fmt(w)) for g, w in [(don, b['don_weight']), (shell, b['shell_weight']), (fog, b['fog_weight'])]))

# ---------------------------------------------------------------- rewards
RW_DIR = 'ScriptableObjects/BossRewards/DengeV1'
ensure_folder(RW_DIR)
reward_guids = []
for r in P.REWARDS:
    mods = r.get('mods', [])
    m = '  modifiersPerStack: []\n' if not mods else '  modifiersPerStack:\n' + ''.join(
        '  - statType: %d\n    target: %d\n    operation: %d\n    value: %s\n' % (S(s), t, o, fmt(v)) for s, t, o, v in mods)
    body = ('  id: %s\n  displayName: %s\n  effectLabel: %s\n  inverseLabel: %s\n  note: %s\n%s'
            '  directDamageMultiplier: %s\n  rareDirectMultiplier: %s\n  rareDirectFrom: %d\n  behaviorDamageMultiplier: %s\n'
            '  maxStacks: %d\n  weight: %s\n  condition: %d\n  conditionThreshold: %s\n  minRound: %d\n  maxRound: %d\n'
            % (r['id'], yaml_str(r['name']), yaml_str(r['label']), yaml_str(r.get('inverse', '')) if r.get('inverse') else '""', yaml_str(r['note']), m,
               fmt(r.get('direct', 1)), fmt(r.get('rare_direct', 1)), r.get('rare_from', 2), fmt(r.get('behavior', 1)),
               r.get('max', 3), fmt(r.get('weight', 1)), r.get('condition', 0), fmt(r.get('threshold', 0.7)), r.get('min_round', 0), r.get('max_round', 0)))
    reward_guids.append(write_asset('%s/%s.asset' % (RW_DIR, r['file']), 'BossRewardSO', r['file'], body))
reward_pool = write_asset(RW_DIR + '/BossRewardPool_DengeV1.asset', 'BossRewardPoolSO', 'BossRewardPool_DengeV1',
    '  rewards:\n' + ''.join('  - %s\n' % ref(g) for g in reward_guids) + '  choices: %d\n' % P.REWARD_CHOICES)

# ---------------------------------------------------------------- profile
profile_body = ('  displayName: %s\n  runLength: 50\n  segmentRounds: 5\n  quotaStart: 10\n  quotaGrowth: 1.45\n  segmentTargets:\n%s'
                '  events: []\n  specializationAfterSegment: 0\n  specializationOptions: []\n  bossPool: %s\n  bossTargets:\n%s'
                '  bossRewards: %s\n  bossSeed: 0\n  balance: %s\n  startingGold: %d\n  startingIron: 0\n  startingStone: 0\n  debugBudget: 0\n  electricMode: 0\n'
                '  electricCharge:\n    referenceSeconds: 10\n    referenceChance: 0.5\n    minimumSeconds: 3\n    quickChargeMultiplier: 1\n'
                '  fixedRoundDuration: %s\n  victoryTitle: %s\n'
                % (yaml_str('Run50 Denge V1 · 50 round'), ''.join('  - %d\n' % t for t in P.SEGMENT_TARGETS), ref(boss_pool),
                   ''.join('  - %d\n' % t for t in P.BOSS_TARGETS), ref(reward_pool), ref(balance), P.START_GOLD, fmt(P.ROUND_SECONDS),
                   yaml_str('RUN TAMAMLANDI · DENGE V1')))
profile = write_asset('ScriptableObjects/RunProfiles/Run50_DengeV1.asset', 'RunProfileSO', 'Run50_DengeV1', profile_body)

# ---------------------------------------------------------------- summary
total = {'G': 0, 'I': 0, 'S': 0}
for n in nodes_out.values():
    for t in n['tiers']: total['SIG'[t['costType']]] += t['cost']
out = dict(nodes=len(nodes_out), tiers=sum(len(n['tiers']) for n in nodes_out.values()), total_cost=total, families=summary,
           xp=xp[:90], hp=P.HP_ANCHORS, removed=sorted(n for n in by_name if n not in nodes_out))
io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'denge_v1_summary.json'), 'w', encoding='utf-8').write(json.dumps(out, ensure_ascii=False, indent=1))
print('DengeV1: %d düğüm, %d kademe, toplam %s; kaldırılan %d düğüm; profil %s' % (out['nodes'], out['tiers'], total, len(out['removed']), profile))
generation_guard.record(A, MANIFEST)
