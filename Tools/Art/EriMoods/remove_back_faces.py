"""Arka karelerdeki (sırtı dönük Eri) soğanın üstüne yanlışlıkla çizilmiş yüzü siler (4 Kasım 2026, Faz 3). Kredi harcamaz.

Neden: Higgsfield / GPT Image 2.5 üzgün tabloda arka karelere de yüz çizdi; bugünkü Eri'de arka kareler yüzsüz.
Yeniden üretmek bütün tabloyu değiştirirdi. Burada yalnız seçilen karelerde, soğanın krem alanının İÇİNDE kalan
koyu çizgiler (göz, ağız, gözyaşı) bulunur ve çevredeki kremden yumuşakça doldurulur. Başka hiçbir piksele dokunulmaz.

Çalıştırma (Blender'ın Python'u numpy taşır):
  "<Blender>/5.2/python/bin/python.exe" -I Tools/Art/EriMoods/remove_back_faces.py <ffmpeg> <girdi.png> <çıktı.png> <maske_önizleme.png> 4,5,6,7,14,15

Yöntem:
  1. Krem maskesi: sıcak, açık renk (R > 195, G > 170, R - B 22..90), saydam değil.
  2. Soğan: hücrenin üst ortasına en yakın krem noktadan başlayan bağlı krem bölge (robotun krem göbeği ve
     ayak parlamaları ayrı bölgelerdir, alınmaz).
  3. Delikler: soğanın kısa bir kapanışla (genişlet + daralt) toparlanmış alanında, dışarıdan ulaşılamayan krem olmayan
     pikseller. Kenara değen çukurlar dışarıdan ulaşılır, yani dış çizgiye dokunulmaz.
  4. Kenar yumuşatma (anti-alias) halkası için delikler 3 px genişletilir (yalnız soğan alanında kalır).
  5. Doldurma: delik pikselleri komşu ortalamasıyla tekrar tekrar güncellenir (difüzyon); soğanın gölgesi korunur.
"""
import os
import subprocess
import sys

import numpy as np

CELL_CENTER = (0.52, 0.52)   # soğanın kabaca bulunduğu yer (hücre oranı, x ve y): arama başlangıcı
CLOSE_R = 4                  # kapanış yarıçapı (px): göz çizgisi soğan kenarına çok yaklaştığında deliği kapatır
FRINGE = 3                   # deliğin çevresindeki yumuşak kenar halkası (px)
MAX_SPOT = 3000              # tek başına koyu leke için üst alan sınırı (px); göz ~600-1200 px
MIN_PART = 12                # bundan küçük parça yüz sayılmaz. Üzgün tabloda ölçülen (4 Kasım): göz 260-380 px,
                             # ağız 49, gözyaşı 35-66, göz altı parça 15-16; sap dibi / kenar noktası 1-3 px (çentik açıyordu)


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


def erode(m, r=1):
    return ~dilate(~m, r)


def reconstruct(seed, allowed):
    """seed'den başlayıp allowed içinde 4-komşulukla ulaşılabilen her yer."""
    cur = seed & allowed
    while True:
        nxt = dilate(cur) & allowed
        if (nxt == cur).all():
            return cur
        cur = nxt


