"""Oyundaki tuş ikonları: Kenney "Input Prompts" 1.5A (CC0) beyaz tuş kalıplarından comic tuş başlıkları üretir.

Türev: kalıbın silueti krem tuş yüzü olur, harf boşlukları mürdüm mürekkep harf, altına koyu kehribar bir tuş yanı
eklenir ve hepsi kalın mürekkep konturla çevrilir (Docs/UI-Art-Direction.md: kalın mürdüm mürekkep, düz baskı renkleri).
Kaynak kalıplar ve özgün lisans: Tools/Art/KeyPrompts/kenney-input-prompts-1.5a/ (License.txt korunur).

Çıktı: Assets/Resources/UI/KeyPrompts/KeyPromptsAtlas.png + KeyPromptsLayout.json (TMP sprite dikdörtgenleri).
Saf Python (PIL yok). Kullanım: python Tools/Art/KeyPrompts/build_key_prompts.py
"""
import json
import struct
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / 'kenney-input-prompts-1.5a'
ROOT = HERE.parents[2]
OUT = ROOT / 'Assets' / 'Resources' / 'UI' / 'KeyPrompts'
# Doğrudan Kenney kalıbı kullanılanlar; geri kalanlar aşağıda COMPOSED ile harflerden kurulur.
KEYS = {'space': None, 'esc': None, 'tab': None, 'pgup': None, 'pgdn': None, 'enter': 'enter', 'alt': None, 'r': 'r'}
INK, PAPER, SIDE = (54, 39, 54), (255, 242, 210), (214, 150, 74)   # StartSelectionPanelUI Ink/Paper, kehribar tuş yanı
OUTLINE, DEPTH, PAD, GAP = 5, 7, 2, 8   # GAP: mipmap'te komşu tuş sızmasın


