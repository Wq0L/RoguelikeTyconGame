using System.Collections.Generic;
using UnityEngine;

// Kırılma ödüllerinin gecikmiş ikinci darbeleri (Bölüm 3.6): Artçı Patlama ve Çifte Akım. RoundManager kurar.
// - Yalnız şansı tutmuş NORMAL bir tetik bir yankı planlar; yankı yeni tetik atmaz, yeni yankı ya da zincir başlatmaz
//   (yankı öldürmeleri davranış hasarıdır: DamageTypeRules.CanTriggerBehaviors bunlara izin vermez; ayrıca darbe sürerken plan reddedilir).
// - Hasar plan anında hesaplanır: ilk darbenin HESAPLANMIŞ hasarı × ödülün oranı. Katsayılar ikinci kez uygulanmaz.
// - Hedefler darbe anında, hücre üzerinden bulunur. İşte bitki referansı tutulmaz: ilk darbede ölüp havuza dönen ya da başka
//   hücrede yeniden doğan bir nesne eski işten hasar alamaz. Darbe anında alanda canlı olan bitki geçerli hedeftir.
// - İş, planlandığı run'a ve round'a aittir: round ya da run biterse, menüye dönülürse düşürülür (hasar vermez).
// - Her kare sahne aranmaz: yalnız kendi iş listesine bakar.
[DisallowMultipleComponent]
public sealed class BehaviorEchoes : MonoBehaviour
{
    public static BehaviorEchoes Instance { get; private set; }
    // Şu an bir yankı darbesi uygulanıyor (ölçüm: bu sırada ölen bitki yankı hasadıdır).
    public static bool IsExecuting { get; private set; }

    private struct Job
    {
        public float due;
        public int run, round;
        public DamageType type;
        public PlanterBrain source;
        public int damage;
        public float radiusCells;
        public Vector3 center;
        public List<GridPosition> footprint;
    }

    private readonly List<Job> jobs = new();
    private readonly Stack<List<GridPosition>> spare = new();
    private readonly List<GridPosition> targets = new();
    private readonly int[] scheduled = new int[8], executed = new int[8], dropped = new int[8], hits = new int[8], kills = new int[8];
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
    }

    private void OnDisable()
    {
        DropAll();
        if (rounds != null)
        {
            rounds.OnRunStarted -= HandleRunStarted;
            rounds.OnRoundEnded -= DropAll;
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
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state != GameStates.Round) DropAll();
    }

    // Bekleyen bütün işleri uygulamadan düşürür.
    public void DropAll()
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            dropped[(int)jobs[i].type]++;
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
            due = Time.time + Mathf.Max(0f, echo.Delay), run = runSerial, round = rounds.CurrentRound, type = DamageType.Explosion, source = source,
            damage = ScaleDamage(firstDamage, echo.Damage), radiusCells = HarvestBehaviorGeometry.ExplosionRadiusCells * Mathf.Max(0f, echo.Radius),
            center = center, footprint = cells,
        });
        scheduled[(int)DamageType.Explosion]++;
        return true;
    }

    // Çifte Akım: aynı saksıdan, gecikmeyle, mevcut çapraz geometriyle. firstDamage ilk dalganın hesaplanmış hasarıdır.
    public bool ScheduleElectric(PlanterBrain source, int firstDamage, BehaviorEcho echo)
    {
        if (!CanSchedule || source == null) return false;
        jobs.Add(new Job
        {
            due = Time.time + Mathf.Max(0f, echo.Delay), run = runSerial, round = rounds.CurrentRound, type = DamageType.Electric, source = source,
            damage = ScaleDamage(firstDamage, echo.Damage),
        });
        scheduled[(int)DamageType.Electric]++;
        return true;
    }

    public static int ScaleDamage(int firstDamage, float ratio) =>
        (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round(firstDamage * (double)Mathf.Max(0f, ratio))));

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
        float now = Time.time;
        for (int i = 0; i < jobs.Count;)
        {
            Job job = jobs[i];
            if (job.due > now) { i++; continue; }
            jobs.RemoveAt(i);
            if (job.run != runSerial || job.round != rounds.CurrentRound) dropped[(int)job.type]++;
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
        try
        {
            if (job.type == DamageType.Explosion) done = Explode(job);
            else if (job.type == DamageType.Electric) done = Shock(job);
        }
        finally { IsExecuting = false; }
        if (done) executed[index]++; else dropped[index]++;
    }

    private bool Explode(Job job)
    {
        GridSystem grid = GridManager.Instance != null ? GridManager.Instance.GetGridSystem() : null;
        if (grid == null) return false;
        HarvestBehaviorGeometry.ExplosionCells(job.footprint, job.radiusCells, targets);
        VFXManager.Instance?.PlayExplosion(job.center, true);
        foreach (GridPosition cell in targets)
        {
            GridObject entry = grid.GetGridObject(cell);
            GameObject plant = entry?.GetPlantObject();
            if (plant == null || !plant.TryGetComponent(out PlantHealth health) || health.IsDead) continue;
            VFXManager.Instance?.PlayExplosion(plant.transform.position, false);
            hits[(int)DamageType.Explosion]++;
            health.TakeDamage(job.damage, DamageType.Explosion);
            if (health.IsDead) kills[(int)DamageType.Explosion]++;
        }
        return true;
    }

    private bool Shock(Job job)
    {
        HarvestBehaviorManager manager = HarvestBehaviorManager.Instance;
        if (manager == null || job.source == null || job.source.OccupiedGrids.Count == 0) return false;
        // Hedef hücre kalmadıysa (struck 0) dalga yine atılmış sayılır; görsel havuzu doluysa hasar yine uygulanır.
        manager.TryElectricEcho(job.source, job.damage, out int struck, out int killed);
        hits[(int)DamageType.Electric] += struck;
        kills[(int)DamageType.Electric] += killed;
        return true;
    }
}
