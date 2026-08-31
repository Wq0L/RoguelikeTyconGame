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
    }

    private void OnPlayClicked()
    {
        GameManager.Instance.StartRunSetup();
        SceneManager.LoadScene(gameSceneName);
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
        Debug.Log("Quit clicked");
        Application.Quit();
    }
}