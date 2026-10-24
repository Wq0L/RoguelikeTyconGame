"""Açgözlü Hasat "aç ağız halkası"nı Unity için dışa aktarır (24 Ekim 2026).
  blender -b --factory-startup -P Tools/Art/GreedJaw/export_greed_jaw.py
build_greed_jaw.py'nin geometri bölümü (animasyon/render öncesi) aynı süreçte çalıştırılır, sonra ORTAK ağlar kurulur:
  2 diş eti (üst/alt), 6 diş tipi (köpek/normal/küçük × üst/alt kalkış), 1 mini diş, 3 kırıntı rengi, 1 diken kartı, 1 toz kartı.
Çıktı: Tools/Art/GreedJaw/export/greed_jaw.json (Unity JsonUtility biçimi).
- Işıklı parçalar: gövde = modifier'sız taban ağı (köşe rengi doğrusal), mürekkep = Solidify kabuğunun yüzleri (malzeme 1).
  Kabuk Blender'daki gibi içe bakar; Unity'de arka yüz ayıklamalı mürekkep malzemesiyle yalnız arkadaki kenar görünür.
  (Projedeki ThornInk merkezden dışa iter; uzun kavisli diş etinde bu yanlış yöne iterdi.)
- Çene uzayı: Blender (x, y, z) → Unity (x, z, y), üçgen sırası çevrilir; çenenin "dış" yönü Unity +Z, halka zemini y = 0.
- Diş yerleşimi: çene uzayında konum + Y çevresinde dönüş (derece) + tip + ölçek; dişin kökeni tabanıdır (sarkma oradan).
- Kartlar: kart uzayı (x ekranda sağ, y ekranda yukarı, z kamera bakış yönü); Unity her karede kameraya çevirir.
"""
import bpy, json, math, random
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
source = (HERE / 'build_greed_jaw.py').read_text(encoding='utf-8')
marker = '# ---------------------------------------------------------------- animasyon'
exec(compile(source[:source.index(marker)], str(HERE / 'build_greed_jaw.py'), 'exec'))

EXPORT = HERE / 'export'
EXPORT.mkdir(exist_ok=True)
INK_LIN = lin(INK)


def to_unity(p, origin):
    q = p - origin
    return (q.x, q.z, q.y)


