using UnityEngine;

public class GridObject
{
    private GridSystem gridSystem;
    private GridPosition gridPosition;

    private GameObject spawnedGroundObject;
    private GameObject spawnedPlantObject;
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

    public GameObject GetGroundObject()
    {
        return spawnedGroundObject;
    }

    public void SetPlantObject(GameObject obj)
    {
        spawnedPlantObject = obj;
    }
    public GameObject GetPlantObject()
    {
        return spawnedPlantObject;
    }
    public void ClearPlantObject()
    {
        spawnedPlantObject = null;
    }

    public bool HasGroundObject()
    {
        return spawnedGroundObject != null;
    }

    public bool HasPlantObject()
    {
        return spawnedPlantObject != null;
    }

    public GroundCell GetGroundCellCached()
    {
        if (spawnedGroundObject == null)
            return null;

        if (groundCellCacheDirty || groundCellCache == null)
        {
            groundCellCache = spawnedGroundObject.GetComponent<GroundCell>();
            groundCellCacheDirty = false;
        }

        return groundCellCache;
    }

}