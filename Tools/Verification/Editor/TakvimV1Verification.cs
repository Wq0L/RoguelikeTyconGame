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

// Batch (izole kopya): Bölüm 3.7.2 İŞLEV testi — 15 boss takvimi ve değişken kota dönemleri (Run50_TakvimV1).
// Denge ölçümü DEĞİLDİR: oyuncu vuruşu kapalıdır, skor ve level testin eliyle (debug) verilir.
// Round'lar sırayla oynanır, round numarası atlanmaz: her round oyunun kendi StartNextRound yoluyla başlar; test yalnız kalan
// süreyi sıfırlar, round'u RoundManager'ın kendi sayaç yolu (Update → EndRound) bitirir. 45 saniyelik süre beklenmez.
// A) Veri: profil ayrımı, takvim, aktarım hesabı, eski profillerin eski formüllerle birebir aynı kalması, hatalı takvim denetimi.
// B) Tam run (R1–50): 15 boss aktivasyonu, dönemler, önizleme = aktif boss, kota / boss eşitliği geçer, geçişler, R50 sırası.
// C) Kayıplar: kota bir eksik, boss hasadı bir eksik, ikisi birden, R50'de kayıp. Her birinden sonra yeniden başlatma temizliği.
// D) Final kaydına elle atanmış boss (test verisi), geçersiz takvimle run'ın başlamaması, eski profilin çalışması.
[InitializeOnLoad]
public static class TakvimV1Verification
{
    const string Key = "TakvimV1Verification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string MenuPath = "Tools/Run Profili/Run50 Takvim V1 · 50 round (15 boss takvimi prototipi)";
    const string TempProfile = Profiles + "_Tmp372_GecersizTakvim.asset";
    const int Seed = 3722;
    const double Again = double.NaN;

    static readonly int[] ExpectedDates = { 3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50 };
    static readonly long[] ExpectedQuotas = { 27, 40, 88, 90, 104, 176, 180, 220, 400, 720, 1040, 2240, 2700, 3000, 4800 };
    static readonly long[] ExpectedBoss = { 5, 10, 16, 24, 33, 45, 57, 72, 100, 172, 276, 500, 680, 860, 1100 };
    static readonly long[] OldQuotas = { 45, 110, 150, 220, 300, 500, 1200, 2800, 4500, 6000 };
    static readonly long[] OldBoss = { 8, 16, 30, 45, 65, 100, 220, 500, 800 };