def read_png(path):
    d = path.read_bytes(); i = 8; idat = b''; plte = trns = None
    while i < len(d):
        n = struct.unpack('>I', d[i:i + 4])[0]; t = d[i + 4:i + 8]; body = d[i + 8:i + 8 + n]
        if t == b'IHDR': w, h, depth, ctype, _, _, interlace = struct.unpack('>IIBBBBB', body)
        elif t == b'PLTE': plte = body
        elif t == b'tRNS': trns = body
        elif t == b'IDAT': idat += body
        i += 12 + n
    assert interlace == 0 and (depth == 8 or (ctype == 3 and depth in (1, 2, 4))), (path, depth, interlace)
    bpp = {6: 4, 2: 3, 4: 2, 0: 1, 3: 1}[ctype]
    raw = zlib.decompress(idat); stride = w * bpp if depth == 8 else (w * depth + 7) // 8
    prev = bytearray(stride); px = []; p = 0
    for _ in range(h):
        f = raw[p]; line = bytearray(raw[p + 1:p + 1 + stride]); p += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0; b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        for x in range(w):
            if ctype == 6: px.append(line[4 * x + 3])
            elif ctype == 4: px.append(line[2 * x + 1])
            elif ctype == 3:
                if depth == 8: k = line[x]
                else:   # paletli düşük bit derinliği (Kenney ok kalıpları 4 bit)
                    bit = x * depth; k = (line[bit // 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1)
                px.append(trns[k] if trns and k < len(trns) else 255)
            else: px.append(255)
    return w, h, px   # yalnız alfa: kalıp beyaz, rengi biz veriyoruz


def write_png(path, w, h, rgba):
    rows = b''.join(b'\0' + bytes(rgba[y * w * 4:(y + 1) * w * 4]) for y in range(h))
    chunk = lambda t, b: struct.pack('>I', len(b)) + t + b + struct.pack('>I', zlib.crc32(t + b) & 0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b''))


def keycap(alpha, w, h):
    """Döndürür: (gw, gh, rgba) — tuşun kırpılmış comic görüntüsü."""
    W, H = w + 2 * (OUTLINE + PAD), h + 2 * (OUTLINE + PAD) + DEPTH
    ox = oy = OUTLINE + PAD
    A = [0] * (W * H)
    for y in range(h):
        for x in range(w): A[(y + oy) * W + x + ox] = alpha[y * w + x]
    # dış bölge: kenardan, yarı saydamdan az dolu piksellerle ulaşılan yer; içerideki boşluklar harftir
    outside = bytearray(W * H); stack = [i for i in range(W)] + [(H - 1) * W + i for i in range(W)]
    while stack:
        i = stack.pop()
        if outside[i] or A[i] >= 128: continue
        outside[i] = 1; x, y = i % W, i // W
        if x > 0: stack.append(i - 1)
        if x < W - 1: stack.append(i + 1)
        if y > 0: stack.append(i - W)
        if y < H - 1: stack.append(i + W)
    S = [A[i] if outside[i] else 255 for i in range(W * H)]               # tuş yüzü (deliksiz siluet)
    L = [0 if outside[i] else 255 - A[i] for i in range(W * H)]           # harfler
    side = [S[i - DEPTH * W] if i >= DEPTH * W else 0 for i in range(W * H)]
    body = [max(a, b) for a, b in zip(S, side)]
    disk = [(dx, dy) for dy in range(-OUTLINE, OUTLINE + 1) for dx in range(-OUTLINE, OUTLINE + 1) if dx * dx + dy * dy <= OUTLINE * OUTLINE + 1]
    O = [0] * (W * H)
    for y in range(H):
        for x in range(W):
            m = 0
            for dx, dy in disk:
                xx, yy = x + dx, y + dy
                if 0 <= xx < W and 0 <= yy < H:
                    v = body[yy * W + xx]
                    if v > m:
                        m = v
                        if m == 255: break
            O[y * W + x] = m
    rgba = []
    for i in range(W * H):
        col, a = list(INK), O[i] / 255
        for c, k in ((SIDE, side[i]), (PAPER, S[i]), (INK, L[i])):
            t = k / 255
            if t: col = [round(col[j] * (1 - t) + c[j] * t) for j in range(3)]
        rgba += [*col, round(a * 255)]
    return W, H, rgba


def outside_of(A, W, H):
    outside = bytearray(W * H); stack = [i for i in range(W)] + [(H - 1) * W + i for i in range(W)]
    stack += [y * W for y in range(H)] + [y * W + W - 1 for y in range(H)]
    while stack:
        i = stack.pop()
        if outside[i] or A[i] >= 128: continue
        outside[i] = 1; x, y = i % W, i // W
        if x > 0: stack.append(i - 1)
        if x < W - 1: stack.append(i + 1)
        if y > 0: stack.append(i - W)
        if y < H - 1: stack.append(i + W)
    return outside


def glyph_mask(file):
    """Kalıbın içindeki boşluk (harf / ok), kırpılmış: (w, h, maske 0–255)."""
    w, h, A = read_png(SRC / f'keyboard_{file}.png')
    out = outside_of(A, w, h)
    for _ in range(3):   # dış kenarın yumuşatma halkası harf sayılmasın: dışarıyı 3 piksel içeri büyüt
        grown = bytearray(out)
        for i in range(w * h):
            if out[i]: continue
            x, y = i % w, i // w
            if (x > 0 and out[i - 1]) or (x < w - 1 and out[i + 1]) or (y > 0 and out[i - w]) or (y < h - 1 and out[i + w]): grown[i] = 1
        out = grown
    m = [0 if out[i] else 255 - A[i] for i in range(w * h)]
    xs = [x for x in range(w) if any(m[y * w + x] > 8 for y in range(h))]
    ys = [y for y in range(h) if any(m[y * w + x] > 8 for x in range(w))]
    x0, x1, y0, y1 = xs[0], xs[-1] + 1, ys[0], ys[-1] + 1
    return x1 - x0, y1 - y0, [m[y * w + x] for y in range(y0, y1) for x in range(x0, x1)]


def resize(mask, w, h, s):
    """Alan örneklemeli küçültme (4×4 alt örnek)."""
    W, H = max(1, round(w * s)), max(1, round(h * s)); out = []
    for y in range(H):
        for x in range(W):
            acc = 0
            for sy in range(4):
                for sx in range(4):
                    u = min(w - 1, int((x + (sx + .5) / 4) / s)); v = min(h - 1, int((y + (sy + .5) / 4) / s))
                    acc += mask[v * w + u]
            out.append(acc // 16)
    return W, H, out


def composed(files, like='space', letter=.56, gap=3):
    """Tek satırlı tuş: Kenney harf/ok kalıpları (ör. P, G, ↑) geniş tuşun silueti üstünde. Kenney'nin iki satırlı
    "PAGE UP" yazısı oyundaki küçük ipucu boyunda okunmuyordu (19 pt, görüldü)."""
    w, h, A = read_png(SRC / f'keyboard_{like}.png')
    xs = [x for x in range(w) if any(A[y * w + x] >= 128 for y in range(h))]
    ys = [y for y in range(h) if any(A[y * w + x] >= 128 for x in range(w))]
    fw, fh = xs[-1] + 1 - xs[0], ys[-1] + 1 - ys[0]
    glyphs = [glyph_mask(f) for f in files]
    s = letter * fh / glyphs[0][1]
    glyphs = [resize(m, gw, gh, s) for gw, gh, m in glyphs]
    total = sum(g[0] for g in glyphs) + gap * (len(glyphs) - 1)
    fw = max(fw, total + 2 * round(fh * .22))
    W, H = fw + 4, fh + 4; R = fh * .22
    alpha = [0] * (W * H)
    for y in range(H):
        for x in range(W):   # yuvarlak köşeli tuş yüzü, kenarı yumuşatılmış
            px, py = x + .5 - W / 2, y + .5 - H / 2
            qx, qy = max(abs(px) - (fw / 2 - R), 0), max(abs(py) - (fh / 2 - R), 0)
            d = (qx * qx + qy * qy) ** .5 - R
            alpha[y * W + x] = round(255 * min(1, max(0, .5 - d)))
    x = (W - total) // 2
    for gw, gh, m in glyphs:
        y0 = (H - gh) // 2
        for yy in range(gh):
            for xx in range(gw):
                i = (y0 + yy) * W + x + xx
                alpha[i] = round(alpha[i] * (1 - m[yy * gw + xx] / 255))
        x += gw + gap
    return W, H, alpha


# Yazılı tuşlar Kenney'nin harf kalıplarından tek satır ve kalın kurulur: kare ESC ile iki satırlı PAGE UP küçük boyda okunmuyordu,
# SPACE/TAB/ALT'ın ince yazısı da yanlarında sönük kalıyordu (19 pt ipucunda görüldü).
COMPOSED = {'space': ('s', 'p', 'a', 'c', 'e'), 'esc': ('e', 's', 'c'), 'tab': ('t', 'a', 'b'), 'alt': ('a', 'l', 't'),
            'pgup': ('p', 'g', 'arrow_up'), 'pgdn': ('p', 'g', 'arrow_down')}
caps = {}
for name, file in KEYS.items():
    w, h, alpha = composed(COMPOSED[name]) if name in COMPOSED else read_png(SRC / f'keyboard_{file}.png')
    W, H, rgba = keycap(alpha, w, h)
    ys = [y for y in range(H) if any(rgba[(y * W + x) * 4 + 3] for x in range(W))]
    xs = [x for x in range(W) if any(rgba[(y * W + x) * 4 + 3] for y in range(H))]
    x0, x1, y0, y1 = xs[0], xs[-1] + 1, ys[0], ys[-1] + 1
    crop = [rgba[(y * W + x) * 4 + j] for y in range(y0, y1) for x in range(x0, x1) for j in range(4)]
    caps[name] = (x1 - x0, y1 - y0, crop)

AW = sum(c[0] for c in caps.values()) + GAP * (len(caps) + 1)
AH = max(c[1] for c in caps.values()) + 2 * GAP
atlas = [0] * (AW * AH * 4); rects = []; x = GAP
for name, (w, h, crop) in caps.items():
    for yy in range(h):
        row = (GAP + yy) * AW + x
        atlas[row * 4:(row + w) * 4] = crop[yy * w * 4:(yy + 1) * w * 4]
    # TMP glyph dikdörtgeni alttan ölçülür; PNG satırları üstten yazılır
    rects.append({'name': name, 'x': x, 'y': AH - GAP - h, 'w': w, 'h': h})
    x += w + GAP
OUT.mkdir(parents=True, exist_ok=True)
write_png(OUT / 'KeyPromptsAtlas.png', AW, AH, atlas)
(OUT / 'KeyPromptsLayout.json').write_text(json.dumps({'width': AW, 'height': AH, 'keys': rects}, indent=1), encoding='utf-8')
print('KEY PROMPTS', AW, 'x', AH, [r['name'] for r in rects])
