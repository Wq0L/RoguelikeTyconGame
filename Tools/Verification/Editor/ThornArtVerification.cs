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
    // 24 Ekim 2026: tümsek yalnız kök hücresinde (doğum yeri), kapan kökün altında yok; güçlü vuruş, vuruş yıldızı.
    static Vector2 XZ(Vector3 v)=>new Vector2(v.x,v.z);
    static Vector2 CellXZ(GridPosition c)=>XZ(FieldCells.World(GridManager.Instance.GetGridSystem(),c));
    static bool OnTarget(Vector3 at)=>Director.Current.Targets.Any(x=>(CellXZ(x.Cell)-XZ(at)).magnitude<.05f);
    static int LiveBlasts()=>((Array)typeof(LateGamePresenter).GetField("thornBlasts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Presenter)).Cast<object>().Count(s=>s!=null&&(bool)s.GetType().GetField("live").GetValue(s));
    static int BlastObjects()=>Presenter.GetComponentsInChildren<Transform>(true).Count(x=>x.name=="Thorn Burst");
    static int LiveStars()=>((Array)typeof(LateGamePresenter).GetField("eventStars",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Presenter)).Cast<object>().Count(s=>s!=null&&(bool)s.GetType().GetField("live").GetValue(s));
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
                    Check(soil.Length==Director.Current.Targets.Count && soil.Any(c=>c.visual.localPosition.y>0f),"one birth mound per root heaves during preparation (no mounds on the other zone cells)");
                    Check(soil.All(c=>OnTarget(c.transform.position)),"birth mounds sit centred on the root cells");
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
                        // Patlama çıkışı (24 Ekim): eğri deterministik örneklenir; gerçek kökler ve kapanlar patlama modunda, kapanlar dalga hâlinde.
                        var burst=typeof(LateGamePresenter).GetMethod("ThornBurst",BindingFlags.NonPublic|BindingFlags.Static);
                        float[] B(float t){var a=new object[]{t,0f,0f,0f};burst.Invoke(null,a);return new[]{(float)a[1],(float)a[2],(float)a[3]};}
                        var b0=B(.05f);var b1=B(.2f);var b2=B(.37f);var b3=B(.7f);
                        Check(b0[2]<.3f&&b1[0]>1.1f&&b1[1]<.9f&&b1[2]>1f&&b2[0]<.95f&&b2[1]>1.05f&&b3[0]==1f&&b3[1]==1f&&b3[2]==1f,"emergence bursts: pushes up, shoots out stretched, lands squashed, settles");
                        var items=((System.Collections.IList)typeof(LateGamePresenter).GetField("eventItems",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Presenter)).Cast<object>().ToArray();
                        int Id(object i)=>(int)i.GetType().GetField("identity").GetValue(i);bool Burst(object i)=>(bool)i.GetType().GetField("burst").GetValue(i);float Delay(object i)=>(float)i.GetType().GetField("riseDelay").GetValue(i);
                        int targetCount=Director.Current.Targets.Count;var rootItems=items.Where(i=>Id(i)>=10000&&Id(i)<10000+targetCount).ToArray();var trapItems=items.Where(i=>!(Id(i)>=10000&&Id(i)<10000+targetCount)&&Burst(i)).ToArray();
                        Check(rootItems.Length==2&&rootItems.All(Burst)&&trapItems.Length>0&&trapItems.Max(Delay)>trapItems.Min(Delay),$"roots burst out, traps snap up in a wave from the roots ({trapItems.Length} traps, delay {trapItems.Min(Delay):0.00}–{trapItems.Max(Delay):0.00} s; roots {rootItems.Length}: {string.Join(",",rootItems.Select(i=>Id(i)+(Burst(i)?"B":"-")))}; ids {string.Join(",",items.Select(Id))})");
                        Check(!Presenter.GetComponentsInChildren<LateGameContent>().Any(c=>c.groundRumble&&c.gameObject.activeInHierarchy&&!OnTarget(c.transform.position)),"no mounds outside the root cells while bursting");
                        // Blender toprak patlaması (24 Ekim): kök başına bir örnek, kök hücresinde; karede bellek yok.
                        Check(LiveBlasts()==Director.Current.Targets.Count,$"one soil blast per root at emergence ({LiveBlasts()})");
                        {var sync=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),Presenter,typeof(LateGamePresenter).GetMethod("SyncThornBlasts",BindingFlags.NonPublic|BindingFlags.Instance));sync(0f);
                         long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)sync(0f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                         Check(bytes==0,$"300 blast syncs allocate {bytes} bytes");}
                        // Toz kısa: kökün çıkış eğrisi bittikten (.55 sn) kısa süre sonra hiçbir toz bulutu kalmaz.
                        {var blasts=((Array)typeof(LateGamePresenter).GetField("thornBlasts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Presenter)).Cast<object>().Where(s=>s!=null&&(bool)s.GetType().GetField("live").GetValue(s)).ToArray();
                         var b=blasts[0];var ageField=b.GetType().GetField("age");float keep=(float)ageField.GetValue(b);var pose=typeof(LateGamePresenter).GetMethod("PoseThornBlast",BindingFlags.NonPublic|BindingFlags.Instance);
                         int Dust(float age){ageField.SetValue(b,age);pose.Invoke(Presenter,new object[]{b,Camera.main.transform});return ((Transform[])b.GetType().GetField("clouds").GetValue(b)).Count(x=>x.gameObject.activeSelf);}
                         int at02=Dust(.2f),at06=Dust(LateGamePresenter.ThornDustEnd+.01f);ageField.SetValue(b,keep);
                         Check(at02>=5&&at06==0,$"dust puffs up fast and is gone by {LateGamePresenter.ThornDustEnd:0.00} s ({at02} clouds at .2 s, {at06} after)");}
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
                    Check(!Presenter.GetComponentsInChildren<LateGameContent>().Any(c=>c.groundRumble),"birth mounds collapse and leave once the roots are out");
                    Check(LiveBlasts()==0&&BlastObjects()<=4,$"soil blasts finish and return to the pool ({BlastObjects()} pooled)");
                    var traps=Presenter.GetComponentsInChildren<LateGameContent>().Where(c=>c.name.StartsWith("ThornPiece")).ToArray();
                    Check(traps.Length==Director.Current.Zone(0).Count-Director.Current.Targets.Count&&!traps.Any(c=>OnTarget(c.transform.position)),$"traps only on the affected cells, none under a root ({traps.Length})");
                    Check(Roots().All(r=>r.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertices.All(v=>Camera.main.WorldToViewportPoint(f.transform.TransformPoint(v)).y<.915f))),"root vertices clear top resource counters at 1600x900");
                    Check(Roots().All(r=>r.GetComponentsInChildren<Renderer>(true).All(x=>x.sharedMaterial.shader.name=="ClickerGame/ThornComicToon")),"all root renderers use toon shader");
                    Capture("02_Active");
                    step=30;break;
                case 31:
                    // Sample the real hit deterministically; batch loop speed is not animation time.
                    Vector3 rest=root.visual.localPosition;
                    Hit(0);Check(EventHitChecks.HasFlash(root),"real hit uses plant white flash");
                    Check(LiveStars()==1,"a real hit shows one toon hit star");Check(Roots().Contains(root),"hit preserves same root instance");Check(Director.Current.Targets[0].Remaining==5,"one direct hit uses one of six");
                    Time.timeScale=0;root.visual.localScale=Vector3.one;root.Step(.07f,.6f);
                    Check(root.visual.localScale.y<.8f,"actual TargetHit squashes visual child hard (strong hit)");
                    root.Step(0f,.6f);float push=(XZ(root.visual.localPosition)-XZ(rest)).magnitude;
                    Check(push>.1f&&Quaternion.Angle(root.visual.localRotation,Quaternion.identity)>3f,$"strong hit recoils ({push:0.00}) and tilts away from the blow");
                    Check(root.transform.localScale==Vector3.one,"target root transform stays fixed");
                    Capture("03_Hit");Time.timeScale=1f;Hit(0);Hit(0);Hit(0);step=4;break;
                case 5:
                    Check(root.damageStages.Count(x=>x.activeSelf)>=2,"damage reveals persistent cracks");Capture("04_Cracked");
                    {Vector3 a=root.visual.localPosition;root.Step(0f,1.013f);Vector3 b=root.visual.localPosition;root.Step(0f,1.041f);
                     Check((XZ(b)-XZ(a)).magnitude>.004f||(XZ(root.visual.localPosition)-XZ(b)).magnitude>.004f,"a badly damaged root trembles angrily");}
                    // Yıldız havuzu: 12 art arda vuruş en çok 8 yıldız, en eskisi yeniden kullanılır; karede bellek yok.
                    {var at=typeof(LateGamePresenter).GetMethod("EventStarAt",BindingFlags.NonPublic|BindingFlags.Instance);int replaced=Presenter.EventStarsReplaced;
                     for(int i=0;i<12;i++)at.Invoke(Presenter,new object[]{root.transform.position,.7f,.07f,0f});
                     Check(LiveStars()==8&&Presenter.EventStarsReplaced>replaced,$"12 rapid stars keep at most 8 ({LiveStars()}), oldest reused ({Presenter.EventStarsReplaced-replaced})");
                     var sync=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),Presenter,typeof(LateGamePresenter).GetMethod("SyncEventStars",BindingFlags.NonPublic|BindingFlags.Instance));sync(0f);
                     long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)sync(0f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                     Check(bytes==0,$"300 star syncs allocate {bytes} bytes");sync(1f);Check(LiveStars()==0,"stars expire");}
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
                    Director.End();Check(CameraFeel.Instance.FrameShakeOffset==Vector2.zero,"cleanup leaves no event camera shake");Check(LiveBlasts()==0,"cleanup leaves no soil blast");Check(Presenter.EventItems==0&&Presenter.ActiveDecorations==0,"hard cleanup removes event, effects and tails");
                    Check(Director.StartNow(FieldEventKind.ThornSiege,0f,12),"new event starts after hard cleanup");break;
                case 9:
                    if(Director.Current.Phase!=FieldEventPhase.Active)return;
                    Check(Roots().All(r=>r.damageStages.All(d=>!d.activeSelf)),"pooled roots reset cracks");
                    Check(BlastObjects()<=4&&Presenter.ThornBlastsCut==0,$"second event reuses the pooled blasts ({BlastObjects()} objects, none cut)");
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



