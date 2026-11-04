"""Açılış previs v5 — geçici ses (28 Ekim 2026). Yalnız kurgu ve vurgu sınaması için; nihai ses değildir.
  python Tools/Art/IntroAnimatic/make_intro_v5_sound.py
Çıktı: Docs/AcilisVideosu/Animatik/v5/gecici-ses-v5.wav (48 kHz, mono, 16 bit, 14,75 sn).
Özgün sentez (gürültü + sinüs); dış kayıt, örnek veya müzik yok. Kare zamanları build_intro_v5.py ile aynı (tek dünya, kesintisiz kamera).
v3'ten farkı: şok dalgası f140'ta; ardından lomboza dönüş vınlaması ve camın içindeki ışığın uğultusu, kokpit f154'te;
öksürükler ve susma f191–202; atmosfere girişte parlama sesi yerine bulut tabakasına dalış hışırtısı; Ç6 bulut
perdesinin açılmasıyla başlar.
v5.5: kokpit f154–172 (el ve vuruşlar yok, uyarı lambasının iki bibi); dışarıda boğulma "pof" f186, üç tık f190/194/200,
susma f202.
v5.7 (4 Kasım): sabit açılar ve olay üstünde kesmeler; kamera hareketine bağlı hışırtılar (gövdeye geçiş, lomboza dönüş,
camdan çıkış, yere süzülüş) kaldırıldı. Vurgular Kenney CC0 örnekleriyle (Docs/AcilisVideosu/Ses/Kenney/, lisans
Docs/ThirdParty-Licenses.md): patlamalar, motor, itici ateşi, gövde ve kaya çarpmaları, uyarı bibi, tık. Rüzgâr, hışırtı,
yükselen uğultu ve yakalanma uğultusu özgün sentez olarak kalır. Örnekler FFmpeg ile çözülür.
"""
import math, random, struct, subprocess, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v5' / 'gecici-ses-v5.wav'
SR, FPS, END = 48000, 24, 390   # v5.8: 354 kare açılış + 36 kare seçim ekranı (son sesler seçim ekranına taşar)
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


KEN = ROOT / 'Docs' / 'AcilisVideosu' / 'Ses' / 'Kenney'
_cache = {}


def sample(rel):
    """Kenney örneği (OGG) → mono 48 kHz, tepe 1'e ölçeklenmiş liste."""
    if rel not in _cache:
        raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(KEN / rel), '-ac', '1', '-ar', str(SR), '-f', 's16le', '-'],
                             capture_output=True, check=True).stdout
        v = [x / 32768 for x in struct.unpack(f'<{len(raw) // 2}h', raw)]
        p = max(abs(x) for x in v) or 1; _cache[rel] = [x / p for x in v]
    return _cache[rel]


def place(rel, t0, gain=1.0, start=0.0, length=None, fade_in=.0, fade_out=.0, lp=None, env=None):
    """Örneği t0'a koyar. start/length: örnekten dilim; lp: alçak geçiren (Hz); env(t): zaman zarfı."""
    s = sample(rel); a = int(start * SR); seg = s[a:a + int(length * SR)] if length else s[a:]
    if lp: seg = lowpass(seg, lp, 1)
    L, i0, fi, fo = len(seg), int(t0 * SR), max(1, int(fade_in * SR)), max(1, int(fade_out * SR))
    for k, v in enumerate(seg):
        i = i0 + k
        if not 0 <= i < N: continue
        e = min(1.0, k / fi if fade_in else 1.0, (L - k) / fo if fade_out else 1.0)
        if env: e *= env(i / SR)
        mix[i] += gain * e * v


SF, IM, UI = 'sci-fi-sounds/', 'impact-sounds/', 'interface-sounds/'

