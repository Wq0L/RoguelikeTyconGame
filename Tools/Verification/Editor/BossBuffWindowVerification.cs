using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class BossBuffWindowVerification
{
 const string Key="BossBuffWindowVerification";static double next;static int phase;static readonly List<string> notes=new();
 static BossBuffWindowVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool value,string n){if(!value)throw new Exception(n);notes.Add("PASS "+n);}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
 try{
  if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;phase++;}
  var boss=BossRewardManager.Instance;
  var window=EditorWindow.GetWindow<LateGameTestWindow>();
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  typeof(LateGameTestWindow).GetMethod("LoadRewards",flags).Invoke(window,null);
  var rewards=(List<BossRewardSO>)typeof(LateGameTestWindow).GetField("rewards",flags).GetValue(window);
  var expected=boss.Pool.stages.SelectMany(x=>x.rewards).Select(x=>x.reward).Where(x=>x!=null).Distinct().ToList();
  Check(rewards.Count==expected.Count&&expected.All(rewards.Contains),"all active-pool rewards present: "+rewards.Count);
  Check(rewards.Distinct().Count()==rewards.Count,"no duplicate asset rows");
  Check(rewards.Any(x=>x.effect==null&&x.modifiersPerStack.Count>0),"stat buffs included");
  Check(rewards.Any(x=>x.IsBreakthrough),"breakthrough buffs included");
  Check(rewards.Any(x=>x.HasCost),"tradeoff buffs included");
  Check(rewards.Any(x=>x.id=="hilal_darbesi"),"runtime effect buffs included");
  var grant=typeof(LateGameTestWindow).GetMethod("GrantReward",flags);
  bool Give(BossRewardSO r){object[] args={boss,r,null,0};return (bool)grant.Invoke(window,args);}
  var stat=rewards.First(x=>x.effect==null&&x.maxStacks>1&&boss.BlockOf(x,true)==BossRewardBlock.None);
  for(int i=0;i<stat.maxStacks;i++)Check(Give(stat)&&boss.Stacks(stat)==i+1,"repeat grant "+stat.id+" stack "+(i+1));
  Check(!Give(stat)&&boss.Stacks(stat)==stat.maxStacks,"stack cap enforced");
  Check(Give(rewards.First(x=>x.id=="hilal_darbesi")),"runtime effect granted through same window entry");
  Check(MetaSave.EditorSessionMemoryOnly,"window grant protects real save");
  var search=typeof(LateGameTestWindow).GetMethod("MatchesSearch",BindingFlags.NonPublic|BindingFlags.Static);
  var moon=rewards.First(x=>x.id=="hilal_darbesi");
  Check((bool)search.Invoke(null,new object[]{moon,"HILAL_DARBESI"})&&!(bool)search.Invoke(null,new object[]{moon,"no_such_buff"}),"search finds ID and filters unrelated rows");
  var prerequisite=rewards.First(x=>x.id=="kapali_devre");
  Check(!Give(prerequisite),"prerequisite preserved");
  window.Close();
  Finish(null);
 }catch(Exception e){Finish(e.InnerException??e);}}
 static void Finish(Exception e){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;File.WriteAllLines("Logs/BossBuffWindowVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
