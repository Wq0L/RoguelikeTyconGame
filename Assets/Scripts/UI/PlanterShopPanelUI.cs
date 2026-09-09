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
    [SerializeField] private float selectedX = -570f;

    private Vector2[] homePositions;
    private int selectedIndex = -1;
    private Coroutine transition;
    private bool animating;
    private ResourceManager resources;
    private StatManager statManager;

    private void Awake()
    {
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
        if (resources != null) resources.OnResourceAmountChanged += OnResourceChanged;
        if (statManager != null) statManager.OnStatChanged += OnStatChanged;
        ResetView();
    }

    private void OnDisable()
    {
        if (resources != null) resources.OnResourceAmountChanged -= OnResourceChanged;
        if (statManager != null) statManager.OnStatChanged -= OnStatChanged;
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
        details.alpha = 0f;
        details.interactable = details.blocksRaycasts = false;
        backButton.gameObject.SetActive(false);
        heading.text = "PLANTER LAB";
        summary.text = "Choose a planter to inspect its production and price.";
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].rect.anchoredPosition = homePositions[i];
            cards[i].group.alpha = 1f;
            cards[i].group.interactable = cards[i].group.blocksRaycasts = true;
            cards[i].background.sprite = normalCard;
        }
        Refresh();
    }

    public void SelectCard(int index)
    {
        if (animating || selectedIndex >= 0 || index < 0 || index >= cards.Count) return;
        selectedIndex = index;
        cards[index].background.sprite = selectedCard;
        heading.text = "PLANTER DETAILS";
        summary.text = "Inspect the planter, then buy and place it on the grid.";
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
        var starts = new Vector2[cards.Count];
        var alphas = new float[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            starts[i] = cards[i].rect.anchoredPosition;
            alphas[i] = cards[i].group.alpha;
            cards[i].group.interactable = cards[i].group.blocksRaycasts = false;
        }
        float initialDetails = details.alpha;
        float duration = Mathf.Max(0.05f, transitionDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < cards.Count; i++)
            {
                Vector2 target = opening
                    ? new Vector2(i == selectedIndex ? selectedX : homePositions[i].x + 1600f, homePositions[i].y)
                    : homePositions[i];
                cards[i].rect.anchoredPosition = Vector2.LerpUnclamped(starts[i], target, ease);
                cards[i].group.alpha = Mathf.Lerp(alphas[i], opening && i != selectedIndex ? 0f : 1f, ease);
            }
            details.alpha = Mathf.Lerp(initialDetails, opening ? 1f : 0f, opening ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.75f)) : ease);
            yield return null;
        }
        animating = false;
        transition = null;
        backButton.interactable = true;
        if (!opening) ResetView();
        else
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].rect.anchoredPosition = new Vector2(i == selectedIndex ? selectedX : homePositions[i].x + 1600f, homePositions[i].y);
                cards[i].group.alpha = i == selectedIndex ? 1f : 0f;
            }
            details.alpha = 1f;
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
            card.button.interactable = configured;
            card.price.text = configured ? $"{card.data.cost} {card.data.costType}" : "Not configured";
        }
        if (selectedIndex < 0) { buyButton.interactable = false; return; }
        PlanterSO data = cards[selectedIndex].data;
        if (data == null) return;
        int cells = data.sizeX * data.sizeZ;
        stats.text = $"<b>{data.planterName}</b>\n\nFOOTPRINT     {data.sizeX} x {data.sizeZ} / {cells} cells\nSPAWN INTERVAL     {EffectiveStat(data, StatType.PlantSpawnRate):0.##} s\nRARE BONUS     +{EffectiveStat(data, StatType.RareSpawnChance):0.##}%";
        StringBuilder plants = new StringBuilder();
        if (data.spawnTable != null)
            foreach (PlantSpawnEntry entry in data.spawnTable)
                if (entry != null && entry.plant != null && entry.baseChance > 0f)
                {
                    if (plants.Length > 0) plants.Append(", ");
                    plants.Append(entry.plant.plantName);
                }
        description.text = $"<b>PRODUCTION</b>\n{(plants.Length > 0 ? plants.ToString() : "No plants configured")}\n\nNeeds {cells} empty, unlocked grid cells.\nR rotates the planter during placement.\n\nValues include global upgrades. Tile bonuses apply after placement.";
        int balance = resources != null ? resources.GetResourceAmount(data.costType) : 0;
        bool valid = data.prefab != null && data.sizeX > 0 && data.sizeZ > 0 && data.cost >= 0
            && data.prefab.GetComponent<PlanterBrain>() != null;
        bool affordable = resources != null && balance >= data.cost;
        buyButton.interactable = valid && affordable && !animating;
        buyLabel.text = $"BUY  /  {data.cost} {data.costType}";
        status.text = !valid ? "Planter setup is incomplete."
            : affordable ? $"Available: {balance} {data.costType}"
            : $"Need {data.cost - balance} more {data.costType}  /  Available: {balance}";
        status.color = valid && affordable ? new Color(0.45f, 0.9f, 0.83f) : new Color(1f, 0.63f, 0.47f);
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
