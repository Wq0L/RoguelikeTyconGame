using System.Collections.Generic;
using UnityEngine;
using System;

public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance { get; private set; }

    [SerializeField] private ProgressionSO progressionData;
    [SerializeField] private List<TileModifierSO> possibleModifiers;
    [SerializeField] private GridManager gridManager;

    public event Action<int> OnLevelUp;
    public event Action OnXPChanged;

    public int CurrentLevel { get; private set; } = 1;
    public float CurrentXP { get; private set; } = 0f;
    public float XPToNextLevel { get; private set; }

    private int pendingMutationCount = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    
    private void Start()
    {
        XPToNextLevel = progressionData.GetXPForLevel(CurrentLevel);
        RoundManager.Instance.OnRoundEnded += HandleRoundEnded;
    }

    private void OnDestroy()
    {
        if (RoundManager.Instance != null)
            RoundManager.Instance.OnRoundEnded -= HandleRoundEnded;
    }


    public void AddXP(float amount)
    {
        CurrentXP += amount;
        OnXPChanged?.Invoke();

        while (CurrentXP >= XPToNextLevel)
            LevelUp();
    }

    private void LevelUp()
    {
        CurrentXP -= XPToNextLevel;
        CurrentLevel++;
        XPToNextLevel = progressionData.GetXPForLevel(CurrentLevel);

        pendingMutationCount++;
        OnLevelUp?.Invoke(CurrentLevel);

        Debug.Log("Level Up! Seviye: " + CurrentLevel);
    }

    private void HandleRoundEnded()
    {
        // Kart sistemi bu işi yapıyor artık, random mutation kaldırıldı
        pendingMutationCount = 0;
    }

    // UI'dan seçilen modifier buraya gelir
    public bool ApplyRandomEligibleCell(TileModifierSO modifier)
    {
        if (modifier == null) return false;

        List<GroundCell> eligibleCells = GetEligibleCells();
        if (eligibleCells.Count == 0)
        {
            Debug.Log("Uygun tile yok.");
            return false;
        }

        GroundCell selectedCell = eligibleCells[UnityEngine.Random.Range(0, eligibleCells.Count)];
        selectedCell.ApplyModifier(modifier);

        Debug.Log($"Kart uygulandı: {modifier.modifierName} → {selectedCell.GetGridPosition()}");
        return true;
    }

    private List<GroundCell> GetEligibleCells()
    {
        List<GroundCell> eligibleCells = new List<GroundCell>();
        GridSystem gridSystem = gridManager.GetGridSystem();

        for (int x = 0; x < gridManager.GetWidth(); x++)
        {
            for (int z = 0; z < gridManager.GetHeight(); z++)
            {
                GridPosition pos = new GridPosition(x, z);
                GridObject gridObj = gridSystem.GetGridObject(pos);
                if (gridObj == null) continue;

                GroundCell cell = gridObj.GetGroundCellCached();
                if (cell == null) continue;

                if (!cell.IsLocked && cell.CurrentModifier == null)
                    eligibleCells.Add(cell);
            }
        }

        return eligibleCells;
    }
}