"""Original mesh-only comic VFX lesson. Run with Blender --background --python.
No downloads, textures, add-ons, or third-party assets. Existing scenes are preserved.
"""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'ComicBlackHole'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('Comic Black Hole - Lesson')
bpy.context.window.scene = scene
engines = scene.render.bl_rna.properties['engine'].enum_items.keys()
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x = 1000
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.render.fps = 24
scene.frame_start = 1
scene.frame_end = 96
scene.view_settings.view_transform = 'Standard'
scene.render.image_settings.file_format = 'PNG'
scene.world = bpy.data.worlds.new('Paper backdrop')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (0.075, .052, .11, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .4

def mat(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    n = m.node_tree.nodes
    n.clear()
    e = n.new('ShaderNodeEmission')
    e.inputs[0].default_value = (*color, 1)
    o = n.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(e.outputs[0], o.inputs['Surface'])
    return m

ink = mat('Ink - deep plum', (.014, .006, .025))
gold = mat('Solar yellow', (1, .66, .055))
cream = mat('Hot paper highlight', (1, .94, .62))
orange = mat('Burnt orange', (1, .17, .035))
purple = mat('Orbit violet', (.43, .09, .76))
stone = [mat('Stone shadow', (.15, .08, .23)), mat('Stone midtone', (.37, .24, .47)), mat('Stone light', (.64, .47, .72)), mat('Stone warm rim', (.94, .53, .27))]

def group(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

core_group = group('01 - Core / merkez')
ring_group = group('02 - Accretion ribbons / halkalar')
rock_group = group('03 - Orbiting rocks / taslar')
line_group = group('04 - Speed streaks / hiz izleri')
stage_group = group('05 - Camera / kamera')

def move(obj, collection):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)

def mesh(name, vertices, faces, material, collection):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj

def spin(obj, turns=1):
    obj.rotation_euler.z = 0
    obj.keyframe_insert(data_path='rotation_euler', frame=1)
    obj.rotation_euler.z = math.tau * turns
    obj.keyframe_insert(data_path='rotation_euler', frame=97)
    action = obj.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points: key.interpolation = 'LINEAR'

def ribbon(name, radius, width, start, sweep, z, material, collection, parent=None):
    # Tapered angular strip: two vertices per sample, one quad per segment.
    verts, faces = [], []
    count = max(8, int(abs(sweep) * 17))
    for i in range(count + 1):
        t = i/count
        a = start + sweep*t
        w = width * (math.sin(math.pi*t)**.55) + .003
        r = radius + .035*math.sin(a*5)
        for edge in (-1, 1):
            rr = r + edge*w/2
            verts.append((rr*math.cos(a), rr*math.sin(a), z))
        if i: faces.append((2*i-2, 2*i, 2*i+1, 2*i-1))
    o = mesh(name, verts, faces, material, collection)
    o.parent = parent
    return o

# Step 1: opaque center. No realistic lensing: silhouette is the visual priority.
bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=.92)
core = bpy.context.object
core.name = 'Event horizon - opaque ink'
core.data.materials.append(ink)
move(core, core_group)
for p in core.data.polygons: p.use_smooth = True

# Step 2: circles become torn, tapered ribbons; separate speeds add depth.
for band, (radius, width, material, turns) in enumerate([(1.13,.26,cream,1),(1.42,.32,gold,-1),(1.76,.22,orange,1),(2.05,.11,purple,-2)]):
    pivot = bpy.data.objects.new('Ribbon rotation %02d' % band, None)
    ring_group.objects.link(pivot)
    spin(pivot, turns)
    for j in range(3):
        a = j*math.tau/3 + band*.55
        ribbon('Ink edge %d-%d' % (band,j), radius, width+.09,a,1.72,-.035,ink,ring_group,pivot)
        ribbon('Color ribbon %d-%d' % (band,j),radius,width,a+.025,1.66,.005,material,ring_group,pivot)

# Step 3: each rock has its own parent pivot (orbit) and local rotation (tumble).
rng = random.Random(73)
for i in range(11):
    pivot = bpy.data.objects.new('Orbit pivot %02d' % (i+1), None)
    rock_group.objects.link(pivot)
    pivot.rotation_euler.x = rng.uniform(-.12,.12)
    spin(pivot, 1 if i%3 else 2)
    a = i*math.tau/11 + .13
    radius = rng.uniform(2.35,3.15)
    size = rng.uniform(.13,.31)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1)
    rock = bpy.context.object
    rock.name = 'Rock %02d - flat facets' % (i+1)
    move(rock, rock_group)
    for v in rock.data.vertices: v.co *= rng.uniform(.82,1.18)
    rock.parent = pivot
    rock.location = (radius*math.cos(a),radius*math.sin(a),rng.uniform(-.2,.45))
    rock.scale = (size*1.3,size*.85,size)
    for m in stone: rock.data.materials.append(m)
    for face in rock.data.polygons: face.material_index = rng.choice([0,1,1,2,3])
    spin(rock, -2 if i%2 else 3)
    # Thin ink on edges makes the polygon planes readable without a texture.
    edges = rock.copy()
    edges.data = rock.data.copy()
    edges.name = 'Rock %02d - ink edges' % (i+1)
    rock_group.objects.link(edges)
    edges.parent = rock
    edges.animation_data_clear()
    edges.location = (0,0,0)
    edges.rotation_euler = (0,0,0)
    edges.scale = (1.008,1.008,1.008)
    edges.data.materials.clear()
    edges.data.materials.append(ink)
    for face in edges.data.polygons: face.material_index = 0
    wire = edges.modifiers.new('Comic edge lines', 'WIREFRAME')
    wire.thickness = .035
    wire.use_replace = True
    ribbon('Rock wake %02d' % (i+1),radius,.028,a-.7,.53,-.02,gold if i%2 else purple,line_group,pivot)

# Step 4: short curved strokes give motion a direction.
for i in range(14):
    a = i*2.399
    ribbon('Outer motion dash %02d' % i,3.15+(i%3)*.17,.035,a,.10+(i%4)*.035,0,purple if i%2 else gold,line_group)

bpy.ops.object.camera_add(location=(0,-7.8,10.5))
camera = bpy.context.object
camera.name = 'Lesson Camera'
move(camera, stage_group)
camera.rotation_euler = (Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 8.4
scene.camera = camera
scene.frame_set(13)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'RENDERED'
            area.spaces.active.overlay.show_overlays = False
            area.spaces.active.region_3d.view_camera_zoom = 0
text = bpy.data.texts.new('START HERE - Comic Black Hole')
text.write('COMIC BLACK HOLE / 4 second loop at 24 fps\n\n01 Core: opaque sphere.\n02 Ribbons: tapered flat meshes, parent rotation.\n03 Rocks: faceted ico spheres. Parent = orbit; local rotation = tumble.\n04 Streaks: short arcs show the orbit direction.\n\nSpace: play/pause. Numpad 0: camera.\nCollections can be hidden one at a time to study each layer.\nFrames 1-96 loop; frame 97 is the repeated start pose.\nAll geometry authored for this lesson, no external assets.\n')
bpy.ops.object.select_all(action='DESELECT')
core.select_set(True)
bpy.context.view_layer.objects.active = core
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ComicBlackHole.blend'))
scene.render.filepath = str(OUT/'ComicBlackHole-Preview.png')
bpy.ops.render.render(write_still=True)
print('COMIC_BLACK_HOLE_OK', str(OUT))
