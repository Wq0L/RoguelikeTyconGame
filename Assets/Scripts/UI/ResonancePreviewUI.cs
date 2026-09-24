using System.Collections.Generic;
using UnityEngine;

// Owned exclusively by the placement ghost. No icons are attached to placed planters.
public sealed class ResonancePreviewUI : MonoBehaviour
{
    readonly Dictionary<TileModifierType, int> counts = new();
    readonly List<StatModifier> modifiers = new();
    readonly List<ActiveResonance> active = new();
    readonly List<ResonanceBadgeGraphic> badges = new();
    readonly int[] previousCounts = new int[10];
    GameObject canvasObject;
    RectTransform row;
    Canvas canvas;
    Renderer[] ghostRenderers;
    ResonanceRulesSO previousRules;
    bool initialized;
    ComicPopupView details;
    static bool detailsOpen = true;
    string previousDetails;
    readonly System.Text.StringBuilder description = new();
    public bool DetailsVisible => details != null && details.gameObject.activeInHierarchy;
    public void ToggleDetails() { detailsOpen = !detailsOpen; if (details != null) details.gameObject.SetActive(detailsOpen); }
    public int VisibleCount => canvasObject != null && canvasObject.activeSelf ? active.Count : 0;
    public IReadOnlyList<ActiveResonance> PreviewResonances => active;

    void EnsureUI()
    {
        if (canvasObject != null) return;
        ghostRenderers = GetComponentsInChildren<Renderer>();
        canvasObject = new GameObject("Placement resonance badges", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Placement details", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        details = panel.AddComponent<ComicPopupView>();
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = Vector2.zero; panelRect.pivot = Vector2.one;
        var container = new GameObject("Resonances", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        row = container.GetComponent<RectTransform>(); row.SetParent(canvas.transform, false);
        row.anchorMin = row.anchorMax = Vector2.zero; row.pivot = new Vector2(.5f, 0);
        var layout = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        // Bounded and reused for the entire placement, never rebuilt when the mouse moves.
        for (int i = 0; i < 10; i++)
        {
            var icon = new GameObject("Resonance " + i, typeof(RectTransform), typeof(ResonanceBadgeGraphic));
            var rect = icon.GetComponent<RectTransform>(); rect.SetParent(row, false); rect.sizeDelta = new Vector2(52, 52);
            var badge = icon.GetComponent<ResonanceBadgeGraphic>(); badge.raycastTarget = false;
            badges.Add(badge); icon.SetActive(false);
        }
    }

    public void Show(IReadOnlyList<GridObject> footprint, ResonanceRulesSO rules, bool valid)
    {
        if (!valid || footprint == null) { Hide(); return; }
        EnsureUI(); counts.Clear();
        foreach (var grid in footprint)
        {
            var tile = grid?.GetGroundCellCached()?.CurrentModifier;
            if (tile == null) continue;
            counts.TryGetValue(tile.modifierType, out int number); counts[tile.modifierType] = number + 1;
        }
        bool changed = !initialized || previousRules != rules;
        for (int i = 0; i < previousCounts.Length; i++)
        {
            counts.TryGetValue((TileModifierType)i, out int count);
            changed |= previousCounts[i] != count; previousCounts[i] = count;
        }
        if (changed)
        {
            initialized = true; previousRules = rules;
            ResonanceManager.Evaluate(rules, counts, modifiers, active);
            for (int i = 0; i < badges.Count; i++)
            {
                badges[i].gameObject.SetActive(i < active.Count);
                if (i < active.Count) badges[i].SetRecipe(ResonanceManager.Identity(active[i]));
            }
            row.sizeDelta = new Vector2(Mathf.Max(0, active.Count * 58 - 6), 52);
        }
        // The actual roll matters too: moving between equal family counts can change the bonuses.
        description.Clear();
        foreach (var grid in footprint)
        {
            var cell = grid?.GetGroundCellCached();
            description.Append(cell?.GetInstanceID() ?? 0).Append(':');
            description.Append(cell?.CurrentModifier != null ? cell.CurrentModifier.modifierName + " · " + cell.CurrentModifier.rarity : "Boş tile");
            if (cell != null) description.Append(TileBuffText.Modifiers(cell.RolledModifiers));
            description.Append('|');
        }
        string signature = description.ToString();
        if (changed || previousDetails != signature)
        {
            previousDetails = signature;
            details.Begin("BU KONUMDA AÇILACAK", footprint.Count + " tile · " + active.Count + " rezonans", footerHint: "ALT: aç / kapat");
            if (active.Count == 0) details.Add("Bu konumda rezonans oluşmuyor.");
            foreach (var resonance in active)
                details.Add("<b>" + resonance.resonanceName + "</b>\n" + TileBuffText.Resonance(resonance), ResonanceManager.Identity(resonance));
            details.Add("<b>SAKSININ ALTINDAKİ TILE’LAR</b>");
            int index = 0;
            foreach (var grid in footprint)
            {
                var cell = grid?.GetGroundCellCached(); var tile = cell?.CurrentModifier;
                details.Add(++index + ". " + (tile == null ? "Boş tile" : "<b>" + tile.modifierName + "</b> · " + tile.rarity + "\n" + TileBuffText.Modifiers(cell.RolledModifiers)));
            }
            details.End();
        }
        canvasObject.SetActive(true);
        details.gameObject.SetActive(detailsOpen);
        Position();
    }

    void LateUpdate()
    {
        if (canvasObject == null || !canvasObject.activeSelf) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameStates.Placing) { Hide(); return; }
        Position();
    }

    void Update()
    {
        if (canvasObject != null && canvasObject.activeSelf && (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))) ToggleDetails();
    }

    void Position()
    {
        var camera = Camera.main;
        if (camera == null) { Hide(); return; }
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var renderer in ghostRenderers) if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
        Vector3 screen = camera.WorldToScreenPoint(bounds.center + Vector3.up * (bounds.extents.y + .15f));
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) { Hide(); return; }
        float scale = Mathf.Max(.01f, canvas.scaleFactor);
        details.Fit();
        var safe = Screen.safeArea;
        var panelRect = (RectTransform)details.transform;
        // Move the explanation away from a ghost occupying the right edge.
        bool left = screen.x > safe.xMax - (panelRect.rect.width * scale + 90);
        panelRect.pivot = new Vector2(left ? 0 : 1, 1);
        panelRect.anchoredPosition = new Vector2((left ? safe.xMin + 20 : safe.xMax - 20) / scale, (safe.yMax - 20) / scale);
        float half = row.sizeDelta.x * .5f;
        row.anchoredPosition = new Vector2(Mathf.Clamp(screen.x / scale, half + 12, Screen.width / scale - half - 12),
            Mathf.Clamp(screen.y / scale + 12, 12, Screen.height / scale - 64));
    }

    public void Hide() { if (canvasObject != null) canvasObject.SetActive(false); }
    void OnDisable() => Hide();
    void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
