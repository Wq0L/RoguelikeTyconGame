using System;
using System.Collections.Generic;
using UnityEngine;

// Alınmış boss ödülü etkilerinin yürütücüsü (Bölüm 3.8). RoundManager kurar; etki yokken hiçbir şey yapmaz ve hiçbir yeri
// dinlemez. Sorumluluğu yalnız yürütmedir: etkileri kurar, kancalarını çağırır, ortak vuruşu ve gecikmeli işleri taşır, temizler.
// Hangi ödülün sunulacağı ve alınacağı BossRewardManager'dadır; her etkinin kuralı kendi sınıfındadır.
// - Vuruş (Strike): mevcut hasar yolu (PlantHealth.TakeDamage), tür DamageType.Reward, kaynak vuruşun bağlamında. Kritik zarı,
//   doğrudan vuruş çarpanları ve saksı bonusu yoktur; ölüm, ödül ve hasat bir kez ve eski yoldan verilir. Vuruş kök taşımaz:
//   öldürdüğü bitki Zincir Hasat'ı başlatmaz, doğrudan hasat sayaçlarına (Hasat Ritmi, Açgözlü Hasat, Hilal) girmez.
// - Gecikmeli işler: kalan oyun süresiyle sayılır (BehaviorEchoes ile aynı kural), sonlu bir kuyruktadır (Capacity); kuyruk
//   doluysa iş düşer ve sayılır. Round sonu, run sonu, menü ve devre dışı kalmada bütün işler uygulanmadan düşer.
// - Rastgelelik: run seed'i + round'dan türeyen ayrı bir akış (ChainRandom). UnityEngine.Random'a ve boss ödülü teklif akışına
//   dokunmaz.
// - Sunum: Cued olayı (gözlem). Dinleyicinin hatası ya da yokluğu oynanışı etkilemez.
[DisallowMultipleComponent]
public sealed class RunEffects : MonoBehaviour
{
    public const int TimerCapacity = 128;
    private const float DelayTolerance = 1e-4f;
    private const int Sources = 16;

    public static RunEffects Instance { get; private set; }

    private struct Timer
    {
        public float remaining;
        public int frame, kind, a, b, c;
        public RunEffect owner;
    }

    private sealed class Listener : DirectAttackListener
    {
        public RunEffects host;
        public override void Begin(DirectAttackReport attack, ref float multiplier)
        {
            List<RunEffect> list = host.effects;
            for (int i = 0; i < list.Count; i++) list[i].AttackBegin(attack, ref multiplier);
        }
        public override void End(DirectAttackReport attack)
        {
            List<RunEffect> list = host.effects;
            for (int i = 0; i < list.Count; i++) list[i].AttackEnd(attack);
        }
    }

    private readonly List<RunEffect> effects = new();
    private readonly List<Timer> timers = new(TimerCapacity);
    private readonly int[] hits = new int[Sources], kills = new int[Sources];
    private readonly Listener listener = new();
    private RoundManager rounds;
    private GameManager game;
    private FieldBounds bounds;
    private bool hasBounds, listening;
    private int boundsFrame = -1;
    private uint random = 0x6D2B79F5u;

    public IReadOnlyList<RunEffect> Active => effects;
    // Arayüz ve test penceresi için: etki eklenince, kalkınca ve round sınırlarında artar.
    public int Version { get; private set; }
    // Round içinde geçen oyun süresi (sn): bekleme süreleri bununla karşılaştırılır. Round başında sıfırlanır.
    public double Clock { get; private set; }
    public int PendingTimers => timers.Count;
    public int DroppedTimers { get; private set; }
    public int Hits(RewardSource source) => hits[(int)source];
    public int Kills(RewardSource source) => kills[(int)source];

