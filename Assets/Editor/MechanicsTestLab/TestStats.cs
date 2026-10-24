using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Test stat katkıları ve ilerleme komutları.
    // - Katkı AYRI bir kaynaktır: oyunun kendi modifier listesine (StatManager) normal bir global modifier olarak eklenir ve yalnız
    //   bu sınıfın eklediği kaldırılır (boss ödülü, ağaç, kart, başlangıç seçimi ve geçici etkilere dokunulmaz).
    // - Sonuç değer oyunun hesabından okunur (StatManager / PlanterBrain.GetFinalStat): taban, sınır (floor / cap) ve dönüşüm
    //   kuralları aynen uygulanır. "Test katkısı olmadan" değeri, aynı hesaplayıcının katkılar çıkarılmış listeyle çağrılmasıdır.
    public static class TestStats
    {
        // Panelde sunulan statlar ve doğal kapsamları. Kapsam "Planter" ise katkı BÜTÜN saksılara uygulanan global katkıdır;
        // tek bir saksıya özel katkı tile / rezonansla (tarla kurma) verilir.
        public static readonly (StatType stat, StatTarget target, string label)[] Offered =
        {
            (StatType.HarvestDamage, StatTarget.Player, "Doğrudan hasar (hasat hasarı)"),
            (StatType.CritChance, StatTarget.Player, "Kritik şansı (0–1)"),
            (StatType.CritMultiplier, StatTarget.Player, "Kritik çarpanı"),
            (StatType.AttackSpeed, StatTarget.Player, "Saldırı aralığı (sn; küçük = hızlı)"),
            (StatType.AreaRadius, StatTarget.Player, "Saldırı yarıçapı (dünya birimi; hücre 2)"),
            (StatType.PlantSpawnRate, StatTarget.Planter, "Üretim aralığı (sn) · bütün saksılar"),
            (StatType.RareSpawnChance, StatTarget.Planter, "Nadirlik bonusu · bütün saksılar"),
            (StatType.ExplosionChance, StatTarget.Planter, "Patlama şansı · bütün saksılar"),
            (StatType.ElectricChance, StatTarget.Planter, "Elektrik şansı · bütün saksılar"),
            (StatType.TornadoChance, StatTarget.Planter, "Kasırga şansı · bütün saksılar"),
            (StatType.BoomerangChance, StatTarget.Planter, "Bumerang şansı · bütün saksılar"),
            (StatType.DuplicateChance, StatTarget.Planter, "Çift ödül şansı · bütün saksılar"),
            (StatType.PlanterDamageMultiplier, StatTarget.Planter, "Saksı hasar çarpanı (yalnız doğrudan vuruş) · bütün saksılar"),
            (StatType.XPGainMultiplier, StatTarget.Planter, "XP çarpanı · bütün saksılar"),
            (StatType.GoldGainMultiplier, StatTarget.Planter, "Altın çarpanı · bütün saksılar"),
            (StatType.IronGainMultiplier, StatTarget.Planter, "Demir çarpanı · bütün saksılar"),
            (StatType.StoneGainMultiplier, StatTarget.Planter, "Taş çarpanı · bütün saksılar"),
            (StatType.HarvestScoreMultiplier, StatTarget.Player, "Hasat skoru çarpanı"),
            (StatType.MutationLuck, StatTarget.Mutation, "Kart nadirlik şansı (Kart Sezgisi)"),
        };

        private static readonly List<StatModifier> contributions = new();
        public static IReadOnlyList<StatModifier> Contributions => contributions;
        public static int Version { get; private set; }

        private static StatManager Stats => StatManager.Instance;

        public static Result Add(StatModifier modifier)
        {
            if (Stats == null) return Result.Fail("StatManager yok.");
            if (float.IsNaN(modifier.value) || float.IsInfinity(modifier.value)) return Result.Fail("Geçersiz değer.");
            Stats.AddGlobalModifier(modifier);
            contributions.Add(modifier);
            Version++;
            return Result.Success($"Test katkısı: {Describe(modifier)}");
        }

        public static Result RemoveAt(int index)
        {
            if (index < 0 || index >= contributions.Count) return Result.Fail("Katkı bulunamadı.");
            StatModifier modifier = contributions[index];
            contributions.RemoveAt(index);
            if (Stats != null && Contains(Stats.GlobalModifiers, modifier)) Stats.RemoveGlobalModifier(modifier);
            Version++;
            return Result.Success($"Test katkısı kaldırıldı: {Describe(modifier)}");
        }

        // Yalnız test katkıları kalkar.
        public static Result Clear()
        {
            int count = contributions.Count;
            if (Stats != null)
                foreach (StatModifier modifier in contributions)
                    if (Contains(Stats.GlobalModifiers, modifier)) Stats.RemoveGlobalModifier(modifier);
            contributions.Clear();
            Version++;
            return Result.Success($"{count} test katkısı kaldırıldı; diğer kaynaklar aynen duruyor.");
        }

        // Sahne yeniden yüklendi: StatManager yenidir, eski katkılar onunla birlikte gitti.
        public static void Forget()
        {
            contributions.Clear();
            Version++;
        }

        public static string Describe(StatModifier m) => m.operation switch
        {
            ModifierOperation.Flat => $"{m.statType} {(m.value >= 0 ? "+" : "")}{m.value:0.####} (düz) · {m.target}",
            ModifierOperation.AddPercent => $"{m.statType} {(m.value >= 0 ? "+" : "")}{m.value * 100f:0.##}% (toplanan yüzde) · {m.target}",
            ModifierOperation.MorePercent => $"{m.statType} ×{1f + m.value:0.####} (ayrı çarpan) · {m.target}",
            _ => $"{m.statType} = {m.value:0.####} (tabanı değiştirir) · {m.target}",
        };

        // Stat temel settedeyse global taban; değilse (saksıya ait stat) NaN.
        public static float Base(StatType stat) => Stats != null && Stats.HasBaseStat(stat) ? Stats.GetBaseStat(stat) : float.NaN;
        public static float Final(StatType stat, StatTarget target) => Stats != null ? Stats.GetFinalStat(stat, target) : float.NaN;

        // Aynı hesaplayıcı, test katkıları çıkarılmış global listeyle.
        public static float WithoutTest(StatType stat, StatTarget target, float baseValue, IReadOnlyList<StatModifier> local = null)
        {
            if (Stats == null) return float.NaN;
            return StatCalculator.Calculate(baseValue, stat, target, GlobalsWithoutTest(), local);
        }

        public static List<StatModifier> GlobalsWithoutTest()
        {
            var list = new List<StatModifier>(Stats != null ? Stats.GlobalModifiers : System.Array.Empty<StatModifier>());
            foreach (StatModifier modifier in contributions)
            {
                int index = IndexOf(list, modifier);
                if (index >= 0) list.RemoveAt(index);
            }
            return list;
        }

        // ---------------------------------------------------------------- level / XP / kaynak
        // amount XP verilirse normal akışta kaç level kazanılır ve kaç kart seçim hakkı doğar (işlemden ÖNCE gösterilir).
        // Maliyetler oyunun XP verisinden okunur; sayım limit'e ulaşırsa capped true döner.
        public static bool PreviewXP(double amount, int limit, out int levels, out int choices, out bool capped)
        {
            levels = choices = 0;
            capped = false;
            ProgressionManager progression = ProgressionManager.Instance;
            RoundManager rounds = RoundManager.Instance;
            if (progression == null || progression.Data == null || rounds == null || progression.LevelProcessingHalted) return false;
            double xp = progression.StoredXP + System.Math.Max(0d, amount);
            int level = progression.CurrentLevel;
            while (levels < limit)
            {
                float cost = progression.Data.GetXPForLevel(level);
                if (!(cost > 0f) || float.IsInfinity(cost) || xp < cost) break;
                xp -= cost;
                level++;
                levels++;
            }
            capped = levels >= limit;
            choices = levels * rounds.ChoicesPerLevel;
            return true;
        }

        // Normal level kazanma akışı: XP oyunun AddXP yolundan verilir (OnLevelUp, kart seçim hakları).
        public static Result GiveXP(double amount)
        {
            ProgressionManager progression = ProgressionManager.Instance;
            if (progression == null) return Result.Fail("ProgressionManager yok.");
            if (!(amount > 0d)) return Result.Fail("XP miktarı 0'dan büyük olmalı.");
            int before = progression.CurrentLevel;
            int pending = RoundManager.Instance != null ? RoundManager.Instance.PendingCardSelections : 0;
            progression.AddXP(amount);
            int gained = progression.CurrentLevel - before;
            int choices = RoundManager.Instance != null ? RoundManager.Instance.PendingCardSelections - pending : 0;
            return Result.Success($"+{amount:0.#} XP (normal akış): {gained} level, +{choices} kart seçim hakkı" +
                                  (progression.HasPendingLevels ? " · kalan level işi sonraki karelerde sürüyor" : ""));
        }

        // Level'ı doğrudan atar: kart seçim hakkı doğmaz, level kazanma olayı çalışmaz.
        public static Result SetLevel(int level)
        {
            ProgressionManager progression = ProgressionManager.Instance;
            if (progression == null) return Result.Fail("ProgressionManager yok.");
            return progression.EditorSetLevel(level)
                ? Result.Success($"Level {progression.CurrentLevel} atandı (kart hakkı yok; saklı XP 0; sonraki level {progression.XPToNextLevel:0.#} XP).")
                : Result.Fail("Level atanamadı (geçersiz level ya da XP tablosu).");
        }

        public static Result SetResource(ResourceType type, int amount)
        {
            ResourceManager resources = ResourceManager.Instance;
            if (resources == null) return Result.Fail("ResourceManager yok.");
            amount = Mathf.Max(0, amount);
            int current = resources.GetResourceAmount(type);
            if (amount > current) resources.AddResource(type, amount - current);
            else if (amount < current) resources.SpendResource(type, current - amount);
            return Result.Success($"{type}: {resources.GetResourceAmount(type):N0}");
        }

        public static bool Contains(IReadOnlyList<StatModifier> list, StatModifier modifier) => IndexOf(list, modifier) >= 0;

        public static int IndexOf(IReadOnlyList<StatModifier> list, StatModifier modifier)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Equals(modifier)) return i;
            return -1;
        }
    }
}
