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

// Run sonu ekranı (22 Kasım 2026; kullanıcı: "yanınca gördüğümüz UI'ı güncellemek, güzelleştirmek lazım, onu da oturtalım").
// Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080 çekim (tuvaller çekimde kamera kipine alınır, sıralamaları dünyanın
// üstüne). Kayıp: R3 (kota + boss round'u) skor 0 ile bitirilir → kota ve boss hasadı tutmaz. Zafer: son round hedefler geçilmiş
// sayılarak bitirilir. Önce birkaç boss ödülü kurulur (liste görünsün). Her dilde (tr, en, qps ≈ %35 uzun) çekilir; kuruluysa
// kit kuralları ve her yazının kutusuna sığması denetlenir.
// Görüntüler: Logs/RunEnd/<durum>_<dil>.png, sonuç Logs/RunEndScreenVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method RunEndScreenVerification.RunBatch -Full (RunVictoryBatch: zafer;
// RunCaptureOnly: bugünkü hâl, kurulum yok)
[InitializeOnLoad]
public static class RunEndScreenVerification
{
    const string Key = "RunEndScreenVerification", ModeKey = "RunEndScreenVerification.Mode";
    const int W = 1920, H = 1080;
    static readonly string[] Languages = { "tr", "en", GameText.PseudoLanguage };
    static readonly List<string> lines = new();
    static int phase, languageIndex;
    static double next, started;
    static RenderTexture target;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static RunEndScreenVerification() { EditorApplication.update += Tick; }

    public static void RunCaptureOnly() => Begin("capture");
    public static void RunBatch() => Begin("fail");
    public static void RunVictoryBatch() => Begin("victory");

