using System.Collections.Generic;
using UnityEngine;

// Bir hücre kümesinin çevre çizgisi için ortak şerit geometrisi (FrostZoneMarker shader'ı). Boss bölgesi (FrostZoneMarkers) ve
// Artçı Patlama'nın alan geri bildirimi (AreaOutlineFeedback) aynı kenar şeridini kullanır. Hangi hücrelerin kümede olduğuna
// çağıran karar verir; burada alan formülü yoktur.
public static class CellOutlineMesh
{
    public static readonly Vector2Int[] Sides = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    public static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    // Hücrenin "side" yönündeki kenarı boyunca, hücrenin içinde kalan bir şerit. UV.x: şerit boyunca dünya koordinatı,
    // UV.y: 0 dış kenar, 1 iç kenar. center: hücre merkezi (çizginin yüksekliğinde), half: hücrenin yarı boyu.
    public static void AddEdge(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector2Int side,
        float half, float inset, float width)
    {
        float outer = half - inset, inner = outer - width;
        int start = vertices.Count;
        if (side.x != 0)
        {
            float xo = center.x + side.x * outer, xi = center.x + side.x * inner;
            float z0 = center.z - half, z1 = center.z + half;
            vertices.Add(new Vector3(xo, center.y, z0)); uvs.Add(new Vector2(z0, 0f));
            vertices.Add(new Vector3(xo, center.y, z1)); uvs.Add(new Vector2(z1, 0f));
            vertices.Add(new Vector3(xi, center.y, z0)); uvs.Add(new Vector2(z0, 1f));
            vertices.Add(new Vector3(xi, center.y, z1)); uvs.Add(new Vector2(z1, 1f));
        }
        else
        {
            float zo = center.z + side.y * outer, zi = center.z + side.y * inner;
            float x0 = center.x - half, x1 = center.x + half;
            vertices.Add(new Vector3(x0, center.y, zo)); uvs.Add(new Vector2(x0, 0f));
            vertices.Add(new Vector3(x1, center.y, zo)); uvs.Add(new Vector2(x1, 0f));
            vertices.Add(new Vector3(x0, center.y, zi)); uvs.Add(new Vector2(x0, 1f));
            vertices.Add(new Vector3(x1, center.y, zi)); uvs.Add(new Vector2(x1, 1f));
        }
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
        triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
    }
}
