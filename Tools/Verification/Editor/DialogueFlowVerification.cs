using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Diyalog Faz 2 — gerçek oyun akışı (26 Ekim 2026). Durum taklidi yok: menü → başlangıç seçimi → oyun sahnesi → saksı dükkânı →
// yerleştirme → round'lar → kart seçimi → Sınama round'u → Adak dönemi → run sonu, oyunun kendi yollarıyla. Yalnız izole kopyada;
// kayıt bellekte (batch). Üç run:
//   1) ilk run: T1–T8 sırayla; ikinci dönemde Adak tutmaz (erken ölüm) → T11 Adak kaybı. Kapris görülmeden ölünür.
//   2) yeniden başlat: öğretimler tekrar etmez; Sınama'da kayıp → T11 Sınama kaybı.
//   3) yeniden başlat: geç round'da ilk kapris → T10 (önceki run'larda görülmemişti, şimdi çıkar); son round → T11 zafer.
// Büyük kutu (26 Ekim kararı) açıkken arkadaki paneli kapatır: her ekranda arkadaki bütün düğmelere giden tık kutuya düşer;
// konuşma bitince arkadaki düğme yeniden tıklanır. Bu, olay sisteminin gerçek ışın testiyle ölçülür. Ekran görüntüleri Logs/Faz2_*.png.
[InitializeOnLoad]
public static class DialogueFlowVerification
{
    const string Key = "DialogueFlowVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset";
    const double Again = -1;
    static readonly List<string> notes = new();
    static List<Func<double>> plan;
    static int step, errors, directorObjects, bossRound, secondPeriodEnd;
    static double nextAt, stepSince;
    static bool listening;

