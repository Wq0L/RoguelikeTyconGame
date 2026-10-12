using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Boss ödülünün yerleşim adımı (Bölüm 3.8; RoundChoice durumu). Yerleşim isteyen ödül (Elektrik Anteni, İletken Hat) kartından
// "AL" denince ödül ekranı kapanır, tarla görünür ve geçerli kenar konumları işaretlenir. UIManager kurar, açar ve kapatır.
// - Yalnız gösterir ve isteği iletir: hangi konumların geçerli olduğu etkinin verisindedir (RunEffectSO.CollectPlacements),
//   ödülün tek işlemle uygulanması BossRewardManager.Choose(ödül, konum)'dadır. Geçersiz konum işaretlenmez ve seçilemez.
// - Konumun üstüne gelince etkileyeceği hücreler gösterilir (anten: baktığı yöndeki hücreler; hat: iki anten arasındaki hücreler).
// - Vazgeçme (Esc ya da düğme): hiçbir şey uygulanmadan aynı teklife dönülür. Panel kapanıp açılırsa (durum değişimi) bekleyen
//   yerleşim BossRewardManager'da durur; run biterse ya da menüye dönülürse oradaki tek temizlik yolu onu da kapatır.
// - İmleç bir arayüz öğesinin üstündeyken tarlaya tıklanmış sayılmaz.
// İşaretler yer tutucudur (PlaceholderShapes) ya da katalogdaki prefab'lardır (LateGameArtSO: PlacementMarker, PlacementCell).
public sealed class RewardPlacementUI : MonoBehaviour
{
    private static readonly Color MarkerColor = new(0.45f, 0.9f, 1f), HoverColor = new(1f, 0.86f, 0.32f), CellColor = new(1f, 0.86f, 0.32f);
    private readonly List<int> slots = new();
    private readonly List<Vector3> positions = new();
    private readonly List<GridPosition> cells = new();
    private readonly List<GameObject> markers = new(), cellMarkers = new();
    private GameObject hoverMarker;
    private GameObject previewAntenna;
    private readonly List<GameObject> previewLinks=new(), reachMarkers=new();
    private readonly List<GridPosition> shortCells=new();
    private Transform world;
    private TextMeshProUGUI prompt;
    private Button cancel;
    private BossRewardSO shown;
    private int hovered = RunEffectSO.NoPlacement, openedFrame;

    // Test ve doğrulama: geçerli konumlar ve üstünde durulan konum.
    public IReadOnlyList<int> Slots => slots;
    public int Hovered => hovered;
    public int PreviewCells => cells.Count;
    public string PromptText => prompt != null ? prompt.text : null;

