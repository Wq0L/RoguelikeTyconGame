using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class ComicUIBuilder
{
    const string Root = "Assets/Art/UI/ComicToon/";
    static ComicUITheme theme;
    static Color Ink = new Color(.055f,.075f,.08f);
    public static void UpdateRoundBatch()
    {
        try{
            theme=AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
            var sprite=Sprite("Grid Tile",65);
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/GridUI.prefab");
            var cellImage=prefab.GetComponent<Image>();cellImage.sprite=sprite;cellImage.type=Image.Type.Simple;cellImage.pixelsPerUnitMultiplier=5;
            var hover=prefab.GetComponent<ComicHoverMotion>()??prefab.AddComponent<ComicHoverMotion>();hover.hoverScale=1.075f;
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/UI/GridUI.prefab");PrefabUtility.UnloadPrefabContents(prefab);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var map=Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
            var mapRect=(RectTransform)map.transform;mapRect.anchorMin=mapRect.anchorMax=mapRect.pivot=new Vector2(.5f,.5f);mapRect.anchoredPosition=new Vector2(0,55);mapRect.sizeDelta=new Vector2(1000,800);
            var so=new SerializedObject(map);var grid=(RectTransform)so.FindProperty("gridContainer").objectReferenceValue;
            grid.anchorMin=grid.anchorMax=grid.pivot=new Vector2(.5f,.5f);grid.anchoredPosition=new Vector2(24,0);grid.localScale=Vector3.one;
            var fitter=grid.GetComponent<ContentSizeFitter>();if(fitter)Object.DestroyImmediate(fitter);
            grid.GetComponent<GridLayoutGroup>().padding=new RectOffset();
            Set(so,"cellSprite",sprite);Set(so,"theme",theme);
            so.FindProperty("emptyColor").colorValue=Color.white;so.FindProperty("lockedColor").colorValue=new Color(.16f,.19f,.21f,1);so.FindProperty("availableSize").vector2Value=new Vector2(840,680);
            foreach(string name in new[]{"Column Headers","Row Headers"}){var previous=map.transform.Find(name);if(previous)Object.DestroyImmediate(previous.gameObject);}
            Set(so,"columnHeaders",Rect("Column Headers",map.transform,Vector2.zero,Vector2.zero));Set(so,"rowHeaders",Rect("Row Headers",map.transform,Vector2.zero,Vector2.zero));so.ApplyModifiedPropertiesWithoutUndo();
            foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                bool next=b.name=="Next Round Button",shop=b.name=="Placment Shop Button",skill=b.name=="Skill Shop Button";if(!next&&!shop&&!skill)continue;
                theme.StyleButton(b,next?"blue":skill?"gold":"green");
                var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(next?0:shop?-475:475,next?-419:-430);r.sizeDelta=new Vector2(next?420:365,next?120:98);
                var label=b.GetComponentInChildren<TMP_Text>(true);label.text=next?"START ROUND":shop?"PLANTER SHOP":"SKILL SHOP";label.fontSizeMin=25;label.fontSizeMax=next?44:32;label.enableAutoSizing=true;
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(next?20:82,12);label.rectTransform.offsetMax=new Vector2(-16,-10);
                var motion=b.GetComponent<ComicHoverMotion>()??b.gameObject.AddComponent<ComicHoverMotion>();motion.hoverScale=next?1.055f:1.045f;motion.tilt=next?0:shop?-.7f:.7f;
                if(next){
                    var states=Palette("Round Cyan",.53f);var visual=b.GetComponent<ComicButtonVisual>();visual.normal=states[0];visual.hover=states[1];visual.pressed=states[2];visual.disabled=states[3];((Image)b.targetGraphic).material=states[0];
                }else{
                    var old=b.transform.Find("Action Icon");if(old)Object.DestroyImmediate(old.gameObject);
                    var icon=Rect("Action Icon",b.transform,new Vector2(-135,3),new Vector2(62,62)).gameObject.AddComponent<ComicActionIcon>();icon.shape=shop?ComicActionIcon.Shape.Cart:ComicActionIcon.Shape.Star;icon.raycastTarget=false;
                }
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ROUND_COMIC_BUILD_PASS");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void UpdateShopBatch()
    {
        try {
            theme=AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
            FixPlanterUnlockData();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            theme.ironSprite=RenderResource("Iron", "Iron_Bar");
            theme.stoneSprite=RenderResource("Stone", "Stone_Chunks_Small");
            EditorUtility.SetDirty(theme);
            Shop(Object.FindFirstObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include));
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("PLANTER_SHOP_UPDATE_PASS");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void FixPlanterUnlockData()
    {
        foreach(string size in new[]{"1x1","1x2","1x3","2x2","2x3"}){
            var data=AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter "+size+".asset");
            data.requiredUnlock=size=="1x3"?UnlockType.Planter_1x3:size=="2x2"?UnlockType.Planter_2x2:size=="2x3"?UnlockType.Planter_2x3:UnlockType.None;
            EditorUtility.SetDirty(data);
            if(data.requiredUnlock!=UnlockType.None){
                var node=AssetDatabase.LoadAssetAtPath<SkillNodeSO>("Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/"+size.Replace("x","×")+" Saksı.asset");
                node.unlockType=data.requiredUnlock;EditorUtility.SetDirty(node);
            }
        }
        AssetDatabase.SaveAssets();
    }
    static Sprite RenderResource(string name,string mesh)
    {
        string path=Root+name+".png";
        if(!File.Exists(path)){
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Assets/KayKit_ResourceBits_1.0_FREE/Assets/fbx/"+mesh+".fbx"));
            model.transform.position=new Vector3(10000,0,0);
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            var mats=new System.Collections.Generic.List<Material>();
            foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.sharedMaterials=r.sharedMaterials.Select(original=>{
                var m=new Material(Shader.Find("Simple Toon/SToon Default"));m.mainTexture=original.mainTexture;m.SetFloat("_AmbientCol",.35f);m.SetFloat("_OtlWorldWidth",.012f);mats.Add(m);return m;
            }).ToArray();}
            var go=new GameObject("Resource preview camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;
            camera.orthographicSize=bounds.size.magnitude*.62f;camera.transform.position=bounds.center+new Vector3(6,8,-10).normalized*20;camera.transform.LookAt(bounds.center);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.allowHDR=false;camera.GetUniversalAdditionalCameraData().SetRenderer(1);
            var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);target.Create();var old=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
                var image=new Texture2D(512,512,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            }finally{RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(go);Object.DestroyImmediate(model);foreach(var m in mats)Object.DestroyImmediate(m);}
            AssetDatabase.ImportAsset(path);
        }
        return Sprite(name,0);
    }
    [MenuItem("Tools/Comic UI/Apply saved theme")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.Refresh();
        theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
        if (!theme) { theme=ScriptableObject.CreateInstance<ComicUITheme>(); AssetDatabase.CreateAsset(theme,"Assets/Resources/ComicUITheme.asset"); }
        theme.buttonSprite=Sprite("Button",100); theme.cardSprite=Sprite("Card",70);
        theme.coinSprite=Sprite("Coin",0); theme.sproutSprite=Sprite("Sprout",0);
        theme.bodyFont=Font("Barlow-SemiBold"); theme.headingFont=Font("LilitaOne-Regular");
        theme.headingFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>{theme.bodyFont};
        EditorUtility.SetDirty(theme.headingFont);
        theme.outlinedText=AssetDatabase.LoadAssetAtPath<Material>(Root+"Heading Outline.mat");
        if (!theme.outlinedText) { theme.outlinedText=new Material(theme.headingFont.material); AssetDatabase.CreateAsset(theme.outlinedText,Root+"Heading Outline.mat"); }
        theme.outlinedText.SetColor("_OutlineColor",Color.black); theme.outlinedText.SetFloat("_OutlineWidth",.18f); theme.outlinedText.SetFloat("_FaceDilate",.25f);
        theme.outlinedText.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(theme.outlinedText);
        theme.green=Palette("Green",-1); theme.red=Palette("Red",.005f); theme.blue=Palette("Blue",.6f); theme.gold=Palette("Gold",.11f);
        EditorUtility.SetDirty(theme);
        foreach(string scenePath in new[]{"Assets/Scenes/GameScene.unity","Assets/Scenes/MenuScene.unity"})
        {
            var scene=EditorSceneManager.OpenScene(scenePath);
            foreach(var root in scene.GetRootGameObjects()) ThemeTree(root);
            var shop=Object.FindFirstObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
            if(shop) Shop(shop);
            EditorSceneManager.SaveScene(scene);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/UI"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var prefab=PrefabUtility.LoadPrefabContents(path);
            ThemeTree(prefab); PrefabUtility.SaveAsPrefabAsset(prefab,path); PrefabUtility.UnloadPrefabContents(prefab);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("COMIC_UI_BUILD_PASS");
    }
    public static void BuildBatch() { try {Build(); EditorApplication.Exit(0);} catch(Exception e){Debug.LogException(e); EditorApplication.Exit(1);} }
    static Sprite Sprite(string name,int border)
    {
        string path=Root+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.isReadable=true; importer.alphaIsTransparency=true; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        importer.SaveAndReimport();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels=tex.GetPixels32(); int x0=tex.width,y0=tex.height,x1=0,y1=0;
        for(int y=0;y<tex.height;y++) for(int x=0;x<tex.width;x++) if(pixels[y*tex.width+x].a>20){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
        string asset=Root+name+" Sprite.asset";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(asset);
        if(sprite) return sprite;
        sprite=UnityEngine.Sprite.Create(tex,new Rect(x0,y0,x1-x0+1,y1-y0+1),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
        sprite.name=name; AssetDatabase.CreateAsset(sprite,asset); return sprite;
    }
    static TMP_FontAsset Font(string name)
    {
        string path=Root+"Fonts/"+name+" SDF.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if(font) return font;
        font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/"+name+".ttf"),90,12,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        font.name=name+" SDF"; AssetDatabase.CreateAsset(font,path);
        font.TryAddCharacters(string.Concat(Enumerable.Range(32,95).Select(c=>(char)c))+"ÇçĞğİıÖöŞşÜü×",out string missing);
        foreach(var atlas in font.atlasTextures) {atlas.name=name+" Atlas"; AssetDatabase.AddObjectToAsset(atlas,font);}
        font.material.name=name+" Material"; AssetDatabase.AddObjectToAsset(font.material,font);
        EditorUtility.SetDirty(font); return font;
    }
    static Sprite TrimPreview(Sprite source,string name)
    {
        string asset=Root+"Planter "+name+" Sprite.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Sprite>(asset);if(existing)return existing;
        var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.texture)) as TextureImporter;
        if(importer&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        var tex=source.texture;var rect=source.rect;var pixels=tex.GetPixels32();
        int x0=(int)rect.xMax,y0=(int)rect.yMax,x1=(int)rect.x,y1=(int)rect.y;
        for(int y=(int)rect.y;y<rect.yMax;y++)for(int x=(int)rect.x;x<rect.xMax;x++)if(pixels[y*tex.width+x].a>30){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
        var sprite=UnityEngine.Sprite.Create(tex,new Rect(x0,y0,x1-x0+1,y1-y0+1),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        sprite.name="Planter "+name;AssetDatabase.CreateAsset(sprite,asset);return sprite;
    }
    static Sprite RenderPlanter(PlanterSO data,Sprite fallback)
    {
        if(!data.prefab)return fallback;
        string name="Preview "+data.sizeX+"x"+data.sizeZ;
        string path=Root+name+".png";
        if(!File.Exists(path)){
            var model=Object.Instantiate(data.prefab);model.name="Temporary UI preview";
            model.GetComponent<GhostController>()?.SetGhostMode(false);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            model.transform.position=new Vector3(10000,0,0);model.transform.rotation=Quaternion.identity;
            var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var go=new GameObject("Temporary preview camera");var camera=go.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.z)*.76f;
            camera.transform.position=bounds.center+new Vector3(6,8,-10).normalized*20;camera.transform.LookAt(bounds.center);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.allowHDR=false;
            camera.GetUniversalAdditionalCameraData().SetRenderer(1);
            var target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32);target.Create();
            var old=RenderTexture.active;
            try{
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;var image=new Texture2D(768,768,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            }finally{RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(go);Object.DestroyImmediate(model);}
            AssetDatabase.ImportAsset(path);
        }
        return Sprite(name,0);
    }
    static Material[] Palette(string name,float hue)
    {
        var result=new Material[4]; string[] states={"Normal","Hover","Pressed","Locked"};
        for(int i=0;i<4;i++) {
            string path=Root+name+" "+states[i]+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(!m){m=new Material(Shader.Find("UI/ComicToon"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_Hue",0);m.SetFloat("_TargetHue",hue);m.SetFloat("_Saturation",i==3?0:1);m.SetFloat("_Brightness",i==1?1.2f:i==2?.68f:i==3?.7f:1);
            EditorUtility.SetDirty(m);result[i]=m;
        } return result;
    }
    static void ThemeTree(GameObject root)
    {
        foreach(var hud in root.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="ResourcesUI")) {
            hud.anchorMin=hud.anchorMax=new Vector2(.5f,1);hud.pivot=new Vector2(.5f,1);hud.anchoredPosition=new Vector2(0,-24);hud.sizeDelta=new Vector2(640,76);
            int index=0;foreach(RectTransform item in hud){item.anchorMin=item.anchorMax=item.pivot=new Vector2(0,1);item.anchoredPosition=new Vector2(index++*220,0);}
        }
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)) {
            text.font=theme.bodyFont;
            if(text.fontSize>=30){text.font=theme.headingFont; text.fontSharedMaterial=theme.outlinedText;}
        }
        foreach(var b in root.GetComponentsInChildren<Button>(true)) {
            if(!b.GetComponentInChildren<TMP_Text>(true) || b.GetComponentInParent<PlanterShopPanelUI>()) continue;
            foreach(var decoration in b.GetComponentsInChildren<Image>(true))
                if(decoration.name=="CRT_Layer"||decoration.name=="Reflection_Layer")decoration.gameObject.SetActive(false);
            string n=b.name.ToLowerInvariant(); theme.StyleButton(b,n.Contains("sell")?"red":n.Contains("skill")||n.Contains("upgrade")?"blue":n.Contains("round")?"gold":"green");
            var r=b.transform as RectTransform;
            if(n=="skill shop button" || n=="placment shop button" || n=="next round button") {
                r.sizeDelta=new Vector2(270,88);r.anchoredPosition=new Vector2(n.Contains("skill")?310:n.Contains("placment")?-310:0,-390);
                var label=b.GetComponentInChildren<TMP_Text>(true);label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=34;
                if(n.Contains("skill"))label.text="UPGRADES";
                if(n.Contains("placment"))label.text="PLANTERS";
            }
            if(n.Contains("sell")) {
                r.sizeDelta=new Vector2(260,86);
                if(n=="planter sell button"){r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(-645,-90);}
                var label=b.GetComponentInChildren<TMP_Text>(true); label.text="SELL";
                label.fontSize=36;label.enableAutoSizing=true;label.fontSizeMin=28;label.fontSizeMax=36;
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(80,8);label.rectTransform.offsetMax=new Vector2(-14,-8);
                if(!b.transform.Find("Comic Coin")) {var icon=Pic("Comic Coin",b.transform,theme.coinSprite,new Vector2(-83,3),new Vector2(65,65));icon.preserveAspect=true;}
            }
        }
    }
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;return r;
    }
    static Image Pic(string name,Transform parent,Sprite sprite,Vector2 pos,Vector2 size)
    {
        var image=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
        if(sprite==theme.cardSprite||sprite==theme.buttonSprite){image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;}
        return image;
    }
    static TMP_Text Text(string name,Transform parent,string text,Vector2 pos,Vector2 size,float fontSize,bool title=false)
    {
        var t=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.text=text;t.font=title?theme.headingFont:theme.bodyFont;t.fontSize=fontSize;t.color=title?Color.white:Ink;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        t.enableAutoSizing=true;t.fontSizeMin=fontSize*.7f;t.fontSizeMax=fontSize;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;
        if(title)t.fontSharedMaterial=theme.outlinedText; return t;
    }
    static Button Button(string name,Transform parent,string caption,Vector2 pos,Vector2 size,string palette="green")
    {
        var image=Pic(name,parent,theme.buttonSprite,pos,size);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Text("Label",image.transform,caption,new Vector2(0,3),size-new Vector2(32,22),32,true);theme.StyleButton(button,palette);return button;
    }
    static void Set(SerializedObject so,string name,Object value){so.FindProperty(name).objectReferenceValue=value;}
    static void Shop(PlanterShopPanelUI shop)
    {
        var so=new SerializedObject(shop);var old=so.FindProperty("cards");
        var data=new System.Collections.Generic.List<(PlanterSO data,Sprite icon)>();
        for(int i=0;i<old.arraySize;i++){
            var item=old.GetArrayElementAtIndex(i);var d=(PlanterSO)item.FindPropertyRelative("data").objectReferenceValue;
            var r=(RectTransform)item.FindPropertyRelative("rect").objectReferenceValue;
            var icon=r.GetComponentsInChildren<Image>(true).FirstOrDefault(im=>im.name.Contains("Icon")||im.name=="Preview");
            data.Add((d,RenderPlanter(d,icon?icon.sprite:null)));
        }
        data=data.OrderBy(x=>x.data.sizeX*x.data.sizeZ==4?3:x.data.sizeX*x.data.sizeZ==3?4:x.data.sizeX*x.data.sizeZ).ToList();
        foreach(Transform c in shop.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
        var root=(RectTransform)shop.transform;root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=new Vector2(0,-25);root.sizeDelta=new Vector2(1600,820);
        var bg=shop.GetComponent<Image>()??shop.gameObject.AddComponent<Image>();bg.sprite=null;bg.color=new Color(.055f,.075f,.08f,1);
        Pic("Catalogue Header",root,theme.buttonSprite,new Vector2(-360,350),new Vector2(845,100));
        var heading=Text("Heading",root,"PLANTERS",new Vector2(-565,356),new Vector2(400,68),48,true);
        var summary=Text("Summary",root,"PICK IT. PLANT IT. GROW BIG.",new Vector2(-340,292),new Vector2(820,35),22);summary.color=new Color(.65f,.8f,.75f);
        old.arraySize=data.Count;
        for(int i=0;i<data.Count;i++){
            var panel=Pic("Planter Card "+i,root,theme.cardSprite,new Vector2(-635+(i%3)*277,110-(i/3)*323),new Vector2(258,305));panel.raycastTarget=true;
            var title=Text("Name",panel.transform,i<data.Count?$"{data[i].data.sizeX}x{data[i].data.sizeZ} PLANTER":"???",new Vector2(0,114),new Vector2(225,36),26);
            title.font=theme.headingFont;
            var preview=Pic("Preview",panel.transform,i<data.Count?data[i].icon:data[0].icon,new Vector2(0,12),new Vector2(225,177));preview.preserveAspect=true;
            var price=Text("Price",panel.transform,i<data.Count?$"{data[i].data.cost} {data[i].data.costType}":"???",new Vector2(0,-111),new Vector2(220,35),27);price.font=theme.headingFont;
            var lockText=Text("Locked",panel.transform,"LOCKED",new Vector2(0,-35),new Vector2(225,45),32,true);lockText.gameObject.SetActive(i>=data.Count);
            if(i>=data.Count){panel.color=new Color(.48f,.5f,.49f);preview.color=new Color(.3f,.3f,.3f);continue;}
            var group=panel.gameObject.AddComponent<CanvasGroup>();var b=panel.gameObject.AddComponent<Button>();b.targetGraphic=panel;
            var item=old.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("data").objectReferenceValue=data[i].data;item.FindPropertyRelative("rect").objectReferenceValue=panel.rectTransform;
            item.FindPropertyRelative("group").objectReferenceValue=group;item.FindPropertyRelative("button").objectReferenceValue=b;item.FindPropertyRelative("background").objectReferenceValue=panel;item.FindPropertyRelative("price").objectReferenceValue=price;
            item.FindPropertyRelative("preview").objectReferenceValue=preview;item.FindPropertyRelative("lockedOverlay").objectReferenceValue=lockText.gameObject;
        }
        var detailRoot=Rect("Planter Details",root,new Vector2(441,0),new Vector2(682,804));var groupDetails=detailRoot.gameObject.AddComponent<CanvasGroup>();
        Pic("Paper",detailRoot,theme.cardSprite,new Vector2(0,-35),new Vector2(675,720));
        Pic("Detail Header",detailRoot,theme.buttonSprite,new Vector2(0,350),new Vector2(682,100));
        var detailTitle=Text("Detail Title",detailRoot,"1x1 PLANTER",new Vector2(0,357),new Vector2(630,64),43,true);
        var detailPreview=Pic("Large Preview",detailRoot,data[0].icon,new Vector2(0,150),new Vector2(570,277));detailPreview.preserveAspect=true;
        var description=Text("Description",detailRoot,"",new Vector2(0,-32),new Vector2(600,95),24);description.alignment=TextAlignmentOptions.TopLeft;
        string[] names={"PLOTS","SIZE","GROW TIME"};TMP_Text[] values=new TMP_Text[3];
        for(int i=0;i<3;i++){
            var tile=Pic(names[i],detailRoot,theme.cardSprite,new Vector2(-204+i*204,-147),new Vector2(195,107));
            Text("Caption",tile.transform,names[i],new Vector2(0,27),new Vector2(177,28),22).font=theme.headingFont;
            values[i]=Text("Value",tile.transform,"1",new Vector2(0,-13),new Vector2(175,56),42);values[i].font=theme.headingFont;
        }
        var status=Text("Status",detailRoot,"",new Vector2(0,-230),new Vector2(600,35),22);
        var buy=Button("Buy Planter",detailRoot,"BUY",new Vector2(0,-307),new Vector2(620,104));
        var buyLabel=buy.GetComponentInChildren<TMP_Text>();buyLabel.rectTransform.sizeDelta=new Vector2(500,74);buyLabel.rectTransform.anchoredPosition=new Vector2(43,3);
        var resourceIcon=Pic("Cost Resource",buy.transform,theme.coinSprite,new Vector2(-250,4),new Vector2(75,76));resourceIcon.preserveAspect=true;
        var back=Button("Back",root,"CLEAR",new Vector2(-90,352),new Vector2(170,62));
        var stats=Text("Legacy Stats",detailRoot,"",Vector2.zero,Vector2.one,20);stats.gameObject.SetActive(false);
        Set(so,"normalCard",theme.cardSprite);Set(so,"selectedCard",theme.cardSprite);Set(so,"pressedCard",theme.cardSprite);
        Set(so,"details",groupDetails);Set(so,"heading",heading);Set(so,"summary",summary);Set(so,"stats",stats);Set(so,"description",description);Set(so,"status",status);
        Set(so,"buyButton",buy);Set(so,"buyLabel",buyLabel);Set(so,"backButton",back);Set(so,"detailPreview",detailPreview);Set(so,"detailTitle",detailTitle);
        Set(so,"plotsLabel",values[0]);Set(so,"sizeLabel",values[1]);Set(so,"timeLabel",values[2]);so.ApplyModifiedPropertiesWithoutUndo();
        Set(so,"theme",theme);Set(so,"buyResourceIcon",resourceIcon);so.ApplyModifiedPropertiesWithoutUndo();
    }
}
