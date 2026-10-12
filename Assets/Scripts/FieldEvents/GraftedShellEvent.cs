using System.Collections.Generic;
using UnityEngine;

// Aşılı Kabuk (Bölüm 3.8). İki bölge ve her birine bağlı bir hedef vardır. Oyuncu bir hedefi kırarak O HEDEFİN bölgesini
// kurtarır; diğer bölge kapanır.
// - Bölgeler: açık alanda, birbiriyle örtüşmeyen iki dikdörtgen (A ve B), her biri en az bir üretim noktası içerir; ikisi birlikte
//   üretim noktalarının en çok 'maxProductionShare' kadarını kaplar. Adaylardan eşit olasılıkla seçilir.
// - Hazırlık: iki bölge ve sonuç gösterilir; A "varsayılan" bölgedir. Hedefler henüz vurulamaz.
// - Seçim (aktif fazın başı, 'choiceSeconds'): hedefler vurulabilir; her biri 'targetDurability' doğrudan saldırı ister.
//     · İlk kırılan hedef sonucu kesinleştirir: o hedefin bölgesi kurtulur, diğeri kapanır. Sonuç sonradan değişmez; diğer
//       hedef artık vurulamaz.
//     · Aynı saldırı iki hedefi birden kırabilecek durumdaysa hedefler sırayla değerlendirilir: önce A'nın hedefi. Yani o
//       saldırıda A kurtulur, B kapanır (FieldEvent.OnDirectAttack).
//     · Süre dolana kadar hedef kırılmazsa hazırlıkta gösterilen varsayılan bölge (A) kapanır, B kurtulur.
// - Kapanma: kapanan bölgede üretim 'closedSeconds' boyunca durur; kural ve geri alma Diken Kuşatması'yla aynı ortak bileşendir
//   (FieldZoneEffects: üretim noktası başına, sayaçlı, borç yok). Sonra bölge açılır.
public sealed class GraftedShellEvent : FieldEvent
{
    private readonly GraftedShellConfig config;
    private readonly List<GridPosition>[] zones = { new(), new() };
    private int closedIndex = -1;
    private float closedTime;
    private bool blocking;

    public GraftedShellEvent(GraftedShellConfig config) => this.config = config;

    public override FieldEventKind Kind => FieldEventKind.GraftedShell;
    public override string Title => "AŞILI KABUK";
    protected override FieldEventTiming Timing => config.timing;
    protected override float ActiveSeconds => config.choiceSeconds + config.closedSeconds;
    public override int ZoneCount => 2;
    public override IReadOnlyList<GridPosition> Zone(int index) => zones[index];
    public override bool ZoneClosed(int index) => blocking && index == closedIndex;
    // Sonuç: kapanan bölge (0: A, 1: B); karar verilmediyse −1.
    public int ClosedZone => closedIndex;
    public int SavedZone => closedIndex < 0 ? -1 : 1 - closedIndex;
    public bool Decided => closedIndex >= 0;
    public bool DecidedByPlayer { get; private set; }

    public override string Status
    {
        get
        {
            switch (Phase)
            {
                case FieldEventPhase.Preparing:
                case FieldEventPhase.Emerging:
                    return "İki bölgeden biri kapanacak · bir düğümü kır: onun bölgesi kurtulur, diğeri kapanır · seçmezsen A kapanır";
                case FieldEventPhase.Active:
                    if (!Decided)
                        return $"Seç: A düğümü {Targets[0].Remaining}/{Targets[0].Durability} · B düğümü {Targets[1].Remaining}/{Targets[1].Durability} · " +
                               $"{Mathf.CeilToInt(Mathf.Max(0f, config.choiceSeconds - PhaseTime))} sn sonra A kapanır";
                    return $"{Name(SavedZone)} kurtuldu · {Name(closedIndex)} bölgesinde üretim durdu · {Mathf.CeilToInt(Mathf.Max(0f, config.closedSeconds - closedTime))} sn";
                case FieldEventPhase.Resolving:
                    return $"{Name(closedIndex)} bölgesi açıldı";
                default:
                    return "";
            }
        }
    }

    private static string Name(int index) => index == 0 ? "A" : index == 1 ? "B" : "?";

    protected override bool TryPrepare(FieldSnapshot field)
    {
        var candidates = new List<RectInt>();
        var second = new List<RectInt>();
        int total = field.Production.Count;
        int limit = Mathf.Min(total - 1, Mathf.FloorToInt(total * config.maxProductionShare + 1e-4f));
        for (int shrink = 0; shrink < 6; shrink++)
        {
            int w = Mathf.Max(1, config.zoneWidth - shrink), h = Mathf.Max(1, config.zoneHeight - shrink);
            candidates.Clear();
            FieldZones.Candidates(field, w, h, 1f, true, candidates);
            if (w != h) FieldZones.Candidates(field, h, w, 1f, true, candidates);
            // A eşit olasılıkla; B, A ile örtüşmeyen ve pay sınırını aşmayan adaylardan eşit olasılıkla. Sonlu iki seçim.
            if (candidates.Count >= 2)
            {
                RectInt a = candidates[Rng.Next(candidates.Count)];
                int productionA = FieldZones.ProductionIn(field, a);
                second.Clear();
                foreach (RectInt other in candidates)
                    if (!other.Overlaps(a) && productionA + FieldZones.ProductionIn(field, other) <= limit) second.Add(other);
                if (second.Count > 0)
                {
                    RectInt b = second[Rng.Next(second.Count)];
                    FieldZones.Cells(a, zones[0]);
                    FieldZones.Cells(b, zones[1]);
                    AddTarget(field.Grid, a);
                    AddTarget(field.Grid, b);
                    return true;
                }
            }
            if (w == 1 && h == 1) break;
        }
        return false;
    }

    // Hedef, bölgesinin ortasındaki hücrededir.
    private void AddTarget(GridSystem grid, RectInt rect)
    {
        var cell = new GridPosition(rect.xMin + (rect.width - 1) / 2, rect.yMin + (rect.height - 1) / 2);
        Targets.Add(new InterventionTarget(cell, FieldCells.World(grid, cell), config.targetDurability));
    }

    protected override void OnTargetBroken(int index)
    {
        if (!Decided) Decide(index, true);
    }

    protected override void OnActiveTick(float deltaTime)
    {
        if (!Decided)
        {
            // Seçim yapılmadı: hazırlıkta gösterilen varsayılan bölge (A) kapanır.
            if (PhaseTime >= config.choiceSeconds) Decide(1, false);
            return;
        }
        closedTime += deltaTime;
        if (closedTime >= config.closedSeconds) Resolve(DecidedByPlayer);
    }

    private void Decide(int saved, bool byPlayer)
    {
        closedIndex = 1 - saved;
        DecidedByPlayer = byPlayer;
        closedTime = 0f;
        // Sonuç kesin: hedefler artık vurulamaz.
        for (int i = 0; i < Targets.Count; i++) Targets[i].Hittable = false;
        Host.Zones.AddBlock(zones[closedIndex]);
        blocking = true;
        Emit(FieldEventCueKind.ZoneClosed, closedIndex, default, byPlayer ? 1 : 0);
    }

    protected override void RemoveEffects()
    {
        if (!blocking) return;
        blocking = false;
        Host.Zones.RemoveBlock(zones[closedIndex]);
        Emit(FieldEventCueKind.ZoneOpened, closedIndex);
    }
}
