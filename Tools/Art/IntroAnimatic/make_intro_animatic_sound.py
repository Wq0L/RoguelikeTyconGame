"""Açılış animatiği — geçici ses (28 Ekim 2026). Yalnız kurgu sınaması için; nihai ses değildir.
  python Tools/Art/IntroAnimatic/make_intro_animatic_sound.py
Çıktı: Docs/AcilisVideosu/Animatik/gecici-ses.wav (48 kHz, mono, 16 bit, 10 sn).
Özgün sentez (gürültü + sinüs); dış kayıt, örnek veya müzik yok. Kare zamanları Docs/AcilisVideosu-CekimListesi.md ile aynı.
Ses çizgisi: gürültü → motor → bip → öksürük → sessizlik → çekim uğultusu → rüzgâr → darbe → çıtırtı.
"""
import math, random, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'gecici-ses.wav'
SR, DUR, FPS = 48000, 10.0, 24
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


LOW = norm(lowpass(white, 110))
MID = norm(lowpass(white, 600))
L300, L1500 = lowpass(white, 300), lowpass(white, 1500)
BAND = norm([b - a for a, b in zip(L300, L1500)])
L2000 = lowpass(white, 2000, 1)
HIGH = norm([w - l for w, l in zip(white, L2000)])
CRUNCH = norm(L2000)
mix = [0.0] * N


def curve(t, pts):
    """Parçalı doğrusal zarf: [(zaman, değer), ...]."""
    if t <= pts[0][0]: return pts[0][1]
    for (t0, v0), (t1, v1) in zip(pts, pts[1:]):
        if t <= t1: return v0 + (v1 - v0) * (t - t0) / max(t1 - t0, 1e-9)
    return pts[-1][1]


def add(t0, t1, fn):
    for i in range(max(0, int(t0 * SR)), min(N, int(t1 * SR))):
        mix[i] += fn(i / SR, i)


# Ç1 — derinden yükselen gürültü; Ç2'de geride kalıp azalır
add(0, 4.0, lambda t, i: curve(t, [(0, .2), (f(10), .32), (f(22), .45), (1.95, .62), (2.3, .3), (4.0, 0)])
    * (LOW[i] * 1.6 + .35 * math.sin(TAU * 38 * t)))
for tc in (f(10), f(22)):   # iki çatırtı
    add(tc, tc + .5, lambda t, i, tc=tc: (.75 * HIGH[i] * math.exp(-(t - tc) * 28) + .6 * math.sin(TAU * 55 * (t - tc)) * math.exp(-(t - tc) * 10)))


# Motor: kesmeden 4 kare önce başlar (f44), Ç3a'da kokpitten boğuk, f114'ten sonra öksürür, f125'te susar
def engine_amp(t):
    if t < f(114): return curve(t, [(f(44), 0), (f(48), .5), (f(95), .5), (f(97), .3), (f(114), .3)])
    bursts = sum(.55 * math.exp(-((t - c) / .028) ** 2) for c in (f(116), f(121)))
    tail = .25 * math.exp(-((t - f(124)) / .02) ** 2)
    return (.04 if t < f(125) else 0) + bursts + tail


add(f(44), f(126), lambda t, i: engine_amp(t) * ((MID[i] if t < f(96) else LOW[i] * 1.3) + .3 * (((62 + 3 * math.sin(TAU * 7 * t)) * t) % 1 - .5)))
for tb in (f(104), f(108)):   # iki uyarı biplemesi, parmak vuruşlarıyla aynı karede
    add(tb, tb + .09, lambda t, i, tb=tb: .3 * math.sin(TAU * 1250 * t) * min(1, (t - tb) / .005, (tb + .09 - t) / .01))

# f125–f148 tam sessizlik. Ç4: alçak çekim uğultusu, f172'de rüzgâr başlar
add(f(148), f(213), lambda t, i: curve(t, [(f(148), 0), (f(178), .3), (8.6, .16), (f(213), 0)])
    * (math.sin(TAU * 46 * t) + .35 * math.sin(TAU * 92 * t) + .2 * math.sin(TAU * 69 * t + math.sin(TAU * .7 * t))))
add(f(172), f(213) + .03, lambda t, i: curve(t, [(f(172), 0), (f(180), .2), (f(212), .62), (f(213) + .03, 0)]) * BAND[i] * 1.4)
add(8.0, f(213), lambda t, i: curve(t, [(8.0, 0), (f(212), .1)]) * math.sin(TAU * (700 * (t - 8.0) + 500 * (t - 8.0) ** 2 / .875)))

# Ç6: darbe, kayma, çıtırtı
T_HIT = f(213)
add(T_HIT, T_HIT + 1.1, lambda t, i: .95 * math.sin(TAU * 42 * (t - T_HIT)) * math.exp(-(t - T_HIT) * 5)
    + .7 * CRUNCH[i] * math.exp(-(t - T_HIT) * 9))
add(T_HIT + .03, T_HIT + .7, lambda t, i: .3 * MID[i] * math.exp(-(t - T_HIT) * 3))
for tt in (9.45, 9.57, 9.66, 9.79, 9.86, 9.95):
    add(tt, tt + .03, lambda t, i, tt=tt: .32 * HIGH[i] * math.exp(-(t - tt) * 380))

peak = max(abs(v) for v in mix) or 1
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * .89 / peak)) * 32767)) for v in mix))
print('SES', OUT, f'{DUR} sn')
