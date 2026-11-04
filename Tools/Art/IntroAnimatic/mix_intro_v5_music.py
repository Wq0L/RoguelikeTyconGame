"""Açılış v5 — efekt + müzik karışımı (4 Kasım 2026).
  python Tools/Art/IntroAnimatic/mix_intro_v5_music.py <müzik dosyası> <başlangıç sn> <çıktı.wav>
Girdi: make_intro_v5_sound.py'nin efekt izi (Animatik/v5/gecici-ses-v5.wav) ve bir müzik dosyası.
Çıktı: 48 kHz stereo, 16,25 sn (14,75 sn açılış + 1,5 sn seçim ekranı).

Efekt izi: 35 Hz altı kesilir, 120 Hz çevresi 4 dB kısılır (uğultu baskındı: alçaklar ortanın 10–20 dB üstündeydi, ölçüldü),
3 kHz çevresi 4 dB açılır (netlik), kısa iki yankı ve sağ kanala 11 ms gecikme ile genişlik verilir (mono ve kuruydu).
Müzik: <başlangıç sn>'den itibaren alınır, efektlerin altında durur; düzeyi kullanıcının çizdiği eğriyi izler (GAIN).
Final karışım: Docs/AcilisVideosu/Animatik/v5_1080/ses-karisim.wav (assemble_intro_previs.py v5_1080 bunu kullanır).
Müzik dosyaları CC0 (OpenGameArt); kayıt Docs/ThirdParty-Licenses.md.
"""
import math, struct, subprocess, sys, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SFX = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v5' / 'gecici-ses-v5.wav'
SR, FPS, TOTAL = 48000, 24, 16.25
MUSIC, START, OUT = Path(sys.argv[1]), float(sys.argv[2]), Path(sys.argv[3])


def f(frame): return frame / FPS


def curve(t, pts):
    if t <= pts[0][0]: return pts[0][1]
    for (t0, v0), (t1, v1) in zip(pts, pts[1:]):
        if t <= t1: return v0 + (v1 - v0) * (t - t0) / max(t1 - t0, 1e-9)
    return pts[-1][1]


# v5.8: kullanıcının çizdiği eğri (4 Kasım): kısık başlar, ~%43'te tepe, yavaşça iner, en sonda hızla kapanır.
# Çizimden okunan noktalar (süre oranı, tepeye göre düzey). Ortadaki çekilme (motor susunca) kaldırıldı.
PEAK = .55
CURVE = [(0, .08), (.052, .2), (.11, .35), (.16, .53), (.237, .72), (.29, .85), (.36, .97), (.43, 1.0), (.51, .96),
         (.61, .85), (.67, .73), (.75, .61), (.79, .53), (.87, .41), (.926, .29), (.97, .19), (.985, .11), (1.0, 0)]
GAIN = [(x * TOTAL, PEAK * y) for x, y in CURVE]

# Müziğin kendi iniş çıkışı önce sıkıştırılır: düzeyi çizilen eğri belirlesin (sıkıştırmasız ölçümde parçanın kendi
# boşlukları eğrinin 4–11 dB altına düşüyordu)
raw = subprocess.run(['ffmpeg', '-v', 'error', '-ss', str(START), '-t', str(TOTAL), '-i', str(MUSIC),
                      '-af', 'acompressor=threshold=0.06:ratio=5:attack=30:release=400:makeup=3,dynaudnorm=f=250:g=15:m=6',
                      '-ac', '2', '-ar', str(SR), '-f', 's16le', '-'], capture_output=True, check=True).stdout
m = struct.unpack(f'<{len(raw) // 2}h', raw)
peak = max(abs(v) for v in m) or 1
n = int(TOTAL * SR)
out = bytearray()
for i in range(n):
    g = curve(i / SR, GAIN) * 32767 / peak * .9
    l = m[2 * i] if 2 * i < len(m) else 0; r = m[2 * i + 1] if 2 * i + 1 < len(m) else 0
    out += struct.pack('<hh', int(l * g), int(r * g))
tmp = OUT.with_suffix('.muzik.wav')
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(tmp), 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(bytes(out))

pre = ('[0:a]apad=whole_dur=16.25,highpass=f=35,equalizer=f=120:t=q:w=1:g=-4,equalizer=f=3000:t=q:w=1.2:g=4,'
       'aecho=0.8:0.75:45|95:0.2|0.1,pan=stereo|c0=c0|c1=c0,adelay=0|11[s];'
       '[s][1:a]amix=inputs=2:normalize=0:duration=longest,atrim=end=16.25')
# Yükseklik −16 LUFS'e SABİT kazançla çekilir, yalnız ani tepeler sınırlanır. loudnorm (tek geçiş ya da linear=true) tepe
# sınırını tutamayınca dinamik kipe düşüyor ve çizilen eğriyi düzleştiriyordu (ölçüldü: normalization_type "dynamic").
meas = subprocess.run(['ffmpeg', '-v', 'info', '-i', str(SFX), '-i', str(tmp), '-filter_complex',
                       pre + ',loudnorm=I=-16:TP=-1.5:LRA=20:print_format=json', '-f', 'null', '-'], capture_output=True, text=True).stderr
import json
gain = -16 - float(json.loads(meas[meas.rfind('{'):meas.rfind('}') + 1])['input_i'])
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(SFX), '-i', str(tmp), '-filter_complex',
                # level=0: alimiter varsayılan olarak çıkışı limit'ten 0 dBFS'e büyütüyordu (patlamada kırpılan örnek, ölçüldü).
                pre + f',volume={gain:.2f}dB,alimiter=limit=0.89:attack=4:release=60:level=0,aresample={SR}[a]', '-map', '[a]',
                '-ar', str(SR), str(OUT)], check=True)
tmp.unlink()
print('KARISIM', OUT)
