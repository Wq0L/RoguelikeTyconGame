using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BountyBuffVerification
{
 const string Key="BountyBuffVerification";static int step=-1;static double next;static GridPosition cell;static PlantSpawner spawner;static float normalInterval,at;static int harvests;static Vector3[] pose;
 static int cycles,poolObjects,materials,meshes;
 static readonly List<string> notes=new();
 static Renderer[] plantRenderers;static Material[] originalMaterials;
 static FieldEventDirector D=>FieldEventDirector.Instance;
 static WanderingBountyEvent E=>D.Current as WanderingBountyEvent;
 static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
 static FieldEventHud H=>Object.FindAnyObjectByType<FieldEventHud>();
 static BountyBuffVerification(){EditorApplication.update+=Tick;}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 public static void RepairGoldAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  var tint=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/BountyPlantGold.mat");
  tint.shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/BountyPlantGold.shader");
  EditorUtility.SetDirty(tint);AssetDatabase.SaveAssets();RunBatch();
 }
 static object Lab(string name,params object[] args)=>typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
 static void Check(bool value,string note){if(!value)throw new Exception(note);notes.Add("PASS "+note);}
 static PlantHealth Plant(GridPosition c)=>(PlantHealth)Lab("PlantAt",c.x,c.z);
 static LateGameContent[] Markers()=>P.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith("BountyMarker")).ToArray();
 static void Capture(string name){Call(H,"Update");Lab("Capture","BountyBuff_"+name);}
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try
  {
   switch(step)
   {
    case -1:if(double.IsNaN((double)Lab("Lab")))return;Time.captureDeltaTime=0;Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real saves disabled");break;
    case 0:
     Check(D.StartNow(FieldEventKind.WanderingBounty,0,41),"start real plan");Call(E,"Tick",.01f);cell=E.Zone(0)[0];spawner=(PlantSpawner)Lab("SpawnerAt",cell.x,cell.z);normalInterval=(float)Call(spawner,"GetEffectiveSpawnInterval");
     Check(FieldEventDirector.BountyAt(cell)==null,"no benefit during preparation");
     var existing=spawner.SpawnedPlant;
     Call(P,"LateUpdate");
     Check(E.Phase==FieldEventPhase.Preparing&&BountyPlantTint.DrawnPlants==9&&BountyPlantTint.DrawnMeshes>=9,"all nine existing plants tint on first visible preparation frame without damage or respawn");
     Check(existing==spawner.SpawnedPlant&&E.BonusHarvests==0&&E.AcceleratedSpawns==0,"immediate tint keeps existing plant and does not require harvest or spawn");
     Capture("01_Prepare");
     Call(E,"Tick",3f);Call(P,"LateUpdate");
     Check(E.Phase==FieldEventPhase.Emerging&&BountyPlantTint.DrawnPlants==9,"existing plants stay gold throughout emergence");break;
    case 1:
     if(E.Phase!=FieldEventPhase.Active)return;
     Check(Mathf.Approximately((float)Call(spawner,"GetEffectiveSpawnInterval"),normalInterval*.5f),"active region halves normal production interval");
     Check(Markers().Length==9&&E.Targets.Count==0,"nine signs and no attack target");
     var curtains=P.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith("BountyCurtain")).ToArray();
     Check(curtains.Length==1&&Markers().All(m=>m.bounty.aurora==null&&m.bounty.scan!=null),"one outer curtain and nine independent scanner robots");
     Check(Mathf.Abs(curtains[0].visual.localScale.x-FieldCells.CellSize*3)<.01f&&Mathf.Abs(curtains[0].visual.localScale.z-FieldCells.CellSize*3)<.01f,"curtain covers full 3x3 footprint");
     plantRenderers=E.Zone(0).SelectMany(c=>Plant(c).GetComponentsInChildren<Renderer>()).ToArray();originalMaterials=plantRenderers.Select(r=>r.sharedMaterial).ToArray();
     BountyPlantTint.Draw(E);Check(BountyPlantTint.DrawnPlants==9&&BountyPlantTint.DrawnMeshes>=9,"nine already-existing regional plants submit actual gold meshes");
     GoldPixels();
     Check(plantRenderers.Select((r,i)=>r.sharedMaterial==originalMaterials[i]).All(x=>x),"tint does not replace original plant materials");
     Check(E.BonusHarvests==0&&E.AcceleratedSpawns==0,"no six-harvest gate or fabricated reward");Capture("02_Active");
     RewardCheck();
     Lab("Strike",100f,.05f,0f);Lab("SetHealth",cell.x,cell.z,1);harvests=E.BonusHarvests;Lab("Attack",cell.x,cell.z);
     Check(E.BonusHarvests==harvests+1&&spawner.SpawnedPlant==null,"actual direct kill pays once and leaves empty slot");
     // Advance only the existing timer to its accelerated boundary; use actual Update/TrySpawnPlant.
     typeof(PlantSpawner).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(spawner,(float)Call(spawner,"GetEffectiveSpawnInterval"));Call(spawner,"Update");
     Check(spawner.SpawnedPlant!=null&&E.AcceleratedSpawns==1,"empty slot respawns through normal production without six kills");
     var spawned=spawner.SpawnedPlant;Call(spawner,"Update");Check(spawner.SpawnedPlant==spawned&&E.AcceleratedSpawns==1,"full slot never duplicates");Capture("03_Respawn");
     Lab("SetHealth",cell.x,cell.z,1);harvests=E.BonusHarvests;Plant(cell).TakeDamage(1000000,DamageType.Explosion);
     Check(E.BonusHarvests==harvests+1,"behavior harvest also benefits from passive area");
     Time.timeScale=0;at=E.PhaseTime;step=19;break;
    case 20:pose=Markers().Select(m=>m.bounty.pollen.position).ToArray();break;
    case 21:Check(E.PhaseTime==at&&Markers().Select((m,i)=>m.bounty.pollen.position==pose[i]).All(x=>x),"pause freezes region and markers");Time.timeScale=1;step=2;break;
    case 3:
     var old=E.Zone(0).ToArray();Call(E,"Tick",E.MoveInterval);var left=old.First(c=>!E.Zone(0).Any(n=>n.x==c.x&&n.z==c.z));
     Check(FieldEventDirector.BountyAt(left)==null&&E.Zone(0).All(c=>FieldEventDirector.BountyAt(c)==E),"movement instantly updates benefit membership");
     Call(P,"LateUpdate");
     Check(!BountyPlantTint.Covers(E,left)&&E.Zone(0).All(c=>BountyPlantTint.Covers(E,c))&&BountyPlantTint.DrawnMeshes>0,"movement updates tint on the same frame without a hit or respawn");
     var leftSpawner=(PlantSpawner)Lab("SpawnerAt",left.x,left.z);float outside=(float)Call(leftSpawner,"GetEffectiveSpawnInterval");D.End();Check(Mathf.Approximately(outside,(float)Call(leftSpawner,"GetEffectiveSpawnInterval")),"old cell already uses normal spawn interval before event ends");
     Check(P.EventItems==0&&P.Flashes==0&&FieldEventDirector.BountyAt(cell)==null,"end removes all benefits and visuals");
     BountyPlantTint.Draw(null);Check(BountyPlantTint.DrawnPlants==0,"no tint submissions after region is gone");
     Check(Mathf.Approximately(normalInterval,(float)Call(spawner,"GetEffectiveSpawnInterval")),"cleanup restores normal spawn timing");
     Check(D.StartNow(FieldEventKind.WanderingBounty,0,41),"restart");Call(E,"Tick",.01f);Call(E,"Tick",3f);Call(E,"Tick",.8f);
     Check(E.BonusHarvests==0&&E.AcceleratedSpawns==0,"restart counters reset");Call(E,"Tick",E.MoveInterval);Capture("04_Moved");
     Call(D,"HandleStateChanged",GameStates.MainMenu);Check(D.Current==null&&P.ActiveDecorations==0,"menu cleanup");break;
    case 4:
     Check(D.StartNow(FieldEventKind.WanderingBounty,0,41),"start before run reset");Call(E,"Tick",.01f);Call(D,"HandleRunStarted");Check(D.Current==null&&P.EventItems==0,"run reset cleanup");
     Check(D.StartNow(FieldEventKind.WanderingBounty,0,41),"start expiry test");Call(E,"Tick",.01f);Call(E,"Tick",3f);Call(E,"Tick",.8f);cell=E.Zone(0)[0];Call(E,"Tick",30f);Check(E.Phase==FieldEventPhase.Active,"bounty remains active beyond old thirty-second timeout");D.End();break;
    case 5:
     D.StartNow(FieldEventKind.WanderingBounty,0,41+cycles);Call(E,"Tick",.01f);Call(E,"Tick",3f);Call(E,"Tick",.8f);
     for(int i=0;i<5;i++)Call(E,"Tick",E.MoveInterval);
     var stressCells=E.Zone(0);foreach(var c in stressCells){E.RecordSpawn(c);E.RecordHarvest(c);}
     D.End();BountyPlantTint.Draw(null);cycles++;
     if(cycles==3)
     {
      poolObjects=P.GetComponentsInChildren<Transform>(true).Length;
      materials=Resources.FindObjectsOfTypeAll<Material>().Length;meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
     }
     if(cycles<43){step=4;break;}
     Check(P.GetComponentsInChildren<Transform>(true).Length==poolObjects,"40 warmed start/move/effect/end cycles keep pool object count stable");
     Check(Resources.FindObjectsOfTypeAll<Material>().Length==materials,"40 warmed cycles create no accumulating materials");
     Check(Resources.FindObjectsOfTypeAll<Mesh>().Length==meshes,"40 warmed cycles create no accumulating meshes");
     Check(P.ActiveDecorations==0&&P.Flashes==0&&P.EventItems==0&&BountyPlantTint.DrawnPlants==0,"stress cleanup has zero live signs effects and tint");
     File.WriteAllText("Logs/BountyPoolStress.txt",$"3 warmup + 40 measured cycles, five region moves and nine synthetic spawn/harvest cues per cycle. Pooled transforms={poolObjects}; materials={materials}; meshes={meshes}; counts unchanged. Retained pool is intentional. This is bounded object-retention testing, not a long-session native-memory profiler capture.");
     Finish(null);return;

   }
   step++;next=EditorApplication.timeSinceStartup+.04;File.WriteAllLines("Logs/BountyBuffVerification.txt",new[]{"RUNNING step "+step}.Concat(notes));
  }
  catch(Exception ex){Finish(ex.InnerException??ex);}EditorApplication.QueuePlayerLoopUpdate();
 }
 static void RewardCheck()
 {
  var plant=Plant(cell);var resource=plant.GetComponent<PlantResource>();var type=plant.Data.resourceType;
  // Same live plant and same stats. Invoke the existing payout path to measure exact deltas without rerolling plants.
  var e=E;D.End();int r0=ResourceManager.Instance.GetResourceAmount(type);double x0=ProgressionManager.Instance.TotalXPEarned;long s0=HarvestScoreManager.Instance.TotalScore;
  Call(resource,"GiveReward");int r1=ResourceManager.Instance.GetResourceAmount(type);double x1=ProgressionManager.Instance.TotalXPEarned;long s1=HarvestScoreManager.Instance.TotalScore;
  D.StartNow(FieldEventKind.WanderingBounty,0,41);Call(E,"Tick",.01f);Call(E,"Tick",3f);Call(E,"Tick",.8f);
  Call(resource,"GiveReward");int r2=ResourceManager.Instance.GetResourceAmount(type);double x2=ProgressionManager.Instance.TotalXPEarned;long s2=HarvestScoreManager.Instance.TotalScore;
  Check(r1>r0&&r2-r1==2*(r1-r0),"same plant resource payout exactly doubles");Check(x1>x0&&Math.Abs((x2-x1)-2*(x1-x0))<.001,"same plant XP payout exactly doubles");Check(s2-s1==s1-s0,"score is not doubled");
  var outside=Enumerable.Range(0,11).SelectMany(x=>Enumerable.Range(0,11).Select(z=>new GridPosition(x,z))).First(c=>FieldEventDirector.BountyAt(c)==null);
  Check(FieldEventDirector.BountyAt(outside)==null,"outside plants have no bounty multiplier");Call(H,"Update");Check(H.Text.Contains("Kaynak ×2")&&H.Text.Contains("XP ×2")&&!H.Text.Contains("/6"),"HUD shows passive resource XP and speed, no obsolete counter");
 }
 static void GoldPixels()
 {
  Check(Resources.Load<Material>("BountyPlantGold").shader==AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/BountyPlantGold.shader"),"gold material references the actual gold shader asset");
  // Render an existing plant mesh/material off-field, then submit the same gold overlay.
  // This verifies visible pixels, not merely the region-membership counter.
  var source=Plant(cell).GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<MeshRenderer>()?.sharedMaterial?.HasProperty("_ToonFlash")==true);
  var go=new GameObject("Tint pixel fixture");go.layer=30;go.transform.position=new Vector3(-1000,0,-1000);
  go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
  var cameraObject=new GameObject("Tint pixel camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;
  camera.orthographicSize=Mathf.Max(.5f,r.bounds.extents.magnitude*1.2f);camera.transform.rotation=Camera.main.transform.rotation;camera.transform.position=r.bounds.center-camera.transform.forward*10;camera.nearClipPlane=.1f;camera.farClipPlane=20;
  var target=new RenderTexture(256,256,24);target.Create();var old=RenderTexture.active;var texture=new Texture2D(256,256,TextureFormat.RGB24,false);
  try
  {
   Color32[] Read(){UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();return texture.GetPixels32();}
   var before=Read();File.WriteAllBytes("Logs/BountyTint_Before.png",texture.EncodeToPNG());
   for(int s=0;s<source.sharedMesh.subMeshCount;s++)Graphics.DrawMesh(source.sharedMesh,go.transform.localToWorldMatrix,Resources.Load<Material>("BountyPlantGold"),30,camera,s,null,false,false,false);
   var after=Read();File.WriteAllBytes("Logs/BountyTint_After.png",texture.EncodeToPNG());
   int gold=0;for(int i=0;i<before.Length;i++)if(after[i].r-before[i].r>15&&after[i].r-after[i].b>30)gold++;
   Check(gold>100,"gold overlay changes visible existing-plant pixels: "+gold);
  }
  finally{RenderTexture.active=old;target.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(target);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(go);}
 }
 static void Finish(Exception ex){SessionState.SetBool(Key,false);Time.timeScale=1;File.WriteAllLines("Logs/BountyBuffVerification.txt",new[]{ex==null?"PASS "+notes.Count+" checks":"FAIL "+ex}.Concat(notes));EditorApplication.Exit(ex==null?0:1);}
}
