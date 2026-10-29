"""Açılış videosu — animatik v1 (28 Ekim 2026). Docs/AcilisVideosu-CekimListesi.md'deki 7 çekimin kaba 3D yerleşimi.
  blender -b --factory-startup -t 4 -P Tools/Art/IntroAnimatic/build_intro_animatic.py
Çıktı: Docs/AcilisVideosu/Animatik/paneller/*.png, paneller.json, IntroAnimatic.blend.
Hiçbir şey canlandırılmaz: her vuruş tek durağan panel; süreleri assemble_intro_animatic.py uygular.
Kaba toon: düz 2–3 ton + mürekkep kabuk. Özgün geometri; dış model/doku/eklenti yok.
Etiketler yalnız inceleme videosu için, Blender'ın yerleşik yazı tipiyle; oyuna girmez.
"""
import bpy, bmesh, math, json, random
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion, Euler

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik'
PANELS = OUT / 'paneller'
PANELS.mkdir(parents=True, exist_ok=True)
FPS = 24

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 8
except Exception: pass
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 540, 100
s.render.image_settings.file_format = 'PNG'


def srgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


# ---------------------------------------------------------------- palet (oyunun mürekkep/krem/amber dili)
INK, CREAM, SPACE = '#1A1324', '#FFF4D6', '#151330'
OCEAN, LAND, CLOUD = '#3D86C6', '#5DBB4C', '#F4F1EA'
CRACK, CORE, HOT = '#FF9A2E', '#FF6A2A', '#FFD27A'
HULL, NOSE, FIN, NOZZLE, GLASS, PATCH = '#FFF0D2', '#E8742F', '#F2A23A', '#4A4458', '#BFB6D6', '#C9B27A'
FLAME, FLAME_IN = '#FFB43A', '#FFF1A8'
PANEL, REDZONE, LIGHT_OFF, LIGHT_ON, GLOVE = '#2B2238', '#E0503A', '#6B2A2A', '#FF4A3A', '#D9783A'
RUST, RUST_DARK, GLINT = '#B8643C', '#8A4430', '#9BE58A'
SKY_CLOUD, SKY_GROUND, GLOW_LOW, GLOW_HIGH = '#4B3B60', '#2E2240', '#B8743A', '#5A3A48'
CLOUD_W, SOIL, HILL, ROCK, TUFT, LEAF_FG = '#E9D8C0', '#3B2A20', '#3A2C45', '#6B5A55', '#3E8E3A', '#21182B'
DUST, SMOKE = '#E6D2B0', '#8E8796'


# ---------------------------------------------------------------- malzemeler (GreedJaw/Aurora ile aynı toon kurgusu)
def ramp_toon(m, color_socket_from=None, color=None):
    n = m.node_tree.nodes; L = m.node_tree.links
    dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    base = lin(srgb(color)) if color else (1, 1, 1)
    e[0].position, e[0].color = 0, (*[x * .55 for x in base], 1)
    e[1].position, e[1].color = .3, (*[x * .8 for x in base], 1)
    e3 = e.new(.6); e3.color = (*base, 1)
    L.new(dif.outputs[0], rgb.inputs[0]); L.new(rgb.outputs[0], ramp.inputs[0])
    return ramp


def mat_flat(name, color, toon=True):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission')
    if toon:
        m.node_tree.links.new(ramp_toon(m, color=color).outputs[0], em.inputs['Color'])
    else:
        em.inputs['Color'].default_value = (*lin(srgb(color)), 1)
    m.node_tree.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def mat_toon_attr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute'); at.attribute_name = 'Col'
    mix = n.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    cin = [i for i in mix.inputs if i.type == 'RGBA']; [i for i in mix.inputs if i.name == 'Factor'][0].default_value = 1
    cout = [o for o in mix.outputs if o.type == 'RGBA'][0]
    L = m.node_tree.links
    L.new(ramp_toon(m).outputs[0], cin[0]); L.new(at.outputs['Color'], cin[1])
    L.new(cout, em.inputs['Color']); L.new(em.outputs[0], out.inputs['Surface'])
    return m


MATS = {}


def M(color, toon=True):
    k = (color, toon)
    if k not in MATS: MATS[k] = mat_flat(f"{'Toon' if toon else 'Düz'} {color}", color, toon)
    return MATS[k]


INK_M = mat_flat('Mürekkep', INK, toon=False); INK_M.use_backface_culling = True
PAINTED = mat_toon_attr('Boyalı toon')


# ---------------------------------------------------------------- sahne düzeni
COLLS = {}


def coll(name):
    if name not in COLLS:
        c = bpy.data.collections.new(name); s.collection.children.link(c); COLLS[name] = c
    return COLLS[name]


def obj(name, bm, mat, ink=0.0, parent=None, where=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); (where or s.collection).objects.link(o)
    o.data.materials.append(mat)
    if ink > 0:
        o.data.materials.append(INK_M)
        md = o.modifiers.new('Mürekkep kabuk', 'SOLIDIFY')
        md.thickness, md.offset, md.use_flip_normals, md.use_rim, md.material_offset = ink, 1.0, True, False, 1
    if parent: o.parent = parent
    return o


def empty(name, parent=None, at=(0, 0, 0), where=None):
    e = bpy.data.objects.new(name, None); (where or s.collection).objects.link(e); e.location = at
    if parent: e.parent = parent
    return e


