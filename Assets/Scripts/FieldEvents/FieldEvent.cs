using System.Collections.Generic;
using UnityEngine;

// Tarla olaylarının ortak yaşam döngüsü (Bölüm 3.8):
//   Bekleme → Hazırlık (bölge işaretli, etki yok) → Ortaya çıkış (hedefler belirir) → Aktif (etki sürer, hedefler vurulabilir)
//   → Çözülme (etki kalkmış; yalnız sunum) → Bitti (temizlik).
// - Bölge ve hedefler olay kurulurken BİR KEZ seçilir (TryPrepare); hazırlıkta gösterilen ile uygulanan aynıdır.
// - Fazlar oyun zamanıyla ilerler (FieldEventDirector.Update). Mantık hiçbir animasyonun, parçacığın ya da prefabın bitmesini
//   beklemez; sunum yalnız olayları (FieldEventDirector.Cued) dinler.
// - Temizlik tek yoldan ve idempotenttir (Cleanup): olayın eklediği üretim etkileri kaynağına göre kaldırılır. Saksı, bitki,
//   tile modifier'ı ve yerleşim hiçbir aşamada silinmez ya da değiştirilmez.
// - Müdahale hedefleri (InterventionTarget) yalnız oyuncunun DOĞRUDAN saldırısıyla vurulur.
public enum FieldEventPhase { Waiting, Preparing, Emerging, Active, Resolving, Done }

public enum FieldEventCueKind
{
    Prepared, Emerged, Activated, TargetHit, TargetBroken, ZoneClosed, ZoneOpened, Spread, Moved, Progress, Bloom, Resolved, Cleaned,
}

// Sunum olayı: Index olaya göre hedef ya da bölge sırası; Cell ilgili hücre; Value sayı (kalan dayanıklılık, canlanan nokta …).
public readonly struct FieldEventCue
{
    public readonly FieldEvent Event;
    public readonly FieldEventCueKind Kind;
    public readonly int Index, Value;
    public readonly GridPosition Cell;
    public FieldEventCue(FieldEvent source, FieldEventCueKind kind, int index = 0, GridPosition cell = default, int value = 0)
    { Event = source; Kind = kind; Index = index; Cell = cell; Value = value; }
}

public abstract class FieldEvent
{
    private float startDelay;
    private bool cleaned;

    protected FieldEventDirector Host { get; private set; }
    protected System.Random Rng { get; private set; }

    public abstract FieldEventKind Kind { get; }
    public abstract string Title { get; }
    // Arayüzdeki tek satırlık durum (faza göre): oyuncuya ne olacağını ve ne yapabileceğini söyler.
    public abstract string Status { get; }
    protected abstract FieldEventTiming Timing { get; }
    // Aktif fazın üst sınırı (sn). Dolunca olay kendiliğinden çözülür.
    protected virtual float ActiveSeconds => Timing.activeSeconds;

    public FieldEventPhase Phase { get; private set; } = FieldEventPhase.Waiting;
    // Bulunulan fazda geçen oyun süresi.
    public float PhaseTime { get; private set; }
    public bool ResolvedByPlayer { get; private set; }
    public readonly List<InterventionTarget> Targets = new(2);
    public float ActiveRemaining => Phase == FieldEventPhase.Active ? Mathf.Max(0f, ActiveSeconds - PhaseTime) : 0f;
    // Sunum için salt okunur: hazırlığın bitmesine kalan süre (ör. çıkıştan hemen önceki bekleme animasyonu).
    public float PrepareRemaining => Phase == FieldEventPhase.Preparing ? Mathf.Max(0f, Timing.prepareSeconds - PhaseTime) : 0f;

    // Sunum ve test: olayın bölgeleri (hücre listeleri). Çoğu olayda tek bölge; Aşılı Kabuk'ta iki.
    public abstract int ZoneCount { get; }
    public abstract IReadOnlyList<GridPosition> Zone(int index);
    // Bölge şu an üretime kapalı mı (kapatan olaylar için).
    public virtual bool ZoneClosed(int index) => false;

    // Kurulum: bölge ve hedefler seçilir. false: uygun yer yok (olay hiç başlamaz, hiçbir etki bırakmaz).
    internal bool Begin(FieldEventDirector host, System.Random rng, float delay, FieldSnapshot field)
    {
        Host = host;
        Rng = rng;
        startDelay = Mathf.Max(0f, delay);
        return TryPrepare(field);
    }

