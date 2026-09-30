using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss sonrası uzmanlaşma ekranı (RoundChoice durumu). Yalnız gösterir ve seçim isteğini iletir:
// seçilebilirlik, tek sefer uygulama ve etki SpecializationManager'da. UIManager kurar ve durumla açıp kapatır.
public sealed class SpecializationPanelUI : MonoBehaviour
{
    private const float CardWidth = 380f, CardHeight = 360f, Gap = 36f;
    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color Plus = new Color32(46, 125, 50, 255);
    private static readonly Color Minus = new Color32(180, 35, 24, 255);
    private static readonly Color Neutral = new Color32(110, 96, 110, 255);

    private sealed class Card
    {
        public RectTransform root;
        public TextMeshProUGUI title, plus, minus;
        public Button button;
        public SpecializationSO option;
    }

    private readonly List<Card> cards = new();
    private RectTransform row;
    private ComicUITheme theme;
    private TextMeshProUGUI subtitle;
    private bool requested;

    // Seçilen uzmanlaşmanın kısa özeti (HUD, round özeti, run sonu). Seçim yoksa null.
    public static string Describe(SpecializationSO chosen)
    {
        if (chosen == null) return null;
        if (chosen.ChangesNothing) return $"{chosen.displayName} · değişiklik yok";
        return $"{chosen.displayName} · doğrudan ×{Number(chosen.directDamageMultiplier)} · davranış ×{Number(chosen.behaviorDamageMultiplier)}";
    }

    private static string Number(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    public static SpecializationPanelUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<SpecializationPanelUI>(true);
        if (existing != null) return existing;
        var go = new GameObject("Specialization Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvasRoot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rect);
        var dim = go.GetComponent<Image>();
        dim.color = new Color(0.05f, 0.04f, 0.08f, 0.72f);
        dim.raycastTarget = true; // arkadaki panellere tıklanmasın
        var panel = go.AddComponent<SpecializationPanelUI>();
        go.SetActive(false);
        return panel;
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        var title = CreateText((RectTransform)transform, "Title", 46f, TextAlignmentOptions.Center, Color.white);
        title.text = "BOSS GEÇİLDİ · UZMANLAŞMA SEÇ";
        Place(title.rectTransform, new Vector2(0f, 300f), new Vector2(1400f, 70f));
        subtitle = CreateText((RectTransform)transform, "Subtitle", 26f, TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.78f));
        Place(subtitle.rectTransform, new Vector2(0f, 245f), new Vector2(1400f, 40f));
        row = new GameObject("Options", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform, false);
        Place(row, new Vector2(0f, -30f), new Vector2(3 * CardWidth + 2 * Gap, CardHeight));
    }

    private void OnEnable()
    {
        requested = false;
        int next = RoundManager.Instance != null ? RoundManager.Instance.CurrentRound + 1 : 0;
        subtitle.text = $"Yalnız biri alınır · run sonuna kadar geçerli · geri alınamaz · etkisi Round {next} ve sonrası";
        SpecializationManager manager = SpecializationManager.Instance;
        IReadOnlyList<SpecializationSO> options = manager != null ? manager.Options : null;
        int count = options != null ? options.Count : 0;
        float total = count * CardWidth + Mathf.Max(0, count - 1) * Gap;
        for (int i = 0; i < count; i++)
        {
            if (i == cards.Count) cards.Add(CreateCard(i));
            Card card = cards[i];
            card.option = options[i];
            card.root.gameObject.SetActive(true);
            card.root.anchoredPosition = new Vector2(-total * 0.5f + CardWidth * 0.5f + i * (CardWidth + Gap), 0f);
            card.title.text = options[i].displayName;
            bool neutral = options[i].ChangesNothing;
            card.plus.text = neutral ? "Bonus yok, ceza yok" : Lines(options[i], true);
            card.plus.color = neutral ? Neutral : Plus;
            card.minus.text = neutral ? "Hasar değerleri olduğu gibi kalır" : Lines(options[i], false);
            card.minus.color = neutral ? Neutral : Minus;
            card.button.interactable = manager != null && manager.IsPending;
        }
        for (int i = count; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
    }

    // Kazanç (katsayı > 1) ya da bedel (< 1) satırları doğrudan katsayılardan: ayar değişince yazı da değişir.
    private static string Lines(SpecializationSO option, bool gains)
    {
        string text = "";
        if (gains ? option.directDamageMultiplier > 1f : option.directDamageMultiplier < 1f)
            text += $"Doğrudan hasar ×{Number(option.directDamageMultiplier)}";
        if (gains ? option.behaviorDamageMultiplier > 1f : option.behaviorDamageMultiplier < 1f)
            text += (text.Length > 0 ? "\n" : "") + $"Davranış hasarı ×{Number(option.behaviorDamageMultiplier)}\n<size=72%>patlama · kasırga · bumerang · elektrik</size>";
        return text;
    }

    // Çift tıklamaya karşı arayüzde de tek istek; asıl koruma SpecializationManager.Choose'da.
    private void Request(Card card)
    {
        SpecializationManager manager = SpecializationManager.Instance;
        if (requested || manager == null || card.option == null) return;
        requested = true;
        foreach (Card c in cards) c.button.interactable = false;
        if (!manager.Choose(card.option)) requested = false;
    }

    private Card CreateCard(int index)
    {
        var root = new GameObject("Option " + index, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(row, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(CardWidth, CardHeight);
        Plate(root, "Ink shadow", new Color32(40, 29, 43, 220), new Vector2(7f, -8f), new Vector2(7f, -8f));
        Plate(root, "Ink outline", Ink, Vector2.zero, Vector2.zero);
        Plate(root, "Cream paper", new Color32(255, 242, 210, 255), new Vector2(4f, 4f), new Vector2(-4f, -4f));
        var header = Plate(root, "Amber header", new Color32(247, 192, 93, 255), new Vector2(7f, 0f), new Vector2(-7f, -7f));
        var headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.offsetMin = new Vector2(7f, -76f);

        var card = new Card { root = root };
        card.title = CreateText(root, "Name", 32f, TextAlignmentOptions.Center, Ink);
        Place(card.title.rectTransform, new Vector2(0f, CardHeight * 0.5f - 42f), new Vector2(CardWidth - 30f, 56f));
        card.plus = CreateText(root, "Plus", 26f, TextAlignmentOptions.Center, Plus);
        card.plus.textWrappingMode = TextWrappingModes.Normal;
        Place(card.plus.rectTransform, new Vector2(0f, 48f), new Vector2(CardWidth - 44f, 80f));
        card.minus = CreateText(root, "Minus", 26f, TextAlignmentOptions.Center, Minus);
        card.minus.textWrappingMode = TextWrappingModes.Normal;
        Place(card.minus.rectTransform, new Vector2(0f, -40f), new Vector2(CardWidth - 44f, 80f));
        if (theme != null)
        {
            card.button = theme.CreateButton(root, "Choose", "SEÇ", "green", new Vector2(220f, 76f));
            Place((RectTransform)card.button.transform, new Vector2(0f, -CardHeight * 0.5f + 58f), new Vector2(220f, 76f));
        }
        else
        {
            var go = new GameObject("Choose", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            card.button = go.GetComponent<Button>();
            Place((RectTransform)go.transform, new Vector2(0f, -CardHeight * 0.5f + 58f), new Vector2(220f, 76f));
        }
        card.button.onClick.AddListener(() => Request(card));
        return card;
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
