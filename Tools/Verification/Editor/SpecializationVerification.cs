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

// Batch (izole kopya): Bölüm 2 — boss sonrası bedelli uzmanlaşma, GameScene'de.
// A) Akış: kota kaybında ödül yok; kartlar → seçim → özet; tek açılış; seçim bitmeden round 11 yok; çift tıklama;
//    dört davranış ve doğrudan hasar katsayıları gerçek kod yolundan; Koru hiçbir şeyi değiştirmez; yeni run temiz;
//    10 round ve uzun run profillerinde seçim ekranı yok.
// B) Ölçüm: iki tarla düzeni × üç seçenek, aynı seed ve round, bot oyuncu (en çok bitkiyi kapsayan hücreye vurur).
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
            case 4: return FinishRun();
            case 5: Save("SpecVictory"); Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed(); return 3;
            case 6: return AfterRestart();
            case 7: return ProtoCheck();
            case 8: return LongRunCheck();
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
        Require(SM.Chosen == null && !SM.IsPending && SpecializationManager.DirectMultiplier == 1f && SpecializationManager.BehaviorMultiplier == 1f, "No specialization at start");
        for (int r = 1; r <= 9; r++) { PlayRound(); Flush(); }
        PlayRound(0.9);
        Require(RM.EndedByQuota && State == GameStates.RunComplete && !SM.IsPending && SM.Chosen == null && choiceScreens == 0, "Failed boss quota: run lost, no specialization screen");
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
        return 3;
    }

    // Run 2: round 10 kotası geçilir, iki kart bekler → kartlar → seçim ekranı.
    static double SuccessToChoice()
    {
        Listen(); LoadSpecs();
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        PrepareCapture();
        choiceScreens = cardScreens = 0;
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            Place1x1(Grid.GetGridObject(new GridPosition(Center + x, Center + z)), x == 0 && z == 0 ? Tile("Explosive", "Legendary") : null, 1f);
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        for (int r = 1; r <= 9; r++) { PlayRound(); Flush(); }
        int cardsBefore = cardScreens;
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
        var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeInHierarchy).Select(t => Strip(t.text)).ToList();
        Require(texts.Any(t => t == "USTA BİÇİCİ") && texts.Any(t => t == "DAVRANIŞ USTASI") && texts.Any(t => t == "MEVCUT DÜZENİ KORU"), "Three options shown");
        Require(texts.Contains("Doğrudan hasar ×1,25") && texts.Any(t => t.StartsWith("Davranış hasarı ×0,85")) && texts.Any(t => t.StartsWith("Davranış hasarı ×1,25")) && texts.Contains("Doğrudan hasar ×0,85"),
            "Gains and costs visible before choosing");
        Require(texts.Contains("Bonus yok, ceza yok"), "Keep-current option is explicit");
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
        Require(StatManager.Instance.GlobalModifiers.Count == globalsBefore && SpecializationManager.DirectMultiplier == 1.25f && SpecializationManager.BehaviorMultiplier == .85f,
            "Choice adds no stat modifiers; only the two coefficients (×1,25 / ×0,85)");
        Require(choiceScreens == 1, "Specialization screen opened exactly once");
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Call(summary, "Fill", GameFeelDirector.Instance.LastRound);
        var spec = summary.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Specialization Notice");
        Require(spec != null && spec.gameObject.activeInHierarchy && Strip(spec.text) == "Uzmanlaşma: USTA BİÇİCİ · doğrudan ×1,25 · davranış ×0,85", "Summary shows the choice: " + Strip(spec?.text));
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
        var noCrit = new StatModifier { statType = StatType.CritChance, target = StatTarget.Player, operation = ModifierOperation.Set, value = 0f };
        var oneCell = new StatModifier { statType = StatType.AreaRadius, target = StatTarget.Player, operation = ModifierOperation.Set, value = .1f };
        var dmg10 = new StatModifier { statType = StatType.HarvestDamage, target = StatTarget.Player, operation = ModifierOperation.Set, value = 10f };
        stats.AddGlobalModifier(noCrit); stats.AddGlobalModifier(oneCell); stats.AddGlobalModifier(dmg10);
        var player = Object.FindFirstObjectByType<PlayerController>();
        var center = Grid.GetGridObject(new GridPosition(Center, Center));
        var corner = Grid.GetGridObject(new GridPosition(Center - 1, Center - 1));
        var east = Grid.GetGridObject(new GridPosition(Center + 1, Center));
        var cornerBrain = corner.GetPlanterBrain(); var centerBrain = center.GetPlanterBrain();
        var tornadoes = TornadoManager.Instance; var behaviors = HarvestBehaviorManager.Instance;
        var results = new Dictionary<string, int[]>();
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
            var big = new StatModifier { statType = StatType.HarvestDamage, target = StatTarget.Player, operation = ModifierOperation.Set, value = 100f };
            stats.RemoveGlobalModifier(dmg10); stats.AddGlobalModifier(big);
            var neighbor = east.GetPlantObject().GetComponent<PlantHealth>(); int nHp = neighbor.CurrentHealth;
            Call(player, "AttackInRadius", center.GetGroundCellCached().transform.position);
            int explosionDmg = nHp - neighbor.CurrentHealth;
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
        Require(results["Davranış"][4] == 125 && results["Usta"][4] == 85, "Direct penalty does not leak into behavior base (explosion 125 with Davranış Ustası)");
        stats.RemoveGlobalModifier(noCrit); stats.RemoveGlobalModifier(oneCell); stats.RemoveGlobalModifier(dmg10);
        SetP(SM, "Chosen", usta);
        return .3;
    }

    static double FinishRun()
    {
        Call(RM, "EndRound"); Flush();
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

    static double AfterRestart()
    {
        Require(SceneManager.GetActiveScene().name == "GameScene" && State == GameStates.RunSetup && SM != null && SM.Chosen == null && !SM.IsPending, "New run: specialization cleared");
        Require(SpecializationManager.DirectMultiplier == 1f && SpecializationManager.BehaviorMultiplier == 1f, "New run: coefficients back to 1");
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
    // Varsayılan orta oyun: hasat hasarı 6, saldırı 0,8 sn, alan 1,6, kritik taban (%30 × 2). Round 12 bitki canı. 5×5 alan, 25 adet 1×1 saksı.
    static readonly string[] Layouts = { "Doğrudan ağırlıklı", "Davranışlı", "Davranış yoğun" };
    static SpecializationSO candidate; // yalnız ölçüm: Davranış Ustası için aday değerler (asset değil)
    static int layout;
    static readonly List<(string layout, string option, Metrics m)> results = new();
    static Bot bot;

    static void SetupLayout()
    {
        LoadSpecs();
        Require(RM.Profile.name == "Uzmanlasma20", $"Measurement run ({Layouts[layout]})");
        GridUnlockManager.Instance.UnlockNextTier(5);
        var stats = StatManager.Instance;
        stats.AddGlobalModifier(new StatModifier { statType = StatType.HarvestDamage, target = StatTarget.Player, operation = ModifierOperation.Set, value = 6f });
        stats.AddGlobalModifier(new StatModifier { statType = StatType.AttackSpeed, target = StatTarget.Player, operation = ModifierOperation.Set, value = .8f });
        stats.AddGlobalModifier(new StatModifier { statType = StatType.AreaRadius, target = StatTarget.Player, operation = ModifierOperation.Set, value = 1.6f });
        var spots = new[] { (-1, -1), (1, 1), (-2, 0), (2, 0), (0, -2), (0, 2), (-1, 1), (1, -1) };
        var behaviorTiles = new[] { ("Explosive", -1f), ("Explosive", -1f), ("Electric", -1f), ("Electric", -1f), ("Boomerang", -1f), ("Boomerang", -1f), ("Tornado", .6f), ("Tornado", .6f) };
        var cycle = new[] { ("Explosive", -1f), ("Electric", -1f), ("Boomerang", -1f), ("Tornado", .6f) };
        int ring = 0;
        FillOpen((dx, dz) =>
        {
            if (layout == 2)
            {
                // Dış halka (16 hücre) davranış, iç 3×3 düz.
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) < 2) return (null, -1f);
                var t = cycle[ring++ % cycle.Length];
                return (Tile(t.Item1, "Legendary"), t.Item2);
            }
            int k = Array.IndexOf(spots, (dx, dz));
            if (k < 0) return (null, -1f);
            if (layout == 0) return k < 6 ? (Tile("Damage", "Rare"), -1f) : (null, -1f);
            return (Tile(behaviorTiles[k].Item1, "Legendary"), behaviorTiles[k].Item2);
        });
        Require(Spawners().Count == 25, "25 production points");
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        bot = new GameObject("Harvest bot (verification)").AddComponent<Bot>();
        SetP(RM, "CurrentRound", 11); SetF(RM, "awaitingFirstRound", false);
        Note($"düzen {Layouts[layout]}: " + (layout == 0 ? "6 Odak (Damage-Rare) tile, davranış yok"
            : layout == 1 ? "Legendary davranış: 2 patlama, 2 elektrik, 2 bumerang (şans üst sınır), 2 kasırga (%60)"
            : "Legendary davranış dış halkada 16 hücre (4'er patlama/elektrik/bumerang/kasırga), iç 3×3 düz"));
    }

    const int Repeats = 3;
    static int phase, index; // çift: düzen kurulumu (yeniden yükleme sonrası), tek: o düzenin ölçümü

    static SpecializationSO[] Order()
    {
        if (candidate == null)
        {
            candidate = ScriptableObject.CreateInstance<SpecializationSO>();
            candidate.displayName = "ADAY DAVRANIŞ ×1,5 / DOĞRUDAN ×0,95";
            candidate.directDamageMultiplier = .95f; candidate.behaviorDamageMultiplier = 1.5f;
        }
        return new[] { usta, davranis, koru, candidate };
    }

    static double MeasureStep()
    {
        if (phase % 2 == 0) { layout = phase / 2; SetupLayout(); phase++; index = 0; return .3; }
        if (index > 0) Collect();
        var order = Order();
        if (index == Repeats * order.Length)
        {
            Report(layout);
            if (layout < Layouts.Length - 1) { phase++; SceneManager.LoadScene("GameScene"); return 3; }
            Summary();
            return -1;
        }
        BeginRound(order[index / Repeats], index % Repeats);
        index++;
        return 9.5;
    }

    static Metrics current; static string currentOption;

    sealed class Metrics
    {
        public int[] kills = new int[5], triggered = new int[5], skipped = new int[5];
        public int electricVisualSkips, attacks, rounds;
        public long score; public double xp; public int gold, iron, stone;
    }

    static long score0; static double xp0; static int gold0, iron0, stone0, visual0;

    static void BeginRound(SpecializationSO option, int repeat)
    {
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) { }
        foreach (var s in Spawners()) { s.RemoveSpawnedPlant(); s.enabled = true; SetF(s, "timer", 0f); }
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        HarvestBehaviorStats.Reset(); bot.ResetCounts();
        SetP(SM, "Chosen", option);
        currentOption = option.displayName;
        current = results.Where(r => r.layout == Layouts[layout] && r.option == currentOption).Select(r => r.m).FirstOrDefault();
        if (current == null) { current = new Metrics(); results.Add((Layouts[layout], currentOption, current)); }
        score0 = HarvestScoreManager.Instance.TotalScore; xp0 = ProgressionManager.Instance.TotalXPEarned; visual0 = HarvestBehaviorManager.Instance.SkippedElectricVisuals;
        var r = ResourceManager.Instance; gold0 = r.GetResourceAmount(ResourceType.Gold); iron0 = r.GetResourceAmount(ResourceType.Iron); stone0 = r.GetResourceAmount(ResourceType.Stone);
        UnityEngine.Random.InitState(900 + repeat);
        SetP(RM, "CurrentRound", 11);
        RM.StartNextRound();
        if (RM.CurrentRound != 12 || State != GameStates.Round) throw new Exception("measurement round did not start: " + State + " " + RM.CurrentRound);
        Time.timeScale = 4f;
    }

    static void Collect()
    {
        if (State == GameStates.Round) throw new Exception("measurement round still running");
        for (int t = 0; t < 5; t++) { current.kills[t] += bot.Kills[t]; current.triggered[t] += HarvestBehaviorStats.Triggered((DamageType)t); current.skipped[t] += HarvestBehaviorStats.Skipped((DamageType)t); }
        current.electricVisualSkips += HarvestBehaviorManager.Instance.SkippedElectricVisuals - visual0;
        current.attacks += bot.Attacks; current.rounds++;
        current.score += HarvestScoreManager.Instance.TotalScore - score0; current.xp += ProgressionManager.Instance.TotalXPEarned - xp0;
        var r = ResourceManager.Instance;
        current.gold += r.GetResourceAmount(ResourceType.Gold) - gold0; current.iron += r.GetResourceAmount(ResourceType.Iron) - iron0; current.stone += r.GetResourceAmount(ResourceType.Stone) - stone0;
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) { }
    }

    static void Report(int l)
    {
        Note($"--- {Layouts[l]} ({Repeats} round × 30 sn, round 12 canı, aynı seed'ler; ADAY yalnız ölçüm) ---");
        Note("seçenek | doğrudan öldürme | davranış öldürme (patlama/kasırga/bumerang/elektrik) | skor | altın/demir/taş | XP | tetik (atlanan) P/K/B/E | elektrik görseli atlanan | vuruş");
        foreach (var (lay, option, m) in results.Where(r => r.layout == Layouts[l]))
        {
            int behaviorKills = m.kills[1] + m.kills[2] + m.kills[3] + m.kills[4];
            Note($"{option} | {m.kills[0]} | {behaviorKills} ({m.kills[1]}/{m.kills[2]}/{m.kills[3]}/{m.kills[4]}) | {m.score} | {m.gold}/{m.iron}/{m.stone} | {m.xp:0} | " +
                 $"{m.triggered[1]}({m.skipped[1]})/{m.triggered[2]}({m.skipped[2]})/{m.triggered[3]}({m.skipped[3]})/{m.triggered[4]}({m.skipped[4]}) | {m.electricVisualSkips} | {m.attacks}");
        }
    }

    static void Summary()
    {
        Require(results.Count == Layouts.Length * 4 && results.All(r => r.m.rounds == Repeats), $"Measured {Layouts.Length} layouts × 4 options × {Repeats} rounds");
        var koruDirect = results.First(r => r.layout == Layouts[0] && r.option == koru.displayName).m;
        Require(koruDirect.attacks > 0 && koruDirect.kills[0] > 0, "Bot harvested in the direct layout");
        var koruBehavior = results.First(r => r.layout == Layouts[1] && r.option == koru.displayName).m;
        Require(koruBehavior.kills[1] + koruBehavior.kills[2] + koruBehavior.kills[3] + koruBehavior.kills[4] > 0, "Behaviors killed plants in the behavior layout");
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
