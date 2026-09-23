using System.Collections.Generic;
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

    private int previousLevel = 0;

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

    public void ApplyGridLayout()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition3D = new Vector3(LayoutPosition.x, LayoutPosition.y, 0f);
    }

    private void OnEnable()
    {
        ApplyGridLayout();
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
        bool visible = SkillTreeManager.Instance.IsNodeVisible(node);
        gameObject.SetActive(visible);
        if (!visible) return;

        if (iconImage != null && node.icon != null)
            iconImage.sprite = node.icon;

        int level = SkillTreeManager.Instance.GetCurrentLevel(node);
        bool isMax = SkillTreeManager.Instance.IsMaxLevel(node);
        bool canUpgrade = SkillTreeManager.Instance.CanUpgrade(node);

        frameImage.color = GetFrameColor(level, isMax, canUpgrade);
        button.interactable = canUpgrade;

        UpdateTierDots(level);


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

    private void UpdateTierDots(int level)
    {
        int tierCount = node.tiers.Count;

        for (int i = 0; i < tierDots.Count; i++)
        {
            bool used = i < tierCount;
            tierDots[i].gameObject.SetActive(used);
            if (!used) continue;

            tierDots[i].color = (i < level) ? dotFilledColor : dotEmptyColor;
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
        StatType.RoundDuration or StatType.AttackSpeed or StatType.PlantSpawnRate => $"{value:0.###} sn",
        StatType.CritChance => $"%{value * 100f:0.##}",
        StatType.CritMultiplier => $"×{value:0.###}",
        _ => value.ToString("0.###")
    };

    private string GetUnlockDescription(bool completed)
    {
        string planter = node.unlockType switch
        {
            UnlockType.Planter_1x3 => "1×3", UnlockType.Planter_2x2 => "2×2",
            UnlockType.Planter_2x3 => "2×3", _ => null
        };
        if (planter != null) return completed ? $"{planter} saksı mağazada açık." : $"{planter} saksıyı mağazada açar.";
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
        return completed
            ? $"{card} kartları seçim havuzuna eklendi."
            : $"Son seviyede {card} kartlarını seçim havuzuna ekler.";
    }
}
