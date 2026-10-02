using System.Collections.Generic;
using UnityEngine;

// Segment olayı (boss) verisi: ad, kural metni, renk ve ayarlar. Oynanışa etkisi CreateRuntime'ın döndürdüğü
// nesnededir; SegmentEventDirector onu hazırlar, etkinleştirir ve temizler. Boss bir düşman değil, tarla kuralıdır.
public abstract class SegmentEventSO : ScriptableObject
{
    public string displayName = "OLAY";
    [Tooltip("Arayüzdeki kısa kural metni.")]
    public string ruleText = "";
    public Color color = Color.white;

    public abstract SegmentEventRuntime CreateRuntime(SegmentEventTiming timing);

    // Boss havuzundan seçilebilir mi (duyuru anındaki tarla ve oyuncu durumu). Eski profillerde sorulmaz.
    public virtual bool IsEligible(SegmentEventContext context) => true;
}

// Olayın run içindeki yeri. İki kullanım:
// - WholeSegment (eski profiller): bütün segment aktif, bir önceki segmentin başında duyurulur.
// - BossRound (Bölüm 3.4): segmentin ilk round'unda duyurulur, yalnız son round'unda aktiftir.
// Seed 0: olay kendi verisindeki seed'i kullanır (eski davranış). Başka değer: run'ın boss seed'inden türetilmiş bölge seed'i.
public readonly struct SegmentEventTiming
{
    public readonly int Segment, StartRound, EndRound, AnnounceRound, Seed;
    public readonly bool BossRoundOnly;

    private SegmentEventTiming(int segment, int start, int end, int announce, int seed, bool bossRoundOnly)
    {
        Segment = segment; StartRound = start; EndRound = end; AnnounceRound = announce; Seed = seed; BossRoundOnly = bossRoundOnly;
    }

    public static SegmentEventTiming WholeSegment(int segment, int segmentRounds)
    {
        segment = Mathf.Max(1, segment);
        int start = (segment - 1) * segmentRounds + 1;
        return new SegmentEventTiming(segment, start, segment * segmentRounds, Mathf.Max(1, start - segmentRounds), 0, false);
    }

    public static SegmentEventTiming BossRound(int segment, int segmentRounds, int seed)
    {
        segment = Mathf.Max(1, segment);
        int end = segment * segmentRounds;
        return new SegmentEventTiming(segment, end, end, (segment - 1) * segmentRounds + 1, seed, true);
    }
}

// Olayın hazırlığı için tarlanın o anki durumu.
public sealed class SegmentEventContext
{
    public int GridWidth, GridHeight;
    public readonly List<GridPosition> OpenCells = new();
    public readonly List<GridPosition> ProductionPoints = new();
    // Oyuncunun o anki vuruş yarıçapı (boss uygunluğu için; Sis).
    public float PlayerRadius = 1f;
}

// Bir run'daki tek olay örneği. Bölge bir kez seçilir (TryPrepare); önizleme ve uygulama aynı bölgeyi kullanır.
public abstract class SegmentEventRuntime
{
    public SegmentEventSO Data { get; }
    public int Segment { get; }
    public int StartRound { get; }
    public int EndRound { get; }
    // Bölgenin seçilip gösterildiği round (WholeSegment: bir önceki segmentin ilk round'u; BossRound: kendi segmentinin ilk round'u).
    public int AnnounceRound { get; }
    // Yalnız segmentin son round'unda aktif (Bölüm 3.4 boss ritmi).
    public bool BossRoundOnly { get; }
    protected int TimingSeed { get; }
    public bool IsPrepared { get; protected set; }
    public bool IsActive { get; private set; }
    public bool IsFinished { get; private set; }

    protected SegmentEventRuntime(SegmentEventSO data, SegmentEventTiming timing)
    {
        Data = data;
        Segment = timing.Segment;
        StartRound = timing.StartRound;
        EndRound = timing.EndRound;
        AnnounceRound = timing.AnnounceRound;
        BossRoundOnly = timing.BossRoundOnly;
        TimingSeed = timing.Seed;
    }

