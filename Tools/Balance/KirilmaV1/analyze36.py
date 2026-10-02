# Bölüm 3.6 ölçüm çözümleyicisi. BalanceRunMeasurement'ın k36* setlerinin CSV'lerini okur, Markdown tablo yazar.
#   python analyze36.py <klasör>            (klasörde BalanceRuns_k36base.csv, _k36kirilma.csv, _k36twin.csv, _k36choice.csv)
# Sayı üretir; "dengeli" demez. LAB satırları (kontrollü laboratuvar) normal run tablolarına girmez.
import csv, io, os, sys, statistics as st
from collections import defaultdict

ROUND_SECONDS = 45.0
# Laboratuvar tekrarları: sayılar toplanır, oranlar ortalanır.
SUM = ('kills', 'direct', 'explosion', 'electric', 'score', 'echoKills', 'rhythmAttacks', 'attacks')
AVG = ('fill', 'endFill')
PATHS = ('Patlama yolu', 'Elektrik yolu', 'Hız-alan + Ritim')


def num(v, d=0.0):
    try: return float(v)
    except (TypeError, ValueError): return d


class Run:
    def __init__(self, key):
        self.key = key; self.rows = []; self.outcome = ''; self.end = 0; self.labs = []
        self.set = self.profile = self.policy = self.label = ''; self.seed = 0

    def r(self, n): return next((x for x in self.rows if int(x['round']) == n), None)
    def window(self, a, b): return [x for x in self.rows if a <= int(x['round']) <= b]
    @property
    def last(self): return self.rows[-1] if self.rows else None
    @property
    def won(self): return self.outcome == 'KAZANDI'
    @property
    def total(self): return sum(num(x['score']) for x in self.rows)


def load(folder):
    runs = {}
    for name in sorted(os.listdir(folder)):
        if not (name.startswith('BalanceRuns_') and name.endswith('.csv')) or '_nodes' in name or '_rewards' in name: continue
        setname = name[len('BalanceRuns_'):-4]
        rows = list(csv.reader(io.open(os.path.join(folder, name), encoding='utf-8')))
        header = rows[0]
        for row in rows[1:]:
            if not row: continue
            key = (setname, int(row[0]))
            run = runs.get(key)
            if run is None:
                run = runs[key] = Run(key)
                run.set = setname
            if len(row) <= 10:                      # sonuç satırı
                run.outcome = row[8]; run.end = int(row[7]); continue
            d = dict(zip(header, row))
            if d['label'].startswith('LAB'):
                run.labs.append(d); continue
            if not run.rows:
                run.profile, run.policy, run.label, run.seed = d['profile'], d['policy'], d['label'], int(d['seed'])
            run.rows.append(d)
    return [r for r in runs.values() if r.rows]


def med(v, f='%.0f'): return (f % st.median(v)) if v else '-'
def mean(v): return sum(v) / len(v) if v else 0.0
def fm(v, f='%.2f'): return (f % mean(v)) if v else '-'
def table(head, rows):
    out = ['| ' + ' | '.join(head) + ' |', '|' + '|'.join('---' for _ in head) + '|']
    out += ['| ' + ' | '.join(str(c) for c in r) + ' |' for r in rows]
    return '\n'.join(out) + '\n'


def group(runs, f):
    g = defaultdict(list)
    for r in runs: g[f(r)].append(r)
    return g


def share(rows, a, total='kills'):
    t = sum(num(x[total]) for x in rows)
    return sum(num(x[a]) for x in rows) / t if t else 0.0


def quota_share(run):
    x = run.last
    if run.outcome == 'BOSS': return 'boss %.0f%%' % (100 * num(x['bossScore']) / max(1, num(x['bossTarget'])))
    if run.outcome == 'KOTA': return 'kota %.0f%%' % (100 * num(x['segScore']) / max(1, num(x['segTarget'])))
    return ''


def taken_at(run):
    b = run.last['breakthrough']
    if not b: return None, None
    rid, at = b.split(' ')[0].split('@R')
    return rid, int(at)


