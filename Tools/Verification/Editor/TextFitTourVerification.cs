using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Yazı sığma turu (26 Kasım 2026: oyun yazıları sade cümlelere çevrildi, İngilizce yeniden yazıldı; kullanıcı: "yazılar oyunda
// UI'dan taşmasın, otursun"). Taşma testi olmayan ekranlar, Türkçe ve İngilizce:
//  1) Eri'nin konuşmaları: diyalog kataloğundaki her satır, seçenek ve cevap kendi kutu boyunda (büyük / küçük);
//  2) tur özeti: kota ve Sınama bildirimleri, oyunun kendi birleştirmesiyle (RoundSummaryUI.QuotaNotice biçimi) en uzun hâlleri;
//  3) başlangıç ekranı (çiftçi / tırpan kartları): panel gerçekten açılır, görünen her yazı;
//  4) yetenek ağacı açılır kutusu: her yetenek adı (sıra numarasıyla) ve açıklama satırları.
// Ölçüm: yazının çizilen sınırı kutusunun içinde (±2 px), kesilen yazı yok, otomatik boy alt sınırın altında değil.
// Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080. Görüntüler Logs/TextFit/*.png, sonuç Logs/TextFitTourVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method TextFitTourVerification.RunBatch -Full
[InitializeOnLoad]
public static class TextFitTourVerification
{
    const string Key = "TextFitTourVerification";
    const int W = 1920, H = 1080;
    static readonly string[] Languages = { "tr", "en" };
    static readonly List<string> lines = new();
    static int phase, languageIndex;
    static double next, started;
    static RenderTexture target;
    static Transform canvasRoot;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static TextFitTourVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Yazı sığma turu", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
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
        if (now - started > 300) { lines.Add("FAIL timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            switch (phase)
            {
                case 0:
                {
                    Camera cam = Camera.main;
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    CameraCanvases();
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    // Açık ve en kalabalık kök tuval (oyunun ana arayüzü). Kapalı bir tuvale kurulan arayüz hiç çizilmez, ölçüm boş kalır.
                    canvasRoot = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                        .Where(c => c.isRootCanvas && c.isActiveAndEnabled).OrderByDescending(c => c.transform.childCount).First().transform;
                    lines.Add($"INFO arayüz tuvali: {canvasRoot.name} ({canvasRoot.childCount} çocuk)");
                    // Tur biter (seviye yok): tur özeti kartı açılır.
                    typeof(RoundManager).GetMethod("EndRound", Any).Invoke(RoundManager.Instance, null);
                    phase = 1; next = now + 1.5; return;
                }
                case 1:
                    GameText.Language = Languages[languageIndex];
                    phase = 2; next = now + .6; return;
                case 2:
                {
                    string language = Languages[languageIndex];
                    Section(() => Dialogue(language), "diyalog", language);
                    Section(() => RoundSummary(language), "tur özeti", language);
                    Section(() => SkillTooltips(language), "yetenek kutusu", language);
                    Section(() => FieldEventPlate(language), "kapris plakası", language);
                    Section(() => StartScreen(language), "başlangıç ekranı", language);
                    phase = 3; next = now + .8; return;
                }
                case 3:
                {
                    string language = Languages[languageIndex];
                    CameraCanvases();
                    Capture($"baslangic_{language}.png");
                    var panel = UnityEngine.Object.FindAnyObjectByType<StartSelectionPanelUI>(FindObjectsInactive.Include);
                    if (panel != null) panel.gameObject.SetActive(false);
                    // Görüntü: konuşma kutuları tek tek, oyunun gösterim akışıyla; giriş hareketi bitince çekilir.
                    ShowDialogueAlone(true);
                    phase = 4; next = now + 1.6; return;
                }
                case 4:
                    CameraCanvases();
                    Capture($"diyalog_buyuk_{Languages[languageIndex]}.png");
                    ShowDialogueAlone(false);
                    phase = 5; next = now + 1.6; return;
                case 5:
                {
                    CameraCanvases();
                    Capture($"diyalog_kucuk_{Languages[languageIndex]}.png");
                    foreach (var b in boxes.Values) if (b != null) b.gameObject.SetActive(false);
                    languageIndex++;
                    if (languageIndex < Languages.Length) { phase = 1; next = now + .2; return; }
                    GameText.Language = "tr";
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    static void Section(Action body, string name, string language)
    {
        try { body(); }
        catch (Exception e) { lines.Add($"FAIL [{language}] {name}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
    }

    // ---- 1) Eri'nin konuşmaları ----
    static readonly Dictionary<bool, DialogueBox> boxes = new();

    static void Dialogue(string language)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalogSO>("Assets/Resources/Dialogue/DialogueCatalog.asset");
        if (catalog == null) throw new Exception("diyalog kataloğu yok");
        var bad = new List<string>();
        int measured = 0;
        float smallest = float.MaxValue;
        foreach (bool big in new[] { true, false })
        {
            if (!boxes.TryGetValue(big, out DialogueBox box) || box == null)
            {
                box = DialogueBox.Create(canvasRoot, big, 900);
                boxes[big] = box;
                // Kutuyu kurup yerleştirmek için bir kez gösterilir; sonra yazılar tek tek yazılıp ölçülür.
                var sample = catalog.entries.First(e => e.size == (big ? DialogueSize.Big : DialogueSize.Small) && e.lines.Length > 0);
                box.Show(sample);
                box.Settle();
                box.gameObject.SetActive(true);
                var group = box.GetComponentInChildren<CanvasGroup>(true);
                if (group != null) group.alpha = 1f;
            }
            var text = (TMP_Text)typeof(DialogueBox).GetField("text", Any).GetValue(box);
            var choices = (TMP_Text[])typeof(DialogueBox).GetField("choiceLabels", Any).GetValue(box);
            Canvas.ForceUpdateCanvases();
            foreach (DialogueEntry entry in catalog.entries.Where(e => e.size == (big ? DialogueSize.Big : DialogueSize.Small)))
                foreach (DialogueLine line in entry.lines)
                {
                    foreach (string key in new[] { line.textKey, line.replyA, line.replyB })
                    {
                        if (string.IsNullOrEmpty(key) || !GameText.Has(key)) continue;
                        measured++;
                        string problem = Measure(text, GameText.Get(key), ref smallest);
                        if (problem != null) bad.Add($"{(big ? "büyük" : "küçük")} kutu {key}: {problem}");
                    }
                    for (int i = 0; i < 2 && choices != null; i++)
                    {
                        string key = i == 0 ? line.choiceA : line.choiceB;
                        if (string.IsNullOrEmpty(key) || !GameText.Has(key)) continue;
                        bool was = choices[i].gameObject.activeSelf;
                        choices[i].transform.parent.gameObject.SetActive(true);
                        choices[i].gameObject.SetActive(true);
                        measured++;
                        string problem = Measure(choices[i], (i + 1) + ". " + GameText.Get(key), ref smallest);
                        if (problem != null) bad.Add($"{(big ? "büyük" : "küçük")} kutu seçenek {key}: {problem}");
                        choices[i].gameObject.SetActive(was);
                    }
                }
        }
        Check(bad.Count == 0, $"[{language}] Eri'nin konuşmaları kutuya sığıyor ({measured} yazı)",
            bad.Count == 0 ? $"en küçük boy {smallest:0.#} pt" : string.Join(" | ", bad.Take(6)));
        foreach (var b in boxes.Values) if (b != null) b.gameObject.SetActive(false);
    }

    // Bir kutuyu tek başına, oyunun gösterim akışıyla açar: en uzun ilk satırı olan konuşma. Diğer kutu kapanır.
    static void ShowDialogueAlone(bool big)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalogSO>("Assets/Resources/Dialogue/DialogueCatalog.asset");
        foreach (var b in boxes.Values) if (b != null) b.gameObject.SetActive(false);
        if (!boxes.TryGetValue(big, out DialogueBox box) || box == null) return;
        DialogueEntry entry = catalog.entries.Where(e => e.size == (big ? DialogueSize.Big : DialogueSize.Small) && e.lines.Length > 0 && GameText.Has(e.lines[0].textKey))
            .OrderByDescending(e => GameText.Get(e.lines[0].textKey).Length).First();
        box.gameObject.SetActive(true);
        box.Show(entry);
        box.gameObject.SetActive(true);
        var group = box.GetComponentInChildren<CanvasGroup>(true);
        if (group != null) group.alpha = 1f;
    }

    // ---- 2) Tur özeti: kota ve Sınama bildirimleri ----
    static void RoundSummary(string language)
    {
        var summary = UnityEngine.Object.FindAnyObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        if (summary == null) throw new Exception("tur özeti yok");
        var warning = (TMP_Text)typeof(RoundSummaryUI).GetField("warning", Any).GetValue(summary);
        var eventNotice = (TMP_Text)typeof(RoundSummaryUI).GetField("eventNotice", Any).GetValue(summary);
        warning.gameObject.SetActive(true); eventNotice.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        string big = "1.234.567";
        string Small(string s) => "\n<size=72%>" + s + "</size>";
        string kota = GameText.Get("ui.quota.name.boss");
        string score = $"{big} / {big}";
        var quota = new Dictionary<string, string>
        {
            ["ilk kota"] = GameText.Format("ui.quota.first.boss", big, 5) + Small(GameText.Get("ui.quota.rule_boss_round")),
            ["ilk kota (her N tur)"] = GameText.Format("ui.quota.first", big, 5) + Small(GameText.Format("ui.quota.rule_every", 5)),
            ["kota tamam"] = GameText.Format("ui.quota.passed.boss", big, big) + Small(GameText.Format("ui.quota.next.boss", big, 5)),
            ["kota erken tamam"] = GameText.Format("ui.quota.met", kota, score) + Small(GameText.Format("ui.quota.segment_closes", 50)),
            ["son tur"] = GameText.Format("ui.quota.last_round", big) + Small(GameText.Format("ui.quota.last_round.detail", kota, score)),
            ["ilerleme"] = GameText.Format("ui.quota.progress", kota, score, 4) + Small(GameText.Format("ui.quota.progress.detail", 50)),
        };
        string boss = GameText.Get("data.boss.don_cephesi.name");
        string rule = GameText.Format("ui.event.frost.rule", GameText.Format("ui.event.band.columns", "A", "K"), "1,5") + " · " + GameText.Format("ui.event.boss_target", big);
        var events = new Dictionary<string, string>
        {
            ["yaklaşan Sınama"] = GameText.Format("ui.event.upcoming_boss", boss, 50) + Small(rule),
            ["son hazırlık"] = GameText.Format("ui.event.final_prep_boss", boss) + Small(rule),
            ["Sınama geçti"] = GameText.Format("ui.event.passed", boss) + Small(GameText.Format("ui.event.next_boss", boss, 50) + " · " + rule),
            ["hazırlık turları"] = GameText.Format("ui.event.upcoming_range", boss, 45, 50) + Small(GameText.Format("ui.event.prep_rounds", 5)),
            // Oyunun gerçek birleşimi (SegmentEventText.Notice): yaklaşan Sınama + kural · hedef · hazırlık turları; en uzun kural (Sert Kabuk, satır aralığı)
            ["yaklaşan Sınama + hazırlık"] = GameText.Format("ui.event.upcoming_boss", GameText.Get("data.boss.sert_kabuk.name"), 50) +
                Small(GameText.Format("ui.event.shell.rule", GameText.Format("ui.event.band.rows", 5, 7), "1,5") + " · " + GameText.Format("ui.event.boss_target", big) + " · " + GameText.Format("ui.event.prep_rounds", 5)),
        };
        var bad = new List<string>();
        float smallest = float.MaxValue;
        foreach (var (name, value) in quota) { string p = Measure(warning, value, ref smallest); if (p != null) bad.Add($"kota · {name}: {p}"); }
        foreach (var (name, value) in events) { string p = Measure(eventNotice, value, ref smallest); if (p != null) bad.Add($"olay · {name}: {p}"); }
        Check(bad.Count == 0, $"[{language}] tur özeti: kota ve Sınama bildirimleri sığıyor ({quota.Count + events.Count} biçim)",
            bad.Count == 0 ? $"en küçük boy {smallest:0.#} pt" : string.Join(" | ", bad.Take(6)));
        warning.text = quota["son tur"]; eventNotice.text = events["yaklaşan Sınama"];
        Capture($"tur_ozeti_{language}.png");
    }

    // ---- 3) Yetenek ağacı açılır kutusu ----
    static void SkillTooltips(string language)
    {
        var host = new GameObject("Text Fit Tooltip", typeof(RectTransform));
        host.layer = canvasRoot.gameObject.layer;
        host.transform.SetParent(canvasRoot, false);
        var view = ComicPopupView.Attach(host);
        view.IsHoverTooltip = true;
        var nodes = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g))).Where(n => n != null).ToList();
        string[] notes =
        {
            GameText.Get("ui.skill.granted.desc"), GameText.Get("ui.skill.disabled.desc2"),
            GameText.Format("ui.skill.unlock.cards_start", GameText.Get("ui.skill.cards.electric")),
            GameText.Format("ui.skill.unlock.cards", GameText.Get("ui.skill.cards.boomerang")),
            GameText.Format("ui.skill.spawn_floor", "0,50"), GameText.Format("ui.skill.disabled.desc", 65),
        };
        var heading = (TMP_Text)typeof(ComicPopupView).GetField("heading", Any).GetValue(view);
        var entries = (List<TMP_Text>)typeof(ComicPopupView).GetField("lines", Any).GetValue(view);
        var bad = new List<string>();
        foreach (SkillNodeSO node in nodes)
        {
            view.Begin(node.DisplayName, GameText.Get("ui.skill.completed_max"));
            foreach (string note in notes) view.Add(note);
            view.End();
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text t in new[] { heading }.Concat(entries.Where(e => e.gameObject.activeSelf)))
            {
                t.ForceMeshUpdate();
                string p = Fits(t);
                if (p != null) bad.Add($"{node.nodeName} / {t.name}: {p}");
            }
        }
        Check(bad.Count == 0, $"[{language}] yetenek ağacı açılır kutusu: {nodes.Count} yeteneğin adı ve açıklamaları sığıyor",
            bad.Count == 0 ? "" : string.Join(" | ", bad.Distinct().Take(6)));
        var longest = nodes.OrderByDescending(n => n.DisplayName.Length).First();
        view.Begin(longest.DisplayName, GameText.Get("ui.skill.completed_max"));
        foreach (string note in notes) view.Add(note);
        view.End();
        CameraCanvases();
        Capture($"yetenek_kutusu_{language}.png");
        host.SetActive(false);
        UnityEngine.Object.Destroy(host);
    }

    // ---- Kapris plakası: dört kaprisin bütün durum yazıları, oyunun kendi plaka hesabıyla (yazı uzarsa plaka aşağı uzar) ----
    // Yazı plakaya sığmalı, plaka ekranın içinde kalmalı ve aşırı uzamamalı (en çok 90 px ek boy: kota kartına yaklaşmasın).
    static void FieldEventPlate(string language)
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<FieldEventHud>(FindObjectsInactive.Include) ?? FieldEventHud.Attach(canvasRoot);
        var plate = (GameObject)typeof(FieldEventHud).GetField("plate", Any).GetValue(hud);
        var label = (TMP_Text)typeof(FieldEventHud).GetField("label", Any).GetValue(hud);
        MethodInfo fit = typeof(FieldEventHud).GetMethod("FitLabel", Any);
        bool was = plate.activeSelf;
        plate.SetActive(true);
        string F(string key, params object[] a) => GameText.Format(key, a);
        string G(string key) => GameText.Get(key);
        var states = new List<(string title, string status, float height)>
        {
            (G("ui.field.shell.title"), G("ui.field.shell.prep"), 106f),
            (G("ui.field.shell.title"), G("ui.field.shell.hud") + "\n" + F("ui.field.shell.hud.nodes", 12, 12, 12, 12) + "\n" + F("ui.field.shell.hud.timer", 10), 106f),
            (G("ui.field.shell.title"), G("ui.field.shell.hud") + "\n" + F("ui.field.shell.hud.nodes", 12, 12, 12, 12) + "\n" + G("ui.field.shell.hud.warn"), 106f),
            (G("ui.field.shell.title"), F("ui.field.shell.closed", "A", "B", 30), 106f),
            (G("ui.field.sprout.title"), G("ui.field.sprout.prep"), 106f),
            (G("ui.field.sprout.title"), F("ui.field.sprout.active", 24, 10, 10, 30), 106f),
            (G("ui.field.sprout.title"), G("ui.field.sprout.broken"), 106f),
            (G("ui.field.sprout.title"), G("ui.field.sprout.withered"), 106f),
            (G("ui.field.thorn.title"), F("ui.field.thorn.prep", 12), 106f),
            (G("ui.field.thorn.title"), F("ui.field.thorn.active", 4, 4, 30), 106f),
            (G("ui.field.thorn.title"), G("ui.field.thorn.broken"), 106f),
            (G("ui.field.thorn.title"), G("ui.field.thorn.withdrew"), 106f),
            (G("ui.field.bounty.title"), G("ui.field.bounty.prep"), 150f),
            (G("ui.field.bounty.title"), G("ui.field.bounty.active") + "\n" + G("ui.field.bounty.active2") + "\n" + F("ui.field.bounty.counts", 120, 45), 150f),
            (G("ui.field.bounty.title"), F("ui.field.bounty.ended", 120), 150f),
        };
        var bad = new List<string>();
        float tallest = 0f; string tallestText = "";
        Camera cam = Camera.main;
        foreach (var (title, status, height) in states)
        {
            label.text = $"<size=120%>{title}</size>\n<size=78%>{status}</size>";
            fit.Invoke(hud, new object[] { height });
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            string p = Fits(label);
            float extra = hud.TextExtra;
            var rect = (RectTransform)plate.transform;
            Rect screen = ScreenRectOf(rect, cam);
            bool inside = screen.xMin >= 0f && screen.yMin >= 0f && screen.xMax <= W && screen.yMax <= H;
            if (p != null) bad.Add($"{Short(status)}: {p}");
            if (!inside) bad.Add($"{Short(status)}: plaka ekran dışında ({screen.xMin:0}–{screen.xMax:0} × {screen.yMin:0}–{screen.yMax:0})");
            if (extra > 90f) bad.Add($"{Short(status)}: plaka {extra:0} px uzadı");
            if (extra > tallest) { tallest = extra; tallestText = Short(status); }
        }
        Check(bad.Count == 0, $"[{language}] kapris plakası: dört kaprisin {states.Count} durum yazısı sığıyor, plaka ekranda",
            bad.Count == 0 ? $"en uzun ek boy {tallest:0} px ({tallestText})" : string.Join(" | ", bad.Take(6)));
        label.text = $"<size=120%>{G("ui.field.shell.title")}</size>\n<size=78%>{G("ui.field.shell.prep")}</size>";
        fit.Invoke(hud, new object[] { 106f });
        Capture($"kapris_{language}.png");
        plate.SetActive(was);
    }

    static Rect ScreenRectOf(RectTransform rect, Camera cam)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 c in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // ---- 4) Başlangıç ekranı ----
    static void StartScreen(string language)
    {
        var panel = StartSelectionPanelUI.Attach(canvasRoot);
        if (panel == null) throw new Exception("başlangıç paneli kurulamadı");
        panel.Open();
        Canvas.ForceUpdateCanvases();
        var bad = new List<string>();
        int measured = 0;
        foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrWhiteSpace(t.text)) continue;
            t.ForceMeshUpdate();
            measured++;
            string p = Fits(t);
            if (p != null) bad.Add($"{Path(t.transform, panel.transform)}: {p}");
        }
        Check(measured > 0 && bad.Count == 0, $"[{language}] başlangıç ekranı: görünen bütün yazılar sığıyor ({measured} yazı)",
            bad.Count == 0 ? "" : string.Join(" | ", bad.Take(6)));
    }

    // ---- ölçüm ----
    static string Measure(TMP_Text text, string value, ref float smallest)
    {
        text.text = value;
        text.maxVisibleCharacters = 99999;
        text.ForceMeshUpdate();
        if (text.enableAutoSizing && text.fontSize < smallest) smallest = text.fontSize;
        return Fits(text);
    }

    static string Fits(TMP_Text text)
    {
        Rect r = text.rectTransform.rect;
        Vector4 m = text.margin;
        Bounds b = text.textBounds;
        // Hiç harf çizilmediyse ölçüm yapılmamıştır (kapalı nesne, kurulmamış kutu): geçti sayılmaz.
        if (!string.IsNullOrWhiteSpace(text.text) && text.textInfo.characterCount == 0)
            return $"ölçülemedi: '{Short(text.text)}' çizilmedi (nesne açık mı: {text.isActiveAndEnabled})";
        if (text.textInfo.characterCount == 0) return null;
        bool fits = b.min.x >= r.xMin + m.x - 2f && b.max.x <= r.xMax - m.z + 2f && b.min.y >= r.yMin + m.w - 2f && b.max.y <= r.yMax - m.y + 2f;
        bool small = text.enableAutoSizing && text.fontSize < text.fontSizeMin - .01f;
        if (fits && !text.isTextTruncated && !small) return null;
        return $"'{Short(text.text)}' {b.size.x:0}×{b.size.y:0} / {r.width - m.x - m.z:0}×{r.height - m.y - m.w:0}, {text.fontSize:0.#} pt" + (text.isTextTruncated ? " (kesildi)" : "");
    }

    static string Short(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "").Replace('\n', '/');
        return s.Length > 70 ? s.Substring(0, 70) + "…" : s;
    }

    static string Path(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (Transform p = t; p != null && p != root; p = p.parent) parts.Insert(0, p.name);
        return string.Join("/", parts);
    }

    static void CameraCanvases()
    {
        Camera cam = Camera.main;
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
        { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        Camera.main.Render();
        Directory.CreateDirectory("Logs/TextFit");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/TextFit/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/TextFitTourVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