def lit_mesh(o, origin):
    """Düz gölgeli gövde (taban ağı) + mürekkep kabuğu (değerlendirilmiş ağın malzeme 1 yüzleri). Yerel uzay."""
    pos, col, body, ink = [], [], [], []

    def add(points, colors, into):
        idx = []
        for p, c in zip(points, colors):
            idx.append(len(pos) // 3)
            pos.extend(round(v, 5) for v in to_unity(p, origin))
            col.extend(round(v, 5) for v in c[:3])
        into.extend((idx[0], idx[2], idx[1]))           # el yönü değişti: sıra çevrilir

    me = o.data
    me.calc_loop_triangles()
    attr = me.color_attributes['Col']
    for lt in me.loop_triangles:
        add([me.vertices[v].co.copy() for v in lt.vertices], [attr.data[l].color for l in lt.loops], body)
    ev = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    em = ev.to_mesh()
    em.calc_loop_triangles()
    for lt in em.loop_triangles:
        if lt.material_index == 1:
            add([em.vertices[v].co.copy() for v in lt.vertices], [INK_LIN] * 3, ink)
    ev.to_mesh_clear()
    return {'positions': pos, 'colors': col, 'triangles': body, 'ink': ink}


def card_mesh(root):
    """card_shape kökü: iki katman (mürekkep arkada, renk önde), kart uzayına."""
    y = (Vector((0, 0, 1)) - vd * vd.z).normalized()
    x = y.cross(vd).normalized()
    pos, col, tri = [], [], []
    for child in sorted(root.children, key=lambda c: c.name):
        me = child.data
        me.calc_loop_triangles()
        attr = me.color_attributes['Col']
        for lt in me.loop_triangles:
            for v, l in zip(lt.vertices, lt.loops):
                q = child.matrix_world @ me.vertices[v].co - root.matrix_world.translation
                tri.append(len(pos) // 3)
                pos.extend(round(c, 5) for c in (-q.dot(x), q.dot(y), q.dot(vd)))
                col.extend(round(c, 5) for c in attr.data[l].color[:3])
    return {'positions': pos, 'colors': col, 'triangles': tri, 'ink': []}


GROUND = Vector((0, 0, Z))
out = {}
# Diş etleri: dış yön Blender +Y (Unity +Z)
out['gumUpper'] = lit_mesh(gum_arc('Dışa gum üst', math.pi / 2, None, 11), GROUND)
out['gumLower'] = lit_mesh(gum_arc('Dışa gum alt', math.pi / 2, None, 23), GROUND)
# Diş tipleri: 0 köpek dişi, 1 normal, 2 küçük; üst çene kalkışı .1, alt çene .75 (eğik kamerada sivri okunsun)
SIZES = (1.35, 1.0, .85)
for jaw_name, lift in (('Upper', .1), ('Lower', .75)):
    for k, size in enumerate(SIZES):
        o, d, t = tooth(f'Dışa diş {jaw_name} {k}', math.pi / 2, None, size, lift, 400 + k)
        out[f'tooth{jaw_name}{k}'] = lit_mesh(o, Vector())
mini, _, _ = tooth('Dışa mini diş', math.pi / 2, None, .7, .55, 300)
out['mini'] = lit_mesh(mini, Vector())
for k, c in enumerate((LEAF, LEAF_DARK, CREAM)):
    out[f'chip{k}'] = lit_mesh(chip(f'Dışa kırıntı {k}', .08, c, k), Vector())
# Diken: yukarı bakan, uzun boy (.38); kısa olanlar Unity'de ölçekle
ln = .38
out['spike'] = card_mesh(card_shape('Dışa diken', [(-.06, -ln * .45), (.06, -ln * .45), (0, ln * .55)], CREAM, .035))
out['puff'] = card_mesh(card_shape('Dışa toz', scallop(.11, 0), DUST, .03))


def placements(seed):
    """build_greed_jaw.py jaw() ile aynı açılar; çene uzayında [tip, x, y, z, yaw, ölçek] × 7."""
    rows = []
    for k in range(7):
        phi = math.radians(-56 + 112 * k / 6 + random.Random(seed + k).uniform(-3, 3))
        kind = 0 if k in (1, 5) else (2 if k == 3 else 1)
        base_r = R - .16 - .1 * SIZES[kind]
        p = Vector((-math.sin(phi) * base_r, math.cos(phi) * base_r, .04))      # Blender çene uzayı (dış = +Y)
        u = to_unity(p, Vector())
        rows.extend([kind, round(u[0], 5), round(u[1], 5), round(u[2], 5), round(-math.degrees(phi), 4),
                     round(random.Random(seed + k + 50).uniform(.92, 1.08), 4)])
    return rows


rng = random.Random(77)
chips = []
for k in range(9):
    a = 2 * math.pi * k / 9 + rng.uniform(-.3, .3)
    size, dist, up = rng.uniform(.06, .1), rng.uniform(.6, 1.2), rng.uniform(.5, .9)
    chips.extend([k % 3, round(math.cos(a), 5), round(math.sin(a), 5), round(dist, 4), round(up, 4), round(size / .08, 4)])

doc = {'meshes': [dict(name=k, **v) for k, v in out.items()],
       'upper': placements(11), 'lower': placements(23), 'chips': chips,
       'designRadius': R, 'miniRadius': R + .2}
(EXPORT / 'greed_jaw.json').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
print('EXPORTED', {k: (len(v['triangles']) // 3, len(v['ink']) // 3) for k, v in out.items()})
