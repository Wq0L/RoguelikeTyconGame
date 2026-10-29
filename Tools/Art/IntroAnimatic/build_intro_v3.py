"""Açılış videosu — previs v3 (28 Ekim 2026). Docs/AcilisVideosu-CekimListesi.md (taslak 3).
  blender -b --factory-startup -t 4 -P Tools/Art/IntroAnimatic/build_intro_v3.py -- all
  blender ... -- 10 40 120        (yalnız bu kareler: deneme, kareler/deneme_####.png)
Çıktı: Docs/AcilisVideosu/Animatik/v3/kareler/####.png (354 kare, 14,75 sn), kareler_ipucu/, IntroV3.blend.
v2'den farkı (kullanıcı geri bildirimi): Dünya'dan uzaklaşma hissi (rokete yapışık kamera, küçülen Dünya,
geriye akan kıymıklar, arkadan yetişen şok dalgası), roket yatay değil (kameraya doğru fırlar), acelecilik
bitti (her vuruşa bekleme), kesmeler eylemin devamı, cila tasarlanmış şekillerle: 4 karelik el çizimi alev
döngüsü, topak duman, sivri hız çizgileri, Dünya'ya akan çizgiler, parçalı şok halkası, geçişte roketin uzaması,
vurgu öncesi hazırlık, gerçek eldiven. Rastgele kalabalık yok; her efekt bir vuruşa hizmet eder. Etiket yok.
Işık: lamba yok; her malzeme normal · anahtar yön / kenar yön ile basamaklanır ("Sahne ışığı" kare kare).
Nesneler ikişer karede (anime adımı), kamera her karede. Donma (hit-stop) yok.
Özgün kaba geometri; dış model/doku/eklenti yok.
"""
import bpy, bmesh, math, random, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion, Euler, noise as mnoise
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / 'v3'
FR, FR_GLINT = OUT / 'kareler', OUT / 'kareler_ipucu'
for d in (FR, FR_GLINT): d.mkdir(parents=True, exist_ok=True)
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['all']
FPS, END, IMPACT, GLINT_F = 24, 354, 316, 234
SHOTS = [('Ç1', 0), ('Ç2a', 60), ('Ç2b', 96), ('Ç3a', 148), ('Ç3b', 178), ('Ç4', 210), ('Ç5', 282), ('Ç6', 306)]

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
FLAME_OUT, FLAME, FLAME_IN = '#FF6E2E', '#FFB43A', '#FFF1A8'
SMOKE, SMOKE_SH, ASH, ASH_SH = '#9A93A6', '#5E5872', '#C9BBB4', '#7C6670'
PANEL, PANEL_SH, GLOVE, GLOVE_SH, CUFF, LAMP_OFF, LAMP_ON = '#3A4060', '#232640', '#E07A3A', '#9A4030', '#4A5068', '#5A2428', '#FF4A3A'
RUST, RUST_SH, RUST_HI, RUST_DARK = '#B5623E', '#7A3428', '#D98858', '#8E4430'
SKY5, CLOUD_W, CLOUD_SH = '#35304A', '#F2E3CC', '#A98EA8'
SKY6, GLOW_LOW, GLOW_HIGH, HILL, LEAF_FG = '#1E2436', '#D07A3A', '#9A3E2E', '#3A2630', '#141626'
DUST, DUST_SH, GLINT = '#E6C9A0', '#9A6450', '#9BE58A'

# ---------------------------------------------------------------- sahne ışığı: tek düğüm grubu, kare kare anahtarlanır
LG = bpy.data.node_groups.new('Sahne ışığı', 'ShaderNodeTree')
for name, typ in (('Key', 'NodeSocketVector'), ('Rim', 'NodeSocketVector'), ('RimColor', 'NodeSocketColor'),
                  ('RimAmount', 'NodeSocketFloat'), ('RimEdge', 'NodeSocketFloat'), ('Tint', 'NodeSocketColor'), ('Impact', 'NodeSocketFloat')):
    LG.interface.new_socket(name=name, in_out='OUTPUT', socket_type=typ)
_go = LG.nodes.new('NodeGroupOutput')
L_KEY = LG.nodes.new('ShaderNodeCombineXYZ'); L_RIM = LG.nodes.new('ShaderNodeCombineXYZ')
L_RIMC = LG.nodes.new('ShaderNodeRGB'); L_RIMA = LG.nodes.new('ShaderNodeValue'); L_RIME = LG.nodes.new('ShaderNodeValue')
L_TINT = LG.nodes.new('ShaderNodeRGB'); L_IMP = LG.nodes.new('ShaderNodeValue')
for src, dst in ((L_KEY, 'Key'), (L_RIM, 'Rim'), (L_RIMC, 'RimColor'), (L_RIMA, 'RimAmount'), (L_RIME, 'RimEdge'), (L_TINT, 'Tint'), (L_IMP, 'Impact')):
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
    rimf = op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', lw.outputs['Facing'], g.outputs['RimEdge']), op(nt, 'GREATER_THAN', dr, .12))
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
    for i in range(seg if closed else seg):
        A, B = rings[i], rings[(i + 1) % len(rings)]
        for j in range(rseg): bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    return bm


def between(p0, p1, r0, r1, seg=12, bm=None):
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0
    m = Matrix.Translation((p0 + p1) / 2) @ d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return cone(r0, r1, d.length, m, seg, bm)


def star(r_out, r_in, n=8, m=Matrix(), bm=None, jitter=0.0, seed=0):
    """Düz yıldız/patlama şekli (XY). jitter: kolları eşit olmayan, elle çizilmiş gibi."""
    rng = random.Random(seed); bm = bm or bmesh.new(); c = bm.verts.new(m @ Vector((0, 0, 0))); ring = []
    for i in range(2 * n):
        a = math.pi * i / n + (rng.uniform(-jitter, jitter) * .3 if jitter else 0)
        r = (r_out * (1 + rng.uniform(-jitter, jitter))) if i % 2 == 0 else r_in
        ring.append(bm.verts.new(m @ Vector((math.cos(a) * r, math.sin(a) * r, 0))))
    for i in range(2 * n): bm.faces.new((c, ring[i], ring[(i + 1) % (2 * n)]))
    return bm


def wedge(bm=None):
    """Sivri hız çizgisi: kalın uç -X, sivri uç +X (uzunluk 1, kalınlık 1)."""
    bm = bm or bmesh.new(); v = [bm.verts.new(p) for p in ((-.5, -.5, 0), (-.35, .5, 0), (.5, 0, 0))]; bm.faces.new(v); return bm


def puff(seed, bm=None):
    """Topak duman/toz: üst üste binen 4–5 küre; mürekkep kesişimlerde kabarık hat çizer."""
    r = random.Random(seed); bm = bm or bmesh.new(); sphere(1.0, Matrix(), 16, 8, bm)
    for _ in range(4):
        a, e = r.uniform(0, 2 * math.pi), r.uniform(-.3, .7)
        sphere(r.uniform(.5, .74), Matrix.Translation(Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * r.uniform(.55, .82)), 16, 8, bm)
    return bm


