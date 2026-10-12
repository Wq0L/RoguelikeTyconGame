using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ShellArtVerification
{
 const string Key="ShellArtVerification";static int step=-1,route;static double next,started;static float at,phase,closure;static LateGameShell[] pieces;static LateGameContent node;static int nodeId;static float openScale;
 static readonly List<string> notes=new();
 static FieldEventDirector D=>FieldEventDirector.Instance;
 static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
 static GraftedShellEvent E=>D.Current as GraftedShellEvent;
 static ShellArtVerification(){EditorApplication.update+=Tick;}
 public static void BuildAndRunBatch(){ShellArtBuilder.Build();RunBatch();}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static object Lab(string name,params object[] args)=>typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static void Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(obj,args);
 static void Check(bool value,string text){if(!value)throw new Exception(text);notes.Add("PASS route "+route+": "+text);}
 static LateGameShell[] Shells()=>P.GetComponentsInChildren<LateGameShell>();
 static void Capture(string name)=>Lab("Capture","Shell_"+route+"_"+name);
 static void Hit(int index){var cell=E.Targets[index].Cell;Lab("Attack",cell.x,cell.z);}
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;started=next;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try
  {
   if(EditorApplication.timeSinceStartup-started>220)throw new Exception("timeout step "+step);
   switch(step)
   {
    case -1:if(double.IsNaN((double)Lab("Lab")))return;Time.captureDeltaTime=0;Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real save disabled");break;
    case 0:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,route==0?42:route==1?12:21),"start real shell plan");
     var cfg=D.Plan.graftedShell;Check(cfg.targetDurability==4&&cfg.choiceSeconds==8&&cfg.closedSeconds==20&&cfg.timing.prepareSeconds==6&&cfg.timing.emergeSeconds==1.2f,"mechanical values unchanged");break;
    case 1:
     if(E.Phase!=FieldEventPhase.Preparing||E.PhaseTime<.7f)return;
     Check(!E.ZoneClosed(0)&&!E.ZoneClosed(1),"preparation does not block production");
     Check(Shells().Count(x=>x.target)==2,"both targets visible during preparation");
     Check(Shells().Count(x=>!x.target)==E.Zone(0).Count+E.Zone(1).Count,"each region point has a shell motif");
     Check(Shells().Where(x=>x.target&&x.zone==0).Single().warning.gameObject.activeSelf,"default A warning visible");
     Check(Object.FindAnyObjectByType<FieldEventHud>().LegendShown,"A/B HUD legend visible");
     Check(Object.FindAnyObjectByType<FieldEventHud>().GetComponentsInChildren<ShellMotifGraphic>().Length==2,"HUD uses same two polygon motifs");
     if(route==0)Capture("01_Prepare");break;
    case 2:
     if(E.Phase!=FieldEventPhase.Active)return;
     node=Shells().Single(x=>x.target&&x.zone==(route==1?1:0)).GetComponent<LateGameContent>();
     if(route==0)nodeId=node.GetInstanceID();
     if(route==2)Check(nodeId==node.GetInstanceID(),"A target reused from pool");
     Check(node.damageStages.All(x=>!x.activeSelf)&&!EventHitChecks.HasFlash(node),"pool appearance and damage reset");
     Check(Shells().All(x=>x.State==0&&Mathf.Abs(x.Closure)<.001f),"A/B animation state reset");
     Check(Shells().Where(x=>!x.target).All(x=>x.shutters.All(v=>v.localScale.y<.34f)),"pooled vines return to low preparation pose");
     Check(Shells().Where(x=>!x.target).All(x=>x.shutters.SelectMany(v=>v.GetComponent<MeshFilter>().sharedMesh.vertices).All(v=>new Vector2(v.x,v.z).magnitude<.98f)),"vine geometry stays inside its cell footprint");
     Check(P.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.shader.name=="ClickerGame/ThornComicToon"),"all art and VFX use shared toon");
     Check(Shells().Where(x=>x.target).All(x=>x.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertices.All(v=>Camera.main.WorldToViewportPoint(f.transform.TransformPoint(v)).y<.915f))),"targets clear top counters");
     Capture("02_Choice");if(route==0)step=19;
     break;
    case 20:
     Hit(0);Check(E.Targets[0].Remaining==3&&EventHitChecks.HasFlash(node),"real direct hit flashes and consumes one durability");
     Time.timeScale=0;node.visual.localScale=Vector3.one;node.Step(.07f,.6f);
     Check(node.visual.localScale.y<.95f,"hit presentation squashes at 70ms");Capture("03_Hit");Time.timeScale=1;step=2;break;
    case 3:
     if(route==0){Hit(0);Check(node.damageStages.Any(x=>x.activeSelf),"damage reveals cracks");}     pieces=Shells().Where(x=>!x.target).ToArray();
     if(route<2){int index=route==0?0:1;while(!E.Decided)Hit(index);}
     else{Call(E,"Tick",8f);}
     int closed=route==0?1:0;
     Check(D.Zones.BlockedCells==E.Zone(closed).Count,"exact closed region effect count");
     Check(E.Decided&&E.ClosedZone==closed&&E.ZoneClosed(closed)&&!E.ZoneClosed(1-closed),"correct region blocks immediately");
     Check(E.DecidedByPlayer==(route<2),"player choice versus timeout path correct");
     Check(pieces.Any(x=>x.zone!=closed&&x.State==2),"saved region opens independently of gameplay");
     Check(pieces.Any(x=>x.zone==closed&&x.Closure<1),"logical closure precedes visual closure");at=Time.time;break;
    case 4:
     if(Time.time-at<.16f)return;
     Capture("04_Closing");Time.timeScale=0;phase=E.PhaseTime;closure=pieces.First(x=>x.zone==E.ClosedZone).Closure;break;
    case 5:
     Check(E.PhaseTime==phase&&pieces.First(x=>x.zone==E.ClosedZone).Closure==closure,"pause freezes closing and logic");Time.timeScale=1;at=Time.time;break;
    case 6:
     if(Time.time-at<.8f)return;
     Check(Shells().All(x=>!x.target&&x.zone==E.ClosedZone&&x.State==1&&x.Closure>.99f),"only closed region remains after saved release");Check(Shells().All(x=>x.shutters.All(v=>v.localScale.y>.99f)),"closed vines rise around every affected pot");Capture("05_Closed");
     Call(E,"Tick",20f);Check(D.Zones.BlockedCells==0&&!E.ZoneClosed(0)&&!E.ZoneClosed(1),"expiry removes logical closure immediately");
     Check(Shells().Length>0&&Shells().All(x=>x.State==2),"reopening tail remains after production resumes");at=Time.time;break;
    case 7:
     if(Time.time-at<.14f)return;Capture("06_Open");Time.timeScale=0;openScale=Shells()[0].GetComponent<LateGameContent>().visual.localScale.y;step=29;break;
    case 30: Check(Shells()[0].GetComponent<LateGameContent>().visual.localScale.y==openScale,"pause freezes reopening tail");Time.timeScale=1;D.End();Check(P.EventItems==0&&P.ActiveDecorations==0&&Shells().Length==0,"hard cleanup clears all tails");
     if(route<2){route++;step=-1;break;}step=7;break;
    case 8:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,42),"restart ready");Call(D.Current,"Tick",.01f);Call(D.Current,"Tick",6f);Call(D.Current,"Tick",1.2f);Call(D.Current,"Tick",8f);
     Check(E.ZoneClosed(0),"restart default closure active");Call(D,"HandleStateChanged",GameStates.MainMenu);
     Check(D.Current==null&&P.ActiveDecorations==0&&Shells().Length==0&&D.Zones.BlockedCells==0,"menu callback cleans closure and visuals");break;
    case 9:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,42),"start before run restart");Call(D.Current,"Tick",.01f);Call(D,"HandleRunStarted");Check(D.Current==null&&P.ActiveDecorations==0,"run restart callback clears state");break;
    case 10:
     var art=Resources.Load<LateGameArtSO>(LateGameArtSO.ResourcePath);
     foreach(var v in new[]{LateGameVariant.ShellA,LateGameVariant.ShellB})
     foreach(var sound in new[]{LateGameSound.EventPrepared,LateGameSound.ShellChoice,LateGameSound.ShellClose,LateGameSound.ShellOpen,LateGameSound.TargetHit,LateGameSound.TargetBroken})Check(art.Sound(sound,v).clip!=null,"sound bound "+v+" "+sound);
     WriteBudget();Finish(null);return;
   }
   step++;next=EditorApplication.timeSinceStartup+.035;File.WriteAllLines("Logs/ShellArtVerification.txt",new[]{"RUNNING step "+step}.Concat(notes));
  }
  catch(Exception ex){Finish(ex.InnerException??ex);}
  EditorApplication.QueuePlayerLoopUpdate();
 }
 static void WriteBudget()
 {
  var lines=new List<string>{"| Model | Vertex (kontursuz) | Üçgen (kontursuz) | Konturlarla üçgen | Renderer |","|---|---:|---:|---:|---:|"};
  foreach(var path in Directory.GetFiles("Assets/Art/LateGame/GraftedShell","*.prefab"))
  {
   var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var all=root.GetComponentsInChildren<MeshFilter>(true);var body=all.Where(f=>f.name!="Ink"&&f.name!="AttentionRim");
   lines.Add($"| {root.name} | {body.Sum(f=>f.sharedMesh.vertexCount)} | {body.Sum(f=>f.sharedMesh.triangles.Length/3)} | {all.Sum(f=>f.sharedMesh.triangles.Length/3)} | {all.Length} |");
  }
  File.WriteAllLines("Logs/ShellGeometry.md",lines);
 } static void Finish(Exception ex){SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/ShellArtVerification.txt",new[]{ex==null?"PASS "+notes.Count+" checks":"FAIL "+ex}.Concat(notes));EditorApplication.Exit(ex==null?0:1);}
}





