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

// Batch (izole kopya): Bölüm 3.7.7 (P7) İŞLEV testi — Run50_XPV1 verisi, tablo sonrası level maliyeti, level işleme, temel güç XP
// kartlarının birikim grubu, tile → yükseltme → temel güç geçişi, seçim akışı (arka arkaya, kalan seçim sayacı, "hesaplanıyor"
// durumu, sıra) ve eski profillerin korunması. Denge ölçümü DEĞİLDİR: tarla ve XP testin kurduğu sabit durumdur; XP doğrudan
// verilir (ProgressionManager.AddXP), kartlar oyunun kendi yoluyla seçilir (CardSelectionUI.OnCardSelected).
// Yüksek level adımları izole bir ilerleme testidir; endless oynanış testi değildir (oyunda R50 sonrası akış yok).
[InitializeOnLoad]
public static class XPV1Verification
{
    const string Key = "XPV1Verification";
    const string LogFile = "Logs/XPV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 37701;
    const double Again = double.NaN;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static XPV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        foreach (var profile in new[] { X1, Z1 }) { profile.bossSeed = Seed; EditorUtility.SetDirty(profile); }
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = X1;
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
    static BossRewardManager Boss => BossRewardManager.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO X1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_XPV1.asset");
    static RunProfileSO Z1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_ZincirV1.asset");
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static LevelWorkStatusUI Status => Object.FindFirstObjectByType<LevelWorkStatusUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static string Pct(float v) => (v * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    static bool Near(double a, double b, double relative = 1e-5) => Math.Abs(a - b) <= relative * Math.Max(1d, Math.Max(Math.Abs(a), Math.Abs(b)));
    static float XpStat => Stats.GetFinalStat(StatType.XPGainMultiplier, StatTarget.Planter);
    static float GroupTotal => Stats.SummedGroupTotal(StatType.XPGainMultiplier, StatTarget.Planter);
    static int XpMoreCount => Stats.GlobalModifiers.Count(m => m.statType == StatType.XPGainMultiplier && m.target == StatTarget.Planter && m.operation == ModifierOperation.MorePercent);

    static readonly List<(int level, int frame)> levelEvents = new();
    static void OnLevel(int level) => levelEvents.Add((level, Time.frameCount));
    static void Subscribe() { Progress.OnLevelUp += OnLevel; }
    static void Unsubscribe() { if (Progress != null) Progress.OnLevelUp -= OnLevel; }

    // Bağımsız referans: maliyet tablodan ya da formülden (C(L) = C(N) × [1 + s × (L − N)]), level'lar tek tek düşülür.
    static float RefCost(ProgressionSO table, float s, int level)
    {
        int n = table.xpRequirements.Count;
        if (level <= n) return Mathf.Max(1f, table.xpRequirements[level - 1]);
        double last = Mathf.Max(1f, table.xpRequirements[n - 1]);
        return (float)(last * (1.0 + (double)s * (level - n)));
    }
    static (int levels, double rest, double spent) Reference(ProgressionSO table, float s, int level, double xp)
    {
        int k = 0; double spent = 0;
        while (true)
        {
            double cost = RefCost(table, s, level + k);
            if (xp < cost) return (k, xp, spent);
            xp -= cost; spent += cost; k++;
        }
    }
    static double CostSum(ProgressionSO table, float s, int level, int count)
    {
        double sum = 0; for (int i = 0; i < count; i++) sum += RefCost(table, s, level + i);
        return sum;
    }

    // Kota dönemi sonu olmayan ilk round (testin round sonları run'ı kota yüzünden bitirmesin).
    static int SafeRound(int from) { int r = from; while (RM.Profile.Calendar.IsPeriodEnd(r)) r++; return r; }
    static void StartRound(int round)
    {
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
    }
    // Round'u bitirir ve kart ekranını açar (bekleyen seçim hakkı testin verdiği sayı).
    static void OpenCards(int pending)
    {
        SetF(RM, "pendingCardSelections", pending);
        Call(RM, "EndRound");
        Must(State == GameStates.CardSelection, "card screen should open: " + State);
    }
    static void FinishChoice()
    {
        if (State == GameStates.RoundChoice && Boss.IsPending) { if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward(); }
    }
    // Kalan seçimleri bırakıp round'u kapatır ve sıradaki güvenli round'u başlatır.
    static void CloseCardsAndContinue()
    {
        SetF(RM, "pendingCardSelections", 1);
        RM.OnCardSelectionComplete();
        FinishChoice();
        Must(State == GameStates.RoundEnd, "round should close: " + State);
        StartRound(SafeRound(RM.CurrentRound + 1));
    }
    static void Pick(TileCardOffer offer) => Call(Cards, "OnCardSelected", offer);
    static TextMeshProUGUI Banner => F<TextMeshProUGUI>(Cards, "banner");
    static string BannerText => Banner != null && Banner.transform.parent.gameObject.activeSelf ? Banner.text : null;
    // Teklifler yeniden atılır; koşulu sağlayan ilk teklif döner (yoksa null).
    static TileCardOffer Roll(Func<TileCardOffer, bool> wanted, int tries = 200)
    {
        for (int i = 0; i < tries; i++)
        {
            Cards.RefreshCards();
            var hit = Offers.FirstOrDefault(wanted);
            if (hit != null) return hit;
        }
        return null;
    }
    static bool IsBase(TileCardOffer o, StatType stat) => o.IsBaseStat && o.Modifiers[0].statType == stat;
    static string SlotText(TileCardOffer offer)
    {
        foreach (var slot in F<List<CardUI>>(Cards, "cardSlots"))
            if (slot != null && ReferenceEquals(F<TileCardOffer>(slot, "currentOffer"), offer)) return F<TextMeshProUGUI>(slot, "buffText").text;
        throw new Exception("offer is not on a card slot");
    }
    static void FillTiles(int level)
    {
        var tile = RewardOfferLab.Tile("Damage");
        foreach (var ground in RewardOfferLab.OpenCells())
        {
            if (ground.CurrentModifier == null)
                ground.ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = r.minValue }).ToList());
            SetF(ground, "level", level);
        }
    }

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

    sealed class PanelProbe : MonoBehaviour
    {
        public int Enables, Disables;
        void OnEnable() => Enables++;
        void OnDisable() => Disables++;
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("veri: profil, denge seti, XP tablosu", DataChecks);
        Add("maliyet formülü", CostFormula);
        Add("sahne: Run50_XPV1", StartX);
        Add("XP: normal aralık", XpNormal);
        Add("XP: tablo sonrası", XpTail);
        Add("XP: tablo sonrası sonucu", XpTailResult);
        Add("kart: tile → yükseltme → temel güç", Transition);
        Add("kart: XP kartları toplanır", SummedCards);
        Add("kart: diğer temel güç kartları ve diğer XP kaynakları", OtherSources);
        Add("kart: işlevsiz seçenek", FloorFilter);
        Add("seçim akışı: arka arkaya ve sayaç", FlowCase);
        Add("round sonu: hesaplanıyor", StatusCase);
        Add("round sonu: hesaplanıyor görüntüsü", StatusShown);
        Add("round sonu: hesaplanıyor sonucu", StatusResult);
        Add("kart paneli görüntüsü", CardPicture);
        Add("sıra: kartlar → boss ödülü", BossOrderCase);
        Add("sıra: boss ödülü sonucu", BossOrderResult);
        Add("seçim hakkı: bedelli ödülden önce kazanılanlar", ChoiceDelta);
        Add("teknik sınır raporlanır", Limits);
        Add("ana menüye dönüş: liste temizlenince grup sıfırlanır", RestartClear);
        Add("sahne yeniden: Run50_XPV1", () => Load(X1));
        Add("yeni run temiz", CleanStart);
        Add("sahne: Run50_ZincirV1 (eski kural)", () => Load(Z1));
        Add("eski profil: sabit kuyruk", OldTail);
        Add("eski profil: sabit kuyruk sonucu", OldTailResult);
        Add("eski profil: kartlar", OldCards);
        Add("kapanış", FinalCheck);
    }

    // ================================================================ veri
    static double DataChecks()
    {
        var x = X1; var z = Z1;
        Must(x != null && z != null, "profiles missing");
        Require(x.runLength == 50 && x.choicesPerLevel == 3 && z.choicesPerLevel == 3 && x.fixedRoundDuration == z.fixedRoundDuration &&
                x.bossCalendar.Select(d => d.round).SequenceEqual(z.bossCalendar.Select(d => d.round)) && x.segmentTargets.SequenceEqual(z.segmentTargets) &&
                x.bossTargets.SequenceEqual(z.bossTargets) && x.bossPool == z.bossPool && x.bossRewards == z.bossRewards &&
                x.startingGold == z.startingGold && x.startingIron == z.startingIron && x.startingStone == z.startingStone,
            "Run50_XPV1 keeps Run50_ZincirV1's run: 50 rounds, 3 choices per level, boss calendar, quota and boss targets, boss pool, reward pool (chain included), round duration, starting economy");
        var xb = x.balance; var zb = z.balance;
        Require(xb != zb && xb.coreStats == zb.coreStats && xb.plantHealth == zb.plantHealth && xb.skillTree == zb.skillTree &&
                xb.rarityXpMultipliers.SequenceEqual(zb.rarityXpMultipliers) && xb.startingUnlocks.SequenceEqual(zb.startingUnlocks) &&
                xb.firstBehaviorOffer.SequenceEqual(zb.firstBehaviorOffer) && xb.planterPrices.Count == zb.planterPrices.Count &&
                xb.planterBaseStats.Select(e => (e.statType, e.value)).SequenceEqual(zb.planterBaseStats.Select(e => (e.statType, e.value))),
            "Its balance set is its own asset but shares core stats, plant health, skill tree, rarity XP, planter base stats, starting unlocks and the early behavior offer");
        Require(xb.additiveBaseXpCards && xb.hideFlooredBaseStats && !zb.additiveBaseXpCards && !zb.hideFlooredBaseStats,
            "Only the new balance set turns on the two base-card rules (summed XP cards, floored attack-interval card hidden)");
        var xt = xb.progression; var zt = zb.progression;
        Require(xt != zt && xt.useAuthoredRequirements && xt.xpRequirements.SequenceEqual(zt.xpRequirements) && xt.xpRequirements.Count == 140 &&
                xt.tailGrowth == .05f && zt.tailGrowth == 0f && xt.Validate() == null,
            "XP table: the 140 authored costs are identical to the old table; the new table adds only the tail growth (s = 0.05), the old one has none");
        int old = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RunProfileSO", new[] { "Assets/ScriptableObjects/RunProfiles" }))
        {
            var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (p == x) continue;
            // Bölüm 3.7.8: Run50_AlphaDengeV1, XP V1'den sonra gelen adaydır ve P7 kurallarını aynen taşır (eski profil değildir).
            if (p.name == "Run50_AlphaDengeV1")
            {
                Must(p.balance != null && p.balance.additiveBaseXpCards && p.balance.hideFlooredBaseStats && p.balance.progression != null &&
                     p.balance.progression.tailGrowth == xt.tailGrowth, "the later candidate should keep the P7 rules: " + p.name);
                continue;
            }
            old++;
            Must(p.balance == null || (!p.balance.additiveBaseXpCards && !p.balance.hideFlooredBaseStats && (p.balance.progression == null || p.balance.progression.tailGrowth == 0f)),
                "old profile changed: " + p.name);
        }
        Require(old >= 13, $"None of the {old} older profiles has the new rules: constant cost after the table, separate XP card multipliers, no card filter");
        return .05;
    }

    // ================================================================ maliyet formülü (sahne gerekmez)
    static double CostFormula()
    {
        var source = X1.balance.progression; int n = source.xpRequirements.Count; float last = source.xpRequirements[n - 1];
        int[] samples = { n + 1, n + 2, n + 10, 200, 500, 1000, 10000, 1000000, 1000000000, int.MaxValue };
        foreach (float s in new[] { .025f, .05f, .10f })
        {
            var t = Object.Instantiate(source); t.tailGrowth = s;
            Must(t.Validate() == null && t.HasGrowingTail && !t.CostIsConstantFrom(n) && !t.CostIsConstantFrom(1000), "tail flags " + s);
            Must(t.GetXPForLevel(n) == last, "continuity at N");
            Must(Near(t.GetXPForLevel(n + 1), last * (1.0 + s), 1e-6), "first tail cost");
            float previous = last;
            foreach (int level in samples)
            {
                float cost = t.GetXPForLevel(level); double reference = (double)last * (1.0 + (double)s * (level - n));
                Must(!float.IsNaN(cost) && !float.IsInfinity(cost) && cost > previous && Near(cost, reference, 1e-6), $"cost at {level}: {cost} vs {reference}");
                previous = cost;
            }
            for (int level = n; level < 3000; level++) Must(t.GetXPForLevel(level + 1) > t.GetXPForLevel(level), "not increasing at " + level);
            // Kapalı hesap (bekleyen level sayısı) tek tek saymayla aynı.
            foreach (var (level, xp) in new[] { (n, 1e6), (n, 1e9), (500, 5e8), (10000, 1e12), (n, 149999d) })
            {
                var reference = Reference(t, s, level, xp);
                double closed = t.LevelsAffordable(level, xp);
                Must(Math.Abs(Math.Floor(closed + 1e-6) - reference.levels) <= 1, $"closed form at {level} / {xp}: {closed} vs {reference.levels}");
            }
            Must(t.LevelsAffordable(n - 1, 1e9) == -1d, "inside the table the closed form is not used");
            Object.DestroyImmediate(t);
        }
        Require(true, $"Cost after the table (s = 0.025 / 0.05 / 0.10): continuous at level {n} ({last}), C(N+1) = C(N) × (1 + s), equals C(N) × [1 + s × (L − N)] at every sampled level up to int.MaxValue " +
                      "(finite, no NaN), strictly increasing for every level up to 3000; the closed pending-level count matches level-by-level counting");
        var bad = Object.Instantiate(source);
        bad.tailGrowth = float.NaN; bool nan = bad.Validate() != null;
        bad.tailGrowth = -1f; bool negative = bad.Validate() != null;
        bad.tailGrowth = float.PositiveInfinity; bool infinite = bad.Validate() != null;
        Object.DestroyImmediate(bad);
        Require(nan && negative && infinite, "A NaN, negative or infinite growth value is rejected by the table validation");
        var z = Z1.balance.progression;
        bool constant = z.CostIsConstantFrom(n) && Enumerable.Range(n, 2000).All(l => z.GetXPForLevel(l) == last) && z.GetXPForLevel(int.MaxValue) == last && z.LevelsAffordable(n, 3e6) == 3e6 / last;
        Require(constant, $"Old table (growth 0): the cost stays {last} after level {n}, as before");
        return .05;
    }

    // ================================================================ Run50_XPV1 sahnesi
    static ProgressionSO Table => Progress.Data;
    static float S => Table.tailGrowth;

    static float startDamage, startXp; static int startXpMore, startModifiers;
    static double StartX()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != X1) return Again;
        Player.enabled = false;
        MetaSave.UseMemoryOnly();
        NumericSafety.ResetCounters();
        Must(Table == X1.balance.progression && RM.BaseChoicesPerLevel == 3 && RM.ChoicesPerLevel == 3, "profile data not active");
        Must(Status != null && !Status.IsShown, "level work status should exist and be hidden");
        startDamage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player); startXp = XpStat; startXpMore = XpMoreCount; startModifiers = Stats.GlobalModifiers.Count;
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Subscribe();
        return .05;
    }

    static double XpNormal()
    {
        levelEvents.Clear();
        int level = Progress.CurrentLevel, gained = RM.LevelsGained, pending = RM.PendingCardSelections, granted = RM.CardChoicesGranted;
        double amount = CostSum(Table, S, level, 3) - Progress.StoredXP;
        Progress.AddXP(amount);
        Require(RM.LevelsGained - gained == 3 && Progress.CurrentLevel == level + 3 && Progress.StoredXP == 0d && !Progress.HasPendingLevels &&
                levelEvents.Count == 3 && levelEvents.All(e => e.frame == Time.frameCount) &&
                RM.PendingCardSelections - pending == 9 && RM.CardChoicesGranted - granted == 9,
            "Normal XP: an amount worth exactly 3 levels gives 3 levels in the same call, 0 XP left over, and 3 card choices per level (9 choices)");
        return .05;
    }

    static int tailLevel0, tailGained0, tailGranted0; static double tailEarned0, tailStored0; static (int levels, double rest, double spent) tailRef;
    const double TailXp = 6e9;
    static double XpTail()
    {
        levelEvents.Clear();
        SetF(RM, "pendingCardSelections", 0);
        tailLevel0 = Progress.CurrentLevel; tailGained0 = RM.LevelsGained; tailGranted0 = RM.CardChoicesGranted; tailEarned0 = Progress.TotalXPEarned; tailStored0 = Progress.StoredXP;
        tailRef = Reference(Table, S, tailLevel0, Progress.StoredXP + TailXp);
        Must(tailLevel0 + tailRef.levels > Table.TableLevels + 1000, $"reference should pass the table by far (level {tailLevel0 + tailRef.levels})");
        Progress.AddXP(TailXp);
        return .02;
    }
    static double XpTailResult()
    {
        if (Progress.HasPendingLevels) return Again;
        int levels = RM.LevelsGained - tailGained0, end = tailLevel0 + tailRef.levels;
        var frames = levelEvents.GroupBy(e => e.frame).Select(g => g.Count()).ToList();
        bool consecutive = levelEvents.Select((e, i) => e.level == tailLevel0 + 1 + i).All(b => b);
        Require(levels == tailRef.levels && Progress.CurrentLevel == end && Progress.StoredXP == tailRef.rest && consecutive && levelEvents.Count == tailRef.levels &&
                frames.Max() <= ProgressionManager.LevelsPerFrame && frames.Count > 1 && !Progress.LevelProcessingHalted &&
                RM.PendingCardSelections == tailRef.levels * 3 && RM.CardChoicesGranted - tailGranted0 == tailRef.levels * 3,
            $"Large XP past the table ({TailXp:0.#e0}): {levels} levels over {frames.Count} frames (at most {frames.Max()} per frame), level {tailLevel0} → {end}, {Progress.StoredXP} XP left, " +
            $"{RM.PendingCardSelections} choices — identical to the level-by-level reference; every level counted once, no halt");
        Require(Progress.TotalXPEarned - tailEarned0 == TailXp && Near(tailRef.spent + tailRef.rest, tailStored0 + TailXp, 1e-12) &&
                Progress.XPToNextLevel == RefCost(Table, S, end) && Progress.XPToNextLevel > Table.xpRequirements[Table.TableLevels - 1] && !Table.CostIsConstantFrom(end),
            $"No XP is lost (spent {tailRef.spent:0} + stored {tailRef.rest:0}); the next level now costs {Progress.XPToNextLevel:0} XP (table end: {Table.xpRequirements[Table.TableLevels - 1]:0}) — the cost keeps growing, there is no level cap in the tested range");
        Note("izole ilerleme testi: XP doğrudan verildi; endless oynanış testi değildir");
        SetF(RM, "pendingCardSelections", 0);
        return .05;
    }

    // ================================================================ tile → yükseltme → temel güç
    static double Transition()
    {
        var cells = RewardOfferLab.OpenCells();
        Must(cells.Count >= 4 && cells.All(c => c.CurrentModifier == null), $"fresh field expected ({cells.Count} open cells)");
        RewardOfferLab.Place(cells[0]);
        OpenCards(1000);
        Must(Progress.FirstBehaviorCardRound == 0, "no behavior card yet");
        Cards.RefreshCards();
        var first = Offers.ToList();
        Require(first.Count == 3 && first.All(o => !o.IsUpgrade && !o.IsBaseStat && o.Tile != null && o.Tile.IsAvailableInCardPool) && BannerText == null &&
                Cards.GuaranteedBehaviorOffer != null && RunBalanceSO.IsFirstBehaviorType(Cards.GuaranteedBehaviorOffer.Tile.modifierType),
            "Open empty cells: the three offers are new tile cards from the unlocked pool, no banner; one slot is the early behavior offer (explosion / electric) while no behavior card has been taken");
        bool locked = false;
        for (int i = 0; i < 40 && !locked; i++) { Cards.RefreshCards(); locked = Offers.Any(o => o.Tile != null && !o.Tile.IsAvailableInCardPool); }
        Require(!locked, "Locked tile types (tornado, boomerang, duplicate before their unlock) never appear in 120 rolled offers");
        FillTiles(0);
        Cards.RefreshCards();
        var upgrades = Offers.ToList();
        Require(upgrades.All(o => o.IsUpgrade && o.UpgradeTarget.CanUpgrade && o.LevelGain >= 1) && upgrades[0].UpgradeTarget.Planter != null && BannerText != null && BannerText.StartsWith("GRİD DOLU"),
            $"Grid full: the offers are upgrades of existing tiles (the tile under a planter first), banner \"{BannerText}\"");
        int taken = RM.CardsTaken; var target = upgrades[0].UpgradeTarget; int before = target.Level;
        Pick(upgrades[0]);
        Require(target.Level == before + upgrades[0].LevelGain && RM.CardsTaken == taken + 1 && State == GameStates.CardSelection, "Taking an upgrade card raises that tile's level and the next choice is offered in the same screen");
        FillTiles(GroundCell.MaxLevel);
        Cards.RefreshCards();
        var bases = Offers.ToList();
        Require(bases.All(o => o.IsBaseStat && o.Modifiers.Count >= 1 && o.Modifiers.All(m => m.operation == ModifierOperation.MorePercent)) && BannerText != null && BannerText.StartsWith("TÜM TILE'LAR MAX"),
            $"Every tile at max level: the offers are permanent base-power cards, banner \"{BannerText}\"");
        return .02;
    }

    // ================================================================ XP kartları toplanır
    static float xpBefore, v1, v2;
    static double SummedCards()
    {
        Must(State == GameStates.CardSelection && GroupTotal == 0f, "card screen with an empty group expected");
        xpBefore = XpStat; int count0 = XpMoreCount;
        var a = Roll(o => IsBase(o, StatType.XPGainMultiplier));
        Must(a != null, "no XP base card in 200 rolls");
        Require(a.IsSummed && a.Modifiers.Count == 1 && new[] { .02f, .03f, .04f, .05f }.Contains(a.Modifiers[0].value) && a.Modifiers[0].target == StatTarget.Planter,
            $"A rolled \"XP kazancı\" base card belongs to the summed group and keeps the old values (+2 / +3 / +4 / +5 %; this one +{Pct(a.Modifiers[0].value)} %)");
        string text = SlotText(a);
        Require(text == CardUI.SummedText(a) && text.Contains("Toplanır:") && text.Contains("+0%") && text.Contains("+" + Pct(a.Modifiers[0].value) + "%"),
            $"The card says what will happen: \"{text.Replace("\n", " / ")}\"");
        v1 = a.Modifiers[0].value;
        Pick(a);
        var b = Roll(o => IsBase(o, StatType.XPGainMultiplier));
        Must(b != null, "no second XP base card");
        v2 = b.Modifiers[0].value;
        string second = SlotText(b);
        Require(second.Contains("+" + Pct(v1) + "%") && second.Contains("+" + Pct(v1 + v2) + "%"),
            $"The second card shows the group total before and after: \"{second.Replace("\n", " / ")}\"");
        Pick(b);
        float ratio = XpStat / xpBefore;
        Require(Near(GroupTotal, v1 + v2, 1e-6) && XpMoreCount == count0 + 1 && Near(ratio, 1f + v1 + v2, 1e-5) && !Near(ratio, (1f + v1) * (1f + v2), 1e-5),
            $"Two XP cards (+{Pct(v1)} % and +{Pct(v2)} %) give ×{ratio:0.####} from this card group — the sum (×{1f + v1 + v2:0.####}), not the product (×{(1f + v1) * (1f + v2):0.######}); the group is one modifier in the list");
        return .02;
    }

    static double OtherSources()
    {
        // Başka bir XP kaynağı (Bilgi Filizi gibi ayrı çarpan): grupla çarpılır, gruba karışmaz.
        float before = XpStat, total = GroupTotal;
        var other = new StatModifier { statType = StatType.XPGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = .25f };
        Stats.AddGlobalModifier(other);
        bool multiplied = Near(XpStat / before, 1.25, 1e-5) && GroupTotal == total;
        var c = Roll(o => IsBase(o, StatType.XPGainMultiplier));
        Must(c != null, "no third XP base card");
        float v3 = c.Modifiers[0].value, withOther = XpStat;
        Pick(c);
        bool grouped = Near(XpStat / withOther, (1f + total + v3) / (1f + total), 1e-5) && Near(GroupTotal, total + v3, 1e-6);
        Stats.RemoveGlobalModifier(other);
        Require(multiplied && grouped && Near(GroupTotal, total + v3, 1e-6) && Near(XpStat / xpBefore, 1f + v1 + v2 + v3, 1e-5),
            $"A separate XP source (×1.25, like a boss reward) multiplies with the group and is not summed into it; removing it leaves the group intact (group total +{Pct(GroupTotal)} %)");
        // Saksının XP stat'ı (tile ve global birlikte): Water tile'ı yerel toplamda kalır, grup ayrı çarpandır.
        var planter = Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).First(p => p.OccupiedGrids.Count > 0);
        float planterBefore = planter.GetHarvestXP();
        var d = Roll(o => IsBase(o, StatType.XPGainMultiplier));
        Must(d != null, "no fourth XP base card");
        float t = GroupTotal, v4 = d.Modifiers[0].value;
        Pick(d);
        Require(Near(planter.GetHarvestXP() / planterBefore, (1f + t + v4) / (1f + t), 1e-5),
            $"The planter's harvest XP grows by the same group step (×{(1f + t + v4) / (1f + t):0.####}) — the card reaches the real harvest XP path");
        // Diğer temel güç kartları eskisi gibi ayrı çarpan.
        float damage0 = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        var d1 = Roll(o => IsBase(o, StatType.HarvestDamage)); Must(d1 != null, "no damage base card");
        bool plain = !d1.IsSummed && SlotText(d1) == TileBuffText.Modifiers(d1.Modifiers);
        float a = d1.Modifiers[0].value; Pick(d1);
        var d2 = Roll(o => IsBase(o, StatType.HarvestDamage)); Must(d2 != null, "no second damage base card");
        float b = d2.Modifiers[0].value; Pick(d2);
        float damageRatio = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) / damage0;
        Require(plain && Near(damageRatio, (1f + a) * (1f + b), 1e-5) && !Near(damageRatio, 1f + a + b, 1e-5),
            $"Other base cards are unchanged: two damage cards (+{Pct(a)} %, +{Pct(b)} %) still multiply (×{damageRatio:0.######}) and their text is the old one");
        return .02;
    }

    static double FloorFilter()
    {
        var floor = new StatModifier { statType = StatType.AttackSpeed, target = StatTarget.Player, operation = ModifierOperation.Set, value = PlayerController.MinAttackInterval };
        Stats.AddGlobalModifier(floor);
        int offered = 0, speed = 0;
        for (int i = 0; i < 60; i++) { Cards.RefreshCards(); offered += Offers.Count; speed += Offers.Count(o => IsBase(o, StatType.AttackSpeed)); }
        Stats.RemoveGlobalModifier(floor);
        var back = Roll(o => IsBase(o, StatType.AttackSpeed));
        Require(speed == 0 && offered == 180 && back != null,
            "Attack interval at the game's floor (0.1 s): the \"Atak aralığı\" base card (which would change nothing) is not offered in 180 offers; above the floor it is offered again");
        return .02;
    }

    // ================================================================ seçim akışı
    static double FlowCase()
    {
        CloseCardsAndContinue();
        var probe = Cards.gameObject.GetComponent<PanelProbe>();
        if (probe == null) probe = Cards.gameObject.AddComponent<PanelProbe>();
        OpenCards(5);
        int enables = probe.Enables, disables = probe.Disables;
        var texts = new List<string>(); var shown = new List<int>(); bool stayed = true;
        for (int i = 0; i < 5; i++)
        {
            texts.Add(Cards.RemainingText); shown.Add(Cards.ShownRemaining);
            stayed &= State == GameStates.CardSelection && Cards.gameObject.activeInHierarchy && Cards.ShownRemaining == RM.PendingCardSelections;
            Pick(Offers[0]);
        }
        Require(stayed && shown.SequenceEqual(new[] { 5, 4, 3, 2, 1 }) && texts[0] == "KALAN SEÇİM: 5" && texts[3] == "KALAN SEÇİM: 2" && texts[4] == "SON SEÇİM" &&
                probe.Enables == enables && probe.Disables <= disables + 1,
            $"Five pending choices are offered one after another in the same panel (never closed and reopened in between); the panel shows the remaining choices: {string.Join(" → ", texts)}");
        Require(State != GameStates.CardSelection && RM.PendingCardSelections == 0, $"Only after the last choice does the round move on ({State})");
        FinishChoice();
        Must(State == GameStates.RoundEnd, "round should close: " + State);
        StartRound(SafeRound(RM.CurrentRound + 1));
        return .05;
    }

    // ================================================================ round sonu: hesaplanıyor
    const int StatusLevels = 40000;
    // Tam "count" level'a yeten XP: sonraki level'ın yarısına kadar (yuvarlama bir level eksik ya da fazla vermesin).
    static double XpForLevels(int count) => CostSum(Table, S, Progress.CurrentLevel, count) + .5 * RefCost(Table, S, Progress.CurrentLevel + count) - Progress.StoredXP;
    static int statusLevel0;
    static double StatusCase()
    {
        SetF(RM, "pendingCardSelections", 0);
        statusLevel0 = Progress.CurrentLevel;
        Progress.AddXP(XpForLevels(StatusLevels));
        Call(RM, "EndRound");
        Must(State == GameStates.Round && RM.IsAwaitingLevels && !RM.IsRoundActive && Progress.HasPendingLevels, $"round end should wait for level work ({State})");
        return .02;
    }
    static bool statusSeen; static string statusText;
    static double StatusShown()
    {
        if (State == GameStates.Round && !Status.IsShown) return Again;
        Must(State == GameStates.Round && Status.IsShown, "status should be visible while the level work runs: " + State);
        statusSeen = true; statusText = Status.Text;
        Capture("T377_01_SeviyeHesaplaniyor");
        return .02;
    }
    static double StatusResult()
    {
        if (State == GameStates.Round) return Again;
        Require(statusSeen && statusText.StartsWith(LevelWorkStatusUI.Title) && statusText.Contains("Level ") && !Status.IsShown,
            $"While the round end waits for pending level work the player sees \"{statusText.Replace("\n", " / ").Replace("<size=70%>", "").Replace("</size>", "")}\"; it disappears when the work is done");
        Require(State == GameStates.CardSelection && Progress.CurrentLevel == statusLevel0 + StatusLevels && RM.PendingCardSelections == StatusLevels * 3 && !Progress.HasPendingLevels &&
                Cards.RemainingText == $"KALAN SEÇİM: {StatusLevels * 3}",
            $"Then the card screen opens with all {RM.PendingCardSelections} choices of the {StatusLevels} levels — none decided early, none lost");
        var xp = Roll(o => IsBase(o, StatType.XPGainMultiplier));
        Must(xp != null, "no XP card for the picture");
        return 1.5;   // kart dağıtma animasyonu bitsin
    }
    static double CardPicture()
    {
        Capture("T377_02_KartPaneli_KalanSecim_ToplananXP");
        CloseCardsAndContinue();
        return .05;
    }

    // ================================================================ sıra: kartlar bitmeden boss ödülü / sonraki round yok
    static int bossRound;
    static double BossOrderCase()
    {
        bossRound = RM.CurrentRound; while (!RM.Profile.Calendar.IsBossRound(bossRound)) bossRound++;
        // Önceki round'u kapatmadan boss round'una geçilir (test düzeneği): mevcut round sessizce kapanır.
        SetF(RM, "pendingCardSelections", 1); Call(RM, "EndRound");
        Must(State == GameStates.CardSelection, "card screen expected: " + State);
        CloseCardsToRoundEnd();
        StartRound(bossRound);
        var score = HarvestScoreManager.Instance;
        SetF(score, "totalScore", score.TotalScore + 1000000L);
        OpenCards(2);
        bool pendingReward = Boss.IsPending && RM.IsRoundChoicePending;
        int round = RM.CurrentRound;
        RM.StartNextRound(); RM.ContinueAfterRoundChoice();
        bool blocked = State == GameStates.CardSelection && RM.CurrentRound == round;
        Pick(Offers[0]);
        bool still = State == GameStates.CardSelection && Cards.RemainingText == "SON SEÇİM";
        Pick(Offers[0]);
        Require(pendingReward && blocked && still && State == GameStates.RoundChoice,
            $"Boss round R{bossRound} with 2 pending card choices and a boss reward: the reward screen does not open and the next round cannot start until the last card is chosen; then the reward screen opens");
        return .1;
    }
    static void CloseCardsToRoundEnd()
    {
        SetF(RM, "pendingCardSelections", 1);
        RM.OnCardSelectionComplete();
        FinishChoice();
        Must(State == GameStates.RoundEnd, "round should close: " + State);
    }
    static double BossOrderResult()
    {
        if (State == GameStates.RoundChoice && Boss.IsPending && Boss.Offer.Count == 0 && !F<bool>(Boss, "offerPrepared")) return Again;
        FinishChoice();
        Require(State == GameStates.RoundEnd && !Boss.IsPending, "After the boss reward the round closes normally");
        StartRound(SafeRound(RM.CurrentRound + 1));
        return .05;
    }

    // ================================================================ seçim hakkı: bedelli ödülden önce kazanılanlar değişmez
    static double ChoiceDelta()
    {
        SetF(RM, "pendingCardSelections", 0);
        Progress.AddXP(XpForLevels(2));
        int earned = RM.PendingCardSelections;
        var reward = RewardOfferLab.All(X1.bossRewards).First(r => r.id == "bereketli_ogrenim");
        bool granted = Boss.EditorGrant(reward, out string refusal);
        Must(granted, "reward not granted: " + refusal);
        int after = RM.PendingCardSelections, perLevel = RM.ChoicesPerLevel;
        Progress.AddXP(XpForLevels(1));
        Require(earned == 6 && after == 6 && perLevel == 4 && RM.PendingCardSelections == 10 && RM.BaseChoicesPerLevel == 3,
            "Two levels earned at 3 choices (6 pending); a +1 choice reward taken afterwards does not change them (still 6); the next level gives 4 (10 pending) — base choices stay 3");
        SetF(RM, "pendingCardSelections", 0);
        return .05;
    }

    // ================================================================ teknik sınır: sayaca sığmayan iş hâlâ durur ve raporlanır
    static double Limits()
    {
        int level = Progress.CurrentLevel, gained = RM.LevelsGained; double stored = Progress.StoredXP;
        const double Huge = 1e24;
        double estimate = Table.LevelsAffordable(level, stored + Huge);
        Must(estimate > int.MaxValue, "the test amount should not fit the level counter: " + estimate);
        Expect("Level işleme durdu");
        Progress.AddXP(Huge);
        Require(Progress.LevelProcessingHalted && Progress.HaltReason.Contains("int sınırını") && RM.LevelsGained == gained && Progress.StoredXP == stored + Huge && !Progress.HasPendingLevels,
            $"Technical limit still reported with the growing cost: {Huge:0.#e0} XP would be {estimate:0.##e0} levels, more than the level counter holds — level work stops at once, the XP is kept, the reason is logged once");
        Call(RM, "EndRound");
        Require(State != GameStates.Round, $"…and the round end does not wait for the halted work ({State})");
        Note("teknik sınır: level sayacı int; sayaca sığan çok büyük iş kare başına 256 level ile işlenir (durdurulmaz)");
        return .05;
    }

    // ================================================================ run sonu ekranının "ana menü" yolu (RunComplateUI.OnMainMenuPressed): global liste temizlenir
    static double RestartClear()
    {
        Must(GroupTotal > 0f && XpMoreCount >= 1, "the group should hold cards before the clear");
        float held = GroupTotal;
        Stats.ClearGlobalModifiers();
        bool empty = GroupTotal == 0f && XpMoreCount == 0;
        Stats.AddToSummedGroup(new StatModifier { statType = StatType.XPGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = .05f });
        Require(empty && Near(GroupTotal, .05f, 1e-6) && XpMoreCount == 1,
            $"Clearing the global modifier list (the run-end main-menu path) also empties the XP card group (it held +{Pct(held)} %): the next card starts from 0, not from the old total");
        return .05;
    }

    // ================================================================ yeni run / eski profil
    static double Load(RunProfileSO profile)
    {
        Unsubscribe();
        Selection.active = profile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != X1 || Progress == null || Progress.CurrentLevel != 1) return Again;
        Require(Progress.CurrentLevel == 1 && Progress.StoredXP == 0d && !Progress.HasPendingLevels && !Progress.LevelProcessingHalted && RM.PendingCardSelections == 0 && !RM.IsAwaitingLevels &&
                GroupTotal == 0f && XpMoreCount == startXpMore && RM.ChoicesPerLevel == 3 && RM.LevelsGained == 0 && RM.CardsTaken == 0 && !Status.IsShown &&
                Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == startDamage && XpStat == startXp && Stats.GlobalModifiers.Count == startModifiers,
            "New scene / new run: level 1, no stored XP, no pending level work or choices, no halt, the XP card group is empty, every base-power card is gone (stats and modifier list equal the first run's start), 3 choices per level again");
        return .05;
    }
    static int oldLevel0; static (int levels, double rest, double spent) oldRef;
    static double OldTail()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1 || Progress == null || Progress.CurrentLevel != 1) return Again;
        Player.enabled = false;
        Must(Table == Z1.balance.progression && Table.tailGrowth == 0f && !RunBalanceSO.AdditiveBaseXpCards && !RunBalanceSO.HideFlooredBaseStats, "old profile data not active");
        RM.StartNextRound();
        Must(State == GameStates.Round, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Subscribe();
        levelEvents.Clear();
        oldLevel0 = Progress.CurrentLevel;
        double amount = CostSum(Table, 0f, 1, Table.TableLevels - 1) + 60.5 * Table.xpRequirements[Table.TableLevels - 1];
        oldRef = Reference(Table, 0f, oldLevel0, amount);
        Progress.AddXP(amount);
        return .02;
    }
    static double OldTailResult()
    {
        if (Progress.HasPendingLevels) return Again;
        float last = Table.xpRequirements[Table.TableLevels - 1];
        Require(Progress.CurrentLevel == oldLevel0 + oldRef.levels && Progress.CurrentLevel == Table.TableLevels + 60 && Progress.StoredXP == oldRef.rest && Progress.XPToNextLevel == last &&
                Table.CostIsConstantFrom(Progress.CurrentLevel) && RM.PendingCardSelections == oldRef.levels * 3,
            $"Old profile (Run50_ZincirV1): after the table every level still costs {last:0} XP (level {Progress.CurrentLevel}, {Progress.StoredXP:0} XP left = reference), 3 choices per level");
        return .05;
    }

    static double OldCards()
    {
        var cells = RewardOfferLab.OpenCells();
        RewardOfferLab.Place(cells[0]);
        FillTiles(GroundCell.MaxLevel);
        OpenCards(1000);
        float before = XpStat; int count0 = XpMoreCount;
        var a = Roll(o => IsBase(o, StatType.XPGainMultiplier)); Must(a != null, "no XP base card (old)");
        bool plain = !a.IsSummed && SlotText(a) == TileBuffText.Modifiers(a.Modifiers);
        float a1 = a.Modifiers[0].value; Pick(a);
        var b = Roll(o => IsBase(o, StatType.XPGainMultiplier)); Must(b != null, "no second XP base card (old)");
        float b1 = b.Modifiers[0].value; Pick(b);
        float ratio = XpStat / before;
        Require(plain && GroupTotal == 0f && XpMoreCount == count0 + 2 && Near(ratio, (1f + a1) * (1f + b1), 1e-5) && !Near(ratio, 1f + a1 + b1, 1e-5),
            $"Old profile: two XP base cards (+{Pct(a1)} %, +{Pct(b1)} %) still multiply (×{ratio:0.######}), as two separate modifiers, with the old card text");
        var floor = new StatModifier { statType = StatType.AttackSpeed, target = StatTarget.Player, operation = ModifierOperation.Set, value = PlayerController.MinAttackInterval };
        Stats.AddGlobalModifier(floor);
        var speed = Roll(o => IsBase(o, StatType.AttackSpeed));
        Stats.RemoveGlobalModifier(floor);
        Require(speed != null && Cards.RemainingText != null, "Old profile: the attack-interval card is still offered at the floor (old behavior); the remaining-choice counter is shown in every profile");
        CloseCardsAndContinue();
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0 && expected.Count == 0,
            $"Every deliberately produced error was reported where expected (halted level work) and nothing else was logged as an error ({errors} unexpected)");
        return .05;
    }
}