def paint(o, color_of_face):
    attr = o.data.color_attributes.new(name='Col', type='FLOAT_COLOR', domain='CORNER')
    for poly in o.data.polygons:
        c = lin(srgb(color_of_face(poly)))
        for li in poly.loop_indices: attr.data[li].color = (*c, 1)


def TRS(at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return Matrix.Translation(at) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))


def sphere(r, m=Matrix(), u=24, v=12, bm=None):
    bm = bm or bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r, matrix=m); return bm


def ico(r, m=Matrix(), sub=1, bm=None):
    bm = bm or bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r, matrix=m); return bm


def cone(r1, r2, depth, m=Matrix(), seg=24, bm=None):
    bm = bm or bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=m); return bm


def box(size, m=Matrix(), bm=None):
    bm = bm or bmesh.new()
    bmesh.ops.create_cube(bm, size=1, matrix=m @ Matrix.Diagonal((*size, 1))); return bm


def torus(R, r, m=Matrix(), seg=40, rseg=8, bm=None, a0=0.0, a1=2 * math.pi):
    """Halka; a0–a1 tam tur değilse uçları açık yay."""
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


def resample(pts, step):
    """Çizgiyi eşit aralıklı noktalara böler (kesik çizgi için)."""
    pts = [Vector(p) for p in pts]; d = [0.0]
    for a, b in zip(pts, pts[1:]): d.append(d[-1] + (b - a).length)
    out, k, j = [], 0, 0
    while k * step <= d[-1]:
        t = k * step
        while d[j + 1] < t: j += 1
        out.append(pts[j].lerp(pts[j + 1], (t - d[j]) / max(d[j + 1] - d[j], 1e-9))); k += 1
    return out


def bezier(p0, direction, p1, n=30):
    p0, p1 = Vector(p0), Vector(p1); c = p0 + Vector(direction).normalized() * (p1 - p0).length * .55
    return [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * c + t * t * p1 for t in (i / n for i in range(n + 1))]


def between(p0, p1, r0, r1, seg=12, bm=None):
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0
    q = d.normalized().to_track_quat('Z', 'Y')
    m = Matrix.Translation((p0 + p1) / 2) @ q.to_matrix().to_4x4()
    return cone(r0, r1, d.length, m, seg, bm)


def tube(name, pts, radius, mat, where):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = radius; cu.bevel_resolution = 2
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, q in zip(sp.points, pts): p.co = (*q, 1)
    o = bpy.data.objects.new(name, cu); where.objects.link(o); cu.materials.append(mat); return o


def show(o, on=True):
    o.hide_render = not on
    for c in o.children: show(c, on)


ROT_Z_TO_X = Matrix.Rotation(math.pi / 2, 4, 'Y')
RNG = random.Random(28)

# ---------------------------------------------------------------- ışık, kamera, ekran katmanı (HUD)
sun = bpy.data.objects.new('Güneş', bpy.data.lights.new('Güneş', 'SUN')); s.collection.objects.link(sun)
sun.data.energy, sun.data.use_shadow = 1.2, False
sun.rotation_euler = Vector((.5, .6, -.6)).to_track_quat('-Z', 'Y').to_euler()
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam); s.camera = cam
cam.data.clip_end = 500

world = bpy.data.worlds.new('Animatik gök'); s.world = world; world.use_nodes = True
BG = world.node_tree.nodes['Background']


def sky(color):
    BG.inputs['Color'].default_value = (*lin(srgb(color)), 1); BG.inputs['Strength'].default_value = 1


# HUD: kameraya bağlı, kadraj genişliği x∈[-1,1], yüksekliği y∈[-.5625,.5625]
hud = empty('HUD', cam, (0, 0, -1))
stars = empty('Yıldızlar', cam, (0, 0, -60))
bm = bmesh.new()
for _ in range(70):
    x, y = RNG.uniform(-1.05, 1.05), RNG.uniform(-.6, .6)
    ico(RNG.uniform(.0025, .0055), Matrix.Translation((x, y, 0)), 1, bm)
obj('Yıldız alanı', bm, M(CREAM, False), parent=stars)


def frame_cam(loc, target, lens=35, roll=0.0):
    cam.location = loc; cam.data.lens = lens
    q = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y') @ Quaternion((0, 0, 1), roll)
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = q
    k = 18 / lens; hud.scale = (k, k, k); stars.scale = (60 * k, 60 * k, 60 * k)


label_txt = bpy.data.objects.new('Etiket', bpy.data.curves.new('Etiket', 'FONT')); s.collection.objects.link(label_txt)
label_txt.parent = hud; label_txt.data.size = .052; label_txt.data.materials.append(M(CREAM, False))
label_txt.location = (-.96, .5625 - .085, .02)
plate = obj('Etiket zemini', box((1, 1, .001)), M(INK, False), parent=hud)


def set_label(text):
    label_txt.data.body = text
    bpy.context.view_layer.update()
    w = label_txt.dimensions.x   # yazının kendi ölçeği 1; HUD birimiyle aynı
    plate.scale = (w + .04, .085, 1); plate.location = (-.96 + w / 2, .5625 - .085 + .016, .01)


