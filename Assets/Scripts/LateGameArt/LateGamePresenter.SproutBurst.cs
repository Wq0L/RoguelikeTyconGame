using UnityEngine;

// İstilacı Filiz kişiliği (25 Ekim 2026; Blender çalışması Tools/Art/SproutBurst, kullanıcı onayı). Diken'den ayrı dil:
// sinsi, yayılan, canlı — turkuaz + pembe, kıvrımlı / yuvarlak; toprak patlaması, kesek, ateş yok.
// - Bloom (ana filiz): hazırlığın son SproutBloomLead sn'sinde yerde sarmaşık çatlaklar dışa uzar; çıkışta dört dal kamçı gibi
//   fırlayıp ucundan kıvrılır, ana filiz dönerek yükselir (Item.spiral), tomurcuk şişip titrer, SproutPopAt'ta yapraklar yıldız
//   gibi açılır (LateGamePetals snap) ve bir an sonra açıklıktan spor bulutu yelpaze gibi çıkıp hızla söner, polen süzülür;
//   dallar kıvrılıp toprağa çekilir.
// - Spread: yeni hücreye kaynak hücreden yerde sürünen bir dal uzanır (yön gösterir), ucunda küçük pof; hücrenin kalıcı işareti
//   pembe sarmaşık halkasıdır (SproutPiece, dal ulaşınca açılır); geçiş dalı sonra söner.
// - Sap (vuruş): turkuaz-yeşil özsu lekesi, damlalar, kopan yapraklar; darbeden uzağa. Wilt (kırılma): büyük özsu + spor bulutu.
// - Ölüm dalgası (25 Ekim, kullanıcı: kesince yok oluş kopuk): ana filiz kurur (kahverengi-gri) ve darbeden uzağa devrilip çöker;
//   pembe halkalar ana filize yakından uzağa sırayla (SproutDeathStart + SproutDeathStep × uzaklık) kurur, bir kez kıvranır ve
//   ufalanır (Crumble: kuru yaprak kırıntıları + toz). Yalnız görsel: yavaşlatma son vuruşta anında kalkar.
// - Kartlar her karede kameraya döner; değerler sabit tablolardan (Random yok). Havuzlu: tür başına sabit kapasite, dolunca en eski
//   kesilir ve sayılır; parçalar örnek başına bir kez kurulur, karede yalnız transform. Yaş yalnız Round'da, timeScale > 0 iken.
public sealed partial class LateGamePresenter
{
    private enum SproutFxKind { Bloom, Spread, Sap, Wilt, Crumble }
    public const float SproutPopAt = .54f;
    private const float SproutBloomLead = .45f, SproutBloomLife = 1.25f, SproutVineSeconds = .35f, SproutSpreadLife = .85f,
        SproutSapLife = .45f, SproutWiltLife = .55f, SproutHeadHeight = 1.35f, SproutRiseSeconds = .72f;
    // Ölüm dalgası: ilk hücrenin gecikmesi, hücre başına adım, kuruma süresi / miktarı, ana filizin düşüşü, ufalanma ömrü.
    // 25 Ekim kullanıcı: "bir tık hızlı, hemen bitiyor" → hepsi ~%30 yavaş (eskisi .25 / .12 / .14 / .2 / .75 / halka ×1 / ufalanma .5).
    private const float SproutDeathStart = .32f, SproutDeathStep = .16f, SproutWitherLead = .2f, SproutWitherAmount = .75f,
        SproutMainWither = .28f, SproutDeathFall = 1f, SproutRingFall = 1.3f, SproutToppleDegrees = 72f, SproutCrumbleLife = .65f;
    private const int SproutFlakes = 4, SproutDust = 2;
    // Oyun ölçüsü (Blender çalışmasına göre): çatlaklar saksı kenarının üstünde (yoksa kenar örter), pof ve polen oyun mesafesinde
    // okunsun diye biraz büyük.
    private const float SproutCrackLift = .05f, SproutPuffSize = .27f, SproutPuffReach = .85f, SproutPollenSize = .05f;
    private const int SproutPuffs = 7, SproutPollen = 10, SproutSpreadPuffs = 4, SproutDrops = 6, SproutShreds = 3;
    private static readonly float[] SproutPuffVary = { 1f, .68f, .86f, .6f, .95f, .72f, .8f };
    private static readonly float[] SproutSplatFrames = { .55f, .8f, .7f, .4f };

