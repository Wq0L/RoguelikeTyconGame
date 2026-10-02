using System.Collections.Generic;
using UnityEngine;

// Segment olaylarını (boss) yürütür. RoundManager kurar; olayları aktif run profilinden alır. İki ritim:
// - Eski profiller (RunProfileSO.events): olay bütün segment aktiftir; bir önceki segmentin başında duyurulur ve bölgesi seçilir.
// - Boss ritmi (RunProfileSO.bossPool, Bölüm 3.4): her segmentin boss'u segment başında havuzdan seçilir ve duyurulur
//   (ilk dört round hazırlık), yalnız segmentin son round'unda aktiftir. Run'ın son segmentinde boss yoktur.
// Boss seçimi: run'ın boss seed'i + segmentten türeyen ayrı bir System.Random; aynı seed ve aynı tarla durumu aynı diziyi verir,
// UnityEngine.Random akışına (hasat, kritik, kart) dokunmaz. Seçilen boss ve bölgesi duyurudan sonra değişmez.
// Run sonu, ana menü ve yok edilmede (sahne değişimi, yeniden başlatma) bütün olaylar aynı yoldan temizlenir.
// Oynanış yalnız çarpan sorguları (üretim aralığı, doğan bitki canı) ve olayın kendi geçici etkisi üzerinden etkilenir; arayüz sadece okur.
[DisallowMultipleComponent]
public sealed class SegmentEventDirector : MonoBehaviour
{
    public static SegmentEventDirector Instance { get; private set; }

    private readonly List<SegmentEventRuntime> events = new();
    private readonly SegmentEventContext context = new();
    private RoundManager rounds;
    private GameManager game;

    // Boss ritmi durumu.
    private BossPoolSO bossPool;
    private int nextBossSegment;
    private SegmentEventSO previousBoss;
    private NoRuleBossSO fallbackBoss;

    public SegmentEventRuntime Active { get; private set; }
    public SegmentEventRuntime LastEnded { get; private set; }
    public int LastEndedRound { get; private set; }
    public IReadOnlyList<SegmentEventRuntime> Events => events;
    // Arayüzün yeniden çizmesi için: olay hazırlanınca, başlayınca, bitince ve temizlenince artar.
    public int Version { get; private set; }
    // Bu run boss ritminde mi (boss yalnız segmentin son round'unda aktif).
    public bool BossMode => bossPool != null;
    // Run'ın boss/ödül rastgelelik seed'i (profil 0 verdiyse run başında üretilir). Eski profillerde 0.
    public int RunSeed { get; private set; }
    // Son seçimde havuz kuralı esnedi mi: null, "tekrar" (başka uygun boss yoktu) ya da "yedek" (uygun boss yoktu).
    public string LastPickNote { get; private set; }

    // Duyurulmuş, henüz başlamamış ilk olay.
    public SegmentEventRuntime Upcoming
    {
        get
        {
            foreach (SegmentEventRuntime e in events)
                if (e.IsPrepared && !e.IsActive && !e.IsFinished) return e;
            return null;
        }
    }

    public SegmentEventRuntime ForSegment(int segment)
    {
        foreach (SegmentEventRuntime e in events) if (e.Segment == segment) return e;
        return null;
    }

    public static float SpawnIntervalMultiplier(GridPosition cell) =>
        Instance != null && Instance.Active != null ? Instance.Active.SpawnIntervalMultiplier(cell) : 1f;

    public static float SpawnHealthMultiplier(GridPosition cell) =>
        Instance != null && Instance.Active != null ? Instance.Active.SpawnHealthMultiplier(cell) : 1f;

