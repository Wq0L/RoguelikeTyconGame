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

// Batch (izole kopya): Bölüm 2.2 — hız, yük biriktiren elektrik ve süre deneyi, GameScene'de.
// A) Güç haritası: gerçek SkillNodeSO asset'lerinden hız / süre / hasar / üretim / skor zincirleri ve birleşik değerleri.
// B) İşlevsel: Deney22_20 profilinde yük modu (öldürmeden tetik, yük yokken yok, saksı başına tek tüketim, eski tetikle çakışmama,
//    hazırlıkta dolmama, round/run sıfırlaması, satış ve erişim kaybı, hasar/XP bir kez, havuz dolu), süre kontrol koşulu,
//    Hızlı Şarj, tempo, normal profillerin değişmediği ve modlar arası sızıntı olmadığı.
// C) Ölçüm: simülatörün normal ekonomiyle ürettiği temsilî R10/R15/R20 durumları (simülatör durumu, insan verisi değil) sahneye kurulur.
//    Doğrudan düzen = durumun kendisi; elektrik düzeni = aynı durum, saksı altındaki tile'ların yarısı aynı nadirlikte elektrik.
//    Bot oyuncu, sabit kare süresi (1/30 sn), aynı seed kümesi; kota, olay ve uzmanlaşma kapalı çalışma zamanı profil kopyası.
[InitializeOnLoad]
public static class SpeedElectricVerification
{
    const string Key = "SpeedElectricVerification";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    const string TreeFolder = "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/";
    const float FrameTime = 1f / 30f;
    static readonly List<string> notes = new();
    static int step; static double nextAt;

