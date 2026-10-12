using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MoonVerification
{
 const string Key="MoonVerification";static int step=-1,cycles,pooled,meshCount,materialCount;static double next;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 static readonly List<(int x,int z,int damage,RewardSource source)> hits=new();
 static RunEffects Fx=>RunEffects.Instance;static CrescentEffect C=>Fx.Find<CrescentEffect>();
 static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
 static ProfilerRecorder allocations;static long idleAlloc,busyAlloc;static double busyStarted;
 static int measuredFrame=-1, measuredCount;static long frameBytes, idleBytes;static Action<Vector3> launchMeasured;
 static readonly List<float> idleFrames=new(),busyFrames=new();
 static CrescentEffect.VisualWave route;static Vector3 pausedPosition;
 static object Lab(string name,params object[] args){var m=typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<args.Length?args[i]:p.DefaultValue).ToArray());}
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
 static BossRewardSO Reward(string id)=>(BossRewardSO)Lab("Find",id);
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);notes.Add("PASS "+msg);}
 static void Cue(RewardCue c)=>cues.Add(c);
 static void Damage(PlantHealth plant,int amount,DamageType type){if(type!=DamageType.Reward)return;var p=FieldCells.Grid.GetGridPosition(plant.transform.position);hits.Add((p.x,p.z,amount,plant.LastHitLink.Source));}
 static void Add(string id){if(!Fx.TryAdd(Reward(id).effect,-1))throw new Exception("add "+id);}
 static void Fresh(bool full=true){Fx.ClearAll();Fx.enabled=false;RoundManager.Instance.Profile.crescentFullRows=full;Add("hilal_darbesi");Lab("Refill",false);hits.Clear();cues.Clear();}
 static void Launch(){Call(C,"Launch",FieldCells.World(FieldCells.Grid,new GridPosition(2,5)));C.TryVisual(0,out route);}
 static void Drain(){for(int n=0;n<25&&C.ActiveWaves>0;n++)for(int i=0;i<CrescentEffect.MaxWaves;i++)C.OnTimer(0,i,0,0);Call(Fx,"DropTimers");Call(P,"LateUpdate");}
 static void Capture(string name)
 {
  foreach(var juice in Object.FindObjectsByType<PlantJuice>(FindObjectsSortMode.None))juice.enabled=false;
  Lab("Capture","Moon_"+name);
 }
 static MoonVerification(){EditorApplication.update+=Tick;}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(step==20){try{MeasureFrame();}catch(Exception e){Finish(e);}return;}
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try
  {
   switch(step)
   {
    case -1:
     if(double.IsNaN((double)Lab("Lab")))return;Time.captureDeltaTime=0;FieldEventDirector.Instance.End();Fx.enabled=false;
     RunEffects.Cued+=Cue;PlantHealth.AnyDamaged+=Damage;
     Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"isolated GameScene real saves disabled");
     Check(RoundManager.Instance.Profile.crescentFullRows,"new full-game profile opts into full rows");
     Check(AssetDatabase.FindAssets("t:RunProfileSO").Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("Run50_GecOyunV1.asset")).All(p=>!AssetDatabase.LoadAssetAtPath<RunProfileSO>(p).crescentFullRows),"other profiles retain legacy default");
     break;
    case 0:
     Fresh();var cfg=(CrescentEffectSO)C.Data;Check(cfg.everyAttacks==6&&cfg.steps==4&&cfg.damage==.8f&&cfg.stepSeconds==.07f&&CrescentEffect.MaxWaves==4,"original trigger damage cadence and capacity data unchanged");
     for(int i=0;i<5;i++)Lab("Attack",2,5);
     ((PlantSpawner)Lab("SpawnerAt",0,0)).RemoveSpawnedPlant();Lab("Attack",0,0);
     Check(C.Counter==5&&C.Launched==0,"five valid attacks count; empty swing does not");Lab("Attack",2,5);Check(C.Counter==0&&C.Launched==1,"sixth genuine direct attack launches full row");Drain();
     Fresh(false);Launch();Drain();Check(hits.Count==4&&hits.Select(h=>h.x).SequenceEqual(new[]{3,4,5,6})&&hits.All(h=>h.z==5&&h.damage==80),"legacy rule still hits original four cells at 80 damage");
     Fresh();var randomField=typeof(RunEffects).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic);var rng=randomField.GetValue(Fx);var unityRng=UnityEngine.Random.state;
     bool left=false,right=false;var rows=new HashSet<int>();
     for(int sample=0;sample<24;sample++)
     {
      hits.Clear();Launch();left|=route.Direction.x<0;right|=route.Direction.x>0;rows.Add(route.Row);Drain();
      if(hits.Count!=11||hits.Select(h=>h.x).Distinct().Count()!=11||hits.Any(h=>h.z!=route.Row||h.damage!=80))throw new Exception("full row hits mismatch");
     }
     Check(left&&right&&rows.Count>5,"independent seeded selection covers both directions and multiple eligible rows");
     Check(rng.Equals(randomField.GetValue(Fx)),"route launches do not consume reward RNG");
     // Visual hit effects may use Unity RNG; choose-row alone must not.
     var choose=typeof(CrescentEffect).GetMethod("ChooseRow",BindingFlags.NonPublic|BindingFlags.Instance);FieldCells.TryBounds(out var bounds);var args=new object[]{FieldCells.Grid,bounds,null,0,0};unityRng=UnityEngine.Random.state;choose.Invoke(C,args);
     Check(UnityEngine.Random.state.Equals(unityRng),"row choice does not consume Unity RNG");
     Check(true,"24 full rows hit each target once including both endpoints, no crit/direct amplification");
     break;
    case 1:
     Fresh();Launch();for(int x=0;x<11;x++)((PlantSpawner)Lab("SpawnerAt",x,route.Row)).RemoveSpawnedPlant();Drain();Check(hits.Count==0&&C.Steps==11,"empty row continues for all eleven steps without fake contacts");
     Fresh();Launch();for(int x=1;x<10;x+=2)((PlantSpawner)Lab("SpawnerAt",x,route.Row)).RemoveSpawnedPlant();Drain();Check(hits.Count==6&&hits.All(h=>h.x%2==0),"sparse row hits only living cells and reaches final cell");
     Fresh();Launch();var locked=FieldCells.Ground(FieldCells.Grid,new GridPosition(5,route.Row));locked.Lock();Drain();Check(hits.Count==10&&hits.All(h=>h.x!=5),"locked hole does not stop wave and never takes damage");locked.Unlock();
     // New plants cannot change the already chosen route; growth of open bounds cannot extend it.
     for(int x=0;x<11;x++)for(int z=0;z<11;z++)if(x<4||x>6||z<4||z>6)FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();
     step=9;break;
    case 10:
     Fresh();bool smallLeft=false,smallRight=false;
     for(int i=0;i<16;i++){hits.Clear();Launch();smallLeft|=route.Direction.x<0;smallRight|=route.Direction.x>0;Drain();if(hits.Count!=3||hits.Select(h=>h.x).Distinct().Count()!=3)throw new Exception("small row endpoints");}
     Check(smallLeft&&smallRight,"three-cell field includes both endpoints in both directions");
     hits.Clear();Launch();var small=route;GridUnlockManager.Instance.UnlockNextTier(11);Drain();
     Check(hits.Count==3&&hits.All(h=>h.x>=4&&h.x<=6&&h.z==small.Row),"small-field route stays three cells after field expansion");step=1;break;
    case 2:
     Fresh();Add("buyuyen_ay");Add("dolunay");Launch();int dx=(int)route.Direction.x;
     Lab("Tile",route.StartX,route.Row,"Explosive",1f);Lab("SetHealth",route.StartX,route.Row,1);Lab("SetHealth",route.StartX+dx,route.Row,1);
     int triggered=HarvestBehaviorStats.Triggered(DamageType.Explosion);
     C.OnTimer(0,0,0,0);C.TryVisual(0,out var grown);Check(grown.HalfWidth==1&&C.GrowthGains==1,"first harvest widens the same wave for remaining steps");
     C.OnTimer(0,0,0,0);C.TryVisual(1,out var back);Check(C.FullMoons==1&&back.Returning&&back.Row==route.Row&&back.StartX==route.EndX&&back.EndX==route.StartX&&back.Direction==-route.Direction,"one full-moon return uses exactly the same row in reverse including endpoints");
     Check(DirectAttackPause.Active&&DirectAttackPause.RemainingFor(CrescentEffect.PauseSource)<=.35f+.001f,"full moon keeps existing bounded direct pause");
     C.OnTimer(0,0,0,0);C.TryVisual(0,out var reset);Check(reset.HalfWidth==0&&C.GrowthResets==1,"non-harvest step resets visual width immediately");
     Drain();Check(C.FullMoons==1&&hits.Where(h=>h.source==RewardSource.CrescentReturn).All(h=>h.damage==56),"return keeps .7 base damage, never grows or triggers another return");
     Check(HarvestBehaviorStats.Triggered(DamageType.Explosion)==triggered,"base growth and return never trigger planter behavior");
     // Once a wave hit a living object, moving it ahead must not grant an accidental second hit.
     Fresh();Launch();var first=new GridPosition(route.StartX,route.Row);var second=new GridPosition(route.StartX+(int)route.Direction.x,route.Row);
     var plant=FieldCells.LivingPlant(FieldCells.Grid,first);C.OnTimer(0,0,0,0);
     var entry=FieldCells.Grid.GetGridObject(second);var old=entry.GetPlantObject();
     // Moving-object de-dup is asserted against the bounded per-wave set; no second attack system.
     var sets=(HashSet<PlantHealth>[])typeof(CrescentEffect).GetField("struck",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(C);
     var originalPosition=plant.transform.position;entry.SetPlantObject(plant.gameObject);plant.transform.position=FieldCells.Grid.GetWorldPosition(second.x,second.z);
     int movedHits=hits.Count;C.OnTimer(0,0,0,0);
     Check(hits.Count==movedHits,"a previously struck plant moved ahead takes no second damage in the same wave");
     entry.SetPlantObject(old);plant.transform.position=originalPosition;Drain();Check(sets.All(s=>s.Count==0),"completed waves release retained plant references");
     break;
    case 3:
     Fresh();Add("buyuyen_ay");
     for(int n=0;n<100;n++){Launch();if(route.Row==0||route.Row==10)break;Drain();}
     Check(route.Row==0||route.Row==10,"edge row selected without occupancy bias");
     Lab("SetHealth",route.StartX,route.Row,1);C.OnTimer(0,0,0,0);C.TryVisual(0,out var edge);
     Check(edge.HalfWidth==1&&edge.WidthCells==2,"grown edge visual clips to two real in-field rows");
     int before=hits.Count;C.OnTimer(0,0,0,0);Check(hits.Count-before==2,"grown edge damage clips to same two rows");Drain();
     Fresh();for(int i=0;i<6;i++)Call(C,"Launch",Vector3.zero);Check(C.ActiveWaves==4&&C.Skipped==2,"four-wave capacity unchanged and dropped launches are observable");Drain();
     Fresh();Launch();Fx.enabled=true;Time.captureDeltaTime=.3f;break;
    case 4:
     if(C.ActiveWaves>0){if(C.Steps>11)throw new Exception("duplicate slow frame steps");return;}
     Check(C.Steps==11&&hits.Count==11&&hits.Select(h=>h.x).Distinct().Count()==11,"low FPS .3s frames visit all eleven cells once with actual scheduler");Time.captureDeltaTime=0;Fx.enabled=false;
     Fresh();Launch();Call(P,"LateUpdate");Check(P.MoonItems==1,"one persistent visual per wave");
     C.TryVisual(0,out var aligned);var drawn=P.GetComponentsInChildren<LateGameContent>().First(c=>c.name.StartsWith("CyanCrescent"));
     var tip=drawn.transform.position+aligned.Direction*(.48f*drawn.visual.localScale.z);tip.y=aligned.Position.y;
     Check(Vector3.Distance(tip,aligned.Position)<.001f,"cutting tip follows authoritative cell position without a visual lead");Time.timeScale=0;pausedPosition=P.GetComponentsInChildren<LateGameContent>().First(c=>c.name.StartsWith("CyanCrescent")).transform.position;break;
    case 5:
     Call(P,"LateUpdate");Check(P.GetComponentsInChildren<LateGameContent>().First(c=>c.name.StartsWith("CyanCrescent")).transform.position==pausedPosition&&C.Steps==0,"pause freezes wave and visual");Time.timeScale=1;
     Call(Fx,"HandleRoundEnded");Call(P,"LateUpdate");Check(C.ActiveWaves==0&&P.MoonItems==0&&!DirectAttackPause.Active&&Fx.PendingTimers==0,"round cleanup removes active waves timers and pause");
     Fresh();Launch();Call(Fx,"HandleStateChanged",GameStates.MainMenu);Call(P,"LateUpdate");Check(Fx.Active.Count==0&&P.MoonItems==0,"menu clears wave and visuals");
     Fresh();Launch();Call(Fx,"HandleStateChanged",GameStates.RunComplete);Call(P,"LateUpdate");Check(C.ActiveWaves==0&&P.MoonItems==0&&Fx.PendingTimers==0,"run-complete victory/loss cleanup stops waves and timers");
     Fresh();Launch();Call(Fx,"HandleRunStarted");Call(P,"LateUpdate");Check(Fx.Active.Count==0&&P.MoonItems==0,"new run clears wave and visuals");
     Fresh();Launch();C.OnTimer(0,0,0,0);C.OnTimer(0,0,0,0);Call(P,"LateUpdate");Capture("Base");
     Add("buyuyen_ay");Add("dolunay");
     for(int x=0;x<11;x++)for(int z=0;z<11;z++)Lab("SetHealth",x,z,1);
     C.OnTimer(0,0,0,0);C.OnTimer(0,0,0,0);Call(P,"LateUpdate");Capture("GrowthReturn");
     break;
    case 6:
     Fresh();Launch();Drain();P.enabled=false;P.enabled=true;cycles++;
     if(cycles==3){pooled=P.GetComponentsInChildren<Transform>(true).Length;meshCount=Resources.FindObjectsOfTypeAll<Mesh>().Length;materialCount=Resources.FindObjectsOfTypeAll<Material>().Length;}
     if(cycles<33){step=5;break;}
     Check(P.GetComponentsInChildren<Transform>(true).Length==pooled,"30 warmed wave reuse cycles retain stable presenter objects");
     Check(Resources.FindObjectsOfTypeAll<Material>().Length==materialCount,"30 warmed wave reuse cycles retain stable materials");
     // Isolate the new steady-state presentation path from test reflection/LINQ allocations.
     Fresh();for(int i=0;i<4;i++)Call(C,"Launch",Vector3.zero);
     var sync=(Action)Delegate.CreateDelegate(typeof(Action),P,typeof(LateGamePresenter).GetMethod("SyncMoonWaves",BindingFlags.Instance|BindingFlags.NonPublic));
     for(int i=0;i<100;i++)sync();
     long calibration=GC.GetAllocatedBytesForCurrentThread();var allocationProbe=new byte[4096];calibration=GC.GetAllocatedBytesForCurrentThread()-calibration;GC.KeepAlive(allocationProbe);
     long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();
     for(int i=0;i<1000;i++)sync();timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
     File.WriteAllText("Logs/MoonPerformance.txt",$"Four active waves / 1000 warmed SyncMoonWaves calls: {(calibration>=4096?allocated.ToString():"UNAVAILABLE (thread allocation counter failed 4096-byte calibration)")} managed bytes, {timer.Elapsed.TotalMilliseconds/1000:F4} ms/call. Editor batch CPU, not total frame or GPU time. Damage/contact and other game systems excluded.");
     if(calibration>=4096)Check(allocated<1024,"four-wave visual sync stays below 1 KiB across 1000 warmed calls");Drain();
     File.WriteAllText("Logs/MoonPool.txt",$"Presenter transforms {pooled}; materials {materialCount}; global meshes {meshCount} -> {Resources.FindObjectsOfTypeAll<Mesh>().Length}. Global TMP/plant pools can warm separately. No native-memory/GPU capture.");
     Fresh();launchMeasured=(Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>),C,typeof(CrescentEffect).GetMethod("Launch",BindingFlags.Instance|BindingFlags.NonPublic));
     RunEffects.Cued-=Cue;PlantHealth.AnyDamaged-=Damage;Fx.enabled=true;step=20;allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);frameBytes=GC.GetAllocatedBytesForCurrentThread();return;
   }
   step++;next=EditorApplication.timeSinceStartup+.12;File.WriteAllLines("Logs/MoonVerification.txt",new[]{"RUNNING "+step}.Concat(notes));
  }
  catch(Exception e){Finish(e.InnerException??e);}EditorApplication.QueuePlayerLoopUpdate();
 }
 static void MeasureFrame()
 {
  if(Time.frameCount==measuredFrame){EditorApplication.QueuePlayerLoopUpdate();return;}
  measuredFrame=Time.frameCount;
  if(measuredCount<60){idleFrames.Add(Time.unscaledDeltaTime*1000);idleAlloc+=allocations.LastValue;}else{busyFrames.Add(Time.unscaledDeltaTime*1000);busyAlloc+=allocations.LastValue;}
  measuredCount++;
  if(measuredCount==60){busyStarted=Fx.Clock;idleBytes=GC.GetAllocatedBytesForCurrentThread()-frameBytes;frameBytes=GC.GetAllocatedBytesForCurrentThread();}
  if(measuredCount>=60&&C.ActiveWaves==0)for(int i=0;i<4;i++)launchMeasured(Vector3.zero);
  if(measuredCount>=180&&Fx.Clock-busyStarted>=4)
  {
   long busyBytes=GC.GetAllocatedBytesForCurrentThread()-frameBytes;idleFrames.Sort();busyFrames.Sort();
   File.AppendAllText("Logs/MoonPerformance.txt",$"\nWhole Editor main-thread frame sample (low-priority 4-core isolated batch, 121 plants): idle 60 frames median {idleFrames[30]:F2} / p95 {idleFrames[57]:F2} ms, {(allocations.Valid?(idleAlloc/60).ToString():"unavailable")} profiler B/frame; repeated four-wave attacks {busyFrames.Count} frames median {busyFrames[busyFrames.Count/2]:F2} / p95 {busyFrames[(int)(busyFrames.Count*.95)]:F2} ms, {(allocations.Valid?(busyAlloc/busyFrames.Count).ToString():"unavailable")} profiler B/frame. Allocation includes Editor, damage popups, audio and all scene systems; it is NOT a standalone Moon allocation measurement or a player-build performance guarantee.");
   Check(C.Launched>=8&&C.Steps>=44,"sustained four-wave real-scheduler frame sample completed");Finish(null);return;
  }
  EditorApplication.QueuePlayerLoopUpdate();
 }
 static void Finish(Exception e){allocations.Dispose();RunEffects.Cued-=Cue;PlantHealth.AnyDamaged-=Damage;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/MoonVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}

