"""Açılış videosu — hareketli previs v2 (28 Ekim 2026). Docs/AcilisVideosu-CekimListesi.md (v2) çekimleri.
  blender -b --factory-startup -t 4 -P Tools/Art/IntroAnimatic/build_intro_previs.py -- all
  blender ... -- 10 40 120        (yalnız bu kareler: hızlı deneme, kareler/deneme_####.png)
Çıktı: Docs/AcilisVideosu/Animatik/v2/kareler/####.png (252 kare, 10,5 sn), kareler_ipucu/ (Ç4 yeşil parıltı), IntroPrevis.blend.
v1 panel animatiğinden farkı: kamera her karede hareketli + sarsıntı, renkli tasarlanmış toon ışık, Dünya gerçek plakalara
ayrılır, vurgular (çatlak, fırlayış, kıl payı, bip, motorun sönmesi, yakalanma, giriş, darbe, ışık) cilayla desteklenir.
Nesneler ikişer karede (anime adımı), kamera her karede. Donma (hit-stop) yok.
Işık: lamba yok; her malzeme normal · anahtar yön / kenar yön ile basamaklanır (renkleri biz seçeriz).
Palet: kullanıcının ERIGONE bannerı (lacivert, kırmızı-turuncu, krem, pas) + oyunun mürekkebi.
Özgün kaba geometri; dış model/doku/eklenti yok. Etiketler yalnız inceleme için, Blender'ın yerleşik yazı tipiyle.
"""
import bpy, bmesh, math, random, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion, Euler, noise as mnoise

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v2'
FR, FR_GLINT = OUT / 'kareler', OUT / 'kareler_ipucu'
for d in (FR, FR_GLINT): d.mkdir(parents=True, exist_ok=True)
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['all']
FPS, END, IMPACT = 24, 252, 212
SHOTS = [('Ç1', 0, 'son gün: yakından yok oluş, bir şey fırlar'), ('Ç2', 48, 'kaçış: o şey roketmiş'),
         ('Ç3a', 100, 'yakıt biter'), ('Ç3b', 118, 'motor söner'), ('Ç4', 136, 'yörünge: rota yakalanır'),
         ('Ç5', 180, 'atmosfer, bulutlar'), ('Ç6', 204, 'yere çakılır, sağ kalır')]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'CONSTANT'   # ikişer kare: aradaki kare tutulur
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 12
except Exception: pass
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 540, 100
s.render.image_settings.file_format = 'PNG'
s.render.fps, s.frame_start, s.frame_end = FPS, 1, END


def srgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


def C(h): return (*lin(srgb(h)), 1.0)


# ---------------------------------------------------------------- palet: banner + oyun mürekkebi
INK, CREAM, CREAM_SH = '#151320', '#F4E6CC', '#CDB592'
SPACE, SPACE_COLD, PANEL_BG = '#1B2130', '#131724', '#1F2338'
OCEAN, OCEAN_SH, LAND, LAND_SH, CLOUD = '#2E86D1', '#1D4C8C', '#6DBF3E', '#3A7B3A', '#F4F1EA'
CRUST, CRUST_SH = '#8A5236', '#5A2E2A'
RED, RED_SH, RED_HI, MAGMA, HOT = '#EF5330', '#A8302A', '#FF8A4C', '#FF5A2A', '#FFD27A'
HULL, HULL_SH, NOSE, NOSE_SH, FIN, FIN_SH = '#F4E6CC', '#B9A6A0', '#E5502E', '#9C2F2E', '#F2A23A', '#B8633A'
NOZZLE, NOZZLE_SH, GLASS, GLASS_SH = '#4A5068', '#2A2C40', '#CFE3EE', '#7F93AE'
FLAME, FLAME_IN, SMOKE, SMOKE_SH = '#FFB43A', '#FFF1A8', '#9A93A6', '#5E5872'
PANEL, PANEL_SH, GLOVE, GLOVE_SH, LAMP_OFF, LAMP_ON = '#3A4060', '#232640', '#E07A3A', '#9A4030', '#5A2428', '#FF4A3A'
RUST, RUST_SH, RUST_HI, RUST_DARK = '#B5623E', '#7A3428', '#D98858', '#8E4430'
SKY5, CLOUD_W, CLOUD_SH = '#35304A', '#F2E3CC', '#A98EA8'
SKY6, GLOW_LOW, GLOW_HIGH, HILL, LEAF_FG = '#1E2436', '#D07A3A', '#9A3E2E', '#3A2630', '#141626'
DUST, DUST_SH, GLINT = '#E6C9A0', '#9A6450', '#9BE58A'

# ---------------------------------------------------------------- sahne ışığı: tek düğüm grubu, kare kare anahtarlanır
LG = bpy.data.node_groups.new('Sahne ışığı', 'ShaderNodeTree')
for name, typ in (('Key', 'NodeSocketVector'), ('Rim', 'NodeSocketVector'), ('RimColor', 'NodeSocketColor'),
                  ('RimAmount', 'NodeSocketFloat'), ('Tint', 'NodeSocketColor'), ('Impact', 'NodeSocketFloat')):
    LG.interface.new_socket(name=name, in_out='OUTPUT', socket_type=typ)
_go = LG.nodes.new('NodeGroupOutput')
L_KEY = LG.nodes.new('ShaderNodeCombineXYZ'); L_RIM = LG.nodes.new('ShaderNodeCombineXYZ')
L_RIMC = LG.nodes.new('ShaderNodeRGB'); L_RIMA = LG.nodes.new('ShaderNodeValue')
L_TINT = LG.nodes.new('ShaderNodeRGB'); L_IMP = LG.nodes.new('ShaderNodeValue')
for src, dst in ((L_KEY, 'Key'), (L_RIM, 'Rim'), (L_RIMC, 'RimColor'), (L_RIMA, 'RimAmount'), (L_TINT, 'Tint'), (L_IMP, 'Impact')):
    LG.links.new(src.outputs[0], _go.inputs[dst])


def node(nt, typ, **kw):
    n = nt.nodes.new(typ)
    for k, v in kw.items(): setattr(n, k, v)
    return n


def put(nt, sock, v):
    if isinstance(v, bpy.types.NodeSocket): nt.links.new(v, sock)
    else: sock.default_value = v


def op(nt, operation, a, b=None):
    n = node(nt, 'ShaderNodeMath', operation=operation); put(nt, n.inputs[0], a)
    if b is not None: put(nt, n.inputs[1], b)
    return n.outputs[0]


def mix(nt, fac, a, b, blend='MIX'):
    n = node(nt, 'ShaderNodeMix', data_type='RGBA', blend_type=blend)
    rgba = [i for i in n.inputs if i.type == 'RGBA']
    put(nt, n.inputs[0], fac); put(nt, rgba[0], a); put(nt, rgba[1], b)
    return [o for o in n.outputs if o.type == 'RGBA'][0]


def dot_to(nt, g_out, geo):
    nrm = node(nt, 'ShaderNodeVectorMath', operation='NORMALIZE'); nt.links.new(g_out, nrm.inputs[0])
    d = node(nt, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); nt.links.new(geo.outputs['Normal'], d.inputs[0]); nt.links.new(nrm.outputs[0], d.inputs[1])
    return d.outputs['Value']


def finish(nt, color, g):
    c = mix(nt, 1.0, color, g.outputs['Tint'], 'MULTIPLY')
    c = mix(nt, g.outputs['Impact'], c, C(INK))
    em = node(nt, 'ShaderNodeEmission'); out = node(nt, 'ShaderNodeOutputMaterial')
    nt.links.new(c, em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])


MATS = {}


