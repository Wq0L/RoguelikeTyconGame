using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Senaryoyu canlı oturuma kurar ve canlı oturumu senaryoya yakalar. Kurulum, panelin kullandığı aynı test komutlarından geçer
    // (TestField / TestStats / TestRewards / TestEvents): senaryo için ayrı bir uygulama yolu yoktur.
    // Kurulum her zaman TEMİZ bir oturumda yapılır (TestSession sahneyi yeniden yükler): eski ödül, stat, davranış, olay ve efekt
    // taşınmaz. Sıra: round → grid → tarla temizliği → tile → saksı → bitki → level / kaynak → test statları → ödüller → boss.
    public static class ScenarioRunner
    {
        public static void Apply(Scenario scenario, List<Result> report)
        {
            RoundManager rounds = RoundManager.Instance;
            if (rounds == null) { report.Add(Result.Fail("RoundManager yok: senaryo kurulamadı.")); return; }

            // Round: 1. round oyunun normal başlangıcıyla, sonrakiler round atlama girişiyle (atlanan round'ların ödülü verilmez).
            int round = Mathf.Clamp(scenario.round, 1, rounds.MaxRounds);
            if (round <= 1) rounds.StartNextRound();
            else if (!rounds.EditorJumpToRound(round)) report.Add(Result.Fail($"R{round} başlatılamadı."));
            if (!rounds.IsRoundActive) { report.Add(Result.Fail("Round başlamadı (profil verisi geçersiz olabilir; Console'a bak).")); return; }

            Add(report, TestField.SetGridSize(scenario.gridSize), false);
            Add(report, TestField.ClearField(), false);

            foreach (ScenarioTile entry in scenario.tiles)
            {
                TileModifierSO tile = LabCatalog.Tile(entry.type, entry.rarity);
                if (tile == null) { report.Add(Result.Fail($"Tile bulunamadı: {entry.type} {entry.rarity}.")); continue; }
                Add(report, TestField.ApplyTile(new GridPosition(entry.x, entry.z), tile, entry.roll, entry.level, entry.values != null && entry.values.Count > 0 ? entry.values : null), false);
            }
            foreach (ScenarioPlanter entry in scenario.planters)
            {
                PlanterSO planter = LabCatalog.Planter(entry.planter);
                if (planter == null) { report.Add(Result.Fail($"Saksı bulunamadı: {entry.planter}.")); continue; }
                Add(report, TestField.PlacePlanter(planter, new GridPosition(entry.x, entry.z), entry.rotation, out _), false);
            }

            ApplyPlants(scenario, report);

            if (scenario.level > 1) Add(report, TestStats.SetLevel(scenario.level), false);
            foreach (ScenarioStat entry in scenario.stats) Add(report, TestStats.Add(entry.ToModifier()), false);

            // Ödüller: ön koşulu olmayan önce. Her biri oyunun alınabilirlik denetiminden geçer; reddedilen raporlanır.
            BossRewardManager boss = BossRewardManager.Instance;
            var ordered = new List<(ScenarioReward entry, BossRewardSO reward)>();
            foreach (ScenarioReward entry in scenario.rewards)
            {
                BossRewardSO reward = boss != null ? LabCatalog.Reward(boss.Pool, entry.id) : null;
                if (reward == null) report.Add(Result.Fail($"Ödül bu profilin havuzunda yok: {entry.id}."));
                else ordered.Add((entry, reward));
            }
            ordered.Sort((a, b) => LabCatalog.Depth(a.reward).CompareTo(LabCatalog.Depth(b.reward)));
            foreach ((ScenarioReward entry, BossRewardSO reward) in ordered)
                for (int i = 0; i < Mathf.Max(1, entry.stacks); i++)
                    Add(report, TestRewards.Grant(reward, entry.placement), true);

            if (scenario.boss == Scenario.NoBoss) Add(report, TestEvents.ClearBoss(), false);
            else if (!string.IsNullOrEmpty(scenario.boss))
            {
                SegmentEventSO bossData = LabCatalog.Boss(TestSession.Profile, scenario.boss);
                Add(report, bossData != null ? TestEvents.StartBoss(bossData, round) : Result.Fail($"Boss bu profilin havuzunda yok: {scenario.boss}."), true);
            }

            TestAttack.AutoAttack = scenario.autoAttack;
            TestAttack.ProductionPaused = scenario.pauseProduction;
            TestAttack.RoundTimerFrozen = scenario.freezeRoundTimer;
            if (scenario.aimFixed) TestAttack.AimAt(new GridPosition(scenario.aimX, scenario.aimZ));
            else TestAttack.UseMouse();
        }

        // Senaryonun bitki kurulumu (yerleşim, tile, stat ve ödüllere dokunmaz): tekrar denemek için ayrıca da çağrılır.
        public static void ApplyPlants(Scenario scenario, List<Result> report)
        {
            // Bitkiler: önce genel doldurma, sonra hücreye özel bitkiler.
            string fill = string.IsNullOrEmpty(scenario.plantFill) ? "roll" : scenario.plantFill;
            if (fill == "empty") TestField.ClearPlants();
            else
            {
                PlantSO all = fill == "roll" ? null : LabCatalog.Plant(fill);
                if (fill != "roll" && all == null) report.Add(Result.Fail($"Bitki bulunamadı: {fill} (normal bitki zarı kullanıldı)."));
                TestField.FillPlants(all, scenario.plantHealth);
            }
            foreach (ScenarioPlant entry in scenario.plants)
            {
                var cell = new GridPosition(entry.x, entry.z);
                PlantSO plant = string.IsNullOrEmpty(entry.plant) ? null : LabCatalog.Plant(entry.plant);
                if (!string.IsNullOrEmpty(entry.plant) && plant == null) { report.Add(Result.Fail($"Bitki bulunamadı: {entry.plant}.")); continue; }
                Result placed = TestField.SetPlant(cell, plant);
                Add(report, placed, false);
                if (placed.Ok && entry.health > 0) Add(report, TestField.SetPlantHealth(cell, entry.health), false);
            }
        }

        // Başarısız adımlar her zaman, başarılılar yalnız istenirse rapora girer (uzun kurulum listesi paneli doldurmasın).
        private static void Add(List<Result> report, Result result, bool always)
        {
            if (!result.Ok || always) report.Add(result);
        }

        // Canlı düzen → senaryo. Yalnız kurulum verisi: süren davranışlar, zincirler, sayaçlar ve bekleyen işler yakalanmaz.
        public static Scenario Capture(Scenario basis)
        {
            Scenario scenario = basis != null ? basis.Clone() : new Scenario();
            scenario.tiles.Clear(); scenario.planters.Clear(); scenario.plants.Clear(); scenario.stats.Clear(); scenario.rewards.Clear();
            RoundManager rounds = RoundManager.Instance;
            if (rounds != null) scenario.round = rounds.CurrentRound;
            scenario.gridSize = Mathf.Max(3, TestField.GridSize);
            scenario.level = ProgressionManager.Instance != null ? ProgressionManager.Instance.CurrentLevel : 0;
            ResourceManager resources = ResourceManager.Instance;
            if (resources != null)
            {
                scenario.gold = resources.GetResourceAmount(ResourceType.Gold);
                scenario.iron = resources.GetResourceAmount(ResourceType.Iron);
                scenario.stone = resources.GetResourceAmount(ResourceType.Stone);
            }
            scenario.freezeRoundTimer = TestAttack.RoundTimerFrozen;
            scenario.pauseProduction = TestAttack.ProductionPaused;
            scenario.autoAttack = TestAttack.AutoAttack;
            scenario.aimFixed = TestAttack.Aim.HasValue;
            if (TestAttack.Aim.HasValue && FieldCells.Grid != null)
            {
                GridPosition aim = FieldCells.Grid.GetGridPosition(TestAttack.Aim.Value);
                scenario.aimX = aim.x; scenario.aimZ = aim.z;
            }

            GridManager manager = GridManager.Instance;
            if (manager != null)
                for (int x = 0; x < manager.GetWidth(); x++)
                    for (int z = 0; z < manager.GetHeight(); z++)
                    {
                        GroundCell ground = TestField.Ground(new GridPosition(x, z));
                        if (ground == null || ground.IsLocked || ground.CurrentModifier == null) continue;
                        var entry = new ScenarioTile { x = x, z = z, type = ground.CurrentModifier.modifierType, rarity = ground.CurrentModifier.rarity, level = ground.Level };
                        // Seviye değerleri çarpar (GroundCell: 1 + 0,25 × seviye): kurulumda yeniden yükseltileceği için seviye 0 değerleri saklanır.
                        float factor = 1f + GroundCell.LevelBonus * ground.Level;
                        foreach (StatModifier modifier in ground.RolledModifiers) entry.values.Add(modifier.value / factor);
                        scenario.tiles.Add(entry);
                    }

            var planters = new List<PlanterBrain>();
            TestField.Planters(planters);
            foreach (PlanterBrain brain in planters)
            {
                if (brain.EditorData == null) continue;
                scenario.planters.Add(new ScenarioPlanter { planter = brain.EditorData.name, x = brain.EditorOrigin.x, z = brain.EditorOrigin.z, rotation = brain.EditorRotation });
                foreach (PlantSpawner spawner in brain.EditorSpawners)
                {
                    if (spawner == null || spawner.GridObject == null || spawner.SpawnedPlant == null) continue;
                    if (!spawner.SpawnedPlant.TryGetComponent(out PlantHealth health) || health.IsDead || health.Data == null) continue;
                    GridPosition cell = spawner.GridObject.GetGridPosition();
                    scenario.plants.Add(new ScenarioPlant { x = cell.x, z = cell.z, plant = health.Data.name, health = health.CurrentHealth < health.MaxHealth ? health.CurrentHealth : 0 });
                }
            }
            // Yakalanan düzende bitkiler hücre hücre yazılır; yazılmayan üretim noktası boş kalır.
            scenario.plantFill = "empty";
            scenario.plantHealth = 0;

            foreach (StatModifier modifier in TestStats.Contributions) scenario.stats.Add(ScenarioStat.From(modifier));
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss != null)
                foreach (BossRewardSO reward in boss.Taken)
                    scenario.rewards.Add(new ScenarioReward { id = reward.id, stacks = boss.Stacks(reward), placement = TestRewards.PlacementOf(reward.id) });
            // Boss round'unda: aktif boss adıyla, boss yoksa (bitmiş / temizlenmiş) "none" olarak yakalanır; diğer round'larda boş.
            SegmentEventRuntime active = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            if (active != null && active.Data != null && LabCatalog.Boss(TestSession.Profile, active.Data.name) != null) scenario.boss = active.Data.name;
            else scenario.boss = rounds != null && rounds.IsBossRound(rounds.CurrentRound) ? Scenario.NoBoss : "";
            return scenario;
        }
    }
}
