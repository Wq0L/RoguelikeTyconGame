using UnityEngine;

// Ana menü arayüzünün hareketi (11 Kasım 2026, kullanıcı: "ekran çok boş; butonlar oynasın, üstte yamuk bir tabelada
// ERIGONE yazsın"). Görünüm sahnededir (MenuScene > MainMenuPanel: "Menu Title Sign" ve butonlar; Tools > Menü > Canlı
// Sahne ile kurulur, editörde düzenlenir). Bu bileşen yalnız hareketi verir:
// - Menü her açıldığında tabela yukarıdan, butonlar soldan sırayla kayarak gelir (kısa, geri tepmeli).
// - Sonra butonlar hafifçe süzülür (her biri ayrı fazda), tabela tepesinden asılı gibi çok az sallanır.
// Butonlarda yalnız konum değişir: ölçek ve dönüş hover hareketinindir (ComicHoverMotion), çakışmaz.
// Unscaled time; karede bellek ayırmaz. Konumlar sahnedeki değerlerdir (Awake'te okunur), burada ezilmez.
public sealed class MenuUiMotion : MonoBehaviour
{
    [Header("Referanslar (araç doldurur)")]
    [SerializeField] private RectTransform sign;
    [SerializeField] private RectTransform[] buttons = new RectTransform[0];

    [Header("Giriş")]
    [SerializeField, Min(.05f)] private float entranceTime = .45f;
    [SerializeField, Min(0f)] private float stagger = .08f;
    [SerializeField] private float buttonSlide = 520f, signDrop = 180f;

    [Header("Boşta")]
    [SerializeField, Min(0f)] private float buttonBob = 4f;
    [SerializeField, Min(.5f)] private float buttonPeriod = 4.2f;
    [SerializeField, Range(0f, 6f)] private float signSwing = 1.1f;
    [SerializeField, Min(.5f)] private float signPeriod = 6.5f;

    private Vector2 signBase;
    private Quaternion signRotation;
    private Vector2[] buttonBase;
    private float openedAt;

    public RectTransform Sign => sign;
    public int ButtonCount => buttons != null ? buttons.Length : 0;

    private void Awake()
    {
        if (sign != null) { signBase = sign.anchoredPosition; signRotation = sign.localRotation; }
        buttonBase = new Vector2[buttons != null ? buttons.Length : 0];
        for (int i = 0; i < buttonBase.Length; i++) buttonBase[i] = buttons[i] != null ? buttons[i].anchoredPosition : Vector2.zero;
    }

    private void OnEnable()
    {
        openedAt = Time.unscaledTime;
        Apply();   // ilk karede yerinden fırlamasın: giriş konumu hemen verilir
    }

    private void OnDisable()
    {
        // Panel kapanınca her şey sahnedeki yerine döner (bir sonraki açılış temiz başlar).
        if (sign != null && buttonBase != null) { sign.anchoredPosition = signBase; sign.localRotation = signRotation; }
        for (int i = 0; buttonBase != null && i < buttonBase.Length; i++) if (buttons[i] != null) buttons[i].anchoredPosition = buttonBase[i];
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (buttonBase == null) return;
        float now = Time.unscaledTime, since = now - openedAt;
        if (sign != null)
        {
            float k = BackOut(Mathf.Clamp01(since / entranceTime));
            float swing = signSwing * Mathf.Sin(now * 2f * Mathf.PI / signPeriod);
            sign.anchoredPosition = signBase + Vector2.up * (signDrop * (1f - k));
            sign.localRotation = signRotation * Quaternion.Euler(0f, 0f, swing * k);
        }
        for (int i = 0; i < buttonBase.Length; i++)
        {
            RectTransform b = buttons[i];
            if (b == null) continue;
            float k = BackOut(Mathf.Clamp01((since - stagger * (i + 1)) / entranceTime));
            float bob = buttonBob * Mathf.Sin(now * 2f * Mathf.PI / buttonPeriod + i * 1.9f);
            b.anchoredPosition = buttonBase[i] + new Vector2(-buttonSlide * (1f - k), bob * k);
        }
    }

    // Hafif geri tepmeli yerine oturma (yerine biraz geçip geri gelir).
    private static float BackOut(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
