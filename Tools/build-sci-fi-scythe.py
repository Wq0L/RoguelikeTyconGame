"""Run with Blender --background --python Tools/build-sci-fi-scythe.py. Units: metres."""
import bpy, math, os
from mathutils import Vector
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def material(name, color, metallic=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metallic; bs.inputs['Roughness'].default_value=.38
    return m
ink=material('Scythe_Gunmetal',(.035,.065,.10),.5)
white=material('Scythe_Ceramic',(.76,.85,.86),.25)
cyan=material('Scythe_Plasma',(.04,.85,.8),.15)
gold=material('Scythe_Amber',(1,.48,.055),.25)
def prism(name, points, thickness, z, mat):
    n=len(points); verts=[(x,y,z+dz) for dz in [-thickness/2,thickness/2] for x,y in points]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o); o.data.materials.append(mat)
    bevel=o.modifiers.new('Machined edge bevel','BEVEL'); bevel.width=.018; bevel.segments=1
    normal=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def block(name,x,y,w,h,z,depth,mat):return prism(name,[(x-w/2,y-h/2),(x+w/2,y-h/2),(x+w/2,y+h/2),(x-w/2,y+h/2)],depth,z,mat)
block('Grip spine',-.38,-.20,.13,1.55,0,.14,ink)
block('Ceramic shaft',-.38,-.03,.085,.96,.085,.045,white)
for i in range(5): block('Grip rib %02d'%i,-.38,-.76+i*.07,.17,.034,.015,.17,ink)
block('Power cell',-.38,-.38,.055,.24,.12,.024,cyan)
for y in [-.93,.26]:block('Amber collar',-.38,y,.20,.085,.025,.2,gold)
prism('Angular head housing',[(-.56,.24),(-.17,.21),(.02,.45),(-.09,.73),(-.57,.70),(-.64,.48)],.22,0,ink)
prism('Head faceplate',[(-.54,.32),(-.21,.30),(-.09,.46),(-.18,.62),(-.53,.61)],.035,.13,white)
block('Reactor core',-.33,.46,.13,.17,.16,.04,cyan)
# Broad hooked silhouette with a pointed inward cutting edge.
prism('Blade dark spine',[(-.18,.72),(.16,.80),(.57,.71),(.88,.47),(1.02,.12),(.96,-.15),(.77,.16),(.50,.37),(.14,.47),(-.18,.43)],.12,0,ink)
prism('Blade ceramic shell',[(-.13,.67),(.16,.74),(.54,.66),(.80,.46),(.88,.27),(.54,.47),(.17,.57),(-.13,.52)],.035,.085,white)
prism('Plasma cutting edge',[(-.12,.48),(.17,.52),(.51,.43),(.79,.22),(.97,-.14),(.71,.15),(.47,.33),(.12,.42),(-.12,.40)],.05,.02,cyan)
for x,y in [(.18,.66),(.35,.62)]:block('Spine heat vent',x,y,.065,.075,.12,.027,ink)
block('Warning tab',-.02,.65,.085,.095,.115,.028,gold)
models=[o for o in bpy.context.scene.objects if o.type=='MESH']
os.makedirs(os.path.join(root,'ArtSource','Scythe'),exist_ok=True)
os.makedirs(os.path.join(root,'Assets','Art','Behaviors'),exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for o in models:o.select_set(True)
bpy.context.view_layer.objects.active=models[0]
# Presentation camera, retained in the editable .blend, excluded from the FBX.
bpy.ops.object.camera_add(location=(2,-3.4,6))
camera=bpy.context.object; camera.rotation_euler=(Vector((.1,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=3.4
scene=bpy.context.scene;scene.camera=camera
for loc,power,size in [((1,-2,5),850,4),((-3,2,3),650,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
scene.world.color=(.1,.1,.1);scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
scene.render.filepath=os.path.join(root,'ArtSource','Scythe','SciFiScythe.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'ArtSource','Scythe','SciFiScythe.blend'))
bpy.ops.render.render(write_still=True)
# Keep the editable source pieces in .blend; export only four material batches.
exports=[]
for mat in [ink,white,cyan,gold]:
    bpy.ops.object.select_all(action='DESELECT');group=[]
    for original in models:
        if original.data.materials[0]!=mat:continue
        clone=original.copy();clone.data=original.data.copy();bpy.context.collection.objects.link(clone)
        bpy.context.view_layer.objects.active=clone;clone.select_set(True)
        for modifier in list(clone.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
        clone.select_set(False);group.append(clone)
    for clone in group:clone.select_set(True)
    bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join()
    joined=bpy.context.object;joined.name=mat.name;exports.append(joined)
bpy.ops.object.select_all(action='DESELECT')
for o in exports:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(root,'Assets','Art','Behaviors','SciFiScythe.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True)
