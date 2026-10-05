using System.Collections.Generic;
using UnityEngine;

// Boss bölgesinin (Don Cephesi, Sert Kabuk şeridi) dünyadaki gösterimi: bölgenin ÇEVRE ÇİZGİSİ.
// Yaklaşırken kesikli, aktifken düz çizgi; renk boss kimliğinden gelir (BossTheme: Don buz mavisi, Sert Kabuk amber).
// Bölgenin içi boyanmaz: tile'ın kendi dolgu rengi (kart türü) aynen okunur, saksı ve bitkinin üstüne boss dolgusu gelmez.
// Çizgi zemindedir; saksının arkasında kalan kısmı ikinci, soluk bir kopya gösterir (tarla saksıyla doluyken de sınır okunur).
// Olayın sakladığı bölgeyi okur (önizleme ile uygulama aynı hücreler); yalnız açık hücreleri sayar, oynanışa dokunmaz.
// Ekran hava efekti (BossWeatherOverlay) bundan ayrıdır. UIManager kurar. Unscaled time kullanır.
public sealed class FrostZoneMarkers : MonoBehaviour
{
    private static readonly int ActiveId = Shader.PropertyToID("_Active");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    // Çizgi kalınlığı ve bölge sınırından içeri pay (dünya birimi; hücre 2 birim).
    public const float LineWidth = 0.22f;
    public const float LineInset = 0.03f;
    // Bir nesnenin (saksı, bitki) arkasında kalan çizginin opaklığı.
    public const float OccludedAlpha = 0.5f;
    private const float Lift = 0.02f;

    private readonly List<GridPosition> shownCells = new();
    private readonly HashSet<long> shownSet = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<Vector2> uvs = new();
    private readonly List<int> triangles = new();
    private int shownVersion = -1, edgeCount;
    private bool shownActive;
    private Material material, occludedMaterial;
    private Mesh mesh;
    private MeshRenderer outline, occluded;
    private bool unavailable;
    private GridUnlockManager unlocks;
    private int gridVersion;

    public IReadOnlyList<GridPosition> ShownCells => shownCells;
    public bool ShowsActive => shownActive && shownCells.Count > 0;
    // Çizilen çevre kenarı sayısı (bölge ile bölge dışı / kilitli hücre arasındaki hücre kenarları).
    public int EdgeCount => edgeCount;
    // Gösterimde kullanılan renk (boss kimliğinden).
    public Color ShownColor { get; private set; }
    public Renderer OutlineRenderer => outline;
    // Saksı / bitki arkasında kalan kısmı soluk çizen kopya.
    public Renderer OccludedRenderer => occluded;

    public static FrostZoneMarkers Ensure()
    {
        var existing = FindAnyObjectByType<FrostZoneMarkers>();
        return existing != null ? existing : new GameObject("Boss Zone Outline").AddComponent<FrostZoneMarkers>();
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
        if (shownCells.Count > 0 && material != null)
        {
            float pulse = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 3f);
            material.SetFloat(PulseId, pulse);
            occludedMaterial.SetFloat(PulseId, pulse);
        }
    }

    // Grid büyüyünce şeridin yeni açılan hücreleri de bölgeye girer: çevre yeniden çizilir.
    private void HandleGridSizeChanged() => gridVersion++;

    private static long Key(int x, int z) => CellOutlineMesh.Key(x, z);

    private void Sync(SegmentEventRuntime shown, int version)
    {
        shownVersion = version;
        shownActive = shown.IsActive;
        shownCells.Clear();
        shownSet.Clear();
        edgeCount = 0;
        if (!EnsureResources()) { Hide(); return; }
        ShownColor = BossTheme.Accent(shown.Data);
        material.SetColor(ColorId, ShownColor);
        material.SetFloat(ActiveId, shown.IsActive ? 1f : 0f);
        occludedMaterial.SetColor(ColorId, ShownColor);
        occludedMaterial.SetFloat(ActiveId, shown.IsActive ? 1f : 0f);

        GridManager grid = GridManager.Instance;
        GridSystem system = grid != null ? grid.GetGridSystem() : null;
        vertices.Clear(); uvs.Clear(); triangles.Clear();
        if (system != null)
        {
            foreach (GridPosition cell in shown.ZoneCells)
            {
                GroundCell ground = system.GetGridObject(cell)?.GetGroundCellCached();
                if (ground == null || ground.IsLocked) continue;
                shownCells.Add(cell);
                shownSet.Add(Key(cell.x, cell.z));
            }
            float half = grid.GetCellSize() * 0.5f;
            foreach (GridPosition cell in shownCells)
            {
                GroundCell ground = system.GetGridObject(cell).GetGroundCellCached();
                Renderer groundRenderer = ground.GroundRenderer;
                Bounds bounds = groundRenderer != null ? groundRenderer.bounds : new Bounds(ground.transform.position, Vector3.zero);
                Vector3 center = new Vector3(ground.transform.position.x, bounds.max.y + Lift, ground.transform.position.z);
                foreach (Vector2Int side in CellOutlineMesh.Sides)
                {
                    // Komşu da gösterilen bölgedeyse bu kenar bölgenin içindedir: çizilmez.
                    if (shownSet.Contains(Key(cell.x + side.x, cell.z + side.y))) continue;
                    CellOutlineMesh.AddEdge(vertices, uvs, triangles, center, side, half, LineInset, LineWidth);
                    edgeCount++;
                }
            }
        }
        mesh.Clear();
        if (vertices.Count > 0)
        {
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
        outline.enabled = occluded.enabled = vertices.Count > 0;
    }

    private void Hide()
    {
        if (outline != null) outline.enabled = false;
        if (occluded != null) occluded.enabled = false;
        shownCells.Clear();
        shownSet.Clear();
        edgeCount = 0;
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
            Debug.LogWarning("FrostZoneMarker shader bulunamadı veya desteklenmiyor; boss bölgesi dünyada gösterilmeyecek.", this);
            return false;
        }
        material = new Material(shader) { name = "Boss Zone Outline (runtime)" };
        occludedMaterial = new Material(shader) { name = "Boss Zone Outline behind objects (runtime)" };
        occludedMaterial.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Greater);
        occludedMaterial.SetFloat(AlphaId, OccludedAlpha);
        mesh = new Mesh { name = "Boss Zone Outline" };
        mesh.MarkDynamic();
        outline = CreateRenderer("Outline", material);
        occluded = CreateRenderer("Outline (behind objects)", occludedMaterial);
        return true;
    }

    private MeshRenderer CreateRenderer(string title, Material sharedMaterial)
    {
        var go = new GameObject(title, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var line = go.GetComponent<MeshRenderer>();
        line.sharedMaterial = sharedMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        line.enabled = false;
        return line;
    }

    private void OnDestroy()
    {
        if (unlocks != null) unlocks.OnGridSizeChanged -= HandleGridSizeChanged;
        if (material != null) Destroy(material);
        if (occludedMaterial != null) Destroy(occludedMaterial);
        if (mesh != null) Destroy(mesh);
    }
}
