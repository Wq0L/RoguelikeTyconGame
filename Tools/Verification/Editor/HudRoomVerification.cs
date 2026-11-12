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

// Üst arayüz payı (12 Kasım 2026; kullanıcı: "round kartı / süre bazı şeylerin üstüne çıkıyor"). Gerçek GameScene, Mechanics Test
// Lab oturumu; FeelBootstrap elle kurulur (batch'te kendisi çalışmaz). Her açık alan boyunda (3, 5, 11) çerçeve oturduktan sonra
// tarlanın görünen alanının dört dış köşesi kameradan ekrana izdüşürülür: üst köşe round kartı + süre plakasının (ComicHud) alt
// kenarının altında kalmalı (HudGap payıyla), alt köşe ekranda kalmalı. Ayrıca pencere boyutu değişince pay yeniden hesaplanır.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method HudRoomVerification.RunBatch
[InitializeOnLoad]
public static class HudRoomVerification
{
    const string Key = "HudRoomVerification";
    static readonly List<string> lines = new();
    static int passed, failed, phase, sizeIndex;
    static double next, started;
    static float before;
    static GameObject roundUi;
    static readonly int[] Sizes = { 3, 5, 11 };
    static HudRoomVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (ok) passed++; else failed++; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Üst arayüz payı", "", "", "", 3);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 200) { Check(false, "timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            var feel = UnityEngine.Object.FindAnyObjectByType<GridUnlockFeel>();
            switch (phase)
            {
                case 0:
                {
                    // Oyundaki sıra: çerçeve, round arayüzü KAPALIYKEN kurulur (hazırlık ekranı); round başlayınca arayüz açılır.
                    GameSettings.GridCoordinates = true;
                    roundUi = UnityEngine.Object.FindAnyObjectByType<RoundUI>(FindObjectsInactive.Include).gameObject;
                    roundUi.SetActive(false);
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    var cameraFeel = Camera.main.GetComponent<CameraFeel>();
                    lines.Add($"INFO screen {Screen.width}×{Screen.height}, HUD installed {ComicHud.Installed}, camera feel {cameraFeel != null}, round UI hidden while framing is set up");
                    phase = 10; next = now + 2; return;
                }
                case 10:
                    roundUi.SetActive(true);   // round başladı: arayüz göründü
                    phase = 1; next = now + 2; return;
                case 1:
                {
                    if (feel == null || feel.Playing) { next = now + .5; return; }
                    int size = Sizes[sizeIndex];
                    Measure(size);
                    sizeIndex++;
                    if (sizeIndex < Sizes.Length)
                    {
                        Result g = TestField.SetGridSize(Sizes[sizeIndex]);
                        if (!g.Ok) { Check(false, "grid size: " + g.Message); Finish(); return; }
                        next = now + 4; return;   // efekt + geri çekilme
                    }
                    phase = 2; next = now + .5; return;
                }
                case 2:
                {
                    // Geniş pencere benzetimi: arayüz ölçeği büyür (CanvasScaler genişlik / yükseklik 0,5 eşlemesinde geniş ekran böyle
                    // olur). Toplu kipte ekran boyu değişmediği için ölçek elle büyütülür ve "pencere değişti" tetiklenir.
                    ComicHud.TryGetTopCenterBottom(out before);
                    var card = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Round Card");
                    var scaler = card.GetComponentInParent<Canvas>().rootCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                    float factor = card.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = factor * 1.5f;
                    Canvas.ForceUpdateCanvases();
                    typeof(GridUnlockFeel).GetField("layoutWidth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(feel, -1);
                    phase = 3; next = now + 1.5; return;
                }
                case 3:
                {
                    var f = typeof(GridUnlockFeel).GetField("layoutWidth", BindingFlags.Instance | BindingFlags.NonPublic);
                    Check((int)f.GetValue(feel) == Screen.width, "window / HUD change re-applies the framing (layout cache refreshed)");
                    ComicHud.TryGetTopCenterBottom(out float after);
                    Check(after > before + .03f, $"wide-window HUD is taller ({before:0.000} -> {after:0.000} of screen height)");
                    Measure(11);
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { Check(false, $"phase {phase} threw: {e}"); Finish(); }
    }

    static void Measure(int openSize)
    {
        Camera cam = Camera.main;
        var feel = CameraFeel.Instance;
        GridSystem system = GridManager.Instance.GetGridSystem();
        int width = GridManager.Instance.GetWidth();
        int framed = feel.FramedCells(openSize, width);
        float cell = (system.GetWorldPosition(1, 0) - system.GetWorldPosition(0, 0)).magnitude;
        Vector3 center = system.GetWorldPosition(width / 2, GridManager.Instance.GetHeight() / 2);
        float half = (framed / 2 + .5f) * cell;
        float top = float.MinValue, bottom = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = center + new Vector3((i & 1) == 0 ? -half : half, 0f, (i & 2) == 0 ? -half : half);
            Vector3 v = cam.WorldToViewportPoint(corner);
            top = Mathf.Max(top, v.y); bottom = Mathf.Min(bottom, v.y);
        }
        bool hud = ComicHud.TryGetTopCenterBottom(out float hudBottom);
        const float tolerance = .012f;   // kameranın nefes (idle) kayması
        float gap = (1f - top) - hudBottom;
        bool clamped = bottom <= GridUnlockFeel.HudBottomGap + tolerance;
        lines.Add($"INFO open {openSize} (framed {framed}): field top {1f - top:0.000} from screen top, HUD bottom {hudBottom:0.000}, field bottom {bottom:0.000} from screen bottom");
        Check(hud, $"open {openSize}: HUD bottom edge measured");
        Check(gap >= GridUnlockFeel.HudGap - tolerance || clamped, $"open {openSize}: field top corner stays below the round card / timer (gap {gap:0.000} of screen height{(clamped ? ", clamped by the bottom edge" : "")})");
        Check(bottom >= -tolerance, $"open {openSize}: field bottom corner stays on screen ({bottom:0.000})");
        // Koordinat rozetleri: ekrandaki gerçek çizimlerinden (formülden bağımsız)
        var labels = GridCoordinateLabels.Active;
        if (labels != null)
        {
            float labelTop = float.MinValue; int shown = 0;
            foreach (Renderer r in labels.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                Bounds b = r.bounds; shown++;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    labelTop = Mathf.Max(labelTop, cam.WorldToViewportPoint(c).y);
                }
            }
            float labelGap = (1f - labelTop) - hudBottom;
            lines.Add($"INFO open {openSize}: {shown} coordinate label parts, top {1f - labelTop:0.000} from screen top");
            Check(shown > 0 && (labelGap >= -.002f || clamped), $"open {openSize}: coordinate letters stay below the round card / timer (gap {labelGap:0.000}{(clamped ? ", clamped" : "")})");
        }
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/HudRoomVerification.txt", new[] { $"{(failed == 0 ? "PASS" : "FAIL")} {passed} passed, {failed} failed" }.Concat(lines));
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
