using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

// Steam mağaza ekran görüntüleri ve klipleri (18 Kasım 2026). YALNIZ izole kopyada (Library/VerificationProject) durur; repo'ya girmez.
// Gerçek GameScene + Mechanics Test Lab senaryosu. Oyun saati sabit adımla ilerler (Time.captureDeltaTime = 1/30): her kare tam
// 1/30 sn oyun süresidir ve kare sonunda kameranın çizdiği doku okunur → klipler gerçek hızda, görüntüler kare hassasiyetinde.
// Çalıştırma: $env:STEAM_SHOTS="s1,s4,c1"; $env:STEAM_OUT="<klasör>"; Tools/run-isolated-verification.ps1 -Method SteamMediaCapture.RunBatch -Full
[InitializeOnLoad]
public static class SteamMediaCapture
{
    const string Key = "SteamMediaCapture";
    const float Fps = 30f;
    const string Ffmpeg = @"C:\Users\User\Desktop\JamVideo\tools\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe";

    sealed class Shot
    {
        public string id;
        public Func<Scenario> build;
        public bool ui;
        public int width = 1920, height = 1080;
        public float zoom = 1f;              // <1: daha yakın kadraj (CameraFeel.SetFraming ölçeği)
        public Vector2? focus;               // bakış hücresi (kesirli olabilir)
        public Func<Vector3?> focusWorld;    // dünyada bakış noktası (ör. boss perdesi); boşsa focus
        public string boss;                  // asset yolu
        public int warmup = 60;              // kayıttan önce geçen kare
        public int length = 30;              // kayıt karesi
        public int[] stills;                 // PNG yazılacak kareler (null → klip)
        public Action<int> onFrame;          // kare başına eylem (0: kaydın ilk karesi)
        public Action setup;                 // kurulumdan hemen sonra (ör. yetenek ağacı)
        public bool keepResonanceText;       // false: kayıttan önce "RESONANCE!" yazıları temizlenir
        public bool textFree;                // arayüzde harfli yazılar gizlenir (sayılar kalır)
        public bool stopWhenCleared;         // canlı bitki kalmayınca 12 kare sonra kayıt biter
    }

    static SteamMediaCapture() { EditorApplication.update += Tick; }

