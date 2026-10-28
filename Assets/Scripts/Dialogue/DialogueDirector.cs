using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Diyalog yöneticisi (25 Ekim 2026, plan: Docs/DiyalogPlani, Faz 1). Tutorial ve hikâye aynı yoldan geçer:
// oyunun zaten yaydığı olaylar → tetik → katalogdan uygun konuşma → kutu → kayıt (DialogueProgress).
// Kurallar (planla aynı):
// - Her adım bir olaya bağlıdır, run sayısına değil: görülmemiş konuşma ilgili olayla ilk uygun karşılaşmada çıkar.
// - Öncelik: ilk karşılaşmanın gerekli öğretimi (Guide) > hikâye > adlandırma > tepki. Aynı anda tek kutu, en çok bir bekleyen.
//   Öğretimle çakışan hikâye kaybolmaz, sonraki sakin ana ertelenir (kalıcı). Bekleyen konuşma gösterilmeden önce hâlâ geçerli mi
//   diye bakılır; geçersizse gösterilmez ve "görülmedi" kalır.
// - Konuşma gösterildiği anda "görüldü" sayılır (atlansa da). Büyük kutu round dışında, küçük kutu round içinde açılır; durum
//   değişince kendi durumuna ait olmayan kutu kapanır.
// - Saat: menü ekranlarında (timeScale 0) akar; Paused ya da pencere arka plandayken durur.
// - Mechanics Test Lab'de kendiliğinden çalışmaz: test sahnesinde ve lab oturumunda (GameScene'i lab yükler; TestSession bayrağı
//   açar). LabEnabled ileride lab araçlarıyla açılır. Otomatik testlerde (batch) yalnız test açarsa (BatchEnabled) kurulur.
// - Olay tabanlı: her karede kontrol yok; kutular kapalıyken güncelleme çalışmaz.
// - Faz 2 tetikleri: round başında Sınama round'u ve "Adak dönemin son round'unda hâlâ dolmadı"; round sonu ekranında "Adak tuttu".
//   Sınama öğretimi tarla önizlemesine (Tab) bağlı değildir; Sınama bilgisi soldaki Adak panelinde hep görünür.
[DefaultExecutionOrder(1200)]
public sealed class DialogueDirector : MonoBehaviour
{
    public const string LabScene = "MechanicsTestScene";
    public static DialogueDirector Instance { get; private set; }
    // Oyuncu duraklatması (ileride pause menüsü bağlanır). Menü ekranlarının "zaman durmuş" hâli bu değildir.
    public static bool Paused;
    public static bool BatchEnabled;
    // Mechanics Test Lab: oturum GameScene'i yükleyerek çalışır; lab oturumu bu bayrağı açar (TestSession), diyalog susar.
    // LabEnabled ileride lab'in diyalog/tutorial test araçları içindir (o zaman lab oturumunda da çalışır).
    public static bool LabSession;
    public static bool LabEnabled;

    // Konuşma saati: ölçeksiz zaman (menüde de akar), duraklatma ya da pencere arka plandayken 0. Uzun kare sıçramaları kırpılır.
    public static float ClockDelta =>
        Paused || (!Application.isFocused && !Application.isBatchMode) ? 0f : Mathf.Min(Time.unscaledDeltaTime, .1f);

    public DialogueBox BigBox { get; private set; }
    public DialogueBox SmallBox { get; private set; }
    public DialogueEntry Current => current;
    public DialogueEntry Queued => queued;
    public int DroppedStale { get; private set; }

