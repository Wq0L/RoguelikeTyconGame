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

// Batch (izole kopya): Bölüm 3.8 İŞLEV testi — geç oyun tarla olayları ve yeni boss ödülleri (Run50_GecOyunV1).
// Denge ölçümü ve insan Play testi DEĞİLDİR: tarla testin kurduğu sabit düzendir (11 × 11, her hücrede 1 × 1 saksı), bitkilerin
// canı ve oyuncu stat'ları elle verilir, vuruşlar oyunun saldırı yoluyla (PlayerController.AttackInRadius) yapılır. Olayların
// süreleri hızlı bir test planından gelir (oyun verisinin kopyası; yalnız süre ve dayanıklılık küçültülür); gerçek asset değerleri
// ayrıca veri adımında denetlenir. Ödüller oyunun teklif üreticisinden (BuildOffer) çıkan bir tekliften, oyunun Choose yoluyla alınır.
[InitializeOnLoad]
public static class GecOyunV1Verification
{
    const string Key = "GecOyunV1Verification";
    const string LogFile = "Logs/GecOyunV1Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 3801, N = 11;
    const double Again = double.NaN;
    const int Hp = 1000000;
    const float Dt = 1f / 30f;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static GecOyunV1Verification() { EditorApplication.update += Tick; }

    // Yalnız derleme denetimi: izole kopya derleniyorsa 0 ile çıkar.
    public static void CompileOnly() => EditorApplication.Exit(0);
    // Retain every original four-cell assertion, explicitly on the legacy profile rule.
    public static void RunLegacyCrescentBatch()
    {
        if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
        RunBatch();
        var profile=AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles+"Run50_GecOyunV1.asset");
        profile.crescentFullRows=false;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_GecOyunV1.asset");
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
        Unsubscribe();
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
        FieldEventDirector.PointerOverUi = null;
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
    static StatManager Stats => StatManager.Instance;
    static RunEffects Fx => RunEffects.Instance;
    static FieldEventDirector Events => FieldEventDirector.Instance;
    static FieldEventHud Hud => Object.FindFirstObjectByType<FieldEventHud>(FindObjectsInactive.Include);
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO G1 => Profile("Run50_GecOyunV1");
    static RunProfileSO A1 => Profile("Run50_AlphaDengeV1");
    static BossRewardPoolSO Pool => G1.bossRewards;
    static RunProfileSelectionSO Selection => AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardSO Find(string id) => RewardOfferLab.All(Pool).FirstOrDefault(r => r.id == id);
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    // Oyuncu stat'ları: hasat hasarı, vuruş yarıçapı (dünya birimi; hücre 2 birim) ve kritik şansı.
    static void Strike(float damage, float radius, float crit = 0f)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, crit, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
    }
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static double waitUntil = -1;
    // Oyun süresiyle bekleme (sabit kare adımı: 1 / 30 sn).
    static double Wait(double seconds)
    {
        if (waitUntil < 0) waitUntil = Time.timeAsDouble + seconds;
        if (Time.timeAsDouble < waitUntil) return Again;
        waitUntil = -1;
        return 0;
    }
    static void AddWait(double seconds) => Add($"bekle {seconds:0.##} sn", () => Wait(seconds));

    // ---------------------------------------------------------------- olay kayıtları
    sealed class Hit { public DamageType Type; public RewardSource Source; public bool Echo; public int Damage, X, Z, Frame; }
    static readonly List<Hit> hits = new();
    static readonly List<(DamageType type, RewardSource source, int x, int z)> harvests = new();
    static int xpEvents;
    static bool subscribed;

    static void OnDamaged(PlantHealth plant, int damage, DamageType type)
    {
        GridPosition p = Grid.GetGridPosition(plant.transform.position); HarvestLink link = plant.LastHitLink;
        hits.Add(new Hit { Type = type, Source = link.Source, Echo = link.Echo, Damage = damage, X = p.x, Z = p.z, Frame = Time.frameCount });
    }
    static void OnHarvested(PlantHealth plant)
    {
        GridPosition p = Grid.GetGridPosition(plant.transform.position);
        harvests.Add((plant.KilledBy, plant.KillLink.Source, p.x, p.z));
    }
    static void OnXP() => xpEvents++;
    static void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        PlantHealth.AnyDamaged += OnDamaged; PlantHealth.AnyHarvested += OnHarvested;
        ProgressionManager.Instance.OnXPChanged += OnXP;
    }
    static void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        PlantHealth.AnyDamaged -= OnDamaged; PlantHealth.AnyHarvested -= OnHarvested;
        if (ProgressionManager.Instance != null) ProgressionManager.Instance.OnXPChanged -= OnXP;
    }
    static void ResetLogs() { hits.Clear(); harvests.Clear(); }
    static List<Hit> Of(RewardSource source) => hits.Where(h => h.Type == DamageType.Reward && h.Source == source).ToList();
    static string Cells(IEnumerable<Hit> list) => string.Join(" ", list.Select(h => $"({h.X},{h.Z})"));

    // ---------------------------------------------------------------- tarla
    static GridObject Go(int x, int z) => Grid.GetGridObject(new GridPosition(x, z));
    static GroundCell CellAt(int x, int z) => Go(x, z).GetGroundCellCached();
    static Vector3 PosAt(int x, int z) => CellAt(x, z).transform.position;
    static PlantHealth PlantAt(int x, int z) { var p = Go(x, z).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static PlanterBrain BrainAt(int x, int z) => Go(x, z).GetPlanterBrain();
    static PlantSpawner SpawnerAt(int x, int z) => BrainAt(x, z).GetComponentsInChildren<PlantSpawner>(true).First(s => s.GridObject == Go(x, z));
    static IEnumerable<PlanterBrain> Brains => Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Where(b => b.IsPlaced);
    static void SetHealth(int x, int z, int health) => SetF(PlantAt(x, z), "currentHealth", health);
    static void Attack(int x, int z) => Call(Player, "AttackInRadius", PosAt(x, z));
    static void AttackAt(Vector3 position) => Call(Player, "AttackInRadius", position);

    // Bütün üretim noktalarına yeni bitki (verilen canla). spawnersOn: üretim noktaları kendi sayaçlarıyla da üretmeye devam eder.
    static void Refill(bool spawnersOn = false, int health = Hp)
    {
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        foreach (var brain in Brains)
            foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true))
            {
                spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); spawner.enabled = spawnersOn;
                var plant = spawner.SpawnedPlant.GetComponent<PlantHealth>();
                SetF(plant, "maxHealth", Hp); SetF(plant, "currentHealth", health);
            }
        ResetLogs();
    }
    static void Tile(int x, int z, string type, float chance)
    {
        var tile = RewardOfferLab.Tile(type);
        CellAt(x, z).ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = chance }).ToList());
    }
    static void ClearTiles()
    {
        foreach (var ground in RewardOfferLab.OpenCells()) if (ground.CurrentModifier != null) ground.ApplyModifier(null);
    }
    // Yeni "run" (ödüller açısından): bütün boss ödülleri ve etkileri oyunun kendi temizlik yoluyla kalkar.
    static void ResetRewards()
    {
        Boss.ClearAll();
        ClearTiles();
        Must(Fx.Active.Count == 0 && !AttackFeed.Listening && !DirectAttackPause.Active && Boss.Taken.Count == 0, "reward reset should leave no effect, listener or pause");
    }

    // Ödülü oyunun teklif üreticisinden çıkan bir tekliften alır: ödülün aşamasının ilk boss round'unda, ödülün sunulduğu ilk
    // seed'in teklifi kurulur ve oyunun Choose yolu çağrılır. Ödül hiçbir seed'de sunulmuyorsa false.
    static List<BossRewardSO> lastOffer;
    static bool Offered(string id, out List<BossRewardSO> offer, out int round)
    {
        var reward = Find(id);
        round = RM.Calendar.NextBossRound(Pool.stages[Pool.StageIndexOf(reward)].firstRound);
        for (int seed = 1; seed <= 300; seed++)
        {
            offer = RewardOfferLab.Offer(Pool, round, seed);
            if (offer.Contains(reward)) return true;
        }
        offer = null;
        return false;
    }
    static bool OfferTake(string id, int placement = RunEffectSO.NoPlacement)
    {
        if (!Offered(id, out var offer, out int round)) return false;
        lastOffer = offer;
        return Inject(offer, round, Find(id), placement);
    }
    static bool Inject(List<BossRewardSO> offer, int round, BossRewardSO reward, int placement = RunEffectSO.NoPlacement)
    {
        var list = F<List<BossRewardSO>>(Boss, "offer");
        list.Clear(); list.AddRange(offer);
        SetF(Boss, "offerRound", round); SetF(Boss, "offerPrepared", true); SetP(Boss, "IsPending", true);
        bool taken = Boss.Choose(reward, placement);
        if (!taken) { SetP(Boss, "IsPending", false); list.Clear(); SetF(Boss, "offerPrepared", false); Boss.CancelPlacement(); }
        return taken;
    }
    static void Take(string id, int placement = RunEffectSO.NoPlacement) => Must(OfferTake(id, placement), id + " should be offered by the normal offer builder and taken through Choose");

    static FieldEventPlanSO fast;
    static FieldEventPlanSO FastPlan()
    {
        if (fast != null) return fast;
        fast = Object.Instantiate(G1.fieldEvents);
        fast.thornSiege.timing = new FieldEventTiming { prepareSeconds = .2f, emergeSeconds = .2f, activeSeconds = 3f, resolveSeconds = .2f };
        fast.thornSiege.rootDurability = 3;
        fast.invasiveSprout.timing = new FieldEventTiming { prepareSeconds = .2f, emergeSeconds = .1f, activeSeconds = 6f, resolveSeconds = .2f };
        fast.invasiveSprout.mainDurability = 3; fast.invasiveSprout.spreadInterval = .2f; fast.invasiveSprout.spreadSeconds = 10f; fast.invasiveSprout.maxPieces = 5;
        fast.graftedShell.timing = new FieldEventTiming { prepareSeconds = .2f, emergeSeconds = .1f, activeSeconds = 3f, resolveSeconds = .2f };
        fast.graftedShell.targetDurability = 2; fast.graftedShell.choiceSeconds = 1.5f; fast.graftedShell.closedSeconds = 1f;
        fast.wanderingBounty.timing = new FieldEventTiming { prepareSeconds = .1f, emergeSeconds = .1f, activeSeconds = 8f, resolveSeconds = .2f };
        fast.wanderingBounty.moveInterval = .5f; fast.wanderingBounty.threshold = 3; fast.wanderingBounty.maxBlooms = 2;
        return fast;
    }
    static double UntilPhase(FieldEventPhase phase)
    {
        Must(Events.Current != null, "the field event ended before reaching " + phase);
        return Events.Current.Phase == phase ? 0 : Again;
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("sahne: R26 boss round'u, gerçek takvim", Lab);
        Add("veri: profil, havuz, plan", Data);
        Add("veri: eski içerik korunuyor", Legacy);
        Add("takvim kuralı ve rastgelelik ayrımı", Schedule);
        // --- tarla olayları
        Add("diken: kur", ThornStart);
        Add("diken: hazırlık", () => UntilPhase(FieldEventPhase.Preparing));
        Add("diken: hazırlıkta etki yok", ThornPreparing);
        Add("diken: ortaya çıkış", () => UntilPhase(FieldEventPhase.Emerging));
        Add("diken: hedef henüz vurulamaz", ThornEmerging);
        Add("diken: aktif", () => UntilPhase(FieldEventPhase.Active));
        Add("diken: üretim durdu", ThornActive);
        AddWait(1.2);
        Add("diken: bölge dışı üretim sürüyor", ThornProduction);
        Add("diken: kök kuralları", ThornRoots);
        Add("diken: duraklatma", ThornPause);
        Add("diken: duraklatma sonucu", ThornPauseResult);
        Add("diken: kökler kırıldı", ThornBreak);
        AddWait(.8);
        Add("diken: üretim kaldığı yerden", ThornReopened);
        Add("görüntü: diken aktif", ThornCaptureStart);
        Add("diken: aktif (görüntü)", () => UntilPhase(FieldEventPhase.Active));
        Add("görüntü al", () => { Must(Hud.IsShown && !Hud.LegendShown, "thorn status plate should show without the A / B legend"); Capture("G1_01_DikenKusatmasi_Aktif"); return .05; });
        AddWait(3.3);
        Add("diken: süre doldu", ThornTimeout);
        Add("çok hücreli saksı ve sayaçlı kapatma", MultiCell);
        AddWait(1.2);
        Add("çok hücreli saksı: sonuç", MultiCellResult);
        Add("boss kuralıyla üst üste", Overlap);
        Add("filiz: kur", SproutStart);
        Add("filiz: aktif", () => UntilPhase(FieldEventPhase.Active));
        AddWait(1.3);
        Add("filiz: yayılma ve sınırlar", SproutSpread);
        Add("filiz: aday yok", SproutNoCandidate);
        Add("filiz: aday yok (aktif)", () => UntilPhase(FieldEventPhase.Active));
        AddWait(.8);
        Add("filiz: aday yok sonucu", SproutNoCandidateResult);
        Add("kabuk: kur", () => ShellStart(31));
        Add("kabuk: aktif", () => UntilPhase(FieldEventPhase.Active));
        Add("kabuk: bölge açıklaması", ShellLegend);
        Add("kabuk: B kurtarıldı", ShellSaveB);
        AddWait(1.1);
        Add("kabuk: kapalı bölge açıldı", ShellReopened);
        Add("kabuk: varsayılan kur", () => ShellStart(32));
        Add("kabuk: aktif (varsayılan)", () => UntilPhase(FieldEventPhase.Active));
        AddWait(1.6);
        Add("kabuk: seçim yapılmadı", ShellDefault);
        Add("kabuk: çift temas kur", () => ShellStart(33));
        Add("kabuk: aktif (çift temas)", () => UntilPhase(FieldEventPhase.Active));
        Add("kabuk: aynı saldırı iki hedefe", ShellTie);
        Add("bereket: kur", BountyStart);
        Add("bereket: aktif", () => UntilPhase(FieldEventPhase.Active));
        Add("bereket: ilerleme ve canlanma", BountyBloom);
        AddWait(.7);
        Add("bereket: hareket", BountyMoves);
        // --- yeni boss ödülleri
        Add("ödül: ön koşullar", Prerequisites);
        Add("anten: al", AntennaTake);
        AddWait(2.6);
        Add("anten: vuruş", AntennaPulse);
        Add("hat: al", LineTake);
        AddWait(4.1);
        Add("hat: vuruş", LinePulse);
        Add("kapalı devre: al", CircuitTake);
        AddWait(4.6);
        Add("kapalı devre: ek vuruş", CircuitResult);
        Add("hilal: al ve say", CrescentTake);
        AddWait(.5);
        Add("hilal: dalga", CrescentResult);
        Add("büyüyen ay: al", GrowingTake);
        AddWait(.5);
        Add("büyüyen ay: büyüme ve sıfırlanma", GrowingResult);
        Add("dolunay: al", FullMoonTake);
        AddWait(1.2);
        Add("dolunay: ikinci dalga ve duraklama", FullMoonResult);
        Add("kritik ark", CritArc);
        Add("hasat yankısı: meteor ve bekleme", EchoCase);
        AddWait(.7);   // meteor düşüşü 0,55 sn
        Add("hasat yankısı: sonuç, merkez, tek hasat", EchoResult);
        AddWait(.7);
        Add("hasat yankısı: tek hücre sonucu, 6 hücre sınırı", EchoCap);
        AddWait(.7);
        Add("hasat yankısı: 6 hücre sonucu", EchoCapResult);
        Add("taşan hasar", Overflow);
        Add("nadir reaktör: çalış", ReactorCase);
        AddWait(4.2);
        Add("nadir reaktör: süre doldu", ReactorResult);
        Add("kritik patlama", CritBurst);
        Add("açgözlü hasat", Greedy);
        Add("aşırı yükleme: al", OverloadTake);
        AddWait(.5);
        Add("aşırı yükleme: ek proc", OverloadResult);
        AddWait(2.4);
        Add("aşırı yükleme: kasırga sınırı ve zincir", OverloadLimits);
        AddWait(.6);
        Add("aşırı yükleme: sınır sonucu", OverloadLimitsResult);
        Add("hasat ritmi ve dışlama", RhythmAndExclusion);
        AddWait(2.6);
        Add("hasat ritmi: sayaç", RhythmResult);
        Add("hepsi birden: al", AllTake);
        for (int i = 0; i < 24; i++) { int n = i; Add($"hepsi birden: saldırı {i + 1}", () => AllAttack(n)); }
        AddWait(1.5);
        Add("hepsi birden: sınırlar", AllResult);
        Add("performans: taban", () => Perf(false));
        Add("performans: 13 ödül + olay", () => Perf(true));
        Add("performans: sonuç", PerfResult);
        // --- temizlik
        Add("temizlik: round kayıpla biter", LossCase);
        Add("temizlik: kayıp sonucu", LossResult);
        Add("temizlik: ana menü", MenuCase);
        Add("sahne yeniden", () => Load(G1));
        Add("yeni run temiz", CleanStart);
        for (int r = 1; r <= 2; r++)
        {
            int round = r;
            Add($"R{r} başla", () => PlainRound(round));
            Add($"R{r} bitiş", () => PlainRoundEnd(round));
        }
        Add("R3 boss: başla", BossRound);
        Add("R3 boss: gerçek teklif", RealOffer);
        Add("görüntü: ödül ekranı", () => { Canvas.ForceUpdateCanvases(); Capture("G1_02_OdulEkrani_R3"); return .05; });
        Add("R3 boss: yerleşim adımı", PlacementBegin);
        Add("R3 boss: yerleşim paneli", PlacementPanel);
        Add("görüntü: yerleşim", () => { placement.SetHovered(AntennaSlots.Id(AntennaSlots.West, 5)); Capture("G1_03_AntenYerlesimi"); return .05; });
        Add("R3 boss: vazgeç ve yeniden", PlacementCancel);
        Add("R3 boss: yerleştir", PlacementConfirm);
        Add("R4: olay ve round sonu", NormalRoundCase);
        Add("R4: aktif", () => UntilPhase(FieldEventPhase.Active));
        Add("R4: süre biter", () => { SetP(RM, "RemainingTime", 0f); return .2; });
        Add("R4: round sonu temizliği", NormalRoundEnd);
        Add("kapanış", FinalCheck);
    }

    // ---------------------------------------------------------------- sahne
    static FieldEventKind scheduledKind;
    static double Lab()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != G1) return Again;
        Player.enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(N);
        var open = RewardOfferLab.OpenCells();
        Must(open.Count == N * N && open.Min(c => c.GetGridPosition().x) == 0 && open.Min(c => c.GetGridPosition().z) == 0, $"open cells {open.Count}");
        RewardOfferLab.Build(RewardOfferLab.Layout.Direct);
        Must(Events != null && Events.Current == null && Events.Plan == G1.fieldEvents && Fx != null, "director and effects should exist with the profile's plan");
        // Round 26'dan başlanır (ilk round hazırlığında round numarası verilir): boss round'unun gerçek başlangıç yolu.
        SetP(RM, "CurrentRound", 26);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive && RM.CurrentRound == 26 && RM.IsBossRound(26), "round 26 should be running as a boss round");
        FieldEvent current = Events.Current;
        Require(current != null && current.Kind != FieldEventKind.WanderingBounty && Events.Started == 1 && current.Phase == FieldEventPhase.Waiting &&
                SegmentEventDirector.Instance.Active != null && SegmentEventDirector.Instance.Active.IsActive,
            $"Round 26 (real calendar boss round) starts through the normal round path: a boss field event is scheduled ({current?.Title}) while the boss rule ({SegmentEventDirector.Instance.Active?.Data.displayName}) is active — the two layers coexist");
        scheduledKind = current.Kind;
        Events.End();
        // Laboratuvar: boss kuralı (Sis yarıçapı, Don yavaşlatması, Sert Kabuk canı) sonraki sabit düzenleri etkilemesin diye kaldırılır.
        // Birlikte çalışma yukarıda ve "boss kuralıyla üst üste" adımında denetlenir.
        SegmentEventDirector.Instance.ClearAll();
        Require(Events.Current == null && Events.Zones.BlockedCells == 0 && Events.Zones.SlowedCells == 0 && !AttackFeed.Listening, "Ending the event leaves no production effect and no attack listener");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f; Time.captureDeltaTime = Dt;
        Strike(100f, .4f);
        Subscribe();
        Refill(true);
        return .05;
    }

    // ---------------------------------------------------------------- veri
    static readonly string[] NewIds =
    {
        "elektrik_anteni", "hilal_darbesi", "iletken_hat", "buyuyen_ay", "kritik_ark", "hasat_yankisi", "tasan_hasar", "nadir_reaktor",
        "acgozlu_hasat", "kapali_devre", "dolunay", "asiri_yukleme",
    };
    static readonly (string id, string stage)[] Protected =
    {
        ("keskin_bicak", "erken"), ("hizli_bilek", "erken"), ("bereketli_toprak", "erken"), ("nadir_tohum", "erken"), ("bilgi_filizi", "erken"),
        ("kivilcim", "orta"), ("agir_darbe", "orta"), ("kritik_goz", "orta"), ("altin_hedef", "orta"), ("yikim_gucu", "orta"),
        ("genis_savurus", "guclu"), ("canavar_kesimi", "guclu"), ("firtina_bilegi", "guclu"), ("artci_patlama", "guclu"), ("cifte_akim", "guclu"),
        ("hasat_ritmi", "guclu"), ("bereketli_ogrenim", "guclu"), ("davranisa_adanis", "guclu"), ("zincir_hasat", "guclu"),
    };

    static double Data()
    {
        var g = G1; var a = A1;
        Require(g != a && g.runLength == 50 && g.balance == a.balance && g.bossPool == a.bossPool && g.choicesPerLevel == a.choicesPerLevel &&
                g.segmentTargets.SequenceEqual(a.segmentTargets) && g.bossTargets.SequenceEqual(a.bossTargets) && g.startingGold == a.startingGold &&
                g.bossCalendar.Select(d => d.round).SequenceEqual(a.bossCalendar.Select(d => d.round)) &&
                g.roundDurations.Select(b => (b.fromRound, b.seconds)).SequenceEqual(a.roundDurations.Select(b => (b.fromRound, b.seconds))) &&
                g.bossRewards != a.bossRewards && g.fieldEvents != null && a.fieldEvents == null && RunCalendar.Validate(g) == null && RoundDurations.Validate(g) == null,
            "Profile Run50_GecOyunV1: Alpha Denge V1's run unchanged (50 rounds, 15 boss dates, quota and boss targets, round durations, 3 choices, the SAME balance asset: plant health, XP, base cards, moving limit 12); only the reward pool and the field event plan differ");
        var gp = g.bossRewards; var ap = a.bossRewards;
        var gAll = RewardOfferLab.All(gp); var aAll = RewardOfferLab.All(ap);
        Require(BossRewardPoolSO.Validate(gp, 50) == null && gp.choices == ap.choices && gp.reserveCurrentStageSlot == ap.reserveCurrentStageSlot &&
                gp.stageDistanceWeights.SequenceEqual(ap.stageDistanceWeights) && gp.stages.Count == 3 &&
                Enumerable.Range(0, 3).All(i => gp.stages[i].firstRound == ap.stages[i].firstRound && gp.stages[i].id == ap.stages[i].id &&
                    gp.stages[i].rewards.Take(ap.stages[i].rewards.Count).Select(e => (e.reward, e.note)).SequenceEqual(ap.stages[i].rewards.Select(e => (e.reward, e.note)))),
            "Pool: valid; the same stages, slot and weight policy; every Alpha reward is the SAME asset in the same order with the same note at the start of its stage");
        Require(aAll.Count == 19 && gAll.Count == 31 && Protected.All(p => { var r = gAll.FirstOrDefault(x => x.id == p.id); return r != null && aAll.Contains(r) && gp.stages[gp.StageIndexOf(r)].id == p.stage; }),
            "Pool: all 19 existing rewards kept in their stages — the 13 listed (Keskin Bıçak, Kritik Göz, Ağır Darbe, Hızlı Bilek, Fırtına Bileği, Geniş Savuruş, Altın Hedef, Nadir Tohum, Bilgi Filizi, Bereketli Toprak, Kıvılcım, Yıkım Gücü, Canavar Kesimi) plus Artçı Patlama, Çifte Akım, Hasat Ritmi, Bereketli Öğrenim, Davranışa Adanış, Zincir Hasat");
        var added = gAll.Where(r => !aAll.Contains(r)).ToList();
        Require(added.Count == 12 && added.Select(r => r.id).OrderBy(s => s).SequenceEqual(NewIds.OrderBy(s => s)) &&
                added.All(r => r.effect != null && r.maxStacks == 1 && r.weight == 1f && !r.HasCost && !r.IsBreakthrough && r.modifiersPerStack.Count == 0 && r.directDamageMultiplier == 1f &&
                               r.behaviorDamageMultiplier == 1f && r.levelChoiceDelta == 0 && string.IsNullOrEmpty(r.exclusiveGroup) && r.movingBehaviorLimitBonus == 0),
            "Pool: exactly 12 new rewards, each with a runtime effect, taken once, weight 1, no stat or multiplier side effect");
        string Stage(string id) => gp.stages[gp.StageIndexOf(Find(id))].id;
        Require(Stage("elektrik_anteni") == "erken" && Stage("hilal_darbesi") == "erken" &&
                new[] { "iletken_hat", "buyuyen_ay", "kritik_ark", "hasat_yankisi", "tasan_hasar", "nadir_reaktor", "acgozlu_hasat" }.All(id => Stage(id) == "orta") &&
                new[] { "kapali_devre", "dolunay", "asiri_yukleme" }.All(id => Stage(id) == "guclu"),
            "Stages: early = Elektrik Anteni, Hilal Darbesi; middle = İletken Hat, Büyüyen Ay, Kritik Ark, Hasat Yankısı, Taşan Hasar, Nadir Reaktör, Açgözlü Hasat; strong = Kapalı Devre, Dolunay, Aşırı Yükleme");
        Require(Find("iletken_hat").requires == Find("elektrik_anteni") && Find("kapali_devre").requires == Find("iletken_hat") &&
                Find("buyuyen_ay").requires == Find("hilal_darbesi") && Find("dolunay").requires == Find("buyuyen_ay") &&
                added.Count(r => r.requires != null) == 4 &&
                new[] { "nadir_reaktor", "kapali_devre", "asiri_yukleme" }.All(id => Find(id).condition == BossRewardCondition.AnyBehaviorPlanter) &&
                added.Count(r => r.condition != BossRewardCondition.Always) == 3,
            "Dependencies: İletken Hat → Elektrik Anteni, Kapalı Devre → İletken Hat, Büyüyen Ay → Hilal Darbesi, Dolunay → Büyüyen Ay; behavior-dependent rewards (Nadir Reaktör, Kapalı Devre, Aşırı Yükleme) need a behavior planter");
        Require(!gAll.Any(r => r.id.Contains("rezonans") || r.displayName.Contains("Rezonans") || r.displayName.Contains("Tetik Darbesi") || r.displayName.Contains("Şarjlı") || r.displayName.Contains("Zincir Reaksiyon")) &&
                gAll.Count(r => r.rhythmHarvests > 0) == 1 && gAll.Count(r => r.chain) == 1,
            "Out of scope stays out: no Rezonans Çöküşü / Rezonans Darbesi / Tetik Darbesi, no second copy of Hasat Ritmi (Şarjlı Savuruş) or Zincir Hasat (Zincir Reaksiyon)");
        var plan = g.fieldEvents;
        Require(FieldEventPlanSO.Validate(plan) == null && plan.firstRound == 26 && plan.pinned.Count == 0 && Near(plan.normalRoundChance, .5f) &&
                plan.bossRounds.Select(e => e.kind).OrderBy(k => k).SequenceEqual(new[] { FieldEventKind.ThornSiege, FieldEventKind.InvasiveSprout, FieldEventKind.GraftedShell }) &&
                plan.normalRounds.Select(e => e.kind).SequenceEqual(new[] { FieldEventKind.WanderingBounty }) &&
                plan.thornSiege.rootDurability == 6 && plan.thornSiege.zoneWidth == 3 && plan.thornSiege.zoneHeight == 3 && Near(plan.thornSiege.maxProductionShare, .4f) &&
                plan.invasiveSprout.maxPieces == 8 && plan.invasiveSprout.maxRadius == 2 && Near(plan.invasiveSprout.slowMultiplier, 1.6f) && plan.invasiveSprout.mainDurability == 8 &&
                Near(plan.graftedShell.choiceSeconds, 8f) && Near(plan.graftedShell.closedSeconds, 20f) && plan.graftedShell.targetDurability == 4 &&
                plan.wanderingBounty.threshold == 6 && plan.wanderingBounty.maxBlooms == 3 && plan.wanderingBounty.zoneSize == 3,
            "Field event plan (single data source): valid; nothing before Round 26; boss rounds = Diken Kuşatması / İstilacı Filiz / Aşılı Kabuk; other rounds = Gezgin Bereket at 50 %; prototype durations, areas and durabilities as generated");
        return .05;
    }

    static double Legacy()
    {
        var profiles = AssetDatabase.FindAssets("t:RunProfileSO").Select(x => AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(x))).ToList();
        var pools = AssetDatabase.FindAssets("t:BossRewardPoolSO").Select(x => AssetDatabase.LoadAssetAtPath<BossRewardPoolSO>(AssetDatabase.GUIDToAssetPath(x))).ToList();
        var rewards = AssetDatabase.FindAssets("t:BossRewardSO").Select(x => AssetDatabase.LoadAssetAtPath<BossRewardSO>(AssetDatabase.GUIDToAssetPath(x))).ToList();
        Require(profiles.Count == 17 && profiles.Where(p => p != G1).All(p => p.fieldEvents == null) &&
                pools.Where(p => p != Pool).All(p => !RewardOfferLab.All(p).Any(r => r.effect != null || r.requires != null)) &&
                rewards.Count(r => r.effect != null) == 13 && rewards.Where(r => r.effect == null).All(r => r.requires == null),
            $"Older profiles ({profiles.Count - 1}) have no field event plan; older pools ({pools.Count - 1}) contain no effect or prerequisite reward; the {rewards.Count - 13} older reward assets carry neither");
        var demo = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/DemoScene.unity");
        Require(demo != null && !DemoSceneSettings.IsDemo && A1.fieldEvents == null && RewardOfferLab.All(A1.bossRewards).All(r => r.effect == null),
            "Demo source (Run50_AlphaDengeV1) has no field event plan and no effect reward: the demo cannot schedule events or offer the new rewards (the director also refuses in a demo scene)");
        Require((int)DamageType.Direct == 0 && (int)DamageType.Electric == 4 && (int)DamageType.Reward == 5 && !DamageType.Reward.UsesPlanterBonus() && !DamageType.Reward.CanTriggerBehaviors(),
            "Damage types: the five old values keep their numbers; reward-effect damage is a sixth type that takes no planter bonus and triggers no behavior");
        return .05;
    }

    // ---------------------------------------------------------------- takvim
    static double Schedule()
    {
        var plan = G1.fieldEvents; var calendar = RM.Calendar;
        int[] bossRounds = Enumerable.Range(1, 50).Where(calendar.IsBossRound).ToArray();
        Must(bossRounds.SequenceEqual(new[] { 3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50 }), "boss calendar");
        int early = 0, bossPicks = 0, normalPicks = 0, normalRounds = 0, repeats = 0; bool wrongKind = false, same = true;
        for (int seed = 1; seed <= 40; seed++)
        {
            FieldEventKind? previous = null;
            for (int round = 1; round <= 50; round++)
            {
                bool boss = calendar.IsBossRound(round);
                bool picked = plan.TryPick(round, boss, new System.Random(SegmentEventDirector.Mix(seed, round, 0x46455654)), previous, out FieldEventKind kind);
                bool again = plan.TryPick(round, boss, new System.Random(SegmentEventDirector.Mix(seed, round, 0x46455654)), previous, out FieldEventKind kind2);
                same &= picked == again && (!picked || kind == kind2);
                if (round < 26) { if (picked) early++; continue; }
                if (boss) { if (picked) bossPicks++; wrongKind |= !picked || kind == FieldEventKind.WanderingBounty; if (picked && kind == previous) repeats++; }
                else { normalRounds++; if (picked) { normalPicks++; wrongKind |= kind != FieldEventKind.WanderingBounty; } }
                if (picked) previous = kind;
            }
        }
        float share = normalPicks / (float)normalRounds;
        Require(early == 0 && bossPicks == 40 * 8 && !wrongKind && same && repeats == 0 && share > .4f && share < .6f,
            $"Schedule rule over 40 seeds × 50 rounds: no event before Round 26; every boss round from R26 (26, 30, 33, 36, 40, 43, 46, 50) gets one of the three boss events, never the same as the event right before it; other rounds get only Gezgin Bereket, {share:P0} of the time (data: 50 %); the same seed and round always give the same pick");
        // Gerçek yol, başka round'lar: yürütücünün round başı işleyicisi (OnRoundChanged aboneliğinin çağırdığı).
        var before = RewardOfferLab.Offer(Pool, 26, Seed);
        UnityEngine.Random.State gameplay = UnityEngine.Random.state;
        Call(Events, "HandleRoundStarted", 25);
        bool none25 = Events.Current == null;
        Call(Events, "HandleRoundStarted", 30);
        bool boss30 = Events.Current != null && Events.Current.Kind != FieldEventKind.WanderingBounty;
        Events.End();
        int bounty = 0;
        for (int round = 27; round <= 29; round++) { Call(Events, "HandleRoundStarted", round); if (Events.Current != null) { Must(Events.Current.Kind == FieldEventKind.WanderingBounty, "normal round kind"); bounty++; } Events.End(); }
        bool sameRandom = gameplay.Equals(UnityEngine.Random.state);
        var after = RewardOfferLab.Offer(Pool, 26, Seed);
        Must(none25, "Round 25 must not schedule an event");
        Must(boss30, "Round 30 must schedule a boss event");
        Must(sameRandom, "scheduling events must not consume the gameplay random stream");
        Must(before.SequenceEqual(after), $"scheduling events must not change the reward offer ([{string.Join(", ", before.Select(r => r.id))}] → [{string.Join(", ", after.Select(r => r.id))}])");
        Require(none25 && boss30 && sameRandom && before.SequenceEqual(after),
            $"Director: Round 25 schedules nothing, Round 30 schedules a boss event, Rounds 27–29 schedule only Gezgin Bereket ({bounty}/3 with this seed); choosing and placing events consumes neither the gameplay random stream nor the reward offer stream (the R26 offer is identical before and after)");
        return .05;
    }

    // ---------------------------------------------------------------- Diken Kuşatması
    static ThornSiegeEvent thorn;
    static int planters0, zoneX, zoneZ, outX, outZ;
    static bool InZone(FieldEvent e, int x, int z, int index = 0) => e.Zone(index).Any(c => c.x == x && c.z == z);

    static double ThornStart()
    {
        ResetRewards();
        Refill(true);
        // Üretim aralığı en kısa değere çekilir (0,5 sn): üretimin durduğu ve sürdüğü birkaç saniyede görülebilsin.
        TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, .5f, ModifierOperation.Set));
        planters0 = Brains.Count();
        Must(Events.StartNow(FieldEventKind.ThornSiege, .1f, 11, FastPlan()), "thorn siege should start");
        thorn = (ThornSiegeEvent)Events.Current;
        var zone = thorn.Zone(0);
        Require(thorn.Phase == FieldEventPhase.Waiting && zone.Count == 9 && zone.All(c => c.x >= 0 && c.x < N && c.z >= 0 && c.z < N) && zone.Distinct().Count() == 9 &&
                thorn.Targets.Count == 2 && thorn.Targets.All(t => InZone(thorn, t.Cell.x, t.Cell.z) && t.Durability == 3 && !t.Hittable) &&
                Mathf.Abs(thorn.Targets[0].Cell.x - thorn.Targets[1].Cell.x) == 2 && Mathf.Abs(thorn.Targets[0].Cell.z - thorn.Targets[1].Cell.z) == 2 &&
                Events.Zones.BlockedCells == 0 && AttackFeed.Listening,
            "Diken Kuşatması is set up once: a 3 × 3 zone inside the open field (9 of 121 production points, never the whole field), two roots on opposite corners of the zone, nothing blocked yet");
        return 0;
    }

    static double ThornPreparing()
    {
        Require(thorn.Zone(0).All(c => !FieldEventDirector.BlocksSpawn(c)) && Events.Zones.BlockedCells == 0 && !thorn.ZoneClosed(0),
            "Preparation: the zone is marked but production is not affected yet");
        return 0;
    }

    static double ThornEmerging()
    {
        var root = thorn.Targets[0];
        Attack(root.Cell.x, root.Cell.z);
        Require(root.Remaining == 3 && Events.Zones.BlockedCells == 0, "Emergence: roots are not hittable before the active phase and production is still not affected");
        return 0;
    }

    static double ThornActive()
    {
        var zone = thorn.Zone(0);
        bool onlyZone = true;
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) onlyZone &= FieldEventDirector.BlocksSpawn(new GridPosition(x, z)) == InZone(thorn, x, z);
        Require(Events.Zones.BlockedCells == 9 && onlyZone && thorn.ZoneClosed(0) && thorn.Targets.All(t => t.Hittable),
            "Active: production is blocked in exactly the 9 zone cells and nowhere else; both roots are hittable");
        // Bölgenin ortasındaki (kök olmayan) hücre ile bölge dışındaki bir hücrenin bitkisi hasat edilir.
        zoneX = zone.Min(c => c.x) + 1; zoneZ = zone.Min(c => c.z) + 1;
        outX = zoneX >= 6 ? 0 : 10; outZ = zoneZ;
        SetHealth(zoneX, zoneZ, 1); SetHealth(outX, outZ, 1);
        Attack(zoneX, zoneZ); Attack(outX, outZ);
        Must(PlantAt(zoneX, zoneZ) == null && PlantAt(outX, outZ) == null && thorn.Targets.All(t => t.Remaining == 3), "both plants should be harvested without touching a root");
        return 0;
    }

    static double ThornProduction()
    {
        var zone = thorn.Zone(0);
        Require(PlantAt(outX, outZ) != null && PlantAt(zoneX, zoneZ) == null && F<float>(SpawnerAt(zoneX, zoneZ), "timer") == 0f &&
                zone.Count(c => PlantAt(c.x, c.z) != null) == 8 && Brains.Count() == planters0 && RewardOfferLab.OpenCells().All(c => c.CurrentModifier == null),
            "Active (1,2 s later): the production point outside the zone grew a new plant; the one inside did not and its timer did not accumulate; the other 8 plants of the zone, every planter and every tile are untouched");
        return 0;
    }

    static double ThornRoots()
    {
        var a = thorn.Targets[0]; var b = thorn.Targets[1];
        Attack(a.Cell.x, a.Cell.z);
        object report = typeof(AttackFeed).GetField("report", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        MethodInfo tryHit = typeof(InterventionTarget).GetMethod("TryHit", BindingFlags.NonPublic | BindingFlags.Instance);
        bool second = (bool)tryHit.Invoke(a, new[] { report, (object)FieldCells.CellSize });
        Require(a.Remaining == 2 && b.Remaining == 3 && !second && a.Remaining == 2,
            "A direct attack on a root's cell counts exactly once; asking the same attack again (as a second collider would) is refused by the attack serial");
        // Davranış ve ödül etkisi hasarı köke ulaşmaz: kökün hücresindeki bitki hasar alır, kök almaz.
        int plantBefore = PlantAt(a.Cell.x, a.Cell.z).CurrentHealth;
        var neighbor = BrainAt(a.Cell.x + (a.Cell.x < 10 ? 1 : -1), a.Cell.z);
        neighbor.Explode(PosAt(a.Cell.x, a.Cell.z), HarvestLink.None, 1f);
        HarvestBehaviorManager.Instance.TryElectric(BrainAt(a.Cell.x + (a.Cell.x < 9 ? 1 : -1), a.Cell.z + (a.Cell.z < 9 ? 1 : -1)), 100);
        Fx.Strike(a.Cell, 50, RewardSource.Antenna);
        Require(a.Remaining == 2 && b.Remaining == 3 && PlantAt(a.Cell.x, a.Cell.z).CurrentHealth < plantBefore,
            "Behavior damage (explosion, electricity) and reward-effect damage hit the plant standing on the root's cell but never the root: only the player's direct attack counts");
        FieldEventDirector.PointerOverUi = () => true;
        Attack(a.Cell.x, a.Cell.z);
        bool blocked = a.Remaining == 2;
        FieldEventDirector.PointerOverUi = () => false;
        Require(blocked, "With the pointer over a UI element the attack does not count against the root (no hitting targets through the UI)");
        // Tek saldırı iki köke birden değerse her biri bir kez sayılır.
        Strike(100f, 60f);
        AttackAt(PosAt(5, 5));
        Strike(100f, .4f);
        TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, .5f, ModifierOperation.Set));
        Require(a.Remaining == 1 && b.Remaining == 2, "One wide attack touching both roots counts once on each");
        return 0;
    }

    static float pausedAt;
    static double ThornPause()
    {
        pausedAt = thorn.PhaseTime;
        Time.timeScale = 0f;
        return .15;
    }
    static double ThornPauseResult()
    {
        bool frozen = thorn.PhaseTime == pausedAt && thorn.Phase == FieldEventPhase.Active;
        Time.timeScale = 1f;
        Require(frozen, "Pause (time scale 0): the event's phase clock does not advance while frames keep running");
        return 0;
    }

    static double ThornBreak()
    {
        var a = thorn.Targets[0]; var b = thorn.Targets[1];
        Attack(a.Cell.x, a.Cell.z);
        Require(a.Broken && !a.Hittable && thorn.Phase == FieldEventPhase.Active && Events.Zones.BlockedCells == 9 && thorn.RootsLeft == 1, "One root broken: the zone stays closed until both are broken");
        Attack(a.Cell.x, a.Cell.z);
        Must(a.Remaining == 0, "a broken root is not counted again");
        Attack(b.Cell.x, b.Cell.z); Attack(b.Cell.x, b.Cell.z);
        Require(b.Broken && thorn.Phase == FieldEventPhase.Resolving && thorn.ResolvedByPlayer && Events.Zones.BlockedCells == 0 && !thorn.ZoneClosed(0) &&
                thorn.Zone(0).All(c => !FieldEventDirector.BlocksSpawn(c)) && PlantAt(zoneX, zoneZ) == null,
            "Both roots broken: the zone opens at once (resolved by the player); nothing is paid back instantly — the empty production point is still empty this frame");
        return 0;
    }

    static double ThornReopened()
    {
        Require(Events.Current == null && PlantAt(zoneX, zoneZ) != null && Brains.Count() == planters0 && Go(zoneX, zoneZ).GetPlantObject() == SpawnerAt(zoneX, zoneZ).SpawnedPlant,
            "After the event: it cleaned itself up, the reopened production point produced one plant after its normal interval, and no planter was lost");
        return 0;
    }

    static double ThornCaptureStart()
    {
        ClearTestMods(); Strike(100f, .4f);
        Refill(true);
        Must(Events.StartNow(FieldEventKind.ThornSiege, 0f, 12, FastPlan()), "thorn siege (capture) should start");
        thorn = (ThornSiegeEvent)Events.Current;
        return 0;
    }

    static double ThornTimeout()
    {
        Require(Events.Current == null && thorn.Phase == FieldEventPhase.Done && !thorn.ResolvedByPlayer && thorn.RootsLeft == 2 && Events.Zones.BlockedCells == 0,
            "Untouched Diken Kuşatması: the active time runs out, the zone reopens by itself and the event cleans up (the closure is always finite)");
        return 0;
    }

    // ---------------------------------------------------------------- çok hücreli saksı, sayaçlar
    static PlanterBrain wide;
    static readonly List<GridPosition> half = new() { new GridPosition(4, 5) };
    static double MultiCell()
    {
        BrainAt(4, 5).RemoveSelf(); BrainAt(4, 6).RemoveSelf();
        wide = KirilmaErisimMeasurement.PlacePlanter(AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x2.asset"), CellAt(4, 5), null, 0f);
        Must(wide.OccupiedGrids.Count == 2 && BrainAt(4, 5) == wide && BrainAt(4, 6) == wide && wide.GetComponentsInChildren<PlantSpawner>(true).Length == 2, "1 × 2 planter with two production points");
        TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, .5f, ModifierOperation.Set));
        foreach (var spawner in wide.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; }
        // Yalnız (4, 5) kapatılır: iki ayrı kaynak aynı hücreyi kapatır.
        Events.Zones.Resize(N, N);
        Events.Zones.AddBlock(half); Events.Zones.AddBlock(half);
        Events.Zones.SetSlow(new GridPosition(4, 6), 1.6f); Events.Zones.SetSlow(new GridPosition(4, 6), 1.6f);
        return 0;
    }

    static double MultiCellResult()
    {
        bool oneBlocked = PlantAt(4, 5) == null && PlantAt(4, 6) != null;
        float slow = FieldEventDirector.SpawnIntervalMultiplier(new GridPosition(4, 6));
        Events.Zones.RemoveBlock(half);
        bool stillBlocked = FieldEventDirector.BlocksSpawn(new GridPosition(4, 5)) && Events.Zones.BlockedCells == 1;
        Events.Zones.RemoveBlock(half);
        bool open = !FieldEventDirector.BlocksSpawn(new GridPosition(4, 5)) && Events.Zones.BlockedCells == 0;
        Events.Zones.RemoveBlock(half);
        Events.Zones.ClearSlow(new GridPosition(4, 6));
        Require(oneBlocked && Near(slow, 1.6f) && stillBlocked && open && Events.Zones.BlockedCells == 0 && Events.Zones.SlowedCells == 0,
            "Multi-cell planter: the effect applies per production point, not per planter — the 1 × 2 planter's point inside the zone stopped while its other point kept producing; writing the same slow twice stays ×1,6 (not ×2,56); a cell blocked by two sources stays blocked until both remove it, and an extra removal cannot go below zero");
        // Tarla eski düzenine döner: 1 × 2 saksı kalkar, iki hücreye 1 × 1 saksı gelir.
        wide.RemoveSelf();
        RewardOfferLab.Place(CellAt(4, 5)); RewardOfferLab.Place(CellAt(4, 6));
        ClearTestMods(); Strike(100f, .4f);
        Refill(true);
        return 0;
    }

    // ---------------------------------------------------------------- boss kuralıyla üst üste
    static double Overlap()
    {
        var events = SegmentEventDirector.Instance;
        SegmentEventRuntime old = events.Active;
        var frostData = AssetDatabase.FindAssets("t:FrostFrontSO").Select(x => AssetDatabase.LoadAssetAtPath<FrostFrontSO>(AssetDatabase.GUIDToAssetPath(x))).First();
        var context = new SegmentEventContext { GridWidth = N, GridHeight = N };
        foreach (var ground in RewardOfferLab.OpenCells()) { context.OpenCells.Add(ground.GetGridPosition()); context.ProductionPoints.Add(ground.GetGridPosition()); }
        SegmentEventRuntime frost = frostData.CreateRuntime(SegmentEventTiming.BossRound(8, 24, 26, 4242));
        Must(frost.TryPrepare(context), "frost zone");
        frost.Activate();
        GridPosition cell = frost.ZoneCells[0];
        PlantSpawner spawner = SpawnerAt(cell.x, cell.z);
        float Interval() => (float)Call(spawner, "GetEffectiveSpawnInterval");
        SetP(events, "Active", null);
        float plain = Interval();
        SetP(events, "Active", frost);
        float frozen = Interval();
        Events.Zones.Resize(N, N);
        Events.Zones.SetSlow(cell, 1.6f);
        float both = Interval();
        SetP(events, "Active", null);
        float sproutOnly = Interval();
        SetP(events, "Active", frost);
        Events.Zones.ClearSlow(cell);
        float frostOnly = Interval();
        frost.Finish();
        SetP(events, "Active", old);
        float frostFactor = frostData.spawnIntervalMultiplier;
        Require(frostFactor > 1f && Near(frozen, plain * frostFactor) && Near(both, plain * frostFactor * 1.6f) && Near(sproutOnly, plain * 1.6f) && Near(frostOnly, plain * frostFactor),
            $"Overlap with a boss rule: on a cell slowed by Don Cephesi (×{frostFactor:0.##}) and by a field event (×1,6) the interval is {plain:0.##} → {both:0.##} sn; removing the boss rule leaves the field event's slow, removing the field event leaves the boss rule's — neither removes the other");
        return 0;
    }

    // ---------------------------------------------------------------- İstilacı Filiz
    static InvasiveSproutEvent sprout;
    static double SproutStart()
    {
        Refill(true);
        Must(Events.StartNow(FieldEventKind.InvasiveSprout, .05f, 21, FastPlan()), "sprout should start");
        sprout = (InvasiveSproutEvent)Events.Current;
        Require(sprout.Pieces == 1 && sprout.Targets.Count == 1 && PlantAt(sprout.Zone(0)[0].x, sprout.Zone(0)[0].z) != null && Events.Zones.SlowedCells == 0,
            "İstilacı Filiz is set up: the main sprout stands on a production point; no cell is slowed before the active phase");
        return 0;
    }

    static double SproutSpread()
    {
        var pieces = sprout.Zone(0); GridPosition main = pieces[0];
        bool bounded = pieces.All(c => Mathf.Max(Mathf.Abs(c.x - main.x), Mathf.Abs(c.z - main.z)) <= 2 && FieldCells.IsOpen(Grid, c)) && pieces.Distinct().Count() == pieces.Count;
        bool connected = Enumerable.Range(1, pieces.Count - 1).All(i => pieces.Take(i).Any(p => Mathf.Abs(p.x - pieces[i].x) + Mathf.Abs(p.z - pieces[i].z) == 1));
        bool slowed = true;
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++)
            slowed &= Near(FieldEventDirector.SpawnIntervalMultiplier(new GridPosition(x, z)), pieces.Any(c => c.x == x && c.z == z) ? 1.6f : 1f);
        Require(sprout.Pieces == 5 && !sprout.Spreading && bounded && connected && slowed && Events.Zones.SlowedCells == 5 && Events.Zones.BlockedCells == 0,
            "Spread: one new piece every interval, each next to an earlier piece, all within 2 cells of the main sprout, stopping at the piece limit (5 in the test plan); exactly those cells are slowed ×1,6, nothing is blocked");
        var target = sprout.Targets[0];
        Attack(main.x, main.z); Attack(main.x, main.z);
        Must(target.Remaining == 1 && Events.Zones.SlowedCells == 5, "two hits should leave one");
        Attack(main.x, main.z);
        Require(target.Broken && sprout.Phase == FieldEventPhase.Resolving && sprout.ResolvedByPlayer && Events.Zones.SlowedCells == 0,
            "Main sprout broken by direct attacks: every effect of this event is removed at once");
        return .4;
    }

    static double SproutNoCandidate()
    {
        var plan = Object.Instantiate(FastPlan());
        plan.invasiveSprout.maxRadius = 0;
        Must(Events.Current == null && Events.StartNow(FieldEventKind.InvasiveSprout, 0f, 22, plan), "sprout (no candidate) should start");
        sprout = (InvasiveSproutEvent)Events.Current;
        return 0;
    }

    static double SproutNoCandidateResult()
    {
        Require(sprout.Pieces == 1 && !sprout.Spreading && sprout.Phase == FieldEventPhase.Active && Events.Zones.SlowedCells == 1,
            "No valid neighbour (radius 0): spreading stops for good after one bounded look — no search loop, the event keeps running with the main sprout only");
        Events.End();
        Must(Events.Zones.SlowedCells == 0, "ending should clear the slow");
        return 0;
    }

    // ---------------------------------------------------------------- Aşılı Kabuk
    static GraftedShellEvent shell;
    static double ShellStart(int seed)
    {
        Refill(true);
        Must(Events.Current == null && Events.StartNow(FieldEventKind.GraftedShell, .05f, seed, FastPlan()), "shell should start");
        shell = (GraftedShellEvent)Events.Current;
        var a = shell.Zone(0); var b = shell.Zone(1);
        Must(a.Count == 6 && b.Count == 6 && !a.Any(c => b.Any(d => d.x == c.x && d.z == c.z)) && shell.Targets.Count == 2 &&
             InZone(shell, shell.Targets[0].Cell.x, shell.Targets[0].Cell.z, 0) && InZone(shell, shell.Targets[1].Cell.x, shell.Targets[1].Cell.z, 1) && !shell.Decided,
            "two disjoint 3 × 2 zones, each with its own target");
        return 0;
    }

    // Durum plakasındaki A / B açıklaması: yalnız Aşılı Kabuk'ta görünür; metindeki harfi tarladaki bölge rengine bağlar.
    static double ShellLegend()
    {
        Must(!shell.Decided && shell.Phase == FieldEventPhase.Active, "the legend is checked inside the choice window");
        // Plaka metni faz değişiminden sonraki karede yenilenir.
        if (!Hud.IsShown || !Hud.Text.Contains("A düğümü")) return Again;
        Require(Hud.LegendShown && Hud.Text.Contains("B düğümü") &&
                LateGamePresenter.ZoneColor(FieldEventKind.GraftedShell, 0) != LateGamePresenter.ZoneColor(FieldEventKind.GraftedShell, 1),
            "Aşılı Kabuk status plate: names both nodes and shows the A / B colour legend (same two colours as the zone signs and nodes on the field)");
        Capture("G1_04_AsiliKabuk_Secim");
        return 0;
    }

    static double ShellSaveB()
    {
        var a = shell.Targets[0]; var b = shell.Targets[1];
        Must(Events.Zones.BlockedCells == 0 && a.Hittable && b.Hittable, "choice window: nothing closed, both targets hittable");
        Attack(b.Cell.x, b.Cell.z);
        Must(!shell.Decided && b.Remaining == 1, "one hit should not decide");
        Attack(b.Cell.x, b.Cell.z);
        bool onlyA = true;
        for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) onlyA &= FieldEventDirector.BlocksSpawn(new GridPosition(x, z)) == InZone(shell, x, z, 0);
        Attack(a.Cell.x, a.Cell.z);
        Require(shell.Decided && shell.DecidedByPlayer && shell.SavedZone == 1 && shell.ClosedZone == 0 && onlyA && Events.Zones.BlockedCells == 6 && a.Remaining == 2 && !a.Hittable &&
                shell.ZoneClosed(0) && !shell.ZoneClosed(1),
            "Aşılı Kabuk: breaking B's target saves zone B and closes zone A (exactly its 6 cells, through the same blocker as Diken Kuşatması); the first valid choice is final — A's target can no longer be hit");
        return 0;
    }

    static double ShellReopened()
    {
        Require(shell.Phase != FieldEventPhase.Active && Events.Zones.BlockedCells == 0, "The closed zone reopens after its closed time");
        Events.End();
        return 0;
    }

    static double ShellDefault()
    {
        Require(shell.Decided && !shell.DecidedByPlayer && shell.ClosedZone == 0 && shell.SavedZone == 1 && shell.ZoneClosed(0) && Events.Zones.BlockedCells == 6 &&
                shell.Targets.All(t => t.Remaining == 2 && !t.Hittable),
            "No choice made: when the choice window ends the default zone shown in the preparation (A) closes");
        Events.End();
        return 0;
    }

    static double ShellTie()
    {
        var a = shell.Targets[0]; var b = shell.Targets[1];
        Attack(a.Cell.x, a.Cell.z); Attack(b.Cell.x, b.Cell.z);
        Must(a.Remaining == 1 && b.Remaining == 1 && !shell.Decided, "both targets one hit from breaking");
        Strike(100f, 60f);
        AttackAt(PosAt(5, 5));
        Strike(100f, .4f);
        Require(shell.Decided && shell.SavedZone == 0 && shell.ClosedZone == 1 && a.Broken && b.Remaining == 1 && !b.Broken && shell.ZoneClosed(1) && !shell.ZoneClosed(0),
            "One attack that would break both targets: they are evaluated in list order (A first), so A is saved and B closes; B's target is not counted — documented and deterministic");
        Events.End();
        return 0;
    }

    // ---------------------------------------------------------------- Gezgin Bereket
    static WanderingBountyEvent bounty;
    static double BountyStart()
    {
        Refill(true);
        Must(Events.Current == null && Events.StartNow(FieldEventKind.WanderingBounty, .05f, 41, FastPlan()), "bounty should start");
        bounty = (WanderingBountyEvent)Events.Current;
        Must(bounty.Zone(0).Count == 9 && bounty.Targets.Count == 0, "3 × 3 zone, no target");
        return 0;
    }

    static double BountyBloom()
    {
        var zone=bounty.Zone(0).ToList();var c=zone[0];
        Require(FieldEventDirector.BountyAt(c)==bounty && Events.Zones.BlockedCells==0 && Events.Zones.SlowedCells==0,
            "Bounty is a passive active-region benefit, with no harvest gate, blocking or slowdown");
        SetHealth(c.x,c.z,1);Attack(c.x,c.z);
        Require(bounty.BonusHarvests==1 && PlantAt(c.x,c.z)==null,
            "Direct harvest receives bounty; respawn uses its normal timer instead of immediate duplicate creation");
        var other=zone[1];SetHealth(other.x,other.z,1);PlantAt(other.x,other.z).TakeDamage(Hp,DamageType.Explosion);
        Require(bounty.BonusHarvests==2,"Behavior harvest in the passive region also receives its reward benefit");
        Events.End();Require(FieldEventDirector.BountyAt(c)==null,"Ending bounty removes benefits immediately");
        return 0;
    }

    static double BountyMoves()
    {
        Must(Events.Current == null, "bounty should have cleaned up");
        Must(Events.StartNow(FieldEventKind.WanderingBounty, 0f, 42, FastPlan()), "bounty (moves) should start");
        bounty = (WanderingBountyEvent)Events.Current;
        Add2("bereket: aktif (hareket)", () => UntilPhase(FieldEventPhase.Active), () => Wait(1.7), () =>
        {
            for(int i=0;i<3;i++)Call(bounty,"Tick",bounty.MoveInterval);
            var zone = bounty.Zone(0);
            Require(bounty.Moves >= 3 && zone.Count == 9 && zone.All(c => c.x >= 0 && c.x < N && c.z >= 0 && c.z < N) && bounty.BonusHarvests == 0,
                $"The bounty zone wanders one cell at a time inside the open field ({bounty.Moves} slow region steps); ignoring it creates no harvest rewards");
            Events.End();
            ClearTestMods(); Strike(100f, .4f);
            return 0;
        });
        return 0;
    }
    // Sıradaki adımların önüne adım ekler (kuyruğun başına).
    static void Add2(string name, params Func<double>[] runs)
    {
        var rest = steps.ToList(); steps.Clear();
        steps.Enqueue(rest[0]);   // şu an çalışan adım (dönünce kuyruktan çıkar)
        for (int i = 0; i < runs.Length; i++) steps.Enqueue(($"{name} {i + 1}", runs[i]));
        foreach (var step in rest.Skip(1)) steps.Enqueue(step);
    }

    // ---------------------------------------------------------------- ön koşullar
    static bool EverOffered(string id) => Offered(id, out _, out _);
    static bool Forced(string id)
    {
        var reward = Find(id);
        return Inject(new List<BossRewardSO> { reward }, 23, reward, reward.effect.NeedsPlacement ? AntennaSlots.Id(AntennaSlots.East, 2) : RunEffectSO.NoPlacement);
    }

    static double Prerequisites()
    {
        ResetRewards();
        Refill();
        Strike(100f, .4f, .5f);
        var chained = new[] { ("iletken_hat", "elektrik_anteni"), ("kapali_devre", "iletken_hat"), ("buyuyen_ay", "hilal_darbesi"), ("dolunay", "buyuyen_ay") };
        Require(chained.All(p => Boss.BlockOf(Find(p.Item1), true) == BossRewardBlock.Prerequisite && !Boss.IsEligible(Find(p.Item1)) && !EverOffered(p.Item1) && !Forced(p.Item1)) &&
                Boss.Taken.Count == 0 && Fx.Active.Count == 0,
            "Prerequisites: İletken Hat, Kapalı Devre, Büyüyen Ay and Dolunay are never offered (300 seeds) and cannot be taken even from a forced offer while their prerequisite is missing; a refused take changes nothing");
        Require(new[] { "nadir_reaktor", "asiri_yukleme" }.All(id => Boss.BlockOf(Find(id), true) == BossRewardBlock.Condition && !EverOffered(id) && !Forced(id)),
            "Behavior-dependent rewards (Nadir Reaktör, Aşırı Yükleme) are neither offered nor takeable on a field without a behavior planter");
        Strike(100f, .4f, 0f);
        bool noCrit = new[] { "kritik_ark" }.All(id => Boss.BlockOf(Find(id), true) == BossRewardBlock.Condition && !EverOffered(id) && !Forced(id));
        Strike(100f, .4f, .5f);
        Require(noCrit && new[] { "kritik_ark", "elektrik_anteni", "hilal_darbesi", "hasat_yankisi", "tasan_hasar", "acgozlu_hasat" }.All(EverOffered),
            "Crit rewards are not offered with 0 crit chance; with crit chance they and the unconditional rewards appear in normal offers");
        return 0;
    }

    // ---------------------------------------------------------------- anten ailesi
    static AntennaEffect antenna;
    static int antennaHits0, antennaKills0, explosionTriggers0;

    static double AntennaTake()
    {
        ResetRewards();
        Tile(1, 5, "Explosive", 1f);
        Refill();
        Strike(100f, .4f);
        var reward = Find("elektrik_anteni");
        Must(Offered("elektrik_anteni", out var offer, out int round) && round == 3, "antenna should be offered at the first early boss (R3)");
        bool noPlace = Inject(offer, round, reward);
        bool badPlace = Inject(offer, round, reward, AntennaSlots.Id(AntennaSlots.West, 99));
        Require(!noPlace && !badPlace && Boss.Taken.Count == 0 && Fx.Active.Count == 0 && Boss.PlacementReward == null && !Boss.IsPending,
            "Elektrik Anteni cannot be taken without a position or with an invalid one: nothing is applied (no stack, no effect, no half state)");
        var slots = new List<int>(); reward.effect.CollectPlacements(Fx, slots);
        Must(slots.Count == 44 && slots.Distinct().Count() == 44, $"44 edge positions on an 11 × 11 field ({slots.Count})");
        int planters = Brains.Count(); var tiles = RewardOfferLab.OpenCells().Select(c => c.CurrentModifier).ToList();
        Must(Inject(offer, round, reward, AntennaSlots.Id(AntennaSlots.West, 5)), "antenna should be taken with a valid position");
        antenna = Fx.Find<AntennaEffect>();
        Require(Boss.Stacks(reward) == 1 && antenna != null && antenna.Count == 1 && AttackFeed.Listening && Brains.Count() == planters &&
                RewardOfferLab.OpenCells().Select(c => c.CurrentModifier).SequenceEqual(tiles) && !Boss.IsEligible(reward) && !Forced("elektrik_anteni"),
            "Elektrik Anteni taken from a normal offer with the chosen edge position (west edge, row 5): one antenna, no planter and no tile modifier changed, cannot be taken twice");
        SetHealth(1, 5, 1);
        antennaHits0 = Fx.Hits(RewardSource.Antenna); antennaKills0 = Fx.Kills(RewardSource.Antenna);
        explosionTriggers0 = HarvestBehaviorStats.Triggered(DamageType.Explosion);
        ResetLogs();
        return 0;
    }

    static double AntennaPulse()
    {
        var pulse = Of(RewardSource.Antenna);
        int serial = AttackFeed.Serial;
        Require(antenna.Pulses == 1 && pulse.Count == 3 && pulse.All(h => h.Damage == 60 && h.Z == 5 && !h.Echo) && pulse.Select(h => h.X).OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2 }) &&
                hits.Count == 3 && harvests.Count == 1 && harvests[0] == (DamageType.Reward, RewardSource.Antenna, 1, 5) && PlantAt(1, 5) == null &&
                HarvestBehaviorStats.Triggered(DamageType.Explosion) == explosionTriggers0 && Fx.Hits(RewardSource.Antenna) == antennaHits0 + 3 && Fx.Kills(RewardSource.Antenna) == antennaKills0 + 1,
            "Antenna pulse after 2,5 s: the first 3 cells in its facing direction take 60 (= 100 × 0,60) as reward damage — not direct (no crit, no planter bonus); the plant it harvested on an explosive planter (chance 1) triggered no behavior and was harvested once");
        Must(serial == AttackFeed.Serial, "an antenna pulse is not a direct attack");
        return 0;
    }

    static ConductiveLineEffect line;
    static double LineTake()
    {
        var reward = Find("iletken_hat");
        var slots = new List<int>(); reward.effect.CollectPlacements(Fx, slots);
        int west5 = AntennaSlots.Id(AntennaSlots.West, 5);
        Require(Boss.BlockOf(reward, true) == BossRewardBlock.None && EverOffered("iletken_hat") &&
                !reward.effect.NeedsPlacement && slots.Count == 1 && slots[0] == AntennaSlots.Id(AntennaSlots.East, 5) &&
                reward.effect.IsValidPlacement(Fx, RunEffectSO.NoPlacement) && !reward.effect.IsValidPlacement(Fx, west5),
            "İletken Hat automatically uses only the opposite edge in the first antenna's selected row");
        Must(Offered("iletken_hat", out var offer, out int round), "line offer");
        bool invalid = Inject(offer, round, reward, AntennaSlots.Id(AntennaSlots.West, 4));
        Must(!invalid && Boss.Stacks(reward) == 0 && antenna.Count == 1 && Fx.Active.Count == 1, "an unselectable position must not apply anything");
        Must(Inject(offer, round, reward, RunEffectSO.NoPlacement), "line should be taken without a second placement");
        line = Fx.Find<ConductiveLineEffect>();
        var cells = new List<GridPosition>(); line.LineCells(cells);
        Require(antenna.Count == 2 && cells.Count == 11 && cells.All(c => c.z == 5) && Boss.Stacks(reward) == 1, "İletken Hat taken: second antenna on the east edge of the same row; the line is the 11 cells between the two antennas");
        Refill();
        return 0;
    }

    static double LinePulse()
    {
        var pulse = Of(RewardSource.Line); var east = Of(RewardSource.Antenna).Where(h => h.X >= 8).ToList();
        Require(line.Pulses == 1 && pulse.Count == 11 && pulse.All(h => h.Damage == 45 && h.Z == 5) && pulse.Select(h => h.X).Distinct().Count() == 11 && east.Count == 3 && east.All(h => h.Damage == 60),
            "Line pulse after 4 s: all 11 cells between the antennas take 45 (= 100 × 0,45); the second antenna also fires its own 3 cells");
        return 0;
    }

    static ClosedCircuitEffect circuit;
    static double CircuitTake()
    {
        var reward = Find("kapali_devre");
        ClearTiles();
        Must(Boss.BlockOf(reward, true) == BossRewardBlock.Condition && !EverOffered("kapali_devre"), "closed circuit needs a behavior planter");
        Tile(5, 5, "Explosive", 1f); Tile(6, 5, "Explosive", 1f);
        Take("kapali_devre");
        circuit = Fx.Find<ClosedCircuitEffect>();
        Refill();
        SetHealth(5, 5, 1); SetHealth(6, 5, 60);
        return 0;
    }

    static double CircuitResult()
    {
        var extra = Of(RewardSource.LineExtra);
        Require(circuit.Rolls == 1 && circuit.Extras == 1 && line.ExtraPulses == 1 && extra.Count == 10 && extra.All(h => h.Damage == 27 && h.Z == 5) &&
                harvests.Any(h => h == (DamageType.Reward, RewardSource.Line, 5, 5)) && harvests.Any(h => h == (DamageType.Reward, RewardSource.LineExtra, 6, 5)) &&
                !hits.Any(h => h.Type == DamageType.Explosion) && Fx.PendingTimers == 0,
            "Kapalı Devre: the line harvested a behavior planter's plant, rolled that planter's behavior chance (1) once and fired ONE extra line pulse at 27 (= 45 × 0,60); the extra pulse also harvested a behavior plant but started nothing — it cannot restart itself; no behavior was triggered by line harvests");
        // Bekleme: ilk ek vuruştan sonra süre dolmadan yeni ek vuruş planlanmaz.
        int blocks = circuit.CooldownBlocks, rolls = circuit.Rolls;
        circuit.OnLineHarvest(BrainAt(5, 5));
        Require(circuit.CooldownBlocks == blocks + 1 && circuit.Rolls == rolls && Fx.PendingTimers == 0, "Line cooldown (3 s): a second trigger inside the cooldown is refused without a roll");
        return 0;
    }

    // ---------------------------------------------------------------- hilal ailesi
    static CrescentEffect crescent;
    static double CrescentTake()
    {
        ResetRewards();
        Refill();
        Strike(100f, .4f);
        Take("hilal_darbesi");
        crescent = Fx.Find<CrescentEffect>();
        for (int i = 0; i < 5; i++) Attack(2, 5);
        SpawnerAt(0, 0).RemoveSpawnedPlant();
        Attack(0, 0);
        Require(crescent.Counter == 5 && crescent.Launched == 0 && Of(RewardSource.Crescent).Count == 0, "Hilal Darbesi: five valid direct attacks count; an empty swing (no living plant) does not");
        ResetLogs();
        Attack(2, 5);
        Must(crescent.Counter == 0 && crescent.Launched == 1, "the sixth valid attack launches the crescent");
        return 0;
    }

    static double CrescentResult()
    {
        var wave = Of(RewardSource.Crescent);
        Require(wave.Count == 4 && wave.All(h => h.Damage == 80 && h.Z == 5) && wave.Select(h => h.X).SequenceEqual(new[] { 3, 4, 5, 6 }) &&
                Enumerable.Range(1, 3).All(i => wave[i].Frame > wave[i - 1].Frame) && crescent.Counter == 0 && crescent.Steps == 4 && crescent.ActiveWaves == 0,
            "Crescent: starts at the attacked cell (2, 5), travels toward the middle of the field (+x), one cell per step for 4 steps — (3,5) (4,5) (5,5) (6,5) on later frames — 80 (= 100 × 0,80) each as reward damage; its hits do not count as attacks");
        return 0;
    }

    static double GrowingTake()
    {
        Must(Find("buyuyen_ay").requires == Find("hilal_darbesi") && Boss.BlockOf(Find("buyuyen_ay"), true) == BossRewardBlock.None, "growing moon available after crescent");
        Take("buyuyen_ay");
        Refill();
        SetHealth(3, 5, 1);
        for (int i = 0; i < 6; i++) Attack(2, 5);
        Must(crescent.Launched == 2, "second crescent");
        ResetLogs();
        return 0;
    }

    static double GrowingResult()
    {
        var wave = Of(RewardSource.Crescent);
        var step2 = wave.Where(h => h.X == 4).ToList(); var step3 = wave.Where(h => h.X == 5).ToList(); var step4 = wave.Where(h => h.X == 6).ToList();
        Require(wave.Count(h => h.X == 3) == 1 && wave.First(h => h.X == 3).Damage == 80 && step2.Count == 3 && step2.All(h => h.Damage == 100) && step2.Select(h => h.Z).OrderBy(z => z).SequenceEqual(new[] { 4, 5, 6 }) &&
                step3.Count == 1 && step3[0].Damage == 80 && step3[0].Z == 5 && step4.Count == 1 && step4[0].Damage == 80 && crescent.GrowthGains == 1 && crescent.GrowthResets == 1,
            "Büyüyen Ay: step 1 really harvested → growth 1 applies to the REST of the same wave: step 2 hits a 3-cell front at 100 (= 80 × 1,25); step 2 harvested nothing (a failed step) → growth resets: steps 3 and 4 are back to 1 cell at 80");
        return 0;
    }

    static double FullMoonTake()
    {
        Take("dolunay");
        Refill();
        SetHealth(3, 5, 1); SetHealth(4, 5, 1);
        for (int i = 0; i < 6; i++) Attack(2, 5);
        Must(crescent.Launched == 3 && !DirectAttackPause.Active, "third crescent launched, no pause yet");
        ResetLogs();
        return 0;
    }

    static double FullMoonResult()
    {
        var forward = Of(RewardSource.Crescent); var back = Of(RewardSource.CrescentReturn);
        var step3 = forward.Where(h => h.X == 5).ToList();
        Require(crescent.FullMoons == 1 && step3.Count == 3 && step3.All(h => h.Damage == 120) && forward.Count(h => h.X == 6) == 1 && forward.First(h => h.X == 6).Damage == 80,
            "Dolunay trigger: growth reached 2 on step 2; the forward wave goes on (step 3 at growth 2: 3 cells at 120 = 80 × 1,5; then a failed step resets it)");
        Require(back.Count > 0 && back.All(h => h.Damage == 56 && h.Z >= 4 && h.Z <= 6 && h.X >= 2 && h.X <= 6) && back.Any(h => h.X == 6) && back.Any(h => h.X == 2 && h.Z == 5) &&
                back.Min(h => h.Frame) > forward.Where(h => h.X == 4).Min(h => h.Frame) && crescent.GrowthGains == 3 && crescent.ActiveWaves == 0 && crescent.FullMoons == 1,
            "Second wave from the opposite side: starts at the first wave's last cell (6,5) and comes back to the start cell (2,5), a 3-cell front at 56 (= 80 × 0,70); it does not grow and does not start another Dolunay");
        // Bedel: yalnız oyuncunun saldırı zamanlayıcısı bekler.
        float remaining = DirectAttackPause.RemainingFor(CrescentEffect.PauseSource);
        float timerBefore = F<float>(Player, "attackTimer"); int serial = AttackFeed.Serial;
        Call(Player, "HandleAutoAttack", PosAt(8, 8));
        bool held = F<float>(Player, "attackTimer") == timerBefore && AttackFeed.Serial == serial;
        float left = DirectAttackPause.RemainingFor(CrescentEffect.PauseSource);
        Require(DirectAttackPause.Requests == 1 && remaining > 0f && remaining <= .35f + 1e-4f && left > 0f && left < remaining && held && Time.timeScale == 1f && RM.IsRoundActive && Fx.PendingTimers == 0,
            $"Cost: a direct-attack pause of at most 0,35 s was requested once ({remaining:0.00} s left); while it lasts the player's attack timer does not advance and no attack happens, but the game, the round clock and the effects keep running");
        // Çoklu kaynak ve sert üst sınır.
        DirectAttackPause.Request(77, 5f);
        bool capped = Near(DirectAttackPause.RemainingFor(77), DirectAttackPause.MaxSeconds) && DirectAttackPause.Sources == 2;
        DirectAttackPause.Release(77);
        bool otherKept = DirectAttackPause.Active && DirectAttackPause.RemainingFor(CrescentEffect.PauseSource) == left;
        bool stillPaused = DirectAttackPause.Tick(left * .5f);
        bool over = !DirectAttackPause.Tick(left);
        Call(Player, "HandleAutoAttack", PosAt(8, 8));
        Require(capped && otherKept && stillPaused && over && !DirectAttackPause.Active && F<float>(Player, "attackTimer") > timerBefore,
            "Pause rules: a request is clipped to the hard limit (0,6 s); releasing one source does not release another; the pause ends by game time and the attack timer runs again — it never waits for a visual");
        SetF(Player, "attackTimer", 0f);
        return 0;
    }

    // ---------------------------------------------------------------- kritik / hasat ödülleri
    static double CritArc()
    {
        ResetRewards();
        Refill();
        Strike(100f, .4f, 1f);
        Take("kritik_ark");
        var arc = Fx.Find<CritArcEffect>();
        ResetLogs();
        Attack(5, 5);
        var direct = hits.Single(h => h.Type == DamageType.Direct); var arcs = Of(RewardSource.CritArc);
        Require(arc.Arcs == 1 && arcs.Count == 1 && (arcs[0].X, arcs[0].Z) == (4, 5) && arcs[0].Damage == RunEffects.Scale(direct.Damage, .3f) && hits.Count == 2,
            $"Kritik Ark: a direct crit ({direct.Damage}) arcs to the nearest OTHER plant the attack did not touch — (4,5) by the fixed scan order — for 30 % of the computed crit damage ({arcs[0].Damage}); the arc does not crit and does not arc again");
        Strike(100f, 2f, 1f);
        ResetLogs();
        AttackAt(PosAt(5, 5));
        var directCells = hits.Where(h => h.Type == DamageType.Direct).Select(h => (h.X, h.Z)).ToList(); arcs = Of(RewardSource.CritArc);
        Require(directCells.Count == 9 && arcs.Count == 3 && arc.Arcs == 4 && arcs.Select(h => (h.X, h.Z)).Distinct().Count() == 3 && arcs.All(h => !directCells.Contains((h.X, h.Z))),
            "Nine crits in one attack: at most 3 arcs, each to a different plant outside the attack");
        Strike(100f, .4f, 0f);
        ResetLogs();
        Attack(8, 8);
        Require(Of(RewardSource.CritArc).Count == 0 && arc.Arcs == 4, "No crit, no arc: the per-plant crit roll is unchanged and decides alone");
        return 0;
    }

    static HarvestEchoEffect echo;
    static double EchoCase()
    {
        ResetRewards();
        Refill();
        Strike(100f, 1.2f);
        Take("hasat_yankisi");
        echo = Fx.Find<HarvestEchoEffect>();
        // 23 Ekim 2026 meteor düzeni: tek meteor, kesilen hücrelerin simetriğine; eşik 1 hasat, en çok 6 hücre, 6 sn bekleme.
        SetHealth(1, 1, 1); SetHealth(2, 1, 1); SetHealth(1, 2, 1);
        ResetLogs();
        AttackAt(PosAt(1, 1));
        Must(harvests.Count == 3 && echo.Meteors == 1 && echo.Scheduled == 3 && Fx.PendingTimers == 1 && Of(RewardSource.HarvestEcho).Count == 0,
            "three direct harvests schedule ONE delayed meteor over their three mirrored cells");
        // Bekleme: meteor planlandıktan hemen sonraki hasat yeni meteor doğurmaz.
        SetHealth(1, 7, 1); SetHealth(2, 7, 1);
        AttackAt(PosAt(1, 7));
        Must(echo.Meteors == 1 && echo.CooldownBlocks == 1 && Fx.PendingTimers == 1, "cooldown: a harvest right after the meteor does not schedule another");
        return 0;
    }

    static double EchoResult()
    {
        var hitsEcho = Of(RewardSource.HarvestEcho);
        Require(hitsEcho.Count == 3 && hitsEcho.All(h => h.Damage == 50) && hitsEcho.Select(h => (h.X, h.Z)).OrderBy(c => c).SequenceEqual(new[] { (8, 9), (9, 8), (9, 9) }) && echo.Empty == 0,
            "Hasat Yankısı: one meteor hits the cells mirrored through the centre of the open field — (1,1)→(9,9), (1,2)→(9,8), (2,1)→(8,9) — for 50 (= 100 × 0,50) each after the delay; the cooldown blocked the second harvest");
        // Yansıma saldırının kendi alanındaysa atlanır: tarlanın ortasına vuruş meteor doğurmaz, bekleme de başlamaz.
        echo.OnRoundStarted();
        Refill();
        SetHealth(5, 5, 1); SetHealth(4, 5, 1); SetHealth(6, 5, 1);
        int meteors = echo.Meteors, scheduled = echo.Scheduled;
        AttackAt(PosAt(5, 5));
        Require(echo.Meteors == meteors && echo.Scheduled == scheduled && Fx.PendingTimers == 0,
            "A mirror cell inside the attack's own area is skipped: an attack on the centre mirrors onto itself and drops no meteor");
        // Eşik 1: tek doğrudan hasat da meteor düşürür (tek hücre).
        Refill();
        SetHealth(1, 1, 1);
        ResetLogs();
        AttackAt(PosAt(1, 1));
        Require(harvests.Count == 1 && echo.Meteors == meteors + 1 && echo.Scheduled == scheduled + 1 && Fx.PendingTimers == 1,
            "Threshold 1: a single direct harvest drops a one-cell meteor");
        return 0;
    }

    static double EchoCap()
    {
        var hitsEcho = Of(RewardSource.HarvestEcho);
        Require(hitsEcho.Count == 1 && (hitsEcho[0].X, hitsEcho[0].Z) == (9, 9) && hitsEcho[0].Damage == 50, "The one-cell meteor hits the single mirrored cell (1,1)→(9,9)");
        // 6 hücre sınırı: tek saldırıda 9 hasat → yalnız ilk 6 yansıma, tek meteor.
        echo.OnRoundStarted();
        Refill();
        for (int x = 1; x <= 3; x++) for (int z = 1; z <= 3; z++) SetHealth(x, z, 1);
        Strike(100f, 2.6f);
        int meteors = echo.Meteors, scheduled = echo.Scheduled;
        ResetLogs();
        AttackAt(PosAt(2, 2));
        Strike(100f, 1.2f);
        Require(harvests.Count == 9 && echo.Meteors == meteors + 1 && echo.Scheduled == scheduled + 6 && Fx.PendingTimers == 1,
            "Cap: nine harvests in one attack give one meteor over the first six mirrored cells");
        return 0;
    }

    static double EchoCapResult()
    {
        var hitsEcho = Of(RewardSource.HarvestEcho);
        Require(hitsEcho.Count == 6 && hitsEcho.All(h => h.Damage == 50 && h.X >= 7 && h.Z >= 7) && hitsEcho.Select(h => (h.X, h.Z)).Distinct().Count() == 6 && echo.Empty == 0,
            "The six-cell meteor hits six distinct mirrored cells (inside 7–9) for 50 each, at the same moment");
        return 0;
    }

    static double Overflow()
    {
        ResetRewards();
        Refill();
        Strike(100f, .4f);
        Take("tasan_hasar");
        var overflow = Fx.Find<OverflowEffect>();
        SetHealth(5, 5, 30); SetHealth(4, 5, 10);
        ResetLogs();
        Attack(5, 5);
        var direct = hits.Single(h => h.Type == DamageType.Direct); var moved = Of(RewardSource.Overflow);
        int first = RunEffects.Scale(direct.Damage - 30, .5f, 0), second = RunEffects.Scale(first - 10, .5f, 0);
        Require(overflow.Sources == 1 && moved.Count == 2 && (moved[0].X, moved[0].Z, moved[0].Damage) == (4, 5, first) && (moved[1].X, moved[1].Z, moved[1].Damage) == (3, 5, second) &&
                harvests.Count == 2 && PlantAt(3, 5) != null && overflow.Transfers == 2,
            $"Taşan Hasar: the direct hit ({direct.Damage}) on a plant with 30 health leaves {direct.Damage - 30} real overkill; half ({first}) moves to the nearest plant (10 health → dies), half of ITS overkill ({second}) moves once more, then the transfer limit (2) stops it; each plant is harvested once");
        SetHealth(8, 8, 200);
        ResetLogs();
        Attack(8, 8);
        Require(Of(RewardSource.Overflow).Count == 0 && overflow.Sources == 1, "No kill, no overkill: nothing moves");
        return 0;
    }

    static RareReactorEffect reactor;
    static int modsBefore;
    static double ReactorCase()
    {
        ResetRewards();
        Tile(5, 5, "Explosive", .4f);
        Refill();
        Strike(100f, .4f);
        // Başka bir ödülün (Kıvılcım gibi) katkısı: reaktör kalkarken buna dokunmamalı.
        TestMod(Mod(StatType.ExplosionChance, StatTarget.Planter, .35f, ModifierOperation.MorePercent));
        Take("nadir_reaktor");
        reactor = Fx.Find<RareReactorEffect>();
        var planter = BrainAt(5, 5);
        float before = planter.GetFinalStat(StatType.ExplosionChance);
        modsBefore = Stats.GlobalModifiers.Count;
        var table = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset").spawnTable;
        PlantSO common = table.First(e => e.plant.rarity == PlantRarity.Common).plant, rare = table.First(e => e.plant.rarity >= PlantRarity.Rare).plant;
        SetF(PlantAt(8, 8), "plantData", common); SetHealth(8, 8, 1);
        Attack(8, 8);
        bool commonIgnored = !reactor.IsActive && reactor.Activations == 0;
        SetF(PlantAt(7, 7), "plantData", rare); SetHealth(7, 7, 1);
        PlantAt(7, 7).TakeDamage(Hp, DamageType.Explosion);
        bool behaviorIgnored = !reactor.IsActive;
        Refill();
        SetF(PlantAt(7, 7), "plantData", rare); SetHealth(7, 7, 1);
        Attack(7, 7);
        float during = planter.GetFinalStat(StatType.ExplosionChance);
        Require(Near(before, .54f) && commonIgnored && behaviorIgnored && reactor.IsActive && reactor.Activations == 1 && Near(during, .675f) && Stats.GlobalModifiers.Count == modsBefore + 4 &&
                BrainAt(2, 2).GetFinalStat(StatType.ExplosionChance) == 0f,
            "Nadir Reaktör: a Common direct harvest and a Rare behavior harvest do nothing; a Rare DIRECT harvest raises existing behavior chances ×1,25 (0,54 → 0,675) with four own modifiers; a planter without the behavior stays at 0");
        SetF(PlantAt(8, 8), "plantData", rare); SetHealth(8, 8, 1);
        Attack(8, 8);
        Must(reactor.Refreshes == 1 && Stats.GlobalModifiers.Count == modsBefore + 4 && Near(BrainAt(5, 5).GetFinalStat(StatType.ExplosionChance), .675f), "a second rare harvest only refreshes the time (no stacking)");
        return 0;
    }

    static double ReactorResult()
    {
        Require(!reactor.IsActive && Stats.GlobalModifiers.Count == modsBefore && Near(BrainAt(5, 5).GetFinalStat(StatType.ExplosionChance), .54f) &&
                Stats.GlobalModifiers.Any(m => m.statType == StatType.ExplosionChance && Near(m.value, .35f) && m.target == StatTarget.Planter),
            "After 4 s the reactor removes exactly its own four modifiers: the chance is back to 0,54 and the other source's bonus (×1,35) is still there");
        return 0;
    }

    static double CritBurst()
    {
        var retired = AssetDatabase.LoadAssetAtPath<CritBurstEffectSO>("Assets/ScriptableObjects/RunEffects/GecOyunV1/KritikPatlamaEffect_G1.asset");
        Require(retired != null && !retired.CanTake(Fx) && !Fx.TryAdd(retired, RunEffectSO.NoPlacement) &&
                !RewardOfferLab.All(G1.bossRewards).Any(r => r.id == "kritik_patlama"),
            "Retired Kritik Patlama is absent from the pool and cannot be granted");
        return 0;
    }
    static List<int> DirectDamages(Action attack, int randomSeed)
    {
        UnityEngine.Random.InitState(randomSeed);
        ResetLogs();
        attack();
        return hits.Where(h => h.Type == DamageType.Direct).Select(h => h.Damage).ToList();
    }

    static double Greedy()
    {
        ResetRewards();
        Refill();
        Strike(100f, 1.2f);
        Take("acgozlu_hasat");
        var greed = Fx.Find<GreedyHarvestEffect>();
        var normal = DirectDamages(() => AttackAt(PosAt(8, 2)), 7);
        Must(normal.Count == 5 && !greed.Ready, "baseline: five targets, no charge");
        SetHealth(1, 1, 1); SetHealth(2, 1, 1); SetHealth(1, 2, 1);
        AttackAt(PosAt(1, 1));
        Must(greed.Ready && greed.Charges == 1, "three direct harvests arm the charge");
        SpawnerAt(10, 10).RemoveSpawnedPlant();
        Strike(100f, .4f); Attack(10, 10); Strike(100f, 1.2f);
        bool keptOnEmpty = greed.Ready;
        var boosted = DirectDamages(() => AttackAt(PosAt(8, 2)), 7);
        Require(keptOnEmpty && !greed.Ready && greed.Spent == 1 && boosted.Count == 5 && normal.All(d => d >= 85 && d <= 115) && boosted.All(d => d >= 127 && d <= 173),
            $"Açgözlü Hasat: 3 direct harvests in one attack arm the next attack; an empty swing keeps the charge; the next attack on 5 plants deals ×1,5 direct damage (normal hits {string.Join(", ", normal)} lie in 85–115 = 100 × variance; empowered hits {string.Join(", ", boosted)} lie in 127–173 = 150 × variance — the ranges do not overlap) and spends it");
        // Güçlenen saldırı yeni hak doğurmaz.
        SetHealth(1, 5, 1); SetHealth(2, 5, 1); SetHealth(1, 6, 1);
        AttackAt(PosAt(1, 5));
        Must(greed.Ready, "armed again");
        SetHealth(5, 8, 1); SetHealth(4, 8, 1); SetHealth(6, 8, 1);
        AttackAt(PosAt(5, 8));
        Require(!greed.Ready && greed.Spent == 2 && greed.Charges == 2, "An empowered attack that itself harvests 3 does not arm a new charge (at best every second attack is empowered)");
        // Tek hedefli vuruşta hak kaybolur.
        SetHealth(1, 9, 1); SetHealth(2, 9, 1); SetHealth(1, 10, 1);
        AttackAt(PosAt(1, 9));
        Must(greed.Ready && greed.Charges == 3, "armed a third time");
        Strike(100f, .4f);
        var single = DirectDamages(() => Attack(8, 6), 11);
        bool lost = !greed.Ready && greed.Lost == 1 && greed.Spent == 2;
        var plain = DirectDamages(() => Attack(8, 6), 11);
        Require(lost && single.Count == 1 && plain.Count == 1 && single[0] >= 85 && single[0] <= 115 && plain[0] >= 85 && plain[0] <= 115, $"A single-target attack loses the charge and deals normal damage ({single[0]}, in 85–115)");
        return 0;
    }

    // ---------------------------------------------------------------- Aşırı Yükleme
    static OverloadEffect overload;
    static double OverloadTake()
    {
        ResetRewards();
        Tile(5, 5, "Explosive", 1f);
        Refill();
        Strike(100f, .4f);
        Take("asiri_yukleme");
        overload = Fx.Find<OverloadEffect>();
        SetHealth(5, 5, 1);
        ResetLogs();
        Attack(5, 5);
        var normal = hits.Where(h => h.Type == DamageType.Explosion).ToList();
        Must(normal.Count == 4 && normal.All(h => h.Damage == 100 && !h.Echo) && overload.Granted == 1 && overload.Fired == 0, "normal explosion: four neighbours at 100, one extra proc granted");
        return 0;
    }

    static double OverloadResult()
    {
        var extra = hits.Where(h => h.Type == DamageType.Explosion && h.Echo).ToList();
        Require(overload.Fired == 1 && extra.Count == 4 && extra.All(h => h.Damage == 50 && h.Source == RewardSource.Overload) &&
                extra.Select(h => (h.X, h.Z)).OrderBy(c => c).SequenceEqual(new[] { (4, 5), (5, 4), (5, 6), (6, 5) }) && hits.Count(h => h.Type == DamageType.Explosion) == 8,
            "Aşırı Yükleme: the triggered explosion runs once more 0,3 s later through the planter's own explosion path at 50 (= its normal behavior damage 100 × 0,50, the planter's stats applied once), marked as outside the chain");
        // Bekleme: aynı saksı + davranış süre dolmadan yeni hak doğurmaz; ek proc yeni ek proc doğurmaz.
        Refill();
        SetHealth(5, 5, 1);
        Attack(5, 5);
        Require(overload.Granted == 1 && overload.CooldownBlocks == 1 && Fx.PendingTimers == 0 && overload.Fired == 1,
            "Cooldown (2,5 s per planter + behavior): a second trigger inside it grants nothing; the extra proc never granted another one");
        return 0;
    }

    static int excludedBefore;
    static double OverloadLimits()
    {
        ClearTiles();
        Tile(5, 5, "Explosive", 1f); Tile(6, 5, "Explosive", 1f); Tile(2, 2, "Tornado", 1f);
        Refill();
        Take("zincir_hasat");
        Must(BossRewardManager.TryGetChain(out _), "chain should be active");
        var tornadoes = TornadoManager.Instance;
        Must(tornadoes.ActiveLimit == 12 && HarvestBehaviorManager.Instance.BoomerangLimit == 12, "moving behaviors start at 12 each in the new profile");
        for (int i = 0; i < 11; i++) Must(tornadoes.TrySpawn(Go(9, 9), 1), "tornado " + i);
        // (6,5) normal patlamadan sağ çıkar (120 can), ek proc'la ölür: zincir başlamamalı.
        SetHealth(5, 5, 1); SetHealth(6, 5, 120); SetHealth(2, 2, 1);
        excludedBefore = HarvestChain.Instance.Rejected(HarvestChain.Reject.Excluded);
        ResetLogs();
        Attack(2, 2);
        Must(tornadoes.ActiveCount == 12 && overload.Granted == 2, "the normal tornado took the last slot and granted an extra proc");
        Attack(5, 5);
        return 0;
    }

    static double OverloadLimitsResult()
    {
        var tornadoes = TornadoManager.Instance;
        Require(tornadoes.ActiveCount <= 12 && tornadoes.ActiveLimit == 12 && overload.Dropped >= 1,
            "Moving-behavior limit: with 12 tornadoes active the extra tornado proc is dropped, not queued and not hidden — the limit of 12 is never exceeded");
        Require(harvests.Any(h => h.type == DamageType.Explosion && (h.x, h.z) == (6, 5)) && HarvestChain.Instance.Rejected(HarvestChain.Reject.Excluded) > excludedBefore &&
                !hits.Any(h => h.Type == DamageType.Explosion && (h.X, h.Z) == (7, 5)) && HarvestChain.Instance.Pending == 0,
            "Chain protection: the plant killed by the extra proc belongs to an explosive planter, yet Zincir Hasat refuses it as excluded — the extra proc neither joins the chain nor bypasses its repeat rule");
        // Kapasite artırma yolu (ödül alanı) yeni profilde de çalışır.
        var capacity = ScriptableObject.CreateInstance<BossRewardSO>();
        capacity.id = "test_capacity"; capacity.displayName = "Kapasite testi"; capacity.movingBehaviorLimitBonus = 4; capacity.maxStacks = 1;
        bool raised = Boss.EditorGrant(capacity, out _) && tornadoes.ActiveLimit == 16 && HarvestBehaviorManager.Instance.BoomerangLimit == 16;
        Boss.ClearAll();
        Require(raised && tornadoes.ActiveLimit == 12 && HarvestBehaviorManager.Instance.BoomerangLimit == 12, "The existing capacity path still works: a +4 reward raises both limits to 16 and a new run returns them to 12");
        Object.Destroy(capacity);
        return 0;
    }

    // ---------------------------------------------------------------- Hasat Ritmi ve dışlama
    static double RhythmAndExclusion()
    {
        ResetRewards();
        Tile(5, 5, "Explosive", .5f);
        Refill();
        Strike(100f, .4f);
        Take("hasat_ritmi");
        var chain = Find("zincir_hasat");
        Require(Boss.BlockOf(chain, true) == BossRewardBlock.Exclusive && !Boss.IsEligible(chain) && RunPower.Rhythm.Enabled && RunPower.Rhythm.Threshold == 5,
            "Existing rule kept: Hasat Ritmi and Zincir Hasat still exclude each other in the new pool; Hasat Ritmi still needs 5 direct harvests");
        Take("elektrik_anteni", AntennaSlots.Id(AntennaSlots.South, 3));
        foreach (int z in new[] { 0, 1, 2 }) SetHealth(3, z, 1);
        SetHealth(8, 8, 1);
        Attack(8, 8);
        Must(RunPower.Rhythm.Count == 1, "a direct harvest counts for the rhythm");
        ResetLogs();
        return 0;
    }

    static double RhythmResult()
    {
        Require(harvests.Count(h => h.source == RewardSource.Antenna) == 3 && RunPower.Rhythm.Count == 1 && !RunPower.Rhythm.Ready,
            "Hasat Ritmi counts HARVESTS made by the player's own attack: three plants harvested by the antenna did not move the counter");
        return 0;
    }

    // ---------------------------------------------------------------- hepsi birden
    static int allAttacks, droppedBase;
    static readonly int[] hitBase = new int[16];
    static int Since(RewardSource source) => Fx.Hits(source) - hitBase[(int)source];
    static double AllTake()
    {
        ResetRewards();
        Tile(5, 5, "Explosive", 1f); Tile(3, 3, "Electric", 1f); Tile(7, 7, "Explosive", 1f); Tile(6, 2, "Electric", 1f);
        Refill(false, 60);
        Strike(100f, 2f, 1f);
        Take("elektrik_anteni", AntennaSlots.Id(AntennaSlots.West, 5));
        Take("iletken_hat", AntennaSlots.Id(AntennaSlots.East, 5));
        foreach (string id in new[] { "hilal_darbesi", "buyuyen_ay", "kritik_ark", "hasat_yankisi", "tasan_hasar", "nadir_reaktor", "acgozlu_hasat", "kapali_devre", "dolunay", "asiri_yukleme" }) Take(id);
        Require(Fx.Active.Count == 13 && Boss.Taken.Count == 13 && NewIds.All(id => Boss.Stacks(Find(id)) == 1),
            "All 13 new rewards taken in one run, each from an offer produced by the game's offer builder and through Choose");
        allAttacks = 0;
        for (int i = 0; i < hitBase.Length; i++) hitBase[i] = Fx.Hits((RewardSource)i);
        droppedBase = Fx.DroppedTimers;
        ResetLogs();
        return 0;
    }

    static readonly (int x, int z)[] Aims = { (5, 5), (2, 2), (8, 8), (2, 8), (8, 2), (5, 1), (1, 5), (9, 5) };
    static double AllAttack(int index)
    {
        // Kare başına bir saldırı (oyundaki gibi): saldırı başına sınırlar kareye göre sayılabilsin.
        if (index % 6 == 0) Refill(false, 60);
        int frame = Time.frameCount;
        var aim = Aims[index % Aims.Length];
        int before = hits.Count;
        AttackAt(PosAt(aim.x, aim.z));
        var mine = hits.Skip(before).Where(h => h.Frame == frame).ToList();
        int arcs = mine.Count(h => h.Source == RewardSource.CritArc), flows = mine.Count(h => h.Source == RewardSource.Overflow);
        Must(arcs <= 3 && flows <= 8 && Fx.PendingTimers <= RunEffects.TimerCapacity, $"per-attack limits exceeded: arcs {arcs}, overflow {flows}, timers {Fx.PendingTimers}");
        allAttacks++;
        return .02;
    }

    static double AllResult()
    {
        int total = Enumerable.Range(1, 9).Sum(i => Since((RewardSource)i));
        Require(allAttacks == 24 && Fx.PendingTimers == 0 && Fx.DroppedTimers == droppedBase && total > 0 && Since(RewardSource.CritArc) <= 3 * allAttacks && Since(RewardSource.Overflow) <= 8 * allAttacks &&
                Since(RewardSource.HarvestEcho) <= 3 * allAttacks && Fx.Find<CritBurstEffect>() == null && Fx.Find<CrescentEffect>().ActiveWaves == 0 && errors == 0,
            $"All 12 together on a dense, low-health field, 24 wide crit attacks: every loop ended ({total} reward hits; arcs {Since(RewardSource.CritArc)}, overflow {Since(RewardSource.Overflow)}, echo {Since(RewardSource.HarvestEcho)}, burst {Since(RewardSource.CritBurst)}, crescent {Since(RewardSource.Crescent) + Since(RewardSource.CrescentReturn)}, line {Since(RewardSource.Line) + Since(RewardSource.LineExtra)}, antenna {Since(RewardSource.Antenna)}); per-attack caps held, the delayed queue drained, nothing was dropped and no error was logged");
        return 0;
    }

    // ---------------------------------------------------------------- performans (batch; GPU ya da insan Play ölçümü değildir)
    static readonly Dictionary<bool, (double ms, long bytes, double frameMs)> perf = new();
    static double Perf(bool loaded)
    {
        if (!loaded) { ResetRewards(); Events.End(); }
        else
        {
            Tile(5, 5, "Explosive", 1f); Tile(3, 3, "Electric", 1f);
            Take("elektrik_anteni", AntennaSlots.Id(AntennaSlots.West, 5)); Take("iletken_hat", AntennaSlots.Id(AntennaSlots.East, 5));
            foreach (string id in new[] { "hilal_darbesi", "buyuyen_ay", "kritik_ark", "hasat_yankisi", "tasan_hasar", "nadir_reaktor", "acgozlu_hasat", "kapali_devre", "dolunay", "asiri_yukleme" }) Take(id);
            Must(Events.StartNow(FieldEventKind.InvasiveSprout, 0f, 5, FastPlan()), "event for the measurement");
        }
        Refill(true);
        Strike(100f, 2f, .3f);
        Unsubscribe();
        UnityEngine.Random.InitState(99);
        const int attacks = 400;
        for (int i = 0; i < 20; i++) AttackAt(PosAt(Aims[i % Aims.Length].x, Aims[i % Aims.Length].z));   // ısınma
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < attacks; i++) AttackAt(PosAt(Aims[i % Aims.Length].x, Aims[i % Aims.Length].z));
        watch.Stop();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        perf[loaded] = (watch.Elapsed.TotalMilliseconds / attacks, bytes / attacks, 0);
        Subscribe();
        Note($"measurement ({(loaded ? "13 rewards + field event" : "baseline")}): {perf[loaded].ms:0.0000} ms and {perf[loaded].bytes} bytes per direct attack (9-cell attack, 121 plants, {attacks} attacks); effects {Fx.Active.Count}, timers {Fx.PendingTimers}, presenter items {(Object.FindAnyObjectByType<LateGamePresenter>() is { } p ? p.EventItems + p.AntennaItems + p.Flashes : -1)}");
        return .3;
    }

    static double PerfResult()
    {
        var presenter = Object.FindAnyObjectByType<LateGamePresenter>();
        Require(perf.Count == 2 && presenter != null && presenter.ActiveDecorations <= LateGamePresenter.DecorationCapacity && Fx.PendingTimers <= RunEffects.TimerCapacity,
            $"Batch CPU measurement only (not GPU, not a human Play test): a direct attack costs {perf[false].ms:0.0000} ms / {perf[false].bytes} B without the new content and {perf[true].ms:0.0000} ms / {perf[true].bytes} B with all 13 rewards and a field event; decoration and timer pools stayed inside their limits ({presenter.ActiveDecorations}/{LateGamePresenter.DecorationCapacity} decorations, skipped {presenter.DecorationsSkipped})");
        return 0;
    }

    // ---------------------------------------------------------------- temizlik
    static double LossCase()
    {
        Events.End();
        ResetRewards();
        Tile(5, 5, "Explosive", .4f);
        Refill(true);
        Strike(100f, .4f);
        Take("nadir_reaktor"); Take("hilal_darbesi"); Take("buyuyen_ay"); Take("dolunay");
        Take("elektrik_anteni", AntennaSlots.Id(AntennaSlots.West, 5));
        var rare = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset").spawnTable.First(e => e.plant.rarity >= PlantRarity.Rare).plant;
        SetF(PlantAt(8, 8), "plantData", rare); SetHealth(8, 8, 1);
        modsBefore = Stats.GlobalModifiers.Count;
        Attack(8, 8);
        DirectAttackPause.Request(CrescentEffect.PauseSource, .3f);
        Events.End();
        Must(Events.StartNow(FieldEventKind.ThornSiege, 0f, 13, FastPlan()), "event before the round ends");
        for (int i = 0; i < 5; i++) Attack(2, 5);
        Must(Fx.Find<RareReactorEffect>().IsActive && Stats.GlobalModifiers.Count == modsBefore + 4 && DirectAttackPause.Active && Events.Current != null, "reactor on, pause on, event set up");
        // Round 26 boss round'udur ve skor yoktur: süre bitince run kayıpla biter.
        SetP(RM, "RemainingTime", 0f);
        return .3;
    }

    static double LossResult()
    {
        Require(State == GameStates.RunComplete && RM.RunFailed && Events.Current == null && Events.Zones.BlockedCells == 0 && Events.Zones.SlowedCells == 0 &&
                !DirectAttackPause.Active && Fx.PendingTimers == 0 && !Fx.Find<RareReactorEffect>().IsActive && Stats.GlobalModifiers.Count == modsBefore &&
                Fx.Find<CrescentEffect>().ActiveWaves == 0 && Boss.Taken.Count == 5,
            "Round end by LOSS (R26 without score): the field event is cleaned up, no cell stays blocked or slowed, the attack pause is lifted, delayed jobs are dropped, the reactor's temporary modifiers are removed; taken rewards stay listed for the result screen");
        return 0;
    }

    static double MenuCase()
    {
        GameManager.Instance.ReturnToMenu();
        Require(State == GameStates.MainMenu && Fx.Active.Count == 0 && !AttackFeed.Listening && Boss.Taken.Count == 0 && Events.Current == null && !DirectAttackPause.Active && Boss.PlacementReward == null,
            "Return to menu: every effect, listener and reward is cleared through the single cleanup path");
        return 0;
    }

    static double Load(RunProfileSO profile)
    {
        Unsubscribe();
        ClearTestMods();
        Selection.active = profile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 2;
    }

    static long Total => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Total + amount);

    static double CleanStart()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != G1) return Again;
        Player.enabled = false;
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        Require(Fx != null && Fx.Active.Count == 0 && !AttackFeed.Listening && Events.Current == null && Events.Started == 0 && Events.Zones.BlockedCells == 0 && !DirectAttackPause.Active &&
                Boss.Taken.Count == 0 && Boss.PlacementReward == null && TornadoManager.Instance.ActiveLimit == 12,
            "Restart (new scene, new run): no effect, no event, no pause, no listener; moving limit 12");
        GridUnlockManager.Instance.UnlockNextTier(N);
        RewardOfferLab.Build(RewardOfferLab.Layout.Direct);
        Time.captureDeltaTime = Dt;
        return .05;
    }

    static double PlainRound(int round)
    {
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == round, $"round {round} should start");
        Must(Events.Current == null && Events.Started == 0, "no field event before Round 26 on the real round path");
        AddScore(100);
        SetP(RM, "RemainingTime", 0f);
        return .2;
    }

    static double PlainRoundEnd(int round)
    {
        if (RM.IsRoundActive || State == GameStates.Round) return Again;
        Must(State == GameStates.RoundEnd && !Boss.IsPending, $"round {round} should end in the summary ({State})");
        return 0;
    }

    static int realSeed;
    static double BossRound()
    {
        // Elektrik Anteni'nin R3 teklifinde olduğu bir run seed'i (oyunun teklif üreticisiyle aranır).
        var antennaReward = Find("elektrik_anteni");
        // Teklif, round'un tarla doluluğuna da bağlıdır (Bereketli Toprak): iki durumda da anteni sunan bir seed seçilir.
        realSeed = Enumerable.Range(1, 3000).First(s =>
        {
            RewardOfferLab.SetOccupancy(0f); bool low = RewardOfferLab.Offer(Pool, 3, s).Contains(antennaReward);
            RewardOfferLab.SetOccupancy(1f); return low && RewardOfferLab.Offer(Pool, 3, s).Contains(antennaReward);
        });
        SetP(SegmentEventDirector.Instance, "RunSeed", realSeed);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == 3 && RM.IsBossRound(3), "round 3 should be the first boss round");
        AddScore(RM.QuotaTarget + RM.BossTarget + 100);
        SetP(RM, "RemainingTime", 0f);
        return .3;
    }

    static BossRewardPanelUI panel;
    static RewardPlacementUI placement;
    static double RealOffer()
    {
        if (State != GameStates.RoundChoice) return Again;
        var offer = Boss.Offer;
        panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        placement = Object.FindFirstObjectByType<RewardPlacementUI>(FindObjectsInactive.Include);
        var antennaReward = Find("elektrik_anteni");
        if (!panel.gameObject.activeInHierarchy || panel.ShownCards != offer.Count) return Again;
        Canvas.ForceUpdateCanvases();
        int index = offer.ToList().IndexOf(antennaReward);
        bool fits = true; string texts = "";
        for (int i = 0; i < offer.Count; i++)
            foreach (TextMeshProUGUI text in panel.CardTexts(i))
            {
                // Yalnız yeni (etkili) ödüllerin kartları: eski kartların düzeni bu paketin konusu değil.
                if (offer[i].effect == null || !text.gameObject.activeInHierarchy) continue;
                text.ForceMeshUpdate();
                fits &= !text.isTextTruncated;
            }
        foreach (TextMeshProUGUI text in panel.CardTexts(index)) if (text.gameObject.activeInHierarchy) texts += text.text + " | ";
        Require(!RM.RunFailed && Boss.IsPending && offer.Count == 3 && offer.SequenceEqual(RewardOfferLab.Offer(Pool, 3, realSeed)) && index >= 0 && fits &&
                texts.Contains("Elektrik Anteni") && texts.Contains("ilk 3 hücreye") && texts.Contains("Bir kez alınır") && texts.Contains("konumunu sen belirlersin") && !placement.gameObject.activeSelf,
            $"Real boss flow (R3 passed with score): the reward screen opens with the normal offer [{string.Join(", ", offer.Select(r => r.displayName))}]; the Elektrik Anteni card shows its rule from the effect data, 'taken once' and the placement note, and no text of a new reward's card is truncated");
        return .3;
    }

    static double PlacementBegin()
    {
        var antennaReward = Find("elektrik_anteni");
        var offer = Boss.Offer.ToList();
        bool direct = Boss.Choose(antennaReward);
        Must(!direct && Boss.IsPending && Boss.Taken.Count == 0 && Fx.Active.Count == 0, "choosing a placement reward without a position must change nothing");
        // Karttaki "AL" düğmesi: yerleşim adımı açılır.
        var card = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.name == "Choose").ElementAt(offer.IndexOf(antennaReward));
        card.onClick.Invoke();
        Must(Boss.PlacementReward == antennaReward && Boss.IsPending && Boss.Taken.Count == 0 && Boss.Offer.SequenceEqual(offer), "the card button starts the placement step; the offer is unchanged and nothing is applied");
        return .3;
    }

    static double PlacementPanel()
    {
        if (!placement.gameObject.activeInHierarchy || panel.gameObject.activeSelf) return Again;
        int slot = AntennaSlots.Id(AntennaSlots.West, 5);
        placement.SetHovered(slot);
        Require(placement.Slots.Count == 44 && placement.Hovered == slot && placement.PreviewCells == 3 && placement.PromptText.Contains("ANTENİ YERLEŞTİR") && State == GameStates.RoundChoice,
            "Placement step: the reward screen closes, the placement panel shows the 44 valid edge positions; hovering one previews the 3 cells it would hit");
        return .2;
    }

    static double PlacementCancel()
    {
        var offer = Boss.Offer.ToList();
        placement.Cancel();
        Must(Boss.PlacementReward == null && Boss.IsPending && Boss.Taken.Count == 0 && Fx.Active.Count == 0 && Boss.Offer.SequenceEqual(offer), "cancel returns to the same offer with nothing applied");
        Add2("yerleşim: yeniden", () => panel.gameObject.activeInHierarchy && !placement.gameObject.activeSelf ? .1 : Again, () =>
        {
            Require(panel.ShownCards == offer.Count && Boss.Offer.SequenceEqual(offer), "Cancel: the reward screen reopens with the same three cards; no stack, effect or half state was left");
            Must(Boss.BeginPlacement(Find("elektrik_anteni")) && !Boss.BeginPlacement(Find("keskin_bicak")), "placement can be started again; an ordinary reward has no placement step");
            return .2;
        }, () => placement.gameObject.activeInHierarchy ? 0 : Again);
        return 0;
    }

    static double PlacementConfirm()
    {
        var antennaReward = Find("elektrik_anteni");
        bool invalid = placement.Confirm(AntennaSlots.Id(AntennaSlots.West, 77));
        Must(!invalid && Boss.IsPending && Boss.Taken.Count == 0, "an invalid position is refused");
        bool taken = placement.Confirm(AntennaSlots.Id(AntennaSlots.North, 4));
        Require(taken && !Boss.IsPending && Boss.PlacementReward == null && Boss.Stacks(antennaReward) == 1 && Fx.Find<AntennaEffect>() != null && Fx.Find<AntennaEffect>().Slot(0) == AntennaSlots.Id(AntennaSlots.North, 4) &&
                State == GameStates.RoundEnd && !placement.Confirm(AntennaSlots.Id(AntennaSlots.North, 5)),
            "Position chosen: the reward is applied once with that position (north edge, column 4), the choice closes and the run continues to the round summary; a second confirm does nothing");
        return .3;
    }

    static double NormalRoundCase()
    {
        if (placement.gameObject.activeSelf) return Again;
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.CurrentRound == 4 && Events.Current == null, "round 4 should start without an event");
        Refill(true);
        Must(Events.StartNow(FieldEventKind.ThornSiege, 0f, 14, FastPlan()), "event in an ordinary round");
        return 0;
    }

    static double NormalRoundEnd()
    {
        if (RM.IsRoundActive || State == GameStates.Round) return Again;
        Require((State == GameStates.RoundEnd || State == GameStates.CardSelection) && !RM.RunFailed && Events.Current == null && Events.Zones.BlockedCells == 0 && Fx.PendingTimers == 0 &&
                Fx.Active.Count == 1 && Boss.Stacks(Find("elektrik_anteni")) == 1 && Object.FindAnyObjectByType<LateGamePresenter>().EventItems == 0,
            "Ordinary round end with an active event: the event and its blocked cells are cleaned up (visual items too); the taken reward stays for the next round");
        return 0;
    }

    static double FinalCheck()
    {
        Require(errors == 0 && ObserverEvents.Failures == 0, "No error or exception was logged and no presentation listener failed during the whole run");
        return 0;
    }

    // ---------------------------------------------------------------- görüntü
    static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var target = new RenderTexture(1600, 900, 24);
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f; }
        target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.Destroy(image); Object.Destroy(target);
    }
}
