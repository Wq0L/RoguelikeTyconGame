using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Stat, tile ve rezonans yazıları (kart, tooltip, liste). Adlar ve birimler çeviri tablosundan (stat.*, rarity.*, resource.*,
// ui.unit.*, ui.resonance.*). Sayılar her dilde nokta ondalıklı (eski biçim korunur: "+16.25%").
public static class TileBuffText
{
    // Stat adı anahtarı: "stat." + enum adı küçük harf ve alt çizgiyle (HarvestDamage → stat.harvest_damage). Bir kez kurulur.
    private static readonly Dictionary<StatType, string> statKeys = BuildKeys<StatType>("stat.");
    private static readonly Dictionary<TileRarity, string> tileRarityKeys = BuildKeys<TileRarity>("rarity.");
    private static readonly Dictionary<PlantRarity, string> plantRarityKeys = BuildKeys<PlantRarity>("rarity.");
    private static readonly Dictionary<ResourceType, string> resourceKeys = BuildKeys<ResourceType>("resource.");

    public static string StatKey(StatType stat) => statKeys.TryGetValue(stat, out string key) ? key : "stat." + Snake(stat.ToString());
    public static string Name(StatType stat) => GameText.Get(StatKey(stat));
    public static string RarityName(TileRarity rarity) => GameText.Get(tileRarityKeys[rarity]);
    public static string RarityName(PlantRarity rarity) => GameText.Get(plantRarityKeys[rarity]);
    public static string ResourceName(ResourceType resource) => GameText.Get(resourceKeys[resource]);

    // "Sv 3" (tr) / "Lv 3" (en).
    public static string Level(int level) => GameText.Format("ui.unit.level_n", level);

    private static Dictionary<T, string> BuildKeys<T>(string prefix) where T : System.Enum
    {
        var keys = new Dictionary<T, string>();
        foreach (T value in System.Enum.GetValues(typeof(T))) keys[value] = prefix + Snake(value.ToString());
        return keys;
    }

    // "HarvestDamage" → "harvest_damage", "XPGainMultiplier" → "xp_gain_multiplier".
    public static string Snake(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool upper = char.IsUpper(c);
            if (upper && i > 0 && (char.IsLower(name[i - 1]) || i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string Signed(float value) => (value >= 0f ? "+" : "−") +
        System.Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture);

    public static string Modifier(StatModifier mod) => Name(mod.statType) + " " + Amount(mod);

    private static string PointsUnit => " " + GameText.Get("ui.unit.points");
    private static string RateUnit => " (" + GameText.Get("ui.unit.rate") + ")";

    public static string Amount(StatModifier mod)
    {
        bool percent = mod.operation == ModifierOperation.AddPercent || mod.operation == ModifierOperation.MorePercent;
        bool chance = mod.statType == StatType.ExplosionChance || mod.statType == StatType.DuplicateChance || mod.statType == StatType.CritChance ||
            mod.statType == StatType.TornadoChance || mod.statType == StatType.BoomerangChance || mod.statType == StatType.ElectricChance;
        string amount = percent || chance ? Signed(mod.value * 100f) + "%" : Signed(mod.value);
        // Crystal is already expressed in percentage points; never multiply its flat value by 100.
        if (!percent && mod.statType == StatType.RareSpawnChance) amount += "%" + PointsUnit;
        if (mod.operation == ModifierOperation.Flat && chance) amount += PointsUnit;
        if (mod.operation == ModifierOperation.Set) amount = mod.value.ToString("0.##", CultureInfo.InvariantCulture) + (chance ? RateUnit : "");
        return amount;
    }

    // Eski değer kırmızı, yeni değer yeşil. Kağıt (tooltip, liste) koyu tonlar ister; kartın
    // koyu çerçeveli yazısı açık tonlar. default: renksiz.
    public readonly struct ChangeColors
    {
        private readonly string oldHex, newHex;
        public ChangeColors(string oldHex, string newHex) { this.oldHex = oldHex; this.newHex = newHex; }
        public string Old(string text) => oldHex == null ? text : $"<color=#{oldHex}>{text}</color>";
        public string New(string text) => newHex == null ? text : $"<color=#{newHex}>{text}</color>";
    }

    public static readonly ChangeColors OnPaper = new ChangeColors("B8322A", "23752F");
    public static readonly ChangeColors OnCard = new ChangeColors("FF8577", "7CF08E");

    // Bu modifier'ların üretim süresi çarpanı (Fertile −%47,5 → 0,525). Üretime dokunmayan tile: 1.
    public static float SpawnFactor(IReadOnlyList<StatModifier> modifiers)
    {
        float factor = 1f;
        if (modifiers != null) foreach (var mod in modifiers)
            if (mod.statType == StatType.PlantSpawnRate && mod.operation == ModifierOperation.MorePercent) factor *= 1f + mod.value;
        return factor;
    }

    // Saksı zaten üretim tabanındayken bu tile'ın hızının dönüştüğü nadirlik.
    public static float SpawnRarityAtFloor(IReadOnlyList<StatModifier> modifiers) =>
        StatCalculator.SpawnOverflowRarity(StatCalculator.MinimumSpawnInterval * SpawnFactor(modifiers));

    public static string Points(float value) => "+" + value.ToString("0.#", CultureInfo.InvariantCulture);

    // "Sv 0 → Sv 1"; kısa hali "Sv 0 → 1" (İngilizcede "Lv").
    public static string LevelChange(int from, int to, ChangeColors colors = default, bool shortForm = false)
    {
        string prefix = GameText.Get("ui.unit.level") + " ";
        return prefix + colors.Old(from.ToString()) + " → " + colors.New((shortForm ? "" : prefix) + to);
    }

    // Seviye atlayan tile: eski ve yeni değer, "+13% → +16.25% puan". Ortak birim sonda bir kez yazılır.
    public static string AmountChange(StatModifier before, StatModifier after, ChangeColors colors = default)
    {
        string from = Amount(before), to = Amount(after);
        foreach (string unit in new[] { PointsUnit, RateUnit })
            if (from.EndsWith(unit) && to.EndsWith(unit)) { from = from.Substring(0, from.Length - unit.Length); break; }
        return colors.Old(from) + " → " + colors.New(to);
    }

    public static string ModifierChanges(IReadOnlyList<StatModifier> before, IReadOnlyList<StatModifier> after, ChangeColors colors = default)
    {
        var text = new StringBuilder();
        if (after != null) for (int i = 0; i < after.Count; i++)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append(before != null && i < before.Count
                ? Name(after[i].statType) + " " + AmountChange(before[i], after[i], colors)
                : Modifier(after[i]));
        }
        return text.ToString();
    }

    public static string Modifiers(IReadOnlyList<StatModifier> modifiers)
    {
        var text = new StringBuilder();
        if (modifiers != null) foreach (var mod in modifiers)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append(Modifier(mod));
        }
        return text.ToString();
    }

    // Davranış adı (rezonans, kart): Patlama / Tornado / Orak / Elektrik.
    public static string BehaviorName(DamageType type) => type switch
    {
        DamageType.Explosion => GameText.Get("ui.behavior.explosion"),
        DamageType.Tornado => GameText.Get("ui.behavior.tornado"),
        DamageType.Boomerang => GameText.Get("ui.behavior.boomerang"),
        DamageType.Electric => GameText.Get("ui.behavior.electric"),
        _ => type.ToString()
    };

    public static string Resonance(ActiveResonance resonance)
    {
        if (resonance.effects == null || resonance.effects.Count == 0)
            return Name(resonance.statType) + " " + Signed((resonance.multiplier - 1f) * 100f) + "%";
        var lines = new List<string>();
        foreach (var effect in resonance.effects)
        {
            string multiplier = "×" + (1f + effect.value).ToString("0.##", CultureInfo.InvariantCulture);
            switch (effect.kind)
            {
                case ResonanceEffectKind.Resources: lines.Add(GameText.Format("ui.resonance.resources", multiplier)); break;
                case ResonanceEffectKind.RareScore: lines.Add(GameText.Format("ui.resonance.rare_score", multiplier)); break;
                case ResonanceEffectKind.ElectricXP: lines.Add(GameText.Format("ui.resonance.electric_xp", multiplier)); break;
                case ResonanceEffectKind.BehaviorDamage:
                    var names = new List<string>();
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Explosion)) != 0) names.Add(BehaviorName(DamageType.Explosion));
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Tornado)) != 0) names.Add(BehaviorName(DamageType.Tornado));
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Boomerang)) != 0) names.Add(BehaviorName(DamageType.Boomerang));
                    if ((resonance.behaviorMask & (1 << (int)DamageType.Electric)) != 0) names.Add(BehaviorName(DamageType.Electric));
                    lines.Add(GameText.Format("ui.resonance.behavior_damage", string.Join(" / ", names), multiplier)); break;
                default:
                    lines.Add(effect.statType == StatType.RareSpawnChance
                        ? GameText.Format("ui.resonance.rarity", effect.value.ToString("0.##", CultureInfo.InvariantCulture))
                        : Name(effect.statType) + " " + (effect.statType == StatType.PlantSpawnRate ? Signed(effect.value * 100f) + "%" : multiplier)); break;
            }
        }
        return string.Join(" · ", lines);
    }
}
