using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated verification only (Library/VerificationProject): builds the options menu with the real
// OptionsMenuBuilder, enters play mode, and renders each tab to Logs/OptionsMenu_<n>.png.
[InitializeOnLoad]
public static class OptionsMenuVerification
{
    const string Key = "OptionsMenuVerification.Active";
    static double nextAt = -1;
    static int step;
    static Camera captureCamera;
    static RenderTexture texture;
    static OptionsUI options;

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
        if (!OptionsMenuBuilder.LoadTheme()) throw new Exception("ComicUITheme missing");
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
                nextAt = EditorApplication.timeSinceStartup + 0.6;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }

            int tab = step / 2;
            if (step % 2 == 0)
            {
                ClickTab(tab);
                // Oynanış sekmesinde bir düğmeyi KAPALI yap: kırmızı palet de görünsün.
                if (tab == 2) ClickFirstToggle();
            }
            else Capture(tab);
            step++;
            if (step < 8) { nextAt = EditorApplication.timeSinceStartup + 0.4; return; }

            string language = LanguageChecks();
            SessionState.SetBool(Key, false);
            File.WriteAllText("Logs/OptionsMenuVerification.txt", "PASS " + options.GetComponentsInChildren<OptionRow>(true).Length + " rows; " + language);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText("Logs/OptionsMenuVerification.txt", "FAIL " + exception);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void ClickTab(int index)
    {
        var tabs = (IList)typeof(OptionsUI).GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(options);
        var tab = tabs[index];
        var button = (Button)tab.GetType().GetField("button").GetValue(tab);
        button.onClick.Invoke();
    }

    // Dil satırı (5 Kasım 2026): Görüntü sekmesinin en üstünde; diller kendi adıyla; seçim hemen uygulanır ve ayara yazılır.
    static string LanguageChecks()
    {
        ClickTab(0);
        var row = (OptionStepper)typeof(OptionsUI).GetField("language", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(options);
        if (row == null) throw new Exception("language row missing");
        Transform page = row.transform.parent;
        if (row.transform.GetSiblingIndex() != 0 || !page.name.StartsWith("Görüntü")) throw new Exception("language row is not the first row of the display tab");
        TMP_Text value = (TMP_Text)typeof(OptionStepper).GetField("value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(row);
        Button next = (Button)typeof(OptionStepper).GetField("next", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(row);
        string start = value.text;
        if (start != "Türkçe" || GameText.Language != "tr") throw new Exception($"batch starts in Turkish, row shows '{start}' ({GameText.Language})");
        string Labels() => string.Join(" | ", options.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
        TMP_Text Title() => options.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Title");
        TMP_Text Tab(string name) => options.GetComponentsInChildren<Button>(true).First(b => b.name == name).GetComponentInChildren<TMP_Text>(true);
        TMP_Text Help() => (TMP_Text)typeof(OptionsUI).GetField("description", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(options);
        var windowRow = (OptionStepper)typeof(OptionsUI).GetField("windowMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(options);
        TMP_Text windowValue = (TMP_Text)typeof(OptionStepper).GetField("value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(windowRow);
        var vsyncRow = (OptionToggle)typeof(OptionsUI).GetField("vSync", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(options);
        TMP_Text vsyncState = (TMP_Text)typeof(OptionToggle).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vsyncRow);
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
        Capture(10);
        next.onClick.Invoke();
        if (value.text != "Türkçe" || GameText.Language != "tr" || GameText.Get("ui.skip") != "Atla" || Title().text != "AYARLAR" || vsyncState.text is not ("AÇIK" or "KAPALI"))
            throw new Exception("back to Turkish failed");
        return "language row: Türkçe → English → Türkçe; title, tabs, values, toggles and help line follow at once, no raw keys";
    }

    static void ClickFirstToggle()
    {
        var toggle = options.GetComponentsInChildren<OptionToggle>(false);
        if (toggle.Length < 2) return;
        var button = (Button)typeof(OptionToggle).GetField("button", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(toggle[1]);
        button.onClick.Invoke();
    }

    static void Capture(int tab)
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
        File.WriteAllBytes("Logs/OptionsMenu_" + tab + ".png", png.EncodeToPNG());
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
