using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Round sonu panelinin sağındaki kart: bu round seçilen kartların hangi tile'a düştüğü ("B3 · Verimli · Rare").
// Liste ProgressionManager'da tutulur ve yeni round başlayınca temizlenir. Satırlar haritadaki
// parlamalarla aynı sırada ve gecikmeyle belirir. Unscaled time kullanır (RoundEnd'de timeScale 0).
public sealed class RoundNewTilesUI : MonoBehaviour
{
    public const float Width = 360f;
    public const float TopY = 250f;
    private const float HeaderHeight = 64f;
    private const float RowHeight = 54f;
    private const int MaxRows = 6;
    // RoundMapUI'daki parlama gecikmesiyle aynı: satır ve tile birlikte belirir.
    private const float FirstRowDelay = 0.3f;
    private const float RowInterval = 0.12f;

    private static readonly Color Ink = new Color32(54, 39, 54, 255);

    private sealed class Row
    {
        public RectTransform root;
        public CanvasGroup group;
        public RectTransform swatchRoot;
        public ComicPopupPlate swatch;
        public TextMeshProUGUI coordinate, label;
    }

    private RectTransform card;
    private TextMeshProUGUI title;
    private readonly List<Row> rows = new();
    private int activeRows;
    private float elapsed;
    private bool animating;
    private ComicUITheme theme;

    public static RoundNewTilesUI Attach(GameObject roundEndPanel)
    {
        if (roundEndPanel == null) return null;
        var existing = roundEndPanel.GetComponentInChildren<RoundNewTilesUI>(true);
        if (existing != null) return existing;
        var container = new GameObject("Round New Tiles", typeof(RectTransform));
        container.layer = roundEndPanel.layer;
        var rect = (RectTransform)container.transform;
        rect.SetParent(roundEndPanel.transform, false);
        FeelOverlay.Stretch(rect);
        return container.AddComponent<RoundNewTilesUI>();
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        BuildCard();
    }

    private void OnEnable()
    {
        ProgressionManager progression = ProgressionManager.Instance;
        IReadOnlyList<GroundCell> cells = progression != null ? progression.RoundAppliedCells : null;
        int count = cells != null ? cells.Count : 0;
        card.gameObject.SetActive(count > 0);
        animating = false;
        if (count == 0) return;

        title.text = count == 1 ? "YENİ TILE" : $"YENİ TILE'LAR ({count})";
        activeRows = 0;
        int shown = count > MaxRows ? MaxRows - 1 : count;
        for (int i = 0; i < shown; i++) SetRow(cells[i]);
        if (count > shown) SetMoreRow(count - shown);
        for (int i = activeRows; i < rows.Count; i++) rows[i].root.gameObject.SetActive(false);
        card.sizeDelta = new Vector2(Width, HeaderHeight + 10f + activeRows * RowHeight + 12f);

        elapsed = 0f;
        animating = true;
        Animate();
    }

    private void SetRow(GroundCell cell)
    {
        Row row = NextRow();
        TileModifierSO modifier = cell != null ? cell.CurrentModifier : null;
        row.swatchRoot.gameObject.SetActive(true);
        row.swatch.color = modifier != null ? modifier.tileColor : Color.white;
        row.coordinate.text = cell != null ? RoundMapUI.CellName(cell.GetGridPosition()) : "?";
        row.label.rectTransform.offsetMin = new Vector2(66f, 0f);
        row.label.text = modifier == null ? "?" :
            $"{modifier.modifierName}\n<size=72%><color=#{ColorUtility.ToHtmlStringRGB(RarityColor(modifier.rarity))}>{modifier.rarity.ToString().ToUpperInvariant()}</color></size>";
    }

    private void SetMoreRow(int remaining)
    {
        Row row = NextRow();
        row.swatchRoot.gameObject.SetActive(false);
        row.label.rectTransform.offsetMin = new Vector2(4f, 0f);
        row.label.text = $"+{remaining} tile daha · haritada parlıyor";
    }

    private Row NextRow()
    {
        if (activeRows == rows.Count) rows.Add(CreateRow(rows.Count));
        Row row = rows[activeRows];
        row.root.gameObject.SetActive(true);
        row.root.anchoredPosition = new Vector2(16f, -(HeaderHeight + 10f + RowHeight * 0.5f) - activeRows * RowHeight);
        activeRows++;
        return row;
    }

