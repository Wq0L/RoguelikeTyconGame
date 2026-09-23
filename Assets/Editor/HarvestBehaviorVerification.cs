using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HarvestBehaviorVerification
{
    const string Key = "HarvestBehaviorTest";
    static int stage;
    static double next;
    static PlanterBrain source;
    static GridObject sourceGrid;
    static readonly List<PlantHealth> plants = new();
    static readonly Dictionary<PlantHealth,int> beforeFlight = new();
    static Vector3 pausedPosition;
    static float pausedProgress;
    static BoomerangScythe moving;
    static int poolSize;
    static readonly List<string> notes = new();
    static HarvestBehaviorVerification() { EditorApplication.update += Tick; }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Harvest behavior verification: " + message);
        SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1); notes.Add(message);
    }
    public static void RunBatch()
    {
        try
        {
            SessionState.SetInt(Key + "Checks", 0);
            HarvestBehaviorAuthoring.Author();
            StaticChecks();
            // Production enters via MenuScene, whose persistent GameManager survives
            // the scene transition. Supply that same bootstrap only in this unsaved test scene.
            if (Object.FindFirstObjectByType<GameManager>() == null)
                new GameObject("Test-only menu bootstrap").AddComponent<GameManager>();
            SessionState.SetString(Key, "running"); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
    [MenuItem("Tools/Harvest Behaviors/Verify In Play Mode")]
    public static void RunInteractive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode before verification.");
        for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new Exception("Save your scene edits before verification.");
        RunBatch();
    }
    static void StaticChecks()
    {
        var fp = new List<GridPosition>(); var targets = new List<GridPosition>(); var origins = new List<GridPosition>();
        foreach (var size in new[] { new Vector2Int(1,1), new Vector2Int(2,3), new Vector2Int(3,2) })
        {
            fp.Clear(); for (int x=0;x<size.x;x++)for(int z=0;z<size.y;z++)fp.Add(new GridPosition(4+x,4+z));
            HarvestBehaviorGeometry.ElectricCells(fp, targets, origins);
            Require(targets.Count == 8, "eight diagonal targets for " + size);
            for (int i=0;i<targets.Count;i++)
            {
                int dx=Math.Abs(targets[i].x-origins[i].x),dz=Math.Abs(targets[i].z-origins[i].z);
                Require(dx==dz && dx>=1 && dx<=2,"diagonal range outside footprint");
                Require(!fp.Any(p=>p.x==targets[i].x && p.z==targets[i].z),"source footprint excluded");
            }
        }
        Require(HarvestBehaviorGeometry.SegmentDistanceSquared(Vector3.right*2,Vector3.zero,Vector3.right*5)<.001f,"swept hit between frames");
        Require(!DamageType.Boomerang.CanTriggerBehaviors() && !DamageType.Electric.CanTriggerBehaviors(),"secondary kills cannot chain");
        var nodes=Object.FindFirstObjectByType<SkillTreeManager>(FindObjectsInactive.Include).AllNodes;
        Require(nodes.Count==140,"scene roster includes 140 skill nodes");
        var behaviorManager = new SerializedObject(Object.FindFirstObjectByType<HarvestBehaviorManager>());
        Require(behaviorManager.FindProperty("boomerangPrefab").objectReferenceValue != null &&
            behaviorManager.FindProperty("electricPrefab").objectReferenceValue != null,"scene pool prefabs persist after authoring");
        var player = new SerializedObject(Object.FindFirstObjectByType<PlayerController>());
        Require(player.FindProperty("cursorVisualPrefab").objectReferenceValue != null &&
            player.FindProperty("radiusOverlayMaterial").objectReferenceValue != null,"scene cursor references persist");
        foreach(var type in new[]{UnlockType.TileBehavior_Boomerang,UnlockType.TileBehavior_Electric})
        {
            var node=nodes.Single(n=>n.unlockType==type);Require(node.prerequisites.Count==1 && node.tiers[0].cost>0,"unlock prerequisites and price");
            var cards=AssetDatabase.FindAssets("t:TileModifierSO").Select(g=>AssetDatabase.LoadAssetAtPath<TileModifierSO>(AssetDatabase.GUIDToAssetPath(g))).Where(t=>t.requiredUnlock==type).ToArray();
            Require(cards.Length==4 && cards.Select(c=>c.rarity).Distinct().Count()==4,"four rarities per behavior");
            foreach(var c in cards)Require(c.modifierRanges.Single().maxValue<=1,"chance authored as normalized fraction");
        }
        Require(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Behaviors/Harvest Cursor Overlay.mat").GetFloat("_ZTest")==8,"circle depth overlay");
        foreach(var mat in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Behaviors"}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m=>m.name.EndsWith("Cursor")))
            Require(mat.GetFloat("_ToonZTest")==8 && mat.GetFloat("_ToonZWrite")==0,"cursor scythe uses toon overlay");
    }
    static void Tick()
    {
        string state=SessionState.GetString(Key,"");if(state.Length==0)return;
        if(state=="done" || state=="failed")
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            SessionState.SetString(Key,"");
            if (Application.isBatchMode) EditorApplication.Exit(state=="done"?0:1);
            else
            {
                EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
                Debug.Log("Harvest behavior verification " + state + "; see Logs/HarvestBehaviorVerification.txt");
            }
            return;
        }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Play-mode verification timed out");
            if(EditorApplication.timeSinceStartup<next)return;
            if(stage==0){stage++;next=EditorApplication.timeSinceStartup+1;return;}
            var manager=HarvestBehaviorManager.Instance;
            if(stage==1)
            {
                Setup();
                Require(manager!=null,"runtime behavior manager initialized");
                var pool=Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
                var roster=new SerializedObject(pool).FindProperty("allModifiers");
                foreach(var unlock in new[]{UnlockType.TileBehavior_Boomerang,UnlockType.TileBehavior_Electric})
                {
                    var node=SkillTreeManager.Instance.AllNodes.Single(n=>n.unlockType==unlock);
                    var cards=new List<TileModifierSO>();for(int i=0;i<roster.arraySize;i++){var c=roster.GetArrayElementAtIndex(i).objectReferenceValue as TileModifierSO;if(c!=null&&c.requiredUnlock==unlock)cards.Add(c);}
                    Require(cards.Count==4 && cards.All(c=>!c.IsAvailableInCardPool),"new cards locked in actual scene");
                    Buy(node);Require(cards.All(c=>c.IsAvailableInCardPool),"skill purchase unlocks all rarities");
                }
                // Buying prerequisites can relock tiles through grid recalculation.
                foreach(var c in Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None))c.Unlock();
                Require(manager.TryElectric(source,7),"electric burst spawns");
                var targets=new List<GridPosition>();var origins=new List<GridPosition>();
                HarvestBehaviorGeometry.ElectricCells(source.OccupiedGrids.Select(g=>g.GetGroundCellCached().GetGridPosition()).ToArray(),targets,origins);
                foreach(var g in source.OccupiedGrids)Require(g.GetPlantObject().GetComponent<PlantHealth>().CurrentHealth==100,"electric excludes source planter");
                foreach(var p in targets)Require(GridManager.Instance.GetGridSystem().GetGridObject(p).GetPlantObject().GetComponent<PlantHealth>().CurrentHealth==93,"electric hits diagonal target once");
                for(int i=0;i<20;i++)manager.TryElectric(source,0);
                Require(manager.ActiveElectricBursts==8,"electric pool cap");manager.ClearAll();
                beforeFlight.Clear();foreach(var p in plants)if(p!=null)beforeFlight[p]=p.CurrentHealth;
                for(int i=0;i<20;i++)manager.TryBoomerang(source,sourceGrid,10);
                Require(manager.ActiveBoomerangs==6,"boomerang pool cap");
                poolSize=manager.GetComponentsInChildren<BoomerangScythe>(true).Length;
                manager.ClearAll();manager.TryBoomerang(source,sourceGrid,10);
                moving=manager.GetComponentsInChildren<BoomerangScythe>().Single();
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var from=(Vector3)typeof(BoomerangScythe).GetField("from",flags).GetValue(moving);
                var to=(Vector3)typeof(BoomerangScythe).GetField("to",flags).GetValue(moving);
                var delta=to-from;
                Require(Mathf.Abs(delta.x)<.001f != (Mathf.Abs(delta.z)<.001f),"flight uses exactly one grid axis");
                float cellSize=Vector3.Distance(GridManager.Instance.GetGridSystem().GetWorldPosition(0,0),GridManager.Instance.GetGridSystem().GetWorldPosition(1,0));
                float range=delta.magnitude/cellSize;
                Require(Mathf.Abs(range-2)<.001f || Mathf.Abs(range-3)<.001f,"flight range is two or three cells");
                stage++;next=EditorApplication.timeSinceStartup+.25;return;
            }
            if(stage==2)
            {
                Require(moving!=null && moving.transform.position!=sourceGrid.GetGroundCellCached().transform.position,"boomerang moves along straight flight");
                Require(plants.Any(p=>p!=null && p.CurrentHealth<beforeFlight[p]),"moving boomerang deals swept damage");
                pausedPosition=moving.transform.position;pausedProgress=(float)Time.time;
                GameManager.Instance.OpenShop(); stage++; next=EditorApplication.timeSinceStartup+.3;return;
            }
            if(stage==3)
            {
                Require((moving.transform.position-pausedPosition).sqrMagnitude<.00001f,"projectile pauses in shop");
                Require(Math.Abs(Time.time-pausedProgress)<.001f,"scaled lifetime pauses");
                GameManager.Instance.StartGame();stage++;next=EditorApplication.timeSinceStartup+4;return;
            }
            if(stage==4)
            {
                Require(manager.ActiveBoomerangs==0 && manager.ActiveElectricBursts==0,"effects return naturally to pools");
                manager.TryBoomerang(source,sourceGrid,10);manager.TryElectric(source,7);
                Require(manager.GetComponentsInChildren<BoomerangScythe>(true).Length==poolSize,"reuse does not grow prewarmed pool");
                Capture();
                typeof(RoundManager).GetMethod("EndRound",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(RoundManager.Instance,null);
                Require(manager.ActiveBoomerangs==0 && manager.ActiveElectricBursts==0,"round end returns every active effect");
                RoundManager.Instance.StartRound();manager.TryBoomerang(source,sourceGrid,10);source.RemoveSelf();
                stage++;next=EditorApplication.timeSinceStartup+.2;return;
            }
            if(stage==5)
            {
                Require(manager.ActiveBoomerangs==0,"selling source cancels projectile");
                Finish(true,null);
            }
        }
        catch(Exception ex){Debug.LogException(ex);Finish(false,ex.ToString());}
    }
    static void Buy(SkillNodeSO node)
    {
        foreach(var pre in node.prerequisites){Buy(pre.node);while(SkillTreeManager.Instance.GetCurrentLevel(pre.node)<pre.level)Require(SkillTreeManager.Instance.TryUpgrade(pre.node),"purchase prerequisite tier");}
        if(SkillTreeManager.Instance.GetCurrentLevel(node)==0)Require(SkillTreeManager.Instance.TryUpgrade(node),"purchase unlock node");
    }
    static void Setup()
    {
        Object.FindFirstObjectByType<PlayerController>().enabled=false;
        foreach(var c in Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None))c.Unlock();
        foreach(ResourceType type in Enum.GetValues(typeof(ResourceType)))ResourceManager.Instance.AddResource(type,100000);
        RoundManager.Instance.StartRound();
        var grid=GridManager.Instance.GetGridSystem();var fp=new List<GridObject>();
        for(int x=4;x<6;x++)for(int z=4;z<7;z++)fp.Add(grid.GetGridObject(new GridPosition(x,z)));
        var data=AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x3.asset");
        var go=Object.Instantiate(data.prefab,grid.GetWorldPosition(4,4),Quaternion.identity);source=go.GetComponent<PlanterBrain>();
        var so=new SerializedObject(source);so.FindProperty("spawnPoints").ClearArray();so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var g in fp){g.SetPlanterObject(go);g.SetPlanterBrain(source);}source.Initialize(data,fp);sourceGrid=fp[0];
        foreach(var s in source.GetComponentsInChildren<PlantSpawner>())s.enabled=false;
        var plantData=Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlantSO>("Assets/ScriptableObjects/Plants/Carrot.asset"));plantData.maxHealth=200;plantData.rarity=PlantRarity.Common;
        for(int x=0;x<GridManager.Instance.GetWidth();x++)for(int z=0;z<GridManager.Instance.GetHeight();z++)
        {
            var g=grid.GetGridObject(new GridPosition(x,z));if(g.GetGroundCellCached()==null)continue;
            var p=Object.Instantiate(plantData.prefab,g.GetGroundCellCached().transform.position+Vector3.up*.3f,Quaternion.identity);
            var h=p.GetComponent<PlantHealth>();h.Initialize(plantData,fp.Contains(g)?source:null);g.SetPlantObject(p);plants.Add(h);
        }
        UnityEngine.Random.InitState(78123);
    }
    static void Capture()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var camera=Camera.main;
        var cursor=Object.FindFirstObjectByType<HarvestCursorVisual>(FindObjectsInactive.Include);
        if(cursor!=null){cursor.gameObject.SetActive(true);cursor.Show(source.transform.position+Vector3.right,1.5f);}
        var player=Object.FindFirstObjectByType<PlayerController>();
        var ring=(LineRenderer)new SerializedObject(player).FindProperty("radiusIndicator").objectReferenceValue;
        ring.enabled=true;
        typeof(PlayerController).GetMethod("UpdateRadiusVisual",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(player,new object[]{source.transform.position+Vector3.right});
        var rt=new RenderTexture(1280,900,24);var prior=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1280,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,900),0,0);tex.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/HarvestBehaviorPreview.png",tex.EncodeToPNG());
        camera.targetTexture=prior;RenderTexture.active=active;Object.Destroy(tex);rt.Release();Object.Destroy(rt);
    }
    static void Finish(bool passed,string error)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/HarvestBehaviorVerification.txt",(passed?"PASS":"FAIL")+": "+SessionState.GetInt(Key+"Checks",0)+" checks\n"+string.Join("\n",notes)+"\n"+error);
        SessionState.SetString(Key,passed?"done":"failed");EditorApplication.ExitPlaymode();
    }
}