    private sealed class SproutFx
    {
        public SproutFxKind kind;
        public GameObject root;
        public Transform ground;   // yerdeki parçalar (çatlak, dal, sürünen dal): hücre boyu ölçeği
        public Transform[] cracks, tendrilRoots, tendrils, vine, puffs, pollen, drops, shreds;
        public Transform splat;
        public Vector3 at, direction;
        public GridPosition cell;
        public float age, scale = 1f, size = 1f;
        public bool live, started;
    }

    private readonly SproutFx[] sproutBlooms = new SproutFx[2], sproutSpreads = new SproutFx[4], sproutSaps = new SproutFx[4], sproutWilts = new SproutFx[2],
        sproutCrumbles = new SproutFx[8];
    private readonly int[] sproutNext = new int[5];
    private static MaterialPropertyBlock sproutDeadBlock;
    private SproutBurstArtSO sproutBurstArt;
    private bool sproutBurstArtLoaded;
    private Transform sproutFxRoot;

    public int SproutFxShown { get; private set; }
    public int SproutFxCut { get; private set; }

    private SproutBurstArtSO SproutBurstArt
    {
        get
        {
            if (!sproutBurstArtLoaded)
            {
                sproutBurstArt = Resources.Load<SproutBurstArtSO>(SproutBurstArtSO.ResourcePath);
                sproutBurstArtLoaded = true;
                if (sproutBurstArt == null || !sproutBurstArt.Valid) Debug.LogWarning("SproutBurstArt eksik; İstilacı Filiz efektleri çizilmeyecek (oynanış etkilenmez).");
            }
            return sproutBurstArt != null && sproutBurstArt.Valid ? sproutBurstArt : null;
        }
    }

    private SproutFx[] SproutPool(SproutFxKind kind) => kind switch
    {
        SproutFxKind.Bloom => sproutBlooms, SproutFxKind.Spread => sproutSpreads, SproutFxKind.Sap => sproutSaps,
        SproutFxKind.Wilt => sproutWilts, _ => sproutCrumbles,
    };

    // Filiz hazırlanırken: bir bloom ve bir spread örneği önceden kurulur.
    private void PrewarmSproutFx()
    {
        SproutBurstArtSO art = SproutBurstArt;
        if (art == null || !EnsureArcAssets()) return;
        sproutBlooms[0] ??= BuildSproutFx(SproutFxKind.Bloom, art);
        sproutSpreads[0] ??= BuildSproutFx(SproutFxKind.Spread, art);
    }

    private SproutFx StartSproutFx(SproutFxKind kind, Vector3 at)
    {
        SproutBurstArtSO art = SproutBurstArt;
        if (art == null || !EnsureArcAssets()) return null;
        SproutFx[] pool = SproutPool(kind);
        int slot = -1;
        for (int i = 0; i < pool.Length && slot < 0; i++) if (pool[i] == null || !pool[i].live) slot = i;
        if (slot < 0) { slot = sproutNext[(int)kind]; sproutNext[(int)kind] = (slot + 1) % pool.Length; SproutFxCut++; }
        SproutFx fx = pool[slot] ??= BuildSproutFx(kind, art);
        fx.at = at;
        fx.age = 0f;
        fx.size = 1f;
        fx.started = true;
        fx.direction = Vector3.forward;
        fx.scale = Mathf.Max(.1f, FieldCells.CellSize * .5f);
        fx.live = true;
        if (!fx.root.activeSelf) fx.root.SetActive(true);
        return fx;
    }

