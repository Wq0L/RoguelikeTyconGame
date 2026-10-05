using System.Collections.Generic;
using UnityEngine;
using System;

public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance { get; private set; }

    [SerializeField] private ProgressionSO progressionData;
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
    // Henüz level'a dönüşmemiş XP (Bölüm 3.7.6.1: double; büyük XP float'ta ilerlemeyen döngüye girebiliyordu). Arayüz float okur.
    private double xp;
    public float CurrentXP => (float)xp;
    public double StoredXP => xp;
    public float XPToNextLevel { get; private set; }
    // Run boyunca kazanılan toplam XP; round özeti round başı farkı gösterir.
    public double TotalXPEarned { get; private set; }

    // Level işleme bütçesi (Bölüm 3.7.6.1): bir karede, bütün AddXP çağrıları toplamında en çok bu kadar level işlenir. Normal
    // hasatta bütçe dolmaz: level'lar eskisi gibi AddXP içinde, aynı anda işlenir. Bütçeyi aşan level işi saklanır ve sonraki
    // karelerde aynı bütçeyle sürer. Round sonu kart kararı bekleyen iş bitince verilir (RoundManager). Her level bir kez işlenir
    // ve OnLevelUp'ı bir kez çağırır; XP silinmez. (Bütçe çağrı başına olsaydı, her hasadın binlerce level getirdiği durumda
    // kare başına iş hasat sayısıyla çarpılırdı.)
    public const int LevelsPerFrame = 256;
    private int budgetFrame = -1, budgetUsed;
    // XP'si olup işlenmeyi bekleyen level var mı (teknik sınırda durduysa yok sayılır: o durum raporlanır, XP saklı kalır).
    public bool HasPendingLevels => !LevelProcessingHalted && xp >= XPToNextLevel;
    // Teknik sınır: geçersiz level maliyeti, level sayacının int sınırı ya da maliyetin XP'nin hassasiyetinden küçük kalması.
    // Level işleme durur, XP silinmez; neden bir kez raporlanır (P7 / endless sayı modeli).
    public bool LevelProcessingHalted { get; private set; }
    public string HaltReason { get; private set; }

    private int pendingMutationCount = 0;

    // İlk davranış kartının alındığı round (0: henüz alınmadı). Profilin erken davranış teklifi (RunBalanceSO.firstBehaviorOffer)
    // buna bakar: kart alınana kadar tekliflerde bir slot o türlerden gelir. Run başına tutulur (sahneyle birlikte sıfırlanır).
    public int FirstBehaviorCardRound { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Run profilinin denge seti kendi XP tablosunu getirebilir (Bölüm 3.5); yoksa sahnedeki veri.
        RunBalanceSO balance = RunBalanceSO.Active;
        if (balance != null && balance.progression != null) progressionData = balance.progression;
        string error = progressionData != null ? progressionData.Validate() : "XP tablosu yok";
        if (error != null) Halt($"XP tablosu geçersiz ({error})");
        XPToNextLevel = progressionData != null ? progressionData.GetXPForLevel(CurrentLevel) : float.PositiveInfinity;
        CostUsable();
    }

    private void Update()
    {
        // Kalan level işi: karenin bütçesinden kalanla. Run bitmişken ya da menüde işlenmez (eski run'ın işi sürmez).
        if (!HasPendingLevels || GameManager.Instance == null) return;
        GameStates state = GameManager.Instance.CurrentState;
        if (state == GameStates.RunComplete || state == GameStates.MainMenu) return;
        if (ProcessLevels() > 0) OnXPChanged?.Invoke();
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


    // Geçersiz XP (NaN, sonsuz, negatif) eklenmez ve raporlanır. Geçerli XP saklanır; level'lar bütçeyle işlenir.
    public void AddXP(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0d)
        {
            NumericSafety.ReportInvalid(NumericSite.Experience, amount);
            return;
        }
        xp += amount;
        TotalXPEarned += amount;
        OnXPChanged?.Invoke();
        ProcessLevels();
    }

    // Bu karenin bütçesinden kalan kadar level işler; işlenen sayıyı döner.
    private int ProcessLevels()
    {
        int frame = Time.frameCount;
        if (frame != budgetFrame) { budgetFrame = frame; budgetUsed = 0; }
        if (HasPendingLevels && BacklogExceedsCounter())
        {
            Halt($"bekleyen level sayısı ({PendingLevelEstimate:0.###e0}) level sayacının int sınırını aşıyor; bu iş hiçbir karede bitmez");
            return 0;
        }
        int done = 0;
        while (budgetUsed < LevelsPerFrame && HasPendingLevels && LevelUp()) { budgetUsed++; done++; }
        return done;
    }

    // Tablo bittikten sonra bekleyen level sayısı bellidir (sabit maliyette saklı XP ÷ maliyet; büyüyen kuyrukta kapalı hesap:
    // ProgressionSO.LevelsAffordable). Level sayacına (int) sığmıyorsa iş tamamlanamaz: kare bütçesiyle günlerce işleyip sayacın
    // sınırında durmak ve round sonunu o süre bekletmek yerine hemen durur ve raporlar. XP silinmez. (Sayaca sığan ama çok büyük
    // bir iş durdurulmaz; bütçeyle işlenir.)
    private bool BacklogExceedsCounter() => PendingLevelEstimate > int.MaxValue - CurrentLevel;

    // Saklı XP'nin yettiği level sayısının kapalı hesabı (yalnız tablo bittikten sonra; tablo içinde −1). Ölçüm ve durum yazısı için.
    public double PendingLevelEstimate => progressionData != null ? progressionData.LevelsAffordable(CurrentLevel, xp) : -1d;
    // Aktif XP verisinin tablo sonrası kuralı (ölçüm ve test için).
    public ProgressionSO Data => progressionData;

    private bool LevelUp()
    {
        if (!CostUsable()) return false;
        if (CurrentLevel == int.MaxValue) { Halt("level sayacı int sınırında"); return false; }
        double remaining = xp - XPToNextLevel;
        if (remaining == xp) { Halt($"level maliyeti ({XPToNextLevel}) saklı XP'nin ({xp}) hassasiyetinden küçük"); return false; }
        xp = remaining;
        CurrentLevel++;
        XPToNextLevel = progressionData.GetXPForLevel(CurrentLevel);
        CostUsable();   // NaN / sonsuz maliyet karşılaştırmada sessizce takılmasın: hemen raporlanır

        pendingMutationCount++;
        OnLevelUp?.Invoke(CurrentLevel);
        return true;
    }

    // Sonraki level'ın maliyeti: NaN ya da ≤ 0 geçersiz veridir (eskiden ilerlemeyen döngü ya da sessiz durma), +sonsuz formülün
    // sayı sınırıdır. İkisinde de level işleme durur ve raporlanır.
    private bool CostUsable()
    {
        float cost = XPToNextLevel;
        if (cost > 0f && !float.IsInfinity(cost)) return true;
        Halt(float.IsPositiveInfinity(cost) ? $"level {CurrentLevel} maliyeti sayı sınırını aştı" : $"level {CurrentLevel} maliyeti geçersiz ({cost})");
        return false;
    }

    private void Halt(string reason)
    {
        if (LevelProcessingHalted) return;
        LevelProcessingHalted = true;
        HaltReason = reason;
        Debug.LogError($"Level işleme durdu: {reason}. Saklı XP {xp} silinmedi; level {CurrentLevel}. Teknik sınır (P7 / endless sayı modeli).");
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
        if (FirstBehaviorCardRound == 0 && RunBalanceSO.IsFirstBehaviorType(modifier.modifierType))
            FirstBehaviorCardRound = Mathf.Max(1, RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 1);

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
