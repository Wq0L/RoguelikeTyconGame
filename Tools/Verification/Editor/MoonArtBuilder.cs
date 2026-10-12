using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class MoonArtBuilder
{
 const string Folder="Assets/Art/LateGame/CrescentFamily";
 static Material toon,ink;static LateGameArtSO art;
 static readonly Color Pale=new Color32(236,255,246,255),Cyan=new Color32(64,211,230,255),Gold=new Color32(255,192,65,255),Cream=new Color32(255,240,181,255);
 public static void BuildAndRunBatch(){Build();MoonCursorVerification.RunBatch();}
 public static void BuildAndRunSmallBatch(){Build();MoonCursorVerification.RunBatch();}
 public static void Build()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs");AssetDatabase.Refresh();
  toon=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  BuildWave(false);BuildWave(true);Contact(false);Contact(true);
  art.sounds.RemoveAll(s=>s.variant==LateGameVariant.Crescent);
  Sound("Launch",LateGameSound.CrescentLaunch,.30f,.12f);Sound("Grow",LateGameSound.MoonGrowth,.23f,.13f);Sound("Return",LateGameSound.FullMoon,.34f,.18f);
  var profile=AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset");
  profile.crescentFullRows=true;EditorUtility.SetDirty(profile);EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
  // Preserve the production profile before the legacy regression's isolated overrides.
  File.Copy("Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset","Logs/FullRowProfile.asset",true);
  using var report=new StreamWriter("Logs/MoonGeometry.txt");
  foreach(var path in Directory.GetFiles(Folder,"*.prefab"))
  {var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);int tris=0,render=0,vertices=0;foreach(var f in go.GetComponentsInChildren<MeshFilter>()){render+=f.sharedMesh.triangles.Length/3;if(f.name=="Ink")continue;tris+=f.sharedMesh.triangles.Length/3;vertices+=f.sharedMesh.vertexCount;}report.WriteLine($"{go.name}: {tris} triangles; {vertices} flat vertices; {render} triangles including ink; {go.GetComponentsInChildren<Renderer>().Length} renderers");}
 }
 static GameObject Root(string name,out LateGameContent c){var root=new GameObject(name);c=root.AddComponent<LateGameContent>();c.visual=new GameObject("Visual").transform;c.visual.SetParent(root.transform,false);return root;}
 static void Save(GameObject root,LateGameSlot slot,LateGameVariant variant,float life)
 {var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+root.name+".prefab");Object.DestroyImmediate(root);art.slots.RemoveAll(s=>s.slot==slot&&s.variant==variant);art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=variant,prefab=prefab,lifetime=life});}
 static void Render(LateGameMeshBuilder g,string name,Transform parent,bool outline)
 {
  var mesh=g.Mesh(name);var path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}EditorUtility.SetDirty(mesh);
  var go=new GameObject(name);go.transform.SetParent(parent,false);Draw(go,mesh,toon);
  if(outline){var hull=new GameObject("Ink");hull.transform.SetParent(go.transform,false);Draw(hull,mesh,ink);}
 }
 static void Draw(GameObject go,Mesh mesh,Material mat){go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
 static void Arc(LateGameMeshBuilder g,float offset,float thickness,Color color,bool reverse)
 {
  const int segments=14;
  for(int i=0;i<segments;i++)
  {
   float a=-1.65f+3.3f*i/segments,b=-1.65f+3.3f*(i+1)/segments;
   float za=.48f*Mathf.Cos(a)+offset,zb=.48f*Mathf.Cos(b)+offset;
   float wa=thickness*Mathf.Sin(Mathf.PI*i/segments),wb=thickness*Mathf.Sin(Mathf.PI*(i+1)/segments);
   var slice=new LateGameMeshBuilder();slice.Prism(new[]{new Vector2(.48f*Mathf.Sin(a),za-wa),new Vector2(.48f*Mathf.Sin(b),zb-wb),new Vector2(.48f*Mathf.Sin(b),zb),new Vector2(.48f*Mathf.Sin(a),za)},.016f,color);
   g.Append(slice,Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(90,0,0),Vector3.one));
  }
 }
 static void BuildWave(bool back)
 {
  string name=back?"WarmReturnMoon":"CyanCrescent";var root=Root(name,out var c);c.hideSeconds=.28f;
  var face=new LateGameMeshBuilder();Arc(face,0,back?.29f:.24f,back?Cream:Pale,back);Render(face,name+"Face",c.visual,true);
  var energy=new LateGameMeshBuilder();Arc(energy,-.10f,.10f,back?Gold:Cyan,back);
  // Two short tapered trails, attached to this single travelling object; no per-cell clones.
  for(int side=-1;side<=1;side+=2)
  {
   float x=side*.36f;energy.Tube(new[]{new Vector3(x,.015f,-.14f),new Vector3(x*.7f,.015f,-.48f),new Vector3(x*.6f,.015f,-.83f)},new[]{.035f,.018f,0},back?Gold:Cyan,3);
  }
  if(back)Arc(energy,-.28f,.055f,Gold,true);
  for(int i=0;i<3;i++){float x=(i-1)*.28f;energy.Tube(new[]{new Vector3(x,.03f,-.48f-i*.08f),new Vector3(x,.06f,-.52f-i*.08f)},new[]{.025f,0},back?Gold:Cyan,3);}
  Render(energy,name+"EdgeTrail",c.visual,false);Save(root,LateGameSlot.CrescentBlade,back?LateGameVariant.MoonReturn:LateGameVariant.Crescent,0);
 }
 static void Contact(bool back)
 {
  string name=back?"WarmMoonContact":"MoonContact";var root=Root(name,out var c);var g=new LateGameMeshBuilder();
  for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var d=new Vector3(Mathf.Cos(a),.65f,Mathf.Sin(a));g.Tube(new[]{d*.06f,d*.35f},new[]{.04f,0},back?Gold:Cyan,3);}
  Render(g,name+"Mesh",c.visual,false);Save(root,LateGameSlot.MoonContact,back?LateGameVariant.MoonReturn:LateGameVariant.Crescent,.16f);
 }
 static void Sound(string name,LateGameSound sound,float volume,float interval)
 {
  var path=Folder+"/Audio/"+name+".wav";var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(importer==null)throw new Exception(path);
  importer.forceToMono=true;var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.Add(new LateGameArtSO.SoundBinding{variant=LateGameVariant.Crescent,sound=sound,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=volume,repeatSeconds=interval,voices=2});
 }
}
