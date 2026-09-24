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
        view.Begin(cell.CurrentModifier.modifierName, cell.CurrentModifier.rarity.ToString());
        view.Add("<b>BU TILE</b>"); view.Add(TileBuffText.Modifiers(cell.RolledModifiers));
        if (cell.Planter != null && cell.Planter.ActiveResonances.Count > 0)
        {
            view.Add("<b>SAKSININ REZONANSLARI</b>");
            foreach (var resonance in cell.Planter.ActiveResonances)
                view.Add("<b>" + resonance.resonanceName + "</b>\n" + TileBuffText.Resonance(resonance), ResonanceManager.Identity(resonance));
        }
        view.End();
    }
}