    public abstract bool TryPrepare(SegmentEventContext context);
    public abstract bool Covers(GridPosition cell);
    // Arayüz için: bölgedeki hücreler (açık olup olmadıklarına arayüz bakar). Bölgesiz olayda boş.
    public abstract IReadOnlyList<GridPosition> ZoneCells { get; }
    public abstract string RuleSummary { get; }
    // Arayüzde bölgenin adı ("mavi şerit"); bölgesiz olayda null.
    public virtual string ZoneHint => null;
    // Olay bitince round özetinde yazan kısa cümle.
    public virtual string EndedText => "Kural kalktı";
    public virtual float SpawnIntervalMultiplier(GridPosition cell) => 1f;
    // Aktifken bu hücrede doğan bitkinin can çarpanı (Sert Kabuk).
    public virtual float SpawnHealthMultiplier(GridPosition cell) => 1f;

    protected virtual void OnActivated() { }
    protected virtual void OnFinished() { }

    public void Activate()
    {
        if (IsActive || IsFinished) return;
        IsActive = true;
        OnActivated();
    }

    public void Finish()
    {
        bool wasActive = IsActive;
        IsActive = false;
        IsFinished = true;
        if (wasActive) OnFinished();
    }
}

// Kenar şeridi bölgesi (Don Cephesi, Sert Kabuk): duyuru anında açık alanın dört kenar şeridinden biri seçilir ve saklanır.
// Şerit grid'in o satır/sütun(lar)ıdır: grid sonradan genişlerse yeni açılan hücrelerden şeridin satır/sütununda olanlar bölgeye
// dahildir, diğerleri değildir; şerit yer değiştirmez ve kalınlaşmaz.
public sealed class EdgeBandZone
{
    private readonly List<GridPosition> cells = new();
    private bool byColumn;
    private int from, to, gridHeight;

    public bool IsChosen { get; private set; }
    public IReadOnlyList<GridPosition> Cells => cells;
    public bool Covers(GridPosition cell) => IsChosen && (byColumn ? cell.x >= from && cell.x <= to : cell.z >= from && cell.z <= to);

    // Harita koordinatlarıyla (RoundMapUI): sütun harfleri A…, satır 1 = en üst.
    public string Name
    {
        get
        {
            if (!IsChosen) return "şeritte";
            if (byColumn)
                return from == to ? $"{RoundMapUI.ColumnName(from)} sütununda" : $"{RoundMapUI.ColumnName(from)}–{RoundMapUI.ColumnName(to)} sütunlarında";
            int top = gridHeight - to, bottom = gridHeight - from;
            return top == bottom ? $"{top}. satırda" : $"{top}–{bottom}. satırlarda";
        }
    }

    public bool TryChoose(SegmentEventContext context, float coverage, System.Random rng)
    {
        if (IsChosen) return true;
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
        AddEdges(candidates, true, minX, maxX, coverage);
        AddEdges(candidates, false, minZ, maxZ, coverage);
        if (candidates.Count == 0) return false;

        // Hazırlık anındaki bütün üretim noktalarını kapsayan şerit seçilmez (başka seçenek varsa).
        if (context.ProductionPoints.Count > 0)
        {
            var spared = candidates.FindAll(c => !CoversAll(c, context.ProductionPoints));
            if (spared.Count > 0) candidates = spared;
        }

        var pick = candidates[rng.Next(candidates.Count)];
        byColumn = pick.column;
        from = pick.from;
        to = pick.to;
        gridHeight = context.GridHeight;

        cells.Clear();
        for (int x = 0; x < context.GridWidth; x++)
        for (int z = 0; z < context.GridHeight; z++)
        {
            int value = byColumn ? x : z;
            if (value >= from && value <= to) cells.Add(new GridPosition(x, z));
        }
        IsChosen = true;
        return true;
    }

    // Kenar şeritleri: kalınlık = kenar × coverage, en az 1, en fazla kenar − 1 (açık alanın tamamı asla).
    private static void AddEdges(List<(bool, int, int)> list, bool column, int min, int max, float coverage)
    {
        int side = max - min + 1;
        if (side < 2) return;
        int thickness = System.Math.Min(side - 1, System.Math.Max(1, (int)System.Math.Round(side * coverage, System.MidpointRounding.AwayFromZero)));
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
