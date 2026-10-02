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

// Batch (izole kopya): Bölüm 3.3 — başlangıç seçimi (çiftçi + tırpan), kalıcı kayıt ve görev.
// Kayıt testleri kullanıcının kaydına yazmaz: Logs/MetaSaveTest klasörü kullanılır; batch varsayılanının diske dokunmadığı ayrıca ölçülür.
// Akış: menü → seçim ekranı (kilitli kart) → Tüccar + Dar Kesim run'ı (ekonomi, katsayı ayrımı, yarıçap) → 5 gerçek Legendary hasadı
// (doğrudan, elektrik, çifte ödül) → kayıp → 3 yeniden başlatma → menü → açılmış kart → Seçici Yetiştirici run'ı → sahiplik temizliği → nötr run.
[InitializeOnLoad]
public static class StartLoadoutVerification
{
    const string Key = "StartLoadoutVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Run50_Referans.asset";
    const string Options = "Assets/ScriptableObjects/StartOptions/";
    const float FrameTime = 1f / 30f;
    static readonly List<string> notes = new();
    static int step; static double nextAt;

    static StartLoadoutVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
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
                File.WriteAllLines("Logs/StartLoadoutVerification.txt", new[] { $"RUNNING step {step}" }.Concat(notes));
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
        Time.captureDeltaTime = 0f;
        Application.logMessageReceived -= CountWarnings;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/StartLoadoutVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
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
    static RoundManager RM => RoundManager.Instance;
    static StartLoadoutManager Loadout => StartLoadoutManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static Vector3 Pos(int dx, int dz) => Cell(dx, dz).GetGroundCellCached().transform.position;
    static int Res(ResourceType t) => ResourceManager.Instance.GetResourceAmount(t);
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static PlanterSO PlanterAsset(string n) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {n}.asset");
    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static PlantSO Plant(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    static T Option<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Options + path + ".asset");
    static FarmerSO Bahcivan => Option<FarmerSO>("Farmers/Bahcivan");
    static FarmerSO Tuccar => Option<FarmerSO>("Farmers/Tuccar");
    static FarmerSO Secici => Option<FarmerSO>("Farmers/SeciciYetistirici");
    static ScytheSO Standart => Option<ScytheSO>("Scythes/Standart");
    static ScytheSO DarKesim => Option<ScytheSO>("Scythes/DarKesim");
    static QuestSO Quest => Option<QuestSO>("Quests/Legendary5");
    static string SaveDir => Path.GetFullPath("Logs/MetaSaveTest");
    static string RealPath => Path.Combine(Application.persistentDataPath, MetaSave.FileName);
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z).ToList();
    static PlantHealth PlantAt(int dx, int dz) { var p = Cell(dx, dz).GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static int CountMod(StatModifier m) => Stats.GlobalModifiers.Count(x => x.Equals(m));

    static void Respawn()
    {
        foreach (var s in Spawners())
        {
            s.RemoveSpawnedPlant(); s.enabled = true;
            Call(s, "TrySpawnPlant");
        }
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

    // Oyuncunun gerçek vuruşu (PlayerController.AttackInRadius), stat'taki yarıçapla.
    static void Attack(Vector3 position) => Call(Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include), "AttackInRadius", position);

    static readonly List<StatModifier> testMods = new();
    static void TestMod(StatModifier m) { Stats.AddGlobalModifier(m); testMods.Add(m); }
    static void ClearTestMods() { foreach (var m in testMods) if (CountMod(m) > 0) Stats.RemoveGlobalModifier(m); testMods.Clear(); }

    // İstenen round'u başlatır; round kendi süresiyle bitmez (Tick), oyuncu elle vurulur.
    static void StartRound(int round)
    {
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        SetP(RM, "CurrentRound", round - 1); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        if (RM.CurrentRound != round || State != GameStates.Round) throw new Exception($"round {round} did not start ({RM.CurrentRound}, {State})");
        SetP(RM, "RemainingTime", 1000f);
        Time.timeScale = 1f;
    }
    static void StartRound50() => StartRound(50);

    // Hasat sayacı (testin kendi, bağımsız): nadirlik ve öldüren hasar türü.
    static readonly List<(PlantRarity rarity, DamageType by)> harvests = new();
    static void CountHarvest(PlantHealth h) { if (h.Data != null) harvests.Add((h.Data.rarity, h.KilledBy)); }
    static int warnings;
    static void CountWarnings(string message, string stack, LogType type) { if (type == LogType.Warning && message.Contains("RemoveGlobalModifier")) warnings++; }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return SaveTests();
            case 1: return MenuLocked();
            case 2: return TuccarEconomy();
            case 3: return DirectVersusBehavior();
            case 4: return Radius();
            case 5: RingShot("DarKesim_Radius"); return .2;
            case 6: return LegendaryQuest();
            case 7: return RunEnd();
            case 8: case 9: case 10: return Restart(i - 7);
            case 11: return MenuReturn();
            case 12: return MenuUnlocked();
            case 13: return SeciciEffects();
            case 14: return Ownership();
            case 15: return Neutral();
            case 16: RingShot("Standart_Radius"); return -1;
            default: return -1;
        }
    }

    // ---------------- 0) kayıt ----------------
    static bool realExisted; static DateTime realTime;

    static double SaveTests()
    {
        Application.logMessageReceived += CountWarnings;
        PlantHealth.AnyHarvested += CountHarvest;
        realExisted = File.Exists(RealPath); realTime = realExisted ? File.GetLastWriteTimeUtc(RealPath) : default;
        MetaSave.UseDirectory(null);
        MetaSave.Load();
        Require(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "Batch mode without an explicit folder: memory only (the player's save is not read)");
        MetaSave.SetSelection(Tuccar, DarKesim);
        Require(File.Exists(RealPath) == realExisted && (!realExisted || File.GetLastWriteTimeUtc(RealPath) == realTime), "Batch default did not write the player's save: " + RealPath);

        if (Directory.Exists(SaveDir)) Directory.Delete(SaveDir, true);
        Directory.CreateDirectory(SaveDir);
        MetaSave.UseDirectory(SaveDir);

        var catalog = StartCatalogSO.Active;
        Require(catalog != null && catalog.farmers.Count == 3 && catalog.scythes.Count == 2 && catalog.quests.Count == 1, "Catalog: 3 farmers, 2 scythes, 1 quest (Geniş Orak and the rest are not listed)");
        var ids = catalog.farmers.Select(f => f.id).Concat(catalog.scythes.Select(s => s.id)).ToList();
        Require(ids.All(id => !string.IsNullOrEmpty(id)) && ids.Distinct().Count() == ids.Count, "Stable unique ids: " + string.Join(", ", ids));
        Require(catalog.defaultFarmer == Bahcivan && catalog.defaultScythe == Standart && Bahcivan.IsNeutral && Standart.IsNeutral && Bahcivan.OpenAtStart && Standart.OpenAtStart,
            "Defaults Bahçıvan + Standart: neutral and always open");
        Require(Tuccar.OpenAtStart && DarKesim.OpenAtStart && !Secici.OpenAtStart && Secici.unlockQuest == Quest && Quest.target == 5 && Quest.counter == QuestCounter.LegendaryHarvestInRun,
            "Open at start: Bahçıvan, Tüccar, Standart, Dar Kesim; Seçici Yetiştirici needs the 5-Legendary quest");
        Require(Mathf.Approximately(Tuccar.harvestResourceMultiplier, 1.25f) && Mathf.Approximately(Tuccar.harvestScoreMultiplier, 0.9f) && Tuccar.modifiers.Count == 0 &&
                Mathf.Approximately(DarKesim.directDamageMultiplier, 1.4f) && DarKesim.modifiers.Count == 1 && DarKesim.modifiers[0].statType == StatType.AreaRadius && Mathf.Approximately(DarKesim.modifiers[0].value, -0.25f) &&
                Secici.modifiers.Count == 2, "Test values from the 3.1 plan (Tüccar ×1,25 / ×0,9; Dar Kesim ×1,4 / radius ×0,75; Seçici +15 points / spawn ×1,15)");

        MetaSave.Load();
        var fresh = StartLoadoutManager.Resolve(catalog);
        Require(MetaSave.LastLoad == MetaSave.LoadResult.Fresh && fresh.farmer == Bahcivan && fresh.scythe == Standart && !fresh.fallback, "No save: Bahçıvan + Standart, no fallback flag");
        Require(MetaSave.IsUnlocked(Bahcivan) && MetaSave.IsUnlocked(Tuccar) && MetaSave.IsUnlocked(Standart) && MetaSave.IsUnlocked(DarKesim) && !MetaSave.IsUnlocked(Secici), "Open / locked state");

        MetaSave.SetSelection(Tuccar, DarKesim);
        Require(File.Exists(MetaSave.FilePath), "Selection written: " + MetaSave.FilePath);
        MetaSave.UseDirectory(SaveDir);
        var loaded = StartLoadoutManager.Resolve(catalog);
        Require(MetaSave.LastLoad == MetaSave.LoadResult.Loaded && loaded.farmer == Tuccar && loaded.scythe == DarKesim && !loaded.fallback, "Reload from disk: Tüccar + Dar Kesim remembered");
        string json = File.ReadAllText(MetaSave.FilePath);
        Require(json.Contains("\"version\": 1") && json.Contains("\"farmer\": \"tuccar\"") && json.Contains("\"scythe\": \"dar_kesim\""), "File has version and stable ids");

        MetaSave.Data.farmer = "secici_yetistirici"; MetaSave.Save(); MetaSave.UseDirectory(SaveDir);
        var locked = StartLoadoutManager.Resolve(catalog);
        Require(locked.farmer == Bahcivan && locked.scythe == DarKesim && locked.fallback, "Locked id in the save → Bahçıvan (scythe kept), fallback flagged");
        MetaSave.Data.farmer = "silinmis_ciftci"; MetaSave.Data.scythe = ""; MetaSave.Data.unlocked.Add("gelecek_icerik"); MetaSave.Save(); MetaSave.UseDirectory(SaveDir);
        var unknown = StartLoadoutManager.Resolve(catalog);
        Require(unknown.farmer == Bahcivan && unknown.scythe == Standart && unknown.fallback && MetaSave.Data.unlocked.Contains("gelecek_icerik"),
            "Unknown / deleted / empty ids → Bahçıvan + Standart; unknown unlocked id kept, not deleted");

        // Bozuk ana dosya, sağlam yedek (File.Replace önceki sürümü .bak yapar; aynı içerikle bir yazım daha).
        MetaSave.Save();
        Require(File.Exists(MetaSave.BackupPath), "Backup exists after replacing writes");
        File.WriteAllText(MetaSave.FilePath, "{ bu bir kayıt değil");
        MetaSave.UseDirectory(SaveDir); MetaSave.Load();
        int quarantined = Directory.GetFiles(SaveDir, "meta.corrupt-*.json").Length;
        Require(MetaSave.LastLoad == MetaSave.LoadResult.FromBackup && quarantined == 1 && MetaSave.Data.unlocked.Contains("gelecek_icerik"), "Corrupt file + backup: loaded from backup, corrupt file kept aside");
        MetaSave.UseDirectory(SaveDir); MetaSave.Load();
        Require(MetaSave.LastLoad == MetaSave.LoadResult.Loaded, "Main file restored from the backup");
        File.Delete(MetaSave.BackupPath);
        File.WriteAllText(MetaSave.FilePath, "");
        MetaSave.UseDirectory(SaveDir); MetaSave.Load();
        Require(MetaSave.LastLoad == MetaSave.LoadResult.CorruptReset && MetaSave.Data.unlocked.Count == 0 && MetaSave.Data.farmer == "" && Directory.GetFiles(SaveDir, "meta.corrupt-*.json").Length >= 1,
            "Empty/corrupt file without backup: defaults, nothing crashes");
        File.WriteAllText(MetaSave.FilePath, "{\"version\": 99, \"farmer\": \"tuccar\", \"scythe\": \"standart\", \"unlocked\": [], \"quests\": []}");
        MetaSave.UseDirectory(SaveDir);
        var newer = StartLoadoutManager.Resolve(catalog);
        string before = File.ReadAllText(MetaSave.FilePath);
        MetaSave.SetSelection(Bahcivan, Standart);
        Require(newer.farmer == Tuccar && File.ReadAllText(MetaSave.FilePath) == before, "Newer save version: read, never overwritten");

        Directory.Delete(SaveDir, true); Directory.CreateDirectory(SaveDir);
        MetaSave.UseDirectory(SaveDir);
        MetaSave.SetSelection(Tuccar, DarKesim);

        // Kesir taşıma: küçük değerlerde bonus ve bedel kaybolmaz.
        var scale = new FractionalScale(); int sum = 0;
        for (int k = 0; k < 10; k++) sum += scale.Apply(1, 0.9f);
        var resource = new HarvestResourceScale(); int stone = 0;
        for (int k = 0; k < 4; k++) stone += resource.Apply(ResourceType.Stone, 2, 1.25f);
        Require(sum == 9 && stone == 10 && Mathf.RoundToInt(2 * 1.25f) == 2, $"Carry: 10 × score 1 × 0,9 = {sum}; 4 × 2 Stone × 1,25 = {stone} (per-reward rounding would give 8)");
        return .2;
    }

    // ---------------- 1) menü: seçim ekranı, kilitli kart ----------------
    static StartSelectionPanelUI Panel => Object.FindFirstObjectByType<StartSelectionPanelUI>(FindObjectsInactive.Include);

    sealed class CardView { public StartOptionSO option; public Button button; public GameObject cover; public string effects, lockText, footer; }
    static List<CardView> Cards(string field) => ((System.Collections.IEnumerable)F<object>(Panel, field)).Cast<object>().Select(c => new CardView
    {
        option = (StartOptionSO)c.GetType().GetField("option").GetValue(c), button = (Button)c.GetType().GetField("button").GetValue(c),
        cover = (GameObject)c.GetType().GetField("lockCover").GetValue(c), effects = ((TextMeshProUGUI)c.GetType().GetField("effects").GetValue(c)).text,
        lockText = ((TextMeshProUGUI)c.GetType().GetField("lockText").GetValue(c)).text, footer = ((TextMeshProUGUI)c.GetType().GetField("footer").GetValue(c)).text
    }).Where(c => c.option != null).ToList();

    static double MenuLocked()
    {
        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        Require(SceneManager.GetActiveScene().name == "MenuScene" && menu != null && State == GameStates.MainMenu, "Menu scene");
        Call(menu, "OnPlayClicked");
        var panel = Panel;
        Require(panel != null && panel.gameObject.activeInHierarchy && !F<GameObject>(menu, "mainMenuPanel").activeSelf, "Play opens the start selection (main menu panel hidden, no scene load yet)");
        Require(SceneManager.GetActiveScene().name == "MenuScene", "Still in the menu until the run is started");
        Require(panel.SelectedFarmer == Tuccar && panel.SelectedScythe == DarKesim, "Last valid selection pre-selected: Tüccar + Dar Kesim");
        var farmers = Cards("farmerCards"); var scythes = Cards("scytheCards");
        Require(farmers.Select(c => c.option).SequenceEqual(new StartOptionSO[] { Bahcivan, Tuccar, Secici }) && scythes.Select(c => c.option).SequenceEqual(new StartOptionSO[] { Standart, DarKesim }),
            "Cards: Bahçıvan, Tüccar, Seçici Yetiştirici | Standart, Dar Kesim");
        var sec = farmers[2];
        Require(!sec.button.interactable && sec.cover.activeSelf && Strip(sec.lockText).StartsWith("KİLİTLİ · GÖREV/Tek run'da 5 Legendary bitki hasat et") && Strip(sec.lockText).Contains("En iyi run: 0 / 5"),
            "Locked card shows the quest and progress: " + Strip(sec.lockText));
        sec.button.onClick.Invoke();
        Require(!panel.Select(Secici) && panel.SelectedFarmer == Tuccar, "Locked card cannot be selected (click and direct request)");
        farmers[0].button.onClick.Invoke();
        Require(panel.SelectedFarmer == Bahcivan, "Click selects Bahçıvan");
        farmers[1].button.onClick.Invoke();
        Require(panel.SelectedFarmer == Tuccar && Strip(F<TextMeshProUGUI>(panel, "summary").text) == "Seçim: Tüccar + Dar Kesim", "Click Tüccar; summary: " + Strip(F<TextMeshProUGUI>(panel, "summary").text));
        farmers = Cards("farmerCards"); scythes = Cards("scytheCards");
        string tuc = Strip(farmers[1].effects), dar = Strip(scythes[1].effects), secText = Strip(farmers[2].effects), neutral = Strip(farmers[0].effects);
        Require(tuc.Contains("+ Hasat kaynağı ×1,25") && tuc.Contains("− Hasat skoru ×0,90") && dar.Contains("+ Doğrudan vuruş hasarı ×1,40") && dar.Contains("davranış hasarı değişmez") &&
                dar.Contains("− Vuruş yarıçapı ×0,75") && secText.Contains("+ Nadirlik bonusu +15 puan") && secText.Contains("− Üretim aralığı ×1,15") && neutral.StartsWith("Nötr"),
            "Card texts from asset values");
        Require(!dar.Contains("hedef") && !dar.Contains("bitki"), "Dar Kesim text makes no target-count or area claim: " + dar);
        Note("Tüccar: " + tuc); Note("Dar Kesim: " + dar); Note("Seçici Yetiştirici: " + secText);
        Require(farmers[1].footer == "SEÇİLİ" && scythes[1].footer == "SEÇİLİ" && farmers[0].footer == "Seçmek için tıkla", "Selected cards marked");
        Screenshot("StartSelection_Locked");
        Call(panel, "RequestStart");
        Call(panel, "RequestStart"); // çift tıklama: tek istek
        return 3;
    }

    // ---------------- 2) Tüccar ekonomisi ----------------
    static PlanterSO Only(PlantSO plant)
    {
        var so = Object.Instantiate(PlanterAsset("1x1"));
        so.spawnTable = new List<PlantSpawnEntry> { new PlantSpawnEntry { plant = plant, baseChance = 100 } };
        return so;
    }

    static double TuccarEconomy()
    {
        Require(SceneManager.GetActiveScene().name == "GameScene" && State == GameStates.RunSetup && RM.Profile != null && RM.Profile.name == "Run50_Referans", "Start → GameScene, Run50_Referans, run setup");
        Require(Loadout != null && Loadout.Farmer == Tuccar && Loadout.Scythe == DarKesim && !Loadout.UsedFallback && Loadout.OwnedModifiers.Count == 1, "Run uses Tüccar + Dar Kesim (one owned modifier: radius)");
        Require(Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 0.75f) && Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1.4f) &&
                Mathf.Approximately(StartLoadoutManager.HarvestResourceMultiplier, 1.25f) && Mathf.Approximately(StartLoadoutManager.HarvestScoreMultiplier, 0.9f),
            "Effects: radius 0,75 · direct ×1,4 · harvest resources ×1,25 · score ×0,9");
        Require(Res(ResourceType.Gold) == 80 && Res(ResourceType.Iron) == 0 && Res(ResourceType.Stone) == 0, "Starting money not scaled by Tüccar: 80 / 0 / 0");
        StartRound(12);
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 100000f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        var grass = Plant("Grass");
        var cells = new List<(int, int)> { (-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0) };
        foreach (var (x, z) in cells) Place(Only(grass), x, z);
        int gold0 = Res(ResourceType.Gold); long score0 = Score; harvests.Clear();
        for (int round = 0; round < 2; round++)
        {
            Respawn();
            foreach (var (x, z) in cells) Attack(Pos(x, z));
        }
        int goldGain = Res(ResourceType.Gold) - gold0; long score = Score - score0;
        Require(harvests.Count == 10 && harvests.All(h => h.rarity == PlantRarity.Common && h.by == DamageType.Direct), "10 real Grass harvests (Common, 2 Gold, score 1 each)");
        Require(goldGain == 25, $"Tüccar: 10 × 2 Gold × 1,25 = {goldGain} (carry keeps the bonus; without it 20)");
        Require(score == 9, $"Tüccar: 10 × score 1 × 0,9 = {score} (carry keeps the cost; per-harvest rounding would give 10)");
        // Hasat dışı kaynaklar: doğrudan ekleme, saksı satışı, kart atlama.
        int goldBefore = Res(ResourceType.Gold);
        ResourceManager.Instance.AddResource(ResourceType.Gold, 7);
        Require(Res(ResourceType.Gold) - goldBefore == 7, "Direct resource add not scaled (+7)");
        var sold = Cell(0, 0).GetPlanterBrain(); var soldData = F<PlanterSO>(sold, "planterData"); int cost = soldData.cost; var costType = soldData.costType; int before = Res(costType);
        sold.RemoveSelf();
        Require(Res(costType) - before == cost / 2, $"Sell refund not scaled: +{cost / 2} {costType}");
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        Call(RM, "EndRound"); // round 12: kota yok, level → kart ekranı
        Require(State == GameStates.CardSelection, "Level-up at round 12 end opens the card screen");
        var cardsUI = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        var offers = F<List<TileCardOffer>>(cardsUI, "currentCards");
        TileRarity highest = offers.Max(o => o.Rarity);
        int expected = Mathf.RoundToInt(F<Dictionary<TileRarity, int>>(cardsUI, "skipBaseRewards")[highest] * (1f + RM.CurrentRound * 0.1f));
        int total0 = Res(ResourceType.Gold) + Res(ResourceType.Iron) + Res(ResourceType.Stone);
        Call(cardsUI, "OnSkipPressed");
        int skipGain = Res(ResourceType.Gold) + Res(ResourceType.Iron) + Res(ResourceType.Stone) - total0;
        Require(skipGain == expected, $"Card skip reward not scaled: +{skipGain} (formula {expected})");
        if (State == GameStates.CardSelection) Flush();
        return .2;
    }

    // ---------------- 3) doğrudan / davranış ayrımı ----------------
    static double DirectVersusBehavior()
    {
        ClearTestMods();
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        StartRound50();
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 40f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        var usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/UstaBicici.asset");
        SetP(SpecializationManager.Instance, "Chosen", usta);
        var a = Place(PlanterAsset("1x1"), -1, -1, Tile("Electric", "Legendary"), 1f);
        Place(PlanterAsset("1x1"), 0, 0);
        Respawn();
        var plantA = PlantAt(-1, -1); var plantB = PlantAt(0, 0);
        Require(plantA != null && plantB != null && plantB.MaxHealth > 90 && Mathf.Approximately(a.GetFinalStat(StatType.ElectricChance), 1f), $"Electric source A and diagonal target B (B HP {plantB.MaxHealth} at R50)");
        SetF(plantA, "currentHealth", 1);
        int triggered = HarvestBehaviorStats.Triggered(DamageType.Electric);
        Attack(Pos(-1, -1)); // yarıçap 0,75: yalnız A
        int electric = plantB.MaxHealth - plantB.CurrentHealth;
        Require(plantA.IsDead && HarvestBehaviorStats.Triggered(DamageType.Electric) == triggered + 1, "Direct kill of A triggered electric once");
        Require(electric == (int)Math.Round(40 * (double)usta.behaviorDamageMultiplier), $"Electric on B = 40 × {usta.behaviorDamageMultiplier} (Usta Biçici behavior) = {electric}: Dar Kesim ×1,4 not applied to behaviors");
        UnityEngine.Random.InitState(4242); float v = UnityEngine.Random.Range(0.85f, 1.15f); UnityEngine.Random.InitState(4242);
        int hp = plantB.CurrentHealth;
        Attack(Pos(0, 0));
        int direct = hp - plantB.CurrentHealth;
        int expected = Mathf.Max(1, Mathf.RoundToInt(40f * v * usta.directDamageMultiplier * DarKesim.directDamageMultiplier));
        Require(direct == expected, $"Direct hit on B = 40 × {v:0.000} × 1,25 (Usta) × 1,4 (Dar Kesim) = {direct}: each multiplier once, one rounding");
        SetP(SpecializationManager.Instance, "Chosen", null);
        return .2;
    }

    // ---------------- 4) yarıçap: gerçek hedef sayısı ----------------
    static int HitCount(Vector3 aim)
    {
        Respawn();
        var plants = Spawners().Select(s => F<GameObject>(s, "spawnedPlant")).Where(p => p != null).Select(p => p.GetComponent<PlantHealth>()).ToList();
        Attack(aim);
        return plants.Count(p => p.CurrentHealth < p.MaxHealth || p.IsDead);
    }

    static double Radius()
    {
        ClearTestMods();
        var hits = FieldHits();
        darHits = hits;
        float dar = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
        Require(Mathf.Approximately(dar, 0.75f) && hits == (1, 2, 4), $"Dar Kesim in the scene: radius {dar:0.00}; plants hit per swing on a full 3×3 (center / between two cells / corner) {hits}");
        Respawn(); // görüntü için taze bitkiler; hasar yazıları sönsün
        return 1.5;
    }

    static (int center, int edge, int corner) darHits;

    // 3×3 dolu tarla (1×1 saksılar), hasar 1 (kimse ölmez): gerçek AttackInRadius ile vurulan bitki sayısı.
    static (int, int, int) FieldHits()
    {
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Cell(x, z).GetGroundCellCached().ApplyModifier(null);
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 1f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Place(PlanterAsset("1x1"), x, z);
        Vector3 center = Pos(0, 0), edge = (Pos(0, 0) + Pos(1, 0)) / 2f, corner = (Pos(0, 0) + Pos(1, 1)) / 2f;
        var result = (HitCount(center), HitCount(edge), HitCount(corner));
        ClearTestMods();
        return result;
    }

    // Oyuncunun kendi yarıçap halkası (PlayerController.UpdateRadiusVisual) merkez hücrede; görüntü tarlanın ortasından kırpılır.
    static void RingShot(string name)
    {
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var visual = F<HarvestCursorVisual>(player, "cursorVisual");
        if (visual != null) visual.gameObject.SetActive(true);
        Call(player, "UpdateRadiusVisual", Pos(0, 0));
        Screenshot(name, new RectInt(560, 290, 800, 470));
    }

    // ---------------- 5) görev: 5 gerçek Legendary ----------------
    static double LegendaryQuest()
    {
        ClearTestMods();
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Cell(x, z).GetGroundCellCached().ApplyModifier(null);
        TestMod(Mod(StatType.HarvestDamage, StatTarget.Player, 100000f, ModifierOperation.Set));
        TestMod(Mod(StatType.CritChance, StatTarget.Player, 0f, ModifierOperation.Set));
        var grape = Plant("Grape");
        Require(grape.rarity == PlantRarity.Legendary, "Grape is Legendary (30 Gold)");
        Place(Only(grape), -1, -1, Tile("Electric", "Legendary"), 1f);  // doğrudan öldürülür, elektrik tetikler
        Place(Only(grape), 0, 0);                                       // çapraz: elektrikle ölür
        Place(Only(grape), 1, -1, Tile("Duplicate", "Legendary"), 1f);  // çifte ödül
        var tracker = QuestTracker.Instance;
        Require(tracker != null && tracker.Progress(Quest) == 0 && !MetaSave.IsUnlocked(Secici) && MetaSave.BestProgress(Quest) == 0, "Before: progress 0, Seçici locked");
        harvests.Clear();
        Respawn(); Attack(Pos(-1, -1));
        Require(harvests.Count(h => h.rarity == PlantRarity.Legendary) == 2 && harvests.Any(h => h.by == DamageType.Electric) && tracker.Progress(Quest) == 2 && !MetaSave.IsUnlocked(Secici),
            "Direct kill + electric kill = 2 Legendary; still locked");
        Require(MetaSave.BestProgress(Quest) == 2 && MetaSave.HasPendingChanges, "Progress is buffered outside the synchronous harvest path");
        Require(MetaSave.FlushPending() && File.ReadAllText(MetaSave.FilePath).Contains("\"best\": 2"), "Buffered best progress persists on flush (2)");
        Respawn(); Attack(Pos(-1, -1));
        Require(tracker.Progress(Quest) == 4 && !MetaSave.IsUnlocked(Secici), "4 Legendary: still locked");
        int gold0 = Res(ResourceType.Gold); long raw0 = SpecializationManager.Instance.HarvestResources.Raw(ResourceType.Gold);
        Attack(Pos(1, -1));
        long raw = SpecializationManager.Instance.HarvestResources.Raw(ResourceType.Gold) - raw0;
        Require(raw == 60 && Res(ResourceType.Gold) - gold0 >= 74, $"Duplicate: reward doubled (raw {raw} Gold, paid {Res(ResourceType.Gold) - gold0} with ×1,25)");
        Require(tracker.Progress(Quest) == 5 && MetaSave.IsUnlocked(Secici) && MetaSave.IsQuestDone(Quest) && tracker.UnlockedThisRun.Count == 1 && tracker.UnlockedThisRun[0] == Secici,
            "5th Legendary (with double reward) counted once → quest done, Seçici Yetiştirici unlocked");
        string saved = File.ReadAllText(MetaSave.FilePath);
        Require(saved.Contains("secici_yetistirici") && saved.Contains("\"done\": true"), "Unlock saved to disk immediately");
        Respawn(); Attack(Pos(-1, -1)); Attack(Pos(1, -1));
        Require(harvests.Count(h => h.rarity == PlantRarity.Legendary) == 8 && tracker.UnlockedThisRun.Count == 1 && File.ReadAllText(MetaSave.FilePath) == saved,
            "More Legendary harvests: no second completion, save unchanged");
        Note($"hasatlar: {harvests.Count(h => h.by == DamageType.Direct)} doğrudan, {harvests.Count(h => h.by == DamageType.Electric)} elektrik");
        // Run ortasında seçim değişirse aktif build değişmez.
        MetaSave.SetSelection(Secici, Standart);
        Require(Loadout.Farmer == Tuccar && Loadout.Scythe == DarKesim && Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 0.75f) &&
                Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1.4f), "Changing the saved selection mid-run does not change the active build");
        MetaSave.SetSelection(Tuccar, DarKesim);
        return .2;
    }

    static double RunEnd()
    {
        ClearTestMods();
        SetF(HarvestScoreManager.Instance, "totalScore", Score); // kotayı doldurma
        SetF(RM, "segmentStartScore", Score);
        Call(RM, "EndRound");
        Require(RM.Outcome == RunOutcome.QuotaFailed && State == GameStates.RunComplete, "Run lost at round 50 (quota)");
        return 1.5;
    }

    static double Restart(int n)
    {
        if (n == 1)
        {
            var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
            string text = Strip(complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score")));
            Require(text.StartsWith("KOTA TUTMADI") && text.Contains("Başlangıç: Tüccar · Dar Kesim") && text.Contains("Kalıcı olarak açıldı: Seçici Yetiştirici"), "Run end screen: " + text);
            Screenshot("RunEnd_Unlocked");
            Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
            return 3;
        }
        // n = 2, 3: bir önceki yeniden başlatmanın sonucu
        CheckRestarted(n - 1);
        if (n == 2) { Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed(); return 3; }
        // Aynı sahnede yeni run (BeginRun iki kez): etkiler katlanmaz.
        RM.BeginRun(); RM.BeginRun();
        CheckRestarted(3);
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static readonly StatModifier DarRadius = new StatModifier { statType = StatType.AreaRadius, target = StatTarget.Player, operation = ModifierOperation.MorePercent, value = -0.25f };

    static void CheckRestarted(int n)
    {
        Require(State == GameStates.RunSetup && Loadout.Farmer == Tuccar && Loadout.Scythe == DarKesim && Loadout.OwnedModifiers.Count == 1 && CountMod(DarRadius) == 1 &&
                Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 0.75f) && Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1.4f) &&
                Mathf.Approximately(StartLoadoutManager.HarvestResourceMultiplier, 1.25f), $"Restart {n}: same combination, effects applied once (radius 0,75, direct ×1,4)");
        Require(QuestTracker.Instance.Progress(Quest) == 0 && QuestTracker.Instance.UnlockedThisRun.Count == 0, $"Restart {n}: single-run counter reset");
        MetaSave.UseDirectory(SaveDir);
        Require(MetaSave.IsUnlocked(Secici) && MetaSave.LastLoad == MetaSave.LoadResult.Loaded, $"Restart {n}: unlock read back from disk");
    }

    // ---------------- menü dönüşü, açılmış kart ----------------
    static double MenuReturn()
    {
        CheckRestarted(4);
        StartRound50();
        SetF(HarvestScoreManager.Instance, "totalScore", Score); SetF(RM, "segmentStartScore", Score);
        Call(RM, "EndRound");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static double MenuUnlocked()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && State == GameStates.MainMenu && StartLoadoutManager.Instance == null &&
                Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1f) && Mathf.Approximately(StartLoadoutManager.HarvestResourceMultiplier, 1f), "Main menu: no active loadout, multipliers back to 1");
        Require(MetaSave.IsUnlocked(Secici), "Losing and returning to the menu does not revoke the unlock");
        Call(Object.FindFirstObjectByType<MainMenuUI>(), "OnPlayClicked");
        var panel = Panel;
        var farmers = Cards("farmerCards");
        Require(panel.SelectedFarmer == Tuccar && panel.SelectedScythe == DarKesim && farmers[2].button.interactable && !farmers[2].cover.activeSelf, "Seçici Yetiştirici card unlocked and selectable");
        farmers[2].button.onClick.Invoke();
        Cards("scytheCards")[0].button.onClick.Invoke();
        Require(panel.SelectedFarmer == Secici && panel.SelectedScythe == Standart, "Selected Seçici Yetiştirici + Standart");
        Screenshot("StartSelection_Unlocked");
        panel.RequestStart();
        return 3;
    }

    // ---------------- Seçici Yetiştirici: mevcut ve sonradan saksı ----------------
    static double SeciciEffects()
    {
        Require(Loadout.Farmer == Secici && Loadout.Scythe == Standart && Loadout.OwnedModifiers.Count == 2 && Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1f) &&
                Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1f) && Mathf.Approximately(StartLoadoutManager.HarvestResourceMultiplier, 1f), "Seçici Yetiştirici + Standart: two planter modifiers, player radius and damage unchanged");
        var small = PlanterAsset("1x1"); var big = PlanterAsset("2x2");
        var first = Place(small, -1, -1);
        StartRound50();
        var cells = new List<GridObject> { Cell(0, 0), Cell(1, 0), Cell(0, 1), Cell(1, 1) };
        var planter = Object.Instantiate(big.prefab);
        planter.transform.position = (Pos(0, 0) + Pos(1, 1)) / 2f;
        foreach (var c in cells) c.SetPlanterObject(planter);
        var later = planter.GetComponent<PlanterBrain>(); later.Initialize(big, cells); foreach (var c in cells) c.SetPlanterBrain(later);
        foreach (var (brain, so) in new[] { (first, small), (later, big) })
        {
            float rare = brain.GetFinalStat(StatType.RareSpawnChance) - brain.GetSpawnOverflowRarity();
            float spawn = brain.GetFinalStat(StatType.PlantSpawnRate);
            float expectSpawn = Mathf.Max(StatCalculator.MinimumSpawnInterval, so.GetBaseStat(StatType.PlantSpawnRate) * 1.15f);
            Require(Mathf.Approximately(rare, so.GetBaseStat(StatType.RareSpawnChance) + 15f) && Mathf.Abs(spawn - expectSpawn) < 1e-4f,
                $"{so.name} ({(brain == first ? "placed before the round" : "placed mid-round")}): rarity {so.GetBaseStat(StatType.RareSpawnChance)} → {rare}, spawn {so.GetBaseStat(StatType.PlantSpawnRate)} → {spawn:0.###} s");
        }
        return .2;
    }

    // ---------------- sahiplik: yalnız kendi modifier'ı ----------------
    static double Ownership()
    {
        var rare = Secici.modifiers[0]; var spawn = Secici.modifiers[1];
        var foreignSame = rare; // aynı değerde yabancı kopya
        var foreignOther = Mod(StatType.HarvestDamage, StatTarget.Player, 3f, ModifierOperation.Flat);
        Stats.AddGlobalModifier(foreignSame); Stats.AddGlobalModifier(foreignOther);
        int warn0 = warnings;
        Require(CountMod(rare) == 2 && CountMod(spawn) == 1, "Before cleanup: loadout rarity + an identical foreign copy, loadout spawn");
        Loadout.Clear();
        Require(CountMod(rare) == 1 && CountMod(spawn) == 0 && CountMod(foreignOther) == 1 && warnings == warn0, "Cleanup removed only the loadout's own modifiers (identical foreign copy and other modifiers stay)");
        Loadout.Apply();
        Require(CountMod(rare) == 2 && CountMod(spawn) == 1 && Loadout.OwnedModifiers.Count == 2, "Re-apply adds exactly the loadout's two modifiers");
        Stats.ClearGlobalModifiers();
        Loadout.Clear();
        Require(Stats.GlobalModifiers.Count == 0 && warnings == warn0, "After StatManager clears everything, loadout cleanup removes nothing and logs no warning");
        MetaSave.SetSelection(Bahcivan, Standart);
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static double Neutral()
    {
        Require(Loadout.Farmer == Bahcivan && Loadout.Scythe == Standart && Loadout.OwnedModifiers.Count == 0 && Stats.GlobalModifiers.Count == 0 &&
                Mathf.Approximately(StartLoadoutManager.DirectDamageMultiplier, 1f) && Mathf.Approximately(StartLoadoutManager.HarvestResourceMultiplier, 1f) &&
                Mathf.Approximately(StartLoadoutManager.HarvestScoreMultiplier, 1f) && Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1f),
            "Bahçıvan + Standart: no modifiers, all multipliers 1 (same as before Bölüm 3.3)");
        Require(StartLoadoutManager.ScaleScore(7) == 7 && FractionalIdentity(), "Neutral score scaling is the identity");
        StartRound50();
        var hits = FieldHits();
        Require(Mathf.Approximately(Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), 1f) && hits == (5, 6, 4), // edge tangency now includes the four neighbouring surfaces
            $"Standart in the scene: radius 1,00; plants hit per swing {hits} (Dar Kesim {darHits})");
        Note($"yarıçap bedeli (sahne, 3×3 dolu tarla, gerçek AttackInRadius; merkez / iki hücre arası / köşe): Standart {hits} · Dar Kesim {darHits}");
        Respawn();
        Require(File.Exists(RealPath) == realExisted && (!realExisted || File.GetLastWriteTimeUtc(RealPath) == realTime), "Player's real save untouched by the whole test");
        return 1.5;
    }

    static bool FractionalIdentity() { var s = new FractionalScale(); return s.Apply(1, 1f) == 1 && s.Apply(3, 1f) == 3; }

    // ---------------- ekran görüntüsü ----------------
    static RenderTexture target;
    static void Screenshot(string name, RectInt? crop = null)
    {
        // Menü sahnesindeki kamera MainCamera etiketli değil; canvas kamerası boş (overlay gibi çizer).
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
        RectInt area = crop ?? new RectInt(0, 0, target.width, target.height);
        var png = new Texture2D(area.width, area.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(area.x, area.y, area.width, area.height), 0, 0); png.Apply();
        RenderTexture.active = active; cam.targetTexture = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG()); Object.DestroyImmediate(png);
        notes.Add("saved Logs/" + name + ".png");
    }
}
