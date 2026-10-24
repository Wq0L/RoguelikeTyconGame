using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class HarvestEchoArtBuilder
{
 const string Folder="Assets/Art/LateGame/HarvestEcho";
 static Material body,ink;static LateGameArtSO art;
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  Motif(false);Motif(true);
  string path=Folder+"/EchoTok.wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==LateGameSound.HarvestEcho&&x.variant==LateGameVariant.HarvestEcho);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=LateGameSound.HarvestEcho,variant=LateGameVariant.HarvestEcho,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.28f,repeatSeconds=.12f,voices=1});
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();HarvestEchoArtVerification.RunBatch();
 }
 static void Motif(bool hit)
 {
  string name=hit?"EchoImpact":"EchoPreparation";
  var root=Root(name,out var c);var g=new LateGameMeshBuilder();
  Color purple=hit?new Color32(220,195,255,255):new Color32(171,105,221,255),cream=new Color32(255,245,218,255);
  for(int side=0;side<2;side++){
   var points=new Vector3[6];var width=new float[6];
   for(int i=0;i<6;i++){float a=side*Mathf.PI+(.15f+i*.11f)*Mathf.PI;points[i]=new Vector3(Mathf.Cos(a)*.48f,0,Mathf.Sin(a)*.48f);width[i]=i==0||i==5?.012f:.045f;}
   g.Tube(points,width,purple,4);
   for(int i=0;i<6;i++){points[i]+=Vector3.up*.03f;width[i]*=.25f;}g.Tube(points,width,cream,3);
  }
  Render(g,name+"Motif",c.visual);
  if(hit){
   var star=new LateGameMeshBuilder();
   for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var d=new Vector3(Mathf.Cos(a),.4f,Mathf.Sin(a));star.Tube(new[]{Vector3.zero,d*.18f,d*.33f},new[]{.06f,.04f,0f},cream,3);}
   Render(star,"EchoStar",c.visual,false);
   c.fragments=new Transform[3];c.fragmentVelocities=new Vector3[3];
   for(int i=0;i<3;i++){
    var chip=new LateGameMeshBuilder();chip.Tube(new[]{Vector3.zero,Vector3.up*.12f},new[]{.035f,0f},i==1?cream:purple,3);
    c.fragments[i]=Render(chip,"EchoChip"+i,c.visual,false).transform;
    float a=i*Mathf.PI*2/3;c.fragmentVelocities[i]=new Vector3(Mathf.Cos(a),1.4f,Mathf.Sin(a));
   }
  }
  Save(root,hit?LateGameSlot.Pulse:LateGameSlot.HarvestEchoPreparation,LateGameVariant.HarvestEcho,hit?.16f:.12f);
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
