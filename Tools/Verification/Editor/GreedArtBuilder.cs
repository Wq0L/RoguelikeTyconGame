using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
// 24 Ekim 2026: bu araç ESKİ amber diş görselini (GreedTeeth.prefab) kurar; oyun artık onu kullanmaz.
// Güncel görsel: GreedJawArtBuilder (aç ağız halkası) ve LateGamePresenter.Greed.cs.
public static class GreedArtBuilder
{
 const string Folder="Assets/Art/LateGame/GreedyHarvest";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  var root=Root("GreedTeeth",out var c);c.hideSeconds=.1f;
  var geo=new LateGameMeshBuilder();
  for(int tooth=0;tooth<3;tooth++)
  {
   // Three separated, tapered harvesting hooks; explicit dark edge, no ring.
   for(int layer=0;layer<3;layer++)
   {
    var points=new Vector3[5];var widths=new float[5];
    for(int i=0;i<5;i++){
     float a=(tooth*120+40+i*10)*Mathf.Deg2Rad;
     float radius=.78f-i*.015f;
     points[i]=new Vector3(Mathf.Cos(a)*radius,0f,Mathf.Sin(a)*radius);
     widths[i]=new[]{.075f,.13f,.11f,.07f,.002f}[i]*(layer==0?1.2f:layer==1?.95f:.38f);
    }
    var strip=new LateGameMeshBuilder();strip.Tube(points,widths,layer==0?new Color32(42,25,37,255):layer==1?new Color32(255,173,42,255):new Color32(255,246,201,255),4);
    geo.Append(strip,Matrix4x4.TRS(Vector3.up*(layer*.04f),Quaternion.identity,new Vector3(1,.12f,1)));
   }
  }
  var facing=new LateGameMeshBuilder();facing.Append(geo,Matrix4x4.Rotate(Quaternion.Euler(-90f,0,0)));
  Render(facing,"GreedTeethMesh",c.visual,false);
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/GreedTeeth.prefab");Object.DestroyImmediate(root);
  foreach(var slot in new[]{LateGameSlot.GreedIndicator,LateGameSlot.Pulse}){
   art.slots.RemoveAll(s=>s.slot==slot&&s.variant==LateGameVariant.Greed);
   art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=LateGameVariant.Greed,prefab=prefab,lifetime=slot==LateGameSlot.Pulse?.16f:0});
  }
  foreach(var sound in new[]{LateGameSound.GreedReady,LateGameSound.GreedSpent}){
   string path=Folder+"/"+sound+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
   var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
   art.sounds.RemoveAll(x=>x.sound==sound&&x.variant==LateGameVariant.Greed);
   art.sounds.Add(new LateGameArtSO.SoundBinding{sound=sound,variant=LateGameVariant.Greed,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.25f,repeatSeconds=.18f,voices=1});
  }
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();GreedArtVerification.RunBatch();
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



