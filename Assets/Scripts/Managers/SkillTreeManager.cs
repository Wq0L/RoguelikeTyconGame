using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    [SerializeField] private List<SkillNodeSO> allNodes;

    private Dictionary<SkillNodeSO, int> nodeLevels = new();
    private HashSet<Vector2Int> unlockedPositions = new();

    public event Action<SkillNodeSO, int> OnNodeLevelChanged;

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
        unlockedPositions.Add(Vector2Int.zero); // kök her zaman "açık" sayılır
    }

    public int GetCurrentLevel(SkillNodeSO node)
    {
        return nodeLevels.TryGetValue(node, out int lvl) ? lvl : 0;
    }

    public bool CanUpgrade(SkillNodeSO node)
    {
        int currentLevel = GetCurrentLevel(node);

        if (currentLevel >= node.tiers.Count) return false; // max seviye

        if (currentLevel == 0 && !HasUnlockedNeighbor(node.gridPosition))
            return false; // ilk seviye için komşuluk şart

        SkillNodeTier tier = node.tiers[currentLevel];
        return ResourceManager.Instance.CanAfford(tier.costType, tier.cost);
    }

    private bool HasUnlockedNeighbor(Vector2Int pos)
    {
        foreach (var dir in Directions)
            if (unlockedPositions.Contains(pos + dir)) return true;

        return false;
    }

   public bool TryUpgrade(SkillNodeSO node)
    {
        if (!CanUpgrade(node)) return false;

        int currentLevel = GetCurrentLevel(node);

        if (currentLevel > 0)
        {
            SkillNodeTier previousTier = node.tiers[currentLevel - 1];
            StatManager.Instance.RemoveGlobalModifiers(previousTier.effects);
        }

        SkillNodeTier newTier = node.tiers[currentLevel];
        ResourceManager.Instance.SpendResource(newTier.costType, newTier.cost);

        int newLevel = currentLevel + 1;
        nodeLevels[node] = newLevel;
        unlockedPositions.Add(node.gridPosition);

        StatManager.Instance.AddGlobalModifiers(newTier.effects);

        if (node.unlockType != UnlockType.None && newLevel == node.tiers.Count)
            UnlockManager.Instance.Unlock(node.unlockType);

        OnNodeLevelChanged?.Invoke(node, newLevel);
        return true;
    }
}