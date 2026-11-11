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
        QuotaHUD.Attach((RectTransform)transform);
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (RoundManager.Instance == null)
            yield return null;

        RoundManager.Instance.OnRoundChanged += UpdateRoundText;
        RoundManager.Instance.OnTimeChanged += UpdateTimerText;
        GameText.Changed -= RefreshLanguage;
        GameText.Changed += RefreshLanguage;

        UpdateRoundText(RoundManager.Instance.CurrentRound);
        UpdateTimerText(Mathf.CeilToInt(RoundManager.Instance.RemainingTime));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetTimerVisual();
        GameText.Changed -= RefreshLanguage;

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnRoundChanged -= UpdateRoundText;
            RoundManager.Instance.OnTimeChanged -= UpdateTimerText;
        }
    }

    // Kart biçimli HUD (ComicHud): "ROUND" kartın başlığında yazar, bu alan yalnız sayıları gösterir.
    private bool compactRound;
    public void UseCompactRound()
    {
        compactRound = true;
        if (RoundManager.Instance != null) UpdateRoundText(RoundManager.Instance.CurrentRound);
    }

    // Dil değişince (ör. duraklatıp ayarlardan) yazılar hemen yeni dilde: süre bir sonraki saniyeyi beklemez. Yalnız yazı;
    // son saniye nabzı tekrar oynamaz (6 Kasım 2026, ComicKitVerification dil turu yakaladı).
    private void RefreshLanguage()
    {
        if (RoundManager.Instance == null) return;
        UpdateRoundText(RoundManager.Instance.CurrentRound);
        if (timerText != null) timerText.text = GameText.Format("ui.hud.time", Mathf.CeilToInt(RoundManager.Instance.RemainingTime));
    }

    private void UpdateRoundText(int roundNumber)
    {
        roundText.text = GameText.Format(compactRound ? "ui.hud.round_value" : "ui.hud.round", roundNumber, RoundManager.Instance.MaxRounds);
    }

    private void UpdateTimerText(int secondsLeft)
    {
        timerText.text = GameText.Format("ui.hud.time", secondsLeft);

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

    // HUD tek stil (ComicHud): süre krem plakada mürekkep rengiyle yazılır; son saniye uyarısından sonra bu renge döner.
    public void SetTimerRestColor(Color color)
    {
        timerRestColor = color;
        hasTimerRestColor = true;
        if (timerText != null) timerText.color = color;
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