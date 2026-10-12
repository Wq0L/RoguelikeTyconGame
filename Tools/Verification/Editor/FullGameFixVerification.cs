using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated GameScene integration checks. Not a human balance/play approval.
[InitializeOnLoad]
public static class FullGameFixVerification
{
    const string Key = "FullGameFixVerification";
    static readonly List<string> notes = new();
    static readonly Queue<Action> steps = new();
    static double next, deadline;
    static int errors;
    static PlanterBrain source;
    static GridObject start;
    static int boomHits, tornadoHits;
    static RunProfileSO Profile => AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_AlphaDengeV1.asset");
    static FullGameFixVerification() => EditorApplication.update += Tick;

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>("Assets/Resources/RunProfileSelection.asset");
        selection.active = Profile; EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Test manager").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (next == 0)
            {
                next = EditorApplication.timeSinceStartup + 2;
                deadline = next + 120;
                Application.logMessageReceived += Log;
                foreach (Action step in new Action[] { Setup, Cards, Rewards, Geometry, Overflow, OverflowResult, Cleanup, RewardPanel, RewardPanelCheck, Result, Screenshot, FinishChecks }) steps.Enqueue(step);
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (EditorApplication.timeSinceStartup < next) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { Finish(null); return; }
            Action action = steps.Dequeue();
            next = EditorApplication.timeSinceStartup + .2;
            action();
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/FullGameFixVerification.txt", notes);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        notes.Add("ok: " + message);
    }
    static void Log(string text, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (stack.Contains("UnityEditor.Search")) return;
        errors++; notes.Add("ERROR " + text);
    }
    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false); Application.logMessageReceived -= Log;
        PlantHealth.AnyDamaged -= Damage;
        Time.timeScale = 1f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/FullGameFixVerification.txt", new[] { e == null ? "PASS " + notes.Count(n => n.StartsWith("ok:")) : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }
    static void Field(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Property(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    static void Setup()
    {
        Check(RoundManager.Instance.Profile == Profile && !DemoSceneSettings.IsDemo, "Full-game Alpha profile in GameScene");
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(11);
        RoundManager.Instance.StartNextRound();
        Property(RoundManager.Instance, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        var planter = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        foreach (var cell in RewardOfferLab.OpenCells())
        {
            var brain = KirilmaErisimMeasurement.PlacePlanter(planter, cell, null, 0f);
            foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true))
            {
                spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); spawner.enabled = false;
            }
            foreach (var grid in brain.OccupiedGrids)
            {
                var health = grid.GetPlantObject().GetComponent<PlantHealth>();
                Field(health, "maxHealth", 10000000); Field(health, "currentHealth", 10000000);
            }
            if (cell.GetGridPosition().x == 5 && cell.GetGridPosition().z == 5) { source = brain; start = brain.OccupiedGrids[0]; }
        }
        Check(source != null, "Dense 11x11 field with live center planter");
    }
    static void Cards()
    {
        var stats = StatManager.Instance;
        float[] wanted = { .0002f, .0004f, .0007f, .001f };
        foreach (TileRarity rarity in Enum.GetValues(typeof(TileRarity)))
            Check(BasePowerCardRules.Amount(rarity, Profile.balance) == wanted[(int)rarity], "Base card amount " + rarity);
        var old = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_XPV1.asset");
        Check(BasePowerCardRules.Amount(TileRarity.Legendary, old.balance) == .05f && old.balance.movingBehaviorLimit == 0, "Legacy profile keeps 5% and old pool policy");
        foreach (var tuple in new[] { (StatType.HarvestDamage, StatTarget.Player), (StatType.XPGainMultiplier, StatTarget.Planter),
            (StatType.GoldGainMultiplier, StatTarget.Planter), (StatType.IronGainMultiplier, StatTarget.Planter), (StatType.StoneGainMultiplier, StatTarget.Planter) })
        {
            stats.ClearGlobalModifiers();
            var mod = new StatModifier { statType = tuple.Item1, target = tuple.Item2, operation = ModifierOperation.MorePercent, value = .001f };
            float before = tuple.Item2 == StatTarget.Planter ? source.GetFinalStat(tuple.Item1) : stats.GetFinalStat(tuple.Item1, tuple.Item2);
            var offer = TileCardOffer.BaseStat("test", TileRarity.Legendary, new List<StatModifier> { mod }, BasePowerCardRules.IsSummed(tuple.Item1, Profile.balance));
            for (int i = 0; i < 1000; i++) BasePowerCardRules.Apply(offer, stats);
            float after = tuple.Item2 == StatTarget.Planter ? source.GetFinalStat(tuple.Item1) : stats.GetFinalStat(tuple.Item1, tuple.Item2);
            Check(Math.Abs(after / before - 2f) < .001f && stats.GlobalModifiers.Count == 1, tuple.Item1 + ": 1000 cards give x2 on actual target, one modifier");
            Check(CardUI.SummedText(offer).Contains("0.1%"), "Card displays the actual 0.1% unit: " + tuple.Item1);
        }
        stats.ClearGlobalModifiers();
        foreach (var tuple in new[] { (StatType.AttackSpeed, StatTarget.Player), (StatType.PlantSpawnRate, StatTarget.Planter) })
        {
            float before = tuple.Item2 == StatTarget.Planter ? source.GetFinalStat(tuple.Item1) : stats.GetFinalStat(tuple.Item1, tuple.Item2);
            var offer = TileCardOffer.BaseStat("interval", TileRarity.Legendary, new List<StatModifier> { new StatModifier { statType = tuple.Item1, target = tuple.Item2, operation = ModifierOperation.MorePercent, value = -.001f } });
            BasePowerCardRules.Apply(offer, stats);
            float after = tuple.Item2 == StatTarget.Planter ? source.GetFinalStat(tuple.Item1) : stats.GetFinalStat(tuple.Item1, tuple.Item2);
            Check(after > 0f && after < before && Math.Abs(after / before - .999f) < .0001f, tuple.Item1 + ": reduction has correct sign and units");
        }
        stats.ClearGlobalModifiers();
        Check(stats.SummedGroupTotal(StatType.XPGainMultiplier, StatTarget.Planter) == 0, "New run clears base-card accumulation");
    }
    static void Rewards()
    {
        var boss = BossRewardManager.Instance;
        var rewards = RewardOfferLab.All(Profile.bossRewards).ToList();
        var rhythm = rewards.First(r => r.id == "hasat_ritmi"); var chain = rewards.First(r => r.id == "zincir_hasat");
        Check(!string.IsNullOrEmpty(rhythm.exclusiveGroup) && rhythm.exclusiveGroup == chain.exclusiveGroup, "Rhythm/chain share the explicit choice group");
        // Test exclusion symmetrically without bypassing the production BlockOf check.
        var taken = (List<BossRewardSO>)typeof(BossRewardManager).GetField("taken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boss);
        taken.Add(rhythm);
        Check(boss.BlockOf(chain, false) == BossRewardBlock.Exclusive && !boss.IsEligible(chain) && !boss.EditorGrant(chain, out _), "Rhythm blocks offering and accepting chain");
        taken.Clear(); taken.Add(chain);
        Check(boss.BlockOf(rhythm, false) == BossRewardBlock.Exclusive && !boss.IsEligible(rhythm) && !boss.EditorGrant(rhythm, out _), "Chain blocks offering and accepting rhythm");
        taken.Clear();
        var alternatives = new List<BossRewardSO>(); Profile.bossRewards.CollectExclusive(rhythm, alternatives);
        Check(alternatives.Contains(chain), "Card explanation resolves the excluded reward from pool data");
        alternatives.Clear(); Profile.bossRewards.CollectExclusive(rhythm, alternatives, false);
        Check(alternatives.Count == 0, "Demo keeps the previous nonexclusive rhythm/chain policy");
    }
    static void Geometry()
    {
        var grid = GridManager.Instance.GetGridSystem();
        var rng = new System.Random(106);
        int full = 0, bounded = 0;
        for (int i = 0; i < 10000; i++)
        {
            var a = new Vector3((float)rng.NextDouble() * 14 - 2, .8f, (float)rng.NextDouble() * 14 - 2);
            var b = a + new Vector3((float)rng.NextDouble() * 2 - 1, 0, (float)rng.NextDouble() * 2 - 1);
            float radius = Vector3.Distance(grid.GetWorldPosition(0, 0), grid.GetWorldPosition(1, 0)) * .42f;
            grid.GetSegmentBounds(a, b, radius, out var min, out var max);
            for (int x = 0; x < 11; x++) for (int z = 0; z < 11; z++)
            {
                full++;
                bool inside = x >= min.x && x <= max.x && z >= min.z && z <= max.z;
                if (inside) bounded++;
                bool hit = HarvestBehaviorGeometry.SegmentDistanceSquared(grid.GetWorldPosition(x, z), a, b) <= radius * radius;
                if (hit && !inside) throw new Exception("Bounds omitted a hit");
            }
        }
        Check(bounded < full / 10, "10000 sweeps omit no geometric hits; candidate cells " + bounded + " vs " + full);
    }
    static void Damage(PlantHealth plant, int amount, DamageType type)
    {
        if (type == DamageType.Boomerang) boomHits++;
        if (type == DamageType.Tornado) tornadoHits++;
    }
    static void Overflow()
    {
        var b = HarvestBehaviorManager.Instance; var t = TornadoManager.Instance;
        Check(b.BoomerangLimit == 12 && t.ActiveLimit == 12, "Each moving behavior starts at exactly 12");
        PlantHealth.AnyDamaged += Damage;
        for (int i = 0; i < 12; i++)
        {
            Check(b.TryBoomerang(source, start, 100), "Boomerang accepted " + i);
            Check(t.TrySpawn(start, 100), "Tornado accepted " + i);
        }
        Check(!b.TryBoomerang(source, start, 100) && !t.TrySpawn(start, 100) && b.ActiveBoomerangs == 12 && t.ActiveCount == 12, "13th normal trigger is refused, no hidden overflow");
        var reward = ScriptableObject.CreateInstance<BossRewardSO>();
        reward.id = "test_capacity"; reward.displayName = "Kapasite testi"; reward.movingBehaviorLimitBonus = 4; reward.maxStacks = 1;
        var gains = new List<string>(); var costs = new List<string>(); BossRewardText.TradeLines(reward, 3, gains, costs);
        Check(gains.Any(s => s.Contains("12 → 16")), "Capacity card preview uses live limit");
        Check(BossRewardManager.Instance.IsEligible(reward) && BossRewardManager.Instance.EditorGrant(reward, out _) && b.BoomerangLimit == 16 && t.ActiveLimit == 16, "Only taking a capacity reward raises both limits to 16");
        Check(!BossRewardManager.Instance.EditorGrant(reward, out _), "One-stack capacity reward cannot be taken twice");
        var invalid = ScriptableObject.CreateInstance<BossRewardSO>(); invalid.movingBehaviorLimitBonus = int.MaxValue;
        bool tooLarge = !BossRewardManager.Instance.EditorGrant(invalid, out _);
        invalid.movingBehaviorLimitBonus = -1;
        Check(tooLarge && !BossRewardManager.Instance.EditorGrant(invalid, out _) && b.BoomerangLimit == 16 && t.ActiveLimit == 16,
            "Negative and overflowing capacity cards are rejected without partial effect");
        Object.Destroy(invalid);
        for (int i = 0; i < 4; i++) Check(b.TryBoomerang(source, start, 100) && t.TrySpawn(start, 100), "Reward permits one additional active pair " + i);
        Check(!b.TryBoomerang(source, start, 100) && !t.TrySpawn(start, 100) && b.ActiveBoomerangs == 16 && t.ActiveCount == 16, "Reward remains bounded: 17th trigger refused");
        PerformanceTrendCapture.Begin();
        next = EditorApplication.timeSinceStartup + 6;
    }
    static void OverflowResult()
    {
        PerformanceTrendCapture.Stop();
        Check(boomHits > 0 && tornadoHits > 0, "Accepted behaviors reach real PlantHealth: boomerang hits=" + boomHits + ", tornado hits=" + tornadoHits);
        Check(HarvestBehaviorManager.Instance.ActiveBoomerangs == 0 && TornadoManager.Instance.ActiveCount == 0, "Both behaviors return to pool after movement completes");
        for (int i = 0; i < 16; i++) { HarvestBehaviorManager.Instance.TryBoomerang(source, start, 100); TornadoManager.Instance.TrySpawn(start, 100); }
    }
    static void Cleanup()
    {
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        Check(HarvestBehaviorManager.Instance.ActiveBoomerangs == 0 && TornadoManager.Instance.ActiveCount == 0, "Round cleanup cancels moving behaviors and releases roots");
        BossRewardManager.Instance.ClearAll();
        Check(HarvestBehaviorManager.Instance.BoomerangLimit == 12 && TornadoManager.Instance.ActiveLimit == 12, "New run resets the capacity reward to 12");
        PlantHealth.AnyDamaged -= Damage;
    }
    static RunCompleteUI result;
    static BossRewardPanelUI rewardPanel;
    static void RewardPanel()
    {
        var boss = BossRewardManager.Instance;
        var offer = (List<BossRewardSO>)typeof(BossRewardManager).GetField("offer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boss);
        offer.Clear();
        offer.AddRange(RewardOfferLab.All(Profile.bossRewards).Where(r => r.id == "hasat_ritmi" || r.id == "zincir_hasat"));
        Field(boss, "offerRound", 23); Field(boss, "offerPrepared", true); Property(boss, "IsPending", true);
        rewardPanel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        rewardPanel.gameObject.SetActive(true); Call(rewardPanel, "Fill");
        next = EditorApplication.timeSinceStartup + 1;
    }
    static void RewardPanelCheck()
    {
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < 2; i++)
        {
            var stack = rewardPanel.CardTexts(i).First(t => t.name == "Stack");
            stack.ForceMeshUpdate();
            string other = BossRewardManager.Instance.Offer[1 - i].displayName;
            Check(stack.text.Contains("Bunu alırsan " + other + " bir daha sunulmaz") && !stack.isTextOverflowing,
                "Actual boss card displays and fits exclusion: " + other);
        }
        Capture("FullGameRewardChoice");
        rewardPanel.gameObject.SetActive(false); BossRewardManager.Instance.ClearAll();
    }
    static void Result()
    {
        result = Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include);
        result.gameObject.SetActive(true);
        var label = (TextMeshProUGUI)typeof(RunCompleteUI).GetField("scoreText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result);
        label.text = "<size=125%><color=#9BEA7C>RUN TAMAMLANDI</color></size>\n50 round · son kota 1.270.504 / 68.000\nHarvest Score: 4.867.755\n\n<b>BOSS ÖDÜLLERİ</b>\n" +
            string.Join("\n", Enumerable.Range(1, 30).Select(i => "• " + i + ". Ödül — kazanç ve bedel açıklaması"));
        Canvas.ForceUpdateCanvases();
        Check(result.GetComponent<RunResultLayout>() != null && result.GetComponentsInChildren<Button>().Length == 2, "GameScene result gets readable layout and retains both buttons");
        var scroll = result.GetComponentInChildren<ScrollRect>();
        Check(scroll.content.rect.height > scroll.viewport.rect.height && !label.enableAutoSizing, "Long reward list scrolls instead of shrinking text");
        result.gameObject.SetActive(false); result.gameObject.SetActive(true);
        Check(result.GetComponentsInChildren<ScrollRect>().Length == 1, "Reopening result does not duplicate layout");
        label.text = "<size=125%><color=#9BEA7C>RUN TAMAMLANDI</color></size>\n50 round · son kota 1.270.504 / 68.000\nHarvest Score: 4.867.755\n\n<b>BOSS ÖDÜLLERİ</b>\n" +
            string.Join("\n", Enumerable.Range(1, 30).Select(i => "• " + i + ". Ödül — kazanç ve bedel açıklaması"));
        next = EditorApplication.timeSinceStartup + 1;
    }
    static void Screenshot() => Capture("FullGameResult");
    static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var target = new RenderTexture(1600, 900, 24);
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f; }
        target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.Destroy(image); Object.Destroy(target);
    }
    static void FinishChecks() => Check(errors == 0, "No unexpected runtime errors");
}
