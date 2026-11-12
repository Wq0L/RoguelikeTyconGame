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

// Ana menü arka planı için oyunun kendi sahnesinden çekim (11 Kasım 2026, kullanıcı: "tekrar Higgsfield ile dene, olmazsa
// Unity içinde canlı sahne"). Gerçek GameScene, Mechanics Test Lab oturumu (3 × 3 saksı, gerçek bitkiler; kayıt yalnız
// bellekte). Arayüz ve koordinat etiketleri gizlenir, kamera menü kadrajlarına alınır, çekilir. Oyuna ve sahneye dokunmaz.
// Görüntüler: Logs/MenuScene/*.png. Çalıştırma: Tools/run-isolated-verification.ps1 -Method MenuSceneCapture.RunBatch -Full
[InitializeOnLoad]
public static class MenuSceneCapture
{
    const string Key = "MenuSceneCapture";
    static double next, started;
    static int phase;
    static Vector3 basePos, center; static float baseSize, tile;
    static readonly List<string> notes = new();
    static MenuSceneCapture() { EditorApplication.update += Tick; }

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

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 14; }   // bitkiler biraz büyüsün
        if (now - started > 120) { Finish(new Exception("timeout at phase " + phase)); return; }
        if (now < next) return;
        try
        {
            Camera cam = Camera.main;
            if (phase == 0)
            {
                // Yalnız ekran üstü arayüz (HUD, paneller) gizlenir; uzay arka planı ve gezegen sahnede kalır.
                foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    notes.Add($"INFO canvas '{c.name}' mode={c.renderMode} order={c.sortingOrder} root={c.isRootCanvas}");
                    if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    if (c.name != "Canvas") { c.enabled = false; continue; }
                    // Ana arayüz katmanı: uzay arka planı ve gezegen bunun içinde; yalnız onlar kalır.
                    foreach (Transform child in c.transform)
                    {
                        string n = child.name.ToLowerInvariant();
                        bool keep = n.Contains("back") || n.Contains("space") || n.Contains("planet") || n.Contains("uzay") || n.Contains("arka") || n.Contains("bg") || n.Contains("star") || n.Contains("nebula");
                        notes.Add($"INFO canvas child '{child.name}' active={child.gameObject.activeSelf} keep={keep}");
                        if (!keep) child.gameObject.SetActive(false);
                    }
                }
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    notes.Add($"INFO root '{root.name}' active={root.activeSelf}");
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("cursor") || n.Contains("imleç") || n.Contains("imlec") || n.Contains("reticle") || n.Contains("range indicator"))
                    { notes.Add($"INFO hid '{t.name}'"); t.gameObject.SetActive(false); }
                }
                foreach (var labels in UnityEngine.Object.FindObjectsByType<GridCoordinateLabels>(FindObjectsSortMode.None)) labels.gameObject.SetActive(false);
                Time.timeScale = 0f;   // bitkiler ve efektler çekim boyunca yerinde kalsın
                basePos = cam.transform.position; baseSize = cam.orthographicSize;
                notes.Add($"INFO camera ortho={cam.orthographic} size={baseSize} pos={basePos} rot={cam.transform.eulerAngles} far={cam.farClipPlane}");
                // Oyunun uzay arka planı (SpaceBackground): ayar kapalıysa yalnız bellekte açılır ve bileşen yeniden kurulur.
                var space = cam.GetComponent<SpaceBackground>();
                notes.Add($"INFO SpaceBackground={(space != null)} BackgroundEffects={GameSettings.BackgroundEffects}");
                GameSettings.BackgroundEffects = true;
                // Kamera sarsıntısı kapatılır (Instance boşalır: arka plan dörtgeni kameranın kendi dönüşünü izler).
                var feel = cam.GetComponent<CameraFeel>(); if (feel != null) feel.enabled = false;
                cam.aspect = 1920f / 1080f;   // çekim 16:9 (toplu kipte ekran 4:3: arka plan dörtgeni dar kalıyordu)
                if (space == null) space = cam.gameObject.AddComponent<SpaceBackground>();
                space.enabled = false; space.enabled = true;
                // Ada: saksıların ortası ve karo boyu; açık 5 × 5 ve bir halka dışındaki her şey gizlenir.
                var planters = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.StartsWith("Grass Planter")).Select(g => g.transform.position).ToList();
                center = planters.Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, planters.Count);
                tile = 99f;
                for (int i = 0; i < planters.Count; i++)
                    for (int j = i + 1; j < planters.Count; j++)
                    { float d = Vector3.Distance(planters[i], planters[j]); if (d > .01f) tile = Mathf.Min(tile, d); }
                int hidden = 0;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r.transform.IsChildOf(cam.transform) || r.name == "Space Background") continue;
                    Vector3 p = r.bounds.center - center;
                    if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) / tile > 3.6f && r.bounds.size.x < tile * 3f) { r.enabled = false; hidden++; }
                }
                notes.Add($"INFO island center={center} tile={tile:0.00} planters={planters.Count} hidden renderers={hidden}");
            }
            else if (phase == 1) Capture("s0_oyun_kadraji.png");
            else if (phase <= 9)
            {
                // Perspektif kahraman çekimleri: (yükselim°, yön°, uzaklık × karo, görüş açısı, ada ekranda x 0..1, ad).
                // Tek adımda kamera kurulur, sonraki adımda çekilir (arka plan dörtgeni bir karede yerine otursun).
                int k = (phase - 2) / 2;
                (float elev, float azim, float dist, float fov, float x, string name)[] shots =
                {
                    (24f, 45f, 15f, 26f, .66f, "p1_sag_alcak.png"),
                    (32f, 35f, 16f, 26f, .66f, "p2_sag_yuksek.png"),
                    (19f, 55f, 12.5f, 28f, .67f, "p3_sag_cok_alcak.png"),
                    (26f, 45f, 13.5f, 26f, .64f, "p4_sag_yakin.png"),
                };
                var s = shots[k];
                if (phase % 2 == 0)
                {
                    cam.orthographic = false; cam.fieldOfView = s.fov;
                    Vector3 dir = Quaternion.Euler(s.elev, s.azim, 0f) * Vector3.forward;
                    Vector3 target = center + Vector3.up * (tile * .4f);
                    float dist = s.dist * tile;
                    cam.transform.rotation = Quaternion.LookRotation(dir);
                    float halfW = dist * Mathf.Tan(s.fov * .5f * Mathf.Deg2Rad) * cam.aspect;
                    cam.transform.position = target - dir * dist - cam.transform.right * ((s.x - .5f) * 2f * halfW);
                }
                else
                {
                    // İmleç ve benzeri sahne betikleri gizlenen parçaları geri açabiliyor: çekimden hemen önce yeniden gizle.
                    foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (r.transform.IsChildOf(cam.transform) || r.name == "Space Background") continue;
                        Vector3 p = r.bounds.center - center;
                        string n = r.name.ToLowerInvariant();
                        if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) / tile > 3.6f || n.Contains("cursor") || n.Contains("harvest") || n.Contains("scythe") || n.Contains("trail")) r.enabled = false;
                    }
                    Capture(s.name);
                }
            }
            else { Finish(null); return; }
            phase++;
            next = now + .5;
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/MenuSceneCapture.txt", new[] { e == null ? "DONE" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/MenuScene");
        Camera camera = Camera.main;
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target;
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/MenuScene/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
    }
}
