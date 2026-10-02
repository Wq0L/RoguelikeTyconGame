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

// Batch (izole kopya): Bölüm 3.6 İŞLEV testi. Denge ölçümü değildir ("dengeli" demez); ölçümler BalanceRunMeasurement'tadır.
// A) Kıvılcım: saksının gerçek davranış şansı ödül öncesi / sonrası, tek çarpım, sınır, davranışsız saksıya etki yok, uyarı yok,
//    gerçek tetik sıklığı.
// B) Boss bölgesi: tarlada ve haritada dolgu yok, çevre çizgisi var (önizleme kesikli, aktif düz), tile ve saksı pikselleri değişmiyor.
// C) Boss HUD renkleri boss kimliğinden; boss değişince, bitince, temizlenince ve yeniden başlatınca eski renk kalmıyor.
// D) Run50_KirilmaV1: veri ayrımı, başlangıç kilitleri, erken davranış teklifi, level başına üç seçim, kırılma ödülü slotu,
//    Artçı Patlama, Çifte Akım, Hasat Ritmi. E) Eski profil (DengeV1) bu kurallardan etkilenmiyor.
// F) Bölüm 3.7.1: ağaçta başlangıç erişimi satın almadan ayrı gösterilir (yazı + renk), gerçek düğüm kimlikleriyle kilit
//    durumu, bağlantı çizgileri, yeniden başlatmada ödül etkisinin katlanmaması, eski profile erişim sızmaması.
// Sayılar asset'lerden okunur: ayar turunda değerler değişince test yeniden yazılmaz.
[InitializeOnLoad]
public static class KirilmaV1Verification
{
    const string Key = "KirilmaV1Verification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string MenuPath = "Tools/Run Profili/Run50 Kırılma V1 · 50 round (build kırılması adayı)";
    const int Seed = 3636;
    static readonly List<string> notes = new();
    static int step; static double nextAt; static int errors, baseWarnings, harvests;

