using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private float cellSize = 2f;

    [Header("Scene Ground Cells")]
    [SerializeField] private GroundCell[] groundCells;

    private GridSystem gridSystem;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        
        gridSystem = new GridSystem(width, height, cellSize);
        RegisterSceneGrounds();
    }

    private void RegisterSceneGrounds()
    {
        foreach (GroundCell groundCell in groundCells)
        {
            if (groundCell == null) continue;

            GridPosition gridPosition =
                gridSystem.GetGridPosition(groundCell.transform.position);

            GridObject gridObject =
                gridSystem.GetGridObject(gridPosition);

            if (gridObject == null)
            {
                Debug.LogWarning("Grid dışında GroundCell var: " + groundCell.name, groundCell);
                continue;
            }

            if (gridObject.HasGroundObject())
            {
                Debug.LogWarning("Bu grid pozisyonunda zaten Ground var: " + gridPosition, groundCell);
                continue;
            }

            gridObject.SetGroundObject(groundCell.gameObject);
            groundCell.SetGridPosition(gridPosition);

            Debug.Log("GroundCell grid'e bağlandı: " + gridPosition);
        }
    }

    public GridSystem GetGridSystem()
    {
        return gridSystem;
    }

    public int GetWidth()
    {
        return width;
    }

    public int GetHeight()
    {
        return height;
    }

    public float GetCellSize()
    {
        return cellSize;
    }
}