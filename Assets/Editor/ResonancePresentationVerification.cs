using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ResonancePresentationVerification
{
    const string Key="ResonancePresentationVerification.Active";static int stage,frames;static float next,pausedElapsed;static double started;
    static PlanterBrain owner;static List<ActiveResonance> active;static ResonanceBurst burst;
    static ResonancePresentationVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");new GameObject("Verification GameManager").AddComponent<GameManager>();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);Debug.Log("RESONANCE_CHECK: "+message);}
    static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
    static ResonanceBurst[] Playing()=>Object.FindObjectsByType<ResonanceBurst>(FindObjectsSortMode.None).Where(b=>b.IsPlaying).ToArray();
    static void Tick(){Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>100){Debug.LogError("Resonance test timeout");Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||++frames<30||Time.unscaledTime<next)return;
        try{
            if(stage==0){
                RoundManager.Instance.BeginRun();GameManager.Instance.StartCardSelection();
                owner=new GameObject("Presentation fixture planter").AddComponent<PlanterBrain>();
                var grid=GridManager.Instance.GetGridSystem();var cells=new List<GridObject>{grid.GetGridObject(new GridPosition(4,4)),grid.GetGridObject(new GridPosition(5,4))};
                Field(typeof(PlanterBrain),"occupiedGrids").SetValue(owner,cells);
                active=(List<ActiveResonance>)Field(typeof(PlanterBrain),"activeResonances").GetValue(owner);
                active.Add(new ActiveResonance{resonanceName="BEREKET",tileType=TileModifierType.Water,statType=StatType.XPGainMultiplier,requiredCount=2,tileCount=2,multiplier=2});
                VFXManager.Instance.RequestResonance(owner,new List<ActiveResonance>());
                var prior=new List<ActiveResonance>(active);active[0]=new ActiveResonance{resonanceName="BEREKET",tileType=TileModifierType.Water,statType=StatType.XPGainMultiplier,requiredCount=3,tileCount=3,multiplier=10};VFXManager.Instance.RequestResonance(owner,prior);
                GameManager.Instance.ShowRoundEnd();stage++;next=Time.unscaledTime+1.4f;return;
            }
            if(stage==1){
                Check(VFXManager.Instance.HasPendingResonances&&Playing().Length==0,"Preparation retains accumulated resonance without playing");
                Check(!VFXManager.Instance.PlayPendingResonances()&&VFXManager.Instance.HasPendingResonances,"Queue cannot be consumed outside Round");
                GameManager.Instance.OpenShop();VFXManager.Instance.RequestResonance(owner,new List<ActiveResonance>());GameManager.Instance.StartGame();GameManager.Instance.ShowRoundEnd();stage++;next=Time.unscaledTime+.5f;return;
            }
            if(stage==2){Check(VFXManager.Instance.HasPendingResonances&&Playing().Length==0,"Interrupted transition preserves queue");GameManager.Instance.StartGame();stage++;next=Time.unscaledTime+.9f;return;}
            if(stage==3){
                Check(!VFXManager.Instance.HasPendingResonances&&Playing().Length==1,"Round presents one final upgrade per planter");burst=Playing()[0];var label=burst.GetComponentInChildren<TextMeshPro>();
                Check(label.text.Contains("REZONANS!")&&label.text.Contains("900%"),"Final tier and highlighted heading displayed");
                var damagePrefab=(FloatingText)Field(typeof(VFXManager),"floatingTextPrefab").GetValue(VFXManager.Instance);
                var damageLabel=(TextMeshPro)Field(typeof(FloatingText),"textMesh").GetValue(damagePrefab);
                Check(label.font==damageLabel.font&&label.fontSharedMaterial==damageLabel.fontSharedMaterial&&label.alpha>.99f,"Damage text font/material shared and fully readable");
                typeof(ComicUIVerification).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"resonance-round.png"});
                GameManager.Instance.OpenShop();pausedElapsed=(float)Field(typeof(ResonanceBurst),"elapsed").GetValue(burst);stage++;next=Time.unscaledTime+.7f;return;
            }
            if(stage==4){Check(Mathf.Approximately(pausedElapsed,(float)Field(typeof(ResonanceBurst),"elapsed").GetValue(burst))&&!burst.GetComponentInChildren<TextMeshPro>().GetComponent<Renderer>().enabled,"Overlay hides effect without consuming readable time");GameManager.Instance.StartGame();stage++;next=Time.unscaledTime+1.3f;return;}
            if(stage==5){Check(burst.IsPlaying&&burst.GetComponentInChildren<TextMeshPro>().alpha>.99f,"Text still visible after two seconds of gameplay");stage++;next=Time.unscaledTime+1.5f;return;}
            if(stage==6){Check(Playing().Length==0,"Effect expires after readable duration");
                GameManager.Instance.OpenShop();VFXManager.Instance.RequestResonance(owner,new List<ActiveResonance>());Object.DestroyImmediate(owner.gameObject);GameManager.Instance.StartGame();stage++;next=Time.unscaledTime+.5f;return;}
            if(stage==7){Check(!VFXManager.Instance.HasPendingResonances&&Playing().Length==0,"Removed planter is skipped safely");VFXManager.Instance.PlayResonance(new Bounds(Vector3.zero,Vector3.one),Color.cyan,"RESET");GameManager.Instance.StartRunSetup();Check(Playing().Length==0&&!VFXManager.Instance.HasPendingResonances,"New run clears active and pending celebrations");Debug.Log("RESONANCE_PRESENTATION_PASS");Finish(0);}
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}
