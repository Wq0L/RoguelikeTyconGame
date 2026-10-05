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

// Batch (izole kopya): Bölüm 3.7.3 İŞLEV testi — aşamalı boss ödül havuzları (Run50_OdulAsamalariV1).
// Denge ölçümü DEĞİLDİR: oyuncu vuruşu kapalıdır, skor ve level testin eliyle verilir; tarla düzeni testin kurduğu sabit düzendir.
// A) Veri: profil ayrımı, aşama round'ları, 16 ödülün dağılımı, hatalı havuz verisi, menü.
// B) Sabit durumlar: 15 boss tarihi × düzen × seed için teklif kuralı (sızıntı yok, mevcut aşamadan aday, tekrar yok, koşullar).
// C) Tam run (R1–50, round'lar sırayla): ekrandaki aşama başlığı ve kart etiketleri, kart sonrası uygunluk, geçişler, R50 sırası.
// D) İkinci run: 0 / 1 / 2 aday, mevcut aşama tükenince alt aşama, bütün ödül kartlarında yazı sığması, bekleyen teklifle menü.
// E) Uzmanlaşmalı kopya profil (sıra), geçersiz havuzla run'ın başlamaması, eski profil: kayıtlı teklif dizisi ve eski metinler.
[InitializeOnLoad]
public static class OdulAsamalariV1Verification
{
    const string Key = "OdulAsamalariV1Verification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string MenuPath = "Tools/Run Profili/Run50 Ödül Aşamaları V1 · 50 round (aşamalı boss ödülleri prototipi)";
    const string TempPool = "Assets/ScriptableObjects/BossRewards/_Tmp373_GecersizHavuz.asset";
    const string TempProfile = Profiles + "_Tmp373_GecersizHavuz.asset";
    const int Seed = 3733;
    const double Again = double.NaN;

    static readonly string[][] ExpectedStages =
    {
        new[] { "keskin_bicak", "hizli_bilek", "bereketli_toprak", "nadir_tohum", "bilgi_filizi" },
        new[] { "kivilcim", "agir_darbe", "kritik_goz", "altin_hedef", "yikim_gucu" },
        new[] { "genis_savurus", "canavar_kesimi", "firtina_bilegi", "artci_patlama", "cifte_akim", "hasat_ritmi" },
    };
    static readonly string[] StageNames = { "ERKEN", "ORTA", "GÜÇLÜ" };
    static readonly int[] StageFirst = { 1, 12, 22 }, StageFirstBoss = { 3, 13, 23 };

