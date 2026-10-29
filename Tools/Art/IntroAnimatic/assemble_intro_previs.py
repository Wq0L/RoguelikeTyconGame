"""Açılış previs — birleştirme (28 Ekim 2026). Kareler + geçici ses + oyuna geçiş.
  python Tools/Art/IntroAnimatic/assemble_intro_previs.py        (v2: 252 kare)
  python Tools/Art/IntroAnimatic/assemble_intro_previs.py v3     (v3: 354 kare; v4 de aynı uzunlukta)
  python Tools/Art/IntroAnimatic/assemble_intro_previs.py v5_1080   (build_intro_v5.py -- all hd çıktısı, 1920×1080)
Önce: build_intro_previs.py / build_intro_v3.py -- all (Blender) ve ilgili ses betiği.
Çıktı (Docs/AcilisVideosu/Animatik/<sürüm>/):
  Acilis-Previs-<sürüm>.mp4          varsayılan (ipucu yok, ön plan yaprakları var)
  Acilis-Previs-<sürüm>-ipuclu.mp4   Ç4'te 2 karelik yeşil parıltı
  Acilis-Previs-<sürüm>-pano.png     saniyede 4 kare
Son 8 kare mürekkep siyahına kararır; ardından 1,5 sn seçim ekranı (kendi ekran görüntümüz) açılır.
Mevcut FFmpeg geliştirme aracıyla; dış içerik yok.
"""
import shutil, subprocess, sys, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VER = sys.argv[1] if len(sys.argv) > 1 else 'v2'
V2 = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / VER
FR, FR_GLINT = V2 / 'kareler', V2 / 'kareler_ipucu'
FPS, END, INK = 24, {'v2': 252, 'v3': 354, 'v4': 354, 'v5': 354, 'v5_1080': 354}[VER], '0x1A1324'
SOUND = V2.parent / {'v2': 'v2/gecici-ses-v2.wav', 'v3': 'v3/gecici-ses-v3.wav', 'v4': 'v4/gecici-ses-v4.wav', 'v5': 'v5/gecici-ses-v5.wav',
                     'v5_1080': 'v5/gecici-ses-v5.wav'}[VER]   # 1080p çıktı v5'in sesini kullanır
SIZE = '1920:1080' if VER.endswith('1080') else '960:540'
TAIL, TAIL_FRAMES, FADE_OUT, FADE_IN = ROOT / 'Docs' / 'DiyalogPlani' / 'Faz2' / 'Faz2_01_Secim.png', 36, 8, 6


def build(seq_dir, name):
    total = (END + TAIL_FRAMES) / FPS
    graph = (f'[0:v]format=yuv420p,fade=t=out:start_frame={END - FADE_OUT}:nb_frames={FADE_OUT}:color={INK}[a];'
             f'[1:v]scale={SIZE},setsar=1,format=yuv420p,trim=end_frame={TAIL_FRAMES},setpts=PTS-STARTPTS,'
             f'fade=t=in:start_frame=0:nb_frames={FADE_IN}:color={INK}[b];[a][b]concat=n=2:v=1:a=0[v];'
             f'[2:a]apad,atrim=end={total:.6f}[s]')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-framerate', str(FPS), '-start_number', '1', '-i', str(seq_dir / '%04d.png'),
                    '-loop', '1', '-framerate', str(FPS), '-t', f'{(TAIL_FRAMES + 1) / FPS:.6f}', '-i', str(TAIL),
                    '-i', str(SOUND), '-filter_complex', graph, '-map', '[v]', '-map', '[s]', '-r', str(FPS),
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'medium', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '160k',
                    '-movflags', '+faststart', str(V2 / name)], check=True)
    print(name, f'{total:.2f} sn')


build(FR, f'Acilis-Previs-{VER}.mp4')
with tempfile.TemporaryDirectory() as tmp:   # ipuçlu çeşit: iki kare değişir, gerisi aynı
    seq = Path(tmp)
    for p in FR.glob('[0-9][0-9][0-9][0-9].png'): shutil.copy2(p, seq / p.name)
    for p in FR_GLINT.glob('*.png'): shutil.copy2(p, seq / p.name)
    build(seq, f'Acilis-Previs-{VER}-ipuclu.mp4')
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(V2 / f'Acilis-Previs-{VER}.mp4'), '-vf', 'fps=4,scale=320:-1,tile=8x6:padding=3',
                '-frames:v', '1', str(V2 / f'Acilis-Previs-{VER}-pano.png')], check=True)
print('PREVIS TAMAM')
