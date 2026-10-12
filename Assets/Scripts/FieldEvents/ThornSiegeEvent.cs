using System.Collections.Generic;
using UnityEngine;

// Diken Kuşatması (Bölüm 3.8). Önceden işaretlenen sınırlı bir bölgede üretim geçici olarak durur; bölgeye bağlı iki kök,
// oyuncunun doğrudan saldırılarıyla kırılınca bölge açılır.
// - Bölge: açık alanın içinde bir dikdörtgen (veri: boyut). Adaylar arasından eşit olasılıkla seçilir; saksının gücüne ya da
//   türüne bakılmaz. Üretim noktalarının en çok 'maxProductionShare' kadarını kaplar ve hiçbir zaman tamamını kaplamaz; sığmazsa
//   boyut küçültülür, hiç uygun aday yoksa olay başlamaz.
// - Kökler: bölgenin iki karşı köşe hücresindedir (bölge tek hücreyse tek kök). Her biri 'rootDurability' doğrudan saldırı ister.
// - Aktifken bölgedeki üretim noktaları yeni bitki çıkarmaz ve beklerken süre biriktirmez (PlantSpawner). Mevcut bitkiler yerinde
//   kalır ve hasat edilebilir. Saksı, bitki, tile modifier'ı, rezonans ve yerleşim değişmez.
// - İki kök de kırılınca bölge hemen açılır; kırılmazsa aktif süre dolunca kendiliğinden açılır. Açılınca üretim kaldığı yerden
//   sürer: üretim borcu ya da toplu geri ödeme yoktur. Boss kuralının (Don, Sert Kabuk) etkisi ayrı sürer.
public sealed class ThornSiegeEvent : FieldEvent
{
    private readonly ThornSiegeConfig config;
    private readonly List<GridPosition> zone = new();
    private bool closed;

    public ThornSiegeEvent(ThornSiegeConfig config) => this.config = config;

    public override FieldEventKind Kind => FieldEventKind.ThornSiege;
    public override string Title => "DİKEN KUŞATMASI";
    protected override FieldEventTiming Timing => config.timing;
    public override int ZoneCount => 1;
    public override IReadOnlyList<GridPosition> Zone(int index) => zone;
    public override bool ZoneClosed(int index) => closed;
    public int RootsLeft { get { int n = 0; for (int i = 0; i < Targets.Count; i++) if (!Targets[i].Broken) n++; return n; } }

    public override string Status => Phase switch
    {
        FieldEventPhase.Preparing or FieldEventPhase.Emerging => $"İşaretli {zone.Count} hücrede üretim duracak · kökleri kendi vuruşunla kır",
        FieldEventPhase.Active => $"Bölgede üretim durdu · kalan kök {RootsLeft}/{Targets.Count} · {Mathf.CeilToInt(ActiveRemaining)} sn",
        FieldEventPhase.Resolving => ResolvedByPlayer ? "Kökler kırıldı · bölge açıldı" : "Dikenler çekildi · bölge açıldı",
        _ => "",
    };

    protected override bool TryPrepare(FieldSnapshot field)
    {
        var candidates = new List<RectInt>();
        // İstenen boyut (iki yönüyle), sonra her adımda bir hücre küçülen boyutlar: sonlu sayıda deneme.
        for (int shrink = 0; candidates.Count == 0 && shrink < 6; shrink++)
        {
            int w = Mathf.Max(1, config.zoneWidth - shrink), h = Mathf.Max(1, config.zoneHeight - shrink);
            FieldZones.Candidates(field, w, h, config.maxProductionShare, true, candidates);
            if (w != h) FieldZones.Candidates(field, h, w, config.maxProductionShare, true, candidates);
            if (w == 1 && h == 1) break;
        }
        if (candidates.Count == 0) return false;
        RectInt rect = candidates[Rng.Next(candidates.Count)];
        FieldZones.Cells(rect, zone);
        AddRoot(field.Grid, new GridPosition(rect.xMin, rect.yMin));
        if (rect.width * rect.height > 1) AddRoot(field.Grid, new GridPosition(rect.xMax - 1, rect.yMax - 1));
        return true;
    }

    private void AddRoot(GridSystem grid, GridPosition cell) =>
        Targets.Add(new InterventionTarget(cell, FieldCells.World(grid, cell), config.rootDurability));

    protected override void OnActivated()
    {
        Host.Zones.AddBlock(zone);
        closed = true;
        Emit(FieldEventCueKind.ZoneClosed);
    }

    protected override void OnTargetBroken(int index)
    {
        if (RootsLeft == 0) Resolve(true);
    }

    protected override void RemoveEffects()
    {
        if (!closed) return;
        closed = false;
        Host.Zones.RemoveBlock(zone);
        Emit(FieldEventCueKind.ZoneOpened);
    }
}