# Hız çizgileri: kadraj kenarlarında, gruba göre döndürülür
speed = empty('Hız çizgileri', hud)
bm = bmesh.new()
for i in range(18):
    band = 1 if i % 2 else -1
    y = band * RNG.uniform(.36, .55); x = RNG.uniform(-.95, .95); ln = RNG.uniform(.12, .32)
    box((ln, .004, .001), Matrix.Translation((x, y, 0)), bm)
obj('Kenar çizgileri', bm, M('#9C94B5', False), parent=speed)

smear = empty('Kamçı bulanıklığı', hud)
bm = bmesh.new()
for i in range(46):
    y = RNG.uniform(-.56, .56); x = RNG.uniform(-1.1, 1.1); ln = RNG.uniform(.25, .9)
    box((ln, RNG.uniform(.003, .012), .001), Matrix.Translation((x, y, 0)), bm)
obj('Kamçı çizgileri', bm, M('#6E6890', False), parent=smear)
obj('Kamçı alev izi', box((1.5, .018, .001), Matrix.Translation((.1, .04, .002))), M(FLAME, False), parent=smear)

flash = empty('Parıltı', hud)
obj('Parıltı zemini', box((2.4, 1.4, .001), Matrix.Translation((0, 0, -.05))), M('#FFC46B', False), parent=flash)
obj('Parıltı göbeği', sphere(.32, TRS((0, 0, -.04), scale=(1.4, 1, .01))), M('#FFF1C9', False), parent=flash)

# ---------------------------------------------------------------- gemi (tek model, her çekimde aynı)
SHIP = empty('GEMİ')
obj('Gövde', cone(.45, .45, 2.2, ROT_Z_TO_X), M(HULL), .03, SHIP)
nose = obj('Burun', cone(.45, .07, 1.0, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X), M(NOSE), .03, SHIP)
nose_hot = obj('Burun (ısınmış)', cone(.46, .08, 1.02, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X), M(HOT, False), .03, SHIP)
obj('Motor ağzı', cone(.38, .26, .35, Matrix.Translation((-1.275, 0, 0)) @ ROT_Z_TO_X), M(NOZZLE), .03, SHIP)
bm = bmesh.new()
for a in (math.radians(d) for d in (45, 135, 225, 315)):
    d, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    vs = [bm.verts.new(Vector((x, 0, 0)) + d * r + t * side)
          for x, r in ((-1.10, .42), (-.35, .42), (-1.02, .98), (-1.42, .98)) for side in (-.035, .035)]
    bmesh.ops.convex_hull(bm, input=vs)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
obj('Kanatlar', bm, M(FIN), .03, SHIP)
obj('Yama', box((.34, .05, .24), TRS((-.6, -.43, -.14), (0, .12, 0))), M(PATCH), .02, SHIP)
obj('Lomboz çerçevesi', torus(.2, .045, TRS((.35, -.45, .06), (math.pi / 2, 0, 0))), M(INK, False), 0, SHIP)
glass = obj('Lomboz camı', sphere(.19, TRS((.35, -.44, .06), scale=(1, .3, 1))), M(GLASS), 0, SHIP)
glass_lit = obj('Lomboz camı (ışıklı)', sphere(.192, TRS((.35, -.441, .06), scale=(1, .3, 1))), M(HOT, False), 0, SHIP)
flame = empty('Alev', SHIP, (-1.45, 0, 0))
obj('Alev dış', cone(.02, .30, 1.6, Matrix.Translation((-.8, 0, 0)) @ ROT_Z_TO_X), M(FLAME, False), .025, flame)
obj('Alev iç', cone(.01, .16, .95, Matrix.Translation((-.5, 0, .0)) @ ROT_Z_TO_X), M(FLAME_IN, False), 0, flame)
bm = bmesh.new()
for z in (-.25, .05, .3):
    between((1.6, -.2, z), (-.6, -.25, z * 1.4), .035, .005, 6, bm)
heat = obj('Isı izleri', bm, M(HOT, False), 0, SHIP)


def place_ship(at, rot=(0, 0, 0), scale=1.0, thrust=1.0, hot=False, lit=False):
    SHIP.location, SHIP.rotation_euler, SHIP.scale = at, rot, (scale,) * 3
    show(SHIP, True)
    show(flame, thrust > 0); flame.scale = (max(thrust, .001), max(min(1, thrust * 1.4), .3), max(min(1, thrust * 1.4), .3))
    show(nose_hot, hot); show(nose, not hot); show(heat, hot)
    show(glass_lit, lit); show(glass, not lit)
    bpy.context.view_layer.update()


def ship_point(local):
    return SHIP.matrix_world @ Vector(local)


# ---------------------------------------------------------------- Ç1 — Dünya
C1 = coll('Ç1 Dünya'); R = 3.0


def earth_color(n):
    v = math.sin(2.6 * n.x + .7) + math.cos(3.1 * n.z - .4) + .9 * math.sin(4.2 * n.y + 2 * n.x)
    if math.sin(6 * n.x + 5 * n.z) * math.cos(4 * n.y) > .78: return CLOUD
    return LAND if v > .55 else OCEAN


earth = obj('Dünya', sphere(R, u=64, v=32), PAINTED, .055, where=C1)
paint(earth, lambda p: earth_color(Vector(p.center).normalized()))


