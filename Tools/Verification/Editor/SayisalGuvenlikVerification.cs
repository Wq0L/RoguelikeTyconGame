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

// Batch (izole kopya): Bölüm 3.7.6.1 İŞLEV testi — sayısal güvenlik (hasar / can / kaynak / XP dönüşümleri), level işleme kare bütçesi,
// round sonu bekleyişi, zincir ayar doğrulaması ve eksik zincir kontrolleri. Denge ölçümü DEĞİLDİR: tarla testin kurduğu sabit
// düzendir; büyük değerler stat'a doğrudan verilir (Set), vuruşlar oyunun saldırı yoluyla (PlayerController.AttackInRadius) yapılır.
// Bilerek üretilen hata logları (NaN, geçersiz ödül, level işleme durdu) beklenen log olarak sayılır ve görünmeleri ayrıca istenir.
[InitializeOnLoad]
public static class SayisalGuvenlikVerification
{
    const string Key = "SayisalGuvenlikVerification";
    const string LogFile = "Logs/SayisalGuvenlikVerification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 37611, N = 11, C = 5;
    const double Again = double.NaN;
    const float Huge = 3e10f;   // HarvestDamage stat'ı: int sınırının ~14 katı (P6 seed 101'de R49: 2,1e11)

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static SayisalGuvenlikVerification() { EditorApplication.update += Tick; }

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

