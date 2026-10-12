using System.Collections.Generic;
using UnityEngine;

// Moving positive region: faster normal spawning and double resource/XP on harvest.
// Bonuses query the current active cell, never remain attached to pooled plants.
public sealed class WanderingBountyEvent : FieldEvent
{
    private static readonly Vector2Int[] Steps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
    private readonly WanderingBountyConfig config;
    private readonly List<GridPosition> zone = new();


    private readonly List<Vector2Int> moves = new(4);
    private GridSystem grid;
    private FieldBounds bounds;
    private RectInt rect;
    private float moveTimer;
    private readonly List<GridPosition> bloomCells = new();
    public IReadOnlyList<GridPosition> BloomCells => bloomCells;
    public const float SpawnSpeed = 2f;
    public float MoveInterval => Mathf.Max(10f,config.moveInterval);
    protected override float ActiveSeconds => float.PositiveInfinity;
    public bool Benefits(GridPosition cell) => Phase==FieldEventPhase.Active && FieldZones.Contains(rect,cell);
    public int BonusHarvests { get; private set; }
    public int AcceleratedSpawns { get; private set; }
    public void RecordHarvest(GridPosition cell)
    {
        if(!Benefits(cell))return;
        BonusHarvests++;
        Emit(FieldEventCueKind.Progress,0,cell,BonusHarvests);
    }
    public void RecordSpawn(GridPosition cell)
    {
        if(!Benefits(cell))return;
        AcceleratedSpawns++;
        bloomCells.Clear();bloomCells.Add(cell);
        Emit(FieldEventCueKind.Bloom,AcceleratedSpawns,cell,1);
    }

    public WanderingBountyEvent(WanderingBountyConfig config) => this.config = config;

    public override FieldEventKind Kind => FieldEventKind.WanderingBounty;
    public override string Title => "GEZGİN BEREKET";
    protected override FieldEventTiming Timing => config.timing;
    public override int ZoneCount => 1;
    public override IReadOnlyList<GridPosition> Zone(int index) => zone;
    public int Moves { get; private set; }
    public override string Status => Phase switch
    {
        FieldEventPhase.Preparing or FieldEventPhase.Emerging => "Bereket geliyor: kaynak ve XP ×2 · üretim ×2 hızlı",
        FieldEventPhase.Active => $"Kaynak ×2 · XP ×2\nBoş yuvalar ×2 hızlı dolar · round boyunca\n{BonusHarvests} bereketli hasat · {AcceleratedSpawns} hızlı doğuş",
        FieldEventPhase.Resolving => $"Bereket bitti · {BonusHarvests} bonuslu hasat\n{AcceleratedSpawns} hızlı doğuş",
        _ => "",
    };

    protected override bool TryPrepare(FieldSnapshot field)
    {
        grid = field.Grid;
        bounds = field.Bounds;
        var candidates = new List<RectInt>();
        for (int size = config.zoneSize; size >= 1 && candidates.Count == 0; size--)
            FieldZones.Candidates(field, size, size, 1f, false, candidates);
        if (candidates.Count == 0) return false;
        rect = candidates[Rng.Next(candidates.Count)];
        FieldZones.Cells(rect, zone);
        return true;
    }

    protected override void OnActiveTick(float deltaTime)
    {
        moveTimer += deltaTime;
        if (moveTimer < MoveInterval) return;
        moveTimer = Mathf.Min(moveTimer - MoveInterval, MoveInterval);
        Move();
    }

    private void Move()
    {
        moves.Clear();
        foreach (Vector2Int step in Steps)
        {
            var next = new RectInt(rect.x + step.x, rect.y + step.y, rect.width, rect.height);
            if (bounds.Contains(next.xMin, next.yMin) && bounds.Contains(next.xMax - 1, next.yMax - 1) && FieldZones.AllOpen(grid, next)) moves.Add(step);
        }
        if (moves.Count == 0) return;
        Vector2Int pick = moves[Rng.Next(moves.Count)];
        rect = new RectInt(rect.x + pick.x, rect.y + pick.y, rect.width, rect.height);
        FieldZones.Cells(rect, zone);
        Moves++;
        Emit(FieldEventCueKind.Moved, 0, new GridPosition(rect.xMin, rect.yMin), Moves);
    }

    // Region-local queries automatically stop applying as soon as the phase or region changes.
    protected override void RemoveEffects() => bloomCells.Clear();
}
