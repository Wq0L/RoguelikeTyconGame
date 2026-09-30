using System.Collections.Generic;
using UnityEngine;

// Segment olaylarını (boss) yürütür. RoundManager kurar; olayları aktif run profilinden alır.
// - Duyuru: olayın bir önceki segmentinin başında bölge bir kez seçilir (ilk segment için run başında).
// - Segmentin ilk round'u başlarken etkinleşir, son round'u bitince kalkar.
// - Run sonu, ana menü ve yok edilmede (sahne değişimi, yeniden başlatma) bütün olaylar aynı yoldan temizlenir.
// Oynanış yalnız SpawnIntervalMultiplier üzerinden etkilenir; arayüz sadece okur.
[DisallowMultipleComponent]
public sealed class SegmentEventDirector : MonoBehaviour
{
    public static SegmentEventDirector Instance { get; private set; }

    private readonly List<SegmentEventRuntime> events = new();
    private readonly SegmentEventContext context = new();
    private RoundManager rounds;
    private GameManager game;

    public SegmentEventRuntime Active { get; private set; }
    public SegmentEventRuntime LastEnded { get; private set; }
    public int LastEndedRound { get; private set; }
    public IReadOnlyList<SegmentEventRuntime> Events => events;
    // Arayüzün yeniden çizmesi için: olay hazırlanınca, başlayınca, bitince ve temizlenince artar.
    public int Version { get; private set; }

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

    public static float SpawnIntervalMultiplier(GridPosition cell) =>
        Instance != null && Instance.Active != null ? Instance.Active.SpawnIntervalMultiplier(cell) : 1f;

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
        if (Instance == this) Instance = null;
    }

    private void HandleRunStarted()
    {
        ClearAll();
        RunProfileSO profile = rounds.Profile;
        if (profile == null) return;
        foreach (SegmentEventEntry entry in profile.events)
            if (entry.segmentEvent != null && entry.segment >= 1)
                events.Add(entry.segmentEvent.CreateRuntime(entry.segment, rounds.QuotaSegmentRounds));
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

    // Duyuru zamanı gelmiş ama bölgesi seçilmemiş olayları hazırlar (grid açılışı Start'ta olduğu için ertelenebilir).
    private void PrepareDue()
    {
        if (rounds == null || events.Count == 0) return;
        GameStates state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameStates.MainMenu;
        if (state == GameStates.MainMenu || state == GameStates.RunComplete) return;
        foreach (SegmentEventRuntime e in events)
            if (!e.IsPrepared && !e.IsFinished && rounds.CurrentRound >= e.AnnounceRound) Prepare(e);
    }

    private void Prepare(SegmentEventRuntime e)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || grid.GetGridSystem() == null) return;
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
        foreach (PlantSpawner spawner in FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None))
            if (spawner.GridObject != null) context.ProductionPoints.Add(spawner.GridObject.GetGridPosition());
        if (e.TryPrepare(context)) Version++;
    }
}
