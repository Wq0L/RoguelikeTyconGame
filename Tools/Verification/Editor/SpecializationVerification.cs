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

// Batch (izole kopya): Bölüm 2 / 2.1 — boss sonrası bedelli uzmanlaşma, GameScene'de.
// A) Akış: kota kaybında ödül yok; kartlar → seçim → özet; tek açılış; seçim bitmeden round 11 yok; çift tıklama;
//    dört davranış ve doğrudan hasar katsayıları gerçek kod yolundan; Koru hiçbir şeyi değiştirmez; yeni run temiz;
//    10 round ve uzun run profillerinde seçim ekranı yok.
// B) 2.1 kaynak bedeli: yalnız hasat ödülüne, duplicate dahil bir kez, kesirli kalan kaynak başına; doğrudan ve davranış
//    öldürmelerinde aynı; başlangıç parası, satış iadesi, kart atlama ödülü ve düz kaynak eklemesi etkilenmez; XP ve skor aynı;
//    kalanlar yeni run'da ve ClearAll'da sıfır.
// C) Ölçüm: üç tarla düzeni × üç stat/can eşiği × dört seçenek (Davranış Ustası'nın bedelsiz kopyası kontrol) × aynı seed kümesi,
//    bot oyuncu (en çok bitkiyi kapsayan hücreye vurur), sabit kare süresi (Time.captureDeltaTime) ile.
[InitializeOnLoad]
public static class SpecializationVerification
{
    const string Key = "SpecializationVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string Specs = "Assets/ScriptableObjects/Specializations/";
    static readonly List<string> notes = new();
    static int step; static double nextAt;
    static RenderTexture target; static Camera cam;
    static int choiceScreens, cardScreens;
    static bool listening;
    static SpecializationSO usta, davranis, koru;

