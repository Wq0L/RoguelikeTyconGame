using Geo = LateGameMeshBuilder;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Original authored geometry. Editor-only, repeatable; no downloaded meshes, textures or runtime fracture.
public static class ThornArtBuilder
{
    const string Folder = "Assets/Art/LateGame/ThornSiege";
    static Material body, ink;
    static readonly Color Bark = new Color32(173, 91, 48, 255), LightBark = new Color32(220, 145, 66, 255),
        DarkBark = new Color32(93, 47, 52, 255), Tip = new Color32(255, 218, 138, 255), Soil = new Color32(92, 63, 54, 255),
        Leaf = new Color32(155, 171, 66, 255), Crack = new Color32(40, 27, 44, 255);

    public static void BuildRootBatch() { BuildRoot(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
    [MenuItem("Tools/Art/Build Thorn Siege Root")]
    public static void BuildRoot()
    {
        Init();
        var root = New("ThornRoot", out var content);
        var mesh = new Geo();
        mesh.Tube(new[] { new Vector3(0,0,0), new Vector3(-.1f,.5f,.03f), new Vector3(.06f,1.2f,0), new Vector3(-.04f,1.8f,0), new Vector3(.04f,2.12f,0) }, new[] {.27f,.32f,.61f,.56f,.23f}, Bark, 7);
        // Asymmetric crown, hooked rather than a regular crystal/post silhouette.
        mesh.Tube(new[] {new Vector3(-.3f,1.8f,0),new Vector3(-.62f,2.25f,.04f),new Vector3(-.43f,2.7f,.1f)},new[]{.22f,.17f,0f},Tip,5);
        mesh.Tube(new[] {new Vector3(.28f,1.8f,.1f),new Vector3(.57f,2.16f,.1f),new Vector3(.8f,2.24f,.03f)},new[]{.24f,.14f,0f},LightBark,5);
        mesh.Tube(new[] {new Vector3(.03f,1.95f,-.29f),new Vector3(.08f,2.31f,-.49f),new Vector3(-.1f,2.5f,-.44f)},new[]{.17f,.11f,0f},Tip,5);
        for(int i=0;i<5;i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            Vector3 d = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            mesh.Tube(new[]{Vector3.up*.65f,d*.43f+Vector3.up*.18f,d*.72f+Vector3.up*.08f},new[]{.16f,.12f,0f},DarkBark,5);
        }
        Render(mesh,"RootBody", content.visual);
        var stages = new List<GameObject>();
        for(int stage=0;stage<3;stage++)
        {
            var g=new Geo();
            for(int side=0;side<3;side++)
            {
                float a = side*2*Mathf.PI/3 + stage*.63f;
                Vector3 d = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)), tangent = new Vector3(-d.z,0,d.x);
                Vector3 p0=d*.555f+Vector3.up*1.75f;
                Vector3 p1=d*.615f+tangent*.10f+Vector3.up*1.49f;
                Vector3 p2=d*.56f-tangent*.07f+Vector3.up*1.2f;
                g.Ribbon(p0,p1,.055f,Crack); g.Ribbon(p1,p2,.047f,Crack);
                g.Ribbon(p0+d*.009f,p1+d*.009f,.017f,Tip);
            }
            stages.Add(Render(g,"CrackStage"+stage,content.visual,false));
        }
        content.damageStages=stages.ToArray();
        content.hideSeconds=.36f;content.emergeDepth=.48f;
        var top=new GameObject("ImpactPoint");top.transform.SetParent(root.transform,false);top.transform.localPosition=Vector3.up*1.224f;content.topPoint=top.transform;
        Bind(LateGameSlot.ThornRoot,Save(root,"ThornRoot"));
        AssetDatabase.SaveAssets();
    }

