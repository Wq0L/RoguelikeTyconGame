using System.Collections.Generic;
using UnityEngine;

// Geç oyun içeriğinin sunumu (Bölüm 3.8): tarla olayları ve yeni boss ödülleri için görsel / ses bağlama katmanı. UIManager kurar.
// Yalnız DİNLER (FieldEventDirector.Cued, RunEffects.Cued) ve mevcut durumu okur; oynanışa hiçbir şey yazmaz. Bu nesne ya da
// bağlı prefab / malzeme / ses eksik olsa da oyun aynen çalışır.
// - Her yuvanın (LateGameSlot) görseli katalogdan gelir (LateGameArtSO, Resources/LateGameArt); boş yuva yer tutucuyla çizilir.
// - Havuz: nesneler ilk ihtiyaçta üretilir, sonrasında yeniden kullanılır. Dekor parçaları toplam
//   DecorationCapacity ile sınırlıdır: dolunca yeni dekor çizilmez ve sayılır. Müdahale hedefleri bu sınıra girmez (her zaman
//   gösterilir); hedefin kendisi zaten görsele bağlı değildir.
// - Tek Update: nabız yalnız görsel child'ın ölçeğidir; hücre başına Update, coroutine ya da Animator eklenmez. Collider yoktur.
// - Bölge, oyun görünümünde hücre sınır çizgisiyle DEĞİL hücre başına küçük bir zemin belirtisiyle gösterilir. Kesin hücre
//   çizgisi yalnız test görünümündedir (DebugOutlines; Editor Play test penceresi açar).
// Diken içerikleri katalogda bağlıdır; diğer boş yuvalar placeholder kullanır.
[DisallowMultipleComponent]
public sealed partial class LateGamePresenter : MonoBehaviour
{
    public const int DecorationCapacity = 96;
    private const float RiseSeconds = 0.5f, FlashSeconds = 0.22f;
    // Diken (24 Ekim 2026): kökün doğduğu tümsek (kökten geniş), güçlü vuruş katsayısı, vuruş / kırılma efekt ölçüleri.
    private const float ThornMoundScale = 1.8f, ThornHitStrength = 1.5f, ThornHitBurst = 1.15f, ThornBreakBurst = 1.5f;
    // Diken çıkışı: kök fırlama eğrisinin süresi, kapan dalgasının hücre başına gecikmesi (patlamanın kendisi: ThornBurst.cs).
    private const float ThornBurstSeconds = .55f, ThornTrapWave = .07f;
    // Saksısız hücrede kapan: halka modelinin tabanı (~.095) zemine otursun diye iner; saksı olmadığı için biraz dar.
    private const float ThornTrapFloorDrop = .085f, ThornTrapFloorWidth = .88f;
    // İstilacı Filiz (24 Ekim, Diken kuralları): ana filizin doğum tümseği, saksısız hücrede yayılma parçasının inişi (sürünen dal
    // halkası da saksı kenarını saracak yükseklikte modellendi, taban ~.125), yayılmada küçük toprak sıçraması.
    private const float SproutMoundScale = 1.5f, SproutPieceFloorDrop = .115f, SproutPieceFloorWidth = .88f, SproutSpreadSoil = .75f;

    // Test görünümü: aktif tarla olayının bölgelerini kesin hücre çizgisiyle de çizer. Normal oyunda kapalıdır.
    public static bool DebugOutlines;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => DebugOutlines = false;

    private sealed class Item
    {
        public Renderer[] moonRenderers;
        public GameObject root;
        public Transform visual;
        public LateGameContent content;
        public long key;
        public Vector3 scale;
        public float age, life, pulse;
        public bool rise, decoration, seen;
        public int identity;
        public float exitAge, exitLife, exitDelay;
        public float wiltDelay;
        public bool overflowTravel;
        // Diken çıkışı (24 Ekim): doğrusal yükseliş yerine baskı → patlama → iniş → oturma; riseDelay ile dalga hâlinde.
        public bool burst;
        public float riseDelay;
        public Vector3 travelFrom, travelTo;
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color"), ActiveId = Shader.PropertyToID("_Active"), PulseId = Shader.PropertyToID("_Pulse");
    // Yer tutucu renkleri: bitki yeşilinden ve zemin grisinden ayrışan doygun tonlar (olay kimliği renkle okunur).
    private static readonly Color Thorn = new(1f, 0.45f, 0.1f), ThornDark = new(0.5f, 0.22f, 0.08f), Sprout = new(0.2f, 0.85f, 0.8f),
        ShellA = new(1f, 0.9f, 0.3f), ShellB = new(.325f, .863f, 1f), Bounty = new(1f, 0.8f, 0.25f), Spark = new(0.45f, 0.9f, 1f),
        Moon = new(0.92f, 0.94f, 1f), Hot = new(1f, 0.55f, 0.25f), Metal = new(0.55f, 0.6f, 0.68f);

    // Bir olay bölgesinin yer tutucu rengi: tarladaki belirti, bölgeye bağlı hedef ve durum plakasındaki açıklama aynı rengi okur.
    public static Color ZoneColor(FieldEventKind kind, int zone) => kind switch
    {
        FieldEventKind.ThornSiege => Thorn,
        FieldEventKind.InvasiveSprout => Sprout,
        FieldEventKind.GraftedShell => zone == 0 ? ShellA : ShellB,
        _ => Bounty,
    };

    private readonly Dictionary<long, Stack<Item>> spare = new();
    private readonly List<Item> eventItems = new(), antennaItems = new(), flashes = new(), exiting = new();
    private readonly List<GridPosition> outlineCells = new();
    private LateGameArtSO art;
    private LateGameAudio soundPlayer;
    private bool artLoaded;
    private bool placementSound;
    private int decorations, shownEffectsVersion = -1;
    private FieldEvent shownEvent;
    // Test görünümü çizgisi (yalnız DebugOutlines açıkken kurulur).
    private Mesh outlineMesh;
    private Material outlineMaterial;
    private MeshRenderer outlineRenderer;
    private bool outlineDirty;

    public int ActiveDecorations => decorations;
    public int DecorationsSkipped { get; private set; }
    public int EventItems => eventItems.Count;
    public int AntennaItems => antennaItems.Count;
    public int Flashes => flashes.Count;

