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

            GameObject spawnerObj = new GameObject("PlantSpawner_" + i);
            spawnerObj.transform.position = spawnPoints[i].position;
            spawnerObj.transform.SetParent(transform);

            PlantSpawner spawner = spawnerObj.AddComponent<PlantSpawner>();
            spawner.Initialize(planterData, gridObjects[i]);

            spawners.Add(spawner);
        }
    }

    // 1x1 planter için geriye dönük uyumluluk
    public void SetGridObject(GridObject gridObject)
    {
        Initialize(new List<GridObject> { gridObject });
    }
}