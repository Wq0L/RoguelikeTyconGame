"""Açılış videosunu oyun için kodlar: Assets/StreamingAssets/Intro/ErigoneIntro.webm.

VP8 + Vorbis (WebM): telifsiz codec'ler; Unity VideoPlayer Windows/Mac/Linux ve WebGL'de (URL ile) oynatır.
Görüntü doğrudan render karelerinden alınır (H.264 önizlemesinden yeniden kodlama kaybı yok).
Son 8 kare mürekkep rengine kararır, ardından 1,5 sn düz mürekkep gelir: önizlemedeki seçim ekranı görüntüsü yerine
oyun burada gerçek seçim ekranını açar, ses kuyruğu (kask, kapak) onun üstünde biter.

Kullanım: python Tools/Art/IntroAnimatic/export_intro_unity.py
"""
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v5_1080'
FRAMES, SOUND = SRC / 'kareler' / '%04d.png', SRC / 'ses-karisim.wav'
OUT = ROOT / 'Assets' / 'StreamingAssets' / 'Intro' / 'ErigoneIntro.webm'
FPS, END, FADE_OUT, TAIL = 24, 354, 8, 36   # assemble_intro_previs.py ile aynı zamanlama
INK = '0x1A1324'

OUT.parent.mkdir(parents=True, exist_ok=True)
tmp = OUT.with_name('yazim-' + OUT.name)
vf = (f'format=yuv420p,fade=t=out:start_frame={END - FADE_OUT}:nb_frames={FADE_OUT}:color={INK},'
      f'tpad=stop_mode=add:stop={TAIL}:color={INK}')
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-framerate', str(FPS), '-i', str(FRAMES), '-i', str(SOUND),
                '-vf', vf, '-c:v', 'libvpx', '-b:v', '8M', '-crf', '6', '-qmin', '0', '-qmax', '40',
                '-deadline', 'good', '-cpu-used', '1', '-auto-alt-ref', '1', '-lag-in-frames', '16', '-g', '48',
                '-c:a', 'libvorbis', '-q:a', '6', '-ar', '48000',
                '-t', f'{(END + TAIL) / FPS:.4f}', str(tmp)], check=True)
tmp.replace(OUT)
print('UNITY VIDEO', OUT, f'{OUT.stat().st_size / 1e6:.1f} MB')
