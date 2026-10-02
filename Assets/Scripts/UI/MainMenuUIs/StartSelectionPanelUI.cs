using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Yeni run başlangıç ekranı (ana menü → Oyna): çiftçi ve tırpan aynı ekranda seçilir, avantaj ve bedeller kartta görünür, run başlatılır.
// Yalnız gösterir ve seçimi kaydeder: kilit kuralı MetaSave'de, etkiler StartLoadoutManager'da (yalnız yeni run başında uygulanır).
// Kilitli kart seçilemez; görevi ve en iyi run ilerlemesini gösterir. Koddan kurulur, MenuScene dosyasına dokunmaz.
// Kart metni asset değerlerinden üretilir (StartOptionText): değer değişince yazı da değişir, vaat edilmeyen etki yazılmaz.
public sealed class StartSelectionPanelUI : MonoBehaviour
{
    private const float MaxCardWidth = 380f, Gap = 28f, RowWidth = 1760f, FarmerHeight = 360f, ScytheHeight = 300f, LockBanner = 122f;
    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color Paper = new Color32(255, 242, 210, 255);
    private static readonly Color Amber = new Color32(247, 192, 93, 255);
    private static readonly Color Chosen = new Color32(46, 125, 50, 255);
    private static readonly Color LockedPaper = new Color32(206, 196, 178, 255);
    private static readonly Color Muted = new Color32(110, 96, 110, 255);

    private sealed class Card
    {
        public StartOptionSO option;
        public RectTransform root;
        public ComicPopupPlate outline, paper, header;
        public TextMeshProUGUI title, style, effects, footer;
        public GameObject lockCover;
        public TextMeshProUGUI lockText;
        public Button button;
    }

    public event Action StartRequested;
    public event Action BackRequested;

    public FarmerSO SelectedFarmer { get; private set; }
    public ScytheSO SelectedScythe { get; private set; }
    public bool HasCatalog => StartCatalogSO.Active != null && StartCatalogSO.Active.defaultFarmer != null && StartCatalogSO.Active.defaultScythe != null;

    private readonly List<Card> farmerCards = new(), scytheCards = new();
    private RectTransform farmerRow, scytheRow;
    private TextMeshProUGUI summary;
    private ComicUITheme theme;
    private bool starting;

    public static StartSelectionPanelUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<StartSelectionPanelUI>(true);
        if (existing != null) return existing;
        var go = new GameObject("Start Selection Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvasRoot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rect);
        var dim = go.GetComponent<Image>();
        dim.color = new Color(0.05f, 0.04f, 0.08f, 0.82f);
        dim.raycastTarget = true;
        go.SetActive(false);
        return go.AddComponent<StartSelectionPanelUI>();
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        var root = (RectTransform)transform;
        Place(Text(root, "Title", 50f, Color.white, "YENİ RUN · BAŞLANGIÇ"), new Vector2(0f, 492f), new Vector2(1600f, 70f));
        Place(Text(root, "Subtitle", 24f, new Color(1f, 0.93f, 0.78f),
            "Bir çiftçi ve bir tırpan seç · etkiler bu run boyunca geçerli · yeni run'da yeniden uygulanır"), new Vector2(0f, 448f), new Vector2(1600f, 36f));
        Place(Text(root, "Farmer Label", 30f, Amber, "ÇİFTÇİ · genel yaklaşım"), new Vector2(0f, 406f), new Vector2(1200f, 44f));
        farmerRow = Row(root, "Farmers", new Vector2(0f, 200f), FarmerHeight);
        Place(Text(root, "Scythe Label", 30f, Amber, "TIRPAN · hasat biçimi"), new Vector2(0f, -4f), new Vector2(1200f, 44f));
        scytheRow = Row(root, "Scythes", new Vector2(0f, -180f), ScytheHeight);
        summary = Text(root, "Summary", 30f, Color.white, "");
        Place(summary, new Vector2(0f, -368f), new Vector2(1600f, 44f));
        Button back = CreateButton(root, "Back", "GERİ", "blue", new Vector2(240f, 80f));
        Place((RectTransform)back.transform, new Vector2(-260f, -458f), new Vector2(240f, 80f));
        back.onClick.AddListener(() => BackRequested?.Invoke());
        Button start = CreateButton(root, "Start", "RUN'I BAŞLAT", "green", new Vector2(340f, 86f));
        Place((RectTransform)start.transform, new Vector2(220f, -458f), new Vector2(340f, 86f));
        start.onClick.AddListener(RequestStart);
    }

