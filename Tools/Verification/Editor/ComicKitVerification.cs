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
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Comic kağıt kiti oyunda (6 Kasım 2026): HUD (ComicHud), hazırlık ekranının dört butonu, round özeti kartı, harita hücreleri.
// Gerçek GameScene, Mechanics Test Lab oturumu (kayıt yalnız bellekte). Sınananlar:
// - HUD kurulu; büyük sayılar (12.345 altın) plakadan taşmıyor; XP dolgusu Slider'ın değerini izliyor.
// - Butonlar: kitin sprite'ı, SpriteSwap hâlleri, eski malzeme bileşeni yok, yazı butona sığıyor; hover gerçekten hover sprite'ı.
// - Round özeti kartı: krem panel + lacivert başlık, eski plakalar yok, başlık beyaz.
// - Harita: açık hücre beyaz kare (tile rengi), kapalı hücre kömür karesi.
// - Dükkâna gidip dönünce hiçbir şey çoğalmıyor (kart, başlık, buton sayıları aynı).
// - Bungee: HUD, butonlar, kart başlığı, koordinatlar, dükkân yazıları kitin fontunda ve sığıyor; Türkçede "bitki" → "BİTKİ"
//   (i → İ), İngilizcede değişmez.
// - Koordinat kutuları: haritada ve tarlada slot_blue, tarlada vurgu için altın kare.
// - Dükkân: pencere, başlıklar, kartlar, istatistik kutuları, Satın al / Temizle, Sat ve GERİ kitte; GERİ sayaçlarla çakışmıyor.
// - Gerçek tile verileri (Elektrik, Su, Bereket, Patlayıcı) uygulanınca hücreler tile rengine boyanıyor; yeni tile'lar kartı kitte.
// - Dil turu (kullanıcı: "yazılar değişse de her şey düzgün gözüksün"): hazırlık ekranı, dükkân ve HUD İngilizcede ve sahte dilde
//   (qps: ~%35 uzun, aksanlı) yeniden kurulur; kitteki her yazı kutusuna sığar ve çizilir (yoksa FAIL). Kite geçmemiş gövde
//   yazısı taşarsa WARN satırı yazılır. Her dil için görüntü: c6_prep_<dil>, c7_shop_<dil>, c8_hud_<dil>.
// - Yetenek ağacı: her düğüm durumuna göre kitin karesinde (altın alınabilir, kömür alınamaz, beyaz kısmen, yeşil tamam, mavi
//   başlangıç, lacivert kapalı); bir düğüm alınınca karesi ve noktası değişir; bağlantılar krem çizgi; tooltip kitte (krem panel,
//   amber şerit, Bungee başlık) ve üç dilde sığar. Görüntüler: c9_skill_<dil>.
// - Ağaç yerleşimi ve hissi (11 Kasım): zemin comic pano; bütün düğümler yeni boyda, hiçbiri üst üste binmiyor, sıra ve hizalar
//   yetenek verisindekiyle aynı (veri değişmedi), ağaç eskisinden küçük alana sığıyor; alınabilir düğüm nefes alıyor, alınamayan
//   almıyor; üstüne gelince büyüyor ve bağlantısı parlıyor, çıkınca dönüyor; alınamayana tıklayınca sallanıp yerine dönüyor.
//   Görüntü: c9_skill_all (bütün düğümler görünür kılınmış, yalnız çekim için).
// - Kaynağı yetmeyen düğümün bilgi kutusunda bedel kırmızı ve eksik miktar yanında (c9_skill_short). Düğümün üstünde fiyat
//   etiketi, ekran başlığı / ipucu ve "pat" yıldızı yok (11 Kasım, kullanıcı reddetti: "ilk hali korunup juice'lanmalı").
// Görüntüler: Logs/ComicKit/*.png. Çalıştırma: Tools/run-isolated-verification.ps1 -Method ComicKitVerification.RunBatch -Full
[InitializeOnLoad]
public static class ComicKitVerification
{
    const string Key = "ComicKitVerification";
    static double next, started;
    static int phase, summaryChildren, prepButtons;
    static readonly List<string> notes = new();
    static ComicKitVerification() { EditorApplication.update += Tick; }

    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.Default();
        s.gridSize = 5;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static readonly (string name, string colour)[] Buttons =
        { ("Next Round Button", "blue"), ("Placment Shop Button", "green"), ("Skill Shop Button", "gold"), ("Preview Button", "purple") };

    static Button FindButton(string name) =>
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

    static Transform SummaryCard()
    {
        var summary = UnityEngine.Object.FindAnyObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        return summary != null ? summary.transform.Find("Card") : null;
    }

