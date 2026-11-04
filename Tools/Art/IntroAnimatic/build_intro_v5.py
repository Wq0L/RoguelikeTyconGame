"""Açılış videosu — previs v5 (28 Ekim 2026). Docs/AcilisVideosu-CekimListesi.md (taslak 5).
  blender -b --factory-startup -t 4 -P Tools/Art/IntroAnimatic/build_intro_v5.py -- all
  blender ... -- 10 40 120        (yalnız bu kareler: deneme, kareler/deneme_####.png)
Çıktı: Docs/AcilisVideosu/Animatik/v5/kareler/####.png (354 kare, 14,75 sn), kareler_ipucu/, IntroV5.blend.
v4'ten farkı (kullanıcı: "her geçiş başka bir video gibi, tek kurgu/tek bütün hissi yok"; "toonic olmalı, cartoon
olmuş"):
- TEK DÜNYA, KESİNTİSİZ KAMERA: Dünya (R=10), kaçış yolu, gezegen aynı uzayda; yıldızlar dünyada sabit; güneş yönü
  baştan sona aynı. Kamera yüzeyden kalkıp alev topunu kovalar, yanına yetişir (ateş dağılır: roket), gövdeye geçip
  küçülen Dünya'ya bakar; lombozdan kokpite girer, camdan çıkar, motor söner, geri çekilir, roket gezegene kıvrılıp
  bulutlarına dalar; bulutun içinden inen kamera yere yaklaşıp alçalır. Yalnız iki görünmez kesme: lomboz camı ve
  bulutun içi (kokpit ve iniş ayrı setler).
- OYUNUN TOON DİLİ (Assets/Resources/ThornComicToon.shader): aynı rengin üç parlaklık basamağı (×1 / ×0,8 / ×0,57,
  eşikler .55 / −.12), sabit kalınlıkta mürekkep kabuğu, az poligonlu düz gölgeli (yüzeyli) biçimler. Freestyle,
  tram ve titrek çizgi (v4'ün "cartoon" havası) yok. Kenar ışığı yalnız yanan Dünya'nın roketi aydınlattığı yerde.
Roket her karede, ikincil nesneler ikişer karede, kamera her karede. Donma yok.
Özgün kaba geometri; dış model/doku/eklenti yok.
"""
import bpy, bmesh, math, random, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion, Euler, noise as mnoise
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parents[3]
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['all']
HD = 'hd' in ARGS; ARGS = [a for a in ARGS if a != 'hd']   # "-- all hd": 1920×1080 üretim çıktısı (v5_1080/)
OUT = ROOT / 'Docs' / 'AcilisVideosu' / 'Animatik' / ('v5_1080' if HD else 'v5')
FR, FR_GLINT = OUT / 'kareler', OUT / 'kareler_ipucu'
for d in (FR, FR_GLINT): d.mkdir(parents=True, exist_ok=True)
FPS, END, IMPACT, GLINT_F = 24, 354, 316, 234
SHOCK, T3A, T3C, TDIVE = 140, 154, 172, 282   # şok çarpar; camdan kokpite; camdan dışarı; bulutun içinde iniş seti
# v5.5: kokpit 30 → 18 kare, el yok (kullanıcı: "el olmasın, daha kısa, yalnız yakıtın bittiği görünsün");
# kazanılan 12 kare dışarıdaki teklemeye gitti: önce bir duman, sonra tık tık tık. Kokpitten sonrası aynı karelerde.
# Üç kesintisiz bölüm + kokpit: A (yüzey → kaçış → lomboz), B (kokpit), C (camdan çıkış → gezegen → bulut), D (iniş)
SHOTS = [('A', 0), ('B', T3A), ('C', T3C), ('D', TDIVE)]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'CONSTANT'   # ikişer kare: aradaki kare tutulur
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 24 if HD else 12
except Exception: pass
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = (1920, 1080, 100) if HD else (960, 540, 100)
s.render.image_settings.file_format = 'PNG'
s.render.fps, s.frame_start, s.frame_end = FPS, 1, END

NOLINE = bpy.data.collections.new('Çizgisiz'); s.collection.children.link(NOLINE)   # HUD, iz, ışık: kontur almaz


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


