"""Hasat Yankısı — kuzey ışığı meteoru, toon görünüm çalışması (23 Ekim 2026).
Özgün geometri; dış model, doku, eklenti yok. Blender 5.x, komut satırı:
  blender -b --factory-startup -P Tools/Art/AuroraMeteor/build_aurora_meteor.py
Çıktı: Docs/AuroraMeteor/ (önizleme render'ları ve .blend).

Kurallar (kullanıcı): mavi yok (mavi elektriğin), yerde halka yok, "AI slop" değil: asimetrik, kırık kenarlı, az ama kalın şekil,
parlama/bloom yok, 2–3 düz ton + mürekkep kontur. Renkler köşe rengindedir: Unity'de mevcut toon/ink malzemeleriyle aynı veri.
Varyant A: yeşil → zümrüt + macenta saçak uçları. Varyant B: yalnız yeşil → zümrüt.
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AuroraMeteor'
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 16
except Exception: pass


def srgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


INK = srgb('#1A1324')
STONE = [srgb('#3A2350'), srgb('#45295E'), srgb('#311C45')]
HEAT, HEAT_CORE = srgb('#8FF5BE'), srgb('#EFFFF4')
CORE, GREEN, EMERALD, MAGENTA = srgb('#EFFFF4'), srgb('#7CF5B0'), srgb('#22B38E'), srgb('#FF63B8')
SPACE = srgb('#141633')

# Unity düşüş yönü (0,45; −1; 0,25) → Blender (x, y=z_u, z=y_u)
FALL = Vector((.45, .25, -1)).normalized()

# ---------------------------------------------------------------- malzemeler
def mat_emit_attr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute')
    at.attribute_name = 'Col'
    m.node_tree.links.new(at.outputs['Color'], em.inputs['Color'])
    m.node_tree.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def mat_toon_attr(name):
    """Işığa göre 3 sert ton × köşe rengi (Unity tarafında toon malzemenin yaptığı iş)."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute')
    at.attribute_name = 'Col'
    dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0, (.42, .42, .42, 1)
    e[1].position, e[1].color = .22, (.74, .74, .74, 1)
    e3 = e.new(.55); e3.color = (1, 1, 1, 1)
    mix = n.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    # Mix düğümünde aynı adlı float/vektör/renk soketleri var: renk soketleri türüne göre seçilir.
    col_in = [i for i in mix.inputs if i.type == 'RGBA']
    fac = [i for i in mix.inputs if i.name == 'Factor'][0]; fac.default_value = 1
    col_out = [o for o in mix.outputs if o.type == 'RGBA'][0]
    L = m.node_tree.links
    L.new(dif.outputs[0], rgb.inputs[0]); L.new(rgb.outputs[0], ramp.inputs[0])
    L.new(ramp.outputs[0], col_in[0]); L.new(at.outputs['Color'], col_in[1])
    L.new(col_out, em.inputs['Color']); L.new(em.outputs[0], out.inputs['Surface'])
    return m


def mat_flat(name, color, toon=True):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission')
    if toon:
        dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.interpolation = 'CONSTANT'
        e = ramp.color_ramp.elements
        e[0].position, e[0].color = 0, (*[x * .55 for x in lin(color)], 1)
        e[1].position, e[1].color = .3, (*[x * .8 for x in lin(color)], 1)
        e3 = e.new(.6); e3.color = (*lin(color), 1)
        L = m.node_tree.links
        L.new(dif.outputs[0], rgb.inputs[0]); L.new(rgb.outputs[0], ramp.inputs[0]); L.new(ramp.outputs[0], em.inputs['Color'])
    else:
        em.inputs['Color'].default_value = (*lin(color), 1)
    m.node_tree.links.new(em.outputs[0], out.inputs['Surface'])
    return m


