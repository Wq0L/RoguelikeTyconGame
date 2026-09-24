using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActiveResonance
{
    public string id, resonanceName;
    public TileModifierType tileType;
    public int tileCount, requiredCount, behaviorMask;
    public StatType statType;
    public float multiplier;
    public List<ResonanceEffect> effects = new();
}

// Shared by placed planters, placement previews and the economy analyzer.
public static class ResonanceManager
{
    private static ResonanceRulesSO defaultRules;
    public static ResonanceRulesSO DefaultRules => defaultRules != null
        ? defaultRules : defaultRules = Resources.Load<ResonanceRulesSO>("ResonanceRules");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => defaultRules = null;

    public static string Identity(ActiveResonance active) => string.IsNullOrEmpty(active.id)
        ? active.tileType + ":" + active.statType : active.id;

    public static bool IsNewTier(ActiveResonance current, IReadOnlyList<ActiveResonance> previous)
    {
        foreach (var old in previous)
            if (Identity(old) == Identity(current) && old.requiredCount >= current.requiredCount &&
                (old.behaviorMask & current.behaviorMask) == current.behaviorMask) return false;
        return true;
    }

    public static void Evaluate(ResonanceRulesSO config, IReadOnlyDictionary<TileModifierType, int> counts,
        List<StatModifier> modifiers, List<ActiveResonance> active)
    {
        modifiers.Clear(); active.Clear();
        if (config == null || config.rules == null || counts == null) return;
        int mask = 0;
        for (int i = 1; i <= (int)DamageType.Electric; i++)
            if (TryTile((DamageType)i, out var tile) && counts.TryGetValue(tile, out int number) && number > 0) mask |= 1 << i;
        foreach (var rule in config.rules)
        {
            if (rule == null || rule.tiers == null || !counts.TryGetValue(rule.tileType, out int count)) continue;
            if (rule.requiresSecondary && (!counts.TryGetValue(rule.secondaryType, out int second) || second < rule.secondaryCount)) continue;
            if (rule.requiresAnyBehavior && mask == 0) continue;
            ResonanceTier best = null;
            foreach (var tier in rule.tiers)
            {
                if (tier == null || tier.requiredCount < 1 || tier.requiredCount > count ||
                    float.IsNaN(tier.multiplier) || float.IsInfinity(tier.multiplier) || tier.multiplier <= 0f) continue;
                if (best == null || tier.requiredCount > best.requiredCount) best = tier;
            }
            if (best == null) continue;
            var effects = best.effects != null && best.effects.Count > 0 ? best.effects : new List<ResonanceEffect>
            { new() { statType = rule.statType, value = best.multiplier - 1f } };
            active.Add(new ActiveResonance { id = rule.id, resonanceName = rule.resonanceName, tileType = rule.tileType,
                tileCount = count, requiredCount = best.requiredCount, statType = rule.statType, multiplier = best.multiplier,
                effects = effects, behaviorMask = rule.requiresAnyBehavior ? mask : 0 });
        }
        BuildModifiers(active, modifiers);
    }

    public static bool TryTile(DamageType type, out TileModifierType tile)
    {
        tile = type switch { DamageType.Explosion => TileModifierType.Explosive, DamageType.Tornado => TileModifierType.Tornado,
            DamageType.Boomerang => TileModifierType.Boomerang, DamageType.Electric => TileModifierType.Electric, _ => default };
        return type >= DamageType.Explosion && type <= DamageType.Electric;
    }

    public static void BuildModifiers(IReadOnlyList<ActiveResonance> active, List<StatModifier> output)
    {
        output.Clear();
        foreach (var resonance in active) foreach (var effect in resonance.effects)
        {
            if (effect.kind == ResonanceEffectKind.Stat) Merge(output, effect.statType, effect.operation, effect.value);
            if (effect.kind == ResonanceEffectKind.Resources)
            {
                Merge(output, StatType.GoldGainMultiplier, ModifierOperation.MorePercent, effect.value);
                Merge(output, StatType.IronGainMultiplier, ModifierOperation.MorePercent, effect.value);
                Merge(output, StatType.StoneGainMultiplier, ModifierOperation.MorePercent, effect.value);
            }
        }
    }

    static void Merge(List<StatModifier> output, StatType stat, ModifierOperation operation, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        for (int i = 0; i < output.Count; i++)
        {
            var old = output[i];
            if (old.statType != stat || old.operation != operation) continue;
            old.value = stat == StatType.PlantSpawnRate ? Mathf.Min(old.value, value) : Mathf.Max(old.value, value);
            output[i] = old; return;
        }
        output.Add(new StatModifier { statType = stat, target = StatTarget.Planter, operation = operation, value = value });
    }

    public static float Multiplier(IReadOnlyList<ActiveResonance> active, StatType stat,
        PlantRarity rarity = PlantRarity.Common)
    {
        float result = 1f;
        foreach (var resonance in active) foreach (var effect in resonance.effects)
        {
            bool match = effect.kind == ResonanceEffectKind.Stat && effect.statType == stat && effect.operation == ModifierOperation.MorePercent;
            match |= effect.kind == ResonanceEffectKind.RareScore && stat == StatType.HarvestScoreMultiplier && rarity >= PlantRarity.Rare;
            if (match) result = Mathf.Max(result, 1f + effect.value);
        }
        return result;
    }

    // Only conditional source XP transfers across planters, never the source's ordinary Water bonus.
    public static float ElectricXP(IReadOnlyList<ActiveResonance> active)
    {
        float result = 1f;
        foreach (var resonance in active) foreach (var effect in resonance.effects)
            if (effect.kind == ResonanceEffectKind.ElectricXP) result = Mathf.Max(result, 1f + effect.value);
        return result;
    }

    public static float BehaviorMultiplier(IReadOnlyList<ActiveResonance> active, DamageType damage)
    {
        float result = 1f;
        foreach (var resonance in active)
            if ((resonance.behaviorMask & (1 << (int)damage)) != 0)
                foreach (var effect in resonance.effects)
                    if (effect.kind == ResonanceEffectKind.BehaviorDamage) result = Mathf.Max(result, 1f + effect.value);
        return result;
    }
}