    // Sunum olayı (gözlem): görsel, ses ve test dinler. Oynanış buna bağlı değildir.
    private static Action<RewardCue>[] cueObservers = Array.Empty<Action<RewardCue>>();
    public static event Action<RewardCue> Cued
    {
        add => ObserverEvents.Add(ref cueObservers, value);
        remove => ObserverEvents.Remove(ref cueObservers, value);
    }
    public void Emit(RewardCue cue)
    {
        if (cueObservers.Length > 0) ObserverEvents.Raise(cueObservers, cue);
    }

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
        listener.host = this;
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += HandleRunStarted;
        rounds.OnRoundChanged += HandleRoundStarted;
        rounds.OnRoundEnded += HandleRoundEnded;
    }

    private void OnDisable()
    {
        HandleRoundEnded();
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundChanged -= HandleRoundStarted;
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

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu) ClearAll();
        else if (state == GameStates.RunComplete) HandleRoundEnded();
    }

    private void HandleRunStarted()
    {
        ClearAll();
        Array.Clear(hits, 0, hits.Length);
        Array.Clear(kills, 0, kills.Length);
        DroppedTimers = 0;
        DirectAttackPause.ResetCounters();
    }

    private void HandleRoundStarted(int round)
    {
        Clock = 0d;
        boundsFrame = -1;
        int seed = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.RunSeed : 0;
        random = ChainRandom.Seed(SegmentEventDirector.Mix(seed, round, 0x52574658)); // 'RWFX'
        for (int i = 0; i < effects.Count; i++) effects[i].OnRoundStarted();
        Version++;
    }

    // Round sonu temizliği (idempotent): bekleyen işler uygulanmadan düşer, saldırı duraklaması kalkar, etkiler geçici durumlarını
    // bırakır. Alınmış etkiler run boyunca kalır.
    private void HandleRoundEnded()
    {
        DropTimers();
        DirectAttackPause.Clear();
        for (int i = 0; i < effects.Count; i++) effects[i].OnRoundEnded();
        Version++;
    }

    private void DropTimers()
    {
        DroppedTimers += timers.Count;
        timers.Clear();
    }

    private bool RoundRunning => rounds != null && rounds.IsRoundActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;

    private void Update()
    {
        if (effects.Count == 0) return;
        if (!RoundRunning) { if (timers.Count > 0) DropTimers(); return; }
        float delta = Time.deltaTime;
        if (delta <= 0f) return;   // duraklatma: süreler ve işler bekler
        Clock += delta;
        int frame = Time.frameCount;
        // Yürütülen iş yeni iş doğurabilir (listenin sonuna): döngü onu da görür; planlandığı karenin süresi sayılmaz.
        for (int i = 0; i < timers.Count;)
        {
            Timer timer = timers[i];
            if (timer.frame != frame) timer.remaining -= delta;
            if (timer.remaining > DelayTolerance) { timers[i] = timer; i++; continue; }
            timers.RemoveAt(i);
            timer.owner.OnTimer(timer.kind, timer.a, timer.b, timer.c);
        }
        for (int i = 0; i < effects.Count; i++) effects[i].Tick(delta);
    }

    // ---------------------------------------------------------------- etkiler
    public T Find<T>() where T : RunEffect
    {
        for (int i = 0; i < effects.Count; i++) if (effects[i] is T match) return match;
        return null;
    }

    public bool Has(RunEffectSO data)
    {
        for (int i = 0; i < effects.Count; i++) if (effects[i].Data == data) return true;
        return false;
    }

    // Etki şu an işlev görebilir mi (teklif ve alım denetimi). Yerleşim isteyen etkide en az bir geçerli konum olmalıdır.
    public bool CanTake(RunEffectSO data) => data != null && !Has(data) && data.CanTake(this);

    // Tek işlem: etki kurulur ya da hiçbir şey değişmez. Aynı etki ikinci kez eklenmez.
    public bool TryAdd(RunEffectSO data, int placement)
    {
        if (data == null || Has(data) || !data.CanTake(this)) return false;
        if (data.NeedsPlacement && !data.IsValidPlacement(this, placement)) return false;
        RunEffect effect = data.Create();
        effect.Data = data;
        effect.Host = this;
        bool added;
        try { added = effect.OnAdded(placement); }
        catch { effect.OnRemoved(); throw; }
        if (!added) { effect.OnRemoved(); return false; }
        effects.Add(effect);
        Listen(true);
        Version++;
        return true;
    }

    // Ödül uygulaması yarıda kalırsa geri alma (BossRewardManager): etkinin kendi eklediği her şey kaynağına göre kalkar.
    public void Remove(RunEffectSO data)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Data != data) continue;
            RunEffect effect = effects[i];
            effects.RemoveAt(i);
            DropTimersOf(effect);
            effect.OnRemoved();
        }
        if (effects.Count == 0) Listen(false);
        Version++;
    }

    // Tek temizlik yolu (yeni run, ana menü, yok edilme). İki kez çağrılabilir.
    public void ClearAll()
    {
        timers.Clear();
        // Sondan başa: yükseltme etkisi, kurulduğu aile etkisinden önce kalkar.
        for (int i = effects.Count - 1; i >= 0; i--) effects[i].OnRemoved();
        effects.Clear();
        Listen(false);
        DirectAttackPause.Clear();
        Clock = 0d;
        Version++;
    }

    private void DropTimersOf(RunEffect effect)
    {
        for (int i = timers.Count - 1; i >= 0; i--)
            if (timers[i].owner == effect) { timers.RemoveAt(i); DroppedTimers++; }
    }

    private void Listen(bool wanted)
    {
        if (wanted == listening) return;
        listening = wanted;
        if (wanted) { AttackFeed.Add(listener); AttackFeed.BehaviorTriggered += HandleBehavior; }
        else { AttackFeed.Remove(listener); AttackFeed.BehaviorTriggered -= HandleBehavior; }
    }

    private void HandleBehavior(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position)
    {
        for (int i = 0; i < effects.Count; i++) effects[i].BehaviorTriggered(planter, type, cell, position);
    }

    // ---------------------------------------------------------------- ortak araçlar
    public GridSystem Grid => FieldCells.Grid;

    // Açık alanın sınırları: kare başına bir kez okunur (tarla round arasında büyüyebilir).
    public bool TryBounds(out FieldBounds result)
    {
        if (boundsFrame != Time.frameCount)
        {
            boundsFrame = Time.frameCount;
            hasBounds = FieldCells.TryBounds(out bounds);
        }
        result = bounds;
        return hasBounds;
    }

    // Etki hasarının tabanı: oyuncunun hasat hasarı stat'ı × etkinin katsayısı (en az 1). Davranışların tabanıyla aynı stat'tır;
    // doğrudan vuruş çarpanları, kritik ve davranış çarpanı (Yıkım Gücü, uzmanlaşma, rezonans) uygulanmaz.
    public int StatDamage(float coefficient)
    {
        float stat = StatManager.Instance != null ? StatManager.Instance.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) : 0f;
        return NumericSafety.ToInt(stat * (double)coefficient, 1, NumericSite.RewardDamage);
    }

    // Hesaplanmış bir hasarın oranı (tek yuvarlama). minimum 0: oran hasarı bitirebilir (Taşan Hasar aktarımı böyle durur).
    public static int Scale(int damage, float ratio, int minimum = 1) =>
        NumericSafety.ToInt(damage * (double)Mathf.Max(0f, ratio), minimum, NumericSite.RewardDamage);

    public StrikeResult Strike(GridPosition cell, int damage, RewardSource source)
    {
        PlantHealth plant = damage > 0 ? FieldCells.LivingPlant(Grid, cell) : null;
        if (plant == null) return default;
        PlanterBrain owner = plant.Owner;
        Vector3 point = plant.transform.position;
        plant.TakeDamage(damage, DamageType.Reward, false, 1f, HarvestLink.Reward(source));
        hits[(int)source]++;
        bool killed = plant.IsDead;
        if (killed) kills[(int)source]++;
        VFXManager.Instance?.PlayHit(point, damage, false, source == RewardSource.Overflow ? DamageTextKind.Overflow : DamageTextKind.Default);
        return new StrikeResult(true, killed, killed ? plant.Overkill : 0, owner, point);
    }

    public bool Schedule(RunEffect owner, float delay, int kind, int a = 0, int b = 0, int c = 0)
    {
        if (owner == null || !RoundRunning) return false;
        if (timers.Count >= TimerCapacity) { DroppedTimers++; return false; }
        timers.Add(new Timer { remaining = Mathf.Max(0f, delay), frame = Time.frameCount, kind = kind, a = a, b = b, c = c, owner = owner });
        return true;
    }

    // [0, 1) — etkilerin ortak, tekrar üretilebilir zar akışı.
    public float Roll() => ChainRandom.Value(ref random);
    // Bir davranışa ayrı akış (kasırganın yolu, bumerangın yönü); hiçbir zaman 0 değildir.
    public uint Fork() => ChainRandom.Fork(ref random);

    // Saksının en yüksek davranış şansı (0: davranışı yok).
    public static float BestBehaviorChance(PlanterBrain planter)
    {
        if (planter == null) return 0f;
        return Mathf.Max(Mathf.Max(planter.GetFinalStat(StatType.ExplosionChance), planter.GetFinalStat(StatType.TornadoChance)),
            Mathf.Max(planter.GetFinalStat(StatType.BoomerangChance), planter.GetFinalStat(StatType.ElectricChance)));
    }
}