INK_M = mat_flat('Ink', INK, toon=False)
INK_M.use_backface_culling = True
LIT = mat_toon_attr('Toon lit (vertex colour)')
UNLIT = mat_emit_attr('Unlit (vertex colour)')

# ---------------------------------------------------------------- yardımcılar
def obj_from_bm(name, bm, mat, ink=0.0, collection=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    (collection or s.collection).objects.link(o)
    o.data.materials.append(mat)
    if ink > 0:
        o.data.materials.append(INK_M)
        m = o.modifiers.new('Ink hull', 'SOLIDIFY')
        m.thickness, m.offset, m.use_flip_normals, m.use_rim, m.material_offset = ink, 1.0, True, False, 1
    return o


def paint(o, color_of_face):
    """Yüz başına düz renk (köşe rengi, doğrusal)."""
    me = o.data
    attr = me.color_attributes.new(name='Col', type='FLOAT_COLOR', domain='CORNER')
    for poly in me.polygons:
        c = lin(color_of_face(poly))
        for li in poly.loop_indices: attr.data[li].color = (*c, 1)


# ---------------------------------------------------------------- taş
def rock(name, radius, seed, at, col):
    r = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius)
    for v in bm.verts:
        v.co = v.co.normalized() * radius * (1 + r.uniform(-.13, .17))
    # Kırık kenar: birkaç düzlemle yontulmuş yüzler (asimetrik).
    for _ in range(4):
        d = Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-1, 1))).normalized()
        if d.dot(FALL) > .6: continue   # ön yüz yuvarlak kalsın
        cut = radius * r.uniform(.70, .86)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=d * cut, plane_no=d, clear_outer=True)
        hole = [e for e in bm.edges if e.is_boundary]
        if hole: bmesh.ops.contextual_create(bm, geom=hole)
    # Düşüş yönünde hafif basık, arkaya doğru uzun: hız hissi.
    o = obj_from_bm(name, bm, LIT, collection=col)
    o.location = at
    # Yuvalar: 0 ışıklı taş, 1 ışıksız kızgın yüz, 2 mürekkep kabuk (Solidify malzeme kayması 2).
    o.data.materials.append(UNLIT); o.data.materials.append(INK_M)
    m = o.modifiers.new('Ink hull', 'SOLIDIFY')
    m.thickness, m.offset, m.use_flip_normals, m.use_rim, m.material_offset = .045 * radius / .6, 1.0, True, False, 2

    def face(poly):
        nd = poly.normal.dot(FALL)
        if nd > .3: poly.material_index = 1
        if nd > .7: return HEAT_CORE
        if nd > .3: return HEAT
        return STONE[poly.index % 3 if (poly.index * 7) % 5 else 1]
    paint(o, face)
    cracks(name + ' cracks', o, seed, radius, col)
    return o


def cracks(name, rock_obj, seed, radius, col):
    """Kameraya bakan iki geniş yüzde kısa, düzensiz, yeşil parlayan kırık (yüz düzleminde; kenar takip etmez)."""
    r = random.Random(seed + 5)
    me = rock_obj.data
    vd = view_dir()
    faces = sorted((p for p in me.polygons if p.normal.dot(-vd) > .2 and p.material_index == 0), key=lambda p: -p.area)[:2]
    out = bmesh.new()
    for k, poly in enumerate(faces):
        n = poly.normal; c = poly.center
        d = n.orthogonal().normalized()
        d = (d * math.cos(r.uniform(0, 3.1)) + n.cross(d) * math.sin(r.uniform(0, 3.1))).normalized()
        e = n.cross(d).normalized()
        size = math.sqrt(poly.area) * .9
        pts = [c + d * (size * (i / 3 - .5)) + e * (size * r.uniform(-.18, .18)) + n * .012 for i in range(4)]
        tube(out, pts, [radius * .04, radius * .045, radius * .04, radius * .015], 4, lambda t: GREEN)
    o = obj_from_bm(name, out, UNLIT, collection=col)
    o.parent = rock_obj
    paint_vertex_from_layer(o)
    return o