def surf(d, k=1.0):
    return tuple(Vector(d).normalized() * R * k)


CRACK1 = [surf(d, 1.004) for d in ((-.62, -.7, .55), (-.48, -.82, .42), (-.36, -.8, .38), (-.28, -.9, .2), (-.12, -.88, .18),
                                     (-.05, -.95, -.02), (.1, -.9, -.08), (.18, -.88, -.25), (.3, -.85, -.32), (.42, -.75, -.5))]
CRACK2 = [surf(d, 1.004) for d in ((-.05, -.95, -.02), (.12, -.95, .1), (.3, -.9, .08), (.45, -.82, .22), (.62, -.72, .2), (.75, -.6, .3))]
# Işık tüpü mürekkep tüpünün üstüne çıkar: önden bakınca ortası turuncu, kenarları mürekkep.
crack_thin = [tube('Çatlak 1 mürekkep', CRACK1, .075, M(INK, False), C1), tube('Çatlak 1 ışık', [surf(p, 1.019) for p in CRACK1], .04, M(CRACK, False), C1)]
crack_wide = [tube('Çatlak 1 geniş mürekkep', CRACK1, .14, M(INK, False), C1), tube('Çatlak 1 geniş ışık', [surf(p, 1.032) for p in CRACK1], .085, M(CRACK, False), C1),
              tube('Çatlak 2 mürekkep', CRACK2, .1, M(INK, False), C1), tube('Çatlak 2 ışık', [surf(p, 1.024) for p in CRACK2], .055, M(CRACK, False), C1)]
bm = bmesh.new()
for i, (d, k) in enumerate(((CRACK1[2], 1.09), (CRACK1[5], 1.12), (CRACK1[7], 1.07), (CRACK2[3], 1.1))):
    ico(.18 + .05 * (i % 2), TRS(surf(d, k), (i, i * .7, i * 1.3), (1.4, 1, .6)), 1, bm)
chunks = obj('Kalkan kabuk parçaları', bm, M(LAND), .03, where=C1)
LAUNCH_FROM, LAUNCH_TO = Vector(surf((.92, -.32, .3), 1.0)), Vector(surf((.92, -.32, .3), 1.0)) + Vector((1.1, -.2, .55))
bm = between(LAUNCH_FROM, LAUNCH_TO, .02, .07, 10)
launch = [obj('Fırlayış izi', bm, M(FLAME, False), .02, where=C1),
          obj('Fırlayan gemi', sphere(.13, Matrix.Translation(LAUNCH_TO)), M(HOT, False), .03, where=C1)]

# ---------------------------------------------------------------- Ç2 — kaçış, ikiye ayrılan Dünya
C2 = coll('Ç2 Kaçış'); X2 = 100.0


def half_earth(name, keep_positive, at, rot):
    bm = sphere(R, u=48, v=24)
    no = Vector((1, 0, .25)).normalized()
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=no, clear_inner=keep_positive, clear_outer=not keep_positive)
    edges = [e for e in bm.edges if e.is_boundary]
    bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj(name, bm, PAINTED, .055, where=C2); o.location, o.rotation_euler = at, rot
    paint(o, lambda p: CORE if len(p.vertices) > 8 else earth_color(Vector(p.center).normalized()))
    return o


EC2 = Vector((X2 - 10.5, 22, 4.2))   # arkada, sol üstte; gemiyle üst üste binmez
half_earth('Dünya yarısı A', False, EC2 + Vector((-1.7, 0, .6)), (.1, .25, -.2))
half_earth('Dünya yarısı B', True, EC2 + Vector((1.5, .6, -.7)), (-.15, -.2, .3))
obj('Çekirdek', sphere(1.7, Matrix.Translation(EC2)), M(CORE, False), .05, where=C2)
bm = bmesh.new()
for i in range(11):
    p = EC2 + Vector((RNG.uniform(-5, 6), RNG.uniform(-3, 3), RNG.uniform(-3.2, 2.5)))
    ico(RNG.uniform(.18, .5), TRS(p, (RNG.random() * 3,) * 3, (1, RNG.uniform(.6, 1), RNG.uniform(.5, .9))), 1, bm)
frags = obj('Dünya parçaları', bm, PAINTED, .035, where=C2)
paint(frags, lambda p: (LAND, OCEAN, '#7A5A40')[p.index % 3])
near_rock = obj('Ön plan kaya', ico(1.0, TRS((X2 - 1.5, -6.0, -.6), (.4, .9, .2), (1.3, .9, .8)), 1), M('#6E5E6E'), .05, where=C2)

# ---------------------------------------------------------------- Ç3a — gösterge
C3 = coll('Ç3a Gösterge'); X3 = 200.0
FACE = TRS((X3, 0, 0), (math.pi / 2, 0, 0))   # XY düzlemindeki şekilleri kameraya (−Y) çevirir
obj('Kokpit paneli', box((14, .2, 9), Matrix.Translation((X3, .35, 0))), M(PANEL), 0, where=C3)
obj('Gösterge yüzü', cone(1.1, 1.1, .12, FACE @ Matrix.Translation((0, 0, .0))), M(CREAM), .0, where=C3)
obj('Gösterge çerçevesi', torus(1.13, .075, FACE @ Matrix.Translation((0, 0, .07)), 48), M(INK, False), 0, where=C3)


