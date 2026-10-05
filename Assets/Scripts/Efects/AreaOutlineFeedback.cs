using System.Collections.Generic;
using UnityEngine;

// Hücre çevresi çizgisiyle kısa alan geri bildirimi; iki kullanım aynı havuzu, geometriyi ve çizimi paylaşır (Bölüm 3.7.6.2: sınıfın
// adı ortak sorumluluğuna göre değişti; eski adı AftershockAreaFeedback).
// - Artçı alanı (Bölüm 3.7.5.1, PlayAftershock): Artçı Patlama'nın ikinci darbesinin vurduğu alanın dış sınırı kısa bir dışa
//   yayılmayla belirir ve söner. İlk patlamada yoktur. Alan formülü burada yok: bölge, BehaviorEchoes'un o darbe için oyun
//   geometrisinden (HarvestBehaviorGeometry.ExplosionCells) aldığı hedef hücreler ile saksının ayak izidir. Tarlanın dışındaki ve
//   kilitli hücreler bölgeye girmez. Çok hücreli saksıda bölge bir daire değildir; çizgi hücre kenarlarını izler. Yarıçap profile
//   göre ne ise (eski 1,50, yeni 2,00 hücre) çizgi odur. Renk kor kırmızısı.
// - Zincir kaynağı (Bölüm 3.7.6, PlayChainSource): zincirle tetiklenen saksının ayak izi, mor renkte. Yalnız kaynak saksıyı
//   gösterir; zincirin yayılımını çizmez (o görsel TODO'da açık).
// - Çizgi, boss bölgesi çizgisiyle aynı shader'ı kullanır (FrostZoneMarker: dolgu yok; saksı ve bitki arkasında kalan kısım soluk).
// - Yalnız görseldir: havuz (Capacity dalga, iki kullanım için ortak) doluysa dalga çizilmez ve kullanımına göre ayrı sayılır
//   (AftershockSkipped / ChainSkipped); hasar ve hedefler bundan bağımsızdır. Nesneler, ağlar ve iki malzeme sahnede bir kez
//   kurulur; darbe başına nesne, malzeme ya da liste üretilmez. Round bitince, menüde ve sahne değişiminde bütün dalgalar kalkar.
public sealed class AreaOutlineFeedback : MonoBehaviour
{
    public const int Capacity = 8;
    // Zaman çizelgesi (sn, oyun zamanı): GrowTime'da bölgenin sınırına ulaşır, HoldTime'dan sonra söner, Lifetime'da kalkar.
    // Patlama bulutları ~0,3 sn sürer; çizgi onlardan biraz sonra da okunur kalır.
    public const float GrowTime = 0.15f, HoldTime = 0.32f, Lifetime = 0.6f, StartScale = 0.55f;
    // Çizgi (dünya birimi; hücre 2 birim): boss bölgesi çizgisinden (0,22) ince. Saksı / bitki arkasında kalan kısmın opaklığı:
    // izometrik görünümde alanın uzak kenarları saksı gövdelerinin arkasında kalır; çizgi kısa süreli olduğu için boss bölgesinden
    // (0,5) yüksek tutulur.
    public const float LineWidth = 0.2f, LineInset = 0.03f, BehindAlpha = 0.7f;
    // Çizim sırası: patlama parçacıklarından (saydam, 3000) sonra. Darbe anında bulutlar çizgiyi örtmesin (imleç halkası da en üstte).
    public const int RenderQueue = 3100;
    private const float Lift = 0.03f;
    public static readonly Color Tint = new Color(1f, 0.36f, 0.2f, 1f);
    // Zincir kaynağı: mor (kor kırmızısı artçı, amber Sert Kabuk, mavi Don, altın Hasat Ritmi'nden ayrı).
    public static readonly Color ChainTint = new Color(0.72f, 0.45f, 1f, 1f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ActiveId = Shader.PropertyToID("_Active");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    private sealed class Wave
    {
        public Transform root;
        public Mesh mesh;
        public MeshRenderer line, behind;
        public float age;
        public int frame;
        public bool active;
        public Color tint;
    }

    public static AreaOutlineFeedback Instance { get; private set; }

    private readonly Wave[] waves = new Wave[Capacity];
    private int created, activeCount;
    private readonly List<Vector3> vertices = new(256);
    private readonly List<Vector2> uvs = new(256);
    private readonly List<int> triangles = new(384);
    private readonly HashSet<long> area = new();
    private readonly List<GridPosition> cells = new(64);
    private MaterialPropertyBlock block;
    private Material material, behindMaterial;
    private bool unavailable;

    // Çizilen ve havuz dolu olduğu için çizilmeyen dalga sayısı (run boyunca değil, bu sahne nesnesinin ömrü boyunca).
    public int AftershockPlayed { get; private set; }
    public int AftershockSkipped { get; private set; }
    // Zincir kaynağı vurgusu: çizilen ve havuz dolu olduğu için çizilmeyen.
    public int ChainPlayed { get; private set; }
    public int ChainSkipped { get; private set; }
    public int ActiveCount => activeCount;
    // Son çizilen dalga: bölgedeki hücreler (ayak izi dahil), çevre kenarı sayısı, yayılmanın merkezi (ayak izinin ortası).
    public IReadOnlyList<GridPosition> LastCells => cells;
    public int LastEdgeCount { get; private set; }
    public Vector3 LastCenter { get; private set; }
    // Son çizilen dalganın kökü (ağ yerel koordinatta, kök ayak izinin ortasında; ölçek yalnız yayılma sırasında 1'in altında).
    public Transform LastWave { get; private set; }

    // İkinci darbenin başladığı anda çağrılır. footprint: saksının hücreleri; targets: o darbenin oyun geometrisinden gelen hedef hücreleri.
    public static bool PlayAftershock(GridSystem grid, IReadOnlyList<GridPosition> footprint, IReadOnlyList<GridPosition> targets) =>
        Ensure(grid, footprint) && Instance.Show(grid, footprint, targets, false);

    // Zincirle tetiklenen saksının ayak izi (HarvestChain, davranış başladığı karede). Bölge yalnız ayak izidir.
    public static bool PlayChainSource(GridSystem grid, IReadOnlyList<GridPosition> footprint) =>
        Ensure(grid, footprint) && Instance.Show(grid, footprint, null, true);

    private static bool Ensure(GridSystem grid, IReadOnlyList<GridPosition> footprint)
    {
        if (grid == null || footprint == null || footprint.Count == 0) return false;
        Create();
        return true;
    }

    private static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("Area Outline");
        if (VFXManager.Instance != null) go.transform.SetParent(VFXManager.Instance.transform, false);
        go.AddComponent<AreaOutlineFeedback>();
    }

