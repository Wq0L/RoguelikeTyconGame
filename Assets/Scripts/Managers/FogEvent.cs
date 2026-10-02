using System.Collections.Generic;
using System.Globalization;

// Sis uygulaması: bölgesi yoktur. Aktifken oyuncunun vuruş yarıçapına (AreaRadius, Player) tek bir geçici çarpan ekler;
// vuruş, imleç halkası ve saldırı halkası aynı stat'ı okur. Olay bitince yalnız kendi eklediği modifier'ı kaldırır
// (StatManager listeyi toptan temizlediyse dokunmaz).
// İki kural (FogSO.stepDown): eski sabit çarpan (×0,85) ya da "bir basamak": yarıçap, en iyi nişanın hedef sayısı tam bir
// basamak düşecek kadar küçülür. Çarpan boss başladığı andaki yarıçaptan hesaplanır ve round boyunca sabittir.
public sealed class FogEvent : SegmentEventRuntime
{
    private static readonly List<GridPosition> NoCells = new();
    private readonly FogSO data;
    private StatManager stats;
    private StatModifier modifier;
    private bool applied;
    private int cellsBefore, cellsDuring;

    public FogEvent(FogSO data, SegmentEventTiming timing) : base(data, timing)
    {
        this.data = data;
    }

    public override IReadOnlyList<GridPosition> ZoneCells => NoCells;
    public override string RuleSummary
    {
        get
        {
            if (!data.stepDown)
                return $"vuruş yarıçapı ×{data.radiusMultiplier.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')}";
            // Aktifken uygulanan sayılar; önizlemede oyuncunun o anki yarıçapına göre beklenen sayılar.
            if (applied) return $"vuruş alanı bir basamak dar: en iyi nişanda {cellsBefore} → {cellsDuring} bitki";
            StatManager manager = StatManager.Instance;
            float cell = CellSize;
            if (manager == null || cell <= 0f) return "vuruş alanı bir basamak daralır";
            float radius = manager.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            int now = HarvestArea.BestCells(radius, cell), then = HarvestArea.BestCells(HarvestArea.StepDownRadius(radius, cell), cell);
            return $"vuruş alanı bir basamak daralır: en iyi nişanda {now} → {then} bitki";
        }
    }
    public override string EndedText => data.stepDown ? "Vuruş alanı normale döndü" : "Vuruş yarıçapı normale döndü";
    public override bool Covers(GridPosition cell) => false;

    private static float CellSize => GridManager.Instance != null ? GridManager.Instance.GetCellSize() : 0f;

    public override bool TryPrepare(SegmentEventContext context)
    {
        IsPrepared = true;
        return true;
    }

    protected override void OnActivated()
    {
        stats = StatManager.Instance;
        if (stats == null) return;
        float multiplier = data.radiusMultiplier;
        if (data.stepDown)
        {
            float cell = CellSize, radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            float target = HarvestArea.StepDownRadius(radius, cell);
            cellsBefore = HarvestArea.BestCells(radius, cell);
            cellsDuring = HarvestArea.BestCells(target, cell);
            multiplier = radius > 0f ? target / radius : 1f;
        }
        modifier = new StatModifier
        {
            statType = StatType.AreaRadius, target = StatTarget.Player, operation = ModifierOperation.MorePercent, value = multiplier - 1f
        };
        stats.AddGlobalModifier(modifier);
        stats.OnGlobalModifiersCleared += Forget;
        applied = true;
    }

    protected override void OnFinished()
    {
        if (stats != null)
        {
            stats.OnGlobalModifiersCleared -= Forget;
            if (applied) stats.RemoveGlobalModifier(modifier);
        }
        applied = false;
        stats = null;
    }

    private void Forget() => applied = false;
}
