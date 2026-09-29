using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject roundEndUI;
    [SerializeField] private GameObject placementShopPanel;
    [SerializeField] private GameObject skillShopPanel;
    [SerializeField] private GameObject roundUI;
    [SerializeField] private GameObject xpUI;
    [SerializeField] private GameObject cardSelectionPanel;
    [SerializeField] private GameObject runCompletePanel;
    [Tooltip("Altın/Demir/Taş sayaçlarının kökü. Skill ağacı açıkken panelin üstüne taşınır. Boşsa GoldUI'ların ebeveyni kullanılır.")]
    [SerializeField] private RectTransform resourcesUI;

    private GameObject currentPanel;
    private GameObject lastShopPanel;
    private Coroutine resonancePresentation;
    private Button exitSellButton;
    private Button exitSkillButton;
    private RoundPreviewUI roundPreview;
    private Transform resourcesHome;
    private int resourcesHomeIndex;


    private void Start()
    {
        exitSellButton = CreateExitButton("Exit Sell Mode", "SATIŞTAN ÇIK");
        exitSellButton.onClick.AddListener(() =>
        {
            if (GameManager.Instance.CurrentState == GameStates.Selling)
                PlacementManager.Instance.ExitSellMode();
        });
        exitSkillButton = CreateExitButton("Exit Shop", "GERİ");
        exitSkillButton.onClick.AddListener(() =>
        {
            if (GameManager.Instance.CurrentState == GameStates.Shop &&
                (currentPanel == skillShopPanel || currentPanel == placementShopPanel))
                GameManager.Instance.ShowRoundEnd();
        });
        // Açılış animasyonları. Kart ve skill paneli kendi içeriğini kendisi canlandırır.
        UIPanelTransition.Attach(roundEndUI, true);
        UIPanelTransition.Attach(placementShopPanel, true);
        UIPanelTransition.Attach(skillShopPanel, false);
        UIPanelTransition.Attach(cardSelectionPanel, false);
        UIPanelTransition.Attach(runCompletePanel, true);
        RoundSummaryUI.Attach(roundEndUI);
        RoundNewTilesUI.Attach(roundEndUI);
        roundPreview = RoundPreviewUI.Attach(roundEndUI, this);
        if (roundPreview != null) UIPanelTransition.Attach(roundPreview.gameObject, true);
        SkillTreeSpaceBackground.Attach(skillShopPanel);
        if (resourcesUI == null)
        {
            GoldUI counter = FindFirstObjectByType<GoldUI>(FindObjectsInactive.Include);
            if (counter != null) resourcesUI = counter.transform.parent as RectTransform;
        }
        GameManager.Instance.OnGameStateChanged += HandleStateChanged;
        // Scene loading can enter RunSetup before this component subscribes.
        HandleStateChanged(GameManager.Instance.CurrentState);
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
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleRoundPreview();
        // Opt-in performance capture; nothing exists until the first press.
        if (Input.GetKeyDown(KeyCode.F9))
            PerformanceTrendCapture.ToggleFromHotkey();
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

            case GameStates.RoundEnd:
            case GameStates.RunSetup:
                CloseRoundPreview();
                break;
        }
    }

    private void HandleStateChanged(GameStates state)
    {
        exitSellButton.gameObject.SetActive(state == GameStates.Selling);
        if (state == GameStates.Selling) UIPop.For(exitSellButton).Play(0.08f);
        exitSkillButton.gameObject.SetActive(false);
        CancelResonancePresentation();
        if (state == GameStates.MainMenu || state == GameStates.RunSetup || state == GameStates.RunComplete)
            VFXManager.Instance?.ClearPendingResonances();
        bool isRoundActive = state == GameStates.Round;
        roundUI.SetActive(isRoundActive);
        xpUI.SetActive(isRoundActive);

        switch (state)
        {
            case GameStates.RunSetup:
            case GameStates.RoundEnd:
                ShowRoundEndUI();
                break;

            case GameStates.Shop:
                if (lastShopPanel != null)
                    OpenPanel(lastShopPanel);
                break;

            case GameStates.Placing:
            case GameStates.Selling:
                CloseCurrentPanel();
                break;

            case GameStates.Round:
                CloseAll();
                lastShopPanel = null;
                resonancePresentation = StartCoroutine(PresentPendingResonancesOnRound());
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
        CancelResonancePresentation();
        CloseCurrentPanel();
        lastShopPanel = null;
        roundEndUI.SetActive(true);
        currentPanel = roundEndUI;
    }

    private IEnumerator PresentPendingResonancesOnRound()
    {
        // Wait for the preparation panels to close, then present in the actual round.
        yield return new WaitForSecondsRealtime(.25f);
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round)
            VFXManager.Instance?.PlayPendingResonances();
        resonancePresentation = null;
    }

    private void CancelResonancePresentation()
    {
        if (resonancePresentation == null) return;
        StopCoroutine(resonancePresentation);
        resonancePresentation = null;
    }

    private void OnDisable() => CancelResonancePresentation();
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

    // Round sonu önizlemesi bir panel gibi açılır; oyun durumu RoundEnd'de kalır.
    public void OpenRoundPreview()
    {
        GameStates state = GameManager.Instance.CurrentState;
        if (roundPreview == null || (state != GameStates.RoundEnd && state != GameStates.RunSetup)) return;
        if (currentPanel != roundEndUI) return;
        OpenPanel(roundPreview.gameObject);
    }

    public void CloseRoundPreview()
    {
        if (roundPreview == null || currentPanel != roundPreview.gameObject) return;
        ShowRoundEndUI();
    }

    private void ToggleRoundPreview()
    {
        if (roundPreview != null && currentPanel == roundPreview.gameObject) CloseRoundPreview();
        else OpenRoundPreview();
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
        if (panel == skillShopPanel) PinResources(panel.transform);
        if (exitSkillButton != null)
        {
            bool showExit = panel == skillShopPanel || panel == placementShopPanel;
            exitSkillButton.gameObject.SetActive(showExit);
            exitSkillButton.transform.SetAsLastSibling();
            if (showExit) UIPop.For(exitSkillButton).Play(0.15f);
        }
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
        if (exitSkillButton != null) exitSkillButton.gameObject.SetActive(false);
        UnpinResources();
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            currentPanel = null;
        }
    }

    public void CloseAll()
    {
        if (exitSkillButton != null) exitSkillButton.gameObject.SetActive(false);
        UnpinResources();
        roundEndUI.SetActive(false);
        if (roundPreview != null) roundPreview.gameObject.SetActive(false);
        placementShopPanel.SetActive(false);
        skillShopPanel.SetActive(false);
        cardSelectionPanel.SetActive(false); // yeni
        currentPanel = null;
    }

    // Skill ağacı tam ekran ve sayaçları örtüyor: açıkken sayaçlar panelin en üstüne taşınır, kapanınca yerine döner.
    // Panel kapanmadan önce geri alınır; sayaçlar hiç devre dışı kalmaz, abonelikleri bozulmaz.
    private void PinResources(Transform panel)
    {
        if (resourcesUI == null || resourcesUI.parent == panel) return;
        UnpinResources();
        resourcesHome = resourcesUI.parent;
        resourcesHomeIndex = resourcesUI.GetSiblingIndex();
        resourcesUI.SetParent(panel, false);
        resourcesUI.SetAsLastSibling();
    }

    private void UnpinResources()
    {
        if (resourcesUI == null || resourcesHome == null) return;
        resourcesUI.SetParent(resourcesHome, false);
        resourcesUI.SetSiblingIndex(resourcesHomeIndex);
        resourcesHome = null;
    }

    private Button CreateExitButton(string objectName, string caption)
    {
        // Separate from the movable skill-tree content and the hidden shop panel.
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.layer = roundEndUI.layer;
        obj.transform.SetParent(roundEndUI.transform.parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(240f, 64f);
        Button button = obj.GetComponent<Button>();
        Image background = obj.GetComponent<Image>();
        Button reference = roundEndUI.GetComponentInChildren<Button>(true);
        Image referenceImage = reference != null ? reference.targetGraphic as Image : null;
        if (referenceImage != null)
        {
            background.sprite = referenceImage.sprite;
            background.type = referenceImage.type;
            background.color = referenceImage.color;
            background.pixelsPerUnitMultiplier = referenceImage.pixelsPerUnitMultiplier;
            button.transition = reference.transition == Selectable.Transition.Animation
                ? Selectable.Transition.ColorTint : reference.transition;
            button.colors = reference.colors;
            button.spriteState = reference.spriteState;
        }
        else background.color = new Color(.15f, .2f, .25f, 1f);
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = obj.layer;
        labelObject.transform.SetParent(obj.transform, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = new Vector2(-24f, -12f);
        TMP_Text referenceLabel = reference != null ? reference.GetComponentInChildren<TMP_Text>(true) : null;
        label.font = TMP_Settings.defaultFontAsset;
        label.color = referenceLabel != null ? referenceLabel.color : Color.white;
        label.text = caption;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16;
        label.fontSizeMax = 24;
        label.raycastTarget = false;
        Resources.Load<ComicUITheme>("ComicUITheme")?.StyleButton(button);
        obj.SetActive(false);
        return button;
    }
}
