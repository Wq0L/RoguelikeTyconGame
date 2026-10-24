using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Original geometry authored for this project from the user's visual direction; no imported meshes.
public static class AntennaArtBuilder
{
 const string Folder="Assets/Art/LateGame/AntennaFamily";
 static Material body,ink;static LateGameArtSO art;
 static readonly Color CircuitRed=new Color32(255,40,48,255),CircuitCore=new Color32(255,148,148,255);
 public static void RecolorCircuitAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("Use isolated verification project");
  foreach(var name in new[]{"CircuitDoubleMesh","ContactDoubleMesh"})
  {
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".asset");
   var colors=mesh.colors;
   for(int i=0;i<colors.Length;i++)
   {
    var original=colors[i].gamma;
    var color=(original.b>.5f?CircuitCore:CircuitRed)*original.r;
    color.a=1;colors[i]=color.linear;
   }
   mesh.colors=colors;EditorUtility.SetDirty(mesh);
  }
  AssetDatabase.SaveAssets();AntennaArtVerification.RunBatch();
 }
 static readonly Color Stone=new Color32(76,101,94,255),Dark=new Color32(54,42,60,255),Gold=new Color32(255,192,68,255),Copper=new Color32(186,100,67,255),Leaf=new Color32(121,164,63,255),Cyan=new Color32(112,238,248,255),Cream=new Color32(255,245,186,255);
 public static void BuildAndRunBatch(){Build();AntennaArtVerification.RunBatch();}
 public static void Build()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("Use isolated verification project");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  Model();Bolt("AntennaSnap",LateGameSlot.AntennaBolt,LateGameVariant.Antenna,.18f,0);
  Bolt("ConductiveFlash",LateGameSlot.LineBolt,LateGameVariant.Antenna,.23f,1);
  Bolt("CircuitDouble",LateGameSlot.LineBolt,LateGameVariant.Circuit,.34f,2);
  Bolt("CircuitReturn",LateGameSlot.CircuitReturn,LateGameVariant.Antenna,.30f,3);
  Contact("ContactStar",LateGameVariant.Antenna);Contact("ContactDouble",LateGameVariant.Circuit);
  Marker("PlacementSocket",LateGameSlot.PlacementMarker,Cyan,false);Marker("PlacementAim",LateGameSlot.PlacementSelected,Gold,true);Marker("PlacementCell",LateGameSlot.PlacementCell,Gold,false);
  art.sounds.RemoveAll(s=>s.variant==LateGameVariant.Antenna);
  Sound("Settle",LateGameSound.AntennaPlaced,.38f,.12f);Sound("Snap",LateGameSound.AntennaPulse,.28f,.1f);Sound("Line",LateGameSound.LinePulse,.34f,.15f);Sound("Circuit",LateGameSound.CircuitPulse,.34f,.2f);
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();Report();
 }
 static GameObject Root(string name,out LateGameContent c)
 {var root=new GameObject(name);c=root.AddComponent<LateGameContent>();c.visual=new GameObject("Visual").transform;c.visual.SetParent(root.transform,false);return root;}
 static void Save(GameObject root,LateGameSlot slot,LateGameVariant variant,float life=0)
 {var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+root.name+".prefab");Object.DestroyImmediate(root);art.slots.RemoveAll(s=>s.slot==slot&&s.variant==variant);art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=variant,prefab=prefab,lifetime=life});}
 static GameObject Render(LateGameMeshBuilder geo,string name,Transform parent,bool outlined=true)
 {
  var mesh=geo.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
  EditorUtility.SetDirty(mesh);
  var go=new GameObject(name);go.transform.SetParent(parent,false);Draw(go,mesh,body);
  if(outlined){var hull=new GameObject("Ink");hull.transform.SetParent(go.transform,false);Draw(hull,mesh,ink);}return go;
 }
 static void Draw(GameObject go,Mesh mesh,Material m){go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
 static void Column(LateGameMeshBuilder g,Vector3 p,float bottom,float top,float height,Color c,int sides=6)=>g.Tube(new[]{p,p+Vector3.up*height},new[]{bottom,top},c,sides);
 static void Model()
 {
  var root=Root("FieldAntenna",out var content);var anim=root.AddComponent<LateGameAntenna>();content.antenna=anim;
  var baseGeo=new LateGameMeshBuilder();
  for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Column(baseGeo,new Vector3(Mathf.Cos(a)*.47f,0,Mathf.Sin(a)*.38f),.27f,.25f,.18f,Dark,4);}
  baseGeo.Tube(new[]{new Vector3(0,.12f,0),new Vector3(-.05f,.60f,-.05f),new Vector3(.02f,.87f,0)},new[]{.48f,.40f,.24f},Stone,6);
  Column(baseGeo,new Vector3(0,.83f,0),.28f,.26f,.12f,Dark);
  for(int i=0;i<3;i++){var leaf=new LateGameMeshBuilder();leaf.Leaf(.48f,.14f,Leaf,Stone);baseGeo.Append(leaf,Matrix4x4.TRS(new Vector3(i==0?-.40f:.24f,.2f+i*.16f,-.19f),Quaternion.Euler(-40,i*110,30),Vector3.one));}
  baseGeo.Tube(new[]{new Vector3(-.32f,.49f,-.2f),new Vector3(-.55f,.4f,-.18f),new Vector3(-.58f,.24f,-.35f),new Vector3(-.43f,.2f,-.42f)},new[]{.06f,.06f,.06f,.06f},Copper,4);
  Render(baseGeo,"StoneLeafBase",content.visual);
  anim.mast=new GameObject("SpringMast").transform;anim.mast.SetParent(content.visual,false);
  var mast=new LateGameMeshBuilder();Column(mast,new Vector3(0,.91f,0),.13f,.13f,.87f,Dark);
  for(int j=0;j<3;j++)
  {
   var points=new Vector3[9];var radii=new float[9];for(int i=0;i<9;i++){float a=i*Mathf.PI/4;points[i]=new Vector3(Mathf.Cos(a)*.235f,1.02f+j*.20f,Mathf.Sin(a)*.235f);radii[i]=.085f;}
   mast.Tube(points,radii,Gold,4);
  }
  Column(mast,new Vector3(0,1.57f,.06f),.19f,.15f,.18f,Stone);
  for(int side=-1;side<=1;side+=2)
   mast.Tube(new[]{new Vector3(side*.08f,1.67f,.06f),new Vector3(side*.4f,1.73f,.14f),new Vector3(side*.45f,2.04f,.39f),new Vector3(side*.30f,2.16f,.56f)},new[]{.085f,.085f,.07f,.045f},Copper,4);
  // Forward gold beak and cyan tip make orientation legible from side/top views.
  mast.Tube(new[]{new Vector3(0,1.74f,.1f),new Vector3(0,1.84f,.53f)},new[]{.13f,.035f},Gold,5);
  Render(mast,"CoilAndFork",anim.mast);
  var glow=new LateGameMeshBuilder();Column(glow,Vector3.zero,.11f,.015f,.18f,Cream,4);
  anim.charge=Render(glow,"ChargeDiamond",anim.mast,false).transform;anim.charge.localPosition=new Vector3(0,1.92f,.45f);
  content.topPoint=new GameObject("Muzzle").transform;content.topPoint.SetParent(anim.mast,false);content.topPoint.localPosition=new Vector3(0,1.91f,.55f);
  Save(root,LateGameSlot.Antenna,LateGameVariant.Antenna);
 }
 static void Bolt(string name,LateGameSlot slot,LateGameVariant variant,float life,int mode)
 {
  var root=Root(name,out var c);var g=new LateGameMeshBuilder();
  if(mode==3)
  {
   var token=new LateGameMeshBuilder();
   for(int i=0;i<3;i++)Column(token,new Vector3(0,i*.055f,-i*.008f),.085f,.0f,.14f,Gold,4);
   // This prefab's Z is stretched to the entire line. Keep the travelling token compact.
   g.Append(token,Matrix4x4.Scale(new Vector3(1,1,.05f)));
  }
  else
  {
   int strands=mode==2?2:1;
   for(int s=0;s<strands;s++)
   {
    var p=new Vector3[7];var widths=new float[7];
    for(int i=0;i<7;i++){p[i]=new Vector3((i==0||i==6?0:(i%2==0?-.13f:.13f))+(s==0?0:.12f),s*.13f,i/6f);widths[i]=mode==0?.033f:.044f;}
    g.Tube(p,widths,mode==2?CircuitRed:Cyan,4);
    for(int i=0;i<7;i++){p[i]+=Vector3.up*.018f;widths[i]*=.4f;}g.Tube(p,widths,mode==2?CircuitCore:Cream,3);
   }
  }
  var mesh=Render(g,name+"Mesh",c.visual,false);
  if(mode>=2){var a=root.AddComponent<LateGameAntenna>();c.antenna=a;a.feedback=mesh.transform;a.doubleStrike=mode==2;a.returning=mode==3;}
  Save(root,slot,variant,life);
 }
 static void Contact(string name,LateGameVariant variant)
 {
  var root=Root(name,out var c);var g=new LateGameMeshBuilder();
  int count=variant==LateGameVariant.Circuit?6:4;
  for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;var d=new Vector3(Mathf.Cos(a),.6f,Mathf.Sin(a));g.Tube(new[]{d*.08f,d*.35f,d*.50f},new[]{.06f,.035f,0f},variant==LateGameVariant.Circuit?(i%2==0?CircuitCore:CircuitRed):(i%2==0?Cream:Cyan),3);}
  Render(g,name+"Mesh",c.visual,false);Save(root,LateGameSlot.AntennaContact,variant,.20f);
 }
 static void Marker(string name,LateGameSlot slot,Color col,bool selected)
 {
  var root=Root(name,out var c);var g=new LateGameMeshBuilder();
  // Sparse bracket/arrow, only during placement. +Z always points into the field.
  float size=slot==LateGameSlot.PlacementCell?.23f:.43f;
  for(int side=-1;side<=1;side+=2)g.Tube(new[]{new Vector3(side*size,0,-size),new Vector3(side*size,0,size),new Vector3(side*size*.5f,0,size)},new[]{.025f,.025f,.025f},col,4);
  if(slot!=LateGameSlot.PlacementCell)g.Tube(new[]{new Vector3(-.18f,.05f,.3f),new Vector3(0,.05f,.58f),new Vector3(.18f,.05f,.3f)},new[]{.04f,.04f,.04f},col,4);
  if(selected){var p=new LateGameMeshBuilder();p.Leaf(.5f,.18f,Gold,Cream);g.Append(p,Matrix4x4.TRS(new Vector3(0,.03f,0),Quaternion.identity,Vector3.one));}
  Render(g,name+"Mesh",c.visual,false);Save(root,slot,LateGameVariant.Antenna);
 }
 static void Sound(string name,LateGameSound kind,float volume,float cooldown)
 {
  var path=Folder+"/Audio/"+name+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new Exception(path);
  importer.forceToMono=true;var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=kind,variant=LateGameVariant.Antenna,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=volume,repeatSeconds=cooldown,voices=2});
 }
 static void Report()
 {
  Directory.CreateDirectory("Logs");using var report=new StreamWriter("Logs/AntennaGeometry.txt");
  foreach(var path in Directory.GetFiles(Folder,"*.prefab"))
  {var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);int tris=0,verts=0,renderTris=0;foreach(var f in p.GetComponentsInChildren<MeshFilter>(true)){renderTris+=f.sharedMesh.triangles.Length/3;if(f.name=="Ink")continue;tris+=f.sharedMesh.triangles.Length/3;verts+=f.sharedMesh.vertexCount;}report.WriteLine($"{p.name}: triangles={tris}; flat-shaded vertices={verts}; triangles including ink={renderTris}; renderers={p.GetComponentsInChildren<Renderer>(true).Length}");}
 }
}
