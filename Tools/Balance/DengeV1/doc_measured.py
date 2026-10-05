# Ölçüm setinden belge tabloları (Markdown).  kullanım: doc_measured.py <set> [outcome curve level tree hits income access margin split share time rewards]
import csv, sys, io, os, statistics as st
from collections import OrderedDict, defaultdict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
_DEF = os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs')
LOGS = os.environ.get('BALANCE_LOGS', _DEF)
name = sys.argv[1]; sections = sys.argv[2:] or ['outcome', 'curve', 'hits', 'income', 'access', 'margin', 'split', 'share', 'time', 'rewards']

def read(suffix=''):
    path = os.path.join(LOGS, 'BalanceRuns_%s%s.csv' % (name, suffix))
    if not os.path.exists(path): return [], []
    rows = list(csv.reader(io.open(path, encoding='utf-8')))
    return rows[0], rows[1:]
head, rows = read()
import calendar_guard; calendar_guard.require_uniform(rows, 'doc_measured.py')   # açık boss takvimli profil: açık 'desteklenmiyor' sonucu
runs = OrderedDict()
for r in rows:
    run = runs.setdefault(r[0], dict(id=r[0], rounds={}, meta=(r[2], r[3], r[4], r[6]), seed=r[5], outcome='?', end=0))
    if len(r) < 12: run['outcome'] = r[8]; run['end'] = int(r[7]); continue
    run['rounds'][int(r[7])] = dict(zip(head, r))
def f(x):
    try: return float(x)
    except Exception: return None
FARMER = {'bahcivan': 'Bahçıvan', 'tuccar': 'Tüccar', 'secici_yetistirici': 'Seçici Yetiştirici'}
SCYTHE = {'standart': 'Standart', 'dar_kesim': 'Dar Kesim'}
def gname(m): return '%s · %s + %s%s' % (m[0], FARMER.get(m[1], m[1]), SCYTHE.get(m[2], m[2]), (' · ' + m[3]) if m[3] else '')
groups = OrderedDict()
for run in runs.values(): groups.setdefault(run['meta'], []).append(run)
def med(v, fmt='%.0f'):
    v = [x for x in v if x is not None]
    return (fmt % st.median(v)).replace('.', ',') if v else '–'
def rng(v, fmt='%.0f'):
    v = [x for x in v if x is not None]
    return ((fmt + ' [' + fmt + '–' + fmt + ']') % (st.median(v), min(v), max(v))).replace('.', ',') if v else '–'
def col(group, R, key): return [f(run['rounds'][R][key]) for run in group if R in run['rounds']]
ROUNDS = [5, 10, 15, 20, 25, 30, 35, 40, 45, 50]
structured = [run for run in runs.values() if run['meta'][0] != 'Yeni']
OUT = {'KAZANDI': 'kazandı', 'KOTA': 'kota', 'BOSS': 'boss hedefi'}
nh, nrows = read('_nodes')
tiers = defaultdict(int)
for r in nrows: tiers[r[0]] += 1

if 'outcome' in sections:
    print('| Politika · başlangıç | Kazanan | Elenenler (neden @ round) | R50\'de ağaç kademesi (221 üzerinden) | R50 level | Toplam skor |')
    print('|---|---|---|---|---|---|')
    tw = tn = 0
    for meta, group in groups.items():
        wins = sum(1 for run in group if run['outcome'] == 'KAZANDI'); tw += wins; tn += len(group)
        lost = ', '.join('%s @R%d' % (OUT.get(run['outcome'], run['outcome']), run['end']) for run in group if run['outcome'] != 'KAZANDI') or '—'
        last = [run['rounds'][max(run['rounds'])] for run in group if run['rounds']]
        score = [sum(f(d['score']) for d in run['rounds'].values()) for run in group]
        print('| %s | %d / %d | %s | %s | %s | %s |' % (gname(meta), wins, len(group), lost, rng([f(d['treeTiers']) for d in last]), rng([f(d['level']) for d in last]), rng(score)))
    print('\nToplam: %d run, %d kazanan.\n' % (tn, tw))

