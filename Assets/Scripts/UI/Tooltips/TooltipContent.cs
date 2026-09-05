using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipContent : MonoBehaviour
{
    [SerializeField] private Image iconImage;      // şimdilik boş, ileride node.icon
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text valueText;  
    [SerializeField] private TMP_Text costText;

    public void SetIcon(Sprite icon)
    {
        if (iconImage == null) return;

        if (icon != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;  // ikon yoksa gizle
        }
    }

    public void SetName(string nodeName)
    {
        nameText.text = nodeName;
    }

    public void SetLevel(string levelStr)
    {
        levelText.text = levelStr;
    }

    // Tüm effect satırlarını tek metinde ver (FillTooltip hazırlayıp yollar)
    public void SetValues(string valuesText)
    {
        valueText.text = valuesText;
    }

    public void SetCost(string costStr)
    {
        costText.text = costStr;
    }
}