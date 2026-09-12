using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeUI : MonoBehaviour, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IScrollHandler
{
    [SerializeField] private List<SkillNodeUI> nodeUIs;

    [Header("Tree Navigation")]
    [SerializeField, Range(0.1f, 1f)] private float minZoom = 0.5f;
    [SerializeField, Range(1f, 5f)] private float maxZoom = 2f;
    [SerializeField, Range(0.01f, 0.5f)] private float zoomSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float viewportInset = 24f;
    [SerializeField, Min(0f)] private float boundsPadding = 250f;
    [SerializeField] private Vector2 minimumMapSize = new Vector2(2800f, 1800f);

    [Header("Tree Connections")]
    [SerializeField] private bool showConnections = true;
    [SerializeField, Min(1f)] private float connectionWidth = 3f;
    [SerializeField, Min(2f)] private float arrowSize = 12f;
    [SerializeField, Min(0f)] private float connectionGap = 8f;
    [SerializeField] private Color availableConnectionColor = Color.white;
    [SerializeField] private Color openedConnectionColor = new Color(1f, 1f, 1f, 0.55f);

    private sealed class Connection
    {
        public SkillNodeUI a, b;
        public RectTransform root;
        public Image shaft, wingA, wingB;
    }

    private readonly List<Connection> connections = new List<Connection>();
    private RectTransform connectionLayer;
    private Image originMarker;

    private RectTransform viewport;
    private RectTransform content;
    private Rect mapBounds;
    private Vector2 lastViewportSize;
    private Vector2 lastDragPosition;
    private float zoom = 1f;
    private bool dragging;
    private bool navigationReady;
    private SkillTreeManager treeManager;
    private ResourceManager resources;

    private void Awake()
    {
        CreateNavigation();
    }

    private void OnEnable()
    {
        dragging = false;
        TrySubscribe();
    }

    private void Start() => TrySubscribe();

    private void TrySubscribe()
    {
        if (treeManager == null && SkillTreeManager.Instance != null)
        {
            treeManager = SkillTreeManager.Instance;
            treeManager.OnTreeChanged += RefreshAll;
        }
        if (resources == null && ResourceManager.Instance != null)
        {
            resources = ResourceManager.Instance;
            resources.OnResourceAmountChanged += HandleResourceChanged;
        }
        if (treeManager != null && resources != null) RefreshAll();
    }

    private void OnDisable()
    {
        dragging = false;
        if (treeManager != null) treeManager.OnTreeChanged -= RefreshAll;
        if (resources != null) resources.OnResourceAmountChanged -= HandleResourceChanged;
        treeManager = null;
        resources = null;
    }

    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();

    private void RefreshAll()
    {
        foreach (SkillNodeUI nodeUI in nodeUIs)
            if (nodeUI != null) nodeUI.Refresh();
        RefreshConnections();
    }

    private void CreateNavigation()
    {
        if (viewport != null || !(transform is RectTransform)) return;

        // Existing scene references stay intact. Only nodes move into the map;
        // the panel background and other shop controls remain stationary.
        var viewportObject = new GameObject("Skill Tree Viewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewportObject.layer = gameObject.layer;
        viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.one * viewportInset;
        viewport.offsetMax = Vector2.one * -viewportInset;
        var inputSurface = viewportObject.GetComponent<Image>();
        inputSurface.color = Color.clear;
        inputSurface.raycastTarget = true;

        var contentObject = new GameObject("Skill Tree Content", typeof(RectTransform));
        contentObject.layer = gameObject.layer;
        content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;

        foreach (SkillNodeUI nodeUI in nodeUIs)
        {
            if (nodeUI == null) continue;
            var rect = nodeUI.GetComponent<RectTransform>();
            Vector2 size = rect.rect.size;
            rect.SetParent(content, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = nodeUI.LayoutPosition;
        }
        CreateConnections();
    }

    private void CreateConnections()
    {
        var layer = new GameObject("Skill Connections", typeof(RectTransform));
        layer.layer = gameObject.layer;
        connectionLayer = layer.GetComponent<RectTransform>();
        connectionLayer.SetParent(content, false);
        connectionLayer.anchorMin = connectionLayer.anchorMax = Vector2.one * 0.5f;
        connectionLayer.sizeDelta = Vector2.zero;
        connectionLayer.SetAsFirstSibling();
        originMarker = CreateLine("Starting Point", connectionLayer);
        originMarker.rectTransform.sizeDelta = Vector2.one * 10f;
        originMarker.gameObject.SetActive(false);

        var validNodes = new List<SkillNodeUI>();
        var seen = new HashSet<SkillNodeSO>();
        foreach (var ui in nodeUIs)
            if (ui != null && ui.Node != null && seen.Add(ui.Node)) validNodes.Add(ui);

        for (int i = 0; i < validNodes.Count; i++)
        {
            for (int j = i + 1; j < validNodes.Count; j++)
                if (SkillTreeManager.AreNeighbors(validNodes[i].Node.gridPosition,
                    validNodes[j].Node.gridPosition)) AddConnection(validNodes[i], validNodes[j]);

            // ResetTree unlocks (0,0) even when no skill is displayed there.
            if (SkillTreeManager.AreNeighbors(Vector2Int.zero, validNodes[i].Node.gridPosition))
                AddConnection(null, validNodes[i]);
        }
    }

    private Image CreateLine(string label, Transform parent)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = gameObject.layer;
        var img = obj.GetComponent<Image>();
        img.raycastTarget = false;
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.one * 0.5f;
        return img;
    }

    private void AddConnection(SkillNodeUI a, SkillNodeUI b)
    {
        var obj = new GameObject($"Link { (a == null ? "Start" : a.name) } -> {b.name}",
            typeof(RectTransform));
        obj.layer = gameObject.layer;
        var root = obj.GetComponent<RectTransform>();
        root.SetParent(connectionLayer, false);
        root.anchorMin = root.anchorMax = Vector2.one * 0.5f;
        root.sizeDelta = Vector2.zero;
        connections.Add(new Connection { a = a, b = b, root = root,
            shaft = CreateLine("Line", root), wingA = CreateLine("Arrow A", root),
            wingB = CreateLine("Arrow B", root) });
        obj.SetActive(false);
    }

    private void RefreshConnections()
    {
        if (connectionLayer == null || treeManager == null) return;
        bool hasVisibleOrigin = false;
        foreach (var ui in nodeUIs)
            if (ui != null && ui.Node != null && ui.Node.gridPosition == Vector2Int.zero &&
                ui.gameObject.activeSelf) hasVisibleOrigin = true;

        bool showOrigin = false;
        foreach (var link in connections)
        {
            bool virtualOrigin = link.a == null;
            bool visible = showConnections && link.b != null && link.b.gameObject.activeSelf &&
                (virtualOrigin ? !hasVisibleOrigin : link.a.gameObject.activeSelf);
            Vector2Int aGrid = virtualOrigin ? Vector2Int.zero : link.a.Node.gridPosition;
            bool aOpen = treeManager.IsPositionUnlocked(aGrid);
            bool bOpen = treeManager.IsPositionUnlocked(link.b.Node.gridPosition);
            visible &= aOpen || bOpen;
            link.root.gameObject.SetActive(visible);
            if (!visible) continue;

            // Point from the unlocked end to the unbought end; once both are
            // unlocked a plain line records the completed connection.
            SkillNodeUI from = !aOpen && bOpen ? link.b : link.a;
            SkillNodeUI to = !aOpen && bOpen ? link.a : link.b;
            Vector2 start = from == null ? Vector2.zero : from.LayoutPosition;
            Vector2 end = to == null ? Vector2.zero : to.LayoutPosition;
            Vector2 direction = (end - start).normalized;
            start += direction * (NodeEdgeDistance(from, direction) + connectionGap);
            end -= direction * (NodeEdgeDistance(to, -direction) + connectionGap);
            if (Vector2.Dot(end - start, direction) <= 1f)
            {
                link.root.gameObject.SetActive(false);
                continue;
            }
            bool completed = aOpen && bOpen;
            Color tint = completed ? openedConnectionColor : availableConnectionColor;
            SetLine(link.shaft, start, end, tint);
            link.wingA.gameObject.SetActive(!completed);
            link.wingB.gameObject.SetActive(!completed);
            if (!completed)
            {
                float size = Mathf.Min(arrowSize, Vector2.Distance(start, end) * 0.4f);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                SetLine(link.wingA, end, end - direction * size + normal * size * 0.6f, tint);
                SetLine(link.wingB, end, end - direction * size - normal * size * 0.6f, tint);
            }
            showOrigin |= virtualOrigin;
        }
        originMarker.gameObject.SetActive(showOrigin);
        originMarker.color = availableConnectionColor;
    }

    private static float NodeEdgeDistance(SkillNodeUI ui, Vector2 direction)
    {
        if (ui == null) return 5f;
        Rect rect = ((RectTransform)ui.transform).rect;
        float x = Mathf.Abs(direction.x) < 0.0001f ? float.PositiveInfinity :
            (direction.x > 0 ? rect.xMax : -rect.xMin) / Mathf.Abs(direction.x);
        float y = Mathf.Abs(direction.y) < 0.0001f ? float.PositiveInfinity :
            (direction.y > 0 ? rect.yMax : -rect.yMin) / Mathf.Abs(direction.y);
        return Mathf.Min(x, y);
    }

    private void SetLine(Image line, Vector2 start, Vector2 end, Color tint)
    {
        Vector2 delta = end - start;
        line.rectTransform.anchoredPosition = (start + end) * 0.5f;
        line.rectTransform.sizeDelta = new Vector2(delta.magnitude, connectionWidth);
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.color = tint;
    }

    private void LateUpdate()
    {
        if (viewport == null) return;
        // Canvas scaling and window resizing must not leave the map outside bounds.
        if (!navigationReady || viewport.rect.size != lastViewportSize)
        {
            if (viewport.rect.width <= 0f || viewport.rect.height <= 0f) return;
            RebuildBounds();
            if (!navigationReady)
            {
                zoom = Mathf.Clamp(1f, minZoom, maxZoom);
                content.localScale = Vector3.one * zoom;
                content.anchoredPosition = -mapBounds.center * zoom;
                navigationReady = true;
            }
            ClampPosition();
            lastViewportSize = viewport.rect.size;
        }
    }

    private void RebuildBounds()
    {
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        // Include hidden nodes so buying a node never shifts the navigation limits.
        foreach (SkillNodeUI nodeUI in nodeUIs)
        {
            if (nodeUI == null) continue;
            var rect = nodeUI.GetComponent<RectTransform>();
            min = Vector2.Min(min, nodeUI.LayoutPosition + rect.rect.min);
            max = Vector2.Max(max, nodeUI.LayoutPosition + rect.rect.max);
        }
        Vector2 center = (min + max) * 0.5f;
        Vector2 size = Vector2.Max(max - min + Vector2.one * boundsPadding * 2f,
            Vector2.Max(minimumMapSize, viewport.rect.size * 1.25f));
        mapBounds = new Rect(center - size * 0.5f, size);
        content.sizeDelta = size;
    }

    private void ClampPosition()
    {
        Vector2 position = content.anchoredPosition;
        position.x = ClampAxis(position.x, viewport.rect.xMin, viewport.rect.xMax,
            mapBounds.xMin * zoom, mapBounds.xMax * zoom);
        position.y = ClampAxis(position.y, viewport.rect.yMin, viewport.rect.yMax,
            mapBounds.yMin * zoom, mapBounds.yMax * zoom);
        content.anchoredPosition = position;
    }

    private static float ClampAxis(float position, float viewMin, float viewMax,
        float mapMin, float mapMax)
    {
        // A map smaller than the viewport stays centered instead of drifting away.
        if (mapMax - mapMin <= viewMax - viewMin)
            return (viewMin + viewMax - mapMin - mapMax) * 0.5f;
        return Mathf.Clamp(position, viewMax - mapMax, viewMin - mapMin);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = false;
        if (!navigationReady || eventData.button != PointerEventData.InputButton.Left) return;
        GameObject hit = eventData.pointerPressRaycast.gameObject;
        if (hit == null || !hit.transform.IsChildOf(viewport)) return;
        // Drag only empty space; pressing a skill node must not pan the tree.
        if (hit.GetComponentInParent<SkillNodeUI>() != null ||
            hit.GetComponentInParent<Selectable>() != null) return;
        dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
            eventData.position, eventData.pressEventCamera, out lastDragPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
            eventData.position, eventData.pressEventCamera, out Vector2 current)) return;
        content.anchoredPosition += current - lastDragPosition;
        lastDragPosition = current;
        ClampPosition();
    }

    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) dragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!navigationReady || !RectTransformUtility.RectangleContainsScreenPoint(
            viewport, eventData.position, eventData.enterEventCamera)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
            eventData.position, eventData.enterEventCamera, out Vector2 pointer)) return;
        float next = Mathf.Clamp(zoom * Mathf.Exp(eventData.scrollDelta.y * zoomSensitivity),
            minZoom, maxZoom);
        // Keep the point under the cursor stationary unless a boundary is reached.
        content.anchoredPosition = pointer - (pointer - content.anchoredPosition) * (next / zoom);
        zoom = next;
        content.localScale = Vector3.one * zoom;
        ClampPosition();
        eventData.Use();
    }
}