if 'curve' in sections:
    for key, title, fmt in (('kills', 'Round başına hasat (medyan; o round\'u oynayan run\'lar)', '%.0f'),):
        print('**%s**\n' % title)
        print('| Politika · başlangıç | ' + ' | '.join('R%d' % R for R in ROUNDS) + ' |')
        print('|---|' + '---|' * len(ROUNDS))
        for meta, group in groups.items():
            print('| %s | %s |' % (gname(meta), ' | '.join(med(col(group, R, key), fmt) for R in ROUNDS)))
        print()

for sec, key, title in (('level', 'level', 'Level (medyan)'), ('tree', 'treeTiers', 'Alınan ağaç kademesi (medyan; toplam 221)')):
    if sec in sections:
        print('**%s**' % title)
        print()
        print('| Politika · başlangıç | ' + ' | '.join('R%d' % R for R in ROUNDS) + ' |')
        print('|---|' + '---|' * len(ROUNDS))
        for meta, group in groups.items():
            print('| %s | %s |' % (gname(meta), ' | '.join(med(col(group, R, key)) for R in ROUNDS)))
        print()

if 'hits' in sections:
    RAR = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
    for title, pick in (('Bütün yapılandırılmış politikalar', lambda m: m[0] != 'Yeni'), ('Hasar-değerli hedef', lambda m: m[0].startswith('Hasar')), ('Hız-alan', lambda m: m[0].startswith('Hız'))):
        sel = [run for run in runs.values() if pick(run['meta'])]
        if not sel: continue
        print('**%s** (medyan; Common / Uncommon / Rare / Epic / Legendary)\n' % title)
        print('| Round | Öldürmek için vuruş | Tek vuruşta ölen % | İlk vuruştan hasada süre (sn) |')
        print('|---|---|---|---|')
        for R in [1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50]:
            if not any(R in run['rounds'] for run in sel): continue
            print('| R%d | %s | %s | %s |' % (R, ' / '.join(med(col(sel, R, 'htk' + r), '%.1f') for r in RAR), ' / '.join(med(col(sel, R, 'os' + r)) for r in RAR),
                                              ' / '.join(med(col(sel, R, 'ttk' + r), '%.1f') for r in RAR)))
        print()

if 'income' in sections:
    print('| Politika · başlangıç | Σ altın R10 / R20 / R30 / R40 / R50 | Σ demir R20 / R30 / R40 / R50 | Σ taş R30 / R40 / R50 | R50 kasada kalan (A / D / T) |')
    print('|---|---|---|---|---|')
    for meta, group in groups.items():
        def cum(k, R): return med([sum(f(run['rounds'][x][k]) for x in run['rounds'] if x <= R) for run in group if R in run['rounds']])
        print('| %s | %s | %s | %s | %s / %s / %s |' % (gname(meta), ' / '.join(cum('gold', R) for R in (10, 20, 30, 40, 50)), ' / '.join(cum('iron', R) for R in (20, 30, 40, 50)),
              ' / '.join(cum('stone', R) for R in (30, 40, 50)), med(col(group, 50, 'bankGold')), med(col(group, 50, 'bankIron')), med(col(group, 50, 'bankStone'))))
    print()

if 'access' in sections:
    per = defaultdict(lambda: defaultdict(list))   # family -> run -> rounds
    for r in nrows:
        if r[1] == 'Yeni': continue
        node = r[7]; fam = node.rsplit(' - ', 1)[0] if ' - ' in node else node
        per[fam][r[0]].append(int(r[6]))
    n = len(structured)
    print('| Aile / düğüm | İlk kademe (medyan [en erken–en geç]) | Son alınan kademe (medyan) | Alınan kademe (medyan) | Aileye giren run |')
    print('|---|---|---|---|---|')
    for fam in sorted(per, key=lambda k: st.median([min(v) for v in per[k].values()])):
        by = per[fam]
        print('| %s | %s | R%s | %s | %d / %d |' % (fam, 'R' + rng([min(v) for v in by.values()]), med([max(v) for v in by.values()]), med([len(v) for v in by.values()]), len(by), n))
    print()

