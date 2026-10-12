using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Boss ödülünün çalışma zamanı etkisi (Bölüm 3.8). Veri (bu asset: prototip sayılar) ile oynanış etkisi (RunEffect) ayrıdır;
// ödülün sunulması ve alınması BossRewardManager'da, etkilerin yürütülmesi RunEffects'tedir. Her yeni ödül kendi küçük sınıfıdır:
// yöneticilere ödüle özel alan ya da dal eklenmez. Sayılar tek kaynaktan üretilir (Tools/Balance/GecOyunV1).
public abstract class RunEffectSO : ScriptableObject
{
    public const int NoPlacement = -1;

    // Ödül kartındaki etki satırı (değerler bu veriden yazılır).
    public abstract string Summary { get; }
    public abstract RunEffect Create();

    // Şu an işlev görebilir mi: teklif hazırlanırken ve ödül alınırken sorulur. Ön koşul ödülü ayrıca aranır (BossRewardSO.requires).
    public virtual bool CanTake(RunEffects host) => true;

    // Yerleşim isteyen etki (anten): geçerli konum kimlikleri, konumun dünyadaki yeri ve etkileyeceği hücreler (önizleme).
    public virtual bool NeedsPlacement => false;
    public virtual string PlacementPrompt => "";
    public virtual void CollectPlacements(RunEffects host, List<int> into) => into.Clear();
    public virtual bool IsValidPlacement(RunEffects host, int placement) => !NeedsPlacement;
    public virtual Vector3 PlacementPosition(RunEffects host, int placement) => Vector3.zero;
    public virtual void PlacementCells(RunEffects host, int placement, List<GridPosition> into) => into.Clear();

    protected static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    protected static string Percent(float ratio) => Mathf.RoundToInt(ratio * 100f).ToString(CultureInfo.InvariantCulture);
}

// Alınmış bir etkinin bu run'daki durumu. RunEffects kurar, çağırır ve temizler. Kancalar somut ihtiyaçlardan çıkar: doğrudan
// saldırının başı ve sonu, normal davranış tetiği, kare adımı, gecikmeli iş, round ve run sınırları.
public abstract class RunEffect
{
    public RunEffectSO Data { get; internal set; }
    public RunEffects Host { get; internal set; }

    // false: etki kurulamadı (ön koşul yok, konum geçersiz); hiçbir şey bırakmadan döner ve ödül alınmaz.
    public virtual bool OnAdded(int placement) => true;
    // Etkinin eklediği her şeyi (geçici stat, kilit, başka etkiye kurduğu ayar) kaynağına göre geri alır. İki kez çağrılabilir.
    public virtual void OnRemoved() { }
    public virtual void OnRoundStarted() { }
    public virtual void OnRoundEnded() { }
    public virtual void AttackBegin(DirectAttackReport attack, ref float multiplier) { }
    public virtual void AttackEnd(DirectAttackReport attack) { }
    public virtual void BehaviorTriggered(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position) { }
    public virtual void Tick(float deltaTime) { }
    public virtual void OnTimer(int kind, int a, int b, int c) { }
    // Play test penceresi: aktif durum, bekleme süresi ve bütçe (yalnız okuma).
    public virtual void Describe(StringBuilder text) { }
}

