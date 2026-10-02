using System.Collections.Generic;
using UnityEngine;

public class GridSystem
{
    private int width;
    private int height;
    private float cellSize;
    private GridObject[,] gridObjectArray;

    public GridSystem(int width, int height, float cellSize)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
    
        gridObjectArray = new GridObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GridPosition gridPosition = new GridPosition(x, z);
                gridObjectArray[x, z] = new GridObject(this, gridPosition);
            }
        }
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x, 0, z) * cellSize;
    }

    public GridPosition GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / cellSize);
        int z = Mathf.RoundToInt(worldPosition.z / cellSize);

        return new GridPosition(x, z);
    }

    public bool IsValidGridPosition(GridPosition gridPosition)
    {
        return gridPosition.x >= 0 &&
               gridPosition.z >= 0 &&
               gridPosition.x < width &&
               gridPosition.z < height;
    }
    
    public GridObject GetGridObject(GridPosition gridPosition)
    {
        if (!IsValidGridPosition(gridPosition))
        {
            return null;
        }

        return gridObjectArray[gridPosition.x, gridPosition.z];
    }
    // Visible circle radius. Targeting intersects this circle with each cell's square surface.
    public float HarvestReach(float radius) => Mathf.Max(0f, radius);

    public List<GridObject> GetGridObjectsInRadius(Vector3 worldCenter, float radius)
    {
        List<GridObject> result = new List<GridObject>();
        GetGridObjectsInRadius(worldCenter, radius, result);
        return result;
    }

    // Caller-owned buffer: normal attacks allocate no target list. A circle touching a cell hits that cell only.
    public void GetGridObjectsInRadius(Vector3 worldCenter, float radius, List<GridObject> result)
    {
        result.Clear();
        float reach = HarvestReach(radius) + cellSize * .5f;
        int minX = Mathf.Max(0, Mathf.CeilToInt((worldCenter.x - reach) / cellSize));
        int maxX = Mathf.Min(width - 1, Mathf.FloorToInt((worldCenter.x + reach) / cellSize));
        int minZ = Mathf.Max(0, Mathf.CeilToInt((worldCenter.z - reach) / cellSize));
        int maxZ = Mathf.Min(height - 1, Mathf.FloorToInt((worldCenter.z + reach) / cellSize));

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3 cellWorldPos = GetWorldPosition(x, z);

                if (HarvestArea.TouchesCell(worldCenter, cellWorldPos, radius, cellSize))
                    result.Add(gridObjectArray[x, z]);
            }
        }

    }
}
