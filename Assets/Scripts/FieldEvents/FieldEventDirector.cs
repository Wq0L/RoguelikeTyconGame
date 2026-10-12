using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Geç oyun tarla olaylarının yürütücüsü (Bölüm 3.8). RoundManager kurar; profil bir olay planı göstermiyorsa (eski profiller) ve
// demoda hiçbir şey yapmaz. Boss ödülü sistemiyle ilişkisi yoktur; boss kuralları (SegmentEventDirector) ayrıca sürer.
// Sorumluluğu zamanlamadır: round başında o round'un olayını seçer ve kurar, fazlarını oyun zamanıyla ilerletir, doğrudan
// saldırıları olaya iletir ve olayı tek yoldan temizler. Alan seçimi ve oynanış etkisi olayın kendi sınıfındadır (FieldEvent);
// görsel ayrı bir dinleyicidir (LateGamePresenter).
// - Aynı anda en çok bir tarla olayı: yeni olay ancak öncekinin temizliğinden sonra kurulur.
// - Temizlik (End): round sonu (kayıp ve zafer dahil), run sonu, ana menü, yeniden başlatma, devre dışı kalma. İdempotenttir.
// - Seçim akışı: run seed'i + round'dan türeyen ayrı bir System.Random. Boss seçimi ('BOSS'), bölge ('ZONE') ve ödül teklifi
//   ('RWDR') akışlarından ve UnityEngine.Random'dan bağımsızdır; onların tükettiği zarı değiştirmez.
// - Duraklatma: fazlar Time.deltaTime ile ilerler; oyun durduğunda (timeScale 0) olay da durur.
[DisallowMultipleComponent]
public sealed class FieldEventDirector : MonoBehaviour
{
    public static FieldEventDirector Instance { get; private set; }
    // Üretim noktaları her karede sorar: Unity nesnesi karşılaştırması yapmamak için düz referans.
    private static FieldZoneEffects activeZones;

    // İmleç arayüzün üstündeyken müdahale hedefi vurulmaz. Test bu soruyu değiştirebilir.
    public static Func<bool> PointerOverUi = DefaultPointerOverUi;
    private static bool DefaultPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private sealed class Listener : DirectAttackListener
    {
        public FieldEventDirector host;
        public override void End(DirectAttackReport attack)
        {
            if (host.Current != null) host.Current.OnDirectAttack(attack, PointerOverUi != null && PointerOverUi());
        }
    }

    private readonly FieldZoneEffects zones = new();
    private readonly FieldSnapshot snapshot = new();
    private readonly Listener listener = new();
    private RoundManager rounds;
    private GameManager game;
    private bool listening;
    private FieldEventKind? previous;

    public FieldEvent Current { get; private set; }
    public FieldZoneEffects Zones => zones;
    // Arayüz: olay kurulunca, faz değişince ve temizlenince artar.
    public int Version { get; private set; }
    public int Started { get; private set; }
    // Seçildiği hâlde kurulamayan olay (uygun bölge yok).
    public int Skipped { get; private set; }
    public string PlanError { get; private set; }

    // Bu run'ın olay planı (yoksa null): profil vermediyse ya da demo ise olay yok.
    public FieldEventPlanSO Plan =>
        rounds != null && rounds.Profile != null && !DemoSceneSettings.IsDemo ? rounds.Profile.fieldEvents : null;

    public static bool BlocksSpawn(GridPosition cell) => activeZones != null && activeZones.Blocks(cell);
    public static float SpawnIntervalMultiplier(GridPosition cell) => activeZones != null ? activeZones.Slow(cell) : 1f;

    public static WanderingBountyEvent BountyAt(GridPosition cell)
    {
        var bounty=Instance!=null?Instance.Current as WanderingBountyEvent:null;
        return bounty!=null&&bounty.Benefits(cell)?bounty:null;
    }

