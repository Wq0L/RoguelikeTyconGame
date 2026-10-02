# Belge tabloları: ortak (eski) veri ile DengeV1 verisini asset'lerden okuyup Markdown üretir (elle kopyalama hatası olmasın).
import io, os, re, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import treelib
import denge_v1_params as P

A = os.path.join(treelib.ROOT, 'Assets')
old = treelib.read_nodes()
new = treelib.read_nodes(os.path.join(A, 'ScriptableObjects', 'Skill Tree Upgrades', 'DengeV1'))
STAT_TR = {'HarvestDamage': 'hasar', 'AreaRadius': 'yarıçap', 'AttackSpeed': 'saldırı aralığı', 'CritChance': 'kritik şansı', 'CritMultiplier': 'kritik çarpanı',
           'PlantSpawnRate': 'üretim aralığı', 'RareSpawnChance': 'nadirlik', 'GoldGainMultiplier': 'altın', 'IronGainMultiplier': 'demir', 'StoneGainMultiplier': 'taş',
           'XPGainMultiplier': 'XP', 'HarvestScoreMultiplier': 'skor', 'MutationLuck': 'kart şansı', 'GridUnlockSize': 'grid', 'RoundDuration': 'süre', 'CardSkip': 'kart atlama'}
CUR = 'SIG'

def num(v):
    s = ('%.2f' % v).rstrip('0').rstrip('.')
    return s.replace('.', ',')

def families(nodes):
    out = {}
    for n in nodes.values():
        fam = n['name'].rsplit(' - ', 1)[0] if ' - ' in n['name'] else n['name']
        out.setdefault(fam, []).append(n)
    return out

def effect(nodes):
    tot = {}
    for n in nodes:
        for st, tg, op, val in n['tiers'][-1]['effects']:
            key = (st, op)
            if op == 2: tot[key] = tot.get(key, 1.0) * (1 + val)
            elif op == 3: tot[key] = max(tot.get(key, 0), val)
            else: tot[key] = tot.get(key, 0.0) + val
    parts = []
    for (st, op), v in tot.items():
        name = STAT_TR.get(treelib.STAT[st], treelib.STAT[st])
        if op == 2: parts.append('%s ×%s' % (name, num(v)))
        elif op == 1: parts.append('%s +%%%s' % (name, num(v * 100)))
        elif op == 3: parts.append('%s %d×%d' % (name, v, v))
        elif treelib.STAT[st] == 'CritChance': parts.append('%s +%s puan' % (name, num(v * 100)))
        else: parts.append('%s +%s' % (name, num(v)))
    return '; '.join(parts) if parts else 'kilit açar'

def cost(nodes):
    tot = {}
    for n in nodes:
        for t in n['tiers']: tot[t['costType']] = tot.get(t['costType'], 0) + t['cost']
    return ' + '.join('%s %s' % ('{:,}'.format(v).replace(',', '.'), CUR[k]) for k, v in sorted(tot.items(), reverse=True))

def window(nodes):
    return 'R%d–%d' % (min(n['target'][0] for n in nodes), max(n['target'][1] for n in nodes))

fo, fn = families(old), families(new)
print('### Ağaç: aile aile eski → yeni\n')
print('| Aile | Eski etki | Eski fiyat · hedef | DengeV1 etki | DengeV1 fiyat · hedef |')
print('|---|---|---|---|---|')
for fam in sorted(fo, key=lambda f: min(n['target'][0] for n in fo[f])):
    o = fo[fam]
    if fam in fn:
        n = fn[fam]
        print('| %s (%d) | %s | %s · %s | %s | %s · %s |' % (fam, len(o), effect(o), cost(o), window(o), effect(n), cost(n), window(n)))
    else:
        print('| %s (%d) | %s | %s · %s | **ağaçtan çıktı** | — |' % (fam, len(o), effect(o), cost(o), window(o)))
tot_o = {}; tot_n = {}
for n in old.values():
    for t in n['tiers']: tot_o[t['costType']] = tot_o.get(t['costType'], 0) + t['cost']
for n in new.values():
    for t in n['tiers']: tot_n[t['costType']] = tot_n.get(t['costType'], 0) + t['cost']
print('\nToplam: eski %d düğüm / %d kademe · %s — DengeV1 %d düğüm / %d kademe · %s\n' % (
    len(old), sum(len(n['tiers']) for n in old.values()), ' + '.join('%s %s' % ('{:,}'.format(tot_o[k]).replace(',', '.'), CUR[k]) for k in (2, 1, 0)),
    len(new), sum(len(n['tiers']) for n in new.values()), ' + '.join('%s %s' % ('{:,}'.format(tot_n[k]).replace(',', '.'), CUR[k]) for k in (2, 1, 0))))

# can
def curve(anchors, r):
    lo = max((a for a in anchors if a[0] <= r), key=lambda a: a[0]); hi = min((a for a in anchors if a[0] >= r), key=lambda a: a[0])
    if lo[0] == hi[0]: return lo[1]
    return lo[1] * (hi[1] / float(lo[1])) ** ((r - lo[0]) / float(hi[0] - lo[0]))
