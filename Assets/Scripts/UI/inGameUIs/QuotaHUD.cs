using DG.Tweening;
using TMPro;
using UnityEngine;

// Round HUD'unda Hasat Kotası: bu segmentte kazanılan skor / kota, ilerleme çubuğu ve kalan round.
// Segment olayı (boss) varsa kart buz rengine döner ve alt satırda kural ya da yaklaşan olay yazar.
// RoundUI kurar; sahneye kayıt gerekmez. Skoru saniyede 10 kez okur, değişmediyse yazıyı yeniden kurmaz.
public sealed class QuotaHUD : MonoBehaviour
{
    private const float Width = 440f;
    private const float Height = 76f;
    private const float EventLineHeight = 28f;
    private const float BarInset = 16f;

    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color Amber = new Color32(247, 192, 93, 255);
    private static readonly Color Green = new Color32(98, 176, 74, 255);
    private static readonly Color Hot = new Color32(232, 120, 60, 255);
    private static readonly Color Paper = new Color32(255, 242, 210, 255);
    private static readonly Color FrostPaper = new Color32(224, 240, 252, 255);

    private RectTransform card, fill;
    private CanvasGroup group;
    private ComicPopupPlate fillPlate, paper;
    private TextMeshProUGUI label, value, eventLine, specLine;
    private string shownEvent, shownSpec;
    private bool shownBoss;
    private long shownProgress = -1, shownTarget = -1;
    private int shownLeft = -1, shownState = -1;
    private bool wasDone;
    private float nextRefresh;

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
        eventLine.color = SegmentEventText.Ink;
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
        string eventText = SegmentEventText.HudLine(events);
        string specText = SpecializationManager.Instance != null ? SpecializationPanelUI.Describe(SpecializationManager.Instance.Chosen) : null;

        if (progress == shownProgress && target == shownTarget && left == shownLeft && state == shownState &&
            boss == shownBoss && eventText == shownEvent && specText == shownSpec) return;
        bool reached = done && !wasDone && shownState >= 0 && rounds.IsRoundActive;
        shownProgress = progress; shownTarget = target; shownLeft = left; shownState = state; wasDone = done;
        shownBoss = boss; shownEvent = eventText; shownSpec = specText;

        string name = boss ? "BOSS KOTASI" : "KOTA";
        label.text = done ? $"{name} TAMAM" : left <= 1 ? $"{name} · SON ROUND" : $"{name} · {left} ROUND";
        paper.color = boss ? FrostPaper : Paper;
        eventLine.gameObject.SetActive(eventText != null);
        eventLine.text = eventText ?? "";
        // Seçilen uzmanlaşma run boyunca görünür (olay satırının altında).
        specLine.gameObject.SetActive(specText != null);
        specLine.text = specText != null ? "Uzmanlaşma: " + specText : "";
        specLine.rectTransform.anchoredPosition = new Vector2(16f, eventText != null ? -40f - EventLineHeight : -40f);
        float extra = (eventText != null ? EventLineHeight : 0f) + (specText != null ? 24f : 0f);
        card.sizeDelta = new Vector2(Width, Height + extra);
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
