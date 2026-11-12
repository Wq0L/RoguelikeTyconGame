using UnityEngine;
using UnityEngine.UI;

// Skill tree panelinin düz siyah arka planını derinlikli uzaya çevirir. Mevcut Background Image'ına
// runtime materyal takar; ağaç sürüklendikçe/zoom yapıldıkça katmanlar farklı hızlarda kayar.
// Sadece panel açıkken çalışır, unscaled time kullanır (shop'ta timeScale 0).
[RequireComponent(typeof(Graphic))]
public class SkillTreeSpaceBackground : MonoBehaviour
{
    // Oyuncu geri bildirimi: renkli uzay node'lardan dikkati çalıyordu. Taban neredeyse siyah, nebula/galaksi
    // sadece sezilir, yıldızlar seyrek ve sönük; açık renkli node kartları öne çıkar.
    [Header("Colors")]
    [SerializeField] private Color deepColor = new Color(0.012f, 0.012f, 0.03f);
    [SerializeField] private Color centerGlow = new Color(0.045f, 0.035f, 0.09f);
    [SerializeField] private Color nebulaA = new Color(0.557f, 0.231f, 0.749f);
    [SerializeField] private Color nebulaB = new Color(0.173f, 0.498f, 0.82f);
    [SerializeField] private Color galaxyColor = new Color(0.95f, 0.78f, 1f);

    [Header("Look")]
    [SerializeField, Range(0f, 2f)] private float nebulaIntensity = 0.1f;
    [SerializeField, Range(0f, 2f)] private float galaxyIntensity = 0.1f;
    [SerializeField, Range(0f, 3f)] private float starIntensity = 0.35f;
    [SerializeField, Range(0f, 1f)] private float starDensity = 0.3f;
    [SerializeField, Range(0f, 1f)] private float vignette = 0.65f;
    [Tooltip("Derinlik gücü. 0: arka plan sabit, 1: yakın katmanlar ağaçla neredeyse birlikte kayar.")]
    [SerializeField, Range(0f, 2f)] private float parallax = 1f;

    // Comic kağıt panosu (10 Kasım 2026, kullanıcı: "derinliği var ama kötü hissettiriyor"): kitin diliyle basılı plan panosu
    // (UI/ComicBoardBackground). Lacivert taban, tram noktalı bulutlar, keskin toon yıldızlar, düğümlerle birebir kayan ızgara.
    // Kapalıysa ya da shader yoksa eski uzay (SpaceUIBackground).
    [Header("Comic board")]
    [SerializeField] private bool comicBoard = true;
    [SerializeField] private Color boardCenter = new Color(0.105f, 0.2f, 0.31f);
    [SerializeField] private Color boardEdge = new Color(0.06f, 0.105f, 0.19f);
    [SerializeField] private Color boardGrid = new Color(0.3f, 0.46f, 0.62f);
    [SerializeField] private Color boardDotsA = new Color(0.16f, 0.29f, 0.44f);
    [SerializeField] private Color boardDotsB = new Color(0.2f, 0.22f, 0.42f);
    [SerializeField] private Color boardStars = new Color(0.74f, 0.83f, 0.93f);
    [SerializeField, Range(0f, 1f)] private float boardGridStrength = 0.5f;
    [SerializeField, Range(0f, 1f)] private float boardDotStrength = 0.8f;
    [SerializeField, Range(10f, 120f)] private float boardDotScale = 54f;
    [SerializeField, Range(0f, 1f)] private float boardStarDensity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float boardVignette = 0.3f;
    public bool UsesComicBoard { get; private set; }

    private static readonly int SpaceViewId = Shader.PropertyToID("_SpaceView");
    private static readonly int SpacePanId = Shader.PropertyToID("_SpacePan");

    private Graphic graphic;
    private Material material;
    private SkillTreeUI tree;
    private Color originalColor;
    private Material originalMaterial;