# Kırılma V1 run'ı ÷ eşi (aynı politika, aynı seed).
def paired(P, kir, twin, title):
    P('## Eşli karşılaştırma: Kırılma V1 ÷ ' + title + '\n')
    rows = []; detail = []
    for policy in PATHS:
        for a, b in ((20, 30), (31, 40), (41, 50)):
            for early in (False, True):
                sr = []; kr = []
                for (p, seed), r in sorted(kir.items()):
                    if p != policy or (p, seed) not in twin: continue
                    rid, at = taken_at(r)
                    if early and (at is None or at > 20): continue
                    t = twin[(p, seed)]
                    wa, wb = r.window(a, b), t.window(a, b)
                    if not wa or not wb: continue
                    s1, s2 = mean([num(x['scorePerSec']) for x in wa]), mean([num(x['scorePerSec']) for x in wb])
                    k1, k2 = mean([num(x['killsPerSec']) for x in wa]), mean([num(x['killsPerSec']) for x in wb])
                    if s2 > 0: sr.append(s1 / s2)
                    if k2 > 0: kr.append(k1 / k2)
                    if (a, b) == (20, 30) and not early:
                        detail.append([policy, seed, r.last['breakthrough'] or '-', '%s R%d' % (r.outcome, r.end), '%s R%d' % (t.outcome, t.end), '%.2f' % s1, '%.2f' % s2, '×%.2f' % (s1 / s2 if s2 else 0), '%.2f' % k1, '%.2f' % k2, '×%.2f' % (k1 / k2 if k2 else 0)])
                if sr:
                    rows.append([policy, 'R%d–R%d' % (a, b), 'ödülü R20\'ye kadar alanlar' if early else 'hepsi', len(sr), '×' + med(sr, '%.2f'), '×%.2f – ×%.2f' % (min(sr), max(sr)), '%d / %d' % (sum(1 for v in sr if v > 1.0), len(sr)),
                                 '×' + med(kr, '%.2f'), '×%.2f – ×%.2f' % (min(kr), max(kr)), '%d / %d' % (sum(1 for v in kr if v > 1.0), len(kr))])
    P(table(['politika', 'pencere', 'eşler', 'eş sayısı', 'skor hızı oranı (medyan)', 'aralık', '×1,00 üstü', 'hasat hızı oranı (medyan)', 'aralık', '×1,00 üstü'], rows))
    P('### R20–R30, seed bazında\n')
    P(table(['politika', 'seed', 'kırılma ödülü', 'kırılma sonucu', 'eş sonucu', 'skor/sn', 'eş skor/sn', 'oran', 'hasat/sn', 'eş hasat/sn', 'oran'], detail))
    # ödülün alındığı round'dan sonra
    P('### Ödülün alındığı round\'dan sonraki 5 ve 10 round (run ÷ eş; alınmadan önceki 4 round karşılaştırma için)\n')
    rows = []; s5 = {}; s10 = {}
    for (policy, seed), r in sorted(kir.items()):
        rid, at = taken_at(r)
        t = twin.get((policy, seed))
        if at is None or t is None: continue
        def ratio(field, a, b):
            x, y = [num(v[field]) for v in r.window(a, b)], [num(v[field]) for v in t.window(a, b)]
            return (mean(x) / mean(y)) if x and y and mean(y) > 0 else None
        f = lambda v: '×%.2f' % v if v else '-'
        r5, r10 = ratio('killsPerSec', at + 1, at + 5), ratio('killsPerSec', at + 1, at + 10)
        if r5: s5.setdefault(policy, []).append(r5)
        if r10: s10.setdefault(policy, []).append(r10)
        rows.append([policy, seed, rid, 'R%d' % at, f(ratio('killsPerSec', at - 3, at)), f(r5), f(r10), f(ratio('scorePerSec', at - 3, at)), f(ratio('scorePerSec', at + 1, at + 5)), f(ratio('scorePerSec', at + 1, at + 10))])
    P(table(['politika', 'seed', 'ödül', 'alındığı round', 'hasat hızı: önceki 4', 'sonraki 5', 'sonraki 10', 'skor hızı: önceki 4', 'sonraki 5', 'sonraki 10'], rows))
    P(table(['politika', 'eş', 'hasat hızı oranı, sonraki 5 round (medyan)', 'sonraki 10 round (medyan)', '×1,00 üstü (10 round)'],
            [[k, len(s10.get(k, [])), '×' + med(s5.get(k, []), '%.2f'), '×' + med(s10.get(k, []), '%.2f'), '%d / %d' % (sum(1 for v in s10.get(k, []) if v > 1.0), len(s10.get(k, [])))] for k in PATHS if k in s10]))


