using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResonanceTier
{
    [Min(2)] public int requiredCount = 2;
    [Tooltip("Total resonance multiplier at this tier, not added to earlier tiers. Spawn interval uses values below 1.")]
    [Min(0.01f)] public float multiplier = 2f;
}

[Serializable]
public class ResonanceRule
{
    public string resonanceName;
    public TileModifierType tileType;
    public StatType statType;
    public List<ResonanceTier> tiers = new();
}

[CreateAssetMenu(menuName = "Game/Resonance Rules")]
public class ResonanceRulesSO : ScriptableObject
{
    public List<ResonanceRule> rules = new();
}
