using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject roundEndUI;
    [SerializeField] private GameObject placementShopPanel;
    [SerializeField] private GameObject skillShopPanel;
    [SerializeField] private GameObject roundUI;
    [SerializeField] private GameObject xpUI;
    [SerializeField] private GameObject cardSelectionPanel;
    [SerializeField] private GameObject runCompletePanel;

    private GameObject currentPanel;
    private GameObject lastShopPanel;


    private void Start()
    {
        GameManager.Instance.OnGameStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
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
        bool isRoundActive = state == GameStates.Round;
        roundUI.SetActive(isRoundActive);
        xpUI.SetActive(isRoundActive);

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
            case GameStates.CardSelection:
                ShowCardSelectionUI();
                break;

            case GameStates.RunComplete:
                ShowRunCompleteUI();
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
    public void ShowRunCompleteUI()
    {
        CloseAll();
        runCompletePanel.SetActive(true);
        currentPanel = runCompletePanel;
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

    public void ShowCardSelectionUI()
    {
        CloseCurrentPanel();
        cardSelectionPanel.SetActive(true);
        currentPanel = cardSelectionPanel;

        CardSelectionUI cardUI = cardSelectionPanel.GetComponent<CardSelectionUI>();
        cardUI?.RefreshCards();  // her açılışta yeni random kartlar
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
        cardSelectionPanel.SetActive(false); // yeni
        currentPanel = null;
    }
}