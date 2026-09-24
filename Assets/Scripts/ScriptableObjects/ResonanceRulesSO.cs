using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResonanceTier
{
    [Min(1)] public int requiredCount = 2;
    [Tooltip("Total resonance multiplier at this tier, not added to earlier tiers. Spawn interval uses values below 1.")]
    [Min(0.01f)] public float multiplier = 2f;
    public List<ResonanceEffect> effects = new();
}

public enum ResonanceEffectKind { Stat, Resources, BehaviorDamage, RareScore, ElectricXP }

[Serializable]
public class ResonanceEffect
{
    public ResonanceEffectKind kind;
    public StatType statType;
    public ModifierOperation operation = ModifierOperation.MorePercent;
    public float value;
}

[Serializable]
public class ResonanceRule
{
    public string resonanceName;
    public string id;
    public TileModifierType tileType;
    public StatType statType;
    public bool requiresSecondary;
    public TileModifierType secondaryType;
    public int secondaryCount = 1;
    public bool requiresAnyBehavior;
    public List<ResonanceTier> tiers = new();
}

[CreateAssetMenu(menuName = "Game/Resonance Rules")]
public class ResonanceRulesSO : ScriptableObject
{
    public List<ResonanceRule> rules = new();
}
