"""Ana menü canlı arka planı — Blender toon sahnesi (11 Kasım 2026).
Kullanıcı: "Blender ile bir sahne oluşturalım, 3 boyutlu, gerçek bir sahne gibi görünsün"; AI ile üretilen arka planlar
"AI slop" diye reddedildi. Açılış videosunun dili (Tools/Art/IntroAnimatic/build_intro_v5.py): oyunun toon'u (aynı rengin
üç parlaklık basamağı), fırça mürekkebi kabuk, boyalı lacivert bulutsu, Eri'nin kızıl gezegeni, düşmüş roket.
Özgün kaba geometri; dış model, doku, eklenti yok.

  blender -b --factory-startup -P Tools/Art/MenuScene/build_menu_scene.py -- 0 48 96       (önizleme kareleri, 960×540)
  blender -b --factory-startup -P Tools/Art/MenuScene/build_menu_scene.py -- all hd        (döngünün tamamı, 1920×1080)
Çıktı: Docs/MenuArkaPlan/Blender/v1[_1080]/kareler/####.png, MenuSahnesi.blend

Kurgu: kamera sabit ve yavaşça "nefes alır" (küçük yatay salınım: 3B derinlik paralaksla okunur). Ortası boş (başlık +
butonlar). Solda düşmüş roket (açılışın sonu) ve ilk saksı; sağda kristal mahsullü saksılar, uzakta sera kubbesi.
Döngü: LOOP kare; her hareket döngüye tam sayı kez sığar (son kare ilk kareye bağlanır). Nesneler ikişer karede (anime
adımı), kamera her karede. Rastgelelik sabit tohumlu.
"""
import bpy, bmesh, math, random, sys
from pathlib import Path
from mathutils import Vector, Matrix, Euler, noise as mnoise

ROOT = Path(__file__).resolve().parents[3]
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['0']
HD = 'hd' in ARGS; ARGS = [a for a in ARGS if a != 'hd']
OUT = ROOT / 'Docs' / 'MenuArkaPlan' / 'Blender' / ('v1_1080' if HD else 'v1')
FR = OUT / 'kareler'; FR.mkdir(parents=True, exist_ok=True)
FPS, LOOP = 24, 192   # 8 sn

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 24 if HD else 12
except Exception: pass
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = (1920, 1080, 100) if HD else (960, 540, 100)
s.render.image_settings.file_format = 'PNG'
s.render.fps, s.frame_start, s.frame_end = FPS, 1, LOOP

NOLINE = bpy.data.collections.new('Çizgisiz'); s.collection.children.link(NOLINE)


def srgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


def C(h): return (*lin(srgb(h)), 1.0)


# ---------------------------------------------------------------- palet (açılış + oyunun kiti)
INK, CREAM = '#151320', '#F4E6CC'
SPACE = '#1B2130'
RUST, RUST_HI, RUST_DARK, RUST_SH = '#B5623E', '#D98858', '#8E4430', '#7A3428'
HILL, GLOW_LOW, GLOW_HIGH = '#3A2630', '#D07A3A', '#9A3E2E'
HULL, NOSE, FIN, NOZZLE, GLASS = '#F4E6CC', '#E5502E', '#F2A23A', '#4A5068', '#CFE3EE'
SMOKE = '#9A93A6'
POT, POT_RIM, SOIL = '#C8643E', '#E07A50', '#3A2830'
LEAF, LEAF_DK = '#5BAE52', '#3E8A48'
ROOT_C = '#F2A23A'
CRY_P, CRY_B, CRY_G = '#9B6AD0', '#4FA3D6', '#F2C14A'
DOME_GLASS, DOME_LIGHT = '#9EC7DA', '#FFD27A'

# ---------------------------------------------------------------- sahne ışığı (tek düğüm grubu)
LG = bpy.data.node_groups.new('Sahne ışığı', 'ShaderNodeTree')
for name, typ in (('Key', 'NodeSocketVector'), ('Tint', 'NodeSocketColor')):
    LG.interface.new_socket(name=name, in_out='OUTPUT', socket_type=typ)
