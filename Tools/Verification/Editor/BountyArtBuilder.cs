using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class BountyArtBuilder
{
 const string Folder="Assets/Art/LateGame/WanderingBounty";
 static Material body,ink,light;
 static readonly Color Cream=new Color32(255,246,215,255),Peach=new Color32(245,167,124,255),Stem=new Color32(150,118,103,255);
 public static void BuildAndRunBatch(){Build();BountyBuffVerification.RunBatch();}
 public static void Build()
 {
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  var shader=Shader.Find("ClickerGame/BountyAuroraToon");if(shader==null)throw new System.Exception("Bounty aurora shader missing");
  light=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/BountyLight.mat");if(light==null){light=new Material(shader);AssetDatabase.CreateAsset(light,Folder+"/BountyLight.mat");}
  var tint=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/BountyPlantGold.mat");
  if(tint==null){tint=new Material(Shader.Find("ClickerGame/BountyPlantGold"));AssetDatabase.CreateAsset(tint,"Assets/Resources/BountyPlantGold.mat");}
  tint.shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/BountyPlantGold.shader");EditorUtility.SetDirty(tint);
  Make("BountyCurtain",LateGameSlot.BountyCurtain,4);
  Make("BountyMarker",LateGameSlot.BountyMarker,0);Make("BountyProgress",LateGameSlot.BountyProgress,1);Make("BountyFlow",LateGameSlot.BountyFlow,2);Make("BountyBloom",LateGameSlot.BloomBurst,3);
  var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");art.sounds.RemoveAll(x=>x.variant==LateGameVariant.Bounty);
  Sound(art,LateGameSound.EventPrepared,"Arrive",.27f,.5f);Sound(art,LateGameSound.BountyProgress,"Progress",.24f,.14f);Sound(art,LateGameSound.Bloom,"Bloom",.36f,.4f);
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
 }
 static void Ball(LateGameMeshBuilder g,Vector3 at,float radius,Color c)
 {g.Tube(new[]{at+Vector3.down*radius,at,at+Vector3.up*radius},new[]{radius*.18f,radius,radius*.18f},c,6);}
 static void Flower(LateGameMeshBuilder g,Vector3 at,float size)
 {
  for(int k=0;k<5;k++){float a=k*Mathf.PI*2/5;Ball(g,at+new Vector3(Mathf.Cos(a)*size*.68f,0,Mathf.Sin(a)*size*.68f),size*.55f,Cream);}
  Ball(g,at+Vector3.up*.06f,size*.42f,Peach);
 }
 static void Make(string name,LateGameSlot slot,int mode)
 {
  var root=new GameObject(name);var c=root.AddComponent<LateGameContent>();var v=new GameObject("Visual");v.transform.SetParent(root.transform,false);c.visual=v.transform;
  var b=root.AddComponent<LateGameBounty>();c.bounty=b;b.flow=mode==2;c.hideSeconds=mode==0?.24f:0;c.emergeDepth=mode==0?.12f:0;
  var geo=new LateGameMeshBuilder();
  if(mode==0)
  {
   // A small hovering collection drone: broad body, dark visor, two eyes and side grippers.
   var at=new Vector3(-.40f,1.18f,-.35f);
   geo.Tube(new[]{at-Vector3.up*.12f,at,at+Vector3.up*.16f},new[]{.16f,.26f,.17f},Cream,6);
   geo.Tube(new[]{at+new Vector3(0,0,-.18f),at+new Vector3(0,.04f,-.22f)},new[]{.17f,.15f},new Color32(55,49,70,255),6);
   Ball(geo,at+new Vector3(-.075f,.04f,-.235f),.043f,new Color32(255,212,58,255));
   Ball(geo,at+new Vector3(.075f,.04f,-.235f),.043f,new Color32(255,212,58,255));
   for(int sign=-1;sign<=1;sign+=2)geo.Tube(new[]{at+new Vector3(sign*.18f,0,0),at+new Vector3(sign*.32f,-.06f,0),at+new Vector3(sign*.25f,-.18f,-.05f)},new[]{.045f,.045f,.025f},Peach,4);
   geo.Tube(new[]{at+Vector3.up*.14f,at+Vector3.up*.33f},new[]{.025f,.012f},Peach,4);Ball(geo,at+Vector3.up*.34f,.055f,Cream);
   var scan=LightMesh(name+"Scanner",v.transform,true);b.scan=scan.transform;
   var beamGeo=new LateGameMeshBuilder();beamGeo.Tube(new[]{at-Vector3.up*.11f,new Vector3(0,.18f,0)},new[]{.018f,.042f},new Color(1,.75f,.1f,1),5);
   var beam=Render(beamGeo,name+"RobotRay",v.transform,false);
   beam.GetComponent<MeshRenderer>().sharedMaterial=light;
  }
  else if(mode==4)
  {
   b.aurora=LightMesh(name+"Perimeter",v.transform,false).transform;
  }
  else if(mode==2){for(int i=0;i<3;i++)Ball(geo,new Vector3(i*.10f,i*.07f,0),.065f,Cream);}
  else
  {
   int count=mode==1?3:6;c.fragments=new Transform[count];c.fragmentVelocities=new Vector3[count];
   for(int i=0;i<count;i++)
   {
    var petal=new LateGameMeshBuilder();Ball(petal,Vector3.zero,mode==1?.09f:.12f,i%2==0?Cream:Peach);
    var go=Render(petal,name+"Seed"+(i%2),v.transform,true);float a=i*2.39996f;
    go.transform.localPosition=new Vector3(Mathf.Cos(a)*.2f,.1f,Mathf.Sin(a)*.2f);c.fragments[i]=go.transform;c.fragmentVelocities[i]=new Vector3(Mathf.Cos(a)*.6f,1.4f+(i%2)*.2f,Mathf.Sin(a)*.6f);
   }
  }
  if(mode<1||mode==2){var shape=Render(geo,name+"Mesh",v.transform,true);b.pollen=shape.transform;}
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);
  var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");art.slots.RemoveAll(x=>x.variant==LateGameVariant.Bounty&&x.slot==slot);art.slots.Add(new LateGameArtSO.SlotBinding{variant=LateGameVariant.Bounty,slot=slot,prefab=prefab,lifetime=mode==1?.4f:.85f});EditorUtility.SetDirty(art);
 }
 static GameObject LightMesh(string name,Transform parent,bool scanner)
 {
  var vertices=new System.Collections.Generic.List<Vector3>();var colors=new System.Collections.Generic.List<Color>();var indices=new System.Collections.Generic.List<int>();
  int segments=16;
  for(int i=0;i<=segments;i++)
  {
   float a=i*Mathf.PI*2/segments;float r=.79f;
   Vector2 edge=scanner?new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r):new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.49f/Mathf.Max(Mathf.Abs(Mathf.Cos(a)),Mathf.Abs(Mathf.Sin(a)));
   float low=scanner?-.15f:-.12f, high=scanner?-.095f:1.35f+.22f*Mathf.Sin(a*3);
   vertices.Add(new Vector3(edge.x,low,edge.y));vertices.Add(new Vector3(edge.x,high,edge.y));
   colors.Add(new Color(1f,.66f,.06f,scanner?.68f:.32f));colors.Add(new Color(1f,.92f,.38f,scanner?.68f:0f));
   if(i<segments){int n=i*2;indices.AddRange(new[]{n,n+1,n+3,n,n+3,n+2});}
  }
  var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
  string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
  var go=new GameObject(name);go.transform.SetParent(parent,false);Draw(go,mesh,light);return go;
 }
 static GameObject Render(LateGameMeshBuilder g,string name,Transform parent,bool outline)
 {
  var mesh=g.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
  var go=new GameObject(name);go.transform.SetParent(parent,false);Draw(go,mesh,body);
  if(outline){var child=new GameObject("Ink");child.transform.SetParent(go.transform,false);Draw(child,mesh,ink);}return go;
 }
 static void Draw(GameObject go,Mesh mesh,Material material){go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
 static void Sound(LateGameArtSO art,LateGameSound sound,string name,float volume,float interval)
 {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/"+name+".wav");if(clip==null)throw new System.Exception(name+" audio missing");art.sounds.Add(new LateGameArtSO.SoundBinding{variant=LateGameVariant.Bounty,sound=sound,clip=clip,volume=volume,repeatSeconds=interval,voices=1});}
}
