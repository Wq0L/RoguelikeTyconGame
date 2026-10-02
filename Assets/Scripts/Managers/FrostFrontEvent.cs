using System.Collections.Generic;
using System.Globalization;

// Don Cephesi uygulaması. Duyuru anında açık alanın dört kenar şeridinden biri seçilir ve saklanır (EdgeBandZone); şerit grid'in
// o satır/sütun(lar)ıdır, sonradan açılan hücreler de aynı şeritte kalır. Etki üretim noktası başınadır:
// saksının yalnız şeritteki üretim noktaları yavaşlar. Bitki, saksı, tile ve rezonansa dokunmaz.
public sealed class FrostFrontEvent : SegmentEventRuntime
{
    private readonly FrostFrontSO data;
    private readonly EdgeBandZone zone = new();

    public FrostFrontEvent(FrostFrontSO data, SegmentEventTiming timing) : base(data, timing)
    {
        this.data = data;
    }

    public override IReadOnlyList<GridPosition> ZoneCells => zone.Cells;

    public override string RuleSummary =>
        $"{BandName} üretim ×{data.spawnIntervalMultiplier.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} yavaş";

    public override string ZoneHint => "mavi şerit";
    public override string EndedText => "Şeritteki üretim normale döndü";

    public string BandName => zone.Name;

    public override bool Covers(GridPosition cell) => IsPrepared && zone.Covers(cell);

    public override float SpawnIntervalMultiplier(GridPosition cell) => IsActive && Covers(cell) ? data.spawnIntervalMultiplier : 1f;

    public override bool TryPrepare(SegmentEventContext context)
    {
        if (IsPrepared) return true;
        // Boss ritminde bölge seed'i run'ın boss seed'inden gelir; eski profillerde olayın kendi seed'i (0: her run farklı).
        int seed = TimingSeed != 0 ? TimingSeed : data.seed != 0 ? data.seed + Segment : System.Environment.TickCount ^ (Segment * 7919);
        if (!zone.TryChoose(context, data.coverage, new System.Random(seed))) return false;
        IsPrepared = true;
        return true;
    }
}