def flame_bm(L, r0, variant, seg=20, rings=12, tongues=3):
    """Elle çizilmiş alev: ağızdan sonra şişen damla, kuyrukta dillere ayrılır; her çeşitte dalga ve sapma farklı.
    Burun -X yönünde uzar. (Önceki koni biçimi yandan düz turuncu şerit gibi okunuyordu.)"""
    bm = bmesh.new(); prev = None; first = None
    for i in range(rings):
        u = i / rings; x = -L * u
        off = Vector((0, .12 * L * u * u * math.sin(variant * 1.7 + u * 4), .09 * L * u * u * math.cos(variant * 2.3 + u * 3)))
        base = r0 * math.sin(math.pi * (.18 + .82 * u)) ** .8
        ring = []
        for j in range(seg):
            th = 2 * math.pi * j / seg
            jag = 1 + .65 * u ** 1.6 * math.cos(tongues * th + variant * 1.3) + .22 * u * math.sin(th * 5 - variant * 2.3 + u * 9)
            jag = max(jag, .08)
            ring.append(bm.verts.new(Vector((x, base * jag * math.cos(th), base * jag * math.sin(th))) + off))
        if prev:
            for j in range(seg): bm.faces.new((prev[j], prev[(j + 1) % seg], ring[(j + 1) % seg], ring[j]))
        else: first = ring
        prev = ring
    tip = bm.verts.new((-L * (1.05 + .12 * math.sin(variant * 2.1)), .12 * L * math.sin(variant * 1.7 + 4), .08 * L * math.cos(variant * 2.3 + 3)))
    for j in range(seg): bm.faces.new((prev[j], prev[(j + 1) % seg], tip))
    bm.faces.new(list(reversed(first)))
    bm.normal_update()
    for fc in bm.faces:
        if fc.normal.dot(fc.calc_center_median() - Vector((-L * .4, 0, 0))) < 0: fc.normal_flip()
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
cam.data.clip_start, cam.data.clip_end = .05, 600; cam.rotation_mode = 'QUATERNION'
world = bpy.data.worlds.new('Previs gök'); s.world = world; world.use_nodes = True
BG = world.node_tree.nodes['Background']
hud = empty('HUD', cam, (0, 0, -1)); stars = empty('Yıldızlar', cam, (0, 0, -500))
bm = bmesh.new()
for _ in range(110):
    ico(RNG.uniform(.0018, .0048), Matrix.Translation((RNG.uniform(-1.05, 1.05), RNG.uniform(-.6, .6), 0)), 1, bm)
star_obj = obj('Yıldız alanı', bm, glow(CREAM), parent=stars, smooth=False, toggle=True)


def hud_pool(prefix, n, color):
    return [obj(f'{prefix} {i}', wedge(), glow(color), parent=hud, smooth=False, toggle=True) for i in range(n)]


SPEED = [(o, RNG.choice((-1, 1)) * RNG.uniform(.3, .56), RNG.uniform(.14, .38), RNG.uniform(.004, .011), RNG.random())
         for o in hud_pool('Hız çizgisi', 24, '#B9B0D6')]
SMEAR = [(o, RNG.uniform(-.56, .56), RNG.uniform(.3, 1.1), RNG.uniform(.004, .016), RNG.random())
         for o in hud_pool('Kamçı çizgisi', 30, '#8F86B8') + hud_pool('Kamçı alev çizgisi', 8, FLAME)]
RADIAL = [(o, i / 30 * 2 * math.pi + RNG.uniform(-.06, .06), RNG.random(), RNG.uniform(.12, .3), RNG.uniform(.005, .012))
          for i, o in enumerate(hud_pool('Uzaklaşma çizgisi', 30, '#C9C0E6'))]
flash_a = obj('Parıltı dış', sphere(1, TRS((0, 0, -.05), scale=(1, 1, .01)), 48, 8), glow('#FFC46B'), parent=hud, smooth=False, toggle=True)
flash_b = obj('Parıltı iç', sphere(1, TRS((0, 0, -.04), scale=(1, 1, .01)), 48, 8), glow('#FFF1C9'), parent=hud, smooth=False, toggle=True)
bm = bmesh.new()
for i in range(20):
    a = i / 20 * 2 * math.pi + RNG.uniform(-.1, .1); r0 = RNG.uniform(.18, .34); ln = RNG.uniform(.45, .95)
    v = Vector((math.cos(a), math.sin(a), 0))
    p0, p1 = v * (r0 + .45), v * (r0 + .45 + ln)
    t = Vector((-v.y, v.x, 0)) * RNG.uniform(.008, .02)
    q = [bm.verts.new(p0 - t), bm.verts.new(p0 + t), bm.verts.new(p1)]; bm.faces.new(q)
impact_lines = obj('Darbe çizgileri', bm, glow(INK), parent=hud, smooth=False, toggle=True)

# ---------------------------------------------------------------- roket (tek model)
SHIP = empty('ROKET'); SHIP.rotation_mode = 'QUATERNION'
obj('Gövde', cone(.45, .45, 2.2, ROT_Z_TO_X, 32), toon(HULL, HULL_SH), .028, SHIP)
obj('Gövde şeridi', cone(.462, .462, .16, Matrix.Translation((.82, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE, NOSE_SH), 0, SHIP)
bm = bmesh.new()
for x in (-.55, .2):
    torus(.452, .012, Matrix.Translation((x, 0, 0)) @ ROT_Z_TO_X, 48, 4, bm)
obj('Sac çizgileri', bm, glow(INK), 0, SHIP, smooth=False)
bm = bmesh.new()
for a in range(0, 360, 30):
    for x in (-.5, .25):
        sphere(.018, Matrix.Translation((x + .03, .455 * math.cos(math.radians(a)), .455 * math.sin(math.radians(a)))), 6, 4, bm)
obj('Perçinler', bm, glow(NOZZLE), 0, SHIP)
nose = obj('Burun', cone(.45, .07, 1.0, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE, NOSE_SH), .028, SHIP, toggle=True)
nose_hot = obj('Burun (ısınmış)', cone(.46, .08, 1.02, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X, 32), toon(HOT, RED_HI, '#FFF6D8'), .028, SHIP, toggle=True)
obj('Anten', between((2.0, 0, .05), (2.45, 0, .18), .012, .008, 6), glow(INK), 0, SHIP)
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
FLAMES = []   # 4 çeşit × (3 katman + kopan kıvılcımlar); ikişer karede sırayla değişir (elle çizilmiş alev döngüsü)
for v in range(4):
    r = random.Random(60 + v); bm = bmesh.new()
    for k in range(3):
        ico(r.uniform(.035, .07), TRS((-1.6 - r.uniform(0, .7), r.uniform(-.28, .28), r.uniform(-.28, .28)), (r.random() * 3,) * 3, (1.7, 1, 1)), 1, bm)
    FLAMES.append([obj(f'Alev {v} dış', flame_bm(1.55, .36, v), glow(FLAME_OUT), .025, flame, toggle=True),
                   # orta katman dışa yakın: dişleri dış katmanı yer yer delip sarı lekeler açar (opak katmanlarda iç renk böyle görünür)
                   obj(f'Alev {v} orta', flame_bm(1.3, .32, v + .5, tongues=2), glow(FLAME), .012, flame, toggle=True),
                   obj(f'Alev {v} iç', flame_bm(.8, .22, v + .25, tongues=2), glow(FLAME_IN), 0, flame, toggle=True),
                   obj(f'Alev {v} kıvılcım', bm, glow(FLAME_IN), .015, flame, smooth=False, toggle=True)])
bm = bmesh.new()
for z in (-.28, .04, .3):
    between((1.7, -.25, z), (-.9, -.3, z * 1.5), .035, .004, 6, bm)
heat = obj('Isı izleri', bm, glow(HOT), 0, SHIP, toggle=True)
port_ring = obj('Lomboz ışık halkası', torus(.3, .025, Matrix(), 32, 4), glow(HOT), 0, SHIP, at=(.35, -.5, .06), toggle=True)
port_ring.rotation_mode = 'XYZ'; port_ring.rotation_euler = (math.pi / 2, 0, 0)


def ship_pt(local): return SHIP.matrix_world @ Vector(local)


def look_q(v, roll=0.0):
    """+X burnu v yönüne çeviren dörtlü; roll burun ekseni çevresinde."""
    return Vector(v).normalized().to_track_quat('X', 'Z') @ Quaternion((1, 0, 0), roll)


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
    rng = random.Random(seed)
    if zone:
        D, ang = Vector(zone[0]).normalized(), zone[1]
        T1 = D.cross(Vector((0, 0, 1))).normalized() if abs(D.z) < .95 else D.cross(Vector((1, 0, 0))).normalized(); T2 = D.cross(T1)
        seeds = []
        while len(seeds) < K:
            u, v = rng.uniform(-ang, ang), rng.uniform(-ang, ang)
            if u * u + v * v < (ang * .85) ** 2: seeds.append((D + T1 * u + T2 * v).normalized())
        base = bmesh.new(); N = 64; grid = {}
        for i in range(N + 1):
            for j in range(N + 1):
                u, v = -ang + 2 * ang * i / N, -ang + 2 * ang * j / N
                grid[i, j] = base.verts.new((D + T1 * u + T2 * v).normalized())
        for i in range(N):
            for j in range(N):
                cu, cv = -ang + 2 * ang * (i + .5) / N, -ang + 2 * ang * (j + .5) / N
                if cu * cu + cv * cv <= ang * ang: base.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
        for v in [v for v in base.verts if not v.link_faces]: base.verts.remove(v)
    else:
        seeds = [Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized() for _ in range(K)]
        base = ico(1.0, sub=sub)
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
        bm.normal_update()   # açık yama: yönü merkezden dışarı zorla
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


def shard_bm(i, lo=.07, hi=.2):
    r = random.Random(100 + i)
    return ico(r.uniform(lo, hi), TRS(rot=(r.random() * 3,) * 3, scale=(r.uniform(1.1, 1.8), 1, r.uniform(.45, .8))), 1)


# ---------------------------------------------------------------- Ç1 — Dünya yakından
R1 = 10.0
D1 = Vector((0, math.sin(.1), math.cos(.1)))
EARTH_M1 = toon(OCEAN, OCEAN_SH, tex=earth_tex(.16), name='Dünya yüzeyi (yakın)')
ZONE_ANG = .24
bm = sphere(R1, u=192, v=96)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().normalized().angle(D1) < ZONE_ANG * .97], context='FACES')
earth = obj('Dünya (delik)', bm, EARTH_M1, .03, toggle=True)
earth_whole = obj('Dünya (bütün)', sphere(R1, u=192, v=96), EARTH_M1, .03, toggle=True)   # ilk çatlağa kadar
obj('Magma (çatlak altı)', sphere(R1 - .02, u=128, v=64), glow(MAGMA), 0)
zone_plates = sphere_plates('Ç1', R1 + .015, 9, 11, EARTH_M1, .45, (0, 0, 0), zone=(D1, ZONE_ANG), ink=.025)
TOGGLE.extend(p['obj'] for p in zone_plates)
shards1 = pool('Ç1 kıymık', 16, shard_bm, toon(LAND, LAND_SH), .02, False)
for i, o in enumerate(shards1):
    if i % 3 == 0: o.data.materials[0] = ROCK_M