def toon(base, shade, hi=None, tex=None, rim=1.0, name=None):
    """Üç basamak (gölge/ışık/parlak) + renkli kenar ışığı. tex: (nt) -> (taban, gölge) soketleri."""
    key = (base, shade, hi, tex, rim)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name or f'Toon {base}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    geo = node(nt, 'ShaderNodeNewGeometry')
    dk = dot_to(nt, g.outputs['Key'], geo); dr = dot_to(nt, g.outputs['Rim'], geo)
    lit = op(nt, 'GREATER_THAN', dk, -.06); shine = op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, .82), lit)
    lw = node(nt, 'ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = .3
    rimf = op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', lw.outputs['Facing'], .56), op(nt, 'GREATER_THAN', dr, .12))
    rimf = op(nt, 'MULTIPLY', op(nt, 'MULTIPLY', rimf, g.outputs['RimAmount']), rim)
    if tex: b, sh = tex(nt)
    else: b, sh = C(base), C(shade)
    h = mix(nt, .32, b, C(CREAM)) if hi is None else C(hi)
    c = mix(nt, lit, sh, b); c = mix(nt, shine, c, h); c = mix(nt, rimf, c, g.outputs['RimColor'])
    finish(nt, c, g); MATS[key] = m; return m


def glow(color, name=None):
    key = ('glow', color)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name or f'Işık {color}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    finish(nt, C(color), g); MATS[key] = m; return m


def plain(color):
    """Etiketler: ışıktan ve darbe karesinden etkilenmez."""
    key = ('plain', color)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(f'Düz {color}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    em = node(nt, 'ShaderNodeEmission'); out = node(nt, 'ShaderNodeOutputMaterial')
    em.inputs['Color'].default_value = C(color); nt.links.new(em.outputs[0], out.inputs['Surface']); MATS[key] = m; return m


INK_M = glow(INK, 'Mürekkep'); INK_M.use_backface_culling = True


def noise_mask(nt, scale, cut, detail=3.0, seed=0.0):
    tc = node(nt, 'ShaderNodeTexCoord'); nz = node(nt, 'ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value, nz.inputs['Detail'].default_value = scale, detail
    if seed:
        add = node(nt, 'ShaderNodeVectorMath', operation='ADD'); add.inputs[1].default_value = (seed, seed * .7, seed * 1.3)
        nt.links.new(tc.outputs['Object'], add.inputs[0]); nt.links.new(add.outputs[0], nz.inputs['Vector'])
    else: nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
    return op(nt, 'GREATER_THAN', nz.outputs['Fac'], cut)


def earth_tex(scale):
    def f(nt):
        land, cloud = noise_mask(nt, scale, .53), noise_mask(nt, scale * 1.9, .64, 2.0, 7.3)
        b = mix(nt, cloud, mix(nt, land, C(OCEAN), C(LAND)), C(CLOUD))
        sh = mix(nt, cloud, mix(nt, land, C(OCEAN_SH), C(LAND_SH)), C('#9DA6C4'))
        return b, sh
    f.__name__ = f'earth{scale}'; return f


def rust_tex(scale):
    def f(nt):
        patch = noise_mask(nt, scale, .56, 4.0, 3.1)
        return mix(nt, patch, C(RUST), C(RUST_DARK)), mix(nt, patch, C(RUST_SH), C('#5E2A24'))
    f.__name__ = f'rust{scale}'; return f


# ---------------------------------------------------------------- geometri yardımcıları
def TRS(at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return Matrix.Translation(at) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))


def sphere(r, m=Matrix(), u=32, v=16, bm=None):
    bm = bm or bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r, matrix=m); return bm


def ico(r, m=Matrix(), sub=1, bm=None):
    bm = bm or bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r, matrix=m); return bm


def cone(r1, r2, depth, m=Matrix(), seg=28, bm=None):
    bm = bm or bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=m); return bm


def box(size, m=Matrix(), bm=None):
    bm = bm or bmesh.new(); bmesh.ops.create_cube(bm, size=1, matrix=m @ Matrix.Diagonal((*size, 1))); return bm


def torus(R, r, m=Matrix(), seg=40, rseg=8, bm=None, a0=0.0, a1=2 * math.pi):
    bm = bm or bmesh.new(); rings = []
    closed = abs(a1 - a0 - 2 * math.pi) < 1e-6
    for i in range(seg if closed else seg + 1):
        a = a0 + (a1 - a0) * i / seg
        rings.append([bm.verts.new(m @ Vector(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
                      for b in (2 * math.pi * j / rseg for j in range(rseg))])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % len(rings)]
        for j in range(rseg): bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    return bm


def between(p0, p1, r0, r1, seg=12, bm=None):
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0
    m = Matrix.Translation((p0 + p1) / 2) @ d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return cone(r0, r1, d.length, m, seg, bm)


def star(r_out, r_in, n=8, m=Matrix(), bm=None):
    """Düz yıldız/patlama şekli (XY düzleminde)."""
    bm = bm or bmesh.new(); c = bm.verts.new(m @ Vector((0, 0, 0))); ring = []
    for i in range(2 * n):
        a = math.pi * i / n; r = r_out if i % 2 == 0 else r_in
        ring.append(bm.verts.new(m @ Vector((math.cos(a) * r, math.sin(a) * r, 0))))
    for i in range(2 * n): bm.faces.new((c, ring[i], ring[(i + 1) % (2 * n)]))
    return bm


def bezier(p0, direction, p1, n=30):
    p0, p1 = Vector(p0), Vector(p1); c = p0 + Vector(direction).normalized() * (p1 - p0).length * .55
    return [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * c + t * t * p1 for t in (i / n for i in range(n + 1))]


def resample(pts, step):
    pts = [Vector(p) for p in pts]; d = [0.0]
    for a, b in zip(pts, pts[1:]): d.append(d[-1] + (b - a).length)
    out, k, j = [], 0, 0
    while k * step <= d[-1]:
        t = k * step
        while d[j + 1] < t: j += 1
        out.append(pts[j].lerp(pts[j + 1], (t - d[j]) / max(d[j + 1] - d[j], 1e-9))); k += 1
    return out


TOGGLE = []


def obj(name, bm, mats, ink=0.0, parent=None, smooth=True, at=None, toggle=False):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); s.collection.objects.link(o)
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    for m in mats: o.data.materials.append(m)
    if smooth: me.shade_smooth()
    if ink > 0:
        for _ in mats: o.data.materials.append(INK_M)
        md = o.modifiers.new('Mürekkep kabuk', 'SOLIDIFY')
        md.thickness, md.offset, md.use_flip_normals, md.use_rim, md.material_offset = ink, 1.0, True, False, len(mats)
    if parent: o.parent = parent
    if at is not None: o.location = at
    if toggle: TOGGLE.append(o)
    return o


def empty(name, parent=None, at=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); s.collection.objects.link(e); e.location = at
    if parent: e.parent = parent
    return e


ROT_Z_TO_X = Matrix.Rotation(math.pi / 2, 4, 'Y')
RNG = random.Random(28)


def clamp(x, a=0.0, b=1.0): return max(a, min(b, x))
def ease(x): x = clamp(x); return x * x * (3 - 2 * x)
def ease_in(x): x = clamp(x); return x * x
def ease_out(x): x = clamp(x); return 1 - (1 - x) ** 2


# ---------------------------------------------------------------- kamera + HUD (kadraj x∈[-1,1], y∈[-.5625,.5625])
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam); s.camera = cam
cam.data.clip_start, cam.data.clip_end = .05, 400; cam.rotation_mode = 'QUATERNION'
world = bpy.data.worlds.new('Previs gök'); s.world = world; world.use_nodes = True
BG = world.node_tree.nodes['Background']
hud = empty('HUD', cam, (0, 0, -1)); stars = empty('Yıldızlar', cam, (0, 0, -300))
bm = bmesh.new()
for _ in range(90):
    ico(RNG.uniform(.0022, .0052), Matrix.Translation((RNG.uniform(-1.05, 1.05), RNG.uniform(-.6, .6), 0)), 1, bm)
star_obj = obj('Yıldız alanı', bm, plain(CREAM), parent=stars, smooth=False, toggle=True)

labels = []
for name, f0, desc in SHOTS:
    t = bpy.data.objects.new(f'Etiket {name}', bpy.data.curves.new(f'Etiket {name}', 'FONT')); s.collection.objects.link(t)
    t.data.body = f'{name} · {desc}'; t.data.size = .046; t.data.materials.append(plain(CREAM))
    t.parent = hud; t.location = (-.96, .5625 - .075, .03); TOGGLE.append(t); labels.append(t)
bpy.context.view_layer.update()
plates_lbl = [obj(f'Etiket zemini {i}', box((t.dimensions.x + .04, .072, .001), Matrix.Translation((-.96 + t.dimensions.x / 2, .5625 - .062, .02))),
                  plain(INK), parent=hud, smooth=False, toggle=True) for i, t in enumerate(labels)]

speed = empty('Hız çizgileri', hud); SPEED_LINES = []
for i in range(22):
    band = 1 if i % 2 else -1
    o = obj(f'Hız çizgisi {i}', box((1, 1, .001)), glow('#B9B0D6'), parent=speed, smooth=False, toggle=True)
    SPEED_LINES.append((o, band * RNG.uniform(.3, .56), RNG.uniform(.12, .34), RNG.uniform(.003, .007), RNG.random()))
