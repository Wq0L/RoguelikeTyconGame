"""Saksı (Saksı 1x1) "elle boyanmış" doku denemesi (11 Kasım 2026; kullanıcı: "saksı ve toprakta böyle boyarsak sorun olmaz;
boyama ile hafif geçiş olabilir, ürün netleşti"). Yalnız prototip: çıktı izole kopyaya gider, oyuna / ana projeye girmez.
AI yok. Karonunkiyle aynı dil: boyalı ışık bantları, kırık kenar parlaması, mürekkep, iri fırça.

Saksının özgün UV'si renk paleti atlası (8 renk karesi + toprak bölgesi): boyamaya uygun değil. Bu yüzden:
  1) Blender her yüzün parçasını (palet karesi / toprak) özgün UV'den okur,
  2) yeni UV açar (Smart UV Project; Blender'da elle boyamaya da uygun),
  3) yeni UV'ye pişirir: özgün renk, parça, nesne normali, konum, kenar (Bevel), ortam gölgesi (AO),
  4) numpy ile boyar (ışık YUKARIDAN, yönden bağımsız: saksı oyunda döndürülse de doğru),
  5) yeni UV'li modeli FBX olarak yazar (özgün FBX değişmez).
  blender -b --factory-startup -P Tools/Art/HadesPrototype/paint_planter.py [-- <çıktı klasörü> [<boyut: 1x1|1x2|1x3|2x2|2x3>]]
Çıktı: Saksi_<boyut>_Boyali.fbx, T_Saksi_<boyut>_Boyali.png (1x1: 1024², büyükler: 2048²).
"""
import bpy, sys, os, zlib, struct, math
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TEX = os.path.join(ROOT, "Assets", "3D Assets", "AI", "Models", "M_AIs", "CAN_02_BaseColor.png")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(ROOT, "Library", "VerificationProject", "Assets", "HadesPrototype")
SIZE = argv[1] if len(argv) > 1 else "1x1"
FBX = os.path.join(ROOT, "Assets", "3D Assets", "AI", "Models", f"Saksı {SIZE}.fbx")
DIAG = os.path.join(OUT, "..", "..", "Logs", "HadesPrototype")
os.makedirs(OUT, exist_ok=True); os.makedirs(DIAG, exist_ok=True)
RES = 1024 if SIZE == "1x1" else 2048
SOIL, LABEL = 8, 9

# ---------------------------------------------------------------- model, parçalar, yeni UV
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
me = obj.data
old_uv = me.uv_layers[0]
old_name = old_uv.name
part = np.zeros(len(me.polygons), dtype=np.float32)
for p in me.polygons:
    u = sum(old_uv.data[l].uv.x for l in p.loop_indices) / p.loop_total
    v = sum(old_uv.data[l].uv.y for l in p.loop_indices) / p.loop_total
    part[p.index] = SOIL if v <= 0.75 else (LABEL if v <= 0.86 else min(7, int(u * 8.25)))
attr = me.attributes.new("part", 'FLOAT', 'FACE'); attr.data.foreach_set('value', part / 16.0)
new_uv = me.uv_layers.new(name="Boyama")
me.uv_layers.active = new_uv
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.012, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pişirme
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 16
world = bpy.data.worlds.new("W"); scene.world = world; world.light_settings.distance = 0.12
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
bb_min = np.array([min(xs), min(ys), min(zs)]); bb_size = np.array([max(xs), max(ys), max(zs)]) - bb_min

mat = bpy.data.materials.new("bake"); mat.use_nodes = True
nt = mat.node_tree; nt.nodes.clear()
out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
nt.links.new(em.outputs[0], out.inputs['Surface'])
src = nt.nodes.new('ShaderNodeTexImage'); src.image = bpy.data.images.load(TEX); src.interpolation = 'Closest'
uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = old_name; nt.links.new(uvn.outputs[0], src.inputs['Vector'])
at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = "part"; at.attribute_type = 'GEOMETRY'
tc = nt.nodes.new('ShaderNodeTexCoord')
mp = nt.nodes.new('ShaderNodeMapping'); mp.vector_type = 'POINT'
mp.inputs['Location'].default_value = tuple(-bb_min / bb_size); mp.inputs['Scale'].default_value = tuple(1 / bb_size)
nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
bev = nt.nodes.new('ShaderNodeBevel'); bev.samples = 8; bev.inputs['Radius'].default_value = 0.018
geo = nt.nodes.new('ShaderNodeNewGeometry')
dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
nt.links.new(bev.outputs[0], dot.inputs[0]); nt.links.new(geo.outputs['Normal'], dot.inputs[1])
inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
nt.links.new(dot.outputs['Value'], inv.inputs[1])
target = nt.nodes.new('ShaderNodeTexImage'); nt.nodes.active = target
for slot in obj.material_slots: slot.material = mat