def toon(base, shade=None, hi=None, tex=None, rim=1.0, name=None):
    """Oyunun toon'u (ThornComicToon.shader): aynı rengin üç parlaklık basamağı — n > .55 → ×1, n > −.12 → ×0,8,
    yoksa ×0,57. Ton kaydırma, tram, parlak vurgu yok. Kenar ışığı yalnız RimAmount > 0 olan karelerde (yanan Dünya).
    tex: (nt) -> (taban, _) soketleri; shade/hi v4 imzası için kabul edilir, kullanılmaz."""
    key = (base, tex, rim)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name or f'Toon {base}'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    geo = node(nt, 'ShaderNodeNewGeometry')
    dk = dot_to(nt, g.outputs['Key'], geo); dr = dot_to(nt, g.outputs['Rim'], geo)
    band = op(nt, 'ADD', op(nt, 'ADD', .57, op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, -.12), .23)),
              op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, .55), .2))
    lw = node(nt, 'ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = .3
    rimf = op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', lw.outputs['Facing'], g.outputs['RimEdge']), op(nt, 'GREATER_THAN', dr, .12))
    rimf = op(nt, 'MULTIPLY', op(nt, 'MULTIPLY', rimf, g.outputs['RimAmount']), rim)
    b = tex(nt)[0] if tex else C(base)
    sc = node(nt, 'ShaderNodeVectorMath', operation='SCALE'); put(nt, sc.inputs[0], b if isinstance(b, bpy.types.NodeSocket) else b[:3])
    nt.links.new(band, sc.inputs['Scale'])
    c = mix(nt, 1.0, sc.outputs[0], (0, 0, 0, 1), 'ADD')
    c = mix(nt, rimf, c, g.outputs['RimColor'])
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


def wobble(bm, amp, freq, seed=0.0, center=None):
    """El yapımı düzensizlik: noktaları merkezden dışa doğru gürültüyle iter (kusursuz daire CG kokuyordu)."""
    c = Vector(center) if center is not None else sum((v.co for v in bm.verts), Vector()) / max(len(bm.verts), 1)
    for v in bm.verts:
        d = v.co - c
        if d.length > 1e-6: v.co += d.normalized() * amp * mnoise.noise(v.co * freq + Vector((seed, seed * .3, 0)))
    return bm


def puff(seed, bm=None):
    """Topak duman/toz: üst üste binen 4–5 düzensiz küre; çizgi kesişimlerde kabarık hat çizer."""
    r = random.Random(seed); bm = bm or bmesh.new(); sphere(1.0, Matrix(), 16, 8, bm)
    for _ in range(4):
        a, e = r.uniform(0, 2 * math.pi), r.uniform(-.3, .7)
        sphere(r.uniform(.5, .74), Matrix.Translation(Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * r.uniform(.55, .82)), 16, 8, bm)
    return wobble(bm, .1, 2.2, seed, (0, 0, 0))


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


BRUSH_MIN, BRUSH_GAIN = .3, 1.35   # fırça mürekkebi: en ince yer ×0,4, en kalın ×1,35 (ortalama v5.2 kalınlığına yakın)


def brush_ink(o, md, seed=0.0):
    """v5.3 fırça mürekkebi (GPT stil karesi hedef konsept): kabuk kalınlığı nesne boyunca gürültüyle incelip
    kalınlaşır. Gürültü nesnenin kendi boyuna göre ölçeklenir; sabit tohum, kareden kareye titremez."""
    vs = o.data.vertices
    if not vs: return
    lo = Vector([min(v.co[i] for v in vs) for i in range(3)]); hi = Vector([max(v.co[i] for v in vs) for i in range(3)])
    fr = 2.2 / max((hi - lo).length, 1e-3); off = Vector((seed, seed * .37, seed * .71))
    vg = o.vertex_groups.new(name='Fırça')
    for v in vs: vg.add([v.index], clamp(.55 + .8 * mnoise.noise(v.co * fr + off)), 'REPLACE')
    md.vertex_group, md.thickness_vertex_group = vg.name, BRUSH_MIN / BRUSH_GAIN
    md.thickness *= BRUSH_GAIN


def obj(name, bm, mats, ink=0.0, parent=None, smooth=False, at=None, toggle=False, lines=None):
    """ink > 0: oyundaki gibi mürekkep kabuğu (ters kabuk), v5.3'ten beri fırça gibi incelip kalınlaşır. Varsayılan
    düz gölge: yüzeyler görünür (oyunun az poligonlu toon'u). lines yalnız v4 imzası için."""
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
# v5.3 boyalı bulutsu (GPT stil karesi hedef konsept): bakış yönüne bağlı gürültü dünyada sabit, kamera dönünce gök de döner.
# Oyunun toon dili: koyu lacivert tabanın üstünde iki sert basamak (bir ton açık lacivert, bir ton mor-lacivert).
# Taban rengi SKY_RGB'de kare kare anahtarlanır; NEB_AMT atmosferde, kokpitte ve beyaz çarpma karesinde 0.
WN = world.node_tree
SKY_RGB, NEB_AMT = node(WN, 'ShaderNodeRGB'), node(WN, 'ShaderNodeValue')
_tc = node(WN, 'ShaderNodeTexCoord')
_neb = []
for sc, det, rough in ((2.4, 3.0, .55), (.75, 2.0, .5)):   # ayrıntı 3: kenarlar kâğıt kesiği gibi temiz, gürültü lekesi yok
    nz = node(WN, 'ShaderNodeTexNoise'); WN.links.new(_tc.outputs['Generated'], nz.inputs['Vector'])
    nz.inputs['Scale'].default_value, nz.inputs['Detail'].default_value, nz.inputs['Roughness'].default_value = sc, det, rough
    _neb.append(nz.outputs['Fac'])
_nf = op(WN, 'ADD', op(WN, 'MULTIPLY', _neb[0], .6), op(WN, 'MULTIPLY', _neb[1], .4))
NEB1, NEB2 = '#252B47', '#2F305C'   # lacivert kalsın: ikinci basamakta yalnız bir tutam mor
_d1 = tuple(a - b for a, b in zip(C(NEB1)[:3], C(SPACE)[:3])); _d2 = tuple(a - b for a, b in zip(C(NEB2)[:3], C(NEB1)[:3]))
_sky = mix(WN, op(WN, 'MULTIPLY', op(WN, 'GREATER_THAN', _nf, .54), NEB_AMT.outputs[0]), SKY_RGB.outputs[0], (*_d1, 1), 'ADD')
_sky = mix(WN, op(WN, 'MULTIPLY', op(WN, 'GREATER_THAN', _nf, .61), NEB_AMT.outputs[0]), _sky, (*_d2, 1), 'ADD')
WN.links.new(_sky, BG.inputs['Color'])
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
NOSE_HEAT = []   # v5.12: kızgın burun çarpmaya kadar sarı kalır, sonra yavaşça kendi kırmızısına soğur (takeD anahtarlar)


def _nose_heat_tex(nt):
    v = node(nt, 'ShaderNodeValue'); v.outputs[0].default_value = 1.0; NOSE_HEAT.append(v)
    # sarı → kızgın turuncu → kendi kırmızısı (doğrudan karışım arada bulanık, kahvemsi bir turuncu veriyordu; görüldü)
    hot_half = mix(nt, op(nt, 'SUBTRACT', op(nt, 'MULTIPLY', v.outputs[0], 2.0), 1.0), C(RED_HI), C(HOT))
    return mix(nt, op(nt, 'MULTIPLY', v.outputs[0], 2.0), C(NOSE), hot_half), None


nose_hot = obj('Burun (ısınmış)', cone(.46, .08, 1.02, Matrix.Translation((1.6, 0, 0)) @ ROT_Z_TO_X, 32), toon(HOT, tex=_nose_heat_tex), .028, SHIP, toggle=True)
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
    """Kalın plaka: üst yüz Dünya, yan ve alt kaya, sabit kalınlıkta mürekkep kabuğu (oyundaki gibi)."""
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.shade_smooth()
    o = bpy.data.objects.new(name, me); s.collection.objects.link(o)
    for m in (top_mat, ROCK_M, INK_M, INK_M): o.data.materials.append(m)
    t = o.modifiers.new('Kalınlık', 'SOLIDIFY'); t.thickness, t.offset, t.use_rim = thickness, -1.0, True
    t.material_offset, t.material_offset_rim = 1, 1
    k = o.modifiers.new('Mürekkep kabuk', 'SOLIDIFY')
    k.thickness, k.offset, k.use_flip_normals, k.use_rim, k.material_offset = ink, 1.0, True, False, 2
    brush_ink(o, k, len(name) * 1.3)
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
# Patlamanın dumanı: yükselip kabarır; Ç2a'daki bulut bu dumandır (olaylar arası bağ)
plume1 = pool('Ç1 duman', 9, lambda i: puff(140 + i), toon(ASH, ASH_SH), .05)
PLUME1 = [(Vector((RNG.uniform(-1.2, 1.2), RNG.uniform(-.6, 1.0), 0)), RNG.uniform(1.2, 3.2), RNG.uniform(.55, 1.05)) for _ in range(9)]


# ================================================================ v5 — TEK DÜNYA: Dünya, kaçış yolu, gezegen aynı uzayda
LDIR = Vector((3.2, -.5, 3.8)).normalized()          # Ç1'deki fırlayışın yönü: bütün kaçış bu doğrultuda
ZL = D1 * R1 + D1 * .4                                # fırlayış noktası (çatlağın ortası)
SIDE = LDIR.cross(Vector((0, 0, 1))).normalized()     # kameranın tarafı: Ç1 kamerasıyla aynı yan (−Y)
UPV = SIDE.cross(LDIR).normalized()
SWAP, REVEAL0, REVEAL1 = 64, 58, 72   # yüzey Dünyası → bütün plakalı Dünya (kamera yukarı bakarken); ateş dağılır
V60 = 5.0 * (1 + 8 * 16 / FPS) / FPS  # f60'taki hız: alev topu formülünün türevi (hız eğrisi kopmaz)


def _speed(f):
    """Birim/kare: motorla hızlanır, motor sönünce yavaşlar (itki kaybı), yakalanmada yeniden hızlanır."""
    if f < 140: return V60 + (5.0 - V60) * ease_in((f - 60) / 80)
    if f < 202: return 5.0
    return 5.0 - 3.0 * ease((f - 202) / 30)


SS = {}
for f_ in range(0, 233):
    if f_ <= 60: a_ = max(f_ - 44, 0) / FPS; SS[f_] = 5.0 * a_ * (1 + 4 * a_)
    else: SS[f_] = SS[f_ - 1] + _speed(f_ - 1)
P232 = ZL + LDIR * SS[232]
PLR = 40.0
PL = P232 + LDIR * 125 - UPV * 78 + SIDE * 10                          # Eri'nin gezegeni: yolun önünde, aşağıda
E_IN = PL + (P232 + LDIR * 95 - PL).normalized() * (PLR + 1.2)          # atmosfere giriş noktası
CAP = bezier(P232, LDIR, E_IN, 80)
CAP_LEN = [0.0]
for a_, b_ in zip(CAP, CAP[1:]): CAP_LEN.append(CAP_LEN[-1] + (b_ - a_).length)
K_CAP = clamp(2.0 * 49 / CAP_LEN[-1], .05, .95)   # yakalanma başında hız = süzülüş hızı (2 birim/kare)


def path(f):
    """Roketin baştan sona tek yolu (f ≤ 281)."""
    if f <= 232: return ZL + LDIR * SS[max(0, f)]
    u = clamp((f - 232) / 49); s_ = CAP_LEN[-1] * (K_CAP * u + (1 - K_CAP) * u * u)
    j = 0
    while j < len(CAP_LEN) - 2 and CAP_LEN[j + 1] < s_: j += 1
    return CAP[j].lerp(CAP[j + 1], clamp((s_ - CAP_LEN[j]) / max(CAP_LEN[j + 1] - CAP_LEN[j], 1e-9)))


def tangent(f):
    return (path(f + 1) - path(f - 1)).normalized() if f > 45 else LDIR.copy()


# Dünya bir bütün olarak parçalanır: yüzeydeki aynı malzeme ve doku (Ç1'le aynı Dünya)
plates_full = sphere_plates('Dünya', R1 + .012, 16, 5, EARTH_M1, 1.6, (0, 0, 0), sub=4, ink=.06)
TOGGLE.extend(p['obj'] for p in plates_full)
core_full = obj('Dünya çekirdeği', wobble(ico(R1 * .86, sub=3), .3, .3, 2, (0, 0, 0)), toon(RED), .06, toggle=True)
core_glow = obj('Dünya çekirdek ışığı', ico(R1 * .6, sub=2), glow(HOT), 0, toggle=True)
# v5.3 anime alev yıldızı (GPT stil karesi): çekirdeğin arkasında kameraya bakan iki katlı sivri patlama, plakalar önünden geçer
core_burst = obj('Çekirdek alev yıldızı', star(R1 * 2.4, R1 * .95, 13, jitter=.38, seed=4), glow(RED_HI), 0, toggle=True)
core_burst_in = obj('Çekirdek alev yıldızı iç', star(R1 * 1.7, R1 * .85, 11, jitter=.32, seed=9), glow(HOT), 0, toggle=True)
bm = bmesh.new()
for k in range(9):   # parçalı şok halkası
    a0 = k / 9 * 2 * math.pi + RNG.uniform(0, .15); a1 = a0 + 2 * math.pi / 9 * RNG.uniform(.55, .85)
    torus(1.0, RNG.uniform(.03, .055), Matrix(), 10, 6, bm, a0, a1)
shock = obj('Şok halkası', bm, glow(CREAM), 0, toggle=True); shock.rotation_mode = 'QUATERNION'
STREAM = []   # yol boyunca duran kıymıklar: roket geçtikçe geriye akar
rng = random.Random(7)
for i in range(30):
    dd = rng.uniform(20, SS[140]); sd = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
    sd = (sd - LDIR * sd.dot(LDIR)).normalized() * rng.uniform(1.4, 5.5)
    o = obj(f'Akan kıymık {i}', shard_bm(i, .18, .6), toon(RED) if i % 2 else ROCK_M, .03, toggle=True)
    STREAM.append((o, ZL + LDIR * dd + sd, Vector((rng.random() * 3, rng.random() * 3, rng.random() * 3))))
bm = bmesh.new(); rs = random.Random(3)
for _ in range(420):   # yıldızlar dünyada sabit: kamera dönünce gökyüzü de döner (ekrana yapışık değil)
    d_ = Vector((rs.gauss(0, 1), rs.gauss(0, 1), rs.gauss(0, 1))).normalized()
    ico(rs.uniform(3.0, 7.0), Matrix.Translation(d_ * 2600), 1, bm)
stars_world = obj('Yıldızlar', bm, glow(CREAM), 0, toggle=True)
cam.data.clip_end = 4000
# ---- cila (v5.2): oyunun efekt dilinden elmas parıltılar, kaçış duman izi, Dünya kıvılcımları, çarpma pofu, buhar
bm = bmesh.new()
for _ in range(110):   # gökte dört köşeli parıltılar (oyundaki elmas kıvılcımlar gibi); v5.3'te GPT stil karesine göre daha çok ve iri
    d_ = Vector((rs.gauss(0, 1), rs.gauss(0, 1), rs.gauss(0, 1))).normalized()
    star(rs.uniform(14, 28), rs.uniform(2.6, 4.2), 4, Matrix.Translation(d_ * 2550) @ (-d_).to_track_quat('Z', 'Y').to_matrix().to_4x4(), bm)
sparkle_sky = obj('Gök parıltıları', bm, glow('#FFF6E0'), 0, toggle=True)
trail_a = pool('Kaçış duman izi', 22, lambda i: puff(170 + i), toon(ASH), .04)   # motorun arkasında kalır, geriye akar
EMBERS = [(obj(f'Kıvılcım {i}', ico(.22, sub=1), glow((HOT, RED_HI, FLAME_IN)[i % 3]), 0, toggle=True),
           Vector((RNG.gauss(0, 1), RNG.gauss(0, 1), RNG.gauss(0, 1))).normalized(), RNG.uniform(.6, 1.4)) for i in range(26)]
PLASMA = []   # ateş: f58–72 kalkışta dağılır (o şey roketmiş), f262+ atmosfere girişte burnu sarar
for v in range(4):
    o = obj(f'Ateş {v}', flame_bm(1.9, .62, v + 7, tongues=4), glow(HOT), .02, SHIP, toggle=True); o.location = (2.25, 0, 0)
    o2 = obj(f'Ateş iç {v}', flame_bm(1.3, .5, v + 7.5, tongues=3), glow(RED_HI), 0, SHIP, toggle=True); o2.location = (2.2, 0, 0)
    PLASMA.append((o, o2))
puffs3 = pool('Öksürük dumanı', 4, lambda i: puff(50 + i), toon(SMOKE), .03)
dashes = pool('Yörünge izi', 70, lambda i: cone(.18, .18, 1.0, ROT_Z_TO_X, 8), glow(CREAM), 0)
for d_ in dashes: d_.rotation_mode = 'QUATERNION'
CLOUD_M = toon(CLOUD_W)


def cloud_bm(rng, size):
    bm = bmesh.new(); x = -size * 2.2
    while x < size * 2.2:
        r = rng.uniform(.55, 1.0) * size
        sphere(r, TRS((x, rng.uniform(-.3, .3), rng.uniform(0, .3) * size), scale=(1.25, .8, .62)), 12, 6, bm); x += r * rng.uniform(1.0, 1.4)
    return wobble(bm, .08 * size, 1.6 / size, rng.random() * 9)


# Gezegen ve bulut tabakası yalnız C'de görünür: kalkışta uzakta küçük bir top olarak ikinci bir ay gibi okunuyordu
planet = obj("Eri'nin gezegeni", wobble(ico(PLR, sub=4), .9, .06, 5, (0, 0, 0)), toon(RUST, tex=rust_tex(.05), name='Gezegen'), .25, at=PL, toggle=True)
NIN = (E_IN - PL).normalized(); T1_ = NIN.cross(Vector((0, 0, 1))).normalized(); T2_ = NIN.cross(T1_)
DECK = []
# Bulutlar gezegenin bütün yüzünde, kabuğa boyanmış bantlar halinde (yalnız giriş noktasında bulut olunca "neden sadece
# orada?" dediriyordu). Üstüne oturan 3D bulut topakları kaldırıldı: v5.3'te gezegen kenarında karnabahar saçağı,
# v5.4'te giriş noktasında dışarı taşan bir yığın gibi duruyordu (kullanıcı: "girmeden gezegenin dışında görünen bulut").
# Roket artık düz bulut bandına dalar; hacimli bulutlar düşüşte (D) başlar.

# Bant ekseni bakış yönüne dik: bantlar diskin üstünden eğik şeritler halinde geçer (kameraya bakan eksen halka veriyordu)
_vd = (P232 - PL).normalized(); _pu = UPV + SIDE * .35
CL_POLE = (_pu - _vd * _pu.dot(_vd)).normalized()


def cloud_shell_mat():
    """Uzaktan gezegen bulutları (v5.3): kabuğa boyanmış bantlar, kenarında mürekkep; giriş noktasının çevresi hep bulutlu.
    Oyunun toon'u: aynı üç basamak (×1/×0,8/×0,57), boşluklar saydam."""
    m = bpy.data.materials.new('Gezegen bulut kabuğu'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    if hasattr(m, 'surface_render_method'): m.surface_render_method = 'DITHERED'
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    geo, tc = node(nt, 'ShaderNodeNewGeometry'), node(nt, 'ShaderNodeTexCoord')
    dk = dot_to(nt, g.outputs['Key'], geo)
    band = op(nt, 'ADD', op(nt, 'ADD', .57, op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, -.12), .23)),
              op(nt, 'MULTIPLY', op(nt, 'GREATER_THAN', dk, .55), .2))
    p = node(nt, 'ShaderNodeVectorMath', operation='NORMALIZE'); nt.links.new(tc.outputs['Object'], p.inputs[0])
    sh = node(nt, 'ShaderNodeVectorMath', operation='SCALE'); nt.links.new(p.outputs[0], sh.inputs[0]); sh.inputs['Scale'].default_value = 1.5
    nz = node(nt, 'ShaderNodeTexNoise'); nt.links.new(sh.outputs[0], nz.inputs['Vector'])   # düşük ayrıntı: geniş, yumuşak kenarlı bantlar
    nz.inputs['Detail'].default_value, nz.inputs['Roughness'].default_value = .5, .4
    def dot_c(v):
        d = node(nt, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); nt.links.new(p.outputs[0], d.inputs[0]); d.inputs[1].default_value = v[:]
        return d.outputs['Value']
    v = op(nt, 'ADD', nz.outputs['Fac'], op(nt, 'MULTIPLY', op(nt, 'COSINE', op(nt, 'MULTIPLY', dot_c(CL_POLE), 11.0)), .3))
    v = op(nt, 'ADD', v, op(nt, 'MAXIMUM', op(nt, 'MULTIPLY', op(nt, 'SUBTRACT', dot_c(NIN), .88), 6.0), 0.0))   # giriş noktası: tabakanın altı da bulut
    cloud, edge = op(nt, 'GREATER_THAN', v, .67), op(nt, 'GREATER_THAN', v, .64)   # gezegenin üçte biri kadar bulut
    sc = node(nt, 'ShaderNodeVectorMath', operation='SCALE'); sc.inputs[0].default_value = C(CLOUD_W)[:3]; nt.links.new(band, sc.inputs['Scale'])
    c = mix(nt, cloud, C(INK), mix(nt, 1.0, sc.outputs[0], (0, 0, 0, 1), 'ADD'))
    c = mix(nt, 1.0, c, g.outputs['Tint'], 'MULTIPLY'); c = mix(nt, g.outputs['Impact'], c, C(INK))
    em, tr, ms = node(nt, 'ShaderNodeEmission'), node(nt, 'ShaderNodeBsdfTransparent'), node(nt, 'ShaderNodeMixShader')
    out = node(nt, 'ShaderNodeOutputMaterial'); nt.links.new(c, em.inputs['Color'])
    nt.links.new(edge, ms.inputs[0]); nt.links.new(tr.outputs[0], ms.inputs[1]); nt.links.new(em.outputs[0], ms.inputs[2])
    nt.links.new(ms.outputs[0], out.inputs['Surface'])
    return m


