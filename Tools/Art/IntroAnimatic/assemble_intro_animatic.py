"""Açılış animatiği — birleştirme (28 Ekim 2026). Panelleri kare sayısına göre dizer, geçici sesi ekler.
  python Tools/Art/IntroAnimatic/assemble_intro_animatic.py
Önce: build_intro_animatic.py (Blender) ve make_intro_animatic_sound.py.
Çıktı (Docs/AcilisVideosu/Animatik/):
  Acilis-Animatik-v1.mp4          varsayılan (ipucu yok, ön plan yaprakları var)
  Acilis-Animatik-v1-ipuclu.mp4   Ç4'te 2 karelik yeşil parıltı
  Acilis-Animatik-v1-pano.png     bütün paneller, sırayla
Mevcut FFmpeg geliştirme aracıyla. Sondaki oyun karesi kendi ekran görüntümüz (Docs/DiyalogPlani/Faz2/Faz2_01_Secim.png).
"""
import json, subprocess, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
A = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik'
P = A / 'paneller'
DATA = json.load(open(A / 'paneller.json', encoding='utf-8'))
FPS, INK = DATA['fps'], '0x1A1324'
TAIL, TAIL_FRAMES, FADE_OUT, FADE_IN = ROOT / 'Docs' / 'DiyalogPlani' / 'Faz2' / 'Faz2_01_Secim.png', 36, 8, 6
EXTRA = {e['id']: P / e['file'] for e in DATA['extras']}


def segments(with_glint):
    segs = []
    for p in DATA['panels']:
        n = p['f1'] - p['f0']
        if with_glint and p['id'] == '4b':   # 148–164: 2 kare yay, 2 kare parıltı (150–152), kalan yay
            segs += [(P / p['file'], 2), (EXTRA['4b_ipucu'], 2), (P / p['file'], n - 4)]
        else:
            segs.append((P / p['file'], n))
    return segs


def build(with_glint, name):
    segs = segments(with_glint) + [(TAIL, TAIL_FRAMES)]
    args, chains = ['ffmpeg', '-v', 'error', '-y'], []
    for k, (src, n) in enumerate(segs):
        args += ['-loop', '1', '-framerate', str(FPS), '-t', f'{(n + 1) / FPS:.6f}', '-i', str(src)]
        fx = f'[{k}:v]scale=960:540,setsar=1,format=yuv420p,trim=end_frame={n},setpts=PTS-STARTPTS'
        if k == len(segs) - 2: fx += f',fade=t=out:start_frame={n - FADE_OUT}:nb_frames={FADE_OUT}:color={INK}'
        if k == len(segs) - 1: fx += f',fade=t=in:start_frame=0:nb_frames={FADE_IN}:color={INK}'
        chains.append(fx + f'[v{k}]')
    total = sum(n for _, n in segs) / FPS
    k_audio = len(segs)
    args += ['-i', str(A / 'gecici-ses.wav')]
    graph = ';'.join(chains) + ';' + ''.join(f'[v{k}]' for k in range(len(segs))) + f'concat=n={len(segs)}:v=1:a=0[v]'
    graph += f';[{k_audio}:a]apad,atrim=end={total:.6f}[a]'
    args += ['-filter_complex', graph, '-map', '[v]', '-map', '[a]', '-r', str(FPS),
             '-c:v', 'libx264', '-crf', '18', '-preset', 'medium', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '160k',
             '-movflags', '+faststart', str(A / name)]
    subprocess.run(args, check=True)
    print(name, f'{total:.2f} sn', sum(n for _, n in segs), 'kare')


def board():
    files = [P / p['file'] for p in DATA['panels']] + list(EXTRA.values())
    with tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False, encoding='utf-8') as t:
        for f in files: t.write(f"file '{f.as_posix()}'\nduration 1\n")
    cols = 4; rows = -(-len(files) // cols)
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', t.name,
                    '-vf', f'scale=480:270,tile={cols}x{rows}:padding=6:color={INK}', '-frames:v', '1',
                    str(A / 'Acilis-Animatik-v1-pano.png')], check=True)
    Path(t.name).unlink()


build(False, 'Acilis-Animatik-v1.mp4')
build(True, 'Acilis-Animatik-v1-ipuclu.mp4')
board()
print('ANIMATIK TAMAM')
