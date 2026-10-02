using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.5 — Run50_DengeV1 İŞLEV testi (denge ölçümü BalanceRunMeasurement'ta; bu test "dengeli" demez).
// Doğrulanan: profil bazlı veri ayrımı (temel stat, can eğrisi, XP tablosu, skill tree seti, nadirlik XP'si, saksı fiyatı),
// ağaç arayüzünün sete bağlanması, eski profillerin ortak veriyle aynen çalışması, ödül sistemindeki yeni etki yolları
// (sunum penceresi, nadirliğe bağlı doğrudan hasar, davranış hasarı) ve imleç halkasının gerçek hasat erişimini göstermesi.
// Sayılar asset'lerden okunur: denge değerleri değişince test yeniden yazılmaz.
[InitializeOnLoad]
public static class DengeV1Verification
{
    const string Key = "DengeV1Verification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string ProfilePath = Profiles + "Run50_DengeV1.asset";
    const string SharedTree = "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree";
    const string V1Tree = "Assets/ScriptableObjects/Skill Tree Upgrades/DengeV1";
    const string Rewards = "Assets/ScriptableObjects/BossRewards/DengeV1/";
    const string MenuPath = "Tools/Run Profili/Run50 Denge V1 · 50 round (ilk denge adayı)";
    static readonly List<string> notes = new();
    static int step; static double nextAt; static int errors;

    static DengeV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: DengeV1 seçilir ve boss seed'i sabitlenir.
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        profile.bossSeed = 3535;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static int shownStep = -1; static double stepSince;
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (nextAt == 0) { nextAt = EditorApplication.timeSinceStartup + 2; Application.logMessageReceived += CountErrors; }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/DengeV1Verification.txt", new[] { $"RUNNING step {step}" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240) throw new Exception($"step {step} stuck");
            if (RoundManager.Instance != null && RoundManager.Instance.IsRoundActive) SetP(RoundManager.Instance, "RemainingTime", 1000f);
            double wait = Run(step++);
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void CountErrors(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;   // editörün arama dizini (oyunla ilgisiz)
        if (type == LogType.Exception || type == LogType.Error) { errors++; notes.Add("   LOG " + type + ": " + message); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountErrors;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/DengeV1Verification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static SkillTreeManager Tree => SkillTreeManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static Vector3 Pos(int dx, int dz) => Cell(dx, dz).GetGroundCellCached().transform.position;
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO V1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
    static PlanterSO PlanterAsset(string n) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {n}.asset");
    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static PlantSO Plant(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    static BossRewardSO Reward(string id) => V1.bossRewards.rewards.First(r => r.id == id);
    static List<T> All<T>(string folder) where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
        .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null).ToList();
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    static PlantHealth PlantAt(int dx, int dz) { var p = Cell(dx, dz).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static void Attack(Vector3 position) => Call(Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include), "AttackInRadius", position);

    static PlanterSO Only(PlantSO plant)
    {
        var so = Object.Instantiate(PlanterAsset("1x1"));
        so.spawnTable = new List<PlantSpawnEntry> { new PlantSpawnEntry { plant = plant, baseChance = 100 } };
        return so;
    }

    static PlanterBrain Place(PlanterSO so, int dx, int dz, TileModifierSO tile = null, float value = 1f)
    {
        var cell = Cell(dx, dz);
        if (tile != null)
            cell.GetGroundCellCached().ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value }).ToList());
        var planter = Object.Instantiate(so.prefab);
        planter.transform.position = cell.GetGroundCellCached().transform.position;
        cell.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(so, new List<GridObject> { cell });
        cell.SetPlanterBrain(brain);
        return brain;
    }

    static void Respawn()
    {
        foreach (var s in Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null))
        { s.RemoveSpawnedPlant(); s.enabled = true; Call(s, "TrySpawnPlant"); }
    }

    static void ClearField()
    {
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Cell(x, z).GetGroundCellCached().ApplyModifier(null);
    }

    static void StartRound(int round)
    {
        Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
    }

    // Round'u oyunun kendi yoluyla bitirir; bekleyen kart ekranlarını ilk kartla geçer.
    static void EndRound()
    {
        Call(RM, "EndRound");
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        int guard = 0;
        while (State == GameStates.CardSelection)
        {
            if (guard++ > 60) throw new Exception("card selection did not finish");
            Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]);
        }
        if (State != GameStates.RoundEnd) throw new Exception("round did not end: " + State);
    }

    static bool Grant(BossRewardSO reward)
    {
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetF(Boss, "offerPrepared", true); // Explicit fixture: this test injects an already prepared offer.
        SetP(Boss, "IsPending", true);
        bool taken = Boss.Choose(reward);
        if (!taken) { SetP(Boss, "IsPending", false); offer.Clear(); }
        return taken;
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Assets();
            case 1: return V1Runtime();
            case 2: return V1TreeUI();
            case 3: return V1Purchases();
            case 4: return RarityXp();
            case 5: return RewardWindows();
            case 6: return RewardEffects();
            case 7: return Ring();
            case 8: return AreaSteps();
            case 9: return FogStep();
            case 10: return LoadProfile("Run50_Referans");
            case 11: return ReferenceRuntime();
            case 12: return LoadProfile("Run50_BossPrototip");
            case 13: return PrototypeRuntime();
            case 14: return FinalCheck();
            default: return -1;
        }
    }

    // ---------------- 0) asset'ler ----------------
    static double Assets()
    {
        var p = V1;
        Require(p != null && p.runLength == 50 && p.segmentRounds == 5 && !p.debugBudget && p.startingIron == 0 && p.startingStone == 0 && p.startingGold > 0,
            $"Run50_DengeV1: 50 rounds, 5-round segments, normal economy (start {p.startingGold} / 0 / 0)");
        Require(p.fixedRoundDuration >= 30f && p.fixedRoundDuration <= 60f && p.specializationAfterSegment == 0 && p.events.Count == 0,
            $"Round duration fixed by the profile ({p.fixedRoundDuration:0} s); no legacy event list, no specialization");
        Require(p.bossPool != null && p.bossPool.entries.Count >= 3 && p.bossRewards != null && Enumerable.Range(1, 9).All(s => p.HasBoss(s) && p.BossTargetFor(s) > 0) && !p.HasBoss(10),
            "Boss on R5…R45 with a boss target each; none on R50");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(a => a.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);

        var b = p.balance;
        Require(b != null && b.coreStats != null && b.plantHealth != null && b.progression != null && b.skillTree != null, "Balance set: own core stats, plant health, XP table and skill tree");
        foreach (string name in new[] { "Prototip10", "Prototip10_DebugButce", "Uzmanlasma20", "Deney22_20", "UzunRun130", "Run50_Referans", "Run50_BossPrototip" })
            if (Profile(name).balance != null) throw new Exception(name + " has a balance set");
        Require(true, "Older profiles (incl. Run50_Referans and Run50_BossPrototip) carry no balance set: shared assets");

        var shared = AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
        var sharedHealth = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        Require(b.coreStats != shared && b.plantHealth != sharedHealth && shared.GetBaseStat(StatType.HarvestDamage) == 1f && Near(sharedHealth.anchors[0].commonHealth, 5f),
            "Shared core stats (damage 1) and shared health curve (round 1: 5) are separate assets and unchanged");
        var changed = shared.stats.Where(s => !Near(s.value, b.coreStats.GetBaseStat(s.statType))).Select(s => $"{s.statType} {s.value} → {b.coreStats.GetBaseStat(s.statType)}").ToList();
        Require(b.coreStats.stats.Count == shared.stats.Count && changed.Count > 0, "Core stat differences: " + string.Join(", ", changed));

        // ağaç seti
        var sharedNodes = All<SkillNodeSO>(SharedTree); var v1Nodes = All<SkillNodeSO>(V1Tree);
        var set = b.skillTree; var nodes = set.Nodes();
        Require(sharedNodes.Count == 140 && set.slots.Count == 140 && set.slots.Select(s => s.source).Distinct().Count() == 140 && set.slots.All(s => sharedNodes.Contains(s.source)),
            "Tree set: one slot for each of the 140 shared nodes (the scene's interface slots)");
        Require(nodes.Count == v1Nodes.Count && nodes.All(v1Nodes.Contains) && !nodes.Any(sharedNodes.Contains) && nodes.Select(n => n.name).Distinct().Count() == nodes.Count,
            $"Tree set: {nodes.Count} own node assets (none shared with the old tree); {140 - nodes.Count} slots closed");
        Require(nodes.All(n => n.explicitPrerequisites && n.prerequisites.All(r => r.node != null && nodes.Contains(r.node) && r.level >= 1 && r.level <= r.node.tiers.Count)),
            "Every prerequisite points inside the set and to an existing tier");
        var reachable = new HashSet<SkillNodeSO>(nodes.Where(n => n.prerequisites.Count == 0)); int roots = reachable.Count;
        for (bool grew = true; grew;) { grew = false; foreach (var n in nodes) if (!reachable.Contains(n) && n.prerequisites.All(r => reachable.Contains(r.node))) { reachable.Add(n); grew = true; } }
        Require(roots >= 3 && reachable.Count == nodes.Count, $"{roots} root nodes; every node reachable (no cycle, no orphan)");
        Require(nodes.All(n => n.tiers.Count >= 1 && n.tiers.Count <= 3 && n.tiers.All(t => t.cost > 0)), "1–3 tiers per node (interface dots), every tier has a price");
        Require(!nodes.Any(SkillTreeManager.IsDurationOnly) && !nodes.Any(n => n.tiers.Any(t => t.effects.Any(e => e.statType == StatType.RoundDuration))),
            "No round-duration node in the set (duration is the profile's)");
        var closed = set.slots.Where(s => s.node == null).Select(s => s.source.name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Require(closed.Any(n => n.StartsWith("Aşırı Güç")) && closed.Any(n => n.StartsWith("Yıldırım Kesim")) && closed.Any(n => n.StartsWith("Seri Üretim")) && closed.Any(n => n.StartsWith("Plazma Kesim")),
            "Closed slots include the peak nodes moved to boss rewards (Aşırı Güç, Yıldırım Kesim, Seri Üretim, Plazma Kesim …)");
        Note("kapalı yuvalar: " + string.Join(", ", closed));
        var oldStart = sharedNodes.First(n => n.name == "Keskin Başlangıç - 1"); var oldPeak = sharedNodes.First(n => n.name == "Aşırı Güç I");
        Require(oldStart.tiers.Select(t => t.cost).SequenceEqual(new[] { 3, 5, 8 }) && Near(oldStart.tiers[2].effects[0].value, 8f) && Near(oldPeak.tiers[0].effects[0].value, 7f),
            "Shared tree untouched: Keskin Başlangıç - 1 still 3 / 5 / 8 G for +8, Aşırı Güç I still ×8");

        // can eğrisi
        var grass = Plant("Grass"); var hp = b.plantHealth;
        var curve = Enumerable.Range(1, 50).Select(r => hp.Calculate(grass, r)).ToList();
        double worst = Enumerable.Range(1, 49).Max(k => curve[k] / (double)curve[k - 1]);
        Require(Enumerable.Range(1, 49).All(k => curve[k] >= curve[k - 1]) && curve[49] > curve[0] && worst <= 1.12,
            $"Common health R1…R50: {curve[0]} … {curve[49]}, monotone, largest round-to-round step ×{worst:0.###} (no wall)");
        var m = hp.rarityMultipliers; float k1 = m[1] - m[0];
        Require(m.Length == 5 && Near(m[0], 1f) && k1 > 0f && Enumerable.Range(1, 4).All(n => Near(m[n] - m[n - 1], k1)), $"Rarity health is linear: 1 + {k1:0.##} × step ({string.Join(" / ", m.Select(v => v.ToString("0.##")))})");
        var xp = b.progression.xpRequirements;
        Require(b.progression.useAuthoredRequirements && xp.Count >= 60 && Enumerable.Range(1, xp.Count - 1).All(k => xp[k] >= xp[k - 1]), $"XP table: {xp.Count} levels, non-decreasing ({xp[0]} … {xp[xp.Count - 1]})");

        // ödüller ve bosslar
        var rewards = p.bossRewards.rewards;
        Require(rewards.All(r => r != null) && rewards.Select(r => r.id).Distinct().Count() == rewards.Count && rewards.All(r => AssetDatabase.GetAssetPath(r).StartsWith(Rewards)),
            $"Reward pool: {rewards.Count} rewards with unique ids, own assets ({p.bossRewards.choices} choices)");
        Require(rewards.Any(r => r.minRound > 5) && rewards.Any(r => r.maxRound > 0) && rewards.Any(r => !Near(r.behaviorDamageMultiplier, 1f)) && rewards.Any(r => !Near(r.rareDirectMultiplier, 1f)),
            "Pool uses the new data: late-only rewards, an early-only reward, a behavior-damage reward and a rarity-direct reward");
        var proto = Profile("Run50_BossPrototip");
        Require(proto.bossRewards.rewards.All(r => r.minRound == 0 && r.maxRound == 0 && Near(r.rareDirectMultiplier, 1f) && Near(r.behaviorDamageMultiplier, 1f) && r.OfferedAt(5) && r.OfferedAt(45)) &&
                !p.bossPool.entries.Any(e => proto.bossPool.entries.Any(o => o.boss == e.boss)) && !rewards.Any(proto.bossRewards.rewards.Contains),
            "Boss prototype keeps its own bosses and rewards (new reward fields at neutral defaults)");
        return .2;
    }

    // ---------------- 1) DengeV1 sahnede ----------------
    static double V1Runtime()
    {
        var p = V1; var b = p.balance;
        Require(State == GameStates.RunSetup && RM.Profile == p && RunBalanceSO.Active == b, "GameScene runs Run50_DengeV1 with its balance set");
        Require(RM.FixedRoundDuration && Near(RM.EffectiveRoundDuration, p.fixedRoundDuration) && Near(RM.TempoMultiplier, 1f), $"Round lasts {RM.EffectiveRoundDuration:0} s, no tempo");
        Require(Near(Stats.GetBaseStat(StatType.HarvestDamage), b.coreStats.GetBaseStat(StatType.HarvestDamage)) && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), b.coreStats.GetBaseStat(StatType.HarvestDamage)) &&
                !Near(Stats.GetBaseStat(StatType.HarvestDamage), 1f), $"Base damage from the set: {Stats.GetBaseStat(StatType.HarvestDamage)} (shared: 1)");
        var grape = Plant("Grape"); var grass = Plant("Grass"); var sharedHealth = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        Require(PlantHealthCalculator.Calculate(grass, 1) == b.plantHealth.Calculate(grass, 1) && PlantHealthCalculator.Calculate(grape, 30) == b.plantHealth.Calculate(grape, 30) &&
                PlantHealthCalculator.Calculate(grass, 1) != sharedHealth.Calculate(grass, 1),
            $"Plant health from the set: Grass R1 {PlantHealthCalculator.Calculate(grass, 1)} (shared {sharedHealth.Calculate(grass, 1)}), Grape R30 {PlantHealthCalculator.Calculate(grape, 30)} (shared {sharedHealth.Calculate(grape, 30)})");
        Require(Near(ProgressionManager.Instance.XPToNextLevel, b.progression.GetXPForLevel(1)), $"XP for level 2 from the set: {ProgressionManager.Instance.XPToNextLevel}");
        var nodes = b.skillTree.Nodes();
        Require(Tree.AllNodes.Count == nodes.Count && Tree.AllNodes.All(nodes.Contains), $"SkillTreeManager uses the set's {nodes.Count} nodes");
        Require(ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == p.startingGold, "Starting gold from the profile");
        var planter11 = PlanterAsset("1x1");
        foreach (var entry in b.planterBaseStats)
            if (!Near(planter11.GetBaseStat(entry.statType), entry.value) || !Near(PlanterAsset("2x3").GetBaseStat(entry.statType), entry.value)) throw new Exception("planter base stat not from the set: " + entry.statType);
        Require(true, "Planter base stats from the set: " + (b.planterBaseStats.Count == 0 ? "none" : string.Join(", ", b.planterBaseStats.Select(e => $"{e.statType} {e.value}"))));
        var loadout = StartLoadoutManager.Instance;
        Require(loadout != null && loadout.Farmer.id == "bahcivan" && loadout.Scythe.id == "standart" && MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "Neutral start; batch save in memory only");
        return .2;
    }

    // ---------------- 2) ağaç arayüzü ----------------
    static double V1TreeUI()
    {
        var ui = Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
        // Paneli aç: kapalı atalarını geçici olarak aç (Awake burada çalışır), sonra eski hâline döndür.
        var opened = new List<GameObject>();
        for (Transform t = ui.transform; t != null; t = t.parent) if (!t.gameObject.activeSelf) { opened.Add(t.gameObject); }
        opened.Reverse(); foreach (var go in opened) go.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var slots = F<List<SkillNodeUI>>(ui, "nodeUIs").Where(n => n != null).ToList();
        var nodes = V1.balance.skillTree.Nodes();
        var bound = slots.Where(s => s.Node != null).ToList();
        Require(slots.Count == 140 && bound.Count == nodes.Count && bound.Select(s => s.Node).Distinct().Count() == nodes.Count && bound.All(s => nodes.Contains(s.Node)),
            $"Tree interface: {bound.Count} of 140 slots show the set's nodes, each node once");
        Require(slots.Where(s => s.Node == null).All(s => !s.gameObject.activeSelf), "Closed slots are hidden");
        Require(bound.All(s => Near(((RectTransform)s.transform).anchoredPosition.x, s.Node.gridPosition.x * 150f) && Near(((RectTransform)s.transform).anchoredPosition.y, s.Node.gridPosition.y * 150f)),
            "Each slot sits at its node's own position");
        var visible = bound.Where(s => s.gameObject.activeSelf).Select(s => s.Node).ToList();
        var roots = nodes.Where(n => n.prerequisites.Count == 0).ToList();
        Require(roots.All(visible.Contains) && visible.All(n => Tree.IsNodeVisible(n)), $"Visible at run start: the {roots.Count} roots ({string.Join(", ", roots.Select(r => r.name))})");
        var connections = F<System.Collections.IList>(ui, "connections");
        int expected = nodes.Sum(n => Math.Max(1, n.prerequisites.Count));
        Require(connections.Count == expected, $"Connection lines follow the set's prerequisites ({connections.Count})");
        foreach (var go in opened) go.SetActive(false);
        return .2;
    }

    // ---------------- 3) satın alma ----------------
    static double V1Purchases()
    {
        var bank = ResourceManager.Instance;
        bank.AddResource(ResourceType.Gold, 5000);
        var damage = Tree.AllNodes.First(n => n.prerequisites.Count == 0 && n.tiers[0].effects.Any(e => e.statType == StatType.HarvestDamage));
        var speed = Tree.AllNodes.First(n => n.prerequisites.Count == 0 && n.tiers[0].effects.Any(e => e.statType == StatType.AttackSpeed));
        float before = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player); int gold = bank.GetResourceAmount(ResourceType.Gold);
        Require(Tree.TryUpgrade(damage) && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), before + damage.tiers[0].effects[0].value) &&
                bank.GetResourceAmount(ResourceType.Gold) == gold - damage.tiers[0].cost, $"{damage.name} tier 1: damage {before} → {Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player):0.##}, price {damage.tiers[0].cost} G paid");
        while (!Tree.IsMaxLevel(damage)) if (!Tree.TryUpgrade(damage)) throw new Exception("tier not bought");
        Require(Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), before + damage.tiers[damage.tiers.Count - 1].effects[0].value), "Later tiers replace the earlier one (not added twice)");
        var next = Tree.AllNodes.First(n => n.prerequisites.Any(r => r.node == damage));
        Require(Tree.MeetsPrerequisites(next) && Tree.IsNodeVisible(next), $"Completing {damage.name} opens {next.name}");
        float interval = Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player);
        Require(Tree.TryUpgrade(speed) && Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player) < interval, $"{speed.name}: attack interval {interval} → {Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player):0.###}");
        // saksı fiyatı: sette yoksa asset fiyatı; sette varsa setin fiyatı (mağaza, yerleştirme ve iade aynı değeri okur)
        var planter = PlanterAsset("1x1"); var balance = V1.balance; int assetCost = planter.cost;
        bool hadOverride = balance.planterPrices.Any(x => x.planter == planter);
        if (!hadOverride)
        {
            Require(planter.Price == assetCost && planter.PriceType == planter.costType, $"Planter price without an entry in the set: asset price ({assetCost} {planter.costType})");
            balance.planterPrices.Add(new RunBalanceSO.PlanterPrice { planter = planter, costType = ResourceType.Iron, cost = assetCost + 7 });
            Require(planter.Price == assetCost + 7 && planter.PriceType == ResourceType.Iron && planter.cost == assetCost, "Planter price with an entry in the set: the set's price; the asset itself is unchanged");
            balance.planterPrices.RemoveAll(x => x.planter == planter);
        }
        else Require(planter.Price == balance.planterPrices.First(x => x.planter == planter).cost, "Planter price comes from the set");
        Tree.ResetTree();
        Require(Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), before) && Near(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), interval), "ResetTree removes the set's node effects");
        return .2;
    }

    // ---------------- 4) nadirliğe göre XP ----------------
    static double RarityXp()
    {
        var balance = V1.balance;
        StartRound(2);
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 100000f, ModifierOperation.Set));
        Place(Only(Plant("Grass")), -1, 0); Place(Only(Plant("Grape")), 1, 0);
        Respawn();
        var progress = ProgressionManager.Instance;
        double xp0 = progress.TotalXPEarned; Attack(Pos(-1, 0));   // yalnız (-1,0)
        double common = progress.TotalXPEarned - xp0;
        xp0 = progress.TotalXPEarned; Attack(Pos(1, 0));          // yalnız (1,0)
        double legendary = progress.TotalXPEarned - xp0;
        Require(Near((float)common, Plant("Grass").xpAmount * balance.XpMultiplier(PlantRarity.Common)) && Near((float)legendary, Plant("Grape").xpAmount * balance.XpMultiplier(PlantRarity.Legendary)),
            $"Harvest XP follows the set's rarity multipliers: Common {common}, Legendary {legendary}");
        ClearTestMods();
        return .8;   // round birkaç yüz kare sürsün: tarla doluluğu örneklenir
    }

    // ---------------- 5) ödül sunum penceresi ----------------
    static double RewardWindows()
    {
        var pool = V1.bossRewards; var late = pool.rewards.Where(r => r.minRound > 5).ToList(); var early = pool.rewards.Where(r => r.maxRound > 0).ToList();
        var seenAt = new Dictionary<int, HashSet<BossRewardSO>>();
        foreach (int round in new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45 })
        {
            seenAt[round] = new HashSet<BossRewardSO>();
            for (int seed = 1; seed <= 60; seed++)
            {
                SetF(Boss, "offerRound", round);
                SetP(SegmentEventDirector.Instance, "RunSeed", seed);
                Call(Boss, "BuildOffer", pool, round / 5);
                if (Boss.Offer.Count != Boss.Offer.Distinct().Count() || Boss.Offer.Count > pool.choices) throw new Exception("offer has duplicates or too many options");
                foreach (var r in Boss.Offer) seenAt[round].Add(r);
            }
        }
        F<List<BossRewardSO>>(Boss, "offer").Clear();
        SetP(SegmentEventDirector.Instance, "RunSeed", V1.bossSeed);
        Require(late.All(r => seenAt.Where(kv => kv.Key < r.minRound).All(kv => !kv.Value.Contains(r)) && seenAt.Where(kv => kv.Key >= r.minRound).Any(kv => kv.Value.Contains(r))),
            "Late rewards are never offered before their round and do appear from it: " + string.Join(", ", late.Select(r => $"{r.displayName} R{r.minRound}+")));
        Require(early.All(r => seenAt.Where(kv => kv.Key > r.maxRound).All(kv => !kv.Value.Contains(r)) && seenAt.Where(kv => kv.Key <= r.maxRound).Any(kv => kv.Value.Contains(r))),
            "Early-only rewards stop after their round: " + string.Join(", ", early.Select(r => $"{r.displayName} ≤R{r.maxRound}")));
        Require(seenAt[5].Count >= pool.choices && seenAt[45].Count >= pool.choices, $"Enough different rewards at the first ({seenAt[5].Count}) and the last boss ({seenAt[45].Count})");
        return .1;
    }

    // ---------------- 6) yeni etki yolları ----------------
    static double RewardEffects()
    {
        // Tarla doluluğu: round sırasında örneklenir, round bitince okunur. "Tarla boşalıyor" koşullu ödül buna bakar.
        EndRound();
        float occupancy = Boss.LastRoundOccupancy;
        Require(occupancy >= 0f && occupancy < 1f, $"Field occupancy of the finished round was sampled: {occupancy:0.##} (two planters, both harvested once)");
        var field = V1.bossRewards.rewards.FirstOrDefault(r => r.condition == BossRewardCondition.FieldRunsLow);
        if (field != null)
        {
            SetP(Boss, "LastRoundOccupancy", field.conditionThreshold + .01f);
            bool full = Boss.IsEligible(field);
            SetP(Boss, "LastRoundOccupancy", field.conditionThreshold - .01f);
            Require(!full && Boss.IsEligible(field), $"{field.displayName}: offered only while the field runs low (average occupancy below {field.conditionThreshold:0.##})");
        }
        Boss.ClearAll(); ClearField();
        Require(Near(Boss.LastRoundOccupancy, 1f), "ClearAll resets the occupancy reading");
        var behavior = V1.bossRewards.rewards.First(r => !Near(r.behaviorDamageMultiplier, 1f));
        var rare = V1.bossRewards.rewards.First(r => !Near(r.rareDirectMultiplier, 1f));
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 40f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        StartRound(49);   // geç round: bitki canı hasardan büyük (ölçülebilir), boss round'u değil
        Require(behavior.condition == BossRewardCondition.AnyBehaviorPlanter && !Boss.IsEligible(behavior), $"{behavior.displayName}: not offered while no planter has a behavior");
        var a = Place(Only(Plant("Grass")), -1, -1, Tile("Electric", "Legendary"), 1f);
        Place(Only(Plant("Carrot")), 0, 0);    // Rare
        Place(Only(Plant("Grass")), 1, -1);    // Common
        Respawn();
        Require(Boss.IsEligible(behavior) && Grant(behavior) && Near(BossRewardManager.BehaviorDamageMultiplier, behavior.behaviorDamageMultiplier) &&
                Near(BossRewardManager.DirectDamageMultiplier, 1f) && Boss.OwnedModifiers.Count == 0,
            $"{behavior.displayName} ×1: behavior multiplier {BossRewardManager.BehaviorDamageMultiplier:0.##}, direct multiplier still 1, no stat modifier");
        var plantA = PlantAt(-1, -1); var plantB = PlantAt(0, 0);
        SetF(plantA, "currentHealth", 1);
        Attack(Pos(-1, -1));   // yalnız (-1,-1): diğer saksılar erişimin dışında
        int electric = plantB.MaxHealth - plantB.CurrentHealth; int expectedElectric = Mathf.RoundToInt(40f * behavior.behaviorDamageMultiplier);
        Require(plantA.IsDead && electric == expectedElectric && a.GetBehaviorDamage(40, DamageType.Electric) == expectedElectric, $"Electric from A hits B for {electric} = 40 × {behavior.behaviorDamageMultiplier:0.##}");
        // doğrudan vuruş bu ödülden etkilenmez
        var common = PlantAt(1, -1); int commonBefore = common.CurrentHealth;
        UnityEngine.Random.InitState(7); float v = UnityEngine.Random.Range(.85f, 1.15f); UnityEngine.Random.InitState(7);
        Attack(Pos(1, -1));     // yalnız (1,-1)
        Require(commonBefore - common.CurrentHealth == Mathf.Max(1, Mathf.RoundToInt(40f * v)), $"Direct hit unchanged by {behavior.displayName}: {commonBefore - common.CurrentHealth}");

        // nadirliğe bağlı doğrudan hasar
        Require(Grant(rare) && Near(BossRewardManager.RareDirectMultiplier(rare.rareDirectFrom), rare.rareDirectMultiplier) && Near(BossRewardManager.RareDirectMultiplier(PlantRarity.Common), 1f) &&
                Near(BossRewardManager.RareDirectMultiplier(PlantRarity.Legendary), rare.rareDirectMultiplier),
            $"{rare.displayName} ×1: {rare.rareDirectFrom} and above ×{rare.rareDirectMultiplier:0.##}, Common ×1");
        Respawn();
        var rarePlant = PlantAt(0, 0); common = PlantAt(1, -1);
        int rareHp = rarePlant.CurrentHealth, commonHp = common.CurrentHealth;
        UnityEngine.Random.InitState(11); float v1 = UnityEngine.Random.Range(.85f, 1.15f); UnityEngine.Random.InitState(11);
        Attack(Pos(0, 0));       // yalnız (0,0): diğer saksılar çaprazda, erişimin dışında
        UnityEngine.Random.InitState(11);
        Attack(Pos(1, -1));
        Require(rareHp - rarePlant.CurrentHealth == Mathf.Max(1, Mathf.RoundToInt(40f * v1 * rare.rareDirectMultiplier)) && commonHp - common.CurrentHealth == Mathf.Max(1, Mathf.RoundToInt(40f * v1)),
            $"Same roll: Rare takes {rareHp - rarePlant.CurrentHealth} (×{rare.rareDirectMultiplier:0.##}), Common takes {commonHp - common.CurrentHealth} — one multiplication, one rounding");
        Require(a.GetBehaviorDamage(40, DamageType.Electric) == expectedElectric, $"{rare.displayName} does not change behavior damage");
        Require(Grant(rare) == (rare.maxStacks >= 2) && (rare.maxStacks < 2 || Near(BossRewardManager.RareDirectMultiplier(PlantRarity.Epic), rare.rareDirectMultiplier * rare.rareDirectMultiplier)),
            "Second copy multiplies (up to the stack limit)");
        Require(BossRewardText.Value(behavior, 1) == "×" + BossRewardText.Number(behavior.behaviorDamageMultiplier) && BossRewardText.Value(rare, 1) == "×" + BossRewardText.Number(rare.rareDirectMultiplier),
            $"Card values: {behavior.effectLabel} {BossRewardText.Value(behavior, 1)} · {rare.effectLabel} {BossRewardText.Value(rare, 1)}");

        // stat ödülleri: değer veriden, tek sefer; temizlikte hepsi kalkar
        Boss.ClearAll();
        Require(Near(BossRewardManager.BehaviorDamageMultiplier, 1f) && Near(BossRewardManager.RareDirectMultiplier(PlantRarity.Legendary), 1f) && Near(BossRewardManager.DirectDamageMultiplier, 1f) && Boss.Taken.Count == 0,
            "ClearAll resets behavior, rarity-direct and direct multipliers");
        ClearTestMods();
        int baseCount = Stats.GlobalModifiers.Count;
        foreach (var r in V1.bossRewards.rewards.Where(r => r.modifiersPerStack.Count > 0 && r.condition == BossRewardCondition.Always))
        {
            var mod = r.modifiersPerStack[0];
            float beforeStat = mod.target == StatTarget.Planter ? a.GetFinalStat(mod.statType) : Stats.GetFinalStat(mod.statType, mod.target);
            if (!Grant(r)) throw new Exception("not granted: " + r.id);
            float afterStat = mod.target == StatTarget.Planter ? a.GetFinalStat(mod.statType) : Stats.GetFinalStat(mod.statType, mod.target);
            float expected = mod.operation == ModifierOperation.MorePercent ? beforeStat * (1f + mod.value) : beforeStat + mod.value;
            if (!Near(afterStat, StatCalculator.ClampStat(mod.statType, expected), 2e-3f)) throw new Exception($"{r.id}: {mod.statType} {beforeStat} → {afterStat}, expected {expected}");
            Note($"{r.displayName}: {mod.statType} {beforeStat:0.###} → {afterStat:0.###}");
        }
        Require(true, "Each stat reward changes its stat by exactly its data value");
        Boss.ClearAll();
        Require(Stats.GlobalModifiers.Count == baseCount, "ClearAll removes every reward modifier");
        return .2;
    }

    // ---------------- 7) halka = gerçek erişim ----------------
    static double Ring()
    {
        ClearField();
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, 1f, ModifierOperation.Set)); // exact side-contact boundary, independent of balance base
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 1f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        float cell = GridManager.Instance.GetCellSize();
        float radius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
        float reach = Grid.HarvestReach(radius);
        Require(Near(reach, radius), $"Contact targeting: visible circle uses radius {radius}, no added half cell");
        Place(Only(Plant("Grape")), 0, 0); Place(Only(Plant("Grape")), 1, 0); Place(Only(Plant("Grape")), 1, 1);
        Respawn();
        var center = PlantAt(0, 0); var side = PlantAt(1, 0); var diagonal = PlantAt(1, 1);
        Attack(Pos(0, 0));
        Require(center.CurrentHealth < center.MaxHealth && side.CurrentHealth < side.MaxHealth && diagonal.CurrentHealth == diagonal.MaxHealth,
            $"Aim at a cell centre: the neighbour {cell} away (on the reach circle) is hit, the diagonal {cell * 1.414f:0.##} away is not");
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var line = F<LineRenderer>(player, "radiusIndicator");
        if (line != null)
        {
            bool wasEnabled = player.enabled; var grid = F<GridSystem>(player, "gridSystem");
            if (grid == null) SetF(player, "gridSystem", Grid);
            Call(player, "UpdateRadiusVisual", Pos(0, 0));
            var points = new Vector3[line.positionCount]; line.GetPositions(points);
            Vector3 c = Pos(0, 0);
            float min = points.Min(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(c.x, c.z))), max = points.Max(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(c.x, c.z)));
            Require(Near(min, reach, .01f) && Near(max, reach, .01f), $"Cursor ring is drawn at the reach ({reach}), not at the bare radius ({radius}): the neighbour's centre lies on the ring");
            player.enabled = wasEnabled;
        }
        else Note("radiusIndicator sahnede bağlı değil; halka çizimi kontrol edilemedi");
        // Eşik: yarıçap bir tık küçülünce komşu vurulmaz (hedefleme kuralı değişmedi).
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, -.01f, ModifierOperation.MorePercent));
        Respawn();
        center = PlantAt(0, 0); side = PlantAt(1, 0);
        Attack(Pos(0, 0));
        Require(center.CurrentHealth < center.MaxHealth && side.CurrentHealth == side.MaxHealth, "Radius 1 % smaller: the neighbour is just outside and is not hit (targeting rule unchanged)");
        ClearTestMods();
        return .2;
    }

    // ---------------- 8) alan basamakları: hesap = gerçek vuruş; her alan yatırımı bir basamak geçirir ----------------
    static int RealBest(float radius)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 1f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
        int best = 0;
        Vector3 c = Pos(0, 0);
        foreach (Vector3 aim in new[] { c, (c + Pos(1, 0)) * .5f, (c + Pos(1, 1)) * .5f })
        {
            Respawn();
            var plants = Object.FindObjectsByType<PlantHealth>(FindObjectsSortMode.None).Where(h => !h.IsDead).ToList();
            Attack(aim);
            best = Math.Max(best, plants.Count(h => h.IsDead || h.CurrentHealth < h.MaxHealth));
        }
        ClearTestMods();
        return best;
    }

    static double AreaSteps()
    {
        ClearField();
        GridUnlockManager.Instance.UnlockNextTier(7);
        var grape = Only(Plant("Grape"));
        for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) Place(grape, x, z);
        float cell = GridManager.Instance.GetCellSize();
        float baseRadius = V1.balance.coreStats.GetBaseStat(StatType.AreaRadius);
        // ağaçtaki alan aileleri (AddPercent toplanır) ve alan ödülü (MorePercent)
        var families = Tree.AllNodes.Where(n => n.tiers[0].effects.Any(e => e.statType == StatType.AreaRadius))
            .GroupBy(n => n.name.Contains(" - ") ? n.name.Substring(0, n.name.LastIndexOf(" - ", StringComparison.Ordinal)) : n.name)
            .Select(g => (name: g.Key, start: g.Min(n => n.targetRounds.x), add: g.Sum(n => n.tiers[n.tiers.Count - 1].effects.First(e => e.statType == StatType.AreaRadius).value)))
            .OrderBy(f => f.start).ToList();
        var areaReward = V1.bossRewards.rewards.FirstOrDefault(r => r.modifiersPerStack.Any(m => m.statType == StatType.AreaRadius));
        var stages = new List<(string name, float radius)> { ("başlangıç", baseRadius) };
        float add = 0f;
        foreach (var f in families) { add += f.add; stages.Add(("+ " + f.name, baseRadius * (1f + add))); }
        if (areaReward != null)
            for (int k = 1; k <= areaReward.maxStacks; k++)
                stages.Add(($"+ {areaReward.displayName} ×{k}", baseRadius * (1f + add) * Mathf.Pow(1f + areaReward.modifiersPerStack.First(m => m.statType == StatType.AreaRadius).value, k)));
        var scythe = StartCatalogSO.Active.Scythe("dar_kesim");
        float narrow = 1f; foreach (var m in scythe.modifiers) if (m.statType == StatType.AreaRadius && m.operation == ModifierOperation.MorePercent) narrow *= 1f + m.value;
        var standard = new List<int>(); var narrowCells = new List<int>();
        foreach (var (name, radius) in stages)
        {
            int formula = HarvestArea.BestCells(radius, cell), real = RealBest(radius);
            int formulaNarrow = HarvestArea.BestCells(radius * narrow, cell), realNarrow = RealBest(radius * narrow);
            if (formula != real || formulaNarrow != realNarrow) throw new Exception($"{name}: formula {formula}/{formulaNarrow} but real hits {real}/{realNarrow}");
            standard.Add(real); narrowCells.Add(realNarrow);
            Note($"alan · {name}: Standart yarıçap {radius:0.###} → {real} bitki · Dar Kesim {radius * narrow:0.###} → {realNarrow} bitki");
        }
        Require(true, "Best-aim target count on a full field: formula equals the real hit count at every stage (Standart and Dar Kesim)");
        Require(families.Count >= 2 && Enumerable.Range(1, families.Count).All(k => standard[k] > standard[k - 1]),
            "Standart: every area family in the tree adds targets (" + string.Join(" → ", standard.Take(families.Count + 1)) + ")");
        Require(areaReward == null || standard[families.Count + 1] > standard[families.Count], $"Standart: the first {areaReward?.displayName} adds targets ({string.Join(" → ", standard.Skip(families.Count))})");
        Require(narrowCells[narrowCells.Count - 1] > narrowCells[0] && Enumerable.Range(1, narrowCells.Count - 1).Count(k => narrowCells[k] > narrowCells[k - 1]) >= 2,
            "Dar Kesim: area investment also adds targets, in fewer steps (" + string.Join(" → ", narrowCells) + ")");
        ClearField();
        return .2;
    }

    // ---------------- 9) Sis: bir basamak ----------------
    static double FogStep()
    {
        var fog = V1.bossPool.entries.Select(e => e.boss).OfType<FogSO>().First();
        var oldFog = Profile("Run50_BossPrototip").bossPool.entries.Select(e => e.boss).OfType<FogSO>().First();
        Require(fog.stepDown && !oldFog.stepDown && fog != oldFog, "Sis (DengeV1) uses the one-step rule; the prototype's Sis keeps the fixed multiplier");
        float cell = GridManager.Instance.GetCellSize();
        Require(!fog.IsEligible(new SegmentEventContext { PlayerRadius = fog.minPlayerRadius - .01f }) && fog.IsEligible(new SegmentEventContext { PlayerRadius = fog.minPlayerRadius }) &&
                HarvestArea.BestCells(fog.minPlayerRadius, cell) > HarvestArea.BestCells(fog.minPlayerRadius - .02f, cell),
            $"Sis is only picked from radius {fog.minPlayerRadius} (where the best aim reaches {HarvestArea.BestCells(fog.minPlayerRadius, cell)} cells)");
        int modifiers = Stats.GlobalModifiers.Count;
        foreach (float radius in new[] { 1.3f, 1.6f, 1.85f, 2.22f, 2.66f })
        {
            TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
            var runtime = fog.CreateRuntime(SegmentEventTiming.BossRound(1, 5, 1));
            int before = HarvestArea.BestCells(radius, cell);
            string preview = runtime.RuleSummary;
            runtime.Activate();
            float during = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player); int cells = HarvestArea.BestCells(during, cell);
            string active = runtime.RuleSummary;
            runtime.Finish();
            float after = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            if (!(cells < before && during < radius && Near(after, radius) && preview.Contains($"{before} → {cells}") && active.Contains($"{before} → {cells}")))
                throw new Exception($"Sis at radius {radius}: {before} → {cells} cells, radius {during}, after {after}, text '{preview}' / '{active}'");
            Note($"Sis · yarıçap {radius:0.##}: {before} → {cells} bitki (yarıçap {during:0.###}) · \"{active}\"");
            ClearTestMods();
        }
        Require(Stats.GlobalModifiers.Count == modifiers, "Sis lowers the best-aim target count by exactly one step at every tested radius and restores the radius afterwards");
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, 1.6f, ModifierOperation.Set));
        var old = oldFog.CreateRuntime(SegmentEventTiming.BossRound(1, 5, 1));
        old.Activate();
        Require(Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.6f * oldFog.radiusMultiplier), "Prototype Sis: still radius × " + oldFog.radiusMultiplier);
        old.Finish();
        ClearTestMods();
        return .1;
    }

    // ---------------- 10–13) eski profiller ----------------
    static double LoadProfile(string name)
    {
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = Profile(name);
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double ReferenceRuntime()
    {
        var reference = Profile("Run50_Referans");
        Require(State == GameStates.RunSetup && RM.Profile == reference && RunBalanceSO.Active == null, "GameScene runs Run50_Referans without a balance set");
        var shared = AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
        var sharedHealth = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling"); var grass = Plant("Grass"); var grape = Plant("Grape");
        Require(Near(Stats.GetBaseStat(StatType.HarvestDamage), 1f) && shared.stats.All(s => Near(Stats.GetBaseStat(s.statType), s.value)), "Old profile: shared core stats (damage 1)");
        Require(PlantHealthCalculator.Calculate(grass, 1) == sharedHealth.Calculate(grass, 1) && PlantHealthCalculator.Calculate(grape, 50) == sharedHealth.Calculate(grape, 50) && PlantHealthCalculator.Calculate(grass, 1) == 5,
            $"Old profile: shared health curve (Grass R1 {PlantHealthCalculator.Calculate(grass, 1)}, Grape R50 {PlantHealthCalculator.Calculate(grape, 50)})");
        var sharedNodes = All<SkillNodeSO>(SharedTree);
        Require(Tree.AllNodes.Count == 140 && Tree.AllNodes.All(sharedNodes.Contains), "Old profile: the shared 140-node tree");
        Require(!RM.FixedRoundDuration && Near(RM.EffectiveRoundDuration, 30f), "Old profile: duration from the stat (30 s base), duration nodes active");
        var sharedXp = AssetDatabase.FindAssets("t:ProgressionSO", new[] { "Assets/ScriptableObjects/Prograsiondata" }).Select(g => AssetDatabase.LoadAssetAtPath<ProgressionSO>(AssetDatabase.GUIDToAssetPath(g))).First();
        Require(Near(ProgressionManager.Instance.XPToNextLevel, sharedXp.GetXPForLevel(1)), $"Old profile: shared XP table ({ProgressionManager.Instance.XPToNextLevel})");
        var ui = Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
        var opened = new List<GameObject>();
        for (Transform t = ui.transform; t != null; t = t.parent) if (!t.gameObject.activeSelf) opened.Add(t.gameObject);
        opened.Reverse(); foreach (var go in opened) go.SetActive(true);
        var slots = F<List<SkillNodeUI>>(ui, "nodeUIs").Where(n => n != null).ToList();
        Require(slots.Count == 140 && slots.All(s => s.Node != null && sharedNodes.Contains(s.Node)), "Old profile: every interface slot keeps its shared node");
        foreach (var go in opened) go.SetActive(false);
        // nadirlik XP'si ve saksı fiyatı ortak
        StartRound(2);
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 100000f, ModifierOperation.Set));
        Place(Only(grape), 1, 0); Respawn();
        double xp0 = ProgressionManager.Instance.TotalXPEarned; Attack(Pos(1, 0));
        Require(Near((float)(ProgressionManager.Instance.TotalXPEarned - xp0), grape.xpAmount), $"Old profile: Legendary gives the plain XP ({ProgressionManager.Instance.TotalXPEarned - xp0})");
        var planter = PlanterAsset("2x3");
        Require(planter.Price == planter.cost && planter.PriceType == planter.costType, "Old profile: planter prices from the assets");
        Require(Near(planter.GetBaseStat(StatType.PlantSpawnRate), planter.baseStats.First(e => e.statType == StatType.PlantSpawnRate).value) && Near(planter.GetBaseStat(StatType.PlantSpawnRate), 5f),
            "Old profile: planter base spawn interval from the asset (5 s)");
        ClearTestMods();
        return .2;
    }

    static double PrototypeRuntime()
    {
        var proto = Profile("Run50_BossPrototip");
        Require(State == GameStates.RunSetup && RM.Profile == proto && RunBalanceSO.Active == null && Tree.AllNodes.Count == 140 && Near(Stats.GetBaseStat(StatType.HarvestDamage), 1f) &&
                SegmentEventDirector.Instance.BossMode && Near(BossRewardManager.BehaviorDamageMultiplier, 1f) && Near(BossRewardManager.RareDirectMultiplier(PlantRarity.Legendary), 1f),
            "Boss prototype still runs on the shared data with its own bosses and rewards");
        return .1;
    }

    static double FinalCheck()
    {
        Require(errors == 0, "No error or exception logged during the test");
        return -1;
    }
}
