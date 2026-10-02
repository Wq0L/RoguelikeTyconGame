using UnityEngine;
using UnityEngine.UI;

// Presentation only: reads the active event; never consumes gameplay randomness.
[DisallowMultipleComponent]
public sealed class BossWeatherOverlay : MonoBehaviour
{
    public enum Weather { None, Fog, Snow, Sand }
    [Range(0f, 1f)] public float intensity = 0.65f;
    private RawImage image;
    private Material material;
    private float elapsed, fade;
    private Weather shown;
    public Weather CurrentWeather => shown;

    public static BossWeatherOverlay Ensure()
    {
        var existing = FindAnyObjectByType<BossWeatherOverlay>();
        return existing != null ? existing : new GameObject("Boss Weather").AddComponent<BossWeatherOverlay>();
    }

    public static Weather Resolve(SegmentEventRuntime active, bool roundActive)
    {
        if (!roundActive || active == null || !active.IsActive) return Weather.None;
        if (active.Data is FogSO) return Weather.Fog;
        if (active.Data is FrostFrontSO) return Weather.Snow;
        if (active.Data is HardShellSO) return Weather.Sand;
        return Weather.None;
    }

    private void Awake()
    {
        Shader shader = Resources.Load<Shader>("BossWeather");
        if (shader == null || !shader.isSupported) { enabled = false; return; }
        material = new Material(shader);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100; // World overlay, below normal HUD and choice panels.
        var surface = new GameObject("Atmosphere", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        surface.transform.SetParent(transform, false);
        image = surface.GetComponent<RawImage>();
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        image.material = material;
        image.enabled = false;
    }

    private void LateUpdate()
    {
        var rounds = RoundManager.Instance;
        Weather wanted = Resolve(SegmentEventDirector.Instance?.Active, rounds != null && rounds.IsRoundActive);
        // Menus, round rewards and restart clear immediately; active entry eases in.
        if (wanted == Weather.None) { Clear(); return; }
        if (wanted != shown) { fade = 0; elapsed = 0; shown = wanted; }
        elapsed += Time.deltaTime; // Pausing freezes the weather with gameplay.
        fade = Mathf.MoveTowards(fade, 1, Time.deltaTime * 1.5f);
        material.SetFloat("_Weather", (float)shown);
        material.SetFloat("_WeatherTime", elapsed);
        material.SetFloat("_Strength", fade * intensity);
        material.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
        image.enabled = true;
    }

    private void Clear()
    {
        shown = Weather.None;
        fade = elapsed = 0;
        if (image != null) image.enabled = false;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() { if (material != null) Destroy(material); }
}
