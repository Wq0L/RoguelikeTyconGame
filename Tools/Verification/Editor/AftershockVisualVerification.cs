using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AftershockVisualVerification
{
    const string Key="AftershockVisualVerification";
    static int step, cycles, damaged, objects, materials, meshes;
    static double next;
    static readonly List<string> notes=new();
    static VFXManager V=>VFXManager.Instance;
    static object Lab(string name,params object[] args)
    {
        var m=typeof(GecOyunV1Verification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
        return m.Invoke(null,m.GetParameters().Select((p,i)=>i<args.Length?args[i]:p.DefaultValue).ToArray());
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);notes.Add("PASS "+text);}
    static void Capture(string name)=>Lab("Capture","Aftershock_"+name);
    static Vector3 Center=>FieldCells.World(FieldCells.Grid,new GridPosition(5,5));
    static AftershockVisualVerification(){EditorApplication.update+=Tick;}
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
        double wait=.1;
        try
        {
            switch(step)
            {
                case 0:
                    if(double.IsNaN((double)Lab("Lab")))return;
                    Time.captureDeltaTime=0;RunEffects.Instance.enabled=false;wait=1;break;
                case 1: V.PlayExplosion(Center);break;
                case 2: Capture("Normal");wait=4;break;
                case 3:
                    Check(V.ExplosionLeased==0,"normal explosion returned after lifetime");
                    var health=FieldCells.LivingPlant(FieldCells.Grid,new GridPosition(6,5));
                    health.OnDamaged+=OnDamage;
                    var source=(PlanterBrain)Lab("BrainAt",5,5);
                    Check(BehaviorEchoes.Instance.ScheduleExplosion(source,source.OccupiedGrids,Center,100,new BehaviorEcho(0,.7f,1.25f)),"real aftershock scheduled");
                    break;
                case 4:
                    Capture("Second");
                    Check(V.AftershockLeased==1 && V.ExplosionLeased==0,"only one Battle_Effect_White; no normal explosion duplicates");
                    Check(damaged==1 && BehaviorEchoes.Instance.Hits(DamageType.Explosion)>0,"real damage invokes normal plant OnDamaged response");
                    var outline=Object.FindAnyObjectByType<AreaOutlineFeedback>();
                    Check(outline==null||outline.AftershockPlayed==0,"no red aftershock outline");
                    var active=V.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Battle_Effect_White"));
                    Check(active.localScale.x<1f,"aftershock scale smaller than normal explosion");
                    wait=4;break;
                case 5:
                    Check(V.AftershockLeased==0,"aftershock naturally returned to pool");
                    for(int i=0;i<40;i++)V.PlayAftershock(Center);
                    Check(V.AftershockCreated==32&&V.AftershockLeased==32&&V.AftershockVisualsSkipped>=8,"pool cap 32; excess visuals skipped");
                    wait=4;break;
                case 6:
                    Check(V.AftershockLeased==0,"all saturated effects returned");
                    if(cycles==0)
                    {
                        objects=V.GetComponentsInChildren<Transform>(true).Length;
                        materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                        meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
                    }
                    else Check(V.GetComponentsInChildren<Transform>(true).Length==objects&&Resources.FindObjectsOfTypeAll<Material>().Length==materials&&Resources.FindObjectsOfTypeAll<Mesh>().Length==meshes,"stable warmed object/material/mesh counts, cycle "+cycles);
                    if(cycles++<8){for(int i=0;i<40;i++)V.PlayAftershock(Center);step--;wait=4;break;}
                    V.PlayAftershock(Center);V.enabled=false;
                    Check(V.AftershockLeased==0&&V.ExplosionLeased==0,"disable returns all leases");V.enabled=true;
                    V.PlayAftershock(Center);Check(V.AftershockLeased==1&&V.AftershockCreated==32,"pool reuses cleanly after disable");
                    V.enabled=false;Finish(null);return;
            }
            step++;next=EditorApplication.timeSinceStartup+wait;
        }
        catch(Exception e){Finish(e.InnerException??e);}
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static void OnDamage(bool crit){damaged++;}
    static void Finish(Exception e)
    {
        SessionState.SetBool(Key,false);
        File.WriteAllLines("Logs/AftershockVisualVerification.txt",new[]{e==null?"PASS":"FAIL "+e}.Concat(notes));
        EditorApplication.Exit(e==null?0:1);
    }
}
