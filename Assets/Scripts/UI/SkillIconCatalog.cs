// Shared by tree nodes and their tooltips. Classification reads effects, never names or prices.
public static class SkillIconCatalog
{
    public static string For(SkillNodeSO node)
    {
        if (node == null) return "skill-unlock";
        if (node.unlockType != UnlockType.None)
            return node.unlockType switch
            {
                UnlockType.TileBehavior_Tornado => "skill-tornado",
                UnlockType.TileBehavior_Explosive => "skill-explosion",
                UnlockType.TileBehavior_Electric => "skill-electric",
                UnlockType.TileBehavior_Boomerang => "skill-scythe",
                UnlockType.TileBehavior_Duplicate => "skill-duplicate",
                UnlockType.Planter_1x3 or UnlockType.Planter_2x2 or UnlockType.Planter_2x3 => "skill-planter",
                _ => "skill-unlock"
            };
        if (node.tiers != null)
            foreach (var tier in node.tiers)
                if (tier != null && tier.effects != null && tier.effects.Count > 0)
                    return For(tier.effects[0].statType);
        return "skill-unlock";
    }

    public static string For(StatType stat) => stat switch
    {
        StatType.HarvestDamage or StatType.PlanterDamageMultiplier => "skill-attack",
        StatType.AttackSpeed => "skill-speed",
        StatType.AreaRadius => "skill-area",
        StatType.CritChance or StatType.CritMultiplier => "skill-critical",
        StatType.PlantSpawnRate => "skill-spawn",
        StatType.RareSpawnChance => "skill-rare",
        StatType.GoldGainMultiplier or StatType.StartingGoldBonus => "skill-gold",
        StatType.IronGainMultiplier => "skill-iron",
        StatType.StoneGainMultiplier => "skill-stone",
        StatType.XPGainMultiplier => "skill-xp",
        StatType.HarvestScoreMultiplier => "skill-score",
        StatType.RoundDuration => "skill-time",
        StatType.GridUnlockSize => "skill-grid",
        StatType.StartingPlanterCount => "skill-planter",
        StatType.MutationLuck or StatType.ExtraCardChoice or StatType.RerollCard or StatType.CardSkip => "skill-cards",
        StatType.ExplosionChance => "skill-explosion",
        StatType.DuplicateChance => "skill-duplicate",
        StatType.TornadoChance => "skill-tornado",
        StatType.BoomerangChance => "skill-scythe",
        StatType.ElectricChance => "skill-electric",
        _ => "skill-unlock"
    };
}
