using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class ReactorArtBuilder
{
 const string Folder="Assets/Art/LateGame/RareReactor";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  Color purple=new Color32(167,95,222,255),gold=new Color32(255,200,79,255);
  var root=Root("ReactorCore",out var c);c.hideSeconds=.12f;
  var g=new LateGameMeshBuilder();
  // Layered closed diamonds: explicit dark border, avoiding inverted hull overlap on a thin sign.
  var outer=new LateGameMeshBuilder();outer.Tube(new[]{new Vector3(0,0,.02f),new Vector3(0,0,-.02f)},new[]{.33f,.33f},new Color32(35,21,44,255),4);
  g.Append(outer,Matrix4x4.Scale(new Vector3(1,1.5f,1)));
  var face=new LateGameMeshBuilder();face.Tube(new[]{new Vector3(0,0,-.04f),new Vector3(0,0,-.07f)},new[]{.27f,.27f},purple,4);
  g.Append(face,Matrix4x4.Scale(new Vector3(1,1.5f,1)));
  var core=new LateGameMeshBuilder();core.Tube(new[]{new Vector3(0,0,-.08f),new Vector3(0,0,-.1f)},new[]{.13f,.13f},gold,4);
  g.Append(core,Matrix4x4.Scale(new Vector3(1,1.8f,1)));
  Render(g,"ReactorCoreMesh",c.visual,false);Save(root,LateGameSlot.ReactorIndicator,LateGameVariant.Reactor);
  root=Root("ReactorOpening",out c);c.fragments=new Transform[4];c.fragmentVelocities=new Vector3[4];
  for(int i=0;i<4;i++){
   var chip=new LateGameMeshBuilder();chip.Tube(new[]{Vector3.zero,Vector3.up*.18f,Vector3.up*.3f},new[]{0f,.065f,0f},i%2==0?purple:gold,4);
   c.fragments[i]=Render(chip,"ReactorSpark"+i,c.visual,false).transform;
   float a=i*Mathf.PI*.5f;c.fragments[i].localPosition=new Vector3(Mathf.Cos(a)*.2f,0,Mathf.Sin(a)*.2f);
   c.fragmentVelocities[i]=new Vector3(Mathf.Cos(a)*.7f,1.8f,Mathf.Sin(a)*.7f);
  }
  Save(root,LateGameSlot.Pulse,LateGameVariant.Reactor,.24f);
  string path=Folder+"/ReactorCharge.wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==LateGameSound.ReactorOn&&x.variant==LateGameVariant.Reactor);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=LateGameSound.ReactorOn,variant=LateGameVariant.Reactor,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.26f,repeatSeconds=.3f,voices=1});
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();ReactorArtVerification.RunBatch();
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
