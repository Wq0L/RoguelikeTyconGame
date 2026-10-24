using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MechanicsTestLab
{
    // Gerçek oyun durumunun okunması. Hiçbir üretim formülü burada yeniden yazılmaz: sonuç değerler oyunun çözülmüş değerleridir
    // (PlanterBrain.GetFinalStat, StatManager.GetFinalStat, PlantSpawner'ın gerçek aralığı, PlantHealth'in gerçek hasarı).
    // Kaynak dökümü, hesaba giren modifier listelerinin (taban, global, tile, rezonans) aynen gösterilmesidir. Oyunun tutmadığı
    // ayrıntı (ör. bir global modifier'ın ağaçtan mı karttan mı geldiği) uydurulmaz; "kaynağı kayıtlı değil" yazılır.
    public static class TestInspector
    {
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        public static string Plain(string text) => string.IsNullOrEmpty(text) ? "" : Tags.Replace(text, "");

        private static readonly DamageType[] Behaviors = { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang };
        private static readonly StatType[] BehaviorChances = { StatType.ExplosionChance, StatType.ElectricChance, StatType.TornadoChance, StatType.BoomerangChance };
        private static readonly string[] BehaviorNames = { "Patlama", "Elektrik", "Kasırga", "Bumerang" };

        // ---------------------------------------------------------------- hücre / saksı / bitki
        public static void DescribeCell(GridPosition position, StringBuilder text)
        {
            GroundCell ground = TestField.Ground(position);
            text.Append("HÜCRE (").Append(position.x).Append(", ").Append(position.z).Append(")");
            if (ground == null) { text.Append(" · tarlanın dışında\n"); return; }
            text.Append(ground.IsLocked ? " · KİLİTLİ\n" : " · açık\n");
            TileModifierSO tile = ground.CurrentModifier;
            if (tile == null) text.Append("Tile: yok\n");
            else
            {
                text.Append("Tile: ").Append(tile.name).Append(" · tür ").Append(tile.modifierType).Append(" · nadirlik ").Append(tile.rarity)
                    .Append(" · Sv ").Append(ground.Level).Append('/').Append(GroundCell.MaxLevel).Append('\n');
                foreach (StatModifier modifier in ground.RolledModifiers) text.Append("   · ").Append(Plain(TileBuffText.Modifier(modifier))).Append("  [").Append(modifier.operation).Append(' ').Append(modifier.value.ToString("0.####")).Append("]\n");
            }
            SegmentEventRuntime boss = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            if (boss != null && boss.Covers(position)) text.Append("Boss bölgesi: ").Append(boss.Data.displayName).Append(" (").Append(boss.RuleSummary).Append(")\n");
            if (FieldEventDirector.BlocksSpawn(position)) text.Append("Tarla olayı: bu hücrede üretim KAPALI\n");
            float slow = FieldEventDirector.SpawnIntervalMultiplier(position);
            if (!Mathf.Approximately(slow, 1f)) text.Append("Tarla olayı: üretim aralığı ×").Append(slow.ToString("0.##")).Append('\n');
            if (FieldEventDirector.BountyAt(position) != null) text.Append("Gezgin Bereket: bu hücrede kaynak ×2 · XP ×2 · üretim ×2 hızlı\n");

            PlanterBrain brain = TestField.PlanterAt(position);
            if (brain == null) { text.Append("\nSAKSI: yok\n"); return; }
            DescribePlanter(brain, text);
            DescribePlant(position, text);
        }

        public static void DescribePlanter(PlanterBrain brain, StringBuilder text)
        {
            PlanterSO data = brain.EditorData;
            text.Append("\nSAKSI: ").Append(data != null ? data.name : brain.name);
            if (data != null) text.Append(" · ").Append(data.sizeX).Append('×').Append(data.sizeZ);
            text.Append(" · köşe (").Append(brain.EditorOrigin.x).Append(", ").Append(brain.EditorOrigin.z).Append(") · ").Append(brain.EditorRotation).Append("°\n");
            text.Append("Kapladığı hücreler:");
            foreach (GridObject grid in brain.OccupiedGrids)
            {
                GridPosition cell = grid.GetGridPosition();
                text.Append(" (").Append(cell.x).Append(", ").Append(cell.z).Append(')');
            }
            text.Append("\nÜretim noktası: ").Append(brain.EditorSpawners.Count).Append('\n');
            text.Append("Altındaki tile sayıları:");
            if (brain.EditorTileCounts.Count == 0) text.Append(" yok");
            foreach (KeyValuePair<TileModifierType, int> pair in brain.EditorTileCounts) text.Append(' ').Append(pair.Key).Append('×').Append(pair.Value);
            text.Append('\n');
            DescribeResonance(brain, text);
            text.Append("Davranışlar (şans · tek tetiğin hesaplanmış hasarı):\n");
            int baseDamage = PlanterBrain.HarvestDamage;
            for (int i = 0; i < Behaviors.Length; i++)
            {
                float chance = brain.GetFinalStat(BehaviorChances[i]);
                text.Append("   ").Append(BehaviorNames[i]).Append(": ").Append((chance * 100f).ToString("0.#")).Append('%');
                if (chance > 0f)
                    text.Append(" · hasar ").Append(brain.GetBehaviorDamage(baseDamage, Behaviors[i])).Append(" (taban ").Append(baseDamage).Append(" × ")
                        .Append(brain.BehaviorDamageMultiplier(Behaviors[i]).ToString("0.###")).Append(')');
                text.Append('\n');
            }
            text.Append("   Çift ödül: ").Append((brain.GetFinalStat(StatType.DuplicateChance) * 100f).ToString("0.#")).Append("%\n");
            text.Append("Saksı hasar çarpanı (yalnız doğrudan vuruş): ×").Append(brain.GetFinalStat(StatType.PlanterDamageMultiplier).ToString("0.###")).Append('\n');
            text.Append("Üretim: çözülmüş aralık ").Append(brain.GetFinalStat(StatType.PlantSpawnRate).ToString("0.###")).Append(" sn · ham ")
                .Append(brain.GetRawSpawnInterval().ToString("0.###")).Append(" sn (taban ").Append(StatCalculator.MinimumSpawnInterval.ToString("0.##"))
                .Append(" sn) · tabandan taşan nadirlik +").Append(brain.GetSpawnOverflowRarity().ToString("0.##")).Append('\n');
            text.Append("Nadirlik bonusu: ").Append(brain.GetFinalStat(StatType.RareSpawnChance).ToString("0.##")).Append('\n');
        }

        // Etkin rezonanslar oyunun değerlendirmesinden (PlanterBrain.ActiveResonances); etkin olmayan kurallar için şartın neresinin
        // eksik olduğu kural verisi ve saksının tile sayılarından yazılır.
        public static void DescribeResonance(PlanterBrain brain, StringBuilder text)
        {
            text.Append("Rezonans:\n");
            ResonanceRulesSO rules = brain.Rules;
            if (rules == null || rules.rules == null) { text.Append("   kural verisi yok\n"); return; }
            IReadOnlyDictionary<TileModifierType, int> counts = brain.EditorTileCounts;
            bool anyBehavior = false;
            foreach (DamageType behavior in Behaviors)
                if (ResonanceManager.TryTile(behavior, out TileModifierType tile) && counts.TryGetValue(tile, out int n) && n > 0) anyBehavior = true;
            foreach (ResonanceRule rule in rules.rules)
            {
                if (rule == null || rule.tiers == null || rule.tiers.Count == 0) continue;
                counts.TryGetValue(rule.tileType, out int count);
                ActiveResonance active = null;
                foreach (ActiveResonance candidate in brain.ActiveResonances)
                    if (candidate.id == rule.id && candidate.tileType == rule.tileType && candidate.statType == rule.statType) active = candidate;
                int lowest = int.MaxValue;
                foreach (ResonanceTier tier in rule.tiers) if (tier != null && tier.requiredCount < lowest) lowest = tier.requiredCount;
                // Yalnız bu saksıyla ilgili kurallar: etkin olanlar ve gereken tile'dan en az biri bulunanlar.
                if (active == null && count == 0) continue;
                text.Append("   ").Append(active != null ? "[ETKİN] " : "[değil] ").Append(rule.resonanceName).Append(" · ").Append(rule.tileType).Append(' ').Append(count).Append('/')
                    .Append(active != null ? active.requiredCount : lowest);
                if (rule.requiresSecondary)
                {
                    counts.TryGetValue(rule.secondaryType, out int second);
                    text.Append(" · ek şart ").Append(rule.secondaryType).Append(' ').Append(second).Append('/').Append(rule.secondaryCount).Append(second >= rule.secondaryCount ? " ✓" : " ✗");
                }
                if (rule.requiresAnyBehavior) text.Append(" · davranış tile'ı gerekir ").Append(anyBehavior ? "✓" : "✗");
                if (active != null) text.Append(" → ").Append(Plain(TileBuffText.Resonance(active)).Replace("\n", " · "));
                text.Append('\n');
            }
            if (brain.ActiveResonances.Count == 0) text.Append("   etkin rezonans yok\n");
        }

        public static void DescribePlant(GridPosition position, StringBuilder text)
        {
            PlantSpawner spawner = TestField.SpawnerAt(position);
            text.Append("\nBİTKİ YUVASI (").Append(position.x).Append(", ").Append(position.z).Append("): ");
            if (spawner == null) { text.Append("bu hücrede üretim noktası yok\n"); return; }
            text.Append("oyunun kullandığı gerçek üretim aralığı ").Append(spawner.EditorInterval.ToString("0.###")).Append(" sn");
            PlantHealth plant = TestField.PlantAt(position);
            if (plant == null)
            {
                text.Append(" · boş · geçen ").Append(spawner.EditorTimer.ToString("0.##")).Append(" sn")
                    .Append(TestAttack.ProductionPaused ? " · ÜRETİM TESTTE DURAKLATILDI" : "").Append('\n');
                return;
            }
            PlantSO data = plant.Data;
            text.Append("\nBitki: ").Append(data != null ? data.name : "?").Append(" · nadirlik ").Append(data != null ? data.rarity.ToString() : "?")
                .Append(" · can ").Append(plant.CurrentHealth).Append('/').Append(plant.MaxHealth);
            if (!Mathf.Approximately(plant.HealthMultiplier, 1f)) text.Append(" · olay can çarpanı ×").Append(plant.HealthMultiplier.ToString("0.##"));
            text.Append(" · doğduğu round ").Append(plant.SpawnRound).Append('\n');
            if (data != null)
            {
                StatManager stats = StatManager.Instance;
                float damageStat = stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
                // Sapmasız (×1) doğrudan vuruş: oyunun aynı çarpımı (RunPower.DirectDamage) ve aynı saksı bonusu (GetIncomingDamage).
                int direct = NumericSafety.ToInt(RunPower.DirectDamage(damageStat, 1f, data.rarity, 1f), 1, NumericSite.DirectDamage);
                int applied = plant.GetIncomingDamage(direct);
                float critMultiplier = stats.GetFinalStat(StatType.CritMultiplier, StatTarget.Player);
                text.Append("Bu bitkiye doğrudan vuruş (sapmasız; gerçek vuruş ×0,85–1,15): ").Append(applied)
                    .Append(" · kritikte ").Append(plant.GetIncomingDamage(NumericSafety.ToInt(direct * critMultiplier, 0, NumericSite.CritDamage)))
                    .Append(" · öldürmek için ≈ ").Append(applied > 0 ? Mathf.CeilToInt(plant.CurrentHealth / (float)applied) : 0).Append(" vuruş\n");
                text.Append("Ödül: ").Append(data.rewardAmount).Append(' ').Append(data.resourceType).Append(" · ").Append(data.xpAmount).Append(" XP (çarpanlardan önce)\n");
            }
        }

        // ---------------------------------------------------------------- stat kaynağı
        // "Bu stat neden bu değer?" brain null: oyuncu kapsamı; dolu: o saksının kapsamı.
        public static void ExplainStat(StatType stat, StatTarget target, PlanterBrain brain, StringBuilder text)
        {
            StatManager stats = StatManager.Instance;
            if (stats == null) { text.Append("StatManager yok.\n"); return; }
            bool planterScope = brain != null;
            StatTarget context = planterScope ? StatTarget.Planter : target;
            float baseValue = planterScope ? brain.EditorData.GetBaseStat(stat) : stats.GetBaseStat(stat);
            text.Append(TileBuffText.Name(stat)).Append(" [").Append(stat).Append("] · kapsam: ")
                .Append(planterScope ? $"saksı ({brain.EditorOrigin.x}, {brain.EditorOrigin.z})" : context.ToString()).Append('\n');
            if (!planterScope && !stats.HasBaseStat(stat))
            {
                text.Append("Bu stat saksıya aittir: oyuncu / global bir sonucu yoktur. Bir saksı seçip onun kapsamında incele.\n");
                return;
            }
            text.Append("1) Taban: ").Append(baseValue.ToString("0.####"))
                .Append(planterScope ? (RunBalanceSO.TryPlanterBaseStat(stat, out _) ? " (profilin denge setinden)" : " (saksı verisinden / varsayılan)") : " (temel stat seti)").Append('\n');

            text.Append("2) Global modifier'lar (hedef All ya da ").Append(context).Append("):\n");
            var testPool = new List<StatModifier>(TestStats.Contributions);
            var bossPool = new List<StatModifier>(BossRewardManager.Instance != null ? BossRewardManager.Instance.OwnedModifiers : System.Array.Empty<StatModifier>());
            var loadoutPool = new List<StatModifier>(StartLoadoutManager.Instance != null ? StartLoadoutManager.Instance.OwnedModifiers : System.Array.Empty<StatModifier>());
            int globals = 0;
            foreach (StatModifier modifier in stats.GlobalModifiers)
            {
                if (modifier.statType != stat || (modifier.target != StatTarget.All && modifier.target != context)) continue;
                globals++;
                string source = Take(testPool, modifier) ? "TEST KATKISI" : Take(bossPool, modifier) ? "boss ödülü" : Take(loadoutPool, modifier) ? "başlangıç seçimi (çiftçi / tırpan)"
                    : "kaynağı kayıtlı değil (ağaç / temel güç kartı / geçici ödül etkisi)";
                text.Append("   · ").Append(Operation(modifier)).Append("  ← ").Append(source).Append('\n');
            }
            if (globals == 0) text.Append("   yok\n");

            if (planterScope)
            {
                text.Append("3) Tile modifier'ları (saksının altındaki hücreler):\n");
                int tiles = 0;
                foreach (GridObject grid in brain.OccupiedGrids)
                {
                    GroundCell ground = grid.GetGroundCellCached();
                    if (ground == null || ground.CurrentModifier == null) continue;
                    foreach (StatModifier modifier in ground.RolledModifiers)
                    {
                        if (modifier.statType != stat || (modifier.target != StatTarget.All && modifier.target != StatTarget.Planter)) continue;
                        tiles++;
                        GridPosition cell = grid.GetGridPosition();
                        text.Append("   · ").Append(Operation(modifier)).Append("  ← ").Append(ground.CurrentModifier.name).Append(" Sv ").Append(ground.Level)
                            .Append(" @ (").Append(cell.x).Append(", ").Append(cell.z).Append(")\n");
                    }
                }
                if (tiles == 0) text.Append("   yok\n");
                text.Append("   Rezonanstan önceki değer (oyunun hesabı): ").Append(brain.GetOrdinaryStat(stat).ToString("0.####")).Append('\n');
                text.Append("4) Rezonans modifier'ları (ayrı aşama; stat başına en güçlü koşulsuz etki):\n");
                int resonances = 0;
                foreach (StatModifier modifier in brain.EditorResonanceModifiers)
                {
                    if (modifier.statType != stat) continue;
                    resonances++;
                    text.Append("   · ").Append(Operation(modifier)).Append('\n');
                }
                if (resonances == 0) text.Append("   yok\n");
                if (stat == StatType.RareSpawnChance)
                    text.Append("   + üretim tabanından taşan nadirlik: ").Append(brain.GetSpawnOverflowRarity().ToString("0.##")).Append(" (ham aralık ")
                        .Append(brain.GetRawSpawnInterval().ToString("0.###")).Append(" sn)\n");
                float final = brain.GetFinalStat(stat);
                text.Append("SONUÇ (oyunun çözülmüş değeri): ").Append(final.ToString("0.####")).Append('\n');
                float without = StatCalculator.Calculate(baseValue, stat, StatTarget.Planter, TestStats.GlobalsWithoutTest(), brain.LocalModifiers);
                text.Append("Test katkısı olmadan, rezonanstan önce (aynı hesaplayıcı): ").Append(without.ToString("0.####"))
                    .Append(" · test katkısıyla: ").Append(brain.GetOrdinaryStat(stat).ToString("0.####")).Append('\n');
            }
            else
            {
                float final = stats.GetFinalStat(stat, context);
                text.Append("SONUÇ (oyunun çözülmüş değeri): ").Append(final.ToString("0.####")).Append('\n');
                text.Append("Test katkısı olmadan (aynı hesaplayıcı): ").Append(TestStats.WithoutTest(stat, context, baseValue).ToString("0.####")).Append('\n');
            }
            text.Append("Sınır: ").Append(ClampNote(stat)).Append('\n');
            if (stat == StatType.HarvestDamage) DescribeDirectChain(text);
            if (stat == StatType.AttackSpeed)
                text.Append("Oyundaki alt sınır ").Append(PlayerController.MinAttackInterval.ToString("0.##")).Append(" sn; şu anki gerçek saldırı aralığı ")
                    .Append(TestAttack.Interval.ToString("0.###")).Append(" sn (tempo dahil).\n");
        }

        // Doğrudan vuruşun stat dışındaki katsayıları (oyunun tek çarpımı: RunPower.DirectDamage) ve davranış katsayıları.
        private static void DescribeDirectChain(StringBuilder text)
        {
            text.Append("Doğrudan vuruşa ayrıca (stat değil, tek çarpımda bir kez): uzmanlaşma ×").Append(SpecializationManager.DirectMultiplier.ToString("0.###"))
                .Append(" · başlangıç seçimi ×").Append(StartLoadoutManager.DirectDamageMultiplier.ToString("0.###"))
                .Append(" · boss ödülü ×").Append(BossRewardManager.DirectDamageMultiplier.ToString("0.###"))
                .Append(" · Rare+ hedefte ×").Append(BossRewardManager.RareDirectMultiplier(PlantRarity.Rare).ToString("0.###")).Append('\n');
            text.Append("Davranış hasarı ayrı bir stat değildir: taban = bu stat; çarpan = rezonans × uzmanlaşma ×")
                .Append(SpecializationManager.BehaviorMultiplier.ToString("0.###")).Append(" × boss ödülü ×").Append(BossRewardManager.BehaviorDamageMultiplier.ToString("0.###"))
                .Append(" (saksıya göre değişir; saksı incelemesinde yazılır).\n");
        }

        private static bool Take(List<StatModifier> pool, StatModifier modifier)
        {
            int index = TestStats.IndexOf(pool, modifier);
            if (index < 0) return false;
            pool.RemoveAt(index);
            return true;
        }

        private static string Operation(StatModifier m) => m.operation switch
        {
            ModifierOperation.Flat => $"{(m.value >= 0 ? "+" : "")}{m.value:0.####} düz",
            ModifierOperation.AddPercent => $"{(m.value >= 0 ? "+" : "")}{m.value * 100f:0.##}% (toplanan yüzde)",
            ModifierOperation.MorePercent => $"×{1f + m.value:0.####} (ayrı çarpan)",
            _ => $"= {m.value:0.####} (tabanın yerine geçer)",
        };

        // Sınır metni oyunun sınır fonksiyonundan türetilir: uç değerler aynı fonksiyona sorulur.
        private static string ClampNote(StatType stat)
        {
            float low = StatCalculator.ClampStat(stat, float.MinValue), high = StatCalculator.ClampStat(stat, float.MaxValue);
            string lower = low > float.MinValue ? low.ToString("0.####") : "yok", upper = high < float.MaxValue ? high.ToString("0.####") : "yok";
            return $"alt {lower} · üst {upper} (StatCalculator.ClampStat)";
        }

        // ---------------------------------------------------------------- oturum geneli: etkiler, beklemeler, kapasiteler
        public static void DescribeRuntime(StringBuilder text)
        {
            RoundManager rounds = RoundManager.Instance;
            GameManager game = GameManager.Instance;
            if (rounds == null || game == null) { text.Append("Oyun yöneticileri yok.\n"); return; }
            text.Append("Round ").Append(rounds.CurrentRound).Append('/').Append(rounds.MaxRounds).Append(" · durum ").Append(game.CurrentState)
                .Append(rounds.IsRoundActive ? " · aktif" : " · round dışında").Append(" · kalan ").Append(rounds.RemainingTime.ToString("0.0")).Append(" sn")
                .Append(TestAttack.RoundTimerFrozen ? " (SAYAÇ DONDURULDU)" : "").Append(rounds.IsBossRound(rounds.CurrentRound) ? " · BOSS ROUND'U" : "").Append('\n');
            text.Append("Zaman ölçeği ×").Append(Time.timeScale.ToString("0.##")).Append(" · oyun zamanı ").Append(Time.time.ToString("0.00")).Append(" sn · kare ").Append(Time.frameCount).Append('\n');
            ProgressionManager progression = ProgressionManager.Instance;
            if (progression != null)
                text.Append("Level ").Append(progression.CurrentLevel).Append(" · XP ").Append(progression.StoredXP.ToString("0.#")).Append('/').Append(progression.XPToNextLevel.ToString("0.#"))
                    .Append(" · level başına seçim hakkı ").Append(rounds.ChoicesPerLevel).Append(" (temel ").Append(rounds.BaseChoicesPerLevel).Append(", ödül etkisi ")
                    .Append(BossRewardManager.LevelChoiceDelta >= 0 ? "+" : "").Append(BossRewardManager.LevelChoiceDelta).Append(") · bekleyen seçim ").Append(rounds.PendingCardSelections)
                    .Append(" · verilen hak ").Append(rounds.CardChoicesGranted).Append(" · alınan kart ").Append(rounds.CardsTaken).Append('\n');
            text.Append("Saldırı: ").Append(TestAttack.AutoAttack ? "otomatik" : "elle").Append(" · aralık ").Append(TestAttack.Interval.ToString("0.###")).Append(" sn · bekleme ")
                .Append(TestAttack.Cooldown.ToString("0.00")).Append(" sn · nişan ").Append(TestAttack.Aim.HasValue ? "sabit" : "fare").Append('\n');
            text.Append("Doğrudan saldırı duraklaması: ").Append(DirectAttackPause.Active ? $"AKTİF · kalan {DirectAttackPause.Remaining:0.00} sn" : "yok")
                .Append(" · istek ").Append(DirectAttackPause.Requests).Append(" · toplam ").Append(DirectAttackPause.PausedSeconds.ToString("0.00")).Append(" sn\n");
            HarvestRhythm rhythm = RunPower.Rhythm;
            if (rhythm.Enabled)
                text.Append("Hasat Ritmi: ").Append(rhythm.Ready ? "HAK HAZIR" : $"{rhythm.Count}/{rhythm.Threshold}").Append(" · hazırlanan ").Append(rhythm.Charges)
                    .Append(" · harcanan ").Append(rhythm.EmpoweredAttacks).Append('\n');
            TornadoManager tornado = TornadoManager.Instance;
            HarvestBehaviorManager behaviors = HarvestBehaviorManager.Instance;
            if (tornado != null) text.Append("Kasırga: aktif ").Append(tornado.ActiveCount).Append('/').Append(tornado.ActiveLimit).Append(" · reddedilen ").Append(tornado.RejectedTriggers).Append('\n');
            if (behaviors != null)
                text.Append("Bumerang: aktif ").Append(behaviors.ActiveBoomerangs).Append('/').Append(behaviors.BoomerangLimit).Append(" · reddedilen ").Append(behaviors.RejectedBoomerangTriggers)
                    .Append(" · elektrik: aktif görsel ").Append(behaviors.ActiveElectricBursts).Append(" · atlanan görsel ").Append(behaviors.SkippedElectricVisuals).Append(" (hasar etkilenmez)\n");
            // Şans zarları tek tetikle doğrulanmaz: oyunun sayaçları (şansı tutan tetik / başlatılamayan) run başından beri.
            text.Append("Davranış tetik sayaçları (şansı tutan · başlatılamayan):");
            for (int i = 0; i < Behaviors.Length; i++)
                text.Append(' ').Append(BehaviorNames[i]).Append(' ').Append(HarvestBehaviorStats.Triggered(Behaviors[i])).Append(" · ").Append(HarvestBehaviorStats.Skipped(Behaviors[i])).Append(';');
            text.Append('\n');
            text.Append("Sınır kaynağı: denge verisi + ödül bonusu ").Append(MovingBehaviorLimits.Bonus).Append(" (teknik tavan ").Append(MovingBehaviorLimits.SafetyCeiling).Append(")\n");
            HarvestChain chain = HarvestChain.Instance;
            if (chain != null && BossRewardManager.TryGetChain(out _))
                text.Append("Zincir: bekleyen iş ").Append(chain.Pending).Append(" · açık kök ").Append(chain.ActiveRoots).Append(" · en çok bekleyen ").Append(chain.MaxPending).Append('\n');
            BehaviorEchoes echoes = BehaviorEchoes.Instance;
            if (echoes != null && echoes.Pending > 0) text.Append("Artçı / ikinci dalga: bekleyen iş ").Append(echoes.Pending).Append('\n');
            text.Append('\n').Append(TestEvents.BossStatus()).Append("\n\n").Append(TestEvents.EventStatus()).Append('\n');

            RunEffects effects = RunEffects.Instance;
            text.Append("\nÖDÜL ETKİLERİ (oyunun kendi durum dökümü):\n");
            if (effects == null || effects.Active.Count == 0) text.Append("   aktif etki yok\n");
            else
            {
                text.Append("   etki saati ").Append(effects.Clock.ToString("0.00")).Append(" sn · bekleyen iş ").Append(effects.PendingTimers).Append('/').Append(RunEffects.TimerCapacity)
                    .Append(" · düşen iş ").Append(effects.DroppedTimers).Append('\n');
                foreach (RunEffect effect in effects.Active)
                {
                    text.Append("   — ").Append(effect.Data != null ? effect.Data.name : effect.GetType().Name).Append('\n');
                    var detail = new StringBuilder();
                    effect.Describe(detail);
                    foreach (string line in detail.ToString().Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) text.Append("     ").Append(line.TrimEnd()).Append('\n');
                }
            }
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss != null)
            {
                text.Append("\nALINAN ÖDÜLLER:");
                if (boss.Taken.Count == 0) text.Append(" yok");
                foreach (BossRewardSO reward in boss.Taken) text.Append("\n   ").Append(reward.displayName).Append(" ×").Append(boss.Stacks(reward));
                text.Append('\n');
            }
        }
    }
}
