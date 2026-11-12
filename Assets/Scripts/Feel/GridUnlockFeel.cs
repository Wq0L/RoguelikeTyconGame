using System.Collections.Generic;
using UnityEngine;

// Grid çerçevesi ve açılma efekti (6 Kasım 2026, kullanıcı isteği: "3×3 açıksa kamera 5×5 göstersin, büyüdükçe yavaş yavaş
// uzaklaşsın, grid açılma efekti").
// - Görünen alan = açık alan + bir kilitli halka (3×3 açık → 5×5, 5×5 → 7×7 …, en çok bütün grid). Dışındaki kilitli hücreler
//   çizilmez: tarla uzayda küçük bir ada olarak durur (koyu kilitli hücre denizi yok; gökyüzü ve gezegen görünür). Kamera bu alanı
//   çerçeveler (CameraFeel.SetFraming).
// - Grid büyüyünce kamera yavaşça geri çekilir; önce yeni açılan halka(lar), sonra yeni kilitli halka sırayla, hafif yaylanarak
//   yerden yükselir.
// - Grid yükseltmesi yetenek ağacında alınır (tarla görünmez): geri çekilme ve efekt tarlanın ilk göründüğü ana ertelenir (round,
//   yerleştirme, satış, önizleme). O ana kadar yeni açılan hücrelerin zemini çizilmez.
// Yalnız görsel: gizleme forceRenderingOff ile (sınırlar, oynanış durumu ve düz harita değişmez); hareket yalnız zemin görselinin
// (Floor) yerel konumu / ölçeği, hücre kökü (GroundCell) oynamaz. Karede bellek ayırma yok (listeler yeniden kullanılır); bitince
// her şey ilk değerine döner. FeelBootstrap kurar (batch testlerinde yoktur).
[DefaultExecutionOrder(890)]   // CameraFeel'den (900) önce: çerçeve aynı karede uygulanır
public sealed class GridUnlockFeel : MonoBehaviour
{
    public const float RiseSeconds = .5f, StaggerSeconds = .7f, RingStagger = .35f, RiseDepth = .9f, StartScale = .35f;
    // Üst HUD için pay (H9, 6 Kasım 2026): tarla ekran yüksekliğinin bu oranı kadar aşağıda durur; round kartı en üst koordinat
    // rozetini örtmez. Her çerçevede aynı oran (kamera kendi yukarı ekseninde kayar).
    public const float HudTopRoom = .05f;
    // 12 Kasım 2026 (kullanıcı: "round kartı / süre bazı şeylerin üstüne çıkıyor"): yeni round kartı + süre plakası daha büyük ve
    // geniş pencerede arayüz büyüyor; pay artık arayüzün gerçek alt kenarından hesaplanır (en az HudTopRoom). Tarlanın üst köşesi
    // arayüzün HudGap kadar altında kalır; alt köşe ekrandan taşmaz (HudBottomGap). Pencere boyutu ya da arayüz değişince yeniden.
    public const float HudGap = .015f, HudBottomGap = .01f;
    private int layoutWidth, layoutHeight, layoutHud = -1;
    private bool layoutLabels;

    private struct Cell
    {
        public Transform floor;
        public Renderer renderer;
        public Vector3 position, scale;
        public float order, delay;
    }

    private readonly List<Cell> playing = new();
    private readonly List<Renderer> hidden = new();   // görünen alanın dışında (çizilmeyen) hücreler: geri alma için
    private GridUnlockManager unlocks;
    private GridManager grid;
    private int shownSize, framedSize, pendingSize;   // gösterilen açık alan, görünen alan, efekti bekleyen açık alan
    private float playAge = -1f;
    private bool initialized;

    public int ShownSize => shownSize;
    public int FramedSize => framedSize;
    public bool HasPending => pendingSize > shownSize;
    public bool Playing => playAge >= 0f;

    private void LateUpdate()
    {
        if (!initialized && !TryInitialize()) return;
        if (HasPending && FieldVisible()) Begin();
        if (playAge >= 0f) Step(Time.deltaTime);
        else if (LayoutChanged()) ApplyFraming(shownSize, false);   // pencere boyutu / arayüz kuruldu: üst payı yeniden hesapla
    }

    // Pencere boyutu, arayüzün alt kenarı (binde bir hassasiyetle) ya da koordinat rozetleri değişince: bellek ayırmaz.
    private bool LayoutChanged()
    {
        int hud = ComicHud.TryGetTopCenterBottom(out float bottom) ? Mathf.RoundToInt(bottom * 1000f) : -1;
        bool labels = GameSettings.GridCoordinates && GridCoordinateLabels.Active != null;
        if (Screen.width == layoutWidth && Screen.height == layoutHeight && hud == layoutHud && labels == layoutLabels) return false;
        layoutWidth = Screen.width; layoutHeight = Screen.height; layoutHud = hud; layoutLabels = labels;
        return true;
    }

