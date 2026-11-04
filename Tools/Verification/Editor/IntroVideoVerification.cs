using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

// Açılış videosu + tuş ikonları doğrulaması (4 Kasım 2026). Yalnız izole kopyada (kayıt bellekte kalır).
// Gerçek akış: MenuScene → "Oyna" → video → Space ile geçme (basılı tutma çubuğu) → seçim ekranı; ikinci "Oyna"da video yok;
// doğal bitiş (karardığında seçim ekranı, ses kuyruğundan sonra kapanış); kaynak sızıntısı yok. Görüntüler: Logs/IntroVideo/.
// Çalıştırma: StreamingAssets/Intro kopyaya elle konur, sonra
//   Tools/run-isolated-verification.ps1 -Method IntroVideoVerification.RunBatch -Full
[InitializeOnLoad]
public static class IntroVideoVerification
{
    const string Key = "IntroVideoVerification", Out = "Logs/IntroVideo";
    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;
    static bool revealed; static double revealedAt;

    static IntroVideoVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        // Sessiz: batch Unity videoyu kullanıcının hoparlöründen çalıyordu (4 Kasım, kullanıcı kendi Play testinde duydu).
        EditorUtility.audioMasterMute = true;
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            EditorUtility.audioMasterMute = true;
            AudioListener.volume = 0f;   // ayarlar değişince SoundManager geri yazar; her karede sıfırlanır
            if (nextAt == 0) { nextAt = now + 2; stepSince = now; Application.logMessageReceived += CountLogs; Plan(); return; }
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { Finish(null); return; }
            if (stepIndex != shownStep) { shownStep = stepIndex; stepSince = now; }
            else if (now - stepSince > 60) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now + .02; EditorApplication.QueuePlayerLoopUpdate(); return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static void Plan()
    {
        MainMenuUI menu = null; GameObject mainPanel = null; StartSelectionPanelUI start = null; IntroVideoPlayer player = null;
        int rtBefore = 0;
        steps.Enqueue(("menu", () =>
        {
            menu = Object.FindFirstObjectByType<MainMenuUI>();
            Require(menu != null, "MenuScene has MainMenuUI");
            Require(EditorUtility.audioMasterMute && AudioListener.volume == 0f, "test is silent (no sound on the user's speakers)");
            Require(Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length == 0 && !IntroVideoPlayer.IsPlaying,
                "nothing plays on the menu before Play is clicked");
            mainPanel = (GameObject)F(menu, "mainMenuPanel");
            start = (StartSelectionPanelUI)F(menu, "startPanel");
            bool seen = DialogueProgress.IntroSeen;   // kaydı yükler
            Require(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "real save untouched (memory only)");
            Require(!seen, "fresh save: intro not seen");
            Require(File.Exists(IntroVideoPlayer.FilePath), "video file in StreamingAssets: " + IntroVideoPlayer.FilePath);
            rtBefore = RuntimeTextures();
            ((Button)F(menu, "playButton")).onClick.Invoke();
            return 0;
        }));
        steps.Enqueue(("intro starts", () =>
        {
            player = Object.FindFirstObjectByType<IntroVideoPlayer>();
            Require(IntroVideoPlayer.IsPlaying && player != null, "first Play starts the intro");
            Require(!mainPanel.activeSelf && (start == null || !start.gameObject.activeSelf), "menu hidden, selection waits for the intro");
            Require(EventSystem.current == null || !EventSystem.current.sendNavigationEvents, "Space cannot submit a menu button under the video");
            ((Button)F(menu, "playButton")).onClick.Invoke();
            Require(Object.FindObjectsByType<IntroVideoPlayer>(FindObjectsSortMode.None).Length == 1, "second click during the video does nothing");
            return 0;
        }));
        steps.Enqueue(("prepared", () =>
        {
            if (!(bool)F(player, "prepared")) return double.NaN;
            Require(Video(player).isPlaying || Video(player).isPrepared, "video prepared and playing");
            return 0;
        }));
        steps.Enqueue(("3 s frame", () =>
        {
            if (Video(player).time < 3.0) return double.NaN;
            Require(Video(player).frame > 0 && ((RawImage)F(player, "screen")).enabled, $"video frames arrive (frame {Video(player).frame})");
            Require(((CanvasGroup)F(player, "hint")).alpha > .8f, "skip hint visible");
            Capture("01_Intro_3s");
            Require(!revealed && !DialogueProgress.IntroSeen, "nothing revealed yet");
            return 0;
        }));
        steps.Enqueue(("hold", () =>
        {
            SetF(player, "hold", .7f);
            return .03;
        }));
        steps.Enqueue(("hold capture", () =>
        {
            float p = player.SkipProgress;
            Require(p > .4f && p < .75f, $"hold bar fills ({p:0.00}) and drains when released");
            var fill = (RectTransform)F(player, "fill"); var track = (RectTransform)F(player, "track");
            Require(Mathf.Abs(fill.rect.width - (track.rect.width - 2f * IntroVideoPlayer.FillInset) * p) < 1f, "bar width follows the hold");
            Capture("02_Intro_hold");
            player.Skip();
            return .6;
        }));
        steps.Enqueue(("after skip", () =>
        {
            Require(DialogueProgress.IntroSeen, "skip marks intro seen");
            Require(start != null && start.gameObject.activeSelf, "selection screen opens after the skip");
            Require(!IntroVideoPlayer.IsPlaying && Object.FindObjectsByType<IntroVideoPlayer>(FindObjectsSortMode.None).Length == 0, "intro overlay destroyed");
            Require(EventSystem.current == null || EventSystem.current.sendNavigationEvents, "UI navigation restored");
            Require(RuntimeTextures() == rtBefore, "render texture released");
            Capture("03_Selection_after_skip");
            start.Close();
            mainPanel.SetActive(true);
            ((Button)F(menu, "playButton")).onClick.Invoke();
            return .2;
        }));
        steps.Enqueue(("second play", () =>
        {
            Require(!IntroVideoPlayer.IsPlaying && start.gameObject.activeSelf, "second Play goes straight to selection (no intro)");
            start.Close(); mainPanel.SetActive(true);
            revealed = false;
            Require(IntroVideoPlayer.Play(() => { revealed = true; revealedAt = EditorApplication.timeSinceStartup; }), "direct Play for natural end");
            player = Object.FindFirstObjectByType<IntroVideoPlayer>();
            return 0;
        }));
        steps.Enqueue(("seek", () =>
        {
            if (!(bool)F(player, "prepared") || Video(player).frame < 2) return double.NaN;
            Video(player).time = 13.6;
            return 0;
        }));
        steps.Enqueue(("natural reveal", () =>
        {
            if (!revealed) return double.NaN;
            Require(Video(player).time >= 14.7, $"reveal when the picture is dark ({Video(player).time:0.00} s)");
            Require(IntroVideoPlayer.IsPlaying, "sound tail continues over the selection screen");
            return 0;
        }));
        steps.Enqueue(("tail end", () =>
        {
            if (IntroVideoPlayer.IsPlaying) return double.NaN;
            double tail = EditorApplication.timeSinceStartup - revealedAt;
            Require(tail > 1.0 && tail < 3.0, $"overlay closes after the sound tail ({tail:0.00} s)");
            return .2;   // Destroy bir sonraki karede biter
        }));
        steps.Enqueue(("tail cleanup", () =>
        {
            Require(RuntimeTextures() == rtBefore && Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length == 0, "no texture or player left behind");
            return 0;
        }));
        steps.Enqueue(("repeat", () =>
        {
            // Sızıntı: art arda 5 aç/geç; nesne ve doku sayısı sabit kalmalı.
            for (int i = 0; i < 5; i++)
            {
                Require(IntroVideoPlayer.Play(null), "repeat play " + i);
                Object.FindFirstObjectByType<IntroVideoPlayer>().Skip();
                Object.DestroyImmediate(Object.FindFirstObjectByType<IntroVideoPlayer>().gameObject);
            }
            return .2;
        }));
        steps.Enqueue(("repeat cleanup", () =>
        {
            Require(RuntimeTextures() == rtBefore && !IntroVideoPlayer.IsPlaying && Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length == 0,
                "5 open/skip cycles leave nothing behind");
            return 0;
        }));
        steps.Enqueue(("key prompts", () =>
        {
            Require(KeyPrompts.Asset != null, "key prompt sprite asset builds from Resources");
            foreach (string k in new[] { KeyPrompts.Space, KeyPrompts.Esc, KeyPrompts.Tab, KeyPrompts.PageUp, KeyPrompts.PageDown, KeyPrompts.Alt })
                Require(KeyPrompts.Tag(k).StartsWith("<sprite"), "icon for " + k);
            KeyPromptSheet();
            return 0;
        }));
        steps.Enqueue(("errors", () => { Require(errors == 0, "no errors in the log"); return 0; }));
    }

