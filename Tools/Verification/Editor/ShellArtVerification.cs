using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Aşılı Kabuk sunum doğrulaması (25 Ekim 2026, "aşı damarlı zırh tohumu", LateGamePresenter.ShellBurst.cs). Gerçek olay ve gerçek
// vuruşlarla üç yol: 0 = A kırılır (B kapanır), 1 = B kırılır (A kapanır), 2 = seçim yapılmaz (A kapanır). Zamanlamalı görsel
// kontroller oyun durdurulup sunumun efekt adımı elle sürülerek yapılır (kare süresinden bağımsız). Yalnız izole kopyada.
[InitializeOnLoad]
public static class ShellArtVerification
{
 const string Key="ShellArtVerification";static int step=-1,route,rootCount;static double next,started;static float phase,collarAge;static int nodeRootId;
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
 static void Capture(string name)=>Lab("Capture","Shell_"+route+"_"+name);
 static void Hit(int index){var cell=E.Targets[index].Cell;Lab("Attack",cell.x,cell.z);}
 // ---- sunum iç durumu (yansıma; ShellFx alanları açık)
 static object[] Pool(string name)=>((Array)typeof(LateGamePresenter).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>().ToArray();
 static T F<T>(object o,string n)=>(T)o.GetType().GetField(n).GetValue(o);
 static object[] Live(string name)=>Pool(name).Where(o=>o!=null&&F<bool>(o,"live")).ToArray();
 static Transform Root(object fx)=>F<GameObject>(fx,"root").transform;
 static GridPosition Cell(object fx)=>F<GridPosition>(fx,"cell");
 static Action<float> sync;
 static void Step(float seconds){sync??=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),P,typeof(LateGamePresenter).GetMethod("SyncShellFx",BindingFlags.NonPublic|BindingFlags.Instance));for(float t=0;t<seconds-1e-4f;t+=.02f)sync(Mathf.Min(.02f,seconds-t));}
 static ShellBurstArtSO Art=>Resources.Load<ShellBurstArtSO>(ShellBurstArtSO.ResourcePath);
 static bool Bluish(Color c){Color.RGBToHSV(c,out float h,out float s,out float v);return s>.35f&&v>.25f&&h>.47f&&h<.70f;}
 static bool Same(GridPosition a,GridPosition b)=>a.x==b.x&&a.z==b.z;
 static int ZoneOf(GridPosition c){for(int z=0;z<2;z++)if(E.Zone(z).Any(x=>Same(x,c)))return z;return -1;}
 static Vector3 W(GridPosition c)=>FieldCells.World(GridManager.Instance.GetGridSystem(),c);
 static float Angle(Transform t)=>Quaternion.Angle(Quaternion.identity,t.localRotation);
 static int ShellRoots()=>P.GetComponentsInChildren<Transform>(true).Count(x=>x.parent!=null&&x.parent.name=="Shell Burst");
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;started=next;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try
  {
   if(EditorApplication.timeSinceStartup-started>240)throw new Exception("timeout step "+step);
   switch(step)
   {
    case -1:if(double.IsNaN((double)Lab("Lab")))return;Time.captureDeltaTime=0;Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real save disabled");
     if(route==0){var art=Art;Check(art!=null&&art.Valid,"ShellBurstArt complete");
      int Tris(Mesh m)=>m==null?0:Enumerable.Range(0,m.subMeshCount).Sum(i=>(int)m.GetIndexCount(i)/3);
      notes.Add($"BUDGET node {Tris(art.nodeBody[0])+Tris(art.nodeCore[0])+Tris(art.nodeMotif[0])+art.nodeCracks.Take(3).Sum(Tris)} tris, split {art.nodePlates.Take(5).Sum(Tris)+Tris(art.nodeKnob[0])+Tris(art.nodeCore[0])}, birth {Tris(art.birthCenter)+art.birthSlabs.Sum(Tris)+Tris(art.birthGlow[0])+Tris(art.shock)+8*Tris(art.dust[0])}, vein {9*Tris(art.veinSegment[0])+Tris(art.clamp[0])+Tris(art.bead[0])}, collar {art.collarPlates.Sum(Tris)+Tris(art.collarBand[0])+art.crust.Sum(Tris)+3*Tris(art.chip)+2*Tris(art.dust[0])}, hit {Tris(art.tink[0])+3*Tris(art.chip)}");
      var zoneB=new Mesh[]{art.nodeBody[1],art.nodeCore[1],art.nodeMotif[1],art.birthGlow[1],art.veinSegment[1],art.clamp[1],art.bead[1],art.collarBand[1],art.tink[1],art.spark[1]}.Concat(art.nodeCracks.Skip(3));
      Check(!Bluish(LateGamePresenter.ZoneColor(FieldEventKind.GraftedShell,1))&&!zoneB.SelectMany(m=>m.colors).Any(Bluish),"zone B is violet: no blue in its colour or meshes (blue = electric)");}
     break;
    case 0:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,route==0?42:route==1?12:21),"start real shell plan");
     var cfg=D.Plan.graftedShell;Check(cfg.targetDurability==4&&cfg.choiceSeconds==8&&cfg.closedSeconds==20&&cfg.timing.prepareSeconds==6&&cfg.timing.emergeSeconds==1.2f,"mechanical values unchanged");break;
    case 1:
    {
     if(E.Phase!=FieldEventPhase.Preparing||E.PhaseTime<.7f)return;
     Check(!E.ZoneClosed(0)&&!E.ZoneClosed(1),"preparation does not block production");
     Check(P.EventItems==0,"no legacy shell items (covers / node prefabs) are spawned");
     Check(Live("shellNodes").Length==0,"no node during preparation (it is born from its birth plates at emergence)");
     var births=Live("shellBirths");
     Check(births.Length==2&&births.All(b=>Same(Cell(b),E.Targets[F<int>(b,"zone")].Cell)&&new Vector2(Root(b).position.x-W(Cell(b)).x,Root(b).position.z-W(Cell(b)).z).magnitude<.01f),"birth plates centred on each node cell (the shell's birthplace)");
     Check(births.All(b=>F<Transform>(b,"glow").gameObject.activeSelf),"glowing seams show between the birth plates");
     var veins=Live("shellVeins");
     int expected=E.Zone(0).Count+E.Zone(1).Count-2;
     Check(veins.Length==expected&&veins.All(v=>ZoneOf(Cell(v))==F<int>(v,"zone"))&&!veins.Any(v=>Same(Cell(v),E.Targets[0].Cell)||Same(Cell(v),E.Targets[1].Cell)),$"one graft vein per zone cell, none under a node ({veins.Length}/{expected})");
     Check(veins.All(v=>{var node=W(E.Targets[F<int>(v,"zone")].Cell);var clamp=F<Transform>(v,"clamp");return clamp.gameObject.activeSelf&&Vector3.Distance(clamp.position,node)<Vector3.Distance(W(Cell(v)),node);}),"every clamp sits on its cell's rim, on the side facing its node");
     Check(Object.FindAnyObjectByType<FieldEventHud>().LegendShown,"A/B HUD legend visible");
     Check(Object.FindAnyObjectByType<FieldEventHud>().GetComponentsInChildren<ShellMotifGraphic>().Length==2,"HUD uses same two polygon motifs");
     {Step(.02f);long before=GC.GetAllocatedBytesForCurrentThread();Step(6f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,$"300 shell effect steps during preparation allocate {bytes} bytes");}
     if(route==0)Capture("01_Prepare");break;
    }
    case 2:
    {
     if(E.Phase!=FieldEventPhase.Emerging)return;
     Time.timeScale=0;
     var nodes=Live("shellNodes");Check(nodes.Length==2,"both nodes start at emergence");
     var a=nodes.First(n=>F<int>(n,"zone")==0);float age=F<float>(a,"age");
     Step(Mathf.Max(0f,.25f-age));
     Check(Root(a).position.y<F<Vector3>(a,"at").y-.2f,"heavy rise: node is still pushing up out of the ground at .25 s");
     Check(Live("shellBirths").All(b=>F<Transform[]>(b,"slabs").Any(s=>Angle(s)>20f)),"birth plates tip outward as the node pushes through");
     Step(LateGamePresenter.ShellTokAt+.02f-.25f);
     var birth=Live("shellBirths").First(b=>F<int>(b,"zone")==0);
     Check(F<Transform>(birth,"shock").gameObject.activeSelf&&F<Transform[]>(birth,"dust").Any(d=>d.gameObject.activeSelf),"TOK: ink shock ring and stone-dust ring when the node lands");
     Check((float)typeof(LateGamePresenter).GetField("shellShake",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)>.5f,"TOK gives a short heavy screen thump");
     Check(Root(a).localScale.y<Root(a).localScale.x,"landing squashes the node");
     if(route==0)Capture("02a_Tok");
     Step(.5f);
     Check(Mathf.Abs(Root(a).position.y-F<Vector3>(a,"at").y)<.03f,"node settles on its cell");
     Step(.5f);
     Check(Live("shellBirths").Length==0,"birth plates sink away after the landing");
     Time.timeScale=1;break;
    }
    case 3:
    {
     if(E.Phase!=FieldEventPhase.Active)return;
     var nodes=Live("shellNodes");
     Check(nodes.Length==2,"both nodes out during the choice");
     var a=nodes.First(n=>F<int>(n,"zone")==0);var b=nodes.First(n=>F<int>(n,"zone")==1);
     if(route==0)nodeRootId=Root(a).gameObject.GetInstanceID();
     if(route==2)Check(nodeRootId==Root(a).gameObject.GetInstanceID(),"A node reused from its pool slot");
     Check(F<int>(a,"damage")==0&&F<Transform[]>(a,"cracks").All(c=>!c.gameObject.activeSelf),"pooled node starts without cracks");
     Time.timeScale=0;float s0=F<Transform>(a,"motif").localScale.x;Step(.13f);float s1=F<Transform>(a,"motif").localScale.x;
     Check(Mathf.Abs(s0-s1)>.005f&&Mathf.Abs(F<Transform>(b,"motif").localScale.x-1f)<1e-4f,"default A node's motif pulses as the warning (B steady)");
     Time.timeScale=1;
     var art=Art;var allowed=new HashSet<Material>{art.lit,art.ink,(Material)typeof(LateGamePresenter).GetField("arcMaterial",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)};
     Check(P.transform.Find("Shell Burst").GetComponentsInChildren<MeshRenderer>(true).All(r=>r.sharedMaterials.All(allowed.Contains)),"all shell parts use the shared toon / ink / unlit-card materials (no new materials)");
     Check(nodes.All(n=>Root(n).GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertices.All(v=>Camera.main.WorldToViewportPoint(f.transform.TransformPoint(v)).y<.915f))),"nodes clear top counters");
     Capture("02_Choice");if(route==0)step=19;
     break;
    }
    case 20:
    {
     var a=Live("shellNodes").First(n=>F<int>(n,"zone")==0);
     Hit(0);Check(E.Targets[0].Remaining==3,"one real direct hit consumes one durability");
     var hit=Live("shellHits");
     Check(hit.Length==1&&F<Transform>(hit[0],"tinkCard")!=null,"hit shows a hard 'tink' and bark chips");
     Check(F<int>(a,"damage")==1,"first hit opens the first crack stage");
     Time.timeScale=0;Step(.03f);
     Check(Quaternion.Angle(Quaternion.identity,Root(a).rotation)>8f,"node rocks heavily from its base, away from the blow");Step(.04f);
     Check(F<Transform[]>(a,"cracks")[0].gameObject.activeSelf&&!F<Transform[]>(a,"cracks")[1].gameObject.activeSelf,"crack stage 1 visible, stage 2 not yet");
     Check(F<Transform[]>(hit[0],"chips").All(c=>c.gameObject.activeSelf),"chips fly from the struck side");
     Check(Root(a).GetComponentsInChildren<Renderer>().All(r=>{var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return block.GetFloat("_ToonFlash")<.5f;}),"no white flash on the shell (hard shell: crack + rock instead)");
     Capture("03_Hit");Time.timeScale=1;
     Hit(0);Hit(0);Step(.02f);
     Check(F<int>(a,"damage")==3&&F<Transform[]>(a,"cracks").All(c=>c.gameObject.activeSelf)&&F<bool>(a,"release"),"three hits: all crack stages show, the node trembles with one hit left");
     step=3;break;
    }
    case 4:
    {
     int closed=route==0?1:0,saved=1-closed;
     if(route<2){int index=route==0?0:1;while(!E.Decided)Hit(index);}
     else{Call(E,"Tick",8f);}
     Check(D.Zones.BlockedCells==E.Zone(closed).Count,"exact closed region effect count");
     Check(E.Decided&&E.ClosedZone==closed&&E.ZoneClosed(closed)&&!E.ZoneClosed(saved),"correct region blocks immediately");
     Check(E.DecidedByPlayer==(route<2),"player choice versus timeout path correct");
     Check(Live("shellSplits").Any(s=>F<int>(s,"zone")==saved)&&!Live("shellNodes").Any(n=>F<int>(n,"zone")==saved),"saved node splits open like a nut (also when the choice timed out)");
     var sinking=Live("shellNodes").Single(n=>F<int>(n,"zone")==closed);
     Check(F<float>(sinking,"sinkAge")>=0f&&Live("shellSinks").Length==1,"closed node starts sinking into the ground");
     Check(Live("shellVeins").All(v=>F<float>(v,"decideAt")>=0f&&F<bool>(v,"release")==(F<int>(v,"zone")==saved)),"every vein carries the decision (saved: release, closed: shut)");
     Check(Live("shellCollars").Length==1&&Same(Cell(Live("shellCollars")[0]),E.Targets[closed].Cell),"closed node's own cell collar waits for the node to sink");
     Time.timeScale=0;
     long before=GC.GetAllocatedBytesForCurrentThread();
     Step(.35f);
     var split=Live("shellSplits").First(s=>F<int>(s,"zone")==saved);
     bool open=F<Transform[]>(split,"plates").All(p=>Angle(p)>60f);
     Step(.25f);if(route==0)Capture("04_Closing");
     Step(.9f);
     long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
     Check(open,"split plates fall open outward");
     Check(bytes==0,$"break / sink / beads / collars allocate {bytes} bytes over 1.5 s of steps");
     var collars=Live("shellCollars");
     Check(collars.Length==E.Zone(closed).Count&&E.Zone(closed).All(c=>collars.Any(x=>Same(Cell(x),c))),"a bark collar closes on every cell of the closed zone (node cell included)");
     Check(collars.All(c=>F<Transform[]>(c,"plates").All(p=>p.gameObject.activeSelf)&&F<Transform>(c,"band").gameObject.activeSelf),"collar plates stand and the inner band glows");
     Check(!Live("shellVeins").Any(v=>F<int>(v,"zone")==saved),"saved zone's veins release their clamps and fade");
     Check(!Live("shellNodes").Any()&&Live("shellSplits").Length==0,"sunk node and split shell are back in their pools");
     phase=E.PhaseTime;collarAge=F<float>(collars[0],"age");
     if(route==0)Capture("05_Closed");
     break;   // durdurma bir sonraki karede ölçülür (bu karenin süresi timeScale'den önce hesaplandı)
    }
    case 5:
    {
     int closed=route==0?1:0;
     var collars=Live("shellCollars");
     Check(E.PhaseTime==phase&&collars.Length==E.Zone(closed).Count&&F<float>(collars[0],"age")==collarAge,"pause freezes collars and logic");
     Time.timeScale=1;
     Call(E,"Tick",20f);Check(D.Zones.BlockedCells==0&&!E.ZoneClosed(0)&&!E.ZoneClosed(1),"expiry removes logical closure immediately");
     Check(Live("shellCollars").All(c=>F<float>(c,"openAt")>=0f),"collars start opening when the zone reopens");
     Time.timeScale=0;Step(.4f);
     Check(Live("shellCollars").All(c=>F<Transform[]>(c,"plates").Any(p=>Angle(p)>30f)),"collar plates crack and tip outward");
     if(route==0)Capture("06_Open");
     Step(.7f);
     Check(Live("shellCollars").Length==0&&Live("shellVeins").Length==0,"opening finishes inside the resolve window (collars and veins pooled)");
     Time.timeScale=1;
     D.End();Check(P.ShellFxShown==0&&Pool("shellCollars").Concat(Pool("shellVeins")).Concat(Pool("shellNodes")).All(o=>o==null||!F<bool>(o,"live")),"hard cleanup clears all shell effects");
     if(route==0)rootCount=ShellRoots();
     else Check(ShellRoots()==rootCount,$"shell effects reuse their pools across events ({ShellRoots()} roots)");
     if(route<2){route++;step=-1;break;}step=7;break;
    }
    case 8:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,42),"restart ready");Call(D.Current,"Tick",.01f);Call(D.Current,"Tick",6f);Call(D.Current,"Tick",1.2f);Call(D.Current,"Tick",8f);
     Check(E.ZoneClosed(0),"restart default closure active");Call(D,"HandleStateChanged",GameStates.MainMenu);
     Check(D.Current==null&&P.ShellFxShown==0&&Live("shellCollars").Length==0&&D.Zones.BlockedCells==0,"menu callback cleans closure and visuals");break;
    case 9:
     Check(D.StartNow(FieldEventKind.GraftedShell,0,42),"start before run restart");Call(D.Current,"Tick",.01f);Call(D,"HandleRunStarted");Check(D.Current==null&&Live("shellVeins").Length==0&&Live("shellBirths").Length==0,"run restart callback clears state");break;
    case 10:
     var soundArt=Resources.Load<LateGameArtSO>(LateGameArtSO.ResourcePath);
     foreach(var v in new[]{LateGameVariant.ShellA,LateGameVariant.ShellB})
     foreach(var sound in new[]{LateGameSound.EventPrepared,LateGameSound.ShellChoice,LateGameSound.ShellClose,LateGameSound.ShellOpen,LateGameSound.TargetHit,LateGameSound.TargetBroken})Check(soundArt.Sound(sound,v).clip!=null,"sound bound "+v+" "+sound);
     break;
    case 11:Finish(null);return;
   }
   step++;next=EditorApplication.timeSinceStartup+.035;File.WriteAllLines("Logs/ShellArtVerification.txt",new[]{"RUNNING route "+route+" step "+step}.Concat(notes));
  }
  catch(Exception ex){Finish(ex.InnerException??ex);}
  EditorApplication.QueuePlayerLoopUpdate();
 }
 static void Finish(Exception ex)
 {
  SessionState.SetBool(Key,false);Time.timeScale=1;
  File.WriteAllLines("Logs/ShellArtVerification.txt",new[]{ex==null?"PASS "+notes.Count+" checks":"FAIL "+ex}.Concat(notes));EditorApplication.Exit(ex==null?0:1);
 }
}
