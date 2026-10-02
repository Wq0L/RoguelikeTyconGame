using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    [SerializeField] private List<SkillNodeSO> allNodes;
    public IReadOnlyList<SkillNodeSO> AllNodes => allNodes;
#if UNITY_EDITOR
    [Header("Editor Debug")]
    [Tooltip("Inspector'daki ücretsiz tüm ağacı tamamlama butonunu açar. Build'e dahil edilmez.")]
    [SerializeField] private bool enableDebugUnlockAll;
    public bool DebugUnlockAllEnabled => enableDebugUnlockAll;

    public int DebugMaxAllSkills()
    {
        if (!Application.isPlaying || !enableDebugUnlockAll || allNodes == null ||
            StatManager.Instance == null || UnlockManager.Instance == null) return 0;
        int changed = 0;
        foreach (var node in allNodes)
        {
            if (node == null || node.tiers == null || node.tiers.Count == 0 || IsMaxLevel(node)) continue;
            ApplyLevel(node, node.tiers.Count);
            changed++;
        }
        if (changed > 0) OnTreeChanged?.Invoke();
        return changed;
    }
#endif

    private Dictionary<SkillNodeSO, int> nodeLevels = new();
    private HashSet<Vector2Int> unlockedPositions = new();

    public event Action OnTreeChanged;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int(1, 1), new Vector2Int(-1, -1),
        new Vector2Int(1, -1), new Vector2Int(-1, 1)
    };

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // Run profilinin denge seti kendi ağacını getirebilir (Bölüm 3.5); yoksa sahnedeki ortak liste.
        // Arayüz yuvaları aynı sete göre SkillTreeUI'da bağlanır.
        RunBalanceSO balance = RunBalanceSO.Active;
        if (balance != null && balance.skillTree != null) allNodes = balance.skillTree.Nodes();
    }

    public void ResetTree()
    {
        if (StatManager.Instance != null)
            foreach (var entry in nodeLevels)
                StatManager.Instance.RemoveGlobalModifiers(entry.Key.tiers[entry.Value - 1].effects);
        nodeLevels.Clear();
        unlockedPositions.Clear();
        unlockedPositions.Add(Vector2Int.zero); // kök her zaman açık

        OnTreeChanged?.Invoke();
    }

    public int GetCurrentLevel(SkillNodeSO node)
        => nodeLevels.TryGetValue(node, out int lvl) ? lvl : 0;

    public bool IsMaxLevel(SkillNodeSO node)
        => GetCurrentLevel(node) >= node.tiers.Count;

    public bool IsNodeVisible(SkillNodeSO node)
    {
        if (GetCurrentLevel(node) > 0) return true;      // zaten açtım
        return MeetsPrerequisites(node);
    }

    public bool MeetsPrerequisites(SkillNodeSO node)
    {
        if (node == null) return false;
        if (!node.explicitPrerequisites) return HasUnlockedNeighbor(node.gridPosition);
        foreach (var requirement in node.prerequisites)
        {
            if (requirement.node == null) return false;
            // Kilidi profilin başlangıçta verdiği düğüm satılmaz; onun yerine kendi ön koşulları aranır. Böylece arkasındaki
            // düğümlerin (ör. Tornado Kartları) erişim sırası değişmez, yalnız aradaki ücretli kilit adımı kalkar.
            if (IsGrantedByProfile(requirement.node))
            {
                if (!MeetsPrerequisites(requirement.node)) return false;
                continue;
            }
            if (!IsDisabledByProfile(requirement.node) && GetCurrentLevel(requirement.node) < requirement.level) return false;
        }
        return true;
    }

    // Deney (Bölüm 2.2): run profili round süresini sabitlediyse yalnız süre veren node'lar bu run'da etkisizdir. Satın alınamaz
    // ve önkoşul olarak karşılanmış sayılır (arkasındaki hız ve süre dışı yollar kilitlenmesin). Node asset'leri değişmez.
    // Bölüm 3.6: denge seti bir kilidi başlangıçtan açık veriyorsa, yalnız o kilidi açan düğüm de aynı kurala girer.
    public bool IsDisabledByProfile(SkillNodeSO node) =>
        node != null && ((RoundManager.Instance != null && RoundManager.Instance.FixedRoundDuration && IsDurationOnly(node)) || IsGrantedByProfile(node));

    // Düğümün açtığı kilidi profil run başında açık veriyor: erişim satın almayla gelmedi. Düğümün seviyesi artmaz, kayıt oluşmaz.
    // Stat veren kademeleri varsa onlar yine normal satılır; arayüz erişimi ayrıca "Başlangıçtan açık" diye gösterir.
    public static bool HasStartingAccess(SkillNodeSO node) =>
        node != null && node.unlockType != UnlockType.None && RunBalanceSO.IsStartingUnlock(node.unlockType);

    // Kilidi profil başlangıçta açık verdiği için satılmayan düğüm (stat etkisi olmayan, yalnız kilit açan düğüm).
    public static bool IsGrantedByProfile(SkillNodeSO node)
    {
        if (!HasStartingAccess(node) || node.tiers == null) return false;
        foreach (var tier in node.tiers)
            if (tier.effects != null && tier.effects.Count > 0) return false;
        return true;
    }

    // Ağaç arayüzü: satılmayan başlangıç düğümü, yerine ulaşıldığında (kendi ön koşulları sağlanınca) bağlantı çizgileri için
    // açık uç sayılır. Satın alma seviyesi yine 0'dır.
    public bool IsOpenFromStart(SkillNodeSO node) => IsGrantedByProfile(node) && MeetsPrerequisites(node);

    public static bool IsDurationOnly(SkillNodeSO node)
    {
        if (node == null || node.unlockType != UnlockType.None || node.tiers == null || node.tiers.Count == 0) return false;
        foreach (var tier in node.tiers)
            foreach (var effect in tier.effects)
                if (effect.statType != StatType.RoundDuration) return false;
        return true;
    }

    public bool CanUpgrade(SkillNodeSO node)
    {
        if (IsDisabledByProfile(node)) return false;
        int currentLevel = GetCurrentLevel(node);

        if (currentLevel >= node.tiers.Count) return false;
        if (currentLevel == 0 && !MeetsPrerequisites(node)) return false;

        SkillNodeTier tier = node.tiers[currentLevel];
        return ResourceManager.Instance.CanAfford(tier.costType, tier.cost);
    }

    public bool TryUpgrade(SkillNodeSO node)
    {
        if (!CanUpgrade(node)) return false;

        int currentLevel = GetCurrentLevel(node);
        SkillNodeTier newTier = node.tiers[currentLevel];
        if (!ResourceManager.Instance.SpendResource(newTier.costType, newTier.cost)) return false;

        ApplyLevel(node, currentLevel + 1);
        OnTreeChanged?.Invoke();
        return true;
    }

    // Both normal purchases and the editor helper replace the previous tier.
    private void ApplyLevel(SkillNodeSO node, int newLevel)
    {
        int currentLevel = GetCurrentLevel(node);
        if (currentLevel > 0)
            StatManager.Instance.RemoveGlobalModifiers(node.tiers[currentLevel - 1].effects);
        nodeLevels[node] = newLevel;
        unlockedPositions.Add(node.gridPosition);
        StatManager.Instance.AddGlobalModifiers(node.tiers[newLevel - 1].effects);
        if (node.unlockType != UnlockType.None && newLevel == node.tiers.Count)
            UnlockManager.Instance.Unlock(node.unlockType);
    }

    public bool IsPositionUnlocked(Vector2Int position) => unlockedPositions.Contains(position);

    public static bool AreNeighbors(Vector2Int a, Vector2Int b)
    {
        foreach (var direction in Directions)
            if (a + direction == b) return true;
        return false;
    }

    private bool HasUnlockedNeighbor(Vector2Int pos)
    {
        foreach (var dir in Directions)
            if (unlockedPositions.Contains(pos + dir)) return true;

        return false;
    }
}
