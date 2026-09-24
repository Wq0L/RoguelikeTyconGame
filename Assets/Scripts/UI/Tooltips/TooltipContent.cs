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
        view.Begin(title, level == "MAX" ? "TAMAMLANDI  ·  MAX" : "KADEME  " + level, icon, skillIcon: skillIcon);
        view.Add(level == "MAX" ? "<b>MEVCUT ETKİ</b>" : "<b>SONRAKİ KADEME</b>");
        view.Add(values.TrimEnd());
        if (level != "MAX") { view.Add("<b>YÜKSELTME MALİYETİ</b>"); view.Add(value); }
        view.End();
    }
}
