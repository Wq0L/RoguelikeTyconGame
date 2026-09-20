using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Opt-in batch smoke test. Screenshots use the real scene and live shop data.
[InitializeOnLoad]
public static class ComicUIVerification
{
    const string Key="ComicUIVerification.Active";
    static int stage=-1,frames;
    static float next;
    static double started;
    static PlanterShopPanelUI shop;
    static UIManager ui;
    static PlanterSO selected;
    static int balance;
    static ComicUIVerification(){if(SessionState.GetBool(Key,false)) EditorApplication.update+=Tick;}
    public static void RunBatch()
    {
        SessionState.SetBool(Key+".Menu",false);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        new GameObject("Verification GameManager").AddComponent<GameManager>();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void RunMenuBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        SessionState.SetBool(Key+".Menu",true);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static T Field<T>(string name) where T:Object => (T)new SerializedObject(shop).FindProperty(name).objectReferenceValue;
    static void Tick()
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>60){Debug.LogError("Verification timeout: stage="+stage+" frames="+frames+" time="+Time.unscaledTime);Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||++frames<30||Time.unscaledTime<next)return;
        try {
            if(SessionState.GetBool(Key+".Menu",false)){Capture("menu.png");Debug.Log("COMIC_MENU_RENDER_PASS");Finish(0);return;}
            if(stage==-1){
                ui=Object.FindFirstObjectByType<UIManager>();shop=Object.FindFirstObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
                Require(ui&&shop,"GameScene UI initialized");
                RoundManager.Instance.BeginRun();next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==0){
                Capture("round-end.png");
                ui.OpenPlacementShop();next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==1){
                Require(Time.timeScale==0,"Shop pauses gameplay");
                Require(Field<CanvasGroup>("details").alpha>.99f,"Detail transition completes while paused");
                Require(Field<Image>("detailPreview").sprite,"Planter preview exists");
                Capture("planter-shop.png");
                var array=new SerializedObject(shop).FindProperty("cards");
                int locked=0;
                for(int i=0;i<array.arraySize;i++){
                    var item=array.GetArrayElementAtIndex(i);var data=(PlanterSO)item.FindPropertyRelative("data").objectReferenceValue;
                    var b=(Button)item.FindPropertyRelative("button").objectReferenceValue;
                    if(!data.IsUnlocked){Require(!b.interactable,"Locked card cannot be clicked");locked++;}
                    if(i==0) selected=data;
                }
                Debug.Log("COMIC_LOCKED_CARDS_CHECKED="+locked);
                Require(locked==3,"Exactly three real planters require skill unlocks");
                Require(array.arraySize==5 && shop.transform.Find("Planter Card 5")==null,"No fake catalogue card");
                Require(Field<TMP_Text>("buyLabel").text.StartsWith("BUY"),"Purchase caption");
                Require(Field<Image>("buyResourceIcon").sprite==Resources.Load<ComicUITheme>("ComicUITheme").coinSprite,"Gold price icon");
                var buy=Field<Button>("buyButton");Require(buy.interactable,"Affordable planter can be selected");
                var v=buy.GetComponent<ComicButtonVisual>();var img=buy.GetComponent<Image>();
                var e=new PointerEventData(EventSystem.current);
                v.OnPointerEnter(e);Require(img.material==v.hover,"Hover appearance");
                v.OnPointerDown(e);Require(img.material==v.pressed,"Pressed appearance");
                v.OnPointerUp(e);v.OnPointerExit(e);
                balance=ResourceManager.Instance.GetResourceAmount(selected.costType);
                ResourceManager.Instance.SpendResource(selected.costType,balance);
                Require(!buy.interactable,"Unaffordable purchase disabled");
                v.OnPointerEnter(e);Require(img.material==v.disabled,"Locked button appearance");
                ResourceManager.Instance.AddResource(selected.costType,balance);
                buy.onClick.Invoke();Require(GameManager.Instance.CurrentState==GameStates.Placing,"Buy starts placement");
                Require(ResourceManager.Instance.GetResourceAmount(selected.costType)==balance-selected.cost,"Buy charges once");
                PlacementManager.Instance.CancelPlacement();
                next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==2){
                Require(GameManager.Instance.CurrentState==GameStates.Shop,"Cancel returns to shop");
                Require(ResourceManager.Instance.GetResourceAmount(selected.costType)==balance-selected.cost+selected.cost/2,"Cancel preserves existing half-cost refund");
                var sell=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name=="Planter Sell Button");
                Require(sell.GetComponent<ComicButtonVisual>(),"Sell uses comic theme");sell.onClick.Invoke();
                Require(GameManager.Instance.CurrentState==GameStates.Selling,"Sell action preserved");
                PlacementManager.Instance.ExitSellMode();
                next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==3){
                VerifySkillUnlocks();shop.SelectCard(4);next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==4){
                Require(Field<Image>("buyResourceIcon").sprite==Resources.Load<ComicUITheme>("ComicUITheme").stoneSprite,"Stone price icon");Capture("planters-unlocked.png");shop.SelectCard(2);next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==5){
                Require(Field<Image>("buyResourceIcon").sprite==Resources.Load<ComicUITheme>("ComicUITheme").ironSprite,"Iron price icon");RoundManager.Instance.BeginRun();ui.OpenPlacementShop();next=Time.unscaledTime+1;stage++;return;
            }
            if(stage==6){
                Require(new SerializedObject(shop).FindProperty("cards").arraySize==5,"Catalogue retains five real planters");
                Require(!AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x3.asset").IsUnlocked,"New run resets unlocks");
                Directory.CreateDirectory("Logs/ComicToon");
                File.WriteAllText("Logs/ComicToon/validation.txt","PASS: scene initialization, paused shop animation, planter preview, locked catalogue entries, normal/hover/pressed/disabled material states, affordability, purchase enters placement and charges once, cancellation refund, sell action. Screenshots: live GameScene.");
                Debug.Log("COMIC_UI_PLAY_PASS");Finish(0);
            }
        }catch(Exception ex){Debug.LogException(ex);Finish(1);}
    }
    static void VerifySkillUnlocks()
    {
        var tree=SkillTreeManager.Instance;
        foreach(ResourceType resource in Enum.GetValues(typeof(ResourceType)))ResourceManager.Instance.AddResource(resource,1000000);
        var cards=new SerializedObject(shop).FindProperty("cards");
        foreach(int index in new[]{3,2,4}){
            var item=cards.GetArrayElementAtIndex(index);
            var data=(PlanterSO)item.FindPropertyRelative("data").objectReferenceValue;
            var node=tree.AllNodes.Single(n=>n.unlockType==data.requiredUnlock);
            Require(!data.IsUnlocked,"Planter starts locked: "+data.name);
            if(node.prerequisites.Count>0 && !tree.MeetsPrerequisites(node))Require(!tree.TryUpgrade(node),"Prerequisites cannot be bypassed");
            PurchasePath(node,node.tiers.Count,new System.Collections.Generic.HashSet<SkillNodeSO>());
            Require(data.IsUnlocked,"Skill purchase unlocks matching planter: "+data.name);
            Require(((Button)item.FindPropertyRelative("button").objectReferenceValue).interactable,"Shop updates immediately after skill purchase");
            Require(!((GameObject)item.FindPropertyRelative("lockedOverlay").objectReferenceValue).activeSelf,"Unlocked overlay removed");
        }
    }
    static void PurchasePath(SkillNodeSO node,int level,System.Collections.Generic.HashSet<SkillNodeSO> active)
    {
        Require(active.Add(node),"Skill prerequisites are acyclic");
        foreach(var requirement in node.prerequisites)PurchasePath(requirement.node,requirement.level,active);
        while(SkillTreeManager.Instance.GetCurrentLevel(node)<level)
            Require(SkillTreeManager.Instance.TryUpgrade(node),"Real skill path can be purchased: "+node.nodeName);
        active.Remove(node);
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/ComicToon");var camera=Camera.main;
        if(!camera)camera=Object.FindFirstObjectByType<Camera>();
        if(!camera){
            camera=new GameObject("Verification capture camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.08f,.09f);
            camera.GetUniversalAdditionalCameraData().SetRenderer(1);
        }
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.01f;}
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();var old=RenderTexture.active;
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        RenderTexture.active=target;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        File.WriteAllBytes("Logs/ComicToon/"+name,image.EncodeToPNG());
        Require(image.GetPixels32().Count(p=>p.r>240&&p.b>240&&p.g<15)<100,"UI shader has no magenta error pixels");
        camera.targetTexture=null;RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
        foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}