burst_star = obj('Ç1 patlama yıldızı', star(1.0, .32, 11, jitter=.35, seed=3), glow(HOT), .0, smooth=False, toggle=True)
burst_core = obj('Ç1 patlama göbeği', sphere(1.0, u=24, v=12), glow('#FFF6D8'), 0, toggle=True)
crack_flash = obj('Ç1 çatlak parlaması', star(1.0, .22, 6, jitter=.3, seed=5), glow(HOT), .0, smooth=False, toggle=True)
fire_head = obj('Ç1 fırlayan şey', sphere(.24, u=16, v=8), glow(FLAME_IN), .04, toggle=True)
fire_tail = obj('Ç1 fırlayan şey izi', cone(.0, .24, 1.0, Matrix.Translation((0, 0, -.5))), glow(FLAME), .035, toggle=True)
fire_tail.rotation_mode = 'QUATERNION'
CAM1 = Vector((.6, -1.7, 11.15))
CRACK_SPARK = [(D1 * R1).lerp(p['cen'], .5) + D1 * .12 for p in zone_plates[:2]]

# ---------------------------------------------------------------- Ç2a — toz bulutundan kameraya fırlayan roket
O2A = Vector((2000, 0, 0)); C2A = O2A + Vector((0, 9, .4))   # bulut yakın: roket kameraya doğru gelir
ASH_M = toon(ASH, ASH_SH)
CLOUD2A = []
rng = random.Random(21)
for i in range(8):
    a = i / 8 * 2 * math.pi
    at = C2A + Vector((math.cos(a) * rng.uniform(1.6, 3.0), rng.uniform(-1.5, 1.0), math.sin(a) * rng.uniform(1.2, 2.2)))
    o = obj(f'Ç2a bulut {i}', puff(30 + i), ASH_M, .05, at=at); o.scale = (rng.uniform(1.3, 2.1),) * 3
    CLOUD2A.append((o, at.copy(), o.scale.copy(), (at - C2A).normalized()))
for i, side in enumerate((-1, 1)):   # ortadaki iki perde topak: roket çıkarken iki yana açılır
    at = C2A + Vector((.7 * side, -1.6, .1 * side)); o = obj(f'Ç2a ön bulut {i}', puff(40 + i), ASH_M, .05, at=at); o.scale = (1.7,) * 3
    CLOUD2A.append((o, at.copy(), o.scale.copy(), Vector((side, -.2, .3 * side)).normalized() * 1.8))
cloud_glow = obj('Ç2a bulut ışığı', sphere(1.0, u=24, v=12), glow(HOT), 0, at=C2A + Vector((0, .8, 0)), toggle=True)
HORIZON2A = C2A + Vector((0, 28, -50))
obj('Ç2a Dünya ufku', sphere(40, u=128, v=64), toon(OCEAN, OCEAN_SH, tex=earth_tex(.05), name='Dünya yüzeyi (Ç2a)'), .1, at=HORIZON2A)
fire_rim = obj('Ç2a ufuk yangını', torus(40.3, .45, Matrix(), 160, 6), glow(MAGMA), 0, at=HORIZON2A)   # kameraya bakan kenarda
fire_rim.rotation_mode = 'QUATERNION'; fire_rim.rotation_quaternion = (O2A + Vector((0, -6.2, 0)) - HORIZON2A).to_track_quat('Z', 'Y')
shards2a = pool('Ç2a kıymık', 12, lambda i: shard_bm(i, .15, .4), toon(RED, RED_SH, RED_HI), .03, False)
SH2A_V = [Vector((RNG.uniform(-4, 4), RNG.uniform(-6, -2), RNG.uniform(-2.5, 3))) for _ in range(12)]

# ---------------------------------------------------------------- Ç2b — rokete yapışık kamera, küçülen Dünya
O2B = Vector((3000, 0, 0)); R2 = 6.0; E2B = O2B
EARTH_M2 = toon(OCEAN, OCEAN_SH, tex=earth_tex(.27), name='Dünya yüzeyi (uzak)')
plates2 = sphere_plates('Ç2b', R2, 16, 5, EARTH_M2, .9, E2B, sub=4, ink=.12)
core2 = obj('Ç2b çekirdek', sphere(R2 * .86, u=48, v=24), toon(RED, RED_SH, RED_HI), .06, at=E2B)
obj('Ç2b çekirdek ışığı', sphere(R2 * .6, u=32, v=16), glow(HOT), 0, at=E2B)
bm = bmesh.new()
for k in range(9):   # parçalı şok halkası: eşit olmayan yaylar, aralarında boşluk
    a0 = k / 9 * 2 * math.pi + RNG.uniform(0, .15); a1 = a0 + 2 * math.pi / 9 * RNG.uniform(.55, .85)
    torus(1.0, RNG.uniform(.03, .055), Matrix(), 10, 6, bm, a0, a1)
shock = obj('Ç2b şok halkası', bm, glow(CREAM), 0, toggle=True); shock.rotation_mode = 'QUATERNION'
DIR2B = Vector((.3, -.35, .89)).normalized()


def d2b(f): return 9.0 * math.exp(1.3 * (f - 96) / FPS)   # Dünya merkezinden uzaklık (üstel: kaçış)


STREAM = []   # yol boyunca duran kıymıklar: roket hızla geçtikçe kameranın yanından geriye akar
rng = random.Random(7)
for i in range(26):
    dd = rng.uniform(11, 150); side = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
    side = (side - DIR2B * side.dot(DIR2B)).normalized() * rng.uniform(1.0, 4.5)
    o = obj(f'Ç2b akan kıymık {i}', shard_bm(i, .15, .55), toon(RED, RED_SH, RED_HI) if i % 2 else ROCK_M, .03, smooth=False, toggle=True)
    STREAM.append((o, E2B + DIR2B * dd + side, Vector((rng.random() * 3, rng.random() * 3, rng.random() * 3))))
# Burnun yakınında, gövdenin yanında, arkaya bakar: önde krem gövde ve lomboz, ortada kanat ve alev, geride küçülen Dünya
CAM2B_LOCAL, TGT2B_LOCAL = Vector((1.15, -1.3, .5)), Vector((-7.0, .2, -1.25))

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
obj('Cam parıltısı', torus(.88, .03, Matrix.Translation(O3 + Vector((0, -.14, 0))) @ FACE, 24, 4, a0=math.radians(100), a1=math.radians(150)), glow('#FFFFFF'), 0, smooth=False)
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


