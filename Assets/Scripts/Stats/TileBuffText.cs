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
        StatType.PlantSpawnRate => "Üretim süresi",
        StatType.RareSpawnChance => "Nadirlik bonusu",
        StatType.GoldGainMultiplier => "Gold kazancı",
        StatType.IronGainMultiplier => "Iron kazancı",
        StatType.StoneGainMultiplier => "Stone kazancı",
        StatType.ExplosionChance => "Patlama şansı",
        StatType.DuplicateChance => "Çift ödül şansı",
        StatType.CritChance => "Kritik şansı",
        _ => stat.ToString()
    };

    public static string Signed(float value) => (value >= 0f ? "+" : "−") +
        System.Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture);

    public static string Modifier(StatModifier mod)
    {
        bool percent = mod.operation == ModifierOperation.AddPercent || mod.operation == ModifierOperation.MorePercent;
        bool chance = mod.statType == StatType.ExplosionChance || mod.statType == StatType.DuplicateChance || mod.statType == StatType.CritChance;
        string amount = percent || chance ? Signed(mod.value * 100f) + "%" : Signed(mod.value);
        // Crystal is already expressed in percentage points; never multiply its flat value by 100.
        if (!percent && mod.statType == StatType.RareSpawnChance) amount += "% puan";
        if (mod.operation == ModifierOperation.Flat && chance) amount += " puan";
        if (mod.operation == ModifierOperation.Set) amount = mod.value.ToString("0.##", CultureInfo.InvariantCulture) + (chance ? " (oran)" : "");
        return Name(mod.statType) + " " + amount;
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

    public static string Resonance(ActiveResonance resonance) =>
        Name(resonance.statType) + " " + Signed((resonance.multiplier - 1f) * 100f) + "%";
}
