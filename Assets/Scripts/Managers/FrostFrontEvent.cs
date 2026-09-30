using System.Collections.Generic;
using System.Globalization;

// Don Cephesi uygulaması. Duyuru anında açık alanın dört kenar şeridinden biri seçilir ve saklanır; şerit grid'in
// o satır/sütun(lar)ıdır, sonradan açılan hücreler de aynı şeritte kalır. Etki üretim noktası başınadır:
// saksının yalnız şeritteki üretim noktaları yavaşlar. Bitki, saksı, tile ve rezonansa dokunmaz.
public sealed class FrostFrontEvent : SegmentEventRuntime
{
    private readonly FrostFrontSO data;
    private readonly List<GridPosition> zone = new();
    private bool byColumn;
    private int from, to, gridHeight;

    public FrostFrontEvent(FrostFrontSO data, int segment, int segmentRounds) : base(data, segment, segmentRounds)
    {
        this.data = data;
    }

    public override IReadOnlyList<GridPosition> ZoneCells => zone;

    public override string RuleSummary =>
        $"{BandName} üretim ×{data.spawnIntervalMultiplier.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} yavaş";

    // Harita koordinatlarıyla (RoundMapUI): sütun harfleri A…, satır 1 = en üst.
    public string BandName
    {
        get
        {
            if (!IsPrepared) return "şeritte";
            if (byColumn)
                return from == to ? $"{RoundMapUI.ColumnName(from)} sütununda" : $"{RoundMapUI.ColumnName(from)}–{RoundMapUI.ColumnName(to)} sütunlarında";
            int top = gridHeight - to, bottom = gridHeight - from;
            return top == bottom ? $"{top}. satırda" : $"{top}–{bottom}. satırlarda";
        }
    }

    public override bool Covers(GridPosition cell) => IsPrepared && (byColumn ? cell.x >= from && cell.x <= to : cell.z >= from && cell.z <= to);

    public override float SpawnIntervalMultiplier(GridPosition cell) => IsActive && Covers(cell) ? data.spawnIntervalMultiplier : 1f;

    public override bool TryPrepare(SegmentEventContext context)
    {
        if (IsPrepared) return true;
        if (context == null || context.OpenCells.Count == 0) return false;

        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
        foreach (GridPosition cell in context.OpenCells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.z < minZ) minZ = cell.z;
            if (cell.z > maxZ) maxZ = cell.z;
        }

        var candidates = new List<(bool column, int from, int to)>();
        AddEdges(candidates, true, minX, maxX);
        AddEdges(candidates, false, minZ, maxZ);
        if (candidates.Count == 0) return false;

        // Hazırlık anındaki bütün üretim noktalarını kapsayan şerit seçilmez (başka seçenek varsa).
        if (context.ProductionPoints.Count > 0)
        {
            var spared = candidates.FindAll(c => !CoversAll(c, context.ProductionPoints));
            if (spared.Count > 0) candidates = spared;
        }

        var rng = new System.Random(data.seed != 0 ? data.seed + Segment : System.Environment.TickCount ^ (Segment * 7919));
        var pick = candidates[rng.Next(candidates.Count)];
        byColumn = pick.column;
        from = pick.from;
        to = pick.to;
        gridHeight = context.GridHeight;

        zone.Clear();
        for (int x = 0; x < context.GridWidth; x++)
        for (int z = 0; z < context.GridHeight; z++)
        {
            int value = byColumn ? x : z;
            if (value >= from && value <= to) zone.Add(new GridPosition(x, z));
        }
        IsPrepared = true;
        return true;
    }

    // Kenar şeritleri: kalınlık = kenar × coverage, en az 1, en fazla kenar − 1 (açık alanın tamamı asla).
    private void AddEdges(List<(bool, int, int)> list, bool column, int min, int max)
    {
        int side = max - min + 1;
        if (side < 2) return;
        int thickness = System.Math.Min(side - 1, System.Math.Max(1, (int)System.Math.Round(side * data.coverage, System.MidpointRounding.AwayFromZero)));
        list.Add((column, min, min + thickness - 1));
        list.Add((column, max - thickness + 1, max));
    }

    private static bool CoversAll((bool column, int from, int to) band, List<GridPosition> points)
    {
        foreach (GridPosition point in points)
        {
            int value = band.column ? point.x : point.z;
            if (value < band.from || value > band.to) return false;
        }
        return true;
    }
}
