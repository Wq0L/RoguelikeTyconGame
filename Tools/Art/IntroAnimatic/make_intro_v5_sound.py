"""Açılış previs v5 — geçici ses (28 Ekim 2026). Yalnız kurgu ve vurgu sınaması için; nihai ses değildir.
  python Tools/Art/IntroAnimatic/make_intro_v5_sound.py
Çıktı: Docs/AcilisVideosu/Animatik/v5/gecici-ses-v5.wav (48 kHz, mono, 16 bit, 14,75 sn).
Özgün sentez (gürültü + sinüs); dış kayıt, örnek veya müzik yok. Kare zamanları build_intro_v5.py ile aynı (tek dünya, kesintisiz kamera).
v3'ten farkı: şok dalgası f140'ta; ardından lomboza dönüş vınlaması ve camın içindeki ışığın uğultusu, kokpit f154'te;
öksürükler ve susma f191–202; atmosfere girişte parlama sesi yerine bulut tabakasına dalış hışırtısı; Ç6 bulut
perdesinin açılmasıyla başlar.
"""
import math, random, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v5' / 'gecici-ses-v5.wav'
SR, FPS, END = 48000, 24, 354
DUR = END / FPS
N = int(SR * DUR)
TAU = 2 * math.pi


def f(frame): return frame / FPS


rng = random.Random(28)
white = [rng.uniform(-1, 1) for _ in range(N)]


def lowpass(x, hz, passes=2):
    a = 1 - math.exp(-TAU * hz / SR)
    for _ in range(passes):
        y, out = 0.0, [0.0] * len(x)
        for i, v in enumerate(x): y += a * (v - y); out[i] = y
        x = out
    return x


def norm(x, rms=.3):
    r = math.sqrt(sum(v * v for v in x) / len(x)) or 1
    return [v * rms / r for v in x]


LOW = norm(lowpass(white, 110)); MID = norm(lowpass(white, 600))
L300, L1500, L2000 = lowpass(white, 300), lowpass(white, 1500), lowpass(white, 2000, 1)
BAND = norm([b - a for a, b in zip(L300, L1500)])
HIGH = norm([w - l for w, l in zip(white, L2000)]); CRUNCH = norm(L2000)
mix = [0.0] * N


def curve(t, pts):
    if t <= pts[0][0]: return pts[0][1]
    for (t0, v0), (t1, v1) in zip(pts, pts[1:]):
        if t <= t1: return v0 + (v1 - v0) * (t - t0) / max(t1 - t0, 1e-9)
    return pts[-1][1]


def add(t0, t1, fn):
    for i in range(max(0, int(t0 * SR)), min(N, int(t1 * SR))): mix[i] += fn(i / SR, i)


def boom(t0, amp, hz=42, decay=5.0, noise=.7, ndecay=9.0, length=1.2):
    add(t0, t0 + length, lambda t, i: amp * (math.sin(TAU * hz * (t - t0)) * math.exp(-(t - t0) * decay)
                                              + noise * CRUNCH[i] * math.exp(-(t - t0) * ndecay)))


def whoosh(t0, t1, peak, amp):
    add(t0, t1, lambda t, i: amp * curve(t, [(t0, 0), (peak, 1), (t1, 0)]) * BAND[i] * 1.6)


def click(t0, amp=.3, decay=380):
    add(t0, t0 + .03, lambda t, i: amp * HIGH[i] * math.exp(-(t - t0) * decay))


def beep(t0, hz=1250, amp=.28, d=.09):
    add(t0, t0 + d, lambda t, i: amp * math.sin(TAU * hz * t) * min(1, (t - t0) / .005, (t0 + d - t) / .01))


def saw(hz, t): return ((hz * t) % 1) - .5


# Ç1 — gürültü, çatırtılar, büyüyen titreme, bir anlık sessizlik (f40–41), büyük patlama, fırlayış, kamçı
add(0, f(42), lambda t, i: curve(t, [(0, .16), (f(14), .28), (f(28), .42), (f(39), .7), (f(40), .05), (f(42), 0)])
    * (LOW[i] * 1.6 + .35 * math.sin(TAU * 38 * t)) + curve(t, [(f(30), 0), (f(39), .25), (f(40), 0)]) * HIGH[i])
for tc, a in ((f(14), .55), (f(28), .75)):
    add(tc, tc + .5, lambda t, i, tc=tc, a=a: a * (HIGH[i] * math.exp(-(t - tc) * 26) + .8 * math.sin(TAU * 55 * (t - tc)) * math.exp(-(t - tc) * 10)))
boom(f(42), 1.0, 34, 3.2, .9, 5.5, 1.8)
add(f(42), f(96), lambda t, i: curve(t, [(f(42), .5), (f(70), .3), (f(96), .15)]) * LOW[i] * 1.4)
whoosh(f(44), f(60), f(52), .55)
add(f(44), f(59), lambda t, i: .14 * curve(t, [(f(44), 0), (f(50), 1), (f(59), 0)]) * math.sin(TAU * (500 * (t - f(44)) + 900 * (t - f(44)) ** 2)))

