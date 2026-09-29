using System.Collections.Generic;

// Each displayed slot owns its roll, even when two slots offer the same asset.
// Üç tür teklif: yeni tile (grid'de açık boş hücre varken), tile yükseltme (grid doluyken)
// ve Temel güç (yükseltilecek tile da kalmadıysa, kalıcı global stat).
public sealed class TileCardOffer
{
    public TileModifierSO Tile { get; }
    public IReadOnlyList<StatModifier> Modifiers { get; }
    public TileRarity Rarity { get; }
    public GroundCell UpgradeTarget { get; }
    public int LevelGain { get; }
    public string BaseStatName { get; }
    public bool IsUpgrade => UpgradeTarget != null;
    public bool IsBaseStat => Tile == null;

    public TileCardOffer(TileModifierSO tile)
    {
        Tile = tile;
        Rarity = tile.rarity;
        Modifiers = tile.RollModifiers().AsReadOnly();
    }

    private TileCardOffer(TileModifierSO tile, TileRarity rarity, IReadOnlyList<StatModifier> modifiers, GroundCell target, int levelGain, string baseStatName)
    {
        Tile = tile;
        Rarity = rarity;
        Modifiers = modifiers;
        UpgradeTarget = target;
        LevelGain = levelGain;
        BaseStatName = baseStatName;
    }

    // Modifiers: yükseltme sonrası değerler (önizleme).
    public static TileCardOffer Upgrade(GroundCell target, TileRarity rarity)
    {
        int gain = System.Math.Min(GroundCell.LevelsFor(rarity), GroundCell.MaxLevel - target.Level);
        return new TileCardOffer(target.CurrentModifier, rarity, target.PreviewLevels(gain).AsReadOnly(), target, gain, null);
    }

    public static TileCardOffer BaseStat(string name, TileRarity rarity, List<StatModifier> modifiers) =>
        new TileCardOffer(null, rarity, modifiers.AsReadOnly(), null, 0, name);
}
