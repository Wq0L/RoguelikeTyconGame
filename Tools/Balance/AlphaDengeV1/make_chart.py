"""Bölüm 3.7.8 grafiği (SVG, ek kütüphane gerekmez). Round ekseninde build gücü ile oyun gereksinimi, tanımlı ölçülerle:

  1. Skor / sn: dönem başına build medyanı (ölçüm) ↔ gereksinim = kota ÷ dönemin aktif saniyesi (tasarım hedefi).
  2. Kota payı = dönem skoru ÷ kota (ölçüm); 1,0 = geçme sınırı, 2,0 = üstünlük eşiği.
  3. Gerçek hasat / sn (ölçüm).
  4. Can puanı: Common bitki canı (çapa noktaları = tasarım, aradaki round'lar = geometrik ara değer) ↔ doğrudan vuruş hasarı (ölçüm).

  python make_chart.py <çıktı.svg> <runs.csv[,…]> [--tablo=params|kural|eski] [--baslik=…] [politika=etiket …]

Ölçüm = dolu işaret; ölçümler arası çizgi yalnız okumayı kolaylaştırır (ara değer). Bot ölçümüdür.
"""
import io
import math
import sys
import os

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import alpha_denge_v1_params as P
import analyze_alpha as A

COLORS = ['#d1495b', '#edae49', '#00798c', '#6a4c93', '#30638e', '#8d99ae', '#5b8e7d']
W, PANEL_H, LEFT, RIGHT, TOP, GAP = 1180, 250, 78, 250, 46, 64
PLOT_W = W - LEFT - RIGHT


def x_of(round_): return LEFT + (round_ - 1) / 49.0 * PLOT_W


class Panel:
    def __init__(self, index, title, unit, lo, hi, log):
        self.top = TOP + index * (PANEL_H + GAP)
        self.title, self.unit, self.lo, self.hi, self.log = title, unit, lo, hi, log
        self.out = []

    def y(self, v):
        if self.log:
            v = min(max(v, self.lo), self.hi)
            t = (math.log10(v) - math.log10(self.lo)) / (math.log10(self.hi) - math.log10(self.lo))
        else:
            t = (min(max(v, self.lo), self.hi) - self.lo) / (self.hi - self.lo)
        return self.top + PANEL_H - t * PANEL_H

    def frame(self, ticks):
        o = self.out
        o.append('<rect x="%d" y="%d" width="%d" height="%d" fill="#fbfaf6" stroke="#3a3340" stroke-width="1"/>' % (LEFT, self.top, PLOT_W, PANEL_H))
        # üstünlük penceresi aralığı (R27–46)
        o.append('<rect x="%.1f" y="%d" width="%.1f" height="%d" fill="#e9e4d4" opacity="0.7"/>' % (x_of(27), self.top, x_of(46) - x_of(27), PANEL_H))
        o.append('<text x="%.1f" y="%d" font-size="11" fill="#7a7060" text-anchor="middle">değerlendirme penceresi R27–46</text>' % ((x_of(27) + x_of(46)) / 2, self.top + 14))
        for date in A.DATES:
            o.append('<line x1="%.1f" y1="%d" x2="%.1f" y2="%d" stroke="#c9c2b2" stroke-width="1" stroke-dasharray="2 4"/>' % (x_of(date), self.top, x_of(date), self.top + PANEL_H))
        for tick in ticks:
            y = self.y(tick)
            o.append('<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" stroke="#ddd6c6" stroke-width="1"/>' % (LEFT, y, LEFT + PLOT_W, y))
            label = ('%g' % tick).replace('.', ',')
            o.append('<text x="%d" y="%.1f" font-size="11" fill="#3a3340" text-anchor="end">%s</text>' % (LEFT - 6, y + 4, label))
        for r in (1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50):
            o.append('<text x="%.1f" y="%d" font-size="11" fill="#3a3340" text-anchor="middle">R%d</text>' % (x_of(r), self.top + PANEL_H + 15, r))
        o.append('<text x="%d" y="%d" font-size="14" font-weight="bold" fill="#3a3340">%s</text>' % (LEFT, self.top - 8, self.title))
        o.append('<text x="%d" y="%d" font-size="11" fill="#3a3340" text-anchor="end">%s</text>' % (LEFT + PLOT_W, self.top - 8, self.unit))

    def series(self, points, color, dash=None, width=1.4, marker=True):
        pts = [(x_of(r), self.y(v)) for r, v in points if v is not None and not math.isnan(v) and (not self.log or v > 0)]
        if len(pts) >= 2:
            self.out.append('<polyline points="%s" fill="none" stroke="%s" stroke-width="%s"%s opacity="0.85"/>' % (
                ' '.join('%.1f,%.1f' % p for p in pts), color, width, ' stroke-dasharray="%s"' % dash if dash else ''))
        if marker:
            for x, y in pts: self.out.append('<circle cx="%.1f" cy="%.1f" r="3.2" fill="%s" stroke="#fff" stroke-width="0.8"/>' % (x, y, color))

    def steps(self, values, color, width=2.6, dash=None):
        """Dönem başına sabit değer (tasarım tablosu): basamak çizgisi."""
        pts = []
        for i, (a, b) in enumerate(A.PERIODS):
            y = self.y(values[i])
            pts += [(x_of(a - 0.5 if a > 1 else 1), y), (x_of(min(50, b + 0.5)), y)]
        self.out.append('<polyline points="%s" fill="none" stroke="%s" stroke-width="%s"%s/>' % (
            ' '.join('%.1f,%.1f' % p for p in pts), color, width, ' stroke-dasharray="%s"' % dash if dash else ''))

    def hline(self, value, color, label, dash='6 4'):
        y = self.y(value)
        self.out.append('<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" stroke="%s" stroke-width="1.4" stroke-dasharray="%s"/>' % (LEFT, y, LEFT + PLOT_W, y, color, dash))
        self.out.append('<text x="%d" y="%.1f" font-size="11" fill="%s">%s</text>' % (LEFT + 6, y - 4, color, label))


