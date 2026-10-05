using System;
using System.Collections.Generic;
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

// Batch (izole kopya): Bölüm 3.7.4 KONTROLLÜ ÖLÇÜMÜ — ödülsüz / Bereketli Öğrenim / Davranışa Adanış, aynı düzen ve aynı seed'ler.
// Run oynanmaz: tarla, statlar ve level elle kurulan sabit bir laboratuvar durumudur (ödülün alınabildiği R23 / R26 boss'larından
// sonrası). Bot gerçek saldırı yoluyla vurur (PlayerController.AttackInRadius), XP gerçek hasattan gelir, level kartları
// gerçek kart ekranından alınır. Yalnız üç round (R27, R28, R29) oynanır: bu round'larda kota kontrolü ve boss yoktur.
// Bu bir denge ölçümü DEĞİLDİR ve XP yatırımının uzun vadedeki değerini göstermez (üç round, sabit düzen, tek kart politikası).
//   Tools/run-isolated-verification.ps1 -Method BedelliOdulMeasurement.Run -Full
[InitializeOnLoad]
public static class BedelliOdulMeasurement
{
    const string Key = "BedelliOdulMeasurement";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset";
    const float FrameTime = 1f / 30f;

    // Laboratuvar durumu. Statlar, Bölüm 3.6 taban ölçümünde botların R24 satırlarının tipik değerleridir
    // (Docs/Bolum3-6/olcum/taban: hasar 20–29, saldırı aralığı 1,2–2,2 sn, yarıçap 1,0–2,0, level 22–39, hasat başına 40–50 XP).
    // XP çarpanı: laboratuvarda ağaç, XP ödülü ve nadir bitki payı olmadığı için hasat başına XP o ölçümün yarısı kadardır;
    // ×2 ile o düzeye getirilir (yoksa üç round'da level kazanılmaz ve seçim hakkı sütunları boş kalır).
    const int FirstRound = 27, Rounds = 3, GridSize = 5, StartLevel = 24;
    const float Damage = 25f, Interval = 1.8f, Radius = 1.4f, XpMore = 1f;
    static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
    static readonly string[] Arms = { "odulsuz", "ogrenim", "adanis" };
    // 5×5 tarlada davranış tile'ları: (sütun, satır) → tür ve şans. Diğer 17 saksı davranışsızdır.
    static readonly (int x, int z, string tile, float chance)[] BehaviorTiles =
    {
        (1, 1, "Explosive", .30f), (3, 3, "Explosive", .30f), (1, 3, "Electric", .40f), (3, 1, "Electric", .40f),
        (2, 0, "Tornado", .22f), (2, 4, "Tornado", .22f), (0, 2, "Boomerang", .18f), (4, 2, "Boomerang", .18f),
    };

    sealed class Row
    {
        public int Seed, Repeat, Round, Attacks, DirectHits, BehaviorHits, Kills, LevelStart, LevelEnd, Levels, ChoicesPerLevel, Granted, Cards, Pending, CardScreens;
        public string Arm;
        public long DirectRaw, DirectApplied, BehaviorRaw, BehaviorApplied, Score;
        public readonly long[] TypeRaw = new long[5]; public readonly int[] KillsBy = new int[5];
        public double Xp;
    }

    static readonly List<(int seed, string arm, int repeat)> jobs = new();
    static readonly List<Row> rows = new();
    static int job = -1, phase, guard; static double nextAt, since;
    static Row row; static Bot bot; static int levels0, granted0, cards0; static double xp0; static long score0;

