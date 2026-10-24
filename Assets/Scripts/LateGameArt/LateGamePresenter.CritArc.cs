using System.Collections.Generic;
using UnityEngine;

// Kritik Ark sunumu (23 Ekim 2026): kritik vuruş yiyen bitkiden hedefe zıplayan, mürekkep çizgili altın kavis + iniş yıldızı.
// - Renk kritik sayısıyla aynı altındır: oyuncu "kritik sıçradı" diye okur. Elektrik zikzağı kullanılmaz; ark elektrik ailesi değildir.
// - Hasar cue gelmeden uygulanmıştır. Yay bir anda tam belirir ve ArcSeconds içinde incelerek söner: hasarı geciktirmez.
// - Havuzlu: parçalar ilk ihtiyaçta kurulur, sonra yeniden kullanılır. Aynı anda en çok ArcCapacity yay; fazlası çizilmez, sayılır.
// - Malzeme yoksa (shader eksik) hiçbir şey çizilmez; oynanış etkilenmez. Taşan Hasar kendi ArcBolt yolunu korur.
public sealed partial class LateGamePresenter
{
    private const float ArcSeconds = .3f, ArcLift = .65f;
    private const int ArcCapacity = 24, ArcPoints = 24;
    private static readonly Color ArcInk = new(.10f, .075f, .14f), ArcGold = new(1f, .70f, .12f), ArcCream = new(1f, .95f, .78f);

    private sealed class GoldArc
    {
        public GameObject root;
        public LineRenderer ink, gold, shine;
        public Transform star;
        public float age;
    }

    private readonly List<GoldArc> liveArcs = new(), spareArcs = new();
    private readonly Vector3[] arcPoints = new Vector3[ArcPoints], arcShifted = new Vector3[ArcPoints];
    private Material arcMaterial;
    private Mesh arcStarMesh;
    private bool arcUnavailable;

    public int CritArcsLive => liveArcs.Count;
    public int CritArcsSkipped { get; private set; }

    private void ShowCritArc(Vector3 from, Vector3 to)
    {
        Camera camera = Camera.main;
        if (camera == null || !EnsureArcAssets()) return;
        if (liveArcs.Count >= ArcCapacity) { CritArcsSkipped++; return; }
        GoldArc arc;
        if (spareArcs.Count > 0) { arc = spareArcs[^1]; spareArcs.RemoveAt(spareArcs.Count - 1); }
        else arc = BuildArc();

        Vector3 a = from + Vector3.up * ArcLift, b = to + Vector3.up * ArcLift;
        // Uzak hedefte yay biraz yükselir; 1 hücrelik sıçrama tarlaya yakın kalır.
        float height = (.55f + .25f * Vector3.Distance(a, b)) * .75f;
        for (int i = 0; i < ArcPoints; i++)
        {
            float t = i / (ArcPoints - 1f);
            arcPoints[i] = Vector3.Lerp(a, b, t) + Vector3.up * (4f * height * t * (1f - t));
        }
        // Katmanlar kameraya göre çok az ayrılır: mürekkep arkada, krem ışık önde (z kavgası olmaz).
        Vector3 back = camera.transform.forward * .02f;
        SetArcPoints(arc.ink, back);
        SetArcPoints(arc.gold, Vector3.zero);
        SetArcPoints(arc.shine, Vector3.up * .025f - back);
        arc.star.SetPositionAndRotation(b, camera.transform.rotation);
        arc.age = 0f;
        arc.root.SetActive(true);
        PoseArc(arc);
        liveArcs.Add(arc);
    }

    // LateUpdate: pause'da yaş ilerlemez; round dışında kalan yaylar temizlenir.
    private void SyncCritArcs()
    {
        if (liveArcs.Count == 0) return;
        if (RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) { ClearCritArcs(); return; }
        bool running = Time.timeScale > 0f && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        float delta = running ? Time.deltaTime : 0f;
        Camera camera = Camera.main;
        for (int i = liveArcs.Count - 1; i >= 0; i--)
        {
            GoldArc arc = liveArcs[i];
            arc.age += delta;
            if (arc.age >= ArcSeconds) { StoreArc(arc); liveArcs.RemoveAt(i); continue; }
            if (camera != null) arc.star.rotation = camera.transform.rotation;
            PoseArc(arc);
        }
    }

