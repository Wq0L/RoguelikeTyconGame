using System.Collections.Generic;
using UnityEngine;

public class PlanterBrain : MonoBehaviour
{
    [SerializeField] private PlanterSO planterData;
    [SerializeField] private List<Transform> spawnPoints;

    private List<PlantSpawner> spawners = new List<PlantSpawner>();
    private List<StatModifier> activeModifiers = new List<StatModifier>();

    public List<StatModifier> ActiveModifiers => activeModifiers;

    public void Initialize(List<GridObject> gridObjects)
    {
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

    public void ApplyBuff(TileModifierSO modifier)
    {
        StatModifier statMod = new StatModifier
        {
            statType = modifier.statType,
            operation = modifier.operation,
            value = modifier.value
        };

        activeModifiers.Add(statMod);
        Debug.Log($"PlanterBrain buff aldı: {modifier.modifierName}");
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