    // Seed karıştırma: aynı (seed, segment, amaç) hep aynı değeri verir; 0 dönmez (0 "seed yok" demektir).
    public static int Mix(int seed, int segment, int salt)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)segment * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
            int value = (int)(h & 0x7FFFFFFF);
            return value == 0 ? 1 : value;
        }
    }

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += HandleRunStarted;
        rounds.OnRoundChanged += HandleRoundChanged;
        rounds.OnRoundEnded += HandleRoundEnded;
    }

    private void OnDisable()
    {
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundChanged -= HandleRoundChanged;
            rounds.OnRoundEnded -= HandleRoundEnded;
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        ClearAll();
        if (fallbackBoss != null) Destroy(fallbackBoss);
        if (Instance == this) Instance = null;
    }

    private void HandleRunStarted()
    {
        ClearAll();
        RunProfileSO profile = rounds.Profile;
        if (profile == null) return;
        if (profile.bossPool != null)
        {
            bossPool = profile.bossPool;
            RunSeed = profile.bossSeed != 0 ? profile.bossSeed : Mix(System.Environment.TickCount, System.DateTime.Now.Millisecond, 0x5345);
            nextBossSegment = 1;
            Version++;
            return;
        }
        foreach (SegmentEventEntry entry in profile.events)
            if (entry.segmentEvent != null && entry.segment >= 1)
                events.Add(entry.segmentEvent.CreateRuntime(SegmentEventTiming.WholeSegment(entry.segment, rounds.QuotaSegmentRounds)));
        events.Sort((a, b) => a.Segment.CompareTo(b.Segment));
        Version++;
    }

    private void HandleRoundChanged(int round)
    {
        PrepareDue();
        foreach (SegmentEventRuntime e in events)
        {
            if (e.IsActive || e.IsFinished || round != e.StartRound) continue;
            if (!e.IsPrepared) Prepare(e);
            e.Activate();
            Active = e;
            Version++;
        }
    }

    private void HandleRoundEnded()
    {
        if (Active == null || rounds.CurrentRound < Active.EndRound) return;
        Active.Finish();
        LastEnded = Active;
        LastEndedRound = rounds.CurrentRound;
        Active = null;
        Version++;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.RunComplete || state == GameStates.MainMenu) ClearAll();
    }

    // Tek temizlik yolu: olay sonu dışındaki bütün kapanışlar buradan geçer.
    public void ClearAll()
    {
        foreach (SegmentEventRuntime e in events) e.Finish();
        events.Clear();
        Active = null;
        LastEnded = null;
        LastEndedRound = 0;
        bossPool = null;
        nextBossSegment = 0;
        previousBoss = null;
        LastPickNote = null;
        RunSeed = 0;
        Version++;
    }

    private void LateUpdate()
    {
        if (game == null && GameManager.Instance != null)
        {
            game = GameManager.Instance;
            game.OnGameStateChanged += HandleStateChanged;
        }
        PrepareDue();
    }

    // Duyuru zamanı gelmiş boss'u seçer ve bölgesi seçilmemiş olayları hazırlar (grid açılışı Start'ta olduğu için ertelenebilir).
    private void PrepareDue()
    {
        if (rounds == null) return;
        GameStates state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameStates.MainMenu;
        if (state == GameStates.MainMenu || state == GameStates.RunComplete) return;
        if (bossPool != null) ChooseDueBosses();
        foreach (SegmentEventRuntime e in events)
            if (!e.IsPrepared && !e.IsFinished && rounds.CurrentRound >= e.AnnounceRound) Prepare(e);
    }

    // Boss, segmentinin ilk round'undan önce duyurulur: run başında (1. segment) ya da bir önceki boss round'u bittiği anda.
    private void ChooseDueBosses()
    {
        RunProfileSO profile = rounds.Profile;
        int segmentRounds = rounds.QuotaSegmentRounds;
        while (profile != null && profile.HasBoss(nextBossSegment))
        {
            int first = (nextBossSegment - 1) * segmentRounds + 1;
            bool due = rounds.CurrentRound >= first ||
                       (rounds.CurrentRound == first - 1 && !rounds.IsRoundActive && !rounds.IsPreparingFirstRound);
            if (!due || !BuildContext()) return;
            int segment = nextBossSegment;
            SegmentEventSO pick = bossPool.Pick(context, previousBoss, new System.Random(Mix(RunSeed, segment, 0x424F5353)), out bool repeated); // 'BOSS'
            LastPickNote = repeated ? "tekrar" : null;
            if (pick == null)
            {
                if (fallbackBoss == null) fallbackBoss = NoRuleBossSO.Create();
                pick = fallbackBoss;
                LastPickNote = "yedek";
            }
            SegmentEventRuntime e = pick.CreateRuntime(SegmentEventTiming.BossRound(segment, segmentRounds, Mix(RunSeed, segment, 0x5A4F4E45))); // 'ZONE'
            e.TryPrepare(context);
            events.Add(e);
            previousBoss = pick;
            nextBossSegment++;
            Version++;
        }
    }

    private void Prepare(SegmentEventRuntime e)
    {
        if (!BuildContext()) return;
        if (e.TryPrepare(context)) Version++;
    }

    private bool BuildContext()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || grid.GetGridSystem() == null) return false;
        context.GridWidth = grid.GetWidth();
        context.GridHeight = grid.GetHeight();
        context.OpenCells.Clear();
        context.ProductionPoints.Clear();
        GridSystem system = grid.GetGridSystem();
        for (int x = 0; x < context.GridWidth; x++)
        for (int z = 0; z < context.GridHeight; z++)
        {
            GridObject cell = system.GetGridObject(new GridPosition(x, z));
            GroundCell ground = cell?.GetGroundCellCached();
            if (ground != null && !ground.IsLocked) context.OpenCells.Add(new GridPosition(x, z));
        }
        foreach (PlantSpawner spawner in PlantSpawner.Active)
            if (spawner.GridObject != null) context.ProductionPoints.Add(spawner.GridObject.GetGridPosition());
        context.PlayerRadius = StatManager.Instance != null ? StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player) : 1f;
        return true;
    }
}
