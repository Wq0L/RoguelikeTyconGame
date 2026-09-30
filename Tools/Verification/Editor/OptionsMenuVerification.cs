using System;
using System.Collections;
using System.IO;
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

            SessionState.SetBool(Key, false);
            File.WriteAllText("Logs/OptionsMenuVerification.txt", "PASS " + options.GetComponentsInChildren<OptionRow>(true).Length + " rows");
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
