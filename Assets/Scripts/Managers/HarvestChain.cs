using System.Collections.Generic;
using UnityEngine;

// Davranış zinciri (Bölüm 3.7.6, Zincir Hasat ödülü). RoundManager kurar; ödül alınmadıysa hiçbir şey yapmaz. Zincirin bütün
// kuralları burada; davranışlar (PlanterBrain, TornadoManager, HarvestBehaviorManager) yalnız kendi mevcut hasar yollarını
// bağlamla (HarvestLink) ve hasar çarpanıyla çalıştırır.
// - Kök: oyuncunun her doğrudan saldırısı (PlayerController.AttackInRadius) bir kök açar. Kök bağlamı saldırı bitince, ona bağlı
//   bekleyen iş ve canlı kasırga / bumerang kalmayınca havuza döner (referans sayımı: BeginRoot / EndRoot, işler, Hold / Release).
// - Nesil: doğrudan hasadın normal tetiği nesil 0'dır (eski kural, eski şans zarı). Nesil g davranışının öldürdüğü bitkinin kendi
//   saksısı g+1. nesil olarak denenir; ek nesil sayısı ödül verisindedir. Artçı / ikinci dalga vuruşları zincire katılmaz.
// - Tekrar sınırı: aynı kökte aynı saksı + davranış çifti en çok bir kez denenir (başarısız deneme de hakkı tüketir); kökte normal
//   tetikle gerçekten çalışmış çiftler de ziyaret edilmiş sayılır (MarkTriggered). İşaret şans zarından ve kuyruktan önce konur.
//   Çok hücreli saksı tek saksıdır. Normal doğrudan tetik bu kayıttan etkilenmez.
// - Şans ve hasar: saksının gerçek şansı × neslin şans çarpanı; tetiklenen saksının normal davranış hasarı × neslin hasar çarpanı
//   (önceki neslin çarpanıyla birikmez). Zar ayrı ve tekrar üretilebilir bir akıştan gelir (run seed + kök sırası);
//   UnityEngine.Random'a dokunmaz. Zincirden doğan kasırganın yolu ve bumerangın yönü de bu akıştan.
// - Kuyruk: başarılı tetikler kuyruğa girer, doğdukları kareden sonraki karelerde sırayla işlenir (ölüm olayından iç içe davranış
//   çağrısı yok). Kare başına en çok JobsPerFrame iş; kök başına en çok RootBudget başarılı ek tetik (bütçe dolunca zar atılmaz).
//   Kasırga / bumerang havuzu doluysa iş yerinde bekler (zar yeniden atılmaz, bütçe yeniden tüketilmez); arkasındaki işler beklemez.
// - Temizlik: round sonu, run başı, menü / run sonu ve devre dışı kalma: bütün işler düşer, kökler bırakılır. Eski kökün
//   kimliği yeniden kullanılmaz; havuzdan dönen nesne (bitki, kasırga, bumerang) bağlamını kendi sıfırlar.
// - Görsel: zincirle çalışan davranış kendi mevcut görselini oynatır; tetiklenen saksının ayak izi kısa, mor bir çizgi alır
//   (AreaOutlineFeedback.PlayChainSource). Normal tetikte bu çizgi yoktur.
[DisallowMultipleComponent]
public sealed class HarvestChain : MonoBehaviour
{
    public static HarvestChain Instance { get; private set; }

    // Ret nedenleri (ölçüm). Excluded: artçı / ikinci dalga öldürmesi.
    public enum Reject { Repeat, Generation, Budget, InvalidSource, RoundEnd, Excluded }
    public const int RejectKinds = 6, MaxGenerations = 4;
    private static readonly DamageType[] Order = { DamageType.Explosion, DamageType.Tornado, DamageType.Boomerang, DamageType.Electric };

