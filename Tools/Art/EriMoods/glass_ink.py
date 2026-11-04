"""Üzgün Eri'nin camına diğer hâllerdeki siyah mürekkep çizgilerini ekler (4 Kasım 2026, Faz 3). Kredi harcamaz.

Neden: Higgsfield / GPT Image 2.5 üzgün tabloda kubbe camını düz gri boyadı; mutlu ve şeytani tablolarda (ve bugünkü
Eri'de) camın içinde dış çizgiye paralel ince siyah çizgiler var. Kullanıcı: "camında diğerlerinde olan o siyah çizgiler
yok". Ayrıca üzgünün dış çizgisi morumsu (≈20,4,20); diğerlerinde siyah.

Çalıştırma (Blender'ın Python'u numpy taşır):
  "<Blender>/5.2/python/bin/python.exe" -I Tools/Art/EriMoods/glass_ink.py <ffmpeg> <girdi.png> <çıktı.png> gri|mavi

Yapılanlar (her karede ayrı, karenin kendi kubbe hattına göre):
  1. Mor mürekkep siyaha: koyu piksellerde kırmızı ve mavinin yeşilden ortak fazlası çıkarılır (yalnız morumsu
     tonlu ve koyu piksellerde; toprak, saksı, krem, cam etkilenmez).
  2. Cam çizgileri: mutlu tablonun 8. karesinden ölçülen düzen (kubbe tepesinden aşağı piksel, kenardan içeri piksel):
       sağ üst yay     y 28-56,  kenardan 38→27, kalınlık 5
       sağ yan çizgi   y 100-148, kenardan 23,   kalınlık 3,5
       sol yan çizgi 1 y 186-230, kenardan 25,   kalınlık 3,5
       sol yan çizgi 2 y 282-336, kenardan 24,   kalınlık 3
     Uçlar incelir; çizgi yalnız cam piksellerine düşer (yaprak, sap, toprak üstüne taşmaz); kenarlar yumuşatılır.
  3. "mavi" seçilirse cam rengi diğer hâllerin camına kaydırılır (gri ≈201,210,213 → mavi ≈190,227,241).
"""
import os
import subprocess
import sys

import numpy as np

INK = np.array([6.0, 6.0, 8.0])
GLASS_GREY = np.array([201.0, 210.0, 213.0])
GLASS_BLUE = np.array([190.0, 227.0, 241.0])
# (taraf, y başlangıç, y bitiş, kenardan uzaklık başta, sonda, en kalın)
STROKES = [
    ("sag", 28, 56, 38, 27, 5.0),
    ("sag", 100, 148, 23, 23, 3.5),
    ("sol", 186, 230, 25, 25, 3.5),
    ("sol", 282, 336, 24, 24, 3.0),
]


