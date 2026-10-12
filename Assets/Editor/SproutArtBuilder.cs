using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Original low-poly sprout art. Reuses Thorn's authored toon materials/soil without rewriting them.
public static class SproutArtBuilder
{
    const string Folder="Assets/Art/LateGame/InvasiveSprout", Shared="Assets/Art/LateGame/ThornSiege";
    static Material body,ink;
    static readonly Color Teal=new Color32(61,190,177,255), Light=new Color32(155,229,195,255),
        Stem=new Color32(39,95,91,255), Wilt=new Color32(118,106,71,255), Heart=new Color32(185,67,114,255),
        Cream=new Color32(255,218,139,255), Dark=new Color32(47,31,53,255);
    public static void BuildBatch(){Build();EditorApplication.Exit(0);}
    [MenuItem("Tools/Art/Build Invasive Sprout")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        body=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/ThornToon.mat");ink=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/ThornInk.mat");
        if(body==null||ink==null)throw new Exception("Shared Thorn toon materials missing");
        Plant(true);Plant(false);Burst("SproutHit",LateGameSlot.HitBurst,4,.3f);Burst("SproutBreak",LateGameSlot.BreakBurst,8,.62f);
        Bind(LateGameSlot.GroundSign,AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/GroundHeave.prefab"));
        Bind(LateGameSlot.GroundBurst,AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/ThornSoilBurst.prefab"),.48f);
        var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
        art.sounds.RemoveAll(x=>x.variant==LateGameVariant.Sprout);
        Sound(art,LateGameSound.EventPrepared,Shared+"/Audio/Prepare.wav",.32f);
        Sound(art,LateGameSound.EventActivated,Folder+"/Audio/Unfurl.wav",.4f);
        Sound(art,LateGameSound.TargetHit,Folder+"/Audio/LeafHit.wav",.35f);
        Sound(art,LateGameSound.TargetBroken,Shared+"/Audio/Break.wav",.3f);
        Sound(art,LateGameSound.EventResolved,Folder+"/Audio/Wilt.wav",.42f);
        Sound(art,LateGameSound.Spread,Folder+"/Audio/Spread.wav",.3f);
        EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        var report=new List<string>{"Triangles include ink hull and all closed damage stages; leaves use shared meshes/materials."};
        foreach(string path in Directory.GetFiles(Folder,"*.prefab"))
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);int tris=0,vertices=0;var filters=p.GetComponentsInChildren<MeshFilter>(true);
            foreach(var f in filters){tris+=f.sharedMesh.triangles.Length/3;vertices+=f.sharedMesh.vertexCount;}
            report.Add(p.name+": renderers="+filters.Length+", submitted triangles="+tris+", submitted vertices="+vertices);
        }
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/SproutGeometryBudget.txt",report);
    }
    static void Plant(bool main)
    {
        string name=main?"SproutMain":"SproutPiece";
        var root=New(name,out var content);var geo=new LateGameMeshBuilder();
        float h=main?1.25f:.20f;
        geo.Tube(new[]{Vector3.zero,new Vector3(-.1f,h*.45f,0),new Vector3(.04f,h,0)},new[]{main?.2f:.09f,main?.17f:.07f,main?.21f:.08f},Stem,6);
        if(main)
        {
            geo.Tube(new[]{new Vector3(.04f,h,0),new Vector3(.04f,h+.24f,0),new Vector3(.01f,h+.5f,0)},new[]{.17f,.32f,.08f},Heart,7);
            geo.Tube(new[]{new Vector3(.01f,h+.42f,0),new Vector3(.01f,h+.55f,0)},new[]{.1f,0f},Cream,5);
        }
        Render(geo,name+"Body",content.visual);
        var petal=root.AddComponent<LateGamePetals>();content.petals=petal;
        petal.openSeconds=main?.8f:.5f;petal.breatheDegrees=main?4f:1.3f;
        int count=main?4:2;petal.leaves=new Transform[count];
        for(int i=0;i<count;i++)
        {
            geo=new LateGameMeshBuilder();geo.Leaf(main?1f:.46f,main?.36f:.18f,i%2==0?Teal:Light,Wilt);
            var leaf=Render(geo,name+"Leaf"+(i%2),content.visual);
            leaf.transform.localPosition=new Vector3(.04f,h,0);
            leaf.transform.localRotation=Quaternion.Euler(0f,main?i*90f+25f:i*150f+15f,0f);
            petal.leaves[i]=leaf.transform;
        }
        content.emergeDepth=main?.35f:.16f;content.hideSeconds=main?.4f:.55f;
        if(main)
        {
            var cracks=new List<GameObject>();
            for(int stage=0;stage<3;stage++)
            {
                geo=new LateGameMeshBuilder();
                for(int k=0;k<3;k++)
                {
                    float a=k*2.094f+stage*.55f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    geo.Ribbon(d*.31f+Vector3.up*(h+.21f),d*.24f+Vector3.up*(h+.38f),.035f,Dark);
                }
                cracks.Add(Render(geo,"SproutScar"+stage,content.visual,false));
            }
            content.damageStages=cracks.ToArray();
        }
        var top=new GameObject("ImpactPoint");top.transform.SetParent(root.transform,false);top.transform.localPosition=Vector3.up*(h+.25f);content.topPoint=top.transform;
        Bind(main?LateGameSlot.SproutMain:LateGameSlot.SproutPiece,Save(root,name));
    }
    static void Burst(string name,LateGameSlot slot,int count,float life)
    {
        var root=New(name,out var content);content.fragments=new Transform[count];content.fragmentVelocities=new Vector3[count];
        for(int i=0;i<count;i++)
        {
            var geo=new LateGameMeshBuilder();geo.Leaf(.22f,.08f,i%2==0?Teal:Heart,Wilt);
            var leaf=Render(geo,name+"Leaf"+(i%2),content.visual,false);float a=i*2.39996f;
            leaf.transform.localPosition=new Vector3(Mathf.Cos(a)*.12f,0,Mathf.Sin(a)*.12f);
            content.fragments[i]=leaf.transform;content.fragmentVelocities[i]=new Vector3(Mathf.Cos(a),.7f+i*.09f,Mathf.Sin(a));
        }
        Bind(slot,Save(root,name),life);
    }
    static GameObject New(string name,out LateGameContent content)
    {
        var root=new GameObject(name);content=root.AddComponent<LateGameContent>();var v=new GameObject("Visual");v.transform.SetParent(root.transform,false);content.visual=v.transform;return root;
    }
    static GameObject Render(LateGameMeshBuilder geo,string name,Transform parent,bool outline=true)
    {
        var mesh=geo.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
        var go=new GameObject(name);go.transform.SetParent(parent,false);Renderer(go,mesh,body);
        if(outline){var hull=new GameObject("Ink");hull.transform.SetParent(go.transform,false);Renderer(hull,mesh,ink);}return go;
    }
    static void Renderer(GameObject go,Mesh mesh,Material material)
    {go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.allowOcclusionWhenDynamic=false;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
    static GameObject Save(GameObject root,string name)
    {var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);return prefab;}
    static void Bind(LateGameSlot slot,GameObject prefab,float life=0f)
    {
        if(prefab==null)throw new Exception("Missing shared content for "+slot);
        var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");art.slots.RemoveAll(x=>x.slot==slot&&x.variant==LateGameVariant.Sprout);
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=LateGameVariant.Sprout,prefab=prefab,lifetime=life});EditorUtility.SetDirty(art);
    }
    static void Sound(LateGameArtSO art,LateGameSound sound,string path,float volume)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(clip==null)throw new Exception("Missing audio "+path);
        art.sounds.Add(new LateGameArtSO.SoundBinding{sound=sound,variant=LateGameVariant.Sprout,clip=clip,volume=volume,repeatSeconds=.08f,voices=2});
    }
}