smear = empty('Kamçı', hud); SMEAR_LINES = []
for i in range(40):
    o = obj(f'Kamçı çizgisi {i}', box((1, 1, .001)), glow(('#6E6890', '#9A90C0', FLAME)[i % 3 if i % 7 else 2]), parent=smear, smooth=False, toggle=True)
    SMEAR_LINES.append((o, RNG.uniform(-.56, .56), RNG.uniform(.3, 1.1), RNG.uniform(.003, .014), RNG.random()))
flash_a = obj('Parıltı dış', sphere(1, TRS((0, 0, -.05), scale=(1, 1, .01)), 48, 8), glow('#FFC46B'), parent=hud, smooth=False, toggle=True)
flash_b = obj('Parıltı iç', sphere(1, TRS((0, 0, -.04), scale=(1, 1, .01)), 48, 8), glow('#FFF1C9'), parent=hud, smooth=False, toggle=True)
bm = bmesh.new()
for i in range(18):
    a = i / 18 * 2 * math.pi + RNG.uniform(-.08, .08); r0 = RNG.uniform(.16, .3)
    box((RNG.uniform(.5, .9), RNG.uniform(.006, .016), .001), TRS((math.cos(a) * (r0 + .4), math.sin(a) * (r0 + .4), 0), (0, 0, a)), bm)
impact_lines = obj('Darbe çizgileri', bm, plain(INK), parent=hud, smooth=False, toggle=True)

