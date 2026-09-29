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
    public void StylePopupText(TMP_Text text)
    {
        if (headingFont != null)
        {
            text.font = headingFont;
            text.fontSharedMaterial = headingFont.material;
        }
        // Cream popup paper needs solid dark glyphs, unlike white outlined button labels.
        text.color = new Color32(54,39,54,255);
        text.fontStyle = FontStyles.Normal;
        text.extraPadding = true;
    }
    // Kodla kurulan paneller için: tema sprite'ı, palet materyalleri, konturlu yazı ve hover hareketi.
    // 64px'ten alçak butonlarda StyleButton'ın yazı boşlukları etiketi gizler.
    public Button CreateButton(Transform parent, string name, string caption, string palette, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.gameObject.layer = go.layer;
        label.rectTransform.SetParent(rect, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16, 10);
        label.rectTransform.offsetMax = new Vector2(-16, -8);
        label.text = caption;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = 34;
        label.raycastTarget = false;
        StyleButton(button, palette);
        go.AddComponent<ComicHoverMotion>().hoverScale = 1.05f;
        return button;
    }
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
