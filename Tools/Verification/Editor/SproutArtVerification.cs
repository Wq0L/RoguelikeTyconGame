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
                    Check(Content("GroundHeave").Length==1,"shared ground preparation reused");Capture("01_Prepare");break;
                case 2:
                    if(D.Current.Phase!=FieldEventPhase.Emerging||D.Current.PhaseTime<.13f)return;
                    main=Content("SproutMain").Single();leaf=main.petals.leaves[0].localRotation;targetPosition=main.transform.position;
                    Check(main.visual.localPosition.y<0,"bud rises from soil");Check(main.petals.leaves.Length==4,"main silhouette has four hinged crown leaves");Capture("02_Unfurl");break;
                case 3:
                    if(D.Current.Phase!=FieldEventPhase.Active)return;
                    Check(Quaternion.Angle(leaf,main.petals.leaves[0].localRotation)>15f,"crown unfolds from bud pose");
                    Check(D.Zones.SlowedCells==1 && Content("SproutPiece").Length==1,"main slowed cell has a footprint before first spread");
                    Check(main.transform.position==targetPosition&&main.transform.localScale==Vector3.one,"root and logical target stay fixed");
                    Check(P.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.shader.name=="ClickerGame/ThornComicToon"),"all event renderers share approved toon shader");Capture("03_Main");break;
                case 4:
                    if(((InvasiveSproutEvent)D.Current).Pieces<2)return;
                    Check(Content("SproutPiece").Length==2 && D.Zones.SlowedCells==2,"real first spread creates one small piece and one slowdown");
                    Check(Content("SproutMain").Single()==main,"spread preserves main instance");
                    Check(Content("ThornSoilBurst").Length>0,"spread reuses pooled soil burst");Capture("04_Spread");
                    for(int i=0;i<6;i++)Call(D.Current,"Tick",2.5f);at=Time.time;break;
                case 5:
                    if(Time.time-at<.75f)return;
                    small=Content("SproutPiece");Check(small.Length==8 && D.Zones.SlowedCells==8,"eight affected cells have eight footprints");
                    Check(small.All(c=>c.petals.leaves.Length==2),"small silhouette has two low leaves, distinct from main");
                    Check(small.All(c=>c.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y)-c.transform.position.y<.8f),"small pieces remain below .8 world units");
                    Check(main.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y)-main.transform.position.y>1.5f,"main crown clearly taller than small pieces");
                    Check(main.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="AttentionRim")==5,"main has shared red rim on body and four leaves"); Check(main.petals.leaves.All(l=>l.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.95f),"broad crown mesh retained"); Capture("05_FullSpread");break;
                case 6:Hit();at=Time.time;Check(D.Current.Targets[0].Remaining==7,"one real direct hit consumes one durability"); Check(EventHitChecks.HasFlash(main),"real hit uses plant white flash"); Time.timeScale=0;step=60;break;
                case 61: Check(EventHitChecks.HasFlash(main),"pause retains hit flash");
                    main.visual.localScale=Vector3.one;main.Step(.07f,.6f);
                    Check(main.visual.localScale.y<.95f,"direct hit visibly squashes main");Capture("06_Hit");
                    Time.timeScale=1;Hit();Hit();Hit();Check(main.damageStages.Any(c=>c.activeSelf),"damage reveals scars");step=7;break;
                case 8:
                    Capture("07_Damaged");Time.timeScale=0;phase=D.Current.PhaseTime;leaf=main.petals.leaves[0].localRotation;break;
                case 9:
                    Check(phase==D.Current.PhaseTime&&leaf==main.petals.leaves[0].localRotation,"pause freezes leaf animation and event");
                    Time.timeScale=1;for(int i=0;i<4;i++)Hit();
                    Check(D.Zones.SlowedCells==0&&D.Current.Phase==FieldEventPhase.Resolving,"all slow effects clear immediately on eighth hit");
                    Check(small.All(c=>c.gameObject.activeSelf),"visual tails remain after gameplay slowdown is removed");at=Time.time;break;
                case 10:
                    if(Time.time-at<.14f)return;
                    var heights=small.Select(c=>c.visual.localScale.y).ToArray();
                    Check(heights.Max()-heights.Min()>.12f,"spread pieces wilt in a short staggered sequence");Capture("08_Wilt");
                    Time.timeScale=0;scales=small.Select(c=>c.visual.localScale).ToArray();break;
                case 11:
                    Check(small.Select((c,i)=>c.visual.localScale==scales[i]).All(x=>x),"pause freezes staggered exit");Time.timeScale=1;
                    D.End();Check(P.EventItems==0&&P.ActiveDecorations==0,"round-style hard cleanup removes tails and effects");
                    Check(D.StartNow(FieldEventKind.InvasiveSprout,0f,21),"restart while old tails were pending");break;
                case 12:
                    if(D.Current.Phase!=FieldEventPhase.Active)return;
                    var reused=Content("SproutMain").Single();Check(reused==main,"main prefab reused from pool");
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






