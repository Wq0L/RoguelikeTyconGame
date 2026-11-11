using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Grid çerçevesi, açılma efekti ve hazırlık ekranı karartması (6 Kasım 2026). Gerçek GameScene, Mechanics Test Lab oturumu (kayıt
// yalnız bellekte). Batch'te FeelBootstrap çalışmadığı için oyundaki gibi elle kurulur. Sınananlar:
// - 3×3 açık: kamera 5×5'i çerçeveler (ölçek 5/11), dışındaki kilitli hücreler çizilmez, rozetler yalnız görünen 5 sütun / satırda.
// - Hazırlık ekranı: tam ekran karartma ana tuvalin ilk çocuğu (yalnız dünya kararır); yükseltme alınınca yeni hücreler efekt oynayana kadar gizli kalır.
// - Round başında efekt oynar: kamera 7×7'ye geri çekilir, yeni halkalar yükselir; bitince zemin görselleri tam ilk değerinde.
// - 11×11: bütün grid görünür, ölçek 1.
// Görüntüler: Logs/Framing/*.png. Çalıştırma: Tools/run-isolated-verification.ps1 -Method GridFramingVerification.RunBatch -Full
[InitializeOnLoad]
public static class GridFramingVerification
{
    const string Key = "GridFramingVerification";
    static double next, started;
    static int phase;
    static readonly List<string> notes = new();
    static readonly Dictionary<Transform, (Vector3 position, Vector3 scale)> floors = new();
    static GridFramingVerification() { EditorApplication.update += Tick; }

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
        s.gridSize = 3;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static GridManager Grid => GridManager.Instance;
    static int Width => Grid.GetWidth();
    static int Ring(int x, int z) => Mathf.Max(Mathf.Abs(x - Width / 2), Mathf.Abs(z - Grid.GetHeight() / 2));
    static GroundCell Ground(int x, int z) => Grid.GetGridSystem().GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
    static IEnumerable<(int x, int z)> Cells() { for (int x = 0; x < Width; x++) for (int z = 0; z < Grid.GetHeight(); z++) yield return (x, z); }
    static int HiddenCount() => Cells().Count(c => Ground(c.x, c.z).GroundRenderer.forceRenderingOff);
    static bool HiddenExactly(Func<int, bool> hiddenRing) => Cells().All(c => Ground(c.x, c.z).GroundRenderer.forceRenderingOff == hiddenRing(Ring(c.x, c.z)));
    static float Frame => CameraFeel.Instance.FrameScale;
    static int LabelsShown() => UnityEngine.Object.FindAnyObjectByType<GridCoordinateLabels>().GetComponentsInChildren<Transform>(false).Count(t => t.name.StartsWith("Coordinate "));

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
            var feel = UnityEngine.Object.FindAnyObjectByType<GridUnlockFeel>();
            switch (phase)
            {
                case 0:
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    GameSettings.GridCoordinates = true;
                    foreach (var c in Cells()) { Transform f = Ground(c.x, c.z).GroundRenderer.transform; floors[f] = (f.localPosition, f.localScale); }
                    phase = 1; next = now + 2; return;
                case 1:
                    Check(GridUnlockManager.Instance.CurrentSize == 3 && feel != null && feel.ShownSize == 3 && feel.FramedSize == 5,
                        $"3×3 open: the effect shows 3, frames 5 (open {GridUnlockManager.Instance.CurrentSize}, framed {feel?.FramedSize})");
                    Check(Mathf.Abs(Frame - 5f / 11f) < .002f, $"camera frames 5 of 11 cells (scale {Frame:0.###})");
                    Check(HiddenExactly(r => r > 2) && HiddenCount() == 121 - 25, $"only the 5×5 island is drawn ({121 - HiddenCount()} cells drawn)");
                    Check(LabelsShown() == 10, $"coordinate labels only for the 5 framed columns and rows ({LabelsShown()})");
                    Capture("f1_round_open3.png");
                    Check(RoundManager.Instance.EditorEndRound(false), "round ends through the game's path");
                    phase = 2; next = now + .3; return;
                case 2:
                    if (GameManager.Instance.CurrentState == GameStates.CardSelection || GameManager.Instance.CurrentState == GameStates.RoundChoice) GameManager.Instance.ShowRoundEnd();
                    if (GameManager.Instance.CurrentState != GameStates.RoundEnd) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                    phase = 3; next = now + 2.5; return;
                case 3:
                {
                    var map = UnityEngine.Object.FindFirstObjectByType<RoundMapUI>();
                    Image backdrop = map != null ? map.Backdrop : null;
                    var rect = backdrop != null ? backdrop.rectTransform : null;
                    Check(backdrop != null && backdrop.isActiveAndEnabled && rect.parent == map.GetComponentInParent<Canvas>().rootCanvas.transform && rect.GetSiblingIndex() == 0 &&
                          rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && backdrop.color == RoundMapUI.BackdropColor && !backdrop.raycastTarget,
                        "preparation screen: full-screen dark backdrop is the root canvas's first child (world dims, HUD and panels stay bright), takes no clicks");
                    Check(!RoundMapUI.Visible || LabelsShown() == 0, "world coordinate labels hidden while the map is open");
                    Capture("f2_prep_backdrop.png");
                    GridUnlockManager.Instance.UnlockNextTier(5);
                    Check(feel.HasPending && feel.ShownSize == 3 && HiddenExactly(r => r == 2 || r > 2) && Mathf.Abs(Frame - 5f / 11f) < .002f,
                        "grid bought during preparation: the new ring is unlocked but hidden; camera waits for the field");
                    Check(Cells().Where(c => Ring(c.x, c.z) <= 2).All(c => !Ground(c.x, c.z).IsLocked), "gameplay: the 5×5 area is unlocked at once");
                    phase = 4; next = now + .5; return;
                }
                case 4:
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().NextRound();
                    phase = 5; next = now + .05; return;
                case 5:
                    if (GameManager.Instance.CurrentState != GameStates.Round) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                    phase = 6; next = now + .35; return;
                case 6:
                    Check(feel.Playing && !feel.HasPending && Frame > 5f / 11f + .01f && Frame < 7f / 11f, $"round start: the effect plays, camera pulls back (scale {Frame:0.###})");
                    Capture("f4_grow_mid.png");
                    phase = 7; next = now + 2.2; return;
                case 7:
                    Check(!feel.Playing && feel.ShownSize == 5 && feel.FramedSize == 7 && Mathf.Abs(Frame - 7f / 11f) < .002f, $"after the effect: 5 open, 7 framed (scale {Frame:0.###})");
                    Check(HiddenExactly(r => r > 3), $"7×7 island drawn ({121 - HiddenCount()} cells)");
                    Check(floors.All(kv => kv.Key.localPosition == kv.Value.position && kv.Key.localScale == kv.Value.scale), "every floor visual is back at its exact original pose");
                    Check(LabelsShown() == 14, $"labels for the 7 framed columns and rows ({LabelsShown()})");
                    Capture("f5_round_open5.png");
                    GridUnlockManager.Instance.UnlockNextTier(11);
                    phase = 8; next = now + 2.5; return;
                case 8:
                    Check(!feel.Playing && feel.ShownSize == 11 && Mathf.Abs(Frame - 1f) < .001f && HiddenCount() == 0, "11×11: whole grid drawn, camera back at the scene framing");
                    Check(floors.All(kv => kv.Key.localPosition == kv.Value.position && kv.Key.localScale == kv.Value.scale), "floors exact after the second effect");
                    Check(LabelsShown() == 22, $"all 22 labels ({LabelsShown()})");
                    Capture("f6_round_open11.png");
                    Finish(null);
                    return;
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/GridFramingVerification.txt", new[] { e == null ? "PASS " + notes.Count + " checks" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/Framing");
        Camera camera = Camera.main;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera;
            c.planeDistance = camera.nearClipPlane + .02f + (canvases.Length - i) * .01f;
        }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/Framing/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
