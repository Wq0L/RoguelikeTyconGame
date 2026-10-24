"""Kuzey ışığı meteorunu Unity için dışa aktarır (23 Ekim 2026).
  blender -b --factory-startup -P Tools/Art/AuroraMeteor/export_aurora_meteor.py
Önce build_aurora_meteor.py sahneyi kurar (aynı süreçte exec), sonra parçalar düz veri olarak yazılır:
  Tools/Art/AuroraMeteor/export/aurora_meteor.json
Biçim: {mesh adı: {positions: [x,y,z,...], colors: [r,g,b,...] (doğrusal), triangles: [...]}} — Unity koordinatları (y yukarı).
- Taş, çatlak, taş parçaları: kendi merkezlerinde (yerel); Blender (x, y, z) → Unity (x, z, y), üçgen sırası çevrilir.
- Kartlar (iz, taç, bulut): kart uzayında. y = ekrandaki yön (iz için geri, taç/bulut için yukarı), z = kamera bakış yönü,
  x = ekranda sağ (Blender render'ı ile aynı görünüm). Unity bu tabanı her karede kendi kamerasından kurar.
- Krater zemini: yere yatık (Unity XZ düzlemi), merkez (0, 0, 0), zemin yüksekliği çıkarılmış.
Her üçgen kendi köşelerini taşır (düz gölgeleme).
"""
import bpy, bmesh, json, math, sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
exec(compile((HERE / 'build_aurora_meteor.py').read_text(encoding='utf-8'), str(HERE / 'build_aurora_meteor.py'), 'exec'))

EXPORT = HERE / 'export'
EXPORT.mkdir(exist_ok=True)
depsgraph = bpy.context.evaluated_depsgraph_get()


def triangles_world(objs, use_world=True):
    """(dünya konumu, doğrusal renk) üçgen listesi; modifier'sız taban ağı (mürekkep kabuğu Unity'de ayrı malzemedir)."""
    tris = []
    for o in objs:
        me = o.data
        bm = bmesh.new(); bm.from_mesh(me)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        layer = bm.loops.layers.float_color.get('Col')
        mw = o.matrix_world if use_world else None
        for f in bm.faces:
            tri = []
            for loop in f.loops:
                p = loop.vert.co.copy()
                if mw is not None: p = mw @ p
                c = loop[layer] if layer is not None else (1, 1, 1, 1)
                tri.append((p, (c[0], c[1], c[2])))
            tris.append(tri)
        bm.free()
    return tris


def pack(tris, to_unity):
    pos, col, idx = [], [], []
    for tri in tris:
        mapped = [(to_unity(p), c) for p, c in tri]
        for p, c in mapped:
            idx.append(len(pos) // 3)
            pos.extend(round(v, 5) for v in p)
            col.extend(round(v, 5) for v in c)
    return {'positions': pos, 'colors': col, 'triangles': idx}


def local_unity(origin):
    """Yerel: Blender (x, y, z) → Unity (x, z, y); el yönü değiştiği için üçgen sırası ters çevrilir (pack sonrası)."""
    def f(p):
        q = p - origin
        return (q.x, q.z, q.y)
    return f


def flip(mesh):
    t = mesh['triangles']
    for i in range(0, len(t), 3): t[i + 1], t[i + 2] = t[i + 2], t[i + 1]
    return mesh


def card_space(origin, y_dir):
    vd = view_dir()
    y = (y_dir - vd * y_dir.dot(vd)).normalized()
    x = y.cross(vd).normalized()
    def f(p):
        q = p - origin
        return (-q.dot(x), q.dot(y), q.dot(vd))
    return f


def objs(prefix):
    return [o for o in bpy.data.objects if o.name.startswith(prefix) and o.type == 'MESH']


out = {}
# Taş ve çatlak (taşın yerel uzayı)
rock_obj = bpy.data.objects['Meteor A']
out['rock'] = flip(pack(triangles_world([rock_obj], use_world=False), local_unity(Vector())))
out['rockCracks'] = flip(pack(triangles_world([bpy.data.objects['Meteor A cracks']], use_world=False), local_unity(Vector())))
# İz kartı: başlangıç taşın merkezi, y = ekranda geri yön
back = -FALL
out['trail'] = pack(triangles_world(objs('Meteor A trail ')), card_space(rock_obj.location.copy(), back))
# Taç kartı: çarpma noktasının biraz üstü, y = ekranda yukarı
out['crown'] = pack(triangles_world([o for o in objs('Taç ') if not o.name.startswith('Taç sonrası')]),
                    card_space(HIT + Vector((0, 0, .3)), Vector((0, 0, 1))))
# Bulutlar: her biri kendi merkezinde (mürekkep katmanının ilk köşesi = merkez)
for k in range(2):
    ink_layer = bpy.data.objects[f'Toz {k}.0']
    center = ink_layer.data.vertices[0].co.copy()
    out[f'cloud{k}'] = pack(triangles_world([ink_layer, bpy.data.objects[f'Toz {k}.1']]), card_space(center, Vector((0, 0, 1))))
# Taş parçaları (enkaz) ve krater kenar blokları: yerel, merkezli
for k in range(3):
    out[f'stone{k}'] = flip(pack(triangles_world([bpy.data.objects[f'Parça {k}']], use_world=False), local_unity(Vector())))
    out[f'soil{k}'] = flip(pack(triangles_world([bpy.data.objects[f'Krater 1 rim {k}']], use_world=False), local_unity(Vector())))
# Krater zemini: yanık leke + çatlaklar + kristaller (dünya → krater merkezi, zemin yüksekliği .23 çıkarılır)
ground_parts = [bpy.data.objects['Krater 1 scorch'], bpy.data.objects['Krater 1 cracks']] + objs('Krater 1 crystal ')
out['craterGround'] = flip(pack(triangles_world(ground_parts), local_unity(HIT + Vector((0, 0, .23)))))
# Krater kenar blokları: her biri kendi dönüşü uygulanmış, kendi merkezinde ayrı ağ (mürekkep kabuğu merkezden çalışır) + konum.
rims = []
crater_origin = HIT + Vector((0, 0, .23))
for k in range(9):
    o = bpy.data.objects[f'Krater 1 rim {k}']
    out[f'rim{k}'] = flip(pack(triangles_world([o]), local_unity(o.location.copy())))
    q = o.location - crater_origin
    rims.append([round(q.x, 4), round(q.z, 4), round(q.y, 4)])
out['_craterRims'] = rims
out['_meta'] = {'cell': 2.0, 'rockRadius': .6, 'note': 'Blender build_aurora_meteor.py / export_aurora_meteor.py, 23 Ekim 2026'}

# Unity JsonUtility biçimi: {meshes: [{name, positions, colors, triangles}], rims: [x,y,z,...], cell, rockRadius}
doc = {'meshes': [dict(name=k, **v) for k, v in out.items() if not k.startswith('_')],
       'rims': [c for p in out['_craterRims'] for c in p], 'cell': 2.0, 'rockRadius': .6}
(EXPORT / 'aurora_meteor.json').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
print('EXPORTED', {k: (len(v['triangles']) // 3 if isinstance(v, dict) and 'triangles' in v else len(v)) for k, v in out.items() if not k.startswith('_meta')})
