using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.5.1 İŞLEV ve GÖRÜNTÜ testi — Artçı alan geri bildirimi ve Hasat Ritmi (Run50_KirilmaErisimiV1).
// Denge ölçümü DEĞİLDİR: tarla testin kurduğu sabit düzendir, bitkilerin canı elle verilir, vuruşlar test eliyle yapılır.
// A) Artçı alanı: çizginin bölgesi = saksının ayak izi + o darbenin gerçek hedef hücreleri (testin kendi geometri hesabıyla);
//    çizgi yalnız bu hücrelerin üstünde; ilk patlamada çizgi yok, ikinci darbenin karesinde var; eski ve yeni yarıçap; hasar aynı.
// B) Uygulama: görsel havuzu doluyken hasar sürer; darbe başına ayırma yok; round sonu ve sahne değişiminde temizlik.
// C) Görüntüler (gerçek oyun render'ı, sabit 1/30 sn adım, kareler LateUpdate'te): ilk darbe → bekleme → ikinci darbe → temizlik;
//    eski / yeni yarıçap, küçük / çok hücreli saksı, orta / kenar / köşe; aynı karenin çizgisiz hâli; boss hava efektiyle dolu tarla.
// D) Hasat Ritmi: sayaç kuralları (doğrudan hasat, davranış ve artçı hasadı, aynı bitki, eşik aşımı, hakkın tek kez harcanması,
//    boş savuruş, yeni run) ve imleç / vuruş halkasının görüntüleri.
[InitializeOnLoad]
public static class AlanGeriBildirimiVerification
{
    const string Key = "AlanGeriBildirimiVerification";
    const string LogFile = "Logs/AlanGeriBildirimiVerification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 3751, N = 11, C = 5;   // 11×11 açık tarla; orta hücre (5,5)
    const float Step = 1f / 30f;
    const double Again = double.NaN;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static AlanGeriBildirimiVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_KirilmaErisimiV1.asset");
        profile.bossSeed = Seed; EditorUtility.SetDirty(profile);
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (nextAt == 0) { nextAt = now + 2; stepSince = now; Application.logMessageReceived += CountLogs; Plan(); return; }
            if (recorderError != null) throw recorderError;
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { Finish(null); return; }
            if (stepIndex != shownStep)
            {
                shownStep = stepIndex; stepSince = now;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines(LogFile, new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 240) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now + .02; EditorApplication.QueuePlayerLoopUpdate(); return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

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
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested; BehaviorEchoes.Traced -= OnEcho;
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(LogFile, new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Must(bool c, string m) { if (!c) throw new Exception(m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static AreaOutlineFeedback Area => AreaOutlineFeedback.Instance;
    static HarvestRhythm Rhythm => RunPower.Rhythm;
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO E1 => Profile("Run50_KirilmaErisimiV1");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static BossRewardPoolSO Pool => E1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardSO Find(BossRewardPoolSO pool, string id) => RewardOfferLab.All(pool).FirstOrDefault(r => r.id == id);
    static BossRewardSO Artci => Find(Pool, "artci_patlama");
    static BossRewardSO OldArtci => Find(K1.bossRewards, "artci_patlama");
    static BossRewardSO Ritim => Find(Pool, "hasat_ritmi");
    static bool Grant(BossRewardSO reward) => RewardOfferLab.Grant(reward);
    static float CellsOf(BossRewardSO reward) => HarvestBehaviorGeometry.ExplosionRadiusCells * reward.echoRadius;
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    static void Strike(float damage, float radius)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
    }

    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));

    // ---------------------------------------------------------------- görüntü
    static RenderTexture target;
    static int captures;
    // Gerçek oyun render'ı (HUD dahil). focus verilirse ayrıca o noktanın çevresinden 2 kat büyütülmüş bir kesit kaydedilir.
    static void Capture(string name, Vector3? focus = null, float worldRadius = 7f)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        var roots = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        foreach (var canvas in roots.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var old = cam.targetTexture;
        cam.targetTexture = target;
        // HUD saniyede 10 kez yenilenir; batch'te kareler hızlı aktığı için görüntüden önce elle yenilenir (önceki vakanın satırı kalmasın).
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        if (hud != null) Call(hud, "Refresh");
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG());
        if (focus.HasValue)
        {
            // Kesit: odağın çevresindeki dünya karesinin ekrandaki sınırları (render dokusunun koordinatında).
            float sx = (float)target.width / Mathf.Max(1, cam.pixelWidth), sy = (float)target.height / Mathf.Max(1, cam.pixelHeight);
            var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) }
                .Select(d => cam.WorldToScreenPoint(focus.Value + d * worldRadius)).ToList();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(corners.Min(p => p.x) * sx), 0, target.width - 2), x1 = Mathf.Clamp(Mathf.CeilToInt(corners.Max(p => p.x) * sx), x0 + 2, target.width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(corners.Min(p => p.y) * sy), 0, target.height - 2), y1 = Mathf.Clamp(Mathf.CeilToInt(corners.Max(p => p.y) * sy), y0 + 2, target.height);
            int w = x1 - x0, h = y1 - y0;
            var pixels = png.GetPixels(x0, y0, w, h);
            var crop = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            var big = new Color[w * 2 * h * 2];
            for (int y = 0; y < h * 2; y++) for (int x = 0; x < w * 2; x++) big[y * w * 2 + x] = pixels[(y / 2) * w + x / 2];
            crop.SetPixels(big); crop.Apply();
            File.WriteAllBytes($"Logs/{name}_yakin.png", crop.EncodeToPNG());
            Object.DestroyImmediate(crop);
        }
        Object.DestroyImmediate(png);
        captures++;
        Note("görüntü: Logs/" + name + ".png" + (focus.HasValue ? " (+ _yakin)" : ""));
    }

    // Kare kare görüntü: oyun döngüsünün içinde (LateUpdate, BehaviorEchoes.Update'ten sonra) belirli karede çalışır.
    static Exception recorderError;
    sealed class Recorder : MonoBehaviour
    {
        public readonly List<(int frame, string name, Action action)> plan = new();
        public readonly List<string> late = new();
        void LateUpdate()
        {
            try
            {
                for (int i = 0; i < plan.Count;)
                {
                    if (plan[i].frame > Time.frameCount) { i++; continue; }
                    var item = plan[i]; plan.RemoveAt(i);
                    if (item.frame < Time.frameCount) late.Add($"{item.name}: {Time.frameCount - item.frame} kare geç");
                    item.action();
                }
            }
            catch (Exception ex) { recorderError = ex; plan.Clear(); }
        }
    }
    static Recorder recorder;
    static Recorder Rec => recorder != null ? recorder : recorder = new GameObject("Capture Recorder (verification)").AddComponent<Recorder>();

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("tarla ve round", LabRound);
        // A) alan ve gerçek darbe
        Add("yeni: orta 1×1", () => AreaCase("yeni 2,00 · orta 1×1", 1, 1, C, C, Artci));
        Add("sonuç", AreaResult);
        Add("yeni: kenar 1×1", () => AreaCase("yeni 2,00 · kenar 1×1", 1, 1, 0, C, Artci));
        Add("sonuç", AreaResult);
        Add("yeni: köşe 1×1", () => AreaCase("yeni 2,00 · köşe 1×1", 1, 1, 0, 0, Artci));
        Add("sonuç", AreaResult);
        Add("yeni: orta 2×2", () => AreaCase("yeni 2,00 · orta 2×2", 2, 2, 4, 4, Artci));
        Add("sonuç", AreaResult);
        Add("yeni: kenar 2×2", () => AreaCase("yeni 2,00 · kenar 2×2", 2, 2, 0, 4, Artci));
        Add("sonuç", AreaResult);
        Add("yeni: orta 2×3", () => AreaCase("yeni 2,00 · orta 2×3", 2, 3, 4, 4, Artci));
        Add("sonuç", AreaResult);
        Add("eski: orta 1×1", () => AreaCase("eski 1,50 · orta 1×1", 1, 1, C, C, OldArtci));
        Add("sonuç", AreaResult);
        Add("eski: orta 2×3", () => AreaCase("eski 1,50 · orta 2×3", 2, 3, 4, 4, OldArtci));
        Add("sonuç", AreaResult);
        Add("eski ve yeni bölge", OldVersusNew);
        // B) uygulama
        Add("havuz dolu: on artçı aynı karede", PoolFullCase);
        Add("havuz dolu sonucu", PoolFullResult);
        Add("ayırma ve maliyet", Allocation);
        // C) görüntüler
        Add("dizi: kur", SequenceSetup);
        Add("dizi: bekle", () => WaitRecorder());
        foreach (var shot in Shots())
        {
            var s = shot;
            Add("görüntü: " + s.name, () => ShotCase(s.name, s.sx, s.sz, s.ox, s.oz, s.reward(), s.blank));
            Add("görüntü bekle", () => WaitRecorder());
        }
        Add("boss: Don Cephesi", () => BossShot(true));
        Add("görüntü bekle", () => WaitRecorder());
        Add("boss bitir", EndBoss);
        Add("boss: Sert Kabuk", () => BossShot(false));
        Add("görüntü bekle", () => WaitRecorder());
        Add("boss bitir", EndBoss);
        Add("görüntü kontrolü", ShotSummary);
        // D) Hasat Ritmi
        Add("ritim: patlayan önce", RhythmBehaviorKill);
        Add("ritim: sıra ters", RhythmOrder);
        Add("ritim: artçı hasadı", RhythmEchoCase);
        Add("ritim: artçı sonucu", RhythmEchoResult);
        Add("ritim: eşik aşımı", RhythmThreshold);
        Add("ritim: güçlü saldırı", RhythmEmpowered);
        Add("ritim: boş savuruş", RhythmEmptySwing);
        Add("ritim görüntüleri", RhythmVisuals);
        Add("ritim vuruş halkası", () => WaitRecorder());
        Add("ritim normale dönüş", RhythmVisualsAfter);
        // B) temizlik
        Add("round sonu: dalga", RoundEndCase);
        Add("round sonu: bekle", RoundEndApply);
        Add("round sonu sonucu", RoundEndResult);
        Add("sahne yeniden", () => Load(E1));
        Add("yeni sahne temiz", CleanStart);
        Add("kapanış", FinalCheck);
    }

    // ---------------------------------------------------------------- tarla
    sealed class Hit { public DamageType Type; public int Damage, X, Z; public bool Echo; }
    static readonly List<Hit> hits = new();
    static readonly List<(DamageType type, bool echo, int x, int z)> harvests = new();
    static int minX, minZ;
    static int planFrame, applyFrame, playedAtPlan, playedAtApply;
    static readonly List<(int offset, string name, Action action)> echoShots = new();

    static (int, int) Local(Vector3 world)
    {
        var p = GridManager.Instance.GetGridSystem().GetGridPosition(world);
        return (p.x - minX, p.z - minZ);
    }
    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        var c = Local(plant.transform.position);
        hits.Add(new Hit { Type = type, Damage = damage, X = c.Item1, Z = c.Item2, Echo = BehaviorEchoes.IsExecuting });
    }
    static void OnHarvested(PlantHealth plant)
    {
        var c = Local(plant.transform.position);
        harvests.Add((plant.KilledBy, BehaviorEchoes.IsExecuting, c.Item1, c.Item2));
    }
    static void OnEcho(string phase, DamageType type, int id, float delay, int frames)
    {
        if (type != DamageType.Explosion) return;
        if (phase == "plan") { planFrame = Time.frameCount; playedAtPlan = Area != null ? Area.AftershockPlayed : 0; }
        if (phase != "uygula") return;
        applyFrame = Time.frameCount; playedAtApply = Area != null ? Area.AftershockPlayed : 0;
        // İkinci darbenin karesine göre görüntüler (aynı kare: offset 0).
        foreach (var shot in echoShots) Rec.plan.Add((applyFrame + shot.offset, shot.name, shot.action));
        echoShots.Clear();
    }

    static double LabRound()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != E1) return Again;
        Player.enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(N);
        var open = RewardOfferLab.OpenCells();
        Must(open.Count == N * N, $"open cells {open.Count}");
        minX = open.Min(c => c.GetGridPosition().x); minZ = open.Min(c => c.GetGridPosition().z);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Strike(100f, .4f);
        PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested; BehaviorEchoes.Traced += OnEcho;
        return .05;
    }

    static GroundCell CellAt(int x, int z) => RewardOfferLab.OpenCells().First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
    static Vector3 PosAt(int x, int z) => CellAt(x, z).transform.position;
    static PlanterSO Planter(int sx, int sz) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {sx}x{sz}.asset");
    static PlanterBrain source; static HashSet<(int, int)> sourceCells; static readonly List<PlanterBrain> field = new();
    static PlantHealth PlantOf(GridObject grid) { var p = grid.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static PlanterBrain Place(int sx, int sz, int x, int z, string tile = null) =>
        KirilmaErisimMeasurement.PlacePlanter(Planter(sx, sz), CellAt(x, z), tile != null ? RewardOfferLab.Tile(tile) : null, tile != null ? 1f : 0f);

    static void Respawn(PlanterBrain brain, int health)
    {
        foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); }
        foreach (var grid in brain.OccupiedGrids)
        {
            var plant = PlantOf(grid);
            if (plant == null) throw new Exception("no plant spawned");
            SetF(plant, "maxHealth", 1000000); SetF(plant, "currentHealth", health);
        }
    }

    static HashSet<(int, int)> CellsOf(PlanterBrain brain) =>
        new HashSet<(int, int)>(brain.OccupiedGrids.Select(g => { var p = g.GetGroundCellCached().GetGridPosition(); return (p.x - minX, p.z - minZ); }));

    // Kaynak saksı (patlama tile'ı, şans %100) + kalan bütün hücrelerde 1×1 saksı; hepsinde canı yüksek bitki, kaynağınkiler 1 canla.
    static void BuildSource(int sx, int sz, int ox, int oz)
    {
        RewardOfferLab.ClearField();
        field.Clear();
        source = Place(sx, sz, ox, oz, "Explosive");
        sourceCells = CellsOf(source);
        Must(sourceCells.Count == sx * sz && sourceCells.Contains((ox, oz)), $"source planter covers {sourceCells.Count} cells");
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++)
            if (!sourceCells.Contains((x, z))) field.Add(Place(1, 1, x, z));
        foreach (var brain in field) Respawn(brain, 1000000);
        Respawn(source, 1000000);
        SetF(PlantOf(source.OccupiedGrids[0]), "currentHealth", 1);
        hits.Clear(); harvests.Clear();
    }

    static void Kill(PlanterBrain brain) => Call(Player, "AttackInRadius", brain.OccupiedGrids[0].GetGroundCellCached().transform.position);
    static HashSet<(int, int)> Cells(IEnumerable<Hit> list) => new HashSet<(int, int)>(list.Select(h => (h.X, h.Z)));

    // Testin kendi geometrisi: ayak izindeki bir hücrenin merkezine radius içinde, tarlanın içinde, ayak izinin dışında.
    static HashSet<(int, int)> Around(HashSet<(int, int)> footprint, float radius)
    {
        var set = new HashSet<(int, int)>();
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++)
        {
            if (footprint.Contains((x, z))) continue;
            foreach (var f in footprint)
                if ((x - f.Item1) * (x - f.Item1) + (z - f.Item2) * (z - f.Item2) <= radius * radius + 1e-3f) { set.Add((x, z)); break; }
        }
        return set;
    }
    static int Perimeter(HashSet<(int, int)> region) =>
        region.Sum(c => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Count(d => !region.Contains((c.Item1 + d.Item1, c.Item2 + d.Item2))));
    static HashSet<(int, int)> LastRegion() => new HashSet<(int, int)>(Area.LastCells.Select(p => (p.x - minX, p.z - minZ)));

    // Son dalganın çizgisi, tam boyda (ölçek 1): her köşe noktası bölgedeki bir hücrenin içinde mi? Şeritlerin uçları hücre
    // köşesine denk gelir; nokta, kendi şeridinin ortasına doğru 0,05 birim kaydırılarak hücreye atanır.
    static int VerticesOutside(HashSet<(int, int)> region, out int total)
    {
        var mesh = Area.LastWave.GetComponentInChildren<MeshFilter>().sharedMesh;
        var grid = GridManager.Instance.GetGridSystem();
        var verts = mesh.vertices; total = verts.Length;
        int outside = 0;
        for (int q = 0; q + 3 < verts.Length; q += 4)
        {
            Vector3 mid = (verts[q] + verts[q + 1] + verts[q + 2] + verts[q + 3]) * .25f;
            for (int k = 0; k < 4; k++)
            {
                Vector3 v = verts[q + k];
                v += (mid - v).normalized * .05f;
                var p = grid.GetGridPosition(Area.LastWave.position + new Vector3(v.x, 0f, v.z));
                if (!region.Contains((p.x - minX, p.z - minZ))) outside++;
            }
        }
        return outside;
    }

    // ---------------------------------------------------------------- A) alan ve gerçek darbe
    static string caseName; static BossRewardSO caseReward; static int played0, skipped0;

    static double AreaCase(string name, int sx, int sz, int ox, int oz, BossRewardSO reward)
    {
        caseName = name; caseReward = reward;
        BuildSource(sx, sz, ox, oz);
        Boss.ClearAll();
        Must(Grant(reward), "Artçı not granted");
        played0 = Area != null ? Area.AftershockPlayed : 0; skipped0 = Area != null ? Area.AftershockSkipped : 0;
        planFrame = applyFrame = -1;
        hits.Clear();
        Kill(source);
        var first = hits.Where(h => h.Type == DamageType.Explosion && !h.Echo).ToList();
        Must(Cells(first).SetEquals(Around(sourceCells, 1f)) && first.All(h => h.Damage == 100) && Echoes.Pending == 1 && planFrame > 0 &&
             (Area == null || Area.AftershockPlayed == played0) && playedAtPlan == played0,
            $"{name}: the first blast hit {first.Count} cells and drew no area outline");
        return reward.echoDelay + .9;
    }

    static double AreaResult()
    {
        float radius = CellsOf(caseReward);
        var echo = hits.Where(h => h.Echo).ToList();
        var expected = Around(sourceCells, radius);
        var region = LastRegion();
        var wanted = new HashSet<(int, int)>(sourceCells); wanted.UnionWith(expected);
        int outside = VerticesOutside(region, out int vertices);
        int perimeter = Perimeter(region);
        Require(applyFrame > 0 && Area.AftershockPlayed == played0 + 1 && playedAtApply == played0 + 1 && Area.AftershockSkipped == skipped0 &&
                region.SetEquals(wanted) && Cells(echo).SetEquals(expected) && echo.Count == expected.Count && echo.All(h => h.Damage == 100) &&
                Area.LastEdgeCount == perimeter && outside == 0 && vertices == perimeter * 4 && Echoes.Pending == 0,
            $"Aftershock area · {caseName}: one outline, drawn in the frame of the second blast (frame {applyFrame}, scheduled at {planFrame}); its region is the planter " +
            $"({sourceCells.Count}) + the {expected.Count} cells the blast really hit ({radius:0.00} cells, field edge clipped) = {region.Count} cells, {perimeter} border edges; " +
            $"all {vertices} outline vertices lie on those cells; damage unchanged ({echo.Count} hits of 100, each cell once)");
        return .05;
    }

    static double OldVersusNew()
    {
        var one = new HashSet<(int, int)> { (C, C) };
        int oldCells = Around(one, CellsOf(OldArtci)).Count + 1, newCells = Around(one, CellsOf(Artci)).Count + 1;
        Require(Near(CellsOf(OldArtci), 1.5f) && Near(CellsOf(Artci), 2f, .001f) && oldCells == 9 && newCells == 13,
            $"The outline follows each profile's own reward: old Artçı (Kırılma V1 asset, ×{OldArtci.echoRadius}) {CellsOf(OldArtci):0.00} cells → a 3×3 block of {oldCells} cells; " +
            $"new variant (×{Artci.echoRadius:0.######}) {CellsOf(Artci):0.00} cells → {newCells} cells; nothing is fixed to 2,00 in the visual");
        return .05;
    }

    // ---------------------------------------------------------------- B) uygulama
    static readonly (int x, int z)[] PoolSources = { (1, 1), (1, 5), (1, 9), (5, 1), (5, 5), (5, 9), (9, 1), (9, 5), (9, 9), (3, 7) };
    static HashSet<(int, int)> poolSet;

    static double PoolFullCase()
    {
        RewardOfferLab.ClearField();
        field.Clear();
        poolSet = new HashSet<(int, int)>(PoolSources);
        var sources = PoolSources.Select(p => Place(1, 1, p.x, p.z, "Explosive")).ToList();
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) if (!poolSet.Contains((x, z))) field.Add(Place(1, 1, x, z));
        foreach (var brain in field) Respawn(brain, 1000000);
        foreach (var brain in sources) { Respawn(brain, 1000000); SetF(PlantOf(brain.OccupiedGrids[0]), "currentHealth", 1); }
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        played0 = Area.AftershockPlayed; skipped0 = Area.AftershockSkipped;
        hits.Clear(); harvests.Clear();
        foreach (var brain in sources) Kill(brain);
        Must(Echoes.Pending == PoolSources.Length, $"{Echoes.Pending} aftershocks pending, expected {PoolSources.Length}");
        return Artci.echoDelay + .1;
    }

    static double PoolFullResult()
    {
        if (Echoes.Pending > 0) return Again;
        int expected = PoolSources.Sum(p => Around(new HashSet<(int, int)> { p }, CellsOf(Artci)).Count(c => !poolSet.Contains(c)));
        var echo = hits.Where(h => h.Echo).ToList();
        int drawn = Area.AftershockPlayed - played0, skipped = Area.AftershockSkipped - skipped0;
        Require(drawn == AreaOutlineFeedback.Capacity && skipped == PoolSources.Length - AreaOutlineFeedback.Capacity && echo.Count == expected && echo.All(h => h.Damage == 100) &&
                Area.ActiveCount <= AreaOutlineFeedback.Capacity,
            $"Visual pool full: {PoolSources.Length} aftershocks land in the same frame; {drawn} outlines drawn, {skipped} not drawn (capacity {AreaOutlineFeedback.Capacity}) — " +
            $"all {echo.Count} aftershock hits are still applied (expected {expected}); the pool does not grow");
        return .6;
    }

    static double Allocation()
    {
        var grid = GridManager.Instance.GetGridSystem();
        var footprint = new List<GridPosition>();
        for (int x = 0; x < 2; x++) for (int z = 0; z < 3; z++) footprint.Add(new GridPosition(minX + 4 + x, minZ + 4 + z));
        var targets = new List<GridPosition>();
        HarvestBehaviorGeometry.ExplosionCells(footprint, CellsOf(Artci), targets);
        for (int i = 0; i < 20; i++) { Area.Clear(); AreaOutlineFeedback.PlayAftershock(grid, footprint, targets); }
        const int runs = 500;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++) { Area.Clear(); AreaOutlineFeedback.PlayAftershock(grid, footprint, targets); }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Area.Clear();
        double micro = watch.Elapsed.TotalMilliseconds * 1000.0 / runs;
        Require(allocated == 0 && Area.LastCells.Count == footprint.Count + targets.Count && Area.ActiveCount == 0,
            $"No allocation per aftershock: {runs} outlines for a 2×3 planter at 2,00 cells ({Area.LastCells.Count} cells, {Area.LastEdgeCount} edges) allocated {allocated} bytes; " +
            $"building one outline costs {micro:0.0} µs on this machine (batch, mesh build only)");
        return .05;
    }

    // ---------------------------------------------------------------- C) görüntüler
    static int killFrame;

    static double WaitRecorder()
    {
        if (recorder != null && (recorder.plan.Count > 0 || echoShots.Count > 0)) return Again;
        if (recorder != null) foreach (var l in recorder.late) Note("görüntü kare kayması: " + l);
        recorder?.late.Clear();
        Time.captureDeltaTime = 0f;
        return .05;
    }

    static Vector3 Focus => sourceCells.Aggregate(Vector3.zero, (s, c) => s + PosAt(c.Item1, c.Item2)) / sourceCells.Count;

    static double SequenceSetup()
    {
        BuildSource(1, 1, C, C);
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        Time.captureDeltaTime = Step;
        var focus = Focus;
        Capture("T3751_01_Dizi_0_vurustan_once", focus);
        Kill(source);
        killFrame = Time.frameCount;
        Rec.plan.Add((killFrame + 1, "ilk darbe", () => Capture("T3751_01_Dizi_1_ilk_darbe", focus)));
        Rec.plan.Add((killFrame + 3, "bekleme", () => { Must(Area == null || Area.ActiveCount == 0, "no outline during the wait"); Capture("T3751_01_Dizi_2_bekleme", focus); }));
        echoShots.Add((0, "ikinci darbe", () => { Must(Area.ActiveCount == 1 && Area.LastWave.localScale.x < .6f, "outline should start small in the blast frame"); Capture("T3751_01_Dizi_3_ikinci_darbe", focus); }));
        echoShots.Add((2, "yayılma", () => Capture("T3751_01_Dizi_4_yayilma", focus)));
        echoShots.Add((5, "sınır", () => { Must(Near(Area.LastWave.localScale.x, 1f, .001f), "outline should have reached the border"); Capture("T3751_01_Dizi_5_sinir", focus); }));
        echoShots.Add((9, "bulut sonrası", () => { Must(Area.ActiveCount == 1, "outline should still be on after the clouds"); Capture("T3751_01_Dizi_6_bulut_sonrasi", focus); }));
        echoShots.Add((14, "sönme", () => Capture("T3751_01_Dizi_7_sonme", focus)));
        echoShots.Add((19, "temizlik", () => { Must(Area.ActiveCount == 0, "outline should be gone after 0,6 s"); Capture("T3751_01_Dizi_8_temizlik", focus); }));
        return .05;
    }

    static IEnumerable<(string name, int sx, int sz, int ox, int oz, Func<BossRewardSO> reward, bool blank)> Shots() => new (string, int, int, int, int, Func<BossRewardSO>, bool)[]
    {
        ("T3751_02_Eski150_orta_1x1", 1, 1, C, C, () => OldArtci, false),
        ("T3751_03_Yeni200_orta_1x1", 1, 1, C, C, () => Artci, true),
        ("T3751_04_Eski150_orta_2x3", 2, 3, 4, 4, () => OldArtci, false),
        ("T3751_05_Yeni200_orta_2x3", 2, 3, 4, 4, () => Artci, true),
        ("T3751_06_Yeni200_kenar_1x1", 1, 1, 0, C, () => Artci, false),
        ("T3751_07_Yeni200_kose_2x2", 2, 2, 0, 0, () => Artci, false),
    };

    // Sınır karesi (ikinci darbeden 5 kare sonra, çizgi tam boyda). blank: aynı karenin çizgisiz hâli de ("önce").
    static double ShotCase(string name, int sx, int sz, int ox, int oz, BossRewardSO reward, bool blank)
    {
        BuildSource(sx, sz, ox, oz);
        Boss.ClearAll();
        Must(Grant(reward), "Artçı not granted");
        Time.captureDeltaTime = Step;
        var focus = Focus;
        Kill(source);
        echoShots.Add((5, name, () =>
        {
            Capture(blank ? name + "_sonra" : name, focus);
            if (!blank) return;
            foreach (var r in Area.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            Capture(name + "_once", focus);
            Area.Clear();
        }));
        return .05;
    }

    static SegmentEventRuntime boss;
    static double BossShot(bool frost)
    {
        BuildSource(1, 1, 4, 6);
        var events = SegmentEventDirector.Instance;
        var data = E1.bossPool.entries.Select(e => e.boss).First(b => frost ? b is FrostFrontSO : b is HardShellSO);
        boss = data.CreateRuntime(SegmentEventTiming.BossRound(1, 1, RM.CurrentRound, 81));
        var context = new SegmentEventContext { GridWidth = GridManager.Instance.GetWidth(), GridHeight = GridManager.Instance.GetHeight(), PlayerRadius = 2f };
        foreach (var ground in RewardOfferLab.OpenCells()) context.OpenCells.Add(ground.GetGridPosition());
        boss.TryPrepare(context);
        F<List<SegmentEventRuntime>>(events, "events").Add(boss);
        boss.Activate(); SetP(events, "Active", boss); SetP(events, "Version", events.Version + 1);
        var overlay = BossWeatherOverlay.Ensure();
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        Time.captureDeltaTime = Step;
        var focus = Focus;
        string name = frost ? "T3751_08_Don_dolu_tarla" : "T3751_09_SertKabuk_dolu_tarla";
        Kill(source);
        // Hava efekti 0,7 sn'de açılır: görüntüden iki kare önce tam güce getirilir (yalnız sunum; oyun zamanı ilerletilmez).
        echoShots.Add((3, "hava tam", () => SetF(overlay, "fade", 1f)));
        echoShots.Add((5, name, () =>
        {
            Must(overlay.CurrentWeather != BossWeatherOverlay.Weather.None, "boss weather should be on");
            Capture(name, focus, 9f);
        }));
        return .05;
    }

    static double EndBoss()
    {
        var events = SegmentEventDirector.Instance;
        boss.Finish(); SetP(events, "Active", null); F<List<SegmentEventRuntime>>(events, "events").Remove(boss);
        SetP(events, "Version", events.Version + 1);
        return .3;
    }

    static double ShotSummary()
    {
        Require(captures == 9 + 6 + 2 + 2, $"{captures} real-render images written (sequence 9, comparison 6, the same frame without the outline 2, boss weather 2); every frame came from the real game loop");
        return .05;
    }

    // ---------------------------------------------------------------- D) Hasat Ritmi
    static int Counter => Rhythm.Count;
    static int RitimN => Ritim.rhythmHarvests;

    static void RhythmField(params (int x, int z, string tile, int health)[] plants)
    {
        RewardOfferLab.ClearField();
        field.Clear();
        foreach (var (x, z, tile, health) in plants)
        {
            var brain = Place(1, 1, x, z, tile);
            Respawn(brain, health);
            field.Add(brain);
        }
        hits.Clear(); harvests.Clear();
    }
    static void Attack(Vector3 at) => Call(Player, "AttackInRadius", at);
    static Vector3 Between(int x0, int z0, int x1, int z1) => (PosAt(x0, z0) + PosAt(x1, z1)) * .5f;

    static double RhythmBehaviorKill()
    {
        Boss.ClearAll(); Rhythm.Clear();
        Must(Grant(Ritim) && Rhythm.Enabled && Rhythm.Threshold == RitimN && Counter == 0, "Hasat Ritmi not granted");
        // A (patlama, 1 can) solda, B (1 can) sağda; saldırı ikisine de değer. Hedefler x sırasıyla gezilir: önce A.
        RhythmField((4, C, "Explosive", 1), (5, C, null, 1));
        Strike(40f, .4f);
        Attack(Between(4, C, 5, C));
        var onB = hits.Where(h => (h.X, h.Z) == (5, C)).ToList();
        Require(harvests.Count == 2 && harvests.Count(h => h.type == DamageType.Direct) == 1 && harvests.Any(h => (h.x, h.z) == (5, C) && h.type == DamageType.Explosion) &&
                onB.Count == 1 && onB[0].Type == DamageType.Explosion && Counter == 1 && !Rhythm.Ready,
            "Direct harvest counts, behavior harvest does not: one swing touches A and B; A dies to the hit, its explosion harvests B before the swing reaches B; " +
            "B is gone from the cell, so the swing does not hit it again — two harvests, counter +1 (the dead plant is never counted twice)");
        return .05;
    }

    static double RhythmOrder()
    {
        // Ters sıra: önce düz bitki (doğrudan), sonra patlayan saksı (doğrudan). İkisi de sayılır.
        RhythmField((4, C, null, 1), (5, C, "Explosive", 1));
        Attack(Between(4, C, 5, C));
        Require(harvests.Count(h => h.type == DamageType.Direct) == 2 && Counter == 3,
            $"Both plants hit directly before any explosion reaches them: counter +2 (now {Counter} / {RitimN}); the count follows how each plant really died");
        return .05;
    }

    static double RhythmEchoCase()
    {
        // Kaynak ortada; ilk patlamanın komşuları boş; yalnız artçının eriştiği dört hücrede 1 canlı bitki.
        RhythmField((C, C, "Explosive", 1), (C + 2, C, null, 1), (C - 2, C, null, 1), (C, C + 2, null, 1), (C, C - 2, null, 1));
        Must(Grant(Artci), "Artçı not granted");
        Strike(40f, .4f);
        Attack(PosAt(C, C));
        Must(Counter == 4 && Echoes.Pending == 1, $"source harvest should count (counter {Counter})");
        return Artci.echoDelay + .5;
    }

    static double RhythmEchoResult()
    {
        int echoKills = harvests.Count(h => h.echo);
        Require(echoKills == 4 && Counter == 4 && !Rhythm.Ready,
            $"Aftershock harvests do not count: the aftershock harvested {echoKills} plants, the counter stays at {Counter} / {RitimN}");
        Boss.ClearAll();
        Must(Grant(Ritim), "Hasat Ritmi not granted again");
        return .05;
    }

    static double RhythmThreshold()
    {
        // Sayaç 4 / 5. Tek saldırı üç bitkiyi doğrudan hasat eder: eşik saldırının ortasında aşılır.
        Must(Counter == 4 && !Rhythm.Ready, $"counter should be 4 (is {Counter})");
        RhythmField((4, C, null, 1), (5, C, null, 1), (6, C, null, 1));
        Strike(40f, 1f);
        int charges = Rhythm.Charges;
        Attack(PosAt(5, C));
        var direct = hits.Where(h => h.Type == DamageType.Direct).ToList();
        int lowNormal = Mathf.RoundToInt(40f * .85f), highNormal = Mathf.RoundToInt(40f * 1.15f);
        Require(direct.Count == 3 && direct.All(h => h.Damage >= lowNormal && h.Damage <= highNormal) && Near(Player.LastAttackRadius, 1f) &&
                Rhythm.Ready && Counter == 0 && Rhythm.Charges == charges + 1,
            $"Threshold crossed in the middle of a swing (4 + 3 direct harvests): every target of that swing got normal damage ({string.Join(" / ", direct.Select(h => h.Damage))}, normal {lowNormal}–{highNormal}) " +
            "and normal radius; afterwards exactly one charge is ready and the counter is 0 (the surplus of 2 is not banked)");
        return .05;
    }

    static double RhythmEmpowered()
    {
        RhythmField((4, C, null, 1000000), (5, C, null, 1000000), (6, C, null, 1000000));
        int empowered = Rhythm.EmpoweredAttacks;
        Attack(PosAt(5, C));
        var direct = hits.Where(h => h.Type == DamageType.Direct).ToList();
        int low = Mathf.RoundToInt(40f * .85f * Ritim.rhythmDamage) - 1, high = Mathf.RoundToInt(40f * 1.15f * Ritim.rhythmDamage) + 1;
        Require(direct.Count == 3 && direct.All(h => h.Damage >= low && h.Damage <= high) && Near(Player.LastAttackRadius, Ritim.rhythmRadius) &&
                Rhythm.EmpoweredAttacks == empowered + 1 && !Rhythm.Ready && Counter == 0,
            $"Empowered swing: decided once at the start; all three targets got the strong damage ({string.Join(" / ", direct.Select(h => h.Damage))}, range {low}–{high}) and the radius ×{Ritim.rhythmRadius}; " +
            "the charge is spent once for the whole swing, not per target");
        return .05;
    }

    static void ChargeUp()
    {
        var plants = new List<(int, int, string, int)>();
        for (int x = 3; x <= 7; x++) plants.Add((x, C, null, 1));
        RhythmField(plants.ToArray());
        Strike(40f, 5f);
        Attack(PosAt(C, C));
        Strike(40f, 1f);
        Must(Rhythm.Ready, "charge should be ready");
    }

    static double RhythmEmptySwing()
    {
        ChargeUp();
        RewardOfferLab.ClearField();
        int empowered = Rhythm.EmpoweredAttacks;
        Attack(PosAt(C, C));
        Require(Rhythm.Ready && Rhythm.EmpoweredAttacks == empowered,
            "A swing that touches no living plant does not spend the charge (existing rule, unchanged)");
        return .05;
    }

    static float RingRadius(Vector3 center)
    {
        var line = F<LineRenderer>(Player, "radiusIndicator");
        var points = new Vector3[line.positionCount]; line.GetPositions(points);
        return points.Average(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(center.x, center.z)));
    }

    static void ShowCursor(Vector3 center)
    {
        var player = Player;
        if (F<GridSystem>(player, "gridSystem") == null) SetF(player, "gridSystem", GridManager.Instance.GetGridSystem());
        var line = F<LineRenderer>(player, "radiusIndicator"); if (line != null) line.enabled = true;
        var cursor = F<HarvestCursorVisual>(player, "cursorVisual"); if (cursor != null) cursor.gameObject.SetActive(true);
        Call(player, "UpdateRadiusVisual", center);
        // Test imleci bir anda taşır: imlecin hareket izi (oyunda fareyi izler) temizlenir.
        if (cursor != null) F<TrailRenderer>(cursor, "trail")?.Clear();
    }

    static Vector3 aim;
    static double RhythmVisuals()
    {
        // Normal: hak yok.
        Rhythm.Clear();
        var plants = new List<(int, int, string, int)>();
        for (int x = 3; x <= 7; x++) for (int z = 3; z <= 7; z++) plants.Add((x, z, null, 1000000));
        RhythmField(plants.ToArray());
        Strike(40f, 1.5f);
        aim = PosAt(C, C);
        ShowCursor(aim);
        float normal = RingRadius(aim);
        Must(!Player.ShowsChargedCursor && Near(normal, 1.5f, .01f), $"normal cursor ring {normal}");
        Capture("T3751_10_Ritim_normal", aim, 6f);
        // Hazır: hak oyunun yoluyla dolar (beş doğrudan hasat), sonra aynı blok yeniden kurulur.
        ChargeUp();
        RhythmField(plants.ToArray());
        Strike(40f, 1.5f);
        ShowCursor(aim);
        float ready = RingRadius(aim);
        Require(Player.ShowsChargedCursor && Near(ready, 1.5f * Ritim.rhythmRadius, .01f) && !Near(ready, normal, .1f),
            $"Cursor shows the next swing's real contact area: normal ring {normal:0.##}, with a ready charge {ready:0.##} (= {1.5f} × {Ritim.rhythmRadius}) in gold");
        Capture("T3751_11_Ritim_hazir", aim, 6f);
        // Güçlü vuruş ve vuruş halkası (oyunun saldırı yolu: AttackInRadius + PlayAttackRing, HandleAutoAttack'teki gibi).
        Time.captureDeltaTime = Step;
        bool crit = (bool)Call(Player, "AttackInRadius", aim);
        VFXManager.Instance.PlayAttackRing(aim, GridManager.Instance.GetGridSystem().HarvestReach(Player.LastAttackRadius), crit);
        Must(!Rhythm.Ready && Near(Player.LastAttackRadius, 1.5f * Ritim.rhythmRadius), "empowered swing should spend the charge");
        int frame = Time.frameCount;
        Rec.plan.Add((frame + 3, "güçlü vuruş halkası", () => { ShowCursor(aim); Capture("T3751_12_Ritim_guclu_vurus", aim, 6f); }));
        return .05;
    }

    static double RhythmVisualsAfter()
    {
        ShowCursor(aim);
        float after = RingRadius(aim);
        Require(!Player.ShowsChargedCursor && Near(after, 1.5f, .01f) && !F<HarvestCursorVisual>(Player, "cursorVisual").Charged,
            $"After the empowered swing the cursor is normal again (ring {after:0.##}, no gold)");
        Capture("T3751_13_Ritim_normale_donus", aim, 6f);
        F<LineRenderer>(Player, "radiusIndicator").enabled = false;
        F<HarvestCursorVisual>(Player, "cursorVisual").gameObject.SetActive(false);
        ClearTestMods();
        Strike(100f, .4f);
        return .05;
    }

    // ---------------------------------------------------------------- B) temizlik
    static double RoundEndCase()
    {
        BuildSource(1, 1, C, C);
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        applyFrame = -1;
        Kill(source);
        return .01;
    }

    static double RoundEndApply()
    {
        if (applyFrame < 0) return Again;
        Must(Area.ActiveCount == 1, $"outline should be on screen ({Area.ActiveCount})");
        SetP(RM, "RemainingTime", 0f);
        return .3;
    }

    static double RoundEndResult()
    {
        bool anyVisible = Area.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled);
        Require(!RM.IsRoundActive && Area.ActiveCount == 0 && !anyVisible,
            "Round ends while an outline is on screen: it is removed at once (no renderer left on); the same rule covers the menu (state is no longer Round)");
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested; BehaviorEchoes.Traced -= OnEcho;
        ClearTestMods();
        return .05;
    }

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != E1) return Again;
        Player.enabled = false;
        bool leftover = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.name.StartsWith("Aftershock"));
        Require(AreaOutlineFeedback.Instance == null && !leftover && Rhythm.Count == 0 && !Rhythm.Ready && !Rhythm.Enabled && !Rhythm.Next().Empowered,
            "Scene change / new run: no aftershock outline object survives; the rhythm counter and charge are reset and the reward is gone");
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0, "No error or exception logged during the test");
        return .05;
    }
}
