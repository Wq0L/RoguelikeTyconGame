using System.Collections.Generic;
using UnityEngine;

// Boss bölgesinin (Don Cephesi, Sert Kabuk şeridi) dünyadaki gösterimi: bölgenin ÇEVRE ÇİZGİSİ.
// Yaklaşırken kesikli, aktifken düz çizgi; renk boss kimliğinden gelir (BossTheme: Don buz mavisi, Sert Kabuk amber).
// Bölgenin içi boyanmaz: tile'ın kendi dolgu rengi (kart türü) aynen okunur, saksı ve bitkinin üstüne boss dolgusu gelmez.
// Çizgi zemindedir; saksının arkasında kalan kısmı ikinci, soluk bir kopya gösterir (tarla saksıyla doluyken de sınır okunur).
// Olayın sakladığı bölgeyi okur (önizleme ile uygulama aynı hücreler); yalnız açık hücreleri sayar, oynanışa dokunmaz.
// Ekran hava efekti (BossWeatherOverlay) bundan ayrıdır. UIManager kurar. Çizgi nabzı unscaled time kullanır.
// Perde (A1, 5 Kasım 2026; referans Gezgin Bereket perdesi): boss AKTİFKEN bölgenin çevre kenarlarından yükselen, yukarıda solan
// ışık duvarı (yalnız kenarlarda: bölgenin içi, bitkiler ve darbeler örtülmez). Boss başlayınca yerden yükselir, sonra hafifçe
// nefes alır; oyun zamanıyla akar (duraklatınca durur). Ağ bölge değişince bir kez kurulur; karede yalnız boyu ayarlanır.
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
    // Perde: yükseklik (dünya birimi; Gezgin Bereket perdesiyle aynı), alttaki opaklık, yükselme süresi.
    public const float CurtainHeight = 1.35f;
    public const float CurtainAlpha = 0.32f;
    public const float CurtainRiseSeconds = 0.45f;
    // Kameraya bakan (ön) duvarların opaklık çarpanı: öndeki bitkiler ve darbeler perdenin arkasında kalmaz.
    public const float CurtainFrontFactor = 0.4f;

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
    private readonly List<Vector3> curtainVertices = new();
    private readonly List<Color> curtainColors = new();
    private readonly List<int> curtainTriangles = new();
    private Mesh curtainMesh;
    private Material curtainMaterial;
    private MeshRenderer curtain;
    private float curtainAge;
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
    public Renderer CurtainRenderer => curtain;
    public bool ShowsCurtain => curtain != null && curtain.enabled;
    public int CurtainWalls { get; private set; }
    // Perdesi olan boss'lar ve renkleri (A1: Don Cephesi buz mavisi; A2: Sert Kabuk kahverengi). Alt renk CurtainAlpha ile, üst renk
    // saydam çizilir.
    public static bool HasCurtain(SegmentEventSO boss) => boss is FrostFrontSO || boss is HardShellSO;
    public static void CurtainColors(SegmentEventSO boss, out Color bottom, out Color top)
    {
        if (boss is HardShellSO)
        {
            bottom = new Color(0.62f, 0.38f, 0.16f, CurtainAlpha);
            top = new Color(0.93f, 0.72f, 0.42f, 0f);
            return;
        }
        bottom = new Color(0.27f, 0.64f, 0.97f, CurtainAlpha);
        top = new Color(0.62f, 0.88f, 1f, 0f);
    }

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
        if (ShowsCurtain)
        {
            curtainAge += Time.deltaTime;
            float rise = Mathf.Clamp01(curtainAge / CurtainRiseSeconds);
            rise = rise * rise * (3f - 2f * rise);
            curtain.transform.localScale = new Vector3(1f, Mathf.Max(0.001f, rise * (0.92f + 0.08f * Mathf.Sin(curtainAge * 0.9f))), 1f);
        }
    }

    // Grid büyüyünce şeridin yeni açılan hücreleri de bölgeye girer: çevre yeniden çizilir.
    private void HandleGridSizeChanged() => gridVersion++;

    private static long Key(int x, int z) => CellOutlineMesh.Key(x, z);

    private void Sync(SegmentEventRuntime shown, int version)
    {
        if (shown.IsActive && !shownActive) curtainAge = 0f;   // boss başladı: perde yerden yükselir
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
        BuildCurtain(shown, system, grid);
    }

    // Çevre kenarlarına dikilen duvarlar: altta boss renginin açık tonu (CurtainAlpha), üstte saydam. Üst kenar konuma bağlı hafifçe
    // dalgalanır (komşu duvarlar aynı köşede birleşir: kesintisiz). Yükseklik transform'un ölçeğiyle (zemin hizasından) değişir.
    private void BuildCurtain(SegmentEventRuntime shown, GridSystem system, GridManager grid)
    {
        curtainVertices.Clear(); curtainColors.Clear(); curtainTriangles.Clear();
        CurtainWalls = 0;
        bool wanted = shown.IsActive && HasCurtain(shown.Data) && system != null && shownCells.Count > 0 && EnsureCurtain();
        if (!wanted) { if (curtain != null) curtain.enabled = false; return; }
        float half = grid.GetCellSize() * 0.5f, edgeAt = half - LineInset;
        CurtainColors(shown.Data, out Color bottom, out Color top);
        Color frontBottom = bottom;
        frontBottom.a *= CurtainFrontFactor;
        Camera view = Camera.main;
        Vector3 toCamera = view != null ? -view.transform.forward : Vector3.back;
        float baseY = float.MaxValue;
        foreach (GridPosition cell in shownCells)
        {
            GroundCell ground = system.GetGridObject(cell).GetGroundCellCached();
            Renderer groundRenderer = ground.GroundRenderer;
            float y = groundRenderer != null ? groundRenderer.bounds.max.y : ground.transform.position.y;
            if (y < baseY) baseY = y;
        }
        foreach (GridPosition cell in shownCells)
        {
            Vector3 c = system.GetGridObject(cell).GetGroundCellCached().transform.position;
            foreach (Vector2Int side in CellOutlineMesh.Sides)
            {
                if (shownSet.Contains(Key(cell.x + side.x, cell.z + side.y))) continue;
                Vector3 a, b;
                if (side.x != 0) { float x = c.x + side.x * edgeAt; a = new Vector3(x, 0f, c.z - half); b = new Vector3(x, 0f, c.z + half); }
                else { float z = c.z + side.y * edgeAt; a = new Vector3(c.x - half, 0f, z); b = new Vector3(c.x + half, 0f, z); }
                int start = curtainVertices.Count;
                curtainVertices.Add(a); curtainVertices.Add(b);
                curtainVertices.Add(a + Vector3.up * WallHeight(a)); curtainVertices.Add(b + Vector3.up * WallHeight(b));
                // Dış yüzü kameraya bakan duvar öndedir: daha saydam.
                Color low = side.x * toCamera.x + side.y * toCamera.z > 0f ? frontBottom : bottom;
                curtainColors.Add(low); curtainColors.Add(low); curtainColors.Add(top); curtainColors.Add(top);
                curtainTriangles.Add(start); curtainTriangles.Add(start + 2); curtainTriangles.Add(start + 1);
                curtainTriangles.Add(start + 1); curtainTriangles.Add(start + 2); curtainTriangles.Add(start + 3);
                CurtainWalls++;
            }
        }
        curtainMesh.Clear();
        curtainMesh.SetVertices(curtainVertices);
        curtainMesh.SetColors(curtainColors);
        curtainMesh.SetTriangles(curtainTriangles, 0);
        curtainMesh.RecalculateBounds();
        curtain.transform.position = new Vector3(0f, baseY + Lift, 0f);
        curtain.enabled = CurtainWalls > 0;
    }

    private static float WallHeight(Vector3 at) => CurtainHeight * (1f + 0.16f * Mathf.Sin(at.x * 1.7f + at.z * 1.3f));

    private bool EnsureCurtain()
    {
        if (curtainMaterial != null) return true;
        Shader shader = Resources.Load<Shader>("BountyAuroraToon");
        if (shader == null || !shader.isSupported) return false;
        curtainMaterial = new Material(shader) { name = "Boss Zone Curtain (runtime)" };
        curtainMesh = new Mesh { name = "Boss Zone Curtain" };
        curtainMesh.MarkDynamic();
        curtain = CreateRenderer("Curtain", curtainMaterial);
        curtain.GetComponent<MeshFilter>().sharedMesh = curtainMesh;
        return true;
    }

    private void Hide()
    {
        if (outline != null) outline.enabled = false;
        if (occluded != null) occluded.enabled = false;
        if (curtain != null) curtain.enabled = false;
        shownActive = false;
        CurtainWalls = 0;
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
        if (curtainMaterial != null) Destroy(curtainMaterial);
        if (curtainMesh != null) Destroy(curtainMesh);
    }
}