# Kabuk yüzeye yakın: gezegenin mürekkep çizgisinin içinde kalır (1,1'de kenarda beyaz sızıntı veriyordu); yüksek tepeler
# kabuğu deler, bulutların arasından ada gibi görünür
cloud_shell = obj('Gezegen bulut kabuğu', sphere(PLR + .45, u=96, v=48), cloud_shell_mat(), 0, smooth=True, at=PL, toggle=True)
DECK.append(cloud_shell)
glint = obj('İpucu parıltısı', star(2.2, .3, 4), glow(GLINT), 0, toggle=True)
glint.location = PL + (P232 - PL).normalized() * (PLR + 1.6); glint.rotation_mode = 'QUATERNION'   # bulut kabuğunun üstünde
glint.rotation_quaternion = (P232 - PL).normalized().to_track_quat('Z', 'Y')
# Bulutun içine giriş/çıkış. Mürekkep kabuğu yok: içinden bakınca kalın siyah şeritler gibi görünüyordu (v5.5)
engulf = obj('Kamerayı saran bulut', cloud_bm(random.Random(5), 1.0), CLOUD_M, 0, cam, toggle=True)
PORT_C, PORT_FRONT = Vector((.35, -.45, .06)), Vector((.35, -1.12, .06))   # lombozun merkezi ve önündeki kamera noktası
CAM2B_LOCAL, TGT2B_LOCAL = Vector((1.15, -1.3, .5)), Vector((-7.0, .2, -1.25))   # gövdeye yapışık, arkaya bakar
TAIL_LOCAL, TAIL_TGT = Vector((-4.2, -3.2, -1.3)), Vector((-.7, 0, .25))         # arka üç çeyrek

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


# v5.5: göstergeye vuran eldiven kaldırıldı (kullanıcı); yakıtın bittiğini ibre, lamba ve dışarıdaki tekleme anlatır


# ---------------------------------------------------------------- D — iniş: bulut katmanları yerin üstünde, kamera içinden iner
O6 = Vector((10000, 0, 0)); LAND = O6 + Vector((1.6, -.6, 0))
D0, DMID, D1END = O6 + Vector((-16, 26, 44)), O6 + Vector((-12, 8, 11)), LAND + Vector((-.4, 0, .5))
DLOW = 306   # gemi alçak kameranın kadrajına sol üstten girer; kamera bu kareye kadar aşağı süzülüp alçalır
CAM_FALL = Vector((-3.0, -7.5, 4.2))


def path_d(f):
    """İniş yolu: bulutun içinden alçak kameranın önüne (f306), oradan yere; hız iki parçada da yakın (kopmaz)."""
    # v5.7: tek ikinci derece eğri (DMID kontrol noktası), hızlanarak. Önce iki düz parça DLOW'da birleşiyordu:
    # roket o karede yön kırıyor, ekranda iki karede 78° dönüyordu (ölçüldü, kullanıcı: "roket yönü bir anda değişmiş")
    x = clamp((f - TDIVE) / (IMPACT - TDIVE)); t = .65 * x + .35 * x * x
    return D0 * (1 - t) ** 2 + DMID * (2 * t * (1 - t)) + D1END * t * t


obj('Zemin', box((300, 300, .2), Matrix.Translation(O6 + Vector((0, 100, -.1)))), toon(RUST, tex=rust_tex(.35), rim=0, name='Zemin'), 0)
obj('Ufuk ışığı alt', box((340, .2, 6), Matrix.Translation(O6 + Vector((0, 90, 1.0)))), glow(GLOW_LOW), 0)
obj('Ufuk ışığı üst', box((340, .2, 6), Matrix.Translation(O6 + Vector((0, 90.5, 6.5)))), glow(GLOW_HIGH), 0)
rng = random.Random(6); bm = bmesh.new(); x_ = -90
while x_ < 90:
    sphere(rng.uniform(7, 12), TRS(O6 + Vector((x_, 60 + rng.uniform(-4, 4), -1.5)), scale=(1.3, .6, rng.uniform(3, 7) / 22)), 12, 6, bm); x_ += rng.uniform(9, 15)
obj('Tepeler', bm, toon(HILL, rim=0), .25)
bm = bmesh.new()
for i in range(40):
    p_ = O6 + Vector((rng.uniform(-18, 18), rng.uniform(-4, 30), 0))
    ico(rng.uniform(.15, .55), TRS(p_, (rng.random() * 3,) * 3, (1.4, 1, .6)), 1, bm)
obj('Kayalar', bm, toon(RUST_HI), .03)
rc = random.Random(44)
for i in range(30):   # üç bulut katmanı: roket içlerinden geçer, kamera aralarından (içinden geçerse kadraj bembeyaz kalıyordu)
    layer = i % 3; z = (31, 38, 45)[layer] + rc.uniform(-1.5, 1.5)   # bulut tabanı ~29: kamera alçalırken altta açık gök
    pz = D0.lerp(DMID, clamp((D0.z - z) / (D0.z - DMID.z)))
    at = Vector((pz.x + rc.uniform(-12, 12), pz.y + rc.uniform(-10, 10), z))
    cz = Vector((pz.x + CAM_FALL.x, pz.y + CAM_FALL.y, z))
    away = Vector((at.x - cz.x, at.y - cz.y, 0))
    if away.length < 11: at = cz + (away.normalized() if away.length > 1e-3 else Vector((1, 0, 0))) * rc.uniform(11, 13)
    obj(f'İniş bulutu {i}', cloud_bm(rc, rc.uniform(1.6, 2.6)), CLOUD_M, .08, at=at)


def leaf_bm(L, w, bend):
    bm = sphere(1.0, u=16, v=6)
    for v in bm.verts:
        t = (v.co.x + 1) / 2; taper = math.sin(math.pi * clamp(t)) ** .75
        v.co = Vector((v.co.x * L, v.co.y * .05, v.co.z * w * taper + bend * L * t * t))
    return bm


LEAVES = []
for name, at, rot in (('sol', (-3.3, -6.9, -.45), (0, -.5, .25)), ('sol 2', (-3.0, -7.15, -.6), (0, -.15, .5)), ('sağ', (3.3, -6.9, -.5), (0, -.5, math.pi - .25))):
    o = obj(f'Ön yaprak {name}', leaf_bm(1.5 if name != 'sol 2' else 1.1, .32, .35), toon(LEAF_FG), .035, at=O6 + Vector(at)); o.rotation_euler = rot
    LEAVES.append((o, Euler(rot)))
bm = bmesh.new()
for i in range(9):
    a_ = i / 9 * 2 * math.pi + .3
    box((RNG.uniform(.8, 1.6), .05, .02), TRS(LAND + Vector((math.cos(a_), math.sin(a_), 0)) + Vector((0, 0, .03)), (0, 0, a_)), bm)
