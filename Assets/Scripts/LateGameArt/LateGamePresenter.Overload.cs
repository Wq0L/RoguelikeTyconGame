using System.Collections.Generic;
using UnityEngine;

// Aşırı Yükleme hazırlık rozeti (24 Ekim 2026, B5; eski krem çekirdek + amber zikzak işaretinin ve boşalma nabzının yerine).
// - Gerçekten kabul edilen her işte (OverloadEffect.TryPreparation) kaynak saksının üstünde toon "dolan tekrar oku" rozeti:
//   dalgalı kenarlı iki tonlu krem disk, mürekkep kontur ve sticker gölgesi, fuşya ok. Ok işin gerçek ilerlemesiyle ↻ olarak
//   tamamlanır. Tekrar gerçekten çalışınca (Overload cue) rozet kısa bir "pop" ile kıvılcım saçıp söner; iptalde sessizce kalkar.
// - Görünürlük (kullanıcı kararı B): rozet patlama bulutlarının ve hasar yazılarının üstünde. Kamera ortografik: rozet bakış
//   ekseninde kameraya doğru kaydırılır (ekrandaki yeri değişmez) ve derinlik yazar; arkasındaki bulut / yazı altında kalır.
// - Ağlar çalışma anında bir kez kurulur (ok dolumu 10°'lik basamaklar + pop; köşeler paylaşımlı, renk 4 bayt) ve presenter yok
//   olurken silinir. Kartlar havuzlu: iş yuvası başına bir rozet (OverloadEffect.PresentationCapacity), pop'lar sabit halkada
//   (OverloadPops; dolunca en eskisinin yerine yazılır, sayılır). Karede yalnız transform ve hazır ağ ataması; bellek ayrılmaz.
// - Konum işin anlık görüntüsüdür; saksı / bitki takip edilmez. Pause'da ilerleme gerçek saatten okunduğu için donar; pop yaşı
//   yalnız Round'da ve timeScale > 0 iken ilerler. Round dışında, ödül kalkınca ve kapatılınca temizlenir. Renkler sabit (Random yok).
public sealed partial class LateGamePresenter
{
    private const int OverloadPops = 16, OverloadFillSteps = 30;
    private const float OverloadSweep = 290f, OverloadBadgeLift = .7f, OverloadBadgeSize = .8f, OverloadDepthBias = 4f, OverloadPopSeconds = .14f;
    // Renkler (sRGB); ağa doğrusal yazılır (projede ağ köşe rengi dönüştürülmez).
    private static readonly Color OverloadInk = new Color32(26, 19, 36, 255), OverloadCream = new Color32(255, 244, 214, 255),
        OverloadCreamShade = new Color32(232, 212, 172, 255), OverloadTrack = new Color32(214, 200, 210, 255),
        OverloadFuchsia = new Color32(179, 21, 110, 255);

    private sealed class OverloadCard
    {
        public GameObject root;
        public MeshFilter filter;
    }

    private struct OverloadPop
    {
        public Vector3 at;
        public float age;
        public bool live;
    }

    private readonly OverloadCard[] overloadBadges = new OverloadCard[OverloadEffect.PresentationCapacity];
    private readonly OverloadCard[] overloadPopCards = new OverloadCard[OverloadPops];
    private readonly OverloadPop[] overloadPops = new OverloadPop[OverloadPops];
    private Mesh[] overloadFills;
    private Mesh overloadPopMesh;
    private Transform overloadRoot;
    private OverloadEffect overloadOwner;
    private int overloadPopFrame = -1, overloadPopNext;

    public int OverloadBadgesShown { get; private set; }
    public int OverloadPopsShown { get; private set; }
    public int OverloadPopsReplaced { get; private set; }

    private void ClearOverload()
    {
        foreach (OverloadCard card in overloadBadges) HideOverloadCard(card);
        foreach (OverloadCard card in overloadPopCards) HideOverloadCard(card);
        for (int i = 0; i < OverloadPops; i++) overloadPops[i].live = false;
        OverloadBadgesShown = OverloadPopsShown = 0;
        overloadOwner = null;
    }

