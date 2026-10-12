using UnityEngine;
public sealed class ShellMotifGraphic : UnityEngine.UI.MaskableGraphic
{
    public int zone;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();var rect=rectTransform.rect;float size=Mathf.Min(rect.width,rect.height);
        foreach(var polygon in ShellMotifs.Polygons(zone))
        {
            int first=vh.currentVertCount;
            foreach(var p in polygon)vh.AddVert((Vector3)(rect.center+p*size),color,Vector2.zero);
            for(int i=1;i<polygon.Length-1;i++)vh.AddTriangle(first,first+i+1,first+i);
        }
    }
}