    // Havuzu, malzemeleri ve bütün dalgaları önceden kurar (ilk darbede tek seferlik takılma olmasın). Round başında, çizgiyi
    // kullanacak bir ödül (Artçı Patlama, Zincir Hasat) varken çağrılır; ikinci çağrı bir şey yapmaz.
    public static void Prewarm()
    {
        Create();
        if (!Instance.EnsureResources()) return;
        while (Instance.created < Capacity) { Instance.waves[Instance.created] = Instance.CreateWave(Instance.created); Instance.created++; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        block = new MaterialPropertyBlock();
    }

    private static GroundCell Ground(GridSystem grid, GridPosition cell) => grid.GetGridObject(cell)?.GetGroundCellCached();

    private void Include(GridPosition cell)
    {
        if (area.Add(CellOutlineMesh.Key(cell.x, cell.z))) cells.Add(cell);
    }

    private bool Show(GridSystem grid, IReadOnlyList<GridPosition> footprint, IReadOnlyList<GridPosition> targets, bool chainSource)
    {
        if (!EnsureResources()) return false;
        Wave wave = null;
        for (int i = 0; i < created && wave == null; i++) if (!waves[i].active) wave = waves[i];
        if (wave == null && created < Capacity) { wave = CreateWave(created); waves[created++] = wave; }
        if (wave == null) { if (chainSource) ChainSkipped++; else AftershockSkipped++; return false; }

        cells.Clear(); area.Clear();
        float sumX = 0f, sumZ = 0f; int count = 0;
        // Arayüz üzerinden foreach sayaç nesnesi ayırır: indeksle gezilir (darbe başına ayırma yok).
        for (int i = 0; i < footprint.Count; i++)
        {
            GroundCell ground = Ground(grid, footprint[i]);
            if (ground == null) continue;
            sumX += ground.transform.position.x; sumZ += ground.transform.position.z; count++;
            Include(footprint[i]);
        }
        if (count == 0) return false;
        if (targets != null)
            for (int i = 0; i < targets.Count; i++)
            {
                GroundCell ground = Ground(grid, targets[i]);
                if (ground != null && !ground.IsLocked) Include(targets[i]);
            }

        Vector3 origin = new Vector3(sumX / count, 0f, sumZ / count);
        float half = (GridManager.Instance != null ? GridManager.Instance.GetCellSize() : 2f) * 0.5f;
        vertices.Clear(); uvs.Clear(); triangles.Clear();
        int edges = 0;
        foreach (GridPosition cell in cells)
        {
            GroundCell ground = Ground(grid, cell);
            Renderer groundRenderer = ground.GroundRenderer;
            float top = groundRenderer != null ? groundRenderer.bounds.max.y : ground.transform.position.y;
            Vector3 center = new Vector3(ground.transform.position.x - origin.x, top + Lift, ground.transform.position.z - origin.z);
            foreach (Vector2Int side in CellOutlineMesh.Sides)
            {
                // Komşu da bölgedeyse bu kenar alanın içindedir: çizilmez.
                if (area.Contains(CellOutlineMesh.Key(cell.x + side.x, cell.z + side.y))) continue;
                CellOutlineMesh.AddEdge(vertices, uvs, triangles, center, side, half, LineInset, LineWidth);
                edges++;
            }
        }
        wave.mesh.Clear();
        wave.mesh.SetVertices(vertices);
        wave.mesh.SetUVs(0, uvs);
        wave.mesh.SetTriangles(triangles, 0);
        wave.mesh.RecalculateBounds();
        wave.root.position = origin;
        wave.age = 0f;
        wave.frame = Time.frameCount;
        wave.active = true;
        wave.tint = chainSource ? ChainTint : Tint;
        wave.line.enabled = wave.behind.enabled = true;
        activeCount++;
        Apply(wave);
        LastEdgeCount = edges;
        LastCenter = origin;
        LastWave = wave.root;
        if (chainSource) ChainPlayed++; else AftershockPlayed++;
        return true;
    }

    private void Apply(Wave wave)
    {
        float grow = Mathf.Clamp01(wave.age / GrowTime);
        float scale = Mathf.Lerp(StartScale, 1f, 1f - (1f - grow) * (1f - grow) * (1f - grow));
        wave.root.localScale = new Vector3(scale, 1f, scale);
        Color color = wave.tint;
        color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HoldTime, Lifetime, wave.age));
        block.SetColor(ColorId, color);
        wave.line.SetPropertyBlock(block);
        wave.behind.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (activeCount == 0) return;
        RoundManager rounds = RoundManager.Instance;
        GameManager game = GameManager.Instance;
        if (rounds == null || !rounds.IsRoundActive || game == null || game.CurrentState != GameStates.Round) { Clear(); return; }
        float delta = Time.deltaTime;
        int frame = Time.frameCount;
        for (int i = 0; i < created; i++)
        {
            Wave wave = waves[i];
            if (!wave.active || wave.frame == frame) continue;   // başladığı karenin süresi sayılmaz
            wave.age += delta;
            if (wave.age >= Lifetime) Hide(wave);
            else Apply(wave);
        }
    }

    private void Hide(Wave wave)
    {
        if (!wave.active) return;
        wave.active = false;
        wave.line.enabled = wave.behind.enabled = false;
        activeCount--;
    }

    // Bütün dalgaları hemen kaldırır (round sonu, menü, devre dışı kalma).
    public void Clear()
    {
        for (int i = 0; i < created; i++) Hide(waves[i]);
        activeCount = 0;
    }

    private void OnDisable() => Clear();

    private bool EnsureResources()
    {
        if (material != null) return true;
        if (unavailable) return false;
        Shader shader = Resources.Load<Shader>("FrostZoneMarker");
        if (shader == null || !shader.isSupported)
        {
            unavailable = true;
            Debug.LogWarning("FrostZoneMarker shader bulunamadı veya desteklenmiyor; alan çizgisi (artçı alanı, zincir kaynağı) gösterilmeyecek (hasar etkilenmez).", this);
            return false;
        }
        material = new Material(shader) { name = "Area Outline (runtime)" };
        material.SetFloat(ActiveId, 1f);
        material.SetFloat(PulseId, 0f);
        behindMaterial = new Material(shader) { name = "Area Outline behind objects (runtime)" };
        behindMaterial.SetFloat(ActiveId, 1f);
        behindMaterial.SetFloat(PulseId, 0f);
        behindMaterial.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Greater);
        behindMaterial.SetFloat(AlphaId, BehindAlpha);
        material.renderQueue = behindMaterial.renderQueue = RenderQueue;
        return true;
    }

    private Wave CreateWave(int index)
    {
        var root = new GameObject("Area Outline Wave " + index).transform;
        root.SetParent(transform, false);
        var mesh = new Mesh { name = "Area Outline " + index };
        mesh.MarkDynamic();
        return new Wave
        {
            root = root, mesh = mesh,
            line = CreateRenderer(root, "Outline", mesh, material),
            behind = CreateRenderer(root, "Outline (behind objects)", mesh, behindMaterial),
        };
    }

    private static MeshRenderer CreateRenderer(Transform parent, string title, Mesh mesh, Material sharedMaterial)
    {
        var go = new GameObject(title, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
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
        if (Instance == this) Instance = null;
        for (int i = 0; i < created; i++) if (waves[i].mesh != null) Destroy(waves[i].mesh);
        if (material != null) Destroy(material);
        if (behindMaterial != null) Destroy(behindMaterial);
    }
}
