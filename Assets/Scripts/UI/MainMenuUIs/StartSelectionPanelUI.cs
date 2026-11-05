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
        // Sabit yazılar çeviri tablosuna bağlı (LocalizedText): dil değişince kendiliğinden yenilenir.
        Place(Localized(Text(root, "Title", 50f, Color.white, ""), "ui.start.title"), new Vector2(0f, 492f), new Vector2(1600f, 70f));
        Place(Localized(Text(root, "Subtitle", 24f, new Color(1f, 0.93f, 0.78f), ""), "ui.start.subtitle"), new Vector2(0f, 448f), new Vector2(1600f, 36f));
        Place(Localized(Text(root, "Farmer Label", 30f, Amber, ""), "ui.start.farmer_row"), new Vector2(0f, 406f), new Vector2(1200f, 44f));
        farmerRow = Row(root, "Farmers", new Vector2(0f, 200f), FarmerHeight);
        Place(Localized(Text(root, "Scythe Label", 30f, Amber, ""), "ui.start.scythe_row"), new Vector2(0f, -4f), new Vector2(1200f, 44f));
        scytheRow = Row(root, "Scythes", new Vector2(0f, -180f), ScytheHeight);
        summary = Text(root, "Summary", 30f, Color.white, "");
        Place(summary, new Vector2(0f, -368f), new Vector2(1600f, 44f));
        Button back = CreateButton(root, "Back", "ui.start.back", "blue", new Vector2(240f, 80f));
        Place((RectTransform)back.transform, new Vector2(-260f, -458f), new Vector2(240f, 80f));
        back.onClick.AddListener(() => BackRequested?.Invoke());
        Button start = CreateButton(root, "Start", "ui.start.begin", "green", new Vector2(340f, 86f));
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
        Opened?.Invoke();
    }

    // Seçim ekranı açıldı (diyalog: Eri'nin ilk konuşması burada başlar).
    public static event System.Action Opened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpened() => Opened = null;

    public void Close() => gameObject.SetActive(false);

    // Kart yazıları (ad, tarz, etkiler, alt satır, kilit) koddan yazılır: dil değişince yeniden doldurulur.
    private void OnEnable() => GameText.Changed += Refresh;
    private void OnDisable() => GameText.Changed -= Refresh;

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
            ? GameText.Format("ui.start.summary", $"<color=#F7C05D>{SelectedFarmer.DisplayName}</color>", $"<color=#F7C05D>{SelectedScythe.DisplayName}</color>")
            : "";
    }

    private void Fill(Card card, bool selected)
    {
        if (!card.root.gameObject.activeSelf || card.option == null) return;
        StartOptionSO option = card.option;
        bool open = MetaSave.IsUnlocked(option);
        card.title.text = option.DisplayName;
        card.style.text = option.Playstyle;
        card.effects.text = StartOptionText.Effects(option);
        card.button.interactable = open;
        card.lockCover.SetActive(!open);
        card.paper.color = open ? Paper : LockedPaper;
        card.outline.color = selected ? Chosen : Ink;
        card.header.color = selected ? new Color32(150, 214, 120, 255) : Amber;
        card.footer.text = !open ? "" : GameText.Get(selected ? "ui.start.selected" : "ui.start.click_to_select");
        card.footer.color = selected ? Chosen : Muted;
        if (!open)
        {
            QuestSO quest = option.unlockQuest;
            int best = MetaSave.BestProgress(quest);
            card.lockText.text = quest != null
                ? $"{GameText.Get("ui.start.locked_quest")}\n<size=72%>{quest.Description}\n<color=#F7C05D>" +
                  $"{GameText.Format("ui.start.best_run", Mathf.Min(best, quest.target), quest.target)}</color></size>"
                : GameText.Get("ui.start.locked");
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

    // Düğme yazısı çeviri anahtarından (LocalizedText).
    private Button CreateButton(RectTransform parent, string name, string captionKey, string palette, Vector2 size)
    {
        if (theme != null)
        {
            Button themed = theme.CreateButton(parent, name, "", palette, size);
            Localized(themed.GetComponentInChildren<TMP_Text>(true), captionKey);
            return themed;
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var label = Localized(Text((RectTransform)go.transform, "Label", 30f, Ink, ""), captionKey);
        FeelOverlay.Stretch(label.rectTransform);
        return go.GetComponent<Button>();
    }

    private static T Localized<T>(T label, string key) where T : TMP_Text
    {
        LocalizedText.Bind(label, key);
        return label;
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
// Yüzde bonusu "×", yüzde puanı "puan" diye yazılır. Yazılar çeviri tablosundan (ui.start.effect.*); ondalık ayırıcı etkin dile göre
// (tr "1,25", en "1.25"). Renk ve boyut etiketleri kodda kalır: çevirmen yalnız cümleyi görür.
public static class StartOptionText
{
    private const string Plus = "#2E7D32", Minus = "#B42318";

    public static string Effects(StartOptionSO option)
    {
        if (option == null) return "";
        if (option.IsNeutral) return $"<color=#6E606E>{GameText.Get("ui.start.neutral")}\n<size=80%>{GameText.Get("ui.start.neutral.detail")}</size></color>";
        var gains = new List<string>();
        var costs = new List<string>();
        Add(option.directDamageMultiplier, true, "ui.start.effect.direct_damage", gains, costs);
        Add(option.harvestResourceMultiplier, true, "ui.start.effect.harvest_resources", gains, costs);
        Add(option.harvestScoreMultiplier, true, "ui.start.effect.harvest_score", gains, costs);
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

    // key: "… ×{0}" (çarpan); key + ".detail": küçük alt satır.
    private static void Add(float multiplier, bool higherIsBetter, string key, List<string> gains, List<string> costs)
    {
        if (Mathf.Approximately(multiplier, 1f)) return;
        bool better = higherIsBetter ? multiplier > 1f : multiplier < 1f;
        (better ? gains : costs).Add(Line(GameText.Format(key, Number(multiplier)), key + ".detail"));
    }

    private static string Line(string head, string detailKey) => $"{head}\n<size=78%>{GameText.Get(detailKey)}</size>";

    public static (string line, bool better) Describe(StatModifier m)
    {
        switch (m.statType)
        {
            case StatType.AreaRadius when m.operation == ModifierOperation.MorePercent:
                return (Line(GameText.Format("ui.start.effect.radius", Number(1f + m.value)),
                    m.value < 0 ? "ui.start.effect.radius.smaller" : "ui.start.effect.radius.larger"), m.value > 0f);
            case StatType.RareSpawnChance when m.operation == ModifierOperation.Flat:
                return (Line(GameText.Format("ui.start.effect.rarity", Signed(m.value)),
                    m.value > 0 ? "ui.start.effect.rarity.more" : "ui.start.effect.rarity.less"), m.value > 0f);
            case StatType.PlantSpawnRate when m.operation == ModifierOperation.MorePercent:
                return (Line(GameText.Format("ui.start.effect.spawn_interval", Number(1f + m.value)),
                    m.value > 0 ? "ui.start.effect.spawn_interval.slower" : "ui.start.effect.spawn_interval.faster"), m.value < 0f);
            case StatType.AttackSpeed when m.operation == ModifierOperation.MorePercent:
                return (GameText.Format("ui.start.effect.attack_interval", Number(1f + m.value)), m.value < 0f);
            default:
                string value = m.operation switch
                {
                    ModifierOperation.MorePercent => "×" + Number(1f + m.value),
                    ModifierOperation.AddPercent => (m.value >= 0 ? "+" : "−") +
                        GameText.Format("ui.format.percent", Mathf.Abs(m.value * 100f).ToString("0.#", GameText.Culture)),
                    ModifierOperation.Set => "= " + Number(m.value),
                    _ => Signed(m.value)
                };
                bool lowerIsBetter = m.statType == StatType.AttackSpeed || m.statType == StatType.PlantSpawnRate;
                bool increases = m.operation == ModifierOperation.Set || m.value > 0f;
                return ($"{TileBuffText.Name(m.statType)} {value}", lowerIsBetter ? !increases : increases);
        }
    }

    public static string Number(float value) => value.ToString("0.00", GameText.Culture);
    private static string Signed(float value) => (value >= 0 ? "+" : "−") + Mathf.Abs(value).ToString("0.##", GameText.Culture);
}
