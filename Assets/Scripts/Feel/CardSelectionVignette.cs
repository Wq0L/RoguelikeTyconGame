using UnityEngine;
using UnityEngine.UI;

// Kart seçimi açıkken renkli köşe vignette'i: grid altın sarısı, boss ödülü mor.
// Post-processing kapalı olduğu için UI'da, kart panelinin en arkasında çizilir; panelle açılır, panelle kapanır.
// Unscaled time kullanır (kart seçiminde oyun durur).
public sealed class CardSelectionVignette : MonoBehaviour
{
    private const float FadeDuration = 0.3f;
    private const float MaxAlpha = 0.65f;
    public enum Palette { Grid, Boss }
    private Color tint = new Color32(235, 177, 52, 255);
    // Kararma merkezden bu mesafede başlar, köşede (1.41) tamamlanır. 1 = ekran kenarının ortası.
    private const float InnerRadius = 0.45f;
    private const float OuterRadius = 1.35f;

    private static Texture2D texture;
    private RawImage image;
    private float elapsed;

    public static CardSelectionVignette Attach(RectTransform panel, Palette palette = Palette.Grid)
    {
        var existing = panel.GetComponentInChildren<CardSelectionVignette>(true);
        if (existing != null) { existing.SetPalette(palette); return existing; }
        var go = new GameObject("Card Vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
        go.layer = panel.gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)go.transform;
        rect.SetParent(panel, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var raw = go.GetComponent<RawImage>();
        raw.texture = Texture;
        raw.color = new Color(1f, 1f, 1f, 0f);
        raw.raycastTarget = false;
        var vignette = go.AddComponent<CardSelectionVignette>();
        vignette.SetPalette(palette);
        return vignette;
    }

    private void SetPalette(Palette palette)
    {
        tint = palette == Palette.Boss ? new Color32(153, 87, 224, 255) : new Color32(235, 177, 52, 255);
        if (image == null) image = GetComponent<RawImage>();
        ApplyFade();
    }

    private void ApplyFade()
    {
        float t = Mathf.Clamp01(elapsed / FadeDuration);
        Color color = tint;
        color.a = MaxAlpha * (1f - (1f - t) * (1f - t));
        image.color = color;
    }

    private void Awake() => image = GetComponent<RawImage>();

    private void OnEnable()
    {
        elapsed = 0f;
        if (image != null) ApplyFade();
    }

    private void Update()
    {
        if (elapsed >= FadeDuration) return;
        elapsed += Time.unscaledDeltaTime;
        ApplyFade();
    }

    // Ortası şeffaf beyaz maske; renk RawImage üzerinden verilir, doku ortaktır.
    private static Texture2D Texture
    {
        get
        {
            if (texture != null) return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Card Vignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            var ink = new Color32(255, 255, 255, 0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float t = Mathf.InverseLerp(InnerRadius, OuterRadius, Mathf.Sqrt(u * u + v * v));
                ink.a = (byte)Mathf.RoundToInt(t * t * (3f - 2f * t) * 255f);
                pixels[y * size + x] = ink;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
