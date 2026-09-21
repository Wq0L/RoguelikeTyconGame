using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RoundMapComicVerification
{
    const string Key="RoundMapComicVerification.Active";
    static int stage,frames; static float next; static double started;
    static RoundMapUI map; static Transform grid; static ComicHoverMotion motion; static Button start;
    static RoundMapComicVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");new GameObject("Verification GameManager").AddComponent<GameManager>();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("ROUND_CHECK: "+message);}
    static void Capture(string file){typeof(ComicUIVerification).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{file});}
    static void Tick(){
        Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>90){Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||++frames<30||Time.unscaledTime<next)return;
        try{
            if(stage==0){RoundManager.Instance.BeginRun();stage++;next=Time.unscaledTime+1;return;}
            if(stage==1){
                map=Object.FindFirstObjectByType<RoundMapUI>();Check(map,"Round map visible");
                grid=(Transform)new SerializedObject(map).FindProperty("gridContainer").objectReferenceValue;
                int w=GridManager.Instance.GetWidth(),h=GridManager.Instance.GetHeight();Check(grid.childCount==w*h,"All real grid cells represented");
                Check(map.transform.Find("Column Headers").childCount==w&&map.transform.Find("Row Headers").childCount==h,"Coordinate headers match map dimensions");
                for(int i=0;i<grid.childCount;i++){
                    var cell=grid.GetChild(i);var ground=GridManager.Instance.GetGridSystem().GetGridObject(new GridPosition(i%w,h-1-i/w)).GetGroundCellCached();
                    Check(cell.name==$"Cell_x{i%w}_z{h-1-i/w}","Mapping "+cell.name);
                    Check(cell.GetComponent<TooltipTrigger>()&&cell.GetComponent<TileCellUI>(),"Tooltip components preserved");
                    var expected=ground.IsLocked?new Color(.16f,.19f,.21f):ground.CurrentModifier?ground.CurrentModifier.tileColor:Color.white;
                    Check(cell.GetComponent<Image>().color==expected,"Cell state color");
                }
                int first=grid.GetChild(0).GetInstanceID();map.BuildGrid();Check(grid.childCount==w*h&&grid.GetChild(0).GetInstanceID()==first,"Cells pooled across refresh");
                Capture("round-map.png");
                var target=grid.Cast<Transform>().First(t=>!((GroundCell)typeof(TileCellUI).GetFields(BindingFlags.NonPublic|BindingFlags.Instance).First(f=>f.FieldType==typeof(GroundCell)).GetValue(t.GetComponent<TileCellUI>())).IsLocked);
                var groundCell=(GroundCell)typeof(TileCellUI).GetFields(BindingFlags.NonPublic|BindingFlags.Instance).First(f=>f.FieldType==typeof(GroundCell)).GetValue(target.GetComponent<TileCellUI>());
                var modifier=ScriptableObject.CreateInstance<TileModifierSO>();modifier.modifierName="Comic color verification";modifier.tileColor=new Color(.3f,.8f,.5f);groundCell.ApplyModifier(modifier);map.BuildGrid();Check(target.GetComponent<Image>().color==modifier.tileColor,"Modifier tint preserved");
                target.GetComponent<TooltipTrigger>().OnPointerEnter(new PointerEventData(EventSystem.current));
                var popup=(GameObject)typeof(TooltipManager).GetField("activeTooltip",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(TooltipManager.Instance);Check(popup&&popup.activeInHierarchy,"Real modifier popup opens");
                target.GetComponent<TooltipTrigger>().OnPointerExit(new PointerEventData(EventSystem.current));Check(!popup.activeSelf,"Popup closes");
                start=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Next Round Button");motion=start.GetComponent<ComicHoverMotion>();motion.OnPointerEnter(new PointerEventData(EventSystem.current));stage++;next=Time.unscaledTime+.4f;return;
            }
            if(stage==2){Check(Time.timeScale==0&&motion.transform.localScale.x>1.04f,"Hover animates while paused");motion.OnPointerExit(new PointerEventData(EventSystem.current));stage++;next=Time.unscaledTime+.4f;return;}
            if(stage==3){
                Check(Mathf.Approximately(motion.transform.localScale.x,1),"Hover returns to rest");
                var ui=Object.FindFirstObjectByType<UIManager>();
                foreach(string name in new[]{"Placment Shop Button","Skill Shop Button"}){
                    var button=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name==name);
                    Check(button.GetComponentInChildren<ComicActionIcon>().GetComponent<CanvasRenderer>(),"Action icon renderer");
                    button.onClick.Invoke();Check(GameManager.Instance.CurrentState==GameStates.Shop,"Shop action preserved: "+name);
                    string field=name.StartsWith("Placment")?"placementShopPanel":"skillShopPanel";
                    Check(((GameObject)new SerializedObject(ui).FindProperty(field).objectReferenceValue).activeInHierarchy,"Correct shop opens");ui.ShowRoundEndUI();
                }
                Check(grid.childCount==GridManager.Instance.GetWidth()*GridManager.Instance.GetHeight(),"Reopening retains pooled map");
                Capture("round-map-colored.png");start.onClick.Invoke();Check(Time.timeScale>0,"Start round action preserved");Debug.Log("ROUND_COMIC_PLAY_PASS");Finish(0);
            }
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}
