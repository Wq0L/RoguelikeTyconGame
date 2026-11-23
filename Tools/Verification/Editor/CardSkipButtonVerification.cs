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

// Kart seçim ekranının ATLA butonu (22 Kasım 2026; kullanıcı: "atla butonu sıkıntılı, tipini düzelteceğiz, yazısını düzelteceğiz,
// dil değişse de taşmaya uygun olacak"). Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080 çekim (kamera ve arayüz aynı
// dokuya; tuvaller çekimde kamera kipine alınır, sıralamaları dünyanın üstüne).
// Akış: level → round biter → kart seçimi açılır → her dilde (tr, en, qps ≈ %35 uzun) buton ölçülür ve çekilir; hak sayısı
// (1 → 3) rozetinde doğru mu.
// Kit kuralları (Docs/UI-Dili.md): eski palet butonu (ComicButtonVisual) yok, SpriteSwap + kit sprite'ı, en az 64 px, yazı Bungee,
// hiçbir yazı kesilmez (Overflow), her yazı kutusuna sığar.
// Görüntüler: Logs/CardSkip/<dil>.png, sonuç Logs/CardSkipButtonVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method CardSkipButtonVerification.RunBatch -Full
[InitializeOnLoad]
public static class CardSkipButtonVerification
{
    const string Key = "CardSkipButtonVerification";
    const int W = 1920, H = 1080;
    static readonly string[] Languages = { "tr", "en", GameText.PseudoLanguage };
    static readonly List<string> lines = new();
    static int phase, languageIndex;
    static double next, started;
    static RenderTexture target;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static CardSkipButtonVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        // Kopyada kartları, butonu ve plakayı gerçek kurucuyla kur ve sahneyi kaydet (lab, GameScene'i diskten yükler).
        string cardFonts = CardSkipButtonInstaller.RestyleCardPrefab();
        if (cardFonts != null) throw new Exception("card prefab: " + cardFonts);
        string install = CardSkipButtonInstaller.InstallInOpenScene();
        if (install != null) throw new Exception("installer: " + install);
        EditorSceneManager.SaveOpenScenes();
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Atla butonu", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static CardSelectionUI Cards => UnityEngine.Object.FindAnyObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static Button Skip => (Button)typeof(CardSelectionUI).GetField("skipButton", Any).GetValue(Cards);
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
            switch (phase)
            {
                case 0:
                {
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    // Üç hak (round başında hesaplanır; burada doğrudan): rozetin geri sayımı görülsün.
                    typeof(RoundManager).GetField("skipUsesRemaining", Any).SetValue(RM, 3);
                    phase = 1; next = now + .4; return;
                }
                case 1:
                {
                    var progress = ProgressionManager.Instance;
                    progress.AddXP(progress.XPToNextLevel); progress.AddXP(progress.XPToNextLevel);
                    Call(RM, "EndRound");
                    phase = 2; next = now + 1.6; return;
                }
                case 2:
                {
                    Check(GameManager.Instance.CurrentState == GameStates.CardSelection && Skip.gameObject.activeInHierarchy, "kart seçimi açık, ATLA görünüyor",
                        $"durum {GameManager.Instance.CurrentState}, hak {RM.SkipUsesRemaining}");
                    KitChecks();
                    CardFontChecks();
                    phase = 3; next = now + .2; return;
                }
                case 3:
                    GameText.Language = Languages[languageIndex];
                    phase = 4; next = now + .8; return;
                case 4:
                {
                    string language = Languages[languageIndex];
                    FitChecks(language);
                    CardFit(language);
                    Capture(language + ".png");
                    languageIndex++;
                    if (languageIndex < Languages.Length) { phase = 3; next = now + .2; return; }
                    GameText.Language = "tr";
                    phase = 5; next = now + .6; return;
                }
                case 5:
                {
                    // Hak sayısı geri sayar: 3 → 2 → 1, son hakta buton kapanır.
                    var seen = new List<string> { CountText() };
                    Call(Cards, "OnSkipPressed"); seen.Add(Skip.gameObject.activeSelf ? CountText() : "kapalı");
                    Call(Cards, "OnSkipPressed"); seen.Add(Skip.gameObject.activeSelf ? CountText() : "kapalı");
                    Check(seen[0].Contains("3") && seen[1].Contains("2") && seen[2].Contains("1"), "hak sayısı geri sayar", string.Join(" → ", seen));
                    phase = 6; next = now + .8; return;
                }
                case 6:
                    Capture("tr_son_hak.png");
                    Finish(); return;
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    // Hak sayısının yazıldığı yer: rozet (varsa) ya da düğme yazısı.
    static string CountText()
    {
        TMP_Text badge = Skip.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Count");
        return badge != null ? badge.text : Skip.GetComponentInChildren<TMP_Text>(true).text;
    }

    static void KitChecks()
    {
        Button skip = Skip;
        var rect = (RectTransform)skip.transform;
        Check(skip.GetComponent<ComicButtonVisual>() == null, "eski palet butonu (ComicButtonVisual) yok");
        Check(skip.transition == Selectable.Transition.SpriteSwap && skip.targetGraphic is Image { sprite: not null } image && image.sprite.name.StartsWith("button_"),
            "kit butonu: SpriteSwap + kit sprite'ı", skip.targetGraphic is Image i ? $"{skip.transition}, {i.sprite?.name}" : "-");
        Check(rect.rect.height >= 63.5f, "en az 64 px yükseklik", $"{rect.rect.width:0}×{rect.rect.height:0}");
        var theme = FeelOverlay.Theme;
        TMP_Text[] texts = skip.GetComponentsInChildren<TMP_Text>(true);
        Check(texts.All(t => t.overflowMode == TextOverflowModes.Overflow), "yazı kesilmiyor (Overflow kipi)", string.Join(", ", texts.Select(t => $"{t.name}:{t.overflowMode}")));
        Check(theme != null && texts.All(t => t.font == theme.displayFont), "yazılar Bungee", string.Join(", ", texts.Select(t => $"{t.name}:{t.font?.name}")));
        Check(texts.All(t => t.font != theme?.displayFont || t.textPreprocessor is ComicKit.TurkishCaps), "Türkçe büyük İ (TurkishCaps)");
        // Kalan seçim plakası: sahnede kurulu, köşeli krem kit şeridi, Bungee, eski yumuşak plaka (ComicPopupPlate) yok.
        TMP_Text remaining = Remaining;
        Image plate = remaining != null ? remaining.transform.parent.GetComponent<Image>() : null;
        Check(remaining != null && remaining.transform.parent.gameObject.activeInHierarchy, "kalan seçim plakası görünüyor", remaining?.text);
        Check(plate != null && plate.sprite != null && plate.sprite.name == "label_cream" && plate.GetComponentsInChildren<ComicPopupPlate>(true).Length == 0,
            "kalan seçim plakası köşeli krem kit şeridi (label_cream), yumuşak plaka yok", plate != null ? plate.sprite?.name : "-");
        Check(remaining != null && theme != null && remaining.font == theme.displayFont && remaining.textPreprocessor is ComicKit.TurkishCaps &&
              remaining.overflowMode == TextOverflowModes.Overflow, "kalan seçim yazısı Bungee, Türkçe büyük İ, kesilmez", remaining?.font?.name);
    }

    static TMP_Text Remaining => (TMP_Text)typeof(CardSelectionUI).GetField("remaining", Any).GetValue(Cards);

    static List<CardUI> Slots => (List<CardUI>)typeof(CardSelectionUI).GetField("cardSlots", Any).GetValue(Cards);

    // Kartların bütün yazıları Bungee, Türkçe büyük İ, kesilmez.
    static void CardFontChecks()
    {
        var theme = FeelOverlay.Theme;
        TMP_Text[] texts = Slots.SelectMany(s => s.GetComponentsInChildren<TMP_Text>(true)).ToArray();
        Check(theme != null && texts.Length > 0 && texts.All(t => t.font == theme.displayFont), "kart yazıları Bungee",
            $"{texts.Length} yazı; Bungee olmayan: " + string.Join(", ", texts.Where(t => t.font != theme?.displayFont).Select(t => $"{t.name}:{t.font?.name}").Distinct()));
        Check(texts.All(t => t.textPreprocessor is ComicKit.TurkishCaps), "kart yazıları Türkçe büyük İ ile çizilir");
        Check(texts.All(t => t.overflowMode == TextOverflowModes.Overflow), "kart yazıları kesilmez (Overflow)");
    }

    // Her kartın görünen her yazısı kutusuna sığar, en az 12 pt.
    static void CardFit(string language)
    {
        var bad = new List<string>();
        float smallest = float.MaxValue; string smallestName = "";
        foreach (CardUI slot in Slots)
            foreach (TMP_Text text in slot.GetComponentsInChildren<TMP_Text>(false))
            {
                text.ForceMeshUpdate();
                if (text.textInfo.characterCount == 0 || string.IsNullOrWhiteSpace(text.text)) continue;
                Rect r = text.rectTransform.rect;
                Vector4 m = text.margin;
                Bounds b = text.textBounds;
                bool fits = b.min.x >= r.xMin + m.x - 1.5f && b.max.x <= r.xMax - m.z + 1.5f && b.min.y >= r.yMin + m.w - 1.5f && b.max.y <= r.yMax - m.y + 1.5f;
                if (!fits) bad.Add($"{slot.name}/{text.name} '{text.text}' {b.size.x:0}×{b.size.y:0} / {r.width:0}×{r.height:0}");
                if (text.fontSize < 11.9f) bad.Add($"{slot.name}/{text.name} {text.fontSize:0.#} pt");
                if (text.fontSize < smallest) { smallest = text.fontSize; smallestName = $"{text.name} '{text.text}'"; }
            }
        Check(bad.Count == 0, $"[{language}] kart yazıları kutularına sığıyor", bad.Count == 0 ? $"en küçük {smallest:0.#} pt ({smallestName})" : string.Join(" | ", bad.Take(5)));
    }

    static void FitChecks(string language)
    {
        var bad = new List<string>();
        string sizes = "";
        // Kalan seçim yazısı bu dilde mi (dil değişince hemen yenilenir).
        int shownCount = Cards.ShownRemaining;
        string expected = shownCount == 1 ? GameText.Get("ui.card.last_choice") : GameText.Format("ui.card.choices_left", shownCount);
        if (Remaining == null || Remaining.text != expected) bad.Add($"kalan seçim dili: '{Remaining?.text}' (beklenen '{expected}')");
        var all = Skip.GetComponentsInChildren<TMP_Text>(false).ToList();
        if (Remaining != null) all.Add(Remaining);
        // Kalan seçimin iki biçimi de: "KALAN SEÇİM: 12" ve son seçim yazısı.
        string shown = Remaining != null ? Remaining.text : null;
        foreach (string variant in new[] { GameText.Format("ui.card.choices_left", 12), GameText.Get("ui.card.last_choice") })
        {
            if (Remaining == null) break;
            Remaining.text = variant;
            Remaining.ForceMeshUpdate();
            Bounds vb = Remaining.textBounds;
            Rect vr = Remaining.rectTransform.rect;
            if (vb.size.x > vr.width + 1.5f || vb.size.y > vr.height + 1.5f) bad.Add($"kalan seçim '{variant}' {vb.size.x:0}×{vb.size.y:0} / {vr.width:0}×{vr.height:0}");
        }
        if (Remaining != null) Remaining.text = shown;
        foreach (TMP_Text text in all)
        {
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0) continue;
            Rect r = text.rectTransform.rect;
            Vector4 m = text.margin;
            Bounds b = text.textBounds;
            bool fits = b.min.x >= r.xMin + m.x - 1.5f && b.max.x <= r.xMax - m.z + 1.5f && b.min.y >= r.yMin + m.w - 1.5f && b.max.y <= r.yMax - m.y + 1.5f;
            if (!fits) bad.Add($"{text.name} '{text.text}' {b.size.x:0}×{b.size.y:0} / kutu {r.width - m.x - m.z:0}×{r.height - m.y - m.w:0}");
            sizes += $"{text.name} '{text.text}' {text.fontSize:0.#} pt; ";
        }
        Check(bad.Count == 0, $"[{language}] ATLA ve kalan seçim yazıları kutusuna sığıyor", bad.Count == 0 ? sizes : string.Join(" | ", bad));
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/CardSkip");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/CardSkip/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/CardSkipButtonVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
