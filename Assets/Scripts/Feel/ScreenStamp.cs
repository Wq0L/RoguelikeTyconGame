using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Ekrana "damga" gibi inen büyük yazı: büyükten küçülerek oturur, kısa bekler, yukarı süzülüp kaybolur.
// Her key için tek slot: aynı key tekrar gelirse (arka arkaya level up) yazı güncellenip yeniden vurur.
// Tıklanmaz, unscaled time kullanır.
public sealed class ScreenStamp : MonoBehaviour
{
    private const float InTime = 0.24f;
    private const float OutTime = 0.3f;

    private sealed class Slot
    {
        public string key;
        public RectTransform root;
        public TextMeshProUGUI title, subtitle;
        public Color color;
        public float y, hold, elapsed, tilt;
        public bool active;
    }

    private static ScreenStamp instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;
    private readonly List<Slot> slots = new();
    private RectTransform canvasRoot;

    public static void Show(string key, string title, string subtitle, Color color, float y, float hold = 0.6f)
    {
        if (Application.isBatchMode) return;
        if (instance == null) Create();
        instance.Present(key, title, subtitle, color, y, hold);
    }

    private static void Create()
    {
        RectTransform root = FeelOverlay.CreateCanvas("Screen Stamp", 480);
        instance = root.gameObject.AddComponent<ScreenStamp>();
        instance.canvasRoot = root;
    }

    private void Present(string key, string title, string subtitle, Color color, float y, float hold)
    {
        Slot slot = slots.Find(s => s.key == key) ?? CreateSlot(key);
        slot.title.text = title;
        slot.subtitle.text = subtitle ?? "";
        slot.subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        slot.color = color;
        slot.y = y;
        slot.hold = hold;
        // Zaten ekrandaysa baştan girmek yerine küçük bir vuruşla yenilenir.
        slot.elapsed = slot.active && slot.elapsed < InTime + slot.hold ? InTime * 0.45f : 0f;
        slot.tilt = slots.IndexOf(slot) % 2 == 0 ? -5f : 5f;
        slot.active = true;
        slot.root.gameObject.SetActive(true);
        Apply(slot);
    }

    private Slot CreateSlot(string key)
    {
        var rootObject = new GameObject("Stamp " + key, typeof(RectTransform));
        var root = (RectTransform)rootObject.transform;
        root.SetParent(canvasRoot, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(1400f, 260f);

        var slot = new Slot
        {
            key = key,
            root = root,
            title = CreateText(root, "Title", 118f, new Vector2(0f, 30f), new Vector2(1400f, 150f)),
            subtitle = CreateText(root, "Subtitle", 44f, new Vector2(0f, -62f), new Vector2(1000f, 60f))
        };
        slots.Add(slot);
        return slot;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, float size, Vector2 position, Vector2 box)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        ComicUITheme theme = FeelOverlay.Theme;
        if (theme != null && theme.headingFont != null)
        {
            text.font = theme.headingFont;
            if (theme.outlinedText != null) text.fontSharedMaterial = theme.outlinedText;
        }
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void Update()
    {
        foreach (Slot slot in slots)
        {
            if (!slot.active) continue;
            slot.elapsed += Time.unscaledDeltaTime;
            if (slot.elapsed >= InTime + slot.hold + OutTime)
            {
                slot.active = false;
                slot.root.gameObject.SetActive(false);
                continue;
            }
            Apply(slot);
        }
    }

    private static void Apply(Slot slot)
    {
        float t = slot.elapsed;
        float scale, alpha, rise = 0f, rotation;
        if (t < InTime)
        {
            float k = t / InTime;
            scale = Mathf.LerpUnclamped(2.1f, 1f, OutBack(k));
            alpha = Mathf.Clamp01(k * 2.2f);
            rotation = slot.tilt * (1f - k);
        }
        else if (t < InTime + slot.hold)
        {
            scale = 1f;
            alpha = 1f;
            rotation = 0f;
        }
        else
        {
            float k = (t - InTime - slot.hold) / OutTime;
            scale = 1f + 0.12f * k;
            alpha = 1f - k * k;
            rise = 36f * k;
            rotation = 0f;
        }

        slot.root.anchoredPosition = new Vector2(0f, slot.y + rise);
        slot.root.localScale = Vector3.one * scale;
        slot.root.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Color titleColor = slot.color; titleColor.a = alpha;
        slot.title.color = titleColor;
        slot.subtitle.color = new Color(1f, 1f, 1f, alpha);
    }

    private static float OutBack(float k)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
