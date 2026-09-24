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
