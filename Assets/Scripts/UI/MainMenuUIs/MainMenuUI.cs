using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    [SerializeField] private string gameSceneName = "GameScene";

    private void OnEnable()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        optionsButton.onClick.AddListener(OnOptionsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnDisable()
    {
        playButton.onClick.RemoveListener(OnPlayClicked);
        optionsButton.onClick.RemoveListener(OnOptionsClicked);
        quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void OnPlayClicked()
    {
        GameManager.Instance.StartRunSetup();
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnOptionsClicked()
    {
        Debug.Log("Options clicked");
    }

    private void OnQuitClicked()
    {
        Debug.Log("Quit clicked");
        Application.Quit();
    }
}