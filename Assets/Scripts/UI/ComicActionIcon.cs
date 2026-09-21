using UnityEngine;
using UnityEngine.UI;

// Small vector UI icons stay crisp at any canvas resolution.
[RequireComponent(typeof(CanvasRenderer))]
public class ComicActionIcon : MaskableGraphic
{
    public enum Shape { Cart, Star }
    public Shape shape;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(shape==Shape.Star){Star(vh,1,Color.black);Star(vh,.74f,new Color(1,.96f,.7f));return;}
        Line(vh,new Vector2(-.85f,.72f),new Vector2(-.6f,.65f),.18f,Color.black);
        Line(vh,new Vector2(-.6f,.65f),new Vector2(-.35f,-.45f),.18f,Color.black);
        Line(vh,new Vector2(-.35f,-.45f),new Vector2(.65f,-.45f),.18f,Color.black);
        Quad(vh,new Vector2(-.58f,.5f),new Vector2(.85f,.5f),new Vector2(.6f,-.27f),new Vector2(-.4f,-.27f),Color.black);
        Quad(vh,new Vector2(-.37f,.31f),new Vector2(.61f,.31f),new Vector2(.46f,-.08f),new Vector2(-.28f,-.08f),Color.white);
        Line(vh,new Vector2(-.81f,.73f),new Vector2(-.58f,.67f),.07f,Color.white);
        Circle(vh,new Vector2(-.25f,-.69f),.19f,Color.black);Circle(vh,new Vector2(.52f,-.69f),.19f,Color.black);
        Circle(vh,new Vector2(-.25f,-.69f),.09f,Color.white);Circle(vh,new Vector2(.52f,-.69f),.09f,Color.white);
    }
    void Point(VertexHelper vh,Vector2 p,Color c){var r=rectTransform.rect;vh.AddVert(new Vector3(r.center.x+p.x*r.width*.48f,r.center.y+p.y*r.height*.48f),c*color,Vector2.zero);}
    void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color col){int k=vh.currentVertCount;Point(vh,a,col);Point(vh,b,col);Point(vh,c,col);Point(vh,d,col);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color col){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(vh,a+n,b+n,b-n,a-n,col);}
    void Circle(VertexHelper vh,Vector2 center,float radius,Color col){int k=vh.currentVertCount;Point(vh,center,col);for(int i=0;i<=24;i++){float a=i*Mathf.PI/12;Point(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,col);if(i>0)vh.AddTriangle(k,k+i,k+i+1);}}
    void Star(VertexHelper vh,float radius,Color col){int k=vh.currentVertCount;Point(vh,Vector2.zero,col);for(int i=0;i<=10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5;float r=(i%2==0?1:.48f)*radius;Point(vh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,col);if(i>0)vh.AddTriangle(k,k+i,k+i+1);}}
}
