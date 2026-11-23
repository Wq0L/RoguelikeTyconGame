using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run sonu ekranı. 22 Kasım 2026 (kullanıcı: "yanınca gördüğümüz UI'ı güzelleştirmek lazım; öldüğümüzde hissedilsin; emsallere bak;
// kazanınca da aynı his veriyor; açıklama yazıları rahatsız etti; kaybedince nasıl kaybettiğimizi anlamıyoruz"). Emsal: Balatro
// (hedef ile skor yan yana + "seni yenen" rozeti, kayıp / zafer bütün ekranın rengini değiştirir), Slay the Spire (öldüren sahnede kalır).
// Comic kit yerleşimi sahnede (Tools > Comic UI > Run Sonu Ekranı): ortada tek panel; damga başlık (kayıp kırmızı "RUN BİTTİ", zafer
// altın "ZAFER!"); solda NEDEN bloğu: boss rozeti (boss'un kendi renginde) + KOTA ve BOSS HASADI çubukları (hedef / senin değerin;
// tutmayan kırmızı, tutan yeşil); sağda kısa tablo (skor, round, boss ödülü, süre); altta TEKRAR (mavi) / ANA MENÜ (kırmızı).
// Açıklama cümlesi yok. Kod yalnız değerleri, çubukları ve plaka rengini doldurur; dil değişince hemen yeniler.
// Kit alanları boşsa eski yol (tek yazı + RunResultLayout).
public class RunCompleteUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Ana menü butonu bu sahneyi yükler (MenuUI ve GameManager'ın ana menü hâli).")]
    [SerializeField] private string menuSceneName = "MenuScene";
    [Header("Comic kit (Tools > Comic UI > Run Sonu Ekranı)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image titlePlate;
    [SerializeField] private Sprite failPlate, winPlate;
    [SerializeField] private TMP_Text scoreValue, roundValue, rewardCountValue, timeValue;
    [Header("Neden bloğu")]
    [SerializeField] private TMP_Text causeHeader;
    [SerializeField] private GameObject bossBadge;
    [SerializeField] private TMP_Text bossName;
    [SerializeField] private TMP_Text quotaValue;
    [SerializeField] private RectTransform quotaFill;
    [SerializeField] private Image quotaFillImage;
    [SerializeField] private GameObject bossRow;
    [SerializeField] private TMP_Text bossValue;
    [SerializeField] private RectTransform bossFill;
    [SerializeField] private Image bossFillImage;
    [SerializeField] private GameObject allMetNote;   // zaferde gösterilecek hedef yoksa: "TÜM KOTALAR TUTTU"

    private static readonly Color Short = new Color32(222, 72, 58, 255), Met = new Color32(98, 176, 74, 255);
    private bool capsBound;

    public bool UsesKit => titleText != null;
    public bool Won { get; private set; }

    private void OnEnable()
    {
        GameText.Changed += Refill;
        Refill();
    }

    private void OnDisable() => GameText.Changed -= Refill;

    private void Refill()
    {
        if (UsesKit) FillKit();
        else Legacy();
    }

    private void FillKit()
    {
        if (!capsBound)
        {
            capsBound = true;
            ComicUITheme theme = FeelOverlay.Theme;
            if (theme != null && theme.displayFont != null)
                foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                    if (text.font == theme.displayFont) text.textPreprocessor = ComicKit.TurkishCaps.Instance;
        }
        RoundManager rounds = RoundManager.Instance;
        HarvestScoreManager score = HarvestScoreManager.Instance;
        Won = rounds != null && rounds.Outcome == RunOutcome.Victory;

        titleText.text = GameText.Get(Won ? "ui.runend.win_title" : "ui.runend.over");
        titleText.color = Color.white;
        if (titlePlate != null && failPlate != null && winPlate != null) titlePlate.sprite = Won ? winPlate : failPlate;

        // Tablo.
        scoreValue.text = HarvestQuota.Format(score != null ? score.TotalScore : 0);
        if (roundValue != null) roundValue.text = rounds != null ? $"{rounds.CurrentRound} / {rounds.MaxRounds}" : "-";
        BossRewardManager manager = BossRewardManager.Instance;
        if (rewardCountValue != null) rewardCountValue.text = (manager != null ? manager.Taken.Count : 0).ToString();
        RunClock clock = RunClock.Instance;
        if (timeValue != null) timeValue.text = clock != null ? RunClock.Format(clock.ActiveGameSeconds) : "-";

        // Neden: son kota ve (o round boss round'uysa) boss hasadı; hedef / değer + çubuk. Kayıpta "SENİ DURDURAN" + boss rozeti.
        if (causeHeader != null) causeHeader.text = GameText.Get(Won ? "ui.runend.last_quota_label" : "ui.runend.stopped_by");
        long quotaScore = rounds != null ? rounds.LastQuotaScore : 0, quotaTarget = rounds != null ? rounds.LastQuotaTarget : 0;
        // Hedefi olmayan kota satırı gösterilmez ("0 / 0" anlamsız).
        if (quotaValue != null) quotaValue.transform.parent.gameObject.SetActive(quotaTarget > 0);
        if (quotaTarget > 0) Bar(quotaValue, quotaFill, quotaFillImage, quotaScore, quotaTarget);
        bool bossCounted = rounds != null && rounds.LastBossTarget > 0 && (rounds.EndedByBoss || rounds.LastBossRound == rounds.LastQuotaRound);
        if (bossRow != null) bossRow.SetActive(bossCounted);
        if (bossCounted) Bar(bossValue, bossFill, bossFillImage, rounds.LastBossScore, rounds.LastBossTarget);
        SegmentEventDirector events = SegmentEventDirector.Instance;
        SegmentEventSO boss = events != null ? (events.Active ?? events.LastEnded)?.Data : null;
        bool showBoss = boss != null && bossCounted;
        if (bossBadge != null) bossBadge.SetActive(showBoss);
        if (allMetNote != null) allMetNote.SetActive(Won && quotaTarget <= 0 && !bossCounted);
        if (showBoss && bossName != null)
        {
            bossName.text = boss.DisplayName;
            bossName.color = BossTheme.Bright(boss);   // boss'un kimlik rengi (koyu plakada açık tonu)
        }
    }

    // "0 / 40" ve çubuk: dolu kısım değer / hedef; hedef tutmadıysa kırmızı, tuttuysa yeşil.
    private static void Bar(TMP_Text value, RectTransform fill, Image fillImage, long current, long target)
    {
        if (value != null) value.text = $"{HarvestQuota.Format(current)} / {HarvestQuota.Format(target)}";
        float ratio = target > 0 ? Mathf.Clamp01((float)((double)current / target)) : 1f;
        if (fill != null) fill.anchorMax = new Vector2(ratio, 1f);
        if (fillImage != null)
        {
            fillImage.enabled = ratio > .02f;
            fillImage.color = current >= target ? Met : Short;
        }
    }

    // Eski yol (kit kurulmamış sahne): tek yazı + RunResultLayout.
    private void Legacy()
    {
        bool fullGame = !DemoSceneSettings.IsDemo && gameObject.scene.name == "GameScene";
        if (fullGame && scoreText != null)
            (GetComponent<RunResultLayout>() ?? gameObject.AddComponent<RunResultLayout>()).Prepare(scoreText);
        // Run'ın nasıl bittiği skorun üstünde yazar: kota tutmadı (ulaşılan skor / gereken kota) ya da run kazanıldı.
        RoundManager rounds = RoundManager.Instance;
        string total = GameText.Format("ui.runend.score", HarvestQuota.Format(HarvestScoreManager.Instance.TotalScore));
        if (rounds == null) { scoreText.text = total; return; }

        string quota = $"{HarvestQuota.Format(rounds.LastQuotaScore)} / {HarvestQuota.Format(rounds.LastQuotaTarget)}";
        string spec = SpecializationManager.Instance != null ? SpecializationPanelUI.Describe(SpecializationManager.Instance.Chosen) : null;
        total = (spec != null ? $"<size=70%>{GameText.Format("ui.summary.specialization", spec)}</size>\n" : "") + total;
        // Başlangıç seçimi ve bu run'da kalıcı olarak açılan içerik (görev).
        StartLoadoutManager loadout = StartLoadoutManager.Instance;
        if (loadout != null && loadout.Farmer != null && loadout.Scythe != null)
            total = $"<size=70%>{GameText.Format("ui.runend.loadout", loadout.Farmer.DisplayName, loadout.Scythe.DisplayName)}</size>\n" + total;
        if (QuestTracker.Instance != null && QuestTracker.Instance.UnlockedThisRun.Count > 0)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (StartOptionSO option in QuestTracker.Instance.UnlockedThisRun) names.Add(option.DisplayName);
            total += $"\n<size=80%><color=#9BEA7C>{GameText.Format("ui.runend.unlocked", string.Join(", ", names))}</color></size>";
        }
        // Bu run'da alınan boss ödülleri.
        string rewards = BossRewardText.Summary(BossRewardManager.Instance);
        if (rewards != null) total += fullGame
            ? $"\n\n<b>{GameText.Get("ui.runend.boss_rewards")}</b>\n<size=85%>• {rewards.Replace(", ", "\n• ")}</size>"
            : $"\n<size=70%>{GameText.Format("ui.runend.boss_rewards_line", rewards)}</size>";
        // Bedelli ödüller (Bölüm 3.7.4): kazanç ve bedel ayrı yazılır; seçim hakkı değiştiyse run sonundaki değer de.
        string trades = BossRewardText.TradeSummary(BossRewardManager.Instance);
        if (trades != null) total += $"\n<size=70%>{trades}</size>";
        string choices = BossRewardText.LevelChoiceLine(rounds.ChoicesPerLevel, rounds.BaseChoicesPerLevel);
        if (choices != null) total += $"\n<size=70%>{choices}</size>";
        // Süreler (Bölüm 3.7.8): aktif oynanış yalnız round'ların kendisidir; seçim, mağaza, hazırlık ve duraklama ayrı sayılır.
        string times = TimeLine(RunClock.Instance);
        if (times != null) total += $"\n<size=60%>{times}</size>";
        if (rounds.EndedByBoss)
        {
            // Boss round'unda iki koşul aranır; hangisinin eksik kaldığı ayrı ayrı yazılır.
            string boss = $"{HarvestQuota.Format(rounds.LastBossScore)} / {HarvestQuota.Format(rounds.LastBossTarget)}";
            string title = GameText.Get(rounds.EndedByQuota ? "ui.runend.fail_both" : "ui.runend.fail_boss");
            string quotaLine = GameText.Format(rounds.EndedByQuota ? "ui.runend.segment_quota_short" : "ui.runend.segment_quota_met", quota);
            scoreText.text = $"<size=125%><color=#FF8A7A>{title}</color></size>\n" +
                             $"<size=75%>{GameText.Format("ui.runend.boss_line", rounds.LastBossRound, boss, quotaLine)}</size>\n{total}";
        }
        else if (rounds.EndedByQuota)
            scoreText.text = $"<size=125%><color=#FF8A7A>{GameText.Get("ui.runend.fail_quota")}</color></size>\n" +
                             $"<size=75%>{GameText.Format("ui.runend.quota_line", rounds.LastQuotaRound, quota)}" +
                             (rounds.LastBossRound == rounds.LastQuotaRound && rounds.LastBossTarget > 0
                                 ? " · " + GameText.Format("ui.runend.boss_met", HarvestQuota.Format(rounds.LastBossScore), HarvestQuota.Format(rounds.LastBossTarget)) : "") +
                             $"</size>\n{total}";
        else if (rounds.Outcome == RunOutcome.Victory)
        {
            string title = rounds.Profile != null && !string.IsNullOrEmpty(rounds.Profile.victoryTitle) ? rounds.Profile.VictoryTitle : GameText.Get("ui.runend.victory");
            string last = rounds.LastQuotaRound > 0 ? " · " + GameText.Format("ui.runend.last_quota", quota) : "";
            scoreText.text = $"<size=125%><color=#9BEA7C>{title}</color></size>\n<size=75%>{GameText.Format("ui.runend.rounds", rounds.MaxRounds)}{last}</size>\n{total}";
        }
        else scoreText.text = total;
    }

    // "Aktif oynanış 51:40 · seçimler 6:12 · mağaza ve hazırlık 9:30" (+ varsa level hesaplama bekleyişi ve duraklama).
    public static string TimeLine(RunClock clock)
    {
        if (clock == null || clock.RoundsTimed <= 0) return null;
        string line = GameText.Format("ui.runend.times", RunClock.Format(clock.ActiveGameSeconds), RunClock.Format(clock.ChoiceSeconds),
                                      RunClock.Format(clock.ShopSeconds + clock.PreparationSeconds));
        if (clock.LevelWorkSeconds >= 1d) line += " · " + GameText.Format("ui.runend.times.level_work", RunClock.Format(clock.LevelWorkSeconds));
        if (clock.PausedSeconds >= 1d) line += " · " + GameText.Format("ui.runend.times.paused", RunClock.Format(clock.PausedSeconds));
        return line;
    }

    public void OnRestartPressed()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenuPressed()
    {
        if (DemoSceneSettings.IsDemo)
        {
            DemoSceneSettings.Instance.ReturnToDemo();
            return;
        }
        HarvestScoreManager.Instance.ResetScore();
        RoundManager.Instance.ResetRounds();
        StatManager.Instance.ClearGlobalModifiers();

        GameManager.Instance.ReturnToMenu();
        // Ana menü ayrı sahnede; yalnız durum değiştirmek oyuncuyu oyun sahnesinde bırakıyordu.
        if (!string.IsNullOrEmpty(menuSceneName) && SceneManager.GetActiveScene().name != menuSceneName)
            SceneManager.LoadScene(menuSceneName);
    }
}
