"""Karo "elle boyanmış" doku denemesi (11 Kasım 2026; kullanıcı: "o boyamayı da deneyelim, yerdeki şeyleri çözsek sorun çözülecek").
Yalnız prototip: çıktı izole kopyaya (Library/VerificationProject/Assets/HadesPrototype) gider, oyuna / ana projeye girmez.
AI yok: Blender karonun tepeden haritasını (malzeme, yükseklik) çıkarır; boyama numpy ile kuralla yapılır.

Karonun özgün UV'si (CarteUV) boyamaya uygun değil: dış kenarlar dokunun ince bir şeridine sıkışık (ölçüldü, halka teşhis
dokusuyla). Bu yüzden boyama YENİ bir düzene yapılır; aynı kural HadesPrototypeCapture.PaintedMesh'te (Unity) uygulanır:
  - Üst yüz (ve oyuk içleri): tepeden düz izdüşüm, u = TOP_U0..TOP_U1 (x), v = TOP_V0..TOP_V1 (z). Elle boyamaya da uygun:
    dokunun sol üst karesi karonun tepeden görünüşüdür.
  - Dış yan duvarlar: alt şerit, 4 yüz yan yana (u = (yüz + s) / 4), v = STRIP_V0..STRIP_V1 (yükseklik).
Boyama ekrandaki boyuta göre ölçeklidir (karo menüde ~120 px; dokuda ~13 doku pikseli = 1 ekran pikseli), çizgiler iri.
  blender -b --factory-startup -P Tools/Art/HadesPrototype/paint_floor_tiles.py [-- <çıktı klasörü>]
Çıktı: T_Floor_Boyali_Acik.png (açık karo), T_Floor_Boyali_Kilitli.png (kapalı karo), 2048².
"""
import bpy, sys, os, zlib, struct
import numpy as np
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FBX = os.path.join(ROOT, "Assets", "3D Assets", "Main Assets", "GroundFBX", "SM_Floor_V1.fbx")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(ROOT, "Library", "VerificationProject", "Assets", "HadesPrototype")
DIAG = os.path.join(OUT, "..", "..", "Logs", "HadesPrototype")
os.makedirs(OUT, exist_ok=True); os.makedirs(DIAG, exist_ok=True)

RES = 2048
TOP_U0, TOP_U1, TOP_V0, TOP_V1 = 0.004, 0.746, 0.254, 0.996      # HadesPrototypeCapture ile aynı
STRIP_V0, STRIP_V1 = 0.02, 0.22
N = int(round((TOP_U1 - TOP_U0) * RES))                         # üst yüz tuvali (1520 px = karonun 100 birimi)
U = N / 100.0                                                    # tuval pikseli / karo birimi
# Işığa doğru yön, karo mesh uzayında (Unity; menüde ölçüldü: HadesPrototypeCapture günlüğü)
LIGHT_MESH = np.array([0.321, 0.766, -0.557])
# tuval: sütun ∝ x, satır ∝ −z (v ∝ z, PNG üstten)
Lc = np.array([LIGHT_MESH[0], -LIGHT_MESH[2]]); Lc /= np.linalg.norm(Lc)   # (sütun, satır)

