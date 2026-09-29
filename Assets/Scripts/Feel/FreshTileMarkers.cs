using System.Collections.Generic;
using UnityEngine;

// Bu round'un kart seçimlerinin düştüğü tile'lara dünyada sarı çerçeve. Kart seçilince tile'a oturarak belirir;
// round sonunda, önizlemede ve saksı yerleştirirken görünür, yeni round başlayınca kalkar
// (liste ProgressionManager'da). Sadece listeyi okur, oynanışa dokunmaz. Unscaled time kullanır.
public class FreshTileMarkers : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.87f, 0.32f);
    [Tooltip("Quad'ın tile'a oranı. Fazlası tile kenarının dışına taşan hale için.")]
    [SerializeField, Range(1f, 1.6f)] private float quadScale = 1.3f;
    [SerializeField, Min(0.05f)] private float popDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float breathSpeed = 4.4f;

    private static readonly int GlowId = Shader.PropertyToID("_Glow");

    private sealed class Marker
    {
        public Transform root;
        public GroundCell cell;
        public float born;
    }

    private readonly List<Marker> markers = new();
    private int activeCount;
    private Material material;
    private Mesh quad;
    private bool unavailable;

    private void LateUpdate()
    {
        ProgressionManager progression = ProgressionManager.Instance;
        GameManager game = GameManager.Instance;
        IReadOnlyList<GroundCell> cells = progression != null && game != null && IsShownIn(game.CurrentState)
            ? progression.RoundAppliedCells : null;
        Sync(cells);
        if (activeCount == 0) return;

        float now = Time.unscaledTime;
        material.SetFloat(GlowId, 0.5f - 0.5f * Mathf.Cos(now * breathSpeed));
        float size = GridManager.Instance != null ? GridManager.Instance.GetCellSize() * quadScale : quadScale;
        for (int i = 0; i < activeCount; i++)
        {
            float pop = Mathf.Clamp01((now - markers[i].born) / popDuration);
            markers[i].root.localScale = Vector3.one * size * Mathf.LerpUnclamped(1.6f, 1f, OutBack(pop));
        }
    }

    private static bool IsShownIn(GameStates state) => state == GameStates.CardSelection || state == GameStates.RoundEnd ||
        state == GameStates.Shop || state == GameStates.Placing || state == GameStates.Selling;

    // Liste sadece büyür (kart seçildikçe) ya da tamamen temizlenir; eşleşen işaretler yerinde kalır.
    private void Sync(IReadOnlyList<GroundCell> cells)
    {
        int count = cells != null ? cells.Count : 0;
        if (count > 0 && !EnsureResources()) count = 0;
        for (int i = 0; i < count; i++)
        {
            if (i < activeCount && markers[i].cell == cells[i]) continue;
            if (i == markers.Count) markers.Add(new Marker { root = CreateMarker(i) });
            Marker marker = markers[i];
            marker.cell = cells[i];
            marker.born = Time.unscaledTime;
            Renderer ground = marker.cell != null ? marker.cell.GroundRenderer : null;
            Bounds bounds = ground != null ? ground.bounds : new Bounds(marker.cell != null ? marker.cell.transform.position : Vector3.zero, Vector3.zero);
            marker.root.position = new Vector3(bounds.center.x, bounds.max.y + 0.01f, bounds.center.z);
            marker.root.gameObject.SetActive(marker.cell != null);
        }
        for (int i = count; i < activeCount; i++)
        {
            markers[i].cell = null;
            markers[i].root.gameObject.SetActive(false);
        }
        activeCount = count;
    }

    private bool EnsureResources()
    {
        if (material != null) return true;
        if (unavailable) return false;
        Shader shader = Resources.Load<Shader>("FreshTileMarker");
        if (shader == null || !shader.isSupported)
        {
            unavailable = true;
            Debug.LogWarning("FreshTileMarker shader bulunamadı veya desteklenmiyor; yeni tile çerçeveleri gösterilmeyecek.", this);
            return false;
        }
        material = new Material(shader) { name = "Fresh Tile Marker (runtime)" };
        material.SetColor("_Color", color);
        material.SetFloat("_Edge", 1f / quadScale);
        quad = new Mesh { name = "Fresh Tile Quad" };
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
        var go = new GameObject("Fresh Tile " + index, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        // Quad XY düzleminde; yere yatır.
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

    private static float OutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (quad != null) Destroy(quad);
    }
}