    static SpecializationVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Uzmanlasma20.asset");
        var frost = AssetDatabase.LoadAssetAtPath<FrostFrontSO>("Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset");
        frost.seed = 7;
        EditorUtility.SetDirty(selection); EditorUtility.SetDirty(frost); AssetDatabase.SaveAssets();
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
        File.WriteAllLines("Logs/SpecializationVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
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
    static SpecializationManager SM => SpecializationManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static void SetScore(long v) => SetF(HarvestScoreManager.Instance, "totalScore", v);
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static PlantSO PlantData(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    static int Res(ResourceType t) => ResourceManager.Instance.GetResourceAmount(t);
    static int ResTotal => Res(ResourceType.Gold) + Res(ResourceType.Iron) + Res(ResourceType.Stone);
    static double Xp => ProgressionManager.Instance.TotalXPEarned;
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static StatModifier Mod(StatType s, StatTarget t, float v) => new StatModifier { statType = s, target = t, operation = ModifierOperation.Set, value = v };

    static void Listen()
    {
        if (listening) return;
        listening = true;
        GameManager.Instance.OnGameStateChanged += s => { if (s == GameStates.RoundChoice) choiceScreens++; if (s == GameStates.CardSelection) cardScreens++; };
    }

    static void LoadSpecs()
    {
        usta = AssetDatabase.LoadAssetAtPath<SpecializationSO>(Specs + "UstaBicici.asset");
        davranis = AssetDatabase.LoadAssetAtPath<SpecializationSO>(Specs + "DavranisUstasi.asset");
        koru = AssetDatabase.LoadAssetAtPath<SpecializationSO>(Specs + "MevcutDuzeniKoru.asset");
    }

    static void PrepareCapture()
    {
        cam = Camera.main;
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = target;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
    }

    static void Save(string name)
    {
        Canvas.ForceUpdateCanvases(); cam.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply(); RenderTexture.active = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG()); Object.DestroyImmediate(png);
        notes.Add("saved Logs/" + name + ".png");
    }

    static void Flush()
    {
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        while (State == GameStates.CardSelection)
        {
            var offers = F<List<TileCardOffer>>(cards, "currentCards");
            Call(cards, "OnCardSelected", offers[0]);
        }
    }

    // Round'u kota değeriyle bitirir (segment sonunda skor = hedef × oran).
    static void PlayRound(double quotaRatio = 1.0)
    {
        RM.StartNextRound();
        if (RM.IsQuotaSegmentEnd(RM.CurrentRound))
        {
            long start = HarvestScoreManager.Instance.TotalScore - RM.QuotaProgress;
            SetScore(start + (long)Math.Ceiling(RM.QuotaTarget * quotaRatio));
        }
        Call(RM, "EndRound");
    }

    static void Place1x1(GridObject cell, TileModifierSO tile = null, float value = -1)
    {
        var data = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        var planter = Object.Instantiate(data.prefab);
        planter.transform.position = cell.GetGroundCellCached().transform.position;
        cell.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(data, new List<GridObject> { cell }); cell.SetPlanterBrain(brain);
        if (tile != null) ApplyTile(cell, tile, value);
    }

    static void ApplyTile(GridObject cell, TileModifierSO so, float value)
    {
        var mods = so.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value < 0 ? r.maxValue : value }).ToList();
        cell.GetGroundCellCached().ApplyModifier(so, mods);
    }

    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");

    static void FillOpen(Func<int, int, (TileModifierSO, float)> tiles)
    {
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        {
            var cell = Grid.GetGridObject(new GridPosition(x, z)); var g = cell?.GetGroundCellCached();
            if (g == null || g.IsLocked || cell.GetPlanterBrain() != null) continue;
            var (tile, value) = tiles(x - Center, z - Center);
            Place1x1(cell, tile, value);
        }
    }

    static void RespawnAll() { foreach (var s in Spawners()) if (F<GameObject>(s, "spawnedPlant") == null) Call(s, "TrySpawnPlant"); }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return FailRun();
            case 1: return SuccessToChoice();
            case 2: return Choose();
            case 3: return Coefficients();
            case 4: return HarvestResources();
            case 5: return FinishRun();
            case 6: return BeforeRestart();
            case 7: return AfterRestart();
            case 8: return ProtoCheck();
            case 9: return LongRunCheck();
            default: return MeasureStep();
        }
    }

    // Run 1: segment 1 geçilir, segment 2 (boss) kotası tutmaz → ödül yok.
    static double FailRun()
    {
        Listen(); LoadSpecs();
        Require(RM.Profile != null && RM.Profile.name == "Uzmanlasma20" && RM.MaxRounds == 20 && RM.Profile.specializationAfterSegment == 2 && SM.Options.Count == 3,
            "Specialization profile: 20 rounds, choice after segment 2, 3 options");
        Require(RM.QuotaTargetFor(1) == 40 && RM.QuotaTargetFor(2) == 200 && RM.QuotaTargetFor(3) == 350 && RM.QuotaTargetFor(4) == 600, "Quota table 40 / 200 / 350 / 600");
        Require(ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == 80 && ResourceManager.Instance.GetResourceAmount(ResourceType.Iron) == 0, "Normal economy (80 Gold)");
        Require(SM.Chosen == null && !SM.IsPending && SpecializationManager.DirectMultiplier == 1f && SpecializationManager.BehaviorMultiplier == 1f && SpecializationManager.HarvestResourceMultiplier == 1f,
            "No specialization at start");
        Require(usta.directDamageMultiplier == 1.25f && usta.behaviorDamageMultiplier == .85f && usta.harvestResourceMultiplier == 1f &&
                davranis.directDamageMultiplier == 1f && davranis.behaviorDamageMultiplier == 1.25f && davranis.harvestResourceMultiplier == .9f && koru.ChangesNothing,
            "Assets: Usta 1,25/0,85/1 · Davranış 1/1,25/kaynak 0,90 · Koru 1/1/1");
        for (int r = 1; r <= 9; r++) { PlayRound(); Flush(); }
        PlayRound(0.9);
        Require(RM.EndedByQuota && State == GameStates.RunComplete && !SM.IsPending && SM.Chosen == null && choiceScreens == 0, "Failed boss quota: run lost, no specialization screen");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static List<string> PanelTexts(SpecializationPanelUI panel) =>
        panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeInHierarchy).Select(t => Strip(t.text)).ToList();

    // Run 2: round 10 kotası geçilir, iki kart bekler → kartlar → seçim ekranı.
    static double SuccessToChoice()
    {
        Listen(); LoadSpecs();
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        PrepareCapture();
        choiceScreens = cardScreens = 0;
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            Place1x1(Cell(x, z), x == 0 && z == 0 ? Tile("Explosive", "Legendary") : null, 1f);
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        for (int r = 1; r <= 9; r++) { PlayRound(); Flush(); }
        RM.StartNextRound();
        Require(RM.CurrentRound == 10, "Round 10 running");
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        long start = HarvestScoreManager.Instance.TotalScore - RM.QuotaProgress;
        SetScore(start + 200);
        Call(RM, "EndRound");
        Require(!RM.EndedByQuota && RM.LastQuotaScore == 200 && SM.IsPending && SM.Chosen == null, "200 / 200 at round 10: boss passed, specialization pending");
        Require(State == GameStates.CardSelection && choiceScreens == 0, "Pending card selections come first");
        int picks = 0;
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        while (State == GameStates.CardSelection) { Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]); picks++; }
        Require(picks == 2, "Both pending card selections kept: " + picks);
        Require(State == GameStates.RoundChoice && choiceScreens == 1, "Then the specialization screen");
        RM.StartNextRound();
        Require(State == GameStates.RoundChoice && RM.CurrentRound == 10, "Round 11 cannot start before choosing");
        var panel = Object.FindFirstObjectByType<SpecializationPanelUI>(FindObjectsInactive.Include);
        Require(panel != null && panel.gameObject.activeInHierarchy, "Specialization panel open");
        var texts = PanelTexts(panel);
        Require(texts.Any(t => t == "USTA BİÇİCİ") && texts.Any(t => t == "DAVRANIŞ USTASI") && texts.Any(t => t == "MEVCUT DÜZENİ KORU"), "Three options shown");
        Require(texts.Contains("Doğrudan hasar ×1,25") && texts.Any(t => t.StartsWith("Davranış hasarı ×0,85")) && texts.Any(t => t.StartsWith("Davranış hasarı ×1,25")) &&
                texts.Any(t => t.StartsWith("Hasat kaynağı ×0,90/hasattan gelen Gold · Iron · Stone")) && !texts.Any(t => t.StartsWith("Doğrudan hasar ×0,")),
            "Gains and costs visible before choosing (Davranış Ustası: cost is harvest resources, no direct penalty)");
        Require(texts.Contains("Bonus yok, ceza yok") && texts.Contains("Hasar ve hasat geliri olduğu gibi kalır"), "Keep-current option is explicit");
        // Metin asset değerinden üretilir: değer değişince kart yazısı da değişir.
        davranis.harvestResourceMultiplier = .8f; panel.gameObject.SetActive(false); panel.gameObject.SetActive(true);
        bool follows = PanelTexts(panel).Any(t => t.StartsWith("Hasat kaynağı ×0,80"));
        davranis.harvestResourceMultiplier = .9f; panel.gameObject.SetActive(false); panel.gameObject.SetActive(true);
        Require(follows && PanelTexts(panel).Any(t => t.StartsWith("Hasat kaynağı ×0,90")), "Card cost text follows the asset value (×0,80 → ×0,90)");
        return .6;
    }

    static double Choose()
    {
        Save("SpecChoice");
        var panel = Object.FindFirstObjectByType<SpecializationPanelUI>(FindObjectsInactive.Include);
        var buttons = panel.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.activeInHierarchy).ToList();
        Require(buttons.Count == 3 && buttons.All(b => b.interactable), "Three active choose buttons");
        int globalsBefore = StatManager.Instance.GlobalModifiers.Count;
        buttons[0].onClick.Invoke();
        buttons[0].onClick.Invoke(); // çift tıklama
        Require(SM.Chosen == usta && !SM.IsPending && State == GameStates.RoundEnd, "Usta Biçici chosen once, back to round end");
        Require(!SM.Choose(davranis) && !SM.Choose(usta) && SM.Chosen == usta, "Second request rejected (no double reward, no switching)");
        panel.gameObject.SetActive(true);
        Require(panel.GetComponentsInChildren<Button>(true).All(b => !b.interactable) && !SM.Choose(koru), "Re-opened panel cannot choose again");
        panel.gameObject.SetActive(false);
        Require(StatManager.Instance.GlobalModifiers.Count == globalsBefore && SpecializationManager.DirectMultiplier == 1.25f && SpecializationManager.BehaviorMultiplier == .85f &&
                SpecializationManager.HarvestResourceMultiplier == 1f,
            "Choice adds no stat modifiers; only the coefficients (×1,25 / ×0,85, resources ×1)");
        Require(choiceScreens == 1, "Specialization screen opened exactly once");
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Call(summary, "Fill", GameFeelDirector.Instance.LastRound);
        var spec = summary.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Specialization Notice");
        Require(spec != null && spec.gameObject.activeInHierarchy && Strip(spec.text) == "Uzmanlaşma: USTA BİÇİCİ · doğrudan ×1,25 · davranış ×0,85", "Summary shows the choice: " + Strip(spec?.text));
        Require(SpecializationPanelUI.Describe(davranis) == "DAVRANIŞ USTASI · davranış ×1,25 · hasat kaynağı ×0,90" && SpecializationPanelUI.Describe(koru) == "MEVCUT DÜZENİ KORU · değişiklik yok",
            "Short description lists only changed values: " + SpecializationPanelUI.Describe(davranis));
        return 1;
    }

    // Round 11 sürerken: doğrudan ve dört davranışın katsayısı her seçenek için gerçek kod yolundan.
    static double Coefficients()
    {
        Save("SpecSummary");
        RM.StartNextRound();
        Require(RM.CurrentRound == 11 && State == GameStates.Round, "Round 11 starts after the choice");
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include); Call(hud, "Refresh");
        var specLine = hud.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Specialization");
        Require(specLine != null && specLine.gameObject.activeInHierarchy && specLine.text.StartsWith("Uzmanlaşma: USTA BİÇİCİ"), "HUD shows the choice");
        Save("SpecHUD");

        var stats = StatManager.Instance;
        var noCrit = Mod(StatType.CritChance, StatTarget.Player, 0f);
        var oneCell = Mod(StatType.AreaRadius, StatTarget.Player, .1f);
        var dmg10 = Mod(StatType.HarvestDamage, StatTarget.Player, 10f);
        stats.AddGlobalModifier(noCrit); stats.AddGlobalModifier(oneCell); stats.AddGlobalModifier(dmg10);
        var player = Object.FindFirstObjectByType<PlayerController>();
        var center = Cell(0, 0); var corner = Cell(-1, -1); var east = Cell(1, 0);
        var cornerBrain = corner.GetPlanterBrain();
        var tornadoes = TornadoManager.Instance; var behaviors = HarvestBehaviorManager.Instance;
        var results = new Dictionary<string, int[]>();
        var explosionTriggers = new Dictionary<string, int>();
        foreach (var (name, option) in new[] { ("yok", (SpecializationSO)null), ("Koru", koru), ("Usta", usta), ("Davranış", davranis) })
        {
            SetP(SM, "Chosen", option);
            float m = option != null ? option.behaviorDamageMultiplier : 1f, d = option != null ? option.directDamageMultiplier : 1f;
            RespawnAll();
            // Doğrudan: tek hücre, kritik yok; sapma aynı seed'den.
            var victim = center.GetPlantObject().GetComponent<PlantHealth>();
            UnityEngine.Random.InitState(42); float v = UnityEngine.Random.Range(.85f, 1.15f);
            int expectDirect = Mathf.Max(1, Mathf.RoundToInt(10f * v * d));
            int hp = victim.CurrentHealth; UnityEngine.Random.InitState(42);
            Call(player, "AttackInRadius", center.GetGroundCellCached().transform.position);
            int direct = hp - victim.CurrentHealth;
            RespawnAll();
            // Kasırga ve bumerang: fırlatıldıkları andaki hasar (TornadoManager / BoomerangScythe.Launch).
            Call(tornadoes, "ClearAll"); behaviors.ClearAll();
            Require(tornadoes.TrySpawn(corner, 10), "tornado launched");
            var tornado = F<List<Tornado>>(tornadoes, "activeTornadoes").Last(); int tornadoDmg = F<int>(tornado, "damage");
            Require(behaviors.TryBoomerang(cornerBrain, corner, 10), "boomerang launched");
            var scythe = F<List<BoomerangScythe>>(behaviors, "boomerangs").Last(); int boomerangDmg = F<int>(scythe, "damage");
            Call(tornadoes, "ClearAll"); behaviors.ClearAll();
            // Elektrik: köşe saksısından çapraz komşu (merkez) bitkisi.
            RespawnAll();
            var eVictim = center.GetPlantObject().GetComponent<PlantHealth>(); int eHp = eVictim.CurrentHealth;
            behaviors.TryElectric(cornerBrain, 10);
            int electricDmg = eHp - eVictim.CurrentHealth;
            behaviors.ClearAll();
            // Patlama: merkezdeki Explosive (şans 1) saksısının bitkisi doğrudan kesilince doğu komşusu vurulur.
            RespawnAll();
            var big = Mod(StatType.HarvestDamage, StatTarget.Player, 100f);
            stats.RemoveGlobalModifier(dmg10); stats.AddGlobalModifier(big);
            var neighbor = east.GetPlantObject().GetComponent<PlantHealth>(); int nHp = neighbor.CurrentHealth;
            HarvestBehaviorStats.Reset();
            Call(player, "AttackInRadius", center.GetGroundCellCached().transform.position);
            int explosionDmg = nHp - neighbor.CurrentHealth;
            explosionTriggers[name] = HarvestBehaviorStats.Triggered(DamageType.Explosion);
            stats.RemoveGlobalModifier(big); stats.AddGlobalModifier(dmg10);
            Call(tornadoes, "ClearAll"); behaviors.ClearAll();

            int expTornado = (int)Math.Round(Math.Max(1, Mathf.RoundToInt(10 * .5f)) * (double)m);
            int expBoomerang = (int)Math.Round(Math.Max(1, Mathf.RoundToInt(10 * .65f)) * (double)m);
            int expElectric = (int)Math.Round(10 * (double)m), expExplosion = (int)Math.Round(100 * (double)m);
            Require(direct == expectDirect && tornadoDmg == expTornado && boomerangDmg == expBoomerang && electricDmg == expElectric && explosionDmg == expExplosion,
                $"{name}: doğrudan {direct} (beklenen {expectDirect}) · kasırga {tornadoDmg}/{expTornado} · bumerang {boomerangDmg}/{expBoomerang} · elektrik {electricDmg}/{expElectric} · patlama {explosionDmg}/{expExplosion}");
            results[name] = new[] { direct, tornadoDmg, boomerangDmg, electricDmg, explosionDmg };
        }
        Require(results["Koru"].SequenceEqual(results["yok"]), "Keep-current changes no damage value");
        Require(results["Davranış"][0] == results["yok"][0] && results["Davranış"][0] == results["Koru"][0], "Davranış Ustası: direct damage unchanged (×1,00)");
        Require(results["Davranış"][4] == 125 && results["Usta"][4] == 85, "Behavior coefficient applied once, on the shared base (explosion 125 Davranış / 85 Usta)");
        Require(explosionTriggers.Values.All(n => n == 1), "Direct kill triggers the explosion the same way for every option");
        stats.RemoveGlobalModifier(noCrit); stats.RemoveGlobalModifier(oneCell); stats.RemoveGlobalModifier(dmg10);
        SetP(SM, "Chosen", usta);
        return .3;
    }

    // ---------------- 2.1: hasat kaynağı bedeli ----------------
    static PlantResource Reward(GridObject cell, PlantSO data)
    {
        var r = cell.GetPlantObject().GetComponentInChildren<PlantResource>(true);
        r.Initialize(data, cell.GetPlanterBrain());
        return r;
    }

    // Aynı ödül dizisini seçilen uzmanlaşmayla gerçek ödül kodundan (PlantResource.GiveReward) geçirir.
    static (int gold, int iron, int stone, double xp, long score) RewardSequence(SpecializationSO option, int calls, params (PlantResource r, int weight)[] mix)
    {
        SetP(SM, "Chosen", option); SM.HarvestResources.Clear();
        int g = Res(ResourceType.Gold), i = Res(ResourceType.Iron), s = Res(ResourceType.Stone); double xp = Xp; long score = Score;
        for (int c = 0; c < calls; c++)
            foreach (var (r, weight) in mix) for (int k = 0; k < weight; k++) Call(r, "GiveReward");
        return (Res(ResourceType.Gold) - g, Res(ResourceType.Iron) - i, Res(ResourceType.Stone) - s, Xp - xp, Score - score);
    }

    static long Due(long raw, float multiplier) => (raw * (long)Math.Round(multiplier * 10000.0) + 5000) / 10000;

    static double HarvestResources()
    {
        var stats = StatManager.Instance;
        var grass = PlantData("Grass"); var carrot = PlantData("Carrot"); var potato = PlantData("Potato");
        Require(grass.resourceType == ResourceType.Gold && grass.rewardAmount == 2 && carrot.resourceType == ResourceType.Iron && carrot.rewardAmount == 4 &&
                potato.resourceType == ResourceType.Stone && potato.rewardAmount == 2, "Small harvest rewards: Grass 2 Gold, Carrot 4 Iron, Potato 2 Stone");
        RespawnAll();
        var gold = Reward(Cell(-1, -1), grass); var iron = Reward(Cell(-1, 1), carrot); var stone = Reward(Cell(1, -1), potato);

        // (1) Kesirli kalan: ceza öncesi 100'er birim → 90'ar; her ödül küçük olsa da oran korunur.
        var k = RewardSequence(koru, 25, (gold, 2), (iron, 1), (stone, 2));
        var dv = RewardSequence(davranis, 25, (gold, 2), (iron, 1), (stone, 2));
        Require(k.gold == 100 && k.iron == 100 && k.stone == 100, $"Koru: 100 Gold / 100 Iron / 100 Stone from 50+25+50 harvests ({k.gold}/{k.iron}/{k.stone})");
        Require(dv.gold == 90 && dv.iron == 90 && dv.stone == 90, $"Davranış Ustası: the same harvests pay 90 / 90 / 90 ({dv.gold}/{dv.iron}/{dv.stone})");
        Note($"karşılaştırma: ödül başına en yakına yuvarlama 2×0,9→2, 4×0,9→4 olurdu (100/100/100, ceza yok); aşağı yuvarlama 2→1, 4→3 olurdu (50/75/50)");
        Require(dv.xp == k.xp && dv.score == k.score && k.xp > 0 && k.score > 0, $"XP and score not penalized (XP {dv.xp:0} = {k.xp:0}, score {dv.score} = {k.score})");
        Require(SM.HarvestResources.Raw(ResourceType.Gold) == 100 && SM.HarvestResources.Paid(ResourceType.Gold) == 90 && SM.HarvestResources.Raw(ResourceType.Iron) == 100,
            "Carry kept per resource (raw 100 → paid 90 each)");
        // Her adımda ödenen toplam hedefin yarım biriminden fazla sapmaz.
        SetP(SM, "Chosen", davranis); SM.HarvestResources.Clear();
        int g0 = Res(ResourceType.Gold), worst = 0; var paidSteps = new List<int>();
        for (int c = 1; c <= 50; c++)
        {
            int before = Res(ResourceType.Gold); Call(gold, "GiveReward"); paidSteps.Add(Res(ResourceType.Gold) - before);
            double err = Math.Abs((Res(ResourceType.Gold) - g0) - c * 2 * .9); worst = Math.Max(worst, (int)Math.Ceiling(err * 10));
        }
        Require(worst <= 5 && paidSteps.All(p => p == 1 || p == 2), $"Running total stays within 0,5 of raw × 0,90 at every harvest (steps: {string.Join("", paidSteps.Take(10))}…)");

        // (2) Duplicate dahil, bir kez: çift ödül 4 Gold; 25 hasat → ham 100 → 90 (iki kez uygulansa 81).
        var dup = Mod(StatType.DuplicateChance, StatTarget.Planter, 1f);
        stats.AddGlobalModifier(dup);
        var kd = RewardSequence(koru, 25, (gold, 1));
        var dd = RewardSequence(davranis, 25, (gold, 1));
        stats.RemoveGlobalModifier(dup);
        Require(kd.gold == 100 && dd.gold == 90, $"Duplicate included, applied once after it: Koru {kd.gold} → Davranış {dd.gold}");

        // (3) Doğrudan ve davranış öldürmesi: aynı kalan dizisi, aynı XP ve skor.
        var noCrit = Mod(StatType.CritChance, StatTarget.Player, 0f); var oneCell = Mod(StatType.AreaRadius, StatTarget.Player, .1f); var big = Mod(StatType.HarvestDamage, StatTarget.Player, 100f);
        stats.AddGlobalModifier(noCrit); stats.AddGlobalModifier(oneCell); stats.AddGlobalModifier(big);
        var player = Object.FindFirstObjectByType<PlayerController>();
        var cornerBrain = Cell(-1, -1).GetPlanterBrain();
        (int[] steps, int kills, double xp, long score, int direct, int behavior) Kills(SpecializationSO option)
        {
            SetP(SM, "Chosen", option); SM.HarvestResources.Clear();
            var steps = new List<int>(); int direct = 0, behavior = 0; double xp0 = Xp; long s0 = Score;
            void Count(PlantHealth h) { if (h.KilledBy == DamageType.Direct) direct++; else behavior++; }
            PlantHealth.AnyHarvested += Count;
            for (int n = 0; n < 10; n++)
            {
                RespawnAll();
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) Reward(Cell(x, z), grass);
                int before = Res(ResourceType.Gold);
                Call(player, "AttackInRadius", Cell(-1, -1).GetGroundCellCached().transform.position); // doğrudan: köşe (davranış tile'ı yok)
                steps.Add(Res(ResourceType.Gold) - before); before = Res(ResourceType.Gold);
                HarvestBehaviorManager.Instance.TryElectric(cornerBrain, 100);                          // davranış: çaprazdaki iki bitki
                steps.Add(Res(ResourceType.Gold) - before);
                HarvestBehaviorManager.Instance.ClearAll();
            }
            PlantHealth.AnyHarvested -= Count;
            return (steps.ToArray(), direct + behavior, Xp - xp0, Score - s0, direct, behavior);
        }
        var kk = Kills(koru); var kdv = Kills(davranis);
        stats.RemoveGlobalModifier(noCrit); stats.RemoveGlobalModifier(oneCell); stats.RemoveGlobalModifier(big);
        Require(kk.direct == 10 && kk.behavior == 20 && kdv.direct == 10 && kdv.behavior == 20, $"10 direct + 20 electric kills per option ({kdv.direct}+{kdv.behavior})");
        // Beklenen: tek bir kalan dizisi, öldürme yolundan bağımsız (ham toplam 2, 6, 8, 12 … → round(×0,9)).
        var expected = new List<int>(); long raw = 0, paid = 0;
        for (int n = 0; n < 10; n++) foreach (int kills in new[] { 1, 2 }) { raw += 2 * kills; long due = Due(raw, .9f); expected.Add((int)(due - paid)); paid = due; }
        Require(kk.steps.Sum() == 60 && kdv.steps.Sum() == 54 && kdv.steps.SequenceEqual(expected),
            $"Direct and behavior kills share one carry: 60 → 54 Gold (steps {string.Join(",", kdv.steps.Take(8))}…)");
        Require(kdv.xp == kk.xp && kdv.score == kk.score, $"Kill XP and score unchanged (XP {kdv.xp:0}, score {kdv.score})");

        // (4) Hasat dışı kaynak eklemeleri etkilenmez (bekleyen kalan varken).
        SetP(SM, "Chosen", davranis);
        Call(gold, "GiveReward");
        Require(SM.HarvestResources.Raw(ResourceType.Gold) > 0, "Carry pending before the non-harvest checks");
        int before2 = Res(ResourceType.Gold);
        ResourceManager.Instance.AddResource(ResourceType.Gold, 7);
        Require(Res(ResourceType.Gold) - before2 == 7, "Plain AddResource keeps its amount (+7)");
        var sold = Cell(1, 0).GetPlanterBrain(); var soldData = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        before2 = Res(soldData.costType);
        sold.RemoveSelf();
        Require(Res(soldData.costType) - before2 == soldData.cost / 2, $"Sale refund untouched: +{soldData.cost / 2} {soldData.costType}");

        // Kart atlama: round 11 sonunda level kartı → Atla ödülü tam.
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        Call(RM, "EndRound");
        Require(State == GameStates.CardSelection, "Round 11 ends with a card selection");
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        var offers = F<List<TileCardOffer>>(cards, "currentCards");
        var highest = offers.Max(o => o.Rarity);
        int skipReward = Mathf.RoundToInt(F<Dictionary<TileRarity, int>>(cards, "skipBaseRewards")[highest] * (1f + RM.CurrentRound * .1f));
        int total = ResTotal;
        Call(cards, "OnSkipPressed");
        Require(ResTotal - total == skipReward, $"Card skip reward untouched: +{skipReward} ({highest})");
        Flush();
        Require(State == GameStates.RoundEnd && SpecializationManager.HarvestResourceMultiplier == .9f, "Round 11 closed; Davranış Ustası still active");
        SetP(SM, "Chosen", usta); // akışın geri kalanı Usta ile (zafer ekranı); Davranış kalanları yeni run'a kadar durur
        return .3;
    }

    static double FinishRun()
    {
        for (int r = 12; r <= 20; r++)
        {
            PlayRound(); Flush();
            if (r == 15) Require(!SM.IsPending && State == GameStates.RoundEnd, "No second choice after segment 3");
        }
        Require(RM.Outcome == RunOutcome.Victory && State == GameStates.RunComplete && choiceScreens == 1 && !SM.IsPending, "Round 20 passed: victory, still one choice");
        var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        var text = complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score"));
        Require(text != null && Strip(text).StartsWith("UZMANLAŞMA TESTİ TAMAMLANDI") && Strip(text).Contains("Uzmanlaşma: USTA BİÇİCİ"), "Victory screen: " + Strip(text));
        return 1.5;
    }

    static double BeforeRestart()
    {
        Save("SpecVictory");
        Require(SM.HarvestResources.Raw(ResourceType.Gold) > 0, "Old run still holds a resource carry before restart");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    static double AfterRestart()
    {
        Require(SceneManager.GetActiveScene().name == "GameScene" && State == GameStates.RunSetup && SM != null && SM.Chosen == null && !SM.IsPending, "New run: specialization cleared");
        Require(SpecializationManager.DirectMultiplier == 1f && SpecializationManager.BehaviorMultiplier == 1f && SpecializationManager.HarvestResourceMultiplier == 1f, "New run: coefficients back to 1");
        Require(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().All(t => SM.HarvestResources.Raw(t) == 0 && SM.HarvestResources.Paid(t) == 0), "New run: resource carry is zero");
        Require(Res(ResourceType.Gold) == 80 && Res(ResourceType.Iron) == 0 && Res(ResourceType.Stone) == 0, "New run: starting budget untouched (80 Gold)");
        SetP(SM, "Chosen", davranis);
        SM.HarvestResources.Apply(ResourceType.Gold, 2, SpecializationManager.HarvestResourceMultiplier);
        bool had = SM.HarvestResources.Raw(ResourceType.Gold) == 2;
        SM.ClearAll(); // OnRunStarted / ana menü / yok edilme yolu
        Require(had && SM.Chosen == null && SM.HarvestResources.Raw(ResourceType.Gold) == 0 && SM.HarvestResources.Paid(ResourceType.Gold) == 0, "ClearAll also clears the carry");
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Prototip10.asset");
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double ProtoCheck()
    {
        Listen(); choiceScreens = 0;
        Require(RM.Profile.name == "Prototip10" && RM.Profile.specializationAfterSegment == 0, "10-round prototype has no specialization");
        for (int r = 1; r <= 10; r++) { PlayRound(); Flush(); }
        Require(RM.Outcome == RunOutcome.Victory && choiceScreens == 0 && !SM.IsPending, "10-round prototype: victory at 10, no choice screen");
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "UzunRun130.asset");
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double LongRunCheck()
    {
        Listen(); choiceScreens = 0;
        Require(RM.Profile.name == "UzunRun130", "Long-run profile loaded");
        for (int r = 1; r <= 10; r++) { PlayRound(); Flush(); }
        Require(RM.CurrentRound == 10 && State == GameStates.RoundEnd && choiceScreens == 0 && !SM.IsPending, "Long run: round 10 ends normally, no choice screen");
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Uzmanlasma20.asset");
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    // ---------------- ölçüm ----------------
    // Saldırı 0,8 sn, alan 1,6, kritik taban (%30 × 2). 5×5 alan, 25 adet 1×1 saksı. Stat/can eşikleri:
    //  hasar 6 · R12 (Bölüm 2 ölçümü), hasar 10 · R14, hasar 14 · R12 (davranış ×1,25 patlaması Common'ı tek vuruşta keser),
    //  hasar 30 · R14 (simülatörün deneyimli oyuncusuna yakın: doğrudan vuruş çoğu bitkiyi tek seferde keser).
    static readonly string[] Layouts = { "Doğrudan ağırlıklı", "Davranışlı", "Davranış yoğun" };
    static readonly (string name, float damage, int round)[] StatSets = { ("hasar 6 · R12 canı", 6f, 12), ("hasar 10 · R14 canı", 10f, 14), ("hasar 14 · R12 canı", 14f, 12), ("hasar 30 · R14 canı", 30f, 14) };
    const int Seeds = 4;
    const float FrameTime = 1f / 30f;
    static SpecializationSO control; // yalnız ölçüm: Davranış Ustası'nın kaynak bedelsiz kopyası (asset değil)
    static int config; // düzen × stat seti
    static readonly List<(int config, string option, int seed, Metrics m)> results = new();
    static Bot bot;
    static int Layout => config / StatSets.Length;
    static (string name, float damage, int round) StatSet => StatSets[config % StatSets.Length];
    static string ConfigName(int c) => $"{Layouts[c / StatSets.Length]} · {StatSets[c % StatSets.Length].name}";

    static void SetupLayout()
    {
        LoadSpecs();
        Require(RM.Profile.name == "Uzmanlasma20", $"Measurement run ({ConfigName(config)})");
        GridUnlockManager.Instance.UnlockNextTier(5);
        var stats = StatManager.Instance;
        stats.AddGlobalModifier(Mod(StatType.HarvestDamage, StatTarget.Player, StatSet.damage));
        stats.AddGlobalModifier(Mod(StatType.AttackSpeed, StatTarget.Player, .8f));
        stats.AddGlobalModifier(Mod(StatType.AreaRadius, StatTarget.Player, 1.6f));
        var spots = new[] { (-1, -1), (1, 1), (-2, 0), (2, 0), (0, -2), (0, 2), (-1, 1), (1, -1) };
        var behaviorTiles = new[] { ("Explosive", -1f), ("Explosive", -1f), ("Electric", -1f), ("Electric", -1f), ("Boomerang", -1f), ("Boomerang", -1f), ("Tornado", .6f), ("Tornado", .6f) };
        var cycle = new[] { ("Explosive", -1f), ("Electric", -1f), ("Boomerang", -1f), ("Tornado", .6f) };
        int ring = 0;
        FillOpen((dx, dz) =>
        {
            if (Layout == 2)
            {
                // Dış halka (16 hücre) davranış, iç 3×3 düz.
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) < 2) return (null, -1f);
                var t = cycle[ring++ % cycle.Length];
                return (Tile(t.Item1, "Legendary"), t.Item2);
            }
            int k = Array.IndexOf(spots, (dx, dz));
            if (k < 0) return (null, -1f);
            if (Layout == 0) return k < 6 ? (Tile("Damage", "Rare"), -1f) : (null, -1f);
            return (Tile(behaviorTiles[k].Item1, "Legendary"), behaviorTiles[k].Item2);
        });
        Require(Spawners().Count == 25, "25 production points");
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        bot = new GameObject("Harvest bot (verification)").AddComponent<Bot>();
        SetF(RM, "awaitingFirstRound", false);
        if (config % StatSets.Length == 0)
            Note($"düzen {Layouts[Layout]}: " + (Layout == 0 ? "6 Odak (Damage-Rare) tile, davranış yok"
                : Layout == 1 ? "Legendary davranış: 2 patlama, 2 elektrik, 2 bumerang (şans üst sınır), 2 kasırga (%60)"
                : "Legendary davranış dış halkada 16 hücre (4'er patlama/elektrik/bumerang/kasırga), iç 3×3 düz"));
    }

    static int phase, index; static bool roundRunning; // çift phase: yapılandırma kurulumu (yeniden yükleme sonrası), tek: ölçümü

    static SpecializationSO[] Order()
    {
        if (control == null)
        {
            control = ScriptableObject.CreateInstance<SpecializationSO>();
            control.displayName = "KONTROL: DAVRANIŞ ×1,25 BEDELSİZ";
            control.behaviorDamageMultiplier = davranis.behaviorDamageMultiplier;
        }
        return new[] { usta, davranis, koru, control };
    }

    static int Configs => Layouts.Length * StatSets.Length;

    static double MeasureStep()
    {
        if (phase % 2 == 0) { config = phase / 2; SetupLayout(); phase++; index = 0; return .3; }
        if (roundRunning)
        {
            if (State == GameStates.Round) return .25;
            Collect(); roundRunning = false;
        }
        var order = Order();
        if (index == Seeds * order.Length)
        {
            Report(config);
            if (config < Configs - 1) { phase++; Time.captureDeltaTime = 0f; SceneManager.LoadScene("GameScene"); return 3; }
            Summary();
            return -1;
        }
        BeginRound(order[index / Seeds], index % Seeds);
        index++; roundRunning = true;
        return .5;
    }

    static Metrics current; static SpecializationSO currentOption; static int currentSeed;

    sealed class Metrics
    {
        public int[] kills = new int[5], triggered = new int[5], skipped = new int[5];
        public int electricVisualSkips, attacks;
        public long score; public double xp; public int gold, iron, stone;
        public int Kills => kills.Sum();
        public int BehaviorKills => kills[1] + kills[2] + kills[3] + kills[4];
        public int Triggers => triggered[1] + triggered[2] + triggered[3] + triggered[4];
        public int Skips => skipped[1] + skipped[2] + skipped[3] + skipped[4];
        public int Resources => gold + iron + stone;
    }

    static long score0; static double xp0; static int gold0, iron0, stone0, visual0;

    static void BeginRound(SpecializationSO option, int seed)
    {
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) { }
        foreach (var s in Spawners()) { s.RemoveSpawnedPlant(); s.enabled = true; SetF(s, "timer", 0f); }
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        HarvestBehaviorStats.Reset(); bot.ResetCounts();
        SetP(SM, "Chosen", option); SM.HarvestResources.Clear();
        currentOption = option; currentSeed = seed; current = new Metrics();
        score0 = Score; xp0 = Xp; visual0 = HarvestBehaviorManager.Instance.SkippedElectricVisuals;
        gold0 = Res(ResourceType.Gold); iron0 = Res(ResourceType.Iron); stone0 = Res(ResourceType.Stone);
        UnityEngine.Random.InitState(900 + seed);
        SetP(RM, "CurrentRound", StatSet.round - 1);
        RM.StartNextRound();
        if (RM.CurrentRound != StatSet.round || State != GameStates.Round) throw new Exception("measurement round did not start: " + State + " " + RM.CurrentRound);
        // Sabit kare süresi: sonuç makinenin kare hızına bağlı olmasın (aynı seed → aynı oyun zamanı adımları).
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void Collect()
    {
        for (int t = 0; t < 5; t++) { current.kills[t] = bot.Kills[t]; current.triggered[t] = HarvestBehaviorStats.Triggered((DamageType)t); current.skipped[t] = HarvestBehaviorStats.Skipped((DamageType)t); }
        current.electricVisualSkips = HarvestBehaviorManager.Instance.SkippedElectricVisuals - visual0;
        current.attacks = bot.Attacks;
        current.score = Score - score0; current.xp = Xp - xp0;
        current.gold = Res(ResourceType.Gold) - gold0; current.iron = Res(ResourceType.Iron) - iron0; current.stone = Res(ResourceType.Stone) - stone0;
        results.Add((config, currentOption.displayName, currentSeed, current));
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) { }
    }

    static string Stat(IEnumerable<double> values, string format = "0")
    {
        var v = values.ToList();
        return $"{v.Average().ToString(format)} [{v.Min().ToString(format)}–{v.Max().ToString(format)}]";
    }

    static void Report(int c)
    {
        var rows = results.Where(r => r.config == c).ToList();
        Note($"--- {ConfigName(c)} ({Seeds} seed × 30 sn round, kare 1/30 sn; ortalama [en az–en çok]) ---");
        Note("seçenek | toplam hasat | doğrudan | davranış (P/K/B/E ort.) | skor | XP | Gold/Iron/Stone ort. | kaynak toplamı | tetik P/K/B/E ort. (atlanan) | vuruş");
        var koruRows = rows.Where(r => r.option == koru.displayName).ToList();
        foreach (var option in Order())
        {
            var m = rows.Where(r => r.option == option.displayName).Select(r => r.m).ToList();
            double A(Func<Metrics, double> f) => m.Average(f);
            Note($"{option.displayName} | {Stat(m.Select(x => (double)x.Kills))} | {Stat(m.Select(x => (double)x.kills[0]))} | {Stat(m.Select(x => (double)x.BehaviorKills))} ({A(x => x.kills[1]):0.#}/{A(x => x.kills[2]):0.#}/{A(x => x.kills[3]):0.#}/{A(x => x.kills[4]):0.#}) | " +
                 $"{Stat(m.Select(x => (double)x.score))} | {Stat(m.Select(x => x.xp))} | {A(x => x.gold):0}/{A(x => x.iron):0}/{A(x => x.stone):0} | {Stat(m.Select(x => (double)x.Resources))} | " +
                 $"{A(x => x.triggered[1]):0.#}/{A(x => x.triggered[2]):0.#}/{A(x => x.triggered[3]):0.#}/{A(x => x.triggered[4]):0.#} ({m.Sum(x => x.Skips)}) | {A(x => x.attacks):0}");
        }
        // Koru'ya göre, aynı seed çiftleri üzerinden.
        foreach (var option in new[] { usta, davranis, control })
        {
            var pairs = rows.Where(r => r.option == option.displayName).Join(koruRows, a => a.seed, b => b.seed, (a, b) => (a: a.m, b: b.m)).ToList();
            string Rel(Func<Metrics, double> f) { double a = pairs.Sum(p => f(p.a)), b = pairs.Sum(p => f(p.b)); return b > 0 ? $"{(a / b - 1) * 100:+0;-0;0}%" : "-"; }
            int wins = pairs.Count(p => p.a.score > p.b.score), losses = pairs.Count(p => p.a.score < p.b.score);
            Note($"  {option.displayName} / Koru: hasat {Rel(x => x.Kills)} · skor {Rel(x => x.score)} (seed bazında {wins} üstün / {losses} geride) · XP {Rel(x => x.xp)} · kaynak {Rel(x => x.Resources)} · tetik {Rel(x => x.Triggers)}");
        }
        // Kaynak bedeli tetikleri azaltıyor mu: Davranış Ustası ile bedelsiz kopyası aynı seed'de.
        var twin = rows.Where(r => r.option == davranis.displayName).Join(rows.Where(r => r.option == control.displayName), a => a.seed, b => b.seed, (a, b) => (a: a.m, b: b.m)).ToList();
        int same = twin.Count(p => p.a.Kills == p.b.Kills && p.a.Triggers == p.b.Triggers && p.a.score == p.b.score && p.a.xp == p.b.xp);
        double ratio = twin.Sum(p => (double)p.a.Resources) / Math.Max(1, twin.Sum(p => (double)p.b.Resources));
        Note($"  Davranış Ustası / bedelsiz kopya: birebir aynı hasat+tetik+skor+XP {same}/{twin.Count} seed · tetik {twin.Sum(p => p.a.Triggers)} / {twin.Sum(p => p.b.Triggers)} · hasat {twin.Sum(p => p.a.Kills)} / {twin.Sum(p => p.b.Kills)} · kaynak oranı {ratio:0.000}");
    }

    static void Summary()
    {
        Require(results.Count == Configs * 4 * Seeds, $"Measured {Layouts.Length} layouts × {StatSets.Length} stat sets × 4 options × {Seeds} seeds");
        Require(results.Where(r => r.config / StatSets.Length == 0).All(r => r.m.attacks > 0 && r.m.kills[0] > 0), "Bot harvested in every direct-layout round");
        Require(results.Where(r => r.config / StatSets.Length > 0 && r.option == koru.displayName).Sum(r => r.m.BehaviorKills) > 0, "Behaviors killed plants in the behavior layouts");
        var twin = results.Where(r => r.option == davranis.displayName).Join(results.Where(r => r.option == control.displayName), a => (a.config, a.seed), b => (b.config, b.seed), (a, b) => (a: a.m, b: b.m)).ToList();
        int same = twin.Count(p => p.a.Kills == p.b.Kills && p.a.Triggers == p.b.Triggers);
        long trigA = twin.Sum(p => (long)p.a.Triggers), trigB = twin.Sum(p => (long)p.b.Triggers);
        double ratio = twin.Sum(p => (double)p.a.Resources) / Math.Max(1, twin.Sum(p => (double)p.b.Resources));
        Note($"Davranış Ustası / bedelsiz kopya, tüm yapılandırmalar: aynı hasat+tetik {same}/{twin.Count} · tetik {trigA} / {trigB} · kaynak oranı {ratio:0.000}");
        if (same == twin.Count)
            Require(Math.Abs(ratio - .9) < .01, $"Resource cost does not change triggers or harvests (identical on every seed); resources ×{ratio:0.000}");
        else
            Note("UYARI: ölçüm seed başına birebir tekrar etmedi; tetik karşılaştırması istatistiksel okunmalı");
    }

    // Oyuncu yerine: saldırı zamanı gelince en çok canlı bitkiyi kapsayan açık hücreye vurur; kart ekranlarını bastırır (düzen değişmesin).
    sealed class Bot : MonoBehaviour
    {
        public readonly int[] Kills = new int[5];
        public int Attacks;
        float timer;
        MethodInfo attack; PlayerController player;
        void OnEnable() { PlantHealth.AnyHarvested += Count; }
        void OnDisable() { PlantHealth.AnyHarvested -= Count; }
        void Count(PlantHealth h) { if (GameManager.Instance.CurrentState == GameStates.Round) Kills[(int)h.KilledBy]++; }
        public void ResetCounts() { Array.Clear(Kills, 0, Kills.Length); Attacks = 0; timer = 0; }
        void Update()
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            SetF(RoundManager.Instance, "pendingCardSelections", 0);
            if (player == null) { player = FindFirstObjectByType<PlayerController>(); attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance); }
            float interval = Mathf.Max(StatManager.Instance.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RoundManager.Instance.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            float radius = StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var grid = GridManager.Instance.GetGridSystem();
            Vector3 best = Vector3.zero; int bestCount = -1;
            for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
            {
                var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                if (ground == null || ground.IsLocked) continue;
                int n = grid.GetGridObjectsInRadius(ground.transform.position, radius).Count(g => g.GetPlantObject() != null && g.GetPlantObject().TryGetComponent(out PlantHealth h) && !h.IsDead);
                if (n > bestCount) { bestCount = n; best = ground.transform.position; }
            }
            if (bestCount <= 0) return;
            attack.Invoke(player, new object[] { best });
            Attacks++;
        }
    }
}
