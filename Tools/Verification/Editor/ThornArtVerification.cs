using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs ONLY through the isolated runner. Reuses the existing populated GameScene laboratory setup.
[InitializeOnLoad]
public static class ThornArtVerification
{
    const string Key="ThornArtVerification";
    static double next; static int step = -1; static float start; static LateGameContent root;
    static readonly List<string> notes=new();
    static bool capturedEmergence, pausedEmergence;
    static float pausedPhase;
    static Vector3 pausedVisual;
    static FieldEventDirector Director=>FieldEventDirector.Instance;
    static LateGamePresenter Presenter=>Object.FindAnyObjectByType<LateGamePresenter>();
    static ThornArtVerification(){EditorApplication.update+=Tick;}
    public static void BuildAndRunBatch(){ThornArtBuilder.BuildRoot();ThornArtBuilder.BuildParts();RunBatch();}
    public static void RootBatch(){SessionState.SetBool("ThornRootOnly",true);RunBatch();}
    public static void RunBatch()
    {
        if(!Path.GetFullPath(Application.dataPath).Contains("VerificationProject"))throw new Exception("Use isolated verification runner");
        GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);
    }
    static object Lab(string name,params object[] args)=>typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    static object Call(object instance,string name,params object[] args)=>instance.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(instance,args);
    static void Check(bool value,string note){if(!value)throw new Exception(note);notes.Add("PASS "+note);}
    static LateGameContent[] Roots()=>Presenter.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith("ThornRoot")).ToArray();
    static void Capture(string name)=>Lab("Capture","Thorn_"+name);
    static void Hit(int index){var cell=Director.Current.Targets[index].Cell;Lab("Attack",cell.x,cell.z);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
        if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        try
        {
            switch(step)
            {
                case -1:
                    var result=(double)Lab("Lab");if(double.IsNaN(result))return;
                    Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"real save disabled in batch");break;
                case 0:
                    Time.captureDeltaTime = 0f;
                    GameSettings.ScreenShake = 1f;
                    GameSettings.CameraMotion = false;
                    if(CameraFeel.Instance==null)Camera.main.gameObject.AddComponent<CameraFeel>();
                    FieldEventDirector.Cued += Trace;
                    Check(Director.StartNow(FieldEventKind.ThornSiege,0f,12),"start real 3x3 / six-hit / thirty-second data");break;
                case 1:
                    if(Director.Current == null) throw new Exception("Event disappeared: state="+GameManager.Instance.CurrentState+" time="+Time.time);
                    if(Director.Current.Phase!=FieldEventPhase.Preparing)return;
                    if(Director.Current.PhaseTime < .3f)return;
                    Check(!Presenter.GetComponentsInChildren<LateGameContent>().Any(c=>c.name.StartsWith("ThornWarning")),"no floating canopy warning symbols");
                    var soil=Presenter.GetComponentsInChildren<LateGameContent>().Where(c=>c.groundRumble).ToArray();
                    Check(soil.Length==9 && soil.Any(c=>c.visual.localPosition.y>0f),"nine ground patches heave during preparation");
                    Check(soil.All(c=>c.transform.localScale==Vector3.one),"ground quake moves visual children only");
                    Capture("01_Prepare");Check(Director.Current.Zone(0).Count==9,"nine cells prepared");break;
                case 2:
                    if(pausedEmergence)
                    {
                        Check(Director.Current.PhaseTime==pausedPhase && Roots()[0].visual.localPosition==pausedVisual,"pause freezes underground emergence");
                        Check(CameraFeel.Instance.FrameShakeOffset==Vector2.zero,"emergence camera shake stops during pause");
                        Time.timeScale=1f;pausedEmergence=false;
                    }
                    if(!capturedEmergence && Director.Current.Phase==FieldEventPhase.Emerging && Director.Current.PhaseTime>.1f)
                    {
                        Check(CameraFeel.Instance!=null && CameraFeel.Instance.FrameShakeOffset.sqrMagnitude>0f,"light camera shake runs during visual emergence");
                        GameSettings.ScreenShake=0f;
                        Check(CameraFeel.Instance.FrameShakeOffset==Vector2.zero,"screen shake preference disables event shake");
                        GameSettings.ScreenShake=1f;
                        Check(Roots()[0].visual.localPosition.y<0f,"root rises from below ground");
                        Check(Presenter.GetComponentsInChildren<LateGameContent>().Any(c=>c.name.StartsWith("ThornSoilBurst")),"soil fragments burst during emergence");
                        Capture("01b_Emerge");capturedEmergence=true;
                        pausedPhase=Director.Current.PhaseTime;pausedVisual=Roots()[0].visual.localPosition;
                        Time.timeScale=0f;pausedEmergence=true;return;
                    }
                    if(Director.Current.Phase!=FieldEventPhase.Active)return;
                    Check(capturedEmergence,"emergence camera and soil sequence observed");
                    Check(CameraFeel.Instance.FrameShakeOffset==Vector2.zero,"camera shake ends with visual emergence");
                    start=Time.time;break;
                case 3:
                    if(Time.time-start<.7f)return;
                    root=Roots()[0];
                    if(SessionState.GetBool("ThornRootOnly",false))
                    {
                        var roots=Roots();if(roots.Length>1)roots[1].gameObject.SetActive(false);
                        Capture("02_SingleRoot");Finish(null);return;
                    }
                    Check(Roots().Length==2,"two authored roots");
                    Check(Presenter.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.shader.name=="ClickerGame/ThornComicToon"),"all event models and VFX use toon shader");
                    Capture("02_Active");
                    Check(Roots().All(r=>r.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertices.All(v=>Camera.main.WorldToViewportPoint(f.transform.TransformPoint(v)).y<.915f))),"root vertices clear top resource counters at 1600x900");
                    Check(Roots().All(r=>r.GetComponentsInChildren<Renderer>(true).All(x=>x.sharedMaterial.shader.name=="ClickerGame/ThornComicToon")),"all root renderers use toon shader");
                    Capture("02_Active");
                    step=30;break;
                case 31:
                    // Sample the real hit deterministically; batch loop speed is not animation time.
                    Hit(0);Check(EventHitChecks.HasFlash(root),"real hit uses plant white flash");Check(Roots().Contains(root),"hit preserves same root instance");Check(Director.Current.Targets[0].Remaining==5,"one direct hit uses one of six");
                    Time.timeScale=0;root.visual.localScale=Vector3.one;root.Step(.07f,.6f);
                    Check(root.visual.localScale.y<.95f,"actual TargetHit squashes visual child");
                    Check(root.transform.localScale==Vector3.one,"target root transform stays fixed");
                    Capture("03_Hit");Time.timeScale=1f;Hit(0);Hit(0);Hit(0);step=4;break;
                case 5:
                    Check(root.damageStages.Count(x=>x.activeSelf)>=2,"damage reveals persistent cracks");Capture("04_Cracked");
                    Time.timeScale=0f;start=Director.Current.PhaseTime;break;
                case 6:
                    Check(Director.Current.PhaseTime==start,"pause freezes event phase");
                    Time.timeScale=1f;Hit(0);Hit(0);
                    Check(Director.Current.Targets[0].Broken&&!Director.Current.Targets[1].Broken,"six direct hits break only the selected root");
                    Check(Director.Zones.BlockedCells==9,"one root remaining keeps production blocked");
                    Check(root.gameObject.activeSelf,"broken root keeps a bounded visual exit");start=Time.time;break;
                case 7:
                    if(Time.time-start<.1f)return;
                    Capture("05_Break");for(int i=0;i<6;i++)Hit(1);
                    Check(Director.Zones.BlockedCells==0,"production unblocks immediately before exit animation ends");
                    start=Time.time;break;
                case 8:
                    if(Time.time-start<.15f)return;
                    Capture("06_Dissolve");
                    Director.End();Check(CameraFeel.Instance.FrameShakeOffset==Vector2.zero,"cleanup leaves no event camera shake");Check(Presenter.EventItems==0&&Presenter.ActiveDecorations==0,"hard cleanup removes event, effects and tails");
                    Check(Director.StartNow(FieldEventKind.ThornSiege,0f,12),"new event starts after hard cleanup");break;
                case 9:
                    if(Director.Current.Phase!=FieldEventPhase.Active)return;
                    Check(Roots().All(r=>r.damageStages.All(d=>!d.activeSelf)),"pooled roots reset cracks");
                    Call(Director.Current,"Tick",30f);
                    Check(Director.Zones.BlockedCells==0&&Director.Current.Phase==FieldEventPhase.Resolving,"real thirty-second timeout opens zone");
                    Director.End();Catalog();Audio();break;
                case 10: Finish(null);return;
            }
            step++; next=EditorApplication.timeSinceStartup+.07;
            File.WriteAllLines("Logs/ThornArtVerification.txt",new[]{"RUNNING step "+step}.Concat(notes));
        }
        catch(Exception ex){Finish(ex.InnerException??ex);}
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static void Trace(FieldEventCue cue) { notes.Add("cue "+cue.Kind+" t="+Time.time+" phase="+cue.Event.Phase); }
    static void Catalog()
    {
        var art=ScriptableObject.CreateInstance<LateGameArtSO>();var a=new GameObject("A");var b=new GameObject("B");
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=LateGameSlot.Pulse,prefab=a,lifetime=.31f});
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=LateGameSlot.Pulse,variant=LateGameVariant.FullMoon,prefab=b,lifetime=.9f});
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=LateGameSlot.ShellNode,variant=LateGameVariant.ShellA,prefab=a});
        art.slots.Add(new LateGameArtSO.SlotBinding{slot=LateGameSlot.ShellNode,variant=LateGameVariant.ShellB,prefab=b});
        Check(art.Prefab(LateGameSlot.Pulse,LateGameVariant.FullMoon)==b&&art.Prefab(LateGameSlot.Pulse,LateGameVariant.Greed)==a,"reward variant and common fallback");
        Check(art.Lifetime(LateGameSlot.Pulse,LateGameVariant.FullMoon)==.9f,"per-variant lifetime");
        Check(art.Prefab(LateGameSlot.ShellNode,LateGameVariant.ShellA)!=art.Prefab(LateGameSlot.ShellNode,LateGameVariant.ShellB),"shell A/B prefab motifs can differ");
        Object.Destroy(a);Object.Destroy(b);Object.Destroy(art);
    }
    static void Audio()
    {
        var go=new GameObject("Audio test");var audio=new LateGameAudio(go);var clip=AudioClip.Create("test",44100,1,44100,false);
        var binding=new LateGameArtSO.SoundBinding{clip=clip,volume=.5f,voices=2,repeatSeconds=.1f};
        for(int i=0;i<100;i++)audio.Play(binding);
        Check(audio.ActiveVoices==1&&audio.Skipped==99,"dense sound cues throttled");
        audio.Step(.11f,true);audio.Play(binding);audio.Step(.11f,true);audio.Play(binding);
        Check(audio.ActiveVoices==2,"per-clip concurrent voice cap");audio.Step(0f,false);Check(audio.ActiveVoices==2,"pause retains bounded audio voices");
        audio.Clear();Check(audio.ActiveVoices==0,"audio cleanup");Object.Destroy(go);Object.Destroy(clip);
    }
    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key,false);SessionState.SetBool("ThornRootOnly",false);
        File.WriteAllLines("Logs/ThornArtVerification.txt",new[]{ex==null?"PASS":"FAIL "+ex}.Concat(notes));
        Time.timeScale=1f;EditorApplication.Exit(ex==null?0:1);
    }
}



