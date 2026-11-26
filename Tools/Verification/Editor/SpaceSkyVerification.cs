using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Toon gökyüzü + güce göre renk doğrulaması (23 Kasım 2026). YALNIZ izole kopyada çalışır.
// Edit Mode: palet verisi (aşama sırası, güç → aşama), OKLCh geçişinde gri çukuru yok, koyuluk eşitliği, ayar varsayılanı, inspector önizlemesi
// (oyunun shader'ıyla çizilir; Logs/SpaceSky/onizleme_*.png).
// Play Mode (gerçek GameScene + Mechanics Test Lab): FeelBootstrap gökyüzünü toon paletle kurar; güç değişince geçiş başlar ve hedef renge varır;
// round aktif değilken renk kaymaz; ayar 'Sabit' → mor palet; bileşen 5 kez kapatılıp açılınca tek gökyüzü dörtgeni; geçiş sırasında kare başı
// GC ayırması 0. Her palet (sabit + 5 aşama) normal tarlada, Don Cephesi ve Sert Kabuk boss'larında arayüzle çekilir: Logs/SpaceSky/<sahne>_<palet>.png.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method SpaceSkyVerification.RunBatch -Full
[InitializeOnLoad]
public static class SpaceSkyVerification
{
    const string Key = "SpaceSkyVerification", LinesKey = "SpaceSkyVerification.Lines";
    const int W = 1920, H = 1080;
    static readonly List<string> lines = new();
    static int phase, sceneIndex, paletteIndex, fails;
    static bool wasActive;
    static double next, started, waitUntil;
    static RenderTexture target;
    static Texture2D readback;
    static SpaceBackground space;
    static SpaceSkyPaletteSO palette;
    static readonly (string id, string boss)[] Scenes =
    {
        ("normal", null),
        ("don_cephesi", "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset"),
        ("sert_kabuk", "Assets/ScriptableObjects/SegmentEvents/SertKabuk.asset"),
    };

