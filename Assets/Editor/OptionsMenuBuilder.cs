using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Ana menüdeki Options panelini kategorili ayarlar ekranı olarak MenuScene'e kurar.
// 20 Kasım 2026: comic kağıt kitine geçti (Docs/UI-Dili.md): lacivert pencere + başlık şeridi, kâğıt sekmeler (seçili sekme
// altın kâğıt, dükkânın seçili kartı gibi), krem satır şeritleri (aynı kesik art arda tekrar etmesin diye her ikinci satır aynalı),
// kare ok butonları, kit çubuğu + altın kare tutamaklı kaydırıcı, lacivert tuş plakaları; GERİ mavi (geri dön), VARSAYILANLAR mor
// (ikincil eylem). Bütün başlık / buton / değer yazıları Bungee. Hâller Unity'nin SpriteSwap'ı (malzeme yok, karede kod yok).
// Dil kuralı ("her dilde yazılar cuk otursun"): kutular sabit, yazı kutuya göre küçülür (en küçük 12–16), gerekirse alt satıra
// geçer, hiçbir zaman kesilmez (Overflow). Doğrulama: OptionsMenuVerification (tr / en / qps dil turu, her yazı kutusuna sığar).
// Tekrar çalıştırılabilir: önceki kurulumu siler, baştan kurar; paneldeki BackGround ve MainMenuUI'ın Back butonu korunur.
// Kurulan her şey sahneye kaydedilir, hierarchy'den düzenlenebilir (AGENTS.md editör kuralı).
public static class OptionsMenuBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string Pack = "Assets/Resources/ComicPack/", Art = "Assets/Art/UI/ComicPack/", Hud = "Assets/Resources/HudKit/";
    const float WindowWidth = 1360f, WindowHeight = 960f;
    const float RowWidth = 1220f, RowHeight = 72f, RowGap = 8f, Control = 64f, Edge = 34f;
    static readonly Color Ink = new Color32(54, 39, 54, 255);
    static readonly Color Cream = new Color32(255, 242, 210, 255);
    static readonly Color FillGreen = new Color32(98, 176, 74, 255);
    static ComicUITheme theme;
    static int rowIndex;

    // ---------------------------------------------------------------- oyun içi ayarlar (hazırlık ekranı)
    // 22 Kasım 2026 (kullanıcı: "round bitince gelen, grid haritalı ekrana küçük kare bir ayarlar simgesi; tıklayınca ayarlar açılsın").
    // GameScene ve DemoScene: ana tuvalde "Options Panel" (mürekkep rengi karartma + menüdekiyle aynı kit penceresi, aynı BuildInto),
    // hazırlık ekranının (roundEndUI) sol üst köşesinde lacivert kare dişli butonu (lacivert = pencere / bilgi zemini, ayarlar
    // penceresiyle aynı aile; açık mavi kare koordinat kutusunun rengi olduğu için kullanılmadı). Dişli: draw_icons.ps1.
    // Buton paneli açar ve en öne alır; GERİ ve Esc (UIManager) kapatır. Bağlantılar kalıcı olay olarak sahneye yazılır.
    static readonly string[] GameScenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/DemoScene.unity" };
    static readonly Color Dim = new Color32(30, 21, 32, 200);   // mürekkep (54, 39, 54) koyusu, %78

    [MenuItem("Tools/Comic UI/Oyun İçi Ayarlar (hazırlık ekranı)")]
    public static void BuildGameScenesMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Ayarlar", "Play modundan çıkıp tekrar dene.", "Tamam"); return; }
        Debug.Log("OPTIONS_INGAME_BUILD\n" + BuildGameScenes());
    }

    // Her oyun sahnesine kurar ve kaydeder; rapor döner ("HATA" içerirse bir sahne kurulamadı).
    public static string BuildGameScenes()
    {
        if (!LoadTheme()) return "HATA: ComicUITheme / kit sprite'ları yok";
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "iptal";
        string start = EditorSceneManager.GetActiveScene().path;
        var report = new System.Text.StringBuilder();
        foreach (string path in GameScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string error = BuildIntoGameScene();
            if (error == null) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.AppendLine(path + ": kuruldu"); }
            else report.AppendLine(path + ": HATA " + error);
        }
        if (!string.IsNullOrEmpty(start) && EditorSceneManager.GetActiveScene().path != start) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        return report.ToString();
    }

    static string BuildIntoGameScene()
    {
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null) return "UIManager yok";
        var uiSo = new SerializedObject(ui);
        var roundEnd = uiSo.FindProperty("roundEndUI").objectReferenceValue as GameObject;
        if (roundEnd == null) return "roundEndUI yok";
        Transform canvas = roundEnd.transform.parent;

        // Panel: tam ekran, başta kapalı; karartma tıklamayı tutar (arkadaki harita / butonlar basılmaz).
        Transform found = canvas.Find("Options Panel");
        var panel = found != null ? (RectTransform)found : Rect("Options Panel", canvas, Vector2.zero, Vector2.zero);
        Stretch(panel);
        Transform dimFound = panel.Find("BackGround");
        var dim = dimFound != null ? dimFound.GetComponent<Image>() : Rect("BackGround", panel, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Stretch(dim.rectTransform);
        dim.sprite = null;
        dim.color = Dim;
        dim.raycastTarget = true;
        var options = panel.GetComponent<OptionsUI>() ?? panel.gameObject.AddComponent<OptionsUI>();
        Transform backFound = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Back Button")?.transform;
        Button back;
        if (backFound != null) back = backFound.GetComponent<Button>();
        else
        {
            var image = Rect("Back Button", panel, Vector2.zero, new Vector2(360f, 80f)).gameObject.AddComponent<Image>();
            back = image.gameObject.AddComponent<Button>();
            back.targetGraphic = image;
            Label("Label", image.transform, "ui.options.back", Vector2.zero, Vector2.zero, 32f, 14f, true, Color.white, TextAlignmentOptions.Center, true);
        }
        BuildInto(options, back);
        ClearPersistent(back.onClick);
        UnityEventTools.AddBoolPersistentListener(back.onClick, panel.gameObject.SetActive, false);
        panel.gameObject.SetActive(false);
        panel.SetAsLastSibling();

        // Hazırlık ekranının sol üst köşesi: lacivert kare + krem dişli.
        Transform buttonFound = roundEnd.transform.Find("Settings Button");
        Image square = buttonFound != null ? buttonFound.GetComponent<Image>() : Rect("Settings Button", roundEnd.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        var rect = square.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);   // ComicHud.Margin
        rect.sizeDelta = new Vector2(IconSquare, IconSquare);   // kullanıcı, 22 Kasım: "kare biraz daha küçük"; dişli sıkı oturur
        square.sprite = Sprite(Art + "slot_button_navy_normal.png");
        square.type = Image.Type.Sliced;
        square.pixelsPerUnitMultiplier = square.sprite.rect.height / IconSquare;
        square.raycastTarget = true;
        var button = square.GetComponent<Button>() ?? square.gameObject.AddComponent<Button>();
        button.targetGraphic = square;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Sprite(Art + "slot_button_navy_hover.png"),
            pressedSprite = Sprite(Art + "slot_button_navy_pressed.png"),
            selectedSprite = square.sprite,
            disabledSprite = Sprite(Art + "slot_button_navy_disabled.png"),
        };
        Transform iconFound = square.transform.Find("Icon");
        var icon = iconFound != null ? iconFound.GetComponent<Image>() : Rect("Icon", square.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        icon.sprite = Sprite(Art + "icon_gear.png");
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Inset(icon.rectTransform, 5f, 5f, 8f, 3f);   // dişli kareyi doldurur; altta karenin 3B kenarı kadar pay
        if (square.GetComponent<ComicHoverMotion>() == null) square.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.1f;
        ClearPersistent(button.onClick);
        UnityEventTools.AddBoolPersistentListener(button.onClick, panel.gameObject.SetActive, true);
        UnityEventTools.AddVoidPersistentListener(button.onClick, panel.SetAsLastSibling);

        SoundSquare(roundEnd.transform);

        uiSo.FindProperty("optionsPanel").objectReferenceValue = panel.gameObject;
        uiSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
        return null;
    }

    const float IconSquare = 56f, IconGap = 8f;

    // Ayarlar karesinin sağında ses karesi: açıkken lacivert + krem hoparlör, kapalıyken gri + gri hoparlör + kırmızı çizgi.
    static void SoundSquare(Transform roundEnd)
    {
        Transform found = roundEnd.Find("Sound Button");
        Image square = found != null ? found.GetComponent<Image>() : Rect("Sound Button", roundEnd, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        var rect = square.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f + IconSquare + IconGap, -24f);
        rect.sizeDelta = new Vector2(IconSquare, IconSquare);
        square.sprite = Sprite(Art + "slot_button_navy_normal.png");
        square.type = Image.Type.Sliced;
        square.pixelsPerUnitMultiplier = square.sprite.rect.height / IconSquare;
        square.raycastTarget = true;
        var button = square.GetComponent<Button>() ?? square.gameObject.AddComponent<Button>();
        button.targetGraphic = square;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState States(string colour) => new SpriteState
        {
            highlightedSprite = Sprite($"{Art}slot_button_{colour}_hover.png"),
            pressedSprite = Sprite($"{Art}slot_button_{colour}_pressed.png"),
            selectedSprite = Sprite($"{Art}slot_button_{colour}_normal.png"),
            disabledSprite = Sprite($"{Art}slot_button_{colour}_disabled.png"),
        };
        button.spriteState = States("navy");
        Transform iconFound = square.transform.Find("Icon");
        var icon = iconFound != null ? iconFound.GetComponent<Image>() : Rect("Icon", square.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        icon.sprite = Sprite(Art + "icon_sound.png");
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Inset(icon.rectTransform, 5f, 5f, 8f, 3f);
        if (square.GetComponent<ComicHoverMotion>() == null) square.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.1f;
        ClearPersistent(button.onClick);   // tıklamayı SoundToggleButton bağlar (Awake)
        var toggle = square.GetComponent<SoundToggleButton>() ?? square.gameObject.AddComponent<SoundToggleButton>();
        toggle.SetSprites(button, icon, Sprite(Art + "slot_button_navy_normal.png"), States("navy"), Sprite(Art + "slot_button_grey_normal.png"), States("grey"),
            Sprite(Art + "icon_sound.png"), Sprite(Art + "icon_sound_off.png"));
        EditorUtility.SetDirty(toggle);
    }

    static void ClearPersistent(UnityEngine.Events.UnityEventBase evt)
    {
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(evt, i);
    }

    [MenuItem("Tools/Comic UI/Build Options Menu")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Options Menu", "Play modundan çıkıp tekrar dene.", "Tamam");
            return;
        }
        if (!LoadTheme())
        {
            EditorUtility.DisplayDialog("Options Menu", "ComicUITheme (Bungee fontuyla) ya da comic kit sprite'ları bulunamadı.", "Tamam");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MenuScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MenuScenePath);
        }

        var options = Object.FindFirstObjectByType<OptionsUI>(FindObjectsInactive.Include);
        var menu = Object.FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (options == null || menu == null)
        {
            EditorUtility.DisplayDialog("Options Menu", "MenuScene'de OptionsUI veya MainMenuUI bulunamadı.", "Tamam");
            return;
        }

        var back = new SerializedObject(menu).FindProperty("backButton").objectReferenceValue as Button;
        BuildInto(options, back);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("OPTIONS_MENU_BUILD_PASS");
    }

    public static bool LoadTheme()
    {
        theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
        return theme != null && theme.displayFont != null && Sprite(Pack + "window_navy.png") != null && Sprite(Art + "tab_gold.png") != null;
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    // Sahneden bağımsız kurulum (doğrulama testi de bunu kullanır). Önce LoadTheme çağrılmalı.
    public static RectTransform BuildInto(OptionsUI options, Button back)
    {
        Transform panel = options.transform;
        rowIndex = 0;

        // Önceki kurulumu ve eski kontrolleri kaldır; Back butonu ve arka plan kalır.
        if (back != null) back.transform.SetParent(panel, false);
        foreach (Transform child in panel.Cast<Transform>().ToArray())
        {
            if (child.name == "BackGround" || (back != null && child == back.transform)) continue;
            Object.DestroyImmediate(child.gameObject);
        }

        // ---------- Pencere + başlık şeridi ----------
        var window = Rect("Options Window", panel, Vector2.zero, new Vector2(WindowWidth, WindowHeight));
        var frame = Sliced("Frame", window, Pack + "window_navy.png", Vector2.zero, new Vector2(WindowWidth, WindowHeight), 1.15f);
        frame.raycastTarget = true;   // arkadaki menüye tıklanmasın
        var header = Sliced("Header", window, Pack + "header_navy.png", new Vector2(0f, WindowHeight * .5f - 8f), new Vector2(720f, 112f), 0f);
        Loc(Label("Title", header.transform, "ui.options.title", new Vector2(0f, 6f), new Vector2(620f, 78f), 54f, 24f, true, Color.white, TextAlignmentOptions.Center, false), "ui.options.title");

        // ---------- Sekmeler (seçili sekme altın kâğıt; OptionsUI hâlleri değiştirir) ----------
        // Nesne adları sabit (sahne yolları değişmesin); yazılar çeviri anahtarından (LocalizedText).
        string[] tabNames = { "GÖRÜNTÜ", "SES", "OYNANIŞ", "KONTROLLER" };
        string[] tabKeys = { "ui.options.tab.display", "ui.options.tab.audio", "ui.options.tab.gameplay", "ui.options.tab.controls" };
        var tabButtons = new Button[tabNames.Length];
        const float tabWidth = 286f, tabGap = 18f;
        float firstTab = -(tabNames.Length - 1) * (tabWidth + tabGap) * .5f;
        for (int i = 0; i < tabNames.Length; i++)
        {
            var image = Sliced("Tab " + tabNames[i], window, Art + "tab_blue.png", new Vector2(firstTab + i * (tabWidth + tabGap), 330f), new Vector2(tabWidth, 76f), 0f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = TabStates(false);
            var label = Label("Label", image.transform, tabKeys[i], Vector2.zero, Vector2.zero, 28f, 13f, false, Ink, TextAlignmentOptions.Center, true);
            Inset(label.rectTransform, 18f, 18f, 16f, 8f);   // altta sekmenin renk şeridi
            Loc(label, tabKeys[i]);
            image.gameObject.AddComponent<ComicHoverMotion>();
            tabButtons[i] = button;
        }

        var pages = Rect("Pages", window, new Vector2(0f, -2f), new Vector2(RowWidth, 7 * RowHeight + 6 * RowGap));
        var display = Page(pages, "Görüntü Page");
        var audio = Page(pages, "Ses Page");
        var gameplay = Page(pages, "Oynanış Page");
        var controls = Page(pages, "Kontroller Page");

        // ---------- Görüntü (en üstte dil: her dilden oyuncu bulabilsin diye iki dilde) ----------
        rowIndex = 0;
        var language = Stepper(display, "ui.options.language");
        var resolution = Stepper(display, "ui.options.resolution");
        var windowMode = Stepper(display, "ui.options.window_mode");
        var vSync = Toggle(display, "ui.options.vsync");
        var fpsLimit = Stepper(display, "ui.options.fps_limit");
        var quality = Stepper(display, "ui.options.quality");
        // Arka plan: Kapalı / Sabit / Güce göre (23 Kasım 2026: gökyüzü müzik gücüyle renk değiştirir; sayfa 7 satırla dolu, ayrı satır yerine üç seçenek).
        var backgroundEffects = Stepper(display, "ui.options.background_effects");

        // ---------- Ses ----------
        rowIndex = 0;
        var sound = Toggle(audio, "ui.options.sound");
        var masterVolume = Slider(audio, "ui.options.master_volume");
        var musicVolume = Slider(audio, "ui.options.music");
        var effectsVolume = Slider(audio, "ui.options.effects");
        var muteInBackground = Toggle(audio, "ui.options.mute_background");

        // ---------- Oynanış ----------
        rowIndex = 0;
        var screenShake = Slider(gameplay, "ui.options.screen_shake");
        var cameraMotion = Toggle(gameplay, "ui.options.camera_motion");
        var damageNumbers = Toggle(gameplay, "ui.options.damage_numbers");
        var gridCoordinates = Toggle(gameplay, "ui.options.grid_coordinates");
        var rarityAuras = Toggle(gameplay, "ui.options.rarity_auras");
        var roundEndSlowMotion = Toggle(gameplay, "ui.options.round_end_slowmo");
        var screenFlashes = Toggle(gameplay, "ui.options.screen_flashes");

        // ---------- Kontroller ----------
        rowIndex = 0;
        Info(controls, "ui.options.ctrl.harvest", "ui.options.key.cursor");
        Info(controls, "ui.options.ctrl.rotate", "R");
        Info(controls, "ui.options.ctrl.cancel", "ESC");
        Info(controls, "ui.options.ctrl.tab", "TAB");
        Info(controls, "ui.options.ctrl.resonance", "ALT");
        Info(controls, "ui.options.ctrl.perf", "F9");

        // ---------- Alt kısım: açıklama satırı, GERİ (mavi), VARSAYILANLAR (mor) ----------
        var description = Label("Description", window, null, new Vector2(0f, -318f), new Vector2(RowWidth - 20f, 64f), 24f, 14f, false, Cream, TextAlignmentOptions.Center, true);
        description.font = theme.bodyFont;
        description.fontSharedMaterial = theme.bodyFont.material;
        var reset = KitButton("Reset Defaults", window, "ui.options.defaults", new Vector2(420f, -408f), new Vector2(360f, 80f), "purple", 32f);
        if (back != null)
        {
            back.transform.SetParent(window, false);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-420f, -408f);
            rect.sizeDelta = new Vector2(360f, 80f);
            rect.localScale = Vector3.one;
            foreach (var old in back.GetComponents<ComicButtonVisual>()) Object.DestroyImmediate(old);
            var label = back.GetComponentInChildren<TMP_Text>(true);
            if (label == null) label = Label("Label", back.transform, null, Vector2.zero, Vector2.zero, 32f, 14f, true, Color.white, TextAlignmentOptions.Center, true);
            Loc(label, "ui.options.back");
            StyleKitButton(back, "blue", label, 32f);
            if (back.GetComponent<ComicHoverMotion>() == null) back.gameObject.AddComponent<ComicHoverMotion>();
        }

        // ---------- Bağlantılar ----------
        var so = new SerializedObject(options);
        Set(so, "theme", theme);
        var tabs = so.FindProperty("tabs");
        RectTransform[] pageRoots = { display, audio, gameplay, controls };
        tabs.arraySize = tabButtons.Length;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var tab = tabs.GetArrayElementAtIndex(i);
            tab.FindPropertyRelative("button").objectReferenceValue = tabButtons[i];
            tab.FindPropertyRelative("page").objectReferenceValue = pageRoots[i].gameObject;
        }
        Set(so, "description", description);
        so.FindProperty("defaultDescription").stringValue = "ui.options.hint";
        Set(so, "resetButton", reset);
        Set(so, "language", language);
        Set(so, "resolution", resolution); Set(so, "windowMode", windowMode); Set(so, "vSync", vSync);
        Set(so, "fpsLimit", fpsLimit); Set(so, "quality", quality); Set(so, "backgroundEffects", backgroundEffects);
        Set(so, "sound", sound); Set(so, "masterVolume", masterVolume); Set(so, "musicVolume", musicVolume); Set(so, "effectsVolume", effectsVolume);
        Set(so, "muteInBackground", muteInBackground);
        Set(so, "screenShake", screenShake); Set(so, "cameraMotion", cameraMotion); Set(so, "damageNumbers", damageNumbers);
        Set(so, "gridCoordinates", gridCoordinates); Set(so, "rarityAuras", rarityAuras);
        Set(so, "roundEndSlowMotion", roundEndSlowMotion); Set(so, "screenFlashes", screenFlashes);
        so.ApplyModifiedPropertiesWithoutUndo();
        options.SetKitSprites(Sprite(Art + "tab_blue.png"), TabStates(false), Sprite(Art + "tab_gold.png"), TabStates(true));
        EditorUtility.SetDirty(options);
        return window;
    }

    static SpriteState TabStates(bool selected)
    {
        Sprite gold = Sprite(Art + "tab_gold.png"), normal = Sprite(Art + "tab_blue.png");
        return selected
            ? new SpriteState { highlightedSprite = gold, pressedSprite = gold, selectedSprite = gold, disabledSprite = gold }
            : new SpriteState { highlightedSprite = Sprite(Art + "tab_blue_hover.png"), pressedSprite = Sprite(Art + "tab_blue_pressed.png"), selectedSprite = normal, disabledSprite = normal };
    }

    static SpriteState ButtonStates(string colour) => new SpriteState
    {
        highlightedSprite = Sprite($"{Pack}button_{colour}_hover.png"),
        pressedSprite = Sprite($"{Pack}button_{colour}_pressed.png"),
        selectedSprite = Sprite($"{Pack}button_{colour}_normal.png"),   // tıklanınca "seçili" kalıp parlamasın
        disabledSprite = Sprite($"{Pack}button_{colour}_disabled.png"),
    };

    // ---------- Satırlar ----------

    static RectTransform Page(RectTransform pages, string name)
    {
        var page = Rect(name, pages, Vector2.zero, Vector2.zero);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowGap;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return page;
    }

    // key: satır başlığının çeviri anahtarı; açıklama "<key>.desc". Nesne adı anahtardır (editörün diline göre değişmesin).
    // controlWidth: sağdaki denetimin kapladığı genişlik; başlık kalan alana sığar (küçülür, gerekirse iki satır).
    static T Row<T>(RectTransform page, string key, float controlWidth) where T : OptionRow
    {
        var root = Rect(key, page, Vector2.zero, new Vector2(RowWidth, RowHeight));
        // Krem şerit ayrı nesnede: her ikinci satırda aynalanır (aynı kesik art arda tekrar etmesin), yazılar aynalanmaz.
        var paper = Sliced("Paper", root, Pack + "label_cream.png", Vector2.zero, Vector2.zero, 0f);
        Stretch(paper.rectTransform);
        paper.pixelsPerUnitMultiplier = paper.sprite.rect.height / RowHeight;
        paper.raycastTarget = true;   // üzerine gelince açıklama (olay satıra çıkar)
        if (rowIndex++ % 2 == 1) paper.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        float labelWidth = RowWidth - 34f - controlWidth - 28f;
        var label = Label("Label", root, key, Vector2.zero, new Vector2(labelWidth, RowHeight - 10f), 28f, 14f, false, Ink, TextAlignmentOptions.MidlineLeft, true);
        AnchorSide(label.rectTransform, 0f, 34f, 3f);
        Loc(label, key);
        var row = root.gameObject.AddComponent<T>();
        var so = new SerializedObject(row);
        Set(so, "label", label);
        so.FindProperty("description").stringValue = key + ".desc";
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    const float ValueWidth = 300f;

    static OptionStepper Stepper(RectTransform page, string key)
    {
        var row = Row<OptionStepper>(page, key, 2 * Control + ValueWidth + 16f);
        var next = SquareButton("Next", row.transform, ">");
        AnchorSide((RectTransform)next.transform, 1f, -Edge, 0f);
        var value = Label("Value", row.transform, null, Vector2.zero, new Vector2(ValueWidth, Control - 6f), 28f, 13f, false, Ink, TextAlignmentOptions.Center, true);
        value.text = "-";
        AnchorSide(value.rectTransform, 1f, -Edge - Control - 8f, 3f);
        var previous = SquareButton("Previous", row.transform, "<");
        AnchorSide((RectTransform)previous.transform, 1f, -Edge - Control - 16f - ValueWidth, 0f);
        var so = new SerializedObject(row);
        Set(so, "previous", previous); Set(so, "next", next); Set(so, "value", value);
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    const float SwitchWidth = 230f;

    static OptionToggle Toggle(RectTransform page, string key)
    {
        var row = Row<OptionToggle>(page, key, SwitchWidth);
        var button = KitButton("Switch", row.transform, "ui.options.on", Vector2.zero, new Vector2(SwitchWidth, Control), "green", 28f);
        // Yazıyı OptionToggle yazar (AÇIK / KAPALI): sabit çeviri bağı olmasın.
        Object.DestroyImmediate(button.GetComponentInChildren<LocalizedText>(true));
        AnchorSide((RectTransform)button.transform, 1f, -Edge, 0f);
        var so = new SerializedObject(row);
        Set(so, "button", button);
        Set(so, "state", button.GetComponentInChildren<TMP_Text>(true));
        Set(so, "theme", theme);
        so.FindProperty("onText").stringValue = "ui.options.on";
        so.FindProperty("offText").stringValue = "ui.options.off";
        so.ApplyModifiedPropertiesWithoutUndo();
        row.SetKitSprites(Sprite(Pack + "button_green_normal.png"), ButtonStates("green"), Sprite(Pack + "button_red_normal.png"), ButtonStates("red"));
        EditorUtility.SetDirty(row);
        return row;
    }

    const float SliderWidth = 380f, PercentWidth = 112f;

    static OptionSlider Slider(RectTransform page, string key)
    {
        var row = Row<OptionSlider>(page, key, SliderWidth + 20f + PercentWidth);
        var value = Label("Value", row.transform, null, Vector2.zero, new Vector2(PercentWidth, Control - 6f), 28f, 13f, false, Ink, TextAlignmentOptions.MidlineRight, false);
        value.text = "100%";
        AnchorSide(value.rectTransform, 1f, -Edge, 3f);

        // Kit çubuğu: bar_track ve bar_fill aynı dikdörtgen ve aynı 9-dilim ölçeğinde (dolgu tam içine oturur), dolgu Slider'ın
        // çapasıyla uzar; tutamak altın kare (slot_gold), çubuğun ortasında, kendi boyunda.
        var root = Rect("Slider", row.transform, Vector2.zero, new Vector2(SliderWidth, Control));
        AnchorSide(root, 1f, -Edge - PercentWidth - 20f, 0f);
        const float trackHeight = 34f, knob = 44f;
        float multiplier = 116f / trackHeight;
        var track = Sliced("Track", root, Art + "bar_track.png", Vector2.zero, new Vector2(SliderWidth, trackHeight), multiplier);
        var fillArea = Rect("Fill Area", root, Vector2.zero, new Vector2(SliderWidth, trackHeight));
        var fill = Sliced("Fill", fillArea, Art + "bar_fill.png", Vector2.zero, Vector2.zero, multiplier);
        Stretch(fill.rectTransform);
        fill.color = FillGreen;
        var handleArea = Rect("Handle Slide Area", root, Vector2.zero, new Vector2(SliderWidth - knob, knob));
        var handle = Sliced("Handle", handleArea, Pack + "slot_gold.png", Vector2.zero, new Vector2(knob, knob), 256f / knob);
        handle.rectTransform.sizeDelta = new Vector2(knob, 0f);   // Slider dikeyde alanı doldurur: yükseklik = alan (knob)
        handle.raycastTarget = true;
        track.raycastTarget = true;
        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.None;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        handle.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.12f;
        var so = new SerializedObject(row);
        Set(so, "slider", slider); Set(so, "value", value);
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    const float BadgeWidth = 230f;

    // badgeText: tuş adı (R, ESC …; çevrilmez) ya da çeviri anahtarı ("ui." ile başlar: İMLEÇ gibi). Lacivert HUD plakası.
    static void Info(RectTransform page, string key, string badgeText)
    {
        var row = Row<OptionInfo>(page, key, BadgeWidth);
        var badge = Sliced("Key", row.transform, Hud + "hud_plate_navy.png", Vector2.zero, new Vector2(BadgeWidth, 58f), 0f);
        AnchorSide(badge.rectTransform, 1f, -Edge, 0f);
        bool localized = badgeText.StartsWith("ui.");
        var keyLabel = Label("Key Label", badge.transform, localized ? badgeText : null, Vector2.zero, Vector2.zero, 26f, 12f, true, Color.white, TextAlignmentOptions.Center, false);
        Inset(keyLabel.rectTransform, 22f, 22f, 10f, 8f);
        if (localized) Loc(keyLabel, badgeText); else keyLabel.text = badgeText;
    }

    // ---------- Yapı taşları ----------

    static Button KitButton(string name, Transform parent, string key, Vector2 position, Vector2 size, string colour, float fontSize)
    {
        var image = Simple(name, parent, $"{Pack}button_{colour}_normal.png", position, size);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label("Label", image.transform, key, Vector2.zero, Vector2.zero, fontSize, 13f, true, Color.white, TextAlignmentOptions.Center, true);
        Loc(label, key);
        StyleKitButton(button, colour, label, fontSize);
        image.gameObject.AddComponent<ComicHoverMotion>();
        return button;
    }

    // Kit butonu: 9-dilim (dikeyde 1:1), SpriteSwap hâlleri; yazı sprite'ın üst ~%80'inde (alt şerit 3B kenar).
    static void StyleKitButton(Button button, string colour, TMP_Text label, float fontSize)
    {
        var image = (Image)button.targetGraphic;
        image.material = null;
        image.color = Color.white;
        image.sprite = Sprite($"{Pack}button_{colour}_normal.png");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / ((RectTransform)button.transform).sizeDelta.y;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = ButtonStates(colour);
        Display(label, true, Color.white, fontSize, 13f, true);
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        Inset(label.rectTransform, 22f, 22f, ((RectTransform)button.transform).sizeDelta.y * .2f, 8f);
    }

    // Kare ok butonu (mavi kare, dört hâl aynı silüetten).
    static Button SquareButton(string name, Transform parent, string glyph)
    {
        var image = Sliced(name, parent, Art + "slot_button_blue_normal.png", Vector2.zero, new Vector2(Control, Control), 0f);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Sprite(Art + "slot_button_blue_hover.png"),
            pressedSprite = Sprite(Art + "slot_button_blue_pressed.png"),
            selectedSprite = image.sprite,
            disabledSprite = Sprite(Art + "slot_button_blue_disabled.png"),
        };
        var label = Label("Label", image.transform, null, Vector2.zero, Vector2.zero, 34f, 20f, true, Color.white, TextAlignmentOptions.Center, false);
        Inset(label.rectTransform, 6f, 10f, 10f, 6f);
        label.text = glyph;
        image.gameObject.AddComponent<ComicHoverMotion>().hoverScale = 1.1f;
        return button;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    // 9-dilim; multiplier 0: dikeyde 1:1 (sprite yüksekliği / hedef yükseklik), yatayda orta parça uzar.
    static Image Sliced(string name, Transform parent, string sprite, Vector2 position, Vector2 size, float multiplier)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = Sprite(sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier > 0f ? multiplier : image.sprite != null && size.y > 1f ? image.sprite.rect.height / size.y : 1f;
        image.raycastTarget = false;
        return image;
    }

    static Image Simple(string name, Transform parent, string sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = Sprite(sprite);
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    // Yazıyı çeviri anahtarına bağlar (oyunda dil değişince kendiliğinden yenilenir); sahnedeki metin editörde yer tutucudur.
    static TMP_Text Loc(TMP_Text label, string key)
    {
        if (label == null) return null;
        var loc = label.GetComponent<LocalizedText>();
        if (loc == null) loc = label.gameObject.AddComponent<LocalizedText>();
        var so = new SerializedObject(loc);
        so.FindProperty("key").stringValue = key;
        so.ApplyModifiedPropertiesWithoutUndo();
        return label;
    }

    // Bungee yazı (outlined: koyu zeminde beyaz + mürekkep kontur; değilse krem zeminde koyu mürekkep). Kutuya göre küçülür,
    // wrap: gerekirse alt satıra geçer; hiçbir zaman kesilmez. Türkçe büyük İ: OptionsUI.Awake TurkishCaps bağlar.
    static TMP_Text Label(string name, Transform parent, string key, Vector2 position, Vector2 size, float max, float min, bool outlined, Color color, TextAlignmentOptions alignment, bool wrap)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = key != null ? GameText.Get(key) : "";
        Display(label, outlined, color, max, min, wrap);
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    static void Display(TMP_Text label, bool outlined, Color color, float max, float min, bool wrap)
    {
        label.font = theme.displayFont;
        label.fontSharedMaterial = outlined && theme.displayOutline != null ? theme.displayOutline : theme.displayFont.material;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.fontSize = max;
        label.enableAutoSizing = true;
        label.fontSizeMax = max;
        label.fontSizeMin = min;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.extraPadding = true;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Ebeveyni doldurur, kenarlardan içe: sol, sağ, alt, üst.
    static void Inset(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // Satır içinde sola (side 0) veya sağa (side 1) yaslar; x kenardan uzaklık, y dikey kaydırma (şeridin alt 3B kenarı için).
    static void AnchorSide(RectTransform rect, float side, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(side, .5f);
        rect.pivot = new Vector2(side, .5f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
}
