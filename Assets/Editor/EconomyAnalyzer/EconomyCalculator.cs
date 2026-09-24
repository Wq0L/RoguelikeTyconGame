using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickerGame.EconomyAnalysis
{
    public sealed class EconomyInput
    {
        public PlanterSO Planter;
        public PlantHealthScalingSO Health;
        public int Round, SpawnerCount, EffectiveTargets;
        public float Duration, SpawnInterval, AttackInterval, Damage, CritChance, CritMultiplier;
        public float Radius, RareBonus, GoldMultiplier, IronMultiplier, StoneMultiplier, XpMultiplier;
        public float DuplicateChance, PlanterDamageMultiplier, ExplosionChance, TornadoChance, BoomerangChance, ElectricChance;
        public float ScoreMultiplier = 1, RareScoreMultiplier = 1;
        public double GoldWeight = 1, IronWeight = 7, StoneWeight = 14;
    }

    public sealed class EconomyAmounts
    {
        public double Gold, Iron, Stone, Xp, Score, Harvests;
        public double Currency(ResourceType type) => type == ResourceType.Gold ? Gold : type == ResourceType.Iron ? Iron : Stone;
        public void AddCurrency(ResourceType type, double amount)
        {
            if (type == ResourceType.Gold) Gold += amount;
            else if (type == ResourceType.Iron) Iron += amount;
            else Stone += amount;
        }
        public double Weighted(EconomyInput input) => Gold * input.GoldWeight + Iron * input.IronWeight + Stone * input.StoneWeight;
        public EconomyAmounts Scale(double factor) => new() { Gold = Gold * factor, Iron = Iron * factor,
            Stone = Stone * factor, Xp = Xp * factor, Score = Score * factor, Harvests = Harvests * factor };
    }

    public sealed class PlantProbability
    {
        public PlantSO Plant;
        public double Probability;
        public int Hp, RoundedReward, RoundedXp;
    }

    public sealed class EconomySnapshot
    {
        public EconomyInput Input;
        public List<PlantProbability> Plants;
        public EconomyAmounts PerSpawn, Estimate;
        public double ProductionCeiling, CombatCapacity, EstimatedHarvests, Utilization, CombatToSpawnRatio;
        public string Bottleneck;
    }

    public static class EconomyCalculator
    {
        public static List<StatModifier> BuildModifiers(EconomyBuild build, IEnumerable<SkillNodeSO> roster,
            IEnumerable<SkillPurchase> purchases)
        {
            var result = new List<StatModifier>();
            if (build == EconomyBuild.Base) return result;
            var selected = new Dictionary<SkillNodeSO, int>();
            if (build == EconomyBuild.MaxSkillTree)
            {
                foreach (var node in roster ?? Array.Empty<SkillNodeSO>())
                    if (node != null) selected[node] = node.tiers.Count;
            }
            else
                foreach (var purchase in purchases ?? Array.Empty<SkillPurchase>())
                {
                    if (purchase?.node == null) throw new ArgumentException("Custom purchase has no node.");
                    if (selected.ContainsKey(purchase.node)) throw new ArgumentException("Duplicate custom node: " + purchase.node.name);
                    if (purchase.level < 0 || purchase.level > purchase.node.tiers.Count)
                        throw new ArgumentException("Invalid tier: " + purchase.node.name);
                    selected[purchase.node] = purchase.level;
                }
            foreach (var entry in selected)
                if (entry.Value > 0) result.AddRange(entry.Key.tiers[entry.Value - 1].effects);
            return result;
        }

        public static EconomyInput Resolve(EconomyBalanceProfileSO profile, EconomyRoundAssumption row,
            int round, int spawners, bool resonance, ISet<TileModifierType> resonanceFamilies = null)
        {
            if (profile.planter == null || profile.coreStats == null) throw new ArgumentException("Select planter and CoreStatsSO.");
            var global = BuildModifiers(row.build, profile.skillTree, row.purchases);
            global.AddRange(row.extraGlobalModifiers);
            var local = new List<StatModifier>();
            var counts = new Dictionary<TileModifierType, int>();
            var cells = new HashSet<Vector2Int>();
            foreach (var tile in row.tiles)
            {
                if (tile.tile == null) throw new ArgumentException("Tile entry needs a TileModifierSO.");
                if (tile.cell.x < 0 || tile.cell.y < 0 || tile.cell.x >= profile.planter.sizeX || tile.cell.y >= profile.planter.sizeZ)
                    throw new ArgumentException("Tile outside planter footprint.");
                if (!cells.Add(tile.cell)) throw new ArgumentException("Two tiles occupy the same cell.");
                local.AddRange(tile.rolledModifiers);
                counts.TryGetValue(tile.tile.modifierType, out int count);
                counts[tile.tile.modifierType] = count + 1;
            }
            var resonanceModifiers = new List<StatModifier>();
            var active = new List<ActiveResonance>();
            if (resonance)
            {
                ResonanceManager.Evaluate(profile.resonanceRules, counts, resonanceModifiers, active);
                if (resonanceFamilies != null)
                {
                    active.RemoveAll(item => !resonanceFamilies.Contains(item.tileType));
                    ResonanceManager.BuildModifiers(active, resonanceModifiers);
                }
            }
            float Player(StatType stat) => StatCalculator.Calculate(profile.coreStats.GetBaseStat(stat), stat, StatTarget.Player, global, null);
            float Planter(StatType stat)
            {
                float ordinary = StatCalculator.Calculate(profile.planter.GetBaseStat(stat), stat, StatTarget.Planter, global, local);
                return StatCalculator.Calculate(ordinary, stat, StatTarget.Planter, null, resonanceModifiers);
            }
            // RoundManager requests StatTarget.All, not Round.
            float duration = StatCalculator.Calculate(profile.coreStats.GetBaseStat(StatType.RoundDuration),
                StatType.RoundDuration, StatTarget.All, global, null);
            float ordinaryScore = Player(StatType.HarvestScoreMultiplier) * StatCalculator.Calculate(
                profile.planter.GetBaseStat(StatType.HarvestScoreMultiplier), StatType.HarvestScoreMultiplier,
                StatTarget.Planter, global, local, false);
            var input = new EconomyInput
            {
                Planter = profile.planter, Health = profile.healthScaling, Round = round, SpawnerCount = spawners,
                EffectiveTargets = row.effectiveTargets, Duration = Mathf.Clamp(row.durationOverride > 0 ? row.durationOverride : duration, 30, 90),
                SpawnInterval = Planter(StatType.PlantSpawnRate), AttackInterval = Mathf.Max(.1f, Player(StatType.AttackSpeed)),
                Damage = Player(StatType.HarvestDamage), CritChance = Player(StatType.CritChance), CritMultiplier = Player(StatType.CritMultiplier),
                Radius = Player(StatType.AreaRadius), RareBonus = Planter(StatType.RareSpawnChance),
                GoldMultiplier = Planter(StatType.GoldGainMultiplier), IronMultiplier = Planter(StatType.IronGainMultiplier),
                StoneMultiplier = Planter(StatType.StoneGainMultiplier), XpMultiplier = Planter(StatType.XPGainMultiplier),
                DuplicateChance = Planter(StatType.DuplicateChance), PlanterDamageMultiplier = Planter(StatType.PlanterDamageMultiplier),
                ExplosionChance = Planter(StatType.ExplosionChance), TornadoChance = Planter(StatType.TornadoChance),
                BoomerangChance = Planter(StatType.BoomerangChance), ElectricChance = Planter(StatType.ElectricChance),
                GoldWeight = profile.goldWeight, IronWeight = profile.ironWeight, StoneWeight = profile.stoneWeight
            };
            input.ScoreMultiplier = ordinaryScore * ResonanceManager.Multiplier(active, StatType.HarvestScoreMultiplier);
            input.RareScoreMultiplier = ordinaryScore * ResonanceManager.Multiplier(active, StatType.HarvestScoreMultiplier, PlantRarity.Rare);
            Validate(input);
            return input;
        }

        public static void Validate(EconomyInput input)
        {
            if (input == null || input.Planter == null || input.SpawnerCount < 1 || input.SpawnerCount > 1024 || input.EffectiveTargets < 1)
                throw new ArgumentException("Planter, 1–1024 production lanes and positive EffectiveTargets required.");
            foreach (double value in new double[] { input.Duration, input.SpawnInterval, input.AttackInterval, input.Damage,
                input.CritChance, input.CritMultiplier, input.PlanterDamageMultiplier, input.RareBonus, input.GoldMultiplier,
                input.IronMultiplier, input.StoneMultiplier, input.XpMultiplier, input.DuplicateChance,
                input.GoldWeight, input.IronWeight, input.StoneWeight })
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentException("Invalid non-finite or negative input.");
            if (input.Duration <= 0 || input.SpawnInterval < StatCalculator.MinimumSpawnInterval || input.AttackInterval < .1f || input.Round < 1)
                throw new ArgumentException("Invalid duration/round or intervals below gameplay minimum.");
        }

        public static float AdjustedWeight(PlantSpawnEntry entry, float bonus)
        {
            if (entry.baseChance <= 0) return 0;
            float chance = entry.baseChance;
            switch (entry.plant.rarity)
            {
                case PlantRarity.Uncommon: chance *= 1f + bonus * .01f; break;
                case PlantRarity.Rare: chance *= 1f + bonus * .01f * 1.5f; break;
                case PlantRarity.Epic: chance *= 1f + bonus * .01f * 2f; break;
                case PlantRarity.Legendary: chance *= 1f + bonus * .01f * 3f; break;
            }
            return chance;
        }

        public static int Reward(PlantSO plant, EconomyInput input)
        {
            float multiplier = plant.resourceType == ResourceType.Gold ? input.GoldMultiplier :
                plant.resourceType == ResourceType.Iron ? input.IronMultiplier : input.StoneMultiplier;
            return Mathf.RoundToInt(plant.rewardAmount * multiplier);
        }
        public static int Xp(PlantSO plant, EconomyInput input) => Mathf.RoundToInt(plant.xpAmount * input.XpMultiplier);
        public static int Score(PlantSO plant, EconomyInput input) => HarvestScoreManager.CalculateAward(plant.rarity, 1,
            plant.rarity >= PlantRarity.Rare ? input.RareScoreMultiplier : input.ScoreMultiplier);

        public static EconomySnapshot Analyze(EconomyInput input)
        {
            Validate(input);
            var plants = new List<PlantProbability>();
            float total = 0;
            foreach (var entry in input.Planter.spawnTable)
            {
                if (entry?.plant == null) throw new ArgumentException("Missing plant in spawn table.");
                total += AdjustedWeight(entry, input.RareBonus);
            }
            if (float.IsNaN(total) || float.IsInfinity(total) || total < 0) throw new ArgumentException("Invalid spawn weights.");
            var ev = new EconomyAmounts();
            double hits = 0;
            double damage = input.Damage * (1 + input.CritChance * (input.CritMultiplier - 1)) * input.PlanterDamageMultiplier;
            foreach (var entry in input.Planter.spawnTable)
            {
                double probability = total > 0 ? AdjustedWeight(entry, input.RareBonus) / (double)total : 0;
                var plant = new PlantProbability { Plant = entry.plant, Probability = probability,
                    Hp = input.Health != null ? input.Health.Calculate(entry.plant, input.Round) : Mathf.Max(1, entry.plant.maxHealth),
                    RoundedReward = Reward(entry.plant, input), RoundedXp = Xp(entry.plant, input) };
                plants.Add(plant);
                ev.AddCurrency(plant.Plant.resourceType, probability * Math.Max(0, plant.RoundedReward) * (1 + input.DuplicateChance));
                ev.Xp += probability * plant.RoundedXp;
                ev.Score += probability * Score(plant.Plant, input);
                if (probability > 0) hits += probability * (damage > 0 ? Math.Max(1, plant.Hp / damage) : double.PositiveInfinity);
            }
            double ceiling = total > 0 ? input.SpawnerCount * input.Duration / (double)input.SpawnInterval : 0;
            double combat = hits > 0 ? input.Duration / input.AttackInterval * Math.Min(input.EffectiveTargets, input.SpawnerCount) / hits : 0;
            double harvests = Math.Min(ceiling, combat);
            var estimate = ev.Scale(harvests); estimate.Harvests = harvests;
            double ratio = ceiling > 0 ? combat / ceiling : double.NaN;
            return new EconomySnapshot { Input = input, Plants = plants, PerSpawn = ev, Estimate = estimate,
                ProductionCeiling = ceiling, CombatCapacity = combat, EstimatedHarvests = harvests,
                Utilization = ceiling > 0 ? harvests / ceiling : 0, CombatToSpawnRatio = ratio, Bottleneck = Bottleneck(ratio) };
        }

        public static string Bottleneck(double ratio) => double.IsNaN(ratio) ? "No production" :
            ratio < .75 ? "Strong Combat Bottleneck" : ratio < .9 ? "Combat Bottleneck" :
            ratio <= 1.1 ? "Balanced" : ratio <= 1.5 ? "Spawn Bottleneck" : "Strong Spawn Bottleneck";

        public static (double rounds, string currency) PurchaseWait(EconomyAmounts cost, EconomyAmounts income)
        {
            double longest = 0; string bottleneck = "None";
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (cost.Currency(type) <= 0) continue;
                double wait = income.Currency(type) > 0 ? cost.Currency(type) / income.Currency(type) : double.PositiveInfinity;
                if (wait > longest) { longest = wait; bottleneck = type.ToString(); }
                else if (wait == longest) bottleneck += ", " + type;
            }
            return (longest, bottleneck);
        }

        public static double Roi(EconomyAmounts cost, EconomyAmounts before, EconomyAmounts after, EconomyInput weights)
        {
            double delta = after.Weighted(weights) - before.Weighted(weights);
            return delta > 0 ? cost.Weighted(weights) / delta : double.NaN;
        }
        public static double Ratio(double after, double before) => before > 0 ? after / before : double.NaN;
    }
}
