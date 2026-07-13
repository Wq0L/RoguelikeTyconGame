using System.Collections.Generic;
using UnityEngine;

public class PlanterBrain : MonoBehaviour
{
    [SerializeField] private PlanterSO planterData;
    [SerializeField] private List<Transform> spawnPoints;

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
        occupiedGrids = new List<GridObject>(gridObjects);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (i >= gridObjects.Count) break;

            GridObject closest = FindClosestGridObject(spawnPoints[i].position, gridObjects);

            GameObject spawnerObj = new GameObject("PlantSpawner_" + i);
            spawnerObj.transform.position = spawnPoints[i].position;
            spawnerObj.transform.SetParent(transform);

            PlantSpawner spawner = spawnerObj.AddComponent<PlantSpawner>();
            spawner.Initialize(planterData, closest, this);

            spawners.Add(spawner);
        }
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
        foreach (GridObject gridObj in occupiedGrids)
        {
            GameObject plant = gridObj.GetPlantObject();
            if (plant != null)
            {
                gridObj.ClearPlantObject();
                Destroy(plant);
            }

            gridObj.ClearPlanterObject();
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

            float dist = Vector3.Distance(worldPos, cell.transform.position);
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