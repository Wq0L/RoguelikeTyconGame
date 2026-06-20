public enum StatType
{
    // === HASAT ===
    HarvestDamage,
    AreaRadius,
    AttackSpeed,
    CritChance,
    CritMultiplier,

    // === ÜRETİM ===
    PlantSpawnRate,
    RareSpawnChance,

    // === EKONOMİ ===
    GoldGainMultiplier,
    IronGainMultiplier,
    StoneGainMultiplier,
    XPGainMultiplier,
    HarvestScoreMultiplier,

    // === META / SKILL TREE ===
    MutationLuck,
    GridUnlockSize,
    RoundDuration,
    StartingGoldBonus,
    StartingPlanterCount,

    // === KART AJANSI ===
    ExtraCardChoice,
    RerollCard,
    CardSkip,           // kartı atla, resource al
    // === DAVRANIŞ ===
    ExplosionChance,    // patlama tetikleme şansı (0-1)
    DuplicateChance   // hasat ödülünü 2x sayma şansı (0-1)
}