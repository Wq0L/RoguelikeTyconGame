using System.Collections;
using UnityEngine;

public class RoundUI : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text roundText;
    [SerializeField] private TMPro.TMP_Text timerText;

    private void OnEnable()
    {
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
    }
}