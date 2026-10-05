using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.5 LABORATUVARI — kırılma ödüllerinin (Artçı Patlama, Çifte Akım) erişim adayları.
// Run oynanmaz. Sabit bir tarla düzeni ve sabit statlar kurulur; tek bir round (R23, R30 ya da R40) oyunun ÜRETİM yoluyla oynanır:
// bot PlayerController.AttackInRadius ile vurur, davranışlar ve yankılar oyunun kendi kodundan çalışır. Level ve kart yoktur
// (sabit stat laboratuvarı). Bedelli ödüller kapalıdır. Kollar yalnız bir ödülle ayrışır.
//
// ÖN KAYIT (ölçümden önce yazıldı, 2026-10-02): düzenler, statlar, kollar ve karar kuralı aşağıdaki sabitlerdir; sonuçlara göre
// değiştirilmez. "Güçlü sinerji düzeni" Artçı için PatlamaSinerji, Çifte Akım için ElektrikSinerji'dir.
//   Statlar (Bölüm 3.6 son parametre run'larının patlama / elektrik yolu medyanları, Docs/Bolum3-6/olcum/son-parametreler):
//     R23: 7×7 tarla, hasar 27, saldırı aralığı 2,00 sn, yarıçap 1,10, üretim aralığı 5,3 sn
//     R30: 9×9 tarla, hasar 44, saldırı aralığı 1,90 sn, yarıçap 1,20, üretim aralığı 4,7 sn
//     R40: 9×9 tarla, hasar 75, saldırı aralığı 1,28 sn, yarıçap 1,50, üretim aralığı 4,1 sn
//   Saksılar: tarla 2×2 saksılarla döşenir; son sütun ve son satır 1×1 saksıdır (7×7: 9 büyük + 13 küçük; 9×9: 16 + 17).
//   Davranış tile'ı saksının köşe hücresinin altındadır; saksı sırası: önce büyükler (satır satır), sonra küçükler.
//     Dogrudan         her 4 saksıdan biri: sırayla patlama %25 / elektrik %15 (Common ortası)
//     Patlama          her 3 saksıdan ikisi patlama %45
//     Elektrik         her 3 saksıdan ikisi elektrik %30
//     Karma            beşli döngü: patlama %40, elektrik %28, kasırga %40, bumerang %28, davranışsız
//     PatlamaSinerji   bütün saksılar patlama %90 (Legendary tile'ın üst zarı)
//     ElektrikSinerji  bütün saksılar elektrik %60 (Legendary tile'ın üst zarı)
//   Nişan: saldırı alanında en çok canlı bitki olan nokta (hücre merkezleri ve ara noktalar). İkinci bir politika yok.
//   Karar kuralı (aday seçimi): güçlü sinerji düzeninde, ödülsüz kola göre medyan toplam hasat oranı ≥ ×1,5, üç round'un en az
//   ikisinde. Geçen adaylardan küçük erişim seçilir; hiçbiri geçmezse gerçek hasat katkısı en yüksek olan teslim edilir.
//   Çifte Akım havuzda kalır: Elektrik ya da ElektrikSinerji düzeninde, üç round'un en az ikisinde, seçilen adayın medyan toplam
//   hasat oranı ≥ ×1,10 VE 10 seed'in en az 7'sinde oran > ×1,00 ise. Değilse yalnız yeni profilin havuzundan çıkarılır.
//
// Bölüm 3.7.6 ZİNCİR LABORATUVARI (aynı araç, "zincir23/30/40" kipleri): zincir kapalı ("zincirsiz") ve açık ("zincir", Zincir Hasat
// asset'inin kendisi, gerçek değerler) aynı düzen, aynı stat, aynı seed. Düzenler: Patlama, Elektrik, KasirgaBumerang (her 3 saksıdan
// ikisi: sırayla kasırga %40 / bumerang %28), Karma, Davranissiz (kontrol). Statlar yukarıdaki R23 / R30 / R40 statlarıdır.
// Karar kuralı yoktur (aday seçilmez): sonuç rapor edilir; zayıfsa katsayı yükseltilmez, güçlüyse kısılmaz.
[InitializeOnLoad]
public static class KirilmaErisimMeasurement
{
    const string Key = "KirilmaErisimMeasurement", ModeKey = "KirilmaErisimMeasurement.mode";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset";
    const float FrameTime = 1f / 30f;
    const float ExplosionCurrentCells = 1.5f;

    public static readonly string[] Layouts = { "Dogrudan", "Patlama", "Elektrik", "Karma", "PatlamaSinerji", "ElektrikSinerji" };
    static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    // Round'a göre sabit statlar: (tarla, hasar, saldırı aralığı, yarıçap, üretim aralığı).
    static (int grid, float damage, float interval, float radius, float spawn) Stats(int round) =>
        round <= 23 ? (7, 27f, 2.00f, 1.10f, 5.3f) : round <= 30 ? (9, 44f, 1.90f, 1.20f, 4.7f) : (9, 75f, 1.28f, 1.50f, 4.1f);

    public static string[] ArmsFor(string layout) => layout switch
    {
        "Dogrudan" => new[] { "odulsuz", "ritim", "yikim", "artci_A", "artci_C", "cifte_A", "cifte_C" },
        "Patlama" or "PatlamaSinerji" => new[] { "odulsuz", "artci_A", "artci_B", "artci_C", "ritim", "yikim" },
        "Elektrik" or "ElektrikSinerji" => new[] { "odulsuz", "cifte_A", "cifte_B", "cifte_C", "ritim", "yikim" },
        _ => new[] { "odulsuz", "artci_A", "artci_B", "artci_C", "cifte_A", "cifte_B", "cifte_C", "ritim", "yikim" },
    };

