using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class TileBuffText
{
    public static string Name(StatType stat) => stat switch
    {
        StatType.PlanterDamageMultiplier => "Hasar",
        StatType.HarvestDamage => "Saksı hasar statı",
        StatType.XPGainMultiplier => "XP kazancı",
        StatType.HarvestScoreMultiplier => "Harvest Score",
        StatType.PlantSpawnRate => "Üretim süresi",
        StatType.RareSpawnChance => "Nadirlik bonusu",
        StatType.GoldGainMultiplier => "Gold kazancı",
        StatType.IronGainMultiplier => "Iron kazancı",
        StatType.StoneGainMultiplier => "Stone kazancı",
        StatType.ExplosionChance => "Patlama şansı",
        StatType.TornadoChance => "Tornado şansı",
        StatType.BoomerangChance => "Bumerang orak şansı",
        StatType.ElectricChance => "Çapraz elektrik şansı",
        StatType.DuplicateChance => "Çift ödül şansı",
        StatType.CritChance => "Kritik şansı",
        StatType.MutationLuck => "Kart nadirliği",
        _ => stat.ToString()
    };

    public static string Signed(float value) => (value >= 0f ? "+" : "−") +
        System.Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture);

    public static string Modifier(StatModifier mod) => Name(mod.statType) + " " + Amount(mod);

    public static string Amount(StatModifier mod)
    {
        bool percent = mod.operation == ModifierOperation.AddPercent || mod.operation == ModifierOperation.MorePercent;
        bool chance = mod.statType == StatType.ExplosionChance || mod.statType == StatType.DuplicateChance || mod.statType == StatType.CritChance ||
            mod.statType == StatType.TornadoChance || mod.statType == StatType.BoomerangChance || mod.statType == StatType.ElectricChance;
        string amount = percent || chance ? Signed(mod.value * 100f) + "%" : Signed(mod.value);
        // Crystal is already expressed in percentage points; never multiply its flat value by 100.
        if (!percent && mod.statType == StatType.RareSpawnChance) amount += "% puan";
        if (mod.operation == ModifierOperation.Flat && chance) amount += " puan";
        if (mod.operation == ModifierOperation.Set) amount = mod.value.ToString("0.##", CultureInfo.InvariantCulture) + (chance ? " (oran)" : "");
        return amount;
    }

    // Eski değer kırmızı, yeni değer yeşil. Kağıt (tooltip, liste) koyu tonlar ister; kartın
    // koyu çerçeveli yazısı açık tonlar. default: renksiz.
    public readonly struct ChangeColors
    {
        private readonly string oldHex, newHex;
        public ChangeColors(string oldHex, string newHex) { this.oldHex = oldHex; this.newHex = newHex; }
        public string Old(string text) => oldHex == null ? text : $"<color=#{oldHex}>{text}</color>";
        public string New(string text) => newHex == null ? text : $"<color=#{newHex}>{text}</color>";
    }

    public static readonly ChangeColors OnPaper = new ChangeColors("B8322A", "23752F");
    public static readonly ChangeColors OnCard = new ChangeColors("FF8577", "7CF08E");

    // Bu modifier'ların üretim süresi çarpanı (Fertile −%47,5 → 0,525). Üretime dokunmayan tile: 1.
    public static float SpawnFactor(IReadOnlyList<StatModifier> modifiers)
    {
        float factor = 1f;
        if (modifiers != null) foreach (var mod in modifiers)
            if (mod.statType == StatType.PlantSpawnRate && mod.operation == ModifierOperation.MorePercent) factor *= 1f + mod.value;
        return factor;
    }

    // Saksı zaten üretim tabanındayken bu tile'ın hızının dönüştüğü nadirlik.
    public static float SpawnRarityAtFloor(IReadOnlyList<StatModifier> modifiers) =>
        StatCalculator.SpawnOverflowRarity(StatCalculator.MinimumSpawnInterval * SpawnFactor(modifiers));

    public static string Points(float value) => "+" + value.ToString("0.#", CultureInfo.InvariantCulture);

    // "Sv 0 → Sv 1"; kısa hali "Sv 0 → 1".
    public static string LevelChange(int from, int to, ChangeColors colors = default, bool shortForm = false) =>
        "Sv " + colors.Old(from.ToString()) + " → " + colors.New((shortForm ? "" : "Sv ") + to);

    // Seviye atlayan tile: eski ve yeni değer, "+13% → +16.25% puan". Ortak birim sonda bir kez yazılır.
    public static string AmountChange(StatModifier before, StatModifier after, ChangeColors colors = default)
    {
        string from = Amount(before), to = Amount(after);
        foreach (string unit in SharedUnits)
            if (from.EndsWith(unit) && to.EndsWith(unit)) { from = from.Substring(0, from.Length - unit.Length); break; }
        return colors.Old(from) + " → " + colors.New(to);
    }

    private static readonly string[] SharedUnits = { " puan", " (oran)" };

    public static string ModifierChanges(IReadOnlyList<StatModifier> before, IReadOnlyList<StatModifier> after, ChangeColors colors = default)
    {
        var text = new StringBuilder();
        if (after != null) for (int i = 0; i < after.Count; i++)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append(before != null && i < before.Count
                ? Name(after[i].statType) + " " + AmountChange(before[i], after[i], colors)
                : Modifier(after[i]));
        }
        return text.ToString();
    }

    public static string Modifiers(IReadOnlyList<StatModifier> modifiers)
    {
        var text = new StringBuilder();
        if (modifiers != null) foreach (var mod in modifiers)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append(Modifier(mod));
        }
        return text.ToString();
    }

    public static string Resonance(ActiveResonance resonance)
    {
        if (resonance.effects == null || resonance.effects.Count == 0)
            return Name(resonance.statType) + " " + Signed((resonance.multiplier - 1f) * 100f) + "%";
        var lines = new List<string>();
        foreach (var effect in resonance.effects)
        {
            string multiplier = "×" + (1f + effect.value).ToString("0.##", CultureInfo.InvariantCulture);
            switch (effect.kind)
            {
                case ResonanceEffectKind.Resources: lines.Add("Gold / Iron / Stone " + multiplier); break;
                case ResonanceEffectKind.RareScore: lines.Add("Rare+ Harvest Score " + multiplier); break;
                case ResonanceEffectKind.ElectricXP: lines.Add("Elektrik öldürmesi XP " + multiplier); break;
                case ResonanceEffectKind.BehaviorDamage:
                    var names = new List<string>();
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Explosion)) != 0) names.Add("Patlama");
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Tornado)) != 0) names.Add("Tornado");
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Boomerang)) != 0) names.Add("Orak");
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Electric)) != 0) names.Add("Elektrik");
                    lines.Add(string.Join(" / ", names) + " hasarı " + multiplier); break;
                default:
                    lines.Add(effect.statType == StatType.RareSpawnChance ? "Nadirlik +" + effect.value.ToString("0.##", CultureInfo.InvariantCulture) + " puan" :
                        Name(effect.statType) + " " + (effect.statType == StatType.PlantSpawnRate ? Signed(effect.value * 100f) + "%" : multiplier)); break;
            }
        }
        return string.Join(" · ", lines);
    }
}