    // Panelin tam ekran "Background" Image'ına takılır; yoksa panelin en arkasına yenisini açar.
    public static SkillTreeSpaceBackground Attach(GameObject skillPanel)
    {
        if (skillPanel == null) return null;
        var existing = skillPanel.GetComponentInChildren<SkillTreeSpaceBackground>(true);
        if (existing != null) return existing;

        Transform background = skillPanel.transform.Find("Background");
        GameObject target;
        if (background != null && background.GetComponent<Graphic>() != null) target = background.gameObject;
        else
        {
            target = new GameObject("Space Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)target.transform;
            rect.SetParent(skillPanel.transform, false);
            rect.SetAsFirstSibling();
            FeelOverlay.Stretch(rect);
            target.GetComponent<Image>().raycastTarget = false;
        }
        return target.AddComponent<SkillTreeSpaceBackground>();
    }

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        tree = GetComponentInParent<SkillTreeUI>(true);
        // Ayarlardan arka plan efektleri kapalıysa panelin kendi düz arka planı kalır.
        if (!GameSettings.BackgroundEffects)
        {
            enabled = false;
            return;
        }
        Shader shader = comicBoard ? Resources.Load<Shader>("ComicBoardBackground") : null;
        UsesComicBoard = shader != null && shader.isSupported;
        if (!UsesComicBoard) shader = Resources.Load<Shader>("SpaceUIBackground");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("SpaceUIBackground shader bulunamadı veya desteklenmiyor; skill tree arka planı değişmedi.", this);
            enabled = false;
            return;
        }
        material = new Material(shader) { name = UsesComicBoard ? "Skill Tree Comic Board (runtime)" : "Skill Tree Space (runtime)" };
        originalColor = graphic.color;
        originalMaterial = graphic.material;
        graphic.material = material;
        graphic.color = Color.white;
        ApplyProperties();
    }

    private void OnValidate()
    {
        if (material != null) ApplyProperties();
    }

    private void ApplyProperties()
    {
        material.SetColor("_DeepColor", deepColor);
        material.SetColor("_CenterColor", centerGlow);
        material.SetColor("_NebulaA", nebulaA);
        material.SetColor("_NebulaB", nebulaB);
        material.SetColor("_GalaxyColor", galaxyColor);
        material.SetFloat("_NebulaIntensity", nebulaIntensity);
        material.SetFloat("_GalaxyIntensity", galaxyIntensity);
        material.SetFloat("_StarIntensity", starIntensity);
        material.SetFloat("_StarDensity", starDensity);
        material.SetFloat("_Vignette", vignette);
        material.SetFloat("_Parallax", parallax);
        if (!UsesComicBoard) return;
        material.SetColor("_BaseCenter", boardCenter);
        material.SetColor("_BaseEdge", boardEdge);
        material.SetColor("_GridColor", boardGrid);
        material.SetColor("_DotColor", boardDotsA);
        material.SetColor("_DotColorB", boardDotsB);
        material.SetColor("_StarColor", boardStars);
        material.SetFloat("_GridStrength", boardGridStrength);
        material.SetFloat("_DotStrength", boardDotStrength);
        material.SetFloat("_DotScale", boardDotScale);
        material.SetFloat("_StarDensity", boardStarDensity);
        material.SetFloat("_Vignette", boardVignette);
    }

    private void LateUpdate()
    {
        if (material == null) return;
        SpaceNoiseBaker.Bind(material);
        Rect rect = graphic.rectTransform.rect;
        float height = Mathf.Max(1f, rect.height);
        float aspect = Mathf.Max(0.01f, rect.width / height);
        Vector2 pan = tree != null ? tree.PanPosition / height : Vector2.zero;
        float zoom = tree != null ? tree.Zoom : 1f;
        // w: ekran yüksekliğinde kaç düğüm aralığı var (panonun ızgarası düğümlere oturur).
        float cells = tree != null ? height / Mathf.Max(1f, tree.NodeSpacing) : 0f;
        material.SetVector(SpaceViewId, new Vector4(Time.unscaledTime, aspect, zoom, cells));
        material.SetVector(SpacePanId, new Vector4(pan.x, pan.y, 0f, 0f));
    }

    private void OnDestroy()
    {
        if (graphic != null && material != null)
        {
            graphic.material = originalMaterial;
            graphic.color = originalColor;
        }
        if (material != null) Destroy(material);
    }
}
