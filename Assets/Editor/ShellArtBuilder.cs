using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class ShellArtBuilder
{
    const string Folder="Assets/Art/LateGame/GraftedShell",Shared="Assets/Art/LateGame/ThornSiege";
    static Material body,ink,rim;
    static readonly Color Bark=new Color32(120,69,64,255), Pale=new Color32(255,231,175,255), Dark=new Color32(47,31,53,255);
    public static void BuildBatch(){Build();EditorApplication.Exit(0);}
    [MenuItem("Tools/Art/Build Grafted Shell")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        body=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/ThornToon.mat");ink=AssetDatabase.LoadAssetAtPath<Material>(Shared+"/ThornInk.mat");
        rim=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/Attention/TargetRim.mat");
        for(int z=0;z<2;z++)
        {
            Plant(z,true);Plant(z,false);
            Burst(z,LateGameSlot.GroundBurst,"Breath",6);
            Burst(z,LateGameSlot.HitBurst,"Rustle",6);
            Burst(z,LateGameSlot.BreakBurst,"Shatter",9);
            var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
            var v=z==0?LateGameVariant.ShellA:LateGameVariant.ShellB;art.sounds.RemoveAll(x=>x.variant==v);
            Sound(art,v,LateGameSound.EventPrepared,"Prepare",.3f);Sound(art,v,LateGameSound.ShellChoice,"Choice",.44f);
            Sound(art,v,LateGameSound.ShellClose,"Close",.38f);Sound(art,v,LateGameSound.ShellOpen,"Open",.38f);
            SharedSound(art,v,LateGameSound.TargetHit,"Hit",.35f);SharedSound(art,v,LateGameSound.TargetBroken,"Break",.32f);
            EditorUtility.SetDirty(art);
        }
        AssetDatabase.SaveAssets();
    }
    // Original mesh construction inspired by the user's warm husk / cold cocoon reference.
    static Color Tone(int z)=>z==0?new Color32(255,183,35,255):new Color32(83,220,255,255);
    static Color Wood(int z)=>z==0?new Color32(161,95,35,255):new Color32(76,62,142,255);
    static Vector3 Rad(float a,float radius,float height)=>new Vector3(Mathf.Cos(a)*radius,height,Mathf.Sin(a)*radius);
    static void Leaf(LateGameMeshBuilder into,Vector3 at,float yaw,float tilt,float length,float width,Color face,Color back)
    {
        var leaf=new LateGameMeshBuilder();leaf.Leaf(length,width,face,back);
        into.Append(leaf,Matrix4x4.TRS(at,Quaternion.Euler(tilt,yaw,0),Vector3.one));
    }
    static void Plant(int zone,bool target)
    {
        string name=(target?"ShellNode":"ShellPiece")+(zone==0?"A":"B");Color color=Tone(zone);
        var root=new GameObject(name);var content=root.AddComponent<LateGameContent>();
        var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);content.visual=visual.transform;
        var shell=root.AddComponent<LateGameShell>();content.shell=shell;shell.zone=zone;shell.target=target;
        content.hideSeconds=target?.5f:.68f;content.emergeDepth=target?.35f:.12f;
        var geo=new LateGameMeshBuilder();
        if(target)
        {
            // A is wide and cupped, B tall and sharply faceted. The cores are never the same mesh.
            geo.Tube(new[]{Vector3.up*.10f,Vector3.up*.52f,Vector3.up*(zone==0?.95f:1.14f),new Vector3(zone==0?.10f:-.12f,zone==0?1.5f:1.8f,0)},
                zone==0?new[]{.20f,.43f,.30f,.005f}:new[]{.18f,.34f,.28f,.005f},color,zone==0?7:5);
            if(zone==0)geo.Tube(new[]{new Vector3(.04f,.4f,-.23f),new Vector3(.04f,.78f,-.27f),new Vector3(.13f,1.20f,-.09f)},new[]{.10f,.19f,.005f},Pale,5);
            else for(int k=0;k<3;k++){float a=k*2.094f;geo.Tube(new[]{Rad(a,.30f,.14f),Rad(a,.43f,.56f),Rad(a,.52f,1.05f+k*.08f)},new[]{.13f,.17f,.005f},color,4);}
            Render(geo,name+"Core",content.visual,true,true);
            geo=new LateGameMeshBuilder();
            for(int k=0;k<7;k++)Leaf(geo,Vector3.up*.07f,k*51+15,-7, .72f+(k%2)*.15f,.19f,new Color32(82,147,54,255),new Color32(38,83,54,255));
            Render(geo,name+"Rosette",content.visual,true,false);
            shell.shutters=new Transform[zone==0?5:3];
            for(int k=0;k<shell.shutters.Length;k++)
            {
                geo=new LateGameMeshBuilder();float a=k*Mathf.PI*2/shell.shutters.Length;
                // Folded bark blades: cream outside A, purple ice sheath outside B.
                Leaf(geo,Vector3.zero,0,-63,zone==0?1.25f:1.40f,zone==0?.32f:.22f,zone==0?Pale:Wood(zone),Wood(zone));
                geo.Tube(new[]{Vector3.zero,new Vector3(0,.53f,.29f),new Vector3(0,1.13f,.43f)},new[]{.045f,.055f,.005f},zone==0?Wood(zone):color,4);
                var blade=Render(geo,name+"Blade",content.visual,true,false);
                blade.transform.localPosition=Rad(a,.23f,.12f);blade.transform.localRotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);shell.shutters[k]=blade.transform;
            }
            var stages=new GameObject[3];
            for(int st=0;st<3;st++)
            {
                geo=new LateGameMeshBuilder();
                for(int k=0;k<3;k++){float a=k*2.094f+st*.5f;geo.Tube(new[]{Rad(a,.43f,.40f+st*.12f),Rad(a+.16f,.45f,.53f+st*.12f),Rad(a-.08f,.38f,.68f+st*.12f)},new[]{.025f,.034f,.012f},Dark,3);}
                stages[st]=Render(geo,name+"Crack"+st,content.visual,false,false);
            }
            content.damageStages=stages;
        }
        else
        {
            // Two incomplete organic arcs surround the pot, never a square cell frame or a solid lid.
            shell.shutters=new Transform[2];
            for(int k=0;k<2;k++)
            {
                geo=new LateGameMeshBuilder();var points=new Vector3[8];var radii=new float[8];
                for(int j=0;j<8;j++){float a=k*Mathf.PI+j*.39f;points[j]=Rad(a,.81f+.035f*Mathf.Sin(j*2),.08f+j*.065f);radii[j]=Mathf.Lerp(.085f,.025f,j/7f);}
                geo.Tube(points,radii,Wood(zone),5);
                var light=new Vector3[8];for(int j=0;j<8;j++)light[j]=points[j]+Vector3.up*.046f;
                geo.Tube(light,new[]{.036f,.046f,.046f,.046f,.046f,.046f,.035f,.006f},color,4);
                for(int j=1;j<8;j+=2)
                {
                    float a=k*Mathf.PI+j*.39f;
                    if(zone==0)Leaf(geo,points[j],-90-a*Mathf.Rad2Deg,-28,.29f,.10f,color,Wood(zone));
                    else geo.Tube(new[]{points[j],points[j]+Rad(a,.025f,.17f),points[j]+Rad(a,.065f,.31f)},new[]{.075f,.08f,.002f},color,4);
                }
                var vine=Render(geo,name+"Vine"+k,content.visual,true,false);shell.shutters[k]=vine.transform;
            }
        }
        geo=new LateGameMeshBuilder();foreach(var polygon in ShellMotifs.Polygons(zone))geo.Prism(polygon,.025f,zone==0?Pale:Tone(zone));
        var emblem=Render(geo,name+"Emblem",content.visual,true,false);
        emblem.transform.localPosition=target?new Vector3(0,.69f,-.43f):new Vector3(-.60f,.24f,-.58f);
        emblem.transform.localScale=Vector3.one*(target?.39f:.27f);shell.emblem=emblem.transform;
        if(zone==0&&target)
        {
            var holder=new GameObject("DefaultClosureWarning");holder.transform.SetParent(emblem.transform,false);holder.transform.localPosition=new Vector3(1.12f,.66f,-.12f);shell.warning=holder.transform;
            geo=new LateGameMeshBuilder();geo.Tube(new[]{new Vector3(0,-.06f,0),new Vector3(0,.25f,0)},new[]{.10f,.10f},new Color32(255,71,84,255),4);
            geo.Tube(new[]{new Vector3(0,-.22f,0),new Vector3(0,-.16f,0)},new[]{.10f,.10f},Pale,4);Render(geo,name+"Warning",holder.transform,true,false);
        }
        var top=new GameObject("ImpactPoint");top.transform.SetParent(root.transform,false);top.transform.localPosition=Vector3.up;content.topPoint=top.transform;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);Bind(zone,target?LateGameSlot.ShellNode:LateGameSlot.ShellCover,prefab);
    }
    static void Burst(int zone,LateGameSlot slot,string suffix,int count)
    {
        string name="Shell"+(zone==0?"A":"B")+suffix;var root=new GameObject(name);var content=root.AddComponent<LateGameContent>();
        var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);content.visual=visual.transform;
        content.fragments=new Transform[count];content.fragmentVelocities=new Vector3[count];
        for(int i=0;i<count;i++)
        {
            var geo=new LateGameMeshBuilder();
            if(i%3==0)geo.Tube(new[]{Vector3.down*.08f,Vector3.zero,Vector3.up*.09f},new[]{.05f,.14f,.04f},zone==0?Pale:new Color32(175,234,245,255),5);
            else if(i%3==1)Leaf(geo,Vector3.zero,0,0,.30f,.10f,Tone(zone),Wood(zone));
            else geo.Tube(new[]{Vector3.zero,new Vector3(.04f,.13f,0),new Vector3(.12f,.24f,0)},new[]{.035f,.045f,.01f},Wood(zone),4);
            var piece=Render(geo,name+"Fragment"+(i%3),visual.transform,i%3!=0,false);float a=i*2.39996f;
            piece.transform.localPosition=Rad(a,.3f,.02f);content.fragments[i]=piece.transform;content.fragmentVelocities[i]=Rad(a,.65f+i*.08f,1.1f+(i%3)*.4f);
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);Bind(zone,slot,prefab,.62f);
    }
    static GameObject Render(LateGameMeshBuilder geo,string name,Transform parent,bool outline,bool attention)
    {
        var mesh=geo.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
        var go=new GameObject(name);go.transform.SetParent(parent,false);Mesh(go,mesh,body);
        if(outline){var child=new GameObject("Ink");child.transform.SetParent(go.transform,false);Mesh(child,mesh,ink);}
        if(attention){var child=new GameObject("AttentionRim");child.transform.SetParent(go.transform,false);Mesh(child,mesh,rim);}
        return go;
    }
    static void Mesh(GameObject go,Mesh mesh,Material mat)
    {go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.allowOcclusionWhenDynamic=false;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
    static void Bind(int z,LateGameSlot slot,GameObject prefab,float lifetime=0)
    {var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");var v=z==0?LateGameVariant.ShellA:LateGameVariant.ShellB;art.slots.RemoveAll(x=>x.variant==v&&x.slot==slot);art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=v,prefab=prefab,lifetime=lifetime});EditorUtility.SetDirty(art);}
    static void Sound(LateGameArtSO art,LateGameVariant v,LateGameSound sound,string name,float volume)=>Clip(art,v,sound,Folder+"/Audio/"+name+".wav",volume);
    static void SharedSound(LateGameArtSO art,LateGameVariant v,LateGameSound sound,string name,float volume)=>Clip(art,v,sound,Shared+"/Audio/"+name+".wav",volume);
    static void Clip(LateGameArtSO art,LateGameVariant v,LateGameSound sound,string path,float volume)
    {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(clip==null)throw new Exception(path);art.sounds.Add(new LateGameArtSO.SoundBinding{variant=v,sound=sound,clip=clip,volume=volume,repeatSeconds=.08f,voices=2});}
}



