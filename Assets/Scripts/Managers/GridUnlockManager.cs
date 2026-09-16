using UnityEngine;
using System;

public class GridUnlockManager : MonoBehaviour
{
    public static GridUnlockManager Instance { get; private set; }
    public event Action OnGridSizeChanged;

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

        int newUnlockSize = NormalizeSize(value);

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

        return NormalizeSize(value);
    }

    private static int NormalizeSize(float value) => Mathf.Clamp(Mathf.RoundToInt(value) / 2 * 2 + 1, 3, 11);

    public void UnlockNextTier(int newSize)
    {
        newSize = NormalizeSize(newSize);
        currentUnlockSize = newSize;
        UnlockCenter(newSize);
        OnGridSizeChanged?.Invoke();
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
