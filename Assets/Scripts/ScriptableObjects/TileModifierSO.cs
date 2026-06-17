using System.Collections.Generic;
using UnityEngine;

public enum TileModifierType
{
    Fertile,
    Water,
    Crystal,
    Energy,
    Golden
}

[CreateAssetMenu(menuName = "Game/TileModifier")]
public class TileModifierSO : ScriptableObject
{
    [Header("Bilgi")]
    public string modifierName;
    public TileModifierType modifierType;
    public Color tileColor = Color.white;

    [Header("Stat Etkileri")]
    public List<StatModifier> modifiers = new();

    // [Header("Özel Davranış")]
    // // public bool affectsNeighbors;
    // // public int neighborRange = 1;
}