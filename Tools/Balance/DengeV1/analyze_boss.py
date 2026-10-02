# Boss açık / kapalı: boss round'unun hasat ve skoru, aynı run'ın komşu round'larının (R-1, R+1) ortalamasına oranı.
# Oran kuralsız boss'ta ("none") da hesaplanır: büyüme ve seed gürültüsü tabanı odur. kullanım: analyze_boss.py [set]
import csv, sys, io, os, statistics as st
from collections import defaultdict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
LOGS = os.environ.get('BALANCE_LOGS', os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs'))
name = sys.argv[1] if len(sys.argv) > 1 else 'bossab'
rows = list(csv.reader(io.open(os.path.join(LOGS, 'BalanceRuns_%s.csv' % name), encoding='utf-8')))
head = rows[0]
runs = defaultdict(dict); meta = {}
for r in rows[1:]:
    if len(r) < 12: meta.setdefault(r[0], {})['outcome'] = (r[8], int(r[7])); continue
    d = dict(zip(head, r)); runs[r[0]][int(d['round'])] = d
    meta.setdefault(r[0], {}).update(policy=d['policy'], label=d['label'], seed=d['seed'])
def f(x):
    try: return float(x)
    except Exception: return 0.0
bands = [('R5–15', (5, 10, 15)), ('R20–30', (20, 25, 30)), ('R35–45', (35, 40, 45))]
data = defaultdict(lambda: defaultdict(list))   # (policy, label) -> (band, metric) -> ratios
names = defaultdict(set)
for rid, rounds in runs.items():
    key = (meta[rid]['policy'], meta[rid]['label'])
    for band, rs in bands:
        for R in rs:
            if R not in rounds or R - 1 not in rounds or R + 1 not in rounds: continue
            names[key].add(rounds[R]['boss'])
            for metric in ('kills', 'score', 'direct', 'hitsPerAttack', 'fill'):
                base = (f(rounds[R - 1][metric]) + f(rounds[R + 1][metric])) / 2.0
                if base > 0: data[key][(band, metric)].append(f(rounds[R][metric]) / base)
            data[key][(band, 'margin')].append(f(rounds[R]['bossScore']) / max(1.0, f(rounds[R]['bossTarget'])))
def m(v): return '%.2f' % st.median(v) if v else '-'
print('boss round / komşu round ortalaması (medyan). 1,00 = fark yok. "boss=none" satırı büyüme tabanı.')
print('politika · varyant | boss adı | band | hasat | doğrudan hasat | skor | vuruş/saldırı | doluluk | boss hasadı / hedef (medyan, en düşük) | n')
for key in sorted(data):
    for band, _ in bands:
        d = data[key]
        if not d[(band, 'kills')]: continue
        mg = d[(band, 'margin')]
        print('%s · %s | %s | %s | %s | %s | %s | %s | %s | ×%s, ×%.2f | %d' % (key[0], key[1], '/'.join(sorted(n for n in names[key] if n)), band, m(d[(band, 'kills')]), m(d[(band, 'direct')]),
              m(d[(band, 'score')]), m(d[(band, 'hitsPerAttack')]), m(d[(band, 'fill')]), m(mg), min(mg) if mg else 0, len(d[(band, 'kills')])))
print()
print('sonuçlar: ' + ' · '.join('%s/%s/%s %s@R%d' % (meta[r]['policy'][:6], meta[r]['label'], meta[r]['seed'], meta[r]['outcome'][0], meta[r]['outcome'][1]) for r in sorted(meta, key=int) if 'outcome' in meta[r]))
