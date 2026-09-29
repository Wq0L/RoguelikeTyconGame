using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Round sonu önizlemesi: harita ve butonlar kalkar, dünyada bitkiler ve efektler gizlenir; sadece saksılar ve
// boyalı tile'lar kalır. Her saksının üstünde aktif rezonans rozetleri, üstte toplam rezonans şeridi, bu round'un
// kartlarının düştüğü tile'larda "YENİ" etiketi. Tile'ın üstüne gelince tile + saksı bilgisi çıkar.
// Oyun durumu değişmez (RoundEnd, timeScale 0); UIManager bir panel gibi açıp kapatır. Unscaled time kullanır.
[DefaultExecutionOrder(960)] // CameraFeel'den sonra: rozetler kameranın o kareki yerine oturur
public sealed class RoundPreviewUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private const float BadgeSize = 44f;
    private const float BadgeGap = 4f;
    private const float ChipHeight = 50f;
    private const float ChipMaxRowWidth = 1400f;
    private const float HeaderTop = 112f;
    private const float HeaderHeight = 84f;

    private sealed class PlanterMarker
    {
        public RectTransform root, pill;
        public readonly List<ResonanceBadgeGraphic> badges = new();
        public Vector3 anchor;
    }

    private sealed class TileTag
    {
        public RectTransform root;
        public TextMeshProUGUI label;
        public Vector3 anchor;
        public float halfSize;
    }

    private sealed class Chip
    {
        public RectTransform root;
        public ResonanceBadgeGraphic badge;
        public TextMeshProUGUI label;
    }

    private ComicUITheme theme;
    private RectTransform markerLayer, chipLayer;
    private TextMeshProUGUI emptyHint;
    private ComicPopupView details;
    private RectTransform detailsRect;
    private Canvas rootCanvas;

    private readonly List<PlanterMarker> markers = new();
    private readonly List<TileTag> tags = new();
    private readonly List<Chip> chips = new();
    private int activeMarkers, activeTags, activeChips;

    private readonly List<Renderer> hiddenRenderers = new();
    private static readonly List<Renderer> rendererScratch = new();
    private readonly HashSet<PlanterBrain> planterScratch = new();
    private readonly Dictionary<string, int> resonanceCounts = new();
    private readonly List<ActiveResonance> resonanceOrder = new();

    private GroundCell hoveredCell;
    private bool detailsVisible;

    // Round sonu paneline "ÖNİZLEME" butonunu, panelin yanına da (kapalı) önizleme panelini kurar.
    public static RoundPreviewUI Attach(GameObject roundEndPanel, UIManager ui)
    {
        if (roundEndPanel == null || ui == null) return null;
        Transform parent = roundEndPanel.transform.parent;
        var existing = parent != null ? parent.GetComponentInChildren<RoundPreviewUI>(true) : null;
        if (existing != null) return existing;
        ComicUITheme theme = FeelOverlay.Theme;
        if (theme == null) return null;

        // Sağ sütun: "yeni tile'lar" kartının hemen üstünde, kartla ortalı.
        Button open = theme.CreateButton(roundEndPanel.transform, "Preview Button", "ÖNİZLEME", "blue", new Vector2(300f, 84f));
        var openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = new Vector2(1f, 0.5f);
        openRect.pivot = new Vector2(0.5f, 0f);
        openRect.anchoredPosition = new Vector2(-48f - RoundNewTilesUI.Width * 0.5f, RoundNewTilesUI.TopY + 18f);
        AddEyeIcon(open);
        open.onClick.AddListener(ui.OpenRoundPreview);

        var panel = new GameObject("Round Preview", typeof(RectTransform));
        panel.layer = roundEndPanel.layer;
        var rect = (RectTransform)panel.transform;
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(roundEndPanel.transform.GetSiblingIndex() + 1);
        FeelOverlay.Stretch(rect);
        panel.SetActive(false);
        var preview = panel.AddComponent<RoundPreviewUI>();
        preview.Build(theme, ui);
        return preview;
    }

    private static void AddEyeIcon(Button button)
    {
        var icon = new GameObject("Action Icon", typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        icon.gameObject.layer = button.gameObject.layer;
        icon.SetParent(button.transform, false);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(46f, 3f);
        icon.sizeDelta = new Vector2(58f, 58f);
        var graphic = icon.gameObject.AddComponent<ComicActionIcon>();
        graphic.shape = ComicActionIcon.Shape.Eye;
        graphic.raycastTarget = false;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.rectTransform.offsetMin = new Vector2(80f, 10f);
    }

    private void Build(ComicUITheme theme, UIManager ui)
    {
        this.theme = theme;
        // Dünyaya bağlı işaretler en altta; başlık, şerit ve buton üstlerinde kalır.
        markerLayer = Layer("World Markers");

        var header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
        header.gameObject.layer = gameObject.layer;
        header.SetParent(transform, false);
        header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -HeaderTop);
        header.sizeDelta = new Vector2(660f, HeaderHeight);
        Plate(header, "Ink shadow", new Color32(40, 29, 43, 220), new Vector2(6f, -7f), new Vector2(6f, -7f));
        Plate(header, "Ink outline", Ink, Vector2.zero, Vector2.zero);
        Plate(header, "Amber face", new Color32(247, 192, 93, 255), new Vector2(4f, 4f), new Vector2(-4f, -4f));
        TextMeshProUGUI title = Text(header, "Title", 36f, TextAlignmentOptions.Top);
        title.rectTransform.offsetMin = new Vector2(16f, 30f);
        title.rectTransform.offsetMax = new Vector2(-16f, -6f);
        title.text = "ÖNİZLEME";
        TextMeshProUGUI hint = Text(header, "Hint", 19f, TextAlignmentOptions.Bottom);
        hint.rectTransform.offsetMin = new Vector2(16f, 8f);
        hint.rectTransform.offsetMax = new Vector2(-16f, -48f);
        hint.text = "Tile'ın üstüne gel: detay  ·  TAB / ESC: geri dön";

        chipLayer = Layer("Resonance Chips");
        chipLayer.anchorMin = chipLayer.anchorMax = new Vector2(0.5f, 1f);
        chipLayer.pivot = new Vector2(0.5f, 1f);
        chipLayer.anchoredPosition = new Vector2(0f, -(HeaderTop + HeaderHeight + 14f));
        chipLayer.sizeDelta = new Vector2(ChipMaxRowWidth, ChipHeight);
        emptyHint = Text(chipLayer, "Empty", 22f, TextAlignmentOptions.Top);
        emptyHint.color = Color.white;
        if (theme.outlinedText != null) emptyHint.fontSharedMaterial = theme.outlinedText;
        emptyHint.text = "Henüz aktif rezonans yok · aynı tipteki tile'ları bir saksının altında topla";

        Button back = theme.CreateButton(transform, "Back Button", "GERİ DÖN", "blue", new Vector2(380f, 96f));
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 60f);
        back.onClick.AddListener(ui.CloseRoundPreview);

        var popup = new GameObject("Tile Details", typeof(RectTransform));
        popup.layer = gameObject.layer;
        detailsRect = (RectTransform)popup.transform;
        detailsRect.SetParent(transform, false);
        detailsRect.anchorMin = detailsRect.anchorMax = Vector2.zero;
        popup.SetActive(false);
        details = popup.AddComponent<ComicPopupView>();
    }

    private void OnEnable()
    {
        IsOpen = true;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        HideWorldClutter();
        RefreshMarkers();
        RefreshTags();
        RefreshChips();
        SetDetails(null);
    }

    private void OnDisable()
    {
        IsOpen = false;
        RestoreWorld();
        SetDetails(null);
    }

    // ---------- Dünya: sadece saksılar ----------

    // Bitkiler ve efektler (hasar yazıları, parçacıklar) sadece görünmez olur; objelere ve oyun durumuna dokunulmaz.
    private void HideWorldClutter()
    {
        hiddenRenderers.Clear();
        HideUnder(PlantPool.ForScene(gameObject.scene).transform);
        if (VFXManager.Instance != null) HideUnder(VFXManager.Instance.transform);
    }

    private void HideUnder(Transform root)
    {
        root.GetComponentsInChildren(false, rendererScratch);
        foreach (Renderer renderer in rendererScratch)
        {
            if (!renderer.enabled) continue;
            renderer.enabled = false;
            hiddenRenderers.Add(renderer);
        }
        rendererScratch.Clear();
    }

    private void RestoreWorld()
    {
        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null) renderer.enabled = true;
        hiddenRenderers.Clear();
    }

    // ---------- Saksı rozetleri ----------

    private void RefreshMarkers()
    {
        activeMarkers = 0;
        planterScratch.Clear();
        GridManager grid = GridManager.Instance;
        GridSystem system = grid != null ? grid.GetGridSystem() : null;
        if (system != null)
        {
            for (int x = 0; x < grid.GetWidth(); x++)
            for (int z = 0; z < grid.GetHeight(); z++)
            {
                PlanterBrain planter = system.GetGridObject(new GridPosition(x, z))?.GetPlanterBrain();
                if (planter == null || !planterScratch.Add(planter) || planter.ActiveResonances.Count == 0) continue;
                AddMarker(planter);
            }
        }
        for (int i = activeMarkers; i < markers.Count; i++) markers[i].root.gameObject.SetActive(false);
    }

    private void AddMarker(PlanterBrain planter)
    {
        if (activeMarkers == markers.Count) markers.Add(CreateMarker(markers.Count));
        PlanterMarker marker = markers[activeMarkers++];
        IReadOnlyList<ActiveResonance> active = planter.ActiveResonances;
        while (marker.badges.Count < active.Count) marker.badges.Add(Badge(marker.root, BadgeSize));
        for (int i = 0; i < marker.badges.Count; i++)
        {
            ResonanceBadgeGraphic badge = marker.badges[i];
            bool used = i < active.Count;
            badge.gameObject.SetActive(used);
            if (!used) continue;
            badge.SetRecipe(ResonanceManager.Identity(active[i]));
            badge.rectTransform.anchoredPosition = new Vector2((i - (active.Count - 1) * 0.5f) * (BadgeSize + BadgeGap), 0f);
        }
        marker.pill.sizeDelta = new Vector2(active.Count * (BadgeSize + BadgeGap) + 8f, BadgeSize + 10f);
        marker.anchor = TopOf(planter.gameObject) + Vector3.up * 0.15f;
        marker.root.gameObject.SetActive(true);
    }

    private PlanterMarker CreateMarker(int index)
    {
        var root = Rect("Planter " + index, markerLayer);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(BadgeSize, BadgeSize + 10f);
        var pill = Rect("Pill", root);
        pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0.5f);
        var plate = pill.gameObject.AddComponent<ComicPopupPlate>();
        plate.color = new Color(Ink.r, Ink.g, Ink.b, 0.82f);
        plate.raycastTarget = false;
        return new PlanterMarker { root = root, pill = pill };
    }

    // Saksının üst yüzeyinin ortası: rozetler saksının hemen üstünde durur.
    private static Vector3 TopOf(GameObject planter)
    {
        planter.GetComponentsInChildren(false, rendererScratch);
        bool found = false;
        Bounds bounds = new Bounds(planter.transform.position, Vector3.zero);
        foreach (Renderer renderer in rendererScratch)
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        rendererScratch.Clear();
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    // ---------- Bu round'un tile'ları ----------

    private void RefreshTags()
    {
        activeTags = 0;
        ProgressionManager progression = ProgressionManager.Instance;
        if (progression != null)
        {
            foreach (GroundCell cell in progression.RoundAppliedCells)
            {
                if (cell == null) continue;
                if (activeTags == tags.Count) tags.Add(CreateTag(tags.Count));
                TileTag tag = tags[activeTags++];
                tag.label.text = FreshTileRing.LabelFor(cell) + " · " + RoundMapUI.CellName(cell.GetGridPosition());
                Renderer ground = cell.GroundRenderer;
                tag.anchor = ground != null
                    ? new Vector3(ground.bounds.center.x, ground.bounds.max.y, ground.bounds.center.z)
                    : cell.transform.position;
                tag.halfSize = GridManager.Instance != null ? GridManager.Instance.GetCellSize() * 0.5f : 0.5f;
                tag.root.gameObject.SetActive(true);
            }
        }
        for (int i = activeTags; i < tags.Count; i++) tags[i].root.gameObject.SetActive(false);
    }

    private TileTag CreateTag(int index)
    {
        var root = Rect("New Tile " + index, markerLayer);
        // Tile'ın ekrandaki en üst köşesinin üstünde durur; zemindeki sarı çerçeveyi örtmez.
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(132f, 38f);
        root.localRotation = Quaternion.Euler(0f, 0f, -6f);
        Plate(root, "Shadow", Ink, new Vector2(2f, -3f), new Vector2(2f, -3f));
        Plate(root, "Outline", Ink, Vector2.zero, Vector2.zero);
        Plate(root, "Face", FreshTileRing.GlowColor, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        TextMeshProUGUI label = Text(root, "Label", 22f, TextAlignmentOptions.Center);
        label.rectTransform.offsetMin = new Vector2(6f, 2f);
        label.rectTransform.offsetMax = new Vector2(-6f, -2f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 22f;
        return new TileTag { root = root, label = label };
    }

    // ---------- Üst şerit: tüm aktif rezonanslar ----------

    private void RefreshChips()
    {
        resonanceCounts.Clear();
        resonanceOrder.Clear();
        foreach (PlanterBrain planter in planterScratch)
        {
            if (planter == null) continue;
            foreach (ActiveResonance resonance in planter.ActiveResonances)
            {
                string id = ResonanceManager.Identity(resonance);
                resonanceCounts.TryGetValue(id, out int count);
                if (count == 0) resonanceOrder.Add(resonance);
                resonanceCounts[id] = count + 1;
            }
        }
        // Çok saksıda olan önce; eşitse isim sırası. Her açılışta aynı düzen.
        resonanceOrder.Sort((a, b) =>
        {
            int byCount = resonanceCounts[ResonanceManager.Identity(b)].CompareTo(resonanceCounts[ResonanceManager.Identity(a)]);
            return byCount != 0 ? byCount : string.CompareOrdinal(a.resonanceName, b.resonanceName);
        });

        activeChips = 0;
        emptyHint.gameObject.SetActive(resonanceOrder.Count == 0);
        foreach (ActiveResonance resonance in resonanceOrder)
        {
            if (activeChips == chips.Count) chips.Add(CreateChip(chips.Count));
            Chip chip = chips[activeChips++];
            int count = resonanceCounts[ResonanceManager.Identity(resonance)];
            chip.badge.SetRecipe(ResonanceManager.Identity(resonance));
            chip.label.text = count > 1 ? $"{resonance.resonanceName} <color=#8C5A1E>×{count}</color>" : resonance.resonanceName;
            chip.root.gameObject.SetActive(true);
            float width = 12f + 40f + 8f + chip.label.GetPreferredValues(chip.label.text, 600f, ChipHeight).x + 16f;
            chip.root.sizeDelta = new Vector2(width, ChipHeight);
        }
        for (int i = activeChips; i < chips.Count; i++) chips[i].root.gameObject.SetActive(false);
        LayoutChips();
    }

    // Satır satır ortalı akış: sığmayan çip bir alt satıra geçer.
    private void LayoutChips()
    {
        int rowStart = 0;
        float y = 0f;
        while (rowStart < activeChips)
        {
            float width = 0f;
            int rowEnd = rowStart;
            while (rowEnd < activeChips)
            {
                float next = width + (rowEnd > rowStart ? 10f : 0f) + chips[rowEnd].root.sizeDelta.x;
                if (next > ChipMaxRowWidth && rowEnd > rowStart) break;
                width = next;
                rowEnd++;
            }
            float x = -width * 0.5f;
            for (int i = rowStart; i < rowEnd; i++)
            {
                RectTransform root = chips[i].root;
                root.anchoredPosition = new Vector2(x + root.sizeDelta.x * 0.5f, y);
                x += root.sizeDelta.x + 10f;
            }
            y -= ChipHeight + 8f;
            rowStart = rowEnd;
        }
    }

    private Chip CreateChip(int index)
    {
        var root = Rect("Chip " + index, chipLayer);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        Plate(root, "Shadow", new Color32(40, 29, 43, 220), new Vector2(3f, -4f), new Vector2(3f, -4f));
        Plate(root, "Outline", Ink, Vector2.zero, Vector2.zero);
        Plate(root, "Paper", new Color32(255, 242, 210, 255), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        ResonanceBadgeGraphic badge = Badge(root, 40f);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(12f + 20f, 0f);
        TextMeshProUGUI label = Text(root, "Label", 22f, TextAlignmentOptions.MidlineLeft);
        label.rectTransform.offsetMin = new Vector2(12f + 40f + 8f, 0f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        return new Chip { root = root, badge = badge, label = label };
    }

    // ---------- Her kare: konumlar ve tile bilgisi ----------

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        for (int i = 0; i < activeMarkers; i++) Place(markers[i].root, cam.WorldToScreenPoint(markers[i].anchor));
        for (int i = 0; i < activeTags; i++) Place(tags[i].root, AboveTile(cam, tags[i]));
        UpdateHover(cam);
    }

    // Tile'ın dört köşesinden ekranda en yukarıda olanın biraz üstü (kamera açısından bağımsız).
    private static Vector3 AboveTile(Camera cam, TileTag tag)
    {
        Vector3 screen = cam.WorldToScreenPoint(tag.anchor);
        float top = screen.y;
        for (int corner = 0; corner < 4; corner++)
        {
            var offset = new Vector3(corner % 2 == 0 ? -tag.halfSize : tag.halfSize, 0f, corner < 2 ? -tag.halfSize : tag.halfSize);
            top = Mathf.Max(top, cam.WorldToScreenPoint(tag.anchor + offset).y);
        }
        screen.y = top + 4f;
        return screen;
    }

    private void Place(RectTransform target, Vector3 screen)
    {
        Vector2 local = default;
        bool visible = screen.z > 0f && ToLocal(screen, out local);
        if (target.gameObject.activeSelf != visible) target.gameObject.SetActive(visible);
        if (visible) target.anchoredPosition = local;
    }

    private bool ToLocal(Vector2 screen, out Vector2 local)
    {
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        // markerLayer tam ekran ve ortalı; anchoredPosition merkeze göre.
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(markerLayer, screen, uiCamera, out local);
    }

    private void UpdateHover(Camera cam)
    {
        GroundCell cell = null;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        GridManager grid = GridManager.Instance;
        if (!overUI && grid != null && grid.GetGridSystem() != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
            {
                GridSystem system = grid.GetGridSystem();
                GridPosition position = system.GetGridPosition(ray.GetPoint(enter));
                if (system.IsValidGridPosition(position))
                    cell = system.GetGridObject(position)?.GetGroundCellCached();
            }
        }
        // Kilitli ve anlatacak bir şeyi olmayan tile'da bilgi yok.
        if (cell != null && (cell.IsLocked || cell.CurrentModifier == null &&
            (cell.Planter == null || cell.Planter.ActiveResonances.Count == 0))) cell = null;
        if (cell != hoveredCell) SetDetails(cell);
        if (detailsVisible) PositionDetails();
    }

    private void SetDetails(GroundCell cell)
    {
        hoveredCell = cell;
        detailsVisible = cell != null;
        if (detailsRect == null) return;
        detailsRect.gameObject.SetActive(detailsVisible);
        if (!detailsVisible) return;
        GridTileToolTipContent.Fill(details, cell, RoundMapUI.CellName(cell.GetGridPosition()));
    }

    // İmlecin yanında; ekranın sağ/üst yarısında karşı tarafa açılır, kenardan taşmaz.
    private void PositionDetails()
    {
        details.Fit();
        RectTransform parent = (RectTransform)transform;
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, uiCamera, out Vector2 mouse)) return;
        Rect area = parent.rect;
        bool right = Input.mousePosition.x > Screen.width * 0.5f;
        bool top = Input.mousePosition.y > Screen.height * 0.5f;
        detailsRect.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        Vector2 position = mouse - area.min + new Vector2(right ? -24f : 24f, top ? -24f : 24f);
        Vector2 size = detailsRect.rect.size;
        position.x = right ? Mathf.Max(position.x, size.x + 12f) : Mathf.Min(position.x, area.width - size.x - 12f);
        position.y = top ? Mathf.Max(position.y, size.y + 12f) : Mathf.Min(position.y, area.height - size.y - 12f);
        detailsRect.anchoredPosition = position;
    }

    // ---------- Küçük yardımcılar ----------

    private RectTransform Layer(string name)
    {
        RectTransform rect = Rect(name, transform);
        FeelOverlay.Stretch(rect);
        return rect;
    }

    private RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = gameObject.layer;
        rect.SetParent(parent, false);
        return rect;
    }

    private void Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Rect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.gameObject.AddComponent<CanvasRenderer>();
        var plate = rect.gameObject.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
    }

    private ResonanceBadgeGraphic Badge(RectTransform parent, float size)
    {
        RectTransform rect = Rect("Badge", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        var badge = rect.gameObject.AddComponent<ResonanceBadgeGraphic>();
        badge.raycastTarget = false;
        return badge;
    }

    private TextMeshProUGUI Text(RectTransform parent, string name, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(text);
        else text.color = Ink;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