# Tüp yüzlerine renk: tube() 'face_t' listesi doldurur, paint_vertex_from_layer okur.
TUBE_COLORS = {}


def tube(bm, pts, radii, sides, color_at, seed=0, wobble=0.0):
    r = random.Random(seed)
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        a = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        u = a.orthogonal().normalized(); w = a.cross(u).normalized()
        ring = []
        for j in range(sides):
            ang = 2 * math.pi * j / sides + i * .35
            rr = radii[i] * (1 + wobble * math.sin(j * 1.7 + i * 2.1 + seed))
            ring.append(bm.verts.new(p + (u * math.cos(ang) + w * math.sin(ang)) * rr))
        rings.append(ring)
    for i in range(n - 1):
        c = color_at((i + .5) / (n - 1))
        for j in range(sides):
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % sides], rings[i + 1][(j + 1) % sides], rings[i + 1][j]))
            TUBE_COLORS[(id(bm), len(bm.faces) - 1)] = c
    cap = bm.faces.new(list(reversed(rings[0])))
    TUBE_COLORS[(id(bm), len(bm.faces) - 1)] = color_at(0)
    TUBE_KEYS.append(id(bm))


TUBE_KEYS = []
LAST_BM = {}


def paint_vertex_from_layer(o):
    # Yüz sırası to_mesh'te korunur; renkleri tube() kaydetti (son kullanılan bmesh kimliğiyle).
    key = TUBE_KEYS[-1] if TUBE_KEYS else None
    paint(o, lambda poly: TUBE_COLORS.get((key, poly.index), GREEN))


# ---------------------------------------------------------------- iz (kuzey ışığı alevleri)
# Sabit izometrik oyun kamerasına bakan katmanlı alev dilleri (klasik toon VFX): arkada mürekkep kontur, zümrüt dış alev,
# önünde daha kısa yeşil alev, en önde krem çekirdek. Katmanlar kameraya doğru çok az öne alınır, taş hepsinin önündedir.
CAM_ROT = (55, 0, 45)


def view_dir():
    from mathutils import Euler
    return (Euler(tuple(math.radians(a) for a in CAM_ROT)).to_matrix() @ Vector((0, 0, -1))).normalized()


def tongue(bm, origin, ax, bx, length, width, seed, color_at, lift, wobble=.22, asym=.75, tip_curl=.0):
    """Tek alev dili: ax yönünde uzanır, bx yönünde genişler. Şerit dörtgenleri x boyunca renk bandı alabilir."""
    r = random.Random(seed)
    n = 14
    top, bot = [], []
    for i in range(n + 1):
        t = i / n
        w = width * (1 - t) ** 1.25 * (1 + .45 * math.sin(t * math.pi * .9)) * (1 + wobble * math.sin(t * 9 + seed))
        curl = tip_curl * t * t
        cx = origin + ax * (length * t) + bx * (curl * length)
        top.append(bm.verts.new(cx + bx * w + lift))
        bot.append(bm.verts.new(cx - bx * w * asym * (1 + .2 * math.sin(t * 7 + seed * .5)) + lift))
    for i in range(n):
        f = bm.faces.new((bot[i], bot[i + 1], top[i + 1], top[i]))
        TUBE_COLORS[(id(bm), len(bm.faces) - 1)] = color_at((i + .5) / n)
    TUBE_KEYS.append(id(bm))