    // Bilerek üretilen hata logu önceden bildirilir (parça eşleşmesi); bildirilmemiş hata ya da istisna testi düşürür.
    static readonly List<string> expected = new();
    static void Expect(string fragment) => expected.Add(fragment);
    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        int i = expected.FindIndex(f => message.Contains(f));
        if (i >= 0 && type == LogType.Error) { expected.RemoveAt(i); notes.Add("   beklenen log: " + message.Split('\n')[0]); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Unsubscribe();
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
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
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static HarvestChain Chain => HarvestChain.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static HarvestBehaviorManager Behaviors => HarvestBehaviorManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Z1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_ZincirV1.asset");
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardSO Zincir => RewardOfferLab.All(Z1.bossRewards).First(r => r.id == "zincir_hasat");
    static StatModifier Mod(StatType s, StatTarget t, float v) => new StatModifier { statType = s, target = t, operation = ModifierOperation.Set, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    static void Strike(float damage, float critChance = 0f, float critMultiplier = 1f)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, critChance));
        TestMod(Mod(StatType.CritMultiplier, StatTarget.Player, critMultiplier));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, .4f));
        TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, 1000f));   // bekleme sırasında yeni bitki doğmasın
    }
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));

    static readonly List<BossRewardSO> copies = new();
    static BossRewardSO ChainCopy(float[] chance, float[] damage = null)
    {
        var copy = Object.Instantiate(Zincir);
        copy.chainChance = chance; copy.chainDamage = damage ?? new[] { .75f, .5f }; copy.chainGenerations = chance.Length;
        copies.Add(copy);
        return copy;
    }

    // ---------------------------------------------------------------- olay kayıtları
    sealed class Hit { public DamageType Type; public int Damage, X, Z, Root, Generation; public bool Echo; }
    static readonly List<Hit> hits = new();
    static readonly List<(DamageType type, int generation, int x, int z)> harvests = new();
    static readonly HashSet<(int, uint)> harvestedLives = new();
    static readonly List<(int level, int frame)> levelEvents = new();
    static int xpEvents;
    static bool lifeTwice;
    static int minX, minZ;
    static (int, int) Local(Vector3 world) { var p = GridManager.Instance.GetGridSystem().GetGridPosition(world); return (p.x - minX, p.z - minZ); }
    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        var c = Local(plant.transform.position); var link = plant.LastHitLink;
        hits.Add(new Hit { Type = type, Damage = damage, X = c.Item1, Z = c.Item2, Root = link.Root, Generation = link.Generation, Echo = link.Echo });
    }
    static void OnHarvested(PlantHealth plant)
    {
        var c = Local(plant.transform.position);
        harvests.Add((plant.KilledBy, plant.KillLink.Generation, c.Item1, c.Item2));
        if (!harvestedLives.Add((plant.GetInstanceID(), plant.LifetimeVersion))) lifeTwice = true;
    }
    static void OnXP() => xpEvents++;
    static void OnLevel(int level) => levelEvents.Add((level, Time.frameCount));
    static void Subscribe()
    {
        PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested;
        Progress.OnXPChanged += OnXP; Progress.OnLevelUp += OnLevel;
    }
    static void Unsubscribe()
    {
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested;
        if (Progress != null) { Progress.OnXPChanged -= OnXP; Progress.OnLevelUp -= OnLevel; }
    }
    static void ResetLogs() { hits.Clear(); harvests.Clear(); levelEvents.Clear(); xpEvents = 0; }

    // ---------------------------------------------------------------- tarla
    static GroundCell CellAt(int x, int z) => RewardOfferLab.OpenCells().First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
    static Vector3 PosAt(int x, int z) => CellAt(x, z).transform.position;
    static GridObject GridAt(int x, int z) => GridManager.Instance.GetGridSystem().GetGridObject(CellAt(x, z).GetGridPosition());
    static PlanterSO Planter11 => AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
    static PlantHealth PlantOf(GridObject grid) { var p = grid.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static PlantHealth PlantAt(int x, int z) => PlantOf(GridAt(x, z));
    static readonly Dictionary<string, PlanterBrain> named = new();
    static PlanterBrain P(string name) => named[name];
    static string TileOf(DamageType type) => type switch { DamageType.Explosion => "Explosive", DamageType.Electric => "Electric", DamageType.Tornado => "Tornado", _ => "Boomerang" };
    static string Tr(DamageType type) => type switch { DamageType.Explosion => "patlama", DamageType.Electric => "elektrik", DamageType.Tornado => "kasırga", _ => "bumerang" };
    static readonly (int x, int z)[] Around8 = { (C - 1, C), (C + 1, C), (C, C - 1), (C, C + 1), (C - 1, C - 1), (C + 1, C + 1), (C - 1, C + 1), (C + 1, C - 1) };

    static PlanterBrain Place(string name, int x, int z, string tile)
    {
        var brain = KirilmaErisimMeasurement.PlacePlanter(Planter11, CellAt(x, z), tile != null ? RewardOfferLab.Tile(tile) : null, tile != null ? 1f : 0f);
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
            SetF(plant, "maxHealth", Math.Max(health, 1)); SetF(plant, "currentHealth", health);
        }
    }
    static void ClearField()
    {
        Behaviors.ClearAll();
        Call(TornadoManager.Instance, "ClearAll");
        RewardOfferLab.ClearField();
        named.Clear();
        ResetLogs();
    }
    static void Attack(int x, int z) => Call(Player, "AttackInRadius", PosAt(x, z));
    static int LiveTornadoes => F<List<Tornado>>(TornadoManager.Instance, "activeTornadoes").Count;

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
    // Kota dönemi sonu olmayan ilk round (testin round sonları run'ı kota yüzünden bitirmesin).
    static int SafeRound(int from) { int r = from; while (Z1.Calendar.IsPeriodEnd(r)) r++; return r; }
    static void StartRound(int round)
    {
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("tarla ve round", LabRound);
        Add("sınır değerleri", Bounds);
        Add("zincir ayarı", ChainConfig);
        Add("yüksek hasar: doğrudan", DirectCase);
        Add("yüksek hasar: doğrudan sonucu", DirectResult);
        Add("yüksek hasar: kritik", CritCase);
        Add("yüksek hasar: kritik sonucu", CritResult);
        foreach (var type in new[] { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang })
        {
            var t = type;
            Add($"yüksek hasar: {Tr(t)}", () => BehaviorCase(t));
            Add($"yüksek hasar: {Tr(t)} sonucu", () => BehaviorResult(t));
        }
        Add("yüksek hasar: artçı ve ikinci dalga", EchoCase);
        Add("yüksek hasar: artçı ve ikinci dalga sonucu", EchoResult);
        Add("yüksek hasar: zincir nesli", ChainDamageCase);
        Add("yüksek hasar: zincir nesli sonucu", ChainDamageResult);
        Add("Sert Kabuk can çarpanı", HealthCase);
        Add("zincir elektriği: görsel havuzu dolu", ElectricPoolCase);
        Add("zincir elektriği: sonuç", ElectricPoolResult);
        Add("temizlik: round sonu, işler + canlı kasırga + canlı bumerang", CleanupCase);
        Add("temizlik: round sonu sonucu", CleanupResult);
        Add("XP: normal miktar", XpNormal);
        Add("XP: büyük miktar", XpLarge);
        Add("XP: büyük miktar sonucu", XpLargeResult);
        Add("XP: round sonu bekler", XpBarrierCase);
        Add("XP: round sonu bekler sonucu", XpBarrierResult);
        Add("XP: geçersiz girdi", XpInvalid);
        Add("XP: sayaca sığmayan iş", XpCounterLimit);
        Add("XP: hassasiyet sınırı", XpPrecision);
        Add("XP: geçersiz maliyet", XpBadCost);
        Add("run sonu: bekleyen level, zincir işi ve canlı efektler", RunEndCase);
        Add("run sonu: sonuç", RunEndResult);
        Add("sahne yeniden", Load);
        Add("yeni run temiz", CleanStart);
        Add("kapanış", FinalCheck);
    }

    static double LabRound()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1) return Again;
        Player.enabled = false;
        MetaSave.UseMemoryOnly();   // run sonu adımı gerçek kayda yazmasın (batch'te zaten kapalı; açıkça)
        NumericSafety.ResetCounters();
        GridUnlockManager.Instance.UnlockNextTier(N);
        var open = RewardOfferLab.OpenCells();
        Must(open.Count == N * N, $"open cells {open.Count}");
        minX = open.Min(c => c.GetGridPosition().x); minZ = open.Min(c => c.GetGridPosition().z);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Strike(100f);
        Subscribe();
        return .05;
    }

    // ================================================================ sınır değerleri ve eski formülle eşdeğerlik
    static double Bounds()
    {
        const NumericSite S = NumericSite.BehaviorDamage;
        int sat0 = NumericSafety.Saturated(S);
        Require(NumericSafety.ToInt(.4f, 0, S) == 0 && NumericSafety.ToInt(.5f, 0, S) == 0 && NumericSafety.ToInt(1.5f, 0, S) == 2 && NumericSafety.ToInt(2.5f, 0, S) == 2 &&
                NumericSafety.ToInt(.2f, 1, S) == 1 && NumericSafety.ToInt(-5f, 0, S) == 0 && NumericSafety.ToInt(-5f, 1, S) == 1,
            "Normal range keeps the old rounding (round half to even, like Mathf.RoundToInt) and the caller's minimum (0 or 1); a negative finite value is floored to the minimum");
        Require(NumericSafety.ToInt(2147483520f, 0, S) == 2147483520 && NumericSafety.ToInt(2147483648f, 0, S) == int.MaxValue &&
                NumericSafety.ToInt(3e10f, 1, S) == int.MaxValue && NumericSafety.ToInt(float.MaxValue, 1, S) == int.MaxValue && NumericSafety.ToInt(float.PositiveInfinity, 1, S) == int.MaxValue &&
                NumericSafety.Saturated(S) - sat0 == 4,
            "float: the largest float below 2^31 (2147483520) converts exactly; 2^31, 3e10, float.MaxValue and +∞ saturate to int.MaxValue (counted: 4) — never 1, 0 or negative");
        int sat1 = NumericSafety.Saturated(S);
        Require(NumericSafety.ToInt(2147483646.4d, 0, S) == 2147483646 && NumericSafety.ToInt(2147483647.4d, 0, S) == int.MaxValue && NumericSafety.ToInt(2147483647.5d, 0, S) == int.MaxValue &&
                NumericSafety.ToInt(2147483648d, 0, S) == int.MaxValue && NumericSafety.ToInt(1e300, 0, S) == int.MaxValue && NumericSafety.ToInt(-1e300, 0, S) == 0 &&
                NumericSafety.Saturated(S) - sat1 == 2,
            "double: values around the limit convert or saturate correctly (2147483646,4 → 2147483646; 2147483647,5 → int.MaxValue; 2^31 and 1e300 saturate, counted: 2); −1e300 → minimum");
        Require(NumericSafety.Add(int.MaxValue - 1, 5, S) == int.MaxValue && NumericSafety.Add(3, 4, S) == 7,
            "Saturating add: int.MaxValue − 1 + 5 stays int.MaxValue; 3 + 4 = 7");
        // Geçersiz sayı: en küçük değer, sayılır, bu tür için bir kez hata logu (log yağmuru yok).
        const NumericSite V = NumericSite.Score;
        int inv0 = NumericSafety.Invalid(V);
        Expect("Sayısal güvenlik (Score)");
        int a = NumericSafety.ToInt(float.NaN, 0, V), b = NumericSafety.ToInt(double.NaN, 1, V), c = NumericSafety.ToInt(float.NegativeInfinity, 0, V);
        for (int i = 0; i < 1000; i++) NumericSafety.ToInt(float.NaN, 0, V);
        Require(a == 0 && b == 1 && c == 0 && NumericSafety.Invalid(V) - inv0 == 1003 && !expected.Contains("Sayısal güvenlik (Score)"),
            "NaN and −∞ return the caller's minimum (0 / 1), are counted (1003 here) and reported with ONE error log for this kind — no log per call");
        // Eski formüllerle eşdeğerlik (normal aralık): rastgele değil, sabit bir dizi.
        int mismatchDirect = 0, mismatchDouble = 0, mismatchCrit = 0;
        uint s = 12345;
        float NextF(float max) { s = s * 1664525u + 1013904223u; return (s >> 8) * (1f / 16777216f) * max; }
        for (int i = 0; i < 100000; i++)
        {
            float x = NextF(2.1e9f);
            if (NumericSafety.ToInt(x, 1, S) != Mathf.Max(1, Mathf.RoundToInt(x))) mismatchDirect++;
            double d = NextF(2.1e9f) * (double)NextF(1f) - 5d;
            if (NumericSafety.ToInt(d, 0, S) != (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(d)))) mismatchDouble++;
            int dmg = (int)NextF(1e6f); float mult = 1f + NextF(4f);
            if (NumericSafety.ToInt(dmg * mult, 0, S) != Mathf.RoundToInt(dmg * mult)) mismatchCrit++;
        }
        Require(mismatchDirect == 0 && mismatchDouble == 0 && mismatchCrit == 0,
            "Same results as the old formulas over 100 000 normal-range samples each: direct (Max(1, RoundToInt)), double conversions (Min/Max/Round) and crit (RoundToInt(damage × multiplier)) — 0 differences");
        int oldDirect = Mathf.Max(1, Mathf.RoundToInt(Huge)), oldCrit = Mathf.RoundToInt(int.MaxValue * 2f), oldBehavior = (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(Mathf.RoundToInt(Huge) * 1d)));
        Note($"eski dönüşümler bu platformda: doğrudan {Huge:0.#e0} → {oldDirect}, kritik int.MaxValue × 2 → {oldCrit}, davranış tabanı {Huge:0.#e0} → {oldBehavior}");
        return .05;
    }

    // ================================================================ zincir ayarı
    static double ChainConfig()
    {
        string Err(BossRewardSO r) { HarvestChainConfig.TryCreate(r, out _, out string e); return e; }
        bool Ok(BossRewardSO r) => HarvestChainConfig.TryCreate(r, out _, out _);
        var nanChance = ChainCopy(new[] { float.NaN, .5f });
        var infDamage = ChainCopy(new[] { .75f, .5f }, new[] { float.PositiveInfinity, .5f });
        var nanDamage = ChainCopy(new[] { .75f, .5f }, new[] { .75f, float.NaN });
        var highChance = ChainCopy(new[] { 1.5f, .5f });
        var shortArray = ChainCopy(new[] { .75f, .5f }, new[] { .75f });
        Require(!Ok(nanChance) && !Ok(infDamage) && !Ok(nanDamage) && !Ok(highChance) && !Ok(shortArray) && Ok(Zincir) && Ok(ChainCopy(new[] { 1f, 0f }, new[] { 0f, 1f })),
            $"Chain config: NaN chance, +∞ damage, NaN damage, chance 1,5 and a short array are refused ({Err(nanChance)} / {Err(infDamage)} / {Err(nanDamage)} / {Err(highChance)}); the real Zincir Hasat and edge values 0 / 1 are accepted");
        int taken = Boss.Taken.Count;
        Expect("Boss ödülü uygulanamadı");
        bool granted = RewardOfferLab.Grant(nanDamage);
        Require(!granted && Boss.Taken.Count == taken && !BossRewardManager.TryGetChain(out _) && Boss.Stacks(nanDamage) == 0,
            "An invalid chain reward is not applied at all (no stack, no taken entry, chain stays off) and the refusal is logged");
        int sat0 = NumericSafety.Saturated(NumericSite.ChainDamage), inv0 = NumericSafety.Invalid(NumericSite.ChainDamage);
        Expect("Sayısal güvenlik (ChainDamage)");
        int neg = HarvestChain.Scale(-5, 1f), nan = HarvestChain.Scale(100, float.NaN), big = HarvestChain.Scale(int.MaxValue, 1.5f), gen1 = HarvestChain.Scale(int.MaxValue, .75f), same = HarvestChain.Scale(188, 1f);
        Require(neg == 0 && nan == 1 && big == int.MaxValue && gen1 == 1610612735 && same == 188 && HarvestChain.Scale(188, .75f) == 141 &&
                NumericSafety.Saturated(NumericSite.ChainDamage) - sat0 == 1 && NumericSafety.Invalid(NumericSite.ChainDamage) - inv0 == 1,
            "HarvestChain.Scale: the factor-1 shortcut no longer passes a broken (negative) damage through (−5 → 0); NaN factor → 1 and reported; int.MaxValue × 1,5 saturates; generation 1 of int.MaxValue = 1 610 612 735; 188 → 188 / 141 as before");
        return .05;
    }

    // ================================================================ yüksek hasar
    const int Max = int.MaxValue;
    static double DirectCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(Huge);
        Place("P", C, C, null); Respawn(P("P"), Max);
        int sat0 = NumericSafety.Saturated(NumericSite.DirectDamage);
        Attack(C, C);
        Must(NumericSafety.Saturated(NumericSite.DirectDamage) > sat0, "direct damage should have saturated");
        return .05;
    }
    static double DirectResult()
    {
        var hit = hits.FirstOrDefault(h => h.Type == DamageType.Direct && (h.X, h.Z) == (C, C));
        Require(hit != null && hit.Damage == Max && harvests.Any(h => (h.x, h.z) == (C, C) && h.type == DamageType.Direct),
            $"Direct hit with HarvestDamage {Huge:0.#e0}: {hit?.Damage} applied (int.MaxValue — before this fix the same hit was {Mathf.Max(1, Mathf.RoundToInt(Huge))}); the plant with int.MaxValue health dies");
        return .05;
    }

    static double CritCase()
    {
        ClearField();
        Strike(1e9f, 1f, 3f);
        Place("P", C, C, null); Respawn(P("P"), Max);
        Attack(C, C);
        return .05;
    }
    static double CritResult()
    {
        var hit = hits.FirstOrDefault(h => h.Type == DamageType.Direct && (h.X, h.Z) == (C, C));
        int oldCrit = Mathf.RoundToInt(Mathf.RoundToInt(1e9f * .85f) * 3f);
        Require(hit != null && hit.Damage == Max,
            $"Crit ×3 on a ~1e9 hit: {hit?.Damage} applied (saturated; the old crit conversion of the same product gives {oldCrit} — negative, i.e. healing)");
        return .05;
    }

    // Kaynak S ortada (davranış şansı 1, can 1: doğrudan hasatla ölür); sekiz komşu davranışsız, int.MaxValue can.
    static double BehaviorCase(DamageType type)
    {
        ClearField(); Boss.ClearAll();
        Strike(Huge);
        Place("S", C, C, TileOf(type));
        for (int i = 0; i < Around8.Length; i++) Place("T" + i, Around8[i].x, Around8[i].z, null);
        Respawn(P("S"), 1);
        for (int i = 0; i < Around8.Length; i++) Respawn(P("T" + i), Max);
        Attack(C, C);
        return type == DamageType.Tornado || type == DamageType.Boomerang ? 1.5 : .3;
    }
    static double BehaviorResult(DamageType type)
    {
        var behaviorHits = hits.Where(h => h.Type == type).ToList();
        int expectedDamage = type switch
        {
            DamageType.Explosion or DamageType.Electric => Max,
            _ => -1,
        };
        // Kasırga / bumerang: tabanın (doymuş int.MaxValue) kendi çarpanı kadarı; çarpan prefab verisi, burada yalnız çökmediği aranır.
        bool ok = behaviorHits.Count > 0 && (expectedDamage > 0 ? behaviorHits.All(h => h.Damage == expectedDamage) : behaviorHits.All(h => h.Damage >= 100_000_000));
        int kills = harvests.Count(h => h.type == type);
        string old = type switch { DamageType.Explosion or DamageType.Electric => "0", _ => "1" };
        Require(ok, $"High damage, {Tr(type)}: {behaviorHits.Count} hits of {string.Join(" / ", behaviorHits.Select(h => h.Damage).Distinct())} (taban HarvestDamage {Huge:0.#e0} doyar; the old conversion made this behavior deal {old}); {kills} plants with int.MaxValue health harvested");
        ClearField(); ClearTestMods(); Strike(100f);
        return .05;
    }

    // Artçı ve ikinci dalga: ilk darbe int.MaxValue, oran 2 → hesaplanan 2^32, uygulanan int.MaxValue. Plan, oyunun yankı yoluyla.
    static double EchoCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(100f);
        Place("E", 2, 2, "Explosive"); Place("L", 8, 8, "Electric");
        foreach (var (x, z) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) }) Place($"t{x}{z}", x, z, null);
        foreach (var (x, z) in new[] { (7, 7), (9, 9), (7, 9), (9, 7) }) Place($"t{x}{z}", x, z, null);
        foreach (var p in named.Values) Respawn(p, Max);
        int sat0 = NumericSafety.Saturated(NumericSite.EchoDamage);
        Must(BehaviorEchoes.Instance.ScheduleExplosion(P("E"), P("E").OccupiedGrids, PosAt(2, 2), Max, new BehaviorEcho(.05f, 2f, 1f)), "explosion echo not scheduled");
        Must(BehaviorEchoes.Instance.ScheduleElectric(P("L"), Max, new BehaviorEcho(.05f, 2f, 1f, 0)), "electric echo not scheduled");
        Must(NumericSafety.Saturated(NumericSite.EchoDamage) - sat0 == 2, "both echo damages should saturate when planned");
        return .5;
    }
    static double EchoResult()
    {
        var explosion = hits.Where(h => h.Echo && h.Type == DamageType.Explosion).ToList();
        var electric = hits.Where(h => h.Echo && h.Type == DamageType.Electric).ToList();
        Require(explosion.Count > 0 && electric.Count > 0 && explosion.All(h => h.Damage == Max) && electric.All(h => h.Damage == Max),
            $"Artçı and the second wave with a first hit of int.MaxValue and ratio 2 (2^32 computed): {explosion.Count} + {electric.Count} echo hits, all int.MaxValue — they saturate instead of wrapping");
        ClearField();
        return .05;
    }

    // Zincir: A (patlama, can 1) → normal patlama int.MaxValue → B (patlama, int.MaxValue can) ölür → B nesil 1, hasar int.MaxValue × 0,75.
    static double ChainDamageCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(Huge);
        Place("A", 3, C, "Explosive"); Place("B", 4, C, "Explosive"); Place("X", 5, C, null);
        Respawn(P("A"), 1); Respawn(P("B"), Max); Respawn(P("X"), Max);
        Must(RewardOfferLab.Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Attack(3, C);
        return .3;
    }
    static double ChainDamageResult()
    {
        var gen1 = hits.Where(h => h.Type == DamageType.Explosion && h.Generation == 1 && (h.X, h.Z) == (5, C)).ToList();
        Require(gen1.Count == 1 && gen1[0].Damage == 1610612735,
            $"Chain generation 1 on saturated damage: {gen1.FirstOrDefault()?.Damage} = int.MaxValue × 0,75 (the factor is applied once to the saturated normal damage, not to the overflowed stat)");
        ClearField(); Boss.ClearAll(); ClearTestMods(); Strike(100f);
        return .05;
    }

    // ================================================================ Sert Kabuk can çarpanı
    static double HealthCase()
    {
        ClearField();
        Place("P", C, C, null); Respawn(P("P"), 1000);
        var plant = PlantAt(C, C);
        SetF(plant, "baseMaxHealth", 2_000_000_000); SetF(plant, "maxHealth", 2_000_000_000); SetF(plant, "currentHealth", 1_000_000_000);
        bool applied = plant.ApplyHealthMultiplier(2f);
        int max = plant.MaxHealth, current = plant.CurrentHealth;
        int oldMax = Mathf.Max(1, Mathf.RoundToInt(2_000_000_000 * 2f));
        bool cleared = plant.ClearHealthMultiplier();
        int back = plant.CurrentHealth, backMax = plant.MaxHealth;
        Require(applied && max == Max && current == 1073741824 && cleared && backMax == 2_000_000_000 && back == 1_000_000_000,
            $"Sert Kabuk ×2 on a 2e9-health plant at half health: max {max} (saturated; the old conversion gave {oldMax}), current {current} (the ratio is kept); clearing restores {backMax} / {back}");
        int inv0 = NumericSafety.Invalid(NumericSite.PlantHealth);
        Expect("Sayısal güvenlik (PlantHealth)");
        bool nan = plant.ApplyHealthMultiplier(float.NaN), inf = plant.ApplyHealthMultiplier(float.PositiveInfinity);
        Require(!nan && !inf && plant.MaxHealth == 2_000_000_000 && NumericSafety.Invalid(NumericSite.PlantHealth) - inv0 == 2,
            "NaN and +∞ health multipliers are refused (health unchanged) and reported (before: NaN set max health to 1)");
        ClearField();
        return .05;
    }

    // ================================================================ zincir elektriği: görsel havuzu dolu
    // A (patlama, can 1) doğrudan hasatla ölür; patlaması E'nin (elektrik) bitkisini öldürür; E'nin elektriği nesil 1 olarak sonraki
    // karede çalışır. Görsel havuzu önce uzak bir saksıdan doldurulur; timeScale 0 iken çizimler sönmez (süreleri işlemez).
    static int skippedVisuals0, xpEvents0;
    static double ElectricPoolCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(100f);
        Place("A", 3, C, "Explosive"); Place("E", 4, C, "Electric"); Place("F", 9, 9, null);
        var targets = new[] { (5, C + 1), (5, C - 1), (6, C + 2), (6, C - 2) };
        for (int i = 0; i < targets.Length; i++) Place("T" + i, targets[i].Item1, targets[i].Item2, null);
        foreach (var p in named.Values) Respawn(p, 1);
        Must(RewardOfferLab.Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Time.timeScale = 0f;
        int capacity = F<int>(Behaviors, "maxElectricBursts");
        for (int i = 0; i < capacity; i++) Must(Behaviors.TryElectric(P("F"), 1), "prefill electric");
        Must(Behaviors.ActiveElectricBursts == capacity, $"electric visual pool should be full ({Behaviors.ActiveElectricBursts}/{capacity})");
        skippedVisuals0 = Behaviors.SkippedElectricVisuals;
        ResetLogs();
        Attack(3, C);
        return .3;
    }
    static double ElectricPoolResult()
    {
        var chainHits = hits.Where(h => h.Type == DamageType.Electric && h.Generation == 1).ToList();
        var chainKills = harvests.Where(h => h.type == DamageType.Electric && h.generation == 1).ToList();
        int skipped = Behaviors.SkippedElectricVisuals - skippedVisuals0;
        Require(skipped >= 1 && chainHits.Count == 4 && chainHits.All(h => h.Damage > 0) && chainKills.Count == 4 && xpEvents == harvests.Count && !lifeTwice,
            $"Chained electric with the visual pool full: drawing skipped ({skipped}), damage still applied to all {chainHits.Count} diagonal targets, {chainKills.Count} harvested; {harvests.Count} harvests = {xpEvents} XP grants (one reward per death)");
        Time.timeScale = 1f;
        ClearField(); Boss.ClearAll();
        return .05;
    }

    // ================================================================ temizlik: round sonu
    // A'nın (4,4) dört komşusundan ikisi halkada: (3,4) bumerang, (4,3) kasırga — A'nın patlaması ikisini de nesil 1'e taşır.
    static readonly (int x, int z)[] Ring = { (3, 4), (3, 5), (3, 6), (6, 4), (6, 5), (6, 6), (4, 3), (5, 3), (4, 7), (5, 7) };
    static string RingTile(int i) => i == 0 ? "Boomerang" : i == 6 ? "Tornado" : i % 2 == 0 ? "Tornado" : "Boomerang";
    static double CleanupCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(100f);
        Place("A", 4, 4, "Explosive"); Place("B", 5, 5, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, Ring[i].x, Ring[i].z, RingTile(i));
        foreach (var p in named.Values) Respawn(p, 1);
        Must(RewardOfferLab.Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        Attack(4, 4);
        return .4;
    }
    static double CleanupResult()
    {
        int tornadoes = LiveTornadoes, boomerangs = Behaviors.ActiveBoomerangs;
        Must(tornadoes > 0 && boomerangs > 0 && Chain.ActiveRoots > 0, $"chained tornadoes ({tornadoes}) and boomerangs ({boomerangs}) should be alive and hold the root ({Chain.ActiveRoots})");
        int pending = QueueOneJob();
        EndRoundQuiet();
        Require(LiveTornadoes == 0 && Behaviors.ActiveBoomerangs == 0 && Chain.ActiveRoots == 0 && Chain.Pending == 0,
            $"Round ends with {pending} chain job(s) queued and {tornadoes} chained tornadoes + {boomerangs} chained boomerangs alive: everything is cleared and every root released");
        ClearTestMods(); Strike(100f);
        StartRound(SafeRound(RM.CurrentRound + 1));
        return .05;
    }

    // Kuyrukta kesin bir iş: B (patlama) doğrudan hasatla ölür, patlaması yeni yerleşen Q'nun (patlama, can 1) bitkisini öldürür;
    // Q'nun nesil 1 işi bu karede kuyruğa girer (sonraki karede çalışır).
    static int QueueOneJob()
    {
        Place("Q", 5, 6, "Explosive"); Respawn(P("Q"), 1);
        Attack(5, 5);
        Must(Chain.Pending > 0, "a chain job should be queued");
        return Chain.Pending;
    }

    // ================================================================ XP
    static ProgressionSO Table => F<ProgressionSO>(Progress, "progressionData");
    // Küçük referans hesap: tablo bölümünde tek tek, tablo bittikten sonra sabit maliyetle bölme.
    static (int levels, double rest) Reference(int level, double xp)
    {
        var t = Table; int n = 0;
        while (true)
        {
            double cost = t.GetXPForLevel(level + n);
            if (t.useAuthoredRequirements && level + n >= t.xpRequirements.Count)
            {
                double k = Math.Floor(xp / cost);
                return (n + (int)k, xp - k * cost);
            }
            if (xp < cost) return (n, xp);
            xp -= cost; n++;
        }
    }

    static double XpNormal()
    {
        ResetLogs();
        int level = Progress.CurrentLevel, gained = RM.LevelsGained;
        double amount = Table.GetXPForLevel(level) + Table.GetXPForLevel(level + 1) + Table.GetXPForLevel(level + 2) - Progress.StoredXP;
        Progress.AddXP(amount);
        Require(RM.LevelsGained - gained == 3 && Progress.CurrentLevel == level + 3 && Progress.StoredXP == 0d && !Progress.HasPendingLevels && levelEvents.All(e => e.frame == Time.frameCount),
            "Normal XP: an amount worth exactly 3 levels gives 3 levels in the same call and frame (old timing kept), 0 XP left over");
        return .05;
    }

    const double BigXp = 1e9;
    static int bigLevel0, bigGained0, bigPending0, bigGranted0, bigFrame0, bigFirstCall;
    static (int levels, double rest) bigRef;
    static double XpLarge()
    {
        ResetLogs();
        SetF(RM, "pendingCardSelections", 0);
        bigLevel0 = Progress.CurrentLevel; bigGained0 = RM.LevelsGained; bigPending0 = RM.PendingCardSelections; bigGranted0 = RM.CardChoicesGranted; bigFrame0 = Time.frameCount;
        bigRef = Reference(bigLevel0, Progress.StoredXP + BigXp);
        // Aynı karede dört ayrı çağrı (dört hasat gibi): bütçe çağrı başına değil kare başınadır.
        Progress.AddXP(BigXp / 4);
        int afterFirst = RM.LevelsGained - bigGained0;
        for (int i = 0; i < 3; i++) Progress.AddXP(BigXp / 4);
        bigFirstCall = RM.LevelsGained - bigGained0;
        Must(bigRef.levels > 2 * ProgressionManager.LevelsPerFrame, $"reference levels {bigRef.levels} should exceed the frame budget");
        Require(afterFirst == ProgressionManager.LevelsPerFrame && bigFirstCall == ProgressionManager.LevelsPerFrame && Progress.HasPendingLevels,
            $"Large finite XP ({BigXp:0.#e0} in four calls in one frame): the frame processes exactly its budget ({bigFirstCall} of {bigRef.levels} levels) — the first call uses it up, the other three add XP but process no further level; the rest is kept as pending level work");
        return .02;
    }
    static double XpLargeResult()
    {
        if (Progress.HasPendingLevels) return Again;
        int levels = RM.LevelsGained - bigGained0, choices = RM.ChoicesPerLevel;
        var frames = levelEvents.GroupBy(e => e.frame).Select(g => g.Count()).ToList();
        bool consecutive = levelEvents.Select((e, i) => e.level == bigLevel0 + 1 + i).All(b => b);
        Require(levels == bigRef.levels && Progress.CurrentLevel == bigLevel0 + bigRef.levels && Progress.StoredXP == bigRef.rest &&
                RM.PendingCardSelections - bigPending0 == bigRef.levels * choices && RM.CardChoicesGranted - bigGranted0 == bigRef.levels * choices &&
                levelEvents.Count == bigRef.levels && consecutive && frames.Max() <= ProgressionManager.LevelsPerFrame && frames.Count > 1,
            $"…then over {frames.Count} frames (at most {frames.Max()} levels per frame): {levels} levels, {Progress.StoredXP} XP left and {RM.PendingCardSelections - bigPending0} card choices — identical to the small reference calculation ({bigRef.levels} levels, {bigRef.rest} XP, × {choices} choices); every level raised OnLevelUp exactly once, in order");
        SetF(RM, "pendingCardSelections", 0);
        return .05;
    }

    static int barrierChoices;
    static bool sawWaiting;
    static double XpBarrierCase()
    {
        ResetLogs();
        SetF(RM, "pendingCardSelections", 0);
        var reference = Reference(Progress.CurrentLevel, Progress.StoredXP + BigXp);
        barrierChoices = reference.levels * RM.ChoicesPerLevel;
        Progress.AddXP(BigXp);
        Call(RM, "EndRound");
        sawWaiting = State == GameStates.Round && RM.IsAwaitingLevels && !RM.IsRoundActive && Progress.HasPendingLevels;
        Must(sawWaiting, $"round end should wait for the pending level work (state {State}, waiting {RM.IsAwaitingLevels})");
        return .02;
    }
    static double XpBarrierResult()
    {
        if (State == GameStates.Round) return Again;
        Require(sawWaiting && State == GameStates.CardSelection && RM.PendingCardSelections == barrierChoices && !Progress.HasPendingLevels,
            $"Round end with pending level work: the round stays in its end state until every level is processed, then opens the card screen with all {RM.PendingCardSelections} choices (= reference) — no choice is lost or decided early");
        SetF(RM, "pendingCardSelections", 1);
        RM.OnCardSelectionComplete();
        if (State == GameStates.RoundChoice && Boss.IsPending) { if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward(); }
        Must(State == GameStates.RoundEnd, "round should close: " + State);
        StartRound(SafeRound(RM.CurrentRound + 1));
        return .05;
    }

    static double XpInvalid()
    {
        double xp = Progress.StoredXP, total = Progress.TotalXPEarned; int level = Progress.CurrentLevel;
        int inv0 = NumericSafety.Invalid(NumericSite.Experience);
        Expect("Sayısal güvenlik (Experience)");
        Progress.AddXP(double.NaN); Progress.AddXP(double.PositiveInfinity); Progress.AddXP(-5d); Progress.AddXP(float.NaN);
        Require(Progress.StoredXP == xp && Progress.TotalXPEarned == total && Progress.CurrentLevel == level && NumericSafety.Invalid(NumericSite.Experience) - inv0 == 4,
            "Invalid XP (NaN, +∞, negative) is not added and is reported once (counted: 4); XP and level unchanged (before: NaN silently stopped all levelling, +∞ looped forever)");
        return .05;
    }

    // Tablonun sonunda (maliyet sabit) saklı XP'nin karşılığı olan level sayısı level sayacına (int) sığmıyorsa iş hiçbir zaman
    // bitmez: kare bütçesiyle günlerce işlenip round sonu bekletilmez; hemen durur ve raporlanır.
    static double XpCounterLimit()
    {
        int level = Progress.CurrentLevel, gained = RM.LevelsGained;
        double before = Progress.StoredXP;
        Must(Table.CostIsConstantFrom(level), $"level {level} should be past the authored table");
        Expect("Level işleme durdu");
        Progress.AddXP(1e16);
        Require(Progress.LevelProcessingHalted && Progress.HaltReason.Contains("int sınırını") && RM.LevelsGained == gained && Progress.CurrentLevel == level &&
                Progress.StoredXP == before + 1e16 && !Progress.HasPendingLevels,
            $"XP 1e16 past the cost table (≈ 6,7e10 levels at the constant cost; seed 101 reached 1,6e11): the backlog can never fit the int level counter, so level processing stops at once and says why ({Progress.HaltReason}); the XP is kept, not clipped; no days-long wait");
        // Round sonu durmuş işi beklemez.
        SetP(RM, "RemainingTime", 0f);
        return .1;
    }

    // Test düzeneği: durmuş işi sıfırlar (yalnız test; oyunda durma kalıcıdır).
    static void ResetHalt() { SetP(Progress, "LevelProcessingHalted", false); SetP(Progress, "HaltReason", null); SetF(Progress, "xp", 0d); }

    static double XpPrecision()
    {
        Must(State != GameStates.Round || !RM.IsAwaitingLevels, "a halted level work must not block the round end");
        if (State == GameStates.CardSelection) { SetF(RM, "pendingCardSelections", 1); RM.OnCardSelectionComplete(); }
        if (State == GameStates.RoundChoice && Boss.IsPending) { if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward(); }
        Must(State == GameStates.RoundEnd, "round should have closed: " + State);
        Note("durmuş level işi round sonunu bekletmedi: round normal kapandı");
        StartRound(SafeRound(RM.CurrentRound + 1));
        // Tablo başında (maliyet küçük ve değişken): 1e22 XP'den 100'lük maliyeti çıkarmak sayıyı değiştirmez.
        int realLevel = Progress.CurrentLevel;
        ResetHalt();
        SetP(Progress, "CurrentLevel", 1); SetP(Progress, "XPToNextLevel", Table.GetXPForLevel(1));
        int gained = RM.LevelsGained;
        Expect("Level işleme durdu");
        Progress.AddXP(1e22);
        Require(Progress.LevelProcessingHalted && Progress.HaltReason.Contains("hassasiyet") && RM.LevelsGained == gained && Progress.CurrentLevel == 1 &&
                Progress.StoredXP == 1e22 && !Progress.HasPendingLevels,
            $"XP 1e22 at the start of the table (level cost below the precision of the stored XP): level processing stops at once and says why ({Progress.HaltReason}); the XP is kept; no endless loop (before: the level counter rose forever while the XP never fell)");
        ResetHalt();
        SetP(Progress, "CurrentLevel", realLevel); SetP(Progress, "XPToNextLevel", Table.GetXPForLevel(realLevel));
        return .05;
    }

    static double XpBadCost()
    {
        // Test düzeneği: bozuk bir tabloyla dene.
        var original = Table;
        ResetHalt();
        var broken = ScriptableObject.CreateInstance<ProgressionSO>();
        broken.useAuthoredRequirements = true; broken.xpRequirements = new List<float> { 100f, float.NaN, 200f };
        SetF(Progress, "progressionData", broken);
        SetP(Progress, "CurrentLevel", 1); SetP(Progress, "XPToNextLevel", 100f);
        int gained = RM.LevelsGained;
        Expect("Level işleme durdu");
        Progress.AddXP(150d);
        Require(RM.LevelsGained - gained == 1 && Progress.LevelProcessingHalted && Progress.HaltReason.Contains("geçersiz") && Progress.StoredXP == 50d && !Progress.HasPendingLevels &&
                broken.Validate() != null && original.Validate() == null,
            $"A NaN level cost stops level processing as soon as it is reached ({Progress.HaltReason}); the 50 XP left over is kept; ProgressionSO.Validate refuses the table ({broken.Validate()}) and accepts the real one");
        SetF(Progress, "progressionData", original);
        SetP(Progress, "LevelProcessingHalted", false); SetP(Progress, "HaltReason", null);
        SetP(Progress, "XPToNextLevel", original.GetXPForLevel(Progress.CurrentLevel)); SetF(Progress, "xp", 0d);
        Object.DestroyImmediate(broken);
        return .05;
    }

    // ================================================================ run sonu
    // Gerçek yol: kota dönemi sonunda skor hedefin altında → EndRound → RunFailed → CompleteRun.
    static int runEndGained;
    static double RunEndCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(100f);
        Place("A", 4, 4, "Explosive"); Place("B", 5, 5, "Explosive");
        for (int i = 0; i < Ring.Length; i++) Place("R" + i, Ring[i].x, Ring[i].z, RingTile(i));
        foreach (var p in named.Values) Respawn(p, 1);
        Must(RewardOfferLab.Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        int periodEnd = Enumerable.Range(1, Z1.runLength).First(Z1.Calendar.IsPeriodEnd);
        SetP(RM, "CurrentRound", periodEnd);
        Attack(4, 4);
        return .4;
    }
    static double RunEndResult()
    {
        int tornadoes = LiveTornadoes, boomerangs = Behaviors.ActiveBoomerangs;
        int pending = QueueOneJob();
        Progress.AddXP(BigXp);
        Must(Progress.HasPendingLevels, "level work should be pending");
        runEndGained = RM.LevelsGained;
        // Bu dönemde skor kazanılmamış say: kota kesin tutmaz (oyunun kendi kota değerlendirmesi).
        SetF(RM, "segmentStartScore", HarvestScoreManager.Instance.TotalScore);
        Must(RM.QuotaTarget > 0, "the period should have a quota target");
        Call(RM, "EndRound");
        Require(State == GameStates.RunComplete && RM.Outcome == RunOutcome.QuotaFailed && LiveTornadoes == 0 && Behaviors.ActiveBoomerangs == 0 &&
                Chain.ActiveRoots == 0 && Chain.Pending == 0 && RM.PendingCardSelections == 0,
            $"Run ends (quota failed) with {pending} chain jobs queued, {tornadoes} tornadoes + {boomerangs} boomerangs alive and level work pending: the run ends at once (a failed run does not wait for levels), effects and roots are cleared, no card choices remain");
        return .3;
    }

    static double Load()
    {
        Require(RM.LevelsGained == runEndGained, $"After the run ended, the pending level work is not processed any more ({RM.LevelsGained - runEndGained} levels in the following frames)");
        Unsubscribe();
        Selection.active = Z1;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1) return Again;
        Require(Progress.CurrentLevel == 1 && Progress.StoredXP == 0d && !Progress.HasPendingLevels && !Progress.LevelProcessingHalted && RM.PendingCardSelections == 0 &&
                Chain.Pending == 0 && Chain.ActiveRoots == 0 && !RM.IsAwaitingLevels,
            "New scene / new run: level 1, no stored XP, no pending level work, no halt, no card choices, no chain work — nothing of the old run is left");
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0 && expected.Count == 0,
            $"Every deliberately produced error was reported where expected (invalid numbers once per kind, refused reward, halted level work) and nothing else was logged as an error ({errors} unexpected)");
        foreach (var copy in copies) if (copy != null) Object.DestroyImmediate(copy);
        return .05;
    }
}
