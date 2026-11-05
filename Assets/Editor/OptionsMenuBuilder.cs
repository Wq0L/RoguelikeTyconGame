using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Ana menüdeki Options panelini kategorili ayarlar ekranı olarak MenuScene'e kurar (ComicUIBuilder stili).
// Tekrar çalıştırılabilir: önceki kurulumu ve eski dropdown/toggle'ı silip baştan kurar;
// paneldeki BackGround ve MainMenuUI'ın Back butonu korunur. Kurulan her şey hierarchy'den düzenlenebilir.
public static class OptionsMenuBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const float RowWidth = 1240f, RowHeight = 68f;
    static readonly Color Ink = new Color(.055f, .075f, .08f);
    static ComicUITheme theme;

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
            EditorUtility.DisplayDialog("Options Menu", "Assets/Resources/ComicUITheme.asset bulunamadı.", "Tamam");
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
        return theme != null;
    }

    // Sahneden bağımsız kurulum (doğrulama testi de bunu kullanır). Önce LoadTheme çağrılmalı.
    public static RectTransform BuildInto(OptionsUI options, Button back)
    {
        Transform panel = options.transform;

        // Önceki kurulumu ve eski kontrolleri kaldır; Back butonu ve arka plan kalır.
        if (back != null) back.transform.SetParent(panel, false);
        foreach (Transform child in panel.Cast<Transform>().ToArray())
        {
            if (child.name == "BackGround" || (back != null && child == back.transform)) continue;
            Object.DestroyImmediate(child.gameObject);
        }

        // ---------- Pencere ----------
        var window = Rect("Options Window", panel, Vector2.zero, new Vector2(1320, 900));
        var frame = Pic("Frame", window, theme.cardSprite, Vector2.zero, new Vector2(1320, 900));
        frame.color = new Color(.1f, .12f, .14f, .97f);
        frame.raycastTarget = true;
        Pic("Header", window, theme.buttonSprite, new Vector2(0, 395), new Vector2(1320, 100));
        Loc(Text("Title", window, GameText.Get("ui.options.title"), new Vector2(0, 402), new Vector2(900, 70), 56, true), "ui.options.title");

        // ---------- Sekmeler ----------
        // Nesne adları sabit (sahne yolları değişmesin); yazılar çeviri anahtarından (LocalizedText).
        string[] tabNames = { "GÖRÜNTÜ", "SES", "OYNANIŞ", "KONTROLLER" };
        string[] tabKeys = { "ui.options.tab.display", "ui.options.tab.audio", "ui.options.tab.gameplay", "ui.options.tab.controls" };
        var tabButtons = new Button[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            tabButtons[i] = Button("Tab " + tabNames[i], window, GameText.Get(tabKeys[i]), new Vector2(-480 + i * 320, 298), new Vector2(300, 76), "blue", 30);
            Loc(tabButtons[i].GetComponentInChildren<TMP_Text>(true), tabKeys[i]);
        }

        var pages = Rect("Pages", window, new Vector2(0, -28), new Vector2(RowWidth, 560));
        var display = Page(pages, "Görüntü Page");
        var audio = Page(pages, "Ses Page");
        var gameplay = Page(pages, "Oynanış Page");
        var controls = Page(pages, "Kontroller Page");

        // ---------- Görüntü (en üstte dil: her dilden oyuncu bulabilsin diye iki dilde) ----------
        var language = Stepper(display, "ui.options.language");
        var resolution = Stepper(display, "ui.options.resolution");
        var windowMode = Stepper(display, "ui.options.window_mode");
        var vSync = Toggle(display, "ui.options.vsync");
        var fpsLimit = Stepper(display, "ui.options.fps_limit");
        var quality = Stepper(display, "ui.options.quality");
        var backgroundEffects = Toggle(display, "ui.options.background_effects");

        // ---------- Ses ----------
        var masterVolume = Slider(audio, "ui.options.master_volume");
        var musicVolume = Slider(audio, "ui.options.music");
        var effectsVolume = Slider(audio, "ui.options.effects");
        var muteInBackground = Toggle(audio, "ui.options.mute_background");

        // ---------- Oynanış ----------
        var screenShake = Slider(gameplay, "ui.options.screen_shake");
        var cameraMotion = Toggle(gameplay, "ui.options.camera_motion");
        var damageNumbers = Toggle(gameplay, "ui.options.damage_numbers");
        var gridCoordinates = Toggle(gameplay, "ui.options.grid_coordinates");
        var rarityAuras = Toggle(gameplay, "ui.options.rarity_auras");
        var roundEndSlowMotion = Toggle(gameplay, "ui.options.round_end_slowmo");
        var screenFlashes = Toggle(gameplay, "ui.options.screen_flashes");

        // ---------- Kontroller ----------
        Info(controls, "ui.options.ctrl.harvest", "ui.options.key.cursor");
        Info(controls, "ui.options.ctrl.rotate", "R");
        Info(controls, "ui.options.ctrl.cancel", "ESC");
        Info(controls, "ui.options.ctrl.resonance", "ALT");
        Info(controls, "ui.options.ctrl.perf", "F9");

        // ---------- Alt kısım ----------
        var description = Text("Description", window, "", new Vector2(0, -345), new Vector2(1100, 56), 26);
        description.color = new Color(.85f, .9f, .85f);
        var reset = Button("Reset Defaults", window, GameText.Get("ui.options.defaults"), new Vector2(430, -405), new Vector2(330, 72), "gold", 30);
        Loc(reset.GetComponentInChildren<TMP_Text>(true), "ui.options.defaults");
        if (back != null)
        {
            back.transform.SetParent(window, false);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-430, -405);
            rect.sizeDelta = new Vector2(330, 72);
            theme.StyleButton(back, "red");
            var label = back.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = GameText.Get("ui.options.back");
                Loc(label, "ui.options.back");
                label.alignment = TextAlignmentOptions.Center;
                label.margin = Vector4.zero;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(10, 6);
                label.rectTransform.offsetMax = new Vector2(-10, -6);
            }
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
        Set(so, "masterVolume", masterVolume); Set(so, "musicVolume", musicVolume); Set(so, "effectsVolume", effectsVolume);
        Set(so, "muteInBackground", muteInBackground);
        Set(so, "screenShake", screenShake); Set(so, "cameraMotion", cameraMotion); Set(so, "damageNumbers", damageNumbers);
        Set(so, "gridCoordinates", gridCoordinates); Set(so, "rarityAuras", rarityAuras);
        Set(so, "roundEndSlowMotion", roundEndSlowMotion); Set(so, "screenFlashes", screenFlashes);
        so.ApplyModifiedPropertiesWithoutUndo();
        return window;
    }

    // ---------- Satırlar ----------

    static RectTransform Page(RectTransform pages, string name)
    {
        var page = Rect(name, pages, Vector2.zero, Vector2.zero);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return page;
    }

    // key: satır başlığının çeviri anahtarı; açıklama "<key>.desc". Nesne adı anahtardır (editörün diline göre değişmesin).
    static T Row<T>(RectTransform page, string key) where T : OptionRow
    {
        var background = Pic(key, page, theme.cardSprite, Vector2.zero, new Vector2(RowWidth, RowHeight));
        background.raycastTarget = true;
        var label = Loc(Text("Label", background.transform, GameText.Get(key), Vector2.zero, new Vector2(560, 56), 32), key);
        label.font = theme.headingFont;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        AnchorSide(label.rectTransform, 0f, 36f);
        var row = background.gameObject.AddComponent<T>();
        var so = new SerializedObject(row);
        Set(so, "label", label);
        so.FindProperty("description").stringValue = key + ".desc";
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    static OptionStepper Stepper(RectTransform page, string key)
    {
        var row = Row<OptionStepper>(page, key);
        var next = Button("Next", row.transform, ">", Vector2.zero, new Vector2(64, 54), "blue", 36);
        AnchorSide((RectTransform)next.transform, 1f, -30f);
        var value = Text("Value", row.transform, "-", Vector2.zero, new Vector2(320, 54), 30);
        value.font = theme.headingFont;
        AnchorSide(value.rectTransform, 1f, -104f);
        var previous = Button("Previous", row.transform, "<", Vector2.zero, new Vector2(64, 54), "blue", 36);
        AnchorSide((RectTransform)previous.transform, 1f, -434f);
        var so = new SerializedObject(row);
        Set(so, "previous", previous); Set(so, "next", next); Set(so, "value", value);
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    static OptionToggle Toggle(RectTransform page, string key)
    {
        var row = Row<OptionToggle>(page, key);
        var button = Button("Switch", row.transform, GameText.Get("ui.options.on"), Vector2.zero, new Vector2(240, 54), "green", 30);
        AnchorSide((RectTransform)button.transform, 1f, -30f);
        var so = new SerializedObject(row);
        Set(so, "button", button);
        Set(so, "state", button.GetComponentInChildren<TMP_Text>(true));
        Set(so, "theme", theme);
        so.FindProperty("onText").stringValue = "ui.options.on";
        so.FindProperty("offText").stringValue = "ui.options.off";
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    static OptionSlider Slider(RectTransform page, string key)
    {
        var row = Row<OptionSlider>(page, key);
        var value = Text("Value", row.transform, "100%", Vector2.zero, new Vector2(110, 54), 30);
        value.font = theme.headingFont;
        value.alignment = TextAlignmentOptions.MidlineRight;
        AnchorSide(value.rectTransform, 1f, -30f);

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")
        };
        var sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = "Slider";
        SetLayerRecursive(sliderObject, 5);
        var sliderRect = (RectTransform)sliderObject.transform;
        sliderRect.SetParent(row.transform, false);
        sliderRect.sizeDelta = new Vector2(360, 30);
        AnchorSide(sliderRect, 1f, -160f);
        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        foreach (var image in sliderObject.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Background") image.color = new Color(.2f, .22f, .25f);
            else if (image.name == "Fill") image.color = new Color(.36f, .82f, .45f);
            // Tutamaç yüksekliği = bar alanı (30) + 8 → 38x38 yuvarlak.
            else if (image.name == "Handle") { image.color = new Color(1f, .95f, .82f); ((RectTransform)image.transform).sizeDelta = new Vector2(38, 8); }
        }
        var so = new SerializedObject(row);
        Set(so, "slider", slider); Set(so, "value", value);
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    // badgeText: tuş adı (R, ESC …; çevrilmez) ya da çeviri anahtarı ("ui." ile başlar: İMLEÇ gibi).
    static void Info(RectTransform page, string key, string badgeText)
    {
        var row = Row<OptionInfo>(page, key);
        var badge = Pic("Key", row.transform, theme.buttonSprite, Vector2.zero, new Vector2(240, 54));
        badge.material = theme.blue != null && theme.blue.Length > 0 ? theme.blue[0] : null;
        AnchorSide(badge.rectTransform, 1f, -30f);
        bool localized = badgeText.StartsWith("ui.");
        var keyLabel = Text("Key Label", badge.transform, localized ? GameText.Get(badgeText) : badgeText, new Vector2(0, 3), new Vector2(210, 40), 30, true);
        if (localized) Loc(keyLabel, badgeText);
    }

    // ---------- Yardımcılar (ComicUIBuilder ile aynı stil) ----------

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static Image Pic(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sprite == theme.cardSprite || sprite == theme.buttonSprite)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 4;
        }
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

    static TMP_Text Text(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, bool title = false)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = title ? theme.headingFont : theme.bodyFont;
        label.fontSize = fontSize;
        label.color = title ? Color.white : Ink;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * .7f;
        label.fontSizeMax = fontSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (title) label.fontSharedMaterial = theme.outlinedText;
        return label;
    }

    static Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size, string palette, float fontSize)
    {
        var image = Pic(name, parent, theme.buttonSprite, position, size);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Text("Label", image.transform, caption, new Vector2(0, 3), size - new Vector2(20, 12), fontSize, true);
        theme.StyleButton(button, palette);
        // Tema buton yazısına kenar boşluğu ekler; 54 px'lik küçük butonlarda yazı sığmayıp hiç çizilmiyordu.
        label.margin = Vector4.zero;
        image.gameObject.AddComponent<ComicHoverMotion>();
        return button;
    }

    // Satır içinde sola (side 0) veya sağa (side 1) yaslar; x kenardan uzaklık.
    static void AnchorSide(RectTransform rect, float side, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(side, .5f);
        rect.pivot = new Vector2(side, .5f);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
}
