using UnityEngine;

public class GroundCell : MonoBehaviour
{
    private GridPosition gridPosition;

    public void SetGridPosition(GridPosition gridPosition)
    {
        this.gridPosition = gridPosition;
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }
}