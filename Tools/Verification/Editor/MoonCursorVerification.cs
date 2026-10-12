using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class MoonCursorVerification
{
 const string Key="MoonCursorVerification";static int phase=-1;static double next;static readonly List<string> notes=new();static readonly List<GridPosition> hits=new();
 static RunEffects Fx=>RunEffects.Instance;static CrescentEffect C=>Fx.Find<CrescentEffect>();static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);
 static void Check(bool c,string n){if(!c)throw new Exception(n);notes.Add("PASS "+n);}
 static void Add(string id){var r=(BossRewardSO)Lab("Find",id);if(!Fx.TryAdd(r.effect,-1))throw new Exception(id);}
 static void Fresh(){Fx.ClearAll();Fx.enabled=false;Lab("Refill",false);Add("hilal_darbesi");hits.Clear();}
 static void Damage(PlantHealth p,int amount,DamageType type){if(type==DamageType.Reward)hits.Add(FieldCells.Grid.GetGridPosition(p.transform.position));}
 static CrescentEffect.VisualWave Launch(Vector3 at){Call(C,"Launch",at);C.TryVisual(0,out var v);return v;}
 static void Drain(){for(int n=0;n<30&&C.ActiveWaves>0;n++)for(int i=0;i<4;i++)C.OnTimer(0,i,0,0);Call(Fx,"DropTimers");}
 static MoonCursorVerification(){EditorApplication.update+=Tick;}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
 try{switch(phase){
 case -1:if(double.IsNaN((double)Lab("Lab")))return;PlantHealth.AnyDamaged+=Damage;Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"isolated memory-only GameScene");break;
 case 0:
  Fresh();Lab("Strike",100f,.1f,0f);Lab("AttackAt",new Vector3(9,0,9));Check(C.Counter==4&&C.Launched==0,"one real direct swing damaging four plants charges four");
  Lab("AttackAt",new Vector3(9,0,9));Check(C.Counter==2&&C.Launched==1,"second four-target swing launches once and retains 2/6");
  C.TryVisual(0,out var v);Check(Vector3.Distance(v.Position,new Vector3(9,0,9))<.001f,"wave begins at exact attack/cursor point");
  Drain();Check(C.Counter==2,"wave damage does not refill direct counter");Lab("AttackAt",new Vector3(-50,0,-50));Check(C.Counter==2,"empty swing does not charge");
  Fresh();Lab("Strike",100f,100f,0f);Lab("AttackAt",new Vector3(10,0,10));Check(C.Counter==1&&C.Launched==4&&C.Skipped==16,"121 real plant hits produce 20 threshold attempts, capacity four, remainder one");Drain();
  Fresh();Lab("Strike",100f,.1f,1f);Lab("AttackAt",new Vector3(9,0,9));Check(C.Counter==4,"four critical hits count four plants, not extra crit charges");
  var untouched=(PlantHealth)Lab("PlantAt",1,1);untouched.TakeDamage(10,DamageType.Explosion);Check(C.Counter==4,"behavior damage never charges cursor wave");
  // A zero-damage direct attack is recorded by the shared feed but must not count.
  Lab("Strike",0f,.1f,0f);
  var report=new DirectAttackReport();typeof(DirectAttackReport).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(report,new object[]{Vector3.zero,1f,new List<GridObject>()});
  typeof(DirectAttackReport).GetMethod("Record",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(report,new object[]{FieldCells.Grid.GetGridObject(new GridPosition(1,1)),untouched,100,false,false});
  C.AttackEnd(report);Check(C.Counter==4,"blocked/non-damaging recorded hits do not charge");
  Fresh();var rng=typeof(RunEffects).GetField("random",BindingFlags.NonPublic|BindingFlags.Instance);var initial=rng.GetValue(Fx);var dirs=new HashSet<Vector3>();
  for(int n=0;n<40;n++){hits.Clear();var r=Launch(new Vector3(10.3f,0,10.2f));dirs.Add(r.Direction);CheckRoute(r,6,5,5);Drain();if(hits.Count!=6||hits.Distinct().Count()!=6)throw new Exception("full-field once-only route hits");}
  Check(dirs.Count==4,"all four cardinal directions selected from the cursor cell");Check(initial.Equals(rng.GetValue(Fx)),"private direction RNG leaves reward RNG untouched");
  Fresh();Add("buyuyen_ay");Add("dolunay");var forward=Launch(new Vector3(10,0,10));Lab("SetHealth",5,5,1);Lab("SetHealth",5+(int)forward.Direction.x,5+(int)forward.Direction.z,1);
  C.OnTimer(0,0,0,0);C.TryVisual(0,out var grown);Check(grown.HalfWidth==1,"cursor-cell harvest grows remaining wave");C.OnTimer(0,0,0,0);C.TryVisual(1,out var back);
  Check(back.Returning&&back.Direction==-forward.Direction&&back.StartX==forward.EndX&&back.StartZ==forward.EndZ&&back.EndX==5&&back.EndZ==5,"full moon retraces cursor route in reverse in both axes");Drain();Check(C.FullMoons==1,"one return only");
  for(int x=0;x<11;x++)for(int z=0;z<11;z++)if(x<4||x>6||z<4||z>6)FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();break;
 case 1:
  Fresh();var smallDirs=new HashSet<Vector3>();for(int n=0;n<32;n++){hits.Clear();var r=Launch(new Vector3(10,0,10));smallDirs.Add(r.Direction);CheckRoute(r,2,5,5);Drain();if(hits.Count!=2||hits.Any(h=>h.x<4||h.x>6||h.z<4||h.z>6))throw new Exception("small-field bounds");}
  Check(smallDirs.Count==4,"3x3 cursor routes choose all directions, stay inside open field and hit two cells");
  var frozen=Launch(new Vector3(10,0,10));GridUnlockManager.Instance.UnlockNextTier(11);hits.Clear();Drain();Check(hits.Count==2,"expansion does not extend born route");break;
 case 2:
  Fresh();var last=Launch(new Vector3(10,0,10));Call(P,"LateUpdate");Check(P.MoonItems==1,"one pooled visual per wave");Time.timeScale=0;C.TryVisual(0,out last);paused=last.Position;break;
 case 3:
  C.TryVisual(0,out var pausedView);Check(pausedView.Position==paused&&C.Steps==0,"pause freezes cursor wave");Time.timeScale=1;Call(Fx,"HandleRoundEnded");Call(P,"LateUpdate");Check(C.ActiveWaves==0&&P.MoonItems==0&&Fx.PendingTimers==0,"round cleanup removes waves and timers");
  Fresh();Launch(new Vector3(10,0,10));Call(Fx,"HandleRunStarted");Call(P,"LateUpdate");Check(P.MoonItems==0&&Fx.Active.Count==0,"restart clears cursor effects");Finish(null);return;
 }phase++;next=EditorApplication.timeSinceStartup+.12;}catch(Exception e){Finish(e.InnerException??e);}EditorApplication.QueuePlayerLoopUpdate();}
 static Vector3 paused;
 static void CheckRoute(CrescentEffect.VisualWave r,int steps,int x,int z){if(r.StartX!=x||r.StartZ!=z||r.Steps!=steps||Mathf.Abs(r.Direction.x)+Mathf.Abs(r.Direction.z)!=1)throw new Exception("cursor route endpoints/direction");}
 static void Finish(Exception e){SessionState.SetBool(Key,false);PlantHealth.AnyDamaged-=Damage;Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/MoonCursorVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
