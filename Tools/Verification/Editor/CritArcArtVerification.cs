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
  // 23 Ekim 2026: camgöbeği ışın yerine altın kavis + iniş yıldızı (LateGamePresenter.CritArc.cs).
  Check(presenter.CritArcsLive==1&&flashes.Count==0,"one gold arc and no legacy cyan bolt/contact for a successful arc");
  var arcs=(System.Collections.IList)typeof(LateGamePresenter).GetField("liveArcs",flags).GetValue(presenter);
  object arc=arcs[0];var at=arc.GetType();var lift=Vector3.up*.65f;
  var gold=(LineRenderer)at.GetField("gold").GetValue(arc);
  Check(Vector3.Distance(gold.GetPosition(0),origin+lift)<.001f,"arc starts at actual crit plant position");
  Check(Vector3.Distance(gold.GetPosition(gold.positionCount-1),destination+lift)<.001f,"arc lands on damaged target in 3D");
  Check(gold.GetPosition(gold.positionCount/2).y>origin.y+lift.y+.1f,"arc rises between source and target");
  Check(gold.startColor.r>.95f&&gold.startColor.g>.6f&&gold.startColor.g<.8f&&gold.startColor.b<.2f,"crit gold colour, not electric cyan");
  var star=(Transform)at.GetField("star").GetValue(arc);
  Check(Vector3.Distance(star.position,destination+lift)<.001f&&Mathf.Approximately(star.localScale.x,1f),"landing star at target, full size on appear");
  Check(Mathf.Approximately((float)typeof(LateGamePresenter).GetField("ArcSeconds",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),.3f),"arc lifetime 0.3 seconds");
  float age=(float)at.GetField("age").GetValue(arc);Time.timeScale=0;step.Invoke(presenter,null);
  Check((float)at.GetField("age").GetValue(arc)==age,"pause preserves arc age");Time.timeScale=1;
  Lab("Capture","CritArc_NormalCamera");
  at.GetField("age").SetValue(arc,.31f);step.Invoke(presenter,null);
  Check(presenter.CritArcsLive==0&&!((GameObject)at.GetField("root").GetValue(arc)).activeSelf,"arc returns to pool after its lifetime");
  presenter.enabled=false;presenter.enabled=true;
  int count=presenter.GetComponentsInChildren<Transform>(true).Length;
  for(int i=0;i<12;i++){Lab("Refill");Lab("Attack",5,5);presenter.enabled=false;presenter.enabled=true;}
  Check(presenter.GetComponentsInChildren<Transform>(true).Length==count,"12 reuse cycles retain stable pooled object count");
  Lab("Refill");Lab("Attack",5,5);Check(presenter.CritArcsLive==1,"reused arc resets and displays");
  RoundManager.Instance.EditorJumpToRound(3);Check(presenter.CritArcsLive==0,"restart clears arc visuals");
  Lab("Refill");Lab("Strike",100f,100f,1f);Lab("Attack",5,5);
  Check(presenter.CritArcsLive==0&&flashes.Count==0,"all plants directly touched: no eligible target and no fake arc");
  Finish(null);
 }catch(Exception e){Finish(e.InnerException??e);}}
 static void Finish(Exception e){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;File.WriteAllLines("Logs/CritArcArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}

