using System;
using System.Collections.Generic;
using UnityEngine;

// Boss havuzu: her segmentin boss'u duyuru anında buradan seçilir (SegmentEventDirector). Yeni boss = yeni SegmentEventSO + bir satır.
// Seçim kuralı:
// - Uygun (IsEligible) ve ağırlığı > 0 olanlar aday olur; bir önceki boss aday dışıdır (art arda tekrar yok).
// - Yalnız bir önceki boss uygunsa o tekrar edilir ve bu arayüzde belirtilir (Repeated).
// - Hiçbiri uygun değilse null döner: yönetici "kuralsız boss" yedeğini kullanır (yalnız boss hasadı hedefi geçerli).
// Rastgelelik çağıranın verdiği System.Random'dan gelir; UnityEngine.Random akışına (hasat, kritik, kart) dokunmaz.
[CreateAssetMenu(menuName = "ClickerGame/Boss Pool", fileName = "BossPool")]
public sealed class BossPoolSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public SegmentEventSO boss;
        [Min(0f)] public float weight;
    }

    public List<Entry> entries = new();

    public SegmentEventSO Pick(SegmentEventContext context, SegmentEventSO previous, System.Random rng, out bool repeated)
    {
        repeated = false;
        var eligible = new List<Entry>();
        foreach (Entry entry in entries)
            if (entry.boss != null && entry.weight > 0f && entry.boss.IsEligible(context)) eligible.Add(entry);
        if (eligible.Count == 0) return null;
        var candidates = eligible.FindAll(e => e.boss != previous);
        if (candidates.Count == 0)
        {
            candidates = eligible;
            repeated = true;
        }
        double total = 0;
        foreach (Entry entry in candidates) total += entry.weight;
        double roll = rng.NextDouble() * total;
        foreach (Entry entry in candidates)
        {
            roll -= entry.weight;
            if (roll < 0) return entry.boss;
        }
        return candidates[candidates.Count - 1].boss;
    }
}

// Yedek: uygun boss kuralı yokken kullanılır. Kural uygulamaz; boss hasadı hedefi yine geçerlidir. Asset değildir.
public sealed class NoRuleBossSO : SegmentEventSO
{
    public static NoRuleBossSO Create()
    {
        var so = CreateInstance<NoRuleBossSO>();
        so.name = "NoRuleBoss";
        so.displayName = "KURALSIZ BOSS";
        so.ruleText = "Uygun boss kuralı yok; yalnız boss hasadı hedefi geçerli.";
        so.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        return so;
    }

    public override SegmentEventRuntime CreateRuntime(SegmentEventTiming timing) => new NoRuleBossEvent(this, timing);
}

public sealed class NoRuleBossEvent : SegmentEventRuntime
{
    private static readonly List<GridPosition> NoCells = new();

    public NoRuleBossEvent(SegmentEventSO data, SegmentEventTiming timing) : base(data, timing) { }

    public override IReadOnlyList<GridPosition> ZoneCells => NoCells;
    public override string RuleSummary => "kural yok";
    public override string EndedText => "Boss round'u bitti";
    public override bool Covers(GridPosition cell) => false;

    public override bool TryPrepare(SegmentEventContext context)
    {
        IsPrepared = true;
        return true;
    }
}
