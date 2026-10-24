using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Mechanics Test Lab'ın ARAÇ doğrulaması. Denge ölçümü ve insan Play testi DEĞİLDİR; görsel önizleme hiçbir
// yerde mekanik PASS sayılmaz. Oturum, MechanicsTestScene açılıp Play'e girilerek başlar (kullanıcının yaptığı gibi); bütün
// müdahaleler panelin kullandığı aynı komutlardan (TestField / TestStats / TestRewards / TestEvents / TestAttack / TestTime /
// TestSession) geçer. Beklentiler oyunun durumundan okunur: hücre sahipliği, bitki canı, davranış sayaçları, olay fazları.
// Zaman: sabit kare adımı (1 / 30 sn) — sonuçlar makinenin hızına bağlı değildir.
[InitializeOnLoad]
public static class MechanicsTestLabVerification
{
    const string Key = "MechanicsTestLabVerification";
    // İki koşu: tam doğrulama (sahne açılıp Play) ve panelin başlatma yolu (açık sahne değişmeden Play).
    static bool StartPlayMode => SessionState.GetString(Key + ".mode", "") == "startplay";
    static string LogFile => StartPlayMode ? "Logs/MechanicsTestLabStartPlay.txt" : "Logs/MechanicsTestLabVerification.txt";
    const string HashFile = "Logs/MechanicsTestLabVerification.hashes.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string PlayerScripts = "Temp/MechanicsTestLabPlayerScripts";
    const double Again = double.NaN;
    const float Dt = 1f / 30f;
    const int C = 5;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince;
    static int stepIndex, shownStep = -1, errors;
    static bool planned, exiting;

    static MechanicsTestLabVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        if (!TestSession.SceneExists) throw new Exception("test scene could not be built");
        SessionState.SetString(Key + ".mode", "");
        SessionState.SetString(Key + ".player", PlayerCompile());
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        SessionState.SetString(Key + ".selection", selection != null && selection.active != null ? selection.active.name : "");
        SessionState.SetString(Key + ".save", SaveStamp());
        Directory.CreateDirectory("Logs");
        File.WriteAllText(HashFile, Hashes());
        EditorSceneManager.OpenScene(Lab.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    // Panelin "Test oturumunu başlat" yolu: açık sahne GameScene iken, sahne değiştirilmeden, seçilen senaryoyla Play başlatılır.
    public static void RunStartPlayBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetString(Key + ".mode", "startplay");
        SessionState.SetString(Key + ".player", "OK: not part of this run");
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        SessionState.SetString(Key + ".selection", selection != null && selection.active != null ? selection.active.name : "");
        SessionState.SetString(Key + ".save", SaveStamp());
        Directory.CreateDirectory("Logs");
        File.WriteAllText(HashFile, Hashes());
        SessionState.SetBool(Key, true);
        Result started = TestSession.StartPlay(ScenarioPresets.All().First(p => p.name.StartsWith("06")));
        if (!started.Ok) throw new Exception(started.Message);
    }

    static double StartPlayChecks()
    {
        Ok(TestSession.Active && TestSession.Ready && SceneManager.GetActiveScene().path == Lab.GameScenePath, "panelden başlatma: açık sahne GameScene iken test oturumu açıldı ve GameScene'i yükledi");
        Ok(TestSession.Current.name.StartsWith("06") && TestSession.SetupReport.All(r => r.Ok) && Boss.Stacks(Reward("hasat_ritmi")) == 1 && RunPower.Rhythm.Enabled,
            "panelde seçilen senaryo (06 · Hasat Ritmi) Play'e taşındı ve kuruldu");
        Ok(Rounds.Profile == TestSession.Profile && RunProfileSelectionSO.HasSessionOverride && MetaSave.EditorSessionMemoryOnly, "aynı güvenlik: profil kopyası ve bellekte kayıt");
        return 0;
    }