    protected abstract bool TryPrepare(FieldSnapshot field);
    protected virtual void OnEmerged() { }
    protected virtual void OnActivated() { }
    protected virtual void OnActiveTick(float deltaTime) { }
    protected virtual void OnTargetBroken(int index) { }
    // Aktifken biten her doğrudan saldırı (hedef sayımından sonra).
    protected virtual void OnAttack(DirectAttackReport attack) { }
    // Etki burada kalkar (çözülme ve temizlik aynı geri almayı çağırır; iki kez çağrılabilir).
    protected abstract void RemoveEffects();

    internal void Tick(float deltaTime)
    {
        if (Phase == FieldEventPhase.Done) return;
        PhaseTime += deltaTime;
        switch (Phase)
        {
            case FieldEventPhase.Waiting:
                if (PhaseTime >= startDelay) Enter(FieldEventPhase.Preparing);
                break;
            case FieldEventPhase.Preparing:
                if (PhaseTime >= Timing.prepareSeconds) Enter(FieldEventPhase.Emerging);
                break;
            case FieldEventPhase.Emerging:
                if (PhaseTime >= Timing.emergeSeconds) Enter(FieldEventPhase.Active);
                break;
            case FieldEventPhase.Active:
                OnActiveTick(deltaTime);
                if (Phase == FieldEventPhase.Active && PhaseTime >= ActiveSeconds) Resolve(false);
                break;
            case FieldEventPhase.Resolving:
                if (PhaseTime >= Timing.resolveSeconds) Cleanup();
                break;
        }
    }

    private void Enter(FieldEventPhase next)
    {
        Phase = next;
        PhaseTime = 0f;
        switch (next)
        {
            case FieldEventPhase.Preparing:
                Emit(FieldEventCueKind.Prepared);
                break;
            case FieldEventPhase.Emerging:
                OnEmerged();
                Emit(FieldEventCueKind.Emerged);
                break;
            case FieldEventPhase.Active:
                SetHittable(true);
                OnActivated();
                Emit(FieldEventCueKind.Activated);
                break;
        }
    }

    // Aktif faz biter: etki hemen kalkar, çözülme sunumu başlar. byPlayer: oyuncunun müdahalesiyle mi (yoksa süre mi doldu).
    protected void Resolve(bool byPlayer)
    {
        if (Phase != FieldEventPhase.Active) return;
        ResolvedByPlayer = byPlayer;
        Phase = FieldEventPhase.Resolving;
        PhaseTime = 0f;
        SetHittable(false);
        RemoveEffects();
        Emit(FieldEventCueKind.Resolved, byPlayer ? 1 : 0);
    }

    // Tek temizlik yolu: fazın bitmesi, round sonu, kayıp, zafer, menü, yeniden başlatma ve yeni olayın başlaması. İki kez çağrılabilir.
    internal void Cleanup()
    {
        if (cleaned) return;
        cleaned = true;
        SetHittable(false);
        RemoveEffects();
        Phase = FieldEventPhase.Done;
        Emit(FieldEventCueKind.Cleaned);
    }

    private void SetHittable(bool hittable)
    {
        for (int i = 0; i < Targets.Count; i++) Targets[i].Hittable = hittable && !Targets[i].Broken;
    }

    // Oyuncunun bir doğrudan saldırısı bitti. Hedefler liste sırasıyla değerlendirilir: aynı saldırı iki hedefi birden kırabilecekse
    // önce dizini küçük olan kırılır (Aşılı Kabuk'ta sonucu o belirler). blocked: imleç arayüzün üstündeyse hedef sayılmaz.
    internal void OnDirectAttack(DirectAttackReport attack, bool blocked)
    {
        if (Phase != FieldEventPhase.Active) return;
        if (!blocked)
        {
            float cellSize = FieldCells.CellSize;
            for (int i = 0; i < Targets.Count && Phase == FieldEventPhase.Active; i++)
            {
                InterventionTarget target = Targets[i];
                if (!target.TryHit(attack, cellSize)) continue;
                Emit(target.Broken ? FieldEventCueKind.TargetBroken : FieldEventCueKind.TargetHit, i, target.Cell, target.Remaining);
                if (target.Broken) OnTargetBroken(i);
            }
        }
        if (Phase == FieldEventPhase.Active) OnAttack(attack);
    }

