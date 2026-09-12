using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    [SerializeField] private List<SkillNodeSO> allNodes;

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
    }

    public void ResetTree()
    {
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
        return HasUnlockedNeighbor(node.gridPosition);   // ulaşılabilir mi
    }

    public bool CanUpgrade(SkillNodeSO node)
    {
        int currentLevel = GetCurrentLevel(node);

        if (currentLevel >= node.tiers.Count) return false;
        if (currentLevel == 0 && !HasUnlockedNeighbor(node.gridPosition)) return false;

        SkillNodeTier tier = node.tiers[currentLevel];
        return ResourceManager.Instance.CanAfford(tier.costType, tier.cost);
    }

    public bool TryUpgrade(SkillNodeSO node)
    {
        if (!CanUpgrade(node)) return false;

        int currentLevel = GetCurrentLevel(node);

        if (currentLevel > 0)
            StatManager.Instance.RemoveGlobalModifiers(node.tiers[currentLevel - 1].effects);

        SkillNodeTier newTier = node.tiers[currentLevel];
        ResourceManager.Instance.SpendResource(newTier.costType, newTier.cost);

        int newLevel = currentLevel + 1;
        nodeLevels[node] = newLevel;
        unlockedPositions.Add(node.gridPosition);

        StatManager.Instance.AddGlobalModifiers(newTier.effects);

        if (node.unlockType != UnlockType.None && newLevel == node.tiers.Count)
            UnlockManager.Instance.Unlock(node.unlockType);

        OnTreeChanged?.Invoke();
        return true;
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
