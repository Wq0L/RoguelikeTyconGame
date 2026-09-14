using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlantHealthAnchor
{
    [Min(1)] public int round;
    [Min(1)] public float commonHealth;
}

[CreateAssetMenu(menuName = "Game/Plant Health Scaling")]
public class PlantHealthScalingSO : ScriptableObject
{
    [Tooltip("PlantSO maxHealth / referenceHealth preserves per-plant tuning. Existing assets use 10.")]
    [Min(1)] public float referenceHealth = 10f;
    public List<PlantHealthAnchor> anchors = new();
    public float[] rarityMultipliers = { 1f, 1.2f, 1.6f, 2f, 3f };

    public int Calculate(PlantSO plant, int round)
    {
        if (plant == null) return 1;
        PlantHealthAnchor lower = null, upper = null;
        foreach (var anchor in anchors)
        {
            if (anchor == null || anchor.round < 1 || anchor.commonHealth <= 0f) continue;
            if (anchor.round <= round && (lower == null || anchor.round > lower.round)) lower = anchor;
            if (anchor.round >= round && (upper == null || anchor.round < upper.round)) upper = anchor;
        }
        lower ??= upper; upper ??= lower;
        if (lower == null) return Mathf.Max(1, plant.maxHealth);
        double common = lower.commonHealth;
        if (lower.round != upper.round)
            common *= Math.Pow(upper.commonHealth / (double)lower.commonHealth,
                (round - lower.round) / (double)(upper.round - lower.round));
        int rarity = (int)plant.rarity;
        double multiplier = rarityMultipliers != null && rarity >= 0 && rarity < rarityMultipliers.Length
            ? Math.Round(rarityMultipliers[rarity], 6) : 1d;
        double health = common * multiplier * Math.Max(1, plant.maxHealth) / Math.Max(1f, referenceHealth);
        if (double.IsNaN(health)) return Mathf.Max(1, plant.maxHealth);
        return (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling(health - .000001d)));
    }
}
