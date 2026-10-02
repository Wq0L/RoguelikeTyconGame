using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.2 — Run50_Referans profili ve 50 round referans ölçümü, GameScene'de.
// A) Profil: asset ayarları (uzun run'ın ilk 50 round'uyla aynı kota eğrisi), diğer profillerin değerleri, menü kaydı.
// B) Akış: 50 round, 10 segment geçişi, eşit skorla R50 zaferi, R20'de kota kaybı, yeniden başlatma, ana menü.
// C) Sahne ölçümü: simülatörün temsilî R10/20/30/40/50 durumları (Deneyimli ve Orta politika; simülatör durumu, insan verisi değil)
//    ve bir uç durum (tam ağaç, normal R50 build'i sayılmaz). Bot oyuncu, sabit kare (1/30 sn), durum başına 5 seed, tam round.
//    Ölçüm kopyası profilde kota değerlendirmesi kapalıdır (her seferinde tek round ölçülür); oyuncu akışına eklenmez.
[InitializeOnLoad]
public static class Run50ReferenceVerification
{
    const string Key = "Run50ReferenceVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string ProfilePath = Profiles + "Run50_Referans.asset";
    const string MenuPath = "Tools/Run Profili/Run50 Referans · 50 round (ölçüm tabanı)";
    const float FrameTime = 1f / 30f;
    const int Seeds = 5, SimSeeds = 100;
    static readonly long[] Expected = { 10, 15, 20, 30, 45, 65, 95, 130, 200, 280 };
    static readonly List<string> notes = new();
    static int step; static double nextAt;