    private bool TryInitialize()
    {
        unlocks = GridUnlockManager.Instance;
        grid = GridManager.Instance;
        if (unlocks == null || grid == null || grid.GetGridSystem() == null || unlocks.CurrentSize <= 0) return false;
        initialized = true;
        shownSize = pendingSize = unlocks.CurrentSize;
        framedSize = Framed(shownSize);
        unlocks.OnGridSizeChanged += OnGridGrown;
        // Görünen alanın dışı çizilmez.
        int half = framedSize / 2;
        ForCells(half + 1, int.MaxValue, (x, z, ring) => Hide(Renderer(x, z)));
        ApplyFraming(shownSize, false);
        return true;
    }

    private int Framed(int openSize)
    {
        CameraFeel feel = CameraFeel.Instance;
        int width = grid.GetWidth();
        return feel != null ? feel.FramedCells(openSize, width) : Mathf.Min(openSize + 2, width);
    }

    // Tarla ekranda mı: round, yerleştirme / satış ya da round sonu önizlemesi.
    private static bool FieldVisible()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return false;
        GameStates state = game.CurrentState;
        return state == GameStates.Round || state == GameStates.Placing || state == GameStates.Selling || RoundPreviewUI.IsOpen;
    }

    // Satın alma anı: yeni açılan hücreler (eskiden kilitli halka olarak görünüyordu) efekt oynayana kadar gizlenir.
    private void OnGridGrown()
    {
        int size = unlocks.CurrentSize;
        if (size <= pendingSize) return;
        ForCells(pendingSize / 2 + 1, size / 2, (x, z, ring) => Hide(Renderer(x, z)));
        pendingSize = size;
    }

    // Efekt: eski açık alanın dışından yeni görünen alanın kenarına kadar bütün halkalar (yeni açılanlar, sonra yeni kilitli halka).
    private void Begin()
    {
        Restore(playing);
        playing.Clear();
        int inner = shownSize / 2, newFramed = Framed(pendingSize);
        float maxOrder = 0f;
        GridSystem system = grid.GetGridSystem();
        int cx = grid.GetWidth() / 2, cz = grid.GetHeight() / 2;
        ForCells(inner + 1, newFramed / 2, (x, z, ring) =>
        {
            GroundCell ground = system.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
            Renderer renderer = ground != null ? ground.GroundRenderer : null;
            if (renderer == null) return;
            Hide(renderer);
            Transform floor = renderer.transform == ground.transform ? null : renderer.transform;   // kök oynamaz
            // Halka içinde saat yönünde sırayla (üstten başlayarak); dıştaki halka biraz sonra.
            float angle = Mathf.Repeat(Mathf.Atan2(x - cx, z - cz) / (Mathf.PI * 2f), 1f);
            float order = (ring - inner - 1) * RingStagger / StaggerSeconds + angle;
            maxOrder = Mathf.Max(maxOrder, order);
            playing.Add(new Cell
            {
                floor = floor, renderer = renderer, order = order,
                position = floor != null ? floor.localPosition : Vector3.zero, scale = floor != null ? floor.localScale : Vector3.one,
            });
        });
        for (int i = 0; i < playing.Count; i++)
        {
            Cell c = playing[i];
            c.delay = maxOrder > 0f ? c.order / maxOrder * StaggerSeconds : 0f;
            playing[i] = c;
        }
        shownSize = pendingSize;
        framedSize = newFramed;
        playAge = 0f;
        ApplyFraming(shownSize, true);
        Step(0f);
    }

    private void Step(float dt)
    {
        playAge += dt;
        bool done = true;
        for (int i = 0; i < playing.Count; i++)
        {
            Cell c = playing[i];
            float t = (playAge - c.delay) / RiseSeconds;
            if (t <= 0f) { done = false; continue; }   // sırası gelmedi: gizli kalır
            Show(c.renderer);
            if (t < 1f) done = false;
            if (c.floor == null) continue;
            float e = BackOut(Mathf.Clamp01(t));
            c.floor.localPosition = c.position + Vector3.down * (RiseDepth * (1f - e));
            c.floor.localScale = c.scale * Mathf.LerpUnclamped(StartScale, 1f, e);
        }
        if (!done) return;
        Restore(playing);
        playing.Clear();
        playAge = -1f;
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // Kamera: görünen alanı çerçeveler. Yakın çerçevede bakış noktası grid merkezine kayar.
    private void ApplyFraming(int openSize, bool animate)
    {
        CameraFeel feel = CameraFeel.Instance;
        if (feel == null) return;
        int width = grid.GetWidth();
        float scale = feel.FramedCells(openSize, width) / (float)width;
        Vector3 center = grid.GetGridSystem().GetWorldPosition(width / 2, grid.GetHeight() / 2);
        Vector3 look = feel.BaseLookPoint(center.y);
        Vector3 shift = center - look;
        shift.y = 0f;
        shift *= Mathf.InverseLerp(1f, .5f, scale);
        // Kamera yukarı kayınca tarla ekranda aşağı iner: görünen yüksekliğin "room" kadarı (2 × ortho boyu × oran).
        if (feel.BaseSize > 0f) shift += feel.BaseWorldRotation * Vector3.up * (2f * feel.BaseSize * scale * HudRoom(feel, scale, shift, center));
        feel.SetFraming(scale, shift, animate);
    }

    // Tarlanın ekrandaki üst / alt köşesi (görünen alanın dört dış köşesinden) ve arayüzün alt kenarından gereken pay.
    private float HudRoom(CameraFeel feel, float scale, Vector3 shift, Vector3 center)
    {
        if (!ComicHud.TryGetTopCenterBottom(out float hudBottom)) return HudTopRoom;
        GridSystem system = grid.GetGridSystem();
        float cell = (system.GetWorldPosition(1, 0) - system.GetWorldPosition(0, 0)).magnitude;
        float half = (Mathf.RoundToInt(scale * grid.GetWidth()) / 2 + .5f) * cell;   // görünen alanın yarı kenarı (dünya)
        float size = feel.BaseSize * scale;                                          // ortografik yarı yükseklik
        Vector3 up = feel.BaseWorldRotation * Vector3.up;
        Vector3 look = feel.BaseLookPoint(center.y) + shift;                         // ekranın ortası
        float top = float.MinValue, bottom = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = center + new Vector3((i & 1) == 0 ? -half : half, 0f, (i & 2) == 0 ? -half : half);
            float v = .5f + Vector3.Dot(corner - look, up) / (2f * size);             // ekranda 0 alt, 1 üst
            top = Mathf.Max(top, v); bottom = Mathf.Min(bottom, v);
        }
        // Koordinat rozetleri (harfler üst kenarın, sayılar sol kenarın dışında): üst köşe yanındaki uç rozetler de arayüzün altında kalsın.
        GridCoordinateLabels labels = GridCoordinateLabels.Active;
        if (labels != null && GameSettings.GridCoordinates)
        {
            // Rozetler kameraya dönük dik levha: merkezleri yerde kenarın dışında, ekranda yarı boyları kadar yukarı uzanırlar.
            float reach = labels.OuterReach(scale), plate = labels.PlateHalf(scale);
            float along = half - .5f * cell;                                            // son sütun / satır merkezi
            Vector3 lift = Vector3.up * labels.LabelHeight;
            Vector3 columnEnd = center + new Vector3(along, 0f, half + reach) + lift;   // son sütun harfi (üst kenar dışında)
            Vector3 rowEnd = center + new Vector3(-half - reach, 0f, along) + lift;     // en üst satır sayısı (sol kenar dışında)
            top = Mathf.Max(top, .5f + (Vector3.Dot(columnEnd - look, up) + plate) / (2f * size));
            top = Mathf.Max(top, .5f + (Vector3.Dot(rowEnd - look, up) + plate) / (2f * size));
        }
        float needed = hudBottom + HudGap - (1f - top);
        return Mathf.Max(HudTopRoom, Mathf.Min(needed, bottom - HudBottomGap));
    }

    // Merkezden ring halka uzaklığındaki hücreler (Chebyshev), minRing ≤ ring ≤ maxRing, grid içinde.
    private void ForCells(int minRing, int maxRing, System.Action<int, int, int> visit)
    {
        int width = grid.GetWidth(), height = grid.GetHeight(), cx = width / 2, cz = height / 2;
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            int ring = Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(z - cz));
            if (ring >= minRing && ring <= maxRing) visit(x, z, ring);
        }
    }

    private Renderer Renderer(int x, int z)
    {
        GroundCell ground = grid.GetGridSystem().GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
        return ground != null ? ground.GroundRenderer : null;
    }

    private void Hide(Renderer renderer)
    {
        if (renderer == null || renderer.forceRenderingOff) return;
        renderer.forceRenderingOff = true;
        hidden.Add(renderer);
    }

    private void Show(Renderer renderer)
    {
        if (renderer == null || !renderer.forceRenderingOff) return;
        renderer.forceRenderingOff = false;
        hidden.Remove(renderer);
    }

    private void Restore(List<Cell> cells)
    {
        foreach (Cell c in cells)
        {
            Show(c.renderer);
            if (c.floor == null) continue;
            c.floor.localPosition = c.position;
            c.floor.localScale = c.scale;
        }
    }

    private void OnDestroy()
    {
        if (unlocks != null) unlocks.OnGridSizeChanged -= OnGridGrown;
        Restore(playing);
        playing.Clear();
        foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false;
        hidden.Clear();
    }
}
