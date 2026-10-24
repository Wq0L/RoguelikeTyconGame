using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MechanicsTestLab
{
    // Stat KAYNAKLARININ kontrolleri: tile, rezonans, kart, skill ağacı, başlangıç seçimi, uzmanlaşma.
    // Her kaynak için aynı üç soru sorulur:
    //   1) Oyunun oyuncuya YAZDIĞI değer (tooltip / kart metni) ile veride duran ve uygulanan değer aynı mı?
    //   2) Değer doğru HEDEFE uygulanıyor mu (o saksı / oyuncu) ve başka hedefe sızmıyor mu?
    //   3) Oyun o değeri GERÇEKTEN kullanıyor mu (gerçek vuruş, gerçek hasat, gerçek üretim süresi, gerçek tetik oranı)?
    // Beklenen değerler burada, açık aritmetikle üretilir (Arith): ham taban + modifier listesi + veri.
    public static partial class NumericChecks
    {
        private static readonly TileRarity[] Rarities = { TileRarity.Common, TileRarity.Rare, TileRarity.Epic, TileRarity.Legendary };
        private const int Samples = 240;

        private static void SourceChecks(List<CheckDefinition> list)
        {
            const string tiles = "Tile'lar", resonance = "Rezonanslar", cards = "Kartlar", skills = "Skill ağacı", loadout = "Başlangıç seçimi";
            const string two = "iki eş saksı: biri tile'ın üstünde, diğeri tile'sız; hasat hasarı test katkısıyla 1000";
            list.Add(Def("T00", tiles, "Tile verisi: ad, nadirlik alanı ve değer aralıkları tutarlı mı", "oyunun 40 tile asset'i", "veri okunur", TileData));
            (TileModifierType type, string title, string use)[] types =
            {
                (TileModifierType.Explosive, "Patlama tile'ı: şans", "gerçek tetik oranı"), (TileModifierType.Electric, "Elektrik tile'ı: şans", "gerçek tetik oranı"),
                (TileModifierType.Tornado, "Kasırga tile'ı: şans", "gerçek tetik oranı"), (TileModifierType.Boomerang, "Bumerang tile'ı: şans", "gerçek tetik oranı"),
                (TileModifierType.Duplicate, "Çift ödül tile'ı: şans", "gerçek çift ödül oranı"), (TileModifierType.Damage, "Hasar tile'ı: saksı hasar çarpanı", "gerçek doğrudan vuruş"),
                (TileModifierType.Crystal, "Kristal tile'ı: nadirlik bonusu", "gerçek bitki zarı dağılımı"), (TileModifierType.Energy, "Enerji tile'ı: Harvest Score", "gerçek hasat skoru"),
                (TileModifierType.Fertile, "Verimli tile: üretim süresi", "ölçülen gerçek doğuş süresi"), (TileModifierType.Water, "Su tile'ı: XP kazancı", "gerçek hasat XP'si"),
            };
            for (int i = 0; i < types.Length; i++)
            {
                TileModifierType type = types[i].type;
                list.Add(Def($"T{i + 1:00}", tiles, $"{types[i].title} — yazan = uygulanan = {types[i].use}", two, "dört nadirlik, en yüksek zar; sonra seviye 3; sonra tile'ı kaldır", c => TileType(c, type)));
            }

            ResonanceRulesSO rules = ResonanceManager.DefaultRules;
            for (int i = 0; rules != null && rules.rules != null && i < rules.rules.Count; i++)
            {
                ResonanceRule rule = rules.rules[i];
                if (rule == null) continue;
                list.Add(Def($"R{i + 1:00}", resonance, $"{rule.resonanceName}: kademeler, yazan etki, uygulanan etki, gerçek kullanım", "tek 2 × 3 saksı; altına gereken tile'lar sırayla konur",
                    "tile sayısını artır, her kademede saksıyı ve gerçek vuruş / hasadı ölç", c => Resonance(c, rule)));
            }

            list.Add(Def("C01", cards, "Tile kartı: kartta yazan değer tarlaya aynen gelir", "boş açık hücreler", "oyunun kart uygulama yoluyla üç tile kartı", TileCard));
            list.Add(Def("C02", cards, "Yükseltme kartı: seviye kazancı ve değer çarpanı", "seviye 0 bir tile", "Common / Epic / Legendary yükseltme kartı", UpgradeCard));
            list.Add(Def("C03", cards, "Temel güç kartları: beş kart × dört nadirlik, toplanan ve çarpılan gruplar", "temiz düzen", "oyunun temel güç kartı yoluyla ikişer kart", BasePowerCards));
            list.Add(Def("SK01", skills, "Skill ağacı: her satın alımda bedel ve stat etkisi", "bol kaynak", "satın alınabilen her düğüm, her seviye, oyunun satın alma yoluyla", SkillTree));
            list.Add(Def("L01", loadout, "Çiftçi ve tırpan: stat, doğrudan vuruş, kaynak ve skor çarpanı", "her seçenek için temiz oturum", "varsayılan seçimle ölç, sonra her seçenekle yeniden kur ve ölç", Loadout));
            list.Add(Def("U01", loadout, "Uzmanlaşma", "", "", Specialization, CheckCoverage.Observed, "Bu profilde uzmanlaşma kapalıysa yalnız çarpanların nötr olduğu doğrulanır."));
            list.Add(Def("M05", "Henüz olmayan sistemler", "Okunmayan statlar: StartingGoldBonus, StartingPlanterCount, ExtraCardChoice, RerollCard", "", "", null, CheckCoverage.Missing,
                "sistem henüz yok: bu dört stat temel sette tanımlı ama hiçbir oyun kodu okumuyor; hiçbir içerik de vermiyor (kaynak kodu taramasıyla bulundu)."));
        }

        // ---------------------------------------------------------------- ortak yardımcılar
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Oyunun yazdığı metindeki ilk işaretli sayı ("+90% puan" → 90, "−15%" → −15, "×1.5" → 1.5).
        private static bool Declared(string text, out double number)
        {
            number = 0d;
            if (string.IsNullOrEmpty(text)) return false;
            int i = 0;
            while (i < text.Length && !char.IsDigit(text[i])) i++;
            if (i >= text.Length) return false;
            bool negative = i > 0 && (text[i - 1] == '−' || text[i - 1] == '-');
            int start = i;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
            if (!double.TryParse(text.Substring(start, i - start), NumberStyles.Float, Inv, out number)) return false;
            if (negative) number = -number;
            return true;
        }

        // Modifier değerinin oyunun yazım birimindeki karşılığı (yüzde ve şans ×100; nadirlik puanı olduğu gibi).
        private static double DisplayUnits(StatModifier mod)
        {
            bool percent = mod.operation == ModifierOperation.AddPercent || mod.operation == ModifierOperation.MorePercent;
            bool chance = Snapshots.UnitOf(mod.statType) == ValueUnit.Chance && mod.statType != StatType.MutationLuck;
            return percent || chance ? mod.value * 100d : mod.value;
        }

        private static void LocalModifiers(PlanterBrain brain, List<StatModifier> into)
        {
            into.Clear();
            foreach (GridObject grid in brain.OccupiedGrids)
            {
                GroundCell ground = TestField.Ground(grid.GetGridPosition());
                if (ground != null && ground.CurrentModifier != null) into.AddRange(ground.RolledModifiers);
            }
        }

        // Saksının rezonanstan ÖNCEKİ değeri: ham taban + global katkılar + altındaki tile'ların gerçek zar değerleri, burada yazılı kuralla.
        private static double Ordinary(PlanterBrain brain, StatType stat, bool includeAllTarget = true)
        {
            var locals = new List<StatModifier>();
            LocalModifiers(brain, locals);
            return Arith.Resolve(brain.EditorData.GetBaseStat(stat), stat, StatTarget.Planter, Stats.GlobalModifiers, locals, includeAllTarget);
        }

        private static double Tolerance(double value) => 1e-4 * Math.Max(1d, Math.Abs(value));

        // ---------------------------------------------------------------- T00: tile verisi
        private static IEnumerator TileData(CheckContext c)
        {
            IReadOnlyList<TileModifierSO> all = LabCatalog.Tiles;
            c.Exact("tile asset sayısı (10 tür × 4 nadirlik)", 40, all.Count);
            var wrongName = new List<string>();
            var badRange = new List<string>();
            var seen = new Dictionary<string, string>();
            var duplicates = new List<string>();
            foreach (TileModifierSO tile in all)
            {
                string suffix = tile.name.Contains("-") ? tile.name.Substring(tile.name.LastIndexOf('-') + 1) : "";
                if (!string.Equals(suffix, tile.rarity.ToString(), StringComparison.OrdinalIgnoreCase)) wrongName.Add($"{tile.name} → nadirlik alanı {tile.rarity}");
                string key = tile.modifierType + "/" + tile.rarity;
                if (seen.TryGetValue(key, out string other)) duplicates.Add($"{key}: {other} ve {tile.name}"); else seen[key] = tile.name;
                foreach (StatModifierRange range in tile.modifierRanges)
                    if (Math.Abs(range.maxValue) < Math.Abs(range.minValue) || Math.Sign(range.maxValue) != Math.Sign(range.minValue)) badRange.Add($"{tile.name}: {range.minValue} … {range.maxValue}");
            }
            c.True("dosya adındaki nadirlik = asset'in nadirlik alanı", "uyumsuz yok", wrongName.Count == 0 ? "uyumsuz yok" : string.Join("; ", wrongName), wrongName.Count == 0);
            c.True("her tür + nadirlik için tek asset", "çift yok", duplicates.Count == 0 ? "çift yok" : string.Join("; ", duplicates), duplicates.Count == 0);
            c.True("değer aralıkları: en büyük zar en küçükten güçlü, işaret aynı", "bozuk aralık yok", badRange.Count == 0 ? "bozuk aralık yok" : string.Join("; ", badRange), badRange.Count == 0);
            // Nadirlik arttıkça güç artmalı: bir üst nadirliğin en zayıf zarı, alt nadirliğin en güçlü zarından zayıf olmamalı.
            var overlaps = new List<string>();
            foreach (TileModifierType type in Enum.GetValues(typeof(TileModifierType)))
                for (int r = 0; r + 1 < Rarities.Length; r++)
                {
                    TileModifierSO low = LabCatalog.Tile(type, Rarities[r]), high = LabCatalog.Tile(type, Rarities[r + 1]);
                    if (low == null || high == null || low.modifierRanges.Count == 0 || high.modifierRanges.Count == 0) continue;
                    if (Math.Abs(high.modifierRanges[0].minValue) < Math.Abs(low.modifierRanges[0].maxValue) - 1e-6)
                        overlaps.Add($"{type}: {Rarities[r + 1]} en zayıf {high.modifierRanges[0].minValue} < {Rarities[r]} en güçlü {low.modifierRanges[0].maxValue}");
                }
            c.True("nadirlikler arası aralıklar üst üste binmiyor", "binme yok", overlaps.Count == 0 ? "binme yok" : string.Join("; ", overlaps), overlaps.Count == 0);
            yield break;
        }

        // ---------------------------------------------------------------- T01–T10: tile türü
        private static IEnumerator TileType(CheckContext c, TileModifierType type)
        {
            Scenario s = Clean("tile " + type); BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            if (c.Aborted) yield break;
            var cellA = new GridPosition(C, C);
            PlanterBrain a = CheckContext.Planter(C, C), b = CheckContext.Planter(C + 1, C);
            GroundCell ground = TestField.Ground(cellA);
            if (a == null || b == null || ground == null) { c.Skip("ön koşul", "saksılar kurulamadı"); yield break; }
            TileModifierSO last = null;
            StatType stat = default;
            double plain = 0d;
            foreach (TileRarity rarity in Rarities)
            {
                TileModifierSO tile = LabCatalog.Tile(type, rarity);
                if (tile == null) { c.True($"{rarity}: asset", "var", "bu tür + nadirlik için asset bulunamadı (nadirlik alanı yanlış olabilir: T00)", false); continue; }
                if (tile.modifierRanges.Count == 0) { c.True($"{rarity}: stat etkisi", "en az bir", "asset'te stat etkisi yok", false); continue; }
                stat = tile.modifierRanges[0].statType;
                plain = b.GetFinalStat(stat);
                Result applied = TestField.ApplyTile(cellA, tile, 1f, 0);
                if (!applied.Ok) { c.True($"{rarity}: tile uygulandı", "uygulandı", applied.Message, false); continue; }
                last = tile;
                StatModifier mod = ground.RolledModifiers[0];
                StatModifierRange range = tile.modifierRanges[0];
                c.Near($"{rarity}: tile değeri = asset'in en yüksek zarı", range.maxValue, mod.value, Tolerance(range.maxValue), "float");
                string text = TileBuffText.Amount(mod);
                bool parsed = Declared(text, out double declared);
                c.Near($"{rarity}: oyunun yazdığı \"{TileBuffText.Modifier(mod)}\" = uygulanan değer", DisplayUnits(mod), parsed ? declared : double.NaN, 0.006, "metin iki ondalığa yuvarlanır");
                double expected = Arith.Clamp(stat, Arith.Apply(plain, mod));
                c.Near($"{rarity}: tile'lı saksının {stat} değeri (tile'sız eş saksı {plain:0.####})", expected, a.GetFinalStat(stat), Tolerance(expected), "float");
                c.Near($"{rarity}: komşu saksıya sızmadı", plain, b.GetFinalStat(stat), 1e-6, "float");
                yield return TileUse(c, type, rarity, stat, a, b, expected, plain);
                if (c.Aborted) yield break;
            }
            if (last == null) yield break;
            // Seviye: her seviye tile'ın zar değerine +%25 (oyunun kuralı: GroundCell.LevelBonus).
            TestField.ApplyTile(cellA, last, 1f, GroundCell.MaxLevel);
            StatModifier leveled = ground.RolledModifiers[0];
            double levelValue = last.modifierRanges[0].maxValue * (1d + GroundCell.LevelBonus * GroundCell.MaxLevel);
            c.Exact("seviye 3'e çıktı", GroundCell.MaxLevel, ground.Level);
            c.Near($"seviye {GroundCell.MaxLevel}: tile değeri = en yüksek zar × (1 + {GroundCell.LevelBonus:0.##} × {GroundCell.MaxLevel})", levelValue, leveled.value, Tolerance(levelValue), "float");
            Declared(TileBuffText.Amount(leveled), out double leveledText);
            c.Near($"seviye {GroundCell.MaxLevel}: oyunun yazdığı \"{TileBuffText.Modifier(leveled)}\" = uygulanan değer", DisplayUnits(leveled), leveledText, 0.006, "metin iki ondalığa yuvarlanır");
            double leveledExpected = Arith.Clamp(stat, Arith.Apply(plain, leveled));
            c.Near($"seviye {GroundCell.MaxLevel}: saksının {stat} değeri", leveledExpected, a.GetFinalStat(stat), Tolerance(leveledExpected), "float");
            TestField.ClearTile(cellA);
            c.Near("tile kalkınca saksı tile'sız değerine döndü", plain, a.GetFinalStat(stat), 1e-6, "float");
            // Yazan değer uygulanabilen sınırı aşıyor mu: tek tile, en yüksek zar, her nadirlik ve seviye (veriden, açık aritmetikle).
            double cap = Arith.Clamp(stat, double.MaxValue);
            if (cap < 1e30)
            {
                var over = new List<string>();
                foreach (TileRarity rarity in Rarities)
                {
                    TileModifierSO tile = LabCatalog.Tile(type, rarity);
                    if (tile == null || tile.modifierRanges.Count == 0 || tile.modifierRanges[0].operation != ModifierOperation.Flat) continue;
                    var levels = new List<string>();
                    for (int level = 0; level <= GroundCell.MaxLevel; level++)
                    {
                        double value = tile.modifierRanges[0].maxValue * (1d + GroundCell.LevelBonus * level);
                        if (plain + value > cap + 1e-6) levels.Add($"Sv {level}: yazan {DisplayUnits(new StatModifier { statType = stat, operation = ModifierOperation.Flat, value = (float)value }):0.#}");
                    }
                    if (levels.Count > 0) over.Add($"{rarity} ({string.Join(", ", levels)})");
                }
                bool chanceStat = Snapshots.UnitOf(stat) == ValueUnit.Chance;
                if (over.Count > 0)
                    c.Warn("yazan değer uygulanabilen sınırı aşıyor (tek tile, en yüksek zar)", $"yazan değerin tamamı kullanılır (sınır {(chanceStat ? cap * 100d : cap):0.#}{(chanceStat ? "%" : " puan")})",
                        string.Join(" · ", over) + " — sınırın üstü hiçbir şeye dönüşmüyor");
            }
        }

        // Gerçek kullanım: oyun o değeri gerçekten kullanıyor mu.
        private static IEnumerator TileUse(CheckContext c, TileModifierType type, TileRarity rarity, StatType stat, PlanterBrain a, PlanterBrain b, double expected, double plain)
        {
            int seed = 500 + (int)type * 10 + (int)rarity;
            switch (type)
            {
                case TileModifierType.Explosive: yield return BehaviorRate(c, rarity, DamageType.Explosion, expected, seed); break;
                case TileModifierType.Electric: yield return BehaviorRate(c, rarity, DamageType.Electric, expected, seed); break;
                case TileModifierType.Tornado: yield return BehaviorRate(c, rarity, DamageType.Tornado, expected, seed); break;
                case TileModifierType.Boomerang: yield return BehaviorRate(c, rarity, DamageType.Boomerang, expected, seed); break;
                case TileModifierType.Duplicate: yield return DuplicateRate(c, rarity, expected, seed); break;
                case TileModifierType.Crystal: yield return RarityRoll(c, rarity, expected, seed); break;
                case TileModifierType.Damage:
                {
                    Refill();
                    yield return c.Attack(C, C, seed);
                    TraceHit hit = c.Direct(C, C);
                    if (hit == null) { if (!c.Aborted) c.Unseen($"{rarity}: gerçek vuruş", "vuruş kaydı yok"); yield break; }
                    c.Near($"{rarity}: gerçek doğrudan vuruş = yuvarla(yuvarlanan {hit.DamageBeforeCrit} × saksı çarpanı {expected:0.####})", Arith.Round(hit.DamageBeforeCrit * expected), hit.Computed, 1d, Rounding);
                    break;
                }
                case TileModifierType.Energy:
                case TileModifierType.Water:
                {
                    // Önce tile'sız saksıda bir hasat (taban), sonra tile'lı saksıda: aynı bitki, aynı seed.
                    Refill();
                    yield return c.Attack(C + 1, C, seed);
                    if (c.Aborted) yield break;
                    if (c.Trace.Harvests.Count != 1 || !c.Trace.Harvests[0].HasReward) { c.Unseen($"{rarity}: taban hasat", "tile'sız saksıda tek hasat kaydı oluşmadı"); yield break; }
                    TraceHarvest basis = c.Trace.Harvests[0];
                    yield return c.Attack(C, C, seed);
                    if (c.Aborted) yield break;
                    if (c.Trace.Harvests.Count != 1 || !c.Trace.Harvests[0].HasReward) { c.Unseen($"{rarity}: hasat", "tile'lı saksıda tek hasat kaydı oluşmadı"); yield break; }
                    TraceHarvest harvest = c.Trace.Harvests[0];
                    if (type == TileModifierType.Water)
                    {
                        c.Near($"{rarity}: gerçek hasadın kullandığı XP çarpanı", expected, harvest.XpMultiplier, Tolerance(expected), "float");
                        double xp = Math.Round(harvest.BaseXp * expected * harvest.RarityXpMultiplier, MidpointRounding.ToEven);
                        c.Near($"{rarity}: gerçek hasat XP'si = yuvarla(taban {harvest.BaseXp} × {expected:0.####}) (tile'sız saksıda {basis.XpFinal:0.##})", xp, harvest.XpFinal, 1d, Rounding);
                    }
                    else
                    {
                        c.Near($"{rarity}: gerçek hasadın kullandığı saksı skor çarpanı", expected, harvest.ScorePlanterMultiplier, Tolerance(expected), "float");
                        double unit = basis.ScorePlanterMultiplier > 0f ? basis.ScoreAward / (double)basis.ScorePlanterMultiplier : 0d;
                        c.Near($"{rarity}: gerçek hasat skoru = yuvarla(tile'sız skor {basis.ScoreAward} × {expected / plain:0.####})", Arith.Round(unit * expected), harvest.ScoreAward, 1d, Rounding);
                    }
                    break;
                }
                case TileModifierType.Fertile:
                {
                    PlantSpawner fast = CheckContext.Spawner(C, C), slow = CheckContext.Spawner(C + 1, C);
                    if (fast == null || slow == null) { c.Unseen($"{rarity}: üretim", "üretim noktası yok"); yield break; }
                    double interval = slow.EditorInterval * (expected / plain);
                    c.Near($"{rarity}: yuvanın gerçek üretim aralığı = tile'sız {slow.EditorInterval:0.###} sn × {expected / plain:0.####}", interval, fast.EditorInterval, 1e-3, "float");
                    // Ölçüm: yuva boşaltılır, üretim açılır, bitkinin gerçekten doğduğu ana kadar geçen oyun süresi okunur.
                    TestAttack.ProductionPaused = false;
                    fast.EditorClearPlant();
                    double started = Time.timeAsDouble;
                    bool timedOut = false;
                    yield return LabRoutine.Until(() => fast.SpawnedPlant != null, 120d, () => timedOut = true);
                    double measured = Time.timeAsDouble - started;
                    TestAttack.ProductionPaused = true;
                    if (timedOut) { c.Unseen($"{rarity}: ölçülen doğuş süresi", "bitki doğmadı"); yield break; }
                    c.Near($"{rarity}: ÖLÇÜLEN doğuş süresi (boş yuvadan bitkiye)", interval, measured, 2.5 * Dt, "kare süresi");
                    break;
                }
            }
        }

        // Davranış şansı: oyunun gerçek tetik yolundan örnekleme. Tolerans istatistikseldir (±4 standart sapma).
        private static IEnumerator BehaviorRate(CheckContext c, TileRarity rarity, DamageType type, double chance, int seed)
        {
            var cell = new GridPosition(C, C);
            PlantSO plant = LabCatalog.Plant(ScenarioPresets.CommonPlant());
            Vector3 world = TestAttack.WorldOf(cell);
            int opportunities = 0, rolled = 0, rejected = 0;
            UnityEngine.Random.InitState(seed);
            for (int i = 0; i < Samples; i++)
            {
                TestField.SetPlant(cell, plant);
                PlantHealth target = TestField.PlantAt(cell);
                if (target == null) continue;
                target.EditorSetHealth(1);
                int before = HarvestBehaviorStats.Triggered(type), skipped = HarvestBehaviorStats.Skipped(type);
                TestAttack.ResetCooldown();
                if (TestAttack.AttackOnce(world).Ok && target.IsDead && target.KilledBy == DamageType.Direct)
                {
                    opportunities++;
                    rolled += HarvestBehaviorStats.Triggered(type) - before;
                    rejected += HarvestBehaviorStats.Skipped(type) - skipped;
                }
                yield return LabRoutine.Frames(1);
            }
            if (opportunities < Samples / 2) { c.Unseen($"{rarity}: gerçek tetik oranı", $"yeterli fırsat doğmadı ({opportunities}/{Samples})"); yield break; }
            double observed = rolled / (double)opportunities, tolerance = 4d * Math.Sqrt(chance * (1d - chance) / opportunities) + 0.5d / opportunities;
            c.Near($"{rarity}: GERÇEK tetik oranı ({rolled}/{opportunities} fırsat; başlatılan {rolled - rejected}, kapasite reddi {rejected})", chance, observed, tolerance, $"istatistik: ±4σ, n = {opportunities}", "0.###");
        }

        private static int duplicated, duplicateRewards, duplicateWrong;
        private static void OnDuplicateReward(EditorNumericTrace.HarvestReward reward)
        {
            duplicateRewards++;
            if (!reward.Duplicated) return;
            duplicated++;
            if (reward.AfterDuplicate != 2 * reward.Rounded) duplicateWrong++;
        }

        private static IEnumerator DuplicateRate(CheckContext c, TileRarity rarity, double chance, int seed)
        {
            var cell = new GridPosition(C, C);
            PlantSO plant = LabCatalog.Plant(ScenarioPresets.CommonPlant());
            Vector3 world = TestAttack.WorldOf(cell);
            duplicated = duplicateRewards = duplicateWrong = 0;
            EditorNumericTrace.HarvestRewardObserved += OnDuplicateReward;
            UnityEngine.Random.InitState(seed);
            for (int i = 0; i < Samples; i++)
            {
                TestField.SetPlant(cell, plant);
                TestAttack.ResetCooldown();
                TestAttack.AttackOnce(world);
                yield return LabRoutine.Frames(1);
            }
            EditorNumericTrace.HarvestRewardObserved -= OnDuplicateReward;
            if (duplicateRewards < Samples / 2) { c.Unseen($"{rarity}: gerçek çift ödül oranı", $"yeterli hasat olmadı ({duplicateRewards})"); yield break; }
            double observed = duplicated / (double)duplicateRewards, tolerance = 4d * Math.Sqrt(chance * (1d - chance) / duplicateRewards) + 0.5d / duplicateRewards;
            c.Near($"{rarity}: GERÇEK çift ödül oranı ({duplicated}/{duplicateRewards} hasat)", chance, observed, tolerance, $"istatistik: ±4σ, n = {duplicateRewards}", "0.###");
            c.Exact($"{rarity}: çift ödülde eklenen = 2 × yuvarlanan (uymayan hasat sayısı)", 0, duplicateWrong);
        }

        // Nadirlik bonusu: oyunun bitki zarından örnekleme. Beklenen olasılık üretim tablosundan (veri) ve bonustan, oyunun
        // tooltipinde anlatılan kuralla hesaplanır: ağırlık = taban şans × (1 + bonus% × nadirlik katsayısı).
        private static IEnumerator RarityRoll(CheckContext c, TileRarity rarity, double bonus, int seed)
        {
            PlantSpawner spawner = CheckContext.Spawner(C, C);
            if (spawner == null || spawner.EditorData == null) { c.Unseen($"{rarity}: bitki zarı", "üretim noktası yok"); yield break; }
            double total = 0d, rare = 0d;
            foreach (PlantSpawnEntry entry in spawner.EditorData.spawnTable)
            {
                if (entry.plant == null || entry.baseChance <= 0f) continue;
                double factor = entry.plant.rarity switch { PlantRarity.Uncommon => 1d, PlantRarity.Rare => 1.5d, PlantRarity.Epic => 2d, PlantRarity.Legendary => 3d, _ => 0d };
                double weight = entry.baseChance * (1d + bonus * 0.01d * factor);
                total += weight;
                if (entry.plant.rarity >= PlantRarity.Rare) rare += weight;
            }
            if (!(total > 0d)) { c.Unseen($"{rarity}: bitki zarı", "üretim tablosu boş"); yield break; }
            double probability = rare / total;
            const int rolls = 600;
            int seen = 0, born = 0;
            UnityEngine.Random.InitState(seed);
            for (int i = 0; i < rolls; i++)
            {
                if (!spawner.EditorRespawn(null) || spawner.SpawnedPlant == null) continue;
                born++;
                if (spawner.SpawnedPlant.TryGetComponent(out PlantHealth plant) && plant.Data != null && plant.Data.rarity >= PlantRarity.Rare) seen++;
                if ((i & 63) == 63) yield return null;
            }
            spawner.EditorRespawn(LabCatalog.Plant(ScenarioPresets.CommonPlant()));
            if (born < rolls / 2) { c.Unseen($"{rarity}: bitki zarı", "bitki doğmadı"); yield break; }
            double tolerance = 4d * Math.Sqrt(probability * (1d - probability) / born) + 0.5d / born;
            c.Near($"{rarity}: GERÇEK Rare+ bitki oranı ({seen}/{born} doğuş; bonus {bonus:0.#} puan)", probability, seen / (double)born, tolerance, $"istatistik: ±4σ, n = {born}", "0.###");
            if (rarity == TileRarity.Legendary)
            {
                double totalZero = 0d, rareZero = 0d;
                foreach (PlantSpawnEntry entry in spawner.EditorData.spawnTable)
                {
                    if (entry.plant == null || entry.baseChance <= 0f) continue;
                    totalZero += entry.baseChance;
                    if (entry.plant.rarity >= PlantRarity.Rare) rareZero += entry.baseChance;
                }
                double zero = totalZero > 0d ? rareZero / totalZero : 0d;
                c.Warn("\"Nadirlik bonusu +N% puan\" yazımı Rare+ olasılığına eklenen puan değildir", $"yazan +{bonus:0.#}% puan",
                    $"bu saksıda Rare+ bitki olasılığı %{zero * 100d:0.#} → %{probability * 100d:0.#} (+{(probability - zero) * 100d:0.#} yüzde puanı). Bonus bitki ağırlığını çarpar (Uncommon ×{1d + bonus * 0.01:0.##}, " +
                    $"Rare ×{1d + bonus * 0.015:0.##}, Epic ×{1d + bonus * 0.02:0.##}, Legendary ×{1d + bonus * 0.03:0.##}); olasılığa doğrudan eklenmez");
            }
        }

        // ---------------------------------------------------------------- R01–R10: rezonans
        private static ResonanceTier BestTier(ResonanceRule rule, int count)
        {
            ResonanceTier best = null;
            foreach (ResonanceTier tier in rule.tiers)
                if (tier != null && tier.requiredCount <= count && (best == null || tier.requiredCount > best.requiredCount)) best = tier;
            return best;
        }

        private static List<ResonanceEffect> Effects(ResonanceRule rule, ResonanceTier tier) =>
            tier.effects != null && tier.effects.Count > 0 ? tier.effects : new List<ResonanceEffect> { new ResonanceEffect { statType = rule.statType, value = tier.multiplier - 1f } };

        // "Stat başına en güçlü etki": etkin rezonansların aynı stat + işlem için verdiği değerlerden en güçlüsü (üretim süresinde en küçüğü).
        private static void MergedStatEffects(PlanterBrain brain, List<StatModifier> into)
        {
            into.Clear();
            void Merge(StatType stat, ModifierOperation operation, float value)
            {
                for (int i = 0; i < into.Count; i++)
                {
                    StatModifier old = into[i];
                    if (old.statType != stat || old.operation != operation) continue;
                    old.value = stat == StatType.PlantSpawnRate ? Mathf.Min(old.value, value) : Mathf.Max(old.value, value);
                    into[i] = old;
                    return;
                }
                into.Add(new StatModifier { statType = stat, target = StatTarget.Planter, operation = operation, value = value });
            }
            foreach (ActiveResonance active in brain.ActiveResonances)
                foreach (ResonanceEffect effect in active.effects)
                {
                    if (effect.kind == ResonanceEffectKind.Stat) Merge(effect.statType, effect.operation, effect.value);
                    else if (effect.kind == ResonanceEffectKind.Resources)
                        foreach (StatType resource in new[] { StatType.GoldGainMultiplier, StatType.IronGainMultiplier, StatType.StoneGainMultiplier }) Merge(resource, ModifierOperation.MorePercent, effect.value);
                }
        }

        private static double ExpectedPlanterStat(PlanterBrain brain, StatType stat)
        {
            var merged = new List<StatModifier>();
            MergedStatEffects(brain, merged);
            return Arith.Clamp(stat, Arith.Stat(Arith.Clamp(stat, Ordinary(brain, stat)), merged, stat));
        }

        private static IEnumerator Resonance(CheckContext c, ResonanceRule rule)
        {
            Scenario s = ScenarioPresets.Manual(ScenarioPresets.New("Kontrol · rezonans " + rule.id, "", "", "", 7), C, C);
            ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(2, 3), C - 1, C - 1);
            // Davranış hasarı rezonansı gerçek patlamayla ölçülür: yan komşular ve kesin tetik (test katkısı).
            ScenarioPresets.Planter(s, ScenarioPresets.Small(), C - 2, C - 1);
            ScenarioPresets.Planter(s, ScenarioPresets.Small(), C - 2, C);
            ScenarioPresets.Planter(s, ScenarioPresets.Small(), C - 2, C + 1);
            ScenarioPresets.Boost(s, StatType.ExplosionChance);
            ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
            s.plantFill = ScenarioPresets.CommonPlant();
            s.pauseProduction = true;
            BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            if (c.Aborted) yield break;
            PlanterBrain brain = CheckContext.Planter(C - 1, C - 1);
            if (brain == null || brain.OccupiedGrids.Count < 6) { c.Skip("ön koşul", "2 × 3 saksı kurulamadı"); yield break; }
            var cells = new List<GridPosition>();
            foreach (GridObject grid in brain.OccupiedGrids) cells.Add(grid.GetGridPosition());
            TileModifierSO primary = LabCatalog.Tile(rule.tileType, TileRarity.Common);
            TileModifierSO secondary = rule.requiresSecondary ? LabCatalog.Tile(rule.secondaryType, TileRarity.Common) : null;
            TileModifierSO behavior = rule.requiresAnyBehavior ? LabCatalog.Tile(TileModifierType.Explosive, TileRarity.Common) : null;
            if (primary == null || (rule.requiresSecondary && secondary == null)) { c.Skip("ön koşul", "gereken tile asset'i yok"); yield break; }

            var counts = new SortedSet<int>();
            int smallest = int.MaxValue, largest = 0;
            foreach (ResonanceTier tier in rule.tiers) { counts.Add(tier.requiredCount); smallest = Math.Min(smallest, tier.requiredCount); largest = Math.Max(largest, tier.requiredCount); }
            if (smallest > 1) counts.Add(smallest - 1);
            int extra = (rule.requiresSecondary ? rule.secondaryCount : 0) + (rule.requiresAnyBehavior ? 1 : 0);
            if (largest + extra > cells.Count) { c.Skip("ön koşul", "kural 2 × 3 saksıya sığmıyor"); yield break; }

            foreach (int count in counts)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    TileModifierSO tile = i < count ? primary : i < count + (rule.requiresSecondary ? rule.secondaryCount : 0) ? secondary : i == cells.Count - 1 ? behavior : null;
                    if (tile != null) TestField.ApplyTile(cells[i], tile, 1f, 0); else TestField.ClearTile(cells[i]);
                }
                ResonanceTier expectedTier = BestTier(rule, count);
                ActiveResonance active = null;
                foreach (ActiveResonance candidate in brain.ActiveResonances) if (candidate.id == rule.id) active = candidate;
                c.True($"{count} tile: beklenen kademe", expectedTier != null ? $"etkin (≥ {expectedTier.requiredCount})" : "etkin değil",
                    active != null ? $"etkin (≥ {active.requiredCount})" : "etkin değil", (active != null) == (expectedTier != null) && (active == null || active.requiredCount == expectedTier.requiredCount));
                if (active == null || expectedTier == null) continue;
                string text = TileBuffText.Resonance(active);
                foreach (ResonanceEffect effect in Effects(rule, expectedTier))
                {
                    string shown = effect.kind == ResonanceEffectKind.Stat && effect.statType == StatType.RareSpawnChance ? effect.value.ToString("0.##", Inv)
                        : effect.kind == ResonanceEffectKind.Stat && effect.statType == StatType.PlantSpawnRate ? Math.Abs(effect.value * 100f).ToString("0.##", Inv) + "%"
                        : "×" + (1f + effect.value).ToString("0.##", Inv);
                    c.True($"{count} tile: oyunun yazdığı etki veriyle aynı", $"metinde \"{shown}\"", $"\"{text}\"", text.Contains(shown));
                    switch (effect.kind)
                    {
                        case ResonanceEffectKind.Stat:
                        {
                            double expected = ExpectedPlanterStat(brain, effect.statType);
                            c.Near($"{count} tile: saksının {effect.statType} değeri = (taban + tile'lar) üzerine en güçlü rezonans etkisi", expected, brain.GetFinalStat(effect.statType), Tolerance(expected), "float");
                            break;
                        }
                        case ResonanceEffectKind.Resources:
                            foreach (StatType resource in new[] { StatType.GoldGainMultiplier, StatType.IronGainMultiplier, StatType.StoneGainMultiplier })
                            {
                                double expected = ExpectedPlanterStat(brain, resource);
                                c.Near($"{count} tile: saksının {resource} değeri", expected, brain.GetFinalStat(resource), Tolerance(expected), "float");
                            }
                            break;
                        case ResonanceEffectKind.BehaviorDamage:
                            c.Near($"{count} tile: tile'ı olan davranışın (patlama) hasar çarpanı", 1d + effect.value, brain.BehaviorDamageMultiplier(DamageType.Explosion), 1e-4, "float");
                            c.Near($"{count} tile: tile'ı OLMAYAN davranışın (elektrik) hasar çarpanı değişmedi", 1d, brain.BehaviorDamageMultiplier(DamageType.Electric), 1e-4, "float");
                            break;
                        case ResonanceEffectKind.RareScore:
                        {
                            // Skor çarpanında rezonanslar ÇARPILMAZ, en güçlüsü geçerlidir. Aynı tile'larla başka bir skor rezonansı
                            // (Şöhret) da etkin olabilir: Common bitki onu, Rare+ bitki ikisinden güçlü olanı alır.
                            double ordinary = Arith.Clamp(StatType.HarvestScoreMultiplier, Ordinary(brain, StatType.HarvestScoreMultiplier, false));
                            double general = 1d;
                            string others = "";
                            foreach (ActiveResonance other in brain.ActiveResonances)
                                foreach (ResonanceEffect e in other.effects)
                                    if (e.kind == ResonanceEffectKind.Stat && e.statType == StatType.HarvestScoreMultiplier && e.operation == ModifierOperation.MorePercent)
                                    { general = Math.Max(general, 1d + e.value); others += $"{other.resonanceName} ×{1f + e.value:0.##} "; }
                            double rare = Math.Max(general, 1d + effect.value);
                            c.Near($"{count} tile: Common bitkinin skor çarpanı = (taban + tile'lar {ordinary:0.##}) × genel skor rezonansı {general:0.##} — bu rezonans Common'a uygulanmaz",
                                ordinary * general, brain.GetHarvestScore(PlantRarity.Common), Tolerance(ordinary * general), "float");
                            c.Near($"{count} tile: Rare+ bitkinin skor çarpanı = (taban + tile'lar {ordinary:0.##}) × en güçlü etki {rare:0.##}", ordinary * rare, brain.GetHarvestScore(PlantRarity.Rare), Tolerance(ordinary * rare), "float");
                            if (general > 1.0001d)
                                c.Warn($"{count} tile: \"Rare+ Harvest Score ×{1f + effect.value:0.##}\" başka skor rezonansıyla ÇARPILMIYOR", $"yazan ×{1f + effect.value:0.##} kazancın tamamı",
                                    $"aynı anda etkin: {others.Trim()}. Rare+ toplam ×{rare:0.##} alır (×{general * (1d + effect.value):0.##} değil); Common'a göre gerçek fark ×{rare / general:0.##}");
                            break;
                        }
                        case ResonanceEffectKind.ElectricXP:
                            c.Near($"{count} tile: elektrik öldürmesi XP çarpanı (oyunun okuduğu değer)", 1d + effect.value, ResonanceManager.ElectricXP(brain.ActiveResonances), 1e-4, "float");
                            c.Manual("elektrik öldürmesi XP'sinin gerçek hasada yansıması", "elektriğin öldürdüğü başka saksının bitkisinde XP ×çarpan; bu kontrolde ölçülmüyor");
                            break;
                    }
                }
            }

            // Gerçek kullanım, en üst kademede: bu saksının bir bitkisi gerçek saldırıyla hasat edilir.
            Refill();
            GridPosition target = cells[0];
            double damageMultiplier = ExpectedPlanterStat(brain, StatType.PlanterDamageMultiplier), xp = ExpectedPlanterStat(brain, StatType.XPGainMultiplier);
            double score = Arith.Clamp(StatType.HarvestScoreMultiplier, Ordinary(brain, StatType.HarvestScoreMultiplier, false));
            foreach (ActiveResonance active in brain.ActiveResonances)
                foreach (ResonanceEffect effect in active.effects)
                    if (effect.kind == ResonanceEffectKind.Stat && effect.statType == StatType.HarvestScoreMultiplier && effect.operation == ModifierOperation.MorePercent) score *= 1d + effect.value;
            PlantHealth plant = TestField.PlantAt(target);
            if (plant == null || plant.Data == null) { c.Unseen("gerçek hasat", "saksının ilk yuvasında bitki yok"); yield break; }
            StatType resourceStat = plant.Data.resourceType == ResourceType.Gold ? StatType.GoldGainMultiplier : plant.Data.resourceType == ResourceType.Iron ? StatType.IronGainMultiplier : StatType.StoneGainMultiplier;
            double resourceExpected = ExpectedPlanterStat(brain, resourceStat);
            yield return c.Attack(target.x, target.z, 700);
            if (c.Aborted) yield break;
            TraceHit hit = c.Direct(target.x, target.z);
            if (hit != null) c.Near($"gerçek doğrudan vuruş = yuvarla(yuvarlanan × saksı hasar çarpanı {damageMultiplier:0.####})", Arith.Round(hit.DamageBeforeCrit * damageMultiplier), hit.Computed, 1d, Rounding);
            TraceHarvest harvest = c.Trace.Harvests.Find(h => h.Cell.x == target.x && h.Cell.z == target.z);
            if (harvest == null || !harvest.HasReward) { c.Unseen("gerçek hasat", "hasat kaydı oluşmadı"); yield break; }
            c.Near($"gerçek hasadın kullandığı kaynak çarpanı ({resourceStat})", resourceExpected, harvest.ResourceMultiplier, Tolerance(resourceExpected), "float");
            c.Near("gerçek hasadın kullandığı XP çarpanı", xp, harvest.XpMultiplier, Tolerance(xp), "float");
            c.Near("gerçek hasadın kullandığı saksı skor çarpanı (Common)", score, harvest.ScorePlanterMultiplier, Tolerance(score), "float");
            if (rule.requiresAnyBehavior)
            {
                // Patlama tile'ı son hücrede: o yuvanın bitkisi hasat edilince saksı gerçekten patlar.
                Refill();
                GridPosition source = cells[cells.Count - 1];
                yield return c.Attack(source.x, source.z, 701);
                if (c.Aborted) yield break;
                c.Hits(DamageType.Explosion, hits);
                AllSame(c, $"gerçek patlama vuruşu = yuvarla(hasat hasarı × {brain.BehaviorDamageMultiplier(DamageType.Explosion):0.##}) — veri: ×{1f + Effects(rule, BestTier(rule, largest))[0].value:0.##}",
                    hits, Arith.Round(Big * (1d + Effects(rule, BestTier(rule, largest))[0].value)), "patlama komşu bitkiye değmedi");
            }
        }

        // ---------------------------------------------------------------- kartlar
        private static IEnumerator TileCard(CheckContext c)
        {
            Scenario s = Clean("tile kartı", 0);
            yield return c.Build(s);
            if (c.Aborted) yield break;
            ProgressionManager progression = ProgressionManager.Instance;
            foreach ((TileModifierType type, TileRarity rarity) in new[] { (TileModifierType.Explosive, TileRarity.Rare), (TileModifierType.Fertile, TileRarity.Epic), (TileModifierType.Water, TileRarity.Common) })
            {
                TileModifierSO tile = LabCatalog.Tile(type, rarity);
                if (tile == null) { c.True($"{type} {rarity} asset'i", "var", "yok", false); continue; }
                // Kartın gösterdiği değerler teklif anında zarlanır; seçilince oyun aynı değerleri tarlaya koyar.
                var offer = new TileCardOffer(tile);
                int before = progression.RoundAppliedCells.Count;
                bool applied = progression.ApplyRandomEligibleCell(offer.Tile, offer.Modifiers);
                if (!applied || progression.RoundAppliedCells.Count != before + 1) { c.True($"{tile.name}: kart tarlaya geldi", "bir hücre", "uygulanmadı", false); continue; }
                GroundCell cell = progression.RoundAppliedCells[progression.RoundAppliedCells.Count - 1];
                bool same = cell.CurrentModifier == tile && cell.RolledModifiers.Count == offer.Modifiers.Count;
                for (int i = 0; same && i < offer.Modifiers.Count; i++) same = cell.RolledModifiers[i].Equals(offer.Modifiers[i]);
                StatModifierRange range = tile.modifierRanges[0];
                float low = Mathf.Min(range.minValue, range.maxValue), high = Mathf.Max(range.minValue, range.maxValue);
                c.True($"{tile.name}: kartta yazan \"{TileBuffText.Modifiers(offer.Modifiers)}\" tarladaki tile'da aynen duruyor", "aynı değer", same ? "aynı değer" : TileBuffText.Modifiers(cell.RolledModifiers), same);
                c.True($"{tile.name}: zar asset aralığında ({low} … {high})", "aralıkta", offer.Modifiers[0].value.ToString("0.####"), offer.Modifiers[0].value >= low - 1e-6f && offer.Modifiers[0].value <= high + 1e-6f);
                c.True($"{tile.name}: tile açık ve seviye 0 bir hücreye geldi", "açık, Sv 0", $"{(cell.IsLocked ? "kilitli" : "açık")}, Sv {cell.Level}", !cell.IsLocked && cell.Level == 0);
            }
        }

        private static IEnumerator UpgradeCard(CheckContext c)
        {
            Scenario s = Clean("yükseltme kartı", 1);
            ScenarioPresets.Tile(s, C, C, TileModifierType.Explosive, TileRarity.Common);
            ScenarioPresets.Tile(s, C + 1, C, TileModifierType.Water, TileRarity.Common);
            ScenarioPresets.Tile(s, C - 1, C, TileModifierType.Damage, TileRarity.Common);
            yield return c.Build(s);
            if (c.Aborted) yield break;
            ProgressionManager progression = ProgressionManager.Instance;
            (int x, TileRarity rarity)[] cases = { (C, TileRarity.Common), (C + 1, TileRarity.Epic), (C - 1, TileRarity.Legendary) };
            foreach ((int x, TileRarity rarity) in cases)
            {
                GroundCell cell = TestField.Ground(new GridPosition(x, C));
                PlanterBrain brain = CheckContext.Planter(x, C);
                if (cell == null || cell.CurrentModifier == null || brain == null) { c.True("ön koşul: tile ve saksı", "var", "yok", false); continue; }
                StatModifier before = cell.RolledModifiers[0];
                double statBefore = brain.GetFinalStat(before.statType);
                // Kartın seviye kazancı: Common / Rare 1, Epic 2, Legendary 3 (oyunun kuralı; en çok seviye 3).
                int gain = rarity == TileRarity.Legendary ? 3 : rarity == TileRarity.Epic ? 2 : 1;
                TileCardOffer offer = TileCardOffer.Upgrade(cell, rarity);
                c.Exact($"{rarity} yükseltme kartı: yazdığı seviye kazancı", gain, offer.LevelGain);
                bool applied = progression.ApplyUpgrade(cell, rarity);
                c.True($"{rarity}: kart uygulandı", "uygulandı", applied ? "uygulandı" : "reddedildi", applied);
                int level = Math.Min(GroundCell.MaxLevel, gain);
                c.Exact($"{rarity}: tile seviyesi", level, cell.Level);
                double value = before.value * (1d + GroundCell.LevelBonus * level);
                c.Near($"{rarity}: tile değeri = önceki {before.value:0.####} × (1 + {GroundCell.LevelBonus:0.##} × {level})", value, cell.RolledModifiers[0].value, Tolerance(value), "float");
                StatModifier after = before; after.value = (float)value;
                double plain = Arith.Clamp(before.statType, Arith.Unapply(statBefore, before));
                double expected = Arith.Clamp(before.statType, Arith.Apply(plain, after));
                c.Near($"{rarity}: üstündeki saksının {before.statType} değeri yeni tile değerini kullanıyor", expected, brain.GetFinalStat(before.statType), Tolerance(expected), "float");
            }
            GroundCell maxed = TestField.Ground(new GridPosition(C - 1, C));
            if (maxed != null)
            {
                bool again = progression.ApplyUpgrade(maxed, TileRarity.Common);
                c.True("seviye 3'teki tile yeniden yükseltilemez", "reddedildi, Sv 3", $"{(again ? "uygulandı" : "reddedildi")}, Sv {maxed.Level}", !again && maxed.Level == GroundCell.MaxLevel && !maxed.CanUpgrade);
            }
        }

        private static IEnumerator BasePowerCards(CheckContext c)
        {
            Scenario s = Clean("temel güç kartları", 0);
            yield return c.Build(s);
            if (c.Aborted) yield break;
            PlanterBrain brain = CheckContext.Planter(C, C);
            RunBalanceSO balance = RunBalanceSO.Active;
            if (brain == null) { c.Skip("ön koşul", "saksı yok"); yield break; }
            // Oyunun kart listesi (kart ekranındaki beş temel güç kartı): ad, etkilediği statlar, hedef, yön.
            (string name, StatType[] stats, StatTarget target, float sign)[] cards =
            {
                ("Hasat hasarı", new[] { StatType.HarvestDamage }, StatTarget.Player, 1f), ("Atak aralığı", new[] { StatType.AttackSpeed }, StatTarget.Player, -1f),
                ("Üretim süresi", new[] { StatType.PlantSpawnRate }, StatTarget.Planter, -1f),
                ("Tüm kaynaklar", new[] { StatType.GoldGainMultiplier, StatType.IronGainMultiplier, StatType.StoneGainMultiplier }, StatTarget.Planter, 1f),
                ("XP kazancı", new[] { StatType.XPGainMultiplier }, StatTarget.Planter, 1f),
            };
            bool small = balance != null && balance.smallBasePowerCards;
            float[] defaults = { .02f, .03f, .04f, .05f };
            c.True("profilin kart miktarı verisi", "geçerli", small ? "küçük temel güç kartları: " + string.Join(" / ", Array.ConvertAll(balance.basePowerAmounts, v => (v * 100f).ToString("0.###") + "%")) : "varsayılan %2 / %3 / %4 / %5",
                !small || (balance.basePowerAmounts != null && balance.basePowerAmounts.Length == 4));
            foreach ((string name, StatType[] stats, StatTarget target, float sign) in cards)
                foreach (TileRarity rarity in Rarities)
                {
                    float amount = small ? balance.basePowerAmounts[(int)rarity] : defaults[(int)rarity];
                    bool summed = CardSelectionUI.IsSummedBaseStat(stats[0]);
                    for (int copy = 1; copy <= 2; copy++)
                    {
                        var before = new double[stats.Length];
                        var groups = new double[stats.Length];
                        for (int i = 0; i < stats.Length; i++)
                        {
                            before[i] = target == StatTarget.Player ? PlayerStat(stats[i]) : brain.GetFinalStat(stats[i]);
                            groups[i] = Stats.SummedGroupTotal(stats[i], target);
                        }
                        var modifiers = new List<StatModifier>();
                        foreach (StatType stat in stats) modifiers.Add(new StatModifier { statType = stat, target = target, operation = ModifierOperation.MorePercent, value = amount * sign });
                        BasePowerCardRules.Apply(TileCardOffer.BaseStat(name, rarity, modifiers, summed), Stats);
                        for (int i = 0; i < stats.Length; i++)
                        {
                            // Toplanan grup: kartlar grubun toplamına eklenir (iki +a → ×(1 + 2a)). Çarpılan: her kart ayrı çarpan (×(1 − a)²).
                            double expected = summed ? before[i] / (1d + groups[i]) * (1d + groups[i] + amount * sign) : before[i] * (1d + amount * sign);
                            expected = Arith.Clamp(stats[i], expected);
                            double actual = target == StatTarget.Player ? PlayerStat(stats[i]) : brain.GetFinalStat(stats[i]);
                            c.Near($"{name} · {rarity} · {copy}. kart ({(summed ? "toplanan" : "çarpılan")}, {(sign > 0 ? "+" : "−")}{amount * 100f:0.###}%): {stats[i]} [{target}]", expected, actual, Tolerance(expected) * 0.1, "float", "0.#######");
                        }
                    }
                }
            c.Near("oyuncu kartı saksı statına, saksı kartı oyuncu statına sızmadı: kritik şansı değişmedi", Stats.GetBaseStat(StatType.CritChance), PlayerStat(StatType.CritChance), 1e-6, "float");
        }

        // ---------------------------------------------------------------- skill ağacı
        private static IEnumerator SkillTree(CheckContext c)
        {
            yield return c.Build(Clean("skill ağacı", 0));
            if (c.Aborted) yield break;
            SkillTreeManager tree = SkillTreeManager.Instance;
            ResourceManager resources = ResourceManager.Instance;
            PlanterBrain brain = CheckContext.Planter(C, C);
            if (tree == null || resources == null || brain == null) { c.Skip("ön koşul", "skill ağacı yöneticisi yok"); yield break; }
            int purchases = 0, wrongCost = 0, wrongStat = 0, wrongList = 0, effects = 0, closed = 0, unaffordable = 0;
            var details = new List<string>();
            var touched = new HashSet<StatType>();
            bool progress = true;
            for (int pass = 0; pass < 40 && progress; pass++)
            {
                progress = false;
                foreach (SkillNodeSO node in tree.AllNodes)
                {
                    if (node == null) continue;
                    while (tree.GetCurrentLevel(node) < node.tiers.Count && purchases < 900)
                    {
                        int level = tree.GetCurrentLevel(node);
                        SkillNodeTier tier = node.tiers[level];
                        foreach (ResourceType type in new[] { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone }) TestStats.SetResource(type, 2000000);
                        if (!tree.CanUpgrade(node)) break;
                        int balanceBefore = resources.GetResourceAmount(tier.costType), listBefore = Stats.GlobalModifiers.Count;
                        int removed = level > 0 ? node.tiers[level - 1].effects.Count : 0;
                        if (!tree.TryUpgrade(node)) break;
                        purchases++;
                        progress = true;
                        if (balanceBefore - resources.GetResourceAmount(tier.costType) != tier.cost)
                        { wrongCost++; if (details.Count < 6) details.Add($"{node.nodeName} Sv {level + 1}: bedel {tier.cost}, düşen {balanceBefore - resources.GetResourceAmount(tier.costType)}"); }
                        // Yeni seviye eskisinin yerine geçer: listede eski seviyenin etkileri kalkar, yenisininkiler girer.
                        if (Stats.GlobalModifiers.Count != listBefore - removed + tier.effects.Count)
                        { wrongList++; if (details.Count < 6) details.Add($"{node.nodeName} Sv {level + 1}: modifier sayısı {listBefore} → {Stats.GlobalModifiers.Count} (beklenen {listBefore - removed + tier.effects.Count})"); }
                        foreach (StatModifier effect in tier.effects)
                        {
                            effects++;
                            touched.Add(effect.statType);
                            // Stat, ham tabandan ve oyunun modifier listesinden burada yeniden hesaplanır.
                            bool player = effect.target != StatTarget.Planter && Stats.HasBaseStat(effect.statType);
                            StatTarget context = effect.target == StatTarget.All ? (player ? StatTarget.Player : StatTarget.Planter) : effect.target;
                            double expected, actual;
                            if (effect.target == StatTarget.Planter || !Stats.HasBaseStat(effect.statType)) { expected = Arith.Clamp(effect.statType, Ordinary(brain, effect.statType)); actual = brain.GetFinalStat(effect.statType); }
                            else { expected = Arith.Clamp(effect.statType, Arith.Resolve(Stats.GetBaseStat(effect.statType), effect.statType, context, Stats.GlobalModifiers, null)); actual = Stats.GetFinalStat(effect.statType, context); }
                            if (Math.Abs(expected - actual) > Tolerance(expected))
                            { wrongStat++; if (details.Count < 6) details.Add($"{node.nodeName} Sv {level + 1}: {effect.statType} [{effect.target}] beklenen {expected:0.#####}, gerçekleşen {actual:0.#####}"); }
                        }
                    }
                    if (purchases % 40 == 39) yield return null;
                }
            }
            foreach (SkillNodeSO node in tree.AllNodes)
            {
                if (node == null || tree.GetCurrentLevel(node) >= node.tiers.Count) continue;
                if (tree.IsDisabledByProfile(node)) closed++; else unaffordable++;
            }
            c.True("satın alım yapıldı", "> 0", $"{purchases} satın alım, {effects} stat etkisi, {tree.AllNodes.Count} düğüm", purchases > 0);
            c.Exact("bedeli yanlış düşen satın alım", 0, wrongCost);
            c.Exact("modifier listesi yanlış değişen satın alım (eski seviye kalkmalı, yenisi girmeli)", 0, wrongList);
            c.Exact("stat değeri bağımsız hesapla uyuşmayan etki", 0, wrongStat);
            if (details.Count > 0) c.True("uyuşmazlık ayrıntısı", "yok", string.Join(" | ", details), false);
            c.True("sonuna kadar alınamayan düğüm", "0 (profilin kapattıkları hariç)", $"{unaffordable} alınamadı, {closed} profilde kapalı", unaffordable == 0);
            var names = new List<string>();
            foreach (StatType stat in touched) names.Add(stat.ToString());
            names.Sort();
            c.Manual("skill'lerin verdiği statlar", string.Join(", ", names) + " — değerleri doğrulandı; GridUnlockSize, RoundDuration, CardSkip ve MutationLuck'ın oyundaki etkisi bu kontrolde ölçülmüyor");
        }

        // ---------------------------------------------------------------- başlangıç seçimi
        private sealed class LoadoutMeasure
        {
            public readonly Dictionary<StatType, double> Player = new(), Planter = new();
            public double Radius, DirectFactor, Resources, Score;
            public int Harvests;
        }

        private static readonly StatType[] LoadoutPlayerStats = { StatType.HarvestDamage, StatType.AreaRadius, StatType.AttackSpeed, StatType.CritChance, StatType.CritMultiplier };
        private static readonly StatType[] LoadoutPlanterStats = { StatType.PlantSpawnRate, StatType.RareSpawnChance, StatType.XPGainMultiplier, StatType.GoldGainMultiplier, StatType.HarvestScoreMultiplier, StatType.PlanterDamageMultiplier };

        private static IEnumerator MeasureLoadout(CheckContext c, string farmer, string scythe, LoadoutMeasure into)
        {
            Scenario s = Clean("başlangıç seçimi", 0); BigDamage(s); NoCrit(s);
            s.stats.RemoveAll(e => e.stat == StatType.AreaRadius);
            s.farmer = farmer; s.scythe = scythe;
            yield return c.Build(s);
            if (c.Aborted) yield break;
            PlanterBrain brain = CheckContext.Planter(C, C);
            if (brain == null) { c.Skip("ön koşul", "saksı yok"); yield break; }
            foreach (StatType stat in LoadoutPlayerStats) into.Player[stat] = PlayerStat(stat);
            foreach (StatType stat in LoadoutPlanterStats) into.Planter[stat] = brain.GetFinalStat(stat);
            // Gerçek kullanım: sekiz gerçek hasat (bitkinin canı 1), ilk vuruşun izinden doğrudan çarpan ve yarıçap.
            TestField.SetPlantHealth(new GridPosition(C, C), 1);
            yield return c.Attack(C, C, 900);
            if (c.Aborted) yield break;
            TraceHit hit = c.Direct(C, C);
            into.Radius = c.Trace.Radius;
            into.DirectFactor = hit != null && hit.BaseDamage > 0f ? hit.DamageBeforeCrit / ((double)hit.BaseDamage * hit.Variance) : double.NaN;
            Economy before = c.Trace.Before;
            PlantSO plant = LabCatalog.Plant(ScenarioPresets.CommonPlant());
            Vector3 world = TestAttack.WorldOf(new GridPosition(C, C));
            for (int i = 0; i < 7; i++)
            {
                TestField.SetPlant(new GridPosition(C, C), plant);
                TestField.SetPlantHealth(new GridPosition(C, C), 1);
                TestAttack.ResetCooldown();
                TestAttack.AttackOnce(world);
                yield return LabRoutine.Frames(1);
            }
            Economy after = Economy.Read();
            into.Harvests = 8;
            into.Resources = after.Resource(plant.resourceType) - before.Resource(plant.resourceType);
            into.Score = after.Score - before.Score;
        }

        private static IEnumerator Loadout(CheckContext c)
        {
            StartCatalogSO catalog = StartCatalogSO.Active;
            if (catalog == null || catalog.defaultFarmer == null || catalog.defaultScythe == null) { c.Skip("ön koşul", "başlangıç kataloğu yok"); yield break; }
            var baseline = new LoadoutMeasure();
            yield return MeasureLoadout(c, catalog.defaultFarmer.id, catalog.defaultScythe.id, baseline);
            if (c.Aborted) yield break;
            c.True("varsayılan seçim nötr", "nötr", $"{catalog.defaultFarmer.displayName} + {catalog.defaultScythe.displayName}", catalog.defaultFarmer.IsNeutral && catalog.defaultScythe.IsNeutral);
            c.Near("varsayılan seçimde doğrudan vuruş çarpanı 1", 1d, baseline.DirectFactor, 0.002, "tek vuruşun yuvarlaması (hasat hasarı 1000)");
            var options = new List<(StartOptionSO option, bool farmer)>();
            foreach (FarmerSO farmer in catalog.farmers) if (farmer != null && farmer != catalog.defaultFarmer) options.Add((farmer, true));
            foreach (ScytheSO scythe in catalog.scythes) if (scythe != null && scythe != catalog.defaultScythe) options.Add((scythe, false));
            foreach ((StartOptionSO option, bool isFarmer) in options)
            {
                var measured = new LoadoutMeasure();
                yield return MeasureLoadout(c, isFarmer ? option.id : catalog.defaultFarmer.id, isFarmer ? catalog.defaultScythe.id : option.id, measured);
                if (c.Aborted) yield break;
                string name = option.displayName;
                foreach (StatType stat in LoadoutPlayerStats)
                {
                    double expected = baseline.Player[stat];
                    foreach (StatModifier modifier in option.modifiers) if (modifier.statType == stat && modifier.target != StatTarget.Planter) expected = Arith.Apply(expected, modifier);
                    expected = Arith.Clamp(stat, expected);
                    c.Near($"{name}: oyuncu {stat}", expected, measured.Player[stat], Tolerance(expected), "float");
                }
                foreach (StatType stat in LoadoutPlanterStats)
                {
                    double expected = baseline.Planter[stat];
                    foreach (StatModifier modifier in option.modifiers) if (modifier.statType == stat && modifier.target != StatTarget.Player) expected = Arith.Apply(expected, modifier);
                    expected = Arith.Clamp(stat, expected);
                    c.Near($"{name}: saksı {stat}", expected, measured.Planter[stat], Tolerance(expected), "float");
                }
                double radius = baseline.Radius;
                foreach (StatModifier modifier in option.modifiers) if (modifier.statType == StatType.AreaRadius) radius = Arith.Apply(radius, modifier);
                c.Near($"{name}: gerçek saldırının temas yarıçapı", radius, measured.Radius, 1e-3, "float");
                c.Near($"{name}: gerçek vuruşun doğrudan çarpanı (yuvarlanan ÷ taban × sapma) = veri ×{option.directDamageMultiplier:0.##}", option.directDamageMultiplier, measured.DirectFactor, 0.002, "tek vuruşun yuvarlaması (hasat hasarı 1000)");
                c.Near($"{name}: sekiz gerçek hasadın kaynağı = yuvarla(varsayılan {baseline.Resources:0} × {option.harvestResourceMultiplier:0.##}) (kesir sonraki hasada taşınır)",
                    Arith.Round(baseline.Resources * option.harvestResourceMultiplier), measured.Resources, 1d, "taşınan kesir");
                c.Near($"{name}: sekiz gerçek hasadın skoru = yuvarla(varsayılan {baseline.Score:0} × {option.harvestScoreMultiplier:0.##})",
                    Arith.Round(baseline.Score * option.harvestScoreMultiplier), measured.Score, 1d, "taşınan kesir");
            }
        }

        private static IEnumerator Specialization(CheckContext c)
        {
            yield return c.Build(Clean("uzmanlaşma", 0));
            if (c.Aborted) yield break;
            RunProfileSO profile = Rounds.Profile;
            SpecializationManager manager = SpecializationManager.Instance;
            if (profile == null || manager == null) { c.Skip("ön koşul", "uzmanlaşma yöneticisi yok"); yield break; }
            bool enabled = profile.specializationAfterSegment > 0 && profile.specializationOptions.Count > 0;
            c.True("seçilmemişken çarpanlar nötr (doğrudan / davranış / kaynak)", "1 / 1 / 1",
                $"{SpecializationManager.DirectMultiplier:0.##} / {SpecializationManager.BehaviorMultiplier:0.##} / {SpecializationManager.HarvestResourceMultiplier:0.##}",
                Mathf.Approximately(SpecializationManager.DirectMultiplier, 1f) && Mathf.Approximately(SpecializationManager.BehaviorMultiplier, 1f) && Mathf.Approximately(SpecializationManager.HarvestResourceMultiplier, 1f));
            if (!enabled) c.Manual("uzmanlaşma seçimi", $"bu profilde KAPALI (specializationAfterSegment = {profile.specializationAfterSegment}): seçim ekranı hiç açılmaz; üç uzmanlaşma asset'inin çarpanları bu run'da kullanılmaz");
            else c.Manual("uzmanlaşma seçimi", $"profilde açık ({profile.specializationAfterSegment}. dönem sonu): seçim oyunun round sonu akışında yapılır; sayısal etkisi bu kontrolde ölçülmüyor");
        }
    }
}
