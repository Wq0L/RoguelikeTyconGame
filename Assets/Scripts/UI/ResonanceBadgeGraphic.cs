using UnityEngine;

// Small code-native vector badges: comic ink, flat toon fill, ten distinct pictograms.
// No textures, materials, fonts or allocations are created per frame.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResonanceBadgeGraphic : UnityEngine.UI.MaskableGraphic
{
    string recipe;
    static readonly Color Ink = new(.10f, .075f, .14f, 1f);
    static readonly Color Paper = new(1f, .96f, .81f, 1f);
    UnityEngine.UI.VertexHelper mesh;
    Vector2 center;
    float unit;

    public void SetRecipe(string id) { if (recipe == id) return; recipe = id; SetVerticesDirty(); }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear(); mesh = vh; var rect = GetPixelAdjustedRect(); center = rect.center; unit = Mathf.Min(rect.width, rect.height) / 2f;
        Color fill = recipe switch
        {
            "focus" or "empowered-harvest" or "skill-attack" or "skill-critical" or "skill-explosion" => new Color(1f, .47f, .27f),
            "fame" or "precious-harvest" or "skill-gold" or "skill-score" or "skill-time" => new Color(1f, .77f, .20f),
            "bounty" or "fertile-soil" or "skill-spawn" or "skill-planter" or "skill-grid" => new Color(.42f, .84f, .45f),
            "wisdom" or "electric-wisdom" or "skill-xp" or "skill-speed" or "skill-electric" or "skill-tornado" => new Color(.36f, .73f, 1f),
            "skill-iron" or "skill-stone" => new Color(.65f,.73f,.81f),
            _ => new Color(.75f, .49f, .94f)
        };
        Disc(new Vector2(.025f,-.045f), .94f, Ink); Disc(Vector2.zero, .89f, Ink); Disc(Vector2.zero, .77f, fill);
        Line(new(-.42f,.53f), new(.2f,.65f), .065f, Paper);
        switch (recipe)
        {
            case "focus": Sword(); break;
            case "fame": Star(Vector2.zero,.48f); break;
            case "bounty": Coins(); break;
            case "wisdom": Book(); break;
            case "crystal-garden": Gem(Vector2.zero,.55f); break;
            case "empowered-harvest": Sword(); Star(new(.38f,.3f),.19f); break;
            case "precious-harvest": Star(Vector2.zero,.49f); Gem(Vector2.zero,.27f); break;
            case "fertile-soil": Coins(); Sprout(); break;
            case "electric-wisdom": Book(); Bolt(); break;
            case "rare-nursery": Gem(new(0,-.13f),.43f); Sprout(); break;
            case "skill-attack": Sword(); break;
            case "skill-speed": Sword(); Line(new(-.48f,.1f),new(-.25f,.4f),.07f,Paper); Line(new(-.58f,-.08f),new(-.38f,.18f),.07f,Paper); break;
            case "skill-critical": Sword(); Star(new(.32f,-.28f),.22f); break;
            case "skill-gold": Coins(); break;
            case "skill-iron": Ingot(); break;
            case "skill-stone": Rock(); break;
            case "skill-xp": Book(); break;
            case "skill-score": Star(Vector2.zero,.5f); break;
            case "skill-rare": Gem(Vector2.zero,.54f); break;
            case "skill-spawn": Clock(new(0,-.12f),.33f); Sprout(); break;
            case "skill-time": Clock(Vector2.zero,.46f); break;
            case "skill-area": Disc(Vector2.zero,.49f,Ink);Disc(Vector2.zero,.41f,Paper);Disc(Vector2.zero,.27f,Ink);Disc(Vector2.zero,.19f,fill);break;
            case "skill-grid": Grid(); break;
            case "skill-planter": Pot(); Sprout(); break;
            case "skill-cards": Cards(); break;
            case "skill-duplicate": Cards(); Stroke(new(.2f,-.2f),new(.48f,-.2f),.06f,Paper); Stroke(new(.34f,-.34f),new(.34f,-.06f),.06f,Paper); break;
            case "skill-electric": Bolt(); break;
            case "skill-tornado": Tornado(); break;
            case "skill-scythe": Scythe(); break;
            case "skill-explosion": Star(Vector2.zero,.56f);Disc(Vector2.zero,.19f,Ink);Disc(Vector2.zero,.12f,new Color(1,.6f,.12f));break;
            case "skill-unlock": Key(); break;
        }
    }
    void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(center+a*unit,color,Vector2.zero); mesh.AddVert(center+b*unit,color,Vector2.zero); mesh.AddVert(center+c*unit,color,Vector2.zero);
        mesh.AddTriangle(i,i+1,i+2);
    }
    void Disc(Vector2 p,float radius,Color color)
    {
        for(int i=0;i<40;i++)
        {
            float a=i*Mathf.PI/20f,b=(i+1)*Mathf.PI/20f;
            Triangle(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color);
        }
    }
    void Line(Vector2 a,Vector2 b,float width,Color color)
    {
        var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;
        Triangle(a-n,a+n,b+n,color);Triangle(a-n,b+n,b-n,color);
    }
    void Stroke(Vector2 a,Vector2 b,float width,Color color) { Line(a,b,width+.085f,Ink);Line(a,b,width,color); }
    void Sword()
    {
        Stroke(new(-.24f,-.33f),new(.25f,.32f),.16f,Paper);
        Triangle(new(.12f,.31f),new(.29f,.5f),new(.34f,.19f),Ink);
        Stroke(new(-.38f,-.14f),new(-.05f,-.39f),.09f,new Color(1f,.8f,.2f));
        Stroke(new(-.27f,-.35f),new(-.38f,-.5f),.10f,new Color(.6f,.34f,.19f));
    }
    void Star(Vector2 p,float r)
    {
        for(int i=0;i<10;i++)
        {
            float a=Mathf.PI*.5f+i*Mathf.PI/5,b=a+Mathf.PI/5;
            float x=i%2==0?r:r*.48f,y=i%2==0?r*.48f:r;
            var v=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*x;var w=p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*y;
            Triangle(p,v,w,Paper);Line(v,w,.065f,Ink);
        }
    }
    void Coins()
    {
        Disc(new(-.18f,.02f),.31f,Ink);Disc(new(-.18f,.02f),.24f,Paper);
        Disc(new(.17f,-.18f),.33f,Ink);Disc(new(.17f,-.18f),.25f,new Color(1f,.8f,.18f));
        Line(new(.17f,-.34f),new(.17f,-.02f),.07f,Ink);
    }
    void Book()
    {
        Stroke(new(-.25f,-.2f),new(-.25f,.24f),.38f,Paper);Stroke(new(.25f,-.2f),new(.25f,.24f),.38f,Paper);
        Line(new(0,-.43f),new(0,.45f),.075f,Ink);
        Line(new(-.36f,.1f),new(-.13f,.1f),.05f,Ink);Line(new(.13f,.1f),new(.36f,.1f),.05f,Ink);
    }
    void Gem(Vector2 p,float r)
    {
        Vector2 top=p+Vector2.up*r,right=p+Vector2.right*r*.65f,bottom=p-Vector2.up*r,left=p-Vector2.right*r*.65f;
        Triangle(top,right,bottom,Paper);Triangle(top,bottom,left,new Color(.54f,.96f,1f));
        Line(top,right,.06f,Ink);Line(right,bottom,.06f,Ink);Line(bottom,left,.06f,Ink);Line(left,top,.06f,Ink);Line(top,bottom,.045f,Ink);
    }
    void Sprout()
    {
        Stroke(new(0,.06f),new(0,.4f),.07f,new Color(.2f,.65f,.25f));
        Stroke(new(0,.32f),new(-.25f,.47f),.14f,new Color(.55f,1f,.4f));
        Stroke(new(.02f,.25f),new(.25f,.42f),.14f,new Color(.55f,1f,.4f));
    }
    void Bolt()
    {
        Vector2 a=new(.18f,.55f),b=new(-.16f,.04f),c=new(.08f,.04f),d=new(-.12f,-.5f),e=new(.3f,.15f),f=new(.06f,.15f);
        Triangle(a,b,c,new Color(1f,.74f,.05f));Triangle(c,d,e,new Color(1f,.74f,.05f));Triangle(a,c,f,new Color(1f,.74f,.05f));
        Line(a,b,.055f,Ink);Line(b,c,.055f,Ink);Line(c,d,.055f,Ink);Line(d,e,.055f,Ink);Line(e,f,.055f,Ink);Line(f,a,.055f,Ink);
    }
    void Clock(Vector2 p,float r)
    {
        Disc(p,r+.07f,Ink);Disc(p,r,Paper);
        Stroke(p,p+new Vector2(0,r*.66f),.05f,Ink);Stroke(p,p+new Vector2(r*.5f,-r*.24f),.05f,Ink);
    }
    void Ingot()
    {
        Stroke(new(-.35f,0),new(.35f,0),.42f,Paper);
        Line(new(-.34f,-.1f),new(.33f,-.1f),.1f,new Color(.4f,.53f,.65f));
        Line(new(-.3f,.15f),new(.15f,.15f),.055f,Color.white);
    }
    void Rock()
    {
        Vector2 a=new(-.45f,-.28f),b=new(-.3f,.28f),c=new(.14f,.43f),d=new(.46f,-.07f),e=new(.25f,-.38f);
        Triangle(a,b,c,Paper);Triangle(a,c,e,new Color(.68f,.75f,.79f));Triangle(c,d,e,new Color(.4f,.5f,.6f));
        Line(a,b,.07f,Ink);Line(b,c,.07f,Ink);Line(c,d,.07f,Ink);Line(d,e,.07f,Ink);Line(e,a,.07f,Ink);
    }
    void Grid()
    {
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            Stroke(new(-.24f+x*.48f,-.39f+y*.48f),new(-.24f+x*.48f,-.1f+y*.48f),.3f,Paper);
    }
    void Pot()
    {
        Stroke(new(-.24f,-.22f),new(.24f,-.22f),.33f,new Color(1f,.64f,.3f));
        Stroke(new(-.34f,-.02f),new(.34f,-.02f),.11f,Paper);
    }
    void Cards()
    {
        Stroke(new(-.22f,-.24f),new(-.22f,.3f),.35f,new Color(.62f,.91f,1));
        Stroke(new(.16f,-.34f),new(.16f,.18f),.38f,Paper);Star(new(.16f,-.04f),.13f);
    }
    void Tornado()
    {
        for(int i=0;i<4;i++)
        {
            float width=.46f-i*.1f,y=.34f-i*.22f;
            Stroke(new(-width,y),new(width,y+.08f),.09f,Paper);
        }
    }
    void Scythe()
    {
        Stroke(new(-.29f,-.5f),new(.22f,.35f),.10f,new Color(.4f,.8f,.9f));
        Vector2 a=new(-.47f,.17f),b=new(.19f,.51f),c=new(.5f,.22f),d=new(.1f,.25f);
        Triangle(a,b,d,Paper);Triangle(b,c,d,Paper);Line(a,b,.065f,Ink);Line(b,c,.065f,Ink);Line(c,d,.065f,Ink);Line(d,a,.065f,Ink);
    }
    void Key()
    {
        Disc(new(-.2f,.2f),.27f,Ink);Disc(new(-.2f,.2f),.18f,Paper);Disc(new(-.2f,.2f),.085f,Ink);
        Stroke(new(-.02f,.03f),new(.37f,-.36f),.1f,Paper);Stroke(new(.18f,-.16f),new(.3f,-.04f),.09f,Paper);
    }
}