    static Run50ReferenceVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: Run50_Referans seçilir.
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
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
            // İlerleme dosyası: takılan adım görünsün. Aynı adım gerçek zamanda uzun sürerse durum bilgisiyle durur.
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/Run50ReferenceVerification.txt", new[] { $"RUNNING step {step} (config {config}, index {index})" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240)
                throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, round {RoundManager.Instance?.CurrentRound}, timeScale {Time.timeScale}, time {Time.timeAsDouble:0.0}");
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
        File.WriteAllLines("Logs/Run50ReferenceVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", "<[^>]+>", "").Replace('\n', '/');
    static RoundManager RM => RoundManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static int Res(ResourceType t) => ResourceManager.Instance.GetResourceAmount(t);
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static void SetScore(long value) => SetF(HarvestScoreManager.Instance, "totalScore", value);
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    // Sabit sıra: başlangıç zamanlayıcıları seed'den bu sırayla dağıtılır; sıra değişirse koşudan koşuya sonuç değişir.
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z)
        .ThenBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z).ToList();

    static void Flush()
    {
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        int guard = 0;
        while (State == GameStates.CardSelection)
        {
            if (guard++ > 60) throw new Exception("card selection did not finish (state " + State + ")");
            Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]);
        }
    }

    static void SkipCards()
    {
        int guard = 0;
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) if (guard++ > 200) throw new Exception("pending cards did not clear");
    }

    static string RunEndText()
    {
        var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        return Strip(complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return ProfileAndMenu();
            case 1: return SceneSetup();
            case 2: return Flow50();
            case 3: return VictoryScreen();
            case 4: return RestartAndDefeat();
            case 5: return DefeatScreen();
            case 6: return MenuCheck();
            case 7: return StartMeasurement();
            default: return MeasureStep();
        }
    }

    // ---------------- A) profil ve menü ----------------
    static double ProfileAndMenu()
    {
        var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        var longRun = Profile("UzunRun130");
        Require(p != null && p.name == "Run50_Referans" && p.displayName == "Run50 Referans · 50 round", "Run50_Referans asset exists: " + p?.displayName);
        Require(p.runLength == 50 && p.segmentRounds == 5 && p.runLength / p.segmentRounds == 10, "50 rounds in ten 5-round segments");
        Require(p.segmentTargets.Count == 0 && p.quotaStart == longRun.quotaStart && p.quotaGrowth == longRun.quotaGrowth &&
                p.quotaStart == HarvestQuota.DefaultStart && p.quotaGrowth == HarvestQuota.DefaultGrowth,
            $"Quota source: same curve as UzunRun130 (start {p.quotaStart}, growth {p.quotaGrowth}, no table)");
        var targets = Enumerable.Range(1, 10).Select(s => p.TargetFor(s)).ToArray();
        Require(targets.SequenceEqual(Expected) && Enumerable.Range(1, 10).All(s => longRun.TargetFor(s) == targets[s - 1] && HarvestQuota.Target(s, HarvestQuota.DefaultStart, HarvestQuota.DefaultGrowth) == targets[s - 1]),
            "Segment quotas equal UzunRun130's first 10 segments: " + string.Join("/", targets));
        Require(p.events.Count == 0 && p.specializationAfterSegment == 0 && p.specializationOptions.Count == 0, "No segment events, no specialization (explicit)");
        Require(p.startingGold == 80 && p.startingIron == 0 && p.startingStone == 0 && !p.debugBudget, "Normal starting economy 80 / 0 / 0, no debug budget");
        Require(p.fixedRoundDuration == 0f && p.electricMode == ElectricTriggerMode.KillChance, "Duration from upgrades (no fixed duration); kill-trigger electric");
        Require(p.victoryTitle == "REFERANS RUN TAMAMLANDI", "Victory title");

        // Diğer profiller Bölüm 3.2 öncesi değerlerinde.
        var proto = Profile("Prototip10"); var debug = Profile("Prototip10_DebugButce"); var spec = Profile("Uzmanlasma20"); var exp = Profile("Deney22_20");
        Require(proto.runLength == 10 && proto.segmentTargets.SequenceEqual(new long[] { 40, 200 }) && proto.events.Count == 1 && proto.events[0].segment == 2 && proto.startingGold == 80 && !proto.debugBudget,
            "Prototip10 unchanged (10 rounds, 40/200, Don segment 2, 80 Gold)");
        Require(debug.runLength == 10 && debug.debugBudget && debug.startingGold == 200000, "Prototip10_DebugButce unchanged (debug budget)");
        foreach (var s in new[] { spec, exp })
            Require(s.runLength == 20 && s.segmentTargets.SequenceEqual(new long[] { 40, 200, 350, 600 }) && s.events.Count == 2 && s.specializationAfterSegment == 2 && s.specializationOptions.Count == 3 && s.fixedRoundDuration == 0f,
                $"{s.name} unchanged (20 rounds, 40/200/350/600, Don 2+4, specialization after segment 2)");
        Require(longRun.runLength == 130 && longRun.segmentRounds == 5 && longRun.segmentTargets.Count == 0 && longRun.events.Count == 0 && longRun.specializationAfterSegment == 0,
            "UzunRun130 unchanged (130 rounds, curve, no events)");

        // Menü: yeni seçenek, seçim ve etiket; eski seçenekler yerinde.
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SelectMany(m => m.GetCustomAttributes<MenuItem>().Select(a => (method: m, attr: a))).ToList();
        Require(items.Any(x => x.attr.menuItem == MenuPath && !x.attr.validate && x.method.Name == "Reference50") && items.Any(x => x.attr.menuItem == MenuPath && x.attr.validate),
            "Menu item and validator: " + MenuPath);
        string[] old = { "Prototip · 10 round (normal ekonomi)", "Prototip · 10 round (debug bütçe)", "Uzmanlaşma testi · 20 round (normal ekonomi)", "Deney 2.2 · 20 round (süre deneyi)", "Uzun run · 130 round (normal ekonomi)", "Profil yok (sahnedeki ayarlar)" };
        Require(old.All(o => items.Count(x => x.attr.menuItem == "Tools/Run Profili/" + o) == 2), "Previous profile menu items still present");
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        MethodInfo M(string n) => typeof(RunProfileMenu).GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        M("LongRun").Invoke(null, null);
        Require(selection.active == longRun, "Menu: long run selects UzunRun130");
        M("Reference50").Invoke(null, null);
        Require(selection.active == p, "Menu: Run50 Referans selects Run50_Referans.asset");
        Require((string)M("Label").Invoke(null, new object[] { ProfilePath }) == "Run50 Referans · 50 round (ölçüm tabanı)" &&
                (string)M("Label").Invoke(null, new object[] { Profiles + "UzunRun130.asset" }) == "Uzun run · 130 round (normal ekonomi)", "Menu labels: Run50 and long run distinct");
        return .1;
    }

    static double SceneSetup()
    {
        var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        Require(State == GameStates.RunSetup && RM.Profile == p && RM.MaxRounds == 50 && RM.QuotaEnabled && RM.QuotaSegmentRounds == 5, "GameScene runs Run50_Referans: 50 rounds, 5-round quota segments");
        Require(Enumerable.Range(1, 10).All(s => RM.QuotaTargetFor(s) == Expected[s - 1]), "RoundManager quota targets 10…280 from the profile");
        Require(Res(ResourceType.Gold) == 80 && Res(ResourceType.Iron) == 0 && Res(ResourceType.Stone) == 0, "Starting resources 80 / 0 / 0");
        Require(Events != null && Events.Events.Count == 0 && Events.Active == null && Events.Upcoming == null, "No segment event scheduled");
        Require(SpecializationManager.Instance != null && SpecializationManager.Instance.Chosen == null && !SpecializationManager.Instance.IsPending && !RM.IsRoundChoicePending, "No specialization offered");
        Require(!RM.FixedRoundDuration && Mathf.Approximately(RM.RawRoundDuration, 30f) && Mathf.Approximately(RM.TempoMultiplier, 1f), "Round duration from stats: 30 s base, tempo 1");
        Require(NeutralLoadout() && MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly,
            "Reference start is the neutral Bahçıvan + Standart (batch save is memory-only: the player's selection cannot leak in)");
        return .2;
    }

    // Bölüm 3.3: referans ölçümü açıkça nötr başlangıçla çalışır (etki yok, katsayılar 1).
    static bool NeutralLoadout()
    {
        var loadout = StartLoadoutManager.Instance;
        return loadout != null && loadout.Farmer != null && loadout.Farmer.id == "bahcivan" && loadout.Scythe != null && loadout.Scythe.id == "standart" &&
               loadout.Farmer.IsNeutral && loadout.Scythe.IsNeutral && loadout.OwnedModifiers.Count == 0 && StartLoadoutManager.DirectDamageMultiplier == 1f &&
               StartLoadoutManager.HarvestResourceMultiplier == 1f && StartLoadoutManager.HarvestScoreMultiplier == 1f;
    }

    // ---------------- B) akış ----------------
    static double Flow50()
    {
        int judged = 0;
        for (int r = 1; r <= 50; r++)
        {
            RM.StartNextRound();
            if (RM.CurrentRound != r || State != GameStates.Round) throw new Exception($"round {r} did not start (round {RM.CurrentRound}, state {State})");
            int seg = (r - 1) / 5 + 1;
            if ((r - 1) % 5 == 0)
            {
                if (RM.QuotaSegment != seg || RM.QuotaProgress != 0 || RM.QuotaTarget != Expected[seg - 1] || RM.QuotaSegmentEnd != seg * 5)
                    throw new Exception($"segment {seg} start: segment {RM.QuotaSegment}, progress {RM.QuotaProgress}, target {RM.QuotaTarget}, end {RM.QuotaSegmentEnd}");
                string intro = GameFeelDirector.QuotaIntro(RM);
                if (intro != $"KOTA {HarvestQuota.Format(Expected[seg - 1])} · 5 ROUND") throw new Exception($"round {r} intro: {intro}");
            }
            if (Events.Active != null || Events.Upcoming != null || RM.IsRoundChoicePending) throw new Exception($"round {r}: unexpected event or round choice");
            if (r % 5 == 0) SetScore(Score - RM.QuotaProgress + Expected[seg - 1]); // eşitlik geçer
            Call(RM, "EndRound");
            if (r % 5 == 0)
            {
                if (RM.EndedByQuota || RM.LastQuotaRound != r || RM.LastQuotaTarget != Expected[seg - 1] || RM.LastQuotaScore != Expected[seg - 1])
                    throw new Exception($"round {r} quota: ended {RM.EndedByQuota}, round {RM.LastQuotaRound}, {RM.LastQuotaScore}/{RM.LastQuotaTarget}");
                judged++;
            }
            else if (RM.LastQuotaRound != r / 5 * 5) throw new Exception($"round {r}: quota judged mid-segment ({RM.LastQuotaRound})");
            if (r < 50)
            {
                Flush();
                if (State != GameStates.RoundEnd) throw new Exception($"round {r} end state {State} (expected RoundEnd, no round choice)");
            }
        }
        Require(judged == 10, "10 segment ends judged exactly at rounds 5, 10, …, 50; never mid-segment; no event or specialization screen");
        Require(RM.Outcome == RunOutcome.Victory && State == GameStates.RunComplete && RM.CurrentRound == 50, "Round 50 with quota 280 / 280 → victory, run complete");
        return 1.5;
    }

    static double VictoryScreen()
    {
        string text = RunEndText();
        Require(text.StartsWith("REFERANS RUN TAMAMLANDI") && text.Contains("50 round · son kota 280 / 280"), "Victory screen: " + text);
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static double RestartAndDefeat()
    {
        Require(State == GameStates.RunSetup && RM.CurrentRound == 1 && Score == 0 && Res(ResourceType.Gold) == 80 && Res(ResourceType.Iron) == 0 && Res(ResourceType.Stone) == 0,
            "Restart: fresh run in RunSetup, score 0, 80 / 0 / 0");
        for (int r = 1; r <= 20; r++)
        {
            RM.StartNextRound();
            int seg = (r - 1) / 5 + 1;
            if (r % 5 == 0) SetScore(Score - RM.QuotaProgress + (r == 20 ? Expected[seg - 1] - 1 : Expected[seg - 1] + 3));
            Call(RM, "EndRound");
            if (r < 20) { Flush(); if (RM.EndedByQuota) throw new Exception("ended early at " + r); }
        }
        Require(RM.EndedByQuota && RM.Outcome == RunOutcome.QuotaFailed && State == GameStates.RunComplete && RM.LastQuotaRound == 20 && RM.LastQuotaScore == 29 && RM.LastQuotaTarget == 30,
            "Segment 4 at 29 / 30 → run lost at round 20 (segments 1–3 passed above target)");
        return 1.5;
    }

    static double DefeatScreen()
    {
        string text = RunEndText();
        Require(text.StartsWith("KOTA TUTMADI") && text.Contains("Round 20 · segment skoru 29 / 30"), "Defeat screen: " + text);
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static double MenuCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu, "Main menu button loads MenuScene");
        Require(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length == 1, "Single GameManager after returning to the menu");
        return .1;
    }

    // ---------------- C) sahne ölçümü ----------------
    sealed class Config { public string Name; public RunSimulator.Snapshot Snap; public RunSimulator.RoundLog Sim; public List<StatModifier> Global; }
    sealed class SetupInfo
    {
        public int Round, Planters, Spawners, Tiles, Unlocked;
        public float Damage, Interval, Radius, Crit, CritMult, Raw, Effective, Tempo;
        public double Ceiling; public int[] Hp;
    }
    sealed class Metrics
    {
        public int[] kills = new int[5], rarity = new int[5], freshHits = new int[5], freshKills = new int[5], tracked = new int[5];
        public double[] hitSum = new double[5], timeSum = new double[5];
        public int attacks, idle, hits; public long score; public double xp; public int gold, iron, stone; public double fill;
        public int Kills => kills.Sum();
        public int Behavior => Kills - kills[0];
    }

    static RunProfileSO measureProfile;
    static readonly List<Config> configs = new();
    static readonly List<(int config, int seed, Metrics m)> results = new();
    static readonly Dictionary<int, SetupInfo> infos = new();
    static int config, phase, index; static bool roundRunning;
    static Bot bot; static SetupInfo info;
    static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
    static readonly string[] DamageNames = { "doğrudan", "patlama", "kasırga", "bumerang", "elektrik" };

    static double StartMeasurement()
    {
        var sets = new[] { RunSimulator.Policies[0], RunSimulator.Policies[1] }.Select(pol => (pol, run: RunSimulator.Reference50Representative(pol, SimSeeds))).ToList();
        configs.Clear();
        foreach (var (pol, run) in sets)
        {
            Require(run.Snapshots.Select(s => s.Round).SequenceEqual(RunSimulator.Checkpoints) && run.Rounds.Count == 50,
                $"{pol.Name}: representative simulator run (condition B, seed {run.Snapshots[0].Seed} of {SimSeeds}; quota {(run.QuotaFailedAt == 0 ? "passed every segment, same run under A" : "failed at R" + run.QuotaFailedAt)})");
            foreach (var s in run.Snapshots)
            {
                configs.Add(new Config { Name = $"{pol.Name} · R{s.Round}", Snap = s, Sim = run.Rounds[s.Round - 1], Global = s.Global });
                Note($"durum {pol.Name} R{s.Round}: {s.Global.Count} skill etkisi, {s.Planters.Count} saksı ({string.Join(", ", s.Planters.GroupBy(q => q.so.name).Select(g => $"{g.Count()}×{g.Key.Replace("GrassPlanter ", "")}"))}), " +
                     $"{s.Tiles.Count} tile ({string.Join(", ", s.Tiles.GroupBy(t => t.so.modifierType).Select(g => $"{g.Key} {g.Count()}"))}), banka {s.Gold:0}G/{s.Iron:0}I/{s.Stone:0}S");
            }
        }
        var late = sets[0].run.Snapshots[^1];
        configs.Add(new Config { Name = "Uç durum · tam ağaç (Deneyimli R50 saksı ve tile'ları; normal build değil)", Snap = late, Sim = null, Global = RunSimulator.FullTreeEffects() });
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        measureProfile = Object.Instantiate(source);
        measureProfile.name = "Run50_olcum";
        measureProfile.segmentRounds = 1000; measureProfile.runLength = 1000; // tek round ölçümü: kota değerlendirmesi ve run sonu yok
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = measureProfile;
        Note($"=== SAHNE ÖLÇÜMÜ: {configs.Count} durum × {Seeds} seed, tam round, kare 1/30 sn; bot her saldırıda en çok canlı bitkiyi kapsayan nişana vurur (merkez, hücre arası, köşe; insan isabeti değil) ===");
        phase = 0; config = 0;
        Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double MeasureStep()
    {
        if (phase % 2 == 0) { config = phase / 2; SetupConfig(); phase++; index = 0; return .3; }
        if (roundRunning)
        {
            if (State == GameStates.Round) return .25;
            Collect(); roundRunning = false;
        }
        if (index == Seeds)
        {
            Report(config);
            if (config < configs.Count - 1) { phase++; Time.captureDeltaTime = 0f; SceneManager.LoadScene("GameScene"); return 3; }
            Summary();
            return -1;
        }
        BeginRound(index);
        index++; roundRunning = true;
        return .5;
    }

    static void SetupConfig()
    {
        var cfg = configs[config]; var snap = cfg.Snap;
        Require(RM.Profile == measureProfile && GridManager.Instance.GetWidth() == 11 && GridManager.Instance.GetHeight() == 11, $"Measurement scene ({cfg.Name}): measurement profile copy, 11×11 grid");
        if (!NeutralLoadout()) throw new Exception("measurement scene is not on the neutral Bahçıvan + Standart start");
        Stats.AddGlobalModifiers(new List<StatModifier>(cfg.Global));
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
        bot = new GameObject("Harvest bot (verification)").AddComponent<Bot>();
        SetF(RM, "awaitingFirstRound", false);

        var health = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        var plants = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset").spawnTable.Select(e => e.plant).Where(p => p != null)
            .GroupBy(p => p.rarity).Select(g => g.First()).OrderBy(p => p.rarity).ToList();
        int unlocked = 0;
        for (int x = 0; x < 11; x++) for (int z = 0; z < 11; z++) { var g = Grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && !g.IsLocked) unlocked++; }
        var spawners = Spawners();
        info = new SetupInfo
        {
            Round = snap.Round, Planters = snap.Planters.Count, Spawners = spawners.Count, Tiles = snap.Tiles.Count, Unlocked = unlocked,
            Damage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), Radius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player),
            Interval = Mathf.Max(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RM.TempoMultiplier,
            Crit = Mathf.Clamp01(Stats.GetFinalStat(StatType.CritChance, StatTarget.Player)), CritMult = Stats.GetFinalStat(StatType.CritMultiplier, StatTarget.Player),
            Raw = RM.RawRoundDuration, Effective = RM.EffectiveRoundDuration, Tempo = RM.TempoMultiplier,
            Ceiling = spawners.Sum(s => RM.EffectiveRoundDuration / Math.Max(1e-3, (float)Call(s, "GetEffectiveSpawnInterval"))),
            Hp = plants.Select(p => health.Calculate(p, snap.Round)).ToArray()
        };
        infos[config] = info;
        Require(info.Spawners > 0 && unlocked >= snap.Planters.Sum(p => p.cells.Count), $"{cfg.Name}: {info.Planters} planters, {info.Spawners} production points, {unlocked} open cells");
        if (cfg.Sim != null)
            Require(Math.Abs(info.Damage - cfg.Sim.Damage) < .01 && Math.Abs(info.Interval - cfg.Sim.Interval) < .001 && Math.Abs(info.Effective - cfg.Sim.Duration) < .01 && Math.Abs(info.Tempo - cfg.Sim.Tempo) < .001,
                $"{cfg.Name}: scene reproduces the simulator state (damage {info.Damage:0.#}, interval {info.Interval:0.###} s, duration {info.Effective:0} s, tempo {info.Tempo:0.##})");
    }

    static Metrics metrics; static long score0; static double xp0; static int gold0, iron0, stone0;

    static void BeginRound(int seed)
    {
        SkipCards();
        metrics = new Metrics();
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        HarvestBehaviorStats.Reset();
        UnityEngine.Random.InitState(1300 + seed);
        var spawners = Spawners();
        foreach (var s in spawners) { s.RemoveSpawnedPlant(); s.enabled = true; }
        int round = configs[config].Snap.Round;
        SetP(RM, "CurrentRound", round - 1);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception("measurement round did not start: " + State + " " + RM.CurrentRound);
        foreach (var s in spawners) SetF(s, "timer", UnityEngine.Random.Range(0f, (float)Call(s, "GetEffectiveSpawnInterval")));
        bot.Begin(spawners);
        score0 = Score; xp0 = ProgressionManager.Instance.TotalXPEarned;
        gold0 = Res(ResourceType.Gold); iron0 = Res(ResourceType.Iron); stone0 = Res(ResourceType.Stone);
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void Collect()
    {
        Array.Copy(bot.Kills, metrics.kills, 5); Array.Copy(bot.Rarity, metrics.rarity, 5);
        Array.Copy(bot.FreshHits, metrics.freshHits, 5); Array.Copy(bot.FreshKills, metrics.freshKills, 5); Array.Copy(bot.Tracked, metrics.tracked, 5);
        Array.Copy(bot.HitSum, metrics.hitSum, 5); Array.Copy(bot.TimeSum, metrics.timeSum, 5);
        metrics.attacks = bot.Attacks; metrics.idle = bot.Idle; metrics.hits = bot.Hits;
        metrics.fill = bot.FillSamples > 0 ? bot.FillSum / bot.FillSamples : 0;
        metrics.score = Score - score0; metrics.xp = ProgressionManager.Instance.TotalXPEarned - xp0;
        metrics.gold = Res(ResourceType.Gold) - gold0; metrics.iron = Res(ResourceType.Iron) - iron0; metrics.stone = Res(ResourceType.Stone) - stone0;
        results.Add((config, index - 1, metrics));
        SkipCards();
    }

    static string Stat(IEnumerable<double> values, string format = "0")
    {
        var v = values.ToList();
        return $"{v.Average().ToString(format)} [{v.Min().ToString(format)}–{v.Max().ToString(format)}]";
    }

    static void Report(int c)
    {
        var cfg = configs[c]; var inf = infos[c];
        var m = results.Where(r => r.config == c).Select(r => r.m).ToList();
        double A(Func<Metrics, double> f) => m.Average(f);
        Note($"--- {cfg.Name} (sahne, {m.Count} seed; ort. [en az–en çok]) ---");
        Note($"durum: hasar {inf.Damage:0.#} · kritik %{inf.Crit * 100:0} ×{inf.CritMult:0.0#} · saldırı aralığı {inf.Interval:0.###} sn · yarıçap {inf.Radius:0.##} · süre {inf.Raw:0} sn (etkili {inf.Effective:0}, tempo {inf.Tempo:0.##})" +
             $" · {inf.Planters} saksı / {inf.Spawners} üretim noktası / {inf.Unlocked} açık hücre · {inf.Tiles} tile · üretim tavanı {inf.Ceiling:0}/round · can R{inf.Round} {string.Join("/", inf.Hp)}");
        Note($"sahne: hasat {Stat(m.Select(x => (double)x.Kills))} · doğrudan {A(x => x.kills[0]):0} · davranış {A(x => x.Behavior):0} (%{A(x => x.Kills > 0 ? 100.0 * x.Behavior / x.Kills : 0):0}: " +
             string.Join(" ", Enumerable.Range(1, 4).Select(k => $"{DamageNames[k]} {A(x => x.kills[k]):0}")) + ")");
        Note($"      skor {Stat(m.Select(x => (double)x.score))} · XP {A(x => x.xp):0} · G/I/S {A(x => x.gold):0}/{A(x => x.iron):0}/{A(x => x.stone):0} · saldırı {A(x => x.attacks):0} (boşa {A(x => x.idle):0}) · vuruş/saldırı {A(x => x.attacks > 0 ? (double)x.hits / x.attacks : 0):0.0}");
        Note($"      darboğaz: hasat/üretim tavanı %{A(x => 100.0 * x.Kills / Math.Max(1, inf.Ceiling)):0} · üretim noktası doluluğu %{A(x => x.fill * 100):0} (yüksek = hasat yetişmiyor, düşük = üretim yetişmiyor) · saldırı başına canlı hedef var %{A(x => x.attacks + x.idle > 0 ? 100.0 * x.attacks / (x.attacks + x.idle) : 0):0}");
        var rows = new List<string>();
        for (int k = 0; k < 5; k++)
        {
            int fresh = m.Sum(x => x.freshHits[k]), one = m.Sum(x => x.freshKills[k]), tracked = m.Sum(x => x.tracked[k]);
            rows.Add($"{RarityNames[k]} hasat {A(x => x.rarity[k]):0} · tek vuruş %{(fresh > 0 ? 100.0 * one / fresh : double.NaN):0} ({one}/{fresh}) · doğrudan öldürmede vuruş {(tracked > 0 ? m.Sum(x => x.hitSum[k]) / tracked : double.NaN):0.00} · ilk vuruştan hasada {(tracked > 0 ? m.Sum(x => x.timeSum[k]) / tracked : double.NaN):0.0} sn");
        }
        foreach (var row in rows) Note("      " + row);
        int allFresh = m.Sum(x => x.freshHits.Sum()), allOne = m.Sum(x => x.freshKills.Sum());
        Note($"      tek vuruş (bütün nadirlikler, taze bitki) %{(allFresh > 0 ? 100.0 * allOne / allFresh : 0):0} ({allOne}/{allFresh})");
        if (cfg.Sim != null)
        {
            var s = cfg.Sim;
            double direct = s.Harvest - s.BehaviorHarvest;
            Note($"simülatör (aynı durum, aynı round; isabet politikası, davranış MODEL): hasat {s.Harvest:0} · doğrudan {direct:0} · davranış %{(s.Harvest > 0 ? 100 * s.BehaviorHarvest / s.Harvest : 0):0} · skor {s.Score:0} · G/I/S {s.Gold:0}/{s.Iron:0}/{s.Stone:0}" +
                 $" · hasat/tavan %{s.HarvestRatio * 100:0} · tek vuruş (model) %{s.OneShot * 100:0}");
            Note($"sahne / simülatör: hasat ×{A(x => x.Kills) / Math.Max(1e-9, s.Harvest):0.00} · doğrudan ×{A(x => x.kills[0]) / Math.Max(1e-9, direct):0.00} · skor ×{A(x => x.score) / Math.Max(1e-9, s.Score):0.00} · Gold ×{A(x => x.gold) / Math.Max(1e-9, s.Gold):0.00}");
        }
    }

    static void Summary()
    {
        Require(results.Count == configs.Count * Seeds, $"Measured {configs.Count} states × {Seeds} seeds = {results.Count} full rounds");
        Require(results.All(r => r.m.attacks > 0 && r.m.Kills > 0), "Bot harvested in every measured round");
        Require(results.Where(r => configs[r.config].Sim != null && configs[r.config].Snap.Round >= 30).Sum(r => r.m.Behavior) > 0, "Behaviors fired in the R30+ states (kill-trigger)");
    }

    // Oyuncu yerine: saldırı zamanı gelince en çok canlı bitkiyi kapsayan nişana vurur (hücre merkezi, iki hücre arası, köşe); kart ekranlarını bastırır.
    // Tek vuruş: tam canlı bitkiye isabet ve aynı saldırıda doğrudan hasatla ölüm. Vuruş sayısı: bot vuruşlarıyla doğrudan ölen bitkiler.
    sealed class Bot : MonoBehaviour
    {
        public readonly int[] Kills = new int[5], Rarity = new int[5], FreshHits = new int[5], FreshKills = new int[5], Tracked = new int[5];
        public readonly double[] HitSum = new double[5], TimeSum = new double[5];
        public int Attacks, Idle, Hits, FillSamples;
        public double FillSum;
        float timer;
        MethodInfo attack; PlayerController player;
        List<PlantSpawner> spawners = new();
        readonly List<(PlantHealth h, bool fresh, int rarity)> scratch = new();
        readonly Dictionary<PlantHealth, (int hits, float first)> tracked = new();
        static readonly FieldInfo PlantData = typeof(PlantResource).GetField("plantData", BindingFlags.NonPublic | BindingFlags.Instance);
        static int RarityOf(PlantHealth h)
        {
            var res = h.GetComponent<PlantResource>();
            var data = res != null ? PlantData.GetValue(res) as PlantSO : null;
            return data != null ? Mathf.Clamp((int)data.rarity, 0, 4) : 0;
        }
        void OnEnable() { PlantHealth.AnyHarvested += Count; }
        void OnDisable() { PlantHealth.AnyHarvested -= Count; }
        void Count(PlantHealth h)
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            Kills[(int)h.KilledBy]++;
            Rarity[RarityOf(h)]++;
            if (h.KilledBy != DamageType.Direct) tracked.Remove(h);
        }
        public void Begin(List<PlantSpawner> list)
        {
            foreach (var a in new[] { Kills, Rarity, FreshHits, FreshKills, Tracked }) Array.Clear(a, 0, 5);
            Array.Clear(HitSum, 0, 5); Array.Clear(TimeSum, 0, 5);
            Attacks = Idle = Hits = FillSamples = 0; FillSum = 0; timer = 0; spawners = list; tracked.Clear();
            aims = null; cachedRadius = -1f;
        }

        // Nişan adayları: açık hücrelerin merkezi, sağ ve üst komşuyla arası, dört hücre köşesi. Yarıçap ≥ 1,24'te iki hücre arası
        // 6 hücre kapsar (merkez 5); yalnız merkez denenirse alan yatırımı eksik ölçülür. Kapsanan hücreler yarıçap değişince yeniden hesaplanır.
        List<Vector3> aims; List<List<GridObject>> aimCells; float cachedRadius = -1f;
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
            if (player == null) { player = FindFirstObjectByType<PlayerController>(); attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance); }
            float interval = Mathf.Max(StatManager.Instance.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RoundManager.Instance.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            float radius = StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var grid = GridManager.Instance.GetGridSystem();
            BuildAims(grid, radius);
            Vector3 best = Vector3.zero; int bestScore = 0;
            for (int a = 0; a < aims.Count; a++)
            {
                int score = 0;
                foreach (var g in aimCells[a])
                {
                    var p = g.GetPlantObject();
                    if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) score++;
                }
                if (score > bestScore) { bestScore = score; best = aims[a]; }
            }
            if (bestScore <= 0) { Idle++; return; }
            scratch.Clear();
            foreach (var g in grid.GetGridObjectsInRadius(best, radius))
            {
                var p = g.GetPlantObject();
                if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) scratch.Add((h, h.CurrentHealth >= h.MaxHealth, RarityOf(h)));
            }
            attack.Invoke(player, new object[] { best });
            Attacks++;
            float now = Time.time;
            foreach (var (h, fresh, r) in scratch)
            {
                Hits++;
                if (fresh) FreshHits[r]++;
                (int hits, float first) entry = tracked.TryGetValue(h, out var e) ? e : (0, now);
                entry.hits++;
                if (h == null) { tracked.Remove(h); continue; }
                if (!h.IsDead) { tracked[h] = entry; continue; }
                tracked.Remove(h);
                if (h.KilledBy != DamageType.Direct) continue;
                Tracked[r]++; HitSum[r] += entry.hits; TimeSum[r] += now - entry.first;
                if (fresh && entry.hits == 1) FreshKills[r]++;
            }
        }
    }
}
