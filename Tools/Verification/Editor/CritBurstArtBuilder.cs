using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class CritBurstArtBuilder
{
 const string Folder="Assets/Art/LateGame/CritBurst";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  var root=Root("CritBurstImpact",out var c);var stages=root.AddComponent<LateGameCritBurst>();c.critBurst=stages;
  var star=new LateGameMeshBuilder();
  Star(star,1f,.48f,0f,new Color32(44,24,38,255));
  Star(star,.94f,.43f,.045f,new Color32(255,157,33,255));
  Star(star,.72f,.32f,.075f,new Color32(255,218,70,255));
  stages.silhouette=Render(star,"CritBurstStar",c.visual,false).transform;
  var core=new LateGameMeshBuilder();Star(core,.25f,.11f,.16f,new Color32(255,251,226,255));
  stages.core=Render(core,"CritBurstCore",c.visual,false).transform;
  c.fragments=new Transform[6];c.fragmentVelocities=new Vector3[6];
  for(int i=0;i<6;i++){
   var chip=new LateGameMeshBuilder();chip.Tube(new[]{Vector3.zero,Vector3.up*.13f,Vector3.up*.23f},new[]{0f,.047f,0f},i%2==0?new Color32(255,226,111,255):new Color32(255,165,48,255),4);
   c.fragments[i]=Render(chip,"CritBurstChip"+i,c.visual,false).transform;
   float a=i*Mathf.PI/3;c.fragments[i].localPosition=new Vector3(Mathf.Cos(a)*.5f,.13f,Mathf.Sin(a)*.5f);
   c.fragmentVelocities[i]=new Vector3(Mathf.Cos(a)*.9f,1.7f,Mathf.Sin(a)*.9f);
  }
  stages.chips=c.fragments;
  Save(root,LateGameSlot.Pulse,LateGameVariant.CritBurst,.32f);
  string path=Folder+"/CritBurstThump.wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==LateGameSound.CritBurst&&x.variant==LateGameVariant.CritBurst);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=LateGameSound.CritBurst,variant=LateGameVariant.CritBurst,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.28f,repeatSeconds=.16f,voices=1});
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();CritBurstArtVerification.RunBatch();
 }
 static void Star(LateGameMeshBuilder target,float outer,float inner,float y,Color color)
 {
  // Individual wedges avoid concave polygon triangulation; authored tip radius exactly one world unit.
  for(int i=0;i<16;i++){
   float a=(i*22.5f+7)*Mathf.Deg2Rad,b=((i+1)*22.5f+7)*Mathf.Deg2Rad;
   float ra=i%2==0?outer:inner,rb=i%2==0?inner:outer;
   var wedge=new LateGameMeshBuilder();wedge.Prism(new[]{Vector2.zero,new Vector2(Mathf.Cos(a)*ra,Mathf.Sin(a)*ra),new Vector2(Mathf.Cos(b)*rb,Mathf.Sin(b)*rb)},.007f,color);
   target.Append(wedge,Matrix4x4.TRS(Vector3.up*y,Quaternion.Euler(90,0,0),Vector3.one));
  }
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
}

