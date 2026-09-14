using System.Collections.Generic;

// Each displayed slot owns its roll, even when two slots offer the same asset.
public sealed class TileCardOffer
{
    public TileModifierSO Tile { get; }
    public IReadOnlyList<StatModifier> Modifiers { get; }
    public TileCardOffer(TileModifierSO tile)
    {
        Tile = tile;
        Modifiers = tile.RollModifiers().AsReadOnly();
    }
}
