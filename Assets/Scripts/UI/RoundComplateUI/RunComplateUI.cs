using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RunCompleteUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        // Kota tutmadıysa run'ın neden bittiği skorun üstünde yazar.
        RoundManager rounds = RoundManager.Instance;
        string reason = rounds != null && rounds.EndedByQuota
            ? $"<size=70%>Kota tutmadı · Round {rounds.LastQuotaRound} · {HarvestQuota.Format(rounds.LastQuotaScore)} / {HarvestQuota.Format(rounds.LastQuotaTarget)}</size>\n" : "";
        scoreText.text = $"{reason}Harvest Score: {HarvestScoreManager.Instance.TotalScore}";
    }

        public void OnRestartPressed()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenuPressed()
    {
        HarvestScoreManager.Instance.ResetScore();
        RoundManager.Instance.ResetRounds();
        StatManager.Instance.ClearGlobalModifiers();

        GameManager.Instance.ReturnToMenu();
    }
}