"""Diken çıkış patlamasını Unity için dışa aktarır (24 Ekim 2026).
  blender -b --factory-startup -P Tools/Art/ThornBurst/export_thorn_burst.py
build_thorn_burst.py'nin yardımcıları (nesneler / animasyon / render öncesi) aynı süreçte çalıştırılır; ortak ağlar kurulur:
  blast (5 katmanlı toprak patlaması kartı), lines (6 hız çizgisi tek kart), cloud0–2 (toz bulutu kartları, boy 1),
  clod0–2 (ışıklı kesek + mürekkep kabuğu), craterLit (6 kırık toprak keseği tek ağ + kabuk), craterScorch (yanık leke, ışıksız).
Çıktı: Tools/Art/ThornBurst/export/thorn_burst.json + üçgen sayımı (konsola ve json'a).
- Kartlar kart uzayında (x ekranda sağ, y yukarı, z bakış yönü; eksi z kameraya yakın); Unity her karede kameraya çevirir.
- Işıklı ağlar Blender (x, y, z) → Unity (x, z, y), üçgen sırası çevrilir; gövde = taban ağı, kabuk = Solidify'ın malzeme 1 yüzleri.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector, Matrix

HERE = Path(__file__).resolve().parent
source = (HERE / 'build_thorn_burst.py').read_text(encoding='utf-8')
marker = '# ---------------------------------------------------------------- nesneler'
exec(compile(source[:source.index(marker)], str(HERE / 'build_thorn_burst.py'), 'exec'))

EXPORT = HERE / 'export'
EXPORT.mkdir(exist_ok=True)
INK_LIN = lin(INK)


def to_unity(p):
    return (p.x, p.z, p.y)


class Pack:
    def __init__(self):
        self.pos, self.col, self.body, self.ink = [], [], [], []

    def add(self, points, colors, into, flip):
        idx = []
        for p, c in zip(points, colors):
            idx.append(len(self.pos) // 3)
            self.pos.extend(round(v, 5) for v in p)
            self.col.extend(round(v, 5) for v in c[:3])
        into.extend((idx[0], idx[2], idx[1]) if flip else (idx[0], idx[1], idx[2]))

    def lit(self, o, matrix):
        """Gövde (taban ağı, köşe rengi) + kabuk (değerlendirilmiş ağın malzeme 1 yüzleri); matrix: nesne → paket uzayı (Blender)."""
        me = o.data; me.calc_loop_triangles()
        attr = me.color_attributes['Col']
        for lt in me.loop_triangles:
            self.add([to_unity(matrix @ me.vertices[v].co) for v in lt.vertices], [attr.data[l].color for l in lt.loops], self.body, True)
        ev = o.evaluated_get(bpy.context.evaluated_depsgraph_get()); em = ev.to_mesh(); em.calc_loop_triangles()
        for lt in em.loop_triangles:
            if lt.material_index == 1:
                self.add([to_unity(matrix @ em.vertices[v].co) for v in lt.vertices], [INK_LIN] * 3, self.ink, True)
        ev.to_mesh_clear()

    def card(self, root):
        """Kart kökü altındaki katmanlar, kart uzayına (sıra = katman sırası: mürekkep önce)."""
        for child in sorted(root.children, key=lambda c: c.name):
            me = child.data; me.calc_loop_triangles()
            attr = me.color_attributes['Col']
            for lt in me.loop_triangles:
                pts = []
                for v in lt.vertices:
                    q = child.matrix_world @ me.vertices[v].co - root.matrix_world.translation
                    pts.append((q.dot(SR), q.dot(SUP), q.dot(VD)))
                self.add(pts, [attr.data[l].color for l in lt.loops], self.body, False)

    def flat(self, o, matrix):
        """Işıksız düz ağ (krater lekesi), Unity koordinatlarına."""
        me = o.data; me.calc_loop_triangles()
        attr = me.color_attributes['Col']
        for lt in me.loop_triangles:
            self.add([to_unity(matrix @ me.vertices[v].co) for v in lt.vertices], [attr.data[l].color for l in lt.loops], self.body, True)

    def doc(self, name):
        return {'name': name, 'positions': self.pos, 'colors': self.col, 'triangles': self.body, 'ink': self.ink}


meshes = []
# Kartlar (kök orijinde)
p = Pack(); p.card(blast('Dışa patlama', Vector())); meshes.append(p.doc('blast'))
p = Pack()
for k, a in enumerate((-100, -55, 55, 100, 150, -150)): p.card(speed_line(f'Dışa çizgi {k}', a, Vector()))
meshes.append(p.doc('lines'))
for k in range(3):
    p = Pack(); p.card(cloud(f'Dışa bulut {k}', 90 + k, 1.0)); meshes.append(p.doc(f'cloud{k}'))
# Kesekler (yerel, merkezli)
for k, cols in enumerate(([SOIL_A, SOIL_B, SOIL_D], [SOIL_B, SOIL_D, SOIL_A], [SOIL_A, SOIL_B, SOIL_D, LEAF])):
    o = clod(f'Dışa kesek {k}', .12, 300 + k, cols)
    p = Pack(); p.lit(o, Matrix.Identity(4)); meshes.append(p.doc(f'clod{k}'))
# Krater: kök orijinde; kesekler kendi dönüş/konumlarıyla tek ağ, leke ayrı
crater = scorch(); crater.location = Vector(); bpy.context.view_layer.update()
lit_pack, flat_pack = Pack(), Pack()
for child in crater.children:
    rel = crater.matrix_world.inverted() @ child.matrix_world
    if child.name.startswith('Krater kesek'): lit_pack.lit(child, rel)
    else: flat_pack.flat(child, rel)
meshes.append(lit_pack.doc('craterLit')); meshes.append(flat_pack.doc('craterScorch'))

counts = {m['name']: (len(m['triangles']) // 3, len(m['ink']) // 3, len(m['positions']) // 3) for m in meshes}
# Uçuş tabloları (build_thorn_burst.py ile aynı tohumlar): oyunda Random yok, sabit tablo.
import random as _random
r0 = _random.Random(77)
clods = []
for k in range(9):
    a = k * 2 * math.pi / 9 + r0.uniform(-.3, .3)
    size, dist, up, spin = r0.uniform(.09, .15), r0.uniform(.9, 1.6), r0.uniform(1.2, 2.0), r0.uniform(-4, 4)
    # [varyant, yön x, yön z (Unity), uzaklık, yükselme, dönüş, boy / .12]
    clods.extend([2 if k % 4 == 0 else k % 2, round(math.cos(a), 5), round(math.sin(a), 5), round(dist, 4), round(up, 4), round(spin, 4), round(size / .12, 4)])
r1 = _random.Random(33)
clouds = []
for k in range(7):
    a = k * 2 * math.pi / 7 + r1.uniform(-.25, .25)
    size, reach, rise, delay = r1.uniform(.32, .46), r1.uniform(.45, .85), r1.uniform(.25, .65), r1.uniform(0, 2)
    # [varyant, ekranda sağ, ekranda yukarı (×.35), uzaklık, yükselme, gecikme, boy]
    clouds.extend([k % 3, round(math.cos(a), 5), round(.35 * math.sin(a), 5), round(reach, 4), round(rise, 4), round(delay, 4), round(size, 4)])
doc = {'meshes': meshes, 'clods': clods, 'clouds': clouds,
       'counts': [{'name': k, 'body': v[0], 'ink': v[1], 'vertices': v[2]} for k, v in counts.items()]}
(EXPORT / 'thorn_burst.json').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
print('EXPORTED', counts)