    public static Color RarityColor(TileRarity rarity) => rarity switch
    {
        TileRarity.Rare => new Color32(48, 110, 210, 255),
        TileRarity.Epic => new Color32(140, 70, 190, 255),
        TileRarity.Legendary => new Color32(214, 128, 16, 255),
        _ => new Color32(112, 100, 96, 255)
    };

    private void Update()
    {
        if (!animating) return;
        elapsed += Time.unscaledDeltaTime;
        Animate();
    }

    private void Animate()
    {
        float cardT = Mathf.Clamp01((elapsed - 0.15f) / 0.25f);
        card.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, OutBack(cardT));
        bool done = cardT >= 1f;
        for (int i = 0; i < activeRows; i++)
        {
            float t = Mathf.Clamp01((elapsed - FirstRowDelay - i * RowInterval) / 0.25f);
            rows[i].group.alpha = t;
            rows[i].root.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, OutBack(t));
            done &= t >= 1f;
        }
        if (done) animating = false;
    }

    private static float OutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void BuildCard()
    {
        card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
        card.gameObject.layer = gameObject.layer;
        card.SetParent(transform, false);
        card.anchorMin = card.anchorMax = new Vector2(1f, 0.5f);
        card.pivot = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-48f, TopY);
        card.sizeDelta = new Vector2(Width, 200f);

        // Round özeti kartıyla aynı çizgi roman kağıdı.
        Plate(card, "Ink shadow", new Color32(40, 29, 43, 220), Vector2.zero, Vector2.one, new Vector2(6f, -7f), new Vector2(6f, -7f));
        Plate(card, "Ink outline", Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Plate(card, "Cream paper", new Color32(255, 242, 210, 255), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        Plate(card, "Yellow header", FreshTileRing.GlowColor, new Vector2(0f, 1f), Vector2.one, new Vector2(7f, -HeaderHeight), new Vector2(-7f, -7f));

        title = CreateText(card, "Title", 28f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(12f, -HeaderHeight + 4f);
        title.rectTransform.offsetMax = new Vector2(-12f, -8f);
    }

    private Row CreateRow(int index)
    {
        var root = new GameObject("Row " + index, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.gameObject.layer = gameObject.layer;
        root.SetParent(card, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(Width - 32f, RowHeight - 6f);

        // Tile rengi karesi + üstünde haritadaki koordinat adı.
        var swatchRoot = new GameObject("Swatch", typeof(RectTransform)).GetComponent<RectTransform>();
        swatchRoot.gameObject.layer = gameObject.layer;
        swatchRoot.SetParent(root, false);
        swatchRoot.anchorMin = swatchRoot.anchorMax = new Vector2(0f, 0.5f);
        swatchRoot.pivot = new Vector2(0f, 0.5f);
        swatchRoot.sizeDelta = new Vector2(56f, 44f);
        Plate(swatchRoot, "Outline", Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ComicPopupPlate swatch = Plate(swatchRoot, "Tile color", Color.white, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

        TextMeshProUGUI coordinate = CreateText(swatchRoot, "Coordinate", 26f, TextAlignmentOptions.Center);
        coordinate.rectTransform.anchorMin = Vector2.zero;
        coordinate.rectTransform.anchorMax = Vector2.one;
        coordinate.rectTransform.offsetMin = coordinate.rectTransform.offsetMax = Vector2.zero;
        if (theme != null && theme.outlinedText != null) coordinate.fontSharedMaterial = theme.outlinedText;
        coordinate.color = Color.white;

        TextMeshProUGUI label = CreateText(root, "Label", 24f, TextAlignmentOptions.MidlineLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(66f, 0f);
        label.rectTransform.offsetMax = Vector2.zero;
        label.lineSpacing = -10f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 24f;

        return new Row
        {
            root = root, group = root.GetComponent<CanvasGroup>(),
            swatchRoot = swatchRoot, swatch = swatch, coordinate = coordinate, label = label
        };
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var plate = rect.gameObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, float size, TextAlignmentOptions alignment)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.gameObject.layer = parent.gameObject.layer;
        text.rectTransform.SetParent(parent, false);
        if (theme != null) theme.StylePopupText(text);
        else text.color = Ink;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