ground_cracks = obj('Zemin çatlakları', bm, glow(INK), 0, toggle=True)
skid = obj('Kayma izi', box((3.0, .8, .05), TRS(LAND + Vector((-.9, .2, .02)), (0, 0, .05))), glow('#4A1E18'), 0, toggle=True)
smoke_d = pool('İniş dumanı', 14, lambda i: puff(90 + i), toon(SMOKE), .025)
dust6 = pool('Toz', 16, lambda i: puff(110 + i), toon(DUST), .03)
rocks6 = pool('Taş', 10, lambda i: shard_bm(i, .06, .13), toon(RUST_HI), .015)
# çarpmada oyunun pofu: krem çiçek-pof + elmas kıvılcım (Docs/SproutBurst'teki dil); sonra gemiden küçük buhar
pof6 = pool('Çarpma pofu', 7, lambda i: star(1.0, .72, 6, jitter=.12, seed=i), glow('#FFF1E0'), 0)
pof6_rim = pool('Çarpma pofu kenarı', 7, lambda i: star(1.18, .86, 6, jitter=.12, seed=i), glow('#F2A0B0'), 0)
glit6 = pool('Çarpma kıvılcımı', 10, lambda i: star(.5, .12, 4), glow('#FFE6F0'), 0)
POF_V = [(Vector((math.cos(a_) * 1.4, -.6, .6 + .9 * abs(math.sin(a_)))), RNG.uniform(.35, .6)) for a_ in (i / 7 * math.pi for i in range(7))]
GLIT_V = [Vector((RNG.uniform(-3, 3), RNG.uniform(-1.2, -.2), RNG.uniform(1.2, 3.2))) for _ in range(10)]
steam6 = pool('Buhar', 4, lambda i: puff(130 + i), toon('#E8E2EA'), .02)
DUST_P = [(Vector((math.cos(a_), .55 * math.sin(a_), 0)) * RNG.uniform(.6, 1.3), RNG.uniform(.3, 2.2), RNG.uniform(.55, 1.1))
          for a_ in (i / 16 * 2 * math.pi + RNG.uniform(-.2, .2) for i in range(16))]
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


def light(f, key, rim, rim_col, rim_amt, tint=(1, 1, 1), impact=0.0, sky=SPACE, edge=.56, neb=1.0):
    """edge: kenar ışığının başladığı yan bakış eşiği; kamera gövdeyi yandan sıyırarak görürken yükselir.
    neb: boyalı bulutsu (uzayda 1; kokpit ve atmosfer içinde 0)."""
    for i in range(3): L_KEY.inputs[i].default_value = key[i]; L_RIM.inputs[i].default_value = rim[i]
    L_RIMC.outputs[0].default_value = C(rim_col); L_RIMA.outputs[0].default_value = rim_amt; L_RIME.outputs[0].default_value = edge
    L_TINT.outputs[0].default_value = (*tint, 1); L_IMP.outputs[0].default_value = impact
    SKY_RGB.outputs[0].default_value = C(CREAM) if impact > .5 else (C(sky) if isinstance(sky, str) else sky)
    NEB_AMT.outputs[0].default_value = 0.0 if impact > .5 else neb
    for n in (L_KEY, L_RIM):
        for i in range(3): n.inputs[i].keyframe_insert('default_value', frame=f + 1)
    for n in (L_RIMC, L_RIMA, L_RIME, L_TINT, L_IMP, SKY_RGB, NEB_AMT): n.outputs[0].keyframe_insert('default_value', frame=f + 1)


# Vurgular: (kare, derece). Kamera her karede; sarsıntı açısal (mesafeden bağımsız).
IMPULSES = [(14, 1.2), (28, 2.0), (42, 3.8), (61, 1.2), (90, 2.4), (96, 1.0), (104, 1.2), (SHOCK, 4.2), (T3A, 1.4),
            (168, .45), (174, .45), (190, .4), (194, .7), (202, .8), (232, .5), (276, 1.2), (IMPACT, 5.0)]


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
    if 96 <= f < SHOCK:                                               # Ç2b: Dünya uzaklaştıkça uğultu söner,
        rum += 1.0 * math.exp(-(f - 96) / FPS * 1.1)                  #       şok dalgası yaklaşırken yeniden kabarır
        rum += 1.8 * ease_in((f - 124) / 16) if f >= 124 else 0
    if SHOCK <= f < T3A: rum += 1.2 * math.exp(-(f - SHOCK) / 6)      # çarpmadan sonra lomboza dönerken söner
    if 266 <= f < 282: rum += .4 + .8 * ease_in((f - 266) / 16)       # Ç4: atmosfere giriş
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


