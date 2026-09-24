using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

// Runs only in the disposable fixture project. This is a lifecycle/CPU stress
// test, not a GPU or low-end PC FPS benchmark of GameScene.
[InitializeOnLoad]
public static class PerformanceStressVerification
{
    const string Key = "PerformanceStressVerification";
    static IEnumerator run;
    static int lastFrame = -1, errors, editorSearchErrors;
    static double nextStatus;
    static readonly List<string> notes = new();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static PerformanceStressVerification() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/Library/VerificationProject/"))
            throw new Exception("Run this test only in the isolated verification project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorApplication.isPaused = false;
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        // The disposable Editor can pause on its own SearchDatabase startup bug.
        // Gameplay errors are still counted and fail the test below.
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup > nextStatus)
        {
            nextStatus = EditorApplication.timeSinceStartup + 5;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PerformanceStressProgress.txt", $"Frame {Time.frameCount}; time {Time.time}; scale {Time.timeScale}; paused {EditorApplication.isPaused}; checks {notes.Count}; last: {(notes.Count > 0 ? notes[notes.Count-1] : "starting")}");
        }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (run == null) { Application.logMessageReceived += Log; run = Execute(); }
            if (run.MoveNext()) return;
            Check(errors == 0, "No runtime errors/exceptions during stress");
            notes.Add("Editor-only SearchDatabase startup exceptions (excluded): " + editorSearchErrors);
            Finish(true, null);
        }
        catch (Exception ex) { Finish(false, ex); }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) { editorSearchErrors++; return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Finish(bool pass, Exception error)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= Log;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/PerformanceStressVerification.txt",
            new[] { pass ? "PASS: lifecycle and allocation stress" : "FAIL: " + error }.Concat(notes));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(pass ? 0 : 1);
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        notes.Add(message);
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static T Data<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();
    static T Component<T>(string label) where T : Component => new GameObject(label).AddComponent<T>();
    static Action Method(object target, string name) => (Action)Delegate.CreateDelegate(typeof(Action), target, target.GetType().GetMethod(name, Private));
    static PlantHealth Initialize(GameObject go, PlantSO data, GridObject grid)
    {
        go.GetComponent<PlantBrain>()?.Initialize(data, grid);
        var health = go.GetComponent<PlantHealth>(); health.Initialize(data);
        go.GetComponent<PlantResource>().Initialize(data, null);
        go.SetActive(true); grid.SetPlantObject(go);
        return health;
    }
    static int RingMaterials() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name == "Shared Attack Ring (runtime)");
    static IEnumerator Execute()
    {
        notes.Add("Unity " + Application.unityVersion + "; " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName);
        notes.Add("Isolated Editor Play Mode; actual plant prefabs; no full GameScene GPU/FPS claim.");
        var game = Component<GameManager>("Game"); game.StartGame();
        Component<AudioListener>("Test audio listener");
        var core = Data<CoreStatsSO>();
        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            core.stats.Add(new StatEntry { statType = stat, value = StatDefaults.GetDefaultBase(stat) });
        var stats = Component<StatManager>("Stats"); Set(stats, "coreStatsSO", core);
        var resources = Component<ResourceManager>("Resources");
        var score = Component<HarvestScoreManager>("Score"); score.enabled = false;
        var progressionData = Data<ProgressionSO>(); progressionData.baseXP = 1000000000;
        var progressionObject = new GameObject("Progression"); progressionObject.SetActive(false);
        var progression = progressionObject.AddComponent<ProgressionManager>();
        Set(progression, "progressionData", progressionData); progression.enabled = false; progressionObject.SetActive(true);
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Plants" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p.GetComponent<PlantHealth>() != null && p.GetComponent<PlantBrain>() != null && p.GetComponent<PlantResource>() != null).ToArray();
        Check(prefabs.Length >= 11, "Loaded actual plant prefabs: " + prefabs.Length);
        var data = Data<PlantSO>(); data.maxHealth = 10; data.rewardAmount = 5; data.xpAmount = 2;
        data.resourceType = ResourceType.Gold; data.rarity = PlantRarity.Rare;
        var pool = PlantPool.ForScene(SceneManager.GetActiveScene());
        var grid = new GridObject(null, new GridPosition(0, 0));

        // Every prefab: reuse, exactly one award, no stale electric XP, flash cleanup.
        var vfx = Component<VFXManager>("VFX");
        Check(RingMaterials() == 1, "One owned shared attack-ring material");
        foreach (var prefab in prefabs)
        {
            int createdBefore = pool.CreatedCount;
            data.prefab = prefab;
            int gold = resources.GetResourceAmount(ResourceType.Gold), scoreBefore = score.TotalScore;
            float xp = progression.CurrentXP;
            for (int cycle = 0; cycle < 12; cycle++)
            {
                var go = pool.Rent(prefab, Vector3.zero, Quaternion.identity);
                var health = Initialize(go, data, grid);
                Check(!health.IsDead && health.CurrentHealth == health.MaxHealth && health.KillingElectricXPMultiplier == 1,
                    prefab.name + " reset life " + cycle);
                var renderer = go.GetComponentInChildren<Renderer>();
                if (renderer != null) vfx.PlayHitFlash(renderer, Color.red);
                health.TakeDamage(int.MaxValue, cycle % 2 == 0 ? DamageType.Electric : DamageType.Direct, false, 10);
                health.TakeDamage(int.MaxValue);
                PlantPool.Release(go); // Duplicate return is harmless.
                Check(!grid.HasPlantObject(), "Death clears grid before callbacks finish");
                yield return null;
                Check(!go.activeSelf, "Returned plant inactive");
                if (renderer != null)
                {
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    Check(block.GetFloat(Shader.PropertyToID("_ToonFlash")) == 0, "Flash cleared on return");
                }
            }
            Check(pool.CreatedCount == createdBefore + 1, prefab.name + " reused one instance across 12 deaths");
            Check(resources.GetResourceAmount(ResourceType.Gold) - gold == 60 && score.TotalScore - scoreBefore == 120 && progression.CurrentXP - xp == 132,
                prefab.name + " exact single rewards; electric/direct XP isolated");
        }

        // Real spawner timer: no spawn while alive, full interval after death, pause, removal.
        data.prefab = prefabs[0];
        var planterData = Data<PlanterSO>();
        planterData.spawnTable = new List<PlantSpawnEntry> { new() { plant = data, baseChance = 100 } };
        planterData.baseStats.Add(new StatEntry { statType = StatType.PlantSpawnRate, value = .5f });
        var spawner = Component<PlantSpawner>("Timer spawner"); spawner.Initialize(planterData, grid, null);
        Set(spawner, "timer", 0f);
        double until = Time.timeAsDouble + .65;
        while (Time.timeAsDouble < until) yield return null;
        Check(grid.HasPlantObject(), "Actual Update spawns after interval");
        var first = grid.GetPlantObject();
        until = Time.timeAsDouble + .65;
        while (Time.timeAsDouble < until) yield return null;
        Check(grid.GetPlantObject() == first, "Occupied cell cannot spawn again");
        first.GetComponent<PlantHealth>().TakeDamage(int.MaxValue);
        until = Time.timeAsDouble + .2;
        while (Time.timeAsDouble < until) yield return null;
        Check(!grid.HasPlantObject(), "Death resets full spawn timer");
        game.OpenShop();
        until = Time.realtimeSinceStartupAsDouble + .55;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        Check(!grid.HasPlantObject(), "No spawning while paused");
        game.StartGame();
        until = Time.timeAsDouble + .4;
        while (Time.timeAsDouble < until) yield return null;
        Check(grid.HasPlantObject(), "Spawn resumes after remaining interval");
        int removalGold = resources.GetResourceAmount(ResourceType.Gold);
        float removalXP = progression.CurrentXP; int removalScore = score.TotalScore;
        spawner.RemoveSpawnedPlant(); spawner.RemoveSpawnedPlant();
        yield return null;
        Check(!grid.HasPlantObject() && resources.GetResourceAmount(ResourceType.Gold) == removalGold &&
            progression.CurrentXP == removalXP && score.TotalScore == removalScore, "Selling/removal returns without any award");
        Object.Destroy(spawner.gameObject);

        foreach (var currency in new[] { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone })
        {
            data.resourceType = currency;
            int before = resources.GetResourceAmount(currency);
            for (int i = 0; i < 20; i++)
            {
                var go = pool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
                Initialize(go, data, grid).TakeDamage(int.MaxValue);
                yield return null;
            }
            Check(resources.GetResourceAmount(currency) - before == 100, currency + " reward data reset on pooled reuse");
        }
        data.resourceType = ResourceType.Gold;
        var round = Component<RoundManager>("Round snapshot"); round.enabled = false;
        var ownerA = Component<PlanterBrain>("Owner A");
        var ownerB = Component<PlanterBrain>("Owner B");
        var owned = pool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
        var ownedHealth = Initialize(owned, data, grid);
        ownedHealth.Initialize(data, ownerA);
        Check(ownedHealth.Owner == ownerA && ownedHealth.SpawnRound == 1, "Initial owner / round snapshot");
        PlantPool.Release(owned); yield return null;
        Check(ownedHealth.Owner == null, "Returned plant releases owner reference");
        Set(round, "<CurrentRound>k__BackingField", 130);
        owned = pool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
        ownedHealth = Initialize(owned, data, grid);
        ownedHealth.Initialize(data, ownerB);
        Check(ownedHealth.Owner == ownerB && ownedHealth.SpawnRound == 130 &&
            ownedHealth.CurrentHealth == PlantHealthCalculator.Calculate(data, 130), "Reused plant takes new owner and R130 health");
        PlantPool.Release(owned); yield return null;
        Object.Destroy(round.gameObject); Object.Destroy(ownerA.gameObject); Object.Destroy(ownerB.gameObject);
        yield return null;

        // Pool reuse must not cause a boomerang's per-leg identity to skip a new life.
        var reused = pool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
        var priorHealth = Initialize(reused, data, grid);
        var oldKey = (priorHealth, priorHealth.LifetimeVersion);
        priorHealth.TakeDamage(int.MaxValue); yield return null;
        reused = pool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
        var newHealth = Initialize(reused, data, grid);
        Check(oldKey != (newHealth, newHealth.LifetimeVersion), "Boomerang identity changes on pooled respawn");
        PlantPool.Release(reused); yield return null;
        Object.Destroy(vfx.gameObject); yield return null; yield return null;
        Check(RingMaterials() == 0, "VFX destruction releases owned ring material");

        const int Width = 121, BaselineWaves = 100, PooledWaves = 1000;
        long probeStart = GC.GetAllocatedBytesForCurrentThread();
        var probe = new byte[16384]; GC.KeepAlive(probe);
        bool allocationCounterWorks = GC.GetAllocatedBytesForCurrentThread() - probeStart >= 16384;
        var grids = Enumerable.Range(0, Width).Select(i => new GridObject(null, new GridPosition(i % 11, i / 11))).ToArray();
        var victims = new PlantHealth[Width];
        var elapsed = new double[PooledWaves];
        long baselineBytes = 0, pooledBytes = 0; double baselineMs = 0;
        int baselineGold = resources.GetResourceAmount(ResourceType.Gold);
        float baselineXP = progression.CurrentXP;
        for (int wave = 0; wave < BaselineWaves; wave++)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Width; i++)
                victims[i] = Initialize(Object.Instantiate(data.prefab), data, grids[i]);
            for (int i = 0; i < Width; i++) victims[i].TakeDamage(int.MaxValue);
            baselineMs += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            baselineBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            yield return null;
        }
        Check(resources.GetResourceAmount(ResourceType.Gold) - baselineGold == Width * BaselineWaves * 5 &&
            progression.CurrentXP - baselineXP == Width * BaselineWaves * 2, "Instantiate/Destroy baseline exact rewards");
        int goldStart = resources.GetResourceAmount(ResourceType.Gold), scoreStart = score.TotalScore;
        float xpStart = progression.CurrentXP;
        int stableCreated = 0, stableMaterials = 0;
        for (int wave = 0; wave < PooledWaves; wave++)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Width; i++)
                victims[i] = Initialize(pool.Rent(data.prefab, Vector3.zero, Quaternion.identity), data, grids[i]);
            for (int i = 0; i < Width; i++) { victims[i].TakeDamage(int.MaxValue); victims[i].TakeDamage(int.MaxValue); }
            elapsed[wave] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            if (wave > 0) pooledBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            yield return null;
            if (wave == 0) { stableCreated = pool.CreatedCount; stableMaterials = Resources.FindObjectsOfTypeAll<Material>().Length; }
            if (pool.ActiveCount != 0 || pool.PendingCount != 0 || pool.CreatedCount != stableCreated)
                throw new Exception("Pool failed to plateau at wave " + wave);
        }
        Check(resources.GetResourceAmount(ResourceType.Gold) - goldStart == Width * PooledWaves * 5 &&
            progression.CurrentXP - xpStart == Width * PooledWaves * 2 && score.TotalScore - scoreStart == Width * PooledWaves * 10,
            "121,000 pooled harvests: exact Gold/XP/score despite duplicate damage calls");
        Check(pool.CreatedCount == stableCreated && pool.ActiveCount == 0 && pool.PendingCount == 0,
            "1000 waves plateau: created=" + pool.CreatedCount + ", retained=" + pool.InactiveCount + ", active=0, pending=0");
        Check(Resources.FindObjectsOfTypeAll<Material>().Length == stableMaterials, "Material count unchanged through 1000 pooled waves");
        var samples = elapsed.Skip(1).OrderBy(x => x).ToArray();
        notes.Add($"121 plants/wave; baseline {BaselineWaves} waves, pooled {PooledWaves} waves (first excluded from warmed metrics).");
        notes.Add($"Synchronous lifecycle submission only, deferred engine/pool work excluded: baseline mean {baselineMs / BaselineWaves:F3} ms; pooled mean {samples.Average():F3} ms, P50 {samples[samples.Length/2]:F3}, P95 {samples[(int)(samples.Length*.95)]:F3}, P99 {samples[(int)(samples.Length*.99)]:F3} ms.");
        notes.Add(allocationCounterWorks
            ? $"Managed bytes/submission wave (calling thread): baseline {baselineBytes/BaselineWaves}, pooled {pooledBytes/(PooledWaves-1)}; excludes deferred work."
            : "Managed allocation counter failed 16 KB calibration on this Unity runtime; allocation values are unavailable, not zero.");

        // Ring stress: 20,000 requests, a single material and fixed 4 renderers.
        vfx = Component<VFXManager>("Ring stress");
        for (int frame = 0; frame < 1000; frame++)
        {
            for (int i = 0; i < 20; i++) vfx.PlayAttackRing(Vector3.zero, 3f, i % 2 == 0);
            yield return null;
        }
        Check(RingMaterials() == 1 && vfx.GetComponentsInChildren<LineRenderer>(true).Length == 4,
            "20,000 ring requests: exactly 1 material / 4 renderers retained");
        Object.Destroy(vfx.gameObject); yield return null; yield return null;
        Check(RingMaterials() == 0, "Ring stress cleanup: 0 owned materials remain");
        var isolated = SceneManager.CreateScene("Pool unload test");
        var disposablePool = PlantPool.ForScene(isolated);
        var disposablePlant = disposablePool.Rent(data.prefab, Vector3.zero, Quaternion.identity);
        Initialize(disposablePlant, data, new GridObject(null, new GridPosition(0, 0)));
        var unload = SceneManager.UnloadSceneAsync(isolated);
        while (!unload.isDone) yield return null;
        Check(disposablePool == null && disposablePlant == null, "Scene unload destroys active pool and plants");
    }
}