    static KirilmaV1Verification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: Kırılma V1 seçilir ve boss seed'i sabitlenir.
        foreach (string name in new[] { "Run50_KirilmaV1", "Run50_DengeV1" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            profile.bossSeed = Seed; EditorUtility.SetDirty(profile);
        }
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Run50_KirilmaV1.asset");
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
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
            if (nextAt == 0)
            {
                nextAt = EditorApplication.timeSinceStartup + 2;
                Application.logMessageReceived += CountLogs;
                PlantHealth.AnyHarvested += CountHarvest;
            }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/KirilmaV1Verification.txt", new[] { $"RUNNING step {step}" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240) throw new Exception($"step {step} stuck");
            if (RoundManager.Instance != null && RoundManager.Instance.IsRoundActive) SetP(RoundManager.Instance, "RemainingTime", 1000f);
            double wait = Run(step++);
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void CountHarvest(PlantHealth plant) => harvests++;

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;   // editörün arama dizini (oyunla ilgisiz)
        if (type == LogType.Exception || type == LogType.Error) { errors++; notes.Add("   LOG " + type + ": " + message); }
        if (type == LogType.Warning && message.Contains("base stat bulunamadı")) { baseWarnings++; notes.Add("   WARN: " + message); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        PlantHealth.AnyHarvested -= CountHarvest;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/KirilmaV1Verification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static bool Near(float a, float b, float eps = 1e-3f) => Mathf.Abs(a - b) <= eps;
    static bool Same(Color a, Color b, float eps = 0.02f) => Distance(a, b) <= eps;
    static float Distance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Strip(string s) => s == null ? null : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    static float Hue(Color c) { Color.RGBToHSV(c, out float h, out _, out _); return h * 360f; }
    static float Sat(Color c) { Color.RGBToHSV(c, out _, out float s, out _); return s; }
    static RoundManager RM => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static SkillTreeManager Tree => SkillTreeManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static ProgressionManager Progress => ProgressionManager.Instance;
    static ResourceManager Bank => ResourceManager.Instance;
    static BehaviorEchoes Echoes => BehaviorEchoes.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static Vector3 Pos(int dx, int dz) => Cell(dx, dz).GetGroundCellCached().transform.position;
    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static RunProfileSO K1 => Profile("Run50_KirilmaV1");
    static RunProfileSO D1 => Profile("Run50_DengeV1");
    static PlanterSO PlanterAsset(string n) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {n}.asset");
    static TileModifierSO Tile(string t, string r)
    {
        var tile = AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
        if (tile == null) throw new Exception($"tile asset missing: {t}-{r}");
        return tile;
    }
    static PlantSO Plant(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    static BossRewardSO Small(string id) => K1.bossRewards.rewards.First(r => r.id == id);
    static BossRewardSO Break(string id) => K1.bossRewards.breakthroughs.First(r => r.id == id);
    static SkillNodeSO Node(string name) => Tree.AllNodes.First(n => n.name == name);
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (Stats.GlobalModifiers.Any(x => x.Equals(m))) Stats.RemoveGlobalModifier(m); testMods.Clear(); }
    // Sabit, ölçülebilir vuruş: hasar, kritik yok, yarıçap.
    static void Strike(float damage, float radius)
    {
        ClearTestMods();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, damage, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        TestMod(Mod(StatType.AreaRadius, StatTarget.Player, radius, ModifierOperation.Set));
    }
    static PlantHealth PlantAt(int dx, int dz) { var p = Cell(dx, dz).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static bool Dead(int dx, int dz) { var h = PlantAt(dx, dz); return h == null || h.IsDead; }
    static int Lost(int dx, int dz) { var h = PlantAt(dx, dz); return h == null || h.IsDead ? -1 : h.MaxHealth - h.CurrentHealth; }
    static PlayerController Player => Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    static void Attack(Vector3 position) => Call(Player, "AttackInRadius", position);
    static void AddScore(long amount) => SetF(HarvestScoreManager.Instance, "totalScore", HarvestScoreManager.Instance.TotalScore + amount);

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

    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
    static void Respawn(PlantSpawner s) { s.RemoveSpawnedPlant(); s.enabled = true; Call(s, "TrySpawnPlant"); }
    static void Respawn() { foreach (var s in Spawners()) Respawn(s); }
    static PlantSpawner SpawnerAt(int dx, int dz) => Spawners().First(x => x.GridObject == Cell(dx, dz));
    static void RespawnAt(int dx, int dz) => Respawn(SpawnerAt(dx, dz));

    static IEnumerable<GroundCell> OpenCells()
    {
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        {
            var ground = Grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
            if (ground != null && !ground.IsLocked) yield return ground;
        }
    }

    static void ClearField()
    {
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        foreach (var ground in OpenCells().ToList()) ground.ApplyModifier(null);
    }

    static int TileCount() => OpenCells().Count(g => g.CurrentModifier != null);

    static void StartRound(int round)
    {
        Player.enabled = false;
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
    }

    static CardSelectionUI Cards => Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static List<TileCardOffer> Offers => F<List<TileCardOffer>>(Cards, "currentCards");
    static bool IsBehavior(TileCardOffer o) => o != null && o.Tile != null && !o.IsUpgrade &&
        (o.Tile.modifierType == TileModifierType.Explosive || o.Tile.modifierType == TileModifierType.Electric);

    // Bekleyen kart seçimlerini ilk kartla geçer; seçim sayısını döner.
    static int ResolveCards()
    {
        int picks = 0;
        while (State == GameStates.CardSelection)
        {
            if (picks++ > 300) throw new Exception("card selection did not finish");
            Call(Cards, "OnCardSelected", Offers[0]);
        }
        return picks;
    }

    // Round'u oyunun kendi yoluyla bitirir. Bu adımın konusu olmayan kart seçimleri atılır (rastgele tile'lar test alanını bozmasın).
    static void EndRoundQuiet()
    {
        SetF(RM, "pendingCardSelections", 0);
        Call(RM, "EndRound");
        if (State == GameStates.RoundChoice && Boss.IsPending)
        {
            if (Boss.Offer.Count > 0) Boss.Choose(Boss.Offer[0]); else Boss.ContinueWithoutReward();
        }
        if (State != GameStates.RoundEnd) throw new Exception("round did not end: " + State);
    }

    static bool Grant(BossRewardSO reward)
    {
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetF(Boss, "offerPrepared", true); // Explicit fixture: this test injects an already prepared offer.
        SetP(Boss, "IsPending", true);
        bool taken = Boss.Choose(reward);
        if (!taken) { SetP(Boss, "IsPending", false); offer.Clear(); }
        return taken;
    }

    static QuotaHUD Hud => Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
    static void RefreshHud() => Call(Hud, "Refresh");

    // ---------------------------------------------------------------- görüntü
    static RenderTexture target;
    static Camera Cam => Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

    // withUi false: yalnız dünya (bütün kök canvas'lar kapalı) — piksel karşılaştırmaları bununla yapılır.
    static Texture2D Capture(string name, bool withUi)
    {
        var cam = Cam;
        if (cam == null) throw new Exception("no camera for " + name);
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        var roots = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        foreach (var canvas in roots.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay || (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
        var hidden = withUi ? new List<Canvas>() : roots.Where(c => c.enabled).ToList();
        foreach (var canvas in hidden) canvas.enabled = false;
        var old = cam.targetTexture;
        cam.targetTexture = target;
        Canvas.ForceUpdateCanvases(); cam.Render();
        var active = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        foreach (var canvas in hidden) canvas.enabled = true;
        if (name != null) { Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG()); }
        return png;
    }

    static Color Sample(Texture2D picture, Vector3 world)
    {
        var cam = Cam;
        var old = cam.targetTexture; cam.targetTexture = target;
        Vector3 screen = cam.WorldToScreenPoint(world);
        cam.targetTexture = old;
        return picture.GetPixel(Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y));
    }

    static Vector3 Ground(GridPosition cell)
    {
        var ground = Grid.GetGridObject(cell).GetGroundCellCached();
        return new Vector3(ground.transform.position.x, ground.GroundRenderer.bounds.max.y, ground.transform.position.z);
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Assets();
            case 1: return Runtime();
            case 2: return CardOffers();
            case 3: return ZonePreview();
            case 4: return LevelChoices();
            case 5: return BossRoundActive();
            case 6: return AfterBoss();
            case 7: return RewardSlot();
            case 8: return KivilcimChance();
            case 9: return KivilcimTriggers();
            case 10: return AftershockTrigger();
            case 11: return AftershockResult();
            case 12: return AftershockChainResult();
            case 13: return AftershockDropAndReuse();
            case 14: return AftershockReuseResult();
            case 15: return ElectricTrigger();
            case 16: return ElectricResult();
            case 17: return Rhythm();
            case 18: return BossPalette();
            case 19: return TreeAccess();
            case 20: return TreeAccessDisplay();
            case 21: return LoadProfile("Run50_DengeV1");
            case 22: return OldProfile();
            case 23: return OldProfileAfter();
            case 24: return OldTreeDisplay();
            case 25: return FinalCheck();
            default: return -1;
        }
    }

    // ---------------------------------------------------------------- 0) asset'ler ve veri ayrımı
    static double Assets()
    {
        var k = K1; var d = D1;
        Require(k != null && d != null && k != d && k.balance != null && k.balance != d.balance, "Run50_KirilmaV1 is a separate profile with its own balance set");
        foreach (string guid in AssetDatabase.FindAssets("t:RunProfileSO", new[] { "Assets/ScriptableObjects/RunProfiles" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
            bool starts = profile.balance != null && profile.balance.startingUnlocks != null && profile.balance.startingUnlocks.Count > 0;
            Note($"profil · {profile.name}: başlangıç kilidi {(starts ? string.Join(", ", profile.balance.startingUnlocks) : "yok")}");
            if (starts != (profile == k)) throw new Exception("starting unlocks leaked to profile " + profile.name);
        }
        Require(true, "Profile assets: only Run50_KirilmaV1 opens unlocks from the start; every older profile has none");
        Require(k.runLength == 50 && k.runLength == d.runLength && k.segmentRounds == d.segmentRounds && k.fixedRoundDuration == d.fixedRoundDuration && k.startingGold == d.startingGold &&
                k.startingIron == d.startingIron && k.startingStone == d.startingStone && !k.debugBudget,
            $"50 rounds; fixed round duration ({k.fixedRoundDuration} s) and start budget ({k.startingGold} gold) are DengeV1's");
        Require(k.segmentTargets.SequenceEqual(d.segmentTargets) && k.bossTargets.SequenceEqual(d.bossTargets) && k.bossPool == d.bossPool,
            "Quota table, boss targets and boss pool are DengeV1's (same pool asset)");
        Require(k.balance.coreStats == d.balance.coreStats && k.balance.plantHealth == d.balance.plantHealth && k.balance.progression == d.balance.progression &&
                k.balance.skillTree == d.balance.skillTree && k.balance.rarityXpMultipliers.SequenceEqual(d.balance.rarityXpMultipliers) &&
                k.balance.planterPrices.Count == d.balance.planterPrices.Count &&
                k.balance.planterBaseStats.Select(s => (s.statType, s.value)).SequenceEqual(d.balance.planterBaseStats.Select(s => (s.statType, s.value))),
            "Core stats, plant health curve, XP table, skill tree and planter data are the same DengeV1 assets (referenced, not copied): XP is not changed");
        Require(k.choicesPerLevel == 3 && d.choicesPerLevel == 1 && Profile("Run50_Referans").choicesPerLevel == 1 && Profile("Run50_BossPrototip").choicesPerLevel == 1,
            "choicesPerLevel: 3 in KirilmaV1 (profile data), 1 in DengeV1 and the older profiles");
        Require(k.balance.startingUnlocks.SequenceEqual(new[] { UnlockType.TileBehavior_Explosive, UnlockType.TileBehavior_Electric }) && d.balance.startingUnlocks.Count == 0,
            "Starting unlocks: explosion and electric cards in KirilmaV1; none in DengeV1");
        Require(k.balance.firstBehaviorOffer.SequenceEqual(new[] { TileModifierType.Explosive, TileModifierType.Electric }) && d.balance.firstBehaviorOffer.Count == 0,
            "Early behavior offer: explosion / electric candidates in KirilmaV1; none in DengeV1");
        var pool = k.bossRewards;
        Require(pool != d.bossRewards && pool.choices == d.bossRewards.choices && pool.rewards.SequenceEqual(d.bossRewards.rewards) && d.bossRewards.breakthroughs.Count == 0,
            $"Reward pool: DengeV1's {pool.rewards.Count} small rewards (same assets) kept; DengeV1's own pool has no breakthrough rewards");
        Require(pool.breakthroughs.Select(r => r.id).SequenceEqual(new[] { "artci_patlama", "cifte_akim", "hasat_ritmi" }) &&
                pool.breakthroughs.All(r => r.IsBreakthrough && r.maxStacks == 1 && r.minRound == 10 && r.weight > 0f && !pool.rewards.Contains(r) && r.modifiersPerStack.Count == 0),
            "Three breakthrough rewards only in the new pool: once per run, first offered at the R10 boss");
        var a = Break("artci_patlama"); var c = Break("cifte_akim"); var h = Break("hasat_ritmi");
        Require(a.echo == BossRewardEcho.Explosion && a.condition == BossRewardCondition.AnyPlanterHasConditionStat && a.conditionStat == StatType.ExplosionChance &&
                a.echoDelay >= 0.19f && a.echoDelay <= 0.21f && a.echoDamage >= 0.5f && a.echoDamage <= 1f && a.echoRadius >= 1.15f && a.echoRadius <= 1.4f,
            $"Artçı Patlama data: delay {a.echoDelay} s, damage ×{a.echoDamage}, radius ×{a.echoRadius} (inside the allowed tuning range)");
        Require(c.echo == BossRewardEcho.Electric && c.condition == BossRewardCondition.AnyPlanterHasConditionStat && c.conditionStat == StatType.ElectricChance &&
                c.echoDelay >= 0.14f && c.echoDelay <= 0.16f && c.echoDamage >= 0.6f && c.echoDamage <= 1f,
            $"Çifte Akım data: delay {c.echoDelay} s, damage ×{c.echoDamage} (inside the allowed tuning range)");
        Require(h.rhythmHarvests >= 5 && h.rhythmHarvests <= 8 && h.rhythmDamage >= 1.25f && h.rhythmDamage <= 1.75f && h.rhythmRadius >= 1.35f && h.rhythmRadius <= 1.6f &&
                h.condition == BossRewardCondition.Always && h.echo == BossRewardEcho.None,
            $"Hasat Ritmi data: every {h.rhythmHarvests} direct harvests, damage ×{h.rhythmDamage}, radius ×{h.rhythmRadius} (inside the allowed tuning range)");
        Require(d.bossRewards.rewards.All(r => !r.IsBreakthrough) && Profile("Run50_BossPrototip").bossRewards.rewards.All(r => !r.IsBreakthrough),
            "Existing reward assets carry the new fields at neutral defaults");
        var items = typeof(RunProfileMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).SelectMany(m => m.GetCustomAttributes<MenuItem>()).ToList();
        Require(items.Count(x => x.menuItem == MenuPath) == 2, "Menu item and validator: " + MenuPath);
        // Patlama geometrisi: yarıçap 1,2 hücre mevcut kuralın (dört yönlü komşu) hedef kümesini birebir verir.
        foreach (var size in new[] { (1, 1), (1, 3), (2, 2), (2, 3) })
        {
            var footprint = new List<GridPosition>();
            for (int x = 0; x < size.Item1; x++) for (int z = 0; z < size.Item2; z++) footprint.Add(new GridPosition(5 + x, 5 + z));
            var rule = new HashSet<(int, int)>();
            foreach (var p in footprint)
                foreach (var dir in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                    if (!footprint.Any(q => q.x == p.x + dir.Item1 && q.z == p.z + dir.Item2)) rule.Add((p.x + dir.Item1, p.z + dir.Item2));
            var cells = new List<GridPosition>();
            HarvestBehaviorGeometry.ExplosionCells(footprint, HarvestBehaviorGeometry.ExplosionRadiusCells, cells);
            if (!rule.SetEquals(cells.Select(q => (q.x, q.z))) || cells.Count != rule.Count) throw new Exception($"explosion cells differ for {size}");
            var wide = new List<GridPosition>();
            HarvestBehaviorGeometry.ExplosionCells(footprint, HarvestBehaviorGeometry.ExplosionRadiusCells * a.echoRadius, wide);
            if (wide.Count <= cells.Count || !cells.All(q => wide.Any(w => w.x == q.x && w.z == q.z))) throw new Exception($"aftershock radius adds no cell for {size}");
            Note($"patlama hücreleri {size.Item1}×{size.Item2} saksı: normal {cells.Count} → artçı {wide.Count}");
        }
        Require(true, "Explosion radius 1,2 cells reproduces the existing 4-neighbour rule for 1×1, 1×3, 2×2 and 2×3 planters; the aftershock radius adds cells around it");
        return .1;
    }

    // ---------------------------------------------------------------- 1) sahne: kilitler ve ağaç
    static double Runtime()
    {
        Player.enabled = false;
        Require(State == GameStates.RunSetup && RM.Profile == K1 && RunBalanceSO.Active == K1.balance && RM.ChoicesPerLevel == 3 && Events.BossMode && Events.RunSeed == Seed,
            "GameScene runs Run50_KirilmaV1 (3 choices per level, boss mode, fixed seed)");
        Require(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "Batch save is in memory only (the player's save file is not read or written)");
        var unlocks = UnlockManager.Instance;
        Require(unlocks.IsUnlocked(UnlockType.TileBehavior_Explosive) && unlocks.IsUnlocked(UnlockType.TileBehavior_Electric) &&
                !unlocks.IsUnlocked(UnlockType.TileBehavior_Tornado) && !unlocks.IsUnlocked(UnlockType.TileBehavior_Boomerang) && !unlocks.IsUnlocked(UnlockType.TileBehavior_Duplicate) &&
                !unlocks.IsUnlocked(UnlockType.Planter_2x2),
            "Run start: explosion and electric cards unlocked; tornado, boomerang, duplicate and bigger planters still locked");
        Require(Tile("Explosive", "Common").IsAvailableInCardPool && Tile("Electric", "Legendary").IsAvailableInCardPool && !Tile("Tornado", "Common").IsAvailableInCardPool,
            "Card pool: explosion and electric tiles available from the start");
        // Gerçek düğüm kimlikleriyle: kilit açan her düğüm ve run başındaki durumu (görüntüden değil veriden).
        foreach (var n in Tree.AllNodes.Where(n => n.unlockType != UnlockType.None).OrderBy(n => n.gridPosition.x).ThenBy(n => n.gridPosition.y))
            Note($"kilit düğümü · {n.name} · guid {AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(n))} · {n.unlockType} · konum ({n.gridPosition.x},{n.gridPosition.y}) · run başında kilit " +
                 $"{(unlocks.IsUnlocked(n.unlockType) ? "AÇIK" : "kapalı")} · başlangıç erişimi {(SkillTreeManager.HasStartingAccess(n) ? "evet" : "hayır")} · satın alma seviyesi {Tree.GetCurrentLevel(n)}/{n.tiers.Count}");
        var openTypes = Enum.GetValues(typeof(UnlockType)).Cast<UnlockType>().Where(unlocks.IsUnlocked).ToList();
        Require(openTypes.Count == 2 && openTypes.Contains(UnlockType.TileBehavior_Explosive) && openTypes.Contains(UnlockType.TileBehavior_Electric) && K1.balance.startingUnlocks.Count == 2,
            "Run start, every unlock type checked: only explosion and electric are open (tornado, boomerang, duplicate and the three planters are locked)");
        var startNodes = Tree.AllNodes.Where(SkillTreeManager.HasStartingAccess).Select(n => n.name).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Require(startNodes.SequenceEqual(new[] { "Patlayıcı Kartlar", "Çapraz Elektrik Kartları" }.OrderBy(x => x, StringComparer.Ordinal)) && Tree.AllNodes.All(n => Tree.GetCurrentLevel(n) == 0),
            "Starting access belongs to exactly two nodes (Patlayıcı Kartlar, Çapraz Elektrik Kartları); no node has a purchase level at run start");
        var roster = F<List<TileModifierSO>>(Cards, "allModifiers").Where(t => t != null).ToList();
        var leaked = roster.Where(t => t.IsAvailableInCardPool && t.requiredUnlock != UnlockType.None && t.requiredUnlock != UnlockType.TileBehavior_Explosive && t.requiredUnlock != UnlockType.TileBehavior_Electric).ToList();
        Require(roster.Count(t => t.requiredUnlock == UnlockType.TileBehavior_Tornado) >= 4 && roster.Count(t => t.requiredUnlock == UnlockType.TileBehavior_Boomerang) >= 4 && leaked.Count == 0,
            "Scene card roster: no tornado, boomerang or duplicate card is in the pool at run start" + (leaked.Count > 0 ? " — leaked: " + string.Join(", ", leaked.Select(t => t.name)) : ""));
        Require(TileCount() == 0 && Spawners().Count == 0 && Bank.GetResourceAmount(ResourceType.Gold) == K1.startingGold,
            "No free tile and no free planter: empty field, normal start budget");
        var explosive = Node("Patlayıcı Kartlar"); var electric = Node("Çapraz Elektrik Kartları"); var tornado = Node("Tornado Kartları"); var first = Node("1×3 Saksı");
        Bank.AddResource(ResourceType.Iron, 500); Bank.AddResource(ResourceType.Gold, 500);
        int iron = Bank.GetResourceAmount(ResourceType.Iron);
        Require(SkillTreeManager.IsGrantedByProfile(explosive) && SkillTreeManager.IsGrantedByProfile(electric) && Tree.IsDisabledByProfile(explosive) &&
                !Tree.CanUpgrade(explosive) && !Tree.CanUpgrade(electric) && !Tree.TryUpgrade(explosive) && !Tree.TryUpgrade(electric) && Tree.GetCurrentLevel(explosive) == 0 &&
                Bank.GetResourceAmount(ResourceType.Iron) == iron,
            "The two paid unlock nodes are not sold in this profile (a rich player cannot buy them; nothing is charged)");
        Require(!SkillTreeManager.IsGrantedByProfile(tornado) && !SkillTreeManager.IsGrantedByProfile(first) && !Tree.MeetsPrerequisites(tornado) && !Tree.CanUpgrade(tornado) && !Tree.IsNodeVisible(tornado),
            "Tornado Kartları keeps its place in the chain: locked until the nodes before the removed step are bought");
        // "1×3 Saksı alındı" durumu (grid ve kaynak değişmesin diye satın alma yerine ağacın kendi seviye tablosuyla kurulur).
        var levels = F<Dictionary<SkillNodeSO, int>>(Tree, "nodeLevels");
        levels[first] = 1;
        Require(Tree.MeetsPrerequisites(tornado) && Tree.CanUpgrade(tornado) && Tree.IsNodeVisible(explosive) && !Tree.CanUpgrade(explosive),
            "With 1×3 Saksı owned the removed step counts as met: Tornado Kartları can be bought; only the paid unlock step disappeared");
        // Arayüz: verilen düğüm "başlangıçtan açık" görünür (satın alınmış yeşili değil), tıklanamaz.
        var ui = Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
        var opened = new List<GameObject>();
        for (Transform t = ui.transform; t != null; t = t.parent) if (!t.gameObject.activeSelf) opened.Add(t.gameObject);
        opened.Reverse(); foreach (var go in opened) go.SetActive(true);
        var slot = F<List<SkillNodeUI>>(ui, "nodeUIs").First(n => n != null && n.Node == explosive);
        slot.Refresh();
        Require(slot.gameObject.activeSelf && !F<Button>(slot, "button").interactable && Same(Face(slot), SkillNodeUI.StartingAccessColor) && !Same(Face(slot), Bought, .08f) &&
                slot.ShowsStartingAccess && Tree.GetCurrentLevel(explosive) == 0,
            "Tree interface: the granted node is drawn as 'Başlangıçtan açık' (own color and label, not the green of a bought node), keeps purchase level 0 and cannot be clicked");
        levels.Remove(first);
        slot.Refresh();
        foreach (var go in opened) go.SetActive(false);
        Bank.SpendResource(ResourceType.Iron, 500); Bank.SpendResource(ResourceType.Gold, 500);
        Require(Bank.GetResourceAmount(ResourceType.Gold) == K1.startingGold && Tree.GetCurrentLevel(first) == 0, "Test fixture removed (budget and tree as at run start)");
        return .1;
    }

    // ---------------------------------------------------------------- 2) erken davranış teklifi (dağılım)
    static double CardOffers()
    {
        var cards = Cards;
        UnityEngine.Random.InitState(4242);
        // Saksı yok: slot davranış kartıyla kilitlenmez.
        int forced = 0;
        for (int n = 0; n < 60; n++) { cards.RefreshCards(); if (cards.GuaranteedBehaviorOffer != null) forced++; }
        Require(forced == 0 && Spawners().Count == 0, "No planter on the field: no slot is reserved for a behavior card (0 of 60 offers)");
        Place(PlanterAsset("1x1"), 0, 0);
        var slots = new int[3]; int all = 0, total = 200; var types = new HashSet<TileModifierType>(); var rarities = new HashSet<TileRarity>();
        for (int n = 0; n < total; n++)
        {
            cards.RefreshCards();
            var offers = Offers;
            if (offers.Count != 3) throw new Exception("offer size " + offers.Count);
            var guaranteed = cards.GuaranteedBehaviorOffer;
            if (guaranteed == null || !IsBehavior(guaranteed) || !offers.Contains(guaranteed)) throw new Exception("offer " + n + " has no guaranteed behavior card");
            slots[offers.IndexOf(guaranteed)]++; types.Add(guaranteed.Tile.modifierType); rarities.Add(guaranteed.Rarity);
            if (offers.All(IsBehavior)) all++;
        }
        Require(slots.All(s => s > 30) && types.Count == 2 && rarities.Count >= 3,
            $"With a planter: every one of {total} offers has one slot from the explosion / electric candidates; the slot ({slots[0]} / {slots[1]} / {slots[2]}), the type and the rarity are random");
        Require(all < total / 2, $"The other two slots keep the normal distribution: all three cards are behavior cards in only {all} of {total} offers");
        Require(Progress.FirstBehaviorCardRound == 0 && TileCount() == 0, "Offers alone place nothing and take nothing");
        ClearField();
        return .8;
    }

    // ---------------------------------------------------------------- 3) boss bölgesi önizlemesi (tarla + harita) ve HUD
    static List<GridPosition> zone; static SegmentEventSO firstBoss; static Color accent;
    static GridPosition tileCell; static (GridPosition cell, Vector2Int side) boundary;

    static List<GridPosition> OpenZone(SegmentEventRuntime e) => e.ZoneCells.Where(p => { var g = Grid.GetGridObject(p)?.GetGroundCellCached(); return g != null && !g.IsLocked; })
        .OrderBy(p => p.x * 100 + p.z).ToList();

    static int Perimeter(List<GridPosition> cells) => cells.Sum(c => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Count(d => !cells.Any(o => o.x == c.x + d.Item1 && o.z == c.z + d.Item2)));

    // Bölgenin, açık (bölge dışı) bir hücreye bakan kenarı: çizginin en iyi görüldüğü yer.
    static (GridPosition cell, Vector2Int side) InnerBoundary(GridPosition cell)
    {
        foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
        {
            var other = Grid.GetGridObject(new GridPosition(cell.x + d.x, cell.z + d.y))?.GetGroundCellCached();
            if (other != null && !other.IsLocked && !zone.Any(o => o.x == cell.x + d.x && o.z == cell.z + d.y)) return (cell, d);
        }
        throw new Exception("zone cell has no inner boundary");
    }

    static List<Color> LineSamples(Texture2D picture, GridPosition cell, Vector2Int side)
    {
        float half = GridManager.Instance.GetCellSize() * .5f, depth = half - FrostZoneMarkers.LineInset - FrostZoneMarkers.LineWidth * .5f;
        Vector3 centre = Ground(cell); var list = new List<Color>();
        for (int i = 0; i < 60; i++)
        {
            float along = Mathf.Lerp(-half * .9f, half * .9f, i / 59f);
            list.Add(Sample(picture, centre + new Vector3(side.x * depth + (side.x == 0 ? along : 0f), 0f, side.y * depth + (side.y == 0 ? along : 0f))));
        }
        return list;
    }

    static FrostZoneMarkers Markers => Object.FindAnyObjectByType<FrostZoneMarkers>();
    static void ShowOutline(bool on) { Markers.OutlineRenderer.enabled = on; Markers.OccludedRenderer.enabled = on; }

    static Vector3 PlantPoint(GridPosition cell) => Grid.GetGridObject(cell).GetPlantObject().transform.position + Vector3.up * .3f;

    static double ZonePreview()
    {
        Events.SendMessage("LateUpdate");
        var e = Events.Upcoming;
        Require(State == GameStates.RunSetup && Events.Events.Count == 1 && e != null && e.IsPrepared && !e.IsActive && (e.Data is FrostFrontSO || e.Data is HardShellSO) && RM.CurrentRound == 1,
            "Run setup: the first boss is announced with its zone (" + (e != null ? e.Data.displayName : "none") + "), not active yet");
        firstBoss = e.Data; accent = BossTheme.Accent(firstBoss); zone = OpenZone(e);
        Require(Same(accent, firstBoss is FrostFrontSO ? new Color(.55f, .82f, 1f) : new Color(1f, .62f, .25f)) && zone.Count >= 2 && zone.Count < OpenCells().Count(),
            $"Boss identity color: {firstBoss.displayName} {Hex(accent)}; zone {zone.Count} of {OpenCells().Count()} open cells");
        // Bir bölge hücresine hasar tile'ı (saksısız), diğer bölge hücrelerine saksı + bitki.
        tileCell = zone[0];
        var damage = Tile("Damage", "Common");
        Grid.GetGridObject(tileCell).GetGroundCellCached().ApplyModifier(damage);
        foreach (var cell in zone.Skip(1)) Place(Only(Plant("Grass")), cell.x - Center, cell.z - Center);
        Respawn();
        var markers = Markers; markers.SendMessage("LateUpdate");
        Require(!markers.ShowsActive && markers.ShownCells.OrderBy(c => c.x * 100 + c.z).SequenceEqual(zone) && markers.EdgeCount == Perimeter(zone) && Same(markers.ShownColor, accent) &&
                markers.OutlineRenderer != null && markers.OutlineRenderer.enabled && markers.OutlineRenderer.GetComponent<MeshFilter>().sharedMesh.vertexCount == markers.EdgeCount * 4 &&
                markers.OutlineRenderer.sharedMaterial.GetFloat("_Active") == 0f,
            $"Field: the zone is drawn as its perimeter only: {markers.EdgeCount} cell edges around {zone.Count} cells, boss color, preview (dashed) state");
        Require(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("FrostZoneMarker")) && markers.GetComponentsInChildren<MeshRenderer>(true).Length == 2 &&
                markers.OccludedRenderer.GetComponent<MeshFilter>().sharedMesh == markers.OutlineRenderer.GetComponent<MeshFilter>().sharedMesh &&
                markers.OccludedRenderer.sharedMaterial.GetFloat("_Alpha") < 1f && markers.OutlineRenderer.sharedMaterial.GetFloat("_Alpha") == 1f,
            "Outline shader imports without errors; one outline mesh (drawn once normally, once faint where an object hides it), no per-tile fill quads");
        // Piksel karşılaştırması: çizgi kapalıyken ve açıkken aynı kare (yalnız dünya).
        ShowOutline(false);
        var bare = Capture(null, false);
        ShowOutline(true);
        var preview = Capture("K36_Zone_Preview_World", false);
        Color tileBare = Sample(bare, Ground(tileCell)), tileNow = Sample(preview, Ground(tileCell));
        var emptyOutside = OpenCells().Select(g => g.GetGridPosition()).First(p => !zone.Any(z => z.x == p.x && z.z == p.z) && Grid.GetGridObject(p).GetPlanterBrain() == null);
        Color emptyGround = Sample(preview, Ground(emptyOutside));
        Require(Distance(tileNow, tileBare) < .012f && Distance(tileNow, emptyGround) > .06f,
            $"Damage tile inside the zone keeps its own fill color: {Hex(tileNow)} with the zone shown = {Hex(tileBare)} without it (empty ground is {Hex(emptyGround)}; nothing is blended over the tile)");
        var planterCell = zone[zone.Count - 1];
        Require(Distance(Sample(preview, PlantPoint(planterCell)), Sample(bare, PlantPoint(planterCell))) < .012f &&
                Distance(Sample(preview, Ground(planterCell) + Vector3.up * .25f), Sample(bare, Ground(planterCell) + Vector3.up * .25f)) < .012f,
            "Planter and plant inside the zone: pixels unchanged, no boss color over them");
        boundary = InnerBoundary(tileCell);
        var line = LineSamples(preview, boundary.cell, boundary.side); var lineBare = LineSamples(bare, boundary.cell, boundary.side);
        int drawn = Enumerable.Range(0, line.Count).Count(i => Distance(line[i], lineBare[i]) > .08f);
        int runs = Enumerable.Range(1, line.Count - 1).Count(i => (Distance(line[i], lineBare[i]) > .08f) != (Distance(line[i - 1], lineBare[i - 1]) > .08f));
        Require(drawn >= line.Count * .3f && drawn <= line.Count * .8f && runs >= 4,
            $"Preview perimeter is a dashed line: {drawn} of {line.Count} samples along one cell edge are drawn ({runs} on/off changes), the rest show the ground");
        Object.DestroyImmediate(bare); Object.DestroyImmediate(preview);

        // Harita: çerçeve + köşe işareti, tile rengi aynı.
        var map = Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
        map.BuildGrid();
        var cells = map.GetComponentsInChildren<TileCellUI>(false);
        var marked = cells.Where(t => t.InEventZone).ToList();
        var tileUi = cells.First(t => t.name == $"Cell_x{tileCell.x}_z{tileCell.z}");
        Require(marked.Count == zone.Count && marked.All(t => !t.EventZoneActive && Same(t.EventZoneColor, accent) && t.GetComponentInChildren<BossZoneFrame>() != null) && tileUi.InEventZone &&
                Same(tileUi.GetComponent<Image>().color, damage.tileColor),
            $"Round map: the same {zone.Count} cells carry a dashed frame in the boss color; the tile image keeps its own color ({Hex(damage.tileColor)})");
        Canvas.ForceUpdateCanvases();
        Require(FrameLeavesCentreFree(tileUi.GetComponentInChildren<BossZoneFrame>()), "Map frame geometry: nothing is drawn over the central 60 % of the tile (frame and a small corner mark only)");
        Capture("K36_Map_Preview", true);

        // HUD: yaklaşan boss satırı boss'un kendi renginde; kâğıt henüz boyanmaz.
        StartRound(1);
        RefreshHud();
        Require(Hud.EventLineText != null && Hud.EventLineText.Contains(firstBoss.displayName) && Same(Hud.EventLineColor, BossTheme.Ink(firstBoss)) && Same(Hud.PaperColor, BossTheme.Paper(null)),
            $"HUD preview line \"{Hud.EventLineText}\" uses the boss identity color {Hex(Hud.EventLineColor)}; the card paper stays neutral until the boss is active");
        Require(SegmentEventText.IntroSubject(Events, RM) == firstBoss, "Round intro text takes its color from the same boss identity");
        return .3;
    }

    // Çerçevenin mesh'i hücre merkezine (kenarların %60'lık iç bölgesi) girmiyor.
    static bool FrameLeavesCentreFree(BossZoneFrame frame)
    {
        var rect = frame.rectTransform.rect;
        using (var helper = new VertexHelper())
        {
            typeof(BossZoneFrame).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null)
                .Invoke(frame, new object[] { helper });
            var mesh = new Mesh(); helper.FillMesh(mesh);
            var v = mesh.vertices; var t = mesh.triangles; Object.DestroyImmediate(mesh);
            if (v.Length == 0) return false;
            var centre = new Rect(rect.center.x - rect.width * .3f, rect.center.y - rect.height * .3f, rect.width * .6f, rect.height * .6f);
            for (int gx = 0; gx <= 8; gx++) for (int gy = 0; gy <= 8; gy++)
            {
                var p = new Vector2(Mathf.Lerp(centre.xMin, centre.xMax, gx / 8f), Mathf.Lerp(centre.yMin, centre.yMax, gy / 8f));
                for (int i = 0; i < t.Length; i += 3) if (Inside(p, v[t[i]], v[t[i + 1]], v[t[i + 2]])) return false;
            }
            return true;
        }
    }

    static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y), d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y), d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // ---------------------------------------------------------------- 4) level başına üç seçim + erken davranış slotu
    static double LevelChoices()
    {
        var cards = Cards;
        Require(RM.LevelsGained == 0 && RM.CardChoicesGranted == 0 && RM.PendingCardSelections == 0 && Progress.CurrentLevel == 1, "New run: no level, no pending choice");
        Progress.AddXP(Progress.XPToNextLevel);
        Progress.AddXP(Progress.XPToNextLevel);
        Require(Progress.CurrentLevel == 3 && RM.LevelsGained == 2 && RM.CardChoicesGranted == 6 && RM.PendingCardSelections == 6,
            "Two levels gained in the round: 2 × 3 = 6 separate choices queued (none lost)");
        UnityEngine.Random.InitState(77);
        Call(RM, "EndRound");
        Require(State == GameStates.CardSelection && Offers.Count == 3 && cards.GuaranteedBehaviorOffer != null && Offers.Contains(cards.GuaranteedBehaviorOffer),
            "Round end opens the first choice: three candidates, one of them from the behavior candidates");
        int guard = 0;
        while (Offers.All(IsBehavior) && guard++ < 50) cards.RefreshCards();
        var first = Offers.ToList(); var other = first.First(o => !IsBehavior(o));
        int tiles = TileCount();
        Call(cards, "OnCardSelected", other);
        Require(RM.PendingCardSelections == 5 && State == GameStates.CardSelection && Offers.Count == 3 && !Offers.Any(first.Contains) && TileCount() == tiles + 1,
            "One card is taken from the three (not all three); the next choice shows three NEW candidates generated after the first was applied");
        Require(Progress.FirstBehaviorCardRound == 0 && cards.GuaranteedBehaviorOffer != null,
            "The player took another card: nothing was forced, and the next choice offers a behavior slot again");
        // Çift tıklama: alınmış (eski) teklif ikinci kez tüketilemez.
        Call(cards, "OnCardSelected", other);
        Require(RM.PendingCardSelections == 5 && TileCount() == tiles + 1, "Double click on the already taken card does nothing (no extra card, no extra choice consumed)");
        // Yeniden açılma hak üretmez / tüketmez.
        Object.FindFirstObjectByType<UIManager>().ShowCardSelectionUI();
        Require(RM.PendingCardSelections == 5 && RM.CardChoicesGranted == 6 && State == GameStates.CardSelection && cards.GuaranteedBehaviorOffer != null,
            "Reopening the panel does not create or consume a choice");
        var pick = cards.GuaranteedBehaviorOffer;
        Call(cards, "OnCardSelected", pick);
        Require(Progress.FirstBehaviorCardRound == 1 && TileCount() == tiles + 2 && cards.GuaranteedBehaviorOffer == null && RM.PendingCardSelections == 4 &&
                OpenCells().Count(g => g.CurrentModifier == pick.Tile) == 1,
            $"Behavior card ({pick.Tile.modifierName}) taken through the normal choice and placed by the existing random rule; the next choice is back to the normal distribution");
        // Atlama yalnız o seçimi tüketir; ikinci basışta hak yoksa hiçbir şey olmaz.
        int skips = RM.SkipUsesRemaining;
        Call(cards, "OnSkipPressed");
        Require(skips == 1 && RM.SkipUsesRemaining == 0 && RM.PendingCardSelections == 3 && State == GameStates.CardSelection && TileCount() == tiles + 2 && Offers.Count == 3,
            "Skip consumes only that one choice; the level's remaining choices stay queued");
        Call(cards, "OnSkipPressed");
        Require(RM.PendingCardSelections == 3 && State == GameStates.CardSelection, "No skip left: pressing skip again changes nothing");
        int rest = ResolveCards();
        Require(rest == 3 && RM.PendingCardSelections == 0 && State == GameStates.RoundEnd && TileCount() == tiles + 5,
            "The remaining three choices are taken one after another; then the round summary opens (6 choices = 5 cards + 1 skip)");
        // Seçim bittikten sonraki basışlar hak üretmez, durum değiştirmez.
        Call(cards, "OnSkipPressed"); Call(cards, "OnCardSelected", pick);
        Require(RM.PendingCardSelections == 0 && State == GameStates.RoundEnd && TileCount() == tiles + 5, "Late clicks after the last choice do nothing");
        int forced = 0;
        for (int n = 0; n < 60; n++) { cards.RefreshCards(); if (cards.GuaranteedBehaviorOffer != null) forced++; }
        Require(forced == 0, "After the first behavior card no slot is reserved any more (0 of 60 offers)");
        // Grid dolu: tile yükseltme, sonra temel güç alternatifleri duruyor.
        foreach (var ground in OpenCells().ToList()) if (ground.CurrentModifier == null) ground.ApplyModifier(Tile("Fertile", "Common"));
        cards.RefreshCards();
        Require(!Progress.HasEligibleCell() && Offers.Count == 3 && Offers.All(o => o.IsUpgrade), "Grid full: the three candidates are tile upgrades (existing alternative kept)");
        foreach (var ground in OpenCells().ToList()) ground.AddLevels(GroundCell.MaxLevel);
        cards.RefreshCards();
        Require(Offers.Count == 3 && Offers.All(o => o.IsBaseStat && !o.IsUpgrade), "All tiles at max level: the candidates are permanent base-power cards (existing alternative kept)");
        ClearField();
        return .8;
    }

    // ---------------------------------------------------------------- 5) aktif boss: çizgi düz, HUD boss renginde
    static double BossRoundActive()
    {
        // Bölgeyi önizlemedeki gibi kur (önceki adım alanı temizledi).
        var damage = Tile("Damage", "Common");
        Grid.GetGridObject(tileCell).GetGroundCellCached().ApplyModifier(damage);
        foreach (var cell in zone.Skip(1)) Place(Only(Plant("Grass")), cell.x - Center, cell.z - Center);
        for (int r = 2; r <= 4; r++) { StartRound(r); AddScore(60); EndRoundQuiet(); }
        StartRound(5);
        Respawn();
        var active = Events.Active;
        Require(active != null && active.Data == firstBoss && active.IsActive && OpenZone(active).SequenceEqual(zone) && RM.IsBossRound(5), "Round 5: the announced boss is active on the announced zone");
        var weather = Object.FindAnyObjectByType<BossWeatherOverlay>();
        weather.SendMessage("LateUpdate");
        Require(weather.CurrentWeather == (firstBoss is FrostFrontSO ? BossWeatherOverlay.Weather.Snow : BossWeatherOverlay.Weather.Sand),
            "Screen weather effect is separate from the cell markers and still follows the active boss: " + weather.CurrentWeather);
        var markers = Markers; markers.SendMessage("LateUpdate");
        Require(markers.ShowsActive && markers.ShownCells.OrderBy(c => c.x * 100 + c.z).SequenceEqual(zone) && markers.EdgeCount == Perimeter(zone) &&
                markers.OutlineRenderer.sharedMaterial.GetFloat("_Active") == 1f && Same(markers.ShownColor, accent),
            "Field: the same perimeter switches to the active (solid) state; the zone itself is unchanged");
        ShowOutline(false);
        var bare = Capture(null, false);
        ShowOutline(true);
        var picture = Capture("K36_Zone_Active_World", false);
        var line = LineSamples(picture, boundary.cell, boundary.side); var lineBare = LineSamples(bare, boundary.cell, boundary.side);
        int drawn = Enumerable.Range(0, line.Count).Count(i => Distance(line[i], lineBare[i]) > .08f);
        int inColor = line.Count(c => Distance(c, accent) < .25f);
        Require(drawn >= line.Count * .95f && inColor >= line.Count * .85f,
            $"Active perimeter is a solid line in the boss color: {drawn} of {line.Count} samples drawn, {inColor} in the boss color {Hex(accent)}");
        var planterCell = zone[zone.Count - 1];
        Require(Distance(Sample(picture, Ground(tileCell)), Sample(bare, Ground(tileCell))) < .012f &&
                Distance(Sample(picture, PlantPoint(planterCell)), Sample(bare, PlantPoint(planterCell))) < .012f &&
                Distance(Sample(picture, Ground(planterCell) + Vector3.up * .25f), Sample(bare, Ground(planterCell) + Vector3.up * .25f)) < .012f,
            "Active boss: tile fill and planter / plant pixels are still unchanged (no boss fill over them)");
        Object.DestroyImmediate(bare); Object.DestroyImmediate(picture);
        RefreshHud();
        Color paper = Hud.PaperColor;
        Require(Same(paper, BossTheme.Paper(firstBoss)) && Same(Hud.EventLineColor, BossTheme.Ink(firstBoss)) && (firstBoss is HardShellSO ? paper.r > paper.b + .05f : paper.b > paper.r + .05f) &&
                Hud.EventLineText.StartsWith(firstBoss.displayName),
            $"Boss HUD: paper {Hex(paper)} and rule line {Hex(Hud.EventLineColor)} derive from {firstBoss.displayName} ({(firstBoss is HardShellSO ? "amber, not ice blue" : "ice blue")})");
        var texts = Hud.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeSelf).ToDictionary(t => t.name, t => t.text);
        Require(texts.ContainsKey("Boss") && texts["Boss"].Contains("#B4530A"), "Status color kept (boss harvest not reached yet): " + Strip(texts["Boss"]));
        // Görsel kontrol için: bütün açık hücrelerde saksı varken çizgi (dünya + arayüz).
        foreach (var ground in OpenCells().ToList())
            if (ground.Planter == null) Place(Only(Plant("Grass")), ground.GetGridPosition().x - Center, ground.GetGridPosition().z - Center);
        Respawn();
        Capture("K36_Zone_Active_" + (firstBoss is HardShellSO ? "SertKabuk" : "Don"), true);
        // Tarla saksıyla doluyken sınır yine okunur: saksının arkasında kalan çizgi soluk çizilir (dolgu değil, ince çizgi).
        ShowOutline(false);
        var bareFull = Capture(null, false);
        ShowOutline(true);
        var full = Capture("K36_Zone_Active_Full_World", false);
        // Saksılı bir bölge hücresinin, bölge dışındaki açık hücreye bakan kenarı.
        var edge = zone.Skip(1).Where(cell => { try { InnerBoundary(cell); return true; } catch (Exception) { return false; } }).Select(InnerBoundary).First();
        var hiddenLine = LineSamples(full, edge.cell, edge.side); var hiddenBare = LineSamples(bareFull, edge.cell, edge.side);
        int faint = Enumerable.Range(0, hiddenLine.Count).Count(i => Distance(hiddenLine[i], hiddenBare[i]) > .03f);
        Require(faint >= hiddenLine.Count * .6f &&
                Distance(Sample(full, PlantPoint(edge.cell)), Sample(bareFull, PlantPoint(edge.cell))) < .012f &&
                Distance(Sample(full, Ground(edge.cell) + Vector3.up * .25f), Sample(bareFull, Ground(edge.cell) + Vector3.up * .25f)) < .012f,
            $"Field full of planters: the zone border under a planter is still readable ({faint} of {hiddenLine.Count} samples along the edge show the line); the planter's and plant's own pixels at the cell centre are unchanged");
        Object.DestroyImmediate(bareFull); Object.DestroyImmediate(full);
        AddScore(200);
        RefreshHud();
        Require(Hud.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.name == "Boss").text.Contains("#2E7D32"), "Status color kept (boss harvest reached: green)");
        // Boss round'unda bir level: sıra kartlar → boss ödülü → özet.
        Progress.AddXP(Progress.XPToNextLevel);
        return .2;
    }

    // ---------------------------------------------------------------- 6) boss bitti: sıra, R5 teklifi, eski renk kalmaz
    static double AfterBoss()
    {
        Call(RM, "EndRound");
        Require(State == GameStates.CardSelection && RM.PendingCardSelections == 3 && Boss.IsPending, "Boss passed with one level: the three card choices come first, the boss reward waits");
        Require(ResolveCards() == 3 && State == GameStates.RoundChoice && Boss.IsPending, "After the last card choice the boss reward selection opens");
        var offer = Boss.Offer.ToList();
        Require(offer.Count == 3 && offer.Distinct().Count() == 3 && offer.All(r => !r.IsBreakthrough), "R5 boss offer: three different small rewards, no breakthrough reward before R10");
        var panel = Object.FindFirstObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);
        panel.gameObject.SetActive(false); panel.gameObject.SetActive(true);
        Boss.PrepareOffer();
        Require(Boss.Offer.SequenceEqual(offer), "The offer does not change when it is read again or when the panel is reopened");
        Require(Boss.Choose(offer[0]) && Boss.Taken.Count == 1 && !Boss.Choose(offer[1]) && Boss.Taken.Count == 1 && State == GameStates.RoundEnd,
            "One of the three rewards is taken; a second request for the same boss is rejected; then the round summary");
        Events.SendMessage("LateUpdate");
        var next = Events.Upcoming;
        Require(Events.Active == null && next != null && next.Data != firstBoss, "Boss ended; the next boss is a different one: " + (next != null ? next.Data.displayName : "none"));
        var markers = Markers; markers.SendMessage("LateUpdate");
        Require(!markers.ShowsActive && (OpenZone(next).Count == 0 ? markers.ShownCells.Count == 0 : Same(markers.ShownColor, BossTheme.Accent(next.Data))),
            "Field markers follow the new boss (preview state, its own color) or disappear for a boss without a zone");
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        var notice = F<TextMeshProUGUI>(summary, "eventNotice");
        // Özet bildirimi: anlattığı boss'un renginde. Biten round verildiğinde biten boss'u, verilmediğinde yaklaşanı anlatır.
        Require(SegmentEventText.NoticeSubject(Events, 5) == firstBoss && SegmentEventText.Notice(Events, 5).Contains(firstBoss.displayName) &&
                SegmentEventText.NoticeSubject(Events, 0) == next.Data && SegmentEventText.Notice(Events, 0).Contains(next.Data.displayName),
            "Round summary notice and its color subject agree: the finished boss right after its round, otherwise the upcoming boss");
        Call(summary, "RefreshNotices");
        var filled = F<object>(summary, "filled");
        int finished = filled != null ? (int)filled.GetType().GetField("Round").GetValue(filled) : 0;
        var subject = SegmentEventText.NoticeSubject(Events, finished);
        Require(notice.gameObject.activeSelf && Same(notice.color, BossTheme.Ink(subject)) && Strip(notice.text).Contains(subject.displayName) &&
                !Same(BossTheme.Ink(firstBoss), BossTheme.Ink(next.Data)),
            $"Round summary on screen: \"{Strip(notice.text).Split('\n')[0]}\" in the color of the boss it talks about ({Hex(notice.color)})");
        StartRound(6);
        RefreshHud();
        Require(Same(Hud.PaperColor, BossTheme.Paper(null)) && Same(Hud.EventLineColor, BossTheme.Ink(next.Data)) && !Same(Hud.EventLineColor, BossTheme.Ink(firstBoss)) &&
                Hud.EventLineText.Contains(next.Data.displayName),
            $"Next round: paper back to neutral, line in the new boss's color {Hex(Hud.EventLineColor)} (the old {Hex(BossTheme.Ink(firstBoss))} is gone)");
        return .2;
    }

    // ---------------------------------------------------------------- 7) ödül teklifinde kırılma slotu
    static double RewardSlot()
    {
        EndRoundQuiet();
        ClearField();
        Boss.ClearAll();
        var pool = K1.bossRewards;
        var artci = Break("artci_patlama"); var cifte = Break("cifte_akim"); var ritim = Break("hasat_ritmi");
        Require(!Boss.IsEligible(artci) && !Boss.IsEligible(cifte) && Boss.IsEligible(ritim), "Empty field: only Hasat Ritmi is eligible (the echo rewards need their behavior on a placed planter)");
        Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Common"), .3f);
        Require(Boss.IsEligible(artci) && !Boss.IsEligible(cifte) && Boss.IsEligible(ritim),
            "Eligibility: Artçı Patlama needs a placed explosion planter (present), Çifte Akım a placed electric planter (absent)");
        var offerList = F<List<BossRewardSO>>(Boss, "offer");
        var seenAt = new Dictionary<int, List<List<BossRewardSO>>>();
        UnityEngine.Random.InitState(555);
        string stream = JsonUtility.ToJson(UnityEngine.Random.state);
        foreach (int round in new[] { 5, 10, 25, 45 })
        {
            seenAt[round] = new List<List<BossRewardSO>>();
            for (int seed = 1; seed <= 80; seed++)
            {
                SetF(Boss, "offerRound", round);
                SetP(Events, "RunSeed", seed);
                Call(Boss, "BuildOffer", pool, round / 5);
                var offer = offerList.ToList();
                if (offer.Count != offer.Distinct().Count() || offer.Count != pool.choices) throw new Exception("offer has duplicates or wrong size");
                Call(Boss, "BuildOffer", pool, round / 5);
                if (!offerList.SequenceEqual(offer)) throw new Exception("same seed and state gave a different offer");
                seenAt[round].Add(offer);
            }
        }
        Require(JsonUtility.ToJson(UnityEngine.Random.state) == stream, "Building reward offers does not consume the gameplay random stream; the same seed gives the same offer");
        Require(seenAt[5].All(o => o.All(r => !r.IsBreakthrough)), "R5: no breakthrough reward in any of 80 offers");
        foreach (int round in new[] { 10, 25, 45 })
            if (!seenAt[round].All(o => o.Count(r => r.IsBreakthrough) == 1 && o.Count(r => !r.IsBreakthrough) == 2)) throw new Exception("round " + round + " offers must hold exactly one breakthrough");
        var picked = seenAt[10].Select(o => o.First(r => r.IsBreakthrough)).ToList();
        var positions = seenAt[10].Select(o => o.FindIndex(r => r.IsBreakthrough)).ToList();
        Require(picked.Contains(artci) && picked.Contains(ritim) && !picked.Contains(cifte) && positions.Distinct().Count() == 3,
            $"From R10: exactly one slot holds a random eligible breakthrough reward (Artçı {picked.Count(r => r == artci)}, Ritim {picked.Count(r => r == ritim)} of 80, never the ineligible Çifte Akım); its position varies; the other two slots come from the normal pool");
        Place(Only(Plant("Grass")), 2, 0, Tile("Electric", "Common"), .3f);
        Require(Boss.IsEligible(cifte), "With an electric planter placed Çifte Akım becomes eligible");
        // Alınan kırılma ödülü bir daha sunulmaz; hepsi alınınca normal teklife dönülür.
        Require(Grant(artci) && !Boss.IsEligible(artci) && !Grant(artci) && Boss.Stacks(artci) == 1, "A breakthrough reward is taken at most once per run");
        for (int seed = 1; seed <= 40; seed++)
        {
            SetF(Boss, "offerRound", 25); SetP(Events, "RunSeed", seed);
            Call(Boss, "BuildOffer", pool, 5);
            if (offerList.Contains(artci) || offerList.Count(r => r.IsBreakthrough) != 1) throw new Exception("taken breakthrough offered again");
        }
        Require(true, "The taken one is never offered again; the slot goes to the remaining eligible breakthrough rewards");
        Grant(cifte); Grant(ritim);
        for (int seed = 1; seed <= 40; seed++)
        {
            SetF(Boss, "offerRound", 25); SetP(Events, "RunSeed", seed);
            Call(Boss, "BuildOffer", pool, 5);
            if (offerList.Any(r => r.IsBreakthrough) || offerList.Count != pool.choices || offerList.Distinct().Count() != pool.choices) throw new Exception("offer after all breakthroughs taken");
        }
        Require(true, "All three taken: offers return to the normal flow (three different small rewards)");
        offerList.Clear();
        SetP(Events, "RunSeed", Seed);
        Require(BossRewardManager.TryGetEcho(DamageType.Explosion, out var e1) && Near(e1.Damage, artci.echoDamage) && Near(e1.Radius, artci.echoRadius) && BossRewardManager.TryGetEcho(DamageType.Electric, out var e2) &&
                Near(e2.Delay, cifte.echoDelay) && BossRewardManager.TryGetRhythm(out var rh) && rh.Harvests == ritim.rhythmHarvests && Boss.OwnedModifiers.Count == 0,
            "Taken breakthrough rewards expose their data through the reward manager (no stat modifier involved)");
        string card = BossRewardText.Effect(artci) + " | " + BossRewardText.Effect(cifte) + " | " + BossRewardText.Effect(ritim);
        Require(card.Contains("%" + Mathf.RoundToInt(artci.echoDamage * 100)) && card.Contains("%" + Mathf.RoundToInt(cifte.echoDamage * 100)) && card.Contains("her " + ritim.rhythmHarvests),
            "Card texts are written from the reward data: " + card);
        Boss.ClearAll();
        Require(!BossRewardManager.TryGetEcho(DamageType.Explosion, out _) && !BossRewardManager.TryGetEcho(DamageType.Electric, out _) && !BossRewardManager.TryGetRhythm(out _) && Boss.Taken.Count == 0,
            "ClearAll (new run / menu) removes the breakthrough effects");
        ClearField();
        return .8;
    }

    // ---------------------------------------------------------------- 8) Kıvılcım: gerçek şans, tek çarpım, sınır, uyarı yok
    static readonly StatType[] Chances = { StatType.ExplosionChance, StatType.TornadoChance, StatType.BoomerangChance, StatType.ElectricChance };

    static double KivilcimChance()
    {
        GridUnlockManager.Instance.UnlockNextTier(7);
        StartRound(47);
        var kivilcim = Small("kivilcim");
        float factor = 1f + kivilcim.modifiersPerStack[0].value;
        Require(kivilcim.modifiersPerStack.Select(m => m.statType).SequenceEqual(Chances) && kivilcim.modifiersPerStack.All(m => m.target == StatTarget.Planter &&
                m.operation == ModifierOperation.MorePercent && Near(m.value, factor - 1f)) && kivilcim.maxStacks == 3,
            $"Kıvılcım multiplies the four behavior chances of planters by ×{factor} per copy (up to 3)");
        Require(Chances.Skip(1).All(s => !Stats.HasBaseStat(s)) && Stats.HasBaseStat(StatType.HarvestDamage) && K1.balance.coreStats.TryGetBaseStat(StatType.HarvestDamage, out _) &&
                !K1.balance.coreStats.TryGetBaseStat(StatType.ElectricChance, out _),
            "Tornado / Boomerang / Electric chance have no base in the core (player/global) stat set: their base belongs to the planter (no made-up value was added to the core set)");
        var explode = Place(Only(Plant("Grass")), -2, 0, Tile("Explosive", "Common"), .30f);
        var tornado = Place(Only(Plant("Grass")), -1, 0, Tile("Tornado", "Common"), .22f);
        var boomerang = Place(Only(Plant("Grass")), 0, 0, Tile("Boomerang", "Common"), .18f);
        var electric = Place(Only(Plant("Grass")), 1, 0, Tile("Electric", "Common"), .40f);
        var high = Place(Only(Plant("Grass")), 2, 0, Tile("Electric", "Legendary"), .80f);
        var plain = Place(Only(Plant("Grass")), 0, 2);
        var planters = new[] { explode, tornado, boomerang, electric, high, plain };
        float[,] before = new float[planters.Length, 4];
        for (int p = 0; p < planters.Length; p++) for (int s = 0; s < 4; s++) before[p, s] = planters[p].GetFinalStat(Chances[s]);
        Require(Near(before[0, 0], .30f) && Near(before[1, 1], .22f) && Near(before[2, 2], .18f) && Near(before[3, 3], .40f) && Near(before[4, 3], .80f) &&
                Enumerable.Range(0, 4).All(s => before[5, s] == 0f) && before[0, 3] == 0f && before[3, 0] == 0f,
            "Before the reward: each planter's chance is the value of its own tile (planter base 0 + tile); a planter without that tile has 0");
        // Bildirim: saksıya ait statta global değer yok (NaN); yine de dört bildirim gider.
        var seen = new List<(StatType stat, float value)>();
        Action<StatType, float> listen = (s, v) => seen.Add((s, v));
        Stats.OnStatChanged += listen;
        int warnings0 = baseWarnings;
        bool granted = Grant(kivilcim);
        Stats.OnStatChanged -= listen;
        Require(granted && baseWarnings == warnings0,
            "Kıvılcım taken through the reward manager: no 'base stat not found' warning (the player/global base is no longer queried for planter stats)");
        Require(Chances.All(c => seen.Count(x => x.stat == c) == 1) && seen.Where(x => x.stat != StatType.ExplosionChance && Chances.Contains(x.stat)).All(x => float.IsNaN(x.value)),
            "Change notifications are still sent once for each of the four stats; planter-owned stats report no global value");
        for (int p = 0; p < planters.Length; p++) for (int s = 0; s < 4; s++)
        {
            float expected = Mathf.Clamp01(before[p, s] * factor);
            if (!Near(planters[p].GetFinalStat(Chances[s]), expected, 1e-4f)) throw new Exception($"planter {p} {Chances[s]}: {planters[p].GetFinalStat(Chances[s])} expected {expected}");
        }
        Require(Near(explode.GetFinalStat(StatType.ExplosionChance), .30f * factor, 1e-4f) && Near(high.GetFinalStat(StatType.ElectricChance), 1f) && plain.GetFinalStat(StatType.ExplosionChance) == 0f &&
                explode.GetFinalStat(StatType.ElectricChance) == 0f,
            $"After one Kıvılcım: existing chances × {factor} exactly once (0,30 → {explode.GetFinalStat(StatType.ExplosionChance):0.###}), capped at 1 (0,80 → {high.GetFinalStat(StatType.ElectricChance):0.###}), no new behavior on planters without it");
        Grant(kivilcim); Grant(kivilcim);
        float three = Mathf.Pow(factor, 3);
        Require(Boss.Stacks(kivilcim) == 3 && !Grant(kivilcim) && Near(explode.GetFinalStat(StatType.ExplosionChance), Mathf.Clamp01(.30f * three), 1e-4f) && Near(tornado.GetFinalStat(StatType.TornadoChance), Mathf.Clamp01(.22f * three), 1e-4f) &&
                Near(boomerang.GetFinalStat(StatType.BoomerangChance), Mathf.Clamp01(.18f * three), 1e-4f) && Near(electric.GetFinalStat(StatType.ElectricChance), Mathf.Clamp01(.40f * three), 1e-4f) &&
                plain.GetFinalStat(StatType.TornadoChance) == 0f && baseWarnings == warnings0,
            $"Three copies: ×{three:0.###} on each existing chance ({explode.GetFinalStat(StatType.ExplosionChance):0.###} / {tornado.GetFinalStat(StatType.TornadoChance):0.###} / {boomerang.GetFinalStat(StatType.BoomerangChance):0.###} / {electric.GetFinalStat(StatType.ElectricChance):0.###}); still no warning");
        // Ödül metni (panel ve HUD listesi) taban sorgulamaz.
        string card = BossRewardText.Effect(kivilcim), line = BossRewardText.ListLine(kivilcim, 3);
        RefreshHud();
        Require(card.Length > 0 && line.Contains("3/3") && baseWarnings == warnings0, "Reward card and HUD list texts are produced without any stat query warning: " + line);
        Boss.ClearAll();
        Require(baseWarnings == warnings0 && Near(explode.GetFinalStat(StatType.ExplosionChance), .30f) && Near(high.GetFinalStat(StatType.ElectricChance), .80f),
            "Removing the reward (new run / menu path) restores the chances and logs no warning either");
        return .1;
    }

    // ---------------------------------------------------------------- 9) Kıvılcım: gerçek tetik sıklığı
    static double KivilcimTriggers()
    {
        var kivilcim = Small("kivilcim");
        float factor = 1f + kivilcim.modifiersPerStack[0].value;
        ClearField();
        Strike(100000f, .4f);
        var explode = Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Common"), .20f);
        var electric = Place(Only(Plant("Grass")), 2, 2, Tile("Electric", "Epic"), .80f);
        Place(Only(Plant("Grass")), -2, -2);
        UnityEngine.Random.InitState(909);
        int Kill(int dx, int dz, int n, DamageType type)
        {
            var spawner = SpawnerAt(dx, dz); Vector3 at = Pos(dx, dz);
            int start = HarvestBehaviorStats.Triggered(type), harvested = harvests;
            for (int i = 0; i < n; i++) { Respawn(spawner); Attack(at); }
            if (harvests - harvested != n) throw new Exception($"expected {n} direct harvests, got {harvests - harvested}");
            return HarvestBehaviorStats.Triggered(type) - start;
        }
        const int N = 1000;
        float p0 = explode.GetFinalStat(StatType.ExplosionChance);
        int t0 = Kill(0, 0, N, DamageType.Explosion);
        int plain0 = Kill(-2, -2, 200, DamageType.Explosion);
        int e0 = Kill(2, 2, 200, DamageType.Electric);
        Grant(kivilcim); Grant(kivilcim); Grant(kivilcim);
        float p1 = explode.GetFinalStat(StatType.ExplosionChance);
        int t1 = Kill(0, 0, N, DamageType.Explosion);
        int plain1 = Kill(-2, -2, 200, DamageType.Explosion);
        int e1 = Kill(2, 2, 200, DamageType.Electric);
        float Tolerance(float p) => 4f * Mathf.Sqrt(p * (1f - p) / N);
        Require(Near(p0, .20f) && Near(p1, .20f * Mathf.Pow(factor, 3), 1e-4f) && Mathf.Abs(t0 / (float)N - p0) <= Tolerance(p0) && Mathf.Abs(t1 / (float)N - p1) <= Tolerance(p1) && t1 > t0 * 1.6f,
            $"Real explosion triggers on {N} direct harvests: {t0} at chance {p0:0.###} before, {t1} at chance {p1:0.###} after three Kıvılcım (the trigger frequency follows the computed chance)");
        Require(plain0 == 0 && plain1 == 0, "A planter without the behavior never triggers it, before or after the reward (0 of 200 + 200 harvests)");
        Require(e0 < 200 && e0 > 120 && e1 == 200 && Near(electric.GetFinalStat(StatType.ElectricChance), 1f),
            $"Electric at 0,80: {e0} of 200 before; capped at 1,00 after the reward: {e1} of 200 (every direct harvest triggers, exactly once each)");
        Boss.ClearAll(); ClearTestMods();
        return .1;
    }

    // ---------------------------------------------------------------- 10–14) Artçı Patlama
    static int firstDamage, triggersBefore, harvestsBefore;

    static void BuildBlastField(bool withChain)
    {
        ClearField();
        Strike(40f, .4f);
        Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Legendary"), 1f);
        foreach (var p in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (2, 0) })
        {
            if (withChain && p == (1, 0)) Place(Only(Plant("Grape")), 1, 0, Tile("Explosive", "Legendary"), 1f);
            else Place(Only(Plant("Grape")), p.Item1, p.Item2);
        }
        Respawn();
    }

    static double AftershockTrigger()
    {
        var artci = Break("artci_patlama"); var yikim = Small("yikim_gucu");
        Require(Echoes != null && Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Explosion) == 0, "Echo scheduler present, idle");
        // Ödül yokken: yalnız normal patlama.
        BuildBlastField(false);
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(Pos(0, 0));
        Require(Lost(1, 0) == 40 && Lost(0, 1) == 40 && Lost(-1, 0) == 40 && Lost(0, -1) == 40 && Lost(1, 1) == 0 && Lost(2, 0) == 0 && Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Explosion) == 0,
            "Without the reward: one explosion, 40 damage to the four neighbours, nothing scheduled");
        // Ödül + Yıkım Gücü: ilk patlama 40 × davranış çarpanı; artçı = o HESAPLANMIŞ hasarın oranı (çarpan ikinci kez uygulanmaz).
        Grant(artci); Grant(yikim);
        BuildBlastField(false);
        firstDamage = PlantAt(0, 0).Owner.GetBehaviorDamage(40, DamageType.Explosion);
        triggersBefore = HarvestBehaviorStats.Triggered(DamageType.Explosion);
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(Pos(0, 0));
        Require(firstDamage == Mathf.RoundToInt(40f * yikim.behaviorDamageMultiplier) && firstDamage > 40 && Lost(1, 0) == firstDamage && Lost(1, 1) == 0 && Echoes.Pending == 1 &&
                Echoes.Scheduled(DamageType.Explosion) == 1 && Echoes.Executed(DamageType.Explosion) == 0,
            $"With the reward (and Yıkım Gücü): the normal explosion hits for its computed {firstDamage}; one aftershock is scheduled and has not hit yet");
        return artci.echoDelay + .8;
    }

    static double AftershockResult()
    {
        var artci = Break("artci_patlama");
        int echo = BehaviorEchoes.ScaleDamage(firstDamage, artci.echoDamage);
        Require(Echoes.Pending == 0 && Echoes.Executed(DamageType.Explosion) == 1 && Lost(1, 0) == firstDamage + echo && Lost(0, 1) == firstDamage + echo && Lost(-1, 0) == firstDamage + echo && Lost(0, -1) == firstDamage + echo,
            $"After {artci.echoDelay} s the aftershock hits the same neighbours for {echo} = {firstDamage} × {artci.echoDamage} (computed damage × ratio; the behavior multiplier is not applied a second time)");
        Require(Lost(1, 1) == echo && Lost(-1, -1) == echo && Lost(2, 0) == 0,
            $"Aftershock radius ×{artci.echoRadius}: the diagonal neighbours are hit too ({echo}), the cell two steps away is not");
        Require(Echoes.Hits(DamageType.Explosion) == 6 && Echoes.Kills(DamageType.Explosion) == 0 && HarvestBehaviorStats.Triggered(DamageType.Explosion) == triggersBefore + 1 && Echoes.Scheduled(DamageType.Explosion) == 1,
            "One trigger, one aftershock (6 plants hit); the aftershock is not a new trigger and schedules nothing");
        // Zincir yok: artçının öldürdüğü, patlama şansı %100 olan saksı patlamaz ve yeni artçı planlamaz.
        BuildBlastField(true);
        SetF(PlantAt(1, 0), "currentHealth", firstDamage + 1);      // ilk patlamadan 1 canla çıkar, artçıda ölür
        SetF(PlantAt(0, 0), "currentHealth", 1);
        triggersBefore = HarvestBehaviorStats.Triggered(DamageType.Explosion);
        harvestsBefore = harvests;
        Attack(Pos(0, 0));
        Require(!Dead(1, 0) && PlantAt(1, 0).CurrentHealth == 1 && Echoes.Pending == 1 && harvests == harvestsBefore + 1,
            "Chain test: the neighbour (explosion chance 100 %) survives the first explosion with 1 HP");
        return artci.echoDelay + .8;
    }

    static double AftershockChainResult()
    {
        Require(Dead(1, 0) && harvests == harvestsBefore + 2 && Echoes.Kills(DamageType.Explosion) == 1 && Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Explosion) == 2 &&
                HarvestBehaviorStats.Triggered(DamageType.Explosion) == triggersBefore + 1 && Lost(2, 0) == 0,
            "The aftershock really harvested that plant (a behavior harvest): no explosion from it, no new aftershock, the plant behind it is untouched");
        return .05;
    }

    static double AftershockDropAndReuse()
    {
        var artci = Break("artci_patlama");
        // Round biterse bekleyen artçı düşer.
        BuildBlastField(false);
        SetF(PlantAt(0, 0), "currentHealth", 1);
        int dropped = Echoes.Dropped(DamageType.Explosion), executed = Echoes.Executed(DamageType.Explosion);
        Attack(Pos(0, 0));
        Require(Echoes.Pending == 1, "Aftershock scheduled");
        EndRoundQuiet();
        Require(Echoes.Pending == 0 && Echoes.Dropped(DamageType.Explosion) == dropped + 1 && Echoes.Executed(DamageType.Explosion) == executed,
            "Round ends during the delay: the pending aftershock is dropped, it never executes");
        StartRound(48);
        Respawn();
        // Havuz: ilk patlamada ölen bitki nesnesi başka hücrede yeniden doğsa da eski iş ona vurmaz; iş hücreye bakar.
        SetF(PlantAt(0, 1), "currentHealth", 10);
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(Pos(0, 0));
        Require(Dead(0, 1) && Echoes.Pending == 1, "Pool test: the first explosion kills the neighbour at (0,1); the aftershock is pending");
        RespawnAt(2, 0);   // artçı alanının dışındaki hücre: havuzdaki nesne burada yeniden kullanılabilir
        RespawnAt(0, 1);   // artçı alanının içindeki hücre: yeni bir bitki
        return artci.echoDelay + .8;
    }

    static double AftershockReuseResult()
    {
        var artci = Break("artci_patlama");
        int echo = BehaviorEchoes.ScaleDamage(firstDamage, artci.echoDamage);
        Require(Echoes.Pending == 0 && Lost(2, 0) == 0 && Lost(0, 1) == echo && Lost(1, 0) == firstDamage + echo,
            $"Targets are evaluated at aftershock time by cell: the plant respawned outside the area is untouched, the one now standing inside the area is hit once ({echo}); no stale plant reference is used");
        ClearTestMods();
        return .05;
    }

    // ---------------------------------------------------------------- 15–16) Çifte Akım
    static int electricFirst, skippedBefore;

    static double ElectricTrigger()
    {
        var cifte = Break("cifte_akim");
        Boss.ClearAll();
        ClearField();
        Strike(40f, .4f);
        var source = Place(Only(Plant("Grass")), 0, 0, Tile("Electric", "Legendary"), 1f);
        Place(Only(Plant("Grape")), 1, 1, Tile("Electric", "Legendary"), 1f);   // çaprazda, elektrik şansı %100: zincir denemesi
        Place(Only(Plant("Grape")), 2, 2);
        Place(Only(Plant("Grape")), -1, -1);
        Place(Only(Plant("Grape")), 1, 0);                                       // çapraz değil
        Respawn();
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(Pos(0, 0));
        Require(Lost(1, 1) == 40 && Lost(2, 2) == 40 && Lost(-1, -1) == 40 && Lost(1, 0) == 0 && Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Electric) == 0,
            "Without the reward: one electric wave, 40 damage on the diagonal cells, nothing scheduled");
        Grant(cifte);
        Respawn();
        electricFirst = source.GetBehaviorDamage(40, DamageType.Electric);
        int second = BehaviorEchoes.ScaleDamage(electricFirst, cifte.echoDamage);
        SetF(PlantAt(1, 1), "currentHealth", electricFirst + second);            // ikinci dalgada ölür
        SetF(PlantAt(-1, -1), "currentHealth", 5);                               // ilk dalgada ölür
        SetF(PlantAt(0, 0), "currentHealth", 1);
        triggersBefore = HarvestBehaviorStats.Triggered(DamageType.Electric);
        // Görsel havuzu dolu: yalnız çizim atlanır.
        SetF(HarvestBehaviorManager.Instance, "maxElectricBursts", 0);
        skippedBefore = HarvestBehaviorManager.Instance.SkippedElectricVisuals;
        Attack(Pos(0, 0));
        harvestsBefore = harvests;
        Require(Lost(2, 2) == electricFirst && Dead(-1, -1) && !Dead(1, 1) && Lost(1, 0) == 0 && Echoes.Pending == 1 && Echoes.Scheduled(DamageType.Electric) == 1 &&
                HarvestBehaviorManager.Instance.SkippedElectricVisuals == skippedBefore + 1,
            $"With the reward: the first wave hits for {electricFirst} (visual pool full: only the drawing is skipped); one second wave is scheduled");
        return cifte.echoDelay + .8;
    }

    static double ElectricResult()
    {
        var cifte = Break("cifte_akim");
        int second = BehaviorEchoes.ScaleDamage(electricFirst, cifte.echoDamage);
        Require(Echoes.Pending == 0 && Echoes.Executed(DamageType.Electric) == 1 && Lost(2, 2) == electricFirst + second && Lost(1, 0) == 0,
            $"After {cifte.echoDelay} s the second wave uses the same diagonal geometry from the same planter and hits for {second} = {electricFirst} × {cifte.echoDamage}");
        Require(Dead(1, 1) && Echoes.Kills(DamageType.Electric) == 1 && Echoes.Hits(DamageType.Electric) == 2 && HarvestBehaviorStats.Triggered(DamageType.Electric) == triggersBefore + 1 &&
                Echoes.Scheduled(DamageType.Electric) == 1,
            "The second wave rolls no new chance and starts no chain: the planter it harvested (electric chance 100 %) triggers nothing; each living target was hit once");
        Require(harvests == harvestsBefore + 1, "The plant killed by the first wave is not harvested / rewarded again: the second wave produced exactly one new harvest");
        Require(HarvestBehaviorManager.Instance.SkippedElectricVisuals == skippedBefore + 2, "Second wave with the visual pool full: damage applied, only the drawing skipped");
        SetF(HarvestBehaviorManager.Instance, "maxElectricBursts", 8);
        // Round sonu: bekleyen ikinci dalga düşer.
        Respawn();
        SetF(PlantAt(0, 0), "currentHealth", 1);
        int dropped = Echoes.Dropped(DamageType.Electric);
        Attack(Pos(0, 0));
        Require(Echoes.Pending == 1, "Second wave scheduled");
        EndRoundQuiet();
        Require(Echoes.Pending == 0 && Echoes.Dropped(DamageType.Electric) == dropped + 1 && Echoes.Executed(DamageType.Electric) == 1, "Round ends during the delay: the pending second wave is dropped");
        ClearTestMods();
        return .05;
    }

    // ---------------------------------------------------------------- 17) Hasat Ritmi
    static double Rhythm()
    {
        var ritim = Break("hasat_ritmi");
        Boss.ClearAll();
        RunPower.Rhythm.Clear();
        StartRound(49);
        ClearField();
        var rhythm = RunPower.Rhythm;
        Strike(40f, .4f);
        Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Legendary"), 1f);
        Place(Only(Plant("Grass")), 1, 0);
        Place(Only(Plant("Grape")), -1, 0);
        Place(Only(Plant("Grass")), 0, -2);
        Respawn();
        void Kill(int dx, int dz) { RespawnAt(dx, dz); SetF(PlantAt(dx, dz), "currentHealth", 1); Attack(Pos(dx, dz)); }
        // Ödül yok: sayaç çalışmaz, saldırı bağlamı nötr.
        Kill(0, -2);
        RefreshHud();
        Require(!rhythm.Enabled && rhythm.Count == 0 && !rhythm.Ready && !rhythm.Next().Empowered && Hud.RhythmText == null, "Without the reward nothing is counted and attacks are neutral");
        Grant(ritim);
        int n = ritim.rhythmHarvests;
        RefreshHud();
        Require(rhythm.Enabled && rhythm.Threshold == n && Strip(Hud.RhythmText) == $"Hasat Ritmi 0 / {n}", "Reward taken: HUD shows the counter: " + Strip(Hud.RhythmText));
        // Doğrudan hasat sayılır; aynı vuruşun tetiklediği patlamanın öldürdüğü bitki sayılmaz.
        RespawnAt(0, 0); RespawnAt(1, 0);
        SetF(PlantAt(0, 0), "currentHealth", 1); SetF(PlantAt(1, 0), "currentHealth", 1);
        harvestsBefore = harvests;
        Attack(Pos(0, 0));
        Require(Dead(0, 0) && Dead(1, 0) && harvests == harvestsBefore + 2 && rhythm.Count == 1 && !rhythm.Ready,
            "One direct harvest whose explosion harvests a second plant: the counter goes up by 1 (behavior harvests do not count)");
        for (int i = 0; i < n - 2; i++) Kill(0, -2);
        Require(rhythm.Count == n - 1 && !rhythm.Ready, $"Counter {rhythm.Count} / {n} after {n - 1} direct harvests");
        // Sayaç round'lar arasında korunur.
        EndRoundQuiet(); StartRound(49);
        Respawn();
        Require(rhythm.Count == n - 1 && !rhythm.Ready, "The counter is kept between rounds");
        // Eşiği aşan aynı vuruş: tek hak, fazlası birikmez.
        Strike(100000f, 3f);
        Attack(Pos(0, 0));
        Require(rhythm.Ready && rhythm.Count == 0 && rhythm.Charges == 1, "A swing with several direct harvests crosses the threshold: exactly one charge is ready, the surplus is not banked");
        float radius = 1.0f;
        Strike(40f, radius);
        // Gösterge: HUD; imleç halkası gerçek (büyümüş) temas alanında ve vurgulu.
        RefreshHud();
        var player = Player; var line = F<LineRenderer>(player, "radiusIndicator");
        if (F<GridSystem>(player, "gridSystem") == null) SetF(player, "gridSystem", Grid);
        Vector3 c = Pos(0, 0);
        Call(player, "UpdateRadiusVisual", c);
        var points = new Vector3[line.positionCount]; line.GetPositions(points);
        float ring = points.Average(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(c.x, c.z)));
        Require(Strip(Hud.RhythmText).Contains("HAZIR") && player.ShowsChargedCursor && Near(ring, radius * ritim.rhythmRadius, .01f) && Same(line.startColor, new Color(1f, .84f, .25f)),
            $"Ready: HUD says \"{Strip(Hud.RhythmText)}\"; the cursor ring is highlighted and drawn at the empowered contact radius {ring:0.###} = {radius} × {ritim.rhythmRadius}");
        Require(Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), radius), "The general AreaRadius stat is untouched while the charge is ready");
        Capture("K36_Rhythm_Ready", true);
        // Boş savuruş hakkı harcamaz.
        ClearField();
        Attack(c);
        Require(rhythm.Ready && rhythm.EmpoweredAttacks == 0, "A swing that touches no living plant does not spend the charge");
        // Güçlendirilmiş vuruş. Hücre 2 birim; nişan merkezden +1,6: (0,0) yüzeyi 0,6, (1,0) içinde, (2,0) yüzeyi 1,4 uzakta.
        // Normal yarıçap 1,0 iki hücreye, güçlü yarıçap (×1,4 ve üstü) üç hücreye değer.
        Place(Only(Plant("Grape")), 0, 0); Place(Only(Plant("Grape")), 1, 0); Place(Only(Plant("Grape")), 2, 0);
        Respawn();
        Vector3 aim = c + new Vector3(1.6f, 0f, 0f);
        // Sapma 0,85–1,15: normal vuruş 34–46, güçlendirilmiş vuruş (×oran) ayrı bir aralıkta.
        int lowNormal = Mathf.RoundToInt(40f * .85f), highNormal = Mathf.RoundToInt(40f * 1.15f);
        int lowStrong = Mathf.RoundToInt(40f * .85f * ritim.rhythmDamage) - 1, highStrong = Mathf.RoundToInt(40f * 1.15f * ritim.rhythmDamage) + 1;
        Attack(aim);
        var hits = new[] { Lost(0, 0), Lost(1, 0), Lost(2, 0) };
        Require(lowStrong > highNormal && hits.All(h => h >= lowStrong && h <= highStrong) && Near(player.LastAttackRadius, radius * ritim.rhythmRadius),
            $"Empowered attack: three cells reached with radius {player.LastAttackRadius:0.##} (the normal radius reaches two), damage {string.Join(" / ", hits)} inside {lowStrong}–{highStrong} (normal hits are {lowNormal}–{highNormal})");
        Require(!rhythm.Ready && rhythm.Count == 0 && rhythm.EmpoweredAttacks == 1 && !rhythm.Next().Empowered && Near(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), radius),
            "The charge is spent by that one attack; no extra attack is produced and the stat is unchanged");
        // Sıradaki normal saldırı: eski yarıçap ve hasar.
        Respawn();
        Attack(aim);
        var normal = new[] { Lost(0, 0), Lost(1, 0) };
        Require(Lost(2, 0) == 0 && normal.All(h => h >= lowNormal && h <= highNormal) && Near(player.LastAttackRadius, radius),
            $"The next attack is normal again: the third cell is out of reach, damage {string.Join(" / ", normal)} without the multiplier");
        // Güçlendirilmiş vuruşun hasatları sayacı doldurmaz.
        void Charge()
        {
            ClearField();
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Place(Only(Plant("Grass")), x, z);
            Respawn();
            Strike(100000f, 3f);
            Attack(c);
        }
        Charge();
        Require(rhythm.Ready && rhythm.Count == 0 && rhythm.Charges == 2, "Nine direct harvests in one normal swing: one charge ready again");
        Respawn();
        harvestsBefore = harvests;
        Attack(c);
        Require(harvests == harvestsBefore + 9 && !rhythm.Ready && rhythm.Count == 0 && rhythm.EmpoweredAttacks == 2, "The empowered attack's own harvests (nine) do not fill the counter");
        // Tek hedef, aynı zar: çarpan sapmayla birlikte tek çarpımda ve tek yuvarlamada.
        Charge();
        ClearField();
        Strike(40f, .4f);
        Place(Only(Plant("Grape")), 0, 0);
        Respawn();
        UnityEngine.Random.InitState(11); float roll = UnityEngine.Random.Range(.85f, 1.15f); UnityEngine.Random.InitState(11);
        Attack(c);
        Require(rhythm.Charges == 3 && rhythm.EmpoweredAttacks == 3 && Lost(0, 0) == Mathf.Max(1, Mathf.RoundToInt(40f * roll * ritim.rhythmDamage)),
            $"Single target, known roll: {Lost(0, 0)} = round(40 × {roll:0.###} × {ritim.rhythmDamage}) — one multiplication, one rounding");
        // Davranış hasarı ve alanı güçlendirmeden etkilenmez: hak hazırken patlama yine 40 ve dört komşu.
        Charge();
        Require(rhythm.Ready && rhythm.Charges == 4, "Charge ready for the behavior check");
        ClearField();
        Strike(40f, .4f);
        Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Legendary"), 1f);
        Place(Only(Plant("Grape")), 0, 1); Place(Only(Plant("Grape")), 1, 1); Place(Only(Plant("Grape")), 0, 2);
        Respawn();
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(c);
        Require(Dead(0, 0) && !rhythm.Ready && rhythm.EmpoweredAttacks == 4 && Lost(0, 1) == 40 && Lost(1, 1) == 0 && Lost(0, 2) == 0,
            "Empowered direct hit that triggers an explosion: the explosion keeps its own damage (40) and its own cells (the bonus does not leak to behaviors)");
        ClearTestMods();
        return .05;
    }

    // ---------------------------------------------------------------- 18) boss paleti ve HUD: her boss, temizlik
    static SegmentEventRuntime Inject(SegmentEventSO boss, int seed)
    {
        var runtime = boss.CreateRuntime(SegmentEventTiming.BossRound(10, 5, seed));
        var context = new SegmentEventContext { GridWidth = GridManager.Instance.GetWidth(), GridHeight = GridManager.Instance.GetHeight(), PlayerRadius = 2f };
        foreach (var ground in OpenCells()) context.OpenCells.Add(ground.GetGridPosition());
        runtime.TryPrepare(context);
        F<List<SegmentEventRuntime>>(Events, "events").Add(runtime);
        SetP(Events, "Version", Events.Version + 1);
        return runtime;
    }

    static double BossPalette()
    {
        // Üç boss kimliği: Don buz mavisi, Sert Kabuk amber, Sis gri-lila.
        var bosses = K1.bossPool.entries.Select(e => e.boss).ToList();
        var don = bosses.OfType<FrostFrontSO>().First(); var shell = bosses.OfType<HardShellSO>().First(); var fog = bosses.OfType<FogSO>().First();
        Require(Hue(BossTheme.Accent(don)) > 190f && Hue(BossTheme.Accent(don)) < 220f && Hue(BossTheme.Accent(shell)) > 15f && Hue(BossTheme.Accent(shell)) < 45f &&
                Hue(BossTheme.Accent(fog)) > 235f && Hue(BossTheme.Accent(fog)) < 285f && Sat(BossTheme.Accent(fog)) < .3f,
            $"Identity colors: Don {Hex(BossTheme.Accent(don))} ice blue, Sert Kabuk {Hex(BossTheme.Accent(shell))} amber, Sis {Hex(BossTheme.Accent(fog))} grey-lilac");
        foreach (var legacy in AssetDatabase.FindAssets("t:SegmentEventSO", new[] { "Assets/ScriptableObjects/SegmentEvents" }).Select(g => AssetDatabase.LoadAssetAtPath<SegmentEventSO>(AssetDatabase.GUIDToAssetPath(g))))
        {
            var match = legacy is FrostFrontSO ? (SegmentEventSO)don : legacy is HardShellSO ? shell : legacy is FogSO ? fog : null;
            if (match == null) continue;
            if (!Same(BossTheme.Accent(legacy), BossTheme.Accent(match))) throw new Exception("boss color differs: " + AssetDatabase.GetAssetPath(legacy));
            Note($"boss rengi · {AssetDatabase.GetAssetPath(legacy)}: {Hex(BossTheme.Accent(legacy))}");
        }
        Require(true, "Every boss asset (older profiles too) carries the identity color of its boss type");
        // Her boss için HUD: önizleme satırı, aktif kâğıt ve bitişte sıfırlama (yürütücüye enjekte edilen olaylarla).
        ClearField();
        var list = F<List<SegmentEventRuntime>>(Events, "events");
        foreach (var e in list) e.Finish();
        list.Clear(); SetP(Events, "Active", null);
        Color previous = Color.clear;
        foreach (var boss in new SegmentEventSO[] { shell, fog, don })
        {
            var runtime = Inject(boss, 77);
            RefreshHud();
            if (!Same(Hud.EventLineColor, BossTheme.Ink(boss)) || !Same(Hud.PaperColor, BossTheme.Paper(null)) || Same(Hud.EventLineColor, previous)) throw new Exception("preview HUD color for " + boss.displayName);
            runtime.Activate(); SetP(Events, "Active", runtime);
            SetP(Events, "Version", Events.Version + 1);
            RefreshHud();
            if (!Same(Hud.PaperColor, BossTheme.Paper(boss)) || !Same(Hud.EventLineColor, BossTheme.Ink(boss))) throw new Exception("active HUD color for " + boss.displayName);
            Markers.SendMessage("LateUpdate");
            if (runtime.ZoneCells != null && runtime.ZoneCells.Count > 0 && !Same(Markers.ShownColor, BossTheme.Accent(boss))) throw new Exception("outline color for " + boss.displayName);
            Note($"HUD · {boss.displayName}: kâğıt {Hex(Hud.PaperColor)}, satır {Hex(Hud.EventLineColor)}");
            Capture("K36_HUD_" + (boss is HardShellSO ? "SertKabuk" : boss is FogSO ? "Sis" : "Don"), true);
            previous = Hud.EventLineColor;
            runtime.Finish(); SetP(Events, "Active", null); list.Clear();
            SetP(Events, "Version", Events.Version + 1);
            RefreshHud();
            if (!Same(Hud.PaperColor, BossTheme.Paper(null)) || Hud.EventLineText != null) throw new Exception("HUD not reset after " + boss.displayName);
        }
        Require(true, "HUD for each boss in turn (Sert Kabuk → Sis → Don): preview line and active paper follow the current boss; after each boss the paper is neutral again and the line is gone");
        Color a = BossTheme.Paper(shell), b = BossTheme.Paper(don), f = BossTheme.Paper(fog);
        Require(a.r > a.b + .08f && b.b > b.r + .08f && f.b > f.g && !Same(a, b, .04f) && !Same(b, f, .04f) && !Same(a, f, .04f),
            $"Paper tints differ per boss: Sert Kabuk {Hex(a)} warm, Don {Hex(b)} cold, Sis {Hex(f)} lilac-grey");
        // Menüye dönüş / yeniden başlatma yolu (ClearAll): aktif boss'un rengi kalmaz.
        var last = Inject(don, 78);
        last.Activate(); SetP(Events, "Active", last); SetP(Events, "Version", Events.Version + 1);
        RefreshHud(); Markers.SendMessage("LateUpdate");
        bool shown = Same(Hud.PaperColor, BossTheme.Paper(don)) && Markers.ShownCells.Count > 0;
        Events.ClearAll();
        RefreshHud(); Markers.SendMessage("LateUpdate");
        Require(shown && Same(Hud.PaperColor, BossTheme.Paper(null)) && Hud.EventLineText == null && Markers.ShownCells.Count == 0 && !Markers.OutlineRenderer.enabled,
            "Clean-up path used by restart and return to menu: paper neutral, boss line gone, zone outline hidden");
        return .05;
    }

    // ---------------------------------------------------------------- 19–20) ağaçta başlangıç erişimi (gerçek satın almalarla)
    static readonly Color32 Bought = new Color32(125, 223, 162, 255), Buyable = new Color32(255, 213, 119, 255), DotFilled = new Color32(255, 207, 87, 255);
    static int kivilcimBeforeRestart;
    static RenderTexture cameraTargetBefore;
    static readonly List<Renderer> hiddenWorld = new();
    static SkillTreeUI TreeUi => Object.FindFirstObjectByType<SkillTreeUI>(FindObjectsInactive.Include);
    static SkillNodeUI Slot(SkillNodeSO node) => F<List<SkillNodeUI>>(TreeUi, "nodeUIs").First(n => n != null && n.Node == node);
    static Color Face(SkillNodeUI slot) => F<ComicPopupPlate>(slot, "comicFace").color;
    static bool Clickable(SkillNodeUI slot) => F<Button>(slot, "button").interactable;
    static int ActiveDots(SkillNodeUI slot) => F<List<Image>>(slot, "tierDots").Count(d => d.gameObject.activeSelf);
    static int FilledDots(SkillNodeUI slot) => F<List<Image>>(slot, "tierDots").Count(d => d.gameObject.activeSelf && Same(d.color, DotFilled));
    static int[] Amounts() => Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().Select(Bank.GetResourceAmount).ToArray();

    // Bağlantı çizgisi: 0 çizilmiyor, 1 ok (açık uçtan alınmamış uca), 2 düz çizgi (iki uç da açık).
    static int Link(SkillNodeSO from, SkillNodeSO to)
    {
        foreach (object link in F<System.Collections.IList>(TreeUi, "connections"))
        {
            var type = link.GetType();
            var a = (SkillNodeUI)type.GetField("a").GetValue(link); var b = (SkillNodeUI)type.GetField("b").GetValue(link);
            if (a == null || b == null || a.Node != from || b.Node != to) continue;
            var root = (RectTransform)type.GetField("root").GetValue(link); var wing = (Image)type.GetField("wingA").GetValue(link);
            return !root.gameObject.activeSelf ? 0 : wing.gameObject.activeSelf ? 1 : 2;
        }
        throw new Exception($"no connection {from.name} -> {to.name}");
    }

    // Ağacı verilen ızgara noktasına ortalar; açılış animasyonlarını bitmiş hâle getirir (ekran görüntüsü için).
    static void FocusTree(Vector2 gridFocus, float zoom)
    {
        var ui = TreeUi;
        Canvas.ForceUpdateCanvases();
        ui.SendMessage("LateUpdate");
        Call(ui, "RefreshAll");
        var content = F<RectTransform>(ui, "content");
        SetF(ui, "zoom", zoom); content.localScale = Vector3.one * zoom;
        content.anchoredPosition = -gridFocus * 150f * zoom;
        foreach (var slot in F<List<SkillNodeUI>>(ui, "nodeUIs").Where(s => s != null && s.gameObject.activeSelf))
        { slot.gameObject.SetActive(false); slot.gameObject.SetActive(true); }
        Canvas.ForceUpdateCanvases();
    }

    // Batch'te imleç yok: düğümün gerçek tooltip'i (kendi prefab'ı + FillTooltip) ekrana elle konur.
    static ComicPopupView Tooltip(SkillNodeUI slot, Vector2 at, List<GameObject> shown)
    {
        var canvas = TreeUi.GetComponentInParent<Canvas>().rootCanvas;
        var go = Object.Instantiate(slot.GetTooltipPrefab(), canvas.transform, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = at;
        go.SetActive(true); shown.Add(go);
        slot.FillTooltip(go);
        return go.GetComponent<ComicPopupView>();
    }
    static string Caption(ComicPopupView view) => F<TMP_Text>(view, "subtitle").text;
    static List<string> Lines(ComicPopupView view) => F<List<TMP_Text>>(view, "lines").Where(t => t.gameObject.activeSelf).Select(t => Strip(t.text)).ToList();

    static void OpenTree()
    {
        ClearTestMods();
        if (State == GameStates.Round) EndRoundQuiet();
        Object.FindFirstObjectByType<UIManager>().OpenSkillShop();
        if (State != GameStates.Shop || !TreeUi.gameObject.activeInHierarchy) throw new Exception("skill tree did not open: " + State);
        Call(TreeUi, "RefreshAll");
        // Ekran görüntüsü 1920×1080 dokuya alınır; canvas ölçeği bir sonraki karede bu boyuta oturur.
        Capture(null, true);
        cameraTargetBefore = Cam.targetTexture; Cam.targetTexture = target;
        // Oyunda arayüz her şeyin üstünde çizilir. Burada canvas kameradan çizildiği için sahadaki eski test efektleri panelin
        // üstüne çıkıyor; ağaç görüntülerinde saha gizlenir (yalnız görüntü için, CloseTree geri açar).
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (renderer.enabled && renderer.GetComponentInParent<Canvas>() == null) { renderer.enabled = false; hiddenWorld.Add(renderer); }
    }

    static void CloseTree()
    {
        foreach (var renderer in hiddenWorld) if (renderer != null) renderer.enabled = true;
        hiddenWorld.Clear();
        Cam.targetTexture = cameraTargetBefore;
    }

    static double TreeAccess()
    {
        // Run sonuna iki Kıvılcım bırakılır: yeniden başlatmada (aşağıda, yeni profil yüklemesi) taşınmamalı.
        ClearTestMods();
        var kivilcim = Small("kivilcim");
        Grant(kivilcim); Grant(kivilcim);
        kivilcimBeforeRestart = Boss.Stacks(kivilcim);
        Require(kivilcimBeforeRestart == 2 && Stats.GlobalModifiers.Count(m => m.statType == StatType.ExplosionChance && m.target == StatTarget.Planter) == 2,
            "Two Kıvılcım copies are left active at the end of this run (they must not carry into the next run)");

        var explosive = Node("Patlayıcı Kartlar"); var electric = Node("Çapraz Elektrik Kartları"); var tornado = Node("Tornado Kartları"); var boomerang = Node("Bumerang Orak Kartları");
        var grid1 = Node("Grid Genişleme I"); var planter13 = Node("1×3 Saksı");
        var unlocks = UnlockManager.Instance;
        OpenTree();
        Require(true, "Skill tree opened the way the player opens it (round end → skill shop)");
        Require(Tree.GetCurrentLevel(planter13) == 0 && new[] { explosive, electric, tornado, boomerang }.All(n => !Slot(n).gameObject.activeSelf),
            "Before 1×3 Saksı is bought none of the four behavior nodes is shown in the tree (their place in the chain is kept)");
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) Bank.AddResource(type, 5000);
        Require(Tree.TryUpgrade(grid1) && Tree.TryUpgrade(planter13) && Tree.GetCurrentLevel(grid1) == 1 && grid1.tiers.Count == 2 && Tree.IsMaxLevel(planter13),
            "Real purchases: Grid Genişleme I (first of two tiers) and 1×3 Saksı (its only tier)");
        Call(TreeUi, "RefreshAll");
        var sExp = Slot(explosive); var sEle = Slot(electric); var sTor = Slot(tornado); var sBoo = Slot(boomerang); var sP13 = Slot(planter13); var sGrid = Slot(grid1);
        // Başlangıç erişimi
        foreach (var (slot, node) in new[] { (sExp, explosive), (sEle, electric) })
            if (!slot.gameObject.activeSelf || !slot.ShowsStartingAccess || !Same(Face(slot), SkillNodeUI.StartingAccessColor) || Same(Face(slot), Bought, .08f) || ActiveDots(slot) != 0 || Clickable(slot) ||
                Tree.GetCurrentLevel(node) != 0 || Tree.IsMaxLevel(node)) throw new Exception("starting access display: " + node.name);
        Require(true, $"Starting access (Patlayıcı Kartlar, Çapraz Elektrik Kartları): label \"{SkillNodeUI.StartingAccessLabel}\", own face color {Hex(Face(sExp))} (bought nodes are {Hex(Bought)}), no tier dots, purchase level 0, not clickable");
        var label = sExp.GetComponentsInChildren<TMP_Text>().FirstOrDefault(t => t.text == SkillNodeUI.StartingAccessLabel);
        Canvas.ForceUpdateCanvases();
        if (label != null) label.ForceMeshUpdate();
        Require(label != null && label.textInfo.characterCount == SkillNodeUI.StartingAccessLabel.Length && label.color.a > .9f && !label.raycastTarget && label.rectTransform.rect.height > 10f,
            $"The label is laid-out text on the node, not only a color: \"{label?.text}\" ({label?.textInfo.characterCount} glyphs, font size {label?.fontSize:0.#})");
        int[] before = Amounts();
        Require(!Tree.CanUpgrade(explosive) && !Tree.TryUpgrade(explosive) && !Tree.TryUpgrade(electric) && Amounts().SequenceEqual(before) && Tree.GetCurrentLevel(explosive) == 0 && Tree.GetCurrentLevel(electric) == 0,
            "A rich player is not charged for the granted access: no purchase, no resource spent, no purchase level written");
        // Satın alma seviyesi ve tamamlanma
        Require(sP13.gameObject.activeSelf && Same(Face(sP13), Bought) && !sP13.ShowsStartingAccess && ActiveDots(sP13) == 1 && FilledDots(sP13) == 1,
            "Completed purchase (1×3 Saksı): green, its tier dot filled, no starting-access label");
        Require(sGrid.gameObject.activeSelf && Same(Face(sGrid), Buyable) && !sGrid.ShowsStartingAccess && ActiveDots(sGrid) == 2 && FilledDots(sGrid) == 1 && Clickable(sGrid),
            "Purchase level (Grid Genişleme I, 1 of 2 tiers): one of two dots filled, still purchasable, not shown as completed");
        Require(sTor.gameObject.activeSelf && Same(Face(sTor), Buyable) && !sTor.ShowsStartingAccess && ActiveDots(sTor) == 1 && FilledDots(sTor) == 0 && Clickable(sTor) &&
                !unlocks.IsUnlocked(UnlockType.TileBehavior_Tornado) && !Tile("Tornado", "Common").IsAvailableInCardPool,
            "Purchasable (Tornado Kartları): shown with an empty tier dot and no label; tornado cards are NOT in the pool until it is bought (the node being visible is not an unlock)");
        Require(!sBoo.gameObject.activeSelf && !Tree.CanUpgrade(boomerang) && !unlocks.IsUnlocked(UnlockType.TileBehavior_Boomerang) && !Tile("Boomerang", "Common").IsAvailableInCardPool,
            "Bumerang Orak Kartları stays hidden and locked until Tornado Kartları is bought");
        Require(Link(planter13, explosive) == 2 && Link(explosive, electric) == 2 && Link(explosive, tornado) == 1 && Link(tornado, boomerang) == 0,
            "Connections: plain line into and between the two starting nodes, arrow from Patlayıcı Kartlar to the not-yet-bought Tornado Kartları, nothing drawn beyond it");
        return 1.2;
    }

    static double TreeAccessDisplay()
    {
        var explosive = Node("Patlayıcı Kartlar"); var electric = Node("Çapraz Elektrik Kartları"); var tornado = Node("Tornado Kartları"); var boomerang = Node("Bumerang Orak Kartları");
        var planter13 = Node("1×3 Saksı");
        var unlocks = UnlockManager.Instance;
        var sExp = Slot(explosive); var sTor = Slot(tornado); var sBoo = Slot(boomerang); var sP13 = Slot(planter13);
        FocusTree(new Vector2(8f, 2.6f), 1f);
        Capture("K371_Agac_KirilmaV1", true);
        // Tooltip: üç durumun metni.
        FocusTree(new Vector2(9.6f, 2.6f), 1f);
        var shown = new List<GameObject>();
        var tipStart = Tooltip(sExp, new Vector2(690f, 285f), shown); var tipBuy = Tooltip(sTor, new Vector2(690f, -25f), shown); var tipDone = Tooltip(sP13, new Vector2(690f, -335f), shown);
        var startLines = Lines(tipStart); var buyLines = Lines(tipBuy);
        Require(Caption(tipStart) == SkillNodeUI.StartingAccessLabel && startLines.Contains("ERİŞİM") && startLines.Any(l => l.Contains("başlangıçtan açık")) && startLines.Any(l => l.Contains("satın alınmadı")) &&
                !startLines.Any(l => l.Contains("MALİYET") || l.Contains("KADEME")),
            "Tooltip of a starting node: caption \"" + Caption(tipStart) + "\", says it was not bought, no tier and no cost section — " + string.Join(" / ", startLines).Replace((char)10, (char)32));
        Require(Caption(tipBuy).Contains("0 / 1") && buyLines.Contains("YÜKSELTME MALİYETİ") && buyLines.Contains($"{tornado.tiers[0].cost} {tornado.tiers[0].costType}") && !buyLines.Any(l => l.Contains("başlangıçtan")),
            "Tooltip of a purchasable node: tier 0 / 1 and its price — " + string.Join(" / ", buyLines).Replace((char)10, (char)32));
        Require(Caption(tipDone).Contains("MAX") && !Lines(tipDone).Any(l => l.Contains("başlangıçtan")), "Tooltip of a completed node: " + Caption(tipDone));
        Canvas.ForceUpdateCanvases();
        Capture("K371_Agac_KirilmaV1_Tooltip", true);
        foreach (var go in shown) Object.DestroyImmediate(go);
        FocusTree(new Vector2(8f, 3.3f), 2f);
        Capture("K371_Agac_KirilmaV1_Yakin", true);

        // Gerçek satın alma: Tornado normal bir alımdır; başlangıç düğümleri seviye 0'da kalır.
        var costType = tornado.tiers[0].costType; int owned = Bank.GetResourceAmount(costType);
        Require(Tree.TryUpgrade(tornado) && Bank.GetResourceAmount(costType) == owned - tornado.tiers[0].cost && unlocks.IsUnlocked(UnlockType.TileBehavior_Tornado) &&
                Tile("Tornado", "Common").IsAvailableInCardPool && Tree.GetCurrentLevel(tornado) == 1 && !Tree.TryUpgrade(tornado),
            $"Tornado Kartları is an ordinary purchase: {tornado.tiers[0].cost} {costType} charged once, level 1/1, tornado cards enter the pool only now");
        Call(TreeUi, "RefreshAll");
        Require(Same(Face(sTor), Bought) && sBoo.gameObject.activeSelf && Same(Face(sBoo), Buyable) && !sBoo.ShowsStartingAccess && Link(explosive, tornado) == 2 && Link(tornado, boomerang) == 1 &&
                !unlocks.IsUnlocked(UnlockType.TileBehavior_Boomerang) && sExp.ShowsStartingAccess && Tree.GetCurrentLevel(explosive) == 0 && Tree.GetCurrentLevel(electric) == 0,
            "After the purchase: Tornado Kartları green, Bumerang Orak Kartları appears as purchasable and is still locked; the two starting nodes keep level 0 and their label");
        FocusTree(new Vector2(9f, 2.6f), 1f);
        Capture("K371_Agac_KirilmaV1_TornadoAlindi", true);

        // Karma düğüm (TEST VERİSİ, oyunda böyle bir düğüm yok): kilidi başlangıçtan açık, stat veren iki kademesi satılıyor.
        StatModifier Bonus(float v) => Mod(StatType.HarvestDamage, StatTarget.Player, v, ModifierOperation.Flat);
        var mixed = ScriptableObject.CreateInstance<SkillNodeSO>();
        mixed.name = mixed.nodeName = "TEST · karma düğüm";
        mixed.unlockType = UnlockType.TileBehavior_Explosive; mixed.explicitPrerequisites = true; mixed.gridPosition = new Vector2Int(9, 5);
        mixed.tiers = new List<SkillNodeTier>
        {
            new SkillNodeTier { costType = ResourceType.Gold, cost = 10, effects = new List<StatModifier> { Bonus(1f) } },
            new SkillNodeTier { costType = ResourceType.Gold, cost = 20, effects = new List<StatModifier> { Bonus(3f) } },
        };
        var spare = F<List<SkillNodeUI>>(TreeUi, "nodeUIs").First(s => s != null && s.Node == null);
        spare.Bind(mixed); spare.Refresh();
        float damage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player); int gold = Bank.GetResourceAmount(ResourceType.Gold);
        Require(SkillTreeManager.HasStartingAccess(mixed) && !SkillTreeManager.IsGrantedByProfile(mixed) && !Tree.IsDisabledByProfile(mixed) && Tree.CanUpgrade(mixed) &&
                spare.gameObject.activeSelf && spare.ShowsStartingAccess && Same(Face(spare), Buyable) && ActiveDots(spare) == 2 && FilledDots(spare) == 0 && Clickable(spare),
            "Mixed node (test data): access is from the start, but its stat tiers stay purchasable — label shown together with tier dots, clickable, not drawn as a finished starting node");
        var tipMixed = Tooltip(spare, new Vector2(690f, 285f), shown = new List<GameObject>());
        Require(Caption(tipMixed).Contains("0 / 2") && Lines(tipMixed).Contains("10 Gold") && Lines(tipMixed).Any(l => l.Contains("başlangıçtan açık")) && !Lines(tipMixed).Any(l => l.Contains("Son seviyede")),
            "Mixed node tooltip: tier 0 / 2 with its price; the card access is described as open from the start, not as something the last tier adds — " + string.Join(" / ", Lines(tipMixed)).Replace((char)10, (char)32));
        FocusTree(new Vector2(9.6f, 2.6f), 1f);
        Capture("K371_Agac_KarmaDugum_TestVerisi", true);
        foreach (var go in shown) Object.DestroyImmediate(go);
        bool firstTier = Tree.TryUpgrade(mixed); int goldAfterFirst = Bank.GetResourceAmount(ResourceType.Gold); float damageFirst = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        bool secondTier = Tree.TryUpgrade(mixed); float damageSecond = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        Require(firstTier && goldAfterFirst == gold - 10 && damageFirst > damage + .5f && secondTier && Bank.GetResourceAmount(ResourceType.Gold) == gold - 30 && Tree.IsMaxLevel(mixed) &&
                damageSecond > damageFirst + .5f && !Tree.TryUpgrade(mixed) && Bank.GetResourceAmount(ResourceType.Gold) == gold - 30,
            $"Mixed node: both stat tiers are bought at their own price (10 + 20 Gold) and apply their effect (damage {damage:0.##} → {damageFirst:0.##} → {damageSecond:0.##}); nothing is charged twice");
        spare.Refresh();
        Require(Same(Face(spare), Bought) && FilledDots(spare) == 2 && spare.ShowsStartingAccess && unlocks.IsUnlocked(UnlockType.TileBehavior_Explosive),
            "Mixed node completed: green with both dots filled; the starting-access label stays (the access itself was never bought)");
        // Test verisini kaldır.
        Stats.RemoveGlobalModifiers(mixed.tiers[1].effects);
        F<Dictionary<SkillNodeSO, int>>(Tree, "nodeLevels").Remove(mixed);
        F<HashSet<Vector2Int>>(Tree, "unlockedPositions").Remove(mixed.gridPosition);
        spare.Bind(null);
        Object.DestroyImmediate(mixed);
        Call(TreeUi, "RefreshAll");
        Require(Near(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), damage) && spare.Node == null && !spare.gameObject.activeSelf, "Test node removed (stat and tree as before)");
        CloseTree();
        GameManager.Instance.ShowRoundEnd();
        return .2;
    }

    // ---------------------------------------------------------------- 21–25) eski profil ve kapanış
    static double LoadProfile(string name)
    {
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = Profile(name);
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double OldProfile()
    {
        Player.enabled = false;
        Require(State == GameStates.RunSetup && RM.Profile == D1 && RM.ChoicesPerLevel == 1, "GameScene runs Run50_DengeV1: one choice per level");
        Require(!RunPower.Rhythm.Enabled && RunPower.Rhythm.Count == 0 && !RunPower.Rhythm.Ready && RunPower.Rhythm.Charges == 0 && Boss.Taken.Count == 0 && Echoes != null && Echoes.Pending == 0 &&
                Echoes.Scheduled(DamageType.Explosion) == 0 && Echoes.Executed(DamageType.Electric) == 0 && RM.LevelsGained == 0 && RM.PendingCardSelections == 0 && Progress.FirstBehaviorCardRound == 0,
            "New run after restart: rhythm counter and charge, rewards, echo jobs and counters, pending choices are all reset");
        var unlocks = UnlockManager.Instance;
        Require(!unlocks.IsUnlocked(UnlockType.TileBehavior_Explosive) && !unlocks.IsUnlocked(UnlockType.TileBehavior_Electric) && !Tile("Explosive", "Common").IsAvailableInCardPool &&
                !SkillTreeManager.IsGrantedByProfile(Node("Patlayıcı Kartlar")) && !Tree.IsDisabledByProfile(Node("Çapraz Elektrik Kartları")),
            "DengeV1: explosion and electric cards are still locked at run start and their unlock nodes are sold as before");
        Require(Enum.GetValues(typeof(UnlockType)).Cast<UnlockType>().All(t => !unlocks.IsUnlocked(t)) && !Tree.AllNodes.Any(SkillTreeManager.HasStartingAccess) &&
                (D1.balance.startingUnlocks == null || D1.balance.startingUnlocks.Count == 0) && Tree.AllNodes.All(n => Tree.GetCurrentLevel(n) == 0),
            "DengeV1 run start, every unlock type checked: nothing is open from the start and no node has starting access (no leak from Kırılma V1, also right after playing it)");
        // Yeniden başlatma: önceki run'ın iki Kıvılcım'ı taşınmaz; yeni alınan ödül saksının gerçek şansına bir kez uygulanır.
        var spark = Small("kivilcim"); float sparkFactor = 1f + spark.modifiersPerStack[0].value;
        var probe = Place(Only(Plant("Grass")), 1, 1, Tile("Explosive", "Common"), .20f);
        float Chance() => probe.GetFinalStat(StatType.ExplosionChance);
        Require(kivilcimBeforeRestart == 2 && Boss.Stacks(spark) == 0 && !Stats.GlobalModifiers.Any(m => Chances.Contains(m.statType) && m.target == StatTarget.Planter) && Near(Chance(), .20f, 1e-4f),
            "Restart: the two Kıvılcım copies of the previous run are gone (no leftover chance modifier); a 0,20 explosion tile gives 0,20 again");
        int sparkWarnings = baseWarnings;
        Require(Grant(spark) && Near(Chance(), .20f * sparkFactor, 1e-4f) && Stats.GlobalModifiers.Count(m => m.statType == StatType.ExplosionChance && m.target == StatTarget.Planter) == 1,
            $"New run, ONE Kıvılcım: 0,20 → {Chance():0.####} (×{sparkFactor} applied once; not stacked on the previous run's copies)");
        Require(Grant(spark) && Near(Chance(), .20f * sparkFactor * sparkFactor, 1e-4f), $"Second copy: {Chance():0.####}");
        Require(Grant(spark) && Near(Chance(), Mathf.Clamp01(.20f * Mathf.Pow(sparkFactor, 3)), 1e-4f) && !Grant(spark) && baseWarnings == sparkWarnings,
            $"THREE copies (the maximum): 0,20 → {Chance():0.####} = 0,20 × {sparkFactor}³ (×{Mathf.Pow(sparkFactor, 3):0.###}); this figure is the effect of three rewards, not of one. No 'base stat' warning");
        Boss.ClearAll();
        Require(Near(Chance(), .20f, 1e-4f), "Rewards removed again: the chance is back to the tile's own 0,20");
        probe.RemoveSelf(); Cell(1, 1).GetGroundCellCached().ApplyModifier(null);
        Bank.AddResource(ResourceType.Iron, 200); Bank.AddResource(ResourceType.Gold, 300);
        Require(Tree.TryUpgrade(Node("Grid Genişleme I")) && Tree.TryUpgrade(Node("1×3 Saksı")) && Tree.TryUpgrade(Node("Patlayıcı Kartlar")) && unlocks.IsUnlocked(UnlockType.TileBehavior_Explosive),
            "DengeV1: Patlayıcı Kartlar is bought the old way");
        Place(PlanterAsset("1x1"), 0, 0);
        var cards = Cards; int forced = 0;
        UnityEngine.Random.InitState(1);
        for (int n = 0; n < 60; n++) { cards.RefreshCards(); if (cards.GuaranteedBehaviorOffer != null) forced++; }
        Require(forced == 0, "DengeV1: no reserved behavior slot in card offers");
        StartRound(1);
        RefreshHud();
        var upcoming = Events.Upcoming;
        Require(upcoming != null && Same(Hud.PaperColor, BossTheme.Paper(null)) && Same(Hud.EventLineColor, BossTheme.Ink(upcoming.Data)),
            "After restart the HUD starts clean: neutral paper, line in the color of this run's first boss (" + (upcoming != null ? upcoming.Data.displayName : "none") + ")");
        Progress.AddXP(Progress.XPToNextLevel); Progress.AddXP(Progress.XPToNextLevel);
        Require(RM.LevelsGained == 2 && RM.PendingCardSelections == 2 && RM.CardChoicesGranted == 2, "DengeV1: two levels give two choices");
        Call(RM, "EndRound");
        Require(ResolveCards() == 2 && State == GameStates.RoundEnd, "DengeV1: two card choices, then the round summary");
        var pool = D1.bossRewards; var offerList = F<List<BossRewardSO>>(Boss, "offer");
        for (int seed = 1; seed <= 40; seed++)
        {
            SetF(Boss, "offerRound", 25); SetP(Events, "RunSeed", seed);
            Call(Boss, "BuildOffer", pool, 5);
            if (offerList.Any(r => r.IsBreakthrough) || offerList.Count != pool.choices) throw new Exception("DengeV1 offer changed");
        }
        offerList.Clear();
        SetP(Events, "RunSeed", Seed);
        Require(true, "DengeV1: reward offers hold three small rewards, never a breakthrough reward");
        // Patlama: ödül olmadan yankı planlanmaz, ritim sayılmaz.
        StartRound(47);
        ClearField();
        Strike(40f, .4f);
        Place(Only(Plant("Grass")), 0, 0, Tile("Explosive", "Legendary"), 1f);
        Place(Only(Plant("Grape")), 1, 0); Place(Only(Plant("Grape")), 1, 1);
        Respawn();
        SetF(PlantAt(0, 0), "currentHealth", 1);
        Attack(Pos(0, 0));
        Require(Dead(0, 0) && Lost(1, 0) == 40 && Lost(1, 1) == 0 && Echoes.Pending == 0 && Echoes.Scheduled(DamageType.Explosion) == 0 && RunPower.Rhythm.Count == 0,
            "DengeV1: explosion as before (40 to the four neighbours), no aftershock, no rhythm counting");
        ClearTestMods();
        return .8;
    }

    static double OldProfileAfter()
    {
        Require(Lost(1, 0) == 40 && Lost(1, 1) == 0 && Echoes.Executed(DamageType.Explosion) == 0, "DengeV1: nothing delayed happened after the explosion");
        ClearField();
        OpenTree();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) Bank.AddResource(type, 5000);
        Call(TreeUi, "RefreshAll");
        return 1.2;
    }

    // Eski profil: Patlayıcı Kartlar eski yolla satın alındı (OldProfile). Aynı ağaç görünümünde başlangıç erişimi izi yok.
    static double OldTreeDisplay()
    {
        var explosive = Node("Patlayıcı Kartlar"); var electric = Node("Çapraz Elektrik Kartları"); var tornado = Node("Tornado Kartları"); var boomerang = Node("Bumerang Orak Kartları");
        var planter13 = Node("1×3 Saksı");
        var unlocks = UnlockManager.Instance;
        var sExp = Slot(explosive); var sEle = Slot(electric); var sTor = Slot(tornado);
        Require(Tree.GetCurrentLevel(explosive) == 1 && Same(Face(sExp), Bought) && !sExp.ShowsStartingAccess && ActiveDots(sExp) == 1 && FilledDots(sExp) == 1,
            "DengeV1 tree: Patlayıcı Kartlar bought the old way is a completed purchase (green, dot filled, level 1/1), without a starting-access label");
        Require(F<List<SkillNodeUI>>(TreeUi, "nodeUIs").Where(s => s != null && s.Node != null).All(s => !s.ShowsStartingAccess && !Same(Face(s), SkillNodeUI.StartingAccessColor)),
            "DengeV1 tree: no node anywhere carries the starting-access label or color");
        Require(sEle.gameObject.activeSelf && Same(Face(sEle), Buyable) && Clickable(sEle) && !unlocks.IsUnlocked(UnlockType.TileBehavior_Electric) && sTor.gameObject.activeSelf && Same(Face(sTor), Buyable) &&
                !unlocks.IsUnlocked(UnlockType.TileBehavior_Tornado) && !Slot(boomerang).gameObject.activeSelf && Link(planter13, explosive) == 2 && Link(explosive, electric) == 1 && Link(explosive, tornado) == 1,
            "DengeV1 tree: Çapraz Elektrik and Tornado are ordinary purchasable nodes behind the bought Patlayıcı Kartlar (arrows point to them); both still locked");
        FocusTree(new Vector2(9.6f, 2.6f), 1f);
        var shown = new List<GameObject>();
        var tip = Tooltip(sExp, new Vector2(690f, 285f), shown);
        Require(Caption(tip).Contains("MAX") && Lines(tip).Any(l => l.Contains("seçim havuzuna eklendi")) && !Lines(tip).Any(l => l.Contains("başlangıçtan")),
            "DengeV1 tooltip of the bought node: completed purchase text, no 'başlangıçtan açık' wording — " + string.Join(" / ", Lines(tip)).Replace((char)10, (char)32));
        Canvas.ForceUpdateCanvases();
        Capture("K371_Agac_DengeV1", true);
        foreach (var go in shown) Object.DestroyImmediate(go);
        CloseTree();
        return .1;
    }

    static double FinalCheck()
    {
        Require(baseWarnings == 0, "No 'base stat not found' warning in the whole test");
        Require(errors == 0, "No error or exception logged during the test");
        return -1;
    }
}
