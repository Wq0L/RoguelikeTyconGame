using System.Collections.Generic;
using DG.Tweening;
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

    [Header("Open Ripple")]
    [SerializeField, Min(0.01f)] private float rippleDuration = 0.3f;
    [Tooltip("Merkezden en uzak node'un gecikmesi (sn).")]
    [SerializeField, Min(0f)] private float rippleSpread = 0.35f;
    [SerializeField, Min(1f)] private float rippleRange = 1400f;

    // Yerleşim (11 Kasım 2026, kullanıcı: "skiller çok uzak; iç içe girmeden yakınlaştır"). Yetenek verisindeki konumlar
    // (gridPosition, komşuluk kuralları) DEĞİŞMEZ; yalnız ekrandaki yer: aralık küçülür, düğüm büyür, dallar arasındaki boş
    // koridorlar (3 ve üstü boş sıra / sütun) kısalır. Sıra ve hizalar korunur, iki düğüm asla aynı hücreye düşmez.
    [Header("Layout")]
    [SerializeField, Min(40f)] private float nodeSpacing = 112f;
    [SerializeField, Min(32f)] private float nodeSize = 84f;
    [SerializeField, Min(1)] private int corridorCells = 2;

    [Header("Feel")]
    [SerializeField] private Color hoverConnectionColor = new Color(1f, 0.82f, 0.36f, 1f);
    [SerializeField, Min(0.01f)] private float connectionGrowSeconds = 0.28f;
    [SerializeField, Min(1f)] private float zoomSharpness = 16f;
    [SerializeField, Min(0f)] private float dragFriction = 7f;

    private sealed class Connection
    {
        public SkillNodeUI a, b;
        public RectTransform root;
        public Image shaft, wingA, wingB;
        // Son hesaplanan uçlar ve renk; grow < 1 iken çizgi açık uçtan hedefe doğru uzar.
        public Vector2 start, end;
        public Color tint;
        public bool completed;
        public float grow = 1f;
    }

    private readonly List<int> layoutX = new List<int>(), layoutY = new List<int>();
    private SkillNodeUI hoveredNode;
    private float zoomTarget = 1f;
    private Vector2 zoomPivot, dragVelocity;
    private bool zooming, opening;
    public float NodeSize => nodeSize;

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

    // Arka plan paralaksı için (SkillTreeSpaceBackground).
    public Vector2 PanPosition => content != null ? content.anchoredPosition : Vector2.zero;
    // Düğüm aralığı (panonun ızgarası düğümlere oturur); düğüm yoksa prefab değeri.
    public float NodeSpacing => nodeSpacing;
    public float Zoom => zoom;

    private void Awake()
    {
        ApplyTreeSet();
        ApplyLayout();
        CreateNavigation();
    }

    // Her düğüme ekrandaki hücresini ve boyunu verir. Koordinatlar eksen eksen sıkıştırılır: 1–2'lik adımlar aynen kalır, daha
    // büyük boşluk corridorCells hücreye iner. Başlangıç noktası (0,0) yerinde kalır.
    private void ApplyLayout()
    {
        layoutX.Clear(); layoutY.Clear();
        layoutX.Add(0); layoutY.Add(0);
        foreach (SkillNodeUI nodeUI in nodeUIs)
        {
            if (nodeUI == null || nodeUI.Node == null) continue;
            Vector2Int p = nodeUI.Node.gridPosition;
            if (!layoutX.Contains(p.x)) layoutX.Add(p.x);
            if (!layoutY.Contains(p.y)) layoutY.Add(p.y);
        }
        layoutX.Sort(); layoutY.Sort();
        foreach (SkillNodeUI nodeUI in nodeUIs)
        {
            if (nodeUI == null || nodeUI.Node == null) continue;
            Vector2Int p = nodeUI.Node.gridPosition;
            nodeUI.SetLayout(new Vector2(Compact(layoutX, p.x), Compact(layoutY, p.y)) * nodeSpacing, nodeSize);
        }
    }

    private int Compact(List<int> sorted, int value)
    {
        int zero = sorted.IndexOf(0), index = sorted.IndexOf(value), cell = 0;
        for (int i = zero; i < index; i++) cell += Step(sorted[i + 1] - sorted[i]);
        for (int i = zero; i > index; i--) cell -= Step(sorted[i] - sorted[i - 1]);
        return cell;
    }

    private int Step(int gap) => gap <= 2 ? gap : corridorCells;

    // Üstüne gelinen düğümün bağlantıları parlar (SkillNodeUI bildirir).
    public void SetHovered(SkillNodeUI node, bool hovered)
    {
        SkillNodeUI next = hovered ? node : hoveredNode == node ? null : hoveredNode;
        if (next == hoveredNode) return;
        hoveredNode = next;
        RefreshConnections();
    }

    // Run profilinin ağaç seti (Bölüm 3.5): her arayüz yuvası setteki karşılığını gösterir, karşılığı olmayan yuva kapanır.
    // Set yoksa (eski profiller) yuvalar sahnedeki düğümleriyle kalır. Bağlantı çizgileri bundan sonra kurulur.
    private void ApplyTreeSet()
    {
        RunBalanceSO balance = RunBalanceSO.Active;
        if (balance == null || balance.skillTree == null) return;
        foreach (SkillNodeUI nodeUI in nodeUIs)
            if (nodeUI != null) nodeUI.Bind(balance.skillTree.NodeFor(nodeUI.Node));
    }

    private void OnEnable()
    {
        dragging = false;
        opening = true;   // açılıştaki ilk yenileme "yeni yetenek açıldı" sesini çalmaz
        TrySubscribe();
        opening = false;
        PlayOpenRipple();
    }

    // Panel açılınca node'lar merkezden dışa doğru dalga halinde belirir.
    // SkillNodeUI.OnDisable tween'leri öldürüp ölçeği sıfırladığı için yarıda kapanması güvenli.
    private void PlayOpenRipple()
    {
        foreach (SkillNodeUI nodeUI in nodeUIs)
        {
            if (nodeUI == null || !nodeUI.gameObject.activeInHierarchy) continue;
            Transform node = nodeUI.transform;
            float delay = Mathf.Clamp01(nodeUI.LayoutPosition.magnitude / rippleRange) * rippleSpread;
            node.DOKill();
            node.localScale = Vector3.one * 0.3f;
            node.DOScale(1f, rippleDuration).SetDelay(delay).SetEase(Ease.OutBack, 1.8f).SetUpdate(true);
        }
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
        zooming = false;
        dragVelocity = Vector2.zero;
        hoveredNode = null;
        if (treeManager != null) treeManager.OnTreeChanged -= RefreshAll;
        if (resources != null) resources.OnResourceAmountChanged -= HandleResourceChanged;
        treeManager = null;
        resources = null;
    }

    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();

    private void RefreshAll()
    {
        int revealed = SkillNodeUI.RevealCount;
        foreach (SkillNodeUI nodeUI in nodeUIs)
            if (nodeUI != null) nodeUI.Refresh();
        RefreshConnections();
        // Bir alım yeni yetenek açtıysa tek kısa ses (kaç düğüm açılırsa açılsın).
        if (!opening && isActiveAndEnabled && SkillNodeUI.RevealCount != revealed) FeelAudio.Play(FeelSound.Shimmer, 0.3f, 1.15f);
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
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            nodeUI.ApplyGridLayout();
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
        if (kitLine != null && ComicKit.Get("pip_gold") != null)
        {
            originMarker.sprite = ComicKit.Get("pip_gold"); originMarker.type = Image.Type.Simple;
            originMarker.rectTransform.sizeDelta = Vector2.one * 18f;
        }
        originMarker.gameObject.SetActive(false);

        var validNodes = new List<SkillNodeUI>();
        var seen = new HashSet<SkillNodeSO>();
        foreach (var ui in nodeUIs)
            if (ui != null && ui.Node != null && seen.Add(ui.Node)) validNodes.Add(ui);

        for (int i = 0; i < validNodes.Count; i++)
        {
            var node = validNodes[i].Node;
            if (node.explicitPrerequisites)
            {
                if (node.prerequisites.Count == 0) AddConnection(null, validNodes[i]);
                foreach (var requirement in node.prerequisites)
                {
                    var parent = validNodes.Find(ui => ui.Node == requirement.node);
                    if (parent != null) AddConnection(parent, validNodes[i]);
                }
                continue;
            }
            for (int j = i + 1; j < validNodes.Count; j++)
                if (SkillTreeManager.AreNeighbors(validNodes[i].Node.gridPosition,
                    validNodes[j].Node.gridPosition)) AddConnection(validNodes[i], validNodes[j]);

            // ResetTree unlocks (0,0) even when no skill is displayed there.
            if (SkillTreeManager.AreNeighbors(Vector2Int.zero, validNodes[i].Node.gridPosition))
                AddConnection(null, validNodes[i]);
        }
    }

    // Comic kağıt kiti (6 Kasım 2026): bağlantılar kitin krem mürekkepli çizgisi (Art'taki divider değil, Resources'taki line_cream);
    // 9-dilim, yatayda uzar. Kit yoksa eski düz beyaz çizgi.
    private Sprite kitLine;
    private float LineWidth => kitLine != null ? KitLineWidth : connectionWidth;
    public const float KitLineWidth = 9f;
    public bool UsesKitLines => kitLine != null;

    private Image CreateLine(string label, Transform parent)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = gameObject.layer;
        var img = obj.GetComponent<Image>();
        img.raycastTarget = false;
        if (kitLine == null) kitLine = ComicKit.Ready ? ComicKit.Get("line_cream") : null;
        if (kitLine != null) { img.sprite = kitLine; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = kitLine.rect.height / KitLineWidth; }
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
            bool aOpen = virtualOrigin ? treeManager.IsPositionUnlocked(Vector2Int.zero) : IsOpenEnd(link.a.Node);
            bool bOpen = IsOpenEnd(link.b.Node);
            visible &= aOpen || bOpen;
            bool wasVisible = link.root.gameObject.activeSelf;
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
            bool lit = hoveredNode != null && (link.a == hoveredNode || link.b == hoveredNode);
            link.start = start; link.end = end; link.completed = completed;
            link.tint = lit ? hoverConnectionColor : completed ? openedConnectionColor : availableConnectionColor;
            // Yeni görünen bağlantı açık uçtan hedefe doğru uzar; ok ucuyla birlikte ilerler (ok görünürlüğü değişmez).
            if (!wasVisible && isActiveAndEnabled) link.grow = 0f;
            link.wingA.gameObject.SetActive(!completed);
            link.wingB.gameObject.SetActive(!completed);
            ApplyLink(link);
            showOrigin |= virtualOrigin;
        }
        originMarker.gameObject.SetActive(showOrigin);
        originMarker.color = availableConnectionColor;
    }

    // Çizginin açık ucu: satın alınmış düğüm ya da profilin başlangıçtan açık verdiği (satılmayan) düğüm. Ok açık uçtan
    // henüz alınmamış uca bakar; iki uç da açıksa düz çizgi kalır.
    private bool IsOpenEnd(SkillNodeSO node) =>
        treeManager.IsPositionUnlocked(node.gridPosition) || treeManager.IsOpenFromStart(node);

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

    private void ApplyLink(Connection link)
    {
        float k = link.grow >= 1f ? 1f : 1f - Mathf.Pow(1f - Mathf.Clamp01(link.grow), 3f);
        Vector2 tip = Vector2.LerpUnclamped(link.start, link.end, k);
        SetLine(link.shaft, link.start, tip, link.tint);
        if (link.completed) return;
        Vector2 direction = (link.end - link.start).normalized;
        float size = Mathf.Min(arrowSize, Vector2.Distance(link.start, link.end) * 0.4f);
        Vector2 normal = new Vector2(-direction.y, direction.x);
        SetLine(link.wingA, tip, tip - direction * size + normal * size * 0.6f, link.tint);
        SetLine(link.wingB, tip, tip - direction * size - normal * size * 0.6f, link.tint);
    }

    // His (11 Kasım 2026): alınabilir düğümlerin nefesi, uzayan bağlantılar, yumuşak zoom, sürükleme ataleti. Unscaled time
    // (dükkânda timeScale 0). Bellek ayırmaz; boşta yalnız kısa düğüm ve bağlantı döngüleri döner.
    private void Animate()
    {
        float dt = Time.unscaledDeltaTime, time = Time.unscaledTime;
        for (int i = 0; i < nodeUIs.Count; i++)
        {
            SkillNodeUI nodeUI = nodeUIs[i];
            if (nodeUI != null && nodeUI.isActiveAndEnabled) nodeUI.Breathe(time);
        }
        for (int i = 0; i < connections.Count; i++)
        {
            Connection link = connections[i];
            if (link.grow >= 1f || !link.root.gameObject.activeSelf) continue;
            link.grow = Mathf.Min(1f, link.grow + dt / connectionGrowSeconds);
            ApplyLink(link);
        }
        if (zooming)
        {
            float next = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-dt * zoomSharpness));
            if (Mathf.Abs(next - zoomTarget) < 0.002f) { next = zoomTarget; zooming = false; }
            content.anchoredPosition = zoomPivot - (zoomPivot - content.anchoredPosition) * (next / zoom);
            zoom = next;
            content.localScale = Vector3.one * zoom;
            ClampPosition();
        }
        if (dragging) dragVelocity *= Mathf.Exp(-dt * 18f);   // imleç durduysa bırakınca savrulmaz
        else if (dragVelocity.sqrMagnitude > 1f)
        {
            content.anchoredPosition += dragVelocity * dt;
            dragVelocity *= Mathf.Exp(-dt * dragFriction);
            if (dragVelocity.sqrMagnitude < 25f) dragVelocity = Vector2.zero;
            ClampPosition();
        }
    }

    private void SetLine(Image line, Vector2 start, Vector2 end, Color tint)
    {
        Vector2 delta = end - start;
        line.rectTransform.anchoredPosition = (start + end) * 0.5f;
        line.rectTransform.sizeDelta = new Vector2(delta.magnitude, LineWidth);
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
                content.anchoredPosition = Vector2.zero;
                navigationReady = true;
            }
            ClampPosition();
            lastViewportSize = viewport.rect.size;
        }
        if (navigationReady) Animate();
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
        dragVelocity = Vector2.zero;
        zooming = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
            eventData.position, eventData.pressEventCamera, out Vector2 current)) return;
        Vector2 delta = current - lastDragPosition;
        content.anchoredPosition += delta;
        lastDragPosition = current;
        // Bırakınca ağaç biraz daha kayar ve yavaşlar (atalet); hız son hareketlerden.
        dragVelocity = Vector2.Lerp(dragVelocity, delta / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.5f);
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
        // Yumuşak zoom: hedef hemen belirlenir, ağaç oraya kısa sürede akar (Animate); imlecin altındaki nokta sabit kalır.
        float from = zooming ? zoomTarget : zoom;
        zoomTarget = Mathf.Clamp(from * Mathf.Exp(eventData.scrollDelta.y * zoomSensitivity), minZoom, maxZoom);
        zoomPivot = pointer;
        zooming = !Mathf.Approximately(zoomTarget, zoom);
        dragVelocity = Vector2.zero;
        eventData.Use();
    }
}