def main(folder):
    runs = load(folder)
    P = print
    P('## Run özeti (gerçek run; laboratuvar hariç)\n')
    rows = []
    order = lambda r: (r.set, r.label, r.policy)
    for key, rs in sorted(group(runs, order).items()):
        rs.sort(key=lambda r: r.seed)
        fails = ['R%d %s' % (r.end, quota_share(r)) for r in rs if not r.won]
        rows.append([key[0], key[1], key[2], len(rs), sum(r.won for r in rs), '; '.join(fails) or '-',
                     med([r.total for r in rs]), med([num(r.last['level']) for r in rs]),
                     med([num(r.last['levels']) for r in rs]), med([num(r.last['choices']) for r in rs]),
                     '%s / %s / %s' % (med([num(r.last['cardTiles']) for r in rs]), med([num(r.last['cardUpgrades']) for r in rs]), med([num(r.last['cardBase']) for r in rs])),
                     '%.0f dk' % (mean([r.end for r in rs]) * ROUND_SECONDS / 60)])
    P(table(['set', 'etiket', 'politika', 'run', 'kazanan', 'elenen (round, pay)', 'medyan toplam skor', 'medyan level', 'kazanılan level', 'seçim hakkı',
             'kart: tile / yükseltme / temel güç', 'ort. oyun süresi (yalnız round)'], rows))

    P('## İlk davranış kartı ve ilk gerçek tetik\n')
    rows = []
    for key, rs in sorted(group(runs, order).items()):
        fb = [num(r.last['firstBehavior']) for r in rs]; ft = [num(r.last['firstTrigger']) for r in rs]; fo = [num(r.last['firstOwn']) for r in rs]
        by6 = sum(1 for v in fb if 0 < v <= 6)
        rows.append([key[0], key[1], key[2], len(rs), '%d / %d' % (by6, len(rs)), ' '.join('%d' % v for v in fb), ' '.join('%d' % v for v in fo), ' '.join('%d' % v for v in ft)])
    P(table(['set', 'etiket', 'politika', 'run', 'R6 sonuna kadar alan', 'ilk patlama/elektrik kartı (round; 0 = yok)', 'kendi yolunun ilk kartı', 'ilk gerçek tetik'], rows))

    P('## Round pencereleri: skor ve hasat hızı, hasat payları, doluluk\n')
    for a, b in ((1, 10), (11, 19), (20, 30), (31, 40), (41, 50)):
        rows = []
        for key, rs in sorted(group(runs, order).items()):
            w = [x for r in rs for x in r.window(a, b)]
            if not w: continue
            rows.append([key[0], key[1], key[2], len({(r.key) for r in rs if r.window(a, b)}),
                         fm([num(x['scorePerSec']) for x in w]), fm([num(x['killsPerSec']) for x in w]),
                         '%.0f%%' % (100 * share(w, 'direct')), '%.0f%%' % (100 * (1 - share(w, 'direct'))),
                         '%.0f%%' % (100 * share(w, 'echoKills')), fm([num(x['fill']) for x in w]), fm([num(x['endFill']) for x in w]),
                         fm([num(x['hitsPerAttack']) for x in w])])
        P('### R%d–R%d\n' % (a, b))
        P(table(['set', 'etiket', 'politika', 'run', 'skor / sn', 'hasat / sn', 'doğrudan hasat payı', 'davranış hasat payı', 'yankı hasadı payı', 'ort. doluluk', 'round sonu doluluk', 'vuruş başına hedef'], rows))

    kir = {(r.policy, r.seed): r for r in runs if r.set == 'k36kirilma'}
    twin = {(r.policy, r.seed): r for r in runs if r.set == 'k36twin'}
    decline = {(r.policy, r.seed): r for r in runs if r.set == 'k36decline'}
    if decline:
        paired(P, kir, decline, 'almayan eş (aynı teklifler; bot kırılma ödülünü almaz, diğer iki seçenekten birini alır; ödülün alındığı round\'a kadar aynı run)')
    if twin:
        paired(P, kir, twin, 'ödülsüz eş (kırılma ödülleri havuzdan çıkarılmış; R10\'dan sonra küçük ödül teklifleri de farklılaşır)')

    # ---- level başına seçim sayısı × boss ödülü (2 × 2)
    P('## Level başına seçim sayısı × boss ödülleri (Kırılma V1, aynı politika, seed 100–104)\n')
    cells = defaultdict(dict)
    for r in runs:
        if r.profile != 'Run50_KirilmaV1' or r.seed > 104 or r.policy not in PATHS: continue
        if r.set == 'k36kirilma': key = ('3 seçim', 'ödül var')
        elif r.set == 'k36choice': key = ('1 seçim' if r.label.startswith('1') else '3 seçim', 'ödül yok' if 'yok' in r.label else 'ödül var')
        else: continue
        cells[(r.policy,) + key][r.seed] = r
    rows = []
    for key, rs in sorted(cells.items()):
        v = list(rs.values())
        w2 = [x for r in v for x in r.window(20, 30)]; w5 = [x for r in v for x in r.window(41, 50)]
        rows.append([key[0], key[1], key[2], len(v), sum(r.won for r in v), '; '.join('R%d' % r.end for r in v if not r.won) or '-', med([r.total for r in v]),
                     fm([num(x['scorePerSec']) for x in w2]), fm([num(x['scorePerSec']) for x in w5]), fm([num(x['killsPerSec']) for x in w5]),
                     med([num(r.last['levels']) for r in v]), med([num(r.last['choices']) for r in v]),
                     '%s / %s / %s' % (med([num(r.last['cardTiles']) for r in v]), med([num(r.last['cardUpgrades']) for r in v]), med([num(r.last['cardBase']) for r in v]))])
    P(table(['politika', 'seçim', 'boss ödülü', 'run', 'kazanan', 'elenen', 'medyan toplam skor', 'skor/sn R20–30', 'skor/sn R41–50', 'hasat/sn R41–50', 'level', 'seçim hakkı', 'kart: tile / yükseltme / temel güç'], rows))
    rows = []
    for policy in PATHS:
        for choice in ('3 seçim', '1 seçim'):
            a, b = cells.get((policy, choice, 'ödül var'), {}), cells.get((policy, choice, 'ödül yok'), {})
            ratios = [a[s].total / b[s].total for s in sorted(a) if s in b and b[s].total > 0]
            if ratios: rows.append([policy, choice, len(ratios), '×' + med(ratios, '%.2f'), '×%.2f – ×%.2f' % (min(ratios), max(ratios)), sum(a[s].won for s in a), sum(b[s].won for s in b)])
    P('Boss ödüllerinin toplam skora etkisi (ödül var ÷ ödül yok, aynı seed):\n')
    P(table(['politika', 'seçim', 'eş', 'toplam skor oranı (medyan)', 'aralık', 'kazanan (ödül var)', 'kazanan (ödül yok)'], rows))
    rows = []
    for policy in PATHS:
        for reward in ('ödül var', 'ödül yok'):
            a, b = cells.get((policy, '3 seçim', reward), {}), cells.get((policy, '1 seçim', reward), {})
            ratios = [a[s].total / b[s].total for s in sorted(a) if s in b and b[s].total > 0]
            if ratios: rows.append([policy, reward, len(ratios), '×' + med(ratios, '%.2f'), '×%.2f – ×%.2f' % (min(ratios), max(ratios))])
    P('Üç seçimin toplam skora etkisi (3 seçim ÷ 1 seçim, aynı seed):\n')
    P(table(['politika', 'boss ödülü', 'eş', 'toplam skor oranı (medyan)', 'aralık'], rows))

    # ---- görsel / havuz sınırları
    P('## Havuz sınırları ve atlanan işlemler (run başına toplam, ortalama)\n')
    rows = []
    for key, rs in sorted(group(runs, order).items()):
        tot = lambda f: mean([sum(num(x[f]) for x in r.rows) for r in rs])
        rows.append([key[0], key[1], key[2], '%.0f' % tot('echoSched'), '%.0f' % tot('echoExec'), '%.0f' % tot('echoDrop'), '%.0f' % tot('echoKills'), '%.0f' % tot('rhythmCharges'), '%.0f' % tot('rhythmAttacks'),
                     '%.0f' % tot('skipElectricVisual'), '%.0f' % tot('skipExplosionVisual'), '%.0f' % tot('skipBoomerang')])
    P(table(['set', 'etiket', 'politika', 'yankı planlanan', 'uygulanan', 'düşen (round sonu)', 'yankı hasadı', 'ritim hakkı', 'güçlü vuruş', 'atlanan elektrik çizimi', 'atlanan patlama çizimi', 'atlanan bumerang (oynanış)'], rows))

    # ---- laboratuvar
    P('## Kontrollü laboratuvar (normal run DEĞİL): aynı kayıt, aynı seed; A = ödülsüz, B = kırılma ödülüyle\n')
    labs = defaultdict(dict)
    for r in runs:
        for d in r.labs:
            parts = d['label'].split(' ')            # LAB R25 [sinerji] A|B ... #tekrar
            rnd = int(parts[1][1:]); syn = parts[2] == 'sinerji'; arm = parts[3] if syn else parts[2]
            cell = labs[(r.policy, rnd, syn)].setdefault((r.seed, arm), None)
            if cell is None:
                cell = labs[(r.policy, rnd, syn)][(r.seed, arm)] = dict(d); cell['n'] = 0
                for f in SUM: cell[f] = 0.0
                for f in AVG: cell[f] = 0.0
            cell['n'] += 1
            for f in SUM: cell[f] += num(d[f])
            for f in AVG: cell[f] += num(d[f])
    for cells in labs.values():
        for cell in cells.values():
            for f in AVG: cell[f] = '%.3f' % (cell[f] / cell['n'])
    rows = []; detail = []
    for (policy, rnd, syn), cells in sorted(labs.items()):
        kr = []; sr = []; eshare = []; bshare = []
        for seed in sorted({k[0] for k in cells}):
            a, b = cells.get((seed, 'A')), cells.get((seed, 'B'))
            if not a or not b: continue
            ka, kb, sa, sb = num(a['kills']), num(b['kills']), num(a['score']), num(b['score'])
            if ka > 0: kr.append(kb / ka)
            if sa > 0: sr.append(sb / sa)
            eshare.append(num(b['echoKills']) / kb if kb else 0)
            bshare.append(1 - num(b['direct']) / kb if kb else 0)
            detail.append([policy, 'R%d%s' % (rnd, ' sinerji' if syn else ''), seed, '%.0f' % ka, '%.0f' % kb, '×%.2f' % (kb / ka if ka else 0), '%.0f' % sa, '%.0f' % sb, '×%.2f' % (sb / sa if sa else 0),
                           '%.0f' % num(b['echoKills']), a['fill'], b['fill'], a['endFill'], b['endFill'], b['rhythmAttacks']])
        if kr:
            rows.append([policy, 'R%d%s' % (rnd, ' · güçlü sinerji' if syn else ''), len(kr), '×' + med(kr, '%.2f'), '×%.2f – ×%.2f' % (min(kr), max(kr)), '×' + med(sr, '%.2f'), '×%.2f – ×%.2f' % (min(sr), max(sr)),
                         '%.0f%%' % (100 * mean(eshare)), '%.0f%%' % (100 * mean(bshare)), sum(1 for v in kr if v >= 1.5), sum(1 for v in sr if v >= 1.5)])
    P(table(['politika', 'kurulum', 'eş', 'hasat oranı B÷A (medyan)', 'aralık', 'skor oranı B÷A (medyan)', 'aralık', 'B\'de yankı hasadı payı', 'B\'de davranış hasadı payı', 'hasat ≥×1,5', 'skor ≥×1,5'], rows))
    P('### Laboratuvar, seed bazında\n')
    P(table(['politika', 'kurulum', 'seed', 'hasat A', 'hasat B', 'oran', 'skor A', 'skor B', 'oran', 'yankı hasadı (B)', 'doluluk A', 'doluluk B', 'round sonu A', 'round sonu B', 'güçlü vuruş (B)'], detail))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main(sys.argv[1])
