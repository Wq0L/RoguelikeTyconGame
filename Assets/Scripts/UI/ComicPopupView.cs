using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shared, pooled presentation for tile, skill and placement information. Never changes gameplay data.
public sealed class ComicPopupView : MonoBehaviour
{
    public static readonly Color Ink = new Color32(54, 39, 54, 255);
    readonly List<TMP_Text> lines = new();
    readonly List<ResonanceBadgeGraphic> icons = new();
    RectTransform root, viewport, content;
    TMP_Text heading, subtitle, footer;
    UnityEngine.UI.Image portrait;
    ResonanceBadgeGraphic skillPortrait;
    ComicUITheme theme;
    int used;
    float contentHeight, scrollOffset;
    string hint = "";
    Rect fittedBounds;
    float fittedScale;
    bool dirty = true;
    public bool CanScroll => viewport != null && contentHeight > viewport.rect.height + 1;
    public int RowCount => used;
    public float ContentHeight => contentHeight;
    public bool IsHoverTooltip { get; set; }
    public static ComicPopupView Attach(GameObject host)
    {
        var existing = host.GetComponent<ComicPopupView>();
        if (existing != null) return existing;
        // Keep prefab references intact; replace only their presentation on this pooled instance.
        foreach (Transform child in host.transform) child.gameObject.SetActive(false);
        foreach (var graphic in host.GetComponents<UnityEngine.UI.Graphic>()) graphic.enabled = false;
        return host.AddComponent<ComicPopupView>();
    }
    RectTransform Rect(string label, Transform parent)
    {
        var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); return rect;
    }
    void Box(string label, Color color, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var rect = Rect(label, root); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = low; rect.offsetMax = high;
        var graphic = rect.gameObject.AddComponent<ComicPopupPlate>(); graphic.color = color; graphic.raycastTarget = false;
    }
    TMP_Text Text(string label, Transform parent, float size, bool title = false)
    {
        var rect = Rect(label, parent);
        rect.gameObject.SetActive(false);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size; text.color = Ink; text.raycastTarget = false;
        if (theme != null) theme.StylePopupText(text);
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
        rect.gameObject.SetActive(true);
        return text;
    }
    void Awake()
    {
        root = (RectTransform)transform; theme = Resources.Load<ComicUITheme>("ComicUITheme");
        Box("Ink shadow", new Color32(40, 29, 43, 220), Vector2.zero, Vector2.one, new Vector2(6,-7), new Vector2(6,-7));
        Box("Ink outline", Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Box("Cream paper", new Color32(255,242,210,255), Vector2.zero, Vector2.one, new Vector2(4,4), new Vector2(-4,-4));
        Box("Amber header", new Color32(247,192,93,255), new Vector2(0,1), Vector2.one, new Vector2(7,-80), new Vector2(-7,-7));
        heading = Text("Heading", root, 28, true); subtitle = Text("Subtitle", root, 20);
        var imageRect = Rect("Portrait", root); imageRect.anchoredPosition = new Vector2(18,-18); imageRect.sizeDelta = new Vector2(48,48);
        portrait = imageRect.gameObject.AddComponent<UnityEngine.UI.Image>(); portrait.preserveAspect = true; portrait.raycastTarget = false; portrait.enabled = false;
        var badgeRect = Rect("Skill portrait", root); badgeRect.anchoredPosition = new Vector2(16,-16); badgeRect.sizeDelta = new Vector2(54,54);
        skillPortrait = badgeRect.gameObject.AddComponent<ResonanceBadgeGraphic>(); skillPortrait.raycastTarget = false; skillPortrait.gameObject.SetActive(false);
        viewport = Rect("Viewport", root); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        content = Rect("Content", viewport);
        footer = Text("Footer", root, 19);
    }
    public void Begin(string title, string caption = "", Sprite icon = null, string footerHint = "", string skillIcon = null)
    {
        used = 0; scrollOffset = 0;
        heading.text = (title ?? "").Normalize(System.Text.NormalizationForm.FormC);
        subtitle.text = (caption ?? "").Normalize(System.Text.NormalizationForm.FormC);
        bool hasBadge = !string.IsNullOrEmpty(skillIcon);
        portrait.sprite = icon; portrait.enabled = icon != null && !hasBadge; hint = footerHint;
        skillPortrait.gameObject.SetActive(hasBadge);
        if (hasBadge) skillPortrait.SetRecipe(skillIcon);
    }
    public void Add(string text, string recipe = null)
    {
        if (used == lines.Count)
        {
            lines.Add(Text("Entry " + used, content, 24));
            var icon = Rect("Badge " + used, content).gameObject.AddComponent<ResonanceBadgeGraphic>();
            icon.raycastTarget = false; icons.Add(icon);
        }
        lines[used].gameObject.SetActive(true); lines[used].text = (text ?? "").Normalize(System.Text.NormalizationForm.FormC).Replace("→", "<font=\"LiberationSans SDF\">→</font>");
        icons[used].gameObject.SetActive(!string.IsNullOrEmpty(recipe));
        if (!string.IsNullOrEmpty(recipe)) icons[used].SetRecipe(recipe);
        used++;
    }
    public void End()
    {
        for (int i = used; i < lines.Count; i++) { lines[i].gameObject.SetActive(false); icons[i].gameObject.SetActive(false); }
        dirty = true; Fit();
    }
    public void Fit()
    {
        var canvas = GetComponentInParent<Canvas>(); float scale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
        Rect bounds = Screen.safeArea;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
        {
            Rect c = canvas.worldCamera.pixelRect;
            bounds = UnityEngine.Rect.MinMaxRect(Mathf.Max(bounds.xMin,c.xMin), Mathf.Max(bounds.yMin,c.yMin), Mathf.Min(bounds.xMax,c.xMax), Mathf.Min(bounds.yMax,c.yMax));
        }
        if (!dirty && fittedBounds == bounds && Mathf.Approximately(fittedScale,scale)) return;
        dirty = false; fittedBounds = bounds; fittedScale = scale;
        float width = Mathf.Min(440, Mathf.Max(100, (bounds.width - 40) / scale));
        float titleX = portrait.enabled || skillPortrait.gameObject.activeSelf ? 82 : 22;
        heading.rectTransform.anchoredPosition = new Vector2(titleX,-15);
        heading.rectTransform.sizeDelta = new Vector2(width-titleX-20, 64);
        float headingHeight = heading.GetPreferredValues(heading.text, width-titleX-20, 0).y;
        float top = Mathf.Max(88, headingHeight + 52);
        subtitle.rectTransform.anchoredPosition = new Vector2(titleX,-22-headingHeight);
        subtitle.rectTransform.sizeDelta = new Vector2(width-titleX-20,26);
        var header = (RectTransform)transform.Find("Amber header"); header.offsetMin = new Vector2(7,-top+8);
        float y = 4;
        for (int i=0;i<used;i++)
        {
            bool badge = icons[i].gameObject.activeSelf; float x = badge ? 58 : 4;
            float textWidth = width - 44 - x;
            float h = Mathf.Max(badge ? 48 : 24, lines[i].GetPreferredValues(lines[i].text,textWidth,0).y);
            lines[i].rectTransform.anchoredPosition = new Vector2(x,-y);
            lines[i].rectTransform.sizeDelta = new Vector2(textWidth,h);
            icons[i].rectTransform.anchoredPosition = new Vector2(0,-y); icons[i].rectTransform.sizeDelta = new Vector2(46,46);
            y += h + 14;
        }
        contentHeight = y;
        float height = Mathf.Min(top + contentHeight + 44, Mathf.Max(top+64,(bounds.height-40)/scale));
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        viewport.anchoredPosition = new Vector2(20,-top); viewport.sizeDelta = new Vector2(width-40,Mathf.Max(20,height-top-38));
        content.sizeDelta = new Vector2(width-40,contentHeight);
        footer.rectTransform.anchoredPosition = new Vector2(22,-height+29); footer.rectTransform.sizeDelta = new Vector2(width-44,24);
        footer.text = hint + (CanScroll ? (hint.Length > 0 ? "  ·  " : "") + "PgUp / PgDn: kaydır" : "");
        Scroll(0);
    }
    public void Scroll(float amount)
    {
        scrollOffset = Mathf.Clamp(scrollOffset+amount,0,Mathf.Max(0,contentHeight-viewport.rect.height));
        content.anchoredPosition = new Vector2(0,scrollOffset);
    }
    void Update()
    {
        if (!CanScroll) return;
        if (Input.GetKeyDown(KeyCode.PageDown)) Scroll(viewport.rect.height * .75f);
        if (Input.GetKeyDown(KeyCode.PageUp)) Scroll(-viewport.rect.height * .75f);
    }
}

// Rounded paper geometry stays crisp at any Canvas scale; no runtime texture allocations.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ComicPopupPlate : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear(); Rect r = rectTransform.rect; float radius = Mathf.Min(14,Mathf.Min(r.width,r.height)*.5f);
        vh.AddVert(r.center,color,Vector2.zero);
        for(int corner=0;corner<4;corner++)
        {
            Vector2 center = new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius, corner<2?r.yMax-radius:r.yMin+radius);
            for(int step=0;step<=6;step++)
            {
                float angle=(corner*90+step*15)*Mathf.Deg2Rad;
                vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
            }
        }
        for(int i=1;i<=28;i++) vh.AddTriangle(0,i,i==28?1:i+1);
    }
}