if 'margin' in sections:
    print('| Round | Oynayan run | Segment skoru ÷ kota (medyan [en düşük]) | Boss hasadı ÷ hedef (medyan [en düşük]) | Kotada kalan | Boss hedefinde kalan |')
    print('|---|---|---|---|---|---|')
    for R in ROUNDS:
        seg = []; boss = []; fs = fb = 0; n = 0
        for run in structured:
            if R not in run['rounds']: continue
            d = run['rounds'][R]; n += 1
            if f(d['segTarget']): seg.append(f(d['segScore']) / f(d['segTarget'])); fs += f(d['segScore']) < f(d['segTarget'])
            if f(d['bossTarget']): boss.append(f(d['bossScore']) / f(d['bossTarget'])); fb += f(d['bossScore']) < f(d['bossTarget'])
        if n: print(('| R%d | %d | ×%.1f [×%.2f] | %s | %d | %d |' % (R, n, st.median(seg), min(seg), ('×%.1f [×%.2f]' % (st.median(boss), min(boss))) if boss else '—', fs, fb)).replace('.', ','))
    print()
    naive = [run for run in runs.values() if run['meta'][0] == 'Yeni']
    if naive:
        print('"Yeni" bot (rastgele nişan, en ucuzu alır):\n')
        print('| Round | Oynayan run | Segment skoru ÷ kota | Boss hasadı ÷ hedef | Kotada kalan | Boss hedefinde kalan |')
        print('|---|---|---|---|---|---|')
        for R in ROUNDS:
            seg = []; boss = []; fs = fb = 0; n = 0
            for run in naive:
                if R not in run['rounds']: continue
                d = run['rounds'][R]; n += 1
                if f(d['segTarget']): seg.append(f(d['segScore']) / f(d['segTarget'])); fs += f(d['segScore']) < f(d['segTarget'])
                if f(d['bossTarget']): boss.append(f(d['bossScore']) / f(d['bossTarget'])); fb += f(d['bossScore']) < f(d['bossTarget'])
            if n: print(('| R%d | %d | ×%.1f [×%.2f] | %s | %d | %d |' % (R, n, st.median(seg), min(seg), ('×%.1f [×%.2f]' % (st.median(boss), min(boss))) if boss else '—', fs, fb)).replace('.', ','))
        print()

if 'split' in sections:
    rh, rrows = read('_rewards')
    taken = defaultdict(lambda: defaultdict(list))
    for r in rrows: taken[r[0]][r[8]].append(int(r[6]))
    SPEED = {'hizli_bilek': 0.85, 'firtina_bilegi': 0.70}; AREA = {'genis_savurus': 1.32}
    print('| Politika · başlangıç | Round | Doğrudan hasar: ağaç + kart × ödül (+ tırpan) = toplam | Saldırı aralığı: ağaç + kart × ödül = toplam | Yarıçap: ağaç + kart × ödül = toplam → en iyi nişanda hücre |')
    print('|---|---|---|---|---|')
    for meta, group in groups.items():
        if meta[0] == 'Yeni': continue
        for R in (25, 50):
            dmg = []; mult = []; itv = []; rs = []; rad = []; ra = []; cells = []
            for run in group:
                if R not in run['rounds']: continue
                d = run['rounds'][R]; sp = 1.0; ar = 1.0
                for k, v in SPEED.items(): sp *= v ** sum(1 for x in taken[run['id']].get(k, []) if x < R)
                for k, v in AREA.items(): ar *= v ** sum(1 for x in taken[run['id']].get(k, []) if x < R)
                dmg.append(f(d['damage'])); mult.append(f(d['directMult'])); itv.append(f(d['interval'])); rs.append(sp); rad.append(f(d['radius'])); ra.append(ar); cells.append(f(d['reachCells']))
            if not dmg: continue
            m = st.median
            print(('| %s | R%d | %.0f × %.2f = %.0f | %.2f sn × %.2f = %.2f sn | %.2f × %.2f = %.2f → %.0f |' % (gname(meta), R, m(dmg), m(mult), m([a * b for a, b in zip(dmg, mult)]),
                  m([a / b for a, b in zip(itv, rs)]), m(rs), m(itv), m([a / b for a, b in zip(rad, ra)]), m(ra), m(rad), m(cells))).replace('.', ','))
    print()

