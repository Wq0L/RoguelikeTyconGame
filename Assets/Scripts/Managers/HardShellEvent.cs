using System.Collections.Generic;
using System.Globalization;

// Sert Kabuk uygulaması. Bölge Don Cephesi gibi bir kenar şerididir (EdgeBandZone) ve duyuruda sabitlenir.
// - Aktifken şeritte doğan bitki canı × çarpanla doğar (PlantSpawner → PlantHealth.Initialize).
// - Olay başlarken şeritte zaten yaşayan bitkiler (hazırlık stoğu) de sertleşir: azami ve mevcut can aynı oranda büyür,
//   yaralı bitki dolmaz. Aksi hâlde her üretim noktasındaki ilk bitki kuraldan kaçardı.
// - Çarpan bitkinin o yaşamına bir kez uygulanır (havuzdan yeniden doğan bitki tabandan başlar).
// - Olay bitince şeritte hâlâ yaşayan sert bitkiler aynı oranla normale döner.
public sealed class HardShellEvent : SegmentEventRuntime
{
    private readonly HardShellSO data;
    private readonly EdgeBandZone zone = new();

    public HardShellEvent(HardShellSO data, SegmentEventTiming timing) : base(data, timing)
    {
        this.data = data;
    }

    public override IReadOnlyList<GridPosition> ZoneCells => zone.Cells;
    public override string RuleSummary =>
        $"{zone.Name} bitki canı ×{data.healthMultiplier.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')}";
    public override string ZoneHint => "turuncu şerit";
    public override string EndedText => "Şeritteki bitkiler normal cana döndü";

    public override bool Covers(GridPosition cell) => IsPrepared && zone.Covers(cell);
    public override float SpawnHealthMultiplier(GridPosition cell) => IsActive && Covers(cell) ? data.healthMultiplier : 1f;

    public override bool TryPrepare(SegmentEventContext context)
    {
        if (IsPrepared) return true;
        int seed = TimingSeed != 0 ? TimingSeed : System.Environment.TickCount ^ (Segment * 7919);
        if (!zone.TryChoose(context, data.coverage, new System.Random(seed))) return false;
        IsPrepared = true;
        return true;
    }

    protected override void OnActivated() => ForEachPlant(plant => plant.ApplyHealthMultiplier(data.healthMultiplier));
    protected override void OnFinished() => ForEachPlant(plant => plant.ClearHealthMultiplier());

    private void ForEachPlant(System.Action<PlantHealth> action)
    {
        GridManager grid = GridManager.Instance;
        GridSystem system = grid != null ? grid.GetGridSystem() : null;
        if (system == null) return;
        foreach (GridPosition cell in zone.Cells)
        {
            UnityEngine.GameObject plant = system.GetGridObject(cell)?.GetPlantObject();
            if (plant != null && plant.TryGetComponent(out PlantHealth health) && !health.IsDead) action(health);
        }
    }
}