    public static RewardPlacementUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<RewardPlacementUI>(true);
        if (existing != null) return existing;
        var go = new GameObject("Reward Placement", typeof(RectTransform));
        go.layer = canvasRoot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rect);
        var panel = go.AddComponent<RewardPlacementUI>();
        panel.Build(rect);
        go.SetActive(false);
        return panel;
    }

    private void Build(RectTransform root)
    {
        ComicUITheme theme = FeelOverlay.Theme;
        // Karartma yok: tarla görünmeli. Yalnız üstte bir talimat plakası ve vazgeçme düğmesi raycast alır.
        var plate = new GameObject("Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
        plate.gameObject.layer = root.gameObject.layer;
        plate.SetParent(root, false);
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(.5f, 1f);
        plate.anchoredPosition = new Vector2(0f, -36f);
        plate.sizeDelta = new Vector2(980f, 118f);
        AddPlate(plate, "Ink", new Color32(54, 39, 54, 255), 0f);
        AddPlate(plate, "Face", new Color32(255, 244, 214, 255), 3f);
        prompt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        prompt.gameObject.layer = root.gameObject.layer;
        prompt.rectTransform.SetParent(plate, false);
        FeelOverlay.Stretch(prompt.rectTransform);
        prompt.rectTransform.offsetMin = new Vector2(20f, 8f);
        prompt.rectTransform.offsetMax = new Vector2(-20f, -8f);
        theme?.StylePopupText(prompt);
        prompt.color = new Color32(54, 39, 54, 255);
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.enableAutoSizing = true;
        prompt.fontSizeMin = 16f;
        prompt.fontSizeMax = 32f;
        prompt.raycastTarget = false;
        if (theme != null) cancel = theme.CreateButton(root, "Cancel", "VAZGEÇ", "green", new Vector2(240f, 72f));
        else cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        var cancelRect = (RectTransform)cancel.transform;
        cancelRect.SetParent(root, false);
        cancelRect.anchorMin = cancelRect.anchorMax = cancelRect.pivot = new Vector2(.5f, 0f);
        cancelRect.anchoredPosition = new Vector2(0f, 48f);
        cancelRect.sizeDelta = new Vector2(240f, 72f);
        cancel.onClick.AddListener(Cancel);
    }

    private static void AddPlate(RectTransform parent, string name, Color color, float inset)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
        var graphic = rect.gameObject.AddComponent<ComicPopupPlate>();
        graphic.color = color;
        graphic.raycastTarget = false;
    }

    private void OnEnable()
    {
        openedFrame = Time.frameCount;
        shown = null;
        Refresh();
    }

    private void OnDisable() => HideMarkers();

    private void OnDestroy()
    {
        if (world != null) Destroy(world.gameObject);
    }

    // Geçerli konumlar etkinin verisinden okunur; işaretler dünyada kurulur.
    private void Refresh()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        RunEffects effects = RunEffects.Instance;
        shown = manager != null ? manager.PlacementReward : null;
        hovered = RunEffectSO.NoPlacement;
        slots.Clear();
        positions.Clear();
        cells.Clear();
        HideMarkers();
        if (shown == null || shown.effect == null || effects == null) return;
        if (prompt != null) prompt.text = $"{shown.effect.PlacementPrompt}\n<size=60%>{shown.displayName} · tıkla: yerleştir · Esc: vazgeç</size>";
        shown.effect.CollectPlacements(effects, slots);
        if (world == null) world = new GameObject("Reward Placement Markers").transform;
        for (int i = 0; i < slots.Count; i++)
        {
            Vector3 position = shown.effect.PlacementPosition(effects, slots[i]);
            positions.Add(position);
            if (i == markers.Count) markers.Add(Marker(LateGameSlot.PlacementMarker, MarkerColor, "Slot " + i));
            markers[i].transform.position = position;
            markers[i].transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            Orient(markers[i],slots[i]);
            markers[i].transform.position=Surface(position,AntennaSlots.Anchor(Bounds(),slots[i]));
            markers[i].SetActive(true);
        }
    }

    private GameObject Marker(LateGameSlot slot, Color color, string title)
    {
        var art = Resources.Load<LateGameArtSO>(LateGameArtSO.ResourcePath);
        GameObject prefab = art != null ? art.Prefab(slot,LateGameVariant.Antenna) : null;
        if (prefab != null) return Instantiate(prefab, world);
        return PlaceholderShapes.Create(slot == LateGameSlot.PlacementCell ? PlaceholderShapes.Shape.Orb : PlaceholderShapes.Shape.Post, color, title, world);
    }

    private void HideMarkers()
    {
        foreach (GameObject marker in markers) if (marker != null) marker.SetActive(false);
        foreach (GameObject marker in cellMarkers) if (marker != null) marker.SetActive(false);
        if (hoverMarker != null) hoverMarker.SetActive(false);
        if(previewAntenna!=null)previewAntenna.SetActive(false);
        foreach(var link in previewLinks)link.SetActive(false);
        foreach(var marker in reachMarkers)marker.SetActive(false);
        cells.Clear();shortCells.Clear();hovered=RunEffectSO.NoPlacement;
    }

    private void Update()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (manager == null || manager.PlacementReward == null) return;   // UIManager paneli kapatır
        if (manager.PlacementReward != shown) Refresh();
        if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        SetHovered(overUi ? RunEffectSO.NoPlacement : Pick());
        // Açıldığı karedeki tıklama "AL" düğmesinin tıklamasıdır: konum seçimi sayılmaz.
        if (Input.GetMouseButtonDown(0) && !overUi && Time.frameCount != openedFrame && hovered != RunEffectSO.NoPlacement) Confirm(hovered);
    }

    // İmlecin zemindeki izdüşümüne en yakın geçerli konum (bir hücre boyundan uzaktaysa yok).
    private int Pick()
    {
        Camera camera = Camera.main;
        if (camera == null || slots.Count == 0) return RunEffectSO.NoPlacement;
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!new Plane(Vector3.up, new Vector3(0f, positions[0].y, 0f)).Raycast(ray, out float enter)) return RunEffectSO.NoPlacement;
        Vector3 point = ray.GetPoint(enter);
        float best = FieldCells.CellSize * FieldCells.CellSize;
        int pick = RunEffectSO.NoPlacement;
        for (int i = 0; i < slots.Count; i++)
        {
            float dx = positions[i].x - point.x, dz = positions[i].z - point.z, distance = dx * dx + dz * dz;
            if (distance >= best) continue;
            best = distance;
            pick = slots[i];
        }
        return pick;
    }

    // Üstünde durulan konum: vurgu ve etkileyeceği hücreler. Test de bu yoldan sorar.
    public void SetHovered(int slot)
    {
        if (slot == hovered) return;
        hovered = slot;
        cells.Clear();
        foreach (GameObject marker in cellMarkers) if (marker != null) marker.SetActive(false);
        foreach(var link in previewLinks)link.SetActive(false);
        foreach(var marker in reachMarkers)marker.SetActive(false);
        if(previewAntenna!=null)previewAntenna.SetActive(false);
        int index = slots.IndexOf(slot);
        if (index < 0 || shown == null || RunEffects.Instance == null)
        {
            hovered = RunEffectSO.NoPlacement;
            if (hoverMarker != null) hoverMarker.SetActive(false);
            return;
        }
        if (hoverMarker == null) hoverMarker = Marker(LateGameSlot.PlacementSelected, HoverColor, "Hovered slot");
        hoverMarker.transform.position = Surface(positions[index],AntennaSlots.Anchor(Bounds(),slot));
        Orient(hoverMarker,slot);
        hoverMarker.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        hoverMarker.SetActive(true);
        if(previewAntenna==null)previewAntenna=Marker(LateGameSlot.Antenna,HoverColor,"Antenna preview");
        previewAntenna.transform.position=hoverMarker.transform.position;
        previewAntenna.transform.localScale=Vector3.one;
        Orient(previewAntenna,slot);previewAntenna.SetActive(true);
        previewAntenna.GetComponent<LateGameContent>()?.Show();
        shown.effect.PlacementCells(RunEffects.Instance, slot, cells);
        GridSystem grid = FieldCells.Grid;
        for (int i = 0; i < cells.Count && grid != null; i++)
        {
            if (i == cellMarkers.Count) cellMarkers.Add(Marker(LateGameSlot.PlacementCell, CellColor, "Cell " + i));
            cellMarkers[i].transform.position = FieldCells.World(grid, cells[i]) + Vector3.up * 1.7f;
            cellMarkers[i].transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            cellMarkers[i].SetActive(true);
        }
        if(shown.effect is ConductiveLineEffectSO && grid!=null)
        {
            var antennas=RunEffects.Instance.Find<AntennaEffect>();
            if(antennas!=null&&antennas.Count>0)
            {
                Vector3 last=Surface(AntennaSlots.World(grid,Bounds(),antennas.Slot(0)),AntennaSlots.Anchor(Bounds(),antennas.Slot(0)))+Vector3.up*1.8f;
                for(int i=0;i<=cells.Count;i++)
                {
                    Vector3 next=i<cells.Count?Surface(FieldCells.World(grid,cells[i]),cells[i])+Vector3.up*1.7f:previewAntenna.transform.position+Vector3.up*1.8f;
                    if(i==previewLinks.Count)previewLinks.Add(Marker(LateGameSlot.LineBolt,CellColor,"Preview link"));
                    var link=previewLinks[i];var d=next-last;link.transform.SetPositionAndRotation(last,Quaternion.LookRotation(d));link.transform.localScale=new Vector3(.35f,.35f,d.magnitude);link.SetActive(true);last=next;
                }
                var antennaData=antennas.Data as AntennaEffectSO;
                AntennaSlots.Reach(Bounds(),slot,antennaData!=null?antennaData.reach:3,shortCells);
                for(int i=0;i<shortCells.Count;i++)
                {if(i==reachMarkers.Count)reachMarkers.Add(Marker(LateGameSlot.PlacementMarker,MarkerColor,"Short reach"));var marker=reachMarkers[i];marker.transform.position=Surface(FieldCells.World(grid,shortCells[i]),shortCells[i])+Vector3.up*1.5f;marker.transform.localScale=Vector3.one*.5f;Orient(marker,slot);marker.SetActive(true);}
            }
        }
    }

    private static FieldBounds Bounds(){FieldCells.TryBounds(out var b);return b;}
    private static void Orient(GameObject go,int slot){var d=AntennaSlots.Facing(slot);go.transform.rotation=Quaternion.LookRotation(new Vector3(d.x,0,d.y));}
    private static Vector3 Surface(Vector3 at,GridPosition cell){var ground=FieldCells.Ground(FieldCells.Grid,cell);if(ground!=null&&ground.GroundRenderer!=null)at.y=ground.GroundRenderer.bounds.max.y+.08f;return at;}

    // Konum seçildi: ödül tek işlemle uygulanır. Reddedilirse (konum bu arada geçersizleşti) panel açık kalır.
    public bool Confirm(int slot)
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (manager == null || manager.PlacementReward == null || !slots.Contains(slot)) return false;
        return manager.Choose(manager.PlacementReward, slot);
    }

    public void Cancel()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (manager != null) manager.CancelPlacement();
    }
}
