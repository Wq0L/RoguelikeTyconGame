using UnityEngine;

// Oyun sahnesinin uzay arka planı. Kameranın önüne, görüşü tam kaplayan tek bir quad yerleştirir
// ve Resources/SpaceBackground shader'ı ile çizer. Kamera kayınca/zoom yapınca yıldız katmanları
// farklı hızlarda kayar (paralaks). Collider yok: raycast'leri etkilemez. Unscaled time kullanır.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class SpaceBackground : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color deepColor = new Color(0.043f, 0.043f, 0.118f);
    [SerializeField] private Color centerGlow = new Color(0.165f, 0.106f, 0.302f);
    [SerializeField] private Color nebulaA = new Color(0.557f, 0.231f, 0.749f);
    [SerializeField] private Color nebulaB = new Color(0.173f, 0.498f, 0.82f);
    [SerializeField, Range(0f, 2f)] private float nebulaIntensity = 0.34f;

    [Header("Stars")]
    [SerializeField, Range(0f, 3f)] private float starIntensity = 0.95f;
    [SerializeField, Range(0f, 1f)] private float starDensity = 0.38f;
    [SerializeField, Range(0f, 1f)] private float shootingStarRate = 0.35f;

    [Header("Planet")]
    [SerializeField] private bool showPlanet = true;
    [SerializeField] private Color planetColor = new Color(0.95f, 0.55f, 0.42f);
    [SerializeField] private Color planetRim = new Color(0.5f, 0.8f, 1f);
    [Tooltip("Viewport koordinatı (0-1).")]
    [SerializeField] private Vector2 planetPosition = new Vector2(0.86f, 0.8f);
    [Tooltip("Ekran yüksekliğine oranla yarıçap.")]
    [SerializeField, Range(0.02f, 0.4f)] private float planetRadius = 0.13f;

    [Header("Motion")]
    [SerializeField, Range(0f, 1f)] private float parallax = 0.35f;

    [Header("Scene Integration")]
    [Tooltip("Fog rengini uzay tonuna çeker; ada arka planla kaynaşır. Kapatınca sahnedeki orijinal renk kalır.")]
    [SerializeField] private bool tintFog = true;
    [SerializeField] private Color fogColor = new Color(0.36f, 0.38f, 0.58f);
    [SerializeField, Min(1f)] private float distance = 900f;

    private const float Margin = 1.1f;

    private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int NebulaAId = Shader.PropertyToID("_NebulaA");
    private static readonly int NebulaBId = Shader.PropertyToID("_NebulaB");
    private static readonly int NebulaIntensityId = Shader.PropertyToID("_NebulaIntensity");
    private static readonly int StarIntensityId = Shader.PropertyToID("_StarIntensity");
    private static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
    private static readonly int PlanetColorId = Shader.PropertyToID("_PlanetColor");
    private static readonly int PlanetRimId = Shader.PropertyToID("_PlanetRim");
    private static readonly int PlanetParamsId = Shader.PropertyToID("_PlanetParams");
    private static readonly int ShootingRateId = Shader.PropertyToID("_ShootingRate");
    private static readonly int ParallaxId = Shader.PropertyToID("_Parallax");
    private static readonly int SpaceViewId = Shader.PropertyToID("_SpaceView");
    private static readonly int SpaceOffsetId = Shader.PropertyToID("_SpaceOffset");

    private Camera cam;
    private Material material;
    private Mesh mesh;
    private Transform quad;
    private Color originalFogColor, originalBackground;
    private bool sceneChanged;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        originalFogColor = RenderSettings.fogColor;
        originalBackground = cam.backgroundColor;
        sceneChanged = true;
        GameSettings.Changed += OnSettingsChanged;

        // Ayarlardan kapalıysa (zayıf ekran kartı) sadece düz uzay rengi: shader maliyeti yok.
        if (!GameSettings.BackgroundEffects)
        {
            cam.backgroundColor = deepColor;
            return;
        }
        var shader = Resources.Load<Shader>("SpaceBackground");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("SpaceBackground shader bulunamadı veya desteklenmiyor; düz arka plan kullanılıyor.", this);
            cam.backgroundColor = deepColor;
            return;
        }

        material = new Material(shader) { name = "Space Background (runtime)" };
        mesh = CreateQuadMesh();
        var quadObject = new GameObject("Space Background", typeof(MeshFilter), typeof(MeshRenderer));
        quadObject.layer = gameObject.layer;
        quadObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = quadObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        quad = quadObject.transform;

        ApplyProperties();
        LateUpdate();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= OnSettingsChanged;
        if (quad != null) Destroy(quad.gameObject);
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
        quad = null;
        material = null;
        mesh = null;
        // Sadece gerçekten değiştirdiysek geri yükle (yarıda kapanınca kamerayı siyaha çevirmesin).
        if (sceneChanged && cam != null)
        {
            RenderSettings.fogColor = originalFogColor;
            cam.backgroundColor = originalBackground;
        }
        sceneChanged = false;
    }

    // Oyun içinden ayar değişirse arka planı yeniden kur.
    private void OnSettingsChanged()
    {
        if (GameSettings.BackgroundEffects == (quad != null)) return;
        enabled = false;
        enabled = true;
    }

    private void OnValidate()
    {
        // Play modunda inspector'dan canlı ayar.
        if (material != null) ApplyProperties();
    }

    private void ApplyProperties()
    {
        material.SetColor(DeepColorId, deepColor);
        material.SetColor(HorizonColorId, centerGlow);
        material.SetColor(NebulaAId, nebulaA);
        material.SetColor(NebulaBId, nebulaB);
        material.SetFloat(NebulaIntensityId, nebulaIntensity);
        material.SetFloat(StarIntensityId, starIntensity);
        material.SetFloat(StarDensityId, starDensity);
        material.SetColor(PlanetColorId, planetColor);
        material.SetColor(PlanetRimId, planetRim);
        material.SetVector(PlanetParamsId, new Vector4(planetPosition.x, planetPosition.y, planetRadius, showPlanet ? 1f : 0f));
        material.SetFloat(ShootingRateId, shootingStarRate);
        material.SetFloat(ParallaxId, parallax);

        cam.backgroundColor = deepColor;
        RenderSettings.fogColor = tintFog ? fogColor : originalFogColor;
    }

    private void LateUpdate()
    {
        if (quad == null) return;
        SpaceNoiseBaker.Bind(material);

        float viewHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = Mathf.Max(0.01f, cam.aspect);
        float depth = Mathf.Min(distance, cam.farClipPlane * 0.9f);

        // Kameranın taban yönüne hizalanır: shake roll'u dünyayla birlikte gökyüzünü de döndürür.
        Quaternion rotation = CameraFeel.Instance != null ? CameraFeel.Instance.BaseWorldRotation : cam.transform.rotation;
        quad.SetPositionAndRotation(cam.transform.position + rotation * Vector3.forward * depth, rotation);
        quad.localScale = new Vector3(viewHeight * aspect * Margin, viewHeight * Margin, 1f);

        Vector2 offset = CameraFeel.Instance != null ? CameraFeel.Instance.ViewOffset : Vector2.zero;
        float zoom = CameraFeel.Instance != null ? CameraFeel.Instance.ZoomOffset : 0f;
        material.SetVector(SpaceViewId, new Vector4(Time.unscaledTime, aspect, zoom, Margin));
        material.SetVector(SpaceOffsetId, new Vector4(offset.x / viewHeight, offset.y / viewHeight, 0f, 0f));
    }

    private static Mesh CreateQuadMesh()
    {
        var quadMesh = new Mesh { name = "Space Background Quad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.RecalculateBounds();
        return quadMesh;
    }
}