# Ç1 — gürültü, çatırtılar, büyüyen titreme, bir anlık sessizlik (f40–41), büyük patlama, fırlayış
add(0, f(42), lambda t, i: curve(t, [(0, .16), (f(14), .28), (f(28), .42), (f(39), .7), (f(40), .05), (f(42), 0)])
    * (LOW[i] * 1.6 + .35 * math.sin(TAU * 38 * t)) + curve(t, [(f(30), 0), (f(39), .25), (f(40), 0)]) * HIGH[i])
# Çatlama (v5.10): impactMining (kazma) "demir dövme" gibi duyuluyordu (kullanıcı). Artık kaya/kabuk çatırtısı: çatlak her
# büyüme adımında boğuk bir yarılma + alçak gümbürtü, büyürken sık ve kuru çıtırtılar (görüntüdeki Build adımları: 2, 15, 28).
rc = random.Random(5)


def crackle(t0, t1, rate, amp):
    t = t0
    while t < t1:
        a = amp * rc.uniform(.4, 1.0); d = rc.uniform(.004, .012)
        add(t, t + d * 6, lambda tt, i, t=t, a=a, d=d: a * (BAND[i] + .6 * HIGH[i]) * math.exp(-(tt - t) / d))
        t += rc.expovariate(rate)


for t0, t1, a in ((f(2), f(15), .5), (f(15), f(28), .65), (f(28), f(41), .8)):
    place(SF + 'explosionCrunch_000.ogg', t0, a * .55, length=.5, fade_out=.2, lp=1300)   # kabuk yarılır (boğuk)
    add(t0, t0 + .6, lambda t, i, t0=t0, a=a: a * .45 * math.sin(TAU * 38 * (t - t0)) * math.exp(-(t - t0) * 6))
    crackle(t0, t1, 26, a * .7)   # .35'te uğultunun altında kalıyordu (yüksek bant ≈ −45 dB, ölçüldü)
crackle(f(44), f(62), 20, .4)   # geniş çekimde çatlaklar yayılmayı sürdürür
# v5.8: büyük patlama burada değil (geriye bakışta, f114). f42: roket ateş içinde çatlaktan fırlar
place(SF + 'explosionCrunch_000.ogg', f(42), .55, lp=2200)   # çatlak açılır, kabuk yarılır
boom(f(42), .35, 46, 6, .3, 8, .8)
add(f(42), f(96), lambda t, i: curve(t, [(f(42), .3), (f(70), .18), (f(96), .1)]) * LOW[i] * 1.4)
whoosh(f(44), f(60), f(52), .55)
place(SF + 'thrusterFire_000.ogg', f(43), .7, length=1.2, fade_in=.05, fade_out=.35)   # ateş topu (roket) fırlar

# Ç2 — kaçış: roketin motoru (64'te yan çekime kesme), arkadan yetişen şok dalgası, gövdeye çarpma
SHOCK, T3A = 140, 154
place(SF + 'spaceEngineLarge_000.ogg', f(58), .32, length=f(T3A - 58), fade_in=.4, fade_out=.25,   # vurguların altında kalır
      env=lambda t: curve(t, [(f(58), .6), (f(90), 1), (f(SHOCK), 1), (f(T3A), .5)]))
add(f(96), f(SHOCK), lambda t, i: math.exp(-(t - f(96)) * 1.3) * .35 * LOW[i] * 1.5)
EXPLODE = 114   # v5.8: Dünya geriye bakışta patlar
add(f(100), f(EXPLODE), lambda t, i: curve(t, [(f(100), 0), (f(EXPLODE - 1), .35), (f(EXPLODE), 0)]) * LOW[i] * 1.5)   # çatlarken inler
place(SF + 'lowFrequency_explosion_000.ogg', f(EXPLODE), 1.0)   # Dünya patlar: alçak gövde + çatırtı
place(SF + 'explosionCrunch_001.ogg', f(EXPLODE), .7)
boom(f(EXPLODE), .45, 34, 3.2, .3, 5.5, 1.8)


