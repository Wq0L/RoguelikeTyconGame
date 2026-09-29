using UnityEngine;
using UnityEngine.UI;

// Feel efektlerinin ortak overlay canvas kurulumu. Tıklanmaz (GraphicRaycaster yok), oyun UI'ının üstünde durur.
public static class FeelOverlay
{
    private static ComicUITheme theme;
    private static bool themeLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        theme = null;
        themeLoaded = false;
    }

    public static ComicUITheme Theme
    {
        get
        {
            if (!themeLoaded)
            {
                theme = Resources.Load<ComicUITheme>("ComicUITheme");
                themeLoaded = true;
            }
            return theme;
        }
    }

    public static RectTransform CreateCanvas(string name, int sortingOrder)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        // Oyun canvas'ıyla aynı referans: yazılar her çözünürlükte aynı oranda durur.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)root.transform;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
