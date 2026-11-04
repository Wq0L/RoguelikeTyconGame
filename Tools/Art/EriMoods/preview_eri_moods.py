"""Eri'nin ruh hâli tablolarını önizler (4 Kasım 2026, Faz 3). Kredi harcamaz; yalnız yerel FFmpeg ve numpy.

Çalıştırma (Blender'ın Python'u numpy taşır; ayrı kurulum yok):
  "<Blender>/5.2/python/bin/python.exe" -I Tools/Art/EriMoods/preview_eri_moods.py <ffmpeg> <çıktı klasörü> <ad>=<tablo.png> ...

Çıktılar:
  Kare_Kare_Karsilastirma.png : satır başına bir tablo, sütun başına bir kare (16 kare), aynı ölçekte. İlk satır bugünkü Eri.
  Animasyon_Onizleme.mp4      : GreenhouseMascot'taki gerçek zamanlama ve kare sırası, tablolar yan yana.

Kareler: tablo 4×4 eşit hücreye bölünür, her hücrede saydam olmayan alanın sınırı ölçülür (ayak hizası = sınırın altı).
Bir tablodaki bütün kareler aynı oranla ölçeklenir (oyundaki gibi: MascotFrames.Size × sabit), 8. kare (önden) hedef boya oturur.
"""
import os
import subprocess
import sys

import numpy as np

BG = np.array([42, 42, 58], dtype=np.float32)
FPS = 30
DURATION = 11.8
PANEL_W, PANEL_H, FRONT_H = 480, 420, 300


