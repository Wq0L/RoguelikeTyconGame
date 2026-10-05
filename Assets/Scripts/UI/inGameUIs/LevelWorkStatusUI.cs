using TMPro;
using UnityEngine;

// Round süresi bitti ama round içinde kazanılan XP'nin level'ları hâlâ işleniyor (ProgressionManager kare bütçesi): kart ekranı
// bu iş bitince açılır (RoundManager.IsAwaitingLevels). O sürede oyuncuya kısa bir durum yazısı gösterir. Normal hasatta bekleyen
// iş olmaz ve yazı hiç görünmez. Yalnız gösterir: beklemeyi RoundManager, işi ProgressionManager yönetir.
public sealed class LevelWorkStatusUI : MonoBehaviour
{
    public const string Title = "SEVİYE KAZANIMLARI HESAPLANIYOR";

    private GameObject plate;
    private TextMeshProUGUI label;
    private int shownLevel = -1;

    public bool IsShown => plate != null && plate.activeSelf;
    public string Text => label != null ? label.text : null;

    public static LevelWorkStatusUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var root = new GameObject("Level Work Status", typeof(RectTransform));
        root.layer = canvasRoot.gameObject.layer;
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rootRect);
        var status = root.AddComponent<LevelWorkStatusUI>();
        status.Build(rootRect);
        return status;
    }

    private void Build(RectTransform root)
    {
        plate = new GameObject("Plate", typeof(RectTransform));
        plate.layer = root.gameObject.layer;
        var rect = (RectTransform)plate.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(820f, 120f);
        AddPlate(rect, "Ink", new Color32(54, 39, 54, 255), 0f);
        AddPlate(rect, "Face", new Color32(255, 244, 214, 255), 3f);
        label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.gameObject.layer = root.gameObject.layer;
        label.rectTransform.SetParent(rect, false);
        FeelOverlay.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(20f, 8f);
        label.rectTransform.offsetMax = new Vector2(-20f, -8f);
        FeelOverlay.Theme?.StylePopupText(label);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 34f;
        label.raycastTarget = false;
        plate.SetActive(false);
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

    private void Update()
    {
        RoundManager rounds = RoundManager.Instance;
        ProgressionManager progression = ProgressionManager.Instance;
        bool working = rounds != null && progression != null && GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameStates.Round && rounds.IsAwaitingLevels && progression.HasPendingLevels;
        if (!working)
        {
            if (plate.activeSelf) plate.SetActive(false);
            return;
        }
        if (!plate.activeSelf)
        {
            plate.SetActive(true);
            transform.SetAsLastSibling();
            shownLevel = -1;
        }
        if (progression.CurrentLevel == shownLevel) return;
        shownLevel = progression.CurrentLevel;
        label.text = $"{Title}\n<size=70%>Level {shownLevel}</size>";
    }
}
