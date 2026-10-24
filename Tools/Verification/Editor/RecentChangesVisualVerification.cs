using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RecentChangesVisualVerification
{
    const string Key = "RecentChangesVisualVerification";
    static int step;
    static double next;
    static string pendingCapture;
    static readonly List<string> notes = new();
    static LateGamePresenter Presenter => Object.FindAnyObjectByType<LateGamePresenter>();
    static RunEffects Fx => RunEffects.Instance;
    static object Lab(string name, params object[] args)
    {
        var method = typeof(GecOyunV1Verification).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        return method.Invoke(null, method.GetParameters().Select((p,i) => i < args.Length ? args[i] : p.DefaultValue).ToArray());
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target,args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); notes.Add("PASS " + message); }
    static void Capture(string name) => Lab("Capture", "Recent_" + name);
    static RecentChangesVisualVerification() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        GecOyunV1Verification.RunBatch();
        SessionState.SetBool("GecOyunV1Verification",false);
        SessionState.SetBool(Key,true);
    }
    static void Antennas(int side, int size, bool add)
    {
        if (add)
        {
            Fx.ClearAll();
            for(int x=0;x<11;x++) for(int z=0;z<11;z++)
                if(x<4||x>6||z<4||z>6) FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();
            Lab("Take","elektrik_anteni",AntennaSlots.Id(side,4));
            Lab("Take","iletken_hat",RunEffectSO.NoPlacement);
        }
        GridUnlockManager.Instance.UnlockNextTier(size);
        // The fixture pre-populates all 121 cells. Hide its out-of-bounds planters for readable small-field captures.
        for(int x=0;x<11;x++) for(int z=0;z<11;z++)
        {
            var entry=FieldCells.Grid.GetGridObject(new GridPosition(x,z));
            var brain=entry.GetPlanterBrain();
            if(brain!=null) foreach(var renderer in brain.GetComponentsInChildren<Renderer>()) renderer.enabled=!entry.GetGroundCellCached().IsLocked;
        }
        Call(Presenter,"Update");
        Call(Presenter,"StepAntennas",0f,false);
        var antennas=Fx.Find<AntennaEffect>();
        Check(antennas.Count==2 && antennas.Slot(0)==AntennaSlots.Id(side,4) && antennas.Slot(1)==AntennaSlots.Opposite(antennas.Slot(0)),"automatic opposite pair retains lane 4, side " + side + ", grid " + size);
        var models=Presenter.GetComponentsInChildren<LateGameContent>().Where(c=>c.antenna!=null).ToArray();
        FieldCells.TryBounds(out var bounds);
        Check(models.Length==2,"exactly two visible antenna models");
        foreach(var model in models)
        {
            var p=model.transform.position;
            Check(Enumerable.Range(0,2).Any(i=> { var expected=AntennaSlots.World(FieldCells.Grid,bounds,antennas.Slot(i)); return Mathf.Abs(p.x-expected.x)<.001f&&Mathf.Abs(p.z-expected.z)<.001f; }),"model is at current outer edge");
        }
        pendingCapture="Antenna_"+(side==0?"Row":"Column")+"_"+size;
    }
    static void Electric(bool yellow, string name)
    {
        var lines=HarvestBehaviorManager.Instance.GetComponentsInChildren<LineRenderer>().Where(l=>l.enabled).ToArray();
        Check(lines.Length>0,"electric lines visible: "+name);
        Check(lines.All(l=>yellow ? l.startColor.r>.9f&&l.startColor.b<.8f : l.startColor.b>.99f),"electric palette: "+name);
        Capture(name);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
        if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        try
        {
            if(pendingCapture!=null){Capture(pendingCapture);pendingCapture=null;}
            switch(step)
            {
                case 0:
                    if(double.IsNaN((double)Lab("Lab")))return;
                    Time.captureDeltaTime=0; Time.timeScale=1; Fx.enabled=false;
                    FieldEventDirector.Instance.End();
                    Check(MetaSave.LastLoad==MetaSave.LoadResult.MemoryOnly,"isolated scene uses no real saves");
                    break;
                case 1: Antennas(0,3,true);break;
                case 2: Antennas(0,7,false);break;
                case 3: Antennas(0,11,false);break;
                case 4: BossRewardManager.Instance.ClearAll();Antennas(2,3,true);break;
                case 5: Antennas(2,11,false);break;
                case 6:
                    BossRewardManager.Instance.ClearAll();Call(Presenter,"Update");
                    var catalog=new List<BossRewardSO>();MechanicsTestLab.TestRewards.Catalog(catalog);
                    Check(catalog.All(r=>r.id!="kritik_patlama"),"active test menu catalog excludes Kritik Patlama");
                    Check(MechanicsTestLab.ScenarioPresets.All().All(s=>!s.name.Contains("Kritik Patlama")),"visible scenario list excludes Kritik Patlama");
                    File.WriteAllLines("Logs/Recent_RewardCatalog.txt",catalog.Select(r=>r.displayName));
                    var source=(PlanterBrain)Lab("BrainAt",5,5);
                    Check(HarvestBehaviorManager.Instance.TryElectric(source,1),"first electric wave runs");
                    Electric(false,"Electric_First");
                    HarvestBehaviorManager.Instance.ClearAll();
                    Check(BehaviorEchoes.Instance.ScheduleElectric(source,1,new BehaviorEcho(0,.7f,1f,2)),"real second wave scheduled");
                    Call(BehaviorEchoes.Instance,"Update");
                    Electric(true,"Electric_Second");
                    HarvestBehaviorManager.Instance.ClearAll();
                    Check(HarvestBehaviorManager.Instance.TryElectric(source,1),"normal wave runs after warm wave returned to pool");
                    Electric(false,"Electric_ReusedNormal");
                    HarvestBehaviorManager.Instance.ClearAll();
                    Check(HarvestBehaviorManager.Instance.TryElectricEcho(source,1,2,out _,out _,out _),"overload shared path runs");
                    Electric(false,"Electric_OverloadPath");
                    break;
                default: Finish(null);return;
            }
            step++;next=EditorApplication.timeSinceStartup+1;
        }
        catch(Exception e){Finish(e.InnerException??e);}
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);Time.timeScale=1;
        File.WriteAllLines("Logs/RecentChangesVisualVerification.txt",new[]{error==null?"PASS":"FAIL "+error}.Concat(notes));
        EditorApplication.Exit(error==null?0:1);
    }
}