    // Oyuncu derlemesi (UNITY_EDITOR tanımsız): oyun kodu Editor girişleri olmadan derlenmeli ve çıktıda test adı bulunmamalı.
    static string PlayerCompile()
    {
        try
        {
            if (Directory.Exists(PlayerScripts)) Directory.Delete(PlayerScripts, true);
            var settings = new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None };
            ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(settings, PlayerScripts);
            string dll = Path.Combine(PlayerScripts, "Assembly-CSharp.dll");
            if (result.assemblies == null || result.assemblies.Count == 0 || !File.Exists(dll)) return "FAIL: player scripts did not compile";
            string text = Encoding.ASCII.GetString(File.ReadAllBytes(dll));
            if (!text.Contains("AttackInRadius")) return "FAIL: player assembly does not look like the game assembly";
            string[] forbidden = { "EditorAttackNow", "EditorAutoAttack", "EditorPlace", "EditorRemove", "EditorRespawn", "EditorPauseProduction", "EditorSetHealth",
                "EditorEndRound", "EditorFreezeTimer", "EditorRestartBossRhythm", "EditorIsEligible", "EditorPreview", "EditorSetLevel", "EditorAllowOfferAgain",
                "EditorRecordPlacement", "EditorGrant", "EditorNumericTrace", "DirectHitObserved", "HarvestRewardObserved", "MechanicsTestLab" };
            string leaked = string.Join(", ", forbidden.Where(name => text.Contains(name)));
            return leaked.Length > 0 ? "FAIL: player assembly contains test entry points: " + leaked
                : $"OK: {result.assemblies.Count} player assemblies compiled without UNITY_EDITOR; none of {forbidden.Length} test entry points present";
        }
        catch (Exception e) { return "FAIL: player compile threw " + e.Message; }
    }

    static string SaveStamp()
    {
        string path = Path.Combine(Application.persistentDataPath, MetaSave.FileName);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks + ":" + new FileInfo(path).Length : "none";
    }

    static string Hashes()
    {
        var text = new StringBuilder();
        using MD5 md5 = MD5.Create();
        var files = new List<string>();
        foreach (string root in new[] { "Assets/ScriptableObjects", "Assets/Resources", "Assets/Scenes" })
            files.AddRange(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta") && (f.EndsWith(".asset") || f.EndsWith(".unity"))));
        files.Sort(StringComparer.Ordinal);
        foreach (string file in files)
            text.Append(file.Replace('\\', '/')).Append(' ').Append(BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
        text.Append("BUILD ").Append(string.Join("|", EditorBuildSettings.scenes.Select(s => s.path)));
        return text.ToString();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
        try
        {
            if (exiting)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                AfterPlay();
                Finish(null);
                return;
            }
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (!planned)
            {
                planned = true; nextAt = now + 1; stepSince = now;
                Application.logMessageReceived += CountLogs;
                Time.captureDeltaTime = Dt;
                Plan();
                return;
            }
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { exiting = true; EditorApplication.ExitPlaymode(); return; }
            if (stepIndex != shownStep)
            {
                shownStep = stepIndex; stepSince = now;
                File.WriteAllLines(LogFile, new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 600) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck · session status: {TestSession.Status}");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now; EditorApplication.QueuePlayerLoopUpdate(); return; }
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
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
        Directory.CreateDirectory("Logs");
        bool pass = ex == null && errors == 0;
        string head = pass ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks"
            : ex != null ? "FAIL: " + ex : $"FAIL: {errors} error log(s) during the run";
        File.WriteAllLines(LogFile, new[] { head }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(pass ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Ok(bool c, string m) { if (!c) throw new Exception("CHECK FAILED: " + m); notes.Add("ok: " + m); }
    static void Must(bool c, string m) { if (!c) throw new Exception("SETUP FAILED: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static GridPosition P(int x, int z) => new GridPosition(x, z);
    static RoundManager Rounds => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static BossRewardSO Reward(string id) { BossRewardSO r = LabCatalog.Reward(Boss.Pool, id); Must(r != null, "reward in pool: " + id); return r; }
    static StatModifier Mod(StatType s, StatTarget t, ModifierOperation op, float v) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static Scenario Preset(string prefix) { Scenario s = ScenarioPresets.All().FirstOrDefault(p => p.name.StartsWith(prefix)); Must(s != null, "preset " + prefix); return s; }
    static int PlanterCount() { var list = new List<PlanterBrain>(); TestField.Planters(list); return list.Count; }
    static string Failures() => string.Join(" | ", TestSession.SetupReport.Where(r => !r.Ok).Select(r => r.Message));
    static List<TestLog.Entry> Log(TestLog.Kind mask, string search = null) { var list = new List<TestLog.Entry>(); TestLog.Collect(mask, search, 0, list); return list; }

    static int waitFrame = -1;
    static double Frames(int count)
    {
        if (waitFrame < 0) waitFrame = Time.frameCount + count;
        if (Time.frameCount < waitFrame) return Again;
        waitFrame = -1;
        return 0;
    }
    static void AddFrames(int count) => Add($"bekle {count} kare", () => Frames(count));
    static void AddReady(string what) => Add("hazır: " + what, () =>
    {
        if (!TestSession.Ready) return Again;
        Must(TestSession.SetupReport.All(r => r.Ok), what + " setup failures: " + Failures());
        return 0;
    });
    static void AddRebuild(string prefix) { Add("kur: " + prefix, () => { Must(TestSession.Rebuild(Preset(prefix)).Ok, "rebuild " + prefix); return 0; }); AddReady(prefix); }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        if (StartPlayMode)
        {
            Add("oturum hazır", () => TestSession.Ready ? 0 : Again);
            Add("panelden başlatma", StartPlayChecks);
            return;
        }
        Add("oturum hazır", () => TestSession.Ready ? 0 : Again);
        Add("oturum: güvenlik ve gerçek sistemler", Session);
        AddFrames(20);
        Add("oturum: round sayacı ve üretim kontrolü", SessionTimers);
        AddFrames(45);
        Add("oturum: üretim duraklatması", ProductionPause);
        Add("tarla", Field);
        Add("stat", StatChecks);
        Add("ödül: kontrollü kurulum", RewardControlled);
        Add("ödül: normal teklif akışı", OfferOpen);
        AddFrames(3);
        Add("kart: oyunun kart yolu", Cards);
        AddFrames(3);
        Add("ödül: oyunun teklifinden seçim", OfferChoose);
        Add("senaryo: yakala, kaydet, temizle, yükle", SaveLoad);
        AddReady("yüklenen senaryo");
        Add("senaryo: aynı başlangıç düzeni", SaveLoadCheck);
        Add("çakışanı kaldırıp yeniden kur", Reinstall);
        AddReady("çakışansız yeniden kurulum");
        Add("çakışansız yeniden kurulum: sonuç", ReinstallCheck);
        AddRebuild("02");
        Add("manuel saldırı: gerçek mekanik zinciri", ManualAttack);
        AddFrames(120);
        Add("manuel saldırı: otomatik saldırı kapalı kalır", NoAutoAttack);
        Add("zaman: duraklat", () => { TestTime.SetPaused(true); return 0; });
        AddFrames(3);
        Add("zaman: duraklatma başlangıcı", PauseStart);
        AddFrames(10);
        Add("zaman: duraklatılmışken zaman akmaz, tek adım", PauseAndStep);
        AddFrames(4);
        Add("zaman: tek adım tam bir kare", StepResult);
        AddFrames(6);
        Add("zaman: adımdan sonra yine durur, yarı hız", HalfSpeed);
        AddFrames(4);
        Add("zaman: yarı hız başlangıcı", HalfStart);
        AddFrames(12);
        Add("zaman: yarı hız", HalfResult);
        AddRebuild("08");
        Add("gecikmeli etki: duraklatıp saldır", EchoStart);
        AddFrames(10);
        Add("gecikmeli etki: duraklatılmışken bekler", EchoPaused);
        Add("gecikmeli etki: adım adım ilerlet", EchoStep);
        AddRebuild("04");
        Add("hareketli davranış: kasırga başlar", TornadoStart);
        AddFrames(15);
        Add("hareketli davranış: ilerler, sonra duraklat", TornadoMoving);
        AddFrames(4);
        Add("hareketli davranış: duraklatma anı", TornadoPausedStart);
        AddFrames(10);
        Add("hareketli davranış: duraklatılmışken durur", TornadoPaused);
        Add("hareketli davranış: biter", TornadoEnds);
        AddRebuild("18");
        Add("tarla olayı: geçersiz ve geçerli konum", EventStart);
        Add("tarla olayı: hazırlık fazı", () => Phase(FieldEventPhase.Preparing));
        Add("tarla olayı: duraklat", () => { TestTime.SetPaused(true); return 0; });
        AddFrames(3);
        Add("tarla olayı: duraklatma anı", EventPauseStart);
        AddFrames(10);
        Add("tarla olayı: duraklatılmışken faz ilerlemez", EventPaused);
        Add("tarla olayı: aktif faz (normal zamanlama)", () => Phase(FieldEventPhase.Active));
        Add("tarla olayı: aktif etki ve gerçek saldırıyla hedefler", EventTargets);
        Add("tarla olayı: çözülme biter", () => FieldEventDirector.Instance.Current == null ? 0 : Again);
        Add("tarla olayı: süre dolması", EventTimeoutStart);
        Add("tarla olayı: süre doldu", EventTimeout);
        Add("tarla olayı: diğer üç olay", OtherEvents);
        Add("boss: üç boss aynı tarlada", Bosses);
        AddRebuild("07");
        Add("yeniden kurma: taban", RebuildBaseline);
        Add("yeniden kurma: bekleyen işle yeniden kur 1", RebuildDirty);
        AddReady("yeniden kurma 1");
        Add("yeniden kurma: bekleyen işle yeniden kur 2", RebuildDirty);
        AddReady("yeniden kurma 2");
        Add("yeniden kurma: bekleyen işle yeniden kur 3", RebuildDirty);
        AddReady("yeniden kurma 3");
        Add("yeniden kurma: birikme ve çift abonelik yok", RebuildStable);
        AddFrames(45);
        Add("yeniden kurma: zincir kaydı", ChainLogged);
        Add("hazır senaryolar", Presets);
        Add("panel: sekmeler çiziliyor", PanelSmoke);
    }

    // ---------------------------------------------------------------- oturum
    static float remainingAtStart;
    static int plantsAtPause;

    static double Session()
    {
        Ok(TestSession.Active && TestSession.Ready, "Play, MechanicsTestScene'den girilince test oturumu kendiliğinden açıldı");
        Ok(SceneManager.GetActiveScene().path == Lab.GameScenePath, "oturum gerçek GameScene'i yükledi (kopya sahne yok)");
        Ok(Rounds.Profile == TestSession.Profile && TestSession.Profile != TestSession.SourceProfile && AssetDatabase.GetAssetPath(TestSession.Profile) == "",
            "run, profilin çalışma zamanı kopyasıyla çalışıyor (kopya bir asset değil)");
        Ok(TestSession.SourceProfile.name == Lab.DefaultProfile, "kaynak profil " + Lab.DefaultProfile);
        Ok(Rounds.Profile.bossRewards == TestSession.SourceProfile.bossRewards && Rounds.Profile.balance == TestSession.SourceProfile.balance &&
           Rounds.Profile.fieldEvents == TestSession.SourceProfile.fieldEvents && Rounds.Profile.bossPool == TestSession.SourceProfile.bossPool,
            "ödül havuzu, denge seti, boss havuzu ve tarla olayı planı oyunun gerçek asset'leri (paylaşılıyor)");
        Ok(RunProfileSelectionSO.HasSessionOverride, "profil yalnız bu oturum için geçersiz kılındı");
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        Ok((selection.active != null ? selection.active.name : "") == SessionState.GetString(Key + ".selection", "?"), "seçili run profili (seçim dosyası) değişmedi");
        Ok(MetaSave.EditorSessionMemoryOnly && !MetaSave.Save(), "kayıt bu oturumda yalnız bellekte: yazma isteği reddediliyor");
        Ok(State == GameStates.Round && Rounds.IsRoundActive && Rounds.CurrentRound == 1, "oturum aktif bir round'la başlıyor (R1, oyunun normal başlangıç yolu)");
        Ok(Object.FindFirstObjectByType<PlayerController>() != null && Boss != null && Boss.Pool != null && FieldEventDirector.Instance != null &&
           FieldEventDirector.Instance.Plan != null && TornadoManager.Instance != null && HarvestBehaviorManager.Instance != null && VFXManager.Instance != null &&
           SegmentEventDirector.Instance != null && HarvestChain.Instance != null && RunEffects.Instance != null && PlacementManager.Instance != null &&
           Object.FindFirstObjectByType<UIManager>() != null, "gerçek oynanış ve arayüz bileşenleri sahnede (oyuncu, ödül, olay, davranış, VFX, zincir, yerleşim, UI)");
        Ok(TestSession.SetupReport.All(r => r.Ok), "varsayılan senaryo eksiksiz kuruldu" + (TestSession.SetupReport.All(r => r.Ok) ? "" : ": " + Failures()));
        Ok(PlanterCount() == 9 && TestField.GridSize == 5, "varsayılan düzen: 5 × 5 açık alan, 9 saksı");
        Ok(!EditorBuildSettings.scenes.Any(s => s.path == Lab.ScenePath), "test sahnesi Build Settings'te değil");
        Ok(typeof(TestSession).Assembly.GetName().Name == "Assembly-CSharp-Editor" && typeof(LabWindow).Assembly == typeof(TestSession).Assembly,
            "test aracının bütün kodu Editor derlemesinde");
        string player = SessionState.GetString(Key + ".player", "FAIL: not run");
        Ok(player.StartsWith("OK"), "oyuncu derlemesi: " + player);
        Ok(RoundManager.EditorFreezeTimer && TestLog.Enabled, "round sayacı dondurulmuş, olay kaydı açık");
        remainingAtStart = Rounds.RemainingTime;
        return 0;
    }

    static double SessionTimers()
    {
        Ok(Near(Rounds.RemainingTime, remainingAtStart, 1e-5f) && Rounds.IsRoundActive, "round sayacı dondurulmuşken 20 karede ilerlemedi");
        TestAttack.RoundTimerFrozen = false;
        TestAttack.ProductionPaused = true;
        TestField.ClearPlants();
        plantsAtPause = 0;
        return 0;
    }

    static double ProductionPause()
    {
        Ok(Rounds.RemainingTime < remainingAtStart - 1f, "sayaç serbest bırakılınca round süresi oyunun saatiyle akıyor");
        TestAttack.RoundTimerFrozen = true;
        var planters = new List<PlanterBrain>(); TestField.Planters(planters);
        int plants = planters.Sum(b => b.EditorSpawners.Count(s => s.SpawnedPlant != null));
        Ok(plants == plantsAtPause, "üretim duraklatılmışken 45 karede (1,5 sn) hiçbir üretim noktası bitki çıkarmadı");
        Ok(PlantSpawner.Active.Count == planters.Sum(b => b.EditorSpawners.Count), "üretim noktaları kayıtlı kaldı (duraklatma bileşeni kapatmıyor: tarla olayları bölge bulabilir)");
        TestAttack.ProductionPaused = false;
        TestAttack.AutoAttack = false;
        return 0;
    }

    // ---------------------------------------------------------------- tarla
    static double Field()
    {
        GridSystem grid = FieldCells.Grid;
        int gold = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold);
        Ok(TestField.SetGridSize(7).Ok && TestField.GridSize == 7 && FieldCells.IsOpen(grid, P(2, 2)) && !FieldCells.IsOpen(grid, P(1, 1)), "grid 7 × 7 açıldı (oyunun açma yolu)");
        Ok(!TestField.SetGridSize(5).Ok && TestField.GridSize == 7, "canlı küçültme reddedildi (oyunda hücre kilitleme yok)");
        Ok(!TestField.SetGridSize(4).Ok, "desteklenmeyen boyut reddedildi");

        TileModifierSO tile = LabCatalog.Tile(TileModifierType.Explosive, TileRarity.Legendary);
        Must(tile != null && tile.modifierRanges.Count > 0, "Explosive-Legendary tile");
        StatModifierRange range = tile.modifierRanges[0];
        Ok(TestField.ApplyTile(P(C, C), tile, 1f, 2).Ok, "tile atandı: " + tile.name);
        GroundCell ground = TestField.Ground(P(C, C));
        float expected = range.maxValue * (1f + GroundCell.LevelBonus * 2);
        Ok(ground.CurrentModifier == tile && ground.Level == 2 && Near(ground.RolledModifiers[0].value, expected, 1e-4f),
            $"hücrenin gerçek tile durumu: tür, Sv 2 ve değer {expected:0.####} (aralığın üst ucu × 1,5)");
        PlanterBrain brain = TestField.PlanterAt(P(C, C));
        Must(brain != null, "default planter at centre");
        Ok(brain.LocalModifiers.Count == tile.modifierRanges.Count, "saksının tile toplamı oyunun yeniden hesabından geldi");
        if (range.operation == ModifierOperation.Flat && range.statType == StatType.ExplosionChance)
            Ok(Near(brain.GetFinalStat(StatType.ExplosionChance), Mathf.Clamp01(expected), 1e-4f), $"saksının patlama şansı = tile değeri (sınırla): {Mathf.Clamp01(expected):0.####}");
        else Note($"tile range is {range.statType} {range.operation}: exact chance check skipped");
        Ok(Near(TestField.PlanterAt(P(C + 1, C)).GetFinalStat(StatType.ExplosionChance), 0f), "komşu saksı tile'dan etkilenmedi (etki ilgisiz saksıya sızmıyor)");
        Ok(!TestField.ApplyTile(P(0, 0), tile, .5f, 0).Ok && TestField.Ground(P(0, 0)).CurrentModifier == null, "kilitli hücreye tile reddedildi");
        Ok(TestField.ClearTile(P(C, C)).Ok && ground.CurrentModifier == null && brain.LocalModifiers.Count == 0 && Near(brain.GetFinalStat(StatType.ExplosionChance), 0f),
            "tile temizlenince saksının toplamı da temizlendi");

        PlanterSO small = LabCatalog.SmallestPlanter, big = LabCatalog.Planters.First(p => p.sizeX == 2 && p.sizeZ == 2), bar = LabCatalog.Planters.First(p => p.sizeX == 1 && p.sizeZ == 3);
        Ok(!TestField.PlacePlanter(small, P(C, C), 0, out _).Ok, "dolu hücreye saksı reddedildi (çakışma)");
        Ok(!TestField.PlacePlanter(small, P(0, 0), 0, out _).Ok, "kilitli hücreye saksı reddedildi");
        Ok(!TestField.PlacePlanter(small, P(40, 40), 0, out _).Ok, "tarla dışına saksı reddedildi");
        Ok(!TestField.PlacePlanter(big, P(8, 8), 0, out _).Ok && TestField.PlanterAt(P(8, 8)) == null, "açık alandan taşan 2 × 2 saksı reddedildi, yarım yerleşim kalmadı");
        Ok(TestField.PlacePlanter(big, P(2, 2), 0, out PlanterBrain b22).Ok, "2 × 2 saksı (2, 2) köşesine kuruldu");
        var cells = new[] { P(2, 2), P(3, 2), P(2, 3), P(3, 3) };
        Ok(b22.OccupiedGrids.Count == 4 && cells.All(c => TestField.PlanterAt(c) == b22), "saksı–hücre bağı: dört hücrenin sahibi aynı saksı");
        Vector3 centre = Vector3.zero;
        foreach (GridPosition c in cells) centre += TestAttack.WorldOf(c);
        centre /= 4f;
        Vector3 pos = b22.transform.position;
        Ok(Mathf.Abs(pos.x - centre.x) < .01f && Mathf.Abs(pos.z - centre.z) < .01f, "saksının görseli, mantıksal hücrelerinin ortasında");
        Ok(b22.EditorSpawners.Count > 0 && b22.EditorSpawners.All(s => b22.OccupiedGrids.Contains(s.GridObject)), "üretim noktaları saksının kendi hücrelerinde");

        Ok(TestField.PlacePlanter(bar, P(6, 2), 90, out PlanterBrain rotated).Ok, "1 × 3 saksı 90° döndürülerek kuruldu");
        var rotatedCells = rotated.OccupiedGrids.Select(g => g.GetGridPosition()).OrderBy(c => c.x).ThenBy(c => c.z).Select(c => $"{c.x},{c.z}").ToArray();
        Ok(string.Join(" ", rotatedCells) == "6,2 7,2 8,2" && rotated.EditorRotation == 90 && Near(Mathf.DeltaAngle(rotated.transform.eulerAngles.y, 90f), 0f, .5f),
            "döndürülmüş saksının hücreleri ve görsel yönü aynı: (6,2) (7,2) (8,2), 90°");
        Ok(!TestField.PlacePlanter(bar, P(7, 8), 90, out _).Ok, "döndürülünce açık alandan taşan saksı reddedildi");
        Ok(TestField.RemovePlanter(P(7, 2)).Ok && new[] { P(6, 2), P(7, 2), P(8, 2) }.All(c => TestField.PlanterAt(c) == null), "saksı kaldırıldı: üç hücre de boşaldı");
        Ok(ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == gold, "test kaldırması iade vermedi (kaynak değişmedi)");
        Ok(TestField.ReplacePlanter(P(2, 2), small, 0).Ok && TestField.PlanterAt(P(2, 2)).EditorData == small && TestField.PlanterAt(P(3, 3)) == null, "saksı aynı köşede başka türle değiştirildi");
        Ok(!TestField.ReplacePlanter(P(C, C), big, 0).Ok && TestField.PlanterAt(P(C, C)) != null && TestField.PlanterAt(P(C, C)).EditorData == small,
            "sığmayan değiştirme reddedildi ve eski saksı geri kuruldu");

        var table = new List<PlantSO>(); LabCatalog.PlantsOf(small, table);
        PlantSO rare = table.First(p => p.rarity >= PlantRarity.Rare);
        Ok(TestField.SetPlant(P(C, C), rare).Ok, "belirli bitki kondu: " + rare.name);
        PlantHealth plant = TestField.PlantAt(P(C, C));
        Ok(plant != null && plant.Data == rare && TestField.Cell(P(C, C)).GetPlantObject() == plant.gameObject && plant.Owner == TestField.PlanterAt(P(C, C)),
            "bitki oyunun durumunda: tür, hücre kaydı ve sahibi saksı");
        Vector3 plantPos = plant.transform.position, cellPos = TestAttack.WorldOf(P(C, C));
        Ok(Mathf.Abs(plantPos.x - cellPos.x) < .01f && Mathf.Abs(plantPos.z - cellPos.z) < .01f, "bitkinin görseli kendi hücresinde");
        Ok(plant.MaxHealth == PlantHealthCalculator.Calculate(rare, Rounds.CurrentRound), "bitkinin azami canı oyunun can eğrisinden (normal doğuş yolu)");
        Ok(TestField.SetPlantHealth(P(C, C), 1).Ok && plant.CurrentHealth == 1, "mevcut can 1 yapıldı");
        Ok(TestField.SetPlantHealth(P(C, C), 9999999).Ok && plant.CurrentHealth == plant.MaxHealth, "can azami canın üstüne çıkmadı");
        Ok(!TestField.SetPlant(P(3, 3), null).Ok, "saksısız hücreye bitki reddedildi (geçersiz yuva)");
        double xpBefore = ProgressionManager.Instance.TotalXPEarned;
        Ok(TestField.ClearPlant(P(C, C)).Ok && TestField.PlantAt(P(C, C)) == null && TestField.Cell(P(C, C)).GetPlantObject() == null &&
           ProgressionManager.Instance.TotalXPEarned == xpBefore, "bitki kaldırıldı: hücre boş, hasat sayılmadı (XP yok)");
        var planters = new List<PlanterBrain>(); TestField.Planters(planters);
        int points = planters.Sum(b => b.EditorSpawners.Count);
        Ok(TestField.FillPlants(null) == points && planters.All(b => b.EditorSpawners.All(s => s.SpawnedPlant != null)), $"yerleşim korunarak bütün bitkiler yenilendi ({points} nokta)");

        // Rezonans: oyunun kendi değerlendirmesi (araç yalnız tile ve saksı koyar).
        Ok(TestField.PlacePlanter(big, P(2, 6), 0, out PlanterBrain resonant).Ok, "2 × 2 saksı (2, 6) köşesine kuruldu");
        TileModifierSO damageTile = LabCatalog.Tile(TileModifierType.Damage, TileRarity.Epic);
        foreach (GridPosition c in new[] { P(2, 6), P(3, 6), P(2, 7), P(3, 7) }) Must(TestField.ApplyTile(c, damageTile, .5f, 0).Ok, "damage tile");
        resonant.EditorTileCounts.TryGetValue(TileModifierType.Damage, out int damageTiles);
        Ok(damageTiles == 4 && resonant.LocalModifiers.Count == 4 * damageTile.modifierRanges.Count, "saksının altındaki dört tile oyunun sayımında");
        var text = new StringBuilder(); TestInspector.DescribeResonance(resonant, text);
        Ok(resonant.ActiveResonances.All(r => text.ToString().Contains("[ETKİN] " + r.resonanceName)), $"inceleme, oyunun etkin rezonanslarını ({resonant.ActiveResonances.Count}) aynen gösteriyor");
        Note("resonance view: " + text.ToString().Replace("\n", " / "));
        return 0;
    }

    // ---------------------------------------------------------------- stat
    static double StatChecks()
    {
        int count0 = Stats.GlobalModifiers.Count;
        float damage0 = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), chance0 = TestField.PlanterAt(P(C, C)).GetFinalStat(StatType.ExplosionChance);
        Ok(TestStats.Add(Mod(StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, 1f)).Ok && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), damage0 * 2f, 1e-2f),
            $"test katkısı (hasar ×2) sonuca yansıdı: {damage0:0.##} → {damage0 * 2f:0.##}");
        Ok(Near(TestStats.WithoutTest(StatType.HarvestDamage, StatTarget.Player, TestStats.Base(StatType.HarvestDamage)), damage0, 1e-2f), "\"test'siz\" değer katkıdan önceki değerle aynı");
        Ok(Near(TestField.PlanterAt(P(C, C)).GetFinalStat(StatType.ExplosionChance), chance0), "doğrudan hasar katkısı davranış şansına sızmadı");

        Ok(TestRewards.Grant(Reward("kritik_goz")).Ok, "stat veren bir boss ödülü kuruldu (Kritik Göz)");
        var owned = Boss.OwnedModifiers.ToList();
        Must(owned.Count > 0, "Kritik Göz should own modifiers");
        Ok(TestStats.Add(Mod(StatType.CritChance, StatTarget.Player, ModifierOperation.Flat, .1f)).Ok && TestStats.Contributions.Count == 2, "ikinci test katkısı eklendi");
        var view = new StringBuilder(); TestInspector.ExplainStat(StatType.CritChance, StatTarget.Player, null, view);
        Ok(view.ToString().Contains("TEST KATKISI") && view.ToString().Contains("boss ödülü"), "stat dökümü test katkısını ve boss ödülünü ayrı kaynak olarak gösteriyor");
        Ok(TestStats.Clear().Ok && TestStats.Contributions.Count == 0 && Stats.GlobalModifiers.Count == count0 + owned.Count &&
           owned.All(m => TestStats.Contains(Stats.GlobalModifiers, m)) && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), damage0, 1e-3f),
            "\"test katkısını sıfırla\" yalnız test katkılarını kaldırdı (boss ödülünün modifier'ları yerinde)");

        // İnceleme = gerçek sonuç.
        foreach ((StatType stat, StatTarget target, string label) in TestStats.Offered)
        {
            if (target == StatTarget.Planter || !Stats.HasBaseStat(stat)) continue;
            view.Clear(); TestInspector.ExplainStat(stat, target, null, view);
            Must(view.ToString().Contains("SONUÇ (oyunun çözülmüş değeri): " + Stats.GetFinalStat(stat, target).ToString("0.####") + "\n"), "player explain " + stat + ": " + view);
        }
        Ok(true, "oyuncu kapsamlı her stat için inceleme sonucu = StatManager'ın çözülmüş değeri");
        PlanterBrain brain = TestField.PlanterAt(P(C, C));
        Must(TestField.ApplyTile(P(C, C), LabCatalog.Tile(TileModifierType.Explosive, TileRarity.Rare), .5f, 1).Ok, "explosive tile");
        Must(TestStats.Add(Mod(StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, .5f)).Ok, "planter test contribution");
        foreach ((StatType stat, StatTarget target, string label) in TestStats.Offered)
        {
            if (target != StatTarget.Planter) continue;
            view.Clear(); TestInspector.ExplainStat(stat, target, brain, view);
            Must(view.ToString().Contains("SONUÇ (oyunun çözülmüş değeri): " + brain.GetFinalStat(stat).ToString("0.####") + "\n"), "planter explain " + stat + ": " + view);
        }
        view.Clear(); TestInspector.ExplainStat(StatType.ExplosionChance, StatTarget.Planter, brain, view);
        Ok(view.ToString().Contains("Explosive-Rare") && view.ToString().Contains("TEST KATKISI"), "saksı kapsamlı her stat için inceleme sonucu = saksının çözülmüş değeri; tile ve test katkısı ayrı kaynak");
        Ok(Near(TestField.PlanterAt(P(C + 1, C)).GetFinalStat(StatType.ExplosionChance), 0f), "şans çarpanı katkısı, şansı olmayan saksıya şans vermedi");
        TestStats.Clear();

        // Sınırlar oyunun kurallarıyla sürer.
        Must(TestStats.Add(Mod(StatType.AttackSpeed, StatTarget.Player, ModifierOperation.Set, .001f)).Ok && TestStats.Add(Mod(StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 5f)).Ok, "extreme contributions");
        Ok(Near(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .05f) && Near(TestAttack.Interval, PlayerController.MinAttackInterval) &&
           Near(Stats.GetFinalStat(StatType.CritChance, StatTarget.Player), 1f), "uç test değerlerinde taban ve tavan korunuyor (saldırı aralığı 0,1 sn, kritik şansı %100)");
        TestStats.Clear();

        // XP: önizleme = gerçek akış.
        ProgressionManager progression = ProgressionManager.Instance;
        double xp = 0; for (int level = progression.CurrentLevel; level < progression.CurrentLevel + 3; level++) xp += progression.Data.GetXPForLevel(level);
        xp += 1 - progression.StoredXP;
        Ok(TestStats.PreviewXP(xp, 1000, out int levels, out int choices, out bool capped) && levels == 3 && choices == 3 * Rounds.ChoicesPerLevel && !capped,
            $"XP önizlemesi: {xp:0.#} XP → 3 level → {choices} kart seçim hakkı");
        int levelBefore = progression.CurrentLevel, pendingBefore = Rounds.PendingCardSelections;
        Ok(TestStats.GiveXP(xp).Ok && progression.CurrentLevel == levelBefore + levels && Rounds.PendingCardSelections == pendingBefore + choices,
            "XP normal akıştan verildi: kazanılan level ve seçim hakkı önizlemeyle aynı");
        int pending = Rounds.PendingCardSelections;
        Ok(TestStats.SetLevel(20).Ok && progression.CurrentLevel == 20 && Rounds.PendingCardSelections == pending && progression.StoredXP == 0d, "level ataması kart hakkı doğurmadı (ayrı işlem)");
        Ok(TestStats.SetResource(ResourceType.Gold, 12345).Ok && ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == 12345 &&
           TestStats.SetResource(ResourceType.Gold, 10).Ok && ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == 10, "kaynak miktarı artırıldı ve azaltıldı");
        return 0;
    }

    // ---------------------------------------------------------------- ödül
    static double RewardControlled()
    {
        var all = new List<BossRewardSO>(); TestRewards.Catalog(all);
        int expected = Boss.Pool.stages.Sum(s => s.rewards.Count);
        Ok(all.Count == expected && all.Count >= 30 && all.All(r => Boss.Pool.StageIndexOf(r) >= 0), $"ödül kataloğu gerçek havuzdan okundu: {all.Count} ödül, üç aşama");
        Ok(TestRewards.Grant(Reward("hasat_ritmi")).Ok && Boss.Stacks(Reward("hasat_ritmi")) == 1 && RunPower.Rhythm.Enabled, "kontrollü kurulum: Hasat Ritmi kuruldu ve oyunda etkin");
        Result again = TestRewards.Grant(Reward("hasat_ritmi"));
        Ok(!again.Ok && again.Message.Contains("stack") && Boss.Stacks(Reward("hasat_ritmi")) == 1, "stack sınırı aşılmadı: " + again.Message);
        Result excluded = TestRewards.Grant(Reward("zincir_hasat"));
        var conflicts = new List<BossRewardSO>(); TestRewards.Conflicts(Reward("zincir_hasat"), conflicts);
        Ok(!excluded.Ok && excluded.Message.Contains("dışlayan") && conflicts.Contains(Reward("hasat_ritmi")) && !BossRewardManager.TryGetChain(out _), "karşılıklı dışlama aşılmadı: " + excluded.Message);
        Result line = TestRewards.Grant(Reward("iletken_hat"));
        Ok(!line.Ok && line.Message.Contains("ön koşul"), "ön koşulsuz ödül reddedildi: " + line.Message);
        Result badSlot = TestRewards.Grant(Reward("elektrik_anteni"), 987654);
        Ok(!badSlot.Ok && badSlot.Message.Contains("geçerli değil") && Boss.Stacks(Reward("elektrik_anteni")) == 0, "geçersiz yerleşim konumu reddedildi, başka konuma taşınmadı");
        var report = new List<Result>();
        Ok(TestRewards.GrantWithPrerequisites(Reward("kapali_devre"), report).Ok && report.Count == 3 && Boss.Stacks(Reward("elektrik_anteni")) == 1 &&
           Boss.Stacks(Reward("iletken_hat")) == 1 && Boss.Stacks(Reward("kapali_devre")) == 1 && RunEffects.Instance.Active.Count == 3,
            "ön koşul zinciri sırayla kuruldu (anten → hat → devre): oyunun üç etkisi aktif");
        int choices = Rounds.ChoicesPerLevel;
        Ok(TestRewards.Grant(Reward("bereketli_ogrenim")).Ok && Rounds.ChoicesPerLevel == choices + 1 && BossRewardManager.DirectDamageMultiplier < 1f,
            $"bedelli ödül kazancı ve bedeliyle birlikte uygulandı: seçim hakkı {choices} → {choices + 1}, doğrudan vuruş ×{BossRewardManager.DirectDamageMultiplier:0.##}");
        Result trade = TestRewards.Grant(Reward("davranisa_adanis"));
        Ok(!trade.Ok && Rounds.ChoicesPerLevel == choices + 1, "dışlanan bedelli ödül reddedildi; seçim hakkı değişmedi: " + trade.Message);
        Ok(TestRewards.Describe(Reward("bereketli_ogrenim")).Contains("Dışlama grubu") && TestRewards.Describe(Reward("bereketli_ogrenim")).Contains("Bedel"), "ödül dökümü bedeli ve dışlama grubunu yazıyor");
        return 0;
    }

    static int tilesBeforeCards, cardsBefore;

    static double OfferOpen()
    {
        Result notBoss = TestRewards.OpenNormalOffer(4);
        Ok(!notBoss.Ok && !Boss.IsPending, "boss round'u olmayan round için teklif açılmadı: " + notBoss.Message);
        // Round atlama girişi bekleyen kart seçimlerini sıfırlar: XP, boss round'una gelindikten sonra verilir.
        Must(Rounds.EditorJumpToRound(3) && Rounds.PendingCardSelections == 0, "jump to boss round 3");
        ProgressionManager progression = ProgressionManager.Instance;
        double xp = progression.Data.GetXPForLevel(progression.CurrentLevel) + progression.Data.GetXPForLevel(progression.CurrentLevel + 1) + 1 - progression.StoredXP;
        Must(TestStats.GiveXP(xp).Ok && Rounds.PendingCardSelections == 2 * Rounds.ChoicesPerLevel, "two levels of pending card selections");
        tilesBeforeCards = TileCount();
        cardsBefore = Rounds.CardsTaken;
        Ok(TestRewards.OpenNormalOffer(3).Ok && Boss.IsPending && State == GameStates.CardSelection,
            $"R3 oyunun round sonu akışıyla bitti: önce {Rounds.PendingCardSelections} kart seçimi, boss teklifi sırada bekliyor");
        return 0;
    }

    static int TileCount()
    {
        int n = 0;
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++)
            for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
            { GroundCell g = TestField.Ground(P(x, z)); if (g != null && g.CurrentModifier != null) n++; }
        return n;
    }

    // Kart seçimi oyunun kart ekranının kendi seçim yoluyla yapılır (CardSelectionUI.OnCardSelected: uygulama + sayaç + sıradaki teklif).
    static double Cards()
    {
        var ui = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        Must(ui != null && ui.gameObject.activeInHierarchy, "card selection screen is open");
        FieldInfo cards = typeof(CardSelectionUI).GetField("currentCards", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo select = typeof(CardSelectionUI).GetMethod("OnCardSelected", BindingFlags.NonPublic | BindingFlags.Instance);
        int selections = Rounds.PendingCardSelections, guard = 0;
        while (State == GameStates.CardSelection && guard++ < 200)
        {
            var offer = (List<TileCardOffer>)cards.GetValue(ui);
            Must(offer.Count > 0, "card offer is shown");
            select.Invoke(ui, new object[] { offer[0] });
        }
        Ok(Rounds.PendingCardSelections == 0 && Rounds.CardsTaken == cardsBefore + selections && TileCount() == tilesBeforeCards + selections,
            $"oyunun kart ekranından {selections} kart alındı: her biri oyunun rastgele yerleşim kuralıyla yeni bir tile oldu");
        Ok(State == GameStates.RoundChoice && Boss.IsPending, "kartlardan sonra oyun boss ödülü seçimine geçti");
        return 0;
    }

    static double OfferChoose()
    {
        IReadOnlyList<BossRewardSO> offer = Boss.Offer;
        Ok(offer.Count > 0 && offer.Count <= Boss.Pool.choices && offer.Distinct().Count() == offer.Count, $"oyunun teklif üreticisi {offer.Count} tekrarsız aday çıkardı");
        Ok(offer.All(r => Boss.Pool.StageIndexOf(r) <= Boss.Pool.StageIndexAt(3) && Boss.BlockOf(r, true) == BossRewardBlock.None && Boss.Stacks(r) < r.maxStacks),
            "teklifteki her aday R3 aşamasında ve alınabilir (aşama, stack, dışlama, koşul kuralları)");
        Note("R3 offer: " + string.Join(", ", offer.Select(r => r.displayName)));
        BossRewardSO pick = offer[0];
        int placement = RunEffectSO.NoPlacement, before = Boss.Stacks(pick);
        if (Boss.NeedsPlacement(pick))
        {
            var slots = new List<int>(); TestRewards.Placements(pick, slots);
            Must(Boss.BeginPlacement(pick) && slots.Count > 0, "placement step");
            placement = slots[0];
        }
        Ok(Boss.Choose(pick, placement) && Boss.Stacks(pick) == before + 1 && !Boss.IsPending && State == GameStates.RoundEnd, "ödül oyunun normal edinme yolundan alındı: " + pick.displayName);
        Ok(!Boss.Choose(pick, placement), "kapanan teklif ikinci kez seçilemedi");
        Must(Rounds.EditorJumpToRound(3) && Rounds.IsRoundActive, "back to an active round");
        return 0;
    }

    // ---------------------------------------------------------------- senaryo
    static string signature, savedPath;

    static string Signature(Scenario s)
    {
        var text = new StringBuilder();
        text.Append($"{s.profile}|seed {s.seed}|R{s.round}|grid {s.gridSize}|level {s.level}|gold {s.gold} iron {s.iron} stone {s.stone}|boss {s.boss}|auto {s.autoAttack} frozen {s.freezeRoundTimer} paused {s.pauseProduction}\n");
        foreach (ScenarioTile t in s.tiles.OrderBy(t => t.x).ThenBy(t => t.z))
            text.Append($"T {t.x},{t.z} {t.type} {t.rarity} Sv{t.level} {string.Join("/", t.values.Select(v => v.ToString("0.###")))}\n");
        foreach (ScenarioPlanter p in s.planters.OrderBy(p => p.x).ThenBy(p => p.z)) text.Append($"P {p.planter} {p.x},{p.z} {p.rotation}\n");
        foreach (ScenarioPlant p in s.plants.OrderBy(p => p.x).ThenBy(p => p.z)) text.Append($"B {p.x},{p.z} {p.plant} {p.health}\n");
        foreach (ScenarioStat e in s.stats.OrderBy(e => e.stat).ThenBy(e => e.value)) text.Append($"S {e.stat} {e.target} {e.operation} {e.value:0.###}\n");
        foreach (ScenarioReward r in s.rewards.OrderBy(r => r.id)) text.Append($"R {r.id} x{r.stacks}\n");
        return text.ToString();
    }

    static double SaveLoad()
    {
        Must(TestStats.Add(Mod(StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, .25f)).Ok, "a test contribution to save");
        Must(TestField.SetPlantHealth(P(C, C), 1).Ok || TestField.SetPlant(P(C, C), null).Ok, "a plant to save");
        TestField.SetPlantHealth(P(C, C), 1);
        Scenario captured = ScenarioRunner.Capture(TestSession.Current);
        captured.name = "dogrulama-gidis-donus";
        signature = Signature(captured);
        savedPath = ScenarioFiles.PathFor(captured.name);
        Ok(ScenarioFiles.Save(captured, savedPath).Ok && File.Exists(savedPath) && !savedPath.Replace('\\', '/').Contains("/Assets/"), "senaryo Assets dışındaki bir dosyaya kaydedildi");
        Ok(captured.planters.Count == PlanterCount() && captured.tiles.Count == TileCount() && captured.rewards.Count == Boss.Taken.Count && captured.stats.Count == 1 && captured.round == 3,
            $"yakalanan düzen: {captured.planters.Count} saksı, {captured.tiles.Count} tile, {captured.plants.Count} bitki, {captured.rewards.Count} ödül, 1 test katkısı, R3");
        Ok(TestField.ClearField().Ok && PlanterCount() == 0 && TileCount() == 0, "tarla temizlendi");
        Ok(ScenarioFiles.Load(savedPath, out Scenario loaded).Ok && Signature(loaded) == signature, "dosyadan okunan senaryo kaydedilenle aynı");
        Must(TestSession.Rebuild(loaded).Ok, "rebuild loaded scenario");
        return 0;
    }

    static double SaveLoadCheck()
    {
        string now = Signature(ScenarioRunner.Capture(TestSession.Current));
        if (now != signature) Note("EXPECTED\n" + signature + "ACTUAL\n" + now);
        Ok(now.Contains("|boss none|") && SegmentEventDirector.Instance.Active == null, "boss round'unda bitmiş boss \"boss yok\" olarak yakalandı ve öyle geri kuruldu");
        Ok(now == signature, "kaydet → temizle → yükle: temiz oturumda aynı başlangıç düzeni kuruldu (tile, saksı, bitki, stat, ödül, round, level, kaynak)");
        File.Delete(savedPath);
        return 0;
    }

    static double Reinstall()
    {
        BossRewardSO chain = Reward("zincir_hasat");
        Must(Boss.BlockOf(chain, true) == BossRewardBlock.Exclusive, "Zincir Hasat is excluded by Hasat Ritmi");
        var conflicts = new List<BossRewardSO>(); TestRewards.Conflicts(chain, conflicts);
        Scenario scenario = ScenarioRunner.Capture(TestSession.Current);
        foreach (BossRewardSO conflict in conflicts) scenario.rewards.RemoveAll(e => e.id == conflict.id);
        scenario.rewards.Add(new ScenarioReward { id = chain.id, stacks = 1 });
        rewardsBeforeReinstall = Boss.Taken.Count;
        Must(TestSession.Rebuild(scenario).Ok, "rebuild without conflict");
        return 0;
    }

    static int rewardsBeforeReinstall;

    static double ReinstallCheck()
    {
        Ok(Boss.Stacks(Reward("zincir_hasat")) == 1 && Boss.Stacks(Reward("hasat_ritmi")) == 0 && BossRewardManager.TryGetChain(out _) && !RunPower.Rhythm.Enabled &&
           Boss.Taken.Count == rewardsBeforeReinstall, "çakışan ödül listeden çıkarılıp temiz oturumda yeniden kuruldu: Zincir Hasat var, Hasat Ritmi yok, diğer ödüller aynı");
        return 0;
    }

    // ---------------------------------------------------------------- manuel saldırı
    static int attackEntries;

    static double ManualAttack()
    {
        PlanterBrain brain = TestField.PlanterAt(P(C, C));
        Ok(Near(brain.GetFinalStat(StatType.ExplosionChance), 1f) && Near(TestField.PlanterAt(P(C + 1, C)).GetFinalStat(StatType.ExplosionChance), 0f),
            "\"02 Patlama\": patlama şansı yalnız tile'ı olan merkez saksıda %100 (görünür test çarpanıyla)");
        PlantHealth centre = TestField.PlantAt(P(C, C));
        Ok(centre != null && centre.CurrentHealth == 1 && !TestAttack.AutoAttack && TestAttack.Aim.HasValue, "merkez bitkinin canı 1, otomatik saldırı kapalı, nişan merkezde sabit");
        var neighbours = new[] { P(C + 1, C), P(C - 1, C), P(C, C + 1), P(C, C - 1) };
        int[] before = neighbours.Select(c => TestField.PlantAt(c).CurrentHealth).ToArray();
        int diagonal = TestField.PlantAt(P(C + 1, C + 1)).CurrentHealth;
        int expected = brain.GetBehaviorDamage(PlanterBrain.HarvestDamage, DamageType.Explosion);
        int triggered = HarvestBehaviorStats.Triggered(DamageType.Explosion);
        double xp = ProgressionManager.Instance.TotalXPEarned;
        Vector3 aim = TestAttack.Aim.Value;
        Must(TestAttack.Cooldown > 0f, "attack cooldown has not elapsed right after setup");
        Result early = TestAttack.AttackOnce(aim);
        Ok(!early.Ok && early.Message.Contains("bekleme") && TestField.PlantAt(P(C, C)) != null, "bekleme dolmadan tek saldırı reddedildi ve nedeni yazıldı: " + early.Message);
        Ok(TestAttack.ResetCooldown().Ok && TestAttack.Cooldown <= 1e-4f, "bekleme ayrı test komutuyla dolduruldu");
        Ok(TestAttack.AttackOnce(aim).Ok, "tek gerçek doğrudan saldırı yapıldı");
        Ok(TestField.PlantAt(P(C, C)) == null && ProgressionManager.Instance.TotalXPEarned > xp, "merkez bitki gerçekten hasat edildi (ödül ve XP oyunun yolundan)");
        Ok(HarvestBehaviorStats.Triggered(DamageType.Explosion) == triggered + 1, "hasat, saksının patlamasını oyunun normal tetiğiyle başlattı");
        for (int i = 0; i < neighbours.Length; i++)
        {
            PlantHealth plant = TestField.PlantAt(neighbours[i]);
            int after = plant != null ? plant.CurrentHealth : 0;
            Must(after == Mathf.Max(0, before[i] - expected) || (plant == null && expected >= before[i]), $"neighbour {neighbours[i]} took {before[i] - after}, expected {expected}");
        }
        Ok(true, $"dört yan komşu, saksı incelemesindeki patlama hasarını aldı ({expected})");
        Ok(TestField.PlantAt(P(C + 1, C + 1)).CurrentHealth == diagonal, "çapraz komşu hasar almadı");
        Ok(Log(TestLog.Kind.Attack, "başladı").Count == 1 && Log(TestLog.Kind.Harvest, $"({C}, {C})").Count == 1 && Log(TestLog.Kind.Behavior, "Patlama").Count == 1 &&
           Log(TestLog.Kind.Damage, "Patlama →").Count == 4 && Log(TestLog.Kind.Damage, "Doğrudan vuruş →").Count == 1,
            "olay kaydı: 1 saldırı, 1 doğrudan vuruş, 1 hasat, 1 başlayan davranış, 4 patlama vuruşu");
        int serial = Log(TestLog.Kind.Attack)[0].Attack;
        var loose = Log(TestLog.Kind.All).Where(e => e.Kind != TestLog.Kind.Tool && e.Kind != TestLog.Kind.Stat && (e.Attack != serial || !e.Linked)).ToList();
        foreach (TestLog.Entry entry in loose) Note("NOT LINKED: " + entry.Kind + " · " + TestLog.Format(entry));
        Ok(loose.Count == 0, "saldırının bütün olayları aynı saldırı kimliğine kesin bağlı");
        Result second = TestAttack.AttackOnce(aim);
        Ok(!second.Ok, "hemen ardından ikinci saldırı reddedildi (normal saldırı kuralı atlanmıyor)");
        attackEntries = Log(TestLog.Kind.Attack, "başladı").Count;
        return 0;
    }

    static double NoAutoAttack()
    {
        Ok(Log(TestLog.Kind.Attack, "başladı").Count == attackEntries && TestAttack.Cooldown <= 1e-4f, "otomatik saldırı kapalıyken 120 karede (4 sn) saldırı olmadı; bekleme oyun zamanıyla doldu");
        return 0;
    }

    // ---------------------------------------------------------------- zaman
    static float pauseTime, halfTime;

    static double PauseStart() { pauseTime = Time.time; return 0; }

    static double PauseAndStep()
    {
        Ok(Time.time == pauseTime && Time.timeScale == 0f, "duraklatılmışken oyun zamanı 10 karede hiç ilerlemedi");
        TestTime.StepSeconds = .05f;
        Ok(TestTime.Step().Ok, "tek adım istendi (50 ms)");
        return 0;
    }

    static double StepResult()
    {
        Ok(Near(Time.time - pauseTime, .05f, 1e-4f) && TestTime.StepsDone == 1 && Time.timeScale == 0f && Near(Time.captureDeltaTime, Dt, 1e-6f),
            $"tek adım tam bir kare ilerletti: +{(Time.time - pauseTime) * 1000f:0.#} ms, sonra yeniden durdu");
        pauseTime = Time.time;
        return 0;
    }

    static double HalfSpeed()
    {
        Ok(Time.time == pauseTime, "adımdan sonra zaman yine duruyor");
        TestTime.SetPaused(false);
        Ok(!TestTime.Step().Ok, "duraklatılmamışken tek adım reddedildi");
        TestTime.SetSpeed(.5f);
        return 0;
    }

    static double HalfStart() { halfTime = Time.time; Must(Near(Time.timeScale, .5f), "time scale 0.5 but is " + Time.timeScale); return 0; }

    static double HalfResult()
    {
        float perFrame = Time.deltaTime;
        Ok(Near(perFrame, Dt * .5f, 1e-5f) && Time.time > halfTime, $"yarı hızda kare başına oyun süresi yarıya indi ({perFrame * 1000f:0.##} ms)");
        TestTime.SetSpeed(1f);
        return 0;
    }

    // ---------------------------------------------------------------- gecikmeli etki
    static int echoSteps;

    static double EchoStart()
    {
        TestTime.SetPaused(true);
        Must(TestAttack.ResetCooldown().Ok && TestAttack.AttackOnce(TestAttack.Aim.Value).Ok, "attack while paused");
        BehaviorEchoes echoes = BehaviorEchoes.Instance;
        Ok(echoes.Pending == 1 && echoes.Scheduled(DamageType.Explosion) == 1 && echoes.Executed(DamageType.Explosion) == 0, "\"08\": gerçek patlama, Artçı Patlama'yı oyunun kuyruğuna planladı");
        echoSteps = 0;
        return 0;
    }

    static double EchoPaused()
    {
        Ok(BehaviorEchoes.Instance.Pending == 1 && BehaviorEchoes.Instance.Executed(DamageType.Explosion) == 0, "duraklatılmışken artçı 10 kare boyunca beklemede kaldı");
        TestTime.StepSeconds = Dt;
        return 0;
    }

    static double EchoStep()
    {
        BehaviorEchoes echoes = BehaviorEchoes.Instance;
        if (echoes.Pending > 0)
        {
            if (TestTime.Stepping) return Again;
            Must(echoSteps < 200, "aftershock never executed");
            if (Time.timeScale == 0f && TestTime.Step().Ok) echoSteps++;
            return Again;
        }
        float delay = Reward("artci_patlama").echoDelay, stepped = echoSteps * Dt;
        Ok(echoes.Executed(DamageType.Explosion) == 1 && Mathf.Abs(stepped - delay) <= Dt + 1e-4f,
            $"artçı, tek adımlarla tam gecikmesinde uygulandı: {echoSteps} adım × {Dt * 1000f:0.#} ms = {stepped:0.###} sn (ödül verisi {delay:0.###} sn)");
        Ok(Log(TestLog.Kind.Extra, "Artçı Patlama planlandı").Count == 1 && Log(TestLog.Kind.Extra, "Artçı Patlama uygulandı").Count == 1, "olay kaydı artçının planlanmasını ve uygulanmasını yazdı");
        TestTime.SetPaused(false);
        return 0;
    }

    // ---------------------------------------------------------------- hareketli davranış
    static Tornado tornado;
    static Vector3 tornadoStart, tornadoPaused;

    static double TornadoStart()
    {
        Must(TestAttack.ResetCooldown().Ok && TestAttack.AttackOnce(TestAttack.Aim.Value).Ok, "attack");
        Ok(TornadoManager.Instance.ActiveCount == 1, "\"04\": hasat, oyunun kasırgasını başlattı (aktif 1)");
        tornado = Object.FindObjectsByType<Tornado>(FindObjectsSortMode.None).FirstOrDefault(t => t.isActiveAndEnabled);
        Must(tornado != null, "active tornado object");
        tornadoStart = tornado.transform.position;
        return 0;
    }

    static double TornadoMoving()
    {
        Ok(TornadoManager.Instance.ActiveCount == 1 && Vector3.Distance(tornado.transform.position, tornadoStart) > .05f, "kasırga oyunun zamanıyla hareket ediyor");
        TestTime.SetPaused(true);
        return 0;
    }

    static double TornadoPausedStart() { tornadoPaused = tornado.transform.position; return 0; }

    static double TornadoPaused()
    {
        Ok(tornado.transform.position == tornadoPaused && TornadoManager.Instance.ActiveCount == 1, "duraklatılmışken kasırga 10 kare boyunca yerinde durdu");
        TestTime.SetPaused(false);
        return 0;
    }

    static double TornadoEnds()
    {
        if (TornadoManager.Instance.ActiveCount > 0) return Again;
        Ok(true, "kasırga kendi ömrüyle bitti; aktif sayı 0");
        Note("tornado hits in log: " + Log(TestLog.Kind.Damage, "Kasırga →").Count);
        return 0;
    }

    // ---------------------------------------------------------------- tarla olayı
    static FieldEventPhase pausedPhase;
    static float pausedPhaseTime;

    static double Phase(FieldEventPhase phase)
    {
        FieldEvent current = FieldEventDirector.Instance.Current;
        Must(current != null, "field event ended before reaching " + phase);
        return current.Phase == phase ? 0 : Again;
    }

    static double EventStart()
    {
        FieldEventDirector director = FieldEventDirector.Instance;
        Must(!TestAttack.AutoAttack && TestAttack.Aim.HasValue, "manual fixed aim for the event preset");
        Ok(Rounds.CurrentRound == 27 && director.Current == null && PlanterCount() == 49 && SegmentEventDirector.Instance.Active == null,
            "\"18\": R27 (boss round'u değil), 7 × 7 dolu tarla, kendiliğinden olay ve boss yok");
        Result locked = TestEvents.StartEvent(FieldEventKind.ThornSiege, 0f, 1, P(0, 0));
        Ok(!locked.Ok && director.Current == null, "geçersiz konum: olay başlatılmadı, bölge başka yere taşınmadı — " + locked.Message);
        Result started = TestEvents.StartEvent(FieldEventKind.ThornSiege, .5f, 1, P(C, C));
        Ok(started.Ok && director.Current != null && director.Current.Kind == FieldEventKind.ThornSiege && director.Current.Zone(0).Any(c => c.x == C && c.z == C),
            "Diken Kuşatması oyunun yürütücüsüyle kuruldu; bölge seçilen hücreyi içeriyor — " + started.Message);
        Ok(director.Zones.BlockedCells == 0, "hazırlıkta üretim etkisi yok");
        return 0;
    }

    static double EventPauseStart()
    {
        pausedPhase = FieldEventDirector.Instance.Current.Phase; pausedPhaseTime = FieldEventDirector.Instance.Current.PhaseTime;
        return 0;
    }

    static double EventPaused()
    {
        FieldEvent current = FieldEventDirector.Instance.Current;
        Ok(current.Phase == pausedPhase && current.PhaseTime == pausedPhaseTime, "duraklatılmışken olayın fazı ve faz süresi ilerlemedi");
        TestTime.SetPaused(false);
        return 0;
    }

    static double EventTargets()
    {
        FieldEventDirector director = FieldEventDirector.Instance;
        FieldEvent current = director.Current;
        ThornSiegeConfig config = director.Plan.thornSiege;
        Ok(director.Zones.BlockedCells == current.Zone(0).Count && current.ZoneClosed(0), $"aktif fazda bölgenin {current.Zone(0).Count} hücresinde üretim kapalı (oyunun plan süreleriyle)");
        Ok(current.Targets.Count == 2 && current.Targets.All(t => t.Hittable && t.Durability == config.rootDurability), "iki kök vurulabilir; dayanıklılık plan verisinden");
        foreach (InterventionTarget target in current.Targets)
        {
            for (int i = 0; i < config.rootDurability; i++)
                Must(TestAttack.ResetCooldown().Ok && TestAttack.AttackOnce(TestAttack.WorldOf(target.Cell)).Ok, "attack root");
            Must(target.Broken, "root broken after " + config.rootDurability + " direct attacks");
        }
        Ok(current.Phase == FieldEventPhase.Resolving && current.ResolvedByPlayer && director.Zones.BlockedCells == 0,
            $"kökler yalnız gerçek doğrudan saldırılarla kırıldı ({config.rootDurability} saldırı / kök): bölge hemen açıldı (başarı)");
        Ok(Log(TestLog.Kind.Event, "TargetBroken").Count == 2, "olay kaydı iki kökün kırılmasını yazdı");
        return 0;
    }

    static double EventTimeoutStart()
    {
        Result started = TestEvents.StartEvent(FieldEventKind.ThornSiege, 0f, 2);
        Ok(started.Ok && FieldEventDirector.Instance.Current != null, "olay temizlendikten sonra yeniden başlatıldı");
        return 0;
    }

    static double EventTimeout()
    {
        FieldEvent current = FieldEventDirector.Instance.Current;
        Must(current != null, "event disappeared before resolving");
        if (current.Phase != FieldEventPhase.Resolving) return Again;
        Ok(!current.ResolvedByPlayer && FieldEventDirector.Instance.Zones.BlockedCells == 0, "hiç vurulmadan beklenince olay süre dolunca kendiliğinden çözüldü (başarısızlık yolu)");
        Ok(TestEvents.EndEvent().Ok && FieldEventDirector.Instance.Current == null, "olay oyunun temizlik yoluyla kapatıldı");
        return 0;
    }

    static double OtherEvents()
    {
        FieldEventDirector director = FieldEventDirector.Instance;
        foreach (FieldEventKind kind in new[] { FieldEventKind.InvasiveSprout, FieldEventKind.GraftedShell, FieldEventKind.WanderingBounty })
        {
            Result started = TestEvents.StartEvent(kind, 0f, 1);
            Must(started.Ok && director.Current != null && director.Current.Kind == kind, kind + ": " + started.Message);
            Must(TestEvents.EventStatus().Contains(director.Current.Title), "status shows " + kind);
        }
        Ok(director.Current.Kind == FieldEventKind.WanderingBounty, "İstilacı Filiz, Aşılı Kabuk ve Gezgin Bereket ayrı ayrı kuruldu; her yeni olay öncekini kapattı (aynı anda tek olay)");
        Result anchored = TestEvents.StartEvent(FieldEventKind.InvasiveSprout, 0f, 1, P(C + 1, C));
        Ok(anchored.Ok && director.Current.Targets[0].Cell.x == C + 1 && director.Current.Targets[0].Cell.z == C, "İstilacı Filiz'in ana filizi seçilen üretim noktasında");
        Ok(TestEvents.EndEvent().Ok && director.Current == null && director.Zones.BlockedCells == 0 && director.Zones.SlowedCells == 0, "olay temizlendi: üretim etkisi kalmadı");
        return 0;
    }

    static double Bosses()
    {
        var bosses = new List<SegmentEventSO>(); LabCatalog.Bosses(TestSession.Profile, bosses);
        Must(bosses.Count >= 3, "three bosses in the pool");
        SegmentEventDirector director = SegmentEventDirector.Instance;
        int period = Rounds.Calendar.PeriodOf(26);
        SegmentEventSO sourceFixed = TestSession.SourceProfile.bossCalendar[period - 1].boss;
        Result wrong = TestEvents.StartBoss(bosses[0], 25, true);
        Ok(!wrong.Ok, "boss round'u olmayan round'da boss başlatılmadı: " + wrong.Message);
        foreach (SegmentEventSO boss in bosses)
        {
            Result started = TestEvents.StartBoss(boss, 26, true);
            Must(started.Ok && director.Active != null && director.Active.Data == boss && director.Active.IsActive, boss.name + ": " + started.Message);
            Must(PlanterCount() == 49 && Rounds.CurrentRound == 26 && Rounds.IsRoundActive, "field kept for " + boss.name);
        }
        Ok(true, $"{bosses.Count} boss ({string.Join(", ", bosses.Select(b => b.displayName))}) aynı tarlada sırayla oyunun boss akışıyla etkinleşti");
        Ok(TestSession.SourceProfile.bossCalendar[period - 1].boss == sourceFixed, "boss ataması yalnız çalışma zamanı kopyasında: kaynak profilin takvimi değişmedi");
        Ok(TestEvents.ClearBoss().Ok && director.Active == null, "boss etkisi oyunun temizlik yoluyla kaldırıldı");
        return 0;
    }

    // ---------------------------------------------------------------- yeniden kurma
    static int baseModifiers, baseTaken, baseEffects, basePlanters, baseTiles, baseObservers;

    static int Observers() => ((Array)typeof(PlantHealth).GetField("damageObservers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length +
                              ((Array)typeof(AttackFeed).GetField("listeners", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length;

    static double RebuildBaseline()
    {
        baseModifiers = Stats.GlobalModifiers.Count; baseTaken = Boss.Taken.Sum(r => Boss.Stacks(r)); baseEffects = RunEffects.Instance.Active.Count;
        basePlanters = PlanterCount(); baseTiles = TileCount(); baseObservers = Observers();
        Ok(BossRewardManager.TryGetChain(out _) && baseTaken == 1 && basePlanters == 25 && baseTiles == 25, "\"07 Zincir Hasat\" kuruldu: 25 patlama saksısı, zincir ödülü");
        return 0;
    }

    // Bekleyen iş bırakıp (zincir, davranış) hemen yeniden kurar.
    static double RebuildDirty()
    {
        Must(TestAttack.ResetCooldown().Ok && TestAttack.AttackOnce(TestAttack.Aim.Value).Ok, "attack before rebuild");
        Note($"before rebuild: chain pending {HarvestChain.Instance.Pending}, explosions triggered {HarvestBehaviorStats.Triggered(DamageType.Explosion)}, log {TestLog.Count}");
        Must(TestSession.Rebuild().Ok, "rebuild");
        return 0;
    }

    static double RebuildStable()
    {
        Ok(Stats.GlobalModifiers.Count == baseModifiers && Boss.Taken.Sum(r => Boss.Stacks(r)) == baseTaken && RunEffects.Instance.Active.Count == baseEffects &&
           PlanterCount() == basePlanters && TileCount() == baseTiles, "üç yeniden kurmadan sonra stat modifier, ödül stack, etki, saksı ve tile sayıları aynı (birikme yok)");
        Ok(HarvestChain.Instance.Pending == 0 && BehaviorEchoes.Instance.Pending == 0 && TornadoManager.Instance.ActiveCount == 0 && HarvestBehaviorManager.Instance.ActiveBoomerangs == 0 &&
           RunEffects.Instance.PendingTimers == 0 && FieldEventDirector.Instance.Current == null && SegmentEventDirector.Instance.Active == null && !DirectAttackPause.Active &&
           RunPower.Rhythm.Count == 0 && (AreaOutlineFeedback.Instance == null || AreaOutlineFeedback.Instance.ActiveCount == 0),
            "yeni oturuma eski zincir, artçı, davranış, zamanlayıcı, olay, boss ya da alan çizgisi taşınmadı");
        Ok(Observers() == baseObservers && TestSession.Rebuilds >= 3, "gözlem ve saldırı dinleyicisi sayısı aynı (abonelik çoğalmadı)");
        Ok(Log(TestLog.Kind.Attack | TestLog.Kind.Damage | TestLog.Kind.Harvest).Count == 0, "yeniden kurma olay kaydını temizledi");
        Must(TestAttack.ResetCooldown().Ok && TestAttack.AttackOnce(TestAttack.Aim.Value).Ok, "attack after rebuilds");
        Ok(Log(TestLog.Kind.Attack, "başladı").Count == 1 && Log(TestLog.Kind.Damage, "Doğrudan vuruş →").Count == 1, "tek saldırı kayıtta bir kez görünüyor (çift abonelik yok)");
        return 0;
    }

    static double ChainLogged()
    {
        int fired = Log(TestLog.Kind.Chain, "Zincir tetiği çalıştı").Count, attempts = Log(TestLog.Kind.Chain, "Zincir denemesi").Count;
        Ok(attempts > 0 && fired > 0 && HarvestChain.Instance.Pending == 0, $"zincir gerçek patlama hasatlarıyla çalıştı ve kayda geçti: {attempts} deneme, {fired} tetik");
        Ok(Log(TestLog.Kind.Chain).All(e => e.Linked && e.Attack == Log(TestLog.Kind.Attack)[0].Attack), "zincir kayıtları kök saldırının kimliğine kesin bağlı");
        return 0;
    }

    // ---------------------------------------------------------------- hazır senaryolar
    static List<Scenario> presets;
    static int presetIndex;

    static double Presets()
    {
        if (presets == null)
        {
            presets = ScenarioPresets.All();
            presetIndex = 0;
            Must(TestSession.Rebuild(presets[0]).Ok, "rebuild preset 0");
            stepSince = EditorApplication.timeSinceStartup;
            return Again;
        }
        if (!TestSession.Ready) return Again;
        Scenario preset = presets[presetIndex];
        CheckPreset(preset);
        presetIndex++;
        stepSince = EditorApplication.timeSinceStartup;
        if (presetIndex >= presets.Count)
        {
            Ok(presets.Count >= 25, $"{presets.Count} hazır senaryonun hepsi temiz oturumda açıldı ve temel koşulunu kurdu");
            return 0;
        }
        Must(TestSession.Rebuild(presets[presetIndex]).Ok, "rebuild preset " + presetIndex);
        return Again;
    }

    static void CheckPreset(Scenario preset)
    {
        string name = preset.name;
        Must(TestSession.SetupReport.All(r => r.Ok), name + " setup failures: " + Failures());
        Must(!string.IsNullOrEmpty(preset.whatToDo) && !string.IsNullOrEmpty(preset.whatToSee) && !string.IsNullOrEmpty(preset.whatToCheck), name + " has its three descriptions");
        Must(Rounds.CurrentRound == preset.round && Rounds.IsRoundActive && State == GameStates.Round, name + " round");
        Must(TestField.GridSize == preset.gridSize && PlanterCount() == preset.planters.Count && TileCount() == preset.tiles.Count, name + $" field: grid {TestField.GridSize}, planters {PlanterCount()}, tiles {TileCount()}");
        Must(TestStats.Contributions.Count == preset.stats.Count, name + " visible test stats");
        foreach (ScenarioReward entry in preset.rewards) Must(Boss.Stacks(Reward(entry.id)) == entry.stacks, name + " reward " + entry.id);
        Must(Boss.Taken.Count == preset.rewards.Count, name + " has only its own rewards");
        Must(TestAttack.AutoAttack == preset.autoAttack && TestAttack.Aim.HasValue == preset.aimFixed && RoundManager.EditorFreezeTimer, name + " input flags");
        var planters = new List<PlanterBrain>(); TestField.Planters(planters);
        Must(planters.All(b => b.EditorSpawners.All(s => s.SpawnedPlant != null)), name + " every production point has a plant");
        if (preset.plantHealth > 0) Must(planters.All(b => b.EditorSpawners.All(s => s.SpawnedPlant.GetComponent<PlantHealth>().CurrentHealth == preset.plantHealth)), name + " plant health");
        foreach (ScenarioPlant plant in preset.plants) if (plant.health > 0) Must(TestField.PlantAt(P(plant.x, plant.z)).CurrentHealth == plant.health, name + " plant health at " + plant.x + "," + plant.z);
        string prefix = name.Substring(0, 2);
        PlanterBrain centre = TestField.PlanterAt(P(C, C));
        string condition = "kuruldu";
        switch (prefix)
        {
            case "02": Must(Near(centre.GetFinalStat(StatType.ExplosionChance), 1f), name); condition = "merkez saksı patlama %100"; break;
            case "03": Must(Near(centre.GetFinalStat(StatType.ElectricChance), 1f), name); condition = "merkez saksı elektrik %100"; break;
            case "04": Must(Near(centre.GetFinalStat(StatType.TornadoChance), 1f) && TornadoManager.Instance.HasCapacity, name); condition = "merkez saksı kasırga %100"; break;
            case "05": Must(Near(centre.GetFinalStat(StatType.BoomerangChance), 1f) && HarvestBehaviorManager.Instance.HasBoomerangCapacity, name); condition = "merkez saksı bumerang %100"; break;
            case "06": Must(RunPower.Rhythm.Enabled && RunPower.Rhythm.Threshold > 0 && !RunPower.Rhythm.Ready, name); condition = $"Hasat Ritmi etkin, eşik {RunPower.Rhythm.Threshold}"; break;
            case "07": Must(BossRewardManager.TryGetChain(out HarvestChainConfig chain) && planters.All(b => Near(b.GetFinalStat(StatType.ExplosionChance), 1f)), name); condition = "zincir etkin, 25 patlama saksısı"; break;
            case "08": Must(RunPower.TryGetEcho(DamageType.Explosion, out _) && RunPower.TryGetEcho(DamageType.Electric, out _) && Near(TestField.PlanterAt(P(C - 2, C - 2)).GetFinalStat(StatType.ElectricChance), 1f), name); condition = "artçı ve ikinci dalga etkin"; break;
            case "09": Must(RunEffects.Instance.Find<AntennaEffect>() != null && RunEffects.Instance.Find<ConductiveLineEffect>() != null && RunEffects.Instance.Active.Count == 3, name); condition = "anten, hat ve devre etkileri aktif"; break;
            case "10": Must(RunEffects.Instance.Find<CrescentEffect>() != null && RunEffects.Instance.Active.Count == 3, name); condition = "hilal, büyüyen ay ve dolunay etkileri aktif"; break;
            case "11": case "16": Must(Near(Stats.GetFinalStat(StatType.CritChance, StatTarget.Player), 1f) && RunEffects.Instance.Active.Count == 1, name); condition = "kritik şansı %100, etki aktif"; break;
            case "12": case "13": case "15": case "17": Must(RunEffects.Instance.Active.Count == 1, name); condition = "ödülün etkisi aktif"; break;
            case "14": Must(TestField.PlantAt(P(C, C)).Data.rarity >= PlantRarity.Rare && RunEffects.Instance.Active.Count == 1 && TestField.PlanterAt(P(C - 1, C)).GetFinalStat(StatType.ExplosionChance) > 0f, name); condition = "merkezde nadir bitki, çevrede davranışlı saksılar"; break;
            case "18": case "19": case "20": case "21": Must(FieldEventDirector.Instance.Plan != null && FieldEventDirector.Instance.Current == null && Rounds.CurrentRound >= FieldEventDirector.Instance.Plan.firstRound, name); condition = "R27, olay planı hazır, olay elle başlatılacak"; break;
            case "22":
                centre.EditorTileCounts.TryGetValue(TileModifierType.Damage, out int damage); centre.EditorTileCounts.TryGetValue(TileModifierType.Explosive, out int explosive);
                Must(damage == 2 && explosive == 2 && centre.OccupiedGrids.Count == 4 && TestField.Ground(P(C - 3, C + 2)).Level == 3, name); condition = "2 × 2 saksı altında 2 Damage + 2 Explosive, Sv 3 tile"; break;
            case "23": Must(Rounds.ChoicesPerLevel == Rounds.BaseChoicesPerLevel + 1 && Boss.BlockOf(Reward("davranisa_adanis"), true) == BossRewardBlock.Exclusive, name); condition = "seçim hakkı +1, Davranışa Adanış dışlanmış"; break;
            case "24": Must(planters.All(b => Near(b.GetFinalStat(StatType.TornadoChance), 1f) || Near(b.GetFinalStat(StatType.BoomerangChance), 1f)), name); condition = $"49 kasırga / bumerang saksısı; sınır {TornadoManager.Instance.ActiveLimit}"; break;
        }
        notes.Add($"ok: senaryo \"{name}\" açıldı — {condition}");
    }

    // ---------------------------------------------------------------- panel
    static double PanelSmoke()
    {
        int before = errors;
        LabWindow window = EditorWindow.GetWindow<LabWindow>();
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(LabWindow).GetField("hasSelection", flags).SetValue(window, true);
        typeof(LabWindow).GetField("anchor", flags).SetValue(window, P(C, C));
        typeof(LabWindow).GetField("corner", flags).SetValue(window, P(C + 1, C + 1));
        string drawn = "";
        try
        {
            for (int tab = 0; tab < 8; tab++)
            {
                typeof(LabWindow).GetField("tab", flags).SetValue(window, tab);
                window.Repaint();
                window.SendEvent(EditorGUIUtility.CommandEvent("MechanicsTestLabSmoke"));
                drawn += tab + " ";
            }
        }
        catch (Exception e) { Note("panel smoke could not send events in batch mode: " + e.GetType().Name + " " + e.Message); }
        window.Close();
        if (drawn.Trim().Split(' ').Length == 8 && errors == before) Ok(true, "panelin sekiz sekmesi canlı oturumda hata vermeden çizildi (olay gönderilerek)");
        else Note($"panel smoke incomplete: tabs drawn [{drawn.Trim()}], new errors {errors - before} — panel drawing is NOT verified by this run");
        return 0;
    }

    // ---------------------------------------------------------------- Play'den sonra
    static void AfterPlay()
    {
        Ok(!TestSession.Active && !TestSession.Ready && !RunProfileSelectionSO.HasSessionOverride && TestSession.Profile == null, "oturum kapandı: profil geçersiz kılması kaldırıldı");
        Ok(Time.timeScale == 1f && PlayerController.EditorAutoAttack && PlayerController.EditorAim == null && !PlantSpawner.EditorPauseProduction && !RoundManager.EditorFreezeTimer &&
           !LateGamePresenter.DebugOutlines && !TestLog.Enabled, "zaman ölçeği, saldırı / üretim / sayaç bayrakları ve test görünümleri geri yüklendi");
        Ok(EditorSceneManager.playModeStartScene == null, "Play başlangıç sahnesi ayarı bırakılmadı");
        if (StartPlayMode)
            Ok(SceneManager.GetActiveScene().path == Lab.GameScenePath && SceneManager.sceneCount == 1, "Play'den çıkınca açık sahne aynen geri geldi (GameScene); test sahnesi açık kalmadı");
        Ok(SaveStamp() == SessionState.GetString(Key + ".save", "?"), "gerçek oyuncu kayıt dosyası değişmedi (" + SaveStamp() + ")");
        string before = File.ReadAllText(HashFile), after = Hashes();
        if (before != after)
        {
            var a = before.Split('\n'); var b = after.Split('\n');
            foreach (string line in b.Except(a)) Note("CHANGED " + line);
        }
        Ok(before == after, $"{before.Split('\n').Length - 1} asset ve sahne dosyası ile Build Settings sahne listesi değişmedi (profil seçimi, profiller, ödüller, tile'lar, GameScene, DemoScene dahil)");
    }
}