def health(round_):
    """PlantHealthScalingSO.Calculate ile aynı geometrik ara değer (Common, yuvarlamadan önce)."""
    anchors = P.HP_ANCHORS
    lower = max((a for a in anchors if a[0] <= round_), key=lambda a: a[0])
    upper = min((a for a in anchors if a[0] >= round_), key=lambda a: a[0])
    if lower[0] == upper[0]: return float(lower[1])
    return lower[1] * (upper[1] / lower[1]) ** ((round_ - lower[0]) / (upper[0] - lower[0]))


def main():
    out_path, rows = sys.argv[1], A.read(sys.argv[2])
    options = dict(a[2:].split('=', 1) for a in sys.argv[3:] if a.startswith('--'))
    wanted = [a.split('=', 1) for a in sys.argv[3:] if not a.startswith('--')]
    runs = A.runs_of(rows)
    groups = A.groups(runs)
    if wanted: series = [(name, groups[key]) for policy, name in wanted for key in groups if key[0] == policy or '%s|%s' % key == policy]
    else: series = [('%s · %s' % key, group) for key, group in groups.items()]
    quota, boss = A.tables(options.get('tablo', 'params'), runs)
    title = options.get('baslik', 'Run50 Alpha Denge V1 — build gücü ve oyun gereksinimi (bot ölçümü, seed medyanları)')
    mid = [(a + b) / 2.0 for a, b in A.PERIODS]

    def per_period(group, fn):
        pts = []
        for i in range(15):
            values = [fn(run, i) for run in group]
            values = [v for v in values if v is not None]
            pts.append((mid[i], A.med(values) if values else None))
        return pts

    def alive(run, i):
        failed = A.evaluate(run, quota, boss)[0]
        return run.period_score(i) is not None and (failed is None or i <= failed)

    rate = lambda run, i: run.period_score(i) / A.period_seconds(i) if alive(run, i) else None
    share = lambda run, i: run.period_score(i) / quota[i] if alive(run, i) else None

    def kills(run, i):
        if not alive(run, i): return None
        a, b = A.PERIODS[i]
        return sum(A.num(run.rounds[r]['kills']) for r in range(a, b + 1)) / A.period_seconds(i)

    def damage(run, i):
        if not alive(run, i): return None
        r = A.PERIODS[i][1]
        return (A.col(run, r, 'damage') or 0) * (A.col(run, r, 'directMult') or 1)

    top_rate = max([v for _, g in series for _, v in per_period(g, rate) if v] + [q / A.period_seconds(i) for i, q in enumerate(quota)])
    top_share = max([v for _, g in series for _, v in per_period(g, share) if v] + [4])
    top_kills = max([v for _, g in series for _, v in per_period(g, kills) if v] + [1])
    top_damage = max([v for _, g in series for _, v in per_period(g, damage) if v] + [health(50) * 3])

    def decade_ticks(lo, hi):
        ticks, v = [], lo
        while v <= hi * 1.0001: ticks.append(v); v *= 10
        return ticks

    lo_rate = 10 ** math.floor(math.log10(min(q / A.period_seconds(i) for i, q in enumerate(quota))))
    hi_rate = 10 ** math.ceil(math.log10(top_rate))
    hi_share = 10 ** math.ceil(math.log10(top_share))
    hi_damage = 10 ** math.ceil(math.log10(top_damage))
    p1 = Panel(0, '1 · Skor hızı: build (dönem medyanı) ve gereksinim', 'Harvest Score / sn (log)', lo_rate, hi_rate, True)
    p2 = Panel(1, '2 · Kota payı = dönem skoru ÷ kota', 'oran (log)', 0.5, max(10, hi_share), True)
    step = 2 if top_kills <= 12 else 5 if top_kills <= 30 else 10
    p3 = Panel(2, '3 · Gerçek hasat', 'hasat edilen bitki / sn', 0, math.ceil(top_kills / step) * step, False)
    p4 = Panel(3, '4 · Common bitki canı ve doğrudan vuruş hasarı', 'can puanı (log)', 10, hi_damage, True)
    p1.frame(decade_ticks(lo_rate, hi_rate)); p2.frame([0.5, 1, 2, 5] + [t for t in decade_ticks(10, max(10, hi_share))])
    p3.frame([i * step for i in range(int(p3.hi / step) + 1)]); p4.frame(decade_ticks(10, hi_damage))

    p1.steps([q / A.period_seconds(i) for i, q in enumerate(quota)], '#1b1b1e')
    p1.steps([2 * q / A.period_seconds(i) for i, q in enumerate(quota)], '#1b1b1e', 1.2, '3 4')
    p2.hline(1.0, '#1b1b1e', 'kota (1,0): altı kayıp')
    p2.hline(A.WINDOW_SHARE, '#9a3b3b', 'üstünlük eşiği (2,0)')
    # can: çapa noktaları (tasarım) + geometrik ara değer
    p4.series([(r, health(r)) for r in range(1, 51)], '#1b1b1e', '5 3', 1.6, marker=False)
    for r, hp in P.HP_ANCHORS: p4.out.append('<rect x="%.1f" y="%.1f" width="7" height="7" fill="#1b1b1e"/>' % (x_of(r) - 3.5, p4.y(hp) - 3.5))
    p4.series([(r, health(r) * 3) for r in range(1, 51)], '#8d8d8d', '2 4', 1.2, marker=False)

    for n, (name, group) in enumerate(series):
        color = COLORS[n % len(COLORS)]
        p1.series(per_period(group, rate), color, '4 3')
        p2.series(per_period(group, share), color, '4 3')
        p3.series(per_period(group, kills), color, '4 3')
        p4.series(per_period(group, damage), color, '4 3')

    height = TOP + 4 * (PANEL_H + GAP) + 10
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d" font-family="Segoe UI, Arial, sans-serif">' % (W, height, W, height),
           '<rect width="%d" height="%d" fill="#ffffff"/>' % (W, height),
           '<text x="%d" y="20" font-size="16" font-weight="bold" fill="#1b1b1e">%s</text>' % (LEFT, title)]
    for p in (p1, p2, p3, p4): svg += p.out
    # açıklama
    lx, ly = LEFT + PLOT_W + 18, TOP + 6
    legend = ['<text x="%d" y="%d" font-size="12" font-weight="bold" fill="#1b1b1e">Gerçek ölçüm (dönem medyanı)</text>' % (lx, ly)]
    for n, (name, group) in enumerate(series):
        y = ly + 20 + n * 18
        legend.append('<circle cx="%d" cy="%d" r="3.6" fill="%s"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">%s (%d run)</text>' % (lx + 6, y - 4, COLORS[n % len(COLORS)], lx + 16, y, name, len(group)))
    y = ly + 34 + len(series) * 18
    legend += [
        '<text x="%d" y="%d" font-size="12" font-weight="bold" fill="#1b1b1e">Ara değer</text>' % (lx, y),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#555" stroke-width="1.4" stroke-dasharray="4 3"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">ölçümler arası çizgi</text>' % (lx, y + 14, lx + 26, y + 14, lx + 32, y + 18),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#1b1b1e" stroke-width="1.6" stroke-dasharray="5 3"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">can: çapalar arası</text>' % (lx, y + 32, lx + 26, y + 32, lx + 32, y + 36),
        '<text x="%d" y="%d" font-size="12" fill="#1b1b1e">geometrik ara değer</text>' % (lx + 32, y + 50),
        '<text x="%d" y="%d" font-size="12" font-weight="bold" fill="#1b1b1e">Tasarım hedefi (tablo)</text>' % (lx, y + 76),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#1b1b1e" stroke-width="2.6"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">kota ÷ dönem süresi</text>' % (lx, y + 90, lx + 26, y + 90, lx + 32, y + 94),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#1b1b1e" stroke-width="1.2" stroke-dasharray="3 4"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">kotanın iki katı</text>' % (lx, y + 108, lx + 26, y + 108, lx + 32, y + 112),
        '<rect x="%d" y="%d" width="7" height="7" fill="#1b1b1e"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">Common can çapası</text>' % (lx + 9, y + 122, lx + 32, y + 130),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#8d8d8d" stroke-width="1.2" stroke-dasharray="2 4"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">Legendary canı (×3)</text>' % (lx, y + 144, lx + 26, y + 144, lx + 32, y + 148),
        '<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#c9c2b2" stroke-width="1" stroke-dasharray="2 4"/><text x="%d" y="%d" font-size="12" fill="#1b1b1e">boss round\'u</text>' % (lx + 13, y + 158, lx + 13, y + 172, lx + 32, y + 170),
        '<text x="%d" y="%d" font-size="11" fill="#555">Hasar: doğrudan vuruşun taban</text>' % (lx, y + 200),
        '<text x="%d" y="%d" font-size="11" fill="#555">hasarı × ödül çarpanı (kritiksiz).</text>' % (lx, y + 214),
        '<text x="%d" y="%d" font-size="11" fill="#555">Elenen run, elendiği dönemden</text>' % (lx, y + 234),
        '<text x="%d" y="%d" font-size="11" fill="#555">sonra medyana girmez.</text>' % (lx, y + 248),
    ]
    svg += legend
    svg.append('</svg>')
    io.open(out_path, 'w', encoding='utf-8', newline='\n').write('\n'.join(svg) + '\n')
    print('yazildi: %s (%d seri)' % (out_path, len(series)))


if __name__ == '__main__':
    if len(sys.argv) < 3: raise SystemExit(__doc__)
    main()
