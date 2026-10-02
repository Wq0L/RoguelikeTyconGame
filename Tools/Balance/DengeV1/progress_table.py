# Grup başına seçilen sütunun round round medyanı.  kullanım: progress_table.py <set> <sütun> [sütun…]
import csv, sys, io, os, statistics as st
from collections import OrderedDict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
LOGS = os.environ.get('BALANCE_LOGS', os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs'))
name = sys.argv[1]; cols = sys.argv[2:]
CHECK = [5, 10, 15, 20, 25, 30, 35, 40, 45, 50]
rows = list(csv.reader(io.open(os.path.join(LOGS, 'BalanceRuns_%s.csv' % name), encoding='utf-8')))
head = rows[0]; runs = OrderedDict()
for r in rows[1:]:
    run = runs.setdefault(r[0], dict(rounds={}, meta=(r[2], r[3], r[4], r[6])))
    if len(r) < 12: continue
    run['rounds'][int(r[7])] = dict(zip(head, r))
groups = OrderedDict()
for run in runs.values(): groups.setdefault(run['meta'], []).append(run)
for c in cols:
    print('=== %s (medyan) ===' % c)
    print('%-58s %s' % ('', ' '.join('%7s' % ('R%d' % k) for k in CHECK)))
    for meta, group in groups.items():
        cells = []
        for k in CHECK:
            v = []
            for run in group:
                if k in run['rounds']:
                    try: v.append(float(run['rounds'][k][c]))
                    except ValueError: pass
            cells.append('%7s' % (('%.0f' % st.median(v)) if v and st.median(v) >= 20 else ('%.2f' % st.median(v)) if v else '-'))
        print('%-58s %s' % (('%s · %s+%s%s' % (meta[0], meta[1], meta[2], (' · ' + meta[3]) if meta[3] else ''))[:58], ' '.join(cells)))
