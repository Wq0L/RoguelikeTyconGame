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

// Batch (izole kopya): Bölüm 3.7.6 İŞLEV testi — sınırlandırılmış davranış zinciri (Run50_ZincirV1, Zincir Hasat).
// Denge ölçümü DEĞİLDİR: tarla testin kurduğu sabit düzendir, şanslar 0 / 1 (ya da hesap için bilinen değer), bitkilerin canı elle
// verilir, vuruşlar oyunun saldırı yoluyla (PlayerController.AttackInRadius) yapılır. Zincir ödülünün test kopyaları yalnız şans,
// hasar ve bütçe alanlarında ayrışır (Object.Instantiate); oyun koduna testlik bir yol yoktur.
// Aşama 1: temel kurallar + patlama ve elektrik. Aşama 2: kasırga ve bumerang, dört davranışın eşleşmeleri, havuz beklemesi,
// round / run / sahne temizliği, kart, Hasat Ritmi'nin round geçişi.
[InitializeOnLoad]
public static class ZincirV1Verification
{
    const string Key = "ZincirV1Verification";
    const string LogFile = "Logs/ZincirV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 3761, N = 11, C = 5;
    const double Again = double.NaN;
    const int Hp = 1000000;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static ZincirV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_ZincirV1.asset");
        profile.bossSeed = Seed; EditorUtility.SetDirty(profile);
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (nextAt == 0) { nextAt = now + 2; stepSince = now; Application.logMessageReceived += CountLogs; Plan(); return; }
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { Finish(null); return; }
            if (stepIndex != shownStep)
            {
                shownStep = stepIndex; stepSince = now;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines(LogFile, new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 240) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now + .02; EditorApplication.QueuePlayerLoopUpdate(); return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Unsubscribe();
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(LogFile, new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Must(bool c, string m) { if (!c) throw new Exception(m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static HarvestChain Chain => HarvestChain.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO Z1 => Profile("Run50_ZincirV1");
    static RunProfileSO E1 => Profile("Run50_KirilmaErisimiV1");
    static BossRewardPoolSO Pool => Z1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardSO Find(BossRewardPoolSO pool, string id) => RewardOfferLab.All(pool).FirstOrDefault(r => r.id == id);
    static BossRewardSO Zincir => Find(Pool, "zincir_hasat");
    static BossRewardSO Artci => Find(Pool, "artci_patlama");
    static BossRewardSO Cifte => Find(Pool, "cifte_akim");
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    static void Strike(float damage, float radius)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
    }
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));

    // Zincir ödülünün test kopyası: yalnız şans, hasar ve bütçe alanları değişir. Gerçek değerler (×0,75 / ×0,50) ayrı vakalarda.
    static readonly List<BossRewardSO> copies = new();
    static BossRewardSO ChainCopy(float[] chance, float[] damage = null, int budget = 32, int perFrame = 8)
    {
        var copy = Object.Instantiate(Zincir);
        copy.chainChance = chance; copy.chainDamage = damage ?? new[] { .75f, .5f };
        copy.chainGenerations = chance.Length; copy.chainRootBudget = budget; copy.chainJobsPerFrame = perFrame;
        copies.Add(copy);
        return copy;
    }
    static bool Grant(BossRewardSO reward) => RewardOfferLab.Grant(reward);

    // ---------------------------------------------------------------- olay kayıtları
    sealed class Hit { public DamageType Type; public int Damage, X, Z, Root, Generation; }
    static readonly List<Hit> hits = new();
    static readonly List<(DamageType type, int root, int generation, bool echo, int x, int z, PlantHealth plant, uint life)> harvests = new();
    static readonly List<(HarvestChain.Trace trace, int frame)> traces = new();
    static int xpEvents, harvestEvents;
    static readonly HashSet<(int, uint)> harvestedLives = new();
    static bool lifeTwice;
    static int minX, minZ;

    static (int, int) Local(Vector3 world)
    {
        var p = GridManager.Instance.GetGridSystem().GetGridPosition(world);
        return (p.x - minX, p.z - minZ);
    }
    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        var c = Local(plant.transform.position); var link = plant.LastHitLink;
        hits.Add(new Hit { Type = type, Damage = damage, X = c.Item1, Z = c.Item2, Root = link.Root, Generation = link.Generation });
    }
    static void OnHarvested(PlantHealth plant)
    {
        var c = Local(plant.transform.position); var link = plant.KillLink;
        harvests.Add((plant.KilledBy, link.Root, link.Generation, link.Echo, c.Item1, c.Item2, plant, plant.LifetimeVersion));
        harvestEvents++;
        if (!harvestedLives.Add((plant.GetInstanceID(), plant.LifetimeVersion))) lifeTwice = true;
    }
    static void OnXP() => xpEvents++;
    static void OnTrace(HarvestChain.Trace trace) => traces.Add((trace, Time.frameCount));
    static void Subscribe()
    {
        PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested; HarvestChain.Traced += OnTrace;
        ProgressionManager.Instance.OnXPChanged += OnXP;
    }
    static void Unsubscribe()
    {
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested; HarvestChain.Traced -= OnTrace;
        if (ProgressionManager.Instance != null) ProgressionManager.Instance.OnXPChanged -= OnXP;
    }
    static void ResetLogs() { hits.Clear(); harvests.Clear(); traces.Clear(); }

    static IEnumerable<HarvestChain.Trace> Of(HarvestChain.TraceKind kind) => traces.Where(t => t.trace.Kind == kind).Select(t => t.trace);
    static IEnumerable<HarvestChain.Trace> FiredBy(PlanterBrain p, DamageType type) => Of(HarvestChain.TraceKind.Fired).Where(t => t.Planter == p && t.Type == type);
    static int RejectedFor(PlanterBrain p, HarvestChain.Reject reason) => Of(HarvestChain.TraceKind.Rejected).Count(t => t.Planter == p && t.Reason == reason);
    static int Rejected(HarvestChain.Reject reason) => Of(HarvestChain.TraceKind.Rejected).Count(t => t.Reason == reason);

