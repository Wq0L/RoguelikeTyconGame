using System.Collections.Generic;
using UnityEngine;

// Kırılma ödüllerinin gecikmiş ikinci darbeleri (Bölüm 3.6): Artçı Patlama ve Çifte Akım. RoundManager kurar.
// - Yalnız şansı tutmuş NORMAL bir tetik bir yankı planlar; yankı yeni tetik atmaz, yeni yankı ya da zincir başlatmaz
//   (yankı öldürmeleri davranış hasarıdır: DamageTypeRules.CanTriggerBehaviors bunlara izin vermez; ayrıca darbe sürerken plan reddedilir).
// - Hasar plan anında hesaplanır: ilk darbenin HESAPLANMIŞ hasarı × ödülün oranı. Katsayılar ikinci kez uygulanmaz.
// - Hedefler darbe anında, hücre üzerinden bulunur. İşte bitki referansı tutulmaz: ilk darbede ölüp havuza dönen ya da başka
//   hücrede yeniden doğan bir nesne eski işten hasar alamaz. Darbe anında alanda canlı olan bitki geçerli hedeftir.
// - İş, planlandığı run'a ve round'a aittir: round ya da run biterse, menüye dönülürse düşürülür (hasar vermez).
// - Gecikme, işin planlandığı kareden SONRA geçen oyun süresiyle sayılır (kalan süre). Mutlak oyun zamanıyla (Time.time)
//   karşılaştırılmaz: o değerin kayan nokta çözünürlüğü oturum uzadıkça kabalaşır ve gecikme kare adımının tam katıysa
//   (0,20 sn = 6 × 1/30) yankı bir kare erken ya da geç düşebiliyordu; hangisi olacağı oturumun o anki zamanına bağlıydı
//   (Bölüm 3.7.5: aynı seed'li ölçümlerin çalıştırmadan çalıştırmaya ayrışmasının nedeni).
// - Her kare sahne aranmaz: yalnız kendi iş listesine bakar.
[DisallowMultipleComponent]
public sealed class BehaviorEchoes : MonoBehaviour
{
    public static BehaviorEchoes Instance { get; private set; }
    // Şu an bir yankı darbesi uygulanıyor (ölçüm: bu sırada ölen bitki yankı hasadıdır).
    public static bool IsExecuting { get; private set; }
    // Uygulanmakta olan yankının kaynak saksısı (ölçüm: vuruş, ilk darbenin alanında mı yeni alanda mı).
    public static PlanterBrain ExecutingSource { get; private set; }
    // Ölçüm için: işin planlanması ("plan"), uygulanması ("uygula") ve düşmesi ("dus"). Parametreler: aşama, tür, iş kimliği,
    // planlanan gecikme (sn), planlandığından beri geçen kare sayısı. Oynanış buna bağlı değildir.
    // Gözlem olayı (ObserverEvents): dinleyicinin hatası yankı kuyruğunu ve uygulanan darbeyi yarıda bırakmaz.
    private static System.Action<string, DamageType, int, float, int>[] traceObservers = System.Array.Empty<System.Action<string, DamageType, int, float, int>>();
    public static event System.Action<string, DamageType, int, float, int> Traced
    {
        add => ObserverEvents.Add(ref traceObservers, value);
        remove => ObserverEvents.Remove(ref traceObservers, value);
    }
    private static void Emit(string phase, DamageType type, int id, float delay, int frames) => ObserverEvents.Raise(traceObservers, phase, type, id, delay, frames);

    private struct Job
    {
        public float remaining;
        public int run, round;
        public DamageType type;
        public PlanterBrain source;
        public int damage;
        public float radiusCells;
        public int reach;
        public Vector3 center;
        public List<GridPosition> footprint;
        public int id, frame;
        public float delay;
    }
    private int jobSerial;
    // Kalan süre bu kadarın altına indiğinde gecikme dolmuş sayılır (kare adımının toplamındaki kayan nokta artığı için).
    private const float DelayTolerance = 1e-4f;

    private readonly List<Job> jobs = new();
    private readonly Stack<List<GridPosition>> spare = new();
    private readonly List<GridPosition> targets = new();
    private readonly int[] scheduled = new int[8], executed = new int[8], dropped = new int[8], hits = new int[8], kills = new int[8], cells = new int[8];
    private RoundManager rounds;
    private GameManager game;
    private int runSerial;