    // Test ve ölçüm için olay: zar denemesi, yürütülen iş, ret. Oynanış buna bağlı değildir.
    public enum TraceKind { Attempt, Fired, Rejected, Waiting }
    public readonly struct Trace
    {
        public readonly TraceKind Kind;
        public readonly int Root, Generation;
        public readonly PlanterBrain Planter;
        public readonly DamageType Type;
        public readonly float Chance;
        public readonly bool Success;
        public readonly Reject Reason;
        public Trace(TraceKind kind, int root, int generation, PlanterBrain planter, DamageType type, float chance, bool success, Reject reason)
        { Kind = kind; Root = root; Generation = generation; Planter = planter; Type = type; Chance = chance; Success = success; Reason = reason; }
    }
    private static System.Action<Trace>[] traceObservers = System.Array.Empty<System.Action<Trace>>();
    // Gözlem olayı (ObserverEvents): dinleyicinin hatası kök tutumunu, kuyruğu ve sayaçları yarıda bırakmaz.
    public static event System.Action<Trace> Traced
    {
        add => ObserverEvents.Add(ref traceObservers, value);
        remove => ObserverEvents.Remove(ref traceObservers, value);
    }
    private static void Emit(TraceKind kind, int root, int generation, PlanterBrain planter, DamageType type, float chance, bool success, Reject reason)
    {
        if (traceObservers.Length > 0) ObserverEvents.Raise(traceObservers, new Trace(kind, root, generation, planter, type, chance, success, reason));
    }

    private sealed class Root
    {
        public int id, refs, successes;
        public uint random;
        public readonly HashSet<long> visited = new();
    }

    private struct Job
    {
        public int root, generation, run, round, frame;
        public float time;
        public PlanterBrain planter;
        public DamageType type;
        public GridObject cell;
        public Vector3 position;
        public bool waited;
    }

    private enum Outcome { Fired, Waiting, Invalid }

    private readonly Dictionary<int, Root> roots = new();
    private readonly Stack<Root> spare = new();
    private readonly List<Job> jobs = new();
    private readonly List<GridPosition> footprint = new();
    private RoundManager rounds;
    private GameManager game;
    private HarvestChainConfig config;
    private int nextRoot, rootOrdinal, runSerial;