public readonly struct StrikeResult
{
    public readonly bool Hit, Killed;
    public readonly int Overkill;
    public readonly PlanterBrain Owner;
    public readonly Vector3 Point;
    public StrikeResult(bool hit, bool killed, int overkill, PlanterBrain owner, Vector3 point)
    { Hit = hit; Killed = killed; Overkill = overkill; Owner = owner; Point = point; }
}

// Sunum için olay türleri (görsel / ses bağlama noktaları). Oynanış bunlara bağlı değildir.
public enum RewardCueKind
{
    AntennaPlaced, AntennaRemoved, AntennaPulse, LinePulse, LineExtraPulse,
    CrescentLaunch, CrescentStep, CrescentGrow, CrescentReset, FullMoon,
    CritArc, HarvestEcho, Overflow, CritBurst,
    ReactorOn, ReactorOff, GreedReady, GreedSpent, GreedLost, Overload,
    AntennaContact, LineContact, LineExtraContact, CircuitReturn,
    MoonContact, MoonReturnContact,
    HarvestEchoPrepared,
    OverloadPrepared, OverloadCancelled,
}

// From / To: dünya konumları (tek noktalı olayda ikisi aynı). Value: olaya göre (büyüme düzeyi, hücre sayısı, konum kimliği …).
public readonly struct RewardCue
{
    public readonly RewardCueKind Kind;
    public readonly Vector3 From, To;
    public readonly int Value;
    public readonly float Radius; // Snapshot of the actual CritBurst damage radius; zero for other cues.
    public readonly int TransferStep; // Overflow presentation only, zero-based; damage stays in Value.
    public RewardCue(RewardCueKind kind, Vector3 from, Vector3 to, int value = 0, int transferStep = 0, float radius = 0f) { Radius = radius; Kind = kind; From = from; To = to; Value = value; TransferStep = transferStep; }
    public RewardCue(RewardCueKind kind, Vector3 at, int value = 0, float radius = 0f) : this(kind, at, at, value, radius: radius) { }
}