    public int Pending => jobs.Count;
    public int Scheduled(DamageType type) => scheduled[(int)type];
    public int Executed(DamageType type) => executed[(int)type];
    // Round / run bittiği ya da kaynak kaybolduğu için uygulanmadan düşen işler.
    public int Dropped(DamageType type) => dropped[(int)type];
    public int Hits(DamageType type) => hits[(int)type];
    public int Kills(DamageType type) => kills[(int)type];
    // Yankıların eriştiği tarla hücresi sayısı (canlı bitki olsun olmasın): Hits ile farkı boşa giden darbedir.
    public int Cells(DamageType type) => cells[(int)type];

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += HandleRunStarted;
        rounds.OnRoundEnded += DropAll;
        rounds.OnRoundChanged += HandleRoundChanged;
    }

    private void OnDisable()
    {
        DropAll();
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundEnded -= DropAll;
            rounds.OnRoundChanged -= HandleRoundChanged;
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HandleRunStarted()
    {
        DropAll();
        runSerial++;
        System.Array.Clear(scheduled, 0, scheduled.Length);
        System.Array.Clear(executed, 0, executed.Length);
        System.Array.Clear(dropped, 0, dropped.Length);
        System.Array.Clear(hits, 0, hits.Length);
        System.Array.Clear(kills, 0, kills.Length);
        System.Array.Clear(cells, 0, cells.Length);
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state != GameStates.Round) DropAll();
    }

    // Artçı varken alan çizgisinin havuzu round başında kurulur: ilk artçıda tek seferlik takılma olmasın (yalnız görsel).
    private void HandleRoundChanged(int round)
    {
        if (RunPower.TryGetEcho(DamageType.Explosion, out _)) AreaOutlineFeedback.Prewarm();
    }

    // Bekleyen bütün işleri uygulamadan düşürür.
    public void DropAll()
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            dropped[(int)jobs[i].type]++;
            Emit("dus", jobs[i].type, jobs[i].id, jobs[i].delay, Time.frameCount - jobs[i].frame);
            Recycle(jobs[i].footprint);
        }
        jobs.Clear();
    }

    private bool CanSchedule => !IsExecuting && isActiveAndEnabled && rounds != null && rounds.IsRoundActive;

    // Artçı Patlama: aynı ayak izinin çevresinde, gecikmeyle. firstDamage ilk patlamanın hesaplanmış hasarıdır.
    public bool ScheduleExplosion(PlanterBrain source, IReadOnlyList<GridObject> footprint, Vector3 center, int firstDamage, BehaviorEcho echo)
    {
        if (!CanSchedule || source == null || footprint == null) return false;
        List<GridPosition> cells = spare.Count > 0 ? spare.Pop() : new List<GridPosition>();
        cells.Clear();
        foreach (GridObject grid in footprint)
        {
            GroundCell ground = grid?.GetGroundCellCached();
            if (ground != null) cells.Add(ground.GetGridPosition());
        }
        if (cells.Count == 0) { spare.Push(cells); return false; }
        jobs.Add(new Job
        {
            remaining = Mathf.Max(0f, echo.Delay), run = runSerial, round = rounds.CurrentRound, type = DamageType.Explosion, source = source,
            damage = ScaleDamage(firstDamage, echo.Damage), radiusCells = HarvestBehaviorGeometry.ExplosionRadiusCells * Mathf.Max(0f, echo.Radius),
            center = center, footprint = cells, id = ++jobSerial, frame = Time.frameCount, delay = Mathf.Max(0f, echo.Delay),
        });
        scheduled[(int)DamageType.Explosion]++;
        Emit("plan", DamageType.Explosion, jobSerial, Mathf.Max(0f, echo.Delay), 0);
        return true;
    }

    // Çifte Akım: aynı saksıdan, gecikmeyle, mevcut çapraz geometriyle. firstDamage ilk dalganın hesaplanmış hasarıdır.
    public bool ScheduleElectric(PlanterBrain source, int firstDamage, BehaviorEcho echo)
    {
        if (!CanSchedule || source == null) return false;
        jobs.Add(new Job
        {
            remaining = Mathf.Max(0f, echo.Delay), run = runSerial, round = rounds.CurrentRound, type = DamageType.Electric, source = source,
            damage = ScaleDamage(firstDamage, echo.Damage), reach = echo.Reach,
            id = ++jobSerial, frame = Time.frameCount, delay = Mathf.Max(0f, echo.Delay),
        });
        scheduled[(int)DamageType.Electric]++;
        Emit("plan", DamageType.Electric, jobSerial, Mathf.Max(0f, echo.Delay), 0);
        return true;
    }

    public static int ScaleDamage(int firstDamage, float ratio) =>
        NumericSafety.ToInt(firstDamage * (double)Mathf.Max(0f, ratio), 0, NumericSite.EchoDamage);

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void Update()
    {
        if (jobs.Count == 0) return;
        if (rounds == null || !rounds.IsRoundActive || GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round)
        {
            DropAll();
            return;
        }
        float delta = Time.deltaTime;
        int frame = Time.frameCount;
        for (int i = 0; i < jobs.Count;)
        {
            Job job = jobs[i];
            // Planlandığı karenin süresi sayılmaz (o süre işten önce geçti); script sırasından bağımsızdır.
            if (job.frame != frame) job.remaining -= delta;
            if (job.remaining > DelayTolerance) { jobs[i] = job; i++; continue; }
            jobs.RemoveAt(i);
            if (job.run != runSerial || job.round != rounds.CurrentRound)
            {
                dropped[(int)job.type]++;
                Emit("dus", job.type, job.id, job.delay, Time.frameCount - job.frame);
            }
            else Execute(job);
            Recycle(job.footprint);
        }
    }

    private void Recycle(List<GridPosition> cells)
    {
        if (cells == null) return;
        cells.Clear();
        spare.Push(cells);
    }

    private void Execute(Job job)
    {
        int index = (int)job.type;
        bool done = false;
        IsExecuting = true;
        ExecutingSource = job.source;
        try
        {
            if (job.type == DamageType.Explosion) done = Explode(job);
            else if (job.type == DamageType.Electric) done = Shock(job);
        }
        finally { IsExecuting = false; ExecutingSource = null; }
        if (done) executed[index]++; else dropped[index]++;
        Emit(done ? "uygula" : "dus", job.type, job.id, job.delay, Time.frameCount - job.frame);
    }

    private bool Explode(Job job)
    {
        GridSystem grid = GridManager.Instance != null ? GridManager.Instance.GetGridSystem() : null;
        if (grid == null) return false;
        HarvestBehaviorGeometry.ExplosionCells(job.footprint, job.radiusCells, targets);
        VFXManager.Instance?.PlayExplosion(job.center, true);
        // Alan geri bildirimi (Bölüm 3.7.5.1): bu darbenin gerçek hedef hücreleri ve saksının ayak izi. Yalnız görsel; hasar aşağıda.
        AreaOutlineFeedback.PlayAftershock(grid, job.footprint, targets);
        foreach (GridPosition cell in targets)
        {
            GridObject entry = grid.GetGridObject(cell);
            GroundCell ground = entry?.GetGroundCellCached();
            if (ground != null && !ground.IsLocked) cells[(int)DamageType.Explosion]++;
            GameObject plant = entry?.GetPlantObject();
            if (plant == null || !plant.TryGetComponent(out PlantHealth health) || health.IsDead) continue;
            VFXManager.Instance?.PlayExplosion(plant.transform.position, false);
            hits[(int)DamageType.Explosion]++;
            // Artçının öldürdüğü bitki zincir başlatmaz (Bölüm 3.7.6).
            health.TakeDamage(job.damage, DamageType.Explosion, false, 1f, HarvestLink.Aftershock);
            if (health.IsDead) kills[(int)DamageType.Explosion]++;
        }
        return true;
    }

    private bool Shock(Job job)
    {
        HarvestBehaviorManager manager = HarvestBehaviorManager.Instance;
        if (manager == null || job.source == null || job.source.OccupiedGrids.Count == 0) return false;
        // Hedef hücre kalmadıysa (struck 0) dalga yine atılmış sayılır; görsel havuzu doluysa hasar yine uygulanır.
        manager.TryElectricEcho(job.source, job.damage, job.reach, out int struck, out int killed, out int reached);
        cells[(int)DamageType.Electric] += reached;
        hits[(int)DamageType.Electric] += struck;
        kills[(int)DamageType.Electric] += killed;
        return true;
    }
}
