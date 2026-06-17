using UnityEngine;

public class GridUnlockManager : MonoBehaviour
{
    public static GridUnlockManager Instance { get; private set; }

    [SerializeField] private GridManager gridManager;

    private GridSystem gridSystem;
    private int currentUnlockSize;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();

        StatManager.Instance.OnStatChanged += HandleStatChanged;

        currentUnlockSize = GetGridUnlockSize();
        UnlockCenter(currentUnlockSize);
    }

    private void OnDestroy()
    {
        if (StatManager.Instance != null)
            StatManager.Instance.OnStatChanged -= HandleStatChanged;
    }

    private void HandleStatChanged(StatType statType, float value)
    {
        if (statType != StatType.GridUnlockSize)
            return;

        int newUnlockSize = Mathf.RoundToInt(value);

        if (newUnlockSize <= currentUnlockSize)
            return;

        UnlockNextTier(newUnlockSize);
    }

    private int GetGridUnlockSize()
    {
        float value = StatManager.Instance.GetFinalStat(
            StatType.GridUnlockSize,
            StatTarget.Grid
        );

        return Mathf.RoundToInt(value);
    }

    public void UnlockNextTier(int newSize)
    {
        currentUnlockSize = newSize;
        UnlockCenter(newSize);
    }

    private void UnlockCenter(int size)
    {
        int gridWidth = gridManager.GetWidth();
        int gridHeight = gridManager.GetHeight();

        int centerX = gridWidth / 2;
        int centerZ = gridHeight / 2;

        int half = size / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
        {
            for (int z = centerZ - half; z <= centerZ + half; z++)
            {
                GridPosition pos = new GridPosition(x, z);
                GridObject gridObj = gridSystem.GetGridObject(pos);
                if (gridObj == null) continue;

                GroundCell cell = gridObj.GetGroundCellCached();
                cell?.Unlock();
            }
        }
    }
}