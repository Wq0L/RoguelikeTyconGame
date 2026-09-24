using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClickerGame.EconomyAnalysis
{
    public static class EconomyAnalyzerVerification
    {
        static int checks;
        static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Economy verification failed: " + message);
        }
        static void Equal(double actual, double expected, string message, double tolerance = .0001)
            => Require(Math.Abs(actual - expected) <= tolerance, message + $" (actual={actual}, expected={expected})");

        [MenuItem("Tools/Economy Analyzer Verification")]
        public static void Run()
        {
            checks = 0;
            var temporary = new List<UnityEngine.Object>();
            T Make<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); temporary.Add(item); return item; }
            try
            {
                var plant = Make<PlantSO>(); plant.maxHealth = 5; plant.rewardAmount = 1; plant.xpAmount = 5;
                plant.resourceType = ResourceType.Gold; plant.rarity = PlantRarity.Common;
                var disabled = Make<PlantSO>(); disabled.rarity = PlantRarity.Legendary;
                var planter = Make<PlanterSO>(); planter.sizeX = 2; planter.sizeZ = 3;
                planter.spawnTable = new List<PlantSpawnEntry> { new() { plant = disabled, baseChance = 0 }, new() { plant = plant, baseChance = 10 } };
                var xp = Make<ProgressionSO>(); xp.baseXP = 5; xp.xpMultiplier = 1.2f;
                var input = new EconomyInput
                {
                    Planter = planter, Round = 1, SpawnerCount = 1, EffectiveTargets = 1, Duration = 30,
                    SpawnInterval = 1, AttackInterval = 1, Damage = 1, CritMultiplier = 2, RareBonus = 10,
                    GoldMultiplier = 1.25f, IronMultiplier = 1, StoneMultiplier = 1, XpMultiplier = 1.1f, PlanterDamageMultiplier = 1
                };
                var snapshot = EconomyCalculator.Analyze(input);
                Equal(snapshot.Plants[0].Probability, 0, "zero weight stays impossible");
                Equal(snapshot.Plants[1].Probability, 1, "weights normalize");
                Equal(snapshot.PerSpawn.Gold, 1, "round reward per plant, before expectation");
                Equal(snapshot.PerSpawn.Xp, 6, "Mathf midpoint reward rounding");
                input.DuplicateChance = 1;
                Equal(EconomyCalculator.Analyze(input).PerSpawn.Gold, 2, "duplicate after rounding");
                input.DuplicateChance = 0;
                Equal(EconomyCalculator.AdjustedWeight(new PlantSpawnEntry { plant = disabled, baseChance = 10 }, 10), 13, "legendary weight");
                var trial = EconomySimulation.RunTrial(new[] { snapshot }, xp, 60, 17, 0);
                var repeated = EconomySimulation.RunTrial(new[] { snapshot }, xp, 60, 17, 0);
                Equal(trial[0].Income.Harvests, repeated[0].Income.Harvests, "seeded replay");
                Require(trial[0].Income.Harvests < snapshot.ProductionCeiling, "alive lanes block spawning");
                Require(trial[0].Income.Harvests <= 6 && trial[0].Income.Harvests >= 4, "five-hit lifecycle throughput");
                Equal(trial[0].Income.Gold, trial[0].Income.Harvests, "one delivery per death");
                Equal(trial[0].Income.Xp, trial[0].Income.Harvests * 6, "actual rounded XP deliveries");
                var distribution = EconomyDistribution.Of(new double[] { 0, 10, 20, 30, 40 });
                Equal(distribution.Average, 20, "mean"); Equal(distribution.P50, 20, "median");
                Equal(distribution.P10, 4, "P10 interpolation"); Equal(distribution.P90, 36, "P90 interpolation");

                var node = Make<SkillNodeSO>();
                node.tiers.Add(new SkillNodeTier { effects = new List<StatModifier> { new() { statType = StatType.GoldGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.AddPercent, value = .1f } } });
                node.tiers.Add(new SkillNodeTier { effects = new List<StatModifier> { new() { statType = StatType.GoldGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.AddPercent, value = .3f } } });
                var max = EconomyCalculator.BuildModifiers(EconomyBuild.MaxSkillTree, new[] { node, node }, null);
                Require(max.Count == 1, "max tier replacement and duplicate roster entries");
                Equal(StatCalculator.Calculate(1, StatType.GoldGainMultiplier, StatTarget.Planter, max, null), 1.3, "old tier does not stack");
                Require(double.IsInfinity(EconomyCalculator.PurchaseWait(new EconomyAmounts { Iron = 1 }, new EconomyAmounts { Gold = 99 }).rounds), "unreachable purchase");
                Require(double.IsNaN(EconomyCalculator.Roi(new EconomyAmounts { Gold = 1 }, new EconomyAmounts { Gold = 2 }, new EconomyAmounts { Gold = 1 }, input)), "negative delta ROI N/A");
                Equal(EconomyXp.CumulativeTo(3, xp, false), 11, "current cumulative thresholds");
                Equal(EconomyXp.CumulativeTo(3, xp, true), 83.25, "proposed curve separate");

                var live = EconomyEditorData.Defaults(); temporary.Add(live);
                Require(live.planter != null && live.coreStats != null && live.healthScaling != null && live.currentXpCurve != null, "real source assets loaded");
                if (Application.isBatchMode)
                {
                    var manager = UnityEngine.Object.FindFirstObjectByType<SkillTreeManager>(FindObjectsInactive.Include);
                    Require(manager != null, "GameScene skill manager loads");
                    var roster = new SerializedObject(manager).FindProperty("allNodes");
                    Require(roster.arraySize == live.skillTree.Count, "GameScene contains the complete skill roster");
                    var bound = new HashSet<SkillNodeSO>();
                    for (int i = 0; i < roster.arraySize; i++) bound.Add(roster.GetArrayElementAtIndex(i).objectReferenceValue as SkillNodeSO);
                    Require(bound.SetEquals(live.skillTree), "all scene manager skill references resolve after Unity import");
                    var uiNodes = UnityEngine.Object.FindObjectsByType<SkillNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    var uiBindings = uiNodes.Select(n => new SerializedObject(n).FindProperty("node").objectReferenceValue as SkillNodeSO).ToArray();
                    Require(uiBindings.Length == live.skillTree.Count && new HashSet<SkillNodeSO>(uiBindings).SetEquals(live.skillTree),
                        "all authored skill buttons resolve after Unity scene load");
                    var progression = UnityEngine.Object.FindFirstObjectByType<ProgressionManager>(FindObjectsInactive.Include);
                    Require(progression != null && new SerializedObject(progression).FindProperty("progressionData").objectReferenceValue == live.currentXpCurve,
                        "GameScene uses the calibrated live XP asset");
                }
                int spawners = EconomyEditorData.Spawners(live, out string source);
                Equal(spawners, 6, "real 2x3 prefab production lanes");
                var row = new EconomyRoundAssumption { build = EconomyBuild.MaxSkillTree, durationOverride = 90, effectiveTargets = 6 };
                live.history = new List<EconomyRoundAssumption> { row };
                var off = EconomyEditorData.History(live, false);
                var on = EconomyEditorData.History(live, true);
                Equal(off[128].Input.Damage, 3201552, "actual max damage (float accumulation)", 2);
                Equal(off[128].Input.SpawnInterval, .5, "actual max spawn interval");
                Equal(off[128].Input.AttackInterval, .2, "actual max attack interval");
                Equal(off[128].Input.GoldMultiplier, 4, "actual max gold multiplier");
                Equal(off[128].Input.IronMultiplier, 5, "actual max iron multiplier");
                Equal(off[128].Input.StoneMultiplier, 5, "actual max stone multiplier");
                Equal(off[128].Input.XpMultiplier, 3, "actual max XP multiplier");
                var oldSpeed = live.skillTree.Where(n => !n.nodeName.StartsWith("Seri Üretim") && !n.nodeName.StartsWith("Yıldırım Kesim"));
                var oldEffects = EconomyCalculator.BuildModifiers(EconomyBuild.MaxSkillTree, oldSpeed, null);
                Equal(StatCalculator.Calculate(5, StatType.PlantSpawnRate, StatTarget.Planter, oldEffects, null), 3.6125, "original production skills restored");
                Equal(StatCalculator.Calculate(3, StatType.AttackSpeed, StatTarget.Player, oldEffects, null), 1.47, "original attack skills restored");
                Require(live.skillTree.Count(n => n.nodeName.StartsWith("Seri Üretim")) == 3 &&
                    live.skillTree.Count(n => n.nodeName.StartsWith("Yıldırım Kesim")) == 3, "three new skills per speed branch");
                var tornado = live.skillTree.Single(n => n.unlockType == UnlockType.TileBehavior_Tornado);
                Require(tornado.tiers.Count == 1 && tornado.tiers[0].costType == ResourceType.Iron && tornado.tiers[0].cost == 40,
                    "Tornado unlock costs 40 Iron");
                Require(tornado.prerequisites.Count == 1 && tornado.prerequisites[0].node.unlockType == UnlockType.TileBehavior_Explosive,
                    "Tornado follows Explosive cards");
                var tornadoTiles = AssetDatabase.FindAssets("t:TileModifierSO", new[] { "Assets/ScriptableObjects/GridModifiers/Tornado" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<TileModifierSO>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
                Require(tornadoTiles.Length == 4 && tornadoTiles.All(t => t.requiredUnlock == UnlockType.TileBehavior_Tornado), "all Tornado rarities gated");
                if (Application.isBatchMode)
                {
                    var cards = UnityEngine.Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
                    Require(cards != null, "GameScene card picker exists");
                    var pool = new SerializedObject(cards).FindProperty("allModifiers");
                    var entries = new HashSet<UnityEngine.Object>();
                    for (int i = 0; i < pool.arraySize; i++) entries.Add(pool.GetArrayElementAtIndex(i).objectReferenceValue);
                    Require(tornadoTiles.All(t => entries.Contains(t)), "all Tornado rarities are wired to the scene card picker");
                }
                if (UnlockManager.Instance == null)
                {
                    var go = new GameObject("Economy verification unlock fixture"); temporary.Add(go);
                    var unlocks = go.AddComponent<UnlockManager>();
                    if (UnlockManager.Instance == null) go.SendMessage("Awake");
                    Require(tornadoTiles.All(t => !t.IsAvailableInCardPool), "Tornado absent before unlock");
                    unlocks.Unlock(tornado.unlockType);
                    Require(tornadoTiles.All(t => t.IsAvailableInCardPool), "Tornado enters pool after skill unlock");
                    unlocks.ResetUnlocks();
                    Require(tornadoTiles.All(t => !t.IsAvailableInCardPool), "new run locks Tornado again");
                }
                Require(live.currentXpCurve.useAuthoredRequirements && live.currentXpCurve.xpRequirements.Count == 120,
                    "live XP curve has 120 authored level costs");
                foreach (var cost in live.currentXpCurve.xpRequirements) Require(cost > 0 && !float.IsInfinity(cost) && !float.IsNaN(cost), "finite positive XP cost");
                Equal(EconomyXp.CumulativeTo(25, live.currentXpCurve, false), 17250, "calibrated level 25 total XP");
                Equal(EconomyXp.CumulativeTo(70, live.currentXpCurve, false), 142191, "calibrated level 70 total XP");
                Equal(EconomyXp.CumulativeTo(121, live.currentXpCurve, false), 6190107, "calibrated level 121 total XP");
                Equal(live.currentXpCurve.GetXPForLevel(121), live.currentXpCurve.GetXPForLevel(120), "post-121 XP remains useful");
                Equal(EconomyXp.State(6190107, live.currentXpCurve, false).level, 121, "cumulative XP resolves live level 121");
                Equal(EconomyXp.CumulativeTo(3, live.currentXpCurve, true), 83.25, "proposed curve remains independent of live tuning");
                Equal(off[128].Estimate.Gold, 9092.64706, "real asset analytical R129", .03);
                Equal(StatCalculator.Calculate(.5f, StatType.PlantSpawnRate, StatTarget.Planter, null,
                    new[] { new StatModifier { statType = StatType.PlantSpawnRate, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = -.9f } }),
                    .5, "resonance cannot bypass the production interval floor");
                var p5 = live.skillTree.Single(n => n.unlockType == UnlockType.Planter_2x2);
                var p8 = live.skillTree.Single(n => n.unlockType == UnlockType.Planter_2x3);
                Require(p5.prerequisites.All(p => p.level == 1), "2x2 does not require 7x7 grid");
                Require(p8.prerequisites.All(p => p.node.tiers.All(t => t.effects.All(e => e.statType != StatType.GridUnlockSize || e.value <= 7))),
                    "2x3 does not require 9x9 grid");
                Require(p8.tiers[0].costType == ResourceType.Gold && p8.tiers[0].cost == 180, "early 2x3 unlock price");
                Require(live.planter.costType == ResourceType.Stone && live.planter.cost == 20, "first 2x3 purchase cost");
                row.extraGlobalModifiers.Add(new StatModifier { statType = StatType.AttackSpeed, target = StatTarget.Player, operation = ModifierOperation.Set, value = .01f });
                Equal(EconomyCalculator.Resolve(live, row, 1, 6, false).AttackInterval, .1, "PlayerController clamp wins");
                row.extraGlobalModifiers.Clear();

                var water = Make<TileModifierSO>(); water.modifierType = TileModifierType.Water;
                for (int i = 0; i < 3; i++) row.tiles.Add(new EconomyTile { cell = new Vector2Int(i % 2, i / 2), tile = water,
                    rolledModifiers = new List<StatModifier> { new() { statType = StatType.XPGainMultiplier, target = StatTarget.Planter, operation = ModifierOperation.AddPercent, value = .2f } } });
                var tileOff = EconomyCalculator.Resolve(live, row, 129, 6, false);
                var tileOn = EconomyCalculator.Resolve(live, row, 129, 6, true);
                Equal(tileOff.XpMultiplier, 3.6, "tile rolls retained when resonance off");
                Equal(tileOn.XpMultiplier, 7.2, "only resonance contribution enabled");
                Equal(EconomyCalculator.Resolve(live, row, 129, 6, true, new HashSet<TileModifierType> { TileModifierType.Damage }).XpMultiplier,
                    tileOff.XpMultiplier, "family isolation keeps ordinary tile bonuses");
                Equal(tileOff.SpawnInterval, tileOn.SpawnInterval, "XP A/B keeps unrelated stats unchanged");
                var abOff = EconomySimulation.RunTrial(new[] { EconomyCalculator.Analyze(tileOff) }, xp, 60, 42, 0);
                var abOn = EconomySimulation.RunTrial(new[] { EconomyCalculator.Analyze(tileOn) }, xp, 60, 42, 0);
                Equal(abOff[0].Income.Gold, abOn[0].Income.Gold, "paired streams preserve harvest outcomes in XP-only A/B");
                Equal(abOn[0].Income.Xp, abOff[0].Income.Xp * 2, "XP-only simulation A/B");
                row.tiles.Clear();

                var health = Make<PlantHealthScalingSO>(); health.referenceHealth = 5;
                health.anchors.Add(new PlantHealthAnchor { round = 1, commonHealth = 5 });
                health.anchors.Add(new PlantHealthAnchor { round = 2, commonHealth = 100 });
                EconomyInput CarryInput(int round) => new EconomyInput
                {
                    Planter = planter, Health = health, Round = round, SpawnerCount = 1, EffectiveTargets = 1, Duration = 3,
                    SpawnInterval = 1, AttackInterval = 1, Damage = 1, CritMultiplier = 1, PlanterDamageMultiplier = 1,
                    GoldMultiplier = 1, IronMultiplier = 1, StoneMultiplier = 1, XpMultiplier = 1
                };
                var carry = EconomySimulation.RunTrial(new[] { EconomyCalculator.Analyze(CarryInput(1)), EconomyCalculator.Analyze(CarryInput(2)) }, xp, 60, 17, 0);
                Equal(carry[0].Income.Harvests, 0, "living plant persists after first round");
                Equal(carry[1].Income.Harvests, 1, "living HP and attack phase carry across rounds, no HP rescale");

                var simulations = new EconomySimulationResult();
                for (int i = 0; i < 8; i++) simulations.Trials.Add(EconomySimulation.RunTrial(off, live.currentXpCurve, 60, 12345, i));
                var repeat = EconomySimulation.RunTrial(on, live.currentXpCurve, 60, 12345, 0);
                double sum = 0;
                for (int r = 0; r < 130; r++)
                {
                    var actual = simulations.Trials[0][r]; sum += actual.Income.Xp;
                    Equal(actual.CumulativeXp, sum, "round-by-round cumulative XP R" + (r + 1));
                    Equal(actual.Income.Gold, repeat[r].Income.Gold, "no-tile A/B replay R" + (r + 1));
                }
                var output = new List<string> { $"PASS: {checks} checks", "R129 MAX, no tiles/resonance, 90s, 6 effective targets, 8 seeds; full explicit MAX-from-R1 history", source };
                foreach (var metric in new (string, Func<EconomyTrialRound, double>)[] {
                    ("Gold", r => r.Income.Gold), ("Iron", r => r.Income.Iron), ("Stone", r => r.Income.Stone),
                    ("XP", r => r.Income.Xp), ("Harvests", r => r.Income.Harvests) })
                {
                    var d = simulations.Distribution(128, metric.Item2);
                    output.Add($"{metric.Item1}: average={d.Average:F3}; P50={d.P50:F3}; P10={d.P10:F3}; P90={d.P90:F3}");
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/EconomyAnalyzerVerification.txt", output);
                Debug.Log(string.Join("\n", output));
            }
            finally { foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item); }
        }

        public static void RunBatch()
        {
            try
            {
                // Load before creating transient fixtures: replacing a scene can unload them.
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
                HarvestPolishVerification.VerifyFinalTree(); Run(); EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
    }
}
