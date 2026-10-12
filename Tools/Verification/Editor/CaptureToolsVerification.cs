using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CaptureToolsVerification
{
 const string Key="CaptureToolsVerification";static double next;static int phase;static readonly List<string> notes=new();
 static CaptureToolsVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool value,string n){if(!value)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
 try{
  if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;phase++;}
  var rounds=RoundManager.Instance;var fx=RunEffects.Instance;
  var reward=(BossRewardSO)Lab("Find","hilal_darbesi");Check(BossRewardManager.Instance.EditorGrant(reward,out var refusal),"grant existing reward before jump: "+refusal);
  var owned=BossRewardManager.Instance.Taken.Count;var effect=fx.Find<CrescentEffect>();var plant=FieldCells.LivingPlant(FieldCells.Grid,new GridPosition(5,5));var data=MetaSave.Data;
  typeof(CrescentEffect).GetMethod("Launch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(effect,new object[]{new Vector3(10,0,10)});
  Check(effect.ActiveWaves==1,"active wave exists before transition");
  Check(rounds.EditorJumpToRound(3)&&rounds.CurrentRound==3&&rounds.IsRoundActive&&GameManager.Instance.CurrentState==GameStates.Round,"jump to chosen boss round starts actual Round state");
  Check(fx.PendingTimers==0&&effect.ActiveWaves==0,"previous wave timers removed on jump");
  Check(BossRewardManager.Instance.Taken.Count==owned&&fx.Find<CrescentEffect>()==effect,"owned rewards preserved");
  Check(FieldCells.LivingPlant(FieldCells.Grid,new GridPosition(5,5))==plant,"placed field and plants preserved");
  Check(!BossRewardManager.Instance.IsPending&&!SpecializationManager.Instance.IsPending,"no stale choice blocks new round");
  Check(MetaSave.EditorSessionMemoryOnly&&ReferenceEquals(data,MetaSave.Data),"save writes disabled without replacing loaded progression");
  float time=rounds.RemainingTime;rounds.EditorAddRoundTime(60);Check(Mathf.Abs(rounds.RemainingTime-time-60)<.01f,"extra 60 seconds applied");
  foreach(ResourceType type in Enum.GetValues(typeof(ResourceType))){int before=ResourceManager.Instance.GetResourceAmount(type);ResourceManager.Instance.AddResource(type,10000);Check(ResourceManager.Instance.GetResourceAmount(type)==before+10000,"resource granted: "+type);}
  Check(rounds.EditorJumpToRound(999)&&rounds.CurrentRound==rounds.MaxRounds,"out-of-range round clamps to final round");
  Check(rounds.EditorJumpToRound(-2)&&rounds.CurrentRound==1&&!rounds.RunFailed&&rounds.QuotaProgress==0,"backward jump resets quota/failure state");
  Check(rounds.EditorJumpToRound(1)&&rounds.CurrentRound==1,"same round can restart");
  var window=EditorWindow.GetWindow<LateGameTestWindow>();Check(window!=null,"existing LateGameTestWindow remains available");window.Close();
  Finish(null);
 }catch(Exception e){Finish(e.InnerException??e);}}
 static void Finish(Exception e){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;File.WriteAllLines("Logs/CaptureToolsVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