    public static LateGamePresenter Ensure()
    {
        var existing = FindAnyObjectByType<LateGamePresenter>();
        return existing != null ? existing : new GameObject("Late Game Presenter").AddComponent<LateGamePresenter>();
    }

    private void OnEnable()
    {
        FieldEventDirector.Cued += OnFieldCue;
        RunEffects.Cued += OnRewardCue;
    }

    private void OnDisable()
    {
        CameraFeel.Instance?.SetFrameShake(Vector2.zero);
        FieldEventDirector.Cued -= OnFieldCue;
        RunEffects.Cued -= OnRewardCue;
        Release(eventItems);
        Release(antennaItems);
        Release(flashes);
        Release(exiting);
        ClearMoonWaves();
        ClearReactor();
        ClearGreed();
        ClearOverload();
        ClearCritArcs();
        ClearMeteors();
        ClearEventStars();
        ClearThornBlasts();
        soundPlayer?.Clear();
        placementSound=false;
        shownEvent = null;
        shownEffectsVersion = -1;
        if (outlineRenderer != null) outlineRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        foreach(var material in moonFadeMaterials.Values)if(material!=null)Destroy(material);
        moonFadeMaterials.Clear();
        DestroyCritArcAssets();
        DestroyMeteorAssets();
        DestroyGreedAssets();
        DestroyOverloadAssets();
        DestroyEventFeelAssets();
        PlaceholderShapes.Release();
        if (outlineMesh != null) Destroy(outlineMesh);
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    private LateGameArtSO Art
    {
        get
        {
            if (!artLoaded) { art = Resources.Load<LateGameArtSO>(LateGameArtSO.ResourcePath); artLoaded = true; }
            return art;
        }
    }

    // ---------------------------------------------------------------- havuz
    private static PlaceholderShapes.Shape ShapeOf(LateGameSlot slot) => slot switch
    {
        // Müdahale hedefleri kalın bir "iğne"dir (direk + tepe): kalabalık tarlada bitkilerin üstünden görünür.
        LateGameSlot.ThornRoot or LateGameSlot.SproutMain or LateGameSlot.ShellNode or LateGameSlot.Antenna or LateGameSlot.PlacementMarker => PlaceholderShapes.Shape.Post,
        LateGameSlot.ThornPiece or LateGameSlot.SproutPiece => PlaceholderShapes.Shape.Spike,
        LateGameSlot.GroundSign or LateGameSlot.BountyMarker or LateGameSlot.PlacementCell or LateGameSlot.ShellCover or LateGameSlot.BreakBurst or LateGameSlot.BloomBurst or
            LateGameSlot.Pulse => PlaceholderShapes.Shape.Orb,
        LateGameSlot.CrescentBlade => PlaceholderShapes.Shape.Blade,
        _ => PlaceholderShapes.Shape.Beam,
    };

    private Item Rent(LateGameSlot slot, Color color, bool decoration, LateGameVariant variant)
    {
        if (decoration && decorations >= DecorationCapacity) { DecorationsSkipped++; return null; }
        Color32 c = color;
        long key = ((long)variant << 48) | ((long)slot << 32) | ((long)c.r << 16) | ((long)c.g << 8) | c.b;
        Item item = spare.TryGetValue(key, out Stack<Item> stack) && stack.Count > 0 ? stack.Pop() : Create(slot, color, key, variant);
        item.decoration = decoration;
        item.age = 0f;
        item.overflowTravel = false;
        item.travelFrom = item.travelTo = Vector3.zero;
        item.life = 0f;
        item.pulse = 0f;
        item.rise = false;
        item.burst = false;
        item.riseDelay = 0f;
        item.scale = Vector3.one;
        item.root.transform.localScale = Vector3.one;
        if (item.visual != null) item.visual.localScale = Vector3.one;
        item.root.SetActive(true);
        // Show is called only after position, rotation and scale have been assigned.
        if (decoration) decorations++;
        return item;
    }

    private Item Create(LateGameSlot slot, Color color, long key, LateGameVariant variant)
    {
        var item = new Item { key = key };
        GameObject prefab = Art != null ? Art.Prefab(slot, variant) : null;
        if (prefab != null)
        {
            item.root = Instantiate(prefab, transform);
            item.content = item.root.GetComponent<LateGameContent>();
            item.visual = item.content != null ? item.content.visual : null;
        }
        else
        {
            item.root = new GameObject(slot.ToString());
            item.root.transform.SetParent(transform, false);
            item.visual = PlaceholderShapes.Create(ShapeOf(slot), color, "Visual", item.root.transform).transform;
        }
        item.root.SetActive(false);
        return item;
    }

    private void Release(Item item)
    {
        if (item == null || item.root == null) return;
        if (item.content != null) item.content.ResetForPool();
        item.root.SetActive(false);
        if (item.decoration) decorations--;
        if (!spare.TryGetValue(item.key, out Stack<Item> stack)) spare.Add(item.key, stack = new Stack<Item>());
        stack.Push(item);
    }

    private void Release(List<Item> items)
    {
        for (int i = 0; i < items.Count; i++) Release(items[i]);
        items.Clear();
    }

    // Detached tails own no event reference and cannot affect a later event. Hard cleanup skips tails.
    private void Retire(Item item, float delay = 0f)
    {
        if (item == null) return;
        if (item.content == null || item.content.hideSeconds <= 0f) { Release(item); return; }
        item.content.Hide();
        item.exitAge = 0f;
        item.exitDelay = Mathf.Clamp(delay, 0f, .5f);
        item.exitLife = Mathf.Clamp(item.content.hideSeconds, 0f, 2f);
        // Snapshot the current visual scale; gameplay already removed this item.
        if (item.visual != null) item.scale = item.visual.localScale;
        exiting.Add(item);
    }

    private static Vector3 Top(GridSystem grid, GridPosition cell)
    {
        GroundCell ground = FieldCells.Ground(grid, cell);
        if (ground == null) return grid.GetWorldPosition(cell.x, cell.z);
        Renderer renderer = ground.GroundRenderer;
        Vector3 position = ground.transform.position;
        position.y = (renderer != null ? renderer.bounds.max.y : position.y) + 0.03f;
        return position;
    }

    private Item Place(List<Item> into, LateGameSlot slot, Color color, Vector3 position, Vector3 scale, float pulse, bool decoration, LateGameVariant variant = LateGameVariant.Default)
    {
        Item item = Rent(slot, color, decoration, variant);
        if (item == null) return null;
        item.root.transform.SetPositionAndRotation(position, Quaternion.identity);
        item.scale = scale;
        item.pulse = pulse;
        if (item.visual != null) item.visual.localScale = scale;
        item.content?.Show();
        into.Add(item);
        return item;
    }

    // Kısa ömürlü darbe (tek nokta).
    private void Flash(LateGameSlot slot, Color color, Vector3 position, float size, LateGameVariant variant = LateGameVariant.Default)
    {
        Item item = Place(flashes, slot, color, position + Vector3.up * 0.6f, Vector3.one * size, 0f, true, variant);
        if (item != null) item.life = Art != null ? Art.Lifetime(slot, variant) : FlashSeconds;
    }

    // Kısa ömürlü ışın (iki nokta arası): ışın şekli +Z boyunca birimdir; uzunluk ölçekle verilir.
    private void Bolt(LateGameSlot slot, Color color, Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        float length = delta.magnitude;
        if (length < 0.05f) { Flash(LateGameSlot.Pulse, color, from, 0.7f); return; }
        Item item = Place(flashes, slot, color, from + Vector3.up * 0.9f, new Vector3(1f, 1f, length), 0f, true);
        if (item == null) return;
        item.root.transform.rotation = Quaternion.LookRotation(delta / length, Vector3.up);
        item.life = Art != null ? Art.Lifetime(slot) : FlashSeconds;
    }

    // Immutable hit positions. Gold arc + landing star live in LateGamePresenter.CritArc.cs; Overflow keeps its existing ArcBolt.
    private void CritArc(RewardCue cue)
    {
        if ((cue.To - cue.From).sqrMagnitude < .0025f) return;
        ShowCritArc(cue.From, cue.To);
        Play(LateGameSound.CritArc, LateGameVariant.CritArc);
    }

    private void Play(LateGameSound sound, LateGameVariant variant = LateGameVariant.Default)
    {
        if (Art == null) return;
        soundPlayer ??= new LateGameAudio(gameObject);
        soundPlayer.Play(Art.Sound(sound, variant));
    }

    private static LateGameVariant Variant(FieldEventKind kind, int zone = 0) => kind switch
    {
        FieldEventKind.ThornSiege => LateGameVariant.Thorn,
        FieldEventKind.InvasiveSprout => LateGameVariant.Sprout,
        FieldEventKind.GraftedShell => zone == 0 ? LateGameVariant.ShellA : LateGameVariant.ShellB,
        FieldEventKind.WanderingBounty => LateGameVariant.Bounty,
        _ => LateGameVariant.Default,
    };

    // ---------------------------------------------------------------- tarla olayları
    private void OnFieldCue(FieldEventCue cue)
    {
        FieldEvent source = cue.Event;
        GridSystem grid = FieldCells.Grid;
        if (source == null || grid == null) return;
        if (cue.Kind == FieldEventCueKind.Cleaned)
        {
            if (shownEvent == source) { CameraFeel.Instance?.SetFrameShake(Vector2.zero); Release(eventItems); Release(exiting); Release(flashes); ClearThornBlasts(); soundPlayer?.Clear(); shownEvent = null; outlineDirty = true; }
            return;
        }
        if (shownEvent != source) { Release(eventItems); Release(exiting); }
        Vector3 impact = Top(grid, cue.Cell) + Vector3.up * .6f;
        // Hit the existing instance before reconciliation (the final hit also removes the target).
        bool thornEvent = source.Kind == FieldEventKind.ThornSiege, cutEvent = CutTargetEvent(source.Kind);
        if (cue.Kind == FieldEventCueKind.TargetHit || cue.Kind == FieldEventCueKind.TargetBroken)
            foreach (Item item in eventItems) if (item.identity == 10000 + cue.Index)
            {
                if (item.content != null && item.content.topPoint != null) impact = item.content.topPoint.position;
                if (cutEvent && item.content != null) item.content.Hit(RecoilFrom(item.root.transform.position), ThornHitStrength);
                else item.content?.Hit();
            }
        shownEvent = source;
        LateGameVariant variant = Variant(source.Kind, source.Kind == FieldEventKind.GraftedShell ? cue.Index : 0);
        Sync(source, grid, cue.Kind == FieldEventCueKind.Emerged);
        outlineDirty = true;
        switch (cue.Kind)
        {
            case FieldEventCueKind.Prepared: Play(LateGameSound.EventPrepared, variant); if (cutEvent) PrewarmThornBlasts(); break;
            case FieldEventCueKind.Emerged:
                if (source.Kind == FieldEventKind.ThornSiege || source.Kind == FieldEventKind.InvasiveSprout)
                {
                    Play(LateGameSound.EventActivated, variant);
                    // Ana bitkinin fırladığı an Blender'da tasarlanan toprak patlaması (LateGamePresenter.ThornBurst.cs; Diken ve Filiz).
                    for (int i = 0; i < source.Targets.Count; i++)
                        ThornBlastBurst(source.Targets[i].Cell, SoilTop(grid, source.Targets[i].Cell));
                }
                break;
            case FieldEventCueKind.Activated:
                if (source.Kind != FieldEventKind.ThornSiege && source.Kind != FieldEventKind.InvasiveSprout) Play(LateGameSound.EventActivated, variant);
                break;
            case FieldEventCueKind.TargetHit:
                Flash(source.Kind == FieldEventKind.ThornSiege || source.Kind == FieldEventKind.InvasiveSprout || source.Kind == FieldEventKind.GraftedShell ? LateGameSlot.HitBurst : LateGameSlot.BreakBurst, Hot, impact - Vector3.up * .6f,
                    thornEvent ? ThornHitBurst : source.Kind == FieldEventKind.InvasiveSprout ? 1f : .5f, variant);
                if (cutEvent) EventStarAt(impact, .7f, .07f);
                Play(LateGameSound.TargetHit, variant);
                break;
            case FieldEventCueKind.TargetBroken:
                Flash(LateGameSlot.BreakBurst, Hot, impact - Vector3.up * .6f, cutEvent ? ThornBreakBurst : 1.1f, variant);
                if (cutEvent) EventStarAt(impact, 1.25f, .11f);
                Play(LateGameSound.TargetBroken, variant);
                break;
            case FieldEventCueKind.Spread:
                // Yayılma: yeni parçanın hücresinde, ortada küçük toprak sıçraması (parça patlama eğrisiyle fırlar).
                Flash(LateGameSlot.GroundBurst, Sprout, Top(grid, cue.Cell) - Vector3.up * .48f, SproutSpreadSoil, variant);
                Play(LateGameSound.Spread, variant);
                break;
            case FieldEventCueKind.ZoneClosed:
                if(source.Kind==FieldEventKind.GraftedShell)
                {
                    Play(LateGameSound.ShellChoice,variant);Play(LateGameSound.ShellClose,variant);
                    var saved=source.Zone(1-cue.Index);
                    foreach(var cell in source.Zone(cue.Index))Flash(LateGameSlot.GroundBurst,ZoneColor(source.Kind,cue.Index),Top(grid,cell)-Vector3.up*.48f,.8f,variant);
                    foreach(var cell in saved)Flash(LateGameSlot.GroundBurst,ShellB,Top(grid,cell)+new Vector3(-.36f*FieldCells.CellSize,-.48f,-.36f*FieldCells.CellSize),.45f,Variant(source.Kind,1-cue.Index));
                }
                break;
            case FieldEventCueKind.ZoneOpened:
                if(source.Kind==FieldEventKind.GraftedShell)
                {
                    Play(LateGameSound.ShellOpen,variant);
                    foreach(var cell in source.Zone(cue.Index))Flash(LateGameSlot.GroundBurst,ZoneColor(source.Kind,cue.Index),Top(grid,cell)-Vector3.up*.48f,.6f,variant);
                }
                break;
            case FieldEventCueKind.Progress:
                if(source is WanderingBountyEvent)
                {Flash(LateGameSlot.BountyProgress,Bounty,Top(grid,cue.Cell),.7f,variant);Play(LateGameSound.BountyProgress,variant);}
                break;
            case FieldEventCueKind.Bloom:
                if(source is WanderingBountyEvent bounty)
                {
                    var cells=bounty.BloomCells;var area=bounty.Zone(0);
                    Vector3 from=Top(grid,area[area.Count/2])+Vector3.up*1.1f;
                    foreach(var cell in cells)
                    {
                        Vector3 to=Top(grid,cell)+Vector3.up*.2f;
                        var flow=Place(flashes,LateGameSlot.BountyFlow,Bounty,from,Vector3.one,0,true,variant);
                        if(flow!=null){flow.life=.85f;flow.content?.bounty?.FlyTo(to);}
                        Flash(LateGameSlot.BloomBurst,Bounty,to-Vector3.up*.6f,1f,variant);
                    }
                    // Frequent accelerated spawns stay quiet; harvest response is already rate limited.
                }
                break;
            case FieldEventCueKind.Resolved: Play(LateGameSound.EventResolved, variant); break;
        }
    }

    // Reconcile stable cell/target identities. A hit never rebuilds surviving roots or restarts Show.
    private Item EventItem(int identity, LateGameSlot slot, Color color, Vector3 position, Vector3 scale,
        float pulse, bool decoration, LateGameVariant variant, bool rise = false)
    {
        Item item = null;
        foreach (Item existing in eventItems) if (existing.identity == identity) { item = existing; break; }
        if (item == null)
        {
            item = Place(eventItems, slot, color, position, scale, pulse, decoration, variant);
            if (item == null) return null;
            item.identity = identity;
            item.rise = rise;
        }
        item.seen = true;
        item.scale = scale;
        item.pulse = pulse;
        return item;
    }

    private void Sync(FieldEvent source, GridSystem grid, bool rise)
    {
        if(source is WanderingBountyEvent bounty){SyncBounty(bounty,grid);return;}
        if(source is GraftedShellEvent shell){SyncShell(shell,grid,rise);return;}
        foreach (Item item in eventItems) item.seen = false;
        FieldEventPhase phase = source.Phase;
        bool visible = phase != FieldEventPhase.Waiting && phase != FieldEventPhase.Done && phase != FieldEventPhase.Resolving;
        bool active = phase == FieldEventPhase.Active;
        float cellSize = FieldCells.CellSize;
        var corner = new Vector3(-.36f * cellSize, 0f, -.36f * cellSize);
        if (visible) for (int z = 0; z < source.ZoneCount; z++)
        {
            IReadOnlyList<GridPosition> cells = source.Zone(z);
            bool closed = source.ZoneClosed(z);
            Color color = ZoneColor(source.Kind, z);
            LateGameVariant variant = Variant(source.Kind, z);
            for (int i = 0; i < cells.Count; i++)
            {
                int id = z * 100000 + (cells[i].x * 256 + cells[i].z) * 4;
                Vector3 ground = Top(grid, cells[i]);
                bool thorn = source.Kind == FieldEventKind.ThornSiege;
                bool sprout = source.Kind == FieldEventKind.InvasiveSprout;
                // Diken (24 Ekim 2026, kullanıcı yönü): tümsek ana bitkinin doğduğu yerdir — yalnız kök hücresinde, ortada; hazırlıkta
                // kabarır, çıkışta kök içinden yükselir, aktif olunca çöküp kalkar (uzlaştırmada çıkış animasyonu). Kökün altına kapan
                // konmaz; kapanlar yalnız etkilenen (diğer) bölge hücrelerinde.
                if (thorn)
                {
                    bool potted = IsPotted(grid, cells[i]);
                    if (IsTargetCell(source, cells[i]))
                    {
                        // Tümsek yalnız hazırlıkta; çıkış anında (kök fırlarken) uzlaştırmada dağılır.
                        if (phase == FieldEventPhase.Preparing)
                            EventItem(id, LateGameSlot.GroundSign, color, ground, Vector3.one * ThornMoundScale, .07f, true, variant)
                                ?.content?.SetPreparing(true);
                        continue;
                    }
                    if (!closed && phase != FieldEventPhase.Emerging) continue;
                    // Kapan halkası saksı kenarını saracak yükseklikte modellendi: saksısız hücrede zemine iner ve biraz daralır.
                    Vector3 trapAt = potted ? ground : ground + Vector3.down * ThornTrapFloorDrop;
                    float trapWidth = cellSize * .5f * (potted ? 1f : ThornTrapFloorWidth);
                    Item trap = EventItem(id + 2, LateGameSlot.ThornPiece, ThornDark, trapAt, new Vector3(trapWidth, 1f, trapWidth), .035f, true, variant, true);
                    if (trap != null)
                    {
                        trap.root.transform.rotation = Quaternion.Euler(0f, (cells[i].x * 73 + cells[i].z * 37) % 360, 0f);
                        if (trap.rise && trap.age == 0f) { trap.burst = true; trap.riseDelay = .12f + ThornTrapWave * CellsFromTarget(source, cells[i]); }
                    }
                    continue;
                }
                // Filiz (Diken kuralları): tümsek yalnız ana filizin hücresinde; ana filizin altına sürünen dal konmaz; yayılma parçaları
                // yalnız etkilenen hücrelerde, saksısız hücrede zemine iner; her yeni parça patlama eğrisiyle fırlar.
                if (sprout)
                {
                    bool potted = IsPotted(grid, cells[i]);
                    if (IsTargetCell(source, cells[i]))
                    {
                        if (phase == FieldEventPhase.Preparing)
                            EventItem(id, LateGameSlot.GroundSign, color, ground, Vector3.one * SproutMoundScale, .07f, true, variant)
                                ?.content?.SetPreparing(true);
                        continue;
                    }
                    if (!active) continue;
                    Vector3 pieceAt = potted ? ground : ground + Vector3.down * SproutPieceFloorDrop;
                    float pieceWidth = cellSize * .5f * (potted ? 1f : SproutPieceFloorWidth);
                    Item runner = EventItem(id + 2, LateGameSlot.SproutPiece, color, pieceAt, new Vector3(pieceWidth, 1f, pieceWidth), .035f, true, variant, true);
                    if (runner != null)
                    {
                        runner.wiltDelay = Mathf.Min(i * .045f, .32f);
                        if (runner.rise && runner.age == 0f) runner.burst = true;
                    }
                    continue;
                }
                LateGameSlot slot = source.Kind == FieldEventKind.WanderingBounty ? LateGameSlot.BountyMarker : LateGameSlot.GroundSign;
                // Legacy floating markers remain fallback; authored ground content sits on soil.
                bool grounded = slot == LateGameSlot.GroundSign && Art != null && Art.Prefab(slot, variant) != null;
                Item groundItem = EventItem(id, slot, color, ground + (grounded ? corner : Vector3.up * 2.2f),
                    Vector3.one * (grounded ? (sprout ? .65f : 1f) : active ? .5f : .34f), active ? .04f : .07f, true, variant);
                groundItem?.content?.SetPreparing(!active);
                if (groundItem != null) groundItem.wiltDelay = sprout ? Mathf.Min(i * .045f, .32f) : 0f;
                if (thorn && Art != null && Art.Prefab(LateGameSlot.OverheadWarning, variant) != null)
                    EventItem(id + 1, LateGameSlot.OverheadWarning, color, ground + Vector3.up * 2.4f,
                        Vector3.one, .04f, true, variant);
                if (!closed && source.Kind != FieldEventKind.InvasiveSprout && !(thorn && phase == FieldEventPhase.Emerging)) continue;
                if (source.Kind == FieldEventKind.InvasiveSprout && !active) continue;
                LateGameSlot piece = thorn ? LateGameSlot.ThornPiece : source.Kind == FieldEventKind.GraftedShell ? LateGameSlot.ShellCover : LateGameSlot.SproutPiece;
                Item decoration = EventItem(id + 2, piece, thorn ? ThornDark : color, ground + ((thorn || sprout) ? Vector3.zero : corner),
                    (thorn || sprout) ? new Vector3(cellSize * .5f, 1f, cellSize * .5f) : new Vector3(.8f, 1.3f, .8f), .035f, true, variant, true);
                if (decoration != null) decoration.wiltDelay = sprout ? Mathf.Min(i * .045f, .32f) : 0f;
                if (decoration != null && thorn) decoration.root.transform.rotation = Quaternion.Euler(0f, (cells[i].x * 73 + cells[i].z * 37) % 360, 0f);
            }
        }
        if (visible && phase != FieldEventPhase.Preparing) for (int i = 0; i < source.Targets.Count; i++)
        {
            InterventionTarget target = source.Targets[i];
            if (target.Broken) continue;
            LateGameSlot slot = source.Kind == FieldEventKind.ThornSiege ? LateGameSlot.ThornRoot : source.Kind == FieldEventKind.InvasiveSprout ? LateGameSlot.SproutMain : LateGameSlot.ShellNode;
            LateGameVariant variant = Variant(source.Kind, i);
            bool authored = Art != null && Art.Prefab(slot, variant) != null;
            float health = Mathf.Lerp(.55f, 1f, target.Remaining / (float)target.Durability);
            Item item = EventItem(10000 + i, slot, ZoneColor(source.Kind, i), Top(grid, target.Cell),
                authored ? Vector3.one : new Vector3(2f, 1.5f * health, 2f), target.Hittable ? .035f : 0f, false, variant, rise);
            if (item != null && item.rise && item.age == 0f && CutTargetEvent(source.Kind)) item.burst = true;
            item?.content?.SetDamage(1f - target.Remaining / (float)target.Durability);
        }
        for (int i = eventItems.Count - 1; i >= 0; i--) if (!eventItems[i].seen)
        { Retire(eventItems[i], source.Kind == FieldEventKind.InvasiveSprout ? eventItems[i].wiltDelay : 0f); eventItems.RemoveAt(i); }
    }

    // Kesilecek ana bitkisi olan olaylar (Diken, Filiz): tümsek = ana bitkinin doğum yeri, altında kapan / parça yok, güçlü vuruş,
    // patlayarak çıkış ve toprak patlaması (24 Ekim, kullanıcının Diken'de onayladığı kurallar).
    private static bool CutTargetEvent(FieldEventKind kind) => kind == FieldEventKind.ThornSiege || kind == FieldEventKind.InvasiveSprout;

    // En yakın köke uzaklık (hücre, Chebyshev): kapan dalgası kökten dışarı yayılır.
    private static int CellsFromTarget(FieldEvent source, GridPosition cell)
    {
        int best = int.MaxValue;
        for (int i = 0; i < source.Targets.Count; i++)
            best = Mathf.Min(best, Mathf.Max(Mathf.Abs(source.Targets[i].Cell.x - cell.x), Mathf.Abs(source.Targets[i].Cell.z - cell.z)));
        return best == int.MaxValue ? 0 : best;
    }

    // Diken çıkış eğrisi (t: yükselişin başından sn). Döner: dikey ölçek, yatay ölçek, yer altı → üstü (1 = yerinde, >1 sıçrama).
    // 0–.10 baskı (uç görünür) · .10–.24 patlama (uzar, incelir, sıçrar) · .24–.38 iniş (basılır, genişler) · .38–.55 oturma.
    private static void ThornBurst(float t, out float height, out float across, out float lift)
    {
        if (t <= 0f) { height = 0f; across = 1f; lift = 0f; return; }
        if (t < .10f) { float u = t / .10f; height = .25f * u; across = 1f; lift = .25f * u; return; }
        if (t < .24f)
        {
            float u = (t - .10f) / .14f, e = 1f - (1f - u) * (1f - u) * (1f - u);
            height = Mathf.Lerp(.25f, 1.3f, e); across = Mathf.Lerp(1f, .82f, e); lift = Mathf.Lerp(.25f, 1.25f, e); return;
        }
        if (t < .38f)
        {
            float u = (t - .24f) / .14f, s = u * u * (3f - 2f * u);
            height = Mathf.Lerp(1.3f, .78f, s); across = Mathf.Lerp(.82f, 1.2f, s); lift = Mathf.Lerp(1.25f, 1f, s); return;
        }
        if (t < ThornBurstSeconds)
        {
            float u = (t - .38f) / (ThornBurstSeconds - .38f), s = u * u * (3f - 2f * u);
            height = Mathf.Lerp(.78f, 1f, s) + .06f * Mathf.Sin(u * Mathf.PI * 2f) * (1f - u); across = Mathf.Lerp(1.2f, 1f, s); lift = 1f; return;
        }
        height = across = lift = 1f;
    }

    // Patlama anı sarsıntısı: fırlamada tepe yapar, inişle söner.
    private static float ThornBurstShake(float t) => t <= .08f || t >= .45f ? 0f : t < .2f ? (t - .08f) / .12f : 1f - (t - .2f) / .25f;

    // Hücrede yerleşik saksı var mı (24 Ekim, kullanıcı: kapan saksı varsa saksıya, yoksa zemine göre konum alsın).
    // Kök ve tümsek her iki durumda da hücre merkezinde, zemin yüksekliğinde (saksılıda saksının içinden çıkar).
    private static bool IsPotted(GridSystem grid, GridPosition cell)
    {
        GridObject go = grid.GetGridObject(cell);
        PlanterBrain planter = go != null ? go.GetPlanterBrain() : null;
        return planter != null && planter.IsPlaced;
    }

    private static bool IsTargetCell(FieldEvent source, GridPosition cell)
    {
        for (int i = 0; i < source.Targets.Count; i++) if (source.Targets[i].Cell.x == cell.x && source.Targets[i].Cell.z == cell.z) return true;
        return false;
    }

    private void SyncBounty(WanderingBountyEvent source,GridSystem grid)
    {
        foreach(var item in eventItems)item.seen=false;
        bool visible=BountyPlantTint.IsVisible(source);
        if(visible)foreach(var cell in source.Zone(0))
        {
            int id=(cell.x*256+cell.z)*4;
            EventItem(id,LateGameSlot.BountyMarker,Bounty,Top(grid,cell),new Vector3(FieldCells.CellSize*.5f,1,FieldCells.CellSize*.5f),.012f,true,LateGameVariant.Bounty,true);
        }
        if(visible && source.Zone(0).Count>0)
        {
            var cells=source.Zone(0);var first=cells[0];var last=cells[cells.Count-1];
            var center=(Top(grid,first)+Top(grid,last))*.5f;
            var curtain=EventItem(-200,LateGameSlot.BountyCurtain,Bounty,center,
                new Vector3((last.x-first.x+1)*FieldCells.CellSize,1,(last.z-first.z+1)*FieldCells.CellSize),0,true,LateGameVariant.Bounty);
            if(curtain!=null)curtain.root.transform.position=center;
        }
        for(int i=eventItems.Count-1;i>=0;i--)if(!eventItems[i].seen)
        {
            // A moved-away cell loses its active sign immediately. Never display a false moving zone.
            if(visible)Release(eventItems[i]);else Retire(eventItems[i]);
            eventItems.RemoveAt(i);
        }
    }

    private void SyncShell(GraftedShellEvent source,GridSystem grid,bool rise)
    {
        foreach(var item in eventItems)item.seen=false;
        bool visible=source.Phase!=FieldEventPhase.Waiting&&source.Phase!=FieldEventPhase.Done&&source.Phase!=FieldEventPhase.Resolving;
        if(visible)for(int z=0;z<2;z++)
        {
            var variant=Variant(source.Kind,z);var color=ZoneColor(source.Kind,z);
            // Saved side leaves immediately through a visual opening tail; gameplay was already released.
            if(source.Decided&&source.SavedZone==z)continue;
            var cells=source.Zone(z);
            for(int i=0;i<cells.Count;i++)
            {
                var cell=cells[i];int id=z*100000+(cell.x*256+cell.z)*4+2;
                var item=EventItem(id,LateGameSlot.ShellCover,color,Top(grid,cell),new Vector3(FieldCells.CellSize*.5f,1f,FieldCells.CellSize*.5f),0f,true,variant,true);
                item?.content?.shell?.SetState(source.ZoneClosed(z)?1:0);
            }
            if(!source.Decided)
            {
                var target=source.Targets[z];
                if(target.Broken)continue;
                var item=EventItem(10000+z,LateGameSlot.ShellNode,color,Top(grid,target.Cell),Vector3.one,source.Phase==FieldEventPhase.Preparing?.018f:.03f,false,variant,true);
                if(item!=null)
                {
                    item.content?.shell?.SetState(0);item.content?.SetDamage(1f-target.Remaining/(float)target.Durability);
                    if(rise){item.rise=true;item.age=0;}
                }
            }
        }
        for(int i=eventItems.Count-1;i>=0;i--)if(!eventItems[i].seen)
        {Retire(eventItems[i]);eventItems.RemoveAt(i);}
    }
    // ---------------------------------------------------------------- boss ödülleri
    private void OnRewardCue(RewardCue cue)
    {
        switch (cue.Kind)
        {
            case RewardCueKind.AntennaPlaced:
                if(RoundManager.Instance==null||!RoundManager.Instance.IsRoundActive){soundPlayer?.Clear();placementSound=true;}
                Play(LateGameSound.AntennaPlaced,LateGameVariant.Antenna);
                shownEffectsVersion = -1;
                break;
            case RewardCueKind.AntennaRemoved:
                shownEffectsVersion = -1;
                break;
            case RewardCueKind.AntennaPulse: AntennaShot(cue,false,false); break;
            case RewardCueKind.LinePulse: AntennaShot(cue,true,false); break;
            case RewardCueKind.LineExtraPulse: AntennaShot(cue,true,true); break;
            case RewardCueKind.CircuitReturn: AntennaReturn(cue); break;
            case RewardCueKind.AntennaContact:
            case RewardCueKind.LineContact:
            case RewardCueKind.LineExtraContact:
                Flash(LateGameSlot.AntennaContact,Spark,cue.From,.7f,cue.Kind==RewardCueKind.LineExtraContact?LateGameVariant.Circuit:LateGameVariant.Antenna);break;
            case RewardCueKind.CrescentLaunch: Play(LateGameSound.CrescentLaunch,LateGameVariant.Crescent); break;
            case RewardCueKind.CrescentStep: break; // A persistent model reads the actual wave clock below.
            case RewardCueKind.CrescentGrow: Play(LateGameSound.MoonGrowth,LateGameVariant.Crescent); break;
            case RewardCueKind.FullMoon: Play(LateGameSound.FullMoon,LateGameVariant.Crescent); break;
            case RewardCueKind.MoonContact:
            case RewardCueKind.MoonReturnContact:
                Flash(LateGameSlot.MoonContact,Moon,cue.From,.55f,cue.Kind==RewardCueKind.MoonReturnContact?LateGameVariant.MoonReturn:LateGameVariant.Crescent);break;
            case RewardCueKind.CritArc:
                CritArc(cue);
                break;
            case RewardCueKind.Overflow: Overflow(cue); break;
            // Hasat Yankısı meteoru (yer tutucu): LateGamePresenter.Meteor.cs. Eski lila motif bağları katalogda kalır, kullanılmaz.
            case RewardCueKind.HarvestEchoPrepared: ShowMeteor(cue); break;
            case RewardCueKind.HarvestEcho: ResolveMeteor(cue); break;
            case RewardCueKind.CritBurst: CritBurst(cue); break;
            case RewardCueKind.ReactorOn:
                Flash(LateGameSlot.Pulse, Moon, cue.From, .65f, LateGameVariant.Reactor);
                Play(LateGameSound.ReactorOn, LateGameVariant.Reactor);
                break;
            case RewardCueKind.GreedReady: Play(LateGameSound.GreedReady, LateGameVariant.Greed); break;
            case RewardCueKind.GreedSpent: GreedUsed(cue); break;
            // Açgözlü Hasat ağzı: LateGamePresenter.Greed.cs. Eski amber diş / kapanma nabzı bağları katalogda kalır, kullanılmaz.
            case RewardCueKind.GreedLost: GreedLost(cue); break;
            // Aşırı Yükleme rozeti: LateGamePresenter.Overload.cs. Eski hazırlık / Pulse(Overload) bağları katalogda kalır, kullanılmaz.
            case RewardCueKind.OverloadPrepared:
                SyncOverload(); Play(LateGameSound.OverloadPrepared, LateGameVariant.Overload); break;
            case RewardCueKind.OverloadCancelled: SyncOverload(); break;
            case RewardCueKind.Overload: OverloadReleased(cue); break;
        }
    }

    // Antenler: alınmış etkinin o anki durumundan (konumlar açık alanın o anki kenarından çözülür).
    private void SyncAntennas()
    {
        foreach(var old in antennaItems)old.seen=false;
        RunEffects effects = RunEffects.Instance;
        AntennaEffect antennas = effects != null ? effects.Find<AntennaEffect>() : null;
        GridSystem grid = FieldCells.Grid;
        if (antennas == null || grid == null || !FieldCells.TryBounds(out FieldBounds bounds)) {Release(antennaItems);return;}
        for (int i = 0; i < antennas.Count; i++)
        {
            int slot = antennas.Slot(i);
            if (!AntennaSlots.IsValid(bounds, slot)) continue;
            Vector3 at = AntennaSlots.World(grid, bounds, slot);
            at.y = Top(grid, AntennaSlots.Anchor(bounds, slot)).y;
            Item item=null;
            foreach(var old in antennaItems)if(old.identity==slot){item=old;break;}
            if(item==null){item=Place(antennaItems,LateGameSlot.Antenna,Metal,at,Vector3.one,0f,false,LateGameVariant.Antenna);if(item!=null)item.identity=slot;}
            if (item == null) continue;
            item.seen=true;item.root.transform.position=at;
            Vector2Int facing = AntennaSlots.Facing(slot);
            item.root.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
        }
        for(int i=antennaItems.Count-1;i>=0;i--)if(!antennaItems[i].seen){Release(antennaItems[i]);antennaItems.RemoveAt(i);}
    }

    // ---------------------------------------------------------------- kare adımı
    private void LateUpdate()
    {
        // PlantJuice updates the model's scale in Update. Submit the matching final pose.
        BountyPlantTint.Draw(FieldEventDirector.Instance!=null?FieldEventDirector.Instance.Current as WanderingBountyEvent:null);
        SyncMoonWaves();
        SyncReactor();
        SyncGreed();
        SyncOverload();
        SyncCritArcs();
        SyncMeteors();
    }

    private void CritBurst(RewardCue cue)
    {
        if (cue.Radius <= 0f) return;
        var item = Place(flashes, LateGameSlot.Pulse, Hot, cue.From + Vector3.up * .65f,
            new Vector3(cue.Radius, 1f, cue.Radius), 0f, true, LateGameVariant.CritBurst);
        if (item != null) item.life = Art != null ? Art.Lifetime(LateGameSlot.Pulse, LateGameVariant.CritBurst) : .32f;
        Play(LateGameSound.CritBurst, LateGameVariant.CritBurst);
    }

    private void Update()
    {
        RunEffects effects = RunEffects.Instance;
        int version = effects != null ? effects.Version : -1;
        if (version != shownEffectsVersion) { shownEffectsVersion = version; SyncAntennas(); }

        bool running = GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        bool placementState=GameManager.Instance!=null&&(GameManager.Instance.CurrentState==GameStates.RoundChoice||GameManager.Instance.CurrentState==GameStates.RoundEnd);
        if(!placementState)placementSound=false;
        if (RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) { Release(flashes); Release(exiting); ClearEventStars(); ClearThornBlasts(); if(!placementSound)soundPlayer?.Clear(); }
        float delta = running ? Time.deltaTime : 0f;
        StepAntennas(running||placementState?Time.deltaTime:0f,running);
        soundPlayer?.Step(running||placementSound?Time.deltaTime:0f, (running||placementSound) && Time.timeScale > 0f);
        if(placementSound&&soundPlayer!=null&&soundPlayer.ActiveVoices==0)placementSound=false;
        for (int i = exiting.Count - 1; i >= 0; i--)
        {
            Item item = exiting[i];
            item.exitAge += delta;
            if (item.exitAge < item.exitDelay) continue;
            float exitTime = item.exitAge - item.exitDelay;
            if (exitTime >= item.exitLife) { Release(item); exiting.RemoveAt(i); continue; }
            float t = 1f - exitTime / item.exitLife;
            if (item.visual != null) item.visual.localScale = Vector3.Scale(item.scale, new Vector3(Mathf.Sqrt(t), t * t, Mathf.Sqrt(t)));
            item.content?.Step(delta, item.age + exitTime, 1f, 1f - t);
        }
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            Item flash = flashes[i];
            flash.age += delta;
            if (flash.age >= flash.life)
            {
                Release(flash);
                flashes.RemoveAt(i);
                continue;
            }
            flash.content?.Step(delta, flash.age);
            if (flash.overflowTravel)
                flash.root.transform.position = Vector3.Lerp(flash.travelFrom, flash.travelTo, Mathf.Clamp01(flash.age / (flash.life * .8f)));
            float fade = 1f - flash.age / flash.life;
            if (flash.visual != null && flash.content?.critBurst == null) flash.visual.localScale = new Vector3(flash.scale.x * fade, flash.scale.y * fade, flash.scale.z);
        }
        SyncEventStars(delta);
        ThornBlastLeadIn(shownEvent, FieldCells.Grid);
        SyncThornBlasts(delta);
        float emergeShake = 0f;
        if (eventItems.Count > 0)
        {
            float wave = Mathf.Sin(Time.time * 5f);
            for (int i = 0; i < eventItems.Count; i++)
            {
                Item item = eventItems[i];
                if (item.visual == null) continue;
                float scale = 1f + item.pulse * wave;
                float height = 1f, across = 1f, lift = 1f;
                item.age += delta;
                if (item.rise)
                {
                    float t = item.age - item.riseDelay;
                    if (item.burst) { ThornBurst(t, out height, out across, out lift); if (t >= ThornBurstSeconds) item.rise = false; }
                    else { height = lift = Mathf.Clamp01(t / RiseSeconds); if (height >= 1f) item.rise = false; }
                    if (shownEvent != null && CutTargetEvent(shownEvent.Kind) && (!item.decoration || shownEvent.Kind == FieldEventKind.ThornSiege))   // Filiz yayılması sarsmaz
                        emergeShake = Mathf.Max(emergeShake, item.burst ? ThornBurstShake(t) : Mathf.Sin(height * Mathf.PI));
                }
                item.visual.localScale = new Vector3(item.scale.x * scale * across, item.scale.y * scale * height, item.scale.z * scale * across);
                item.content?.Step(delta, item.age, lift);
            }
        }
        if (delta > 0f && emergeShake > 0f)
            CameraFeel.Instance?.SetFrameShake(new Vector2(Mathf.Sin(Time.time * 53f), Mathf.Cos(Time.time * 43f)) * (.055f * emergeShake));
        UpdateDebugOutline();
    }

