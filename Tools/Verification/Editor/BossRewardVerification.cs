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
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.4 — her 5 round'da boss, ayrı boss hasadı hedefi ve boss ödülleri (işlev testi; denge ölçümü ayrı dosyada).
// Run50_BossPrototip, sabit boss seed'i (yalnız izole kopyada). Kayıt bellekte: kullanıcının kaydına ve seçimine dokunmaz.
// Akış: önizleme → R1–4 etkisiz → R5 boss (kota önceden dolu, boss hedefi 1 eksik → kayıp, ödül yok) → yeniden başlat (aynı seed, aynı boss)
// → R5 hedefe eşit → kartlar → ödül (tek seçim) → altı ödülün kapsamı, birikme, uygunluk → az / hiç uygun ödül → R50 → temizlik
// → Sert Kabuk (stok, oran, tek çarpan, havuz, sızıntı) → Sis ve yedek (kuralsız boss, tekrar).
[InitializeOnLoad]
public static class BossRewardVerification
{
    const string Key = "BossRewardVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string ProfilePath = Profiles + "Run50_BossPrototip.asset";
    const string Rewards = "Assets/ScriptableObjects/BossRewards/";
    const string MenuPath = "Tools/Run Profili/Run50 Boss Prototip · 50 round (boss + ödül)";
    const int Seed = 4242;
    static readonly long[] Targets = { 15, 40, 60, 80, 105, 120, 135, 150, 170 };
    static readonly List<string> notes = new();
    static int step; static double nextAt;

