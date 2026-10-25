using UnityEngine;

// Aşılı Kabuk kişiliği (25 Ekim 2026; Blender çalışması Tools/Art/ShellBurst, kullanıcı onayı): "aşı damarlı zırh tohumu".
// Kabuk = ağır, kalın, inatçı (Diken patlayıcı, Filiz sinsi): kalın lob kabuk plakaları, çatlaklar; gövde koyu kabuk kahvesi,
// bölge rengi (A amber, B mor; mavi yok) damarlarda ve çekirdekte. Eski kabuk halkaları / düğüm öğeleri (ShellCover / ShellNode)
// kurulmaz; Kabuk'un tüm tarla görseli buradadır.
// - Birth (hazırlık): düğüm hücresinde toprak plaka plaka çatlar, aralarından bölge ışığı sızar; son 1,5 sn plakalar titrer.
//   Çıkışta plakalar dışa devrilir, ortadaki düğümün tepesinde kalkıp yana düşer; "TOK"ta taş tozu halkası + şok halkası, kısa sarsıntı.
// - Vein: düğümden bölgesinin her hücresine aşı damarı + saksı kenarını saran kelepçe; damarda ara ara ışık boncuğu koşar.
// - Node: düğüm toprağı iterek ağır ağır kalkar, az aşar, ShellTokAt'ta oturur. Vuruş: tabandan ağır sallanma, yeni çatlak aşaması
//   (çatlaklar = kalan vuruş göstergesi), "tink" + kabuk kıymıkları; beyaz flaş yok. Bir vuruş kalınca titrer. A (varsayılan kapanan)
//   motifi seçim boyunca nabız atar.
// - Karar (ZoneClosed): kurtulan düğüm (kırılan ya da süre dolunca) ceviz gibi açılır (Split); kapanan düğüm sarsılıp toprağa
//   çekilir (Sink). Damarlardan boncuk hücrelere koşar: kurtulan hücrede kelepçe kopup savrulur + kıvılcım, damar söner; kapanan
//   hücrede kabuk yaka kalkar (Collar). ZoneOpened: yakalar çatlar, plakalar dışa devrilip düşer (çözülme süresi içinde biter).
// - Yalnız görsel; oynanış etkileri olaydadır. Havuzlu: tür başına sabit kapasite (düğüm / doğum / açılış bölge başına bir örnek),
//   dolunca en eski kesilir ve sayılır; parçalar örnek başına bir kez kurulur, karede yalnız transform. Yaş yalnız Round'da, timeScale > 0.
public sealed partial class LateGamePresenter
{
    private enum ShellFxKind { Node, Birth, Vein, Collar, Hit, Split, Sink }
    // Çıkış: itme, oturma ("TOK"); damar boncuğu: aralık / koşu; karar boncuğunun gecikmesi ve hızı (hücre yarı boyu / sn).
    public const float ShellTokAt = .5f;
    private const float ShellPushSeconds = .42f, ShellBeadEvery = 2.4f, ShellBeadRun = .3f, ShellDecideLead = .25f, ShellBeadSpeed = 7.7f,
        ShellNodeCollarDelay = .6f, ShellBirthEnd = 1.3f, ShellOpenSeconds = .95f, ShellClampRadius = .8f;   // kelepçe: export ile aynı yarıçap
    private const int ShellVeinSegments = 9, ShellPathPoints = ShellVeinSegments + 1, ShellTokDust = 8, ShellChips = 3, ShellSplitSparks = 8,
        ShellFreeSparks = 4, ShellSinkDust = 5, ShellFlakes = 3, ShellCollarDust = 2;

    private sealed class ShellFx
    {
        public ShellFxKind kind;
        public GameObject root;
        public Transform local;   // hücre uzayı (ölçek = hücre yarı boyu)
        public bool live, warn, release, collarStarted;
        public int zone = -1, index, damage;
        public GridPosition cell;
        public Vector3 at, away, toNode, clampAt;
        public float length, age, ch = 1f, hitAge = 99f, sinkAge = -1f, emergeAt = -1f, prepLength = 6f, decideAt = -1f, openAt = -1f, travel, size = 1f, coreScale = 1f, crustY;
        public Transform core, motif, center, glow, shock, clamp, bead, band, knob, tinkCard;
        public Transform[] cracks, slabs, dust, segs, sparks, plates, plateCracks, crust, flakes, chips;
        public Vector3[] path;
    }

    private readonly ShellFx[] shellNodes = new ShellFx[2], shellBirths = new ShellFx[2], shellSplits = new ShellFx[2],
        shellVeins = new ShellFx[12], shellCollars = new ShellFx[8], shellHits = new ShellFx[4], shellSinks = new ShellFx[2];
    private readonly int[] shellNext = new int[7];
    private ShellBurstArtSO shellBurstArt;
    private bool shellBurstArtLoaded;
    private Transform shellFxRoot;
    private float shellShake;

    public int ShellFxShown { get; private set; }
    public int ShellFxCut { get; private set; }

    private ShellBurstArtSO ShellBurstArt
    {
        get
        {
            if (!shellBurstArtLoaded)
            {
                shellBurstArt = Resources.Load<ShellBurstArtSO>(ShellBurstArtSO.ResourcePath);
                shellBurstArtLoaded = true;
                if (shellBurstArt == null || !shellBurstArt.Valid) Debug.LogWarning("ShellBurstArt eksik; Aşılı Kabuk görselleri çizilmeyecek (oynanış etkilenmez).");
            }
            return shellBurstArt != null && shellBurstArt.Valid ? shellBurstArt : null;
        }
    }

    private ShellFx[] ShellPool(ShellFxKind kind) => kind switch
    {
        ShellFxKind.Node => shellNodes, ShellFxKind.Birth => shellBirths, ShellFxKind.Split => shellSplits, ShellFxKind.Vein => shellVeins,
        ShellFxKind.Collar => shellCollars, ShellFxKind.Hit => shellHits, _ => shellSinks,
    };

    // Bölge başına tek örnekli türler (düğüm, doğum, açılış) bölge dizininde durur; diğerleri ortak havuz.
    private static bool ShellZoned(ShellFxKind kind) => kind == ShellFxKind.Node || kind == ShellFxKind.Birth || kind == ShellFxKind.Split;

