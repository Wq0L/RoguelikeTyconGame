using System.Collections.Generic;
using UnityEngine;

// Yer tutucu görseller (Bölüm 3.8): nihai model gelene kadar okunabilir, düşük poligonlu, tek renkli şekiller. Dış varlık
// kullanmaz. Renk köşe rengindedir ve tek bir paylaşılan malzemeyle (Resources/ParticleVertexColor) çizilir: nesne başına malzeme
// kopyası ya da MaterialPropertyBlock yoktur. Ağlar şekil + renk başına bir kez üretilir ve paylaşılır; Release hepsini yok eder.
// Bu şekiller nihai görsel değildir.
public static class PlaceholderShapes
{
    public enum Shape { Spike, Post, Diamond, Orb, Blade, Beam }

    private static readonly Dictionary<long, Mesh> meshes = new();
    private static Material material;
    private static bool unavailable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        meshes.Clear();
        material = null;
        unavailable = false;
    }

    public static bool Available => EnsureMaterial();

    private static bool EnsureMaterial()
    {
        if (material != null) return true;
        if (unavailable) return false;
        Shader shader = Resources.Load<Shader>("ParticleVertexColor");
        if (shader == null || !shader.isSupported)
        {
            unavailable = true;
            Debug.LogWarning("ParticleVertexColor shader bulunamadı; geç oyun yer tutucu görselleri çizilmeyecek (oynanış etkilenmez).");
            return false;
        }
        material = new Material(shader) { name = "Late Game Placeholder (runtime)" };
        return true;
    }

    // Collider'sız, gölgesiz tek bir şekil. Malzeme yoksa (shader eksik) boş bir nesne döner: çağıran aynı yoldan sürer.
    public static GameObject Create(Shape shape, Color color, string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (!EnsureMaterial()) return go;
        go.AddComponent<MeshFilter>().sharedMesh = MeshFor(shape, color);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    public static void Release()
    {
        foreach (Mesh mesh in meshes.Values) if (mesh != null) Object.Destroy(mesh);
        meshes.Clear();
        if (material != null) Object.Destroy(material);
        material = null;
    }

    private static Mesh MeshFor(Shape shape, Color color)
    {
        Color32 c = color;
        long key = ((long)shape << 32) | ((long)c.r << 24) | ((long)c.g << 16) | ((long)c.b << 8) | c.a;
        if (meshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        switch (shape)
        {
            case Shape.Spike: Cone(vertices, triangles, 0.32f, 1f, 5); break;          // taban yerde, uç yukarıda
            case Shape.Post: Box(vertices, triangles, new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 1f, 0.22f)); Octa(vertices, triangles, new Vector3(0f, 1.15f, 0f), 0.26f); break;
            case Shape.Diamond: Octa(vertices, triangles, Vector3.zero, 0.5f, 0.08f); break; // yassı, zeminde
            case Shape.Orb: Octa(vertices, triangles, Vector3.zero, 0.5f); break;
            case Shape.Blade: Blade(vertices, triangles); break;
            default: Box(vertices, triangles, new Vector3(0f, 0f, 0.5f), new Vector3(0.14f, 0.14f, 1f)); break; // +Z boyunca birim ışın
        }
        // Yüzler düz gölgelenmez (unlit); hacim hissi için üst köşeler açık, alt köşeler koyu renklenir.
        var colors = new List<Color32>(vertices.Count);
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 v in vertices) { if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y; }
        foreach (Vector3 v in vertices)
        {
            float t = maxY > minY ? (v.y - minY) / (maxY - minY) : 1f;
            colors.Add(Color.Lerp(color * 0.62f, color, t) + new Color(0f, 0f, 0f, 1f));
        }
        mesh = new Mesh { name = "Placeholder " + shape };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }

    private static void Triangle(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(start); t.Add(start + 1); t.Add(start + 2);
    }

    private static void Cone(List<Vector3> v, List<int> t, float radius, float height, int sides)
    {
        var top = new Vector3(0f, height, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Triangle(v, t, p0, top, p1);
        }
    }

    private static void Octa(List<Vector3> v, List<int> t, Vector3 center, float radius, float halfHeight = -1f)
    {
        float h = halfHeight < 0f ? radius : halfHeight;
        Vector3 up = center + Vector3.up * h, down = center - Vector3.up * h;
        var ring = new[] { center + Vector3.right * radius, center + Vector3.forward * radius, center - Vector3.right * radius, center - Vector3.forward * radius };
        for (int i = 0; i < 4; i++)
        {
            Triangle(v, t, ring[i], up, ring[(i + 1) % 4]);
            Triangle(v, t, ring[(i + 1) % 4], down, ring[i]);
        }
    }

    private static void Box(List<Vector3> v, List<int> t, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        var corner = new Vector3[8];
        for (int i = 0; i < 8; i++)
            corner[i] = center + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        int[] quads = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
        for (int q = 0; q < quads.Length; q += 4)
        {
            Triangle(v, t, corner[quads[q]], corner[quads[q + 1]], corner[quads[q + 2]]);
            Triangle(v, t, corner[quads[q]], corner[quads[q + 2]], corner[quads[q + 3]]);
        }
    }

    // Hilal: +Z'ye bakan yassı yay (XZ düzleminde), ortası kalın, uçları sivri.
    private static void Blade(List<Vector3> v, List<int> t)
    {
        const int segments = 6;
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Lerp(-1.1f, 1.1f, i / (float)segments), a1 = Mathf.Lerp(-1.1f, 1.1f, (i + 1) / (float)segments);
            Vector3 o0 = Arc(a0, 0.5f), o1 = Arc(a1, 0.5f);
            Vector3 i0 = Arc(a0, 0.5f - 0.2f * Mathf.Cos(a0 * 1.3f)), i1 = Arc(a1, 0.5f - 0.2f * Mathf.Cos(a1 * 1.3f));
            Triangle(v, t, o0, o1, i1);
            Triangle(v, t, o0, i1, i0);
        }
    }

    private static Vector3 Arc(float angle, float radius) => new Vector3(Mathf.Sin(angle) * radius, 0.12f, Mathf.Cos(angle) * radius - 0.25f);
}