    static BedelliOdulMeasurement() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        profile.bossSeed = 7400; EditorUtility.SetDirty(profile);
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (measurement)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Profile => AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (job < 0)
            {
                foreach (int seed in Seeds) foreach (string arm in Arms) jobs.Add((seed, arm, 0));
                // Tekrarlanabilirlik: ilk seed'in üç kolu ikinci kez çalıştırılır.
                foreach (string arm in Arms) jobs.Add((Seeds[0], arm, 1));
                job = 0; phase = 0; nextAt = now + 2; since = now;
                PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested;
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (now < nextAt) return;
            if (now - since > 300) throw new Exception($"stuck in job {job} phase {phase} state {State}");
            if (phase == 0) { if (!Ready()) { if (guard++ > 3000) throw new Exception("scene did not reach RunSetup"); return; } Setup(); phase = 1; since = now; return; }
            Drive(now);
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static bool Ready() => RM != null && GameManager.Instance != null && State == GameStates.RunSetup && RM.Profile == Profile && GridUnlockManager.Instance != null && Boss != null;

    static BossRewardSO Reward(string id) => RewardOfferLab.All(Profile.bossRewards).First(r => r.id == id);

    static void Setup()
    {
        var (seed, arm, repeat) = jobs[job];
        guard = 0;
        Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
        // Aynı seed'de üç kol aynı zar dizisiyle başlar; diziler yalnız ödülün etkisiyle ayrışır.
        UnityEngine.Random.InitState(9000 + seed);
        GridUnlockManager.Instance.UnlockNextTier(GridSize);
        var cells = RewardOfferLab.OpenCells();
        if (cells.Count != GridSize * GridSize) throw new Exception($"open cells {cells.Count}, expected {GridSize * GridSize}");
        int minX = cells.Min(c => c.GetGridPosition().x), minZ = cells.Min(c => c.GetGridPosition().z);
        foreach (var cell in cells.OrderBy(c => c.GetGridPosition().x * 100 + c.GetGridPosition().z))
        {
            var pos = cell.GetGridPosition();
            var behavior = BehaviorTiles.FirstOrDefault(b => b.x == pos.x - minX && b.z == pos.z - minZ);
            if (behavior.tile != null) RewardOfferLab.Place(cell, RewardOfferLab.Tile(behavior.tile), behavior.chance);
            else RewardOfferLab.Place(cell);
        }
        foreach (var (stat, value) in new[] { (StatType.HarvestDamage, Damage), (StatType.AttackSpeed, Interval), (StatType.AreaRadius, Radius) })
            Stats.AddGlobalModifier(new StatModifier { statType = stat, target = StatTarget.Player, operation = ModifierOperation.Set, value = value });
        Stats.AddGlobalModifier(new StatModifier { statType = StatType.XPGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = XpMore });
        var table = F<ProgressionSO>(Progress, "progressionData");
        SetP(Progress, "CurrentLevel", StartLevel); SetP(Progress, "XPToNextLevel", table.GetXPForLevel(StartLevel));
        if (arm != "odulsuz" && !RewardOfferLab.Grant(Reward(arm == "ogrenim" ? "bereketli_ogrenim" : "davranisa_adanis"))) throw new Exception("reward not granted: " + arm);
        bot = new GameObject("Measurement Bot").AddComponent<Bot>();
        SetP(RM, "CurrentRound", FirstRound - 1); SetF(RM, "awaitingFirstRound", false);
        BeginRound(seed, arm, repeat);
    }

    static void BeginRound(int seed, string arm, int repeat)
    {
        levels0 = RM.LevelsGained; granted0 = RM.CardChoicesGranted; cards0 = RM.CardsTaken; xp0 = Progress.TotalXPEarned; score0 = HarvestScoreManager.Instance.TotalScore;
        row = new Row { Seed = seed, Arm = arm, Repeat = repeat, LevelStart = Progress.CurrentLevel, ChoicesPerLevel = RM.ChoicesPerLevel };
        bot.Attacks = 0;
        RM.StartNextRound();
        if (State != GameStates.Round) throw new Exception($"round did not start ({State}, round {RM.CurrentRound})");
        row.Round = RM.CurrentRound;
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        if (row == null || State != GameStates.Round) return;
        int applied = Mathf.Min(damage, Mathf.Max(0, plant.CurrentHealth));
        row.TypeRaw[(int)type] += damage;
        if (type == DamageType.Direct) { row.DirectHits++; row.DirectRaw += damage; row.DirectApplied += applied; }
        else { row.BehaviorHits++; row.BehaviorRaw += damage; row.BehaviorApplied += applied; }
    }

    static void OnHarvested(PlantHealth plant)
    {
        if (row == null || State != GameStates.Round) return;
        row.Kills++; row.KillsBy[(int)plant.KilledBy]++;
    }

    static void Drive(double now)
    {
        var (seed, arm, repeat) = jobs[job];
        if (State == GameStates.Round) return;
        if (State == GameStates.CardSelection)
        {
            // Kart politikası: her ekranda ilk aday alınır (üç kolda aynı kural).
            var offers = F<List<TileCardOffer>>(Cards, "currentCards");
            if (offers.Count == 0) return;
            if (offers.Count != 3) throw new Exception($"card screen shows {offers.Count} candidates");
            row.CardScreens++;
            Call(Cards, "OnCardSelected", offers[0]);
            since = now;
            return;
        }
        if (State != GameStates.RoundEnd) { if (guard++ > 2000) throw new Exception($"unexpected state {State} after round {row.Round}"); return; }
        guard = 0;
        row.Attacks = bot.Attacks;
        row.Xp = Progress.TotalXPEarned - xp0; row.Levels = RM.LevelsGained - levels0; row.Granted = RM.CardChoicesGranted - granted0;
        row.Cards = RM.CardsTaken - cards0; row.Pending = RM.PendingCardSelections; row.LevelEnd = Progress.CurrentLevel;
        row.Score = HarvestScoreManager.Instance.TotalScore - score0;
        rows.Add(row);
        since = now;
        if (row.Round < FirstRound + Rounds - 1) { BeginRound(seed, arm, repeat); return; }
        row = null;
        job++;
        if (job >= jobs.Count) { Finish(null); return; }
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        phase = 0; nextAt = now + 1.5;
        SceneManager.LoadScene("GameScene");
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested;
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        try { if (ex == null) Write(); }
        catch (Exception write) { ex = write; }
        File.WriteAllText("Logs/BedelliOdulMeasurement.txt", ex == null ? $"DONE: {rows.Count} round rows, {jobs.Count} lab runs" : "FAIL: " + ex);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static string Csv(Row r) => string.Join(",", new object[]
    {
        r.Seed, r.Arm, r.Repeat, r.Round, r.Attacks, r.DirectHits, r.DirectRaw, r.DirectApplied, r.BehaviorHits, r.BehaviorRaw, r.BehaviorApplied,
        r.TypeRaw[(int)DamageType.Explosion], r.TypeRaw[(int)DamageType.Electric], r.TypeRaw[(int)DamageType.Tornado], r.TypeRaw[(int)DamageType.Boomerang],
        r.Kills, r.KillsBy[(int)DamageType.Direct], r.KillsBy[(int)DamageType.Explosion], r.KillsBy[(int)DamageType.Electric], r.KillsBy[(int)DamageType.Tornado], r.KillsBy[(int)DamageType.Boomerang],
        N(r.Xp, "0"), r.LevelStart, r.LevelEnd, r.Levels, r.ChoicesPerLevel, r.Granted, r.CardScreens, r.Cards, r.Pending, r.Score,
    });

    static string ArmName(string arm) => arm == "ogrenim" ? "Bereketli Öğrenim" : arm == "adanis" ? "Davranışa Adanış" : "Ödülsüz";

    static void Write()
    {
        var csv = new StringBuilder("seed,arm,repeat,round,attacks,directHits,directRaw,directApplied,behaviorHits,behaviorRaw,behaviorApplied,explosionRaw,electricRaw,tornadoRaw,boomerangRaw," +
                                    "harvests,harvestDirect,harvestExplosion,harvestElectric,harvestTornado,harvestBoomerang,xp,levelStart,levelEnd,levels,choicesPerLevelAtStart,choicesGranted,cardScreens,cardsTaken,pendingAfter,score\n");
        foreach (Row r in rows) csv.AppendLine(Csv(r));
        File.WriteAllText("Logs/BedelliOdulMeasurement.csv", csv.ToString());

        var main = rows.Where(r => r.Repeat == 0).ToList();
        var md = new StringBuilder();
        md.AppendLine("# Bedelli ödüller — kontrollü kısa karşılaştırma (Bölüm 3.7.4)");
        md.AppendLine();
        md.AppendLine("Üretim: `BedelliOdulMeasurement.Run` (izole kopya). **Denge ölçümü değildir.** Run oynanmadı; sabit bir laboratuvar durumu kuruldu.");
        md.AppendLine();
        md.AppendLine($"- Profil: `Run50_BedelliOdullerV1` · round'lar: R{FirstRound}–R{FirstRound + Rounds - 1} (bu round'larda boss ve kota kontrolü yok) · round süresi {N(Profile.fixedRoundDuration)} sn · kare adımı 1/30 sn");
        md.AppendLine($"- Tarla: {GridSize}×{GridSize} açık, {GridSize * GridSize} tek hücrelik saksı; 8'inin altında davranış tile'ı (2 patlama %30, 2 elektrik %40, 2 kasırga %22, 2 bumerang %18)");
        md.AppendLine($"- Sabitlenen statlar: hasar {N(Damage)}, saldırı aralığı {N(Interval)} sn, yarıçap {N(Radius)}, XP kazancı ×{N(1f + XpMore)}; kritik ve diğer statlar profilin tabanı; ağaç ve başka ödül yok");
        md.AppendLine($"- Başlangıç level'ı {StartLevel} (XP tablosu profilin kendi tablosu); XP gerçek hasattan gelir, level kartları gerçek kart ekranından alınır");
        md.AppendLine("- Nişan: saldırı alanında en çok canlı bitki olan nokta (hücre merkezleri ve ara noktalar) · kart: her ekranda ilk aday");
        md.AppendLine($"- Kollar: ödülsüz · Bereketli Öğrenim · Davranışa Adanış (ödül round başlamadan verilir) · seed'ler: {string.Join(", ", Seeds)} (üç kolda aynı)");
        md.AppendLine();
        md.AppendLine("Hasar: \"vuruş\" = uygulanan vuruşların toplamı (fazlası dahil); \"can\" = bitkinin kalan canını aşmayan kısmı.");
        md.AppendLine();
        md.AppendLine($"## Kol ortalamaları ({Seeds.Length} seed, {Rounds} round toplamı)");
        md.AppendLine();
        md.AppendLine("| Kol | Saldırı | Doğrudan vuruş / can | Doğrudan vuruş başına | Davranış vuruş / can | Davranış vuruşu (adet) | Hasat (doğrudan + davranış) | XP | Level | Verilen seçim hakkı | Alınan kart |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (string arm in Arms)
        {
            var runs = main.Where(r => r.Arm == arm).GroupBy(r => r.Seed).ToList();
            double Avg(Func<Row, double> f) => runs.Average(g => g.Sum(f));
            double hits = Avg(r => r.DirectHits);
            md.AppendLine($"| {ArmName(arm)} | {N(Avg(r => r.Attacks), "0.#")} | {N(Avg(r => r.DirectRaw), "0")} / {N(Avg(r => r.DirectApplied), "0")} | {N(hits > 0 ? Avg(r => r.DirectRaw) / hits : 0, "0.0")} | " +
                          $"{N(Avg(r => r.BehaviorRaw), "0")} / {N(Avg(r => r.BehaviorApplied), "0")} | {N(Avg(r => r.BehaviorHits), "0.#")} | " +
                          $"{N(Avg(r => r.Kills), "0.#")} ({N(Avg(r => r.KillsBy[0]), "0.#")} + {N(Avg(r => r.Kills - r.KillsBy[0]), "0.#")}) | {N(Avg(r => r.Xp), "0")} | {N(Avg(r => r.Levels), "0.##")} | " +
                          $"{N(Avg(r => r.Granted), "0.##")} | {N(Avg(r => r.Cards), "0.##")} |");
        }
        md.AppendLine();
        md.AppendLine($"## Seed başına ({Rounds} round toplamı)");
        md.AppendLine();
        md.AppendLine("| Seed | Kol | Doğrudan vuruş / can | Davranış vuruş / can | Patlama · elektrik · kasırga · bumerang (vuruş) | Hasat (doğrudan + davranış) | XP | Level | Level başına hak | Verilen hak | Alınan kart | Bekleyen |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (int seed in Seeds)
            foreach (string arm in Arms)
            {
                var r = main.Where(x => x.Seed == seed && x.Arm == arm).OrderBy(x => x.Round).ToList();
                md.AppendLine($"| {seed} | {ArmName(arm)} | {r.Sum(x => x.DirectRaw)} / {r.Sum(x => x.DirectApplied)} | {r.Sum(x => x.BehaviorRaw)} / {r.Sum(x => x.BehaviorApplied)} | " +
                              $"{r.Sum(x => x.TypeRaw[1])} · {r.Sum(x => x.TypeRaw[4])} · {r.Sum(x => x.TypeRaw[2])} · {r.Sum(x => x.TypeRaw[3])} | " +
                              $"{r.Sum(x => x.Kills)} ({r.Sum(x => x.KillsBy[0])} + {r.Sum(x => x.Kills - x.KillsBy[0])}) | {N(r.Sum(x => x.Xp), "0")} | {r.Sum(x => x.Levels)} | {r[0].ChoicesPerLevel} | " +
                              $"{r.Sum(x => x.Granted)} | {r.Sum(x => x.Cards)} | {r[r.Count - 1].Pending} |");
            }
        md.AppendLine();
        md.AppendLine("## Tekrarlanabilirlik");
        md.AppendLine();
        int same = 0, total = 0; var diffs = new List<string>();
        foreach (Row again in rows.Where(r => r.Repeat == 1))
        {
            Row first = main.First(r => r.Seed == again.Seed && r.Arm == again.Arm && r.Round == again.Round);
            total++;
            string a = Csv(first), b = Csv(again).Replace($"{again.Seed},{again.Arm},1,", $"{again.Seed},{again.Arm},0,");
            if (a == b) same++; else diffs.Add($"seed {again.Seed} · {ArmName(again.Arm)} · R{again.Round}: ilk {first.Kills} hasat / {first.DirectRaw} doğrudan / {first.BehaviorRaw} davranış → tekrar {again.Kills} / {again.DirectRaw} / {again.BehaviorRaw}");
        }
        md.AppendLine($"İlk seed'in üç kolu ikinci kez çalıştırıldı: {total} round satırından bütün sütunlarıyla aynı çıkan {same}.");
        foreach (string d in diffs) md.AppendLine("- " + d);
        File.WriteAllText("Logs/BedelliOdulMeasurement.md", md.ToString());
    }

    // Bot: oyuncunun saldırı zamanlayıcısı ve gerçek saldırı yolu; nişan, saldırı alanında en çok canlı bitki olan noktadır.
    sealed class Bot : MonoBehaviour
    {
        public int Attacks;
        MethodInfo attack; PlayerController player; float timer;
        List<Vector3> aims; List<List<GridObject>> aimCells; float cachedRadius = -1f; int[] lastUsed;

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
            }
            if (radius == cachedRadius) return;
            cachedRadius = radius;
            aimCells = aims.Select(a => grid.GetGridObjectsInRadius(a, radius)).ToList();
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round) return;
            var rm = RoundManager.Instance; var stats = StatManager.Instance;
            var grid = GridManager.Instance.GetGridSystem();
            float radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player) * RunPower.Rhythm.Next().RadiusMultiplier;
            Build(grid, radius);
            float interval = Mathf.Max(stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / rm.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            int best = -1; int bestCount = 0;
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
            attack.Invoke(player, new object[] { aims[best] });
            Attacks++;
        }
    }
}