    protected void Emit(FieldEventCueKind kind, int index = 0, GridPosition cell = default, int value = 0) =>
        Host.Emit(new FieldEventCue(this, kind, index, cell, value));
}

// Müdahale hedefi (kök, ana filiz, kabuk düğümü). Mantıksal bir hedeftir: collider, PlantHealth ya da IDamageable değildir.
// - Yalnız oyuncunun doğrudan saldırısı sayılır: saldırının dairesi hedefin hücresine değiyorsa (bitkilerle aynı temas kuralı)
//   dayanıklılık 1 azalır. Davranış, artçı, zincir ve ödül etkisi vuruşları hedefe hiç ulaşmaz (bu yoldan geçmezler).
// - Bir saldırı bir hedefi en çok bir kez sayar (saldırının sıra numarası): hedefin görselinde kaç collider olursa olsun.
// - Dayanıklılık "kaç doğrudan saldırı" demektir; hasar miktarına bağlı değildir (oyuncunun gücüyle ölçeklenmez).
public sealed class InterventionTarget
{
    public readonly GridPosition Cell;
    public readonly Vector3 Position;
    public readonly int Durability;
    private int lastSerial = -1;

    public int Remaining { get; private set; }
    public int HitsTaken => Durability - Remaining;
    public bool Hittable { get; internal set; }
    public bool Broken => Remaining <= 0;

    public InterventionTarget(GridPosition cell, Vector3 position, int durability)
    {
        Cell = cell;
        Position = position;
        Durability = Mathf.Max(1, durability);
        Remaining = Durability;
    }

    internal bool TryHit(DirectAttackReport attack, float cellSize)
    {
        if (!Hittable || Broken || attack.Serial == lastSerial) return false;
        if (!HarvestArea.TouchesCell(attack.Center, Position, attack.Radius, cellSize)) return false;
        lastSerial = attack.Serial;
        Remaining--;
        if (Broken) Hittable = false;
        return true;
    }
}

// Tarlanın olay kurulurken okunan durumu: açık alan ve üretim noktalarının hücreleri (x, sonra z sırasıyla: seçim, üretim
// noktalarının kayıt sırasından bağımsızdır).
public sealed class FieldSnapshot
{
    public GridSystem Grid;
    public FieldBounds Bounds;
    public readonly List<GridPosition> Production = new();
    private readonly HashSet<long> productionSet = new();

    public bool IsProduction(GridPosition cell) => productionSet.Contains(FieldCells.Key(cell));

    public bool Read()
    {
        Grid = FieldCells.Grid;
        Production.Clear();
        productionSet.Clear();
        if (Grid == null || !FieldCells.TryBounds(out Bounds)) return false;
        foreach (PlantSpawner spawner in PlantSpawner.Active)
        {
            if (spawner.GridObject == null) continue;
            GridPosition cell = spawner.GridObject.GetGridPosition();
            if (FieldCells.IsOpen(Grid, cell) && productionSet.Add(FieldCells.Key(cell))) Production.Add(cell);
        }
        Production.Sort(ByCell);
        return true;
    }

    public static int ByCell(GridPosition a, GridPosition b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);
}

// Dikdörtgen bölge adayları (Diken Kuşatması, Aşılı Kabuk, Gezgin Bereket). Aday sayısı sonludur (açık alan en çok 11 × 11):
// arama döngüsü yoktur; uygun aday yoksa boş liste döner.
public static class FieldZones
{
    // Açık alanın içinde kalan, bütün hücreleri açık, en az bir üretim noktası içeren ve üretim noktalarının en çok 'maxShare'
    // kadarını (hiçbir zaman tamamını değil) kaplayan w × h dikdörtgenler. maxShare ≥ 1: pay sınırı yok (yalnız "tamamı değil").
    public static void Candidates(FieldSnapshot field, int width, int height, float maxShare, bool needsProduction, List<RectInt> into)
    {
        FieldBounds b = field.Bounds;
        int total = field.Production.Count;
        int limit = maxShare >= 1f ? total - 1 : Mathf.Min(total - 1, Mathf.FloorToInt(total * maxShare + 1e-4f));
        for (int x = b.MinX; x + width - 1 <= b.MaxX; x++)
        for (int z = b.MinZ; z + height - 1 <= b.MaxZ; z++)
        {
            var rect = new RectInt(x, z, width, height);
            if (!AllOpen(field.Grid, rect)) continue;
            int production = ProductionIn(field, rect);
            if (needsProduction && (production < 1 || production > limit)) continue;
            into.Add(rect);
        }
    }

