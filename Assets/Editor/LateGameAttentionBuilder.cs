using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Optional attention rim around intervention art; never changes the accepted source geometry.
public static class LateGameAttentionBuilder
{
    const string Folder="Assets/Art/LateGame/Attention";
    [MenuItem("Tools/Art/Refresh Event Target Rims")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var main=Material("TargetRim",.115f,new Color32(255,71,84,255));
        var small=Material("PieceRim",.065f,new Color32(222,64,78,255));
        Patch("ThornSiege/ThornRoot",main);Patch("ThornSiege/ThornPiece",small);
        Patch("InvasiveSprout/SproutMain",main);Patch("InvasiveSprout/SproutPiece",small);
        AssetDatabase.SaveAssets();
    }
    static Material Material(string name,float width,Color color)
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("ClickerGame/ThornComicToon"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetFloat("_InkMode",1);mat.SetFloat("_Cull",1);mat.SetFloat("_OutlineWidth",width);mat.SetColor("_Ink",color);
        EditorUtility.SetDirty(mat);return mat;
    }
    static void Patch(string name,Material mat)
    {
        string path="Assets/Art/LateGame/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name!="Ink")continue;
                var parent=filter.transform.parent;var existing=parent.Find("AttentionRim");
                var rim=existing!=null?existing.gameObject:new GameObject("AttentionRim");rim.transform.SetParent(parent,false);
                var mf=rim.GetComponent<MeshFilter>();if(mf==null)mf=rim.AddComponent<MeshFilter>();mf.sharedMesh=filter.sharedMesh;
                var r=rim.GetComponent<MeshRenderer>();if(r==null)r=rim.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
                r.allowOcclusionWhenDynamic=false;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}




