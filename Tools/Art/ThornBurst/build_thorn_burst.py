"""Diken Kuşatması çıkış patlaması — toon VFX çalışması (24 Ekim 2026). Özgün geometri; dış model/doku/eklenti yok.
  blender -b --factory-startup -P Tools/Art/ThornBurst/build_thorn_burst.py
Çıktı: Docs/ThornBurst/frames (yakın), Docs/ThornBurst/game_frames (oyun kamerası mesafesi), ThornBurst.blend.
Kullanıcı: düz "BAM" kartı boş hissettirdi. Burada patlama dünyada olur: hacim (katmanlı toz bulutları), ağırlık (sekerek düşen
toprak kesekleri), vurgu (kısa sıcak alev tacı + hız çizgileri), ardında kalan (yanık leke, kırık toprak, çözülen toz).
24 kare/sn: 1–9 hazırlık (tümsek kabarır, çatlar, kesekler zıplar) · 10 patlama · 10–20 takip · 20–40 sonrası.
Toon: düz 2–3 ton + mürekkep kontur, bloom yok, Random yalnız sabit tohumla (her çalıştırmada aynı).
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Euler, Quaternion

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'ThornBurst'
FRAMES = OUT / 'frames'; FRAMES.mkdir(parents=True, exist_ok=True)
GAME = OUT / 'game_frames'; GAME.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 12
except Exception: pass
FPS, N = 24, 44
s.render.fps = FPS
s.frame_start, s.frame_end = 1, N
BURST = 10   # patlama karesi


def srgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


INK, CREAM = srgb('#1A1324'), srgb('#FFF4D6')
YELLOW, ORANGE, RED = srgb('#FFC83D'), srgb('#F08A2C'), srgb('#E8452C')
SOIL_A, SOIL_B, SOIL_D = srgb('#5C4030'), srgb('#4A3426'), srgb('#3B2A20')
DUST_L, DUST_S = srgb('#EADFCB'), srgb('#B9A288')
SCORCH, LEAF = srgb('#2A1E1A'), srgb('#5DBB4C')
POD, POD_D, SPIKE = srgb('#C8703A'), srgb('#8E4A2A'), srgb('#F4D9A6')
SPACE = srgb('#141633')
CAM_ROT = (55, 0, 45)


def view_dir():
    return (Euler(tuple(math.radians(a) for a in CAM_ROT)).to_matrix() @ Vector((0, 0, -1))).normalized()


VD = view_dir()
SUP = (Vector((0, 0, 1)) - VD * VD.z).normalized()
SR = VD.cross(SUP).normalized()


# ---------------------------------------------------------------- malzemeler (meteor / ağızla aynı toon kurgusu)
def mat_toon_attr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute'); at.attribute_name = 'Col'
    dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0, (.57, .57, .57, 1)
    e[1].position, e[1].color = .22, (.8, .8, .8, 1)
    e3 = e.new(.55); e3.color = (1, 1, 1, 1)
    mix = n.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    cin = [i for i in mix.inputs if i.type == 'RGBA']; [i for i in mix.inputs if i.name == 'Factor'][0].default_value = 1
    cout = [o for o in mix.outputs if o.type == 'RGBA'][0]
    L = m.node_tree.links
    L.new(dif.outputs[0], rgb.inputs[0]); L.new(rgb.outputs[0], ramp.inputs[0])
    L.new(ramp.outputs[0], cin[0]); L.new(at.outputs['Color'], cin[1]); L.new(cout, em.inputs['Color']); L.new(em.outputs[0], out.inputs['Surface'])
    return m


def mat_unlit_attr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute'); at.attribute_name = 'Col'
    m.node_tree.links.new(at.outputs['Color'], em.inputs['Color']); m.node_tree.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def mat_flat(name, color):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission')
    dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0, (*[x * .55 for x in lin(color)], 1)
    e[1].position, e[1].color = .3, (*[x * .8 for x in lin(color)], 1)
    e3 = e.new(.6); e3.color = (*lin(color), 1)
    L = m.node_tree.links
    L.new(dif.outputs[0], rgb.inputs[0]); L.new(rgb.outputs[0], ramp.inputs[0]); L.new(ramp.outputs[0], em.inputs['Color']); L.new(em.outputs[0], out.inputs['Surface'])
    return m


INK_M = bpy.data.materials.new('Ink'); INK_M.use_nodes = True
_n = INK_M.node_tree.nodes; _n.clear()
_o = _n.new('ShaderNodeOutputMaterial'); _e = _n.new('ShaderNodeEmission'); _e.inputs['Color'].default_value = (*lin(INK), 1)
INK_M.node_tree.links.new(_e.outputs[0], _o.inputs['Surface']); INK_M.use_backface_culling = True
LIT, UNLIT = mat_toon_attr('Toon lit'), mat_unlit_attr('Unlit')


def obj_from_bm(name, bm, mat, ink=0.0, parent=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); s.collection.objects.link(o)
    o.data.materials.append(mat)
    if ink > 0:
        o.data.materials.append(INK_M)
        md = o.modifiers.new('Ink hull', 'SOLIDIFY')
        md.thickness, md.offset, md.use_flip_normals, md.use_rim, md.material_offset = ink, 1.0, True, False, 1
    if parent: o.parent = parent
    return o


def paint(o, color_of_face):
    attr = o.data.color_attributes.new(name='Col', type='FLOAT_COLOR', domain='CORNER')
    for poly in o.data.polygons:
        c = lin(color_of_face(poly))
        for li in poly.loop_indices: attr.data[li].color = (*c, 1)


def empty(name, at=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); s.collection.objects.link(e); e.location = at
    return e


def key(o, f, loc=None, scale=None, rot=None):
    if loc is not None: o.location = loc; o.keyframe_insert('location', frame=f)
    if scale is not None:
        o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale; o.keyframe_insert('scale', frame=f)
    if rot is not None: o.rotation_euler = rot; o.keyframe_insert('rotation_euler', frame=f)


HIDE = .001


def smooth(x):
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)


def ease_out(x):
    x = min(1.0, max(0.0, x)); return 1 - (1 - x) ** 3


# ---------------------------------------------------------------- tarla (oyun benzeri saksılar) ve imleç dışı bağlam
RIM_M, SOIL_M, PLANT_M = mat_flat('Saksı', srgb('#C9CED6')), mat_flat('Toprak', srgb('#3B2A20')), mat_flat('Bitki', srgb('#3E9B3A'))


def box(name, size, at, mat):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1); bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    o = obj_from_bm(name, bm, mat); o.location = at; return o


def cone(name, at, h, r, mat, seed):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r, radius2=0, depth=h)
    o = obj_from_bm(name, bm, mat); o.location = at + Vector((0, 0, h / 2)); o.rotation_euler = (0, 0, seed); return o


rr = random.Random(5)
for x in range(-4, 5):
    for y in range(-4, 5):
        c = Vector((x * 2.0, y * 2.0, 0))
        box(f'rim {x},{y}', (1.85, 1.85, .35), c, RIM_M)
        box(f'soil {x},{y}', (1.3, 1.3, .38), c + Vector((0, 0, .02)), SOIL_M)
        if (x, y) != (0, 0):
            for k in range(rr.randint(1, 3)):
                cone(f'plant {x},{y},{k}', c + Vector((rr.uniform(-.35, .35), rr.uniform(-.35, .35), .2)), rr.uniform(.45, .9), .17, PLANT_M, rr.uniform(0, 3))

GROUND = Vector((0, 0, .22))   # kök hücresinin toprak üstü


# ---------------------------------------------------------------- tümsek + çatlaklar (hazırlık)
def mound():
    r = random.Random(7)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=.62)
    for v in bm.verts:
        v.co = v.co.normalized() * .62 * (1 + r.uniform(-.1, .1))
        v.co.z = max(v.co.z, -.05) * .55
    o = obj_from_bm('Tümsek', bm, LIT, ink=.035)
    o.location = GROUND
    paint(o, lambda p: SOIL_A if p.normal.z > .55 else SOIL_B)
    return o


def crack_lines(parent):
    """Tümseğin üstünde açılan mürekkep çatlaklar (yerde düz şerit; tümseğin üst yüzüne yakın)."""
    r = random.Random(11)
    out = []
    for k in range(6):
        a = k * math.pi / 3 + r.uniform(-.25, .25)
        d = Vector((math.cos(a), math.sin(a), 0)); e = Vector((-d.y, d.x, 0))
        bm = bmesh.new()
        pts = [d * .05, d * .24 + e * r.uniform(-.06, .06), d * .48]
        widths = [.045, .035, .006]
        verts = []
        for p, w in zip(pts, widths):
            z = .34 * (1 - (p.length / .62) ** 2) + .02
            verts.append((bm.verts.new(p + e * w + Vector((0, 0, z))), bm.verts.new(p - e * w + Vector((0, 0, z)))))
        for i in range(2):
            bm.faces.new((verts[i][0], verts[i + 1][0], verts[i + 1][1], verts[i][1]))
        o = obj_from_bm(f'Çatlak {k}', bm, UNLIT); paint(o, lambda p: INK)
        o.parent = parent
        out.append(o)
    return out


# ---------------------------------------------------------------- kök (bağlam için basit diken kabuğu, oyundaki kökün yerine)
def pod():
    root = empty('Kök')
    root.location = GROUND
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=.5)
    r = random.Random(3)
    for v in bm.verts:
        v.co = v.co.normalized() * .5 * (1 + r.uniform(-.06, .06))
        v.co.z = v.co.z * 1.7 + .8
        v.co.x *= .9
    body = obj_from_bm('Kök gövde', bm, LIT, ink=.035); body.parent = root
    paint(body, lambda p: POD if p.normal.z > -.2 else POD_D)
    for k in range(6):
        a = k * math.pi / 3 + .3
        d = Vector((math.cos(a), math.sin(a), 0))
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=.09, radius2=0, depth=.32)
        sp = obj_from_bm(f'Kök diken {k}', bm, LIT, ink=.02); sp.parent = root
        sp.location = d * .42 + Vector((0, 0, .7 + .35 * (k % 2)))
        sp.rotation_mode = 'QUATERNION'; sp.rotation_quaternion = Vector((0, 0, 1)).rotation_difference((d + Vector((0, 0, .5))).normalized())
        paint(sp, lambda p: SPIKE)
    return root


# ---------------------------------------------------------------- kart şekilleri (kameraya bakan düzlemde; nesne kökü merkez)
def card(name, outline, layers, at=None):
    """outline(scale) -> 2B nokta listesi; layers: [(ölçek, renk, ileri)] — ilk katman mürekkep (arkada)."""
    root = empty(name, at or (0, 0, 0))
    for j, (scale, col, fwd) in enumerate(layers):
        bm = bmesh.new()
        c = bm.verts.new(-VD * fwd)
        ring = [bm.verts.new(-VD * fwd + SR * x + SUP * y) for (x, y) in outline(scale)]
        for i in range(len(ring)): bm.faces.new((c, ring[i], ring[(i + 1) % len(ring)]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT); paint(o, lambda p, col=col: col)
        o.parent = root
    return root


def cloud_outline(seed, squash=.8):
    r = random.Random(seed)
    ph = [r.uniform(0, 6.28) for _ in range(3)]
    def f(scale):
        pts = []
        for i in range(40):
            a = 2 * math.pi * i / 40
            bump = 1 + .2 * abs(math.sin(a * 3 + ph[0])) + .06 * math.sin(a * 7 + ph[1])
            pts.append((math.cos(a) * scale * bump, math.sin(a) * scale * bump * squash))
        return pts
    return f


def cloud(name, seed, size):
    """Toz bulutu: mürekkep kenar, açık üst / gölgeli alt hilal (iki katman: gölge diski + kaydırılmış açık disk)."""
    root = empty(name)
    outline = cloud_outline(seed)
    for j, (scale, col, fwd, dy) in enumerate([(1.12, INK, 0.0, 0.0), (1.0, DUST_S, .02, 0.0), (.86, DUST_L, .04, .1)]):
        bm = bmesh.new()
        off = -VD * fwd + SUP * (dy * size)
        c = bm.verts.new(off)
        ring = [bm.verts.new(off + SR * x + SUP * y) for (x, y) in outline(size * scale)]
        for i in range(len(ring)): bm.faces.new((c, ring[i], ring[(i + 1) % len(ring)]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT); paint(o, lambda p, col=col: col); o.parent = root
    return root


def tongue_outline(length, width, curl, seed):
    """Alev dili: tabandan uca daralan, hafif kıvrık, uçta sivri (kart düzleminde y = yukarı)."""
    r = random.Random(seed)
    def f(scale):
        pts = []
        L, W = length * scale, width * scale
        for i in range(9):
            t = i / 8
            pts.append((W * (1 - t) ** .7 + curl * L * t * t, L * t))
        tip = (curl * L * 1.05, L * 1.08)
        left = []
        for i in range(8, -1, -1):
            t = i / 8
            left.append((-W * (1 - t) ** .7 * (1 + .15 * math.sin(t * 9 + r.uniform(0, 1))) + curl * L * t * t, L * t))
        return [(0, -W * .2)] + pts + [tip] + left
    return f


def flame(name, angle_deg, length, width, curl, seed, at):
    """Patlama tacının bir dili: mürekkep, kırmızı, turuncu, sarı, krem katmanları; kart düzleminde 'angle' yönünde."""
    root = empty(name, at)
    a = math.radians(angle_deg)
    ax = SUP * math.cos(a) + SR * math.sin(a); bx = VD.cross(ax).normalized()
    layers = [(1.14, INK, 0.0), (1.0, RED, .02), (.78, ORANGE, .04), (.56, YELLOW, .06), (.3, CREAM, .08)]
    for j, (scale, col, fwd) in enumerate(layers):
        bm = bmesh.new()
        pts = tongue_outline(length, width, curl, seed)(scale)
        base = -VD * fwd
        c = bm.verts.new(base + ax * length * scale * .25)
        ring = [bm.verts.new(base + bx * x + ax * y) for (x, y) in pts]
        for i in range(len(ring)): bm.faces.new((c, ring[i], ring[(i + 1) % len(ring)]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT); paint(o, lambda p, col=col: col); o.parent = root
    return root


def blast_outline(scale, turn=0.0):
    """Çizgi roman toprak patlaması: yukarı yönlü, asimetrik sivri uçlar + yuvarlak kabarcıklar (yaprak / yıldız simetrisi yok)."""
    spikes = [(90, .78, .2), (58, .46, .17), (122, .58, .18), (24, .3, .15), (158, .36, .15), (205, .2, .13), (338, .24, .13), (268, .14, .2)]
    pts = []
    for i in range(72):
        a = 2 * math.pi * i / 72
        r = .55 * (1 + .3 * max(0.0, math.sin(a)))
        for deg, length, width in spikes:
            d = abs((a - math.radians(deg + turn) + math.pi) % (2 * math.pi) - math.pi)
            r += length * max(0.0, 1 - d / width)
        r += .07 * abs(math.sin(a * 4.5 + .8))
        pts.append((math.cos(a) * r * scale, math.sin(a) * r * scale))
    return pts


def blast(name, at):
    root = empty(name, at)
    for j, (scale, col, fwd, turn) in enumerate([(1.12, INK, 0.0, 0), (1.0, SOIL_A, .02, 0), (.76, ORANGE, .04, 6), (.54, YELLOW, .06, -5), (.3, CREAM, .08, 9)]):
        bm = bmesh.new()
        c = bm.verts.new(-VD * fwd + SUP * .1 * scale)
        ring = [bm.verts.new(-VD * fwd + SR * x + SUP * y) for (x, y) in blast_outline(scale, turn)]
        for i in range(len(ring)): bm.faces.new((c, ring[i], ring[(i + 1) % len(ring)]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT); paint(o, lambda p, col=col: col); o.parent = root
    return root


def speed_line(name, angle_deg, at):
    a = math.radians(angle_deg)
    d = SUP * math.cos(a) + SR * math.sin(a); side = VD.cross(d).normalized()
    root = empty(name, at)
    bm = bmesh.new()
    p0 = d * .75; p1 = d * 1.35
    vs = [bm.verts.new(p0 - side * .05), bm.verts.new(p0 + side * .05), bm.verts.new(p1)]
    bm.faces.new(vs)
    o = obj_from_bm(name + ' şekil', bm, UNLIT); paint(o, lambda p: INK); o.parent = root
    return root


def clod(name, size, seed, colors):
    r = random.Random(seed)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=size)
    for v in bm.verts: v.co = v.co.normalized() * size * (1 + r.uniform(-.25, .25))
    bmesh.ops.scale(bm, vec=(1.2, 1, .8), verts=bm.verts)
    o = obj_from_bm(name, bm, LIT, ink=.025 * size / .12)
    paint(o, lambda p: colors[(p.index * 5 + seed) % len(colors)])
    return o


def scorch():
    r = random.Random(21)
    bm = bmesh.new()
    ring = [bm.verts.new(Vector((math.cos(a), math.sin(a), 0)) * .62 * r.uniform(.75, 1.08) + Vector((0, 0, .005)))
            for a in [i * 2 * math.pi / 13 + r.uniform(-.12, .12) for i in range(13)]]
    bm.faces.new(ring)
    o = obj_from_bm('Yanık leke', bm, UNLIT); paint(o, lambda p: SCORCH)
    root = empty('Krater'); root.location = GROUND; o.parent = root
    for k in range(6):   # kırık toprak halkası (eğik kesekler)
        a = k * math.pi / 3 + r.uniform(-.2, .2)
        d = Vector((math.cos(a), math.sin(a), 0))
        c = clod(f'Krater kesek {k}', .13, 40 + k, [SOIL_A, SOIL_B, SOIL_D]); c.parent = root
        c.location = d * .58 + Vector((0, 0, .04)); c.rotation_euler = (r.uniform(-.6, .6), r.uniform(-.6, .6), a)
    return root


# ---------------------------------------------------------------- nesneler
MOUND = mound()
CRACKS = crack_lines(MOUND)
POD_ROOT = pod()
CRATER = scorch()
BLAST = blast('Patlama', GROUND + Vector((0, 0, .35)))
LINES = [speed_line(f'Hız çizgisi {k}', a, GROUND + Vector((0, 0, .4))) for k, a in enumerate((-100, -55, 55, 100, 150, -150))]
r0 = random.Random(77)
CLODS = []
for k in range(9):
    a = k * 2 * math.pi / 9 + r0.uniform(-.3, .3)
    cols = [SOIL_A, SOIL_B, SOIL_D] + ([LEAF] if k % 4 == 0 else [])
    CLODS.append((clod(f'Kesek {k}', r0.uniform(.09, .15), k, cols), Vector((math.cos(a), math.sin(a), 0)), r0.uniform(.9, 1.6), r0.uniform(1.2, 2.0), r0.uniform(-4, 4)))
HOPS = [(clod(f'Zıplayan {k}', .07, 20 + k, [SOIL_A, SOIL_B]), Vector((math.cos(k * 2.1), math.sin(k * 2.1), 0)) * .35) for k in range(4)]
r1 = random.Random(33)
CLOUDS = []
for k in range(7):
    a = k * 2 * math.pi / 7 + r1.uniform(-.25, .25)
    side = SR * math.cos(a) + SUP * (.35 * math.sin(a))
    CLOUDS.append((cloud(f'Toz {k}', 90 + k, r1.uniform(.32, .46)), side * r1.uniform(.45, .85), r1.uniform(.25, .65), r1.uniform(0, 2)))
EDGE_PUFFS = [(cloud(f'Kenar pof {k}', 120 + k, .16), Vector((math.cos(k * 2.6 + .4), math.sin(k * 2.6 + .4), 0)) * .62) for k in range(3)]


# ---------------------------------------------------------------- kare kare animasyon
def burst_curve(t):
    """Oyundaki ThornBurst ile aynı: baskı → uzayarak fırlama → basılarak iniş → oturma (t sn)."""
    if t <= 0: return 0, 1, 0
    if t < .10: u = t / .10; return .25 * u, 1, .25 * u
    if t < .24:
        u = (t - .10) / .14; e = 1 - (1 - u) ** 3
        return .25 + 1.05 * e, 1 - .18 * e, .25 + 1.0 * e
    if t < .38:
        u = (t - .24) / .14; sm = u * u * (3 - 2 * u)
        return 1.3 - .52 * sm, .82 + .38 * sm, 1.25 - .25 * sm
    if t < .55:
        u = (t - .38) / .17; sm = u * u * (3 - 2 * u)
        return .78 + .22 * sm + .06 * math.sin(u * 6.283) * (1 - u), 1.2 - .2 * sm, 1
    return 1, 1, 1


for f in range(1, N + 1):
    t = (f - BURST) / FPS   # patlamaya göre zaman (sn); eksi = hazırlık
    pre = max(0.0, min(1.0, (f - 1) / (BURST - 1)))
    # tümsek: kabarır + titrer, patlamada kaybolur
    if f < BURST:
        jitter = Vector((math.sin(f * 2.7), math.cos(f * 3.1), 0)) * .03 * pre
        key(MOUND, f, loc=GROUND + jitter + Vector((0, 0, .03 * pre * abs(math.sin(f * 1.9)))), scale=(1 + .12 * pre, 1 + .12 * pre, 1 + .35 * pre))
    else:
        key(MOUND, f, scale=HIDE)
    for k, c in enumerate(CRACKS):   # çatlaklar sırayla açılır
        open_ = max(0.0, min(1.0, (f - 3 - k * .8) / 3))
        key(c, f, scale=HIDE if f >= BURST or open_ <= 0 else (1, 1, 1) if open_ >= 1 else (open_, open_, 1))
    for k, (o, d) in enumerate(HOPS):   # küçük kesekler zıplar
        phase = (f + k * 2) % 4
        h = .12 * pre * math.sin(phase / 4 * math.pi)
        key(o, f, loc=GROUND + d + Vector((0, 0, .32 + h)), scale=HIDE if f >= BURST or f < 4 else 1)
    for k, (o, d) in enumerate(EDGE_PUFFS):   # kenar pofları
        u = (f - 5 - k) / 4
        key(o, f, loc=GROUND + d + Vector((0, 0, .15 + .2 * max(0, u))), scale=HIDE if f >= BURST or u < 0 or u > 1 else .6 + .5 * math.sin(u * math.pi))
    # kök: oyun eğrisi
    hgt, acr, lift = burst_curve(t)
    key(POD_ROOT, f, loc=GROUND + Vector((0, 0, -.9 * (1 - lift))), scale=HIDE if t <= 0 else (acr, acr, max(HIDE, hgt)))
    # toprak patlaması: 1. kare tam, 2. kare büyür, sonra yukarı kayıp küçülerek toza devreder
    pf = f - BURST
    bscale = {0: 1.0, 1: 1.18, 2: 1.05, 3: .62}.get(pf, 0)
    key(BLAST, f, loc=GROUND + Vector((0, 0, .35 + .12 * max(0, pf))), scale=HIDE if bscale <= 0 else bscale)
    for k, ln in enumerate(LINES):
        u = (f - BURST) / 3
        key(ln, f, scale=HIDE if u < 0 or u > 1 else 1 + .6 * u)
    # kesekler: balistik yay, bir sekme, sonra batar
    for o, d, dist, up, spin in CLODS:
        tt = t
        if tt < 0: key(o, f, loc=GROUND, scale=HIDE); continue
        T1 = .42 * up / 1.6
        if tt < T1:
            u = tt / T1
            pos = GROUND + d * dist * u + Vector((0, 0, .3 + up * 4 * u * (1 - u)))
        elif tt < T1 + .18:
            u = (tt - T1) / .18
            pos = GROUND + d * (dist + .25 * u) + Vector((0, 0, .3 + .35 * 4 * u * (1 - u)))
        else:
            pos = GROUND + d * (dist + .25) + Vector((0, 0, .3))
        sink = 1 - smooth((tt - .7) / .25)
        key(o, f, loc=pos, rot=(tt * spin, tt * spin * .7, tt * spin * .4), scale=max(HIDE, sink))
    # toz bulutları: hızla kabarır, yükselir, yavaşça çözülür
    for k, (o, side, rise, delay) in enumerate(CLOUDS):
        u = t - delay * .03
        if u < 0: key(o, f, loc=GROUND + Vector((0, 0, .3)), scale=HIDE); continue
        grow = ease_out(u / .16)
        fade = 1 - smooth((u - .45 - .05 * k) / .55)
        key(o, f, loc=GROUND + side * (.6 + .5 * grow) + Vector((0, 0, .35 + rise * ease_out(u / .9))), scale=max(HIDE, (.35 + .75 * grow) * fade))
    # krater: patlamada belirir, sonra sönüp batar
    cu = t
    key(CRATER, f, scale=HIDE if cu < 0 else max(HIDE, 1 - smooth((cu - 1.0) / .3)))

for o in bpy.data.objects:
    ad = o.animation_data
    if ad and ad.action:
        for fc in getattr(ad.action, 'fcurves', []):
            for kp in fc.keyframe_points: kp.interpolation = 'CONSTANT'

# ---------------------------------------------------------------- ışık, kamera, render
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); s.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38), math.radians(-18), math.radians(-40)); sun.data.energy = 3
s.world = bpy.data.worlds.new('Uzay'); s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (*lin(SPACE), 1)
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam)
cam.rotation_euler = tuple(math.radians(a) for a in CAM_ROT)
FOCUS = GROUND + Vector((0, 0, .9))
cam.location = FOCUS + cam.rotation_euler.to_matrix() @ Vector((0, 0, 30))
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 6.4; cam.data.clip_end = 100
s.camera = cam
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 600, 100
s.render.image_settings.file_format = 'PNG'
s.render.filepath = str(FRAMES / 'f_')
bpy.ops.render.render(animation=True)
cam.data.ortho_scale = 22 * 1920 / 1080
s.render.resolution_x, s.render.resolution_y = 1920, 1080
s.render.filepath = str(GAME / 'g_')
bpy.ops.render.render(animation=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ThornBurst.blend'))
print('THORN BURST DONE')
