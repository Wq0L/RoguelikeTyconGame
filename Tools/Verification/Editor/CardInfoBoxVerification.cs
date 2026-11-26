using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Kart seçim ekranının bilgi kutusu (24 Kasım 2026, test geri bildirimi: "kartın adı üstte yazmalı, mouse üstüne gelince kartın
// özelliği yazmalı"). Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080 çekim.
// Kontroller:
//  - kurucu iki kez çalışınca kutu çoğalmaz (tek "Card Info", tek başlık, tek gövde), sahneye kaydedilir;
//  - kutu kartlara, ATLA'ya ve kalan seçim plakasına binmez, ekranın içinde; fareyi / tıklamayı engellemez;
//  - fare kartın üstüne gelince başlık = kartın adı, gövde = kartın açıklaması; kart değiştikçe değişir; kartlar arası boşlukta son
//    kart kalır (titreme yok); yeni dağıtımda fare kartta kalmışsa yeni teklifi yazar;
//  - her tile türünün açıklaması tabloda (anahtar adı ekrana çıkmaz); yükseltme ve Temel güç kartlarında kutu çıkmaz;
//  - açıklama yazısı krem alanın dikey ortasında (çekimden ölçülür);
//  - tr / en / qps (≈ %35 uzun) yazılar kutusuna sığar; dil değişince kutu hemen yeni dilde;
//  - eski "tarla dolu" şeridi kurulmaz; kutu yenilemede çoğalmaz.
// Görüntüler: Logs/CardInfo/<ad>.png, sonuç Logs/CardInfoBoxVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method CardInfoBoxVerification.RunBatch -Full
[InitializeOnLoad]
public static class CardInfoBoxVerification
{
    const string Key = "CardInfoBoxVerification";
    static int W = 1920, H = 1080;   // çekim boyutu; ekran boyutu turunda değişir
    static readonly string[] Languages = { "tr", "en", GameText.PseudoLanguage };
    static readonly List<string> lines = new();
    static int phase, languageIndex, infoNodes;
    static double next, started;
    static RenderTexture target;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static CardInfoBoxVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        string cardFonts = CardSkipButtonInstaller.RestyleCardPrefab();
        if (cardFonts != null) throw new Exception("card prefab: " + cardFonts);
        // İki kez: ikinci çalıştırma hiçbir şeyi çoğaltmamalı.
        string install = CardSkipButtonInstaller.InstallInOpenScene() ?? CardSkipButtonInstaller.InstallInOpenScene();
        if (install != null) throw new Exception("installer: " + install);
        var cards = UnityEngine.Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        var boxes = cards.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "Card Info").ToList();
        Transform box = boxes.FirstOrDefault();
        bool single = boxes.Count == 1 && box.GetComponentsInChildren<RectTransform>(true).Count(t => t.name == "Title") == 1 &&
                      box.GetComponentsInChildren<RectTransform>(true).Count(t => t.name == "Body") == 1 &&
                      box.GetComponents<Image>().Length == 1;
        SessionState.SetString(Key + ".install", (single ? "PASS" : "FAIL") + " kurucu iki kez çalışınca kutu çoğalmıyor — " +
            $"{boxes.Count} kutu, {box?.GetComponentsInChildren<RectTransform>(true).Length} düğüm");
        var so = new SerializedObject(cards);
        bool bound = so.FindProperty("infoTitle").objectReferenceValue != null && so.FindProperty("infoBody").objectReferenceValue != null;
        SessionState.SetString(Key + ".bound", (bound ? "PASS" : "FAIL") + " kutu sahnede kurulu ve CardSelectionUI'a bağlı (editörde görünür)");
        EditorSceneManager.SaveOpenScenes();
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Kart bilgi kutusu", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static CardSelectionUI Cards => UnityEngine.Object.FindAnyObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<CardUI> Slots => (List<CardUI>)typeof(CardSelectionUI).GetField("cardSlots", Any).GetValue(Cards);
    static Button Skip => (Button)typeof(CardSelectionUI).GetField("skipButton", Any).GetValue(Cards);
    static TMP_Text Title => (TMP_Text)typeof(CardSelectionUI).GetField("infoTitle", Any).GetValue(Cards);
    static TMP_Text Body => (TMP_Text)typeof(CardSelectionUI).GetField("infoBody", Any).GetValue(Cards);
    static RectTransform Box => (RectTransform)Title.transform.parent.parent;
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Any).Invoke(target, args);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0)
        {
            started = now; next = now + 2;
            lines.Add(SessionState.GetString(Key + ".install", "FAIL kurulum sonucu yok"));
            lines.Add(SessionState.GetString(Key + ".bound", "FAIL bağlama sonucu yok"));
            return;
        }
        if (now - started > 240) { lines.Add("FAIL timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    typeof(RoundManager).GetField("skipUsesRemaining", Any).SetValue(RM, 3);
                    phase = 1; next = now + .4; return;
                }
                case 1:
                {
                    var progress = ProgressionManager.Instance;
                    progress.AddXP(progress.XPToNextLevel); progress.AddXP(progress.XPToNextLevel); progress.AddXP(progress.XPToNextLevel);
                    Call(RM, "EndRound");
                    phase = 2; next = now + 1.8; return;
                }
                case 2:
                {
                    Check(GameManager.Instance.CurrentState == GameStates.CardSelection && Box.gameObject.activeInHierarchy, "kart seçimi açık, bilgi kutusu görünüyor",
                        $"durum {GameManager.Instance.CurrentState}");
                    Check(Title.text == GameText.Get("ui.card.info.idle_title") && Body.text == GameText.Get("ui.card.info.idle"),
                        "fare kartta değilken kutu ekranın ne yaptığını söylüyor", $"'{Title.text}' / '{Body.text}'");
                    Check(Box.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "kutu fareyi ve tıklamayı engellemiyor");
                    Check(Cards.GetComponentsInChildren<RectTransform>(true).All(t => t.name != "Grid Full Banner"), "eski tarla dolu şeridi kurulmadı");
                    LayoutChecks();
                    infoNodes = OwnNodes();
                    Capture("tr_bos.png");
                    CenterCheck("boş ekran");
                    phase = 3; next = now + .2; return;
                }
                case 3:
                {
                    // Fare kartların üstünde gezer: her kartta başlık = kartın adı, gövde = kartın açıklaması.
                    var bad = new List<string>();
                    foreach (CardUI slot in Slots.Where(s => s.isActiveAndEnabled))
                    {
                        Enter(slot);
                        CardSelectionUI.Describe(slot.Offer, slot.ShowsSpawnFloor, out string title, out string body);
                        if (Title.text != title || Body.text != body || string.IsNullOrWhiteSpace(body) || body.StartsWith("ui.")) bad.Add($"{slot.name}: '{Title.text}' / '{Body.text}'");
                    }
                    Check(bad.Count == 0, "fare kartın üstüne gelince kutu o kartı anlatıyor (her kart)", bad.Count == 0
                        ? string.Join(" | ", Slots.Select(s => s.Offer?.Tile?.DisplayName)) : string.Join(" | ", bad));
                    CardUI middle = Slots[1];
                    Enter(middle);
                    string shown = Title.text;
                    Exit(middle);
                    Check(Title.text == shown, "kartlar arasındaki boşlukta kutu son kartı yazmaya devam ediyor (titreme yok)", shown);
                    Enter(middle);
                    phase = 4; next = now + .3; return;
                }
                case 4:
                    Capture("tr_kart.png");
                    CenterCheck("kart üstünde");
                    TypeChecks();
                    phase = 5; next = now + .1; return;
                case 5:
                    GameText.Language = Languages[languageIndex];
                    phase = 6; next = now + .8; return;
                case 6:
                {
                    string language = Languages[languageIndex];
                    CardSelectionUI.Describe(Slots[1].Offer, Slots[1].ShowsSpawnFloor, out string title, out string body);
                    Check(Title.text == title && Body.text == body, $"[{language}] dil değişince kutu hemen yeni dilde", $"'{Title.text}'");
                    FitChecks(language);
                    Capture(language + ".png");
                    languageIndex++;
                    if (languageIndex < Languages.Length) { phase = 5; next = now + .2; return; }
                    GameText.Language = "tr";
                    resolutionIndex = 0;
                    phase = 20; next = now + .6; return;
                }
                case 20:
                {
                    // Ekran boyutu turu: kamera ve arayüz o boyutta bir dokuya çizilir; CanvasScaler tuvali ona göre ölçekler.
                    Vector2Int size = Resolutions[resolutionIndex];
                    SetResolution(size.x, size.y);
                    // Gerçek fare gibi: yalnız ortadaki kartın üstünde (o kart %3,5 büyür); büyüme bitene kadar beklenir.
                    foreach (CardUI slot in Slots) Exit(slot);
                    Enter(Slots[1]);
                    phase = 21; next = now + .8; return;
                }
                case 21:
                {
                    string label = $"{W}×{H}";
                    LayoutChecks(label);
                    CenterCheck(label);
                    Capture($"boyut_{W}x{H}.png");
                    resolutionIndex++;
                    if (resolutionIndex < Resolutions.Length) { phase = 20; next = now + .1; return; }
                    SetResolution(1920, 1080);
                    phase = 7; next = now + .8; return;
                }
                case 7:
                {
                    // Kart seçilir: yeni dağıtımda fare aynı yerde (ortadaki kartta) kalıyor; kutu yeni teklifi yazmalı.
                    Enter(Slots[1]);
                    Slots[0].GetComponentInChildren<Button>(true).onClick.Invoke();
                    phase = 8; next = now + 1.2; return;
                }
                case 8:
                {
                    CardSelectionUI.Describe(Slots[1].Offer, Slots[1].ShowsSpawnFloor, out string title, out string body);
                    Check(GameManager.Instance.CurrentState == GameStates.CardSelection && Title.text == title && Body.text == body,
                        "yeni dağıtımda fare kartta kalmışsa kutu yeni teklifi yazıyor", $"'{Title.text}' / '{Body.text}'");
                    HiddenForUpgradesCheck();
                    // TMP, yedek fonttan harf çizince yazının altına kendi alt ağını (TMP_SubMeshUI) bir kez ekler ve yeniden kullanır;
                    // o sayılmaz. Bizim kurduğumuz düğümler çoğalmamalı.
                    int before = OwnNodes(), subBefore = SubMeshes();
                    for (int i = 0; i < 5; i++) Cards.RefreshCards();
                    int nodes = OwnNodes(), subAfter = SubMeshes();
                    Check(nodes == infoNodes && before == infoNodes && subAfter == subBefore &&
                          Cards.GetComponentsInChildren<RectTransform>(true).Count(t => t.name == "Card Info") == 1,
                        "5 yenilemede kutu çoğalmıyor", $"kurulan düğüm {infoNodes} → {before} → {nodes}; TMP alt ağı {subBefore} → {subAfter}");
                    Capture("tr_ikinci_dagitim.png");
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    static int OwnNodes() => Box.GetComponentsInChildren<Transform>(true).Count(t => t.GetComponent<TMP_SubMeshUI>() == null);
    static int SubMeshes() => Box.GetComponentsInChildren<TMP_SubMeshUI>(true).Length;

    static void Enter(CardUI card) => ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
    static void Exit(CardUI card) => ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);

    // Kutu ekranın içinde; kartlara, ATLA'ya ve kalan seçim plakasına binmiyor (ekran koordinatında, kartların eğikliği dahil).
    // Ekran boyutu turunda HUD'ın kaynak göstergeleri (sağ üst) ve kenar payı da denetlenir; kutunun üstte en az 4 px payı olmalı.
    static void LayoutChecks(string label = null)
    {
        Canvas.ForceUpdateCanvases();
        string tag = label == null ? "" : $"[{label}] ";
        Rect box = ScreenRect(Box);
        Rect screen = new Rect(0, 0, W, H);
        float topRoom = H - box.yMax;
        Check(screen.Contains(box.min) && screen.Contains(box.max) && (label == null || topRoom >= 4f), tag + "kutu ekranın içinde",
            $"{Fmt(box)}, üstte {topRoom:0} px pay");
        var hits = new List<string>();
        foreach (CardUI slot in Slots.Where(s => s.isActiveAndEnabled))
            if (ScreenRect((RectTransform)slot.transform).Overlaps(box)) hits.Add($"{slot.name} {Fmt(ScreenRect((RectTransform)slot.transform))}");
        if (Skip != null && Skip.gameObject.activeInHierarchy && ScreenRect((RectTransform)Skip.transform).Overlaps(box)) hits.Add("ATLA");
        var plate = Cards.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Remaining Choices");
        if (plate != null && plate.gameObject.activeInHierarchy && ScreenRect(plate).Overlaps(box)) hits.Add("kalan seçim");
        foreach (GoldUI resource in UnityEngine.Object.FindObjectsByType<GoldUI>(FindObjectsSortMode.None).Where(g => g.isActiveAndEnabled))
            if (ScreenRect((RectTransform)resource.transform).Overlaps(box)) hits.Add($"kaynak göstergesi {resource.name} {Fmt(ScreenRect((RectTransform)resource.transform))}");
        // Ekran koordinatında y yukarı: kutunun alt kenarı ile en yüksek kart köşesi arasındaki boşluk.
        float gap = box.yMin - Slots.Where(s => s.isActiveAndEnabled).Max(s => ScreenRect((RectTransform)s.transform).yMax);
        Check(hits.Count == 0, tag + "kutu kartlara, ATLA'ya, kalan seçime ve kaynak göstergelerine binmiyor",
            hits.Count == 0 ? $"kutu {Fmt(box)}, kartlarla arası {gap:0} px" : string.Join(" | ", hits));
    }

    // Ekran boyutları (oran / boy): 16:9 (720p, 1080p, 1440p), 16:10, 4:3, 5:4, 21:9 (2560 ve 3440), 1366×768 (dizüstü).
    static readonly Vector2Int[] Resolutions =
    {
        new(1280, 720), new(1366, 768), new(2560, 1440), new(1920, 1200), new(1680, 1050),
        new(1440, 1080), new(1280, 1024), new(2560, 1080), new(3440, 1440),
    };
    static int resolutionIndex;

    static void SetResolution(int width, int height)
    {
        Camera cam = Camera.main;
        cam.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        W = width; H = height;
        target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
        cam.targetTexture = target;
    }

    // Her tile türünün açıklaması tabloda; yükseltme ve temel güç kartları da bir cümle söylüyor.
    static void TypeChecks()
    {
        var all = (List<TileModifierSO>)typeof(CardSelectionUI).GetField("allModifiers", Any).GetValue(Cards);
        var bad = new List<string>();
        foreach (TileModifierType type in Enum.GetValues(typeof(TileModifierType)))
        {
            TileModifierSO tile = all.FirstOrDefault(m => m != null && m.modifierType == type);
            if (tile == null) { bad.Add(type + ": havuzda yok"); continue; }
            CardSelectionUI.Describe(new TileCardOffer(tile), false, out string title, out string body);
            if (string.IsNullOrWhiteSpace(body) || body.StartsWith("ui.") || title.Contains("-")) bad.Add($"{type}: '{title}' / '{body}'");
        }
        CardSelectionUI.Describe(new TileCardOffer(all.First(m => m != null)), true, out _, out string floor);
        if (floor != GameText.Get("ui.card.desc.spawn_floor")) bad.Add("üretim tabanı: " + floor);
        Check(bad.Count == 0, "10 tile türünün adı ve açıklaması tabloda (iç ad / anahtar adı yok)", string.Join(" | ", bad));
    }

    // Yükseltme ve Temel güç kartlarında kutu çıkmaz (kullanıcı, 24 Kasım: "onlarda üstteki şey çıkmasın"); yeni tile kartlarında
    // geri gelir. Gerçek akış (tarla dolu → yükseltme, hepsi en üstte → Temel güç) XPV1Verification ve MechanicsVerification'da.
    static void HiddenForUpgradesCheck()
    {
        List<GroundCell> cells = ProgressionManager.Instance.GetUpgradeCandidates(1);
        bool upgradeSilent = cells.Count > 0 && !CardSelectionUI.Describe(TileCardOffer.Upgrade(cells[0], TileRarity.Common), false, out _, out _);
        bool baseSilent = !CardSelectionUI.Describe(TileCardOffer.BaseStat("test", TileRarity.Common, new List<StatModifier>()), false, out _, out _);
        FieldInfo mode = typeof(CardSelectionUI).GetField("infoMode", Any);
        object newTile = mode.GetValue(Cards);
        var states = new List<string>();
        bool hidden = true;
        foreach (string state in new[] { "GridFull", "AllMax" })
        {
            mode.SetValue(Cards, Enum.Parse(mode.FieldType, state));
            Call(Cards, "ShowInfo", (object)null);
            states.Add($"{state}: {(Cards.InfoBoxShown ? "açık" : "kapalı")}");
            hidden &= !Cards.InfoBoxShown;
        }
        mode.SetValue(Cards, newTile);
        Call(Cards, "ShowInfo", (object)null);
        bool back = Cards.InfoBoxShown && !string.IsNullOrWhiteSpace(Title.text);
        Check(upgradeSilent && baseSilent && hidden && back, "yükseltme ve Temel güç kartlarında kutu çıkmıyor, yeni tile kartlarında geri geliyor",
            string.Join(", ", states) + $", yeni tile: {(back ? "açık" : "kapalı")}" + (cells.Count == 0 ? " (tarlada yükseltilecek tile yok)" : ""));
    }

    // Gövde yazısı krem alanın dikey ortasında (kullanıcı, 24 Kasım: "yazı biraz alta kalmış"). Krem alan çekimden ölçülür: yazı bir
    // anlığına gizlenir, kutunun tam ortasındaki sütunda şeridin altındaki en uzun krem şerit alınır (kenarlardaki süs ve uzun yazı
    // ölçümü bozmaz). Yazının ortası, TMP'nin çizdiği harflerin sınırından.
    static void CenterCheck(string when)
    {
        Canvas.ForceUpdateCanvases();
        Body.ForceMeshUpdate();
        Bounds b = Body.textBounds;
        Vector3 world = Body.rectTransform.TransformPoint(b.center);
        float textMid = RectTransformUtility.WorldToScreenPoint(Camera.main, world).y;
        Rect box = ScreenRect(Box);
        Body.enabled = false;
        Canvas.ForceUpdateCanvases();
        Camera.main.Render();
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        RenderTexture.active = old;
        Body.enabled = true;
        int bestStart = 0, bestLength = 0, start = -1, x = Mathf.RoundToInt(box.center.x);
        for (int y = Mathf.FloorToInt(box.yMin); y <= Mathf.CeilToInt(box.yMax) + 1; y++)
        {
            Color c = y >= 0 && y < H ? image.GetPixel(x, y) : Color.black;
            bool cream = c.r > .9f && c.g > .84f && c.b > .7f;
            if (cream && start < 0) start = y;
            if (!cream && start >= 0) { if (y - start > bestLength) { bestLength = y - start; bestStart = start; } start = -1; }
        }
        UnityEngine.Object.DestroyImmediate(image);
        float creamMid = bestStart + (bestLength - 1) * .5f;
        Check(bestLength > 30 && Mathf.Abs(textMid - creamMid) <= 4f, $"[{when}] açıklama krem alanın dikey ortasında",
            $"krem alan y {bestStart}–{bestStart + bestLength - 1} (orta {creamMid:0.#}), yazının ortası {textMid:0.#}, fark {textMid - creamMid:+0.#;-0.#;0}");
    }

    // Başlık ve gövde kutusuna sığıyor; en uzun açıklama (iki satırlı yükseltme dahil) bu dilde de sığıyor.
    static void FitChecks(string language)
    {
        var bad = new List<string>();
        string title = Title.text, body = Body.text;
        var all = (List<TileModifierSO>)typeof(CardSelectionUI).GetField("allModifiers", Any).GetValue(Cards);
        var bodies = all.Where(m => m != null).Select(m => { CardSelectionUI.Describe(new TileCardOffer(m), false, out _, out string b); return b; }).ToList();
        bodies.Add(GameText.Get("ui.card.info.idle"));
        bodies.Add(GameText.Get("ui.card.desc.spawn_floor"));
        var titles = new List<string> { GameText.Get("ui.card.info.idle_title") };
        titles.AddRange(all.Where(m => m != null).Select(m => m.DisplayName));
        float smallest = float.MaxValue;
        foreach (var (text, variants) in new[] { (Title, titles), (Body, bodies) })
            foreach (string variant in variants.Distinct())
            {
                text.text = variant;
                text.ForceMeshUpdate();
                Rect r = text.rectTransform.rect;
                Bounds b = text.textBounds;
                bool fits = b.min.x >= r.xMin - 1.5f && b.max.x <= r.xMax + 1.5f && b.min.y >= r.yMin - 1.5f && b.max.y <= r.yMax + 1.5f;
                if (!fits) bad.Add($"{text.name} '{variant.Replace("\n", " / ")}' {b.size.x:0}×{b.size.y:0} / {r.width:0}×{r.height:0}");
                if (text.fontSize < smallest) smallest = text.fontSize;
                if (text.fontSize < text.fontSizeMin - .01f) bad.Add($"{text.name} {text.fontSize:0.#} pt");
            }
        Title.text = title; Body.text = body;
        Check(bad.Count == 0, $"[{language}] kutudaki bütün başlık ve açıklamalar sığıyor", bad.Count == 0 ? $"en küçük {smallest:0.#} pt" : string.Join(" | ", bad.Take(5)));
    }

    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera cam = Camera.main;
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 c in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static string Fmt(Rect r) => $"x {r.xMin:0}–{r.xMax:0}, y {r.yMin:0}–{r.yMax:0}";

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/CardInfo");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/CardInfo/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/CardInfoBoxVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
