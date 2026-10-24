using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Açgözlü Hasat "aç ağız halkası" (24 Ekim 2026; eski amber diş işaretinin yerine): gerçek hak / harcama / kayıp cue'ları,
// halkaya oturma (ekrandaki merkez ve yarıçap), halka büyüyünce büyüme, hareket yayı, pause, havuz sınırı, kare başına bellek,
// round / kapatma / ödül kaldırma temizliği ve nesne sayısı kararlılığı. Mekanik ayrıntı GecOyunV1Verification.Greedy'de.
[InitializeOnLoad]
public static class GreedArtVerification
{
 const string Key="GreedArtVerification";static double next;static int phase;static object identity;static float pausedAge;
 static readonly List<string> notes=new();static readonly List<RewardCue> cues=new();
 const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static GreedArtVerification(){EditorApplication.update+=Tick;}
 static object Lab(string n,params object[] a){var m=typeof(GecOyunV1Verification).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);return m.Invoke(null,m.GetParameters().Select((p,i)=>i<a.Length?a[i]:p.DefaultValue).ToArray());}
 static void Check(bool v,string n){if(!v)throw new Exception(n);notes.Add("PASS "+n);}
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags|BindingFlags.Public).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags|BindingFlags.Public).SetValue(o,v);
 static void Sync(LateGamePresenter p)=>typeof(LateGamePresenter).GetMethod("SyncGreed",Flags).Invoke(p,null);
 static object Live(LateGamePresenter p)=>Field(p,"greedLive");
 static IList Jaws(LateGamePresenter p)=>(IList)Field(p,"greedJaws");
 static string Phase(object jaw)=>Field(jaw,"phase").ToString();
 static Transform Root(object jaw)=>((GameObject)Field(jaw,"root")).transform;
 static GreedJawArtSO Art=>Resources.Load<GreedJawArtSO>(GreedJawArtSO.ResourcePath);
 public static void RunBatch(){if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");GecOyunV1Verification.RunBatch();SessionState.SetBool("GecOyunV1Verification",false);SessionState.SetBool(Key,true);}
 static void Cue(RewardCue c){if(c.Kind==RewardCueKind.GreedReady||c.Kind==RewardCueKind.GreedSpent||c.Kind==RewardCueKind.GreedLost)cues.Add(c);}
 static void Arm(){Lab("Refill");Lab("Strike",100f,1.2f);Lab("SetHealth",1,1,1);Lab("SetHealth",2,1,1);Lab("SetHealth",1,2,1);Lab("Attack",1,1);}
 // Ağız halkaya oturuyor mu: ekranda aynı merkez (ortografik kaydırma yeri değiştirmez) ve tasarım yarıçapı × ölçek = halka yarıçapı.
 static void Fits(object jaw,PlayerController player,string label){
  var cam=Camera.main;Vector3 ring=player.CursorWorldPosition+Vector3.up*.05f;
  Vector2 a=cam.WorldToScreenPoint(Root(jaw).position),b=cam.WorldToScreenPoint(ring);
  float radius=Art.designRadius*Root(jaw).localScale.x;
  Check((a-b).magnitude<.5f&&Mathf.Abs(radius-player.CursorRadius)<1e-3f,$"{label}: jaw sits on the ring (screen offset {(a-b).magnitude:0.000} px, radius {radius:0.000} vs ring {player.CursorRadius:0.000})");
 }
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(next==0){next=EditorApplication.timeSinceStartup+2;return;}
  if(EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
  try{
   if(phase==0){if(double.IsNaN((double)Lab("Lab")))return;Lab("Greedy");Check(true,"existing mechanics regression: x1.5, 3 direct harvests, empty retention, single loss, no empowered rearm");RunEffects.Cued+=Cue;phase=1;}
   var p=UnityEngine.Object.FindFirstObjectByType<LateGamePresenter>();var fx=RunEffects.Instance;var greed=fx.Find<GreedyHarvestEffect>();
   var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var pose=typeof(PlayerController).GetMethod("UpdateRadiusVisual",Flags);
   if(phase==1){
    Check(Art!=null&&Art.Valid,"GreedJawArt asset loads and is complete");
    // Lab'da PlayerController.Update halkayı kurmaz: halka gerçek yolundan (UpdateRadiusVisual) bir hücreye konur.
    p.enabled=false;p.enabled=true;Arm();pose.Invoke(player,new object[]{FieldCells.World(fx.Grid,new GridPosition(5,5))});Sync(p);identity=Live(p);
    Check(player.CursorRadius>0f,"ring has a real radius ("+player.CursorRadius+")");
    Check(greed.Ready&&identity!=null&&Jaws(p).Count==1&&Phase(identity)=="Ready"&&cues.Count==1,"real three-harvest attack shows one ready jaw");
    Sync(p);Check(ReferenceEquals(identity,Live(p))&&Jaws(p).Count==1,"repeated sync retains one instance");
    Fits(identity,player,"normal ring");float normalRadius=player.CursorRadius;
    int chomps=p.GreedChomps;   // Lab("Greedy") gerçek harcamalar yaptı
    var spawner=Lab("SpawnerAt",10,10);spawner.GetType().GetMethod("RemoveSpawnedPlant").Invoke(spawner,null);Lab("Strike",100f,.4f);Lab("Attack",10,10);Sync(p);
    Check(greed.Ready&&ReferenceEquals(identity,Live(p))&&cues.Count==1&&p.GreedChomps==chomps,"empty attack keeps the same jaw (no chomp, no cue)");
    Lab("Strike",100f,1.2f);Lab("Attack",8,2);Sync(p);
    Check(!greed.Ready&&Live(p)==null&&p.GreedChomps==chomps+1&&cues.Last().Kind==RewardCueKind.GreedSpent,"multi-target spend: exactly one chomp");
    var exit=Jaws(p)[0];Check(Jaws(p).Count==1&&Phase(exit)=="Chomp"&&((Vector3)Field(exit,"center")-(Vector3)Lab("PosAt",8,2)).sqrMagnitude<1e-4f,"chomp stays at the real attack centre");
    p.enabled=false;p.enabled=true;Arm();Sync(p);int losses=p.GreedLosses;Lab("Strike",100f,.4f);Lab("Attack",8,6);Sync(p);
    Check(!greed.Ready&&Live(p)==null&&p.GreedLosses==losses+1&&Jaws(p).Count==1&&Phase(Jaws(p)[0])=="Lose"&&cues.Last().Kind==RewardCueKind.GreedLost,"single target: one lose animation, charge gone");
    // Reaktör + Hasat Ritmi: altın halka büyür; ağız da büyümeli.
    Lab("ReactorCase");Lab("Take","acgozlu_hasat");Arm();greed=fx.Find<GreedyHarvestEffect>();
    var rhythm=BossRewardManager.Instance.Pool.stages.SelectMany(x=>x.rewards).Select(x=>x.reward).First(x=>x.rhythmHarvests>0);
    Check(BossRewardManager.Instance.EditorGrant(rhythm,out var refusal),"grant existing Rhythm: "+refusal);
    RunPower.Rhythm.Complete(DirectAttack.Normal,RunPower.Rhythm.Threshold,true);
    pose.Invoke(player,new object[]{FieldCells.World(fx.Grid,new GridPosition(5,5))});Sync(p);typeof(LateGamePresenter).GetMethod("SyncReactor",Flags).Invoke(p,null);
    Check(Live(p)!=null&&Field(p,"reactorItem")!=null&&player.ShowsChargedCursor,"Greed jaw, Reactor and golden Rhythm ring coexist");
    Fits(Live(p),player,"golden Rhythm ring");
    notes.Add($"ring radius normal {normalRadius:0.000}, Rhythm {player.CursorRadius:0.000}");
    if(player.CursorRadius>normalRadius+1e-3f)Check(Root(Live(p)).localScale.x*Art.designRadius>normalRadius+1e-3f,"bigger ring → bigger jaw");
    identity=Live(p);Time.timeScale=0;pausedAge=(float)Field(identity,"age");phase=2;next=EditorApplication.timeSinceStartup+.3;return;
   }
   if(phase==2){Check((float)Field(identity,"age")==pausedAge&&greed.Ready,"pause freezes the jaw animation and keeps the real charge");Time.timeScale=1;phase=3;next=EditorApplication.timeSinceStartup;return;}
   if(phase==3){
    // Hareket yayı: imleç sağa kayarken dişler sola yatar; durunca az sönümlü yay sallanır (işaret değişir) ve söner.
    var jaw=Live(p);var follow=typeof(LateGamePresenter).GetMethod("FollowCursor",Flags);Vector3 start=player.CursorWorldPosition;
    for(int i=1;i<=15;i++){pose.Invoke(player,new object[]{start+Vector3.right*(.12f*i)});follow.Invoke(p,new object[]{jaw,1f/60f});}
    Vector3 lean=(Vector3)Field(jaw,"lean");Check(lean.x< -.05f&&lean.magnitude<=.56f,$"moving right leans the teeth back (lean {lean})");
    bool flipped=false;for(int i=0;i<180;i++){follow.Invoke(p,new object[]{jaw,1f/60f});if(((Vector3)Field(jaw,"lean")).x>.01f)flipped=true;}
    Check(flipped&&((Vector3)Field(jaw,"lean")).magnitude<.01f,"stopping: spring overshoots once (wobble) then settles");
    Sync(p);Fits(jaw,player,"after motion");   // konum poz adımında yazılır (FollowCursor yalnız durumu günceller)
    // Kare başına bellek: hazır ağzın 300 senkronu 0 bayt ayırmalı.
    // Giriş dumanı: köşe pofları önce, dudaklar buluşunca ortadakiler; giriş bitince hiç poff kalmaz.
    var poseJaw=typeof(LateGamePresenter).GetMethod("PoseGreedJaw",Flags);int Smoke(float age){Set(Live(p),"age",age);poseJaw.Invoke(p,new object[]{Live(p),Camera.main});return ((Transform[])Field(Live(p),"entryPuffs")).Count(x=>x.gameObject.activeSelf);}
    int s1=Smoke(.1f),s2=Smoke(.3f),s3=Smoke(1f);Check(s1==4&&s2==6&&s3==0,$"entrance smoke: corners {s1}, + lips meeting {s2}, after entrance {s3}");
    // Giriş (dudak süpürmesi köşe yazar) de ölçüme girer: yaş başa alınır.
    var sync=(Action)Delegate.CreateDelegate(typeof(Action),p,typeof(LateGamePresenter).GetMethod("SyncGreed",Flags));Set(Live(p),"age",0f);sync();
    var shown=(float[])Field(Live(p),"sweepShown");Check(shown[0]<1f&&shown[1]<1f,$"entrance: lips start from the mouth corners (sweep {shown[0]:0.00})");
    long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)sync();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
    Check(bytes==0,$"300 ready-jaw syncs (entrance included) allocate {bytes} bytes");
    Check(shown[0]==1f&&shown[1]==1f&&(float)Field(Live(p),"age")>1f,"entrance finished: full gums restored");
    // Tam döngü belleği: hazır → hap → (havuzdan) yeni hazır → kayıp. Isınmadan sonra (havuz, liste kapasitesi, ses kaynağı) 0 bayt.
    var useDel=(Action<RewardCue>)Delegate.CreateDelegate(typeof(Action<RewardCue>),p,typeof(LateGamePresenter).GetMethod("GreedUsed",Flags));
    var loseDel=(Action<RewardCue>)Delegate.CreateDelegate(typeof(Action<RewardCue>),p,typeof(LateGamePresenter).GetMethod("GreedLost",Flags));
    var spendCue=new RewardCue(RewardCueKind.GreedSpent,player.CursorWorldPosition);var lossCue=new RewardCue(RewardCueKind.GreedLost,player.CursorWorldPosition);
    int chompsBefore=p.GreedChomps,lossesBefore=p.GreedLosses;
    void Cycle(){sync();useDel(spendCue);for(int i=0;i<80;i++)sync();loseDel(lossCue);for(int i=0;i<80;i++)sync();}
    Cycle();Cycle();
    long cycleStart=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<5;i++)Cycle();long cycleBytes=GC.GetAllocatedBytesForCurrentThread()-cycleStart;
    Check(p.GreedChomps==chompsBefore+7&&p.GreedLosses==lossesBefore+7,"7 full cycles ran (chomp and lose each time)");
    Check(cycleBytes==0,$"5 ready → chomp → ready → lose cycles (after warm-up) allocate {cycleBytes} bytes");
    ((LineRenderer)typeof(PlayerController).GetField("radiusIndicator",Flags).GetValue(player)).enabled=true;Lab("Capture","Greed_NormalCamera");
    p.enabled=false;Check(Live(p)==null&&Jaws(p).Count==0,"disable clears the jaw immediately");p.enabled=true;Sync(p);Check(Live(p)!=null,"re-enable restores only the actual ready state");
    int count=p.GetComponentsInChildren<Transform>(true).Length;
    for(int i=0;i<12;i++){RoundManager.Instance.EditorJumpToRound(3);Check(Live(p)==null&&Jaws(p).Count==0,"round cleanup cycle "+i);Sync(p);}
    Check(p.GetComponentsInChildren<Transform>(true).Length==count,"12 round/pool cycles retain stable object count");
    Check(greed.Ready&&Live(p)!=null,"existing cross-round charge rule preserved; jaw returns in active round");
    // Havuz sınırı: art arda harcama/kayıp (çok hızlı saldırı) en çok 3 örnek; fazlası en eski çıkışı keser.
    var used=typeof(LateGamePresenter).GetMethod("GreedUsed",Flags);var lost=typeof(LateGamePresenter).GetMethod("GreedLost",Flags);int cut=p.GreedJawsCut;
    for(int i=0;i<8;i++){(i%2==0?used:lost).Invoke(p,new object[]{new RewardCue(i%2==0?RewardCueKind.GreedSpent:RewardCueKind.GreedLost,player.CursorWorldPosition)});}
    Check(Jaws(p).Count<=3&&p.GreedJawsCut>cut,$"burst of 8 releases keeps at most 3 jaws ({Jaws(p).Count}), cuts the oldest ({p.GreedJawsCut-cut})");
    int copies=((IList)Field(p,"greedRuntimeMeshes")).Count;Check(copies>0&&copies<=6&&copies%2==0,$"runtime gum copies stay within the pool ({copies} ≤ 6)");
    int pooled=p.GetComponentsInChildren<Transform>(true).Length;
    for(int i=0;i<10;i++){RoundManager.Instance.EditorJumpToRound(3);Arm();Sync(p);used.Invoke(p,new object[]{new RewardCue(RewardCueKind.GreedSpent,player.CursorWorldPosition)});Sync(p);}
    Check(p.GetComponentsInChildren<Transform>(true).Length==pooled,"10 ready → chomp cycles reuse the pool (object count stable)");
    // Saldırı ritmi: hap bir aralığın %90'ına sığar (yavaşta tasarım hızı, en çok 4 kat); boş savuruşta "çat-çat" başlar.
    var intervalNow=typeof(PlayerController).GetProperty("AttackIntervalNow");var attacksDone=typeof(PlayerController).GetProperty("AttacksPerformed");
    if(!greed.Ready)Arm();
    foreach(var c in new[]{(1f,1f),(.2f,.55f/(.2f*.9f)),(.05f,4f)}){
     intervalNow.SetValue(player,c.Item1);Sync(p);var j=Live(p);Check(j!=null,"ready jaw at interval "+c.Item1);
     used.Invoke(p,new object[]{new RewardCue(RewardCueKind.GreedSpent,player.CursorWorldPosition)});float rate=(float)Field(j,"rate");
     Check(Mathf.Abs(rate-c.Item2)<1e-3f&&Phase(j)=="Chomp",$"attack interval {c.Item1} s → chomp rate {rate:0.00} (plays in {.55f/rate:0.000} s)");
    }
    Sync(p);var clacking=Live(p);attacksDone.SetValue(player,(int)attacksDone.GetValue(player)+1);follow.Invoke(p,new object[]{clacking,1f/60f});
    float clack=(float)Field(clacking,"clack");Check(clack<.1f,$"empty swing starts a clack in the ready jaw (one frame in: {clack:0.000} s of .24)");   // 4 kat hızda bir kare ≈ .067
    intervalNow.SetValue(player,0f);
    Lab("ResetRewards");Sync(p);Check(Live(p)==null&&Jaws(p).Count==0,"reward removal clears the jaw");Finish(null);
   }
  }catch(Exception e){Finish(e.InnerException??e);}
 }
 static void Finish(Exception e){RunEffects.Cued-=Cue;SessionState.SetBool(Key,false);Time.timeScale=1;Time.captureDeltaTime=0;File.WriteAllLines("Logs/GreedArtVerification.txt",new[]{e==null?"PASS "+notes.Count+" checks":"FAIL "+e}.Concat(notes));EditorApplication.Exit(e==null?0:1);}
}