    private float ShellHalf => Mathf.Max(.1f, FieldCells.CellSize * .5f);

    // Kelepçe / yaka yüksekliği: saksılıda saksı kenarı (toprağın biraz üstü), saksısızda zemin.
    private float ShellRimY(GridSystem grid, GridPosition cell) => IsPotted(grid, cell) ? SoilTop(grid, cell).y + .03f : Top(grid, cell).y;

    // Kabuk hazırlanırken: bölge başına düğüm, doğum ve açılış örneği ile birkaç damar / yaka önceden kurulur.
    private void PrewarmShellFx()
    {
        ShellBurstArtSO art = ShellBurstArt;
        if (art == null || !EnsureArcAssets()) return;
        for (int z = 0; z < 2; z++)
        {
            shellNodes[z] ??= BuildShellFx(ShellFxKind.Node, art, z);
            shellBirths[z] ??= BuildShellFx(ShellFxKind.Birth, art, z);
            shellSplits[z] ??= BuildShellFx(ShellFxKind.Split, art, z);
        }
        for (int i = 0; i < 10; i++) shellVeins[i] ??= BuildShellFx(ShellFxKind.Vein, art, 0);
        for (int i = 0; i < 6; i++) shellCollars[i] ??= BuildShellFx(ShellFxKind.Collar, art, 0);
        shellHits[0] ??= BuildShellFx(ShellFxKind.Hit, art, 0);
        shellSinks[0] ??= BuildShellFx(ShellFxKind.Sink, art, 0);
    }

    private ShellFx StartShellFx(ShellFxKind kind, int zone, Vector3 at)
    {
        ShellBurstArtSO art = ShellBurstArt;
        if (art == null || !EnsureArcAssets()) return null;
        ShellFx[] pool = ShellPool(kind);
        ShellFx fx;
        if (ShellZoned(kind))
        {
            fx = pool[zone] ??= BuildShellFx(kind, art, zone);
            if (fx.live) ShellFxCut++;
        }
        else
        {
            int slot = -1;
            for (int i = 0; i < pool.Length && slot < 0; i++) if (pool[i] == null || !pool[i].live) slot = i;
            if (slot < 0) { slot = shellNext[(int)kind]; shellNext[(int)kind] = (slot + 1) % pool.Length; ShellFxCut++; }
            fx = pool[slot] ??= BuildShellFx(kind, art, zone);
            if (fx.zone != zone) SetShellZone(fx, art, zone);
        }
        fx.at = at;
        fx.age = 0f;
        fx.ch = ShellHalf;
        fx.size = 1f;
        fx.hitAge = 99f; fx.sinkAge = -1f; fx.emergeAt = -1f; fx.decideAt = -1f; fx.openAt = -1f;
        fx.damage = 0; fx.coreScale = 1f;
        fx.warn = fx.release = fx.collarStarted = false;
        fx.live = true;
        fx.root.transform.SetPositionAndRotation(fx.kind == ShellFxKind.Vein ? Vector3.zero : at, Quaternion.identity);
        if (fx.local != null) fx.local.localScale = Vector3.one * fx.ch;
        if (!fx.root.activeSelf) fx.root.SetActive(true);
        return fx;
    }

    private static ShellFx FindShell(ShellFx[] pool, GridPosition cell)
    {
        foreach (ShellFx fx in pool) if (fx != null && fx.live && fx.cell.x == cell.x && fx.cell.z == cell.z) return fx;
        return null;
    }

    private static bool ShellLive(ShellFx fx) => fx != null && fx.live;

    // SyncShell'den (her cue'da): olayın durumuna göre doğum, damar ve düğüm efektleri bir kez kurulur (kimlik: bölge + hücre).
    private void EnsureShellFx(GraftedShellEvent source, GridSystem grid)
    {
        FieldEventPhase phase = source.Phase;
        if (phase != FieldEventPhase.Preparing && phase != FieldEventPhase.Emerging && phase != FieldEventPhase.Active) return;
        if (ShellBurstArt == null) return;
        for (int z = 0; z < 2; z++)
        {
            InterventionTarget target = source.Targets[z];
            if (!source.Decided)
            {
                var cells = source.Zone(z);
                int vein = 0;
                for (int i = 0; i < cells.Count; i++)
                {
                    GridPosition cell = cells[i];
                    if (cell.x == target.Cell.x && cell.z == target.Cell.z) continue;
                    if (FindShell(shellVeins, cell) == null) StartShellVein(grid, z, target.Cell, cell, vein);
                    vein++;
                }
                if (phase == FieldEventPhase.Preparing)
                {
                    if (!ShellLive(shellBirths[z]) || shellBirths[z].cell.x != target.Cell.x || shellBirths[z].cell.z != target.Cell.z)
                    {
                        ShellFx birth = StartShellFx(ShellFxKind.Birth, z, SoilTop(grid, target.Cell));
                        if (birth != null) { birth.cell = target.Cell; birth.age = source.PhaseTime; birth.prepLength = source.PhaseTime + source.PrepareRemaining; }
                    }
                }
                else if (!target.Broken && !ShellLive(shellNodes[z]))
                {
                    // Sonradan açılan sunum: düğüm çıkmış hâlde (çıkış cue'su ShellEmerge'de yaşı sıfırlar).
                    ShellFx node = StartShellFx(ShellFxKind.Node, z, SoilTop(grid, target.Cell));
                    if (node != null) { node.cell = target.Cell; node.age = ShellTokAt + 1f; }
                }
            }
            if (ShellLive(shellNodes[z])) shellNodes[z].warn = z == 0 && !source.Decided;
        }
    }