    static void Begin(string mode)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        if (mode != "capture")
        {
            var install = Type.GetType("RunEndScreenInstaller, Assembly-CSharp-Editor")?.GetMethod("InstallInOpenScene", BindingFlags.Static | BindingFlags.Public);
            if (install == null) throw new Exception("RunEndScreenInstaller yok");
            string error = (string)install.Invoke(null, null);
            if (error != null) throw new Exception("installer: " + error);
            EditorSceneManager.SaveOpenScenes();
        }
        SessionState.SetBool(Key, true);
        SessionState.SetString(ModeKey, mode);
        Scenario s = ScenarioPresets.New("Run sonu", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static RunCompleteUI Screen => UnityEngine.Object.FindAnyObjectByType<RunCompleteUI>(FindObjectsInactive.Include);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 200) { lines.Add("FAIL timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        string mode = SessionState.GetString(ModeKey, "capture");
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    // Liste görünsün: havuzdan ilk birkaç ödül.
                    var pool = new List<BossRewardSO>();
                    TestRewards.Catalog(pool);
                    int granted = 0;
                    foreach (BossRewardSO reward in pool) { if (granted >= 9) break; if (TestRewards.GrantWithPrerequisites(reward).Ok) granted++; }
                    lines.Add($"INFO {granted} boss ödülü kuruldu, kip {mode}");
                    if (mode == "victory")
                    {
                        RM.EditorJumpToRound(RM.MaxRounds);
                        RM.EditorEndRound(false);
                    }
                    else
                    {
                        RM.EditorJumpToRound(3);
                        RM.EditorEndRound(true);
                    }
                    phase = 1; next = now + 2.5; return;
                }
                case 1:
                {
                    // Round sonu seçimleri (boss ödülü / uzmanlaşma / kartlar) varsa run sonu sonra gelir: zafer için temizle.
                    int guard = 0;
                    while (GameManager.Instance.CurrentState != GameStates.RunComplete && guard++ < 20)
                    {
                        if (GameManager.Instance.CurrentState == GameStates.CardSelection)
                        {
                            var cards = UnityEngine.Object.FindAnyObjectByType<CardSelectionUI>();
                            var offers = (System.Collections.IList)typeof(CardSelectionUI).GetField("currentCards", Any).GetValue(cards);
                            typeof(CardSelectionUI).GetMethod("OnCardSelected", Any).Invoke(cards, new[] { offers[0] });
                        }
                        else if (GameManager.Instance.CurrentState == GameStates.RoundChoice)
                        {
                            // Son round boss round'u: önce uzmanlaşma / boss ödülü seçilir (oyuncu gibi ilk seçenek).
                            var spec = SpecializationManager.Instance;
                            var boss = BossRewardManager.Instance;
                            if (spec != null && spec.IsPending && spec.Options.Count > 0) spec.Choose(spec.Options[0]);
                            else if (boss != null && boss.IsPending && boss.Offer.Count > 0 && !boss.Choose(boss.Offer[0])) { boss.EditorDiscardPendingOffer(); RM.ContinueAfterRoundChoice(); }
                            else if (boss != null && boss.IsPending) { boss.EditorDiscardPendingOffer(); RM.ContinueAfterRoundChoice(); }
                            else break;
                        }
                        else break;
                    }
                    Check(GameManager.Instance.CurrentState == GameStates.RunComplete && Screen != null && Screen.gameObject.activeInHierarchy,
                        "run sonu ekranı açık", $"durum {GameManager.Instance.CurrentState}, sonuç {RM.Outcome}");
                    if (mode != "capture") KitChecks();
                    phase = 2; next = now + 1.6; return;   // kararma + damga + kartlar otursun
                }
                case 2:
                    GameText.Language = Languages[languageIndex];
                    phase = 3; next = now + 1.0; return;
                case 3:
                {
                    string language = Languages[languageIndex];
                    if (mode != "capture") { if (languageIndex == 0) VignetteDone(); Fit(language); }
                    Capture($"{mode}_{language}.png");
                    languageIndex++;
                    if (mode != "capture" && languageIndex < Languages.Length) { phase = 2; next = now + .2; return; }
                    GameText.Language = "tr";
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    static void KitChecks()
    {
        Transform root = Screen.transform;
        Check(root.GetComponentsInChildren<ComicButtonVisual>(true).Length == 0, "eski palet butonu (ComicButtonVisual) yok");
        Button[] buttons = root.GetComponentsInChildren<Button>(false);
        Check(buttons.Length >= 2 && buttons.All(b => b.transition == Selectable.Transition.SpriteSwap && b.targetGraphic is Image { sprite: not null } i && i.sprite.name.StartsWith("button_")),
            "butonlar kit butonu (SpriteSwap)", string.Join(", ", buttons.Select(b => $"{b.name}:{(b.targetGraphic as Image)?.sprite?.name}")));
        Check(buttons.All(b => ((RectTransform)b.transform).rect.height >= 63.5f), "butonlar en az 64 px");
        Check(root.GetComponent<RunResultLayout>() == null, "eski çalışma anı yerleşimi (RunResultLayout) kurulmadı");
        var theme = FeelOverlay.Theme;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        Check(texts.All(t => t.overflowMode != TextOverflowModes.Ellipsis && t.overflowMode != TextOverflowModes.Truncate), "hiçbir yazı kesilmiyor");
        Check(texts.Where(t => t.font == theme.displayFont).All(t => t.textPreprocessor is ComicKit.TurkishCaps), "Bungee yazıları Türkçe büyük İ ile çizilir");
        var vignette = root.GetComponent<RunEndVignette>();
        var dim = root.Find("Backdrop")?.GetComponent<Image>();
        Check(vignette != null && dim != null && dim.material != null && dim.material.shader.name == "UI/HalftoneVignette", "kenarlardan tram kararması kurulu (HalftoneVignette)",
            dim != null && dim.material != null ? dim.material.shader.name : "-");
        bool win = RM.Outcome == RunOutcome.Victory;
        var plate = root.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Title Plate");
        Check(plate != null && plate.sprite != null && plate.sprite.name == (win ? "hud_panel_yellow" : "hud_plate_red"),
            win ? "zafer: altın plaka" : "kayıp: kırmızı uyarı plakası", plate?.sprite?.name);
        TMP_Text Value(int i) => root.Find("Result Content/Result Panel/Row " + i + "/Value")?.GetComponent<TMP_Text>();
        Check(Value(0) != null && Value(1) != null && Value(1).text.Contains("/ " + RM.MaxRounds) && Value(2).text == BossRewardManager.Instance.Taken.Count.ToString(),
            "istatistikler: skor, ulaşılan round, boss ödülü, aktif süre", $"{Value(0)?.text} | {Value(1)?.text} | {Value(2)?.text} | {Value(3)?.text}");
    }

    // Kararma sona ulaştı mı, zıplayan her parça (iki buton dahil) görünür boyda mı, ödül listesi en üstte mi (çekimden önce).
    static void VignetteDone()
    {
        Check(Screen.GetComponent<RunEndVignette>().Progress >= .999f, "kararma tamamlandı (kenarlardan içeri kapandı)", Screen.GetComponent<RunEndVignette>().Progress.ToString("0.###"));
        var group = Screen.transform.Find("Result Content")?.GetComponent<CanvasGroup>();
        Button[] buttons = Screen.GetComponentsInChildren<Button>(false);
        Check(group != null && group.alpha > .99f && buttons.Length == 2 && buttons.All(b => b.transform.lossyScale.x > .5f * Screen.transform.lossyScale.x),
            "içerik ve iki buton görünür boyda (TEKRAR + ANA MENÜ)", string.Join(", ", buttons.Select(b => $"{b.name} {b.transform.localScale.x:0.##}")));
        // v3 (kullanıcı: "kaybedince nasıl kaybettiğimizi anlamıyoruz; açıklama yazıları rahatsız etti"): neden görsel, cümle yok.
        Transform cause = Screen.transform.Find("Result Content/Result Panel/Cause Card");
        Image quotaFill = cause?.Find("Quota Row/Track/Fill")?.GetComponent<Image>();
        TMP_Text quotaValue = cause?.Find("Quota Row/Value")?.GetComponent<TMP_Text>();
        bool win = RM.Outcome == RunOutcome.Victory;
        string expectQuota = $"{HarvestQuota.Format(RM.LastQuotaScore)} / {HarvestQuota.Format(RM.LastQuotaTarget)}";
        bool quotaMet = RM.LastQuotaScore >= RM.LastQuotaTarget;
        if (RM.LastQuotaTarget <= 0) Check(cause != null && !cause.Find("Quota Row").gameObject.activeSelf && (!win || cause.Find("All Met").gameObject.activeSelf),
            "hedefi olmayan kota satırı gizli; zaferde 'TÜM KOTALAR TUTTU' notu");
        else Check(cause != null && quotaValue != null && quotaValue.text == expectQuota && quotaFill != null &&
              (!quotaFill.enabled || ColorUtility.ToHtmlStringRGB(quotaFill.color) == (quotaMet ? "62B04A" : "DE483A")),
            "neden kartı: KOTA hedef / değer + çubuk (tutmayan kırmızı, tutan yeşil)", $"{quotaValue?.text} (beklenen {expectQuota}), çubuk {(quotaFill != null ? ColorUtility.ToHtmlStringRGB(quotaFill.color) : "-")} açık {quotaFill?.enabled}");
        Transform bossRow = cause?.Find("Boss Row");
        Transform badge = cause?.Find("Boss Badge");
        TMP_Text bossName = badge?.Find("Name")?.GetComponent<TMP_Text>();
        if (!win)
            Check(bossRow != null && bossRow.gameObject.activeSelf && badge != null && badge.gameObject.activeSelf && bossName != null && bossName.text.Length > 0,
                "kayıp: boss round'unda BOSS HASADI çubuğu ve 'seni durduran' boss rozeti", bossName?.text);
        string[] gone = { "Reason", "Details", "Times", "Rewards Card" };
        Check(gone.All(n => Screen.GetComponentsInChildren<Transform>(true).All(t => t.name != n)), "açıklama cümlesi, başlangıç ve süre satırları yok");
        var title = Screen.transform.Find("Result Content/Title Plate/Title")?.GetComponent<TMP_Text>();
        Check(title != null && title.text == GameText.Get(win ? "ui.runend.win_title" : "ui.runend.over"), win ? "başlık ZAFER!" : "başlık RUN BİTTİ", title?.text);
        var dim = Screen.transform.Find("Backdrop")?.GetComponent<Image>();
        Material m = dim != null ? dim.materialForRendering : null;
        Check(m != null && m.GetFloat("_Power") > 1f && m.GetFloat("_Cells") > 5f && m.GetFloat("_Feather") > .01f && m.GetFloat("_BaseDim") < .6f,
            "kararma ayarları geçerli (ortası tam mürekkep değil)", m != null ? $"güç {m.GetFloat("_Power")}, nokta {m.GetFloat("_Cells")}, taban {m.GetFloat("_BaseDim")}" : "-");
        Check(dim != null && (win ? dim.color.r > .7f : dim.color.r < .4f), win ? "zafer: kenarlar altın tram (sahne açık)" : "kayıp: kenarlar kırmızı-mürekkep tram",
            dim != null ? ColorUtility.ToHtmlStringRGB(dim.color) : "-");
    }

    // Görünen her yazı kutusuna sığar (kaydırılan liste dışında: o kendi görünümünde kaydırılır, yalnız genişlik denetlenir).
    static void Fit(string language)
    {
        var bad = new List<string>();
        foreach (TMP_Text text in Screen.GetComponentsInChildren<TMP_Text>(false))
        {
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0 || string.IsNullOrWhiteSpace(text.text)) continue;
            bool scrolled = text.GetComponentInParent<ScrollRect>() != null;
            Rect r = text.rectTransform.rect;
            Vector4 m = text.margin;
            Bounds b = text.textBounds;
            bool fitsX = b.min.x >= r.xMin + m.x - 1.5f && b.max.x <= r.xMax - m.z + 1.5f;
            bool fitsY = scrolled || (b.min.y >= r.yMin + m.w - 1.5f && b.max.y <= r.yMax - m.y + 1.5f);
            if (!fitsX || !fitsY) bad.Add($"{text.transform.parent.name}/{text.name} {b.size.x:0}×{b.size.y:0} / {r.width:0}×{r.height:0}");
            if (text.fontSize < 11.9f) bad.Add($"{text.name} {text.fontSize:0.#} pt");
        }
        Check(bad.Count == 0, $"[{language}] run sonu yazıları kutularına sığıyor", string.Join(" | ", bad.Take(5)));
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/RunEnd");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/RunEnd/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/RunEndScreenVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
