using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated verification only (Library/VerificationProject): builds the options menu with the real OptionsMenuBuilder, enters
// play mode and checks it.
// 20 Kasım 2026 (comic kit + "her dilde yazılar cuk otursun"):
//  - kit: no old palette button (ComicButtonVisual), every button SpriteSwap, buttons ≥ 64 px, no text that cuts (Ellipsis /
//    Truncate), Bungee texts get the Turkish capital İ (TurkishCaps), the selected tab is the gold tab;
//  - language tour (tr, en, qps ≈ %35 longer): on every tab every visible text fits its box; every stepper value, both toggle
//    states and every help line are tried one by one; one capture per language and tab (Logs/OptionsMenu/<lang>_<n>.png);
//  - the language row (Türkçe → English → Türkçe) switches the panel at once (older check, kept).
// Result: Logs/OptionsMenuVerification.txt.
[InitializeOnLoad]
public static class OptionsMenuVerification
{
    const string Key = "OptionsMenuVerification.Active";
    static readonly string[] Languages = { "tr", "en", GameText.PseudoLanguage };
    static readonly string[] TabNames = { "gorunum", "ses", "oynanis", "kontroller" };
    static double nextAt = -1;
    static int step;
    static Camera captureCamera;
    static RenderTexture texture;
    static OptionsUI options;
    static readonly List<string> lines = new();
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static OptionsMenuVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch verification only.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/OptionsMenuVerification.txt", "STARTED");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        var cameraObject = new GameObject("Capture Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .08f, .22f);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0;

        var panel = Ui("OptionsPanel", canvasObject.transform);
        Stretch(panel);
        var background = Ui("BackGround", panel).gameObject.AddComponent<Image>();
        Stretch(background.rectTransform);
        background.color = new Color(.2f, .14f, .32f);
        var backRect = Ui("Back Button", panel);
        backRect.sizeDelta = new Vector2(200, 50);
        var backImage = backRect.gameObject.AddComponent<Image>();
        var back = backRect.gameObject.AddComponent<Button>();
        back.targetGraphic = backImage;
        var backLabel = Ui("Text (TMP)", backRect).gameObject.AddComponent<TextMeshProUGUI>();
        Stretch(backLabel.rectTransform);
        backLabel.text = "Back";

        var optionsUi = panel.gameObject.AddComponent<OptionsUI>();
        if (!OptionsMenuBuilder.LoadTheme()) throw new Exception("ComicUITheme / kit sprites missing");
        OptionsMenuBuilder.BuildInto(optionsUi, back);
        EditorSceneManager.SaveScene(scene, "Assets/OptionsVerification.unity");

        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (nextAt < 0)
            {
                captureCamera = Camera.main;
                options = Object.FindFirstObjectByType<OptionsUI>();
                if (captureCamera == null || options == null) throw new Exception("Scene objects missing in play mode");
                texture = new RenderTexture(1920, 1080, 24);
                captureCamera.targetTexture = texture;
                KitChecks();
                nextAt = EditorApplication.timeSinceStartup + 0.6;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }

            // step: dil × sekme × (0 tıkla, 1 ölç + çek)
            int perLanguage = TabNames.Length * 2, total = Languages.Length * perLanguage;
            if (step < total)
            {
                int language = step / perLanguage, tab = step % perLanguage / 2;
                if (step % 2 == 0)
                {
                    if (tab == 0) GameText.Language = Languages[language];
                    ClickTab(tab);
                    // Oynanış sekmesinde bir düğmeyi KAPALI yap: kırmızı hâl de görünsün (yalnız ilk dilde; sonra açık kalır).
                    if (tab == 2 && language == 0) ClickFirstToggle();
                }
                else
                {
                    FitChecks(Languages[language], tab);
                    Capture($"{Languages[language]}_{tab}_{TabNames[tab]}");
                }
                step++;
                nextAt = EditorApplication.timeSinceStartup + (step % 2 == 1 && step % perLanguage == 1 ? 0.8 : 0.4);
                return;
            }

            GameText.Language = "tr";
            lines.Add("PASS " + LanguageChecks());
            Finish();
        }
        catch (Exception exception)
        {
            lines.Add("FAIL " + exception);
            Debug.LogException(exception);
            Finish();
        }
    }

    // ---------------------------------------------------------------- kit kuralları
    static void KitChecks()
    {
        Check(options.GetComponentsInChildren<ComicButtonVisual>(true).Length == 0, "eski palet butonu (ComicButtonVisual) yok");
        Button[] buttons = options.GetComponentsInChildren<Button>(true);
        Check(buttons.All(b => b.transition == Selectable.Transition.SpriteSwap && b.targetGraphic is Image { sprite: not null }),
            "bütün butonlar SpriteSwap + kit sprite'ı", string.Join(", ", buttons.Where(b => b.transition != Selectable.Transition.SpriteSwap).Select(b => b.name)));
        Check(buttons.All(b => ((RectTransform)b.transform).rect.height >= 63.5f), "butonlar en az 64 px",
            string.Join(", ", buttons.Where(b => ((RectTransform)b.transform).rect.height < 63.5f).Select(b => $"{b.name} {((RectTransform)b.transform).rect.height:0}")));
        TMP_Text[] texts = options.GetComponentsInChildren<TMP_Text>(true);
        Check(texts.All(t => t.overflowMode == TextOverflowModes.Overflow), "hiçbir yazı kesilmiyor (Overflow kipi)",
            string.Join(", ", texts.Where(t => t.overflowMode != TextOverflowModes.Overflow).Select(t => $"{t.name}:{t.overflowMode}")));
        var theme = (ComicUITheme)typeof(OptionsUI).GetField("theme", Private).GetValue(options);
        TMP_Text[] display = texts.Where(t => t.font == theme.displayFont).ToArray();
        Check(display.Length > 20 && display.All(t => t.textPreprocessor == ComicKit.TurkishCaps.Instance), "Bungee yazıları Türkçe büyük İ ile çizilir (TurkishCaps)", $"{display.Length} Bungee yazısı");
        var tabsSprite = (Sprite)typeof(OptionsUI).GetField("activeTabSprite", Private).GetValue(options);
        Check(tabsSprite != null && tabsSprite.name == "tab_gold", "seçili sekme altın kâğıt (tab_gold)");
        lines.Add($"INFO {options.GetComponentsInChildren<OptionRow>(true).Length} satır, {buttons.Length} buton, {texts.Length} yazı");
    }

    // ---------------------------------------------------------------- her yazı kutusuna sığar
    static void FitChecks(string language, int tab)
    {
        string at = $"[{language} · {TabNames[tab]}]";
        var bad = new List<string>();
        float smallest = float.MaxValue;
        string smallestName = "";
        void Fit(TMP_Text text, string what)
        {
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0) return;
            Rect r = text.rectTransform.rect;
            Vector4 m = text.margin;
            Bounds b = text.textBounds;
            bool fits = b.min.x >= r.xMin + m.x - 1.5f && b.max.x <= r.xMax - m.z + 1.5f && b.min.y >= r.yMin + m.w - 1.5f && b.max.y <= r.yMax - m.y + 1.5f;
            if (!fits) bad.Add($"{what} '{Short(text.text)}' {b.size.x:0}×{b.size.y:0} / {r.width:0}×{r.height:0}");
            if (text.fontSize < smallest) { smallest = text.fontSize; smallestName = what; }
            if (text.fontSize < 11.9f) bad.Add($"{what} yazı {text.fontSize:0.#} pt (< 12)");
        }

        foreach (TMP_Text text in options.GetComponentsInChildren<TMP_Text>(false))
            if (!string.IsNullOrWhiteSpace(text.text)) Fit(text, Path(text.transform));
        // Adımlayıcıların bütün değerleri (çözünürlük, ekran modu, FPS, kalite, dil) tek tek.
        foreach (OptionStepper stepper in options.GetComponentsInChildren<OptionStepper>(false))
        {
            var value = (TMP_Text)typeof(OptionStepper).GetField("value", Private).GetValue(stepper);
            var items = (string[])typeof(OptionStepper).GetField("options", Private).GetValue(stepper);
            string shown = value.text;
            foreach (string item in items) { value.text = item; Fit(value, stepper.name + " = " + item); }
            value.text = shown;
        }
        // Düğmelerin iki hâli.
        foreach (OptionToggle toggle in options.GetComponentsInChildren<OptionToggle>(false))
        {
            var state = (TMP_Text)typeof(OptionToggle).GetField("state", Private).GetValue(toggle);
            string shown = state.text;
            foreach (string key in new[] { "ui.options.on", "ui.options.off" }) { state.text = GameText.Get(key); Fit(state, toggle.name + " " + key); }
            state.text = shown;
        }
        // Bu sekmedeki her satırın açıklaması (alttaki yardım satırında).
        var help = (TMP_Text)typeof(OptionsUI).GetField("description", Private).GetValue(options);
        foreach (OptionRow row in options.GetComponentsInChildren<OptionRow>(false))
        {
            string key = (string)typeof(OptionRow).GetField("description", Private).GetValue(row);
            help.text = GameText.Resolve(key);
            Fit(help, "açıklama " + key);
        }
        help.text = GameText.Get("ui.options.hint");
        foreach (TMP_Text text in options.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate();

        Check(bad.Count == 0, $"{at} her yazı kutusuna sığıyor", bad.Count == 0 ? $"en küçük yazı {smallest:0.#} pt ({smallestName})" : string.Join(" | ", bad.Take(6)));
    }

    static string Path(Transform t) => t.parent != null && t.parent.GetComponent<OptionRow>() != null ? t.parent.name + "/" + t.name
        : t.parent != null && t.parent.parent != null && t.parent.parent.GetComponent<OptionRow>() != null ? t.parent.parent.name + "/" + t.parent.name + "/" + t.name
        : t.parent != null ? t.parent.name + "/" + t.name : t.name;

    static string Short(string text) => text.Length > 40 ? text.Substring(0, 40) + "…" : text;

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/OptionsMenuVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "PASS")} {pass}/{pass + fail}" }.Concat(lines));
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }

    static void ClickTab(int index)
    {
        var tabs = (IList)typeof(OptionsUI).GetField("tabs", Private).GetValue(options);
        var tab = tabs[index];
        var button = (Button)tab.GetType().GetField("button").GetValue(tab);
        button.onClick.Invoke();
    }

    // Dil satırı (5 Kasım 2026): Görüntü sekmesinin en üstünde; diller kendi adıyla; seçim hemen uygulanır ve ayara yazılır.
    static string LanguageChecks()
    {
        ClickTab(0);
        var row = (OptionStepper)typeof(OptionsUI).GetField("language", Private).GetValue(options);
        if (row == null) throw new Exception("language row missing");
        Transform page = row.transform.parent;
        if (row.transform.GetSiblingIndex() != 0 || !page.name.StartsWith("Görüntü")) throw new Exception("language row is not the first row of the display tab");
        TMP_Text value = (TMP_Text)typeof(OptionStepper).GetField("value", Private).GetValue(row);
        Button next = (Button)typeof(OptionStepper).GetField("next", Private).GetValue(row);
        string start = value.text;
        if (start != "Türkçe" || GameText.Language != "tr") throw new Exception($"batch starts in Turkish, row shows '{start}' ({GameText.Language})");
        string Labels() => string.Join(" | ", options.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
        TMP_Text Title() => options.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Title");
        TMP_Text Tab(string name) => options.GetComponentsInChildren<Button>(true).First(b => b.name == name).GetComponentInChildren<TMP_Text>(true);
        TMP_Text Help() => (TMP_Text)typeof(OptionsUI).GetField("description", Private).GetValue(options);
        var windowRow = (OptionStepper)typeof(OptionsUI).GetField("windowMode", Private).GetValue(options);
        TMP_Text windowValue = (TMP_Text)typeof(OptionStepper).GetField("value", Private).GetValue(windowRow);
        var vsyncRow = (OptionToggle)typeof(OptionsUI).GetField("vSync", Private).GetValue(options);
        TMP_Text vsyncState = (TMP_Text)typeof(OptionToggle).GetField("state", Private).GetValue(vsyncRow);
        if (Title().text != "AYARLAR" || Tab("Tab GÖRÜNTÜ").text != "GÖRÜNTÜ" || vsyncState.text is not ("AÇIK" or "KAPALI")
            || !new[] { "Tam Ekran", "Kenarlıksız", "Pencere" }.Contains(windowValue.text) || !Help().text.StartsWith("Bir ayarın"))
            throw new Exception($"Turkish panel texts wrong: {Title().text} / {Tab("Tab GÖRÜNTÜ").text} / {vsyncState.text} / {windowValue.text} / {Help().text}");
        if (options.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.StartsWith("ui.options"))) throw new Exception("a raw key is visible: " + Labels());

        next.onClick.Invoke();
        if (value.text != "English" || GameText.Language != "en" || GameSettings.Language != "en" || GameText.Get("ui.skip") != "Skip")
            throw new Exception($"choosing English: row '{value.text}', game '{GameText.Language}', setting '{GameSettings.Language}'");
        if (Title().text != "SETTINGS" || Tab("Tab GÖRÜNTÜ").text != "DISPLAY" || Tab("Tab KONTROLLER").text != "CONTROLS"
            || vsyncState.text is not ("ON" or "OFF") || !new[] { "Fullscreen", "Borderless", "Windowed" }.Contains(windowValue.text)
            || !Help().text.StartsWith("Hover over") || options.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.StartsWith("ui.options")))
            throw new Exception($"the panel did not switch to English at once: {Title().text} / {Tab("Tab GÖRÜNTÜ").text} / {vsyncState.text} / {windowValue.text} / {Help().text}");
        next.onClick.Invoke();
        if (value.text != "Türkçe" || GameText.Language != "tr" || GameText.Get("ui.skip") != "Atla" || Title().text != "AYARLAR" || vsyncState.text is not ("AÇIK" or "KAPALI"))
            throw new Exception("back to Turkish failed");
        return "language row: Türkçe → English → Türkçe; title, tabs, values, toggles and help line follow at once, no raw keys";
    }

    static void ClickFirstToggle()
    {
        var toggle = options.GetComponentsInChildren<OptionToggle>(false);
        if (toggle.Length < 2) return;
        var button = (Button)typeof(OptionToggle).GetField("button", Private).GetValue(toggle[1]);
        button.onClick.Invoke();
    }

    static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in options.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        captureCamera.Render();
        RenderTexture.active = texture;
        var png = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        png.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/OptionsMenu");
        File.WriteAllBytes("Logs/OptionsMenu/" + name + ".png", png.EncodeToPNG());
        Object.DestroyImmediate(png);
    }

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