def glove_bm():
    """Eldiven: işaret parmağı uzanmış (uç orijinde, +X boyunca ele doğru), öteki parmaklar kıvrık, başparmak."""
    bm = bmesh.new()
    sphere(.13, Matrix(), 16, 8, bm)
    for (a, b, r0, r1) in (((0, 0, 0), (.4, 0, 0), .13, .14), ((.4, 0, 0), (.8, 0, .02), .14, .15), ((.8, 0, .02), (1.18, 0, .05), .15, .16)):
        between(a, b, r0, r1, 14, bm); sphere(r1 * 1.02, Matrix.Translation(b), 14, 7, bm)
    sphere(1.0, TRS((1.55, .05, -.05), scale=(.5, .38, .26)), 20, 10, bm)
    for k in range(3):
        y = .14 - k * .17
        between((1.3, y, -.12), (1.12, y, -.34), .12, .11, 12, bm); sphere(.115, Matrix.Translation((1.12, y, -.34)), 12, 6, bm)
    between((1.35, -.3, .05), (1.0, -.42, .12), .13, .12, 12, bm); sphere(.125, Matrix.Translation((1.0, -.42, .12)), 12, 6, bm)
    return bm


finger = obj('Eldiven', glove_bm(), toon(GLOVE, GLOVE_SH), .035, toggle=True)
cuff = obj('Eldiven bileği', cone(.36, .4, .5, Matrix.Translation((2.1, .05, -.05)) @ ROT_Z_TO_X, 20), toon(CUFF, NOZZLE_SH), .035, finger, toggle=True)
finger.rotation_mode = 'QUATERNION'
FINGER_Q = Vector((1.0, -.5, -1.0)).normalized().to_track_quat('X', 'Z')
bm = bmesh.new()
for k in range(3):
    a = math.radians(95 + k * 38); d = Vector((math.cos(a), 0, math.sin(a)))
    between(d * .26, d * .44, .017, .017, 6, bm)
taps = obj('Vuruş izleri', bm, glow(INK), 0, toggle=True)

# ---------------------------------------------------------------- Ç3b — motor söner
O4 = Vector((6000, 0, 0))
puffs3 = pool('Ç3b duman', 4, lambda i: puff(50 + i), toon(SMOKE, SMOKE_SH), .03)

# ---------------------------------------------------------------- Ç4 — yeni gezegen
O5 = Vector((8000, 0, 0)); R5 = 16.0; PL = O5 + Vector((14, 34, -24))
obj('Eri\'nin gezegeni', sphere(R5, u=128, v=64), toon(RUST, RUST_SH, RUST_HI, tex=rust_tex(.12), rim=1.4, name='Gezegen'), .12, at=PL)
glint = obj('İpucu parıltısı', star(.9, .12, 4), glow(GLINT), 0, smooth=False, toggle=True)
glint.location = PL + Vector((-.3, -.4, .87)).normalized() * (R5 + .05); glint.rotation_euler = (math.pi / 2, 0, 0)
dashes = pool('Ç4 yay', 60, lambda i: cone(.06, .06, 1.0, ROT_Z_TO_X, 8), glow(CREAM), 0)
for d in dashes: d.rotation_mode = 'QUATERNION'
FAR_EARTH = O5 + Vector((-46, 70, 14))   # uzakta hâlâ yanan Dünya kalıntısı: ne kadar uzaklaştıklarını söyler
obj('Ç4 uzak Dünya ışığı', sphere(1.1, u=16, v=8), glow(MAGMA), .08, at=FAR_EARTH)
bm = bmesh.new()
for i in range(9):
    a = i / 9 * 2 * math.pi; ico(RNG.uniform(.2, .45), Matrix.Translation(FAR_EARTH + Vector((math.cos(a) * 2.2, RNG.uniform(-.5, .5), math.sin(a) * 1.6))), 1, bm)
obj('Ç4 uzak Dünya parçaları', bm, toon(RED, RED_SH), .04, smooth=False)

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
wipe = [obj(f'Bulut perdesi {i}', cloud_bm(random.Random(9 + i), 1.0), CLOUD_M, .04, toggle=True) for i in range(6)]   # iki sıra topak
smoke5 = pool('Ç5 duman', 12, lambda i: puff(70 + i), toon(SMOKE, SMOKE_SH), .025)

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


def leaf_bm(L, w, bend):
    bm = sphere(1.0, u=32, v=12)
    for v in bm.verts:
        t = (v.co.x + 1) / 2; taper = math.sin(math.pi * clamp(t)) ** .75
        v.co = Vector((v.co.x * L, v.co.y * .05, v.co.z * w * taper + bend * L * t * t))
    return bm


LEAF_M = toon(LEAF_FG, '#0C0D18')
LEAVES = []
for name, at, rot in (('sol', (-3.3, -6.9, -.45), (0, -.5, .25)), ('sol 2', (-3.0, -7.15, -.6), (0, -.15, .5)), ('sağ', (3.3, -6.9, -.5), (0, -.5, math.pi - .25))):
    o = obj(f'Ön yaprak {name}', leaf_bm(1.5 if name != 'sol 2' else 1.1, .32, .35), LEAF_M, .035, at=O7 + Vector(at)); o.rotation_euler = rot
    LEAVES.append((o, Euler(rot)))
bm = bmesh.new()
for i in range(9):
    a = i / 9 * 2 * math.pi + .3
    box((RNG.uniform(.8, 1.6), .05, .02), TRS(LAND + Vector((math.cos(a), math.sin(a), 0)) * 1.0 + Vector((0, 0, .03)), (0, 0, a)), bm)
ground_cracks = obj('Zemin çatlakları', bm, glow(INK), 0, smooth=False, toggle=True)
skid = obj('Kayma izi', box((3.0, .8, .05), TRS(LAND + Vector((-.9, .2, .02)), (0, 0, .05))), glow('#4A1E18'), 0, smooth=False, toggle=True)
smoke6 = pool('Ç6 duman', 10, lambda i: puff(90 + i), toon(SMOKE, SMOKE_SH), .025)
dust6 = pool('Ç6 toz', 16, lambda i: puff(110 + i), toon(DUST, DUST_SH), .03)
rocks6 = pool('Ç6 taş', 10, lambda i: shard_bm(i, .06, .13), toon(RUST_HI, RUST_SH), .015, False)
DUST = [(Vector((math.cos(a), .55 * math.sin(a), 0)) * RNG.uniform(.6, 1.3), RNG.uniform(.3, 2.2), RNG.uniform(.55, 1.1))
        for a in (i / 16 * 2 * math.pi + RNG.uniform(-.2, .2) for i in range(16))]
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


def light(f, key, rim, rim_col, rim_amt, tint=(1, 1, 1), impact=0.0, sky=SPACE, edge=.56):
    """edge: kenar ışığının başladığı yan bakış eşiği; kamera gövdeyi yandan sıyırarak görürken yükselir."""
    for i in range(3): L_KEY.inputs[i].default_value = key[i]; L_RIM.inputs[i].default_value = rim[i]
    L_RIMC.outputs[0].default_value = C(rim_col); L_RIMA.outputs[0].default_value = rim_amt; L_RIME.outputs[0].default_value = edge
    L_TINT.outputs[0].default_value = (*tint, 1); L_IMP.outputs[0].default_value = impact
    BG.inputs['Color'].default_value = C(CREAM if impact > .5 else sky)
    for n in (L_KEY, L_RIM):
        for i in range(3): n.inputs[i].keyframe_insert('default_value', frame=f + 1)
    for n in (L_RIMC, L_RIMA, L_RIME, L_TINT, L_IMP): n.outputs[0].keyframe_insert('default_value', frame=f + 1)
    BG.inputs['Color'].keyframe_insert('default_value', frame=f + 1)


