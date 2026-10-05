using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.8 (P8) İŞLEV testi — Run50_AlphaDengeV1 verisi, round süresi tablosu ve sınırları, süre sayaçları
// (aktif / level hesaplama / kart / boss ödülü / mağaza / hazırlık / duraklama), dönem–süre ilişkisi, kart ve ödül sırası, zincir
// ayarının izinli aralığı, yeni run temizliği ve eski profillerin korunması. Denge ölçümü DEĞİLDİR: round'lar boş tarlayla ve
// sabit kare adımıyla (1/30 sn) oynanır; XP ve skor doğrudan verilir.
// Duraklama testi sayacın mantığını sınar (askıya alma / geri dönüş çağrılarıyla); işletim sisteminin pencere odağı davranışı
// batch'te sınanamaz — insan Play kontrolüne bırakılmıştır.
[InitializeOnLoad]
public static class AlphaDengeV1Verification
{
    const string Key = "AlphaDengeV1Verification";
    const string LogFile = "Logs/AlphaDengeV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 37801;
    const double Again = double.NaN;
    const float FrameTime = 1f / 30f;
    // Beklenen tablo (görev): R1–5 45 sn, R6–10 55 sn, R11–50 65 sn; toplam 3 100 sn.
    static float Expected(int round) => round <= 5 ? 45f : round <= 10 ? 55f : 65f;
    const double ExpectedTotal = 3100d;
    static readonly int[] Dates = { 3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50 };

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static AlphaDengeV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        foreach (var profile in new[] { A1, X1 }) { profile.bossSeed = Seed; EditorUtility.SetDirty(profile); }
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = A1;
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
    static readonly List<string> seenExpected = new();
    static void Expect(string fragment) => expected.Add(fragment);
    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        int i = expected.FindIndex(f => message.Contains(f));
        if (i >= 0 && type == LogType.Error) { expected.RemoveAt(i); seenExpected.Add(message.Split('\n')[0]); notes.Add("   beklenen log: " + message.Split('\n')[0]); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(LogFile, new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
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
    static RunClock Clock => RunClock.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO A1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_AlphaDengeV1.asset");
    static RunProfileSO X1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_XPV1.asset");
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static bool Near(double a, double b, double tolerance = 1e-3) => Math.Abs(a - b) <= tolerance;
    static string S(double v, string f = "0.###") => v.ToString(f, System.Globalization.CultureInfo.InvariantCulture);

    static RunProfileSO Table(params (int from, float seconds)[] bands)
    {
        var p = ScriptableObject.CreateInstance<RunProfileSO>();
        p.runLength = 50;
        foreach (var (from, seconds) in bands) p.roundDurations.Add(new RoundDurationBand { fromRound = from, seconds = seconds });
        return p;
    }

    static BossRewardSO ChainOf(RunProfileSO profile) =>
        profile.bossRewards.stages.SelectMany(s => s.rewards).Select(e => e.reward).FirstOrDefault(r => r != null && r.chain);

    // UI görüntüsü (Logs/<ad>.png): overlay canvas'lar kameraya alınır, tek kare çizilir, geri bırakılır.
    static void Capture(string name)
    {
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .01f; }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create(); var old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        Note("görüntü: Logs/" + name + ".png");
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("veri: profil, denge seti, ödül havuzu", DataChecks);
        Add("süre tablosu: kurallar ve sınırlar", TableRules);
        Add("dönem – süre ilişkisi", PeriodSeconds);
        Add("sahne: Run50_AlphaDengeV1", StartA);
        Add("sayaç: ekranlara göre ayrım ve duraklama", ClockUnit);
        Add("R1 oynanır (45 sn)", () => Play(1));
        Add("R1 sonucu", () => Played(1));
        Add("hazırlık ve mağaza süresi aktif süreye yazılmaz", PrepAndShop);
        Add("hazırlık ve mağaza sonucu", PrepAndShopResult);
        Add("R7 oynanır (55 sn)", () => Play(7));
        Add("R7 sonucu", () => Played(7));
        Add("R11 oynanır (65 sn)", () => Play(11));
        Add("R11 sonu: bekleyen level işi", LevelWork);
        Add("R11 sonu: level hesaplama süresi", LevelWorkResult);
        Add("R11 sonu: kart ekranı süresi", CardTime);
        Add("R11 sonucu", () => Played(11));
        Add("XP: üç seçim ve tablo", XpChoices);
        Add("sıra: kartlar → boss ödülü (R13, 65 sn)", BossOrder);
        Add("sıra: boss ödülü süresi", BossOrderResult);
        Add("zincir ayarı", ChainConfig);
        Add("run sonu ekranı: süre satırı", RunEnd);
        Add("run sonu ekranı görüntüsü", RunEndPicture);
        Add("ana menü yolu: süre round 1'e döner", MenuPath);
        Add("sahne yeniden: Run50_AlphaDengeV1", () => Load(A1));
        Add("yeni run temiz", CleanStart);
        Add("geçersiz tablo: sahne", LoadInvalid);
        Add("geçersiz tablo: run başlamaz", InvalidBlocked);
        Add("sahne: Run50_XPV1 (eski sabit süre)", () => Load(X1));
        Add("eski profil: sabit 45 sn", OldProfile);
        Add("eski profil: R11 oynanır (45 sn)", () => Play(11));
        Add("eski profil: R11 sonucu", OldPlayed);
        Add("kapanış", FinalCheck);
    }

    // ================================================================ veri
    static double DataChecks()
    {
        var a = A1; var x = X1;
        Must(a != null && x != null, "profiles missing");
        Require(a.runLength == 50 && a.choicesPerLevel == 3 && a.bossCalendar.Select(d => d.round).SequenceEqual(Dates) && a.bossCalendar.Select(d => d.round).SequenceEqual(x.bossCalendar.Select(d => d.round)) &&
                a.bossCalendar[14].final && a.bossPool == x.bossPool && a.startingGold == x.startingGold && a.startingIron == x.startingIron && a.startingStone == x.startingStone &&
                !a.debugBudget && a.events.Count == 0 && a.specializationAfterSegment == 0 && RunCalendar.Validate(a) == null,
            "Run50_AlphaDengeV1 keeps Run50_XPV1's run: 50 rounds, the same 15 boss dates (R50 final), 3 choices per level, boss pool, starting economy; calendar valid");
        Require(a.fixedRoundDuration == 0f && x.fixedRoundDuration == 45f && x.roundDurations.Count == 0 && a.roundDurations.Count == 3 &&
                a.roundDurations.Select(b => (b.fromRound, b.seconds)).SequenceEqual(new[] { (1, 45f), (6, 55f), (11, 65f) }) && RoundDurations.Validate(a) == null,
            "Round duration comes from the profile's table: R1+ 45 s, R6+ 55 s, R11+ 65 s (no fixed duration next to it); Run50_XPV1 keeps its fixed 45 s and has no table");
        Require(Enumerable.Range(1, 50).All(r => RoundDurations.SecondsFor(a, r) == Expected(r)) && RoundDurations.TotalSeconds(a) == ExpectedTotal,
            $"Every round resolves from the table (R1–5 45 s, R6–10 55 s, R11–50 65 s); the 50 rounds add up to {S(RoundDurations.TotalSeconds(a))} s = 51 min 40 s of active play");
        Require(a.segmentTargets.Count == 15 && a.bossTargets.Count == 15 && a.segmentTargets.All(t => t > 0) && a.bossTargets.All(t => t >= 0),
            "Quota and boss targets are complete tables of the profile (15 periods): quota " + string.Join(" / ", a.segmentTargets) + " · boss " + string.Join(" / ", a.bossTargets));

        var ab = a.balance; var xb = x.balance;
        Require(ab != null && ab != xb && ab.coreStats == xb.coreStats && ab.skillTree == xb.skillTree && ab.rarityXpMultipliers.SequenceEqual(xb.rarityXpMultipliers) &&
                ab.startingUnlocks.SequenceEqual(xb.startingUnlocks) && ab.firstBehaviorOffer.SequenceEqual(xb.firstBehaviorOffer) && ab.planterPrices.Count == xb.planterPrices.Count &&
                ab.planterBaseStats.Select(e => (e.statType, e.value)).SequenceEqual(xb.planterBaseStats.Select(e => (e.statType, e.value))) &&
                ab.additiveBaseXpCards && ab.hideFlooredBaseStats,
            "Its balance set is its own asset; core stats, skill tree, rarity XP, planter data, early behavior access and the two P7 card rules are XP V1's");
        var xp = ab.progression;
        Require(xp != null && xp.tailGrowth == xb.progression.tailGrowth && xp.xpRequirements.Count == xb.progression.xpRequirements.Count && xp.Validate() == null,
            xp == xb.progression ? "XP table: the same asset as Run50_XPV1 (not changed by this profile)"
                                 : "XP table: the profile's own copy (same length and tail growth as XP V1; changed levels are listed in the document)");
        var hp = ab.plantHealth; var oldHp = xb.plantHealth;
        Must(hp != null && hp != oldHp && hp.name == "PlantHealth_AlphaDengeV1", "the profile should own its plant health curve");
        var common = AssetDatabase.FindAssets("t:PlantSO").Select(g => AssetDatabase.LoadAssetAtPath<PlantSO>(AssetDatabase.GUIDToAssetPath(g))).First(p => p != null && p.rarity == PlantRarity.Common);
        double worst = 0; int worstRound = 0; bool monotone = true;
        for (int r = 2; r <= 50; r++)
        {
            int before = hp.Calculate(common, r - 1), now = hp.Calculate(common, r);
            if (now < before) monotone = false;
            double step = now / (double)Math.Max(1, before);
            if (step > worst) { worst = step; worstRound = r; }
        }
        Require(hp.anchors.Count >= 2 && hp.anchors[0].round == 1 && hp.anchors[hp.anchors.Count - 1].round == 50 && monotone && worst <= 1.25 &&
                hp.rarityMultipliers.SequenceEqual(oldHp.rarityMultipliers) && hp.referenceHealth == oldHp.referenceHealth,
            $"Plant health is the profile's own curve: round based (R1 … R50), never decreasing, largest single-round step ×{S(worst, "0.00")} (R{worstRound}; integer rounding included), " +
            $"rarity multipliers unchanged ({string.Join(" / ", hp.rarityMultipliers.Select(v => S(v)))}). Common health R1 {hp.Calculate(common, 1)} · R10 {hp.Calculate(common, 10)} · R20 {hp.Calculate(common, 20)} · " +
            $"R30 {hp.Calculate(common, 30)} · R40 {hp.Calculate(common, 40)} · R50 {hp.Calculate(common, 50)}");

        // Ödül havuzu: içerik (aşamalar ve ödül kimlikleri) XP V1 / Zincir V1 ile aynı; değeri değişen ödül bu profilin kopyasıdır.
        var pool = a.bossRewards; var oldPool = x.bossRewards;
        Must(pool != null && pool.IsStaged && BossRewardPoolSO.Validate(pool, a.runLength) == null, "reward pool invalid");
        Require(pool.stages.Count == oldPool.stages.Count && pool.choices == oldPool.choices && pool.reserveCurrentStageSlot == oldPool.reserveCurrentStageSlot &&
                pool.stageDistanceWeights.SequenceEqual(oldPool.stageDistanceWeights) &&
                Enumerable.Range(0, pool.stages.Count).All(i => pool.stages[i].id == oldPool.stages[i].id && pool.stages[i].firstRound == oldPool.stages[i].firstRound &&
                    pool.stages[i].rewards.Select(e => e.reward.id).SequenceEqual(oldPool.stages[i].rewards.Select(e => e.reward.id)) &&
                    pool.stages[i].rewards.Select(e => (e.reward.maxStacks, e.reward.weight, e.reward.levelChoiceDelta)).SequenceEqual(oldPool.stages[i].rewards.Select(e => (e.reward.maxStacks, e.reward.weight, e.reward.levelChoiceDelta)))),
            (pool == oldPool ? "Reward pool: the same asset as Run50_XPV1" : "Reward pool: the profile's own copy") +
            " — same stages, same rewards in the same order, same stack limits, weights and level-choice trades (no reward added, removed or re-weighted)");
        int changed = Enumerable.Range(0, pool.stages.Count).Sum(i => Enumerable.Range(0, pool.stages[i].rewards.Count).Count(k => pool.stages[i].rewards[k].reward != oldPool.stages[i].rewards[k].reward));
        Note($"değeri bu profilde değişen ödül sayısı: {changed}" + (changed > 0 ? " (" + string.Join(", ", Enumerable.Range(0, pool.stages.Count).SelectMany(i => Enumerable.Range(0, pool.stages[i].rewards.Count)
            .Where(k => pool.stages[i].rewards[k].reward != oldPool.stages[i].rewards[k].reward).Select(k => pool.stages[i].rewards[k].reward.id))) + ")" : ""));

        int old = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RunProfileSO", new[] { "Assets/ScriptableObjects/RunProfiles" }))
        {
            var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (p == a) continue;
            old++;
            Must(p.roundDurations == null || p.roundDurations.Count == 0, "old profile got a duration table: " + p.name);
            Must(RoundDurations.Validate(p) == null && !RoundDurations.HasTable(p) && RoundDurations.SecondsFor(p, 11) == 0f, "old profile should be untouched by the table code: " + p.name);
        }
        Require(old >= 14, $"None of the {old} older profiles has a duration table: they keep the fixed duration / duration upgrades path");
        return .05;
    }

    // ================================================================ süre tablosu kuralları (sahne gerekmez)
    static double TableRules()
    {
        string Error(RunProfileSO p) => RoundDurations.Validate(p);
        Require(Error(Table()) == null && !RoundDurations.HasTable(Table()) && RoundDurations.SecondsFor(Table(), 5) == 0f && RoundDurations.TotalSeconds(Table()) == 0d,
            "Empty table: valid, and the table code decides nothing (old path)");
        var one = Table((1, 60f));
        Require(Error(one) == null && RoundDurations.SecondsFor(one, 1) == 60f && RoundDurations.SecondsFor(one, 50) == 60f && RoundDurations.TotalSeconds(one) == 3000d,
            "One row from Round 1 covers the whole run (50 × 60 s = 3 000 s)");
        var edges = Table((1, RoundDurations.MinSeconds), (50, RoundDurations.MaxSeconds));
        Require(Error(edges) == null && RoundDurations.SecondsFor(edges, 49) == RoundDurations.MinSeconds && RoundDurations.SecondsFor(edges, 50) == RoundDurations.MaxSeconds,
            $"Limits are inclusive: {S(RoundDurations.MinSeconds)} s and {S(RoundDurations.MaxSeconds)} s are accepted; a row may start on the last round");
        var three = Table((1, 45f), (6, 55f), (11, 65f));
        Require(RoundDurations.SecondsFor(three, 5) == 45f && RoundDurations.SecondsFor(three, 6) == 55f && RoundDurations.SecondsFor(three, 10) == 55f && RoundDurations.SecondsFor(three, 11) == 65f &&
                RoundDurations.SecondsFor(three, 0) == 45f && RoundDurations.SecondsFor(three, 999) == 65f,
            "A round uses the last row that starts at or before it (R5 45 · R6 55 · R10 55 · R11 65); rounds outside the run read the nearest row");
        Require(Error(Table((2, 45f))).Contains("Round 1'den başlamalı") && Error(Table((1, 45f), (6, 55f), (6, 65f))).Contains("iki kez") &&
                Error(Table((1, 45f), (11, 65f), (6, 55f))).Contains("sıralı değil") && Error(Table((1, 45f), (51, 65f))).Contains("run sınırlarının dışında"),
            "Rejected, each with its own message: first row not at Round 1 · the same start twice · rows out of order · a row beyond the run");
        Require(Error(Table((1, 29.9f))).Contains("30–120") && Error(Table((1, 45f), (6, 120.1f))).Contains("30–120") && Error(Table((1, float.NaN))) != null &&
                Error(Table((1, float.PositiveInfinity))) != null && Error(Table((1, 0f))) != null && Error(Table((1, -5f))) != null,
            "Rejected: a duration below 30 s or above 120 s, zero, negative, NaN and infinity");
        var both = Table((1, 45f)); both.fixedRoundDuration = 45f;
        Require(Error(both).Contains("birlikte kullanılamaz"), "Rejected: a table together with a fixed duration (the duration must come from one place)");
        var kept = Table((1, 45f), (6, 20f));
        string error = Error(kept);
        Require(error != null && kept.roundDurations[1].seconds == 20f && kept.roundDurations.Count == 2, $"Invalid data is reported, not corrected (\"{error}\")");
        return .05;
    }

    // ================================================================ dönem – süre ilişkisi
    static double PeriodSeconds()
    {
        var a = A1; var c = a.Calendar;
        double total = 0; var lines = new List<string>();
        for (int period = 1; period <= 15; period++)
        {
            int first = c.PeriodStart(period), last = c.PeriodEnd(period);
            double seconds = 0; for (int r = first; r <= last; r++) seconds += RoundDurations.SecondsFor(a, r);
            total += seconds;
            Must(last == Dates[period - 1] && c.IsBossRound(last) && c.PeriodLength(period) == last - first + 1, "period bounds: " + period);
            lines.Add($"dönem {period,2} R{first}–{last}: {S(seconds)} sn aktif · boss round'u R{last} {S(RoundDurations.SecondsFor(a, last))} sn · kota {c.QuotaTarget(period)} · boss hedefi {c.BossTarget(period)}");
        }
        Require(total == ExpectedTotal && Enumerable.Range(1, 50).All(r => c.PeriodOf(r) >= 1 && c.PeriodOf(r) <= 15),
            "Quota periods still follow the 15 boss dates and every round belongs to exactly one period; the periods' active seconds add up to 3 100 s " +
            "(a period's seconds are the sum of its rounds: R1–3 135 s, R4–6 145 s, R7–10 220 s, R11–13 195 s …)");
        foreach (string line in lines) Note(line);
        return .05;
    }

    // ================================================================ sahne
    static double StartA()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != A1) return Again;
        Player.enabled = false;
        MetaSave.UseMemoryOnly();
        NumericSafety.ResetCounters();
        Must(Clock != null && Clock.GetComponent<RoundManager>() == RM, "run clock should live on the round manager");
        Require(RM.HasDurationTable && RM.FixedRoundDuration && RM.DurationError == null && RM.CalendarError == null && RM.RewardPoolError == null &&
                RM.RemainingTime == 45f && RM.EffectiveRoundDuration == 45f && RM.RawRoundDuration == 45f && RM.TempoMultiplier == 1f,
            "Scene runs Run50_AlphaDengeV1: the round timer is ready at 45 s for Round 1, no tempo");
        Require(Enumerable.Range(1, 50).All(r => RM.RoundSecondsFor(r) == Expected(r)), "RoundManager answers every round's duration from the profile table (no round-number branches of its own)");
        bool ok = true;
        foreach (int round in new[] { 6, 11, 50 })
        {
            SetP(RM, "CurrentRound", round);
            ok &= RM.EffectiveRoundDuration == Expected(round) && RM.RawRoundDuration == Expected(round) && RM.TempoMultiplier == 1f;
        }
        SetP(RM, "CurrentRound", 1);
        Require(ok, "65 s rounds are not cut to the old 60 s cap and do not turn into tempo: R6 55 s, R11 65 s, R50 65 s, attack and spawn tempo ×1");
        var tree = SkillTreeManager.Instance;
        int durationNodes = tree.AllNodes.Count(n => n != null && SkillTreeManager.IsDurationOnly(n));
        Require(tree.AllNodes.Where(n => n != null && SkillTreeManager.IsDurationOnly(n)).All(n => tree.IsDisabledByProfile(n)),
            $"Duration upgrades stay out of this run, as with the fixed duration ({durationNodes} duration-only nodes in this tree)");
        Require(Clock.RoundsTimed == 0 && Clock.ActiveGameSeconds == 0d && Clock.ActiveRealSeconds == 0d && Clock.LevelWorkSeconds == 0d && Clock.CardSelectionSeconds == 0d &&
                Clock.RoundChoiceSeconds == 0d && Clock.ShopSeconds == 0d && Clock.PausedSeconds == 0d,
            "Run clock starts at zero: no active time, no menu time, no pause");
        return .05;
    }

    // ================================================================ sayaç: ekranlara göre ayrım (doğrudan çağrı)
    static double ClockUnit()
    {
        var c = Clock;
        double active = c.ActiveGameSeconds, real = c.ActiveRealSeconds;
        (double level, double card, double choice, double shop, double prep, double other) Now() => (c.LevelWorkSeconds, c.CardSelectionSeconds, c.RoundChoiceSeconds, c.ShopSeconds, c.PreparationSeconds, c.OtherSeconds);
        var z = Now();
        c.Advance(GameStates.Round, true, false, 5d);
        var a = Now();
        c.Advance(GameStates.Round, false, true, 1d);
        c.Advance(GameStates.CardSelection, false, false, 2d);
        c.Advance(GameStates.RoundChoice, false, false, 3d);
        c.Advance(GameStates.Shop, false, false, 4d); c.Advance(GameStates.Placing, false, false, 4d); c.Advance(GameStates.Selling, false, false, 4d);
        c.Advance(GameStates.RunSetup, false, false, 6d); c.Advance(GameStates.RoundEnd, false, false, 6d);
        c.Advance(GameStates.RunComplete, false, false, 7d);
        var b = Now();
        Require(a.Equals(z) && Near(b.level - z.level, 1) && Near(b.card - z.card, 2) && Near(b.choice - z.choice, 3) && Near(b.shop - z.shop, 12) && Near(b.prep - z.prep, 12) && Near(b.other - z.other, 7) &&
                c.ActiveGameSeconds == active && c.ActiveRealSeconds == real,
            "Each screen writes to its own counter: pending level work 1 s · card choice 2 s · boss reward 3 s · shop, placing and selling 12 s · run setup and round summary 12 s · " +
            "run end 7 s; none of it reaches the active time (an active round frame is reported only by the round timer)");
        Require(Near(c.ChoiceSeconds - (z.card + z.choice), 5) && Near(c.OutsideRoundSeconds - (z.level + z.card + z.choice + z.shop + z.prep), 30),
            "Totals: choice screens = cards + boss reward; outside-round time = level work + choices + shop + preparation");

        // Duraklama: arada kare işlenmediyse geçen gerçek süre duraklamadır ve sonraki karenin süresinden düşülür.
        c.Resume(0d);   // batch editörün kendi odak durumu varsa kapat (sayılmaz: arada kare işlendi)
        double paused = c.PausedSeconds, card = c.CardSelectionSeconds;
        c.Suspend(1000d); c.Resume(1030d);
        c.Advance(GameStates.CardSelection, false, false, 30.02d);
        Require(Near(c.PausedSeconds - paused, 30) && Near(c.CardSelectionSeconds - card, .02, 1e-6),
            "Pause: 30 s away with no frame processed counts as 30 s of pause; the first frame after the return (30.02 s reported by the engine) adds only 0.02 s to the screen it belongs to");
        paused = c.PausedSeconds; card = c.CardSelectionSeconds;
        c.Suspend(2000d); c.Resume(2010d);
        c.Advance(GameStates.CardSelection, false, false, .03d);
        Require(Near(c.PausedSeconds - paused, 10) && Near(c.CardSelectionSeconds - card, .03, 1e-6),
            "Pause: if the engine already left the gap out of the next frame (0.03 s), nothing is subtracted twice");
        paused = c.PausedSeconds; card = c.CardSelectionSeconds;
        c.Suspend(3000d); c.NoteFrameWhileSuspended(); c.Advance(GameStates.CardSelection, false, false, 5d); c.Resume(3005d);
        Require(c.PausedSeconds == paused && Near(c.CardSelectionSeconds - card, 5),
            "Focus lost but frames kept running (editor, background build): not a pause — the time stays on the screen's own counter");
        paused = c.PausedSeconds; double game = c.ActiveGameSeconds; real = c.ActiveRealSeconds;
        c.Suspend(4000d); c.Resume(4020d);
        c.AddActive(.02d, 20.03d);
        Require(Near(c.PausedSeconds - paused, 20) && Near(c.ActiveGameSeconds - game, .02, 1e-6) && Near(c.ActiveRealSeconds - real, .03, 1e-6),
            "Pause during a round: 20 s away is pause, not active play (active real time grows by the 0.03 s frame only)");
        Require(RunClock.Format(3100) == "51:40" && RunClock.Format(0) == "0:00" && RunClock.Format(59.6) == "1:00" && RunClock.Format(3725) == "1:02:05" && RunClock.Format(-3) == "0:00",
            "Time text: 3 100 s → 51:40, an hour and more → 1:02:05");
        c.ResetClock();
        Require(c.ActiveGameSeconds == 0d && c.ActiveRealSeconds == 0d && c.PausedSeconds == 0d && c.OutsideRoundSeconds == 0d && c.OtherSeconds == 0d && c.RoundsTimed == 0,
            "Reset clears every counter (the same reset runs at run start)");
        return .05;
    }

    // ================================================================ gerçek süreyle oynanan round'lar
    static double activeBefore, realBefore, outsideBefore; static int roundsBefore, framesAtStart;
    static double Play(int round)
    {
        if (round == 1) Must(RM.IsPreparingFirstRound, "round 1 should start from the run setup");
        else { SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false); }
        activeBefore = Clock.ActiveGameSeconds; realBefore = Clock.ActiveRealSeconds; roundsBefore = Clock.RoundsTimed; outsideBefore = Clock.OutsideRoundSeconds;
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive && RM.CurrentRound == round, $"round {round} did not start ({State}, R{RM.CurrentRound})");
        Must(RM.RemainingTime == RM.RoundSecondsFor(round), $"round {round} timer starts at {RM.RemainingTime}");
        framesAtStart = Time.frameCount;
        return .02;
    }

    static double Played(int round)
    {
        if (State == GameStates.Round) return Again;
        float seconds = Expected(round);
        double game = Clock.ActiveGameSeconds - activeBefore, real = Clock.ActiveRealSeconds - realBefore;
        Must(State == GameStates.RoundEnd && !RM.IsRoundActive && RM.RemainingTime == 0f, $"round {round} should end on the round summary ({State})");
        Require(Near(game, seconds, 1e-4) && Near(Clock.LastRoundGameSeconds, seconds, 1e-4) && Clock.RoundsTimed == roundsBefore + 1 && real >= game - 1e-6 && real - game < 2d && Near(Clock.LastRoundRealSeconds, real, 1e-6),
            $"R{round} played to the end of its timer: the round counter ran {S(game, "0.0000")} s (table: {S(seconds)} s); real time during those frames {S(real)} s " +
            $"(+{S(real - game)} s from the round-end slow motion{(GameSettings.RoundEndSlowMotion ? "" : ", which is off")})");
        return .05;
    }

    static double prepAt, shopAt, activeAt;
    static double PrepAndShop()
    {
        prepAt = Clock.PreparationSeconds; shopAt = Clock.ShopSeconds; activeAt = Clock.ActiveGameSeconds;
        return .4;   // round özeti açık: kareler işler
    }
    static int shopPhase;
    static double PrepAndShopResult()
    {
        if (shopPhase == 0)
        {
            Must(Clock.PreparationSeconds > prepAt && Clock.ShopSeconds == shopAt, "preparation time should grow on the round summary");
            prepAt = Clock.PreparationSeconds;
            GameManager.Instance.OpenShop();
            shopPhase = 1;
            return Again;
        }
        if (shopPhase < 12) { shopPhase++; return Again; }
        Must(State == GameStates.Shop, "shop should be open: " + State);
        double shop = Clock.ShopSeconds - shopAt;
        GameManager.Instance.ShowRoundEnd();
        Require(shop > 0d && Clock.ActiveGameSeconds == activeAt && Near(Clock.PreparationSeconds, prepAt, .1),
            $"Time on the round summary went to preparation and time in the shop to the shop counter ({S(shop)} s in this test); the active time did not move while they were open");
        return .05;
    }

    // ---- R11 sonu: bekleyen level işi ve kart ekranı
    // Round bitmeden 3 sn önce verilir; kare başına 256 level işlendiği için round sonunda hâlâ bekleyen iş kalır.
    const int WorkLevels = 60000;
    static double levelAt, cardAt; static int levelBefore;
    static double LevelWork()
    {
        if (State != GameStates.Round) throw new Exception("round 11 ended before the level work was queued");
        if (RM.RemainingTime > 3f) return Again;
        double xp = 0; for (int i = 0; i < WorkLevels; i++) xp += Progress.Data.GetXPForLevel(Progress.CurrentLevel + i);
        levelBefore = Progress.CurrentLevel; levelAt = Clock.LevelWorkSeconds; cardAt = Clock.CardSelectionSeconds;
        Progress.AddXP(xp + 1d - Progress.StoredXP);
        return .02;
    }
    static bool sawWork; static double activeAtWork;
    static double LevelWorkResult()
    {
        if (State == GameStates.Round && RM.IsRoundActive) return Again;
        if (State == GameStates.Round)
        {
            Must(RM.IsAwaitingLevels, "round end should wait for the level work");
            if (!sawWork) { sawWork = true; activeAtWork = Clock.ActiveGameSeconds; }
            return Again;
        }
        Must(State == GameStates.CardSelection, "card screen expected after the level work: " + State);
        double work = Clock.LevelWorkSeconds - levelAt;
        Require(sawWork && work > 0d && Clock.ActiveGameSeconds == activeAtWork && Progress.CurrentLevel >= levelBefore + WorkLevels && RM.PendingCardSelections >= WorkLevels * 3,
            $"Round end waited for {WorkLevels} pending levels: {S(work)} s went to the level-work counter, the active time did not move; then the card screen opened with {RM.PendingCardSelections} choices (3 per level)");
        return .3;   // kart ekranı açık: kareler işler
    }
    static double CardTime()
    {
        double card = Clock.CardSelectionSeconds - cardAt;
        Must(State == GameStates.CardSelection, "card screen should still be open");
        Require(card > 0d && Clock.ActiveGameSeconds == activeAtWork, $"Time on the card screen went to the card counter ({S(card)} s in this test), not to the active time");
        SetF(RM, "pendingCardSelections", 1);
        RM.OnCardSelectionComplete();
        Must(State == GameStates.RoundEnd, "round should close after the last card: " + State);
        return .05;
    }

    // ================================================================ XP: üç seçim
    static double XpChoices()
    {
        Must(State == GameStates.RoundEnd, "round summary expected: " + State);
        Require(RM.BaseChoicesPerLevel == 3 && RM.ChoicesPerLevel == 3 && Progress.Data == A1.balance.progression && Progress.Data.HasGrowingTail && !Progress.LevelProcessingHalted &&
                !Progress.HasPendingLevels && NumericSafety.TotalSaturated == 0 && NumericSafety.TotalInvalid == 0,
            $"XP in this profile: 3 choices per level, the growing cost after the table, no halt, no saturated or invalid number after {Progress.CurrentLevel - 1} levels in one round end");
        return .05;
    }

    // ================================================================ sıra: kartlar → boss ödülü (R13)
    static double choiceAt; static int choicePhase;
    static double BossOrder()
    {
        SetP(RM, "CurrentRound", 12); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == 13 && RM.IsBossRound(13) && RM.RemainingTime == 65f, "boss round 13 should start with 65 s");
        var score = HarvestScoreManager.Instance;
        SetF(score, "totalScore", score.TotalScore + 100000000L);
        SetF(RM, "pendingCardSelections", 2);
        Call(RM, "EndRound");
        Must(State == GameStates.CardSelection, "card screen expected: " + State);
        bool pendingReward = Boss.IsPending && RM.IsRoundChoicePending;
        RM.StartNextRound(); RM.ContinueAfterRoundChoice();
        bool blocked = State == GameStates.CardSelection && RM.CurrentRound == 13;
        Call(Cards, "OnCardSelected", Offers[0]);
        bool still = State == GameStates.CardSelection && Cards.RemainingText == "SON SEÇİM";
        Call(Cards, "OnCardSelected", Offers[0]);
        Require(pendingReward && blocked && still && State == GameStates.RoundChoice,
            "Boss round R13 (65 s) with 2 pending card choices and a boss reward: the reward screen does not open and the next round cannot start until the last card is chosen; then the reward screen opens");
        choiceAt = Clock.RoundChoiceSeconds;
        return .3;
    }
    static double BossOrderResult()
    {
        if (State == GameStates.RoundChoice && Boss.IsPending && Boss.Offer.Count == 0 && !F<bool>(Boss, "offerPrepared")) return Again;
        if (choicePhase++ < 6) return Again;
        double choice = Clock.RoundChoiceSeconds - choiceAt;
        if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward();
        Require(State == GameStates.RoundEnd && !Boss.IsPending && choice > 0d, $"Time on the boss reward screen went to its own counter ({S(choice)} s in this test); after the reward the round closes normally");
        return .05;
    }

    // ================================================================ zincir ayarı
    static double ChainConfig()
    {
        var reward = ChainOf(A1); var old = ChainOf(X1);
        Must(reward != null && old != null && reward.id == "zincir_hasat", "chain reward missing");
        Must(HarvestChainConfig.TryCreate(reward, out var config, out string error), "chain data invalid: " + error);
        bool inRange = reward.chainChance[0] >= .75f && reward.chainChance[0] <= 1f && reward.chainChance[1] >= .5f && reward.chainChance[1] <= .75f &&
                       reward.chainDamage[0] >= .75f && reward.chainDamage[0] <= 1f && reward.chainDamage[1] >= .5f && reward.chainDamage[1] <= .75f;
        Require(config.Generations == 2 && config.RootBudget == 32 && config.JobsPerFrame == 8 && reward.chainGenerations == old.chainGenerations && reward.chainRootBudget == old.chainRootBudget &&
                reward.chainJobsPerFrame == old.chainJobsPerFrame && inRange && reward.maxStacks == 1 && reward.condition == old.condition && reward.echo == BossRewardEcho.None,
            $"Zincir Hasat in this profile: 2 extra generations, root budget 32, 8 jobs per frame (unchanged); chance ×{S(reward.chainChance[0])} / ×{S(reward.chainChance[1])}, " +
            $"damage ×{S(reward.chainDamage[0])} / ×{S(reward.chainDamage[1])} — inside the allowed range (0.75 / 0.50 … 1.00 / 0.75)" +
            (reward == old ? "; the same asset as Zincir V1" : "; the profile's own copy"));
        Require(config.ChanceFactor(1) == reward.chainChance[0] && config.ChanceFactor(2) == reward.chainChance[1] && config.DamageFactor(1) == reward.chainDamage[0] &&
                config.DamageFactor(2) == reward.chainDamage[1] && !BossRewardManager.TryGetChain(out _),
            "The running chain reads exactly these values from the reward (per-generation factors, not accumulated); no chain is active before the reward is taken");
        return .05;
    }

    // ================================================================ run sonu ekranı
    static double RunEnd()
    {
        // Boss round'u R16, o round'da skor yok: boss hasadı tutmaz ve run biter.
        SetP(RM, "CurrentRound", 15); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == 16, "round 16 should start");
        Call(RM, "EndRound");
        Must(State == GameStates.RunComplete && RM.Outcome != RunOutcome.Victory, "run should end on the missed boss harvest: " + State);
        return .6;
    }
    static double RunEndPicture()
    {
        var ui = Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include);
        string text = F<TextMeshProUGUI>(ui, "scoreText").text;
        string line = RunCompleteUI.TimeLine(Clock);
        Require(line != null && text.Contains(line) && line.StartsWith("Aktif oynanış " + RunClock.Format(Clock.ActiveGameSeconds)) && line.Contains("seçimler ") && line.Contains("mağaza ve hazırlık ") &&
                line.Contains("seviye hesaplama "),
            $"Run end screen shows the separate times: \"{line}\" (three rounds were played to the end here: 45 + 55 + 65 s)");
        Capture("T378_01_RunSonu_SureSatiri");
        return .05;
    }

    static double MenuPath()
    {
        SetP(RM, "CurrentRound", 30);
        RM.ResetRounds();
        Require(RM.CurrentRound == 1 && RM.RemainingTime == 45f && !RM.IsRoundActive, "The main-menu path resets the round to 1 and the timer to Round 1's 45 s (not the 65 s of the round it left)");
        return .05;
    }

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != A1 || Progress == null || Progress.CurrentLevel != 1) return Again;
        Player.enabled = false;
        Require(RM.CurrentRound == 1 && RM.IsPreparingFirstRound && RM.RemainingTime == 45f && RM.DurationError == null && Clock != null && Clock.RoundsTimed == 0 && Clock.ActiveGameSeconds == 0d &&
                Clock.ActiveRealSeconds == 0d && Clock.LevelWorkSeconds == 0d && Clock.CardSelectionSeconds == 0d && Clock.RoundChoiceSeconds == 0d && Clock.ShopSeconds == 0d && Clock.PausedSeconds == 0d &&
                Progress.StoredXP == 0d && !Progress.HasPendingLevels && RM.PendingCardSelections == 0 && RM.LevelsGained == 0 && Boss.Taken.Count == 0 && !BossRewardManager.TryGetChain(out _) &&
                HarvestScoreManager.Instance.TotalScore == 0,
            "New scene / new run: Round 1 with 45 s, the run clock back at zero (active, level work, cards, boss reward, shop, pause), level 1, no pending choices, no boss reward, no chain, score 0");
        return .05;
    }

    // ================================================================ geçersiz tablo
    static RunProfileSO invalid;
    static double LoadInvalid()
    {
        invalid = Object.Instantiate(A1);
        invalid.name = "Run50_AlphaDengeV1 (geçersiz süre tablosu)";
        invalid.roundDurations[1] = new RoundDurationBand { fromRound = 6, seconds = 20f };
        Expect("Run başlatılmadı");
        GameManager.Instance.ReturnToMenu();   // run kurulmazsa durum burada kalır
        return Load(invalid);
    }
    static double InvalidBlocked()
    {
        if (RM == null || RM.Profile != invalid || RM.DurationError == null) return Again;
        GameStates before = State;
        // Run kurulmadı (durum ana menüde kaldı). Hazırlık durumuna elle geçilse de round başlamaz.
        GameManager.Instance.StartRunSetup();
        RM.StartNextRound();
        Require(expected.Count == 0 && seenExpected.Count == 1 && seenExpected[0].Contains("round süresi tablosu geçersiz") && RM.DurationError.Contains("30–120") &&
                before == GameStates.MainMenu && State == GameStates.RunSetup && !RM.IsRoundActive && RM.CurrentRound == 1,
            $"Invalid duration table at run time: the run is not set up (state stayed {before}), a round cannot be started, and one clear error is logged (\"{seenExpected[0]}\")");
        Require(invalid.roundDurations[1].seconds == 20f, "The invalid data is left exactly as it was (20 s is not raised to the limit)");
        return .05;
    }

    // ================================================================ eski profil
    static double OldProfile()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != X1) return Again;
        Player.enabled = false;
        bool ok = !RM.HasDurationTable && RM.FixedRoundDuration && RM.DurationError == null && RM.RemainingTime == 45f;
        foreach (int round in new[] { 1, 6, 11, 50 })
        {
            SetP(RM, "CurrentRound", round);
            ok &= RM.EffectiveRoundDuration == 45f && RM.RawRoundDuration == 45f && RM.TempoMultiplier == 1f && RM.RoundSecondsFor(round) == 45f;
        }
        SetP(RM, "CurrentRound", 1);
        Require(ok, "Run50_XPV1 (old fixed path): no table, every round 45 s (R1, R6, R11, R50), no tempo — unchanged");
        Require(Clock != null && Clock.RoundsTimed == 0, "The run clock also runs for old profiles (it only counts)");
        return .05;
    }
    static double OldPlayed()
    {
        if (State == GameStates.Round) return Again;
        double game = Clock.ActiveGameSeconds - activeBefore;
        Require(State == GameStates.RoundEnd && Near(game, 45d, 1e-4) && Clock.RoundsTimed == roundsBefore + 1,
            $"Old profile, Round 11 played to the end: {S(game, "0.0000")} s on the round counter (45 s, not 65 s)");
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0 && expected.Count == 0,
            $"Every deliberately produced error was reported where expected (invalid duration table) and nothing else was logged as an error ({errors} unexpected)");
        return .05;
    }
}
