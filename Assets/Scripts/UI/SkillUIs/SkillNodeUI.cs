using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SkillNodeUI : MonoBehaviour, ITooltipProvider
{
    [Header("Data")]
    [SerializeField] private SkillNodeSO node;

    [Header("Referanslar")]
    [SerializeField] private Button button;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private List<Image> tierDots;
    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPrefab;

    [Header("State Renkleri")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color cantColor = Color.red;
    [SerializeField] private Color upgradeableColor = Color.yellow;
    [SerializeField] private Color maxColor = Color.green;

    [Header("Yuvarlak Renkleri")]
    [SerializeField] private Color dotFilledColor = Color.green;
    [SerializeField] private Color dotEmptyColor = Color.gray;

    [Header("Açılış Efekti")]
    [SerializeField] private RectTransform waveRing;         // Open Effect (halka transform)
    [SerializeField] private Image waveRingImage;            // Open Effect'in Image'ı (alpha için)
    [SerializeField] private float waveTargetScale = 1.8f;
    [SerializeField] private float waveDuration = 0.5f;

    [Header("Konum")]
    [SerializeField] private float spacing = 150f;

    // Profilin run başında verdiği erişim satın almadan ayrı gösterilir: düğümün altında bu yazı, satılacak bir şeyi
    // yoksa ayrıca kendi yüz rengi (yeşil "tamamlandı" değil) ve kademe noktası olmadan.
    public const string StartingAccessLabel = "BAŞLANGIÇTAN AÇIK";
    public static readonly Color32 StartingAccessColor = new Color32(134, 199, 232, 255);

    private int previousLevel = 0;
    private ResonanceBadgeGraphic skillBadge;
    private ComicPopupPlate comicFace;
    private int comicLayer;
    private RectTransform startLabel;

    public bool ShowsStartingAccess => startLabel != null && startLabel.gameObject.activeSelf;

    // Also available before Awake, for nodes initially hidden by the tree.
    public SkillNodeSO Node => node;

    public Vector2 LayoutPosition => node == null ? Vector2.zero :
        new Vector2(node.gridPosition.x * spacing, node.gridPosition.y * spacing);

    private void Awake()
    {
        DOTween.Init();
        button.onClick.AddListener(HandleClick);

        ApplyGridLayout();

        // Halka başta görünmez
        if (waveRingImage != null)
        {
            Color c = waveRingImage.color;
            c.a = 0f;
            waveRingImage.color = c;
        }
    }

    // Run profilinin ağaç seti bu yuvada başka bir düğüm gösterebilir (SkillTreeUI bağlar). null: yuva bu run'da kapalı.
    public void Bind(SkillNodeSO boundNode)
    {
        node = boundNode;
        previousLevel = 0;
        if (node == null) { gameObject.SetActive(false); return; }
        ApplyGridLayout();
        if (skillBadge != null) skillBadge.SetRecipe(SkillIconCatalog.For(node));
    }

    public void ApplyGridLayout()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition3D = new Vector3(LayoutPosition.x, LayoutPosition.y, 0f);
    }

    private void OnEnable()
    {
        ApplyGridLayout();
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (node == null || iconImage == null) return;
        if (comicFace == null)
        {
            AddPlate("Comic shadow", new Color32(32,22,39,255), new Vector2(3,-4), 0);
            AddPlate("Comic outline", new Color32(54,39,54,255), Vector2.zero, 0);
            comicFace = AddPlate("Comic face", new Color32(255,238,197,255), Vector2.zero, 4);
            dotFilledColor = new Color32(255,207,87,255);
            dotEmptyColor = new Color32(103,91,112,255);
            // Keep the existing Image as the button's hit target, not as visible artwork.
            button.transition = Selectable.Transition.None;
            frameImage.color = Color.clear;
            iconImage.rectTransform.localScale = Vector3.one;
            iconImage.rectTransform.anchorMin = Vector2.zero;
            iconImage.rectTransform.anchorMax = Vector2.one;
            iconImage.rectTransform.offsetMin = new Vector2(4,4);
            iconImage.rectTransform.offsetMax = new Vector2(-4,-4);
        }
        // This legacy white overlay covered every icon, even purchased nodes.
        if (lockOverlay != null) lockOverlay.SetActive(false);
        if (skillBadge == null)
        {
            var go = new GameObject("Comic skill icon", typeof(RectTransform), typeof(ResonanceBadgeGraphic));
            var rect = (RectTransform)go.transform; rect.SetParent(iconImage.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.layer = iconImage.gameObject.layer;
            skillBadge = go.GetComponent<ResonanceBadgeGraphic>(); skillBadge.raycastTarget = false;
        }
        iconImage.enabled = false;
        skillBadge.SetRecipe(SkillIconCatalog.For(node));
    }

    private ComicPopupPlate AddPlate(string title, Color color, Vector2 offset, float inset)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(ComicPopupPlate));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform; rect.SetParent(transform,false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = offset + Vector2.one * inset; rect.offsetMax = offset - Vector2.one * inset;
        rect.SetSiblingIndex(comicLayer++);
        var plate = go.GetComponent<ComicPopupPlate>(); plate.color = color; plate.raycastTarget = false;
        return plate;
    }

    private void OnValidate()
    {
        if (node != null && transform is RectTransform) ApplyGridLayout();
    }

    private void ResetNodeAnimation()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        ResetNodeAnimation();
        if (waveRing != null) waveRing.DOKill();
        if (waveRingImage != null)
        {
            waveRingImage.DOKill();
            var color = waveRingImage.color;
            color.a = 0f;
            waveRingImage.color = color;
        }
    }

    private void HandleClick()
    {
        bool success = SkillTreeManager.Instance.TryUpgrade(node);

        if (success)
            TooltipManager.Instance.RefreshActive();
    }

    public void Refresh()
    {
        if (node == null) { gameObject.SetActive(false); return; } // ağaç setinde kapalı yuva
        bool visible = SkillTreeManager.Instance.IsNodeVisible(node);
        gameObject.SetActive(visible);
        if (!visible) return;

        RefreshIcon();

        int level = SkillTreeManager.Instance.GetCurrentLevel(node);
        bool isMax = SkillTreeManager.Instance.IsMaxLevel(node);
        bool canUpgrade = SkillTreeManager.Instance.CanUpgrade(node);

        // Üç ayrı durum: başlangıç erişimi (profil verdi), satın alma seviyesi (noktalar), tamamlanma (yeşil).
        // Satılacak bir şeyi kalmayan başlangıç düğümü satın alınmış gibi çizilmez; stat kademeleri olan düğüm normal satılır.
        bool startingAccess = SkillTreeManager.HasStartingAccess(node);
        bool grantedOnly = SkillTreeManager.IsGrantedByProfile(node);
        bool disabled = !grantedOnly && SkillTreeManager.Instance.IsDisabledByProfile(node);
        comicFace.color = grantedOnly ? StartingAccessColor : disabled ? new Color32(150,145,150,255) : isMax ? new Color32(125,223,162,255) : canUpgrade ? new Color32(255,213,119,255) : new Color32(207,198,188,255);
        button.interactable = canUpgrade;

        UpdateTierDots(level, !grantedOnly);
        RefreshStartLabel(startingAccess, !grantedOnly);


        if (previousLevel == 0 && level == 1)
        {
            PlayOpenEffect();   // ilk açılış: dalga + punch
            PlayPunch();
        }
        else if (level > previousLevel && previousLevel > 0)
        {
            PlayShake();        // yükseltme: shake
        }

        previousLevel = level;
    }

   private void PlayOpenEffect()
    {
        if (waveRing != null && waveRingImage != null)
        {
            waveRing.localScale = Vector3.one;

            Color c = waveRingImage.color;
            c.a = 1f;
            waveRingImage.color = c;

            waveRing.DOScale(waveTargetScale, waveDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            waveRingImage.DOFade(0f, waveDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    private Color GetFrameColor(int level, bool isMax, bool canUpgrade)
    {
        if (isMax) return maxColor;
        if (level > 0) return canUpgrade ? upgradeableColor : cantColor;
        return canUpgrade ? availableColor : cantColor;
    }

    // Yazı düğümün altındadır; kademe noktaları görünüyorsa onların altına iner.
    private void RefreshStartLabel(bool show, bool belowDots)
    {
        if (!show)
        {
            if (startLabel != null) startLabel.gameObject.SetActive(false);
            return;
        }
        if (startLabel == null)
        {
            var plate = new GameObject("Starting access label", typeof(RectTransform), typeof(ComicPopupPlate));
            plate.layer = gameObject.layer;
            startLabel = (RectTransform)plate.transform;
            startLabel.SetParent(transform, false);
            startLabel.anchorMin = startLabel.anchorMax = new Vector2(0.5f, 0f);
            startLabel.pivot = new Vector2(0.5f, 1f);
            startLabel.sizeDelta = new Vector2(148f, 26f);
            var paper = plate.GetComponent<ComicPopupPlate>();
            paper.color = new Color32(255, 242, 210, 255);
            paper.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = gameObject.layer;
            textObject.SetActive(false);
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(startLabel, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5f, 1f); rect.offsetMax = new Vector2(-5f, -1f);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var theme = Resources.Load<ComicUITheme>("ComicUITheme");
            if (theme != null) theme.StylePopupText(text);
            text.text = StartingAccessLabel;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Boyut etkinleştirmeden önce verilir: verilmezse TMP ilk açılışta kendi varsayılanlarını (boyut, sarma, tıklama) yükler.
            text.fontSize = 16f;
            text.enableAutoSizing = true; text.fontSizeMin = 9f; text.fontSizeMax = 16f;
            text.raycastTarget = false;
            textObject.SetActive(true);
        }
        startLabel.anchoredPosition = new Vector2(0f, belowDots ? -24f : -7f);
        startLabel.gameObject.SetActive(true);
    }

    private void UpdateTierDots(int level, bool show)
    {
        int tierCount = node.tiers.Count;

        for (int i = 0; i < tierDots.Count; i++)
        {
            bool used = show && i < tierCount;
            tierDots[i].gameObject.SetActive(used);
            if (!used) continue;

            tierDots[i].color = i < level ? dotFilledColor : dotEmptyColor;
        }
    }

    private void PlayPunch()
    {
        ResetNodeAnimation();
        // Node büyüyüp eski haline gelir
        transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f)
            .SetUpdate(true);
    }

    private void PlayShake()
    {
        ResetNodeAnimation();
        // Node sağa-sola hafif titrer
        transform.DOShakeRotation(0.3f, new Vector3(0, 0, 15f), 10, 90, true)
            .SetUpdate(true);
    }

    // ---------- TOOLTIP ----------

    public bool ShouldShowTooltip()
    {
        // Sadece görünür node'larda tooltip çıksın
        return SkillTreeManager.Instance.IsNodeVisible(node);
    }

    public GameObject GetTooltipPrefab()
    {
        return tooltipPrefab;
    }

    public Vector3 GetTooltipPosition()
    {
        return Input.mousePosition + new Vector3(-10f, 30f, 0f);
    }

    public void FillTooltip(GameObject instance)
    {
        TooltipContent content = instance.GetComponent<TooltipContent>();
        if (content == null) return;

        int currentLevel = SkillTreeManager.Instance.GetCurrentLevel(node);
        bool isMax = SkillTreeManager.Instance.IsMaxLevel(node);

        content.SetName(node.nodeName);
        content.SetIcon(node.icon);
        content.SetSkillIcon(SkillIconCatalog.For(node));

        if (SkillTreeManager.IsGrantedByProfile(node))
        {
            content.ShowStartingAccess(StartingAccessLabel,
                GetUnlockDescription(true) + "\nBu düğüm satın alınmadı; bu profilde ücret ödenmez.");
            return;
        }

        if (SkillTreeManager.Instance.IsDisabledByProfile(node))
        {
            content.SetLevel("—");
            content.SetValues($"Bu deneyde round süresi sabit ({RoundManager.Instance.RawRoundDuration:0} sn).\nBu node etkisiz ve satın alınamaz.");
            content.SetCost("-");
            return;
        }

        if (isMax)
        {
            content.SetLevel("MAX");
            content.SetValues(BuildMaxValues());
            content.SetCost("-");
            return;
        }

        SkillNodeTier nextTier = node.tiers[currentLevel];

        content.SetLevel($"{currentLevel} / {node.tiers.Count}");
        content.SetValues(BuildUpgradeValues(nextTier));
        content.SetCost($"{nextTier.cost} {nextTier.costType}");
    }

    private string BuildUpgradeValues(SkillNodeTier nextTier)
    {
        string result = "";
        var previewModifiers = new List<StatModifier>(StatManager.Instance.GlobalModifiers);
        int currentLevel = SkillTreeManager.Instance.GetCurrentLevel(node);

        // Match TryUpgrade's replacement of the current tier without changing live stats.
        if (currentLevel > 0)
        {
            foreach (StatModifier oldEffect in node.tiers[currentLevel - 1].effects)
                previewModifiers.Remove(oldEffect);
        }

        foreach (StatModifier effect in nextTier.effects)
        {
            float current = StatManager.Instance.GetFinalStat(effect.statType, effect.target);

            float next = StatCalculator.Calculate(
                StatManager.Instance.GetBaseStat(effect.statType),
                effect.statType,
                effect.target,
                previewModifiers,
                nextTier.effects
            );

            result += $"{StatLabel(effect.statType)}: <color=#283B50>{FormatStat(effect.statType, current)}</color> → <color=#19745E>{FormatStat(effect.statType, next)}</color>\n";
        }

        return (result + GetUnlockDescription(false)).TrimEnd();
    }

    private string BuildMaxValues()
    {
        string result = "";

        SkillNodeTier lastTier = node.tiers[node.tiers.Count - 1];

        foreach (StatModifier effect in lastTier.effects)
        {
            float current = StatManager.Instance.GetFinalStat(effect.statType, effect.target);
            result += $"{StatLabel(effect.statType)}: <color=#19745E>{FormatStat(effect.statType, current)}</color>\n";
        }

        return (result + GetUnlockDescription(true)).TrimEnd();
    }

    private static string StatLabel(StatType stat) => stat switch
    {
        StatType.HarvestDamage => "Hasar", StatType.GridUnlockSize => "Grid",
        StatType.RoundDuration => "Round süresi", StatType.AttackSpeed => "Atak aralığı",
        StatType.CritMultiplier => "Kritik çarpanı", StatType.AreaRadius => "Vuruş yarıçapı",
        _ => TileBuffText.Name(stat)
    };

    private static string FormatStat(StatType stat, float value) => stat switch
    {
        StatType.GridUnlockSize => $"{value:0}×{value:0}",
        // 60 sn'nin üstü round'u uzatmaz, saldırı ve üretim hızına dönüşür.
        StatType.RoundDuration when value > RoundManager.RoundSecondsCap =>
            $"{RoundManager.RoundSecondsCap:0} sn · +%{(Mathf.Min(value, 90f) / RoundManager.RoundSecondsCap - 1f) * 100f:0} hız",
        // Taban: bundan sonraki üretim hızı saksılarda nadir bitki şansına dönüşür.
        StatType.PlantSpawnRate when value <= StatCalculator.MinimumSpawnInterval + 1e-3f => $"{value:0.###} sn (taban · fazlası nadirliğe)",
        StatType.RoundDuration or StatType.AttackSpeed or StatType.PlantSpawnRate => $"{value:0.###} sn",
        StatType.CritChance => $"%{value * 100f:0.##}",
        StatType.CritMultiplier => $"×{value:0.###}",
        // Şans değeri yerine kartta ne göreceği: tek kartta Epic+ ve Legendary olasılığı.
        StatType.MutationLuck =>
            $"Epic+ %{CardSelectionUI.RarityWeight(value, TileRarity.Epic) + CardSelectionUI.RarityWeight(value, TileRarity.Legendary):0} · Leg %{CardSelectionUI.RarityWeight(value, TileRarity.Legendary):0}",
        _ => value.ToString("0.###")
    };

    private string GetUnlockDescription(bool completed)
    {
        string planter = node.unlockType switch
        {
            UnlockType.Planter_1x3 => "1×3", UnlockType.Planter_2x2 => "2×2",
            UnlockType.Planter_2x3 => "2×3", _ => null
        };
        // Kilidi profil run başında verdiyse düğüm onu "açmaz": kademeler yalnız kendi statlarını satar.
        bool fromStart = SkillTreeManager.HasStartingAccess(node);
        if (planter != null)
            return fromStart ? $"{planter} saksı başlangıçtan açık (mağazada)." : completed ? $"{planter} saksı mağazada açık." : $"{planter} saksıyı mağazada açar.";
        string card = node.unlockType switch
        {
            UnlockType.TileBehavior_Explosive => "Patlama",
            UnlockType.TileBehavior_Duplicate => "Çoğaltma",
            UnlockType.TileBehavior_Tornado => "Tornado",
            UnlockType.TileBehavior_Boomerang => "Bumerang Orak",
            UnlockType.TileBehavior_Electric => "Çapraz Elektrik",
            _ => null
        };
        if (card == null) return string.Empty;
        if (fromStart) return $"{card} kartları başlangıçtan açık: run başından beri seçim havuzunda.";
        return completed
            ? $"{card} kartları seçim havuzuna eklendi."
            : $"Son seviyede {card} kartlarını seçim havuzuna ekler.";
    }
}