shared = io.open(os.path.join(A, 'Resources', 'PlantHealthScaling.asset'), encoding='utf-8').read()
old_anchors = [(int(a), float(b)) for a, b in re.findall(r'round: (\d+)\s+commonHealth: ([\d.]+)', shared)]
old_mult = [float(x) for x in re.findall(r'^  - ([\d.]+)$', shared[shared.index('rarityMultipliers'):], flags=re.M)]
rounds = [1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50]
print('### Can eğrisi\n')
print('| Round | ' + ' | '.join(str(r) for r in rounds) + ' |')
print('|---|' + '---|' * len(rounds))
print('| Eski Common canı (hasar tabanı 1) | ' + ' | '.join('%d' % math.ceil(curve(old_anchors, r) - 1e-6) for r in rounds) + ' |')
print('| DengeV1 Common canı (hasar tabanı 10) | ' + ' | '.join('%d' % math.ceil(curve(P.HP_ANCHORS, r) - 1e-6) for r in rounds) + ' |')
print('| DengeV1 Legendary canı | ' + ' | '.join('%d' % math.ceil(curve(P.HP_ANCHORS, r) * (1 + 4 * P.HP_RARITY_K) - 1e-6) for r in rounds) + ' |')
print('| Round başına büyüme (DengeV1) | ' + ' | '.join('—' if i == 0 else '×%s' % num((curve(P.HP_ANCHORS, r) / curve(P.HP_ANCHORS, rounds[i - 1])) ** (1.0 / (r - rounds[i - 1]))) for i, r in enumerate(rounds)) + ' |')
print('\nNadirlik çarpanı (Common … Legendary): eski %s — DengeV1 %s (Can = Common × (1 + %s × basamak))\n' % (
    ' / '.join(num(x) for x in old_mult), ' / '.join(num(1 + P.HP_RARITY_K * n) for n in range(5)), num(P.HP_RARITY_K)))

# xp
xp_old = [float(x) for x in re.findall(r'^  - ([\d.]+)$', io.open(os.path.join(A, 'ScriptableObjects', 'Prograsiondata', 'Xp çarpanı.asset'), encoding='utf-8').read(), flags=re.M)]
levels = [1, 5, 10, 20, 30, 40, 50, 60, 70, 80]
print('### XP tablosu (bir sonraki level için gereken XP)\n')
print('| Level | ' + ' | '.join(str(l) for l in levels) + ' |')
print('|---|' + '---|' * len(levels))
print('| Eski | ' + ' | '.join('%d' % xp_old[min(l - 1, len(xp_old) - 1)] for l in levels) + ' |')
print('| DengeV1 | ' + ' | '.join('%d' % P.xp_requirement(l) for l in levels) + ' |')
print('\nBitki XP\'si: eski her bitki 20 — DengeV1 20 × (%s) (nadirlik can çarpanıyla aynı)\n' % ' / '.join(num(x) for x in P.RARITY_XP))

# ödüller
print('### Boss ödülleri (DengeV1)\n')
print('| Ödül | Etki (tek alış) | En çok | Tamamı | Sunulduğu boss round\'u | Koşul |')
print('|---|---|---|---|---|---|')
for r in P.REWARDS:
    mx = r.get('max', 3)
    if 'direct' in r: one, full = '×%s' % num(r['direct']), '×%s' % num(r['direct'] ** mx)
    elif 'rare_direct' in r: one, full = '×%s' % num(r['rare_direct']), '×%s' % num(r['rare_direct'] ** mx)
    elif 'behavior' in r: one, full = '×%s' % num(r['behavior']), '×%s' % num(r['behavior'] ** mx)
    else:
        st, tg, op, v = r['mods'][0]
        if op == 2: one, full = '×%s' % num(1 + v), '×%s' % num((1 + v) ** mx)
        elif st == 'CritChance': one, full = '+%s puan' % num(v * 100), '+%s puan' % num(v * 100 * mx)
        else: one, full = '+%s' % num(v), '+%s' % num(v * mx)
    when = 'R5–45' if not r.get('min_round') and not r.get('max_round') else ('R%d–45' % r['min_round'] if r.get('min_round') else 'R5–%d' % r['max_round'])
    cond = {0: '—', 1: 'şansı 0–100 arası davranış saksısı varsa', 2: 'davranışı olan saksı varsa', 3: 'tarla boşalıyorsa (doluluk < %%%d)' % round(r.get('threshold', .7) * 100)}[r.get('condition', 0)]
    print('| %s | %s %s | %d | %s | %s | %s |' % (r['name'], r['label'], one, mx, full, when, cond))
print()
print('### Hedefler\n')
print('| Segment | ' + ' | '.join(str(i + 1) for i in range(10)) + ' |')
print('|---|' + '---|' * 10)
print('| Segment kotası | ' + ' | '.join(str(t) for t in P.SEGMENT_TARGETS) + ' |')
print('| Boss hasadı hedefi (segmentin son round\'u) | ' + ' | '.join(str(t) for t in P.BOSS_TARGETS) + ' | — |')
