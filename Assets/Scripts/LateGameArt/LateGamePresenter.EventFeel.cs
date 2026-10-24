using System.Collections.Generic;
using UnityEngine;

// Kesilecek hedeflerin vuruş hissi (24 Ekim 2026, O-serisi; önce Diken Kuşatması).
// - Toon "BAM" patlaması (kullanıcı: beyaz yıldız fazla sade): düzensiz, hafif kıvrık sivri uçlar; sticker gölgesi + kalın mürekkep,
//   kırmızı-turuncu dış, yarım adım dönük sarı iç uçlar, dalgalı krem çekirdek ve parlama, çevrede mürekkep kıymıkları.
//   İki çizim karesi 24 kare/sn'de değişir (el çizimi kaynaması; sabit desen, Random yok). Vuruşta küçük ve kısa, kırılmada ve
//   kökün fırladığı anda büyük. Aynı anda birden çok patlama farklı açıda durur.
// - Bulut ve parçaların üstünde: ortografik kamerada bakış ekseninde kameraya doğru kaydırılır (ekrandaki yeri değişmez).
// - Ağlar çalışma anında bir kez kurulur (iki kare), presenter yok olurken silinir. Havuzlu: en çok EventStarCapacity; dolunca en
//   eskisi yeniden kullanılır ve sayılır. Karede yalnız transform ve hazır ağ ataması; bellek ayrılmaz.
// - Yaş yalnız Round'da ve timeScale > 0 iken ilerler; round dışında ve kapatılınca temizlenir.
// - Geri tepme yönü: son doğrudan saldırının merkezinden hedefe (yatay). Merkez hedefin üstündeyse kameradan uzağa.
public sealed partial class LateGamePresenter
{
    private const int EventStarCapacity = 8;
    private const float EventStarDepthBias = 4f, EventStarBoil = 24f;
    private static readonly Color BurstInk = new Color32(26, 19, 36, 255), BurstRed = new Color32(232, 69, 44, 255),
        BurstYellow = new Color32(255, 200, 61, 255), BurstCream = new Color32(255, 244, 214, 255), BurstShine = new Color32(255, 255, 250, 255);

    private sealed class EventStar
    {
        public Transform part;
        public MeshFilter filter;
        public Vector3 at;
        public float age, life, size, spin;
        public bool live;
    }

    private readonly EventStar[] eventStars = new EventStar[EventStarCapacity];
    private readonly Mesh[] eventBursts = new Mesh[2];
    private Transform eventStarRoot;
    private int eventStarNext;

    public int EventStarsShown { get; private set; }
    public int EventStarsReplaced { get; private set; }

    private static Vector3 RecoilFrom(Vector3 target)
    {
        Vector3 away = target - AttackFeed.LastCenter;
        away.y = 0f;
        if (away.sqrMagnitude >= .01f) return away;
        Camera camera = Camera.main;
        return camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : Vector3.forward;
    }

    // delay: patlama bu kadar sonra belirir (ör. kök fırladığı an).
    private void EventStarAt(Vector3 at, float size, float life, float delay = 0f)
    {
        if (!EnsureArcAssets() || !EnsureEventBursts()) return;
        int slot = -1;
        for (int i = 0; i < EventStarCapacity && slot < 0; i++) if (eventStars[i] == null || !eventStars[i].live) slot = i;
        if (slot < 0) { slot = eventStarNext; eventStarNext = (eventStarNext + 1) % EventStarCapacity; EventStarsReplaced++; }
        EventStar star = eventStars[slot];
        if (star == null)
        {
            Transform part = MeshPart(eventStarRoot, "Hit Burst", eventBursts[0], arcMaterial);
            star = eventStars[slot] = new EventStar { part = part, filter = part.GetComponent<MeshFilter>() };
        }
        star.at = at;
        star.size = size;
        star.life = Mathf.Max(.02f, life);
        star.age = -Mathf.Max(0f, delay);
        star.spin = slot * 37f;   // aynı anda birden çok patlama farklı açıda
        star.live = true;
        if (star.age >= 0f) PoseEventStar(star, Camera.main); else SetActive(star.part, false);
    }

    private void SyncEventStars(float delta)
    {
        Camera camera = Camera.main;
        int shown = 0;
        for (int i = 0; i < EventStarCapacity; i++)
        {
            EventStar star = eventStars[i];
            if (star == null || !star.live) continue;
            star.age += delta;
            if (star.age < 0f) continue;   // gecikmede
            if (star.age >= star.life) { star.live = false; SetActive(star.part, false); continue; }
            PoseEventStar(star, camera);
            shown++;
        }
        EventStarsShown = shown;
    }

    private void PoseEventStar(EventStar star, Camera camera)
    {
        if (camera == null) return;
        Transform view = camera.transform;
        float u = star.age / star.life;
        float scale = star.size * (u < .3f ? Mathf.Lerp(.55f, 1.15f, u / .3f) : Mathf.Lerp(1.15f, .7f, (u - .3f) / .7f));
        Mesh frame = eventBursts[(int)(star.age * EventStarBoil) & 1];
        if (star.filter.sharedMesh != frame) star.filter.sharedMesh = frame;
        star.part.SetPositionAndRotation(star.at - view.forward * EventStarDepthBias, view.rotation * Quaternion.AngleAxis(star.spin + 10f * u, Vector3.forward));
        star.part.localScale = Vector3.one * scale;
        SetActive(star.part, true);
    }