    static readonly List<string> notes = new();
    static readonly List<string> expectedLogs = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static OdulAsamalariV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: yeni profil seçilir ve boss seed'i sabitlenir (gerçek projedeki seçim ve asset'ler değişmez).
        foreach (string name in new[] { "Run50_OdulAsamalariV1", "Run50_KirilmaV1" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            profile.bossSeed = Seed; EditorUtility.SetDirty(profile);
        }
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_OdulAsamalariV1.asset");
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
                File.WriteAllLines("Logs/OdulAsamalariV1Verification.txt", new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 240) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now + .02; return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;   // editörün arama dizini (oyunla ilgisiz)
        if (type != LogType.Exception && type != LogType.Error) return;
        // Geçersiz havuz denemelerinin kendi hata mesajları beklenen çıktıdır; ayrı sayılır.
        if (type == LogType.Error && message.Contains("ödül aşamaları geçersiz")) { expectedLogs.Add(message); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        foreach (string path in new[] { TempProfile, TempPool })
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null) AssetDatabase.DeleteAsset(path);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/OdulAsamalariV1Verification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
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
    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");
    static bool MentionsRound(string s) => s != null && System.Text.RegularExpressions.Regex.IsMatch(s, @"Round \d+");
    static string N(long v) => HarvestQuota.Format(v);
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static SpecializationManager Spec => SpecializationManager.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO O1 => Profile("Run50_OdulAsamalariV1");
    static RunProfileSO T1 => Profile("Run50_TakvimV1");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static BossRewardPoolSO Pool => O1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static BossRewardPanelUI Panel => Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
    static int TotalStacks => Boss.Taken.Sum(r => Boss.Stacks(r));
    static BossRewardSO R(string id) => RewardOfferLab.All(Pool).First(r => r.id == id);
    static bool Prepared => F<bool>(Boss, "offerPrepared");
    static string Ids(IEnumerable<BossRewardSO> rewards) => string.Join(", ", rewards.Select(r => r.displayName));
    static bool SameRandom(UnityEngine.Random.State a) => a.Equals(UnityEngine.Random.state);

    static string EndText()
    {
        var panel = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        return Strip(panel.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
    }

    static int ResolveCards(Func<List<TileCardOffer>, TileCardOffer> choose = null)
    {
        int picks = 0;
        while (State == GameStates.CardSelection)
        {
            if (picks++ > 300) throw new Exception("card selection did not finish");
            Call(Cards, "OnCardSelected", choose != null && picks == 1 ? choose(Offers) : Offers[0]);
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

    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    // Şu an çalışan adımın hemen arkasına adım ekler (görüntü almak için bir kare beklemek gerektiğinde).
    static void InsertNext(params (string name, Func<double> run)[] items)
    {
        var rest = steps.ToList(); steps.Clear();
        steps.Enqueue(rest[0]);
        foreach (var item in items) steps.Enqueue(item);
        foreach (var step in rest.Skip(1)) steps.Enqueue(step);
    }
    static void Shot(string name) { Add("bekle " + name, () => .7); Add("görüntü " + name, () => { Capture(name); return .05; }); }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("veri", Data);
        Add("hatalı havuz verisi", Validation);
        Add("menü", Menu);
        Add("sabit durumlar", FixedStates);
        Add("koşullar", Conditions);
        // C) tam run
        Add("tam run başlangıç", () => Begin("Tam run", O1, RewardOfferLab.Layout.Direct));
        PlanRounds(1, 50, new[] { 13, 50 }, BossInFullRun);
        Add("zafer", Victory);
        Add("yeniden başlat", Restart);
        // D) ikinci run: aday sayıları, alt aşamaya dönüş, yazı sığması, bekleyen teklifle menü
        Add("ikinci run başlangıç", () => Begin("İkinci run", O1, RewardOfferLab.Layout.Empty));
        Add("ikinci run: aday bırakmayan durum", () => { Fixture(0); return .05; });
        PlanRounds(1, 16, new int[0], BossInSecondRun);
        Add("menü kontrolü", MenuCheck);
        // E) uzmanlaşmalı kopya, geçersiz havuz, eski profil
        Add("uzmanlaşmalı kopya profil", LoadSpecialization);
        Add("uzmanlaşma run başlangıç", () => Begin("Uzmanlaşma sırası (test verisi)", specProfile, RewardOfferLab.Layout.Direct));
        PlanRounds(1, 3, new[] { 3 }, BossWithSpecialization);
        Add("geçersiz havuzla sahne", LoadInvalid);
        Add("geçersiz havuz: run başlamaz", InvalidBlocked);
        Add("eski profil", () => Load(K1));
        Add("eski profil: kayıtlı teklif dizisi", LegacyGolden);
        Add("eski profil run başlangıç", () => Begin("Eski profil (Kırılma V1)", K1, RewardOfferLab.Layout.Empty));
        PlanRounds(1, 5, new int[0], BossInLegacy);
        Add("kapanış", FinalCheck);
    }

    // Round'ları sırayla oynatır; boss round'unun sonunda (kartlar çözüldükten sonra ya da önce) atBoss çağrılır.
    static void PlanRounds(int from, int to, int[] levelRounds, Func<int, double> atBoss)
    {
        for (int r = from; r <= to; r++)
        {
            int round = r;
            Add($"R{r} başla", () => StartRound(round, levelRounds.Contains(round)));
            Add($"R{r} süre biter", () => { SetP(RM, "RemainingTime", 0f); return .03; });
            Add($"R{r} bitiş", () => AfterRound(round, atBoss));
        }
    }

    // ---------------------------------------------------------------- A) veri
    static double Data()
    {
        var o = O1; var t = T1; var k = K1; var pool = o.bossRewards;
        Require(o != null && o != t && pool != null && pool != t.bossRewards && BossRewardPoolSO.Validate(pool, o.runLength) == null && RunCalendar.Validate(o) == null,
            "Run50_OdulAsamalariV1 is a separate profile with its own (valid) reward pool asset");
        Require(o.bossCalendar.Select(d => d.round).SequenceEqual(t.bossCalendar.Select(d => d.round)) && o.bossCalendar.Select(d => d.final).SequenceEqual(t.bossCalendar.Select(d => d.final)) &&
                o.segmentTargets.SequenceEqual(t.segmentTargets) && o.bossTargets.SequenceEqual(t.bossTargets) && o.runLength == t.runLength && o.balance == t.balance && o.bossPool == t.bossPool &&
                o.choicesPerLevel == t.choicesPerLevel && o.choicesPerLevel == 3 && o.startingGold == t.startingGold && o.startingIron == t.startingIron && o.startingStone == t.startingStone &&
                o.fixedRoundDuration == t.fixedRoundDuration && o.fixedRoundDuration == 45f && o.debugBudget == t.debugBudget && o.events.Count == 0 && o.specializationAfterSegment == 0,
            "Everything except the reward pool is Takvim V1's: 15 boss dates, quota and boss targets, balance set (same asset), boss pool, 3 choices per level, start budget, 45 s rounds");
        Require(pool.IsStaged && pool.rewards.Count == 0 && pool.breakthroughs.Count == 0 && pool.choices == 3 && pool.reserveCurrentStageSlot &&
                pool.stageDistanceWeights.SequenceEqual(new[] { 1f, .6f, .35f }) && pool.stages.Count == 3 &&
                pool.stages.Select(s => s.displayName).SequenceEqual(StageNames) && pool.stages.Select(s => s.firstRound).SequenceEqual(StageFirst),
            "Staged pool data: stages ERKEN (from R1), ORTA (from R12), GÜÇLÜ (from R22); 3 cards; distance weights 1 / 0,60 / 0,35; one slot reserved for the current stage");
        for (int s = 0; s < 3; s++)
            Require(pool.stages[s].rewards.Select(e => e.reward.id).SequenceEqual(ExpectedStages[s]),
                $"{StageNames[s]} stage rewards: {string.Join(", ", pool.stages[s].rewards.Select(e => e.reward.displayName))}");
        // Aynı ödül asset'leri: etki, katsayı, stack sınırı ve ağırlık Kırılma V1 havuzundakiyle aynı nesnelerden okunur.
        var staged = RewardOfferLab.All(pool); var old = RewardOfferLab.All(k.bossRewards);
        Require(staged.Count == 16 && staged.Distinct().Count() == 16 && new HashSet<BossRewardSO>(staged).SetEquals(old) && staged.All(r => r.weight == 1f),
            "The 16 rewards are the very same assets as in Kırılma V1's pool (referenced, not copied): effects, coefficients, stack limits and base weights are unchanged");
        Require(pool.endlessStage != null && pool.endlessStage.rewards.Count == 0 && pool.endlessStage.displayName == "SINIRSIZ" && pool.StageIndexAt(51) == 2 && pool.StageIndexAt(500) == 2,
            "Endless: a separate stage field exists as data only (name SINIRSIZ, no rewards); no round opens it — rounds past 50 still resolve to the last main-run stage");
        // Aşama çözümü: her round ve 15 boss tarihi.
        var dates = o.bossCalendar.Select(d => d.round).ToList();
        for (int round = 1; round <= 50; round++)
        {
            int expected = round <= 11 ? 0 : round <= 21 ? 1 : 2;
            if (pool.StageIndexAt(round) != expected || pool.StageAt(round) != pool.stages[expected]) throw new Exception($"round {round} resolves to stage {pool.StageIndexAt(round)}");
        }
        Require(pool.StageIndexAt(11) == 0 && pool.StageIndexAt(12) == 1 && pool.StageIndexAt(21) == 1 && pool.StageIndexAt(22) == 2 && pool.StageIndexAt(0) == -1 &&
                pool.StageLastRound(0, 50) == 11 && pool.StageLastRound(1, 50) == 21 && pool.StageLastRound(2, 50) == 50,
            "Stage boundaries in data: R11 erken · R12 orta · R21 orta · R22 güçlü (ranges 1–11, 12–21, 22–50)");
        Require(dates.Select(d => pool.StageIndexAt(d)).SequenceEqual(new[] { 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2 }),
            "All 15 boss dates resolve to the right stage: " + string.Join(" ", dates.Select(d => $"R{d}:{pool.StageAt(d).displayName}")));
        var calendar = o.Calendar;
        Require(Enumerable.Range(0, 3).All(s => calendar.NextBossRound(pool.stages[s].firstRound) == StageFirstBoss[s]) && calendar.NextBossRound(51) == 0 && calendar.NextBossRound(47) == 50,
            "First boss of each stage comes from the run calendar: erken R3, orta R13, güçlü R23");
        // Kart açıklamaları: yeni havuzda eski profilin sunulma zamanı yazmıyor; ödül asset'lerinin kendi metni değişmedi.
        Require(staged.All(r => !MentionsRound(pool.NoteFor(r))) && MentionsRound(R("canavar_kesimi").note) && MentionsRound(R("firtina_bilegi").note) && MentionsRound(R("bilgi_filizi").note) &&
                R("canavar_kesimi").minRound == 25 && R("firtina_bilegi").minRound == 25 && R("bilgi_filizi").maxRound == 30 && R("artci_patlama").minRound == 10,
            "Card notes in the staged pool never name a round; the reward assets keep their old notes and old minRound / maxRound (used only by the old pools)");
        foreach (var r in staged.Where(r => pool.NoteFor(r) != r.note)) Note($"açıklama · {r.displayName}: eski \"{r.note}\" → yeni havuz \"{pool.NoteFor(r)}\"");
        foreach (string name in new[] { "Run50_KirilmaV1", "Run50_DengeV1", "Run50_TakvimV1", "Run50_BossPrototip" })
        {
            var p = Profile(name);
            if (p.bossRewards.IsStaged || BossRewardPoolSO.Validate(p.bossRewards, p.runLength) != null || p.bossRewards.StageIndexAt(10) != -1 || p.bossRewards.NoteFor(p.bossRewards.rewards[0]) != p.bossRewards.rewards[0].note)
                throw new Exception(name + " pool should stay a flat pool");
        }
        Require(t.bossRewards == k.bossRewards && k.bossRewards.rewards.Count == 13 && k.bossRewards.breakthroughs.Count == 3,
            "Old pools are untouched flat pools (Kırılma V1 / Takvim V1: 13 rewards + 3 breakthrough rewards; Denge V1; Boss Prototip)");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(x => x.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);
        return .05;
    }

    static string Bad(Action<BossRewardPoolSO> change, int runLength = 50)
    {
        var copy = Object.Instantiate(Pool);
        change(copy);
        string error = BossRewardPoolSO.Validate(copy, runLength);
        Object.DestroyImmediate(copy);
        return error;
    }

    static BossRewardStageEntry Entry(BossRewardSO reward) => new BossRewardStageEntry { reward = reward };

    static double Validation()
    {
        var clone = Object.Instantiate(R("keskin_bicak"));   // aynı kimlik, ayrı asset: "aynı etkiyi ayrı stack olarak çoğaltma"
        var other = Object.Instantiate(R("keskin_bicak")); other.id = "test_sinirsiz"; other.displayName = "Test ödülü";
        var cases = new (string name, Action<BossRewardPoolSO> change, string expect)[]
        {
            ("aynı ödül iki aşamada (Keskin Bıçak erken + güçlü)", p => p.stages[2].rewards.Add(Entry(R("keskin_bicak"))), "iki kez yazılmış"),
            ("aynı ödül aynı aşamada iki kez", p => p.stages[1].rewards.Add(Entry(R("kivilcim"))), "iki kez yazılmış"),
            ("aynı kimlik, kopya asset (ayrı stack)", p => p.stages[2].rewards.Add(Entry(clone)), "iki ayrı asset"),
            ("ana aşamadaki ödül sınırsız aşamada da", p => p.endlessStage.rewards.Add(Entry(R("hasat_ritmi"))), "iki kez yazılmış"),
            ("aşamalar sırasız (orta R12, güçlü R8)", p => p.stages[2].firstRound = 8, "sıralı değil"),
            ("iki aşama aynı round'da başlıyor", p => p.stages[2].firstRound = 12, "sıralı değil"),
            ("ilk aşama Round 4'te başlıyor", p => p.stages[0].firstRound = 4, "ilk aşama Round 1"),
            ("aşama run'dan sonra başlıyor (R60)", p => p.stages[2].firstRound = 60, "hiç açılmaz"),
            ("boş ödül satırı", p => p.stages[0].rewards.Add(Entry(null)), "boş ödül satırı"),
            ("eski düz liste de dolu", p => p.rewards.Add(R("keskin_bicak")), "boş olmalı"),
            ("ağırlık çarpanı eksik (2 değer, 3 aşama)", p => p.stageDistanceWeights.RemoveAt(2), "ağırlıklarında"),
            ("negatif ağırlık çarpanı", p => p.stageDistanceWeights[1] = -.5f, "negatif"),
            ("aşama adı boş", p => p.stages[1].displayName = "", "adı boş"),
        };
        foreach (var (name, change, expect) in cases)
        {
            string error = Bad(change);
            if (error == null || !error.Contains(expect)) throw new Exception($"validation case '{name}' gave: {error ?? "no error"}");
            Note($"hatalı veri · {name} → \"{error}\"");
        }
        Require(true, $"{cases.Length} invalid staged pools are each rejected with a message naming the problem (nothing is corrected silently)");
        Require(Bad(p => { }) == null && Bad(p => p.endlessStage.rewards.Add(Entry(other))) == null && Bad(p => p.reserveCurrentStageSlot = false) == null &&
                Bad(p => p.stageDistanceWeights[2] = 0f) == null,
            "Valid variants pass: the pool as is; an endless stage with its own reward; no reserved slot; a distance weight of 0");
        Object.DestroyImmediate(clone); Object.DestroyImmediate(other);
        return .05;
    }

    // Menü: geçersiz aşamalı havuzu olan profil seçilmez (yalnız izole kopyada; geçici asset'ler silinir).
    static double Menu()
    {
        var select = typeof(RunProfileMenu).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Static);
        var before = Selection.active;
        var badPool = Object.Instantiate(Pool); badPool.name = "_Tmp373_GecersizHavuz"; badPool.stages[2].rewards.Add(Entry(R("keskin_bicak")));
        AssetDatabase.CreateAsset(badPool, TempPool);
        var badProfile = Object.Instantiate(O1); badProfile.name = "_Tmp373_GecersizHavuz"; badProfile.bossRewards = badPool;
        AssetDatabase.CreateAsset(badProfile, TempProfile);
        int logs = expectedLogs.Count;
        select.Invoke(null, new object[] { TempProfile });
        bool refused = Selection.active == before && expectedLogs.Skip(logs).Any(m => m.Contains("Run profili seçilmedi") && m.Contains("iki kez yazılmış"));
        AssetDatabase.DeleteAsset(TempProfile); AssetDatabase.DeleteAsset(TempPool);
        Require(before == O1 && refused, "Tools > Run Profili refuses a profile whose staged pool is invalid: error logged, selection unchanged (temporary assets removed)");
        select.Invoke(null, new object[] { Profiles + "Run50_OdulAsamalariV1.asset" });
        Require(Selection.active == O1, "The valid Ödül Aşamaları V1 profile is selected through the same menu path");
        return .05;
    }

    // ---------------------------------------------------------------- B) sabit durumlarda teklif kuralı
    static List<BossRewardSO> Eligible(int stage) => Pool.stages[stage].rewards.Select(e => e.reward).Where(Boss.IsEligible).ToList();

    // Bir teklifin kurala uyup uymadığı: tekrarsız, açılmamış aşamadan ödül yok, hepsi uygun, mevcut aşamadan en az bir aday
    // (varsa), kart sayısı = min(3, açılmış aşamalardaki uygun aday sayısı).
    static string RuleProblem(List<BossRewardSO> offer, int round)
    {
        var pool = Pool; int stage = pool.StageIndexAt(round);
        if (offer.Count > pool.choices || offer.Distinct().Count() != offer.Count) return "duplicate or too many cards";
        foreach (var r in offer)
        {
            if (pool.StageIndexOf(r) > stage) return r.displayName + " is from a stage that is not open yet";
            if (pool.StageIndexOf(r) < 0) return r.displayName + " is not in the pool";
            if (!Boss.IsEligible(r)) return r.displayName + " is not eligible";
        }
        int candidates = Enumerable.Range(0, stage + 1).Sum(s => Eligible(s).Count);
        if (offer.Count != Math.Min(pool.choices, candidates)) return $"{offer.Count} cards for {candidates} candidates";
        if (Eligible(stage).Count > 0 && offer.All(r => pool.StageIndexOf(r) != stage)) return "no card from the current stage although it has an eligible reward";
        return null;
    }

    static double FixedStates()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != O1) return Again;
        Player.enabled = false;
        var pool = Pool; var dates = O1.bossCalendar.Select(d => d.round).ToList();
        Require(Boss.Pool == pool && Boss.OfferStage == null && !Boss.IsPending && Boss.Taken.Count == 0 && Events.RunSeed == Seed,
            "GameScene runs Run50_OdulAsamalariV1 with its staged pool; no offer and no stage is pending at run start");
        const int seeds = 60;
        int offers = 0, cards = 0; var seen = new HashSet<BossRewardSO>();
        var layouts = new (RewardOfferLab.Layout layout, float occupancy)[]
        {
            (RewardOfferLab.Layout.Empty, 1f), (RewardOfferLab.Layout.Direct, .75f), (RewardOfferLab.Layout.Direct, .5f), (RewardOfferLab.Layout.Explosive, .75f),
            (RewardOfferLab.Layout.Electric, .75f), (RewardOfferLab.Layout.Multi, .75f), (RewardOfferLab.Layout.Multi, .5f),
        };
        int checksBefore = RewardOfferLab.RandomChecks;
        foreach (var (layout, occupancy) in layouts)
        {
            Boss.ClearAll();
            RewardOfferLab.Build(layout);
            RewardOfferLab.SetOccupancy(occupancy);
            foreach (int round in dates)
                for (int seed = 1; seed <= seeds; seed++)
                {
                    var offer = RewardOfferLab.Offer(pool, round, seed);
                    string problem = RuleProblem(offer, round);
                    if (problem != null) throw new Exception($"{RewardOfferLab.Name(layout)}, R{round}, seed {seed}: {problem} ({Ids(offer)})");
                    if (seed <= 5 && !RewardOfferLab.Offer(pool, round, seed).SequenceEqual(offer)) throw new Exception($"R{round} seed {seed}: the same seed and state gave a different offer");
                    offers++; cards += offer.Count; foreach (var r in offer) seen.Add(r);
                }
        }
        Require(true, $"{offers} offers over {layouts.Length} fixed field states × 15 boss dates × {seeds} seeds all follow the rule: no duplicate card, nothing from a stage that is not open, " +
                      "every card eligible, at least one card from the current stage whenever it has an eligible reward, card count = min(3, candidates)");
        Require(RewardOfferLab.RandomChecks - checksBefore >= offers && seen.Count == 16,
            $"Building these offers never touched the gameplay random stream (checked before / after each of {RewardOfferLab.RandomChecks - checksBefore} builds); all 16 rewards appeared somewhere");

        // Aşama sınırları ve geçişler (çok davranışlı düzen: bütün ödüllerin koşulu sağlanıyor).
        Boss.ClearAll(); RewardOfferLab.Build(RewardOfferLab.Layout.Multi); RewardOfferLab.SetOccupancy(.5f);
        List<BossRewardSO> At(int round) => Enumerable.Range(1, 300).SelectMany(s => RewardOfferLab.Offer(pool, round, s)).Distinct().ToList();
        var r3 = At(3); var r10 = At(10); var r13 = At(13); var r20 = At(20); var r23 = At(23); var r50 = At(50);
        Require(new[] { r3, At(6), r10 }.All(set => set.Count == 5 && set.All(r => pool.StageIndexOf(r) == 0)),
            "Erken boss'ları (R3, R6, R10), 300 seed: only the five erken rewards ever appear — no orta or güçlü reward leaks in");
        Require(r13.Count == 10 && r20.Count == 10 && r13.Concat(r20).All(r => pool.StageIndexOf(r) <= 1) && r13.Any(r => pool.StageIndexOf(r) == 1),
            "R10 → R13: the orta stage opens at R13 (orta and erken rewards appear); no güçlü reward appears at R13, R16 or R20");
        Require(r23.Count == 16 && r50.Count == 16 && new[] { "canavar_kesimi", "firtina_bilegi", "artci_patlama", "cifte_akim", "hasat_ritmi", "genis_savurus" }.All(id => r23.Contains(R(id))),
            "R20 → R23: the güçlü stage opens at R23 — Canavar Kesimi, Fırtına Bileği, Artçı Patlama, Çifte Akım, Hasat Ritmi and Geniş Savuruş first appear there; all 16 rewards are candidates up to R50");
        Require(r23.Contains(R("bilgi_filizi")) && r50.Contains(R("bilgi_filizi")) && !RewardOfferLab.Offer(k1Pool, 35, 1).Contains(R("bilgi_filizi")),
            "Bilgi Filizi stays a candidate up to R50 in the staged pool (its old maxRound 30 is not read there)");

        // Ayrılan slotun etkisi: aynı durum, slot ayırma kapalı kopya.
        var noReserve = Object.Instantiate(pool); noReserve.reserveCurrentStageSlot = false;
        int with = 0, without = 0, positions0 = 0, positions2 = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var on = RewardOfferLab.Offer(pool, 23, seed); var off = RewardOfferLab.Offer(noReserve, 23, seed);
            if (on.Any(r => pool.StageIndexOf(r) == 2)) with++;
            if (off.Any(r => pool.StageIndexOf(r) == 2)) without++;
            if (pool.StageIndexOf(on[0]) == 2) positions0++;
            if (pool.StageIndexOf(on[2]) == 2) positions2++;
        }
        Object.DestroyImmediate(noReserve);
        Require(with == 400 && without < 400 && without > 300 && positions0 > 150 && positions0 < 390 && positions2 > 150 && positions2 < 390,
            $"Reserved slot at R23 (400 seeds): with it every offer holds a güçlü card (400 / 400), without it {without} / 400; the güçlü card is not pinned to one screen position " +
            $"(first card güçlü in {positions0}, third card güçlü in {positions2} offers)");
        Boss.ClearAll(); RewardOfferLab.ClearField();
        return .05;
    }

    static BossRewardPoolSO k1Pool => K1.bossRewards;
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }

    // Stack, stat tavanı, davranış ve tarla doluluğu koşulları; mevcut aşama tükenince alt aşama; 0 / 1 / 2 aday.
    static double Conditions()
    {
        var pool = Pool;
        bool Ever(string id, int round, int seeds = 200) => Enumerable.Range(1, seeds).Any(s => RewardOfferLab.Offer(pool, round, s).Contains(R(id)));
        // Rastgelelik: tarla kurmak ya da ödül almak oyunun akışını tüketiyor mu? (Bilgi notu; teklif üretimi ayrıca her seferinde denetlenir.)
        var state = UnityEngine.Random.state; RewardOfferLab.Build(RewardOfferLab.Layout.Direct); bool buildKeeps = SameRandom(state);
        state = UnityEngine.Random.state; RewardOfferLab.Grant(R("keskin_bicak")); bool grantKeeps = SameRandom(state);
        Note($"oyunun rastgelelik akışı · tarlaya saksı koymak: {(buildKeeps ? "değişmedi" : "değişti (saksı kurulumu kendi zarını atıyor)")} · ödül almak: {(grantKeeps ? "değişmedi" : "değişti")}");
        Boss.ClearAll();

        // Davranış koşulları.
        RewardOfferLab.Build(RewardOfferLab.Layout.Direct); RewardOfferLab.SetOccupancy(.75f);
        Require(new[] { "kivilcim", "yikim_gucu", "artci_patlama", "cifte_akim", "bereketli_toprak" }.All(id => !Boss.IsEligible(R(id)) && !Ever(id, 50)),
            "Field without any behavior and not running low: Kıvılcım, Yıkım Gücü, Artçı Patlama, Çifte Akım and Bereketli Toprak are never offered, even at R50 (200 seeds)");
        RewardOfferLab.Build(RewardOfferLab.Layout.Explosive); RewardOfferLab.SetOccupancy(.75f);
        Require(Ever("kivilcim", 13) && Ever("yikim_gucu", 13) && Ever("artci_patlama", 23) && !Ever("artci_patlama", 20) && !Ever("cifte_akim", 50),
            "Explosion layout: Kıvılcım and Yıkım Gücü from R13, Artçı Patlama from R23 (not at R20); Çifte Akım never (no electric planter)");
        RewardOfferLab.Build(RewardOfferLab.Layout.Electric);
        Require(Ever("cifte_akim", 23) && !Ever("cifte_akim", 20) && !Ever("artci_patlama", 50), "Electric layout: Çifte Akım from R23; Artçı Patlama never (no explosion planter)");
        // Tarla doluluğu.
        RewardOfferLab.SetOccupancy(.5f);
        bool low = Boss.IsEligible(R("bereketli_toprak")) && Ever("bereketli_toprak", 3);
        RewardOfferLab.SetOccupancy(.75f);
        Require(low && !Boss.IsEligible(R("bereketli_toprak")) && !Ever("bereketli_toprak", 3),
            "Bereketli Toprak is offered only while the field runs low (last round occupancy 0,50 → offered; 0,75 → never), also when it would be the current-stage pick");
        // Stat tavanı.
        TestMod(new StatModifier { statType = StatType.AttackSpeed, target = StatTarget.Player, operation = ModifierOperation.Set, value = .1f });
        Require(!Boss.IsEligible(R("hizli_bilek")) && !Boss.IsEligible(R("firtina_bilegi")) && !Ever("hizli_bilek", 3) && !Ever("firtina_bilegi", 23),
            "Stat ceiling: with the attack interval at its floor (0,10 s) Hızlı Bilek and Fırtına Bileği are not offered");
        ClearTestMods();
        RewardOfferLab.ClearField();
        var full = RewardOfferLab.Place(RewardOfferLab.OpenCells()[0], RewardOfferLab.Tile("Explosive"), 1f);
        Require(full.GetFinalStat(StatType.ExplosionChance) >= 1f - 1e-4f && !Boss.IsEligible(R("kivilcim")) && Boss.IsEligible(R("yikim_gucu")) && !Ever("kivilcim", 13),
            "Stat ceiling: the only behavior chance is already 100 % → Kıvılcım is not offered (Yıkım Gücü still is)");
        // Stack sınırı.
        RewardOfferLab.Build(RewardOfferLab.Layout.Direct); RewardOfferLab.SetOccupancy(.75f);
        var blade = R("keskin_bicak");
        Require(RewardOfferLab.Grant(blade) && RewardOfferLab.Grant(blade) && Ever("keskin_bicak", 3) && RewardOfferLab.Grant(blade) && !RewardOfferLab.Grant(blade) &&
                Boss.Stacks(blade) == blade.maxStacks && !Boss.IsEligible(blade) && !Ever("keskin_bicak", 3) && !Ever("keskin_bicak", 50),
            "Stack limit: Keskin Bıçak is still offered at 2 / 3, cannot be taken a fourth time, and is never offered again once it is at 3 / 3");
        Require(Boss.Taken.Count == 1 && Boss.Stacks(blade) == 3 && Pool.StageIndexOf(blade) == 0 && Mathf.Approximately(BossRewardManager.DirectDamageMultiplier, Mathf.Pow(blade.directDamageMultiplier, 3)),
            "The reward keeps one identity across stages: a single entry with 3 stacks and one combined effect (no separate copy per stage)");

        // Mevcut aşama tükenince alt aşama: orta aşamanın uygun adayı kalmıyor (davranış ödülleri koşulsuz, diğerleri sınırında).
        Boss.ClearAll(); RewardOfferLab.SetOccupancy(.75f);
        foreach (string id in new[] { "agir_darbe", "kritik_goz", "altin_hedef" }) while (RewardOfferLab.Grant(R(id))) { }
        var fallback = Enumerable.Range(1, 200).Select(s => RewardOfferLab.Offer(pool, 13, s)).ToList();
        Require(Eligible(1).Count == 0 && Eligible(0).Count == 4 && fallback.All(o => o.Count == 3 && o.Distinct().Count() == 3 && o.All(r => pool.StageIndexOf(r) == 0)),
            "Current stage exhausted at R13 (no eligible orta reward): the offer falls back to the lower stage — still three different cards, all erken");
        // 0 / 1 / 2 aday (erken aşama, R3).
        Boss.ClearAll(); RewardOfferLab.ClearField(); RewardOfferLab.SetOccupancy(1f);
        Fixture(0);
        Require(Enumerable.Range(1, 100).All(s => RewardOfferLab.Offer(pool, 3, s).Count == 0), "No candidate: the offer is empty (0 cards), never padded");
        Fixture(1);
        Require(Enumerable.Range(1, 100).All(s => RewardOfferLab.Offer(pool, 3, s).SequenceEqual(new[] { R("bilgi_filizi") })), "One candidate: one card");
        Fixture(2);
        Require(Enumerable.Range(1, 100).All(s => { var o = RewardOfferLab.Offer(pool, 3, s); return o.Count == 2 && o.Contains(R("bilgi_filizi")) && o.Contains(R("nadir_tohum")); }),
            "Two candidates: two different cards (the same reward is not shown twice to reach three)");
        Boss.ClearAll(); RewardOfferLab.ClearField();
        return .05;
    }

    // Erken aşamada tam n uygun aday bırakan durum (boş tarla: Bereketli Toprak koşulsuz). 0: hiçbiri · 1: Bilgi Filizi · 2: + Nadir Tohum.
    static void Fixture(int candidates)
    {
        Boss.ClearAll();
        foreach (string id in new[] { "keskin_bicak", "hizli_bilek" }) while (RewardOfferLab.Grant(R(id))) { }
        for (int i = 0; i < (candidates >= 2 ? 2 : 3); i++) RewardOfferLab.Grant(R("nadir_tohum"));
        for (int i = 0; i < (candidates >= 1 ? 1 : 2); i++) RewardOfferLab.Grant(R("bilgi_filizi"));
        if (Eligible(0).Count != candidates) throw new Exception($"fixture for {candidates} candidates gives {Eligible(0).Count}: {Ids(Eligible(0))}");
    }

    // ---------------------------------------------------------------- C–E) run sürücüsü
    static string runName; static int[] D; static long[] Q, BT;
    static long expectedTotal; static int rewardsTaken, baseModifiers = -1;
    static RunProfileSO runProfile, specProfile, invalidProfile;
    static readonly List<string> headers = new();

    static int Period(int round) { for (int i = 0; i < D.Length; i++) if (round <= D[i]) return i + 1; return D.Length; }
    static int First(int p) => p == 1 ? 1 : D[p - 2] + 1;

    static double Begin(string name, RunProfileSO profile, RewardOfferLab.Layout layout)
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != profile) return Again;
        runName = name; runProfile = profile;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        RunCalendar c = profile.Calendar;
        D = Enumerable.Range(1, profile.runLength).Where(c.IsPeriodEnd).ToArray();
        Q = D.Select((_, i) => c.QuotaTarget(i + 1)).ToArray(); BT = D.Select((_, i) => c.BossTarget(i + 1)).ToArray();
        // Temiz başlangıç: önceki run'dan teklif, aşama, ödül ya da ödül etkisi kalmamış.
        Must(RM.CurrentRound == 1 && RM.IsPreparingFirstRound && RM.Outcome == RunOutcome.None && RM.RewardPoolError == null && RM.CalendarError == null && Total == 0 && RM.PendingCardSelections == 0,
            name + ": run does not start clean");
        Must(Boss.Taken.Count == 0 && !Boss.IsPending && !Prepared && Boss.Offer.Count == 0 && Boss.OfferStage == null && Boss.OwnedModifiers.Count == 0 && F<int>(Boss, "offerRound") == 0 &&
             BossRewardManager.DirectDamageMultiplier == 1f && BossRewardManager.BehaviorDamageMultiplier == 1f && !RunPower.Rhythm.Enabled && !BossRewardManager.TryGetEcho(DamageType.Explosion, out _),
            name + ": an offer, a stage or a reward effect leaked into the new run");
        int modifiers = Stats.GlobalModifiers.Count;
        if (baseModifiers < 0) baseModifiers = modifiers;
        Must(modifiers == baseModifiers, $"{name}: {modifiers} global modifiers at run start, the first run had {baseModifiers}");
        RewardOfferLab.Build(layout);
        expectedTotal = 0; rewardsTaken = 0; headers.Clear();
        Require(true, $"{name}: starts clean in RunSetup ({profile.name}; no offer, no stage, no reward, no reward effect); field: {RewardOfferLab.Name(layout)}");
        return .05;
    }

    static double StartRound(int r, bool level)
    {
        int p = Period(r), first = First(p), B = D[p - 1];
        Must(State == (r == 1 ? GameStates.RunSetup : GameStates.RoundEnd), $"{runName} R{r}: unexpected state before the round ({State})");
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == r, $"{runName} R{r}: round did not start");
        SetP(RM, "RemainingTime", 1000f);
        long add = 0;
        bool hasBoss = RM.Calendar.HasBoss(p);
        long bossTarget = hasBoss ? BT[p - 1] : 0;
        if (r == first) add += Q[p - 1] - bossTarget;
        if (r == B) add += bossTarget;
        AddScore(add); expectedTotal += add;
        if (level) { Progress.AddXP(Progress.XPToNextLevel); Must(RM.PendingCardSelections == RM.ChoicesPerLevel, $"R{r}: a level should queue card choices"); }
        return .05;
    }

    static double AfterRound(int r, Func<int, double> atBoss)
    {
        if (RM.IsRoundActive) return Again;
        int p = Period(r), B = D[p - 1];
        Must(Total == expectedTotal, $"{runName} R{r}: total score {Total}, expected {expectedTotal}");
        if (r != B || !RM.IsBossRound(r))
        {
            if (State == GameStates.CardSelection) ResolveCards();
            Must(State == GameStates.RoundEnd && !Boss.IsPending && Boss.OfferStage == null, $"{runName} R{r}: an ordinary round should end in the round summary without an offer ({State})");
            return .03;
        }
        Must(!RM.RunFailed && Boss.IsPending && RM.LastBossRound == r, $"{runName} R{r}: the boss should be passed and a reward pending ({State}, failed {RM.RunFailed})");
        return atBoss(r);
    }

    // Ekrandaki panelin bekleyen teklifi doğru gösterdiği: başlık, alt başlık, kart sayısı, her kartın sınıf etiketi ve açıklaması.
    static List<BossRewardSO> CheckPanel(int r)
    {
        var pool = Boss.Pool; var panel = Panel; int stage = pool.StageIndexAt(r);
        var offer = Boss.Offer.ToList();
        Call(panel, "Fill");
        bool last = r >= RM.MaxRounds;
        Must(panel.gameObject.activeInHierarchy && Boss.OfferStage == pool.stages[stage] && panel.TitleText == StageNames[stage] + " AŞAMA · BOSS ÖDÜLÜ" && panel.ShownCards == offer.Count,
            $"R{r}: panel title \"{panel.TitleText}\" / {panel.ShownCards} cards, expected {StageNames[stage]} AŞAMA · BOSS ÖDÜLÜ / {offer.Count}");
        if (offer.Count > 0)
            Must(panel.SubtitleText == (last ? "Son boss geçildi · yalnız biri alınır · run burada biter · ödül run sonu listesine yazılır"
                                              : $"Boss geçildi · yalnız biri alınır · run sonuna kadar geçerli · etkisi Round {r + 1} ve sonrası"), $"R{r}: subtitle \"{panel.SubtitleText}\"");
        for (int i = 0; i < offer.Count; i++)
        {
            int s = pool.StageIndexOf(offer[i]);
            string tag = $"{StageNames[s]} AŞAMA · Round {StageFirstBoss[s]} boss'undan itibaren";
            Must(panel.StageTagText(i) == tag && panel.NoteText(i) == pool.NoteFor(offer[i]) && !MentionsRound(panel.NoteText(i)),
                $"R{r}: card {i} ({offer[i].displayName}) tag \"{panel.StageTagText(i)}\" / note \"{panel.NoteText(i)}\"");
            string fit = FitProblem(panel, i);
            Must(fit == null, $"R{r}: card {i} ({offer[i].displayName}): {fit}");
        }
        return offer;
    }

    // Kart yazıları kendi bölgesine sığıyor mu: kesilme / taşma yok, punto bölgenin en küçüğünün altına inmemiş.
    static string FitProblem(BossRewardPanelUI panel, int index)
    {
        foreach (var text in panel.CardTexts(index))
        {
            if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            Rect rect = text.rectTransform.rect; Vector3 size = text.textBounds.size;
            float min = text.name == "Effect" ? 20f : text.name == "Note" || text.name == "Stack" ? 15f : 14f;
            if (text.isTextTruncated) return $"{text.name} text is cut: \"{Strip(text.text)}\"";
            if (size.x > rect.width + 1.5f || size.y > rect.height + 1.5f) return $"{text.name} text {size.x:0}×{size.y:0} does not fit {rect.width:0}×{rect.height:0}: \"{Strip(text.text)}\"";
            if (text.fontSize < min - .05f) return $"{text.name} text shrank to {text.fontSize:0.#} pt (minimum {min})";
        }
        return null;
    }

    static string Take(int r, List<BossRewardSO> offer, BossRewardSO pick = null)
    {
        if (offer.Count == 0)
        {
            Must(Boss.ContinueWithoutReward() && !Boss.ContinueWithoutReward(), $"R{r}: an empty offer should be passed once");
            return null;
        }
        pick ??= offer[0];
        int before = TotalStacks;
        Must(!Boss.ContinueWithoutReward() && Boss.Choose(pick) && TotalStacks == before + 1 && !Boss.Choose(offer[offer.Count - 1]) && !Boss.Choose(pick) && TotalStacks == before + 1 &&
             !Boss.IsPending && Boss.OfferStage == null, $"R{r}: exactly one reward should be taken");
        rewardsTaken++;
        return pick.displayName;
    }

    // ---------------------------------------------------------------- C) tam run
    static readonly List<(int round, int taken, int owned, float direct)> effects = new();
    static string finalReward;

    static List<BossRewardSO> pendingOffer;
    static readonly Dictionary<int, string> shots = new()
    {
        { 3, "T373_01_R3_ErkenOduller" }, { 13, "T373_02_R13_OrtaOduller" }, { 23, "T373_03_R23_GucluOduller" }, { 50, "T373_04_R50_SonBoss_GucluOduller" },
    };

    static double BossInFullRun(int r)
    {
        var pool = Pool;
        var kivilcim = R("kivilcim"); var yikim = R("yikim_gucu");
        bool cards = State == GameStates.CardSelection;
        if (r == 13)
        {
            // Kart sonrası uygunluk: teklif kartlardan önce hazırlanmaz; son kart davranış kazandırınca ödül uygunluğu bunu görür.
            Must(cards && !Prepared && F<List<BossRewardSO>>(Boss, "offer").Count == 0 && Boss.Offer.Count == 0 && !Prepared, "R13: the offer must not be prepared before the level cards");
            var behavior = Cards.GuaranteedBehaviorOffer;
            Must(!Boss.IsEligible(kivilcim) && !Boss.IsEligible(yikim) && behavior != null, "R13: no behavior on the field before the card; a behavior card should be on offer");
            string cardName = behavior.Tile.modifierName;
            Call(Cards, "OnCardSelected", behavior);
            Must(State == GameStates.CardSelection && !Prepared && Boss.IsEligible(kivilcim) && Boss.IsEligible(yikim),
                $"R13: after the behavior card ({cardName}) the behavior rewards should be eligible while the offer is still not prepared");
            // Yalnız test düzeneği: bu boss'un seed'i, teklifte bir davranış ödülü çıkacak biçimde seçilir (teklif henüz hazırlanmadı).
            int seed = Enumerable.Range(1, 500).First(s => RewardOfferLab.Offer(pool, 13, s).Any(x => x == kivilcim || x == yikim));
            SetP(Events, "RunSeed", seed);
            Must(ResolveCards() == 2 && State == GameStates.RoundChoice, "R13: after the remaining card choices the reward selection should open");
            Note($"R13 kartı: {cardName} (saksının altına düştü)");
        }
        else if (r == 50)
        {
            Must(cards && RM.PendingCardSelections == 3 && !Prepared && RM.Outcome == RunOutcome.None, "R50: the level cards should come first, the reward waits, no victory yet");
            RM.StartNextRound();
            Must(RM.CurrentRound == 50 && State == GameStates.CardSelection, "R50: a round started while cards were pending");
            Must(ResolveCards() == 3 && State == GameStates.RoundChoice && RM.Outcome == RunOutcome.None, "R50: after the cards the reward selection should open");
        }
        else Must(!cards && State == GameStates.RoundChoice, $"R{r}: reward selection expected ({State})");

        var before = UnityEngine.Random.state;
        var offer = CheckPanel(r);
        Must(Prepared && SameRandom(before), $"R{r}: preparing / showing the offer consumed the gameplay random stream");
        string problem = RuleProblem(offer, r);
        Must(problem == null, $"R{r}: live offer breaks the rule: {problem} ({Ids(offer)})");
        Must(RewardOfferLab.Offer(pool, r, Events.RunSeed).SequenceEqual(offer), $"R{r}: the same seed and state should rebuild the same offer");
        // Panel kapat / aç ve yeniden hazırlama isteği teklifi değiştirmez.
        Panel.gameObject.SetActive(false); Panel.gameObject.SetActive(true);
        Boss.PrepareOffer(); Object.FindFirstObjectByType<UIManager>().SendMessage("Update");
        Must(Boss.Offer.SequenceEqual(offer) && CheckPanel(r).SequenceEqual(offer), $"R{r}: the offer changed after the panel was closed and reopened");
        RM.StartNextRound();
        Must(RM.CurrentRound == r && State == GameStates.RoundChoice, $"R{r}: a round started before the reward was chosen");
        headers.Add($"R{r} {Panel.TitleText}");
        if (r == 13)
            Require(offer.Any(x => x == kivilcim || x == yikim) && offer.Any(x => pool.StageIndexOf(x) == 1),
                $"R13 (first orta boss): the offer was prepared after the level cards and sees the behavior the first card brought — it offers {Ids(offer.Where(x => x == kivilcim || x == yikim))}; cards: {Ids(offer)}");
        if (r == 3 || r == 10 || r == 13 || r == 23 || r == 50)
            Require(true, $"{runName} · R{r}: \"{Panel.TitleText}\" / \"{Panel.SubtitleText}\" — kartlar: " +
                          string.Join(" | ", offer.Select((x, i) => $"{x.displayName} [{Panel.StageTagText(i)}]")));
        pendingOffer = offer;
        if (!shots.TryGetValue(r, out string shot)) return TakeInFullRun(r);
        InsertNext(("görüntü " + shot, () => { Capture(shot); return .05; }), ($"R{r} ödül alınır", () => TakeInFullRun(r)));
        return .7;
    }

    static double TakeInFullRun(int r)
    {
        var pool = Pool; var offer = pendingOffer;
        // Ödül etkileri aşama değişince silinmez.
        string taken = Take(r, offer, r == 23 ? offer.FirstOrDefault(x => x.id == "canavar_kesimi") : null);
        if (effects.Count > 0)
        {
            var prev = effects[effects.Count - 1];
            Must(Boss.Taken.Count >= prev.taken && Boss.OwnedModifiers.Count >= prev.owned && BossRewardManager.DirectDamageMultiplier >= prev.direct - 1e-4f,
                $"R{r}: reward effects shrank after the stage changed");
        }
        effects.Add((r, Boss.Taken.Count, Boss.OwnedModifiers.Count, BossRewardManager.DirectDamageMultiplier));
        Note($"R{r} {StageNames[pool.StageIndexAt(r)]}: {string.Join(" | ", offer.Select(x => x.displayName + " [" + StageNames[pool.StageIndexOf(x)] + "]"))} → alınan: {taken ?? "yok"}");
        if (r == 50) { finalReward = taken; Must(State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory, "R50: victory expected after the reward"); return .3; }
        Must(State == GameStates.RoundEnd, $"R{r}: round summary expected after the reward ({State})");
        // Yalnız test düzeneği: R23 teklifinde Canavar Kesimi ve Hasat Ritmi çıksın (yeni profildeki açıklama ve kart yazısı görüntüsü için).
        if (r == 20)
        {
            int seed = Enumerable.Range(1, 500).First(s => { var o = RewardOfferLab.Offer(pool, 23, s); return o.Any(x => x.id == "canavar_kesimi") && o.Any(x => x.id == "hasat_ritmi"); });
            SetP(Events, "RunSeed", seed);
        }
        return .05;
    }

    static double Victory()
    {
        string end = EndText();
        Require(headers.Count == 15 && headers.Take(3).All(h => h.EndsWith("ERKEN AŞAMA · BOSS ÖDÜLÜ")) && headers.Skip(3).Take(3).All(h => h.EndsWith("ORTA AŞAMA · BOSS ÖDÜLÜ")) && headers.Skip(6).All(h => h.EndsWith("GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ")),
            "Whole run R1–50 in order: the reward screen header followed the stage at all 15 bosses — " + string.Join(" · ", headers));
        Require(headers[2].StartsWith("R10") && headers[3].StartsWith("R13") && headers[5].StartsWith("R20") && headers[6].StartsWith("R23"),
            "Transitions on screen: R10 ERKEN → R13 ORTA, R20 ORTA → R23 GÜÇLÜ");
        Require(effects.Count == 15 && effects[14].taken >= effects[2].taken && effects.Zip(effects.Skip(1), (a, b) => b.owned >= a.owned && b.direct >= a.direct - 1e-4f).All(x => x),
            "Reward effects taken in an earlier stage stay in force after the stage changes (taken list, stat modifiers and damage multiplier never shrink)");
        Require(State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory && end.StartsWith("RUN TAMAMLANDI · ÖDÜL AŞAMALARI V1") && finalReward != null && end.Contains(finalReward) &&
                rewardsTaken == TotalStacks && Boss.OfferStage == null && !Boss.IsPending,
            $"R50: level cards → reward ({finalReward}) → victory; the run-end list holds the rewards including the R50 one: {end}");
        RM.StartNextRound();
        Require(RM.CurrentRound == 50 && State == GameStates.RunComplete, "R51 does not start");
        return .05;
    }

    static double Restart()
    {
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 2;
    }

    // ---------------------------------------------------------------- D) ikinci run
    static double BossInSecondRun(int r)
    {
        var pool = Pool; var panel = Panel;
        Must(State == GameStates.RoundChoice, $"R{r}: reward selection expected ({State})");
        if (r == 3 || r == 6 || r == 10)
        {
            // Durum, tur bitmeden önce kuruldu (aşağıda, bir önceki boss'tan sonra). 0 → 1 → 2 aday.
            int expected = r == 3 ? 0 : r == 6 ? 1 : 2;
            var offer = CheckPanel(r);
            var empty = F<TextMeshProUGUI>(panel, "empty"); var cont = F<UnityEngine.UI.Button>(panel, "continueButton");
            Must(offer.Count == expected && offer.Distinct().Count() == expected && empty.gameObject.activeSelf == (expected == 0) && cont.gameObject.activeSelf == (expected == 0),
                $"R{r}: {offer.Count} cards shown, expected {expected}");
            if (expected == 0)
            {
                Must(panel.SubtitleText == "Bu boss için sunulabilecek ödül yok" && Strip(empty.text).StartsWith("Uygun ödül kalmadı") && !Boss.Choose(R("keskin_bicak")), "R3: empty offer screen");
                Call(panel, "RequestContinue");
                Must(State == GameStates.RoundEnd && !Boss.IsPending, "R3: 'DEVAM' should close an empty offer");
                Require(true, "İkinci run · R3, no candidate: the screen says \"Uygun ödül kalmadı\" and DEVAM continues the run (no reward, no lock)");
                Fixture(1);
                return .05;
            }
            if (r == 10) return TextFit(offer);
            string taken = Take(r, offer);
            Must(State == GameStates.RoundEnd, $"R{r}: round summary expected");
            Require(true, $"İkinci run · R{r}, {expected} candidate: {expected} card ({Ids(offer)}), taken {taken}; the run continues");
            Fixture(2);
            return .05;
        }
        if (r == 13)
        {
            var offer = CheckPanel(r);
            Must(offer.Count == 3 && offer.All(x => pool.StageIndexOf(x) == 0) && panel.TitleText == "ORTA AŞAMA · BOSS ÖDÜLÜ" && Eligible(1).Count == 0,
                $"R13: with the orta stage exhausted the cards should all be erken ({Ids(offer)})");
            Require(Enumerable.Range(0, 3).All(i => panel.StageTagText(i) == "ERKEN AŞAMA · Round 3 boss'undan itibaren"),
                "İkinci run · R13, current stage exhausted: header \"ORTA AŞAMA · BOSS ÖDÜLÜ\", three erken cards, each tagged \"ERKEN AŞAMA · Round 3 boss'undan itibaren\"");
            Take(r, offer);
            return .05;
        }
        // R16: teklif bekliyorken ana menüye dönüş (oyunun menü yolu: durum MainMenu, sonra menü sahnesi).
        var pending = CheckPanel(r);
        Must(pending.Count > 0 && Boss.IsPending && Prepared && Boss.OfferStage != null && Boss.Taken.Count > 0, "R16: an offer should be pending before the menu return");
        GameManager.Instance.ReturnToMenu();
        Require(!Boss.IsPending && !Prepared && Boss.Offer.Count == 0 && Boss.OfferStage == null && Boss.Taken.Count == 0 && Boss.OwnedModifiers.Count == 0 && F<int>(Boss, "offerRound") == 0 &&
                BossRewardManager.DirectDamageMultiplier == 1f,
            "Return to the main menu while an offer is pending: the offer, its stage, the taken rewards and their effects are all cleared");
        HarvestScoreManager.Instance.ResetScore(); RM.ResetRounds(); Stats.ClearGlobalModifiers();
        SceneManager.LoadScene("MenuScene");
        return 2;
    }

    // R10'da bekleyen teklif açıkken: 16 ödülün her biri, her birikim adedinde karta yazılır ve yazıların sığdığı denetlenir.
    static double TextFit(List<BossRewardSO> realOffer)
    {
        var panel = Panel; var list = F<List<BossRewardSO>>(Boss, "offer"); var stacks = F<Dictionary<BossRewardSO, int>>(Boss, "stacks");
        var saved = new Dictionary<BossRewardSO, int>(stacks);
        int states = 0; float smallest = 99f; string smallestWhere = "";
        var all = RewardOfferLab.All(Pool);
        foreach (var reward in all)
            for (int have = 0; have < reward.maxStacks; have++)
            {
                list.Clear(); list.Add(reward);
                if (have > 0) stacks[reward] = have; else stacks.Remove(reward);
                Call(panel, "Fill");
                string problem = FitProblem(panel, 0);
                if (problem != null) throw new Exception($"{reward.displayName} at {have}/{reward.maxStacks}: {problem}");
                foreach (var text in panel.CardTexts(0).Where(t => t.gameObject.activeInHierarchy && (t.name == "Note" || t.name == "Stack" || t.name == "Effect")))
                    if (text.fontSize < smallest) { smallest = text.fontSize; smallestWhere = $"{reward.displayName} {text.name}"; }
                states++;
            }
        stacks.Clear(); foreach (var pair in saved) stacks[pair.Key] = pair.Value;
        // Görüntü: Hasat Ritmi (en uzun alt satır), Kıvılcım (en uzun etki), Bereketli Toprak (en uzun açıklama).
        list.Clear(); list.Add(R("hasat_ritmi")); list.Add(R("kivilcim")); list.Add(R("bereketli_toprak"));
        Call(panel, "Fill");
        var ritim = panel.CardTexts(0).First(t => t.name == "Stack"); ritim.ForceMeshUpdate();
        Require(FitProblem(panel, 0) == null && ritim.textInfo.lineCount >= 3 && ritim.fontSize >= 15f && ritim.textBounds.size.x <= ritim.rectTransform.rect.width + 1.5f,
            $"Hasat Ritmi card: the bottom text now wraps inside the card ({ritim.textInfo.lineCount} lines at {ritim.fontSize:0.#} pt, {ritim.textBounds.size.x:0} px wide in a {ritim.rectTransform.rect.width:0} px area)");
        Require(states == all.Sum(r => r.maxStacks), $"All 16 reward cards, at every stack count ({states} card states): no text is cut or leaves its area; smallest font {smallest:0.#} pt ({smallestWhere})");
        pendingRestore = realOffer;
        InsertNext(("görüntü kart yazıları", () => { Capture("T373_05_KartYazilari_HasatRitmi"); return .05; }), ("R10 teklif geri", RestoreAfterFit));
        return .7;
    }

    static List<BossRewardSO> pendingRestore;

    static double RestoreAfterFit()
    {
        var list = F<List<BossRewardSO>>(Boss, "offer");
        list.Clear(); list.AddRange(pendingRestore);
        Call(Panel, "Fill");
        string taken = Take(10, pendingRestore);
        Must(State == GameStates.RoundEnd, "R10: round summary expected");
        Require(true, $"İkinci run · R10, 2 candidates: 2 cards ({Ids(pendingRestore)}), taken {taken}; the run continues");
        // R13 için: orta aşamada uygun aday bırakma (boş tarla: davranış ödülleri koşulsuz; diğerleri sınırına getirilir).
        Boss.ClearAll();
        foreach (string id in new[] { "agir_darbe", "kritik_goz", "altin_hedef" }) while (RewardOfferLab.Grant(R(id))) { }
        return .05;
    }

    static double MenuCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && BossRewardManager.Instance == null && BossRewardManager.DirectDamageMultiplier == 1f &&
                !BossRewardManager.TryGetRhythm(out _), "Menu scene: no reward manager, offer or reward effect survives the scene change");
        return .05;
    }

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    // ---------------------------------------------------------------- E) uzmanlaşma sırası (test verisi)
    static double LoadSpecialization()
    {
        specProfile = Object.Instantiate(O1); specProfile.name = "Ödül aşamaları (test: uzmanlaşma)"; specProfile.displayName = "Uzmanlaşma sırası testi";
        specProfile.specializationAfterSegment = 1;
        specProfile.specializationOptions = new List<SpecializationSO>(Profile("Uzmanlasma20").specializationOptions);
        Require(specProfile.specializationOptions.Count >= 2 && RunCalendar.Validate(specProfile) == null, "Test data: the new profile with a specialization choice after the first boss (options from Uzmanlasma20)");
        return Load(specProfile);
    }

    static double BossWithSpecialization(int r)
    {
        Must(State == GameStates.CardSelection && Spec.IsPending && Boss.IsPending && !Prepared, "R3: cards first; specialization and reward both waiting; offer not prepared");
        ResolveCards();
        Object.FindFirstObjectByType<UIManager>().SendMessage("Update");
        var specPanel = Object.FindFirstObjectByType<SpecializationPanelUI>(FindObjectsInactive.Include);
        Require(State == GameStates.RoundChoice && Spec.IsPending && Boss.IsPending && !Prepared && Boss.Offer.Count == 0 && !Prepared && specPanel.gameObject.activeInHierarchy && !Panel.gameObject.activeInHierarchy,
            "After the level cards the specialization choice opens first; the reward offer is not prepared while it is pending (reading the offer does not prepare it)");
        var option = specProfile.specializationOptions[0];
        Must(Spec.Choose(option) && !Spec.IsPending && State == GameStates.RoundChoice, "R3: specialization chosen, reward still waiting");
        Object.FindFirstObjectByType<UIManager>().SendMessage("Update");
        var offer = CheckPanel(r);
        Require(Prepared && offer.Count == 3 && RuleProblem(offer, r) == null && !specPanel.gameObject.activeInHierarchy && Spec.Chosen == option,
            $"Once the specialization ({option.name}) is chosen the reward offer is prepared and shown: {Ids(offer)}");
        Take(r, offer);
        Must(State == GameStates.RoundEnd, "R3: round summary expected");
        return .05;
    }

    // ---------------------------------------------------------------- E) geçersiz havuz
    static double LoadInvalid()
    {
        invalidProfile = Object.Instantiate(O1); invalidProfile.name = "Ödül aşamaları (test: geçersiz havuz)"; invalidProfile.displayName = "Geçersiz havuz testi";
        invalidProfile.bossRewards = Object.Instantiate(Pool);
        invalidProfile.bossRewards.stages[2].rewards.Add(Entry(R("keskin_bicak")));
        return Load(invalidProfile);
    }

    static double InvalidBlocked()
    {
        if (RM == null || RM.Profile != invalidProfile || RM.RewardPoolError == null) return Again;
        GameStates before = State; int logs = expectedLogs.Count(m => m.Contains("Run başlatılmadı"));
        RM.StartNextRound();
        Require(logs == 1 && RM.RewardPoolError.Contains("iki kez yazılmış") && State == before && State != GameStates.RunSetup && State != GameStates.Round && !RM.IsRoundActive,
            $"Invalid staged pool at run time: the run is not started and one clear error is logged (\"{expectedLogs.Last(m => m.Contains("Run başlatılmadı"))}\")");
        Require(invalidProfile.bossRewards.stages[2].rewards.Count == 7 && invalidProfile.bossRewards.stages[0].rewards.Count == 5, "The invalid data is left exactly as it was (no entry removed)");
        return .05;
    }

    // ---------------------------------------------------------------- E) eski profil
    static double LegacyGolden()
    {
        if (RM == null || RM.Profile != K1 || State != GameStates.RunSetup) return Again;
        Player.enabled = false;
        // Kod değişmeden önce kaydedilen teklif dizisi (BossOfferGolden) ile şimdi üretilen: satır satır aynı olmalı.
        var now = RewardOfferMeasurement.GoldenLines();
        var golden = BossOfferGolden.Lines;
        if (now.Count != golden.Length) throw new Exception($"golden has {golden.Length} lines, now {now.Count}");
        for (int i = 0; i < golden.Length; i++)
            if (now[i] != golden[i]) throw new Exception($"old pool offer sequence changed at line {i}:\nrecorded {golden[i]}\nnow      {now[i]}");
        int offers = golden.Length * RewardOfferMeasurement.GoldenSeeds;
        Require(golden.Length == 162 && !RM.Calendar.IsExplicit && !Boss.Pool.IsStaged,
            $"Old pools (Kırılma V1, Denge V1, Boss Prototip): {offers} offers over 3 field states × 2 stack states × 9 boss rounds × {RewardOfferMeasurement.GoldenSeeds} seeds are identical, " +
            "card for card and in order, to the sequences recorded before the offer code was changed");
        Require(!Enumerable.Range(1, 100).Any(s => RewardOfferLab.Offer(k1Pool, 20, s).Any(r => r.id == "canavar_kesimi" || r.id == "firtina_bilegi")) &&
                Enumerable.Range(1, 100).Any(s => RewardOfferLab.Offer(k1Pool, 25, s).Any(r => r.id == "canavar_kesimi")) &&
                !Enumerable.Range(1, 100).Any(s => RewardOfferLab.Offer(k1Pool, 35, s).Any(r => r.id == "bilgi_filizi")) &&
                Enumerable.Range(1, 100).Any(s => RewardOfferLab.Offer(k1Pool, 10, s).Any(r => r.id == "hasat_ritmi")),
            "Old pools still use the rewards' own minRound / maxRound: Canavar Kesimi / Fırtına Bileği from the R25 boss, Bilgi Filizi not after R30, breakthrough rewards from R10");
        return .05;
    }

    static double BossInLegacy(int r)
    {
        Must(State == GameStates.RoundChoice && r == 5, $"legacy: reward selection expected at R5 ({State})");
        var panel = Panel; var offer = Boss.Offer.ToList();
        Call(panel, "Fill");
        Must(offer.Count == 3 && Boss.OfferStage == null && panel.TitleText == "BOSS GEÇİLDİ · ÖDÜL SEÇ" && panel.SubtitleText == "Yalnız biri alınır · run sonuna kadar geçerli · etkisi Round 6 ve sonrası" &&
             Enumerable.Range(0, 3).All(i => panel.StageTagText(i) == null && panel.NoteText(i) == offer[i].note && FitProblem(panel, i) == null), "legacy panel texts changed");
        // Eski profilde açıklama asset'teki metindir: Canavar Kesimi kartı eski kuralını (R25) söyler ve eski kural da öyle çalışır.
        var list = F<List<BossRewardSO>>(Boss, "offer"); var real = list.ToList();
        list.Clear(); list.Add(R("canavar_kesimi")); list.Add(R("bilgi_filizi")); list.Add(R("hasat_ritmi"));
        Call(panel, "Fill");
        Require(panel.NoteText(0).Contains("Round 25 boss'undan sonra çıkar") && panel.NoteText(1).Contains("Round 30 boss'undan sonra çıkmaz") && panel.StageTagText(0) == null &&
                Enumerable.Range(0, 3).All(i => FitProblem(panel, i) == null),
            "Old profile (Kırılma V1): header \"BOSS GEÇİLDİ · ÖDÜL SEÇ\", no stage tag; cards show the asset's own note, which matches the old rule (\"Round 25 boss'undan sonra çıkar\")");
        InsertNext(("görüntü eski profil", () => { Capture("T373_06_EskiProfil_KirilmaV1"); return .05; }), ("eski profil teklif geri", () =>
        {
            list.Clear(); list.AddRange(real);
            Must(Boss.Choose(real[0]) && State == GameStates.RoundEnd, "legacy: reward taken, round summary");
            return .05;
        }));
        return .7;
    }

    static double FinalCheck()
    {
        Require(expectedLogs.Count(m => m.Contains("Run başlatılmadı")) == 1 && expectedLogs.Count(m => m.Contains("Run profili seçilmedi")) == 1,
            $"Expected error messages only from the invalid-pool probes ({expectedLogs.Count} lines in total)");
        foreach (string log in expectedLogs) Note("beklenen hata · " + log);
        Require(errors == 0, "No other error or exception logged during the test");
        return .05;
    }
}
