using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// All visual objects are saved in GameScene and can be edited in the hierarchy.
// Animations use unscaled time because the shop pauses gameplay.
public class PlanterShopPanelUI : MonoBehaviour
{
    [Serializable]
    public class Card
    {
        public PlanterSO data;
        public RectTransform rect;
        public CanvasGroup group;
        public Button button;
        public Image background;
        public TMP_Text price;
        public Image preview;
        public GameObject lockedOverlay;
    }

    [SerializeField] private List<Card> cards = new List<Card>();
    [SerializeField] private Sprite normalCard;
    [SerializeField] private Sprite selectedCard;
    [SerializeField] private Sprite pressedCard;
    [SerializeField] private CanvasGroup details;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text summary;
    [SerializeField] private TMP_Text stats;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text buyLabel;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button backButton;
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private Image detailPreview;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text plotsLabel;
    [SerializeField] private TMP_Text sizeLabel;
    [SerializeField] private TMP_Text timeLabel;
    [SerializeField] private Image buyResourceIcon;
    [SerializeField] private ComicUITheme theme;
    private Vector2 detailsHome;

    private Vector2[] homePositions;
    private int selectedIndex = -1;
    private Coroutine transition;
    private bool animating;
    private ResourceManager resources;
    private StatManager statManager;
    private UnlockManager unlocks;

    private void Awake()
    {
        detailsHome = ((RectTransform)details.transform).anchoredPosition;
        homePositions = new Vector2[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            int index = i;
            Card card = cards[i];
            homePositions[i] = card.rect.anchoredPosition;
            card.button.onClick.AddListener(() => SelectCard(index));
            card.button.transition = Selectable.Transition.SpriteSwap;
            card.button.spriteState = new SpriteState
            {
                highlightedSprite = selectedCard,
                selectedSprite = selectedCard,
                pressedSprite = pressedCard
            };
        }
        buyButton.onClick.AddListener(Buy);
        backButton.onClick.AddListener(ShowList);
    }

    private void OnEnable()
    {
        resources = ResourceManager.Instance;
        statManager = StatManager.Instance;
        unlocks = UnlockManager.Instance;
        if (unlocks != null) unlocks.OnChanged += Refresh;
        if (resources != null) resources.OnResourceAmountChanged += OnResourceChanged;
        if (statManager != null) statManager.OnStatChanged += OnStatChanged;
        ResetView();
        if (cards.Count > 0) SelectCard(0);
    }

    private void OnDisable()
    {
        if (resources != null) resources.OnResourceAmountChanged -= OnResourceChanged;
        if (statManager != null) statManager.OnStatChanged -= OnStatChanged;
        if (unlocks != null) unlocks.OnChanged -= Refresh;
        StopTransition();
        // Always reopen on the catalogue, including after buying/cancelling placement.
        ResetView();
    }

    private void OnResourceChanged(ResourceType type, int amount) => Refresh();
    private void OnStatChanged(StatType type, float value) => Refresh();