# ---------------------------------------------------------------- roket (tek model)
SHIP = empty('ROKET')
SHIP.rotation_mode = 'XYZ'
obj('Gövde', cone(.45, .45, 2.2, ROT_Z_TO_X, 32), toon(HULL, HULL_SH), .028, SHIP)
obj('Gövde şeridi', cone(.462, .462, .16, Matrix.Translation((.82, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE, NOSE_SH), .0, SHIP)
bm = bmesh.new()
for x in (-.55, .2):
    torus(.452, .012, Matrix.Translation((x, 0, 0)) @ ROT_Z_TO_X, 48, 4, bm)
obj('Sac çizgileri', bm, glow(INK), 0, SHIP, smooth=False)
nose = obj('Burun', cone(.45, .07, 1.0, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE, NOSE_SH), .028, SHIP, toggle=True)
nose_hot = obj('Burun (ısınmış)', cone(.46, .08, 1.02, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X, 32), toon(HOT, RED_HI, '#FFF6D8'), .028, SHIP, toggle=True)
obj('Motor ağzı', cone(.38, .26, .35, Matrix.Translation((-1.275, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOZZLE, NOZZLE_SH), .028, SHIP)
bm = bmesh.new()
for a in (math.radians(d) for d in (45, 135, 225, 315)):
    d, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    vs = [bm.verts.new(Vector((x, 0, 0)) + d * r + t * side)
          for x, r in ((-1.10, .42), (-.35, .42), (-1.02, .98), (-1.42, .98)) for side in (-.035, .035)]
    bmesh.ops.convex_hull(bm, input=vs)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
obj('Kanatlar', bm, toon(FIN, FIN_SH), .028, SHIP, smooth=False)
obj('Yama', box((.34, .05, .24), TRS((-.6, -.43, -.14), (0, .12, 0))), toon('#C9B27A', '#8C7650'), .02, SHIP, smooth=False)
obj('Lomboz çerçevesi', torus(.2, .05, TRS((.35, -.45, .06), (math.pi / 2, 0, 0))), toon(NOZZLE, NOZZLE_SH), .02, SHIP)
glass = obj('Lomboz camı', sphere(.19, TRS((.35, -.44, .06), scale=(1, .3, 1))), toon(GLASS, GLASS_SH, '#FFFFFF'), 0, SHIP, toggle=True)
glass_lit = obj('Lomboz camı (ışıklı)', sphere(.192, TRS((.35, -.441, .06), scale=(1, .3, 1))), glow(HOT), 0, SHIP, toggle=True)
flame = empty('Alev', SHIP, (-1.45, 0, 0))
flame_parts = [obj('Alev dış', cone(.02, .32, 1.7, Matrix.Translation((-.85, 0, 0)) @ ROT_Z_TO_X), glow(FLAME), .025, flame, toggle=True),
               obj('Alev iç', cone(.01, .17, 1.0, Matrix.Translation((-.5, 0, 0)) @ ROT_Z_TO_X), glow(FLAME_IN), 0, flame, toggle=True)]
bm = bmesh.new()
for z in (-.28, .04, .3):
    between((1.7, -.25, z), (-.9, -.3, z * 1.5), .035, .004, 6, bm)
heat = obj('Isı izleri', bm, glow(HOT), 0, SHIP, toggle=True)
port_ring = obj('Lomboz ışık halkası', torus(.3, .025, Matrix(), 32, 4), glow(HOT), 0, SHIP, at=(.35, -.5, .06), toggle=True)
port_ring.rotation_euler = (math.pi / 2, 0, 0)


def ship_pt(local): return SHIP.matrix_world @ Vector(local)


def rot_toward(v, roll=0.0):
    """+X burnu v yönüne çevirir."""
    q = Vector(v).normalized().to_track_quat('X', 'Z') @ Quaternion((1, 0, 0), roll)
    return q.to_euler()


# ---------------------------------------------------------------- plaka yardımcıları (Dünya gerçekten kırılır)
ROCK_M = toon(CRUST, CRUST_SH)


def thick_plate(name, bm, top_mat, thickness, ink):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.shade_smooth()
    o = bpy.data.objects.new(name, me); s.collection.objects.link(o)
    for m in (top_mat, ROCK_M, INK_M, INK_M): o.data.materials.append(m)
    t = o.modifiers.new('Kalınlık', 'SOLIDIFY'); t.thickness, t.offset, t.use_rim = thickness, -1.0, True
    t.material_offset, t.material_offset_rim = 1, 1
    k = o.modifiers.new('Mürekkep kabuk', 'SOLIDIFY')
    k.thickness, k.offset, k.use_flip_normals, k.use_rim, k.material_offset = ink, 1.0, True, False, 2
    return o


def sphere_plates(prefix, R, K, seed, top_mat, thickness, center, sub=4, zone=None, ink=.03):
    """Küreyi K parçalık Voronoi plakalara böler. zone=(yön, açı): yalnız o bölge."""
    rng = random.Random(seed)
    if zone:
        D, ang = Vector(zone[0]).normalized(), zone[1]
        T1 = D.cross(Vector((0, 0, 1))).normalized() if abs(D.z) < .95 else D.cross(Vector((1, 0, 0))).normalized(); T2 = D.cross(T1)
        seeds = []
        while len(seeds) < K:
            u, v = rng.uniform(-ang, ang), rng.uniform(-ang, ang)
            if u * u + v * v < (ang * .85) ** 2: seeds.append((D + T1 * u + T2 * v).normalized())
    else:
        seeds = [Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized() for _ in range(K)]
    base = ico(1.0, sub=sub if not zone else 1)
    if zone:   # bölge için ince ızgara
        base.free(); base = bmesh.new(); N = 64; grid = {}
        for i in range(N + 1):
            for j in range(N + 1):
                u, v = -ang + 2 * ang * i / N, -ang + 2 * ang * j / N
                grid[i, j] = base.verts.new((D + T1 * u + T2 * v).normalized())
        for i in range(N):
            for j in range(N):
                cu, cv = -ang + 2 * ang * (i + .5) / N, -ang + 2 * ang * (j + .5) / N
                if cu * cu + cv * cv <= ang * ang: base.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
        for v in [v for v in base.verts if not v.link_faces]: base.verts.remove(v)
    groups = {}
    for f in base.faces:
        c = f.calc_center_median().normalized()
        j = c + Vector((mnoise.noise(c * 7 + Vector((seed, 0, 0))), mnoise.noise(c * 7 + Vector((0, seed, 3))), mnoise.noise(c * 7 + Vector((5, 0, seed))))) * .07
        k = max(range(K), key=lambda q: j.dot(seeds[q]))
        groups.setdefault(k, []).append([v.co.copy() for v in f.verts])
    base.free(); plates = []
    for k, faces in groups.items():
        bm = bmesh.new(); vmap = {}
        for poly in faces:
            vs = []
            for co in poly:
                key = (round(co.x, 5), round(co.y, 5), round(co.z, 5))
                if key not in vmap: vmap[key] = bm.verts.new(co * R)
                vs.append(vmap[key])
            try: bm.faces.new(vs)
            except ValueError: pass
        bm.normal_update()   # açık yama: yönü merkezden dışarı zorla (yoksa kalınlık dışarı taşar)
        for fc in bm.faces:
            if fc.normal.dot(fc.calc_center_median()) < 0: fc.normal_flip()
        bm.normal_update()
        cen = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
        pivot = empty(f'{prefix} eksen {k}', at=Vector(center) + cen)
        o = thick_plate(f'{prefix} plaka {k}', bm, top_mat, thickness, ink); o.parent = pivot; o.location = -cen
        plates.append({'pivot': pivot, 'obj': o, 'n': cen.normalized(), 'cen': Vector(center) + cen,
                       'axis': Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized(), 'w': rng.uniform(.6, 1.0), 'seed': seeds[k]})
    return plates


def pool(prefix, n, bm_fn, mat, ink, smooth=True):
    return [obj(f'{prefix} {i}', bm_fn(i), mat, ink, smooth=smooth, toggle=True) for i in range(n)]


# ---------------------------------------------------------------- Ç1 — Dünya yakından
R1 = 10.0
D1 = Vector((0, math.sin(.1), math.cos(.1)))   # çatlak bölgesi kameranın önünde, ufka yapışık değil
EARTH_M1 = toon(OCEAN, OCEAN_SH, tex=earth_tex(.16), name='Dünya yüzeyi (yakın)')
# Yüzey küresi bölgede delinir; altındaki magma ancak plakalar ayrılınca görünür.
ZONE_ANG = .24
bm = sphere(R1, u=192, v=96)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().normalized().angle(D1) < ZONE_ANG * .97], context='FACES')
earth = obj('Dünya (delik)', bm, EARTH_M1, .03, toggle=True)
# Basit hile: ilk çatlağa (f8) kadar bütün küre; çatlak anında sarsıntı + parlamayla plakalı küreye geçilir.
earth_whole = obj('Dünya (bütün)', sphere(R1, u=192, v=96), EARTH_M1, .03, toggle=True)
obj('Magma (çatlak altı)', sphere(R1 - .02, u=128, v=64), glow(MAGMA), 0)
zone_plates = sphere_plates('Ç1', R1 + .015, 9, 11, EARTH_M1, .45, (0, 0, 0), zone=(D1, ZONE_ANG), ink=.025)
TOGGLE.extend(p['obj'] for p in zone_plates)
shards1 = pool('Ç1 kıymık', 16, lambda i: ico(RNG.uniform(.07, .2), TRS(scale=(1.3, 1, .7)), 1), toon(LAND, LAND_SH), .02, False)
for i, o in enumerate(shards1):
    if i % 3 == 0: o.data.materials[0] = ROCK_M
burst_star = obj('Ç1 patlama yıldızı', star(1.0, .35, 9), glow(HOT), 0, smooth=False, toggle=True)
burst_core = obj('Ç1 patlama göbeği', sphere(1.0, u=24, v=12), glow('#FFF6D8'), 0, toggle=True)
crack_flash = obj('Ç1 çatlak parlaması', star(1.0, .22, 6), glow(HOT), .0, smooth=False, toggle=True)
fire_head = obj('Ç1 fırlayan şey', sphere(.24, u=16, v=8), glow(FLAME_IN), .04, toggle=True)   # krem patlamanın önünde seçilsin
fire_tail = obj('Ç1 fırlayan şey izi', cone(.0, .24, 1.0, Matrix.Translation((0, 0, -.5))), glow(FLAME), .035, toggle=True)
fire_tail.rotation_mode = 'QUATERNION'

# ---------------------------------------------------------------- Ç2 — kaçış, parçalanan Dünya
O2 = Vector((2000, 0, 0)); R2 = 6.0; E2 = O2 + Vector((-15, 30, 7))
EARTH_M2 = toon(OCEAN, OCEAN_SH, tex=earth_tex(.27), name='Dünya yüzeyi (uzak)')
plates2 = sphere_plates('Ç2', R2, 16, 5, EARTH_M2, .9, E2, sub=4, ink=.12)
core2 = obj('Ç2 çekirdek', sphere(R2 * .86, u=48, v=24), toon(RED, RED_SH, RED_HI), .06, at=E2)
core2_hot = obj('Ç2 çekirdek ışığı', sphere(R2 * .6, u=32, v=16), glow(HOT), 0, at=E2)
shock = obj('Ç2 şok halkası', torus(1.0, .05, Matrix(), 64, 6), glow(CREAM), 0, toggle=True)
shards2 = pool('Ç2 kıymık', 14, lambda i: ico(RNG.uniform(.2, .6), TRS(scale=(1.3, 1, .75)), 1), toon(RED, RED_SH, RED_HI), .03, False)
near_shard = obj('Ç2 kıl payı kaya', ico(.7, TRS(scale=(1.5, 1, .8)), 1), toon(CRUST, CRUST_SH), .04, smooth=False, toggle=True)

# ---------------------------------------------------------------- Ç3a — kokpit göstergesi
O3 = Vector((4000, 0, 0)); FACE = Matrix.Rotation(math.pi / 2, 4, 'X')
obj('Kokpit duvarı', box((16, .3, 10), Matrix.Translation(O3 + Vector((0, .45, 0)))), toon(PANEL, PANEL_SH), 0, smooth=False)
obj('Gösterge yuvası', cone(1.45, 1.45, .3, Matrix.Translation(O3 + Vector((0, .25, 0))) @ FACE, 48), toon(PANEL_SH, '#16182A'), .03)
obj('Gösterge çerçevesi', torus(1.2, .16, Matrix.Translation(O3 + Vector((0, -.02, 0))) @ FACE, 64, 12), toon(NOZZLE, NOZZLE_SH, '#9AA0C0'), .03)
obj('Gösterge yüzü', cone(1.1, 1.1, .1, Matrix.Translation(O3 + Vector((0, .05, 0))) @ FACE, 64), toon(CREAM, CREAM_SH), 0)


def dial(a_deg, r, y=-.02):
    a = math.radians(a_deg); return O3 + Vector((r * math.cos(a), y, r * math.sin(a)))


bm = bmesh.new()
for a in (150, 130, 110, 90, 70, 50, 30):
    box((.06, .03, .22), TRS(dial(a, .84), (0, math.pi / 2 - math.radians(a), 0)), bm)
obj('Çentikler', bm, glow(INK), 0, smooth=False)
bm = bmesh.new()
for a in (156, 148, 140):
    box((.13, .03, .22), TRS(dial(a, .84, -.03), (0, math.pi / 2 - math.radians(a), 0)), bm)
obj('Kırmızı bölge', bm, glow('#E0503A'), 0, smooth=False)
bm = box((.2, .04, .26), Matrix.Translation(O3 + Vector((-.05, -.03, -.45))))
box((.05, .04, .16), Matrix.Translation(O3 + Vector((.1, -.03, -.38))), bm)
obj('Yakıt simgesi', bm, glow(INK), 0, smooth=False)
needle = empty('İbre ekseni', at=O3 + Vector((0, -.08, 0))); needle.rotation_mode = 'XYZ'
obj('İbre', box((.8, .03, .05), Matrix.Translation((.4, 0, 0))), glow(INK), 0, needle, smooth=False)
obj('İbre ucu', box((.16, .035, .055), Matrix.Translation((.72, -.002, 0))), glow('#E0503A'), 0, needle, smooth=False)
obj('İbre göbeği', cone(.11, .11, .06, Matrix.Translation(O3 + Vector((0, -.1, 0))) @ FACE), toon(NOZZLE, NOZZLE_SH), .02)
obj('Cam parıltısı', torus(.88, .03, Matrix.Translation(O3 + Vector((0, -.14, 0))) @ FACE, 24, 4, a0=math.radians(100), a1=math.radians(150)), plain('#FFFFFF'), 0, smooth=False)
bm = bmesh.new()
for rr in (.6, .72):
    torus(rr, .013, Matrix.Translation(O3 + Vector((0, -.12, 0))) @ FACE, 16, 4, bm, math.radians(110), math.radians(146))
needle_arcs = obj('İbre hareket yayları', bm, glow(INK), 0, smooth=False, toggle=True)
obj('İkinci gösterge', cone(.55, .55, .12, Matrix.Translation(O3 + Vector((-2.5, 0, -.9))) @ FACE, 40), toon(CREAM, CREAM_SH), .03)
obj('İkinci gösterge çerçevesi', torus(.6, .08, Matrix.Translation(O3 + Vector((-2.5, -.05, -.9))) @ FACE, 40, 8), toon(NOZZLE, NOZZLE_SH), .02)
bm = bmesh.new()
for x, z in ((-1.75, 1.2), (1.75, 1.2), (-1.75, -1.2), (1.75, -1.2)):
    sphere(.07, Matrix.Translation(O3 + Vector((x, .1, z))), 12, 6, bm)
obj('Vidalar', bm, toon(NOZZLE, NOZZLE_SH), .01)
LAMP_AT = O3 + Vector((1.75, -.05, .5))
obj('Uyarı lambası yuvası', cone(.22, .22, .12, Matrix.Translation(LAMP_AT + Vector((0, .1, 0))) @ FACE), toon(NOZZLE, NOZZLE_SH), .02)
lamp_off = obj('Uyarı lambası (sönük)', sphere(.15, Matrix.Translation(LAMP_AT)), toon(LAMP_OFF, '#3A1418'), .02, toggle=True)
lamp_on = obj('Uyarı lambası (yanık)', sphere(.16, Matrix.Translation(LAMP_AT)), glow(LAMP_ON), .02, toggle=True)
lamp_ring = obj('Uyarı halkası', torus(1.0, .06, FACE, 32, 4), glow(LAMP_ON), 0, at=LAMP_AT + Vector((0, -.05, 0)), toggle=True)
bm = between(Vector((1.15, -.45, -1.05)), Vector((0, 0, 0)), .17, .15, 16)
sphere(.15, Matrix(), 16, 8, bm); box((.9, .6, .7), TRS((1.45, -.55, -1.35), (0, .7, 0)), bm)
finger = obj('Eldiven', bm, toon(GLOVE, GLOVE_SH), .035, toggle=True)
bm = bmesh.new()
for k in range(3):
    a = math.radians(95 + k * 38); d = Vector((math.cos(a), 0, math.sin(a)))
    between(d * .26, d * .44, .017, .017, 6, bm)
taps = obj('Vuruş izleri', bm, glow(INK), 0, toggle=True)

# ---------------------------------------------------------------- Ç3b — motor söner
O4 = Vector((6000, 0, 0))
puffs3 = pool('Ç3b duman', 4, lambda i: ico(1.0, sub=2), toon(SMOKE, SMOKE_SH), .03)

# ---------------------------------------------------------------- Ç4 — yeni gezegen
O5 = Vector((8000, 0, 0)); R5 = 16.0; PL = O5 + Vector((14, 34, -24))
obj('Eri\'nin gezegeni', sphere(R5, u=128, v=64), toon(RUST, RUST_SH, RUST_HI, tex=rust_tex(.12), rim=1.4, name='Gezegen'), .12, at=PL)
glint = obj('İpucu parıltısı', star(.9, .12, 4, Matrix()), glow(GLINT), 0, smooth=False, toggle=True)
GLINT_AT = PL + Vector((-.3, -.4, .87)).normalized() * (R5 + .05)   # f150'de kadrajda görünen tepe yakını
glint.location = GLINT_AT; glint.rotation_euler = (math.pi / 2, 0, 0)
dashes = pool('Ç4 yay', 46, lambda i: cone(.06, .06, 1.0, ROT_Z_TO_X, 8), glow(CREAM), 0)
for d in dashes: d.rotation_mode = 'QUATERNION'

# ---------------------------------------------------------------- Ç5 — bulutlar
O6 = Vector((10000, 0, 0))
CLOUD_M = toon(CLOUD_W, CLOUD_SH, '#FFF6E6')


def cloud_bm(rng, size):
    bm = bmesh.new(); x = -size * 2.2
    while x < size * 2.2:
        r = rng.uniform(.55, 1.0) * size
        sphere(r, TRS((x, rng.uniform(-.3, .3), rng.uniform(0, .3) * size), scale=(1.25, .8, .62)), 20, 10, bm); x += r * rng.uniform(1.0, 1.4)
    return bm


clouds = []
for layer, (y, zr, speed_u, size, n) in enumerate(((16, (-14, 8), 6, 1.6, 8), (5, (-22, 6), 12, 1.1, 9), (-4, (-34, 4), 22, .8, 10))):
    rng = random.Random(40 + layer)
    for i in range(n):
        z = zr[0] + (zr[1] - zr[0]) * i / n + rng.uniform(-1, 1); x = rng.uniform(-9, 9) * (1 if layer < 2 else .5)
        o = obj(f'Bulut {layer}-{i}', cloud_bm(rng, size), CLOUD_M, .04 * size, at=O6 + Vector((x, y, z)))
        clouds.append((o, o.location.copy(), speed_u))
wipe = obj('Bulut perdesi', cloud_bm(random.Random(9), 1.6), CLOUD_M, .05, toggle=True)
smoke5 = pool('Ç5 duman', 12, lambda i: ico(1.0, sub=2), toon(SMOKE, SMOKE_SH), .025)

# ---------------------------------------------------------------- Ç6 — yer (banner zemini + seçim ekranı ufku)
O7 = Vector((12000, 0, 0)); LAND = O7 + Vector((1.6, -.6, 0))
obj('Zemin', box((260, 260, .2), Matrix.Translation(O7 + Vector((0, 100, -.1)))), toon(RUST, RUST_SH, RUST_HI, tex=rust_tex(.35), rim=0, name='Zemin'), 0, smooth=False)
obj('Ufuk ışığı alt', box((300, .2, 6), Matrix.Translation(O7 + Vector((0, 90, 1.0)))), glow(GLOW_LOW), 0, smooth=False)
obj('Ufuk ışığı üst', box((300, .2, 6), Matrix.Translation(O7 + Vector((0, 90.5, 6.5)))), glow(GLOW_HIGH), 0, smooth=False)
rng = random.Random(6); bm = bmesh.new(); x = -80
while x < 80:
    sphere(rng.uniform(7, 12), TRS(O7 + Vector((x, 60 + rng.uniform(-4, 4), -1.5)), scale=(1.3, .6, rng.uniform(3, 7) / 22)), 24, 12, bm); x += rng.uniform(9, 15)
obj('Tepeler', bm, toon(HILL, '#24161E', rim=0), .25)
bm = bmesh.new()
for i in range(34):
    p = O7 + Vector((rng.uniform(-16, 16), rng.uniform(-4, 26), 0))
    ico(rng.uniform(.15, .55), TRS(p, (rng.random() * 3,) * 3, (1.4, 1, .6)), 1, bm)
obj('Kayalar', bm, toon(RUST_HI, RUST_SH), .03, smooth=False)
def leaf_bm(L, w, bend, m):
    """Sivri uçlu, kıvrık yaprak: elips küre boyuna incelir ve yukarı bükülür."""
    bm = sphere(1.0, u=32, v=12)
    for v in bm.verts:
        t = (v.co.x + 1) / 2; taper = math.sin(math.pi * clamp(t)) ** .75
        v.co = m @ Vector((v.co.x * L, v.co.y * .05, v.co.z * w * taper + bend * L * t * t))
    return bm


LEAF_M = toon(LEAF_FG, '#0C0D18')
leaves = [obj('Ön yaprak sol', leaf_bm(1.5, .32, .35, TRS(O7 + Vector((-3.3, -6.9, -.45)), (0, -.5, .25))), LEAF_M, .035),
          obj('Ön yaprak sol 2', leaf_bm(1.1, .26, .3, TRS(O7 + Vector((-3.0, -7.15, -.6)), (0, -.15, .5))), LEAF_M, .035),
          obj('Ön yaprak sağ', leaf_bm(1.4, .3, .35, TRS(O7 + Vector((3.3, -6.9, -.5)), (0, -.5, math.pi - .25))), LEAF_M, .035)]
bm = bmesh.new()
for i in range(9):
    a = i / 9 * 2 * math.pi + .3
    box((RNG.uniform(.8, 1.6), .05, .02), TRS(LAND + Vector((math.cos(a), math.sin(a), 0)) * 1.0 + Vector((0, 0, .03)), (0, 0, a)), bm)
ground_cracks = obj('Zemin çatlakları', bm, glow(INK), 0, smooth=False, toggle=True)
skid = obj('Kayma izi', box((3.0, .8, .05), TRS(LAND + Vector((-.9, .2, .02)), (0, 0, .05))), glow('#4A1E18'), 0, smooth=False, toggle=True)
smoke6 = pool('Ç6 duman', 10, lambda i: ico(1.0, sub=2), toon(SMOKE, SMOKE_SH), .025)
dust6 = pool('Ç6 toz', 16, lambda i: ico(1.0, sub=2), toon(DUST, DUST_SH), .03)
rocks6 = pool('Ç6 taş', 10, lambda i: ico(RNG.uniform(.06, .13), sub=1), toon(RUST_HI, RUST_SH), .015, False)
DUST = [(Vector((math.cos(a), .55 * math.sin(a), 0)) * RNG.uniform(.6, 1.3), RNG.uniform(.3, 2.2), RNG.uniform(.6, 1.25))
        for a in (i / 16 * 2 * math.pi + RNG.uniform(-.2, .2) for i in range(16))]   # (yön, yükselme, boy)
ROCK_V = [Vector((RNG.uniform(-3, 3), RNG.uniform(-1.5, .6), RNG.uniform(3, 6))) for _ in range(10)]

# ---------------------------------------------------------------- kare kare durum
VISIBLE = set()


def on(*objs):
    for o in objs: VISIBLE.add(o.name)


def K(o, f, force=False):
    """Nesneler ikişer karede anahtarlanır (anime adımı)."""
    if f % 2 and not force: return
    for p in ('location', 'rotation_euler' if o.rotation_mode != 'QUATERNION' else 'rotation_quaternion', 'scale'):
        o.keyframe_insert(p, frame=f + 1)


def light(f, key, rim, rim_col, rim_amt, tint=(1, 1, 1), impact=0.0, sky=SPACE):
    L_KEY.inputs[0].default_value, L_KEY.inputs[1].default_value, L_KEY.inputs[2].default_value = key
    L_RIM.inputs[0].default_value, L_RIM.inputs[1].default_value, L_RIM.inputs[2].default_value = rim
    L_RIMC.outputs[0].default_value = C(rim_col) if isinstance(rim_col, str) else rim_col
    L_RIMA.outputs[0].default_value = rim_amt
    L_TINT.outputs[0].default_value = (*tint, 1); L_IMP.outputs[0].default_value = impact
    BG.inputs['Color'].default_value = C(CREAM if impact > .5 else sky) if isinstance(sky, str) else sky
    for n, i in ((L_KEY, 0), (L_KEY, 1), (L_KEY, 2), (L_RIM, 0), (L_RIM, 1), (L_RIM, 2)):
        n.inputs[i].keyframe_insert('default_value', frame=f + 1)
    for n in (L_RIMC, L_RIMA, L_TINT, L_IMP): n.outputs[0].keyframe_insert('default_value', frame=f + 1)
    BG.inputs['Color'].keyframe_insert('default_value', frame=f + 1)


# Vurgular: (kare, derece). Kamera her karede; sarsıntı açısal (mesafeden bağımsız).
IMPULSES = [(8, 1.2), (20, 2.0), (32, 3.6), (48, 1.6), (70, 2.2), (104, .5), (108, .35), (112, .35), (129, .7),
            (150, .45), (176, 1.6), (IMPACT, 4.8)]


def shake(f, hand):
    a = 0.0
    for f0, amp in IMPULSES:
        d = f - f0
        if 0 <= d < 30: a += amp * math.exp(-d / 3.2)
    if 180 <= f < 204: a += .5 + .7 * (f - 180) / 24   # atmosfer sarsıntısı
    ar = math.radians(a)
    yaw = ar * math.sin(f * 2.9 + 1) + math.radians(hand) * mnoise.noise(Vector((f * .13, 0, .5)))
    pitch = ar * math.cos(f * 3.4) + math.radians(hand) * mnoise.noise(Vector((0, f * .13, 2.5)))
    return yaw, pitch, ar * .7 * math.sin(f * 2.2 + .4)


def camera(f, loc, target, lens, roll=0.0, hand=.25):
    q = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y') @ Quaternion((0, 0, 1), roll)
    y, p, r = shake(f, hand)
    q = q @ Quaternion((0, 1, 0), y) @ Quaternion((1, 0, 0), p) @ Quaternion((0, 0, 1), r)
    cam.location, cam.rotation_quaternion, cam.data.lens = loc, q, lens
    cam.keyframe_insert('location', frame=f + 1); cam.keyframe_insert('rotation_quaternion', frame=f + 1)
    cam.data.keyframe_insert('lens', frame=f + 1)
    k = 18 / lens; hud.scale = (k, k, k); stars.scale = (300 * k,) * 3
    hud.keyframe_insert('scale', frame=f + 1); stars.keyframe_insert('scale', frame=f + 1)


def ship(f, at, rot, scale=1.0, thrust=0.0, hot=False, lit=False, force=False):
    SHIP.location, SHIP.rotation_euler, SHIP.scale = at, rot, (scale,) * 3
    if thrust > .01:
        fl = thrust * (1 + .12 * math.sin(f * 2.3) + .06 * math.sin(f * 5.1))
        flame.scale = (fl, min(1.2, .5 + .6 * thrust), min(1.2, .5 + .6 * thrust)); on(*flame_parts)
    on(nose_hot if hot else nose); on(glass_lit if lit else glass)
    if hot: on(heat)
    K(SHIP, f, force); K(flame, f, force)
    bpy.context.view_layer.update()


def speed_lines(f, angle, amount=1.0):
    speed.rotation_euler = (0, 0, angle); K(speed, f, True)
    for o, y, ln, th, ph in SPEED_LINES:
        u = ((f * .09 * (1 + ph)) + ph) % 1
        o.location = (1.3 - 2.6 * u, y, 0); o.scale = (ln * amount, th, 1); K(o, f, True); on(o)


def puff_life(o, f, born, at, drift, r0, r1, life):
    a = (f - born) / FPS
    if a < 0 or a > life: return
    o.location = at + drift * a; sc = r0 + (r1 - r0) * ease_out(a / (life * .6)); sc *= 1 - ease_in((a - life * .7) / (life * .3))
    o.scale = (sc,) * 3; o.rotation_euler = (a * 2 + born, a + born * .3, 0); K(o, f); on(o)


# ---------------- Ç1
CAM1 = Vector((.6, -1.7, 11.15))   # yüzeyin ~1,2 üstünde; bölgeye ~20° aşağı bakar, ufuk ve uzay üstte
CRACK_SPARK = [(D1 * R1).lerp(p['cen'], .5) + D1 * .12 for p in zone_plates[:2]]   # iki plaka arasındaki çizgi üstü


def shot1(f):
    zc = D1 * R1
    if f < 8: on(earth_whole)
    else: on(earth, *(p['obj'] for p in zone_plates))
    sep = .13 * ease((f - 8) / 2) if f >= 8 else 0
    lift2 = ease((f - 20) / 5) if f >= 20 else 0
    lift3 = ease_out((f - 32) / 15) if f >= 32 else 0
    for p in zone_plates:
        tang = (p['cen'] - zc); tang = tang - p['n'] * tang.dot(p['n'])
        tang = tang.normalized() if tang.length > 1e-6 else Vector((1, 0, 0))
        w = clamp(1 - (p['seed'].angle(D1) / ZONE_ANG), .15, 1)
        # patlamada plakalar kameraya değil yana ve yukarı savrulur: kamera tarafındakiler yatayda az itilir
        away = .2 if tang.dot((CAM1 - zc).normalized()) > .2 else 1.0   # kamera tarafındaki plakalar fırlayışı örtmesin
        off = p['n'] * (sep * .3 + lift2 * .35 * w + lift3 * (1.4 * w + .3) * away) + tang * (sep + lift2 * .12 + lift3 * 2.6 * w * away)
        p['pivot'].location = p['cen'] + off
        ang = (lift2 * .28 + lift3 * 1.1 * away) * w
        p['pivot'].rotation_euler = Quaternion(p['n'].cross(tang).normalized(), ang).to_euler()
        K(p['pivot'], f)
    for when, scale, at in ((8, .32, CRACK_SPARK[0]), (20, .5, CRACK_SPARK[1])):   # çatlak anında çizginin üstünde kısa kıvılcım
        if when <= f < when + 3:
            crack_flash.location = at
            crack_flash.rotation_euler = ((CAM1 - at).to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), when)).to_euler()
            crack_flash.scale = (scale * (1 - (f - when) / 3) + .05,) * 3
            K(crack_flash, f, True); on(crack_flash)
    if 32 <= f < 42:
        a = (f - 32) / 10
        burst_core.location = zc + D1 * .5; burst_core.scale = (.2 + .45 * ease_out(a * 2) * (1 - ease_in((a - .5) * 2)),) * 3
        burst_star.location = zc + D1 * .6
        burst_star.rotation_euler = ((CAM1 - burst_star.location).to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), f * .12)).to_euler()
        burst_star.scale = (.4 + 1.3 * ease_out(a * 1.6) * (1 - ease_in((a - .6) / .4)),) * 3
        K(burst_core, f, True); K(burst_star, f, True); on(burst_core, burst_star)
    rng = random.Random(1)
    for i, o in enumerate(shards1):
        born = 20 if i < 6 else 32
        v = D1 * rng.uniform(2, 5) + Vector((rng.choice((-1, 1)) * rng.uniform(1.5, 4), rng.uniform(-3.5, -1), rng.uniform(0, 2)))
        if f >= born:
            a = (f - born) / FPS
            o.location = zc + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) + v * a
            o.rotation_euler = (a * 6 + i, a * 4, a * 3 + i * .5); K(o, f); on(o)
    if f >= 32:
        a = (f - 32) / FPS; vel = Vector((3.2, -.5, 3.8))   # yavaş çıkar, sonra hızlanır: ışığın içinden ayrıldığı görülür
        head = zc + D1 * .4 + vel * a * (1 + 4 * a)
        fire_head.location = head; fire_tail.location = head
        fire_tail.rotation_quaternion = vel.normalized().to_track_quat('Z', 'Y')
        fire_tail.scale = (1, 1, 1.2 + 4 * a)
        K(fire_head, f, True); K(fire_tail, f, True); on(fire_head, fire_tail)
    # Patlama kamerayı geri ve yukarı iter (f31–40); alev topu uzayın önünde yükselirken okunur, sonra kamçı.
    loc = CAM1 + Vector((0, .45, -.12)) * ease(min(f, 31) / 40) + Vector((0, -3.0, 1.6)) * ease_out((f - 31) / 9)
    tgt = zc + Vector((.1, .4, .15))
    if f >= 33: tgt = tgt.lerp(fire_head.location, .55 * ease((f - 33) / 6))
    roll = .06 * ease((f - 8) / 26)
    if f >= 40:   # kamçı: fırlayan şeyi kovalar
        w = ease_in((f - 40) / 7)
        tgt = tgt.lerp(fire_head.location + Vector((3, 0, 2)), w); roll += .25 * w
        if f >= 42:
            for o, y, ln, th, ph in SMEAR_LINES:
                o.location = (((ph + f * .37) % 2.2) - 1.1, y, 0); o.scale = (ln, th, 1); K(o, f, True); on(o)
    camera(f, loc, tgt, 22, roll, .2)
    rim = HOT if f >= 8 else CREAM
    tint = (1, .93, .86) if 32 <= f < 40 else (1, 1, 1)
    light(f, (-.55, -.45, .7), (0, 1, .35), rim, 1.0 if f >= 8 else .8, tint)
    on(star_obj)