def flame(name, head, radius, variant, seed, col, scale=1.0, angle=0.0, edge=0.0):
    vd = view_dir()
    back = -FALL
    ax = (back - vd * back.dot(vd)).normalized()
    bx = vd.cross(ax).normalized()
    if angle:
        ax, bx = (ax * math.cos(angle) + bx * math.sin(angle)).normalized(), (bx * math.cos(angle) - ax * math.sin(angle)).normalized()
    toward = -vd
    L, W = 3.9 * radius * scale, 1.05 * radius * scale
    start = head - ax * radius * .35
    if edge:   # ana alevin kenarından: yana ve biraz geriye
        base_ax = (back - vd * back.dot(vd)).normalized(); base_bx = vd.cross(base_ax).normalized()
        start = head + base_bx * (edge * radius * 1.5) + base_ax * (radius * .9)
    tipA = (lambda c: (lambda t: MAGENTA if t > .8 else c))
    layers = [  # (renk fonksiyonu, uzunluk, genişlik, kameraya doğru öne alma)
        (lambda t: INK, L + .14 * radius, W + .1 * radius, 0.0),
        ((tipA(EMERALD) if variant == 'A' else (lambda t: EMERALD)), L, W, .02),
        ((lambda t: GREEN), L * .66, W * .7, .04),
        ((lambda t: CORE), L * .36, W * .42, .06),
    ]
    objs = []
    for k, (color_at, length, width, fwd) in enumerate(layers):
        bm = bmesh.new()
        tongue(bm, start, ax, bx, length, width, seed if k < 2 else seed + k * 3, color_at, toward * fwd, tip_curl=.12)
        o = obj_from_bm(f'{name} layer {k}', bm, UNLIT, collection=col)
        paint_vertex_from_layer(o)
        objs.append(o)
    return objs


def trail(name, head, radius, variant, seed, col):
    flame(name + ' main', head, radius, variant, seed, col)
    flame(name + ' flick L', head, radius * .5, variant, seed + 11, col, scale=1.0, angle=math.radians(14), edge=.62)
    flame(name + ' flick R', head, radius * .44, variant, seed + 17, col, scale=1.1, angle=math.radians(-12), edge=-.7)


# ---------------------------------------------------------------- çarpma ve krater (yerde halka yok)
DUST = srgb('#CFEADF')
SOIL_A, SOIL_B, SCORCH = srgb('#4A3426'), srgb('#5C4030'), srgb('#2A1838')
DUST_M = None


def screen_axes():
    vd = view_dir()
    up = Vector((0, 0, 1))
    sup = (up - vd * up.dot(vd)).normalized()
    return vd, sup, vd.cross(sup).normalized()


def crown(name, at, radius, seed, col, grow=1.0):
    """Çarpma tacı: kameraya bakan, yukarı ve dışa açılan alev dilleri (macenta uçlu). Halka değil, yelpaze."""
    vd, sup, sr = screen_axes()
    r = random.Random(seed)
    tip = lambda c: (lambda t: MAGENTA if t > .78 else c)
    for k, a in enumerate((-68, -38, -12, 14, 40, 66)):
        ang = math.radians(a + r.uniform(-7, 7))
        ax = (sup * math.cos(ang) + sr * math.sin(ang)).normalized()
        bx = vd.cross(ax).normalized()
        L = radius * r.uniform(1.3, 2.1) * grow * (1.15 if abs(a) < 20 else .9)
        W = radius * r.uniform(.32, .46) * grow
        start = at + ax * radius * .1
        layers = [(lambda t: INK, L + .12 * radius, W + .08 * radius, 0.0),
                  (tip(EMERALD), L, W, .02), (lambda t: GREEN, L * .62, W * .62, .04), (lambda t: CORE, L * .3, W * .36, .06)]
        for j, (color_at, length, width, fwd) in enumerate(layers):
            bm = bmesh.new()
            tongue(bm, start, ax, bx, length, width, seed + k if j < 2 else seed + k + j * 5, color_at, -vd * fwd,
                   wobble=.15, tip_curl=.1 if a > 0 else -.1)
            o = obj_from_bm(f'{name} {k}.{j}', bm, UNLIT, collection=col)
            paint_vertex_from_layer(o)