if 'share' in sections:
    print('| Politika · başlangıç | Round | Hasat | Doğrudan % | Patlama / kasırga / bumerang / elektrik % | Ortalama doluluk | En düşük doluluk | Hasat ÷ üretim kapasitesi | Saniyede hasat | Vurulan hücre-saniye başına hasat |')
    print('|---|---|---|---|---|---|---|---|---|---|')
    for meta, group in groups.items():
        for R in (10, 25, 40, 50):
            alive = [run['rounds'][R] for run in group if R in run['rounds']]
            if not alive: continue
            m = lambda k: st.median([f(d[k]) or 0.0 for d in alive])
            kills = max(1.0, m('kills'))
            per = [f(d['kills']) / max(1.0, f(d['duration'])) for d in alive]
            per_cell = [f(d['kills']) / max(1.0, f(d['duration'])) / max(1.0, f(d['reachCells'])) for d in alive]
            cap = [f(d['kills']) / f(d['capacity']) for d in alive if f(d['capacity'])]
            print(('| %s | R%d | %.0f | %.0f | %.0f / %.0f / %.0f / %.0f | %.2f | %.2f | %.2f | %.2f | %.3f |' % (gname(meta), R, m('kills'), 100 * m('direct') / kills,
                  100 * m('explosion') / kills, 100 * m('tornado') / kills, 100 * m('boomerang') / kills, 100 * m('electric') / kills, m('fill'), m('minFill'),
                  st.median(cap) if cap else 0, st.median(per), st.median(per_cell))).replace('.', ','))
    print()

if 'time' in sections:
    print('| Politika · başlangıç | Oynanan round | Hasat süresi (ölçüldü, dk) | Kart ekranı sayısı | Boss ödülü ekranı | Alınan ağaç kademesi | Tahmini oturum (dk; menü süreleri varsayım) | Kart ekranı payı (varsayımla) |')
    print('|---|---|---|---|---|---|---|---|')
    for meta, group in groups.items():
        tot = []; harv = []; cards_ = []; bosses_ = []; tr = []; share = []
        for run in group:
            rounds = len(run['rounds'])
            harvest = sum(f(d['duration']) for d in run['rounds'].values())
            cards = sum(f(d['cards']) for d in run['rounds'].values())
            bosses = sum(1 for d in run['rounds'].values() if f(d['bossTarget']))
            total = harvest + rounds * 25 + cards * 10 + bosses * 15 + tiers[run['id']] * 4
            tot.append(total / 60.0); harv.append(harvest / 60.0); cards_.append(cards); bosses_.append(bosses); tr.append(tiers[run['id']]); share.append(100.0 * cards * 10 / total)
        print('| %s | %s | %s | %s | %s | %s | %s | %%%s |' % (gname(meta), rng([len(r['rounds']) for r in group]), med(harv, '%.1f'), rng(cards_), med(bosses_), med(tr), rng(tot), med(share)))
    print()

if 'rewards' in sections:
    rh, rrows = read('_rewards')
    print('| Politika · başlangıç | Alınan ödüller (alınan / sunulan) |')
    print('|---|---|')
    for meta, group in groups.items():
        picks = defaultdict(int); offers = defaultdict(int)
        for r in rrows:
            if (r[1], r[2], r[3], r[5]) != meta: continue
            picks[r[8]] += 1
            for o in r[7].split(): offers[o] += 1
        print('| %s | %s |' % (gname(meta), ', '.join('%s %d/%d' % (k, v, offers.get(k, 0)) for k, v in sorted(picks.items(), key=lambda x: -x[1]))))
    print()