# ---------------- Ç2
def rocket2(f):
    u = (f - 48) / 52
    return O2 + Vector((-3.0 + 7.0 * u, 0, -1.0 + 2.0 * u))


def shot2(f):
    u = (f - 48) / 52
    for p in plates2:
        d = .4 + 5.0 * ease_out(u * 1.1)
        p['pivot'].location = p['cen'] + p['n'] * d * p['w']
        p['pivot'].rotation_euler = Quaternion(p['axis'], .7 * u * p['w']).to_euler(); K(p['pivot'], f)
    core2.scale = (1 + .12 * u,) * 3; K(core2, f)
    if f < 64:
        a = (f - 48) / 16
        shock.location = E2; shock.rotation_euler = (CAM_POS2(f) - E2).to_track_quat('Z', 'Y').to_euler()
        shock.scale = (R2 * (1.1 + 2.2 * ease_out(a)),) * 3; K(shock, f, True); on(shock)
    rng = random.Random(2)
    for i, o in enumerate(shards2):
        dirv = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
        o.location = E2 + dirv * (R2 * 1.1 + (f - 40) * .16 * rng.uniform(.7, 1.4))
        o.rotation_euler = (f * .1 + i, f * .07, i); K(o, f); on(o)
    if 62 <= f < 76:   # kıl payı: kameranın önünden geçer
        a = (f - 62) / 14
        near_shard.location = CAM_POS2(f) + Vector((4.5 - 9 * a, 3.2, 2.6 - 4.2 * a))
        near_shard.rotation_euler = (a * 5, a * 3, 1); K(near_shard, f, True); on(near_shard)
    pos = rocket2(f)
    ship(f, pos, (math.sin(f * .2) * .06, -.28, -.35), 1.0, 1.0)
    loc, tgt = CAM_POS2(f), CAM_TGT2(f)
    if f >= 92:   # lomboza dalış
        w = ease_in((f - 92) / 7.5)
        port = ship_pt((.35, -.45, .06)); out = (ship_pt((.35, -1.45, .06)) - port).normalized()
        loc = loc.lerp(port + out * .5, w); tgt = tgt.lerp(port, w)
    camera(f, loc, tgt, 24 + 6 * ease_in((f - 92) / 7.5), .03 * math.sin(f * .07), .3)
    speed_lines(f, .28)
    light(f, (-.4, -.8, .5), (-.5, .8, .3), RED_HI, 1.0, (1, .97, .94))
    on(star_obj)