// Açık tarlanın sınırları (grid koordinatı). Açık alan grid'in ortasındaki karedir (GridUnlockManager); sınırlar yine de açık
// hücrelerden okunur.
public readonly struct FieldBounds
{
    public readonly int MinX, MaxX, MinZ, MaxZ;

    public FieldBounds(int minX, int maxX, int minZ, int maxZ) { MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; }

    public int Width => MaxX - MinX + 1;
    public int Height => MaxZ - MinZ + 1;
    public bool Contains(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    public bool Contains(GridPosition cell) => Contains(cell.x, cell.z);
    // Simetri merkezi açık alanın ortasıdır: hücre, merkeze göre nokta yansımasıyla eşlenir (Hasat Yankısı).
    public GridPosition Mirror(GridPosition cell) => new GridPosition(MinX + MaxX - cell.x, MinZ + MaxZ - cell.z);
    public GridPosition Clamp(GridPosition cell) => new GridPosition(Mathf.Clamp(cell.x, MinX, MaxX), Mathf.Clamp(cell.z, MinZ, MaxZ));
}

// Tarla hücresi sorguları: ödül etkileri ve tarla olayları aynı taramayı ve aynı "canlı bitki" tanımını paylaşır.
public static class FieldCells
{
    public static GridSystem Grid => GridManager.Instance != null ? GridManager.Instance.GetGridSystem() : null;
    public static float CellSize => GridManager.Instance != null ? GridManager.Instance.GetCellSize() : 2f;

    public static long Key(GridPosition cell) => CellOutlineMesh.Key(cell.x, cell.z);

    public static GroundCell Ground(GridSystem grid, GridPosition cell) => grid?.GetGridObject(cell)?.GetGroundCellCached();

    // Açık (kilitsiz) tarla hücresi mi. Tarla dışı ve kilitli hücre hiçbir etkinin hedefi olamaz.
    public static bool IsOpen(GridSystem grid, GridPosition cell)
    {
        GroundCell ground = Ground(grid, cell);
        return ground != null && !ground.IsLocked;
    }

    // Açık hücredeki canlı bitki (yoksa null).
    public static PlantHealth LivingPlant(GridSystem grid, GridPosition cell)
    {
        GridObject entry = grid?.GetGridObject(cell);
        GroundCell ground = entry?.GetGroundCellCached();
        if (ground == null || ground.IsLocked) return null;
        GameObject plant = entry.GetPlantObject();
        return plant != null && plant.TryGetComponent(out PlantHealth health) && !health.IsDead ? health : null;
    }

    // Hücrenin dünyadaki yeri: zemin nesnesi varsa onun konumu, yoksa grid formülü (tarla dışı kenar konumları için).
    public static Vector3 World(GridSystem grid, GridPosition cell)
    {
        GroundCell ground = Ground(grid, cell);
        return ground != null ? ground.transform.position : grid.GetWorldPosition(cell.x, cell.z);
    }

    public static bool TryBounds(out FieldBounds bounds)
    {
        bounds = default;
        GridManager manager = GridManager.Instance;
        GridSystem grid = manager != null ? manager.GetGridSystem() : null;
        if (grid == null) return false;
        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
        int width = manager.GetWidth(), height = manager.GetHeight();
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            if (!IsOpen(grid, new GridPosition(x, z))) continue;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }
        if (maxX < minX) return false;
        bounds = new FieldBounds(minX, maxX, minZ, maxZ);
        return true;
    }

    // from hücresine en yakın canlı bitki (hücre uzaklığı ≤ range; from'un kendisi sayılmaz). Eşit uzaklıkta tarama sırası
    // (önce x, sonra z, küçükten büyüğe) belirler: sonuç rastgele değildir. excluded: bu saldırının doğrudan vurduğu hücreler;
    // used: bu saldırıda zaten seçilmiş hücreler (ikisi de boş bırakılabilir).
    public static PlantHealth Nearest(GridSystem grid, GridPosition from, int range, DirectAttackReport excluded, HashSet<long> used,
        out GridPosition found)
    {
        found = from;
        PlantHealth best = null;
        int bestDistance = int.MaxValue;
        for (int dx = -range; dx <= range; dx++)
        for (int dz = -range; dz <= range; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            int distance = dx * dx + dz * dz;
            if (distance >= bestDistance) continue;
            var cell = new GridPosition(from.x + dx, from.z + dz);
            if (used != null && used.Contains(Key(cell))) continue;
            PlantHealth plant = LivingPlant(grid, cell);
            if (plant == null) continue;
            if (excluded != null && excluded.Covers(grid.GetGridObject(cell))) continue;
            best = plant;
            bestDistance = distance;
            found = cell;
        }
        return best;
    }
}
