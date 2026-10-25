using System.Collections.Generic;
using UnityEngine;

// Diken Kuşatması çıkış patlaması (24 Ekim 2026; Blender çalışması Tools/Art/ThornBurst, kullanıcı onayı). Yalnız Diken'in;
// her kaprisin kendi çıkışı var (İstilacı Filiz: LateGamePresenter.SproutBurst.cs). Düz "BAM" kartının ve
// eski toprak saçılmasının yerine; kökün fırlama hareketi (ThornBurst eğrisi) aynen kalır, bu dünya tepkisidir.
// - Bekleme (hazırlığın son ThornBlastLead sn'si): kök hücresinde küçük kesekler zıplar, kenardan toz pofları çıkar.
// - Patlama (Emerged cue anı, t = 0): çizgi roman toprak patlaması kartı (24 kare/sn basamaklı: 1 → 1,18 → 1,05 → ,62 → yok),
//   dışa açılan mürekkep hız çizgileri, balistik yayla saçılıp bir kez seken ve batan 9 toprak keseği, kabarıp yükselen ve yavaşça
//   çözülen 7 toz bulutu, kısa süre kalıp batan krater (yanık leke + kırık toprak).
// - Kartlar her karede kameraya döner. Uçuş değerleri sabit tablodan (ThornBurstArtSO; Random yok).
// - Havuzlu: en çok ThornBlastCapacity örnek; dolunca en eskisi kesilir ve sayılır. Diken hazırlanırken iki örnek önceden kurulur
//   (patlama anında kurulum takılması olmasın). Ağlar asset; karede yalnız transform, bellek ayrılmaz.
// - Yaş yalnız Round'da ve timeScale > 0 iken ilerler; olay temizlenince, round dışında ve kapatılınca örnekler havuza döner.
public sealed partial class LateGamePresenter
{
    private const int ThornBlastCapacity = 4, ThornBlastHops = 4, ThornBlastPuffs = 3;
    private const float ThornBlastLead = .38f, ThornBlastLife = 1.4f, ThornBlastFps = 24f;
    // Toz: tepe süresi, bulut başına kayma, dağılma süresi, yükselme süresi; bulutun kendi gecikmesi en çok .06 sn
    // (son bulut ≈ .06 + .16 + 6 × .02 + .28 = .62 sn; kök .55 sn'de oturur).
    private const float ThornDustHold = .16f, ThornDustStagger = .02f, ThornDustFade = .28f, ThornDustRise = .45f;
    public const float ThornDustEnd = .06f + ThornDustHold + 6 * ThornDustStagger + ThornDustFade;
    private static readonly float[] ThornBlastFrames = { 1f, 1.18f, 1.05f, .62f };

    private sealed class ThornBlast
    {
        public GameObject root;
        public Transform blast, lines, crater;
        public Transform[] clods, clouds;
        public readonly Transform[] hops = new Transform[ThornBlastHops];
        public readonly Transform[] puffs = new Transform[ThornBlastPuffs];
        public GridPosition cell;
        public Vector3 ground;
        public float age;   // patlamaya göre (sn); eksi = bekleme
        public bool live, burst;
    }

    private readonly ThornBlast[] thornBlasts = new ThornBlast[ThornBlastCapacity];
    private ThornBurstArtSO thornBurstArt;
    private bool thornBurstArtLoaded;
    private Transform thornBlastRoot;
    private int thornBlastNext;

    public int ThornBlastsShown { get; private set; }
    public int ThornBlastsCut { get; private set; }

    private ThornBurstArtSO ThornBurstArt
    {
        get
        {
            if (!thornBurstArtLoaded)
            {
                thornBurstArt = Resources.Load<ThornBurstArtSO>(ThornBurstArtSO.ResourcePath);
                thornBurstArtLoaded = true;
                if (thornBurstArt == null || !thornBurstArt.Valid) Debug.LogWarning("ThornBurstArt eksik; Diken çıkış patlaması çizilmeyecek (oynanış etkilenmez).");
            }
            return thornBurstArt != null && thornBurstArt.Valid ? thornBurstArt : null;
        }
    }