def CAM_POS2(f):
    u = ease((f - 48) / 52)
    return rocket2(f) + Vector((-2.2 + 1.5 * u, -5.0 - .3 * u, -.6 + .2 * u))


def CAM_TGT2(f):
    u = (f - 48) / 52; yaw = math.radians(10 - 10 * u)
    return CAM_POS2(f) + Vector((math.sin(yaw), math.cos(yaw), .1)) * 10


# ---------------- Ç3a
def shot3a(f):
    a = {100: 95, 102: 132, 104: 160, 106: 147, 108: 152}
    deg = 152 if f >= 108 else a.get(f - f % 2, 95)
    needle.rotation_euler = (0, -math.radians(deg), 0); K(needle, f)
    if f < 106: on(needle_arcs)
    lamp = f >= 108
    on(lamp_on if lamp else lamp_off)
    for t0 in (108, 112):
        if t0 <= f < t0 + 6:
            lamp_ring.scale = (.25 + .5 * ease_out((f - t0) / 6),) * 3; K(lamp_ring, f, True); on(lamp_ring)
    tip_far, tip_hover, tip_tap = O3 + Vector((2.6, -1.4, -1.8)), O3 + Vector((.62, -.55, .16)), O3 + Vector((.42, -.28, .18))
    if f >= 104:
        if f < 106: tip = tip_far.lerp(tip_hover, (f - 104) / 2)
        elif f in (108, 109, 112, 113): tip = tip_tap
        else: tip = tip_hover
        finger.location = tip; K(finger, f); on(finger)
        if f in (108, 109, 112, 113): taps.location = tip_tap + Vector((0, -.05, 0)); K(taps, f, True); on(taps)
    pulse = f in (108, 109, 112, 113)
    u = (f - 100) / 18
    camera(f, O3 + Vector((-1.3, -4.7 + .35 * u, -.75)), O3 + Vector((.3, 0, .05)), 40, -.09, .45)
    light(f, (-.5, -.6, .6), (.75, -.25, .6), LAMP_ON, 1.0 if lamp else 0, (1, .74, .7) if pulse else (1, 1, 1), sky=PANEL_BG)


