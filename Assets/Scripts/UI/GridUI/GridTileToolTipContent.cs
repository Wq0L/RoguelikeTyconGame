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

    // Haritadaki tooltip ve round önizlemesi aynı içeriği gösterir: tile'ın buff'ları + üstündeki saksının rezonansları.
    public static void Fill(ComicPopupView view, GroundCell cell, string coordinate = null)
    {
        var modifier = cell.CurrentModifier;
        bool fresh = ProgressionManager.Instance != null && ProgressionManager.Instance.WasAppliedThisRound(cell);
        string caption = modifier != null ? modifier.rarity.ToString() : "";
        if (!string.IsNullOrEmpty(coordinate)) caption = caption.Length > 0 ? coordinate + " · " + caption : coordinate;
        if (modifier != null && cell.Level > 0) caption += $" · Sv {cell.Level}/{GroundCell.MaxLevel}";
        bool upgraded = ProgressionManager.Instance != null && ProgressionManager.Instance.WasUpgradedThisRound(cell);
        if (fresh) caption += upgraded ? " · +SV" : " · YENİ";
        view.Begin(modifier != null ? modifier.modifierName : "Boş tile", caption);
        if (fresh) view.Add(upgraded ? "<b>Bu round'un kartı bu tile'ı yükseltti.</b>" : "<b>Bu round'un kartı buraya geldi.</b>");
        if (modifier != null) { view.Add("<b>BU TILE</b>"); view.Add(TileBuffText.Modifiers(cell.RolledModifiers)); }
        if (cell.Planter != null && cell.Planter.ActiveResonances.Count > 0)
        {
            view.Add("<b>SAKSININ REZONANSLARI</b>");
            foreach (var resonance in cell.Planter.ActiveResonances)
                view.Add("<b>" + resonance.resonanceName + "</b>\n" + TileBuffText.Resonance(resonance), ResonanceManager.Identity(resonance));
        }
        view.End();
    }
}