    // ---------------------------------------------------------------- test görünümü
    private void UpdateDebugOutline()
    {
        bool wanted = DebugOutlines && shownEvent != null && shownEvent.Phase != FieldEventPhase.Done && shownEvent.Phase != FieldEventPhase.Waiting;
        if (!wanted)
        {
            if (outlineRenderer != null && outlineRenderer.enabled) outlineRenderer.enabled = false;
            outlineDirty = true;
            return;
        }
        if (!outlineDirty && outlineRenderer != null && outlineRenderer.enabled) return;
        outlineDirty = false;
        if (outlineRenderer == null)
        {
            Shader shader = Resources.Load<Shader>("FrostZoneMarker");
            if (shader == null || !shader.isSupported) { DebugOutlines = false; return; }
            outlineMaterial = new Material(shader) { name = "Field Event Debug Outline (runtime)" };
            outlineMaterial.SetFloat(ActiveId, 1f);
            outlineMaterial.SetFloat(PulseId, 0f);
            outlineMaterial.SetColor(ColorId, new Color(1f, 1f, 1f, 0.9f));
            outlineMesh = new Mesh { name = "Field Event Debug Outline" };
            outlineMesh.MarkDynamic();
            var go = new GameObject("Debug Outline", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            outlineRenderer = go.GetComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        GridSystem grid = FieldCells.Grid;
        if (grid == null) return;
        outlineCells.Clear();
        var set = new HashSet<long>();
        for (int z = 0; z < shownEvent.ZoneCount; z++)
        {
            IReadOnlyList<GridPosition> cells = shownEvent.Zone(z);
            for (int i = 0; i < cells.Count; i++) if (set.Add(FieldCells.Key(cells[i]))) outlineCells.Add(cells[i]);
        }
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        float half = FieldCells.CellSize * 0.5f;
        foreach (GridPosition cell in outlineCells)
        {
            Vector3 center = Top(grid, cell);
            foreach (Vector2Int side in CellOutlineMesh.Sides)
                if (!set.Contains(CellOutlineMesh.Key(cell.x + side.x, cell.z + side.y)))
                    CellOutlineMesh.AddEdge(vertices, uvs, triangles, center, side, half, 0.03f, 0.14f);
        }
        outlineMesh.Clear();
        outlineMesh.SetVertices(vertices);
        outlineMesh.SetUVs(0, uvs);
        outlineMesh.SetTriangles(triangles, 0);
        outlineMesh.RecalculateBounds();
        outlineRenderer.enabled = vertices.Count > 0;
    }
}
