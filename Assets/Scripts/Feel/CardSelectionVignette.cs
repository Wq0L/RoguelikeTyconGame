using UnityEngine;
using UnityEngine.UI;

// Kart seçimi açıkken ekranın köşelerinden içeri kararan vignette: göz ortadaki kartlara gider.
// Post-processing kapalı olduğu için UI'da, kart panelinin en arkasında çizilir; panelle açılır, panelle kapanır.
// Unscaled time kullanır (kart seçiminde oyun durur).
public sealed class CardSelectionVignette : MonoBehaviour
{
    private const float FadeDuration = 0.3f;
    private const float MaxAlpha = 0.85f;
    // Kararma merkezden bu mesafede başlar, köşede (1.41) tamamlanır. 1 = ekran kenarının ortası.
    private const float InnerRadius = 0.45f;
    private const float OuterRadius = 1.35f;

    private static Texture2D texture;
    private RawImage image;
    private float elapsed;

    public static CardSelectionVignette Attach(RectTransform panel)
    {
        var existing = panel.GetComponentInChildren<CardSelectionVignette>(true);
        if (existing != null) return existing;
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
        return go.AddComponent<CardSelectionVignette>();
    }

    private void Awake() => image = GetComponent<RawImage>();

    private void OnEnable()
    {
        elapsed = 0f;
        if (image != null) image.color = new Color(1f, 1f, 1f, 0f);
    }

    private void Update()
    {
        if (elapsed >= FadeDuration) return;
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / FadeDuration);
        image.color = new Color(1f, 1f, 1f, MaxAlpha * (1f - (1f - t) * (1f - t)));
    }

    // Ekrana gerilen tek doku: ortası şeffaf, köşelere doğru koyu mürekkep.
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
            var ink = new Color32(14, 10, 24, 0);
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
