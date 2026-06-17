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

    public void ApplyBuff(TileModifierSO tileModifier)
    {
        if (tileModifier == null) return;
        if (tileModifier.modifiers == null) return;

        for (int i = 0; i < tileModifier.modifiers.Count; i++)
            localModifiers.Add(tileModifier.modifiers[i]);

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
            gridObj.ClearPlanterObject();

        int refund = planterData.cost / 2;

        ResourceManager.Instance.AddResource(planterData.costType, refund);

        Debug.Log($"{planterData.planterName} kaldırıldı, {refund} {planterData.costType} iade edildi.");

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

    public void SetGridObject(GridObject gridObject)
    {
        Initialize(new List<GridObject> { gridObject });
    }
}