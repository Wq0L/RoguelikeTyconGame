using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Ana Menü Butonları")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    [Header("Options Butonları")]
    [SerializeField] private Button backButton;   // OptionsPanel içindeki geri butonu

    [SerializeField] private string gameSceneName = "GameScene";

    // Oyna → başlangıç seçimi (çiftçi + tırpan) → run. Panel koddan kurulur; katalog yoksa eski akış (doğrudan run).
    private StartSelectionPanelUI startPanel;

    private void OnEnable()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        optionsButton.onClick.AddListener(OnOptionsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDisable()
    {
        playButton.onClick.RemoveListener(OnPlayClicked);
        optionsButton.onClick.RemoveListener(OnOptionsClicked);
        quitButton.onClick.RemoveListener(OnQuitClicked);
        backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void Start()
    {
        // Başlangıçta ana menü açık, options kapalı
        mainMenuPanel.SetActive(true);
        optionsPanel.SetActive(false);
        ApplyComicKit();
        startPanel = StartSelectionPanelUI.Attach(mainMenuPanel.transform.parent);
        if (startPanel != null)
        {
            startPanel.StartRequested += StartRun;
            startPanel.BackRequested += OnStartBack;
        }
    }

    // Comic kağıt kiti (11 Kasım 2026, kullanıcı: "main menu"; Docs/UI-Dili.md). Sahnedeki üç buton kitin sprite'larına geçer;
    // sahne dosyasına dokunulmaz, boyut ve konum burada verilir. Renk görevleri: Oyna mavi (ilerle, ekrandaki tek ana buton),
    // Ayarlar mor (ikincil), Çıkış kırmızı (çık / iptal). Yazılar Bungee; hover hareketi eklendi (eskiden yoktu). Her karede malzeme
    // yazan ComicButtonVisual kalkar. Kit yoksa sahnedeki eski görünüm kalır.
    public const float PlayWidth = 600f, PlayHeight = 128f, OtherWidth = 500f, OtherHeight = 104f;
    public bool UsesComicKit { get; private set; }
    // Canlı menü sahnesi (Tools > Menü > Canlı Sahne) buton yerleşimini sahneye yazar ve bunu açar: konum ve boyut artık
    // sahneden gelir, burada ezilmez (editör kuralı). Kapalıysa eski ortalı yerleşim.
    [SerializeField] private bool sceneLayout;
    public bool SceneLayout => sceneLayout;

    private void ApplyComicKit()
    {
        if (UsesComicKit || !ComicKit.Ready) return;
        UsesComicKit = true;
        KitButton(playButton, ComicKit.Blue, new Vector2(0f, 236f), new Vector2(PlayWidth, PlayHeight), sceneLayout);
        KitButton(optionsButton, ComicKit.Purple, new Vector2(0f, 84f), new Vector2(OtherWidth, OtherHeight), sceneLayout);
        KitButton(quitButton, ComicKit.Red, new Vector2(0f, -48f), new Vector2(OtherWidth, OtherHeight), sceneLayout);
    }

    private static void KitButton(Button button, string colour, Vector2 position, Vector2 size, bool keepSceneLayout)
    {
        if (button == null) return;
        var rect = (RectTransform)button.transform;
        if (keepSceneLayout) size = rect.sizeDelta;
        else { rect.anchoredPosition = position; rect.sizeDelta = size; }   // sprite ölçeği yükseklikten hesaplanır: önce boyut
        if (!ComicKit.StyleButton(button, colour)) return;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            // Yazı sprite'ın üst yüzünde durur (alt şerit 3B kenar). "Oyna<br>" gibi eski yerleşim satır sonu çizilmez.
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(34f, 20f); labelRect.offsetMax = new Vector2(-44f, -10f);
            label.margin = Vector4.zero;
            label.textPreprocessor = ComicKit.SingleLineCaps.Instance;
            label.fontSizeMax = size.y * 0.5f;
            label.alignment = TextAlignmentOptions.Center;
        }
        if (!button.TryGetComponent(out ComicHoverMotion _)) button.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.05f;
    }

    private void OnDestroy()
    {
        if (startPanel == null) return;
        startPanel.StartRequested -= StartRun;
        startPanel.BackRequested -= OnStartBack;
    }

    // İlk "Oyna": önce açılış videosu (bir kez, Space basılı tutularak geçilir), video karardığında seçim ekranı.
    private void OnPlayClicked()
    {
        if (IntroVideoPlayer.IsPlaying) return;
        bool panel = startPanel != null && startPanel.HasCatalog;
        if (panel)
        {
            mainMenuPanel.SetActive(false);
            optionsPanel.SetActive(false);
        }
        System.Action next = panel ? OpenStartPanel : StartRun;
        if (!DialogueProgress.IntroSeen && IntroVideoPlayer.Play(next)) return;
        next();
    }

    private void OpenStartPanel() => startPanel.Open();

    private void StartRun()
    {
        GameManager.Instance.StartRunSetup();
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnStartBack()
    {
        startPanel.Close();
        mainMenuPanel.SetActive(true);
    }

    private void OnOptionsClicked()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    private void OnBackClicked()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void OnQuitClicked()
    {
        // Debug.Log("Quit clicked");
        Application.Quit();
    }
}