    public static bool AllOpen(GridSystem grid, RectInt rect)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
        for (int z = rect.yMin; z < rect.yMax; z++)
            if (!FieldCells.IsOpen(grid, new GridPosition(x, z))) return false;
        return true;
    }

    public static int ProductionIn(FieldSnapshot field, RectInt rect)
    {
        int count = 0;
        for (int i = 0; i < field.Production.Count; i++)
            if (Contains(rect, field.Production[i])) count++;
        return count;
    }

    public static bool Contains(RectInt rect, GridPosition cell) => cell.x >= rect.xMin && cell.x < rect.xMax && cell.z >= rect.yMin && cell.z < rect.yMax;

    public static void Cells(RectInt rect, List<GridPosition> into)
    {
        into.Clear();
        for (int x = rect.xMin; x < rect.xMax; x++)
        for (int z = rect.yMin; z < rect.yMax; z++) into.Add(new GridPosition(x, z));
    }
}

// Tarla olaylarının üretim etkileri: hücre (üretim noktası) başına kapatma sayacı ve yavaşlatma çarpanı.
// - Etki ÜRETİM NOKTASINA uygulanır, saksının tamamına değil: çok hücreli saksının yalnız bölgedeki üretim noktaları etkilenir
//   (Don Cephesi'yle aynı kural). Bir üretim noktası tek hücrededir: aynı etki ona iki kez uygulanamaz.
// - Kapatma sayaçlıdır: bir kaynağın kaldırması başka kaynağın kapatmasını açmaz. Boss kurallarının (Don, Sert Kabuk, Sis) etkisi
//   ayrı bir sistemdedir (SegmentEventDirector); ikisi ayrı sorulur, biri kalkınca diğeri aynen sürer.
public sealed class FieldZoneEffects
{
    private int width, height, blocked, slowed;
    private int[] blocks = System.Array.Empty<int>();
    private float[] slow = System.Array.Empty<float>();

    public int BlockedCells => blocked;
    public int SlowedCells => slowed;

    public void Resize(int gridWidth, int gridHeight)
    {
        if (gridWidth == width && gridHeight == height) return;
        width = gridWidth;
        height = gridHeight;
        blocks = new int[width * height];
        slow = new float[width * height];
        blocked = slowed = 0;
    }

    private int Index(GridPosition cell) => cell.x >= 0 && cell.z >= 0 && cell.x < width && cell.z < height ? cell.x * height + cell.z : -1;

    public bool Blocks(GridPosition cell)
    {
        if (blocked == 0) return false;
        int index = Index(cell);
        return index >= 0 && blocks[index] > 0;
    }

    public float Slow(GridPosition cell)
    {
        if (slowed == 0) return 1f;
        int index = Index(cell);
        return index >= 0 && slow[index] > 0f ? slow[index] : 1f;
    }

    public void AddBlock(IReadOnlyList<GridPosition> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            int index = Index(cells[i]);
            if (index >= 0 && blocks[index]++ == 0) blocked++;
        }
    }

    public void RemoveBlock(IReadOnlyList<GridPosition> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            int index = Index(cells[i]);
            if (index >= 0 && blocks[index] > 0 && --blocks[index] == 0) blocked--;
        }
    }

    // Hücrenin yavaşlatma çarpanı (1'den büyük). Aynı hücreye ikinci kez yazılırsa çarpılmaz, değeri değişir.
    public void SetSlow(GridPosition cell, float multiplier)
    {
        int index = Index(cell);
        if (index < 0 || !(multiplier > 1f) || float.IsInfinity(multiplier)) return;
        if (slow[index] <= 0f) slowed++;
        slow[index] = multiplier;
    }

    public void ClearSlow(GridPosition cell)
    {
        int index = Index(cell);
        if (index < 0 || slow[index] <= 0f) return;
        slow[index] = 0f;
        slowed--;
    }

    public void Clear()
    {
        System.Array.Clear(blocks, 0, blocks.Length);
        System.Array.Clear(slow, 0, slow.Length);
        blocked = slowed = 0;
    }
}