    static BossRewardVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: prototip profili seçilir ve boss seed'i sabitlenir.
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        profile.bossSeed = Seed;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = profile;
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static int shownStep = -1; static double stepSince;
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
                File.WriteAllLines("Logs/BossRewardVerification.txt", new[] { $"RUNNING step {step}" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240)
                throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, scene {SceneManager.GetActiveScene().name}");
            if (RoundManager.Instance != null && RoundManager.Instance.IsRoundActive) SetP(RoundManager.Instance, "RemainingTime", 1000f);
            double wait = Run(step++);
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountWarnings;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/BossRewardVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", "<[^>]+>", "").Replace('\n', '/');
    static bool Near(float a, float b, float eps = 1e-4f) => Mathf.Abs(a - b) <= eps;
    static RoundManager RM => RoundManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static Vector3 Pos(int dx, int dz) => Cell(dx, dz).GetGroundCellCached().transform.position;
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", Score + amount);
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static int CountMod(StatModifier m) => Stats.GlobalModifiers.Count(x => x.Equals(m));
    static PlanterSO PlanterAsset(string n) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {n}.asset");
    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static PlantSO Plant(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    static BossRewardSO Reward(string n) => AssetDatabase.LoadAssetAtPath<BossRewardSO>(Rewards + n + ".asset");
    static BossRewardSO Keskin => Reward("KeskinBicak");
    static BossRewardSO Hizli => Reward("HizliBilek");
    static BossRewardSO Bereket => Reward("BereketliToprak");
    static BossRewardSO Nadir => Reward("NadirTohum");
    static BossRewardSO Bilgi => Reward("BilgiFilizi");
    static BossRewardSO Kivilcim => Reward("Kivilcim");
    static SegmentEventSO Don => AssetDatabase.LoadAssetAtPath<SegmentEventSO>("Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset");
    static HardShellSO Shell => AssetDatabase.LoadAssetAtPath<HardShellSO>("Assets/ScriptableObjects/SegmentEvents/SertKabuk.asset");
    static FogSO Fog => AssetDatabase.LoadAssetAtPath<FogSO>("Assets/ScriptableObjects/SegmentEvents/Sis.asset");
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z).ToList();
    static PlantSpawner SpawnerAt(GridPosition p) => Spawners().First(s => s.GridObject.GetGridPosition().x == p.x && s.GridObject.GetGridPosition().z == p.z);
    static PlantHealth PlantOf(PlantSpawner s) { var p = F<GameObject>(s, "spawnedPlant"); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static PlantHealth PlantAt(int dx, int dz) { var p = Cell(dx, dz).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static int warnings;
    static void CountWarnings(string message, string stack, LogType type) { if (type == LogType.Warning && message.Contains("RemoveGlobalModifier")) warnings++; }

    static void Respawn()
    {
        foreach (var s in Spawners()) { s.RemoveSpawnedPlant(); s.enabled = true; Call(s, "TrySpawnPlant"); }
    }

    static void Flush()
    {
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        int guard = 0;
        while (State == GameStates.CardSelection)
        {
            if (guard++ > 60) throw new Exception("card selection did not finish");
            Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]);
        }
    }

    static PlanterSO Only(PlantSO plant)
    {
        var so = Object.Instantiate(PlanterAsset("1x1"));
        so.spawnTable = new List<PlantSpawnEntry> { new PlantSpawnEntry { plant = plant, baseChance = 100 } };
        return so;
    }

    static PlanterBrain Place(PlanterSO so, int dx, int dz, TileModifierSO tile = null, float value = 1f)
    {
        var cell = Cell(dx, dz);
        if (tile != null)
            cell.GetGroundCellCached().ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value }).ToList());
        var planter = Object.Instantiate(so.prefab);
        planter.transform.position = cell.GetGroundCellCached().transform.position;
        cell.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(so, new List<GridObject> { cell });
        cell.SetPlanterBrain(brain);
        return brain;
    }

    static void ClearField()
    {
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Cell(x, z).GetGroundCellCached().ApplyModifier(null);
    }

    static void Attack(Vector3 position) => Call(Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include), "AttackInRadius", position);

    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (CountMod(m) > 0) Stats.RemoveGlobalModifier(m); testMods.Clear(); }

    // İstenen round'u başlatır (önceki round bitmiş olmalı); round kendi süresiyle bitmez (Tick), oyuncu elle vurulur.
    static void StartRound(int round)
    {
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
    }

    static void EndRound() => Call(RM, "EndRound");
    // Oyuncu kendi kendine vurmasın: testte vuruşlar elle yapılır.
    static void DisablePlayer() => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;

    // Ödülü gerçek seçim yolundan verir (teklife koyup Choose): kapsam testleri için belirli ödül.
    static bool Grant(BossRewardSO reward)
    {
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetF(Boss, "offerPrepared", true); // Explicit prepared-offer fixture, not the card-to-reward flow.
        SetP(Boss, "IsPending", true);
        bool taken = Boss.Choose(reward);
        if (!taken) { SetP(Boss, "IsPending", false); offer.Clear(); }
        return taken;
    }

    static Dictionary<string, string> HudTexts()
    {
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Call(hud, "Refresh");
        return hud.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeSelf).ToDictionary(t => t.name, t => Strip(t.text));
    }

    static string RunEndText()
    {
        var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        return Strip(complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
    }

    static List<GridPosition> OpenZone(SegmentEventRuntime e) => e.ZoneCells.Where(p => { var g = Grid.GetGridObject(p)?.GetGroundCellCached(); return g != null && !g.IsLocked; })
        .OrderBy(p => p.x * 100 + p.z).ToList();

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Setup();
            case 1: return Preview();
            case 2: return FailRun();
            case 3: return FailScreen();
            case 4: return PassRun();
            case 5: return RewardChoice();
            case 6: return RewardScope();
            case 7: return FewAndNoOffers();
            case 8: return FewOffersPanelAndNone();
            case 9: return RunEndAndRestart();
            case 10: return CleanAfterRestart();
            case 11: return MenuClean();
            case 12: return HardShellSetup();
            case 13: return HardShellBoss();
            case 14: return HardShellPoolAndEnd();
            case 15: return FogSetup();
            case 16: return FogBoss();
            case 17: return FogMenu();
            case 18: return FinalCheck();
            default: return -1;
        }
    }

    // ---------------- 0) profil, menü, seçim kuralları ----------------
    static double Setup()
    {
        Application.logMessageReceived += CountWarnings;
        var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        var reference = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_Referans.asset");
        Require(p != null && p.runLength == 50 && p.segmentRounds == 5 && p.startingGold == 80 && p.startingIron == 0 && p.startingStone == 0 && !p.debugBudget,
            "Run50_BossPrototip: 50 rounds, 5-round segments, normal start 80 / 0 / 0");
        Require(Enumerable.Range(1, 10).All(s => p.TargetFor(s) == reference.TargetFor(s)) && p.events.Count == 0 && p.specializationAfterSegment == 0 && p.fixedRoundDuration == 0f,
            "Segment quotas equal Run50_Referans (not rebalanced); no legacy event list, no specialization");
        Require(p.bossPool != null && p.bossPool.entries.Select(e => e.boss).SequenceEqual(new SegmentEventSO[] { Don, Shell, Fog }) && p.bossPool.entries.All(e => e.weight == 1f),
            "Boss pool: Don Cephesi, Sert Kabuk, Sis (equal weights, data-driven)");
        Require(p.bossTargets.SequenceEqual(Targets) && Enumerable.Range(1, 9).All(s => p.HasBoss(s) && p.BossTargetFor(s) == Targets[s - 1]) && !p.HasBoss(10) && p.BossTargetFor(10) == 0,
            "Boss targets table for R5…R45: " + string.Join("/", Targets) + "; no boss on the final segment (R50)");
        Require(p.bossRewards != null && p.bossRewards.choices == 3 && p.bossRewards.rewards.SequenceEqual(new[] { Keskin, Hizli, Bereket, Nadir, Bilgi, Kivilcim }) &&
                p.bossRewards.rewards.All(r => r.maxStacks == 3 && r.weight == 1f), "Reward pool: six rewards, 3 choices, max 3 stacks, equal weights");
        foreach (string name in new[] { "Prototip10", "Prototip10_DebugButce", "Uzmanlasma20", "Deney22_20", "UzunRun130", "Run50_Referans" })
        {
            var other = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            if (other.bossPool != null || other.bossTargets.Count != 0 || other.bossRewards != null || other.HasBoss(1)) throw new Exception(name + " has boss data");
        }
        Require(true, "Older profiles and Run50_Referans carry no boss data (legacy event path)");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>().Select(a => a)).ToList();
        Require(items.Count(a => a.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);

        Require(State == GameStates.RunSetup && RM.Profile == p && Events.BossMode && Events.RunSeed == Seed, "GameScene runs the prototype in boss mode with the fixed seed");
        var loadout = StartLoadoutManager.Instance;
        Require(loadout != null && loadout.Farmer != null && loadout.Farmer.id == "bahcivan" && loadout.Scythe.id == "standart" && MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly,
            "Start selection system active (neutral Bahçıvan + Standart; batch save in memory only)");
        Require(ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == 80 && Boss != null && !Boss.IsPending && Boss.Taken.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f,
            "Normal economy; reward manager present with nothing taken");

        // Seçim kuralları (havuzun kendisi, sahneden bağımsız): tekrar yok, üçlü döngü zorunlu değil, aynı seed aynı sonuç.
        var open = new SegmentEventContext { PlayerRadius = 1.3f };
        var narrow = new SegmentEventContext { PlayerRadius = 1.0f };
        bool repeat = false, aba = false; var seen = new HashSet<SegmentEventSO>(); int strict = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            SegmentEventSO previous = null, before = null;
            for (int segment = 1; segment <= 9; segment++)
            {
                var pick = p.bossPool.Pick(open, previous, new System.Random(SegmentEventDirector.Mix(seed, segment, 0x424F5353)), out bool repeated);
                if (pick == previous || repeated) repeat = true;
                if (segment >= 3 && pick == before) aba = true;
                if (segment >= 3 && pick != before) strict++;
                seen.Add(pick); before = previous; previous = pick;
            }
        }
        Require(!repeat && seen.Count == 3 && aba && strict > 0, "Pool pick: never the same boss twice in a row; all three appear; A-B-A happens (no forced three-boss cycle)");
        SegmentEventSO lastNarrow = null; bool fogEarly = false, repeatNarrow = false;
        for (int segment = 1; segment <= 9; segment++)
        {
            var pick = p.bossPool.Pick(narrow, lastNarrow, new System.Random(SegmentEventDirector.Mix(Seed, segment, 0x424F5353)), out bool repeated);
            fogEarly |= pick == Fog; repeatNarrow |= repeated || pick == lastNarrow; lastNarrow = pick;
        }
        Require(!fogEarly && !repeatNarrow, "Radius 1,00 (below the Sis threshold 1,2): only Don and Sert Kabuk, alternating, no fallback");
        var a1 = p.bossPool.Pick(open, Don, new System.Random(77), out _); var a2 = p.bossPool.Pick(open, Don, new System.Random(77), out _);
        Require(a1 == a2 && SegmentEventDirector.Mix(5, 3, 9) == SegmentEventDirector.Mix(5, 3, 9) && SegmentEventDirector.Mix(5, 3, 9) != SegmentEventDirector.Mix(5, 4, 9) && SegmentEventDirector.Mix(0, 0, 0) != 0,
            "Same seed and candidates → same pick; seed mixing is stable and never 0");
        var only = ScriptableObject.CreateInstance<BossPoolSO>(); only.entries.Add(new BossPoolSO.Entry { boss = Fog, weight = 1f });
        Require(only.Pick(narrow, null, new System.Random(1), out _) == null, "No eligible boss → pool returns none (director uses the visible no-rule fallback)");
        only.entries[0] = new BossPoolSO.Entry { boss = Don, weight = 1f };
        Require(only.Pick(narrow, Don, new System.Random(1), out bool rep) == Don && rep, "Only the previous boss eligible → it repeats and the repeat is flagged");

        // Boss ve bölge rastgeleliği UnityEngine.Random akışına dokunmaz.
        UnityEngine.Random.InitState(999);
        string streamBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        p.bossPool.Pick(open, null, new System.Random(3), out _);
        var context = new SegmentEventContext { GridWidth = 11, GridHeight = 11 };
        for (int x = 4; x <= 6; x++) for (int z = 4; z <= 6; z++) context.OpenCells.Add(new GridPosition(x, z));
        new EdgeBandZone().TryChoose(context, 0.4f, new System.Random(5));
        Require(JsonUtility.ToJson(UnityEngine.Random.state) == streamBefore, "Boss pick and zone choice do not consume UnityEngine.Random (harvest / crit / card stream)");
        return .3;
    }

    // ---------------- 1) önizleme ----------------
    static SegmentEventSO previewBoss; static List<GridPosition> previewZone; static string previewName;

    static double Preview()
    {
        DisablePlayer();
        Require(Events.Events.Count == 1 && Events.Upcoming != null && Events.Active == null && Events.LastPickNote == null, "Run setup: exactly one boss chosen (segment 1), none active, no fallback");
        var e = Events.Upcoming;
        Require(e.BossRoundOnly && e.Segment == 1 && e.AnnounceRound == 1 && e.StartRound == 5 && e.EndRound == 5 && e.IsPrepared && !e.IsActive, "Boss 1: announced from round 1, active only on round 5");
        Require(e.Data == Don || e.Data == Shell, "Start radius 1,00: Sis is not eligible; boss is " + e.Data.displayName);
        previewBoss = e.Data; previewZone = OpenZone(e); previewName = e.Data.displayName;
        Require(previewZone.Count == 3, "Zone covers 3 of the 9 open cells (never the whole field)");
        string notice = Strip(SegmentEventText.Notice(Events, 0));
        Require(notice.StartsWith($"Yaklaşan boss: {previewName} · Round 5/") && notice.Contains(e.RuleSummary) && notice.Contains("boss hasadı hedefi 15") && notice.Contains("4 round hazırlık"),
            "Preparation notice (name, rule, target, rounds left): " + notice);
        var markers = Object.FindAnyObjectByType<FrostZoneMarkers>(); markers.SendMessage("LateUpdate");
        Require(!markers.ShowsActive && markers.ShownCells.OrderBy(c => c.x * 100 + c.z).SequenceEqual(previewZone), "World markers show the previewed zone (not active yet)");
        var map = Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
        Require(map.GetComponentsInChildren<TileCellUI>(false).Count(t => t.InEventZone) == 3, "Round map marks the same 3 cells");
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Place(PlanterAsset("1x1"), x, z);
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Call(summary, "Fill", new object[] { null });
        Screenshot("Boss_Preview");
        return .3;
    }

    static float Interval(PlantSpawner s) => (float)Call(s, "GetEffectiveSpawnInterval");

    static void RequireNoBossEffect(int round)
    {
        if (Events.Active != null) throw new Exception($"round {round}: boss active during preparation");
        foreach (var s in Spawners())
        {
            var pos = s.GridObject.GetGridPosition();
            if (SegmentEventDirector.SpawnIntervalMultiplier(pos) != 1f || SegmentEventDirector.SpawnHealthMultiplier(pos) != 1f) throw new Exception($"round {round}: boss multiplier in preparation");
        }
        if (!Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1f)) throw new Exception($"round {round}: radius changed");
        if (RM.IsBossRound(round) || RM.BossProgress != 0) throw new Exception($"round {round}: boss counter running");
    }

    // Rounds 1–4: etkisiz; segment kotası 1. round'da fazlasıyla dolar. Sonra round 5 başlar.
    static void PlayToBossRound(bool checkHud)
    {
        for (int r = 1; r <= 4; r++)
        {
            RM.StartNextRound();
            if (RM.CurrentRound != r) throw new Exception("round " + r + " did not start");
            RequireNoBossEffect(r);
            if (r == 1) AddScore(500);
            if (r == 2 && checkHud)
            {
                var hud = HudTexts();
                Require(hud["Label"] == "SEGMENT KOTASI TAMAM" && hud["Value"] == "500 / 10" && hud["Boss"] == "Boss hasadı hedefi (Round 5): 15" &&
                        hud["Event"].StartsWith($"Yaklaşan boss: {previewName} · Round 5"), $"Preparation HUD: {hud["Label"]} {hud["Value"]} | {hud["Boss"]} | {hud["Event"]}");
            }
            EndRound(); Flush();
            if (State != GameStates.RoundEnd || RM.RunFailed) throw new Exception($"round {r} end: {State}");
        }
        string last = Strip(SegmentEventText.Notice(Events, 4));
        if (!last.StartsWith($"SON HAZIRLIK · boss {previewName} sonraki round")) throw new Exception("round 4 notice: " + last);
        RM.StartNextRound();
    }

    // ---------------- 2) boss round'u: kota dolu, boss hedefi eksik → kayıp ----------------
    static double FailRun()
    {
        PlayToBossRound(true);
        Require(true, "Rounds 1–4: no boss effect (multipliers 1, radius unchanged, boss counter off); segment quota already full (500 / 10)");
        var active = Events.Active;
        Require(RM.CurrentRound == 5 && active != null && active.Data == previewBoss && active.IsActive && OpenZone(active).SequenceEqual(previewZone), "Round 5: the previewed boss is active on the previewed zone");
        Require(RM.IsBossRound(5) && RM.BossTarget == 15 && RM.BossProgress == 0 && RM.QuotaProgress >= 500, "Boss counter starts at 0 although the segment quota is already met (500 / 10)");
        Require(SegmentEventText.Intro(Events, RM) == $"BOSS: {previewName} · HASAT HEDEFİ 15", "Boss start intro: " + SegmentEventText.Intro(Events, RM));
        var inZone = Spawners().Where(s => active.Covers(s.GridObject.GetGridPosition())).ToList();
        var outZone = Spawners().Where(s => !active.Covers(s.GridObject.GetGridPosition())).ToList();
        if (previewBoss == Don)
            Require(inZone.Count == 3 && inZone.All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1.5f) && outZone.All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f),
                "Don Cephesi active: band production ×1,5 slower, the other 6 points unchanged");
        else
            Require(inZone.Count == 3 && inZone.All(s => SegmentEventDirector.SpawnHealthMultiplier(s.GridObject.GetGridPosition()) == 1.5f) && outZone.All(s => SegmentEventDirector.SpawnHealthMultiplier(s.GridObject.GetGridPosition()) == 1f),
                "Sert Kabuk active: band spawn health ×1,5, the other 6 points unchanged");
        var markers = Object.FindAnyObjectByType<FrostZoneMarkers>(); markers.SendMessage("LateUpdate");
        Require(markers.ShowsActive, "World markers switch to active");
        AddScore(9);
        var hud = HudTexts();
        Require(hud["Label"] == "SEGMENT KOTASI TAMAM" && hud["Boss"] == "BOSS HASADI: 9 / 15" && hud["Event"].StartsWith(previewName + ": "), $"Boss HUD separates the two conditions: {hud["Label"]} {hud["Value"]} | {hud["Boss"]} | {hud["Event"]}");
        Screenshot("Boss_HUD_Active");
        AddScore(5); // 14 / 15
        long remaining = (long)RM.RemainingTime;
        Require(State == GameStates.Round && RM.IsRoundActive && remaining > 0, "Round keeps running (target reached or not, the timer decides)");
        EndRound();
        Require(RM.EndedByBoss && !RM.EndedByQuota && RM.Outcome == RunOutcome.BossFailed && State == GameStates.RunComplete && RM.LastBossScore == 14 && RM.LastBossTarget == 15,
            "Boss harvest 14 / 15 with the quota met → run lost (boss condition is separate)");
        Require(!Boss.IsPending && Boss.Taken.Count == 0 && Boss.Offer.Count == 0, "Failed boss gives no reward");
        return 1.5;
    }

    static double FailScreen()
    {
        string text = RunEndText();
        Require(text.StartsWith("BOSS HASADI TUTMADI") && text.Contains("Round 5 · boss hasadı 14 / 15 (eksik) · segment kotası 514 / 10 (tamam)"), "Failure screen names the missing condition: " + text);
        Require(Events.Active == null && Events.Events.Count == 0 && Spawners().All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f &&
                SegmentEventDirector.SpawnHealthMultiplier(s.GridObject.GetGridPosition()) == 1f), "Run end clears the boss");
        Screenshot("Boss_Fail");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    // ---------------- 4) aynı seed: aynı boss; hedefe eşit skor geçer; kartlar → ödül ----------------
    static double PassRun()
    {
        DisablePlayer();
        var e = Events.Upcoming;
        Require(State == GameStates.RunSetup && Events.RunSeed == Seed && e != null && e.Data == previewBoss && OpenZone(e).SequenceEqual(previewZone), "Restart with the same seed → same boss and same zone");
        PlayToBossRound(false);
        Require(Events.Active != null && Events.Active.Data == previewBoss, "Round 5 boss active again");
        AddScore(15);
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        EndRound();
        Require(!RM.RunFailed && RM.LastBossScore == 15 && RM.LastBossTarget == 15, "Boss harvest 15 / 15 passes (equality is success)");
        Require(Boss.IsPending && State == GameStates.CardSelection, "Pending level card comes first; the reward waits");
        Require(Boss.Offer.Count == 0 && !F<bool>(Boss, "offerPrepared"), "Offer is not frozen while level cards are pending");
        Flush();
        Require(State == GameStates.RoundChoice && Boss.IsPending, "After the cards: boss reward choice");
        return .3;
    }

    static double RewardChoice()
    {
        var offer = Boss.Offer.ToList();
        Require(offer.Count == 3 && offer.Distinct().Count() == 3 && offer.All(Boss.IsEligible) && !offer.Contains(Kivilcim), "Three different eligible rewards; Kıvılcım not offered without a behavior planter: " + string.Join(", ", offer.Select(r => r.displayName)));
        UnityEngine.Random.InitState(31337); string before = JsonUtility.ToJson(UnityEngine.Random.state);
        Call(Boss, "BuildOffer", RM.Profile.bossRewards, 1);
        Require(Boss.Offer.SequenceEqual(offer) && JsonUtility.ToJson(UnityEngine.Random.state) == before, "Offer is reproducible from the run seed and does not consume UnityEngine.Random");
        var panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        var specPanel = Object.FindFirstObjectByType<SpecializationPanelUI>(FindObjectsInactive.Include);
        Require(panel != null && panel.gameObject.activeInHierarchy && !specPanel.gameObject.activeSelf, "Reward panel open (specialization panel closed: none in this profile)");
        var cards = ((System.Collections.IEnumerable)F<object>(panel, "cards")).Cast<object>().ToList();
        string Text(object card, string field) => Strip(((TextMeshProUGUI)card.GetType().GetField(field).GetValue(card)).text);
        Require(cards.Count == 3 && Enumerable.Range(0, 3).All(k => Text(cards[k], "title") == offer[k].displayName && Text(cards[k], "effect") == BossRewardText.Effect(offer[k]) &&
                Text(cards[k], "stack") == $"Şu an 0/3: yok/Seçince 1/3: toplam {BossRewardText.Value(offer[k], 1)}"), "Cards show effect, current stack and the result of choosing");
        Require(Strip(BossRewardPanelUI.StackText(Keskin, 2)) == "Şu an 2/3: toplam ×1,32/Seçince 3/3: toplam ×1,52" &&
                Strip(BossRewardPanelUI.StackText(Nadir, 1)) == "Şu an 1/3: toplam +8 puan/Seçince 2/3: toplam +16 puan" &&
                Strip(BossRewardPanelUI.StackText(Hizli, 1)) == "Şu an 1/3: toplam ×0,90 (saldırı sıklığı ×1,11)/Seçince 2/3: toplam ×0,81 (saldırı sıklığı ×1,23)",
            "Card stack line: multipliers multiply, points add up, shown as the total after choosing");
        // Kart metni butonun üstüne taşmaz: her metin kutusunun alt kenarı "AL" butonunun üst kenarından yukarıda.
        Canvas.ForceUpdateCanvases();
        Require(cards.All(card =>
        {
            var stack = (TextMeshProUGUI)card.GetType().GetField("stack").GetValue(card);
            var button = (UnityEngine.UI.Button)card.GetType().GetField("button").GetValue(card);
            stack.ForceMeshUpdate();
            var stackRect = stack.rectTransform; var buttonRect = (RectTransform)button.transform;
            Bounds drawn = stack.textBounds; // çizilen metin (otomatik küçülme sonrası), kutunun yerel uzayında
            float textBottom = stackRect.anchoredPosition.y + drawn.min.y;
            float buttonTop = buttonRect.anchoredPosition.y + buttonRect.sizeDelta.y * .5f;
            return stack.textInfo.lineCount == 2 && textBottom >= buttonTop && drawn.size.x <= stackRect.sizeDelta.x + .5f && !stack.isTextOverflowing;
        }), "Card stack text fits its box and stays above the button (no overlap)");
        foreach (var card in cards) Note($"kart: {Text(card, "title")} | {Text(card, "effect")} | {Text(card, "note")} | {Text(card, "stack")}");
        Screenshot("BossReward_Offer");
        RM.StartNextRound();
        Require(RM.CurrentRound == 5 && State == GameStates.RoundChoice, "Next round cannot start before the reward is chosen");
        var notOffered = RM.Profile.bossRewards.rewards.First(r => !offer.Contains(r));
        Require(!Boss.Choose(notOffered) && Boss.IsPending, "A reward that was not offered cannot be taken");
        int mods = Stats.GlobalModifiers.Count;
        var chosen = offer[0];
        var button = (Button)cards[0].GetType().GetField("button").GetValue(cards[0]);
        button.onClick.Invoke(); button.onClick.Invoke(); // çift tıklama
        Require(!Boss.IsPending && Boss.Stacks(chosen) == 1 && Boss.Taken.Count == 1 && State == GameStates.RoundEnd, "One click = one reward; flow continues to the round summary");
        Require(!Boss.Choose(chosen) && Boss.Stacks(chosen) == 1 && Stats.GlobalModifiers.Count == mods + chosen.modifiersPerStack.Count &&
                Near(BossRewardManager.DirectDamageMultiplier, chosen.directDamageMultiplier), "Second request rejected: effect applied exactly once");
        panel.gameObject.SetActive(true); // panel yeniden açılsa bile
        foreach (var card in cards) ((Button)card.GetType().GetField("button").GetValue(card)).onClick.Invoke();
        panel.gameObject.SetActive(false);
        Require(Boss.Taken.Count == 1 && Boss.Stacks(chosen) == 1, "Re-opening the panel does not duplicate the reward");
        string notice = Strip(SegmentEventText.Notice(Events, 5));
        var next = Events.Upcoming;
        Require(next != null && next.Segment == 2 && next.StartRound == 10 && next.Data != previewBoss && Events.LastEnded != null && Events.LastEnded.Data == previewBoss,
            "Next boss announced right after the boss round and differs from the previous one: " + next.Data.displayName);
        Require(notice.StartsWith($"{previewName} GEÇTİ · boss hasadı 15 / 15/Sıradaki boss: {next.Data.displayName} (Round 10)") && notice.Contains("boss hasadı hedefi 40"), "Summary after the boss: " + notice);
        RM.StartNextRound();
        Require(RM.CurrentRound == 6 && State == GameStates.Round && Events.Active == null, "Round 6 starts after the choice; no boss active");
        var hud = HudTexts();
        Require(hud.ContainsKey("Rewards") && hud["Rewards"].StartsWith("Boss ödülleri/· " + chosen.displayName + " 1/3 · "), "HUD lists the taken reward: " + hud["Rewards"]);
        Screenshot("Boss_HUD_Rewards");
        return .2;
    }

    // ---------------- 6) altı ödülün kapsamı, birikme, uygunluk ----------------
    static double RewardScope()
    {
        Boss.ClearAll();
        Require(Stats.GlobalModifiers.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f && Boss.Taken.Count == 0, "ClearAll removes the taken reward's effect");
        EndRound(); Flush();
        ClearField();
        StartRound(49); // geç round: bitki canı hasardan büyük (ölçülebilir), boss round'u değil
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 40f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        var a = Place(PlanterAsset("1x1"), -1, -1, Tile("Electric", "Legendary"), 1f);
        var b = Place(PlanterAsset("1x1"), 0, 0);
        Respawn();

        // Keskin Bıçak: yalnız doğrudan vuruş; davranış ve HarvestDamage stat'ı değişmez.
        Require(Boss.IsEligible(Keskin) && Grant(Keskin) && Near(BossRewardManager.DirectDamageMultiplier, 1.15f) && Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), 40f) && Boss.OwnedModifiers.Count == 0,
            "Keskin Bıçak ×1: direct multiplier 1,15; HarvestDamage stat still 40 (no stat modifier)");
        var plantA = PlantAt(-1, -1); var plantB = PlantAt(0, 0);
        SetF(plantA, "currentHealth", 1);
        Attack(Pos(-1, -1));
        int electric = plantB.MaxHealth - plantB.CurrentHealth;
        Require(plantA.IsDead && electric == 40 && a.GetBehaviorDamage(40, DamageType.Electric) == 40, $"Electric from A hits B for {electric}: behavior damage not raised by Keskin Bıçak");
        UnityEngine.Random.InitState(555); float v = UnityEngine.Random.Range(0.85f, 1.15f); UnityEngine.Random.InitState(555);
        int hp = plantB.CurrentHealth; Attack(Pos(0, 0));
        Require(hp - plantB.CurrentHealth == Mathf.Max(1, Mathf.RoundToInt(40f * v * 1f * 1f * 1.15f)), $"Direct hit = 40 × {v:0.000} × 1,15 = {hp - plantB.CurrentHealth}");
        Require(Grant(Keskin) && Grant(Keskin) && Boss.Stacks(Keskin) == 3 && Near(BossRewardManager.DirectDamageMultiplier, 1.15f * 1.15f * 1.15f), "Keskin Bıçak ×3: multipliers multiply (1,15³ = 1,52)");
        Require(!Boss.IsEligible(Keskin) && !Grant(Keskin) && Boss.Stacks(Keskin) == 3, "Fourth copy refused: max 3");
        var usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/UstaBicici.asset");
        SetP(SpecializationManager.Instance, "Chosen", usta);
        Respawn(); plantB = PlantAt(0, 0);
        UnityEngine.Random.InitState(556); v = UnityEngine.Random.Range(0.85f, 1.15f); UnityEngine.Random.InitState(556);
        hp = plantB.CurrentHealth; Attack(Pos(0, 0));
        float direct = BossRewardManager.DirectDamageMultiplier;
        Require(hp - plantB.CurrentHealth == Mathf.Max(1, Mathf.RoundToInt(40f * v * usta.directDamageMultiplier * 1f * direct)),
            $"With Usta Biçici: 40 × {v:0.000} × 1,25 × 1,52 = {hp - plantB.CurrentHealth} (each direct multiplier once, one rounding)");
        SetP(SpecializationManager.Instance, "Chosen", null);
        Require(BossRewardText.Effect(Keskin) == "Doğrudan vuruş hasarı ×1,15" && BossRewardText.Total(Keskin, 3) == "Doğrudan vuruş hasarı ×1,52", "Keskin Bıçak text");

        // Hızlı Bilek: saldırı aralığı küçülür; metin aralık ile sıklığı ayırır; tabanda sunulmaz.
        float interval = Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player);
        Require(Grant(Hizli) && Near(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), interval * 0.9f) && Grant(Hizli) && Grant(Hizli) &&
                Near(Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), interval * 0.729f) && !Grant(Hizli), $"Hızlı Bilek: interval {interval:0.###} → ×0,9 → ×0,729 after three; fourth refused");
        Require(BossRewardText.Effect(Hizli) == "Saldırı aralığı ×0,90 (saldırı sıklığı ×1,11)" && BossRewardText.Total(Hizli, 3) == "Saldırı aralığı ×0,73 (saldırı sıklığı ×1,37)",
            "Hızlı Bilek text separates interval from frequency: " + BossRewardText.Effect(Hizli));

        // Bereketli Toprak, Nadir Tohum, Bilgi Filizi: mevcut ve sonradan konan saksı.
        float spawn = b.GetFinalStat(StatType.PlantSpawnRate), rare = b.GetFinalStat(StatType.RareSpawnChance), xp = b.GetHarvestXP();
        Require(Grant(Bereket) && Near(b.GetFinalStat(StatType.PlantSpawnRate), spawn * 0.9f) && Grant(Nadir) && Near(b.GetFinalStat(StatType.RareSpawnChance), rare + 8f) &&
                Grant(Bilgi) && Near(b.GetHarvestXP(), xp * 1.25f), $"Existing planter: spawn {spawn} → {b.GetFinalStat(StatType.PlantSpawnRate):0.##} s, rarity {rare} → {b.GetFinalStat(StatType.RareSpawnChance)}, XP ×1,25");
        var later = Place(PlanterAsset("1x1"), 1, -1);
        Require(Near(later.GetFinalStat(StatType.PlantSpawnRate), spawn * 0.9f) && Near(later.GetFinalStat(StatType.RareSpawnChance), rare + 8f) && Near(later.GetHarvestXP(), xp * 1.25f),
            "Planter placed afterwards gets the same bonuses");
        Require(Grant(Nadir) && Grant(Nadir) && Near(b.GetFinalStat(StatType.RareSpawnChance), rare + 24f) && !Grant(Nadir), "Nadir Tohum ×3: points add up (+8 → +24); fourth refused");
        Require(BossRewardText.Effect(Bereket) == "Üretim aralığı ×0,90 (üretim sıklığı ×1,11)" && BossRewardText.Effect(Nadir) == "Nadirlik bonusu +8 puan" &&
                BossRewardText.Total(Nadir, 3) == "Nadirlik bonusu +24 puan" && BossRewardText.Effect(Bilgi) == "Kazanılan XP ×1,25" && BossRewardText.Effect(Kivilcim) == "Mevcut davranış şansları ×1,25",
            "Texts: multipliers with ×, percentage points as 'puan'");

        // Kıvılcım: yalnız mevcut şansı büyütür; 0 açılmaz; %100'ü aşmaz; katkı veremeyecekse sunulmaz.
        Require(!Boss.IsEligible(Kivilcim), "Kıvılcım not offered when the only behavior chance is already 100% (nothing to add)");
        Cell(-1, -1).GetGroundCellCached().ApplyModifier(Tile("Electric", "Legendary"), new List<StatModifier> { Mod(StatType.ElectricChance, StatTarget.Planter, 0.4f, ModifierOperation.Flat) });
        var high = Place(PlanterAsset("1x1"), -1, 1, Tile("Explosive", "Legendary"), 0.9f);
        Require(Near(a.GetFinalStat(StatType.ElectricChance), 0.4f) && Near(high.GetFinalStat(StatType.ExplosionChance), 0.9f) && Boss.IsEligible(Kivilcim), "Behavior planters with 40% and 90% → Kıvılcım eligible");
        Require(Grant(Kivilcim) && Near(a.GetFinalStat(StatType.ElectricChance), 0.5f) && Near(high.GetFinalStat(StatType.ExplosionChance), 1f) &&
                b.GetFinalStat(StatType.ElectricChance) == 0f && b.GetFinalStat(StatType.ExplosionChance) == 0f && b.GetFinalStat(StatType.TornadoChance) == 0f && b.GetFinalStat(StatType.BoomerangChance) == 0f,
            "Kıvılcım: 40% → 50%, 90% → 100% (capped), planter without behavior stays at 0");
        a.RemoveSelf(); high.RemoveSelf();
        Require(!Boss.IsEligible(Kivilcim), "No behavior planter left → Kıvılcım not eligible");

        // Sınırdaki ödül sunulmaz: tabandaki saldırı aralığı.
        Boss.ClearAll();
        var floor = Mod(StatType.AttackSpeed, StatTarget.Player, 0.1f, ModifierOperation.Set);
        TestMod(floor);
        Require(!Boss.IsEligible(Hizli), "Attack interval at the 0,10 s floor → Hızlı Bilek not offered");
        Stats.RemoveGlobalModifier(floor); testMods.Remove(floor);
        Require(Boss.IsEligible(Hizli), "Above the floor → offered again");

        // Sahiplik: aynı değerde yabancı modifier temizlikte kalır.
        Grant(Hizli); Grant(Hizli); Grant(Bilgi);
        var same = Hizli.modifiersPerStack[0];
        Stats.AddGlobalModifier(same);
        int warn = warnings;
        Require(CountMod(same) == 3 && Boss.OwnedModifiers.Count == 3, "Two stacks + an identical foreign modifier");
        Boss.ClearAll();
        Require(CountMod(same) == 1 && CountMod(Bilgi.modifiersPerStack[0]) == 0 && testMods.All(m => CountMod(m) == 1) && warnings == warn && BossRewardManager.DirectDamageMultiplier == 1f,
            "Cleanup removes only the rewards' own modifiers (foreign copy and test modifiers stay; no warnings)");
        Stats.RemoveGlobalModifier(same);
        return .2;
    }

    // ---------------- 7) az uygun ödül, hiç uygun ödül, R50 ----------------
    static double FewAndNoOffers()
    {
        foreach (var r in new[] { Keskin, Hizli, Bereket }) for (int k = 0; k < 3; k++) Grant(r);
        EndRound(); Flush();
        StartRound(10);
        Require(Events.Active != null && Events.Active.Segment == 2 && RM.IsBossRound(10) && RM.BossTarget == 40, "Round 10: second boss active, target 40");
        AddScore(40);
        EndRound(); Flush();
        Require(State == GameStates.RoundChoice && Boss.Offer.Count == 2 && Boss.Offer.Distinct().Count() == 2 && Boss.Offer.Contains(Nadir) && Boss.Offer.Contains(Bilgi),
            "Only two rewards eligible (three maxed, Kıvılcım without behavior) → two different cards, no filler, no duplicates");
        return .8; // panel bir sonraki adımda okunur: buton renk geçişi ve round sonu parçacıkları bitsin
    }

    static double FewOffersPanelAndNone()
    {
        var panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        var cards = ((System.Collections.IEnumerable)F<object>(panel, "cards")).Cast<object>().ToList();
        var shown = cards.Where(c => ((RectTransform)c.GetType().GetField("root").GetValue(c)).gameObject.activeSelf).ToList();
        Require(State == GameStates.RoundChoice && panel.gameObject.activeInHierarchy && shown.Count == 2, "Panel shows exactly two cards");
        Require(shown.All(c => ((Button)c.GetType().GetField("button").GetValue(c)).interactable) &&
                Strip(((TextMeshProUGUI)shown[0].GetType().GetField("stack").GetValue(shown[0])).text).StartsWith("Şu an 0/3: yok/Seçince 1/3: toplam "),
            "Second offer in the same run: both cards can be clicked again (the panel does not stay locked after the first choice)");
        Screenshot("BossReward_TwoOptions");
        Require(Boss.Choose(Bilgi) && State == GameStates.RoundEnd, "Chose Bilgi Filizi");
        for (int k = 0; k < 3; k++) Grant(Nadir);
        Grant(Bilgi); Grant(Bilgi);
        Require(RM.Profile.bossRewards.rewards.All(r => !Boss.IsEligible(r)), "Everything maxed or not applicable");
        StartRound(15);
        AddScore(60);
        EndRound(); Flush();
        Require(State == GameStates.RoundChoice && Boss.IsPending && Boss.Offer.Count == 0, "Boss passed with no eligible reward → empty offer, still an explicit step");
        var empty = F<TextMeshProUGUI>(panel, "empty"); var cont = F<Button>(panel, "continueButton");
        Require(panel.gameObject.activeInHierarchy && empty.gameObject.activeSelf && cont.gameObject.activeSelf && Strip(empty.text).StartsWith("Uygun ödül kalmadı") &&
                cards.All(c => !((RectTransform)c.GetType().GetField("root").GetValue(c)).gameObject.activeSelf), "Panel says 'Uygun ödül kalmadı' with a continue button and no cards");
        Screenshot("BossReward_None");
        RM.StartNextRound();
        Require(RM.CurrentRound == 15 && !Boss.Choose(Keskin), "Still blocked until continue; nothing can be taken");
        cont.onClick.Invoke(); cont.onClick.Invoke();
        Require(!Boss.IsPending && State == GameStates.RoundEnd && !Boss.ContinueWithoutReward(), "Continue resolves the step once; the player is not locked");
        // R50: boss yok, ödül yok, mevcut bitiş akışı.
        StartRound(50);
        Require(!RM.IsBossRound(50) && RM.BossTarget == 0 && Events.Active == null, "Round 50: no boss, no boss target");
        AddScore(RM.QuotaTarget);
        EndRound();
        Require(RM.Outcome == RunOutcome.Victory && State == GameStates.RunComplete && !Boss.IsPending, "Round 50 ends the run through the existing victory flow; no run buff offered");
        return 1.5;
    }

    static double RunEndAndRestart()
    {
        string text = RunEndText();
        Require(text.StartsWith("BOSS PROTOTİPİ TAMAMLANDI") && text.Contains("Başlangıç: Bahçıvan · Standart") &&
                text.Contains("Boss ödülleri: Keskin Bıçak ×3, Hızlı Bilek ×3, Bereketli Toprak ×3, Bilgi Filizi ×3, Nadir Tohum ×3"), "Result screen lists the rewards: " + text);
        Screenshot("Boss_RunEnd");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static double CleanAfterRestart()
    {
        Require(State == GameStates.RunSetup && Boss.Taken.Count == 0 && Boss.OwnedModifiers.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f && Stats.GlobalModifiers.Count == 0 && !Boss.IsPending,
            "Restart: no reward, no modifier, direct multiplier 1 (nothing accumulates)");
        Require(Events.Events.Count == 1 && Events.Upcoming.Data == previewBoss && OpenZone(Events.Upcoming).SequenceEqual(previewZone), "Restart: boss sequence starts over with the same seed");
        for (int k = 0; k < 2; k++) { Grant(Keskin); Grant(Hizli); }
        RM.BeginRun(); RM.BeginRun();
        Require(Boss.Taken.Count == 0 && Stats.GlobalModifiers.Count == 0 && BossRewardManager.DirectDamageMultiplier == 1f, "New run in the same scene clears run buffs");
        Grant(Hizli);
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static RunProfileSO runtimeProfile;
    static void LoadWithPool(params SegmentEventSO[] bosses)
    {
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        runtimeProfile = Object.Instantiate(source);
        runtimeProfile.name = "Run50_BossPrototip (test havuzu)";
        var pool = ScriptableObject.CreateInstance<BossPoolSO>();
        foreach (var boss in bosses) pool.entries.Add(new BossPoolSO.Entry { boss = boss, weight = 1f });
        runtimeProfile.bossPool = pool;
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = runtimeProfile;
        SceneManager.LoadScene("GameScene");
    }

    static double MenuClean()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && BossRewardManager.Instance == null && SegmentEventDirector.Instance == null &&
                BossRewardManager.DirectDamageMultiplier == 1f && SegmentEventDirector.SpawnHealthMultiplier(new GridPosition(5, 5)) == 1f, "Main menu: no boss, no reward state survives");
        Require(MetaSave.Data.unlocked.Count == 0 && MetaSave.Data.quests.Count == 0 && MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "Run buffs were not written to the persistent save");
        LoadWithPool(Shell);
        return 3;
    }

    // ---------------- Sert Kabuk ----------------
    static PlantSO grass; static int baseR4, baseR5; static List<GridPosition> shellZone;

    static double HardShellSetup()
    {
        DisablePlayer();
        var e = Events.Upcoming;
        Require(RM.Profile == runtimeProfile && e != null && e.Data == Shell && Events.LastPickNote == null, "Test pool with only Sert Kabuk: boss 1 is Sert Kabuk");
        grass = Plant("Grass");
        baseR4 = PlantHealthCalculator.Calculate(grass, 4); baseR5 = PlantHealthCalculator.Calculate(grass, 5);
        shellZone = OpenZone(e);
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Place(Only(grass), x, z);
        for (int r = 1; r <= 3; r++) { RM.StartNextRound(); if (r == 1) AddScore(500); EndRound(); Flush(); }
        RM.StartNextRound(); // round 4: stok
        Respawn();
        var zonePlants = shellZone.Select(p => PlantOf(SpawnerAt(p))).ToList();
        Require(RM.CurrentRound == 4 && zonePlants.Count == 3 && Spawners().All(s => { var h = PlantOf(s); return h != null && h.MaxHealth == baseR4 && h.CurrentHealth == baseR4 && h.HealthMultiplier == 1f; }),
            $"Round 4 stock: 9 Grass plants at base health {baseR4}, no multiplier");
        SetF(zonePlants[0], "currentHealth", baseR4 / 2);
        EndRound(); Flush();
        Require(Spawners().All(s => PlantOf(s) != null && !PlantOf(s).IsDead), "Stock survives into the boss round (plants persist between rounds)");
        return .2;
    }

    static List<PlantSpawner> zoneSpawners, outSpawners;
    static PlantHealth wounded, born; static PlantSpawner free; static GameObject reused; static int hard4;

    static double HardShellBoss()
    {
        zoneSpawners = shellZone.Select(SpawnerAt).ToList();
        outSpawners = Spawners().Where(s => !zoneSpawners.Contains(s)).ToList();
        wounded = PlantOf(zoneSpawners[0]);
        int woundedBefore = wounded.CurrentHealth;
        RM.StartNextRound();
        hard4 = Mathf.Max(1, Mathf.RoundToInt(baseR4 * 1.5f));
        Require(RM.CurrentRound == 5 && Events.Active != null && Events.Active.Data == Shell, "Round 5: Sert Kabuk active");
        Require(zoneSpawners.All(s => PlantOf(s).MaxHealth == hard4 && PlantOf(s).HealthMultiplier == 1.5f) && outSpawners.All(s => PlantOf(s).MaxHealth == baseR4 && PlantOf(s).HealthMultiplier == 1f),
            $"Living stock in the band is hardened at activation ({baseR4} → {hard4}); plants outside unchanged");
        int expectedWounded = Mathf.Max(1, (int)Math.Round((double)woundedBefore * hard4 / baseR4));
        Require(wounded.CurrentHealth == expectedWounded && wounded.CurrentHealth < wounded.MaxHealth && PlantOf(zoneSpawners[1]).CurrentHealth == hard4,
            $"Wounded plant keeps its ratio ({woundedBefore}/{baseR4} → {wounded.CurrentHealth}/{hard4}), it is not refilled; full plants stay full");
        Require(!wounded.ApplyHealthMultiplier(1.5f) && wounded.MaxHealth == hard4 && wounded.CurrentHealth == expectedWounded, "Multiplier cannot be applied twice in one lifetime");

        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 100000f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, 0.1f, ModifierOperation.Set)); // yalnız nişan alınan hücre
        // Boss round'unda doğan bitki: bir kez, tam canla.
        var bornSpawner = zoneSpawners[1];
        Attack(bornSpawner.transform.position);
        Call(bornSpawner, "TrySpawnPlant");
        born = PlantOf(bornSpawner); int hard5 = Mathf.Max(1, Mathf.RoundToInt(baseR5 * 1.5f));
        Require(born != null && born.MaxHealth == hard5 && born.CurrentHealth == hard5 && born.HealthMultiplier == 1.5f && !born.ApplyHealthMultiplier(1.5f) && born.MaxHealth == hard5,
            $"Plant born in the band during the boss: {baseR5} × 1,5 = {hard5}, once");
        // Havuz: sert bitkinin nesnesi bölge dışında yeniden kullanılınca çarpan taşınmaz. Havuz bırakmayı kare sonunda yapar;
        // yeniden kullanım bir sonraki adımda denenir. Bölge dışındaki boş üretim noktası o zamana kadar kapalı tutulur.
        free = outSpawners[0];
        free.RemoveSpawnedPlant();
        reused = F<GameObject>(zoneSpawners[2], "spawnedPlant");
        Require(reused.GetComponent<PlantHealth>().HealthMultiplier == 1.5f, "A hardened band plant is about to be harvested and returned to the pool");
        Attack(zoneSpawners[2].transform.position);
        return .3;
    }

    static double HardShellPoolAndEnd()
    {
        free.enabled = true;
        Call(free, "TrySpawnPlant");
        var again = PlantOf(free);
        Require(again != null && again.gameObject == reused && again.MaxHealth == baseR5 && again.CurrentHealth == baseR5 && again.HealthMultiplier == 1f,
            $"The same pooled object, reused outside the band: base health {baseR5}, no carried multiplier");
        ClearTestMods();

        // Boss biter: yaşayan sert bitkiler aynı oranla normale döner.
        int woundedHard = wounded.CurrentHealth;
        AddScore(15);
        EndRound(); Flush();
        if (Boss.IsPending) Boss.Choose(Boss.Offer[0]);
        Require(!wounded.IsDead && wounded.HealthMultiplier == 1f && wounded.MaxHealth == baseR4 && wounded.CurrentHealth == Mathf.Max(1, (int)Math.Round((double)woundedHard * baseR4 / hard4)) &&
                born.HealthMultiplier == 1f && born.MaxHealth == baseR5 && born.CurrentHealth == baseR5,
            $"Boss over: band plants back to base (wounded {wounded.CurrentHealth}/{baseR4}, ratio kept); no leak into round 6");
        RM.StartNextRound();
        zoneSpawners[1].RemoveSpawnedPlant(); zoneSpawners[1].enabled = true; Call(zoneSpawners[1], "TrySpawnPlant");
        Require(RM.CurrentRound == 6 && PlantOf(zoneSpawners[1]).HealthMultiplier == 1f && PlantOf(zoneSpawners[1]).MaxHealth == PlantHealthCalculator.Calculate(grass, 6), "Round 6: a plant born in the band has base health");
        EndRound(); Flush();

        // Run boss aktifken biterse (kayıp): sızıntı yok. İkinci boss da Sert Kabuk (tek aday → tekrar, işaretli).
        StartRound(10);
        Require(Events.Active != null && Events.Active.Data == Shell && Events.Active.Segment == 2 && Events.LastPickNote == "tekrar", "Round 10: Sert Kabuk again (only candidate → repeat, flagged)");
        var hardened = Spawners().Select(PlantOf).Where(h => h != null && h.HealthMultiplier == 1.5f).ToList();
        Require(hardened.Count > 0, $"Round 10: {hardened.Count} living plants hardened at activation");
        EndRound(); // boss hasadı 0 / 40 → kayıp
        Require(RM.EndedByBoss && State == GameStates.RunComplete && Events.Active == null && hardened.All(h => h.IsDead || h.HealthMultiplier == 1f) &&
                Spawners().Select(PlantOf).Where(h => h != null).All(h => h.HealthMultiplier == 1f), "Run ends while the boss is active: every hardened plant is back to base");
        Require(PlantHealthCalculator.Calculate(grass, 4) == baseR4 && PlantHealthCalculator.Calculate(grass, 5) == baseR5 && Shell.healthMultiplier == 1.5f, "Base health data untouched (PlantHealthScaling, PlantSO)");
        LoadWithPool(Fog);
        return 3;
    }

    // ---------------- Sis ve yedekler ----------------
    static double FogSetup()
    {
        DisablePlayer();
        Require(Object.FindObjectsByType<PlantHealth>(FindObjectsSortMode.None).All(h => h.HealthMultiplier == 1f), "Restart after Sert Kabuk: no plant carries a multiplier");
        var e = Events.Upcoming;
        Require(e != null && e.Data is NoRuleBossSO && Events.LastPickNote == "yedek" && RM.BossTargetFor(1) == 15, "Pool with only Sis at radius 1,00: nothing eligible → visible fallback 'KURALSIZ BOSS', boss target still 15");
        string notice = Strip(SegmentEventText.Notice(Events, 0));
        Require(notice.StartsWith("Yaklaşan boss: KURALSIZ BOSS · Round 5") && notice.Contains("boss hasadı hedefi 15"), "Fallback is shown to the player: " + notice);
        // Yarıçapı 1,3'e çıkar (Geniş Süpürüş gibi): ikinci boss için Sis uygun olur.
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, 0.3f, ModifierOperation.AddPercent));
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Place(PlanterAsset("1x1"), x, z);
        for (int r = 1; r <= 5; r++)
        {
            RM.StartNextRound();
            if (r == 1) AddScore(500);
            if (r == 5)
            {
                Require(Events.Active != null && Events.Active.Data is NoRuleBossSO && Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.3f) &&
                        Spawners().All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f), "Fallback boss round: no rule applied, boss target counted");
                AddScore(15);
            }
            EndRound(); Flush();
        }
        if (Boss.IsPending) Boss.Choose(Boss.Offer[0]);
        Boss.ClearAll(); // yarıçap ölçümünü ödül etkilemesin
        return .3;
    }

    static int Hits(Vector3 aim)
    {
        Respawn();
        var plants = Spawners().Select(PlantOf).Where(p => p != null).ToList();
        Attack(aim);
        return plants.Count(p => p.CurrentHealth < p.MaxHealth || p.IsDead);
    }

    static double FogBoss()
    {
        var next = Events.Upcoming;
        Require(next != null && next.Data == Fog && next.StartRound == 10 && Events.LastPickNote == null && next.ZoneCells.Count == 0, "Radius 1,30 ≥ 1,2: Sis chosen for round 10 (no zone)");
        Require(Strip(SegmentEventText.Notice(Events, 5)).Contains("Sıradaki boss: SİS (Round 10) · Vuruş yarıçapı ×0,85 · boss hasadı hedefi 40"), "Sis preview text: " + Strip(SegmentEventText.Notice(Events, 5)));
        for (int r = 6; r <= 8; r++) { RM.StartNextRound(); if (!Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.3f)) throw new Exception("radius changed in preparation"); EndRound(); Flush(); }
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 1f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        Vector3 center = Pos(0, 0), edge = (Pos(0, 0) + Pos(1, 0)) / 2f;
        RM.StartNextRound(); // round 9: son hazırlık
        var before = (Hits(center), Hits(edge));
        EndRound(); Flush();
        int count = Stats.GlobalModifiers.Count;
        var snapshot = Stats.GlobalModifiers.ToList();
        RM.StartNextRound();
        float radius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
        var added = Stats.GlobalModifiers.Where(m => !snapshot.Contains(m)).ToList();
        Require(RM.CurrentRound == 10 && Events.Active != null && Events.Active.Data == Fog && Near(radius, 1.3f * 0.85f) && Stats.GlobalModifiers.Count == count + 1 &&
                added.Count == 1 && added[0].statType == StatType.AreaRadius && added[0].target == StatTarget.Player,
            $"Sis active: radius 1,30 → {radius:0.###}; exactly one player-radius modifier added (behaviors use no radius stat)");
        var during = (Hits(center), Hits(edge));
        Require(before == (5, 6) && during == (5, 6), $"Surface-contact targeting on the legacy fixed-multiplier Sis: before {before}, during {during}; this multiplier no longer crosses a cell threshold");
        AddScore(40);
        EndRound(); Flush();
        if (Boss.IsPending) Boss.Choose(Boss.Offer[0]);
        Boss.ClearAll();
        Require(Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.3f) && Stats.GlobalModifiers.Count == count, "Sis over: radius back to 1,30, modifier count restored");
        // Tek aday Sis: tekrar eder ve işaretlenir. Boss aktifken run biterse yarıçap yine geri gelir.
        StartRound(15);
        Require(Events.Active != null && Events.Active.Data == Fog && Events.LastPickNote == "tekrar" && Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.3f * 0.85f),
            "Round 15: Sis again (only candidate → repeat, flagged)");
        int warn = warnings;
        EndRound(); // 0 / 60 → kayıp
        Require(RM.EndedByBoss && State == GameStates.RunComplete && Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1.3f) && warnings == warn, "Run lost during the fog: radius restored, no stray modifier");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    // Sis aktifken ana menüye dönüş: StatManager önce toptan temizlenir; olayın kendi kaldırması uyarı üretmez.
    static double FogMenu()
    {
        DisablePlayer();
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, 0.3f, ModifierOperation.AddPercent));
        for (int r = 1; r <= 5; r++) { RM.StartNextRound(); if (r == 1) AddScore(500); if (r == 5) AddScore(15); EndRound(); Flush(); }
        if (Boss.IsPending) Boss.Choose(Boss.Offer[0]);
        StartRound(10);
        Require(Events.Active != null && Events.Active.Data == Fog && Boss.Taken.Count == 1, "New run: Sis active at round 10 with one reward taken");
        warningsBeforeMenu = warnings;
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static int warningsBeforeMenu;

    static double FinalCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && warnings == warningsBeforeMenu && BossRewardManager.Instance == null &&
                SegmentEventDirector.Instance == null && BossRewardManager.DirectDamageMultiplier == 1f, "Main menu while Sis was active and a reward was held: everything cleared, no modifier warning");
        Require(warnings == 0, "No 'modifier not found' warning in the whole test");
        return -1;
    }

    // ---------------- ekran görüntüsü ----------------
    static RenderTexture target;
    static void Screenshot(string name)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas &&
                 (c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null))))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var old = cam.targetTexture;
        cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG()); Object.DestroyImmediate(png);
        notes.Add("saved Logs/" + name + ".png");
    }
}