# ---------------------------------------------------------------- tepeden harita (Blender)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
H_MIN, H_MAX = -2.344, 2.344
for slot_index, slot in enumerate(obj.material_slots):
    mat = bpy.data.materials.new(f"map_{slot_index}"); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ'); mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = H_MIN; mr.inputs['From Max'].default_value = H_MAX
    comb = nt.nodes.new('ShaderNodeCombineColor')
    comb.inputs['Red'].default_value = slot_index / 8.0; comb.inputs['Blue'].default_value = 1.0
    nt.links.new(tc.outputs['Object'], sep.inputs[0]); nt.links.new(sep.outputs['Y'], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], comb.inputs['Green']); nt.links.new(comb.outputs[0], em.inputs['Color'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    slot.material = mat
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 1; scene.cycles.use_denoising = False
scene.cycles.filter_width = 0.01
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = N; scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'OPEN_EXR'; scene.render.image_settings.color_depth = '32'
corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
xs = [c.x for c in corners]; ys = [c.y for c in corners]; zs = [c.z for c in corners]
cam_data = bpy.data.cameras.new("top"); cam_data.type = 'ORTHO'
cam_data.ortho_scale = max(max(xs) - min(xs), max(ys) - min(ys)); cam_data.clip_start = 0.001; cam_data.clip_end = 10
cam = bpy.data.objects.new("top", cam_data); scene.collection.objects.link(cam)
cam.location = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, max(zs) + 1); cam.rotation_euler = (0, 0, 0)
scene.camera = cam
exr = os.path.join(DIAG, "_tepeden.exr"); scene.render.filepath = exr
bpy.ops.render.render(write_still=True)
img = bpy.data.images.load(exr)
px = np.empty(N * N * 4, dtype=np.float32); img.pixels.foreach_get(px)
px = px.reshape(N, N, 4)[::-1]
cov = px[..., 3] > 0.5
cid = np.where(cov, np.rint(px[..., 0] * 8).astype(int), 0)
ch = px[..., 1]

# ---------------------------------------------------------------- yardımcılar
rng = np.random.default_rng(1104)   # sabit tohum: her çalıştırma aynı doku

def gauss_blur(a, sigma):
    fy = np.fft.fftfreq(a.shape[0])[:, None]; fx = np.fft.fftfreq(a.shape[1])[None, :]
    return np.real(np.fft.ifft2(np.fft.fft2(a) * np.exp(-2 * (np.pi ** 2) * (sigma ** 2) * (fx ** 2 + fy ** 2))))

def line_blur(a, length, angle_deg, width):
    ker = np.zeros(a.shape)
    t = np.linspace(-length / 2, length / 2, int(length * 2))
    ang = np.radians(angle_deg)
    ker[np.rint(t * np.sin(ang)).astype(int) % a.shape[0], np.rint(t * np.cos(ang)).astype(int) % a.shape[1]] = 1
    ker = gauss_blur(ker, width); ker /= ker.sum()
    return np.real(np.fft.ifft2(np.fft.fft2(a) * np.fft.fft2(ker)))

def norm01(a):
    lo, hi = np.percentile(a, 1), np.percentile(a, 99)
    return np.clip((a - lo) / (hi - lo + 1e-6), 0, 1)

def noise(shape, sigma): return norm01(gauss_blur(rng.standard_normal(shape), sigma))

def shifted(mask, vec, fill=False):
    # shifted[p] = mask[p + vec]  (vec = (sütun, satır), piksel)
    dx, dy = int(round(vec[0])), int(round(vec[1]))
    out = np.full_like(mask, fill)
    H, W = mask.shape
    ys0, ys1 = max(0, -dy), min(H, H - dy); xs0, xs1 = max(0, -dx), min(W, W - dx)
    out[ys0:ys1, xs0:xs1] = mask[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx]
    return out

def edge_toward(mask, direction, width):
    # bölgenin "direction" yönündeki kenar bandı (width piksel)
    return mask & ~shifted(mask, direction * width)

def dilate(mask, r):
    out = mask.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r: out |= shifted(mask, (dx, dy))
    return out

def flood(mask, seed):
    reg = np.zeros_like(mask); reg[seed] = True
    while True:
        g = (reg | shifted(reg, (1, 0)) | shifted(reg, (-1, 0)) | shifted(reg, (0, 1)) | shifted(reg, (0, -1))
             | shifted(reg, (3, 0)) | shifted(reg, (-3, 0)) | shifted(reg, (0, 3)) | shifted(reg, (0, -3))) & mask
        if g.sum() == reg.sum(): return g
        reg = g

def mix(c, color, mask, amount=1.0):
    t = (mask.astype(float) * amount)[..., None]
    return c * (1 - t) + np.array(color) * t

# ---------------------------------------------------------------- üst yüz tuvali
body = cov & (cid == 1); groove = cov & (cid == 2); accent = cov & (cid == 5)
R, C = np.mgrid[0:N, 0:N]
inner = flood(body & ~dilate(groove, 2), (N // 2, N // 2))
outer = body & ~inner
irr, icc = np.nonzero(inner)
# Çeşitler (kullanıcı 11-11: "karoyu boya, ona göre rastgele yerleştirmeyi de deneyelim"): ışık yönü HEP aynı (döndürmek ışıklı
# köşeyi yanlış yere koyardı); değişen: fırça / leke / kırık deseni (tohum) ve gölge bandının yeri (kaydırma).
VARIANTS = [(1104, 0.0), (2207, 0.13), (3310, -0.11)]
ang = np.degrees(np.arctan2(Lc[1], Lc[0]))
groove_lit = edge_toward(groove, -Lc, 0.9 * U)            # oyuğun ışığa bakan duvarı
rivet_hi = accent & shifted(accent, -Lc * 0.5 * U) & ~shifted(accent, Lc * 0.5 * U)
dark_band = edge_toward(outer, -Lc, 2.4 * U) | edge_toward(inner, -Lc, 2.4 * U)
ink = (edge_toward(outer, -Lc, 0.75 * U) | edge_toward(inner, -Lc, 0.75 * U)) | (dilate(groove, int(0.5 * U)) & body)

def make_variant(seed, shift):
    global rng, warp, band, stroke_q, blotch, chip, lit_rim
    rng = np.random.default_rng(seed)
    warp = (noise((N, N), 2.4 * U) - 0.5) * 0.22 + (noise((N, N), 0.35 * U) - 0.5) * 0.05
    # tek ışık alanı (iç plaka ayrı merkezle boyanınca halkalarla kesişip piramit deseni çıkıyordu); iç plaka biraz yükseltilmiş gibi açık
    light = ((C - N / 2) * Lc[0] + (R - N / 2) * Lc[1]) / (N / 2 * 1.15) + warp + shift + np.where(inner, 0.18, 0.0)
    band = np.digitize(light, [-0.45, 0.35])              # 0 gölge, 1 orta, 2 ışık
    stroke = norm01(line_blur(rng.standard_normal((N, N)), 16 * U, ang + 90, 1.1 * U)
                    + 0.5 * line_blur(rng.standard_normal((N, N)), 10 * U, ang + 60, 0.9 * U))
    stroke_q = np.floor(stroke * 3) / 2 - 0.5              # 3 ton: -0.5 / 0 / +0.5
    blotch = noise((N, N), 14 * U)
    chip = noise((N, N), 0.45 * U)
    lit_rim = (edge_toward(outer, Lc, 1.4 * U) | edge_toward(inner, Lc, 1.4 * U)) & (chip > 0.3)

make_variant(*VARIANTS[0])

def paint_top(p):
    c = np.array([p['shadow'], p['mid'], p['lit']])[band]
    c = mix(c, p['groove'], groove); c = mix(c, p['groove_lip'], groove_lit)
    c = mix(c, p['accent'], accent); c = mix(c, p['rim'], rivet_hi)
    c = c * (1 + (np.clip(blotch - 0.55, 0, 1) * 2.2 * 0.6)[..., None] * (np.array(p['warm']) - 1))
    c = c * (1 + (np.clip(0.45 - blotch, 0, 1) * 2.2 * 0.6)[..., None] * (np.array(p['cool']) - 1))
    c = c * (1 + (stroke_q * p['stroke'] * body)[..., None])
    c = mix(c, np.array(p['shadow']) * 0.82, dark_band & body, 0.75)
    c = mix(c, p['rim'], lit_rim & body)
    c = mix(c, INK, ink & ~groove, 0.9)
    return np.clip(np.where(cov[..., None], c, np.array(p['mid'])), 0, 1)

# ---------------------------------------------------------------- yan şerit (4 duvar yan yana; v = yükseklik)
SH = int(round((STRIP_V1 - STRIP_V0) * RES))
sr, sc = np.mgrid[0:SH, 0:RES]
hgt = 1 - sr / (SH - 1)                                    # 1 = üst dudak (PNG satır 0 = şeridin üstü)
s_stroke = np.floor(norm01(line_blur(rng.standard_normal((SH, RES)), 60, 2, 3)) * 3) / 2 - 0.5

def paint_strip(p):
    c = np.zeros((SH, RES, 3)); c[:] = p['side_mid']
    c = mix(c, p['side_lo'], hgt < 0.4)
    c = mix(c, p['rim'], hgt > 0.86)
    c = mix(c, INK, (hgt > 0.8) & (hgt <= 0.86), 0.85)     # dudağın altında mürekkep
    return np.clip(c * (1 + s_stroke[..., None] * p['stroke']), 0, 1)

def assemble(p):
    tex = np.zeros((RES, RES, 3)); tex[:] = p['side_mid']
    top = np.pad(paint_top(p), ((8, 8), (8, 8), (0, 0)), mode='edge')           # mip taşmasına karşı kenar uzatma
    r0 = int(round((1 - TOP_V1) * RES)) - 8; c0 = int(round(TOP_U0 * RES)) - 8
    top = top[max(0, -r0):, max(0, -c0):]; r0, c0 = max(0, r0), max(0, c0)
    tex[r0:r0 + top.shape[0], c0:c0 + top.shape[1]] = top[:RES - r0, :RES - c0]
    strip = np.pad(paint_strip(p), ((6, 6), (0, 0), (0, 0)), mode='edge')
    s0 = int(round((1 - STRIP_V1) * RES)) - 6
    tex[s0:s0 + strip.shape[0]] = strip[:RES - s0]
    return tex

INK = (0.08, 0.05, 0.10)
OPEN = dict(lit=(1.0, 0.99, 0.95), mid=(0.955, 0.945, 0.905), shadow=(0.885, 0.875, 0.945), rim=(1.0, 1.0, 0.97),
            groove=(0.17, 0.15, 0.27), groove_lip=(0.42, 0.39, 0.57), accent=(0.3, 0.28, 0.42),
            side_lo=(0.42, 0.38, 0.56), side_mid=(0.66, 0.62, 0.78), warm=(1.03, 1.0, 0.93), cool=(0.95, 0.96, 1.03), stroke=0.05)
# Kapalı karo oyunda ekranın çoğunu kaplar: geri planda kalmalı (oyun çekimi 11-11: parlak / desenli hâli gözü tarladan çekiyordu).
# Değerler özgün renge (0,22 / 0,29 / 0,38) yakın, kontrast düşük; boyalı dil (bant, kenar, mürekkep) yalnız fısıltı kadar.
LOCKED = dict(lit=(0.25, 0.32, 0.42), mid=(0.22, 0.28, 0.37), shadow=(0.19, 0.22, 0.33), rim=(0.33, 0.42, 0.51),
              groove=(0.11, 0.12, 0.19), groove_lip=(0.17, 0.2, 0.3), accent=(0.12, 0.13, 0.2),
              side_lo=(0.1, 0.1, 0.18), side_mid=(0.15, 0.17, 0.27), warm=(1.04, 1.0, 0.95), cool=(0.95, 0.98, 1.05), stroke=0.06)

def write_png(path, rgb):
    a = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w, _ = a.shape
    raw = b''.join(b'\x00' + a[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
                           + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))

for vi, (seed, shift) in enumerate(VARIANTS):
    make_variant(seed, shift)
    open_tex, locked_tex = assemble(OPEN), assemble(LOCKED)
    write_png(os.path.join(OUT, f"T_Floor_Boyali_Acik_V{vi}.png"), open_tex)
    write_png(os.path.join(OUT, f"T_Floor_Boyali_Kilitli_V{vi}.png"), locked_tex)
    if vi == 0:   # tek doku seçeneği = 1. çeşit
        write_png(os.path.join(OUT, "T_Floor_Boyali_Acik.png"), open_tex)
        write_png(os.path.join(OUT, "T_Floor_Boyali_Kilitli.png"), locked_tex)
idc = np.array([[0, 0, 0], [0.9, 0.9, 0.9], [0.9, 0.2, 0.2], [0, 0, 0], [0, 0, 0], [0.2, 0.4, 1.0]])[np.clip(cid, 0, 5)]
write_png(os.path.join(DIAG, "_teshis_tepeden.png"), np.concatenate([idc, np.repeat(ch[..., None], 3, 2) * cov[..., None]], 1))
print("PAINTED", OUT, "canvas", N, "light(col,row)", Lc.round(3).tolist(), "inner", int(inner.sum()), "outer", int(outer.sum()),
      "groove", int(groove.sum()), "accent", int(accent.sum()), "rim", int(lit_rim.sum()), "ink", int(ink.sum()))