# Vurgular: (kare, derece). Kamera her karede; sarsıntı açısal (mesafeden bağımsız).
IMPULSES = [(14, 1.2), (28, 2.0), (42, 3.8), (70, 1.4), (90, 2.4), (96, 1.0), (104, 1.2), (146, 4.2), (148, 2.2),
            (166, .45), (172, .45), (186, .4), (190, .7), (198, .8), (232, .5), (274, 1.8), (IMPACT, 5.0)]


def shake(f, hand, vib):
    """Sarsıntı bir anlatım aracı. İki ayrı bileşen (ölçümle ayrıldı: tek frekansta yakın nesne karede ±40 px zıplıyordu):
    darbe = yüksek frekans, hızla söner; uğultu = yavaş dalgalanan gürültü, çekimin kendi eğrisiyle büyür/söner."""
    hit = 0.0
    for f0, amp in IMPULSES:
        d = f - f0
        if 0 <= d < 30: hit += amp * math.exp(-d / 3.2)
    rum = 0.0
    if 30 <= f < 40: rum += 1.0 * ease_in((f - 30) / 10)              # Ç1: patlamadan önce titreme büyür
    if f in (40, 41): hit *= .1; rum = 0                              # ...ve bir an susar
    if 96 <= f < 148:                                                 # Ç2b: Dünya uzaklaştıkça uğultu söner,
        rum += 1.0 * math.exp(-(f - 96) / FPS * 1.1)                  #       şok dalgası yaklaşırken yeniden kabarır
        rum += 1.8 * ease_in((f - 128) / 18) if f >= 128 else 0
    if 282 <= f < 306: rum += .6 + .8 * (f - 282) / 24                # Ç5: atmosfer
    if 306 <= f < IMPACT: rum += .8 + 1.0 * ease_in((f - 306) / 10)   # Ç6: yere yaklaşırken artar
    hr, rr, vr, ar = math.radians(hand), math.radians(rum), math.radians(vib), math.radians(hit)
    n = lambda k, sp: mnoise.noise(Vector((f * sp, k, k * 1.7)))
    yaw = ar * math.sin(f * 2.9 + 1) + rr * n(1, .42) + hr * n(3, .13) + vr * math.sin(f * 1.7)
    pitch = ar * math.cos(f * 3.4) + rr * n(5, .42) + hr * n(7, .13) + vr * math.cos(f * 2.3 + .6)
    return yaw, pitch, ar * .7 * math.sin(f * 2.2 + .4) + rr * .5 * n(9, .3)


def camera(f, loc, target, lens, roll=0.0, hand=.25, vib=0.0, q=None):
    if q is None: q = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y') @ Quaternion((0, 0, 1), roll)
    y, p, r = shake(f, hand, vib)
    q = q @ Quaternion((0, 1, 0), y) @ Quaternion((1, 0, 0), p) @ Quaternion((0, 0, 1), r)
    cam.location, cam.rotation_quaternion, cam.data.lens = loc, q, lens
    cam.keyframe_insert('location', frame=f + 1); cam.keyframe_insert('rotation_quaternion', frame=f + 1)
    cam.data.keyframe_insert('lens', frame=f + 1)
    k = 18 / lens; hud.scale = (k, k, k); stars.scale = (500 * k,) * 3
    hud.keyframe_insert('scale', frame=f + 1); stars.keyframe_insert('scale', frame=f + 1)
    bpy.context.view_layer.update()


def screen(p):
    """Dünya noktasının HUD koordinatı."""
    v = world_to_camera_view(s, cam, Vector(p)); return Vector(((v.x - .5) * 2, (v.y - .5) * 1.125))


def ship(f, at, q, scale=1.0, thrust=0.0, hot=False, lit=False, stretch=1.0, force=True):
    """Roket kahraman nesne: her karede (kamera onu izliyor; ikişer karede ölçümde ±10–40 px titriyordu).
    İkincil nesneler (kıymık, plaka, duman, bulut) ikişer karede kalır."""
    SHIP.location, SHIP.rotation_quaternion = at, q
    side = scale / math.sqrt(stretch); SHIP.scale = (scale * stretch, side, side)
    if thrust > .01:
        fl = thrust * (1 + .08 * math.sin(f * 2.3))
        flame.scale = (fl, min(1.2, .55 + .55 * thrust), min(1.2, .55 + .55 * thrust)); on(*FLAMES[(f // 2) % 4])
    on(nose_hot if hot else nose); on(glass_lit if lit else glass)
    if hot: on(heat)
    K(SHIP, f, force); K(flame, f, force)
    bpy.context.view_layer.update()


def speed_lines(f, angle, amount=1.0):
    for o, y, ln, th, ph in SPEED:
        u = ((f * .09 * (1 + ph)) + ph) % 1
        p = Vector((1.3 - 2.6 * u, y, 0)); c, sn = math.cos(angle), math.sin(angle)
        o.location = (p.x * c - p.y * sn, p.x * sn + p.y * c, 0); o.rotation_euler = (0, 0, angle + math.pi)
        o.scale = (ln * amount, th, 1); K(o, f, True); on(o)


def radial_lines(f, center, speed=1.0, amount=1.0):
    """Kaçış noktasından dışarı akan çizgiler (Dünya'dan uzaklaşma)."""
    for o, a, ph, ln, th in RADIAL:
        u = (ph + f * .045 * speed) % 1; r = .22 + u * 1.25
        d = Vector((math.cos(a), math.sin(a), 0))
        o.location = (center.x + d.x * r, center.y + d.y * r, 0); o.rotation_euler = (0, 0, a + math.pi)
        o.scale = (ln * amount * (.5 + u), th * (.6 + u), 1); K(o, f, True); on(o)


def puff_life(o, f, born, at, drift, r0, r1, life, squash=1.0):
    a = (f - born) / FPS
    if a < 0 or a > life: return
    o.location = at + drift * a; sc = r0 + (r1 - r0) * ease_out(a / (life * .55)); sc *= 1 - ease_in((a - life * .7) / (life * .3))
    o.scale = (sc, sc, sc * squash); o.rotation_euler = (a * 1.5 + born, a * .7 + born * .3, 0); K(o, f); on(o)


# ---------------- Ç1: yakından yok oluş, hazırlık, patlama, bir şey fırlar
def shot1(f):
    zc = D1 * R1
    if f < 14: on(earth_whole)
    else: on(earth, *(p['obj'] for p in zone_plates))
    sep = .13 * ease((f - 14) / 2) if f >= 14 else 0
    lift2 = ease((f - 28) / 5) if f >= 28 else 0
    sink = -.06 * ease((f - 36) / 5) if 36 <= f < 42 else 0         # hazırlık: plakalar içe çöker, titrer
    lift3 = ease_out((f - 42) / 15) if f >= 42 else 0
    for k, p in enumerate(zone_plates):
        tang = (p['cen'] - zc); tang = tang - p['n'] * tang.dot(p['n'])
        tang = tang.normalized() if tang.length > 1e-6 else Vector((1, 0, 0))
        w = clamp(1 - (p['seed'].angle(D1) / ZONE_ANG), .15, 1)
        away = .2 if tang.dot((CAM1 - zc).normalized()) > .2 else 1.0
        tremble = .018 * math.sin(f * 5 + k * 2) if 34 <= f < 42 else 0
        off = p['n'] * (sep * .3 + lift2 * .35 * w + sink + tremble + lift3 * (1.4 * w + .3) * away) + tang * (sep + lift2 * .12 + lift3 * 2.6 * w * away)
        p['pivot'].location = p['cen'] + off
        p['pivot'].rotation_euler = Quaternion(p['n'].cross(tang).normalized(), (lift2 * .28 + lift3 * 1.1 * away) * w).to_euler()
        K(p['pivot'], f)
    for when, scale, at in ((14, .32, CRACK_SPARK[0]), (28, .5, CRACK_SPARK[1])):
        if when <= f < when + 3:
            crack_flash.location = at
            crack_flash.rotation_euler = ((CAM1 - at).to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), when)).to_euler()
            crack_flash.scale = (scale * (1 - (f - when) / 3) + .05,) * 3; K(crack_flash, f, True); on(crack_flash)
    if 42 <= f < 52:
        a = (f - 42) / 10
        burst_core.location = zc + D1 * .5; burst_core.scale = (.2 + .45 * ease_out(a * 2) * (1 - ease_in((a - .5) * 2)),) * 3
        burst_star.location = zc + D1 * .6
        burst_star.rotation_euler = ((CAM1 - burst_star.location).to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), f * .12)).to_euler()
        burst_star.scale = (.4 + 1.3 * ease_out(a * 1.6) * (1 - ease_in((a - .6) / .4)),) * 3
        K(burst_core, f, True); K(burst_star, f, True); on(burst_core, burst_star)
    rng = random.Random(1)
    for i, o in enumerate(shards1):
        born = 28 if i < 6 else 42
        v = D1 * rng.uniform(2, 5) + Vector((rng.choice((-1, 1)) * rng.uniform(1.5, 4), rng.uniform(-3.5, -1), rng.uniform(0, 2)))
        start = zc + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0))
        if f >= born:
            a = (f - born) / FPS
            o.location = start + v * a; o.rotation_euler = (a * 6 + i, a * 4, a * 3 + i * .5); K(o, f); on(o)
    if f >= 44:
        a = (f - 44) / FPS; vel = Vector((3.2, -.5, 3.8))
        head = zc + D1 * .4 + vel * a * (1 + 4 * a)
        fire_head.location = head; fire_tail.location = head
        fire_tail.rotation_quaternion = vel.normalized().to_track_quat('Z', 'Y'); fire_tail.scale = (1, 1, 1.2 + 5 * a)
        K(fire_head, f, True); K(fire_tail, f, True); on(fire_head, fire_tail)
    # kamera: yavaş ileri, hazırlıkta "nefes alır", patlama geri ve yukarı iter, sonra kamçı
    loc = CAM1 + Vector((0, .45, -.12)) * ease(min(f, 35) / 50) + Vector((0, .18, -.06)) * (ease((f - 35) / 6) if f < 42 else 0)
    loc = loc + Vector((0, -3.0, 1.6)) * ease_out((f - 41) / 9)
    tgt = zc + Vector((.1, .4, .15))
    if f >= 45: tgt = tgt.lerp(fire_head.location, .55 * ease((f - 45) / 6))
    roll = .06 * ease((f - 14) / 26)
    if f >= 52:   # kamçı alev topuna kilitlenir (öne geçmez): top ekranın ortasında kalır, dünya bulanıklaşır
        w = ease_in((f - 52) / 8)
        tgt = tgt.lerp(fire_head.location + Vector((3.2, -.5, 3.8)).normalized() * .4, w); roll += .25 * w
        if f >= 54:
            for o, y, ln, th, ph in SMEAR:
                o.location = (((ph + f * .37) % 2.2) - 1.1, y, 0); o.rotation_euler = (0, 0, math.pi)
                o.scale = (ln, th, 1); K(o, f, True); on(o)
    camera(f, loc, tgt, 22, roll, .2)
    tint = (1, .93, .86) if 42 <= f < 50 else ((.86, .86, .95) if 38 <= f < 42 else (1, 1, 1))   # hazırlıkta bir an soğur
    light(f, (-.55, -.45, .7), (0, 1, .35), HOT if f >= 14 else CREAM, 1.0 if f >= 14 else .8, tint)
    on(star_obj)


