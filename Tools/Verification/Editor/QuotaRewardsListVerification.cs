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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

// Kota kartındaki boss ödülleri listesi + kapris plakası (20 Kasım 2026). Kullanıcı istekleri:
//  - "taşıyor, belli bir boyutu olsun, tuşla (Tab) aç kapa, yazılar düzgün sığsın; uzun yazılar aşmamalı"
//  - "kaprisler gelince soldaki plaka kota kartının üstüne biniyor"; kart ve plaka tarla harflerinin (A–J, 1–10) önünde dursun
//  - "fare üstündeyken tekerlekle kaydırma"; "11×11, 7×7, 5×5, 3×3 hepsinde düzgün durduğunu kontrol et"
//  - "güçlüler farklı renkte en üstte, sonra normaller (orta), en altta erken; alış sırasıyla; renkli başlıklar"
// Gerçek GameScene + Mechanics Test Lab oturumu, 1920×1080 çekim (kamera ve arayüz aynı dokuya, HudFramingCapture yöntemi).
// Çekimde tuvaller kamera kipine alınır; oyundaki gibi arayüz 3B dünyanın önünde kalsın diye sıralamaları dünyanın üstüne alınır
// (oyunda tuvaller Screen Space Overlay: her zaman en önde; bu, çekimden önce sahnede denetlenir).
// Akış: ödüller kurulur → sıralama / renk → her grid boyunda (3, 5, 7, 9, 11) kapris + açık / kapalı liste ölçülür ve çekilir →
// sanal fareyle tekerlek (listenin üstünde kaydırır, dışında kaydırmaz) → İngilizce (uzun satırlar).
// Not: Tab tuşunun kendisi (eski Input) toplu kipte basılamaz; liste Tab'ın çağırdığı QuotaHUD.ToggleRewards ile açılır.
// Görüntüler: Logs/QuotaRewards/*.png, sonuç Logs/QuotaRewardsListVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method QuotaRewardsListVerification.RunBatch -Full
[InitializeOnLoad]
public static class QuotaRewardsListVerification
{
    const string Key = "QuotaRewardsListVerification";
    const int W = 1920, H = 1080;
    const float MaxList = 180f, TextRight = 36f, CardWidth = 440f;
    static readonly int[] Sizes = { 3, 5, 7, 9, 11 };
    static readonly List<string> lines = new();
    static int phase, sizeIndex, granted;
    static double next, started;
    static RenderTexture target;
    static Mouse mouse;
    static float scrollBefore;
    static QuotaRewardsListVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Kota kartı ödül listesi", "", "", "", 3);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = false;
        // 3×3'te de kapris kurulabilsin: üretim dört köşeye yayılır (bölge bütün üretimi kaplamasın).
        foreach ((int x, int z) in new[] { (5, 5), (4, 4), (6, 6), (4, 6), (6, 4) })
            ScenarioPresets.Planter(s, ScenarioPresets.Small(), x, z);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static QuotaHUD Hud => UnityEngine.Object.FindAnyObjectByType<QuotaHUD>();
    static FieldEventHud EventHud => UnityEngine.Object.FindAnyObjectByType<FieldEventHud>(FindObjectsInactive.Include);
    static int Size => Sizes[sizeIndex];

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 420) { lines.Add("FAIL timeout at phase " + phase); Finish(1); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    // Oyundaki gerçek tuval kipleri (çekim için değiştirilmeden önce).
                    Canvas quotaCanvas = Hud.GetComponentInParent<Canvas>().rootCanvas, eventCanvas = EventHud.GetComponentInParent<Canvas>().rootCanvas;
                    Check(quotaCanvas.renderMode == RenderMode.ScreenSpaceOverlay && eventCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
                        "oyunda kota kartı ve kapris plakası Screen Space Overlay tuvalde: 3B dünyanın (tarla harfleri dahil) her zaman önünde çizilir",
                        $"{quotaCanvas.name}: {quotaCanvas.renderMode}, {eventCanvas.name}: {eventCanvas.renderMode}");
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    GameSettings.GridCoordinates = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
                    RoundManager.Instance.EditorAddRoundTime(600f);
                    QuotaHUD.SetRewardsOpen(false);
                    lines.Add($"INFO çekim {W}×{H}, HUD kurulu {ComicHud.Installed}, durum {GameManager.Instance.CurrentState}, kota {RoundManager.Instance.QuotaEnabled}");
                    Check(!QuotaHUD.RewardsOpen, "liste varsayılan olarak kapalı");
                    Check(Hud.RewardHeaderText == null, "ödül yokken başlık satırı yok", Hud.RewardHeaderText);
                    phase = 1; next = now + 1; return;
                }
                case 1:
                {
                    var pool = new List<BossRewardSO>();
                    TestRewards.Catalog(pool);
                    var refused = new List<string>();
                    foreach (BossRewardSO reward in pool)
                    {
                        if (BossRewardManager.Instance.Stacks(reward) > 0) continue;
                        if (!TestRewards.GrantWithPrerequisites(reward).Ok) refused.Add(reward.displayName);
                    }
                    granted = BossRewardManager.Instance.Taken.Count;
                    lines.Add($"INFO havuz {pool.Count} ödül, kurulan {granted}, kurulamayan {refused.Count} ({string.Join(", ", refused.Take(8))}{(refused.Count > 8 ? "…" : "")})");
                    Check(granted >= 12, "çok ödüllü durum kuruldu (≥12)", granted.ToString());
                    phase = 2; next = now + .6; return;
                }
                case 2:
                {
                    QuotaHUD hud = Hud;
                    string header = hud.RewardHeaderText;
                    Check(header != null && header.Contains($"({granted})") && header.Contains("ile göster"), "kapalı başlık: sayı + 'Tab ile göster'", header);
                    Check(!hud.RewardListShown, "kapalıyken liste görünmüyor");
                    Check(SpriteCount(Named(hud, "Rewards Header")) == 1, "başlıkta Tab tuş ikonu çiziliyor");
                    CheckOrder(hud.RewardListText);
                    QuotaHUD.ToggleRewards();
                    phase = 3; next = now + .4; return;
                }
                case 3:
                {
                    QuotaHUD hud = Hud;
                    Check(QuotaHUD.RewardsOpen && hud.RewardListShown, "Tab (ToggleRewards) listeyi açtı");
                    Check(hud.RewardHeaderText != null && hud.RewardHeaderText.Contains("ile gizle"), "açık başlık: 'Tab ile gizle'", hud.RewardHeaderText);
                    Check(hud.RewardViewHeight <= MaxList + 2.5f, "liste yüksekliği sınırda (≤180)", hud.RewardViewHeight.ToString("0"));
                    Check(hud.RewardContentHeight > hud.RewardViewHeight + 1f, "çok ödülde içerik kaydırılabilir", $"{hud.RewardContentHeight:0} > {hud.RewardViewHeight:0}");
                    CheckNoHalfLine(hud);
                    hud.ScrollRewards(1e5f);
                    float max = hud.RewardContentHeight - hud.RewardViewHeight;
                    Check(Mathf.Abs(hud.RewardScroll - max) < .5f, "kaydırma en alta kadar iniyor ve sınırda duruyor", $"{hud.RewardScroll:0} / {max:0}");
                    hud.ScrollRewards(0f);
                    phase = 10; next = now + .2; return;
                }
                // ---------------------------------------------------------------- her grid boyu
                case 10:
                {
                    if (TestField.GridSize != Size)
                    {
                        Result g = TestField.SetGridSize(Size);
                        if (!g.Ok) { Check(false, "grid " + Size, g.Message); Finish(1); return; }
                        int h = Size / 2;
                        foreach ((int x, int z) in new[] { (5 - h, 5 - h), (5 + h, 5 + h), (5 - h, 5 + h), (5 + h, 5 - h) })
                            TestField.PlacePlanter(LabCatalog.SmallestPlanter, new GridPosition(x, z), 0, out _);
                    }
                    phase = 11; next = now + 2.5; return;
                }
                case 11:
                {
                    var feel = UnityEngine.Object.FindAnyObjectByType<GridUnlockFeel>();
                    if (feel != null && feel.Playing) { next = now + .5; return; }
                    TestEvents.EndEvent();
                    Result e = default;
                    foreach (FieldEventKind kind in new[] { FieldEventKind.GraftedShell, FieldEventKind.ThornSiege, FieldEventKind.WanderingBounty, FieldEventKind.InvasiveSprout })
                    {
                        for (int seed = 1; seed <= 4; seed++) { e = TestEvents.StartEvent(kind, 0f, seed); if (e.Ok) break; }
                        if (e.Ok) break;
                    }
                    lines.Add($"INFO {Size}×{Size} tarla olayı: {(e.Ok ? e.Message.Split('·')[0].Trim() : "kurulamadı — " + e.Message)}");
                    QuotaHUD.SetRewardsOpen(true);
                    Hud.ScrollRewards(0f);
                    phase = 12; next = now + 1.6; return;
                }
                case 12:
                    Measure(cam, true);
                    Capture($"{Size}x{Size}_acik.png");
                    QuotaHUD.SetRewardsOpen(false);
                    phase = 13; next = now + .4; return;
                case 13:
                    Measure(cam, false);
                    Capture($"{Size}x{Size}_kapali.png");
                    if (Size == 7) { phase = 20; next = now + .2; return; }
                    if (sizeIndex + 1 < Sizes.Length) { sizeIndex++; phase = 10; next = now + .2; return; }
                    phase = 30; next = now + .2; return;
                // ---------------------------------------------------------------- tekerlek (7×7'de)
                case 20:
                {
                    QuotaHUD.SetRewardsOpen(true);
                    Hud.ScrollRewards(0f);
                    phase = 21; next = now + .4; return;
                }
                case 21:
                {
                    QuotaHUD hud = Hud;
                    scrollBefore = hud.RewardScroll;
                    Vector2 over = ScreenCenter(hud.RewardView, cam);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = over });
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = over, scroll = new Vector2(0f, -1f) });
                    phase = 22; next = now + .4; return;
                }
                case 22:
                {
                    QuotaHUD hud = Hud;
                    float moved = hud.RewardScroll - scrollBefore;
                    Check(moved > 20f && moved < 40f, "fare listenin üstündeyken tekerlek bir çentik aşağı: ~2 satır kaydı (sanal fare, Input System)", $"{moved:0.0} px");
                    scrollBefore = hud.RewardScroll;
                    Vector2 field = cam.WorldToScreenPoint(GridManager.Instance.GetGridSystem().GetWorldPosition(5, 5));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = field });
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = field, scroll = new Vector2(0f, -1f) });
                    phase = 23; next = now + .4; return;
                }
                case 23:
                {
                    QuotaHUD hud = Hud;
                    Check(Mathf.Abs(hud.RewardScroll - scrollBefore) < .01f, "fare tarladayken tekerlek listeyi kaydırmıyor");
                    Vector2 over = ScreenCenter(hud.RewardView, cam);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = over });
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = over, scroll = new Vector2(0f, 1f) });
                    phase = 24; next = now + .4; return;
                }
                case 24:
                    Check(Hud.RewardScroll < scrollBefore - 20f, "tekerlek yukarı: liste geri çıktı", $"{Hud.RewardScroll:0}");
                    Capture("7x7_tekerlek.png");
                    QuotaHUD.SetRewardsOpen(false);
                    sizeIndex++;
                    phase = 10; next = now + .3; return;
                // ---------------------------------------------------------------- İngilizce (uzun satırlar), 11×11
                case 30:
                    GameText.Language = "en";
                    QuotaHUD.SetRewardsOpen(true);
                    phase = 31; next = now + 1.2; return;
                case 31:
                {
                    Check(Hud.RewardHeaderText != null && Hud.RewardHeaderText.Contains("to hide"), "İngilizce başlık", Hud.RewardHeaderText);
                    Measure(cam, true, "en");
                    Capture("11x11_en_acik.png");
                    GameText.Language = "tr";
                    Finish(lines.Any(l => l.StartsWith("FAIL")) ? 1 : 0); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(1); }
    }

    // ---------------------------------------------------------------- ölçüm
    static void Measure(Camera cam, bool open, string tag = null)
    {
        string at = $"[{Size}×{Size}{(tag != null ? " " + tag : "")} {(open ? "açık" : "kapalı")}]";
        QuotaHUD hud = Hud;
        var card = (RectTransform)hud.transform;
        Check(hud.RewardListShown == open, $"{at} liste {(open ? "açık" : "kapalı")}");
        // Kartın bütün görünen yazıları yazı alanında (sağdaki kâğıt süsüne değmeden).
        float limit = CardWidth - TextRight + 2f, worst = float.MinValue;
        string worstName = "";
        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(false))
        {
            if (text.name == "Label" || text.name == "Value" || string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo c = text.textInfo.characterInfo[i];
                if (!c.isVisible) continue;
                float right = card.InverseTransformPoint(text.transform.TransformPoint(c.topRight)).x;
                if (right > worst) { worst = right; worstName = text.name; }
            }
        }
        Check(worst <= limit, $"{at} kart yazıları sağ kenara taşmıyor", $"en sağ glif {worst:0.0} ({worstName}) ≤ {limit:0}");
        Check(ViewportBottom(card, cam) > .02f, $"{at} kart ekranın içinde", ViewportBottom(card, cam).ToString("0.000"));
        if (open) CheckNoHalfLine(hud, at);

        FieldEventHud events = EventHud;
        if (events.IsShown)
        {
            var plate = (RectTransform)events.transform.Find("Plate");
            var parent = (RectTransform)plate.parent;
            var corners = new Vector3[4];
            plate.GetWorldCorners(corners);
            float plateTop = parent.InverseTransformPoint(corners[1]).y;
            QuotaHUD.TryGetBottom(out Vector3 bottom);
            float gap = parent.InverseTransformPoint(bottom).y - plateTop;
            Check(gap > 0f && gap < 40f, $"{at} kapris plakası kartın altında", $"boşluk {gap:0.0}");
            TMP_Text label = plate.GetComponentsInChildren<TMP_Text>(false).First(t => t.name == "Label");
            label.ForceMeshUpdate();
            Bounds b = label.textBounds;
            Rect r = label.rectTransform.rect;
            Check(b.size.y <= r.height + 1f && b.size.x <= r.width + 1f, $"{at} kapris yazısı plakaya sığıyor",
                $"yazı {b.size.x:0}×{b.size.y:0} / kutu {r.width:0}×{r.height:0}, ek yükseklik {events.TextExtra:0}");
            Check(ViewportBottom(plate, cam) > .02f, $"{at} plaka ekranın içinde", ViewportBottom(plate, cam).ToString("0.000"));
            lines.Add($"INFO {at} kart {Overlap(card, cam):0}% · plaka {Overlap(plate, cam):0}% açık tarlanın üstünde; örttüğü harf: {Letters(card, plate, cam)}");
        }
        else lines.Add($"INFO {at} kapris yok; kart {Overlap(card, cam):0}% açık tarlanın üstünde; örttüğü harf: {Letters(card, null, cam)}");
    }

    // Açık tarla alanının (eşkenar dörtgen) kartın / plakanın ne kadarının altında kaldığı (örnekleme, %).
    static float Overlap(RectTransform rect, Camera cam)
    {
        GridSystem system = GridManager.Instance.GetGridSystem();
        float cell = (system.GetWorldPosition(1, 0) - system.GetWorldPosition(0, 0)).magnitude;
        Vector3 center = system.GetWorldPosition(GridManager.Instance.GetWidth() / 2, GridManager.Instance.GetHeight() / 2);
        float half = (Size / 2 + .5f) * cell;
        var quad = new Vector2[4];
        Vector3[] world = { new(-half, 0, -half), new(half, 0, -half), new(half, 0, half), new(-half, 0, half) };
        for (int i = 0; i < 4; i++) quad[i] = cam.WorldToScreenPoint(center + world[i]);
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = cam.WorldToScreenPoint(corners[0]), max = cam.WorldToScreenPoint(corners[2]);
        int inside = 0, total = 0;
        for (int x = 0; x < 24; x++)
        for (int y = 0; y < 24; y++)
        {
            var p = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / 24f), Mathf.Lerp(min.y, max.y, (y + .5f) / 24f));
            total++;
            if (InQuad(p, quad)) inside++;
        }
        return 100f * inside / total;
    }

    static bool InQuad(Vector2 p, Vector2[] q)
    {
        bool? sign = null;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = q[i], b = q[(i + 1) % 4];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            bool s = cross >= 0f;
            if (sign == null) sign = s; else if (sign != s) return false;
        }
        return true;
    }

    static string Letters(RectTransform card, RectTransform plate, Camera cam)
    {
        var labels = GridCoordinateLabels.Active;
        if (labels == null) return "-";
        var hit = new List<string>();
        foreach (TMP_Text text in labels.GetComponentsInChildren<TMP_Text>(false))
        {
            Vector2 p = cam.WorldToScreenPoint(text.transform.position);
            if (Contains(card, p, cam) || (plate != null && Contains(plate, p, cam))) hit.Add(text.text);
        }
        return hit.Count == 0 ? "yok" : string.Join(" ", hit);
    }

    static bool Contains(RectTransform rect, Vector2 screen, Camera cam) => RectTransformUtility.RectangleContainsScreenPoint(rect, screen, cam);

    // Liste görünümünün altında yarım satır yok (kaydırma 0'dayken): her satır ya tamamen içeride ya tamamen dışarıda.
    static void CheckNoHalfLine(QuotaHUD hud, string at = "")
    {
        TMP_Text list = hud.RewardListLabel;
        list.ForceMeshUpdate();
        float view = hud.RewardViewHeight, top = list.rectTransform.rect.yMax;
        float scroll = hud.RewardScroll;
        string bad = null;
        for (int i = 0; i < list.textInfo.lineCount; i++)
        {
            TMP_LineInfo line = list.textInfo.lineInfo[i];
            float lineTop = top - line.ascender - scroll, lineBottom = top - line.descender - scroll;
            if (lineTop < view - 1f && lineBottom > view + 1f) bad = $"satır {i}: {lineTop:0}–{lineBottom:0} / görünüm {view:0}";
        }
        Check(bad == null, $"{at} listenin altında yarım kesik satır yok".Trim(), bad);
    }

    // Sıra ve renk: GÜÇLÜ → ORTA → ERKEN başlıkları; her ödül kendi aşamasının başlığı altında, aşamanın renginde, alınış sırasıyla.
    static void CheckOrder(string text)
    {
        BossRewardManager boss = BossRewardManager.Instance;
        BossRewardPoolSO pool = boss.Pool;
        if (pool == null || !pool.IsStaged) { Check(false, "aşamalı havuz yok (sıralama denetlenemedi)"); return; }
        var headings = new List<(int stage, int index, string color)>();
        for (int s = pool.stages.Count - 1; s >= 0; s--)
        {
            int count = boss.Taken.Count(r => pool.StageIndexOf(r) == s);
            if (count == 0) continue;
            string heading = GameText.Format("ui.hud.boss_rewards.group", pool.stages[s].DisplayName, count);
            int index = text.IndexOf("<cspace=0.05em>" + heading, StringComparison.Ordinal);
            string color = index >= 15 ? text.Substring(index - 7, 6) : "?";
            headings.Add((s, index, color));
        }
        Check(headings.All(h => h.index >= 0) && headings.Select(h => h.index).SequenceEqual(headings.Select(h => h.index).OrderBy(i => i)),
            "başlıklar sırayla: güçlü → orta → erken", string.Join(", ", headings.Select(h => $"{pool.stages[h.stage].DisplayName}@{h.index}")));
        Check(headings.Select(h => h.color).Distinct().Count() == headings.Count, "her aşamanın rengi farklı", string.Join(", ", headings.Select(h => $"{pool.stages[h.stage].DisplayName} #{h.color}")));
        bool placed = true, colored = true, ordered = true;
        foreach (var h in headings)
        {
            int end = headings.Where(o => o.index > h.index).Select(o => o.index).DefaultIfEmpty(text.Length).Min();
            int last = -1;
            foreach (BossRewardSO reward in boss.Taken.Where(r => pool.StageIndexOf(r) == h.stage))
            {
                string item = "·<indent=0.8em>" + BossRewardText.ListLine(reward, boss.Stacks(reward)) + "</indent>";
                int at = text.IndexOf(item, StringComparison.Ordinal);
                if (at < h.index || at > end) placed = false;
                if (text.IndexOf("<color=#" + h.color + ">" + item, StringComparison.Ordinal) < 0) colored = false;
                if (at < last) ordered = false;
                last = at;
            }
        }
        Check(placed, "her ödül kendi aşamasının başlığı altında");
        Check(colored, "her ödül satırı aşamasının renginde");
        Check(ordered, "grup içinde alınış sırası");
        lines.Add("INFO gruplar: " + string.Join(" · ", headings.Select(h => $"{pool.stages[h.stage].DisplayName} {boss.Taken.Count(r => pool.StageIndexOf(r) == h.stage)} #{h.color}")));
    }

    static TMP_Text Named(QuotaHUD hud, string name) => hud.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == name);

    static int SpriteCount(TMP_Text text)
    {
        text.ForceMeshUpdate();
        int count = 0;
        for (int i = 0; i < text.textInfo.characterCount; i++) if (text.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite) count++;
        return count;
    }

    static Vector2 ScreenCenter(RectTransform rect, Camera cam) => RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));

    static float ViewportBottom(RectTransform rect, Camera cam)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return cam.WorldToViewportPoint(corners[0]).y;
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(detail != null ? " — " + detail : "")}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/QuotaRewards");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/QuotaRewards/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/QuotaRewardsListVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(code);
    }
}
