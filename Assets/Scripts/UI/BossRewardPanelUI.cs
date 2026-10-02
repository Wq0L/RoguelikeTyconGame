using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss ödülü ekranı (RoundChoice durumu): geçilen boss'tan sonra en çok üç farklı ödül gösterir, biri alınır.
// Yalnız gösterir ve isteği iletir: teklif, tek sefer uygulama ve birikme BossRewardManager'da. UIManager kurar ve açıp kapatır.
// Kart: etki (tek alış), şu anki birikim ve seçince oluşacak toplam. Uygun ödül kalmadıysa "DEVAM" ile geçilir.
public sealed class BossRewardPanelUI : MonoBehaviour
{
    private const float CardWidth = 400f, CardHeight = 430f, Gap = 36f;
    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color Plus = new Color32(46, 125, 50, 255);
    private static readonly Color Muted = new Color32(110, 96, 110, 255);

    private sealed class Card
    {
        public RectTransform root;
        public TextMeshProUGUI title, effect, note, stack;
        public Button button;
        public BossRewardSO reward;
    }

    private readonly List<Card> cards = new();
    private RectTransform row;
    private ComicUITheme theme;
    private TextMeshProUGUI title, subtitle, empty, takenList;
    private Button continueButton;
    private bool requested;
    private int shownVersion = -1;