# ---------------- Ç2a: toz bulutunun içinden kameraya fırlar — "o şey roketmiş"
P2A0, P2A1 = C2A + Vector((-.5, -.4, -.55)), O2A + Vector((2.3, -5.1, 1.5))
CAM2A = O2A + Vector((0, -6.2, 0))


def s2a(f):
    """Ç2a yol parametresi: kesmede sıfır değil, Ç1'den taşınan hızla başlar ve hızlanır (hız eğrisi kopmaz)."""
    u = clamp((f - 60) / 34); return .1 + .36 * u + .54 * u * u


def shot2a(f):
    bulge = ease_out((f - 60) / 6)   # roket kesmeyle birlikte bulutu deler (eylem eşlemesi)
    for o, at, sc, out in CLOUD2A:
        o.location = at + out * (.9 * bulge + .25 * (f - 60) / 36); o.scale = sc * (1 + .25 * bulge); K(o, f)
    cloud_glow.scale = (1.6 + .6 * bulge + .15 * math.sin(f * .9),) * 3; K(cloud_glow, f); on(cloud_glow)
    for i, (o, v) in enumerate(zip(shards2a, SH2A_V)):
        a = (f - 60) / FPS; o.location = C2A + v * a * 1.6; o.rotation_euler = (a * 5 + i, a * 3, i); K(o, f); on(o)
    pos = P2A0.lerp(P2A1, s2a(f))
    dist = (pos - CAM2A).length
    stretch = 1.7 if dist < 3.6 else (1.25 if dist < 6 else 1.0)   # yakından geçerken uzar (smear)
    ship(f, pos, look_q(P2A1 - P2A0, .9 * s2a(f)), 1.0, 1.0, stretch=stretch)
    u = (f - 60) / 36; follow = ease((f - 80) / 14)
    # kamera roketi izler (Ç1'deki kilit sürer); geçişe doğru biraz önüne bakar. Tek formül: hedef sıçramaz
    tgt = (C2A + Vector((0, 0, -.6))).lerp(pos + Vector((0, 0, -.2)) + Vector((.8, 0, .5)) * follow, .65 + .1 * follow)
    if f < 66:   # kamçının devamı: Ç1'deki dönüş burada yavaşlayarak oturur, bulanıklık söner
        tgt = tgt + Vector((-1.6, 0, -1.0)) * (1 - ease_out((f - 60) / 6))
        for o, y, ln, th, ph in SMEAR[:20]:
            o.location = (((ph + f * .37) % 2.2) - 1.1, y, 0); o.rotation_euler = (0, 0, math.pi)
            o.scale = (ln * (1 - (f - 60) / 6), th, 1); K(o, f, True); on(o)
    camera(f, CAM2A + Vector((0, .6, 0)) * ease(u), tgt, 28, -.05 + .12 * follow, .3)
    if f >= 84: speed_lines(f, .35, 1.2)
    light(f, (-.45, -.6, .6), (0, .35, -1), RED_HI, 1.0)   # bulutun alt kenarı aşağıdaki yangından turuncu
    on(star_obj)


# ---------------- Ç2b: rokete yapışık kamera — Dünya arkada patlayarak küçülür
def shot2b(f):
    t = (f - 96) / FPS
    for p in plates2:
        dd = .4 + 9.0 * ease_out(t / 2.2)
        p['pivot'].location = p['cen'] + p['n'] * dd * p['w']
        p['pivot'].rotation_euler = Quaternion(p['axis'], .8 * t * p['w']).to_euler(); K(p['pivot'], f)
    core2.scale = (1 + .1 * t,) * 3; K(core2, f)
    pos = E2B + DIR2B * d2b(f)
    q = look_q(DIR2B, .35 * t)                         # roket yavaşça kendi çevresinde döner: dünya kadrajda döner
    ship(f, pos, q, 1.0, 1.0, force=True)
    if 104 <= f <= 147:   # şok dalgası: Dünya'dan çıkar, rokete arkadan yetişir (f146)
        w = (d2b(146) + 1.5) * ease_in((f - 104) / 42)
        shock.location = E2B + DIR2B * w; shock.rotation_quaternion = DIR2B.to_track_quat('Z', 'Y')
        shock.scale = (6 + .55 * w,) * 3; K(shock, f, True); on(shock)
    for o, at, spin in STREAM:
        if (at - pos).length < 60:
            o.location = at; o.rotation_euler = Euler(spin * t); K(o, f); on(o)
    cq = SHIP.matrix_world.to_quaternion() @ (TGT2B_LOCAL - CAM2B_LOCAL).to_track_quat('-Z', 'Y')
    camera(f, ship_pt(CAM2B_LOCAL), None, 24, hand=.15, vib=.12, q=cq)
    c = screen(E2B)
    radial_lines(f, c, 1.0 + 1.5 * t, .6 + .4 * clamp(t))
    if f == 146: flash_b.location = (0, 0, 0); flash_b.scale = (2.4,) * 3; K(flash_b, f, True); on(flash_b)
    light(f, (-.3, -.6, .75), tuple(-DIR2B), RED_HI, clamp(1.3 - .45 * t, .2, 1.0), (1, .96 - .02 * t, .92 + .03 * t), edge=.86)
    on(star_obj)


