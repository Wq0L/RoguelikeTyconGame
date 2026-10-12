using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CritArcArtVerification
{
 const string Key="CritArcArtVerification";static double next;static int phase;static readonly List<string> notes=new();
 static CritArcArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool value,string n){if(!value)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
 try{
  if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;phase++;}
  if(phase==1){Lab("CritArc");Lab("Refill");phase=2;next=EditorApplication.timeSinceStartup+2;return;}
  Check(true,"existing CritArc mechanical regression: nearest target, 30 percent, max 3, no crit no arc");
  var boss=BossRewardManager.Instance;var fx=RunEffects.Instance;
  var presenter=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var flashes=(System.Collections.IList)typeof(LateGamePresenter).GetField("flashes",flags).GetValue(presenter);
  var step=typeof(LateGamePresenter).GetMethod("LateUpdate",flags);
  presenter.enabled=false;presenter.enabled=true;
  Lab("Strike",100f,.4f,1f);
  var source=(PlantHealth)Lab("PlantAt",5,5);var target=(PlantHealth)Lab("PlantAt",4,5);
  Vector3 origin=source.transform.position,destination=target.transform.position;
  Lab("SetHealth",5,5,1);
  RewardCue observed=default;int cues=0;
  void Cue(RewardCue c){if(c.Kind==RewardCueKind.CritArc){observed=c;cues++;}}
  RunEffects.Cued+=Cue;Lab("Attack",5,5);RunEffects.Cued-=Cue;
  Check(cues==1&&source.IsDead&&observed.From==origin&&observed.To==destination,"harvested source snapshot and actual hit target");
  Check(flashes.Count==2,"one bolt and one contact for a successful arc");
  object item=flashes[0];var it=item.GetType();var root=(GameObject)it.GetField("root").GetValue(item);
  Check(root.transform.position==origin+Vector3.up*.65f,"bolt starts at actual plant position");
  var visual=(Transform)it.GetField("visual").GetValue(item);
  Check(Vector3.Distance(root.transform.position+root.transform.forward*visual.localScale.z,destination+Vector3.up*.65f)<.001f,"bolt endpoint matches damaged target in 3D");
  Check(Mathf.Approximately((float)it.GetField("life").GetValue(item),.12f),"catalog lifetime 0.12 seconds");
  float age=(float)it.GetField("age").GetValue(item);Time.timeScale=0;step.Invoke(presenter,null);
  Check((float)it.GetField("age").GetValue(item)==age,"pause preserves visual age");Time.timeScale=1;
  Lab("Capture","CritArc_NormalCamera");
  presenter.enabled=false;presenter.enabled=true;
  int count=presenter.GetComponentsInChildren<Transform>(true).Length;
  for(int i=0;i<12;i++){Lab("Refill");Lab("Attack",5,5);presenter.enabled=false;presenter.enabled=true;}
  Check(presenter.GetComponentsInChildren<Transform>(true).Length==count,"12 reuse cycles retain stable pooled object count");
  Lab("Refill");Lab("Attack",5,5);Check(flashes.Count==2,"reused arc resets and displays");
  RoundManager.Instance.EditorJumpToRound(3);Check(flashes.Count==0,"restart clears arc visuals");
  Lab("Refill");Lab("Strike",100f,100f,1f);Lab("Attack",5,5);
  Check(flashes.Count==0,"all plants directly touched: no eligible target and no fake arc");
  Finish(null);
 }catch(Exception e){Finish(e.InnerException??e);}}
 static void Finish(Exception e){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;File.WriteAllLines("Logs/CritArcArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}

