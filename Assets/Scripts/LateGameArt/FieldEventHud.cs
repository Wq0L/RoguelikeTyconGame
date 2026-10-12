using TMPro;
using UnityEngine;

// Aktif tarla olayının durum plakası (Bölüm 3.8): olayın adı ve tek satırlık durumu (ne olacak, oyuncu ne yapabilir, kalan süre).
// Yalnız gösterir: metni olay üretir (FieldEvent.Title / Status); zamanlama FieldEventDirector'dadır. Olay yokken görünmez.
// Raycast almaz (arkasındaki tarlaya saldırıyı ve tıklamayı engellemez). UIManager kurar.
public sealed class FieldEventHud : MonoBehaviour
{
    private static readonly Color32 Ink = new(54, 39, 54, 255);
    private static readonly string[] ZoneNames = { "A", "B" };
    private const float LabelInset = 18f, LegendInset = 80f;

    private GameObject plate;
    private TextMeshProUGUI label;
    private BountyProgressGraphic bountyProgress;
    public int BountyProgress => bountyProgress!=null?bountyProgress.Progress:0;
    private readonly GameObject[] legend = new GameObject[2];
    private bool legendShown;
    private int shownVersion = -1;
    private int shownSecond = -1;

    public bool IsShown => plate != null && plate.activeSelf;
    public string Text => label != null ? label.text : null;
    public bool LegendShown => IsShown && legendShown;

    public static FieldEventHud Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<FieldEventHud>(true);
        if (existing != null) return existing;
        var root = new GameObject("Field Event Status", typeof(RectTransform));
        root.layer = canvasRoot.gameObject.layer;
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rootRect);
        var hud = root.AddComponent<FieldEventHud>();
        hud.Build(rootRect);
        return hud;
    }

    private void Build(RectTransform root)
    {
        plate = new GameObject("Plate", typeof(RectTransform));
        plate.layer = root.gameObject.layer;
        var rect = (RectTransform)plate.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        // Left margin below the quota panel; leaves the far field corner and target crowns clear.
        rect.anchoredPosition = new Vector2(24f, -250f);
        rect.sizeDelta = new Vector2(430f, 106f);
        AddPlate(rect, "Ink", Ink, 0f);
        AddPlate(rect, "Face", new Color32(255, 244, 214, 245), 3f);
        label = AddText(rect, "Label");
        FeelOverlay.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(LabelInset, 6f);
        label.rectTransform.offsetMax = new Vector2(-LabelInset, -6f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 26f;
        for (int zone = 0; zone < legend.Length; zone++) legend[zone] = AddLegend(rect, zone);
        var progressRect=new GameObject("Bounty Seeds",typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
        progressRect.SetParent(rect,false);progressRect.gameObject.layer=rect.gameObject.layer;
        progressRect.anchorMin=progressRect.anchorMax=new Vector2(.5f,0);progressRect.pivot=new Vector2(.5f,0);
        progressRect.anchoredPosition=new Vector2(0,10);progressRect.sizeDelta=new Vector2(280,28);
        bountyProgress=progressRect.gameObject.AddComponent<BountyProgressGraphic>();bountyProgress.raycastTarget=false;progressRect.gameObject.SetActive(false);
        plate.SetActive(false);
    }

    // Aşılı Kabuk'un bölge açıklaması: plakanın iki ucunda bölge rengi ve harfi (A solda, B sağda). Tarladaki belirti ve düğüm
    // aynı rengi kullanır (LateGamePresenter.ZoneColor); metindeki "A" / "B" böylece tarladaki bölgeyle eşleşir.
    private GameObject AddLegend(RectTransform parent, int zone)
    {
        var rect = new GameObject("Zone " + ZoneNames[zone], typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(zone == 0 ? 0f : 1f, .5f);
        rect.anchoredPosition = new Vector2(zone == 0 ? 14f : -14f, 0f);
        rect.sizeDelta = new Vector2(56f, 86f);
        AddPlate(rect, "Ink", Ink, 0f);
        AddPlate(rect, "Color", LateGamePresenter.ZoneColor(FieldEventKind.GraftedShell, zone), 3f);
        TextMeshProUGUI letter = AddText(rect, "Letter");
        FeelOverlay.Stretch(letter.rectTransform);
        letter.rectTransform.offsetMin = new Vector2(0f, 40f);
        letter.fontSize = 30f;
        letter.text = ZoneNames[zone];
        var motifRect = new GameObject("World Motif", typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        motifRect.SetParent(rect, false);motifRect.gameObject.layer=rect.gameObject.layer;
        motifRect.anchorMin=motifRect.anchorMax=new Vector2(.5f,0f);motifRect.pivot=new Vector2(.5f,0f);
        motifRect.anchoredPosition=new Vector2(0,8);motifRect.sizeDelta=new Vector2(32,32);
        var motif=motifRect.gameObject.AddComponent<ShellMotifGraphic>();motif.zone=zone;motif.color=Ink;motif.raycastTarget=false;
        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string name)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.gameObject.layer = parent.gameObject.layer;
        text.rectTransform.SetParent(parent, false);
        FeelOverlay.Theme?.StylePopupText(text);
        text.color = Ink;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
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
        FieldEventDirector director = FieldEventDirector.Instance;
        FieldEvent current = director != null ? director.Current : null;
        bool visible = current != null && current.Phase != FieldEventPhase.Waiting && current.Phase != FieldEventPhase.Done &&
            GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        if (!visible)
        {
            if (plate.activeSelf) plate.SetActive(false);
            shownVersion = -1;
            return;
        }
        if (!plate.activeSelf) plate.SetActive(true);
        ShowLegend(current.Kind == FieldEventKind.GraftedShell);
        bool bountyShown=current is WanderingBountyEvent;
        bountyProgress.gameObject.SetActive(false);
        if(!legendShown)
        {
            ((RectTransform)plate.transform).sizeDelta=new Vector2(430,bountyShown?150:106);
            label.rectTransform.offsetMin=new Vector2(LabelInset,6);
        }

        // Metin yalnız olay durumu ya da gösterilen saniye değişince yeniden yazılır.
        int second = Mathf.CeilToInt(current.PhaseTime);
        if (director.Version == shownVersion && second == shownSecond) return;
        shownVersion = director.Version;
        shownSecond = second;
        string status=current.Status;
        if(current is GraftedShellEvent shell)
        {
            if(!shell.Decided)
                status=$"Kırdığın düğümün bölgesi kurtulur\nA düğümü {shell.Targets[0].Remaining}/{shell.Targets[0].Durability} · B düğümü {shell.Targets[1].Remaining}/{shell.Targets[1].Durability}\n"+
                    (current.Phase==FieldEventPhase.Active?$"{Mathf.CeilToInt(Mathf.Max(0,director.Plan.graftedShell.choiceSeconds-current.PhaseTime))} sn sonra A kapanır":"Seçmezsen A kapanır!");
        }
        label.text = $"<size=120%>{current.Title}</size>\n<size=78%>{status}</size>";
    }

    private void ShowLegend(bool show)
    {
        if (show == legendShown) return;
        legendShown = show;
        ((RectTransform)plate.transform).sizeDelta=new Vector2(430f,show?150f:106f);
        for (int zone = 0; zone < legend.Length; zone++) legend[zone].SetActive(show);
        float inset = show ? LegendInset : LabelInset;
        label.rectTransform.offsetMin = new Vector2(inset, 6f);
        label.rectTransform.offsetMax = new Vector2(-inset, -6f);
    }
}



