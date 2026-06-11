using UnityEngine;

public enum TileModifierType
{
    Fertile,  // reward x2
    Water,    // spawn hızı x2
    Crystal,  // rare chance +50%
    Energy,   // komşulara buff
    Golden    // devasa global bonus
}

[CreateAssetMenu(menuName = "Game/TileModifier")]
public class TileModifierSO : ScriptableObject
{
    [Header("Bilgi")]
    public string modifierName;
    public TileModifierType modifierType;
    public Color tileColor;

    [Header("Stat Etkisi")]
    public StatType statType;
    public ModifierOperation operation;
    public float value;
}