# Kalkış (v5): ateş dağılır, roketin motoru kameranın yanında yükselir (kesme yok: tek ses çizgisi)
whoosh(f(56), f(74), f(64), .55)
add(f(58), f(96), lambda t, i: curve(t, [(f(58), 0), (f(70), .5), (f(88), .7), (f(96), .45)])
    * (MID[i] + .35 * saw(66 + 40 * curve(t, [(f(58), 0), (f(88), 1), (f(96), .5)]), t)))
whoosh(f(84), f(104), f(96), .45)   # kamera gövdeye geçer

# Ç2b — gemide motor (sürekli), uzaklaştıkça sönen Dünya gürültüsü, arkadan yetişen şok dalgası, çarpma
SHOCK, T3A = 140, 154
add(f(96), f(T3A), lambda t, i: curve(t, [(f(96), .45), (f(SHOCK), .45), (f(T3A), .2)]) * MID[i] + .22 * saw(64 + 3 * math.sin(TAU * 9 * t), t))
add(f(96), f(SHOCK), lambda t, i: math.exp(-(t - f(96)) * 1.3) * .55 * LOW[i] * 1.5)
boom(f(104), .45, 38, 3, .5, 4, 1.4)
add(f(124), f(SHOCK), lambda t, i: curve(t, [(f(124), 0), (f(SHOCK - 1), .85), (f(SHOCK), 0)]) * (BAND[i] * 1.4 + .2 * math.sin(TAU * (90 + 300 * curve(t, [(f(124), 0), (f(SHOCK), 1)])) * t)))
boom(f(SHOCK), 1.0, 48, 6, 1.0, 8, 1.0)
whoosh(f(SHOCK + 2), f(T3A + 1), f(T3A - 3), .35)                       # kamera lomboza döner
add(f(T3A - 5), f(T3A + 2), lambda t, i: curve(t, [(f(T3A - 5), 0), (f(T3A), .18), (f(T3A + 2), 0)]) * math.sin(TAU * 220 * t))   # camın içindeki ışık

# Ç3a — boğuk motor, sarsıntı tıkırtısı, ibre, iki vuruş + bip, bekleyiş
add(f(T3A), f(184), lambda t, i: .3 * LOW[i] * 1.3 + .12 * saw(62, t))
for k in range(6): click(f(T3A) + k * .035, .25, 250)
click(f(158), .2); click(f(160), .22)
click(f(168), .32, 180); click(f(174), .32, 180); beep(f(168)); beep(f(174))


# Ç3b — dışarıda motor, öksürük (191), parlama (194), öksürük (198), susar (202); sessizlik
def engine_amp(t):
    if t < f(190): return .55
    if t >= f(202): return 0
    return .05 + sum(a * math.exp(-((t - c) / .03) ** 2) for c, a in ((f(191), .3), (f(194), .75), (f(198), .3)))


add(f(184), f(203), lambda t, i: engine_amp(t) * (MID[i] + .3 * saw(62 + 3 * math.sin(TAU * 7 * t), t)))

# Ç4 — sessizlik (f202–232), yakalanma uğultusu, ısınma, bulut tabakasına dalış
add(f(232), f(316), lambda t, i: curve(t, [(f(232), 0), (f(242), .3), (f(270), .26), (f(300), .12), (f(316), 0)])
    * (math.sin(TAU * 46 * t) + .35 * math.sin(TAU * 92 * t) + .2 * math.sin(TAU * 69 * t + math.sin(TAU * .7 * t))))
add(f(250), f(283), lambda t, i: curve(t, [(f(250), 0), (f(274), .35), (f(283), .1)]) * HIGH[i])
whoosh(f(266), f(288), f(281), .7)
whoosh(f(182), f(192), f(186), .3)    # camdan dışarı çıkış
whoosh(f(286), f(300), f(292), .35)   # bulut tabanından çıkış
whoosh(f(292), f(308), f(304), .4)    # kamera yere doğru süzülür

# Ç5 — rüzgâr ve ıslık; Ç6 — yaklaşma, darbe, kayma, taşlar, ışık
add(f(280), f(316) + .03, lambda t, i: curve(t, [(f(280), 0), (f(288), .35), (f(315), .72), (f(316) + .03, 0)]) * BAND[i] * 1.4)
add(f(296), f(316), lambda t, i: curve(t, [(f(296), 0), (f(315), .1)]) * math.sin(TAU * (700 * (t - f(296)) + 700 * (t - f(296)) ** 2)))
boom(f(316), 1.0, 40, 4.5, .8, 8, 1.4)
add(f(317), f(336), lambda t, i: .3 * MID[i] * math.exp(-(t - f(317)) * 3))
for k in range(9): click(f(318) + k * .07 + rng.uniform(0, .03), .22, 300)
add(f(338), f(338) + .4, lambda t, i: .2 * math.exp(-(t - f(338)) * 8) * (math.sin(TAU * 880 * t) + .6 * math.sin(TAU * 1320 * t)))
for tt in (f(342), f(345), f(348)): click(tt, .18)

peak = max(abs(v) for v in mix) or 1
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * .89 / peak)) * 32767)) for v in mix))
print('SES', OUT, f'{DUR} sn')