def chunk(name, at, size, seed, col, colors, rot=(0, 0, 0), stretch=(1, 1, 1)):
    r = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=size)
    for v in bm.verts: v.co = v.co.normalized() * size * (1 + r.uniform(-.22, .22))
    bmesh.ops.scale(bm, vec=stretch, verts=bm.verts)
    o = obj_from_bm(name, bm, LIT, ink=.03 * size / .2, collection=col)
    o.location = at; o.rotation_euler = rot
    paint(o, lambda poly: colors[(poly.index * 5 + seed) % len(colors)])
    return o


def debris(name, at, cell, seed, col, t):
    """Fırlayan taş parçaları: t = 0 çarpma anı, 1 havada en geniş, 2 yere düşmüş."""
    r = random.Random(seed)
    for k in range(6):
        a = k * 2 * math.pi / 6 + r.uniform(-.4, .4)
        d = Vector((math.cos(a), math.sin(a), 0))
        reach = cell * r.uniform(.25, .4) * (1 + 1.6 * min(t, 1.2))
        h = (.35 + r.uniform(0, .3)) if t == 0 else (r.uniform(.7, 1.3) if t == 1 else .26)
        green = [GREEN] if k % 3 == 0 else []
        chunk(f'{name} {k}', at + d * reach + Vector((0, 0, h)), cell * r.uniform(.06, .1) * (1 if t < 2 else .8), seed + k, col,
              STONE + green, rot=(r.uniform(0, 3), r.uniform(0, 3), r.uniform(0, 3)), stretch=(1, 1.5, .8))


DUST_LIGHT, DUST_SHADE = srgb('#EDE3F2'), srgb('#C3B2D3')


def cloud(bm, center, ax, bx, size, seed, color_top, color_bottom, lift):
    """Düz comic bulut: dalgalı kenar (6 kabarcık), alt yarısı gölge tonu. Kameraya bakan düzlemde."""
    r = random.Random(seed)
    n = 36
    c = bm.verts.new(center + lift)
    rim = []
    for i in range(n):
        a = 2 * math.pi * i / n
        bump = 1 + .22 * abs(math.sin(a * 3 + r.uniform(0, .6)))
        rr = size * bump * (1 + r.uniform(-.04, .04))
        rim.append(bm.verts.new(center + (ax * math.sin(a) * .8 + bx * math.cos(a)) * rr + lift))
    for i in range(n):
        f = bm.faces.new((c, rim[i], rim[(i + 1) % n]))
        mid = math.cos(2 * math.pi * (i + .5) / n)
        TUBE_COLORS[(id(bm), len(bm.faces) - 1)] = color_bottom if math.sin(2 * math.pi * (i + .5) / n) < -.15 else color_top
    TUBE_KEYS.append(id(bm))


def dust(name, at, cell, seed, col, t):
    """Toz: düzensiz küme halinde düz bulut kartları (halka değil). t = 0 küçük, 1 büyümüş ve yükselmiş, 2 dağılıyor."""
    vd, sup, sr = screen_axes()
    r = random.Random(seed)
    count = 4 if t < 2 else 2
    for k in range(count):
        a = r.uniform(-1.2, 1.2) + (0 if k % 2 else math.pi)
        side = sr * math.cos(a) + sup * (.25 * math.sin(a))
        size = cell * r.uniform(.11, .16) * (1, 1.45, .9)[t]
        center = at + side * cell * r.uniform(.25, .42) * (1, 1.5, 1.8)[t] + Vector((0, 0, (.3, .55, .95)[t] + r.uniform(0, .15)))
        for j, (scale, fwd, top, bottom) in enumerate([(1.12, 0.0, INK, INK), (1.0, .02, DUST_LIGHT, DUST_SHADE)]):
            bm = bmesh.new()
            cloud(bm, center, sup, sr, size * scale, seed + k, top, bottom, -vd * fwd)
            o = obj_from_bm(f'{name} {k}.{j}', bm, UNLIT, collection=col)
            paint_vertex_from_layer(o)


