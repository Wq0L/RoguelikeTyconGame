using UnityEngine;
using UnityEngine.UI;

// A few stepped, ink-outlined glints at the frame, using unscaled time.
[RequireComponent(typeof(CanvasRenderer))]
public class ComicCardSparkles : MaskableGraphic
{
    public TileRarity rarity;
    int frame=-1;
    public void SetRarity(TileRarity value){rarity=value;color=value==TileRarity.Rare?new Color(.5f,.95f,1):value==TileRarity.Epic?new Color(.88f,.65f,1):new Color(1,.91f,.4f);SetVerticesDirty();}
    void Update(){if(rarity==TileRarity.Common)return;int next=(int)(Time.unscaledTime*10);if(next!=frame){frame=next;SetVerticesDirty();}}
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();int count=(int)rarity;if(count==0)return;
        var r=rectTransform.rect;
        for(int i=0;i<count;i++){
            float phase=Mathf.Repeat(Time.unscaledTime*.65f+i*.33f,1);
            float pulse=phase<.5f?phase*2:(1-phase)*2;
            float size=Mathf.Round(pulse*3)/3*(7+count*2);
            Vector2 point=i==0?new Vector2(r.xMax-20,r.yMax-35):i==1?new Vector2(r.xMin+25,r.yMin+85):new Vector2(r.xMax-18,r.yMin+28);
            Star(vh,point,size+2,new Color(.045f,.02f,.09f));Star(vh,point,size,color);
        }
    }
    static void Star(VertexHelper vh,Vector2 p,float size,Color c){
        int k=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
        for(int j=0;j<=8;j++){float a=j*Mathf.PI/4;float radius=j%2==0?size:size*.28f;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);if(j>0)vh.AddTriangle(k,k+j,k+j+1);}
    }
}