    // Açılışta kayıttaki seçim (geçersizse varsayılan) işaretlenir.
    public void Open()
    {
        StartCatalogSO catalog = StartCatalogSO.Active;
        if (catalog == null) return;
        (FarmerSO farmer, ScytheSO scythe, bool _) = StartLoadoutManager.Resolve(catalog);
        SelectedFarmer = farmer;
        SelectedScythe = scythe;
        starting = false;
        gameObject.SetActive(true);
        Build(farmerRow, farmerCards, Sorted(catalog.farmers), FarmerHeight);
        Build(scytheRow, scytheCards, Sorted(catalog.scythes), ScytheHeight);
        Refresh();
    }

    public void Close() => gameObject.SetActive(false);

    // Kilitli ya da katalogda olmayan içerik seçilemez.
    public bool Select(StartOptionSO option)
    {
        if (option == null || !MetaSave.IsUnlocked(option)) return false;
        if (option is FarmerSO farmer) SelectedFarmer = farmer;
        else if (option is ScytheSO scythe) SelectedScythe = scythe;
        else return false;
        Refresh();
        return true;
    }

    public void RequestStart()
    {
        if (starting || SelectedFarmer == null || SelectedScythe == null) return;
        starting = true;
        MetaSave.SetSelection(SelectedFarmer, SelectedScythe);
        StartRequested?.Invoke();
    }