    static readonly List<string> notes = new();
    static readonly List<string> expectedLogs = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static TakvimV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: Takvim V1 seçilir ve boss seed'i sabitlenir (gerçek projedeki seçim ve asset'ler değişmez).
        foreach (string name in new[] { "Run50_TakvimV1", "Run50_KirilmaV1" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            profile.bossSeed = Seed; EditorUtility.SetDirty(profile);
        }
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_TakvimV1.asset");
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
                File.WriteAllLines("Logs/TakvimV1Verification.txt", new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 240) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now + .02; return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;   // editörün arama dizini (oyunla ilgisiz)
        if (type != LogType.Exception && type != LogType.Error) return;
        // Geçersiz takvim denemelerinin kendi hata mesajları beklenen çıktıdır; ayrı sayılır.
        if (type == LogType.Error && message.Contains("boss takvimi geçersiz")) { expectedLogs.Add(message); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        if (AssetDatabase.LoadAssetAtPath<RunProfileSO>(TempProfile) != null) AssetDatabase.DeleteAsset(TempProfile);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/TakvimV1Verification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    // Her round'da tekrarlanan ara kontroller: tutmazsa test düşer, tutarsa round'un özet satırına girer (ayrı satır yazılmaz).
    static void Must(bool c, string m) { if (!c) throw new Exception(m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");
    static bool Same(Color a, Color b, float eps = 0.02f) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b))) <= eps;
    static string N(long v) => HarvestQuota.Format(v);
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO T1 => Profile("Run50_TakvimV1");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static QuotaHUD Hud => Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
    static FrostZoneMarkers Markers => Object.FindAnyObjectByType<FrostZoneMarkers>();
    static BossWeatherOverlay Weather => Object.FindAnyObjectByType<BossWeatherOverlay>();
    static int TotalStacks => Boss.Taken.Sum(r => Boss.Stacks(r));

    static Dictionary<string, string> HudTexts()
    {
        Call(Hud, "Refresh");
        return Hud.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeSelf).GroupBy(t => t.name).ToDictionary(g => g.Key, g => Strip(g.First().text));
    }

    static string EndText()
    {
        var panel = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        return Strip(panel.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
    }

    static string ZoneKey(SegmentEventRuntime e) => string.Join(";", e.ZoneCells.OrderBy(c => c.x * 100 + c.z).Select(c => c.x + "," + c.z));

    static int OpenZoneCount(SegmentEventRuntime e)
    {
        var grid = GridManager.Instance.GetGridSystem();
        return e.ZoneCells.Count(p => { var g = grid.GetGridObject(p)?.GetGroundCellCached(); return g != null && !g.IsLocked; });
    }

    // Boss kuralının oynanışa etkisi kalmadı mı: bütün hücrelerde üretim aralığı ve bitki canı çarpanı 1.
    static bool NoBossEffect()
    {
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        {
            var cell = new GridPosition(x, z);
            if (SegmentEventDirector.SpawnIntervalMultiplier(cell) != 1f || SegmentEventDirector.SpawnHealthMultiplier(cell) != 1f) return false;
        }
        return true;
    }

    // Bekleyen kart seçimlerini ilk kartla geçer; seçim sayısını döner.
    static int ResolveCards()
    {
        int picks = 0;
        while (State == GameStates.CardSelection)
        {
            if (picks++ > 300) throw new Exception("card selection did not finish");
            Call(Cards, "OnCardSelected", Offers[0]);
        }
        return picks;
    }

    // ---------------------------------------------------------------- görüntü
    static RenderTexture target;

    static void Capture(string name)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        var roots = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        foreach (var canvas in roots.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var old = cam.targetTexture;
        cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG());
        Object.DestroyImmediate(png);
        Note("görüntü: Logs/" + name + ".png");
    }

    // ---------------------------------------------------------------- plan
    sealed class Scenario
    {
        public string Name; public int Last; public bool Detailed;
        public int FailRound; public long QuotaShort, BossShort;   // FailRound: o boss round'unda eksik bırakılan miktarlar
        public int[] LevelRounds = new int[0];                     // bu round'larda bir level kazanılır (bekleyen kart seçimi)
        public string Profile = "Run50_TakvimV1";
    }

    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static void Shot(string name) { Add("bekle " + name, () => .7); Add("görüntü " + name, () => { Capture(name); return .05; }); }

    static void Plan()
    {
        Add("veri", Data);
        Add("hatalı takvim verisi", Validation);
        Add("menü", Menu);
        // B) tam run
        var full = new Scenario { Name = "Tam run", Last = 50, Detailed = true, LevelRounds = new[] { 2, 3, 50 } };
        PlanRun(full);
        Add("zafer sonrası yeniden başlat", Restart);
        // C) kayıplar
        PlanRun(new Scenario { Name = "Kota bir eksik (R3)", Last = 3, FailRound = 3, QuotaShort = 1, LevelRounds = new[] { 3 } });
        Add("yeniden başlat", Restart);
        PlanRun(new Scenario { Name = "Boss hasadı bir eksik (R6)", Last = 6, FailRound = 6, BossShort = 1, LevelRounds = new[] { 6 } });
        Add("yeniden başlat", Restart);
        PlanRun(new Scenario { Name = "İkisi de eksik (R3)", Last = 3, FailRound = 3, QuotaShort = 1, BossShort = 1 });
        Add("yeniden başlat", Restart);
        PlanRun(new Scenario { Name = "R50'de kayıp", Last = 50, FailRound = 50, BossShort = 1, LevelRounds = new[] { 50 } });
        Add("ana menü", MainMenu);
        Add("menü kontrolü", MenuCheck);
        // D) final kaydına atanmış boss (test verisi), geçersiz takvim, eski profil
        Add("final kaydı: test profili", LoadMini);
        PlanRun(new Scenario { Name = "Final kaydı (test verisi, 6 round)", Last = 6, Profile = "mini" });
        Add("geçersiz takvimle sahne", LoadInvalid);
        Add("geçersiz takvim: run başlamaz", InvalidBlocked);
        Add("eski profil", LoadLegacy);
        Add("eski profil kontrolü", LegacyRuntime);
        Add("kapanış", FinalCheck);
    }

    static void PlanRun(Scenario s)
    {
        Add(s.Name + ": başlangıç", () => Begin(s));
        for (int r = 1; r <= s.Last; r++)
        {
            int round = r;
            if (s.Detailed && r == 1) Shot("T372_01_R1_Hazirlik_Onizleme");
            if (s.Detailed && r == 3) Shot("T372_03_R3_SonHazirlik");
            if (s.Detailed && r == 4) Shot("T372_05_R4_Hazirlik_SiradakiBoss");
            Add($"{s.Name} R{r} başla", () => Prep(round));
            if (s.Detailed && r == 1) Shot("T372_02_R1_HUD_YaklasanBoss");
            if (s.Detailed && r == 3) Shot("T372_04_R3_AktifBoss");
            if (s.Detailed && r == 50) Shot("T372_06_R50_AktifBoss");
            Add($"{s.Name} R{r} süre biter", () => { SetP(RM, "RemainingTime", 0f); return .03; });
            if (s.Detailed && r == s.Last)
            {
                Add("R50 round sonu", FinalEnd);
                Shot("T372_07_R50_LevelKartlari");
                Add("R50 kartlar", FinalCards);
                Shot("T372_08_R50_BossOdulu");
                Add("R50 ödül", FinalReward);
                Shot("T372_09_R50_Zafer");
                Add("R50 zafer", FinalVictory);
            }
            else Add($"{s.Name} R{r} bitiş", () => After(round));
            if (r == s.FailRound && s.Last == 50) Shot("T372_10_R50_Kayip");
        }
    }

    // ---------------------------------------------------------------- A) veri
    static long[] TransferQuota(IList<long> old, int n, IList<int> dates)
    {
        var result = new long[dates.Count]; long cumulative = 0, before = 0; int previous = 0;   // cumulative: round paylarının n katı
        for (int i = 0; i < dates.Count; i++)
        {
            for (int r = previous + 1; r <= dates[i]; r++) cumulative += old[(r - 1) / n];
            long rounded = (2 * cumulative + n) / (2 * n);
            result[i] = rounded - before; before = rounded; previous = dates[i];
        }
        return result;
    }

    static decimal BossExact(IList<long> old, int n, int round)
    {
        var xs = new List<int> { 0 }; var ys = new List<decimal> { 0 };
        for (int i = 0; i < old.Count; i++) { xs.Add((i + 1) * n); ys.Add(old[i]); }
        for (int i = 0; i + 1 < xs.Count; i++)
            if (round >= xs[i] && round <= xs[i + 1]) return ys[i] + (ys[i + 1] - ys[i]) * (round - xs[i]) / (xs[i + 1] - xs[i]);
        int m = xs.Count - 1;
        return ys[m] + (ys[m] - ys[m - 1]) * (round - xs[m]) / (xs[m] - xs[m - 1]);
    }

    static double Data()
    {
        var t = T1; var k = K1;
        Require(t != null && k != null && t != k && RunCalendar.Validate(t) == null, "Run50_TakvimV1 is a separate profile asset and its calendar is valid");
        Require(t.balance == k.balance && t.bossPool == k.bossPool && t.bossRewards == k.bossRewards && t.choicesPerLevel == k.choicesPerLevel && t.choicesPerLevel == 3 &&
                t.startingGold == k.startingGold && t.startingIron == k.startingIron && t.startingStone == k.startingStone && !t.debugBudget &&
                t.fixedRoundDuration == k.fixedRoundDuration && t.fixedRoundDuration == 45f && t.runLength == 50 && t.events.Count == 0 && t.specializationAfterSegment == 0,
            "Gameplay settings are Kırılma V1's: same balance set, boss pool and reward pool assets (referenced, not copied), 3 choices per level, same start budget, 45 s rounds");
        Require(k.bossCalendar.Count == 0 && k.segmentRounds == 5 && k.segmentTargets.SequenceEqual(OldQuotas) && k.bossTargets.SequenceEqual(OldBoss),
            "Kırılma V1 itself is unchanged: no calendar, 5-round segments, its own 10 quotas and 9 boss targets");
        Require(t.bossCalendar.Select(d => d.round).SequenceEqual(ExpectedDates) && t.bossCalendar.Count(d => d.final) == 1 && t.bossCalendar[14].final && t.bossCalendar.All(d => d.boss == null),
            "Calendar data: 15 boss dates " + string.Join(", ", ExpectedDates) + "; R50 is a separate 'final' entry; no date has a hand-picked boss (pool rule)");
        Require(t.segmentTargets.SequenceEqual(ExpectedQuotas) && t.bossTargets.SequenceEqual(ExpectedBoss), "Profile asset carries the 15 quota and 15 boss targets as explicit tables");
        // Aktarım kuralı testte bağımsız olarak yeniden hesaplanır (üreticinin Python hesabından ayrı).
        var quota = TransferQuota(k.segmentTargets, k.segmentRounds, ExpectedDates);
        var boss = ExpectedDates.Select(r => Math.Max(1L, (long)Math.Floor(BossExact(k.bossTargets, k.segmentRounds, r) + 0.5m))).ToArray();
        Require(quota.SequenceEqual(t.segmentTargets) && t.segmentTargets.Sum() == k.segmentTargets.Sum() && t.segmentTargets.Sum() == 15825,
            "Quota transfer recomputed from Kırılma V1's table gives the same 15 values; total budget kept: " + t.segmentTargets.Sum());
        Require(boss.SequenceEqual(t.bossTargets) && boss.All(v => v >= 1),
            "Boss target transfer recomputed (linear between old points, R0 = 0, last slope continued) gives the same 15 values: " + string.Join(" ", boss));
        Note("ara değerler: " + string.Join(" ", ExpectedDates.Select(r => $"R{r} {BossExact(k.bossTargets, k.segmentRounds, r):0.0}")));

        // Takvim: her round tam bir dönemde, dönemler bitişik, boss round'ları yalnız tarihler.
        var c = t.Calendar;
        var bounds = new List<(int first, int last)>();
        for (int p = 1; p <= 15; p++) bounds.Add((c.PeriodStart(p), c.PeriodEnd(p)));
        for (int round = 1; round <= 50; round++)
        {
            int owners = bounds.Count(b => round >= b.first && round <= b.last), p = c.PeriodOf(round);
            if (owners != 1 || round < bounds[p - 1].first || round > bounds[p - 1].last) throw new Exception($"round {round} belongs to {owners} periods");
            if (c.IsBossRound(round) != ExpectedDates.Contains(round) || c.IsPeriodEnd(round) != ExpectedDates.Contains(round)) throw new Exception($"boss round mismatch at {round}");
            if (c.IsPeriodStart(round) != (round == 1 || ExpectedDates.Contains(round - 1))) throw new Exception($"period start mismatch at {round}");
        }
        Require(c.IsExplicit && bounds[0] == (1, 3) && bounds[1] == (4, 6) && bounds[2] == (7, 10) && bounds[13] == (44, 46) && bounds[14] == (47, 50) &&
                Enumerable.Range(1, 14).All(i => bounds[i].first == bounds[i - 1].last + 1) && Enumerable.Range(1, 15).All(c.HasBoss) && !c.HasBoss(16) && !c.HasBoss(0),
            "Calendar: every round 1–50 belongs to exactly one period; periods " + string.Join(" ", bounds.Select(b => $"{b.first}–{b.last}")) + "; boss rounds are exactly the 15 dates");
        Require(Enumerable.Range(1, 15).All(p => c.QuotaTarget(p) == ExpectedQuotas[p - 1] && c.BossTarget(p) == ExpectedBoss[p - 1] && c.PeriodLength(p) == bounds[p - 1].last - bounds[p - 1].first + 1) &&
                c.IsFinal(15) && !c.IsFinal(14) && c.FixedBoss(15) == null && c.FinalRoundHasChoices && t.HasBoss(15) && t.BossTargetFor(15) == 1100 && t.TargetFor(1) == 27,
            "Period targets come from the tables by period index; R50 is the final entry; the last round keeps its choices (cards, reward) before victory");

        // Eski profiller: takvim boş → eski formüllerle birebir aynı sonuç (formüller burada elle yazılmıştır).
        int legacy = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RunProfileSO", new[] { "Assets/ScriptableObjects/RunProfiles" }))
        {
            var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (p == t) continue;
            // Bölüm 3.7.3 ve 3.7.4: Run50_OdulAsamalariV1 ve Run50_BedelliOdullerV1, Takvim V1'in takvimini ve hedeflerini aynen taşır
            // (yalnız ödül havuzu farklı).
            if (p.name == "Run50_OdulAsamalariV1" || p.name == "Run50_BedelliOdullerV1" || p.name == "Run50_KirilmaErisimiV1" || p.name == "Run50_ZincirV1" || p.name == "Run50_XPV1")
            {
                if (!p.bossCalendar.Select(d => d.round).SequenceEqual(ExpectedDates) || !p.segmentTargets.SequenceEqual(ExpectedQuotas) || !p.bossTargets.SequenceEqual(ExpectedBoss) || RunCalendar.Validate(p) != null)
                    throw new Exception(p.name + " should carry Takvim V1's calendar and targets unchanged");
                Note($"açık takvimli profil · {p.name}: Takvim V1 ile aynı 15 tarih ve aynı hedefler");
                continue;
            }
            // Bölüm 3.7.8: Run50_AlphaDengeV1 aynı 15 tarihi taşır; kota ve boss hedefleri kendi tablosudur (P8 ayarı), geçerli olmalıdır.
            if (p.name == "Run50_AlphaDengeV1")
            {
                if (!p.bossCalendar.Select(d => d.round).SequenceEqual(ExpectedDates) || p.segmentTargets.Count != 15 || p.bossTargets.Count != 15 || RunCalendar.Validate(p) != null ||
                    !p.Calendar.IsFinal(15) || !p.Calendar.FinalRoundHasChoices)
                    throw new Exception(p.name + " should carry Takvim V1's 15 dates with a valid target table of its own");
                Note($"açık takvimli profil · {p.name}: Takvim V1 ile aynı 15 tarih; hedefler kendi tablosu (geçerli)");
                continue;
            }
            var pc = p.Calendar; int n = Math.Max(1, p.segmentRounds);
            if (pc.IsExplicit || p.bossCalendar.Count != 0 || RunCalendar.Validate(p) != null || pc.FinalRoundHasChoices) throw new Exception(p.name + " should use the uniform rhythm");
            bool OldHasBoss(int s) => p.bossPool != null && s >= 1 && s * n < p.runLength;
            for (int round = 1; round <= p.runLength + 7; round++)
            {
                int s = (Math.Max(1, round) - 1) / n + 1;
                bool end = round % n == 0;
                if (pc.PeriodOf(round) != s || pc.PeriodEnd(s) != s * n || pc.PeriodStart(s) != (s - 1) * n + 1 || pc.IsPeriodEnd(round) != end ||
                    pc.IsPeriodStart(round) != ((round - 1) % n == 0) || pc.IsBossRound(round) != (end && OldHasBoss(s)) || pc.HasBoss(s) != OldHasBoss(s) ||
                    pc.QuotaTarget(s) != p.TargetFor(s) || pc.PeriodLength(s) != n ||
                    pc.BossTarget(s) != (OldHasBoss(s) && s <= p.bossTargets.Count && p.bossTargets[s - 1] > 0 ? p.bossTargets[s - 1] : 0))
                    throw new Exception($"{p.name}: calendar differs from the old formula at round {round}");
            }
            Note($"eski profil · {p.name}: {p.runLength} round, {n} round'luk segment, boss {(p.bossPool != null ? "son segment hariç her segment sonu" : "yok")} — eski formülle aynı");
            legacy++;
        }
        Require(legacy == 9, "The 9 older profiles have no calendar and resolve periods, boss rounds and targets exactly as the old formulas did (checked for every round)");
        var timing = SegmentEventTiming.BossRound(3, 5, 9); var whole = SegmentEventTiming.WholeSegment(2, 5); var whole1 = SegmentEventTiming.WholeSegment(1, 5);
        Require(timing.StartRound == 15 && timing.EndRound == 15 && timing.AnnounceRound == 11 && timing.BossRoundOnly && timing.Seed == 9 &&
                whole.StartRound == 6 && whole.EndRound == 10 && whole.AnnounceRound == 1 && !whole.BossRoundOnly && whole1.StartRound == 1 && whole1.EndRound == 5 && whole1.AnnounceRound == 1,
            "Old event timings unchanged: boss of segment 3 → announced R11, active R15; whole-segment event of segment 2 → R6–10, announced R1");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(x => x.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);
        return .05;
    }

    static string Bad(Action<RunProfileSO> change)
    {
        var copy = Object.Instantiate(T1);
        change(copy);
        string error = RunCalendar.Validate(copy);
        Object.DestroyImmediate(copy);
        return error;
    }

    static void Set(RunProfileSO p, int index, int round, bool final = false) => p.bossCalendar[index] = new BossDate { round = round, final = final };

    static double Validation()
    {
        var cases = new (string name, Action<RunProfileSO> change, string expect)[]
        {
            ("sıralı değil (… 6, 3 …)", p => { Set(p, 0, 6); Set(p, 1, 3); }, "sıralı değil"),
            ("aynı round iki kez (3, 3)", p => Set(p, 1, 3), "iki kez"),
            ("round 0", p => Set(p, 0, 0), "sınırlarının dışında"),
            ("round 51 (run 50 round)", p => Set(p, 14, 51, true), "sınırlarının dışında"),
            ("son tarih 49", p => Set(p, 14, 49, true), "son tarih run'ın son round'u olmalı"),
            ("son tarih eksik (… 46)", p => { p.bossCalendar.RemoveAt(14); p.segmentTargets.RemoveAt(14); p.bossTargets.RemoveAt(14); }, "son tarih run'ın son round'u olmalı"),
            ("final kaydı ortada", p => Set(p, 5, 20, true), "final kaydı"),
            ("kota tablosu kısa", p => p.segmentTargets.RemoveAt(14), "kota tablosunda"),
            ("kota 0", p => p.segmentTargets[4] = 0, "0'dan büyük olmalı"),
            ("boss hedefi tablosu kısa", p => p.bossTargets.RemoveAt(14), "boss hasadı tablosunda"),
            ("negatif boss hedefi", p => p.bossTargets[2] = -1, "negatif"),
            ("boss havuzu yok", p => p.bossPool = null, "boss havuzu"),
            ("eski olay listesi dolu", p => p.events.Add(new SegmentEventEntry { segment = 2, segmentEvent = T1.bossPool.entries[0].boss }), "Segment olayları"),
        };
        foreach (var (name, change, expect) in cases)
        {
            string error = Bad(change);
            if (error == null || !error.Contains(expect)) throw new Exception($"validation case '{name}' gave: {error ?? "no error"}");
            Note($"hatalı veri · {name} → \"{error}\"");
        }
        Require(true, $"{cases.Length} invalid calendars are each rejected with a message naming the problem (nothing is corrected silently)");
        Require(Bad(p => { }) == null && Bad(p => p.bossTargets[3] = 0) == null && Bad(p => Set(p, 14, 50)) == null,
            "Valid variants pass: the profile as is, a boss with target 0 (boss without a harvest target), a calendar without a 'final' mark");
        return .05;
    }

    // Menü: geçersiz takvimli profil seçilmez, seçim olduğu gibi kalır (yalnız izole kopyada, geçici asset silinir).
    static double Menu()
    {
        var select = typeof(RunProfileMenu).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Static);
        var before = Selection.active;
        var bad = Object.Instantiate(T1); bad.name = "_Tmp372_GecersizTakvim"; Set(bad, 1, 2);
        AssetDatabase.CreateAsset(bad, TempProfile);
        int logs = expectedLogs.Count;
        select.Invoke(null, new object[] { TempProfile });
        bool refused = Selection.active == before && expectedLogs.Skip(logs).Any(m => m.Contains("Run profili seçilmedi") && m.Contains("sıralı değil"));
        AssetDatabase.DeleteAsset(TempProfile);
        Require(before == T1 && refused && AssetDatabase.LoadAssetAtPath<RunProfileSO>(TempProfile) == null,
            "Tools > Run Profili refuses a profile with an invalid calendar: error logged, selection unchanged (temporary asset removed)");
        select.Invoke(null, new object[] { Profiles + "Run50_TakvimV1.asset" });
        Require(Selection.active == T1, "The valid Takvim V1 profile is selected through the same menu path");
        return .05;
    }

    // ---------------------------------------------------------------- B–C) run
    static Scenario S;
    static int[] D; static long[] Q, BT;
    static long expectedTotal; static int rewards, emptyOffers, baseModifiers = -1;
    static readonly List<int> activeRounds = new();
    static readonly List<SegmentEventSO> bossOrder = new();
    static SegmentEventRuntime previewed; static SegmentEventSO previewData; static string previewZone;
    static RunProfileSO miniProfile, invalidProfile; static SegmentEventSO finalBoss;

    static int Period(int round) { for (int i = 0; i < D.Length; i++) if (round <= D[i]) return i + 1; return D.Length; }
    static int First(int p) => p == 1 ? 1 : D[p - 2] + 1;

    static double Begin(Scenario s)
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup) return Again;
        var profile = s.Profile == "mini" ? miniProfile : T1;
        if (RM.Profile != profile) return Again;
        S = s;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        D = profile.bossCalendar.Select(d => d.round).ToArray(); Q = profile.segmentTargets.ToArray(); BT = profile.bossTargets.ToArray();
        Events.SendMessage("LateUpdate");
        var up = Events.Upcoming;
        // Temiz başlangıç: önceki run'dan boss, teklif, ödül ya da sayaç kalmamış.
        Must(RM.CurrentRound == 1 && RM.IsPreparingFirstRound && !RM.IsRoundActive && RM.Outcome == RunOutcome.None && !RM.RunFailed && RM.CalendarError == null &&
             RM.LastQuotaRound == 0 && RM.LastBossRound == 0 && RM.QuotaProgress == 0 && RM.PendingCardSelections == 0 && RM.LevelsGained == 0 && Total == 0,
            s.Name + ": run does not start clean (round / quota / score counters)");
        Must(Boss.Taken.Count == 0 && !Boss.IsPending && Boss.OwnedModifiers.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f && BossRewardManager.BehaviorDamageMultiplier == 1f &&
             !RunPower.Rhythm.Enabled, s.Name + ": a boss reward leaked into the new run");
        Must(Events.BossMode && Events.RunSeed == Seed && Events.Active == null && Events.LastEnded == null && Events.Events.Count == 1 && up != null && up.Segment == 1 &&
             up.StartRound == D[0] && up.AnnounceRound == 1, s.Name + ": boss state does not start clean (only the first boss announced)");
        int modifiers = StatManager.Instance.GlobalModifiers.Count;
        if (baseModifiers < 0) baseModifiers = modifiers;
        Must(modifiers == baseModifiers, $"{s.Name}: {modifiers} global modifiers at run start, the first run had {baseModifiers}");
        Markers.SendMessage("LateUpdate"); Weather.SendMessage("LateUpdate");
        Must(!Markers.ShowsActive && Weather.CurrentWeather == BossWeatherOverlay.Weather.None, s.Name + ": zone / weather active before the first round");
        expectedTotal = 0; rewards = 0; emptyOffers = 0; activeRounds.Clear(); bossOrder.Clear(); previewed = null;
        Require(true, $"{s.Name}: starts clean in RunSetup ({profile.name}, round 1, score 0, no reward, no active boss; first boss {up.Data.displayName} announced for Round {up.StartRound})");
        return .05;
    }

    // Round'un hazırlığı (önizleme kontrolleri) ve başlaması; skor ve level burada verilir. Round'u bitiren ayrı adımdır.
    static double Prep(int r)
    {
        int p = Period(r), first = First(p), B = D[p - 1];
        bool boss = r == B, failing = r == S.FailRound || (S.FailRound > 0 && Period(S.FailRound) == p);
        Must(State == (r == 1 ? GameStates.RunSetup : GameStates.RoundEnd) && RM.CurrentRound == Math.Max(1, r - 1), $"R{r}: unexpected state before the round ({State}, round {RM.CurrentRound})");
        Events.SendMessage("LateUpdate");
        var up = Events.Upcoming;
        Must(up != null && up.BossRoundOnly && up.IsPrepared && !up.IsActive && up.Segment == p && up.StartRound == B && up.EndRound == B && up.AnnounceRound == first && Events.Active == null,
            $"R{r}: upcoming boss should be period {p}'s boss at Round {B}");
        if (r == first)
        {
            previewed = up; previewData = up.Data; previewZone = ZoneKey(up);
            if (bossOrder.Count > 0 && bossOrder[bossOrder.Count - 1] == up.Data && Events.LastPickNote != "tekrar") throw new Exception($"R{r}: the same boss twice in a row without the pool's repeat note");
            bossOrder.Add(up.Data);
        }
        else Must(up == previewed && up.Data == previewData && ZoneKey(up) == previewZone, $"R{r}: the previewed boss or its zone changed during the period");

        // Round özeti / hazırlık bildirimi: doğru yaklaşan boss round'u ve kalan hazırlık.
        string notice = Strip(SegmentEventText.Notice(Events, r - 1));
        Must(notice != null && notice.Contains(up.Data.displayName) && (boss ? notice.Contains("SON HAZIRLIK") : notice.Contains($"Round {B}")) &&
             (boss || r == first && r > 1 || notice.Contains($"{B - r} round hazırlık")), $"R{r}: summary notice does not name the boss round {B}: {notice}");
        string quotaNotice = Strip(RoundSummaryUI.QuotaNotice(RM, r - 1, out _));
        int length = B - first + 1;
        if (r == 1) Must(quotaNotice.Contains($"İlk kota: {N(Q[0])} · {length} round") && quotaNotice.Contains("boss round'unun sonunda"), "R1 quota notice: " + quotaNotice);
        else if (r == first) Must(quotaNotice.Contains("KOTA TAMAM") && quotaNotice.Contains($"Sıradaki kota: {N(Q[p - 1])} · {length} round"), $"R{r} quota notice: {quotaNotice}");
        else if (!boss) Must(quotaNotice.Contains($"{B - r + 1} round kaldı") && quotaNotice.Contains($"{B}. round sonunda"), $"R{r} quota notice: {quotaNotice}");
        else Must(quotaNotice.Contains("SON ROUND"), $"R{r} quota notice: {quotaNotice}");
        if (S.Detailed && r == first)
        {
            var map = Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
            map.BuildGrid();
            var marked = map.GetComponentsInChildren<TileCellUI>(false).Where(c => c.InEventZone).ToList();
            Must(marked.Count == OpenZoneCount(up) && marked.All(c => !c.EventZoneActive), $"R{r}: round map shows {marked.Count} preview cells, the zone has {OpenZoneCount(up)}");
            Markers.SendMessage("LateUpdate");
            Must(!Markers.ShowsActive && Markers.ShownCells.Count == OpenZoneCount(up), $"R{r}: field markers do not show the upcoming zone as a preview");
        }

        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == r && RM.IsRoundActive, $"R{r}: round did not start");
        SetP(RM, "RemainingTime", 1000f);
        Must(RM.IsBossRound(r) == boss && RM.IsQuotaSegmentEnd(r) == boss && RM.QuotaSegment == p && RM.QuotaSegmentEnd == B && RM.QuotaTarget == Q[p - 1] && RM.BossTarget == BT[p - 1],
            $"R{r}: period {p} / boss round {B} / targets not resolved");
        if (boss)
        {
            Must(Events.Active == previewed && previewed.IsActive && Events.Active.Data == previewData && ZoneKey(Events.Active) == previewZone && Events.Upcoming == null &&
                 Events.Events.Count(e => e.IsActive) == 1 && Events.Events.Count(e => e.StartRound == r) == 1, $"R{r}: the previewed boss should be the single active boss, unchanged");
            activeRounds.Add(r);
        }
        else Must(Events.Active == null && Events.Events.All(e => !e.IsActive) && Events.Upcoming == previewed, $"R{r}: no boss should be active outside the boss dates");
        if (r == first) Must(RM.QuotaProgress == 0 && Total == expectedTotal, $"R{r}: period score should restart at 0 while the total score ({expectedTotal}) is kept");
        if (!boss) Must(NoBossEffect(), $"R{r}: a boss rule affects the field outside its boss round");
        else if (previewed.ZoneCells.Count > 0)
            Must(previewed.ZoneCells.Any(c => SegmentEventDirector.SpawnIntervalMultiplier(c) != 1f || SegmentEventDirector.SpawnHealthMultiplier(c) != 1f),
                $"R{r}: the active boss rule does not affect its zone");
        if (S.Detailed && r == first && r > 1)
            Require(Events.LastEnded != null && Events.LastEnded.StartRound == r - 1 && Events.LastEnded.IsFinished,
                $"Geçiş R{r - 1}→R{r}: {Events.LastEnded.Data.displayName} bitti; yeni dönem {first}–{B} ({B - first + 1} round) dönem skoru 0'dan başladı, kota {N(Q[p - 1])}; " +
                $"toplam skor {N(Total)} korundu; önizleme {previewData.displayName} · Round {B} (boss hasadı hedefi {N(BT[p - 1])})");
        Markers.SendMessage("LateUpdate"); Weather.SendMessage("LateUpdate");
        Must(Markers.ShowsActive == (boss && OpenZoneCount(previewed) > 0) && Weather.CurrentWeather == BossWeatherOverlay.Resolve(Events.Active, true) &&
             (boss && !(previewData is NoRuleBossSO)) == (Weather.CurrentWeather != BossWeatherOverlay.Weather.None), $"R{r}: zone / weather state wrong ({Weather.CurrentWeather})");

        // HUD: kalan round dönemin uzunluğuna göre, yaklaşan boss'un round'u, boss hedefi; kâğıt yalnız boss aktifken boyanır.
        var hud = HudTexts();
        long progress = RM.QuotaProgress;
        Must(hud["Label"] == (boss ? "SEGMENT KOTASI · SON ROUND" : $"SEGMENT KOTASI · {B - r + 1} ROUND") && hud["Value"] == $"{N(progress)} / {N(Q[p - 1])}", $"R{r}: HUD quota line {hud["Label"]} {hud["Value"]}");
        Must(boss ? hud["Event"].StartsWith(previewData.displayName + ":") && hud["Boss"] == $"BOSS HASADI: 0 / {N(BT[p - 1])}"
                  : hud["Event"].StartsWith($"Yaklaşan boss: {previewData.displayName} · Round {B}") && hud["Boss"] == $"Boss hasadı hedefi (Round {B}): {N(BT[p - 1])}",
            $"R{r}: HUD boss lines: {hud["Event"]} | {hud["Boss"]}");
        Must(Same(Hud.PaperColor, BossTheme.Paper(boss ? previewData : null)) && Same(Hud.EventLineColor, BossTheme.Ink(previewData)), $"R{r}: HUD colors");
        string intro = GameFeelDirector.QuotaIntro(RM), eventIntro = SegmentEventText.Intro(Events, RM);
        if (r == first) Must(intro == $"KOTA {N(Q[p - 1])} · {length} ROUND", $"R{r}: quota intro {intro}");
        if (boss) Must(eventIntro == $"BOSS: {previewData.displayName} · HASAT HEDEFİ {N(BT[p - 1])}", $"R{r}: boss intro {eventIntro}");
        if (r == first && r > 1) Must(eventIntro != null && eventIntro.Contains("GEÇTİ") && eventIntro.EndsWith($"· KOTA {N(Q[p - 1])}"), $"R{r}: intro after boss {eventIntro}");

        // Skor: dönemin ilk round'unda (kota − boss hedefi), boss round'unda tam boss hedefi → iki koşul da tam eşitlikle tutar.
        long quotaShort = failing ? S.QuotaShort : 0, bossShort = failing ? S.BossShort : 0;
        long add = 0;
        if (r == first) add += (Q[p - 1] - quotaShort) - (BT[p - 1] - bossShort);
        if (boss) add += BT[p - 1] - bossShort;
        AddScore(add); expectedTotal += add;
        if (boss)
        {
            Must(RM.BossProgress == BT[p - 1] - bossShort && RM.QuotaProgress == Q[p - 1] - quotaShort, $"R{r}: boss round progress {RM.BossProgress} / period {RM.QuotaProgress}");
            if (!failing)
            {
                hud = HudTexts();
                Must(hud["Label"] == "SEGMENT KOTASI TAMAM" && hud["Boss"] == $"BOSS HASADI: {N(BT[p - 1])} / {N(BT[p - 1])} · TAMAM", $"R{r}: HUD at exact targets: {hud["Label"]} | {hud["Boss"]}");
            }
        }
        if (S.LevelRounds.Contains(r))
        {
            Progress.AddXP(Progress.XPToNextLevel);
            Must(RM.PendingCardSelections == RM.ChoicesPerLevel && RM.ChoicesPerLevel == 3, $"R{r}: one level should queue 3 card choices");
        }
        if (S.Detailed || boss)
            Require(true, $"{S.Name} · R{r} (dönem {p}: {first}–{B}{(boss ? ", BOSS " + previewData.displayName : ", hazırlık")}): önizleme Round {B}, HUD \"{hud["Label"]}\", " +
                          $"dönem skoru {N(RM.QuotaProgress)} / {N(Q[p - 1])}{(boss ? $", boss hasadı {N(RM.BossProgress)} / {N(BT[p - 1])}" : "")}, toplam {N(Total)}");
        return .05;
    }

    static double After(int r)
    {
        if (RM.IsRoundActive) return Again;
        int p = Period(r), B = D[p - 1];
        bool boss = r == B, cards = S.LevelRounds.Contains(r);
        Must(Total == expectedTotal, $"R{r}: total score {Total}, expected {expectedTotal} (nothing but the test should add score)");
        if (!boss)
        {
            if (cards) { Must(State == GameStates.CardSelection && !Boss.IsPending, $"R{r}: card selection expected"); Must(ResolveCards() == 3, $"R{r}: three card choices"); }
            Must(State == GameStates.RoundEnd && RM.LastQuotaRound != r && RM.LastBossRound != r && !RM.RunFailed && RM.Outcome == RunOutcome.None && !Boss.IsPending && Events.Active == null,
                $"R{r}: an ordinary round should end in the round summary without any evaluation ({State})");
            if (cards) Require(true, $"{S.Name} · R{r} bitti: level kartları (3 seçim) → round özeti; kota değerlendirmesi yok");
            return .05;
        }
        Must(RM.LastQuotaRound == r && RM.LastQuotaTarget == Q[p - 1] && RM.LastBossRound == r && RM.LastBossTarget == BT[p - 1], $"R{r}: quota and boss should both be evaluated at the period end");
        if (r == S.FailRound) return Failed(r, p);

        Must(RM.LastQuotaScore == Q[p - 1] && RM.LastBossScore == BT[p - 1] && !RM.EndedByQuota && !RM.EndedByBoss && !RM.RunFailed && Boss.IsPending,
            $"R{r}: exact quota ({RM.LastQuotaScore}) and exact boss harvest ({RM.LastBossScore}) should pass");
        if (cards)
        {
            Must(State == GameStates.CardSelection && RM.PendingCardSelections == 3, $"R{r}: cards should come before the boss reward");
            Must(ResolveCards() == 3, $"R{r}: three card choices");
        }
        Must(State == GameStates.RoundChoice && RM.Outcome == RunOutcome.None, $"R{r}: boss reward selection expected ({State})");
        // Seçim tamamlanmadan sonraki round başlamaz.
        RM.StartNextRound();
        Must(State == GameStates.RoundChoice && RM.CurrentRound == r && !RM.IsRoundActive, $"R{r}: the next round started before the reward was chosen");
        string rewardName = TakeReward(r);
        bool last = r >= RM.MaxRounds;
        if (last)
        {
            Must(State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory, $"R{r}: victory expected after the last reward ({State})");
            RM.StartNextRound();
            Must(RM.CurrentRound == r && State == GameStates.RunComplete, $"R{r}: a round started after the run ended");
            string end = EndText();
            Require(activeRounds.SequenceEqual(D) && (rewardName == null || end.Contains(rewardName)) && end.Contains(RM.Profile.victoryTitle),
                $"{S.Name} · R{r} son boss: ödül ({rewardName ?? "yok"}) → zafer; aktif boss round'ları {string.Join(", ", activeRounds)}; run sonu: {end}");
            return .3;
        }
        Must(State == GameStates.RoundEnd, $"R{r}: round summary expected after the reward ({State})");
        Events.SendMessage("LateUpdate");
        var next = Events.Upcoming;
        Must(Events.Active == null && Events.LastEnded != null && Events.LastEnded.Data == previewData && Events.LastEndedRound == r && Events.LastEnded.IsFinished && !Events.LastEnded.IsActive &&
             next != null && next.Segment == p + 1 && next.StartRound == D[p] && next.AnnounceRound == r + 1, $"R{r}: boss should be over and the next period's boss announced for Round {D[p]}");
        Markers.SendMessage("LateUpdate"); Weather.SendMessage("LateUpdate");
        Must(!Markers.ShowsActive && Weather.CurrentWeather == BossWeatherOverlay.Weather.None && NoBossEffect(), $"R{r}: zone / weather / boss rule still active after the boss");
        string notice = Strip(SegmentEventText.Notice(Events, r));
        Must(notice.Contains(previewData.displayName + " GEÇTİ") && notice.Contains($"boss hasadı {N(BT[p - 1])} / {N(BT[p - 1])}") && notice.Contains($"Sıradaki boss: {next.Data.displayName} (Round {D[p]})"),
            $"R{r}: summary after the boss: {notice}");
        Require(true, $"{S.Name} · R{r} boss geçildi: kota {N(RM.LastQuotaScore)} / {N(RM.LastQuotaTarget)} (eşit), boss hasadı {N(RM.LastBossScore)} / {N(RM.LastBossTarget)} (eşit)" +
                      $"{(cards ? ", önce 3 kart" : "")}, ödül: {rewardName ?? "uygun ödül yok"}; etkiler temizlendi; sıradaki boss {next.Data.displayName} Round {D[p]}");
        return .05;
    }

    // Tek ödül alınır; ikinci istek ve "ödülsüz devam" reddedilir. Dönüş: alınan ödülün adı (uygun ödül yoksa null).
    static string TakeReward(int r)
    {
        var offer = Boss.Offer.ToList();
        Must(offer.Count <= 3 && offer.Distinct().Count() == offer.Count, $"R{r}: offer should hold up to three different rewards");
        Must(Boss.Offer.SequenceEqual(offer), $"R{r}: the offer changed when it was read again");
        int before = TotalStacks;
        if (offer.Count == 0)
        {
            emptyOffers++;
            Must(Boss.ContinueWithoutReward() && !Boss.ContinueWithoutReward() && TotalStacks == before, $"R{r}: empty offer should be passed once");
            return null;
        }
        Must(!Boss.ContinueWithoutReward(), $"R{r}: 'continue without reward' accepted while rewards are offered");
        Must(Boss.Choose(offer[0]) && TotalStacks == before + 1, $"R{r}: reward not taken");
        Must(!Boss.Choose(offer[offer.Count - 1]) && !Boss.Choose(offer[0]) && !Boss.ContinueWithoutReward() && TotalStacks == before + 1 && !Boss.IsPending, $"R{r}: a second reward was accepted for the same boss");
        rewards++;
        return offer[0].displayName;
    }

    // Kayıp: kart ve ödül açılmadan run biter; doğru neden; sonraki round başlamaz.
    static double Failed(int r, int p)
    {
        bool quota = S.QuotaShort > 0, bossFail = S.BossShort > 0;
        Must(RM.LastQuotaScore == Q[p - 1] - S.QuotaShort && RM.LastBossScore == BT[p - 1] - S.BossShort, $"R{r}: scores {RM.LastQuotaScore} / {RM.LastBossScore}");
        Must(State == GameStates.RunComplete && RM.RunFailed && RM.EndedByQuota == quota && RM.EndedByBoss == bossFail &&
             RM.Outcome == (quota ? RunOutcome.QuotaFailed : RunOutcome.BossFailed), $"R{r}: wrong loss reason (quota {RM.EndedByQuota}, boss {RM.EndedByBoss}, {RM.Outcome}, {State})");
        var rewardPanel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        Must(RM.PendingCardSelections == 0 && !Boss.IsPending && Boss.Offer.Count == 0 && TotalStacks == rewards && !RM.IsRoundChoicePending &&
             !Cards.gameObject.activeInHierarchy && (rewardPanel == null || !rewardPanel.gameObject.activeInHierarchy), $"R{r}: a card or reward choice opened on a lost run");
        Must(!Boss.Choose(K1.bossRewards.rewards[0]) && !Boss.ContinueWithoutReward() && TotalStacks == rewards, $"R{r}: a reward could be taken after the loss");
        Must(Events.Active == null && Events.Upcoming == null && Events.Events.Count == 0, $"R{r}: boss state not cleared on the loss");
        Weather.SendMessage("LateUpdate"); Markers.SendMessage("LateUpdate");
        Must(Weather.CurrentWeather == BossWeatherOverlay.Weather.None && !Markers.ShowsActive && Markers.ShownCells.Count == 0 && NoBossEffect(), $"R{r}: weather / zone / boss rule still present on the loss screen");
        RM.StartNextRound();
        Must(RM.CurrentRound == r && State == GameStates.RunComplete && !RM.IsRoundActive, $"R{r}: a round started after the loss");
        string end = EndText();
        string title = quota && bossFail ? "KOTA VE BOSS HASADI TUTMADI" : quota ? "KOTA TUTMADI" : "BOSS HASADI TUTMADI";
        string q = $"{N(Q[p - 1] - S.QuotaShort)} / {N(Q[p - 1])}", b = $"{N(BT[p - 1] - S.BossShort)} / {N(BT[p - 1])}";
        bool text = end.StartsWith(title) && end.Contains($"Round {r}") &&
            (quota && !bossFail ? end.Contains($"segment skoru {q} (gereken kota)") && end.Contains($"boss hasadı {b} (tamam)")
             : !quota ? end.Contains($"boss hasadı {b} (eksik)") && end.Contains($"segment kotası {q} (tamam)")
             : end.Contains($"boss hasadı {b} (eksik)") && end.Contains($"segment kotası {q} (eksik)"));
        Require(text && activeRounds.SequenceEqual(D.Where(d => d <= r)),
            $"{S.Name}: kota {q}, boss hasadı {b} → {RM.Outcome}; bekleyen kart seçimi ve ödül açılmadı ({(S.LevelRounds.Contains(r) ? "bir level bekliyordu" : "level yoktu")}), " +
            $"sonraki round başlamadı; ekran: {end}");
        return .3;
    }

    // ---------------------------------------------------------------- R50 başarı sırası (tam run): koşullar → kartlar → boss ödülü → zafer
    static string finalReward;

    static double FinalEnd()
    {
        if (RM.IsRoundActive) return Again;
        Must(RM.CurrentRound == 50 && RM.MaxRounds == 50 && Total == expectedTotal && RM.LastQuotaRound == 50 && RM.LastQuotaScore == Q[14] && RM.LastBossRound == 50 && RM.LastBossScore == BT[14] &&
             !RM.RunFailed, "R50: quota and boss harvest should both pass at exact targets");
        Require(State == GameStates.CardSelection && RM.PendingCardSelections == 3 && Boss.IsPending && RM.Outcome == RunOutcome.None,
            "R50 passed (exact quota 4.800 / 4.800 and boss harvest 1.100 / 1.100): the pending level cards open first; the boss reward waits; no victory screen yet");
        RM.StartNextRound();
        Require(RM.CurrentRound == 50 && State == GameStates.CardSelection, "R50: the next round cannot start while card choices are pending");
        return .05;
    }

    static double FinalCards()
    {
        Require(ResolveCards() == 3 && State == GameStates.RoundChoice && Boss.IsPending && RM.Outcome == RunOutcome.None,
            "R50: after the three card choices the boss reward selection opens (still no victory screen)");
        return .05;
    }

    static double FinalReward()
    {
        var panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        string Own(string name) => panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.transform.parent == panel.transform && t.name == name).Select(t => t.text).FirstOrDefault();
        string title = Own("Title"), subtitle = Own("Subtitle");
        Require(panel.gameObject.activeInHierarchy && title == "SON BOSS GEÇİLDİ · ÖDÜL SEÇ" && subtitle != null && subtitle.Contains("run burada biter") && !subtitle.Contains("Round 51"),
            $"R50 reward panel: \"{title}\" / \"{subtitle}\" (does not promise a Round 51)");
        RM.StartNextRound();
        Must(RM.CurrentRound == 50 && State == GameStates.RoundChoice, "R50: a round started before the final reward was chosen");
        int before = TotalStacks;
        finalReward = TakeReward(50);
        Require(finalReward != null && TotalStacks == before + 1 && State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory && !Boss.IsPending,
            $"R50: one reward taken ({finalReward}); a second request and 'continue' are rejected; then the victory screen");
        Require(Events.Active == null && Events.Events.Count == 0 && activeRounds.SequenceEqual(ExpectedDates) && activeRounds.Count == 15 && bossOrder.Count == 15,
            "Whole run R1–50 played in order: exactly 15 boss activations, on rounds " + string.Join(", ", activeRounds) + "; no boss active on any other round");
        Note("boss sırası: " + string.Join(" → ", bossOrder.Select(b => b.displayName)));
        Require(rewards + emptyOffers == 15 && rewards == TotalStacks, $"15 bosses gave 15 reward selections: {rewards} rewards taken, {emptyOffers} empty offers; never more than one per boss");
        Require(expectedTotal == ExpectedQuotas.Sum() && Total == 15825, "Total run score was never reset by a period change: " + N(Total) + " = sum of the 15 period quotas");
        return .05;
    }

    static double FinalVictory()
    {
        string end = EndText();
        Require(end.StartsWith("RUN TAMAMLANDI · TAKVİM V1") && end.Contains("50 round") && end.Contains("Boss ödülleri:") && end.Contains(finalReward),
            "Victory screen lists the rewards of the run, including the one taken at R50: " + end);
        RM.StartNextRound();
        Weather.SendMessage("LateUpdate"); Markers.SendMessage("LateUpdate");
        Require(RM.CurrentRound == 50 && State == GameStates.RunComplete && !RM.IsRoundActive && Weather.CurrentWeather == BossWeatherOverlay.Weather.None && Markers.ShownCells.Count == 0 && NoBossEffect(),
            "R51 does not start; boss weather, zone and rule are cleared on the victory screen");
        return .05;
    }

    // ---------------------------------------------------------------- sahne geçişleri
    static double Restart()
    {
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 2;
    }

    static double MainMenu()
    {
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static double MenuCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && SegmentEventDirector.Instance == null && BossRewardManager.Instance == null &&
                Object.FindAnyObjectByType<FrostZoneMarkers>() == null && BossRewardManager.DirectDamageMultiplier == 1f && SegmentEventDirector.SpawnIntervalMultiplier(new GridPosition(5, 5)) == 1f,
            "Return to the main menu after the R50 loss: no boss, zone, offer or reward effect survives the scene change");
        return .05;
    }

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    // Test verisi: 6 round'luk takvim; R6 "final" kaydı ve o tarihe elle atanmış ayrı bir boss (havuzda olmayan kopya).
    static double LoadMini()
    {
        var don = T1.bossPool.entries.Select(e => e.boss).OfType<FrostFrontSO>().First();
        finalBoss = Object.Instantiate(don); finalBoss.name = "Final (test)"; finalBoss.displayName = "FİNAL TEST BOSS'U";
        miniProfile = Object.Instantiate(T1); miniProfile.name = "Takvim (test: final kaydı)"; miniProfile.displayName = "Takvim testi · 6 round";
        miniProfile.runLength = 6; miniProfile.victoryTitle = "TEST RUN'I TAMAMLANDI";
        miniProfile.bossCalendar = new List<BossDate> { new BossDate { round = 3 }, new BossDate { round = 6, final = true, boss = finalBoss } };
        miniProfile.segmentTargets = new List<long> { 27, 40 }; miniProfile.bossTargets = new List<long> { 5, 10 };
        Require(RunCalendar.Validate(miniProfile) == null && miniProfile.Calendar.FixedBoss(2) == finalBoss && miniProfile.Calendar.IsFinal(2) && miniProfile.Calendar.FixedBoss(1) == null,
            "Test data: a 6-round calendar (R3, R6) whose final entry R6 names its own boss, separate from the pool");
        return Load(miniProfile);
    }

    static double LoadInvalid()
    {
        Require(bossOrder.Count == 2 && bossOrder[1] == finalBoss && bossOrder[0] != finalBoss && T1.bossPool.entries.All(e => e.boss != finalBoss) && activeRounds.SequenceEqual(new[] { 3, 6 }),
            "Final entry with its own boss: R3 came from the pool, R6 ran the boss named by the calendar; one boss per date (2 activations), then reward → victory at the 6-round run length");
        invalidProfile = Object.Instantiate(T1); invalidProfile.name = "Takvim (test: geçersiz)"; invalidProfile.displayName = "Geçersiz takvim testi";
        Set(invalidProfile, 0, 6); Set(invalidProfile, 1, 3);
        return Load(invalidProfile);
    }

    static double InvalidBlocked()
    {
        if (RM == null || RM.Profile != invalidProfile || RM.CalendarError == null) return Again;
        GameStates before = State; int logs = expectedLogs.Count(m => m.Contains("Run başlatılmadı"));
        RM.StartNextRound();
        Require(logs == 1 && RM.CalendarError.Contains("sıralı değil") && State != GameStates.RunSetup && State != GameStates.Round && State == before && !RM.IsRoundActive &&
                !Events.BossMode && Events.Events.Count == 0,
            $"Invalid calendar at run time: the run is not started (state stays {State}), one clear error is logged (\"{expectedLogs.Last(m => m.Contains("Run başlatılmadı"))}\"); the calendar is not corrected");
        Require(invalidProfile.bossCalendar[0].round == 6 && invalidProfile.bossCalendar[1].round == 3, "The invalid data is left exactly as it was");
        return .05;
    }

    static double LoadLegacy() => Load(K1);

    static double LegacyRuntime()
    {
        if (RM == null || RM.Profile != K1 || State != GameStates.RunSetup) return Again;
        Player.enabled = false;
        Events.SendMessage("LateUpdate");
        var up = Events.Upcoming;
        Require(!RM.Calendar.IsExplicit && RM.QuotaSegmentRounds == 5 && RM.QuotaSegmentEnd == 5 && RM.QuotaTarget == 45 && RM.BossTarget == 8 && RM.IsBossRound(5) && !RM.IsBossRound(3) &&
                RM.IsBossRound(45) && !RM.IsBossRound(50) && up != null && up.StartRound == 5 && up.AnnounceRound == 1 && Events.Events.Count == 1,
            "Right after the calendar runs, Kırılma V1 still runs its old rhythm in the scene: 5-round segments, first boss at R5, quota 45, boss target 8, no boss at R50");
        RM.StartNextRound();
        var hud = HudTexts();
        Require(State == GameStates.Round && hud["Label"] == "SEGMENT KOTASI · 5 ROUND" && hud["Event"].StartsWith($"Yaklaşan boss: {up.Data.displayName} · Round 5") && hud["Boss"] == "Boss hasadı hedefi (Round 5): 8",
            $"Kırılma V1 HUD unchanged: \"{hud["Label"]}\" / \"{hud["Event"]}\" / \"{hud["Boss"]}\"");
        return .05;
    }

    static double FinalCheck()
    {
        Require(expectedLogs.Count(m => m.Contains("Run başlatılmadı")) == 1 && expectedLogs.Count(m => m.Contains("Run profili seçilmedi")) == 1,
            $"Expected error messages only from the two invalid-calendar probes ({expectedLogs.Count} lines in total)");
        foreach (string log in expectedLogs) Note("beklenen hata · " + log);
        Require(errors == 0, "No other error or exception logged during the test");
        return .05;
    }
}
