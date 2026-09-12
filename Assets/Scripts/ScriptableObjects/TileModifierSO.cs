using System.Collections.Generic;
using UnityEngine;

public enum TileModifierType
{
    Fertile,
    Water,
    Crystal,
    Energy,
    Explosive,
    Duplicate
}

public enum TileRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum TileBehavior
{
    None,
    Explosive,
    Duplicate
}

[System.Serializable]
public class StatModifierRange
{
    public StatType statType;
    public StatTarget target;
    public ModifierOperation operation;
    public float minValue;
    public float maxValue;
}

[CreateAssetMenu(menuName = "Game/TileModifier")]
public class TileModifierSO : ScriptableObject
{
    [Header("Bilgi")]
    public string modifierName;
    public TileModifierType modifierType;
    public TileRarity rarity;
    public Color tileColor = Color.white;

    [Header("Kart Havuzu Kilidi")]
    [Tooltip("None: başlangıçtan itibaren havuzda. Diğerleri: skill tree bu kilidi açınca havuza girer.")]
    public UnlockType requiredUnlock = UnlockType.None;

    public bool IsAvailableInCardPool => requiredUnlock == UnlockType.None ||
        (UnlockManager.Instance != null && UnlockManager.Instance.IsUnlocked(requiredUnlock));

    [Header("Davranış")]
    public TileBehavior behavior = TileBehavior.None;

    [Header("Stat Etkileri")]
    public List<StatModifierRange> modifierRanges = new();

    public List<StatModifier> RollModifiers()
    {
        List<StatModifier> rolled = new();
        foreach (StatModifierRange range in modifierRanges)
        {
            rolled.Add(new StatModifier
            {
                statType = range.statType,
                target = range.target,
                operation = range.operation,
                value = Random.Range(range.minValue, range.maxValue)
            });
        }
        return rolled;
    }
}
