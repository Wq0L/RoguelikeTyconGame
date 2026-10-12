using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class MoonSmallFieldVerification
{
 const string Key="MoonSmallFieldVerification";static int phase=-1;static double next;static int lastFrame=-1;static bool baseSeen,growSeen,returnSeen;static int frames;
 static RunEffects Fx=>RunEffects.Instance;static CrescentEffect C=>Fx.Find<CrescentEffect>();
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Add(string id){var r=(BossRewardSO)Lab("Find",id);if(!Fx.TryAdd(r.effect,-1))throw new Exception(id);}
 static MoonSmallFieldVerification(){EditorApplication.update+=Tick;}
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Tick()
 {
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   if(phase==-1){if(double.IsNaN((double)Lab("Lab")))return;
    for(int x=0;x<11;x++)for(int z=0;z<11;z++)if(x<4||x>6||z<4||z>6){((PlantSpawner)Lab("SpawnerAt",x,z)).RemoveSpawnedPlant();FieldCells.Ground(FieldCells.Grid,new GridPosition(x,z)).Lock();}
    Fx.ClearAll();Time.captureDeltaTime=1f/60;phase=0;next=EditorApplication.timeSinceStartup+.1;return;
   }
   if(phase==0){Add("hilal_darbesi");Add("buyuyen_ay");Add("dolunay");
    for(int x=4;x<=6;x++)for(int z=4;z<=6;z++)Lab("SetHealth",x,z,1);
    foreach(var juice in Object.FindObjectsByType<PlantJuice>(FindObjectsSortMode.None))juice.enabled=false;
    typeof(CrescentEffect).GetMethod("Launch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(C,new object[]{Vector3.zero});
    C.TryVisual(0,out var born);var expected=FieldCells.Grid.GetWorldPosition(born.StartX,born.Row)-born.Direction*FieldCells.CellSize*.5f;
    if(Vector3.Distance(born.Position,expected)>.001f)throw new Exception("birth does not match open boundary");
    File.WriteAllText("Logs/MoonSmallField.txt","Real Update/LateUpdate, no manually advanced timer or presenter. 3x3 field.\n");phase=1;return;
   }
   if(lastFrame==Time.frameCount){EditorApplication.QueuePlayerLoopUpdate();return;}lastFrame=Time.frameCount;frames++;
   var presenter=Object.FindAnyObjectByType<LateGamePresenter>();
   foreach(var visual in presenter.GetComponentsInChildren<LateGameContent>())
   {
    if(!visual.name.StartsWith("CyanCrescent")&&!visual.name.StartsWith("WarmReturnMoon"))continue;
    bool back=visual.name.StartsWith("WarmReturnMoon");bool grown=visual.visual.localScale.x>FieldCells.CellSize*1.5f;
    if(back?returnSeen:grown?growSeen:baseSeen)continue;
    var renderer=visual.GetComponentsInChildren<Renderer>().First(r=>r.name.EndsWith("Face"));var box=renderer.bounds;
    var screen=Camera.main.WorldToViewportPoint(box.center);
    if(screen.x<0||screen.x>1||screen.y<0||screen.y>1||screen.z<0)throw new Exception("wave outside camera "+screen);
    string label=back?"Return":grown?"Growth":"Base";
    File.AppendAllText("Logs/MoonSmallField.txt",$"{label}: world {box.center}, extents {box.extents}, viewport {screen}; active {presenter.MoonItems}, steps {C.Steps}\n");
    Lab("Capture","MoonSmall_"+label);
    if(back)returnSeen=true;else if(grown)growSeen=true;else baseSeen=true;
   }
   if(frames>90){if(!baseSeen||!growSeen||!returnSeen)throw new Exception($"Missing base {baseSeen}, growth {growSeen}, return {returnSeen}; {C.Launched}/{C.Steps}/{C.FullMoons}");File.AppendAllText("Logs/MoonSmallField.txt","PASS all three forms rendered through real lifecycle inside small field camera; 3-step forward + 3-step return.\n");Finish(0);return;}
  }catch(Exception e){File.AppendAllText("Logs/MoonSmallField.txt","FAIL "+e);Finish(1);return;}
  EditorApplication.QueuePlayerLoopUpdate();
 }
 static void Finish(int code){SessionState.SetBool(Key,false);Time.captureDeltaTime=0;EditorApplication.Exit(code);}
}