def bake(kind, output=None, colorspace='Non-Color', samples=16, **kw):
    img = bpy.data.images.new(f"b_{kind}_{len(bpy.data.images)}", RES, RES, alpha=True, float_buffer=True)
    img.generated_color = (0, 0, 0, 0); img.colorspace_settings.name = colorspace
    target.image = img
    if output is not None: nt.links.new(output, em.inputs['Color'])
    scene.cycles.samples = samples
    bpy.ops.object.bake(type=kind, use_clear=True, margin=6, **kw)
    a = np.empty(RES * RES * 4, dtype=np.float32); img.pixels.foreach_get(a)
    return a.reshape(RES, RES, 4)[::-1]

base_px = bake('EMIT', src.outputs['Color'], samples=1)
part_px = bake('EMIT', at.outputs['Fac'], samples=1)
pos_px = bake('EMIT', mp.outputs['Vector'], samples=1)
edge_px = bake('EMIT', inv.outputs['Value'], samples=16)
nrm_px = bake('NORMAL', samples=1, normal_space='OBJECT')
ao_px = bake('AO', samples=48)

cov = base_px[..., 3] > 0.5
lin = np.clip(base_px[..., :3], 0, 1)
base = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)   # doğrusal -> sRGB
pid = np.rint(part_px[..., 0] * 16).astype(int)
pos = pos_px[..., :3]
edge = edge_px[..., 0]
n = nrm_px[..., :3] * 2 - 1
ny = n[..., 1]                                             # nesne uzayında y = yukarı (yerel)
ao = np.clip(ao_px[..., 0], 0, 1)
height = pos[..., 1]

# ---------------------------------------------------------------- yardımcılar
rng = np.random.default_rng(2611)
PERM = rng.permutation(4096)

def vnoise(p, freq, seed=0):
    # 3B değer gürültüsü (konuma bağlı: UV dikişinde kopmaz)
    q = p * freq + seed * 17.31
    i = np.floor(q).astype(np.int64); f = q - i; f = f * f * (3 - 2 * f)
    def h(dx, dy, dz):
        k = PERM[(PERM[(PERM[(i[..., 0] + dx) % 4096] + i[..., 1] + dy) % 4096] + i[..., 2] + dz) % 4096]
        return k / 4095.0
    x0 = h(0, 0, 0) * (1 - f[..., 0]) + h(1, 0, 0) * f[..., 0]
    x1 = h(0, 1, 0) * (1 - f[..., 0]) + h(1, 1, 0) * f[..., 0]
    x2 = h(0, 0, 1) * (1 - f[..., 0]) + h(1, 0, 1) * f[..., 0]
    x3 = h(0, 1, 1) * (1 - f[..., 0]) + h(1, 1, 1) * f[..., 0]
    y0 = x0 * (1 - f[..., 1]) + x1 * f[..., 1]; y1 = x2 * (1 - f[..., 1]) + x3 * f[..., 1]
    return y0 * (1 - f[..., 2]) + y1 * f[..., 2]

def fbm(p, freq, octaves=3, seed=0):
    s, a, t = 0, 1.0, 0
    for o in range(octaves):
        s += vnoise(p, freq * (2 ** o), seed + o) * a; t += a; a *= 0.5
    return s / t

def shifted(mask, dx, dy):
    out = np.zeros_like(mask); H, W = mask.shape
    out[max(0, -dy):min(H, H - dy), max(0, -dx):min(W, W - dx)] = mask[max(0, dy):min(H, H + dy), max(0, dx):min(W, W + dx)]
    return out

def mix(c, color, mask, amount=1.0):
    t = (np.asarray(mask, float) * amount)[..., None]
    return c * (1 - t) + np.asarray(color) * t

world_pos = pos * bb_size                                   # gerçek birim (saksı ~2 birim)
warp = (fbm(world_pos, 3.0, 2, 1) - 0.5) * 0.35
chip = fbm(world_pos, 22.0, 2, 2)
stroke = np.floor(fbm(world_pos * np.array([1.0, 0.25, 1.0]), 9.0, 2, 3) * 3) / 2 - 0.5   # dikey uzun darbeler

