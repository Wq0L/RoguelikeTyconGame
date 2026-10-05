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

// Batch (izole kopya): Bölüm 3.7.4 İŞLEV testi — bedelli boss ödülleri ve level başına seçim hakkı (Run50_BedelliOdullerV1).
// Denge ölçümü DEĞİLDİR. Oyuncu vuruşu kapalıdır; skor testin eliyle verilir; tarla, testin kurduğu sabit düzendir.
// Level'lar YAPAY XP ile verilir (seçim hakkı kuralını doğrulamak için); gerçek XP akışı ayrı ölçümdedir (BedelliOdulMeasurement).
// A) Veri: profil ayrımı, iki ödülün değerleri, eski ödüllerde yeni alanların etkisiz varsayılanı, menü.
// B) Teklif kuralı: R23 öncesi yok, davranışsız tarlada Adanış yok, ikisi aynı teklifte olabilir, biri alınınca diğeri çıkmaz.
// C) Seçim hakkı: 3 → 4 / 2, bekleyen haklar değişmez, aynı anda birden çok level, 1 ve 5 sınırı, kırpma yok.
// D) Atomik uygulama: teklifte olmayan, sınırdaki, çakışan, aralığı aşan, koşulu kalmamış ödül; uygulama sırasında hata.
// E) Hasar: formül (uzmanlaşma, tırpan, diğer boss çarpanlarıyla), dört davranışın gerçek vuruşu, artçı / ikinci dalga.
// F) Tam run (R1–50, Bereketli Öğrenim) ve ikinci run (Davranışa Adanış): ekran, kart yazıları, HUD, run sonu, restart, menü.
[InitializeOnLoad]
public static class BedelliOdullerV1Verification
{
    const string Key = "BedelliOdullerV1Verification";
    const string LogFile = "Logs/BedelliOdullerV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string MenuPath = "Tools/Run Profili/Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)";
    const string PendingNote = "Önceden kazanılmış seçimler değişmez.";
    const int Seed = 3744;
    const double Again = double.NaN;

    static readonly string[] StageNames = { "ERKEN", "ORTA", "GÜÇLÜ" };
    static readonly int[] StageFirstBoss = { 3, 13, 23 };

    static readonly List<string> notes = new();
    static readonly List<string> expectedLogs = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static BedelliOdullerV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: yeni profil seçilir ve boss seed'i sabitlenir (gerçek projedeki seçim ve asset'ler değişmez).
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_BedelliOdullerV1.asset");
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
        // Uygulama sırasında hata denemesinin kendi mesajı beklenen çıktıdır; ayrı sayılır.
        if (type == LogType.Error && message.Contains("Boss ödülü uygulanamadı")) { expectedLogs.Add(message); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        PlantHealth.AnyDamaged -= OnDamaged;
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
    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");
    static bool Near(float a, float b, float eps = 1e-4f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static SpecializationManager Spec => SpecializationManager.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO B1 => Profile("Run50_BedelliOdullerV1");
    static RunProfileSO O1 => Profile("Run50_OdulAsamalariV1");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static BossRewardPoolSO Pool => B1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static BossRewardPanelUI Panel => Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
    static QuotaHUD Hud => Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
    static int TotalStacks => Boss.Taken.Sum(r => Boss.Stacks(r));
    static BossRewardSO R(string id) => RewardOfferLab.All(Pool).First(r => r.id == id);
    static BossRewardSO Ogrenim => R("bereketli_ogrenim");
    static BossRewardSO Adanis => R("davranisa_adanis");
    static bool Prepared => F<bool>(Boss, "offerPrepared");
    static string Ids(IEnumerable<BossRewardSO> rewards) => string.Join(", ", rewards.Select(r => r.displayName));
    static bool Grant(BossRewardSO reward) => RewardOfferLab.Grant(reward);
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    // Sabit, ölçülebilir vuruş: hasar, kritik yok, tek hücrelik yarıçap.
    static void Strike(float damage, float radius)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
    }

    static BossRewardSO TestReward(string id, int delta, float direct = 1f, float behavior = 1f, int maxStacks = 1, string group = "")
    {
        var reward = ScriptableObject.CreateInstance<BossRewardSO>();
        reward.name = reward.id = id; reward.displayName = "Test " + id; reward.effectLabel = ""; reward.note = "";
        reward.levelChoiceDelta = delta; reward.directDamageMultiplier = direct; reward.behaviorDamageMultiplier = behavior;
        reward.maxStacks = maxStacks; reward.weight = 1f; reward.exclusiveGroup = group;
        return reward;
    }

    static string EndText()
    {
        var panel = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        return Strip(panel.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
    }

    // Bekleyen kart seçimlerini çözer: her ekranda aday sayısını denetler, ilk kartı alır. Dönen: yapılan seçim sayısı.
    static int ResolveCards()
    {
        int picks = 0;
        while (State == GameStates.CardSelection)
        {
            if (picks++ > 300) throw new Exception("card selection did not finish");
            Must(Offers.Count == 3, $"a card choice shows {Offers.Count} candidates, expected 3");
            Call(Cards, "OnCardSelected", Offers[0]);
        }
        return picks;
    }

    // n level'ı TEK XP kazancıyla verir (aynı karede birden çok level).
    static void Levels(int n)
    {
        var table = F<ProgressionSO>(Progress, "progressionData");
        float need = Progress.XPToNextLevel - Progress.CurrentXP;
        for (int i = 1; i < n; i++) need += table.GetXPForLevel(Progress.CurrentLevel + i);
        int before = Progress.CurrentLevel;
        Progress.AddXP(need);
        Must(Progress.CurrentLevel == before + n, $"{n} level expected from one XP gain, got {Progress.CurrentLevel - before}");
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

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("veri", Data);
        Add("menü", Menu);
        Add("teklif kuralı", OfferRules);
        Add("seçim hakkı (yapay XP)", ChoiceRights);
        Add("atomik uygulama", Atomic);
        Add("hasar formülü", Formula);
        // E) gerçek vuruşlar (Round 1 açık, oyuncu vuruşu test eliyle)
        Add("gerçek vuruş: round", LabRound);
        foreach (string arm in new[] { "yok", "ogrenim", "adanis" })
        {
            string a = arm;
            Add($"gerçek vuruş [{a}]: patlama, elektrik, doğrudan", () => HitsInstant(a));
            Add($"gerçek vuruş [{a}]: kasırga sonucu", () => HitsTornado(a));
            Add($"gerçek vuruş [{a}]: bumerang sonucu", () => HitsBoomerang(a));
        }
        Add("artçı: patlama", EchoExplosion);
        Add("artçı: patlama sonucu, elektrik", EchoExplosionResult);
        Add("artçı: elektrik sonucu", EchoElectricResult);
        // F) tam run: Bereketli Öğrenim
        Add("sahne yeniden", () => Load(B1));
        Add("tam run başlangıç", () => Begin("Tam run (Bereketli Öğrenim)"));
        PlanRounds(1, 50, new[] { 23, 24, 30, 50 }, BossInFullRun);
        Add("zafer", Victory);
        Add("yeniden başlat", Restart);
        // ikinci run: Davranışa Adanış, sonra ana menü
        Add("ikinci run başlangıç", () => Begin("İkinci run (Davranışa Adanış)"));
        PlanRounds(1, 26, new[] { 24 }, BossInSecondRun);
        Add("menü kontrolü", MenuCheck);
        Add("eski profil sahnesi", () => Load(K1));
        Add("eski profil", Legacy);
        Add("kapanış", FinalCheck);
    }

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
        var b = B1; var o = O1; var pool = b.bossRewards; var old = o.bossRewards;
        Require(b != null && b != o && pool != null && pool != old && BossRewardPoolSO.Validate(pool, b.runLength) == null && RunCalendar.Validate(b) == null,
            "Run50_BedelliOdullerV1 is a separate profile with its own (valid) reward pool asset");
        Require(b.bossCalendar.Select(d => d.round).SequenceEqual(o.bossCalendar.Select(d => d.round)) && b.bossCalendar.Select(d => d.final).SequenceEqual(o.bossCalendar.Select(d => d.final)) &&
                b.segmentTargets.SequenceEqual(o.segmentTargets) && b.bossTargets.SequenceEqual(o.bossTargets) && b.runLength == o.runLength && b.balance == o.balance && b.bossPool == o.bossPool &&
                b.choicesPerLevel == o.choicesPerLevel && b.choicesPerLevel == 3 && b.startingGold == o.startingGold && b.startingIron == o.startingIron && b.startingStone == o.startingStone &&
                b.fixedRoundDuration == o.fixedRoundDuration && b.fixedRoundDuration == 45f && b.debugBudget == o.debugBudget && b.events.Count == 0 && b.specializationAfterSegment == 0,
            "Everything except the reward pool is Ödül Aşamaları V1's: 15 boss dates, quota and boss targets, balance set (same asset: XP, economy), boss pool, base 3 choices per level, start budget, 45 s rounds");
        Require(pool.IsStaged && pool.choices == 3 && pool.reserveCurrentStageSlot == old.reserveCurrentStageSlot && pool.stageDistanceWeights.SequenceEqual(old.stageDistanceWeights) &&
                pool.stages.Count == 3 && pool.stages.Select(s => s.displayName).SequenceEqual(StageNames) && pool.stages.Select(s => s.firstRound).SequenceEqual(old.stages.Select(s => s.firstRound)) &&
                pool.stages[0].rewards.Select(e => e.reward).SequenceEqual(old.stages[0].rewards.Select(e => e.reward)) &&
                pool.stages[1].rewards.Select(e => e.reward).SequenceEqual(old.stages[1].rewards.Select(e => e.reward)) &&
                pool.stages[2].rewards.Take(6).Select(e => e.reward).SequenceEqual(old.stages[2].rewards.Select(e => e.reward)) && pool.stages[2].rewards.Count == 8 &&
                pool.endlessStage.rewards.Count == 0,
            "Pool: the same three stages, offer policy and the same 16 reward assets as Ödül Aşamaları V1 (referenced, not copied); the güçlü stage holds two more rewards (8 in total)");
        var staged = RewardOfferLab.All(old);
        Require(staged.Count == 16 && staged.All(r => pool.NoteFor(r) == old.NoteFor(r) && pool.StageIndexOf(r) == old.StageIndexOf(r)),
            "The 16 existing rewards keep their stage and card note in the new pool");
        var og = Ogrenim; var ad = Adanis;
        Require(og.displayName == "Bereketli Öğrenim" && og.levelChoiceDelta == 1 && Near(og.directDamageMultiplier, .8f) && og.behaviorDamageMultiplier == 1f && og.rareDirectMultiplier == 1f &&
                og.modifiersPerStack.Count == 0 && og.maxStacks == 1 && og.weight == 1f && og.condition == BossRewardCondition.Always && og.HasCost && !og.IsBreakthrough && pool.StageIndexOf(og) == 2,
            "Bereketli Öğrenim data: level choice +1, direct hit damage ×0,80, behavior damage untouched, no stat modifier, at most once, weight 1, no condition, güçlü stage");
        Require(ad.displayName == "Davranışa Adanış" && ad.levelChoiceDelta == -1 && Near(ad.behaviorDamageMultiplier, 1.5f) && ad.directDamageMultiplier == 1f && ad.rareDirectMultiplier == 1f &&
                ad.modifiersPerStack.Count == 0 && ad.maxStacks == 1 && ad.weight == 1f && ad.condition == BossRewardCondition.AnyBehaviorPlanter && ad.HasCost && !ad.IsBreakthrough && pool.StageIndexOf(ad) == 2,
            "Davranışa Adanış data: behavior damage ×1,50, level choice −1, direct damage untouched, at most once, weight 1, needs a placed planter with a behavior chance, güçlü stage");
        var mates = new List<BossRewardSO>(); pool.CollectExclusive(og, mates);
        var mates2 = new List<BossRewardSO>(); pool.CollectExclusive(ad, mates2);
        Require(!string.IsNullOrEmpty(og.exclusiveGroup) && og.exclusiveGroup == ad.exclusiveGroup && mates.SequenceEqual(new[] { ad }) && mates2.SequenceEqual(new[] { og }),
            $"Both rewards are in the same mutual-exclusion group (\"{og.exclusiveGroup}\"); the pool resolves each one's counterpart from data");
        Require(b.Calendar.NextBossRound(pool.stages[2].firstRound) == 23 && pool.stages[2].firstRound == 22 && !MentionsRound(pool.NoteFor(og)) && !MentionsRound(pool.NoteFor(ad)) && og.minRound == 0 && ad.minRound == 0,
            "First offer round comes from the stage and the calendar (güçlü opens at R22 → first boss R23); the rewards carry no round of their own");
        // Eski ödüllerde yeni alanların varsayılanı etkisizdir.
        var all = AssetDatabase.FindAssets("t:BossRewardSO", new[] { "Assets/ScriptableObjects/BossRewards" })
            .Select(g => AssetDatabase.LoadAssetAtPath<BossRewardSO>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var others = all.Where(r => r != og && r != ad).ToList();
        Require(all.Count == others.Count + 2 && others.Count >= 22 && others.All(r => r.levelChoiceDelta == 0 && string.IsNullOrEmpty(r.exclusiveGroup) && !r.HasCost && !BossRewardText.IsTrade(r)),
            $"All {others.Count} older reward assets: level choice change 0, no exclusion group, no cost — the new fields default to no effect");
        foreach (string name in new[] { "Run50_KirilmaV1", "Run50_DengeV1", "Run50_TakvimV1", "Run50_BossPrototip", "Run50_OdulAsamalariV1" })
        {
            var p = Profile(name);
            if (RewardOfferLab.All(p.bossRewards).Any(r => r == og || r == ad || r.HasCost)) throw new Exception(name + " pool should not contain a cost reward");
        }
        Require(RewardOfferLab.All(old).Count == 16 && K1.bossRewards.rewards.Count == 13 && K1.bossRewards.breakthroughs.Count == 3 && !K1.bossRewards.IsStaged,
            "Older profiles' pools are unchanged and hold no cost reward (Ödül Aşamaları V1: 16 rewards; Kırılma V1 / Takvim V1: 13 + 3; Denge V1; Boss Prototip)");
        Require(RunPower.MinLevelChoices == 1 && RunPower.MaxLevelChoices == 5 && RunPower.ValidLevelChoices(1) && RunPower.ValidLevelChoices(5) && !RunPower.ValidLevelChoices(0) && !RunPower.ValidLevelChoices(6),
            "Valid range of choices per level in this package: 1–5");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(x => x.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);
        return .05;
    }

    static bool MentionsRound(string s) => s != null && System.Text.RegularExpressions.Regex.IsMatch(s, @"Round \d+");

    static double Menu()
    {
        var select = typeof(RunProfileMenu).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Static);
        var before = Selection.active;
        select.Invoke(null, new object[] { Profiles + "Run50_OdulAsamalariV1.asset" });
        bool other = Selection.active == O1;
        select.Invoke(null, new object[] { Profiles + "Run50_BedelliOdullerV1.asset" });
        Require(before == B1 && other && Selection.active == B1, "Tools > Run Profili selects the new profile through the same menu path (and back from another profile)");
        return .05;
    }

    // ---------------------------------------------------------------- B) teklif kuralı
    static List<BossRewardSO> Eligible(int stage) => Pool.stages[stage].rewards.Select(e => e.reward).Where(Boss.IsEligible).ToList();

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

    // Tek davranışlı tarla: ortadaki saksının altında o davranışın tile'ı (şans değeri verilir), diğer saksılar davranışsız.
    static readonly List<PlanterBrain> placed = new();
    static GroundCell center;

    static void BuildSingle(string tile, float chance)
    {
        RewardOfferLab.ClearField();
        placed.Clear();
        var cells = RewardOfferLab.OpenCells().OrderBy(c => c.GetGridPosition().x * 100 + c.GetGridPosition().z).ToList();
        int cx = (int)Math.Round(cells.Average(c => c.GetGridPosition().x)), cz = (int)Math.Round(cells.Average(c => c.GetGridPosition().z));
        center = cells.First(c => c.GetGridPosition().x == cx && c.GetGridPosition().z == cz);
        foreach (var cell in cells)
            placed.Add(cell == center && tile != null ? RewardOfferLab.Place(cell, RewardOfferLab.Tile(tile), chance) : RewardOfferLab.Place(cell));
    }

    static double OfferRules()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != B1) return Again;
        Player.enabled = false;
        var pool = Pool; var og = Ogrenim; var ad = Adanis;
        var dates = B1.bossCalendar.Select(d => d.round).ToList();
        Require(Boss.Pool == pool && !Boss.IsPending && Boss.Taken.Count == 0 && Events.RunSeed == Seed && RM.BaseChoicesPerLevel == 3 && RM.ChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 0,
            "GameScene runs Run50_BedelliOdullerV1 with its pool; at run start a level gives 3 choices and no reward effect is active");
        bool Ever(BossRewardSO r, int round, int seeds = 200) => Enumerable.Range(1, seeds).Any(s => RewardOfferLab.Offer(pool, round, s).Contains(r));
        const int seeds = 60;
        int offers = 0;
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
                    if (round < 23 && (offer.Contains(og) || offer.Contains(ad))) throw new Exception($"R{round} seed {seed}: a cost reward was offered before R23 ({Ids(offer)})");
                    offers++;
                }
        }
        Require(RewardOfferLab.RandomChecks - checksBefore >= offers,
            $"{offers} offers over {layouts.Length} fixed field states × 15 boss dates × {seeds} seeds follow the staged rule (no duplicate, nothing from an unopened stage, every card eligible, " +
            "one card from the current stage when possible); no cost reward before R23; building them never touched the gameplay random stream");

        // Davranışsız tarla: Adanış hiç sunulmaz; Öğrenim R23'ten itibaren sunulur.
        Boss.ClearAll(); RewardOfferLab.Build(RewardOfferLab.Layout.Direct); RewardOfferLab.SetOccupancy(.75f);
        Require(Boss.IsEligible(og) && !Boss.IsEligible(ad) && Boss.BlockOf(ad, true) == BossRewardBlock.Condition && Ever(og, 23) && !Ever(og, 20) && dates.All(d => !Ever(ad, d)),
            "Field without any behavior: Davranışa Adanış is never offered at any of the 15 bosses (200 seeds each); Bereketli Öğrenim is offered from R23, not at R20");
        // Dört davranışın her biri tek başına Adanış'ı açar.
        foreach (var (tile, stat) in new[] { ("Explosive", StatType.ExplosionChance), ("Electric", StatType.ElectricChance), ("Tornado", StatType.TornadoChance), ("Boomerang", StatType.BoomerangChance) })
        {
            BuildSingle(tile, .25f);
            var source = placed.First(p => p.GetFinalStat(stat) > 0f);
            if (placed.Count(p => p.GetFinalStat(stat) > 0f) != 1 || !Boss.IsEligible(ad) || !Ever(ad, 23) || Ever(ad, 20)) throw new Exception($"{tile}: Adanış should be offered from R23 with one {tile} planter ({source.name})");
        }
        Require(true, "One placed planter with an explosion, electric, tornado or boomerang chance is enough for Davranışa Adanış (each tried alone): offered from R23, never at R20");
        // İkisi aynı teklifte seçenek olabilir; ayrı garanti yok.
        Boss.ClearAll(); RewardOfferLab.Build(RewardOfferLab.Layout.Multi); RewardOfferLab.SetOccupancy(.75f);
        int both = 0, onlyOg = 0, onlyAd = 0, neither = 0; var counts = new Dictionary<BossRewardSO, int>();
        for (int seed = 1; seed <= 400; seed++)
        {
            var offer = RewardOfferLab.Offer(pool, 23, seed);
            bool o = offer.Contains(og), a = offer.Contains(ad);
            if (o && a) both++; else if (o) onlyOg++; else if (a) onlyAd++; else neither++;
            foreach (var r in offer) counts[r] = counts.TryGetValue(r, out int n) ? n + 1 : 1;
        }
        var strong = pool.stages[2].rewards.Select(e => e.reward).ToList();
        double mean = strong.Where(r => r != og && r != ad).Average(r => counts.TryGetValue(r, out int n) ? n : 0);
        Require(both > 0 && onlyOg > 0 && onlyAd > 0 && neither > 0 && counts[og] > mean * .6 && counts[og] < mean * 1.5 && counts[ad] > mean * .6 && counts[ad] < mean * 1.5,
            $"R23, all behaviors on the field, 400 seeds: both in the same offer {both} times, only Öğrenim {onlyOg}, only Adanış {onlyAd}, neither {neither} — no separate guarantee; " +
            $"seen in {counts[og]} / {counts[ad]} offers vs {mean:0} on average for the other six güçlü rewards (same base weight 1)");
        foreach (var r in strong) Note($"R23 görülme · {r.displayName}: {(counts.TryGetValue(r, out int n) ? n : 0)} / 400");
        // Karşılıklı dışlama: biri alınınca diğeri sonraki tekliflere giremez; alınan da (en çok 1) yeniden çıkmaz.
        Require(Grant(og) && Boss.BlockOf(ad, true) == BossRewardBlock.Exclusive && !Boss.IsEligible(ad) && Boss.BlockOf(og, true) == BossRewardBlock.StackLimit && !Boss.IsEligible(og) &&
                dates.Where(d => d >= 23).All(d => !Ever(ad, d) && !Ever(og, d)),
            "After Bereketli Öğrenim is taken: Davranışa Adanış is blocked by the exclusion group and never offered at R23 … R50 (200 seeds each); Öğrenim itself is at its limit (1 / 1)");
        Boss.ClearAll();
        Require(Grant(ad) && Boss.BlockOf(og, true) == BossRewardBlock.Exclusive && !Boss.IsEligible(og) && dates.Where(d => d >= 23).All(d => !Ever(og, d) && !Ever(ad, d)),
            "After Davranışa Adanış is taken: Bereketli Öğrenim is never offered again (same rule in the other direction)");
        Boss.ClearAll();
        Require(Boss.IsEligible(og) && Boss.IsEligible(ad) && BossRewardManager.LevelChoiceDelta == 0, "A new run state (ClearAll) lifts the exclusion: both are candidates again");
        RewardOfferLab.ClearField();
        return .05;
    }

    // ---------------------------------------------------------------- C) seçim hakkı (yapay XP)
    static double ChoiceRights()
    {
        var pool = Pool; var og = Ogrenim; var ad = Adanis;
        Boss.ClearAll(); RewardOfferLab.Build(RewardOfferLab.Layout.Multi); RewardOfferLab.SetOccupancy(.75f);
        Require(RM.BaseChoicesPerLevel == 3 && RM.ChoicesPerLevel == 3 && RM.LevelsGained == 0 && RM.CardChoicesGranted == 0 && RM.PendingCardSelections == 0 && RM.CardsTaken == 0 && Progress.CurrentLevel == 1,
            "Start: 3 choices per level; no level, no granted choice, no pending choice, no card taken (four separate counters)");
        Levels(2);
        Require(RM.LevelsGained == 2 && RM.PendingCardSelections == 6 && RM.CardChoicesGranted == 6, "Two levels from one XP gain (same frame): each level adds 3 → 6 pending choices");
        Require(Grant(og) && RM.ChoicesPerLevel == 4 && RM.BaseChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 1 && RM.PendingCardSelections == 6 && RM.CardChoicesGranted == 6 &&
                Near(BossRewardManager.DirectDamageMultiplier, .8f) && BossRewardManager.BehaviorDamageMultiplier == 1f,
            "Bereketli Öğrenim taken with 6 choices pending: a level now gives 4, the 6 pending choices stay 6 (not 8); direct ×0,80 and no behavior change — gain and cost together");
        Levels(1);
        Require(RM.PendingCardSelections == 10 && RM.CardChoicesGranted == 10 && RM.LevelsGained == 3, "The next level adds 4 choices (6 → 10)");
        Levels(2);
        Require(RM.PendingCardSelections == 18 && RM.CardChoicesGranted == 18 && RM.LevelsGained == 5, "Two levels at once after the reward: 4 each (10 → 18)");
        Require(!Grant(og) && Boss.Stacks(og) == 1 && RM.ChoicesPerLevel == 4 && Near(BossRewardManager.DirectDamageMultiplier, .8f) && Boss.BlockOf(og, true) == BossRewardBlock.StackLimit,
            "Stack limit: a second Bereketli Öğrenim is refused; nothing changes (still 4 choices, direct ×0,80, 1 / 1)");
        Require(!Grant(ad) && Boss.Stacks(ad) == 0 && Boss.Taken.Count == 1 && RM.ChoicesPerLevel == 4 && BossRewardManager.BehaviorDamageMultiplier == 1f,
            "Exclusion at take time: Davranışa Adanış is refused while Öğrenim is held, even when it is forced into the offer; no effect, no stack, no taken entry");
        Boss.ClearAll();
        Require(RM.ChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 0 && BossRewardManager.DirectDamageMultiplier == 1f && RM.PendingCardSelections == 18,
            "Clearing the reward state returns to 3 choices per level and direct ×1; choices already earned are still pending (18)");
        Require(Grant(ad) && RM.ChoicesPerLevel == 2 && BossRewardManager.LevelChoiceDelta == -1 && RM.PendingCardSelections == 18 && RM.CardChoicesGranted == 18 &&
                Near(BossRewardManager.BehaviorDamageMultiplier, 1.5f) && BossRewardManager.DirectDamageMultiplier == 1f,
            "Davranışa Adanış taken with 18 choices pending: a level now gives 2, none of the 18 pending choices is removed; behavior ×1,50 and no direct change");
        Levels(2);
        Require(RM.PendingCardSelections == 22 && RM.CardChoicesGranted == 22 && RM.LevelsGained == 7 && !Grant(og) && Boss.BlockOf(og, true) == BossRewardBlock.Exclusive && RM.ChoicesPerLevel == 2,
            "Two levels at once add 2 each (18 → 22); Bereketli Öğrenim is refused while Adanış is held");

        // 3 → 4 → 2 aynı run'da (test ödülleriyle; dışlama grubu yok) ve 1 / 5 sınırları.
        var plus1 = TestReward("t_plus1", +1, .9f, 1f, 3); var plus2 = TestReward("t_plus2", +2, .5f);
        var minus1 = TestReward("t_minus1", -1, 1f, 1.1f, 3); var minus2 = TestReward("t_minus2", -2, 1f, 2f);
        Boss.ClearAll();
        int pending = RM.PendingCardSelections;
        Require(Grant(plus1) && RM.ChoicesPerLevel == 4, "Test reward +1: 3 → 4");
        Levels(1); pending += 4;
        Require(RM.PendingCardSelections == pending && Grant(minus2) && RM.ChoicesPerLevel == 2 && RM.PendingCardSelections == pending, "Then a test reward −2 in the same run: 4 → 2; pending choices unchanged");
        Levels(2); pending += 4;
        Require(RM.PendingCardSelections == pending && BossRewardManager.LevelChoiceDelta == -1, "3 → 4 → 2 in one run: effects add up (+1 − 2 = −1); two more levels add 2 each");
        // Üst sınır 5.
        Boss.ClearAll();
        Require(Grant(plus1) && Grant(plus1) && RM.ChoicesPerLevel == 5 && Near(BossRewardManager.DirectDamageMultiplier, .81f), "Upper bound: two +1 rewards → 5 choices per level");
        Levels(1); pending += 5;
        Require(RM.PendingCardSelections == pending, "A level at the upper bound adds 5 choices");
        Require(Boss.BlockOf(plus1, true) == BossRewardBlock.LevelChoiceRange && !Boss.IsEligible(plus1) && !Grant(plus1) && RM.ChoicesPerLevel == 5 && Boss.Stacks(plus1) == 2 &&
                Near(BossRewardManager.DirectDamageMultiplier, .81f),
            "A third +1 would give 6: not eligible and refused at take time; its damage cost is not applied either (direct stays ×0,81, stacks 2 / 3)");
        var withTest = Object.Instantiate(pool); withTest.stages[0].rewards.Add(new BossRewardStageEntry { reward = plus1 });
        bool offeredAtFive = Enumerable.Range(1, 200).Any(s => RewardOfferLab.Offer(withTest, 3, s).Contains(plus1));
        Boss.ClearAll();
        bool offeredAtThree = Enumerable.Range(1, 200).Any(s => RewardOfferLab.Offer(withTest, 3, s).Contains(plus1));
        Require(!offeredAtFive && offeredAtThree, "The out-of-range reward is never put into an offer at 5 choices (200 seeds); the same reward is offered while it fits (test pool copy)");
        Require(Grant(plus1) && RM.ChoicesPerLevel == 4 && !Grant(plus2) && Boss.BlockOf(plus2, true) == BossRewardBlock.LevelChoiceRange && RM.ChoicesPerLevel == 4 && Boss.Stacks(plus2) == 0 &&
                Near(BossRewardManager.DirectDamageMultiplier, .9f) && Boss.Taken.Count == 1,
            "No clipping: at 4 choices a +2 reward (would be 6) is refused whole — it is not cut down to 5, and its ×0,50 damage cost is not applied");
        // Alt sınır 1.
        Boss.ClearAll();
        Require(Grant(minus1) && Grant(minus1) && RM.ChoicesPerLevel == 1, "Lower bound: two −1 rewards → 1 choice per level");
        Levels(2); pending += 2;
        Require(RM.PendingCardSelections == pending, "Two levels at the lower bound add 1 choice each");
        Require(Boss.BlockOf(minus1, true) == BossRewardBlock.LevelChoiceRange && !Boss.IsEligible(minus1) && !Grant(minus1) && RM.ChoicesPerLevel == 1 && Boss.Stacks(minus1) == 2 &&
                Near(BossRewardManager.BehaviorDamageMultiplier, 1.21f),
            "A third −1 would give 0: not eligible and refused; its behavior gain is not applied either (behavior stays ×1,21)");
        Boss.ClearAll();
        Require(Grant(minus1) && RM.ChoicesPerLevel == 2 && !Grant(minus2) && RM.ChoicesPerLevel == 2 && BossRewardManager.BehaviorDamageMultiplier > 1.09f && BossRewardManager.BehaviorDamageMultiplier < 1.11f,
            "No clipping at the bottom: at 2 choices a −2 reward (would be 0) is refused whole — not cut to 1, its ×2 behavior gain is not applied");
        Boss.ClearAll();
        Require(RM.ChoicesPerLevel == 3 && RM.LevelsGained == 13 && RM.CardChoicesGranted == pending && RM.CardsTaken == 0,
            $"Counters stay separate: {RM.LevelsGained} levels gained, {RM.CardChoicesGranted} choices granted, {RM.PendingCardSelections} pending, 0 cards taken (no card was picked in this artificial-XP check)");
        Object.DestroyImmediate(withTest);
        foreach (var r in new[] { plus1, plus2, minus1, minus2 }) Object.DestroyImmediate(r);
        return .05;
    }

    // ---------------------------------------------------------------- D) atomik uygulama
    static double Atomic()
    {
        var og = Ogrenim; var ad = Adanis; var blade = R("keskin_bicak"); var yikim = R("yikim_gucu");
        Boss.ClearAll(); RewardOfferLab.Build(RewardOfferLab.Layout.Multi);
        // Teklifte olmayan ödül.
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(blade); SetF(Boss, "offerPrepared", true); SetP(Boss, "IsPending", true);
        int version = Boss.Version;
        Require(!Boss.Choose(og) && !Boss.Choose(null) && Boss.IsPending && Boss.Taken.Count == 0 && RM.ChoicesPerLevel == 3 && BossRewardManager.DirectDamageMultiplier == 1f && Boss.Version == version,
            "A reward that is not in the pending offer is refused: no effect, no stack, no taken entry, the offer still waits");
        Require(Boss.Choose(blade) && !Boss.IsPending && !Boss.Choose(blade) && !Boss.Choose(og) && Boss.Stacks(blade) == 1 && Near(BossRewardManager.DirectDamageMultiplier, blade.directDamageMultiplier),
            "The offered reward is taken once; a second request (double click) and a request for another reward after it change nothing");
        Boss.ClearAll();

        // Uygulama sırasında hata: ikinci modifier eklenirken bir dinleyici hata verir.
        var m1 = Mod(StatType.HarvestDamage, StatTarget.Player, .10f, ModifierOperation.MorePercent);
        var m2 = Mod(StatType.CritMultiplier, StatTarget.Player, .25f, ModifierOperation.Flat);
        var broken = TestReward("t_bozuk", +1, .5f, 2f);
        broken.modifiersPerStack = new List<StatModifier> { m1, m2 };
        int mods = Stats.GlobalModifiers.Count, owned = Boss.OwnedModifiers.Count, logs = expectedLogs.Count;
        float damage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        Action<StatModifier> boom = m => { if (m.Equals(m2)) throw new InvalidOperationException("test: dinleyici hatası"); };
        Stats.OnGlobalModifierAdded += boom;
        bool taken;
        try { taken = Grant(broken); } finally { Stats.OnGlobalModifierAdded -= boom; }
        Require(!taken && Stats.GlobalModifiers.Count == mods && Boss.OwnedModifiers.Count == owned && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), damage) &&
                BossRewardManager.DirectDamageMultiplier == 1f && BossRewardManager.BehaviorDamageMultiplier == 1f && RM.ChoicesPerLevel == 3 && Boss.Taken.Count == 0 && Boss.Stacks(broken) == 0 &&
                expectedLogs.Count == logs + 1,
            "Failure in the middle of applying (a listener throws on the second stat modifier): the first modifier is rolled back, and no multiplier, no choice change, no stack and no taken " +
            "entry is left behind; one clear error is logged");
        Note("beklenen hata · " + expectedLogs.Last());
        Require(Grant(broken) && Stats.GlobalModifiers.Count == mods + 2 && Boss.OwnedModifiers.Count == owned + 2 && Near(BossRewardManager.DirectDamageMultiplier, .5f) &&
                Near(BossRewardManager.BehaviorDamageMultiplier, 2f) && RM.ChoicesPerLevel == 4 && Boss.Stacks(broken) == 1 && !Grant(broken) && Stats.GlobalModifiers.Count == mods + 2 &&
                Near(BossRewardManager.DirectDamageMultiplier, .5f) && RM.ChoicesPerLevel == 4,
            "Without the failing listener the same reward applies all of its effects exactly once (2 modifiers, both multipliers, +1 choice); asking again adds nothing");
        Boss.ClearAll();
        Require(Stats.GlobalModifiers.Count == mods && RM.ChoicesPerLevel == 3 && BossRewardManager.DirectDamageMultiplier == 1f && BossRewardManager.BehaviorDamageMultiplier == 1f,
            "ClearAll removes exactly what the reward added");
        Object.DestroyImmediate(broken);

        // Koşul alınırken de aranır (bedelli ödül): teklif hazırlandıktan sonra davranış kalmadıysa Adanış reddedilir.
        Must(Boss.IsEligible(ad), "Adanış should be eligible on the multi-behavior field");
        RewardOfferLab.ClearField();
        Require(!Grant(ad) && Boss.BlockOf(ad, true) == BossRewardBlock.Condition && Boss.Taken.Count == 0 && RM.ChoicesPerLevel == 3 && BossRewardManager.BehaviorDamageMultiplier == 1f,
            "Required behavior is checked again when the reward is taken: with no behavior planter left, Davranışa Adanış is refused and its cost (−1 choice) is not paid");
        Require(!yikim.HasCost && Grant(yikim) && Near(BossRewardManager.BehaviorDamageMultiplier, yikim.behaviorDamageMultiplier),
            "Rewards without a cost keep the old rule (condition checked when the offer is built, not when it is taken): Yıkım Gücü forced into an offer is still applied");
        Boss.ClearAll();
        // Aynı sahnede yeni run (BeginRun): bu sisteme ait her şey sıfırlanır.
        Must(Grant(og) && RM.ChoicesPerLevel == 4 && RM.PendingCardSelections > 0 && RM.LevelsGained > 0, "Öğrenim should be active before the new run");
        RM.BeginRun();
        Require(State == GameStates.RunSetup && RM.ChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 0 && BossRewardManager.DirectDamageMultiplier == 1f && Boss.Taken.Count == 0 &&
                Boss.Stacks(og) == 0 && RM.LevelsGained == 0 && RM.CardChoicesGranted == 0 && RM.PendingCardSelections == 0 && RM.CardsTaken == 0 && Boss.IsEligible(og),
            "New run in the same scene: 3 choices per level again, direct ×1, no taken reward; level, granted-choice, pending and taken-card counters are back to 0");
        return .05;
    }

    // ---------------------------------------------------------------- E) hasar: formül
    static int Direct(PlantRarity? rarity = null, float attack = 1f) => Mathf.Max(1, Mathf.RoundToInt(RunPower.DirectDamage(100f, 1f, rarity, attack)));
    static readonly DamageType[] Behaviors = { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang };

    static double Formula()
    {
        var og = Ogrenim; var ad = Adanis;
        Boss.ClearAll();
        BuildSingle("Explosive", 1f);
        var planter = placed.First(p => p.GetFinalStat(StatType.ExplosionChance) > 0f);
        bool AllBehaviors(int expected) => Behaviors.All(t => planter.GetBehaviorDamage(100, t) == expected);
        var loadout = StartLoadoutManager.Instance;
        Must(SpecializationManager.DirectMultiplier == 1f && SpecializationManager.BehaviorMultiplier == 1f && StartLoadoutManager.DirectDamageMultiplier == 1f && Spec.Chosen == null,
            "the formula check needs a neutral specialization and start loadout");
        Require(Direct() == 100 && AllBehaviors(100), "No reward: direct 100 → 100; explosion, electric, tornado and boomerang 100 → 100");
        Grant(og);
        Require(Direct() == 80 && AllBehaviors(100), "Bereketli Öğrenim: direct 100 → 80; all four behaviors stay 100 (the penalty does not leak into behavior damage)");
        Boss.ClearAll(); Grant(ad);
        Require(Direct() == 100 && AllBehaviors(150) && Near(planter.BehaviorDamageMultiplier(DamageType.Tornado), 1.5f),
            "Davranışa Adanış: direct stays 100; explosion, electric, tornado and boomerang 100 → 150");
        // Uzmanlaşma.
        var usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/DavranisUstasi.asset");
        var bicici = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/UstaBicici.asset");
        SetP(Spec, "Chosen", usta);
        Require(usta.behaviorDamageMultiplier == 1.25f && AllBehaviors(188) && Direct() == 100,
            "Davranış Ustası ×1,25 with Adanış: 100 × 1,25 × 1,50 = 187,5 → 188 (each coefficient once, one rounding); direct 100");
        Boss.ClearAll(); Grant(og); SetP(Spec, "Chosen", bicici);
        Require(bicici.directDamageMultiplier == 1.25f && bicici.behaviorDamageMultiplier == .85f && Direct() == 100 && AllBehaviors(85),
            "Usta Biçici (direct ×1,25, behavior ×0,85) with Öğrenim: direct 100 × 1,25 × 0,80 = 100; behavior 85");
        SetP(Spec, "Chosen", null);
        // Tırpan (başlangıç seçimi) doğrudan çarpanı.
        SetF(loadout, "direct", 1.4f);
        Require(Direct() == 112 && AllBehaviors(100), "Scythe multiplier ×1,40 with Öğrenim: direct 100 × 1,40 × 0,80 = 112; behaviors 100");
        SetF(loadout, "direct", 1f);
        // Diğer boss çarpanları.
        var blade = R("keskin_bicak"); var yikim = R("yikim_gucu"); var gold = R("altin_hedef");
        Grant(blade);
        Require(Near(blade.directDamageMultiplier, 1.25f) && Direct() == 100 && AllBehaviors(100), "Keskin Bıçak ×1,25 with Öğrenim: direct 100 × 1,25 × 0,80 = 100");
        Grant(gold);
        Require(Near(gold.rareDirectMultiplier, 1.5f) && gold.rareDirectFrom == PlantRarity.Rare && Direct(PlantRarity.Legendary) == 150 && Direct(PlantRarity.Common) == 100,
            "Altın Hedef (×1,50 on Rare and above) with Keskin Bıçak and Öğrenim: 100 × 1,25 × 0,80 × 1,50 = 150 on a Legendary plant, 100 on a Common one");
        Require(Direct(null, 1.75f) == 175, "A Hasat Ritmi attack (×1,75 for that attack) multiplies the same product once: 100 × 1,25 × 0,80 × 1,75 = 175");
        Boss.ClearAll(); Grant(ad); Grant(yikim);
        Require(Near(yikim.behaviorDamageMultiplier, 1.3f) && AllBehaviors(195) && Direct() == 100, "Yıkım Gücü ×1,30 with Adanış: behavior 100 × 1,30 × 1,50 = 195; direct 100");
        Boss.ClearAll();
        return .05;
    }

    // ---------------------------------------------------------------- E) hasar: gerçek vuruşlar
    static readonly List<(DamageType type, int damage, bool echo)> hits = new();
    static void OnDamaged(PlantHealth plant, int damage, DamageType type) => hits.Add((type, damage, BehaviorEchoes.IsExecuting));

    static double LabRound()
    {
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == 1 && RM.IsRoundActive, "round 1 should be running for the real-hit checks");
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
        Strike(100f, .4f);
        PlantHealth.AnyDamaged += OnDamaged;
        return .05;
    }

    static PlantHealth PlantOf(PlanterBrain brain)
    {
        var grid = GridManager.Instance.GetGridSystem().GetGridObject(brain.OccupiedGrids[0].GetGroundCellCached().GetGridPosition());
        var plant = grid.GetPlantObject();
        return plant != null ? plant.GetComponent<PlantHealth>() : null;
    }

    // Ortadaki saksının altında verilen davranış (%100 şans); bütün saksılarda canı yüksek taze bitki. Ortadaki bitkinin canı 1.
    static PlanterBrain Source(string tile)
    {
        BuildSingle(tile, 1f);
        var source = placed.First(p => p.OccupiedGrids[0].GetGroundCellCached() == center);
        foreach (var brain in placed)
        {
            foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); }
            var plant = PlantOf(brain);
            if (plant == null) throw new Exception("no plant spawned for the test field");
            SetF(plant, "maxHealth", 1000000); SetF(plant, "currentHealth", brain == source ? 1 : 1000000);
        }
        hits.Clear();
        return source;
    }

    static void Arm(string arm)
    {
        Boss.ClearAll();
        if (arm == "ogrenim") Must(Grant(Ogrenim), "Öğrenim not granted");
        if (arm == "adanis") Must(Grant(Adanis), "Adanış not granted");
    }

    static void Kill() => Call(Player, "AttackInRadius", center.transform.position);
    static float DirectOf(string arm) => arm == "ogrenim" ? .8f : 1f;
    static float BehaviorOf(string arm) => arm == "adanis" ? 1.5f : 1f;
    static string ArmName(string arm) => arm == "ogrenim" ? "Bereketli Öğrenim" : arm == "adanis" ? "Davranışa Adanış" : "ödülsüz";

    static double HitsInstant(string arm)
    {
        int expected = (int)Math.Round(100 * (double)BehaviorOf(arm));
        // Patlama: dört komşu. (Ödül, davranışlı tarla kurulduktan sonra alınır: Adanış'ın koşulu.)
        Source("Explosive"); Arm(arm); hits.Clear();
        Kill();
        var explosion = hits.Where(h => h.type == DamageType.Explosion).ToList();
        Must(explosion.Count == 4 && explosion.All(h => h.damage == expected), $"[{arm}] explosion hits: {string.Join(",", explosion.Select(h => h.damage))}, expected 4 × {expected}");
        // Elektrik: dört çapraz komşu.
        Source("Electric");
        Kill();
        var electric = hits.Where(h => h.type == DamageType.Electric).ToList();
        Must(electric.Count == 4 && electric.All(h => h.damage == expected), $"[{arm}] electric hits: {string.Join(",", electric.Select(h => h.damage))}, expected 4 × {expected}");
        // Doğrudan vuruş: aynı zarla beklenen değer (sapma × çarpan, tek yuvarlama); davranışı olmayan tek hedef.
        BuildSingle(null, 0f);
        var only = placed.First(p => p.OccupiedGrids[0].GetGroundCellCached() == center);
        foreach (var spawner in only.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); }
        var plant = PlantOf(only);
        SetF(plant, "maxHealth", 1000000); SetF(plant, "currentHealth", 1000000);
        var directs = new List<int>(); var expectedDirects = new List<int>();
        for (int i = 0; i < 20; i++)
        {
            var state = UnityEngine.Random.state;
            float variance = UnityEngine.Random.Range(.85f, 1.15f);
            UnityEngine.Random.state = state;
            hits.Clear();
            Kill();
            Must(hits.Count == 1 && hits[0].type == DamageType.Direct, $"[{arm}] a direct attack should hit exactly the one plant ({hits.Count} hits)");
            directs.Add(hits[0].damage);
            expectedDirects.Add(plant.GetIncomingDamage(Mathf.Max(1, Mathf.RoundToInt(100f * variance * DirectOf(arm)))));
        }
        Must(directs.SequenceEqual(expectedDirects), $"[{arm}] direct hits {string.Join(",", directs)} expected {string.Join(",", expectedDirects)}");
        int low = Mathf.RoundToInt(85f * DirectOf(arm)), high = Mathf.RoundToInt(115f * DirectOf(arm));
        Require(directs.Min() >= low && directs.Max() <= high,
            $"Real hits · {ArmName(arm)}: explosion 4 × {expected}, electric 4 × {expected}; 20 direct hits = round(100 × variance × {BossRewardText.Number(DirectOf(arm))}) each " +
            $"(range {directs.Min()}–{directs.Max()}, allowed {low}–{high})");
        // Kasırga.
        Source("Tornado"); Arm(arm); hits.Clear();
        Kill();
        return 4.6;
    }

    static double HitsTornado(string arm)
    {
        int expected = (int)Math.Round(Mathf.Max(1, Mathf.RoundToInt(100f * .5f)) * (double)BehaviorOf(arm));
        var tornado = hits.Where(h => h.type == DamageType.Tornado).ToList();
        Must(tornado.Count >= 2 && tornado.All(h => h.damage == expected), $"[{arm}] tornado hits: {string.Join(",", tornado.Select(h => h.damage))}, expected {expected} each");
        Require(hits.All(h => h.type == DamageType.Tornado || h.type == DamageType.Direct),
            $"Real hits · {ArmName(arm)}: tornado {tornado.Count} hits of {expected} each (50 × {BossRewardText.Number(BehaviorOf(arm))})");
        Source("Boomerang"); Arm(arm); hits.Clear();
        Kill();
        return 3.8;
    }

    static double HitsBoomerang(string arm)
    {
        int expected = (int)Math.Round(Mathf.Max(1, Mathf.RoundToInt(100f * .65f)) * (double)BehaviorOf(arm));
        var boomerang = hits.Where(h => h.type == DamageType.Boomerang).ToList();
        Must(boomerang.Count >= 1 && boomerang.All(h => h.damage == expected), $"[{arm}] boomerang hits: {string.Join(",", boomerang.Select(h => h.damage))}, expected {expected} each");
        Require(true, $"Real hits · {ArmName(arm)}: boomerang {boomerang.Count} hits of {expected} each (65 × {BossRewardText.Number(BehaviorOf(arm))})");
        return .05;
    }

    // Artçı Patlama ve Çifte Akım, Adanış'la: ikinci darbe ilk darbenin HESAPLANMIŞ hasarından üretilir; katsayı yeniden çarpılmaz.
    static double EchoExplosion()
    {
        var artci = R("artci_patlama"); var cifte = R("cifte_akim");
        Source("Explosive"); Arm("adanis");
        Must(Grant(artci), "Artçı Patlama not granted");
        Source("Explosive");
        Kill();
        var first = hits.Where(h => h.type == DamageType.Explosion && !h.echo).ToList();
        Must(first.Count == 4 && first.All(h => h.damage == 150) && Echoes.Pending == 1, $"first explosion with Adanış: {string.Join(",", first.Select(h => h.damage))}");
        return artci.echoDelay + .9;
    }

    static double EchoExplosionResult()
    {
        var artci = R("artci_patlama"); var cifte = R("cifte_akim");
        int expected = BehaviorEchoes.ScaleDamage(150, artci.echoDamage);
        var echo = hits.Where(h => h.echo).ToList();
        Require(echo.Count >= 4 && echo.All(h => h.type == DamageType.Explosion && h.damage == expected) && expected < 225 && Echoes.Pending == 0,
            $"Artçı Patlama with Adanış: the first explosion hits for 150, the aftershock hits {echo.Count} plants for {expected} = 150 × {BossRewardText.Number(artci.echoDamage)} " +
            "(the computed damage × the reward's ratio; ×1,50 is not multiplied in a second time — that would be 225)");
        Source("Electric");
        Must(Grant(cifte), "Çifte Akım not granted");
        Source("Electric");
        Kill();
        var first = hits.Where(h => h.type == DamageType.Electric && !h.echo).ToList();
        Must(first.Count == 4 && first.All(h => h.damage == 150) && Echoes.Pending == 1, $"first electric wave with Adanış: {string.Join(",", first.Select(h => h.damage))}");
        return cifte.echoDelay + .9;
    }

    static double EchoElectricResult()
    {
        var cifte = R("cifte_akim");
        int expected = BehaviorEchoes.ScaleDamage(150, cifte.echoDamage);
        var echo = hits.Where(h => h.echo).ToList();
        Require(echo.Count == 4 && echo.All(h => h.type == DamageType.Electric && h.damage == expected) && expected < 225,
            $"Çifte Akım with Adanış: first wave 150, second wave {echo.Count} hits of {expected} = 150 × {BossRewardText.Number(cifte.echoDamage)} (not 225)");
        PlantHealth.AnyDamaged -= OnDamaged;
        Boss.ClearAll(); ClearTestMods(); RewardOfferLab.ClearField();
        return .05;
    }

    // ---------------------------------------------------------------- F) run sürücüsü
    static string runName; static int[] D; static long[] Q, BT;
    static long expectedTotal; static int rewardsTaken, baseModifiers = -1, granted, cardsTaken, levels;
    static readonly List<string> headers = new();

    static int Period(int round) { for (int i = 0; i < D.Length; i++) if (round <= D[i]) return i + 1; return D.Length; }
    static int First(int p) => p == 1 ? 1 : D[p - 2] + 1;

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double Begin(string name)
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != B1) return Again;
        runName = name;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        RunCalendar c = B1.Calendar;
        D = Enumerable.Range(1, B1.runLength).Where(c.IsPeriodEnd).ToArray();
        Q = D.Select((_, i) => c.QuotaTarget(i + 1)).ToArray(); BT = D.Select((_, i) => c.BossTarget(i + 1)).ToArray();
        // Temiz başlangıç: seçim hakkı 3, sayaçlar 0, bu sisteme ait hiçbir etki yok.
        int modifiers = Stats.GlobalModifiers.Count;
        if (baseModifiers < 0) baseModifiers = modifiers;
        Require(RM.CurrentRound == 1 && RM.IsPreparingFirstRound && RM.Outcome == RunOutcome.None && Total == 0 && RM.PendingCardSelections == 0 && RM.LevelsGained == 0 && RM.CardChoicesGranted == 0 &&
                RM.CardsTaken == 0 && RM.ChoicesPerLevel == 3 && RM.BaseChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 0 && Boss.Taken.Count == 0 && !Boss.IsPending && !Prepared &&
                Boss.OwnedModifiers.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f && BossRewardManager.BehaviorDamageMultiplier == 1f && modifiers == baseModifiers && Progress.CurrentLevel == 1,
            $"{name}: starts clean in RunSetup — 3 choices per level, no reward, direct ×1, behavior ×1, counters 0, {modifiers} global modifiers (same as the first scene)");
        RewardOfferLab.Build(RewardOfferLab.Layout.Multi);
        expectedTotal = 0; rewardsTaken = 0; granted = 0; cardsTaken = 0; levels = 0; headers.Clear();
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
        long bossTarget = RM.Calendar.HasBoss(p) ? BT[p - 1] : 0;
        if (r == first) add += Q[p - 1] - bossTarget;
        if (r == B) add += bossTarget;
        AddScore(add); expectedTotal += add;
        if (level)
        {
            int choices = RM.ChoicesPerLevel;
            Levels(1); levels++; granted += choices;
            Must(RM.PendingCardSelections == choices && RM.CardChoicesGranted == granted && RM.LevelsGained == levels, $"R{r}: a level should queue {choices} card choices ({RM.PendingCardSelections})");
        }
        // HUD görüntüsü round sırasında alınır (ödül alındıktan sonraki ilk round).
        if (r == 24)
        {
            string shot = runName.StartsWith("Tam") ? "T374_02_R24_HUD_Ogrenim" : "T374_05_R24_HUD_Adanis";
            Call(Hud, "Refresh");
            InsertNext(("görüntü " + shot, () => { Capture(shot); return .05; }));
            return .7;
        }
        return .05;
    }

    static double AfterRound(int r, Func<int, double> atBoss)
    {
        if (RM.IsRoundActive) return Again;
        int p = Period(r), B = D[p - 1];
        Must(Total == expectedTotal, $"{runName} R{r}: total score {Total}, expected {expectedTotal}");
        if (r != B || !RM.IsBossRound(r))
        {
            if (State == GameStates.CardSelection) TakeCards(r);
            Must(State == GameStates.RoundEnd && !Boss.IsPending, $"{runName} R{r}: an ordinary round should end in the round summary without an offer ({State})");
            return .03;
        }
        Must(!RM.RunFailed && Boss.IsPending && RM.LastBossRound == r, $"{runName} R{r}: the boss should be passed and a reward pending ({State}, failed {RM.RunFailed})");
        return atBoss(r);
    }

    // Bekleyen seçimleri çözer ve üç sayacı (verilen hak, alınan kart, bekleyen) denetler.
    static int TakeCards(int r)
    {
        int pending = RM.PendingCardSelections;
        int picks = ResolveCards();
        cardsTaken += picks;
        Must(picks == pending && RM.PendingCardSelections == 0 && RM.CardsTaken == cardsTaken && RM.CardChoicesGranted == granted,
            $"R{r}: {picks} card choices made for {pending} pending (taken counter {RM.CardsTaken}, expected {cardsTaken}; granted {RM.CardChoicesGranted})");
        return picks;
    }

    static string TextOf(BossRewardPanelUI panel, int index, string name) => panel.CardTexts(index).First(t => t.name == name).text;

    // Kart yazıları kendi bölgesine sığıyor mu: kesilme / taşma yok, punto bölgenin en küçüğünün altına inmemiş.
    static string FitProblem(BossRewardPanelUI panel, int index, bool trade)
    {
        foreach (var text in panel.CardTexts(index))
        {
            if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            Rect rect = text.rectTransform.rect; Vector3 size = text.textBounds.size;
            float min = text.name == "Effect" ? (trade ? 17f : 20f) : text.name == "Note" || text.name == "Stack" ? 15f : 14f;
            if (text.isTextTruncated) return $"{text.name} text is cut: \"{Strip(text.text)}\"";
            if (size.x > rect.width + 1.5f || size.y > rect.height + 1.5f) return $"{text.name} text {size.x:0}×{size.y:0} does not fit {rect.width:0}×{rect.height:0}: \"{Strip(text.text)}\"";
            if (text.fontSize < min - .05f) return $"{text.name} text shrank to {text.fontSize:0.#} pt (minimum {min})";
        }
        return null;
    }

    // Ekrandaki panel: başlık (aşama + "BOSS ÖDÜLÜ"), kart sayısı, her kartın etiketi, yazıların sığması.
    static List<BossRewardSO> CheckPanel(int r)
    {
        var pool = Boss.Pool; var panel = Panel; int stage = pool.StageIndexAt(r);
        var offer = Boss.Offer.ToList();
        Call(panel, "Fill");
        Must(panel.gameObject.activeInHierarchy && Boss.OfferStage == pool.stages[stage] && panel.TitleText == StageNames[stage] + " AŞAMA · BOSS ÖDÜLÜ" && panel.ShownCards == offer.Count,
            $"R{r}: panel title \"{panel.TitleText}\" / {panel.ShownCards} cards, expected {StageNames[stage]} AŞAMA · BOSS ÖDÜLÜ / {offer.Count}");
        for (int i = 0; i < offer.Count; i++)
        {
            int s = pool.StageIndexOf(offer[i]);
            string tag = $"{StageNames[s]} AŞAMA · Round {StageFirstBoss[s]} boss'undan itibaren";
            Must(panel.StageTagText(i) == tag, $"R{r}: card {i} ({offer[i].displayName}) tag \"{panel.StageTagText(i)}\"");
            string fit = FitProblem(panel, i, BossRewardText.IsTrade(offer[i]));
            Must(fit == null, $"R{r}: card {i} ({offer[i].displayName}): {fit}");
        }
        return offer;
    }

    // Kartın kazanç / bedel yazıları: gerçek seçim sayıları, sıra (önce KAZANÇ), geçerlilik notu, birlikte alınamayan ödül.
    static void CheckTradeCard(BossRewardPanelUI panel, int index, BossRewardSO reward, string gain, string cost, string other)
    {
        string effect = Strip(TextOf(panel, index, "Effect")), note = TextOf(panel, index, "Note"), stack = Strip(TextOf(panel, index, "Stack"));
        Must(effect == $"KAZANÇ / {gain} / BEDEL / {cost}", $"{reward.displayName} effect text: \"{effect}\"");
        Must(note.StartsWith(PendingNote) && note.Contains(reward.note), $"{reward.displayName} note: \"{note}\"");
        Must(stack == $"Bir kez alınır / Bunu alırsan {other} bir daha sunulmaz", $"{reward.displayName} bottom text: \"{stack}\"");
    }

    static string Take(int r, List<BossRewardSO> offer, BossRewardSO pick = null)
    {
        pick ??= offer[0];
        int before = TotalStacks;
        Must(!Boss.ContinueWithoutReward() && Boss.Choose(pick) && TotalStacks == before + 1 && !Boss.Choose(offer[offer.Count - 1]) && !Boss.Choose(pick) && TotalStacks == before + 1 &&
             !Boss.IsPending, $"R{r}: exactly one reward should be taken");
        rewardsTaken++;
        return pick.displayName;
    }

    // R23 teklifini, istenen kartları içeren bir seed'le yeniden hazırlatır (yalnız görüntü ve kart yazısı kontrolü için).
    static void Reseed(Func<List<BossRewardSO>, bool> wanted)
    {
        int seed = Enumerable.Range(1, 3000).First(s => wanted(RewardOfferLab.Offer(Pool, 23, s)));
        SetP(Events, "RunSeed", seed);
        F<List<BossRewardSO>>(Boss, "offer").Clear();
        SetF(Boss, "offerPrepared", false);
        Boss.PrepareOffer();
        Must(Prepared && wanted(Boss.Offer.ToList()), "R23: the re-prepared offer should hold the wanted cards");
    }

    // ---------------------------------------------------------------- F) tam run: Bereketli Öğrenim
    static List<BossRewardSO> pendingOffer;
    static string finalReward;

    static double BossInFullRun(int r)
    {
        var pool = Pool; var og = Ogrenim; var ad = Adanis;
        if (State == GameStates.CardSelection)
        {
            Must(!Prepared && Boss.IsPending, $"R{r}: the offer must not be prepared before the level cards");
            int picks = TakeCards(r);
            Must(State == GameStates.RoundChoice, $"R{r}: after the cards the reward selection should open ({State})");
            if (r == 23) Require(picks == 3 && RM.ChoicesPerLevel == 3, "R23, level gained before the reward: 3 card choices (each showing 3 candidates), then the reward screen");
        }
        else Must(State == GameStates.RoundChoice, $"R{r}: reward selection expected ({State})");
        // Yalnız test düzeneği: bu boss'un seed'i, iki bedelli ödül aynı teklifte çıkacak biçimde seçilir ve teklif o seed'le
        // (oyunun kendi teklif koduyla) yeniden hazırlanır. Kural kontrolleri bundan bağımsızdır.
        if (r == 23) Reseed(o => o.Contains(og) && o.Contains(ad));
        var before = UnityEngine.Random.state;
        var offer = CheckPanel(r);
        Must(Prepared && before.Equals(UnityEngine.Random.state), $"R{r}: preparing / showing the offer consumed the gameplay random stream");
        string problem = RuleProblem(offer, r);
        Must(problem == null, $"R{r}: live offer breaks the rule: {problem} ({Ids(offer)})");
        if (r < 23) Must(!offer.Contains(og) && !offer.Contains(ad), $"R{r}: a cost reward was offered before R23");
        if (r > 23) Must(!offer.Contains(og) && !offer.Contains(ad), $"R{r}: a cost reward was offered after Öğrenim was taken ({Ids(offer)})");
        headers.Add($"R{r} {Panel.TitleText}");
        pendingOffer = offer;
        if (r == 23)
        {
            var panel = Panel; int io = offer.IndexOf(og), ia = offer.IndexOf(ad);
            CheckTradeCard(panel, io, og, "Gelecekteki her level: 3 → 4 seçim", "Doğrudan vuruş hasarı ×0,80", "Davranışa Adanış");
            CheckTradeCard(panel, ia, ad, "Davranış hasarı ×1,50", "Gelecekteki her level: 3 → 2 seçim", "Bereketli Öğrenim");
            Require(panel.TitleText == "GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ" && panel.SubtitleText == "Boss geçildi · yalnız biri alınır · run sonuna kadar geçerli · etkisi Round 24 ve sonrası",
                $"R23 reward screen: \"{panel.TitleText}\" / \"{panel.SubtitleText}\" — cards: {Ids(offer)}");
            Require(true, $"Bereketli Öğrenim card: \"{Strip(TextOf(panel, io, "Effect"))}\" · \"{TextOf(panel, io, "Note")}\" · \"{Strip(TextOf(panel, io, "Stack"))}\"");
            Require(true, $"Davranışa Adanış card: \"{Strip(TextOf(panel, ia, "Effect"))}\" · \"{TextOf(panel, ia, "Note")}\" · \"{Strip(TextOf(panel, ia, "Stack"))}\"");
            InsertNext(("görüntü R23", () => { Capture("T374_01_R23_IkiBedelliOdul"); return .05; }), ("R23 ödül alınır", () => TakeInFullRun(23)));
            return .7;
        }
        if (r == 26)
        {
            InsertNext(("görüntü R26", () => { Capture("T374_03_R26_AlinanOdulListesi"); return .05; }), ("R26 ödül alınır", () => TakeInFullRun(26)));
            return .7;
        }
        return TakeInFullRun(r);
    }

    static double TakeInFullRun(int r)
    {
        var pool = Pool; var og = Ogrenim; var ad = Adanis; var offer = pendingOffer;
        if (r == 23)
        {
            int pending = RM.PendingCardSelections, given = RM.CardChoicesGranted, stacks = TotalStacks;
            Must(Boss.Choose(og) && !Boss.Choose(ad) && !Boss.Choose(og) && TotalStacks == stacks + 1 && !Boss.IsPending, "R23: Öğrenim should be taken exactly once");
            rewardsTaken++;
            Require(Boss.Stacks(og) == 1 && Boss.Stacks(ad) == 0 && RM.ChoicesPerLevel == 4 && BossRewardManager.LevelChoiceDelta == 1 && Near(BossRewardManager.DirectDamageMultiplier, .8f * Product(x => x.directDamageMultiplier, og)) &&
                    RM.PendingCardSelections == pending && RM.CardChoicesGranted == given,
                "R23: Bereketli Öğrenim taken from the real offer; a click on the other card and a second click do nothing (1 / 1, effect once); a level now gives 4 choices; granted and pending counters untouched");
            // HUD ve alınan ödül listesi.
            Call(Hud, "Refresh");
            object line1 = Call(Hud, "ChoiceLine", RM), line2 = Call(Hud, "ChoiceLine", RM);
            Require(Hud.ChoiceText == "Level başına seçim: 4 (temel 3) · yeni kazanılan level'lar" && ReferenceEquals(line1, line2) &&
                    Hud.RewardListText.Contains("Bereketli Öğrenim 1/1 · Level seçimi +1 · Doğrudan ×0,80"),
                $"HUD: \"{Hud.ChoiceText}\" and reward list line \"Bereketli Öğrenim 1/1 · Level seçimi +1 · Doğrudan ×0,80\"; the line text is built once and reused until the value changes");
        }
        else
        {
            string taken = Take(r, offer);
            if (r == 50) finalReward = taken;
        }
        if (r == 50) { Must(State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory, "R50: victory expected after the reward"); return .3; }
        Must(State == GameStates.RoundEnd, $"R{r}: round summary expected after the reward ({State})");
        if (r == 26)
        {
            string list = Strip(F<TextMeshProUGUI>(Panel, "takenList").text);
            Require(Enumerable.Range(1, 200).All(s => { var o = RewardOfferLab.Offer(pool, 30, s); return !o.Contains(og) && !o.Contains(ad); }) && RM.ChoicesPerLevel == 4,
                "R26 and later: neither cost reward is offered again in this run (live state, 200 seeds for R30); the level choice stays 4");
            Note("R26 alınan ödül listesi (ekran): " + list);
        }
        return .05;
    }

    static float Product(Func<BossRewardSO, float> f, BossRewardSO except) => Boss.Taken.Where(r => r != except).Aggregate(1f, (a, r) => a * Mathf.Pow(f(r), Boss.Stacks(r)));

    // R24: ödülden sonra kazanılan level 4 seçim getirir (StartRound denetler); görüntü round sırasında alınır.
    static double Victory()
    {
        string end = EndText();
        Require(headers.Count == 15 && headers.Take(3).All(h => h.EndsWith("ERKEN AŞAMA · BOSS ÖDÜLÜ")) && headers.Skip(3).Take(3).All(h => h.EndsWith("ORTA AŞAMA · BOSS ÖDÜLÜ")) &&
                headers.Skip(6).All(h => h.EndsWith("GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ")),
            "Whole run R1–50: the reward screen header named the stage at all 15 bosses — " + string.Join(" · ", headers));
        Require(levels == 4 && RM.LevelsGained == 4 && granted == 3 + 4 + 4 + 4 && RM.CardChoicesGranted == granted && RM.CardsTaken == granted && RM.PendingCardSelections == 0,
            $"Levels in the run: R23 (before the reward) 3 choices; R24, R30 and R50 (after it) 4 choices each — {RM.LevelsGained} levels, {RM.CardChoicesGranted} choices granted, {RM.CardsTaken} cards taken, 0 pending");
        Require(State == GameStates.RunComplete && RM.Outcome == RunOutcome.Victory && end.StartsWith("RUN TAMAMLANDI · BEDELLİ ÖDÜLLER V1") && end.Contains(finalReward) &&
                end.Contains("Bereketli Öğrenim → kazanç: level başına seçim +1 · bedel: doğrudan vuruş hasarı ×0,80") && end.Contains("Level başına seçim: 4 (temel 3)") && rewardsTaken == TotalStacks,
            $"Run end screen lists the rewards and spells out the gain and the cost: {end}");
        InsertNext(("görüntü run sonu", () => { Capture("T374_04_RunSonu_KazancBedel"); return .05; }));
        return .7;
    }

    static double Restart()
    {
        RM.StartNextRound();
        Must(RM.CurrentRound == 50 && State == GameStates.RunComplete, "R51 should not start");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 2;
    }

    // ---------------------------------------------------------------- F) ikinci run: Davranışa Adanış
    static double BossInSecondRun(int r)
    {
        var pool = Pool; var og = Ogrenim; var ad = Adanis;
        Must(State == GameStates.RoundChoice, $"R{r}: reward selection expected ({State})");
        if (r == 23) Reseed(o => o.Contains(ad) && !o.Contains(og));
        var offer = CheckPanel(r);
        Must(RuleProblem(offer, r) == null, $"R{r}: live offer breaks the rule ({Ids(offer)})");
        if (r != 23)
        {
            Must(!offer.Contains(og) && !offer.Contains(ad), $"R{r}: unexpected cost reward in the offer ({Ids(offer)})");
            Take(r, offer);
            if (r == 26)
            {
                // Bekleyen teklif yokken, ödül etkisi varken ana menüye dönüş.
                Must(RM.ChoicesPerLevel == 2 && Boss.Stacks(ad) == 1, "R26: Adanış should still be active");
                GameManager.Instance.ReturnToMenu();
                Require(Boss.Taken.Count == 0 && Boss.OwnedModifiers.Count == 0 && BossRewardManager.LevelChoiceDelta == 0 && BossRewardManager.BehaviorDamageMultiplier == 1f &&
                        BossRewardManager.DirectDamageMultiplier == 1f && RM.ChoicesPerLevel == 3,
                    "Return to the main menu: the taken rewards and their effects are cleared — 3 choices per level, behavior ×1, direct ×1");
                HarvestScoreManager.Instance.ResetScore(); RM.ResetRounds(); Stats.ClearGlobalModifiers();
                SceneManager.LoadScene("MenuScene");
                return 2;
            }
            return .05;
        }
        var panel = Panel; int ia = offer.IndexOf(ad);
        CheckTradeCard(panel, ia, ad, "Davranış hasarı ×1,50", "Gelecekteki her level: 3 → 2 seçim", "Bereketli Öğrenim");
        int stacks = TotalStacks;
        Must(Boss.Choose(ad) && !Boss.Choose(ad) && TotalStacks == stacks + 1, "R23: Adanış should be taken exactly once");
        rewardsTaken++;
        Call(Hud, "Refresh");
        Require(RM.ChoicesPerLevel == 2 && BossRewardManager.LevelChoiceDelta == -1 && Near(BossRewardManager.BehaviorDamageMultiplier, 1.5f * Product(x => x.behaviorDamageMultiplier, ad)) &&
                Hud.ChoiceText == "Level başına seçim: 2 (temel 3) · yeni kazanılan level'lar" && Hud.RewardListText.Contains("Davranışa Adanış 1/1 · Davranış ×1,50 · Level seçimi −1"),
            $"İkinci run · R23: Davranışa Adanış taken once; a level now gives 2 choices; HUD: \"{Hud.ChoiceText}\" and \"Davranışa Adanış 1/1 · Davranış ×1,50 · Level seçimi −1\"");
        Must(State == GameStates.RoundEnd, "R23: round summary expected");
        return .05;
    }

    static double MenuCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && BossRewardManager.Instance == null && BossRewardManager.DirectDamageMultiplier == 1f &&
                BossRewardManager.BehaviorDamageMultiplier == 1f && BossRewardManager.LevelChoiceDelta == 0 && RunPower.LevelChoices(3) == 3,
            "Menu scene: no reward manager and no reward effect survives the scene change (choices per level back to the base value)");
        Require(levels == 1 && granted == 2 && cardsTaken == 2, "İkinci run · R24: the level gained after Adanış gave 2 card choices and 2 cards were taken (each choice still showed 3 candidates)");
        return .05;
    }

    // ---------------------------------------------------------------- eski profil
    static double Legacy()
    {
        if (RM == null || RM.Profile != K1 || State != GameStates.RunSetup) return Again;
        Player.enabled = false;
        var og = Ogrenim; var ad = Adanis; var pool = K1.bossRewards;
        RewardOfferLab.Build(RewardOfferLab.Layout.Multi); RewardOfferLab.SetOccupancy(.5f);
        bool none = new[] { 5, 10, 25, 45 }.All(round => Enumerable.Range(1, 200).All(s => { var o = RewardOfferLab.Offer(pool, round, s); return !o.Contains(og) && !o.Contains(ad) && o.Count == 3; }));
        Require(RM.BaseChoicesPerLevel == 3 && RM.ChoicesPerLevel == 3 && BossRewardManager.LevelChoiceDelta == 0 && none && (Hud == null || Hud.ChoiceText == null),
            "Old profile (Kırılma V1): 3 choices per level, no level-choice line on the HUD, and its pool never offers a cost reward (R5, R10, R25, R45 × 200 seeds)");
        Levels(2);
        Require(RM.PendingCardSelections == 6 && RM.CardChoicesGranted == 6 && RM.LevelsGained == 2, "Old profile: two levels still give 3 choices each");
        RewardOfferLab.ClearField();
        return .05;
    }

    static double FinalCheck()
    {
        Require(expectedLogs.Count == 1, "The only error message is the one from the deliberate apply-failure probe");
        Require(errors == 0, "No other error or exception logged during the test");
        return .05;
    }
}