    static DialogueFlowVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    // ---------------------------------------------------------------- kısa yollar
    static GameStates State => GameManager.Instance.CurrentState;
    static RoundManager RM => RoundManager.Instance;
    static DialogueDirector D => DialogueDirector.Instance;
    static DialogueBox Big => D.BigBox;
    static DialogueBox Small => D.SmallBox;
    static string Current => D.Current != null ? D.Current.id : "-";
    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }
    static void Note(string text) => notes.Add("NOTE " + text);
    static object Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, args);
    static T F<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);
    static void SetF(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    static void SetP(object o, string name, object value) => o.GetType().GetProperty(name).GetSetMethod(true).Invoke(o, new[] { value });
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", HarvestScoreManager.Instance.TotalScore + amount);
    static UIManager UI => Object.FindAnyObjectByType<UIManager>();
    // Gezegendeki Eri'ye giden tepkinin gösterilip gösterilmediği (balon onu hemen alıp boşaltabildiği için yöneticiden okunur).
    static bool Reacted(string id) => F<Dictionary<string, float>>(D, "lastShown").ContainsKey(id);

    static void CountLogs(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++;
        Note($"logged {type}: {message.Split('\n')[0]}");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (nextAt == 0) { nextAt = now + 2; stepSince = now; Plan(); return; }
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (now - stepSince > 120) throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, scene {SceneManager.GetActiveScene().name}, box {Current}");
            if (step >= plan.Count) { Finish(null); return; }
            double wait = plan[step]();
            if (wait == Again) { nextAt = now + .05; EditorApplication.QueuePlayerLoopUpdate(); return; }
            step++; stepSince = now; nextAt = now + wait;
            File.WriteAllLines("Logs/DialogueFlowVerification.txt", new[] { "RUNNING step " + step }.Concat(notes));
        }
        catch (Exception ex) { Finish(ex.InnerException ?? ex); return; }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    static void Plan()
    {
        plan = new List<Func<double>>
        {
            Boot, Selection, StartRun, Preparation, Placement, PlaceAndReturn, FirstRound, FirstRoundLessons, LevelUp, Cards, CardsShot, Settle,
            ToBossRound, BossRound, BossLessons, PassPeriod, Settle, PeriodClosed, PeriodClosedShot, ToSecondPeriodEnd, LoseQuota, QuotaShot,
            Restart, SecondRun, LoseBoss, BossShot, Restart, ThirdRun, WaitKapris, Kapris, ToFinalRound, Settle, Victory, VictoryShot,
        };
    }

    // ---------------------------------------------------------------- 1. run
    static double Boot()
    {
        if (SceneManager.GetActiveScene().name != "MenuScene" || GameManager.Instance == null || State != GameStates.MainMenu) return Again;
        Application.logMessageReceived += CountLogs; listening = true;
        _ = MetaSave.Data;
        Check(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "batch keeps the save in memory (the real save is never touched)");
        Check(!DialogueProgress.Seen("intro.select") && DialogueProgress.Stage == 0, "fresh progress: nothing seen, stage 0");
        DialogueDirector.BatchEnabled = true;
        DialogueDirector.Ensure();
        directorObjects = D.GetComponentsInChildren<Transform>(true).Length;
        Check(!Big.IsOpen && !Small.IsOpen, "main menu: no box before Play");
        return .2;
    }

    static double Selection()
    {
        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        Call(menu, "OnPlayClicked");
        Check(Big.IsOpen && Current == "intro.select", "T1: Play opens the start selection and Eri introduces herself (big box)");
        Complete(Big);
        Capture("Faz2_01_Secim", true);
        Big.Advance(); Big.Advance(); Big.Advance();
        Check(Big.IsOpen && Big.LineIndex == 2, "box click / Space walks through the three intro lines");
        Big.Skip();
        var panel = Object.FindFirstObjectByType<StartSelectionPanelUI>();
        Reachable(panel.GetComponentsInChildren<Button>().First(b => b.name == "Start"), "RUN'I BAŞLAT");
        return .1;
    }

    static double StartRun()
    {
        var panel = Object.FindFirstObjectByType<StartSelectionPanelUI>();
        Call(panel, "RequestStart");
        Check(State == GameStates.RunSetup && SceneManager.GetActiveScene().name == "MenuScene" && !Big.IsOpen && !DialogueProgress.Seen("tut.setup"),
            "Start: the menu switches to RunSetup while still in the menu scene; the planter lesson does NOT fire there");
        return 3;
    }

    static double Preparation()
    {
        if (SceneManager.GetActiveScene().name != "GameScene" || RM == null || State != GameStates.RunSetup || UI == null) return Again;
        Check(Big.IsOpen && Current == "tut.setup", "T2: in the game scene, once the run is built, the planter lesson opens");
        Check(MetaSave.Data.runsStarted == 1, "run counter: 1 started");
        bossRound = RM.Calendar.NextBossRound(1);
        secondPeriodEnd = RM.Calendar.PeriodEnd(2);
        Note($"calendar: first period ends R{RM.Calendar.PeriodEnd(1)}, first Sınama R{bossRound}, second period ends R{secondPeriodEnd}; " +
             $"quota 1 = {RM.QuotaTargetFor(1)}, boss target 1 = {RM.BossTargetFor(1)}");
        Check(bossRound == RM.Calendar.PeriodEnd(1) && bossRound > 1, "profile: the first Adak period ends with the first Sınama");
        Complete(Big);
        Capture("Faz2_02_Hazirlik", true);
        return .1;
    }

    static PlanterSO planter;
    static double Placement()
    {
        Complete(Big); Big.Advance();
        Check(!Big.IsOpen && DialogueProgress.Seen("tut.setup"), "one click after the text: the lesson is done");
        Reachable(F<Button>(Object.FindAnyObjectByType<PlacementShopButtonUI>(), "button"), "PLANTER SHOP");
        UI.OpenPlacementShop();
        Check(State == GameStates.Shop, "the planter shop opens");
        planter = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        Check(ResourceManager.Instance.SpendResource(planter.PriceType, planter.Price), "buy a 1x1 planter with the starting gold");
        PlacementManager.Instance.StartPlacement(planter);
        Check(State == GameStates.Placing && Big.IsOpen && Current == "tut.place", "T3: placement mode explains click-to-place and R to rotate");
        Complete(Big);
        return .1;
    }

    static double PlaceAndReturn()
    {
        // Bir sonraki karede (oyuncunun ikinci tıkı en erken burada gelir): tarlaya tık perdeye düşer, "arayüz üstünde" sayılır.
        Vector2 middle = new(Screen.width * .5f, Screen.height * .5f);
        GameObject onField = TopUi(middle);
        Check(onField != null && onField.GetComponentInParent<DialogueDirector>() != null,
            $"while Eri speaks, a click on the field lands on the curtain (counts as 'over UI': no planter is placed by accident; hit {(onField != null ? onField.name : "nothing")})");
        Capture("Faz2_03_Yerlestirme", true);
        Big.Skip();
        GameObject top = TopUi(middle);
        Check(top == null || top.GetComponentInParent<DialogueDirector>() == null, "after the lesson the field takes clicks again (no dialogue UI over it)");
        var cells = RewardOfferLab.OpenCells();
        GroundCell target = cells.OrderBy(c => Mathf.Abs(c.GetGridPosition().x - 5) + Mathf.Abs(c.GetGridPosition().z - 5)).First();
        var placement = PlacementManager.Instance;
        var ghost = F<GameObject>(placement, "ghostObject");
        ghost.transform.position = target.transform.position + (Vector3)Call(placement, "GetGhostCenterOffset");
        Call(placement, "PlacePlanter", GridManager.Instance.GetGridSystem().GetGridObject(target.GetGridPosition()));
        Check(State == GameStates.Shop && !Big.IsOpen, "placing the planter on the field ends placement");
        GameManager.Instance.ShowRoundEnd();   // GERİ
        Check(State == GameStates.RoundEnd && !Big.IsOpen && !Small.IsOpen, "back on the preparation screen: nothing new to say");
        return .1;
    }

    static double FirstRound()
    {
        UI.NextRound();
        Check(State == GameStates.Round && RM.CurrentRound == 1 && Small.IsOpen && !Big.IsOpen && Current == "tut.hit",
            "T4: round 1 starts with the hit lesson in the small box");
        Check(Small.GetComponent<GraphicRaycaster>() == null, "small box takes no UI raycasts (kapris hits are never blocked)");
        RoundManager.EditorFreezeTimer = true;
        Complete(Small);
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        Check(Small.IsOpen && Current == "tut.hit" && Small.LineIndex == 1, "T4 second line (the joke / first hint) follows by itself");
        Complete(Small);
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        return .1;
    }

    static double FirstRoundLessons()
    {
        Check(Small.IsOpen && Current == "tut.adak", "T5: after the hit lesson closes by itself, Eri names the Adak (small box)");
        Complete(Small);
        Capture("Faz2_04_Round_Adak");
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        Check(!Small.IsOpen && D.Current == null, "round 1 has nothing more to say");
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        return .3;
    }

    static double LevelUp()
    {
        if (ProgressionManager.Instance.HasPendingLevels) return Again;
        RoundManager.EditorFreezeTimer = false;
        Check(RM.EditorEndRound(true), "round 1 ends through the normal round-end path");
        return .3;
    }

    static double Cards()
    {
        if (State != GameStates.CardSelection) return Again;
        Check(Big.IsOpen && Current == "tut.level", "T6: the first level-up opens the card lesson (big box)");
        Complete(Big);
        return 1.2;   // kartlar dağıtılma animasyonundan sonra yerine otursun (ölçüm oturmuş düzende)
    }

    static double CardsShot()
    {
        Capture("Faz2_05_KartSecimi", true);
        Big.Skip();
        var card = Object.FindAnyObjectByType<CardSelectionUI>().GetComponentsInChildren<CardUI>().First(c => c.isActiveAndEnabled);
        Reachable(F<Button>(card, "button"), "the first card");
        return .1;
    }

    // Kart seçimi ve round sonu seçimleri oyunun kendi yollarıyla geçilir: ilk kart, ilk uygun boss ödülü, ilk uzmanlaşma.
    static double SettleNow()
    {
        for (int guard = 0; guard < 64; guard++)
        {
            if (State == GameStates.CardSelection)
            {
                var cardUI = Object.FindAnyObjectByType<CardSelectionUI>();
                var offers = F<System.Collections.IList>(cardUI, "currentCards");
                if (offers.Count == 0) return Again;
                Call(cardUI, "OnCardSelected", offers[0]);
                continue;
            }
            if (State == GameStates.RoundChoice)
            {
                var spec = SpecializationManager.Instance;
                if (spec != null && spec.IsPending) { spec.Choose(spec.Options[0]); continue; }
                var boss = BossRewardManager.Instance;
                if (boss != null && boss.IsPending)
                {
                    bool chosen = false;
                    foreach (BossRewardSO reward in boss.Offer.ToList())
                        if (!boss.NeedsPlacement(reward) && boss.Choose(reward)) { chosen = true; break; }
                    if (!chosen) boss.ContinueWithoutReward();
                    continue;
                }
                return Again;
            }
            return State == GameStates.RoundEnd || State == GameStates.RunComplete ? .1 : Again;
        }
        return Again;
    }

    static double Settle() => SettleNow();

    static double ToBossRound()
    {
        if (State == GameStates.Round)
        {
            Check(!Small.IsOpen && !Big.IsOpen, $"round {RM.CurrentRound}: lessons already seen stay quiet");
            RM.EditorEndRound(true);
            return Again;
        }
        if (State != GameStates.RoundEnd) { SettleNow(); return Again; }
        Check(!Big.IsOpen, $"round {RM.CurrentRound} end: no period closed yet, no box");
        if (RM.CurrentRound + 1 == bossRound) return .1;
        UI.NextRound();
        return Again;
    }

    static double BossRound()
    {
        UI.NextRound();
        Check(State == GameStates.Round && RM.CurrentRound == bossRound && RM.IsBossRound(bossRound), $"round {bossRound} is the first Sınama round");
        Check(Small.IsOpen && Current == "tut.boss" && D.Queued != null && D.Queued.id == "tut.lastround",
            "T9: the Sınama lesson opens first; the 'Adak still short' warning (T7) waits in the queue");
        RoundManager.EditorFreezeTimer = true;
        Complete(Small);
        return .3;
    }

    static double BossLessons()
    {
        Capture("Faz2_06_Sinama");
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        Check(Small.IsOpen && Current == "tut.lastround" && DialogueDirector.QuotaBehind(RM), "T7: then the last-round warning, because the Adak is still short");
        Complete(Small);
        Capture("Faz2_07_SonRound");
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        Check(!Small.IsOpen, "both round lessons closed by themselves");
        return .1;
    }

    static double PassPeriod()
    {
        long need = Math.Max(RM.QuotaTarget - RM.QuotaProgress, RM.BossTarget - RM.BossProgress) + 1;
        AddScore(need);
        RoundManager.EditorFreezeTimer = false;
        Check(RM.EditorEndRound(true) && !RM.RunFailed, "Adak and Sınama targets met: the run goes on");
        return .3;
    }

    static double PeriodClosed()
    {
        if (State != GameStates.RoundEnd) return Again;
        Check(Big.IsOpen && Current == "tut.period", "T8: on the round-end screen after the first Adak period is met, Eri is fed (big box)");
        Check(!Reacted("react.period"), "the repeating planet reaction waits until the lesson has been seen once");
        Complete(Big);
        return 1.2;   // round sonu düğmeleri açılış animasyonundan sonra yerine otursun
    }

    static double PeriodClosedShot()
    {
        Capture("Faz2_08_AdakTuttu", true);
        Big.Advance(); Big.Advance(); Big.Advance();
        Check(!Big.IsOpen, "two lines, then closed");
        Reachable(F<Button>(Object.FindAnyObjectByType<PlacementShopButtonUI>(), "button"), "PLANTER SHOP after the round");
        GameManager.Instance.OpenShop(); GameManager.Instance.ShowRoundEnd();
        Check(!Big.IsOpen, "coming back from a shop to the same round end does not repeat it");
        return .1;
    }

    static double ToSecondPeriodEnd()
    {
        if (State == GameStates.Round)
        {
            if (RM.CurrentRound == secondPeriodEnd)
            {
                Check(!Small.IsOpen && !Big.IsOpen, $"round {RM.CurrentRound} (Sınama + last round of period 2): both lessons were already seen, Eri stays quiet");
                return .1;
            }
            RM.EditorEndRound(true);
            return Again;
        }
        if (State != GameStates.RoundEnd) { SettleNow(); return Again; }
        UI.NextRound();
        return Again;
    }

    static double LoseQuota()
    {
        Check(RM.QuotaProgress < RM.QuotaTarget, "no score this period: the Adak is short");
        RM.EditorEndRound(true);
        Check(State == GameStates.RunComplete && RM.Outcome == RunOutcome.QuotaFailed, "the run ends: Adak failed");
        Check(Big.IsOpen && Current == "end.quota", "T11: Adak failure explains why the run ended");
        Check(!DialogueProgress.Seen("tut.kapris"), "this early death never met a kapris: its lesson is still unseen");
        Check(MetaSave.Data.runsFinished == 1 && DialogueProgress.Stage == 0, "run counter: 1 finished; story stage stays 0 (milestones come in Faz 3)");
        Complete(Big);
        return 1.2;   // run sonu paneli belirsin
    }

    static double QuotaShot()
    {
        Capture("Faz2_09_AdakKaybi", true);
        Big.Skip();
        Reachable(F<Button>(Object.FindAnyObjectByType<RestartButtonUI>(), "button"), "the run-end buttons");
        return .1;
    }
    static double BossShot() { Capture("Faz2_10_SinamaKaybi", true); return .1; }

    // ---------------------------------------------------------------- 2. ve 3. run
    static double Restart()
    {
        Object.FindAnyObjectByType<RunCompleteUI>().OnRestartPressed();
        return 3;
    }

    static double SecondRun()
    {
        if (RM == null || State != GameStates.RunSetup || UI == null) return Again;
        Check(!Big.IsOpen && !Small.IsOpen && Reacted("react.setup"),
            "run 2: the end box closed with the restart; the planter lesson does not repeat, planet Eri only reacts");
        Check(MetaSave.Data.runsStarted == 2, "run counter: 2 started");
        // Sınama kaybı: dönem payı hazırlıkta dolu, Sınama round'unun kendi hedefi boş kalır.
        long bossTarget = RM.BossTargetFor(1);
        Check(bossTarget > 0, $"first Sınama has its own harvest target ({bossTarget})");
        AddScore(RM.QuotaTargetFor(1));
        SetP(RM, "CurrentRound", bossRound);
        UI.NextRound();
        Check(State == GameStates.Round && RM.CurrentRound == bossRound && !Small.IsOpen, $"round {bossRound}: Sınama again, no repeated lessons");
        return .1;
    }

    static double LoseBoss()
    {
        RM.EditorEndRound(true);
        Check(State == GameStates.RunComplete && RM.Outcome == RunOutcome.BossFailed, "Adak met but the Sınama target missed: the run ends");
        Check(Big.IsOpen && Current == "end.boss", "T11: the Sınama loss has its own lines");
        Complete(Big);
        return 1.2;
    }

    static int kaprisRound;
    static double ThirdRun()
    {
        if (RM == null || State != GameStates.RunSetup || UI == null) return Again;
        Check(MetaSave.Data.runsStarted == 3 && MetaSave.Data.runsFinished == 2, "run counters: 3 started, 2 finished");
        // Geç round'lara doğrudan (Lab ile aynı yol): kapris planı R26'dan itibaren.
        SetP(RM, "CurrentRound", 26);
        UI.NextRound();
        Check(State == GameStates.Round && RM.CurrentRound == 26, "run 3 jumps to round 26 through the normal round start");
        Time.captureDeltaTime = .02f;
        return .1;
    }

    static double WaitKapris()
    {
        FieldEvent current = FieldEventDirector.Instance != null ? FieldEventDirector.Instance.Current : null;
        if (Small.IsOpen && Current == "tut.kapris") { kaprisRound = RM.CurrentRound; return .1; }
        if (State == GameStates.Round && current != null) { RoundManager.EditorFreezeTimer = true; return Again; }
        if (State == GameStates.Round)
        {
            if (RM.CurrentRound >= 45) throw new Exception("no kapris between R26 and R45");
            RoundManager.EditorFreezeTimer = false;
            RM.EditorEndRound(false);
            return Again;
        }
        if (State != GameStates.RoundEnd) { SettleNow(); return Again; }
        UI.NextRound();
        return Again;
    }

    static double Kapris()
    {
        FieldEvent current = FieldEventDirector.Instance.Current;
        Check(current != null && current.Phase != FieldEventPhase.Resolving && current.Phase != FieldEventPhase.Done,
            $"T10: the first kapris ({current?.Kind}, R{kaprisRound}, now {current?.Phase}) is announced as it prepares — unseen in the earlier runs, so it shows now");
        Complete(Small);
        Capture("Faz2_11_Kapris");
        Small.Step(DialogueBox.SmallCloseSeconds + .05f);
        Check(!Small.IsOpen, "kapris lesson closed by itself");
        return .1;
    }

    static double ToFinalRound()
    {
        if (FieldEventDirector.Instance.Current != null) FieldEventDirector.Instance.End();
        RoundManager.EditorFreezeTimer = false;
        Time.captureDeltaTime = 0f;
        Check(RM.EditorJumpToRound(RM.MaxRounds) && RM.CurrentRound == RM.MaxRounds && State == GameStates.Round, $"jump to the final round R{RM.MaxRounds}");
        long need = Math.Max(RM.QuotaTarget - RM.QuotaProgress, 0) + Math.Max(RM.BossTarget - RM.BossProgress, 0) + 1;
        AddScore(need);
        Check(RM.EditorEndRound(true) && !RM.RunFailed, "final round targets met");
        return .3;
    }

    static double Victory()
    {
        if (State != GameStates.RunComplete) return Again;
        Check(RM.Outcome == RunOutcome.Victory && Big.IsOpen && Current == "end.victory", "T11: victory has its own lines");
        Complete(Big);
        return 1.2;
    }

    static double VictoryShot()
    {
        Capture("Faz2_12_Zafer", true);
        Big.Skip();
        Check(DialogueProgress.Stage == 0, "story stage is still 0 (victory milestone is Faz 3)");
        Check(D.GetComponentsInChildren<Transform>(true).Length == directorObjects, $"no objects created by showing lines ({directorObjects} under the director, start and end)");
        Check(D.DroppedStale == 0, "no queued line went stale in the real flow");
        Check(errors == 0, $"no error or exception logged during the whole flow ({errors})");
        var seen = new[] { "intro.select", "tut.setup", "tut.place", "tut.hit", "tut.adak", "tut.level", "tut.lastround", "tut.period", "tut.boss", "tut.kapris", "end.quota", "end.boss", "end.victory" };
        Check(seen.All(DialogueProgress.Seen), "every lesson T1–T11 was seen exactly through its own moment");
        return .1;
    }

    // ---------------------------------------------------------------- görüntü ve çakışma
    // Görüntü 1600×900 (canvas'lar geçici olarak kameraya bağlanır). buttons: büyük kutunun görünür düğmelerle çakışmadığı ölçülür.
    static void Capture(string name, bool buttons = false)
    {
        List<string> open = null;
        int behind = 0;
        if (buttons)
        {
            // Olay sisteminin gerçek ışın testi (oyundaki ekran düzeninde): arkadaki her görünür düğmenin ortasına giden tık kutuya düşmeli.
            Check(Big.IsOpen, $"{name}: the big box is open for the input check");
            // Aynı karede açılan perdenin çizim sırası arayüz güncellenince hesaplanır (oyuncu en erken bir sonraki karede tıklar).
            Canvas.ForceUpdateCanvases();
            var visible = Behind();
            behind = visible.Count;
            if (InMenu)
            {
                // Menü kanvası kamerasız "kamera" modunda: penceresiz batch'te dünya konumu ekrana oturmaz, ışın testi ölçülemez.
                // Yerine yapı: perde tam ekran, etkin, tık alıyor ve ışın sıralamasında bütün diğer arayüzlerin üstünde.
                var mine = Big.GetComponent<GraphicRaycaster>();
                var others = Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None).Where(r => r != mine && r.IsActive()).ToList();
                RectTransform curtain = Big.Blocker.rectTransform;
                Check(Big.Blocker.isActiveAndEnabled && Big.Blocker.raycastTarget && curtain.anchorMin == Vector2.zero && curtain.anchorMax == Vector2.one &&
                      curtain.offsetMin == Vector2.zero && curtain.offsetMax == Vector2.zero && others.All(r => r.eventCamera == null && r.sortOrderPriority < mine.sortOrderPriority),
                    $"{name} (menu; batch cannot ray-test this canvas): the curtain covers the whole screen and sorts above the {others.Count} other UI raycasters " +
                    $"({mine.sortOrderPriority} vs {string.Join(", ", others.Select(r => r.sortOrderPriority))}), so clicks on the {behind} buttons behind land on it");
            }
            else open = visible.Select(s => (s, top: TopUi(Center(s)))).Where(x => x.top == null || x.top.GetComponentInParent<DialogueDirector>() == null)
                .Select(x => $"{x.s.name} (click lands on {(x.top != null ? x.top.name : "nothing")})").ToList();
        }
        // Menü sahnesinin kamerası MainCamera etiketli değil ve canvas'ı kamerasız "kamera" modunda (overlay gibi çizer).
        bool menu = SceneManager.GetActiveScene().name == "MenuScene";
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (camera == null) throw new Exception("no camera for " + name);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && (c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null))).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var target = new RenderTexture(1600, 900, 24);
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f; }
        var oldTarget = camera.targetTexture;
        target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        try
        {
            if (menu) camera.Render();
            else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous;
            Object.Destroy(image);
        }
        finally
        {
            camera.targetTexture = oldTarget;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; if (modes[i] == RenderMode.ScreenSpaceCamera) canvases[i].worldCamera = null; }
            Object.Destroy(target);
        }
        // Görüntü önce kaydedilir: çakışma olursa neyin örtüldüğü resimde görülür.
        if (open != null && open.Count > 0)
        {
            var first = Behind().FirstOrDefault();
            var raycasters = Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None);
            Note($"probe {name}: Screen {Screen.width}x{Screen.height}, first {first?.name} at {(first != null ? Center(first) : Vector2.zero)}, " +
                 $"blocker at {(Big.Blocker != null ? Center(Big.Blocker.rectTransform) : Vector2.zero)}, raycasters " +
                 string.Join("; ", raycasters.Select(r => $"{r.name}({r.GetType().Name}, active {r.IsActive()}, cam {(r.eventCamera != null ? r.eventCamera.name : "null")}, sort {r.sortOrderPriority})")));
        }
        if (open != null) Check(open.Count == 0, $"{name}: while Eri speaks, clicks on all {behind} buttons behind land on the box" + (open.Count > 0 ? " — still open: " + string.Join(", ", open) : ""));
    }

    // Yazı diziliyorsa tamamlar (görüntü için). Yazı zaten bittiyse dokunmaz: Advance kutuyu kapatırdı.
    // Görüntü için: giriş hareketi bitmiş, yazı tamam (yazı zaten bittiyse Advance'e dokunmaz: kutuyu kapatırdı).
    static void Complete(DialogueBox box)
    {
        box.Settle();
        if (box.Typing) box.Advance();
    }

    static List<Selectable> Behind() => Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None)
        .Where(s => s.IsActive() && s.interactable && s.GetComponentInParent<DialogueDirector>() == null && Visible(s.transform)).ToList();

    static readonly List<RaycastResult> rayHits = new();
    static GameObject TopUi(Vector2 screen)
    {
        if (EventSystem.current == null) throw new Exception("no EventSystem");
        Canvas.ForceUpdateCanvases();
        rayHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, rayHits);
        return rayHits.Count > 0 ? rayHits[0].gameObject : null;
    }

    static Vector2 Center(Selectable s) => Center((RectTransform)s.transform);
    // Ekran noktası kök kanvasın dikdörtgeni üzerinden (kanvas ekranı kaplar): dünya yerleşiminden bağımsız.
    static Vector2 Center(RectTransform rect)
    {
        var root = (RectTransform)rect.GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3 local = root.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        Rect area = root.rect;
        return new Vector2((local.x - area.xMin) / area.width * Screen.width, (local.y - area.yMin) / area.height * Screen.height);
    }

    // Konuşma bitti: arkadaki düğmenin ortasına giden tık o düğmeye ulaşır.
    static bool InMenu => SceneManager.GetActiveScene().name == "MenuScene";

    static void Reachable(Selectable s, string what)
    {
        Check(s != null, what + " exists");
        if (InMenu)
        {
            Check(!Big.IsOpen && !Big.BlocksInput && s.IsActive() && s.IsInteractable(), $"after Eri is done, {what} is clickable again (menu: curtain stops taking clicks at once, button active)");
            return;
        }
        Canvas.ForceUpdateCanvases();
        GameObject top = TopUi(Center(s));
        Check(!Big.IsOpen && top != null && top.transform.IsChildOf(s.transform), $"after Eri is done, {what} takes the click again (top hit: {(top != null ? top.name : "none")})");
    }

    static bool Visible(Transform t)
    {
        float alpha = 1f;
        foreach (CanvasGroup group in t.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
        return alpha > .01f;
    }

    static Rect ScreenRect(RectTransform rect, Camera camera)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        if (listening) Application.logMessageReceived -= CountLogs;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        RoundManager.EditorFreezeTimer = false;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/DialogueFlowVerification.txt", new[] { e == null ? "PASS " + notes.Count(n => n.StartsWith("PASS")) + " checks" : "FAIL step " + step + ": " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }
}