    // Sayaçlar (run başında sıfırlanır). Dizin: ek nesil (1 …).
    private readonly int[] attempts = new int[MaxGenerations + 1], failures = new int[MaxGenerations + 1], triggers = new int[MaxGenerations + 1], fired = new int[MaxGenerations + 1];
    private readonly int[] rejected = new int[RejectKinds];
    public int Attempts(int generation) => attempts[Mathf.Clamp(generation, 0, MaxGenerations)];
    public int Failures(int generation) => failures[Mathf.Clamp(generation, 0, MaxGenerations)];
    public int Triggers(int generation) => triggers[Mathf.Clamp(generation, 0, MaxGenerations)];
    public int Fired(int generation) => fired[Mathf.Clamp(generation, 0, MaxGenerations)];
    public int Rejected(Reject reason) => rejected[(int)reason];
    public int Pending => jobs.Count;
    public int ActiveRoots => roots.Count;
    public int MaxPending { get; private set; }
    // Havuz dolu olduğu için en az bir kare bekleyen iş ve toplam bekleme karesi.
    public int PoolWaitJobs { get; private set; }
    public int PoolWaitFrames { get; private set; }
    // Kuyruk gecikmesi: işin doğduğu kareden yürütüldüğü kareye.
    public long DelayFramesTotal { get; private set; }
    public int DelayFramesMax { get; private set; }
    public float DelaySecondsMax { get; private set; }

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += HandleRunStarted;
        rounds.OnRoundEnded += HandleRoundEnded;
        rounds.OnRoundChanged += HandleRoundChanged;
    }

    private void OnDisable()
    {
        Clear(Reject.RoundEnd);
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundEnded -= HandleRoundEnded;
            rounds.OnRoundChanged -= HandleRoundChanged;
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleRunStarted()
    {
        Clear(Reject.RoundEnd);
        runSerial++;
        rootOrdinal = 0;
        System.Array.Clear(attempts, 0, attempts.Length);
        System.Array.Clear(failures, 0, failures.Length);
        System.Array.Clear(triggers, 0, triggers.Length);
        System.Array.Clear(fired, 0, fired.Length);
        System.Array.Clear(rejected, 0, rejected.Length);
        MaxPending = PoolWaitJobs = PoolWaitFrames = DelayFramesMax = 0;
        DelayFramesTotal = 0;
        DelaySecondsMax = 0f;
    }

    private void HandleRoundEnded() => DropJobs(Reject.RoundEnd);

    // Kaynak vurgusunun çizgi havuzu round başında kurulur: ilk zincir tetiğinde tek seferlik takılma olmasın (yalnız görsel).
    private void HandleRoundChanged(int round)
    {
        if (BossRewardManager.TryGetChain(out _)) AreaOutlineFeedback.Prewarm();
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu || state == GameStates.RunComplete) Clear(Reject.RoundEnd);
    }

    private bool RoundRunning => rounds != null && rounds.IsRoundActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;

    // Bütün işler düşer, bütün kökler bırakılır (kökü tutan efekt kalmışsa bırakırken kökü bulamaz; kimlikler yeniden kullanılmaz).
    private void Clear(Reject reason)
    {
        DropJobs(reason);
        foreach (Root root in roots.Values) Recycle(root);
        roots.Clear();
    }

    private void DropJobs(Reject reason)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            rejected[(int)reason]++;
            Release(jobs[i].root);
        }
        jobs.Clear();
    }

    private void Recycle(Root root)
    {
        root.visited.Clear();
        root.refs = root.successes = 0;
        spare.Push(root);
    }

    // ---------------------------------------------------------------- kök
    // Doğrudan saldırının başında: ödül yoksa ya da round dışında 0 (bağlam açılmaz, hiçbir şey sayılmaz).
    public static int BeginRoot() => Instance != null ? Instance.Open() : 0;
    // Doğrudan saldırının sonunda: saldırının kök üzerindeki tutumu bırakılır.
    public static void EndRoot(int root) => Release(root);

    // Gecikmeli vuran efekt (kasırga, bumerang) kökü yaşadığı sürece tutar; tutabildiyse true (bırakırken Release çağrılmalı).
    public static bool Hold(int root)
    {
        if (root == 0 || Instance == null || !Instance.roots.TryGetValue(root, out Root context)) return false;
        context.refs++;
        return true;
    }

    public static void Release(int root)
    {
        if (root == 0 || Instance == null || !Instance.roots.TryGetValue(root, out Root context)) return;
        if (--context.refs > 0) return;
        Instance.roots.Remove(root);
        Instance.Recycle(context);
    }

    // Normal doğrudan tetikle gerçekten çalışan saksı + davranış: bu kökte zincir onu yeniden çalıştırmaz. Normal tetiği engellemez.
    public static void MarkTriggered(int root, PlanterBrain planter, DamageType type)
    {
        if (root == 0 || planter == null || Instance == null || !Instance.roots.TryGetValue(root, out Root context)) return;
        context.visited.Add(Key(planter, type));
    }

    private int Open()
    {
        if (!BossRewardManager.TryGetChain(out config) || !RoundRunning) return 0;
        Root root = spare.Count > 0 ? spare.Pop() : new Root();
        root.id = ++nextRoot;
        root.refs = 1;
        root.successes = 0;
        root.visited.Clear();
        int seed = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.RunSeed : 0;
        root.random = ChainRandom.Seed(SegmentEventDirector.Mix(seed, ++rootOrdinal, 0x5A4E4352)); // 'ZNCR'
        roots.Add(root.id, root);
        return root.id;
    }

    private static long Key(PlanterBrain planter, DamageType type) => ((long)planter.GetInstanceID() << 8) | (uint)type;

    // ---------------------------------------------------------------- ölüm: zincir kararı
    // Davranışla ölen bitki (PlanterBrain.TriggerHarvestBehaviors çağırır). Kararlar burada verilir; başarılı tetik kuyruğa girer.
    public void OnBehaviorHarvest(PlanterBrain planter, GridObject cell, PlantHealth plant)
    {
        if (plant == null || !BossRewardManager.TryGetChain(out config)) return;
        HarvestLink link = plant.KillLink;
        if (link.Echo) { Refuse(Reject.Excluded, link.Root, link.Generation + 1, planter); return; }
        if (link.Root == 0) return;
        int generation = link.Generation + 1;
        if (!roots.TryGetValue(link.Root, out Root root) || !RoundRunning) { Refuse(Reject.InvalidSource, link.Root, generation, planter); return; }
        if (generation > config.Generations) { Refuse(Reject.Generation, link.Root, generation, planter); return; }
        if (planter == null || !planter.IsHarvestSource(cell, plant)) { Refuse(Reject.InvalidSource, link.Root, generation, planter); return; }
        for (int i = 0; i < Order.Length; i++) Consider(root, generation, planter, Order[i], cell, plant.transform.position);
    }

    private void Consider(Root root, int generation, PlanterBrain planter, DamageType type, GridObject cell, Vector3 position)
    {
        float chance = planter.GetFinalStat(ChanceStat(type));
        if (chance <= 0f) return;   // davranışsız saksıya yeni davranış verilmez
        long key = Key(planter, type);
        if (root.visited.Contains(key)) { Refuse(Reject.Repeat, root.id, generation, planter, type); return; }
        if (root.successes >= config.RootBudget) { Refuse(Reject.Budget, root.id, generation, planter, type); return; }   // zar atılmaz
        root.visited.Add(key);
        float effective = chance * config.ChanceFactor(generation);
        bool success = ChainRandom.Value(ref root.random) < effective;
        attempts[generation]++;
        Emit(TraceKind.Attempt, root.id, generation, planter, type, effective, success, default);
        if (!success) { failures[generation]++; return; }
        root.successes++;
        root.refs++;
        triggers[generation]++;
        jobs.Add(new Job
        {
            root = root.id, generation = generation, run = runSerial, round = rounds.CurrentRound, frame = Time.frameCount, time = Time.time,
            planter = planter, type = type, cell = cell, position = position,
        });
        if (jobs.Count > MaxPending) MaxPending = jobs.Count;
    }

    private void Refuse(Reject reason, int root, int generation, PlanterBrain planter, DamageType type = DamageType.Direct)
    {
        rejected[(int)reason]++;
        Emit(TraceKind.Rejected, root, generation, planter, type, 0f, false, reason);
    }

    private static StatType ChanceStat(DamageType type) => type switch
    {
        DamageType.Explosion => StatType.ExplosionChance,
        DamageType.Tornado => StatType.TornadoChance,
        DamageType.Boomerang => StatType.BoomerangChance,
        _ => StatType.ElectricChance,
    };

    // ---------------------------------------------------------------- kuyruk
    private void Update()
    {
        if (jobs.Count == 0) return;
        if (!RoundRunning || !BossRewardManager.TryGetChain(out config)) { DropJobs(Reject.RoundEnd); return; }
        int frame = Time.frameCount, done = 0, write = 0;
        // Yürütülen patlama yeni iş doğurabilir (listenin sonuna, bu karenin işi olarak): döngü onları da görür ve bekletir.
        for (int i = 0; i < jobs.Count; i++)
        {
            Job job = jobs[i];
            if (job.frame == frame || done >= config.JobsPerFrame) { jobs[write++] = job; continue; }
            Outcome outcome = job.run == runSerial && job.round == rounds.CurrentRound ? Execute(job) : Outcome.Invalid;
            if (outcome == Outcome.Waiting)
            {
                PoolWaitFrames++;
                if (!job.waited) { job.waited = true; PoolWaitJobs++; }
                Emit(TraceKind.Waiting, job.root, job.generation, job.planter, job.type, 0f, false, default);
                jobs[write++] = job;
                continue;
            }
            done++;
            if (outcome == Outcome.Fired)
            {
                fired[job.generation]++;
                int delay = frame - job.frame;
                DelayFramesTotal += delay;
                if (delay > DelayFramesMax) DelayFramesMax = delay;
                if (Time.time - job.time > DelaySecondsMax) DelaySecondsMax = Time.time - job.time;
                Emit(TraceKind.Fired, job.root, job.generation, job.planter, job.type, 0f, true, default);
            }
            else Refuse(job.run == runSerial && job.round == rounds.CurrentRound ? Reject.InvalidSource : Reject.RoundEnd, job.root, job.generation, job.planter, job.type);
            Release(job.root);   // işin tutumu (başlayan kasırga / bumerang kökü kendisi tutar)
        }
        jobs.RemoveRange(write, jobs.Count - write);
    }

    // Davranışın mevcut yolu, bu neslin bağlamı ve hasar çarpanıyla. Kaynak saksı hâlâ yerinde olmalı.
    private Outcome Execute(Job job)
    {
        if ((job.type == DamageType.Tornado && DemoSceneSettings.Blocks(UnlockType.TileBehavior_Tornado)) ||
            (job.type == DamageType.Boomerang && DemoSceneSettings.Blocks(UnlockType.TileBehavior_Boomerang))) return Outcome.Invalid;
        PlanterBrain planter = job.planter;
        if (planter == null || !planter.IsPlaced || job.cell == null || job.cell.GetPlanterBrain() != planter) return Outcome.Invalid;
        if (!roots.TryGetValue(job.root, out Root root)) return Outcome.Invalid;
        HarvestLink link = HarvestLink.Behavior(job.root, job.generation);
        float factor = config.DamageFactor(job.generation);
        int damage = PlanterBrain.HarvestDamage;
        bool started;
        switch (job.type)
        {
            case DamageType.Explosion:
                planter.Explode(job.position, link, factor);
                started = true;
                break;
            case DamageType.Electric:
                started = HarvestBehaviorManager.Instance != null && HarvestBehaviorManager.Instance.TryElectric(planter, damage, link, factor);
                break;
            case DamageType.Tornado:
                TornadoManager tornadoes = TornadoManager.Instance;
                if (tornadoes == null) return Outcome.Invalid;
                if (!tornadoes.HasCapacity) return Outcome.Waiting;
                started = tornadoes.TrySpawn(job.cell, damage, link, factor, ChainRandom.Fork(ref root.random));
                break;
            default:
                HarvestBehaviorManager manager = HarvestBehaviorManager.Instance;
                if (manager == null) return Outcome.Invalid;
                if (!manager.HasBoomerangCapacity) return Outcome.Waiting;
                started = manager.TryBoomerang(planter, job.cell, damage, link, factor, ChainRandom.Fork(ref root.random));
                break;
        }
        if (!started) return Outcome.Invalid;
        // Kaynak vurgusu: tetiklenen saksının ayak izi kısa bir çizgiyle (yalnız görsel).
        GridSystem grid = GridManager.Instance != null ? GridManager.Instance.GetGridSystem() : null;
        if (grid != null)
        {
            footprint.Clear();
            for (int i = 0; i < planter.OccupiedGrids.Count; i++)
            {
                GroundCell ground = planter.OccupiedGrids[i]?.GetGroundCellCached();
                if (ground != null) footprint.Add(ground.GetGridPosition());
            }
            AreaOutlineFeedback.PlayChainSource(grid, footprint);
        }
        return Outcome.Fired;
    }

    // Zincir hasarı: normal davranış hasarı × neslin çarpanı, tek yuvarlama (en az 1). Çarpan 1 (normal tetik) değeri değiştirmez ama
    // yine güvenlik dönüşümünden geçer: bozuk (negatif) hasar kısa yoldan kaçmaz.
    public static int Scale(int normalDamage, float factor) => factor == 1f
        ? NumericSafety.ToInt((double)normalDamage, 0, NumericSite.ChainDamage)
        : NumericSafety.ToInt(normalDamage * (double)factor, 1, NumericSite.ChainDamage);
}

