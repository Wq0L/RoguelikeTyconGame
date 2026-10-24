using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    public sealed class ChanceSample
    {
        public string Name = "", Note = "";
        public DamageType Behavior;
        public GridPosition Cell;
        public int Seed, Requested, Trials, Opportunities, Rolled, Started, Rejected, NoOpportunity;
        // Gerçek hesabın kullandığı şans (saksının çözülmüş şansı): örnek boyunca en küçük ve en büyük okunan değer.
        public double Expected, ExpectedMin, ExpectedMax, Observed, Low, High;
        public bool Complete, Stopped;

        // Şans tam 0 ya da 1 ise "beklenen tutan / tutmayan sayısı" ölçütü anlamsızdır; yalnız örnek büyüklüğüne bakılır.
        public bool SmallSample => Opportunities < 100 || (Expected > 0d && Expected < 1d && (Opportunities * Expected < 5d || Opportunities * (1d - Expected) < 5d));
        public bool ExpectedMoved => ExpectedMax - ExpectedMin > 1e-6;
        public bool Inside => Opportunities > 0 && Expected >= Low - 1e-9 && Expected <= High + 1e-9;

        public string Verdict()
        {
            if (Opportunities == 0) return "Fırsat doğmadı: oran hesaplanamaz.";
            string verdict = Inside
                ? "Gerçek hesabın kullandığı şans, gözlenen oranın %95 güven aralığının İÇİNDE (çelişki yok)."
                : "Gerçek hesabın kullandığı şans, gözlenen oranın %95 güven aralığının DIŞINDA: İNCELE (tek örnek kanıt değildir; başka seed ve daha büyük örnekle tekrarla).";
            if (SmallSample) verdict += " KÜÇÜK ÖRNEK: bu sonuç kesin değildir.";
            if (ExpectedMoved) verdict += $" UYARI: şans örnek sırasında değişti ({ExpectedMin * 100d:0.##}% – {ExpectedMax * 100d:0.##}%); koşul sabit değil.";
            return verdict;
        }

        public string ToText()
        {
            var text = new StringBuilder();
            text.Append($"ŞANS ÖRNEĞİ · {Name} · {ChanceSampler.Label(Behavior)} · saksı ({Cell.x}, {Cell.z}) · seed {Seed}" + (Complete ? "" : Stopped ? " · DURDURULDU (eksik)" : " · sürüyor") + "\n");
            text.Append($"  Deneme sayısı: {Trials} / istenen {Requested}\n");
            text.Append($"  Gerçek fırsat sayısı (şansı olan saksıda doğrudan hasat): {Opportunities}\n");
            text.Append($"  Fırsat doğmayan deneme (saldırı reddedildi ya da bitki doğrudan vuruşla ölmedi): {NoOpportunity}\n");
            text.Append($"  Başarılı zar sayısı (oyunun sayacı): {Rolled}\n");
            text.Append($"  Gerçekten başlatılan davranış: {Started}\n");
            text.Append($"  Kapasite nedeniyle reddedilen (zar tuttu, davranış başlamadı): {Rejected}\n");
            text.Append($"  Gözlenen oran (başarılı zar ÷ fırsat): {(Opportunities > 0 ? (Observed * 100d).ToString("0.##") + "%" : "hesaplanamaz")}" +
                        (Opportunities > 0 ? $" · %95 güven aralığı {Low * 100d:0.##}% – {High * 100d:0.##}% (Wilson)" : "") + "\n");
            text.Append($"  Gerçek hesabın kullandığı beklenen şans: {Expected * 100d:0.##}%\n");
            text.Append("  ").Append(Verdict()).Append('\n');
            if (Note.Length > 0) text.Append("  ").Append(Note).Append('\n');
            return text.ToString();
        }
    }

    // Şans mekaniklerinin kontrollü örneklemesi. Her deneme oyunun GERÇEK tetik yolundan geçer: bitki oyunun üretim girişinden
    // çıkarılır, tek gerçek doğrudan saldırı atılır, şans zarını oyunun kendi davranış kodu atar. Sayılar oyunun sayaçlarından
    // okunur (HarvestBehaviorStats: tutan zar ve başlayamayan davranış). "Zar başarılı" ile "davranış başlatıldı" ayrıdır.
    // Seed: örnek başlarken oyunun rastgele akışı bu seed'le başlatılır — açık test eylemidir, kayıt için ek zar atılmaz.
    public static class ChanceSampler
    {
        public const int MaxTrials = 5000, Kept = 20;
        private const string RoutineName = "şans örneklemesi";

        public static readonly (DamageType type, TileModifierType tile, StatType stat, string label)[] Behaviors =
        {
            (DamageType.Explosion, TileModifierType.Explosive, StatType.ExplosionChance, "Patlama"),
            (DamageType.Electric, TileModifierType.Electric, StatType.ElectricChance, "Elektrik"),
            (DamageType.Tornado, TileModifierType.Tornado, StatType.TornadoChance, "Kasırga"),
            (DamageType.Boomerang, TileModifierType.Boomerang, StatType.BoomerangChance, "Bumerang"),
        };

        private static readonly List<ChanceSample> samples = new();
        public static IReadOnlyList<ChanceSample> Samples => samples;
        public static ChanceSample Current { get; private set; }
        public static bool Running => LabRoutine.Running && LabRoutine.Name == RoutineName;
        public static int Version { get; private set; }

        public static void Clear() { samples.Clear(); Version++; }

        public static string Label(DamageType type) => Behaviors[Index(type)].label;

        private static int Index(DamageType type)
        {
            for (int i = 0; i < Behaviors.Length; i++) if (Behaviors[i].type == type) return i;
            return 0;
        }

        // Sabit koşullu hazır düzen: tek saksı, altında davranışın gerçek tile'ı (şans tile'dan gelir; test şans katkısı yok),
        // yalnız merkez hücreye vuran saldırı, üretim duraklatılmış. Her deneme tam bir fırsattır.
        public static Scenario Preset(DamageType type, TileRarity rarity = TileRarity.Common)
        {
            (DamageType _, TileModifierType tile, StatType _, string label) = Behaviors[Index(type)];
            const int c = ScenarioPresets.C;
            Scenario s = ScenarioPresets.Manual(ScenarioPresets.New($"Şans · {label} ({rarity} tile, sabit koşul)",
                "'H · Sayısal > Şans' sekmesinden örneklemeyi başlat.",
                "Her denemede merkezdeki bitki tek gerçek doğrudan saldırıyla hasat edilir; şans zarını oyunun davranış kodu atar.",
                "Gözlenen oran ile saksının gerçek şansı; küçük örnekte kesin sonuç gibi okunmaz.", 5), c, c);
            ScenarioPresets.Planter(s, ScenarioPresets.Small(), c, c);
            ScenarioPresets.Tile(s, c, c, tile, rarity);
            ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
            s.plantFill = ScenarioPresets.CommonPlant();
            s.plantHealth = 1;
            s.pauseProduction = true;
            return s;
        }

        public static Result Run(GridPosition cell, DamageType type, int trials, int seed, string name = null)
        {
            if (!TestSession.Ready) return Result.Fail("Test oturumu hazır değil.");
            if (LabRoutine.Running) return Result.Fail("Başka bir iş sürüyor: " + LabRoutine.Name + ".");
            PlanterBrain planter = TestField.PlanterAt(cell);
            if (planter == null || TestField.SpawnerAt(cell) == null) return Result.Fail($"({cell.x}, {cell.z}) bir üretim noktası değil: örnekleme için saksısı olan bir hücre seç.");
            (DamageType _, TileModifierType _, StatType stat, string label) = Behaviors[Index(type)];
            float chance = planter.GetFinalStat(stat);
            if (!(chance > 0f)) return Result.Fail($"Bu saksının {label} şansı 0: fırsat doğmaz (davranış tile'ı yok). Koşul sağlanmadan örnekleme yapılmaz.");
            trials = Mathf.Clamp(trials, 1, MaxTrials);
            var sample = new ChanceSample
            {
                Name = name ?? (TestSession.Current != null ? TestSession.Current.name : "elle kurulan düzen"), Behavior = type, Cell = cell, Seed = seed, Requested = trials,
                Expected = chance, ExpectedMin = chance, ExpectedMax = chance,
            };
            Current = sample;
            samples.Add(sample);
            while (samples.Count > Kept) samples.RemoveAt(0);
            LabRoutine.Start(RoutineName, Sample(sample, stat), Finished);
            return Result.Success($"Şans örneklemesi başladı: {label} · {trials} deneme · seed {seed}.");
        }

        public static void Stop()
        {
            if (!Running) return;
            LabRoutine.Stop();
            if (Current != null) Current.Stopped = true;
            Finished();
        }

        private static bool autoBefore, productionBefore, controlsSaved;

        private static void Finished()
        {
            if (controlsSaved && TestSession.Active)
            {
                TestAttack.AutoAttack = autoBefore;
                TestAttack.ProductionPaused = productionBefore;
            }
            controlsSaved = false;
            ChanceSample sample = Current;
            Current = null;
            Version++;
            if (sample == null) return;
            Close(sample);
            LabReport.Remember("Şans örneği", sample.ToText());
            TestLog.Note(TestLog.Kind.Tool, $"Şans örneği bitti: {sample.Behavior} · fırsat {sample.Opportunities} · başarılı zar {sample.Rolled} · başlayan {sample.Started} · reddedilen {sample.Rejected}");
        }

        private static void Close(ChanceSample sample)
        {
            sample.Started = sample.Rolled - sample.Rejected;
            int n = sample.Opportunities;
            if (n <= 0) return;
            double p = (double)sample.Rolled / n;
            const double z = 1.959964;
            double denominator = 1d + z * z / n, center = (p + z * z / (2d * n)) / denominator;
            double half = z * Math.Sqrt(p * (1d - p) / n + z * z / (4d * n * n)) / denominator;
            sample.Observed = p;
            sample.Low = Math.Max(0d, center - half);
            sample.High = Math.Min(1d, center + half);
        }

        private static IEnumerator Sample(ChanceSample sample, StatType stat)
        {
            GridPosition cell = sample.Cell;
            // Araya başka saldırı ve doğan bitki girmesin: örnek süresince otomatik saldırı kapalı, üretim duraklatılmış
            // (örnek bitince ya da durdurulunca eski hâline döner: Finished).
            autoBefore = TestAttack.AutoAttack; productionBefore = TestAttack.ProductionPaused; controlsSaved = true;
            TestAttack.AutoAttack = false;
            TestAttack.ProductionPaused = true;
            Vector3 world = TestAttack.WorldOf(cell);
            PlantSO plant = null;
            var table = new List<PlantSO>();
            PlantSpawner spawner = TestField.SpawnerAt(cell);
            LabCatalog.PlantsOf(spawner != null ? spawner.EditorData : null, table);
            table.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : string.CompareOrdinal(a.name, b.name));
            if (table.Count > 0) plant = table[0];
            UnityEngine.Random.InitState(sample.Seed);
            for (int i = 0; i < sample.Requested; i++)
            {
                PlanterBrain planter = TestField.PlanterAt(cell);
                RoundManager rounds = RoundManager.Instance;
                if (!TestSession.Ready || planter == null || rounds == null || !rounds.IsRoundActive) { sample.Note = "Örnek erken bitti: round ya da saksı artık yok."; sample.Stopped = true; yield break; }
                // Bitki oyunun üretim girişinden çıkar; canı 1 yapılır ki tek doğrudan vuruş kesin hasat etsin (hasar statına dokunulmaz).
                Result placed = TestField.SetPlant(cell, plant);
                PlantHealth target = placed.Ok ? TestField.PlantAt(cell) : null;
                if (target != null) target.EditorSetHealth(1);
                double chance = planter.GetFinalStat(stat);
                sample.ExpectedMin = Math.Min(sample.ExpectedMin, chance);
                sample.ExpectedMax = Math.Max(sample.ExpectedMax, chance);
                int rolled = HarvestBehaviorStats.Triggered(sample.Behavior), skipped = HarvestBehaviorStats.Skipped(sample.Behavior);
                TestAttack.ResetCooldown();
                bool attacked = target != null && TestAttack.AttackOnce(world).Ok;
                sample.Trials++;
                bool opportunity = attacked && target.IsDead && target.KilledBy == DamageType.Direct;
                if (opportunity)
                {
                    sample.Opportunities++;
                    sample.Rolled += HarvestBehaviorStats.Triggered(sample.Behavior) - rolled;
                    sample.Rejected += HarvestBehaviorStats.Skipped(sample.Behavior) - skipped;
                }
                else sample.NoOpportunity++;
                if ((i & 15) == 0) { Close(sample); Version++; }
                yield return LabRoutine.Frames(1);
            }
            sample.Complete = true;
        }

        public static string ToCsv()
        {
            var text = new StringBuilder("ad;davranis;x;z;seed;istenen;deneme;firsat;firsat_dogmayan;basarili_zar;baslatilan;kapasite_reddi;gozlenen_oran;beklenen_sans;alt_95;ust_95;kucuk_ornek;tamamlandi\n");
            var c = System.Globalization.CultureInfo.InvariantCulture;
            foreach (ChanceSample s in samples)
                text.Append(Snapshots.Csv(s.Name)).Append(';').Append(s.Behavior).Append(';').Append(s.Cell.x).Append(';').Append(s.Cell.z).Append(';').Append(s.Seed).Append(';')
                    .Append(s.Requested).Append(';').Append(s.Trials).Append(';').Append(s.Opportunities).Append(';').Append(s.NoOpportunity).Append(';').Append(s.Rolled).Append(';')
                    .Append(s.Started).Append(';').Append(s.Rejected).Append(';').Append(s.Observed.ToString("0.######", c)).Append(';').Append(s.Expected.ToString("0.######", c)).Append(';')
                    .Append(s.Low.ToString("0.######", c)).Append(';').Append(s.High.ToString("0.######", c)).Append(';').Append(s.SmallSample ? 1 : 0).Append(';').Append(s.Complete ? 1 : 0).Append('\n');
            return text.ToString();
        }
    }
}