    private SproutFx FindSproutBloom(GridPosition cell)
    {
        foreach (SproutFx fx in sproutBlooms) if (fx != null && fx.live && fx.cell.x == cell.x && fx.cell.z == cell.z) return fx;
        return null;
    }

    // Hazırlığın son SproutBloomLead sn'si: çatlaklar (çıkış cue'sundan önce) — aynı hücre için bir kez.
    private void SproutBloomLeadIn(FieldEvent source, GridSystem grid)
    {
        if (source == null || source.Kind != FieldEventKind.InvasiveSprout || source.Phase != FieldEventPhase.Preparing) return;
        float remaining = source.PrepareRemaining;
        if (remaining > SproutBloomLead) return;
        for (int i = 0; i < source.Targets.Count; i++)
        {
            GridPosition cell = source.Targets[i].Cell;
            if (FindSproutBloom(cell) != null) continue;
            SproutFx fx = StartSproutFx(SproutFxKind.Bloom, SoilTop(grid, cell));
            if (fx == null) continue;
            fx.cell = cell;
            fx.age = -remaining;
            fx.started = false;
        }
    }

    // Ana filizin çıkış anı (Emerged cue).
    private void SproutBloomStart(GridPosition cell, Vector3 ground)
    {
        SproutFx fx = FindSproutBloom(cell) ?? StartSproutFx(SproutFxKind.Bloom, ground);
        if (fx == null) return;
        fx.cell = cell;
        fx.age = 0f;
        fx.started = true;
    }

    // Yayılma: kaynak hücreden yeni hücreye sürünen dal + küçük pof.
    private void SproutSpread(FieldEvent source, GridSystem grid, GridPosition to)
    {
        GridPosition from = to;
        var zone = source.Zone(0);
        for (int i = 0; i < zone.Count; i++)
        {
            GridPosition c = zone[i];
            if (Mathf.Abs(c.x - to.x) + Mathf.Abs(c.z - to.z) == 1) { from = c; break; }
        }
        Vector3 start = SoilTop(grid, from), end = Top(grid, to);
        Vector3 dir = end - start; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        SproutFx fx = StartSproutFx(SproutFxKind.Spread, start);
        if (fx == null) return;
        fx.direction = dir.normalized;
        fx.cell = to;
    }

    private void SproutSap(Vector3 impact, Vector3 away, float size)
    {
        SproutFx fx = StartSproutFx(SproutFxKind.Sap, impact);
        if (fx == null) return;
        away.y = 0f;
        fx.direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
        fx.size = size;
    }

    // Ölüm dalgası hücreye ulaştığında: kuru yaprak kırıntıları + toz.
    private void SproutCrumble(Vector3 at)
    {
        SproutFx fx = StartSproutFx(SproutFxKind.Crumble, at);
        if (fx != null) fx.age = 0f;
    }

    private void SproutWilt(Vector3 impact)
    {
        SproutFx fx = StartSproutFx(SproutFxKind.Wilt, impact);
        if (fx != null) fx.age = 0f;
    }

    private void SyncSproutFx(float delta)
    {
        Camera camera = Camera.main;
        int shown = 0;
        for (int kind = 0; kind < 5; kind++)
        {
            SproutFx[] pool = SproutPool((SproutFxKind)kind);
            for (int i = 0; i < pool.Length; i++)
            {
                SproutFx fx = pool[i];
                if (fx == null || !fx.live) continue;
                fx.age += delta;
                float life = fx.kind switch { SproutFxKind.Bloom => SproutBloomLife, SproutFxKind.Spread => SproutSpreadLife, SproutFxKind.Sap => SproutSapLife,
                    SproutFxKind.Wilt => SproutWiltLife, _ => SproutCrumbleLife };
                if ((!fx.started && fx.age > .3f) || fx.age >= life) { StoreSproutFx(fx); continue; }
                if (camera != null) PoseSproutFx(fx, camera.transform);
                shown++;
            }
        }
        SproutFxShown = shown;
    }

