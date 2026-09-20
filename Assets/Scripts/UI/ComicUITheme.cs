using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ComicUITheme : ScriptableObject
{
    public Sprite buttonSprite, cardSprite, coinSprite, sproutSprite, ironSprite, stoneSprite;
    public Sprite ResourceIcon(ResourceType type) => type == ResourceType.Gold ? coinSprite : type == ResourceType.Iron ? ironSprite : stoneSprite;
    public TMP_FontAsset headingFont, bodyFont;
    public Material outlinedText;
    public Material[] green, red, blue, gold;
    public void StyleButton(Button button, string palette = "green")
    {
        var image = button.targetGraphic as Image;
        if (!image) return;
        image.sprite = buttonSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4;
        image.color = Color.white;
        button.transition = Selectable.Transition.None;
        var visual = button.GetComponent<ComicButtonVisual>() ?? button.gameObject.AddComponent<ComicButtonVisual>();
        var mats = palette == "red" ? red : palette == "blue" ? blue : palette == "gold" ? gold : green;
        visual.normal = mats[0]; visual.hover = mats[1]; visual.pressed = mats[2]; visual.disabled = mats[3];
        image.material = mats[0];
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = headingFont; text.fontSharedMaterial = outlinedText;
            text.color = Color.white; text.fontStyle = FontStyles.Normal;
            text.margin = new Vector4(8, 4, 8, 8);
        }
    }
}
