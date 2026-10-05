using System;
using UnityEngine;

public enum RunOutcome { None, Victory, QuotaFailed, BossFailed }

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    public event Action OnRunStarted;
    public event Action OnRoundEnded;
    public event Action<int> OnTimeChanged;
    public event Action<int> OnRoundChanged;


    [SerializeField] private float roundDuration = 30f;
    [Tooltip("Run profili seçili değilse kullanılır (Tools > Run Profili).")]
    [SerializeField] private int maxRounds = 150;

    [Header("Round End Feel")]
    [Tooltip("Sürenin son kaç oyun-saniyesinde zaman yavaşlar. Oyun-zamanı değişmez; ekonomi etkilenmez.")]
    [SerializeField, Min(0f)] private float endSlowdownWindow = 0.3f;
    [Tooltip("Yavaşlamanın indiği hız. Süre bitince GameManager buradan yumuşakça 0'a indirir.")]
    [SerializeField, Range(0.05f, 1f)] private float endSlowdownFloor = 0.45f;
    private int lastDisplayedSecond = -1;

    [Header("Hasat Kotası (run profili seçili değilse)")]
    [Tooltip("Kaç round'da bir kota kontrol edilir. 0: kota kapalı.")]
    [SerializeField, Min(0)] private int quotaSegmentRounds = HarvestQuota.DefaultSegmentRounds;
    [Tooltip("İlk segmentin kotası (o segmentte kazanılan Harvest Score).")]
    [SerializeField, Min(5f)] private float quotaStart = HarvestQuota.DefaultStart;
    [Tooltip("Kota her segmentte bu oranla büyür. 1,45 = %45.")]
    [SerializeField, Range(1f, 3f)] private float quotaGrowth = HarvestQuota.DefaultGrowth;

    // Round en fazla bu kadar sürer. Süre skill'lerinin fazlası saldırı ve üretim hızına dönüşür:
    // round başına gelir aynı kalır, round kısalır. EconomyAnalyzer da bunu kullanır.
    public const float RoundSecondsCap = 60f;
    public const float MaxTempo = 90f / RoundSecondsCap;

    public int CurrentRound { get; private set; } = 1;
    public float RemainingTime { get; private set; }
    public bool IsRoundActive { get; private set; }
    // 0: normal, 1: sürenin bittiği an. Kamera odaklanması bunu okur.
    public float EndSlowdownProgress { get; private set; }

    private int pendingCardSelections = 0;
    // Level kartları (Bölüm 3.6): her level profilin verdiği kadar ayrı seçim hakkı getirir (varsayılan 1).
    public int BaseChoicesPerLevel => Profile != null ? Mathf.Max(1, Profile.choicesPerLevel) : 1;
    // Şu an kazanılacak bir level'ın getireceği hak: temel hak + run'daki aktif değişimler (Bölüm 3.7.4; RunPower.LevelChoices).
    // Ekrandaki aday sayısı (her seçimde üç kart, biri alınır) bundan ayrıdır ve değişmez.
    public int ChoicesPerLevel => RunPower.LevelChoices(BaseChoicesPerLevel);
    public int PendingCardSelections => pendingCardSelections;
    // Run sayaçları (HUD ve ölçüm), birbirinden ayrı: kazanılan level, verilen seçim hakkı, alınan kart (atlanan seçim kart değildir).
    public int LevelsGained { get; private set; }
    public int CardChoicesGranted { get; private set; }
    public int CardsTaken { get; private set; }
    public void RecordCardTaken() => CardsTaken++;
    private int skipUsesRemaining;
    private bool awaitingFirstRound = true;
    // Round süresi bitti, kart kararı bekleyen level işini bekliyor (Bölüm 3.7.6.1; ProgressionManager kare bütçesi).
    private bool awaitingLevels;
    public bool IsAwaitingLevels => awaitingLevels;

    public bool IsPreparingFirstRound => awaitingFirstRound;
    // Deney (Bölüm 2.2): profil süreyi sabitlerse süre stat'ı okunmaz; 60 sn üstü verilemez, yani süreden tempo doğmaz.
    // Bölüm 3.7.8: profil round süresi tablosu taşıyorsa (RoundDurations) süre round numarasına göre tablodan gelir. Süre yine
    // profilce sabittir (süre düğümleri etkisiz, tempo yok); tablodaki süre 60 sn sınırına takılmaz ve hıza dönüşmez.
    public bool HasDurationTable => RoundDurations.HasTable(Profile);
    public bool FixedRoundDuration => Profile != null && (Profile.fixedRoundDuration > 0f || HasDurationTable);
    public float RawRoundDuration => RoundSecondsFor(CurrentRound);
    public float EffectiveRoundDuration => HasDurationTable ? RawRoundDuration : Mathf.Min(RawRoundDuration, RoundSecondsCap);
    // 1: normal. 90 sn'lik süre 60 sn'ye sığınca 1,5: saldırı ve bitki üretimi 1,5 kat hızlı.
    public float TempoMultiplier => HasDurationTable ? 1f : Mathf.Max(1f, RawRoundDuration / RoundSecondsCap);
    // Verilen round'un süresi (sn). Tablolu profilde round'a göre değişir; diğerlerinde bütün round'lar için aynı kuraldır.
    public float RoundSecondsFor(int round) =>
        HasDurationTable ? RoundDurations.SecondsFor(Profile, Mathf.Max(1, round)) :
        FixedRoundDuration ? Mathf.Clamp(Profile.fixedRoundDuration, 30f, RoundSecondsCap) :
        Mathf.Clamp(StatManager.Instance != null
        ? StatManager.Instance.GetFinalStat(StatType.RoundDuration, StatTarget.All) : roundDuration, 30f, 90f);
    // Süre tablosu geçersizse run başlatılmaz (BeginRun). Tablo sessizce düzeltilmez.
    public string DurationError { get; private set; }
    private RunClock clock;

    // Run takvimi: kota dönemleri ve boss round'ları tek yerden çözülür (RunCalendar). Profil açık boss takvimi taşıyorsa dönemler
    // o tarihleri izler; taşımıyorsa eşit segmentler (eski davranış). Buradaki dönem / boss soruları takvime sorulur.
    private RunCalendar sceneCalendar;
    public RunCalendar Calendar => Profile != null ? Profile.Calendar
        : sceneCalendar ??= new RunCalendar(quotaSegmentRounds, maxRounds, quotaStart, quotaGrowth);
    // Açık takvim geçersizse run başlatılmaz (BeginRun); hata burada durur. Takvim sessizce düzeltilmez.
    public string CalendarError { get; private set; }
    // Aşamalı ödül havuzu geçersizse de run başlatılmaz (aynı ödül iki aşamada, sırasız aşamalar …). Veri düzeltilmez.
    public string RewardPoolError { get; private set; }

    // Hasat Kotası: dönem (segment) boyunca kazanılan Harvest Score. İlerleme, dönemin ilk round'undan beri kazanılan skordur.
    // Dönem sonunda değerlendirilir; sonuç bir sonraki dönem başlayana kadar Last* alanlarında kalır.
    public bool QuotaEnabled => Calendar.QuotaEnabled;
    // Eşit segment uzunluğu (eski profiller). Açık takvimde dönemler eşit değildir: içinde bulunulan dönemin uzunluğunu verir.
    public int QuotaSegmentRounds => Calendar.IsExplicit ? Calendar.PeriodLength(QuotaSegment) : Calendar.UniformRounds;
    public int QuotaSegment => Calendar.PeriodOf(CurrentRound);
    public int QuotaSegmentEnd => Calendar.PeriodEnd(QuotaSegment);
    public long QuotaTarget => QuotaTargetFor(QuotaSegment);
    public long QuotaProgress => System.Math.Max(0L, CurrentScore - segmentStartScore);
    public bool IsQuotaSegmentEnd(int round) => Calendar.IsPeriodEnd(round);
    public long QuotaTargetFor(int segment) => Calendar.QuotaTarget(segment);
    public bool EndedByQuota { get; private set; }
    public int LastQuotaRound { get; private set; }
    public long LastQuotaScore { get; private set; }
    public long LastQuotaTarget { get; private set; }
    private long segmentStartScore;
    private static long CurrentScore => HarvestScoreManager.Instance != null ? HarvestScoreManager.Instance.TotalScore : 0;

    // Boss hasadı (Bölüm 3.4): boss round'unda, yalnız o round'da kazanılan Harvest Score. Segment kotasından ayrı ikinci koşuldur;
    // hedef profil tablosundan gelir (oyuncunun gücüne göre ölçeklenmez). Hedef tutunca round erken bitmez.
    public bool IsBossRound(int round) => Calendar.IsBossRound(round);
    public long BossTargetFor(int segment) => Calendar.BossTarget(segment);
    public long BossTarget => BossTargetFor(QuotaSegment);
    public long BossProgress => IsBossRound(CurrentRound) ? System.Math.Max(0L, CurrentScore - roundStartScore) : 0L;
    public bool EndedByBoss { get; private set; }
    public int LastBossRound { get; private set; }
    public long LastBossScore { get; private set; }
    public long LastBossTarget { get; private set; }
    // Run bu round'da bir koşul tutmadığı için bitti (kota, boss hasadı ya da ikisi).
    public bool RunFailed => EndedByQuota || EndedByBoss;
    private long roundStartScore;

    public int SkipUsesRemaining => skipUsesRemaining;
    // Round sonu seçimleri (IRoundChoice): kartlardan sonra, round özetinden önce. Türlerini bilmeden sırayı yönetir.
    private readonly System.Collections.Generic.List<IRoundChoice> roundChoices = new();
    public bool IsRoundChoicePending { get { foreach (IRoundChoice c in roundChoices) if (c.IsPending) return true; return false; } }
    public void RegisterRoundChoice(IRoundChoice choice) { if (choice != null && !roundChoices.Contains(choice)) roundChoices.Add(choice); }
    public void UnregisterRoundChoice(IRoundChoice choice) => roundChoices.Remove(choice);
    // Run uzunluğu, kota segmentleri, olaylar ve başlangıç ekonomisi tek yerden: aktif run profili. Yoksa sahnedeki alanlar.
    public RunProfileSO Profile { get; private set; }
    public int MaxRounds => Profile != null ? Profile.runLength : maxRounds;
    public RunOutcome Outcome { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Profile = RunProfileSelectionSO.Active;
        // Boss kuralları RoundManager'da değil, olay yürütücüsünde; RoundManager sadece round olaylarını yayınlar.
        if (!TryGetComponent(out SegmentEventDirector _)) gameObject.AddComponent<SegmentEventDirector>();
        if (!TryGetComponent(out SpecializationManager _)) gameObject.AddComponent<SpecializationManager>();
        // Run başı seçim (çiftçi + tırpan) ve kalıcı görev sayacı; ikisi de OnRunStarted'da kurulur.
        if (!TryGetComponent(out StartLoadoutManager _)) gameObject.AddComponent<StartLoadoutManager>();
        if (!TryGetComponent(out QuestTracker _)) gameObject.AddComponent<QuestTracker>();
        // Boss ödülleri (run buff'ları): profilde ödül havuzu yoksa hiçbir şey yapmaz.
        if (!TryGetComponent(out BossRewardManager _)) gameObject.AddComponent<BossRewardManager>();
        // Kırılma ödüllerinin gecikmiş ikinci darbeleri: ödül alınmadıysa hiçbir şey yapmaz.
        if (!TryGetComponent(out BehaviorEchoes _)) gameObject.AddComponent<BehaviorEchoes>();
        // Davranış zinciri (Zincir Hasat ödülü): ödül alınmadıysa hiçbir şey yapmaz; kuralları HarvestChain'dedir.
        if (!TryGetComponent(out HarvestChain _)) gameObject.AddComponent<HarvestChain>();
        // Run süre sayaçları (aktif / seçim / mağaza / hazırlık / duraklama): yalnız sayar, akışı değiştirmez.
        if (!TryGetComponent(out clock)) clock = gameObject.AddComponent<RunClock>();
    }

    private void Start()
    {
        ProgressionManager.Instance.OnLevelUp += HandleLevelUp; // Start'ta güvenli
        BeginRun();
    }

    private void OnDestroy()
    {
        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    // Segmentin son round'u bitince: segmentte kazanılan skor kotanın altındaysa run biter.
    private void EvaluateQuota()
    {
        if (!IsQuotaSegmentEnd(CurrentRound)) return;
        LastQuotaRound = CurrentRound;
        LastQuotaTarget = QuotaTarget;
        LastQuotaScore = QuotaProgress;
        EndedByQuota = LastQuotaScore < LastQuotaTarget;
        EvaluateBoss();
    }

    // Boss round'unun sonunda: o round'da kazanılan skor boss hedefinin altındaysa run biter (segment kotası dolmuş olsa da).
    private void EvaluateBoss()
    {
        if (!IsBossRound(CurrentRound)) return;
        LastBossRound = CurrentRound;
        LastBossTarget = BossTarget;
        LastBossScore = BossProgress;
        EndedByBoss = LastBossTarget > 0 && LastBossScore < LastBossTarget;
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
            return;

        if (awaitingLevels)
        {
            ContinueRoundEnd();
            return;
        }

        if (!IsRoundActive)
            return;

        TickRound();
    }

    private void TickRound()
    {
        float before = RemainingTime;
        RemainingTime -= Time.deltaTime;
        RemainingTime = Mathf.Max(RemainingTime, 0f);
        // Aktif süre: sayaçtan düşen oyun zamanı ve bu karede gerçekte geçen süre ayrı tutulur (RunClock).
        if (clock != null) clock.AddActive((double)before - RemainingTime, RunClock.FrameRealSeconds);
        ApplyEndSlowdown();

        int currentSecond = Mathf.CeilToInt(RemainingTime);

        if (currentSecond != lastDisplayedSecond)
        {
            lastDisplayedSecond = currentSecond;
            OnTimeChanged?.Invoke(currentSecond);
        }

        if (RemainingTime <= 0f)
        {
            EndRound();
        }
    }

    // Round aynı oyun-süresinde biter; sadece son an gerçek zamanda biraz uzar.
    // Süre bitince GameManager timeScale'i buradan yumuşakça 0'a indirir, yeni round'da 1'e çeker.
    private void ApplyEndSlowdown()
    {
        if (endSlowdownWindow <= 0f || !GameSettings.RoundEndSlowMotion)
        {
            EndSlowdownProgress = 0f;
            return;
        }

        float t = 1f - Mathf.Clamp01(RemainingTime / endSlowdownWindow);
        EndSlowdownProgress = t;
        if (t <= 0f) return;

        float eased = t * t * (3f - 2f * t);
        Time.timeScale = Mathf.Lerp(1f, endSlowdownFloor, eased);
    }

    public void BeginRun()
    {
        CalendarError = RunCalendar.Validate(Profile);
        if (CalendarError != null)
        {
            Debug.LogError($"Run başlatılmadı: '{Profile.displayName}' profilinin boss takvimi geçersiz — {CalendarError}. " +
                           "Takvim otomatik düzeltilmez; profil verisi düzeltilince run başlar.", Profile);
            return;
        }
        RewardPoolError = Profile != null ? BossRewardPoolSO.Validate(Profile.bossRewards, Profile.runLength) : null;
        if (RewardPoolError != null)
        {
            Debug.LogError($"Run başlatılmadı: '{Profile.displayName}' profilinin boss ödül aşamaları geçersiz — {RewardPoolError}. " +
                           "Havuz otomatik düzeltilmez; veri düzeltilince run başlar.", Profile);
            return;
        }
        DurationError = RoundDurations.Validate(Profile);
        if (DurationError != null)
        {
            Debug.LogError($"Run başlatılmadı: '{Profile.displayName}' profilinin round süresi tablosu geçersiz — {DurationError}. " +
                           "Tablo otomatik düzeltilmez; veri düzeltilince run başlar.", Profile);
            return;
        }
        RunPower.Reset();
        CurrentRound = 1;
        awaitingLevels = false;
        pendingCardSelections = 0;
        LevelsGained = 0;
        CardChoicesGranted = 0;
        CardsTaken = 0;
        awaitingFirstRound = true;
        IsRoundActive = false;
        RemainingTime = EffectiveRoundDuration;
        skipUsesRemaining = 0;
        lastDisplayedSecond = -1;
        segmentStartScore = CurrentScore;
        EndedByQuota = false;
        Outcome = RunOutcome.None;
        LastQuotaRound = 0;
        LastQuotaScore = LastQuotaTarget = 0;
        EndedByBoss = false;
        LastBossRound = 0;
        LastBossScore = LastBossTarget = 0;
        roundStartScore = CurrentScore;

        SkillTreeManager.Instance.ResetTree();
        UnlockManager.Instance.ResetUnlocks();
        // Denge seti bazı kilitleri başlangıçtan açık verebilir (Bölüm 3.6: patlama ve elektrik kartları).
        RunBalanceSO balance = Profile != null ? Profile.balance : null;
        if (balance != null && balance.startingUnlocks != null)
            foreach (UnlockType unlock in balance.startingUnlocks) UnlockManager.Instance.Unlock(unlock);

        if (Application.isEditor && Profile != null)
            Debug.Log($"Run profili: {Profile.displayName} · {MaxRounds} round{(Profile.debugBudget ? " · DEBUG BÜTÇE" : "")}", Profile);
        OnRunStarted?.Invoke();
        GameManager.Instance.StartRunSetup();
    }

    public void StartRound()
    {
        awaitingFirstRound = false;
        awaitingLevels = false;
        CurrentRound = Mathf.Max(CurrentRound, 1);
        RemainingTime = EffectiveRoundDuration;
        IsRoundActive = true;
        if (Calendar.IsPeriodStart(CurrentRound)) segmentStartScore = CurrentScore;
        roundStartScore = CurrentScore;
        EndSlowdownProgress = 0f;
        lastDisplayedSecond = -1;

        skipUsesRemaining = 1 + Mathf.RoundToInt(
        StatManager.Instance.GetFinalStat(StatType.CardSkip, StatTarget.All));

        OnRoundChanged?.Invoke(CurrentRound);
        GameManager.Instance.StartGame();
    }

    private void EndRound()
    {
        IsRoundActive = false;
        RemainingTime = 0f;
        EvaluateQuota();
        OnRoundEnded?.Invoke();
        awaitingLevels = true;
        ContinueRoundEnd();
    }

    // Round sonu kararı. Run bitmiyorsa kart kararı, round içinde kazanılan XP'nin bütün level'ları işlenmeden verilmez: bekleyen
    // level işi varsa (ProgressionManager kare bütçesi) her karede yeniden denenir. Normal hasatta bekleyen iş olmaz; karar
    // EndRound içinde, eskisi gibi aynı karede verilir.
    private void ContinueRoundEnd()
    {
        // Kota ya da boss hasadı tutmadı: run burada biter, bekleyen kart seçimleri atlanır
        if (RunFailed)
        {
            awaitingLevels = false;
            pendingCardSelections = 0;
            Outcome = EndedByQuota ? RunOutcome.QuotaFailed : RunOutcome.BossFailed;
            GameManager.Instance.CompleteRun();
            return;
        }

        // Son round ise direkt bitir: kota geçildi (ya da kapalı), run kazanıldı.
        // Açık takvimde son round boss round'uysa sıra bozulmaz: kartlar → boss ödülü → zafer (CloseRound).
        if (CurrentRound >= MaxRounds && !Calendar.FinalRoundHasChoices)
        {
            awaitingLevels = false;
            pendingCardSelections = 0; // kart seçimini atla
            Outcome = RunOutcome.Victory;
            GameManager.Instance.CompleteRun();
            return;
        }

        if (ProgressionManager.Instance != null && ProgressionManager.Instance.HasPendingLevels) return;
        awaitingLevels = false;

        if (pendingCardSelections > 0)
            GameManager.Instance.StartCardSelection();
        else
            ShowRoundEndOrChoice();
    }

    // Sıra: kota değerlendirmesi → bekleyen kart seçimleri → round sonu seçimi (varsa) → round özeti / hazırlık.
    private void ShowRoundEndOrChoice()
    {
        if (IsRoundChoicePending) GameManager.Instance.StartRoundChoice();
        else CloseRound();
    }

    // Seçim tamamlanınca (IRoundChoice sahibi çağırır) round özetine geçilir.
    public void ContinueAfterRoundChoice()
    {
        if (GameManager.Instance.CurrentState != GameStates.RoundChoice || IsRoundChoicePending) return;
        CloseRound();
    }

    // Seçimler bitti: run'ın son round'uysa zafer ekranı (sonraki round başlamaz), değilse round özeti.
    private void CloseRound()
    {
        if (CurrentRound >= MaxRounds)
        {
            Outcome = RunOutcome.Victory;
            GameManager.Instance.CompleteRun();
            return;
        }
        GameManager.Instance.ShowRoundEnd();
    }
    
    // Hak, level kazanıldığı anda hesaplanır: aynı karede kazanılan her level kendi hakkını ekler; bekleyen sayaç sonradan
    // alınan bir ödülle yeniden hesaplanmaz.
    private void HandleLevelUp(int newLevel)
    {
        int choices = ChoicesPerLevel;
        LevelsGained++;
        // Sayaçlar int sınırında doyar (NumericSafety, raporlanır): o kadar seçim zaten oynanamaz (P7: seçim ekranı yükü).
        CardChoicesGranted = NumericSafety.Add(CardChoicesGranted, choices, NumericSite.Experience);
        pendingCardSelections = NumericSafety.Add(pendingCardSelections, choices, NumericSite.Experience);
    }

    public bool OnCardSelectionComplete()
    {
        pendingCardSelections--;

        if (pendingCardSelections > 0)
            return true;  // daha seçim var, kartları yenile

        ShowRoundEndOrChoice();
        return false;  // bitti, round sonu seçimine ya da RoundEnd'e geç
    }

    public bool TryUseSkip()
    {
        if(skipUsesRemaining <= 0)
        {
            return false;
        }

        skipUsesRemaining--;
        return true;
    }

    public void StartNextRound()
    {
        if (GameManager.Instance.CurrentState != GameStates.Shop &&
            GameManager.Instance.CurrentState != GameStates.RoundEnd &&
            GameManager.Instance.CurrentState != GameStates.RunSetup)
            return;
        // Round sonu seçimi tamamlanmadan sonraki round başlamaz. Geçersiz takvim, ödül havuzu ya da süre tablosuyla run hiç başlamaz.
        if (IsRoundChoicePending || CalendarError != null || RewardPoolError != null || DurationError != null) return;

        if (!awaitingFirstRound) CurrentRound++;

        if (CurrentRound > MaxRounds)
        {
            Outcome = RunOutcome.Victory;
            GameManager.Instance.CompleteRun();
            return;
        }
        StartRound();
    }

    public void ResetRounds()
    {
        awaitingFirstRound = true;
        CurrentRound = 1;
        RemainingTime = EffectiveRoundDuration;
        IsRoundActive = false;
    }

}
