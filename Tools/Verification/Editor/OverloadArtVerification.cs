using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Aşırı Yükleme sunumu (24 Ekim 2026: toon "dolan tekrar oku" rozeti, B5). Mekanik regresyon (gerçek .3 sn / yarım hasar /
// bekleme / sınırlar) GecOyunV1Verification'dan; görsel: kabul edilen iş başına bir rozet, pause, iptal, tekrar pop'u, pop halkası
// sınırı, dekor sınırından bağımsızlık, satılan saksı, round temizliği, ağların bir kez kurulması, karede 0 bayt, nesne sayısı.
[InitializeOnLoad]
public static class OverloadArtVerification
{
 const string Key="OverloadArtVerification";const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static double next,pausedClock,limitsAt,captureAt;static int phase,scenario;static Vector3 pausedScale;static object pausedItem;static Mesh pausedMesh;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 static OverloadArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static object F(object o,string n)=>o.GetType().GetField(n,Flags|BindingFlags.Public).GetValue(o);
 static void Check(bool v,string n){if(!v)throw new Exception(n);notes.Add("PASS "+n);}
 static Array Items(LateGamePresenter p)=>(Array)F(p,"overloadBadges");
 static bool Shown(object card)=>card!=null&&((GameObject)F(card,"root")).activeSelf;
 static int Count(LateGamePresenter p)=>Items(p).Cast<object>().Count(Shown);
 static void Sync(LateGamePresenter p)=>typeof(LateGamePresenter).GetMethod("SyncOverload",Flags).Invoke(p,null);
 static void Released(LateGamePresenter p,Vector3 at)=>typeof(LateGamePresenter).GetMethod("OverloadReleased",Flags).Invoke(p,new object[]{new RewardCue(RewardCueKind.Overload,at)});
 static void Clear(LateGamePresenter p){p.enabled=false;p.enabled=true;}
 static int C(RewardCueKind kind)=>cues.Count(c=>c.Kind==kind);
 static void Cue(RewardCue c){if(c.Kind==RewardCueKind.Overload||c.Kind==RewardCueKind.OverloadPrepared||c.Kind==RewardCueKind.OverloadCancelled)cues.Add(c);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Setup(string type,LateGamePresenter p){Lab("ResetRewards");Lab("ClearTiles");Lab("Tile",5,5,type,1f);Lab("Refill");Lab("Strike",100f,.4f);Lab("Take","asiri_yukleme");Clear(p);cues.Clear();}
 static void Arm(){Lab("SetHealth",5,5,1);Lab("Attack",5,5);}
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   var p=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();
   if(phase==0){
    if(double.IsNaN((double)Lab("Lab")))return;p=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();RunEffects.Cued+=Cue;Lab("OverloadTake");Sync(p);
    Check(C(RewardCueKind.OverloadPrepared)==1&&Count(p)==1,"accepted normal explosion creates exactly one badge");
    var fills=(Mesh[])F(p,"overloadFills");Check(fills!=null&&fills.Length==30&&fills.All(m=>m!=null)&&F(p,"overloadPopMesh")!=null,"badge meshes built once at runtime (30 fill steps + pop)");
    int slot=Array.IndexOf(Items(p).Cast<object>().ToArray(),Items(p).Cast<object>().First(Shown));pausedItem=Items(p).GetValue(slot);var root=((GameObject)F(pausedItem,"root")).transform;
    pausedScale=root.localScale;pausedMesh=((MeshFilter)F(pausedItem,"filter")).sharedMesh;pausedClock=RunEffects.Instance.Clock;
    // Görünürlük B: rozet işin anlık konumunun 0,7 üstünde, bakış ekseninde 4 birim kameraya doğru (ortografik: ekrandaki yeri aynı).
    var cam=Camera.main;RunEffects.Instance.Find<OverloadEffect>().TryPreparation(slot,out Vector3 jobAt,out _);
    Vector3 expected=jobAt+Vector3.up*.7f-cam.transform.forward*4f;Vector2 a=cam.WorldToScreenPoint(root.position),b=cam.WorldToScreenPoint(jobAt+Vector3.up*.7f);
    Check((root.position-expected).sqrMagnitude<1e-4f&&(a-b).magnitude<.5f,$"badge is pulled toward the camera, same screen spot (offset {(a-b).magnitude:0.00} px)");
    Time.timeScale=0;phase=1;next=EditorApplication.timeSinceStartup+.25;return;
   }
   var fx=RunEffects.Instance;
   if(phase==1){
    var root=((GameObject)F(pausedItem,"root")).transform;Sync(p);
    Check(fx.Clock==pausedClock&&root.localScale==pausedScale&&((MeshFilter)F(pausedItem,"filter")).sharedMesh==pausedMesh,"pause freezes actual job and badge pose");
    // Karede bellek: etkin rozetle 300 senkron 0 bayt.
    var sync=(Action)Delegate.CreateDelegate(typeof(Action),p,typeof(LateGamePresenter).GetMethod("SyncOverload",Flags));sync();
    long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)sync();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
    Check(bytes==0,$"300 syncs with an active badge allocate {bytes} bytes");
    Time.timeScale=1;phase=2;
   }
   if(phase==2){if(fx.PendingTimers>0){EditorApplication.QueuePlayerLoopUpdate();return;}Lab("OverloadResult");Check(C(RewardCueKind.OverloadPrepared)==1&&C(RewardCueKind.Overload)==1,"existing .3 second / half-damage / cooldown / no self-rearm regression");limitsAt=fx.Clock+2.6;phase=20;return;}
   if(phase==20){if(fx.Clock<limitsAt){EditorApplication.QueuePlayerLoopUpdate();return;}Lab("OverloadLimits");phase=3;return;}
   if(phase==3){if(fx.PendingTimers>0){EditorApplication.QueuePlayerLoopUpdate();return;}Lab("OverloadLimitsResult");Check(true,"existing moving capacity and chain-exclusion regression");phase=4;scenario=0;}
   if(phase==4){
    string[] types={"Explosive","Electric","Tornado","Boomerang","Tornado","Boomerang","Explosive","Explosive","Explosive"};
    Setup(types[scenario],p);
    if(scenario==4){for(int i=0;i<11;i++)Check(TornadoManager.Instance.TrySpawn((GridObject)Lab("Go",9,9),1),"fill tornado slot "+i);}
    if(scenario==5){for(int i=0;i<11;i++)Check(HarvestBehaviorManager.Instance.TryBoomerang((PlanterBrain)Lab("BrainAt",9,9),(GridObject)Lab("Go",9,9),1),"fill boomerang slot "+i);}
    if(scenario==6){
     Lab("ClearTiles");foreach(var q in new[]{new Vector2Int(2,2),new Vector2Int(2,6),new Vector2Int(6,2),new Vector2Int(6,6)}){Lab("Tile",q.x,q.y,"Explosive",1f);Lab("SetHealth",q.x,q.y,1);}Lab("Strike",100f,7f);Lab("Attack",4,4);
     Sync(p);Check(fx.Find<OverloadEffect>().Granted==3&&fx.Find<OverloadEffect>().BudgetBlocks>=1&&C(RewardCueKind.OverloadPrepared)==3&&Count(p)==3,"four real normal behaviors in one root accept only three preparations, three badges");
    }else{
     if(scenario==7)typeof(LateGamePresenter).GetField("decorations",Flags).SetValue(p,96);
     Arm();Sync(p);
     Check(C(RewardCueKind.OverloadPrepared)==1&&fx.Find<OverloadEffect>().Granted==1,"accepted preparation scenario "+scenario);
     Check(Count(p)==1,"one visible badge scenario "+scenario+(scenario==7?" (independent of the decoration cap)":""));
     if(scenario==1){captureAt=fx.Clock+.17;}
     if(scenario==8){((PlanterBrain)Lab("BrainAt",5,5)).RemoveSelf();Sync(p);Check(Count(p)==0,"selling source immediately detaches badge without following replacement");}
    }
    phase=scenario==1?41:5;return;
   }
   if(phase==41){if(fx.Clock<captureAt){EditorApplication.QueuePlayerLoopUpdate();return;}Sync(p);Lab("Capture","Overload_NormalCamera");phase=5;}
   if(phase==5){
    if(fx.PendingTimers>0){EditorApplication.QueuePlayerLoopUpdate();return;}
    var effect=fx.Find<OverloadEffect>();Sync(p);
    bool cancelled=scenario==4||scenario==5||scenario==8;
    Check(cancelled?effect.Fired==0&&effect.Dropped==1&&C(RewardCueKind.Overload)==0&&C(RewardCueKind.OverloadCancelled)==1:effect.Fired==(scenario==6?3:1)&&C(RewardCueKind.Overload)==effect.Fired,"actual success/cancellation only scenario "+scenario);
    Check(Count(p)==0,"resolved badge released scenario "+scenario);
    if(scenario==7){typeof(LateGamePresenter).GetField("decorations",Flags).SetValue(p,0);Check(effect.Fired==1,"full decoration pool does not prevent actual extra behavior");}
    if(scenario==7){
     // Tekrar pop'u: tek pop, süresi dolunca kalkar; halka 16'yı aşmaz.
     Clear(p);Setup("Explosive",p);Arm();Sync(p);Released(p,(Vector3)Lab("PosAt",5,5));
     Check(p.OverloadPopsShown==1,"a release shows exactly one pop");
     var pops=(Array)F(p,"overloadPops");for(int i=0;i<pops.Length;i++){var pop=pops.GetValue(i);pop.GetType().GetField("age").SetValue(pop,1f);pops.SetValue(pop,i);}
     typeof(LateGamePresenter).GetField("overloadPopFrame",Flags).SetValue(p,-1);Sync(p);Check(p.OverloadPopsShown==0,"pop hides after its duration");
     int replaced=p.OverloadPopsReplaced;for(int i=0;i<20;i++)Released(p,(Vector3)Lab("PosAt",5,5));
     Check(p.OverloadPopsShown==16&&p.OverloadPopsReplaced==replaced+4,$"20 rapid releases keep at most 16 pops ({p.OverloadPopsShown}), oldest replaced ({p.OverloadPopsReplaced-replaced})");
     var fills=(Mesh[])F(p,"overloadFills");
     Clear(p);Setup("Explosive",p);Arm();Sync(p);int count=p.GetComponentsInChildren<Transform>(true).Length;
     for(int i=0;i<12;i++){RoundManager.Instance.EditorJumpToRound(3);Check(Count(p)==0&&p.OverloadPopsShown==0&&fx.PendingTimers==0,"round clears badges, pops and real job "+i);Lab("Refill");Arm();Sync(p);}
     Check(p.GetComponentsInChildren<Transform>(true).Length==count,"12 round/reuse cycles stable pooled object count");
     Check(ReferenceEquals(fills,F(p,"overloadFills")),"badge meshes are not rebuilt across rounds");
     Clear(p);Sync(p);Check(Count(p)==1,"re-enable reconstructs only real pending work");RoundManager.Instance.EditorJumpToRound(3);
    }
    scenario++;if(scenario>=9){Finish(null);return;}phase=4;next=EditorApplication.timeSinceStartup+.2;
   }
  }catch(Exception e){Finish(e.InnerException??e);}
 }
 static void Finish(Exception e){RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/OverloadArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