    private void SyncOverload()
    {
        var source = RunEffects.Instance != null ? RunEffects.Instance.Find<OverloadEffect>() : null;
        if (source == null || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) { ClearOverload(); return; }
        if (overloadOwner != source) { ClearOverload(); overloadOwner = source; }
        Camera camera = Camera.main;
        if (camera == null || !EnsureOverloadAssets()) return;
        Transform view = camera.transform;
        Vector3 pull = Vector3.up * OverloadBadgeLift - view.forward * OverloadDepthBias;

        int shown = 0;
        for (int i = 0; i < overloadBadges.Length; i++)
        {
            if (!source.TryPreparation(i, out Vector3 position, out float progress)) { HideOverloadCard(overloadBadges[i]); continue; }
            OverloadCard badge = overloadBadges[i] ??= BuildOverloadCard();
            int step = Mathf.Clamp(Mathf.RoundToInt(OverloadSmooth(progress) * (OverloadFillSteps - 1)), 0, OverloadFillSteps - 1);
            if (badge.filter.sharedMesh != overloadFills[step]) badge.filter.sharedMesh = overloadFills[step];
            float scale = OverloadBack(progress / .2f) * (1f + .08f * Mathf.Sin(progress * Mathf.PI * 4f));
            badge.root.transform.SetPositionAndRotation(position + pull, view.rotation);
            badge.root.transform.localScale = Vector3.one * (OverloadBadgeSize * scale);
            if (!badge.root.activeSelf) badge.root.SetActive(true);
            shown++;
        }
        OverloadBadgesShown = shown;

        // Pop yaşı karede bir kez ilerler (SyncOverload cue'larda da çağrılır).
        if (overloadPopFrame != Time.frameCount)
        {
            overloadPopFrame = Time.frameCount;
            bool running = Time.timeScale > 0f && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
            float delta = running ? Time.deltaTime : 0f;
            for (int i = 0; i < OverloadPops; i++) if (overloadPops[i].live && (overloadPops[i].age += delta) >= OverloadPopSeconds) overloadPops[i].live = false;
        }
        int pops = 0;
        for (int i = 0; i < OverloadPops; i++)
        {
            if (!overloadPops[i].live) { HideOverloadCard(overloadPopCards[i]); continue; }
            OverloadCard card = overloadPopCards[i] ??= BuildOverloadCard();
            if (card.filter.sharedMesh != overloadPopMesh) card.filter.sharedMesh = overloadPopMesh;
            float u = overloadPops[i].age / OverloadPopSeconds;
            float scale = u < .3f ? 1f + .35f * OverloadSmooth(u / .3f) : 1.35f * (1f - OverloadSmooth((u - .3f) / .7f));
            card.root.transform.SetPositionAndRotation(overloadPops[i].at + pull, view.rotation * Quaternion.AngleAxis(-160f * u, Vector3.forward));
            card.root.transform.localScale = Vector3.one * (OverloadBadgeSize * Mathf.Max(0f, scale));
            if (!card.root.activeSelf) card.root.SetActive(true);
            pops++;
        }
        OverloadPopsShown = pops;
    }

    private void OverloadReleased(RewardCue cue)
    {
        // Gerçek zamanlayıcı işini başarı bildiriminden önce kaldırdı: önce rozet kalkar, aynı yerde pop başlar.
        int slot = -1;
        for (int i = 0; i < OverloadPops && slot < 0; i++) if (!overloadPops[i].live) slot = i;
        if (slot < 0) { slot = overloadPopNext; overloadPopNext = (overloadPopNext + 1) % OverloadPops; OverloadPopsReplaced++; }
        overloadPops[slot] = new OverloadPop { at = cue.From, age = 0f, live = true };
        SyncOverload();
        Play(LateGameSound.Overload, LateGameVariant.Overload);
    }

    private static void HideOverloadCard(OverloadCard card)
    {
        if (card != null && card.root != null && card.root.activeSelf) card.root.SetActive(false);
    }

    private OverloadCard BuildOverloadCard()
    {
        var go = new GameObject("Overload Badge");
        go.transform.SetParent(overloadRoot, false);
        var card = new OverloadCard { root = go, filter = go.AddComponent<MeshFilter>() };
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = arcMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        go.SetActive(false);
        return card;
    }

    private static float OverloadSmooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float OverloadBack(float x) { x = Mathf.Clamp01(x) - 1f; return 1f + x * x * (3.2f * x + 2.2f); }

    // ---- ağlar (bir kez; kart uzayı: x ekranda sağ, y yukarı, z bakış yönü, eksi z kameraya yakın) ----
    private bool EnsureOverloadAssets()
    {
        if (!EnsureArcAssets()) return false;
        if (overloadRoot == null)
        {
            overloadRoot = new GameObject("Overload Badges").transform;
            overloadRoot.SetParent(transform, false);
        }
        if (overloadFills != null) return true;
        var builder = new OverloadMeshBuilder();
        overloadFills = new Mesh[OverloadFillSteps];
        for (int k = 0; k < OverloadFillSteps; k++)
        {
            builder.Clear();
            builder.Badge();
            builder.Arrow(OverloadSweep * k / (OverloadFillSteps - 1));
            overloadFills[k] = builder.Build("Overload Badge " + k + " (runtime)");
        }
        builder.Clear();
        builder.Badge();
        builder.Arrow(OverloadSweep);
        builder.Sparks();
        overloadPopMesh = builder.Build("Overload Badge Pop (runtime)");
        return true;
    }