    static SpeedElectricVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        throw new InvalidOperationException("Yük biriktiren elektrik deneyi geri alındı. Bu tarihsel deney artık kabul testi değildir; RunPrototypeVerification ve SpecializationVerification kullanın.");
    }

    // Eski deneyin tekrar üretim kodu; menü/batch giriş noktası değildir.
    private static void RunRetiredExperiment()
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

    static int shownStep = -1; static double stepSince;
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (nextAt == 0) nextAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            // İlerleme dosyası: takılan adım görünsün. Aynı adım gerçek zamanda uzun sürerse durum bilgisiyle durur.
            if (step != shownStep)
            {
                shownStep = step; stepSince = EditorApplication.timeSinceStartup;
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/SpeedElectricVerification.txt", new[] { $"RUNNING step {step} (config {config}, index {index})" }.Concat(notes));
            }
            else if (EditorApplication.timeSinceStartup - stepSince > 240)
                throw new Exception($"step {step} stuck: state {GameManager.Instance?.CurrentState}, round {RoundManager.Instance?.CurrentRound}, timeScale {Time.timeScale}, time {Time.timeAsDouble:0.0}, capture {Time.captureDeltaTime}");
            // İşlevsel adımlarda round kendi süresiyle bitmesin (yük beklemeleri oyun zamanıyla ölçülür).
            if (step < 25 && RoundManager.Instance != null && RoundManager.Instance.IsRoundActive) SetP(RoundManager.Instance, "RemainingTime", 1000f);
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
        File.WriteAllLines("Logs/SpeedElectricVerification.txt", new[] { ex == null ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " checks" : "FAIL: " + ex }.Concat(notes));
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
    static ElectricChargeManager EC => ElectricChargeManager.Instance;
    static HarvestBehaviorManager HBM => HarvestBehaviorManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static int Center => GridManager.Instance.GetWidth() / 2;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static GridObject Cell(int dx, int dz) => Grid.GetGridObject(new GridPosition(Center + dx, Center + dz));
    static StatModifier Mod(StatType s, StatTarget t, float v, ModifierOperation op = ModifierOperation.Set) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static int Res(ResourceType t) => ResourceManager.Instance.GetResourceAmount(t);
    static double Xp => ProgressionManager.Instance.TotalXPEarned;
    static long Score => HarvestScoreManager.Instance.TotalScore;
    static PlanterSO PlanterAsset(string n) => AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/GrassPlanter {n}.asset");
    static TileModifierSO Tile(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static SkillNodeSO Node(string n) => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(TreeFolder + n + ".asset");
    static PlantSO PlantData(string n) => AssetDatabase.LoadAssetAtPath<PlantSO>($"Assets/ScriptableObjects/Plants/{n}.asset");
    // Sabit sıra: başlangıç zamanlayıcıları seed'den bu sırayla dağıtılır; sıra değişirse koşudan koşuya sonuç değişir.
    static List<PlantSpawner> Spawners() => Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null)
        .OrderBy(s => s.GridObject.GetGridPosition().x).ThenBy(s => s.GridObject.GetGridPosition().z)
        .ThenBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z).ToList();
    static void RespawnAll() { foreach (var s in Spawners()) if (F<GameObject>(s, "spawnedPlant") == null) Call(s, "TrySpawnPlant"); }
    static PlantHealth PlantAt(GridObject cell) { var p = cell.GetPlantObject(); return p != null ? p.GetComponent<PlantHealth>() : null; }
    static int Triggered => HarvestBehaviorStats.Triggered(DamageType.Electric);

    static void Flush()
    {
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        int guard = 0;
        while (State == GameStates.CardSelection)
        {
            if (guard++ > 60) throw new Exception("card selection did not finish (state " + State + ")");
            Call(cards, "OnCardSelected", F<List<TileCardOffer>>(cards, "currentCards")[0]);
        }
    }

    static void SkipCards()
    {
        int guard = 0;
        if (State == GameStates.CardSelection) while (RM.OnCardSelectionComplete()) if (guard++ > 200) throw new Exception("pending cards did not clear");
    }

    static void ApplyTile(GridObject cell, TileModifierSO so, float value)
    {
        var mods = so.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value < 0 ? r.maxValue : value }).ToList();
        cell.GetGroundCellCached().ApplyModifier(so, mods);
    }

    static PlanterBrain Place(PlanterSO so, List<GridObject> cells, bool rotated)
    {
        var planter = Object.Instantiate(so.prefab);
        Vector3 center = Vector3.zero; foreach (var c in cells) center += c.GetGroundCellCached().transform.position;
        planter.transform.position = center / cells.Count;
        planter.transform.rotation = Quaternion.Euler(0f, rotated ? 90f : 0f, 0f);
        foreach (var c in cells) c.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(so, cells);
        foreach (var c in cells) c.SetPlanterBrain(brain);
        return brain;
    }

    static PlanterBrain Place1x1(GridObject cell, TileModifierSO tile = null, float value = -1)
    {
        if (tile != null) ApplyTile(cell, tile, value);
        return Place(PlanterAsset("1x1"), new List<GridObject> { cell }, false);
    }

    // Oyuncu vuruşu (PlayerController.AttackInRadius), istenen yarıçapla.
    static StatModifier radiusMod, damageMod;
    static bool hasRadius, hasDamage;
    static void Attack(Vector3 position, float radius)
    {
        if (hasRadius) Stats.RemoveGlobalModifier(radiusMod);
        hasRadius = true;
        radiusMod = Mod(StatType.AreaRadius, StatTarget.Player, radius);
        Stats.AddGlobalModifier(radiusMod);
        Call(Object.FindFirstObjectByType<PlayerController>(), "AttackInRadius", position);
    }
    static void Hit(GridObject cell, float radius = .1f) => Attack(cell.GetGroundCellCached().transform.position, radius);
    static void SetDamage(float v)
    {
        if (hasDamage) Stats.RemoveGlobalModifier(damageMod);
        hasDamage = true;
        damageMod = Mod(StatType.HarvestDamage, StatTarget.Player, v); Stats.AddGlobalModifier(damageMod);
    }

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return PowerMapAndProfiles();
            case 1: return ChargeSetup();
            case 2: return WaitAllReady(i);
            case 3: return DischargeChecks();
            case 4: return WaitReady(i, true);
            case 5: return TwoPlanters();
            case 6: return SwitchToKillMode();
            case 7: return LegacyKill();
            case 8: return WaitReady(i, false);
            case 9: return NoDoubleTrigger();
            case 10: return WaitReady(i, false);
            case 11: return DamageAndXp();
            case 12: return WaitReady(i, false);
            case 13: return PoolFull();
            case 14: return SellAndAccess();
            case 15: return AfterAccess();
            case 16: return AfterRestore();
            case 17: return RoundEndChecks();
            case 18: return PrepPhase();
            case 19: return NextRoundProgress();
            case 20: return DurationAndQuickCharge();
            case 21: return TempoCharge();
            case 22: return ToNormalProfile();
            case 23: return NormalProfileChecks();
            case 24: return NormalProfileFrames();
            case 25: return StartMeasurement();
            default: return MeasureStep();
        }
    }

    // ---------------- A) güç haritası + profiller ----------------
    static readonly string[] SpeedChain = { "Hızlı Eller - 1", "Hızlı Eller - 2", "Hızlı Eller - 3", "Hızlı Eller - 4", "Akıcı Kesim - 1", "Akıcı Kesim - 2", "Akıcı Kesim - 3", "Akıcı Kesim - 4", "Yıldırım Kesim - 1", "Yıldırım Kesim - 2", "Yıldırım Kesim - 3" };
    static readonly string[] DurationChain = { "Biraz Daha Zaman - 1", "Biraz Daha Zaman - 2", "Biraz Daha Zaman - 3", "Biraz Daha Zaman - 4", "Uzun Hasat - 1", "Uzun Hasat - 2", "Uzun Hasat - 3", "Uzun Hasat - 4", "Son Vardiya - 1", "Son Vardiya - 2", "Son Vardiya - 3", "Son Vardiya - 4" };
    static readonly string[] DamageChain = { "Keskin Başlangıç - 1", "Keskin Başlangıç - 2", "Keskin Başlangıç - 3", "Keskin Başlangıç - 4", "Kesim Tekniği - 1", "Kesim Tekniği - 2", "Kesim Tekniği - 3", "Kesim Tekniği - 4", "Güçlü Kesim - 1", "Güçlü Kesim - 2", "Güçlü Kesim - 3", "Güçlü Kesim - 4", "Kesim Ustalığı - 1", "Kesim Ustalığı - 2", "Kesim Ustalığı - 3", "Kesim Ustalığı - 4", "Ağır Kesim - 1", "Ağır Kesim - 2", "Ağır Kesim - 3", "Ağır Kesim - 4", "Aşırı Güç I", "Aşırı Güç II" };
    static readonly string[] ProductionChain = { "Düzenli Üretim - 1", "Düzenli Üretim - 2", "Düzenli Üretim - 3", "Düzenli Üretim - 4", "Verimli Üretim - 1", "Verimli Üretim - 2", "Verimli Üretim - 3", "Verimli Üretim - 4", "Seri Üretim - 1", "Seri Üretim - 2", "Seri Üretim - 3" };
    static readonly string[] ScoreChain = { "Hasat Rekoru - 1", "Hasat Rekoru - 2", "Hasat Rekoru - 3", "Hasat Rekoru - 4" };
    static readonly string[] BehaviorLocks = { "Grid Genişleme I", "1×3 Saksı", "Patlayıcı Kartlar", "Tornado Kartları", "Bumerang Orak Kartları", "Çapraz Elektrik Kartları" };

    static string Cur(ResourceType t) => t == ResourceType.Gold ? "G" : t == ResourceType.Iron ? "I" : "S";
    static string Effect(StatModifier m) => $"{m.statType} {m.operation} {m.value:0.####}";
    static CoreStatsSO core;

    static float Chain(string[] chain, StatType stat, StatTarget target, float baseValue, out string detail)
    {
        var mods = new List<StatModifier>(); var parts = new List<string>();
        foreach (var name in chain)
        {
            var n = Node(name); if (n == null) { parts.Add(name + " YOK"); continue; }
            mods.AddRange(n.tiers[n.tiers.Count - 1].effects.Where(e => e.statType == stat));
            parts.Add($"{name}: {StatCalculator.Calculate(baseValue, stat, target, mods, null):0.###}");
        }
        detail = string.Join(" → ", parts);
        return StatCalculator.Calculate(baseValue, stat, target, mods, null);
    }

    static double PowerMapAndProfiles()
    {
        core = AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
        Note("=== GÜÇ HARİTASI (gerçek asset'ler) ===");
        Note($"taban (CoreStat): hasar {core.GetBaseStat(StatType.HarvestDamage)} · saldırı aralığı {core.GetBaseStat(StatType.AttackSpeed)} sn · alan {core.GetBaseStat(StatType.AreaRadius)} · kritik %{core.GetBaseStat(StatType.CritChance) * 100:0} × {core.GetBaseStat(StatType.CritMultiplier)} · round süresi {core.GetBaseStat(StatType.RoundDuration)} sn · üretim aralığı (1×1 saksı) {PlanterAsset("1x1").GetBaseStat(StatType.PlantSpawnRate)} sn");
        Note($"sınırlar (kod): saldırı aralığı stat ≥ 0,05; PlayerController max(aralık, 0,1) / tempo → en kısa 0,1/{RoundManager.MaxTempo:0.#} = {0.1f / RoundManager.MaxTempo:0.###} sn · üretim aralığı ≥ {StatCalculator.MinimumSpawnInterval} sn (fazlası nadirliğe) · round süresi 30–90 sn; 60 sn üstü tempo = süre/60 (en çok {RoundManager.MaxTempo:0.##}), saldırı ve üretim aralığını böler");
        foreach (var (title, chain) in new[] { ("Hız", SpeedChain), ("Süre", DurationChain), ("Hasar", DamageChain), ("Üretim", ProductionChain), ("Skor", ScoreChain) })
        {
            Note($"-- {title} ailesi --");
            foreach (var name in chain)
            {
                var n = Node(name); if (n == null) { Note($"{name}: asset yok"); continue; }
                string pre = string.Join(", ", n.prerequisites.Select(p => $"{(p.node != null ? p.node.name : "?")}@{p.level}"));
                string tiers = string.Join(" / ", n.tiers.Select(t => $"{t.cost}{Cur(t.costType)}: {string.Join("; ", t.effects.Select(Effect))}"));
                Note($"{name} [hedef R{n.targetRounds.x}–{n.targetRounds.y}] önkoşul: {(pre.Length > 0 ? pre : "yok")} · {tiers}");
            }
        }
        foreach (var name in BehaviorLocks)
        {
            var n = Node(name); if (n == null) { Note($"{name}: asset yok"); continue; }
            Note($"kilit {name} [R{n.targetRounds.x}–{n.targetRounds.y}] {string.Join(" / ", n.tiers.Select(t => $"{t.cost}{Cur(t.costType)}"))} · açar: {n.unlockType} · önkoşul: {string.Join(", ", n.prerequisites.Select(p => $"{p.node?.name}@{p.level}"))}");
        }
        float interval = Chain(SpeedChain, StatType.AttackSpeed, StatTarget.Player, core.GetBaseStat(StatType.AttackSpeed), out string speedDetail);
        Note($"hız zinciri (MorePercent'ler çarpılır): {speedDetail}");
        float dur = Chain(DurationChain, StatType.RoundDuration, StatTarget.All, core.GetBaseStat(StatType.RoundDuration), out string durDetail);
        float tempo = Mathf.Max(1f, Mathf.Min(dur, 90f) / RoundManager.RoundSecondsCap);
        Note($"süre zinciri (Flat toplanır): {durDetail} → tempo {tempo:0.##}");
        Note($"hız + süre tam: saldırı aralığı max({interval:0.###}, 0,1) / tempo {tempo:0.##} = {Mathf.Max(interval, .1f) / tempo:0.###} sn");
        Chain(DamageChain, StatType.HarvestDamage, StatTarget.Player, core.GetBaseStat(StatType.HarvestDamage), out string dmgDetail);
        Note($"hasar zinciri (Flat toplanır, AddPercent toplanıp çarpar, MorePercent ayrı çarpar): {dmgDetail}");
        Chain(ProductionChain, StatType.PlantSpawnRate, StatTarget.Planter, PlanterAsset("1x1").GetBaseStat(StatType.PlantSpawnRate), out string prodDetail);
        Note($"üretim zinciri (taban 0,5 sn): {prodDetail}");
        Require(Mathf.Abs(interval - 0.2f) < .01f && Mathf.Abs(dur - 90f) < .01f, $"Power map read from assets (full speed chain {interval:0.###} s, duration {dur:0} s)");
        int durationOnly = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { TreeFolder.TrimEnd('/') })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g))).Count(SkillTreeManager.IsDurationOnly);
        Require(SkillTreeManager.IsDurationOnly(Node("Biraz Daha Zaman - 1")) && !SkillTreeManager.IsDurationOnly(Node("Hızlı Eller - 1")) && durationOnly == 12,
            $"Duration-only nodes: {durationOnly} (Biraz Daha Zaman, Uzun Hasat, Son Vardiya)");

        // Profiller: deney alanları yalnız Deney22_20'de.
        foreach (var name in new[] { "Prototip10", "Prototip10_DebugButce", "Uzmanlasma20", "UzunRun130" })
        {
            var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
            Require(p != null && p.electricMode == ElectricTriggerMode.KillChance && p.fixedRoundDuration == 0f && p.electricCharge.quickChargeMultiplier == 1f,
                $"{name}: kill-triggered electric, duration upgrades (unchanged)");
        }
        var exp = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Deney22_20.asset");
        Require(exp.electricMode == ElectricTriggerMode.Charge && exp.fixedRoundDuration == 0f && exp.runLength == 20 && exp.segmentTargets.SequenceEqual(new long[] { 40, 200, 350, 600 }),
            "Deney22_20: 20 rounds, quota 40/200/350/600, charge electric, duration A");
        var t = exp.electricCharge;
        Note($"dolum eşlemesi: süre = max({t.minimumSeconds}, {t.referenceSeconds} × {t.referenceChance} / şans × Hızlı Şarj {t.quickChargeMultiplier}) · şans 0,15 → {t.Seconds(.15f):0.0} sn · 0,30 → {t.Seconds(.3f):0.0} · 0,50 → {t.Seconds(.5f):0.0} · 1,00 → {t.Seconds(1f):0.0} · 0 → yük yok");
        Require(float.IsPositiveInfinity(t.Seconds(0f)) && t.Seconds(.2f) > t.Seconds(.4f) && t.Seconds(1f) >= t.minimumSeconds, "Charge mapping: monotone, floored, no charge at zero chance");
        Require(RM.Profile == exp && ElectricChargeManager.ChargeMode && !RM.FixedRoundDuration, "Scene runs Deney22_20: charge mode on, duration upgrades on");
        return .2;
    }

    // ---------------- B) yük modu ----------------
    static PlanterBrain A, B, C;
    static double t0;
    static float Sec(PlanterBrain p) => RM.Profile.electricCharge.Seconds(p.GetFinalStat(StatType.ElectricChance));
    static readonly StatModifier NoCrit = Mod(StatType.CritChance, StatTarget.Player, 0f);
    static StatModifier electricAll, durationMod;
    static GridObject[] cCells;
    static bool sampled;
    static float normalReference;
    // Kareler batch'te çok hızlı akar; zaman orantısı yavaş dolumla (600 sn) ölçülür, sonra asset değerine dönülür.
    static void Slow(bool on) => RM.Profile.electricCharge.referenceSeconds = on ? 600f : normalReference;

    static double ChargeSetup()
    {
        GridUnlockManager.Instance.UnlockNextTier(7);
        Stats.AddGlobalModifier(NoCrit); SetDamage(3f);
        // 3×3: A (-1,-1) elektrik şansı 0,5; B (1,1) şans 1; diğerleri tile'sız. C: 2×2 saksı, bir hücresinde elektrik 0,5.
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
        {
            var brain = x == -1 && z == -1 ? Place1x1(Cell(x, z), Tile("Electric", "Legendary"), .5f)
                      : x == 1 && z == 1 ? Place1x1(Cell(x, z), Tile("Electric", "Legendary"), 1f) : Place1x1(Cell(x, z));
            if (x == -1 && z == -1) A = brain; else if (x == 1 && z == 1) B = brain;
        }
        cCells = new[] { Cell(2, -3), Cell(3, -3), Cell(2, -2), Cell(3, -2) };
        ApplyTile(cCells[0], Tile("Electric", "Legendary"), .5f);
        C = Place(PlanterAsset("2x2"), cCells.ToList(), false);
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        SetP(RM, "CurrentRound", 13); SetF(RM, "awaitingFirstRound", false);
        RM.StartNextRound();
        Require(RM.CurrentRound == 14 && State == GameStates.Round, "Round 14 running (charge setup)");
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
        RespawnAll();
        HarvestBehaviorStats.Reset();
        normalReference = RM.Profile.electricCharge.referenceSeconds;
        Slow(true);
        t0 = Time.timeAsDouble;
        Require(Mathf.Approximately(A.GetFinalStat(StatType.ElectricChance), .5f) && Mathf.Approximately(B.GetFinalStat(StatType.ElectricChance), 1f) &&
                Mathf.Approximately(C.GetFinalStat(StatType.ElectricChance), .5f) && C.OccupiedGrids.Count == 4 && cCells.All(c => PlantAt(c) != null),
            $"Electric chances A 0,5 · B 1 · C 0,5 (2×2, 4 plants); fill times at the asset reference {normalReference * .5f / .5f:0.0} / {normalReference * .5f:0.0} / {normalReference:0.0} s");
        int d = EC.Discharges, tr = Triggered;
        var plant = PlantAt(Cell(-1, -1)); int hp = plant.CurrentHealth;
        Hit(Cell(-1, -1));
        Require(plant.CurrentHealth == hp - 3 && !plant.IsDead && EC.Discharges == d && Triggered == tr, "No charge yet: a direct hit gives no electric");
        return .1;
    }

    static double WaitAllReady(int i)
    {
        double elapsed = Time.timeAsDouble - t0;
        if (!sampled)
        {
            double a = elapsed / Sec(A), b = elapsed / Sec(B);
            Require(elapsed > 0 && a < 1 && Math.Abs(EC.Progress(A) - a) <= 2.5 * FrameTime / Sec(A) && Math.Abs(EC.Progress(B) - b) <= 2.5 * FrameTime / Sec(B),
                $"Charge grows with active round time and chance: A {EC.Progress(A):0.0000} ≈ {a:0.0000}, B {EC.Progress(B):0.0000} ≈ {b:0.0000} after {elapsed:0.00} s");
            sampled = true;
            Slow(false);
            t0 = Time.timeAsDouble;
        }
        if (!(EC.IsReady(A) && EC.IsReady(B) && EC.IsReady(C)))
        {
            if (Time.timeAsDouble - t0 > Sec(A) * 2 + 2) throw new Exception("charges not ready");
            step = i; return .02;
        }
        Require(EC.Progress(A) == 1f && EC.Progress(B) == 1f && EC.ChargingPlanters == 3, "All three planters ready; one stored charge each; 3 charging planters");
        return .02;
    }

    static double DischargeChecks()
    {
        int d = EC.Discharges, tr = Triggered;
        var aPlant = PlantAt(Cell(-1, -1)); var bPlant = PlantAt(Cell(1, 1)); var mid = PlantAt(Cell(0, 0));
        int aHp = aPlant.CurrentHealth, bHp = bPlant.CurrentHealth, midHp = mid.CurrentHealth;
        Hit(Cell(1, 1)); // B boşalır; B'nin çapraz ışını merkeze ve A'nın bitkisine vurur (davranış hasarı)
        Require(EC.Discharges == d + 1 && Triggered == tr + 1 && !EC.IsReady(B) && EC.Progress(B) == 0f, "Direct hit on B spends B's charge once (electric fired)");
        Require(!bPlant.IsDead && bPlant.CurrentHealth == bHp - 3, "Electric fired without killing the struck plant");
        Require(aPlant.CurrentHealth == aHp - 3 && mid.CurrentHealth == midHp - 3, "Electric hit its diagonal targets with the normal damage (3)");
        Require(EC.IsReady(A), "Behavior (electric) damage on A's plant does not spend A's charge");
        Hit(Cell(-1, -1));
        Require(EC.Discharges == d + 2 && !EC.IsReady(A), "Direct hit on A spends A's charge");
        d = EC.Discharges; tr = Triggered;
        Hit(Cell(-1, -1)); Hit(Cell(-1, -1));
        Require(EC.Discharges == d && Triggered == tr, "Hitting again without a charge gives no electric");
        var hps = cCells.Select(c => PlantAt(c).CurrentHealth).ToArray();
        Vector3 cCenter = cCells.Aggregate(Vector3.zero, (s, c) => s + c.GetGroundCellCached().transform.position) / 4f;
        Attack(cCenter, 1.6f);
        int hit = Enumerable.Range(0, 4).Count(k => PlantAt(cCells[k]) != null && PlantAt(cCells[k]).CurrentHealth < hps[k]);
        Require(hit == 4 && EC.Discharges == d + 1 && !EC.IsReady(C), $"Four plants of one planter hit in one attack → one charge spent ({hit} hit)");
        t0 = Time.timeAsDouble;
        return .05;
    }

    static double WaitReady(int i, bool both)
    {
        bool ready = both ? EC.IsReady(A) && EC.IsReady(B) : EC.IsReady(A);
        if (!ready)
        {
            if (Time.timeAsDouble - t0 > Math.Max(Sec(A), 3f) * 2 + 3) throw new Exception("charge not ready (step " + i + ")");
            step = i; return .05;
        }
        return .02;
    }

    static double TwoPlanters()
    {
        int d = EC.Discharges;
        RespawnAll();
        Attack(Cell(0, 0).GetGroundCellCached().transform.position, 3.2f); // 3×3'ün tamamı
        Require(EC.Discharges == d + 2 && !EC.IsReady(A) && !EC.IsReady(B), "One attack over two ready planters spends each planter's own charge (2)");
        // Yük modunda öldürme elektrik tetiklemez.
        SetDamage(100f);
        d = EC.Discharges; int tr = Triggered;
        Hit(Cell(-1, -1));
        Require(PlantAt(Cell(-1, -1)) == null || PlantAt(Cell(-1, -1)).IsDead, "A's plant killed by a direct hit");
        Require(EC.Discharges == d && Triggered == tr, "Charge mode: a direct kill without charge fires no electric (old trigger off)");
        return .05;
    }

    static double SwitchToKillMode()
    {
        Require(EC.ChargingPlanters > 0, "Charges exist before switching modes");
        RM.Profile.electricMode = ElectricTriggerMode.KillChance;
        Require(!ElectricChargeManager.ChargeMode, "Switched to kill trigger (A)");
        return .1; // kareler geçsin
    }

    static double LegacyKill()
    {
        Require(EC.ChargingPlanters == 0 && !EC.IsReady(A) && !EC.IsReady(B), "Mode switch leaves no charge behind (no leakage)");
        electricAll = Mod(StatType.ElectricChance, StatTarget.Planter, 1f); Stats.AddGlobalModifier(electricAll);
        RespawnAll();
        int tr = Triggered, d = EC.Discharges;
        Hit(Cell(-1, -1));
        Require(Triggered == tr + 1 && EC.Discharges == d, "Kill trigger (A): direct kill with chance 1 fires electric once, no charge used");
        RM.Profile.electricMode = ElectricTriggerMode.Charge;
        t0 = Time.timeAsDouble;
        return .05;
    }

    static double NoDoubleTrigger()
    {
        RespawnAll();
        int tr = Triggered, d = EC.Discharges;
        Hit(Cell(-1, -1)); // hazır yük + öldürücü vuruş (şans 1)
        Require(Triggered == tr + 1 && EC.Discharges == d + 1, "Ready charge + kill with chance 1 → exactly one electric (old and new never both)");
        Stats.RemoveGlobalModifier(electricAll);
        // Hasar ve XP: Davranış Ustası (davranış ×1,25), hasar 8.
        SetP(SM, "Chosen", AssetDatabase.LoadAssetAtPath<SpecializationSO>("Assets/ScriptableObjects/Specializations/DavranisUstasi.asset"));
        SetDamage(8f);
        t0 = Time.timeAsDouble;
        return .05;
    }

    static double DamageAndXp()
    {
        RespawnAll();
        var mid = PlantAt(Cell(0, 0)); var bPlant = PlantAt(Cell(1, 1));
        int midHp = mid.CurrentHealth, bHp = bPlant.CurrentHealth;
        Hit(Cell(-1, -1));
        int expected = A.GetBehaviorDamage(8, DamageType.Electric);
        Require(expected == 10 && midHp - mid.CurrentHealth == 10 && bHp - bPlant.CurrentHealth == 10, $"Discharge damage uses the behavior coefficient once: 8 × 1,25 = {midHp - mid.CurrentHealth}");
        // XP: aynı üç öldürme (A doğrudan, merkez ve B elektrik) yük modunda ve öldürme modunda aynı XP ve skoru verir.
        SetDamage(100f);
        var grass = PlantData("Grass");
        return XpCompareCharge(grass);
    }

    static double xpCharge; static long scoreCharge;
    static double XpCompareCharge(PlantSO grass)
    {
        // A şu an boş (yük harcandı). Kısa bekleme yerine yük modunda A'nın dolmasını beklemek için adım 12 kullanılır.
        t0 = Time.timeAsDouble;
        return .05;
    }

    static double PoolFull()
    {
        // (adım 12'de A yeniden doldu) — önce XP karşılaştırması: yük modu
        var grass = PlantData("Grass");
        RespawnAll();
        foreach (var cell in new[] { Cell(-1, -1), Cell(0, 0), Cell(1, 1) }) PlantAt(cell).GetComponent<PlantResource>().Initialize(grass, cell.GetPlanterBrain());
        double x0 = Xp; long s0 = Score; int d = EC.Discharges;
        Hit(Cell(-1, -1));
        Require(EC.Discharges == d + 1, "Charge-mode kill + discharge");
        xpCharge = Xp - x0; scoreCharge = Score - s0;
        // aynı öldürmeler öldürme modunda (şans 1)
        RM.Profile.electricMode = ElectricTriggerMode.KillChance;
        electricAll = Mod(StatType.ElectricChance, StatTarget.Planter, 1f); Stats.AddGlobalModifier(electricAll);
        RespawnAll();
        foreach (var cell in new[] { Cell(-1, -1), Cell(0, 0), Cell(1, 1) }) PlantAt(cell).GetComponent<PlantResource>().Initialize(grass, cell.GetPlanterBrain());
        x0 = Xp; s0 = Score; int tr = Triggered;
        Hit(Cell(-1, -1));
        Require(Triggered == tr + 1, "Kill-mode electric fired for the XP comparison");
        Require(xpCharge > 0 && Xp - x0 == xpCharge && Score - s0 == scoreCharge, $"Same kills give the same XP and score in both modes (XP {xpCharge:0}, score {scoreCharge})");
        Stats.RemoveGlobalModifier(electricAll);
        SetP(SM, "Chosen", null);
        SetDamage(3f);
        RM.Profile.electricMode = ElectricTriggerMode.Charge;
        return PoolFullSetup();
    }

    static int poolPhase;
    static double PoolFullSetup()
    {
        poolPhase = 1;
        t0 = Time.timeAsDouble;
        return .05;
    }

    static double SellAndAccess()
    {
        // Havuz dolu: A hazır olmalı.
        if (!EC.IsReady(A))
        {
            if (Time.timeAsDouble - t0 > Sec(A) * 2 + 3) throw new Exception("A not ready for pool check");
            step = 14; return .05;
        }
        int max = F<int>(HBM, "maxElectricBursts");
        RespawnAll();
        int guard = 0;
        while (HBM.ActiveElectricBursts < max && guard++ < 100) HBM.TryElectric(B, 0);
        Require(HBM.ActiveElectricBursts == max, $"Electric effect pool full ({max})");
        int skipped = HBM.SkippedElectricVisuals, d = EC.Discharges;
        var mid = PlantAt(Cell(0, 0)); int midHp = mid.CurrentHealth;
        Hit(Cell(-1, -1));
        Require(EC.Discharges == d + 1 && HBM.SkippedElectricVisuals == skipped + 1 && midHp - mid.CurrentHealth == 3, "Pool full: visual skipped, discharge damage still applied");
        HBM.ClearAll();
        // Satış ve erişim kaybı
        C.RemoveSelf();
        Cell(1, 1).GetGroundCellCached().ApplyModifier(null); // B'nin elektrik tile'ı kalkar
        return .1;
    }

    static double AfterAccess()
    {
        Require(!EC.IsReady(C) && EC.Progress(C) == 0f && !ElectricChargeManager.TryDischarge(C, 3), "Sold planter: charge cleared, cannot discharge");
        Require(Mathf.Approximately(B.GetFinalStat(StatType.ElectricChance), 0f) && !EC.IsReady(B) && EC.Progress(B) == 0f, "Electric tile removed: B's charge cleared");
        int d = EC.Discharges;
        Hit(Cell(1, 1));
        Require(EC.Discharges == d, "No electric from B after losing access");
        Slow(true);
        ApplyTile(Cell(1, 1), Tile("Electric", "Legendary"), 1f);
        t0 = Time.timeAsDouble;
        return .02;
    }

    static double AfterRestore()
    {
        double elapsed = Time.timeAsDouble - t0;
        double expected = elapsed / Sec(B);
        Require(expected < 1 && Math.Abs(EC.Progress(B) - expected) <= 2.5 * FrameTime / Sec(B) + 1e-4, $"Restored access starts from zero: {EC.Progress(B):0.0000} ≈ {expected:0.0000}");
        Slow(false);
        return .02;
    }

    static double RoundEndChecks()
    {
        Require(EC.ChargingPlanters > 0, "Charges exist before round end");
        Call(RM, "EndRound");
        Require(EC.ChargingPlanters == 0 && EC.Progress(A) == 0f, "Round end clears every charge");
        return .3; // hazırlık/kart ekranında kareler geçsin
    }

    static double PrepPhase()
    {
        Require(State != GameStates.Round && EC.ChargingPlanters == 0 && EC.Progress(A) == 0f, $"No charging between rounds ({State})");
        Flush();
        Require(EC.ChargingPlanters == 0, "No charging during card selection");
        Slow(true);
        RM.StartNextRound();
        Require(RM.CurrentRound == 15 && State == GameStates.Round, "Round 15 started");
        t0 = Time.timeAsDouble;
        return .02;
    }

    static double NextRoundProgress()
    {
        double elapsed = Time.timeAsDouble - t0;
        double expected = elapsed / Sec(A);
        Require(expected < 1 && Math.Abs(EC.Progress(A) - expected) <= 2.5 * FrameTime / Sec(A) + 1e-4, $"New round: charge starts from zero ({EC.Progress(A):0.0000} ≈ {expected:0.0000})");
        Slow(false);
        return .02;
    }

    static double DurationAndQuickCharge()
    {
        var profile = RM.Profile;
        durationMod = Mod(StatType.RoundDuration, StatTarget.All, 60f, ModifierOperation.Flat);
        Stats.AddGlobalModifier(durationMod);
        profile.fixedRoundDuration = 45f;
        Require(RM.FixedRoundDuration && Mathf.Approximately(RM.RawRoundDuration, 45f) && Mathf.Approximately(RM.EffectiveRoundDuration, 45f) && RM.TempoMultiplier == 1f,
            "Duration B: profile sets 45 s; +60 s of upgrades ignored; no tempo");
        ResourceManager.Instance.AddResource(ResourceType.Gold, 10000); ResourceManager.Instance.AddResource(ResourceType.Iron, 10000); ResourceManager.Instance.AddResource(ResourceType.Stone, 10000);
        var tree = SkillTreeManager.Instance;
        var bdz = Node("Biraz Daha Zaman - 1"); var fast = Node("Hızlı Eller - 1"); var longHarvest = Node("Uzun Hasat - 1"); var sharp = Node("Keskin Başlangıç - 1");
        Require(tree.IsDisabledByProfile(bdz) && !tree.CanUpgrade(bdz) && tree.IsDisabledByProfile(longHarvest) && !tree.CanUpgrade(longHarvest),
            "Duration B: duration nodes cannot be bought");
        Require(tree.GetCurrentLevel(bdz) == 0 && tree.MeetsPrerequisites(fast) && tree.CanUpgrade(fast) && !tree.IsDisabledByProfile(sharp) && tree.CanUpgrade(sharp),
            "Duration B: speed chain reachable without duration nodes; other nodes unaffected");
        profile.fixedRoundDuration = 0f;
        Require(!RM.FixedRoundDuration && Mathf.Approximately(RM.RawRoundDuration, 90f) && Mathf.Approximately(RM.TempoMultiplier, 1.5f) && !tree.MeetsPrerequisites(fast) && tree.CanUpgrade(bdz),
            "Duration A: upgrades count again (90 s → tempo 1,5); duration node needed for the speed chain");
        // Hızlı Şarj: yalnız dolum süresi.
        float attack = Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), fill = Sec(A);
        profile.electricCharge.quickChargeMultiplier = .75f;
        float quick = Sec(A), attackAfter = Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player);
        profile.electricCharge.quickChargeMultiplier = 1f;
        Require(attackAfter == attack && Mathf.Abs(quick - Mathf.Max(profile.electricCharge.minimumSeconds, fill * .75f)) < 1e-4f,
            $"Quick Charge changes only the fill time ({fill:0.00} → {quick:0.00} s); attack interval unchanged ({attack:0.###} s)");
        // Tempo dolumu da hızlandırır (A koşulu, 90 sn → tempo 1,5).
        Slow(true);
        EC.ClearAll();
        t0 = Time.timeAsDouble;
        return .02;
    }

    static double TempoCharge()
    {
        double elapsed = Time.timeAsDouble - t0;
        double expected = elapsed * 1.5 / Sec(A);
        Require(expected < 1 && Math.Abs(EC.Progress(A) - expected) <= 3.5 * FrameTime * 1.5 / Sec(A) + 1e-4, $"Tempo 1,5 fills the charge 1,5× faster ({EC.Progress(A):0.0000} ≈ {expected:0.0000})");
        Slow(false);
        Stats.RemoveGlobalModifier(durationMod);
        return .02;
    }

    static double ToNormalProfile()
    {
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Uzmanlasma20.asset");
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double NormalProfileChecks()
    {
        Require(RM.Profile.name == "Uzmanlasma20" && !ElectricChargeManager.ChargeMode && !RM.FixedRoundDuration && EC != null && EC.ChargingPlanters == 0 && EC.Discharges == 0,
            "New run on Uzmanlasma20: charge off, duration upgrades on, charge state empty");
        GridUnlockManager.Instance.UnlockNextTier(5);
        Stats.AddGlobalModifier(NoCrit); hasDamage = hasRadius = false; SetDamage(100f);
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
        {
            var brain = x == -1 && z == -1 ? Place1x1(Cell(x, z), Tile("Electric", "Legendary"), 1f) : Place1x1(Cell(x, z));
            if (x == -1 && z == -1) A = brain;
        }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        RM.StartNextRound();
        RespawnAll(); HarvestBehaviorStats.Reset();
        int tr = Triggered;
        Hit(Cell(-1, -1));
        Require(Triggered == tr + 1, "Normal profile: electric still kill-triggered (chance 1)");
        return .2;
    }

    static double NormalProfileFrames()
    {
        Require(EC.ChargingPlanters == 0 && EC.Discharges == 0, "Normal profile: no charge after active round frames");
        return .05;
    }

    // ---------------- C) ölçüm ----------------
    const int Seeds = 8;
    static readonly int[] StateRounds = { 10, 15, 20 };
    static RunProfileSO measureProfile;
    static List<RunSimulator.Snapshot> states;
    static int stateSeed;
    sealed class Config { public int State; public bool Electric; public string Name; }
    static readonly List<Config> configs = new();
    static int config, phase, index; static bool roundRunning;
    static List<Variant> variants;
    static float measuredReference = -1f;
    static Bot bot;
    static SetupInfo info;
    static readonly List<(int config, string variant, int seed, Metrics m)> results = new();
    static readonly Dictionary<int, SetupInfo> infos = new();

    sealed class SetupInfo
    {
        public int Round, Planters, Spawners, Tiles, ElectricTiles, ElectricPlanters; public double SumChance;
        public float Damage, Interval, Radius, Raw, Effective, Tempo, Crit;
        public string Hp, Purchases;
        public List<(string family, string node, int cost, ResourceType currency, double delta, string variant)> NextTiers = new();
    }

    sealed class Variant
    {
        public string Name;
        public List<StatModifier> Add = new(), Remove = new();
        public float Behavior = 1f, QuickCharge = 1f, DurationFlat;
        public bool Charge;
        public bool HasDuration;
        public Variant(string name) { Name = name; }
    }

    sealed class Metrics
    {
        public int[] kills = new int[5], rarity = new int[5];
        public int attacks, hits, freshHits, freshKills, electric, discharges;
        public long score; public double xp; public int gold, iron, stone;
        public double fill;
        public int Kills => kills.Sum();
        public int Resources => gold + iron + stone;
    }

    static double StartMeasurement()
    {
        states = RunSimulator.RepresentativeStates(RunSimulator.Policies[0], StateRounds, 40, out stateSeed);
        Require(states.Count == 3 && states.Select(s => s.Round).SequenceEqual(StateRounds), $"Representative simulator states R10/R15/R20 (Deneyimli, seed {stateSeed}, median R15 score of 40)");
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + "Deney22_20.asset");
        measureProfile = Object.Instantiate(source);
        measureProfile.name = "Deney22_olcum";
        measureProfile.segmentRounds = 1000; measureProfile.runLength = 1000;
        measureProfile.events = new List<SegmentEventEntry>(); measureProfile.specializationAfterSegment = 0; measureProfile.specializationOptions = new List<SpecializationSO>();
        measureProfile.electricMode = ElectricTriggerMode.KillChance; measureProfile.fixedRoundDuration = 0f;
        measureProfile.electricCharge = new ElectricChargeTuning
        {
            referenceSeconds = source.electricCharge.referenceSeconds, referenceChance = source.electricCharge.referenceChance,
            minimumSeconds = source.electricCharge.minimumSeconds, quickChargeMultiplier = 1f
        };
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = measureProfile;
        configs.Clear();
        foreach (var (s, e) in new[] { (1, true), (0, true), (2, true), (0, false), (1, false), (2, false) })
            configs.Add(new Config { State = s, Electric = e, Name = $"{(e ? "Elektrik düzeni" : "Doğrudan düzen")} · R{StateRounds[s]}" });
        Note($"=== ÖLÇÜM: simülatör durumu (Deneyimli politika, seed {stateSeed}; insan verisi değil). Kota, olay, uzmanlaşma kapalı ölçüm profili. {Seeds} seed × tam round, kare 1/30 sn. ===");
        foreach (var s in states)
            Note($"durum R{s.Round}: {s.Global.Count} skill etkisi, {s.Planters.Count} saksı ({string.Join(", ", s.Planters.GroupBy(p => p.so.name).Select(g => $"{g.Count()}×{g.Key.Replace("GrassPlanter ", "")}"))}), {s.Tiles.Count} tile ({string.Join(", ", s.Tiles.GroupBy(t => t.so.modifierType).Select(g => $"{g.Key} {g.Count()}"))}), banka {s.Gold:0}G/{s.Iron:0}I/{s.Stone:0}S");
        phase = 0; config = 0;
        Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 3;
    }

    static double MeasureStep()
    {
        if (phase % 2 == 0) { config = phase / 2; SetupConfig(); phase++; index = 0; return .3; }
        if (roundRunning)
        {
            if (State == GameStates.Round) return .25;
            Collect(); roundRunning = false;
        }
        if (index == Seeds * variants.Count)
        {
            Report(config);
            if (config < configs.Count - 1) { phase++; Time.captureDeltaTime = 0f; SceneManager.LoadScene("GameScene"); return 3; }
            Summary();
            return -1;
        }
        BeginRound(variants[index / Seeds], index % Seeds);
        index++; roundRunning = true;
        return .5;
    }

    static readonly string[] Rarities = { "Common", "Rare", "Epic", "Legendary" };

    static void SetupConfig()
    {
        var cfg = configs[config]; var snap = states[cfg.State];
        core ??= AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
        Require(RM.Profile == measureProfile && GridManager.Instance.GetWidth() == 11 && GridManager.Instance.GetHeight() == 11, $"Measurement scene ({cfg.Name}): measurement profile, 11×11 grid");
        Stats.AddGlobalModifiers(new List<StatModifier>(snap.Global));
        // Tile'lar (elektrik düzeninde saksı altındakilerin yarısı aynı nadirlikte elektrik).
        var planted = new HashSet<Vector2Int>(snap.Planters.SelectMany(p => p.cells));
        var tiles = snap.Tiles.OrderBy(t => t.cell.x).ThenBy(t => t.cell.y).ToList();
        int underPlanter = 0, electric = 0;
        foreach (var (cell, so, mods) in tiles)
        {
            var ground = Grid.GetGridObject(new GridPosition(cell.x, cell.y)).GetGroundCellCached();
            bool swap = cfg.Electric && planted.Contains(cell) && underPlanter++ % 2 == 0 && so.modifierType != TileModifierType.Electric;
            if (swap)
            {
                var e = Tile("Electric", so.rarity.ToString());
                var range = e.modifierRanges[0];
                ground.ApplyModifier(e, new List<StatModifier> { new StatModifier { statType = range.statType, target = range.target, operation = range.operation, value = (range.minValue + range.maxValue) / 2f } });
            }
            else ground.ApplyModifier(so, new List<StatModifier>(mods));
            if (swap || so.modifierType == TileModifierType.Electric) electric++;
        }
        foreach (var (so, cells) in snap.Planters)
        {
            int xs = cells.Select(c => c.x).Distinct().Count();
            Place(so, cells.Select(c => Grid.GetGridObject(new GridPosition(c.x, c.y))).ToList(), so.sizeX != so.sizeZ && xs != so.sizeX);
        }
        Object.FindFirstObjectByType<PlayerController>().enabled = false;
        bot = new GameObject("Harvest bot (verification)").AddComponent<Bot>();
        SetF(RM, "awaitingFirstRound", false);

        var brains = Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Where(b => b.OccupiedGrids.Count > 0).ToList();
        var health = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        var table = PlanterAsset("1x1").spawnTable.Select(e => e.plant).Where(p => p != null).GroupBy(p => p.rarity).Select(g => g.First()).OrderBy(p => p.rarity).ToList();
        info = new SetupInfo
        {
            Round = snap.Round, Planters = brains.Count, Spawners = Spawners().Count, Tiles = tiles.Count, ElectricTiles = electric,
            ElectricPlanters = brains.Count(b => b.GetFinalStat(StatType.ElectricChance) > 0f), SumChance = brains.Sum(b => b.GetFinalStat(StatType.ElectricChance)),
            Damage = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), Interval = Stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player),
            Radius = Stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player), Raw = RM.RawRoundDuration, Effective = RM.EffectiveRoundDuration, Tempo = RM.TempoMultiplier,
            Crit = Stats.GetFinalStat(StatType.CritChance, StatTarget.Player),
            Hp = string.Join(" · ", table.Select(p => $"{p.rarity} {health.Calculate(p, snap.Round)}"))
        };
        infos[config] = info;
        variants = BuildVariants(cfg, snap);
        Require(info.Spawners > 0 && (!cfg.Electric || info.ElectricPlanters > 0), $"{cfg.Name}: {info.Planters} planters, {info.Spawners} production points, electric planters {info.ElectricPlanters}");
    }

    // Sonraki kademe: ailede önkoşulu sağlanan en ucuz kademe (Gold 1, Iron 7, Stone 14 ağırlıkla; simülatörle aynı).
    static double Weighted(ResourceType t, int cost) => cost * (t == ResourceType.Gold ? 1 : t == ResourceType.Iron ? 7 : 14);
    static List<(SkillNodeSO node, int from, int to)> NextTiers(Dictionary<SkillNodeSO, int> levels, string[] chain, int count)
    {
        var lv = new Dictionary<SkillNodeSO, int>(levels);
        var picks = new List<(SkillNodeSO, int, int)>();
        for (int k = 0; k < count; k++)
        {
            SkillNodeSO best = null; double bestCost = double.MaxValue;
            foreach (var name in chain)
            {
                var n = Node(name); if (n == null) continue;
                int l = lv.TryGetValue(n, out int v) ? v : 0;
                if (l >= n.tiers.Count) continue;
                if (l == 0 && n.prerequisites.Any(p => p.node == null || (lv.TryGetValue(p.node, out int pl) ? pl : 0) < p.level)) continue;
                double c = Weighted(n.tiers[l].costType, n.tiers[l].cost);
                if (c < bestCost) { bestCost = c; best = n; }
            }
            if (best == null) break;
            int from = lv.TryGetValue(best, out int b) ? b : 0;
            lv[best] = from + 1;
            picks.Add((best, from, from + 1));
        }
        return picks;
    }

    static (List<StatModifier> remove, List<StatModifier> add, int cost, ResourceType currency, string names) Purchase(Dictionary<SkillNodeSO, int> levels, List<(SkillNodeSO node, int from, int to)> picks)
    {
        var remove = new List<StatModifier>(); var add = new List<StatModifier>(); int cost = 0; var currency = ResourceType.Gold;
        foreach (var g in picks.GroupBy(p => p.node))
        {
            int from = g.Min(p => p.from), to = g.Max(p => p.to);
            if (from > 0) remove.AddRange(g.Key.tiers[from - 1].effects);
            add.AddRange(g.Key.tiers[to - 1].effects);
        }
        foreach (var p in picks) { cost += p.node.tiers[p.from].cost; currency = p.node.tiers[p.from].costType; }
        return (remove, add, cost, currency, string.Join(", ", picks.Select(p => $"{p.node.name} {p.to}")));
    }

    static float StatWith(StatType stat, StatTarget target, List<StatModifier> global, float baseValue) => StatCalculator.Calculate(baseValue, stat, target, global, null);

    static List<Variant> BuildVariants(Config cfg, RunSimulator.Snapshot snap)
    {
        var list = new List<Variant> { new("taban") };
        list.Add(new Variant("+%10 hasar") { Add = { Mod(StatType.HarvestDamage, StatTarget.Player, .1f, ModifierOperation.MorePercent) } });
        list.Add(new Variant("+%10 saldırı/sn (aralık ÷1,1)") { Add = { Mod(StatType.AttackSpeed, StatTarget.Player, 1f / 1.1f - 1f, ModifierOperation.MorePercent) } });
        list.Add(new Variant("+%10 davranış hasarı") { Behavior = 1.1f });
        list.Add(new Variant("+%10 üretim hızı (aralık ÷1,1)") { Add = { Mod(StatType.PlantSpawnRate, StatTarget.Planter, 1f / 1.1f - 1f, ModifierOperation.MorePercent) } });
        list.Add(new Variant("+%10 skor") { Add = { Mod(StatType.HarvestScoreMultiplier, StatTarget.Player, .1f, ModifierOperation.MorePercent) } });
        var speed = Purchase(snap.Levels, NextTiers(snap.Levels, SpeedChain, 3));
        string speedName = $"hız: sonraki 3 kademe ({speed.names}; {speed.cost}{Cur(speed.currency)})";
        list.Add(new Variant(speedName) { Remove = speed.remove, Add = speed.add });
        if (cfg.Electric)
        {
            list.Add(new Variant("B taban (yük)") { Charge = true });
            list.Add(new Variant("B + " + speedName) { Charge = true, Remove = speed.remove, Add = speed.add });
            list.Add(new Variant($"B + Hızlı Şarj (aynı bütçe {speed.cost}{Cur(speed.currency)}: dolum ×0,73)") { Charge = true, QuickCharge = .729f });
        }
        if (snap.Round == 15)
            foreach (float d in new[] { 30f, 45f, 60f, 90f })
                list.Add(new Variant($"süre {d:0} sn{(d > 60 ? " (60 sn × tempo 1,5)" : "")}") { HasDuration = true, DurationFlat = d - info.Raw });

        // Satın alma verimi için her ailenin sonraki tek kademesi ve stat değişimi.
        var global = snap.Global;
        void Next(string family, string[] chain, StatType stat, StatTarget target, float baseValue, bool inverse, string variant)
        {
            var picks = NextTiers(snap.Levels, chain, 1);
            if (picks.Count == 0) { info.NextTiers.Add((family, "yok (tamam ya da kilitli)", 0, ResourceType.Gold, 0, variant)); return; }
            var p = Purchase(snap.Levels, picks);
            var after = new List<StatModifier>(global); foreach (var r in p.remove) after.Remove(r); after.AddRange(p.add);
            float before = StatWith(stat, target, global, baseValue), now = StatWith(stat, target, after, baseValue);
            if (stat == StatType.RoundDuration) { before = Mathf.Clamp(before, 30f, 90f); now = Mathf.Clamp(now, 30f, 90f); }
            double delta = inverse ? before / now - 1 : now / before - 1;
            info.NextTiers.Add((family, p.names, p.cost, p.currency, delta, variant));
        }
        Next("Hız", SpeedChain, StatType.AttackSpeed, StatTarget.Player, core.GetBaseStat(StatType.AttackSpeed), true, "+%10 saldırı/sn (aralık ÷1,1)");
        Next("Hasar", DamageChain, StatType.HarvestDamage, StatTarget.Player, core.GetBaseStat(StatType.HarvestDamage), false, "+%10 hasar");
        Next("Üretim", ProductionChain, StatType.PlantSpawnRate, StatTarget.Planter, PlanterAsset("1x1").GetBaseStat(StatType.PlantSpawnRate), true, "+%10 üretim hızı (aralık ÷1,1)");
        Next("Skor", ScoreChain, StatType.HarvestScoreMultiplier, StatTarget.Planter, PlanterAsset("1x1").GetBaseStat(StatType.HarvestScoreMultiplier), false, "+%10 skor");
        Next("Süre", DurationChain, StatType.RoundDuration, StatTarget.All, core.GetBaseStat(StatType.RoundDuration), false, "süre");
        return list;
    }

    static Variant current; static int currentSeed; static Metrics metrics;
    static long score0; static double xp0; static int gold0, iron0, stone0, trig0, disc0;
    static SpecializationSO behaviorSpec;

    static void Apply(Variant v, bool on)
    {
        if (on) { foreach (var r in v.Remove) Stats.RemoveGlobalModifier(r); foreach (var a in v.Add) Stats.AddGlobalModifier(a); }
        else { foreach (var a in v.Add) Stats.RemoveGlobalModifier(a); foreach (var r in v.Remove) Stats.AddGlobalModifier(r); }
        if (v.HasDuration)
        {
            var mod = Mod(StatType.RoundDuration, StatTarget.All, v.DurationFlat, ModifierOperation.Flat);
            if (on) Stats.AddGlobalModifier(mod); else Stats.RemoveGlobalModifier(mod);
        }
        if (v.Behavior != 1f)
        {
            if (behaviorSpec == null) { behaviorSpec = ScriptableObject.CreateInstance<SpecializationSO>(); behaviorSpec.displayName = "ÖLÇÜM DAVRANIŞ +%10"; }
            behaviorSpec.behaviorDamageMultiplier = v.Behavior;
            SetP(SM, "Chosen", on ? behaviorSpec : null);
        }
        measureProfile.electricMode = on && v.Charge ? ElectricTriggerMode.Charge : ElectricTriggerMode.KillChance;
        measureProfile.electricCharge.quickChargeMultiplier = on ? v.QuickCharge : 1f;
    }

    static void BeginRound(Variant v, int seed)
    {
        SkipCards();
        current = v; currentSeed = seed; metrics = new Metrics();
        Apply(v, true);
        HarvestBehaviorManager.Instance.ClearAll(); Call(TornadoManager.Instance, "ClearAll");
        HarvestBehaviorStats.Reset();
        UnityEngine.Random.InitState(900 + seed);
        var spawners = Spawners();
        foreach (var s in spawners) { s.RemoveSpawnedPlant(); s.enabled = true; }
        SetP(RM, "CurrentRound", StateRounds[configs[config].State] - 1);
        RM.StartNextRound();
        if (RM.CurrentRound != StateRounds[configs[config].State] || State != GameStates.Round) throw new Exception("measurement round did not start: " + State + " " + RM.CurrentRound);
        foreach (var s in spawners) SetF(s, "timer", UnityEngine.Random.Range(0f, (float)Call(s, "GetEffectiveSpawnInterval")));
        bot.Begin(spawners);
        score0 = Score; xp0 = Xp; trig0 = Triggered; disc0 = EC.Discharges;
        gold0 = Res(ResourceType.Gold); iron0 = Res(ResourceType.Iron); stone0 = Res(ResourceType.Stone);
        Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
    }

    static void Collect()
    {
        Array.Copy(bot.Kills, metrics.kills, 5); Array.Copy(bot.Rarity, metrics.rarity, 5);
        metrics.attacks = bot.Attacks; metrics.hits = bot.Hits; metrics.freshHits = bot.FreshHits; metrics.freshKills = bot.FreshKills;
        metrics.fill = bot.FillSamples > 0 ? bot.FillSum / bot.FillSamples : 0;
        metrics.electric = Triggered - trig0; metrics.discharges = EC.Discharges - disc0;
        metrics.score = Score - score0; metrics.xp = Xp - xp0;
        metrics.gold = Res(ResourceType.Gold) - gold0; metrics.iron = Res(ResourceType.Iron) - iron0; metrics.stone = Res(ResourceType.Stone) - stone0;
        results.Add((config, current.Name, currentSeed, metrics));
        Apply(current, false);
        SkipCards();
        // Referans dolum süresi: elektrik düzeni R15, A modu taban round'larından (mevcut kuralın ölçülen sıklığı).
        if (configs[config].Electric && configs[config].State == 1 && current.Name == "taban" && currentSeed == Seeds - 1)
        {
            var rows = results.Where(r => r.config == config && r.variant == "taban").ToList();
            double e = rows.Average(r => r.m.electric);
            var t = measureProfile.electricCharge;
            if (e > 0)
            {
                measuredReference = (float)Math.Round(info.SumChance * info.Raw / (e * t.referenceChance), 1);
                Note($"referans dolum: mevcut kuralda round başına {e:0.0} elektrik, {info.ElectricPlanters} elektrik saksısı (şans toplamı {info.SumChance:0.00}), {info.Raw:0} sn → sıklığa eşit referans süre (şans {t.referenceChance}) = {measuredReference:0.0} sn; B varyantları asset değerini kullanır: {t.referenceSeconds:0.0} sn");
            }
            else Note("UYARI: referans durumda elektrik tetiklenmedi; asset değeri kullanılıyor");
        }
    }

    static string Stat(IEnumerable<double> values, string format = "0")
    {
        var v = values.ToList();
        return $"{v.Average().ToString(format)} [{v.Min().ToString(format)}–{v.Max().ToString(format)}]";
    }

    static string Rel(List<(Metrics a, Metrics b)> pairs, Func<Metrics, double> f)
    {
        double a = pairs.Sum(p => f(p.a)), b = pairs.Sum(p => f(p.b));
        if (b <= 0) return a > 0 ? "+∞" : "0%";
        var per = pairs.Where(p => f(p.b) > 0).Select(p => (f(p.a) / f(p.b) - 1) * 100).ToList();
        return $"{(a / b - 1) * 100:+0.0;-0.0;0.0}% [{(per.Count > 0 ? per.Min() : 0):+0;-0;0}…{(per.Count > 0 ? per.Max() : 0):+0;-0;0}]";
    }

    static double RelValue(List<(Metrics a, Metrics b)> pairs, Func<Metrics, double> f)
    {
        double a = pairs.Sum(p => f(p.a)), b = pairs.Sum(p => f(p.b));
        return b > 0 ? a / b - 1 : 0;
    }

    static void Report(int c)
    {
        var cfg = configs[c]; var inf = infos[c];
        var rows = results.Where(r => r.config == c).ToList();
        Note($"--- {cfg.Name} ({Seeds} seed; simülatör durumu seed {stateSeed}) ---");
        Note($"durum: hasar {inf.Damage:0.#} · saldırı aralığı {inf.Interval:0.###} sn (tempo sonrası {Mathf.Max(inf.Interval, .1f) / inf.Tempo:0.###}) · alan {inf.Radius:0.##} · kritik %{inf.Crit * 100:0} · süre {inf.Raw:0} sn (etkili {inf.Effective:0}, tempo {inf.Tempo:0.##}) · {inf.Planters} saksı / {inf.Spawners} üretim noktası · {inf.Tiles} tile (elektrik {inf.ElectricTiles}; elektrik saksısı {inf.ElectricPlanters}, şans toplamı {inf.SumChance:0.00}) · can R{inf.Round}: {inf.Hp}");
        Note("varyant | hasat (doğrudan/davranış) | tek vuruş (taze bitki) | elektrik (yük) | skor | XP | G/I/S | üretim doluluğu | vuruş | hasat nadirliği C/U/R/E/L");
        var baseRows = rows.Where(r => r.variant == "taban").ToList();
        var bBase = rows.Where(r => r.variant == "B taban (yük)").ToList();
        foreach (var v in rows.Select(r => r.variant).Distinct())
        {
            var m = rows.Where(r => r.variant == v).Select(r => r.m).ToList();
            double A(Func<Metrics, double> f) => m.Average(f);
            Note($"{v} | {Stat(m.Select(x => (double)x.Kills))} ({A(x => x.kills[0]):0}/{A(x => x.Kills - x.kills[0]):0}) | %{A(x => x.freshHits > 0 ? 100.0 * x.freshKills / x.freshHits : 0):0} | {A(x => x.electric):0.0} ({A(x => x.discharges):0.0}) | " +
                 $"{Stat(m.Select(x => (double)x.score))} | {A(x => x.xp):0} | {A(x => x.gold):0}/{A(x => x.iron):0}/{A(x => x.stone):0} | %{A(x => x.fill * 100):0} | {A(x => x.attacks):0} | " +
                 string.Join("/", Enumerable.Range(0, 5).Select(k => A(x => x.rarity[k]).ToString("0"))));
        }
        Note("aynı seed'de tabana göre: hasat · skor · kaynak (toplam) · elektrik  [seed aralığı]");
        foreach (var v in rows.Select(r => r.variant).Distinct().Where(v => v != "taban"))
        {
            var basis = v.StartsWith("B + ") ? bBase : baseRows;
            string label = v.StartsWith("B + ") ? "B tabana" : "tabana";
            var pairs = rows.Where(r => r.variant == v).Join(basis, a => a.seed, b => b.seed, (a, b) => (a: a.m, b: b.m)).ToList();
            Note($"  {v} ({label}): hasat {Rel(pairs, x => x.Kills)} · skor {Rel(pairs, x => x.score)} · kaynak {Rel(pairs, x => x.Resources)} · elektrik {Rel(pairs, x => x.electric)}");
        }
        // Satın alma verimi: sonraki tek kademe × ölçülen +%10 getirisi (doğrusal yaklaşım).
        var incomes = new Dictionary<ResourceType, double>
        {
            [ResourceType.Gold] = baseRows.Average(r => r.m.gold), [ResourceType.Iron] = baseRows.Average(r => r.m.iron), [ResourceType.Stone] = baseRows.Average(r => r.m.stone)
        };
        Note("satın alma verimi (sonraki tek kademe; ölçülen +%10 getirisinden doğrusal tahmin; fiyat / o para biriminde round geliri):");
        foreach (var (family, node, cost, currency, delta, variant) in inf.NextTiers)
        {
            if (cost == 0) { Note($"  {family}: {node}"); continue; }
            string gain;
            if (family == "Süre")
            {
                var durations = rows.Where(r => r.variant.StartsWith("süre ")).ToList();
                gain = durations.Count > 0 ? "süre ölçümüne bakın (R15)" : $"≈ +%{delta * 100:0.#} süre → hasat ve skor aynı oranda (R15 ölçümü)";
            }
            else
            {
                var pairs = rows.Where(r => r.variant == variant).Join(baseRows, a => a.seed, b => b.seed, (a, b) => (a: a.m, b: b.m)).ToList();
                double k = delta / .1;
                gain = pairs.Count == 0 ? "-" : $"skor {RelValue(pairs, x => x.score) * k * 100:+0.0;-0.0;0.0}% · hasat {RelValue(pairs, x => x.Kills) * k * 100:+0.0;-0.0;0.0}% · kaynak {RelValue(pairs, x => x.Resources) * k * 100:+0.0;-0.0;0.0}%";
            }
            double rounds = incomes[currency] > 0 ? cost / incomes[currency] : double.PositiveInfinity;
            Note($"  {family}: {node} · {cost}{Cur(currency)} ({(double.IsInfinity(rounds) ? "gelir yok" : $"{rounds:0.0} round geliri")}) · stat {delta * 100:+0.#;-0.#;0}% · tahmini {gain}");
        }
    }

    static void Summary()
    {
        int expected = configs.Sum(c => (7 + (c.Electric ? 3 : 0) + (StateRounds[c.State] == 15 ? 4 : 0)) * Seeds);
        Require(results.Count == expected, $"Measured {configs.Count} configurations, {results.Count} rounds ({Seeds} seeds each)");
        Require(results.Where(r => r.variant == "taban").All(r => r.m.attacks > 0 && r.m.Kills > 0), "Bot harvested in every baseline round");
        Require(results.Where(r => configs[r.config].Electric && r.variant == "taban").Sum(r => r.m.electric) > 0 &&
                results.Where(r => r.variant.StartsWith("B")).Sum(r => r.m.discharges) > 0, "Electric fired in A and discharged in B (electric layouts)");
        Require(results.Where(r => r.variant.StartsWith("B")).All(r => r.m.electric == r.m.discharges), "B rounds: every electric came from a discharge (no kill triggers)");
        Require(results.Where(r => !r.variant.StartsWith("B")).All(r => r.m.discharges == 0), "A rounds: no discharges");
    }

    // Oyuncu yerine: saldırı zamanı gelince en çok canlı bitkiyi kapsayan hücreye vurur (yük modunda hazır saksı +1 sayılır);
    // kart ekranlarını bastırır. Tek vuruş: tam canlı bitkiye isabet ve aynı saldırıda ölüm.
    sealed class Bot : MonoBehaviour
    {
        public readonly int[] Kills = new int[5], Rarity = new int[5];
        public int Attacks, Hits, FreshHits, FreshKills, FillSamples;
        public double FillSum;
        float timer;
        MethodInfo attack; PlayerController player;
        List<PlantSpawner> spawners = new();
        readonly List<(PlantHealth h, bool fresh)> scratch = new();
        void OnEnable() { PlantHealth.AnyHarvested += Count; }
        void OnDisable() { PlantHealth.AnyHarvested -= Count; }
        void Count(PlantHealth h)
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            Kills[(int)h.KilledBy]++;
            var res = h.GetComponent<PlantResource>();
            var data = res != null ? F<PlantSO>(res, "plantData") : null;
            if (data != null) Rarity[Mathf.Clamp((int)data.rarity, 0, 4)]++;
        }
        public void Begin(List<PlantSpawner> list)
        {
            Array.Clear(Kills, 0, 5); Array.Clear(Rarity, 0, 5);
            Attacks = Hits = FreshHits = FreshKills = FillSamples = 0; FillSum = 0; timer = 0; spawners = list;
        }
        void Update()
        {
            if (GameManager.Instance.CurrentState != GameStates.Round) return;
            SetF(RoundManager.Instance, "pendingCardSelections", 0);
            int filled = 0; foreach (var s in spawners) if (s != null && F<GameObject>(s, "spawnedPlant") != null) filled++;
            FillSum += spawners.Count > 0 ? filled / (double)spawners.Count : 0; FillSamples++;
            if (player == null) { player = FindFirstObjectByType<PlayerController>(); attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance); }
            float interval = Mathf.Max(StatManager.Instance.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / RoundManager.Instance.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;
            float radius = StatManager.Instance.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            var grid = GridManager.Instance.GetGridSystem();
            bool charge = ElectricChargeManager.ChargeMode;
            Vector3 best = Vector3.zero; double bestScore = 0;
            for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
            {
                var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                if (ground == null || ground.IsLocked) continue;
                double score = 0; var seen = charge ? new HashSet<PlanterBrain>() : null;
                foreach (var g in grid.GetGridObjectsInRadius(ground.transform.position, radius))
                {
                    var p = g.GetPlantObject();
                    if (p == null || !p.TryGetComponent(out PlantHealth h) || h.IsDead) continue;
                    score += 1;
                    if (charge && h.Owner != null && seen.Add(h.Owner) && ElectricChargeManager.Instance.IsReady(h.Owner)) score += 1;
                }
                if (score > bestScore) { bestScore = score; best = ground.transform.position; }
            }
            if (bestScore <= 0) return;
            scratch.Clear();
            foreach (var g in grid.GetGridObjectsInRadius(best, radius))
            {
                var p = g.GetPlantObject();
                if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) scratch.Add((h, h.CurrentHealth >= h.MaxHealth));
            }
            attack.Invoke(player, new object[] { best });
            Attacks++;
            foreach (var (h, fresh) in scratch)
            {
                Hits++;
                if (!fresh) continue;
                FreshHits++;
                if (h == null || h.IsDead) FreshKills++;
            }
        }
    }
}
