using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundMapUI : MonoBehaviour
{
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(.16f,.19f,.21f,1);
    [SerializeField] private Sprite cellSprite;
    [SerializeField] private ComicUITheme theme;
    [SerializeField] private RectTransform columnHeaders, rowHeaders;
    [SerializeField] private Vector2 availableSize = new Vector2(840,680);
    readonly List<GameObject> cells = new List<GameObject>();
    readonly List<TMP_Text> columns = new List<TMP_Text>(), rows = new List<TMP_Text>();

    int eventVersion=-1;
    // Harita açık mı (6 Kasım 2026, UI kalıntıları): açıkken dünyadaki koordinat rozetleri (GridCoordinateLabels) gizlenir; iki
    // koordinat sistemi üst üste görünmez, rozetler haritanın kenarından taşmaz.
    static int shown;
    public static bool Visible=>shown>0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics(){shown=0;}
    // Ekranın arkası (6 Kasım 2026, kullanıcı: arayüz oyun ekranından ayrışsın; haritanın arkasına kare değil, bütün ekrana): ana
    // tuvalin EN ALTINDA (ilk çocuk) tam ekran koyu
    // katman. Yalnız dünya ve gökyüzü kararır; HUD (kaynaklar) ve paneller üstünde kalır. Harita açıkken görünür, panelin geçiş
    // saydamlığını izler (CanvasGroup), tık almaz.
    public static readonly Color BackdropColor=new Color(.02f,.02f,.05f,.72f);
    Image backdrop;CanvasGroup panelGroup;float backdropAlpha=-1f;
    public Image Backdrop=>backdrop;
    void EnsureBackdrop()
    {
        if(backdrop==null){
            Canvas canvas=GetComponentInParent<Canvas>();if(canvas==null)return;
            var rect=new GameObject("Screen Backdrop",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<RectTransform>();
            rect.gameObject.layer=gameObject.layer;rect.SetParent(canvas.rootCanvas.transform,false);rect.SetAsFirstSibling();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            backdrop=rect.GetComponent<Image>();backdrop.color=BackdropColor;backdrop.raycastTarget=false;
            panelGroup=GetComponentInParent<CanvasGroup>();
        }
        backdrop.gameObject.SetActive(true);backdropAlpha=-1f;FollowPanel();
    }
    void FollowPanel()
    {
        if(backdrop==null)return;
        float a=panelGroup!=null?panelGroup.alpha:1f;if(a==backdropAlpha)return;
        backdropAlpha=a;var c=BackdropColor;c.a*=a;backdrop.color=c;
    }
    private void OnEnable(){shown++;EnsureBackdrop();BuildGrid();}
    private void OnDisable(){shown=Mathf.Max(0,shown-1);if(backdrop!=null)backdrop.gameObject.SetActive(false);}
    private void OnDestroy(){if(backdrop!=null)Destroy(backdrop.gameObject);}
    // Olay bölgesi panel açıkken seçilebilir (hazırlıkta ilk kare): sürüm değişince hücreler yeniden işaretlenir.
    private void Update(){FollowPanel();var events=SegmentEventDirector.Instance;if(cells.Count>0&&(events!=null?events.Version:-1)!=eventVersion)BuildGrid();}
    private void Start(){if(cells.Count==0)BuildGrid();}
    public void BuildGrid()
    {
        if(GridManager.Instance==null)return;
        int width=GridManager.Instance.GetWidth(),height=GridManager.Instance.GetHeight();
        if(width<1||height<1)return;
        var layout=gridContainer.GetComponent<GridLayoutGroup>();
        float unit=Mathf.Min((availableSize.x-(width-1)*6)/(width*1.15f),(availableSize.y-(height-1)*6)/height);
        Vector2 size=new Vector2(unit*1.15f,unit),step=size+Vector2.one*6;
        Vector2 boardSize=new Vector2(width*step.x-6,height*step.y-6);
        var rect=(RectTransform)gridContainer;rect.sizeDelta=boardSize;
        if(layout){layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=width;layout.cellSize=size;layout.spacing=Vector2.one*6;layout.startCorner=GridLayoutGroup.Corner.UpperLeft;layout.startAxis=GridLayoutGroup.Axis.Horizontal;layout.childAlignment=TextAnchor.UpperLeft;}
        // Reuse cells between rounds, including their original tooltip components.
        while(cells.Count<width*height)cells.Add(Instantiate(cellPrefab,gridContainer));
        var grid=GridManager.Instance.GetGridSystem();
        var progression=ProgressionManager.Instance;
        var events=SegmentEventDirector.Instance;eventVersion=events!=null?events.Version:-1;
        var zone=events!=null?(events.Active??events.Upcoming):null;if(zone!=null&&!zone.IsPrepared)zone=null;
        int fresh=0;
        // Comic kağıt kiti (6 Kasım 2026, kullanıcı: kapalı hücre siyah, açık hücre beyaz, renk altındaki tile'a göre): açık hücre beyaz
        // kare (tile rengiyle boyanır, mürekkep konturu koyu kalır), kapalı hücre kömür karesi. 9-dilim, dikeyde 1:1. Koordinat
        // başlıkları eski mavi plakada kalır (UI-Art-Direction istisnası). Kit yoksa eski düz kare + renk.
        Sprite openCell=ComicKit.Get("slot_white"),lockedCell=ComicKit.Get("slot_charcoal");bool kit=openCell!=null&&lockedCell!=null;
        float kitScale=kit?openCell.rect.height/Mathf.Max(1f,size.y):1f;
        for(int i=0;i<cells.Count;i++){
            var cell=cells[i];cell.SetActive(i<width*height);if(i>=width*height)continue;
            int x=i%width,z=height-1-i/width;
            cell.name=$"Cell_x{x}_z{z}";
            var ground=grid.GetGridObject(new GridPosition(x,z))?.GetGroundCellCached();
            var image=cell.GetComponent<Image>();
            bool locked=ground==null||ground.IsLocked;
            if(kit){image.sprite=locked?lockedCell:openCell;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=kitScale;}
            else if(cellSprite){image.sprite=cellSprite;image.type=Image.Type.Simple;image.pixelsPerUnitMultiplier=5;}
            image.color=locked?(kit?Color.white:lockedColor):ground.CurrentModifier!=null?ground.CurrentModifier.tileColor:emptyColor;
            var tile=cell.GetComponent<TileCellUI>();if(tile==null)continue;
            tile.Setup(ground);
            // Bu round'un kartları: panel açılış animasyonundan sonra sırayla parlar.
            bool isFresh=progression!=null&&progression.WasAppliedThisRound(ground);
            tile.SetFresh(isFresh,isFresh?.3f+fresh++*.12f:0,FreshTileRing.LabelFor(ground));
            // Boss bölgesi: dünyadaki çevre çizgisiyle aynı hücreler; çerçeve (kesikli → düz), tile rengi örtülmez.
            tile.SetEventZone(zone!=null&&ground!=null&&!ground.IsLocked&&zone.Covers(new GridPosition(x,z)),zone!=null&&zone.IsActive,zone!=null?BossTheme.Accent(zone.Data):Color.clear);
        }
        bool headers=columnHeaders&&rowHeaders&&theme&&cellSprite;
        if(headers){
            FillHeaders(columns,columnHeaders,width,true,size,step,boardSize,rect.anchoredPosition);
            FillHeaders(rows,rowHeaders,height,false,size,step,boardSize,rect.anchoredPosition);
        }
    }
    // Koordinat kutuları (6 Kasım 2026, kullanıcı isteği): comic kitin mavi karesi (slot_blue, boyanmaz) + Bungee; dünyadaki
    // rozetlerle (GridCoordinateLabels) aynı. Kit yoksa eski mavi yuvarlak kare.
    void FillHeaders(List<TMP_Text> labels,RectTransform parent,int count,bool horizontal,Vector2 size,Vector2 step,Vector2 board,Vector2 origin)
    {
        Sprite kitPlate=ComicKit.Get("slot_blue");
        while(labels.Count<count){
            var go=new GameObject("Coordinate",typeof(RectTransform),typeof(Image));go.layer=gameObject.layer;go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.raycastTarget=false;
            if(kitPlate){image.sprite=kitPlate;image.type=Image.Type.Sliced;image.color=Color.white;}
            else{image.sprite=cellSprite;image.type=Image.Type.Simple;image.pixelsPerUnitMultiplier=5;image.color=new Color(.25f,.43f,.57f);}
            var child=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));child.layer=go.layer;child.transform.SetParent(go.transform,false);
            var text=child.GetComponent<TextMeshProUGUI>();text.font=theme.headingFont;text.fontSharedMaterial=theme.outlinedText;text.color=Color.white;text.fontSize=30;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            if(ComicKit.Display(text,true,theme))text.fontSize=26;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.sizeDelta=Vector2.zero;
            // Kutunun alt 3B kenarı yazının altında kalsın: yazı biraz yukarıda.
            if(kitPlate){text.rectTransform.offsetMin=new Vector2(0f,5f);text.rectTransform.offsetMax=new Vector2(0f,1f);}
            labels.Add(text);
        }
        for(int i=0;i<labels.Count;i++){
            var text=labels[i];text.transform.parent.gameObject.SetActive(i<count);if(i>=count)continue;
            text.text=horizontal?ColumnName(i):(i+1).ToString();
            var r=(RectTransform)text.transform.parent;r.sizeDelta=horizontal?new Vector2(size.x,46):new Vector2(46,size.y);
            var plate=r.GetComponent<Image>();if(kitPlate&&plate.sprite==kitPlate)plate.pixelsPerUnitMultiplier=kitPlate.rect.height/Mathf.Max(1f,Mathf.Min(r.sizeDelta.x,r.sizeDelta.y));
            r.anchoredPosition=origin+(horizontal?new Vector2(-board.x/2+size.x/2+i*step.x,board.y/2+29):new Vector2(-board.x/2-29,board.y/2-size.y/2-i*step.y));
        }
    }
    public static string ColumnName(int index){string name="";for(int n=index+1;n>0;n=(n-1)/26)name=(char)('A'+(n-1)%26)+name;return name;}
    // Haritadaki başlıklarla aynı ad: A = x0, "1" = en üst satır (z en büyük). Örn. "B3".
    public static string CellName(GridPosition position){int height=GridManager.Instance!=null?GridManager.Instance.GetHeight():position.z+1;return ColumnName(position.x)+(height-position.z);}
}