def load_rgba(ffmpeg, path):
    ffprobe = os.path.join(os.path.dirname(ffmpeg), "ffprobe" + os.path.splitext(ffmpeg)[1])
    w, h = map(int, subprocess.run([ffprobe, "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0", path],
                                   capture_output=True, text=True, check=True).stdout.strip().split(","))
    raw = subprocess.run([ffmpeg, "-v", "error", "-i", path, "-f", "rawvideo", "-pix_fmt", "rgba", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(h, w, 4).copy()


def write_rgba(ffmpeg, img, path):
    h, w, _ = img.shape
    subprocess.run([ffmpeg, "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{w}x{h}", "-i", "-",
                    "-pix_fmt", "rgba", path], input=img.tobytes(), check=True)


def dilate(m, r=1):
    for _ in range(r):
        n = m.copy()
        n[1:] |= m[:-1]; n[:-1] |= m[1:]; n[:, 1:] |= m[:, :-1]; n[:, :-1] |= m[:, 1:]
        m = n
    return m


def reconstruct(seed, allowed):
    cur = seed & allowed
    while True:
        nxt = dilate(cur) & allowed
        if (nxt == cur).all():
            return cur
        cur = nxt


def largest_component(mask, k=4):
    """Küçültülmüş maskede en büyük bağlı bölge, tam çözünürlüğe açılıp maskeyle kesiştirilir."""
    h, w = mask.shape
    hh, ww = h // k, w // k
    low = mask[:hh * k, :ww * k].reshape(hh, k, ww, k).mean(axis=(1, 3)) > .5
    best, best_n = None, 0
    todo = low.copy()
    while todo.any():
        y, x = np.argwhere(todo)[0]
        s = np.zeros_like(low); s[y, x] = True
        comp = reconstruct(s, low)
        todo &= ~comp
        if comp.sum() > best_n:
            best, best_n = comp, comp.sum()
    full = np.zeros_like(mask)
    full[:hh * k, :ww * k] = np.repeat(np.repeat(dilate(best, 1), k, axis=0), k, axis=1)
    return full & mask


def unmagenta(rgb, alpha):
    """Koyu, morumsu piksellerden kırmızı ve mavinin yeşilden ortak fazlasını çıkarır."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    tint = np.minimum(r - g, b - g)
    sel = (alpha > 0) & (tint > 4) & (np.maximum(np.maximum(r, g), b) < 150)
    out = rgb.copy()
    out[..., 0] = np.where(sel, r - tint, r)
    out[..., 2] = np.where(sel, b - tint, b)
    return out, int(sel.sum())


def stroke_coverage(shape, pts, widths):
    """Uçları incelen, kenarı yumuşatılmış çizgi örtüsü (0..1): her pikselin çoklu çizgiye uzaklığı ve
    o noktadaki kalınlık."""
    h, w = shape
    cov = np.zeros(shape)
    x0, x1 = int(max(0, pts[:, 0].min() - 8)), int(min(w, pts[:, 0].max() + 9))
    y0, y1 = int(max(0, pts[:, 1].min() - 8)), int(min(h, pts[:, 1].max() + 9))
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float64)
    best_d = np.full(yy.shape, 1e9)
    best_w = np.zeros(yy.shape)
    for i in range(len(pts) - 1):
        p, q = pts[i], pts[i + 1]
        d = q - p
        L2 = max(d @ d, 1e-9)
        t = np.clip(((xx - p[0]) * d[0] + (yy - p[1]) * d[1]) / L2, 0, 1)
        px, py = p[0] + t * d[0], p[1] + t * d[1]
        dist = np.hypot(xx - px, yy - py)
        wid = widths[i] + t * (widths[i + 1] - widths[i])
        closer = dist < best_d
        best_d = np.where(closer, dist, best_d)
        best_w = np.where(closer, wid, best_w)
    cov[y0:y1, x0:x1] = np.clip(best_w / 2 - best_d + .5, 0, 1)
    return cov


def process_cell(cell, mode):
    rgb = cell[..., :3].astype(np.float64)
    alpha = cell[..., 3]
    rgb, n_unmag = unmagenta(rgb, alpha)

    solid = alpha > 128
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    glass = solid & (np.minimum(np.minimum(r, g), b) > 150) & (b >= r - 2)
    dome = largest_component(glass)
    ys, xs = np.nonzero(dome)
    if len(ys) == 0:
        return cell, 0, n_unmag
    # Silüetin tepesi = kubbenin dış çizgisinin tepesi (sap kubbenin içinde kalır).
    top = np.nonzero(solid.any(axis=1))[0].min()
    dome_x0, dome_x1 = xs.min(), xs.max()

    # Satır başına kubbenin dış kenarı: kubbe camına bitişik silüet koşusunun uç pikseli (kollar/bacaklar sayılmaz).
    def edge(y, side):
        row = solid[y]
        gx = np.nonzero(dome[y])[0]
        if len(gx) == 0:
            return None
        x = gx.max() if side == "sag" else gx.min()
        step = 1 if side == "sag" else -1
        while 0 <= x + step < row.size and row[x + step]:
            x += step
        return float(x)

    cover_total = np.zeros(solid.shape)
    for side, ya, yb, off_a, off_b, wmax in STROKES:
        rows = np.arange(top + ya, top + yb + 1, 2)
        ex = [edge(y, side) for y in rows]
        if any(v is None for v in ex):
            continue
        ex = np.array(ex)
        # Kenarı yumuşat (kenar yumuşatma pikselleri ±1 px oynatır).
        if len(ex) >= 5:
            ex = np.convolve(np.pad(ex, 2, mode="edge"), np.ones(5) / 5, mode="valid")
        s = np.linspace(0, 1, len(rows))
        off = off_a + (off_b - off_a) * s
        px = ex - off if side == "sag" else ex + off
        pts = np.stack([px, rows.astype(np.float64)], axis=1)
        widths = wmax * np.sin(np.pi * np.clip(s, 0, 1)) ** .6 + .6
        cover_total = np.maximum(cover_total, stroke_coverage(solid.shape, pts, widths))
    # Çizgi yalnız camın üstüne (yaprak, sap, toprak kesilir); kesim kenarı bir piksel yumuşatılır.
    clip = dome.astype(np.float64)
    clip = (clip + np.roll(clip, 1, 0) + np.roll(clip, -1, 0) + np.roll(clip, 1, 1) + np.roll(clip, -1, 1)) / 5
    cover_total *= clip

    if mode == "mavi":
        # Cam rengini diğer hâllerin camına kaydır; kenar pikselleri kısmen (komşu ortalaması ağırlığıyla).
        wgt = clip
        rgb += (GLASS_BLUE - GLASS_GREY)[None, None, :] * wgt[..., None]
    rgb = rgb * (1 - cover_total[..., None]) + INK[None, None, :] * cover_total[..., None]
    out = cell.copy()
    out[..., :3] = np.clip(np.round(rgb), 0, 255).astype(np.uint8)
    return out, int((cover_total > .05).sum()), n_unmag


def main():
    ffmpeg, src, dst, mode = sys.argv[1:5]
    assert mode in ("gri", "mavi")
    img = load_rgba(ffmpeg, src)
    h, w, _ = img.shape
    ch, cw = h // 4, w // 4
    for i in range(16):
        r, c = divmod(i, 4)
        cell = img[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        out, n_ink, n_unmag = process_cell(cell, mode)
        img[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw] = out
        print(f"kare {i}: çizgi {n_ink} px, mordan siyaha {n_unmag} px")
    write_rgba(ffmpeg, img, dst)
    print("ok")


if __name__ == "__main__":
    main()