    static SpaceSkyVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string what)
    {
        lines.Add((ok ? "PASS " : "FAIL ") + what);
        if (!ok) fails++;
    }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        Directory.CreateDirectory("Logs/SpaceSky");
        EditChecks();
        SessionState.SetString(LinesKey, string.Join("\n", lines));
        SessionState.SetBool(Key, true);
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        Result r = TestSession.StartPlay(Field("Gökyüzü", 7, 12, 5));
        if (!r.Ok) throw new Exception(r.Message);
    }

    // ------------------------------------------------------------------ Edit Mode
    static void EditChecks()
    {
        var p = Resources.Load<SpaceSkyPaletteSO>(SpaceSkyPaletteSO.ResourcePath);
        Check(p != null, "Resources/SpaceSkyPalette yükleniyor");
        if (p == null) return;
        Check(p.stages.Count >= 2 && p.stages[0].minPower == 0, $"en az 2 aşama, ilki güç 0'dan ({p.stages.Count} aşama)");
        bool ascending = true;
        for (int i = 1; i < p.stages.Count; i++) ascending &= p.stages[i].minPower > p.stages[i - 1].minPower;
        Check(ascending, "aşamalar güce göre artan sırada: " + string.Join(" / ", p.stages.Select(s => s.minPower)));
        Check(p.StageIndexFor(0) == 0 && p.StageIndexFor(p.stages[1].minPower - 1) == 0 && p.StageIndexFor(p.stages[1].minPower) == 1
              && p.StageIndexFor(9999) == p.stages.Count - 1, "güç → aşama eşlemesi (sınırlar dahil)");

        // Koyuluk (OKLab L) eşitliği: zemin ve bulut renkleri sabit paletle aynı koyulukta.
        float worstL = 0f;
        foreach (var s in p.stages)
        {
            Color[] a = Arr(s.colors), f = Arr(p.fixedPalette);
            for (int k = 0; k < 5; k++) worstL = Mathf.Max(worstL, Mathf.Abs(OkLch.FromColor(a[k]).x - OkLch.FromColor(f[k]).x));
        }
        Check(worstL < 0.01f, $"aşamalar arası koyuluk farkı < 0,01 (en kötü {worstL:0.0000})");

        // Geçişte gri çukuru yok: her ara noktada doygunluk (C), uçların küçüğünün en az %60'ı.
        float worstRatio = 9f; string worstWhere = "";
        for (int i = 0; i + 1 < p.stages.Count; i++)
        {
            Color[] a = Arr(p.stages[i].colors), b = Arr(p.stages[i + 1].colors);
            for (int k = 0; k < 6; k++)
            {
                float lo = Mathf.Min(OkLch.FromColor(a[k]).y, OkLch.FromColor(b[k]).y);
                if (lo < 0.02f) continue;
                for (int step = 1; step < 10; step++)
                {
                    float c = OkLch.FromColor(OkLch.Lerp(a[k], b[k], step / 10f)).y, ratio = c / lo;
                    if (ratio < worstRatio) { worstRatio = ratio; worstWhere = $"aşama {i}→{i + 1}, renk {k}, t={step / 10f:0.0}"; }
                }
            }
        }
        Check(worstRatio >= 0.6f, $"OKLCh geçişinde gri çukuru yok (en düşük doygunluk oranı {worstRatio:0.00}, {worstWhere})");
        // Karşılaştırma için düz RGB karışımı (bilgi): aynı ölçüde ne kadar griye düşerdi.
        float rgbWorst = 9f;
        for (int i = 0; i + 1 < p.stages.Count; i++)
        {
            Color[] a = Arr(p.stages[i].colors), b = Arr(p.stages[i + 1].colors);
            for (int k = 0; k < 6; k++)
            {
                float lo = Mathf.Min(OkLch.FromColor(a[k]).y, OkLch.FromColor(b[k]).y);
                if (lo < 0.02f) continue;
                rgbWorst = Mathf.Min(rgbWorst, OkLch.FromColor(Color.Lerp(a[k], b[k], .5f)).y / lo);
            }
        }
        lines.Add($"INFO düz RGB karışımında en düşük doygunluk oranı {rgbWorst:0.00} (OKLCh: {worstRatio:0.00})");

        GameSettings.SetDefaults();
        Check(GameSettings.PowerSky && GameSettings.BackgroundEffects, "varsayılan: arka plan açık, güce göre renk açık");

        // Inspector önizlemesi: oyunun shader'ıyla çizim, pembe (shader hatası) / boş değil.
        Material m = null; Mesh q = null;
        var rt = new RenderTexture(480, 270, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.Create();
        var img = new Texture2D(480, 270, TextureFormat.RGB24, false);
        bool previewOk = true;
        var sets = new List<(string, SpaceSkyPaletteSO.SkyColors)> { ("sabit", p.fixedPalette) };
        for (int i = 0; i < p.stages.Count; i++) sets.Add(($"asama{i}", p.stages[i].colors));
        foreach (var (name, colors) in sets)
        {
            bool drawn = SpaceSkyPaletteEditor.RenderPreview(p, colors, rt, ref m, ref q);
            RenderTexture old = RenderTexture.active; RenderTexture.active = rt;
            img.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); img.Apply(); RenderTexture.active = old;
            File.WriteAllBytes($"Logs/SpaceSky/onizleme_{name}.png", img.EncodeToPNG());
            Color32[] px = img.GetPixels32();
            int magenta = px.Count(c => c.r > 200 && c.g < 60 && c.b > 200);
            float spread = px.Max(c => c.r + c.g + c.b) - px.Min(c => c.r + c.g + c.b);
            previewOk &= drawn && magenta < px.Length / 20 && spread > 40;
        }
        Check(previewOk, "inspector önizlemesi oyunun shader'ıyla çiziliyor (6 palet, Logs/SpaceSky/onizleme_*.png)");
        UnityEngine.Object.DestroyImmediate(img); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        if (m != null) UnityEngine.Object.DestroyImmediate(m);
        if (q != null) UnityEngine.Object.DestroyImmediate(q);
    }

    static Color[] Arr(SpaceSkyPaletteSO.SkyColors c) => new[] { c.deep, c.mid, c.glow, c.nebulaA, c.nebulaB, c.lit };

    // ------------------------------------------------------------------ Play Mode
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorUtility.audioMasterMute = true; AudioListener.volume = 0f;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0)
        {
            started = now; next = now + 2;
            lines.Clear();
            lines.AddRange(SessionState.GetString(LinesKey, "").Split('\n').Where(l => l.Length > 0));
            fails = lines.Count(l => l.StartsWith("FAIL"));
            GameText.Language = "tr";
            GameSettings.SetDefaults();
            GameSettings.GridCoordinates = false;
            return;
        }
        if (now - started > 900) { Check(false, "zaman aşımı, aşama " + phase); Finish(); return; }
        if (now < next) return;
        try { Step(now); }
        catch (Exception e) { Check(false, $"aşama {phase}: {e}"); Finish(); }
    }

    static readonly FieldInfo MaterialField = typeof(SpaceBackground).GetField("material", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo BlendStartField = typeof(SpaceBackground).GetField("skyBlendStart", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly PropertyInfo PowerProperty = typeof(BossRewardManager).GetProperty("MusicPower");
    static readonly PropertyInfo RoundActiveProperty = typeof(RoundManager).GetProperty("IsRoundActive");

    static Action LateUpdateOf(SpaceBackground target) =>
        (Action)Delegate.CreateDelegate(typeof(Action), target, typeof(SpaceBackground).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));

    static void SetPower(int value) => PowerProperty.SetValue(BossRewardManager.Instance, value);

    static bool ShowsColors(SpaceSkyPaletteSO.SkyColors c) => SpaceSkyPaletteSO.Same(space.ShownSky, c);

    static void Step(double now)
    {
        switch (phase)
        {
            case 0:   // oturum hazır: oyunun kurulumu (FeelBootstrap) ve zaman durdurma
            {
                if (!TestSession.Ready) { next = now + .5; return; }
                Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                space = Camera.main.GetComponent<SpaceBackground>();
                palette = space != null ? space.SkyPalette : null;
                Check(space != null && palette != null, "FeelBootstrap oyun kamerasına gökyüzünü toon paletle kurdu");
                var mat = space != null ? MaterialField.GetValue(space) as Material : null;
                Check(space != null && space.UsesToonSky && mat != null && mat.shader.name == "ClickerGame/SpaceToonBackground",
                      "oyun gökyüzü ClickerGame/SpaceToonBackground ile çiziliyor (" + (mat != null ? mat.shader.name : "yok") + ")");
                TestTime.SetPaused(true);   // round donar (aktif kalır), ölçeksiz zaman akar: geçişler sürer
                SetPower(0);
                phase = 1; next = now + 1; return;
            }
            case 1:   // güç 0 → aşama 0
                Check(ShowsColors(palette.stages[0].colors), "güç 0: ilk aşama renkleri");
                SetPower(palette.stages[2].minPower + 1);
                phase = 2; next = now + .3; return;
            case 2:   // geçiş başladı mı, kare başı ayırma
            {
                Check(space.SkyTransitioning, "güç artınca geçiş başlıyor");
                var late = LateUpdateOf(space);
                late(); late();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 300; i++) late();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(bytes == 0, $"geçiş sırasında 300 karede GC ayırması 0 ({bytes} bayt)");
                waitUntil = now + palette.transitionSeconds + 1.0;
                phase = 3; next = now + .5; return;
            }
            case 3:
                if (now < waitUntil) { next = now + .3; return; }
                Check(!space.SkyTransitioning && ShowsColors(palette.stages[2].colors), $"geçiş {palette.transitionSeconds:0.#} sn içinde aşama 2'ye vardı");
                // round aktif değilken (ödül ekranı) renk kaymaz
                RoundActiveProperty.SetValue(RoundManager.Instance, false);
                SetPower(palette.stages[3].minPower);
                phase = 4; next = now + .8; return;
            case 4:
                Check(!space.SkyTransitioning && ShowsColors(palette.stages[2].colors), "round aktif değilken güç değişse de renk kaymıyor");
                RoundActiveProperty.SetValue(RoundManager.Instance, true);
                phase = 5; next = now + .3; return;
            case 5:
                Check(space.SkyTransitioning, "round yeniden başlayınca bekleyen geçiş başlıyor");
                GameSettings.PowerSky = false; GameSettings.Commit();
                waitUntil = now + palette.transitionSeconds + 1.0;
                phase = 6; next = now + .5; return;
            case 6:
                if (now < waitUntil) { next = now + .3; return; }
                Check(ShowsColors(palette.fixedPalette), "ayar 'Sabit': gökyüzü sabit mor palete geçti");
                GameSettings.PowerSky = true; GameSettings.Commit();
                for (int i = 0; i < 5; i++) { space.enabled = false; space.enabled = true; }
                phase = 7; next = now + .5; return;
            case 7:
            {
                int quads = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(r => r.name == "Space Background");
                Check(quads == 1, $"bileşen 5 kez kapatılıp açılınca tek gökyüzü dörtgeni ({quads})");
                Check(ShowsColors(palette.stages[palette.StageIndexFor(BossRewardManager.Instance.MusicPower)].colors), "yeniden açılışta geçişsiz doğrudan hedef renk");
                TestTime.SetPaused(false);
                sceneIndex = 0;
                phase = 10; next = now + .2; return;
            }
            // ---------- çekimler: her sahne × her palet ----------
            case 10:
            {
                if (sceneIndex >= Scenes.Length) { Finish(); return; }
                var (id, boss) = Scenes[sceneIndex];
                Scenario s = Field(id, 9, 12, 11 + sceneIndex * 7);
                s.autoAttack = true;
                ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
                Result r = TestSession.Rebuild(s);
                if (!r.Ok) { Check(false, $"{id} kurulum: {r.Message}"); sceneIndex++; return; }
                phase = 11; next = now + 1; return;
            }
            case 11:
            {
                if (!TestSession.Ready) { next = now + .3; return; }
                var (id, boss) = Scenes[sceneIndex];
                Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                space = Camera.main.GetComponent<SpaceBackground>();
                EnsureTarget();
                Camera.main.targetTexture = target;   // gökyüzü dörtgeni çekim oranına (16:9) göre boyutlansın
                if (boss != null)
                {
                    var so = AssetDatabase.LoadAssetAtPath<SegmentEventSO>(boss);
                    int round = TestRewards.NextBossRound(RoundManager.Instance, RoundManager.Instance.CurrentRound);
                    Result b = TestEvents.StartBoss(so, round, ignoreEligibility: true);
                    lines.Add($"{(b.Ok ? "INFO" : "FAIL")} {id} boss: {b.Message}");
                    if (!b.Ok) fails++;
                }
                paletteIndex = -1;
                phase = 12; next = now + 2.5; return;   // hava efekti / perde kurulsun, bitkiler büyüsün
            }
            case 12:
            {
                TestTime.SetPaused(true);
                // palet: −1 sabit, 0…n aşamalar. Geçiş atlanır (çekim hedef rengi gösterir).
                GameSettings.PowerSky = paletteIndex >= 0;
                if (paletteIndex >= 0) SetPower(palette.stages[paletteIndex].minPower);
                wasActive = RoundManager.Instance.IsRoundActive;
                if (!wasActive) RoundActiveProperty.SetValue(RoundManager.Instance, true);
                phase = 13; next = now + .2; return;
            }
            case 13:
            {
                BlendStartField.SetValue(space, 1e-4f);   // geçişin başı çok önce (negatif "geçiş yok" demek)
                LateUpdateOf(space)();   // geçişi bu karede bitir (çekim hedef rengi göstersin)
                phase = 14; next = now + .2; return;
            }
            case 14:
            {
                var (id, _) = Scenes[sceneIndex];
                SpaceSkyPaletteSO.SkyColors want = paletteIndex < 0 ? palette.fixedPalette : palette.stages[paletteIndex].colors;
                string name = paletteIndex < 0 ? "0_sabit" : $"{paletteIndex + 1}_guc{palette.stages[paletteIndex].minPower}";
                if (!ShowsColors(want))
                {
                    var key = typeof(SpaceBackground).GetField("skyKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(space);
                    int count = UnityEngine.Object.FindObjectsByType<SpaceBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                    Check(false, $"{id} {name}: gökyüzü beklenen renkte değil (enabled {space.isActiveAndEnabled}, toon {space.UsesToonSky}, key {key}, " +
                                 $"PowerSky {GameSettings.PowerSky}, güç {BossRewardManager.Instance?.MusicPower}, round {RoundManager.Instance?.IsRoundActive}, " +
                                 $"bileşen {count}, kamera {Camera.main?.name}, gösterilen {space.ShownSky.deep} / istenen {want.deep})");
                }
                Capture($"{id}_{name}");
                if (!wasActive) RoundActiveProperty.SetValue(RoundManager.Instance, false);
                paletteIndex++;
                if (paletteIndex >= palette.stages.Count)
                {
                    lines.Add($"INFO {id}: {palette.stages.Count + 1} palet çekildi");
                    TestTime.SetPaused(false);
                    sceneIndex++;
                    phase = 10; next = now + .3; return;
                }
                phase = 12; next = now + .1; return;
            }
        }
    }

    static Renderer Quad() => UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Space Background");

    // Kamera + bütün ekran arayüzü (boss hava efekti dahil: o tuval boss başlayınca oluşur, her çekimde yeniden bağlanır) tek dokuya.
    static void EnsureTarget()
    {
        if (target != null) return;
        target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; target.Create();
        readback = new Texture2D(W, H, TextureFormat.RGB24, false);
    }

    static void Capture(string file)
    {
        Camera cam = Camera.main;
        EnsureTarget();
        cam.targetTexture = target;
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
        { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; }
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture old = RenderTexture.active; RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, W, H), 0, 0); readback.Apply(false);
        RenderTexture.active = old;
        File.WriteAllBytes($"Logs/SpaceSky/{file}.png", readback.EncodeToPNG());
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        GameSettings.SetDefaults();
        int pass = lines.Count(l => l.StartsWith("PASS"));
        fails = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/SpaceSkyVerification.txt", new[] { $"{(fails == 0 ? "DONE" : "FAIL")} {pass}/{pass + fails}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fails == 0 ? 0 : 1);
    }

    // Orta oyun tarlası: karışık saksılar, süs tile'ları (SteamMediaCapture'daki ile aynı düzen, sade).
    static Scenario Field(string name, int open, int round, int seed)
    {
        Scenario s = ScenarioPresets.New(name, "", "", "", open);
        s.round = round; s.seed = seed; s.boss = Scenario.NoBoss;
        s.autoAttack = false; s.aimFixed = true; s.aimX = 5; s.aimZ = 5;
        int half = open / 2, lo = 5 - half, hi = 5 + half;
        var used = new bool[11, 11];
        uint r = (uint)seed * 2654435761u + 12345u;
        int Next(int n) { r = r * 1664525u + 1013904223u; return (int)((r >> 8) % (uint)n); }
        string small = ScenarioPresets.Small();
        foreach (var (sx, sz) in new[] { (2, 3), (2, 2), (1, 3), (2, 2), (1, 2), (2, 3), (1, 3) })
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
        var types = new[] { TileModifierType.Damage, TileModifierType.Fertile, TileModifierType.Crystal };
        for (int x = lo; x <= hi; x++)
            for (int z = lo; z <= hi; z++)
                if (Next(1000) < 600) ScenarioPresets.Tile(s, x, z, types[Next(types.Length)], (TileRarity)Next(4), Next(3));
        return s;
    }
}
