using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RunCompleteUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Ana menü butonu bu sahneyi yükler (MenuUI ve GameManager'ın ana menü hâli).")]
    [SerializeField] private string menuSceneName = "MenuScene";

    private void OnEnable()
    {
        // Run'ın nasıl bittiği skorun üstünde yazar: kota tutmadı (ulaşılan skor / gereken kota) ya da run kazanıldı.
        RoundManager rounds = RoundManager.Instance;
        string total = $"Harvest Score: {HarvestQuota.Format(HarvestScoreManager.Instance.TotalScore)}";
        if (rounds == null) { scoreText.text = total; return; }

        string quota = $"{HarvestQuota.Format(rounds.LastQuotaScore)} / {HarvestQuota.Format(rounds.LastQuotaTarget)}";
        string spec = SpecializationManager.Instance != null ? SpecializationPanelUI.Describe(SpecializationManager.Instance.Chosen) : null;
        total = (spec != null ? $"<size=70%>Uzmanlaşma: {spec}</size>\n" : "") + total;
        // Başlangıç seçimi ve bu run'da kalıcı olarak açılan içerik (görev).
        StartLoadoutManager loadout = StartLoadoutManager.Instance;
        if (loadout != null && loadout.Farmer != null && loadout.Scythe != null)
            total = $"<size=70%>Başlangıç: {loadout.Farmer.displayName} · {loadout.Scythe.displayName}</size>\n" + total;
        if (QuestTracker.Instance != null && QuestTracker.Instance.UnlockedThisRun.Count > 0)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (StartOptionSO option in QuestTracker.Instance.UnlockedThisRun) names.Add(option.displayName);
            total += $"\n<size=80%><color=#9BEA7C>Kalıcı olarak açıldı: {string.Join(", ", names)}</color></size>";
        }
        // Bu run'da alınan boss ödülleri.
        string rewards = BossRewardText.Summary(BossRewardManager.Instance);
        if (rewards != null) total += $"\n<size=70%>Boss ödülleri: {rewards}</size>";
        if (rounds.EndedByBoss)
        {
            // Boss round'unda iki koşul aranır; hangisinin eksik kaldığı ayrı ayrı yazılır.
            string boss = $"{HarvestQuota.Format(rounds.LastBossScore)} / {HarvestQuota.Format(rounds.LastBossTarget)}";
            string title = rounds.EndedByQuota ? "KOTA VE BOSS HASADI TUTMADI" : "BOSS HASADI TUTMADI";
            string quotaLine = rounds.EndedByQuota ? $"segment kotası {quota} (eksik)" : $"segment kotası {quota} (tamam)";
            scoreText.text = $"<size=125%><color=#FF8A7A>{title}</color></size>\n" +
                             $"<size=75%>Round {rounds.LastBossRound} · boss hasadı {boss} (eksik) · {quotaLine}</size>\n{total}";
        }
        else if (rounds.EndedByQuota)
            scoreText.text = $"<size=125%><color=#FF8A7A>KOTA TUTMADI</color></size>\n" +
                             $"<size=75%>Round {rounds.LastQuotaRound} · segment skoru {quota} (gereken kota)" +
                             (rounds.LastBossRound == rounds.LastQuotaRound && rounds.LastBossTarget > 0
                                 ? $" · boss hasadı {HarvestQuota.Format(rounds.LastBossScore)} / {HarvestQuota.Format(rounds.LastBossTarget)} (tamam)" : "") +
                             $"</size>\n{total}";
        else if (rounds.Outcome == RunOutcome.Victory)
        {
            string title = rounds.Profile != null && !string.IsNullOrEmpty(rounds.Profile.victoryTitle) ? rounds.Profile.victoryTitle : "RUN TAMAMLANDI";
            string last = rounds.LastQuotaRound > 0 ? $" · son kota {quota}" : "";
            scoreText.text = $"<size=125%><color=#9BEA7C>{title}</color></size>\n<size=75%>{rounds.MaxRounds} round{last}</size>\n{total}";
        }
        else scoreText.text = total;
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
        // Ana menü ayrı sahnede; yalnız durum değiştirmek oyuncuyu oyun sahnesinde bırakıyordu.
        if (!string.IsNullOrEmpty(menuSceneName) && SceneManager.GetActiveScene().name != menuSceneName)
            SceneManager.LoadScene(menuSceneName);
    }
}
