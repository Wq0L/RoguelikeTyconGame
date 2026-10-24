using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MoonPolishVerification
{
    const string Key="MoonPolishVerification";
    static double next;static int phase;
    static readonly List<string> notes=new();
    static readonly List<int> damage=new();
    static RunEffects Fx=>RunEffects.Instance;
    static CrescentEffect C=>Fx.Find<CrescentEffect>();
    static LateGamePresenter P=>Object.FindAnyObjectByType<LateGamePresenter>();
    static readonly Vector3 Center=new(10,.5f,10);
    static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);notes.Add("PASS "+message);}
    static void Add(string id){var reward=(BossRewardSO)Lab("Find",id);Check(Fx.TryAdd(reward.effect,-1),"add "+id);}
    static void Fresh(bool growth=false,bool full=false)
    {
        Fx.ClearAll();Fx.enabled=false;Call(P,"LateUpdate");Lab("Refill",false);Lab("Strike",100f,1f,0f);
        Add("hilal_darbesi");if(growth)Add("buyuyen_ay");if(full)Add("dolunay");damage.Clear();
    }
    static object Wave(int i)=>((Array)typeof(CrescentEffect).GetField("waves",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(C)).GetValue(i);
    static T F<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Clock(double time)=>typeof(RunEffects).GetProperty("Clock").GetSetMethod(true).Invoke(Fx,new object[]{time});
    static CrescentEffect.VisualWave View(int i=0){C.TryVisual(i,out var v);return v;}
    static void Launch()=>Call(C,"Launch",Center);
    static void Step(int i)
    {
        var w=Wave(i);Clock(F<double>(w,"legStart")+F<float>(w,"legSeconds"));C.OnTimer(0,i,0,0);
    }
    static void Drain()
    {
        for(int guard=0;guard<120&&C.ActiveWaves>0;guard++)
        {
            int index=-1;double time=double.MaxValue;
            for(int i=0;i<CrescentEffect.MaxWaves;i++){var w=Wave(i);if(!F<bool>(w,"active"))continue;double due=F<double>(w,"legStart")+F<float>(w,"legSeconds");if(due<time){time=due;index=i;}}
            if(index>=0)Step(index);
        }
        Call(Fx,"DropTimers");Check(C.ActiveWaves==0,"all waves complete");
    }
    static void Damage(PlantHealth plant,int amount,DamageType type){if(type==DamageType.Reward)damage.Add(amount);}
    static void KillNext(int i)
    {
        var w=Wave(i);int step=F<int>(w,"step")+1;
        Lab("SetHealth",F<int>(w,"x")+F<int>(w,"dx")*step,F<int>(w,"z")+F<int>(w,"dz")*step,1);
    }
    static void Capture(string name)
    {
        var player=Object.FindAnyObjectByType<PlayerController>();
        F<LineRenderer>(player,"radiusIndicator").enabled=true;
        Call(player,"UpdateRadiusVisual",Center);Call(P,"LateUpdate");Lab("Capture","MoonPolish_"+name);
    }
    static MoonPolishVerification(){EditorApplication.update+=Tick;}
    public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
        if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        try
        {
            if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;phase++;next=EditorApplication.timeSinceStartup+1;return;}
            if(phase==1)
            {
                PlantHealth.AnyDamaged+=Damage;
                Fresh();Launch();var v=View();var pivot=v.Position-v.Direction*CrescentEffect.BladeTip;
                Check(Vector3.Distance(pivot,Center+v.Direction)<.001f,"blade pivot starts on actual radius-one cursor circumference");
                Check(C.CursorRange==3,"base range is three world units");Capture("BaseAtRing");
                var birth=v.Position;double start=Fx.Clock;Drain();
                Check(Mathf.Abs(Vector3.Distance(View().Position,birth)-3)<.001f,"base travels exactly three units from its spawn front");
                Check(Math.Abs(Fx.Clock-start-3/8.0)<.01,"base motion lasts about 0.375 seconds at 8 units/sec");
                Fresh(true);Launch();Check(C.CursorRange==4,"Growing Moon immediately adds one range unit");
                birth=View().Position;start=Fx.Clock;Clock(start+.006);
                Check(View().WidthCells>1&&C.Steps==0&&C.GrowthGains==0,"upgraded visual begins growing from spawn before any hit or harvest");
                KillNext(0);Step(0);
                var immediate=View();Check(immediate.WidthCells>1&&immediate.WidthCells<1.1f,"first harvest does not restart or jump spawn growth");
                Clock(start+.675/8.0);var middle=View();Check(middle.WidthCells>1&&middle.WidthCells<1.5f,"growth stays gradual during early travel");Capture("GrowthHalf");
                Step(0);var reset=View();Check(reset.WidthCells>middle.WidthCells&&reset.WidthCells<3&&C.GrowthResets==1,"nonlethal next front resets gameplay growth without restarting visual growth");
                Clock(start+3.2/8.0);var grown=View();Check(Mathf.Abs(grown.WidthCells-2.6f)<.001f,"growth still has room at eighty percent of travel");Check(grown.Opacity>0&&grown.Opacity<1,"moving wave fades during final travel");Capture("Grown");
                var item=((Array)F<Array>(P,"moonItems")).GetValue(0);
                var block=new MaterialPropertyBlock();
                foreach(var renderer in F<Renderer[]>(item,"moonRenderers"))
                {
                    renderer.GetPropertyBlock(block);
                    Check(Mathf.Abs(block.GetFloat("_Opacity")-grown.Opacity)<.001f,"renderer receives moving fade opacity");
                }
                Check(Vector3.Dot(grown.Position-immediate.Position,grown.Direction)>3f,"growth follows forward world travel");
                Clock(start+3.9/8.0);Check(View().WidthCells>grown.WidthCells&&View().WidthCells<3,"growth continues through final tenth of world travel");
                Drain();Check(Mathf.Abs(Vector3.Distance(View().Position,birth)-4)<.001f&&Math.Abs(Fx.Clock-start-4/8.0)<.01,"upgraded wave travels four units in about 0.50 seconds");
                Check(View().Opacity==0,"wave reaches zero opacity at route end");
                // Four primary waves used to occupy every slot and silently prevent every Full Moon.
                Fresh(true,true);for(int i=0;i<4;i++)Launch();Check(C.ActiveWaves==8,"four primary waves plus four extras active at launch");
                Check(C.FullMoons==4&&C.FullMoonCapacityBlocks==0&&C.ActiveWaves==8,"four primaries each spawn exactly one extra moon");
                Call(P,"LateUpdate");Check(P.MoonItems==8,"four primaries plus four extras have exactly eight visuals");
                for(int i=1;i<8;i+=2)
                {
                    var one=View(i);var parent=View(i-1);
                    Check(one.Returning&&one.Direction!=parent.Direction&&one.WidthCells>=3,"one extra starts large in a different direction "+i);
                    Check(Vector3.Distance(one.Position-one.Direction*CrescentEffect.BladeTip,Center+one.Direction)<.001f,"full moon starts at its side of original ring "+i);
                }
                Drain();Check(C.FullMoons==4,"full moon waves do not recursively trigger pairs");
                Fresh(true,true);Launch();
                Check(C.ActiveWaves==2&&View(1).WidthCells==3,"single trigger leaves one primary and one large extra, not three waves");
                var extraBirth=View(1).Position;
                Check(F<float>(Wave(1),"startDelay")==0,"Full Moon has no stationary spawn delay");
                Clock(Fx.Clock+.001);
                Check(Mathf.Abs(Vector3.Distance(View(1).Position,extraBirth)-.008f)<.0001f,"Full Moon moves at eight units per second immediately after spawn");
                Capture("FullMoonSingle");damage.Clear();Step(1);
                Check(View(1).WidthCells==3&&damage.Count>0&&damage.All(x=>x==56),"extra stays large and applies configured 56 damage");Drain();
                Fresh(true,true);Lab("Strike",100f,1.5f,0f);
                for(int x=0;x<11;x++)for(int z=0;z<11;z++)Lab("SetHealth",x,z,x>=4&&x<=6&&z>=4&&z<=6?150:1);
                Lab("AttackAt",Center);Drain();Check(C.Launched==1&&C.FullMoons==1,"real direct attack charges a ring-born wave and triggers Full Moon outside the attack circle");
                Fresh(true,true);
                for(int cycle=0;cycle<50;cycle++)
                {
                    Lab("Refill",false);Launch();
                    Check(C.FullMoons==cycle+1&&C.ActiveWaves==2,"Full Moon triggers without harvest on launch "+(cycle+1));
                    Drain();
                }
                Check(C.GrowthGains==0&&C.FullMoons==50,"fifty nonlethal primary waves produce fifty extras without kill prerequisites");
                // Repeated awards are not necessary: existing wave arrays and visual pools are reused.
                Fresh(true,true);int warmed=0;
                for(int cycle=0;cycle<24;cycle++)
                {
                    Lab("Refill",false);Launch();KillNext(0);Step(0);KillNext(0);Step(0);Call(P,"LateUpdate");Drain();
                    P.enabled=false;P.enabled=true;
                    int count=P.GetComponentsInChildren<Transform>(true).Length;
                    if(cycle==3)warmed=count;
                    Check(((System.Collections.IDictionary)F<System.Collections.Generic.Dictionary<Material,Material>>(P,"moonFadeMaterials")).Count<=2,"moon fade material cache remains bounded");
                    if(cycle>3)Check(count==warmed,"pooled presentation count stable on cycle "+cycle);
                }
                Fresh(true,true);Fx.TryBounds(out var bounds);
                Check((bool)Call(C,"StartCursor",0,new Vector3(0,.5f,0),1f,1,bounds,4f,80,0,false,0f),"outward corner wave starts beyond field boundary");
                birth=View().Position;Step(0);Check(View().Active&&damage.Count==0,"outward wave survives invalid grid cell without damage");
                Call(C,"SpawnFullMoon",Wave(0));Check(C.FullMoons==1&&View(1).Direction!=View().Direction&&View(1).WidthCells==3,"corner extra keeps full size and different direction");
                Drain();Check(Mathf.Abs(Vector3.Distance(View().Position,birth)-4)<.001f&&View().WidthCells==3,"corner wave completes four world units without visual clipping");
                Fresh();
                for(int x=0;x<11;x++)for(int z=0;z<11;z++)if(x<4||x>6||z<4||z>6)FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();
                Launch();birth=View().Position;Step(0);int validHits=damage.Count;Step(0);
                Check(View().Active&&damage.Count==validHits,"crossing locked cells preserves motion and skips damage");
                Drain();Check(Mathf.Abs(Vector3.Distance(View().Position,birth)-3)<.001f,"small field does not shorten three-unit world travel");
                GridUnlockManager.Instance.UnlockNextTier(11);
                Fresh(true);Launch();Call(Fx,"HandleRoundEnded");Call(P,"LateUpdate");Check(C.ActiveWaves==0&&P.MoonItems==0&&Fx.PendingTimers==0,"round cleanup releases waves, visuals and timers");
                // Actual timed execution, not direct callbacks, for movement/arrival agreement.
                Fresh();Launch();Fx.enabled=true;Time.captureDeltaTime=.025f;realStart=Fx.Clock;damage.Clear();phase++;return;
            }
            if(phase==2)
            {
                if(Fx.Clock-realStart<1.35){EditorApplication.QueuePlayerLoopUpdate();return;}
                Check(C.ActiveWaves==0&&Fx.PendingTimers==0&&damage.Count==2&&damage.All(x=>x==80),"real scheduler completes slow route with two correctly scaled hits");
                Finish(null);
            }
        }
        catch(Exception e){Finish(e.InnerException??e);}
    }
    static double realStart;
    static void Finish(Exception error)
    {
        PlantHealth.AnyDamaged-=Damage;SessionState.SetBool(Key,false);Time.captureDeltaTime=0;Time.timeScale=1;
        File.WriteAllLines("Logs/MoonPolishVerification.txt",new[]{error==null?"PASS "+notes.Count:"FAIL "+error}.Concat(notes));EditorApplication.Exit(error==null?0:1);
    }
}