    private void StartShellVein(GridSystem grid, int zone, GridPosition node, GridPosition cell, int index)
    {
        Vector3 a = FieldCells.World(grid, node), b = FieldCells.World(grid, cell);
        Vector3 d = b - a; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;
        d.Normalize();
        ShellFx fx = StartShellFx(ShellFxKind.Vein, zone, Vector3.zero);
        if (fx == null) return;
        float ch = fx.ch;
        fx.cell = cell;
        fx.index = index;
        fx.toNode = -d;
        float rim = ShellRimY(grid, cell), y0 = SoilTop(grid, node).y + .04f, y1 = rim + .06f * ch;
        Vector3 start = a + d * (.55f * ch), end = b - d * (.78f * ch), side = new Vector3(-d.z, 0f, d.x);
        start.y = y0; end.y = y1;
        float length = 0f;
        for (int i = 0; i < ShellPathPoints; i++)
        {
            float t = i / (float)(ShellPathPoints - 1);
            Vector3 p = Vector3.Lerp(start, end, t) + side * (.1f * ch * Mathf.Sin(t * Mathf.PI * 2f + index)) + Vector3.up * (.2f * ch * Mathf.Sin(Mathf.PI * t));
            fx.path[i] = p;
            if (i > 0) length += (p - fx.path[i - 1]).magnitude;
        }
        fx.length = length;
        fx.travel = Mathf.Max(.21f, length / (ShellBeadSpeed * ch));
        fx.clampAt = new Vector3(b.x, rim, b.z) + fx.toNode * (ShellClampRadius * ch);
        fx.at = new Vector3(b.x, rim, b.z);
    }

    private Vector3 ShellPathPoint(ShellFx fx, float u)
    {
        u = Mathf.Clamp01(u) * (ShellPathPoints - 1);
        int i = Mathf.Min((int)u, ShellPathPoints - 2);
        return Vector3.Lerp(fx.path[i], fx.path[i + 1], u - i);
    }

    // Çıkış cue'su: düğümler toprağın içinden kalkar, doğum plakaları devrilir.
    private void ShellEmerge(GraftedShellEvent source, GridSystem grid)
    {
        for (int z = 0; z < 2; z++)
        {
            InterventionTarget target = source.Targets[z];
            ShellFx node = ShellLive(shellNodes[z]) ? shellNodes[z] : StartShellFx(ShellFxKind.Node, z, SoilTop(grid, target.Cell));
            if (node != null) { node.cell = target.Cell; node.age = 0f; node.warn = z == 0; }
            ShellFx birth = shellBirths[z];
            if (ShellLive(birth) && birth.emergeAt < 0f) birth.emergeAt = birth.age;
        }
    }

    // Vuruş ve kırılma (cue): düğüm ağır sallanır, çatlak aşaması açılır, "tink" + kıymık; kırılan düğüm ceviz gibi açılır.
    private void ShellNodeHit(GraftedShellEvent source, int zone, bool broken)
    {
        ShellFx node = zone >= 0 && zone < 2 ? shellNodes[zone] : null;
        if (!ShellLive(node)) return;
        Vector3 away = RecoilFrom(node.at); away.y = 0f;
        away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.right;
        InterventionTarget target = source.Targets[zone];
        float fraction = 1f - target.Remaining / (float)Mathf.Max(1, target.Durability);
        node.damage = Mathf.Min(ShellBurstArtSO.CrackStages, Mathf.FloorToInt(fraction * (ShellBurstArtSO.CrackStages + 1) + 1e-3f));
        node.hitAge = 0f;
        node.away = away;
        node.release = target.Remaining == 1;   // düğümde: bir vuruş kaldı
        ShellFx hit = StartShellFx(ShellFxKind.Hit, zone, node.at + (Vector3.up * .55f - away * .5f) * node.ch);
        if (hit != null) { hit.away = away; hit.size = broken ? 1.35f : 1f; }
        if (broken) { ShellSplitAt(node); StoreShellFx(node); }
    }

    private void ShellSplitAt(ShellFx node)
    {
        ShellFx split = StartShellFx(ShellFxKind.Split, node.zone, node.at);
        if (split != null) { split.cell = node.cell; split.coreScale = 1f + .045f * node.damage; }
    }

    // Karar: kurtulan düğüm açılır, kapanan düğüm toprağa çekilir; damar boncukları hücrelere koşar (yaka / serbest kalma).
    private void ShellDecide(GraftedShellEvent source, GridSystem grid, int closed)
    {
        if (closed < 0 || closed > 1) return;
        int saved = 1 - closed;
        if (ShellLive(shellNodes[saved])) { ShellSplitAt(shellNodes[saved]); StoreShellFx(shellNodes[saved]); }
        ShellFx sinking = shellNodes[closed];
        if (ShellLive(sinking) && sinking.sinkAge < 0f)
        {
            sinking.sinkAge = 0f;
            sinking.warn = false;
            StartShellFx(ShellFxKind.Sink, closed, sinking.at);
        }
        foreach (ShellFx vein in shellVeins)
            if (ShellLive(vein) && vein.decideAt < 0f) { vein.decideAt = vein.age; vein.release = vein.zone == saved; }
        GridPosition nodeCell = source.Targets[closed].Cell;
        ShellFx collar = StartShellCollar(grid, closed, nodeCell);
        if (collar != null) collar.age = -ShellNodeCollarDelay;
    }

    private ShellFx StartShellCollar(GridSystem grid, int zone, GridPosition cell)
    {
        ShellFx existing = FindShell(shellCollars, cell);
        if (existing != null) return existing;
        Vector3 top = Top(grid, cell);
        float rim = ShellRimY(grid, cell);
        ShellFx fx = StartShellFx(ShellFxKind.Collar, zone, new Vector3(top.x, rim, top.z));
        if (fx == null) return null;
        fx.cell = cell;
        fx.crustY = (SoilTop(grid, cell).y - rim) / fx.ch + .01f;
        return fx;
    }

    private void ShellOpenZone(int zone)
    {
        foreach (ShellFx fx in shellCollars) if (ShellLive(fx) && fx.zone == zone && fx.openAt < 0f) fx.openAt = Mathf.Max(fx.age, 0f);
        foreach (ShellFx fx in shellVeins) if (ShellLive(fx) && fx.zone == zone && fx.openAt < 0f) fx.openAt = fx.age;
    }

    private void SyncShellFx(float delta)
    {
        shellShake = 0f;
        Camera camera = Camera.main;
        int shown = 0;
        for (int kind = 0; kind < 7; kind++)
        {
            ShellFx[] pool = ShellPool((ShellFxKind)kind);
            for (int i = 0; i < pool.Length; i++)
            {
                ShellFx fx = pool[i];
                if (fx == null || !fx.live) continue;
                fx.age += delta;
                fx.hitAge += delta;
                if (fx.sinkAge >= 0f) fx.sinkAge += delta;
                if (ShellDone(fx)) { StoreShellFx(fx); continue; }
                if (camera != null) PoseShellFx(fx, camera.transform);
                shown++;
            }
        }
        ShellFxShown = shown;
    }