    // ---------------------------------------------------------------- tarla
    static GroundCell CellAt(int x, int z) => RewardOfferLab.OpenCells().First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
    static Vector3 PosAt(int x, int z) => CellAt(x, z).transform.position;
    static PlanterSO Planter(int sx, int sz) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {sx}x{sz}.asset");
    static PlantHealth PlantOf(GridObject grid) { var p = grid.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static PlantHealth PlantAt(int x, int z) => PlantOf(GridManager.Instance.GetGridSystem().GetGridObject(CellAt(x, z).GetGridPosition()));

    static readonly Dictionary<string, PlanterBrain> named = new();
    static PlanterBrain P(string name) => named[name];

    // Saksı: boyut, köşe, davranış tile'ı ve şansı (null: davranışsız). extra: ayak izindeki başka bir hücreye ikinci davranış tile'ı.
    static PlanterBrain Place(string name, int sx, int sz, int x, int z, string tile, float chance = 1f, (int x, int z, string tile, float chance)? extra = null)
    {
        if (extra.HasValue)
        {
            var e = extra.Value; var t = RewardOfferLab.Tile(e.tile);
            CellAt(e.x, e.z).ApplyModifier(t, t.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = e.chance }).ToList());
        }
        var brain = KirilmaErisimMeasurement.PlacePlanter(Planter(sx, sz), CellAt(x, z), tile != null ? RewardOfferLab.Tile(tile) : null, tile != null ? chance : 0f);
        named[name] = brain;
        return brain;
    }

    static void Respawn(PlanterBrain brain, int health)
    {
        foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); }
        foreach (var grid in brain.OccupiedGrids)
        {
            var plant = PlantOf(grid);
            if (plant == null) throw new Exception("no plant spawned");
            SetF(plant, "maxHealth", Hp); SetF(plant, "currentHealth", health);
        }
    }
    static void SetHealth(int x, int z, int health) => SetF(PlantAt(x, z), "currentHealth", health);

    static void ClearField()
    {
        HarvestBehaviorManager.Instance.ClearAll();
        Call(TornadoManager.Instance, "ClearAll");
        RewardOfferLab.ClearField();
        named.Clear();
        ResetLogs();
    }

    // Saldırı: hücrenin ortasına, yalnız o hücreye değen küçük yarıçapla (oyunun saldırı yolu).
    static void Attack(int x, int z) => Call(Player, "AttackInRadius", PosAt(x, z));
    static int RootOf(int x, int z) => harvests.Where(h => (h.x, h.z) == (x, z) && h.type == DamageType.Direct).Select(h => h.root).LastOrDefault();

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("tarla ve round", LabRound);
        // aşama 1: temel + patlama ve elektrik
        Add("veri", Data);
        Add("ödül yok: eski kural", NoRewardCase);
        Add("ödül yok: sonuç", NoRewardResult);
        Add("nesiller ve hasar", GenerationsCase);
        Add("nesiller ve hasar: sonuç", GenerationsResult);
        Add("elektrik ↔ patlama", ElectricCase);
        Add("elektrik ↔ patlama: sonuç", ElectricResult);
        Add("A → B → A", RepeatCase);
        Add("A → B → A: sonuç", RepeatResult);
        Add("farklı saksı, aynı davranış", OtherPlanterCase);
        Add("farklı saksı: sonuç", OtherPlanterResult);
        Add("aynı saksı, farklı davranış", OtherBehaviorCase);
        Add("aynı saksı: sonuç", OtherBehaviorResult);
        Add("başarısız zar", FailedRollCase);
        Add("başarısız zar: sonuç", FailedRollResult);
        Add("çok hücreli saksı", MultiCellCase);
        Add("çok hücreli saksı: sonuç", MultiCellResult);
        Add("iki saldırı: 1", () => TwoRootsCase(1));
        Add("iki saldırı: 2", () => TwoRootsCase(2));
        Add("iki saldırı: sonuç", TwoRootsResult);
        Add("şans 0,75", ChanceCase1);
        Add("şans 0,75: sonuç", ChanceResult1);
        Add("şans 0,50", ChanceCase2);
        Add("şans 0,50: sonuç", ChanceResult2);
        Add("katsayılar", CoefficientCase);
        Add("katsayılar: sonuç", CoefficientResult);
        Add("artçı dışlaması", AftershockCase);
        Add("artçı dışlaması: sonuç", AftershockResult);
        Add("ikinci dalga dışlaması", SecondWaveCase);
        Add("ikinci dalga dışlaması: sonuç", SecondWaveResult);
        Add("kare bütçesi", () => QueueCase(32));
        Add("kare bütçesi: sonuç", FrameBudgetResult);
        Add("kök bütçesi", () => QueueCase(3));
        Add("kök bütçesi: sonuç", RootBudgetResult);
        Add("kaynak satıldı", SoldCase);
        Add("kaynak satıldı: sonuç", SoldResult);
        // aşama 2: kasırga ve bumerang, eşleşmeler, havuz, temizlik, kart
        foreach (var source in Behaviors) foreach (var target in Behaviors)
        {
            var s = source; var t = target;
            Add($"eşleşme {Tr(s)} → {Tr(t)}", () => MatrixCase(s, t));
            Add("eşleşme sonucu", () => MatrixResult(s, t));
        }
        Add("eşleşmeler", MatrixSummary);
        foreach (var type in new[] { DamageType.Tornado, DamageType.Boomerang })
        {
            var t = type;
            Add($"havuz dolu: {Tr(t)}", () => PoolCase(t));
            Add("havuz bekliyor", PoolWaiting);
            Add("havuz boşaldı", PoolDrained);
            Add("kök bırakıldı", RootsReleased);
        }
        Add("görüntü: zincir", CaptureCase);
        Add("görüntü: bekle", CaptureWait);
        Add("menü yolu", MenuCase);
        Add("round sonu: bekleyen işler", RoundEndPendingCase);
        Add("round sonu: canlı kasırga", RoundEndLiveCase);
        Add("round sonu: canlı kasırga sonucu", RoundEndLiveResult);
        Add("Hasat Ritmi round geçişi", RhythmCarryCase);
        Add("hasat ve ödül sayımı", PooledAndRewards);
        Add("sahne yeniden", () => Load(Z1));
        Add("yeni run temiz", CleanStart);
        for (int r = 1; r <= 3; r++)
        {
            int round = r;
            Add($"R{r} başla", () => CardRound(round));
            Add($"R{r} süre biter", () => { SetP(RM, "RemainingTime", 0f); return .03; });
            Add($"R{r} bitiş", () => CardAfterRound(round));
        }
        Add("kapanış", FinalCheck);
    }

    static double LabRound()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1) return Again;
        Player.enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(N);
        var open = RewardOfferLab.OpenCells();
        Must(open.Count == N * N, $"open cells {open.Count}");
        minX = open.Min(c => c.GetGridPosition().x); minZ = open.Min(c => c.GetGridPosition().z);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Strike(100f, .4f);
        Subscribe();
        return .05;
    }

    // ---------------------------------------------------------------- veri
    static double Data()
    {
        var z = Zincir; var e = E1;
        var zPool = Pool; var ePool = e.bossRewards;
        var zAll = RewardOfferLab.All(zPool); var eAll = RewardOfferLab.All(ePool);
        int guclu = zPool.stages.FindIndex(s => s.id == "guclu");
        Require(z != null && z.chain && z.chainGenerations == 2 && z.chainChance.SequenceEqual(new[] { .75f, .5f }) && z.chainDamage.SequenceEqual(new[] { .75f, .5f }) &&
                z.chainRootBudget == 32 && z.chainJobsPerFrame == 8 && z.maxStacks == 1 && z.weight == 1f && z.condition == BossRewardCondition.AnyBehaviorPlanter &&
                !z.HasCost && z.IsBreakthrough && z.modifiersPerStack.Count == 0 && z.directDamageMultiplier == 1f && z.behaviorDamageMultiplier == 1f && z.echo == BossRewardEcho.None &&
                zPool.StageIndexOf(z) == guclu && Boss.FirstOfferRound(zPool.stages[guclu]) == 23,
            "Zincir Hasat data: chain on, 2 extra generations, chance ×0,75 / ×0,50, damage ×0,75 / ×0,50, root budget 32, 8 jobs per frame; strong stage (first offered at the R23 boss), weight 1, max 1, offered only with a behavior planter, no cost, no damage or speed bonus");
        Require(zAll.Count == eAll.Count + 1 && eAll.All(r => zAll.Contains(r)) && !eAll.Any(r => r.chain) &&
                zPool.stages.Count == ePool.stages.Count && Enumerable.Range(0, zPool.stages.Count).All(i => zPool.stages[i].firstRound == ePool.stages[i].firstRound) &&
                zPool.choices == ePool.choices && zPool.reserveCurrentStageSlot == ePool.reserveCurrentStageSlot,
            "Pool: Kırılma Erişimi V1's pool with Zincir Hasat added to the strong stage; every other reward is the same asset (Artçı 2,00, Çifte Akım 4, Hasat Ritmi, cost rewards); stages and offer rules unchanged");
        Require(Z1 != e && Z1.bossRewards != e.bossRewards && Z1.balance == e.balance && Z1.bossPool == e.bossPool && Z1.runLength == e.runLength &&
                Z1.segmentTargets.SequenceEqual(e.segmentTargets) && Z1.bossTargets.SequenceEqual(e.bossTargets) &&
                Z1.bossCalendar.Select(d => d.round).SequenceEqual(e.bossCalendar.Select(d => d.round)) && Z1.choicesPerLevel == e.choicesPerLevel &&
                Z1.startingGold == e.startingGold && Z1.fixedRoundDuration == e.fixedRoundDuration,
            "Profile: everything except the reward pool is Kırılma Erişimi V1's (calendar, targets, balance set, start budget, round length, choices)");
        var others = AssetDatabase.FindAssets("t:BossRewardPoolSO").Select(g => AssetDatabase.LoadAssetAtPath<BossRewardPoolSO>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != zPool).ToList();
        Require(others.All(p => !RewardOfferLab.All(p).Any(r => r.chain)) && AssetDatabase.FindAssets("t:BossRewardSO").Select(g => AssetDatabase.LoadAssetAtPath<BossRewardSO>(AssetDatabase.GUIDToAssetPath(g))).Count(r => r.chain) == 1,
            $"Older pools ({others.Count}) contain no chain reward; the chain reward exists once");
        return .05;
    }

    // ---------------------------------------------------------------- 1) ödül yok
    // Doğrudan → A (patlama) → B, C, D (patlama, 1 can) bir sırada.
    static void Line()
    {
        ClearField();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive"); Place("C", 1, 1, 5, C, "Explosive"); Place("D", 1, 1, 6, C, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        ResetLogs();
    }

    static int chainAttemptsBefore;
    static double NoRewardCase()
    {
        Boss.ClearAll();
        Line();
        chainAttemptsBefore = Chain.Attempts(1) + Chain.Attempts(2);
        Attack(3, C);
        Must(harvests.Any(h => (h.x, h.z) == (3, C) && h.type == DamageType.Direct), "A should be harvested directly");
        return .4;
    }

    static double NoRewardResult()
    {
        Require(harvests.Count(h => h.type == DamageType.Explosion) == 1 && harvests.Any(h => (h.x, h.z) == (4, C)) && !Of(HarvestChain.TraceKind.Fired).Any() && !Of(HarvestChain.TraceKind.Attempt).Any() &&
                Chain.Attempts(1) + Chain.Attempts(2) == chainAttemptsBefore && Chain.ActiveRoots == 0 && Chain.Pending == 0 && RootOf(3, C) == 0 &&
                PlantAt(5, C) != null && !PlantAt(5, C).IsDead,
            "No reward: the old rule — A's direct harvest fires A's explosion, B dies to it and nothing more happens (no root is opened, no chain roll, no queue)");
        return .05;
    }

    // ---------------------------------------------------------------- 2) nesiller ve hasar
    static double GenerationsCase()
    {
        Boss.ClearAll();
        Line();
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Attack(3, C);
        return .4;
    }

    static double GenerationsResult()
    {
        int root = RootOf(3, C);
        var bHits = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 1).ToList();
        var cHits = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 2).ToList();
        var gen0 = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 0).ToList();
        Require(root != 0 && FiredBy(P("B"), DamageType.Explosion).Count() == 1 && FiredBy(P("C"), DamageType.Explosion).Count() == 1 && !FiredBy(P("D"), DamageType.Explosion).Any() &&
                FiredBy(P("B"), DamageType.Explosion).All(t => t.Generation == 1 && t.Root == root) && FiredBy(P("C"), DamageType.Explosion).All(t => t.Generation == 2 && t.Root == root) &&
                RejectedFor(P("D"), HarvestChain.Reject.Generation) == 1 &&
                gen0.All(h => h.Damage == 100 && h.Root == root) && bHits.Count > 0 && bHits.All(h => h.Damage == 75 && h.Root == root) && cHits.Count > 0 && cHits.All(h => h.Damage == 50) &&
                harvests.Any(h => (h.x, h.z) == (6, C) && h.generation == 2) && Chain.Pending == 0,
            "Generations: A's direct harvest (one root) → A's explosion (generation 0, 100) kills B → B explodes as generation 1 (75 = 100 × 0,75) and kills C → C explodes as generation 2 (50 = 100 × 0,50, not 0,75 × 0,50) and kills D → D starts nothing (generation limit)");
        return .05;
    }

    // ---------------------------------------------------------------- 3) elektrik ↔ patlama
    static double ElectricCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("E", 1, 1, 4, C, "Electric"); Place("X", 1, 1, 5, C + 1, "Explosive"); Place("Y", 1, 1, 6, C + 1, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(3, C);
        return .4;
    }

    static double ElectricResult()
    {
        int electric = Mathf.RoundToInt(P("E").GetBehaviorDamage(100, DamageType.Electric) * .75f);
        var eHits = hits.Where(h => h.Type == DamageType.Electric && h.Generation == 1).ToList();
        Require(FiredBy(P("E"), DamageType.Electric).Count() == 1 && FiredBy(P("E"), DamageType.Electric).First().Generation == 1 && eHits.Count > 0 && eHits.All(h => h.Damage == electric) &&
                FiredBy(P("X"), DamageType.Explosion).Count() == 1 && FiredBy(P("X"), DamageType.Explosion).First().Generation == 2 &&
                harvests.Any(h => (h.x, h.z) == (5, C + 1) && h.type == DamageType.Electric && h.generation == 1) &&
                !FiredBy(P("Y"), DamageType.Explosion).Any() && RejectedFor(P("Y"), HarvestChain.Reject.Generation) == 1,
            $"Explosion → electric → explosion: A's explosion kills E → E's electric fires as generation 1 ({electric} per bolt = normal electric × 0,75) and kills X on a diagonal → X explodes as generation 2 → Y, killed by it, starts nothing");
        return .05;
    }

    // ---------------------------------------------------------------- 4) A → B → A
    // A: 2×2 patlama (4,4)–(5,5). Doğrudan (4,4). Patlama B'yi (6,5, elektrik) öldürür; B'nin elektriği çaprazdaki (5,4)'ü,
    // yani A'nın başka bitkisini öldürür: A'nın patlaması bu kökte normal tetikle çalıştı, yeniden başlayamaz.
    static double RepeatCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 2, 2, 4, 4, "Explosive"); Place("B", 1, 1, 6, 5, "Electric");
        Respawn(P("A"), Hp); Respawn(P("B"), 1);
        SetHealth(4, 4, 1); SetHealth(5, 4, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(4, 4);
        return .4;
    }

    static double RepeatResult()
    {
        var aKilledByB = harvests.Any(h => (h.x, h.z) == (5, 4) && h.type == DamageType.Electric && h.generation == 1);
        Require(FiredBy(P("B"), DamageType.Electric).Count() == 1 && aKilledByB && !FiredBy(P("A"), DamageType.Explosion).Any() && RejectedFor(P("A"), HarvestChain.Reject.Repeat) == 1 &&
                hits.Count(h => h.Type == DamageType.Explosion && h.Generation == 0) > 0 && !hits.Any(h => h.Type == DamageType.Explosion && h.Generation > 0),
            "A → B → A: A's explosion (normal trigger, marked visited) kills B → B's electric kills another plant of A → A's same explosion is refused in this root (repeat), no second explosion");
        return .05;
    }

    // ---------------------------------------------------------------- 5) farklı saksı, aynı davranış
    static double OtherPlanterCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, C, C, "Explosive"); Place("B1", 1, 1, C - 1, C, "Explosive"); Place("B2", 1, 1, C + 1, C, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(C, C);
        return .4;
    }

    static double OtherPlanterResult()
    {
        Require(FiredBy(P("B1"), DamageType.Explosion).Count() == 1 && FiredBy(P("B2"), DamageType.Explosion).Count() == 1 && RejectedFor(P("B1"), HarvestChain.Reject.Repeat) == 0,
            "The same behavior on different planters: B1 and B2 (both explosion) each fire once in the same root");
        return .05;
    }

    // ---------------------------------------------------------------- 6) aynı saksı, farklı davranış
    static double OtherBehaviorCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, 4, "Explosive");
        Place("B", 2, 2, 4, 4, "Explosive", 1f, (5, 5, "Electric", 1f));
        Respawn(P("A"), 1); Respawn(P("B"), Hp); SetHealth(4, 4, 1);
        Must(P("B").GetFinalStat(StatType.ExplosionChance) >= 1f && P("B").GetFinalStat(StatType.ElectricChance) >= 1f, "B should have both behaviors");
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(3, 4);
        return .4;
    }

    static double OtherBehaviorResult()
    {
        Require(FiredBy(P("B"), DamageType.Explosion).Count() == 1 && FiredBy(P("B"), DamageType.Electric).Count() == 1 &&
                Of(HarvestChain.TraceKind.Attempt).Count(t => t.Planter == P("B")) == 2,
            "Different behaviors of the same planter: B (2×2, explosion + electric tiles) is tried once per behavior — both fire");
        return .05;
    }

    // ---------------------------------------------------------------- 7) başarısız zar, 8) çok hücreli saksı
    // A: 1×2 patlama (3,4)–(3,5); patlaması B'nin (2×2, 4,4) iki hücresine birden değer: B'nin iki bitkisi aynı kökte ölür.
    static void TwoCellField()
    {
        ClearField();
        Place("A", 1, 2, 3, 4, "Explosive"); Place("B", 2, 2, 4, 4, "Explosive");
        Respawn(P("A"), Hp); Respawn(P("B"), Hp);
        SetHealth(3, 4, 1); SetHealth(4, 4, 1); SetHealth(4, 5, 1);
    }

    static double FailedRollCase()
    {
        Boss.ClearAll();
        TwoCellField();
        Must(Grant(ChainCopy(new[] { 0f, 0f })), "chain not granted");
        ResetLogs();
        Attack(3, 4);
        return .4;
    }

    static double FailedRollResult()
    {
        var attempts = Of(HarvestChain.TraceKind.Attempt).Where(t => t.Planter == P("B")).ToList();
        Require(harvests.Count(h => h.type == DamageType.Explosion && (h.x == 4)) == 2 && attempts.Count == 1 && !attempts[0].Success &&
                RejectedFor(P("B"), HarvestChain.Reject.Repeat) == 1 && !FiredBy(P("B"), DamageType.Explosion).Any(),
            "A failed roll uses up the try: two of B's plants die in one root; the first death rolls (chance 0 → fails), the second is refused as a repeat — no second roll");
        return .05;
    }

    static double MultiCellCase()
    {
        Boss.ClearAll();
        TwoCellField();
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(3, 4);
        return .4;
    }

    static double MultiCellResult()
    {
        Require(FiredBy(P("B"), DamageType.Explosion).Count() == 1 && Of(HarvestChain.TraceKind.Attempt).Count(t => t.Planter == P("B")) == 1 && RejectedFor(P("B"), HarvestChain.Reject.Repeat) == 1,
            "A multi-cell planter is one identity: two of B's plants die in one root, B's explosion is tried and fires once");
        return .05;
    }

    // ---------------------------------------------------------------- 9) iki ayrı saldırı
    static readonly List<int> twoRoots = new();
    static double TwoRootsCase(int attack)
    {
        if (attack == 1)
        {
            ClearField();
            Boss.ClearAll();
            Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive");
            Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
            twoRoots.Clear();
        }
        Respawn(P("A"), 1); Respawn(P("B"), 1);
        Attack(3, C);
        twoRoots.Add(RootOf(3, C));
        return .4;
    }

    static double TwoRootsResult()
    {
        var fired = FiredBy(P("B"), DamageType.Explosion).ToList();
        Require(fired.Count == 2 && twoRoots.Count == 2 && twoRoots[0] != 0 && twoRoots[1] != 0 && twoRoots[0] != twoRoots[1] && fired[0].Root == twoRoots[0] && fired[1].Root == twoRoots[1] &&
                RejectedFor(P("B"), HarvestChain.Reject.Repeat) == 0 && Chain.ActiveRoots == 0,
            $"Two separate attacks are two roots ({twoRoots[0]}, {twoRoots[1]}): B fires in each; the first root's visit does not leak into the second; both roots are released afterwards");
        return .05;
    }

    // ---------------------------------------------------------------- 10) şans hesabı
    static float chanceB;
    static double ChanceCase1()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive", .8f);
        foreach (var p in named.Values) Respawn(p, 1);
        chanceB = P("B").GetFinalStat(StatType.ExplosionChance);
        Must(Grant(Zincir), "chain not granted");
        ResetLogs();
        Attack(3, C);
        return .4;
    }

    static double ChanceResult1()
    {
        var attempt = Of(HarvestChain.TraceKind.Attempt).Where(t => t.Planter == P("B")).ToList();
        Require(attempt.Count == 1 && attempt[0].Generation == 1 && Near(attempt[0].Chance, chanceB * .75f, 1e-5f),
            $"Generation 1 chance (real reward data): B's real explosion chance {chanceB:0.##} × 0,75 = {attempt[0].Chance:0.###} (rolled {(attempt[0].Success ? "success" : "fail")})");
        return .05;
    }

    static float chanceC;
    static double ChanceCase2()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive"); Place("C", 1, 1, 5, C, "Explosive", .8f);
        foreach (var p in named.Values) Respawn(p, 1);
        chanceC = P("C").GetFinalStat(StatType.ExplosionChance);
        Must(Grant(ChainCopy(new[] { 1f, .5f })), "chain not granted");
        ResetLogs();
        Attack(3, C);
        return .4;
    }

    static double ChanceResult2()
    {
        var attempt = Of(HarvestChain.TraceKind.Attempt).Where(t => t.Planter == P("C")).ToList();
        Require(FiredBy(P("B"), DamageType.Explosion).Count() == 1 && attempt.Count == 1 && attempt[0].Generation == 2 && Near(attempt[0].Chance, chanceC * .5f, 1e-5f),
            $"Generation 2 chance: C's real chance {chanceC:0.##} × 0,50 = {attempt[0].Chance:0.###} — the generation's own factor, not multiplied with generation 1's");
        return .05;
    }

    // ---------------------------------------------------------------- 11) katsayılar bir kez
    static double CoefficientCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive"); Place("C", 1, 1, 5, C, null);
        Respawn(P("A"), 1); Respawn(P("B"), 1); Respawn(P("C"), Hp);
        var usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/DavranisUstasi.asset");
        SetP(SpecializationManager.Instance, "Chosen", usta);
        Must(Grant(Find(Pool, "davranisa_adanis")) && Grant(ChainCopy(new[] { 1f, 1f })), "rewards not granted");
        ResetLogs();
        Attack(3, C);
        return .4;
    }

    static double CoefficientResult()
    {
        var first = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 0 && (h.X, h.Z) == (4, C)).ToList();
        var second = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 1 && (h.X, h.Z) == (5, C)).ToList();
        Require(first.Count == 1 && first[0].Damage == 188 && second.Count == 1 && second[0].Damage == 141,
            "Coefficients once: Davranışa Adanış ×1,50 and Davranış Ustası ×1,25 → normal explosion 188 (100 × 1,25 × 1,50); generation 1 = 188 × 0,75 = 141 (the coefficients are not applied again, the direct-damage cost does not leak)");
        SetP(SpecializationManager.Instance, "Chosen", null);
        Boss.ClearAll();
        return .05;
    }

    // ---------------------------------------------------------------- 12) artçı ve ikinci dalga
    static int echoScheduled;
    static double AftershockCase()
    {
        ClearField();
        Boss.ClearAll();
        // A ortada; B komşu (ilk patlama); X iki hücre ötede (yalnız artçı erişir).
        Place("A", 1, 1, C, C, "Explosive"); Place("B", 1, 1, C - 1, C, "Explosive"); Place("X", 1, 1, C + 2, C, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(Artci) && Grant(ChainCopy(new[] { 1f, 1f })), "rewards not granted");
        echoScheduled = Echoes.Scheduled(DamageType.Explosion);
        ResetLogs();
        Attack(C, C);
        return Artci.echoDelay + .6;
    }

    static double AftershockResult()
    {
        Require(harvests.Any(h => (h.x, h.z) == (C + 2, C) && h.echo) && !FiredBy(P("X"), DamageType.Explosion).Any() && RejectedFor(P("X"), HarvestChain.Reject.Excluded) == 1 &&
                FiredBy(P("B"), DamageType.Explosion).Count() == 1 && Echoes.Scheduled(DamageType.Explosion) == echoScheduled + 1,
            "Artçı is outside the chain: the aftershock kills X, X starts nothing (excluded); B's chained explosion fires but schedules no new aftershock (only A's normal explosion did)");
        Boss.ClearAll();
        return .05;
    }

    static double SecondWaveCase()
    {
        ClearField();
        Boss.ClearAll();
        // A elektrik ortada; B ilk dalgada (çapraz 1); Y yalnız ikinci dalganın erişiminde (çapraz 3).
        Place("A", 1, 1, C, C, "Electric"); Place("B", 1, 1, C + 1, C + 1, "Electric"); Place("Y", 1, 1, C - 3, C - 3, "Electric");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(Cifte) && Grant(ChainCopy(new[] { 1f, 1f })), "rewards not granted");
        echoScheduled = Echoes.Scheduled(DamageType.Electric);
        ResetLogs();
        Attack(C, C);
        return Cifte.echoDelay + .6;
    }

    static double SecondWaveResult()
    {
        Require(harvests.Any(h => (h.x, h.z) == (C - 3, C - 3) && h.echo) && !FiredBy(P("Y"), DamageType.Electric).Any() && RejectedFor(P("Y"), HarvestChain.Reject.Excluded) == 1 &&
                FiredBy(P("B"), DamageType.Electric).Count() == 1 && Echoes.Scheduled(DamageType.Electric) == echoScheduled + 1,
            "Çifte Akım's second wave is outside the chain: it kills Y, Y starts nothing; B's chained electric fires but schedules no second wave (only A's normal electric did)");
        Boss.ClearAll();
        return .05;
    }

    // ---------------------------------------------------------------- 13) kuyruk: kare bütçesi ve kök bütçesi
    // A: 2×3 patlama (4,4)–(5,6). Patlaması ayak izinin dört yönden komşusu on hücreye değer: hepsinde 1 canlı patlama saksısı.
    // Onların patlamaları yalnız A'nın canı yüksek bitkilerine ve boş hücrelere değer: tek nesil, on iş.
    static readonly (int x, int z)[] Ring = { (3, 4), (3, 5), (3, 6), (6, 4), (6, 5), (6, 6), (4, 3), (5, 3), (4, 7), (5, 7) };
    static int queueFrame;
    static double QueueCase(int budget)
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 2, 3, 4, 4, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, 1, 1, Ring[i].x, Ring[i].z, "Explosive");
        Respawn(P("A"), Hp); SetHealth(4, 4, 1);
        for (int i = 0; i < Ring.Length; i++) Respawn(P("R" + i), 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f }, null, budget, 8)), "chain not granted");
        ResetLogs();
        Attack(4, 4);
        queueFrame = Time.frameCount;
        Must(Chain.Pending == Math.Min(budget, Ring.Length), $"{Chain.Pending} jobs queued, expected {Math.Min(budget, Ring.Length)}");
        return .5;
    }

    static double FrameBudgetResult()
    {
        var fired = traces.Where(t => t.trace.Kind == HarvestChain.TraceKind.Fired).ToList();
        var perFrame = fired.GroupBy(t => t.frame).OrderBy(g => g.Key).Select(g => g.Count()).ToList();
        Require(fired.Count == 10 && perFrame.Count == 2 && perFrame[0] == 8 && perFrame[1] == 2 && fired.All(t => t.frame > queueFrame) && Chain.MaxPending >= 10 && Chain.Pending == 0 &&
                !Of(HarvestChain.TraceKind.Attempt).Any(t => t.Generation == 2),
            $"Queue: ten generation-1 triggers born in one frame run from the next frame on, 8 per frame (frames: {string.Join(" + ", perFrame)}); none is lost; nothing runs inside the death event");
        return .05;
    }

    static double RootBudgetResult()
    {
        var attempts = Of(HarvestChain.TraceKind.Attempt).ToList();
        Require(attempts.Count == 3 && Of(HarvestChain.TraceKind.Fired).Count() == 3 && Rejected(HarvestChain.Reject.Budget) == 7,
            "Root budget (test value 3): three triggers succeed and run; the other seven are counted as budget-refused and are not even rolled (no RNG is spent once the budget is full)");
        return .05;
    }

    // ---------------------------------------------------------------- 14) kaynak satıldı
    static double SoldCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(3, C);
        Must(Chain.Pending == 1, "B's job should be queued");
        P("B").RemoveSelf();   // aynı karede: iş kuyruktayken saksı satılır
        return .4;
    }

    static double SoldResult()
    {
        Require(Of(HarvestChain.TraceKind.Fired).Count() == 0 && Rejected(HarvestChain.Reject.InvalidSource) == 1 && Chain.Pending == 0 && Chain.ActiveRoots == 0,
            "Source sold while its job waits: the job is refused (invalid source) and dropped; the root is released");
        return .05;
    }

    // ---------------------------------------------------------------- 15) havuzdan dönen bitki, 16) tek ölüm tek ödül
    static double PooledAndRewards()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive");
        Respawn(P("A"), 1); Respawn(P("B"), 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Attack(3, C);
        var dead = harvests.Last(h => (h.x, h.z) == (4, C)).plant;
        Respawn(P("B"), Hp);
        var reborn = PlantAt(4, C);
        bool sameObject = reborn == dead;
        Require(reborn.KillLink.Root == 0 && reborn.LastHitLink.Root == 0 && !reborn.KillLink.Echo && !reborn.IsDead,
            $"Pooled plant: the plant reborn in B's cell{(sameObject ? " (the same pooled object)" : "")} carries no root or generation from its previous life");
        Require(!lifeTwice && harvestEvents == harvestedLives.Count && xpEvents == harvestEvents,
            $"One death, one reward: {harvestEvents} harvests in this test, each plant life harvested once, and exactly one XP grant per harvest ({xpEvents}) — chained deaths do not pay twice");
        return .4;
    }

    // ================================================================ AŞAMA 2: kasırga ve bumerang, eşleşmeler, havuz, temizlik
    static readonly DamageType[] Behaviors = { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang };
    static string TileOf(DamageType type) => type switch
    {
        DamageType.Explosion => "Explosive",
        DamageType.Electric => "Electric",
        DamageType.Tornado => "Tornado",
        _ => "Boomerang",
    };
    static string Tr(DamageType type) => type switch
    {
        DamageType.Explosion => "patlama",
        DamageType.Electric => "elektrik",
        DamageType.Tornado => "kasırga",
        _ => "bumerang",
    };
    static readonly (int x, int z)[] Around8 = { (C - 1, C), (C + 1, C), (C, C - 1), (C, C + 1), (C - 1, C - 1), (C + 1, C + 1), (C - 1, C + 1), (C + 1, C - 1) };

    // Üretim kapalı (bekleme sırasında yeni bitki doğmasın): yalnız bu testin modifier'ı, vakadan sonra kalkar.
    static void NoSpawn() => TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, 1000f, ModifierOperation.Set));

    // Kaynak S (src) ortada; sekiz komşuda T saksıları (dst), hepsi 1 can. S doğrudan hasat edilir: S'nin normal davranışı (nesil 0)
    // bir T'yi öldürür → T'nin davranışı nesil 1 olarak çalışmalı. Kasırga ilk adımda dört yönden birine, bumerang dört yönden birine
    // gider; elektrik çaprazlara, patlama dört yöne değer: her durumda en az bir T ölür.
    static readonly List<string> matrix = new();
    static double MatrixCase(DamageType src, DamageType dst)
    {
        ClearField();
        Boss.ClearAll();
        Strike(100f, .4f); NoSpawn();
        Place("S", 1, 1, C, C, TileOf(src));
        for (int i = 0; i < Around8.Length; i++) Place("T" + i, 1, 1, Around8[i].x, Around8[i].z, TileOf(dst));
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(C, C);
        return src == DamageType.Tornado || src == DamageType.Boomerang ? 1.4 : .5;
    }

    static double MatrixResult(DamageType src, DamageType dst)
    {
        int root = RootOf(C, C);
        var targets = Enumerable.Range(0, Around8.Length).Select(i => P("T" + i)).ToList();
        var fired = Of(HarvestChain.TraceKind.Fired).Where(t => targets.Contains(t.Planter) && t.Type == dst && t.Generation == 1 && t.Root == root).ToList();
        var killedBySource = harvests.Where(h => h.type == src && h.generation == 0 && h.root == root).ToList();
        var chainHits = hits.Where(h => h.Type == dst && h.Generation == 1 && h.Root == root).ToList();
        Must(root != 0 && killedBySource.Count > 0 && fired.Count > 0 && !Of(HarvestChain.TraceKind.Fired).Any(t => t.Planter == P("S")),
            $"{Tr(src)} → {Tr(dst)}: root {root}, source kills {killedBySource.Count}, generation-1 {Tr(dst)} fired {fired.Count}");
        matrix.Add($"{Tr(src)} → {Tr(dst)}: {killedBySource.Count} hasat, {fired.Count} tetik, {chainHits.Count} zincir vuruşu");
        Note($"eşleşme · {Tr(src)} öldürür → {Tr(dst)} çalışır: kaynağın {killedBySource.Count} hasadı, {fired.Count} nesil 1 tetiği, {chainHits.Count} vuruş (hepsi kök {root}, nesil 1)");
        ClearField();
        ClearTestMods(); Strike(100f, .4f);
        return .05;
    }

    static double MatrixSummary()
    {
        Require(matrix.Count == 16 && AreaOutlineFeedback.Instance != null && AreaOutlineFeedback.Instance.ChainPlayed > 0,
            $"All four behaviors trigger each other: 16 of 16 source → target pairs (explosion, electric, tornado, boomerang) — a plant killed by the source's normal behavior fires its own planter's behavior as generation 1 with the same root; the chained planter's footprint got the short source outline ({AreaOutlineFeedback.Instance.ChainPlayed} so far)");
        return .05;
    }

    // ---------------------------------------------------------------- havuz beklemesi (kasırga / bumerang)
    // A: 2×3 patlama; komşu on hücrede dst saksıları; biri (R4) patlama. Havuz testin kendi çağrısıyla önceden doldurulur
    // (TornadoManager.TrySpawn / HarvestBehaviorManager.TryBoomerang): zincir işleri kapasite bekler, patlama işi beklemez.
    static int capacity, attemptsAtStart, triggersAtStart, waitJobsAtStart, firstProcessFrame;
    static DamageType poolType;
    static double PoolCase(DamageType type)
    {
        poolType = type;
        ClearField();
        Boss.ClearAll();
        Strike(100f, .4f); NoSpawn();
        Place("A", 2, 3, 4, 4, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, 1, 1, Ring[i].x, Ring[i].z, i == 4 ? "Explosive" : TileOf(type));
        Respawn(P("A"), Hp); SetHealth(4, 4, 1);
        for (int i = 0; i < Ring.Length; i++) Respawn(P("R" + i), 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        // Havuzu doldur: kenardaki boş hücrelerden, 1 hasarla.
        var grid = GridManager.Instance.GetGridSystem();
        GridObject Corner(int x, int z) => grid.GetGridObject(CellAt(x, z).GetGridPosition());
        if (type == DamageType.Tornado)
        {
            capacity = F<int>(TornadoManager.Instance, "maxActiveTornadoes");
            for (int i = 0; i < capacity; i++) Must(TornadoManager.Instance.TrySpawn(Corner(i % 2 == 0 ? 0 : N - 1, i % 3), 1), "prefill tornado");
            Must(!TornadoManager.Instance.HasCapacity, "tornado pool should be full");
        }
        else
        {
            capacity = F<int>(HarvestBehaviorManager.Instance, "maxBoomerangs");
            for (int i = 0; i < capacity; i++) Must(HarvestBehaviorManager.Instance.TryBoomerang(P("A"), Corner(0, i % N), 1), "prefill boomerang");
            Must(!HarvestBehaviorManager.Instance.HasBoomerangCapacity, "boomerang pool should be full");
        }
        attemptsAtStart = Chain.Attempts(1); triggersAtStart = Chain.Triggers(1); waitJobsAtStart = Chain.PoolWaitJobs;
        ResetLogs();
        Attack(4, 4);
        firstProcessFrame = Time.frameCount + 1;
        Must(Chain.Pending == Ring.Length, $"{Chain.Pending} jobs queued");
        return .2;
    }

    static double PoolWaiting()
    {
        var attempts = Of(HarvestChain.TraceKind.Attempt).ToList();
        int explosionIndex = attempts.FindIndex(t => t.Type == DamageType.Explosion);
        var explosionFired = traces.Where(t => t.trace.Kind == HarvestChain.TraceKind.Fired && t.trace.Type == DamageType.Explosion).ToList();
        int waiting = Ring.Length - 1;
        Require(explosionIndex > 0 && attempts.Take(explosionIndex).Any(t => t.Type == poolType) && explosionFired.Count == 1 && explosionFired[0].frame == firstProcessFrame &&
                Of(HarvestChain.TraceKind.Waiting).Any(t => t.Type == poolType) && Chain.PoolWaitJobs - waitJobsAtStart == waiting && Chain.Pending == waiting &&
                Chain.Attempts(1) - attemptsAtStart == Ring.Length && Chain.Triggers(1) - triggersAtStart == Ring.Length,
            $"{Tr(poolType)} pool full ({capacity} active): {waiting} chained {Tr(poolType)} jobs wait in place without a new roll or a second budget charge; the explosion job queued behind them runs in the first frame (it is not blocked)");
        return .1;
    }

    static double waitStarted;
    static double PoolDrained()
    {
        if (waitStarted == 0) waitStarted = EditorApplication.timeSinceStartup;
        if (Chain.Pending > 0 && EditorApplication.timeSinceStartup - waitStarted < 60) return Again;
        double waited = EditorApplication.timeSinceStartup - waitStarted; waitStarted = 0;
        var fired = Of(HarvestChain.TraceKind.Fired).Count(t => t.Type == poolType);
        Require(Chain.Pending == 0 && fired == Ring.Length - 1 && Chain.Attempts(1) - attemptsAtStart == Ring.Length && Chain.Triggers(1) - triggersAtStart == Ring.Length &&
                Chain.PoolWaitFrames > 0 && Chain.DelaySecondsMax > .1f,
            $"When the pool frees up, every waiting {Tr(poolType)} job runs ({fired} of {Ring.Length - 1}); still {Ring.Length} rolls and {Ring.Length} budget charges in total; longest queue delay so far {Chain.DelaySecondsMax:0.0} s, {Chain.PoolWaitFrames} job-frames waited — counted, not lost");
        return .1;
    }

    static double RootsReleased()
    {
        if (waitStarted == 0) waitStarted = EditorApplication.timeSinceStartup;
        if (Chain.ActiveRoots > 0 && EditorApplication.timeSinceStartup - waitStarted < 60) return Again;
        waitStarted = 0;
        Require(Chain.ActiveRoots == 0,
            $"After the last chained {Tr(poolType)} ends, its root is released (the context lived through the delayed hits and is returned to the pool)");
        ClearField();
        ClearTestMods(); Strike(100f, .4f);
        return .05;
    }

    // ---------------------------------------------------------------- round sonu: bekleyen iş ve canlı zincir kasırgası
    static int roundEndRejected;
    static double RoundEndPendingCase()
    {
        ClearField();
        Boss.ClearAll();
        Strike(100f, .4f); NoSpawn();
        Place("A", 2, 3, 4, 4, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, 1, 1, Ring[i].x, Ring[i].z, "Tornado");
        Respawn(P("A"), Hp); SetHealth(4, 4, 1);
        for (int i = 0; i < Ring.Length; i++) Respawn(P("R" + i), 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        roundEndRejected = Chain.Rejected(HarvestChain.Reject.RoundEnd);
        Attack(4, 4);
        Must(Chain.Pending == Ring.Length && Chain.ActiveRoots == 1, "jobs should be pending");
        EndRoundQuiet();
        Require(Chain.Pending == 0 && Chain.ActiveRoots == 0 && Chain.Rejected(HarvestChain.Reject.RoundEnd) - roundEndRejected == Ring.Length,
            $"Round ends with {Ring.Length} chain jobs queued: all are dropped (counted as round end), no root stays open");
        StartRound(2);
        return .05;
    }

    static double RoundEndLiveCase()
    {
        ClearField();
        Boss.ClearAll();
        Strike(100f, .4f); NoSpawn();
        Place("A", 2, 3, 4, 4, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, 1, 1, Ring[i].x, Ring[i].z, "Tornado");
        Respawn(P("A"), Hp); SetHealth(4, 4, 1);
        for (int i = 0; i < Ring.Length; i++) Respawn(P("R" + i), 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        ResetLogs();
        Attack(4, 4);
        return .3;
    }

    static double RoundEndLiveResult()
    {
        int live = F<List<Tornado>>(TornadoManager.Instance, "activeTornadoes").Count;
        Must(live > 0 && Chain.ActiveRoots == 1 && Chain.Pending == 0, $"chained tornadoes should be alive ({live}) and hold the root ({Chain.ActiveRoots})");
        EndRoundQuiet();
        Require(F<List<Tornado>>(TornadoManager.Instance, "activeTornadoes").Count == 0 && Chain.ActiveRoots == 0 && Chain.Pending == 0,
            $"Round ends while {live} chained tornadoes are alive and hold the root: they are cleared at once and the root is released (no context survives the round)");
        ClearTestMods(); Strike(100f, .4f);
        StartRound(4);
        return .05;
    }

    // ---------------------------------------------------------------- menü yolu
    static double MenuCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("B", 1, 1, 4, C, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Attack(3, C);
        Must(Chain.Pending == 1, "a job should be pending");
        // Menüye dönüşte GameManager durumu MainMenu olur; zincir aynı olayla temizlenir (oyunun kendi işleyicisi).
        Call(Chain, "HandleStateChanged", GameStates.MainMenu);
        Require(Chain.Pending == 0 && Chain.ActiveRoots == 0,
            "Menu / run complete: pending jobs and open roots are dropped by the state change handler");
        return .05;
    }

    // ---------------------------------------------------------------- Hasat Ritmi: hazır hak gerçek round geçişinde
    static BossRewardSO Ritim => Find(Pool, "hasat_ritmi");
    static double RhythmCarryCase()
    {
        ClearField();
        Boss.ClearAll();
        RunPower.Rhythm.Clear();
        Must(Grant(Ritim), "Hasat Ritmi not granted");
        for (int x = 3; x <= 7; x++) Place("P" + x, 1, 1, x, C, null);
        foreach (var p in named.Values) Respawn(p, 1);
        Strike(40f, 5f);
        Call(Player, "AttackInRadius", PosAt(C, C));
        Strike(100f, .4f);
        Must(RunPower.Rhythm.Ready, "charge should be ready");
        int round = RM.CurrentRound;
        EndRoundQuiet();
        Must(State == GameStates.RoundEnd && !RM.IsRoundActive, "round should have ended");
        bool readyBetween = RunPower.Rhythm.Ready;
        StartRound(round + 1);
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Call(hud, "Refresh");
        string text = hud.RhythmText ?? "";
        Require(readyBetween && RunPower.Rhythm.Ready && RunPower.Rhythm.Next().Empowered && text.Contains("HAZIR"),
            $"Hasat Ritmi across a real round change (RoundManager.EndRound → StartNextRound, R{round} → R{round + 1}): the ready charge is kept (existing behavior, unchanged); the next swing is empowered and the HUD still says ready");
        Boss.ClearAll();
        RunPower.Rhythm.Clear();
        return .05;
    }

    // Round sonu (sessiz): kart seçimleri atlanır, boss ödülü varsa ilk seçenek alınır (KirilmaV1Verification ile aynı).
    static void EndRoundQuiet()
    {
        SetF(RM, "pendingCardSelections", 0);
        Call(RM, "EndRound");
        if (State == GameStates.RoundChoice && Boss.IsPending)
        {
            if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward();
        }
        if (State != GameStates.RoundEnd) throw new Exception("round did not end: " + State);
    }

    static void StartRound(int round)
    {
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
    }

    // ---------------------------------------------------------------- görüntü: zincirin kaynağı ve yayılımı
    static RenderTexture target;
    static int captures;
    static void Capture(string name, Vector3 focus, float worldRadius = 7f)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        var roots = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        foreach (var canvas in roots.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        if (hud != null) Call(hud, "Refresh");
        var old = cam.targetTexture;
        cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG());
        float sx = (float)target.width / Mathf.Max(1, cam.pixelWidth), sy = (float)target.height / Mathf.Max(1, cam.pixelHeight);
        var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) }
            .Select(d => cam.WorldToScreenPoint(focus + d * worldRadius)).ToList();
        int x0 = Mathf.Clamp(Mathf.FloorToInt(corners.Min(p => p.x) * sx), 0, target.width - 2), x1 = Mathf.Clamp(Mathf.CeilToInt(corners.Max(p => p.x) * sx), x0 + 2, target.width);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(corners.Min(p => p.y) * sy), 0, target.height - 2), y1 = Mathf.Clamp(Mathf.CeilToInt(corners.Max(p => p.y) * sy), y0 + 2, target.height);
        int w = x1 - x0, h = y1 - y0;
        var pixels = png.GetPixels(x0, y0, w, h);
        var crop = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
        var big = new Color[w * 2 * h * 2];
        for (int y = 0; y < h * 2; y++) for (int x = 0; x < w * 2; x++) big[y * w * 2 + x] = pixels[(y / 2) * w + x / 2];
        crop.SetPixels(big); crop.Apply();
        File.WriteAllBytes($"Logs/{name}_yakin.png", crop.EncodeToPNG());
        Object.DestroyImmediate(crop); Object.DestroyImmediate(png);
        captures++;
        Note("görüntü: Logs/" + name + ".png (+ _yakin)");
    }

    static Exception recorderError;
    sealed class Recorder : MonoBehaviour
    {
        public readonly List<(int frame, Action action)> plan = new();
        void LateUpdate()
        {
            try
            {
                for (int i = 0; i < plan.Count;)
                {
                    if (plan[i].frame > Time.frameCount) { i++; continue; }
                    var item = plan[i]; plan.RemoveAt(i);
                    item.action();
                }
            }
            catch (Exception ex) { recorderError = ex; plan.Clear(); }
        }
    }
    static Recorder recorder;
    static Recorder Rec => recorder != null ? recorder : recorder = new GameObject("Capture Recorder (verification)").AddComponent<Recorder>();

    // Doğrudan → A patlar (nesil 0) → E'nin elektriği (nesil 1) → X'in patlaması (nesil 2): E'nin ateşlendiği kareden itibaren kareler.
    static int firedFrame;
    static void OnFiredForCapture(HarvestChain.Trace t)
    {
        if (t.Kind != HarvestChain.TraceKind.Fired || firedFrame != 0) return;
        firedFrame = Time.frameCount;
        var focus = PosAt(4, C);
        Rec.plan.Add((firedFrame, () => Capture("T376_02_Zincir_nesil1", focus)));
        Rec.plan.Add((firedFrame + 3, () => Capture("T376_03_Zincir_nesil1_yayilma", focus)));
    }

    static double CaptureCase()
    {
        ClearField();
        Boss.ClearAll();
        Place("A", 1, 1, 3, C, "Explosive"); Place("E", 1, 1, 4, C, "Electric"); Place("X", 1, 1, 5, C + 1, "Explosive");
        foreach (var p in named.Values) Respawn(p, 1);
        Must(Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        firedFrame = 0;
        HarvestChain.Traced += OnFiredForCapture;
        Time.captureDeltaTime = 1f / 30f;
        Capture("T376_01_Zincir_once", PosAt(4, C));
        Attack(3, C);
        return .1;
    }

    static double CaptureWait()
    {
        if (recorderError != null) throw recorderError;
        if (firedFrame == 0 || Rec.plan.Count > 0) return Again;
        HarvestChain.Traced -= OnFiredForCapture;
        Time.captureDeltaTime = 0f;
        Require(captures == 3, "Chain images written from the real game loop: before, the frame generation 1 fires (violet source outline on the chained planter) and three frames later");
        return .05;
    }

    // ---------------------------------------------------------------- sahne yeniden: temiz başlangıç, kart
    static double Load(RunProfileSO profile)
    {
        Unsubscribe();
        Selection.active = profile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static int[] D; static long[] Q, BT;
    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1) return Again;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        RunCalendar c = Z1.Calendar;
        D = Enumerable.Range(1, Z1.runLength).Where(c.IsPeriodEnd).ToArray();
        Q = D.Select((_, i) => c.QuotaTarget(i + 1)).ToArray(); BT = D.Select((_, i) => c.BossTarget(i + 1)).ToArray();
        Require(Chain != null && Chain.Pending == 0 && Chain.ActiveRoots == 0 && Chain.Attempts(1) == 0 && Chain.Triggers(1) == 0 && Chain.Rejected(HarvestChain.Reject.RoundEnd) == 0 &&
                !BossRewardManager.TryGetChain(out _) && HarvestChain.BeginRoot() == 0 && Boss.Taken.Count == 0 && !RunPower.Rhythm.Ready && RunPower.Rhythm.Count == 0,
            "New scene / new run (restart): a fresh chain with no queue, no open root and zero counters; the reward is gone, so no root opens; Hasat Ritmi reset");
        RewardOfferLab.Build(RewardOfferLab.Layout.Multi);
        return .05;
    }

    static double CardRound(int r)
    {
        Must(State == (r == 1 ? GameStates.RunSetup : GameStates.RoundEnd), $"R{r}: unexpected state before the round ({State})");
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == r, $"R{r}: round did not start");
        SetP(RM, "RemainingTime", 1000f);
        long add = r == 1 ? Q[0] - BT[0] : r == D[0] ? BT[0] : 0;
        AddScore(add);
        return .05;
    }

    static string FitProblem(BossRewardPanelUI panel, int index)
    {
        foreach (var text in panel.CardTexts(index))
        {
            if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            Rect rect = text.rectTransform.rect; Vector3 size = text.textBounds.size;
            float min = text.name == "Effect" ? 20f : text.name == "Note" || text.name == "Stack" ? 15f : 14f;
            if (text.isTextTruncated) return $"{text.name} text is cut: \"{text.text}\"";
            if (size.x > rect.width + 1.5f || size.y > rect.height + 1.5f) return $"{text.name} text {size.x:0}×{size.y:0} does not fit {rect.width:0}×{rect.height:0}";
            if (text.fontSize < min - .05f) return $"{text.name} text shrank to {text.fontSize:0.#} pt (minimum {min})";
        }
        return null;
    }

    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");

    static double CardAfterRound(int r)
    {
        if (RM.IsRoundActive) return Again;
        if (r != D[0])
        {
            Must(State == GameStates.RoundEnd && !Boss.IsPending, $"R{r}: an ordinary round should end in the round summary ({State})");
            return .03;
        }
        Must(!RM.RunFailed && Boss.IsPending && State == GameStates.RoundChoice, $"R{r}: the boss should be passed and a reward pending ({State})");
        var panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        var list = F<List<BossRewardSO>>(Boss, "offer");
        // Yalnız test düzeneği: kart yazısını göstermek için teklif elle kurulur (Zincir Hasat R23'ten önce sunulmaz).
        list.Clear(); list.Add(Zincir); list.Add(Artci); list.Add(Find(Pool, "hasat_ritmi"));
        Call(panel, "Fill");
        for (int i = 0; i < list.Count; i++) { string fit = FitProblem(panel, i); Must(fit == null, $"card {i} ({list[i].displayName}): {fit}"); }
        var texts = panel.CardTexts(0).Where(t => t.gameObject.activeInHierarchy).Select(t => Strip(t.text)).ToList();
        string all = string.Join(" | ", texts);
        Require(all.Contains("başka saksıların davranışlarını tetikler") && all.Contains("en çok 2 ek nesil") && all.Contains("×0,75 / ×0,50") &&
                all.Contains("bir kez denenir") && all.Contains("Artçı ve ikinci dalga zincire katılmaz") && !all.Contains("kuyruk") && !all.Contains("kök"),
            $"Zincir Hasat card (placed on the screen by the test): fits, and says what the chain does in player terms — {all}");
        Capture("T376_04_ZincirKarti", Vector3.zero, 30f);
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0, "No error or exception logged during the test");
        return .05;
    }
}
