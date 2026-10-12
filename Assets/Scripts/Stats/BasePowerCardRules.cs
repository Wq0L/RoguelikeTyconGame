// Only base-power cards use this policy. Tile upgrades and boss modifiers keep their own rules.
public static class BasePowerCardRules
{
    public static bool Enabled(RunBalanceSO balance) => balance != null && balance.smallBasePowerCards;

    public static float Amount(TileRarity rarity, RunBalanceSO balance)
    {
        int index = (int)rarity;
        if (Enabled(balance) && balance.basePowerAmounts != null && index >= 0 && index < balance.basePowerAmounts.Length)
        {
            float amount = balance.basePowerAmounts[index];
            if (float.IsFinite(amount) && amount > 0f && amount < 1f) return amount;
        }
        if (Enabled(balance)) throw new System.InvalidOperationException("Invalid base-power amount: " + rarity);
        return rarity switch { TileRarity.Legendary => .05f, TileRarity.Epic => .04f, TileRarity.Rare => .03f, _ => .02f };
    }

    public static bool IsSummed(StatType stat, RunBalanceSO balance)
    {
        if (Enabled(balance))
            return stat == StatType.HarvestDamage || stat == StatType.XPGainMultiplier ||
                   stat == StatType.GoldGainMultiplier || stat == StatType.IronGainMultiplier || stat == StatType.StoneGainMultiplier;
        return stat == StatType.XPGainMultiplier && balance != null && balance.additiveBaseXpCards;
    }

    // Interval reductions remain multiplicative so unlimited cards never produce a negative interval.
    // StatCalculator / PlayerController retain the existing production and attack floors.
    public static void Apply(TileCardOffer offer, StatManager stats)
    {
        foreach (StatModifier modifier in offer.Modifiers)
            if (offer.IsSummed) stats.AddToSummedGroup(modifier);
            else stats.AddGlobalModifier(modifier);
    }

    public static RunBalanceSO Current => DemoSceneSettings.IsDemo ? null : RunBalanceSO.Active;
}
