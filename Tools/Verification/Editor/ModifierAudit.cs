using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch: 40 tile modifier'ın her birini GameScene'de bir saksının altına koyar, değerin saksıya ulaştığını ölçer
// ve bitki keserek etkisini gözler (spawn süresi, nadir bitki, XP, skor, hasar, çift ödül, patlama, tornado, bumerang, elektrik).
// Hata ilk bulguda durmaz: her tile için OK/FAIL satırı yazar.
[InitializeOnLoad]
public static class ModifierAudit
{
    const string Key = "ModifierAudit";
    static readonly List<string> lines = new();
    static int step, fails, oks; static double nextAt;
    static PlanterBrain A, B; static GroundCell aCell; static GridObject aGrid, bNeighbor;

    static ModifierAudit() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
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
            double wait = step++ == 0 ? Setup() : Audit();
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        var head = ex != null ? "CRASH: " + ex : $"{(fails == 0 ? "PASS" : "FAIL")}: {oks} ok, {fails} fail";
        File.WriteAllLines("Logs/ModifierAudit.txt", new[] { head }.Concat(lines));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null && fails == 0 ? 0 : 1);
    }

    static void Check(bool ok, string message) { if (ok) oks++; else fails++; lines.Add((ok ? "  OK   " : "  FAIL ") + message); }
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(o);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static bool Near(double a, double b, double tol = 1e-3) => Math.Abs(a - b) <= tol * Math.Max(1, Math.Abs(b));
    static string N(double v) => v.ToString("0.###");

    static PlanterBrain Planter(PlanterSO data, int x0, int z0)
    {
        var grid = GridManager.Instance.GetGridSystem(); var cells = new List<GridObject>();
        for (int x = 0; x < 2; x++) for (int z = 0; z < 3; z++) cells.Add(grid.GetGridObject(new GridPosition(x0 + x, z0 + z)));
        var planter = Object.Instantiate(data.prefab);
        planter.transform.position = cells[0].GetGroundCellCached().transform.position + new Vector3(1, 0, 2);
        foreach (var g in cells) g.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(data, cells); foreach (var g in cells) g.SetPlanterBrain(brain);
        return brain;
    }

    static double Setup()
    {
        Check(GameManager.Instance.CurrentState == GameStates.RunSetup, "Scene starts in RunSetup");
        var data = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x3.asset");
        int c = GridManager.Instance.GetWidth() / 2;
        A = Planter(data, c - 2, c - 1);   // A: x 3-4
        B = Planter(data, c, c - 1);       // B: x 5-6, A'nın sağ komşusu
        var grid = GridManager.Instance.GetGridSystem();
        aGrid = grid.GetGridObject(new GridPosition(c - 1, c)); aCell = aGrid.GetGroundCellCached();
        bNeighbor = grid.GetGridObject(new GridPosition(c, c));
        RoundManager.Instance.StartNextRound();
        Check(RoundManager.Instance.IsRoundActive, "Round active for behaviors");
        return .5;
    }

    static PlantSpawner SpawnerOn(PlanterBrain brain, GridObject grid) => F<List<PlantSpawner>>(brain, "spawners").First(s => F<GridObject>(s, "gridObject") == grid);
    static GameObject Plant(PlantSpawner s)
    {
        var plant = F<GameObject>(s, "spawnedPlant");
        if (plant == null) { Call(s, "TrySpawnPlant"); plant = F<GameObject>(s, "spawnedPlant"); }
        return plant;
    }

    static void ResetTiles()
    {
        foreach (var brain in new[] { A, B }) foreach (var g in brain.OccupiedGrids) g.GetGroundCellCached().ApplyModifier(null);
        HarvestBehaviorManager.Instance?.ClearAll();
        if (TornadoManager.Instance != null) Call(TornadoManager.Instance, "ClearAll");
    }

    static void Apply(TileModifierSO so, float value)
    {
        var r = so.modifierRanges[0];
        aCell.ApplyModifier(so, new List<StatModifier> { new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value } });
    }

    // Bitkiyi doğrudan (oyuncu vuruşu gibi) keser; kesilen bitkinin verisini döner.
    static PlantSO Kill(PlantSpawner s)
    {
        var plant = Plant(s);
        var data = F<PlantSO>(plant.GetComponent<PlantResource>(), "plantData");
        plant.GetComponent<PlantHealth>().TakeDamage(10000000, DamageType.Direct);
        return data;
    }

    // Üretim tabanı: skill ağacı süreyi 0,5 sn'ye indirince Fertile'ın fazlası nadir bitki şansına dönüşür.
    static void Overflow(List<TileModifierSO> all, CardSelectionUI cards)
    {
        lines.Add("== Üretim tabanı → nadirlik ==");
        var fert = all.First(t => t.modifierType == TileModifierType.Fertile && t.rarity == TileRarity.Legendary);
        var crystal = all.First(t => t.modifierType == TileModifierType.Crystal && t.rarity == TileRarity.Legendary);
        var aSpawner = SpawnerOn(A, aGrid);
        var cells = A.OccupiedGrids.Select(g => g.GetGroundCellCached()).Where(c => c != aCell).ToList();
        void Put(GroundCell cell, TileModifierSO so, float v) { var r = so.modifierRanges[0]; cell.ApplyModifier(so, new List<StatModifier> { new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = v } }); }
        float Rare() => A.GetFinalStat(StatType.RareSpawnChance);
        float Interval() => (float)Call(aSpawner, "GetEffectiveSpawnInterval");
        (double rare, double legend) RareShare()
        {
            UnityEngine.Random.InitState(7); int rare = 0, legend = 0; const int n = 20000;
            for (int i = 0; i < n; i++) { var p = (PlantSO)Call(aSpawner, "RollPlant"); if (p.rarity >= PlantRarity.Rare) rare++; if (p.rarity == PlantRarity.Legendary) legend++; }
            return (rare * 100.0 / n, legend * 100.0 / n);
        }
        float perTile = StatCalculator.OverflowRarityPerLog * Mathf.Log(1f / .525f);

        ResetTiles(); Put(aCell, fert, -.475f);
        Check(A.GetSpawnOverflowRarity() == 0 && Near(Interval(), 2.625), $"Ağaç yokken Fertile sadece hızlandırır: {N(Interval())} sn, nadirlik +0");

        var tree = new StatModifier { statType = StatType.PlantSpawnRate, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = -.9f };
        StatManager.Instance.AddGlobalModifier(tree);   // tam ağaç gibi: 5 sn × 0,1 = 0,5 sn taban
        ResetTiles();
        float r0 = Rare(); var s0 = RareShare();
        Check(Near(Interval(), .5) && r0 < .1f, $"Tam ağaç: süre tabanda ({N(Interval())} sn), dönüşüm yok (+{N(r0)})");
        Put(aCell, fert, -.475f);
        float r1 = Rare(); var s1 = RareShare();
        Check(Near(Interval(), .5) && Near(r1, perTile, .01), $"Tabanda Legendary Fertile: süre {N(Interval())} sn'de kalır, nadirlik +{N(r1)} (hedef ≈ +45)");
        Check(s1.rare > s0.rare && s1.legend > s0.legend, $"Rare+ bitki payı %{s0.rare:0.0} → %{s1.rare:0.0}, Legendary bitki %{s0.legend:0.00} → %{s1.legend:0.00}");
        Put(cells[0], fert, -.475f);
        float r2 = Rare();
        Check(Near(r2, 2 * perTile, .01), $"İki Legendary Fertile: her biri kendi payını getirir, +{N(r2)}");
        Put(cells[1], fert, -.475f);
        Check(Near(Rare(), 95), $"Üç tile: nadirlik sınırı 95'te durur (+{N(Rare())})");
        ResetTiles(); Put(aCell, crystal, 60f);
        Check(Rare() > perTile, $"Crystal-Legendary (+{N(Rare())}) tabandaki Fertile-Legendary'den (+{N(perTile)}) güçlü kalır");

        // Kart ve tooltip gerçek etkiyi yazar.
        ResetTiles(); Put(aCell, fert, -.475f);
        Check(ProgressionManager.Instance.AllPlantersAtSpawnFloor(), "Bütün saksılar tabanda");
        var slot = F<List<CardUI>>(cards, "cardSlots")[0];
        var offer = new TileCardOffer(fert);
        slot.Setup(offer, _ => { });
        string buff = F<TMPro.TextMeshProUGUI>(slot, "buffText").text, effect = F<TMPro.TextMeshProUGUI>(slot, "effectNameText").text;
        Check(buff == TileBuffText.Points(TileBuffText.SpawnRarityAtFloor(offer.Modifiers)) + " nadirlik" && effect.Contains("Nadirlik"), $"Yeni Fertile kartı: \"{buff}\" · \"{effect}\"");
        var up = TileCardOffer.Upgrade(aCell, TileRarity.Common);
        slot.Setup(up, _ => { });
        buff = F<TMPro.TextMeshProUGUI>(slot, "buffText").text; effect = F<TMPro.TextMeshProUGUI>(slot, "effectNameText").text;
        Check(buff.Contains("nadirlik") && effect.Contains("Nadirlik"), $"Fertile yükseltme kartı: \"{System.Text.RegularExpressions.Regex.Replace(buff, "<[^>]+>", "").Replace((char)10, (char)47)}\"");
        var canvas = Object.FindFirstObjectByType<Canvas>();
        var host = new GameObject("Audit Tooltip", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false);
        var view = ComicPopupView.Attach(host); GridTileToolTipContent.Fill(view, aCell);
        var tip = F<List<TMPro.TMP_Text>>(view, "lines").Where(t => t.gameObject.activeSelf).Select(t => t.text).FirstOrDefault(t => t.Contains("tabanda"));
        Check(tip != null, "Tooltip: " + System.Text.RegularExpressions.Regex.Replace(tip ?? "(yok)", "<[^>]+>", ""));
        Object.DestroyImmediate(host);

        // Ağaç yokken kart eskisi gibi süre yazar.
        StatManager.Instance.RemoveGlobalModifier(tree);
        slot.Setup(new TileCardOffer(fert), _ => { });
        buff = F<TMPro.TextMeshProUGUI>(slot, "buffText").text;
        Check(!buff.Contains("nadirlik") && buff.Contains("%"), $"Ağaç yokken Fertile kartı süre yazar: \"{buff}\"");
        ResetTiles();
    }

    static int Tornadoes() => F<List<Tornado>>(TornadoManager.Instance, "activeTornadoes").Count;

    static double Audit()
    {
        var all = AssetDatabase.FindAssets("t:TileModifierSO", new[] { "Assets/ScriptableObjects/GridModifiers" })
            .Select(g => AssetDatabase.LoadAssetAtPath<TileModifierSO>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(t => t.modifierType).ThenBy(t => t.rarity).ToList();

        // Kart havuzu: sahnedeki kart ekranının listesinde her tile var mı, kilitliler kilidi açılınca mı geliyor?
        lines.Add("== Kart havuzu ==");
        var ui = Object.FindFirstObjectByType<UIManager>();
        var cards = F<GameObject>(ui, "cardSelectionPanel").GetComponent<CardSelectionUI>();
        var pool = F<List<TileModifierSO>>(cards, "allModifiers");
        foreach (var so in all) Check(pool.Contains(so), $"{so.name} kart ekranının listesinde");
        var locked = all.Where(t => t.requiredUnlock != UnlockType.None).ToList();
        var lockedBefore = locked.ToDictionary(t => t, t => t.IsAvailableInCardPool);
        foreach (var so in locked) UnlockManager.Instance.Unlock(so.requiredUnlock);
        foreach (var so in locked)
            Check(!lockedBefore[so] && so.IsAvailableInCardPool, $"{so.name}: skill '{so.requiredUnlock}' alınmadan kartta çıkmıyor, alınınca çıkıyor");
        foreach (var so in all.Where(t => t.requiredUnlock == UnlockType.None)) Check(so.IsAvailableInCardPool, $"{so.name}: baştan kart havuzunda");

        var aSpawner = SpawnerOn(A, aGrid); var bSpawner = SpawnerOn(B, bNeighbor);
        foreach (var so in all)
        {
            var r = so.modifierRanges[0]; float mid = (r.minValue + r.maxValue) / 2f;
            lines.Add($"== {so.name} ({so.modifierName}) · {r.statType} {r.operation} {N(r.minValue)}..{N(r.maxValue)} ==");
            ResetTiles();
            float before = A.GetFinalStat(r.statType);
            Apply(so, mid);
            float after = A.GetFinalStat(r.statType);
            double expected = r.operation == ModifierOperation.Flat ? before + mid : before * (1 + mid);
            if (r.statType is StatType.ExplosionChance or StatType.DuplicateChance or StatType.TornadoChance or StatType.BoomerangChance or StatType.ElectricChance) expected = Math.Min(1, expected);
            Check(Near(after, expected), $"saksıya ulaştı: {TileBuffText.Name(r.statType)} {N(before)} → {N(after)} (beklenen {N(expected)})");

            switch (r.statType)
            {
                case StatType.PlantSpawnRate:
                {
                    ResetTiles(); float i0 = (float)Call(aSpawner, "GetEffectiveSpawnInterval"); Apply(so, mid); float i1 = (float)Call(aSpawner, "GetEffectiveSpawnInterval");
                    Check(i1 < i0 && Near(i1, Math.Max(StatCalculator.MinimumSpawnInterval, i0 * (1 + mid))), $"bitki çıkma süresi {N(i0)} → {N(i1)} sn");
                    break;
                }
                case StatType.RareSpawnChance:
                {
                    double Share()
                    {
                        UnityEngine.Random.InitState(7); int rare = 0; const int n = 20000;
                        for (int i = 0; i < n; i++) { var p = (PlantSO)Call(aSpawner, "RollPlant"); if (p.rarity >= PlantRarity.Rare) rare++; }
                        return rare * 100.0 / n;
                    }
                    ResetTiles(); double s0 = Share(); Apply(so, mid); double s1 = Share();
                    Check(s1 > s0, $"Rare+ bitki payı %{s0:0.0} → %{s1:0.0} (20.000 spawn)");
                    break;
                }
                case StatType.XPGainMultiplier:
                {
                    ResetTiles(); double x0 = ProgressionManager.Instance.TotalXPEarned; var p0 = Kill(aSpawner); double base0 = ProgressionManager.Instance.TotalXPEarned - x0;
                    Apply(so, mid); double x1 = ProgressionManager.Instance.TotalXPEarned; var p1 = Kill(aSpawner); double got = ProgressionManager.Instance.TotalXPEarned - x1;
                    int want = Mathf.RoundToInt(p1.xpAmount * A.GetHarvestXP());
                    Check(Near(got, want) && got > base0, $"bitki kesince XP {N(base0)} → {N(got)} (beklenen {want})");
                    break;
                }
                case StatType.HarvestScoreMultiplier:
                {
                    ResetTiles(); long s0 = HarvestScoreManager.Instance.TotalScore; var p0 = Kill(aSpawner); long g0 = HarvestScoreManager.Instance.TotalScore - s0;
                    Apply(so, mid); long s1 = HarvestScoreManager.Instance.TotalScore; var p1 = Kill(aSpawner); long g1 = HarvestScoreManager.Instance.TotalScore - s1;
                    float player = StatManager.Instance.GetFinalStat(StatType.HarvestScoreMultiplier, StatTarget.Player);
                    int want = HarvestScoreManager.CalculateAward(p1.rarity, player, A.GetHarvestScore(p1.rarity));
                    int plain = HarvestScoreManager.CalculateAward(p1.rarity, player, 1f);
                    Check(g1 == want && want >= plain, $"bitki kesince skor {g0} ({p0.rarity}) → {g1} ({p1.rarity}; tile'sız {plain}, beklenen {want})");
                    break;
                }
                case StatType.PlanterDamageMultiplier:
                {
                    var health = Plant(aSpawner).GetComponent<PlantHealth>();
                    ResetTiles(); int d0 = health.GetIncomingDamage(1000, DamageType.Direct); Apply(so, mid); int d1 = health.GetIncomingDamage(1000, DamageType.Direct);
                    int e1 = health.GetIncomingDamage(1000, DamageType.Explosion);
                    Check(d1 > d0 && Near(d1, Math.Round(1000 * (1 + mid)), .002), $"vuruş hasarı 1000 → {d1} (patlama hasarına eklenmez: {e1})");
                    break;
                }
                case StatType.DuplicateChance:
                {
                    ResetTiles(); Apply(so, 1f);   // şans %100: sonuç rastgele olmasın
                    var type = F<PlantSO>(Plant(aSpawner).GetComponent<PlantResource>(), "plantData").resourceType;
                    int m0 = ResourceManager.Instance.GetResourceAmount(type); var p = Kill(aSpawner); int got = ResourceManager.Instance.GetResourceAmount(type) - m0;
                    var stat = type == ResourceType.Gold ? StatType.GoldGainMultiplier : type == ResourceType.Iron ? StatType.IronGainMultiplier : StatType.StoneGainMultiplier;
                    int single = Mathf.RoundToInt(p.rewardAmount * A.GetFinalStat(stat));
                    Check(got == single * 2, $"şans %100 iken ödül {single} yerine {got} (2 kat)");
                    break;
                }
                case StatType.ExplosionChance:
                {
                    ResetTiles();
                    var target = Plant(bSpawner).GetComponent<PlantHealth>(); int hp0 = target.CurrentHealth; Kill(aSpawner);
                    bool noTile = !target.IsDead && target.CurrentHealth == hp0;
                    Apply(so, 1f);
                    target = Plant(bSpawner).GetComponent<PlantHealth>(); int hp1 = target.CurrentHealth; Kill(aSpawner);
                    bool hit = target.IsDead || target.CurrentHealth < hp1;
                    Check(noTile && hit, $"şans %100 iken bitki kesilince komşu saksıdaki bitki hasar aldı (can {hp1} → {(target.IsDead ? "öldü" : target.CurrentHealth.ToString())}); tile yokken almadı");
                    break;
                }
                case StatType.TornadoChance:
                {
                    ResetTiles(); int t0 = Tornadoes(); Kill(aSpawner); int tNo = Tornadoes() - t0;
                    Apply(so, 1f); int t1 = Tornadoes(); Kill(aSpawner); int tYes = Tornadoes() - t1;
                    Check(tNo == 0 && tYes == 1, $"şans %100 iken bitki kesilince tornado çıktı ({tYes}); tile yokken {tNo}");
                    break;
                }
                case StatType.BoomerangChance:
                {
                    var h = HarvestBehaviorManager.Instance;
                    ResetTiles(); int b0 = h.ActiveBoomerangs; Kill(aSpawner); int bNo = h.ActiveBoomerangs - b0;
                    Apply(so, 1f); int b1 = h.ActiveBoomerangs; Kill(aSpawner); int bYes = h.ActiveBoomerangs - b1;
                    Check(bNo == 0 && bYes == 1, $"şans %100 iken bitki kesilince bumerang fırladı ({bYes}); tile yokken {bNo}");
                    break;
                }
                case StatType.ElectricChance:
                {
                    var h = HarvestBehaviorManager.Instance;
                    ResetTiles(); int e0 = h.ActiveElectricBursts; Kill(aSpawner); int eNo = h.ActiveElectricBursts - e0;
                    Apply(so, 1f); int e1 = h.ActiveElectricBursts; Kill(aSpawner); int eYes = h.ActiveElectricBursts - e1;
                    Check(eNo == 0 && eYes == 1, $"şans %100 iken bitki kesilince elektrik çaktı ({eYes}); tile yokken {eNo}");
                    break;
                }
                default:
                    Check(false, "bu stat için oyun kodunda okuyan yer bulunamadı");
                    break;
            }
        }

        Overflow(all, cards);

        // Saksısız tile: tasarım gereği etkisiz (bilgi satırı).
        ResetTiles();
        var free = GridManager.Instance.GetGridSystem().GetGridObject(new GridPosition(0, 0)).GetGroundCellCached();
        lines.Add($"== Bilgi == Saksı olmayan hücredeki tile'ın sahibi yok (Planter null: {free.Planter == null}); etkisi saksı konunca başlar.");
        return -1;
    }
}
