using System.Collections.Generic;
using UnityEngine;

public class PlanterBrain : MonoBehaviour
{
    [SerializeField] private PlanterSO planterData;
    [Tooltip("Bos birakilirsa her dolu hucrenin merkezinde bir bitki noktasi olusturulur.")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private float automaticSpawnHeight = 0.25f;

    private List<PlantSpawner> spawners = new List<PlantSpawner>();
    private List<StatModifier> localModifiers = new List<StatModifier>();
    private List<GridObject> occupiedGrids = new List<GridObject>();

    // Cache
    private Dictionary<StatType, float> statCache = new();
    private int cachedVersion = -1;
    private bool localDirty = true;

    public List<StatModifier> LocalModifiers => localModifiers; 

    public void Initialize(List<GridObject> gridObjects)
    {
        if (spawners.Count > 0) return;
        if (!TryResolveSpawnGrids(gridObjects, out List<GridObject> spawnGrids, out string error))
        {
            Debug.LogError(error, this);
            return;
        }
        occupiedGrids = new List<GridObject>(gridObjects);
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            foreach (GridObject grid in gridObjects)
            {
                GroundCell cell = grid.GetGroundCellCached();
                if (cell != null)
                    CreateSpawner(cell.transform.position + Vector3.up * automaticSpawnHeight, grid);
            }
            return;
        }
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            CreateSpawner(spawnPoints[i].position, spawnGrids[i]);
        }
    }

    public bool ValidateSpawnPoints(List<GridObject> gridObjects, out string error)
    {
        return TryResolveSpawnGrids(gridObjects, out _, out error);
    }

    // Resolve every point before creating any spawners. Never redirect a duplicate
    // to a different cell: its visual position would no longer match its grid.
    private bool TryResolveSpawnGrids(List<GridObject> gridObjects,
        out List<GridObject> spawnGrids, out string error)
    {
        spawnGrids = new List<GridObject>();
        error = null;
        if (spawnPoints == null || spawnPoints.Count == 0) return true;

        Dictionary<GridObject, int> assignedPoints = new Dictionary<GridObject, int>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform point = spawnPoints[i];
            if (point == null)
            {
                error = $"[{name}] Yerleştirme durduruldu: Spawn Points listesinin {i + 1}. elemanı boş. " +
                    "Noktayı bağla veya otomatik noktalar için listenin tamamını boşalt.";
                return false;
            }

            GridObject closest = FindClosestGridObject(point.position, gridObjects);
            if (closest == null)
            {
                error = $"[{name}] Yerleştirme durduruldu: '{point.name}' (nokta {i + 1}) için zemin hücresi bulunamadı.";
                return false;
            }
            if (assignedPoints.TryGetValue(closest, out int previous))
            {
                GridPosition cell = closest.GetGroundCellCached().GetGridPosition();
                error = $"[{name}] Yerleştirme durduruldu: '{spawnPoints[previous].name}' (nokta {previous + 1}) ve " +
                    $"'{point.name}' (nokta {i + 1}) aynı ({cell.x}, {cell.z}) hücresine denk geliyor. " +
                    "Başka hücreye aktarılmadı. Prefabta noktaları ayrı hücre merkezlerine taşı " +
                    "veya Spawn Points listesini boşaltarak otomatik noktaları kullan.";
                return false;
            }
            assignedPoints.Add(closest, i);
            spawnGrids.Add(closest);
        }
        return true;
    }

    public void Initialize(PlanterSO data, List<GridObject> gridObjects)
    {
        planterData = data;
        Initialize(gridObjects);
    }

    private void CreateSpawner(Vector3 position, GridObject grid)
    {
        GameObject spawnerObj = new GameObject("PlantSpawner_" + spawners.Count);
        spawnerObj.transform.position = position;
        spawnerObj.transform.SetParent(transform);
        PlantSpawner spawner = spawnerObj.AddComponent<PlantSpawner>();
        spawner.Initialize(planterData, grid, this);
        spawners.Add(spawner);
    }

    public void ApplyBuff(TileModifierSO tileModifier, List<StatModifier> rolledModifiers)
    {
        if (tileModifier == null || rolledModifiers == null) return;

        foreach (StatModifier modifier in rolledModifiers)
            localModifiers.Add(modifier);

        localDirty = true;

        Debug.Log($"PlanterBrain buff aldı: {tileModifier.modifierName}");
    }

    public float GetFinalStat(StatType statType)
    {
        int currentVersion = StatManager.Instance.GlobalVersion;

        if (currentVersion != cachedVersion || localDirty)
        {
            statCache.Clear();
            cachedVersion = currentVersion;
            localDirty = false;
        }

        if (statCache.TryGetValue(statType, out float cached))
            return cached;

        float result = StatCalculator.Calculate(
            planterData.GetBaseStat(statType),
            statType,
            StatTarget.Planter,
            StatManager.Instance.GlobalModifiers,
            localModifiers
        );

        statCache[statType] = result;
        return result;
    }

    public void RemoveSelf()
    {
        Debug.Log($"[REMOVE] {planterData.planterName} siliniyor. occupiedGrids sayısı: {occupiedGrids.Count}");

        foreach (GridObject gridObj in occupiedGrids)
        {
            GridPosition pos = gridObj.GetGroundCellCached()?.GetGridPosition() ?? default;
            Debug.Log($"[REMOVE] Temizleniyor: {pos}, HasPlanter önce: {gridObj.HasPlanterObject()}");
            gridObj.ClearPlanterObject();
            Debug.Log($"[REMOVE] Temizlendi: {pos}, HasPlanter sonra: {gridObj.HasPlanterObject()}");
        }

        int refund = planterData.cost / 2;
        ResourceManager.Instance.AddResource(planterData.costType, refund);
        Destroy(gameObject);
    }

    private GridObject FindClosestGridObject(Vector3 worldPos, List<GridObject> gridObjects)
    {
        GridObject closest = null;
        float minDist = float.MaxValue;

        foreach (GridObject gridObj in gridObjects)
        {
            GroundCell cell = gridObj.GetGroundCellCached();
            if (cell == null) continue;

            Vector3 delta = worldPos - cell.transform.position;
            float dist = delta.x * delta.x + delta.z * delta.z;
            if (dist < minDist)
            {
                minDist = dist;
                closest = gridObj;
            }
        }

        return closest;
    }

    public void TryExplode(GridObject sourceGrid)
    {
        float chance = GetFinalStat(StatType.ExplosionChance);
        if (chance <= 0f) return;
        if (Random.value > chance) return; // şans tutmadı

        int damage = Mathf.RoundToInt(
            StatManager.Instance.GetFinalStat(StatType.HarvestDamage, StatTarget.Player)
        );

        GridSystem gridSystem = GridManager.Instance.GetGridSystem();
        HashSet<GridObject> hitTargets = new HashSet<GridObject>();

        foreach (GridObject occupiedGrid in occupiedGrids)
        {
            GroundCell cell = occupiedGrid.GetGroundCellCached();
            if (cell == null) continue;

            GridPosition pos = cell.GetGridPosition();

            Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            foreach (Vector2Int dir in directions)
            {
                GridPosition neighborPos = new GridPosition(pos.x + dir.x, pos.z + dir.y);
                GridObject neighbor = gridSystem.GetGridObject(neighborPos);

                if (neighbor == null) continue;
                if (occupiedGrids.Contains(neighbor)) continue;
                if (!hitTargets.Add(neighbor)) continue;

                GameObject plant = neighbor.GetPlantObject();
                if (plant == null) continue;

                IDamageable damageable = plant.GetComponent<IDamageable>();
                damageable?.TakeDamage(damage, true);
            }
        }
    }

    public void SetGridObject(GridObject gridObject)
    {
        Initialize(new List<GridObject> { gridObject });
    }
}