def crater(name, at, cell, seed, col):
    """Krater: düzensiz yanık leke, merkezden kırık yeşil izler, eğik toprak kenar blokları, birkaç macenta kristal."""
    r = random.Random(seed)
    z = at + Vector((0, 0, .23))
    bm = bmesh.new()
    ring = []
    for i in range(11):
        a = i * 2 * math.pi / 11 + r.uniform(-.15, .15)
        ring.append(bm.verts.new(z + Vector((math.cos(a), math.sin(a), 0)) * cell * .36 * r.uniform(.75, 1.1)))
    bm.faces.new(ring)
    o = obj_from_bm(name + ' scorch', bm, UNLIT, collection=col)
    paint(o, lambda poly: SCORCH)
    out = bmesh.new()
    for k in range(5):
        a = k * 2 * math.pi / 5 + r.uniform(-.3, .3)
        d = Vector((math.cos(a), math.sin(a), 0)); e = Vector((-d.y, d.x, 0))
        bend = e * (cell * r.choice((-.04, .04)))
        length = cell * r.uniform(.22, .3)
        pts = [z + Vector((0, 0, .01)) + d * (cell * .05), z + Vector((0, 0, .01)) + d * (length * .55) + bend, z + Vector((0, 0, .01)) + d * length]
        tube(out, pts, [cell * .016, cell * .014, cell * .003], 4, lambda t: GREEN)
    o = obj_from_bm(name + ' cracks', out, UNLIT, collection=col); paint_vertex_from_layer(o)
    for k in range(9):
        a = k * 2 * math.pi / 9 + r.uniform(-.25, .25)
        d = Vector((math.cos(a), math.sin(a), 0))
        chunk(f'{name} rim {k}', z + d * cell * r.uniform(.33, .46) + Vector((0, 0, .02)), cell * r.uniform(.07, .12), seed + k, col,
              [SOIL_A, SOIL_B, STONE[0]], rot=(d.y * r.uniform(.3, .7), -d.x * r.uniform(.3, .7), a), stretch=(1.4, 1, .7))
    for k in range(3):
        a = r.uniform(0, 2 * math.pi); d = Vector((math.cos(a), math.sin(a), 0))
        bm = bmesh.new()
        h = cell * r.uniform(.12, .2)
        bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=cell * .035, radius2=0, depth=h)
        o = obj_from_bm(f'{name} crystal {k}', bm, UNLIT, ink=.01, collection=col)
        o.location = z + d * cell * r.uniform(.08, .24) + Vector((0, 0, h * .4))
        o.rotation_euler = (d.y * .5, -d.x * .5, 0)
        paint(o, lambda poly: MAGENTA if poly.index % 2 else srgb('#FFB3DB'))


# ---------------------------------------------------------------- sahne: oyun benzeri tarla
def flat_box(name, size, at, mat, col):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    o = obj_from_bm(name, bm, mat, collection=col); o.location = at
    return o


def cone(name, at, h, rr, mat, col, seed):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=rr, radius2=0, depth=h)
    o = obj_from_bm(name, bm, mat, collection=col); o.location = at + Vector((0, 0, h / 2)); o.rotation_euler = (0, 0, seed)
    return o


ground = bpy.data.collections.new('Tarla (bağlam)'); s.collection.children.link(ground)
meteors = bpy.data.collections.new('Meteorlar'); s.collection.children.link(meteors)
RIM, SOIL, LEAF = mat_flat('Saksı kenarı', srgb('#C9CED6')), mat_flat('Toprak', srgb('#3B2A20')), mat_flat('Bitki', srgb('#3E9B3A'))
rr = random.Random(7)
for x in range(-4, 5):
    for y in range(-3, 4):
        c = Vector((x * 2.0, y * 2.0, 0))
        flat_box(f'rim {x},{y}', (1.85, 1.85, .35), c, RIM, ground)
        flat_box(f'soil {x},{y}', (1.3, 1.3, .38), c + Vector((0, 0, .02)), SOIL, ground)
        if rr.random() < .7:
            for k in range(rr.randint(1, 3)):
                cone(f'plant {x},{y},{k}', c + Vector((rr.uniform(-.35, .35), rr.uniform(-.35, .35), .2)), rr.uniform(.5, 1.0), .18, LEAF, ground, rr.uniform(0, 3))