    static bool Fits(TMP_Text text) { text.ForceMeshUpdate(); return text.textBounds.size.x <= text.rectTransform.rect.width + 2f; }
    static TMP_FontAsset Display => FeelOverlay.Theme.displayFont;
    static bool Bungee(TMP_Text text) => text != null && text.font == Display && text.textPreprocessor == ComicKit.TurkishCaps.Instance;
    static void FontAndFit(TMP_Text text, string what) => Check(Bungee(text) && Fits(text), $"{what}: '{text?.text}' in Bungee and fits");
    // Buton yazısı gerçekten görünüyor mu: opak, kırpılmamış, mesh'i var ve butonun içinde (Fits yalnız genişliğe bakar).
    static void Visible(Button button, string what)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.ForceMeshUpdate();
        var renderer = label.canvasRenderer;
        Rect a = ScreenRect((RectTransform)button.transform), b = ScreenRect(label.rectTransform);
        Vector3 center = label.transform.TransformPoint(label.textBounds.center);
        bool inside = a.Contains(new Vector2(center.x, center.y));
        Check(label.isActiveAndEnabled && label.color.a > .9f && renderer.GetAlpha() > .9f && !renderer.cull && label.textInfo.characterCount > 0 && inside,
            $"{what}: label '{label.text}' visible inside the button (alpha {label.color.a:0.0}/{renderer.GetAlpha():0.0}, cull {renderer.cull}, chars {label.textInfo.characterCount}, " +
            $"button {a}, label {b}, text centre {center}, material {label.fontSharedMaterial?.name})");
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    static readonly string[] Languages = { "en", GameText.PseudoLanguage };
    static int languageIndex, warnings;
    static string startLanguage, hudLanguage;
    static GameObject Field(string name) => (GameObject)new SerializedObject(UnityEngine.Object.FindAnyObjectByType<UIManager>()).FindProperty(name).objectReferenceValue;
    static bool FitsBox(TMP_Text t)
    {
        t.ForceMeshUpdate();
        Vector3 b = t.textBounds.size; Rect r = t.rectTransform.rect;
        return b.x <= r.width + 2f && b.y <= r.height + 4f;
    }
    static string Short(string text) => text.Length > 40 ? text.Substring(0, 40) + "…" : text;
    // Bir ekrandaki bütün açık yazılar: kitin yazısı (Bungee) sığmalı ve çizilmeli; diğerleri yalnız raporlanır.
    static void Layout(Transform root, string screen)
    {
        int checkedTexts = 0;
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text) || t.rectTransform.rect.width < 1f) continue;
            bool fits = FitsBox(t);
            if (Bungee(t))
            {
                checkedTexts++;
                if (!fits || t.textInfo.characterCount == 0)
                    throw new Exception($"[{GameText.Language}] {screen} {t.name}: '{Short(t.text)}' does not fit its box (text {t.textBounds.size.x:0}x{t.textBounds.size.y:0}, box {t.rectTransform.rect.width:0}x{t.rectTransform.rect.height:0}, chars {t.textInfo.characterCount})");
            }
            else if (!fits) { warnings++; notes.Add($"WARN [{GameText.Language}] {screen} {t.name}: '{Short(t.text)}' overflows its box (body text, not on the kit yet)"); }
        }
        Check(checkedTexts > 0, $"[{GameText.Language}] {screen}: all {checkedTexts} kit texts fit their boxes");
    }
    static int skillLanguage;
    static readonly Dictionary<ResourceType, int> spent = new();
    static SkillNodeUI hovered;
    static string NodeState(SkillNodeUI ui)
    {
        SkillTreeManager tree = SkillTreeManager.Instance; SkillNodeSO n = ui.Node;
        bool granted = SkillTreeManager.IsGrantedByProfile(n);
        return SkillNodeUI.StateSprite(granted, !granted && tree.IsDisabledByProfile(n), tree.IsMaxLevel(n), tree.CanUpgrade(n), tree.GetCurrentLevel(n));
    }
    static List<SkillNodeUI> VisibleNodes() => UnityEngine.Object.FindObjectsByType<SkillNodeUI>(FindObjectsSortMode.None).Where(n => n.isActiveAndEnabled && n.Node != null).ToList();
    static GameObject ActiveTooltip() => (GameObject)typeof(TooltipManager).GetField("activeTooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(TooltipManager.Instance);
    static void Hover(SkillNodeUI node, bool enter)
    {
        var trigger = node.GetComponent<TooltipTrigger>();
        if (enter) trigger.OnPointerEnter(new PointerEventData(EventSystem.current)); else trigger.OnPointerExit(new PointerEventData(EventSystem.current));
    }
    static void CheckTooltip(string code)
    {
        GameObject tip = ActiveTooltip();
        Check(tip != null && tip.activeInHierarchy, $"[{code}] skill tooltip opens");
        var view = tip.GetComponent<ComicPopupView>();
        Check(view != null && tip.transform.Find("Comic Panel")?.GetComponent<Image>().sprite == ComicKit.Get("panel_cream") &&
              tip.transform.Find("Amber header")?.GetComponent<Image>().sprite == ComicKit.Get("label_cream"), $"[{code}] tooltip on the kit (cream panel, amber label)");
        FontAndFit(tip.transform.Find("Heading").GetComponent<TMP_Text>(), $"[{code}] tooltip heading");
        Layout(tip.transform, "skill tooltip");
    }
    static List<SkillNodeUI> AllNodes(SkillTreeUI tree) =>
        ((List<SkillNodeUI>)typeof(SkillTreeUI).GetField("nodeUIs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tree)).Where(n => n != null && n.Node != null).ToList();
    static PointerEventData LeftClick() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
    static SkillNodeUI probeNode, deniedNode;
    static readonly string[] Tiles = { "Electric/Electric-Rare", "Water/Water-Common", "Fertile/Fertile-Epic", "Explosive/Explosive-Legendary" };

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (next == 0) { next = now + 2; started = now; return; }
        if (now < next) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        try
        {
            if (now - started > 240) throw new Exception("timeout phase " + phase);
            if (!TestSession.Ready) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            switch (phase)
            {
                case 0:
                {
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    GameSettings.GridCoordinates = true;
                    ResourceManager.Instance.AddResource(ResourceType.Gold, 12345);
                    ResourceManager.Instance.AddResource(ResourceType.Iron, 340);
                    ResourceManager.Instance.AddResource(ResourceType.Stone, 7);
                    // Slider 0..1 (XpUI: oran); %60'a kadar XP, level atlatmadan.
                    ProgressionManager progression = ProgressionManager.Instance;
                    double gap = progression.XPToNextLevel * .6 - progression.CurrentXP;
                    if (gap > 0) progression.AddXP(gap);
                    phase = 1; next = now + 2; return;
                }
                case 1:
                {
                    Check(ComicKit.Ready && ComicHud.Installed, "kit sprites present and the comic HUD installed by UIManager");
                    var round = UnityEngine.Object.FindAnyObjectByType<RoundUI>(FindObjectsInactive.Include).transform;
                    Image card = round.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Round Card");
                    Check(card != null && card.sprite != null && card.sprite.name == "hud_panel_blue", "round card on the blue kit panel");
                    foreach (GoldUI resource in UnityEngine.Object.FindObjectsByType<GoldUI>(FindObjectsSortMode.None))
                    {
                        TMP_Text amount = resource.AmountText;
                        Check(amount != null && Fits(amount), $"{resource.ResourceType} amount '{amount?.text}' fits its plate");
                    }
                    Check(Display != null, "theme has the display font (Bungee)");
                    foreach (string name in new[] { "Round Header", "Rounds Text", "Round Timer Text" })
                        FontAndFit(round.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == name), "HUD " + name);
                    FontAndFit(UnityEngine.Object.FindAnyObjectByType<XpUI>(FindObjectsInactive.Include).GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Level Text"), "HUD level");
                    foreach (GoldUI resource in UnityEngine.Object.FindObjectsByType<GoldUI>(FindObjectsSortMode.None)) Check(Bungee(resource.AmountText), $"{resource.ResourceType} amount in Bungee");
                    Slider slider = UnityEngine.Object.FindAnyObjectByType<XpUI>(FindObjectsInactive.Include).GetComponentInChildren<Slider>(true);
                    Image fill = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "XP Fill");
                    Check(fill != null && slider.normalizedValue > .3f && Mathf.Abs(fill.fillAmount - slider.normalizedValue) < .01f,
                        $"XP fill follows the slider ({fill?.fillAmount:0.00} vs {slider.normalizedValue:0.00})");
                    Capture("c1_round_hud.png");
                    Check(RoundManager.Instance.EditorEndRound(false), "round ends through the game's path");
                    phase = 2; next = now + .3; return;
                }
                case 2:
                    if (GameManager.Instance.CurrentState == GameStates.CardSelection || GameManager.Instance.CurrentState == GameStates.RoundChoice) GameManager.Instance.ShowRoundEnd();
                    if (GameManager.Instance.CurrentState != GameStates.RoundEnd) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                    phase = 3; next = now + 2.5; return;
                case 3:
                {
                    foreach (var (name, colour) in Buttons)
                    {
                        Button button = FindButton(name);
                        Check(button != null, name + " exists");
                        var image = (Image)button.targetGraphic;
                        Check(image.sprite == ComicKit.Get($"button_{colour}_normal") && image.type == Image.Type.Sliced, $"{name}: kit sprite button_{colour}_normal");
                        Check(button.transition == Selectable.Transition.SpriteSwap && button.spriteState.highlightedSprite == ComicKit.Get($"button_{colour}_hover") &&
                              button.spriteState.pressedSprite == ComicKit.Get($"button_{colour}_pressed") && button.spriteState.disabledSprite == ComicKit.Get($"button_{colour}_disabled") &&
                              button.spriteState.selectedSprite == image.sprite, $"{name}: hover / pressed / disabled sprite swap, selected = normal");
                        Check(button.GetComponent<ComicButtonVisual>() == null && image.material == Graphic.defaultGraphicMaterial, $"{name}: old palette material component removed (no per-frame work)");
                        FontAndFit(button.GetComponentInChildren<TMP_Text>(true), name + " label");
                        Visible(button, name);
                    }
                    Transform summary = SummaryCard();
                    Check(summary != null, "round summary card exists");
                    Image panel = summary.Find("Comic Panel")?.GetComponent<Image>(), header = summary.Find("Comic Header")?.GetComponent<Image>();
                    Check(panel != null && panel.sprite == ComicKit.Get("panel_cream") && header != null && header.sprite == ComicKit.Get("header_navy") &&
                          panel.transform.GetSiblingIndex() == 0 && header.transform.GetSiblingIndex() == 1, "summary card: cream panel + navy header under its text");
                    Check(summary.Find("Amber header") == null && summary.Find("Cream paper") == null, "summary card: old flat plates gone");
                    Check(summary.Find("Title").GetComponent<TMP_Text>().color == Color.white, "summary title white on the navy header");
                    FontAndFit(summary.Find("Title").GetComponent<TMP_Text>(), "summary title");
                    var map = UnityEngine.Object.FindFirstObjectByType<RoundMapUI>();
                    var cells = map.GetComponentsInChildren<TileCellUI>(true).Select(c => c.GetComponent<Image>()).ToList();
                    int open = cells.Count(i => i.sprite == ComicKit.Get("slot_white")), locked = cells.Count(i => i.sprite == ComicKit.Get("slot_charcoal"));
                    Check(open == 25 && open + locked == cells.Count, $"map cells: {open} open white, {locked} locked charcoal");
                    Check(cells.Where(i => i.sprite == ComicKit.Get("slot_charcoal")).All(i => i.color == Color.white), "locked cells untinted");
                    var headers = map.transform.Find("Column Headers").GetComponentsInChildren<Image>(true).Concat(map.transform.Find("Row Headers").GetComponentsInChildren<Image>(true)).ToList();
                    Check(headers.Count == 22 && headers.All(i => i.sprite == ComicKit.Get("slot_blue") && i.color == Color.white), $"map coordinate boxes on the kit's blue square ({headers.Count})");
                    foreach (TMP_Text text in map.transform.Find("Column Headers").GetComponentsInChildren<TMP_Text>(true).Take(3)) FontAndFit(text, "map column");
                    var world = UnityEngine.Object.FindAnyObjectByType<GridCoordinateLabels>();
                    var plates = world.GetComponentsInChildren<SpriteRenderer>(true);
                    Check(plates.Where(r => r.name == "Plate").All(r => r.sprite == ComicKit.Get("slot_blue")) && plates.Count(r => r.name == "Gold" && r.sprite == ComicKit.Get("slot_gold")) == 22,
                        "world coordinate badges: blue kit square + gold highlight square");
                    Check(world.GetComponentsInChildren<TMP_Text>(true).All(Bungee), "world coordinate letters in Bungee");
                    Capture("c2_prep.png");
                    summaryChildren = summary.childCount;
                    prepButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                    // Hover: Unity'nin kendi geçişi hover sprite'ını gerçekten koyuyor mu.
                    Button start = FindButton("Next Round Button");
                    start.OnPointerEnter(new PointerEventData(EventSystem.current));
                    phase = 4; next = now + .4; return;
                }
                case 4:
                {
                    Button start = FindButton("Next Round Button");
                    var image = (Image)start.targetGraphic;
                    Check(image.overrideSprite == ComicKit.Get("button_blue_hover"), "hovered Next Round shows the hover sprite");
                    Capture("c3_prep_hover.png");
                    start.OnPointerExit(new PointerEventData(EventSystem.current));
                    Check(image.overrideSprite == image.sprite, "pointer exit returns to the normal sprite");
                    // Gerçek tile verileri oyunun kart yoluyla (rastgele uygun hücre); prep yeniden açılınca harita ve yeni tile kartı.
                    foreach (string tile in Tiles)
                    {
                        var modifier = AssetDatabase.LoadAssetAtPath<TileModifierSO>("Assets/ScriptableObjects/GridModifiers/" + tile + ".asset");
                        Check(modifier != null && ProgressionManager.Instance.ApplyRandomEligibleCell(modifier), "tile applied: " + tile);
                    }
                    FindButton("Placment Shop Button").onClick.Invoke();
                    phase = 5; next = now + 1; return;
                }
                case 5:
                {
                    Check(GameManager.Instance.CurrentState == GameStates.Shop, "planter shop opens from the restyled button");
                    var shop = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>();
                    Check(shop != null && shop.UsesComicKit, "planter shop on the comic kit");
                    Check(shop.GetComponent<Image>().sprite == ComicKit.Get("window_navy"), "shop window navy");
                    var images = shop.GetComponentsInChildren<Image>(true);
                    Check(images.Where(i => i.name == "Catalogue Header" || i.name == "Detail Header").All(i => i.sprite == ComicKit.Get("header_navy")) &&
                          images.First(i => i.name == "Paper").sprite == ComicKit.Get("panel_cream") &&
                          images.Where(i => i.name == "PLOTS" || i.name == "SIZE" || i.name == "GROW TIME").All(i => i.sprite == ComicKit.Get("info_cream")), "shop headers, paper and stat boxes on kit sprites");
                    var cards = images.Where(i => i.name.StartsWith("Planter Card")).ToList();
                    Check(cards.Count > 0 && cards.All(i => i.sprite == ComicKit.Get("card_portrait_cream") || i.sprite == ComicKit.Get("card_portrait_cream_selected")), $"shop cards on the cream card ({cards.Count}), selected one gold");
                    Check(((Image)FindButton("Buy Planter").targetGraphic).sprite == ComicKit.Get("button_green_normal") && ((Image)FindButton("Back").targetGraphic).sprite == ComicKit.Get("button_purple_normal"), "Buy green, Clear purple");
                    foreach (string name in new[] { "Buy Planter", "Back", "Planter Sell Button", "Exit Shop" }) Visible(FindButton(name), "shop " + name);
                    foreach (TMP_Text text in shop.GetComponentsInChildren<TMP_Text>(false).Where(t => t.font == Display)) FontAndFit(text, "shop " + text.name);
                    Check(shop.GetComponentsInChildren<TMP_Text>(false).Count(t => t.font == Display) >= 10, "shop titles, names, prices, stats and buttons in Bungee");
                    Button sell = FindButton("Planter Sell Button"), exit = FindButton("Exit Shop");
                    Check(((Image)sell.targetGraphic).sprite == ComicKit.Get("button_red_normal"), "Sell red");
                    Check(exit.gameObject.activeInHierarchy && ((Image)exit.targetGraphic).sprite == ComicKit.Get("button_blue_normal"), "Back (exit shop) blue and visible");
                    FontAndFit(exit.GetComponentInChildren<TMP_Text>(true), "exit shop label");
                    var counters = (RectTransform)UnityEngine.Object.FindAnyObjectByType<GoldUI>().transform.parent;
                    Check(!ScreenRect((RectTransform)exit.transform).Overlaps(ScreenRect(counters)), "Back button below the resource counters (no overlap)");
                    Capture("c5_shop.png");
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().ShowRoundEndUI();
                    phase = 6; next = now + 1.5; return;
                }
                case 6:
                {
                    Transform summary = SummaryCard();
                    Check(summary.childCount == summaryChildren, $"reopening: summary card children unchanged ({summary.childCount})");
                    Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == prepButtons, "reopening: no duplicated buttons");
                    Check(((Image)FindButton("Next Round Button").targetGraphic).sprite == ComicKit.Get("button_blue_normal"), "reopening: buttons keep the kit look");
                    var map = UnityEngine.Object.FindFirstObjectByType<RoundMapUI>();
                    int tinted = map.GetComponentsInChildren<TileCellUI>(true).Select(c => c.GetComponent<Image>()).Count(i => i.sprite == ComicKit.Get("slot_white") && i.color != Color.white);
                    Check(tinted == Tiles.Length, $"map: {tinted} open white cells tinted by their tile");
                    var fresh = UnityEngine.Object.FindAnyObjectByType<RoundNewTilesUI>();
                    Transform freshCard = fresh != null ? fresh.transform.Find("Card") : null;
                    Check(freshCard != null && freshCard.gameObject.activeInHierarchy && freshCard.Find("Comic Panel")?.GetComponent<Image>().sprite == ComicKit.Get("panel_cream") &&
                          freshCard.Find("Comic Header")?.GetComponent<Image>().sprite == ComicKit.Get("label_cream"), "new tiles card shown on the kit (cream panel, yellow label)");
                    Check(freshCard.GetComponentsInChildren<Image>(false).Count(i => i.name == "Tile color" && i.sprite == ComicKit.Get("slot_white")) == Tiles.Length, "new tiles card swatches are the map's white square");
                    FontAndFit(freshCard.Find("Title").GetComponent<TMP_Text>(), "new tiles title");
                    Capture("c4_prep_tiles.png");
                    // Türkçe büyük harf: "bitki" Bungee'de "BİTKİ" (i → İ); İngilizcede i kalır.
                    string language = GameText.Language;
                    var probe = new GameObject("Caps Probe", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    probe.rectTransform.SetParent(summary, false); probe.rectTransform.sizeDelta = new Vector2(300f, 60f);
                    ComicKit.Display(probe, true);
                    GameText.Language = "tr"; probe.text = "bitki <size=90%>i</size>"; probe.ForceMeshUpdate();
                    string tr = new string(probe.textInfo.characterInfo.Take(probe.textInfo.characterCount).Select(c => c.character).ToArray());
                    GameText.Language = "en"; probe.text = "bitki <size=90%>i</size>"; probe.ForceMeshUpdate();
                    string en = new string(probe.textInfo.characterInfo.Take(probe.textInfo.characterCount).Select(c => c.character).ToArray());
                    GameText.Language = language;
                    UnityEngine.Object.DestroyImmediate(probe.gameObject);
                    Check(tr == "bİtkİ İ" && en == "bitki i", $"Turkish caps: tr '{tr}', en '{en}' (tags untouched)");
                    startLanguage = GameText.Language;
                    languageIndex = 0;
                    phase = 7; next = now + .2; return;
                }
                case 7:
                    // Önce dükkân açılır, dil sonra değişir: açık dükkânın kendini yenilediği de sınanır.
                    FindButton("Placment Shop Button").onClick.Invoke();
                    GameText.Language = Languages[languageIndex];
                    phase = 8; next = now + 1.2; return;
                case 8:
                {
                    string code = GameText.Language;
                    var shopPanel = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>();
                    TMP_Text shopHeading = shopPanel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Heading");
                    Check(shopHeading.text == GameText.Get("ui.shop.heading"), $"[{code}] open shop follows the language ('{shopHeading.text}')");
                    Layout(Field("placementShopPanel").transform, "shop");
                    foreach (string name in new[] { "Planter Sell Button", "Exit Shop" }) Layout(FindButton(name).transform, name);
                    Capture($"c7_shop_{code}.png");
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().ShowRoundEndUI();
                    phase = 9; next = now + 1.5; return;
                }
                case 9:
                {
                    string code = GameText.Language;
                    Layout(Field("roundEndUI").transform, "preparation");
                    Capture($"c6_prep_{code}.png");
                    languageIndex++;
                    if (languageIndex < Languages.Length) { phase = 7; next = now + .2; return; }
                    GameText.Language = startLanguage;
                    FindButton("Skill Shop Button").onClick.Invoke();
                    phase = 20; next = now + 2; return;
                }
                case 20:
                {
                    Check(GameManager.Instance.CurrentState == GameStates.Shop, "skill shop opens from the restyled button");
                    List<SkillNodeUI> nodes = VisibleNodes();
                    Check(nodes.Count > 0, $"skill tree shows {nodes.Count} nodes");
                    foreach (SkillNodeUI ui in nodes)
                        Check(ui.FaceSprite == NodeState(ui), $"node {ui.Node.name}: face '{ui.FaceSprite}' matches its state '{NodeState(ui)}'");
                    var states = nodes.Select(NodeState).Distinct().OrderBy(x => x).ToList();
                    notes.Add("INFO node states on screen: " + string.Join(", ", states));
                    Check(states.Contains("slot_gold"), "at least one buyable node (gold)");
                    var tree = UnityEngine.Object.FindAnyObjectByType<SkillTreeUI>();
                    Check(tree.UsesKitLines, "skill tree connections use the kit line");
                    var lines = tree.GetComponentsInChildren<Image>(false).Where(i => i.transform.parent != null && i.transform.parent.name.StartsWith("Link")).ToList();
                    Check(lines.Count > 0 && lines.All(i => i.sprite == ComicKit.Get("line_cream")), $"{lines.Count} visible connection pieces are the cream ink line");
                    var board = UnityEngine.Object.FindAnyObjectByType<SkillTreeSpaceBackground>();
                    Check(board != null && board.enabled && board.UsesComicBoard, "skill tree background is the comic board");
                    // Yerleşim: yetenek verisi değişmeden daha sıkı; üst üste binme yok, sıra ve hizalar aynı.
                    List<SkillNodeUI> all = AllNodes(tree);
                    float spacing = tree.NodeSpacing, size = tree.NodeSize;
                    Check(spacing > size + 20f && all.All(n => Mathf.Abs(((RectTransform)n.transform).sizeDelta.x - size) < .5f), $"all {all.Count} nodes are {size:0} px on a {spacing:0} px grid");
                    int overlaps = 0, disorder = 0;
                    float oldExtent = 0f, newExtent = 0f;
                    for (int a = 0; a < all.Count; a++)
                    {
                        Vector2Int ga = all[a].Node.gridPosition; Vector2 la = all[a].LayoutPosition;
                        oldExtent = Mathf.Max(oldExtent, Mathf.Max(Mathf.Abs(ga.x), Mathf.Abs(ga.y)) * all[a].Spacing);
                        newExtent = Mathf.Max(newExtent, Mathf.Max(Mathf.Abs(la.x), Mathf.Abs(la.y)));
                        for (int b = a + 1; b < all.Count; b++)
                        {
                            Vector2Int gb = all[b].Node.gridPosition; Vector2 lb = all[b].LayoutPosition;
                            if (Mathf.Max(Mathf.Abs(la.x - lb.x), Mathf.Abs(la.y - lb.y)) < spacing - .5f) overlaps++;
                            if (System.Math.Sign(ga.x - gb.x) != System.Math.Sign(Mathf.RoundToInt(la.x - lb.x)) || System.Math.Sign(ga.y - gb.y) != System.Math.Sign(Mathf.RoundToInt(la.y - lb.y))) disorder++;
                        }
                    }
                    Check(overlaps == 0, $"no two skill nodes share a cell or overlap ({overlaps})");
                    Check(disorder == 0, $"layout keeps every row / column order of the skill data ({disorder})");
                    Check(newExtent < oldExtent * .85f, $"tree is tighter: farthest node {newExtent:0} px from the start (was {oldExtent:0} px)");
                    Check(nodes.Where(n => NodeState(n) == "slot_gold").All(n => n.Breathing) && nodes.Where(n => NodeState(n) != "slot_gold").All(n => !n.Breathing), "buyable nodes breathe, others do not");
                    Capture("c9_skill_tree_before.png");
                    // Gerçek satın alma yolu: altın düğümlerden ilki (TryUpgrade); karesi ve ilk noktası değişmeli.
                    SkillNodeUI buy = nodes.First(n => NodeState(n) == "slot_gold");
                    hovered = buy;
                    Check(SkillTreeManager.Instance.TryUpgrade(buy.Node), $"bought {buy.Node.name} through the game's path");
                    phase = 21; next = now + 1; return;
                }
                case 21:
                {
                    SkillNodeUI buy = hovered;
                    Check(buy.FaceSprite == NodeState(buy) && SkillTreeManager.Instance.GetCurrentLevel(buy.Node) == 1, $"after buying: {buy.Node.name} face '{buy.FaceSprite}' (state {NodeState(buy)})");
                    var dots = buy.GetComponentsInChildren<Image>(false).Where(i => i.sprite == ComicKit.Get("pip_gold") || i.sprite == ComicKit.Get("pip_empty")).ToList();
                    Check(dots.Count == buy.Node.tiers.Count && dots.Count(i => i.sprite == ComicKit.Get("pip_gold")) == 1, $"after buying: tier dots {dots.Count(i => i.sprite == ComicKit.Get("pip_gold"))}/{dots.Count} gold");
                    foreach (SkillNodeUI ui in VisibleNodes()) Check(ui.FaceSprite == NodeState(ui), $"after buying: node {ui.Node.name} still matches its state");
                    // Durum galerisi, gerçek oyun yoluyla: bu düğüm sonuna kadar (yeşil), bir başkası yarım (beyaz), sonra kaynaklar
                    // biter (alınamaz düğümler kömür).
                    SkillTreeManager tree = SkillTreeManager.Instance;
                    for (int guard = 0; guard < 10 && !tree.IsMaxLevel(buy.Node) && tree.TryUpgrade(buy.Node); guard++) { }
                    SkillNodeUI partial = VisibleNodes().FirstOrDefault(n => n != buy && NodeState(n) == "slot_gold" && n.Node.tiers.Count > 1);
                    if (partial != null) tree.TryUpgrade(partial.Node);
                    foreach (ResourceType type in new[] { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone })
                    {
                        spent[type] = ResourceManager.Instance.GetResourceAmount(type);
                        ResourceManager.Instance.SpendResource(type, spent[type]);
                    }
                    phase = 24; next = now + 1; return;
                }
                case 24:
                {
                    List<SkillNodeUI> nodes = VisibleNodes();
                    foreach (SkillNodeUI ui in nodes) Check(ui.FaceSprite == NodeState(ui), $"gallery: node {ui.Node.name} face '{ui.FaceSprite}' matches its state");
                    var states = nodes.Select(NodeState).Distinct().OrderBy(x => x).ToList();
                    notes.Add("INFO gallery node states: " + string.Join(", ", states));
                    Check(states.Contains("slot_charcoal"), "gallery: unaffordable nodes are charcoal");
                    if (SkillTreeManager.Instance.IsMaxLevel(hovered.Node)) Check(hovered.FaceSprite == "slot_green", "gallery: completed node is green");
                    else notes.Add("INFO gallery: could not complete " + hovered.Node.name + " with the test resources");
                    if (states.Contains("slot_white")) notes.Add("INFO gallery: a partly bought node is white");
                    // Bütün düğümler tek karede görünsün diye galeri çekiminde ağaç uzaklaştırılır (yalnız test; sonra geri).
                    var treeUI = UnityEngine.Object.FindAnyObjectByType<SkillTreeUI>();
                    var zoomField = typeof(SkillTreeUI).GetField("zoom", BindingFlags.NonPublic | BindingFlags.Instance);
                    var content = (RectTransform)typeof(SkillTreeUI).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(treeUI);
                    float oldZoom = (float)zoomField.GetValue(treeUI); Vector2 oldPan = content.anchoredPosition;
                    zoomField.SetValue(treeUI, .5f); content.localScale = Vector3.one * .5f; content.anchoredPosition = Vector2.zero;
                    Capture("c9_skill_states.png");
                    // Bütün düğümler (yalnız çekim için görünür kılınır; sonra oyunun kendi yenilemesi geri alır).
                    List<SkillNodeUI> every = AllNodes(treeUI);
                    var hiddenNodes = every.Where(n => !n.gameObject.activeSelf).ToList();
                    foreach (SkillNodeUI n in hiddenNodes) n.gameObject.SetActive(true);
                    zoomField.SetValue(treeUI, .5f); content.localScale = Vector3.one * .5f; content.anchoredPosition = Vector2.zero;
                    Capture("c9_skill_all.png");
                    foreach (SkillNodeUI n in hiddenNodes) n.gameObject.SetActive(false);
                    zoomField.SetValue(treeUI, oldZoom); content.localScale = Vector3.one * oldZoom; content.anchoredPosition = oldPan;
                    Check(nodes.Where(n => NodeState(n) == "slot_charcoal").All(n => !n.Breathing), "gallery: unaffordable nodes do not breathe");
                    // His: üstüne gelme ve alınamayana tıklama.
                    deniedNode = nodes.FirstOrDefault(n => NodeState(n) == "slot_charcoal");
                    probeNode = hovered;
                    probeNode.OnPointerEnter(LeftClick());
                    phase = 25; next = now + .25; return;
                }
                case 25:
                {
                    var treeUI = UnityEngine.Object.FindAnyObjectByType<SkillTreeUI>();
                    Check(probeNode.Hovered && Mathf.Abs(probeNode.transform.localScale.x - SkillNodeUI.HoverScale) < .02f, $"hovered node grows to {SkillNodeUI.HoverScale} (scale {probeNode.transform.localScale.x:0.00})");
                    Check(probeNode.transform.GetSiblingIndex() == probeNode.transform.parent.childCount - 1, "hovered node is drawn above its neighbours");
                    Color hoverColour = (Color)typeof(SkillTreeUI).GetField("hoverConnectionColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(treeUI);
                    var lit = treeUI.GetComponentsInChildren<Image>(false).Where(i => i.name == "Line" && i.color == hoverColour).ToList();
                    Check(lit.Count > 0, $"hovered node's connections light up ({lit.Count})");
                    Capture("c9_skill_hover.png");
                    probeNode.OnPointerExit(LeftClick());
                    // Sallanma kısa (0,28 sn) ve sönerek biter: tıklamadan hemen sonra ölçülür.
                    if (deniedNode != null) deniedNode.OnPointerClick(LeftClick());
                    phase = 29; next = now + .07; return;
                }
                case 29:
                {
                    if (deniedNode != null) Check(Mathf.Abs(Mathf.DeltaAngle(0f, deniedNode.transform.localEulerAngles.z)) > .2f, "clicking an unaffordable node shakes it");
                    if (deniedNode != null) Hover(deniedNode, true);   // yetmeyen düğümün bilgi kutusu
                    phase = 26; next = now + .6; return;
                }
                case 26:
                {
                    var treeUI = UnityEngine.Object.FindAnyObjectByType<SkillTreeUI>();
                    Color hoverColour = (Color)typeof(SkillTreeUI).GetField("hoverConnectionColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(treeUI);
                    Check(!probeNode.Hovered && Mathf.Abs(probeNode.transform.localScale.x - 1f) < .02f, "pointer exit returns the node to rest");
                    Check(!treeUI.GetComponentsInChildren<Image>(false).Any(i => i.name == "Line" && i.color == hoverColour), "connection highlight clears");
                    if (deniedNode != null) Check(Mathf.Abs(Mathf.DeltaAngle(0f, deniedNode.transform.localEulerAngles.z)) < .05f && Mathf.Abs(deniedNode.transform.localScale.x - 1f) < .02f, "shaken node settles back exactly");
                    if (deniedNode != null)
                    {
                        GameObject tip = ActiveTooltip();
                        Check(tip != null && tip.activeInHierarchy && tip.GetComponentsInChildren<TMP_Text>(false).Any(t => t.text.Contains("#B4231A") && t.text.Contains(GameText.Format("ui.skill.cost_missing", "", "").Trim())),
                            "unaffordable node's tooltip: price in red with the missing amount");
                        Capture("c9_skill_short.png");
                        Hover(deniedNode, false);
                    }
                    foreach (var pair in spent) ResourceManager.Instance.AddResource(pair.Key, pair.Value);
                    Hover(hovered, true);
                    skillLanguage = -1;
                    phase = 22; next = now + .8; return;
                }
                case 22:
                {
                    string code = GameText.Language;
                    CheckTooltip(code);
                    Capture($"c9_skill_{code}.png");
                    Hover(hovered, false);
                    skillLanguage++;
                    if (skillLanguage < Languages.Length)
                    {
                        GameText.Language = Languages[skillLanguage];
                        Hover(hovered, true);
                        phase = 22; next = now + .6; return;
                    }
                    GameText.Language = startLanguage;
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().ShowRoundEndUI();
                    phase = 23; next = now + 1.2; return;
                }
                case 23:
                    FindButton("Next Round Button").onClick.Invoke();
                    phase = 10; next = now + 2; return;
                case 10:
                    // HUD: dil değişince yazılar kendi güncellemesinde yenilenir (süre her saniye); ölçüm 1.5 sn sonra.
                    if (languageIndex == Languages.Length) Check(GameManager.Instance.CurrentState == GameStates.Round, "next round started from the restyled button");
                    hudLanguage = (languageIndex - Languages.Length) < Languages.Length ? Languages[languageIndex - Languages.Length] : null;
                    if (hudLanguage == null) { GameText.Language = startLanguage; notes.Add($"INFO body-text overflow warnings: {warnings}"); Finish(null); return; }
                    GameText.Language = hudLanguage;
                    phase = 11; next = now + 1.5; return;
                case 11:
                {
                    var round = UnityEngine.Object.FindAnyObjectByType<RoundUI>().transform;
                    string timer = round.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Round Timer Text").text;
                    string quota = round.GetComponentsInChildren<TMP_Text>(true).Where(t => t.transform.parent != null && t.GetComponentInParent<QuotaHUD>() != null).Select(t => t.text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
                    string expectedTimer = GameText.Format("ui.hud.time", Mathf.CeilToInt(RoundManager.Instance.RemainingTime));
                    Check(timer == expectedTimer || timer == GameText.Format("ui.hud.time", Mathf.CeilToInt(RoundManager.Instance.RemainingTime) + 1),
                        $"[{hudLanguage}] HUD timer follows the language ('{timer}')");
                    notes.Add($"INFO [{hudLanguage}] quota card first line: '{Short(quota)}'");
                    TMP_Text level = UnityEngine.Object.FindAnyObjectByType<XpUI>().GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Level Text");
                    Check(level.text == GameText.Format("ui.stamp.level", ProgressionManager.Instance.CurrentLevel), $"[{hudLanguage}] HUD level follows the language ('{level.text}')");
                    GoldUI goldUI = UnityEngine.Object.FindObjectsByType<GoldUI>(FindObjectsSortMode.None).First(g => g.ResourceType == ResourceType.Gold);
                    string goldText = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold).ToString("N0", GameText.Culture);
                    Check(goldUI.AmountText.text == goldText, $"[{hudLanguage}] gold uses the language's number format ('{goldUI.AmountText.text}' = '{goldText}')");
                    foreach (Transform root in new[] { round, UnityEngine.Object.FindAnyObjectByType<XpUI>().transform, UnityEngine.Object.FindAnyObjectByType<GoldUI>().transform.parent })
                        Layout(root, "HUD " + root.name);
                    Capture($"c8_hud_{hudLanguage}.png");
                    languageIndex++;
                    phase = 10; next = now + .1; return;
                }
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ComicKitVerification.txt", new[] { e == null ? "PASS " + notes.Count + " checks" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/ComicKit");
        Camera camera = Camera.main;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
        int[] orders = new int[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera;
            c.planeDistance = camera.nearClipPlane + .02f + (canvases.Length - i) * .01f;
            orders[i] = c.sortingOrder; c.sortingOrder = 30000 + i;   // dünyadaki sprite'ların (koordinat rozetleri) üstünde, oyundaki gibi
        }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/ComicKit/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].sortingOrder = orders[i]; }
    }
}
