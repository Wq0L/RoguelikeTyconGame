using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class XpUI : MonoBehaviour
{
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMPro.TMP_Text levelText;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (ProgressionManager.Instance == null)
            yield return null;

        ProgressionManager.Instance.OnXPChanged += UpdateXPBar;
        ProgressionManager.Instance.OnLevelUp += UpdateLevelText;

        UpdateXPBar();
        UpdateLevelText(ProgressionManager.Instance.CurrentLevel);
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnXPChanged -= UpdateXPBar;
            ProgressionManager.Instance.OnLevelUp -= UpdateLevelText;
        }
    }

    private void UpdateXPBar()
    {
        float ratio = ProgressionManager.Instance.CurrentXP / ProgressionManager.Instance.XPToNextLevel;
        xpSlider.value = ratio;
    }

    private void UpdateLevelText(int level)
    {
        levelText.text = $"Lv. {level}";
        UpdateXPBar();
    }
}