    // Oyundaki yazılarla aynı boyutlar: önizleme ipucu 19, açılır pencere alt satırı 19, ödül yerleştirme ~28, geçme ipucu 28.
    static void KeyPromptSheet()
    {
        var root = FeelOverlay.CreateCanvas("Key Prompt Sheet", 31000);
        var bg = new GameObject("Paper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(root, false); FeelOverlay.Stretch(bg.rectTransform); bg.color = new Color32(255, 242, 210, 255);
        string[] lines =
        {
            "Tile'ın üstüne gel: detay  ·  " + KeyPrompts.Tag(KeyPrompts.Tab) + " / " + KeyPrompts.Tag(KeyPrompts.Esc) + ": geri dön",
            KeyPrompts.Tag(KeyPrompts.Alt) + ": aç / kapat  ·  " + KeyPrompts.Tag(KeyPrompts.PageUp) + " / " + KeyPrompts.Tag(KeyPrompts.PageDown) + ": kaydır",
            "ANTENİ YERLEŞTİR · tıkla: yerleştir · " + KeyPrompts.Tag(KeyPrompts.Esc) + ": vazgeç",
            KeyPrompts.Tag(KeyPrompts.Space) + "  basılı tut: geç",
        };
        float[] sizes = { 19, 19, 28, 19 };
        for (int i = 0; i < lines.Length; i++)
        {
            var t = new GameObject("Line " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(root, false);
            FeelOverlay.Theme?.StylePopupText(t); KeyPrompts.Apply(t);
            t.fontSize = sizes[i]; t.alignment = TextAlignmentOptions.Left; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f); t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(60f, -60f - i * 90f); t.rectTransform.sizeDelta = new Vector2(1700f, 70f);
            t.text = lines[i]; t.ForceMeshUpdate();
            Require(t.textInfo.characterInfo.Take(t.textInfo.characterCount).Count(c => c.elementType == TMP_TextElementType.Sprite) >= 1, $"line {i} renders key sprites");
        }
        Capture("04_KeyPrompts");
        Object.Destroy(root.gameObject);
    }

    static VideoPlayer Video(IntroVideoPlayer p) => (VideoPlayer)F(p, "video");
    static int RuntimeTextures() => Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t => t.name == "Intro Video (runtime)");
    static object F(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);
    static void SetF(object o, string name, object v) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Directory.CreateDirectory(Out);
        File.WriteAllLines(Out + "/result.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static RenderTexture target;
    static void Capture(string name)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas &&
                 (c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null))))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var old = cam.targetTexture; cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory(Out); File.WriteAllBytes($"{Out}/{name}.png", png.EncodeToPNG());
        Object.Destroy(png);
    }
}