_go = LG.nodes.new('NodeGroupOutput')
L_KEY = LG.nodes.new('ShaderNodeCombineXYZ'); L_TINT = LG.nodes.new('ShaderNodeRGB')
LG.links.new(L_KEY.outputs[0], _go.inputs['Key']); LG.links.new(L_TINT.outputs[0], _go.inputs['Tint'])
for i, v in enumerate((-.55, -.45, .7)): L_KEY.inputs[i].default_value = v
L_TINT.outputs[0].default_value = (1, 1, 1, 1)


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


def finish(nt, color, g):
    c = mix(nt, 1.0, color, g.outputs['Tint'], 'MULTIPLY')
    em = node(nt, 'ShaderNodeEmission'); out = node(nt, 'ShaderNodeOutputMaterial')
    nt.links.new(c, em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])


MATS = {}


def toon(base, tex=None, name=None):
    """Oyunun toon'u: aynı rengin üç basamağı (n > .55 → ×1, n > −.12 → ×0,8, yoksa ×0,57)."""
    key = (base, tex)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name or f'Toon {base}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    geo = node(nt, 'ShaderNodeNewGeometry')
    nrm = node(nt, 'ShaderNodeVectorMath', operation='NORMALIZE'); nt.links.new(g.outputs['Key'], nrm.inputs[0])
    d = node(nt, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); nt.links.new(geo.outputs['Normal'], d.inputs[0]); nt.links.new(nrm.outputs[0], d.inputs[1])
    dk = d.outputs['Value']
    band = op(nt, 'ADD', op(nt, 'ADD', .57, op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, -.12), .23)),
              op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, .55), .2))
    b = tex(nt) if tex else C(base)
    sc = node(nt, 'ShaderNodeVectorMath', operation='SCALE'); put(nt, sc.inputs[0], b if isinstance(b, bpy.types.NodeSocket) else b[:3])
    nt.links.new(band, sc.inputs['Scale'])
    c = mix(nt, 1.0, sc.outputs[0], (0, 0, 0, 1), 'ADD')
    finish(nt, c, g); MATS[key] = m; return m


GLOW_VALUES = {}


def glow(color, name=None, animated=False):
    """Düz renk (ışıktan bağımsız). animated: parlaklık kare kare anahtarlanır (GLOW_VALUES)."""
    key = ('glow', color, animated, name if animated else None)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name or f'Işık {color}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    col = C(color)
    if animated:
        v = node(nt, 'ShaderNodeValue'); v.outputs[0].default_value = 1.0; GLOW_VALUES[m.name] = v
        sc = node(nt, 'ShaderNodeVectorMath', operation='SCALE'); sc.inputs[0].default_value = col[:3]; nt.links.new(v.outputs[0], sc.inputs['Scale'])
        col = mix(nt, 1.0, sc.outputs[0], (0, 0, 0, 1), 'ADD')
    finish(nt, col, g); MATS[key] = m; return m


INK_M = glow(INK, 'Mürekkep'); INK_M.use_backface_culling = True


