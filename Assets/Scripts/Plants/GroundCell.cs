using UnityEngine;

public class GroundCell : MonoBehaviour
{
    [SerializeField] private Transform mineSpawnPoint;

    private GridPosition gridPosition;

    public Transform GetPlantSpawnPoint()
    {
        return mineSpawnPoint;
    }

    public void SetGridPosition(GridPosition gridPosition)
    {
        this.gridPosition = gridPosition;
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }
}