using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.0 — yük biriktiren elektrikten eski (doğrudan hasatta şansla) elektriğe dönüşün kabul testi.
// A) Eski deney profili (Deney22_20) ve sahnede kalmış bir ElectricChargeManager bileşeni yük modunu açamaz; RoundManager bileşeni kurmaz.
// B) Elektrik: şans 0 → yok; şans 1 → öldürmeyen vuruşta yok, doğrudan hasatta tam bir kez; hasar ve ödül tek; uzmanlaşma katsayısı bir kez;
//    efekt havuzu doluyken hasar uygulanır; elektrik öldürmesi zincir yapmaz.
// C) Temizlik: round sonu, run sonu, yeniden başlatma, ana menü ve tekrar oyun sahnesi.
// D) Kısa his örneği (bot, insan verisi değil): aynı düzen ve seed ile erken ve ileri oyun stat seti; elektrik sayısı ve şarj halkası yokluğu.
[InitializeOnLoad]
public static class ElectricRevertVerification
{
    const string Key = "ElectricRevertVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const float FrameTime = 1f / 30f;
    static readonly List<string> notes = new();
    static int step; static double nextAt;
    static int shownStep = -1; static double stepSince;

    static ElectricRevertVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Deney22_20.asset");
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
            if (nextAt == 0) nextAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/ElectricRevertVerification.txt", new[] { $"RUNNING step {step}" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240)
                throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, round {RoundManager.Instance?.CurrentRound}");
            // İşlevsel adımlarda round kendi süresiyle bitmesin.
            if (keepAlive && RoundManager.Instance != null && RoundManager.Instance.IsRoundActive) SetP(RoundManager.Instance, "RemainingTime", 1000f);
            double wait = Run(step++);
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ElectricRevertVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static RoundManager RM => RoundManager.Instance;
    static SpecializationManager SM => SpecializationManager.Instance;
    static HarvestBehaviorManager HBM => HarvestBehaviorManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static StatModifier Mod(StatType s, StatTarget t, float v) => new StatModifier { statType = s, target = t, operation = ModifierOperation.Set, value = v };
    static PlanterSO Planter1x1 => AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static PlantSO Grass => AssetDatabase.LoadAssetAtPath<PlantSO>("Assets/ScriptableObjects/Plants/Grass.asset");
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z).ThenBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z).ToList();
    static void RespawnAll() { foreach (var s in Spawners()) if (F<GameObject>(s, "spawnedPlant") == null) Call(s, "TrySpawnPlant"); }
    static PlantHealth PlantAt(GridObject cell) { var p = cell.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static int Triggered => HarvestBehaviorStats.Triggered(DamageType.Electric);
    static int Gold => ResourceManager.Instance.GetResourceAmount(ResourceType.Gold);
    static int RingObjects => Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "Electric Charge Ring" && g.scene.IsValid());
    static int ChargeManagers => Object.FindObjectsByType<ElectricChargeManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    static bool keepAlive;
    // Batch'te kareler çok hızlı akar (0,1 sn gerçek ≈ 10 sn oyun); round başlar başlamaz süresi uzatılır ki beklemede bitmesin.
    static void KeepRoundAlive() { keepAlive = true; if (RM.IsRoundActive) SetP(RM, "RemainingTime", 1000f); }

    static void ApplyTile(GridObject cell, TileModifierSO so, float value)
    {
        if (so == null) { cell.GetGroundCellCached().ApplyModifier(null); return; }
        var mods = so.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value < 0 ? r.maxValue : value }).ToList();
        cell.GetGroundCellCached().ApplyModifier(so, mods);
    }

    static PlanterBrain Place1x1(GridObject cell, TileModifierSO tile = null, float value = -1)
    {
        if (tile != null) ApplyTile(cell, tile, value);
        var planter = Object.Instantiate(Planter1x1.prefab);
        planter.transform.position = cell.GetGroundCellCached().transform.position;
        cell.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(Planter1x1, new List<GridObject> { cell }); cell.SetPlanterBrain(brain);
        return brain;
    }

    static StatModifier radiusMod, damageMod; static bool hasRadius, hasDamage;
    static void SetDamage(float v) { if (hasDamage) Stats.RemoveGlobalModifier(damageMod); hasDamage = true; damageMod = Mod(StatType.HarvestDamage, StatTarget.Player, v); Stats.AddGlobalModifier(damageMod); }
    static void Hit(GridObject cell, float radius = .1f)
    {
        if (hasRadius) Stats.RemoveGlobalModifier(radiusMod);
        hasRadius = true; radiusMod = Mod(StatType.AreaRadius, StatTarget.Player, radius); Stats.AddGlobalModifier(radiusMod);
        Call(Object.FindFirstObjectByType<PlayerController>(), "AttackInRadius", cell.GetGroundCellCached().transform.position);
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Start();
            case 1: return FieldSetup();
            case 2: return NoChargeAfterFrames();
            case 3: return ElectricRules();
            case 4: return RoundEndCleanup();
            case 5: return RunEndCleanup();
            case 6: return AfterRestart();
            case 7: return InMenu();
            case 8: return NormalProfile();
            case 9: return StartSamples();
            default: return SampleStep();
        }
    }

    // ---------------- A) profil ve bileşen ----------------
    static double Start()
    {
        var exp = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Deney22_20.asset");
        Require(RM.Profile == exp, "Scene runs the former experiment profile Deney22_20");
        Require(ChargeManagers == 0 && ElectricChargeManager.Instance == null && !ElectricChargeManager.ChargeMode, "RoundManager does not create ElectricChargeManager; charge mode off");
        foreach (var name in new[] { "Prototip10", "Prototip10_DebugButce", "Uzmanlasma20", "UzunRun130", "Deney22_20" })
        {
            var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            Require(p != null && p.electricMode == ElectricTriggerMode.KillChance, $"{name}: electric mode field is kill-trigger");
        }
        // Eski veri/sahne senaryosu: profil alanı Charge'a çekilir ve bileşen elle eklenir → yine de yük modu yok.
        RM.Profile.electricMode = ElectricTriggerMode.Charge;
        Require(!ElectricChargeManager.ChargeMode, "Profile field set to Charge at runtime: charge mode still off (no component)");
        RM.gameObject.AddComponent<ElectricChargeManager>();
        Require(ElectricChargeManager.Instance != null && !ElectricChargeManager.ChargeMode && ElectricChargeManager.Instance.Mode == ElectricTriggerMode.KillChance,
            "Stale ElectricChargeManager + Charge profile: Mode stays KillChance");
        return .1;
    }

    // ---------------- B) elektrik kuralları ----------------
    static PlanterBrain A, B;
    static double FieldSetup()
    {
        GridUnlockManager.Instance.UnlockNextTier(5);
        Stats.AddGlobalModifier(Mod(StatType.CritChance, StatTarget.Player, 0f)); SetDamage(3f);
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
        {
            var brain = Place1x1(Cell(x, z));
            if (x == -1 && z == -1) A = brain; else if (x == 1 && z == 1) B = brain;
        }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        SetP(RM, "CurrentRound", 13); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        Require(RM.CurrentRound == 14 && State == GameStates.Round, "Round 14 running");
        KeepRoundAlive();
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
        RespawnAll(); HarvestBehaviorStats.Reset();
        ApplyTile(Cell(-1, -1), Tile("Electric", "Legendary"), 1f);
        ApplyTile(Cell(1, 1), Tile("Electric", "Legendary"), 1f);
        Require(Mathf.Approximately(A.GetFinalStat(StatType.ElectricChance), 1f) && Mathf.Approximately(B.GetFinalStat(StatType.ElectricChance), 1f), "Electric chance 1 on A and B");
        return .3; // kareler geçsin (eski bileşen yük doldurabilir mi?)
    }

    static double NoChargeAfterFrames()
    {
        Require(RM.IsRoundActive && State == GameStates.Round, "Round still active after the frame wait (charge would have filled here)");
        var ec = ElectricChargeManager.Instance;
        Require(ec.ChargingPlanters == 0 && ec.Discharges == 0 && !ec.IsReady(A) && ec.Progress(A) == 0f && RingObjects == 0 && !ElectricChargeManager.TryDischarge(A, 5),
            "After active round frames: no charge, no ring objects, discharge impossible");
        return .02;
    }

    static (int events, int gold, long score, double xp) Snapshot(Action act)
    {
        int events = 0; void Count(PlantHealth h) => events++;
        PlantHealth.AnyHarvested += Count;
        int g = Gold; long s = HarvestScoreManager.Instance.TotalScore; double x = ProgressionManager.Instance.TotalXPEarned;
        act();
        PlantHealth.AnyHarvested -= Count;
        return (events, Gold - g, HarvestScoreManager.Instance.TotalScore - s, ProgressionManager.Instance.TotalXPEarned - x);
    }

    static void GrassAll() { for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) { var p = PlantAt(Cell(x, z)); if (p != null) p.GetComponent<PlantResource>().Initialize(Grass, Cell(x, z).GetPlanterBrain()); } }

    // A'nın bitkisini öldürücü son vuruşa kadar (öldürmeyen vuruşlarla) getirir; her öldürmeyen vuruşta elektrik olmamalı.
    static int WearDown(float finalDamage)
    {
        var plant = PlantAt(Cell(-1, -1)); int nonKilling = 0;
        SetDamage(3f);
        while (plant.CurrentHealth > finalDamage * .85f && plant.CurrentHealth > 3)
        {
            int tr = Triggered; Hit(Cell(-1, -1)); nonKilling++;
            if (plant.IsDead) throw new Exception("wear-down hit killed the plant");
            if (Triggered != tr) throw new Exception("non-killing hit fired electric");
        }
        SetDamage(finalDamage);
        return nonKilling;
    }

    static double ElectricRules()
    {
        Require(RM.IsRoundActive, "Round active for the electric checks");
        // Şans 0: öldürme elektrik vermez.
        ApplyTile(Cell(-1, -1), null, 0);
        Require(Mathf.Approximately(A.GetFinalStat(StatType.ElectricChance), 0f), "A: electric chance 0 after removing the tile");
        RespawnAll(); SetDamage(100f);
        var mid = PlantAt(Cell(0, 0)); int midHp = mid.CurrentHealth; int tr = Triggered;
        Hit(Cell(-1, -1));
        Require(Triggered == tr && mid.CurrentHealth == midHp, "Chance 0: direct harvest fires no electric");
        ApplyTile(Cell(-1, -1), Tile("Electric", "Legendary"), 1f);
        Require(Mathf.Approximately(A.GetFinalStat(StatType.ElectricChance), 1f), "A: electric chance back to 1 after re-applying the tile");

        // Şans 1: öldürmeyen vuruşlar yok, öldürücü doğrudan vuruş tam bir elektrik; hasar hedef başına bir kez; tek ödül.
        RespawnAll(); GrassAll();
        int nonKilling = WearDown(8f);
        mid = PlantAt(Cell(0, 0)); var bPlant = PlantAt(Cell(1, 1)); midHp = mid.CurrentHealth; int bHp = bPlant.CurrentHealth;
        tr = Triggered; int skipped = HarvestBehaviorStats.Skipped(DamageType.Electric);
        var aPlant = PlantAt(Cell(-1, -1)); int aHpBefore = aPlant.CurrentHealth;
        var single = Snapshot(() => Hit(Cell(-1, -1)));
        Note($"tanı: A canı {aHpBefore} → öldü {aPlant.IsDead} · tetik farkı {Triggered - tr} · atlanan farkı {HarvestBehaviorStats.Skipped(DamageType.Electric) - skipped} · A şansı {A.GetFinalStat(StatType.ElectricChance)} · round açık {RM.IsRoundActive} · durum {State} · hasat olayı {single.events} · merkez {(mid.IsDead ? "ölü" : "canlı")} · B {(bPlant.IsDead ? "ölü" : "canlı")} · aktif elektrik {HBM.ActiveElectricBursts}");
        Require(nonKilling > 0, $"Guaranteed chance: {nonKilling} non-killing hits fired no electric");
        Require(Triggered == tr + 1 && midHp - mid.CurrentHealth == 8 && bHp - bPlant.CurrentHealth == 8,
            $"Killing direct hit → exactly one electric; each diagonal target hit once for 8 ({midHp - mid.CurrentHealth}, {bHp - bPlant.CurrentHealth})");
        Require(single.events == 1 && single.gold == 2 && single.score == 1, $"One kill → one harvest reward (events {single.events}, Gold +{single.gold}, score +{single.score})");

        // Uzmanlaşma davranış katsayısı bir kez (Davranış Ustası ×1,25: 8 → 10).
        SetP(SM, "Chosen", AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/DavranisUstasi.asset"));
        RespawnAll(); GrassAll();
        WearDown(8f);
        mid = PlantAt(Cell(0, 0)); midHp = mid.CurrentHealth; tr = Triggered;
        Hit(Cell(-1, -1));
        Require(Triggered == tr + 1 && midHp - mid.CurrentHealth == A.GetBehaviorDamage(8, DamageType.Electric) && midHp - mid.CurrentHealth == 10,
            $"Behavior coefficient applied once: 8 × 1,25 = {midHp - mid.CurrentHealth}");
        SetP(SM, "Chosen", null);

        // Öldürücü elektrik: 3 hasat (A doğrudan + 2 elektrik), elektrik öldürmeleri zincir yapmaz (B'nin şansı 1 olsa da).
        RespawnAll(); GrassAll(); SetDamage(100f); tr = Triggered;
        var lethal = Snapshot(() => Hit(Cell(-1, -1)));
        Require(Triggered == tr + 1 && lethal.events == 3 && lethal.gold == 6 && lethal.score == 3,
            $"Lethal electric: one electric, 3 harvests, 3 rewards; electric kills do not chain (events {lethal.events}, Gold +{lethal.gold})");

        // Havuz dolu: çizim atlanır, hasar uygulanır.
        RespawnAll(); GrassAll();
        WearDown(8f);
        int max = F<int>(HBM, "maxElectricBursts"), guard = 0;
        while (HBM.ActiveElectricBursts < max && guard++ < 100) HBM.TryElectric(B, 0);
        Require(HBM.ActiveElectricBursts == max, $"Electric effect pool full ({max})");
        mid = PlantAt(Cell(0, 0)); midHp = mid.CurrentHealth; int visual = HBM.SkippedElectricVisuals; tr = Triggered;
        Hit(Cell(-1, -1));
        Require(Triggered == tr + 1 && HBM.SkippedElectricVisuals == visual + 1 && midHp - mid.CurrentHealth == 8, "Pool full: visual skipped, electric damage still applied");
        Require(HarvestBehaviorStats.Skipped(DamageType.Electric) == skipped, "Electric triggers never reported as skipped");
        return .02;
    }

    // ---------------- C) temizlik ----------------
    static void FireBursts() { RespawnAll(); for (int k = 0; k < 3; k++) HBM.TryElectric(B, 0); }

    static double RoundEndCleanup()
    {
        HBM.ClearAll(); FireBursts();
        Require(HBM.ActiveElectricBursts > 0, "Electric visuals active before round end");
        keepAlive = false;
        Call(RM, "EndRound");
        Require(HBM.ActiveElectricBursts == 0 && ElectricChargeManager.Instance.ChargingPlanters == 0, "Round end returns every electric visual");
        if (State == GameStates.CardSelection)
        {
            var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include); int g = 0;
            while (State == GameStates.CardSelection && g++ < 60) Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]);
        }
        RM.StartNextRound();
        Require(RM.CurrentRound == 15 && State == GameStates.Round, "Next round started");
        KeepRoundAlive();
        return .05;
    }

    static double RunEndCleanup()
    {
        FireBursts();
        Require(HBM.ActiveElectricBursts > 0, "Electric visuals active before run end");
        keepAlive = false;
        GameManager.Instance.CompleteRun();
        Require(HBM.ActiveElectricBursts == 0 && RingObjects == 0, "Run end returns every electric visual; no ring objects");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static double AfterRestart()
    {
        hasDamage = hasRadius = false;
        Require(RM.Profile.name == "Deney22_20" && ChargeManagers == 0 && ElectricChargeManager.Instance == null && HBM.ActiveElectricBursts == 0 && State == GameStates.RunSetup,
            "Restart: fresh run, no charge component, no electric visuals");
        Require(RM.Profile.electricMode == ElectricTriggerMode.Charge && !ElectricChargeManager.ChargeMode, "Profile still says Charge in memory after restart: charge mode stays off");
        Stats.AddGlobalModifier(Mod(StatType.CritChance, StatTarget.Player, 0f)); SetDamage(100f);
        GridUnlockManager.Instance.UnlockNextTier(5);
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) { var b = x == -1 && z == -1 ? Place1x1(Cell(x, z), Tile("Electric", "Legendary"), 1f) : Place1x1(Cell(x, z)); if (x == -1 && z == -1) A = b; }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        RM.StartNextRound(); RespawnAll(); HarvestBehaviorStats.Reset();
        int tr = Triggered; Hit(Cell(-1, -1));
        Require(Triggered == tr + 1, "Restarted run: kill-triggered electric works");
        GameManager.Instance.CompleteRun();
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static double InMenu()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && ChargeManagers == 0 && RingObjects == 0 && ElectricChargeManager.Instance == null,
            "Main menu: menu scene loaded, no charge component or ring objects");
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Uzmanlasma20.asset");
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double NormalProfile()
    {
        hasDamage = hasRadius = false;
        Require(RM.Profile.name == "Uzmanlasma20" && ChargeManagers == 0 && !ElectricChargeManager.ChargeMode && !RM.FixedRoundDuration, "Back from menu on Uzmanlasma20: no charge component");
        Stats.AddGlobalModifier(Mod(StatType.CritChance, StatTarget.Player, 0f)); SetDamage(100f);
        GridUnlockManager.Instance.UnlockNextTier(5);
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) { var b = x == -1 && z == -1 ? Place1x1(Cell(x, z), Tile("Electric", "Legendary"), 1f) : Place1x1(Cell(x, z)); if (x == -1 && z == -1) A = b; }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        RM.StartNextRound(); RespawnAll(); HarvestBehaviorStats.Reset();
        int tr = Triggered; Hit(Cell(-1, -1));
        Require(Triggered == tr + 1, "Normal profile: kill-triggered electric works");
        return .05;
    }

    // ---------------- D) kısa his örneği (bot) ----------------
    // Aynı 5×5 düzen (25 adet 1×1 saksı, 8 Legendary elektrik tile'ı, şans 0,5) ve aynı seed'ler; yalnız stat seti değişir.
    static readonly (string name, float damage, float interval, float radius, int round)[] Sets =
    {
        ("erken · hasar 14 · aralık 3 sn · alan 1 · R5 canı", 14f, 3f, 1f, 5),
        ("ileri · hasar 743 · aralık 2,03 sn · alan 1,1 · R40 canı", 743f, 2.03f, 1.1f, 40),
    };
    const int Seeds = 3;
    static RunProfileSO sampleProfile;
    static int set, seedIndex; static bool running, setUp;
    static Bot bot;
    static readonly List<(int set, int seed, int[] kills, int electric, int visualSkips, int attacks, int rings, int fresh, int freshKills)> rows = new();
    static int trig0, visual0;

    static double StartSamples()
    {
        sampleProfile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Deney22_20.asset"));
        sampleProfile.name = "Deney22_ornek"; sampleProfile.segmentRounds = 1000; sampleProfile.runLength = 1000;
        sampleProfile.events = new List<SegmentEventEntry>(); sampleProfile.specializationAfterSegment = 0; sampleProfile.specializationOptions = new List<SpecializationSO>();
        sampleProfile.fixedRoundDuration = 45f;
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = sampleProfile;
        set = 0; setUp = false;
        Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static readonly (int, int)[] ElectricSpots = { (-1, -1), (1, 1), (-2, 0), (2, 0), (0, -2), (0, 2), (-1, 1), (1, -1) };

    static double SampleStep()
    {
        if (!setUp)
        {
            hasDamage = hasRadius = false;
            var s = Sets[set];
            Require(RM.Profile == sampleProfile && ChargeManagers == 0, $"Sample scene ({s.name}): no charge component");
            GridUnlockManager.Instance.UnlockNextTier(5);
            Stats.AddGlobalModifier(Mod(StatType.HarvestDamage, StatTarget.Player, s.damage));
            Stats.AddGlobalModifier(Mod(StatType.AttackSpeed, StatTarget.Player, s.interval));
            Stats.AddGlobalModifier(Mod(StatType.AreaRadius, StatTarget.Player, s.radius));
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                if (Array.IndexOf(ElectricSpots, (x, z)) >= 0) Place1x1(Cell(x, z), Tile("Electric", "Legendary"), .5f); else Place1x1(Cell(x, z));
            Object.FindFirstObjectByType<PlayerController>().enabled = false;
            bot = new GameObject("Harvest bot (verification)").AddComponent<Bot>();
            SetF(RM, "awaitingFirstRound", false);
            setUp = true; seedIndex = 0; running = false;
            return .3;
        }
        if (running)
        {
            if (State == GameStates.Round) return .25;
            rows.Add((set, seedIndex, (int[])bot.Kills.Clone(), Triggered - trig0, HBM.SkippedElectricVisuals - visual0, bot.Attacks, bot.MaxRings, bot.FreshHits, bot.FreshKills));
            running = false; seedIndex++;
            int g = 0; if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) if (g++ > 200) throw new Exception("cards");
        }
        if (seedIndex == Seeds)
        {
            Report(set);
            if (set < Sets.Length - 1) { set++; setUp = false; Time.captureDeltaTime = 0f; SceneManager.LoadScene("GameScene"); return 3; }
            Require(rows.All(r => r.rings == 0), "No charge ring object appeared in any sample round");
            Require(rows.Where(r => r.set == 0).Sum(r => r.electric) > 0 && rows.Where(r => r.set == 1).Sum(r => r.electric) > 0, "Electric fired in early and late samples");
            return -1;
        }
        BeginSampleRound(seedIndex);
        running = true;
        return .5;
    }

    static void BeginSampleRound(int seed)
    {
        var spawners = Spawners();
        foreach (var s in spawners) { s.RemoveSpawnedPlant(); s.enabled = true; }
        HBM.ClearAll(); Call(TornadoManager.Instance, "ClearAll"); HarvestBehaviorStats.Reset();
        UnityEngine.Random.InitState(900 + seed);
        SetP(RM, "CurrentRound", Sets[set].round - 1);
        RM.StartNextRound();
        if (RM.CurrentRound != Sets[set].round || State != GameStates.Round) throw new Exception("sample round did not start");
        foreach (var s in spawners) SetF(s, "timer", UnityEngine.Random.Range(0f, (float)Call(s, "GetEffectiveSpawnInterval")));
        bot.Begin(spawners);
        trig0 = Triggered; visual0 = HBM.SkippedElectricVisuals;
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void Report(int s)
    {
        var r = rows.Where(x => x.set == s).ToList();
        string Avg(Func<(int set, int seed, int[] kills, int electric, int visualSkips, int attacks, int rings, int fresh, int freshKills), double> f) =>
            $"{r.Average(f):0.#} [{r.Min(f):0}–{r.Max(f):0}]";
        Note($"--- his örneği: {Sets[s].name} ({Seeds} seed × 45 sn; bot, insan verisi değil) ---");
        Note($"doğrudan hasat {Avg(x => x.kills[0])} · elektrik tetik {Avg(x => x.electric)} · elektrik öldürmesi {Avg(x => x.kills[(int)DamageType.Electric])} · " +
             $"vuruş {Avg(x => x.attacks)} · tek vuruş (taze) %{r.Sum(x => x.freshKills) * 100.0 / Math.Max(1, r.Sum(x => x.fresh)):0} · elektrik görseli atlanan {r.Sum(x => x.visualSkips)} · şarj halkası nesnesi {r.Max(x => x.rings)}");
    }

    sealed class Bot : MonoBehaviour
    {
        public readonly int[] Kills = new int[5];
        public int Attacks, FreshHits, FreshKills, MaxRings;
        float timer; MethodInfo attack; PlayerController player;
        readonly List<(PlantHealth h, bool fresh)> scratch = new();
        void OnEnable() { PlantHealth.AnyHarvested += Count; }
        void OnDisable() { PlantHealth.AnyHarvested -= Count; }
        void Count(PlantHealth h) { if (GameManager.Instance.CurrentState == GameStates.Round) Kills[(int)h.KilledBy]++; }
        public void Begin(List<PlantSpawner> list) { Array.Clear(Kills, 0, 5); Attacks = FreshHits = FreshKills = MaxRings = 0; timer = 0; }
        void Update()
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            SetF(RoundManager.Instance, "pendingCardSelections", 0);
            if (Time.frameCount % 30 == 0) MaxRings = Math.Max(MaxRings, RingObjects);
            if (player == null) { player = FindFirstObjectByType<PlayerController>(); attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance); }
            float interval = Mathf.Max(StatManager.Instance.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RoundManager.Instance.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            float radius = StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var grid = GridManager.Instance.GetGridSystem();
            Vector3 best = Vector3.zero; int bestCount = 0;
            for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
            {
                var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                if (ground == null || ground.IsLocked) continue;
                int n = grid.GetGridObjectsInRadius(ground.transform.position, radius).Count(g => g.GetPlantObject() != null && g.GetPlantObject().TryGetComponent(out PlantHealth h) && !h.IsDead);
                if (n > bestCount) { bestCount = n; best = ground.transform.position; }
            }
            if (bestCount <= 0) return;
            scratch.Clear();
            foreach (var g in grid.GetGridObjectsInRadius(best, radius))
            {
                var p = g.GetPlantObject();
                if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) scratch.Add((h, h.CurrentHealth >= h.MaxHealth));
            }
            attack.Invoke(player, new object[] { best });
            Attacks++;
            foreach (var (h, fresh) in scratch) { if (!fresh) continue; FreshHits++; if (h == null || h.IsDead) FreshKills++; }
        }
    }
}
