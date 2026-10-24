using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
// 24 Ekim 2026: bu araç ESKİ hazırlık işaretini / Pulse(Overload) prefab'ını kurar; oyun artık onu kullanmaz.
// Güncel görsel: LateGamePresenter.Overload.cs (çalışma anında kurulan toon rozet).
public static class OverloadArtBuilder
{
 const string Folder="Assets/Art/LateGame/Overload";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  Build(false);Build(true);
  Sound("OverloadCharge",LateGameSound.OverloadPrepared,.16f,.25f);
  Sound("OverloadRelease",LateGameSound.Overload,.18f,.16f);
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();OverloadArtVerification.RunBatch();
 }
 static void Build(bool discharge)
 {
  var root=Root(discharge?"OverloadRelease":"OverloadCharge",out var c);c.hideSeconds=discharge?0:.09f;
  var pieces=new Transform[3];
  for(int i=0;i<3;i++){
   var g=new LateGameMeshBuilder();
   if(i==2){Diamond(g,.1f,0,new Color32(43,25,38,255));Diamond(g,.073f,-.04f,new Color32(255,248,217,255));}
   else {
    float sign=i==0?-1:1;
    var points=new[]{new Vector3(sign*.24f,-.19f,0),new Vector3(sign*.36f,-.015f,0),new Vector3(sign*.21f,.045f,0),new Vector3(sign*.3f,.23f,0)};
    for(int layer=0;layer<2;layer++){
     var tube=new LateGameMeshBuilder();tube.Tube(points,new[]{.006f,.066f,.06f,.004f},layer==0?new Color32(43,25,38,255):new Color32(255,180,56,255),4);
     g.Append(tube,Matrix4x4.TRS(new Vector3(0,0,-layer*.045f),Quaternion.identity,new Vector3(layer==0?1f:.94f,1,.18f)));
    }
   }
   pieces[i]=Render(g,"OverloadPiece"+i,c.visual,false).transform;
  }
  if(discharge){c.fragments=pieces;c.fragmentVelocities=new[]{Vector3.left*1.5f,Vector3.right*1.5f,Vector3.zero};}
  Save(root,discharge?LateGameSlot.Pulse:LateGameSlot.OverloadPreparation,LateGameVariant.Overload,discharge?.16f:0);
 }
 static void Diamond(LateGameMeshBuilder g,float radius,float z,Color color)
 {
  var face=new LateGameMeshBuilder();
  face.Prism(new[]{new Vector2(0,radius),new Vector2(radius,0),new Vector2(0,-radius),new Vector2(-radius,0)},.01f,color);
  g.Append(face,Matrix4x4.Translate(new Vector3(0,0,z)));
 }
 static void Sound(string name,LateGameSound sound,float volume,float repeat)
 {
  string path=Folder+"/"+name+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var s=importer.defaultSampleSettings;s.loadType=AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=s;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==sound&&x.variant==LateGameVariant.Overload);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=sound,variant=LateGameVariant.Overload,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=volume,repeatSeconds=repeat,voices=1});
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