    private void ClearCritArcs()
    {
        foreach (GoldArc arc in liveArcs) StoreArc(arc);
        liveArcs.Clear();
    }

    private void DestroyCritArcAssets()
    {
        if (arcMaterial != null) Destroy(arcMaterial);
        if (arcStarMesh != null) Destroy(arcStarMesh);
    }

    // Kalınlık (1 - t)^.7 ile incelir; yay hedefe doğru kalınlaşır (yön okunur). Yıldız önce hafifçe büyür, sonra küçülür.
    private void PoseArc(GoldArc arc)
    {
        float f = Mathf.Pow(Mathf.Clamp01(1f - arc.age / ArcSeconds), .7f);
        SetArcWidth(arc.ink, .064f * f, .248f * f);
        SetArcWidth(arc.gold, .037f * f, .175f * f);
        SetArcWidth(arc.shine, .011f * f, .055f * f);
        float star = arc.age < .1f ? 1f + .12f * (arc.age / .1f) : 1.12f * Mathf.Sqrt(Mathf.Clamp01(1f - (arc.age - .1f) / .2f));
        arc.star.localScale = Vector3.one * star;
    }

    private void StoreArc(GoldArc arc)
    {
        if (arc.root != null) arc.root.SetActive(false);
        spareArcs.Add(arc);
    }

    private void SetArcPoints(LineRenderer line, Vector3 offset)
    {
        for (int i = 0; i < ArcPoints; i++) arcShifted[i] = arcPoints[i] + offset;
        line.positionCount = ArcPoints;
        line.SetPositions(arcShifted);
    }

    private static void SetArcWidth(LineRenderer line, float start, float end)
    {
        line.startWidth = start;
        line.endWidth = end;
    }

    private GoldArc BuildArc()
    {
        var root = new GameObject("Crit Arc");
        root.transform.SetParent(transform, false);
        var arc = new GoldArc
        {
            root = root,
            ink = ArcLine(root.transform, "Ink", ArcInk),
            gold = ArcLine(root.transform, "Gold", ArcGold),
            shine = ArcLine(root.transform, "Shine", ArcCream),
        };
        var star = new GameObject("Landing Star");
        star.transform.SetParent(root.transform, false);
        star.AddComponent<MeshFilter>().sharedMesh = arcStarMesh;
        var renderer = star.AddComponent<MeshRenderer>();
        Quiet(renderer);
        arc.star = star.transform;
        return arc;
    }

    private LineRenderer ArcLine(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        Quiet(line);
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.startColor = line.endColor = color;
        return line;
    }

    private void Quiet(Renderer renderer)
    {
        renderer.sharedMaterial = arcMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private bool EnsureArcAssets()
    {
        if (arcMaterial != null) return true;
        if (arcUnavailable) return false;
        Shader shader = Resources.Load<Shader>("ParticleVertexColor");
        if (shader == null || !shader.isSupported) { arcUnavailable = true; return false; }
        arcMaterial = new Material(shader) { name = "Crit Arc (runtime)" };
        arcStarMesh = BuildStarMesh();
        return true;
    }

    // Tek ağ, üç katman (yerel +z kameradan uzak): mürekkep 8 köşe, altın 8 köşe, krem merkez. Renk köşe rengindedir.
    private static Mesh BuildStarMesh()
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        void Layer(float outer, float inner, float depth, Color color)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, depth));
            colors.Add(color);
            for (int i = 0; i < 16; i++)
            {
                float radius = i % 2 == 0 ? outer : inner, angle = i * Mathf.PI / 8f + .2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, depth));
                colors.Add(color);
                triangles.Add(center);
                triangles.Add(center + 1 + i);
                triangles.Add(center + 1 + (i + 1) % 16);
            }
        }
        Layer(.40f, .18f, .02f, ArcInk);
        Layer(.32f, .14f, 0f, ArcGold);
        Layer(.10f, .10f, -.02f, ArcCream);
        var mesh = new Mesh { name = "Crit Arc Star (runtime)" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