def dial(a_deg, r):
    a = math.radians(a_deg); return Vector((X3 + r * math.cos(a), -.08, r * math.sin(a)))


bm = bmesh.new()
for a in (150, 130, 110, 90, 70, 50, 30):
    p = dial(a, .86); box((.05, .02, .2), TRS(p, (0, -math.radians(a) + math.pi / 2, 0)), bm)
obj('Çentikler', bm, M(INK, False), 0, where=C3)
bm = bmesh.new()
for a in (154, 146, 138):
    box((.12, .02, .2), TRS(dial(a, .86) + Vector((0, -.005, 0)), (0, -math.radians(a) + math.pi / 2, 0)), bm)
obj('Kırmızı bölge', bm, M(REDZONE, False), 0, where=C3)
bm = box((.2, .03, .26), Matrix.Translation((X3 - .05, -.1, -.45)))
box((.05, .03, .16), Matrix.Translation((X3 + .1, -.1, -.38)), bm); box((.08, .03, .04), Matrix.Translation((X3 + .12, -.1, -.3)), bm)
obj('Yakıt simgesi', bm, M(INK, False), 0, where=C3)
needle = obj('İbre', box((.78, .03, .05), Matrix.Translation((.39, 0, 0))), M(INK, False), 0, where=C3)
needle.location = (X3, -.14, 0)
obj('İbre göbeği', cone(.1, .1, .04, TRS((X3, -.15, 0), (math.pi / 2, 0, 0))), M(INK, False), 0, where=C3)
bm = bmesh.new()
for rr in (.62, .74):   # ibrenin düştüğü açı aralığında iki hareket yayı (gösterge düzleminde)
    torus(rr, .012, TRS((X3, -.16, 0), (math.pi / 2, 0, 0)), 16, 4, bm, math.radians(112), math.radians(148))
motion_arcs = obj('İbre hareket yayları', bm, M(INK, False), 0, where=C3)
light_off = obj('Uyarı ışığı (sönük)', sphere(.13, Matrix.Translation((X3 + 1.5, -.1, .75))), M(LIGHT_OFF), .03, where=C3)
light_on = obj('Uyarı ışığı (yanık)', sphere(.135, Matrix.Translation((X3 + 1.5, -.1, .75))), M(LIGHT_ON, False), .03, where=C3)
bm = bmesh.new()
for a in range(0, 360, 60):
    d = Vector((math.cos(math.radians(a)), 0, math.sin(math.radians(a))))
    between(Vector((X3 + 1.5, -.12, .75)) + d * .22, Vector((X3 + 1.5, -.12, .75)) + d * .38, .018, .018, 6, bm)
rays = obj('Uyarı ışınları', bm, M(LIGHT_ON, False), 0, where=C3)
TIP = Vector((X3 + .28, -.25, .2))
bm = between(TIP + Vector((1.0, -.25, -1.05)), TIP, .17, .15, 16)
sphere(.15, Matrix.Translation(TIP), bm=bm)
box((.9, .55, .65), TRS(TIP + Vector((1.25, -.35, -1.35)), (0, .7, 0)), bm)
finger = obj('Eldiven', bm, M(GLOVE), .035, where=C3)
bm = bmesh.new()
for k in range(3):
    a = math.radians(100 + k * 40)
    d = Vector((math.cos(a), 0, math.sin(a)))
    between(TIP + d * .24 + Vector((0, -.05, 0)), TIP + d * .4 + Vector((0, -.05, 0)), .016, .016, 6, bm)
taps = obj('Vuruş izleri', bm, M(INK, False), 0, where=C3)

# ---------------------------------------------------------------- Ç3b — motor söner (dinamik duman)
C3B = coll('Ç3b Motor'); X3B = 300.0

# ---------------------------------------------------------------- Ç4 — yeni gezegen, yörünge
C4 = coll('Ç4 Yörünge'); X4 = 400.0
PLANET_C, PR = Vector((X4 + 10.5, 9, -10.5)), 8.0
planet = obj('Yeni gezegen', sphere(PR, Matrix.Translation(PLANET_C), 64, 32), PAINTED, .12, where=C4)
paint(planet, lambda p: RUST_DARK if math.sin(3 * (p.center.x - PLANET_C.x) / PR + 2 * (p.center.z - PLANET_C.z) / PR) > .55 else RUST)
obj('Gezegen halkası', torus(PR * 1.45, .11, TRS(PLANET_C, (1.25, .35, .2)), 96, 6), M('#D9A57A'), .03, where=C4)
GLINT_AT = PLANET_C + (Vector((-.62, -.55, .56)).normalized() * PR * 1.01)
bm = box((.9, .04, .06), TRS(GLINT_AT, (0, 0, 0)))
box((.06, .04, .9), TRS(GLINT_AT, (0, 0, 0)), bm); sphere(.12, Matrix.Translation(GLINT_AT), bm=bm)
glint = obj('İpucu parıltısı', bm, M(GLINT, False), 0, where=C4)

# ---------------------------------------------------------------- Ç5 — bulutlar
C5 = coll('Ç5 Bulutlar'); X5 = 500.0


