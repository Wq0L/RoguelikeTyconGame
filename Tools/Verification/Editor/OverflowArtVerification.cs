using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class OverflowArtVerification
{
 const string Key="OverflowArtVerification";static double next;static int phase;static readonly List<string> notes=new();
 static OverflowArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool value,string n){if(!value)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
 try{
  if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;phase++;}
  if(phase==1){Lab("CritArc");Lab("Overflow");Lab("Refill");phase=2;next=EditorApplication.timeSinceStartup+2;return;}
  Check(true,"existing CritArc and Overflow regressions: damage, target order, caps, no kill no transfer");
  var presenter=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var flashes=(System.Collections.IList)typeof(LateGamePresenter).GetField("flashes",flags).GetValue(presenter);
  var step=typeof(LateGamePresenter).GetMethod("Update",flags);
  if(phase==3){
   var paused=flashes[0];var type=paused.GetType();
   Check((float)type.GetField("age").GetValue(paused)==0f,"pause freezes travel lifetime across frames");
   Time.timeScale=1;
   type.GetField("age").SetValue(paused,1f);step.Invoke(presenter,null);
   Check(!((GameObject)type.GetField("root").GetValue(paused)).activeSelf,"expired shard returns to pool");
   Finish(null);return;
  }
  presenter.enabled=false;presenter.enabled=true;
  Lab("Strike",100f,.4f,0f);Lab("SetHealth",5,5,30);Lab("SetHealth",4,5,10);
  var from=((PlantHealth)Lab("PlantAt",5,5)).transform.position;
  var middle=((PlantHealth)Lab("PlantAt",4,5)).transform.position;
  var to=((PlantHealth)Lab("PlantAt",3,5)).transform.position;
  var cues=new List<RewardCue>();void Cue(RewardCue c){if(c.Kind==RewardCueKind.Overflow)cues.Add(c);}
  RunEffects.Cued+=Cue;Lab("Attack",5,5);RunEffects.Cued-=Cue;
  Check(cues.Count==2&&cues[0].From==from&&cues[0].To==middle&&cues[1].From==middle&&cues[1].To==to,"actual harvested source and both real targets captured");
  Check(cues[0].TransferStep==0&&cues[1].TransferStep==1,"zero-based presentation steps");
  Check(flashes.Count==4,"each transfer creates only shard and true-hit contact");
  var it=flashes[0].GetType();object first=flashes[0],second=flashes[2];
  float life=(float)it.GetField("life").GetValue(first);
  Check(Mathf.Approximately(life,.14f)&&(float)it.GetField("life").GetValue(second)<life,"second hop shorter lifetime");
  Check(((Vector3)it.GetField("scale").GetValue(second)).z<((Vector3)it.GetField("scale").GetValue(first)).z,"second hop smaller and shorter trail");
  var root=(GameObject)it.GetField("root").GetValue(first);
  Check(root.transform.position==from+Vector3.up*.65f,"shard starts at captured plant with rim clearance");
  it.GetField("age").SetValue(first,.035f);step.Invoke(presenter,null);
  Check(root.transform.position.x<from.x&&root.transform.position.x>middle.x,"bounded shard travels toward actual target");
  Lab("Capture","Overflow_NormalCamera");
  var audio=(LateGameAudio)typeof(LateGamePresenter).GetField("soundPlayer",flags).GetValue(presenter);
  Check(audio.ActiveVoices<=1,"two transfers use at most one soft voice");
  presenter.enabled=false;presenter.enabled=true;int count=presenter.GetComponentsInChildren<Transform>(true).Length;
  for(int i=0;i<12;i++){Lab("Refill");Lab("SetHealth",5,5,30);Lab("SetHealth",4,5,10);Lab("Attack",5,5);presenter.enabled=false;presenter.enabled=true;}
  Check(presenter.GetComponentsInChildren<Transform>(true).Length==count,"12 two-hop reuse cycles retain stable object count");
  Lab("Refill");Lab("Attack",5,5);Check(flashes.Count==0,"nonlethal direct hit has no fake effect");
  Lab("SetHealth",5,5,30);Lab("Attack",5,5);Check(flashes.Count==2,"one surviving target stops at one transfer");
  RoundManager.Instance.EditorJumpToRound(3);Check(flashes.Count==0&&audio.ActiveVoices==0,"round restart clears travel contacts and audio");
  Lab("Refill",false,1);Lab("Strike",100f,100f,0f);Lab("Attack",5,5);
  Check(flashes.Count==0,"no living destination creates no fake transfer");
  Lab("Refill");Lab("Strike",100f,.4f,0f);Lab("SetHealth",5,5,30);Lab("Attack",5,5);
  Time.timeScale=0;phase=3;next=EditorApplication.timeSinceStartup+.3;return;
 }catch(Exception e){Finish(e.InnerException??e);}}
 static void Finish(Exception e){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;File.WriteAllLines("Logs/OverflowArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}

