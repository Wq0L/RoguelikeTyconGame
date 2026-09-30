using UnityEngine;
using TMPro;

public class GridTileToolTipContent : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText, rarityText, valueText;
    string title, rarity;
    public void SetName(string value) => title = value;
    public void SetRarity(string value) => rarity = value;
    public void SetValues(string values)
    {
        var view = ComicPopupView.Attach(gameObject); view.IsHoverTooltip = true;
        view.Begin(title, rarity); view.Add(values); view.End();
    }
    public void SetCell(GroundCell cell)
    {
        var view = ComicPopupView.Attach(gameObject); view.IsHoverTooltip = true;
        Fill(view, cell);
    }

    // Bölüm başlıkları gövde yazısından ayrışsın: koyu kehribar, gövde mürekkep rengi.
    private const string SectionColor = "9A5B12";
    // Rezonans adı mürekkep, etkisi bir ton açık: ad ve etki satırı birbirinden ayrılır.
    private const string EffectColor = "6E5F5A";
    private static string Section(string title) => $"<color=#{SectionColor}><b>{title}</b></color>";

    // Haritadaki tooltip ve round önizlemesi aynı içeriği gösterir: tile'ın buff'ları + üstündeki saksının rezonansları.
    public static void Fill(ComicPopupView view, GroundCell cell, string coordinate = null)
    {
        var modifier = cell.CurrentModifier;
        bool fresh = ProgressionManager.Instance != null && ProgressionManager.Instance.WasAppliedThisRound(cell);
        string caption = modifier != null ? modifier.rarity.ToString() : "";
        if (!string.IsNullOrEmpty(coordinate)) caption = caption.Length > 0 ? coordinate + " · " + caption : coordinate;
        if (modifier != null) caption += $" · Sv {cell.Level}/{GroundCell.MaxLevel}";
        ProgressionManager.TileUpgrade upgrade = null;
        bool upgraded = ProgressionManager.Instance != null && ProgressionManager.Instance.TryGetRoundUpgrade(cell, out upgrade);
        if (fresh) caption += upgraded ? " · +SV" : " · YENİ";
        view.Begin(modifier != null ? modifier.modifierName : "Boş tile", caption);
        var colors = TileBuffText.OnPaper;
        if (fresh) view.Add(colors.New(upgraded ? "<b>Bu round'un kartı bu tile'ı yükseltti.</b>" : "<b>Bu round'un kartı buraya geldi.</b>"));
        if (modifier != null && upgraded)
        {
            // Başlık aynı zamanda renk anahtarı: önce kırmızı, şimdi yeşil.
            view.Add(Section("BU TILE · ") + $"<b>{colors.Old("önce")} → {colors.New("şimdi")}</b>");
            view.Add(TileBuffText.LevelChange(upgrade.FromLevel, upgrade.ToLevel, colors) + $" · +{upgrade.ToLevel - upgrade.FromLevel} seviye");
            view.Add(TileBuffText.ModifierChanges(upgrade.Before, cell.RolledModifiers, colors));
        }
        else if (modifier != null) { view.Add(Section("BU TILE")); view.Add(TileBuffText.Modifiers(cell.RolledModifiers)); }
        // Üretim tabanda: bu saksıda fazla hız nadir bitki şansına dönüştü.
        if (modifier != null && cell.Planter != null && TileBuffText.SpawnFactor(cell.RolledModifiers) < 1f)
        {
            float overflow = cell.Planter.GetSpawnOverflowRarity();
            if (overflow >= 0.05f)
                view.Add(colors.New($"Üretim tabanda ({StatCalculator.MinimumSpawnInterval.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} sn): fazlası nadirliğe · saksıya {TileBuffText.Points(overflow)} nadirlik"));
        }
        if (cell.Planter != null && cell.Planter.ActiveResonances.Count > 0)
        {
            view.Add(Section("SAKSININ REZONANSLARI"));
            foreach (var resonance in cell.Planter.ActiveResonances)
                view.Add("<b>" + resonance.resonanceName + "</b>\n" + $"<color=#{EffectColor}>" + TileBuffText.Resonance(resonance) + "</color>", ResonanceManager.Identity(resonance));
        }
        view.End();
    }
}
