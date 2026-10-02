# Hedef payı ve güç ayrışması.  kullanım: margins.py <set> [margin split share]
#  margin: segment skoru / kota ve boss hasadı / boss hedefi (medyan ve en düşük; "Yeni" ayrı)
#  split : R25 ve R50'de doğrudan hasar, saldırı aralığı ve yarıçapın ağaç(+kart) ve ödül payı
#  share : doğrudan / davranış hasat payı, tarla doluluğu, alan-saniye başına hasat
import csv, sys, io, os, statistics as st
from collections import OrderedDict, defaultdict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
_DEF = os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs')
LOGS = os.environ.get('BALANCE_LOGS', _DEF)
name = sys.argv[1]; sections = sys.argv[2:] or ['margin', 'split', 'share']
rows = list(csv.reader(io.open(os.path.join(LOGS, 'BalanceRuns_%s.csv' % name), encoding='utf-8')))
head = rows[0]; runs = OrderedDict()
for r in rows[1:]:
    run = runs.setdefault(r[0], dict(rounds={}, meta=(r[2], r[3], r[4], r[6]), outcome='?', end=0))
    if len(r) < 12: run['outcome'] = r[8]; run['end'] = int(r[7]); continue
    run['rounds'][int(r[7])] = dict(zip(head, r))
def f(x):
    try: return float(x)
    except Exception: return 0.0
def gname(m): return '%s · %s+%s%s' % (m[0], m[1], m[2], (' · ' + m[3]) if m[3] else '')
groups = OrderedDict()
for run in runs.values(): groups.setdefault(run['meta'], []).append(run)
BOSS = [5, 10, 15, 20, 25, 30, 35, 40, 45, 50]

if 'margin' in sections:
    print('=== HEDEF PAYI: segment skoru / kota · boss hasadı / boss hedefi (medyan [en düşük]; yalnız o round\'u oynayan run\'lar) ===')
    for title, pick in (('Yapılandırılmış politikalar (Yeni hariç)', lambda m: m[0] != 'Yeni'), ('Yeni (rastgele nişan, en ucuzu alır)', lambda m: m[0] == 'Yeni')):
        print('--- ' + title)
        print('round | n | segment skoru / kota | boss hasadı / hedef | kotayı tutmayan | boss hedefini tutmayan')
        for R in BOSS:
            seg = []; boss = []; fs = fb = 0; n = 0
            for run in runs.values():
                if not pick(run['meta']) or R not in run['rounds']: continue
                d = run['rounds'][R]; n += 1
                if f(d['segTarget']) > 0:
                    seg.append(f(d['segScore']) / f(d['segTarget'])); fs += f(d['segScore']) < f(d['segTarget'])
                if f(d['bossTarget']) > 0:
                    boss.append(f(d['bossScore']) / f(d['bossTarget'])); fb += f(d['bossScore']) < f(d['bossTarget'])
            if not n: continue
            print('R%-2d | %d | ×%.1f [×%.2f] | %s | %d | %d' % (R, n, st.median(seg), min(seg), ('×%.1f [×%.2f]' % (st.median(boss), min(boss))) if boss else '—', fs, fb))
    print('--- politika başına en düşük pay (bütün segmentler)')
    for meta, group in groups.items():
        seg = [(f(run['rounds'][R]['segScore']) / f(run['rounds'][R]['segTarget']), R) for run in group for R in BOSS if R in run['rounds'] and f(run['rounds'][R]['segTarget']) > 0]
        boss = [(f(run['rounds'][R]['bossScore']) / f(run['rounds'][R]['bossTarget']), R) for run in group for R in BOSS if R in run['rounds'] and f(run['rounds'][R]['bossTarget']) > 0]
        if not seg: continue
        print('%-52s kota ×%.2f (R%d) | boss ×%.2f (R%d)' % (gname(meta), min(seg)[0], min(seg)[1], min(boss)[0] if boss else 0, min(boss)[1] if boss else 0))

if 'split' in sections:
    # ödül sayıları: rewards.csv (run, …, round, offer, pick)
    taken = defaultdict(lambda: defaultdict(list))   # run -> reward -> [round…]
    path = os.path.join(LOGS, 'BalanceRuns_%s_rewards.csv' % name)
    if os.path.exists(path):
        for r in list(csv.reader(io.open(path, encoding='utf-8')))[1:]:
            taken[r[0]][r[8]].append(int(r[6]))
    SPEED = {'hizli_bilek': 0.85, 'firtina_bilegi': 0.70}; AREA = {'genis_savurus': 1.32}
    print('\n=== GÜÇ AYRIŞMASI (medyan): ağaç + kart payı × ödül payı = oyundaki değer ===')
    print('grup | round | doğrudan hasar: ağaç+kart × ödül = toplam (taban 10) | saldırı aralığı: ağaç+kart × ödül = toplam (taban 3 sn) | yarıçap: ağaç+kart × ödül = toplam → hücre')
    for meta, group in groups.items():
        for R in (10, 25, 40, 50):
            dmg = []; mult = []; itv = []; rs = []; rad = []; ra = []; cells = []
            for rid, run in runs.items():
                if run['meta'] != meta or R not in run['rounds']: continue
                d = run['rounds'][R]
                sp = 1.0; ar = 1.0
                for k, v in SPEED.items(): sp *= v ** sum(1 for x in taken[rid].get(k, []) if x < R)
                for k, v in AREA.items(): ar *= v ** sum(1 for x in taken[rid].get(k, []) if x < R)
                dmg.append(f(d['damage'])); mult.append(f(d['directMult'])); itv.append(f(d['interval'])); rs.append(sp); rad.append(f(d['radius'])); ra.append(ar); cells.append(f(d['reachCells']))
            if not dmg: continue
            m = st.median
            print('%s | R%d | %.0f × %.2f = %.0f | %.2f × %.2f = %.2f sn | %.2f × %.2f = %.2f → %.0f' % (gname(meta), R, m(dmg), m(mult), m([a * b for a, b in zip(dmg, mult)]),
                  m([a / b for a, b in zip(itv, rs)]), m(rs), m(itv), m([a / b for a, b in zip(rad, ra)]), m(ra), m(rad), m(cells)))

if 'share' in sections:
    print('\n=== HASAT PAYI VE TARLA (medyan) ===')
    print('grup | round | hasat | doğrudan % | patlama / kasırga / bumerang / elektrik % | doluluk | en düşük doluluk | boş kalan pay | hasat ÷ üretim kapasitesi | saniyede hasat | hücre-saniye başına hasat')
    for meta, group in groups.items():
        for R in (10, 25, 40, 50):
            alive = [run['rounds'][R] for run in group if R in run['rounds']]
            if not alive: continue
            m = lambda k: st.median([f(d[k]) for d in alive])
            kills = max(1.0, m('kills'))
            per = [f(d['kills']) / max(1.0, f(d['duration'])) for d in alive]
            per_cell = [f(d['kills']) / max(1.0, f(d['duration'])) / max(1.0, f(d['reachCells'])) for d in alive]
            cap = [f(d['kills']) / f(d['capacity']) for d in alive if f(d['capacity']) > 0]
            print('%s | R%d | %.0f | %.0f | %.0f / %.0f / %.0f / %.0f | %.2f | %.2f | %.2f | %.2f | %.2f | %.3f' % (gname(meta), R, m('kills'), 100 * m('direct') / kills,
                  100 * m('explosion') / kills, 100 * m('tornado') / kills, 100 * m('boomerang') / kills, 100 * m('electric') / kills, m('fill'), m('minFill'), m('emptyShare'),
                  st.median(cap) if cap else 0, st.median(per), st.median(per_cell)))
