using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Round haritasında bu round'un kartının düştüğü tile: kalın sarı çerçeve, içe sönen ışık ve "YENİ" etiketi.
// Kodla çizilir (doku yok). Kendi alt Canvas'ında nabız atar; harita paneli her kare yeniden paketlenmez.
// Unscaled time: round sonunda timeScale 0.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FreshTileRing : MaskableGraphic
{
    public static readonly Color GlowColor = new Color(1f, 0.87f, 0.32f);
    private static readonly Color Ink = new Color32(40, 29, 43, 255);
    private const float Outset = 4f;
    private const float PopDuration = 0.35f;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private float delay, age;

    // Bu round'un kartı: yeni yerleşen tile "YENİ", seviye atlayan tile "+SV".
    public static string LabelFor(GroundCell cell) =>
        ProgressionManager.Instance != null && ProgressionManager.Instance.WasUpgradedThisRound(cell) ? "+SV" : "YENİ";

    public static FreshTileRing Create(Transform cell)
    {
        var go = new GameObject("Fresh Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Canvas), typeof(CanvasGroup));
        go.layer = cell.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(cell, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = -Vector2.one * Outset;
        rect.offsetMax = Vector2.one * Outset;
        Canvas parent = cell.GetComponentInParent<Canvas>();
        if (parent != null) go.GetComponent<Canvas>().additionalShaderChannels = parent.additionalShaderChannels;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;

        var ring = go.AddComponent<FreshTileRing>();
        ring.group = group;
        ring.raycastTarget = false;
        ring.CreateSticker();
        return ring;
    }

    public void Show(float startDelay, string text = "YENİ")
    {
        gameObject.SetActive(true);
        if (label != null) label.text = text;
        delay = startDelay;
        age = 0f;
        Apply(-1f);
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        Apply(age - delay);
    }

    // Önce büyükten oturarak belirir, sonra yavaşça nefes alır. Etiket sabit kalır, sadece çerçeve söner-parlar.
    private void Apply(float t)
    {
        if (t < 0f)
        {
            group.alpha = 0f;
            transform.localScale = Vector3.one;
            return;
        }
        float pop = Mathf.Clamp01(t / PopDuration);
        float breath = pop < 1f ? 0f : 0.5f - 0.5f * Mathf.Cos((t - PopDuration) * 4.4f);
        group.alpha = pop;
        transform.localScale = Vector3.one * (Mathf.LerpUnclamped(1.35f, 1f, OutBack(pop)) + breath * 0.025f);
        canvasRenderer.SetAlpha(1f - breath * 0.35f);
    }

    private static float OutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float unit = Mathf.Min(r.width, r.height);
        if (unit <= 8f) return;
        float radius = unit * 0.14f;
        float ink = 3f, band = Mathf.Max(6f, unit * 0.1f);
        float glow = Mathf.Min(unit * 0.3f, unit * 0.5f - ink - band - 1f);
        Color soft = GlowColor, clear = GlowColor;
        soft.a = 0.6f;
        clear.a = 0f;
        Band(vh, r, radius, 0f, ink, Ink, Ink);
        Band(vh, r, radius, ink, ink + band, GlowColor, GlowColor);
        if (glow > 0f) Band(vh, r, radius, ink + band, ink + band + glow, soft, clear);
    }

    // İki yuvarlak köşeli dikdörtgen arasındaki bant. outerColor dış kenara, innerColor iç kenara (içe sönen ışık).
    private void Band(VertexHelper vh, Rect r, float radius, float outerInset, float innerInset, Color outerColor, Color innerColor)
    {
        const int perCorner = 7; // 0..90° arası 15° adım
        const int count = perCorner * 4;
        int start = vh.currentVertCount;
        AddPath(vh, r, radius, outerInset, outerColor * color);
        AddPath(vh, r, radius, innerInset, innerColor * color);
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            vh.AddTriangle(start + i, start + j, start + count + j);
            vh.AddTriangle(start + i, start + count + j, start + count + i);
        }
    }

    private static void AddPath(VertexHelper vh, Rect r, float radius, float inset, Color vertexColor)
    {
        float round = Mathf.Max(0f, radius - inset);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(
                corner == 0 || corner == 3 ? r.xMax - inset - round : r.xMin + inset + round,
                corner < 2 ? r.yMax - inset - round : r.yMin + inset + round);
            for (int step = 0; step <= 6; step++)
            {
                float angle = (corner * 90 + step * 15) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * round, vertexColor, Vector2.zero);
            }
        }
    }

    // Üst kenardan taşan, hafif eğik çizgi roman etiketi. Hücreyle orantılı: her grid boyutunda okunur.
    // Sadece yukarı taşar: üst satır daha önce çizilir, etiketi örtmez. Sağdaki hücre sonra çizildiği için yana taşmaz.
    private void CreateSticker()
    {
        var sticker = new GameObject("New Sticker", typeof(RectTransform)).GetComponent<RectTransform>();
        sticker.gameObject.layer = gameObject.layer;
        sticker.SetParent(transform, false);
        sticker.anchorMin = new Vector2(0.1f, 0.76f);
        sticker.anchorMax = new Vector2(0.97f, 1.2f);
        sticker.offsetMin = sticker.offsetMax = Vector2.zero;
        sticker.localRotation = Quaternion.Euler(0f, 0f, -8f);
        Plate(sticker, "Shadow", Ink, new Vector2(2f, -3f), 0f);
        Plate(sticker, "Outline", Ink, Vector2.zero, 0f);
        Plate(sticker, "Face", GlowColor, Vector2.zero, 3f);

        label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.gameObject.layer = gameObject.layer;
        label.rectTransform.SetParent(sticker, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(7f, 3f);
        label.rectTransform.offsetMax = new Vector2(-7f, -3f);
        ComicUITheme theme = FeelOverlay.Theme;
        if (theme != null) theme.StylePopupText(label);
        label.text = "YENİ";
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 30f;
        label.raycastTarget = false;
    }

    private static void Plate(RectTransform parent, string name, Color color, Vector2 offset, float inset)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset + Vector2.one * inset;
        rect.offsetMax = offset - Vector2.one * inset;
        var plate = rect.gameObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
    }
}
