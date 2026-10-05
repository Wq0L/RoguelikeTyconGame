using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.6.2 İŞLEV testi — davranışı koruyan refactor. Gözlem olaylarının oynanıştan ayrılması (hata atan
// test dinleyicisiyle), Editor'a özel ödül verme girişi, ortak alan çizgisi sınıfı ve GameScene kalıntılarının temizliği.
// Davranışın değişmediği bu testte değil, A ile aynı seed ve koşullardaki ölçüm karşılaştırmasıyla gösterilir.
[InitializeOnLoad]
public static class Refactor3762Verification
{
    const string Key = "Refactor3762Verification";
    const string LogFile = "Logs/Refactor3762Verification.txt";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const int Seed = 37621, N = 11, C = 5;
    const double Again = double.NaN;
    // P6'da (Bölüm 3.7.6) AftershockAreaFeedback.cs'in GUID'i: dosya yeniden adlandırılınca korunmalı.
    const string AreaGuid = "fe51ab870515ad443bec7fee2cb44032";

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince; static int stepIndex, shownStep = -1, errors;

    static Refactor3762Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_ZincirV1.asset");
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
            if (double.IsNaN(wait)) { nextAt = now + .02; EditorApplication.QueuePlayerLoopUpdate(); return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static readonly List<string> expected = new();
    static void Expect(string fragment) => expected.Add(fragment);
    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        int i = expected.FindIndex(f => message.Contains(f));
        if (i >= 0) { expected.RemoveAt(i); notes.Add("   beklenen log: " + message.Split('\n')[0]); return; }
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Unhook();
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
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
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static HarvestChain Chain => HarvestChain.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static RunProfileSO Z1 => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_ZincirV1.asset");
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static BossRewardSO Zincir => RewardOfferLab.All(Z1.bossRewards).First(r => r.id == "zincir_hasat");
    static StatModifier Mod(StatType s, StatTarget t, float v) => new StatModifier { statType = s, target = t, operation = ModifierOperation.Set, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    static void Strike(float damage)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, .4f));
        TestMod(Mod(StatType.PlantSpawnRate, StatTarget.Planter, 1000f));
    }
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static readonly List<BossRewardSO> copies = new();
    static BossRewardSO ChainCopy(float[] chance, float[] damage = null)
    {
        var copy = Object.Instantiate(Zincir);
        copy.chainChance = chance; copy.chainDamage = damage ?? new[] { .75f, .5f }; copy.chainGenerations = chance.Length;
        copies.Add(copy);
        return copy;
    }

    static int minX, minZ;
    static GroundCell CellAt(int x, int z) => RewardOfferLab.OpenCells().First(c => c.GetGridPosition().x == minX + x && c.GetGridPosition().z == minZ + z);
    static Vector3 PosAt(int x, int z) => CellAt(x, z).transform.position;
    static GridObject GridAt(int x, int z) => GridManager.Instance.GetGridSystem().GetGridObject(CellAt(x, z).GetGridPosition());
    static PlanterSO Planter11 => AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
    static PlantHealth PlantAt(int x, int z) { var p = GridAt(x, z).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static readonly Dictionary<string, PlanterBrain> named = new();
    static PlanterBrain P(string name) => named[name];
    static PlanterBrain Place(string name, int x, int z, string tile)
    {
        var brain = KirilmaErisimMeasurement.PlacePlanter(Planter11, CellAt(x, z), tile != null ? RewardOfferLab.Tile(tile) : null, tile != null ? 1f : 0f);
        named[name] = brain;
        return brain;
    }
    static void Respawn(PlanterBrain brain, int health)
    {
        foreach (var spawner in brain.GetComponentsInChildren<PlantSpawner>(true)) { spawner.RemoveSpawnedPlant(); spawner.enabled = true; Call(spawner, "TrySpawnPlant"); }
        foreach (var grid in brain.OccupiedGrids)
        {
            var plant = grid.GetPlantObject().GetComponent<PlantHealth>();
            SetF(plant, "maxHealth", Math.Max(health, 1)); SetF(plant, "currentHealth", health);
        }
    }
    static void ClearField()
    {
        HarvestBehaviorManager.Instance.ClearAll();
        Call(TornadoManager.Instance, "ClearAll");
        RewardOfferLab.ClearField();
        named.Clear();
    }
    static void Attack(int x, int z) => Call(Player, "AttackInRadius", PosAt(x, z));

    // ---------------------------------------------------------------- gözlemciler
    // Her gözlem olayına önce hata atan, sonra sayan bir dinleyici takılır: sayanın her olayı alması, hatanın sonraki dinleyiciyi
    // engellemediğini gösterir.
    const string Boom = "test gözlemcisi bilerek hata attı";
    static int damagedSeen, harvestedSeen, tracedSeen, echoSeen, gameplayHarvests, xpGrants;
    static void ThrowDamaged(PlantHealth p, int d, DamageType t) => throw new InvalidOperationException(Boom + " (AnyDamaged)");
    static void ThrowHarvested(PlantHealth p) => throw new InvalidOperationException(Boom + " (AnyHarvested)");
    static void ThrowTraced(HarvestChain.Trace t) => throw new InvalidOperationException(Boom + " (HarvestChain.Traced)");
    static void ThrowEcho(string a, DamageType t, int id, float delay, int frames) => throw new InvalidOperationException(Boom + " (BehaviorEchoes.Traced)");
    static void SeeDamaged(PlantHealth p, int d, DamageType t) => damagedSeen++;
    static void SeeHarvested(PlantHealth p) => harvestedSeen++;
    static void SeeTraced(HarvestChain.Trace t) => tracedSeen++;
    static void SeeEcho(string a, DamageType t, int id, float delay, int frames) => echoSeen++;
    static void SeeGameplay(PlantHealth p) => gameplayHarvests++;
    static void SeeXp() => xpGrants++;
    static bool hooked;
    static void Hook()
    {
        PlantHealth.AnyDamaged += ThrowDamaged; PlantHealth.AnyDamaged += SeeDamaged;
        PlantHealth.AnyHarvested += ThrowHarvested; PlantHealth.AnyHarvested += SeeHarvested;
        HarvestChain.Traced += ThrowTraced; HarvestChain.Traced += SeeTraced;
        BehaviorEchoes.Traced += ThrowEcho; BehaviorEchoes.Traced += SeeEcho;
        PlantHealth.Harvested += SeeGameplay;
        ProgressionManager.Instance.OnXPChanged += SeeXp;
        hooked = true;
    }
    static void Unhook()
    {
        if (!hooked) return;
        PlantHealth.AnyDamaged -= ThrowDamaged; PlantHealth.AnyDamaged -= SeeDamaged;
        PlantHealth.AnyHarvested -= ThrowHarvested; PlantHealth.AnyHarvested -= SeeHarvested;
        HarvestChain.Traced -= ThrowTraced; HarvestChain.Traced -= SeeTraced;
        BehaviorEchoes.Traced -= ThrowEcho; BehaviorEchoes.Traced -= SeeEcho;
        PlantHealth.Harvested -= SeeGameplay;
        if (ProgressionManager.Instance != null) ProgressionManager.Instance.OnXPChanged -= SeeXp;
        hooked = false;
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("tarla ve round", LabRound);
        Add("sahne kalıntıları", SceneLeftovers);
        Add("gözlemci hatası: saldırı", ObserverCase);
        Add("gözlemci hatası: sonuç", ObserverResult);
        Add("oynanış olayı hatası yutulmaz", GameplayErrorCase);
        Add("oynanış olayı hatası yutulmaz: sonuç", GameplayErrorResult);
        Add("Editor ödül girişi", EditorGrantCase);
        Add("ortak alan çizgisi", AreaOutlineCase);
        Add("kapanış", FinalCheck);
    }

    static double LabRound()
    {
        if (RM == null || GameManager.Instance == null || State != GameStates.RunSetup || RM.Profile != Z1) return Again;
        Player.enabled = false;
        GridUnlockManager.Instance.UnlockNextTier(N);
        var open = RewardOfferLab.OpenCells();
        Must(open.Count == N * N, $"open cells {open.Count}");
        minX = open.Min(c => c.GetGridPosition().x); minZ = open.Min(c => c.GetGridPosition().z);
        RM.StartNextRound();
        Must(State == GameStates.Round && RM.IsRoundActive, "round 1 should be running");
        SetP(RM, "RemainingTime", 100000f);
        Time.timeScale = 1f;
        Strike(100f);
        return .05;
    }

    // ================================================================ sahne
    static double SceneLeftovers()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
        var leftovers = all.Where(g => g.name.StartsWith("Sellect Grass planter") || g.name == "Legacy Stats" || g.name == "Sound Manager").Select(g => g.name).ToList();
        var game = typeof(GameManager).Assembly;
        bool soundGone = game.GetType("SoundManager") == null;
        Require(leftovers.Count == 0 && soundGone,
            $"GameScene: the five hidden old 'Sellect Grass planter…' buttons, the hidden 'Legacy Stats' text and the unused 'Sound Manager' are gone ({leftovers.Count} left); the SoundManager class is removed");
        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        bool fieldsGone = typeof(ProgressionManager).GetField("possibleModifiers", Any) == null && typeof(PlanterShopPanelUI).GetField("stats", Any) == null;
        string sceneText = File.ReadAllText("Assets/Scenes/GameScene.unity");
        string builder = File.ReadAllText("Assets/Editor/ComicUIBuilder.cs");
        Require(fieldsGone && !sceneText.Contains("possibleModifiers") && !sceneText.Contains("exhaustThreshold") && !sceneText.Contains("Sellect Grass") && !builder.Contains("Legacy Stats"),
            "Unused fields are removed (ProgressionManager.possibleModifiers, PlanterShopPanelUI.stats) and the saved scene keeps none of them or the old exhaust* values; ComicUIBuilder no longer creates 'Legacy Stats'");
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        var gold = Object.FindFirstObjectByType<GoldUI>(FindObjectsInactive.Include);
        bool bound = sceneText.Contains("resourcesUI: {fileID: 0}") == false && F<RectTransform>(ui, "resourcesUI") == gold.transform.parent;
        int cells = all.Count(g => g.GetComponent<GroundCell>() != null), slots = all.Count(g => g.GetComponent<SkillNodeUI>() != null);
        Require(bound && cells == 121 && slots == 140,
            $"UIManager.resourcesUI is bound in the scene to the object the runtime fallback used to find (the GoldUI's parent '{gold.transform.parent.name}'); kept: {cells} ground cells, {slots} skill UI slots");
        return .05;
    }

    // ================================================================ gözlemci hatası
    // A (patlama, can 1) doğrudan hasatla ölür → patlaması B'yi (patlama, can 1) öldürür → B zincirle (nesil 1) patlar → X ölür.
    // Ayrıca bir artçı planlanır. Bütün gözlem olaylarında hata atan bir dinleyici vardır.
    static PlantHealth plantA, plantB, plantX;
    static int failures0, xp0;
    static double ObserverCase()
    {
        ClearField(); Boss.ClearAll();
        Strike(100f);
        Place("A", 3, C, "Explosive"); Place("B", 4, C, "Explosive"); Place("X", 5, C, null);
        foreach (var p in named.Values) Respawn(p, 1);
        Must(RewardOfferLab.Grant(ChainCopy(new[] { 1f, 1f })), "chain not granted");
        plantA = PlantAt(3, C); plantB = PlantAt(4, C); plantX = PlantAt(5, C);
        failures0 = ObserverEvents.Failures;
        damagedSeen = harvestedSeen = tracedSeen = echoSeen = gameplayHarvests = xpGrants = 0;
        Hook();
        foreach (string e in new[] { "(AnyDamaged)", "(AnyHarvested)", "(HarvestChain.Traced)", "(BehaviorEchoes.Traced)" }) { Expect("Gözlem olayı dinleyicisi istisna attı"); Expect(Boom + " " + e); }
        Attack(3, C);
        Must(BehaviorEchoes.Instance.ScheduleExplosion(P("A"), P("A").OccupiedGrids, PosAt(3, C), 10, new BehaviorEcho(.05f, 1f, 1f)), "echo should be planned");
        return .5;
    }
    static double ObserverResult()
    {
        int failures = ObserverEvents.Failures - failures0;
        bool dead = plantA.IsDead && plantB.IsDead && plantX.IsDead && !plantA.gameObject.activeInHierarchy && !plantB.gameObject.activeInHierarchy && !plantX.gameObject.activeInHierarchy;
        Require(dead && harvestedSeen == 3 && gameplayHarvests == 3 && xpGrants == 3 && damagedSeen >= 3 && tracedSeen > 0 && echoSeen >= 2 &&
                failures == damagedSeen + harvestedSeen + tracedSeen + echoSeen && Chain.Pending == 0 && Chain.ActiveRoots == 0 && BehaviorEchoes.Instance.Pending == 0,
            $"Observers that throw on every event: damage is still applied, the 3 plants (direct, normal explosion, chained explosion) die and return to the pool, each paid once ({xpGrants} XP grants), the gameplay harvest event still ran ({gameplayHarvests}); the counting observer after each thrower saw every event; the chain queue and roots and the echo job finished without leaking; {failures} exceptions caught, each thrower logged once");
        Unhook();
        ClearField(); Boss.ClearAll();
        return .05;
    }

    // Oynanış olayı (görev sayacı yolu) hata atarsa yutulmaz: istisna saldırıdan dışarı çıkar; bitki yine havuza döner.
    static void ThrowGameplay(PlantHealth p) => throw new InvalidOperationException("oynanış dinleyicisi bilerek hata attı");
    static double GameplayErrorCase()
    {
        ClearField(); Boss.ClearAll();
        Place("P", C, C, null); Respawn(P("P"), 1);
        errorPlant = PlantAt(C, C);
        PlantHealth.Harvested += ThrowGameplay;
        errorPropagated = false;
        try { Attack(C, C); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { errorPropagated = true; }
        finally { PlantHealth.Harvested -= ThrowGameplay; }
        return .1;   // havuza dönüş LateUpdate'te (PlantPool): sonuç bir sonraki adımda
    }
    static PlantHealth errorPlant; static bool errorPropagated;
    static double GameplayErrorResult()
    {
        var plant = errorPlant; bool propagated = errorPropagated;
        // Görev sayacı oynanış olayında, gözlem olayında değil.
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        var gameplay = (Delegate)typeof(PlantHealth).GetField("Harvested", Static).GetValue(null);
        var observers = (Delegate[])typeof(PlantHealth).GetField("harvestObservers", Static).GetValue(null);
        bool questOnGameplay = gameplay != null && gameplay.GetInvocationList().Any(d => d.Target is QuestTracker);
        bool questOnObservers = observers.Any(d => d.Target is QuestTracker);
        Must(propagated, "the gameplay listener's exception should propagate out of the attack");
        Must(plant.IsDead && !plant.gameObject.activeInHierarchy, $"the plant should be dead and back in the pool (dead {plant.IsDead}, active {plant.gameObject.activeInHierarchy})");
        Must(questOnGameplay && !questOnObservers, $"quest counter on the gameplay event {questOnGameplay}, on the observer event {questOnObservers} ({gameplay?.GetInvocationList().Length} gameplay listeners, {observers.Length} observers)");
        Require(propagated && plant.IsDead && !plant.gameObject.activeInHierarchy && questOnGameplay && !questOnObservers,
            "A gameplay listener's exception is not swallowed (it propagates out of the attack) and the plant still returns to the pool; the quest counter listens on this gameplay event (PlantHealth.Harvested), not on the observer event");
        ClearField();
        return .05;
    }

    // ================================================================ Editor ödül girişi
    static double EditorGrantCase()
    {
        ClearField(); Boss.ClearAll();
        Place("S", C, C, "Explosive");   // Zincir Hasat'ın koşulu: davranışlı bir saksı
        bool first = Boss.EditorGrant(Zincir, out string r1);
        bool chainOn = BossRewardManager.TryGetChain(out _);
        bool second = Boss.EditorGrant(Zincir, out string r2);
        Boss.ClearAll();
        var broken = ChainCopy(new[] { float.NaN, .5f });
        Expect("Boss ödülü uygulanamadı");
        bool invalid = Boss.EditorGrant(broken, out string r3);
        bool invalidOff = !BossRewardManager.TryGetChain(out _) && Boss.Taken.Count == 0;
        SetP(Boss, "IsPending", true);
        bool pending = Boss.EditorGrant(Zincir, out string r4);
        SetP(Boss, "IsPending", false);
        string menu = File.ReadAllText("Assets/Editor/ChainDebugMenu.cs");
        var method = typeof(BossRewardManager).GetMethod("EditorGrant");
        Require(first && chainOn && !second && !invalid && invalidOff && !pending && !menu.Contains("System.Reflection") && !menu.Contains("GetField") && method != null,
            $"Editor-only grant uses the game's own checks and apply path: first grant works (chain on); a second is refused ({r2}); an invalid chain copy is refused with nothing applied ({r3}); refused while a boss offer is pending ({r4}); ChainDebugMenu no longer touches private fields by reflection");
        Boss.ClearAll();
        return .05;
    }

    // ================================================================ ortak alan çizgisi
    static double AreaOutlineCase()
    {
        string path = "Assets/Scripts/Efects/AreaOutlineFeedback.cs";
        bool renamed = File.Exists(path) && !File.Exists("Assets/Scripts/Efects/AftershockAreaFeedback.cs") && AssetDatabase.AssetPathToGUID(path) == AreaGuid &&
                       typeof(GameManager).Assembly.GetType("AftershockAreaFeedback") == null;
        ClearField();
        Place("S", C, C, "Explosive");
        var grid = GridManager.Instance.GetGridSystem();
        var footprint = new List<GridPosition> { CellAt(C, C).GetGridPosition() };
        var targets = new List<GridPosition> { CellAt(C + 1, C).GetGridPosition(), CellAt(C - 1, C).GetGridPosition() };
        AreaOutlineFeedback.Prewarm();
        var area = AreaOutlineFeedback.Instance;
        area.Clear();
        int a0 = area.AftershockPlayed, c0 = area.ChainPlayed, as0 = area.AftershockSkipped, cs0 = area.ChainSkipped;
        for (int i = 0; i < AreaOutlineFeedback.Capacity; i++) AreaOutlineFeedback.PlayAftershock(grid, footprint, targets);
        bool skipA = !AreaOutlineFeedback.PlayAftershock(grid, footprint, targets), skipC = !AreaOutlineFeedback.PlayChainSource(grid, footprint);
        area.Clear();
        bool chain = AreaOutlineFeedback.PlayChainSource(grid, footprint);
        Require(renamed && area.AftershockPlayed - a0 == AreaOutlineFeedback.Capacity && skipA && skipC && area.AftershockSkipped - as0 == 1 && area.ChainSkipped - cs0 == 1 && chain && area.ChainPlayed - c0 == 1,
            $"Shared area outline: the class is now AreaOutlineFeedback (same file GUID, old name gone); one pool of {AreaOutlineFeedback.Capacity} waves for both uses — when the aftershock outlines fill it, both an aftershock and a chain-source outline are skipped and each is counted separately");
        area.Clear();
        ClearField();
        return .05;
    }

    static double FinalCheck()
    {
        Require(errors == 0 && expected.Count == 0, $"Every deliberately produced error was reported where expected and nothing else was logged as an error ({errors} unexpected, {expected.Count} expected but missing)");
        foreach (var copy in copies) if (copy != null) Object.DestroyImmediate(copy);
        return .05;
    }
}
