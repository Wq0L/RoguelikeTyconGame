"""Aşılı Kabuk kişiliğini Unity için dışa aktarır (25 Ekim 2026).
  blender -b --factory-startup -P Tools/Art/ShellBurst/export_shell_burst.py
build_shell_burst.py'nin nesne kurulumu (zamanlama / animasyon / render öncesi) aynı süreçte çalıştırılır; ortak ağlar:
  node{z}_body (5 lob plaka + topuz, düğüm uzayı), node{z}_core, node{z}_motif, node{z}_crack0–2 (vuruş aşamaları),
  node{z}_plate0–4 (açılış için menteşe uzayında; plate0 motifi taşır), node{z}_knob (topuz kendi kökünde),
  birth{z}_glow, birthCenter, birthSlab0–5 (doğum plakaları, menteşe uzayında), vein{z} (birim damar parçası, +Z boyunca 1),
  clamp{z} (kelepçe, +X'e bakan, kendi merkezinde), bead{z}, collarPlate0–5 (menteşe uzayında), collar{z}_crack{k},
  collar{z}_band, crust0–4, chip, shock (yerde mürekkep halka), kartlar: dust0–2, tink{z}, spark{z}, sparkCream.
  z: 0 = A (amber), 1 = B (mor).
Çıktı: Tools/Art/ShellBurst/export/shell_burst.json + üçgen sayımı.
- Kartlar kart uzayında (x ekranda sağ, y yukarı, z bakış yönü). Işıklı ağlar: gövde + Solidify kabuğu (alt ağ 1).
- Hücre uzayı: Blender (x, y, z) → Unity (x, z, y); hücre yarı boyu 1 (oyunda hücre 2 birim: ölçek = hücre / 2).
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
source = (HERE / 'build_shell_burst.py').read_text(encoding='utf-8')
exec(compile(source[:source.index('# ---------------------------------------------------------------- zamanlamalar')], str(HERE / 'build_shell_burst.py'), 'exec'))

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


meshes, tables = [], {k: [] for k in ('plateHinges', 'plateDirections', 'knobRest', 'nodeSize', 'slabHinges', 'slabDirections', 'clampCenter',
                                      'collarHinges', 'collarDirections', 'collarCracked', 'crustCenters')}


def add(name, pack):
    meshes.append(pack.doc(name))


bpy.context.view_layer.update()
for zi, z in enumerate('AB'):
    nd = NODES[z]
    origin = nd.root.matrix_world.translation.copy()
    tables['nodeSize'].extend((round(nd.rx, 5), round(nd.rz, 5), round(nd.cz, 5)))
    motifs = sorted([o for o in nd.plates[0][0].children if 'motif' in o.name], key=lambda o: o.name)
    p = Pack()
    for o, _, _, _ in nd.plates: p.lit(o, origin)
    p.lit(nd.knob, origin)
    add(f'node{zi}_body', p)
    p = Pack(); p.lit(nd.core, origin); add(f'node{zi}_core', p)
    p = Pack()
    for o in motifs: p.flat(o, origin)
    add(f'node{zi}_motif', p)
    for stage in (1, 2, 3):
        p = Pack()
        for st, a, b in nd.cracks:
            if st == stage: p.flat(a, origin); p.flat(b, origin)
        add(f'node{zi}_crack{stage - 1}', p)
    for k, (o, hinge, d, c) in enumerate(nd.plates):
        hw = o.matrix_world.translation.copy()
        p = Pack(); p.lit(o, hw)
        if k == 0:
            for m in motifs: p.flat(m, hw)
        add(f'node{zi}_plate{k}', p)
        tables['plateHinges'].extend(U(hinge)); tables['plateDirections'].extend(U(d))
    p = Pack(); p.lit(nd.knob, nd.knob.matrix_world.translation.copy()); add(f'node{zi}_knob', p)
    tables['knobRest'].extend(U(nd.knob_rest))

# Doğum plakaları (A'nın şekilleri iki bölgede de; ışık diski bölge renginde)
b = BIRTH['A']
for zi, z in enumerate('AB'):
    p = Pack(); p.flat(BIRTH[z].glow, BIRTH[z].c0); add(f'birth{zi}_glow', p)
p = Pack(); p.lit(b.center, b.c0); add('birthCenter', p)
for k, (o, at, d) in enumerate(b.slabs):
    p = Pack(); p.lit(o, at); add(f'birthSlab{k}', p)
    tables['slabHinges'].extend(U(at - b.c0)); tables['slabDirections'].extend(U(d))

# Damar parçası (+Y boyunca 1 → Unity +Z), kelepçe (+X'e bakan, kendi merkezinde), boncuk
for zi, z in enumerate('AB'):
    seg = tube_segment(f'Dışa damar {z}', Vector((0, 0, 0)), Vector((0, 1, 0)), .075, .062, BARK, BARK_D)
    def strip(bm, z=z):
        sd = Vector((.026, 0, 0))
        q0 = Vector((0, 0, .072)); q1 = Vector((0, 1, .062))
        bm.faces.new((bm.verts.new(q0 - sd), bm.verts.new(q0 + sd), bm.verts.new(q1 + sd), bm.verts.new(q1 - sd)))
        return [ZC[z]['main']]
    glow = faces_obj(f'Dışa damar ışığı {z}', strip, UNLIT)
    p = Pack(); p.lit(seg, Vector()); p.flat(glow, Vector()); add(f'vein{zi}', p)

    center = Vector((.8, 0, 0))
    arc = [Vector((math.cos(u) * .8, math.sin(u) * .8, 0)) for u in [math.radians(-55 + 110 * k / 8) for k in range(9)]]
    clamp = sweep(f'Dışa kelepçe {z}', [q - center for q in arc], .08, BARK_L, BARK_D)
    def cglow(bm, z=z):
        pts = [q - center + Vector((0, 0, .078)) for q in arc]
        L_, R_ = [], []
        for i, q in enumerate(pts):
            t = (pts[min(i + 1, 8)] - pts[max(i - 1, 0)]).normalized(); sd = Vector((-t.y, t.x, 0)) * .03
            L_.append(bm.verts.new(q + sd)); R_.append(bm.verts.new(q - sd))
        for i in range(8): bm.faces.new((L_[i], R_[i], R_[i + 1], L_[i + 1]))
        return [ZC[z]['main']] * 8
    cg = faces_obj(f'Dışa kelepçe ışığı {z}', cglow, UNLIT)
    p = Pack(); p.lit(clamp, Vector()); p.flat(cg, Vector()); add(f'clamp{zi}', p)

    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.11)
    bead = obj_from_bm(f'Dışa boncuk {z}', bm, UNLIT, ink=.02); paint(bead, lambda q, z=z: ZC[z]['light'])
    p = Pack(); p.lit(bead, Vector()); add(f'bead{zi}', p)
tables['clampCenter'].extend(U(Vector((.8, 0, 0))))

# Kabuk yaka (kapanan hücre): plakalar bölgeden bağımsız; çatlak ışığı ve iç bant bölge renginde
collars = {z: Collar((0, 0), z) for z in 'AB'}
ca = collars['A']
bpy.context.view_layer.update()
root_w = ca.root.matrix_world.translation.copy()
for k, (o, d, crack) in enumerate(ca.plates):
    hw = o.matrix_world.translation.copy()
    p = Pack(); p.lit(o, hw); add(f'collarPlate{k}', p)
    tables['collarHinges'].extend(U(hw - root_w)); tables['collarDirections'].extend(U(d))
    tables['collarCracked'].append(1 if crack else 0)
for zi, z in enumerate('AB'):
    c = collars[z]
    for k, (o, d, crack) in enumerate(c.plates):
        if not crack: continue
        hw = o.matrix_world.translation.copy()
        p = Pack(); p.flat(crack[0], hw); p.flat(crack[1], hw); add(f'collar{zi}_crack{k}', p)
    p = Pack(); p.flat(c.band, c.root.matrix_world.translation.copy()); add(f'collar{zi}_band', p)
for k, (o, cen) in enumerate(ca.crust):
    hw = o.matrix_world.translation.copy()
    p = Pack(); p.lit(o, hw); add(f'crust{k}', p)
    tables['crustCenters'].extend(U(hw - root_w))

# Parçalar ve kartlar
p = Pack(); p.lit(chip('Dışa kıymık', .12, 70), Vector()); add('chip', p)
p = Pack(); p.flat(ring_mesh('Dışa şok', .92, 1.0, INK), Vector()); add('shock', p)
for k in range(3):
    p = Pack(); p.card(dust(f'Dışa toz {k}', 120 + k, 1.0)); add(f'dust{k}', p)
for zi, z in enumerate('AB'):
    p = Pack(); p.card(tink(f'Dışa tink {z}', z)); add(f'tink{zi}', p)
    p = Pack(); p.card(spark(f'Dışa kıvılcım {z}', 1.0, ZC[z]['light'])); add(f'spark{zi}', p)
p = Pack(); p.card(spark('Dışa krem kıvılcım', 1.0, CREAM)); add('sparkCream', p)

counts = {m['name']: (len(m['triangles']) // 3, len(m['ink']) // 3) for m in meshes}
doc = {'meshes': meshes, **tables}
(EXPORT / 'shell_burst.json').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
total = sum(b_ + i_ for b_, i_ in counts.values())
print('EXPORTED', len(meshes), 'meshes,', total, 'triangles')
for k, v in counts.items(): print('  ', k, v)
