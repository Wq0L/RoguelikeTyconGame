using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipContent : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText, levelText, valueText, costText;
    string title, level, values;
    Sprite icon;
    string skillIcon;
    public void SetIcon(Sprite value) { icon = value; skillIcon = null; }
    public void SetSkillIcon(string value) => skillIcon = value;
    public void SetName(string value) => title = value;
    public void SetLevel(string value) => level = value;
    public void SetValues(string value) => values = value;
    public void SetCost(string value)
    {
        var view = ComicPopupView.Attach(gameObject); view.IsHoverTooltip = true;
        // "MAX" seviye işaretidir (SkillNodeUI); başlık ve bölüm adları çeviri tablosundan.
        bool max = level == MaxLevel;
        view.Begin(title, max ? GameText.Get("ui.skill.completed_max") : GameText.Format("ui.skill.tier", level), icon, skillIcon: skillIcon);
        view.Add($"<b>{GameText.Get(max ? "ui.skill.current_effect" : "ui.skill.next_tier")}</b>");
        view.Add(values.TrimEnd());
        if (!max) { view.Add($"<b>{GameText.Get("ui.skill.upgrade_cost")}</b>"); view.Add(value); }
        view.End();
    }

    public const string MaxLevel = "MAX";
    // Profilin run başında verdiği erişim: satın alma değildir, bu yüzden kademe ve maliyet bölümü yazılmaz.
    public void ShowStartingAccess(string caption, string description)
    {
        var view = ComicPopupView.Attach(gameObject); view.IsHoverTooltip = true;
        view.Begin(title, caption, icon, skillIcon: skillIcon);
        view.Add($"<b>{GameText.Get("ui.skill.access")}</b>");
        view.Add(description.TrimEnd());
        view.End();
    }
}