    private bool ShellDone(ShellFx fx) => fx.kind switch
    {
        ShellFxKind.Node => fx.sinkAge > .7f,
        ShellFxKind.Birth => fx.emergeAt >= 0f && fx.age - fx.emergeAt > ShellBirthEnd,
        ShellFxKind.Vein => fx.decideAt >= 0f && (fx.release ? fx.age > fx.decideAt + ShellDecideLead + fx.travel + .7f : fx.openAt >= 0f && fx.age > fx.openAt + .8f),
        ShellFxKind.Collar => fx.openAt >= 0f && fx.age > fx.openAt + ShellOpenSeconds,
        ShellFxKind.Hit => fx.age > .5f,
        ShellFxKind.Split => fx.age > 1.2f,
        _ => fx.age > .8f,
    };

    private static void StoreShellFx(ShellFx fx)
    {
        fx.live = false;
        if (fx.root != null && fx.root.activeSelf) fx.root.SetActive(false);
    }

    private void ClearShellFx()
    {
        for (int kind = 0; kind < 7; kind++) foreach (ShellFx fx in ShellPool((ShellFxKind)kind)) if (fx != null) StoreShellFx(fx);
        ShellFxShown = 0;
        shellShake = 0f;
    }

    private static float ShellEaseIn(float x) { x = Mathf.Clamp01(x); return x * x * x; }

