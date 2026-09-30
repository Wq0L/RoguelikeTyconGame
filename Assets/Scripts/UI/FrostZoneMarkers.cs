using System.Collections.Generic;
using UnityEngine;

// Segment olayı bölgesinin (Don Cephesi şeridi) dünyadaki gösterimi: yaklaşırken taralı, aktifken dolu buz rengi.
// Olayın sakladığı bölgeyi okur (önizleme ile uygulama aynı hücreler); sadece açık hücrelere çizer, oynanışa dokunmaz.
// UIManager kurar. Unscaled time kullanır.
public sealed class FrostZoneMarkers : MonoBehaviour
{
    private static readonly int ActiveId = Shader.PropertyToID("_Active");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<Transform> markers = new();
    private readonly List<GridPosition> shownCells = new();
    private int activeCount, shownVersion = -1;
    private bool shownActive;
    private Material material;
    private Mesh quad;
    private bool unavailable;
    private GridUnlockManager unlocks;
    private int gridVersion;

    public IReadOnlyList<GridPosition> ShownCells => shownCells;
    public bool ShowsActive => shownActive && activeCount > 0;

    public static FrostZoneMarkers Ensure()
    {
        var existing = FindAnyObjectByType<FrostZoneMarkers>();
        return existing != null ? existing : new GameObject("Frost Zone Markers").AddComponent<FrostZoneMarkers>();
    }

    private void LateUpdate()
    {
        SegmentEventDirector events = SegmentEventDirector.Instance;
        GameManager game = GameManager.Instance;
        bool visible = events != null && game != null && game.CurrentState != GameStates.MainMenu && game.CurrentState != GameStates.RunComplete;
        SegmentEventRuntime shown = !visible ? null : events.Active ?? events.Upcoming;
        if (unlocks == null && GridUnlockManager.Instance != null)
        {
            unlocks = GridUnlockManager.Instance;
            unlocks.OnGridSizeChanged += HandleGridSizeChanged;
        }
        int version = (events != null ? events.Version : -1) * 1000 + gridVersion;
        if (shown == null || !shown.IsPrepared) { Hide(); return; }
        if (version != shownVersion || shownActive != shown.IsActive) Sync(shown, version);
        if (activeCount > 0) material.SetFloat(PulseId, 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 3f));
    }

    // Grid büyüyünce şeridin yeni açılan hücreleri de çizilsin.
    private void HandleGridSizeChanged() => gridVersion++;

    private void Sync(SegmentEventRuntime shown, int version)
    {
        shownVersion = version;
        shownActive = shown.IsActive;
        shownCells.Clear();
        if (!EnsureResources()) { Hide(); return; }
        material.SetColor(ColorId, shown.Data.color);
        material.SetFloat(ActiveId, shown.IsActive ? 1f : 0f);

        GridManager grid = GridManager.Instance;
        GridSystem system = grid != null ? grid.GetGridSystem() : null;
        int count = 0;
        if (system != null)
        {
            float size = grid.GetCellSize() * 0.98f;
            foreach (GridPosition cell in shown.ZoneCells)
            {
                GroundCell ground = system.GetGridObject(cell)?.GetGroundCellCached();
                if (ground == null || ground.IsLocked) continue;
                if (count == markers.Count) markers.Add(CreateMarker(count));
                Transform marker = markers[count++];
                Renderer groundRenderer = ground.GroundRenderer;
                Bounds bounds = groundRenderer != null ? groundRenderer.bounds : new Bounds(ground.transform.position, Vector3.zero);
                marker.position = new Vector3(bounds.center.x, bounds.max.y + 0.012f, bounds.center.z);
                marker.localScale = Vector3.one * size;
                marker.gameObject.SetActive(true);
                shownCells.Add(cell);
            }
        }
        for (int i = count; i < activeCount; i++) markers[i].gameObject.SetActive(false);
        activeCount = count;
    }

    private void Hide()
    {
        for (int i = 0; i < activeCount; i++) markers[i].gameObject.SetActive(false);
        activeCount = 0;
        shownCells.Clear();
        shownVersion = -1;
    }

    private bool EnsureResources()
    {
        if (material != null) return true;
        if (unavailable) return false;
        Shader shader = Resources.Load<Shader>("FrostZoneMarker");
        if (shader == null || !shader.isSupported)
        {
            unavailable = true;
            Debug.LogWarning("FrostZoneMarker shader bulunamadı veya desteklenmiyor; olay bölgesi dünyada gösterilmeyecek.", this);
            return false;
        }
        material = new Material(shader) { name = "Frost Zone Marker (runtime)" };
        quad = new Mesh { name = "Frost Zone Quad" };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
        return true;
    }

    private Transform CreateMarker(int index)
    {
        var go = new GameObject("Frost Zone " + index, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.GetComponent<MeshFilter>().sharedMesh = quad;
        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    private void OnDestroy()
    {
        if (unlocks != null) unlocks.OnGridSizeChanged -= HandleGridSizeChanged;
        if (material != null) Destroy(material);
        if (quad != null) Destroy(quad);
    }
}
