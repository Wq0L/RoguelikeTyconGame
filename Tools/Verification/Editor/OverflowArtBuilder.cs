using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class OverflowArtBuilder
{
 const string Folder="Assets/Art/LateGame/Overflow";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  Color gold=new Color32(255,167,40,255),cream=new Color32(255,245,200,255);
  var root=Root("OverflowShard",out var c);var g=new LateGameMeshBuilder();
  g.Tube(new[]{new Vector3(0,0,-.48f),Vector3.zero,new Vector3(0,0,.3f)},new[]{0f,.19f,0f},gold,4);
  g.Tube(new[]{new Vector3(0,0,-.85f),new Vector3(0,0,-.25f)},new[]{0f,.06f},gold,3);
  g.Tube(new[]{new Vector3(0,.06f,-.3f),new Vector3(0,.16f,0),new Vector3(0,.025f,.24f)},new[]{0f,.055f,0f},cream,3);
  Render(g,"OverflowShardMesh",c.visual);Save(root,LateGameSlot.ArcBolt,LateGameVariant.Overflow,.14f);
  root=Root("OverflowContact",out c);
  var star=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/LateGame/AntennaFamily/ContactDouble.prefab");
  var shared=star.GetComponentInChildren<MeshFilter>().sharedMesh;
  var center=new GameObject("SharedAmberStar");center.transform.SetParent(c.visual,false);Draw(center,shared,body);
  c.fragments=new Transform[3];c.fragmentVelocities=new Vector3[3];
  for(int i=0;i<3;i++){
   var chip=new LateGameMeshBuilder();chip.Tube(new[]{Vector3.zero,Vector3.up*.14f},new[]{.045f,0f},i==1?cream:gold,3);
   c.fragments[i]=Render(chip,"Chip"+i,c.visual,false).transform;
   float angle=i*Mathf.PI*2/3;c.fragmentVelocities[i]=new Vector3(Mathf.Cos(angle)*1.4f,1.7f,Mathf.Sin(angle)*1.4f);
  }
  Save(root,LateGameSlot.AntennaContact,LateGameVariant.Overflow,.12f);
  string path=Folder+"/OverflowFup.wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==LateGameSound.Overflow&&x.variant==LateGameVariant.Overflow);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=LateGameSound.Overflow,variant=LateGameVariant.Overflow,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.32f,repeatSeconds=.12f,voices=1});
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();OverflowArtVerification.RunBatch();
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
