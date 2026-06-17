public static class StatDefaults
{
    public static float GetDefaultBase(StatType statType)
    {
        switch (statType)
        {
            case StatType.HarvestDamage:
                return 1f;

            case StatType.AreaRadius:
                return 1f;

            case StatType.AttackSpeed:
                return 1f;

            case StatType.SeedGainMultiplier:
                return 1f;

            case StatType.CoreSeedGainMultiplier:
                return 1f;

            case StatType.XPGainMultiplier:
                return 1f;

            case StatType.HarvestScoreMultiplier:
                return 1f;

            case StatType.RareSpawnChance:
                return 0f;

            case StatType.PlantSpawnRate:
                return 5f;

            case StatType.RoundDuration:
                return 30f;

            case StatType.MutationLuck:
                return 0f;

            case StatType.GridUnlockSize:
                return 3f;

            case StatType.RefundBonus:
                return 0f;

            default:
                return 0f;
        }
    }
}