    // ------------------------------------------------------------------ durum
    static readonly List<string> lines = new();
    static List<Shot> queue;
    static Shot shot;
    static int phase, frame, generation;
    static double next, started;
    static RenderTexture target;
    static Texture2D readback;
    static Process encoder;
    static string outDir;
    static bool hooked;

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario first = ScenarioPresets.New("Steam medya", "", "", "", 5);
        ScenarioPresets.Planter(first, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(first);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorUtility.audioMasterMute = true; AudioListener.volume = 0f;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0)
        {
            started = now; next = now + 2;
            outDir = Environment.GetEnvironmentVariable("STEAM_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Logs/SteamMedia");
            Directory.CreateDirectory(outDir);
            string wanted = Environment.GetEnvironmentVariable("STEAM_SHOTS") ?? "";
            var all = Shots();
            queue = wanted.Length == 0 ? all : wanted.Split(',').Select(w => all.FirstOrDefault(s => s.id == w.Trim())).Where(s => s != null).ToList();
            lines.Add("INFO shots: " + string.Join(", ", queue.Select(s => s.id)));
            GameText.Language = "en";
            GameSettings.GridCoordinates = false; GameSettings.BackgroundEffects = true; GameSettings.DamageNumbers = true; GameSettings.RarityAuras = true;
            Time.captureDeltaTime = 1f / Fps;
            return;
        }
        if (now - started > 1500) { lines.Add("FAIL timeout"); Finish(1); return; }
        if (now < next) return;
        try { Step(now); }
        catch (Exception e) { lines.Add($"FAIL {shot?.id} phase {phase}: {e}"); CloseEncoder(); NextShot(now); }
    }

    static void Step(double now)
    {
        switch (phase)
        {
            case 0:   // sıradaki çekimin senaryosu
                if (queue.Count == 0) { Finish(lines.Any(l => l.StartsWith("FAIL")) ? 1 : 0); return; }
                shot = queue[0]; queue.RemoveAt(0);
                if (!TestSession.Ready) { next = now + .5; queue.Insert(0, shot); return; }
                generation = TestSession.Rebuilds;
                Result r = TestSession.Rebuild(shot.build());
                if (!r.Ok) { lines.Add($"FAIL {shot.id} rebuild: {r.Message}"); return; }
                phase = 1; next = now + 1; return;
            case 1:   // kurulum bitti mi
                if (!TestSession.Ready) { next = now + .3; return; }
                foreach (Result rr in TestSession.SetupReport) if (!rr.Ok) lines.Add($"WARN {shot.id} setup: {rr.Message}");
                PrepareView();
                if (!string.IsNullOrEmpty(shot.boss))
                {
                    var boss = AssetDatabase.LoadAssetAtPath<SegmentEventSO>(shot.boss);
                    int round = TestRewards.NextBossRound(RoundManager.Instance, RoundManager.Instance.CurrentRound);
                    Result b = TestEvents.StartBoss(boss, round, ignoreEligibility: true);
                    lines.Add($"{(b.Ok ? "INFO" : "FAIL")} {shot.id} boss: {b.Message}");
                }
                shot.setup?.Invoke();
                frame = -shot.warmup;
                Hook();
                phase = 2; next = now + .2; return;
            case 2:   // kayıt kare sonu kancasında sürüyor
                if (frame < shot.length) { next = now + .2; return; }
                CloseEncoder();
                lines.Add($"DONE {shot.id}");
                NextShot(now); return;
        }
    }

    static void NextShot(double now) { phase = 0; frame = int.MinValue; next = now + .5; }

    static void PrepareView()
    {
        Camera cam = Camera.main;
        if (target == null || target.width != shot.width || target.height != shot.height)
        {
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            target = new RenderTexture(shot.width, shot.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            target.Create();
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            readback = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
        }
        cam.targetTexture = target;
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
        {
            if (shot.ui) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; }
            else c.renderMode = RenderMode.ScreenSpaceOverlay;   // dokuya çizilmez
        }
        Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var space = cam.GetComponent<SpaceBackground>(); if (space != null) { space.enabled = false; space.enabled = true; }
        TestVisuals.DamageNumbers = true;
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        TestTime.FrameEnded += OnFrameEnd;
    }

    // Kare sonu: bu karede çizilen görüntüyü oku, sonraki kare için eylemi uygula.
    static void OnFrameEnd()
    {
        if (phase != 2 || shot == null || frame >= shot.length) return;
        try
        {
            ApplyFraming();
            KeepWeatherVisible();
            if ((frame & 3) == 0 || frame == -1) EnsureAuras();
            if (shot.ui && shot.textFree) HideWords();
            if (frame == -1 && !shot.keepResonanceText && VFXManager.Instance != null) VFXManager.Instance.ClearPendingResonances();
            if (frame >= 0)
            {
                bool still = shot.stills != null && shot.stills.Contains(frame);
                if (still || shot.stills == null)
                {
                    RenderTexture old = RenderTexture.active;
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply(false);
                    RenderTexture.active = old;
                    if (still) File.WriteAllBytes(Path.Combine(outDir, $"{shot.id}_f{frame:000}.png"), readback.EncodeToPNG());
                    else WriteClipFrame();
                }
            }
            shot.onFrame?.Invoke(frame);
            if (shot.stopWhenCleared && frame >= 30 && shot.length > frame + 12)
            {
                int alive = AliveCount();
                if ((frame % 15) == 0) lines.Add($"INFO {shot.id} frame {frame} alive {alive}");
                if (alive == 0) { shot.length = frame + 12; lines.Add($"INFO {shot.id} cleared at frame {frame} ({frame / Fps:0.00} s)"); }
            }
        }
        catch (Exception e) { lines.Add($"FAIL {shot.id} frame {frame}: {e.Message}"); frame = shot.length; return; }
        frame++;
    }

    // Boss tipisi / kum fırtınası bir ScreenSpaceOverlay tuvalinde çizilir (BossWeatherOverlay); arayüzsüz çekimde de dokuya girsin.
    static void KeepWeatherVisible()
    {
        if (shot.ui || (frame & 7) != 0) return;
        Camera cam = Camera.main;
        foreach (BossWeatherOverlay w in UnityEngine.Object.FindObjectsByType<BossWeatherOverlay>(FindObjectsSortMode.None))
            if (w.TryGetComponent(out Canvas c) && c.renderMode != RenderMode.ScreenSpaceCamera)
            { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .06f; }
    }

    // Yazısız arayüz (kullanıcı 18 Kasım: sayfa çeviriler bitmeden her dilde aynı görsellerle yayına girsin; sayılar kalabilir).
    // Harf içeren her yazı kapatılır (rakam / sembol kalır); yazıdan ibaret paneller (kota, olay, iş durumu, çıkış düğmesi) gizlenir.
    static readonly System.Text.RegularExpressions.Regex Letters = new(@"\p{L}");
    static void Invisible(GameObject g)
    {
        if (!g.TryGetComponent(out CanvasGroup group)) group = g.AddComponent<CanvasGroup>();
        group.alpha = 0f;
    }

    static readonly System.Text.RegularExpressions.Regex Numbers = new(@"\d+(?:[/.,:]\d+)*");
    static readonly Dictionary<TMPro.TMP_Text, TMPro.TMP_Text> mirrors = new();
    static readonly List<Transform> hudKeep = new();

    // Gizlenen karışık yazının yerine yalnız sayıyı gösteren kopya: aynı font / materyal / renk, plakaya yayılır, ortalı, büyük.
    static void Mirror(TMPro.TMP_Text source, Transform plate = null)
    {
        if (mirrors.TryGetValue(source, out TMPro.TMP_Text copy) && copy != null) return;
        if (plate == null)
        {
            plate = source.transform.parent;
            while (plate != null && !plate.TryGetComponent(out UnityEngine.UI.Image _)) plate = plate.parent;
            if (plate == null) plate = source.transform.parent;
        }
        var go = new GameObject("Steam Number", typeof(RectTransform));
        go.layer = source.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(plate, false);
        go.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(10f, 6f); rect.offsetMax = new Vector2(-10f, -6f);
        var text = go.AddComponent<TMPro.TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
        text.color = source.color; text.fontStyle = source.fontStyle; text.characterSpacing = source.characterSpacing;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.enableWordWrapping = false; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = source.fontSize; text.fontSizeMax = source.fontSize * 1.7f;
        mirrors[source] = text;
    }

    static void UpdateMirrors()
    {
        foreach (var pair in mirrors)
        {
            if (pair.Key == null || pair.Value == null) continue;
            string plain = System.Text.RegularExpressions.Regex.Replace(pair.Key.text ?? "", "<[^>]*>", "");
            // Yalnız harfler silinir; "12 / 50" gibi ayraçlar oyundaki gibi kalır, baştaki / sondaki artıklar (": ", ". ") temizlenir.
            string numbers = System.Text.RegularExpressions.Regex.Replace(plain, @"\p{L}+", "");
            numbers = System.Text.RegularExpressions.Regex.Replace(numbers, @"\s+", " ").Trim(' ', '.', ':', '-');
            if (pair.Value.text != numbers) pair.Value.text = numbers;
            pair.Value.color = pair.Key.color;   // süre uyarı rengi gibi durum renkleri korunur
        }
    }

    static void HideWords()
    {
        // Round kartı, süre plakası ve seviye çubuğu oyundaki gibi kalır: "ROUND", "TIME", "LV." evrensel (kullanıcı 18 Kasım).
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        // Kullanıcı 18 Kasım: "ROUND" yazısı dönmesin; kartta yalnız "12 / 50" büyük ve ortalı. "TIME" ve "LV." oyundaki gibi kalır.
        hudKeep.Clear();
        var timer = typeof(ComicHud).GetField("timerPlate", flags)?.GetValue(null) as RectTransform;
        if (timer != null) hudKeep.Add(timer);
        foreach (XpUI xp in UnityEngine.Object.FindObjectsByType<XpUI>(FindObjectsSortMode.None))
        {
            hudKeep.Add(xp.transform);
            // Seviye atlama zıplaması (DOPunchScale) karede yarıda kalmasın: yazı oyundaki dinlenme boyunda (LV. 1 ile LV. 4 aynı boy).
            var f = BindingFlags.Instance | BindingFlags.NonPublic;
            if (typeof(XpUI).GetField("levelText", f)?.GetValue(xp) is TMPro.TMP_Text level &&
                typeof(XpUI).GetField("hasLevelRestScale", f)?.GetValue(xp) is bool has && has &&
                typeof(XpUI).GetField("levelRestScale", f)?.GetValue(xp) is Vector3 rest)
            {
                DG.Tweening.DOTween.Kill(level.transform);
                level.transform.localScale = rest;
            }
        }
        if (typeof(ComicHud).GetField("roundCard", flags)?.GetValue(null) is RectTransform roundCard)
            foreach (TMPro.TMP_Text t in roundCard.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (mirrors.ContainsValue(t) || (timer != null && t.transform.IsChildOf(timer))) continue;
                string plain = System.Text.RegularExpressions.Regex.Replace(t.text ?? "", "<[^>]*>", "");
                t.enabled = false;
                if (Numbers.IsMatch(plain)) Mirror(t, roundCard);
            }
        UpdateMirrors();
        foreach (var type in new[] { "QuotaHUD", "FieldEventHud", "LevelWorkStatusUI" })
        {
            Type t = Type.GetType(type + ", Assembly-CSharp");
            if (t == null) continue;
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None))
                if (o is Component c && c.gameObject.activeSelf) c.gameObject.SetActive(false);
        }
        // Round kartı + süre plakası ve seviye çubuğu: yazısı gidince oyundaki gibi durmuyor (kullanıcı 18 Kasım) → tümüyle görünmez.
        // SetActive değil saydamlık: yerleşimi ölçen kamera çerçevesi oyundaki gibi kalsın.
        var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();
        if (ui != null && typeof(UIManager).GetField("exitSkillButton", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ui) is UnityEngine.UI.Button exit && exit.gameObject.activeSelf)
            exit.gameObject.SetActive(false);
        foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
        {
            if (!text.enabled || text.canvas == null || text.canvas.renderMode == RenderMode.WorldSpace) continue;
            if (mirrors.ContainsValue(text) || hudKeep.Any(k => k != null && text.transform.IsChildOf(k))) continue;
            string plain = System.Text.RegularExpressions.Regex.Replace(text.text ?? "", "<[^>]*>", "");
            if (!Letters.IsMatch(plain)) continue;
            text.enabled = false;
            // Harf + sayı karışık (round "12/50", süre "65 S", seviye "LV. 2"): kelimeler gider, sayı plakanın ortasında büyük durur.
            if (Numbers.IsMatch(plain)) { Mirror(text); continue; }
            // Yalnız bu yazıyı taşıyan plaka (süre, seviye, "başlangıçtan açık") boş kalmasın.
            Transform parent = text.transform.parent;
            if (parent != null && parent.TryGetComponent(out UnityEngine.UI.Image plate) && plate.enabled)
            {
                bool other = false;
                foreach (UnityEngine.UI.Graphic g in parent.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
                    if (g != plate && g != text && g.enabled) { other = true; break; }
                if (!other) plate.enabled = false;
            }
        }
    }

    static void ApplyFraming()
    {
        if (shot.zoom >= .999f && !shot.focus.HasValue) return;
        CameraFeel feel = CameraFeel.Instance;
        if (feel == null) return;
        Vector2 f = shot.focus ?? new Vector2(5, 5);
        Vector3 world = Cell(f.x, f.y);
        Vector3? custom = shot.focusWorld?.Invoke();
        if (custom.HasValue) world = custom.Value;
        Vector3 shift = world - feel.BaseLookPoint(world.y);
        feel.SetFraming(shot.zoom, shift, false);
    }

    static void WriteClipFrame()
    {
        if (encoder == null)
        {
            string file = Path.Combine(outDir, shot.id + "_master.mp4");
            encoder = Process.Start(new ProcessStartInfo(Ffmpeg,
                $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {target.width}x{target.height} -r {Fps} -i - -vf vflip -c:v libx264 -preset slow -crf 10 -pix_fmt yuv444p \"{file}\"")
            { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true });
        }
        byte[] raw = readback.GetRawTextureData();
        encoder.StandardInput.BaseStream.Write(raw, 0, raw.Length);
    }

    static void CloseEncoder()
    {
        if (encoder == null) return;
        try { encoder.StandardInput.BaseStream.Flush(); encoder.StandardInput.Close(); encoder.WaitForExit(120000); }
        catch (Exception e) { lines.Add("WARN encoder: " + e.Message); }
        encoder = null;
    }

    static void Finish(int code)
    {
        CloseEncoder();
        SessionState.SetBool(Key, false);
        Time.captureDeltaTime = 0f;
        File.WriteAllLines(Path.Combine(outDir ?? "Logs", "SteamMediaCapture.txt"), new[] { code == 0 ? "DONE" : "FAIL" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(code);
    }

    // ------------------------------------------------------------------ yardımcılar
    static Vector3 Cell(float x, float z)
    {
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        Vector3 o = TestAttack.WorldOf(new GridPosition(ix, iz));
        Vector3 dx = TestAttack.WorldOf(new GridPosition(ix + 1, iz)) - o, dz = TestAttack.WorldOf(new GridPosition(ix, iz + 1)) - o;
        return o + dx * (x - ix) + dz * (z - iz);
    }

    static void Aim(float x, float z) => TestAttack.Aim = Cell(x, z);

    static void Attack(Vector3 world)
    {
        TestAttack.ResetCooldown();
        Result r = TestAttack.AttackOnce(world);
        if (!r.Ok) lines.Add($"WARN {shot.id} attack: {r.Message}");
    }

    static void Attack(float x, float z)
    {
        TestAttack.ResetCooldown();
        Result r = TestAttack.AttackOnce(Cell(x, z));
        if (!r.Ok) lines.Add($"WARN {shot.id} attack: {r.Message}");
    }

    // Boss perdesinin (FrostZoneMarkers) görünür parçalarının merkezi ile tarla merkezi arası (t: perdeye yakınlık).
    static Func<Vector3?> TowardCurtain(float t) => () =>
    {
        var marker = UnityEngine.Object.FindAnyObjectByType<FrostZoneMarkers>();
        if (marker == null) return null;
        bool any = false; Bounds b = default;
        foreach (Renderer r in marker.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return null;
        Vector3 center = Cell(5, 5), c = b.center; c.y = center.y;
        return Vector3.Lerp(center, c, t);
    };

    static void TreeZoom(float zoom)
    {
        var ui = UnityEngine.Object.FindAnyObjectByType<SkillTreeUI>();
        if (ui == null) { lines.Add("WARN no SkillTreeUI"); return; }
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SkillTreeUI).GetField("zoomTarget", flags).SetValue(ui, zoom);
        typeof(SkillTreeUI).GetField("zoomPivot", flags).SetValue(ui, Vector2.zero);
        typeof(SkillTreeUI).GetField("zooming", flags).SetValue(ui, true);
    }

    // Nadirlik aurası: oyun bunu batch kipte çizmiyor (PlantRarityAura.Apply → Application.isBatchMode). Normal oyundaki gibi görünsün diye
    // aynı materyal / ölçü / konumla burada uygulanır (oyunun kendi Create + MaterialFor yöntemleri, yansımayla).
    static MethodInfo auraMaterial, auraCreate;
    static void EnsureAuras()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        auraMaterial ??= typeof(PlantRarityAura).GetMethod("MaterialFor", flags);
        auraCreate ??= typeof(PlantRarityAura).GetMethod("Create", flags);
        foreach (PlantHealth p in UnityEngine.Object.FindObjectsByType<PlantHealth>(FindObjectsSortMode.None))
        {
            if (!p.isActiveAndEnabled || p.IsDead || p.Data == null) continue;
            Transform plant = p.transform;
            Transform aura = plant.Find("Rarity Aura");
            if (aura != null && aura.gameObject.activeSelf) continue;
            var material = auraMaterial.Invoke(null, new object[] { p.Data.rarity }) as Material;
            if (material == null) continue;
            if (aura == null) aura = (Transform)auraCreate.Invoke(null, new object[] { plant });
            Vector3 scale = plant.lossyScale;
            float uniform = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            aura.localScale = Vector3.one * (1.7f / uniform);
            aura.localPosition = new Vector3(0f, 0.08f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 0f);
            aura.GetComponent<MeshRenderer>().sharedMaterial = material;
            aura.gameObject.SetActive(true);
        }
    }

    // Yalnız tarla hücrelerindeki gerçek bitkiler (havuzdaki / gizli nesneler sayılmaz).
    static readonly List<PlantHealth> fieldPlants = new();
    static List<PlantHealth> FieldPlants()
    {
        fieldPlants.Clear();
        for (int x = 0; x < 11; x++)
            for (int z = 0; z < 11; z++)
            {
                PlantHealth p = TestField.PlantAt(new GridPosition(x, z));
                // Görünmeyen (ölçeği ~0, yeniden çıkışı yarıda kalmış) bitkiler sayılmaz.
                if (p != null && p.isActiveAndEnabled && !p.IsDead && Mathf.Abs(p.transform.lossyScale.y) > .05f && !fieldPlants.Contains(p)) fieldPlants.Add(p);
            }
        return fieldPlants;
    }

    static int AliveCount() => FieldPlants().Count;

    // Kalan en yakın canlı bitkiye doğru yumuşak nişan (tarla temizleme klibi).
    static Vector3? sweepAim;
    static int lastClick = -999;
    static void SweepToNearest(float cellsPerFrame, bool click = false)
    {
        Vector3 from = sweepAim ?? Cell(5, 5);
        PlantHealth best = null; float bestD = float.MaxValue;
        foreach (PlantHealth p in FieldPlants())
        {
            Vector3 d3 = p.transform.position - from; d3.y = 0f;
            if (d3.sqrMagnitude < bestD) { bestD = d3.sqrMagnitude; best = p; }
        }
        if (best == null) return;
        float cell = (Cell(6, 5) - Cell(5, 5)).magnitude;
        Vector3 goal = best.transform.position; goal.y = from.y;
        from = Vector3.MoveTowards(from, goal, cellsPerFrame * cell);
        sweepAim = from;
        TestAttack.Aim = from;
        Vector3 left = goal - from; left.y = 0f;
        if (click && left.magnitude < .35f * cell && frame - lastClick >= 8) { lastClick = frame; Attack(from); }
    }

    // Orta / geç oyun tarlası: karışık boy saksılar (2×3, 2×2, 1×3, 1×2) + kalan hücrelere 1×1; seed'li yerleşim.
    static Scenario Field(string name, int open, int round, int seed, float tileShare = .65f, TileModifierType[] deco = null, float rareShare = .22f)
    {
        Scenario s = ScenarioPresets.New(name, "", "", "", open);
        s.round = round; s.seed = seed; s.boss = Scenario.NoBoss;
        s.autoAttack = false; s.aimFixed = true; s.aimX = 5; s.aimZ = 5;
        int half = open / 2, lo = 5 - half, hi = 5 + half;
        var used = new bool[11, 11];
        uint r = (uint)seed * 2654435761u + 12345u;
        int Next(int n) { r = r * 1664525u + 1013904223u; return (int)((r >> 8) % (uint)n); }
        var sizes = new (int x, int z)[] { (2, 3), (2, 2), (1, 3), (2, 2), (1, 2), (2, 3), (1, 3), (1, 2), (2, 2), (1, 2), (2, 2) };
        string small = ScenarioPresets.Small();
        foreach (var (sx, sz) in sizes.Take(open >= 9 ? sizes.Length : open >= 7 ? 7 : 3))
        {
            string p = ScenarioPresets.LargePlanter(sx, sz);
            if (p == small) continue;
            for (int t = 0; t < 60; t++)
            {
                int x = lo + Next(open), z = lo + Next(open);
                if (x + sx - 1 > hi || z + sz - 1 > hi) continue;
                bool free = true;
                for (int i = 0; i < sx; i++) for (int j = 0; j < sz; j++) free &= !used[x + i, z + j];
                if (!free) continue;
                for (int i = 0; i < sx; i++) for (int j = 0; j < sz; j++) used[x + i, z + j] = true;
                ScenarioPresets.Planter(s, p, x, z);
                break;
            }
        }
        for (int x = lo; x <= hi; x++) for (int z = lo; z <= hi; z++) if (!used[x, z]) ScenarioPresets.Planter(s, small, x, z);
        // Nadir / epik / efsanevi bitkiler (aura görünsün): 1×1 saksılı hücrelerin bir kısmı, en küçük saksının kendi tablosundan.
        var table = new List<PlantSO>();
        LabCatalog.PlantsOf(LabCatalog.SmallestPlanter, table);
        var rare = table.Where(pl => pl.rarity >= PlantRarity.Rare).OrderBy(pl => pl.rarity).ThenBy(pl => pl.name).ToList();
        if (rare.Count > 0 && rareShare > 0f)
            for (int x = lo; x <= hi; x++)
                for (int z = lo; z <= hi; z++)
                    if (!used[x, z] && Next(1000) < rareShare * 1000) s.plants.Add(new ScenarioPlant { x = x, z = z, plant = rare[Next(rare.Count)].name });
        var types = deco ?? Deco;
        for (int x = lo; x <= hi; x++)
            for (int z = lo; z <= hi; z++)
            {
                if (Next(1000) >= tileShare * 1000) continue;
                ScenarioPresets.Tile(s, x, z, types[Next(types.Length)], (TileRarity)Next(4), Next(3));
            }
        return s;
    }

    // Süs tile'ları: davranış şansı olmayan renkli tile'lar (Boost bunları %100'e çıkarmaz).
    static readonly TileModifierType[] Calm = { TileModifierType.Damage, TileModifierType.Fertile, TileModifierType.Crystal, TileModifierType.Damage, TileModifierType.Fertile };
    // Karışık: doğal (düşük) davranış şanslı tile'lar da var.
    static readonly TileModifierType[] Deco = { TileModifierType.Explosive, TileModifierType.Electric, TileModifierType.Damage, TileModifierType.Fertile,
                                                TileModifierType.Tornado, TileModifierType.Boomerang, TileModifierType.Crystal, TileModifierType.Damage, TileModifierType.Fertile };

    static int[] Every(int step, int count) => Enumerable.Range(0, count).Select(i => i * step).ToArray();

    // Zincir yolu: merkezden çıkan, birbirine değen patlama / elektrik saksıları (yalnız 1×1 saksılı hücrelerde anlamlı).
    static Scenario Chain(string name, int seed, bool secondCluster = false)
    {
        Scenario s = Field(name, 9, 22, seed, .55f, Calm);
        var path = new (int x, int z, bool electric)[] { (5, 5, false), (4, 5, false), (6, 5, false), (5, 4, true), (5, 6, false), (3, 5, false), (6, 6, true), (7, 5, false), (4, 4, false), (5, 3, false), (6, 4, false), (4, 6, true) };
        s.tiles.RemoveAll(t => path.Any(p => p.x == t.x && p.z == t.z));
        if (secondCluster)
            path = path.Concat(new (int x, int z, bool electric)[] { (7, 7, false), (7, 6, false), (6, 7, true), (8, 7, false), (7, 8, false), (8, 6, true), (8, 8, false) })
                       .GroupBy(p => (p.x, p.z)).Select(g => g.First()).ToArray();
        s.tiles.RemoveAll(t => path.Any(p => p.x == t.x && p.z == t.z));
        foreach (var p in path) ScenarioPresets.Tile(s, p.x, p.z, p.electric ? TileModifierType.Electric : TileModifierType.Explosive, TileRarity.Legendary);
        ScenarioPresets.Boost(s, StatType.ExplosionChance); ScenarioPresets.Boost(s, StatType.ElectricChance);
        ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
        s.plantHealth = 1;
        ScenarioPresets.Reward(s, "zincir_hasat");
        return s;
    }

    static List<Shot> Shots()
    {
        var list = new List<Shot>();
        const int W = 3840, H = 2160;

        // S1 · zincir patlama, bütün tarla, biraz yakın kadraj.
        list.Add(new Shot { id = "s1", build = () => Chain("S1 zincir", 7), width = W, height = H, zoom = .64f, focus = new Vector2(5, 5), warmup = 150, length = 40,
                            stills = new[] { 3, 6, 9, 12, 16, 20, 26, 34 }, onFrame = f => { if (f == 0) Attack(5, 5); } });

        // S2 · aynı zincirin yakın planı.
        list.Add(new Shot { id = "s2", build = () => Chain("S2 yakın", 19), width = W, height = H, zoom = .48f, focus = new Vector2(5, 5), warmup = 150, length = 30,
                            stills = new[] { 3, 5, 7, 9, 12, 15, 19, 24 }, onFrame = f => { if (f == 0) Attack(5, 5); } });

        // S3 · kasırga + bumerang tarlayı süpürüyor.
        list.Add(new Shot
        {
            id = "s3", width = W, height = H, zoom = .55f, focus = new Vector2(5, 5), warmup = 150, length = 121, stills = Every(8, 16),
            build = () =>
            {
                Scenario s = Field("S3 kasırga", 7, 18, 23, .2f, Calm);
                for (int x = 3; x <= 7; x++) for (int z = 3; z <= 7; z++)
                    if ((x + z) % 2 == 0) ScenarioPresets.Tile(s, x, z, (x * 3 + z) % 4 == 0 ? TileModifierType.Boomerang : TileModifierType.Tornado, TileRarity.Legendary);
                ScenarioPresets.Boost(s, StatType.TornadoChance); ScenarioPresets.Boost(s, StatType.BoomerangChance);
                ScenarioPresets.Radius(s, 2.5f); s.plantHealth = 1; s.autoAttack = true;
                return s;
            },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + .8f * Mathf.Sin(t * .9f), 5 + .7f * Mathf.Cos(t * .6f)); }
        });

        // S4 · Don Cephesi: tipi + mavi perde.
        list.Add(new Shot
        {
            id = "s4", width = W, height = H, zoom = .62f, focusWorld = TowardCurtain(.5f), boss = "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset", warmup = 180, length = 61, stills = Every(15, 5),
            build = () => { Scenario s = Field("S4 don", 9, 12, 11, .6f, Calm); s.autoAttack = true; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree); return s; },
            onFrame = f => { float t = (f + 180) / Fps; Aim(5 + 2.2f * Mathf.Sin(t * .8f), 5 + 1.8f * Mathf.Cos(t * .55f)); }
        });

        // S5 · Sert Kabuk: kum fırtınası + kahverengi perde.
        list.Add(new Shot
        {
            id = "s5", width = W, height = H, zoom = .62f, focusWorld = TowardCurtain(.5f), boss = "Assets/ScriptableObjects/SegmentEvents/SertKabuk.asset", warmup = 180, length = 61, stills = Every(15, 5),
            build = () => { Scenario s = Field("S5 kabuk", 9, 16, 31, .6f, Calm); s.autoAttack = true; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree); return s; },
            onFrame = f => { float t = (f + 180) / Fps; Aim(5 + 2.0f * Mathf.Cos(t * .7f), 5 + 2.0f * Mathf.Sin(t * .5f)); }
        });

        // S6 · geç oyun ödülleri: hilal, yankı meteoru, anten hatları, kritik ark.
        list.Add(new Shot
        {
            id = "s6", width = W, height = H, zoom = .56f, focus = new Vector2(4, 4), warmup = 120, length = 241, stills = Every(8, 31),
            build = () =>
            {
                Scenario s = Field("S6 geç oyun", 9, 34, 41, .6f, Calm);
                s.autoAttack = true; s.plantHealth = 1; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, .35f);
                foreach (string id in new[] { "hilal_darbesi", "buyuyen_ay", "hasat_yankisi", "elektrik_anteni", "iletken_hat", "kritik_ark" }) ScenarioPresets.Reward(s, id);
                return s;
            },
            onFrame = f => { float t = (f + 120) / Fps; Aim(4 + 1.0f * Mathf.Sin(t * .9f), 4 + .9f * Mathf.Cos(t * .65f)); }
        });

        // S7 · arayüzlü normal round (İngilizce).
        list.Add(new Shot
        {
            id = "s7", ui = true, width = W, height = H, warmup = 150, length = 91, stills = Every(15, 7),
            build = () =>
            {
                Scenario s = Field("S7 hud", 7, 12, 5, .65f, Deco); s.autoAttack = true; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                ScenarioPresets.Stat(s, StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, 3f);
                ScenarioPresets.Stat(s, StatType.ElectricChance, StatTarget.Planter, ModifierOperation.MorePercent, 3f);
                return s;
            },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + 1.6f * Mathf.Sin(t * .9f), 5 + 1.3f * Mathf.Cos(t * .6f)); }
        });

        // S8 · yetenek ağacı (İngilizce): bir kısmı alınmış.
        list.Add(new Shot
        {
            id = "s8", ui = true, width = W, height = H, warmup = 90, length = 46, stills = Every(15, 4), onFrame = f => { if (f == -60) TreeZoom(.62f); },
            build = () => { Scenario s = Field("S8 ağaç", 7, 14, 5, .65f, Calm); s.gold = s.iron = s.stone = 2500; return s; },
            setup = () =>
            {
                SkillTreeManager tree = SkillTreeManager.Instance;
                int bought = 0;
                for (int pass = 0; pass < 6 && bought < 34; pass++)
                    foreach (SkillNodeSO node in tree.AllNodes)
                        if (bought < 34 && tree.TryUpgrade(node)) bought++;
                lines.Add($"INFO s8 bought {bought} nodes");
                UnityEngine.Object.FindAnyObjectByType<UIManager>().OpenSkillShop();
            }
        });

        // C1 · temel döngü (10 sn): imleç gezer, hasat, ara sıra davranış.
        list.Add(new Shot
        {
            id = "c1", width = 2340, height = 1316, zoom = .6f, focus = new Vector2(5, 5), warmup = 150, length = 330,
            build = () =>
            {
                Scenario s = Field("C1 hasat", 7, 10, 3, .65f, Deco); s.autoAttack = true; s.plantHealth = 0; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                ScenarioPresets.Stat(s, StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, 2f);
                ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, .2f);
                ScenarioPresets.Stat(s, StatType.PlantSpawnRate, StatTarget.Planter, ModifierOperation.MorePercent, 2f);
                ScenarioPresets.Stat(s, StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, 4f);
                ScenarioPresets.Stat(s, StatType.ElectricChance, StatTarget.Planter, ModifierOperation.MorePercent, 4f);
                return s;
            },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + 1.3f * Mathf.Sin(t * .75f), 5 + 1.1f * Mathf.Sin(t * .5f + 1f)); }
        });

        // C2 · zincir (10 sn): yakın kadraj, üç ayrı tetik.
        list.Add(new Shot
        {
            id = "c2", width = 2340, height = 1316, zoom = .58f, focus = new Vector2(5, 5), warmup = 150, length = 300,
            build = () => Chain("C2 zincir", 29),
            onFrame = f => { if (f == 10) Attack(5, 5); if (f == 110) Attack(6, 5); if (f == 205) Attack(4, 5); }
        });

        // C3 · Don Cephesi altında hasat (9 sn).
        list.Add(new Shot
        {
            id = "c3", width = 2340, height = 1316, zoom = .62f, focusWorld = TowardCurtain(.45f), boss = "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset", warmup = 180, length = 270,
            build = () => { Scenario s = Field("C3 don", 9, 12, 13, .6f, Calm); s.autoAttack = true; s.plantHealth = 1; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree); return s; },
            onFrame = f => { float t = (f + 180) / Fps; Aim(5 + 2.2f * Mathf.Sin(t * .7f), 5 + 1.8f * Mathf.Cos(t * .5f)); }
        });
        // ---- 3. tur: yazısız arayüz + ilerleme (kullanıcı 18 Kasım) ----
        // E0 · başlangıç: 3×3 açık alan, 2 saksı; arayüzde yalnız sayılar.
        list.Add(new Shot
        {
            id = "e0", ui = true, textFree = true, width = W, height = H, warmup = 150, length = 81, stills = Every(4, 21),
            build = () =>
            {
                Scenario s = ScenarioPresets.New("E0 başlangıç", "", "", "", 3);
                s.round = 2; s.seed = 3; s.boss = Scenario.NoBoss; s.autoAttack = false; s.aimFixed = true; s.aimX = 5; s.aimZ = 5;
                s.plantHealth = 60;   // 2 saksı dolu görünsün; vuruş sayıları yine çıkar
                ScenarioPresets.Planter(s, ScenarioPresets.Small(), 4, 5); ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 4);
                ScenarioPresets.Tile(s, 5, 5, TileModifierType.Fertile, TileRarity.Common); ScenarioPresets.Tile(s, 6, 6, TileModifierType.Damage, TileRarity.Rare);
                ScenarioPresets.Radius(s, 1.2f);
                return s;
            },
            onFrame = f => { if (f == 0) TestAttack.AutoAttack = true; float t = (f + 150) / Fps; Aim(4.6f + .5f * Mathf.Sin(t * 1.3f), 4.6f + .5f * Mathf.Cos(t * 1.1f)); }
        });

        // S7T · orta oyun, yazısız arayüz (kaynak sayaçları, uçan altın, hasar sayıları).
        list.Add(new Shot
        {
            id = "s7t", ui = true, textFree = true, width = W, height = H, warmup = 150, length = 121, stills = Every(8, 16),
            build = () =>
            {
                Scenario s = Field("S7T hud", 7, 12, 5, .65f, Deco); s.autoAttack = true; s.plantHealth = 1; ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                ScenarioPresets.Stat(s, StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, 3f);
                ScenarioPresets.Stat(s, StatType.ElectricChance, StatTarget.Planter, ModifierOperation.MorePercent, 3f);
                return s;
            },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + 1.6f * Mathf.Sin(t * .9f), 5 + 1.3f * Mathf.Cos(t * .6f)); }
        });

        // S8T · yetenek ağacı, yazısız.
        list.Add(new Shot
        {
            id = "s8t", ui = true, textFree = true, width = W, height = H, warmup = 90, length = 46, stills = Every(15, 4), onFrame = f => { if (f == -60) TreeZoom(.8f); },
            build = () => { Scenario s = Field("S8T ağaç", 7, 14, 5, .65f, Calm); s.gold = s.iron = s.stone = 2500; return s; },
            setup = () =>
            {
                SkillTreeManager tree = SkillTreeManager.Instance;
                int bought = 0;
                for (int pass = 0; pass < 6 && bought < 40; pass++)
                    foreach (SkillNodeSO node in tree.AllNodes)
                        if (bought < 40 && tree.TryUpgrade(node)) bought++;
                lines.Add($"INFO s8t bought {bought} nodes");
                UnityEngine.Object.FindAnyObjectByType<UIManager>().OpenSkillShop();
            }
        });

        // Canlı klip ayarı: hızlı saldırı, geniş alan, tek vuruşta hasat, hızlı yeniden büyüme, sık davranış.
        void Lively(Scenario s, float behaviors)
        {
            s.autoAttack = true; s.plantHealth = 1; ScenarioPresets.Radius(s, 2.0f);
            ScenarioPresets.Stat(s, StatType.AttackSpeed, StatTarget.Player, ModifierOperation.MorePercent, 1f);
            ScenarioPresets.Stat(s, StatType.PlantSpawnRate, StatTarget.Planter, ModifierOperation.MorePercent, 5f);
            ScenarioPresets.Stat(s, StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, behaviors);
            ScenarioPresets.Stat(s, StatType.ElectricChance, StatTarget.Planter, ModifierOperation.MorePercent, behaviors);
            ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, .25f);
        }

        list.Add(new Shot
        {
            id = "c1v", width = 2340, height = 1316, zoom = .62f, focus = new Vector2(5, 5), warmup = 150, length = 330,
            build = () => { Scenario s = Field("C1V hasat", 7, 10, 3, .7f, Deco); Lively(s, 6f); return s; },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + 1.2f * Mathf.Sin(t * .8f), 5 + 1.0f * Mathf.Sin(t * .55f + 1f)); }
        });
        list.Add(new Shot
        {
            id = "c2v", width = 2340, height = 1316, zoom = .62f, focus = new Vector2(5, 5), warmup = 150, length = 330,
            build = () =>
            {
                Scenario s = Chain("C2V zincir", 29);
                ScenarioPresets.Stat(s, StatType.PlantSpawnRate, StatTarget.Planter, ModifierOperation.MorePercent, 6f);
                return s;
            },
            onFrame = f => { if (f == 10) Attack(5, 5); if (f == 100) Attack(6, 5); if (f == 190) Attack(4, 5); if (f == 280) Attack(5, 6); }
        });
        list.Add(new Shot
        {
            id = "c3v", width = 2340, height = 1316, zoom = .62f, focusWorld = TowardCurtain(.4f), boss = "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset", warmup = 180, length = 330,
            build = () => { Scenario s = Field("C3V don", 9, 12, 13, .6f, Deco); Lively(s, 4f); return s; },
            onFrame = f => { float t = (f + 180) / Fps; Aim(5 + 1.3f * Mathf.Sin(t * .7f), 5 + 1.1f * Mathf.Cos(t * .5f)); }
        });
        // ---- 4. tur klipleri: tarla dolu kalsın (normal can, güçlü + hızlı vuruş), sürekli sayı ----
        void Busy(Scenario s, float behaviors)
        {
            s.autoAttack = true; s.plantHealth = 0; ScenarioPresets.Radius(s, 2.0f);
            ScenarioPresets.Stat(s, StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, 2f);
            ScenarioPresets.Stat(s, StatType.AttackSpeed, StatTarget.Player, ModifierOperation.MorePercent, 2f);
            ScenarioPresets.Stat(s, StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, behaviors);
            ScenarioPresets.Stat(s, StatType.ElectricChance, StatTarget.Planter, ModifierOperation.MorePercent, behaviors);
            ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, .25f);
        }
        list.Add(new Shot
        {
            id = "c1w", width = 2340, height = 1316, zoom = .62f, focus = new Vector2(5, 5), warmup = 150, length = 330,
            build = () => { Scenario s = Field("C1W hasat", 7, 10, 3, .7f, Deco); Busy(s, 5f); return s; },
            onFrame = f => { float t = (f + 150) / Fps; Aim(5 + 1.2f * Mathf.Sin(t * .8f), 5 + 1.0f * Mathf.Sin(t * .55f + 1f)); }
        });
        list.Add(new Shot
        {
            id = "c2w", width = 2340, height = 1316, zoom = .64f, focus = new Vector2(6, 6), warmup = 150, length = 225,
            build = () => Chain("C2W zincir", 29, true),
            onFrame = f => { if (f == 12) Attack(5, 5); if (f == 110) Attack(7, 7); }
        });
        list.Add(new Shot
        {
            id = "c3w", width = 2340, height = 1316, zoom = .62f, focusWorld = TowardCurtain(.4f), boss = "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset", warmup = 180, length = 330,
            build = () => { Scenario s = Field("C3W don", 9, 12, 13, .6f, Deco); Busy(s, 3f); return s; },
            onFrame = f => { float t = (f + 180) / Fps; Aim(5 + 1.3f * Mathf.Sin(t * .7f), 5 + 1.1f * Mathf.Cos(t * .5f)); }
        });
        // C2X · zincir + Artçı Patlama, ardından imleç kalanları toplar; son bitki hasat edilince biter (üretim kapalı).
        list.Add(new Shot
        {
            id = "c2x", width = 2340, height = 1316, zoom = .8f, focus = new Vector2(5, 5), warmup = 120, length = 360, stopWhenCleared = true,
            build = () =>
            {
                Scenario s = Field("C2X temizle", 7, 22, 29, .5f, Calm);
                var boom = new (int x, int z, bool electric)[] { (5, 5, false), (5, 4, true), (4, 5, false) };
                s.tiles.RemoveAll(t => boom.Any(b => b.x == t.x && b.z == t.z));
                foreach (var b in boom) ScenarioPresets.Tile(s, b.x, b.z, b.electric ? TileModifierType.Electric : TileModifierType.Explosive, TileRarity.Legendary);
                ScenarioPresets.Boost(s, StatType.ExplosionChance); ScenarioPresets.Boost(s, StatType.ElectricChance);
                ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                ScenarioPresets.Stat(s, StatType.AttackSpeed, StatTarget.Player, ModifierOperation.MorePercent, 2.5f);
                s.plantHealth = 1; s.pauseProduction = true;
                foreach (ScenarioPlant pl in s.plants) pl.health = 1;
                ScenarioPresets.Reward(s, "zincir_hasat"); ScenarioPresets.Reward(s, "artci_patlama");
                return s;
            },
            onFrame = f =>
            {
                // 0–1 sn: imleç köşeden merkeze süzülür; 1. sn: tek vuruş zinciri; 2. sn'den sonra imleç kalanları toplar.
                if (f < 30) { float t = Mathf.SmoothStep(0f, 1f, Mathf.Max(0, f) / 30f); Vector3 a = Vector3.Lerp(Cell(3.2f, 3.4f), Cell(5, 5), t); sweepAim = a; TestAttack.Aim = a; }
                if (f == 30) Attack(5, 5);
                if (f == 54) lastClick = -999;
                if (f >= 54) SweepToNearest(.16f, click: true);
            }
        });
        return list;
    }
}