# ---------------- Ç3b
def flame_level(f):
    if f < 120: return 1.0
    return {120: .25, 122: .3, 124: 1.15, 126: .2, 128: .08}.get(f - f % 2, 0.0) if f < 129 else 0.0


def shot3b(f):
    u = (f - 118) / 18; fl = flame_level(f)
    pos = O4 + Vector((.8 * u, 0, .1 * u))
    ship(f, pos, (.25 * ease((f - 129) / 7) if f >= 129 else 0, -.15, -.3), 1.0, fl)
    back = (ship_pt((-1, 0, 0)) - ship_pt((0, 0, 0))).normalized()
    for o, born, r1 in zip(puffs3, (121, 125, 129, 131), (.34, .3, .22, .15)):
        puff_life(o, f, born, ship_pt((-1.9, 0, .1)), back * 1.8 + Vector((0, .2, .25)), .1, r1, 1.0)
    off = Vector((-3.4, -3.6, -1.1)) * (1 - .12 * ease(u))   # arka üç çeyrek: gövde ve motor ağzı birlikte görünür
    camera(f, pos + off, pos + Vector((-.9, 0, .15)), 30, .03 - .07 * ease(u), .3)
    cold = f >= 129
    light(f, (-.4, -.7, .6), (-1, .2, -.3), FLAME, min(1, fl), (.82, .86, 1.0) if cold else (1, 1, 1), sky=SPACE_COLD if cold else SPACE)
    on(star_obj)


# ---------------- Ç4
P_START, P_BEND, P_END = O5 + Vector((-2.6, 0, .5)), O5 + Vector((-1.7, 0, .55)), O5 + Vector((6.5, 4, -7.5))
PATH4 = [P_START] + bezier(P_BEND, (1, 0, .05), P_END, 60)
LEN4 = [0.0]
for a_, b_ in zip(PATH4, PATH4[1:]): LEN4.append(LEN4[-1] + (b_ - a_).length)
DASH_STEP = .3
TRAIL4 = resample(PATH4, DASH_STEP)


