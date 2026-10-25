using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SproutArtVerification
{
    const string Key="SproutArtVerification";
    static int step=-1;static double next,started;static float at,phase;static LateGameContent main;
    static Quaternion leaf;static Vector3 targetPosition;static LateGameContent[] small;static Vector3[] scales;
    static readonly List<string> notes=new();
    static FieldEventDirector D=>FieldEventDirector.Instance;
    static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
    static SproutArtVerification(){EditorApplication.update+=Tick;}
    public static void BuildAndRunBatch(){SproutArtBuilder.Build();LateGameAttentionBuilder.Build();RunBatch();}
    public static void RunBatch()
    {
        if(!Path.GetFullPath(Application.dataPath).Contains("VerificationProject"))throw new Exception("Use isolated runner");
        GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);
    }
    static object Lab(string name,params object[] args)=>typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    static object Call(object o,string name,params object[] a)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static void Check(bool ok,string note){if(!ok)throw new Exception(note);notes.Add("PASS "+note);}
    static LateGameContent[] Content(string name)=>P.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith(name)).ToArray();
    static void Capture(string name)=>Lab("Capture","Sprout_"+name);
    static void Hit(){var c=D.Current.Targets[0].Cell;Lab("Attack",c.x,c.z);}
    // 24 Ekim 2026 (Diken kuralları Filiz'e): tümsek yalnız ana filizde, ana filizin altında sürünen dal yok, patlayarak çıkış +
    // toprak patlaması, güçlü vuruş, saksısız hücrede parça zemine iner.
    static int LiveBlasts()=>((Array)typeof(LateGamePresenter).GetField("thornBlasts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>().Count(s=>s!=null&&(bool)s.GetType().GetField("live").GetValue(s));
    // 25 Ekim: Filiz'in kendi kişiliği (SproutBurst): bloom / spread / sap / wilt havuzları.
    static int LiveSprout(string pool)=>((Array)typeof(LateGamePresenter).GetField(pool,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>().Count(s=>s!=null&&(bool)s.GetType().GetField("live").GetValue(s));
    static bool OnMain(LateGameContent c){var m=FieldCells.World(GridManager.Instance.GetGridSystem(),D.Current.Targets[0].Cell);return new Vector2(c.transform.position.x-m.x,c.transform.position.z-m.z).magnitude<.05f;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(next==0){next=EditorApplication.timeSinceStartup+2;started=next;return;}
        if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        try
        {
            if(EditorApplication.timeSinceStartup-started>150)throw new Exception("test timeout step "+step);
            switch(step)
            {
                case -1:if(double.IsNaN((double)Lab("Lab")))return;Time.captureDeltaTime=0f;break;
                case 0:
                    Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real save disabled");
                    Check(D.StartNow(FieldEventKind.InvasiveSprout,0f,21),"start unchanged sprout plan");
                    Check(D.Plan.invasiveSprout.mainDurability==8 && D.Plan.invasiveSprout.spreadInterval==2.5f && D.Plan.invasiveSprout.maxPieces==8 && D.Plan.invasiveSprout.slowMultiplier==1.6f && D.Plan.invasiveSprout.timing.activeSeconds==35,"real durability, interval, capacity, slow and duration unchanged");break;
                case 1:
                    if(D.Current.Phase!=FieldEventPhase.Preparing||D.Current.PhaseTime<.3f)return;
                    Check(D.Zones.SlowedCells==0 && Content("SproutMain").Length==0,"preparation has no slowdown or target art");
                    Check(Content("GroundHeave").Length==1&&OnMain(Content("GroundHeave")[0]),"one birth mound, centred on the main sprout cell");Capture("01_Prepare");break;
                case 2:
                    if(D.Current.Phase!=FieldEventPhase.Emerging||D.Current.PhaseTime<.13f)return;
                    main=Content("SproutMain").Single();leaf=main.petals.leaves[0].localRotation;targetPosition=main.transform.position;
                    Check(main.visual.localPosition.y<0,"bud rises from soil");
                    Check(LiveBlasts()==0&&LiveSprout("sproutBlooms")==1,$"main sprout blooms with its own effect (no Diken soil blast) [blasts {LiveBlasts()}, blooms {LiveSprout("sproutBlooms")}, built {((Array)typeof(LateGamePresenter).GetField("sproutBlooms",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>().Count(x=>x!=null)}, phase t {D.Current.PhaseTime:0.00}]");
                    {var items=((System.Collections.IList)typeof(LateGamePresenter).GetField("eventItems",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>();
                     Check(items.Any(i=>(int)i.GetType().GetField("identity").GetValue(i)==10000&&(bool)i.GetType().GetField("spiral").GetValue(i)&&!(bool)i.GetType().GetField("burst").GetValue(i)),"main sprout spirals up (not the Diken burst curve)");
                     Check(main.petals.snap&&Mathf.Abs(main.petals.openDelay-LateGamePresenter.SproutPopAt)<1e-4f,"bud stays closed until the pop, then snaps open");}Check(main.petals.leaves.Length==4,"main silhouette has four hinged crown leaves");Capture("02_Unfurl");break;
                case 3:
                    if(D.Current.Phase!=FieldEventPhase.Active)return;
                    Check(Quaternion.Angle(leaf,main.petals.leaves[0].localRotation)>15f,"crown unfolds from bud pose");
                    Check(D.Zones.SlowedCells==1 && Content("SproutPiece").Length==0,"no runner under the main sprout (slowed main cell, no footprint)");
                    Check(Content("GroundHeave").Length==0,"birth mound gone once the main sprout is out");
                    Check(main.transform.position==targetPosition&&main.transform.localScale==Vector3.one,"root and logical target stay fixed");
                    Check(P.GetComponentsInChildren<LateGameContent>().SelectMany(c=>c.GetComponentsInChildren<Renderer>()).All(r=>r.sharedMaterial.shader.name=="ClickerGame/ThornComicToon"),"all event models share approved toon shader (soil blast cards are unlit vertex-colour by design)");Capture("03_Main");break;
                case 4:
                    if(((InvasiveSproutEvent)D.Current).Pieces<2)return;
                    Check(Content("SproutPiece").Length==1 && D.Zones.SlowedCells==2&&!OnMain(Content("SproutPiece")[0]),"real first spread creates one small piece (on the new cell) and one slowdown");
                    Check(Content("SproutMain").Single()==main,"spread preserves main instance");
                    Check(LiveSprout("sproutSpreads")==1,"spread shows one crawling vine toward the new cell");Capture("04_Spread");
                    {var sync=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),P,typeof(LateGamePresenter).GetMethod("SyncSproutFx",BindingFlags.NonPublic|BindingFlags.Instance));sync(0f);
                     long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)sync(0f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                     Check(bytes==0,$"300 sprout effect syncs allocate {bytes} bytes");}
                    for(int i=0;i<6;i++)Call(D.Current,"Tick",2.5f);at=Time.time;break;
                case 5:
                    if(Time.time-at<.75f)return;
                    small=Content("SproutPiece");Check(small.Length==7 && D.Zones.SlowedCells==8&&!small.Any(OnMain),"eight affected cells: seven footprints, none under the main sprout");
                    Check(small.All(c=>c.petals.leaves.Length==2),"small silhouette has two low leaves, distinct from main");
                    Check(small.All(c=>c.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y)-c.transform.position.y<.8f),"small pieces remain below .8 world units");
                    Check(main.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y)-main.transform.position.y>1.5f,"main crown clearly taller than small pieces");
                    Check(main.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="AttentionRim")==5,"main has shared red rim on body and four leaves"); Check(main.petals.leaves.All(l=>l.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.95f),"broad crown mesh retained"); Capture("05_FullSpread");break;
                case 6:Hit();at=Time.time;Check(D.Current.Targets[0].Remaining==7,"one real direct hit consumes one durability");Check(LiveSprout("sproutSaps")==1,"hit splashes sap"); Check(EventHitChecks.HasFlash(main),"real hit uses plant white flash"); Time.timeScale=0;step=60;break;
                case 61: Check(EventHitChecks.HasFlash(main),"pause retains hit flash");
                    main.visual.localScale=Vector3.one;Vector3 rest=main.visual.localPosition;main.Step(.07f,.6f);
                    Check(main.visual.localScale.y<.8f,"direct hit squashes main hard (strong hit)");main.Step(0f,.6f);
                    Check(new Vector2(main.visual.localPosition.x-rest.x,main.visual.localPosition.z-rest.z).magnitude>.1f,"strong hit recoils the main sprout away from the blow");Capture("06_Hit");
                    Time.timeScale=1;Hit();Hit();Hit();Check(main.damageStages.Any(c=>c.activeSelf),"damage reveals scars");step=7;break;
                case 8:
                    Capture("07_Damaged");Time.timeScale=0;phase=D.Current.PhaseTime;leaf=main.petals.leaves[0].localRotation;break;
                case 9:
                    Check(phase==D.Current.PhaseTime&&leaf==main.petals.leaves[0].localRotation,"pause freezes leaf animation and event");
                    Time.timeScale=1;for(int i=0;i<4;i++)Hit();
                    Check(D.Zones.SlowedCells==0&&D.Current.Phase==FieldEventPhase.Resolving,"all slow effects clear immediately on eighth hit");
                    Check(LiveSprout("sproutWilts")==1,"break releases a spore puff");
                    // Ölüm dalgası (25 Ekim): ana filiz kurur ve devrilir; halkalar ana filize yakından uzağa sırayla kurur.
                    {var ex=((System.Collections.IList)typeof(LateGamePresenter).GetField("exiting",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(P)).Cast<object>().ToArray();
                     object F(object o,string n)=>o.GetType().GetField(n).GetValue(o);
                     var mainExit=ex.FirstOrDefault(o=>(int)F(o,"identity")==10000);
                     Check(mainExit!=null&&(bool)F(mainExit,"wither")&&(Vector3)F(mainExit,"topple")!=Vector3.zero&&(float)F(mainExit,"exitLife")>.6f,"main sprout withers and topples (longer fall)");
                     var m=D.Current.Targets[0].Cell;var rings=ex.Where(o=>(int)F(o,"identity")<10000&&(bool)F(o,"wither")).ToArray();
                     int Dist(object o){var c=FieldCells.Grid.GetGridPosition(((GameObject)F(o,"root")).transform.position);return Mathf.Abs(c.x-m.x)+Mathf.Abs(c.z-m.z);}
                     bool ordered=rings.All(a=>rings.All(b=>Dist(a)>=Dist(b)||(float)F(a,"exitDelay")<(float)F(b,"exitDelay")));
                     {var update=(Action)Delegate.CreateDelegate(typeof(Action),P,typeof(LateGamePresenter).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance));update();
                      long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<20;i++)update();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                      Check(bytes==0,$"20 presenter updates during the death wave allocate {bytes} bytes");}
                     Check(rings.Length==7&&ordered,$"seven rings die in a wave from the main sprout outward (delays {string.Join(",",rings.OrderBy(Dist).Select(o=>Dist(o)+":"+((float)F(o,"exitDelay")).ToString("0.00")))})");}
                    Check(small.All(c=>c.gameObject.activeSelf),"visual tails remain after gameplay slowdown is removed");at=Time.time;break;
                case 10:
                    if(Time.time-at<.55f)return;
                    var heights=small.Select(c=>c.visual.localScale.y).ToArray();
                    Check(heights.Max()-heights.Min()>.12f,"spread pieces wilt in a staggered wave");Check(small.Any(c=>c.Withered>.5f),"dying rings turn to the withered colour");Capture("08_Wilt");
                    Time.timeScale=0;scales=small.Select(c=>c.visual.localScale).ToArray();break;
                case 11:
                    Check(small.Select((c,i)=>c.visual.localScale==scales[i]).All(x=>x),"pause freezes staggered exit");Time.timeScale=1;
                    D.End();Check(P.EventItems==0&&P.ActiveDecorations==0,"round-style hard cleanup removes tails and effects");
                    Check(D.StartNow(FieldEventKind.InvasiveSprout,0f,21),"restart while old tails were pending");break;
                case 12:
                    if(D.Current.Phase!=FieldEventPhase.Active)return;
                    var reused=Content("SproutMain").Single();Check(reused==main,"main prefab reused from pool");
                    Check(P.GetComponentsInChildren<Transform>(true).Count(x=>x.name.StartsWith("Sprout Bloom")||x.name.StartsWith("Sprout Spread")||x.name.StartsWith("Sprout Sap")||x.name.StartsWith("Sprout Wilt"))<=12,"sprout effects reuse their pools across events");
                    Check(reused.damageStages.All(c=>!c.activeSelf)&&reused.visual.localPosition==Vector3.zero,"reuse clears scars and underground offset");
                    Check(!EventHitChecks.HasFlash(reused),"pool reuse clears hit flash"); Check(D.Zones.SlowedCells==1,"old tails cannot change new event slowdown");Call(D.Current,"Tick",35f);
                    Check(D.Zones.SlowedCells==0&&D.Current.Phase==FieldEventPhase.Resolving,"real timeout clears all slowdown");D.End();break;
                case 13:Finish(null);return;
            }
            step++;next=EditorApplication.timeSinceStartup+.035;File.WriteAllLines("Logs/SproutArtVerification.txt",new[]{"RUNNING step "+step}.Concat(notes));
        }
        catch(Exception ex){Finish(ex.InnerException??ex);}
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key,false);Time.timeScale=1; if(ex==null)LateGameGeometryReport.Write();
        File.WriteAllLines("Logs/SproutArtVerification.txt",new[]{ex==null?"PASS "+notes.Count+" checks":"FAIL "+ex}.Concat(notes));EditorApplication.Exit(ex==null?0:1);
    }
}