def cloud_layer(name, y, z_rows, spread, size, seed):
    rng = random.Random(seed); bm = bmesh.new()
    for z, x0, x1 in z_rows:
        x = x0
        while x < x1:
            r = rng.uniform(.6, 1.0) * size
            sphere(r, TRS((X5 + x, y + rng.uniform(-.3, .3), z + rng.uniform(0, .25) * size), scale=(1.25, .8, .62)), 20, 10, bm)
            x += r * rng.uniform(1.1, 1.6) * spread
    return obj(name, bm, M(CLOUD_W), .045 * size, where=C5)


cloud_far = cloud_layer('Bulut uzak', 14, [(-4.5, -14, 14), (5.0, -12, 12)], 1.0, 1.4, 1)
cloud_mid = cloud_layer('Bulut orta', 4, [(-3.2, -9, 9), (3.4, -8, 3)], 1.0, 1.0, 2)
cloud_near = cloud_layer('Bulut yakın', -4, [(-2.3, -4.5, -.8), (2.0, 1.2, 4.5)], .9, .8, 3)
CLOUD_BASE = {o: o.location.copy() for o in (cloud_far, cloud_mid, cloud_near)}

# ---------------------------------------------------------------- Ç6 — yer (seçim ekranının ufuk ışığıyla aynı renk)
C6 = coll('Ç6 Yer'); X6 = 600.0
obj('Toprak', box((260, 260, .2), Matrix.Translation((X6, 100, -.1))), M(SOIL), 0, where=C6)
obj('Ufuk ışığı alt', box((300, .2, 7), Matrix.Translation((X6, 90, 2.0))), M(GLOW_LOW, False), 0, where=C6)
obj('Ufuk ışığı üst', box((300, .2, 6), Matrix.Translation((X6, 90.5, 7.5))), M(GLOW_HIGH, False), 0, where=C6)
rng = random.Random(6); bm = bmesh.new()
x = -70
while x < 70:
    h = rng.uniform(3, 8); sphere(rng.uniform(7, 12), TRS((X6 + x, 60 + rng.uniform(-4, 4), -1.5), scale=(1.3, .6, h / 22)), 24, 12, bm); x += rng.uniform(9, 15)
obj('Tepeler', bm, M(HILL), .25, where=C6)
bm = bmesh.new()
for i in range(26):
    p = (X6 + rng.uniform(-14, 14), rng.uniform(-3, 22), 0)
    ico(rng.uniform(.15, .45), TRS(p, (rng.random() * 3,) * 3, (1.3, 1, .55)), 1, bm)
obj('Kayalar', bm, M(ROCK), .03, where=C6)
bm = bmesh.new()
for i in range(18):
    p = Vector((X6 + rng.uniform(-12, 12), rng.uniform(-2, 18), 0))
    for k in range(3):
        a = k * 2.1 + rng.random()
        between(p, p + Vector((math.cos(a) * .25, math.sin(a) * .25, rng.uniform(.45, .8))), .07, .0, 5, bm)
obj('Filizler', bm, M(TUFT), .02, where=C6)
leaves = [obj('Ön yaprak sol', sphere(1.0, TRS((X6 - 2.75, -6.9, -.15), (0, -.75, .1), (1.9, .12, .42)), 24, 12), M(LEAF_FG), .05, where=C6),
          obj('Ön yaprak sol 2', sphere(1.0, TRS((X6 - 2.4, -7.1, -.45), (0, -.35, .2), (1.4, .12, .32)), 24, 12), M(LEAF_FG), .05, where=C6),
          obj('Ön yaprak sağ', sphere(1.0, TRS((X6 + 2.8, -6.9, -.2), (0, .85, -.1), (1.8, .12, .4)), 24, 12), M(LEAF_FG), .05, where=C6)]
LAND_AT = Vector((X6 + 1.6, -.6, 0))   # kameraya yakın: sağ kalan gemi okunur
SKID = obj('Kayma izi', box((3.0, .8, .05), TRS(LAND_AT + Vector((-.9, .2, .02)), (0, 0, .05))), M('#24170F', False), 0, where=C6)

# ---------------------------------------------------------------- panel kurulumları
TEMP = []


def temp(o): TEMP.append(o); return o


def clear_temp():
    for o in TEMP: bpy.data.objects.remove(o, do_unlink=True)
    TEMP.clear()


def puffs(name, points, radii, color=SMOKE, ink=.03):
    bm = bmesh.new()
    for i, (p, r) in enumerate(zip(points, radii)):
        ico(r, TRS(p, (i, i * .5, i * .3)), 2, bm)
    return temp(obj(name, bm, M(color), ink))


def trail_behind(n, start, step, r0, grow, lift=(0, 0, 0)):
    back = (ship_point((-1, 0, 0)) - ship_point((0, 0, 0))).normalized()
    base = ship_point((start, 0, 0))
    pts = [base + back * step * i + Vector(lift) * i * i for i in range(n)]
    return pts, [r0 * (1 + grow * i) for i in range(n)]


def dashes(pts, step=.26, r=.05):
    pts = resample(pts, step); bm = bmesh.new()
    for i in range(0, len(pts) - 1, 2): between(pts[i], pts[i + 1], r, r, 6, bm)
    return temp(obj('Mürekkep yörünge yayı', bm, M(CREAM, False), 0))


def solo(c):
    for k in COLLS.values(): k.hide_render = k is not c


