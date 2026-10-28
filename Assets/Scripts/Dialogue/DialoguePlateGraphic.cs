using UnityEngine;
using UnityEngine.UI;

// Konuşma kutusunun kesik köşeli kâğıt parçaları (UI art direction: açılı kesik kâğıt silüeti, köşeler bilerek farklı).
// Geometri her ölçekte keskin; doku yok. Ağ yalnız boyut / renk değişince yeniden kurulur (karede bellek ayırma yok).
// Triangle: kısa mürekkep köşe çizgisi (dikdörtgenin içinde sol alt → sağ alt → sağ üst).
// Polygon: verilen dışbükey çokgen (noktalar dikdörtgenin yerel koordinatında, saat yönünün tersine).
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DialoguePlateGraphic : MaskableGraphic
{
    public enum Shape { CutRect, Triangle, Polygon }

    private Shape shape;
    // Köşe kesikleri (piksel): x sol üst, y sağ üst, z sağ alt, w sol alt.
    private Vector4 cuts;
    private Vector2[] points;

    public void Set(Shape value, Vector4 cornerCuts)
    {
        shape = value;
        cuts = cornerCuts;
        SetVerticesDirty();
    }

    public void SetPolygon(Vector2[] convexPoints)
    {
        shape = Shape.Polygon;
        points = convexPoints;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Color32 c = color;
        if (shape == Shape.Polygon)
        {
            if (points == null || points.Length < 3) return;
            for (int i = 0; i < points.Length; i++) vh.AddVert(points[i], c, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
            return;
        }
        if (shape == Shape.Triangle)
        {
            vh.AddVert(new Vector2(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            return;
        }
        float max = Mathf.Min(r.width, r.height) * .5f;
        float tl = Mathf.Clamp(cuts.x, 0f, max), tr = Mathf.Clamp(cuts.y, 0f, max), br = Mathf.Clamp(cuts.z, 0f, max), bl = Mathf.Clamp(cuts.w, 0f, max);
        vh.AddVert(r.center, c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin + tl, r.yMax), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax - tr, r.yMax), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMax - tr), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMin + br), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax - br, r.yMin), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin + bl, r.yMin), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin, r.yMin + bl), c, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin, r.yMax - tl), c, Vector2.zero);
        for (int i = 1; i <= 8; i++) vh.AddTriangle(0, i, i == 8 ? 1 : i + 1);
    }
}
