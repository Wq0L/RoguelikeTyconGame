"""İstilacı Filiz kişiliğini Unity için dışa aktarır (25 Ekim 2026).
  blender -b --factory-startup -P Tools/Art/SproutBurst/export_sprout_burst.py
build_sprout_burst.py'nin yardımcıları (yerleşim / animasyon / render öncesi) aynı süreçte çalıştırılır; ortak ağlar kurulur:
  crack0–4 (yerde sarmaşık çatlak kolları, hücre uzayı), tendril{k}_{i} (4 dal × 9 parça, parça başından), vine{i} (sürünen dal,
  +X yönünde 2 birim, 12 parça; 4 ve 8'de yaprakçık), puff0–2 (spor bulutu kartı, boy 1), pollen0–1 (elmas kıvılcım, boy 1),
  splat (özsu kartı), drop (damla, yarıçap 1), shred (kopan yaprak).
Çıktı: Tools/Art/SproutBurst/export/sprout_burst.json + üçgen sayımı.
- Kartlar kart uzayında (x ekranda sağ, y yukarı, z bakış yönü). Işıklı ağlar: gövde + Solidify kabuğu (alt ağ 1).
- Hücre uzayı: Blender (x, y, z − GROUND) → Unity (x, z, y); kök hücrenin toprak üstü = 0.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector, Matrix

HERE = Path(__file__).resolve().parent
source = (HERE / 'build_sprout_burst.py').read_text(encoding='utf-8')
exec(compile(source[:source.index('# ---------------------------------------------------------------- yerleşim')], str(HERE / 'build_sprout_burst.py'), 'exec'))

EXPORT = HERE / 'export'
EXPORT.mkdir(exist_ok=True)
INK_LIN = lin(INK)


def U(p):
    return (round(p.x, 5), round(p.z, 5), round(p.y, 5))


class Pack:
    def __init__(self):
        self.pos, self.col, self.body, self.ink = [], [], [], []

    def add(self, points, colors, into, flip):
        idx = []
        for p, c in zip(points, colors):
            idx.append(len(self.pos) // 3)
            self.pos.extend(p)
            self.col.extend(round(v, 5) for v in c[:3])
        into.extend((idx[0], idx[2], idx[1]) if flip else (idx[0], idx[1], idx[2]))

    def lit(self, o, origin):
        """Dünya uzayında nesne (ebeveyn dahil) → origin'e göre Unity; gövde + kabuk."""
        bpy.context.view_layer.update()
        mw = o.matrix_world
        me = o.data; me.calc_loop_triangles()
        attr = me.color_attributes['Col']
        for lt in me.loop_triangles:
            self.add([U(mw @ me.vertices[v].co - origin) for v in lt.vertices], [attr.data[l].color for l in lt.loops], self.body, True)
        ev = o.evaluated_get(bpy.context.evaluated_depsgraph_get()); em = ev.to_mesh(); em.calc_loop_triangles()
        for lt in em.loop_triangles:
            if lt.material_index == 1:
                self.add([U(mw @ em.vertices[v].co - origin) for v in lt.vertices], [INK_LIN] * 3, self.ink, True)
        ev.to_mesh_clear()

    def flat(self, o, origin):
        bpy.context.view_layer.update()
        mw = o.matrix_world
        me = o.data; me.calc_loop_triangles()
        attr = me.color_attributes['Col']
        for lt in me.loop_triangles:
            self.add([U(mw @ me.vertices[v].co - origin) for v in lt.vertices], [attr.data[l].color for l in lt.loops], self.body, True)

    def card(self, root):
        bpy.context.view_layer.update()
        for child in sorted(root.children, key=lambda c: c.name):
            me = child.data; me.calc_loop_triangles()
            attr = me.color_attributes['Col']
            for lt in me.loop_triangles:
                pts = []
                for v in lt.vertices:
                    q = child.matrix_world @ me.vertices[v].co - root.matrix_world.translation
                    pts.append((round(q.dot(SR), 5), round(q.dot(SUP), 5), round(q.dot(VD), 5)))
                self.add(pts, [attr.data[l].color for l in lt.loops], self.body, False)

    def doc(self, name):
        return {'name': name, 'positions': self.pos, 'colors': self.col, 'triangles': self.body, 'ink': self.ink}


def tendril_points(dir2, length, curl, n=9):   # build_sprout_burst.py ile aynı (yerleşim bölümünde tanımlı)
    up = Vector((0, 0, 1)); pts = [Vector()]
    for i in range(n):
        s_ = (i + .5) / n
        th = s_ * s_ * curl
        pts.append(pts[-1] + (dir2 * math.sin(th) + up * math.cos(th)) * length / n)
    return pts


