using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickerGame.EconomyAnalysis
{
    // Stable across runs; deliberately independent from UnityEngine.Random and gameplay state.
    public sealed class EconomyRandom
    {
        uint state;
        public EconomyRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x9e3779b9; }
        public float Next()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state >> 8) / 16777216f;
        }
        public static int Seed(int seed, int trial, int lane, int stream) => unchecked(
            seed * 16777619 ^ (trial + 1) * 486187739 ^ (lane + 1) * 104729 ^ (stream + 1) * 15485863);
    }

    public sealed class EconomyTrialRound
    {
        public EconomyAmounts Income = new();
        public double CumulativeXp, CurrentLevel, ProposedLevel;
    }

    public sealed class EconomyDistribution
    {
        public double Average, P50, P10, P90;
        public static EconomyDistribution Of(IEnumerable<double> values)
        {
            double[] sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0) throw new ArgumentException("No completed simulation trials.");
            double Percentile(double p)
            {
                double index = (sorted.Length - 1) * p;
                int lo = (int)Math.Floor(index), hi = (int)Math.Ceiling(index);
                return sorted[lo] + (sorted[hi] - sorted[lo]) * (index - lo);
            }
            return new EconomyDistribution { Average = sorted.Average(), P50 = Percentile(.5), P10 = Percentile(.1), P90 = Percentile(.9) };
        }
    }

    public sealed class EconomySimulationResult
    {
        public readonly List<EconomyTrialRound[]> Trials = new();
        public EconomyDistribution Distribution(int index, Func<EconomyTrialRound, double> selector)
            => EconomyDistribution.Of(Trials.Select(t => selector(t[index])));
        public EconomyAmounts Average(int index) => new()
        {
            Gold = Distribution(index, r => r.Income.Gold).Average,
            Iron = Distribution(index, r => r.Income.Iron).Average,
            Stone = Distribution(index, r => r.Income.Stone).Average,
            Xp = Distribution(index, r => r.Income.Xp).Average,
            Harvests = Distribution(index, r => r.Income.Harvests).Average
        };
    }

    public static class EconomyXp
    {
        public static double Needed(int level, ProgressionSO current, bool proposed)
        {
            if (proposed) return 35d + 4d * level + .25d * level * level;
            if (current != null && current.useAuthoredRequirements)
            {
                if (current.xpRequirements == null || current.xpRequirements.Count == 0 ||
                    current.xpRequirements.Any(v => v <= 0 || float.IsNaN(v) || float.IsInfinity(v)))
                    throw new ArgumentException("Authored XP costs must be finite and positive.");
                return current.GetXPForLevel(level);
            }
            if (current == null || current.baseXP <= 0 || current.xpMultiplier < 1 ||
                float.IsNaN(current.baseXP) || float.IsNaN(current.xpMultiplier))
                throw new ArgumentException("Current XP curve needs positive baseXP and multiplier >= 1.");
            // Includes the live float precision/overflow behavior.
            return current.GetXPForLevel(level);
        }
        public static double CumulativeTo(int level, ProgressionSO current, bool proposed)
        {
            double total = 0;
            for (int i = 1; i < level; i++) total += Needed(i, current, proposed);
            return total;
        }
        public static (int level, double progress, double needed) State(double cumulative, ProgressionSO current, bool proposed)
        {
            if (double.IsNaN(cumulative) || double.IsInfinity(cumulative) || cumulative < 0)
                throw new ArgumentException("Cumulative XP must be finite and nonnegative.");
            int level = 1;
            double needed = Needed(level, current, proposed);
            while (cumulative >= needed && level < 100000)
            {
                cumulative -= needed;
                needed = Needed(++level, current, proposed);
            }
            if (level == 100000) throw new ArgumentException("XP level safety limit reached.");
            return (level, cumulative, needed);
        }
        public static (int low, int high) Target(int round)
        {
            if (round == 20) return (20, 30);
            if (round == 40) return (40, 50);
            if (round == 65) return (65, 75);
            if (round == 100) return (95, 105);
            if (round >= 115 && round <= 130) return (121, 121);
            return (0, 0); // Never invent a target between reference checkpoints.
        }
    }

    public static class EconomySimulation
    {
        sealed class Lane
        {
            public float Timer;
            public PlantSO Plant;
            public int Hp;
            public EconomyRandom SpawnRandom, DamageRandom, RewardRandom;
        }

        public static EconomyTrialRound[] RunTrial(IReadOnlyList<EconomySnapshot> history, ProgressionSO xpCurve,
            int fps, int seed, int trial)
        {
            if (history == null || history.Count == 0 || fps < 10 || fps > 240) throw new ArgumentException("Invalid history or FPS.");
            int laneCount = history[0].Input.SpawnerCount;
            var lanes = new Lane[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                lanes[i] = new Lane
                {
                    SpawnRandom = new EconomyRandom(EconomyRandom.Seed(seed, trial, i, 0)),
                    DamageRandom = new EconomyRandom(EconomyRandom.Seed(seed, trial, i, 1)),
                    RewardRandom = new EconomyRandom(EconomyRandom.Seed(seed, trial, i, 2))
                };
                // PlantSpawner.Initialize randomizes the initial timer only once.
                lanes[i].Timer = lanes[i].SpawnRandom.Next() * history[0].Input.SpawnInterval;
            }
            var rounds = new EconomyTrialRound[history.Count];
            float attackTimer = 0, dt = 1f / fps;
            double cumulativeXp = 0;
            int nextTarget = 0;
            for (int r = 0; r < history.Count; r++)
            {
                var snapshot = history[r]; var input = snapshot.Input;
                if (input.SpawnerCount != laneCount || (r > 0 && input.Round != history[r - 1].Input.Round + 1))
                    throw new ArgumentException("History must contain consecutive rounds and a constant lane layout.");
                var result = new EconomyTrialRound();
                int frames = (int)Math.Ceiling(input.Duration * fps - 1e-5);
                for (int frame = 0; frame < frames; frame++)
                {
                    // Explicit assumed frame order: all spawners, then player. No timer catch-up.
                    foreach (var lane in lanes)
                    {
                        if (lane.Plant != null) continue;
                        lane.Timer += dt;
                        if (lane.Timer < input.SpawnInterval) continue;
                        lane.Timer = 0;
                        var plant = Roll(snapshot, lane.SpawnRandom.Next());
                        if (plant == null) continue;
                        lane.Plant = plant.Plant;
                        lane.Hp = plant.Hp; // Survives round changes without HP rescaling.
                    }
                    attackTimer += dt;
                    if (attackTimer < input.AttackInterval) continue;
                    attackTimer = 0;
                    int hitCount = 0, start = nextTarget;
                    // Coverage assumption, not a gameplay target cap: rotate through occupied lanes.
                    for (int offset = 0; offset < laneCount && hitCount < input.EffectiveTargets; offset++)
                    {
                        int index = (start + offset) % laneCount;
                        var lane = lanes[index];
                        if (lane.Plant == null) continue;
                        hitCount++; nextTarget = (index + 1) % laneCount;
                        float variance = .85f + lane.DamageRandom.Next() * .3f;
                        int damage = Mathf.RoundToInt(input.Damage * variance);
                        if (lane.DamageRandom.Next() <= input.CritChance)
                            damage = Mathf.RoundToInt(damage * input.CritMultiplier);
                        damage = (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(damage * (double)input.PlanterDamageMultiplier)));
                        lane.Hp -= damage;
                        if (lane.Hp > 0) continue;
                        int reward = EconomyCalculator.Reward(lane.Plant, input);
                        int xp = EconomyCalculator.Xp(lane.Plant, input);
                        if (input.DuplicateChance > 0 && lane.RewardRandom.Next() <= input.DuplicateChance)
                        {
                            reward = unchecked(reward * 2); xp = unchecked(xp * 2);
                        }
                        // ResourceManager ignores nonpositive resource deliveries. XP has no such guard.
                        result.Income.AddCurrency(lane.Plant.resourceType, Math.Max(0, reward));
                        result.Income.Xp += xp;
                        result.Income.Harvests++; // Duplicate doubles rewards, not physical plant deaths.
                        lane.Plant = null;
                        lane.Timer = 0;
                    }
                }
                cumulativeXp += result.Income.Xp;
                result.CumulativeXp = cumulativeXp;
                result.CurrentLevel = EconomyXp.State(cumulativeXp, xpCurve, false).level;
                result.ProposedLevel = EconomyXp.State(cumulativeXp, xpCurve, true).level;
                rounds[r] = result;
            }
            return rounds;
        }

        static PlantProbability Roll(EconomySnapshot snapshot, float random)
        {
            // Float-weight roll follows PlantSpawner rather than a rounded display percentage.
            float total = 0;
            foreach (var entry in snapshot.Input.Planter.spawnTable)
                total += EconomyCalculator.AdjustedWeight(entry, snapshot.Input.RareBonus);
            if (total <= 0) return null;
            float roll = random * total, cumulative = 0;
            for (int i = 0; i < snapshot.Plants.Count; i++)
            {
                float weight = EconomyCalculator.AdjustedWeight(snapshot.Input.Planter.spawnTable[i], snapshot.Input.RareBonus);
                if (weight <= 0) continue; // Disabled entries stay impossible, including random == 0.
                cumulative += weight;
                if (roll <= cumulative) return snapshot.Plants[i];
            }
            return snapshot.Plants.LastOrDefault(p => p.Probability > 0);
        }
    }
}