# ---------------- Ç3a: sarsıntı içeride sürer, ibre düşer, parmak vurur
def shot3a(f):
    if f < 150: deg = 100 + 4 * math.sin(f * 3)
    else: deg = {150: 122, 152: 150, 154: 163, 156: 147, 158: 154}.get(f - f % 2, 152)
    needle.rotation_euler = (0, -math.radians(deg), 0); K(needle, f)
    if 150 <= f < 156: on(needle_arcs)
    lamp = f >= 166
    on(lamp_on if lamp else lamp_off)
    for t0 in (166, 172):
        if t0 <= f < t0 + 6:
            lamp_ring.scale = (.25 + .5 * ease_out((f - t0) / 6),) * 3; K(lamp_ring, f, True); on(lamp_ring)
    tip_far, tip_hover, tip_back, tip_tap = (O3 + Vector((2.8, -1.6, -2.0)), O3 + Vector((.62, -.55, .16)),
                                             O3 + Vector((.8, -.8, .1)), O3 + Vector((.42, -.28, .18)))
    if f >= 158:
        squash = 1.0
        if f < 162: tip = tip_far.lerp(tip_hover, ease_out((f - 158) / 4))
        elif f in (164, 165, 170, 171): tip = tip_back                  # hazırlık: parmak geri çekilir
        elif f in (166, 167, 172, 173): tip = tip_tap; squash = .9       # vuruş: ezilir
        else: tip = tip_hover
        finger.location = tip; finger.rotation_quaternion = FINGER_Q; finger.scale = (squash, 1 / math.sqrt(squash), 1 / math.sqrt(squash))
        K(finger, f); on(finger, cuff)
        if f in (166, 167, 172, 173): taps.location = tip_tap + Vector((0, -.05, 0)); K(taps, f, True); on(taps)
    pulse = f in (166, 167, 172, 173)
    u = (f - 148) / 30
    camera(f, O3 + Vector((-1.3, -4.7 + .45 * ease(u), -.75)), O3 + Vector((.3, 0, .05)), 40, -.09, .45)
    light(f, (-.5, -.6, .6), (.75, -.25, .6), LAMP_ON, 1.0 if lamp else 0, (1, .74, .7) if pulse else (1, 1, 1), sky=PANEL_BG)


# ---------------- Ç3b: arka üç çeyrek, alev öksürür ve söner, soğuk sessizlik
def flame_level(f):
    if f < 186: return 1.0
    if f >= 198: return 0.0
    return {186: .25, 188: .3, 190: 1.2, 192: .2, 194: .35, 196: .08}.get(f - f % 2, 0.0)


def shot3b(f):
    u = (f - 178) / 32; fl = flame_level(f)
    pos = O4 + Vector((1.0 * u, 0, .35 * u))
    roll = .3 * ease((f - 198) / 12) if f >= 198 else 0
    ship(f, pos, Euler((roll, -.35, -.3)).to_quaternion(), 1.0, fl)
    back = (ship_pt((-1, 0, 0)) - ship_pt((0, 0, 0))).normalized()
    for o, born, r1 in zip(puffs3, (187, 191, 195, 198), (.32, .28, .22, .15)):
        puff_life(o, f, born, ship_pt((-1.9, 0, .1)), back * 1.8 + Vector((0, .2, .25)), .08, r1, 1.1)
    off = Vector((-3.6, -3.4, -1.6)) * (1 - .1 * ease(u))
    camera(f, pos + off, pos + Vector((-.6, 0, .25)), 30, .04 - .08 * ease(u), .3, .1 if f < 198 else 0)
    cold = f >= 198
    light(f, (-.4, -.7, .6), (-1, .2, -.3), FLAME, min(1, fl), (.82, .86, 1.0) if cold else (1, 1, 1), sky=SPACE_COLD if cold else SPACE)
    on(star_obj)


# ---------------- Ç4: sessiz süzülüş, uzakta yanan Dünya, yakalanma, giriş
# P_START, Ç3b'nin son karesinde geminin ekrandaki yeri (~557, 332 px): kesmede konum eşlemesi (ölçüldü)
P_START, P_BEND, P_END = O5 + Vector((1.45, 0, -.72)), O5 + Vector((2.45, 0, -.66)), O5 + Vector((9.0, 4, -9.0))
PATH4 = [P_START] + bezier(P_BEND, (1, 0, .05), P_END, 60)
CAM4_C1, CAM4_T1 = O5 + Vector((.2, -13.4, 1.6)), O5 + Vector((.2, 0, .3))   # f232'deki (yakalanma başı) kadraj


def screen_px(P, Cm, T, lens):
    """Basit iğne deliği izdüşümü: kadraj merkezine göre piksel (y aşağı)."""
    d = (T - Cm).normalized(); r = d.cross(Vector((0, 0, 1))).normalized(); up = r.cross(d)
    rel = Vector(P) - Cm; depth = rel.dot(d); F = lens / 36 * 960
    return Vector((rel.dot(r) / depth * F, -rel.dot(up) / depth * F))


# Süzülüş: kesmede gemi Ç3b'nin son ekran noktasında (merkezden ~+80, +61 px), uzun lensle büyük; lens açıldıkça
# boşluk ve gezegen görünür, gemi yerini yakalanma kadrajındaki noktaya bırakır (ekranda kaymaz, f232'de sıçramaz).
P4_SCREEN0, P4_SCREEN1 = Vector((80, 61)), screen_px(P_BEND, CAM4_C1, CAM4_T1, 30)
LEN4 = [0.0]
for a_, b_ in zip(PATH4, PATH4[1:]): LEN4.append(LEN4[-1] + (b_ - a_).length)
DASH_STEP = .3
TRAIL4 = resample(PATH4, DASH_STEP)


def rocket4(f):
    if f < 232:
        u = (f - 210) / 22; return P_START.lerp(P_BEND, u), Vector((1, 0, .05)), LEN4[1] * u
    s4 = ease_in((f - 232) / 49) * 60; i = min(int(s4), 59); w = s4 - i
    p = PATH4[1 + i].lerp(PATH4[2 + i], w)
    return p, (PATH4[2 + i] - PATH4[1 + i]), LEN4[1 + i] + w * (LEN4[2 + i] - LEN4[1 + i])


def shot4(f):
    pos, vel, dist = rocket4(f)
    turn = ease((f - 232) / 14)
    drift = Euler((.2 * math.sin(f * .04), .08 * math.sin(f * .03), .1)).to_quaternion()   # itkisiz, hafif yalpa
    ship(f, pos, drift.slerp(look_q(vel), turn), .42, 0, hot=f >= 250)
    behind = dist - .75
    for i, d in enumerate(dashes):
        j = 2 * i
        if j + 1 >= len(TRAIL4) or (j + 1) * DASH_STEP > behind: break
        a, b = TRAIL4[j], TRAIL4[j + 1]
        d.location = (a + b) / 2; d.rotation_quaternion = (b - a).normalized().to_track_quat('X', 'Z')
        d.scale = ((b - a).length, 1, 1); K(d, f); on(d)
    if f >= 262: speed_lines(f, -.95, .5 + .5 * ease((f - 262) / 10))
    if f >= 274:
        a = (f - 274) / 8
        for o, k in ((flash_a, 1.0), (flash_b, .62)):
            o.location = (.1, -.1, 0); o.scale = (k * (.15 + 2.6 * ease_in(a + .15)),) * 3; K(o, f, True); on(o)
    u = (f - 210) / 22; w = ease((f - 232) / 30)
    loc = O5 + Vector((.2, -14 + .6 * ease(u), 1.6)).lerp(Vector((3.6, -12.0, -2.2)), w)
    if f < 232:   # lens açılımı 70 → 30 mm; gemi istenen ekran noktasında tutulur
        k = ease(u); lens = 70 - 40 * k; p = P4_SCREEN0.lerp(P4_SCREEN1, k)
        F, depth = lens / 36 * 960, (pos - loc).y
        tgt = pos + Vector((-p.x * depth / F, 0, p.y * depth / F))
        camera(f, loc, tgt, lens, 0, .15)
    else:
        tgt = CAM4_T1.lerp(pos + Vector((1.2, 0, -1.4)), w)
        camera(f, loc, tgt, 30 - 4 * w, -.06 * w, .2)
    light(f, (-.6, -.5, .6), (.3, 1, .2), '#FFD9A8', .9, (.9, .93, 1.0) if f < 232 else (1, 1, 1))
    on(star_obj)