def fix_cell(cell):
    rgb = cell[:, :, :3].astype(np.int32)
    r, g, b, a = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2], cell[:, :, 3]
    cream = (a > 128) & (r > 195) & (g > 170) & (r - b > 22) & (r - b < 90)
    h, w = cream.shape
    ys, xs = np.nonzero(cream)
    if len(xs) == 0:
        return None, None
    cx, cy = CELL_CENTER[0] * w, CELL_CENTER[1] * h
    k = ((xs - cx) ** 2 + (ys - cy) ** 2).argmin()
    seed = np.zeros_like(cream)
    seed[ys[k], xs[k]] = True
    bulb = reconstruct(seed, cream)

    # Kapanış + delik doldurma: soğanın sınır kutusunda, kutunun kenarından ulaşılamayan her yer "içeride".
    by, bx = np.nonzero(bulb)
    pad = CLOSE_R + FRINGE + 2
    y0, y1 = max(0, by.min() - pad), min(h, by.max() + pad + 1)
    x0, x1 = max(0, bx.min() - pad), min(w, bx.max() + pad + 1)
    sub = bulb[y0:y1, x0:x1]
    closed = erode(dilate(sub, CLOSE_R), CLOSE_R) | sub
    border = np.zeros_like(closed)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    outside = reconstruct(border & ~closed, ~closed)
    inside = ~outside
    # Yalnız kapanıştan sonra da kapalı kalan boşluklar; kapanışın tırtıklı kenara eklediği şerit delik sayılmaz
    # (sayılınca dış çizgiye krem çentik açıyordu).
    holes = inside & ~closed

    # Kenara değen göz: yaprağın soğana düşen gölgesine değdiği için delik sayılmaz. İkinci kural: küçük, tek başına
    # duran (bölge kenarına ve büyük çizgi ağına bağlı olmayan) ve çevresi çoğunlukla soğan rengi olan koyu leke.
    pr, pg, pb = r[y0:y1, x0:x1], g[y0:y1, x0:x1], b[y0:y1, x0:x1]
    pa = a[y0:y1, x0:x1]
    # Krem + soğandaki gölge: sıcak renk, kırmızı yeşilden belirgin yüksek (krem ~238,217,185; gölge pembemsi kahve).
    bulbish = (pa > 128) & (pr > 150) & (pr - pb > 15) & (pr - pg > 6)
    ink = (pa > 128) & (np.maximum(np.maximum(pr, pg), pb) < 110)
    spots = np.zeros_like(ink)
    # İki tarama: koyu mürekkep (göz, ağız) ve soğuk açık pikseller (gözyaşı kalıntısı ~198,199,181; soğanın kenarında
    # gölgeye bitiştiği için çevresinin ancak bir kısmı soğandır, eşik daha gevşek). Kubbe camı ve saksının mavi şeridi
    # de soğuktur ama büyüktür ve bölge kenarına değer, alınmaz. Gözün kenar yumuşatmasını FRINGE genişletmesi kapsar;
    # "soğan rengi olmayan her şey" taraması denendi, dış çizgideki yumuşatma piksellerini de alıp kenara krem çentik
    # açtığı için bırakıldı.
    cool = (pa > 128) & (pg > 150) & (pg >= pr - 3)
    for material, min_bulb in ((ink, .5), (cool, .25)):
        todo = material & dilate(sub, 12)                          # yalnız soğanın yakınında başlayan lekeler
        while todo.any():
            yy, xx = np.argwhere(todo)[0]
            s = np.zeros_like(material); s[yy, xx] = True
            comp = reconstruct(s, material)
            todo &= ~comp
            touches_edge = comp[0, :].any() or comp[-1, :].any() or comp[:, 0].any() or comp[:, -1].any()
            if touches_edge or comp.sum() > MAX_SPOT:
                continue
            ring = dilate(comp, 4) & ~dilate(comp, 1)
            if bulbish[ring].mean() >= min_bulb:
                spots |= comp
    holes |= spots
    # Çok küçük parçalar (sapın dibindeki, kenardaki birkaç piksellik koyuluk) yüz değildir; dokunulmaz.
    todo = holes.copy()
    while todo.any():
        yy, xx = np.argwhere(todo)[0]
        s = np.zeros_like(todo); s[yy, xx] = True
        comp = reconstruct(s, holes)
        todo &= ~comp
        if comp.sum() < MIN_PART:
            holes &= ~comp
            spots &= ~comp
    if not holes.any():
        return None, None
    fix = dilate(holes, FRINGE) & (inside | dilate(spots, FRINGE))

    # Bilinen renk yalnız soğanın kendisi: krem ve üstündeki gölge (dış çizgi pikselleri değil).
    patch = rgb[y0:y1, x0:x1].astype(np.float64)
    known = (sub | bulbish) & ~fix
    usable = (known | fix).astype(np.float64)

    def neighbour_mean(values, weight):
        acc = np.zeros_like(values); cnt = np.zeros(weight.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            acc += np.roll(np.roll(values * weight[:, :, None], dy, 0), dx, 1)
            cnt += np.roll(np.roll(weight, dy, 0), dx, 1)
        return acc, cnt

    # Tohum: bilinen kremden içeri doğru halka halka ortalama (deliğin her pikseli bir renk alana kadar).
    out = patch.copy()
    out[~known] = 0
    filled = known.copy()
    while not filled[fix].all():
        acc, cnt = neighbour_mean(out, filled.astype(np.float64))
        grow = fix & ~filled & (cnt > 0)
        if not grow.any():
            break
        out[grow] = acc[grow] / cnt[grow][:, None]
        filled |= grow
    # Pürüzsüzleştirme (Laplace): yalnız düzeltilen pikseller değişir, komşu olarak yalnız krem ve düzeltilenler sayılır.
    for _ in range(300):
        acc, cnt = neighbour_mean(out, usable)
        upd = fix & (cnt > 0)
        out[upd] = acc[upd] / cnt[upd][:, None]
    result = cell.copy()
    region = result[y0:y1, x0:x1]
    region[:, :, :3][fix] = np.clip(np.round(out[fix]), 0, 255).astype(np.uint8)
    region[:, :, 3][fix] = np.maximum(region[:, :, 3][fix], 253)
    mask = np.zeros((h, w), bool)
    mask[y0:y1, x0:x1] = fix
    return result, mask


def main():
    ffmpeg, src, dst, mask_png = sys.argv[1:5]
    frames = [int(v) for v in sys.argv[5].split(",")]
    img = load_rgba(ffmpeg, src)
    h, w, _ = img.shape
    cw, ch = w // 4, h // 4
    preview = img.copy()
    for i in frames:
        r, c = divmod(i, 4)
        cell = img[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        fixed, mask = fix_cell(cell)
        if fixed is None:
            print(f"kare {i}: delik bulunmadı, dokunulmadı")
            continue
        img[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw] = fixed
        pv = preview[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        pv[mask] = (255, 0, 255, 255)
        print(f"kare {i}: {int(mask.sum())} piksel dolduruldu")
    write_rgba(ffmpeg, img, dst)
    write_rgba(ffmpeg, preview, mask_png)
    print("ok")


if __name__ == "__main__":
    main()
