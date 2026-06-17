public enum ModifierOperation
{
    Flat,        // Direkt ekler: +5 damage, +0.10 rare chance
    AddPercent,  // Yüzde bonusları toplar: +%10 = 0.10f
    MorePercent, // Ayrı çarpan: +%10 = x1.10
    Set          // Direkt değeri setler
}

public enum StatTarget
{
    All,
    Player,
    Planter,
    Round,
    Grid,
    Mutation
}

[System.Serializable]
public struct StatModifier
{
    public StatType statType;
    public StatTarget target;
    public ModifierOperation operation;
    public float value;
}