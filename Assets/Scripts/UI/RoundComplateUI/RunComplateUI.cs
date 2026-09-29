using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RunCompleteUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        // Tarla tükendiyse run'ın neden bittiği skorun üstünde yazar.
        RoundManager rounds = RoundManager.Instance;
        string reason = rounds != null && rounds.EndedByExhaustion
            ? $"<size=70%>Tarla tükendi · Round {rounds.CurrentRound}</size>\n" : "";
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