using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Boyalı gezegen denemesi (12 Kasım 2026; kullanıcı: "gezegen sırıtmış, yandaki gezegen de bu işe uygun olsa"). Yalnız izole kopyada.
// Prototip shader kopyada: Assets/HadesPrototype/SpaceBackgroundPainted.shader ("Hidden/SpaceBackgroundPainted"; ana projedeki
// SpaceBackground.shader'ın gezegen bölümü değiştirilmiş kopyası). Uzay dörtgeninin malzemesinde shader aynı karede değiştirilir
// (özellikler aynı adlı, korunur); önce / sonra: Logs/PlanetStyle/<menu|game>_once.png, _sonra.png.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method PlanetStyleCapture.RunMenuBatch (ya da RunGameBatch)
[InitializeOnLoad]
public static class PlanetStyleCapture
{
    const string Key = "PlanetStyleCapture", ModeKey = "PlanetStyleCapture.Mode";
    static double next, started;
    static int phase;
    static Material spaceMaterial;
    static readonly List<string> notes = new();
    static PlanetStyleCapture() { EditorApplication.update += Tick; }

    static void Prepare()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (Shader.Find("Hidden/SpaceBackgroundPainted") == null) throw new Exception("prototype shader missing (Assets/HadesPrototype/SpaceBackgroundPainted.shader)");
    }

    public static void RunMenuBatch()
    {
        Prepare();
        EditorSceneManager.OpenScene(MenuDioramaBuilder.MenuScenePath);
        SessionState.SetBool(Key, true); SessionState.SetString(ModeKey, "menu");
        EditorApplication.EnterPlaymode();
    }

    public static void RunGameBatch()
    {
        Prepare();
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true); SessionState.SetString(ModeKey, "game");
        Scenario s = ScenarioPresets.New("Gezegen çekimi", "", "", "", 5);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(2, 2), 3, 3);
        foreach (var (x, z) in new[] { (5, 3), (6, 4), (5, 5), (7, 6) }) ScenarioPresets.Planter(s, ScenarioPresets.Small(), x, z);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        string mode = SessionState.GetString(ModeKey, "menu");
        if (started == 0) { started = now; next = now + (mode == "game" ? 10 : 3); }
        if (now - started > 120) { Finish(new Exception("timeout at phase " + phase)); return; }
        if (now < next) return;
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    GameSettings.BackgroundEffects = true;
                    var space = cam.GetComponent<SpaceBackground>();
                    if (space == null) space = cam.gameObject.AddComponent<SpaceBackground>();
                    space.enabled = false; space.enabled = true;
                    var feel = cam.GetComponent<CameraFeel>(); if (feel != null) feel.enabled = false;
                    cam.aspect = 1920f / 1080f;
                    Time.timeScale = 0f;
                    break;
                }
                case 1:
                {
                    var quad = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Space Background");
                    if (quad == null) throw new Exception("Space Background quad not found");
                    spaceMaterial = quad.sharedMaterial;
                    notes.Add($"INFO space material shader '{spaceMaterial.shader.name}'");
                    Capture($"{mode}_once.png", cam);
                    spaceMaterial.shader = Shader.Find("Hidden/SpaceBackgroundPainted");
                    break;
                }
                case 2: Capture($"{mode}_sonra.png", cam); break;
                default: Finish(null); return;
            }
            phase++;
            next = now + .6;
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/PlanetStyleCapture.txt", new[] { e == null ? "DONE" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name, Camera camera)
    {
        Directory.CreateDirectory("Logs/PlanetStyle");
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .05f; }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/PlanetStyle/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