    public static BossRewardPanelUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<BossRewardPanelUI>(true);
        if (existing != null) return existing;
        var go = new GameObject("Boss Reward Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvasRoot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rect);
        var dim = go.GetComponent<Image>();
        dim.color = new Color(0.05f, 0.04f, 0.08f, 0.72f);
        dim.raycastTarget = true; // arkadaki panellere tıklanmasın
        var panel = go.AddComponent<BossRewardPanelUI>();
        go.SetActive(false);
        return panel;
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        var root = (RectTransform)transform;
        CardSelectionVignette.Attach(root, CardSelectionVignette.Palette.Boss);
        title = CreateText(root, "Title", 46f, TextAlignmentOptions.Center, Color.white);
        Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1500f, 70f));
        subtitle = CreateText(root, "Subtitle", 26f, TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.78f));
        Place(subtitle.rectTransform, new Vector2(0f, 275f), new Vector2(1500f, 40f));
        row = new GameObject("Options", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform, false);
        Place(row, new Vector2(0f, -20f), new Vector2(3 * CardWidth + 2 * Gap, CardHeight));
        empty = CreateText(root, "Empty", 32f, TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.78f));
        empty.text = "Uygun ödül kalmadı\n<size=70%>Bütün ödüller sınırında ya da şu an bir şey değiştirmiyor</size>";
        Place(empty.rectTransform, new Vector2(0f, 30f), new Vector2(1200f, 120f));
        continueButton = CreateButton(root, "Continue", "DEVAM", "green", new Vector2(260f, 80f));
        Place((RectTransform)continueButton.transform, new Vector2(0f, -90f), new Vector2(260f, 80f));
        continueButton.onClick.AddListener(RequestContinue);
        takenList = CreateText(root, "Taken", 22f, TextAlignmentOptions.Top, new Color(0.82f, 0.95f, 0.74f));
        takenList.textWrappingMode = TextWrappingModes.Normal;
        Place(takenList.rectTransform, new Vector2(0f, -350f), new Vector2(1500f, 150f));
    }

    private void OnEnable()
    {
        requested = false;
        Fill();
    }

    private void Update()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (manager != null && manager.Version != shownVersion) Fill();
    }

    private void Fill()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        RoundManager rounds = RoundManager.Instance;
        shownVersion = manager != null ? manager.Version : -1;
        IReadOnlyList<BossRewardSO> offer = manager != null ? manager.Offer : null;
        int count = offer != null ? offer.Count : 0;
        int next = rounds != null ? rounds.CurrentRound + 1 : 0;
        title.text = "BOSS GEÇİLDİ · ÖDÜL SEÇ";
        subtitle.text = count > 0
            ? $"Yalnız biri alınır · run sonuna kadar geçerli · etkisi Round {next} ve sonrası"
            : "Bu boss için sunulabilecek ödül yok";
        float total = count * CardWidth + Mathf.Max(0, count - 1) * Gap;
        for (int i = 0; i < count; i++)
        {
            if (i == cards.Count) cards.Add(CreateCard(i));
            Card card = cards[i];
            BossRewardSO reward = offer[i];
            card.reward = reward;
            card.root.gameObject.SetActive(true);
            card.root.anchoredPosition = new Vector2(-total * 0.5f + CardWidth * 0.5f + i * (CardWidth + Gap), 0f);
            int have = manager.Stacks(reward);
            card.title.text = reward.displayName;
            card.effect.text = BossRewardText.Effect(reward);
            card.note.text = reward.note;
            card.stack.text = StackText(reward, have);
            card.button.interactable = manager.IsPending && !requested;
        }
        for (int i = count; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
        bool none = manager != null && manager.IsPending && count == 0;
        empty.gameObject.SetActive(none);
        continueButton.gameObject.SetActive(none);
        takenList.text = TakenText(manager);
    }

    // "Şu an 1/3: ×1,15" ve "Seçince 2/3: ×1,32" — art arda alışların nasıl birleştiği kartta görünür.
    public static string StackText(BossRewardSO reward, int have)
    {
        string now = have > 0 ? $"Şu an {have}/{reward.maxStacks}: toplam {BossRewardText.Value(reward, have)}" : $"Şu an 0/{reward.maxStacks}: yok";
        return $"<color=#6E606E>{now}</color>\n<color=#2E7D32>Seçince {have + 1}/{reward.maxStacks}: toplam {BossRewardText.Value(reward, have + 1)}</color>";
    }

    private static string TakenText(BossRewardManager manager)
    {
        if (manager == null || manager.Taken.Count == 0) return "";
        var lines = new List<string>();
        foreach (BossRewardSO reward in manager.Taken) lines.Add(BossRewardText.ListLine(reward, manager.Stacks(reward)));
        return "Alınan boss ödülleri\n<size=82%>" + string.Join("   ·   ", lines) + "</size>";
    }

    // Çift tıklamaya karşı arayüzde de tek istek; asıl koruma BossRewardManager.Choose'da.
    private void Request(Card card)
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (requested || manager == null || card.reward == null) return;
        requested = true;
        foreach (Card c in cards) c.button.interactable = false;
        if (!manager.Choose(card.reward)) requested = false;
    }

    private void RequestContinue()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (requested || manager == null) return;
        requested = true;
        if (!manager.ContinueWithoutReward()) requested = false;
    }

    private Card CreateCard(int index)
    {
        var root = new GameObject("Reward " + index, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(row, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(CardWidth, CardHeight);
        Plate(root, "Ink shadow", new Color32(40, 29, 43, 220), new Vector2(7f, -8f), new Vector2(7f, -8f));
        Plate(root, "Ink outline", Ink, Vector2.zero, Vector2.zero);
        Plate(root, "Cream paper", new Color32(255, 242, 210, 255), new Vector2(4f, 4f), new Vector2(-4f, -4f));
        var header = Plate(root, "Green header", new Color32(150, 214, 120, 255), new Vector2(7f, 0f), new Vector2(-7f, -7f));
        var headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.offsetMin = new Vector2(7f, -76f);

        var card = new Card { root = root };
        float top = CardHeight * 0.5f;
        card.title = CreateText(root, "Name", 34f, TextAlignmentOptions.Center, Ink);
        Place(card.title.rectTransform, new Vector2(0f, top - 42f), new Vector2(CardWidth - 30f, 56f));
        card.effect = CreateText(root, "Effect", 27f, TextAlignmentOptions.Center, Plus);
        card.effect.textWrappingMode = TextWrappingModes.Normal;
        Place(card.effect.rectTransform, new Vector2(0f, top - 128f), new Vector2(CardWidth - 40f, 76f));
        card.note = CreateText(root, "Note", 19f, TextAlignmentOptions.Center, Muted);
        card.note.textWrappingMode = TextWrappingModes.Normal;
        Place(card.note.rectTransform, new Vector2(0f, top - 204f), new Vector2(CardWidth - 44f, 64f));
        // İki satır: şu anki birikim ve seçince oluşacak toplam (satır taşmasın diye otomatik küçülür).
        card.stack = CreateText(root, "Stack", 21f, TextAlignmentOptions.Center, Ink);
        card.stack.enableAutoSizing = true;
        card.stack.fontSizeMin = 14f;
        card.stack.fontSizeMax = 21f;
        Place(card.stack.rectTransform, new Vector2(0f, top - 278f), new Vector2(CardWidth - 30f, 60f));
        card.button = CreateButton(root, "Choose", "AL", "green", new Vector2(220f, 76f));
        Place((RectTransform)card.button.transform, new Vector2(0f, -top + 54f), new Vector2(220f, 76f));
        card.button.onClick.AddListener(() => Request(card));
        return card;
    }

    private Button CreateButton(RectTransform parent, string name, string caption, string palette, Vector2 size)
    {
        if (theme != null) return theme.CreateButton(parent, name, caption, palette, size);
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var plate = go.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, float size, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(text);
        text.color = color;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
