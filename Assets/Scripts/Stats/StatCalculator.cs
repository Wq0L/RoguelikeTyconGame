using System.Collections.Generic;
using UnityEngine;

public static class StatCalculator
{
    public static float Calculate(
        float baseValue,
        StatType statType,
        StatTarget contextTarget,
        IReadOnlyList<StatModifier> globalModifiers,
        IReadOnlyList<StatModifier> localModifiers
    )
    {
        float flatBonus = 0f;
        float addPercentBonus = 0f;
        float moreMultiplier = 1f;

        bool hasSetValue = false;
        float setValue = baseValue;

        ApplyModifiers(
            globalModifiers,
            statType,
            contextTarget,
            ref flatBonus,
            ref addPercentBonus,
            ref moreMultiplier,
            ref hasSetValue,
            ref setValue
        );

        ApplyModifiers(
            localModifiers,
            statType,
            contextTarget,
            ref flatBonus,
            ref addPercentBonus,
            ref moreMultiplier,
            ref hasSetValue,
            ref setValue
        );

        float finalValue = hasSetValue ? setValue : baseValue;

        finalValue += flatBonus;
        finalValue *= 1f + addPercentBonus;
        finalValue *= moreMultiplier;

        return ClampStat(statType, finalValue);
    }

    private static void ApplyModifiers(
        IReadOnlyList<StatModifier> modifiers,
        StatType statType,
        StatTarget contextTarget,
        ref float flatBonus,
        ref float addPercentBonus,
        ref float moreMultiplier,
        ref bool hasSetValue,
        ref float setValue
    )
    {
        if (modifiers == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];

            if (modifier.statType != statType)
                continue;

            if (!TargetMatches(modifier.target, contextTarget))
                continue;

            switch (modifier.operation)
            {
                case ModifierOperation.Flat:
                    flatBonus += modifier.value;
                    break;

                case ModifierOperation.AddPercent:
                    addPercentBonus += modifier.value;
                    break;

                case ModifierOperation.MorePercent:
                    moreMultiplier *= 1f + modifier.value;
                    break;

                case ModifierOperation.Set:
                    hasSetValue = true;
                    setValue = modifier.value;
                    break;
            }
        }
    }

    private static bool TargetMatches(StatTarget modifierTarget, StatTarget contextTarget)
    {
        return modifierTarget == StatTarget.All || modifierTarget == contextTarget;
    }

    private static float ClampStat(StatType statType, float value)
    {
        switch (statType)
        {
            // === HASAT ===
            case StatType.HarvestDamage:
            case StatType.AreaRadius:
                return Mathf.Max(0f, value);

            case StatType.AttackSpeed:
                return Mathf.Max(0.05f, value);

            case StatType.CritChance:
                return Mathf.Clamp(value, 0f, 1f);

            case StatType.CritMultiplier:
                return Mathf.Max(1f, value);

            // === ÜRETİM ===
            case StatType.PlantSpawnRate:
                return Mathf.Max(0.1f, value);

            case StatType.RareSpawnChance:
                return Mathf.Clamp(value, 0f, 95f);

            // === EKONOMİ ===
            case StatType.GoldGainMultiplier:
            case StatType.IronGainMultiplier:
            case StatType.StoneGainMultiplier:
            case StatType.XPGainMultiplier:
            case StatType.HarvestScoreMultiplier:
                return Mathf.Max(0f, value);

            // === META ===
            case StatType.MutationLuck:
                return Mathf.Max(0f, value);

            case StatType.GridUnlockSize:
                return Mathf.Max(1f, value);

            case StatType.RoundDuration:
                return Mathf.Max(1f, value);

            case StatType.StartingGoldBonus:
            case StatType.StartingPlanterCount:
                return Mathf.Max(0f, value);

            // === KART AJANSI ===
            case StatType.ExtraCardChoice:
            case StatType.RerollCard:
            case StatType.CardSkip:
                return Mathf.Max(0f, value);

            case StatType.ExplosionChance:
                return Mathf.Clamp(value, 0f, 1f);
            case StatType.DuplicateChance:
                return Mathf.Clamp(value, 0f, 1f);

            default:
                return value;
        }
    }
}