using DG.Tweening;
using TMPro;
using UnityEngine;

// Round HUD'unda Hasat Kotası: bu segmentte kazanılan skor / kota, ilerleme çubuğu ve kalan round.
// Segment olayı (boss) aktifken kartın kâğıdı o boss'un rengine boyanır (BossTheme: Don buz mavisi, Sert Kabuk amber, Sis gri-lila);
// alt satırdaki kural / yaklaşan boss yazısı da aynı kimlikten renk alır.
// Boss ritminde (Bölüm 3.4) iki koşul ayrı satırdadır: üstte "SEGMENT KOTASI", altında "BOSS HASADI" (boss round'unda sayaç,
// hazırlık round'larında hedef). Alınan boss ödülleri en altta kısa liste olarak durur.
// RoundUI kurar; sahneye kayıt gerekmez. Skoru saniyede 10 kez okur, değişmediyse yazıyı yeniden kurmaz.
public sealed class QuotaHUD : MonoBehaviour
{
    private const float Width = 440f;
    private const float Height = 76f;
    private const float EventLineHeight = 28f;
    private const float RewardLineHeight = 19f;
    private const float BarInset = 16f;

    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color Amber = new Color32(247, 192, 93, 255);
    private static readonly Color Green = new Color32(98, 176, 74, 255);
    private static readonly Color Hot = new Color32(232, 120, 60, 255);
    private static readonly Color Paper = new Color32(255, 242, 210, 255);

    private RectTransform card, fill;
    private CanvasGroup group;
    private ComicPopupPlate fillPlate, paper;
    private TextMeshProUGUI label, value, eventLine, specLine, bossLine, rewardList, rhythmLine;
    private string shownEvent, shownSpec, shownBossLine, shownRewards, shownRhythm;
    public string RhythmText => rhythmLine != null && rhythmLine.gameObject.activeSelf ? rhythmLine.text : null;
    private bool shownBoss;
    private SegmentEventSO shownBossIdentity;
    // Test ve doğrulama için: kartın şu anki kâğıt ve boss satırı rengi.
    public Color PaperColor => paper != null ? paper.color : Color.clear;
    public Color EventLineColor => eventLine != null ? eventLine.color : Color.clear;
    public string EventLineText => eventLine != null && eventLine.gameObject.activeSelf ? eventLine.text : null;
    private long shownProgress = -1, shownTarget = -1;
    private int shownLeft = -1, shownState = -1;
    private bool wasDone;
    private float nextRefresh;
    private BossRewardManager cachedRewardManager;
    private int cachedRewardVersion = -1, cachedRewardLines;
    private string cachedRewardText;

