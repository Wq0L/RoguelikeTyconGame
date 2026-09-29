using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class XpUI : MonoBehaviour
{
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMPro.TMP_Text levelText;
    private Vector3 levelRestScale = Vector3.one;
    private bool hasLevelRestScale;

    private void OnEnable()
    {
        if (!hasLevelRestScale && levelText != null)
        {
            levelRestScale = levelText.transform.localScale;
            hasLevelRestScale = true;
        }
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (ProgressionManager.Instance == null)
            yield return null;

        ProgressionManager.Instance.OnXPChanged += UpdateXPBar;
        ProgressionManager.Instance.OnLevelUp += HandleLevelUp;

        UpdateXPBar();
        UpdateLevelText(ProgressionManager.Instance.CurrentLevel);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (levelText != null)
        {
            levelText.transform.DOKill();
            if (hasLevelRestScale) levelText.transform.localScale = levelRestScale;
        }

        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnXPChanged -= UpdateXPBar;
            ProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    private void UpdateXPBar()
    {
        float next = ProgressionManager.Instance.XPToNextLevel;
        float current = ProgressionManager.Instance.CurrentXP;

        float ratio = (next > 0f) ? current / next : 0f;

        xpSlider.value = ratio;
    }

    // Level atlayınca yazı zıplar (ilk açılıştaki güncellemede değil).
    private void HandleLevelUp(int level)
    {
        UpdateLevelText(level);
        Transform label = levelText.transform;
        label.DOKill();
        label.localScale = levelRestScale;
        label.DOPunchScale(levelRestScale * 0.45f, 0.4f, 7, 0.6f).SetUpdate(true);
    }

    private void UpdateLevelText(int level)
    {
        levelText.text = $"Lv. {level}";
        UpdateXPBar();
    }
}