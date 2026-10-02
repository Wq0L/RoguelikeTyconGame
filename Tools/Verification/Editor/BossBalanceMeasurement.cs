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

// Batch (izole kopya): Bölüm 3.4 DENGE ÖLÇÜMÜ (işlev testi değildir; "geçti" demez, sayı verir). GameScene'de, bot oyuncuyla.
// 1) Yarıçap tablosu: ağaç yarıçapı × tırpan × Sis için gerçek vuruşla (AttackInRadius) vurulan bitki sayısı, 5×5 dolu tarla.
// 2) Boss seçimi: başlangıç kombinasyonlarında yedek (kuralsız) ve tekrar sayısı (havuzun kendi seçimi, 1000 seed × 9 boss).
// 3) Boss açık / kapalı: simülatörün temsilî R10/20/30/40 durumları (Deneyimli, Orta; Bölüm 3.2 ile aynı run'lar) × {yok, Don, Sert Kabuk, Sis}.
// 4) Ödül öncesi / sonrası: aynı durumlar × altı ödül (birer adet) ve iki ödülün üçer adedi.
// Her round: tarla bir önceki round'un stoğuyla dolu başlar (bitkiler round'lar arasında kalır), tam round, kare 1/30 sn, aynı seed kümesi.
[InitializeOnLoad]
public static class BossBalanceMeasurement
{
    const string Key = "BossBalanceMeasurement";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Run50_BossPrototip.asset";
    const float FrameTime = 1f / 30f;
    const int Seeds = 6, SimSeeds = 100;
    static readonly int[] Rounds = { 10, 20, 30, 40 };
    static readonly List<string> notes = new();
    static int step; static double nextAt;