    private static void SetActive(Transform[] parts, bool on)
    {
        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++) if (parts[i] != null && parts[i].gameObject.activeSelf != on) parts[i].gameObject.SetActive(on);
    }

    private static void ShellCard(Transform part, Vector3 position, float size, Transform view)
    {
        bool on = size > 1e-4f;
        SetActive(part, on);
        if (!on) return;
        part.SetPositionAndRotation(position, view.rotation);
        part.localScale = Vector3.one * size;
    }

    // ---- poz ----
    private void PoseShellFx(ShellFx fx, Transform view)
    {
        ShellBurstArtSO art = ShellBurstArt;
        if (art == null) return;
        switch (fx.kind)
        {
            case ShellFxKind.Node: PoseShellNode(fx, art); break;
            case ShellFxKind.Birth: PoseShellBirth(fx, art, view); break;
            case ShellFxKind.Vein: PoseShellVein(fx, view); break;
            case ShellFxKind.Collar: PoseShellCollar(fx, art, view); break;
            case ShellFxKind.Hit: PoseShellHit(fx, view); break;
            case ShellFxKind.Split: PoseShellSplit(fx, art, view); break;
            default: PoseShellSink(fx, view); break;
        }
    }

    private static void PoseShellNode(ShellFx fx, ShellBurstArtSO art)
    {
        float t = fx.age, s = fx.ch;
        Vector3 pos = fx.at, scale = Vector3.one;
        Quaternion rot = Quaternion.identity;
        if (t < ShellTokAt)
        {
            // Ağır çıkış: toprağı iterek titreye titreye kalkar, az aşar.
            float lift = t <= ShellPushSeconds ? -1.25f * (1f - GreedSmooth(t / ShellPushSeconds)) : .12f * GreedBump(t - ShellPushSeconds, ShellTokAt - ShellPushSeconds);
            pos.y += (lift + .025f * Mathf.Sin(t * 74f)) * s;
            rot = Quaternion.Euler(3f * Mathf.Sin(t * 55f), 0f, 3f * Mathf.Cos(t * 46f));
        }
        else
        {
            // "TOK": basılıp yaylanır; sonra ağır nefes.
            float k = t - ShellTokAt, land = Mathf.Exp(-k * 14f) * Mathf.Cos(k * 30f);
            scale = new Vector3(1f + .14f * land, 1f - .18f * land + .015f * Mathf.Sin(t * 6f), 1f + .14f * land);
        }
        if (fx.hitAge < 1.2f)
        {
            float h = fx.hitAge;
            rot = Quaternion.AngleAxis(14f * Mathf.Exp(-h * 6.2f) * Mathf.Cos(h * 14.9f), Vector3.Cross(Vector3.up, fx.away)) * rot;
            if (h < .08f) scale = Vector3.Scale(scale, new Vector3(1.06f, .93f, 1.06f));
        }
        if (fx.release && fx.sinkAge < 0f)   // bir vuruş kaldı: kırılmak üzere titrer
        {
            rot = Quaternion.AngleAxis(1.6f * Mathf.Sin(t * 70f), Vector3.Cross(Vector3.up, fx.away)) * rot;
            pos += fx.away * (.012f * s * Mathf.Sin(t * 89f));
        }
        if (fx.sinkAge >= 0f)   // kapanan düğüm: sarsılır, toprağa çekilir
        {
            float k = fx.sinkAge;
            if (k < .2f) rot = Quaternion.Euler(4f * Mathf.Sin(k * 80f), 0f, 4f * Mathf.Cos(k * 70f)) * rot;
            pos.y -= 1.3f * s * ShellEaseIn((k - .17f) / .46f);
        }
        fx.root.transform.SetPositionAndRotation(pos, rot);
        fx.root.transform.localScale = scale * s;
        fx.core.localScale = Vector3.one * (1f + .045f * fx.damage);
        for (int i = 0; i < fx.cracks.Length; i++)
        {
            bool on = i < fx.damage;
            SetActive(fx.cracks[i], on);
            if (on) fx.cracks[i].localScale = Vector3.one * (i == fx.damage - 1 && fx.hitAge < .08f ? 1.04f : 1f);
        }
        fx.motif.localScale = Vector3.one * (fx.warn && t >= ShellTokAt ? 1f + .05f * Mathf.Abs(Mathf.Sin(t * 6f)) : 1f);
    }

    private void PoseShellBirth(ShellFx fx, ShellBurstArtSO art, Transform view)
    {
        float t = fx.age, s = fx.ch, e = fx.emergeAt >= 0f ? t - fx.emergeAt : -1f;
        float g = GreedSmooth(t / .5f) * (1f + .06f * Mathf.Sin(t * 13f)) * (e < 0f ? 1f : 1f - GreedSmooth((e - .33f) / .21f));
        SetActive(fx.glow, g > .01f);
        if (g > .01f) fx.glow.localScale = new Vector3(g, 1f, g);
        float shakeIn = GreedSmooth((t - (fx.prepLength - 1.6f)) / 1.5f), amp = 1f + 5f * shakeIn, lift = .012f + .05f * shakeIn;
        float sink = e < 0f ? 0f : GreedSmooth((e - .83f) / .42f);
        for (int k = 0; k < fx.slabs.Length; k++)
        {
            Vector3 d = art.slabDirections[k];
            Quaternion rattle = e < .5f ? Quaternion.Euler(amp * Mathf.Sin(t * 65f + k), 0f, amp * Mathf.Cos(t * 74f + 2f * k)) : Quaternion.identity;
            float tilt = e < 0f ? 0f : e < .5f ? 65f * GreedSmooth(e / .375f) : 65f + 45f * GreedEaseOut((e - .5f) / .125f);
            float shift = e < 0f ? 0f : .14f * GreedSmooth(e / .375f) + (e >= .5f ? .18f * GreedEaseOut((e - .5f) / .167f) : 0f);
            Transform slab = fx.slabs[k];
            SetActive(slab, sink < .99f);
            slab.localPosition = art.slabHinges[k] + d * shift + Vector3.up * (lift - .25f * sink);
            slab.localRotation = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, d)) * rattle;
        }
        // Ortadaki plaka: düğümün tepesinde kalkar, oturmada yana kayıp düşer.
        if (e < .29f)
        {
            fx.center.localPosition = Vector3.up * lift;
            fx.center.localRotation = Quaternion.Euler(amp * Mathf.Sin(t * 53f), 0f, amp * Mathf.Cos(t * 60f));
            SetActive(fx.center, true);
        }
        else if (e < ShellTokAt)
        {
            ShellFx node = ShellLive(shellNodes[fx.zone]) ? shellNodes[fx.zone] : null;
            Vector3 size = art.nodeSize[fx.zone];
            float top = node != null ? (node.root.transform.position.y - fx.at.y) / s + size.y + size.z - .05f : lift;
            fx.center.localPosition = Vector3.up * Mathf.Max(lift, top);
            fx.center.localRotation = Quaternion.Euler(0f, 0f, 8f * (e - .29f) * 24f);
        }
        else
        {
            float u = (e - ShellTokAt) / .25f, sink2 = GreedSmooth((e - .92f) / .33f);
            Vector3 side = -view.right * .8f - view.forward * .4f; side.y = 0f; side = side.normalized * .9f;
            fx.center.localPosition = side * GreedEaseOut(u) + Vector3.up * ((u < 1f ? 1.15f * (1f - u) : 0f) + .02f - .25f * sink2);
            fx.center.localRotation = Quaternion.AngleAxis(150f * GreedEaseOut(u), Vector3.Cross(Vector3.up, side.normalized));
            SetActive(fx.center, sink2 < .99f);
        }
        // "TOK": taş tozu halkası, mürekkep şok halkası, kısa sarsıntı.
        for (int k = 0; k < fx.dust.Length; k++)
        {
            float u = e < 0f ? -1f : (e - ShellTokAt - .0125f * (k % 3)) / .42f;
            float a = k * 2f * Mathf.PI / ShellTokDust + .3f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float size = u < 0f || u > 1f ? 0f : (.16f + .03f * (k % 3)) * (.35f + .65f * GreedEaseOut(u / .3f)) * (1f - GreedSmooth((u - .45f) / .55f)) * s;
            ShellCard(fx.dust[k], fx.at + (d * (.55f + .55f * GreedEaseOut(u)) + Vector3.up * (.12f + .12f * u)) * s, size, view);
        }
        float w = e < 0f ? -1f : (e - ShellTokAt) / .21f;
        SetActive(fx.shock, w >= 0f && w <= 1f);
        if (w >= 0f && w <= 1f) { float r = .6f + 1.1f * GreedEaseOut(w); fx.shock.localScale = new Vector3(r, 1f, r); }
        if (e >= ShellTokAt && e < ShellTokAt + .25f) shellShake = Mathf.Max(shellShake, 1f - (e - ShellTokAt) / .25f);
    }

    private void PoseShellVein(ShellFx fx, Transform view)
    {
        float t = fx.age, s = fx.ch;
        float arrive = fx.decideAt < 0f ? float.MaxValue : fx.decideAt + ShellDecideLead + fx.travel;
        for (int k = 0; k < fx.segs.Length; k++)
        {
            float grownAt = .167f + .025f * fx.index + k * .0375f;
            float thick = t < grownAt ? 0f : (t - grownAt < .04f ? 1.25f : 1f), length = 1f;
            if (fx.decideAt >= 0f && fx.release)   // serbest: boncuk geçtikten sonra söner
            {
                float pass = fx.decideAt + ShellDecideLead + fx.travel * (k + .5f) / fx.segs.Length;
                float wilt = GreedSmooth((t - pass - .125f) / .21f);
                thick *= 1f - .5f * wilt; length = 1f - wilt;
            }
            if (fx.openAt >= 0f)   // açılış: hücre ucundan düğüme doğru söner
            {
                float wilt = GreedSmooth((t - fx.openAt - .33f - (fx.segs.Length - 1 - k) * .02f) / .21f);
                thick *= 1f - .5f * wilt; length *= 1f - wilt;
            }
            Transform seg = fx.segs[k];
            bool on = thick > .01f && length > .01f;
            SetActive(seg, on);
            if (!on) continue;
            Vector3 a = fx.path[k], b = fx.path[k + 1], dir = b - a;
            seg.SetPositionAndRotation(a, Quaternion.LookRotation(dir, Vector3.up));
            seg.localScale = new Vector3(thick * s, thick * s, dir.magnitude * length);
        }
        // Kelepçe: damar ulaşınca kenara oturur; serbest kalınca kopup savrulur; kapanınca yaka onu yutar.
        float clampAt = .167f + .025f * fx.index + fx.segs.Length * .0375f;
        float cs = t < clampAt ? 0f : (t - clampAt < .04f ? 1.3f : 1f);
        Vector3 cpos = fx.clampAt;
        Quaternion crot = Quaternion.FromToRotation(Vector3.right, fx.toNode);
        if (fx.release && t >= arrive)
        {
            float u = (t - arrive) / .375f;
            cpos += (fx.toNode * (.35f * GreedEaseOut(u)) + Vector3.up * (.55f * u - .7f * u * u)) * s;
            crot = Quaternion.AngleAxis(80f * GreedEaseOut(u), Vector3.Cross(Vector3.up, fx.toNode)) * crot;
            cs *= 1f - GreedSmooth((u - .55f) / .45f);
        }
        else if (!fx.release && t >= arrive) cs *= 1f - GreedSmooth((t - arrive) / .125f);
        SetActive(fx.clamp, cs > .01f);
        if (cs > .01f) { fx.clamp.SetPositionAndRotation(cpos, crot); fx.clamp.localScale = Vector3.one * (cs * s); }
        // Boncuk: hazırlık / seçimde ara ara koşar; kararda hücreye koşar.
        float bead = -1f;
        if (fx.decideAt < 0f)
        {
            float start = .54f + .03f * fx.index;
            if (t >= start) { float phase = (t - start) % ShellBeadEvery; if (phase < ShellBeadRun) bead = phase / ShellBeadRun; }
        }
        else if (t >= fx.decideAt + ShellDecideLead && t <= arrive) bead = (t - fx.decideAt - ShellDecideLead) / fx.travel;
        SetActive(fx.bead, bead >= 0f);
        if (bead >= 0f) { fx.bead.position = ShellPathPoint(fx, bead); fx.bead.localScale = Vector3.one * s; }
        // Kapanan hücre: boncuk varınca kabuk yaka kalkar.
        if (!fx.release && fx.decideAt >= 0f && t >= arrive && !fx.collarStarted)
        {
            fx.collarStarted = true;
            GridSystem grid = FieldCells.Grid;
            if (grid != null) StartShellCollar(grid, fx.zone, fx.cell);
        }
        // Serbest kalan hücre: kıvılcımlar.
        for (int k = 0; k < fx.sparks.Length; k++)
        {
            float u = fx.release && t >= arrive ? (t - arrive - .02f * k) / .5f : -1f;
            float a = k * Mathf.PI * .5f + .6f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float size = u < 0f || u > 1f ? 0f : .06f * s * (1f - GreedSmooth((u - .5f) / .5f));
            ShellCard(fx.sparks[k], fx.at + (d * (.45f * GreedEaseOut(u)) + Vector3.up * (.3f + .9f * u)) * s, size, view);
        }
    }

    private static void PoseShellCollar(ShellFx fx, ShellBurstArtSO art, Transform view)
    {
        float t = fx.age, s = fx.ch, o = fx.openAt >= 0f ? t - fx.openAt : -1f;
        fx.root.transform.rotation = o >= 0f && o < .25f ? Quaternion.Euler(3f * Mathf.Sin(o * 82f), 0f, -3f * Mathf.Sin(o * 82f)) : Quaternion.identity;
        for (int k = 0; k < fx.plates.Length; k++)
        {
            Transform plate = fx.plates[k];
            Vector3 d = art.collarDirections[k], axis = Vector3.Cross(Vector3.up, d);
            float u = (t - .02f * k) / .167f;
            if (u < 0f) { SetActive(plate, false); continue; }
            float angle, scale = 1f;
            if (o < .25f)
            {
                // kenardan kalkıp içe kapanır, "tak"
                angle = u < 1f ? 100f - 114f * GreedEaseOut(u) : -14f + 8f * GreedSmooth((u - 1f) / 1.5f);
                scale = Mathf.Min(1f, .3f + .7f * GreedEaseOut(u / .6f));
            }
            else
            {
                float v = (o - .25f - .017f * k) / .21f, sink = GreedSmooth((o - .67f) / .25f);
                angle = -6f + 126f * GreedEaseOut(v);
                scale = 1f - sink;
            }
            SetActive(plate, scale > .01f);
            plate.localPosition = art.collarHinges[k];
            plate.localRotation = Quaternion.AngleAxis(angle, axis);
            plate.localScale = new Vector3(1f, scale, 1f);
            if (fx.plateCracks[k] != null) SetActive(fx.plateCracks[k], o >= 0f && o < .58f);
        }
        float band = GreedSmooth((t - .04f) / .125f) * (o < 0f ? 1f : 1f - GreedSmooth((o - .25f) / .125f));
        SetActive(fx.band, band > .01f);
        if (band > .01f) fx.band.localScale = new Vector3(1f, band, 1f);
        for (int k = 0; k < fx.crust.Length; k++)
        {
            Transform slab = fx.crust[k];
            Vector3 c = art.crustCenters[k]; c.y = fx.crustY;
            float u = (t - .083f - .017f * k) / .125f;
            if (u < 0f) { SetActive(slab, false); continue; }
            if (o < .25f)
            {
                SetActive(slab, true);
                slab.localPosition = c; slab.localRotation = Quaternion.identity; slab.localScale = Vector3.one * (u < 1f ? 1.2f : 1f);
                continue;
            }
            float v = (o - .25f - .02f * k) / .33f;
            Vector3 dd = new Vector3(c.x, 0f, c.z); dd = dd.sqrMagnitude > 1e-6f ? dd.normalized : Vector3.right;
            float size = v >= 1f ? 0f : Mathf.Max(0f, 1f - GreedSmooth((v - .5f) / .5f));
            SetActive(slab, size > .01f);
            slab.localPosition = c + dd * (.25f * GreedEaseOut(v)) + Vector3.up * (.35f * GreedBump(v * 8f, 6f));
            slab.localRotation = Quaternion.Euler((v * 3f + k) * Mathf.Rad2Deg, v * 2f * Mathf.Rad2Deg, 0f);
            slab.localScale = Vector3.one * size;
        }
        for (int k = 0; k < fx.flakes.Length; k++)
        {
            float v = o < 0f ? -1f : (o - .25f - .02f * k) / .42f, a = k * 2.1f + .5f;
            bool on = v >= 0f && v <= 1f;
            SetActive(fx.flakes[k], on);
            if (!on) continue;
            fx.flakes[k].localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.7f * GreedEaseOut(v)) + Vector3.up * (.4f + .9f * v - 1.2f * v * v);
            fx.flakes[k].localRotation = Quaternion.Euler((v * 7f + k) * Mathf.Rad2Deg, v * 5f * Mathf.Rad2Deg, v * 3f * Mathf.Rad2Deg);
        }
        for (int k = 0; k < fx.dust.Length; k++)
        {
            float v = o < 0f ? -1f : (o - .25f - .04f * k) / .375f, a = k * Mathf.PI + .8f;
            float size = v < 0f || v > 1f ? 0f : .2f * s * (.4f + .6f * GreedEaseOut(v / .3f)) * (1f - GreedSmooth((v - .4f) / .6f));
            ShellCard(fx.dust[k], fx.at + (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.5f + .3f * v) + Vector3.up * (.25f + .2f * v)) * s, size, view);
        }
    }

    private static void PoseShellHit(ShellFx fx, Transform view)
    {
        float t = fx.age, s = fx.ch;
        int frame = (int)(t * 24f);
        float tink = frame == 0 ? .8f : frame == 1 ? 1.3f : frame == 2 ? .65f : 0f;
        ShellCard(fx.tinkCard, fx.at - view.forward * (.4f * s), tink * fx.size * s, view);
        Vector3 side = Vector3.Cross(Vector3.up, fx.away);
        for (int j = 0; j < fx.chips.Length; j++)
        {
            float u = t / .46f;
            bool on = fx.size <= 1f && u <= 1f;
            SetActive(fx.chips[j], on);
            if (!on) continue;
            Vector3 dir = (-fx.away * .9f + side * ((j - 1) * .7f)).normalized;
            fx.chips[j].SetPositionAndRotation(fx.at + (dir * (1.1f * GreedEaseOut(u)) + Vector3.up * (.7f * u - u * u)) * s,
                Quaternion.Euler((u * 8f + j) * Mathf.Rad2Deg, u * 5f * Mathf.Rad2Deg, (u * 6f + j) * Mathf.Rad2Deg));
            fx.chips[j].localScale = Vector3.one * s;
        }
    }

    private static void PoseShellSplit(ShellFx fx, ShellBurstArtSO art, Transform view)
    {
        float t = fx.age, s = fx.ch;
        int z = fx.zone;
        float sink = GreedSmooth((t - .75f) / .42f);
        for (int k = 0; k < fx.plates.Length; k++)
        {
            Vector3 h = art.plateHinges[z * ShellBurstArtSO.Plates + k], d = art.plateDirections[z * ShellBurstArtSO.Plates + k];
            float u = (t - .025f * k) / .25f;
            float angle = u > 0f ? (102f + 8f * (k % 2)) * GreedEaseOut(u) : 0f;
            Transform plate = fx.plates[k];
            SetActive(plate, sink < .99f);
            plate.localPosition = h + d * (.12f * GreedEaseOut(u)) + Vector3.up * (.18f * GreedBump(t - .025f * k, .29f) - .35f * sink);
            plate.localRotation = Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, d));
        }
        float ku = t / .42f;
        SetActive(fx.knob, ku <= 1.2f);
        fx.knob.localPosition = art.knobRest[z] + new Vector3(-.25f * ku, 1.6f * ku - 1.9f * ku * ku, .1f * ku);
        fx.knob.localRotation = Quaternion.Euler(ku * 12f * Mathf.Rad2Deg, ku * 7f * Mathf.Rad2Deg, 0f);
        Vector3 size = art.nodeSize[z];
        float cs = t < .33f ? Mathf.Lerp(fx.coreScale, 1.25f, GreedSmooth(t / .17f)) : 1.25f * (1f - (t - .33f) / .125f);
        SetActive(fx.core, cs > .01f);
        if (cs > .01f) { fx.core.localPosition = Vector3.up * (size.z + .35f * GreedSmooth((t - .17f) / .17f)); fx.core.localScale = Vector3.one * cs; }
        Vector3 center = fx.at + Vector3.up * ((size.z + .35f) * s);
        for (int k = 0; k < fx.sparks.Length; k++)
        {
            float u = (t - .33f - .0125f * (k % 3)) / .5f, a = k * 2f * Mathf.PI / ShellSplitSparks + .2f;
            Vector3 d = view.right * Mathf.Cos(a) + view.up * Mathf.Sin(a);
            float sz = u < 0f || u > 1f ? 0f : .07f * s * (1f - GreedSmooth((u - .5f) / .5f));
            ShellCard(fx.sparks[k], center + (d * (.4f + .9f * GreedEaseOut(u)) + Vector3.up * (.3f * u)) * s, sz, view);
        }
    }

    private static void PoseShellSink(ShellFx fx, Transform view)
    {
        float t = fx.age, s = fx.ch;
        for (int k = 0; k < fx.dust.Length; k++)
        {
            float v = (t - .25f - .02f * k) / .42f, a = k * 2f * Mathf.PI / ShellSinkDust + .4f;
            float size = v < 0f || v > 1f ? 0f : .2f * s * (.4f + .6f * GreedEaseOut(v / .3f)) * (1f - GreedSmooth((v - .4f) / .6f));
            ShellCard(fx.dust[k], fx.at + (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.5f + .4f * v) + Vector3.up * .15f) * s, size, view);
        }
    }

    // ---- kurulum (havuz örneği başına bir kez) ----
    private ShellFx BuildShellFx(ShellFxKind kind, ShellBurstArtSO art, int zone)
    {
        if (shellFxRoot == null)
        {
            shellFxRoot = new GameObject("Shell Burst").transform;
            shellFxRoot.SetParent(transform, false);
        }
        var root = new GameObject("Shell " + kind);
        root.transform.SetParent(shellFxRoot, false);
        var fx = new ShellFx { kind = kind, root = root, zone = zone };
        var lit = new[] { art.lit, art.ink };
        var flat = new[] { arcMaterial };
        var glowInk = new[] { arcMaterial, art.ink };
        Transform Local()
        {
            var local = new GameObject("Local").transform;
            local.SetParent(root.transform, false);
            return local;
        }
        Transform[] Cards(string name, int count, Mesh mesh, Mesh alternate = null)
        {
            var parts = new Transform[count];
            for (int k = 0; k < count; k++) parts[k] = GreedPart(root.transform, name + " " + k, alternate != null && k % 2 == 1 ? alternate : mesh, flat);
            return parts;
        }
        Transform CorePivot(Transform parent, float height)
        {
            var pivot = new GameObject("Core").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = Vector3.up * height;
            GreedPart(pivot, "Core Mesh", art.nodeCore[zone], glowInk).localPosition = Vector3.down * height;
            return pivot;
        }
        switch (kind)
        {
            case ShellFxKind.Node:
                GreedPart(root.transform, "Body", art.nodeBody[zone], lit);
                fx.core = CorePivot(root.transform, art.nodeSize[zone].z);
                fx.motif = GreedPart(root.transform, "Motif", art.nodeMotif[zone], flat);
                fx.cracks = new Transform[ShellBurstArtSO.CrackStages];
                for (int i = 0; i < fx.cracks.Length; i++) fx.cracks[i] = GreedPart(root.transform, "Crack " + i, art.nodeCracks[zone * ShellBurstArtSO.CrackStages + i], flat);
                break;
            case ShellFxKind.Birth:
                fx.local = Local();
                fx.glow = GreedPart(fx.local, "Glow", art.birthGlow[zone], flat);
                fx.center = GreedPart(fx.local, "Center", art.birthCenter, lit);
                fx.slabs = new Transform[ShellBurstArtSO.Slabs];
                for (int k = 0; k < fx.slabs.Length; k++) fx.slabs[k] = GreedPart(fx.local, "Slab " + k, art.birthSlabs[k], lit);
                fx.shock = GreedPart(fx.local, "Shock", art.shock, flat);
                fx.shock.localPosition = Vector3.up * .01f;
                fx.dust = Cards("Dust", ShellTokDust, art.dust[0], art.dust[1]);
                break;
            case ShellFxKind.Vein:
                fx.path = new Vector3[ShellPathPoints];
                fx.segs = new Transform[ShellVeinSegments];
                for (int k = 0; k < fx.segs.Length; k++) fx.segs[k] = GreedPart(root.transform, "Vein " + k, art.veinSegment[zone], lit);
                fx.clamp = GreedPart(root.transform, "Clamp", art.clamp[zone], lit);
                fx.bead = GreedPart(root.transform, "Bead", art.bead[zone], glowInk);
                fx.sparks = Cards("Free Spark", ShellFreeSparks, art.spark[zone], art.sparkCream);
                break;
            case ShellFxKind.Collar:
                fx.local = Local();
                fx.plates = new Transform[ShellBurstArtSO.CollarPlates];
                fx.plateCracks = new Transform[ShellBurstArtSO.CollarPlates];
                for (int k = 0; k < fx.plates.Length; k++)
                {
                    fx.plates[k] = GreedPart(fx.local, "Plate " + k, art.collarPlates[k], lit);
                    Mesh crack = art.collarCracks[zone * ShellBurstArtSO.CollarPlates + k];
                    if (crack != null) fx.plateCracks[k] = GreedPart(fx.plates[k], "Crack", crack, flat);
                }
                fx.band = GreedPart(fx.local, "Band", art.collarBand[zone], flat);
                fx.crust = new Transform[ShellBurstArtSO.Crust];
                for (int k = 0; k < fx.crust.Length; k++) fx.crust[k] = GreedPart(fx.local, "Crust " + k, art.crust[k], lit);
                fx.flakes = new Transform[ShellFlakes];
                for (int k = 0; k < fx.flakes.Length; k++) fx.flakes[k] = GreedPart(fx.local, "Flake " + k, art.chip, lit);
                fx.dust = Cards("Dust", ShellCollarDust, art.dust[1], art.dust[2]);
                break;
            case ShellFxKind.Hit:
                fx.tinkCard = GreedPart(root.transform, "Tink", art.tink[zone], flat);
                fx.chips = new Transform[ShellChips];
                for (int k = 0; k < fx.chips.Length; k++) fx.chips[k] = GreedPart(root.transform, "Chip " + k, art.chip, lit);
                break;
            case ShellFxKind.Split:
                fx.local = Local();
                fx.plates = new Transform[ShellBurstArtSO.Plates];
                for (int k = 0; k < fx.plates.Length; k++) fx.plates[k] = GreedPart(fx.local, "Plate " + k, art.nodePlates[zone * ShellBurstArtSO.Plates + k], lit);
                fx.knob = GreedPart(fx.local, "Knob", art.nodeKnob[zone], lit);
                fx.core = CorePivot(fx.local, art.nodeSize[zone].z);
                fx.sparks = Cards("Core Spark", ShellSplitSparks, art.spark[zone], art.sparkCream);
                break;
            default:
                fx.dust = Cards("Dust", ShellSinkDust, art.dust[2], art.dust[0]);
                break;
        }
        root.SetActive(false);
        return fx;
    }

    // Ortak havuzdaki örnek başka bölgeye geçerken bölge renkli ağlar değişir (kurulum yok, yalnız sharedMesh).
    private static void SetShellZone(ShellFx fx, ShellBurstArtSO art, int zone)
    {
        fx.zone = zone;
        switch (fx.kind)
        {
            case ShellFxKind.Vein:
                foreach (Transform seg in fx.segs) seg.GetComponent<MeshFilter>().sharedMesh = art.veinSegment[zone];
                fx.clamp.GetComponent<MeshFilter>().sharedMesh = art.clamp[zone];
                fx.bead.GetComponent<MeshFilter>().sharedMesh = art.bead[zone];
                for (int k = 0; k < fx.sparks.Length; k++) fx.sparks[k].GetComponent<MeshFilter>().sharedMesh = k % 2 == 1 ? art.sparkCream : art.spark[zone];
                break;
            case ShellFxKind.Collar:
                for (int k = 0; k < fx.plateCracks.Length; k++)
                    if (fx.plateCracks[k] != null) fx.plateCracks[k].GetComponent<MeshFilter>().sharedMesh = art.collarCracks[zone * ShellBurstArtSO.CollarPlates + k];
                fx.band.GetComponent<MeshFilter>().sharedMesh = art.collarBand[zone];
                break;
            case ShellFxKind.Hit:
                fx.tinkCard.GetComponent<MeshFilter>().sharedMesh = art.tink[zone];
                break;
        }
    }
}