    // Diken hazırlanırken: iki örnek önceden kurulur.
    private void PrewarmThornBlasts()
    {
        if (ThornBurstArt == null || !EnsureArcAssets()) return;
        for (int i = 0; i < 2; i++) if (thornBlasts[i] == null) thornBlasts[i] = BuildThornBlast(ThornBurstArt);
    }

    // Hazırlığın son ThornBlastLead sn'sinde kök hücresinde bekleme animasyonu başlar (aynı hücre için bir kez).
    private void ThornBlastLeadIn(FieldEvent source, GridSystem grid)
    {
        if (source == null || source.Kind != FieldEventKind.ThornSiege || source.Phase != FieldEventPhase.Preparing) return;
        float remaining = source.PrepareRemaining;
        if (remaining > ThornBlastLead) return;
        for (int i = 0; i < source.Targets.Count; i++)
        {
            GridPosition cell = source.Targets[i].Cell;
            if (FindThornBlast(cell) != null) continue;
            ThornBlast blast = StartThornBlast(cell, SoilTop(grid, cell));
            if (blast != null) blast.age = -remaining;
        }
    }

    // Kökün fırladığı an (Emerged cue): bekleme sürüyorsa aynı örnek patlamaya geçer, yoksa doğrudan patlamayla başlar.
    private void ThornBlastBurst(GridPosition cell, Vector3 ground)
    {
        ThornBlast blast = FindThornBlast(cell) ?? StartThornBlast(cell, ground);
        if (blast == null) return;
        blast.age = 0f;
        blast.burst = true;
        Camera camera = Camera.main;
        if (camera != null) PoseThornBlast(blast, camera.transform);
    }

    // Patlamanın oturduğu yüzey: saksılı hücrede saksı toprağı (bu hücrenin bitki noktası), saksısızda zemin (Top).
    // Cue / bekleme başında bir kez; liste önbellekli, bellek ayrılmaz.
    private readonly List<PlantSpawner> soilSpawners = new();
    private Vector3 SoilTop(GridSystem grid, GridPosition cell)
    {
        Vector3 at = Top(grid, cell);
        GridObject go = grid.GetGridObject(cell);
        PlanterBrain planter = go != null ? go.GetPlanterBrain() : null;
        if (planter == null || !planter.IsPlaced) return at;
        planter.GetComponentsInChildren(true, soilSpawners);
        for (int i = 0; i < soilSpawners.Count; i++)
            if (soilSpawners[i].GridObject == go) { at.y = soilSpawners[i].transform.position.y + .01f; break; }
        soilSpawners.Clear();
        return at;
    }

    private ThornBlast FindThornBlast(GridPosition cell)
    {
        for (int i = 0; i < ThornBlastCapacity; i++)
        {
            ThornBlast blast = thornBlasts[i];
            if (blast != null && blast.live && blast.cell.x == cell.x && blast.cell.z == cell.z) return blast;
        }
        return null;
    }

    private ThornBlast StartThornBlast(GridPosition cell, Vector3 ground)
    {
        ThornBurstArtSO art = ThornBurstArt;
        if (art == null || !EnsureArcAssets()) return null;
        int slot = -1;
        for (int i = 0; i < ThornBlastCapacity && slot < 0; i++) if (thornBlasts[i] == null || !thornBlasts[i].live) slot = i;
        if (slot < 0) { slot = thornBlastNext; thornBlastNext = (thornBlastNext + 1) % ThornBlastCapacity; ThornBlastsCut++; }
        ThornBlast blast = thornBlasts[slot] ??= BuildThornBlast(art);
        blast.cell = cell;
        blast.ground = ground;
        blast.age = 0f;
        blast.burst = false;
        blast.live = true;
        blast.root.SetActive(true);
        return blast;
    }

