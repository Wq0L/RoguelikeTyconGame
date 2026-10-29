"""Açılış previs v2 — geçici ses (28 Ekim 2026). Yalnız kurgu ve vurgu sınaması için; nihai ses değildir.
  python Tools/Art/IntroAnimatic/make_intro_previs_sound.py
Çıktı: Docs/AcilisVideosu/Animatik/v2/gecici-ses-v2.wav (48 kHz, mono, 16 bit, 10,5 sn).
Özgün sentez (gürültü + sinüs); dış kayıt, örnek veya müzik yok. Kare zamanları build_intro_previs.py ile aynı.
Her vurgunun sesi var: çatırtı, patlama, fırlayış vınlaması, kıl payı, bip, motorun sönmesi + sessizlik,
yakalanma uğultusu, ısınma, rüzgâr, darbe, kayma, ışığın yanması ("tın").
"""
import math, random, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v2' / 'gecici-ses-v2.wav'
SR, FPS, END = 48000, 24, 252
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


# Ç1 — gürültü, iki çatırtı, büyük patlama, fırlayan şeyin vınlaması, kamçı
add(0, f(100), lambda t, i: curve(t, [(0, .18), (f(8), .3), (f(20), .45), (f(32), .7), (f(48), .45), (f(100), 0)])
    * (LOW[i] * 1.6 + .35 * math.sin(TAU * 38 * t)))
for tc, a in ((f(8), .55), (f(20), .75)):
    add(tc, tc + .5, lambda t, i, tc=tc, a=a: a * (HIGH[i] * math.exp(-(t - tc) * 26) + .8 * math.sin(TAU * 55 * (t - tc)) * math.exp(-(t - tc) * 10)))
boom(f(32), 1.0, 36, 3.5, .9, 6.0, 1.6)
whoosh(f(32), f(48), f(40), .55)
add(f(32), f(47), lambda t, i: .14 * curve(t, [(f(32), 0), (f(38), 1), (f(47), 0)]) * math.sin(TAU * (500 * (t - f(32)) + 900 * (t - f(32)) ** 2)))


# Motor: kesmeden 4 kare önce (f44), kokpitte boğuk (f100–117), f120/124/126 öksürür, f129 susar
def engine_amp(t):
    if t < f(118): return curve(t, [(f(44), 0), (f(48), .5), (f(99), .55), (f(100), .3), (f(118), .3)])
    if t < f(120): return .5
    bursts = sum(a * math.exp(-((t - c) / .03) ** 2) for c, a in ((f(122), .3), (f(124), .7), (f(126), .25)))
    return (.05 if t < f(129) else 0) + bursts


add(f(44), f(130), lambda t, i: engine_amp(t) * ((LOW[i] * 1.3) if f(100) <= t < f(118) else MID[i])
    + engine_amp(t) * .3 * (((62 + 3 * math.sin(TAU * 7 * t)) * t) % 1 - .5))
whoosh(f(62), f(76), f(70), .7)                  # kıl payı
whoosh(f(90), f(100), f(99), .5)                 # lomboza dalış
click(f(104), .25); click(f(108), .3, 200); click(f(112), .3, 200)   # ibre, parmak vuruşları
beep(f(108)); beep(f(112))

# f129–f150 tam sessizlik. Ç4: yakalanma uğultusu, ısınma, parıltı
add(f(150), f(212), lambda t, i: curve(t, [(f(150), 0), (f(158), .3), (f(178), .26), (8.6, .12), (f(212), 0)])
    * (math.sin(TAU * 46 * t) + .35 * math.sin(TAU * 92 * t) + .2 * math.sin(TAU * 69 * t + math.sin(TAU * .7 * t))))
add(f(164), f(181), lambda t, i: curve(t, [(f(164), 0), (f(178), .35), (f(181), .1)]) * HIGH[i])
whoosh(f(172), f(184), f(178), .6)

# Ç5: rüzgâr ve ıslık; Ç6: duman, darbe, kayma, taşlar, ışık
add(f(178), f(212) + .03, lambda t, i: curve(t, [(f(178), 0), (f(184), .35), (f(211), .7), (f(212) + .03, 0)]) * BAND[i] * 1.4)
add(8.1, f(212), lambda t, i: curve(t, [(8.1, 0), (f(211), .1)]) * math.sin(TAU * (700 * (t - 8.1) + 600 * (t - 8.1) ** 2)))
boom(f(212), 1.0)
add(f(213), f(230), lambda t, i: .3 * MID[i] * math.exp(-(t - f(213)) * 3))
for k in range(9): click(f(214) + k * .07 + rng.uniform(0, .03), .22, 300)
add(f(236), f(236) + .35, lambda t, i: .2 * math.exp(-(t - f(236)) * 9) * (math.sin(TAU * 880 * t) + .6 * math.sin(TAU * 1320 * t)))
for tt in (f(240), f(243), f(246), f(249)): click(tt, .2)

peak = max(abs(v) for v in mix) or 1
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * .89 / peak)) * 32767)) for v in mix))
print('SES', OUT, f'{DUR} sn')