    private static List<T> Sorted<T>(List<T> list) where T : StartOptionSO
    {
        var result = new List<T>();
        foreach (T item in list) if (item != null) result.Add(item);
        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    private void Build<T>(RectTransform row, List<Card> cards, List<T> options, float height) where T : StartOptionSO
    {
        int count = options.Count;
        float width = Mathf.Min(MaxCardWidth, (RowWidth - Mathf.Max(0, count - 1) * Gap) / Mathf.Max(1, count));
        float total = count * width + Mathf.Max(0, count - 1) * Gap;
        for (int i = 0; i < count; i++)
        {
            if (i == cards.Count) cards.Add(CreateCard(row, i));
            Card card = cards[i];
            card.option = options[i];
            card.root.gameObject.SetActive(true);
            card.root.sizeDelta = new Vector2(width, height);
            card.root.anchoredPosition = new Vector2(-total * 0.5f + width * 0.5f + i * (width + Gap), 0f);
            Layout(card, width, height);
        }
        for (int i = count; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        foreach (Card card in farmerCards) Fill(card, card.option == SelectedFarmer);
        foreach (Card card in scytheCards) Fill(card, card.option == SelectedScythe);
        summary.text = SelectedFarmer != null && SelectedScythe != null
            ? $"Seçim: <color=#F7C05D>{SelectedFarmer.displayName}</color> + <color=#F7C05D>{SelectedScythe.displayName}</color>"
            : "";
    }

    private void Fill(Card card, bool selected)
    {
        if (!card.root.gameObject.activeSelf || card.option == null) return;
        StartOptionSO option = card.option;
        bool open = MetaSave.IsUnlocked(option);
        card.title.text = option.displayName;
        card.style.text = option.playstyle;
        card.effects.text = StartOptionText.Effects(option);
        card.button.interactable = open;
        card.lockCover.SetActive(!open);
        card.paper.color = open ? Paper : LockedPaper;
        card.outline.color = selected ? Chosen : Ink;
        card.header.color = selected ? new Color32(150, 214, 120, 255) : Amber;
        card.footer.text = !open ? "" : selected ? "SEÇİLİ" : "Seçmek için tıkla";
        card.footer.color = selected ? Chosen : Muted;
        if (!open)
        {
            QuestSO quest = option.unlockQuest;
            int best = MetaSave.BestProgress(quest);
            card.lockText.text = quest != null
                ? $"KİLİTLİ · GÖREV\n<size=72%>{quest.description}\n<color=#F7C05D>En iyi run: {Mathf.Min(best, quest.target)} / {quest.target}</color></size>"
                : "KİLİTLİ";
        }
    }

    private Card CreateCard(RectTransform row, int index)
    {
        var root = new GameObject("Card " + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        root.gameObject.layer = row.gameObject.layer;
        root.SetParent(row, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        var hit = root.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f); // tıklama alanı
        var card = new Card { root = root, button = root.GetComponent<Button>() };
        card.button.transition = Selectable.Transition.None;
        card.button.navigation = new Navigation { mode = Navigation.Mode.None };
        card.button.onClick.AddListener(() => Select(card.option));
        root.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.03f;
        Plate(root, "Ink shadow", new Color32(40, 29, 43, 220), new Vector2(7f, -8f), new Vector2(7f, -8f));
        card.outline = Plate(root, "Outline", Ink, Vector2.zero, Vector2.zero);
        card.paper = Plate(root, "Paper", Paper, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        card.header = Plate(root, "Header", Amber, new Vector2(8f, 0f), new Vector2(-8f, -8f));
        card.title = Text(root, "Name", 32f, Ink, "");
        card.style = Text(root, "Playstyle", 21f, Muted, "");
        card.style.textWrappingMode = TextWrappingModes.Normal;
        card.style.fontStyle = FontStyles.Italic;
        card.effects = Text(root, "Effects", 22f, Ink, "");
        card.effects.textWrappingMode = TextWrappingModes.Normal;
        card.effects.alignment = TextAlignmentOptions.Top;
        card.footer = Text(root, "Footer", 22f, Muted, "");
        // Kilitli kart: etkiler okunur kalır (hafif örtü); görev ve ilerleme alttaki koyu şeritte.
        var cover = Plate(root, "Lock cover", new Color32(40, 29, 43, 90), new Vector2(5f, 5f), new Vector2(-5f, -5f));
        card.lockCover = cover.gameObject;
        var banner = Plate(cover.rectTransform, "Lock banner", new Color32(40, 29, 43, 240), Vector2.zero, Vector2.zero);
        banner.rectTransform.anchorMax = new Vector2(1f, 0f);
        banner.rectTransform.offsetMax = new Vector2(0f, LockBanner);
        card.lockText = Text(banner.rectTransform, "Lock", 27f, new Color32(255, 236, 196, 255), "");
        card.lockText.textWrappingMode = TextWrappingModes.Normal;
        FeelOverlay.Stretch(card.lockText.rectTransform).offsetMin = new Vector2(12f, 6f);
        card.lockText.rectTransform.offsetMax = new Vector2(-12f, -6f);
        return card;
    }

    private static void Layout(Card card, float width, float height)
    {
        var header = card.header.rectTransform;
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(8f, -68f);
        header.offsetMax = new Vector2(-8f, -8f);
        float top = height * 0.5f;
        Place(card.title, new Vector2(0f, top - 38f), new Vector2(width - 24f, 50f));
        Place(card.style, new Vector2(0f, top - 100f), new Vector2(width - 36f, 56f));
        Place(card.effects, new Vector2(0f, (top - 134f + (-top + 48f)) * 0.5f), new Vector2(width - 36f, height - 182f));
        Place(card.footer, new Vector2(0f, -top + 28f), new Vector2(width - 30f, 34f));
    }

    private RectTransform Row(RectTransform parent, string name, Vector2 position, float height)
    {
        var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        row.gameObject.layer = parent.gameObject.layer;
        row.SetParent(parent, false);
        Place(row, position, new Vector2(RowWidth, height));
        return row;
    }

    private Button CreateButton(RectTransform parent, string name, string caption, string palette, Vector2 size)
    {
        if (theme != null) return theme.CreateButton(parent, name, caption, palette, size);
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var label = Text((RectTransform)go.transform, "Label", 30f, Ink, caption);
        FeelOverlay.Stretch(label.rectTransform);
        return go.GetComponent<Button>();
    }

    private static void Place(Component component, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)component.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
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

    private TextMeshProUGUI Text(RectTransform parent, string name, float size, Color color, string content)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(text);
        text.color = color;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }
}

// Başlangıç içeriği kart metni: yalnız asset'teki etkiler, kazanç (yeşil) ve bedel (kırmızı) ayrı.
// Yüzde bonusu "×", yüzde puanı "puan" diye yazılır.
public static class StartOptionText
{
    private const string Plus = "#2E7D32", Minus = "#B42318";

    public static string Effects(StartOptionSO option)
    {
        if (option == null) return "";
        if (option.IsNeutral) return "<color=#6E606E>Nötr · avantaj ve bedel yok\n<size=80%>oyunun kendi dengesi</size></color>";
        var gains = new List<string>();
        var costs = new List<string>();
        Add(option.directDamageMultiplier, true, "Doğrudan vuruş hasarı", "davranış hasarı değişmez", gains, costs);
        Add(option.harvestResourceMultiplier, true, "Hasat kaynağı", "yalnız hasattan gelen Gold · Iron · Stone", gains, costs);
        Add(option.harvestScoreMultiplier, true, "Hasat skoru", "kota ilerlemesi de bununla sayılır", gains, costs);
        foreach (StatModifier m in option.modifiers)
        {
            (string line, bool better) = Describe(m);
            (better ? gains : costs).Add(line);
        }
        var text = new List<string>();
        foreach (string g in gains) text.Add($"<color={Plus}>+ {g}</color>");
        foreach (string c in costs) text.Add($"<color={Minus}>− {c}</color>");
        return string.Join("\n", text);
    }

    private static void Add(float multiplier, bool higherIsBetter, string label, string detail, List<string> gains, List<string> costs)
    {
        if (Mathf.Approximately(multiplier, 1f)) return;
        bool better = higherIsBetter ? multiplier > 1f : multiplier < 1f;
        (better ? gains : costs).Add($"{label} ×{Number(multiplier)}\n<size=78%>{detail}</size>");
    }

    public static (string line, bool better) Describe(StatModifier m)
    {
        switch (m.statType)
        {
            case StatType.AreaRadius when m.operation == ModifierOperation.MorePercent:
                return ($"Vuruş yarıçapı ×{Number(1f + m.value)}\n<size=78%>imleç halkası {(m.value < 0 ? "küçülür" : "büyür")}</size>", m.value > 0f);
            case StatType.RareSpawnChance when m.operation == ModifierOperation.Flat:
                return ($"Nadirlik bonusu {Signed(m.value)} puan\n<size=78%>nadir bitkiler {(m.value > 0 ? "daha sık" : "daha seyrek")} çıkar</size>", m.value > 0f);
            case StatType.PlantSpawnRate when m.operation == ModifierOperation.MorePercent:
                return ($"Üretim aralığı ×{Number(1f + m.value)}\n<size=78%>bitkiler daha {(m.value > 0 ? "seyrek" : "sık")} çıkar</size>", m.value < 0f);
            case StatType.AttackSpeed when m.operation == ModifierOperation.MorePercent:
                return ($"Saldırı aralığı ×{Number(1f + m.value)}", m.value < 0f);
            default:
                string value = m.operation switch
                {
                    ModifierOperation.MorePercent => "×" + Number(1f + m.value),
                    ModifierOperation.AddPercent => $"{(m.value >= 0 ? "+" : "−")}%{Mathf.Abs(m.value * 100f):0.#}",
                    ModifierOperation.Set => "= " + Number(m.value),
                    _ => Signed(m.value)
                };
                bool lowerIsBetter = m.statType == StatType.AttackSpeed || m.statType == StatType.PlantSpawnRate;
                bool increases = m.operation == ModifierOperation.Set || m.value > 0f;
                return ($"{TileBuffText.Name(m.statType)} {value}", lowerIsBetter ? !increases : increases);
        }
    }

    public static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    private static string Signed(float value) => (value >= 0 ? "+" : "−") + Mathf.Abs(value).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}
