using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Hazırlık (round sonu, grid haritalı) ekranından ayarlar (22 Kasım 2026; kullanıcı: "round bitince önümüze gelen ekrana, grid map
// UI'ın olduğu ekrana bir ufak ayarlar butonu koymalıyız ki oradan da ayarlara erişebilsin").
// Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080 çekim (kamera ve arayüz aynı dokuya; tuvaller çekimde kamera kipine
// alınır, sıralamaları dünyanın üstüne). Akış: round biter (level yok) → hazırlık ekranı → çekim → (kuruluysa) ayarlar butonuna
// basılır → panel açılır, dil turu (tr / en / qps) sığma → GERİ ile kapanır, hazırlık ekranı yerinde → Esc ile de kapanır.
// Görüntüler: Logs/RoundEndSettings/*.png, sonuç Logs/RoundEndSettingsVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method RoundEndSettingsVerification.RunBatch -Full
[InitializeOnLoad]
public static class RoundEndSettingsVerification
{
    const string Key = "RoundEndSettingsVerification", InstallKey = "RoundEndSettingsVerification.Install";
    const int W = 1920, H = 1080;
    static readonly string[] Languages = { "tr", "en", GameText.PseudoLanguage };
    static readonly List<string> lines = new();
    static int phase, languageIndex;
    static double next, started;
    static RenderTexture target;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static RoundEndSettingsVerification() { EditorApplication.update += Tick; }

    // Yalnız bugünkü hâli çeker (kurulum yok).
    public static void RunCaptureOnly() => Begin(false);
    // Oyun içi ayarları kurucuyla kurar ve sınar.
    public static void RunBatch() => Begin(true);

    static void Begin(bool install)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        if (install)
        {
            var method = Type.GetType("OptionsMenuBuilder, Assembly-CSharp-Editor")?.GetMethod("BuildGameScenes", BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new Exception("OptionsMenuBuilder.BuildGameScenes yok");
            string result = (string)method.Invoke(null, null);
            if (result != null && result.Contains("HATA")) throw new Exception("builder: " + result);
        }
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(InstallKey, install);
        Scenario s = ScenarioPresets.New("Hazırlıkta ayarlar", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static UIManager UI => UnityEngine.Object.FindAnyObjectByType<UIManager>();
    static GameObject RoundEnd => (GameObject)typeof(UIManager).GetField("roundEndUI", Any).GetValue(UI);
    static OptionsUI Options => UnityEngine.Object.FindAnyObjectByType<OptionsUI>(FindObjectsInactive.Include);
    static Button SettingsButton => RoundEnd.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Settings Button");
    static Button SoundButton => RoundEnd.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Sound Button");
    static SoundToggleButton Toggle => SoundButton.GetComponent<SoundToggleButton>();
    static Image SoundIcon => SoundButton.transform.Find("Icon").GetComponent<Image>();
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Any).Invoke(target, args);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 200) { lines.Add("FAIL timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            Camera cam = Camera.main;
            bool installed = SessionState.GetBool(InstallKey, false);
            switch (phase)
            {
                case 0:
                {
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Call(RM, "EndRound");
                    phase = 1; next = now + 2.5; return;
                }
                case 1:
                {
                    Check(GameManager.Instance.CurrentState == GameStates.RoundEnd && RoundEnd.activeInHierarchy, "hazırlık ekranı açık", GameManager.Instance.CurrentState.ToString());
                    Capture("1_hazirlik.png");
                    if (!installed) { Finish(); return; }
                    Button settings = SettingsButton;
                    Check(settings != null && settings.gameObject.activeInHierarchy, "hazırlık ekranında ayarlar butonu var");
                    var rect = (RectTransform)settings.transform;
                    Check(rect.rect.height >= 55.5f && settings.transition == Selectable.Transition.SpriteSwap, "ayarlar butonu kit karesi (56 px, kullanıcının seçimi)", $"{rect.rect.width:0}×{rect.rect.height:0}, {settings.transition}");
                    // Ses karesi: ayarların hemen sağında, aynı boyda, aynı hizada; başta açık (lacivert + krem hoparlör).
                    Button sound = SoundButton;
                    var soundRect = sound != null ? ScreenRect((RectTransform)sound.transform, cam) : default;
                    var settingsRect = ScreenRect(rect, cam);
                    Check(sound != null && Mathf.Abs(soundRect.height - settingsRect.height) < 1f && Mathf.Abs(soundRect.yMax - settingsRect.yMax) < 1f &&
                          soundRect.xMin > settingsRect.xMax && soundRect.xMin - settingsRect.xMax < 16f, "ses karesi ayarların yanında, aynı boy ve hizada",
                          sound != null ? $"aralık {soundRect.xMin - settingsRect.xMax:0.#} px" : "yok");
                    Check(!GameSettings.SoundMuted && Toggle.ShowsOn && SoundIcon.sprite.name == "icon_sound" && ((Image)sound.targetGraphic).sprite.name == "slot_button_navy_normal",
                        "ses açık: lacivert kare + krem hoparlör", $"{((Image)sound.targetGraphic).sprite.name}, {SoundIcon.sprite.name}");
                    sound.onClick.Invoke();
                    Check(GameSettings.SoundMuted && AudioListener.volume == 0f && !Toggle.ShowsOn && SoundIcon.sprite.name == "icon_sound_off" &&
                          ((Image)sound.targetGraphic).sprite.name == "slot_button_grey_normal" && sound.spriteState.highlightedSprite.name == "slot_button_grey_hover",
                        "tıklayınca bütün sesler kapandı: gri kare + gri hoparlör + kırmızı çizgi", $"ses {AudioListener.volume}, {((Image)sound.targetGraphic).sprite.name}, {SoundIcon.sprite.name}");
                    Capture("1b_ses_kapali.png");
                    sound.onClick.Invoke();
                    Check(!GameSettings.SoundMuted && Mathf.Approximately(AudioListener.volume, GameSettings.MasterVolume) && Toggle.ShowsOn,
                        "yeniden tıklayınca ses eski düzeyiyle açıldı", $"ses {AudioListener.volume}");
                    sound.onClick.Invoke();   // ayarlar penceresinde Tüm Sesler satırını sınamak için kapalı bırak
                    // Başka bir hazırlık butonu ya da kartıyla çakışmıyor.
                    var mine = ScreenRect(rect, cam);
                    var hits = RoundEnd.GetComponentsInChildren<Button>(false).Where(b => b != settings && ScreenRect((RectTransform)b.transform, cam).Overlaps(mine)).Select(b => b.name).ToList();
                    Check(hits.Count == 0, "ayarlar butonu başka bir butona binmiyor", string.Join(", ", hits));
                    Check(Options != null && !Options.gameObject.activeInHierarchy, "ayarlar paneli başta kapalı");
                    settings.onClick.Invoke();
                    phase = 2; next = now + .8; return;
                }
                case 2:
                {
                    OptionsUI options = Options;
                    Check(options.gameObject.activeInHierarchy, "butona basınca ayarlar açıldı");
                    Check(options.transform.GetSiblingIndex() == options.transform.parent.childCount - 1, "ayarlar paneli en üstte (hazırlık ekranının önünde)");
                    Check(options.GetComponentsInChildren<ComicButtonVisual>(true).Length == 0, "ayarlar kit görünümünde (eski palet butonu yok)");
                    // Ses > Tüm Sesler aynı ayarı gösterir (kapalı) ve oradan açınca hazırlık ekranındaki kare de açılır.
                    // Oyuncu gibi önce Ses sekmesi (kapalı sekmedeki satır henüz kurulmamıştır), sonra satır.
                    var tabs = (System.Collections.IList)typeof(OptionsUI).GetField("tabs", Any).GetValue(options);
                    ((Button)tabs[1].GetType().GetField("button").GetValue(tabs[1])).onClick.Invoke();
                    var soundRow = (OptionToggle)typeof(OptionsUI).GetField("sound", Any).GetValue(options);
                    Check(soundRow != null && !soundRow.IsOn, "Ses > Tüm Sesler kapalı gösteriyor (ses karesiyle aynı ayar)");
                    ((Button)typeof(OptionToggle).GetField("button", Any).GetValue(soundRow)).onClick.Invoke();
                    Check(!GameSettings.SoundMuted && Toggle.ShowsOn && SoundIcon.sprite.name == "icon_sound", "pencereden açınca ses karesi de açık hâle döndü");
                    phase = 3; next = now + .2; return;
                }
                case 3:
                    GameText.Language = Languages[languageIndex];
                    phase = 4; next = now + .8; return;
                case 4:
                {
                    string language = Languages[languageIndex];
                    Fit(Options.transform, $"[{language}] ayarlar yazıları kutularına sığıyor");
                    Capture($"2_ayarlar_{language}.png");
                    languageIndex++;
                    if (languageIndex < Languages.Length) { phase = 3; next = now + .2; return; }
                    GameText.Language = "tr";
                    phase = 5; next = now + .5; return;
                }
                case 5:
                {
                    Button back = Options.GetComponentsInChildren<Button>(true).First(b => b.name == "Back Button");
                    back.onClick.Invoke();
                    phase = 6; next = now + .6; return;
                }
                case 6:
                {
                    Check(!Options.gameObject.activeInHierarchy && RoundEnd.activeInHierarchy && GameManager.Instance.CurrentState == GameStates.RoundEnd,
                        "GERİ: ayarlar kapandı, hazırlık ekranı yerinde", GameManager.Instance.CurrentState.ToString());
                    Capture("3_geri_sonra.png");
                    SettingsButton.onClick.Invoke();
                    phase = 7; next = now + .5; return;
                }
                case 7:
                {
                    Check(Options.gameObject.activeInHierarchy, "yeniden açıldı");
                    Call(UI, "HandleEscape");
                    phase = 8; next = now + .5; return;
                }
                case 8:
                    Check(!Options.gameObject.activeInHierarchy && RoundEnd.activeInHierarchy, "Esc: ayarlar kapandı, hazırlık ekranı yerinde");
                    Finish(); return;
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    static void Fit(Transform root, string what)
    {
        var bad = new List<string>();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(false))
        {
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0 || string.IsNullOrWhiteSpace(text.text)) continue;
            Rect r = text.rectTransform.rect;
            Vector4 m = text.margin;
            Bounds b = text.textBounds;
            bool fits = b.min.x >= r.xMin + m.x - 1.5f && b.max.x <= r.xMax - m.z + 1.5f && b.min.y >= r.yMin + m.w - 1.5f && b.max.y <= r.yMax - m.y + 1.5f;
            if (!fits) bad.Add($"{text.transform.parent.name}/{text.name} '{text.text}'");
        }
        Check(bad.Count == 0, what, string.Join(" | ", bad.Take(5)));
    }

    static Rect ScreenRect(RectTransform rect, Camera cam)
    {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        Vector2 a = cam.WorldToScreenPoint(c[0]), b = cam.WorldToScreenPoint(c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/RoundEndSettings");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/RoundEndSettings/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/RoundEndSettingsVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
