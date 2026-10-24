using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CritBurstArtVerification
{
 const string Key="CritBurstArtVerification";static double next;static int phase;static object pausedItem;static float pausedAge;
 const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 static CritBurstArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool v,string n){if(!v)throw new Exception(n);notes.Add("PASS "+n);}
 static object F(object o,string n)=>o.GetType().GetField(n,Flags|BindingFlags.Public).GetValue(o);
 static IList Flashes(LateGamePresenter p)=>(IList)F(p,"flashes");
 static void Clear(LateGamePresenter p){p.enabled=false;p.enabled=true;}
 static void Cue(RewardCue c){if(c.Kind==RewardCueKind.CritBurst)cues.Add(c);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;Lab("CritBurst");Check(true,"existing 9 crits: one burst, exact targets and stat damage; exactly 3 crits once, one crit rejected");RunEffects.Cued+=Cue;phase=1;}
   var p=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();var fx=RunEffects.Instance;var burst=fx.Find<CritBurstEffect>();
   if(phase==1){
    Clear(p);Lab("Refill");Lab("Strike",100f,1.2f,1f);
    var spawner=Lab("SpawnerAt",1,0);spawner.GetType().GetMethod("RemoveSpawnedPlant").Invoke(spawner,null);
    int bursts=burst.Bursts;Lab("Attack",0,0);
    Check(burst.Bursts==bursts&&cues.Count==0&&Flashes(p).Count==0,"two distinct critical targets do not emit burst, visual or sound cue");
    Lab("Refill");int hits=fx.Hits(RewardSource.CritBurst);Lab("Attack",0,0);
    Check(burst.Bursts==bursts+1&&fx.Hits(RewardSource.CritBurst)==hits+3&&cues.Count==1&&Flashes(p).Count==1,"three surviving critical targets: one real burst and one effect");
    var cue=cues.Last();var item=Flashes(p)[0];var root=(GameObject)F(item,"root");var scale=(Vector3)F(item,"scale");
    Check(cue.Radius==1.2f&&(root.transform.position-cue.From-Vector3.up*.65f).sqrMagnitude<.001f&&scale==new Vector3(cue.Radius,1,cue.Radius),"cue and prefab scale use same actual damage radius and center");
    var content=(LateGameContent)F(item,"content");Check(content.critBurst!=null,"catalog binds authored CritBurst prefab");
    content.Step(0,.06f);Check(!content.critBurst.core.gameObject.activeSelf&&content.critBurst.silhouette.gameObject.activeSelf,"white core ends at .05 s, main impact remains");
    content.Step(0,.23f);Check(!content.critBurst.silhouette.gameObject.activeSelf,"main silhouette ends at .22 s");
    Clear(p);Lab("Strike",100f,2f,1f);Lab("Attack",5,5);
    Check(cues.Count==2&&Flashes(p).Count==1,"more than three critical targets still emits one effect");
    pausedItem=Flashes(p)[0];pausedAge=(float)F(pausedItem,"age");Time.timeScale=0;phase=2;next=EditorApplication.timeSinceStartup+.25;return;
   }
   if(phase==2){Check((float)F(pausedItem,"age")==pausedAge,"pause freezes effect age across frames");Time.timeScale=1;phase=3;next=EditorApplication.timeSinceStartup;return;}
   if(phase==3){
    Clear(p);Lab("Refill");Lab("Strike",100f,2f,1f);Lab("Attack",5,5);Lab("Capture","CritBurst_NormalCamera");
    Clear(p);int hits=fx.Hits(RewardSource.CritBurst);int before=cues.Count;
    typeof(LateGamePresenter).GetField("decorations",Flags).SetValue(p,96);Lab("Attack",5,5);typeof(LateGamePresenter).GetField("decorations",Flags).SetValue(p,0);
    Check(fx.Hits(RewardSource.CritBurst)==hits+9&&cues.Count==before+1&&Flashes(p).Count==0,"full decoration pool skips visuals but all nine actual reward hits still apply");
    Lab("Attack",5,5);int count=p.GetComponentsInChildren<Transform>(true).Length;
    for(int i=0;i<12;i++){Clear(p);Lab("Attack",5,5);var c=(LateGameContent)F(Flashes(p)[0],"content");Check(c.critBurst.core.gameObject.activeSelf&&c.critBurst.silhouette.gameObject.activeSelf,"pooled stage reset "+i);}
    Check(p.GetComponentsInChildren<Transform>(true).Length==count,"12 reuse cycles stable object count");
    RoundManager.Instance.EditorJumpToRound(3);Check(Flashes(p).Count==0,"round/restart immediately clears effects");
    Lab("Refill");Lab("Attack",5,5);phase=4;return;
   }
   if(phase==4){if(Flashes(p).Count>0){EditorApplication.QueuePlayerLoopUpdate();return;}Check(true,"normal frame lifecycle releases all pieces by .32 seconds");Finish(null);}
  }catch(Exception e){Finish(e.InnerException??e);}
 }
 static void Finish(Exception e){RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/CritBurstArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