    // Aday değerleri. Artçı: ikinci darbenin yarıçapı hücre cinsinden (taban patlama yarıçapı 1,2 hücre; veri alanı çarpandır).
    // Çifte Akım: ikinci dalganın çapraz erişimi, hücre (0: ilk dalgayla aynı = 2).
    public static float ArtciCells(string arm) => arm == "artci_B" ? 2.00f : arm == "artci_C" ? 2.25f : ExplosionCurrentCells;
    public static int CifteReach(string arm) => arm == "cifte_B" ? 3 : arm == "cifte_C" ? 4 : 0;

    sealed class Job
    {
        public int Round, Seed, Repeat; public string Layout, Arm, Tag = "";
        public bool RealTime, Trace, Perf;
    }

    sealed class Row
    {
        public Job Job; public string Boss = "";
        public int Attacks, DirectHits, Kills, EchoKills, EchoKillsNew, EchoHits, EchoHitsNew, EchoCells, Planned, Executed, Dropped, PendingMax, Frames;
        public long DirectRaw, DirectApplied, BehaviorRaw, BehaviorApplied, EchoRaw, EchoApplied, Score;
        public readonly int[] KillsBy = new int[5], HitsBy = new int[5], Triggers = new int[5];
        public int SkipExplosion, SkipElectric, SkipBolts, SkipBoomerang, Gc0, AreaPlayed, AreaSkipped;
        // Zincir (Bölüm 3.7.6): hasatın kaynağı (öldüren vuruşun bağlamı), nesil başına deneme / tetik / yürütme, retler, kuyruk.
        public int KillsDirect, KillsNormal, KillsEcho, ChainHits, NewTargetHits, MaxPending, DelayFramesMax, PoolWaitJobs, PoolWaitFrames, ChainPlayed, ChainSkipped;
        public long DelayFramesTotal;
        public readonly int[] KillsGen = new int[3], ChainAttempts = new int[3], ChainTriggers = new int[3], ChainFired = new int[3], Rejected = new int[HarvestChain.RejectKinds];
        public double WallMs, MaxFrameMs, Fill; public long AllocBytes;
        public readonly List<int> EchoWait = new();
    }

    static readonly List<Job> jobs = new();
    static readonly List<Row> rows = new();
    static string mode; static int job = -1, phase, guard; static double nextAt, since;
    static Row row; static Bot bot; static StringBuilder trace; static int roundFrame0;
    static long score0; static int skipExplosion0, skipElectric0, skipBolts0, skipBoomerang0, gc0, areaPlayed0, areaSkipped0; static long alloc0; static Stopwatch wall;
    static readonly int[] triggers0 = new int[5], echo0 = new int[6];
    static readonly List<GridPosition> scratchFootprint = new(), scratchTargets = new(), scratchOrigins = new();

    static KirilmaErisimMeasurement() { EditorApplication.update += Tick; }

    public static void RunDiagnosis() => Begin("teshis");
    public static void RunLabR23() => Begin("lab23");
    public static void RunLabR30() => Begin("lab30");
    public static void RunLabR40() => Begin("lab40");
    public static void RunRealtime() => Begin("gercekzaman");
    public static void RunTiming() => Begin("zamanlama");
    public static void RunPerformance() => Begin("performans");
    public static void RunGeometry() => Begin("geometri");
    public static void RunSmoke() => Begin("duman");
    public static void RunChainR23() => Begin("zincir23");
    public static void RunChainR30() => Begin("zincir30");
    public static void RunChainR40() => Begin("zincir40");
    // Hızlı karşılaştırma (yaklaşık 1 dk): aynı tarla, aynı seed, zincir kapalı / açık; dört davranışlı düzen, R30.
    public static void RunChainQuick() => Begin("zincirkisa");
    // Ek bilgi: güçlü sinerji düzenleri (bütün saksılar patlama %90 ya da elektrik %60), R30 ve R40.
    public static void RunChainSynergy() => Begin("zincirsinerji");
    public static readonly string[] ChainLayouts = { "Patlama", "Elektrik", "KasirgaBumerang", "Karma", "Davranissiz" };
    const string ChainRewardPath = "Assets/ScriptableObjects/BossRewards/ZincirV1/ZincirHasat_Z1.asset";