# ---------------------------------------------------------------- gövde boyaması (soil dışı)
solid = cov & (pid != SOIL)
# boyalı ışık: yukarıdan. Üst yüzler açık; yan yüzlerde yükseklikle kademeli; alta bakan koyu.
light = np.where(ny > 0.55, 1.0, np.where(ny < -0.3, -1.0, (height - 0.62) * 2.4)) + warp   # gövdenin alt yarısı gölge bandında
band = np.digitize(light, [-0.35, 0.35])                     # 0 gölge, 1 orta, 2 ışık
LIT_TINT, SHADOW_TINT = np.array([1.0, 0.96, 0.86]), np.array([0.78, 0.74, 1.0])
c = base.copy()
c = np.where((band == 2)[..., None], c * 1.10 * 0.88 + LIT_TINT * 0.12, c)
c = np.where((band == 0)[..., None], c * 0.66 * SHADOW_TINT, c)
c = c * (1 + (stroke * 0.06 * solid)[..., None])
# ortam gölgesi: tek koyu bant
c = mix(c, c * 0.62 * SHADOW_TINT, solid & (ao < 0.55), 1.0)
# kenarlar: dışbükey (AO yüksek) parlama, içbükey (AO düşük) mürekkep
convex = solid & (edge > 0.08) & (ao > 0.75) & (ny > -0.2)
concave = solid & (edge > 0.12) & (ao < 0.7)
INK = np.array([0.08, 0.05, 0.10])
c = mix(c, c * 0.3 + 0.7 * np.array([1.0, 0.99, 0.94]), convex & (chip > 0.3), 1.0)
c = mix(c, INK, concave, 0.85)
# parça (renk) sınırlarında mürekkep: UV adasının içinde komşu parça farklıysa
diff = np.zeros_like(cov)
for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2)):
    nb_p = np.roll(np.roll(pid, dy, 0), dx, 1); nb_c = np.roll(np.roll(cov, dy, 0), dx, 1)
    diff |= cov & nb_c & (nb_p != pid)
c = mix(c, INK, diff & (pid != SOIL), 0.9)

# ---------------------------------------------------------------- toprak
soil = cov & (pid == SOIL)
clump = fbm(world_pos, 14.0, 3, 7) + (fbm(world_pos, 40.0, 2, 8) - 0.5) * 0.25
tone = np.digitize(clump, [0.42, 0.6])
SOIL_COLS = np.array([[0.20, 0.13, 0.10], [0.29, 0.19, 0.13], [0.40, 0.28, 0.19]])
sc = SOIL_COLS[tone]
pebble = fbm(world_pos, 30.0, 2, 9) > 0.76
pebble_hi = pebble & (fbm(world_pos + np.array([0.0, 0.0, 0.012]), 30.0, 2, 9) <= 0.76)  # çakılın ışık alan kenarı
sc = mix(sc, (0.46, 0.40, 0.34), pebble)
sc = mix(sc, (0.62, 0.56, 0.48), pebble_hi)
sc = mix(sc, (0.13, 0.08, 0.07), ao < 0.8, 0.8)            # kenara yakın koyu halka
c = np.where(soil[..., None], sc, c)

c = np.where(cov[..., None], c, np.array([0.3, 0.3, 0.32]))
c = np.clip(c, 0, 1)

def write_png(path, rgb):
    a = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w, _ = a.shape
    raw = b''.join(b'\x00' + a[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
                           + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))

write_png(os.path.join(OUT, f"T_Saksi_{SIZE}_Boyali.png"), c)
write_png(os.path.join(DIAG, f"_saksi_{SIZE}_teshis.png"), np.concatenate([base, np.repeat(ao[..., None], 3, 2), np.repeat(np.clip(edge * 4, 0, 1)[..., None], 3, 2)], 1))

# ---------------------------------------------------------------- yeni UV'li model
me.uv_layers.remove(me.uv_layers[old_name])
me.attributes.remove(me.attributes["part"])
final = bpy.data.materials.new("Saksi_Boyali")
for slot in obj.material_slots: slot.material = final
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"Saksi_{SIZE}_Boyali.fbx"), use_selection=True, object_types={'MESH'},
                         mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=False,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_custom_props=False)
print("PAINTED_PLANTER", SIZE, "mesh", obj.name, "res", RES, "covered", int(cov.sum()), "soil", int(soil.sum()), "convex", int(convex.sum()), "concave", int(concave.sum()),
      "part-ink", int(diff.sum()), "bb", bb_min.round(3).tolist(), bb_size.round(3).tolist())