    private static void StoreSproutFx(SproutFx fx)
    {
        fx.live = false;
        if (fx.root != null && fx.root.activeSelf) fx.root.SetActive(false);
    }

    private void ClearSproutFx()
    {
        for (int kind = 0; kind < 5; kind++) foreach (SproutFx fx in SproutPool((SproutFxKind)kind)) if (fx != null) StoreSproutFx(fx);
        SproutFxShown = 0;
    }

    private void PoseSproutFx(SproutFx fx, Transform view)
    {
        SproutBurstArtSO art = SproutBurstArt;
        if (art == null) return;
        float t = fx.started ? fx.age : Mathf.Min(fx.age, -1e-4f), s = fx.scale;
        Vector3 up = Vector3.up;
        Transform rootTr = fx.root.transform;
        if (fx.ground != null)
        {
            rootTr.SetPositionAndRotation(fx.at, fx.kind == SproutFxKind.Spread ? Quaternion.FromToRotation(Vector3.right, fx.direction) : Quaternion.identity);
            fx.ground.localScale = Vector3.one * s;
        }
        switch (fx.kind)
        {
            case SproutFxKind.Bloom:
            {
                // Çatlaklar: beklemede dışa uzar, pof'tan sonra söner (hücre uzayında yerde).
                for (int k = 0; k < fx.cracks.Length; k++)
                {
                    float grow = Mathf.Clamp01((t + SproutBloomLead - .04f * k) / .3f), fade = 1f - GreedSmooth((t - SproutPopAt - .04f) / .22f), c = grow * fade;
                    bool on = c > .01f;
                    SetActive(fx.cracks[k], on);
                    if (!on) continue;
                    fx.cracks[k].localPosition = Vector3.up * SproutCrackLift;   // saksı kenarının üstünden görünsün
                    fx.cracks[k].localScale = new Vector3(c, 1f, c);
                }
                // Dallar: parça parça kamçılanır, salınır, uçtan geri çekilir.
                for (int k = 0; k < SproutBurstArtSO.Tendrils; k++)
                {
                    Transform pivot = fx.tendrilRoots[k];
                    float grown = (t - .03f * k) * SproutBurstArtSO.TendrilSegments / .2f, retract = (t - .67f - .02f * k) * SproutBurstArtSO.TendrilSegments / .22f;
                    bool any = t >= 0f && grown > 0f && retract < SproutBurstArtSO.TendrilSegments;
                    SetActive(pivot, any);
                    if (!any) continue;
                    Vector2 d2 = art.tendrilDirections[k];
                    Vector3 axis = new Vector3(-d2.y, 0f, d2.x);
                    float sway = t > .12f ? 10f * Mathf.Sin(t * 20f + k) * (1f - GreedSmooth((t - .58f) / .33f)) : 0f;
                    pivot.localRotation = Quaternion.AngleAxis(sway, axis);
                    for (int i = 0; i < SproutBurstArtSO.TendrilSegments; i++)
                    {
                        Transform seg = fx.tendrils[k * SproutBurstArtSO.TendrilSegments + i];
                        bool on = grown > i && retract < SproutBurstArtSO.TendrilSegments - i;
                        SetActive(seg, on);
                        if (on) seg.localScale = Vector3.one * (grown - i < 1.5f ? 1.15f : 1f);
                    }
                }
                // Pof: bir an sonra açıklıktan spor bulutu (yelpaze), polen kıvılcımları.
                Vector3 center = fx.at + up * (SproutHeadHeight * s);
                Fountain(fx.puffs, t, SproutPopAt + .04f, .29f, center, SproutPuffReach * s, 75f, .3f * s, SproutPuffSize * s, view);
                for (int k = 0; k < fx.pollen.Length; k++)
                {
                    Transform p = fx.pollen[k];
                    float u = (t - SproutPopAt - .04f - .02f * (k % 3)) / .58f;
                    bool on = u >= 0f && u <= 1f;
                    SetActive(p, on);
                    if (!on) continue;
                    float a = k * 2.4f, sp = .45f + .045f * k, rise = .8f + .06f * k;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    p.SetPositionAndRotation(center + (d * (sp * GreedEaseOut(u / .4f) * .8f) + up * (.2f + rise * .7f * u)) * s + view.right * (.1f * s * Mathf.Sin(u * 8f + k)), view.rotation);
                    p.localScale = Vector3.one * ((SproutPollenSize + .015f * (k % 3)) * s * (1f - GreedSmooth((u - .55f) / .45f)));
                }
                break;
            }
            case SproutFxKind.Spread:
            {
                float fade = 1f - GreedSmooth((t - .5f) / .3f);
                for (int i = 0; i < fx.vine.Length; i++)
                {
                    float appear = t - i * (SproutVineSeconds / fx.vine.Length);
                    bool on = appear >= 0f && fade > .01f;
                    SetActive(fx.vine[i], on);
                    if (on) fx.vine[i].localScale = Vector3.one * ((appear < .03f ? 1.2f : 1f) * fade);
                }
                Vector3 target = fx.at + fx.direction * (2f * s) + up * (.55f * s);
                Fountain(fx.puffs, t, SproutVineSeconds, .25f, target, .4f * s, 65f, .2f * s, .12f * s, view);
                break;
            }
            case SproutFxKind.Sap:
            {
                Vector3 away = fx.direction, side = new Vector3(-away.z, 0f, away.x);
                int frame = (int)(t * 24f);
                bool splatOn = frame < SproutSplatFrames.Length;
                SetActive(fx.splat, splatOn);
                if (splatOn) { fx.splat.SetPositionAndRotation(fx.at + away * (.15f * s), view.rotation); fx.splat.localScale = Vector3.one * (SproutSplatFrames[frame] * fx.size * s); }
                for (int k = 0; k < fx.drops.Length; k++)
                {
                    float u = t / .33f;
                    bool on = u <= 1f;
                    SetActive(fx.drops[k], on);
                    if (!on) continue;
                    float a = k * .9f - 1.2f;
                    Vector3 dd = (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) + away * 1.2f).normalized;
                    fx.drops[k].position = fx.at + (dd * (.9f * u) + up * (.5f * u - .9f * u * u)) * s * fx.size;
                    fx.drops[k].localScale = Vector3.one * ((.05f + .015f * (k % 3)) * s * fx.size * (1f - t / .37f));
                }
                for (int k = 0; k < fx.shreds.Length; k++)
                {
                    float u = t / .42f;
                    Vector3 dd = (away + up * (.6f + .3f * k) + side * ((k - 1) * .5f)).normalized;
                    fx.shreds[k].SetPositionAndRotation(fx.at + (dd * (.9f * GreedEaseOut(u)) + up * (-.6f * u * u)) * s,
                        Quaternion.Euler((u * 6f + k) * Mathf.Rad2Deg, u * 4f * Mathf.Rad2Deg, (u * 9f + k) * Mathf.Rad2Deg));
                    fx.shreds[k].localScale = Vector3.one * (s * fx.size * (1f - .5f * u));
                    SetActive(fx.shreds[k], true);
                }
                break;
            }
            case SproutFxKind.Wilt:
                Fountain(fx.puffs, t, .06f, .33f, fx.at - up * (.3f * s), .8f * s, 85f, .35f * s, .21f * s, view);
                break;
            default:
            {
                // Ufalanma: kuru yaprak kırıntıları yukarı-dışa savrulup düşer; toz kısa kabarır.
                for (int k = 0; k < fx.shreds.Length; k++)
                {
                    float u = t / .58f, a = k * 1.7f + .4f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    fx.shreds[k].SetPositionAndRotation(fx.at + (d * (.45f * GreedEaseOut(u)) + up * (.35f + .45f * u - .9f * u * u)) * s,
                        Quaternion.Euler((u * 7f + k) * Mathf.Rad2Deg, u * 3f * Mathf.Rad2Deg, (u * 5f + k) * Mathf.Rad2Deg));
                    fx.shreds[k].localScale = Vector3.one * (s * .8f * (1f - GreedSmooth((u - .6f) / .4f)));
                    SetActive(fx.shreds[k], u <= 1f);
                }
                if (fx.puffs != null) Fountain(fx.puffs, t, 0f, .45f, fx.at + up * (.3f * s), .35f * s, 60f, .15f * s, .22f * s, view);
                break;
            }
        }
    }

    // Puflar merkezden küçük doğar, yukarı-dışa yelpaze gibi açılarak büyür, hızla söner (ekran düzleminde).
    private static void Fountain(Transform[] puffs, float t, float start, float life, Vector3 center, float reach, float spread, float rise, float size, Transform view)
    {
        int n = puffs.Length;
        for (int k = 0; k < n; k++)
        {
            float u = (t - start - .017f * (k % 3)) / life;
            bool on = u >= 0f && u <= 1f;
            SetActive(puffs[k], on);
            if (!on) continue;
            float ang = (-spread + 2f * spread * k / Mathf.Max(1, n - 1)) * Mathf.Deg2Rad;
            Vector3 d = view.up * Mathf.Cos(ang) + view.right * Mathf.Sin(ang);
            float go = GreedEaseOut(u / .55f);
            float scale = (.2f + .8f * GreedEaseOut(u / .35f)) * (1f - GreedSmooth((u - .45f) / .55f)) * SproutPuffVary[k % SproutPuffVary.Length];
            puffs[k].SetPositionAndRotation(center + d * (reach * go * (.75f + .25f * (k % 2))) + Vector3.up * (rise * u), view.rotation);
            puffs[k].localScale = Vector3.one * Mathf.Max(1e-4f, size * scale);
        }
    }

    // Ana filizin dönerek yükselişi (t: yükselişin başından sn). Döner: dikey / yatay ölçek, yer altı → üstü, dönüş (derece).
    private static void SproutRise(float t, out float height, out float across, out float lift, out float spin)
    {
        if (t <= 0f) { height = 0f; across = 1f; lift = 0f; spin = -360f; return; }
        lift = GreedEaseOut(t / .3f) + .15f * GreedBump(t - .2f, .2f);
        spin = -360f * (1f - GreedEaseOut(t / .42f));
        float land = GreedBump(t - .38f, .12f), swell = GreedSmooth((t - .42f) / .12f) * (t < SproutPopAt ? 1f : 0f), pop = GreedBump(t - SproutPopAt, .1f);
        height = (1f - .2f * land) * (1f + .06f * swell) * (1f - .15f * pop);
        across = (1f + .1f * land) * (1f + .06f * swell) * (1f + .12f * pop);
    }

    // ---- kurulum (havuz örneği başına bir kez) ----
    private SproutFx BuildSproutFx(SproutFxKind kind, SproutBurstArtSO art)
    {
        if (sproutFxRoot == null)
        {
            sproutFxRoot = new GameObject("Sprout Fx").transform;
            sproutFxRoot.SetParent(transform, false);
        }
        var root = new GameObject("Sprout " + kind);
        root.transform.SetParent(sproutFxRoot, false);
        var fx = new SproutFx { kind = kind, root = root };
        var lit = new[] { art.lit, art.ink };
        var flat = new[] { arcMaterial };
        Transform Card(string name, Mesh mesh) => GreedPart(root.transform, name, mesh, flat);
        if (kind == SproutFxKind.Bloom || kind == SproutFxKind.Spread)
        {
            fx.ground = new GameObject("Ground").transform;
            fx.ground.SetParent(root.transform, false);
        }
        switch (kind)
        {
            case SproutFxKind.Bloom:
                fx.cracks = new Transform[art.cracks.Length];
                for (int k = 0; k < fx.cracks.Length; k++) fx.cracks[k] = GreedPart(fx.ground, "Crack " + k, art.cracks[k], flat);
                fx.tendrilRoots = new Transform[SproutBurstArtSO.Tendrils];
                fx.tendrils = new Transform[art.tendrils.Length];
                for (int k = 0; k < SproutBurstArtSO.Tendrils; k++)
                {
                    Transform pivot = new GameObject("Tendril " + k).transform;
                    pivot.SetParent(fx.ground, false);
                    pivot.localPosition = art.tendrilRoots[k];
                    fx.tendrilRoots[k] = pivot;
                    for (int i = 0; i < SproutBurstArtSO.TendrilSegments; i++)
                    {
                        int n = k * SproutBurstArtSO.TendrilSegments + i;
                        Transform seg = GreedPart(pivot, "Segment " + i, art.tendrils[n], lit);
                        seg.localPosition = art.tendrilStarts[n] - art.tendrilRoots[k];
                        fx.tendrils[n] = seg;
                    }
                }
                fx.puffs = new Transform[SproutPuffs];
                for (int k = 0; k < SproutPuffs; k++) fx.puffs[k] = Card("Spore " + k, art.puffs[k % art.puffs.Length]);
                fx.pollen = new Transform[SproutPollen];
                for (int k = 0; k < SproutPollen; k++) fx.pollen[k] = Card("Pollen " + k, art.pollen[k % art.pollen.Length]);
                break;
            case SproutFxKind.Spread:
                fx.vine = new Transform[art.vine.Length];
                for (int i = 0; i < fx.vine.Length; i++) { fx.vine[i] = GreedPart(fx.ground, "Vine " + i, art.vine[i], lit); fx.vine[i].localPosition = art.vineStarts[i]; }
                fx.puffs = new Transform[SproutSpreadPuffs];
                for (int k = 0; k < SproutSpreadPuffs; k++) fx.puffs[k] = Card("Spread Puff " + k, art.puffs[k % art.puffs.Length]);
                break;
            case SproutFxKind.Sap:
                fx.splat = Card("Sap", art.splat);
                fx.drops = new Transform[SproutDrops];
                for (int k = 0; k < SproutDrops; k++) fx.drops[k] = GreedPart(root.transform, "Drop " + k, art.drop, lit);
                fx.shreds = new Transform[SproutShreds];
                for (int k = 0; k < SproutShreds; k++) fx.shreds[k] = GreedPart(root.transform, "Shred " + k, art.shred, lit);
                break;
            case SproutFxKind.Wilt:
                fx.puffs = new Transform[SproutPuffs];
                for (int k = 0; k < SproutPuffs; k++) fx.puffs[k] = Card("Wilt Puff " + k, art.puffs[k % art.puffs.Length]);
                break;
            default:
            {
                // Kuru yaprak kırıntıları: kopan yaprak ağı, kuruma rengine karışmış (property block bir kez, kurulumda).
                sproutDeadBlock ??= new MaterialPropertyBlock();
                sproutDeadBlock.Clear();
                sproutDeadBlock.SetFloat("_ToonFlash", .8f);
                sproutDeadBlock.SetColor("_ToonFlashColor", LateGameContent.WitherColor);
                fx.shreds = new Transform[SproutFlakes];
                for (int k = 0; k < SproutFlakes; k++)
                {
                    fx.shreds[k] = GreedPart(root.transform, "Flake " + k, art.shred, lit);
                    fx.shreds[k].GetComponent<MeshRenderer>().SetPropertyBlock(sproutDeadBlock, 0);
                }
                // Toz: Diken çalışmasının bej toz kartları (varsa); yoksa yalnız kırıntı.
                ThornBurstArtSO dust = ThornBurstArt;
                if (dust != null)
                {
                    fx.puffs = new Transform[SproutDust];
                    for (int k = 0; k < SproutDust; k++) fx.puffs[k] = Card("Dust " + k, dust.clouds[k % dust.clouds.Length]);
                }
                break;
            }
        }
        root.SetActive(false);
        return fx;
    }
}