    private void SyncThornBlasts(float delta)
    {
        Camera camera = Camera.main;
        int shown = 0;
        for (int i = 0; i < ThornBlastCapacity; i++)
        {
            ThornBlast blast = thornBlasts[i];
            if (blast == null || !blast.live) continue;
            blast.age += delta;
            // Bekleme patlamaya bağlanmadan süresi dolduysa (ör. olay kesildi) ya da patlama bittiyse havuza döner.
            if ((!blast.burst && blast.age > .3f) || blast.age >= ThornBlastLife) { StoreThornBlast(blast); continue; }
            if (camera != null) PoseThornBlast(blast, camera.transform);
            shown++;
        }
        ThornBlastsShown = shown;
    }

    private void StoreThornBlast(ThornBlast blast)
    {
        blast.live = false;
        if (blast.root != null && blast.root.activeSelf) blast.root.SetActive(false);
    }

    private void ClearThornBlasts()
    {
        for (int i = 0; i < ThornBlastCapacity; i++) if (thornBlasts[i] != null) StoreThornBlast(thornBlasts[i]);
        ThornBlastsShown = 0;
    }

    private void PoseThornBlast(ThornBlast blast, Transform view)
    {
        ThornBurstArtSO art = ThornBurstArt;
        if (art == null) return;
        float t = blast.burst ? blast.age : Mathf.Min(blast.age, -1e-4f);   // bekleme, patlama cue'su gelmeden patlamaya geçmez
        Quaternion card = view.rotation;
        Vector3 ground = blast.ground, right = view.right, up = Vector3.up, screenUp = view.up;

        // Bekleme: zıplayan küçük kesekler, kenar pofları.
        float lead = Mathf.Clamp01(1f + t / ThornBlastLead);
        for (int k = 0; k < ThornBlastHops; k++)
        {
            Transform hop = blast.hops[k];
            bool on = t < 0f && lead > .3f;
            SetActive(hop, on);
            if (!on) continue;
            float angle = k * 2.1f, phase = Mathf.Repeat(t * ThornBlastFps + k * 2f, 4f) / 4f;
            hop.position = ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * .35f + up * (.32f + .12f * lead * Mathf.Sin(phase * Mathf.PI));
            hop.localScale = Vector3.one * (.07f / .12f);
        }
        for (int k = 0; k < ThornBlastPuffs; k++)
        {
            Transform puff = blast.puffs[k];
            float u = (lead - .4f - .12f * k) / .45f;
            bool on = t < 0f && u >= 0f && u <= 1f;
            SetActive(puff, on);
            if (!on) continue;
            float angle = k * 2.6f + .4f;
            puff.SetPositionAndRotation(ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * .62f + up * (.15f + .2f * u), card);
            puff.localScale = Vector3.one * (.16f * (.6f + .5f * Mathf.Sin(u * Mathf.PI)));
        }

        // Patlama kartı: 24 kare/sn basamaklı.
        int frame = t < 0f ? -1 : (int)(t * ThornBlastFps);
        bool blastOn = frame >= 0 && frame < ThornBlastFrames.Length;
        SetActive(blast.blast, blastOn);
        if (blastOn)
        {
            blast.blast.SetPositionAndRotation(ground + up * (.35f + .12f * frame), card);
            blast.blast.localScale = Vector3.one * ThornBlastFrames[frame];
        }
        float lineU = t * ThornBlastFps / 3f;
        bool linesOn = t >= 0f && lineU <= 1f;
        SetActive(blast.lines, linesOn);
        if (linesOn)
        {
            blast.lines.SetPositionAndRotation(ground + up * .4f, card);
            blast.lines.localScale = Vector3.one * (1f + .6f * lineU);
        }

        // Kesekler: balistik yay, bir sekme, sonra batar.
        for (int k = 0; k < blast.clods.Length; k++)
        {
            Transform clod = blast.clods[k];
            ThornBurstArtSO.ClodFlight f = art.clodFlights[k];
            float sink = 1f - GreedSmooth((t - .7f) / .25f);
            bool on = t >= 0f && sink > .002f;
            SetActive(clod, on);
            if (!on) continue;
            var d = new Vector3(f.direction.x, 0f, f.direction.y);
            float flight = .42f * f.rise / 1.6f, height, reach;
            if (t < flight) { float u = t / flight; reach = f.distance * u; height = .3f + f.rise * 4f * u * (1f - u); }
            else if (t < flight + .18f) { float u = (t - flight) / .18f; reach = f.distance + .25f * u; height = .3f + .35f * 4f * u * (1f - u); }
            else { reach = f.distance + .25f; height = .3f; }
            clod.SetPositionAndRotation(ground + d * reach + up * height,
                Quaternion.Euler(t * f.spin * Mathf.Rad2Deg, t * f.spin * .7f * Mathf.Rad2Deg, t * f.spin * .4f * Mathf.Rad2Deg));
            clod.localScale = Vector3.one * (f.size * sink);
        }

        // Toz bulutları: hızla kabarır, kısa tepe yapar, yükselerek hızla dağılır (kullanıcı: kök çıktıktan sonra kalmasın;
        // son bulut ~ThornDustEnd sn'de, kökün oturduğu sıralarda biter).
        for (int k = 0; k < blast.clouds.Length; k++)
        {
            Transform cloud = blast.clouds[k];
            ThornBurstArtSO.CloudPuff p = art.cloudPuffs[k];
            float u = t - p.delay * .03f;
            float grow = GreedEaseOut(u / .12f), fade = 1f - GreedSmooth((u - ThornDustHold - ThornDustStagger * k) / ThornDustFade);
            float size = (.35f + .75f * grow) * fade * p.size;
            bool on = u >= 0f && size > .002f;
            SetActive(cloud, on);
            if (!on) continue;
            Vector3 side = right * p.screen.x + screenUp * p.screen.y;
            cloud.SetPositionAndRotation(ground + side * (p.reach * (.6f + .5f * grow)) + up * (.35f + p.rise * GreedEaseOut(u / ThornDustRise)), card);
            cloud.localScale = Vector3.one * size;
        }

        // Krater: patlamada belirir, sonra batar.
        float crater = t < 0f ? 0f : 1f - GreedSmooth((t - 1f) / .3f);
        SetActive(blast.crater, crater > .002f);
        if (crater > .002f)
        {
            blast.crater.position = ground;
            blast.crater.localScale = Vector3.one * crater;
        }
    }