def shimmer(t, i):   # kuzey ışığı gibi enerji halkası: tremololu, yükselen bir sinüs kümesi + parıltılı hışırtı
    u = (t - f(EXPLODE)) / (f(SHOCK) - f(EXPLODE)); env = curve(u, [(0, 0), (.08, .5), (.7, .7), (1, 1)])
    trem = .6 + .4 * math.sin(TAU * (6 + 10 * u) * t)
    tone = sum(a * math.sin(TAU * hz * (1 + .25 * u) * t) for hz, a in ((330, .5), (495, .35), (742, .25), (990, .15)))
    return .16 * env * (trem * tone + .8 * HIGH[i] * (.4 + .6 * u))


add(f(EXPLODE), f(SHOCK), shimmer)
add(f(124), f(SHOCK), lambda t, i: curve(t, [(f(124), 0), (f(SHOCK - 1), .85), (f(SHOCK), 0)]) * (BAND[i] * 1.4 + .2 * math.sin(TAU * (90 + 300 * curve(t, [(f(124), 0), (f(SHOCK), 1)])) * t)))
place(SF + 'explosionCrunch_002.ogg', f(SHOCK), 1.0)   # şok çarpar
place(SF + 'lowFrequency_explosion_001.ogg', f(SHOCK), .75)
place(IM + 'impactMetal_heavy_001.ogg', f(SHOCK), .7)   # gövde sarsılır

# Ç3a — kokpit (kesme): boğuk motor, sarsıntı tıkırtısı, ibre boşa düşer, uyarı lambasının iki bibi
T3C, CHOKE, TICKS = 172, 186, (190, 194, 200)
place(SF + 'spaceEngineLow_000.ogg', f(T3A), .3, length=f(T3C - T3A), lp=700, fade_out=.05)
for k in range(6): click(f(T3A) + k * .035, .2, 250)
place(UI + 'tick_001.ogg', f(158), .3); place(UI + 'tick_001.ogg', f(160), .32)
place(UI + 'error_004.ogg', f(162), .45); place(UI + 'error_004.ogg', f(168), .45)


# Ç3b — dışarıda motor (kesme); boğulma (pof), üç tık (her biri bir öncekinden zayıf), susar (202); sessizlik
def engine_amp(t):
    if t < f(CHOKE): return 1.0
    if t >= f(202): return 0
    return .04


place(SF + 'spaceEngineLarge_000.ogg', f(T3C), .4, start=1.5, length=f(203 - T3C), fade_out=.03, env=engine_amp)
add(f(200), f(240), lambda t, i: .05 * curve(t, [(f(200), 0), (f(206), 1), (f(232), 1), (f(240), 0)]) * LOW[i])   # boşluğun uğultusu (dijital sessizlik kesilme gibi duyulur)
place(SF + 'explosionCrunch_000.ogg', f(CHOKE), .42, length=.45, fade_out=.15, lp=1800)   # boğulma: kısa, boğuk "pof"
for tt, a in zip(TICKS, (.62, .45, .32)):   # tık: itici ateşinden kısa bir dilim + metalik tıkırtı
    place(SF + 'thrusterFire_002.ogg', f(tt), a, start=1.2, length=.09, fade_in=.005, fade_out=.04)
    place(UI + 'click_001.ogg', f(tt), a * .6)

# Ç4 — sessizlik (f202–232), yakalanma uğultusu, atmosfere giriş ateşi (262 sabit yan çekim), bulut bandına dalış
add(f(232), f(316), lambda t, i: curve(t, [(f(232), 0), (f(242), .3), (f(270), .26), (f(300), .12), (f(316), 0)])
    * (math.sin(TAU * 46 * t) + .35 * math.sin(TAU * 92 * t) + .2 * math.sin(TAU * 69 * t + math.sin(TAU * .7 * t))))
add(f(250), f(283), lambda t, i: curve(t, [(f(250), 0), (f(274), .35), (f(283), .1)]) * HIGH[i])
place(SF + 'thrusterFire_000.ogg', f(258), .55, start=.5, length=f(302 - 258), lp=1400, fade_in=.3, fade_out=.4,
      env=lambda t: curve(t, [(f(258), .3), (f(281), 1), (f(290), .6), (f(302), 0)]))   # burnu saran ateş, düşüşte söner
