using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActiveResonance
{
    public string resonanceName;
    public TileModifierType tileType;
    public int tileCount;
    public int requiredCount;
    public StatType statType;
    public float multiplier;
}

// Stateless service: results belong to each planter, never to a global player buff.
// No scene singleton or Update loop is needed. Configuration is included in builds by Resources.
public static class ResonanceManager
{
    private static ResonanceRulesSO defaultRules;
    public static ResonanceRulesSO DefaultRules => defaultRules != null
        ? defaultRules : defaultRules = Resources.Load<ResonanceRulesSO>("ResonanceRules");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => defaultRules = null;

    public static bool IsNewTier(ActiveResonance current, IReadOnlyList<ActiveResonance> previous)
    {
        foreach (var old in previous)
            if (old.tileType == current.tileType && old.statType == current.statType &&
                old.requiredCount >= current.requiredCount) return false;
        return true;
    }

    public static void Evaluate(ResonanceRulesSO config,
        IReadOnlyDictionary<TileModifierType, int> counts,
        List<StatModifier> modifiers, List<ActiveResonance> active)
    {
        modifiers.Clear();
        active.Clear();
        if (config == null || config.rules == null || counts == null) return;

        foreach (ResonanceRule rule in config.rules)
        {
            if (rule == null || rule.tiers == null || !counts.TryGetValue(rule.tileType, out int count)) continue;
            ResonanceTier best = null;
            foreach (ResonanceTier tier in rule.tiers)
            {
                if (tier == null || tier.requiredCount < 2 || tier.requiredCount > count ||
                    float.IsNaN(tier.multiplier) || float.IsInfinity(tier.multiplier) || tier.multiplier <= 0f) continue;
                if (best == null || tier.requiredCount > best.requiredCount) best = tier;
            }
            if (best == null) continue;
            modifiers.Add(new StatModifier { statType = rule.statType, target = StatTarget.Planter,
                operation = ModifierOperation.MorePercent, value = best.multiplier - 1f });
            active.Add(new ActiveResonance { resonanceName = rule.resonanceName, tileType = rule.tileType,
                tileCount = count, requiredCount = best.requiredCount, statType = rule.statType,
                multiplier = best.multiplier });
        }
    }
}