def rocket4(f):
    """Konum, hız yönü, yol boyunca alınan mesafe."""
    if f < 150:
        u = (f - 136) / 14; return P_START.lerp(P_BEND, u), Vector((1, 0, .05)), LEN4[1] * u
    s4 = ease_in((f - 150) / 29) * 60; i = min(int(s4), 59); w = s4 - i
    p = PATH4[1 + i].lerp(PATH4[2 + i], w)
    return p, (PATH4[2 + i] - PATH4[1 + i]), LEN4[1 + i] + w * (LEN4[2 + i] - LEN4[1 + i])


def shot4(f):
    pos, vel, dist = rocket4(f)
    turn = ease((f - 150) / 12)
    rot = Euler((.15 * math.sin(f * .05), 0, .1)).to_quaternion().slerp(rot_toward(vel).to_quaternion(), turn).to_euler()
    ship(f, pos, rot, .42, 0, hot=f >= 166)
    behind = dist - .75   # geminin kuyruğunun gerisinde kalan iz (kesik krem çizgi)
    for i, d in enumerate(dashes):
        j = 2 * i
        if j + 1 >= len(TRAIL4) or (j + 1) * DASH_STEP > behind: break
        a, b = TRAIL4[j], TRAIL4[j + 1]
        d.location = (a + b) / 2; d.rotation_quaternion = (b - a).normalized().to_track_quat('X', 'Z')
        d.scale = ((b - a).length, 1, 1); K(d, f); on(d)
    if f >= 164: speed_lines(f, -.95, .5 + .5 * ease((f - 164) / 10))
    if f >= 176:
        a = (f - 176) / 4
        for o, k in ((flash_a, 1.0), (flash_b, .62)):
            o.location = (.1, -.1, 0); o.scale = (k * (.15 + 2.6 * ease_in(a + .25)),) * 3; K(o, f, True); on(o)
    u = (f - 136) / 14; w = ease((f - 150) / 26)
    loc = O5 + Vector((.2, -14 + .6 * ease(u), 1.6)).lerp(Vector((1.8, -12.0, -1.4)), w)
    tgt = (O5 + Vector((.2, 0, .3))).lerp(pos + Vector((1.2, 0, -1.4)), w)
    camera(f, loc, tgt, 30 - 4 * w, -.06 * w, .25)
    light(f, (-.6, -.5, .6), (.3, 1, .2), '#FFD9A8', .9)
    on(star_obj)


# ---------------- Ç5
def shot5(f):
    a = (f - 180) / FPS
    for o, base, spd in clouds:
        o.location = base + Vector((0, 0, spd * a)); K(o, f)
    pos = O6 + Vector((.3 + .8 * a, 0, .6 - .6 * a)) + Vector((3.8, 0, -3.2)) * ease_in((f - 196) / 7)
    ship(f, pos, (.08 * math.sin(f * .6), .85, -.3), .9, 0, hot=f < 188)
    for k, o in enumerate(smoke5):   # her duman doğduğu yerde kalır; kamera düştüğü için yukarı akar
        born = 180 + 2 * k
        if f >= born:
            pb = O6 + Vector((.3 + .8 * (born - 180) / FPS, 0, .6 - .6 * (born - 180) / FPS))
            at = pb + Vector((-1.4, 0, .9)) + Vector((0, 0, 12 * (f - born) / FPS))
            puff_life(o, f, born, at, Vector(), .2, .6, 1.2)
    if f >= 197:
        w = ease_out((f - 197) / 6)
        wipe.location = O6 + Vector((.5, -6.5, -7 + 7.2 * w)); wipe.scale = (2.2, 1, 2.2); K(wipe, f, True); on(wipe)
    camera(f, O6 + Vector((0, -10, 0)), O6 + Vector((.6, 0, -.2)), 32, .05 * math.sin(f * .3), .5)
    speed_lines(f, math.pi / 2, .8)
    light(f, (-.5, -.6, .7), (.5, -.2, -.8), '#FFB07A', .8, sky=SKY5)


# ---------------- Ç6
def rocket6(f):
    if f < IMPACT:
        u = ease_in((f - 204) / (IMPACT - 204))
        return (O7 + Vector((-7.5, 3.5, 6.5))).lerp(LAND + Vector((-.4, 0, .5)), u), Euler((0, .62, -.25))
    u = ease_out((f - IMPACT - 2) / 14) if f >= IMPACT + 2 else 0
    bounce = .15 * math.sin(clamp((f - 216) / 6) * math.pi) if f >= 216 else 0
    pos = (LAND + Vector((-.4, 0, .5))).lerp(LAND + Vector((.5, 0, .58)), u) + Vector((0, 0, bounce))
    return pos, Euler((.12 * math.exp(-(f - IMPACT) / 8) * math.sin(f), .62 - .32 * u, -.25))


def shot6(f):
    on(*leaves)
    pos, rot = rocket6(f)
    ship(f, pos, rot, .8, 0, lit=f >= 236)
    for k, o in enumerate(smoke6):   # iniş dumanı: doğduğu yerde kalır, büyür, söner
        born = 204 + k; pb, _ = rocket6(born)
        puff_life(o, f, born, pb + Vector((-1.4, .4, 1.0)), Vector((0, 0, .3)), .2, .45, 1.1)
    if f >= IMPACT:
        on(ground_cracks)
        if f >= IMPACT + 4: on(skid)
        a = (f - IMPACT) / FPS
        for o, (dirv, rise, size) in zip(dust6, DUST):   # toz patlar, yükselir, ışık yanmadan (f236) dağılır
            if a > 1.05: continue
            sc = size * (.2 + .65 * ease_out(a / .3)) * (1 - ease_in((a - .55) / .5))
            o.location = LAND + dirv * 2.2 * (1 - math.exp(-a * 5)) + Vector((0, 0, .3 + rise * (1 - math.exp(-a * 3.5))))
            o.scale = (sc,) * 3; o.rotation_euler = (a, a * .5, 0); K(o, f); on(o)
        for o, v in zip(rocks6, ROCK_V):
            p = LAND + Vector((0, 0, .4)) + v * a + Vector((0, 0, -6.5 * a * a))
            if p.z < 0: continue
            o.location = p; o.rotation_euler = (a * 8, a * 5, 0); K(o, f); on(o)
    if 236 <= f < 244:
        port_ring.scale = (.3 + 1.4 * ease_out((f - 236) / 8),) * 3; K(port_ring, f, True); on(port_ring)
    if f in (IMPACT, IMPACT + 1): on(impact_lines)
    w = ease((f - IMPACT - 6) / 32) if f > IMPACT + 6 else 0
    loc = (O7 + Vector((0, -10.5, .85))).lerp(O7 + Vector((1.1, -7.6, .7)), w)
    tgt = (O7 + Vector((.3, 0, 1.9))).lerp(LAND + Vector((0, 0, .7)), w)
    camera(f, loc, tgt, 24, .02 * math.sin(f * .05), .25)
    impact = 1.0 if f in (IMPACT, IMPACT + 1) else 0.0
    light(f, (-.6, -.5, .6), (0, 1, .15), '#F0A060', .9, impact=impact, sky=SKY6)


FNS = [shot1, shot2, shot3a, shot3b, shot4, shot5, shot6]


def shot_index(f):
    return max(i for i, (_, f0, _) in enumerate(SHOTS) if f >= f0)


for f in range(END):
    VISIBLE.clear()
    i = shot_index(f); FNS[i](f); on(labels[i], plates_lbl[i])
    for o in TOGGLE:
        o.hide_render = o.name not in VISIBLE; o.keyframe_insert('hide_render', frame=f + 1)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'IntroPrevis.blend'))


def render_frames(frames, folder, prefix=''):
    for F in frames:
        s.frame_set(F); s.render.filepath = str(folder / f'{prefix}{F:04d}.png'); bpy.ops.render.render(write_still=True)


if ARGS == ['all']:
    s.render.filepath = str(FR) + '/'; bpy.ops.render.render(animation=True)
    # ipucu çeşidi: yalnız iki kare (Ç4, 152–153) yeşil parıltıyla
    glint.animation_data_clear()
    for F in (153, 154): glint.hide_render = False; glint.keyframe_insert('hide_render', frame=F)
    render_frames((153, 154), FR_GLINT)
else:
    render_frames([int(a) + 1 for a in ARGS if a.isdigit()], FR, 'deneme_')
print('INTRO PREVIS DONE', ARGS[:3])
