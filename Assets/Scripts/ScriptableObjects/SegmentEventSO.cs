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

    public abstract SegmentEventRuntime CreateRuntime(int segment, int segmentRounds);
}

// Olayın hazırlığı için tarlanın o anki durumu.
public sealed class SegmentEventContext
{
    public int GridWidth, GridHeight;
    public readonly List<GridPosition> OpenCells = new();
    public readonly List<GridPosition> ProductionPoints = new();
}

// Bir run'daki tek olay örneği. Bölge bir kez seçilir (TryPrepare); önizleme ve uygulama aynı bölgeyi kullanır.
public abstract class SegmentEventRuntime
{
    public SegmentEventSO Data { get; }
    public int Segment { get; }
    public int StartRound { get; }
    public int EndRound { get; }
    // Bir önceki segmentin ilk round'u: bölge o zaman seçilip gösterilir.
    public int AnnounceRound { get; }
    public bool IsPrepared { get; protected set; }
    public bool IsActive { get; private set; }
    public bool IsFinished { get; private set; }

    protected SegmentEventRuntime(SegmentEventSO data, int segment, int segmentRounds)
    {
        Data = data;
        Segment = Mathf.Max(1, segment);
        StartRound = (Segment - 1) * segmentRounds + 1;
        EndRound = Segment * segmentRounds;
        AnnounceRound = Mathf.Max(1, StartRound - segmentRounds);
    }

    public abstract bool TryPrepare(SegmentEventContext context);
    public abstract bool Covers(GridPosition cell);
    // Arayüz için: bölgedeki hücreler (açık olup olmadıklarına arayüz bakar).
    public abstract IReadOnlyList<GridPosition> ZoneCells { get; }
    public abstract string RuleSummary { get; }
    public virtual float SpawnIntervalMultiplier(GridPosition cell) => 1f;

    public void Activate() => IsActive = true;

    public void Finish()
    {
        IsActive = false;
        IsFinished = true;
    }
}
