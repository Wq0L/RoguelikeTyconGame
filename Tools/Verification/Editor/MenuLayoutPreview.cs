using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Ana menü yerleşim seçenekleri (11 Kasım 2026, kullanıcı: "menü dümenden duruyor, güzelleştirelim, atıyorum bir tarafa
// yaslayabiliriz"). Yalnız izole kopyada, yalnız görüntü için: MenuScene oynatılır, butonlar her seçenekte geçici olarak
// yeniden dizilir, geçici başlık / pano eklenir, çekilir, silinir. Oyunun kendisine ve sahneye dokunmaz.
// Görüntüler: Logs/MenuPreview/m0_simdiki.png, m1_sol_pano.png, m2_orta_baslik.png, m3_sol_panosuz.png.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method MenuLayoutPreview.RunBatch -Full
[InitializeOnLoad]
public static class MenuLayoutPreview
{
    const string Key = "MenuLayoutPreview";
    static double next, started;
    static int phase;
    static readonly List<GameObject> temp = new();
    static readonly List<string> notes = new();
    static MenuLayoutPreview() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static Button Field(MainMenuUI menu, string name) =>
        (Button)typeof(MainMenuUI).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(menu);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2.5; }
        if (now - started > 90) { Finish(new Exception("timeout at phase " + phase)); return; }
        if (now < next) return;
        try
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
            if (menu == null) throw new Exception("MainMenuUI not found");
            Button play = Field(menu, "playButton"), options = Field(menu, "optionsButton"), quit = Field(menu, "quitButton");
            var parent = (RectTransform)play.transform.parent;
            Clear();
            switch (phase)
            {
                case 0:
                    notes.Add($"INFO buttons parent '{parent.name}', canvas size {((RectTransform)play.GetComponentInParent<Canvas>().rootCanvas.transform).rect.size}");
                    Capture("m0_simdiki.png");
                    break;
                case 1:
                {
                    // Sol pano: lacivert pencere, içinde başlık ve aynı hizada üç buton; resmin sağı açık kalır.
                    const float x = -590f;
                    Image board = ComicKit.Sliced(parent, "Preview Board", "window_navy", 1.15f);
                    temp.Add(board.gameObject);
                    Center(board.rectTransform, new Vector2(x, 20f), new Vector2(600f, 760f));
                    board.rectTransform.SetSiblingIndex(play.transform.GetSiblingIndex());
                    Title(parent, new Vector2(x, 262f), new Vector2(500f, 130f), 92f, new Color32(255, 242, 210, 255), 0f);
                    Place(play, new Vector2(x, 100f), new Vector2(480f, 120f));
                    Place(options, new Vector2(x, -40f), new Vector2(480f, 100f));
                    Place(quit, new Vector2(x, -165f), new Vector2(480f, 100f));
                    Capture("m1_sol_pano.png");
                    break;
                }
                case 2:
                {
                    // Orta, başlıklı: başlık krem dairede, butonlar altında tek sütun, eşit genişlikte.
                    Title(parent, new Vector2(0f, 345f), new Vector2(1100f, 190f), 168f, new Color32(255, 206, 84, 255), -3f);
                    Place(play, new Vector2(0f, 140f), new Vector2(540f, 124f));
                    Place(options, new Vector2(0f, 0f), new Vector2(460f, 100f));
                    Place(quit, new Vector2(0f, -122f), new Vector2(460f, 100f));
                    Capture("m2_orta_baslik.png");
                    break;
                }
                case 3:
                {
                    // Sol yaslı, panosuz: başlık ve butonlar sol üst çeyrekte, sola hizalı, hafif basamaklı.
                    Title(parent, new Vector2(-870f, 360f), new Vector2(900f, 170f), 140f, new Color32(255, 206, 84, 255), 3f, true);
                    PlaceLeft(play, new Vector2(-870f, 150f), new Vector2(540f, 124f));
                    PlaceLeft(options, new Vector2(-870f, 10f), new Vector2(460f, 100f));
                    PlaceLeft(quit, new Vector2(-870f, -115f), new Vector2(460f, 100f));
                    Capture("m3_sol_panosuz.png");
                    break;
                }
                default:
                    Finish(null);
                    return;
            }
            phase++;
            next = now + .6;
        }
        catch (Exception e) { Finish(e); }
    }

    static void Clear()
    {
        foreach (GameObject go in temp) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        temp.Clear();
    }

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static void Place(Button button, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)button.transform;
        Center(rect, position, size);
        Fit(button, size);
    }

    static void PlaceLeft(Button button, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Fit(button, size);
    }

    static void Fit(Button button, Vector2 size)
    {
        if (button.targetGraphic is Image image && image.sprite != null)
            image.pixelsPerUnitMultiplier = ComicKit.Multiplier(image.sprite, image.rectTransform, 2f);
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) label.fontSizeMax = size.y * .5f;
    }

    static void Title(RectTransform parent, Vector2 position, Vector2 size, float fontSize, Color colour, float tilt, bool left = false)
    {
        var go = new GameObject("Preview Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        temp.Add(go);
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(left ? 0f : .5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        ComicKit.Display(text, true);
        text.text = "ERIGONE";
        text.color = colour;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        text.characterSpacing = 4f;
        text.raycastTarget = false;
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/MenuLayoutPreview.txt", new[] { e == null ? "DONE" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/MenuPreview");
        Camera camera = Camera.main;
        if (camera == null) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            // Menü sahnesinde kamera yok (yalnız overlay canvas): çekim için geçici kamera (ComicUIVerification ile aynı).
            camera = new GameObject("Preview capture camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .09f);
            camera.GetUniversalAdditionalCameraData().SetRenderer(1);
        }
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).OrderBy(c => c.sortingOrder).ToArray();
        int[] orders = new int[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera;
            c.planeDistance = camera.nearClipPlane + .02f + (canvases.Length - i) * .01f;
            orders[i] = c.sortingOrder; c.sortingOrder = 30000 + i;
        }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/MenuPreview/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].sortingOrder = orders[i]; }
    }
}
