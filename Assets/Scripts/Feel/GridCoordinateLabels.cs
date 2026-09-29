using TMPro;
using UnityEngine;

// Satranç tahtası gibi grid koordinatları: kameraya bakan ön kenarlarda harfler (sütun) ve sayılar (satır).
// Adlandırma round haritasıyla (RoundMapUI) birebir aynı: A = x0, "1" = en üst satır (z en büyük).
// İmlecin altındaki tile'ın harfi ve sayısı parlayıp büyür; oyuncu yeri "D4" diye hemen okur.
// Görünüm harita başlıklarıyla aynı: mavi yuvarlak kare + beyaz konturlu yazı.
[DefaultExecutionOrder(950)] // CameraFeel'den sonra: etiketler kamerayla aynı karede döner
public class GridCoordinateLabels : MonoBehaviour
{
    [SerializeField] private Color plateColor = new Color(0.25f, 0.43f, 0.57f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.76f, 0.28f);
    [SerializeField, Min(0.1f)] private float plateSize = 1.15f;
    [SerializeField, Min(0.1f)] private float fontSize = 7.5f;
    [Tooltip("Etiketin grid kenarından uzaklığı (hücre boyutu cinsinden).")]
    [SerializeField, Min(0f)] private float edgeOffset = 1.1f;
    [SerializeField] private float height = 0.35f;
    [SerializeField, Range(1f, 1.6f)] private float highlightScale = 1.3f;

    private sealed class Label
    {
        public Transform root;
        public SpriteRenderer plate;
        public float highlight;
    }

    private Label[] columns, rows;
    private Texture2D plateTexture;
    private Sprite plateSprite;
    private Camera cam;
    private GridManager grid;
    private int hoverX = -1, hoverZ = -1;
    private Quaternion appliedFacing;
    private bool facingApplied;
    private bool labelsVisible = true;

    private void Start()
    {
        grid = GridManager.Instance;
        cam = Camera.main;
        if (grid == null || grid.GetGridSystem() == null)
        {
            enabled = false;
            return;
        }
        Build();
    }

    private void Build()
    {
        plateTexture = CreatePlateTexture(64);
        plateSprite = Sprite.Create(plateTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
        plateSprite.name = "Coordinate Plate (runtime)";

        int width = grid.GetWidth(), height = grid.GetHeight();
        float cell = grid.GetCellSize();
        float offset = edgeOffset * cell;
        columns = new Label[width];
        rows = new Label[height];
        for (int x = 0; x < width; x++)
            columns[x] = CreateLabel(ColumnName(x), new Vector3(x * cell, this.height, -offset));
        for (int z = 0; z < height; z++)
            rows[z] = CreateLabel((height - z).ToString(), new Vector3(-offset, this.height, z * cell));
    }

    private Label CreateLabel(string text, Vector3 position)
    {
        var root = new GameObject("Coordinate " + text).transform;
        root.SetParent(transform, false);
        root.position = position;

        var plateObject = new GameObject("Plate", typeof(SpriteRenderer));
        plateObject.transform.SetParent(root, false);
        plateObject.transform.localScale = Vector3.one * plateSize;
        var plate = plateObject.GetComponent<SpriteRenderer>();
        plate.sprite = plateSprite;
        plate.color = plateColor;
        plate.sortingOrder = 10;
        plate.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        plate.receiveShadows = false;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshPro));
        textObject.transform.SetParent(root, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        var label = textObject.GetComponent<TextMeshPro>();
        ComicUITheme theme = FeelOverlay.Theme;
        if (theme != null && theme.headingFont != null)
        {
            label.font = theme.headingFont;
            if (theme.outlinedText != null) label.fontSharedMaterial = theme.outlinedText;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(plateSize * 1.6f, plateSize);
        label.sortingOrder = 11;
        var textRenderer = label.GetComponent<Renderer>();
        textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        textRenderer.receiveShadows = false;

        return new Label { root = root, plate = plate };
    }

    // Sadece değişen şey yazılır: yön kamera tabanı değişince, ölçek/renk vurgu geçişi sürerken.
    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        // Ayarlardan kapatılabilir; sadece değiştiğinde objeler açılıp kapanır.
        if (GameSettings.GridCoordinates != labelsVisible)
        {
            labelsVisible = GameSettings.GridCoordinates;
            foreach (Label label in columns) label.root.gameObject.SetActive(labelsVisible);
            foreach (Label label in rows) label.root.gameObject.SetActive(labelsVisible);
        }
        if (!labelsVisible) return;
        UpdateHover();

        // Kameranın idle/shake dönüşünü değil taban yönünü izler: etiketler sabit durur, her kare yazılmaz.
        Quaternion facing = CameraFeel.Instance != null ? CameraFeel.Instance.BaseWorldRotation : cam.transform.rotation;
        if (!facingApplied || facing != appliedFacing)
        {
            appliedFacing = facing;
            facingApplied = true;
            ApplyFacing(columns, facing);
            ApplyFacing(rows, facing);
        }

        float step = Time.unscaledDeltaTime * 12f;
        Animate(columns, hoverX, step);
        Animate(rows, hoverZ, step);
    }

    private static void ApplyFacing(Label[] labels, Quaternion facing)
    {
        foreach (Label label in labels) label.root.rotation = facing;
    }

    // Round, yerleştirme, satış ve round önizlemesinde imlecin altındaki tile bulunur (fare yere düşürülür).
    private void UpdateHover()
    {
        hoverX = hoverZ = -1;
        GameManager game = GameManager.Instance;
        if (game == null) return;
        GameStates state = game.CurrentState;
        if (state != GameStates.Round && state != GameStates.Placing && state != GameStates.Selling && !RoundPreviewUI.IsOpen) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
        GridSystem system = grid.GetGridSystem();
        GridPosition position = system.GetGridPosition(ray.GetPoint(enter));
        if (!system.IsValidGridPosition(position)) return;
        hoverX = position.x;
        hoverZ = position.z;
    }

    private void Animate(Label[] labels, int hovered, float step)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            Label label = labels[i];
            float target = i == hovered ? 1f : 0f;
            if (label.highlight == target) continue; // boşta: hiçbir şey yazılmaz
            label.highlight = Mathf.MoveTowards(label.highlight, target, step);
            float eased = label.highlight * label.highlight * (3f - 2f * label.highlight);
            label.root.localScale = Vector3.one * Mathf.Lerp(1f, highlightScale, eased);
            label.plate.color = Color.Lerp(plateColor, highlightColor, eased);
        }
    }

    // Beyaz dolgu + koyu kenar; SpriteRenderer rengi dolguyu boyar, kenar aynı tonun koyusu olur.
    private static Texture2D CreatePlateTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Coordinate Plate (runtime)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        float half = size * 0.5f, radius = size * 0.24f, border = size * 0.07f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Yuvarlak köşeli kare için işaretli mesafe (negatif = içeride).
            float qx = Mathf.Abs(x + 0.5f - half) - (half - radius - 1f);
            float qy = Mathf.Abs(y + 0.5f - half) - (half - radius - 1f);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float alpha = Mathf.Clamp01(0.5f - outside);
            float fill = Mathf.Clamp01(-outside - border + 0.5f);
            byte shade = (byte)Mathf.Lerp(70f, 255f, fill);
            pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static string ColumnName(int index)
    {
        string name = "";
        for (int n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    private void OnDestroy()
    {
        if (plateSprite != null) Destroy(plateSprite);
        if (plateTexture != null) Destroy(plateTexture);
    }
}