    private void StopTransition()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
        animating = false;
    }

    private void ResetView()
    {
        if (homePositions == null) return;
        selectedIndex = -1;
        details.alpha = 1f;
        ((RectTransform)details.transform).anchoredPosition = detailsHome;
        details.interactable = details.blocksRaycasts = false;
        backButton.gameObject.SetActive(false);
        heading.text = "PLANTERS";
        if (detailTitle != null) detailTitle.text = "CHOOSE A PLANTER";
        summary.text = "PICK IT. PLANT IT. GROW BIG.";
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].rect.anchoredPosition = homePositions[i];
            cards[i].rect.localScale = Vector3.one;
            cards[i].group.alpha = 1f;
            cards[i].group.interactable = cards[i].group.blocksRaycasts = true;
            cards[i].background.sprite = normalCard;
        }
        Refresh();
    }

    public void SelectCard(int index)
    {
        if (animating || index < 0 || index >= cards.Count || cards[index].data == null || !cards[index].data.IsUnlocked) return;
        selectedIndex = index;
        cards[index].background.sprite = selectedCard;
        heading.text = "PLANTERS";
        summary.text = "PICK IT. PLANT IT. GROW BIG.";
        backButton.gameObject.SetActive(true);
        Refresh();
        transition = StartCoroutine(Animate(true));
    }

    public void ShowList()
    {
        if (animating || selectedIndex < 0) return;
        transition = StartCoroutine(Animate(false));
    }

    private IEnumerator Animate(bool opening)
    {
        animating = true;
        buyButton.interactable = false;
        backButton.interactable = false;
        details.blocksRaycasts = details.interactable = false;
        RectTransform detailRect = (RectTransform)details.transform;
        float duration = Mathf.Max(0.05f, transitionDuration);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1 - Mathf.Pow(1 - t, 3);
            detailRect.anchoredPosition = detailsHome + Vector2.right * (1 - ease) * 40;
            details.alpha = opening ? ease : 1 - ease;
            for (int i = 0; i < cards.Count; i++)
                cards[i].rect.localScale = Vector3.one * (1 + (opening && i == selectedIndex ? 0.025f * ease : 0));
            yield return null;
        }
        detailRect.anchoredPosition = detailsHome;
        animating = false;
        transition = null;
        backButton.interactable = true;
        if (!opening) ResetView();
        else
        {
            details.alpha = 1;
            details.blocksRaycasts = details.interactable = true;
            Refresh();
        }
    }

    private float EffectiveStat(PlanterSO data, StatType type)
    {
        return StatCalculator.Calculate(data.GetBaseStat(type), type, StatTarget.Planter,
            statManager != null ? statManager.GlobalModifiers : null, null);
    }

    private void Refresh()
    {
        foreach (Card card in cards)
        {
            bool configured = card.data != null && card.data.prefab != null;
            card.button.interactable = configured && card.data.IsUnlocked;
            card.price.text = configured ? card.data.IsUnlocked ? $"{card.data.cost} {card.data.costType}" : "SKILL TREE" : "UNAVAILABLE";
            if (card.lockedOverlay != null) card.lockedOverlay.SetActive(!card.button.interactable);
            if (card.preview != null) card.preview.color = card.button.interactable ? Color.white : new Color(.45f,.47f,.46f);
            card.background.color = card.button.interactable ? Color.white : new Color(.6f,.62f,.61f);
        }
        if (selectedIndex < 0)
        {
            buyButton.interactable = false;
            if (detailPreview != null) detailPreview.enabled = false;
            if (plotsLabel != null) plotsLabel.text = "-";
            if (sizeLabel != null) sizeLabel.text = "-";
            if (timeLabel != null) timeLabel.text = "-";
            description.text = "Choose an unlocked planter from the catalogue.";
            status.text = "";
            buyLabel.text = "BUY";
            if (buyResourceIcon != null) buyResourceIcon.enabled = false;
            return;
        }
        PlanterSO data = cards[selectedIndex].data;
        if (data == null) return;
        int cells = data.sizeX * data.sizeZ;
        if (detailTitle != null) detailTitle.text = $"{data.sizeX}x{data.sizeZ} PLANTER";
        if (detailPreview != null && cards[selectedIndex].preview != null)
        {
            detailPreview.enabled = true;
            detailPreview.sprite = cards[selectedIndex].preview.sprite;
        }
        if (plotsLabel != null) plotsLabel.text = cells.ToString();
        if (sizeLabel != null) sizeLabel.text = $"{data.sizeX}x{data.sizeZ}";
        if (timeLabel != null) timeLabel.text = $"{EffectiveStat(data, StatType.PlantSpawnRate):0.#}s";

        stats.text = $"<b>{data.planterName}</b>\n\nFOOTPRINT     {data.sizeX} x {data.sizeZ} / {cells} cells\nSPAWN INTERVAL     {EffectiveStat(data, StatType.PlantSpawnRate):0.##} s\nRARE BONUS     +{EffectiveStat(data, StatType.RareSpawnChance):0.##}%";
        StringBuilder plants = new StringBuilder();
        if (data.spawnTable != null)
            foreach (PlantSpawnEntry entry in data.spawnTable)
                if (entry != null && entry.plant != null && entry.baseChance > 0f)
                {
                    if (plants.Length > 0) plants.Append(", ");
                    plants.Append(entry.plant.plantName);
                }
        description.text = $"{cells} growing plots for {(plants.Length > 0 ? plants.ToString() : "your next crop")}.\nNeeds {cells} empty, unlocked cells. R rotates during placement.";
        int balance = resources != null ? resources.GetResourceAmount(data.costType) : 0;
        bool valid = data.prefab != null && data.sizeX > 0 && data.sizeZ > 0 && data.cost >= 0
            && data.prefab.GetComponent<PlanterBrain>() != null;
        bool affordable = resources != null && balance >= data.cost;
        buyButton.interactable = valid && data.IsUnlocked && affordable && !animating;
        buyLabel.text = $"BUY  /  {data.cost}";
        if (buyResourceIcon != null && theme != null)
        {
            buyResourceIcon.sprite = theme.ResourceIcon(data.costType);
            buyResourceIcon.enabled = buyResourceIcon.sprite != null;
        }
        status.text = !valid ? "Planter setup is incomplete."
            : !data.IsUnlocked ? "Unlock this planter in the skill tree."
            : affordable ? $"Available: {balance} {data.costType}"
            : $"Need {data.cost - balance} more {data.costType}  /  Available: {balance}";
        status.color = valid && affordable ? new Color(0.05f, 0.33f, 0.25f) : new Color(0.7f, 0.12f, 0.08f);
    }

    private void Buy()
    {
        if (animating || selectedIndex < 0 || GameManager.Instance == null
            || GameManager.Instance.CurrentState != GameStates.Shop || PlacementManager.Instance == null) return;
        Refresh();
        if (!buyButton.interactable) return;
        PlanterSO data = cards[selectedIndex].data;
        // Resource events can refresh this view synchronously. Keep this operation guarded.
        animating = true;
        buyButton.interactable = false;
        if (data.cost > 0 && !resources.SpendResource(data.costType, data.cost))
        {
            animating = false;
            Refresh();
            return;
        }
        PlacementManager.Instance.StartPlacement(data);
        // Existing PlacementManager owns placement/cancel and its existing refund policy.
        animating = false;
    }
}