    static BossBalanceMeasurement() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_Referans.asset");
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
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
            if (nextAt == 0) nextAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/BossBalanceMeasurement.txt", new[] { $"RUNNING step {step} (state {stateIndex}, index {index})" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 300)
                throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, round {RoundManager.Instance?.CurrentRound}, timeScale {Time.timeScale}");
            double wait = Run(step++);
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/BossBalanceMeasurement.txt", new[] { ex == null ? "DONE (ölçüm; işlev testi değil)" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Note(string m) => notes.Add(m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static RoundManager RM => RoundManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static BossRewardSO Reward(string n) => AssetDatabase.LoadAssetAtPath<BossRewardSO>("Assets/ScriptableObjects/BossRewards/" + n + ".asset");
    static SegmentEventSO Don => AssetDatabase.LoadAssetAtPath<SegmentEventSO>("Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset");
    static HardShellSO Shell => AssetDatabase.LoadAssetAtPath<HardShellSO>("Assets/ScriptableObjects/SegmentEvents/SertKabuk.asset");
    static FogSO Fog => AssetDatabase.LoadAssetAtPath<FogSO>("Assets/ScriptableObjects/SegmentEvents/Sis.asset");
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z)
        .ThenBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z).ToList();
    static PlantHealth PlantOf(PlantSpawner s) { var p = F<GameObject>(s, "spawnedPlant"); return p != null ? p.GetComponent<PlantHealth>() : null; }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return RadiusTable();
            case 1: return PickStatistics();
            case 2: return StartMeasurement();
            default: return MeasureStep();
        }
    }

    // ---------------- 1) yarıçap tablosu ----------------
    static double RadiusTable()
    {
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(5);
        int c = GridManager.Instance.GetWidth() / 2;
        var small = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
        {
            var cell = Grid.GetGridObject(new GridPosition(c + x, c + z));
            var planter = Object.Instantiate(small.prefab);
            planter.transform.position = cell.GetGroundCellCached().transform.position;
            cell.SetPlanterObject(planter);
            var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(small, new List<GridObject> { cell }); cell.SetPlanterBrain(brain);
        }
        SetP(RM, "CurrentRound", 29); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        SetP(RM, "RemainingTime", 1000f);
        Stats.AddGlobalModifier(Mod(StatType.HarvestDamage, StatTarget.Player, 1f, ModifierOperation.Set));
        Stats.AddGlobalModifier(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        Vector3 P(int x, int z) => Grid.GetGridObject(new GridPosition(c + x, c + z)).GetGroundCellCached().transform.position;
        Vector3 center = P(0, 0), edge = (P(0, 0) + P(1, 0)) / 2f, corner = (P(0, 0) + P(1, 1)) / 2f;
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        int Hits(Vector3 aim)
        {
            foreach (var s in Spawners()) { s.RemoveSpawnedPlant(); s.enabled = true; Call(s, "TrySpawnPlant"); }
            var plants = Spawners().Select(PlantOf).Where(p => p != null).ToList();
            Call(player, "AttackInRadius", aim);
            return plants.Count(p => p.CurrentHealth < p.MaxHealth || p.IsDead);
        }
        Note("=== 1) Yarıçap ve gerçek hedef sayısı (5×5 dolu tarla, gerçek AttackInRadius; merkez / iki hücre arası / köşe → en iyi) ===");
        Note($"Sis çarpanı ×{Fog.radiusMultiplier}, seçilme eşiği yarıçap ≥ {Fog.minPlayerRadius}. Dar Kesim ×0,75. Ağaç: Geniş Süpürüş +%30, Geniş Tarama +%30.");
        foreach (var (treeName, tree) in new[] { ("ağaç yok (1,00)", 0f), ("Geniş Süpürüş (+%30)", 0.3f), ("Geniş Tarama (+%60)", 0.6f) })
        foreach (var (scytheName, scythe) in new[] { ("Standart", 0f), ("Dar Kesim", -0.25f) })
        {
            var mods = new List<StatModifier>();
            if (tree > 0f) mods.Add(Mod(StatType.AreaRadius, StatTarget.Player, tree, ModifierOperation.AddPercent));
            if (scythe != 0f) mods.Add(Mod(StatType.AreaRadius, StatTarget.Player, scythe, ModifierOperation.MorePercent));
            foreach (var m in mods) Stats.AddGlobalModifier(m);
            float radius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var off = (Hits(center), Hits(edge), Hits(corner));
            var fog = Mod(StatType.AreaRadius, StatTarget.Player, Fog.radiusMultiplier - 1f, ModifierOperation.MorePercent);
            Stats.AddGlobalModifier(fog);
            float foggy = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var on = (Hits(center), Hits(edge), Hits(corner));
            Stats.RemoveGlobalModifier(fog);
            foreach (var m in mods) Stats.RemoveGlobalModifier(m);
            int bestOff = Math.Max(off.Item1, Math.Max(off.Item2, off.Item3)), bestOn = Math.Max(on.Item1, Math.Max(on.Item2, on.Item3));
            Note($"{treeName} · {scytheName}: yarıçap {radius:0.###} → {off.Item1}/{off.Item2}/{off.Item3} (en iyi {bestOff}) · Sis'te {foggy:0.###} → {on.Item1}/{on.Item2}/{on.Item3} (en iyi {bestOn})" +
                 $" · Sis {(radius >= Fog.minPlayerRadius ? "seçilebilir" : "SEÇİLEMEZ (eşik altı)")} · en iyi nişanda fark {bestOn - bestOff}, merkez nişanda fark {on.Item1 - off.Item1}");
        }
        return .2;
    }

    // ---------------- 2) boss seçimi istatistiği ----------------
    static double PickStatistics()
    {
        var pool = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath).bossPool;
        Note("=== 2) Boss seçimi: 1000 seed × 9 boss, yarıçap run boyunca sabit varsayılır (havuzun kendi seçim kuralı) ===");
        foreach (var (name, radius) in new[] { ("Dar Kesim başlangıcı (0,75)", 0.75f), ("Standart başlangıcı (1,00)", 1f), ("Dar Kesim + Geniş Süpürüş (0,975)", 0.975f),
                                               ("Standart + Geniş Süpürüş (1,30)", 1.3f), ("Standart + Geniş Tarama (1,60)", 1.6f) })
        {
            var context = new SegmentEventContext { PlayerRadius = radius };
            int fallback = 0, repeats = 0; var counts = new Dictionary<string, int>();
            for (int seed = 1; seed <= 1000; seed++)
            {
                SegmentEventSO previous = null;
                for (int segment = 1; segment <= 9; segment++)
                {
                    var pick = pool.Pick(context, previous, new System.Random(SegmentEventDirector.Mix(seed, segment, 0x424F5353)), out bool repeated);
                    if (pick == null) { fallback++; continue; }
                    if (repeated) repeats++;
                    counts.TryGetValue(pick.displayName, out int n); counts[pick.displayName] = n + 1;
                    previous = pick;
                }
            }
            Note($"{name}: yedek (kuralsız) {fallback} / 9000 · tekrar {repeats} / 9000 · dağılım {string.Join(", ", counts.OrderBy(k => k.Key).Select(k => $"{k.Key} %{k.Value / 90.0:0}"))}");
        }
        return .1;
    }

    // ---------------- 3–4) sahne ölçümü ----------------
    sealed class StateConfig { public string Name; public RunSimulator.Snapshot Snap; public RunSimulator.RoundLog Sim; }
    sealed class Variant
    {
        public string Name; public SegmentEventSO Boss; public List<(BossRewardSO reward, int count)> Rewards = new();
        public bool IsBoss => Boss != null;
    }
    sealed class Metrics
    {
        public int kills, direct, behavior, zoneKills, zoneTracked, outTracked, freshHits, freshKills, zoneFresh, zoneFreshKills, attacks, stock, stockHard;
        public double zoneHitSum, outHitSum, fill, xp;
        public long score;
    }

    static RunProfileSO measureProfile;
    static readonly List<StateConfig> states = new();
    static List<Variant> variants;
    static readonly List<(int state, string variant, int seed, Metrics m)> results = new();
    static int stateIndex, phase, index; static bool roundRunning;
    static Bot bot; static Metrics metrics; static long score0; static double xp0;
    static readonly Dictionary<int, string> eligibleNote = new();
    static FogSO forcedFog;
    static long[] realTargets;

    static double StartMeasurement()
    {
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        realTargets = source.bossTargets.ToArray();
        foreach (var pol in new[] { RunSimulator.Policies[0], RunSimulator.Policies[1] })
        {
            var run = RunSimulator.Reference50Representative(pol, SimSeeds);
            foreach (var snap in run.Snapshots.Where(s => Rounds.Contains(s.Round)))
                states.Add(new StateConfig { Name = $"{pol.Name} · R{snap.Round}", Snap = snap, Sim = run.Rounds[snap.Round - 1] });
        }
        measureProfile = Object.Instantiate(source);
        measureProfile.name = "Run50_BossPrototip (ölçüm)";
        measureProfile.runLength = 1000;          // run sonu yok
        measureProfile.bossTargets = new List<long>(); // ölçümde boss hedefi run'ı bitirmez; tamamlanma gerçek tabloya göre hesaplanır
        measureProfile.bossRewards = null;        // ölçümde ödül ekranı açılmaz; ödüller açıkça verilir
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = measureProfile;
        forcedFog = Object.Instantiate(Fog); forcedFog.name = "Sis (zorla)"; forcedFog.minPlayerRadius = 0f;
        variants = new List<Variant>
        {
            new Variant { Name = "boss yok" },
            new Variant { Name = "Don Cephesi", Boss = Don },
            new Variant { Name = "Sert Kabuk", Boss = Shell },
            new Variant { Name = "Sis", Boss = forcedFog },
        };
        foreach (string n in new[] { "KeskinBicak", "HizliBilek", "BereketliToprak", "NadirTohum", "BilgiFilizi", "Kivilcim" })
            variants.Add(new Variant { Name = "ödül: " + Reward(n).displayName, Rewards = { (Reward(n), 1) } });
        variants.Add(new Variant { Name = "ödül: Keskin Bıçak ×3", Rewards = { (Reward("KeskinBicak"), 3) } });
        variants.Add(new Variant { Name = "ödül: Hızlı Bilek ×3", Rewards = { (Reward("HizliBilek"), 3) } });
        // Sert Kabuk hasar yatırımını anlamlı kılıyor mu: aynı boss, doğrudan hasar ×1,52 ile.
        variants.Add(new Variant { Name = "Sert Kabuk + Keskin Bıçak ×3", Boss = Shell, Rewards = { (Reward("KeskinBicak"), 3) } });
        Note($"=== 3–4) Sahne ölçümü: {states.Count} durum × {variants.Count} varyant × {Seeds} seed; tam round, kare 1/30 sn; tarla stokla dolu başlar; bot en çok bitki yakalayan nişanı seçer, eşitlikte en uzun süredir vurulmayanı (insan değil) ===");
        Note("boss hedefleri (profil tablosu, ilk test değerleri): " + string.Join(" ", Rounds.Select(r => $"R{r} {realTargets[r / 5 - 1]}")));
        phase = 0; stateIndex = 0;
        Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double MeasureStep()
    {
        if (phase % 2 == 0) { stateIndex = phase / 2; SetupState(); phase++; index = 0; return .3; }
        if (roundRunning)
        {
            if (State == GameStates.Round) return .25;
            Collect(); roundRunning = false;
        }
        if (index == Seeds * variants.Count)
        {
            Report(stateIndex);
            if (stateIndex < states.Count - 1) { phase++; Time.captureDeltaTime = 0f; SceneManager.LoadScene("GameScene"); return 3; }
            return -1;
        }
        BeginRound(variants[index / Seeds], index % Seeds);
        index++; roundRunning = true;
        return .5;
    }

    static float baseRadius;

    static void SetupState()
    {
        var cfg = states[stateIndex]; var snap = cfg.Snap;
        if (RM.Profile != measureProfile) throw new Exception("measurement profile not active");
        var loadout = StartLoadoutManager.Instance;
        if (loadout == null || loadout.Farmer.id != "bahcivan" || loadout.Scythe.id != "standart") throw new Exception("measurement must run on the neutral start");
        Stats.AddGlobalModifiers(new List<StatModifier>(snap.Global));
        foreach (var (cell, so, mods) in snap.Tiles.OrderBy(t => t.cell.x).ThenBy(t => t.cell.y))
            Grid.GetGridObject(new GridPosition(cell.x, cell.y)).GetGroundCellCached().ApplyModifier(so, new List<StatModifier>(mods));
        foreach (var (so, cells) in snap.Planters)
        {
            int xs = cells.Select(c => c.x).Distinct().Count();
            var objs = cells.Select(c => Grid.GetGridObject(new GridPosition(c.x, c.y))).ToList();
            var planter = Object.Instantiate(so.prefab);
            Vector3 center = Vector3.zero; foreach (var c in objs) center += c.GetGroundCellCached().transform.position;
            planter.transform.position = center / objs.Count;
            planter.transform.rotation = Quaternion.Euler(0f, so.sizeX != so.sizeZ && xs != so.sizeX ? 90f : 0f, 0f);
            foreach (var c in objs) c.SetPlanterObject(planter);
            var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(so, objs);
            foreach (var c in objs) c.SetPlanterBrain(brain);
        }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        bot = new GameObject("Harvest bot (measurement)").AddComponent<Bot>();
        SetF(RM, "awaitingFirstRound", false);
        baseRadius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
        float damage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        if (Math.Abs(damage - cfg.Sim.Damage) > .01) throw new Exception($"{cfg.Name}: scene damage {damage} differs from simulator {cfg.Sim.Damage}");
        // Bu durumda oyunun kendi uygunluk kuralına göre sunulabilecek ödüller (hiç ödül alınmamışken).
        Boss.ClearAll();
        var pool = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath).bossRewards;
        eligibleNote[stateIndex] = string.Join(", ", pool.rewards.Select(r => r.displayName + (Boss.IsEligible(r) ? "" : " (SUNULMAZ)")));
    }

    static Variant current;

    static void BeginRound(Variant v, int seed)
    {
        if (State == GameStates.CardSelection) { int guard = 0; while (RM.OnCardSelectionComplete()) if (guard++ > 200) throw new Exception("cards did not clear"); }
        current = v; metrics = new Metrics();
        int round = states[stateIndex].Snap.Round;
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        Boss.ClearAll();
        foreach (var (reward, count) in v.Rewards) for (int k = 0; k < count; k++) Grant(reward);
        // Boss: tek adaylı havuz (ya da boss yok); yönetici bu round için baştan kurulur. Bölge seed'i seed'e bağlı.
        if (v.IsBoss)
        {
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.entries.Add(new BossPoolSO.Entry { boss = v.Boss, weight = 1f });
            measureProfile.bossPool = pool;
        }
        else measureProfile.bossPool = null;
        measureProfile.bossSeed = 5000 + seed;
        Call(Events, "HandleRunStarted");
        UnityEngine.Random.InitState(1300 + seed);
        var spawners = Spawners();
        // Stok: bir önceki round'un bitkileri tarlada (bitkiler round'lar arasında kalır).
        SetP(RM, "CurrentRound", round - 1);
        foreach (var s in spawners) { s.RemoveSpawnedPlant(); s.enabled = true; Call(s, "TrySpawnPlant"); }
        metrics.stock = spawners.Count(s => PlantOf(s) != null);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception("measurement round did not start: " + State + " " + RM.CurrentRound);
        if (v.IsBoss && (Events.Active == null || Events.Active.Data != v.Boss)) throw new Exception($"{v.Name} not active at round {round}");
        if (!v.IsBoss && Events.Active != null) throw new Exception("unexpected boss");
        metrics.stockHard = spawners.Select(PlantOf).Count(h => h != null && h.HealthMultiplier != 1f);
        foreach (var s in spawners) SetF(s, "timer", UnityEngine.Random.Range(0f, (float)Call(s, "GetEffectiveSpawnInterval")));
        bot.Begin(spawners);
        score0 = Score; xp0 = ProgressionManager.Instance.TotalXPEarned;
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void Grant(BossRewardSO reward)
    {
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetP(Boss, "IsPending", true);
        // Ölçümde ödül uygunluk denetimi atlanarak verilir; uygulanmadıysa ölçüm sessizce "etkisiz" göstermesin.
        if (!Boss.Choose(reward)) throw new Exception("measurement could not grant " + reward.displayName);
    }

    static void Collect()
    {
        metrics.kills = bot.Kills.Sum(); metrics.direct = bot.Kills[0]; metrics.behavior = metrics.kills - metrics.direct;
        metrics.zoneKills = bot.ZoneKills; metrics.zoneTracked = bot.ZoneTracked; metrics.outTracked = bot.OutTracked;
        metrics.zoneHitSum = bot.ZoneHitSum; metrics.outHitSum = bot.OutHitSum;
        metrics.freshHits = bot.FreshHits; metrics.freshKills = bot.FreshKills; metrics.zoneFresh = bot.ZoneFresh; metrics.zoneFreshKills = bot.ZoneFreshKills;
        metrics.attacks = bot.Attacks; metrics.fill = bot.FillSamples > 0 ? bot.FillSum / bot.FillSamples : 0;
        metrics.score = Score - score0; metrics.xp = ProgressionManager.Instance.TotalXPEarned - xp0;
        results.Add((stateIndex, current.Name, (index - 1) % Seeds, metrics));
        Boss.ClearAll();
        if (State == GameStates.CardSelection) { int guard = 0; while (RM.OnCardSelectionComplete()) if (guard++ > 200) throw new Exception("cards did not clear"); }
    }

    static string Stat(IEnumerable<double> values, string format = "0")
    {
        var v = values.ToList();
        return $"{v.Average().ToString(format)} [{v.Min().ToString(format)}–{v.Max().ToString(format)}]";
    }

    static string Rel(List<Metrics> a, List<Metrics> b, Func<Metrics, double> f)
    {
        double x = a.Sum(f), y = b.Sum(f);
        return y <= 0 ? "-" : $"{(x / y - 1) * 100:+0.0;-0.0;0.0}%";
    }

    static void Report(int s)
    {
        var cfg = states[s]; int round = cfg.Snap.Round; long target = realTargets[round / 5 - 1];
        var rows = results.Where(r => r.state == s).ToList();
        var baseline = rows.Where(r => r.variant == "boss yok").OrderBy(r => r.seed).Select(r => r.m).ToList();
        float interval = Mathf.Max(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RM.TempoMultiplier;
        Note($"--- {cfg.Name}: hasar {Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player):0.#} · saldırı aralığı {interval:0.###} sn · yarıçap {baseRadius:0.##} · " +
             $"{cfg.Snap.Planters.Count} saksı / {Spawners().Count} üretim noktası · boss hasadı hedefi {target} · Sis bu yarıçapta {(baseRadius >= Fog.minPlayerRadius ? "seçilebilir" : "SEÇİLEMEZ (ölçüm için zorlandı)")} ---");
        Note("oyunun uygunluk kuralı (ölçümde ödüller zorla verilir): " + eligibleNote[s]);
        Note("varyant | hasat (doğrudan/davranış) | round skoru | hedefi geçen | boss yok'a göre hasat · skor | tek vuruş | bölge: hasat, vuruş/öldürme (bölge · dışı), tek vuruş | stok (sertleşen) | doluluk | XP (boss yok'a göre)");
        foreach (string name in variants.Select(v => v.Name))
        {
            var m = rows.Where(r => r.variant == name).OrderBy(r => r.seed).Select(r => r.m).ToList();
            if (m.Count == 0) continue;
            double A(Func<Metrics, double> f) => m.Average(f);
            int zt = m.Sum(x => x.zoneTracked), ot = m.Sum(x => x.outTracked), zf = m.Sum(x => x.zoneFresh), ff = m.Sum(x => x.freshHits);
            string zone = zt + m.Sum(x => x.zoneKills) > 0
                ? $"{A(x => x.zoneKills):0}, {(zt > 0 ? m.Sum(x => x.zoneHitSum) / zt : double.NaN):0.00} · {(ot > 0 ? m.Sum(x => x.outHitSum) / ot : double.NaN):0.00}, %{(zf > 0 ? 100.0 * m.Sum(x => x.zoneFreshKills) / zf : double.NaN):0}"
                : "-";
            Note($"{name} | {Stat(m.Select(x => (double)x.kills))} ({A(x => x.direct):0}/{A(x => x.behavior):0}) | {Stat(m.Select(x => (double)x.score))} | {m.Count(x => x.score >= target)}/{m.Count} | " +
                 $"{(name == "boss yok" ? "taban" : Rel(m, baseline, x => x.kills) + " · " + Rel(m, baseline, x => x.score))} | %{(ff > 0 ? 100.0 * m.Sum(x => x.freshKills) / ff : 0):0} | {zone} | {A(x => x.stock):0} ({A(x => x.stockHard):0}) | %{A(x => x.fill * 100):0} | {A(x => x.xp):0} ({(name == "boss yok" ? "taban" : Rel(m, baseline, x => x.xp))})");
        }
    }

    // Bot: Bölüm 3.2 ile aynı nişan seçimi (hücre merkezi, iki hücre arası, köşe; en çok canlı bitki). Ek olarak bölge içi / dışı sayaçlar.
    sealed class Bot : MonoBehaviour
    {
        public readonly int[] Kills = new int[5];
        public int Attacks, FreshHits, FreshKills, ZoneFresh, ZoneFreshKills, ZoneKills, ZoneTracked, OutTracked, FillSamples;
        public double ZoneHitSum, OutHitSum, FillSum;
        float timer;
        MethodInfo attack; PlayerController player;
        List<PlantSpawner> spawners = new();
        readonly List<(PlantHealth h, bool fresh, bool zone)> scratch = new();
        readonly Dictionary<PlantHealth, int> tracked = new();
        List<Vector3> aims; List<List<GridObject>> aimCells; float cachedRadius = -1f; int[] lastUsed;

        void OnEnable() { PlantHealth.AnyHarvested += Count; }
        void OnDisable() { PlantHealth.AnyHarvested -= Count; }

        static bool InZone(PlantHealth h)
        {
            var active = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            return active != null && active.Covers(GridManager.Instance.GetGridSystem().GetGridPosition(h.transform.position));
        }

        void Count(PlantHealth h)
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            Kills[(int)h.KilledBy]++;
            if (InZone(h)) ZoneKills++;
            if (h.KilledBy != DamageType.Direct) tracked.Remove(h);
        }

        public void Begin(List<PlantSpawner> list)
        {
            Array.Clear(Kills, 0, 5);
            Attacks = FreshHits = FreshKills = ZoneFresh = ZoneFreshKills = ZoneKills = ZoneTracked = OutTracked = FillSamples = 0;
            ZoneHitSum = OutHitSum = FillSum = 0; timer = 0; spawners = list; tracked.Clear(); aims = null; cachedRadius = -1f; lastUsed = null;
        }

        void BuildAims(GridSystem grid, float radius)
        {
            if (aims == null)
            {
                aims = new List<Vector3>();
                int w = GridManager.Instance.GetWidth(), d = GridManager.Instance.GetHeight();
                Vector3? At(int x, int z) { var g = x < w && z < d ? grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached() : null; return g != null ? g.transform.position : null; }
                for (int x = 0; x < w; x++) for (int z = 0; z < d; z++)
                {
                    var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                    if (ground == null || ground.IsLocked) continue;
                    Vector3 c = ground.transform.position;
                    aims.Add(c);
                    foreach (var n in new[] { At(x + 1, z), At(x, z + 1), At(x + 1, z + 1) }) if (n.HasValue) aims.Add((c + n.Value) / 2f);
                }
            }
            if (radius != cachedRadius) { cachedRadius = radius; aimCells = aims.Select(a => grid.GetGridObjectsInRadius(a, radius)).ToList(); }
        }

        void Update()
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            SetF(RoundManager.Instance, "pendingCardSelections", 0);
            int filled = 0; foreach (var s in spawners) if (s != null && F<GameObject>(s, "spawnedPlant") != null) filled++;
            FillSum += spawners.Count > 0 ? filled / (double)spawners.Count : 0; FillSamples++;
            if (player == null) { player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include); attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance); }
            float interval = Mathf.Max(StatManager.Instance.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RoundManager.Instance.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            float radius = StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var grid = GridManager.Instance.GetGridSystem();
            BuildAims(grid, radius);
            // Eşit sayıda bitki yakalayan nişanlar arasında en uzun süredir vurulmayan seçilir (tarlayı dolaşır).
            // İlk koşuda bağ "listede ilk" ile kırılıyordu: dolu tarlada bot hep aynı köşede kalıp davranış saksılarına hiç ulaşmıyor,
            // boss açık / kapalı farkını nişan sırası belirliyordu (Logs/BossBalanceMeasurement_ilkNisan.txt).
            if (lastUsed == null || lastUsed.Length != aims.Count) { lastUsed = new int[aims.Count]; for (int a = 0; a < lastUsed.Length; a++) lastUsed[a] = -1; }
            Vector3 best = Vector3.zero; int bestScore = 0, bestAim = -1;
            for (int a = 0; a < aims.Count; a++)
            {
                int score = 0;
                foreach (var g in aimCells[a])
                {
                    var p = g.GetPlantObject();
                    if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) score++;
                }
                if (score > bestScore || (score == bestScore && score > 0 && lastUsed[a] < lastUsed[bestAim])) { bestScore = score; best = aims[a]; bestAim = a; }
            }
            if (bestScore <= 0) return;
            lastUsed[bestAim] = Attacks;
            scratch.Clear();
            foreach (var g in grid.GetGridObjectsInRadius(best, radius))
            {
                var p = g.GetPlantObject();
                if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) scratch.Add((h, h.CurrentHealth >= h.MaxHealth, InZone(h)));
            }
            attack.Invoke(player, new object[] { best });
            Attacks++;
            foreach (var (h, fresh, zone) in scratch)
            {
                if (fresh) { FreshHits++; if (zone) ZoneFresh++; }
                int hits = (tracked.TryGetValue(h, out int n) ? n : 0) + 1;
                if (h == null) { tracked.Remove(h); continue; }
                if (!h.IsDead) { tracked[h] = hits; continue; }
                tracked.Remove(h);
                if (h.KilledBy != DamageType.Direct) continue;
                if (zone) { ZoneTracked++; ZoneHitSum += hits; } else { OutTracked++; OutHitSum += hits; }
                if (fresh && hits == 1) { FreshKills++; if (zone) ZoneFreshKills++; }
            }
        }
    }
}
