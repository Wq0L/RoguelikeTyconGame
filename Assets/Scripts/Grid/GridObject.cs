using UnityEngine;

public class GridObject
{
    private GridSystem gridSystem;
    private GridPosition gridPosition;

    private GameObject spawnedGroundObject;
    private GameObject spawnedPlantObject;
    private GameObject spawnedPlanterObject;
    private PlanterBrain planterBrain;
    private GroundCell groundCellCache;
    private bool groundCellCacheDirty = true;

    public GridObject(GridSystem gridSystem, GridPosition gridPosition)
    {
        this.gridSystem = gridSystem;
        this.gridPosition = gridPosition;
    }

    public void SetGroundObject(GameObject obj)
    {
        spawnedGroundObject = obj;
        groundCellCacheDirty = true;
    }

    public GameObject GetGroundObject() => spawnedGroundObject;

    public void SetPlantObject(GameObject obj) => spawnedPlantObject = obj;
    public GameObject GetPlantObject() => spawnedPlantObject;

    public void SetPlanterObject(GameObject obj) => spawnedPlanterObject = obj;
    public GameObject GetPlanterObject() => spawnedPlanterObject;

    public void SetPlanterBrain(PlanterBrain brain) => planterBrain = brain;
    public PlanterBrain GetPlanterBrain() => planterBrain;

    public void ClearPlanterObject()
    {
        spawnedPlanterObject = null;
        planterBrain = null;
    }

    public void ClearPlantObject() => spawnedPlantObject = null;

    public bool HasGroundObject() => spawnedGroundObject != null;
    public bool HasPlanterObject() => spawnedPlanterObject != null;
    public bool HasPlantObject() => spawnedPlantObject != null;

    public GroundCell GetGroundCellCached()
    {
        if (spawnedGroundObject == null) return null;

        if (groundCellCacheDirty || groundCellCache == null)
        {
            groundCellCache = spawnedGroundObject.GetComponent<GroundCell>();
            groundCellCacheDirty = false;
        }

        return groundCellCache;
    }
}