def noise_mask(nt, scale, cut, detail=3.0, seed=0.0):
    tc = node(nt, 'ShaderNodeTexCoord'); nz = node(nt, 'ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value, nz.inputs['Detail'].default_value = scale, detail
    add = node(nt, 'ShaderNodeVectorMath', operation='ADD'); add.inputs[1].default_value = (seed, seed * .7, seed * 1.3)
    nt.links.new(tc.outputs['Object'], add.inputs[0]); nt.links.new(add.outputs[0], nz.inputs['Vector'])
    return op(nt, 'GREATER_THAN', nz.outputs['Fac'], cut)


def rust_tex(scale):
    def f(nt):
        patch = noise_mask(nt, scale, .56, 4.0, 3.1)
        return mix(nt, patch, C(RUST), C(RUST_DARK))
    f.__name__ = f'rust{scale}'; return f


# ---------------------------------------------------------------- geometri
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


def torus(R, r, m=Matrix(), seg=40, rseg=8, bm=None):
    bm = bm or bmesh.new(); rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        rings.append([bm.verts.new(m @ Vector(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
                      for b in (2 * math.pi * j / rseg for j in range(rseg))])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % seg]
        for j in range(rseg): bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    return bm


def star(r_out, r_in, n=4, m=Matrix(), bm=None):
    bm = bm or bmesh.new(); c = bm.verts.new(m @ Vector((0, 0, 0))); ring = []
    for i in range(2 * n):
        a = math.pi * i / n; r = r_out if i % 2 == 0 else r_in
        ring.append(bm.verts.new(m @ Vector((math.cos(a) * r, math.sin(a) * r, 0))))
    for i in range(2 * n): bm.faces.new((c, ring[i], ring[(i + 1) % (2 * n)]))
    return bm


def wobble(bm, amp, freq, seed=0.0, center=None):
    c = Vector(center) if center is not None else sum((v.co for v in bm.verts), Vector()) / max(len(bm.verts), 1)
    for v in bm.verts:
        d = v.co - c
        if d.length > 1e-6: v.co += d.normalized() * amp * mnoise.noise(v.co * freq + Vector((seed, seed * .3, 0)))
    return bm


def puff(seed, bm=None):
    r = random.Random(seed); bm = bm or bmesh.new(); sphere(1.0, Matrix(), 16, 8, bm)
    for _ in range(4):
        a, e = r.uniform(0, 2 * math.pi), r.uniform(-.3, .7)
        sphere(r.uniform(.5, .74), Matrix.Translation(Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * r.uniform(.55, .82)), 16, 8, bm)
    return wobble(bm, .1, 2.2, seed, (0, 0, 0))


def leaf_bm(L, w, bend):
    """Açılıştaki yaprak: uzunlamasına sivrilen, ucu bükük ince küre."""
    bm = sphere(1.0, u=16, v=6)
    for v in bm.verts:
        t = (v.co.x + 1) / 2; taper = math.sin(math.pi * clamp(t)) ** .75
        v.co = Vector((v.co.x * L + L, v.co.y * .05, v.co.z * w * taper + bend * L * t * t))
    return bm


def crystal_bm(h, r, sides=6, tip=.35, m=Matrix(), bm=None):
    """Köşeli kristal: altı yüzlü prizma + sivri uç (yüzleri düz gölgeli: toon basamakları yüzlerde okunur)."""
    bm = bm or bmesh.new()
    lo = [bm.verts.new(m @ Vector((r * math.cos(2 * math.pi * i / sides), r * math.sin(2 * math.pi * i / sides), 0))) for i in range(sides)]
    hi = [bm.verts.new(m @ Vector((r * .92 * math.cos(2 * math.pi * i / sides), r * .92 * math.sin(2 * math.pi * i / sides), h))) for i in range(sides)]
    top = bm.verts.new(m @ Vector((0, 0, h + r * tip * 3)))
    bm.faces.new(list(reversed(lo)))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((lo[i], lo[j], hi[j], hi[i])); bm.faces.new((hi[i], hi[j], top))
    return bm


BRUSH_MIN, BRUSH_GAIN = .3, 1.35


def clamp(x, a=0.0, b=1.0): return max(a, min(b, x))


def brush_ink(o, md, seed=0.0):
    """Açılışın fırça mürekkebi: kabuk kalınlığı nesne boyunca gürültüyle incelip kalınlaşır (sabit tohum, titremez)."""
    vs = o.data.vertices
    if not vs: return
    lo = Vector([min(v.co[i] for v in vs) for i in range(3)]); hi = Vector([max(v.co[i] for v in vs) for i in range(3)])
    fr = 2.2 / max((hi - lo).length, 1e-3); off = Vector((seed, seed * .37, seed * .71))
    vg = o.vertex_groups.new(name='Fırça')
    for v in vs: vg.add([v.index], clamp(.55 + .8 * mnoise.noise(v.co * fr + off)), 'REPLACE')
    md.vertex_group, md.thickness_vertex_group = vg.name, BRUSH_MIN / BRUSH_GAIN
    md.thickness *= BRUSH_GAIN


def obj(name, bm, mats, ink=0.0, parent=None, smooth=False, at=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); (s.collection if ink > 0 else NOLINE).objects.link(o)
    mats = list(mats) if isinstance(mats, (list, tuple)) else [mats]
    for m in mats: o.data.materials.append(m)
    if smooth: me.shade_smooth()
    if ink > 0:
        for _ in mats: o.data.materials.append(INK_M)
        md = o.modifiers.new('Mürekkep kabuk', 'SOLIDIFY')
        md.thickness, md.offset, md.use_flip_normals, md.use_rim, md.material_offset = ink, 1.0, True, False, len(mats)
        brush_ink(o, md, len(name) * 1.7)
    if parent: o.parent = parent
    if at is not None: o.location = at
    return o


def empty(name, parent=None, at=(0, 0, 0), rot=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); s.collection.objects.link(e); e.location = at; e.rotation_euler = rot
    if parent: e.parent = parent
    return e


RNG = random.Random(11)

# ---------------------------------------------------------------- gök: boyalı lacivert bulutsu (açılıştaki gibi), döngüde kayar
world = bpy.data.worlds.new('Menü gök'); s.world = world; world.use_nodes = True
WN = world.node_tree; BG = WN.nodes['Background']
tc = node(WN, 'ShaderNodeTexCoord')
DRIFT = node(WN, 'ShaderNodeCombineXYZ')   # döngüde küçük bir daire çizer: son kare ilk kareye bağlanır
add = node(WN, 'ShaderNodeVectorMath', operation='ADD'); WN.links.new(tc.outputs['Generated'], add.inputs[0]); WN.links.new(DRIFT.outputs[0], add.inputs[1])
neb = []
for sc_, det, rough in ((2.4, 3.0, .55), (.75, 2.0, .5)):
    nz = node(WN, 'ShaderNodeTexNoise'); WN.links.new(add.outputs[0], nz.inputs['Vector'])
    nz.inputs['Scale'].default_value, nz.inputs['Detail'].default_value, nz.inputs['Roughness'].default_value = sc_, det, rough
    neb.append(nz.outputs['Fac'])
nf = op(WN, 'ADD', op(WN, 'MULTIPLY', neb[0], .6), op(WN, 'MULTIPLY', neb[1], .4))
NEB1, NEB2 = '#252B47', '#2F305C'
d1 = tuple(a - b for a, b in zip(C(NEB1)[:3], C(SPACE)[:3])); d2 = tuple(a - b for a, b in zip(C(NEB2)[:3], C(NEB1)[:3]))
sky = mix(WN, op(WN, 'GREATER_THAN', nf, .54), C(SPACE), (*d1, 1), 'ADD')
sky = mix(WN, op(WN, 'GREATER_THAN', nf, .61), sky, (*d2, 1), 'ADD')
WN.links.new(sky, BG.inputs['Color'])

# ---------------------------------------------------------------- kamera
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam); s.camera = cam
cam.data.clip_start, cam.data.clip_end, cam.data.lens = .05, 800, 30
CAM_BASE, CAM_TGT = Vector((0, -17.5, 2.6)), Vector((0, 8, 2.9))

# ---------------------------------------------------------------- zemin, ufuk, tepeler
obj('Zemin', box((400, 400, .2), Matrix.Translation((0, 150, -.1))), toon(RUST, rust_tex(.35), 'Zemin'), 0)
obj('Ufuk ışığı alt', box((620, .2, 5), Matrix.Translation((0, 150, 1.0))), glow(GLOW_LOW), 0)
obj('Ufuk ışığı üst', box((620, .2, 3.2), Matrix.Translation((0, 150.5, 4.6))), glow(GLOW_HIGH), 0)
rng = random.Random(6); bm = bmesh.new(); x_ = -170
while x_ < 170:
    sphere(rng.uniform(9, 15), TRS((x_, 100 + rng.uniform(-6, 6), -1.5), scale=(1.4, .6, rng.uniform(6, 13) / 22)), 12, 6, bm); x_ += rng.uniform(11, 18)
obj('Tepeler', bm, toon(HILL), .3)
bm = bmesh.new()
for x0 in (-26, -17, 19, 28):   # yakın sırtlar: ortayı kapatmaz, iki yanda derinlik
    sphere(rng.uniform(5, 7), TRS((x0, 42 + rng.uniform(-3, 3), -1.2), scale=(1.6, .7, rng.uniform(.35, .5))), 14, 7, bm)
obj('Yakın sırtlar', bm, toon('#5A3230'), .12)
bm = bmesh.new()
for i in range(70):
    side = -1 if i % 2 else 1
    p_ = Vector((side * rng.uniform(3, 30) if i % 5 else rng.uniform(-14, 14), rng.uniform(-10, 34), 0))
    ico(rng.uniform(.12, .5), TRS(p_, (rng.random() * 3,) * 3, (1.4, 1, .6)), 1, bm)
obj('Kayalar', bm, toon(RUST_HI), .03)

# ---------------------------------------------------------------- gökteki ay ve yıldızlar (ortayı kapatmaz)
moon = obj('Ay', wobble(ico(5.0, sub=3), .25, .4, 3, (0, 0, 0)), toon('#EADCC0'), .3, at=(-46, 120, 46))
bm = bmesh.new()
for cx, cz, r in ((-1.6, 1.2, .9), (1.4, -.8, 1.2), (.4, 2.4, .5)):
    sphere(r, TRS((cx, -4.6, cz), scale=(1, .25, 1)), 12, 6, bm)
obj('Ay kraterleri', bm, toon('#C9B89A'), 0, moon)
STARS = []
for i in range(14):
    a = RNG.uniform(-1, 1)
    p_ = Vector((RNG.uniform(-150, 150), 260, RNG.uniform(40, 120)))
    if abs(p_.x) < 45 and p_.z < 95: p_.x += 60 * (1 if p_.x >= 0 else -1)   # ortanın üstü boş kalsın
    o = obj(f'Parıltı {i}', star(RNG.uniform(1.2, 2.4), .35, 4, TRS(rot=(math.pi / 2, 0, 0))), glow(CREAM), 0, at=p_)
    STARS.append((o, RNG.random(), RNG.choice((1, 2))))
bm = bmesh.new()
for _ in range(160):
    ico(RNG.uniform(.18, .45), Matrix.Translation((RNG.uniform(-260, 260), 300, RNG.uniform(25, 170))), 1, bm)
obj('Yıldız alanı', bm, glow(CREAM), 0)

# ---------------------------------------------------------------- düşmüş roket (açılışın sonu): burnu yere gömülü, solda
SHIP = empty('ROKET', at=(-10.5, 9.0, 1.05), rot=(0, math.radians(16), math.radians(32))); SHIP.scale = (1.35, 1.35, 1.35)
ROT_Z_TO_X = Matrix.Rotation(math.pi / 2, 4, 'Y')
obj('Gövde', cone(.9, .9, 4.4, ROT_Z_TO_X, 32), toon(HULL), .05, SHIP)
obj('Gövde şeridi', cone(.925, .925, .32, Matrix.Translation((1.64, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE), 0, SHIP)
bm = bmesh.new()
for x in (-1.1, .4): torus(.905, .024, Matrix.Translation((x, 0, 0)) @ ROT_Z_TO_X, 48, 4, bm)
obj('Sac çizgileri', bm, glow(INK), 0, SHIP)
obj('Burun', cone(.9, .14, 2.0, Matrix.Translation((3.2, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOSE), .05, SHIP)
obj('Motor ağzı', cone(.76, .52, .7, Matrix.Translation((-2.55, 0, 0)) @ ROT_Z_TO_X, 32), toon(NOZZLE), .05, SHIP)
bm = bmesh.new()
for a in (math.radians(d) for d in (45, 135, 225, 315)):
    d, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    vs = [bm.verts.new(Vector((x, 0, 0)) + d * r + t * side) for x, r in ((-2.2, .84), (-.7, .84), (-2.04, 1.96), (-2.84, 1.96)) for side in (-.07, .07)]
    bmesh.ops.convex_hull(bm, input=vs)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
obj('Kanatlar', bm, toon(FIN), .05, SHIP)
obj('Yama', box((.68, .1, .48), TRS((-1.2, -.86, -.28), (0, .12, 0))), toon('#C9B27A'), .04, SHIP)
obj('Lomboz çerçevesi', torus(.4, .1, TRS((.7, -.9, .12), (math.pi / 2, 0, 0))), toon(NOZZLE), .04, SHIP)
PORT = obj('Lomboz camı', sphere(.38, TRS((.7, -.88, .12), scale=(1, .3, 1))), glow(DOME_LIGHT, 'Lomboz ışığı', True), 0, SHIP)
obj('Kayma izi', box((9.0, 2.0, .06), TRS((-6.0, 12.6, .02), (0, 0, math.radians(32)))), glow('#5A2620'), 0)
bm = bmesh.new()
for x0, y0, r in ((-6.4, 11.4, 1.6), (-5.5, 12.3, 1.2), (-7.4, 12.0, 1.1)):
    sphere(r, TRS((x0, y0, 0), scale=(1.2, 1, .45)), 14, 7, bm)
obj('Yığılmış toprak', bm, toon(RUST_HI), .05)

# ---------------------------------------------------------------- saksılar (oyunun saksısı: kalın kenarlı toprak kutu)
def planter(name, at, size, rot=0.0):
    root = empty(name, at=at, rot=(0, 0, rot)); w, d, h = size
    bm = box((w, d, h), TRS((0, 0, h / 2)))
    obj(f'{name} gövde', bm, toon(POT), .06, root)
    bm = bmesh.new()
    for sx, sy, ox, oy in ((w + .3, .3, 0, d / 2), (w + .3, .3, 0, -d / 2), (.3, d + .3, w / 2, 0), (.3, d + .3, -w / 2, 0)):
        box((sx, sy, .3), TRS((ox, oy, h + .05)), bm)
    obj(f'{name} kenar', bm, toon(POT_RIM), .05, root)
    obj(f'{name} toprak', box((w - .1, d - .1, .2), TRS((0, 0, h - .05))), toon(SOIL), 0, root)
    return root


PLANT = []   # (nesne, temel dönüş, genlik, faz, döngü sayısı)


def leaves(parent, at, n, L, seed, spread=1.0, color=LEAF):
    r = random.Random(seed)
    for i in range(n):
        a = i / n * 2 * math.pi + r.uniform(-.3, .3)
        e = empty(f'{parent.name} yaprak kökü {i}', parent, at, (0, -math.radians(r.uniform(35, 60)) * spread, a))
        o = obj(f'{parent.name} yaprak {i}', leaf_bm(L * r.uniform(.8, 1.15), L * .32, .45), toon(color if i % 3 else LEAF_DK), .035, e)
        PLANT.append((e, e.rotation_euler.copy(), math.radians(r.uniform(4, 7)), r.random(), r.choice((1, 2))))


# Sol ön: ilk saksı, kocaman bir kök mahsul (oyunun ilk bitkisi gibi)
P1 = planter('Saksı sol', (-8.6, -2.6, 0), (4.0, 2.2, 1.2), math.radians(-12))
obj('Kök mahsul', wobble(sphere(1.05, TRS((0, 0, 0), scale=(1, 1, .9)), 20, 10), .08, .9, 2), toon(ROOT_C), .05, P1, at=(-.4, 0, 1.6))
obj('Küçük kök', wobble(sphere(.5, u=14, v=7), .05, 1.2, 4), toon(ROOT_C), .04, P1, at=(1.3, .2, 1.35))
leaves(P1, (-.4, 0, 2.4), 6, 1.5, 3)
leaves(P1, (1.3, .2, 1.75), 4, .8, 5)

# Sağ ön: kristal mahsul saksısı
P2 = planter('Saksı sağ', (8.8, -2.2, 0), (4.0, 2.2, 1.2), math.radians(12))
CRYSTALS = []
bm_specs = ((-.9, 0, 2.2, .42, CRY_P, -.12), (.2, .2, 3.0, .5, CRY_P, .05), (1.2, -.1, 1.7, .36, CRY_B, .18), (-.2, -.4, 1.3, .3, CRY_B, -.25), (.9, .5, 1.1, .26, CRY_G, .3))
for i, (cx, cy, h, r, col, tilt) in enumerate(bm_specs):
    e = empty(f'Kristal kökü {i}', P2, (cx, cy, 1.2), (tilt, tilt * .5, RNG.uniform(0, 1)))
    o = obj(f'Kristal {i}', crystal_bm(h, r), toon(col), .04, e)
    CRYSTALS.append((e, e.location.copy(), RNG.random()))
leaves(P2, (-1.4, .1, 1.3), 3, .7, 8)
leaves(P2, (1.6, .4, 1.3), 3, .6, 9)

# Sağ orta: ikinci sıra saksılar (derinlik), uzakta sera kubbesi
P3 = planter('Saksı sağ arka', (14.5, 8.5, 0), (3.6, 2.0, 1.1), math.radians(-14))
leaves(P3, (-.8, 0, 1.2), 5, .9, 12)
leaves(P3, (.9, 0, 1.2), 5, .9, 13)
P4 = planter('Saksı sol arka', (-18.5, 4.5, 0), (3.2, 1.8, 1.0), math.radians(18))
leaves(P4, (0, 0, 1.1), 6, 1.0, 14)

DOME = empty('Sera', at=(17.5, 30.0, 0))
bm = sphere(4.2, u=24, v=12)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -.05], context='VERTS')
obj('Sera camı', bm, toon(DOME_GLASS), .12, DOME)
bm = bmesh.new()
for i in range(8):
    a = i / 8 * math.pi
    torus(4.22, .07, TRS(rot=(math.pi / 2, 0, a)), 40, 4, bm)
torus(4.0, .08, TRS((0, 0, 1.7)), 40, 4, bm)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -.05], context='VERTS')
obj('Sera iskeleti', bm, glow(INK), 0, DOME)
obj('Sera ışığı', box((5.0, .2, 1.8), TRS((0, -3.2, .9))), glow(DOME_LIGHT, 'Sera ışığı', True), 0, DOME)
obj('Sera kaidesi', cone(4.6, 4.4, .5, TRS((0, 0, .1))), toon('#6A4A48'), .08, DOME)

# ---------------------------------------------------------------- hareketli küçükler
SMOKE_P = [obj(f'Duman {i}', puff(40 + i), toon(SMOKE), .04) for i in range(6)]
DUST = [obj(f'Toz {i}', puff(70 + i), toon('#E6C9A0'), .02) for i in range(3)]
SPORES = [obj(f'Spor {i}', star(.16, .06, 4, TRS(rot=(math.pi / 2, 0, 0))), glow('#FFE39A'), 0) for i in range(10)]
SPORE_BASE = [Vector((RNG.choice((-1, 1)) * RNG.uniform(4, 13), RNG.uniform(-7, 4), RNG.uniform(1.6, 5.0))) for _ in SPORES]
SPORE_PH = [RNG.random() for _ in SPORES]


def W(f): return 2 * math.pi * f / LOOP


def ease(x): x = clamp(x); return x * x * (3 - 2 * x)


def key_obj(o, f):
    for p in ('location', 'rotation_euler', 'scale'): o.keyframe_insert(p, frame=f + 1)


bpy.context.view_layer.update()
NOZZLE_P = SHIP.matrix_world @ Vector((-2.95, 0, .2))

for f in range(LOOP):
    # kamera her karede: yatayda yavaş salınım + çok küçük dikey nefes (iki döngü)
    loc = CAM_BASE + Vector((.55 * math.sin(W(f)), .0, .12 * math.sin(2 * W(f))))
    tgt = CAM_TGT + Vector((.2 * math.sin(W(f)), 0, 0))
    cam.location = loc; cam.rotation_euler = (tgt - loc).to_track_quat('-Z', 'Y').to_euler()
    cam.keyframe_insert('location', frame=f + 1); cam.keyframe_insert('rotation_euler', frame=f + 1)
    # bulutsu: küçük daire (bir döngü)
    DRIFT.inputs[0].default_value, DRIFT.inputs[1].default_value = .035 * math.cos(W(f)), .035 * math.sin(W(f))
    DRIFT.inputs[0].keyframe_insert('default_value', frame=f + 1); DRIFT.inputs[1].keyframe_insert('default_value', frame=f + 1)
    if f % 2: continue   # nesneler ikişer karede
    # yapraklar esintide (her biri döngüye 1 ya da 2 kez)
    for e, base, amp, ph, k in PLANT:
        e.rotation_euler = (base.x + amp * .4 * math.sin(k * W(f) + ph * 6.28), base.y + amp * math.sin(k * W(f) + ph * 6.28 + .9), base.z)
        key_obj(e, f)
    # kristaller nefes alır (hafif yükselip iner) ve ışıkları atar
    for e, base, ph in CRYSTALS:
        e.location = base + Vector((0, 0, .06 * math.sin(2 * W(f) + ph * 6.28))); key_obj(e, f)
    for name, v in GLOW_VALUES.items():
        v.outputs[0].default_value = .86 + .14 * math.sin(2 * W(f) + (1.3 if 'Sera' in name else 0)); v.outputs[0].keyframe_insert('default_value', frame=f + 1)
    # roketten ince duman: 6 topak, her biri döngüde iki kez yükselir (eşit aralıklı: sürekli akış)
    for i, o in enumerate(SMOKE_P):
        p = (2 * f / LOOP + i / len(SMOKE_P)) % 1.0
        o.location = NOZZLE_P + Vector((-.6 * p - .2 * math.sin(p * 5), .2 * p, 4.2 * p))
        sc = .25 + .9 * p; sc *= min(1.0, p / .12) * (1 - ease((p - .7) / .3))
        o.scale = (sc, sc, sc * .9); key_obj(o, f)
    # yerde süzülen toz (soldan sağa; uçlarda küçülür, döngüde bir kez)
    for i, o in enumerate(DUST):
        p = (f / LOOP + i / len(DUST)) % 1.0
        o.location = Vector((-24 + 48 * p, -8 + 3 * i + 1.2 * math.sin(p * 6.28 + i), .12))
        sc = .2 * math.sin(math.pi * p) ** .5; o.scale = (sc * 1.8, sc, sc * .4); key_obj(o, f)
    # sporlar: saksıların üstünde yavaşça yükselip alçalır, parlar (ikişer döngü)
    for i, o in enumerate(SPORES):
        ph = SPORE_PH[i] * 6.28
        o.location = SPORE_BASE[i] + Vector((.3 * math.sin(W(f) + ph), 0, .5 * math.sin(2 * W(f) + ph)))
        o.rotation_euler = (0, 0, 0)
        sc = .7 + .5 * (.5 + .5 * math.sin(2 * W(f) + ph * 1.7)); o.scale = (sc, sc, sc); key_obj(o, f)
    for o, ph, k in STARS:
        sc = .55 + .45 * (.5 + .5 * math.sin(k * W(f) + ph * 6.28)); o.scale = (sc, sc, sc); key_obj(o, f)


def all_fcurves():
    for action in bpy.data.actions:
        layers = getattr(action, 'layers', None)
        if layers:
            for layer in layers:
                for strip in layer.strips:
                    for cb in strip.channelbags: yield from cb.fcurves
        elif hasattr(action, 'fcurves'): yield from action.fcurves
    for nt in [world.node_tree] + [m.node_tree for m in bpy.data.materials if m.node_tree]:
        ad = nt.animation_data
        if ad and ad.action:
            layers = getattr(ad.action, 'layers', None)
            if layers:
                for layer in layers:
                    for strip in layer.strips:
                        for cb in strip.channelbags: yield from cb.fcurves
            elif hasattr(ad.action, 'fcurves'): yield from ad.action.fcurves


n_keys = 0
for fc in all_fcurves():
    for kp in fc.keyframe_points: kp.interpolation = 'CONSTANT'; n_keys += 1
print('SABIT ANAHTAR', n_keys)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'MenuSahnesi.blend'))

if ARGS == ['all']:
    s.render.filepath = str(FR) + '/'; bpy.ops.render.render(animation=True)
else:
    for a in ARGS:
        if a.isdigit():
            s.frame_set(int(a) + 1); s.render.filepath = str(FR / f'deneme_{int(a):04d}.png'); bpy.ops.render.render(write_still=True)
print('MENU SCENE DONE', ARGS[:3])