# Tek varyant A (kullanıcı seçimi). Aynı noktada dört an, ayrı koleksiyonlar: render'da sırayla açılır.
HIT = Vector((0, 0, 0))
stage = {}
for key in ('meteor', 'carpma', 'sonrasi', 'krater'):
    stage[key] = bpy.data.collections.new('An: ' + key); meteors.children.link(stage[key])
rk = rock('Meteor A', .6, 31, HIT + (-FALL) * 2.2 + Vector((0, 0, .6)), stage['meteor'])
trail('Meteor A trail', rk.location.copy(), .6, 'A', 31, stage['meteor'])
crown('Taç', HIT + Vector((0, 0, .3)), .6, 5, stage['carpma'], grow=1.0)
debris('Parça', HIT, 2.0, 9, stage['carpma'], 0)
dust('Toz', HIT, 2.0, 13, stage['carpma'], 0)
crater('Krater 1', HIT, 2.0, 21, stage['carpma'])
crown('Taç sonrası', HIT + Vector((0, 0, .3)), .6, 5, stage['sonrasi'], grow=.55)
debris('Parça sonrası', HIT, 2.0, 9, stage['sonrasi'], 1)
dust('Toz sonrası', HIT, 2.0, 13, stage['sonrasi'], 1)
crater('Krater 2', HIT, 2.0, 21, stage['sonrasi'])
debris('Parça yerde', HIT, 2.0, 9, stage['krater'], 2)
dust('Toz dağılıyor', HIT, 2.0, 13, stage['krater'], 2)
crater('Krater 3', HIT, 2.0, 21, stage['krater'])

# Işık ve dünya
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); s.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38), math.radians(-18), math.radians(-40)); sun.data.energy = 3
s.world = bpy.data.worlds.new('Uzay'); s.world.use_nodes = True
bg = s.world.node_tree.nodes['Background']; bg.inputs[0].default_value = (*lin(SPACE), 1); bg.inputs[1].default_value = 1


def camera(name, target, rot_deg, dist, ortho=None, lens=50):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); s.collection.objects.link(cam)
    cam.rotation_euler = tuple(math.radians(a) for a in rot_deg)
    cam.location = Vector(target) + cam.rotation_euler.to_matrix() @ Vector((0, 0, dist))
    if ortho: cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else: cam.data.lens = lens
    return cam


def render(cam, file, w, h, hide_ground=False):
    s.camera = cam
    s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = w, h, 100
    ground.hide_render = hide_ground
    s.render.filepath = str(OUT / file)
    bpy.ops.render.render(write_still=True)


def only(key):
    for k, c in stage.items(): c.hide_render = k != key


iso = camera('Oyun açısı', (0, 0, 1.6), CAM_ROT, 30, ortho=12)
only('meteor'); render(iso, 'AuroraMeteor_Oyun_A.png', 1600, 900)
close = camera('Yakın', HIT + Vector((0, 0, 1.0)), CAM_ROT, 30, ortho=6.0)
close_meteor = camera('Yakın meteor', rk.location + (-FALL) * 1.0, CAM_ROT, 30, ortho=6.0)
for i, key in enumerate(('meteor', 'carpma', 'sonrasi', 'krater')):
    only(key); render(close_meteor if key == 'meteor' else close, f'AuroraMeteor_{i + 1}_{key}.png', 1000, 820)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'AuroraMeteor.blend'))
print('AURORA METEOR DONE')
