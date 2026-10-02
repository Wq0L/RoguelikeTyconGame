# Aynı ölçüm setinin iki çalıştırmasını run run karşılaştırır (oynanış sütunları; süre ölçümleri hariç).
# Kullanım: PYTHONIOENCODING=utf-8 python compare_runs.py <eski.csv> <yeni.csv> [etiket]
import csv, io, sys, statistics

def load(path):
    rows = list(csv.DictReader(io.open(path, encoding='utf-8-sig')))
    runs = {}
    for r in rows:
        if not (r.get('round') or '').isdigit() or r.get('score') in (None, ''):
            continue
        runs.setdefault((r['policy'], r['seed']), []).append(r)
    return runs, list(rows[0].keys())

old, cols = load(sys.argv[1])
new, _ = load(sys.argv[2])
tag = sys.argv[3] if len(sys.argv) > 3 else ''
# Süre ve ölçüm sütunları (gerçek zamana bağlı) oynanış sayılmaz.
skip = {'run', 'label', 'duration', 'best5s'}
game = [c for c in cols if c not in skip and not c.startswith('ttk')]

def total(rows):
    return sum(int(float(x['score'])) for x in rows)

done = same = same_before = 0
ratios = {}
with_bt = without_bt = same_without = 0
lines = []
for key, a in old.items():
    b = new.get(key)
    if not b or len(b) < len(a):
        continue
    done += 1
    first = None
    for i in range(len(a)):
        if any(a[i].get(c) != b[i].get(c) for c in game):
            first = int(a[i]['round'])
            break
    pick = next((int(x['round']) for x in a if x.get('breakthrough')), None)
    ta, tb = total(a), total(b)
    ratio = tb / ta if ta else float('nan')
    ratios.setdefault(key[0], []).append((ta, tb, ratio))
    if first is None:
        same += 1
    if pick is None:
        without_bt += 1
        same_without += first is None
    else:
        with_bt += 1
        same_before += first is None or first >= pick
    lines.append(f"{key[0]:<18} seed {key[1]} | kırılma ödülü ilk satır {pick if pick else '-':>3} | ilk fark R{first if first else '-':>3} | "
                 f"skor {ta:>9} → {tb:>9} | oran {ratio:.3f}")

print(f"## {tag}: {done} run karşılaştırıldı")
print(f"- bire bir aynı (bütün oynanış sütunları, bütün round'lar): {same} / {done}")
print(f"- kırılma ödülü almayan run'lar: {same_without} / {without_bt} bire bir aynı")
print(f"- kırılma ödülü alan run'lar: {same_before} / {with_bt} ödülün alındığı round'a kadar aynı")
for policy, v in ratios.items():
    r = [x[2] for x in v]
    print(f"- {policy}: toplam skor medyanı {statistics.median(x[0] for x in v):.0f} → {statistics.median(x[1] for x in v):.0f}; "
          f"run başına oran en az {min(r):.3f}, medyan {statistics.median(r):.3f}, en çok {max(r):.3f}")
print()
for line in lines:
    print(line)
