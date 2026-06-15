using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject roundEndUI;
    public GameObject placementShopPanel;
    public GameObject skillShopPanel;

    private GameObject currentPanel;
    private GameObject lastShopPanel;

    private void Start()
    {
        GameManager.Instance.OnGameStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();
    }

    private void HandleEscape()
    {
        switch (GameManager.Instance.CurrentState)
        {
            case GameStates.Shop:
                GameManager.Instance.ShowRoundEnd();
                break;

            case GameStates.Placing:
                PlacementManager.Instance.CancelPlacement();
                break;

            case GameStates.Selling:
                PlacementManager.Instance.ExitSellMode();
                break;
        }
    }

    private void HandleStateChanged(GameStates state)
    {
        switch (state)
        {
            case GameStates.RoundEnd:
                ShowRoundEndUI();
                break;

            case GameStates.Shop:
                if (lastShopPanel != null)
                    OpenPanel(lastShopPanel);
                break;

            case GameStates.Placing:
                CloseCurrentPanel();
                break;

            case GameStates.Round:
                CloseAll();
                lastShopPanel = null;
                break;
        }
    }

    public void ShowRoundEndUI()
    {
        CloseCurrentPanel();
        roundEndUI.SetActive(true);
        currentPanel = roundEndUI;
        lastShopPanel = null;
    }

    public void OpenPlacementShop()
    {
        lastShopPanel = placementShopPanel;
        GameManager.Instance.OpenShop();
        OpenPanel(placementShopPanel);
    }

    public void OpenSkillShop()
    {
        lastShopPanel = skillShopPanel;
        GameManager.Instance.OpenShop();
        OpenPanel(skillShopPanel);
    }

    public void NextRound()
    {
        CloseAll();
        lastShopPanel = null;
        RoundManager.Instance.StartNextRound();
    }

    private void OpenPanel(GameObject panel)
    {
        roundEndUI.SetActive(false);
        CloseCurrentPanel();

        panel.SetActive(true);
        currentPanel = panel;
    }

    public void CloseCurrentPanel()
    {
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            currentPanel = null;
        }
    }

    public void CloseAll()
    {
        roundEndUI.SetActive(false);
        placementShopPanel.SetActive(false);
        skillShopPanel.SetActive(false);
        currentPanel = null;
    }
}