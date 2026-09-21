using UnityEngine;
using UnityEngine.UI;

// One lightweight UI mesh, tinted from the offered tile rather than its rarity.
[RequireComponent(typeof(CanvasRenderer))]
public class ComicTilePreview : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color ink=new Color(.065f,.035f,.11f), shade=Color.Lerp(color,ink,.48f);
        // Hard, broken silhouette like the cast shadows in the comic reference.
        Polygon(vh,new Color(.12f,.06f,.23f,.25f),new Vector2(-.6f,-.37f),new Vector2(.67f,-.2f),new Vector2(.54f,-.03f),new Vector2(.96f,.3f),new Vector2(.72f,.25f),new Vector2(.92f,.65f),new Vector2(.58f,.39f),new Vector2(.39f,.5f),new Vector2(-.18f,.04f));
        var a=new Vector2(-.72f,.11f);var b=new Vector2(-.05f,.59f);var c=new Vector2(.69f,.19f);var d=new Vector2(.02f,-.33f);var depth=new Vector2(0,-.15f);
        Polygon(vh,ink,a,b,c,c+depth,d+depth,a+depth);
        Polygon(vh,shade,a+new Vector2(.035f,-.005f),d,d+depth+new Vector2(0,.035f),a+depth+new Vector2(.035f,.035f));
        Polygon(vh,shade,d,c-new Vector2(.035f,.005f),c+depth+new Vector2(-.035f,.035f),d+depth+new Vector2(0,.035f));
        Polygon(vh,ink,a,b,c,d);
        Vector2 center=(a+b+c+d)*.25f;
        Polygon(vh,color,Vector2.Lerp(center,a,.9f),Vector2.Lerp(center,b,.9f),Vector2.Lerp(center,c,.9f),Vector2.Lerp(center,d,.9f));
        Color highlight=Color.Lerp(color,Color.white,.55f);
        Polygon(vh,highlight,Vector2.Lerp(center,a,.84f),Vector2.Lerp(center,b,.84f),Vector2.Lerp(center,b,.72f),Vector2.Lerp(center,a,.72f));
        Polygon(vh,highlight,Vector2.Lerp(center,b,.84f),Vector2.Lerp(center,c,.84f),Vector2.Lerp(center,c,.72f),Vector2.Lerp(center,b,.72f));
        Color inset=Color.Lerp(color,ink,.22f);
        Polygon(vh,inset,Vector2.Lerp(center,a,.84f),Vector2.Lerp(center,a,.72f),Vector2.Lerp(center,d,.72f),Vector2.Lerp(center,d,.84f));
        Polygon(vh,inset,Vector2.Lerp(center,d,.84f),Vector2.Lerp(center,d,.72f),Vector2.Lerp(center,c,.72f),Vector2.Lerp(center,c,.84f));
        Line(vh,new Vector2(-.84f,.03f),new Vector2(-.98f,.14f),ink);
        Line(vh,new Vector2(.74f,-.29f),new Vector2(.9f,-.36f),ink);
    }
    void Line(VertexHelper vh,Vector2 a,Vector2 b,Color c){var n=new Vector2(-(b-a).y,(b-a).x).normalized*.018f;Polygon(vh,c,a+n,b+n,b-n,a-n);}
    void Polygon(VertexHelper vh,Color c,params Vector2[] points)
    {
        // These small comic shapes are drawn as triangle fans with no textures/material instances.
        int first=vh.currentVertCount;var r=rectTransform.rect;float unit=Mathf.Min(r.width*.5f,r.height*.77f);
        foreach(var p in points)vh.AddVert(new Vector3(r.center.x+p.x*unit,r.center.y+p.y*unit),c,Vector2.zero);
        for(int i=1;i<points.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
    }
}