def base(c, world, starfield=True):
    solo(c); sky(world); show(stars, starfield); show(speed, False); show(smear, False); show(flash, False); show(SHIP, False)
    speed.rotation_euler = (0, 0, 0)


def p1(stage):
    def f():
        base(C1, SPACE)
        d = (18.5, 16.8, 15.6, 15.6, 15.6)[stage]
        frame_cam((2.6, -d, 1.4), (2.6, 0, .25), 50, (0, 0, .012, .012, 0)[stage])
        for o in crack_thin: show(o, stage == 1)
        for o in crack_wide: show(o, stage >= 2)
        show(chunks, stage >= 2)
        for o in launch: show(o, stage >= 3)
        if stage == 4:
            frame_cam((2.6, -15.6, 1.4), (7.5, 0, .9), 50); show(smear, True)
    return f


def p2(stage):
    def f():
        base(C2, SPACE); show(near_rock, stage == 1)
        frame_cam((X2, -10, .9), (X2, 0, .35), 35)
        rot = (0, -.38, -.5)
        if stage == 0: place_ship((X2 - 1.9, 0, -.35), rot)
        if stage == 1: place_ship((X2 + 1.7, 0, .55), rot)
        if stage < 2:
            show(speed, True); speed.rotation_euler = (0, 0, .3)
        if stage == 2:
            place_ship((X2 + 1.7, 0, .55), rot)
            port = ship_point((.35, -.45, .06)); out = (ship_point((.35, -1.45, .06)) - port).normalized()
            frame_cam(port + out * 1.2, port, 50)   # lomboz, Ç3a'daki göstergeyle aynı boyda
    return f


def p3a(stage):
    def f():
        base(C3, PANEL, False)
        frame_cam((X3 + .25, -5.2, .05), (X3 + .25, 0, .05), 50)
        needle.rotation_euler = (0, -math.radians(152), 0)
        show(motion_arcs, stage == 0); show(light_off, stage == 0); show(light_on, stage == 1); show(rays, stage == 1)
        show(finger, stage == 1); show(taps, stage == 1)
    return f


def p3b(stage):
    def f():
        base(C3B, SPACE)
        place_ship((X3B, 0, 0), (.15 * stage, 0, .08 * stage), 1.0, (.38, 0)[stage])
        frame_cam((X3B - 4.6, -2.3, 1.0), (X3B - .1, 0, .05), 35)
        if stage == 0:
            puffs('Öksürük dumanı', [ship_point((-2.0, .1, .25)), ship_point((-2.6, -.3, -.15))], [.28, .2])
        else:
            puffs('Son duman', [ship_point((-3.4, .2, .45))], [.16])
    return f


ARC_START, ARC_MID = Vector((X4 - 6.5, 0, .35)), Vector((X4 - 1.9, 0, .2))   # ARC_MID = Ç4a'daki gemi
BEND_AT = Vector((X4 + 1.0, 0, -.55))


def orbit_path(tail):
    """Düz süzülüş, sonra gezegene doğru kıvrılma: geminin arkasında kalan iz."""
    return [ARC_START] + bezier(ARC_MID, ARC_MID - ARC_START, tail)


def p4(stage, with_glint=False):
    def f():
        base(C4, SPACE); show(glint, with_glint)
        if stage == 0:
            frame_cam((X4, -14, 1.5), (X4, 0, 0), 35)
            place_ship(ARC_MID, (.1, 0, .12), .42, 0)   # Ç3b2'de gözün baktığı ekran noktası
        if stage == 1:
            frame_cam((X4 + .6, -14, 1.2), (X4 + 1.2, 0, -.6), 35)
            place_ship(BEND_AT, (.1, .55, .05), .42, 0)
            dashes(orbit_path(ship_point((-1.6, 0, 0))))
        if stage == 2:
            frame_cam((X4 + 1.8, -12, .3), (X4 + 2.6, 0, -2.6), 35, -.06)
            place_ship((X4 + 3.0, 0, -3.0), (.1, 1.0, .05), .42, 0)
            p = orbit_path(BEND_AT)
            p += bezier(BEND_AT, p[-1] - p[-2], ship_point((-1.6, 0, 0)), 12)[1:]
            dashes(p); show(speed, True); speed.rotation_euler = (0, 0, -1.0)
        if stage == 3:
            frame_cam((X4 + 1.8, -12, .3), (X4 + 2.6, 0, -2.6), 35); show(flash, True)
    return f


def p5(stage):
    def f():
        base(C5, SKY_CLOUD, False)
        frame_cam((X5, -10, 0), (X5, 0, 0), 35)
        lift = (0, 1.8)[stage]
        for o, k in ((cloud_far, .25), (cloud_mid, .55), (cloud_near, 1.0)):
            o.location = CLOUD_BASE[o] + Vector((0, 0, lift * k))
        rot = (0, .8, -.35)
        if stage == 0:
            place_ship((X5 + .2, 0, .7), rot, .9, 0, hot=True)
            show(speed, True); speed.rotation_euler = (0, 0, -.8)
            pts, rr = trail_behind(6, -1.7, .7, .22, .25); puffs('Duman izi', pts, rr)
        else:
            place_ship((X5 + 4.0, 0, -2.45), rot, .9, 0)
            pts, rr = trail_behind(12, -1.7, .75, .22, .2); puffs('Duman izi', pts, rr)
    return f


