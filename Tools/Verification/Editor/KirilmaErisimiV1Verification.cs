using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.5 İŞLEV testi — kırılma ödüllerinin erişimi (Run50_KirilmaErisimiV1).
// Denge ölçümü DEĞİLDİR: tarla testin kurduğu sabit düzendir, bitkilerin canı elle verilir, vuruşlar test eliyle yapılır.
// A) Veri: profil ayrımı, varyantların yalnız erişim alanında ayrışması, eski asset ve havuzların değişmemesi, kart metinleri.
// B) Geometri: patlama yarıçapları ve elektrik erişimleri (alan sınırı, köşe, çok hücreli saksı, tekilleştirme).
// C) Gerçek vuruşlar: ilk darbe eski hâlinde; ikinci darbe yeni alanda, hedef başına bir kez; eski ödül eski erişimde.
// D) Uygulama sınırları: çizim kapasitesi ve görsel havuzu dolunca hasar korunur.
// E) Yaşam döngüsü: havuzdan yeniden doğan bitki, round sonu, yeni run; yankı gecikmesinin kare sayısı (mutlak zamandan bağımsız).
// F) Katsayılar: Davranışa Adanış ve uzmanlaşma ile hesaplanmış yankı hasarı.
// G) Ödül ekranı: yeni varyantların kart yazıları.
[InitializeOnLoad]
public static class KirilmaErisimiV1Verification
{
    const string Key = "KirilmaErisimiV1Verification";
    const string LogFile = "Logs/KirilmaErisimiV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string MenuPath = "Tools/Run Profili/Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)";
    const int Seed = 3755, N = 11, C = 5;   // 11×11 açık tarla; orta hücre (5,5)
    const double Again = double.NaN;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static KirilmaErisimiV1Verification() { EditorApplication.update += Tick; }

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
            if (double.IsNaN(wait)) { nextAt = now + .02; return; }
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
        PlantHealth.AnyDamaged -= OnDamaged; BehaviorEchoes.Traced -= OnEcho;
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
    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "").Replace("\n", " / ");
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static SpecializationManager Spec => SpecializationManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO E1 => Profile("Run50_KirilmaErisimiV1");
    static RunProfileSO B1 => Profile("Run50_BedelliOdullerV1");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static BossRewardPoolSO Pool => E1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardPanelUI Panel => Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
    static BossRewardSO Find(BossRewardPoolSO pool, string id) => RewardOfferLab.All(pool).FirstOrDefault(r => r.id == id);
    static BossRewardSO Artci => Find(Pool, "artci_patlama");
    static BossRewardSO Cifte => Find(Pool, "cifte_akim");
    static BossRewardSO OldArtci => Find(K1.bossRewards, "artci_patlama");
    static BossRewardSO OldCifte => Find(K1.bossRewards, "cifte_akim");
    static bool Grant(BossRewardSO reward) => RewardOfferLab.Grant(reward);
    static float ArtciCells => HarvestBehaviorGeometry.ExplosionRadiusCells * Artci.echoRadius;
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
    static void InsertNext(params (string name, Func<double> run)[] items)
    {
        var rest = steps.ToList(); steps.Clear();
        steps.Enqueue(rest[0]);
        foreach (var item in items) steps.Enqueue(item);
        foreach (var step in rest.Skip(1)) steps.Enqueue(step);
    }

    static RenderTexture target;
    static void Capture(string name)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        var roots = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        foreach (var canvas in roots.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var old = cam.targetTexture;
        cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG());
        Object.DestroyImmediate(png);
        Note("görüntü: Logs/" + name + ".png");
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("veri", Data);
        Add("geometri", Geometry);
        Add("tarla ve round", LabRound);
        // C) patlama
        Add("artçı: orta, 1×1", () => ExplosionCase("orta 1×1", 1, 1, C, C, Artci));
        Add("artçı sonucu", () => ExplosionResult(Artci));
        Add("artçı: köşe, 1×1", () => ExplosionCase("köşe 1×1", 1, 1, 0, 0, Artci));
        Add("artçı sonucu", () => ExplosionResult(Artci));
        Add("artçı: kenar, 2×2", () => ExplosionCase("kenar 2×2", 2, 2, 0, 4, Artci));
        Add("artçı sonucu", () => ExplosionResult(Artci));
        Add("artçı: orta, 2×3", () => ExplosionCase("orta 2×3", 2, 3, 4, 4, Artci));
        Add("artçı sonucu", () => ExplosionResult(Artci));
        Add("eski artçı: orta, 1×1", () => ExplosionCase("eski ödül, orta 1×1", 1, 1, C, C, OldArtci));
        Add("eski artçı sonucu", () => ExplosionResult(OldArtci));
        // C) elektrik
        foreach (int reach in new[] { 0, 3, 4 })
        {
            int r = reach;
            Add($"ikinci dalga {r}: orta, 1×1", () => ElectricCase("orta 1×1", 1, 1, C, C, r));
            Add("ikinci dalga sonucu", () => ElectricResult(r));
        }
        Add("ikinci dalga 4: köşe, 1×1", () => ElectricCase("köşe 1×1", 1, 1, 0, 0, 4));
        Add("ikinci dalga sonucu", () => ElectricResult(4));
        Add("ikinci dalga 4: orta, 2×2", () => ElectricCase("orta 2×2", 2, 2, 4, 4, 4));
        Add("ikinci dalga sonucu", () => ElectricResult(4));
        Add("ikinci dalga 6: çizim kapasitesi", () => ElectricCase("orta 1×1, erişim 6", 1, 1, C, C, 6));
        Add("çizim kapasitesi sonucu", BoltCapacity);
        Add("görsel havuzu dolu", VisualPoolFull);
        // E) yaşam döngüsü
        Add("havuzdan yeniden doğan bitki", PooledPlant);
        Add("havuz sonucu", PooledPlantResult);
        Add("yankı gecikmesi: sabit adım", () => DelayFrames(0));
        for (int i = 1; i <= 6; i++) { int k = i; Add("yankı gecikmesi " + k, () => DelayFrames(k)); }
        Add("yankı gecikmesi sonucu", DelayResult);
        // F) katsayılar
        Add("katsayılar: Adanış", CoefficientCase);
        Add("katsayılar sonucu", CoefficientResult);
        Add("round sonu: bekleyen artçı", RoundEndCase);
        Add("round sonu sonucu", RoundEndResult);
        // G) ödül ekranı
        Add("sahne yeniden", () => Load(E1));
        Add("run başı temiz", CleanStart);
        for (int r = 1; r <= 3; r++)
        {
            int round = r;
            Add($"R{r} başla", () => StartRound(round));
            Add($"R{r} süre biter", () => { SetP(RM, "RemainingTime", 0f); return .03; });
            Add($"R{r} bitiş", () => AfterRound(round));
        }
        Add("eski profil sahnesi", () => Load(K1));
        Add("eski profil", Legacy);
        Add("kapanış", FinalCheck);
    }

    // ---------------------------------------------------------------- A) veri
    static bool SameExceptReach(BossRewardSO a, BossRewardSO b) =>
        a.id == b.id && a.displayName == b.displayName && a.effectLabel == b.effectLabel && a.note == b.note && a.echo == b.echo && a.echoDelay == b.echoDelay &&
        a.echoDamage == b.echoDamage && a.maxStacks == b.maxStacks && a.weight == b.weight && a.condition == b.condition && a.conditionStat == b.conditionStat &&
        a.modifiersPerStack.Count == 0 && b.modifiersPerStack.Count == 0 && a.directDamageMultiplier == b.directDamageMultiplier &&
        a.behaviorDamageMultiplier == b.behaviorDamageMultiplier && a.rareDirectMultiplier == b.rareDirectMultiplier && a.rhythmHarvests == b.rhythmHarvests &&
        a.levelChoiceDelta == b.levelChoiceDelta && (a.exclusiveGroup ?? "") == (b.exclusiveGroup ?? "");

    static double Data()
    {
        var e = E1; var b = B1; var pool = e.bossRewards; var old = b.bossRewards;
        Require(e != null && e != b && pool != null && pool != old && BossRewardPoolSO.Validate(pool, e.runLength) == null && RunCalendar.Validate(e) == null,
            "Run50_KirilmaErisimiV1 is a separate profile with its own (valid) reward pool asset");
        Require(e.bossCalendar.Select(d => d.round).SequenceEqual(b.bossCalendar.Select(d => d.round)) && e.segmentTargets.SequenceEqual(b.segmentTargets) && e.bossTargets.SequenceEqual(b.bossTargets) &&
                e.runLength == b.runLength && e.balance == b.balance && e.bossPool == b.bossPool && e.choicesPerLevel == b.choicesPerLevel && e.startingGold == b.startingGold &&
                e.fixedRoundDuration == b.fixedRoundDuration && e.debugBudget == b.debugBudget && e.specializationAfterSegment == 0,
            "Everything except the reward pool is Bedelli Ödüller V1's: 15 boss dates, quota and boss targets, balance set (XP, economy, plant health, tree: same asset), boss pool, 3 choices per level, start budget, round duration");
        var artci = Artci; var cifte = Cifte; var oldArtci = OldArtci; var oldCifte = OldCifte;
        var mine = RewardOfferLab.All(pool); var theirs = RewardOfferLab.All(old);
        var changed = new List<BossRewardSO> { oldArtci, oldCifte };
        Require(pool.IsStaged && pool.stages.Select(s => s.firstRound).SequenceEqual(old.stages.Select(s => s.firstRound)) && pool.stageDistanceWeights.SequenceEqual(old.stageDistanceWeights) &&
                pool.reserveCurrentStageSlot == old.reserveCurrentStageSlot && pool.choices == old.choices &&
                theirs.Where(r => !changed.Contains(r)).All(mine.Contains) && !mine.Contains(oldArtci) && !mine.Contains(oldCifte) &&
                mine.Count == theirs.Count - (cifte == null ? 1 : 0),
            $"Pool: same stages and offer policy; the other {theirs.Count - 2} rewards are the very same assets (Hasat Ritmi and the two cost rewards included); " +
            $"Artçı Patlama {(cifte != null ? "and Çifte Akım are" : "is")} replaced by this profile's own variant{(cifte == null ? "; Çifte Akım is not in this pool" : "s")}");
        Require(artci != null && artci != oldArtci && SameExceptReach(artci, oldArtci) && artci.echoReach == 0 && oldArtci.echoRadius == 1.25f && oldArtci.echoReach == 0 &&
                (Near(ArtciCells, 2f) || Near(ArtciCells, 2.25f)) && pool.StageIndexOf(artci) == 2,
            $"Artçı Patlama variant: only the second blast's radius differs — {ArtciCells:0.00} cells (data multiplier ×{artci.echoRadius:0.###} of the {HarvestBehaviorGeometry.ExplosionRadiusCells} cell base); " +
            "the Kırılma V1 asset still has ×1,25 (1,50 cells); delay, damage ratio, trigger condition, name and note are identical");
        if (cifte != null)
            Require(cifte != oldCifte && SameExceptReach(cifte, oldCifte) && cifte.echoRadius == oldCifte.echoRadius && (cifte.echoReach == 3 || cifte.echoReach == 4) && oldCifte.echoReach == 0 &&
                    pool.StageIndexOf(cifte) == 2,
                $"Çifte Akım variant: only the second wave's reach differs — {cifte.echoReach} cells per diagonal; the Kırılma V1 asset keeps reach 0 (= first wave, {HarvestBehaviorGeometry.ElectricReachCells} cells)");
        else
            Require(oldCifte.echoReach == 0 && RewardOfferLab.All(K1.bossRewards).Contains(oldCifte) && RewardOfferLab.All(B1.bossRewards).Contains(oldCifte),
                "Çifte Akım is left out of this profile's pool only: the asset and the older pools still hold it unchanged");
        var all = AssetDatabase.FindAssets("t:BossRewardSO", new[] { "Assets/ScriptableObjects/BossRewards" })
            .Select(g => AssetDatabase.LoadAssetAtPath<BossRewardSO>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var others = all.Where(r => r != artci && r != cifte).ToList();
        Require(others.Count >= 24 && others.All(r => r.echoReach == 0) && all.Where(r => r.echo == BossRewardEcho.Explosion && r != artci).All(r => r.echoRadius == 1.25f),
            $"All {others.Count} other reward assets keep echoReach 0 (the new field defaults to no effect); every other Artçı asset keeps radius ×1,25");
        foreach (string name in new[] { "Run50_KirilmaV1", "Run50_TakvimV1", "Run50_OdulAsamalariV1", "Run50_BedelliOdullerV1" })
        {
            var rewards = RewardOfferLab.All(Profile(name).bossRewards);
            if (!rewards.Contains(oldArtci) || !rewards.Contains(oldCifte) || rewards.Contains(artci) || (cifte != null && rewards.Contains(cifte))) throw new Exception(name + " pool should keep the Kırılma V1 reward assets");
        }
        Require(true, "Older profiles (Kırılma V1, Takvim V1, Ödül Aşamaları V1, Bedelli Ödüller V1) still reference the unchanged Kırılma V1 reward assets");
        var ritim = Find(pool, "hasat_ritmi");
        Require(ritim == Find(K1.bossRewards, "hasat_ritmi") && ritim.rhythmHarvests == 5 && ritim.rhythmDamage == 1.75f && Near(ritim.rhythmRadius, 1.6f),
            "Hasat Ritmi is the same asset with its values untouched (every 5 direct harvests: damage ×1,75, radius ×1,60)");
        var og = Find(pool, "bereketli_ogrenim"); var ad = Find(pool, "davranisa_adanis");
        Require(og == Find(old, "bereketli_ogrenim") && ad == Find(old, "davranisa_adanis") && Near(og.directDamageMultiplier, .8f) && Near(ad.behaviorDamageMultiplier, 1.5f),
            "The two cost rewards are the same assets with the same coefficients (×0,80 / ×1,50, ±1 choice)");
        string artciText = BossRewardText.Effect(artci), oldText = BossRewardText.Effect(oldArtci), oldWave = BossRewardText.Effect(oldCifte);
        Require(artciText.Contains("yarıçap ×" + BossRewardText.Number(artci.echoRadius)) && oldText.Contains("yarıçap ×1,25") && !oldWave.Contains("çaprazda") &&
                (cifte == null || BossRewardText.Effect(cifte).Contains($"çaprazda {cifte.echoReach} hücre")),
            $"Card effect text comes from data: \"{artciText}\"" + (cifte != null ? $" · \"{BossRewardText.Effect(cifte)}\"" : "") + $"; old assets read as before (\"{oldWave}\")");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(x => x.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);
        return .05;
    }

    // ---------------------------------------------------------------- B) geometri (testin kendi hesabıyla karşılaştırma)
    static HashSet<(int, int)> Footprint(int sx, int sz, int ox, int oz)
    {
        var set = new HashSet<(int, int)>();
        for (int x = 0; x < sx; x++) for (int z = 0; z < sz; z++) set.Add((ox + x, oz + z));
        return set;
    }

    // Ayak izindeki herhangi bir hücrenin merkezine radius içinde olan, tarlanın içindeki, ayak izi dışındaki hücreler.
    static HashSet<(int, int)> Around(HashSet<(int, int)> footprint, float radius, int size)
    {
        var set = new HashSet<(int, int)>();
        for (int x = 0; x < size; x++) for (int z = 0; z < size; z++)
        {
            if (footprint.Contains((x, z))) continue;
            foreach (var f in footprint)
                if ((x - f.Item1) * (x - f.Item1) + (z - f.Item2) * (z - f.Item2) <= radius * radius + 1e-3f) { set.Add((x, z)); break; }
        }
        return set;
    }

    // Dört çapraz ışın, ayak izinin o yöndeki köşesinden; ışın başına reach hücre; tarlanın içinde ve ayak izinin dışında.
    static HashSet<(int, int)> Diagonals(HashSet<(int, int)> footprint, int reach, int size)
    {
        var set = new HashSet<(int, int)>();
        foreach (int dx in new[] { -1, 1 }) foreach (int dz in new[] { -1, 1 })
        {
            var corner = footprint.OrderByDescending(f => f.Item1 * dx + f.Item2 * dz).First();
            for (int step = 1; step <= reach; step++)
            {
                var c = (corner.Item1 + dx * step, corner.Item2 + dz * step);
                if (c.Item1 < 0 || c.Item2 < 0 || c.Item1 >= size || c.Item2 >= size || footprint.Contains(c)) continue;
                set.Add(c);
            }
        }
        return set;
    }

    static double Geometry()
    {
        var targets = new List<GridPosition>(); var origins = new List<GridPosition>();
        HashSet<(int, int)> Explode(HashSet<(int, int)> fp, float cells)
        {
            HarvestBehaviorGeometry.ExplosionCells(fp.Select(f => new GridPosition(f.Item1, f.Item2)).ToList(), cells, targets);
            Must(targets.Count == targets.Select(t => (t.x, t.z)).Distinct().Count(), "explosion targets repeat a cell");
            return new HashSet<(int, int)>(targets.Where(t => t.x >= 0 && t.z >= 0 && t.x < N && t.z < N).Select(t => (t.x, t.z)));
        }
        HashSet<(int, int)> Shock(HashSet<(int, int)> fp, int reach)
        {
            HarvestBehaviorGeometry.ElectricCells(fp.Select(f => new GridPosition(f.Item1, f.Item2)).ToList(), targets, origins, reach);
            Must(targets.Count == targets.Select(t => (t.x, t.z)).Distinct().Count() && targets.Count == origins.Count, "electric targets repeat a cell");
            Must(targets.All(t => !fp.Contains((t.x, t.z))), "an electric target lies inside the planter");
            return new HashSet<(int, int)>(targets.Where(t => t.x >= 0 && t.z >= 0 && t.x < N && t.z < N).Select(t => (t.x, t.z)));
        }
        var one = Footprint(1, 1, C, C);
        Require(Explode(one, 1f).Count == 4 && Explode(one, 1.5f).Count == 8 && Explode(one, 2f).Count == 12 && Explode(one, 2.25f).Count == 20 && Explode(one, 1.2f * 1.25f).SetEquals(Explode(one, 1.5f)),
            "Explosion around a 1×1 planter in the open: first blast 4 cells; aftershock 1,50 cells → 8 (the report's value is what the code does: 1,2 × 1,25), 2,00 → 12, 2,25 → 20");
        var cases = new (string name, int sx, int sz, int x, int z)[] { ("1×1 orta", 1, 1, C, C), ("1×1 kenar", 1, 1, 0, C), ("1×1 köşe", 1, 1, 0, 0), ("2×2 orta", 2, 2, 4, 4), ("2×2 köşe", 2, 2, 0, 0), ("2×3 kenar", 2, 3, 0, 4) };
        foreach (var c in cases)
        {
            var fp = Footprint(c.sx, c.sz, c.x, c.z);
            foreach (float cells in new[] { 1f, 1.5f, 2f, 2.25f })
                if (!Explode(fp, cells).SetEquals(Around(fp, cells, N))) throw new Exception($"explosion cells differ for {c.name} at {cells}");
            foreach (int reach in new[] { 2, 3, 4, 6 })
                if (!Shock(fp, reach).SetEquals(Diagonals(fp, reach, N))) throw new Exception($"electric cells differ for {c.name} at reach {reach}");
            Note($"geometri · {c.name}: patlama {Explode(fp, 1f).Count} → artçı 1,50: {Explode(fp, 1.5f).Count} · 2,00: {Explode(fp, 2f).Count} · 2,25: {Explode(fp, 2.25f).Count} | " +
                 $"elektrik 2: {Shock(fp, 2).Count} · 3: {Shock(fp, 3).Count} · 4: {Shock(fp, 4).Count}");
        }
        Require(true, $"{cases.Length} placements (centre, edge, corner; 1×1, 2×2, 2×3) × 4 radii and 4 reaches: the game's cell sets equal an independent calculation; no cell is listed twice; cells outside the field are simply absent");
        var big = Footprint(2, 2, 4, 4);
        Require(Shock(one, 2).Count == 8 && Shock(one, 3).Count == 12 && Shock(one, 4).Count == 16 && Shock(one, 3).IsSupersetOf(Shock(one, 2)) && Shock(one, 4).IsSupersetOf(Shock(one, 3)) &&
                Shock(big, 4).Count == 16 && Shock(Footprint(1, 1, 0, 0), 4).Count == 4,
            "Electric reach: 2 → 8 cells, 3 → 12, 4 → 16 (four diagonals only); a longer reach still contains the near cells; a 2×2 planter gives 16 unique cells from its four corners; a corner planter reaches 4");
        var real = Explode(one, HarvestBehaviorGeometry.ExplosionRadiusCells * Artci.echoRadius);
        Require(real.SetEquals(Around(one, ArtciCells, N)) && real.Count == (Near(ArtciCells, 2f) ? 12 : 20),
            $"The variant's data multiplier (×{Artci.echoRadius:0.######}) resolves to exactly the {ArtciCells:0.00}-cell set ({real.Count} cells) despite float rounding");
        return .05;
    }

    // ---------------------------------------------------------------- C) gerçek vuruşlar
    sealed class Hit { public DamageType Type; public int Damage, X, Z; public bool Echo; }
    static readonly List<Hit> hits = new();
    static int minX, minZ;
    static readonly List<(string phase, DamageType type, int frames)> echoLog = new();

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
    static void OnEcho(string phase, DamageType type, int id, float delay, int frames) => echoLog.Add((phase, type, frames));

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
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
        Strike(100f, .4f);
        PlantHealth.AnyDamaged += OnDamaged; BehaviorEchoes.Traced += OnEcho;
        return .05;
    }

    static GroundCell CellAt(int x, int z) => RewardOfferLab.OpenCells().First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
    static PlanterSO Planter(int sx, int sz) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {sx}x{sz}.asset");
    static PlanterBrain source; static HashSet<(int, int)> sourceCells; static readonly List<PlanterBrain> field = new();

    static PlantHealth PlantOf(GridObject grid) { var p = grid.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }

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

    // Kaynak saksı (verilen boyut ve köşe; altında davranış tile'ı, şans %100) + kalan bütün hücrelerde 1×1 saksı. Hepsinde taze,
    // canı yüksek bitki; kaynak saksının bitkileri 1 canla (ilk vuruşta ölür).
    static void BuildSource(int sx, int sz, int ox, int oz, string tile)
    {
        RewardOfferLab.ClearField();
        field.Clear();
        source = KirilmaErisimMeasurement.PlacePlanter(Planter(sx, sz), CellAt(ox, oz), RewardOfferLab.Tile(tile), 1f);
        // Ayak izi saksının gerçekten kapladığı hücrelerden okunur.
        sourceCells = new HashSet<(int, int)>(source.OccupiedGrids.Select(g => { var p = g.GetGroundCellCached().GetGridPosition(); return (p.x - minX, p.z - minZ); }));
        Must(sourceCells.Count == sx * sz && sourceCells.Contains((ox, oz)), $"source planter covers {sourceCells.Count} cells");
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++)
            if (!sourceCells.Contains((x, z))) field.Add(KirilmaErisimMeasurement.PlacePlanter(Planter(1, 1), CellAt(x, z), null, 0f));
        foreach (var brain in field) Respawn(brain, 1000000);
        Respawn(source, 1000000);
        // Yalnız vurulacak hücrenin bitkisi 1 canla: tek bir doğrudan hasat, tek bir tetik.
        SetF(PlantOf(source.OccupiedGrids[0]), "currentHealth", 1);
        hits.Clear(); echoLog.Clear();
    }

    static void Kill() => Call(Player, "AttackInRadius", source.OccupiedGrids[0].GetGroundCellCached().transform.position);
    static HashSet<(int, int)> Cells(IEnumerable<Hit> list) => new HashSet<(int, int)>(list.Select(h => (h.X, h.Z)));
    static string caseName;

    static double ExplosionCase(string name, int sx, int sz, int ox, int oz, BossRewardSO reward)
    {
        caseName = name;
        BuildSource(sx, sz, ox, oz, "Explosive");
        Boss.ClearAll();
        Must(Grant(reward), "Artçı not granted");
        hits.Clear();
        Kill();
        var first = hits.Where(h => h.Type == DamageType.Explosion && !h.Echo).ToList();
        var expected = Around(sourceCells, 1f, N);
        Must(Cells(first).SetEquals(expected) && first.Count == expected.Count && first.All(h => h.Damage == 100) && Echoes.Pending == 1,
            $"{name}: first blast hit {first.Count} cells, expected {expected.Count} (the planter's four-way neighbours), 100 each");
        return reward.echoDelay + .9;
    }

    static double ExplosionResult(BossRewardSO reward)
    {
        float cells = HarvestBehaviorGeometry.ExplosionRadiusCells * reward.echoRadius;
        var echo = hits.Where(h => h.Echo).ToList();
        var expected = Around(sourceCells, cells, N); var firstArea = Around(sourceCells, 1f, N);
        int damage = BehaviorEchoes.ScaleDamage(100, reward.echoDamage);
        Require(Cells(echo).SetEquals(expected) && echo.Count == expected.Count && echo.All(h => h.Type == DamageType.Explosion && h.Damage == damage) && Echoes.Pending == 0 &&
                !hits.Any(h => sourceCells.Contains((h.X, h.Z)) && h.Type != DamageType.Direct) && hits.Count(h => h.Type != DamageType.Explosion && h.Type != DamageType.Direct) == 0,
            $"Aftershock · {caseName} · {cells:0.00} cells: first blast {firstArea.Count} cells of 100 (unchanged); second blast hits {echo.Count} living plants, each exactly once, {damage} each; " +
            $"{expected.Count(c => !firstArea.Contains(c))} of them are cells the first blast never reached; nothing else is triggered");
        return .05;
    }

    static BossRewardSO waveReward;

    static double ElectricCase(string name, int sx, int sz, int ox, int oz, int reach)
    {
        caseName = name;
        BuildSource(sx, sz, ox, oz, "Electric");
        Boss.ClearAll();
        // Erişim 0: Kırılma V1'in kendi asset'i (eski ödül). Diğerleri: aynı ödülün kopyası, yalnız erişim alanı farklı.
        if (waveReward != null && waveReward != OldCifte) Object.DestroyImmediate(waveReward);
        waveReward = reach == 0 ? OldCifte : Object.Instantiate(OldCifte);
        if (reach != 0) waveReward.echoReach = reach;
        Must(Grant(waveReward), "Çifte Akım not granted");
        hits.Clear();
        Kill();
        var first = hits.Where(h => h.Type == DamageType.Electric && !h.Echo).ToList();
        var expected = Diagonals(sourceCells, HarvestBehaviorGeometry.ElectricReachCells, N);
        Must(Cells(first).SetEquals(expected) && first.Count == expected.Count && first.All(h => h.Damage == 100) && Echoes.Pending == 1,
            $"{name}: first wave hit {first.Count} cells, expected {expected.Count} (two cells per diagonal), 100 each");
        return waveReward.echoDelay + .9;
    }

    static double ElectricResult(int reach)
    {
        int cells = reach > 0 ? reach : HarvestBehaviorGeometry.ElectricReachCells;
        var echo = hits.Where(h => h.Echo).ToList();
        var expected = Diagonals(sourceCells, cells, N); var firstArea = Diagonals(sourceCells, HarvestBehaviorGeometry.ElectricReachCells, N);
        int damage = BehaviorEchoes.ScaleDamage(100, waveReward.echoDamage);
        Require(Cells(echo).SetEquals(expected) && echo.Count == expected.Count && echo.All(h => h.Type == DamageType.Electric && h.Damage == damage) && expected.IsSupersetOf(firstArea) &&
                hits.Count(h => h.Type != DamageType.Electric && h.Type != DamageType.Direct) == 0 && Echoes.Pending == 0,
            $"Second wave · {caseName} · reach {(reach == 0 ? "0 (old reward: same as the first wave)" : cells.ToString())}: first wave {firstArea.Count} cells of 100 (unchanged); second wave hits {echo.Count} living plants, " +
            $"each exactly once, {damage} each, the near cells included; {expected.Count(c => !firstArea.Contains(c))} are beyond the first wave; four diagonals only, no jump to other targets");
        return .05;
    }

    // ---------------------------------------------------------------- D) uygulama sınırları
    static double BoltCapacity()
    {
        var echo = hits.Where(h => h.Echo).ToList();
        var expected = Diagonals(sourceCells, 6, N);
        var manager = HarvestBehaviorManager.Instance;
        Require(expected.Count == 20 && ElectricBurst.MaxBolts == 16 && Cells(echo).SetEquals(expected) && echo.Count == 20 && manager.SkippedElectricBolts == 4,
            "Drawing capacity: a test reach of 6 from the centre of the 11×11 field gives 20 target cells; all 20 plants are damaged once, 16 bolts are drawn and 4 are counted as not drawn — the cap never cuts damage");
        return .05;
    }

    static double VisualPoolFull()
    {
        var manager = HarvestBehaviorManager.Instance;
        BuildSource(1, 1, C, C, "Electric");
        int skipped = manager.SkippedElectricVisuals, active = manager.ActiveElectricBursts;
        int capacity = F<int>(manager, "maxElectricBursts"), waves = capacity + 6, total = 0, minStruck = int.MaxValue;
        hits.Clear();
        for (int i = 0; i < waves; i++)
        {
            Must(manager.TryElectricEcho(source, 50, 4, out int struck, out _, out int reached) && reached == 16, "echo wave did not run");
            total += struck; minStruck = Math.Min(minStruck, struck);
        }
        int notDrawn = manager.SkippedElectricVisuals - skipped;
        Require(minStruck == 16 && total == waves * 16 && hits.Count == waves * 16 && hits.All(h => h.Damage == 50) && notDrawn == waves - (capacity - active) && notDrawn >= 6 && manager.ActiveElectricBursts == capacity,
            $"Visual pool full: {waves} second waves in one frame (reach 4) — every wave still damages all 16 plants ({total} hits); {notDrawn} waves were not drawn because only {capacity} effects exist ({active} were active before)");
        return .6;
    }

    // ---------------------------------------------------------------- E) yaşam döngüsü
    static PlanterBrain inside, outside; static uint insideLife;

    static double PooledPlant()
    {
        BuildSource(1, 1, C, C, "Explosive");
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        hits.Clear();
        Kill();
        Must(Echoes.Pending == 1, "aftershock should be pending");
        // Bekleme sırasında: alanın içindeki bir hücrenin bitkisi yenisiyle değişir, alanın dışındaki bir hücrede de yeni bitki doğar.
        inside = field.First(b => Local(b.transform.position) == (C + 1, C + 1));
        outside = field.First(b => Local(b.transform.position) == (C + 4, C));
        insideLife = PlantOf(inside.OccupiedGrids[0]).LifetimeVersion;
        var old = PlantOf(inside.OccupiedGrids[0]);
        Respawn(inside, 1000000); Respawn(outside, 1000000);
        Must(PlantOf(inside.OccupiedGrids[0]).LifetimeVersion != insideLife || PlantOf(inside.OccupiedGrids[0]) != old, "the plant in the aftershock area should be a new life");
        hits.Clear();
        return Artci.echoDelay + .9;
    }

    static double PooledPlantResult()
    {
        var echo = hits.Where(h => h.Echo).ToList();
        int damage = BehaviorEchoes.ScaleDamage(100, Artci.echoDamage);
        var at = PlantOf(inside.OccupiedGrids[0]); var far = PlantOf(outside.OccupiedGrids[0]);
        Require(echo.Count(h => (h.X, h.Z) == (C + 1, C + 1)) == 1 && at.CurrentHealth == 1000000 - damage && far.CurrentHealth == 1000000 && !echo.Any(h => (h.X, h.Z) == (C + 4, C)) &&
                Cells(echo).SetEquals(Around(sourceCells, ArtciCells, N)),
            "Pooled plants: the job holds cells, not plant objects — a plant reborn inside the area during the delay is hit once as the living plant of that cell; a plant reborn outside the area is not hit");
        return .05;
    }

    // Yankı gecikmesi, sabit simülasyon adımında: planlamadan uygulamaya geçen kare sayısı mutlak oyun zamanından bağımsız olmalı.
    static readonly List<int> explosionFrames = new(), electricFrames = new();
    static double DelayFrames(int k)
    {
        if (k == 0) { explosionFrames.Clear(); electricFrames.Clear(); }
        else
        {
            foreach (var e in echoLog.Where(e => e.phase == "uygula")) (e.type == DamageType.Explosion ? explosionFrames : electricFrames).Add(e.frames);
        }
        if (k == 6) return .05;
        // Çift sayılı adımlarda patlama (0,20 sn), tek sayılı adımlarda elektrik (0,15 sn).
        bool explosion = k % 2 == 0;
        BuildSource(1, 1, C, C, explosion ? "Explosive" : "Electric");
        Boss.ClearAll();
        Must(Grant(explosion ? Artci : OldCifte), "reward not granted");
        echoLog.Clear();
        Time.captureDeltaTime = 1f / 30f;
        Kill();
        Must(Echoes.Pending == 1, "an echo should be pending");
        return .9 + k * .13;   // her denemede farklı bir mutlak zamanda
    }

    static double DelayResult()
    {
        Time.captureDeltaTime = 0f;
        Require(explosionFrames.Count == 3 && explosionFrames.All(f => f == 6) && electricFrames.Count == 3 && electricFrames.All(f => f == 5),
            $"Echo delay at a fixed 1/30 s step, tried at six different absolute game times (Time.time now {Time.time:0.0}): the aftershock (0,20 s) always lands 6 frames after it was scheduled " +
            $"({string.Join(", ", explosionFrames)}), the second wave (0,15 s) 5 frames ({string.Join(", ", electricFrames)}) — the delay no longer depends on the absolute time");
        return .05;
    }

    // ---------------------------------------------------------------- F) katsayılar
    static double CoefficientCase()
    {
        BuildSource(1, 1, C, C, "Explosive");
        Boss.ClearAll();
        var usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/DavranisUstasi.asset");
        SetP(Spec, "Chosen", usta);
        Must(Grant(Find(Pool, "davranisa_adanis")) && Grant(Artci), "rewards not granted");
        hits.Clear();
        Kill();
        return Artci.echoDelay + .9;
    }

    static double CoefficientResult()
    {
        var first = hits.Where(h => h.Type == DamageType.Explosion && !h.Echo).ToList(); var echo = hits.Where(h => h.Echo).ToList();
        int expected = BehaviorEchoes.ScaleDamage(188, Artci.echoDamage);
        Require(first.Count == 4 && first.All(h => h.Damage == 188) && echo.Count == Around(sourceCells, ArtciCells, N).Count && echo.All(h => h.Damage == expected) && expected < 250,
            $"Coefficients with the wider aftershock: Davranışa Adanış ×1,50 and Davranış Ustası ×1,25 → first blast 188 (100 × 1,25 × 1,50, once); the aftershock uses that computed value × {BossRewardText.Number(Artci.echoDamage)} = {expected} " +
            "on every cell of the wider area — neither coefficient is applied a second time");
        SetP(Spec, "Chosen", null);
        return .05;
    }

    static int droppedBefore;
    static double RoundEndCase()
    {
        BuildSource(1, 1, C, C, "Explosive");
        Boss.ClearAll();
        Must(Grant(Artci), "Artçı not granted");
        hits.Clear();
        Kill();
        Must(Echoes.Pending == 1, "aftershock should be pending");
        droppedBefore = Echoes.Dropped(DamageType.Explosion);
        hits.Clear();
        SetP(RM, "RemainingTime", 0f);
        return Artci.echoDelay + 1.2;
    }

    static double RoundEndResult()
    {
        Require(!RM.IsRoundActive && Echoes.Pending == 0 && Echoes.Dropped(DamageType.Explosion) == droppedBefore + 1 && !hits.Any(h => h.Echo),
            "Round ends during the delay: the pending aftershock is dropped and never deals damage (no late hit after the round)");
        PlantHealth.AnyDamaged -= OnDamaged; BehaviorEchoes.Traced -= OnEcho;
        ClearTestMods();
        return .05;
    }

    // ---------------------------------------------------------------- G) ödül ekranı
    static int[] D; static long[] Q, BT; static long expectedTotal;

    static double Load(RunProfileSO profile)
    {
        Selection.active = profile;
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != E1) return Again;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        RunCalendar c = E1.Calendar;
        D = Enumerable.Range(1, E1.runLength).Where(c.IsPeriodEnd).ToArray();
        Q = D.Select((_, i) => c.QuotaTarget(i + 1)).ToArray(); BT = D.Select((_, i) => c.BossTarget(i + 1)).ToArray();
        Require(Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Explosion) == 0 && Echoes.Dropped(DamageType.Explosion) == 0 && Echoes.Cells(DamageType.Explosion) == 0 && Boss.Taken.Count == 0 &&
                !BossRewardManager.TryGetEcho(DamageType.Explosion, out _) && !BossRewardManager.TryGetEcho(DamageType.Electric, out _) && BossRewardManager.BehaviorDamageMultiplier == 1f,
            "New scene / new run: no pending aftershock, echo counters at 0, no echo reward and no reward effect carried over");
        RewardOfferLab.Build(RewardOfferLab.Layout.Multi);
        expectedTotal = 0;
        return .05;
    }

    static double StartRound(int r)
    {
        Must(State == (r == 1 ? GameStates.RunSetup : GameStates.RoundEnd), $"R{r}: unexpected state before the round ({State})");
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == r, $"R{r}: round did not start");
        SetP(RM, "RemainingTime", 1000f);
        long add = r == 1 ? Q[0] - BT[0] : r == D[0] ? BT[0] : 0;
        AddScore(add); expectedTotal += add;
        return .05;
    }

    static string FitProblem(BossRewardPanelUI panel, int index)
    {
        foreach (var text in panel.CardTexts(index))
        {
            if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            Rect rect = text.rectTransform.rect; Vector3 size = text.textBounds.size;
            float min = text.name == "Effect" ? 20f : text.name == "Note" || text.name == "Stack" ? 15f : 14f;
            if (text.isTextTruncated) return $"{text.name} text is cut: \"{Strip(text.text)}\"";
            if (size.x > rect.width + 1.5f || size.y > rect.height + 1.5f) return $"{text.name} text {size.x:0}×{size.y:0} does not fit {rect.width:0}×{rect.height:0}: \"{Strip(text.text)}\"";
            if (text.fontSize < min - .05f) return $"{text.name} text shrank to {text.fontSize:0.#} pt (minimum {min})";
        }
        return null;
    }

    static double AfterRound(int r)
    {
        if (RM.IsRoundActive) return Again;
        if (r != D[0])
        {
            Must(State == GameStates.RoundEnd && !Boss.IsPending, $"R{r}: an ordinary round should end in the round summary ({State})");
            return .03;
        }
        Must(!RM.RunFailed && Boss.IsPending && State == GameStates.RoundChoice, $"R{r}: the boss should be passed and a reward pending ({State})");
        var panel = Panel; var real = Boss.Offer.ToList();
        var list = F<List<BossRewardSO>>(Boss, "offer");
        // Yalnız test düzeneği: kart yazılarını göstermek için teklif elle kurulur (bu ödüller R23'ten önce sunulmaz).
        list.Clear(); list.Add(Artci); if (Cifte != null) list.Add(Cifte); list.Add(Find(Pool, "hasat_ritmi"));
        Call(panel, "Fill");
        for (int i = 0; i < list.Count; i++) { string fit = FitProblem(panel, i); Must(fit == null, $"card {i} ({list[i].displayName}): {fit}"); }
        string effects = string.Join(" | ", Enumerable.Range(0, list.Count).Select(i => Strip(panel.CardTexts(i).First(t => t.name == "Effect").text)));
        Require(panel.TitleText == "ERKEN AŞAMA · BOSS ÖDÜLÜ" && effects.Contains("yarıçap ×" + BossRewardText.Number(Artci.echoRadius)) && (Cifte == null || effects.Contains($"çaprazda {Cifte.echoReach} hücre")),
            $"Reward cards of the variants (placed on the screen by the test): no text is cut or leaves its area — {effects}");
        InsertNext(("görüntü kartlar", () => { Capture("T375_01_ErisimKartlari"); return .05; }), ("teklif geri", () =>
        {
            list.Clear(); list.AddRange(real);
            Must(Boss.Choose(real[0]) && State == GameStates.RoundEnd, "reward taken, round summary");
            return .05;
        }));
        return .7;
    }

    // ---------------------------------------------------------------- eski profil
    static double Legacy()
    {
        if (RM == null || RM.Profile != K1 || State != GameStates.RunSetup) return Again;
        Player.enabled = false;
        var pool = K1.bossRewards;
        Require(Find(pool, "artci_patlama") == OldArtci && Find(pool, "cifte_akim") == OldCifte && OldArtci.echoRadius == 1.25f && OldCifte.echoReach == 0 &&
                Grant(OldArtci) && Grant(OldCifte) && BossRewardManager.TryGetEcho(DamageType.Explosion, out BehaviorEcho blast) && blast.Radius == 1.25f && blast.Delay == .2f &&
                BossRewardManager.TryGetEcho(DamageType.Electric, out BehaviorEcho wave) && wave.Reach == 0 && wave.Delay == .15f,
            "Old profile (Kırılma V1): its Artçı Patlama still gives radius ×1,25 (1,50 cells) and its Çifte Akım the first wave's reach; the live echo settings read exactly those values");
        Boss.ClearAll();
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0, "No error or exception logged during the test");
        return .05;
    }
}