def load_rgba(ffmpeg, path):
    ffprobe = os.path.join(os.path.dirname(ffmpeg), "ffprobe" + os.path.splitext(ffmpeg)[1])
    probe = subprocess.run([ffprobe, "-v", "error", "-show_entries", "stream=width,height",
                            "-of", "csv=p=0", path], capture_output=True, text=True, check=True).stdout.strip().split(",")
    w, h = int(probe[0]), int(probe[1])
    raw = subprocess.run([ffmpeg, "-v", "error", "-i", path, "-f", "rawvideo", "-pix_fmt", "rgba", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.uint8).reshape(h, w, 4).astype(np.float32) / 255.0


def main_component(mask, center, k=4):
    """Hücre merkezine en yakın dolu noktadan başlayan bağlı bölge (komşu karelerin taşan parçaları ayrılır).
    Hız için k kat küçültülmüş maskede doldurulur, sonra tam çözünürlüğe genişletilip maskeyle kesiştirilir."""
    h, w = mask.shape
    hh, ww = h // k, w // k
    low = mask[:hh * k, :ww * k].reshape(hh, k, ww, k).any(axis=(1, 3))
    ys, xs = np.nonzero(low)
    if len(xs) == 0:
        return np.zeros_like(mask)
    d = (ys - center[0] / k) ** 2 + (xs - center[1] / k) ** 2
    start = (int(ys[d.argmin()]), int(xs[d.argmin()]))
    seen = np.zeros_like(low)
    seen[start] = True
    stack = [start]
    while stack:
        y, x = stack.pop()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < hh and 0 <= nx < ww and low[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True
                stack.append((ny, nx))
    # Bir hücre genişlet (küçültmede kaybolan ince kenarlar), tam çözünürlüğe aç.
    grown = seen.copy()
    grown[1:] |= seen[:-1]; grown[:-1] |= seen[1:]; grown[:, 1:] |= seen[:, :-1]; grown[:, :-1] |= seen[:, 1:]
    full = np.zeros_like(mask)
    full[:hh * k, :ww * k] = np.repeat(np.repeat(grown, k, axis=0), k, axis=1)
    return full & mask


def frames_of(sheet):
    """16 kare: her biri yalnız kendi gövdesiyle (saydam olmayan, merkezden bağlı bölge), sınırına kırpılmış.
    Arama penceresi hücreden %15 taşar: Eri'nin hücre dışına çıkan ayağı da alınır, komşunun parçası alınmaz."""
    h, w, _ = sheet.shape
    cw, ch = w / 4.0, h / 4.0
    out = []
    for i in range(16):
        r, c = divmod(i, 4)
        x0, y0 = max(0, int((c - .15) * cw)), max(0, int((r - .15) * ch))
        x1, y1 = min(w, int((c + 1.15) * cw)), min(h, int((r + 1.15) * ch))
        win = sheet[y0:y1, x0:x1]
        # Başlangıç noktası pencerenin değil hücrenin merkezine en yakın dolu nokta.
        mask = win[:, :, 3] > 0.12
        comp = main_component(mask, (int((r + .5) * ch) - y0, int((c + .5) * cw) - x0))
        ys, xs = np.nonzero(comp)
        if len(xs) == 0:
            out.append(np.zeros((2, 2, 4), np.float32))
            continue
        frame = win.copy()
        frame[:, :, 3] *= comp
        out.append(frame[ys.min():ys.max() + 1, xs.min():xs.max() + 1])
    return out


def resize(img, nw, nh):
    """Bilinear, önceden çarpılmış saydamlıkla (kenarlarda koyu hale olmaz)."""
    nw, nh = max(1, int(round(nw))), max(1, int(round(nh)))
    h, w, _ = img.shape
    pre = img.copy()
    pre[:, :, :3] *= pre[:, :, 3:4]
    # Çok küçültmede önce tam sayı adımla ortalama al (bilinear tek başına titrer).
    step = max(1, int(min(w / nw, h / nh)))
    if step > 1:
        hh, ww = (h // step) * step, (w // step) * step
        pre = pre[:hh, :ww].reshape(hh // step, step, ww // step, step, 4).mean(axis=(1, 3))
        h, w = pre.shape[:2]
    ys = (np.arange(nh) + 0.5) * h / nh - 0.5
    xs = (np.arange(nw) + 0.5) * w / nw - 0.5
    y0 = np.clip(np.floor(ys).astype(int), 0, h - 1); y1 = np.clip(y0 + 1, 0, h - 1); fy = np.clip(ys - y0, 0, 1)[:, None, None]
    x0 = np.clip(np.floor(xs).astype(int), 0, w - 1); x1 = np.clip(x0 + 1, 0, w - 1); fx = np.clip(xs - x0, 0, 1)[None, :, None]
    a = pre[y0][:, x0] * (1 - fx) + pre[y0][:, x1] * fx
    b = pre[y1][:, x0] * (1 - fx) + pre[y1][:, x1] * fx
    out = a * (1 - fy) + b * fy
    alpha = out[:, :, 3:4]
    out[:, :, :3] = np.where(alpha > 1e-4, out[:, :, :3] / np.maximum(alpha, 1e-4), 0)
    return out


def paste(canvas, img, cx, foot_y):
    """Ayak hizası (alt orta) cx, foot_y noktasına."""
    h, w, _ = img.shape
    x0, y0 = int(round(cx - w / 2)), int(round(foot_y - h))
    cx0, cy0 = max(0, x0), max(0, y0)
    cx1, cy1 = min(canvas.shape[1], x0 + w), min(canvas.shape[0], y0 + h)
    if cx1 <= cx0 or cy1 <= cy0:
        return
    src = img[cy0 - y0:cy1 - y0, cx0 - x0:cx1 - x0]
    a = src[:, :, 3:4]
    canvas[cy0:cy1, cx0:cx1] = canvas[cy0:cy1, cx0:cx1] * (1 - a) + src[:, :, :3] * a


def timeline(t):
    """GreenhouseMascot.LateUpdate ile aynı: (kare, ilerleme)."""
    if t < 1.4: return 8, 0.0
    if t < 3.8: return int((t - 1.4) * 6) % 4, (t - 1.4) / 2.4
    if t < 6.8: return (8 if t < 4.1 else 9 if t < 5.1 else 11 if t < 5.35 else 10), 1.0
    if t < 7.4: return 12 + min(3, int((t - 6.8) / .15)), 1.0
    if t < 9.8: return 4 + int((t - 7.4) * 6) % 4, 1 - (t - 7.4) / 2.4
    return 4 + int((t - 9.8) * 6) % 4, 0.0


def main():
    ffmpeg, out_dir = sys.argv[1], sys.argv[2]
    sheets = [arg.split("=", 1) for arg in sys.argv[3:]]
    all_frames, scales = [], []
    for name, path in sheets:
        frames = frames_of(load_rgba(ffmpeg, path))
        all_frames.append(frames)
        scales.append(FRONT_H / frames[8].shape[0])
        print(name, "kare boyları:", [f.shape[0] for f in frames])

    # Kare kare karşılaştırma: hücre 170 px, bütün tablolar aynı ölçek kuralıyla.
    cell = 170
    grid = np.tile(BG / 255.0, (cell * len(sheets), cell * 16, 1)).astype(np.float32)
    for row, frames in enumerate(all_frames):
        s = (cell * .8) / frames[8].shape[0]
        for i, f in enumerate(frames):
            img = resize(f, f.shape[1] * s, f.shape[0] * s)
            paste(grid[row * cell:(row + 1) * cell, i * cell:(i + 1) * cell], img, cell / 2, cell - 8)
    write_png(ffmpeg, grid, out_dir + "/Kare_Kare_Karsilastirma.png")

    # Animasyon: paneller yan yana, oyundaki zamanlamayla.
    width = PANEL_W * len(sheets)
    cmd = [ffmpeg, "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{width}x{PANEL_H}", "-r", str(FPS),
           "-i", "-", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", out_dir + "/Animasyon_Onizleme.mp4"]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(DURATION * FPS)):
        frame, progress = timeline(n / FPS)
        smooth = progress * progress * (3 - 2 * progress)
        canvas = np.tile(BG / 255.0, (PANEL_H, width, 1)).astype(np.float32)
        for p, frames in enumerate(all_frames):
            f = frames[frame]
            s = scales[p] * (0.8 + 0.2 * progress)
            img = resize(f, f.shape[1] * s, f.shape[0] * s)
            cx = p * PANEL_W + 175 + (PANEL_W - 350) * smooth
            paste(canvas[:, p * PANEL_W:(p + 1) * PANEL_W], img, cx - p * PANEL_W, PANEL_H - 30)
        proc.stdin.write((np.clip(canvas, 0, 1) * 255).astype(np.uint8).tobytes())
    proc.stdin.close()
    proc.wait()
    print("ok")


def write_png(ffmpeg, img, path):
    h, w, _ = img.shape
    data = (np.clip(img, 0, 1) * 255).astype(np.uint8).tobytes()
    subprocess.run([ffmpeg, "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{w}x{h}", "-i", "-", path],
                   input=data, check=True)


if __name__ == "__main__":
    main()
