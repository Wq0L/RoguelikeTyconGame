using System;
using UnityEngine;

public class GridUnlockManager : MonoBehaviour
{
    public static GridUnlockManager Instance { get; private set; }

    [SerializeField] private GridManager gridManager;
    private int initialUnlockSize;
    private float unlockSizeStat;

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

        unlockSizeStat = StatManager.Instance.GetStat(StatType.GridUnlockSize);
        initialUnlockSize = Mathf.RoundToInt(unlockSizeStat);

        currentUnlockSize = initialUnlockSize;

        UnlockCenter(currentUnlockSize);
    }

    private void OnDestroy()
    {
        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(StatType statType, float value)
    {
        if (statType != StatType.GridUnlockSize)
            return;

        unlockSizeStat = value;
        initialUnlockSize = Mathf.RoundToInt(unlockSizeStat);

        if (initialUnlockSize <= currentUnlockSize)
            return;

        UnlockNextTier(initialUnlockSize);
    }

    // Merkezden dışa doğru unlock
    public void UnlockNextTier(int newSize)
    {
        currentUnlockSize = newSize;
        UnlockCenter(newSize);
    }

    private void UnlockCenter(int size)
    {
        int gridWidth = gridManager.GetWidth();
        int gridHeight = gridManager.GetHeight();

        // Merkez hesapla
        int centerX = gridWidth / 2;
        int centerZ = gridHeight / 2;

        Debug.Log($"Grid: {gridWidth}x{gridHeight} | Merkez: {centerX},{centerZ} | Size: {size}");

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