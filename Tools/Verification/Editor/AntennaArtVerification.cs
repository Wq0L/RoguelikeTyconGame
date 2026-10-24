using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AntennaArtVerification
{
 const string Key="AntennaArtVerification";
 static int step=-1,cycles,objects,materials,meshes;static double next;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 static readonly List<int> circuitDamage=new();
 static void Damage(PlantHealth plant,int amount,DamageType type){if(type==DamageType.Reward)circuitDamage.Add(amount);}
 static void Colors(string name,bool red)
 {
  var content=P.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith(name)).ToArray();
  Check(content.Length>0,name+" is visible");
  var colors=content.SelectMany(c=>c.GetComponentsInChildren<MeshFilter>()).SelectMany(m=>m.sharedMesh.colors).Select(c=>c.gamma).ToArray();
  Check(colors.Length>0&&(red?colors.All(c=>c.r>.85f&&c.r>c.g*1.5f&&c.r>c.b*1.5f):colors.Any(c=>c.b>.85f&&c.g>.8f&&c.r<.5f)),name+(red?" uses red palette":" preserves cyan palette"));
 }
 static HashSet<int> warmMaterials,warmMeshes;
 static Vector3 pausePosition;static Quaternion pauseRotation;static float clock;
 static RunEffects Fx=>RunEffects.Instance;
 static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
 static AntennaEffect A=>Fx.Find<AntennaEffect>();static ConductiveLineEffect L=>Fx.Find<ConductiveLineEffect>();
 static LateGameContent[] Models()=>P.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith("FieldAntenna")).ToArray();
 static AntennaArtVerification(){EditorApplication.update+=Tick;}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static object Lab(string name,params object[] args)
 {var method=typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);var parameters=method.GetParameters();return method.Invoke(null,parameters.Select((p,i)=>i<args.Length?args[i]:p.DefaultValue).ToArray());}
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
 static void Check(bool ok,string note){if(!ok)throw new Exception(note);notes.Add("PASS "+note);}
 static BossRewardSO Reward(string id)=>(BossRewardSO)Lab("Find",id);
 static void Add(string id,int slot=-1){if(!Fx.TryAdd(Reward(id).effect,slot))throw new Exception("add "+id);Call(P,"Update");}
 static void Capture(string name)=>Lab("Capture","Antenna_"+name);
 static void Cue(RewardCue c)=>cues.Add(c);
 static void ClearVisuals(){P.enabled=false;P.enabled=true;Call(P,"Update");}
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try
  {
   switch(step)
   {
    case -1:
     if(double.IsNaN((double)Lab("Lab")))return;
     Time.captureDeltaTime=0;Fx.enabled=false;FieldEventDirector.Instance.End();RunEffects.Cued+=Cue;
     Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real saves disabled in isolated GameScene");
     Check(((AntennaEffectSO)Reward("elektrik_anteni").effect).interval==2.5f&&((AntennaEffectSO)Reward("elektrik_anteni").effect).reach==3,"actual antenna data: 2.5 seconds and three cells");
     var config=(ConductiveLineEffectSO)Reward("iletken_hat").effect;
     Check(config.interval==4&&config.secondAntennaPulses,"actual line data: four seconds, second antenna also pulses");
     Check(Resources.Load<LateGameArtSO>("LateGameArt").Prefab(LateGameSlot.CircuitReturn,LateGameVariant.Antenna).GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.z<.03f,"saved return-token mesh stays compact after editor/play reload");
     break;
    case 0:
     ModelPortrait();
     for(int side=0;side<4;side++)
     {
      Fx.ClearAll();Add("elektrik_anteni",AntennaSlots.Id(side,5));
      var m=Models().Single();var facing=AntennaSlots.Facing(AntennaSlots.Id(side,5));
      Check(Vector3.Dot(m.transform.forward,new Vector3(facing.x,0,facing.y))>.999f,"model faces inward on edge "+side);
      cues.Clear();int hits=Fx.Hits(RewardSource.Antenna);A.Tick(2.49f);Check(cues.Count==0,"no early attack on edge "+side);A.Tick(.01f);
      Check(Fx.Hits(RewardSource.Antenna)-hits==3&&cues.Count(c=>c.Kind==RewardCueKind.AntennaContact)==3,"three actual contact cues on edge "+side);
      FieldCells.TryBounds(out var bounds);var expected=new List<GridPosition>();AntennaSlots.Reach(bounds,AntennaSlots.Id(side,5),3,expected);
      Check(cues.Where(c=>c.Kind==RewardCueKind.AntennaContact).Select(c=>FieldCells.Grid.GetGridPosition(c.From)).All(c=>expected.Any(e=>e.x==c.x&&e.z==c.z)),"contact locations match actual reachable cells on edge "+side);
      Check(P.GetComponentsInChildren<LateGameContent>().Count(c=>c.name.StartsWith("AntennaSnap"))==1,"short attack is one bounded three-cell bolt on edge "+side);
      Capture("Edge"+side);ClearVisuals();
     }
     break;
    case 1:
     Fx.ClearAll();Add("elektrik_anteni",AntennaSlots.Id(AntennaSlots.West,5));Add("iletken_hat",AntennaSlots.Id(AntennaSlots.East,5));
     Check(Models().Length==2,"two models share family prefab");
     A.Tick(2.25f);Call(P,"Update");Check(Models().All(m=>m.antenna.Charge>0),"both models charge from real read-only countdown before firing");
     A.Tick(.25f);ClearVisuals();cues.Clear();var before=Fx.Hits(RewardSource.Line);L.Tick(4);
     Check(Fx.Hits(RewardSource.Line)-before==11&&cues.Count(c=>c.Kind==RewardCueKind.LineContact)==11,"full line contacts match eleven actual hits");
     Check(P.GetComponentsInChildren<LateGameContent>().Count(c=>c.name.StartsWith("ConductiveFlash"))==12,"line follows cell path plus antenna tips");Colors("ConductiveFlash",false);Capture("Line");
     // An empty slot still belongs to the path, but must not emit a false contact.
     Lab("SetHealth",5,5,1);FieldCells.LivingPlant(FieldCells.Grid,new GridPosition(5,5)).TakeDamage(999999,DamageType.Reward);cues.Clear();L.Pulse(false,1);
     Check(cues.Count(c=>c.Kind==RewardCueKind.LineContact)==10,"empty line cell emits no contact");
     Lab("Refill",false);
     break;
    case 2:
     Add("kapali_devre");Lab("Tile",1,5,"Explosive",1f);Lab("SetHealth",1,5,1);ClearVisuals();cues.Clear();L.Pulse(false,1);
     Check(cues.Count(c=>c.Kind==RewardCueKind.CircuitReturn)==1&&Fx.PendingTimers==1,"successful real circuit schedule emits one return cue");Capture("Return");
     // Execute the scheduled effect's real callback; general regression separately tests its .35 s timer.
     ClearVisuals();circuitDamage.Clear();PlantHealth.AnyDamaged+=Damage;
     int extraHits=Fx.Hits(RewardSource.LineExtra);
     Fx.Find<ClosedCircuitEffect>().OnTimer(0,0,0,0);PlantHealth.AnyDamaged-=Damage;
     Check(cues.Count(c=>c.Kind==RewardCueKind.LineExtraPulse)==1,"real circuit callback emits one additional line pulse");
     Check(Fx.Hits(RewardSource.LineExtra)-extraHits==10&&circuitDamage.Count==10&&circuitDamage.All(d=>d==27),"extra pulse applies 27 damage to ten living line cells");
     Colors("CircuitDouble",true);Colors("ContactDouble",true);
     Check(P.GetComponentsInChildren<LateGameContent>().Any(c=>c.name.StartsWith("CircuitDouble")&&c.antenna.doubleStrike),"extra line uses split shape and double-beat animation");Capture("Circuit");
     ClearVisuals();L.Pulse(false,1);Colors("ConductiveFlash",false);Capture("NormalAfterCircuit");
     Fx.ClearAll();Add("elektrik_anteni",5);Add("iletken_hat",105);A.Tick(2.35f);Call(P,"Update");
     Time.timeScale=0;pausePosition=Models()[0].antenna.mast.localPosition;pauseRotation=Models()[0].antenna.mast.localRotation;clock=A.SecondsUntilPulse(0);break;
    case 3:
     Check(Models()[0].antenna.mast.localPosition==pausePosition&&Models()[0].antenna.mast.localRotation==pauseRotation&&A.SecondsUntilPulse(0)==clock,"pause freezes charge and model pose");Time.timeScale=1;
     // Simulate smaller unlocked field, then expand through the actual unlock API.
     for(int x=0;x<11;x++)for(int z=0;z<11;z++)if(x<2||x>8||z<2||z>8)FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();
     Call(P,"StepAntennas",0f,true);var model=Models()[0];var small=model.transform.position;
     GridUnlockManager.Instance.UnlockNextTier(11);Call(P,"StepAntennas",0f,true);
     Check(Models()[0]==model&&Mathf.Abs(model.transform.position.x-small.x)>3.9f,"field expansion moves same pooled model to new real edge");
     ClearVisuals();L.Pulse(false,1);Capture("Expanded");
     Check(P.ActiveDecorations<=LateGamePresenter.DecorationCapacity,"decorations respect shared cap");
     break;
    case 4:
     Placement();
     // Common damage labels are lazily created and capped at 121. Warm that unrelated pool
     // fully before measuring retention so its first use is not mistaken for an antenna leak.
     for(int i=0;i<121;i++)VFXManager.Instance.PlayHit(new Vector3(-1000-i*3,0,-1000),100,false);
     Check(VFXManager.Instance.TextCreated<=121,"common damage-text pool remains capped at 121 during stress warmup");
     step=8;break;
    case 9:
     Fx.ClearAll();Add("elektrik_anteni",5);Add("iletken_hat",105);A.Tick(2.5f);L.Pulse(false,1);L.Pulse(true,.6f);
     Fx.ClearAll();ClearVisuals();cycles++;
     if(cycles==3){objects=P.GetComponentsInChildren<Transform>(true).Length;materials=Resources.FindObjectsOfTypeAll<Material>().Length;meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;warmMaterials=Resources.FindObjectsOfTypeAll<Material>().Select(m=>m.GetInstanceID()).ToHashSet();warmMeshes=Resources.FindObjectsOfTypeAll<Mesh>().Select(m=>m.GetInstanceID()).ToHashSet();}
     if(cycles<33){step=8;break;}
     Check(P.GetComponentsInChildren<Transform>(true).Length==objects,"30 warmed cycles retain bounded pooled object count");
     var finalMaterials=Resources.FindObjectsOfTypeAll<Material>();var finalMeshes=Resources.FindObjectsOfTypeAll<Mesh>();
     File.WriteAllLines("Logs/AntennaRetentionDetail.txt",new[]{$"Materials {materials} -> {finalMaterials.Length}; Meshes {meshes} -> {finalMeshes.Length}"}.Concat(finalMaterials.Where(m=>!warmMaterials.Contains(m.GetInstanceID())).Select(m=>"new material: "+m.name)).Concat(finalMeshes.Where(m=>!warmMeshes.Contains(m.GetInstanceID())).Select(m=>"new mesh: "+m.name)));
     Check(finalMaterials.Length==materials&&finalMeshes.Length==meshes,"30 warmed cycles retain stable material and mesh counts: "+materials+"/"+meshes+" -> "+finalMaterials.Length+"/"+finalMeshes.Length);
     Check(P.AntennaItems==0&&P.Flashes==0&&P.ActiveDecorations==0,"cleanup leaves no active antenna or effect");
     File.WriteAllText("Logs/AntennaPool.txt",$"3 warmup + 30 measured cycles. Presenter transforms={objects}, materials={materials}, meshes={meshes}. Stable counts; not a native-memory profiler capture.");Finish(null);return;
   }
   step++;next=EditorApplication.timeSinceStartup+(step==2||step==4?2.2:.08);File.WriteAllLines("Logs/AntennaArtVerification.txt",new[]{"RUNNING "+step}.Concat(notes));
  }
  catch(Exception e){Finish(e.InnerException??e);}EditorApplication.QueuePlayerLoopUpdate();
 }
 static void Placement()
 {
  var boss=BossRewardManager.Instance;boss.ClearAll();Lab("Take","elektrik_anteni",5);Call(P,"Update");
  // A controlled valid offer, then the real BeginPlacement / Cancel paths. General regression
  // separately exercises the full boss calendar, reward card, confirmation and restart flow.
  var reward=Reward("iletken_hat");var offer=(List<BossRewardSO>)typeof(BossRewardManager).GetField("offer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(boss);offer.Clear();offer.Add(reward);
  typeof(BossRewardManager).GetField("offerRound",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(boss,23);
  typeof(BossRewardManager).GetField("offerPrepared",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(boss,true);
  typeof(BossRewardManager).GetProperty("IsPending").GetSetMethod(true).Invoke(boss,new object[]{true});
  Check(!boss.NeedsPlacement(reward)&&!boss.BeginPlacement(reward),"second antenna requires no separate placement");
  Check(boss.Choose(reward,RunEffectSO.NoPlacement),"line reward automatically places the second antenna");Call(P,"Update");
  Check(A.Count==2&&A.Slot(0)==5&&A.Slot(1)==105,"automatic second antenna preserves the selected row");
  Check(Models().Length==2,"automatic placement presents both antennas");Capture("Placement");
  boss.ClearAll();Fx.ClearAll();ClearVisuals();
 }
 static void ModelPortrait()
 {
  var prefab=Resources.Load<LateGameArtSO>("LateGameArt").Prefab(LateGameSlot.Antenna,LateGameVariant.Antenna);
  var model=Object.Instantiate(prefab);model.transform.position=new Vector3(-1000,0,-1000);
  foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
  model.GetComponent<LateGameContent>().Show();
  var go=new GameObject("Antenna portrait camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=1.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(48,57,66,255);
  camera.transform.rotation=Quaternion.Euler(22,155,0);camera.transform.position=model.transform.position+Vector3.up*1.05f-camera.transform.forward*8;
  var rt=new RenderTexture(640,640,24);rt.Create();var old=RenderTexture.active;var image=new Texture2D(640,640,TextureFormat.RGB24,false);
  try{UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes("Logs/Antenna_Model.png",image.EncodeToPNG());}
  finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);Object.DestroyImmediate(model);}
 }
 static void Finish(Exception e){RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;File.WriteAllLines("Logs/AntennaArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
