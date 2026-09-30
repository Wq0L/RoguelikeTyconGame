using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Round sonu özet kartı (RoundEnd panelinin sol tarafı). Round bittikten sonra ilk açılışta
// satırlar sırayla gelir ve sayılar tık sesleriyle sayılır; shop'tan dönüşte sonuç direkt görünür.
// Veriyi GameFeelDirector toplar. Unscaled time kullanır (RoundEnd'de timeScale 0).
public sealed class RoundSummaryUI : MonoBehaviour
{
    private const float HeaderHeight = 70f;
    private const float RowHeight = 48f;
    private const float Width = 360f;
    private const float FirstRowDelay = 0.3f;
    private const float RowInterval = 0.34f;
    private const float CountDuration = 0.28f;

    private static readonly Color Ink = new Color32(54, 39, 54, 255);

    private sealed class Row
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image icon;
        public TextMeshProUGUI label, value;
        public int target, shown;
        public bool plus;
        public readonly char[] buffer = new char[16];
        public float start;
        public bool landed;
    }

    private RectTransform card;
    private TextMeshProUGUI title;
    private TextMeshProUGUI warning, eventNotice, specNotice;
    private RoundSummaryData filled;
    private int shownEventVersion = -1;
    private readonly List<Row> rows = new();
    private int activeRows;
    private int shownVersion = -1;
    private float elapsed;
    private bool animating;
    private float nextTick;
    private ComicUITheme theme;

    public static RoundSummaryUI Attach(GameObject roundEndPanel)
    {
        if (roundEndPanel == null) return null;
        var existing = roundEndPanel.GetComponentInChildren<RoundSummaryUI>(true);
        if (existing != null) return existing;
        var container = new GameObject("Round Summary", typeof(RectTransform));
        var rect = (RectTransform)container.transform;
        rect.SetParent(roundEndPanel.transform, false);
        FeelOverlay.Stretch(rect);
        return container.AddComponent<RoundSummaryUI>();
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        BuildCard();
    }

    // Round verisi yoksa (ilk round'dan önceki hazırlık) kart yalnız kota ve yaklaşan olay bildirimleriyle açılır.
    private void OnEnable()
    {
        GameFeelDirector director = GameFeelDirector.Instance;
        RoundSummaryData data = director != null ? director.LastRound : null;
        animating = false;
        Fill(data);
        if (data == null) { ShowFinal(); return; }

        bool fresh = director.SummaryVersion != shownVersion;
        shownVersion = director.SummaryVersion;
        if (fresh) BeginAnimation();
        else ShowFinal();
    }

    private void Fill(RoundSummaryData data)
    {
        filled = data;
        activeRows = 0;
        if (data == null)
        {
            title.text = "HAZIRLIK";
            for (int i = 0; i < rows.Count; i++) rows[i].root.gameObject.SetActive(false);
            RefreshNotices();
            return;
        }
        title.text = $"ROUND {data.Round} ÖZETİ";
        SetRow("Hasat", data.Harvests, false, theme != null ? theme.sproutSprite : null, new Color32(70, 130, 60, 255), true);
        SetRow("Altın", data.Gold, true, theme != null ? theme.coinSprite : null, new Color32(196, 128, 20, 255), false);
        SetRow("Demir", data.Iron, true, theme != null ? theme.ironSprite : null, new Color32(80, 104, 140, 255), false);
        SetRow("Taş", data.Stone, true, theme != null ? theme.stoneSprite : null, new Color32(112, 96, 88, 255), false);
        // Round'da kazanılan XP: level eğrisini gerçek oyuna göre ayarlamak için de okunur.
        SetRow("XP", data.Xp, true, null, new Color32(36, 128, 150, 255), false);
        SetRow("Level", data.Levels, true, null, new Color32(214, 110, 40, 255), false);
        SetRow("Skor", data.Score, true, null, new Color32(128, 64, 170, 255), true);
        for (int i = activeRows; i < rows.Count; i++) rows[i].root.gameObject.SetActive(false);
        RefreshNotices();
    }

    // Kota (segmentin durumu, segment sonunda sonuç ve sıradaki kota) ve segment olayı (yaklaşan, aktif, bitti).
    // Olay bölgesi hazırlık sırasında seçildiği için olay sürümü değişince yeniden yazılır.
    private void RefreshNotices()
    {
        SegmentEventDirector events = SegmentEventDirector.Instance;
        SpecializationManager specialization = SpecializationManager.Instance;
        shownEventVersion = (events != null ? events.Version : 0) * 1000 + (specialization != null ? specialization.Version : 0);
        int finished = filled != null ? filled.Round : 0;
        string notice = QuotaNotice(RoundManager.Instance, finished, out QuotaTone tone);
        string eventText = SegmentEventText.Notice(events, finished);
        warning.gameObject.SetActive(notice != null);
        if (notice != null)
        {
            warning.text = notice;
            warning.color = ToneColor(tone);
        }
        // Seçilen uzmanlaşma en altta tek satır; sonradan da görülebilsin.
        string spec = SpecializationManager.Instance != null ? SpecializationPanelUI.Describe(SpecializationManager.Instance.Chosen) : null;
        float specSpace = spec != null ? 34f : 0f;
        specNotice.gameObject.SetActive(spec != null);
        if (spec != null) specNotice.text = "Uzmanlaşma: " + spec;
        eventNotice.gameObject.SetActive(eventText != null);
        if (eventText != null) eventNotice.text = eventText;
        eventNotice.rectTransform.offsetMin = new Vector2(16f, 12f + specSpace);
        eventNotice.rectTransform.offsetMax = new Vector2(-16f, 76f + specSpace);
        float eventSpace = eventText != null ? 70f : 0f;
        warning.rectTransform.offsetMin = new Vector2(16f, 12f + eventSpace + specSpace);
        warning.rectTransform.offsetMax = new Vector2(-16f, 78f + eventSpace + specSpace);
        card.gameObject.SetActive(filled != null || notice != null || eventText != null);
        card.sizeDelta = new Vector2(Width, HeaderHeight + 12f + activeRows * RowHeight + 16f + (notice != null ? 74f : 0f) + eventSpace + specSpace);
    }

    public enum QuotaTone { Info, Done, Danger }

    public static Color ToneColor(QuotaTone tone) => tone == QuotaTone.Done ? new Color32(46, 125, 50, 255)
        : tone == QuotaTone.Danger ? new Color32(180, 35, 24, 255) : new Color32(154, 91, 18, 255);

    // Sayılara ek getirilmez: "5.000'i / 12.000'i" gibi ekler sayıya göre değişir.
    // finishedRound 0: ilk round'dan önceki hazırlık. Olaylı (boss) segmentin kotası "boss kotası" diye yazılır.
    public static string QuotaNotice(RoundManager rounds, int finishedRound, out QuotaTone tone)
    {
        tone = QuotaTone.Info;
        if (rounds == null || !rounds.QuotaEnabled || rounds.EndedByQuota) return null;
        int segmentRounds = rounds.QuotaSegmentRounds;
        SegmentEventDirector events = SegmentEventDirector.Instance;
        string Name(int s, string plain, string boss) => SegmentEventText.IsEventSegment(events, s) ? boss : plain;
        if (finishedRound <= 0)
            return $"{Name(1, "İlk kota", "İlk boss kotası")}: {HarvestQuota.Format(rounds.QuotaTargetFor(1))} · {segmentRounds} round\n" +
                   $"<size=72%>Kota her {segmentRounds} round'da kontrol edilir; tutmazsa run biter</size>";
        if (rounds.LastQuotaRound == finishedRound)
        {
            tone = QuotaTone.Done;
            int closed = HarvestQuota.SegmentOf(finishedRound, segmentRounds), next = closed + 1;
            string passed = $"{Name(closed, "KOTA", "BOSS KOTASI")} TAMAM · {HarvestQuota.Format(rounds.LastQuotaScore)} / {HarvestQuota.Format(rounds.LastQuotaTarget)}";
            if (finishedRound >= rounds.MaxRounds) return passed;
            return passed + $"\n<size=72%>{Name(next, "Sıradaki kota", "Sıradaki boss kotası")}: {HarvestQuota.Format(rounds.QuotaTargetFor(next))} · {segmentRounds} round</size>";
        }
        int segment = HarvestQuota.SegmentOf(finishedRound, segmentRounds);
        int end = HarvestQuota.SegmentEnd(segment, segmentRounds), left = end - finishedRound;
        long target = rounds.QuotaTargetFor(segment), progress = rounds.QuotaProgress;
        string score = $"{HarvestQuota.Format(progress)} / {HarvestQuota.Format(target)}";
        string kota = Name(segment, "Kota", "Boss kotası");
        if (progress >= target)
        {
            tone = QuotaTone.Done;
            return $"{kota} tamam · {score}\n<size=72%>Segment {end}. round sonunda kapanır</size>";
        }
        if (left <= 1)
        {
            tone = QuotaTone.Danger;
            return $"SON ROUND · kotaya {HarvestQuota.Format(target - progress)} kaldı\n<size=72%>{kota} {score} · tutmazsa run biter</size>";
        }
        return $"{kota} {score} · {left} round kaldı\n<size=72%>Tutmazsa run {end}. round sonunda biter</size>";
    }

    private void SetRow(string label, int value, bool plus, Sprite icon, Color color, bool always)
    {
        if (!always && value <= 0) return;
        if (activeRows == rows.Count) rows.Add(CreateRow(rows.Count));
        Row row = rows[activeRows];
        row.root.gameObject.SetActive(true);
        row.root.anchoredPosition = new Vector2(20f, -(HeaderHeight + 12f + RowHeight * 0.5f) - activeRows * RowHeight);
        row.label.text = label;
        row.value.color = color;
        row.icon.sprite = icon;
        row.icon.enabled = icon != null;
        row.label.rectTransform.anchoredPosition = new Vector2(icon != null ? 46f : 4f, 0f);
        row.target = Mathf.Max(0, value);
        row.plus = plus;
        row.start = FirstRowDelay + activeRows * RowInterval;
        activeRows++;
    }

    private void BeginAnimation()
    {
        elapsed = 0f;
        nextTick = 0f;
        animating = true;
        card.localScale = Vector3.one * 0.85f;
        for (int i = 0; i < activeRows; i++)
        {
            Row row = rows[i];
            row.shown = 0;
            row.landed = false;
            row.group.alpha = 0f;
            row.root.localScale = Vector3.one * 0.6f;
            SetValue(row, 0);
        }
    }

    private void ShowFinal()
    {
        card.localScale = Vector3.one;
        for (int i = 0; i < activeRows; i++)
        {
            Row row = rows[i];
            row.group.alpha = 1f;
            row.root.localScale = Vector3.one;
            row.value.rectTransform.localScale = Vector3.one;
            SetValue(row, row.target);
        }
    }

    private void Update()
    {
        SegmentEventDirector events = SegmentEventDirector.Instance;
        SpecializationManager specialization = SpecializationManager.Instance;
        int version = (events != null ? events.Version : 0) * 1000 + (specialization != null ? specialization.Version : 0);
        if (!animating && version != shownEventVersion) RefreshNotices();
        if (!animating) return;
        elapsed += Time.unscaledDeltaTime;
        card.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, OutBack(Mathf.Clamp01(elapsed / 0.25f)));

        bool allDone = true;
        bool counting = false;
        for (int i = 0; i < activeRows; i++)
        {
            Row row = rows[i];
            float local = elapsed - row.start;
            if (local < 0f) { allDone = false; continue; }

            float appear = Mathf.Clamp01(local / 0.2f);
            row.group.alpha = appear;
            row.root.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, OutBack(appear));

            float count = Mathf.Clamp01((local - 0.08f) / CountDuration);
            int value = Mathf.RoundToInt(row.target * count * count * (3f - 2f * count));
            if (value != row.shown) { SetValue(row, value); counting = true; }

            if (!row.landed && count >= 1f)
            {
                row.landed = true;
                bool last = i == activeRows - 1;
                FeelAudio.Play(last ? FeelSound.Thump : FeelSound.Tick, last ? 0.55f : 0.5f, last ? 1f : 1.9f + i * 0.08f);
            }
            float punch = row.landed ? Mathf.Clamp01((local - 0.08f - CountDuration) / 0.2f) : 0f;
            row.value.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(punch * Mathf.PI) * (1f - punch));
            if (!row.landed || punch < 1f) allDone = false;
        }

        // Sayarken hızlı tıkırtı; her satırda perde biraz yükselir.
        if (counting && elapsed >= nextTick)
        {
            nextTick = elapsed + 0.05f;
            FeelAudio.Play(FeelSound.Tick, 0.22f, 1.2f + Mathf.Min(0.8f, elapsed * 0.3f));
        }

        if (allDone) animating = false;
    }

    // Sayarken her karede string oluşturulmaz: sayı satırın char dizisine yazılır (FloatingText gibi).
    private static void SetValue(Row row, int value)
    {
        row.shown = value;
        int start = FormatNumber(row.buffer, row.plus, value);
        row.value.SetCharArray(row.buffer, start, row.buffer.Length - start);
    }

    // "+12.345": dizinin sonundan geriye doğru yazar, başlangıç indeksini döner.
    // Kültür verisine bağlı değil; IL2CPP build'lerinde de aynı.
    private static int FormatNumber(char[] buffer, bool plus, int value)
    {
        value = Mathf.Max(0, value);
        int position = buffer.Length;
        int digits = 0;
        do
        {
            if (digits > 0 && digits % 3 == 0) buffer[--position] = '.';
            buffer[--position] = (char)('0' + value % 10);
            value /= 10;
            digits++;
        } while (value > 0);
        if (plus) buffer[--position] = '+';
        return position;
    }

    private static float OutBack(float k)
    {
        const float c1 = 1.7f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void BuildCard()
    {
        var cardObject = new GameObject("Card", typeof(RectTransform));
        card = (RectTransform)cardObject.transform;
        card.SetParent(transform, false);
        card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
        card.pivot = new Vector2(0f, 0.5f);
        card.anchoredPosition = new Vector2(48f, 40f);
        card.sizeDelta = new Vector2(Width, 300f);

        // Tooltip'lerle aynı çizgi roman kağıdı görünümü.
        Plate("Ink shadow", new Color32(40, 29, 43, 220), Vector2.zero, Vector2.one, new Vector2(6f, -7f), new Vector2(6f, -7f));
        Plate("Ink outline", Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Plate("Cream paper", new Color32(255, 242, 210, 255), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        Plate("Amber header", new Color32(247, 192, 93, 255), new Vector2(0f, 1f), Vector2.one, new Vector2(7f, -HeaderHeight), new Vector2(-7f, -7f));

        title = CreateText(card, "Title", 30f, TextAlignmentOptions.Center);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = Vector2.one;
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(12f, -HeaderHeight + 4f);
        titleRect.offsetMax = new Vector2(-12f, -8f);

        warning = CreateText(card, "Quota Notice", 22f, TextAlignmentOptions.Center);
        warning.color = new Color32(180, 35, 24, 255);
        warning.textWrappingMode = TextWrappingModes.Normal;
        warning.enableAutoSizing = true;
        warning.fontSizeMin = 14f;
        warning.fontSizeMax = 22f;
        var warningRect = warning.rectTransform;
        warningRect.anchorMin = Vector2.zero;
        warningRect.anchorMax = new Vector2(1f, 0f);
        warningRect.pivot = new Vector2(.5f, 0f);
        warningRect.offsetMin = new Vector2(16f, 12f);
        warningRect.offsetMax = new Vector2(-16f, 78f);
        warning.gameObject.SetActive(false);

        eventNotice = CreateText(card, "Event Notice", 22f, TextAlignmentOptions.Center);
        eventNotice.color = SegmentEventText.Ink;
        eventNotice.textWrappingMode = TextWrappingModes.Normal;
        eventNotice.enableAutoSizing = true;
        eventNotice.fontSizeMin = 14f;
        eventNotice.fontSizeMax = 22f;
        var eventRect = eventNotice.rectTransform;
        eventRect.anchorMin = Vector2.zero;
        eventRect.anchorMax = new Vector2(1f, 0f);
        eventRect.pivot = new Vector2(.5f, 0f);
        eventRect.offsetMin = new Vector2(16f, 12f);
        eventRect.offsetMax = new Vector2(-16f, 76f);
        eventNotice.gameObject.SetActive(false);

        specNotice = CreateText(card, "Specialization Notice", 18f, TextAlignmentOptions.Center);
        specNotice.color = new Color32(96, 60, 120, 255);
        specNotice.enableAutoSizing = true;
        specNotice.fontSizeMin = 12f;
        specNotice.fontSizeMax = 18f;
        var specRect = specNotice.rectTransform;
        specRect.anchorMin = Vector2.zero;
        specRect.anchorMax = new Vector2(1f, 0f);
        specRect.pivot = new Vector2(.5f, 0f);
        specRect.offsetMin = new Vector2(16f, 12f);
        specRect.offsetMax = new Vector2(-16f, 42f);
        specNotice.gameObject.SetActive(false);
    }

    private void Plate(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var plateObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rect = (RectTransform)plateObject.transform;
        rect.SetParent(card, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var plate = plateObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
    }

    private Row CreateRow(int index)
    {
        var rowObject = new GameObject("Row " + index, typeof(RectTransform), typeof(CanvasGroup));
        var root = (RectTransform)rowObject.transform;
        root.SetParent(card, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(Width - 40f, RowHeight - 6f);

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var iconRect = (RectTransform)iconObject.transform;
        iconRect.SetParent(root, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(36f, 36f);
        var icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI label = CreateText(root, "Label", 25f, TextAlignmentOptions.MidlineLeft);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(170f, RowHeight);

        TextMeshProUGUI value = CreateText(root, "Value", 29f, TextAlignmentOptions.MidlineRight);
        value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        value.rectTransform.pivot = new Vector2(1f, 0.5f);
        value.rectTransform.anchoredPosition = Vector2.zero;
        value.rectTransform.sizeDelta = new Vector2(150f, RowHeight);

        return new Row { root = root, group = rowObject.GetComponent<CanvasGroup>(), icon = icon, label = label, value = value };
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, float size, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(text);
        else text.color = Ink;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