meshes, tables = [], {}
# Çatlak kolları (yerde; kök hücrenin toprağına göre)
r0 = random.Random(41)
for k in range(5):
    a0 = k * 2 * math.pi / 5 + r0.uniform(-.3, .3)
    pts = []
    for i in range(9):
        t = i / 8
        a = a0 + .55 * math.sin(t * 5 + k)
        pts.append(Vector((math.cos(a), math.sin(a), 0)) * (.42 + .58 * t) + Vector((0, 0, .01)))
    root = empty(f'Dışa çatlak {k}')
    o1 = ribbon(f'Dışa çatlak {k} m', pts, .075, .005, INK, root)
    o2 = ribbon(f'Dışa çatlak {k} t', pts, .042, .008, TEAL, root)
    p = Pack(); p.flat(o1, Vector()); p.flat(o2, Vector()); meshes.append(p.doc(f'crack{k}'))
# Dallar: 4 × 9 parça; her parça kendi başına göre; tablo: [parça başı x, y, z (Unity, hücre uzayı)] ve dal kökü + yönü
starts, roots = [], []
for k in range(4):
    a = k * math.pi / 2 + .5
    d = Vector((math.cos(a), math.sin(a), 0))
    pts = tendril_points(d, r0.uniform(1.25, 1.5), r0.uniform(3.1, 3.6))
    base = d * .12 - Vector((0, 0, .02))
    roots.extend([*U(base), round(d.x, 5), round(d.y, 5)])
    for i in range(len(pts) - 1):
        ra = .1 * (1 - .8 * i / (len(pts) - 1)); rb = .1 * (1 - .8 * (i + 1) / (len(pts) - 1))
        seg = tube_segment(f'Dışa dal {k}.{i}', base + pts[i], base + pts[i + 1], ra, rb, TEAL, TEAL_D)
        p = Pack(); p.lit(seg, base + pts[i]); meshes.append(p.doc(f'tendril{k}_{i}'))
        starts.extend(U(base + pts[i]))
tables['tendrilStarts'] = starts
tables['tendrilRoots'] = roots
# Sürünen dal: kaynaktan +X yönünde 2 birim (Unity'de hedef yönüne döndürülür, hücre boyuyla ölçeklenir)
out, side = Vector((1, 0, 0)), Vector((0, 1, 0))
vpts = [out * (.25 + 1.75 * (i / 12)) + side * (.18 * math.sin(i / 12 * math.pi * 2.2)) + Vector((0, 0, .02 + .1 * math.sin(math.pi * i / 12))) for i in range(13)]
vstarts = []
for i in range(12):
    seg = tube_segment(f'Dışa sürünen {i}', vpts[i], vpts[i + 1], .08 * (1 - .4 * i / 12), .08 * (1 - .4 * (i + 1) / 12), TEAL, TEAL_D)
    p = Pack(); p.lit(seg, vpts[i])
    if i in (4, 8):
        h = empty(f'Dışa dal yaprağı {i}', vpts[i]); h.rotation_euler = (math.radians(-20), 0, math.atan2(side.y, side.x) * (1 if i == 8 else -1) - math.pi / 2)
        leaf = leaf_mesh(f'Dışa dal yaprak {i}', .45, .15, .05, LEAF_L, TEAL_D, parent=h)
        p.lit(leaf, vpts[i])
    meshes.append(p.doc(f'vine{i}'))
    vstarts.extend(U(vpts[i]))
tables['vineStarts'] = vstarts
# Kartlar
for k in range(3):
    p = Pack(); p.card(spore_puff(f'Dışa bulut {k}', 60 + k, 1.0)); meshes.append(p.doc(f'puff{k}'))
for k, c in enumerate((CREAM, PINK_L)):
    p = Pack(); p.card(dot(f'Dışa polen {k}', 1.0, c)); meshes.append(p.doc(f'pollen{k}'))
p = Pack(); p.card(card('Dışa özsu', splat_outline(5), [(1.15, INK, 0.0), (1.0, SAP, .02), (.55, SAP_L, .04)])); meshes.append(p.doc('splat'))
# Damla ve kopan yaprak (ışıklı, merkezli)
bmd = bmesh.new(); bmesh.ops.create_icosphere(bmd, subdivisions=1, radius=1.0)
drop = obj_from_bm('Dışa damla', bmd, LIT, ink=.2); paint(drop, lambda p: SAP if p.normal.z > 0 else TEAL_D)
p = Pack(); p.lit(drop, Vector()); meshes.append(p.doc('drop'))
shred = leaf_mesh('Dışa parça', .22, .08, .03, LEAF_L, TEAL_D)
p = Pack(); p.lit(shred, Vector()); meshes.append(p.doc('shred'))

counts = {m['name']: (len(m['triangles']) // 3, len(m['ink']) // 3) for m in meshes}
doc = {'meshes': meshes, **tables}
(EXPORT / 'sprout_burst.json').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
total = sum(b + i for b, i in counts.values())
print('EXPORTED', len(meshes), 'meshes,', total, 'triangles;', {k: v for k, v in counts.items() if not k.startswith('tendril') and not k.startswith('vine')},
      'tendril seg', counts['tendril0_0'], 'vine seg', counts['vine0'], counts['vine4'])
