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

// Batch (izole kopya): Bölüm 1 prototipi GameScene'de uçtan uca.
// Profil ve ekonomi, kota altı/eşit/üstü, segment sıfırlama, Don Cephesi önizleme = uygulama, yalnız şeritteki üretim
// noktalarının yavaşlaması (aynı seed ile açık/kapalı sayım), temizlik, zafer, kayıp, yeniden başlatma, ana menü.
// Ayrıca: long skor, elektrik havuzu doluyken hasar, saldırı zamanlayıcısı taşması.
[InitializeOnLoad]
public static class RunPrototypeVerification
{
    const string Key = "RunPrototypeVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfilePath = "Assets/ScriptableObjects/RunProfiles/Prototip10.asset";
    const string FrostPath = "Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset";
    const int FrostSeed = 7;
    static readonly List<string> notes = new();
    static int step; static double nextAt;
    static RenderTexture target; static Camera cam;
    static List<GridPosition> previewZone;
    static SpawnCounter counter;
    static Dictionary<PlantSpawner, int> countOn, countOff;
    static long segmentStart;

    static RunPrototypeVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: prototip profilini seç, Don Cephesi seçimini sabitle.
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfilePath);
        var frost = AssetDatabase.LoadAssetAtPath<FrostFrontSO>(FrostPath);
        frost.seed = FrostSeed;
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
        File.WriteAllLines("Logs/RunPrototypeVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", "<[^>]+>", "").Replace('\n', '/');
    static RoundManager RM => RoundManager.Instance;
    static SegmentEventDirector Events => SegmentEventDirector.Instance;
    static void SetScore(long value) => SetF(HarvestScoreManager.Instance, "totalScore", value);
    static float Interval(PlantSpawner s) => (float)Call(s, "GetEffectiveSpawnInterval");
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
    static FrostFrontEvent Frost => Events.Events.OfType<FrostFrontEvent>().FirstOrDefault();
    // Olaysız beklenen aralık: PlantSpawner'ın kendi formülü (taban, tempo) canlı stat'lardan.
    static float NoEvent(PlantSpawner s) => Mathf.Max(StatCalculator.MinimumSpawnInterval, s.GetComponentInParent<PlanterBrain>().GetFinalStat(StatType.PlantSpawnRate)) / RM.TempoMultiplier;
    // Kart ekranlarını geçer; ölçümü bozmamak için üretim aralığını değiştirmeyen teklifi seçer (yoksa atlar).
    static void Flush()
    {
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        while (GameManager.Instance.CurrentState == GameStates.CardSelection)
        {
            var offers = F<List<TileCardOffer>>(cards, "currentCards");
            var pick = offers.FirstOrDefault(o => o.Modifiers.All(m => m.statType != StatType.PlantSpawnRate));
            if (pick == null && RM.SkipUsesRemaining > 0) { Call(cards, "OnSkipPressed"); continue; }
            if (pick == null) { pick = offers[0]; Note("only spawn-rate cards offered; picked one"); }
            Call(cards, "OnCardSelected", pick);
        }
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Setup();
            case 1: return Preview();
            case 2: return SegmentOne();
            case 3: return SegmentOneEnd();
            case 4: return BossStart();
            case 5: return MeasureOn();
            case 6: return MeasureOff();
            case 7: return BossRoundEndShot();
            case 8: return Victory();
            case 9: Save("ProtoVictory"); Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed(); return 3;
            case 10: return Restart();
            case 11: return ElectricAndLongScore();
            case 12: return Defeat();
            case 13: Save("ProtoDefeat"); return MainMenu();
            case 14: return MenuCheck();
            default: return -1;
        }
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

    // 3×3 açık alan: şeride yarısı giren bir 2×2 ve kalan hücrelere 1×1 saksılar.
    static void PlantField()
    {
        var grid = GridManager.Instance.GetGridSystem();
        var open = new List<GridPosition>();
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        { var g = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && !g.IsLocked) open.Add(new GridPosition(x, z)); }
        int minX = open.Min(p => p.x), maxX = open.Max(p => p.x), minZ = open.Min(p => p.z), maxZ = open.Max(p => p.z);
        var frost = Frost;
        // 2×2'yi şeridin bulunduğu köşeye koy: iki hücresi şeritte, ikisi dışarıda.
        bool band(GridPosition p) => frost.Covers(p);
        int ox = band(new GridPosition(maxX, minZ + 1)) && !band(new GridPosition(minX, minZ + 1)) ? maxX - 1 : minX;
        int oz = band(new GridPosition(minX + 1, maxZ)) && !band(new GridPosition(minX + 1, minZ)) ? maxZ - 1 : minZ;
        var big = new List<GridObject>();
        for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++) big.Add(grid.GetGridObject(new GridPosition(ox + x, oz + z)));
        Place("Assets/ScriptableObjects/Planters/GrassPlanter 2x2.asset", big, new Vector3(1, 0, 1));
        foreach (var p in open)
        {
            var cell = grid.GetGridObject(p);
            if (cell.GetPlanterBrain() != null) continue;
            Place("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset", new List<GridObject> { cell }, Vector3.zero);
        }
    }

    static void Place(string path, List<GridObject> cells, Vector3 offset)
    {
        var data = AssetDatabase.LoadAssetAtPath<PlanterSO>(path);
        var planter = Object.Instantiate(data.prefab);
        planter.transform.position = cells[0].GetGroundCellCached().transform.position + offset;
        foreach (var g in cells) g.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(data, cells); foreach (var g in cells) g.SetPlanterBrain(brain);
    }

    static double Setup()
    {
        UnityEngine.Random.InitState(1234);
        Require(GameManager.Instance.CurrentState == GameStates.RunSetup, "Scene starts in RunSetup");
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        PrepareCapture();
        Require(RM.Profile != null && RM.Profile.name == "Prototip10" && RM.MaxRounds == 10 && RM.QuotaSegmentRounds == 5, "Prototype profile: 10 rounds, 5-round segments");
        Require(RM.QuotaTargetFor(1) == 40 && RM.QuotaTargetFor(2) == 200, "Segment targets from the profile table: 40 / 200");
        var res = ResourceManager.Instance;
        Require(res.GetResourceAmount(ResourceType.Gold) == 80 && res.GetResourceAmount(ResourceType.Iron) == 0 && res.GetResourceAmount(ResourceType.Stone) == 0,
            "Normal starting economy from the profile (scene debug budget ignored): 80 / 0 / 0");
        Require(Events != null && Events.GetComponent<RoundManager>() == RM && Events.Events.Count == 1 && Frost != null, "Event director on RoundManager with one Don Cephesi event");
        Require(Frost.StartRound == 6 && Frost.EndRound == 10 && Frost.AnnounceRound == 1, "Don Cephesi: rounds 6–10, announced from round 1");
        return .5;
    }

    static double Preview()
    {
        var frost = Frost;
        Require(frost.IsPrepared && !frost.IsActive && Events.Upcoming == frost && Events.Active == null, "Zone chosen during setup, not active yet");
        var grid = GridManager.Instance.GetGridSystem();
        var openZone = frost.ZoneCells.Where(p => { var g = grid.GetGridObject(p)?.GetGroundCellCached(); return g != null && !g.IsLocked; }).ToList();
        int open = 0; for (int x = 0; x < 11; x++) for (int z = 0; z < 11; z++) { var g = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && !g.IsLocked) open++; }
        Require(open == 9 && openZone.Count == 3, $"Band covers 3 of the 9 open cells ({frost.BandName})");
        previewZone = frost.ZoneCells.ToList();
        var markers = Object.FindAnyObjectByType<FrostZoneMarkers>();
        Require(markers != null && !markers.ShowsActive && markers.ShownCells.OrderBy(p => p.x * 100 + p.z).SequenceEqual(openZone.OrderBy(p => p.x * 100 + p.z)), "World preview shows exactly the open band cells (hatched)");
        var map = Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
        var marked = map.GetComponentsInChildren<TileCellUI>(false).Where(t => t.InEventZone).Select(t => t.name).OrderBy(n => n).ToList();
        var expectedMarks = openZone.Select(p => $"Cell_x{p.x}_z{p.z}").OrderBy(n => n).ToList();
        Require(marked.SequenceEqual(expectedMarks), "Round map marks the same band cells: " + string.Join(",", marked));
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Call(summary, "Fill", new object[] { null });
        var texts = summary.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeInHierarchy).ToDictionary(t => t.name, t => t.text);
        Require(texts.TryGetValue("Title", out var title) && title == "HAZIRLIK", "Setup card title HAZIRLIK");
        Require(texts.TryGetValue("Quota Notice", out var q) && Strip(q).StartsWith("İlk kota: 40 · 5 round"), "Setup shows the first quota: " + Strip(q));
        Require(texts.TryGetValue("Event Notice", out var e) && Strip(e).StartsWith("Yaklaşan: DON CEPHESİ · Round 6–10") && Strip(e).Contains("5 round hazırlık"), "Setup announces the event: " + Strip(e));
        PlantField();
        var spawners = Spawners();
        Require(spawners.Count == 9, "9 production points planted (one 2×2 + five 1×1): " + spawners.Count);
        var big = spawners.Where(s => s.GetComponentInParent<PlanterBrain>().OccupiedGrids.Count == 4).ToList();
        Require(big.Count == 4 && big.Count(s => frost.Covers(s.GridObject.GetGridPosition())) == 2, "The 2×2 planter straddles the band: 2 points in, 2 out");
        Require(spawners.All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f && Mathf.Abs(Interval(s) - NoEvent(s)) < 1e-4f), "Preview does not slow anything");
        Save("ProtoSetup");
        return .3;
    }

    // Round 1: kart seçimi ve dükkân akışı; round 2–4 hızlı.
    static double SegmentOne()
    {
        RM.StartNextRound();
        Require(RM.CurrentRound == 1 && GameFeelDirector.QuotaIntro(RM) == "KOTA 40 · 5 ROUND" && Events.Active == null, "Round 1: quota 40 announced, no event active");
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        Call(RM, "EndRound");
        Require(GameManager.Instance.CurrentState == GameStates.CardSelection, "Level-up opens card selection");
        Flush();
        Require(GameManager.Instance.CurrentState == GameStates.RoundEnd, "Card picked → round end");
        GameManager.Instance.OpenShop();
        Require(GameManager.Instance.CurrentState == GameStates.Shop, "Shop opens");
        for (int r = 2; r <= 4; r++)
        {
            RM.StartNextRound();
            Call(RM, "EndRound");
            Flush();
        }
        Require(RM.CurrentRound == 4 && GameManager.Instance.CurrentState == GameStates.RoundEnd && !RM.EndedByQuota, "Rounds 2–4 flow without judging the quota");
        return .3;
    }

    static double SegmentOneEnd()
    {
        RM.StartNextRound();
        Require(RM.CurrentRound == 5, "Round 5 running");
        long total = HarvestScoreManager.Instance.TotalScore, start = total - RM.QuotaProgress;
        SetScore(start + 40);
        Call(RM, "EndRound");
        Require(!RM.EndedByQuota && RM.LastQuotaRound == 5 && RM.LastQuotaScore == 40 && RM.LastQuotaTarget == 40, "Exactly 40 / 40 passes (equality is success)");
        Require(Events.Active == null && Frost.IsPrepared, "Event still upcoming after round 5");
        Flush();
        string quota = RoundSummaryUI.QuotaNotice(RM, 5, out var tone);
        Require(Strip(quota).StartsWith("KOTA TAMAM · 40 / 40") && quota.Contains("Sıradaki boss kotası: 200") && tone == RoundSummaryUI.QuotaTone.Done, "Round 5 summary: " + Strip(quota));
        string ev = SegmentEventText.Notice(Events, 5);
        Require(Strip(ev).StartsWith("SON HAZIRLIK · DON CEPHESİ sonraki round"), "Last preparation is flagged: " + Strip(ev));
        return .3;
    }

    static double BossStart()
    {
        long totalBefore = HarvestScoreManager.Instance.TotalScore;
        RM.StartNextRound();
        var frost = Frost;
        Require(RM.CurrentRound == 6 && Events.Active == frost && frost.IsActive && RM.QuotaProgress == 0 && RM.QuotaTarget == 200, "Round 6: Don Cephesi active, segment progress reset, boss quota 200");
        Require(HarvestScoreManager.Instance.TotalScore == totalBefore, "Run total score kept across the segment reset");
        Require(frost.ZoneCells.SequenceEqual(previewZone), "Applied zone == previewed zone");
        Require(SegmentEventText.Intro(Events, RM) == "DON CEPHESİ BAŞLADI · KOTA 200", "Boss start intro: " + SegmentEventText.Intro(Events, RM));
        var spawners = Spawners();
        foreach (var s in spawners)
        {
            bool inBand = frost.Covers(s.GridObject.GetGridPosition());
            float expected = NoEvent(s) * (inBand ? 1.5f : 1f);
            if (Mathf.Abs(Interval(s) - expected) > 1e-4f) throw new Exception($"interval {Interval(s)} expected {expected} (band {inBand})");
        }
        Require(true, "Only band production points ×1.5 (" + spawners.Count(s => frost.Covers(s.GridObject.GetGridPosition())) + " of " + spawners.Count + "); the 2×2 is not slowed as a whole");
        var brain = spawners.First(s => frost.Covers(s.GridObject.GetGridPosition())).GetComponentInParent<PlanterBrain>();
        float rarityBefore = brain.GetFinalStat(StatType.RareSpawnChance);
        // Tabandaki saksı: üretim tabanı (0,5 sn) Don'dan önce uygulanır; şeritte 0,75 sn olur, nadirlik değişmez.
        var fast = new StatModifier { statType = StatType.PlantSpawnRate, target = StatTarget.Planter, operation = ModifierOperation.Set, value = .1f };
        StatManager.Instance.AddGlobalModifier(fast);
        var inside = spawners.First(s => frost.Covers(s.GridObject.GetGridPosition())); var outside = spawners.First(s => !frost.Covers(s.GridObject.GetGridPosition()));
        float floorIn = Interval(inside), floorOut = Interval(outside);
        float rarityFast = inside.GetComponentInParent<PlanterBrain>().GetFinalStat(StatType.RareSpawnChance);
        StatManager.Instance.RemoveGlobalModifier(fast);
        Require(Mathf.Abs(floorOut - .5f) < 1e-4f && Mathf.Abs(floorIn - .75f) < 1e-4f, $"At the production floor the band still slows: {floorIn} vs {floorOut}");
        Require(Mathf.Approximately(brain.GetFinalStat(StatType.RareSpawnChance), rarityBefore), "Frost does not touch rarity (no floor-overflow bonus)");
        Note($"rarity at floor with overflow: {rarityFast:0.#} (same inside and outside the band)");
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Call(hud, "Refresh");
        var hudTexts = hud.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.gameObject.activeInHierarchy).ToDictionary(t => t.name, t => t.text);
        Require(hudTexts["Label"] == "BOSS KOTASI · 5 ROUND" && hudTexts["Event"].StartsWith("DON CEPHESİ: ") && hudTexts["Event"].Contains("üretim ×1,5 yavaş"), $"HUD: {hudTexts["Label"]} | {hudTexts["Event"]}");
        var markers = Object.FindAnyObjectByType<FrostZoneMarkers>();
        markers.SendMessage("LateUpdate");
        Require(markers.ShowsActive && markers.ShownCells.All(c => previewZone.Any(p => p.x == c.x && p.z == c.z)), "World band switches to active (same cells)");
        Save("ProtoBossActive");
        // Ölçüm: round 6 Don açık. Aynı seed, sıfır zamanlayıcı, her karede hasat (bitki doğunca öldürülür).
        BeginMeasure();
        return 9.5;
    }

    static void BeginMeasure()
    {
        foreach (var s in Spawners()) { var plant = F<GameObject>(s, "spawnedPlant"); if (plant != null) plant.GetComponent<PlantHealth>().TakeDamage(1 << 28, DamageType.Direct); SetF(s, "timer", 0f); }
        UnityEngine.Random.InitState(99);
        counter = new GameObject("Spawn counter (verification)").AddComponent<SpawnCounter>();
        Time.timeScale = 4f;
    }

    static double MeasureOn()
    {
        Require(GameManager.Instance.CurrentState != GameStates.Round, "Round 6 ended by its timer");
        countOn = new Dictionary<PlantSpawner, int>(counter.Counts); Object.Destroy(counter.gameObject);
        long progress = RM.QuotaProgress;
        Require(progress > 0, "Harvests during the boss round count toward the boss quota: " + progress);
        Flush();
        // Round 7: aynı başlangıç, olay çarpanı 1 (kapalı karşılaştırma).
        var data = AssetDatabase.LoadAssetAtPath<FrostFrontSO>(FrostPath);
        data.spawnIntervalMultiplier = 1f;
        RM.StartNextRound();
        BeginMeasure();
        return 9.5;
    }

    static double MeasureOff()
    {
        countOff = new Dictionary<PlantSpawner, int>(counter.Counts); Object.Destroy(counter.gameObject);
        var data = AssetDatabase.LoadAssetAtPath<FrostFrontSO>(FrostPath);
        data.spawnIntervalMultiplier = 1.5f;
        var frost = Frost;
        int inOn = 0, inOff = 0, outOn = 0, outOff = 0;
        foreach (var s in countOn.Keys)
        {
            bool inside = frost.Covers(s.GridObject.GetGridPosition());
            countOff.TryGetValue(s, out int off);
            if (inside) { inOn += countOn[s]; inOff += off; } else { outOn += countOn[s]; outOff += off; }
            Note($"{RoundMapUI.CellName(s.GridObject.GetGridPosition())} {(inside ? "şerit" : "dışarı")}: Don açık {countOn[s]} · kapalı {off}");
        }
        Note($"şerit toplam: açık {inOn} / kapalı {inOff} · dışarı toplam: açık {outOn} / kapalı {outOff}");
        Require(Math.Abs(outOn - outOff) <= 2, $"Outside the band production is unchanged: {outOn} vs {outOff}");
        Require(inOff > 0 && Math.Abs(inOn * 1.5 - inOff) <= Math.Max(3, inOff * .15), $"Band production ≈ 1/1.5 with the event on: {inOn} vs {inOff}");
        Flush();
        return 1.2;
    }

    // Round 7 bitti: round sonu ekranı (harita ve özet) Don Cephesi aktifken.
    static double BossRoundEndShot()
    {
        Require(GameManager.Instance.CurrentState == GameStates.RoundEnd, "Round 7 end screen open");
        string ev = SegmentEventText.Notice(Events, RM.CurrentRound);
        Require(Strip(ev).StartsWith("DON CEPHESİ AKTİF · 3 round kaldı"), "Round 7 summary: " + Strip(ev));
        var map = Object.FindFirstObjectByType<RoundMapUI>(FindObjectsInactive.Include);
        Require(map.GetComponentsInChildren<TileCellUI>(false).Count(t => t.InEventZone) == 3, "Round map marks the active band");
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Call(summary, "Fill", GameFeelDirector.Instance.LastRound);
        Save("ProtoBossRoundEnd");
        return .3;
    }

    static double Victory()
    {
        for (int r = 8; r <= 9; r++)
        {
            RM.StartNextRound(); Call(RM, "EndRound");
            Flush();
        }
        Require(Events.Active == Frost && SegmentEventText.Notice(Events, 9).StartsWith("DON CEPHESİ AKTİF · 1 round kaldı"), "Round 9 summary: event active, 1 round left");
        RM.StartNextRound();
        long start = HarvestScoreManager.Instance.TotalScore - RM.QuotaProgress;
        SetScore(start + 260);
        Call(RM, "EndRound");
        Require(RM.Outcome == RunOutcome.Victory && !RM.EndedByQuota && RM.LastQuotaScore == 260 && GameManager.Instance.CurrentState == GameStates.RunComplete, "260 / 200 at round 10 → victory");
        Require(Events.Active == null && Events.Events.Count == 0 && Spawners().All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f), "Run end clears the event: no multiplier left");
        foreach (var s in Spawners()) if (Mathf.Abs(Interval(s) - NoEvent(s)) > 1e-4f) throw new Exception("interval not restored " + Interval(s));
        Require(true, "Every production interval back to its pre-event value");
        var markers = Object.FindAnyObjectByType<FrostZoneMarkers>(); markers.SendMessage("LateUpdate");
        Require(markers.ShownCells.Count == 0, "World band hidden after the run");
        var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        var text = complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score"));
        Require(text != null && Strip(text).StartsWith("PROTOTİP TAMAMLANDI") && Strip(text).Contains("10 round · son kota 260 / 200"), "Victory screen: " + Strip(text));
        return 1.5;
    }

    static double Restart()
    {
        Require(SceneManager.GetActiveScene().name == "GameScene" && GameManager.Instance.CurrentState == GameStates.RunSetup && RM.CurrentRound == 1, "Restart reloads a fresh run in RunSetup");
        Require(HarvestScoreManager.Instance.TotalScore == 0 && ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) == 80, "Fresh score 0 and 80 Gold");
        Require(Events != null && Events.Active == null && Frost != null && Frost.IsPrepared && !Frost.IsActive, "New run: event announced again, nothing active");
        Require(Frost.ZoneCells.SequenceEqual(previewZone), "Same seed → same band");
        Require(Spawners().Count == 0, "No production points carried over");
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        PrepareCapture();
        PlantField();
        RM.StartNextRound();
        return .3;
    }

    // Round 1 sürerken: elektrik havuzu doluyken hasar; long skor; saldırı zamanlayıcısı.
    static double ElectricAndLongScore()
    {
        var manager = HarvestBehaviorManager.Instance;
        SetF(manager, "maxElectricBursts", 1);
        var spawners = Spawners();
        foreach (var s in spawners) if (F<GameObject>(s, "spawnedPlant") == null) Call(s, "TrySpawnPlant");
        var grid = GridManager.Instance.GetGridSystem();
        PlanterBrain source = null; PlantHealth victim = null;
        foreach (var s in spawners)
        {
            var brain = s.GetComponentInParent<PlanterBrain>(); if (brain.OccupiedGrids.Count != 1) continue;
            var p = s.GridObject.GetGridPosition();
            foreach (var d in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var t = grid.GetGridObject(new GridPosition(p.x + d.Item1, p.z + d.Item2));
                var plant = t?.GetPlantObject();
                if (plant != null && t.GetPlanterBrain() != brain && plant.TryGetComponent(out PlantHealth h) && !h.IsDead && h.CurrentHealth >= 3) { source = brain; victim = h; break; }
            }
            if (source != null) break;
        }
        Require(source != null, "Found a 1×1 planter with a living diagonal target");
        int hp = victim.CurrentHealth, skipped = manager.SkippedElectricVisuals;
        Require(manager.TryElectric(source, 1) && manager.ActiveElectricBursts == 1, "First electric strike takes the only visual");
        Require(manager.TryElectric(source, 1) && manager.ActiveElectricBursts == 1 && manager.SkippedElectricVisuals == skipped + 1, "Pool full: visual skipped, strike still happens");
        Require(victim.CurrentHealth == hp - 2, $"Both strikes dealt damage: {hp} → {victim.CurrentHealth}");
        // Saldırı zamanlayıcısı: 60 FPS, 0,1333 sn aralık, 60 sn → 450 vuruş (eski sıfırlama ~400).
        float timer = 0; int hits = 0; float interval = .2f / 1.5f;
        for (int f = 0; f < 3600; f++) if (PlayerController.AdvanceAttackTimer(ref timer, 1f / 60f, interval)) hits++;
        float t2 = 0; bool first = PlayerController.AdvanceAttackTimer(ref t2, 1f, interval);
        Require(hits >= 449 && hits <= 450 && first && t2 <= interval + 1e-6f, $"Attack timer keeps overshoot: {hits} hits in 60 s at 60 FPS; a 1 s frame gives one hit and carries at most one interval ({t2:0.###})");
        // long skor: int tavanının üstünde segment ilerlemesi durmaz.
        SetScore(3_000_000_000L);
        Call(RM, "EndRound");
        SetF(RM, "segmentStartScore", 3_000_000_000L);
        Require(RM.QuotaProgress == 0, "Segment start above int.MaxValue");
        SetScore(3_000_000_150L);
        Require(RM.QuotaProgress == 150 && HarvestScoreManager.Instance.TotalScore == 3_000_000_150L, "Score past 2.1 billion keeps counting (long)");
        HarvestScoreManager.Instance.AddScore(PlantRarity.Legendary);
        Require(HarvestScoreManager.Instance.TotalScore > 3_000_000_150L, "AddScore beyond int range");
        SetScore(0); SetF(RM, "segmentStartScore", 0L);
        return .3;
    }

    // Kayıp: segment 1 geçilir (üstünde), segment 2 kotanın altında kalır.
    static double Defeat()
    {
        Flush();
        for (int r = 2; r <= 5; r++)
        {
            RM.StartNextRound();
            if (r == 5) { long start = HarvestScoreManager.Instance.TotalScore - RM.QuotaProgress; SetScore(start + 55); }
            Call(RM, "EndRound"); Flush();
        }
        Require(!RM.EndedByQuota && RM.LastQuotaScore == 55 && RM.LastQuotaTarget == 40, "55 / 40 passes segment 1 (above)");
        for (int r = 6; r <= 10; r++)
        {
            RM.StartNextRound();
            if (r == 6) Require(Events.Active == Frost && RM.QuotaProgress == 0, "Second run: boss starts again at round 6");
            if (r == 10) { long start = HarvestScoreManager.Instance.TotalScore - RM.QuotaProgress; SetScore(start + 150); }
            Call(RM, "EndRound"); if (r < 10) Flush();
        }
        Require(RM.EndedByQuota && RM.Outcome == RunOutcome.QuotaFailed && GameManager.Instance.CurrentState == GameStates.RunComplete, "150 / 200 at round 10 → run lost");
        Require(Events.Active == null && Spawners().All(s => SegmentEventDirector.SpawnIntervalMultiplier(s.GridObject.GetGridPosition()) == 1f), "Defeat clears the event");
        var complete = F<GameObject>(Object.FindFirstObjectByType<UIManager>(), "runCompletePanel");
        var text = complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score"));
        Require(text != null && Strip(text).StartsWith("KOTA TUTMADI") && Strip(text).Contains("Round 10 · segment skoru 150 / 200"), "Defeat screen: " + Strip(text));
        return 1.5;
    }

    static double MainMenu()
    {
        Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
        return 3;
    }

    static double MenuCheck()
    {
        Require(SceneManager.GetActiveScene().name == "MenuScene" && GameManager.Instance.CurrentState == GameStates.MainMenu, "Main menu button loads MenuScene in MainMenu state");
        Require(SegmentEventDirector.Instance == null && Object.FindAnyObjectByType<FrostZoneMarkers>() == null && SegmentEventDirector.SpawnIntervalMultiplier(new GridPosition(5, 5)) == 1f, "No event state survives the scene change");
        Require(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length == 1, "Single GameManager after returning to the menu");
        return -1;
    }

    // Her karede doğan bitkiyi sayar ve öldürür (hasat). Yalnız doğrulama.
    sealed class SpawnCounter : MonoBehaviour
    {
        public readonly Dictionary<PlantSpawner, int> Counts = new();
        readonly FieldInfo plantField = typeof(PlantSpawner).GetField("spawnedPlant", BindingFlags.NonPublic | BindingFlags.Instance);
        void Update()
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            foreach (var s in FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None))
            {
                var plant = plantField.GetValue(s) as GameObject;
                if (plant == null) continue;
                var health = plant.GetComponent<PlantHealth>();
                if (health == null || health.IsDead) continue;
                Counts.TryGetValue(s, out int n); Counts[s] = n + 1;
                health.TakeDamage(1 << 28, DamageType.Direct);
            }
        }
    }
}