def p6(stage, with_leaves=True):
    def f():
        base(C6, SKY_GROUND, False)
        for o in leaves: show(o, with_leaves)
        show(SKID, stage == 2)
        roll = (0, .05, 0)[stage]
        frame_cam((X6, -10.5, .95), (X6, 0, 1.9), 28, roll)
        if stage == 0:
            place_ship((X6 - 4.6, 3, 5.0), (0, .62, -.25), .8, 0)
            pts, rr = trail_behind(7, -1.6, .65, .2, .28); puffs('Duman izi', pts, rr)
        if stage == 1:
            place_ship(LAND_AT + Vector((-.4, 0, .5)), (0, .55, -.25), .8, 0)
            c = LAND_AT + Vector((.3, 0, .2)); pts, rr = [], []
            for i in range(14):
                a = i / 14 * math.pi
                pts.append(c + Vector((math.cos(a) * RNG.uniform(.9, 2.0), RNG.uniform(-.4, .8), math.sin(a) * RNG.uniform(.3, 1.3))))
                rr.append(RNG.uniform(.3, .6))
            puffs('Toz patlaması', pts, rr, DUST, .035)
            bm = bmesh.new()
            for i in range(9):
                ico(RNG.uniform(.05, .11), Matrix.Translation(c + Vector((RNG.uniform(-2.2, 2.2), RNG.uniform(-.5, .5), RNG.uniform(1.4, 2.6)))), 1, bm)
            temp(obj('Savrulan taşlar', bm, M(ROCK), .02))
        if stage == 2:
            place_ship(LAND_AT + Vector((0, 0, .58)), (.12, .3, -.25), .8, 0, lit=True)
            c = LAND_AT + Vector((.5, .2, .15))
            puffs('Yatışan toz', [c + Vector((-1.3, .2, 0)), c + Vector((1.1, .4, 0)), c + Vector((.2, .6, 0))], [.4, .34, .28], DUST, .03)
    return f


# (id, başlangıç karesi, açıklama, kurulum) — bitiş karesi bir sonraki panelin başlangıcıdır
PLAN = [
    ('1a', 0, 'sakin Dünya (önce hâli)', p1(0)),
    ('1b', 10, 'ilk çatlak', p1(1)),
    ('1c', 22, 'ikinci çatlak, kabuk kalkar', p1(2)),
    ('1d', 34, 'gemi kenardan fırlar', p1(3)),
    ('1e', 42, 'kamçı dönüş (geçiş)', p1(4)),
    ('2a', 48, 'kaçış, Dünya ikiye ayrılır', p2(0)),
    ('2b', 64, 'kaya kıl payı sıyırır', p2(1)),
    ('2c', 80, 'lomboza yaklaşma (geçiş: yuvarlak > yuvarlak)', p2(2)),
    ('3a1', 96, 'ibre boşa düşer', p3a(0)),
    ('3a2', 104, 'parmak cama vurur, bip bip', p3a(1)),
    ('3b1', 114, 'motor öksürür', p3b(0)),
    ('3b2', 125, 'motor söner, sessizlik', p3b(1)),
    ('4a', 132, 'süzülüş, yeni gezegen', p4(0)),
    ('4b', 148, 'rota yakalanır, mürekkep yay', p4(1)),
    ('4c', 164, 'hızlanarak düşüş', p4(2)),
    ('4d', 176, 'atmosfer parıltısı (geçiş)', p4(3)),
    ('5a', 180, 'bulutların içinde', p5(0)),
    ('5b', 196, 'sağ alttan çıkış (geçiş)', p5(1)),
    ('6a', 204, 'sol üstten giriş', p6(0)),
    ('6b', 213, 'çarpma, toz', p6(1)),
    ('6c', 228, 'duruş, ışık yanar', p6(2)),
]
END = 240
EXTRA = [('4b_ipucu', 'İPUCU: yeşil parıltı (2 kare)', p4(1, True)), ('6a_yapraksiz', 'yapraksız seçenek', p6(0, False))]


def shot_of(pid): return 'Ç' + pid[0] + ('a' if pid.startswith('3a') else 'b' if pid.startswith('3b') else '')


def render(pid, text, setup, f0):
    clear_temp(); setup()
    set_label(f"{shot_of(pid)} · {f0 / FPS:.2f}".replace('.', ',') + f" sn · {text}")
    path = PANELS / f'{pid}.png'
    s.render.filepath = str(path); bpy.ops.render.render(write_still=True)
    return path.name


panels = []
for i, (pid, f0, text, setup) in enumerate(PLAN):
    f1 = PLAN[i + 1][1] if i + 1 < len(PLAN) else END
    panels.append({'id': pid, 'file': render(pid, text, setup, f0), 'f0': f0, 'f1': f1, 'label': text})
extras = []
for pid, text, setup in EXTRA:
    f0 = 150 if pid.startswith('4b') else 204
    extras.append({'id': pid, 'file': render(pid, text, setup, f0), 'label': text})
clear_temp()
json.dump({'fps': FPS, 'frames': END, 'panels': panels, 'extras': extras}, open(OUT / 'paneller.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'IntroAnimatic.blend'))
print('INTRO ANIMATIC PANELS DONE', len(panels), len(extras))
