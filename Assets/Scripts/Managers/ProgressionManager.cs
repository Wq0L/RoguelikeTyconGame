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

    // Son round'un kart seçimlerinin düştüğü tile'lar. Harita, önizleme ve dünyadaki çerçeveler
    // "bu round'un kartı nereye gitti" diye buradan okur. Yeni round başlayınca temizlenir.
    private readonly List<GroundCell> roundAppliedCells = new List<GroundCell>();
    private readonly Dictionary<GroundCell, TileUpgrade> roundUpgrades = new Dictionary<GroundCell, TileUpgrade>();
    public IReadOnlyList<GroundCell> RoundAppliedCells => roundAppliedCells;
    public bool WasAppliedThisRound(GroundCell cell) => cell != null && roundAppliedCells.Contains(cell);
    // Yeni yerleşen değil, seviye atlayan tile (etiket "+SV").
    public bool WasUpgradedThisRound(GroundCell cell) => cell != null && roundUpgrades.ContainsKey(cell);

    // Yükseltmeden önceki hal: tooltip ve round listesi "Sv 0 → Sv 1 · +13% → +16.25%" yazar.
    public sealed class TileUpgrade
    {
        public int FromLevel;
        public int ToLevel;
        public List<StatModifier> Before;
    }

    public bool TryGetRoundUpgrade(GroundCell cell, out TileUpgrade upgrade)
    {
        upgrade = null;
        return cell != null && roundUpgrades.TryGetValue(cell, out upgrade);
    }

    public int CurrentLevel { get; private set; } = 1;
    public float CurrentXP { get; private set; } = 0f;
    public float XPToNextLevel { get; private set; }
    // Run boyunca kazanılan toplam XP; round özeti round başı farkı gösterir.
    public double TotalXPEarned { get; private set; }

    private int pendingMutationCount = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        XPToNextLevel = progressionData.GetXPForLevel(CurrentLevel);
    }

    
    private void Start()
    {
        
        RoundManager.Instance.OnRoundEnded += HandleRoundEnded;
        RoundManager.Instance.OnRoundChanged += HandleRoundStarted;
    }

    private void OnDestroy()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnRoundEnded -= HandleRoundEnded;
            RoundManager.Instance.OnRoundChanged -= HandleRoundStarted;
        }
    }

    private void HandleRoundStarted(int round)
    {
        roundAppliedCells.Clear();
        roundUpgrades.Clear();
    }

    // Bütün saksılar üretim tabanındaysa yeni Fertile kartı hız yerine nadirlik verir; kart bunu yazar.
    public bool AllPlantersAtSpawnFloor()
    {
        GridSystem gridSystem = gridManager.GetGridSystem();
        bool any = false;
        for (int x = 0; x < gridManager.GetWidth(); x++)
        for (int z = 0; z < gridManager.GetHeight(); z++)
        {
            PlanterBrain brain = gridSystem.GetGridObject(new GridPosition(x, z))?.GetPlanterBrain();
            if (brain == null) continue;
            any = true;
            if (brain.GetRawSpawnInterval() > StatCalculator.MinimumSpawnInterval + 1e-3f) return false;
        }
        return any;
    }

    // Kart ekranı buna göre açılır: açık ve boş hücre yoksa kartlar yeni tile yerine yükseltme verir.
    public bool HasEligibleCell() => GetEligibleCells().Count > 0;

    // Yükseltme adayları: önce saksıların altındaki tile'lar (boş toprağı yükseltmek işe yaramaz), sonra diğerleri.
    // Yer yine şans: oyuncu adayları seçmez, sadece gelen üç aday arasından seçer.
    public List<GroundCell> GetUpgradeCandidates(int count)
    {
        var underPlanters = new List<GroundCell>();
        var others = new List<GroundCell>();
        GridSystem gridSystem = gridManager.GetGridSystem();
        for (int x = 0; x < gridManager.GetWidth(); x++)
        for (int z = 0; z < gridManager.GetHeight(); z++)
        {
            GroundCell cell = gridSystem.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
            if (cell == null || !cell.CanUpgrade) continue;
            (cell.Planter != null ? underPlanters : others).Add(cell);
        }
        Shuffle(underPlanters);
        Shuffle(others);
        underPlanters.AddRange(others);
        if (underPlanters.Count > count) underPlanters.RemoveRange(count, underPlanters.Count - count);
        return underPlanters;
    }

    private static void Shuffle(List<GroundCell> cells)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
    }

    // Kartın nadirliği kaç seviye verdiğini belirler (Common/Rare 1, Epic 2, Legendary 3).
    public bool ApplyUpgrade(GroundCell cell, TileRarity rarity)
    {
        if (cell == null) return false;
        int fromLevel = cell.Level;
        var before = new List<StatModifier>(cell.RolledModifiers);
        if (cell.AddLevels(GroundCell.LevelsFor(rarity)) <= 0) return false;
        if (!roundAppliedCells.Contains(cell)) roundAppliedCells.Add(cell);
        // Aynı round ikinci kez yükselirse ilk hali kalır: "Sv 0 → Sv 2".
        if (roundUpgrades.TryGetValue(cell, out TileUpgrade upgrade)) upgrade.ToLevel = cell.Level;
        else roundUpgrades.Add(cell, new TileUpgrade { FromLevel = fromLevel, ToLevel = cell.Level, Before = before });
        return true;
    }


    public void AddXP(float amount)
    {
        CurrentXP += amount;
        TotalXPEarned += amount;
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

        // Debug.Log("Level Up! Seviye: " + CurrentLevel);
    }

    private void HandleRoundEnded()
    {
        // Kart sistemi bu işi yapıyor artık, random mutation kaldırıldı
        pendingMutationCount = 0;
    }

    // UI'dan seçilen modifier buraya gelir
    public bool ApplyRandomEligibleCell(TileModifierSO modifier, IReadOnlyList<StatModifier> offeredModifiers = null)
    {
        if (modifier == null) return false;

        List<GroundCell> eligibleCells = GetEligibleCells();
        if (eligibleCells.Count == 0)
        {
            // Debug.Log("Uygun tile yok.");
            return false;
        }

        GroundCell selectedCell = eligibleCells[UnityEngine.Random.Range(0, eligibleCells.Count)];
        selectedCell.ApplyModifier(modifier, offeredModifiers);
        roundAppliedCells.Add(selectedCell);

        // Debug.Log($"Kart uygulandı: {modifier.modifierName} → {selectedCell.GetGridPosition()}");
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
