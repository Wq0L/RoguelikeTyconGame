using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AftershockWaveVerification
{
    const string Key="AftershockWaveVerification";
    static double next; static int phase,frame=1000000;
    static readonly List<string> notes=new();
    static readonly List<GridPosition> hit=new();
    static BehaviorEchoes Echo=>BehaviorEchoes.Instance;
    static GridSystem Grid=>FieldCells.Grid;
    static object Lab(string name,params object[] args)
    {
        var m=typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
        return m.Invoke(null,m.GetParameters().Select((p,i)=>i<args.Length?args[i]:p.DefaultValue).ToArray());
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);notes.Add("PASS "+text);}
    static void Advance(float dt)=>typeof(BehaviorEchoes).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Echo,new object[]{dt,frame++});
    static void Reset(){Echo.DropAll();hit.Clear();VFXManager.Instance.enabled=false;VFXManager.Instance.enabled=true;}
    static void Damage(PlantHealth h,int amount,DamageType type)
    {
        if(type!=DamageType.Explosion||!h.LastHitLink.Echo)return;
        Check(amount==70,"aftershock preserves configured damage");
        hit.Add(Grid.GetGridPosition(h.transform.position));
    }
    static void Schedule(params GridPosition[] footprint)
    {
        var source=Grid.GetGridObject(footprint[0]).GetPlanterBrain();
        Check(Echo.ScheduleExplosion(source,footprint.Select(c=>Grid.GetGridObject(c)).ToList(),Vector3.zero,100,new BehaviorEcho(.2f,.7f,99)),"wave scheduled");
    }
    static void Capture(int ring)
    {
        foreach(var p in VFXManager.Instance.GetComponentsInChildren<ParticleSystem>())p.Simulate(.08f,false,false);
        Lab("Capture","AftershockWave_Step"+ring);
    }
    static AftershockWaveVerification(){EditorApplication.update+=Tick;}
    public static void RunBatch()
    {
        if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
        GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
        if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        try
        {
            if(phase==0)
            {
                if(double.IsNaN((double)Lab("Lab")))return;
                Time.captureDeltaTime=0;RunEffects.Instance.enabled=false;phase++;next=EditorApplication.timeSinceStartup+1;return;
            }
            Time.timeScale=0;PlantHealth.AnyDamaged+=Damage;
            Reset();Schedule(new GridPosition(5,5));Advance(.19f);Check(hit.Count==0,"no early first ring");Advance(0);Check(hit.Count==0,"pause preserves timer");
            int total=0;
            for(int ring=1;ring<=3;ring++)
            {
                Advance(ring==1?.01f:.2f);
                int distance=ring+1, count=4*distance;
                Check(hit.Count==total+count,"ring "+ring+" has "+count+" targets");
                Check(hit.Skip(total).All(c=>Math.Abs(c.x-5)+Math.Abs(c.z-5)==distance),"ring "+ring+" starts beyond normal explosion");
                Capture(ring);total+=count;
                VFXManager.Instance.enabled=false;VFXManager.Instance.enabled=true;
            }
            Check(hit.Select(FieldCells.Key).Distinct().Count()==36&&Echo.Pending==0,"36 unique cells, exactly three rings, no center/repeats");
            Advance(10);Check(hit.Count==36,"no fourth ring");
            Reset();Schedule(new GridPosition(5,5));Advance(10);Check(hit.Count==8&&Echo.Pending==1,"long frame does not collapse three beats");Echo.DropAll();int stopped=hit.Count;Advance(10);Check(hit.Count==stopped&&Echo.Pending==0,"cancellation stops remaining rings");
            Reset();int skipped=VFXManager.Instance.AftershockVisualsSkipped;
            Schedule(new GridPosition(5,5));for(int i=0;i<3;i++)Advance(.2f);
            Check(VFXManager.Instance.AftershockLeased==36&&VFXManager.Instance.AftershockVisualsSkipped==skipped,"all 36 cell visuals fit simultaneously without skips");
            Reset();Schedule(new GridPosition(0,0));for(int i=0;i<3;i++)Advance(.2f);Check(hit.Count==12&&hit.All(c=>c.x>=0&&c.z>=0),"corner wave remains within grid");
            for(int z=0;z<11;z++)FieldCells.Ground(Grid,new GridPosition(7,z)).Lock();
            Reset();Schedule(new GridPosition(5,5));for(int i=0;i<3;i++)Advance(.2f);Check(hit.All(c=>c.x<7),"closed wall cannot be hit or crossed");
            GridUnlockManager.Instance.UnlockNextTier(11);
            Reset();Schedule(new GridPosition(5,5),new GridPosition(6,5));for(int i=0;i<3;i++)Advance(.2f);
            Check(hit.Select(FieldCells.Key).Distinct().Count()==hit.Count&&hit.All(c=>Math.Min(Math.Abs(c.x-5)+Math.Abs(c.z-5),Math.Abs(c.x-6)+Math.Abs(c.z-5))>=2),"multi-cell footprint excludes initial area and deduplicates fronts");
            Reset();Schedule(new GridPosition(5,5));Echo.enabled=false;Check(Echo.Pending==0,"disable clears wave jobs");Echo.enabled=true;
            // Warm state and VFX caches, then verify repeated waves reuse the same objects.
            PlantHealth.AnyDamaged-=Damage;
            for(int n=0;n<3;n++){Reset();Schedule(new GridPosition(5,5));for(int i=0;i<3;i++)Advance(.2f);}
            int states=Echo.WaveStatesCreated, visuals=VFXManager.Instance.AftershockCreated;
            for(int n=0;n<100;n++){Reset();Schedule(new GridPosition(5,5));for(int i=0;i<3;i++)Advance(.2f);}
            Check(Echo.WaveStatesCreated==states&&VFXManager.Instance.AftershockCreated==visuals,"100 waves reuse warmed state and VFX pools");Reset();
            Check(VFXManager.Instance.AftershockLeased==0&&Echo.Pending==0,"no active jobs or visual leases after cleanup");Finish(null);
        }
        catch(Exception e){Finish(e.InnerException??e);}
    }
    static void Finish(Exception e)
    {
        PlantHealth.AnyDamaged-=Damage;SessionState.SetBool(Key,false);Time.timeScale=1;
        File.WriteAllLines("Logs/AftershockWaveVerification.txt",new[]{e==null?"PASS":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);
    }
}