// Zincir Hasat ödülünün ayarı (BossRewardSO'dan, ödül alınırken bir kez kopyalanır).
public readonly struct HarvestChainConfig
{
    public readonly int Generations, RootBudget, JobsPerFrame;
    private readonly float[] chance, damage;

    private HarvestChainConfig(int generations, float[] chance, float[] damage, int rootBudget, int jobsPerFrame)
    {
        Generations = generations; this.chance = chance; this.damage = damage; RootBudget = rootBudget; JobsPerFrame = jobsPerFrame;
    }

    // 1. ek nesil = dizin 0.
    public float ChanceFactor(int generation) => chance[generation - 1];
    public float DamageFactor(int generation) => damage[generation - 1];

    // Veri tutarsızsa (dizi uzunluğu, aralık, NaN / sonsuz) ödül uygulanmaz; değer kırpılmaz. Karşılaştırmalar NaN'ı da reddedecek
    // biçimde yazılır (NaN < 0 yanlış döner).
    public static bool TryCreate(BossRewardSO reward, out HarvestChainConfig config, out string error)
    {
        config = default;
        error = null;
        int n = reward.chainGenerations;
        if (n < 1 || n > HarvestChain.MaxGenerations) error = $"ek nesil sayısı {n} (1–{HarvestChain.MaxGenerations})";
        else if (reward.chainChance == null || reward.chainChance.Length != n) error = "şans çarpanı sayısı ek nesil sayısına eşit değil";
        else if (reward.chainDamage == null || reward.chainDamage.Length != n) error = "hasar çarpanı sayısı ek nesil sayısına eşit değil";
        else if (reward.chainRootBudget < 1 || reward.chainJobsPerFrame < 1) error = "kök bütçesi ve kare bütçesi en az 1 olmalı";
        else
            for (int i = 0; i < n && error == null; i++)
            {
                float chance = reward.chainChance[i], damage = reward.chainDamage[i];
                if (!(chance >= 0f && chance <= 1f)) error = $"{i + 1}. ek neslin şans çarpanı geçersiz ({chance})";
                else if (!(damage >= 0f) || float.IsInfinity(damage)) error = $"{i + 1}. ek neslin hasar çarpanı geçersiz ({damage})";
            }
        if (error != null) return false;
        config = new HarvestChainConfig(n, (float[])reward.chainChance.Clone(), (float[])reward.chainDamage.Clone(), reward.chainRootBudget, reward.chainJobsPerFrame);
        return true;
    }
}

// Zincirin tekrar üretilebilir zar akışı (xorshift32): durum tek bir sayıdır, değer tipi olarak taşınır; UnityEngine.Random'dan bağımsız.
public static class ChainRandom
{
    public static uint Seed(int seed)
    {
        uint s = (uint)seed * 2654435761u ^ 0x9E3779B9u;
        return s == 0 ? 0x6D2B79F5u : s;
    }

    public static uint Next(ref uint state)
    {
        uint s = state;
        s ^= s << 13; s ^= s >> 17; s ^= s << 5;
        return state = s;
    }

    // [0, 1)
    public static float Value(ref uint state) => (Next(ref state) >> 8) * (1f / 16777216f);

    // [min, max)
    public static int Range(ref uint state, int min, int max) => max <= min ? min : min + (int)(Next(ref state) % (uint)(max - min));

    // Kökün akışından bir efekte ayrı akış (kasırganın yolu, bumerangın yönü). Hiçbir zaman 0 değildir.
    public static uint Fork(ref uint state)
    {
        uint s = Next(ref state) ^ 0xA5A5A5A5u;
        return s == 0 ? 0x6D2B79F5u : s;
    }
}