    // ---- kurulum (havuz örneği başına bir kez) ----
    private ThornBlast BuildThornBlast(ThornBurstArtSO art)
    {
        if (thornBlastRoot == null)
        {
            thornBlastRoot = new GameObject("Thorn Bursts").transform;
            thornBlastRoot.SetParent(transform, false);
        }
        var root = new GameObject("Thorn Burst");
        root.transform.SetParent(thornBlastRoot, false);
        var blast = new ThornBlast { root = root };
        var lit = new[] { art.lit, art.ink };
        var flat = new[] { arcMaterial };
        blast.blast = GreedPart(root.transform, "Blast", art.blast, flat);
        blast.lines = GreedPart(root.transform, "Speed Lines", art.lines, flat);
        blast.clods = new Transform[art.clodFlights.Length];
        for (int k = 0; k < blast.clods.Length; k++)
            blast.clods[k] = GreedPart(root.transform, "Clod " + k, art.clods[Mathf.Clamp(art.clodFlights[k].variant, 0, art.clods.Length - 1)], lit);
        blast.clouds = new Transform[art.cloudPuffs.Length];
        for (int k = 0; k < blast.clouds.Length; k++)
            blast.clouds[k] = GreedPart(root.transform, "Dust " + k, art.clouds[Mathf.Clamp(art.cloudPuffs[k].variant, 0, art.clouds.Length - 1)], flat);
        for (int k = 0; k < ThornBlastHops; k++) blast.hops[k] = GreedPart(root.transform, "Hop " + k, art.clods[k % art.clods.Length], lit);
        for (int k = 0; k < ThornBlastPuffs; k++) blast.puffs[k] = GreedPart(root.transform, "Edge Puff " + k, art.clouds[k % art.clouds.Length], flat);
        blast.crater = GreedPart(root.transform, "Crater", art.craterLit, lit);
        GreedPart(blast.crater, "Scorch", art.craterScorch, flat);
        root.SetActive(false);
        return blast;
    }
}