def ship(f, at, q, scale=1.0, thrust=0.0, hot=False, lit=False, stretch=1.0, force=True, smear=0.0, traces=None):
    """Roket kahraman nesne: her karede (kamera onu izliyor; ikişer karede ölçümde ±10–40 px titriyordu).
    İkincil nesneler (kıymık, plaka, duman, bulut) ikişer karede kalır.
    smear: anime smear karesi (v5.6). Gövde ekrandaki hareketin tersine uzar, öndeki uç yerinde kalır: > 0 burun önde
    gidiyor (gövde kuyruğa doğru uzar), < 0 ekranda kuyruk önde gidiyor (burna doğru uzar). Kalınlık hacim korunarak incelir.
    Roketin matrisine bağlı kamera bağı (rig_mount, rig_out) olan karelerde kullanılmaz: kamera da uzardı."""
    if smear:
        stretch = 1 + abs(smear)
        anchor = Vector((2.1 if smear > 0 else -1.6, 0, 0)) * scale
        at = at + q @ (anchor * (1 - stretch))
    SHIP.location, SHIP.rotation_quaternion = at, q
    side = scale / math.sqrt(stretch); SHIP.scale = (scale * stretch, side, side)
    if thrust > .01:
        fl = thrust * (1 + .08 * math.sin(f * 2.3))
        flame.scale = (fl, min(1.2, .55 + .55 * thrust), min(1.2, .55 + .55 * thrust)); on(*FLAMES[(f // 2) % 4])
    on(nose_hot if hot else nose); on(glass_lit if lit else glass)
    if hot if traces is None else traces: on(heat)
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
def earth_events(f):
    """Ç1 (v5.8): Dünya roket kaçarken aşağıdan yukarı çatlar; büyük patlama burada YOK, geriye bakışta (EXPLODE).
    Kullanıcı: "Dünya roket kaçarken çatlar, aşağıdan yukarı yavaşça; roketin arkasından bakınca patlama olur".
    Merdiven gibi plaka kenarları ve yeşil altıgen kıymıklar "AI slop" gibi duruyordu: çatlaklar artık tasarlanmış
    mürekkep + lav şeritleri (Build ile büyür), fırlayış noktasında üç kabuk parçası kalkar, roket ateş içinde fırlar."""
    zc = D1 * R1
    if f < SWAP: on(earth_whole)
    for objs, f0 in CRACKS:
        if f0 <= f < SWAP: on(*objs)
    for o, p, tb, life, w0_, drift in CRACK_EMBERS:   # çatlaktan yükselen kıvılcımlar (ikişer karede)
        if not tb <= f < SWAP: continue
        a = ((f - tb) % life) / life
        o.location = p.normalized() * (R1 + .02 + w0_ * 3.5 * ease_out(a)) + drift * w0_ * 1.2 * a
        o.scale = ((1 - ease_in(a)) * (.8 + .3 * math.sin(f * 1.3 + tb)),) * 3; K(o, f); on(o)
    for pivot, piece, base, hinge, t0, ang, rise in CHUNKS:   # kabuk parçaları: yavaşça kalkar, fırlayışta bir kez daha sarsılır
        a = ease((f - t0) / 16) if f >= t0 else 0
        a += .6 * ease_out((f - 44) / 6) if f >= 44 else 0
        pivot.rotation_quaternion = Quaternion(hinge, ang * a); pivot.location = base + base.normalized() * rise * a
        K(pivot, f)
        if t0 <= f < SWAP: on(piece)   # kalkmaya başlayınca belirir (önceden konturu ufukta hazır çizilmiş gibi duruyordu)
    for o, (off, rise, size) in list(zip(plume1, PLUME1))[:5]:   # fırlayışın dumanı: çatlaktan kısa bir sütun (patlama değil)
        if 44 <= f < SWAP:   # 64'teki kesmede kalkar (geriye bakışta Dünya'ya yapışık gri topak gibi duruyordu)
            a = (f - 44) / FPS
            o.location = zc + D1 * (.3 + rise * .6 * (1 - math.exp(-a * 2.6))) + off * .5 * (1 + a)
            o.scale = (size * .5 * (.25 + .9 * ease_out(a / .5)),) * 3; o.rotation_euler = (a, a * .6, 0); K(o, f); on(o)
    if 44 <= f < REVEAL1:   # alev topu = ateşe sarılı roket, kaçış yolunun başında
        head = path(f)
        fire_head.location = head; fire_tail.location = head
        fire_tail.rotation_quaternion = LDIR.to_track_quat('Z', 'Y'); fire_tail.scale = (1, 1, 1.2 + 5 * (f - 44) / FPS)
        g = 1 - ease((f - REVEAL0) / (REVEAL1 - REVEAL0)) if f >= REVEAL0 else 1   # ateş dağılırken küçülür
        fire_head.scale = (g,) * 3; fire_tail.scale = (g, g, fire_tail.scale.z * g)
        K(fire_head, f, True); K(fire_tail, f, True); on(fire_head, fire_tail)


def rig_surface(f):
    """Yüzey kamerası: yavaş ileri, hazırlıkta nefes alır, patlama geri ve yukarı iter, alev topunu izleyerek başını kaldırır."""
    zc = D1 * R1
    loc = CAM1 + Vector((0, .45, -.12)) * ease(min(f, 35) / 50) + Vector((0, .18, -.06)) * (ease((f - 35) / 6) if f < 42 else 0)
    loc = loc + Vector((0, -3.0, 1.6)) * ease_out((f - 41) / 9)
    tgt = zc + Vector((.1, .4, .15))
    if f >= 45: tgt = tgt.lerp(path(f), .55 + .35 * ease((f - 45) / 14))
    return loc, tgt, 22


# ---------------- Ç3a: sarsıntı içeride sürer, ibre boşa düşer, uyarı lambası yanıp söner (el yok, v5.5)
LAMP_PULSES = (162, 168)


def shot3a(f):
    if f < 156: deg = 100 + 4 * math.sin(f * 3)   # lombozdan girişte sarsıntıyla titrer, sonra düşer
    else: deg = {156: 122, 158: 150, 160: 163, 162: 147, 164: 154}.get(f - f % 2, 152)
    needle.rotation_euler = (0, -math.radians(deg), 0); K(needle, f)
    if 156 <= f < 162: on(needle_arcs)
    lamp = f >= LAMP_PULSES[0] and not (165 <= f < 168)   # ibre boşa oturunca yanar, bir kez söner, yeniden yanar
    on(lamp_on if lamp else lamp_off)
    for t0 in LAMP_PULSES:
        if t0 <= f < t0 + 5:
            lamp_ring.scale = (.25 + .5 * ease_out((f - t0) / 5),) * 3; K(lamp_ring, f, True); on(lamp_ring)
    pulse = f in (162, 163, 168, 169)
    u = (f - T3A) / (T3C - T3A)
    camera(f, O3 + Vector((-1.3, -4.7 + .55 * ease(u), -.75)), O3 + Vector((.3, 0, .05)), 40, -.09, .45)
    light(f, (-.5, -.6, .6), (.75, -.25, .6), LAMP_ON, 1.0 if lamp else 0, (1, .74, .7) if pulse else (1, 1, 1), sky=PANEL_BG, neb=0.0)


# ================================================================ v5 — kesintisiz kamera: bağlar ve harmanlar
IMPULSES = [(14, 1.2), (28, 2.0), (42, 2.2), (REVEAL0 + 2, 1.0), (104, 1.2), (114, 2.6), (SHOCK, 4.2), (T3A, 1.4), (162, .35),
            (T3C, .6), (186, .8), (190, .35), (194, .4), (200, .45), (232, .5), (276, 1.0), (IMPACT, 5.0)]   # boğulma + üç tık


def shake(f, hand, vib):
    """Darbe = yüksek frekans, hızla söner; uğultu = yavaş gürültü, olayın kendi eğrisiyle büyür/söner."""
    hit = 0.0
    for f0, amp in IMPULSES:
        d = f - f0
        if 0 <= d < 30: hit += amp * math.exp(-d / 3.2)
    rum = 0.0
    if 30 <= f < 40: rum += 1.0 * ease_in((f - 30) / 10)              # patlamadan önce titreme büyür
    if f in (40, 41): hit *= .1; rum = 0                              # ...ve bir an susar
    if 42 <= f < SHOCK:                                               # Dünya uzaklaştıkça uğultu söner,
        rum += 1.1 * math.exp(-(f - 42) / FPS * 1.0)                  # şok dalgası yaklaşırken yeniden kabarır
        rum += 1.8 * ease_in((f - 124) / 16) if f >= 124 else 0
    if SHOCK <= f < T3A: rum += 1.2 * math.exp(-(f - SHOCK) / 6)
    if 266 <= f < TDIVE: rum += .4 + .8 * ease_in((f - 266) / 16)    # atmosfere giriş
    if TDIVE <= f < IMPACT: rum += .7 + 1.0 * ease_in((f - TDIVE) / 34)   # bulutların içinden yere
    hr, rr, vr, ar = math.radians(hand), math.radians(rum), math.radians(vib), math.radians(hit)
    n = lambda k, sp: mnoise.noise(Vector((f * sp, k, k * 1.7)))
    yaw = ar * math.sin(f * 2.9 + 1) + rr * n(1, .42) + hr * n(3, .13) + vr * math.sin(f * 1.7)
    pitch = ar * math.cos(f * 3.4) + rr * n(5, .42) + hr * n(7, .13) + vr * math.cos(f * 2.3 + .6)
    return yaw, pitch, ar * .7 * math.sin(f * 2.2 + .4) + rr * .5 * n(9, .3)


def look(loc, tgt, roll=0.0):
    return (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y') @ Quaternion((0, 0, 1), roll)


def blend(a, b, w):
    """İki kamera bağı arasında yumuşak geçiş. Bağlar (konum, bakılan nokta, lens) döndürür; yön değil bakılan
    nokta karışır: geçişin her anında kamera konuya bakar (yön karışınca yarı yolda boşluğa bakıyordu, ölçüldü)."""
    return a[0].lerp(b[0], w), a[1].lerp(b[1], w), a[2] + (b[2] - a[2]) * w


def on_ship(cl, tl, lens):
    return ship_pt(cl), ship_pt(tl), lens


def rig_chase(f):   # roketin yanında, aynı taraftan (−Y), biraz geriden
    P = path(f); loc = P + SIDE * 7.5 - LDIR * 3.0 + UPV * 1.2
    return loc, P + LDIR * 1.8, 30


def rig_mount(f):   # gövdeye yapışık, arkaya bakar (v5.7: sabit; lomboza dönüp camdan girmez, kokpite kesilir)
    return on_ship(CAM2B_LOCAL, TGT2B_LOCAL, 24)


def rig_out(f):     # camdan dışarı çıkar, gövde boyunca kuyruğa kayar
    k = ease((f - T3C) / 14)
    return on_ship(PORT_FRONT.lerp(TAIL_LOCAL, k), PORT_C.lerp(TAIL_TGT, ease(k * 1.2)), 24 + 6 * k)


def rig_wide(f):    # motor susunca arkaya ve yukarı çekilir, gezegene döner: sönük motor bize bakar, roket gezegene süzülür
    # v5.2'de 32 birim yandan bakıyordu: roket toz kadar kalıyor, gezegen ancak sonda giriyordu (kullanıcı: "kötü duruyor")
    P = path(f); loc = P - LDIR * 8.5 + SIDE * 3 + UPV * 2.6
    rd_, pd_ = (P - loc).normalized(), (PL - loc).normalized()
    return loc, loc + (rd_ * .6 + pd_ * .4).normalized() * 20, 28


def rig_capture(f):
    P, T = path(f), tangent(f); loc = P + SIDE * 16 - T * 12 + UPV * 5
    return loc, P + T * 8 - UPV * 3, 30


def rig_dive(f):
    P, T = path(f), tangent(f); loc = P - T * 6 + SIDE * 2.5 + UPV * 1.8
    return loc, P + T * 5, 28


def put_cam(f, c, hand=.2, vib=0.0, roll=0.0):
    camera(f, c[0], c[1], c[2], roll, hand, vib)


# ================================================================ v5.7 — sabit açılar, olay üstünde kesme
# Kullanıcı (4 Kasım): "kamera habire ileri geri hareket ediyor gibi, sıkıntı bu; sabit farklı açılara geçen kamera olabilir".
# Her çekimde kamera ya dünyada durur ya da rokete göre sabit bir açıda kalır (roketle birlikte taşınır, kendi içinde
# itip çekmez). Çekimler olayın üstünde kesilir; roketin ekrandaki yönü kesmeden kesmeye korunur: kaçarken sağa/yukarı,
# düşerken aşağı. Sarsıntı (darbe/uğultu) ve hafif el titremesi sürer: o kamera hareketi değil, hissin kendisi.
SHOT1 = (CAM1 + Vector((0, .25, -.07)), D1 * R1 + Vector((.1, .4, .15)), 22)               # 0–41 yüzey: Dünya çatlar
SHOT2 = (CAM1 + Vector((0, -2.6, 1.5)), (D1 * R1).lerp(ZL + LDIR * 5.5, .4), 22)            # 42–63 geniş: ateş fırlar


# ================================================================ v5.8 — Dünya'nın çatlaması (ekranda tasarlanır)
def _cam(c):
    loc, tgt = Vector(c[0]), Vector(c[1]); q = (tgt - loc).to_track_quat('-Z', 'Y')
    return loc, q @ Vector((0, 0, -1)), q @ Vector((1, 0, 0)), q @ Vector((0, 1, 0)), 18 / c[2]


def surf(c, x, y, R=R1):
    """Kameranın ekranındaki (x, y ∈ [−1, 1]) noktanın Dünya yüzeyindeki yeri (ufkun üstündeyse None)."""
    loc, fw, rt, up, th = _cam(c); d = (fw + rt * x * th + up * y * th * 9 / 16).normalized()
    b = loc.dot(d); disc = b * b - (loc.dot(loc) - R * R)
    if disc < 0: return None
    t = -b - math.sqrt(disc); return loc + d * t if t > 0 else None


def ribbon(name, pts, w0, w1, h, mat, seed, i0, n_all, wb, shift=0.0, eye=None):
    """Yüzeye yapışık şerit; yüzleri yol boyunca sıralı (Build bu sırayla açar). Genişlik ve kıvrım gürültüsü bütün
    çatlak boyunca süreklidir (i0), katmanlar aynı kıvrımı izler (wb: mürekkep katmanının uç genişlikleri).
    shift: şeridi kameradan UZAK kenara (+) ya da yakın kenara (−) kaydırır, mürekkep genişliği cinsinden (çukur yanılsaması)."""
    bm = bmesh.new(); L = []; n = len(pts)
    for i, p in enumerate(pts):
        nrm = p.normalized(); sd = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).cross(nrm).normalized()
        if eye is not None and sd.dot(p - eye) < 0: sd = -sd   # sd hep kameradan uzak kenara baksın
        u = (i0 + i) / max(n_all - 1, 1); g = mnoise.noise(Vector(((i0 + i) * .4, seed, 0)))
        w = (w0 + (w1 - w0) * u ** .7) * (1 + .3 * g); wr = wb[0] + (wb[1] - wb[0]) * u ** .7
        c = nrm * (R1 + h) + sd * (mnoise.noise(Vector(((i0 + i) * .23, seed * 1.7, 3))) * .45 + shift) * wr
        L.append((bm.verts.new(c - sd * w / 2), bm.verts.new(c + sd * w / 2)))
    for (a0, b0), (a1, b1) in zip(L, L[1:]): bm.faces.new((a0, b0, b1, a1))
    return obj(name, bm, mat, 0, smooth=False, toggle=True)


# v5.9 çukur yanılsaması ("paint çizimi gibi" bulundu): mürekkep ağız; kameradan UZAK kenarda lavın aydınlattığı kaya duvarı
# (çukurun görünen yüzü), lav ve sarı çekirdek YAKIN kenara kayar (yakın duvar onları kısmen örtüyormuş gibi).
CRACK_LAYERS = ((1.0, .014, glow(INK), 0.0), (.55, .016, glow('#B5653A'), .2),
                (.36, .018, glow('#FF6A2A'), -.12), (.14, .022, glow('#FFD86A'), -.14))   # ağız, duvar, lav, çekirdek
CRACKS = []
CRACK_PATHS = []   # kıvılcımlar için: (yol, başlangıç, bitiş)


def add_crack(name, c, pts, t0, t1, w0, w1, seed, splits=1):
    """Ekranda çizilen çatlak; splits parçada, her parça kendi zaman aralığında büyür (sarsıntı vuruşlarıyla adım adım)."""
    world = [p for p in (surf(c, x, y) for x, y in pts) if p is not None]
    path = [p.normalized() * R1 for p in resample(world, .03)]
    n = len(path); cuts = [round(n * k / splits) for k in range(splits + 1)]
    CRACK_PATHS.append((path, t0, t1, w0))
    for s in range(splits):
        a, b = max(cuts[s] - 1, 0), cuts[s + 1]; seg = path[a:b]
        if len(seg) < 2: continue
        fs, fe = t0 + (t1 - t0) * s / splits, t0 + (t1 - t0) * (s + 1) / splits
        objs = []
        for m, h, mat, sh in CRACK_LAYERS:
            o = ribbon(f'{name} {s} {h}', seg, w0 * m, w1 * m, h, mat, seed, a, n, (w0, w1), sh, Vector(c[0]))
            md = o.modifiers.new('Büyüme', 'BUILD'); md.frame_start, md.frame_duration = fs + 1, max(1.0, fe - fs)
            objs.append(o)
        CRACKS.append((objs, int(fs)))


# Yüzey çekimi (0–41): fırlayış noktası ufukta (ekran ≈ −0,07, −0,02). Çatlaklar kadrajın altından ufka doğru büyür.
add_crack('Çatlak ana', SHOT1, [(.08, -1.12), (0, -.82), (.07, -.6), (-.04, -.4), (.02, -.24), (-.06, -.12), (-.074, -.035)],
          2, 41, .17, .05, 3.1, splits=3)
add_crack('Çatlak sol alt', SHOT1, [(0, -.82), (-.2, -.9), (-.42, -.86), (-.62, -1.08)], 8, 18, .1, .03, 5.3)
add_crack('Çatlak sağ', SHOT1, [(.07, -.6), (.3, -.52), (.52, -.45), (.76, -.41), (1.06, -.46)], 14, 27, .11, .03, 7.7)
add_crack('Çatlak sol', SHOT1, [(-.04, -.4), (-.25, -.33), (-.46, -.36), (-.68, -.27), (-.98, -.25)], 22, 34, .1, .03, 9.2)
add_crack('Çatlak sağ ufuk', SHOT1, [(.02, -.24), (.22, -.18), (.42, -.16), (.6, -.2)], 30, 42, .08, .025, 11.6)
add_crack('Çatlak sol ufuk', SHOT1, [(-.06, -.12), (-.28, -.09), (-.5, -.08)], 34, 44, .07, .02, 13.4)
# Geniş çekim (42–63): Dünya kadrajın altında ince bir yay; yeni çatlaklar alttan ufka doğru yayılmayı sürdürür
add_crack('Çatlak geniş 1', SHOT2, [(-.92, -1.08), (-.76, -.96), (-.6, -.85), (-.5, -.72), (-.44, -.62)], 44, 57, .3, .06, 15.5)
add_crack('Çatlak geniş 2', SHOT2, [(.2, -1.08), (.1, -.9), (0, -.78), (-.1, -.68), (-.2, -.61)], 46, 59, .3, .06, 17.9)
add_crack('Çatlak geniş 3', SHOT2, [(.62, -1.08), (.47, -.96), (.36, -.85), (.26, -.73)], 49, 61, .26, .05, 19.3)
add_crack('Çatlak geniş 4', SHOT2, [(-.28, -1.08), (-.34, -.86), (-.31, -.7)], 52, 63, .22, .05, 21.1)

CHUNKS = []   # fırlayış noktasının çevresinde üç kabuk parçası: üstü Dünya, yanı kaya, mürekkepli


def chunk(name, x, y, r, t0, ang, rise, seed):
    rr = random.Random(seed); cen = surf(SHOT1, x, y).normalized(); t1_ = cen.cross(Vector((0, 0, 1))).normalized(); t2_ = cen.cross(t1_)
    bm = bmesh.new(); vs = []
    for k in range(6):
        a = k / 6 * 2 * math.pi + rr.uniform(-.25, .25)
        vs.append(bm.verts.new((cen * R1 + (t1_ * math.cos(a) + t2_ * math.sin(a)) * r * rr.uniform(.75, 1.15)).normalized() * (R1 + .012)))
    fc = bm.faces.new(vs); bm.normal_update()
    if fc.normal.dot(cen) < 0: fc.normal_flip()
    base = cen * (R1 + .012); pivot = empty(f'{name} eksen', at=base); pivot.rotation_mode = 'QUATERNION'
    o = thick_plate(name, bm, EARTH_M1, .35, .03); o.parent = pivot; o.location = -base; TOGGLE.append(o)
    hinge = (cen.cross(Vector((rr.uniform(-1, 1), rr.uniform(-1, 1), rr.uniform(-1, 1))))).normalized()
    CHUNKS.append((pivot, o, base, hinge, t0, ang, rise))


# Çatlaklardan yükselen kıvılcımlar (v5.9 derinlik): o nokta açıldıktan sonra doğar, yükselir, söner, yeniden doğar.
# Boyları çatlağın genişliğine bağlı; kameraya yakın olanlar büyük, uzaktakiler küçük görünür.
re_ = random.Random(57); CRACK_EMBERS = []
for i in range(32):
    path_, t0_, t1_, w0_ = CRACK_PATHS[i % len(CRACK_PATHS)]
    q = re_.uniform(.05, .95)
    o = obj(f'Çatlak kıvılcımı {i}', ico(w0_ * re_.uniform(.12, .22), sub=1), glow((HOT, FLAME, FLAME_IN)[i % 3]), 0, toggle=True)
    CRACK_EMBERS.append((o, path_[int(q * (len(path_) - 1))], t0_ + (t1_ - t0_) * q + re_.uniform(1, 8), re_.uniform(14, 24), w0_,
                         Vector((re_.gauss(0, 1), re_.gauss(0, 1), re_.gauss(0, 1)))))
chunk('Kabuk parçası 1', -.22, -.05, .42, 28, .34, .08, 1)
chunk('Kabuk parçası 2', .1, -.065, .36, 32, .3, .06, 2)
chunk('Kabuk parçası 3', -.02, -.16, .3, 36, .26, .05, 3)


def rig_follow(f):   # 202–261: roketin arkasında ve üstünde, yola göre sabit; aşağıda önde gezegen, yol kıvrılınca kamera da kıvrılır
    P, T = path(f), tangent(f)
    sd = T.cross(Vector((0, 0, 1))).normalized(); up = sd.cross(T)
    return P - T * 8.5 + sd * 3 + up * 2.6, P + T * 20 - up * 7, 28


# 262–281 sabit yan: atmosfere giriş. Aşağıda gezegenin bulutlu yüzeyi; ateş içindeki roket soldan sağa, aşağı doğru
# bulut bandına dalar (yolun gerisine bakınca gezegen kadrajda yoktu, 280'de kadraj boş kalıyordu)
_D8 = (E_IN - path(262)).normalized(); _S8 = _D8.cross(NIN).normalized(); _M8 = path(268).lerp(E_IN, .5)
SHOT8 = (_M8 + _S8 * (E_IN - path(262)).length * .85 + NIN * 2.0, _M8 - NIN * 2.5, 30)


def roll_to(c, up_w):
    """Kamerayı bakış ekseninde döndürüp up_w'yi ekranın yukarısı yapan açı (camera()'nın roll'u)."""
    q0 = (c[1] - c[0]).to_track_quat('-Z', 'Y'); vz = q0 @ Vector((0, 0, 1)); u0 = q0 @ Vector((0, 1, 0))
    w = (up_w - vz * up_w.dot(vz)).normalized()
    return math.atan2(u0.cross(w).dot(vz), u0.dot(w))


ROLL8 = roll_to(SHOT8, NIN)   # gezegenin yüzeyi kadrajın altında yatay: roket aşağı doğru dalar (dikey ufukta sağa gidiyordu)
CUT10 = 302   # roket bu karede yerden çekimin üst kenarına girer (300'de kadraj dört kare boş kalıyordu)
SHOT10 = (O6 + Vector((0, -10.5, .85)), O6 + Vector((-1.2, -.6, 3.2)), 28)                # 302–327 yerden: roket yukarıdan gelir, çakılır
SHOT11 = (O6 + Vector((1.1, -7.6, .7)), LAND + Vector((0, 0, .7)), 24)                     # 328–353 yakın: gemi, lombozun ışığı


EXPLODE = 114   # v5.8: Dünya geriye bakışta patlar (kullanıcı: "roketin arkasından bakınca patlama olur")


def explode_earth(f):
    """v5.8: Dünya önce aşağıdan yukarı çatlar (plaka dikişleri açılır, içeriden çekirdeğin ışığı sızar), EXPLODE'da
    plakalar savrulur; alev yıldızı ve kıvılcımlar da o anda başlar."""
    t = max(0.0, (f - EXPLODE) / FPS)
    for p in plates_full:
        ta = 98 + 12 * (p['n'].dot(UPV) + 1) / 2   # roketten bakınca alttaki plakalar önce, üsttekiler sonra
        crack = .6 * ease((f - ta) / 6) if f >= ta else 0.0   # uzaktan seçilsin: dikişler açılır, çekirdeğin kızıllığı görünür
        p['pivot'].location = p['cen'] + p['n'] * (crack + 15 * ease_out(t / 3.0) * p['w'])
        p['pivot'].rotation_euler = Quaternion(p['axis'], .7 * t * p['w']).to_euler(); K(p['pivot'], f); on(p['obj'])
    core_full.scale = (1 + .08 * t,) * 3; K(core_full, f); on(core_full, core_glow)
    if f < EXPLODE: return
    look = cam.location.normalized() if cam.location.length > 1e-6 else Vector((0, -1, 0))
    for o, back, spin in ((core_burst, .5, .35), (core_burst_in, .2, -.5)):   # ikişer karede hafif titrer, yavaş döner
        o.location = -look * back
        o.rotation_euler = (look.to_track_quat('Z', 'Y') @ Quaternion((0, 0, 1), t * spin)).to_euler()
        o.scale = ((.35 + .65 * ease_out(t / .3)) * (1 + .07 * math.sin(f * 1.9 + spin * 5)),) * 3
        K(o, f); on(o)
    for o, d_, sp in EMBERS:   # parçalanan Dünya'dan dışarı savrulan kıvılcımlar (ikişer karede, yanıp söner)
        o.location = d_ * (R1 * .9 + (4 + 9 * sp) * ease_out(t / 2.5)); o.scale = ((.6 + .5 * math.sin(f * .9 + sp * 7)) * (1.6 if sp > 1 else 1),) * 3
        K(o, f); on(o)


# v5.9 enerji perdesi. v5.8'deki kameraya düz bakan halka "paint çizimi gibi" bulundu (kullanıcı: derinlik hissi olmalı,
# mor yerine turuncu). Artık Dünya'nın çevresinde YÜKSEKLİĞİ olan bir çember duvar: kamera düzlemin biraz üstünde,
# halka Satürn halkası gibi yandan görünür; arka yay Dünya'nın arkasından, ön yay önünden geçer; ön yüz turuncu, arka yüz
# (içi) sarı-krem; üst kenarda düzensiz sivri ışık perdeleri; mürekkep kontur. Dört çeşit ikişer karede kaynar.
def energy_mat(front, back, name):
    key = ('enerji', front, back)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    g = node(nt, 'ShaderNodeGroup'); g.node_tree = LG
    geo = node(nt, 'ShaderNodeNewGeometry')
    finish(nt, mix(nt, geo.outputs['Backfacing'], C(front), C(back)), g); MATS[key] = m; return m


HOOP_MATS = (energy_mat('#FF6A1E', '#FFB347', 'Enerji perdesi alt'), energy_mat('#FFC24A', '#FFF1C9', 'Enerji perdesi üst'))


def hoop_variant(seed):
    """Yerel XY'de yarıçapı ~1 olan çember duvar; yükseklik yerel Z (alt bant + sivri üst perde)."""
    rr = random.Random(seed); N = 160; ph = [rr.uniform(0, 6.3) for _ in range(3)]
    def nz_(a, s, k): return mnoise.noise(Vector((math.cos(a) * k + seed, math.sin(a) * k, s)))
    bm = bmesh.new(); rows = []
    for i in range(N + 1):
        a = i / N * 2 * math.pi; d = Vector((math.cos(a), math.sin(a), 0)); r = 1 + .035 * nz_(a, 2.0, 3)
        h1 = .03 + .012 * nz_(a, 5.0, 2)
        spike = max(0.0, math.sin(a * 13 + ph[0]) * .6 + math.sin(a * 7 + ph[1]) * .5) ** 4
        h2 = h1 + .025 + .015 * nz_(a, 8.0, 4) + .1 * spike
        rows.append([bm.verts.new(d * r + Vector((0, 0, z))) for z in (-.02, h1, h2)])
    for i in range(N):
        for k in (0, 1):
            fc = bm.faces.new((rows[i][k], rows[i + 1][k], rows[i + 1][k + 1], rows[i][k + 1])); fc.material_index = k
    o = obj(f'Enerji perdesi {seed}', bm, list(HOOP_MATS), .006, smooth=False, toggle=True); o.rotation_mode = 'QUATERNION'
    return o


HOOPS = [hoop_variant(s) for s in (41, 43, 47, 53)]
# Perdenin düzlemi: Dünya'dan SHOCK anındaki kameraya giden doğrultuya göre kamera 8° üstte kalır; düzlem 14° yatık
# (dutch) durur. Yakın yay tam SHOCK'ta kameranın altından geçer, yalnız sivri perdeler kadrajı süpürür.
# (4°'de neredeyse tam yandan görünüyordu: düz bir şerit gibi duruyor, 138'de kadrajı turuncuyla dolduruyordu — flaş gibi)
HOOP_TILT = math.radians(8)
_CAM_HIT = path(SHOCK) + look_q(LDIR, .25 * clamp((SHOCK - 60) / 90)) @ CAM2B_LOCAL
_E1 = _CAM_HIT.normalized(); _Z0 = (Vector((0, 0, 1)) - _E1 * _E1.z).normalized()
_Z0 = Quaternion(_E1, math.radians(14)) @ _Z0
HOOP_N = (_Z0 * math.cos(HOOP_TILT) + _E1 * math.sin(HOOP_TILT)).normalized()
_EX = (_E1 - HOOP_N * _E1.dot(HOOP_N)).normalized()
HOOP_Q = Matrix((_EX, HOOP_N.cross(_EX), HOOP_N)).transposed().to_quaternion()
HOOP_REND = _CAM_HIT.length * math.cos(HOOP_TILT) + 1.0
# Perdenin taşıdığı parçalar: kaya kıymıkları ve kıvılcımlar; yarısı kameranın önünden geçer (yakındakiler hızlı: derinlik)
rw = random.Random(91); WAVE_BITS = []
for i in range(22):
    near = i % 2 == 0
    a = rw.uniform(-.45, .45) if near else rw.uniform(0, 2 * math.pi)
    hf = rw.uniform(.005, .06) if near else rw.uniform(0, .08)
    if i < 14: o = obj(f'Dalga kıymığı {i}', shard_bm(200 + i, .25, .9), ROCK_M if i % 3 else toon(RED), .03, toggle=True)
    else: o = obj(f'Dalga kıvılcımı {i}', ico(rw.uniform(.25, .6), sub=1), glow((HOT, FLAME, RED_HI)[i % 3]), 0, toggle=True)
    WAVE_BITS.append((o, a, hf, rw.uniform(.93, 1.03), Vector((rw.random() * 4, rw.random() * 4, rw.random() * 4))))


def sky_sparkles(f):
    on(stars_world, sparkle_sky)


def escape_trail(f):
    """Motorun arkasında kalan duman: doğduğu yerde kalır, büyür, söner (kaçış boyunca süreklilik)."""
    if 104 <= f < T3A: return   # geriye bakışta yol boyunca dizilen duman Dünya'nın önüne topak gibi oturuyordu (v5.9)
    for k, o in enumerate(trail_a):
        born = 64 + 4 * k
        if born <= min(f, 200): puff_life(o, f, born, path(born) - LDIR * 2.6, -LDIR * .8 + UPV * .2, .4, 1.3 + .04 * k, 2.4)


# ---------------- A: yüzey → kalkış → ateş dağılır (roket) → yanında uçuş → gövdeden arkaya bakış → şok → lomboz
def takeA(f):
    earth_events(f)
    if f >= SWAP: explode_earth(f)
    if f < REVEAL0:   # roket henüz ateşin içinde: görünmesin (gövde parçaları açma/kapama listesinde değil)
        SHIP.location = (0, 0, -5000); K(SHIP, f, True)
    else:              # ateşin içinden roket çıkar (o şey roketmiş): ateş küçülerek dağılır, motor alevi kalır
        roll = .25 * clamp((f - 60) / 90)
        ship(f, path(f), look_q(LDIR, roll), 1.0, 1.0, lit=f >= T3A - 4)
        if f < REVEAL1 + 6:
            g = 1.15 * (1 - ease((f - REVEAL0) / (REVEAL1 + 6 - REVEAL0)))
            for o in PLASMA[(f // 2) % 4]:
                o.scale = (g, g, g); K(o, f, True); on(o)
    if EXPLODE <= f <= SHOCK + 2:   # v5.9: enerji perdesi Dünya'nın çevresinde açılır, yakın yayı tam SHOCK'ta kameradan geçer
        u = (f - EXPLODE) / (SHOCK - EXPLODE)
        R = R1 * 1.15 + (HOOP_REND - R1 * 1.15) * (.3 * u + .7 * u ** 2.2)   # önce hızlı açılır, sonra rokete doğru ivmelenir
        o = HOOPS[(f // 2) % 4]
        o.location = (0, 0, 0); o.rotation_quaternion = HOOP_Q @ Quaternion((0, 0, 1), f * .01); o.scale = (R,) * 3
        K(o, f, True)
        # Son ~3 karede yakın yay kameranın dibinden geçer: kadrajı düz turuncu bir levha gibi kaplıyordu (flaş gibi).
        # Orada perde gizlenir; darbeyi sarsıntı, gövdeye vuran turuncu ışık ve yanımızdan geçen parçalar anlatır.
        if HOOP_REND - R > 45: on(o)
        ex, ey = HOOP_Q @ Vector((1, 0, 0)), HOOP_Q @ Vector((0, 1, 0))
        for b, a, hf, rk, spin in WAVE_BITS:   # perdenin taşıdığı parçalar
            p = (ex * math.cos(a) + ey * math.sin(a)) * R * rk + HOOP_N * R * hf
            if (p - cam.location).length > 3: b.location = p; b.rotation_euler = Euler(spin * f / FPS); K(b, f, True); on(b)
    for o, at, spin in STREAM:   # Dünya'nın parçaları: patlamadan sonra rokete yetişir
        if f >= EXPLODE + 4 and (at - path(f)).length < 70: o.location = at; o.rotation_euler = Euler(spin * f / FPS); K(o, f); on(o)
    # v5.7 sabit açılar: yüzey → (42) geniş → (64, Dünya'nın görünmez değişimi kesmenin altında) yan → (104) geriye bakış
    if f < 42: c = SHOT1
    elif f < SWAP: c = SHOT2
    elif f < 104: c = rig_chase(f)
    else: c = rig_mount(f)
    mount = 1.0 if f >= 104 else 0.0
    put_cam(f, c, .12, .12 * mount * (1 - ease((f - SHOCK) / 8)))
    if 64 <= f < 100: speed_lines(f, .87, ease((f - 64) / 10) * (1 - ease((f - 92) / 8)))
    if 100 <= f < SHOCK + 6: radial_lines(f, screen((0, 0, 0)), 1.0 + .02 * (f - 100), ease((f - 100) / 8) * (1 - ease((f - SHOCK) / 5)))
    dist = path(f).length
    rim_amt = clamp(1.1 - dist / 260) if 42 <= f else 0      # yanan Dünya'nın ışığı roketi turuncu boyar, uzaklaştıkça söner
    tint = (1, .93, .86) if 42 <= f < 50 else ((.86, .86, .95) if 38 <= f < 42 else (1, 1, 1))
    rim_col = RED_HI
    if f >= EXPLODE:   # v5.9: patlamanın ışığı gövdeye vurur, perde yaklaştıkça turuncu ışık büyür (derinlik: ışık uzaktan gelir)
        rim_amt = max(rim_amt, .75 * ease((f - EXPLODE) / 4), ease((f - 128) / 12)); rim_col = FLAME
        if 134 <= f <= SHOCK + 1: tint = (1, .9, .8)
    light(f, (-.55, -.45, .7), tuple(-path(f).normalized()), rim_col, rim_amt, tint,
          edge=.6 + .26 * mount * (1 - ease((f - EXPLODE) / 6) if f >= EXPLODE else 1))   # patlamada kenar ışığı genişler
    escape_trail(f); sky_sparkles(f)


# ---------------- B: kokpit (v4 Ç3a) — shot3a


# ---------------- C: camdan çıkış → motor öksürür, söner → geri çekilme → yakalanma → buluta dalış
OUT3B = 202


CHOKE, TICKS = 186, (190, 194, 200)   # v5.5: önce boğulur (bir duman), sonra tık tık tık (her tıkta kısa alev + duman), susar
# Hepsi çift kare: duman ikişer karede anahtarlanır, tek karede doğarsa bir kare eski yerinde görünürdü; aralık açılır (ölüyor)


def flame_level(f):
    if f < CHOKE: return 1.0
    if f >= OUT3B: return 0.0
    if f - CHOKE < 2: return .3                       # boğulma: alev çöker, kara duman çıkar
    for t, a in zip(TICKS, (.7, .5, .32)):            # tık: tek kare parlar, bir kare söner (her tık bir öncekinden zayıf)
        if f == t: return a
        if f == t + 1: return a * .35
    return 0.0


def takeC(f):
    explode_earth(f)   # Dünya hâlâ geride yanıyor (aynı uzay); kadraja girerse görünür
    on(planet, *DECK)
    P = path(f); fl = flame_level(f)
    drift = look_q(LDIR, .25 + .3 * ease((f - OUT3B) / 30))
    q = drift.slerp(look_q(tangent(f)), ease((f - 232) / 14)) if f >= 232 else drift
    ship(f, P, q, 1.0, fl, hot=f >= 250, smear=SMEAR_C.get(f, 0.0))
    for o, born, r1, life in zip(puffs3, (CHOKE, *TICKS), (.42, .24, .21, .18), (1.1, .75, .75, .8)):
        # Duman roketle birlikte uçar, geriye doğru yavaşça açılır (uzayda hava yok). Doğduğu yerde kalınca roket karede
        # 5 birim ilerlediği için iki karede kadrajdan çıkıyordu (ölçüldü); rokete bağlı olduğu için her karede anahtarlanır.
        a = (f - born) / FPS
        if 0 <= a <= life:
            o.location = path(f) - LDIR * (2.2 + 3.2 * a) + UPV * .35 * a
            sc = .12 + (r1 - .12) * ease_out(a / (life * .5)); sc *= 1 - ease_in((a - life * .7) / (life * .3))
            o.scale = (sc,) * 3; o.rotation_euler = (a * 1.5 + born, a * .7 + born * .3, 0); K(o, f, True); on(o)
    # Kesik krem yörünge izi kaldırıldı (kullanıcı: düşüşteki "tık tık" çizgiler); kıvrılmayı geminin dönüşü anlatır
    if f >= 262:   # atmosfer: burnu ateş sarar
        g = .3 + .7 * ease((f - 262) / 8)
        for o in PLASMA[(f // 2) % 4]:
            o.scale = (g, g, g); K(o, f, True); on(o)
        speed_lines(f, -.95, .5 + .5 * ease((f - 262) / 10))
    # v5.7 sabit açılar: arka üç çeyrek (tekleme) → (202, motor susar) arkadan-yukarıdan → (262, atmosfer) sabit yan
    if f < OUT3B: c = on_ship(TAIL_LOCAL, TAIL_TGT, 30)
    elif f < 262: c = rig_follow(f)
    else: c = SHOT8
    put_cam(f, c, .12, .1 if f < OUT3B else 0, ROLL8 if f >= 262 else 0.0)
    cold = OUT3B <= f < 232
    light(f, (-.55, -.45, .7), (0, 1, 0), FLAME, 0, (.84, .88, 1.0) if cold else (1, 1, 1), sky=SPACE_COLD if cold else SPACE)
    escape_trail(f); sky_sparkles(f)


# ---------------- D: bulutun içinden iner, bulut tabanından çıkar, kamera yere yaklaşıp alçalır; çakılma, sağ kalma
# Anime smear kareleri (v5.6): yalnız roketin ekranda ölçülen hızının kendi boyuyla kıyaslanır olduğu karelerde.
# Ölçüm (kare: hız/boy, hareket–gövde hizası): 306–309 savrulma 0,6–1,1 (kuyruk önde, −1), 312–316 dalış 0,4–0,7 (burun önde, +1).
# Kalkış (≈0,3) ve gezegene dalış (≈0,1) yavaş kaldı: orada smear hata gibi dururdu.
# v5.7 ölçümü (sabit açılar): atmosfere girişte 266–281 roket karede boyunun 0,7–1,0'ı kadar, yerden çekimde 303–316
# 0,35–0,5'i kadar yol alıyor, burun önde (hiza +1). Ölçülen hız kareden kareye titrediği için düzgün bir rampa kullanılır.
SMEAR_C = {f: .35 + .2 * (f - 266) / 15 for f in range(266, 282)}
SMEAR_D = {**{f: .25 + .2 * (f - 303) / 12 for f in range(303, 316)}, 316: .55}


def rocket_d(f):
    if f < IMPACT: return path_d(f), look_q(path_d(f + 1) - path_d(f - 1))
    u = ease_out((f - IMPACT - 2) / 16) if f >= IMPACT + 2 else 0
    bounce = .16 * math.sin(clamp((f - IMPACT - 4) / 6) * math.pi) if f >= IMPACT + 4 else 0
    pos = D1END.lerp(LAND + Vector((.5, 0, .58)), u) + Vector((0, 0, bounce))
    return pos, Euler((.12 * math.exp(-(f - IMPACT) / 8) * math.sin(f), .62 - .32 * u, -.25)).to_quaternion()


def rig_fall(f):
    R = path_d(min(f, IMPACT)); loc = R + CAM_FALL
    return loc, R + Vector((1.8, 1.0, -3.0)), 30


def rig_low(f):
    w = ease((f - IMPACT - 6) / 36) if f > IMPACT + 6 else 0
    loc = (O6 + Vector((0, -10.5, .85))).lerp(O6 + Vector((1.1, -7.6, .7)), w)
    tgt = (O6 + Vector((.3, 0, 1.9))).lerp(LAND + Vector((0, 0, .7)), w)
    if f < IMPACT:   # alçak kamera önce gemiye bakar, gemi indikçe çarpma noktasına eğilir (süzülüşte gemi kadrajdan çıkıyordu)
        tgt = path_d(f).lerp(tgt, ease((f - 298) / 14))
    return loc, tgt, 24


def takeD(f):
    for o, base in LEAVES:
        gust = .12 * math.exp(-(f - IMPACT) / 6) * math.sin(f * 1.3) if f >= IMPACT else 0
        o.rotation_euler = (base.x + .03 * math.sin(f * .2) + gust, base.y, base.z); K(o, f)
    pos, q = rocket_d(f)
    # v5.12 (kullanıcı): burun yere düşene kadar sarı, düştükten sonra rengi yavaşça kendine gelsin; kesmede (302) bir anda
    # kırmızıya dönmesi rahatsız ediyordu. Isı izleri düşüşte yok: hareketsiz sarı çizgiler gövdede ve burnun dışında duruyordu.
    ship(f, pos, q, 1.0, 0, hot=True, traces=False, lit=f >= 338 and not 342 <= f < 345,   # ışık yanar, bir kez göz kırpar: içeride hayat var
         smear=SMEAR_D.get(f, 0.0))
    NOSE_HEAT[0].outputs[0].default_value = 1 - ease((f - IMPACT - 2) / 30)   # 318 → 348 sarı → turuncu → kendi kırmızısı
    NOSE_HEAT[0].outputs[0].keyframe_insert('default_value', frame=f + 1)
    if f < CUT10:   # v5.7: burun ateşi bulutların içinde sürer, düşüşte yavaşça küçülüp söner
        g = .95 * (1 - ease((f - TDIVE) / 17))          # (v5.6'da bulut kesmesinde bir anda kayboluyordu)
        gi = g * clamp((g - .35) / .35)                 # v5.12: iç katman önce söner; küçülünce burunda kırmızı leke gibi kalıyordu
        o, o2 = PLASMA[(f // 2) % 4]
        for ob, s in ((o, g), (o2, gi)):
            if s > .12: ob.scale = (s, s, s); K(ob, f, True); on(ob)   # en sonda burun ucunda küçük topak kalmasın
    for k, o in enumerate(smoke_d):   # duman izi: doğduğu yerde kalır
        born = TDIVE + 2 * k
        if born < IMPACT: puff_life(o, f, born, path_d(born) - (path_d(born + 1) - path_d(born)).normalized() * 1.8, Vector((0, 0, .3)), .25, .6, 1.3)
    if f >= IMPACT:
        on(ground_cracks)
        if f >= IMPACT + 4: on(skid)
        a = (f - IMPACT) / FPS
        for o, (dirv, rise, size) in zip(dust6, DUST_P):
            if a > 1.05: continue
            sc = size * (.2 + .65 * ease_out(a / .3)) * (1 - ease_in((a - .55) / .5))
            o.location = LAND + dirv * 2.2 * (1 - math.exp(-a * 5)) + Vector((0, 0, .3 + rise * (1 - math.exp(-a * 3.5))))
            o.scale = (sc,) * 3; o.rotation_euler = (a, a * .5, 0); K(o, f); on(o)
        for o, v in zip(rocks6, ROCK_V):
            p_ = LAND + Vector((0, 0, .4)) + v * a + Vector((0, 0, -6.5 * a * a))
            if p_.z < 0: continue
            o.location = p_; o.rotation_euler = (a * 8, a * 5, 0); K(o, f); on(o)
        cam_dir = (cam.location - LAND).normalized()
        for o, rim, (v, sz) in zip(pof6, pof6_rim, POF_V):   # krem çiçek-pof: büyür, söner (oyundaki pof dili)
            if a > .7: continue
            sc = sz * ease_out(a / .25) * (1 - ease_in((a - .4) / .3))
            for ob, z in ((o, 0), (rim, -.02)):
                ob.location = LAND + v * (1 + 1.5 * a) + cam_dir * (.4 + z); ob.rotation_euler = cam_dir.to_track_quat('Z', 'Y').to_euler()
                ob.scale = (sc,) * 3; K(ob, f); on(ob)
        for o, v in zip(glit6, GLIT_V):   # elmas kıvılcımlar
            if a > .8: continue
            o.location = LAND + Vector((0, 0, .5)) + v * ease_out(a / .6) + cam_dir * .6
            o.rotation_euler = cam_dir.to_track_quat('Z', 'Y').to_euler(); o.scale = ((1 - ease_in((a - .4) / .4)) * (.7 + .3 * math.sin(f)),) * 3
            K(o, f); on(o)
    for k, o in enumerate(steam6):   # gemiden çıkan küçük buhar: duruştan sonra
        born = 326 + 5 * k
        puff_life(o, f, born, LAND + Vector((-.1 + .2 * k, -.1, 1.0)), Vector((.15, .05, .9)), .08, .28 + .04 * k, 1.0)
    if 338 <= f < 346:
        port_ring.scale = (.3 + 1.4 * ease_out((f - 338) / 8),) * 3; K(port_ring, f, True); on(port_ring)
    if f in (IMPACT, IMPACT + 1): on(impact_lines)
    # v5.7 sabit açılar: rokete göre sabit düşüş (bulutların içinden) → (300) yerden sabit, roket yukarıdan gelip çakılır →
    # (328) yakın sabit. v5.6'daki süzülüş (292–306) roketi ekranda iki karede 74° döndürüyordu (ölçüldü)
    c = rig_fall(f) if f < CUT10 else (SHOT10 if f < 328 else SHOT11)
    put_cam(f, c, .12)
    if f < TDIVE + 6:   # bulutun içinden çıkış: saran bulut kameranın arkasına geçer
        k = ease_out((f - TDIVE) / 6)
        engulf.location = (0, .3 * k, -1.4 + 4.0 * k); engulf.scale = (2.3 - 1.0 * k,) * 3; K(engulf, f, True); on(engulf)
    if f < 300: speed_lines(f, math.pi / 2, .8)
    k = ease((f - 298) / 8); sky = tuple(a_ + (b_ - a_) * k for a_, b_ in zip(C(SKY5), C(SKY6)))
    light(f, (-.55, -.45, .7), (0, 1, .15), '#F0A060', 0, impact=1.0 if f in (IMPACT, IMPACT + 1) else 0.0, sky=sky, neb=0.0)


FNS = [takeA, shot3a, takeC, takeD]


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
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'IntroV5.blend'))


def render_frames(frames, folder, prefix=''):
    for F in frames:
        s.frame_set(F); s.render.filepath = str(folder / f'{prefix}{F:04d}.png'); bpy.ops.render.render(write_still=True)


if ARGS == ['all']:
    # Boş yeni klasöre çizilir, bitince eskisinin yerine geçer: üstüne yazılan kareleri bir önizleyici/tarayıcı ara ara
    # kilitliyordu ("cannot save", 4 Kasım) ve render yarıda kalınca eski ve yeni kareler karışıyordu.
    import shutil
    TMP = OUT / 'kareler_yeni'
    shutil.rmtree(TMP, ignore_errors=True); TMP.mkdir()
    s.render.filepath = str(TMP) + '/'; bpy.ops.render.render(animation=True)
    if len(list(TMP.glob('[0-9][0-9][0-9][0-9].png'))) != END: raise SystemExit(f'EKSIK KARE: {TMP}')
    shutil.rmtree(FR, ignore_errors=True)
    if FR.exists():   # silinemeyen (kilitli) kare kaldıysa yenileri tek tek üstüne kopyala
        for p in TMP.glob('*.png'): shutil.copy2(p, FR / p.name)
        shutil.rmtree(TMP, ignore_errors=True)
    else: TMP.rename(FR)
    glint.animation_data_clear()   # ipucu çeşidi: yalnız iki kare (Ç4, yakalanma anı) yeşil parıltıyla
    for F in (GLINT_F + 1, GLINT_F + 2): glint.hide_render = False; glint.keyframe_insert('hide_render', frame=F)
    render_frames((GLINT_F + 1, GLINT_F + 2), FR_GLINT)
else:
    render_frames([int(a) + 1 for a in ARGS if a.isdigit()], FR, 'deneme_')
print('INTRO V5 DONE', ARGS[:3])
