using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Üst arayüz / tarla görsel testi (12 Kasım 2026; kullanıcı: "play'de test et, her grid'de nasıl duruyor, 3×3'te, 5×5'te").
// Gerçek GameScene + Mechanics Test Lab oturumu; oyundaki sıra: round arayüzü KAPALIYKEN FeelBootstrap çerçeveyi kurar, sonra
// arayüz açılır. Koordinat rozetleri açık. Kamera ve bütün arayüz baştan sona çekim boyutundaki bir dokuya çizer (toplu kipte ekran
// 640×480: kamera / arayüz ölçeği gerçek pencereyle aynı olsun diye). Tarla 3 → 5 → 7 → 9 → 11 büyür; her boyda efekt bitince çekilir
// ve ölçülür (tarla köşesi, en üst rozet, arayüzün alt kenarı). Görüntüler: Logs/HudFraming/<oran>_<boy>.png.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method HudFramingCapture.Run16x9 (ya da RunWide: 2560×1080)
[InitializeOnLoad]
public static class HudFramingCapture
{
    const string Key = "HudFramingCapture", SizeKey = "HudFramingCapture.Size";
    static readonly int[] Sizes = { 3, 5, 7, 9, 11 };
    static readonly List<string> lines = new();
    static int phase, sizeIndex, width, height;
    static double next, started;
    static RenderTexture target;
    static GameObject roundUi;
    static HudFramingCapture() { EditorApplication.update += Tick; }

    public static void Run16x9() => Begin(1920, 1080);
    public static void RunWide() => Begin(2560, 1080);

    static void Begin(int w, int h)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true); SessionState.SetString(SizeKey, w + "x" + h);
        Scenario s = ScenarioPresets.New("Üst arayüz görsel testi", "", "", "", 3);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 4, 4);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 240) { lines.Add("FAIL timeout at phase " + phase); Finish(1); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            Camera cam = Camera.main;
            var feel = UnityEngine.Object.FindAnyObjectByType<GridUnlockFeel>();
            switch (phase)
            {
                case 0:
                {
                    string[] size = SessionState.GetString(SizeKey, "1920x1080").Split('x');
                    width = int.Parse(size[0]); height = int.Parse(size[1]);
                    GameSettings.GridCoordinates = true; GameSettings.BackgroundEffects = true;
                    target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; }
                    roundUi = UnityEngine.Object.FindAnyObjectByType<RoundUI>(FindObjectsInactive.Include).gameObject;
                    roundUi.SetActive(false);   // hazırlık ekranı gibi: çerçeve arayüz kapalıyken kurulur
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    var space = cam.GetComponent<SpaceBackground>(); if (space != null) { space.enabled = false; space.enabled = true; }
                    lines.Add($"INFO capture {width}×{height}, HUD installed {ComicHud.Installed}");
                    phase = 1; next = now + 2; return;
                }
                case 1:
                    roundUi.SetActive(true);    // round başladı
                    phase = 2; next = now + 2.5; return;
                case 2:
                {
                    if (feel == null || feel.Playing) { next = now + .5; return; }
                    int open = Sizes[sizeIndex];
                    Measure(cam, open);
                    Capture($"{width}x{height}_{open}x{open}.png");
                    sizeIndex++;
                    if (sizeIndex < Sizes.Length)
                    {
                        Result g = TestField.SetGridSize(Sizes[sizeIndex]);
                        if (!g.Ok) { lines.Add("FAIL grid size: " + g.Message); Finish(1); return; }
                        next = now + 4.5; return;
                    }
                    Finish(0); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(1); }
    }

    static void Measure(Camera cam, int open)
    {
        var feel = CameraFeel.Instance;
        GridSystem system = GridManager.Instance.GetGridSystem();
        int gridWidth = GridManager.Instance.GetWidth();
        int framed = feel.FramedCells(open, gridWidth);
        float cell = (system.GetWorldPosition(1, 0) - system.GetWorldPosition(0, 0)).magnitude;
        Vector3 center = system.GetWorldPosition(gridWidth / 2, GridManager.Instance.GetHeight() / 2);
        float half = (framed / 2 + .5f) * cell, top = float.MinValue, bottom = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 v = cam.WorldToViewportPoint(center + new Vector3((i & 1) == 0 ? -half : half, 0f, (i & 2) == 0 ? -half : half));
            top = Mathf.Max(top, v.y); bottom = Mathf.Min(bottom, v.y);
        }
        float labelTop = float.MinValue;
        var labels = GridCoordinateLabels.Active;
        if (labels != null)
            foreach (Renderer r in labels.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                    labelTop = Mathf.Max(labelTop, cam.WorldToViewportPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).y);
            }
        ComicHud.TryGetTopCenterBottom(out float hud);
        float fieldGap = (1f - top) - hud, labelGap = (1f - labelTop) - hud;
        bool ok = fieldGap > 0f && labelGap > 0f && bottom > 0f;
        lines.Add($"{(ok ? "PASS" : "FAIL")} {width}×{height} open {open} (framed {framed}): HUD bottom {hud:0.000}, field top {1f - top:0.000} (gap {fieldGap:+0.000;-0.000}), letters top {1f - labelTop:0.000} (gap {labelGap:+0.000;-0.000}), field bottom {bottom:0.000}");
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/HudFraming");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/HudFraming/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/HudFramingCapture.txt", new[] { lines.Any(l => l.StartsWith("FAIL")) ? "FAIL" : "DONE" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(code);
    }
}
