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
        startPanel = StartSelectionPanelUI.Attach(mainMenuPanel.transform.parent);
        if (startPanel != null)
        {
            startPanel.StartRequested += StartRun;
            startPanel.BackRequested += OnStartBack;
        }
    }

    private void OnDestroy()
    {
        if (startPanel == null) return;
        startPanel.StartRequested -= StartRun;
        startPanel.BackRequested -= OnStartBack;
    }

    private void OnPlayClicked()
    {
        if (startPanel != null && startPanel.HasCatalog)
        {
            mainMenuPanel.SetActive(false);
            optionsPanel.SetActive(false);
            startPanel.Open();
            return;
        }
        StartRun();
    }

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