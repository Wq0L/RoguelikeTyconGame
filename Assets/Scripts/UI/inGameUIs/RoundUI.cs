using System.Collections;
using DG.Tweening;
using UnityEngine;

public class RoundUI : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text roundText;
    [SerializeField] private TMPro.TMP_Text timerText;

    [Header("Final Seconds")]
    [SerializeField, Min(0)] private int warningSeconds = 5;
    [SerializeField] private Color warningColor = new Color32(255, 96, 80, 255);

    private Color timerRestColor;
    private Vector3 timerRestScale = Vector3.one;
    private bool hasTimerRestColor;

    private void OnEnable()
    {
        if (!hasTimerRestColor && timerText != null)
        {
            timerRestColor = timerText.color;
            timerRestScale = timerText.transform.localScale;
            hasTimerRestColor = true;
        }
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (RoundManager.Instance == null)
            yield return null;

        RoundManager.Instance.OnRoundChanged += UpdateRoundText;
        RoundManager.Instance.OnTimeChanged += UpdateTimerText;

        UpdateRoundText(RoundManager.Instance.CurrentRound);
        UpdateTimerText(Mathf.CeilToInt(RoundManager.Instance.RemainingTime));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetTimerVisual();

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnRoundChanged -= UpdateRoundText;
            RoundManager.Instance.OnTimeChanged -= UpdateTimerText;
        }
    }

    private void UpdateRoundText(int roundNumber)
    {
        roundText.text = $"Round {roundNumber} / {RoundManager.Instance.MaxRounds}";
    }

    private void UpdateTimerText(int secondsLeft)
    {
        timerText.text = $"Time: {secondsLeft}s";

        // Son saniyeler: her saniye kırmızı nabız.
        bool warning = secondsLeft > 0 && secondsLeft <= warningSeconds;
        if (!warning)
        {
            ResetTimerVisual();
            return;
        }
        Transform timer = timerText.transform;
        timer.DOKill();
        timer.localScale = timerRestScale;
        timer.DOPunchScale(timerRestScale * 0.28f, 0.35f, 6, 0.6f).SetUpdate(true);
        timerText.color = warningColor;
    }

    private void ResetTimerVisual()
    {
        if (timerText == null) return;
        timerText.transform.DOKill();
        if (!hasTimerRestColor) return;
        timerText.transform.localScale = timerRestScale;
        timerText.color = timerRestColor;
    }
}