    // Sunum olayı (gözlem): görsel, ses ve test dinler. Dinleyicinin hatası ya da yokluğu oynanışı etkilemez.
    private static Action<FieldEventCue>[] cueObservers = Array.Empty<Action<FieldEventCue>>();
    public static event Action<FieldEventCue> Cued
    {
        add => ObserverEvents.Add(ref cueObservers, value);
        remove => ObserverEvents.Remove(ref cueObservers, value);
    }
    internal void Emit(FieldEventCue cue)
    {
        Version++;
        if (cueObservers.Length > 0) ObserverEvents.Raise(cueObservers, cue);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeZones = null;
        PointerOverUi = DefaultPointerOverUi;
    }

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
        listener.host = this;
        activeZones = zones;
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += HandleRunStarted;
        rounds.OnRoundChanged += HandleRoundStarted;
        rounds.OnRoundEnded += End;
    }

    private void OnDisable()
    {
        End();
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundChanged -= HandleRoundStarted;
            rounds.OnRoundEnded -= End;
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        End();
        if (activeZones == zones) activeZones = null;
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu || state == GameStates.RunComplete) End();
    }

    private void HandleRunStarted()
    {
        End();
        previous = null;
        Started = Skipped = 0;
        PlanError = FieldEventPlanSO.Validate(Plan);
        if (PlanError != null)
            Debug.LogError($"Tarla olayları bu run'da kapalı: plan geçersiz — {PlanError}. Plan otomatik düzeltilmez.", Plan);
    }

    private void HandleRoundStarted(int round)
    {
        End();
        FieldEventPlanSO plan = Plan;
        if (plan == null || PlanError != null) return;
        int seed = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.RunSeed : 0;
        var rng = new System.Random(SegmentEventDirector.Mix(seed, round, 0x46455654)); // 'FEVT'
        if (!plan.TryPick(round, rounds.IsBossRound(round), rng, previous, out FieldEventKind kind)) return;
        TryStart(kind, plan, rng, plan.startDelay);
    }

    private static FieldEvent Create(FieldEventKind kind, FieldEventPlanSO plan) => kind switch
    {
        FieldEventKind.ThornSiege => new ThornSiegeEvent(plan.thornSiege),
        FieldEventKind.InvasiveSprout => new InvasiveSproutEvent(plan.invasiveSprout),
        FieldEventKind.GraftedShell => new GraftedShellEvent(plan.graftedShell),
        _ => new WanderingBountyEvent(plan.wanderingBounty),
    };

    private bool TryStart(FieldEventKind kind, FieldEventPlanSO plan, System.Random rng, float delay)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || !snapshot.Read()) { Skipped++; return false; }
        zones.Resize(grid.GetWidth(), grid.GetHeight());
        FieldEvent created = Create(kind, plan);
        if (!created.Begin(this, rng, delay, snapshot)) { Skipped++; return false; }
        Current = created;
        previous = kind;
        Started++;
        Listen(true);
        Version++;
        return true;
    }

    // Yalnız Editor Play testi ve otomatik test: aktif round'da bir olayı hemen kurar (varsa öncekini temizleyerek). Normal
    // takvimi, kaydı ve profil seçimini değiştirmez. plan verilmezse profilin planı kullanılır.
    public bool StartNow(FieldEventKind kind, float delay = 0f, int seed = 1, FieldEventPlanSO plan = null)
    {
        End();
        plan ??= Plan;
        if (plan == null || !RoundRunning) return false;
        return TryStart(kind, plan, new System.Random(seed), delay);
    }

    private bool RoundRunning => rounds != null && rounds.IsRoundActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;

    private void Update()
    {
        if (Current == null || !RoundRunning) return;
        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        FieldEventPhase before = Current.Phase;
        Current.Tick(delta);
        if (Current.Phase != before) Version++;
        if (Current.Phase == FieldEventPhase.Done) End();
    }

    // Tek temizlik yolu. Olayın kendi etkileri kaynağına göre kalkar; ardından bütün üretim etkileri sıfırlanır (güvenlik).
    public void End()
    {
        if (Current != null)
        {
            FieldEvent ending = Current;
            Current = null;
            ending.Cleanup();
        }
        zones.Clear();
        Listen(false);
        Version++;
    }

    private void Listen(bool wanted)
    {
        if (wanted == listening) return;
        listening = wanted;
        if (wanted) AttackFeed.Add(listener);
        else AttackFeed.Remove(listener);
    }
}
