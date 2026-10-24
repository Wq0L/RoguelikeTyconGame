using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class ReactorArtVerification
{
 const string Key="ReactorArtVerification";static double next;static int phase;static float pausedRemaining;static Vector3 pausedScale;static object identity;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 static ReactorArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool v,string n){if(!v)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Cue(RewardCue c){if(c.Kind==RewardCueKind.ReactorOn)cues.Add(c);}
 static void Harvest(){Lab("Refill");var rare=AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset").spawnTable.First(x=>x.plant.rarity>=PlantRarity.Rare).plant;Lab("SetF",Lab("PlantAt",7,7),"plantData",rare);Lab("SetHealth",7,7,1);Lab("Attack",7,7);}
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;RunEffects.Cued+=Cue;Lab("ReactorCase");phase=1;}
   var presenter=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();var fx=RunEffects.Instance;var reactor=fx.Find<RareReactorEffect>();
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;var sync=typeof(LateGamePresenter).GetMethod("SyncReactor",flags);var field=typeof(LateGamePresenter).GetField("reactorItem",flags);
   var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var pose=typeof(PlayerController).GetMethod("UpdateRadiusVisual",flags);
   if(phase==1){
    Check(cues.Count==1&&reactor.IsActive&&reactor.Refreshes==1,"existing direct Rare+ trigger, invalid Common/behavior rejection and non-stacking refresh regression");
    var gridPoint=FieldCells.World(fx.Grid,new GridPosition(7,7));Check(Mathf.Abs(cues[0].From.x-gridPoint.x)<.01f&&Mathf.Abs(cues[0].From.z-gridPoint.z)<.01f,"opening snapshot at actual harvested source");
    var rhythm=BossRewardManager.Instance.Pool.stages.SelectMany(x=>x.rewards).Select(x=>x.reward).First(x=>x.rhythmHarvests>0);
    Check(BossRewardManager.Instance.EditorGrant(rhythm,out var refusal),"grant existing Harvest Rhythm for coexistence: "+refusal);
    RunPower.Rhythm.Complete(DirectAttack.Normal,RunPower.Rhythm.Threshold,true);
    pose.Invoke(player,new object[]{FieldCells.World(fx.Grid,new GridPosition(5,5))});sync.Invoke(presenter,null);
    identity=field.GetValue(presenter);Check(identity!=null&&player.ShowsChargedCursor,"single reactor marker coexists with golden ready ring");
    var ring=(LineRenderer)typeof(PlayerController).GetField("radiusIndicator",flags).GetValue(player);Color color=ring.startColor;float radius=player.CursorRadius;
    int mods=StatManager.Instance.GlobalModifiers.Count;Harvest();sync.Invoke(presenter,null);
    Check(ReferenceEquals(identity,field.GetValue(presenter))&&StatManager.Instance.GlobalModifiers.Count==mods&&cues.Count==1,"refresh reuses one marker, adds no modifier or opening sound cue");
    Check(ring.startColor==color&&player.CursorRadius==radius,"reactor does not mutate radius or ready ring colors");
    // Restore ready state after the real harvest consumed it, without changing reactor mechanics.
    RunPower.Rhythm.Complete(DirectAttack.Normal,RunPower.Rhythm.Threshold,true);pose.Invoke(player,new object[]{FieldCells.World(fx.Grid,new GridPosition(5,5))});
    Time.timeScale=0;pausedRemaining=reactor.Remaining;pausedScale=((Transform)identity.GetType().GetField("visual").GetValue(identity)).localScale;
    phase=2;next=EditorApplication.timeSinceStartup+.3;return;
   }
   if(phase==2){
    Check(reactor.Remaining==pausedRemaining&&((Transform)identity.GetType().GetField("visual").GetValue(identity)).localScale==pausedScale,"pause freezes actual timer and indicator pose across frames");
    Time.timeScale=1;phase=3;next=EditorApplication.timeSinceStartup;return;
   }
   if(phase==3){
    ((LineRenderer)typeof(PlayerController).GetField("radiusIndicator",flags).GetValue(player)).enabled=true;
    Lab("Capture","Reactor_NormalCamera");
    reactor.Tick(Mathf.Max(0f,reactor.Remaining-.3f));sync.Invoke(presenter,null);
    Check(reactor.IsActive&&field.GetValue(presenter)!=null&&((Transform)identity.GetType().GetField("visual").GetValue(identity)).localScale.x<.4f,"last interval shrinks: remaining="+reactor.Remaining+" active="+reactor.IsActive+" scale="+((Transform)identity.GetType().GetField("visual").GetValue(identity)).localScale+" dt="+Time.deltaTime);
    phase=5;return;
   }
   if(phase==5){
    if(reactor.IsActive){EditorApplication.QueuePlayerLoopUpdate();return;}
    Lab("ReactorResult");Check(true,"real four-second expiry removes only owned modifiers");
    sync.Invoke(presenter,null);Check(field.GetValue(presenter)==null,"expiry retires indicator with quiet tail");
    presenter.enabled=false;presenter.enabled=true;Harvest();sync.Invoke(presenter,null);int count=presenter.GetComponentsInChildren<Transform>(true).Length;
    for(int i=0;i<12;i++){RoundManager.Instance.EditorJumpToRound(3);Harvest();sync.Invoke(presenter,null);}
    Check(presenter.GetComponentsInChildren<Transform>(true).Length==count,"12 activation/restart cycles retain stable pooled objects");
    RoundManager.Instance.EditorJumpToRound(3);Check(field.GetValue(presenter)==null&&!reactor.IsActive,"restart immediately clears indicator and bonus");
    Harvest();sync.Invoke(presenter,null);presenter.enabled=false;Check(field.GetValue(presenter)==null,"disable releases marker");presenter.enabled=true;sync.Invoke(presenter,null);Check(field.GetValue(presenter)!=null,"re-enable reads actual active state");
    Finish(null);
   }
  }catch(Exception e){Finish(e.InnerException??e);}
 }
 static void Finish(Exception e){RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/ReactorArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