    private void DestroyOverloadAssets()
    {
        if (overloadFills != null) foreach (Mesh mesh in overloadFills) if (mesh != null) Destroy(mesh);
        overloadFills = null;
        if (overloadPopMesh != null) Destroy(overloadPopMesh);
    }

    // Kurulumda bir kez kullanılır; listeler kurulum bitince bırakılır.
    private sealed class OverloadMeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Color32> colors = new();
        private readonly List<int> triangles = new();

        public void Clear() { vertices.Clear(); colors.Clear(); triangles.Clear(); }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);   // CPU kopyası bırakılır: yalnız çizim
            return mesh;
        }

        private int Add(Vector2 p, float z, Color32 color) { vertices.Add(new Vector3(p.x, p.y, z)); colors.Add(color); return vertices.Count - 1; }
        private static Color32 Linear(Color color) => color.linear;
        private static Vector2 At(float radius, float degrees) => new(radius * Mathf.Cos(degrees * Mathf.Deg2Rad), radius * Mathf.Sin(degrees * Mathf.Deg2Rad));
        // El yapımı düzensizlik: sabit dalga (her rozet aynı).
        private static float Wobble(float degrees) => 1f + .035f * Mathf.Sin(degrees * Mathf.Deg2Rad * 5f + .7f) + .02f * Mathf.Sin(degrees * Mathf.Deg2Rad * 9f + 2.1f);

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, float z, Color color)
        {
            Color32 linear = Linear(color);
            triangles.Add(Add(a, z, linear)); triangles.Add(Add(b, z, linear)); triangles.Add(Add(c, z, linear));
        }

        private void Blob(float radius, Vector2 offset, float z, Color color, int segments = 40)
        {
            Color32 linear = Linear(color);
            int center = Add(offset, z, linear), first = vertices.Count;
            for (int i = 0; i < segments; i++) { float a = 360f * i / segments; Add(offset + At(radius * Wobble(a), a), z, linear); }
            for (int i = 0; i < segments; i++) { triangles.Add(center); triangles.Add(first + i); triangles.Add(first + (i + 1) % segments); }
        }

        private void Band(float inner, float outer, float from, float to, int segments, float z, Color color)
        {
            Color32 linear = Linear(color);
            int first = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments);
                Add(At(inner, a), z, linear); Add(At(outer, a), z, linear);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 3);
                triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 2);
            }
        }

        // Sticker gölgesi, mürekkep kenar, iki tonlu krem disk, okun gideceği soluk iz.
        public void Badge()
        {
            Blob(.6f, new Vector2(.05f, -.06f), .05f, OverloadInk);
            Blob(.58f, Vector2.zero, .04f, OverloadInk);
            Blob(.5f, Vector2.zero, .03f, OverloadCreamShade);
            Blob(.45f, new Vector2(-.035f, .04f), .025f, OverloadCream);
            Band(.3f, .33f, 0f, 360f, 40, .02f, OverloadTrack);
        }

        // Saat yönünde ok (90°'den 'sweep' derece): mürekkep, fuşya, koyu iç şerit (toon gölge), krem parlama; uçta ok başı.
        public void Arrow(float sweep)
        {
            const float inner = .22f, outer = .4f, mid = .31f;
            sweep = Mathf.Max(sweep, 10f);
            float end = 90f - sweep;
            int segments = Mathf.Max(3, Mathf.CeilToInt(sweep / 10f));
            Color shade = OverloadFuchsia * .62f + OverloadInk * .38f;
            Band(inner - .05f, outer + .05f, 93f, end, segments, 0f, OverloadInk); Head(end, .05f, 0f, OverloadInk);
            Band(inner, outer, 90f, end, segments, -.01f, OverloadFuchsia); Head(end, 0f, -.01f, OverloadFuchsia);
            Band(inner, mid - .02f, 90f, end, segments, -.02f, shade);
            Band(outer - .045f, outer - .02f, 82f, Mathf.Max(end, 90f - Mathf.Min(sweep, 70f)), 6, -.03f, OverloadCream);
        }

        private void Head(float end, float grow, float z, Color color)
        {
            const float inner = .22f, outer = .4f, mid = .31f;
            Triangle(At(inner - .1f - grow * 1.4f, end + grow * 20f), At(outer + .1f + grow * 1.4f, end + grow * 20f), At(mid, end - 32f - grow * 70f), z, color);
        }

        // Pop kıvılcımları: sekiz kısa mürekkep + krem diken.
        public void Sparks()
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 10f;
                Triangle(At(.66f, a - 6f), At(.66f, a + 6f), At(.95f, a), .06f, OverloadInk);
                Triangle(At(.7f, a - 3.5f), At(.7f, a + 3.5f), At(.88f, a), .055f, OverloadCream);
            }
        }
    }
}
