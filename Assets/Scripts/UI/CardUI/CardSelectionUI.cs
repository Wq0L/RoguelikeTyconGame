using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSelectionUI : MonoBehaviour
{
    [SerializeField] private List<TileModifierSO> allModifiers; // tüm SO'lar buraya
    [SerializeField] private List<CardUI> cardSlots;            // 3 kart slotu
    [SerializeField] private Button skipButton;

    // Tip ağırlıkları — sabit
    private Dictionary<TileModifierType, float> typeWeights = new()
    {
        { TileModifierType.Fertile,   25f },
        { TileModifierType.Water,     20f },
        { TileModifierType.Crystal,   20f },
        { TileModifierType.Energy,    15f },
        { TileModifierType.Explosive, 12f },
        { TileModifierType.Duplicate,  8f },
        { TileModifierType.Damage,    15f },
        { TileModifierType.Tornado,   10f },
        { TileModifierType.Boomerang, 10f },
        { TileModifierType.Electric,  10f }
    };

    private Dictionary<TileRarity, int> skipBaseRewards = new()
    {
        { TileRarity.Common,    2 },
        { TileRarity.Rare,      4 },
        { TileRarity.Epic,      7 },
        { TileRarity.Legendary, 10 }
    };
    private List<TileCardOffer> currentCards = new();
    private readonly List<TileModifierSO> availableModifiers = new();
    private readonly List<TileModifierSO> behaviorPool = new();
    // Son teklifte erken davranış slotu olarak dağıtılan kart (yoksa null). Test ve ölçüm için.
    public TileCardOffer GuaranteedBehaviorOffer { get; private set; }
    

    public void RefreshCards()
    {
        CardSelectionVignette.Attach((RectTransform)transform);
        currentCards.Clear();
        availableModifiers.Clear();
        foreach (var modifier in allModifiers)
            if (modifier != null && modifier.IsAvailableInCardPool &&
                !availableModifiers.Contains(modifier)) availableModifiers.Add(modifier);

        // Never fall back to locked cards or trap the player in an empty selection.
        if (availableModifiers.Count == 0)
        {
            foreach (var slot in cardSlots)
                if (slot != null) slot.gameObject.SetActive(false);
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            Debug.LogWarning("Kart havuzunda açık kart yok. Kart seçimleri ödülsüz geçiliyor.", this);
            if (GameManager.Instance.CurrentState == GameStates.CardSelection)
                while (RoundManager.Instance.OnCardSelectionComplete()) { }
            return;
        }

        // Grid'de açık boş hücre yoksa kartlar yeni tile yerine mevcut tile'ları yükseltir;
        // yükseltilecek tile da kalmadıysa Temel güç verir. Nadirlik aynı şansla atılır.
        ProgressionManager progression = ProgressionManager.Instance;
        bool gridFull = progression != null && !progression.HasEligibleCell();
        List<GroundCell> upgrades = gridFull ? progression.GetUpgradeCandidates(cardSlots.Count) : null;
        SetBanner(!gridFull ? null : upgrades.Count > 0
            ? "GRİD DOLU · kart, seçtiğin tile'ı seviye atlatır"
            : "TÜM TILE'LAR MAX · kart kalıcı Temel güç verir");
        int upgradeIndex = 0;
        // Erken davranış teklifi (Bölüm 3.6): bir slot davranış adaylarından gelir; hangi slot olduğu ve kartın kendisi rastgeledir.
        int behaviorSlot = gridFull ? -1 : FirstBehaviorSlot();
        GuaranteedBehaviorOffer = null;
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] == null) continue;
            cardSlots[i].gameObject.SetActive(true);
            TileCardOffer offer = !gridFull ? new TileCardOffer(i == behaviorSlot ? RollRarity(behaviorPool) : RollCard())
                : upgradeIndex < upgrades.Count ? TileCardOffer.Upgrade(upgrades[upgradeIndex++], RollRarityTier())
                : RollBaseStat();
            if (i == behaviorSlot) GuaranteedBehaviorOffer = offer;
            currentCards.Add(offer);
            cardSlots[i].Setup(offer, OnCardSelected);
        }

        if(skipButton != null)
        {
            bool canSkip = RoundManager.Instance.SkipUsesRemaining > 0;
            skipButton.gameObject.SetActive(canSkip);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkipPressed);
        }

        DealCards();
    }

    // Kartlar sırayla, hafif dönerek masaya "dağıtılır". Pop'un ilk yarısında tıklama kapalı:
    // arka arkaya seçimlerde yeni gelen karta yanlışlıkla basılmaz.
    private void DealCards()
    {
        int dealt = 0;
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] == null || !cardSlots[i].gameObject.activeInHierarchy) continue;
            float angle = dealt % 2 == 0 ? -8f : 8f;
            UIPop.For(cardSlots[i]).Play(0.06f + dealt * 0.08f, 0.34f, angle, lockInput: true);
            dealt++;
        }
        if (skipButton != null && skipButton.gameObject.activeInHierarchy)
            UIPop.For(skipButton).Play(0.1f + dealt * 0.08f);
    }

    // Erken davranış slotunun dizini; koşullar sağlanmıyorsa -1 (normal dağılım). Koşullar: profil bu teklifi tanımlıyor, o türlerden
    // kart henüz alınmamış, tarlada kartı kullanabilecek en az bir saksı var ve havuzda o türlerden açık kart var.
    private int FirstBehaviorSlot()
    {
        behaviorPool.Clear();
        ProgressionManager progression = ProgressionManager.Instance;
        if (progression == null || progression.FirstBehaviorCardRound != 0 || PlantSpawner.Active.Count == 0) return -1;
        foreach (TileModifierSO modifier in availableModifiers)
            if (RunBalanceSO.IsFirstBehaviorType(modifier.modifierType)) behaviorPool.Add(modifier);
        if (behaviorPool.Count == 0) return -1;
        int slots = 0;
        foreach (CardUI slot in cardSlots) if (slot != null) slots++;
        if (slots == 0) return -1;
        int pick = Random.Range(0, slots);
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] == null) continue;
            if (pick-- == 0) return i;
        }
        return -1;
    }

    private TileModifierSO RollCard()
    {
        TileModifierType selectedType = RollType();

        List<TileModifierSO> filtered = availableModifiers.FindAll(m => m.modifierType == selectedType);
        if (filtered.Count == 0) return availableModifiers[Random.Range(0, availableModifiers.Count)];

        return RollRarity(filtered);
    }

    private TileModifierType RollType()
    {
        float total = 0f;
        foreach (var w in typeWeights)
            if (availableModifiers.Exists(m => m.modifierType == w.Key)) total += w.Value;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var w in typeWeights)
        {
            if (!availableModifiers.Exists(m => m.modifierType == w.Key)) continue;
            cumulative += w.Value;
            if (roll <= cumulative) return w.Key;
        }

        return TileModifierType.Fertile;
    }

    private TileModifierSO RollRarity(List<TileModifierSO> filtered)
    {
        TileRarity selectedRarity = RollRarityTier();
        List<TileModifierSO> rarityFiltered = filtered.FindAll(m => m.rarity == selectedRarity);
        if (rarityFiltered.Count == 0) rarityFiltered = filtered;

        return rarityFiltered[Random.Range(0, rarityFiltered.Count)];
    }

    // Kart nadirliği kademeleri (Common, Rare, Epic, Legendary; her satır toplam 100).
    // Kart Sezgisi'nin her kademesi (+0.0333 şans) bir satır ilerletir; ağaç tamken (9 kademe)
    // kartlar çoğunlukla Epic ve Legendary, Common ve Rare nadir. Ara şans değerleri iki satır arasında yumuşar.
    public const float LuckPerTier = 0.1f / 3f;
    private static readonly float[,] RarityTiers =
    {
        { 60, 25, 12,  3 },
        { 49, 28, 18,  5 },
        { 38, 30, 23,  9 },
        { 30, 29, 29, 12 },
        { 22, 27, 34, 17 },
        { 15, 23, 39, 23 },
        { 10, 18, 42, 30 },
        {  7, 14, 43, 36 },
        {  4, 10, 43, 43 },
        {  3,  7, 42, 48 },
    };

    // Bu nadirliğin tek kartta çıkma yüzdesi (skill tooltip'i de buradan okur).
    public static float RarityWeight(float luck, TileRarity rarity)
    {
        int last = RarityTiers.GetLength(0) - 1;
        float tier = Mathf.Clamp(luck / LuckPerTier, 0f, last);
        int low = Mathf.Min(Mathf.FloorToInt(tier), last), high = Mathf.Min(low + 1, last);
        return Mathf.Lerp(RarityTiers[low, (int)rarity], RarityTiers[high, (int)rarity], tier - low);
    }

    private TileRarity RollRarityTier()
    {
        float luck = StatManager.Instance.GetFinalStat(StatType.MutationLuck, StatTarget.Mutation);
        float roll = Random.Range(0f, 100f), cumulative = 0f;
        for (int i = 0; i <= (int)TileRarity.Legendary; i++)
        {
            cumulative += RarityWeight(luck, (TileRarity)i);
            if (roll <= cumulative) return (TileRarity)i;
        }
        return TileRarity.Legendary;
    }

    // Yükseltilecek tile kalmadığında: kalıcı, küçük global güç. Nadirlik miktarı büyütür.
    private static readonly (string name, StatType[] stats, StatTarget target, float sign)[] BaseStats =
    {
        ("Hasat hasarı", new[] { StatType.HarvestDamage }, StatTarget.Player, 1f),
        ("Atak aralığı", new[] { StatType.AttackSpeed }, StatTarget.Player, -1f),
        ("Üretim süresi", new[] { StatType.PlantSpawnRate }, StatTarget.Planter, -1f),
        ("Tüm kaynaklar", new[] { StatType.GoldGainMultiplier, StatType.IronGainMultiplier, StatType.StoneGainMultiplier }, StatTarget.Planter, 1f),
        ("XP kazancı", new[] { StatType.XPGainMultiplier }, StatTarget.Planter, 1f),
    };

    private TileCardOffer RollBaseStat()
    {
        TileRarity rarity = RollRarityTier();
        float amount = rarity switch { TileRarity.Legendary => .05f, TileRarity.Epic => .04f, TileRarity.Rare => .03f, _ => .02f };
        var pick = BaseStats[Random.Range(0, BaseStats.Length)];
        var modifiers = new List<StatModifier>();
        foreach (StatType stat in pick.stats)
            modifiers.Add(new StatModifier { statType = stat, target = pick.target, operation = ModifierOperation.MorePercent, value = amount * pick.sign });
        return TileCardOffer.BaseStat(pick.name, rarity, modifiers);
    }

    private TextMeshProUGUI banner;

    // Kart ekranının üstünde, grid dolduğunda kartların ne yaptığını söyleyen şerit. Kart dizilimine girmez.
    private void SetBanner(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (banner != null) banner.transform.parent.gameObject.SetActive(false);
            return;
        }
        if (banner == null)
        {
            var plate = new GameObject("Grid Full Banner", typeof(RectTransform), typeof(LayoutElement));
            plate.layer = gameObject.layer;
            plate.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)plate.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -118f);
            rect.sizeDelta = new Vector2(760f, 64f);
            AddPlate(rect, "Ink", new Color32(54, 39, 54, 255), 0f);
            AddPlate(rect, "Face", FreshTileRing.GlowColor, 3f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.gameObject.layer = gameObject.layer;
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 4f);
            label.rectTransform.offsetMax = new Vector2(-16f, -4f);
            FeelOverlay.Theme?.StylePopupText(label);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 30f;
            label.raycastTarget = false;
            banner = label;
        }
        banner.text = text;
        banner.transform.parent.gameObject.SetActive(true);
        banner.transform.parent.SetAsLastSibling();
    }

    private static void AddPlate(RectTransform parent, string name, Color color, float inset)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
        var plate = rect.gameObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
    }

    private void OnCardSelected(TileCardOffer offer)
    {
        if (offer == null || !currentCards.Contains(offer)) return;
        TileModifierSO modifier = offer.Tile;
        // Sadece CardSelection state'inde çalış
        if (GameManager.Instance.CurrentState != GameStates.CardSelection)
            return;

        if (offer.IsUpgrade)
            ProgressionManager.Instance.ApplyUpgrade(offer.UpgradeTarget, offer.Rarity);
        else if (offer.IsBaseStat)
            foreach (StatModifier mod in offer.Modifiers) StatManager.Instance.AddGlobalModifier(mod);
        else
        {
            if (!modifier.IsAvailableInCardPool)
            {
                RefreshCards();
                return;
            }
            ProgressionManager.Instance.ApplyRandomEligibleCell(modifier, offer.Modifiers);
        }
        currentCards.Clear(); // The displayed offer cannot be consumed twice.

        bool hasMore = RoundManager.Instance.OnCardSelectionComplete();

        if (hasMore)
            RefreshCards();
    }

    private void OnSkipPressed()
    {
        // Atlama yalnız ekrandaki teklifi tüketir: seçim bittikten sonraki ikinci basış yeni hak harcamaz ve durum değiştirmez.
        if (GameManager.Instance.CurrentState != GameStates.CardSelection || currentCards.Count == 0) return;
        if (!RoundManager.Instance.TryUseSkip()) return;

        // 3 karttan en rare olanı bul
        TileRarity highest = TileRarity.Common;
        foreach (var card in currentCards)
            if (card.Rarity > highest) highest = card.Rarity;

        // Ödül = base × (1 + round × 0.1)
        int round = RoundManager.Instance.CurrentRound;
        int baseReward = skipBaseRewards[highest];
        int reward = Mathf.RoundToInt(baseReward * (1f + round * 0.1f));

        // Random resource seç
        ResourceType[] resources = { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone };
        ResourceType chosen = resources[Random.Range(0, resources.Length)];

        ResourceManager.Instance.AddResource(chosen, reward);
        // Debug.Log($"Skip! {highest} → {chosen} x{reward}");

        // Level takas — seçimi tüket
        currentCards.Clear(); // The skipped offer cannot be consumed twice.
        bool hasMore = RoundManager.Instance.OnCardSelectionComplete();
        if (hasMore)
            RefreshCards();
    }


    private void OnDisable()
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(false);
    }
}
