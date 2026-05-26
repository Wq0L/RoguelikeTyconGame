using UnityEngine;

public class MineBrain : MonoBehaviour
{
    [SerializeField] private MineHealth mineHealth;

    private GridObject gridObject;

    private void OnEnable()
    {
        mineHealth.OnDied += ClearGrid;
    }

    private void OnDisable()
    {
        mineHealth.OnDied -= ClearGrid;
    }

    public void SetGridObject(GridObject gridObject)
    {
        this.gridObject = gridObject;
    }

    public GridObject GetGridObject()
    {
        return gridObject;
    }

    private void ClearGrid()
    {
        if (gridObject != null)
        {
            gridObject.ClearMineObject();
            gridObject = null;
        }
    }
}