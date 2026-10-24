using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Hasat Yankısı meteoru (23 Ekim 2026 yer tutucu sunumu): tek meteor, gerçek planlama/isabet cue'ları, pause, boş hedef, restart,
// havuz kararlılığı. Mekanik ayrıntı GecOyunV1Verification EchoCase/EchoResult/EchoCap'te.
[InitializeOnLoad]
public static class HarvestEchoArtVerification
{
 const string Key="HarvestEchoArtVerification";
 static double next;static int phase,pooled;static readonly List<string> notes=new();
 static readonly List<RewardCue> prepared=new(),hits=new();
 static HarvestEchoArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool v,string n){if(!v)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Cue(RewardCue c){if(c.Kind==RewardCueKind.HarvestEchoPrepared)prepared.Add(c);if(c.Kind==RewardCueKind.HarvestEcho)hits.Add(c);}
 static void PauseNext(int p,double wait=.3){phase=p;next=EditorApplication.timeSinceStartup+wait;}
 static void Attack(){Lab("SetHealth",1,1,1);Lab("SetHealth",1,2,1);Lab("SetHealth",2,1,1);Lab("AttackAt",Lab("PosAt",1,1));}
 static Vector3 W(int x,int z)=>FieldCells.World(RunEffects.Instance.Grid,new GridPosition(x,z));
 static System.Collections.IList Live(LateGamePresenter p)=>(System.Collections.IList)typeof(LateGamePresenter).GetField("liveMeteors",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p);
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;RunEffects.Cued+=Cue;phase=1;}
   var fx=RunEffects.Instance;var presenter=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var audio=(LateGameAudio)typeof(LateGamePresenter).GetField("soundPlayer",flags).GetValue(presenter);
   var echo=fx.Find<HarvestEchoEffect>();
   if(phase==1){
    Lab("EchoCase");echo=fx.Find<HarvestEchoEffect>();
    Check(prepared.Count==1&&hits.Count==0&&fx.PendingTimers==1,"three harvests (and a blocked second harvest) give exactly one planned meteor");
    var c=prepared[0];
    Vector3 target=(W(9,9)+W(9,8)+W(8,9))/3f,source=(W(1,1)+W(1,2)+W(2,1))/3f;
    Check(c.Value==3&&Vector3.Distance(c.To,target)<.01f,"meteor area = the three exact centre mirrors (cell count and centre)");
    Check(Mathf.Abs(c.From.x-source.x)<.05f&&Mathf.Abs(c.From.z-source.z)<.05f,"preparation uses the harvested source centre");
    Check(c.Radius>0f&&presenter.MeteorsLive==1&&(audio==null||audio.ActiveVoices==0),"one falling meteor, no impact sound yet");
    Time.timeScale=0;PauseNext(2);return;
   }
   if(phase==2){
    var m=Live(presenter)[0];
    Check(fx.PendingTimers==1&&hits.Count==0,"pause freezes the real delayed hit");
    Check((float)m.GetType().GetField("age").GetValue(m)==0f,"pause freezes the falling meteor across frames");
    Time.timeScale=1;PauseNext(3,.3);return;
   }
   if(phase==3){
    Check(hits.Count==1&&hits[0].Value==3&&fx.PendingTimers==0,"one impact cue after the delay, three cells struck");
    Check(Vector3.Distance(hits[0].From,prepared[0].To)<.01f,"impact position = planned area centre");
    Check(presenter.MeteorImpacts==1&&presenter.MeteorFizzles==0,"impact presentation (no fizzle) for a real hit");
    Check(presenter.FieldShocks>0,"field reaction: nearby living plants got a visual shock ("+presenter.FieldShocks+")");
    Check(audio.ActiveVoices<=1,"one soft impact voice");
    Lab("Capture","HarvestEcho_NormalCamera");Lab("EchoResult");Check(true,"existing 50 damage, centre exclusion and threshold-1 regression");
    // EchoResult kendi tek hücrelik meteorunu planlar: round yeniden başlatılır (zamanlayıcılar, bekleme ve sunum temizlenir).
    RoundManager.Instance.EditorJumpToRound(3);prepared.Clear();hits.Clear();
    Lab("Refill");Attack();
    // Hedefler meteor düşmeden boşaltılır: isabet yok → meteor yine çarpar (23 Ekim kararı), hiçbir şey ölmez.
    foreach(var cell in new[]{(9,9),(9,8),(8,9)})fx.Strike(new GridPosition(cell.Item1,cell.Item2),int.MaxValue,RewardSource.HarvestEcho);
    PauseNext(4,.3);return;
   }
   if(phase==4){
    Check(prepared.Count==1&&hits.Count==1&&hits[0].Value==0&&echo.Empty>=3,"emptied targets: the meteor resolves with zero hits (three empty cells, no damage)");
    Check(presenter.MeteorImpacts==2&&presenter.MeteorFizzles==0,"empty cells still get the full impact presentation; nothing dies (no damage cue)");
    // Batch'te oyun saati hızlı akar (sabit kare adımı): bekleme sırasında round bitmiş olabilir, yeniden başlatılır.
    RoundManager.Instance.EditorJumpToRound(3);Lab("Refill");prepared.Clear();Attack();
    Check(fx.PendingTimers==1&&presenter.MeteorsLive==1,$"pending meteor before round restart (timers {fx.PendingTimers}, live {presenter.MeteorsLive}, state {GameManager.Instance.CurrentState})");
    RoundManager.Instance.EditorJumpToRound(3);Check(fx.PendingTimers==0&&presenter.MeteorsLive==0&&audio.ActiveVoices==0,"round restart clears the timer, the meteor and audio");
    pooled=presenter.GetComponentsInChildren<Transform>(true).Length;
    for(int i=0;i<12;i++){
     Lab("Refill");prepared.Clear();Attack();
     // His katmanları: tek döngülerde hepsi açık, çift döngülerde kapalı (24 Ekim; pencere anahtarları).
     bool on=i%2==0;LateGamePresenter.MeteorMotion=LateGamePresenter.MeteorImpactFrame=LateGamePresenter.MeteorFieldReaction=on;
     int impacts=presenter.MeteorImpacts,shocks=presenter.FieldShocks;fx.Find<HarvestEchoEffect>().OnTimer(0,50,0,0);   // a = hücre başına hasar
     Check(presenter.MeteorImpacts==impacts+1,"reuse cycle "+i+": the meteor resolves as a real impact");
     var live=(System.Collections.IList)typeof(LateGamePresenter).GetField("liveMeteors",flags).GetValue(presenter);
     var flashPart=(Transform)live[0].GetType().GetField("flash").GetValue(live[0]);
     Check(flashPart.gameObject.activeSelf==on&&(presenter.FieldShocks>shocks)==on,"reuse cycle "+i+": impact flash and field shock follow the toggles ("+(on?"on":"off")+")");
     RoundManager.Instance.EditorJumpToRound(3);
    }
    Check(presenter.GetComponentsInChildren<Transform>(true).Length==pooled,"12 meteor plan + impact cycles keep the pooled object count stable");
    Check(presenter.MeteorsLive==0&&fx.PendingTimers==0&&presenter.MeteorsSkipped==0,"no stale meteor after reuse, none skipped");Finish(null);
   }
  }catch(Exception e){Finish(e.InnerException??e);}
 }
 static void Finish(Exception e){LateGamePresenter.MeteorMotion=LateGamePresenter.MeteorImpactFrame=LateGamePresenter.MeteorFieldReaction=true;RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/HarvestEchoArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
