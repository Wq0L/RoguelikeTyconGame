"""Açgözlü Hasat — "aç ağız halkası" toon VFX örneği (24 Ekim 2026, cila 2). Özgün geometri; dış model/doku/eklenti yok.
  blender -b --factory-startup -P Tools/Art/GreedJaw/build_greed_jaw.py
Çıktı: Docs/GreedJaw/frames (yakın plan, etiketli), Docs/GreedJaw/game_frames (oyun kamerası mesafesi), GreedJaw.blend.
Toon dil: düz 2–3 ton + mürekkep kontur, el yapımı düzensizlik, parlama/bloom yok, mavi yok, yerde ek halka yok.
6 sn, 24 kare/sn: hak belirir → imleçle gezer (süzülme, durma, hızlı fiske) → 2+ bitki "hap!" → yeni hak → tek hedef, kayıp.
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Euler, Quaternion

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'GreedJaw'
FRAMES = OUT / 'frames'
FRAMES.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.view_settings.view_transform = 'Standard'
try: s.eevee.taa_render_samples = 12
except Exception: pass
s.render.fps = 24
s.frame_start, s.frame_end = 1, 96


def srgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lin(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


INK, CREAM, CREAM_SHADE = srgb('#1A1324'), srgb('#FFF4D6'), srgb('#EAD7AE')
GUM, GUM_TOP = srgb('#7A2448'), srgb('#A8395F')
LEAF, LEAF_DARK, RING = srgb('#5DBB4C'), srgb('#3E8E3A'), srgb('#F4F1EA')
DUST, DUST_SHADE, SPACE = srgb('#E6DEEA'), srgb('#BDB0C9'), srgb('#141633')
CAM_ROT = (55, 0, 45)


def view_dir():
    return (Euler(tuple(math.radians(a) for a in CAM_ROT)).to_matrix() @ Vector((0, 0, -1))).normalized()


# ---------------------------------------------------------------- malzemeler (meteorla aynı toon kurgusu)
def mat_toon_attr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission'); at = n.new('ShaderNodeAttribute'); at.attribute_name = 'Col'
    dif = n.new('ShaderNodeBsdfDiffuse'); rgb = n.new('ShaderNodeShaderToRGB'); ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0, (.5, .5, .5, 1)
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


INK_M = mat_flat('Ink', INK, toon=False); INK_M.use_backface_culling = True
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


def empty(name, parent=None, at=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); s.collection.objects.link(e); e.location = at
    if parent: e.parent = parent
    return e


def key(o, f, loc=None, scale=None, rotq=None):
    if loc is not None: o.location = loc; o.keyframe_insert('location', frame=f)
    if scale is not None:
        o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale; o.keyframe_insert('scale', frame=f)
    if rotq is not None: o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = rotq; o.keyframe_insert('rotation_quaternion', frame=f)


# ---------------------------------------------------------------- bağlam: oyun benzeri tarla + beyaz imleç halkası
def box(name, size, at, mat):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1); bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    o = obj_from_bm(name, bm, mat); o.location = at; return o


def cone(name, at, h, r, mat, seed):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r, radius2=0, depth=h)
    o = obj_from_bm(name, bm, mat); o.location = at + Vector((0, 0, h / 2)); o.rotation_euler = (0, 0, seed); return o


RIM_M, SOIL_M, PLANT_M = mat_flat('Saksı', srgb('#C9CED6')), mat_flat('Toprak', srgb('#3B2A20')), mat_flat('Bitki', srgb('#3E9B3A'))
rr = random.Random(5)
for x in range(-5, 6):
    for y in range(-5, 6):
        c = Vector((x * 2.0, y * 2.0, 0))
        box(f'rim {x},{y}', (1.85, 1.85, .35), c, RIM_M)
        box(f'soil {x},{y}', (1.3, 1.3, .38), c + Vector((0, 0, .02)), SOIL_M)
        for k in range(rr.randint(1, 3)):
            cone(f'plant {x},{y},{k}', c + Vector((rr.uniform(-.35, .35), rr.uniform(-.35, .35), .2)), rr.uniform(.45, .9), .17, PLANT_M, rr.uniform(0, 3))

CURSOR = Vector((0, 0, 0))
R = 1.5        # oyundaki saldırı yarıçapı halkası (yaklaşık)
Z = .26        # halkanın zemin yüksekliği

bm = bmesh.new()
inner, outer = [], []
for i in range(64):
    a = 2 * math.pi * i / 64
    d = Vector((math.cos(a), math.sin(a), 0))
    inner.append(bm.verts.new(CURSOR + d * (R - .035) + Vector((0, 0, Z))))
    outer.append(bm.verts.new(CURSOR + d * (R + .035) + Vector((0, 0, Z))))
for i in range(64):
    j = (i + 1) % 64
    bm.faces.new((inner[i], outer[i], outer[j], inner[j]))
ring = obj_from_bm('İmleç halkası (oyundaki beyaz çizgi)', bm, UNLIT)
paint(ring, lambda p: RING)

# ---------------------------------------------------------------- aç ağız: iki çene (diş eti + dişler)
vd = view_dir()
far = Vector((vd.x, vd.y, 0)).normalized()          # ekranda yukarı (üst çene)
FAR_ANGLE = math.atan2(far.y, far.x)


def gum_arc(name, center_angle, parent, seed):
    """Halkanın hemen içinde kalın, düzensiz iç kenarlı diş eti yayı (yerde halka değil: halkayla aynı yerde)."""
    r = random.Random(seed)
    bm = bmesh.new()
    n = 12
    span = math.radians(68)
    rows = []
    for i in range(n + 1):
        a = center_angle - span + 2 * span * i / n
        d = Vector((math.cos(a), math.sin(a), 0))
        taper = math.sin(math.pi * i / n) ** .5           # uçlara doğru incelir (ağız köşeleri)
        r_in = R - .08 - .26 * taper * r.uniform(.85, 1.05)
        r_out = R - .02
        h = .06 + .07 * taper
        rows.append([bm.verts.new(d * r_in + Vector((0, 0, Z))), bm.verts.new(d * r_out + Vector((0, 0, Z))),
                     bm.verts.new(d * r_out + Vector((0, 0, Z + h))), bm.verts.new(d * r_in + Vector((0, 0, Z + h)))])
    for i in range(n):
        a, b = rows[i], rows[i + 1]
        for k in range(4):
            bm.faces.new((a[k], b[k], b[(k + 1) % 4], a[(k + 1) % 4]))
    bm.faces.new(list(reversed(rows[0]))); bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.normal_update()
    o = obj_from_bm(name, bm, LIT, ink=.025, parent=parent)
    paint(o, lambda p: GUM_TOP if p.normal.z > .5 else GUM)
    return o


def tooth(name, angle, parent, size, lift, seed):
    """Merkeze bakan sivri piramit diş (köpek dişi gibi hafif kıvrık). Kökeni diş etindeki tabanı (oradan büyür/sarkar).
    lift: ucun yukarı kalkışı; yakın (alt) çenede büyük, böylece eğik kamerada kısa blok gibi değil sivri diş okunur."""
    r = random.Random(seed)
    d = Vector((math.cos(angle), math.sin(angle), 0)); t = Vector((-d.y, d.x, 0)); up = Vector((0, 0, 1))
    base_r = R - .16 - .1 * size
    w, L = .12 * size * r.uniform(.9, 1.1), .36 * size * r.uniform(.92, 1.08)
    bend = t * r.uniform(-.04, .04)
    bm = bmesh.new()
    base = [bm.verts.new(t * -w), bm.verts.new(t * w), bm.verts.new(d * .05 + up * .2 * size)]   # diş etine yaslanan taban
    c = -d * L * .5 + up * (lift * L * .45 + .05 * size) + bend * .5
    mid = [bm.verts.new(c - t * w * .62), bm.verts.new(c + t * w * .62), bm.verts.new(c + up * .11 * size + d * .03)]
    tip = bm.verts.new(-d * L + up * lift * L + bend)
    bm.faces.new(base)
    for i in range(3):
        j = (i + 1) % 3
        bm.faces.new((base[i], base[j], mid[j], mid[i])); bm.faces.new((mid[i], mid[j], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.normal_update()
    o = obj_from_bm(name, bm, LIT, ink=.032, parent=parent)
    o.location = d * base_r + Vector((0, 0, Z + .04))
    paint(o, lambda p: CREAM if p.normal.dot(-vd) > .35 else CREAM_SHADE)
    return o, d, t


def jaw(name, center_angle, lift, seed):
    j = empty(name)
    gum_arc(name + ' diş eti', center_angle, j, seed)
    teeth = []
    for k in range(7):
        a = center_angle + math.radians(-56 + 112 * k / 6 + random.Random(seed + k).uniform(-3, 3))
        size = 1.35 if k in (1, 5) else (.85 if k == 3 else 1.0)      # iki köpek dişi, ortada küçük diş
        teeth.append(tooth(f'{name} diş {k}', a, j, size, lift, seed + k))
    return j, teeth


upper, upper_teeth = jaw('Üst çene', FAR_ANGLE, .1, 11)
lower, lower_teeth = jaw('Alt çene', FAR_ANGLE + math.pi, .75, 23)

# ---------------------------------------------------------------- kırıntılar, hareket çizgileri, kayıp tozu
def chip(name, size, color, seed):
    r = random.Random(seed)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=size)
    for v in bm.verts: v.co = v.co.normalized() * size * (1 + r.uniform(-.25, .25))
    bmesh.ops.scale(bm, vec=(1.4, .8, .5), verts=bm.verts)
    o = obj_from_bm(name, bm, LIT, ink=.012); paint(o, lambda p: color); return o


def card_shape(name, pts2d, color, ink_pad):
    """Kameraya bakan düz şekil (mürekkep kopyası arkada)."""
    sup = (Vector((0, 0, 1)) - vd * vd.z).normalized(); sr = vd.cross(sup).normalized()
    root = empty(name)
    for j, (pad, col, fwd) in enumerate([(ink_pad, INK, 0.0), (0.0, color, .02)]):
        bm = bmesh.new()
        c = Vector((0, 0, 0)) - vd * fwd
        cv = bm.verts.new(c)
        n = len(pts2d)
        ring_v = []
        for (x, y) in pts2d:
            p = Vector((x, y)); p = p + p.normalized() * pad if p.length > 1e-6 else p
            ring_v.append(bm.verts.new(c + sr * p.x + sup * p.y))
        for i in range(n): bm.faces.new((cv, ring_v[i], ring_v[(i + 1) % n]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT, parent=root); paint(o, lambda p, col=col: col)
    return root


rng = random.Random(77)
chips = []
for k in range(9):
    a = 2 * math.pi * k / 9 + rng.uniform(-.3, .3)
    col = [LEAF, LEAF_DARK, CREAM][k % 3]
    chips.append((chip(f'Kırıntı {k}', rng.uniform(.06, .1), col, k), Vector((math.cos(a), math.sin(a), 0)), rng.uniform(.6, 1.2), rng.uniform(.5, .9)))

# Hap! dikenleri: ağzın üstünde yelpaze gibi açılan krem, mürekkep kenarlı kısa dikenler (ekran düzleminde kart)
lines = []
sup = (Vector((0, 0, 1)) - vd * vd.z).normalized(); sr = vd.cross(sup).normalized()
for k, ang in enumerate((28, 62, 90, 118, 152)):
    a = math.radians(ang)
    ln = .3 if k in (0, 4) else .38
    pts = []
    for (x, y) in ((-.06, -ln * .45), (.06, -ln * .45), (0, ln * .55)):
        ra = a - math.pi / 2
        pts.append((x * math.cos(ra) - y * math.sin(ra), x * math.sin(ra) + y * math.cos(ra)))
    root = card_shape(f'Hap dikeni {k}', pts, CREAM, .035)
    root.location = CURSOR + Vector((0, 0, .4)) + sr * (1.25 * math.cos(a)) + sup * (.32 + .55 * math.sin(a))
    lines.append(root)

# Kayıp: kopan dişin düştüğü yerde küçük toz pofu (yüz/göz gibi okunmasın diye ağzın içinde değil, halkanın dışında)
def scallop(rad, seed):
    r = random.Random(seed)
    return [(math.cos(2 * math.pi * i / 18) * rad * (1 + .22 * abs(math.sin(3 * 2 * math.pi * i / 18 + r.uniform(0, .5)))),
             math.sin(2 * math.pi * i / 18) * rad * .8 * (1 + .22 * abs(math.sin(3 * 2 * math.pi * i / 18 + r.uniform(0, .5))))) for i in range(18)]
FALLING = (1, 4, 8, 11)
puffs = [card_shape(f'Kayıp tozu {k}', scallop(.11, k), DUST, .03) for k in range(len(FALLING))]

# ---------------------------------------------------------------- animasyon (24 Ekim 2026, cila 2): imleçle hareket + dönen mini dişler
# Kare kare yordamsal: her karede oyundaki sunucunun yapacağı hesap (konum, hız, yay, durum yaşı) burada da yapılır; Unity'ye aynen taşınır.
# Ağız gövdesi halkaya yapışık kalır (gecikme saldırı alanını yanlış gösterirdi); his dişlerden gelir:
#   · hareket: dişler rüzgârda gibi geriye yatar (yaylı, durunca ileri sallanır), hızlıyken ağız biraz daha açılır;
#   · beklemede ara ara iki kısa "çat-çat";
#   · 5 mini diş halkanın hemen dışında döner, hızlı harekette biraz geride kalır;
#   · belirmede içeri sarmallar, hap'ta ağza emilir, kayıpta saçılıp düşer.
FPS, N = 24, 144
HIDE = .001
gx = sr.copy(); gx.z = 0; gx.normalize()
P_A, P_B, P_C = gx * -2.6 + far * -.6, gx * -.2 + far * 1.3, gx * 2.4 + far * -.45
P_D, P_E = gx * -.9 + far * -.85, gx * .4 + far * -.4
T_APPEAR, T_CHOMP, T_REAPPEAR, T_LOSE = .1, 3.35, 4.75, 5.3


def smooth(x):
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)


def ease_out(x):
    x = min(1.0, max(0.0, x)); return 1 - (1 - x) ** 3


def back_out(x, k=2.2):
    x = min(1.0, max(0.0, x)); x -= 1; return 1 + x * x * ((k + 1) * x + k)


def bump(x, w):
    return math.sin(math.pi * x / w) if 0 < x < w else 0.0


def cursor_at(t):
    perp = Vector((-(P_B - P_A).y, (P_B - P_A).x, 0)).normalized()
    if t < .55: return P_A.copy()
    if t < 1.6:
        u = smooth((t - .55) / 1.05); return P_A.lerp(P_B, u) + perp * (.55 * math.sin(math.pi * u))   # yumuşak süzülme (kavisli)
    if t < 2.4: return P_B.copy()
    if t < 2.72: return P_B.lerp(P_C, smooth((t - 2.4) / .32))                                       # hızlı fiske
    if t < 4.3: return P_C.copy()
    if t < 4.6: return P_C.lerp(P_D, smooth((t - 4.3) / .3))
    if t < 5.25: return P_D.lerp(P_E, smooth((t - 4.6) / .65))
    return P_E.copy()


cursor = empty('İmleç')
for o in (ring, upper, lower): o.parent = cursor

# Mini dişler: aynı diş biçimi, yarım boy; açı 0'da kurulur (dışa yön +x), her karede z çevresinde döndürülür.
MINI_R = R + .2
minis = []
for k, base in enumerate((0.0, 1.15, 2.45, 3.55, 4.95)):
    o, d, t = tooth(f'Mini diş {k}', 0.0, cursor, .7, .55, 300 + k)
    o.rotation_mode = 'QUATERNION'; minis.append((o, base))

all_teeth = upper_teeth + lower_teeth
homes = [o.location.copy() for o, d, t in all_teeth]
for o, d, t in all_teeth: o.rotation_mode = 'QUATERNION'
jaws = [(upper, far), (lower, -far)]

# Yay durumu (alt adımlı): diş eğimi (az sönümlü → durunca sallanır), mini dişlerin geride kalan merkezi.
lean, lean_v = Vector((0, 0, 0)), Vector((0, 0, 0))
lag, lag_v = cursor_at(0), Vector((0, 0, 0))
spin = 0.0
SUB = 4
dt = 1 / (FPS * SUB)

chomp_at = cursor_at(T_CHOMP); lose_at = cursor_at(T_LOSE)
spike_home = [l.location - CURSOR for l in lines]

for f in range(1, N + 1):
    t0 = (f - 1) / FPS
    for k in range(SUB):
        t = t0 + k * dt
        v = (cursor_at(t + dt) - cursor_at(t - dt)) / (2 * dt)
        target = -v * .045
        if target.length > .55: target = target.normalized() * .55
        a = (target - lean) * 140 - lean_v * 9.5
        lean_v += a * dt; lean += lean_v * dt
        p = cursor_at(t)
        a2 = (p - lag) * 260 - lag_v * 22
        lag_v += a2 * dt; lag += lag_v * dt
        spin += (1.5 + .22 * min(v.length, 12)) * dt
    t = t0
    p = cursor_at(t); v = (cursor_at(t + dt) - cursor_at(t - dt)) / (2 * dt); speed = v.length
    key(cursor, f, loc=p)

    # durum
    if t < T_APPEAR: state, age = 'none', 0
    elif t < T_CHOMP: state, age = 'ready', t - T_APPEAR
    elif t < T_CHOMP + .45: state, age = 'chomp', t - T_CHOMP
    elif t < T_REAPPEAR: state, age = 'none', 0
    elif t < T_LOSE: state, age = 'ready', t - T_REAPPEAR
    elif t < T_LOSE + .65: state, age = 'lose', t - T_LOSE
    else: state, age = 'none', 0

    lean_q = Quaternion(Vector((0, 0, 1)).cross(lean.normalized()), lean.length) if lean.length > 1e-4 else Quaternion()
    gape = min(1.0, speed / 6.0)
    cycle = (t - T_APPEAR) % 1.3
    chatter = (bump(cycle - .9, .09) + bump(cycle - 1.04, .09)) * (1 - gape)

    for j, out in jaws:
        if state == 'none': key(j, f, loc=Vector((0, 0, 0)), scale=HIDE); continue
        if state == 'ready':
            s_ = back_out(age / .22, 1.2)
            off = -.05 + .07 * gape - .09 * chatter
            key(j, f, loc=out * off, scale=max(HIDE, s_))
        elif state == 'chomp':
            if age < .06: off, sc = .02 + .12 * smooth(age / .06), (1, 1, 1)
            elif age < .12: u = smooth((age - .06) / .06); off, sc = .14 - .84 * u, (1, 1, 1 - .2 * u)
            elif age < .22: u = smooth((age - .12) / .1); off, sc = -.7 + .06 * u, (1, 1, .8 + .12 * u)
            else: u = smooth((age - .22) / .2); off, sc = -.64 + .14 * u, tuple(max(HIDE, x * (1 - u)) for x in (1, 1, .92))
            key(j, f, loc=out * off, scale=sc)
        else:   # lose
            if age < .12: off, sc = -.25 * smooth(age / .12), (1, 1, 1)
            elif age < .3: off, sc = -.25 + .07 * smooth((age - .12) / .18), (1, 1, 1)
            else:
                u = smooth((age - .3) / .32); off = -.18 + .08 * u
                sc = tuple(max(HIDE, x * (1 - smooth((age - .5) / .12))) for x in (1 - .06 * u, 1 - .06 * u, 1 - .2 * u))
            key(j, f, loc=out * off, scale=sc)

    for i, ((o, d, tt), home) in enumerate(zip(all_teeth, homes)):
        if state == 'none': key(o, f, loc=home, scale=HIDE, rotq=Quaternion()); continue
        if state == 'ready':
            pop = back_out((age - .03 * (i % 7)) / .14, 3.0)
            wob = Quaternion(tt, .12 * chatter)
            key(o, f, loc=home, scale=max(HIDE, pop), rotq=lean_q @ wob)
        elif state == 'chomp':
            key(o, f, loc=home, scale=1.0, rotq=Quaternion())
        else:
            if i in FALLING and age > .1:
                u = (age - .1) / .35
                pos = home + d * (.4 * u) + Vector((0, 0, .5 * math.sin(math.pi * min(u, 1) * .8) - .25 * u * u))
                key(o, f, loc=pos, scale=max(HIDE, .9 * (1 - smooth((age - .45) / .08))), rotq=Quaternion(tt, math.radians(200) * min(u, 1)))
            else:
                droop = Quaternion(tt, math.radians(-30 + (i % 3) * 6) * smooth((age - .1) / .15))
                key(o, f, loc=home, scale=max(HIDE, .95 * (1 - smooth((age - .5) / .1))), rotq=droop)

    trail_off = lag - p
    if trail_off.length > .3: trail_off = trail_off.normalized() * .3
    for k, (o, base) in enumerate(minis):
        th = base + spin
        dirv = Vector((math.cos(th), math.sin(th), 0))
        nib = Quaternion(Vector((-dirv.y, dirv.x, 0)), .22 * math.sin(7 * t + k * 1.7))
        rot = Quaternion(Vector((0, 0, 1)), th)
        z = Z + .06 + .04 * math.sin(3 * t + k * 1.3)
        if state == 'none': key(o, f, loc=dirv * MINI_R + Vector((0, 0, z)), scale=HIDE, rotq=rot); continue
        if state == 'ready':
            u = ease_out((age - .06 - .04 * k) / .35)
            r_ = MINI_R + .7 * (1 - u)
            key(o, f, loc=dirv * r_ + trail_off + Vector((0, 0, z)), scale=max(HIDE, back_out((age - .06 - .04 * k) / .2)), rotq=lean_q @ nib @ rot)
        elif state == 'chomp':
            u = smooth((age - .05 - .015 * k) / .14)
            key(o, f, loc=dirv * (MINI_R * (1 - u) + .15 * u) + Vector((0, 0, z + .15 * u)), scale=max(HIDE, 1 - u), rotq=rot)
        else:
            u = (age - .08) / .45
            if u <= 0: key(o, f, loc=dirv * MINI_R + Vector((0, 0, z)), scale=1.0, rotq=rot); continue
            pos = dirv * (MINI_R + .6 * min(u, 1)) + Vector((0, 0, z + .35 * math.sin(math.pi * min(u, 1) * .7) - .3 * u * u))
            key(o, f, loc=pos, scale=max(HIDE, 1 - smooth((u - .7) / .3)), rotq=Quaternion(Vector((-dirv.y, dirv.x, 0)), 4 * min(u, 1)) @ rot)

    # hap: dikenler + kırıntılar (hap konumunda, dünya uzayında)
    ca = t - T_CHOMP
    for k, l in enumerate(lines):
        x = ca - .06 - .012 * (k % 2)
        sc = 0 if x < 0 or x > .3 else (back_out(x / .07) if x < .07 else 1 - smooth((x - .18) / .12))
        key(l, f, loc=chomp_at + spike_home[k], scale=max(HIDE, 1.2 * sc))
    for o, dirv, dist, up in chips:
        x = (ca - .07) / .45
        if x < 0 or x > 1: key(o, f, loc=chomp_at + Vector((0, 0, .4)), scale=HIDE); continue
        pos = chomp_at + dirv * dist * ease_out(x) * 1.1 + Vector((0, 0, .4 + up * math.sin(math.pi * min(1, x * 1.2)) * .9))
        key(o, f, loc=pos, scale=max(HIDE, 1 - smooth((x - .7) / .3)))
    la = t - T_LOSE
    for k, (pf, i) in enumerate(zip(puffs, FALLING)):
        o, d, tt = all_teeth[i]
        jaw_shift = (far if i < 7 else -far) * -.18
        at = lose_at + jaw_shift + Vector((homes[i].x, homes[i].y, 0)) + d * .4 + Vector((0, 0, .26))
        x = (la - .45) / .3
        sc = 0 if x < 0 or x > 1 else (back_out(x / .3) if x < .3 else 1 - smooth((x - .5) / .5))
        key(pf, f, loc=at + Vector((0, 0, .2 * max(0, x))), scale=max(HIDE, 1.1 * sc))

# ---------------------------------------------------------------- ışık, kamera, etiket (etiket yalnız önizleme videosunda)
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); s.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38), math.radians(-18), math.radians(-40)); sun.data.energy = 3
s.world = bpy.data.worlds.new('Uzay'); s.world.use_nodes = True
bg = s.world.node_tree.nodes['Background']; bg.inputs[0].default_value = (*lin(SPACE), 1)

VIEW_W = 9.4
FOCUS = gx * -.1 + far * .3 + Vector((0, 0, .5))
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam)
cam.rotation_euler = tuple(math.radians(a) for a in CAM_ROT)
cam.location = FOCUS + cam.rotation_euler.to_matrix() @ Vector((0, 0, 30))
cam.data.type = 'ORTHO'; cam.data.ortho_scale = VIEW_W
cam.data.clip_end = 100
s.camera = cam


def label(text, t_in, t_out, slot):
    o = bpy.data.objects.new(f'Etiket {slot}', bpy.data.curves.new(f'Etiket {slot}', 'FONT'))
    s.collection.objects.link(o)
    o.data.body = text; o.data.size = .26 * VIEW_W / 6.2; o.data.align_x = 'LEFT'
    o.data.materials.append(mat_flat(f'Etiket rengi {slot}', srgb('#FFF4D6'), toon=False))
    o.parent = cam; o.location = (-VIEW_W / 2 + .25, VIEW_W * 600 / 960 / 2 - .6, -5)
    f_in, f_out = int(t_in * FPS) + 1, int(t_out * FPS) + 1
    key(o, 1, scale=HIDE); key(o, max(1, f_in - 1), scale=HIDE); key(o, f_in, scale=1.0)
    key(o, f_out - 1, scale=1.0); key(o, f_out, scale=HIDE)


label('HAZIR: imleçle gezer, mini dişler döner', 0, T_CHOMP, 0)
label('2+ BİTKİ: HAP!  hasar x1,5', T_CHOMP, T_CHOMP + .9, 1)
label('yeni hak (tek vuruşta 3+ hasat)', T_REAPPEAR - .1, T_LOSE, 2)
label('TEK HEDEF: dişler düşer, hak kaybolur', T_LOSE, 6.01, 3)

s.frame_start, s.frame_end = 1, N
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 600, 100
s.render.image_settings.file_format = 'PNG'
s.render.filepath = str(FRAMES / 'f_')
bpy.ops.render.render(animation=True)

# Oyun mesafesi: GameScene kamerası ortografik boyut 11 (dikey 22 birim), 1920×1080; etiketsiz, ffmpeg ile ortadan kırpılır.
for o in bpy.data.objects:
    if o.name.startswith('Etiket'): o.hide_render = True
cam.data.ortho_scale = 22 * 1920 / 1080
s.render.resolution_x, s.render.resolution_y = 1920, 1080
GAME = OUT / 'game_frames'; GAME.mkdir(exist_ok=True)
s.render.filepath = str(GAME / 'g_')
bpy.ops.render.render(animation=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'GreedJaw.blend'))
print('GREED JAW DONE')
