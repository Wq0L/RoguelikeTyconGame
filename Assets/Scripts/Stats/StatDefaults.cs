public static class StatDefaults
{
    public static float GetDefaultBase(StatType statType)
    {
        switch (statType)
        {
            // === HASAT ===
            case StatType.HarvestDamage:
                return 1f;

            case StatType.AreaRadius:
                return 1f;

            case StatType.AttackSpeed:
                return 1f;

            case StatType.CritChance:
                return 0f;

            case StatType.CritMultiplier:
                return 2f;

            // === ÜRETİM ===
            case StatType.PlantSpawnRate:
                return 5f;

            case StatType.RareSpawnChance:
                return 0f;

            // === EKONOMİ ===
            case StatType.GoldGainMultiplier:
                return 1f;

            case StatType.IronGainMultiplier:
                return 1f;

            case StatType.StoneGainMultiplier:
                return 1f;

            case StatType.XPGainMultiplier:
                return 1f;

            case StatType.HarvestScoreMultiplier:
                return 1f;

            // === META / SKILL TREE ===
            case StatType.MutationLuck:
                return 0f;

            case StatType.GridUnlockSize:
                return 3f;

            case StatType.RoundDuration:
                return 10f;

            case StatType.StartingGoldBonus:
                return 0f;

            case StatType.StartingPlanterCount:
                return 0f;

            // === KART AJANSI ===
            case StatType.ExtraCardChoice:
                return 0f;

            case StatType.RerollCard:
                return 0f;

            case StatType.CardSkip:
                return 0f;
            case StatType.ExplosionChance:
                return 0f; 
            case StatType.DuplicateChance:
                return 0f;

            default:
                return 0f;
        }
    }
}