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

    private void OnEnable(){BuildGrid();}
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
        for(int i=0;i<cells.Count;i++){
            var cell=cells[i];cell.SetActive(i<width*height);if(i>=width*height)continue;
            int x=i%width,z=height-1-i/width;
            cell.name=$"Cell_x{x}_z{z}";
            var ground=grid.GetGridObject(new GridPosition(x,z))?.GetGroundCellCached();
            var image=cell.GetComponent<Image>();
            if(cellSprite){image.sprite=cellSprite;image.type=Image.Type.Simple;image.pixelsPerUnitMultiplier=5;}
            image.color=ground==null||ground.IsLocked?lockedColor:ground.CurrentModifier!=null?ground.CurrentModifier.tileColor:emptyColor;
            cell.GetComponent<TileCellUI>()?.Setup(ground);
        }
        if(columnHeaders&&rowHeaders&&theme&&cellSprite){
            FillHeaders(columns,columnHeaders,width,true,size,step,boardSize,rect.anchoredPosition);
            FillHeaders(rows,rowHeaders,height,false,size,step,boardSize,rect.anchoredPosition);
        }
    }
    void FillHeaders(List<TMP_Text> labels,RectTransform parent,int count,bool horizontal,Vector2 size,Vector2 step,Vector2 board,Vector2 origin)
    {
        while(labels.Count<count){
            var go=new GameObject("Coordinate",typeof(RectTransform),typeof(Image));go.layer=gameObject.layer;go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=cellSprite;image.type=Image.Type.Simple;image.pixelsPerUnitMultiplier=5;image.color=new Color(.25f,.43f,.57f);image.raycastTarget=false;
            var child=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));child.layer=go.layer;child.transform.SetParent(go.transform,false);
            var text=child.GetComponent<TextMeshProUGUI>();text.font=theme.headingFont;text.fontSharedMaterial=theme.outlinedText;text.color=Color.white;text.fontSize=30;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.sizeDelta=Vector2.zero;labels.Add(text);
        }
        for(int i=0;i<labels.Count;i++){
            var text=labels[i];text.transform.parent.gameObject.SetActive(i<count);if(i>=count)continue;
            text.text=horizontal?ColumnName(i):(i+1).ToString();
            var r=(RectTransform)text.transform.parent;r.sizeDelta=horizontal?new Vector2(size.x,46):new Vector2(46,size.y);
            r.anchoredPosition=origin+(horizontal?new Vector2(-board.x/2+size.x/2+i*step.x,board.y/2+29):new Vector2(-board.x/2-29,board.y/2-size.y/2-i*step.y));
        }
    }
    static string ColumnName(int index){string name="";for(int n=index+1;n>0;n=(n-1)/26)name=(char)('A'+(n-1)%26)+name;return name;}
}
