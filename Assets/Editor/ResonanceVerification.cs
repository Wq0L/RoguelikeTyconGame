using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResonanceVerification
{
    [MenuItem("Tools/Resonance/Run Verification")]
    public static void RunVerification()
    {
        var temporary = new List<Object>();
        try
        {
            ResonanceRulesSO config = Resources.Load<ResonanceRulesSO>("ResonanceRules");
            Require(config != null, "Resources configuration loads");
            foreach (string rarity in new[] { "Common", "Rare", "Epic", "Legendary" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TileModifierSO>(
                    $"Assets/ScriptableObjects/GridModifiers/Damage/Damage-{rarity}.asset");
                Require(asset != null && asset.modifierType == TileModifierType.Damage &&
                    asset.modifierRanges.Count == 1 && asset.modifierRanges[0].statType == StatType.PlanterDamageMultiplier,
                    "Serialized damage asset binding " + rarity);
            }
            var counts = new Dictionary<TileModifierType, int>();
            var modifiers = new List<StatModifier>();
            var active = new List<ActiveResonance>();
            foreach (var sample in new[] { (0, 1f), (1, 1f), (2, 2f), (3, 10f), (4, 10f), (6, 10f), (1, 1f) })
            {
                counts[TileModifierType.Water] = sample.Item1;
                ResonanceManager.Evaluate(config, counts, modifiers, active);
                Equal(StatCalculator.Calculate(1f, StatType.XPGainMultiplier, StatTarget.Planter, null, modifiers),
                    sample.Item2, "XP highest tier and downgrade " + sample.Item1);
            }
            counts[TileModifierType.Water] = 3;
            counts[TileModifierType.Damage] = 3;
            counts[TileModifierType.Fertile] = 3;
            ResonanceManager.Evaluate(config, counts, modifiers, active);
            Require(active.Count == 3, "Different families coexist");
            Equal(StatCalculator.Calculate(1f, StatType.PlanterDamageMultiplier, StatTarget.Planter, null, modifiers), 4f, "Damage family");
            Equal(StatCalculator.Calculate(5f, StatType.PlantSpawnRate, StatTarget.Planter, null, modifiers), 2.5f, "Interval direction");
            Equal(StatCalculator.Calculate(1f, StatType.XPGainMultiplier, StatTarget.Player, null, modifiers), 1f, "No player leakage");

            if (StatManager.Instance == null)
            {
                var go = new GameObject("ResonanceVerification_Stats"); temporary.Add(go);
                StatManager stats = go.AddComponent<StatManager>();
                // Edit Mode does not automatically run Awake on ordinary MonoBehaviours.
                if (StatManager.Instance == null)
                    typeof(StatManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(stats, null);
            }
            var data = ScriptableObject.CreateInstance<PlanterSO>(); temporary.Add(data);
            var water = Tile(TileModifierType.Water, StatType.XPGainMultiplier, .2f, temporary);
            var damage = Tile(TileModifierType.Damage, StatType.PlanterDamageMultiplier, .25f, temporary);
            var grids = new List<GridObject>();
            for (int i = 0; i < 3; i++)
            {
                var ground = new GameObject("ResonanceVerification_Cell" + i); temporary.Add(ground);
                var cell = ground.AddComponent<GroundCell>();
                var grid = new GridObject(null, new GridPosition(i, 0));
                grid.SetGroundObject(ground); cell.SetGridObject(grid); cell.ApplyModifier(water); grids.Add(grid);
            }
            var ownerGO = new GameObject("ResonanceVerification_Planter"); temporary.Add(ownerGO);
            var owner = ownerGO.AddComponent<PlanterBrain>(); owner.Initialize(data, grids);
            foreach (var grid in grids) grid.SetPlanterBrain(owner);
            // Include any existing global XP modifier in the expected ordinary stat.
            float ordinary = StatCalculator.Calculate(1f, StatType.XPGainMultiplier, StatTarget.Planter,
                StatManager.Instance.GlobalModifiers, owner.LocalModifiers);
            Equal(owner.GetFinalStat(StatType.XPGainMultiplier), ordinary * 10f, "Real planter XP pipeline");
            owner.RefreshTileBuffs(); owner.RefreshTileBuffs();
            Require(owner.LocalModifiers.Count == 3, "Refresh does not accumulate modifiers");
            grids[2].GetGroundCellCached().ApplyModifier(damage);
            Require(owner.ActiveResonances.Count == 1 && owner.ActiveResonances[0].tileCount == 2,
                "Replacement removes old family count and invalidates cached tier");
            Require(owner.LocalModifiers.Count == 3, "Replacement removes old rolled modifier");
            ordinary = StatCalculator.Calculate(1f, StatType.XPGainMultiplier, StatTarget.Planter,
                StatManager.Instance.GlobalModifiers, owner.LocalModifiers);
            Equal(owner.GetFinalStat(StatType.XPGainMultiplier), ordinary * 2f, "XP downgrade invalidates cache");
            foreach (var grid in grids) grid.GetGroundCellCached().ApplyModifier(damage);
            var plantData = ScriptableObject.CreateInstance<PlantSO>(); temporary.Add(plantData);
            var plantGO = new GameObject("ResonanceVerification_Plant"); temporary.Add(plantGO);
            var health = plantGO.AddComponent<PlantHealth>(); health.Initialize(plantData, owner);
            float normalDamage = StatCalculator.Calculate(1f, StatType.PlanterDamageMultiplier,
                StatTarget.Planter, StatManager.Instance.GlobalModifiers, owner.LocalModifiers) * 4f;
            Require(health.GetIncomingDamage(100) == (int)Math.Round(100 * normalDamage), "Actual target damage transfer");
            Require(health.GetIncomingDamage(100, true) == 100, "Explosion multiplier not doubled");
            health.Initialize(plantData);
            Require(health.GetIncomingDamage(100) == 100, "Other/unowned plant remains independent");
            foreach (var grid in grids) grid.GetGroundCellCached().ApplyModifier(null);
            Require(owner.LocalModifiers.Count == 0 && owner.ActiveResonances.Count == 0, "Clearing cells removes all effects");
            Debug.Log("RESONANCE_VERIFICATION_PASS: thresholds, downgrade, coexistence, target isolation, refresh, replacement, damage and clearing.");
        }
        finally
        {
            for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
        }
    }

    private static TileModifierSO Tile(TileModifierType family, StatType stat, float amount, List<Object> temporary)
    {
        var tile = ScriptableObject.CreateInstance<TileModifierSO>(); temporary.Add(tile);
        tile.modifierType = family;
        tile.modifierRanges.Add(new StatModifierRange { statType = stat, target = StatTarget.Planter,
            operation = ModifierOperation.AddPercent, minValue = amount, maxValue = amount });
        return tile;
    }
    private static void Equal(float actual, float expected, string message)
        => Require(Mathf.Abs(actual - expected) < .001f, message + $": {actual} != {expected}");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Resonance verification failed: " + message);
    }
}
