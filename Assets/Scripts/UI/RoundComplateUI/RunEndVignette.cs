using UnityEngine;
using UnityEngine.UI;

// Run sonu ekranı açılınca (22 Kasım 2026): mürekkep rengi tram kararması ekranın kenarlarından ve köşelerinden içeri kapanır
// (HalftoneVignette shader'ı, _Progress 0 → 1), ardından sonuç plakası damga gibi düşer, istatistik kartları, ödül kartı ve butonlar
// sırayla zıplayarak gelir (UIPop). Bir kez oynar (tekrarlayan nabız yok); oyun durmuşken de (timeScale 0) ölçeksiz zamanla.
// Malzeme: Awake'te bir kopya (paylaşılan malzeme değişmesin), OnDestroy'da silinir. Karede yalnız bir SetFloat; bellek ayırmaz.
// Görünüm ve sıralama sahnede (Tools > Comic UI > Run Sonu Ekranı); burada yalnız hareket.
public sealed class RunEndVignette : MonoBehaviour
{
    [SerializeField] private Image vignette;
    [Tooltip("Kararma kapanırken içerik görünmez; parçalar ilk karede zıplamaya başlar (alt nesneler o ana kadar hazırdır).")]
    [SerializeField] private CanvasGroup content;
    [SerializeField] private float closeSeconds = .9f;
    [Tooltip("Sırayla zıplayarak gelen parçalar (başlık plakası, istatistik kartları, ödül kartı, butonlar).")]
    [SerializeField] private RectTransform[] popIn = new RectTransform[0];
    [SerializeField] private float popStart = .45f, popStep = .07f;
    [SerializeField] private float stampAngle = -9f;
    [Header("Kayıp / zafer (kullanıcı: \"kazanınca da aynı his veriyor\")")]
    [Tooltip("Kayıp: kırmızı-mürekkep tram kenarlardan kapanır, sahne kararır.")]
    [SerializeField] private Color lossInk = new Color32(58, 16, 24, 255);
    [SerializeField, Range(0f, 1f)] private float lossBaseDim = .45f;
    [Tooltip("Karanlığın ne kadar içeri geldiği (0: yalnız köşeler, 1.5: bütün ekran). Panelin çevresinde tram görünsün diye orta.")]
    [SerializeField, Range(0f, 1.5f)] private float lossReach = .6f;
    [Tooltip("Zafer: altın tram kenarlardan gelir, sahne açık kalır (altın = ödül).")]
    [SerializeField] private Color winInk = new Color32(236, 170, 52, 255);
    [SerializeField, Range(0f, 1f)] private float winBaseDim = .12f;
    [SerializeField, Range(0f, 1.5f)] private float winReach = .42f;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress"), BaseDimId = Shader.PropertyToID("_BaseDim"),
        ReachId = Shader.PropertyToID("_Reach"), TintId = Shader.PropertyToID("_Color");
    private Material instance;
    private float age;
    private bool playing, popsPending;

    public float Progress { get; private set; }

    private void Awake()
    {
        if (vignette != null && vignette.material != null && vignette.material.HasProperty(ProgressId))
        {
            instance = new Material(vignette.material) { name = vignette.material.name + " (run sonu)" };
            vignette.material = instance;
        }
    }

    private void OnDestroy()
    {
        if (instance != null) Destroy(instance);
    }

    private void OnEnable()
    {
        age = 0f;
        playing = true;
        SetProgress(0f);
        // Zıplamalar ilk karede başlar: bu bileşen panelin kökünde, alt nesnelerin (ComicHoverMotion) Awake'inden önce çalışır;
        // o an başlatılan pop dinlenme boyunu 0 okuyup butonu görünmez bırakıyordu (22 Kasım 2026, TEKRAR butonu kayboluyordu).
        popsPending = true;
        if (content != null) content.alpha = 0f;
        bool won = RoundManager.Instance != null && RoundManager.Instance.Outcome == RunOutcome.Victory;
        if (vignette != null) vignette.color = won ? winInk : lossInk;
        if (instance != null)
        {
            instance.SetColor(TintId, Color.white);   // renk yalnız Image renginden (kayıp / zafer)
            instance.SetFloat(BaseDimId, won ? winBaseDim : lossBaseDim);
            instance.SetFloat(ReachId, won ? winReach : lossReach);
        }
    }

    private void StartPops()
    {
        popsPending = false;
        if (content != null) content.alpha = 1f;
        for (int i = 0; i < popIn.Length; i++)
            if (popIn[i] != null) UIPop.For(popIn[i]).Play(popStart + i * popStep, i == 0 ? .38f : .3f, i == 0 ? stampAngle : 0f, lockInput: true);
    }

    private void Update()
    {
        if (popsPending) StartPops();
        if (!playing) return;
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(.01f, closeSeconds));
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);   // hızlı başlar, yumuşak durur
        SetProgress(eased);
        if (t >= 1f) playing = false;
    }

    private void SetProgress(float value)
    {
        Progress = value;
        if (instance != null) instance.SetFloat(ProgressId, value);
    }

#if UNITY_EDITOR
    public void Setup(Image image, CanvasGroup group, RectTransform[] items)
    {
        vignette = image;
        content = group;
        popIn = items;
    }
#endif
}