    private DialogueCatalogSO catalog;
    private readonly Dictionary<string, DialogueEntry> byId = new();
    private readonly Dictionary<string, float> lastShown = new();
    private DialogueEntry current, queued;
    private DialogueTrigger queuedTrigger;
    private GameStates state = GameStates.MainMenu, bigState;
    private RunOutcome outcome;
    private int periodClosedRound;
    private bool setupPending;
    private GameManager boundGame;
    private RoundManager boundRounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; Paused = false; BatchEnabled = false; LabSession = false; LabEnabled = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode && !BatchEnabled) return;
        Ensure();
    }

    public static DialogueDirector Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("Dialogue Director");
        DontDestroyOnLoad(go);
        return go.AddComponent<DialogueDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        UseCatalog(Resources.Load<DialogueCatalogSO>(DialogueCatalogSO.ResourcePath));
        BigBox = DialogueBox.Create(transform, true, 60);
        SmallBox = DialogueBox.Create(transform, false, 59);
        BigBox.Finished += OnFinished;
        SmallBox.Finished += OnFinished;
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartSelectionPanelUI.Opened += OnSelectionOpened;
        RoundPreviewUI.Opened += OnPreviewOpened;
        FieldEventDirector.Cued += OnFieldCue;
        Bind();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartSelectionPanelUI.Opened -= OnSelectionOpened;
        RoundPreviewUI.Opened -= OnPreviewOpened;
        FieldEventDirector.Cued -= OnFieldCue;
        Unbind();
        GreenhouseMascot.Talking = false;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Bind();

    // GameManager sahneler arası yaşar; RoundManager yalnız oyun sahnesinde: her sahne yüklenince yeniden bağlanılır.
    private void Bind()
    {
        if (boundGame != GameManager.Instance)
        {
            if (boundGame != null) boundGame.OnGameStateChanged -= OnState;
            boundGame = GameManager.Instance;
            if (boundGame != null) { boundGame.OnGameStateChanged += OnState; state = boundGame.CurrentState; }
        }
        if (boundRounds != RoundManager.Instance)
        {
            if (boundRounds != null) boundRounds.OnRunStarted -= OnRunStarted;
            boundRounds = RoundManager.Instance;
            if (boundRounds != null) boundRounds.OnRunStarted += OnRunStarted;
        }
    }

    private void Unbind()
    {
        if (boundGame != null) boundGame.OnGameStateChanged -= OnState;
        if (boundRounds != null) boundRounds.OnRunStarted -= OnRunStarted;
        boundGame = null;
        boundRounds = null;
    }

    public static bool Suspended => !LabEnabled && (LabSession || SceneManager.GetActiveScene().name == LabScene);

    // ---- olaylar → tetikler ----
    // Hazırlık ekranı tetiği yalnız oyun sahnesinde, run kurulunca: menü "Başla"da durumu RunSetup'a menü sahnesindeyken çevirir,
    // oyun sahnesinde aynı durum yeniden yayınlanmaz (GameManager aynı duruma geçişi yok sayar). Bu yüzden run başı bekletilir:
    // durum zaten RunSetup ise hemen, değilse RunSetup'a geçişte tetiklenir.
    private void OnRunStarted()
    {
        periodClosedRound = 0;
        if (Suspended) return;
        DialogueProgress.RunStarted();
        setupPending = true;
        if (state == GameStates.RunSetup) RaiseSetup();
    }

    private void RaiseSetup()
    {
        setupPending = false;
        Raise(DialogueTrigger.RunSetupOpened);
    }
    private void OnSelectionOpened() => Raise(DialogueTrigger.SelectionOpened);
    private void OnPreviewOpened() => Raise(DialogueTrigger.FieldPreview);
    private void OnFieldCue(FieldEventCue cue) { if (cue.Kind == FieldEventCueKind.Prepared) Raise(DialogueTrigger.FieldEventPrepared); }

    private void OnState(GameStates next)
    {
        GameStates previous = state;
        state = next;
        // Kendi durumuna ait olmayan kutu kapanır (görüldü sayılır).
        if (SmallBox.IsOpen && next != GameStates.Round) SmallBox.Close();
        if (BigBox.IsOpen && next != bigState) BigBox.Close();
        if (Suspended) return;
        switch (next)
        {
            case GameStates.RunSetup: if (setupPending) RaiseSetup(); break;
            case GameStates.Placing: Raise(DialogueTrigger.PlacingEntered); break;
            case GameStates.Round: if (previous != GameStates.Round) RoundBegan(); break;
            case GameStates.RoundEnd: PeriodEnded(); break;
            case GameStates.CardSelection: Raise(DialogueTrigger.LevelUp); break;
            case GameStates.RunComplete:
                outcome = RoundManager.Instance != null ? RoundManager.Instance.Outcome : RunOutcome.None;
                DialogueProgress.RunFinished();
                Raise(DialogueTrigger.RunEnded);
                break;
        }
        if (Calm(next)) TryDeferredStory();
    }

    // Round başı: genel round satırı, sonra Sınama round'u ve geride kalan Adak (aynı anda uygunsa biri bekler, sırayla çıkar).
    private void RoundBegan()
    {
        Raise(DialogueTrigger.RoundStarted);
        RoundManager rounds = RoundManager.Instance;
        if (rounds == null || !rounds.QuotaEnabled) return;
        if (rounds.IsBossRound(rounds.CurrentRound)) Raise(DialogueTrigger.BossRound);
        if (QuotaBehind(rounds)) Raise(DialogueTrigger.PeriodLastRound);
    }

    // Dönemin son round'u başlarken Adak'ın dolmamış olması (tek round'luk dönemde de geçerlidir).
    public static bool QuotaBehind(RoundManager rounds) =>
        rounds.IsQuotaSegmentEnd(rounds.CurrentRound) && rounds.QuotaProgress < rounds.QuotaTarget;

    // Round sonu ekranı: bu round bir dönemi kapattıysa ve run sürüyorsa (Adak tuttu). Dükkândan dönüşte aynı round için tekrar etmez.
    private void PeriodEnded()
    {
        RoundManager rounds = RoundManager.Instance;
        if (!PeriodJustClosed(rounds) || periodClosedRound == rounds.CurrentRound) return;
        periodClosedRound = rounds.CurrentRound;
        Raise(DialogueTrigger.PeriodClosed);
    }

    public static bool PeriodJustClosed(RoundManager rounds) =>
        rounds != null && rounds.QuotaEnabled && rounds.LastQuotaRound > 0 && rounds.LastQuotaRound == rounds.CurrentRound && !rounds.RunFailed;

    private static bool Calm(GameStates s) => s == GameStates.RunSetup || s == GameStates.RoundEnd || s == GameStates.RunComplete;

    // ---- seçim ----
    public void Raise(DialogueTrigger trigger)
    {
        if (catalog == null || Suspended) return;
        DialogueEntry best = null;
        foreach (DialogueEntry e in catalog.entries)
            if (e != null && e.trigger == trigger && Eligible(e) && (best == null || Rank(e) > Rank(best))) best = e;
        if (best == null) return;
        // Aynı anda uygun olup seçilmeyen hikâye konuşmaları kaybolmaz: sonraki sakin ana ertelenir.
        foreach (DialogueEntry e in catalog.entries)
            if (e != null && e != best && e.category == DialogueCategory.Story && e.trigger == trigger && Eligible(e)) DialogueProgress.DeferStory(e.id);
        Offer(best, trigger);
    }

    private static int Rank(DialogueEntry e) => (int)e.category * 1000 + e.priority;

    private bool Eligible(DialogueEntry e)
    {
        if (e.lines == null || e.lines.Length == 0 || string.IsNullOrEmpty(e.id)) return false;
        int stage = DialogueProgress.Stage;
        if (stage < e.minStage || stage > e.maxStage) return false;
        if (e.frequency == DialogueFrequency.Once && DialogueProgress.Seen(e.id)) return false;
        if (e.frequency == DialogueFrequency.Repeat && lastShown.TryGetValue(e.id, out float at) && Time.unscaledTime - at < e.cooldownSeconds) return false;
        if (!string.IsNullOrEmpty(e.requiresSeen) && !DialogueProgress.Seen(e.requiresSeen)) return false;
        if (e.trigger == DialogueTrigger.RunEnded && e.outcome != RunOutcome.None && e.outcome != outcome) return false;
        if (!SizeFits(e)) return false;
        return e != current && e != queued;
    }

    private bool SizeFits(DialogueEntry e) => e.size switch
    {
        DialogueSize.Small => state == GameStates.Round,
        DialogueSize.Big => state != GameStates.Round,
        _ => true,
    };

    private void Offer(DialogueEntry e, DialogueTrigger trigger)
    {
        if (e.size == DialogueSize.Planet)
        {
            GreenhouseMascot.React(DialogueText.Get(e.lines[0].textKey));
            MarkShown(e);
            return;
        }
        if (!BigBox.IsOpen && !SmallBox.IsOpen) { Show(e); return; }
        if (queued == null || Rank(e) > Rank(queued))
        {
            if (queued != null && queued.category == DialogueCategory.Story) DialogueProgress.DeferStory(queued.id);
            queued = e;
            queuedTrigger = trigger;
        }
        else if (e.category == DialogueCategory.Story) DialogueProgress.DeferStory(e.id);
    }

    private void Show(DialogueEntry e)
    {
        current = e;
        MarkShown(e);
        DialogueBox box = e.size == DialogueSize.Small ? SmallBox : BigBox;
        if (box.Big) bigState = state;
        GreenhouseMascot.Talking = true;
        box.Show(e);
    }

    private void MarkShown(DialogueEntry e)
    {
        if (e.frequency == DialogueFrequency.Once) DialogueProgress.MarkSeen(e.id);
        lastShown[e.id] = Time.unscaledTime;
    }

    private void OnFinished()
    {
        current = null;
        GreenhouseMascot.Talking = false;
        if (queued != null)
        {
            DialogueEntry next = queued;
            DialogueTrigger trigger = queuedTrigger;
            queued = null;
            if (StillValid(next, trigger)) { Show(next); return; }
            DroppedStale++;   // gösterilmedi: "görülmedi" kalır, bir sonraki karşılaşmada yeniden aday
            if (next.category == DialogueCategory.Story) DialogueProgress.DeferStory(next.id);
        }
        if (Suspended) return;
        if (state == GameStates.Round) Raise(DialogueTrigger.RoundIdle);
        else if (Calm(state)) TryDeferredStory();
    }

    // Bekleyen konuşma hâlâ yerinde mi: tetiğin anı geçtiyse gösterilmez ("şimdi şu köke vur" olay bitince denmez).
    private bool StillValid(DialogueEntry e, DialogueTrigger trigger)
    {
        if (!SizeFits(e) || (e.frequency == DialogueFrequency.Once && DialogueProgress.Seen(e.id))) return false;
        switch (trigger)
        {
            case DialogueTrigger.RoundStarted:
            case DialogueTrigger.RoundIdle:
            case DialogueTrigger.BossRound:
                return state == GameStates.Round && RoundManager.Instance != null && RoundManager.Instance.IsRoundActive;
            case DialogueTrigger.PeriodLastRound:
                return state == GameStates.Round && RoundManager.Instance != null && RoundManager.Instance.IsRoundActive && QuotaBehind(RoundManager.Instance);
            case DialogueTrigger.PeriodClosed: return state == GameStates.RoundEnd && PeriodJustClosed(RoundManager.Instance);
            case DialogueTrigger.FieldEventPrepared:
            {
                FieldEvent fieldEvent = FieldEventDirector.Instance != null ? FieldEventDirector.Instance.Current : null;
                return fieldEvent != null && (fieldEvent.Phase == FieldEventPhase.Preparing || fieldEvent.Phase == FieldEventPhase.Emerging || fieldEvent.Phase == FieldEventPhase.Active);
            }
            case DialogueTrigger.FieldPreview: return RoundPreviewUI.IsOpen;
            case DialogueTrigger.LevelUp: return state == GameStates.CardSelection;
            case DialogueTrigger.PlacingEntered: return state == GameStates.Placing;
            case DialogueTrigger.RunSetupOpened: return state == GameStates.RunSetup;
            case DialogueTrigger.SelectionOpened: return state == GameStates.MainMenu;
            case DialogueTrigger.RunEnded: return state == GameStates.RunComplete;
            default: return true;
        }
    }

    // Ertelenmiş hikâye: sakin anda (round dışı ekran), kutular boşken, sırayla en çok bir konuşma.
    private void TryDeferredStory()
    {
        if (BigBox.IsOpen || SmallBox.IsOpen || queued != null) return;
        IReadOnlyList<string> pending = DialogueProgress.PendingStory;
        for (int i = 0; i < pending.Count; i++)
        {
            if (!byId.TryGetValue(pending[i], out DialogueEntry e) || e.size != DialogueSize.Big) continue;
            int stage = DialogueProgress.Stage;
            if (stage < e.minStage || stage > e.maxStage || DialogueProgress.Seen(e.id)) continue;
            Show(e);
            return;
        }
    }

    // Katalog değiştirme (ilk kurulum, testler ve ileride Mechanics Test Lab araçları).
    public void UseCatalog(DialogueCatalogSO source)
    {
        catalog = source;
        byId.Clear();
        current = queued = null;
        if (catalog == null) return;
        foreach (DialogueEntry e in catalog.entries)
            if (e != null && !string.IsNullOrEmpty(e.id) && !byId.ContainsKey(e.id)) byId.Add(e.id, e);
    }

    // Test / araç: durum bilgisi ve durum değişimini olay yoluyla taklit etme.
    public GameStates State => state;
    public void ForceState(GameStates s) => OnState(s);
    public void ForceRunStarted() => OnRunStarted();
}