whoosh(f(266), f(288), f(281), .6)
whoosh(f(286), f(300), f(292), .35)   # bulut tabanından çıkış

# Ç5 — rüzgâr ve ıslık; Ç6 — darbe, kayma, taşlar, buhar, ışık
add(f(280), f(316) + .03, lambda t, i: curve(t, [(f(280), 0), (f(288), .35), (f(315), .72), (f(316) + .03, 0)]) * BAND[i] * 1.4)
add(f(296), f(316), lambda t, i: curve(t, [(f(296), 0), (f(315), .1)]) * math.sin(TAU * (700 * (t - f(296)) + 700 * (t - f(296)) ** 2)))
place(SF + 'lowFrequency_explosion_000.ogg', f(316), 1.0)   # çarpma
place(IM + 'impactPlate_heavy_000.ogg', f(316), .8)
place(IM + 'impactMining_003.ogg', f(316), .75)
boom(f(316), .4, 40, 4.5, .3, 8, 1.4)
add(f(317), f(336), lambda t, i: .3 * MID[i] * math.exp(-(t - f(317)) * 3))
for tt, rel, a in ((f(320), 'impactMining_001.ogg', .35), (f(325), 'impactMining_002.ogg', .28), (f(331), 'impactMining_000.ogg', .2)):
    place(IM + rel, tt, a, lp=3000)   # savrulan taşlar düşer
for k in range(6): click(f(318) + k * .09 + rng.uniform(0, .03), .14, 300)

# Son (v5.8, kullanıcı: "sonda astronotun kaskını çıkarma sesi vb."): görüntüde yalnız gemi var, içeride biri olduğunu ses
# anlatır: gövde oturur, lomboz ışığı yanar (342–345 göz kırpar), kemer tokası, kask kilitleri, basınç tıslaması,
# kask çıkar, seçim ekranında kapak açılır.
place(SF + 'impactMetal_000.ogg', f(330), .22, lp=900)   # gövde oturur, metal inler
add(f(330), f(338), lambda t, i: .08 * math.exp(-(t - f(330)) * 3) * math.sin(TAU * (70 - 25 * (t - f(330))) * (t - f(330))))
place(SF + 'computerNoise_000.ogg', f(338), .12, start=.6, length=.35, fade_in=.01, fade_out=.15, lp=4000)   # ışık: elektrik
place(UI + 'confirmation_002.ogg', f(338), .2)
add(f(338), f(354), lambda t, i: .02 * (0 if f(342) <= t < f(345) else 1) * curve(t, [(f(338), 0), (f(339), 1), (f(354), .6)])
    * math.sin(TAU * 100 * t))   # ışığın vızıltısı; göz kırpınca susar
place(UI + 'click_001.ogg', f(346), .3); place(UI + 'click_001.ogg', f(347) + .02, .26)   # kemer tokası: klik klik
place(IM + 'impactMetal_light_000.ogg', f(351), .26, lp=6000); place(IM + 'impactMetal_light_001.ogg', f(353), .24, lp=6000)   # kask kilitleri


def hiss(t, t0=f(354)):   # kaskın basınç tıslaması: hızlı açılır, yavaşça söner
    a = t - t0; return 0 if a < 0 else min(1, a / .02) * math.exp(-a * 2.6)


add(f(354), f(354) + 1.1, lambda t, i: .3 * hiss(t) * (HIGH[i] * .9 + BAND[i] * .35))
place(IM + 'impactSoft_medium_000.ogg', f(368), .3)   # kask çıkar, kucağa konur
place(SF + 'doorOpen_000.ogg', f(376), .32)   # kapak açılır (seçim ekranında)

peak = max(abs(v) for v in mix) or 1
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * .89 / peak)) * 32767)) for v in mix))
print('SES', OUT, f'{DUR} sn')