# ---------------- Ç5: bulutların içinden dalış
def shot5(f):
    a = (f - 282) / FPS
    for o, base, spd in clouds:
        o.location = base + Vector((0, 0, spd * a)); K(o, f)
    pos = O6 + Vector((.3 + .8 * a, 0, .6 - .6 * a)) + Vector((3.8, 0, -3.2)) * ease_in((f - 298) / 7)
    ship(f, pos, Euler((.08 * math.sin(f * .6), .85, -.3)).to_quaternion(), .9, 0, hot=f < 292)
    for k, o in enumerate(smoke5):   # her duman doğduğu yerde kalır; kamera düştüğü için yukarı akar
        born = 282 + 2 * k
        if f >= born:
            pb = O6 + Vector((.3 + .8 * (born - 282) / FPS, 0, .6 - .6 * (born - 282) / FPS))
            puff_life(o, f, born, pb + Vector((-1.4, 0, .9)) + Vector((0, 0, 12 * (f - born) / FPS)), Vector(), .2, .55, 1.2)
    if f >= 298:   # perde: önde üstü, arkada altı kapatan iki sıra topak, sırayla yükselir (kabarık kenar okunur)
        for i, o in enumerate(wipe):
            row, col = divmod(i, 3)
            w = ease_out((f - 298 - row - .5 * col) / 6)
            end_z = (.9, -1.0)[row] + .25 * (col - 1)
            o.location = O6 + Vector((-3.3 + 3.3 * col + .9 * row, (-3.6, -2.4)[row], end_z - 6.5 * (1 - w)))
            o.scale = (1.5, 1, 1.5); K(o, f, True); on(o)
    camera(f, O6 + Vector((0, -10, 0)), O6 + Vector((.6, 0, -.2)), 32, .05 * math.sin(f * .3), .5)
    speed_lines(f, math.pi / 2, .8)
    light(f, (-.5, -.6, .7), (.5, -.2, -.8), '#FFB07A', .8, sky=SKY5)


# ---------------- Ç6: çakılır, kayar, toz dağılır, ışık yanar — sağ kaldılar
def rocket6(f):
    if f < IMPACT:   # Ç5'teki hızla girer (sıfırdan başlamaz), yere doğru hızlanır
        x = clamp((f - 306) / (IMPACT - 306)); u = .5 * x + .5 * x * x
        return (O7 + Vector((-7.5, 3.5, 6.5))).lerp(LAND + Vector((-.4, 0, .5)), u), Euler((0, .62, -.25))
    u = ease_out((f - IMPACT - 2) / 16) if f >= IMPACT + 2 else 0
    bounce = .16 * math.sin(clamp((f - IMPACT - 4) / 6) * math.pi) if f >= IMPACT + 4 else 0
    pos = (LAND + Vector((-.4, 0, .5))).lerp(LAND + Vector((.5, 0, .58)), u) + Vector((0, 0, bounce))
    return pos, Euler((.12 * math.exp(-(f - IMPACT) / 8) * math.sin(f), .62 - .32 * u, -.25))


def shot6(f):
    for o, base in LEAVES:   # yapraklar hafifçe sallanır; darbe rüzgârıyla bir kez savrulur
        gust = .12 * math.exp(-(f - IMPACT) / 6) * math.sin(f * 1.3) if f >= IMPACT else 0
        o.rotation_euler = (base.x + .03 * math.sin(f * .2) + gust, base.y, base.z); K(o, f)
    pos, rot = rocket6(f)
    ship(f, pos, rot.to_quaternion(), .8, 0, lit=f >= 338)
    for k, o in enumerate(smoke6):
        born = 306 + k; pb, _ = rocket6(born)
        puff_life(o, f, born, pb + Vector((-1.4, .4, 1.0)), Vector((0, 0, .3)), .2, .45, 1.1)
    if f >= IMPACT:
        on(ground_cracks)
        if f >= IMPACT + 4: on(skid)
        a = (f - IMPACT) / FPS
        for o, (dirv, rise, size) in zip(dust6, DUST):
            if a > 1.05: continue
            sc = size * (.2 + .65 * ease_out(a / .3)) * (1 - ease_in((a - .55) / .5))
            o.location = LAND + dirv * 2.2 * (1 - math.exp(-a * 5)) + Vector((0, 0, .3 + rise * (1 - math.exp(-a * 3.5))))
            o.scale = (sc,) * 3; o.rotation_euler = (a, a * .5, 0); K(o, f); on(o)
        for o, v in zip(rocks6, ROCK_V):
            p = LAND + Vector((0, 0, .4)) + v * a + Vector((0, 0, -6.5 * a * a))
            if p.z < 0: continue
            o.location = p; o.rotation_euler = (a * 8, a * 5, 0); K(o, f); on(o)
    if 338 <= f < 346:
        port_ring.scale = (.3 + 1.4 * ease_out((f - 338) / 8),) * 3; K(port_ring, f, True); on(port_ring)
    if f in (IMPACT, IMPACT + 1): on(impact_lines)
    w = ease((f - IMPACT - 6) / 36) if f > IMPACT + 6 else 0
    loc = (O7 + Vector((0, -10.5, .85))).lerp(O7 + Vector((1.1, -7.6, .7)), w)
    tgt = (O7 + Vector((.3, 0, 1.9))).lerp(LAND + Vector((0, 0, .7)), w)
    camera(f, loc, tgt, 24, .02 * math.sin(f * .05), .2)
    light(f, (-.6, -.5, .6), (0, 1, .15), '#F0A060', .9, impact=1.0 if f in (IMPACT, IMPACT + 1) else 0.0, sky=SKY6)


FNS = [shot1, shot2a, shot2b, shot3a, shot3b, shot4, shot5, shot6]


def shot_index(f):
    return max(i for i, (_, f0) in enumerate(SHOTS) if f >= f0)


for f in range(END):
    VISIBLE.clear()
    FNS[shot_index(f)](f)
    for o in TOGGLE:
        o.hide_render = o.name not in VISIBLE; o.keyframe_insert('hide_render', frame=f + 1)



def all_fcurves():
    """Katmanlı (4.4+) ve eski eylem düzeni için bütün eğriler."""
    for action in bpy.data.actions:
        layers = getattr(action, 'layers', None)
        if layers:
            for layer in layers:
                for strip in layer.strips:
                    for cb in strip.channelbags: yield from cb.fcurves
        elif hasattr(action, 'fcurves'): yield from action.fcurves


# Tercih ayarı keyframe_insert'e uygulanmıyor (ölçüldü: tek karelerde nesne iki anahtar arasında kayıyordu).
# Bütün anahtarları sabit yap: ikişer karede anime adımı gerçekten tutulsun, sahne kesmesinde nesne yolun ortasında kalmasın.
n_keys = 0
for fc in all_fcurves():
    for kp in fc.keyframe_points: kp.interpolation = 'CONSTANT'; n_keys += 1
print('SABIT ANAHTAR', n_keys)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'IntroV3.blend'))


def render_frames(frames, folder, prefix=''):
    for F in frames:
        s.frame_set(F); s.render.filepath = str(folder / f'{prefix}{F:04d}.png'); bpy.ops.render.render(write_still=True)


if ARGS == ['all']:
    s.render.filepath = str(FR) + '/'; bpy.ops.render.render(animation=True)
    glint.animation_data_clear()   # ipucu çeşidi: yalnız iki kare (Ç4, yakalanma anı) yeşil parıltıyla
    for F in (GLINT_F + 1, GLINT_F + 2): glint.hide_render = False; glint.keyframe_insert('hide_render', frame=F)
    render_frames((GLINT_F + 1, GLINT_F + 2), FR_GLINT)
else:
    render_frames([int(a) + 1 for a in ARGS if a.isdigit()], FR, 'deneme_')
print('INTRO V3 DONE', ARGS[:3])
