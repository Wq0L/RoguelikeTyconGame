using System.Collections.Generic;
using UnityEngine;

// İstilacı Filiz (Bölüm 3.8). Bir ana filiz çıkar ve zamanla komşu hücrelere yayılır; yayıldığı hücrelerde üretim yavaşlar.
// Ana filiz oyuncunun doğrudan saldırılarıyla kırılınca bu olayın bütün etkileri kalkar.
// - Ana filiz: üretim noktası olan hücreler arasından eşit olasılıkla seçilir (saksının gücüne bakılmaz).
// - Yayılma: her 'spreadInterval' saniyede bir, etkilenmiş hücrelerin dört yönlü komşularından biri etkilenir. Aday hücre açık
//   olmalı, ana filize en çok 'maxRadius' hücre uzakta olmalı ve henüz etkilenmemiş olmalıdır. Adaylar her seferinde mevcut
//   parçalardan türetilir (en çok 4 × parça sayısı): yol bulma, kök simülasyonu ya da arama döngüsü yoktur. Aday yoksa yayılma
//   kalıcı olarak durur.
// - Sınırlar: en çok 'maxPieces' parça (ana filiz dahil), en çok 'spreadSeconds' süre, 'maxRadius' alan.
// - Etki: etkilenen hücredeki üretim noktasının aralığı × 'slowMultiplier'. Çarpan hücre başına bir kez yazılır (üst üste
//   çarpılmaz); boss kuralının yavaşlatması (Don Cephesi) ayrı bir çarpandır ve bağımsız sürer.
// - Kalıcı kayıp yok: saksı, bitki, tile ve yerleşim değişmez; olay bitince bütün hücreler normale döner.
public sealed class InvasiveSproutEvent : FieldEvent
{
    private static readonly Vector2Int[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
    private readonly InvasiveSproutConfig config;
    private readonly List<GridPosition> pieces = new();
    private readonly HashSet<long> pieceSet = new();
    private readonly List<GridPosition> candidates = new();
    private GridSystem grid;
    private GridPosition main;
    private float spreadTimer, spreadElapsed;
    private bool spreading, applied;

    public InvasiveSproutEvent(InvasiveSproutConfig config) => this.config = config;

    public override FieldEventKind Kind => FieldEventKind.InvasiveSprout;
    public override string Title => "İSTİLACI FİLİZ";
    protected override FieldEventTiming Timing => config.timing;
    public override int ZoneCount => 1;
    public override IReadOnlyList<GridPosition> Zone(int index) => pieces;
    public int Pieces => pieces.Count;
    public bool Spreading => spreading;

    public override string Status => Phase switch
    {
        FieldEventPhase.Preparing or FieldEventPhase.Emerging => "Filiz çıkıyor · yayıldığı hücrelerde üretim yavaşlar · ana filizi kendi vuruşunla kır",
        FieldEventPhase.Active => $"Üretim yavaş: {pieces.Count} hücre · ana filiz {Targets[0].Remaining}/{Targets[0].Durability} · {Mathf.CeilToInt(ActiveRemaining)} sn",
        FieldEventPhase.Resolving => ResolvedByPlayer ? "Ana filiz kırıldı · üretim normale döndü" : "Filiz kurudu · üretim normale döndü",
        _ => "",
    };

    protected override bool TryPrepare(FieldSnapshot field)
    {
        if (field.Production.Count == 0) return false;
        grid = field.Grid;
        main = field.Production[Rng.Next(field.Production.Count)];
        pieces.Add(main);
        pieceSet.Add(FieldCells.Key(main));
        Targets.Add(new InterventionTarget(main, FieldCells.World(grid, main), config.mainDurability));
        return true;
    }

    protected override void OnActivated()
    {
        applied = true;
        spreading = true;
        Host.Zones.SetSlow(main, config.slowMultiplier);
    }

    protected override void OnActiveTick(float deltaTime)
    {
        if (!spreading) return;
        spreadElapsed += deltaTime;
        if (spreadElapsed > config.spreadSeconds || pieces.Count >= config.maxPieces) { spreading = false; return; }
        spreadTimer += deltaTime;
        if (spreadTimer < config.spreadInterval) return;
        spreadTimer = Mathf.Min(spreadTimer - config.spreadInterval, config.spreadInterval);
        Spread();
    }

    private void Spread()
    {
        candidates.Clear();
        for (int i = 0; i < pieces.Count; i++)
            foreach (Vector2Int step in Neighbors)
            {
                var cell = new GridPosition(pieces[i].x + step.x, pieces[i].z + step.y);
                if (Mathf.Max(Mathf.Abs(cell.x - main.x), Mathf.Abs(cell.z - main.z)) > config.maxRadius) continue;
                if (pieceSet.Contains(FieldCells.Key(cell)) || !FieldCells.IsOpen(grid, cell) || Listed(cell)) continue;
                candidates.Add(cell);
            }
        // Geçerli hücre yok: yeniden denenmez.
        if (candidates.Count == 0) { spreading = false; return; }
        GridPosition next = candidates[Rng.Next(candidates.Count)];
        pieces.Add(next);
        pieceSet.Add(FieldCells.Key(next));
        Host.Zones.SetSlow(next, config.slowMultiplier);
        Emit(FieldEventCueKind.Spread, pieces.Count - 1, next, pieces.Count);
    }

    private bool Listed(GridPosition cell)
    {
        for (int i = 0; i < candidates.Count; i++) if (candidates[i].x == cell.x && candidates[i].z == cell.z) return true;
        return false;
    }

    protected override void OnTargetBroken(int index) => Resolve(true);

    protected override void RemoveEffects()
    {
        spreading = false;
        if (!applied) return;
        applied = false;
        for (int i = 0; i < pieces.Count; i++) Host.Zones.ClearSlow(pieces[i]);
    }
}
