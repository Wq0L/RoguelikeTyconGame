using System.Collections.Generic;
using UnityEngine;

public class PlanterBrain : MonoBehaviour
{
    [SerializeField] private PlanterSO planterData;
    [SerializeField] private List<Transform> spawnPoints;

    private List<PlantSpawner> spawners = new List<PlantSpawner>();

    public void Initialize(List<GridObject> gridObjects)
    {
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (i >= gridObjects.Count) break;

            // En yakın gridObject'i bul
            GridObject closest = FindClosestGridObject(spawnPoints[i].position, gridObjects);

            GameObject spawnerObj = new GameObject("PlantSpawner_" + i);
            spawnerObj.transform.position = spawnPoints[i].position;
            spawnerObj.transform.SetParent(transform);

            PlantSpawner spawner = spawnerObj.AddComponent<PlantSpawner>();
            spawner.Initialize(planterData, closest);

            spawners.Add(spawner);
        }
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

        if (closest != null)
        {
            GroundCell closestCell = closest.GetGroundCellCached();
            //Debug.Log($"SpawnPoint {worldPos} → Grid {closestCell.GetGridPosition()}");
        }

        return closest;
    }

    // 1x1 planter için geriye dönük uyumluluk
    public void SetGridObject(GridObject gridObject)
    {
        Initialize(new List<GridObject> { gridObject });
    }
}