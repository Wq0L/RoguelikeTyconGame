"""Original volumetric comic meteor lesson; no external assets. Blender 5.x."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Docs' / 'ComicMeteor'
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(119)
s = bpy.data.scenes.new('Comic Meteor - Fall and Impact')
bpy.context.window.scene = s
engines = s.render.bl_rna.properties['engine'].enum_items.keys()
s.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
s.render.resolution_x = 1100
s.render.resolution_y = 1000
s.render.resolution_percentage = 100
s.render.fps = 24
s.frame_start, s.frame_end = 1, 120
s.view_settings.view_transform = 'Standard'
s.world = bpy.data.worlds.new('Dark plum studio')
s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (.045,.025,.075,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value = .3

def material(name, colors, glow=False):
    m=bpy.data.materials.new(name); m.diffuse_color=(*colors[-1],1); m.use_nodes=True
    n=m.node_tree.nodes; n.clear()
    output=n.new('ShaderNodeOutputMaterial'); emit=n.new('ShaderNodeEmission')
    m.node_tree.links.new(emit.outputs[0],output.inputs['Surface'])
    if glow: emit.inputs[0].default_value=(*colors[-1],1)
    else:
        diffuse=n.new('ShaderNodeBsdfDiffuse'); diffuse.inputs['Color'].default_value=(1,1,1,1)
        rgb=n.new('ShaderNodeShaderToRGB'); ramp=n.new('ShaderNodeValToRGB')
        ramp.color_ramp.interpolation='CONSTANT'
        for i,c in enumerate(colors):
            el=ramp.color_ramp.elements[i] if i<2 else ramp.color_ramp.elements.new(i/(len(colors)-1))
            el.position=[0,.32,.66][i]; el.color=(*c,1)
        m.node_tree.links.new(diffuse.outputs[0],rgb.inputs[0]); m.node_tree.links.new(rgb.outputs[0],ramp.inputs[0]); m.node_tree.links.new(ramp.outputs[0],emit.inputs[0])
    return m

ink=material('Ink',[(.018,.008,.028)],True)
rockmat=material('Meteor - three tone lava rock',[(.035,.012,.025),(.24,.045,.035),(.55,.13,.055)])
stone=material('Ground debris - three tones',[(.07,.045,.11),(.24,.16,.30),(.51,.36,.49)])
smoke=material('Smoke - three comic tones',[(.09,.055,.15),(.31,.23,.39),(.59,.46,.62)])
yellow=material('Hot yellow',[(1,.69,.025)],True)
cream=material('White hot core',[(1,.96,.62)],True)
orange=material('Flame - three tones',[(.65,.035,.008),(1,.15,.012),(1,.47,.018)])
floor=material('Stage - muted violet',[(.07,.05,.11),(.16,.12,.23),(.24,.19,.32)])

def collection(name):
    c=bpy.data.collections.new(name); s.collection.children.link(c); return c
meteorC=collection('01 - Meteor and flame / meteor ve alev')
blastC=collection('02 - Impact burst / carpma')
smokeC=collection('03 - Volumetric dust / hacimli duman')
sparkC=collection('04 - Sparks and debris / kivilcim ve tas')
stageC=collection('05 - Ground lights camera / sahne')

def link(o,name,mat,c):
    o.name=name
    for old in list(o.users_collection): old.objects.unlink(o)
    c.objects.link(o)
    if mat: o.data.materials.append(mat)
    return o

def ico(name,loc,scale,mat,c,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=loc)
    o=link(bpy.context.object,name,mat,c); o.scale=scale
    return o

def edge(o):
    e=o.copy(); e.data=o.data.copy(); e.animation_data_clear(); e.name=o.name+' - ink'
    o.users_collection[0].objects.link(e); e.parent=o
    e.location=(0,0,0); e.rotation_euler=(0,0,0); e.scale=(1.002,)*3
    e.data.materials.clear(); e.data.materials.append(ink)
    w=e.modifiers.new('Fine ink edges','WIREFRAME'); w.thickness=.016
    return e

def key(o,f,loc=None,scale=None):
    if loc is not None: o.location=loc; o.keyframe_insert(data_path='location',frame=f)
    if scale is not None: o.scale=scale; o.keyframe_insert(data_path='scale',frame=f)

def cone(name,base,tip,radius,mat,c,vertices=7):
    a,b=Vector(base),Vector(tip); delta=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius,radius2=0,depth=delta.length,location=(a+b)/2)
    o=link(bpy.context.object,name,mat,c)
    o.rotation_euler=delta.to_track_quat('Z','Y').to_euler(); return o

def appear(o,start,peak,end,scale=None):
    size=Vector(scale if scale else o.scale)
    key(o,1,scale=(.001,)*3); key(o,start,scale=(.001,)*3)
    key(o,peak,scale=size); key(o,end,scale=size*.001)

# An empty carries the whole falling meteor; the mesh tumbles independently.
root=bpy.data.objects.new('Meteor flight controller',None); meteorC.objects.link(root)
for f,pos in [(1,(-3.3,0,8.3)),(18,(-2.6,0,6.6)),(32,(-1.35,0,3.65)),(43,(0,0,.68)),(44,(0,0,.50))]: key(root,f,loc=pos)
key(root,1,scale=(1,)*3); key(root,43,scale=(1,)*3); key(root,45,scale=(.001,)*3)
meteor=ico('Faceted meteor', (0,0,0),(.73,.66,.78),rockmat,meteorC,2); meteor.parent=root
for v in meteor.data.vertices: v.co*=rng.uniform(.87,1.12)
lava=meteor.copy(); lava.data=meteor.data.copy(); meteorC.objects.link(lava); lava.name='Glowing lava seams'; lava.parent=meteor
lava.location=(0,0,0); lava.scale=(1.006,)*3; lava.data.materials.clear(); lava.data.materials.append(yellow)
w=lava.modifiers.new('Fine lava cracks','WIREFRAME'); w.thickness=.012
for f,a in [(1,0),(43,1.8)]: meteor.rotation_euler=(a*.7,a,a*.4); meteor.keyframe_insert(data_path='rotation_euler',frame=f)
for i in range(9):
    a=i*math.tau/9; base=(.40*math.cos(a),.4*math.sin(a),.24)
    tip=(-.85+rng.uniform(-.35,.25),rng.uniform(-.32,.32),rng.uniform(2.2,3.6))
    flame=cone('Flame tongue %02d'%i,base,tip,.25,orange if i%3 else yellow,meteorC)
    flame.parent=root
    original=flame.scale.copy()
    for f in (1,9,17,25,33,41): key(flame,f,scale=(1,1,rng.uniform(.8,1.16)))
hot=cone('Inner flame',(-.1,0,.2),(-.7,0,2.6),.39,cream,meteorC); hot.parent=root

# A circular stage gives the effect a clear ground plane and contact shadows.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=5.5,depth=.22,location=(0,0,-.15))
ground=link(bpy.context.object,'Ground stage',floor,stageC)
bpy.ops.mesh.primitive_cylinder_add(vertices=14,radius=1,depth=.035,location=(0,0,-.015))
scorch=link(bpy.context.object,'Impact scorch',ink,blastC)
key(scorch,1,scale=(.001,)*3); key(scorch,43,scale=(.001,)*3); key(scorch,47,scale=(1.6,1.6,1)); key(scorch,120,scale=(1.6,1.6,1))

# Raised spikes form a genuine 3D burst, not a camera-facing star sprite.
for i in range(13):
    a=i*math.tau/13; r=rng.uniform(1.4,2.4)
    o=cone('Impact spike %02d'%i,(0,0,.16),(r*math.cos(a),r*math.sin(a),rng.uniform(.65,2.1)),.42,orange if i%2 else yellow,blastC)
    appear(o,43,47,59)
ball=ico('Flash volume',(0,0,.5),(1.05,1.05,.95),cream,blastC,2); appear(ball,43,45,52)
bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=6,major_radius=1,minor_radius=.055,location=(0,0,.07))
ring=link(bpy.context.object,'Expanding shock ring',yellow,blastC)
key(ring,1,scale=(.001,)*3); key(ring,43,scale=(.001,)*3); key(ring,47,scale=(1.2,1.2,1)); key(ring,59,scale=(3.7,3.7,.35)); key(ring,68,scale=(4.1,4.1,.001))
key(ring,69,scale=(.001,)*3)

for i in range(12):
    a=i*math.tau/12; r=rng.uniform(1.4,2)
    puff=ico('Dust puff %02d'%i,(0,0,.2),(.001,)*3,smoke,smokeC,2)
    for v in puff.data.vertices: v.co*=rng.uniform(.9,1.12)
    key(puff,1,scale=(.001,)*3); key(puff,45,loc=(.35*math.cos(a),.35*math.sin(a),.15),scale=(.001,)*3)
    sz=rng.uniform(.42,.65)
    key(puff,55,loc=(r*math.cos(a),r*math.sin(a),.4),scale=(sz,sz,sz*.85))
    key(puff,72,loc=(r*1.6*math.cos(a),r*1.6*math.sin(a),.7),scale=(sz*1.3,sz*1.3,sz))
    key(puff,98,loc=(r*1.85*math.cos(a),r*1.85*math.sin(a),1.2),scale=(.001,)*3)

# Art-directed ballistic trajectories. Each spark is a solid elongated mesh.
for i in range(42):
    a=rng.uniform(0,math.tau); speed=rng.uniform(2.3,5.8); vz=rng.uniform(3.2,7.7)
    is_rock=i<14; size=rng.uniform(.07,.18) if is_rock else rng.uniform(.025,.06)
    o=ico(('Debris ' if is_rock else 'Spark ')+str(i),(0,0,.25),(.001,)*3,stone if is_rock else (cream if i%3 else yellow),sparkC)
    if is_rock: edge(o)
    key(o,1,scale=(.001,)*3); key(o,43,scale=(.001,)*3)
    for f in range(44,89,3):
        t=(f-44)/24; z=.35+vz*t-5.8*t*t
        pos=(math.cos(a)*speed*t,math.sin(a)*speed*t,max(.08,z))
        alive=z>.02 and f<86
        shape=(size,size,size*(1 if is_rock else 4.5)) if alive else (.001,)*3
        key(o,f,loc=pos,scale=shape)
        velocity=Vector((math.cos(a)*speed,math.sin(a)*speed,vz-11.6*t))
        o.rotation_euler=velocity.to_track_quat('Z','Y').to_euler()
        if is_rock: o.rotation_euler.x+=t*4
        o.keyframe_insert(data_path='rotation_euler',frame=f)
    key(o,90,scale=(.001,)*3)

bpy.ops.object.light_add(type='AREA',location=(-3,-4,9))
light=link(bpy.context.object,'Large comic key light',None,stageC); light.data.energy=1800; light.data.shape='DISK'; light.data.size=5
light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(10,-16,11))
cam=link(bpy.context.object,'Perspective lesson camera',None,stageC)
cam.rotation_euler=(Vector((0,0,2.3))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='PERSP'; cam.data.lens=48; s.camera=cam
for frame,target_z,lens in [(1,2.3,48),(28,2.3,48),(43,.65,61),(65,.65,61),(100,1.0,54)]:
    cam.rotation_euler=(Vector((0,0,target_z))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.keyframe_insert(data_path='rotation_euler',frame=frame)
    cam.data.lens=lens; cam.data.keyframe_insert(data_path='lens',frame=frame)

for o in s.objects:
    if not o.animation_data or not o.animation_data.action: continue
    for layer in o.animation_data.action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for k in curve.keyframe_points: k.interpolation='LINEAR'
for f,name in [(1,'FALL / DUSUS'),(32,'APPROACH / YAKLASMA'),(44,'IMPACT / CARPMA'),(55,'DEBRIS / PARCALAR'),(90,'SETTLE / SON')]: s.timeline_markers.new(name,frame=f)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'; area.spaces.active.shading.type='RENDERED'; area.spaces.active.overlay.show_overlays=False
s.frame_set(50)
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True); bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ComicMeteor.blend'))
for frame,name in [(28,'Meteor-Fall.png'),(50,'Meteor-Impact.png')]:
    s.frame_set(frame); s.render.filepath=str(OUT/name); bpy.ops.render.render(write_still=True)
report={'scene':s.name,'objects':len(s.objects),'fps':24,'frames':[1,120],'impact_frame':44,'external_assets':False}
(OUT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('COMIC_METEOR_OK',report)
