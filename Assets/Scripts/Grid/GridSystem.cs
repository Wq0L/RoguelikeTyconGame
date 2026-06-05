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
        public List<GridObject> GetGridObjectsInRadius(Vector3 worldCenter, float radius)
    {
        List<GridObject> result = new List<GridObject>();
        float adjustedRadius = radius + (cellSize * 0.5f); // yarım hücre ekle

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 cellWorldPos = GetWorldPosition(x, z);

                float distance = Vector3.Distance(
                    new Vector3(worldCenter.x, 0, worldCenter.z),
                    new Vector3(cellWorldPos.x, 0, cellWorldPos.z)
                );

                if (distance <= adjustedRadius)
                    result.Add(gridObjectArray[x, z]);
            }
        }

        return result;
    }
}