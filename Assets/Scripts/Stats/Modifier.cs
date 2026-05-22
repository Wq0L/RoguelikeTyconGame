public enum ModifierOperation
{
    Add,
    Multiply,
    Set
}

[System.Serializable]
public class StatModifier
{
    public StatType statType;
    public ModifierOperation operation;
    public float value;
}