    // parent: RoundUI'ın 100×100'lük sol üst kutusu. Round yazısı kutudan taşarak ortalanır; kart "Round" ile "Time"
    // yazılarının kapladığı genişlikte, hemen altlarında durur.
    public static QuotaHUD Attach(RectTransform parent)
    {
        if (parent == null) return null;
        var existing = parent.GetComponentInChildren<QuotaHUD>(true);
        if (existing != null) return existing;
        var root = new GameObject("Quota HUD", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(-106f, -108f);
        rect.sizeDelta = new Vector2(Width, Height);
        return root.AddComponent<QuotaHUD>();
    }

    private void Awake()
    {
        card = (RectTransform)transform;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Plate(card, "Ink shadow", new Color32(40, 29, 43, 200), new Vector2(5f, -6f), new Vector2(5f, -6f));
        Plate(card, "Ink outline", Ink, Vector2.zero, Vector2.zero);
        paper = Plate(card, "Cream paper", Paper, new Vector2(4f, 4f), new Vector2(-4f, -4f));

        label = CreateText("Label", 23f, TextAlignmentOptions.MidlineLeft, new Vector2(16f, -8f), new Vector2(250f, 34f), new Vector2(0f, 1f));
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 23f;
        value = CreateText("Value", 26f, TextAlignmentOptions.MidlineRight, new Vector2(-16f, -8f), new Vector2(160f, 34f), new Vector2(1f, 1f));
        value.enableAutoSizing = true;
        value.fontSizeMin = 16f;
        value.fontSizeMax = 26f;
        eventLine = CreateText("Event", 19f, TextAlignmentOptions.MidlineLeft, new Vector2(16f, -40f), new Vector2(Width - 32f, 26f), new Vector2(0f, 1f));
        eventLine.color = BossTheme.Ink(null);
        eventLine.enableAutoSizing = true;
        eventLine.fontSizeMin = 13f;
        eventLine.fontSizeMax = 19f;
        eventLine.gameObject.SetActive(false);
        specLine = CreateText("Specialization", 17f, TextAlignmentOptions.MidlineLeft, new Vector2(16f, -40f), new Vector2(Width - 32f, 24f), new Vector2(0f, 1f));
        specLine.color = new Color32(96, 60, 120, 255);
        specLine.enableAutoSizing = true;
        specLine.fontSizeMin = 12f;
        specLine.fontSizeMax = 17f;
        specLine.gameObject.SetActive(false);
        bossLine = CreateText("Boss", 20f, TextAlignmentOptions.MidlineLeft, new Vector2(16f, -40f), new Vector2(Width - 32f, 26f), new Vector2(0f, 1f));
        bossLine.enableAutoSizing = true;
        bossLine.fontSizeMin = 14f;
        bossLine.fontSizeMax = 20f;
        bossLine.gameObject.SetActive(false);
        rhythmLine = CreateText("Rhythm", 17f, TextAlignmentOptions.MidlineLeft, new Vector2(16f, -40f), new Vector2(Width - 32f, 22f), new Vector2(0f, 1f));
        rhythmLine.enableAutoSizing = true;
        rhythmLine.fontSizeMin = 12f;
        rhythmLine.fontSizeMax = 17f;
        rhythmLine.gameObject.SetActive(false);
        rewardList = CreateText("Rewards", 15f, TextAlignmentOptions.TopLeft, new Vector2(16f, -40f), new Vector2(Width - 32f, RewardLineHeight), new Vector2(0f, 1f));
        rewardList.color = new Color32(70, 92, 40, 255);
        rewardList.enableAutoSizing = true;
        rewardList.fontSizeMin = 11f;
        rewardList.fontSizeMax = 15f;
        rewardList.gameObject.SetActive(false);

        var track = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        track.SetParent(card, false);
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.offsetMin = new Vector2(BarInset, 13f);
        track.offsetMax = new Vector2(-BarInset, 31f);
        Plate(track, "Bar outline", Ink, Vector2.zero, Vector2.zero);
        Plate(track, "Bar well", new Color32(222, 204, 170, 255), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        fill = new GameObject("Bar fill", typeof(RectTransform)).GetComponent<RectTransform>();
        fill.SetParent(track, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        fillPlate = fill.gameObject.AddComponent<ComicPopupPlate>();
        fillPlate.raycastTarget = false;
    }

    private void OnEnable()
    {
        nextRefresh = 0f;
        shownState = -1;
        Refresh();
    }

    private void OnDisable()
    {
        card.DOKill();
        card.localScale = Vector3.one;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        Refresh();
    }

    private void Refresh()
    {
        RoundManager rounds = RoundManager.Instance;
        if (rounds == null) return;
        group.alpha = rounds.QuotaEnabled ? 1f : 0f;
        if (!rounds.QuotaEnabled) return;

        // Segment az önce kapandıysa (round sonu, shop) sonucu gösterir; sıradaki round'da yeni segment başlar.
        bool closed = rounds.LastQuotaRound == rounds.CurrentRound && !rounds.IsRoundActive;
        long target = closed ? rounds.LastQuotaTarget : rounds.QuotaTarget;
        long progress = closed ? rounds.LastQuotaScore : rounds.QuotaProgress;
        bool before = rounds.IsRoundActive || rounds.IsPreparingFirstRound;
        int left = closed ? 0 : rounds.QuotaSegmentEnd - rounds.CurrentRound + (before ? 1 : 0);
        bool done = progress >= target;
        int state = done ? 2 : left <= 1 ? 1 : 0;
        SegmentEventDirector events = SegmentEventDirector.Instance;
        bool boss = events != null && events.Active != null;
        // Satırın anlattığı boss (aktif, yoksa yaklaşan): renkler onun kimliğinden. Boss bitince ya da yenisi seçilince değişir.
        SegmentEventSO identity = BossTheme.Current(events);
        string eventText = SegmentEventText.HudLine(events);
        string specText = SpecializationManager.Instance != null ? SpecializationPanelUI.Describe(SpecializationManager.Instance.Chosen) : null;
        bool bossMode = events != null && events.BossMode;
        string bossText = bossMode ? BossLine(rounds, closed) : null;
        string rewards = RewardLines(out int rewardLines);
        string rhythmText = RhythmLine();

        if (progress == shownProgress && target == shownTarget && left == shownLeft && state == shownState &&
            boss == shownBoss && identity == shownBossIdentity && eventText == shownEvent && specText == shownSpec && bossText == shownBossLine && rewards == shownRewards && rhythmText == shownRhythm) return;
        bool reached = done && !wasDone && shownState >= 0 && rounds.IsRoundActive;
        shownProgress = progress; shownTarget = target; shownLeft = left; shownState = state; wasDone = done;
        shownBoss = boss; shownBossIdentity = identity; shownEvent = eventText; shownSpec = specText; shownBossLine = bossText; shownRewards = rewards; shownRhythm = rhythmText;

        // Boss ritminde kota hep "segment kotası"dır; boss'un kendi hedefi alt satırda ayrı yazılır.
        string name = bossMode ? "SEGMENT KOTASI" : boss ? "BOSS KOTASI" : "KOTA";
        label.text = done ? $"{name} TAMAM" : left <= 1 ? $"{name} · SON ROUND" : $"{name} · {left} ROUND";
        paper.color = boss ? BossTheme.Paper(events.Active.Data) : Paper;
        eventLine.color = BossTheme.Ink(identity);
        float y = -40f;
        bossLine.gameObject.SetActive(bossText != null);
        if (bossText != null)
        {
            bossLine.text = bossText;
            bossLine.rectTransform.anchoredPosition = new Vector2(16f, y);
            y -= EventLineHeight;
        }
        eventLine.gameObject.SetActive(eventText != null);
        eventLine.text = eventText ?? "";
        eventLine.rectTransform.anchoredPosition = new Vector2(16f, y);
        if (eventText != null) y -= EventLineHeight;
        // Seçilen uzmanlaşma run boyunca görünür (olay satırının altında).
        specLine.gameObject.SetActive(specText != null);
        specLine.text = specText != null ? "Uzmanlaşma: " + specText : "";
        specLine.rectTransform.anchoredPosition = new Vector2(16f, y);
        if (specText != null) y -= 24f;
        // Hasat Ritmi (kırılma ödülü): sayaç ya da hazır hak.
        rhythmLine.gameObject.SetActive(rhythmText != null);
        if (rhythmText != null)
        {
            rhythmLine.text = rhythmText;
            rhythmLine.rectTransform.anchoredPosition = new Vector2(16f, y);
            y -= 22f;
        }
        // Alınan boss ödülleri: ad, adet ve toplam etki.
        rewardList.gameObject.SetActive(rewards != null);
        if (rewards != null)
        {
            rewardList.text = rewards;
            rewardList.rectTransform.anchoredPosition = new Vector2(16f, y);
            rewardList.rectTransform.sizeDelta = new Vector2(Width - 32f, rewardLines * RewardLineHeight);
            y -= rewardLines * RewardLineHeight + 4f;
        }
        card.sizeDelta = new Vector2(Width, Height + (-40f - y));
        value.text = $"{HarvestQuota.Format(progress)} / {HarvestQuota.Format(target)}";
        float ratio = target > 0 ? Mathf.Clamp01((float)((double)progress / target)) : 1f;
        fill.anchorMax = new Vector2(ratio, 1f);
        fillPlate.enabled = ratio > 0.001f;
        fillPlate.color = done ? Green : state == 1 ? Hot : Amber;

        if (reached)
        {
            card.DOKill();
            card.localScale = Vector3.one;
            card.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f).SetUpdate(true);
            FeelAudio.Play(FeelSound.Chime, 0.6f, 1.2f);
        }
    }

    // Boss round'unda o round'un skoru / hedef; hazırlık round'larında yaklaşan boss round'unun hedefi.
    private static string BossLine(RoundManager rounds, bool closed)
    {
        int segment = rounds.QuotaSegment;
        if (rounds.Profile == null || !rounds.Profile.HasBoss(segment)) return null;
        long target = rounds.BossTargetFor(segment);
        bool bossRound = rounds.IsBossRound(rounds.CurrentRound) && (rounds.IsRoundActive || closed);
        if (!bossRound)
            return target > 0 ? $"Boss hasadı hedefi (Round {rounds.QuotaSegmentEnd}): {HarvestQuota.Format(target)}" : $"Boss round'u: Round {rounds.QuotaSegmentEnd}";
        if (target <= 0) return "BOSS HASADI: hedef yok";
        long progress = closed ? rounds.LastBossScore : rounds.BossProgress;
        bool done = progress >= target;
        return $"<color={(done ? "#2E7D32" : "#B4530A")}>BOSS HASADI: {HarvestQuota.Format(progress)} / {HarvestQuota.Format(target)}{(done ? " · TAMAM" : "")}</color>";
    }

    // "Hasat Ritmi 4 / 6" ya da hak hazırken "HASAT RİTMİ HAZIR · sıradaki vuruş güçlü". Ödül alınmadıysa satır yok.
    // Sayaç değişmediyse yazı yeniden kurulmaz (HUD saniyede 10 kez sorar).
    private int rhythmVersion = -1, rhythmThreshold = -1;
    private string rhythmCached;

    private string RhythmLine()
    {
        HarvestRhythm rhythm = RunPower.Rhythm;
        if (!rhythm.Enabled) { rhythmCached = null; return null; }
        int threshold = rhythm.Threshold;
        if (rhythmCached != null && rhythm.Version == rhythmVersion && threshold == rhythmThreshold) return rhythmCached;
        rhythmVersion = rhythm.Version;
        rhythmThreshold = threshold;
        return rhythmCached = rhythm.Ready
            ? "<color=#B26A00>HASAT RİTMİ HAZIR · sıradaki vuruş güçlü</color>"
            : $"<color=#5B4A2E>Hasat Ritmi {rhythm.Count} / {threshold}</color>";
    }

    private string RewardLines(out int lines)
    {
        BossRewardManager manager = BossRewardManager.Instance;
        int version = manager != null ? manager.Version : -1;
        if (manager == cachedRewardManager && version == cachedRewardVersion)
        { lines = cachedRewardLines; return cachedRewardText; }
        cachedRewardManager = manager;
        cachedRewardVersion = version;
        lines = cachedRewardLines = 0;
        cachedRewardText = null;
        if (manager == null || manager.Taken.Count == 0) return null;
        var text = new System.Text.StringBuilder("Boss ödülleri");
        lines = 1;
        foreach (BossRewardSO reward in manager.Taken)
        {
            text.Append("\n· ").Append(BossRewardText.ListLine(reward, manager.Stacks(reward)));
            lines++;
        }
        cachedRewardLines = lines;
        return cachedRewardText = text.ToString();
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var plateObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rect = (RectTransform)plateObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var plate = plateObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    private TextMeshProUGUI CreateText(string name, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 box, Vector2 anchor)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(card, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        ComicUITheme theme = FeelOverlay.Theme;
        if (theme != null) theme.StylePopupText(text);
        else text.color = Ink;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
