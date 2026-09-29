using UnityEngine;
using UnityEngine.UI;

// Tam ekran kısa flaş. İlk kullanımda kendi overlay canvas'ını kurar; flaş yokken image kapalıdır
// (boşta overdraw maliyeti yok). Unscaled time kullanır.
public sealed class ScreenFlash : MonoBehaviour
{
    private static ScreenFlash instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private Image image;
    private Color color;
    private float peakAlpha, duration, elapsed;

    public static void Play(Color color, float peakAlpha = 0.25f, float duration = 0.25f)
    {
        if (peakAlpha <= 0f || duration <= 0f || Application.isBatchMode || !GameSettings.ScreenFlashes) return;
        if (instance == null) Create();
        instance.Begin(color, peakAlpha, duration);
    }

    private static void Create()
    {
        RectTransform root = FeelOverlay.CreateCanvas("Screen Flash", 500);
        var flash = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)flash.transform;
        rect.SetParent(root, false);
        FeelOverlay.Stretch(rect);

        instance = root.gameObject.AddComponent<ScreenFlash>();
        instance.image = flash.GetComponent<Image>();
        instance.image.raycastTarget = false;
        instance.image.enabled = false;
    }

    private void Begin(Color flashColor, float alpha, float time)
    {
        color = flashColor;
        peakAlpha = image.enabled ? Mathf.Max(alpha, image.color.a) : alpha;
        duration = time;
        elapsed = 0f;
        image.enabled = true;
        Apply();
    }

    private void Update()
    {
        if (!image.enabled) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            image.enabled = false;
            return;
        }
        Apply();
    }

    private void Apply()
    {
        float t = 1f - Mathf.Clamp01(elapsed / duration);
        image.color = new Color(color.r, color.g, color.b, peakAlpha * t * t);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