    static void Begin(string m)
    {
        SessionState.SetBool(Key, true); SessionState.SetString(ModeKey, m);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        profile.bossSeed = 7501; EditorUtility.SetDirty(profile);
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (measurement)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static void BuildJobs()
    {
        void Add(int round, string layout, int seed, string arm, int repeat = 0, bool real = false, bool tr = false, bool perf = false, string tag = "") =>
            jobs.Add(new Job { Round = round, Layout = layout, Seed = seed, Arm = arm, Repeat = repeat, RealTime = real, Trace = tr, Perf = perf, Tag = tag });
        if (mode == "duman")
        {
            foreach (string layout in Layouts) Add(23, layout, 1, ArmsFor(layout)[1]);
            Add(40, "Karma", 1, "cifte_C"); Add(30, "PatlamaSinerji", 1, "artci_C");
        }
        else if (mode == "teshis")
        {
            // Aynı başlangıç düzeni, aynı seed, aynı statlar: Artçı kapalı ve açık, beşer kez (sabit simülasyon adımı).
            for (int repeat = 0; repeat < 5; repeat++) Add(23, "PatlamaSinerji", 1, "odulsuz", repeat, false, true);
            for (int repeat = 0; repeat < 5; repeat++) Add(23, "PatlamaSinerji", 1, "artci_A", repeat, false, true);
            for (int repeat = 0; repeat < 5; repeat++) Add(23, "ElektrikSinerji", 1, "cifte_A", repeat, false, true);
            for (int repeat = 0; repeat < 3; repeat++) Add(40, "Karma", 2, "artci_A", repeat, false, true);
        }
        else if (mode.StartsWith("lab"))
        {
            int round = int.Parse(mode.Substring(3));
            foreach (string layout in Layouts) foreach (int seed in Seeds) foreach (string arm in ArmsFor(layout)) Add(round, layout, seed, arm);
        }
        else if (mode == "zamanlama")
        {
            // Yankı zamanlaması düzeltmesinin önce / sonra karşılaştırması: mevcut Artçı ve ödülsüz kol, patlamalı üç düzen.
            foreach (int round in new[] { 23, 30, 40 }) foreach (string layout in new[] { "Patlama", "PatlamaSinerji", "Karma" })
                foreach (int seed in Seeds) foreach (string arm in new[] { "odulsuz", "artci_A" }) Add(round, layout, seed, arm);
        }
        else if (mode == "gercekzaman")
        {
            // Gerçek zamanlı kontrol: kare adımı sabitlenmez. Birebir tekrar beklenmez; yön ve büyüklük kontrolüdür.
            foreach (int seed in new[] { 1, 2, 3 })
            {
                foreach (string arm in new[] { "odulsuz", "artci_A", "artci_B", "artci_C" }) Add(23, "PatlamaSinerji", seed, arm, 0, true);
                foreach (string arm in new[] { "odulsuz", "cifte_A", "cifte_B", "cifte_C" }) Add(23, "ElektrikSinerji", seed, arm, 0, true);
            }
        }
        else if (mode == "zincirkisa")
        {
            foreach (string layout in new[] { "Patlama", "Elektrik", "KasirgaBumerang", "Karma" }) foreach (string arm in new[] { "zincirsiz", "zincir" }) Add(30, layout, 1, arm);
        }
        else if (mode == "zincirsinerji")
        {
            foreach (int round in new[] { 30, 40 }) foreach (string layout in new[] { "PatlamaSinerji", "ElektrikSinerji" })
                foreach (int seed in Seeds) foreach (string arm in new[] { "zincirsiz", "zincir" }) Add(round, layout, seed, arm);
        }
        else if (mode.StartsWith("zincir"))
        {
            int round = int.Parse(mode.Substring(6));
            foreach (string layout in ChainLayouts) foreach (int seed in Seeds) foreach (string arm in new[] { "zincirsiz", "zincir" }) Add(round, layout, seed, arm);
        }
        else if (mode == "performans")
        {
            // Yoğun tarla (R40, 9×9, güçlü sinerji): eski ve yeni erişim. Aynı seed üç kez (kare maliyeti ölçümü gürültülüdür).
            for (int repeat = 0; repeat < 3; repeat++)
            {
                foreach (string arm in new[] { "odulsuz", "artci_A", "artci_B", "artci_C" }) Add(40, "PatlamaSinerji", 1, arm, repeat, false, false, true);
                foreach (string arm in new[] { "odulsuz", "cifte_A", "cifte_B", "cifte_C" }) Add(40, "ElektrikSinerji", 1, arm, repeat, false, false, true);
            }
        }
    }

    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static StatManager StatsM => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Profile => AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
    static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (job < 0)
            {
                mode = SessionState.GetString(ModeKey, "duman");
                if (mode == "geometri") { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/KirilmaErisimGeometri.md", GeometryReport()); Finish(null); return; }
                BuildJobs();
                job = 0; phase = 0; nextAt = now + 2; since = now;
                PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested; BehaviorEchoes.Traced += OnEcho;
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (now < nextAt) return;
            if (now - since > 400) throw new Exception($"stuck in job {job} phase {phase} state {State}");
            if (phase == 0) { if (!Ready()) { if (guard++ > 6000) throw new Exception("scene did not reach RunSetup"); return; } Setup(); phase = 1; since = now; return; }
            Drive(now);
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static bool Ready() => RM != null && GameManager.Instance != null && State == GameStates.RunSetup && RM.Profile == Profile && GridUnlockManager.Instance != null &&
                           Boss != null && SegmentEventDirector.Instance != null && SegmentEventDirector.Instance.RunSeed == 7500 + jobs[job].Seed;

    public static BossRewardSO Reward(string id) => RewardOfferLab.All(AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath).bossRewards).First(r => r.id == id);

    // Kolun ödülü: havuzdaki asset'in kopyası; aday kollarında yalnız erişim alanı değişir (hasar, gecikme, tetik aynı).
    public static BossRewardSO ArmReward(string arm)
    {
        if (arm == "odulsuz" || arm == "zincirsiz") return null;
        if (arm == "zincir") return AssetDatabase.LoadAssetAtPath<BossRewardSO>(ChainRewardPath);
        if (arm == "ritim") return Reward("hasat_ritmi");
        if (arm == "yikim") return Reward("yikim_gucu");
        if (arm.StartsWith("artci"))
        {
            var reward = Object.Instantiate(Reward("artci_patlama"));
            if (arm != "artci_A") reward.echoRadius = ArtciCells(arm) / HarvestBehaviorGeometry.ExplosionRadiusCells;
            return reward;
        }
        var wave = Object.Instantiate(Reward("cifte_akim"));
        wave.echoReach = CifteReach(arm);
        return wave;
    }

    // ---------------------------------------------------------------- tarla
    static readonly List<PlanterBrain> planters = new();

    public static PlanterBrain PlacePlanter(PlanterSO so, GroundCell origin, TileModifierSO tile, float chance)
    {
        var grid = GridManager.Instance.GetGridSystem();
        var pos = origin.GetGridPosition();
        if (tile != null)
            origin.ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = chance }).ToList());
        var cells = new List<GridObject>();
        for (int x = 0; x < so.sizeX; x++) for (int z = 0; z < so.sizeZ; z++)
        {
            var g = grid.GetGridObject(new GridPosition(pos.x + x, pos.z + z));
            var ground = g?.GetGroundCellCached();
            if (g == null || ground == null || ground.IsLocked || g.HasPlanterObject()) throw new Exception($"cannot place {so.name} at {pos.x},{pos.z}");
            cells.Add(g);
        }
        // PlacementManager.PlacePlanter ile aynı kurulum (mağaza ve fiyat olmadan): prefab, hücre kayıtları, PlanterBrain.Initialize.
        float cell = GridManager.Instance.GetCellSize();
        var planter = Object.Instantiate(so.prefab);
        planter.transform.position = origin.transform.position + new Vector3((so.sizeX - 1) * cell * .5f, 0f, (so.sizeZ - 1) * cell * .5f);
        planter.GetComponent<GhostController>()?.SetGhostMode(false);
        foreach (var g in cells) g.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>();
        brain.Initialize(so, cells);
        foreach (var g in cells) g.SetPlanterBrain(brain);
        return brain;
    }

    static (string tile, float chance) BehaviorOf(string layout, int index) => layout switch
    {
        "Dogrudan" => index % 8 == 0 ? ("Explosive", .25f) : index % 8 == 4 ? ("Electric", .15f) : (null, 0f),
        "Patlama" => index % 3 != 2 ? ("Explosive", .45f) : (null, 0f),
        "Elektrik" => index % 3 != 2 ? ("Electric", .30f) : (null, 0f),
        "Karma" => (index % 5) switch { 0 => ("Explosive", .40f), 1 => ("Electric", .28f), 2 => ("Tornado", .40f), 3 => ("Boomerang", .28f), _ => (null, 0f) },
        "PatlamaSinerji" => ("Explosive", .90f),
        "ElektrikSinerji" => ("Electric", .60f),
        "KasirgaBumerang" => (index % 3) switch { 0 => ("Tornado", .40f), 1 => ("Boomerang", .28f), _ => (null, 0f) },
        "Davranissiz" => (null, 0f),
        _ => (null, 0f),
    };

    public static void BuildField(string layout, int gridSize)
    {
        planters.Clear();
        GridUnlockManager.Instance.UnlockNextTier(gridSize);
        var open = RewardOfferLab.OpenCells();
        if (open.Count != gridSize * gridSize) throw new Exception($"open cells {open.Count}, expected {gridSize * gridSize}");
        int minX = open.Min(c => c.GetGridPosition().x), minZ = open.Min(c => c.GetGridPosition().z);
        GroundCell At(int x, int z) => open.First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
        var big = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x2.asset");
        var small = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        var spots = new List<(PlanterSO so, int x, int z)>();
        for (int z = 0; z + 1 < gridSize - 1; z += 2) for (int x = 0; x + 1 < gridSize - 1; x += 2) spots.Add((big, x, z));
        for (int z = 0; z < gridSize; z++) for (int x = 0; x < gridSize; x++)
            if (x == gridSize - 1 || z == gridSize - 1) spots.Add((small, x, z));
        for (int i = 0; i < spots.Count; i++)
        {
            var (tile, chance) = BehaviorOf(layout, i);
            planters.Add(PlacePlanter(spots[i].so, At(spots[i].x, spots[i].z), tile != null ? RewardOfferLab.Tile(tile) : null, chance));
        }
    }

    static void Setup()
    {
        Job j = jobs[job];
        guard = 0;
        Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
        UnityEngine.Random.InitState(9100 + j.Seed * 131 + j.Round);
        var stats = Stats(j.Round);
        BuildField(j.Layout, stats.grid);
        foreach (var (stat, target, value) in new[] { (StatType.HarvestDamage, StatTarget.Player, stats.damage), (StatType.AttackSpeed, StatTarget.Player, stats.interval),
                                                     (StatType.AreaRadius, StatTarget.Player, stats.radius), (StatType.PlantSpawnRate, StatTarget.Planter, stats.spawn) })
            StatsM.AddGlobalModifier(new StatModifier { statType = stat, target = target, operation = ModifierOperation.Set, value = value });
        BossRewardSO reward = ArmReward(j.Arm);
        if (reward != null && !RewardOfferLab.Grant(reward)) throw new Exception("reward not granted: " + j.Arm);
        bot = new GameObject("Measurement Bot").AddComponent<Bot>();
        SetP(RM, "CurrentRound", j.Round - 1); SetF(RM, "awaitingFirstRound", false);
        row = new Row { Job = j };
        score0 = HarvestScoreManager.Instance.TotalScore;
        for (int t = 0; t < 5; t++) triggers0[t] = HarvestBehaviorStats.Triggered((DamageType)t);
        skipExplosion0 = VFXManager.Instance != null ? VFXManager.Instance.ExplosionVisualsSkipped : 0;
        skipElectric0 = HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricVisuals : 0;
        skipBolts0 = HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricBolts : 0;
        skipBoomerang0 = HarvestBehaviorStats.Skipped(DamageType.Boomerang);
        // Artçı alan geri bildirimi (Bölüm 3.7.5.1): çizilen ve havuz dolu olduğu için çizilmeyen dalga.
        areaPlayed0 = AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.AftershockPlayed : 0;
        areaSkipped0 = AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.AftershockSkipped : 0;
        trace = j.Trace ? new StringBuilder() : null;
        // Aynı seed'de bütün kollar aynı zar dizisiyle başlar (tarla kurulumu ve ödül zarı tüketmiş olsa da).
        UnityEngine.Random.InitState(9200 + j.Seed * 977 + j.Round);
        RM.StartNextRound();
        if (State != GameStates.Round || RM.CurrentRound != j.Round) throw new Exception($"round did not start ({State}, round {RM.CurrentRound})");
        var active = SegmentEventDirector.Instance.Active;
        row.Boss = active != null ? active.Data.displayName : "";
        roundFrame0 = Time.frameCount;
        Time.timeScale = 1f; Time.captureDeltaTime = j.RealTime ? 0f : FrameTime;
        gc0 = GC.CollectionCount(0); alloc0 = GC.GetTotalMemory(false); wall = Stopwatch.StartNew();
        if (trace != null) trace.AppendLine($"# {j.Layout} R{j.Round} seed {j.Seed} {j.Arm} tekrar {j.Repeat} · Time.time {Time.time.ToString("R", CultureInfo.InvariantCulture)} · kare {Time.frameCount}");
    }

    static string Rng() { var s = JsonUtility.ToJson(UnityEngine.Random.state); return ((uint)s.GetHashCode()).ToString("x8"); }
    static int Frame => Time.frameCount - roundFrame0;
    static string Cell(PlantHealth plant)
    {
        var owner = plant.Owner;
        Vector3 p = plant.transform.position;
        var grid = GridManager.Instance.GetGridSystem().GetGridPosition(p);
        return $"({grid.x},{grid.z})";
    }

    // Yankı vuruşu, kaynağın İLK darbesinin de eriştiği bir hücrede mi (patlama: ayak izine dört yönden komşu; elektrik: 2 hücre)?
    static bool InFirstArea(PlantHealth plant, DamageType type)
    {
        PlanterBrain source = BehaviorEchoes.ExecutingSource;
        if (source == null) return true;
        scratchFootprint.Clear();
        foreach (var g in source.OccupiedGrids) scratchFootprint.Add(g.GetGroundCellCached().GetGridPosition());
        if (type == DamageType.Explosion) HarvestBehaviorGeometry.ExplosionCells(scratchFootprint, 1f, scratchTargets);
        else HarvestBehaviorGeometry.ElectricCells(scratchFootprint, scratchTargets, scratchOrigins);
        var cell = GridManager.Instance.GetGridSystem().GetGridPosition(plant.transform.position);
        foreach (var t in scratchTargets) if (t.x == cell.x && t.z == cell.z) return true;
        return false;
    }

    // Kökün nesil 0'da (doğrudan ve normal tetik) vurduğu hücreler. Tek küme, round sonunda boşalır: ölçüm kök başına allocation
    // yapmaz (zincir kolunun allocation sütunu oyun kodunu gösterir).
    static readonly HashSet<long> rootCells = new();
    static void TrackChain(PlantHealth plant)
    {
        HarvestLink link = plant.LastHitLink;
        if (link.Root == 0) return;
        var cell = GridManager.Instance.GetGridSystem().GetGridPosition(plant.transform.position);
        long key = ((long)link.Root << 16) | (long)((cell.x & 0xFF) << 8) | (long)(cell.z & 0xFF);
        if (link.Generation <= 0) { rootCells.Add(key); return; }
        row.ChainHits++;
        if (!rootCells.Contains(key)) row.NewTargetHits++;
    }

    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        if (row == null || !RM.IsRoundActive) return;
        TrackChain(plant);
        int applied = Mathf.Min(damage, Mathf.Max(0, plant.CurrentHealth));
        bool echo = BehaviorEchoes.IsExecuting;
        row.HitsBy[(int)type]++;
        if (type == DamageType.Direct) { row.DirectHits++; row.DirectRaw += damage; row.DirectApplied += applied; }
        else if (echo)
        {
            row.EchoHits++; row.EchoRaw += damage; row.EchoApplied += applied;
            lastEchoNew = !InFirstArea(plant, type);
            if (lastEchoNew) row.EchoHitsNew++;
        }
        else { row.BehaviorRaw += damage; row.BehaviorApplied += applied; }
        if (trace != null) trace.AppendLine($"{Frame} hasar {type}{(echo ? " yanki" : "")} {damage} {Cell(plant)} yasam {plant.LifetimeVersion} can {plant.CurrentHealth}");
    }
    static bool lastEchoNew;

    static void OnHarvested(PlantHealth plant)
    {
        if (row == null || !RM.IsRoundActive) return;
        row.Kills++; row.KillsBy[(int)plant.KilledBy]++;
        HarvestLink link = plant.KillLink;
        if (plant.KilledBy == DamageType.Direct) row.KillsDirect++;
        else if (link.Echo) row.KillsEcho++;
        else if (link.Generation >= 1) row.KillsGen[Math.Min(2, (int)link.Generation)]++;
        else row.KillsNormal++;
        if (BehaviorEchoes.IsExecuting) { row.EchoKills++; if (lastEchoNew) row.EchoKillsNew++; }
        if (trace != null) trace.AppendLine($"{Frame} hasat {plant.KilledBy}{(BehaviorEchoes.IsExecuting ? " yanki" : "")} {Cell(plant)} yasam {plant.LifetimeVersion}");
    }

    static void OnEcho(string phase, DamageType type, int id, float delay, int frames)
    {
        if (row == null) return;
        if (phase == "uygula") row.EchoWait.Add(frames);
        if (trace != null)
            trace.AppendLine($"{Frame} yanki {phase} {type} is {id} gecikme {delay.ToString("R", CultureInfo.InvariantCulture)} kare {frames} bekleyen {(Echoes != null ? Echoes.Pending : 0)} zar {Rng()}");
    }

    static void Drive(double now)
    {
        Job j = jobs[job];
        if (RM.IsRoundActive) { if (Echoes != null) row.PendingMax = Math.Max(row.PendingMax, Echoes.Pending); return; }
        wall.Stop();
        row.Attacks = bot.Attacks; row.Frames = Frame; row.Fill = bot.FillSamples > 0 ? bot.FillSum / bot.FillSamples : 0;
        row.Score = HarvestScoreManager.Instance.TotalScore - score0;
        for (int t = 0; t < 5; t++) row.Triggers[t] = HarvestBehaviorStats.Triggered((DamageType)t) - triggers0[t];
        if (Echoes != null)
        {
            row.Planned = Echoes.Scheduled(DamageType.Explosion) + Echoes.Scheduled(DamageType.Electric);
            row.Executed = Echoes.Executed(DamageType.Explosion) + Echoes.Executed(DamageType.Electric);
            row.Dropped = Echoes.Dropped(DamageType.Explosion) + Echoes.Dropped(DamageType.Electric);
            row.EchoCells = Echoes.Cells(DamageType.Explosion) + Echoes.Cells(DamageType.Electric);
        }
        row.SkipExplosion = (VFXManager.Instance != null ? VFXManager.Instance.ExplosionVisualsSkipped : 0) - skipExplosion0;
        row.SkipElectric = (HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricVisuals : 0) - skipElectric0;
        row.SkipBolts = (HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricBolts : 0) - skipBolts0;
        row.SkipBoomerang = HarvestBehaviorStats.Skipped(DamageType.Boomerang) - skipBoomerang0;
        row.AreaPlayed = (AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.AftershockPlayed : 0) - areaPlayed0;
        row.ChainPlayed = AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.ChainPlayed : 0;
        row.ChainSkipped = AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.ChainSkipped : 0;
        var chain = HarvestChain.Instance;   // her iş yeni sahnede: sayaçlar bu round'undur
        if (chain != null)
        {
            for (int g = 1; g <= 2; g++) { row.ChainAttempts[g] = chain.Attempts(g); row.ChainTriggers[g] = chain.Triggers(g); row.ChainFired[g] = chain.Fired(g); }
            for (int k = 0; k < HarvestChain.RejectKinds; k++) row.Rejected[k] = chain.Rejected((HarvestChain.Reject)k);
            row.MaxPending = chain.MaxPending; row.DelayFramesMax = chain.DelayFramesMax; row.DelayFramesTotal = chain.DelayFramesTotal;
            row.PoolWaitJobs = chain.PoolWaitJobs; row.PoolWaitFrames = chain.PoolWaitFrames;
        }
        rootCells.Clear();
        row.AreaSkipped = (AreaOutlineFeedback.Instance != null ? AreaOutlineFeedback.Instance.AftershockSkipped : 0) - areaSkipped0;
        row.WallMs = wall.Elapsed.TotalMilliseconds; row.MaxFrameMs = bot.MaxFrameMs; row.Gc0 = GC.CollectionCount(0) - gc0; row.AllocBytes = GC.GetTotalMemory(false) - alloc0;
        rows.Add(row);
        if (trace != null)
        {
            trace.AppendLine($"# round sonu · kare {Frame} · bekleyen is {(Echoes != null ? Echoes.Pending : 0)} · planlanan {row.Planned} uygulanan {row.Executed} dusen {row.Dropped} · zar {Rng()}");
            Directory.CreateDirectory("Logs/KirilmaErisimIz");
            File.WriteAllText($"Logs/KirilmaErisimIz/{mode}_{j.Layout}_R{j.Round}_s{j.Seed}_{j.Arm}_{j.Repeat}.txt", trace.ToString());
            traces[(j.Layout, j.Round, j.Seed, j.Arm, j.Repeat)] = trace.ToString();
        }
        row = null; trace = null;
        since = now;
        job++;
        if (job >= jobs.Count) { Finish(null); return; }
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath).bossSeed = 7500 + jobs[job].Seed;
        phase = 0; nextAt = now + .35;
        SceneManager.LoadScene("GameScene");
    }

    static readonly Dictionary<(string, int, int, string, int), string> traces = new();

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested; BehaviorEchoes.Traced -= OnEcho;
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        try { if (ex == null && mode != "geometri") Write(); }
        catch (Exception write) { ex = write; }
        File.WriteAllText("Logs/KirilmaErisimMeasurement.txt", ex == null ? $"DONE: {mode} · {rows.Count} round" : "FAIL: " + ex);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    const string Header = "mode,round,layout,seed,arm,repeat,realtime,boss,frames,attacks,fill,directHits,directRaw,directApplied,behaviorRaw,behaviorApplied,echoHits,echoHitsNewArea,echoRaw,echoApplied,echoCells," +
                          "harvests,harvestDirect,harvestExplosion,harvestTornado,harvestBoomerang,harvestElectric,echoHarvests,echoHarvestsNewArea,score," +
                          "trigExplosion,trigTornado,trigBoomerang,trigElectric,echoPlanned,echoExecuted,echoDropped,pendingMax,echoWaitMin,echoWaitMax," +
                          "skipExplosionVisual,skipElectricVisual,skipElectricBolts,skipBoomerang,wallMs,maxFrameMs,gc0,allocBytes,areaPlayed,areaSkipped," +
                          "killsDirect,killsNormal,killsEcho,killsGen1,killsGen2,attempts1,attempts2,triggers1,triggers2,fired1,fired2," +
                          "rejRepeat,rejGeneration,rejBudget,rejInvalid,rejRoundEnd,rejExcluded,chainHits,newTargetHits,maxPending,delayFramesMax,delayFramesTotal," +
                          "poolWaitJobs,poolWaitFrames,chainPlayed,chainSkipped";

    static string Csv(Row r, bool timing = true) => string.Join(",", new object[]
    {
        mode, r.Job.Round, r.Job.Layout, r.Job.Seed, r.Job.Arm, r.Job.Repeat, r.Job.RealTime ? 1 : 0, r.Boss, r.Frames, r.Attacks, N(r.Fill, "0.###"), r.DirectHits, r.DirectRaw, r.DirectApplied, r.BehaviorRaw, r.BehaviorApplied,
        r.EchoHits, r.EchoHitsNew, r.EchoRaw, r.EchoApplied, r.EchoCells,
        r.Kills, r.KillsBy[0], r.KillsBy[1], r.KillsBy[2], r.KillsBy[3], r.KillsBy[4], r.EchoKills, r.EchoKillsNew, r.Score,
        r.Triggers[1], r.Triggers[2], r.Triggers[3], r.Triggers[4], r.Planned, r.Executed, r.Dropped, r.PendingMax,
        r.EchoWait.Count > 0 ? r.EchoWait.Min() : 0, r.EchoWait.Count > 0 ? r.EchoWait.Max() : 0,
        r.SkipExplosion, r.SkipElectric, r.SkipBolts, r.SkipBoomerang,
        timing ? N(r.WallMs, "0") : "", timing ? N(r.MaxFrameMs, "0.0") : "", timing ? r.Gc0.ToString() : "", timing ? r.AllocBytes.ToString() : "",
        r.AreaPlayed, r.AreaSkipped,
        r.KillsDirect, r.KillsNormal, r.KillsEcho, r.KillsGen[1], r.KillsGen[2], r.ChainAttempts[1], r.ChainAttempts[2], r.ChainTriggers[1], r.ChainTriggers[2], r.ChainFired[1], r.ChainFired[2],
        r.Rejected[0], r.Rejected[1], r.Rejected[2], r.Rejected[3], r.Rejected[4], r.Rejected[5], r.ChainHits, r.NewTargetHits, r.MaxPending, r.DelayFramesMax, r.DelayFramesTotal,
        r.PoolWaitJobs, r.PoolWaitFrames, r.ChainPlayed, r.ChainSkipped,
    });

    // Tekrar karşılaştırması için: zamanlama sütunları ve tekrar numarası hariç satır.
    static string Signature(Row r) => Csv(r, false).Replace($",{r.Job.Arm},{r.Job.Repeat},", $",{r.Job.Arm},x,");

    static void Write()
    {
        var csv = new StringBuilder(Header + "\n");
        foreach (Row r in rows) csv.AppendLine(Csv(r));
        File.WriteAllText($"Logs/KirilmaErisim_{mode}.csv", csv.ToString());
        if (mode == "teshis") File.WriteAllText("Logs/KirilmaErisimTeshis.md", DiagnosisReport());
    }

    // Teşhis: aynı işin tekrarları aynı mı; değilse izde ilk ayrışan satır.
    static string DiagnosisReport()
    {
        var md = new StringBuilder("# Yankı ölçümünün tekrarlanabilirliği (Bölüm 3.7.5 teşhisi)\n\n");
        md.AppendLine("Aynı düzen, aynı seed, aynı statlar; her iş ayrı sahne yüklemesinde, sabit simülasyon adımıyla (1/30 sn).\n");
        md.AppendLine("| Düzen | Round | Kol | Tekrar | Aynı çıkan | Hasat (tekrar sırasıyla) | Yankının beklediği kare (en az – en çok) |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        var details = new StringBuilder();
        foreach (var group in rows.GroupBy(r => (r.Job.Layout, r.Job.Round, r.Job.Seed, r.Job.Arm)))
        {
            var list = group.OrderBy(r => r.Job.Repeat).ToList();
            int same = list.Count(r => Signature(r) == Signature(list[0]));
            md.AppendLine($"| {group.Key.Layout} | R{group.Key.Round} | {group.Key.Arm} | {list.Count} | {same} / {list.Count} | {string.Join(" · ", list.Select(r => r.Kills))} | " +
                          $"{string.Join(" · ", list.Select(r => r.EchoWait.Count > 0 ? r.EchoWait.Min() + "–" + r.EchoWait.Max() : "–"))} |");
            string first = traces.TryGetValue((group.Key.Layout, group.Key.Round, group.Key.Seed, group.Key.Arm, 0), out string t0) ? t0 : null;
            foreach (Row other in list.Skip(1))
            {
                if (first == null || !traces.TryGetValue((group.Key.Layout, group.Key.Round, group.Key.Seed, group.Key.Arm, other.Job.Repeat), out string t1)) continue;
                string[] a = first.Split('\n'), b = t1.Split('\n');
                int i = 1;   // 0. satır başlıktır (mutlak zaman ve kare; tekrarlar arasında farklıdır)
                while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
                if (i >= a.Length && i >= b.Length) continue;
                details.AppendLine($"### {group.Key.Layout} R{group.Key.Round} {group.Key.Arm}: tekrar 0 ile tekrar {other.Job.Repeat}\n");
                details.AppendLine($"Başlık: `{a[0].Trim()}` / `{b[0].Trim()}`\n");
                details.AppendLine($"İlk ayrışan iz satırı: {i} (öncesindeki {i - 1} satır aynı)\n\n```");
                for (int k = Math.Max(1, i - 4); k < Math.Min(Math.Max(a.Length, b.Length), i + 3); k++)
                    details.AppendLine($"{(k == i ? ">>" : "  ")} [0] {(k < a.Length ? a[k].Trim() : "(bitti)")}\n{(k == i ? ">>" : "  ")} [{other.Job.Repeat}] {(k < b.Length ? b[k].Trim() : "(bitti)")}");
                details.AppendLine("```\n");
            }
        }
        md.AppendLine().Append(details);
        return md.ToString();
    }

    // ---------------------------------------------------------------- geometri (oyunun kendi geometri fonksiyonlarıyla)
    static string GeometryReport()
    {
        var md = new StringBuilder();
        md.AppendLine("# Erişim geometrisi (Bölüm 3.7.5)");
        md.AppendLine();
        md.AppendLine("Hücre sayıları oyunun geometri fonksiyonlarından (`HarvestBehaviorGeometry`) hesaplanır; tarlanın dışında kalan hücreler sayılmaz. 'Ek' = ilk darbenin erişmediği hücreler.");
        md.AppendLine();
        var footprint = new List<GridPosition>(); var first = new List<GridPosition>(); var echo = new List<GridPosition>(); var origins = new List<GridPosition>();
        foreach (int n in new[] { 7, 9 })
        {
            bool Open(GridPosition p) => p.x >= 0 && p.z >= 0 && p.x < n && p.z < n;
            int mid = n / 2;
            var places = new (string name, int sx, int sz, (string where, int x, int z)[] at)[]
            {
                ("1×1", 1, 1, new[] { ("orta", mid, mid), ("kenar", 0, mid), ("köşe", 0, 0) }),
                ("2×2", 2, 2, new[] { ("orta", mid - 1, mid - 1), ("kenar", 0, mid - 1), ("köşe", 0, 0) }),
                ("2×3", 2, 3, new[] { ("orta", mid - 1, mid - 1), ("kenar", 0, mid - 1), ("köşe", 0, 0) }),
            };
            md.AppendLine($"## {n}×{n} tarla · patlama (ilk patlama: saksının dört yönden komşuları; taban yarıçap {N(HarvestBehaviorGeometry.ExplosionRadiusCells)} hücre)");
            md.AppendLine();
            md.AppendLine("| Saksı | Yer | İlk patlama | Artçı 1,50 (×1,25): toplam / ek | Artçı 2,00 (×1,667): toplam / ek | Artçı 2,25 (×1,875): toplam / ek |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var place in places) foreach (var at in place.at)
            {
                footprint.Clear();
                for (int x = 0; x < place.sx; x++) for (int z = 0; z < place.sz; z++) footprint.Add(new GridPosition(at.x + x, at.z + z));
                HarvestBehaviorGeometry.ExplosionCells(footprint, 1f, first);
                var firstOpen = first.Where(Open).ToList();
                var cols = new List<string>();
                foreach (float cells in new[] { 1.5f, 2f, 2.25f })
                {
                    // Oyundaki yol: taban yarıçap × ödülün çarpanı.
                    HarvestBehaviorGeometry.ExplosionCells(footprint, HarvestBehaviorGeometry.ExplosionRadiusCells * (cells / HarvestBehaviorGeometry.ExplosionRadiusCells), echo);
                    var open = echo.Where(Open).ToList();
                    cols.Add($"{open.Count} / {open.Count(c => !firstOpen.Any(f => f.x == c.x && f.z == c.z))}");
                }
                md.AppendLine($"| {place.name} | {at.where} | {firstOpen.Count} | {string.Join(" | ", cols)} |");
            }
            md.AppendLine();
            md.AppendLine($"## {n}×{n} tarla · elektrik (ilk dalga: dört çapraz yönde {HarvestBehaviorGeometry.ElectricReachCells} hücre)");
            md.AppendLine();
            md.AppendLine("| Saksı | Yer | İlk dalga | İkinci dalga 2 hücre: toplam / ek | 3 hücre: toplam / ek | 4 hücre: toplam / ek |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var place in places) foreach (var at in place.at)
            {
                footprint.Clear();
                for (int x = 0; x < place.sx; x++) for (int z = 0; z < place.sz; z++) footprint.Add(new GridPosition(at.x + x, at.z + z));
                HarvestBehaviorGeometry.ElectricCells(footprint, first, origins);
                var firstOpen = first.Where(Open).ToList();
                var cols = new List<string>();
                foreach (int reach in new[] { 2, 3, 4 })
                {
                    HarvestBehaviorGeometry.ElectricCells(footprint, echo, origins, reach);
                    var open = echo.Where(Open).ToList();
                    if (open.Count != open.Select(c => (c.x, c.z)).Distinct().Count()) throw new Exception("electric targets are not unique");
                    cols.Add($"{open.Count} / {open.Count(c => !firstOpen.Any(f => f.x == c.x && f.z == c.z))}");
                }
                md.AppendLine($"| {place.name} | {at.where} | {firstOpen.Count} | {string.Join(" | ", cols)} |");
            }
            md.AppendLine();
        }
        return md.ToString();
    }

    // Bot: oyuncunun saldırı zamanlayıcısı ve gerçek saldırı yolu; nişan, saldırı alanında en çok canlı bitki olan noktadır.
    sealed class Bot : MonoBehaviour
    {
        public int Attacks, FillSamples; public double FillSum, MaxFrameMs;
        MethodInfo attack; PlayerController player; float timer; double lastReal = -1;
        List<Vector3> aims; List<List<GridObject>> aimCells; float cachedRadius = -1f; int[] lastUsed;
        List<PlantSpawner> spawners;

        void Awake()
        {
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        void Build(GridSystem grid, float radius)
        {
            if (aims == null)
            {
                aims = new List<Vector3>();
                int w = GridManager.Instance.GetWidth(), d = GridManager.Instance.GetHeight();
                Vector3? At(int x, int z)
                {
                    var g = x < w && z < d ? grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached() : null;
                    return g != null && !g.IsLocked ? g.transform.position : (Vector3?)null;
                }
                for (int x = 0; x < w; x++) for (int z = 0; z < d; z++)
                {
                    Vector3? c = At(x, z);
                    if (!c.HasValue) continue;
                    aims.Add(c.Value);
                    foreach (var n in new[] { At(x + 1, z), At(x, z + 1), At(x + 1, z + 1) }) if (n.HasValue) aims.Add((c.Value + n.Value) / 2f);
                }
                lastUsed = new int[aims.Count]; for (int a = 0; a < lastUsed.Length; a++) lastUsed[a] = -1;
                cachedRadius = -1f;
                spawners = FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
            }
            if (radius == cachedRadius) return;
            cachedRadius = radius;
            aimCells = aims.Select(a => grid.GetGridObjectsInRadius(a, radius)).ToList();
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round || !RoundManager.Instance.IsRoundActive) return;
            double real = Time.realtimeSinceStartupAsDouble;
            if (lastReal >= 0) MaxFrameMs = Math.Max(MaxFrameMs, (real - lastReal) * 1000.0);
            lastReal = real;
            var rm = RoundManager.Instance; var stats = StatManager.Instance;
            var grid = GridManager.Instance.GetGridSystem();
            float radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player) * RunPower.Rhythm.Next().RadiusMultiplier;
            Build(grid, radius);
            int filled = 0; foreach (var s in spawners) if (s != null && s.GridObject.HasPlantObject()) filled++;
            FillSum += spawners.Count > 0 ? filled / (double)spawners.Count : 0; FillSamples++;
            float interval = Mathf.Max(stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / rm.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            int best = -1, bestCount = 0;
            for (int a = 0; a < aims.Count; a++)
            {
                int count = 0;
                foreach (var g in aimCells[a])
                {
                    var p = g.GetPlantObject();
                    if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) count++;
                }
                if (count > bestCount || (count > 0 && count == bestCount && lastUsed[a] < lastUsed[best])) { bestCount = count; best = a; }
            }
            if (best < 0) return;
            lastUsed[best] = Attacks;
            if (trace != null) trace.AppendLine($"{Frame} saldiri {Attacks} nisan {best} hedef {bestCount} zar {Rng()}");
            attack.Invoke(player, new object[] { aims[best] });
            Attacks++;
        }
    }
}
