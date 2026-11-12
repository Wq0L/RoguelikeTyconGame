"""Ana menü arka planı — Blender basit şekil iskeleti (blockout), 11 Kasım 2026.
Kullanıcı: "Blender ile Higgsfield'ı birleştir; bu sefer sadece basit şekiller yap, Higgsfield AI ile tamamlasın"
ve "daha chill, menü olduğu belli olan bir şey" (roket hikâyeyi ele veriyordu, çıkarıldı).
Kompozisyon, kamera ve ışık bizim: Higgsfield yalnız bu iskeletin üstünü boyar. Ortası boş (başlık + butonlar).
Sakin bir bahçe gecesi: solda fener + saksılar + bank, sağda kristal saksı + fener, arkada ışığı yanan sera kubbesi,
iki köşede ip ışıklar, ay, ateşböcekleri.

  blender -b --factory-startup -P Tools/Art/MenuScene/build_menu_blockout.py
Çıktı: Docs/MenuArkaPlan/Blender/blockout/iskelet.png (1920×1080), MenuIskelet.blend
Özgün kaba geometri; dış model, doku, eklenti yok.
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Matrix, Euler

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'MenuArkaPlan' / 'Blender' / 'blockout'; OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 16
except Exception: pass
s.render.resolution_x, s.render.resolution_y = 1920, 1080
s.render.image_settings.file_format = 'PNG'


def C(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return (*[((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c], 1.0)


KEY = Vector((-.55, -.45, .7)).normalized()
MATS = {}


def mat(color, flat=False):
    """Basit toon: üç parlaklık basamağı (biçim okunsun), flat: ışıktan bağımsız (ışık kaynakları)."""
    key = (color, flat)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(color); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    em, out = nt.nodes.new('ShaderNodeEmission'), nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    if flat: em.inputs['Color'].default_value = C(color)
    else:
        geo = nt.nodes.new('ShaderNodeNewGeometry'); d = nt.nodes.new('ShaderNodeVectorMath'); d.operation = 'DOT_PRODUCT'
        nt.links.new(geo.outputs['Normal'], d.inputs[0]); d.inputs[1].default_value = KEY
        def m_(o, a, b):
            n = nt.nodes.new('ShaderNodeMath'); n.operation = o
            for i, v in enumerate((a, b)):
                if isinstance(v, bpy.types.NodeSocket): nt.links.new(v, n.inputs[i])
                else: n.inputs[i].default_value = v
            return n.outputs[0]
        band = m_('ADD', m_('ADD', .6, m_('MULTIPLY', m_('GREATER_THAN', d.outputs['Value'], -.12), .22)),
                  m_('MULTIPLY', m_('GREATER_THAN', d.outputs['Value'], .55), .18))
        sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'; sc.inputs[0].default_value = C(color)[:3]
        nt.links.new(band, sc.inputs['Scale']); nt.links.new(sc.outputs[0], em.inputs['Color'])
    MATS[key] = m; return m


def TRS(at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return Matrix.Translation(at) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))


def add(name, build, color, flat=False, smooth=False):
    bm = bmesh.new(); build(bm)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    if smooth: me.shade_smooth()
    o = bpy.data.objects.new(name, me); s.collection.objects.link(o); o.data.materials.append(mat(color, flat)); return o


def box(size, m=Matrix()):
    return lambda bm: bmesh.ops.create_cube(bm, size=1, matrix=m @ Matrix.Diagonal((*size, 1)))


def sph(r, m=Matrix(), u=24, v=12):
    return lambda bm: bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r, matrix=m)


def cyl(r1, r2, h, m=Matrix(), seg=16):
    return lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h, matrix=m)


def many(*fns):
    def f(bm):
        for fn in fns: fn(bm)
    return f


R = random.Random(4)

# gök: gece laciverti, ufka doğru mor (yön gradyanı)
w = bpy.data.worlds.new('Gök'); s.world = w; w.use_nodes = True; nt = w.node_tree
tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(tc.outputs['Generated'], sep.inputs[0])
ramp = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(sep.outputs['Z'], ramp.inputs[0])
ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .0, C('#6A4378')
ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .32, C('#1C2246')
nt.links.new(ramp.outputs[0], nt.nodes['Background'].inputs['Color'])

cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam); s.camera = cam
cam.data.lens, cam.data.clip_end = 26, 900
cam.location = Vector((0, -17.5, 2.6)); cam.rotation_euler = (Vector((0, 8, 2.9)) - cam.location).to_track_quat('-Z', 'Y').to_euler()

# zemin (bahçe: koyu toprak + çimen lekeleri), uzak tepeler, ufuk ışığı
add('Zemin', box((500, 500, .2), Matrix.Translation((0, 200, -.1))), '#7A4A3A')
add('Çimen lekeleri', many(*[sph(R.uniform(2.5, 5), TRS((x, y, .01), scale=(1.6, 1, .02)), 16, 6)
                            for x, y in ((-8, -2), (-13, 5), (-5, 7), (8, -2), (12, 6), (16, 14), (-17, 11))]), '#4E7A4A')
add('Basamak taşları', many(*[sph(.75, TRS((x, y, .02), scale=(1.3, .8, .04)), 16, 4) for x, y in ((1.2, -9), (2.6, -5.5), (4.4, -2.0), (6.6, 1.6), (9.0, 5.4), (11.4, 9.4), (13.4, 13.6))]), '#B89A84')
add('Ufuk ışığı', box((700, .2, 4), Matrix.Translation((0, 160, 1.0))), '#C8784A', flat=True)
add('Uzak tepeler', many(*[sph(R.uniform(10, 16), TRS((x, 110 + R.uniform(-8, 8), -2), scale=(1.5, .6, R.uniform(.35, .7))), 16, 8)
                          for x in range(-200, 201, 22)]), '#3A2A48')
add('Yakın sırtlar', many(*[sph(R.uniform(6, 8), TRS((x, 46, -1.5), scale=(1.7, .7, .45)), 16, 8) for x in (-30, -19, 21, 31)]), '#4A3048')

# ay ve yıldızlar (ortanın üstü boş)
add('Ay', sph(6, Matrix.Translation((-48, 130, 50))), '#F1E3C2', flat=True)
add('Yıldızlar', many(*[sph(R.uniform(.25, .6), Matrix.Translation((x, 300, z)), 8, 4)
                       for x, z in ((R.uniform(-260, 260), R.uniform(40, 170)) for _ in range(90)) if not (abs(x) < 70 and z < 120)]), '#FFF2D0', flat=True)

# saksılar ve bitkiler
def planter(name, at, w_, d_, h_, rot):
    m = TRS(at, (0, 0, rot))
    add(f'{name} gövde', box((w_, d_, h_), m @ Matrix.Translation((0, 0, h_ / 2))), '#C8643E')
    add(f'{name} toprak', box((w_ - .3, d_ - .3, .15), m @ Matrix.Translation((0, 0, h_ + .02))), '#3A2830')
    return m


m1 = planter('Saksı sol', (-7.4, -3.5, 0), 3.6, 2.0, 1.1, math.radians(-12))
add('Sol bitki gövdeleri', many(*[sph(r, m1 @ TRS((x, 0, 1.2 + r * .8), scale=(1, 1, 1.25)), 16, 8) for x, r in ((-1.1, .9), (.3, 1.15), (1.4, .75))]), '#5BAE52', smooth=True)
add('Sol bitki meyveleri', many(*[sph(.32, m1 @ Matrix.Translation(p), 12, 6) for p in ((-1.2, -.6, 2.2), (.5, -.8, 2.7), (1.3, -.5, 1.9))]), '#F2A23A')
m2 = planter('Saksı sağ', (7.6, -3.0, 0), 3.6, 2.0, 1.1, math.radians(12))
add('Kristaller mor', many(*[cyl(r, 0, h, m2 @ TRS((x, y, 1.2 + h / 2), (t, 0, 0)), 6) for x, y, r, h, t in ((-.8, 0, .45, 2.6, -.1), (.3, .2, .55, 3.4, .06))]), '#9B6AD0')
add('Kristaller mavi', many(*[cyl(r, 0, h, m2 @ TRS((x, y, 1.2 + h / 2), (t, 0, 0)), 6) for x, y, r, h, t in ((1.3, -.1, .4, 2.0, .18), (-.1, -.5, .32, 1.5, -.22))]), '#4FA3D6')
m3 = planter('Saksı sol arka', (-14.0, 9.0, 0), 3.2, 1.8, 1.0, math.radians(18))
add('Sol arka bitki', many(*[sph(r, m3 @ TRS((x, 0, 1.0 + r * .7), scale=(1, 1, 1.3)), 14, 7) for x, r in ((-.7, .8), (.7, .7))]), '#4E9A50', smooth=True)
m4 = planter('Saksı sağ arka', (13.0, 9.0, 0), 3.4, 1.9, 1.0, math.radians(-14))
add('Sağ arka bitki', many(*[sph(r, m4 @ TRS((x, 0, 1.1 + r * .7), scale=(1, 1, 1.3)), 14, 7) for x, r in ((-.9, .8), (.6, .9))]), '#4E9A50', smooth=True)

# bank (dinlenme: menü hissi), solda saksının yanında
mb = TRS((-10.8, 3.6, 0), (0, 0, math.radians(18)))
add('Bank', many(box((3.6, 1.0, .25), mb @ Matrix.Translation((0, 0, .9))), box((3.6, .2, .9), mb @ Matrix.Translation((0, .45, 1.45))),
                 *[box((.2, .8, .9), mb @ Matrix.Translation((x, 0, .45))) for x in (-1.5, 1.5)]), '#8A5A3A')

# fenerler: direk + sıcak ışıklı kutu + yere düşen ışık lekesi
for name, at in (('Fener sol', (-4.4, .6, 0)), ('Fener sağ', (10.2, 3.2, 0))):
    x, y, _ = at
    add(f'{name} direk', cyl(.12, .12, 4.2, Matrix.Translation((x, y, 2.1)), 10), '#3A2E36')
    add(f'{name} kutu', box((.7, .7, .9), Matrix.Translation((x, y, 4.55))), '#FFD27A', flat=True)
    add(f'{name} şapka', cyl(.6, .1, .4, Matrix.Translation((x, y, 5.2)), 4), '#3A2E36')
    add(f'{name} ışık lekesi', sph(2.6, TRS((x, y, .02), scale=(1, 1, .01)), 24, 4), '#B9774E', flat=True)

# ip ışıklar: iki köşede sarkan kordon ve ampuller (ortayı kapatmaz)
def string_lights(name, p0, p1, sag, n):
    p0, p1 = Vector(p0), Vector(p1); pts = []
    for i in range(n + 1):
        t = i / n; p = p0.lerp(p1, t); p.z -= sag * 4 * t * (1 - t); pts.append(p)
    add(f'{name} kordon', many(*[cyl(.03, .03, (b - a).length, Matrix.Translation((a + b) / 2) @ (b - a).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4(), 6)
                                 for a, b in zip(pts, pts[1:])]), '#2A2230')
    add(f'{name} ampuller', many(*[sph(.16, Matrix.Translation(p - Vector((0, 0, .18))), 10, 5) for p in pts[1:-1:2]]), '#FFE6A0', flat=True)


string_lights('İp ışık sol', (-4.4, .6, 5.0), (-17, 8, 8.0), 1.3, 16)
string_lights('İp ışık sağ', (10.2, 3.2, 5.0), (21, 9, 7.6), 1.1, 14)

# sera kubbesi (ışığı yanan), arkada sağda
md = Matrix.Translation((17.0, 30.0, 0))
bm_dome = bmesh.new(); bmesh.ops.create_uvsphere(bm_dome, u_segments=24, v_segments=12, radius=5.6, matrix=md)
bmesh.ops.delete(bm_dome, geom=[v for v in bm_dome.verts if v.co.z < -.05], context='VERTS')
me = bpy.data.meshes.new('Sera'); bm_dome.to_mesh(me); bm_dome.free()
o = bpy.data.objects.new('Sera', me); s.collection.objects.link(o); o.data.materials.append(mat('#9EC7DA'))
add('Sera ışığı', box((6.4, .2, 2.4), md @ Matrix.Translation((0, -4.6, 1.2))), '#FFD27A', flat=True)

# ateşböcekleri (iki yanda)
add('Ateşböcekleri', many(*[sph(.09, Matrix.Translation((R.choice((-1, 1)) * R.uniform(4, 14), R.uniform(-6, 6), R.uniform(1.2, 4.5))), 8, 4) for _ in range(18)]), '#FFF3A8', flat=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'MenuIskelet.blend'))
s.render.filepath = str(OUT / 'iskelet.png'); bpy.ops.render.render(write_still=True)
print('BLOCKOUT DONE')
