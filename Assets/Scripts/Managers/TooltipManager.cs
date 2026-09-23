using System.Collections.Generic;
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private Transform tooltipParent;

    private Dictionary<GameObject, Queue<GameObject>> pools = new();

    private GameObject activeTooltip;
    private GameObject activePrefabKey;
    private ITooltipProvider activeProvider;   // ← EKLENDİ: aktif popup'ı kim açtı
    private readonly Vector3[] corners = new Vector3[4];

    private void LateUpdate()
    {
        if (activeTooltip == null || activeProvider == null) return;
        var rect = activeTooltip.transform as RectTransform;
        if (rect == null) return;
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 anchor = activeProvider.GetTooltipPosition();
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, anchor, camera, out var world)) return;
        rect.position = world;
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        Rect bounds = Screen.safeArea;
        if (camera != null) bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, camera.pixelRect.xMin), Mathf.Max(bounds.yMin, camera.pixelRect.yMin), Mathf.Min(bounds.xMax, camera.pixelRect.xMax), Mathf.Min(bounds.yMax, camera.pixelRect.yMax));
        Vector2 shift = Vector2.zero;
        if (max.x > bounds.xMax - 12) shift.x = anchor.x - 12 - max.x;
        else if (min.x < bounds.xMin + 12) shift.x = anchor.x + 12 - min.x;
        if (max.y > bounds.yMax - 12) shift.y = anchor.y - 12 - max.y;
        else if (min.y < bounds.yMin + 12) shift.y = anchor.y + 12 - min.y;
        min += shift; max += shift;
        shift.x += Mathf.Max(0, bounds.xMin + 12 - min.x) - Mathf.Max(0, max.x - (bounds.xMax - 12));
        shift.y += Mathf.Max(0, bounds.yMin + 12 - min.y) - Mathf.Max(0, max.y - (bounds.yMax - 12));
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, anchor + shift, camera, out world)) rect.position = world;
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(ITooltipProvider provider)
    {
        if (!provider.ShouldShowTooltip()) return;

        Hide();

        GameObject prefab = provider.GetTooltipPrefab();
        if (prefab == null) return;

        GameObject instance = GetFromPool(prefab);
        instance.transform.SetParent(tooltipParent, false);
        instance.transform.position = provider.GetTooltipPosition();
        instance.SetActive(true);

        provider.FillTooltip(instance);

        activeTooltip = instance;
        activePrefabKey = prefab;
        activeProvider = provider;   // ← EKLENDİ: sakla
        Canvas.ForceUpdateCanvases();
        LateUpdate();
    }

    public void Hide()
    {
        if (activeTooltip == null) return;

        activeTooltip.SetActive(false);
        ReturnToPool(activePrefabKey, activeTooltip);

        activeTooltip = null;
        activePrefabKey = null;
        activeProvider = null;   // ← EKLENDİ: temizle
    }
    public void RefreshActive()
    {
        if (activeTooltip == null || activeProvider == null) return;

        activeProvider.FillTooltip(activeTooltip);
        Canvas.ForceUpdateCanvases();
        LateUpdate();
    }

    private GameObject GetFromPool(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        Queue<GameObject> pool = pools[prefab];

        if (pool.Count > 0)
            return pool.Dequeue();

        return Instantiate(prefab);
    }

    private void ReturnToPool(GameObject prefab, GameObject instance)
    {
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        pools[prefab].Enqueue(instance);
    }
}