    public static void BuildAllBatch() { BuildRoot(); BuildParts(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
    [MenuItem("Tools/Art/Build Thorn Siege Parts")]
    public static void BuildParts()
    {
        Init();
        var piece=New("ThornPiece",out var content);
        var g=new Geo();
        // Three reusable hook shapes in one combined mesh, no per-spike renderer.
        for(int i=0;i<3;i++)
        {
            float a=i*2.1f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            g.Tube(new[]{d*.15f,d*.31f+Vector3.up*(.46f+i*.08f),d*.18f+Vector3.up*(.88f+i*.1f)},new[]{.14f,.11f,0f},i==1?LightBark:DarkBark,5);
        }
        Render(g,"ThornCluster",content.visual);content.hideSeconds=.65f;content.emergeDepth=.28f;Bind(LateGameSlot.ThornPiece,Save(piece,"ThornPiece"));
        var ground=New("GroundHeave",out content);g=new Geo();
        for(int i=0;i<5;i++)
        {
            float a=i*1.2566f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            g.Tube(new[]{d*.2f,d*.26f+Vector3.up*.23f,d*.28f+Vector3.up*.31f},new[]{.19f,.18f,.05f},i%2==0?Soil:DarkBark,5);
            g.Ribbon(d*.14f+Vector3.up*.04f,d*.52f+Vector3.up*.025f,.025f,Crack);
        }
        Render(g,"SoilHeave",content.visual);content.hideSeconds=.45f;content.groundRumble=true;Bind(LateGameSlot.GroundSign,Save(ground,"GroundHeave"));
        // The warning now comes from the ground, not floating canopy symbols.
        Bind(LateGameSlot.OverheadWarning,null);
        Burst("ThornSoilBurst",LateGameSlot.GroundBurst,4,.48f,true);
        Burst("ThornHit",LateGameSlot.HitBurst,5,.27f);
        Burst("ThornBreak",LateGameSlot.BreakBurst,9,.62f);
        var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
        string[] names={"Prepare","Emerge","Hit","Break","Clear"};
        for(int i=0;i<names.Length;i++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/"+names[i]+".wav");
            if(clip==null)throw new Exception("Run Tools/Art/build-thorn-audio.py before the builder");
            art.sounds.RemoveAll(x=>x.sound==(LateGameSound)i&&x.variant==LateGameVariant.Thorn);
            art.sounds.Add(new LateGameArtSO.SoundBinding{sound=(LateGameSound)i,variant=LateGameVariant.Thorn,clip=clip,volume=i==2?.32f:.45f,repeatSeconds=i==2?.065f:.12f,voices=2});
        }
        EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        var report=new List<string>{"Original authored meshes; flat shading duplicates vertices per triangle. Ink hull adds a second draw of the same mesh."};
        foreach(string path in Directory.GetFiles(Folder,"*.prefab"))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
            int triangles=0,vertices=0;foreach(var f in filters){triangles+=f.sharedMesh.triangles.Length/3;vertices+=f.sharedMesh.vertexCount;}
            report.Add(prefab.name+": renderers="+filters.Length+", submitted triangles (including ink / all cracks)="+triangles+", submitted vertices="+vertices);
        }
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/ThornGeometryBudget.txt",report);
    }
    static void Burst(string name,LateGameSlot slot,int count,float lifetime,bool soilOnly=false)
    {
        var root=New(name,out var content);var parts=new List<Transform>();var velocities=new List<Vector3>();
        for(int i=0;i<count;i++)
        {
            var geo=new Geo();bool leaf=!soilOnly && i%3==0;
            geo.Tube(new[]{Vector3.zero,Vector3.up*(leaf?.15f:.09f),Vector3.up*(leaf?.32f:.18f)},new[]{.015f,leaf?.10f:.13f,0f},soilOnly?Soil:leaf?Leaf:i%2==0?Soil:Tip,4);
            var go=Render(geo,name+"Fragment"+(leaf?"Leaf":i%2==0?"Soil":"Wood"),content.visual,false);
            float a=i*2.39996f;go.transform.localPosition=new Vector3(Mathf.Cos(a)*.12f,0,Mathf.Sin(a)*.12f);
            parts.Add(go.transform);velocities.Add(new Vector3(Mathf.Cos(a)*(1f+i*.08f),.65f+i*.1f,Mathf.Sin(a)*(1f+i*.08f)));
        }
        content.fragments=parts.ToArray();content.fragmentVelocities=velocities.ToArray();Bind(slot,Save(root,name),lifetime);
    }

    static void Init()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var shader=Shader.Find("ClickerGame/ThornComicToon"); if(shader==null) throw new Exception("ThornComicToon missing");
        body=Material("ThornToon",shader,false);ink=Material("ThornInk",shader,true);
    }
    static Material Material(string name, Shader shader,bool outline)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.SetFloat("_InkMode",outline?1:0);m.SetFloat("_OutlineWidth",outline?.05f:0f);m.SetFloat("_Cull",outline?1:2);
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject New(string name,out LateGameContent content)
    {
        var root=new GameObject(name);content=root.AddComponent<LateGameContent>();
        var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);content.visual=visual.transform;return root;
    }
    static GameObject Render(Geo geo,string name,Transform parent,bool outlined=true)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var mesh=geo.Mesh(name);
        if(name == "RootBody" || name.StartsWith("CrackStage"))
        { var v=mesh.vertices; for(int i=0;i<v.Length;i++)v[i].y*=.72f;mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds(); }
        string path=Folder+"/"+name+".asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null)AssetDatabase.CreateAsset(mesh,path);else {EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
        AddRenderer(go,mesh,body);
        if(outlined){var hull=new GameObject("Ink");hull.transform.SetParent(go.transform,false);AddRenderer(hull,mesh,ink);}
        return go;
    }
    static void AddRenderer(GameObject go,Mesh mesh,Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }
    static GameObject Save(GameObject root,string name)
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
    }
    static void Bind(LateGameSlot slot,GameObject prefab,float life=0f)
    {
        var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
        art.slots.RemoveAll(s=>s.slot==slot && s.variant==LateGameVariant.Thorn);
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=LateGameVariant.Thorn,prefab=prefab,lifetime=life});EditorUtility.SetDirty(art);
    }
}