    private void ClearEventStars()
    {
        for (int i = 0; i < EventStarCapacity; i++)
            if (eventStars[i] != null) { eventStars[i].live = false; SetActive(eventStars[i].part, false); }
        EventStarsShown = 0;
    }

    private bool EnsureEventBursts()
    {
        if (eventStarRoot == null)
        {
            eventStarRoot = new GameObject("Event Hit Bursts").transform;
            eventStarRoot.SetParent(transform, false);
        }
        if (eventBursts[0] != null) return true;
        var builder = new BurstMeshBuilder();
        for (int k = 0; k < 2; k++) eventBursts[k] = builder.Build(k);
        return true;
    }

    private void DestroyEventFeelAssets()
    {
        for (int k = 0; k < eventBursts.Length; k++) { if (eventBursts[k] != null) Destroy(eventBursts[k]); eventBursts[k] = null; }
    }

    // Toon patlama ağı (kart uzayı: x sağ, y yukarı, z bakış; eksi z kameraya yakın). Yarıçap ~1. Kurulumda bir kez.
    private sealed class BurstMeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Color32> colors = new();
        private readonly List<int> triangles = new();
        // Uç uzunlukları (sabit desen); kare 1 bir adım kaydırılmış ve hafif farklı: kaynama.
        private static readonly float[] Tips = { 1f, .74f, .93f, .68f, .98f, .8f, .9f, .7f, 1f, .77f, .95f, .72f };

        public Mesh Build(int frame)
        {
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float shift = frame * 30f, wobble = frame == 0 ? 0f : .05f;
            Star(1f, .5f, shift, 6f, new Vector2(.05f, -.06f), .06f, BurstInk, wobble, frame);      // sticker gölgesi
            Star(1f, .5f, shift, 6f, Vector2.zero, .05f, BurstInk, wobble, frame);                   // mürekkep kenar
            Star(.85f, .42f, shift, 6f, Vector2.zero, .04f, BurstRed, wobble, frame);                // kırmızı-turuncu
            Star(.6f, .3f, shift + 15f, 9f, Vector2.zero, .03f, BurstYellow, wobble, frame + 1);     // sarı iç uçlar (yarım adım)
            Blob(.27f, Vector2.zero, .02f, BurstCream, frame);                                       // krem çekirdek
            Blob(.08f, new Vector2(-.09f, .09f), .01f, BurstShine, frame);                           // parlama
            for (int i = 0; i < 6; i++)                                                              // mürekkep kıymıkları
            {
                float a = i * 60f + 20f + shift * .5f, r = 1.14f + .1f * ((i + frame) % 3);
                Triangle(At(r, a - 4f), At(r, a + 4f), At(r + .16f, a + 1f), .05f, BurstInk);
            }
            var mesh = new Mesh { name = "Event Hit Burst " + frame + " (runtime)" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Vector2 At(float radius, float degrees) => new(radius * Mathf.Cos(degrees * Mathf.Deg2Rad), radius * Mathf.Sin(degrees * Mathf.Deg2Rad));
        private int Add(Vector2 p, float z, Color32 c) { vertices.Add(new Vector3(p.x, p.y, z)); colors.Add(c); return vertices.Count - 1; }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, float z, Color color)
        {
            Color32 linear = color.linear;
            triangles.Add(Add(a, z, linear)); triangles.Add(Add(b, z, linear)); triangles.Add(Add(c, z, linear));
        }

        // Sivri yıldız: 12 uç, uzunluklar desenden; uçlar 'skew' derece ileri kıvrık (toon dönüş hissi).
        private void Star(float tip, float valley, float shift, float skew, Vector2 offset, float z, Color color, float wobble, int pattern)
        {
            Color32 linear = color.linear;
            int center = Add(offset, z, linear), first = vertices.Count, n = Tips.Length;
            for (int i = 0; i < n; i++)
            {
                float a = shift + i * 360f / n, length = Tips[(i + pattern) % n] * (1f + (i % 2 == 0 ? wobble : -wobble));
                Add(offset + At(valley, a - 180f / n), z, linear);
                Add(offset + At(tip * length, a + skew), z, linear);
            }
            for (int i = 0; i < n * 2; i++) { triangles.Add(center); triangles.Add(first + i); triangles.Add(first + (i + 1) % (n * 2)); }
        }

        private void Blob(float radius, Vector2 offset, float z, Color color, int pattern, int segments = 18)
        {
            Color32 linear = color.linear;
            int center = Add(offset, z, linear), first = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = 360f * i / segments, r = radius * (1f + .08f * Mathf.Sin(a * Mathf.Deg2Rad * 4f + pattern * 1.3f));
                Add(offset + At(r, a), z, linear);
            }
            for (int i = 0; i < segments; i++) { triangles.Add(center); triangles.Add(first + i); triangles.Add(first + (i + 1) % segments); }
        }
    }
}
