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

    private void Awake()
    {
        DOTween.Init();
        button.onClick.AddListener(HandleClick);

        RectTransform rect = GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(
            node.gridPosition.x * spacing,
            node.gridPosition.y * spacing
        );

        // Halka başta görünmez
        if (waveRingImage != null)
        {
            Color c = waveRingImage.color;
            c.a = 0f;
            waveRingImage.color = c;
        }
    }

    private void HandleClick()
    {
        SkillTreeManager.Instance.TryUpgrade(node);
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
        // Node büyüyüp eski haline gelir
        transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f)
            .SetUpdate(true);
    }

    private void PlayShake()
    {
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
        // ŞİMDİLİK BOŞ
    }
}