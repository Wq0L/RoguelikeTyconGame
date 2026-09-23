# Economy Analyzer

`Tools > Economy Analyzer` opens the Edit Mode tool. No scene singleton is instantiated, no gameplay state is modified, and no gameplay asset is saved. Settings are a transient profile copy. The explicit **Save analysis profile** button only creates a new analysis asset; it refuses to overwrite existing assets.

## Usage

1. Select a PlanterSO, CoreStatsSO, health curve, current XP curve, resonance rules and skill roster. Defaults read this project's existing assets; verify these against your intended scene/build. The roster comes from FinalSkillTree, not an inferred max stat table.
2. Set seed, trials and assumed FPS. One trial is a deterministic seeded run; multiple trials give a Monte Carlo distribution. RNG is private and does not consume UnityEngine.Random.
3. Configure History. It must start at round 1. Each explicit row remains active until the next row. Use Base, Custom (node and owned tier), or MaxSkillTree. No purchases/progression are invented between rows. Add exact global modifiers or explicit duration/EffectiveTargets assumptions as needed.
4. For tile tests enter each occupied local cell once, reference its actual TileModifierSO, and enter the existing rolledModifiers. An empty roll list contributes no ordinary stat effects. No tile rerolls occur during analysis. Rezonance OFF still keeps all ordinary tile effects.
5. Run analysis. One paired full-history trial executes per Editor update. Cancel preserves completed trials. Display any round or inspect default checkpoints. Inputs and output snapshots are separate; rerun after changing inputs/assets.

To reproduce the R129 question, use one history row starting at R1 with MaxSkillTree, durationOverride=90, EffectiveTargets=6 and no tiles. This explicitly tests a full-max build throughout the history; it is not a claimed progression path. For realistic XP progression provide actual early/mid/late build rows.

## Exact reuse and mirrored operations

- Existing enums, PlantSO, PlanterSO, SkillNodeSO and modifier structures are reused.
- StatCalculator performs both ordinary and post-calculation resonance stats; ResonanceManager.Evaluate selects actual thresholds. PlanterSO supplies planter bases; CoreStatsSO supplies player bases.
- Max/custom builds contribute only the owned tier of each node. Total purchase cost still includes all purchased tiers. Duplicate roster entries do not double the stats.
- Actual prefab spawnPoints are read with SerializedObject without instantiating it. Empty lists use footprint cell count, matching automatic PlanterBrain initialization. A positive override is explicitly an analysis assumption. Valid placement of distinct points onto distinct world grid cells is still a gameplay responsibility.
- Rare weights mirror PlantSpawner and normalize after adjustment. Disabled entries remain impossible, including a zero RNG sample.
- HP uses PlantHealthScalingSO.Calculate at spawn; a living plant carries its current HP across rounds.
- Resource and XP multiplication use float operations and Mathf.RoundToInt **per plant before duplication**. Duplicate doubles the rounded integer reward/XP, not the physical death count. Nonpositive resource deliveries are ignored as in ResourceManager.
- Attack interval includes PlayerController's 0.1 second floor. Damage variance, crit rounding, and incoming planter multiplier rounding follow gameplay order.
- Each lane has an initial random timer. While occupied it cannot produce or advance its production timer. Death clears the lane and resets the timer. Attack and spawn timers discard overshoot, like Update. Living plants, spawn timers and attack timer persist between rounds; shops do not advance time.

## Explicit approximations and scope

**Analytical V0 Estimate** uses weighted `max(1, HP / expected damage)` combat cost and `min(production ceiling, combat capacity)`. Fractional expected hits do not reproduce discrete attacks/variance. It is a quick estimate, never “Actual Harvest”. **Instant-Harvest Production Ceiling** is a steady-state production rate; first-frame/end-frame phase and plants carried over from earlier rounds can cause a finite round to differ slightly, so this is not a strict finite-window bound.

**Simulated Result** is a seeded fixed-frame lifecycle model. Each assumed frame updates spawners before the player; the scene's unspecified script execution order may differ by a frame. FPS is an explicit input. EffectiveTargets is a coverage assumption, clamped to available lanes in analysis. Simulation rotates through occupied lanes up to this coverage. This is not an exact replay of the mouse path, grid geometry or the game's all-targets-in-radius query. The reported AreaRadius does not automatically alter EffectiveTargets.

Scope is one fixed planter layout. Expansion/removal of planters, explosions between planters, tornado paths/caps, score, card skipping income, post-121 stat cards and automatic purchases are not simulated. Explosion/tornado builds show a visible partial-model warning. The tool should not be used to certify full-map endgame throughput. Skill prerequisites/unlocks are not auto-purchased: custom profiles describe owned tiers, even if not normally reachable.

Separate random streams per lane and purpose provide repeatable paired A/B tests; different lifecycle timing can still shift which spawn/hit events occur. It does not reproduce Unity's random generator or an in-game seed. P10/P50/P90 use linearly interpolated empirical quantiles; a single trial has identical quantiles and is not a confidence interval. Currency accumulators use double for analysis, so runtime wallet int overflow is not reproduced.

## XP, targets and investment diagnostics

Every trial accumulates delivered XP round by round. Current reads the live ProgressionSO, including its authored 120-level cost table and constant final-cost continuation when enabled (see EconomyBalance.md). Current and proposed `35 + 4L + .25L²` curves evaluate that same history independently. No checkpoint income is applied retroactively. Level distributions are calculated per trial. Next-level wait and suggested XP production multipliers use the current rate as an explicitly labelled forecast, not a history rewrite. The mean-XP representative's level is distinct from the mean of trial levels.

Target references: R20=20–30, R40=40–50, R65=65–75, R100=95–105, R115–130≈121. No level targets are invented between these checkpoints. The proposed post-121 stat-card system does not exist in the inspected gameplay code and is not implemented here. Design phases remain fast R1–20, slower R20–65, acceleration R65–100, powerful R100–120, intentional god mode R120–130.

Combat/spawn diagnostic bands are <.75, .75–.90, .90–1.10, 1.10–1.50 and >1.50. No supply produces N/A rather than a division error. Purchase wait ignores zero-cost currencies and reports unreachable for a positive cost with zero income. It describes gross rounds of income, not a wallet-aware buying schedule. Default resource weights 1/7/14 are comparison weights, never exchange rates. Price ranges are displayed only, never written to skills.

The BASE/MAX comparison displays actual final stats and analytical economy. ROI compares the summed real cost of all roster tiers with the weighted income delta; nonpositive delta is N/A. Selected skill pricing also compares tier N−1 to N while holding other owned nodes and tiles fixed. It includes duration changes and is not a measure of resonance/grid strategic value. Comparison of another planter is explicitly BASE analytical, not a shared-combat whole-map sum. A/B toggles all resonance contributions together on an otherwise unchanged profile; ≥2× income/XP gains are review flags, not nerf recommendations. The per-family analytical table isolates each family's resonance contribution while preserving every ordinary tile roll; combined gains exceeding the product of individual gains by 25% are flagged for review. This configurable-build analysis does not enumerate impossible tile combinations or perform an exhaustive combination search.

## Verification

`Tools > Economy Analyzer Verification` runs synthetic regression checks and validates the real 2×3/max-tree assets using eight seeded full histories. Output: `Logs/EconomyAnalyzerVerification.txt` (kept outside Unity's auto-cleared Temp directory). Batch entry: `ClickerGame.EconomyAnalysis.EconomyAnalyzerVerification.RunBatch`. Verification creates only transient ScriptableObjects and a log report, without modifying gameplay data.
