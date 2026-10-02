# Grupları (politika · başlangıç · etiket) aynı round'larda yan yana karşılaştırır: birikimli skor, o segmentin hasadı, level, birikimli altın.
# kullanım: compare_groups.py <set> [round…]
import csv, sys, io, os, statistics as st
from collections import OrderedDict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
LOGS = os.environ.get('BALANCE_LOGS', os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs'))
name = sys.argv[1]
checks = [int(x) for x in sys.argv[2:]] or [5, 10, 15, 20, 25, 30, 35, 40, 45, 50]
rows = list(csv.reader(io.open(os.path.join(LOGS, 'BalanceRuns_%s.csv' % name), encoding='utf-8')))
head = rows[0]
runs = OrderedDict()
for r in rows[1:]:
    run = runs.setdefault(r[0], dict(rounds={}, outcome='?', end=0))
    run['meta'] = (r[2], r[3], r[4], r[6])
    if len(r) < 12: run['outcome'] = r[8]; run['end'] = int(r[7]); continue
    run['rounds'][int(r[7])] = dict(zip(head, r))
groups = OrderedDict()
for run in runs.values(): groups.setdefault(run['meta'], []).append(run)
def f(x):
    try: return float(x)
    except Exception: return 0.0
def gname(m): return '%s · %s+%s%s' % (m[0], m[1], m[2], (' · ' + m[3]) if m[3] else '')
def med(v, fmt='%.0f'): return fmt % st.median(v) if v else '-'
for metric, title in (('cumscore', 'BİRİKİMLİ SKOR (medyan; run o round\'a ulaştıysa)'), ('segkills', 'SON 5 ROUND HASAT TOPLAMI'), ('level', 'LEVEL'), ('cumgold', 'BİRİKİMLİ ALTIN GELİRİ'),
                      ('cumxp', 'BİRİKİMLİ XP'), ('tree', 'ALINAN AĞAÇ KADEMESİ')):
    print('=== ' + title + ' ===')
    print('grup | ' + ' | '.join('R%d' % c for c in checks))
    for meta, group in groups.items():
        cells = []
        for c in checks:
            vals = []
            for run in group:
                rs = run['rounds']
                if c not in rs: continue
                if metric == 'cumscore': vals.append(sum(f(rs[x]['score']) for x in rs if x <= c))
                elif metric == 'segkills': vals.append(sum(f(rs[x]['kills']) for x in rs if c - 5 < x <= c))
                elif metric == 'level': vals.append(f(rs[c]['level']))
                elif metric == 'cumgold': vals.append(sum(f(rs[x]['gold']) for x in rs if x <= c))
                elif metric == 'cumxp': vals.append(sum(f(rs[x]['xp']) for x in rs if x <= c))
                elif metric == 'tree': vals.append(f(rs[c]['treeTiers']))
            cells.append(med(vals) + ('' if len(vals) == len(group) else ' (%d/%d)' % (len(vals), len(group))))
        print('%s | %s' % (gname(meta), ' | '.join(cells)))
print('=== SONUÇ ===')
for meta, group in groups.items():
    outs = {}
    for run in group: outs.setdefault(run['outcome'], []).append(run['end'])
    print('%s | %s' % (gname(meta), ' · '.join('%s %d (R%s)' % (k, len(v), ','.join(map(str, sorted(v)))) for k, v in outs.items())))
