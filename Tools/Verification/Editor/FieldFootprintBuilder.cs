using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Original cell-sized art, preserving approved intervention targets and all event mechanics.
public static class FieldFootprintBuilder
{
 const string Folder="Assets/Art/LateGame/FieldFootprints";
 static Material body,ink;
 static Vector3 Rad(float a,float r,float y)=>new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
 public static void BuildThornAndRun(){Build();ThornArtVerification.RunBatch();}
 // Yalnız izole kopyada: hücre izlerini yeniden kurar ve çıkar (25 Ekim 2026, Filiz pembe halka).
 public static void BuildBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new System.Exception("isolated only");Build();EditorApplication.Exit(0);}
 [MenuItem("Tools/Art/Build Event Cell Footprints")]
 public static void Build()
 {
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  body=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
  ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
  for(int type=0;type<2;type++)
  {
   bool sprout=type==1;GameObject root;LateGameContent content;
   if(sprout)
   {
    root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/LateGame/InvasiveSprout/SproutPiece.prefab"));root.name="SproutPiece";content=root.GetComponent<LateGameContent>();
    foreach(Transform child in content.visual)child.localPosition+=new Vector3(-.48f,.08f,-.48f);
   }
   else
   {
    root=new GameObject("ThornPiece");content=root.AddComponent<LateGameContent>();var v=new GameObject("Visual");v.transform.SetParent(root.transform,false);content.visual=v.transform;
    content.hideSeconds=.65f;content.emergeDepth=.28f;
   }
   var geo=new LateGameMeshBuilder();
   // 25 Ekim 2026 (kullanıcı: turkuaz halka yeşil bitkilerin arasında kayboluyor): Filiz'in hücre işareti pembe — mürdüm gövde,
   // pembe yapraklar; Filiz'in imza rengi, yeşil bitki ve gri saksı arasında net ayrışır. Diken halkası değişmedi.
   Color stem=sprout?new Color32(142,36,87,255):new Color32(151,76,48,255);
   Color tip=sprout?new Color32(242,92,154,255):new Color32(255,207,122,255);
   for(int arc=0;arc<2;arc++)
   {
    var p=new Vector3[7];var r=new float[7];
    for(int j=0;j<7;j++){float a=arc*Mathf.PI+j*.43f;p[j]=Rad(a,.78f+.025f*Mathf.Sin(j*1.9f),.18f+j*.018f);r[j]=Mathf.Lerp(sprout?.075f:.09f,.022f,j/6f);}
    geo.Tube(p,r,stem,5);
    for(int j=1;j<7;j+=2)
    {
     float a=arc*Mathf.PI+j*.43f;
     if(sprout)
     {
      var leaf=new LateGameMeshBuilder();leaf.Leaf(.36f,.15f,tip,new Color32(184,52,110,255));
      geo.Append(leaf,Matrix4x4.TRS(p[j],Quaternion.Euler(-14,-90-a*Mathf.Rad2Deg,0),Vector3.one));
     }
     else
     {
      geo.Tube(new[]{p[j],p[j]+Rad(a,.035f,.23f),p[j]+Rad(a+.3f,-.10f,.38f)},new[]{.10f,.075f,0f},tip,4);
     }
    }
   }
   string name=sprout?"SproutRunners":"ThornEncirclingRoots";var mesh=geo.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
   Render(name,content.visual,mesh,body);Render("Ink",content.visual,mesh,ink);
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+root.name+".prefab");Object.DestroyImmediate(root);
   var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");var slot=sprout?LateGameSlot.SproutPiece:LateGameSlot.ThornPiece;var variant=sprout?LateGameVariant.Sprout:LateGameVariant.Thorn;
   art.slots.RemoveAll(x=>x.slot==slot&&x.variant==variant);art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=variant,prefab=prefab});EditorUtility.SetDirty(art);
  }
  AssetDatabase.SaveAssets();
  var lines=new System.Collections.Generic.List<string>{"| Model | Body vertices | Body triangles | With outlines triangles | Renderers |","|---|---:|---:|---:|---:|"};
  foreach(string path in Directory.GetFiles(Folder,"*.prefab"))
  {
   var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);var all=p.GetComponentsInChildren<MeshFilter>(true);var b=all.Where(f=>f.name!="Ink"&&f.name!="AttentionRim");
   lines.Add($"| {p.name} | {b.Sum(f=>f.sharedMesh.vertexCount)} | {b.Sum(f=>f.sharedMesh.triangles.Length/3)} | {all.Sum(f=>f.sharedMesh.triangles.Length/3)} | {all.Length} |");
  }
  Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/FieldFootprintGeometry.md",lines);
 }
 static void Render(string name,Transform parent,Mesh mesh,Material material)
 {var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
}
