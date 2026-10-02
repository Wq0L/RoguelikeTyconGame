using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClickerGame.EconomyAnalysis;
using UnityEditor;
using UnityEngine;

// Run simülatörü: bir run'ı oyunun kendi verisi ve stat kurallarıyla round round oynatır.
// Kullanılanlar: CoreStat, 140 skill node (ön koşul, kademe maliyeti, para birimi, etki), saksı fiyatları ve spawn tabloları,
// bitki ödül/XP/canı, PlantHealthScaling, XP tablosu, kart tip/nadirlik ağırlıkları, tile değer aralıkları, rezonans kuralları.
// Savaş global: oyuncu her saldırıda imleç yarıçapındaki hücrelere vurur (PlayerController + GridSystem.GetGridObjectsInRadius).
// Modelde olmayan: davranış tile'larının (patlama, tornado, bumerang, elektrik) ek hasarı, koşullu elektrik XP'si.
public static class RunSimulator
{
    public sealed class Policy
    {
        public string Name;
        public float Accuracy = .8f;          // imleç verimi: yarıçaptaki hücrelerin ne kadarında canlı bitki yakalıyor
        public bool SkipWhenFull = true;      // grid doluyken kartı "Atla" ile kaynağa çevirir
        public bool ResonanceAware = true;    // kart seçerken ve saksı koyarken rezonans arar
        public bool SkillByTarget = true;     // skill'leri tasarım sırasıyla alır; değilse en ucuzdan
        public bool ReplaceSmall = true;      // 2x3 açılınca küçük saksıları satıp (yarı iade) 2x3'e geçer
        public float Hesitation;              // alınabilir bir kademeyi o round almama olasılığı (yeni oyuncu okur, bekler)
        public float MenuBase = 30, MenuCard = 15, MenuTier = 8;
        public bool PreferBehavior;           // kart seçerken davranış tipini (patlama, kasırga, bumerang, elektrik) nadirlikten önce tutar
        public TileModifierType? PreferType;  // kart seçerken bu tipi nadirlikten önce tutar (PreferBehavior'dan önce gelir)
        public string[] PriorityNodes;        // alınabilir olunca her şeyden önce alınan node'lar (sırayla)
    }

    public sealed class Rules
    {
        public string Name = "Şimdiki oyun";
        public int MaxRounds = 130;
        public List<Vector2> HpAnchors;       // null: PlantHealthScaling
        public Func<int, double> XpForLevel;  // null: ProgressionSO
        public bool UpgradeWhenFull;          // grid doluyken kart: 3 aday tile'dan birine yıldız (+%25 değer)
        public int MaxStars = 3;
        public float FailThreshold;           // >0: gelir zirvenin bu oranının altında FailRounds round kalırsa run biter
        public int FailRounds = 3, FailFrom = 20;  // RoundManager: exhaustRounds, exhaustFromRound
        public int[][] RarityTiers;           // null: CardSelectionUI formülü
        public int TierPerLuck;               // kademe = MutationLuck / 0.03 (Kart Sezgisi 10 kademe)
        public double Alive = .25;
        public float DurationCap;             // >0: round en fazla bu kadar; fazlası saldırı ve üretim hızına dönüşür
        public float MenuTierFactor = 1;      // skill kademesi birleştirme: menü süresi çarpanı
        public bool NoSpawnOverflow;          // true: tabanı aşan üretim hızı boşa gider (eski kural)
        public int ScreenCap;               // >0: round başına en fazla bu kadar kart ekranı; fazla level'lar kartların nadirliğini yükseltir
        public double XpFloor;                // >0: round XP'si en az mevcut level maliyetinin bu oranı
        public double PlantXpHpExp;           // >0: bitki XP'si × (can / round 1 canı)^üs
        public bool BatchCards;               // round başına 2'den fazla kart toplu ekranda (ek kart 3 sn)            // davranış vurduğunda hücrede canlı bitki olma olasılığı
        public int QuotaSegment;              // >0: Hasat Kotası (RoundManager): bu kadar round'da kazanılan skor kotanın altındaysa run biter
        public float QuotaStart = HarvestQuota.DefaultStart, QuotaGrowth = HarvestQuota.DefaultGrowth;
        public long[] QuotaTargets;           // run profili kota tablosu (RunProfileSO.segmentTargets); boş segment eğriden
        // Don Cephesi (FrostFrontEvent) yaklaşımı: segmentte, run başında seçilen kenar şeridindeki hücrelerin üretim tavanı / çarpan.
        public int FrostSegment; public float FrostMultiplier = 1.5f, FrostCoverage = .25f;
        public int[] FrostSegments;           // birden çok Don segmenti (aynı şerit; oyunda her olay kendi şeridini seçer)
        // Uzmanlaşma (SpecializationManager): SpecFromRound'dan itibaren doğrudan / davranış hasarı katsayıları.
        public int SpecFromRound; public float DirectMult = 1f, BehaviorMult = 1f;
        public float ResourceMult = 1f;       // hasat kaynağı çarpanı (yalnız hasat; kart atlama ödülü, satış iadesi, başlangıç parası hariç)
        public double BehaviorScale = 1;      // duyarlılık: davranış ek hasatının modeldeki payı × bu değer (1 = model)
        // Bölüm 2.2: >0 ise round süresi sabit (30–60 sn, tempo yok); yalnız süre veren node'lar alınmaz ve önkoşulda karşılanmış sayılır.
        public float FixedDuration;
        public int[] SnapshotRounds;          // bu round'ların başındaki durum RunResult.Snapshots'a yazılır (sahne ölçümü için)
        // Bölüm 3.2, YALNIZ ÖLÇÜM: kota tutmasa da run sürer ("kota nedeniyle elenme kapalı"). İlk başarısızlık QuotaFailedAt'e yazılır.
        // Oyunda karşılığı yok; kazanma oranı olarak okunmaz.
        public bool QuotaContinue;
    }

    // Bölüm 2.2: round başındaki simülatör durumu (skill etkileri, saksılar, tile'lar). Simülatör durumudur, insan oyunu verisi değil.
    public sealed class Snapshot
    {
        public int Round, Seed;
        public List<StatModifier> Global = new();
        public List<(PlanterSO so, List<Vector2Int> cells)> Planters = new();
        public List<(Vector2Int cell, TileModifierSO so, List<StatModifier> mods)> Tiles = new();
        public Dictionary<SkillNodeSO, int> Levels = new();
        public double Gold, Iron, Stone;
    }

    sealed class TileState { public TileModifierSO So; public List<StatModifier> Mods; public int Stars; }
    sealed class PlanterState { public PlanterSO So; public List<Vector2Int> Cells = new(); public int Spawners; }

    public sealed class RoundLog
    {
        public int Round, Level, Cards, Dead, Skips, Upgrades, Tiles, Unlocked, Planters, Resonances, TierCount, Screens, Overflow, MaxedTiles;
        public double Gold, Iron, Stone, Xp, Score, Harvest, Ceiling, Duration, HpCommon, Minutes, Tree, HarvestRatio, BehaviorHarvest, Income, Damage, Interval, Tempo;
        // Bölüm 3.2: savaş kapasitesi (doğrudan hasat), imleçteki hedef, üretime göre ağırlıklı ortalama vuruş ve tek vuruş olasılığı (model).
        public double Combat, Targets, AvgHits, OneShot, Radius, CritChance, CritMult;
    }

    public sealed class RunResult
    {
        public readonly List<RoundLog> Rounds = new();
        public readonly Dictionary<string, int> NodeDone = new();
        public readonly Dictionary<string, int> NodeFirst = new(); // Bölüm 3.1: düğümün ilk kademesinin alındığı round
        public int EndedAt;
        public int QuotaFailedAt;                           // ilk tutmayan kota round'u (0: hiç); QuotaContinue kapalıyken EndedAt ile aynı
        public readonly List<double> QuotaMargins = new(); // segment skoru / kota
        public readonly List<double> SegmentScores = new();
        public readonly Dictionary<string, int> Blocked = new() { ["Gold"] = 0, ["Iron"] = 0, ["Stone"] = 0 };
        public readonly List<Snapshot> Snapshots = new();
    }

    static CoreStatsSO core; static PlantHealthScalingSO health; static ProgressionSO progression; static ResonanceRulesSO resonance;
    static List<SkillNodeSO> nodes; static List<TileModifierSO> tileSOs; static List<PlanterSO> planterSOs;
    static readonly Dictionary<PlanterSO, int> spawnerCount = new();
    static readonly double[] RarityPower = { 1, 1.85, 2.9, 4.6 };
    static readonly Dictionary<TileModifierType, float> TypeWeights = new()
    {
        { TileModifierType.Fertile, 25 }, { TileModifierType.Water, 20 }, { TileModifierType.Crystal, 20 }, { TileModifierType.Energy, 15 },
        { TileModifierType.Explosive, 12 }, { TileModifierType.Duplicate, 8 }, { TileModifierType.Damage, 15 },
        { TileModifierType.Tornado, 10 }, { TileModifierType.Boomerang, 10 }, { TileModifierType.Electric, 10 }
    };
    const int Size = 11;

    static void Load()
    {
        if (core != null) return;
        core = AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
        health = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        resonance = Resources.Load<ResonanceRulesSO>("ResonanceRules");
        progression = AssetDatabase.FindAssets("t:ProgressionSO").Select(g => AssetDatabase.LoadAssetAtPath<ProgressionSO>(AssetDatabase.GUIDToAssetPath(g))).First();
        nodes = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(n => n.name, StringComparer.Ordinal).ToList();
        tileSOs = AssetDatabase.FindAssets("t:TileModifierSO", new[] { "Assets/ScriptableObjects/GridModifiers" })
            .Select(g => AssetDatabase.LoadAssetAtPath<TileModifierSO>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        planterSOs = AssetDatabase.FindAssets("t:PlanterSO", new[] { "Assets/ScriptableObjects/Planters" })
            .Select(g => AssetDatabase.LoadAssetAtPath<PlanterSO>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        foreach (var p in planterSOs)
        {
            var profile = EconomyEditorData.Defaults(); profile.planter = p;
            spawnerCount[p] = EconomyEditorData.Spawners(profile, out _);
        }
    }

    static readonly Dictionary<int, (double, double)> reachCache = new();
    static (double avg, double best) Reach(float radius)
    {
        int key = Mathf.RoundToInt(radius * 100);
        if (reachCache.TryGetValue(key, out var v)) return v;
        float sampledRadius = key / 100f; const int steps = 16; double sum = 0; int best = 0;
        for (int a = 0; a < steps; a++) for (int c = 0; c < steps; c++)
        {
            double ox = (a + .5) / steps * 2 - 1, oz = (c + .5) / steps * 2 - 1; int n = 0;
            for (int i = -6; i <= 6; i++) for (int j = -6; j <= 6; j++)
                if (HarvestArea.TouchesCell(new Vector3((float)ox, 0, (float)oz), new Vector3(i * 2, 0, j * 2), sampledRadius, 2f)) n++;
            sum += n; best = Math.Max(best, n);
        }
        return reachCache[key] = (sum / (steps * steps), best);
    }

    static double Weighted(ResourceType type, int cost) => cost * (type == ResourceType.Gold ? 1 : type == ResourceType.Iron ? 7 : 14);
    static bool IsBehavior(TileModifierSO t) => t.modifierType is TileModifierType.Explosive or TileModifierType.Tornado or TileModifierType.Boomerang or TileModifierType.Electric;

    // ---------------- tek run ----------------
    public static RunResult Run(Policy policy, Rules rules, int seed)
    {
        Load();
        var rng = new System.Random(seed);
        var result = new RunResult();
        var bank = new Dictionary<ResourceType, double> { [ResourceType.Gold] = 80, [ResourceType.Iron] = 0, [ResourceType.Stone] = 0 };
        var levels = nodes.ToDictionary(n => n, n => 0);
        var unlocks = new HashSet<UnlockType>();
        var tiles = new TileState[Size, Size];
        var owner = new PlanterState[Size, Size];
        var planters = new List<PlanterState>();
        var global = new List<StatModifier>();
        int level = 1, tierCount = 0; double xpInto = 0, minutes = 0, peakIncome = 0; int low = 0;
        int totalTiers = nodes.Sum(n => n.tiers.Count);

        float Player(StatType s) => StatCalculator.Calculate(core.GetBaseStat(s), s, StatTarget.Player, global, null);
        float All(StatType s, StatTarget t) => StatCalculator.Calculate(core.GetBaseStat(s), s, t, global, null);
        int GridSize() => Mathf.Clamp(Mathf.RoundToInt(All(StatType.GridUnlockSize, StatTarget.Grid)) / 2 * 2 + 1, 3, 11);
        bool Unlocked(int x, int z) { int h = GridSize() / 2; return Math.Abs(x - Size / 2) <= h && Math.Abs(z - Size / 2) <= h; }
        void RebuildGlobal()
        {
            global.Clear();
            foreach (var kv in levels) if (kv.Value > 0) global.AddRange(kv.Key.tiers[kv.Value - 1].effects);
        }

        // Saksı: hücrelerindeki tile'lar + rezonans
        (List<StatModifier> local, List<StatModifier> res, List<ActiveResonance> active) PlanterMods(PlanterState p)
        {
            var local = new List<StatModifier>(); var counts = new Dictionary<TileModifierType, int>();
            foreach (var c in p.Cells)
            {
                var t = tiles[c.x, c.y]; if (t == null) continue;
                local.AddRange(t.Mods); counts.TryGetValue(t.So.modifierType, out int n); counts[t.So.modifierType] = n + 1;
            }
            var res = new List<StatModifier>(); var active = new List<ActiveResonance>();
            ResonanceManager.Evaluate(resonance, counts, res, active);
            return (local, res, active);
        }

        int Hp(PlantSO plant, int round)
        {
            if (rules.HpAnchors == null) return health.Calculate(plant, round);
            var a = rules.HpAnchors; Vector2 lo = a[0], hi = a[a.Count - 1];
            foreach (var v in a) { if (v.x <= round && v.x >= lo.x) lo = v; }
            foreach (var v in a.AsEnumerable().Reverse()) { if (v.x >= round) hi = v; }
            double common = lo.x == hi.x ? lo.y : lo.y * Math.Pow(hi.y / lo.y, (round - lo.x) / (double)(hi.x - lo.x));
            double mult = health.rarityMultipliers[(int)plant.rarity];
            return (int)Math.Max(1, Math.Ceiling(common * mult * Math.Max(1, plant.maxHealth) / Math.Max(1f, health.referenceHealth) - 1e-6));
        }
        double XpReq(int l) => rules.XpForLevel != null ? rules.XpForLevel(l) : progression.GetXPForLevel(l);

        // Saksı yerleşimi: açık, boş hücrelerde; deneyimli oyuncu tile gücü + rezonans + yan yana olmayı arar
        bool TryPlace(PlanterSO so)
        {
            PlanterState best = null; double bestScore = double.NegativeInfinity;
            var options = new List<PlanterState>();
            foreach (bool rot in so.sizeX == so.sizeZ ? new[] { false } : new[] { false, true })
            {
                int sx = rot ? so.sizeZ : so.sizeX, sz = rot ? so.sizeX : so.sizeZ;
                for (int x = 0; x + sx <= Size; x++)
                for (int z = 0; z + sz <= Size; z++)
                {
                    bool ok = true; var cells = new List<Vector2Int>();
                    for (int i = 0; i < sx && ok; i++) for (int j = 0; j < sz && ok; j++)
                    {
                        int cx = x + i, cz = z + j;
                        if (!Unlocked(cx, cz) || owner[cx, cz] != null) ok = false; else cells.Add(new Vector2Int(cx, cz));
                    }
                    if (!ok) continue;
                    options.Add(new PlanterState { So = so, Cells = cells, Spawners = spawnerCount[so] });
                }
            }
            if (options.Count == 0) return false;
            if (!policy.ResonanceAware) best = options[rng.Next(options.Count)];
            else foreach (var o in options)
            {
                double s = 0; var counts = new Dictionary<TileModifierType, int>();
                foreach (var c in o.Cells)
                {
                    var t = tiles[c.x, c.y];
                    if (t != null) { s += RarityPower[(int)t.So.rarity]; counts.TryGetValue(t.So.modifierType, out int n); counts[t.So.modifierType] = n + 1; }
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    { var q = c + d; if (q.x >= 0 && q.y >= 0 && q.x < Size && q.y < Size && owner[q.x, q.y] != null) s += .15; }
                }
                var r = new List<StatModifier>(); var act = new List<ActiveResonance>(); ResonanceManager.Evaluate(resonance, counts, r, act);
                s += act.Count * 3 + rng.NextDouble() * .01;
                if (s > bestScore) { bestScore = s; best = o; }
            }
            foreach (var c in best.Cells) owner[c.x, c.y] = best;
            planters.Add(best);
            return true;
        }

        bool PlanterUnlocked(PlanterSO p) => p.requiredUnlock == UnlockType.None || unlocks.Contains(p.requiredUnlock);
        bool Disabled(SkillNodeSO n) => rules.FixedDuration > 0 && SkillTreeManager.IsDurationOnly(n);

        void Shop()
        {
            if (policy.ReplaceSmall && unlocks.Contains(UnlockType.Planter_2x3))
            {
                var big = planterSOs.First(p => p.requiredUnlock == UnlockType.Planter_2x3);
                bool swapped = true;
                while (swapped && bank[big.costType] >= big.cost)
                {
                    swapped = false; List<PlanterState> bestSell = null; List<Vector2Int> bestCells = null; double bestScore = double.NegativeInfinity;
                    foreach (bool rot in new[] { false, true })
                    {
                        int sx = rot ? big.sizeZ : big.sizeX, sz = rot ? big.sizeX : big.sizeZ;
                        for (int x = 0; x + sx <= Size; x++) for (int z = 0; z + sz <= Size; z++)
                        {
                            bool ok = true; var sell = new HashSet<PlanterState>(); var cells = new List<Vector2Int>();
                            for (int i = 0; i < sx && ok; i++) for (int j = 0; j < sz && ok; j++)
                            {
                                int cx = x + i, cz = z + j; var o = owner[cx, cz];
                                if (!Unlocked(cx, cz) || (o != null && o.Cells.Count >= 6)) ok = false;
                                else { cells.Add(new Vector2Int(cx, cz)); if (o != null) sell.Add(o); }
                            }
                            if (!ok || sell.Count == 0) continue;
                            // Satılan küçük saksıların bölge dışında kalan hücreleri boşa düşmesin: tamamen içeride olanları tercih et
                            double score = -sell.Sum(q => q.Cells.Count(c => c.x < x || c.x >= x + sx || c.y < z || c.y >= z + sz)) * 2 + cells.Sum(c => tiles[c.x, c.y] != null ? RarityPower[(int)tiles[c.x, c.y].So.rarity] : 0);
                            if (score > bestScore) { bestScore = score; bestSell = sell.ToList(); bestCells = cells; }
                        }
                    }
                    if (bestSell == null) break;
                    foreach (var q in bestSell) { planters.Remove(q); foreach (var c in q.Cells) owner[c.x, c.y] = null; bank[q.So.costType] += q.So.cost / 2; }
                    var ps = new PlanterState { So = big, Cells = bestCells, Spawners = spawnerCount[big] };
                    foreach (var c in bestCells) owner[c.x, c.y] = ps; planters.Add(ps); bank[big.costType] -= big.cost; swapped = true;
                }
            }
            // 1) Saksılar: açık alan varken en büyük açık ve alınabilir saksı
            bool bought = true;
            while (bought)
            {
                bought = false;
                bool onlyBig = policy.ReplaceSmall && unlocks.Contains(UnlockType.Planter_2x3);
                foreach (var p in planterSOs.Where(PlanterUnlocked).Where(p => !onlyBig || p.sizeX * p.sizeZ >= 6).OrderByDescending(p => p.sizeX * p.sizeZ))
                {
                    if (bank[p.costType] < p.cost) continue;
                    if (!TryPlace(p)) continue;
                    bank[p.costType] -= p.cost; bought = true; break;
                }
            }
            // 2) Skill kademeleri
            while (true)
            {
                SkillNodeSO pick = null; double pickKey = double.MaxValue;
                foreach (var n in nodes)
                {
                    if (Disabled(n)) continue;
                    int lv = levels[n]; if (lv >= n.tiers.Count) continue;
                    if (lv == 0 && n.prerequisites.Any(r => r.node == null || (!Disabled(r.node) && levels[r.node] < r.level))) continue;
                    var tier = n.tiers[lv];
                    if (bank[tier.costType] < tier.cost) { result.Blocked[tier.costType.ToString()]++; continue; }
                    if (policy.Hesitation > 0 && rng.NextDouble() < policy.Hesitation) continue;
                    double key = policy.SkillByTarget ? (n.targetRounds.x + n.targetRounds.y) * 1e6 + Weighted(tier.costType, tier.cost) : Weighted(tier.costType, tier.cost);
                    int priority = policy.PriorityNodes != null ? Array.IndexOf(policy.PriorityNodes, n.name) : -1;
                    if (priority >= 0) key = -1e9 + priority;
                    if (key < pickKey) { pickKey = key; pick = n; }
                }
                if (pick == null) break;
                var t2 = pick.tiers[levels[pick]];
                bank[t2.costType] -= t2.cost; levels[pick]++; tierCount++;
                if (levels[pick] == 1) result.NodeFirst[pick.name] = result.Rounds.Count + 1;
                if (levels[pick] == pick.tiers.Count) { result.NodeDone[pick.name] = result.Rounds.Count + 1; if (pick.unlockType != UnlockType.None) unlocks.Add(pick.unlockType); }
                RebuildGlobal();
            }
        }

        // Kart teklifi: 3 kart, tip sonra nadirlik (CardSelectionUI)
        TileModifierSO RollCard(float luck)
        {
            var avail = tileSOs.Where(t => t.requiredUnlock == UnlockType.None || unlocks.Contains(t.requiredUnlock)).ToList();
            var types = TypeWeights.Where(w => avail.Any(a => a.modifierType == w.Key)).ToList();
            double total = types.Sum(w => w.Value), roll = rng.NextDouble() * total, cum = 0; var type = types[0].Key;
            foreach (var w in types) { cum += w.Value; if (roll <= cum) { type = w.Key; break; } }
            double[] rw;
            if (rules.RarityTiers != null)
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(luck / .03f), 0, rules.RarityTiers.Length - 1);
                rw = rules.RarityTiers[tier].Select(v => (double)v).ToArray();
            }
            else rw = Enumerable.Range(0, 4).Select(i => (double)CardSelectionUI.RarityWeight(luck, (TileRarity)i)).ToArray();
            double rt = rw.Sum(), rr = rng.NextDouble() * rt, rc = 0; int rarity = 0;
            for (int i = 0; i < 4; i++) { rc += rw[i]; if (rr <= rc) { rarity = i; break; } }
            var pool = avail.Where(a => a.modifierType == type && (int)a.rarity == rarity).ToList();
            if (pool.Count == 0) pool = avail.Where(a => a.modifierType == type).ToList();
            return pool[rng.Next(pool.Count)];
        }

        List<StatModifier> RollMods(TileModifierSO so) => so.modifierRanges.Select(r => new StatModifier
        { statType = r.statType, target = r.target, operation = r.operation, value = r.minValue + (float)rng.NextDouble() * (r.maxValue - r.minValue) }).ToList();

        Shop(); // RunSetup: ilk round öncesi 80 altın

        // Don Cephesi şeridi: oyundaki gibi açık alanın dört kenar şeridinden biri; o anki bütün saksı hücrelerini kaplayan seçilmez.
        bool frostColumn = false; int frostFrom = -1, frostTo = -1;
        if (rules.FrostSegment > 0 || (rules.FrostSegments != null && rules.FrostSegments.Length > 0))
        {
            int minU = Size, maxU = -1;
            for (int x = 0; x < Size; x++) for (int z = 0; z < Size; z++) if (Unlocked(x, z)) { minU = Math.Min(minU, x); maxU = Math.Max(maxU, x); }
            int side = maxU - minU + 1, thick = Math.Min(side - 1, Math.Max(1, (int)Math.Round(side * rules.FrostCoverage, MidpointRounding.AwayFromZero)));
            var bands = new List<(bool col, int from, int to)> { (true, minU, minU + thick - 1), (true, maxU - thick + 1, maxU), (false, minU, minU + thick - 1), (false, maxU - thick + 1, maxU) };
            var cells = planters.SelectMany(q => q.Cells).ToList();
            var spared = bands.Where(b => cells.Count == 0 || cells.Any(c => (b.col ? c.x : c.y) < b.from || (b.col ? c.x : c.y) > b.to)).ToList();
            if (spared.Count > 0) bands = spared;
            var pick = bands[new System.Random(seed + Math.Max(rules.FrostSegment, rules.FrostSegments != null && rules.FrostSegments.Length > 0 ? rules.FrostSegments[0] : 0)).Next(bands.Count)];
            frostColumn = pick.col; frostFrom = pick.from; frostTo = pick.to;
        }
        bool InFrost(Vector2Int c) => frostFrom >= 0 && (frostColumn ? c.x : c.y) >= frostFrom && (frostColumn ? c.x : c.y) <= frostTo;

        for (int round = 1; round <= rules.MaxRounds; round++)
        {
            if (rules.SnapshotRounds != null && Array.IndexOf(rules.SnapshotRounds, round) >= 0)
            {
                var snap = new Snapshot { Round = round, Seed = seed, Global = new List<StatModifier>(global), Levels = new Dictionary<SkillNodeSO, int>(levels),
                    Gold = bank[ResourceType.Gold], Iron = bank[ResourceType.Iron], Stone = bank[ResourceType.Stone] };
                foreach (var p in planters) snap.Planters.Add((p.So, new List<Vector2Int>(p.Cells)));
                for (int x = 0; x < Size; x++) for (int z = 0; z < Size; z++)
                    if (tiles[x, z] != null) snap.Tiles.Add((new Vector2Int(x, z), tiles[x, z].So, new List<StatModifier>(tiles[x, z].Mods)));
                result.Snapshots.Add(snap);
            }
            var log = new RoundLog { Round = round };
            int currentSegment = rules.QuotaSegment > 0 ? (round - 1) / rules.QuotaSegment + 1 : 0;
            bool frostActive = rules.QuotaSegment > 0 && (currentSegment == rules.FrostSegment || (rules.FrostSegments != null && Array.IndexOf(rules.FrostSegments, currentSegment) >= 0));
            bool spec = rules.SpecFromRound > 0 && round >= rules.SpecFromRound;
            float directMult = spec ? rules.DirectMult : 1f, behaviorMult = spec ? rules.BehaviorMult : 1f, resourceMult = spec ? rules.ResourceMult : 1f;
            float duration = rules.FixedDuration > 0 ? Mathf.Clamp(rules.FixedDuration, 30, 60) : Mathf.Clamp(All(StatType.RoundDuration, StatTarget.All), 30, 90);
            float speedUp = 1f;
            if (rules.DurationCap > 0 && duration > rules.DurationCap) { speedUp = duration / rules.DurationCap; duration = rules.DurationCap; }
            float attack = Mathf.Max(.1f, Player(StatType.AttackSpeed)) / speedUp, dmg = Player(StatType.HarvestDamage);
            float cc = Mathf.Clamp01(Player(StatType.CritChance)), cm = Player(StatType.CritMultiplier), radius = Player(StatType.AreaRadius);
            double expDmg = dmg * (1 + cc * (cm - 1));
            // Yarıçaptaki hücre: imleç rastgele yerdeyken ortalama, en iyi yerdeyken en çok (GetGridObjectsInRadius, hücre 2 birim).
            // İsabet 0 = rastgele imleç, 1 = hep en iyi konum.
            var (avgCells, bestCells) = Reach(radius);
            int plantedCells = planters.Sum(p => p.Cells.Count);
            double targets = Math.Min(avgCells + policy.Accuracy * (bestCells - avgCells), plantedCells);

            double sumCeil = 0, sumHitsCeil = 0, sumCeilOneShot = 0; var per = new List<(double ceil, double hits, double g, double i, double s, double x, double sc, double extra)>();
            int unlockedCells = 0; for (int x = 0; x < Size; x++) for (int z = 0; z < Size; z++) if (Unlocked(x, z)) unlockedCells++;
            double density = unlockedCells > 0 ? plantedCells / (double)unlockedCells : 0;
            double avgKill(double dmgB) { double k = 0, w = 0; foreach (var e in planterSOs[0].spawnTable) { if (e.baseChance <= 0) continue; k += e.baseChance * Math.Min(1, dmgB / Math.Max(1, Hp(e.plant, round))); w += e.baseChance; } return w > 0 ? k / w : 0; }
            double alive = rules.Alive;
            int resonances = 0;
            foreach (var p in planters)
            {
                var (local, res, active) = PlanterMods(p); resonances += active.Count;
                float Pl(StatType s)
                {
                    float ordinary = StatCalculator.Calculate(p.So.GetBaseStat(s), s, StatTarget.Planter, global, local);
                    return StatCalculator.Calculate(ordinary, s, StatTarget.Planter, null, res);
                }
                float spawn = Pl(StatType.PlantSpawnRate), rare = Pl(StatType.RareSpawnChance), pdm = Pl(StatType.PlanterDamageMultiplier);
                if (!rules.NoSpawnOverflow)
                {
                    // PlanterBrain: tabanın altına inemeyen üretim hızı nadirliğe dönüşür.
                    float rawSpawn = StatCalculator.CalculateRaw(StatCalculator.CalculateRaw(p.So.GetBaseStat(StatType.PlantSpawnRate), StatType.PlantSpawnRate, StatTarget.Planter, global, local), StatType.PlantSpawnRate, StatTarget.Planter, null, res);
                    rare = StatCalculator.ClampStat(StatType.RareSpawnChance, rare + StatCalculator.SpawnOverflowRarity(rawSpawn));
                }
                float gm = Pl(StatType.GoldGainMultiplier), im = Pl(StatType.IronGainMultiplier), sm = Pl(StatType.StoneGainMultiplier);
                float xm = Pl(StatType.XPGainMultiplier), dup = Pl(StatType.DuplicateChance);
                float playerScore = Player(StatType.HarvestScoreMultiplier);
                float planterScore = StatCalculator.Calculate(p.So.GetBaseStat(StatType.HarvestScoreMultiplier), StatType.HarvestScoreMultiplier, StatTarget.Planter, global, local, false);
                double wsum = p.So.spawnTable.Sum(e => EconomyCalculator.AdjustedWeight(e, rare));
                double eg = 0, ei = 0, es = 0, ex = 0, esc = 0, hits = 0, oneShot = 0;
                foreach (var e in p.So.spawnTable)
                {
                    double pr = wsum > 0 ? EconomyCalculator.AdjustedWeight(e, rare) / wsum : 0; if (pr <= 0) continue;
                    var plant = e.plant; float mult = plant.resourceType == ResourceType.Gold ? gm : plant.resourceType == ResourceType.Iron ? im : sm;
                    double reward = Math.Max(0, Mathf.RoundToInt(plant.rewardAmount * mult)) * (1 + Math.Min(1, dup));
                    if (plant.resourceType == ResourceType.Gold) eg += pr * reward; else if (plant.resourceType == ResourceType.Iron) ei += pr * reward; else es += pr * reward;
                    double hpXp = rules.PlantXpHpExp > 0 ? Math.Pow(Hp(plant, round) / (double)Math.Max(1, Hp(plant, 1)), rules.PlantXpHpExp) : 1;
                    ex += pr * Mathf.RoundToInt((float)(plant.xpAmount * xm * hpXp));
                    esc += pr * HarvestScoreManager.CalculateAward(plant.rarity, playerScore, planterScore * ResonanceManager.Multiplier(active, StatType.HarvestScoreMultiplier, plant.rarity));
                    hits += pr * Math.Max(1, Hp(plant, round) / Math.Max(1e-6, expDmg * directMult * pdm));
                    oneShot += pr * OneShotChance(dmg * directMult * pdm, cc, cm, Hp(plant, round));
                }
                double ceil = p.Spawners * duration / (Math.Max(StatCalculator.MinimumSpawnInterval, spawn) / speedUp);
                if (frostActive)
                {
                    double f = p.Cells.Count(InFrost) / (double)Math.Max(1, p.Cells.Count);
                    ceil *= 1 - f + f / rules.FrostMultiplier;
                }
                // Davranışlar (sadece doğrudan hasatta tetiklenir, zincir yok): beklenen ek hasat / doğrudan hasat
                float ch(StatType s) => Mathf.Clamp01(Pl(s));
                double dmgRaw = dmg * behaviorMult;
                int nExp = 0, nEl = 0; var foot = new HashSet<Vector2Int>(p.Cells);
                foreach (var c in p.Cells) foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                { var q = c + d; if (!foot.Contains(q) && q.x >= 0 && q.y >= 0 && q.x < Size && q.y < Size && owner[q.x, q.y] != null) nExp++; }
                for (int dx = -1; dx <= 1; dx += 2) for (int dz = -1; dz <= 1; dz += 2)
                {
                    var corner = p.Cells[0]; foreach (var c in p.Cells) if (c.x * dx + c.y * dz > corner.x * dx + corner.y * dz) corner = c;
                    for (int st = 1; st <= 2; st++) { var q = new Vector2Int(corner.x + dx * st, corner.y + dz * st); if (!foot.Contains(q) && q.x >= 0 && q.y >= 0 && q.x < Size && q.y < Size && owner[q.x, q.y] != null) nEl++; }
                }
                double extra = alive * rules.BehaviorScale * (
                    ch(StatType.ExplosionChance) * nExp * avgKill(dmgRaw * ResonanceManager.BehaviorMultiplier(active, DamageType.Explosion)) +
                    ch(StatType.TornadoChance) * 8 * density * avgKill(Math.Max(1, Math.Round(dmgRaw * .5)) * ResonanceManager.BehaviorMultiplier(active, DamageType.Tornado)) +
                    ch(StatType.BoomerangChance) * 2.5 * density * avgKill(2 * Math.Max(1, Math.Round(dmgRaw * .65)) * ResonanceManager.BehaviorMultiplier(active, DamageType.Boomerang)) +
                    ch(StatType.ElectricChance) * nEl * avgKill(dmgRaw * ResonanceManager.BehaviorMultiplier(active, DamageType.Electric)));
                per.Add((ceil, hits, eg, ei, es, ex, esc, extra)); sumCeil += ceil; sumHitsCeil += ceil * hits; sumCeilOneShot += ceil * oneShot;
            }
            double harvests = 0;
            log.Targets = targets; log.Radius = radius; log.CritChance = cc; log.CritMult = cm;
            if (sumCeil > 0)
            {
                double avgHits = sumHitsCeil / sumCeil, combat = duration / attack * targets / avgHits;
                log.AvgHits = avgHits; log.Combat = combat; log.OneShot = sumCeilOneShot / sumCeil;
                harvests = Math.Min(sumCeil, combat);
                double direct = harvests, bonus = 0;
                foreach (var q in per) bonus += direct * q.ceil / sumCeil * q.extra;
                bonus = Math.Min(bonus, Math.Max(0, sumCeil - direct));
                log.BehaviorHarvest = bonus; harvests = direct + bonus;
                foreach (var q in per)
                {
                    double h = harvests * q.ceil / sumCeil;
                    double hr = h * resourceMult; // uzmanlaşma kaynak bedeli yalnız hasat gelirine
                    bank[ResourceType.Gold] += hr * q.g; bank[ResourceType.Iron] += hr * q.i; bank[ResourceType.Stone] += hr * q.s;
                    log.Gold += hr * q.g; log.Iron += hr * q.i; log.Stone += hr * q.s; log.Xp += h * q.x; log.Score += h * q.sc;
                }
            }
            log.Harvest = harvests; log.Ceiling = sumCeil; log.HarvestRatio = sumCeil > 0 ? harvests / sumCeil : 0;
            log.Duration = duration; log.Resonances = resonances; log.Damage = dmg; log.Interval = attack; log.Tempo = speedUp;
            log.HpCommon = Hp(planterSOs[0].spawnTable[0].plant, round);

            // Level ve kartlar (round içinde kazanılan XP; seçimler round sonunda)
            if (rules.XpFloor > 0) log.Xp = Math.Max(log.Xp, rules.XpFloor * XpReq(level));
            xpInto += log.Xp; int pending = 0;
            while (xpInto >= XpReq(level)) { xpInto -= XpReq(level); level++; pending++; }
            int skips = 1 + Mathf.RoundToInt(All(StatType.CardSkip, StatTarget.All));
            float luck = All(StatType.MutationLuck, StatTarget.Mutation);
            int screens = rules.ScreenCap > 0 ? Math.Min(pending, rules.ScreenCap) : pending;
            int surplus = pending - screens; log.Screens = screens;
            for (int c = 0; c < screens; c++)
            {
                log.Cards++;
                var offer = new[] { RollCard(luck), RollCard(luck), RollCard(luck) };
                int bestR = offer.Max(o => (int)o.rarity);
                var bestCards = offer.Where(o => (int)o.rarity == bestR).ToList();
                if (policy.PreferType.HasValue && offer.Any(o => o.modifierType == policy.PreferType.Value))
                {
                    var typed = offer.Where(o => o.modifierType == policy.PreferType.Value).ToList();
                    bestR = typed.Max(o => (int)o.rarity);
                    bestCards = typed.Where(o => (int)o.rarity == bestR).ToList();
                }
                else if (policy.PreferBehavior && offer.Any(IsBehavior))
                {
                    var behaviorCards = offer.Where(IsBehavior).ToList();
                    bestR = behaviorCards.Max(o => (int)o.rarity);
                    bestCards = behaviorCards.Where(o => (int)o.rarity == bestR).ToList();
                }
                TileModifierSO chosen = bestCards[rng.Next(bestCards.Count)];
                if (policy.ResonanceAware && bestCards.Count > 1)
                {
                    // Saksıların altında en çok bulunan tipe yakın olanı seç
                    var have = new Dictionary<TileModifierType, int>();
                    foreach (var p in planters) foreach (var cell in p.Cells) { var t = tiles[cell.x, cell.y]; if (t != null) { have.TryGetValue(t.So.modifierType, out int n); have[t.So.modifierType] = n + 1; } }
                    chosen = bestCards.OrderByDescending(o => have.TryGetValue(o.modifierType, out int n) ? n : 0).First();
                }
                // Fazla level'lar: bu ekranın kartı nadirlik atlar (Legendary'nin üstü boşa).
                int boost = rules.ScreenCap > 0 ? surplus / screens + (c < surplus % screens ? 1 : 0) : 0;
                if (boost > 0)
                {
                    int target = Math.Min(3, bestR + boost); log.Overflow += bestR + boost - target;
                    var up = tileSOs.FirstOrDefault(t => t.modifierType == chosen.modifierType && (int)t.rarity == target && (t.requiredUnlock == UnlockType.None || unlocks.Contains(t.requiredUnlock)));
                    if (up != null) { chosen = up; bestR = target; }
                }
                var eligible = new List<Vector2Int>();
                for (int x = 0; x < Size; x++) for (int z = 0; z < Size; z++) if (Unlocked(x, z) && tiles[x, z] == null) eligible.Add(new Vector2Int(x, z));
                if (eligible.Count > 0)
                {
                    var cell = eligible[rng.Next(eligible.Count)];
                    tiles[cell.x, cell.y] = new TileState { So = chosen, Mods = RollMods(chosen) };
                    continue;
                }
                if (policy.SkipWhenFull && skips > 0)
                {
                    skips--; log.Skips++;
                    int[] baseReward = { 2, 4, 7, 10 };
                    int reward = Mathf.RoundToInt(baseReward[bestR] * (1f + round * .1f));
                    var res = (ResourceType)rng.Next(3); bank[res] += reward;
                    continue;
                }
                if (rules.UpgradeWhenFull)
                {
                    var cand = new List<TileState>();
                    foreach (var p in planters) foreach (var cell in p.Cells) { var t = tiles[cell.x, cell.y]; if (t != null && t.Stars < rules.MaxStars) cand.Add(t); }
                    if (cand.Count > 0)
                    {
                        var picks = cand.OrderBy(_ => rng.Next()).Take(3).ToList();
                        var t = picks.OrderByDescending(x => RarityPower[(int)x.So.rarity]).First();
                        int add = bestR <= 1 ? 1 : bestR == 2 ? 2 : 3; int before = t.Stars; t.Stars = Math.Min(rules.MaxStars, t.Stars + add);
                        float factor = (1 + .25f * t.Stars) / (1 + .25f * before);
                        for (int k = 0; k < t.Mods.Count; k++) { var m = t.Mods[k]; m.value *= factor; t.Mods[k] = m; }
                        log.Upgrades++; continue;
                    }
                }
                log.Dead++;
            }

            Shop();

            log.Level = level; log.TierCount = tierCount; log.Tree = tierCount / (double)totalTiers;
            log.Planters = planters.Count;
            for (int x = 0; x < Size; x++) for (int z = 0; z < Size; z++) { if (Unlocked(x, z)) log.Unlocked++; if (tiles[x, z] != null) { log.Tiles++; if (tiles[x, z].Stars >= rules.MaxStars) log.MaxedTiles++; } }
            double cardSec = rules.BatchCards ? policy.MenuCard * Math.Min(2, log.Cards) + 3 * Math.Max(0, log.Cards - 2) : policy.MenuCard * log.Cards;
            minutes += (duration + policy.MenuBase + cardSec + policy.MenuTier * rules.MenuTierFactor * (result.Rounds.Count > 0 ? tierCount - result.Rounds[^1].TierCount : tierCount)) / 60.0;
            log.Minutes = minutes;
            result.Rounds.Add(log);

            if (rules.QuotaSegment > 0 && round % rules.QuotaSegment == 0)
            {
                double segScore = result.Rounds.Skip(result.Rounds.Count - rules.QuotaSegment).Sum(l => l.Score);
                int segment = round / rules.QuotaSegment;
                double quota = rules.QuotaTargets != null && segment <= rules.QuotaTargets.Length && rules.QuotaTargets[segment - 1] > 0
                    ? rules.QuotaTargets[segment - 1] : HarvestQuota.Target(segment, rules.QuotaStart, rules.QuotaGrowth);
                result.SegmentScores.Add(segScore);
                result.QuotaMargins.Add(segScore / quota);
                if (segScore < quota)
                {
                    if (result.QuotaFailedAt == 0) result.QuotaFailedAt = round;
                    if (!rules.QuotaContinue) { result.EndedAt = round; break; }
                }
            }

            double income = log.Gold + log.Iron * 7 + log.Stone * 14; log.Income = income;
            peakIncome = Math.Max(peakIncome, income);
            if (rules.FailThreshold > 0)
            {
                low = round >= rules.FailFrom && income < peakIncome * rules.FailThreshold ? low + 1 : 0;
                if (low >= rules.FailRounds) { result.EndedAt = round; break; }
            }
        }
        return result;
    }

    // ---------------- toplu çalıştırma ----------------
    // Kalibrasyon (2026-09-29): Deneyimli = kullanıcının run'ı (ağaç ~70–80'de tam), Yeni = arkadaşının run'ı (110'da ~%40, ~3 sa).
    public static readonly Policy[] Policies =
    {
        new Policy { Name = "Deneyimli", Accuracy = .75f },
        new Policy { Name = "Orta", Accuracy = .25f },
        new Policy { Name = "Yeni", Accuracy = 0f, SkipWhenFull = false, ResonanceAware = false, SkillByTarget = false, ReplaceSmall = false, Hesitation = .7f },
    };

    static double P(IEnumerable<double> v, double q) { var s = v.OrderBy(x => x).ToArray(); if (s.Length == 0) return double.NaN; return s[(int)Math.Round((s.Length - 1) * q)]; }

    public static string Report(Rules rules, int seeds, StringBuilder csv)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {rules.Name} (maxRounds {rules.MaxRounds}, {seeds} seed) ===");
        foreach (var policy in Policies)
        {
            var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, rules, 1000 + s)).ToList();
            int len = runs.Max(r => r.Rounds.Count);
            double At(RunResult r, int round, Func<RoundLog, double> f) => round <= r.Rounds.Count ? f(r.Rounds[round - 1]) : double.NaN;
            int FirstRound(RunResult r, Func<RoundLog, bool> f) { var x = r.Rounds.FirstOrDefault(f); return x?.Round ?? -1; }
            string Q(Func<RunResult, double> f) { var v = runs.Select(f).Where(x => !double.IsNaN(x) && x >= 0).ToList(); return v.Count == 0 ? "-" : $"{P(v, .1):0.#}/{P(v, .5):0.#}/{P(v, .9):0.#}" + (v.Count < runs.Count ? $" ({v.Count}/{runs.Count})" : ""); }
            sb.AppendLine($"-- {policy.Name} (isabet {policy.Accuracy}) --");
            sb.AppendLine($"  ağaç %25/%50/%75/%100 round (P10/P50/P90): {Q(r => FirstRound(r, l => l.Tree >= .25))} | {Q(r => FirstRound(r, l => l.Tree >= .5))} | {Q(r => FirstRound(r, l => l.Tree >= .75))} | {Q(r => FirstRound(r, l => l.Tree >= .999))}");
            foreach (int rr in new[] { 20, 40, 65, 80, 100, 110, 130 })
                sb.AppendLine($"  r{rr}: level {Q(r => At(r, rr, l => l.Level))} · ağaç% {Q(r => At(r, rr, l => l.Tree * 100))} · hasat oranı {Q(r => At(r, rr, l => l.HarvestRatio * 100))} · saksı {Q(r => At(r, rr, l => l.Planters))} · tile {Q(r => At(r, rr, l => l.Tiles))}/{Q(r => At(r, rr, l => l.Unlocked))} · rezonans {Q(r => At(r, rr, l => l.Resonances))} · dk {Q(r => At(r, rr, l => l.Minutes))}");
            int Wall(RunResult r) { double peak = 0; foreach (var l in r.Rounds) { peak = Math.Max(peak, l.Income); if (l.Round >= 40 && l.Income < peak * .5) return l.Round; } return -1; }
            sb.AppendLine($"  duvar (gelir zirvenin yarısına iniyor): {Q(r => Wall(r))} · davranış hasatı payı r65: {Q(r => At(r, 65, l => l.Harvest > 0 ? l.BehaviorHarvest / l.Harvest * 100 : 0))}%");
            sb.AppendLine($"  run sonu: round {Q(r => r.Rounds.Count)} · süre dk {Q(r => r.Rounds[^1].Minutes)} · son 20 roundda yeni node alınan round {Q(r => r.Rounds.Skip(Math.Max(0, r.Rounds.Count - 20)).Count(l => l.TierCount > (r.Rounds.IndexOf(l) > 0 ? r.Rounds[r.Rounds.IndexOf(l) - 1].TierCount : 0)))}/20");
            int Empty(RunResult r) { int n = 0; for (int i = 1; i < r.Rounds.Count; i++) { var a = r.Rounds[i - 1]; var b = r.Rounds[i]; if (b.TierCount == a.TierCount && b.Tiles == a.Tiles && b.Upgrades == 0) n++; } return n; }
            int AfterTree(RunResult r) { var f = r.Rounds.FirstOrDefault(l => l.Tree >= .999); return f == null ? 0 : r.Rounds.Count - f.Round; }
            sb.AppendLine($"  hiçbir şey olmayan round: {Q(r => Empty(r))} · ağaç bittikten sonra oynanan round: {Q(r => AfterTree(r))}");
            sb.AppendLine($"  boşa giden kart: {Q(r => r.Rounds.Sum(l => l.Dead))} · atlanan: {Q(r => r.Rounds.Sum(l => l.Skips))} · yükseltme: {Q(r => r.Rounds.Sum(l => l.Upgrades))}");
            if (rules.FailThreshold > 0) sb.AppendLine($"  Tarla Tükendi ile biten round: {Q(r => r.EndedAt > 0 ? r.EndedAt : double.NaN)}");
            var blocked = runs.SelectMany(r => r.Blocked).GroupBy(k => k.Key).Select(g => $"{g.Key} {g.Sum(x => x.Value) / runs.Count}");
            sb.AppendLine("  alım bekleten para birimi (round×node): " + string.Join(", ", blocked));
            // node zamanlaması: hedef round aralığıyla
            var late = new List<string>();
            foreach (var n in nodes)
            {
                var done = runs.Select(r => r.NodeDone.TryGetValue(n.name, out int v) ? v : 999).ToList();
                double med = P(done.Select(d => (double)d), .5);
                if (med > n.targetRounds.y + 10) late.Add($"{n.name} hedef {n.targetRounds.x}-{n.targetRounds.y} → {(med >= 999 ? "alınmadı" : med.ToString("0"))}");
            }
            sb.AppendLine($"  hedefinden 10+ round geç biten node: {late.Count}/{nodes.Count}" + (late.Count > 0 ? " · ilk 8: " + string.Join("; ", late.Take(8)) : ""));
            // csv: medyan eğriler
            for (int round = 1; round <= len; round++)
            {
                var alive = runs.Where(r => r.Rounds.Count >= round).Select(r => r.Rounds[round - 1]).ToList();
                if (alive.Count == 0) break;
                string M(Func<RoundLog, double> f) => P(alive.Select(f), .5).ToString("0.###", CultureInfo.InvariantCulture);
                csv.AppendLine(string.Join(",", rules.Name, policy.Name, round, alive.Count, M(l => l.Level), M(l => l.Tree), M(l => l.HarvestRatio), M(l => l.Gold + l.Iron * 7 + l.Stone * 14), M(l => l.Xp), M(l => l.HpCommon), M(l => l.Planters), M(l => l.Tiles), M(l => l.Unlocked), M(l => l.Resonances), M(l => l.Minutes), M(l => l.Duration), M(l => l.Dead)));
            }
        }
        return sb.ToString();
    }

    // XP/level denemesi (2026-09-29): 121 tavanı yerine sınırsız, yumuşak eğri; kart ekranı sınırı; taban XP.
    public static void RunXpBatch()
    {
        var text = new StringBuilder();
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Func<int, double> smooth = l => 600 + 5.3 * Math.Pow(l - 1, 1.636);
            Rules Base(string name) => new Rules { Name = name, UpgradeWhenFull = true, FailThreshold = .3f, DurationCap = 60 };
            var sets = new List<Rules>
            {
                Base("A Şimdiki tablo"),
                Set(Base("B Yumuşak eğri"), r => r.XpForLevel = smooth),
                Set(Base("C B + 3 ekran"), r => { r.XpForLevel = smooth; r.ScreenCap = 3; }),
                Set(Base("D C + taban %25"), r => { r.XpForLevel = smooth; r.ScreenCap = 3; r.XpFloor = .25; }),
                Set(Base("E D + bitki XP can^0.3"), r => { r.XpForLevel = l => 600 + 9 * Math.Pow(l - 1, 1.7); r.ScreenCap = 3; r.XpFloor = .25; r.PlantXpHpExp = .3; }),
            };
            foreach (var rules in sets) text.Append(XpReport(rules));
            text.AppendLine($"süre: {watch.Elapsed.TotalSeconds:0.0} sn");
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimXp.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    static Rules Set(Rules r, Action<Rules> change) { change(r); return r; }

    // Kota ölçümü (2026-09-30): Tarla Tükendi yerine 5 round'luk Harvest Score kotası için segment başına skor.
    public static void RunScoreBatch()
    {
        var text = new StringBuilder();
        try
        {
            var rules = new Rules { Name = "Kuralsız ölçüm", UpgradeWhenFull = true, DurationCap = 60 };
            foreach (var policy in Policies)
            {
                var runs = Enumerable.Range(0, 12).Select(s => Run(policy, rules, 1000 + s)).ToList();
                text.AppendLine($"-- {policy.Name} --");
                for (int seg = 1; seg <= 26; seg++)
                {
                    var sums = runs.Where(r => r.Rounds.Count >= seg * 5).Select(r => r.Rounds.Skip((seg - 1) * 5).Take(5).Sum(l => l.Score)).ToList();
                    var tree = runs.Where(r => r.Rounds.Count >= seg * 5).Select(r => r.Rounds[seg * 5 - 1].Tree * 100).ToList();
                    text.AppendLine($"  seg {seg} (r{(seg - 1) * 5 + 1}-{seg * 5}): skor P10 {P(sums, .1):0} · P50 {P(sums, .5):0} · P90 {P(sums, .9):0} · ağaç% P50 {P(tree, .5):0} · dk P50 {P(runs.Select(r => r.Rounds[seg * 5 - 1].Minutes), .5):0}");
                }
            }
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimScore.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // Bölüm 2 (2026-09-30): 20 round, kota 40/200/350/600, Don 2. ve 4. segment, round 11'den itibaren uzmanlaşma.
    // Simülatörde davranışlar doğrudan hasatın oranı olarak yaklaşık modellenir; sonuçlar yön gösterir, oyun ölçümünün yerine geçmez.
    public static void RunSpecializationBatch()
    {
        var text = new StringBuilder();
        try
        {
            Rules R(float d, float b) => new Rules { MaxRounds = 20, UpgradeWhenFull = true, DurationCap = 60, QuotaSegment = 5, QuotaTargets = new long[] { 40, 200, 350, 600 }, FrostSegments = new[] { 2, 4 }, SpecFromRound = 11, DirectMult = d, BehaviorMult = b };
            var options = new[] { ("Usta Biçici", 1.25f, .85f), ("Davranış Ustası", .85f, 1.25f), ("Mevcut Düzeni Koru", 1f, 1f) };
            const int seeds = 30;
            string Q(IEnumerable<double> v) { var l = v.ToList(); return l.Count == 0 ? "-" : $"{P(l, .1):0}/{P(l, .5):0}/{P(l, .9):0}"; }
            foreach (var policy in Policies)
            {
                text.AppendLine($"-- {policy.Name} ({seeds} seed; 10. round'u geçen run'lar uzmanlaşır) --");
                foreach (var (name, d, b) in options)
                {
                    var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, R(d, b), 4000 + s)).ToList();
                    var past10 = runs.Where(r => r.SegmentScores.Count >= 3 || r.EndedAt == 0 || r.EndedAt > 10).ToList();
                    text.AppendLine($"   {name}: 10'u geçen {past10.Count}/{seeds} · zafer {runs.Count(r => r.EndedAt == 0)}/{seeds} · 3. segment skoru (kota 350) {Q(past10.Where(r => r.SegmentScores.Count >= 3).Select(r => r.SegmentScores[2]))} · 4. segment (kota 600) {Q(runs.Where(r => r.SegmentScores.Count >= 4).Select(r => r.SegmentScores[3]))} · r15 davranış hasat payı {Q(past10.Where(r => r.Rounds.Count >= 15).Select(r => r.Rounds[14].Harvest > 0 ? r.Rounds[14].BehaviorHarvest / r.Rounds[14].Harvest * 100 : 0))}%");
                }
            }
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimSpecialization.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // Bölüm 2.1 (2026-09-30): ilerlemeli segment testi. Aynı seed ve aynı politika ile round 11–20: kazanılan kaynak her round sonunda
    // aynı alışveriş kuralıyla (saksı, skill kademesi) güce dönüşür; seçenekler yalnız round 11'den itibaren ayrılır.
    // 10. round'a (ilk boss) ulaşma seçenekten bağımsızdır ve ayrı raporlanır; sonraki oranlar yalnız ulaşan run'lar üzerinden.
    // Simülatör davranışları yaklaşık modeller (yalnız doğrudan hasatta tetik, zincir yok, "canlı bitki" olasılığı); sonuç yön gösterir.
    public static void RunSpecializationProgressBatch()
    {
        var text = new StringBuilder();
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Rules R((string name, float d, float b, float res) o, double behaviorScale) => new Rules
            {
                Name = o.name, MaxRounds = 20, UpgradeWhenFull = true, DurationCap = 60, QuotaSegment = 5, QuotaTargets = new long[] { 40, 200, 350, 600 },
                FrostSegments = new[] { 2, 4 }, SpecFromRound = 11, DirectMult = o.d, BehaviorMult = o.b, ResourceMult = o.res, BehaviorScale = behaviorScale
            };
            var options = new[]
            {
                ("Usta Biçici", 1.25f, .85f, 1f), ("Davranış Ustası 2.1 (kaynak ×0,90)", 1f, 1.25f, .9f), ("Mevcut Düzeni Koru", 1f, 1f, 1f),
                ("kontrol: Davranış bedelsiz", 1f, 1.25f, 1f), ("eski Davranış Ustası 2.0 (doğrudan ×0,85)", .85f, 1.25f, 1f),
            };
            var lean = new Policy { Name = "Deneyimli · davranış kartı öncelikli", Accuracy = .75f, PreferBehavior = true };
            var sets = Policies.Select(p => (p, 1.0)).Concat(new[] { (lean, 1.0), (lean, 3.0) }).ToList();
            const int seeds = 100;
            string Q(IEnumerable<double> v, string f = "0") { var l = v.Where(x => !double.IsNaN(x)).ToList(); return l.Count == 0 ? "-" : $"ort {l.Average().ToString(f)} · P10/P50/P90 {P(l, .1).ToString(f)}/{P(l, .5).ToString(f)}/{P(l, .9).ToString(f)}"; }
            bool Reached(RunResult r) => r.EndedAt == 0 || r.EndedAt > 10;
            double Sum(RunResult r, int from, int to, Func<RoundLog, double> f) => r.Rounds.Where(l => l.Round >= from && l.Round <= to).Sum(f);
            foreach (var (policy, scale) in sets)
            {
                var runs = options.Select(o => Enumerable.Range(0, seeds).Select(s => Run(policy, R(o, scale), 5000 + s)).ToList()).ToList();
                int koruIndex = 2;
                var koru = runs[koruIndex];
                var reachedSeeds = Enumerable.Range(0, seeds).Where(s => Reached(koru[s])).ToList();
                bool sameBefore = runs.All(rs => Enumerable.Range(0, seeds).All(s => Reached(rs[s]) == Reached(koru[s]) &&
                    Math.Abs(rs[s].Rounds[Math.Min(9, rs[s].Rounds.Count - 1)].Score - koru[s].Rounds[Math.Min(9, koru[s].Rounds.Count - 1)].Score) < 1e-9));
                text.AppendLine($"-- {policy.Name}{(scale != 1 ? $" · DUYARLILIK: davranış payı ×{scale:0} (kanıt değil)" : "")} ({seeds} seed, aynı seed kümesi) --");
                text.AppendLine($"   10. round'u (ilk boss) geçen: {reachedSeeds.Count}/{seeds} — uzmanlaşma öncesi, seçenekten bağımsız (r1–10 bütün seçeneklerde aynı: {(sameBefore ? "evet" : "HAYIR")})");
                text.AppendLine($"   r11–15 davranış hasat payı (Koru): {Q(reachedSeeds.Select(s => { var r = koru[s]; double h = Sum(r, 11, 15, l => l.Harvest); return h > 0 ? Sum(r, 11, 15, l => l.BehaviorHarvest) / h * 100 : 0; }), "0.0")}%");
                // Hasar ile bitki canı: hasar canın çok üstündeyse hasar katsayıları (doğrudan ya da davranış) sonucu değiştirmez.
                foreach (int rr in new[] { 5, 10, 11, 15, 20 })
                {
                    var alive = koru.Where(r => r.Rounds.Count >= rr).Select(r => r.Rounds[rr - 1]).ToList();
                    if (alive.Count == 0) continue;
                    text.AppendLine($"   r{rr} (Koru, {alive.Count} run): hasat hasarı P10/P50/P90 {P(alive.Select(l => (double)l.Damage), .1):0}/{P(alive.Select(l => (double)l.Damage), .5):0}/{P(alive.Select(l => (double)l.Damage), .9):0} · Common canı {alive[0].HpCommon:0}");
                }
                for (int o = 0; o < options.Length; o++)
                {
                    var rs = reachedSeeds.Select(s => runs[o][s]).ToList();
                    int pass15 = rs.Count(r => r.QuotaMargins.Count >= 3 && r.QuotaMargins[2] >= 1), wins = rs.Count(r => r.EndedAt == 0);
                    text.AppendLine($"   {options[o].Item1}: boss'a ulaşanlardan 15'i geçen {pass15}/{rs.Count} · zafer {wins}/{rs.Count}");
                    text.AppendLine($"      3. segment skoru (kota 350) {Q(rs.Select(r => r.SegmentScores[2]))} · 4. segment (kota 600, ulaşanlar) {Q(rs.Where(r => r.SegmentScores.Count >= 4).Select(r => r.SegmentScores[3]))}");
                    text.AppendLine($"      hasat geliri r11–15 Gold/Iron/Stone ort {rs.Average(r => Sum(r, 11, 15, l => l.Gold)):0}/{rs.Average(r => Sum(r, 11, 15, l => l.Iron)):0}/{rs.Average(r => Sum(r, 11, 15, l => l.Stone)):0}" +
                                    $" · r11–15 alınan skill kademesi {Q(rs.Select(r => (double)(r.Rounds[14].TierCount - r.Rounds[9].TierCount)), "0.0")} · r15 hasat hasarı {Q(rs.Select(r => r.Rounds[14].Damage), "0.0")}");
                    if (o == koruIndex) continue;
                    var paired = reachedSeeds.Select(s => (a: runs[o][s], b: koru[s])).ToList();
                    var ratio3 = paired.Select(p => p.a.SegmentScores[2] / Math.Max(1e-9, p.b.SegmentScores[2])).ToList();
                    int better = paired.Count(p => p.a.SegmentScores[2] > p.b.SegmentScores[2] + 1e-9), worse = paired.Count(p => p.a.SegmentScores[2] < p.b.SegmentScores[2] - 1e-9);
                    var tierDiff = paired.Select(p => (double)((p.a.Rounds[14].TierCount - p.a.Rounds[9].TierCount) - (p.b.Rounds[14].TierCount - p.b.Rounds[9].TierCount))).ToList();
                    var both4 = paired.Where(p => p.a.SegmentScores.Count >= 4 && p.b.SegmentScores.Count >= 4).Select(p => p.a.SegmentScores[3] / Math.Max(1e-9, p.b.SegmentScores[3])).ToList();
                    text.AppendLine($"      Koru'ya göre aynı seed: 3. segment skor oranı {Q(ratio3, "0.000")} (üstün {better} / geride {worse} / eşit {paired.Count - better - worse})" +
                                    $" · 4. segment oranı {Q(both4, "0.000")} · r11–15 skill kademesi farkı {Q(tierDiff, "0.0")}");
                }
            }
            text.AppendLine($"süre: {watch.Elapsed.TotalSeconds:0.0} sn");
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimSpecializationProgress.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // ---------------- Bölüm 2.2 ----------------
    // Elektrik öncelikli deneyimli oyuncu: elektrik kilidine giden yolu önce alır, kartta elektriği nadirlikten önce tutar.
    public static Policy ElectricLean => new Policy
    {
        Name = "Deneyimli · elektrik öncelikli", Accuracy = .75f, PreferType = TileModifierType.Electric,
        PriorityNodes = new[] { "Grid Genişleme I", "1×3 Saksı", "Patlayıcı Kartlar", "Çapraz Elektrik Kartları" }
    };

    // Deney 2.2 profilinin simülatör karşılığı (20 round, kota 40/200/350/600, Don 2. ve 4. segment). fixed > 0: süre kontrol koşulu.
    public static Rules ExperimentRules(float fixedDuration = 0, int[] snapshots = null) => new Rules
    {
        Name = fixedDuration > 0 ? $"Süre B (sabit {fixedDuration:0} sn)" : "Süre A (yükseltmeler + tempo)", MaxRounds = 20, UpgradeWhenFull = true, DurationCap = 60,
        QuotaSegment = 5, QuotaTargets = new long[] { 40, 200, 350, 600 }, FrostSegments = new[] { 2, 4 }, FixedDuration = fixedDuration, SnapshotRounds = snapshots
    };

    // Güç zaman çizelgesi (130 round kuralı, R50'ye kadar) + node erişim round'ları + süre A/B ilerlemeli karşılaştırma.
    public static void RunSpeedPowerBatch()
    {
        var text = new StringBuilder();
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Load();
            string Q(IEnumerable<double> v, string f = "0") { var l = v.Where(x => !double.IsNaN(x)).ToList(); return l.Count == 0 ? "-" : $"{P(l, .1).ToString(f)}/{P(l, .5).ToString(f)}/{P(l, .9).ToString(f)}"; }
            var policies = Policies.Concat(new[] { ElectricLean }).ToList();
            const int seeds = 40;
            // 1) Zaman çizelgesi: mevcut kural (130 round, Tarla/kota yok), R50'ye kadar.
            var longRules = new Rules { Name = "Mevcut kural", MaxRounds = 50, UpgradeWhenFull = true, DurationCap = 60 };
            var common = planterSOs[0].spawnTable[0].plant; var legendary = planterSOs[0].spawnTable.Select(e => e.plant).OrderByDescending(p => p.rarity).First();
            text.AppendLine("=== 1) Güç zaman çizelgesi (mevcut kural, 40 seed, P10/P50/P90) ===");
            text.AppendLine($"can (Common / Legendary): " + string.Join(" · ", new[] { 5, 10, 15, 20, 25, 30, 40, 50 }.Select(r => $"r{r} {health.Calculate(common, r)}/{health.Calculate(legendary, r)}")));
            var byPolicy = new Dictionary<string, List<RunResult>>();
            foreach (var policy in policies)
            {
                var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, longRules, 7000 + s)).ToList();
                byPolicy[policy.Name] = runs;
                text.AppendLine($"-- {policy.Name} --");
                foreach (int r in new[] { 5, 10, 15, 20, 25, 30, 40, 50 })
                {
                    var at = runs.Where(x => x.Rounds.Count >= r).Select(x => x.Rounds[r - 1]).ToList();
                    int hpC = health.Calculate(common, r), hpL = health.Calculate(legendary, r);
                    text.AppendLine($"   r{r}: hasar {Q(at.Select(l => l.Damage))} · saldırı aralığı sn {Q(at.Select(l => l.Interval), "0.00")} · süre {Q(at.Select(l => l.Duration))} · tempo {Q(at.Select(l => l.Tempo), "0.00")}" +
                                    $" · hasar×0,85 ≥ Common canı {at.Count(l => l.Damage * .85 >= hpC)}/{at.Count} · ≥ Legendary {at.Count(l => l.Damage * .85 >= hpL)}/{at.Count} · hasat/sn {Q(at.Select(l => l.Harvest / Math.Max(1, l.Duration)), "0.0")}");
                }
            }
            // 2) Node erişimi: aile başına ilk kademe ve tamamlanma round'u (P50; alınmadıysa '-').
            text.AppendLine("=== 2) Node erişim round'ları (mevcut kural, P50: tamamlanma; 50 round içinde) ===");
            string[] families = { "Biraz Daha Zaman", "Hızlı Eller", "Akıcı Kesim", "Yıldırım Kesim", "Uzun Hasat", "Son Vardiya", "Keskin Başlangıç", "Kesim Tekniği", "Güçlü Kesim",
                                  "Kesim Ustalığı", "Ağır Kesim", "Aşırı Güç", "Düzenli Üretim", "Verimli Üretim", "Seri Üretim", "Altın Hasat I", "Demir Hasat", "Hasat Rekoru",
                                  "Patlayıcı Kartlar", "Tornado Kartları", "Bumerang Orak Kartları", "Çapraz Elektrik Kartları", "Grid Genişleme", "Geniş Süpürüş", "Kritik Odak" };
            foreach (var fam in families)
            {
                var members = nodes.Where(n => n.name.StartsWith(fam)).OrderBy(n => n.name, StringComparer.Ordinal).ToList();
                if (members.Count == 0) continue;
                var cells = policies.Select(pol =>
                {
                    var runs = byPolicy[pol.Name];
                    string Done(SkillNodeSO n) { var v = runs.Select(r => r.NodeDone.TryGetValue(n.name, out int d) ? (double)d : 999).ToList(); double m = P(v, .5); return m >= 999 ? "-" : m.ToString("0"); }
                    return $"{pol.Name.Split(' ')[0]}{(pol == ElectricLeanRef(policies) ? "·E" : "")}: " + string.Join(" ", members.Select(Done));
                });
                text.AppendLine($"   {fam} ({members.Count} node): " + string.Join(" | ", cells));
            }
            // 3) Süre A / B (Deney 2.2 kuralı, 20 round): aynı seed, aynı politika. B30 ve B45, A'nın süre yolunu iki yandan sarar.
            text.AppendLine("=== 3) Süre A (yükseltme + tempo) / B30 / B45 (profil sabit, süre node'u yok) — Deney 2.2 kuralı, 100 seed; farklar toplam hasat süresinden de gelir ===");
            foreach (var policy in policies)
            {
                var conds = new[] { ("A", 0f), ("B30", 30f), ("B45", 45f) }
                    .Select(c => (name: c.Item1, runs: Enumerable.Range(0, 100).Select(s => Run(policy, ExperimentRules(c.Item2), 8000 + s)).ToList())).ToList();
                double Seconds(RunResult r, int from, int to) => r.Rounds.Where(l => l.Round >= from && l.Round <= to).Sum(l => l.Duration * l.Tempo);
                int Reach(List<RunResult> rs, int round) => rs.Count(r => r.EndedAt == 0 || r.EndedAt > round);
                string Each(Func<List<RunResult>, string> f) => string.Join(" / ", conds.Select(c => $"{c.name} {f(c.runs)}"));
                text.AppendLine($"-- {policy.Name} --");
                text.AppendLine($"   r10'u geçen {Each(rs => Reach(rs, 10).ToString())} · r15'i geçen {Each(rs => Reach(rs, 15).ToString())} · zafer {Each(rs => rs.Count(r => r.EndedAt == 0).ToString())}");
                text.AppendLine($"   etkili hasat saniyesi (süre × tempo) r1–10 {Each(rs => Q(rs.Select(r => Seconds(r, 1, 10))))} · r11–20 (ulaşanlar) {Each(rs => Q(rs.Where(r => r.Rounds.Count >= 20).Select(r => Seconds(r, 11, 20))))}");
                text.AppendLine($"   1. segment skoru {Each(rs => Q(rs.Select(r => r.SegmentScores[0])))} · 2. segment {Each(rs => Q(rs.Where(r => r.SegmentScores.Count >= 2).Select(r => r.SegmentScores[1])))}");
                text.AppendLine($"   r1–10 hasat geliri G/I/S ort {Each(rs => $"{rs.Average(r => r.Rounds.Take(10).Sum(l => l.Gold)):0}/{rs.Average(r => r.Rounds.Take(10).Sum(l => l.Iron)):0}/{rs.Average(r => r.Rounds.Take(10).Sum(l => l.Stone)):0}")} · r10 skill kademesi {Each(rs => Q(rs.Where(r => r.Rounds.Count >= 10).Select(r => (double)r.Rounds[9].TierCount)))}");
                text.AppendLine($"   saniye başına skor r6–10 {Each(rs => Q(rs.Where(r => r.Rounds.Count >= 10).Select(r => r.Rounds.Skip(5).Take(5).Sum(l => l.Score) / Seconds(r, 6, 10)), "0.00"))} · r16–20 {Each(rs => Q(rs.Where(r => r.Rounds.Count >= 20).Select(r => r.Rounds.Skip(15).Take(5).Sum(l => l.Score) / Seconds(r, 16, 20)), "0.00"))}");
            }
            text.AppendLine($"süre: {watch.Elapsed.TotalSeconds:0.0} sn");
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimSpeedPower.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    static Policy ElectricLeanRef(List<Policy> policies) => policies[policies.Count - 1];

    // Bölüm 3.1 (2026-09-30): düğüm başına erişim (mevcut kural, 50 round, kota yok). Simülatör ölçümüdür, insan verisi değil.
    // Satır: düğüm; politika başına ilk kademe P50, tamamlanma P50 ve R50'ye kadar tamamlayan run oranı. CSV: Logs/RunSimNodeAccess.csv
    public static void RunNodeAccessBatch()
    {
        var csv = new StringBuilder("node,policy,firstTierP50,completeP50,completedBy50,seeds\n");
        try
        {
            Load();
            var rules = new Rules { Name = "Mevcut kural 50", MaxRounds = 50, UpgradeWhenFull = true, DurationCap = 60 };
            const int seeds = 40;
            foreach (var policy in Policies)
            {
                var first = nodes.ToDictionary(n => n, n => new List<double>());
                var runs = new List<RunResult>();
                for (int s = 0; s < seeds; s++)
                {
                    var r = Run(policy, rules, 7000 + s);
                    runs.Add(r);
                    // ilk kademe: round logundaki kademe sayısı artışlarından çıkarılamaz; tamamlanma NodeDone'dan gelir.
                }
                foreach (var n in nodes)
                {
                    string Median(Func<RunResult, Dictionary<string, int>> map)
                    {
                        var v = runs.Select(r => map(r).TryGetValue(n.name, out int d) ? (double)d : 999).ToList();
                        return v.Count(x => x < 999) * 2 >= v.Count ? P(v, .5).ToString("0") : "-";
                    }
                    double share = runs.Count(r => r.NodeDone.ContainsKey(n.name)) / (double)runs.Count;
                    csv.AppendLine(string.Join(",", "\"" + n.name + "\"", policy.Name, Median(r => r.NodeFirst), Median(r => r.NodeDone), share.ToString("0.00", CultureInfo.InvariantCulture), seeds));
                }
            }
        }
        catch (Exception ex) { csv.AppendLine("FAIL," + ex.Message.Replace(',', ';')); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimNodeAccess.csv", csv.ToString());
        EditorApplication.Exit(0);
    }

    // ---------------- Bölüm 3.2: Run50 referans ölçümü ----------------
    public const string Reference50Path = "Assets/ScriptableObjects/RunProfiles/Run50_Referans.asset";
    public static readonly int[] Checkpoints = { 10, 20, 30, 40, 50 };
    const int Reference50SeedBase = 11000;

    // Tek vuruş olasılığı (model): PlayerController'daki sapma U(0,85–1,15), kritik (şans, çarpan) ve saksı çarpanı; yuvarlama yarım puanla yaklaşık.
    static double OneShotChance(double damage, double critChance, double critMult, int hp)
    {
        if (damage <= 0) return 0;
        double Chance(double d) => Math.Clamp((1.15 - (hp - .5) / d) / .3, 0, 1);
        return (1 - critChance) * Chance(damage) + critChance * Chance(damage * Math.Max(1, critMult));
    }

    // Run50_Referans profilinin simülatör karşılığı. Uzunluk, segment ve kota asset'ten okunur. Simülatörün modellemediği alanlar
    // (olay, uzmanlaşma, farklı başlangıç, sabit süre) doluysa durur: profil davranışı örtük kalmasın.
    // quotaContinue = true yalnız ölçüm içindir (koşul B): kota tutmasa da run sürer; oyunda karşılığı yoktur.
    public static Rules Reference50Rules(bool quotaContinue, int[] snapshots = null)
    {
        var p = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Reference50Path);
        if (p == null) throw new Exception("Run50_Referans.asset bulunamadı: " + Reference50Path);
        if (p.events.Count > 0 || p.specializationAfterSegment > 0 || p.startingGold != 80 || p.startingIron != 0 || p.startingStone != 0 || p.debugBudget || p.fixedRoundDuration > 0)
            throw new Exception("Run50_Referans simülatörün modellediği ayarlarda değil (olay, uzmanlaşma, başlangıç ekonomisi ya da sabit süre)");
        return new Rules
        {
            Name = quotaContinue ? "B · kota nedeniyle elenme kapalı (yalnız ölçüm)" : "A · gerçek kural (kota tutmazsa run biter)",
            MaxRounds = p.runLength, UpgradeWhenFull = true, DurationCap = RoundManager.RoundSecondsCap, QuotaSegment = p.segmentRounds,
            QuotaStart = p.quotaStart, QuotaGrowth = p.quotaGrowth, QuotaTargets = p.segmentTargets.ToArray(), QuotaContinue = quotaContinue, SnapshotRounds = snapshots
        };
    }

    // Sahne ölçümü için temsilî run (koşul B, batch ile aynı seed kümesi): kontrol round'larında skor ve hasarı medyana en yakın run.
    // Snapshots = R10/20/30/40/50 başındaki durum; Rounds = simülatörün aynı round'lar için hesapladığı değerler (karşılaştırma için).
    public static RunResult Reference50Representative(Policy policy, int seeds)
    {
        Load();
        var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, Reference50Rules(true, Checkpoints), Reference50SeedBase + s)).ToList();
        var medScore = Checkpoints.ToDictionary(r => r, r => P(runs.Select(x => x.Rounds[r - 1].Score), .5));
        var medDamage = Checkpoints.ToDictionary(r => r, r => P(runs.Select(x => x.Rounds[r - 1].Damage), .5));
        double Gap(double v, double m) => Math.Abs(Math.Log(Math.Max(1e-6, v) / Math.Max(1e-6, m)));
        double Distance(RunResult x) => Checkpoints.Sum(r => Gap(x.Rounds[r - 1].Score, medScore[r]) + Gap(x.Rounds[r - 1].Damage, medDamage[r]));
        return runs.OrderBy(Distance).First();
    }

    // Uç durum kontrolü için: bütün düğümlerin son kademe etkileri (tam ağaç). Normal R50 build'i değildir.
    public static List<StatModifier> FullTreeEffects()
    {
        Load();
        return nodes.Where(n => n.tiers.Count > 0).SelectMany(n => n.tiers[^1].effects).ToList();
    }

    // Ölçülen round'lar: R10/20/30/40/50. A = gerçek kural (kota tutmazsa run biter), B = yalnız ölçüm (elenme kapalı).
    // Aynı seed'lerde A, B'nin ilk kota başarısızlığına kadar olan kısmıyla aynıdır (kontrol edilir). Simülatör ölçümüdür, insan verisi değil.
    public static void RunReference50Batch()
    {
        var text = new StringBuilder();
        var csv = new StringBuilder("condition,policy,round,alive,damage,interval,duration,tempo,level,cardsTotal,tiles,unlocked,harvest,ceiling,combat,harvestRatio,oneShot,avgHits,score,income,minutes,behaviorShare\n");
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Load();
            const int seeds = 100;
            var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Reference50Path);
            var longRun = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/UzunRun130.asset");
            var rulesA = Reference50Rules(false); var rulesB = Reference50Rules(true);
            int segments = profile.runLength / profile.segmentRounds;
            string Q(IEnumerable<double> v, string f = "0") { var l = v.Where(x => !double.IsNaN(x)).ToList(); return l.Count == 0 ? "-" : $"{P(l, .1).ToString(f)}/{P(l, .5).ToString(f)}/{P(l, .9).ToString(f)}"; }
            string Pct(int n, int of) => of == 0 ? "-" : $"%{100.0 * n / of:0}";
            var plants = planterSOs[0].spawnTable.Select(e => e.plant).Where(p => p != null).GroupBy(p => p.rarity).Select(g => g.First()).OrderBy(p => p.rarity).ToList();
            string[] keyNodes =
            {
                "Biraz Daha Zaman - 1", "Keskin Başlangıç - 1", "Hızlı Eller - 1", "Grid Genişleme I", "2×2 Saksı", "2×3 Saksı", "Patlayıcı Kartlar", "Tornado Kartları",
                "Bumerang Orak Kartları", "Çapraz Elektrik Kartları", "Kesim Tekniği - 1", "Geniş Süpürüş - 1", "Düzenli Üretim - 1", "Güçlü Kesim - 1", "Kritik Odak - 1",
                "Uzun Hasat - 1", "Grid Genişleme II", "Ağır Kesim - 1", "Akıcı Kesim - 1", "Kesim Ustalığı - 1", "Verimli Üretim - 1", "Son Vardiya - 1",
                "Hasat Rekoru - 1", "Plazma Kesim - 1", "Aşırı Güç I"
            };
            var missing = keyNodes.Where(k => nodes.All(n => n.name != k)).ToList();
            if (missing.Count > 0) throw new Exception("düğüm bulunamadı: " + string.Join(", ", missing));

            text.AppendLine("=== Run50_Referans · simülatör (insan verisi değil) ===");
            text.AppendLine($"profil (asset): {profile.runLength} round · {profile.segmentRounds} round'luk {segments} segment · kota {profile.quotaStart} × {profile.quotaGrowth}^(segment−1), HarvestQuota.Nice · tablo {(profile.segmentTargets.Count == 0 ? "boş (eğri)" : string.Join("/", profile.segmentTargets))}" +
                            $" · olay {profile.events.Count} · uzmanlaşma segmenti {profile.specializationAfterSegment} · başlangıç {profile.startingGold}G/{profile.startingIron}I/{profile.startingStone}S · debug bütçe {profile.debugBudget} · sabit süre {profile.fixedRoundDuration}");
            text.AppendLine("kotalar (Run50_Referans / UzunRun130): " + string.Join(" ", Enumerable.Range(1, segments).Select(s => $"r{s * profile.segmentRounds} {profile.TargetFor(s)}/{longRun.TargetFor(s)}")) +
                            $" · aynı: {(Enumerable.Range(1, segments).All(s => profile.TargetFor(s) == longRun.TargetFor(s)) ? "evet" : "HAYIR")}");
            text.AppendLine($"kural: kart grid doluyken tile yükseltir (UpgradeWhenFull), süre 60 sn üstü tempo (DurationCap 60), Tarla Tükendi yok, {seeds} seed ({Reference50SeedBase}+), politika başına aynı seed kümesi.");
            text.AppendLine("can (PlantHealthScaling, nadirlik başına): " + string.Join(" · ", Checkpoints.Select(r => $"R{r} " + string.Join("/", plants.Select(p => health.Calculate(p, r))))) + $" ({string.Join("/", plants.Select(p => p.rarity))})");

            foreach (var policy in Policies)
            {
                var runsA = Enumerable.Range(0, seeds).Select(s => Run(policy, rulesA, Reference50SeedBase + s)).ToList();
                var runsB = Enumerable.Range(0, seeds).Select(s => Run(policy, rulesB, Reference50SeedBase + s)).ToList();
                bool same = Enumerable.Range(0, seeds).All(s => runsA[s].Rounds.Count <= runsB[s].Rounds.Count && runsA[s].QuotaFailedAt == runsB[s].QuotaFailedAt &&
                    Enumerable.Range(0, runsA[s].Rounds.Count).All(i => runsA[s].Rounds[i].Score == runsB[s].Rounds[i].Score && runsA[s].Rounds[i].Level == runsB[s].Rounds[i].Level));
                text.AppendLine();
                text.AppendLine($"################ {policy.Name} (isabet {policy.Accuracy}) · {seeds} seed ################");
                text.AppendLine($"A ile B aynı seed'de ilk kota başarısızlığına kadar aynı: {(same ? "evet" : "HAYIR")}");

                // ---- A: gerçek kural ----
                var failed = runsA.Where(r => r.EndedAt > 0).ToList();
                text.AppendLine($"--- A · gerçek kural: R50'yi tamamlayan {runsA.Count(r => r.EndedAt == 0)}/{seeds} · kotaya takılan {failed.Count}/{seeds} ---");
                text.AppendLine("   kontrol round'una ulaşan: " + string.Join(" · ", Checkpoints.Select(r => $"R{r} {runsA.Count(x => x.Rounds.Count >= r)}/{seeds}")));
                if (failed.Count > 0)
                {
                    text.AppendLine("   takılma round'ları: " + string.Join(", ", failed.GroupBy(r => r.EndedAt).OrderBy(g => g.Key).Select(g => $"R{g.Key}×{g.Count()}")));
                    text.AppendLine($"   takıldığı an (erişilen içerik): level {Q(failed.Select(r => (double)r.Rounds[^1].Level))} · ağaç% {Q(failed.Select(r => r.Rounds[^1].Tree * 100))} · saksı {Q(failed.Select(r => (double)r.Rounds[^1].Planters))}" +
                                    $" · tile {Q(failed.Select(r => (double)r.Rounds[^1].Tiles))}/{Q(failed.Select(r => (double)r.Rounds[^1].Unlocked))} · hasar {Q(failed.Select(r => r.Rounds[^1].Damage))} · segment skoru/kota {Q(failed.Select(r => r.QuotaMargins[^1]), "0.00")}");
                    var unlockNames = new[] { "2×2 Saksı", "2×3 Saksı", "Patlayıcı Kartlar", "Tornado Kartları", "Bumerang Orak Kartları", "Çapraz Elektrik Kartları", "Hızlı Eller - 1", "Grid Genişleme II" };
                    text.AppendLine("   takılanlarda açık olan: " + string.Join(" · ", unlockNames.Select(n => $"{n} {Pct(failed.Count(r => r.NodeFirst.TryGetValue(n, out int v) && v <= r.EndedAt), failed.Count)}")));
                }
                text.AppendLine("   segment skoru / kota (o segmente ulaşan run'lar; P10/P50/P90 · en düşük · ulaşan):");
                for (int s = 1; s <= segments; s++)
                {
                    var m = runsA.Where(r => r.QuotaMargins.Count >= s).Select(r => r.QuotaMargins[s - 1]).ToList();
                    text.AppendLine($"      seg {s} (R{(s - 1) * profile.segmentRounds + 1}–{s * profile.segmentRounds}, kota {profile.TargetFor(s)}): {Q(m, "0.0")} · en düşük {(m.Count > 0 ? m.Min().ToString("0.00") : "-")} · {m.Count}/{seeds}");
                }

                // ---- B: elenme kapalı (yalnız ölçüm) ----
                text.AppendLine($"--- B · kota nedeniyle elenme kapalı (yalnız ölçüm; kazanma oranı değildir): {seeds}/{seeds} run R50'ye kadar ölçüldü; kotası en az bir kez tutmayan {runsB.Count(r => r.QuotaFailedAt > 0)} ---");
                text.AppendLine("   P10/P50/P90, her satır bütün run'lar (n=" + seeds + ")");
                foreach (int r in Checkpoints)
                {
                    var at = runsB.Select(x => x.Rounds[r - 1]).ToList();
                    text.AppendLine($"   R{r}:");
                    text.AppendLine($"      hasar {Q(at.Select(l => l.Damage))} · kritik %{Q(at.Select(l => l.CritChance * 100))} ×{Q(at.Select(l => l.CritMult), "0.0")} · saldırı aralığı (tempo sonrası) {Q(at.Select(l => l.Interval), "0.00")} sn · yarıçap {Q(at.Select(l => l.Radius), "0.00")} · imleçteki hedef {Q(at.Select(l => l.Targets), "0.0")}");
                    text.AppendLine($"      süre {Q(at.Select(l => l.Duration))} sn · tempo {Q(at.Select(l => l.Tempo), "0.00")} · toplam oturum {Q(runsB.Select(x => x.Rounds[r - 1].Minutes))} dk");
                    foreach (var p in plants)
                    {
                        int hp = health.Calculate(p, r);
                        var hits = at.Select(l => Math.Ceiling(hp / Math.Max(1e-6, l.Damage))).ToList();
                        text.AppendLine($"      {p.rarity,-9} can {hp,6} · vuruş (ort. hasar, kritiksiz) {Q(hits)} · kesin tek vuruş (hasar×0,85 ≥ can) {Pct(at.Count(l => l.Damage * .85 >= hp), at.Count)}" +
                                        $" · tek vuruş olasılığı (model, sapma+kritik) {Q(at.Select(l => OneShotChance(l.Damage, l.CritChance, l.CritMult, hp) * 100))}% · tek hedef hasat süresi ≈ vuruş × aralık {Q(at.Select(l => Math.Ceiling(hp / Math.Max(1e-6, l.Damage)) * l.Interval), "0.0")} sn");
                    }
                    text.AppendLine($"      üretime göre ağırlıklı: tek vuruş (model) %{Q(at.Select(l => l.OneShot * 100))} · ortalama vuruş (kritikli) {Q(at.Select(l => l.AvgHits), "0.00")}");
                    text.AppendLine($"      level {Q(at.Select(l => (double)l.Level))} · alınan kart (toplam) {Q(runsB.Select(x => (double)x.Rounds.Take(r).Sum(l => l.Cards)))} · tile {Q(at.Select(l => (double)l.Tiles))}/{Q(at.Select(l => (double)l.Unlocked))} açık hücre" +
                                    $" · yükseltme (toplam) {Q(runsB.Select(x => (double)x.Rounds.Take(r).Sum(l => l.Upgrades)))} · max tile {Q(at.Select(l => (double)l.MaxedTiles))} · işlevsiz kart (oyunda Temel güç) {Q(runsB.Select(x => (double)x.Rounds.Take(r).Sum(l => l.Dead)))}" +
                                    $" · atlanan {Q(runsB.Select(x => (double)x.Rounds.Take(r).Sum(l => l.Skips)))} · saksı {Q(at.Select(l => (double)l.Planters))} · rezonans {Q(at.Select(l => (double)l.Resonances))} · ağaç% {Q(at.Select(l => l.Tree * 100))}");
                    text.AppendLine($"      round geliri G/I/S {Q(at.Select(l => l.Gold))}/{Q(at.Select(l => l.Iron))}/{Q(at.Select(l => l.Stone))} · ağırlıklı (G + 7I + 14S) {Q(at.Select(l => l.Income))} · round skoru {Q(at.Select(l => l.Score))}");
                    int seg = r / profile.segmentRounds; long quota = profile.TargetFor(seg);
                    var segScores = runsB.Select(x => x.SegmentScores[seg - 1]).ToList();
                    text.AppendLine($"      segment {seg} skoru {Q(segScores)} · kota {quota} · skor/kota {Q(segScores.Select(v => v / quota), "0.0")} · kotanın altında {segScores.Count(v => v < quota)}/{seeds}");
                    text.AppendLine($"      darboğaz: hasat/üretim tavanı %{Q(at.Select(l => l.HarvestRatio * 100))} · doğrudan savaş kapasitesi/üretim tavanı {Q(at.Select(l => l.Ceiling > 0 ? l.Combat / l.Ceiling : 0), "0.00")}" +
                                    $" · savaş sınırlı (kapasite < tavan) {at.Count(l => l.Combat < l.Ceiling)}/{seeds} · hasat/round {Q(at.Select(l => l.Harvest))} · tavan/round {Q(at.Select(l => l.Ceiling))}");
                    text.AppendLine($"      davranış ek hasat payı (MODEL, güvenilir değil) %{Q(at.Select(l => l.Harvest > 0 ? l.BehaviorHarvest / l.Harvest * 100 : 0))}");
                }
                text.AppendLine("   düğüm erişimi (ilk kademe o round'da aktif olan run oranı, B):");
                foreach (var n in keyNodes)
                    text.AppendLine($"      {n,-26} " + string.Join(" · ", Checkpoints.Select(r => $"R{r} {Pct(runsB.Count(x => x.NodeFirst.TryGetValue(n, out int v) && v <= r), seeds)}")));

                foreach (var (cond, runs) in new[] { ("A", runsA), ("B", runsB) })
                    for (int round = 1; round <= profile.runLength; round++)
                    {
                        var alive = runs.Where(x => x.Rounds.Count >= round).Select(x => x.Rounds[round - 1]).ToList();
                        if (alive.Count == 0) break;
                        string M(Func<RoundLog, double> f) => P(alive.Select(f), .5).ToString("0.###", CultureInfo.InvariantCulture);
                        string cards = P(runs.Where(x => x.Rounds.Count >= round).Select(x => (double)x.Rounds.Take(round).Sum(l => l.Cards)), .5).ToString("0", CultureInfo.InvariantCulture);
                        csv.AppendLine(string.Join(",", cond, policy.Name, round, alive.Count, M(l => l.Damage), M(l => l.Interval), M(l => l.Duration), M(l => l.Tempo), M(l => l.Level), cards, M(l => l.Tiles), M(l => l.Unlocked),
                            M(l => l.Harvest), M(l => l.Ceiling), M(l => l.Combat), M(l => l.HarvestRatio), M(l => l.OneShot), M(l => l.AvgHits), M(l => l.Score), M(l => l.Income), M(l => l.Minutes), M(l => l.Harvest > 0 ? l.BehaviorHarvest / l.Harvest : 0)));
                    }
            }
            text.AppendLine();
            text.AppendLine($"süre: {watch.Elapsed.TotalSeconds:0.0} sn");
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimReference50.txt", text.ToString());
        File.WriteAllText("Logs/RunSimReference50.csv", csv.ToString());
        EditorApplication.Exit(0);
    }

    // Bölüm 3.4: boss round'larındaki (R5 … R45) round skoru dağılımı. Boss hasadı hedeflerinin ilk değerleri buradan gerekçelendirilir.
    // Koşul B (elenme kapalı), boss kuralı ve ödül yok: referans oyunun o round'da tek başına ürettiği skor. Simülatör ölçümüdür.
    public static void RunBossRoundScoreBatch()
    {
        var text = new StringBuilder();
        try
        {
            Load();
            const int seeds = 100;
            var rules = Reference50Rules(true);
            string Q(IEnumerable<double> v) { var l = v.ToList(); return $"{P(l, .1):0}/{P(l, .5):0}/{P(l, .9):0}"; }
            text.AppendLine($"=== Boss round'larında round skoru (Run50_Referans kuralı, koşul B, {seeds} seed, P10/P50/P90; boss kuralı yok) ===");
            // Boss hedefleri profil tablosundan okunur (ilk test değerleri); geçme oranı boss kuralı ve boss ödülü OLMADAN hesaplanır.
            var bossProfile = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_BossPrototip.asset");
            var targets = bossProfile != null ? bossProfile.bossTargets : new List<long>();
            foreach (var policy in Policies)
            {
                var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, rules, Reference50SeedBase + s)).ToList();
                text.AppendLine($"-- {policy.Name} --");
                int alive = seeds;
                for (int r = 5; r <= 45; r += 5)
                {
                    string pass = "";
                    if (r / 5 - 1 < targets.Count)
                    {
                        long target = targets[r / 5 - 1];
                        int ok = runs.Count(x => x.Rounds[r - 1].Score >= target);
                        // Art arda: o boss'a kadar bütün hedefleri geçen seed sayısı (boss'ta kalan run biter).
                        alive = runs.Count(x => Enumerable.Range(1, r / 5).All(k => x.Rounds[k * 5 - 1].Score >= targets[k - 1]));
                        pass = $" · hedef {target}: geçen {ok}/{seeds}, buraya kadar hepsini geçen {alive}/{seeds} · P50/hedef ×{P(runs.Select(x => x.Rounds[r - 1].Score).ToList(), .5) / target:0.0}";
                    }
                    text.AppendLine($"   R{r}: round skoru {Q(runs.Select(x => x.Rounds[r - 1].Score))} · en düşük {runs.Min(x => x.Rounds[r - 1].Score):0} · segment skoru {Q(runs.Select(x => x.SegmentScores[r / 5 - 1]))}{pass}");
                }
            }
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimBossRoundScore.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // Sahne ölçümü için temsilî durum: politikanın seed'leri arasında r15 skoru medyana en yakın run'ın round başı durumları.
    public static List<Snapshot> RepresentativeStates(Policy policy, int[] rounds, int seeds, out int seed)
    {
        Load();
        var runs = Enumerable.Range(0, seeds).Select(s => Run(policy, ExperimentRules(0, rounds), 9000 + s)).Where(r => r.Snapshots.Count == rounds.Length).ToList();
        double Score15(RunResult r) => r.Rounds.Count >= 15 ? r.Rounds[14].Score : 0;
        double median = P(runs.Select(Score15), .5);
        var pick = runs.OrderBy(r => Math.Abs(Score15(r) - median)).First();
        seed = pick.Snapshots[0].Seed;
        return pick.Snapshots;
    }

    // Bölüm 1 prototipi (2026-09-30): 10 round, kota tablosu 40 / 200, 2. segment Don Cephesi; aynı seed'lerle olay açık/kapalı.
    // Simülatörde olay, şeritteki hücrelerin üretim tavanını düşürür (yaklaşım: saksı hücresi = üretim noktası).
    public static void RunPrototypeBatch()
    {
        var text = new StringBuilder();
        try
        {
            Rules Proto(string name, int frost) => new Rules { Name = name, MaxRounds = 10, UpgradeWhenFull = true, DurationCap = 60, QuotaSegment = 5, QuotaTargets = new long[] { 40, 200 }, FrostSegment = frost };
            const int seeds = 30;
            string Q(IEnumerable<double> v) { var l = v.ToList(); return l.Count == 0 ? "-" : $"{P(l, .1):0}/{P(l, .5):0}/{P(l, .9):0}"; }
            foreach (var policy in Policies)
            {
                var on = Enumerable.Range(0, seeds).Select(s => Run(policy, Proto("Don açık", 2), 3000 + s)).ToList();
                var off = Enumerable.Range(0, seeds).Select(s => Run(policy, Proto("Don kapalı", 0), 3000 + s)).ToList();
                int Pass1(List<RunResult> r) => r.Count(x => x.SegmentScores.Count >= 1 && x.QuotaMargins[0] >= 1);
                int Win(List<RunResult> r) => r.Count(x => x.EndedAt == 0);
                text.AppendLine($"-- {policy.Name} ({seeds} seed) --");
                text.AppendLine($"   1. segment (r1–5, kota 40): geçen {Pass1(on)}/{seeds} · skor P10/P50/P90 {Q(on.Select(x => x.SegmentScores[0]))}");
                text.AppendLine($"   2. segment (r6–10, kota 200) Don açık: zafer {Win(on)}/{seeds} · skor {Q(on.Where(x => x.SegmentScores.Count >= 2).Select(x => x.SegmentScores[1]))}");
                text.AppendLine($"   2. segment Don kapalı (aynı seed): zafer {Win(off)}/{seeds} · skor {Q(off.Where(x => x.SegmentScores.Count >= 2).Select(x => x.SegmentScores[1]))}");
                var ratio = on.Zip(off, (a, b) => a.SegmentScores.Count >= 2 && b.SegmentScores.Count >= 2 && b.SegmentScores[1] > 0 ? a.SegmentScores[1] / b.SegmentScores[1] : double.NaN).Where(x => !double.IsNaN(x)).ToList();
                text.AppendLine($"   Don açık / kapalı 2. segment skoru (seed başına) P10/P50/P90: {(ratio.Count == 0 ? "-" : $"{P(ratio, .1):0.00}/{P(ratio, .5):0.00}/{P(ratio, .9):0.00}")} · hasat oranı r8 açık/kapalı {P(on.Where(x => x.Rounds.Count >= 8).Select(x => x.Rounds[7].HarvestRatio * 100), .5):0}%/{P(off.Where(x => x.Rounds.Count >= 8).Select(x => x.Rounds[7].HarvestRatio * 100), .5):0}%");
            }
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimPrototype.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // Kota eğrisi karşılaştırması: hangi profil, hangi round'da elenir; geçenler kotaya ne kadar yaklaşır.
    public static void RunQuotaBatch()
    {
        var text = new StringBuilder();
        try
        {
            var sets = new List<Rules>();
            foreach (var (start, growth) in new[] { (30f, 1.35f), (15f, 1.4f), (10f, 1.45f) })
                sets.Add(new Rules { Name = $"Kota {start} · ×{growth.ToString(CultureInfo.InvariantCulture)}", UpgradeWhenFull = true, DurationCap = 60, QuotaSegment = 5, QuotaStart = start, QuotaGrowth = growth });
            foreach (var rules in sets)
            {
                text.AppendLine($"=== {rules.Name} ===");
                foreach (var policy in Policies)
                {
                    var runs = Enumerable.Range(0, 24).Select(s => Run(policy, rules, 1000 + s)).ToList();
                    var ended = runs.Where(r => r.EndedAt > 0).ToList();
                    string Q(IEnumerable<double> v) { var l = v.ToList(); return l.Count == 0 ? "-" : $"{P(l, .1):0.##}/{P(l, .5):0.##}/{P(l, .9):0.##}"; }
                    text.AppendLine($"-- {policy.Name}: biten {ended.Count}/{runs.Count} · biten round P10/P50/P90 {Q(ended.Select(r => (double)r.EndedAt))} · bitiş dk {Q(ended.Select(r => r.Rounds[^1].Minutes))} · bitişte ağaç% {Q(ended.Select(r => r.Rounds[^1].Tree * 100))}");
                    if (rules.QuotaSegment > 0)
                    {
                        text.AppendLine($"   en dar geçiş (skor/kota, geçilen segmentler): {Q(runs.Select(r => r.QuotaMargins.Where(m => m >= 1).DefaultIfEmpty(double.NaN).Min()).Where(m => !double.IsNaN(m)))}");
                        text.AppendLine($"   ilk 8 segment (r1-40) en dar geçiş: {Q(runs.Select(r => r.QuotaMargins.Take(8).Min()))} · r40'a kadar elenen {runs.Count(r => r.EndedAt > 0 && r.EndedAt <= 40)} · r60'a kadar {runs.Count(r => r.EndedAt > 0 && r.EndedAt <= 60)}");
                    }
                }
                if (rules.QuotaSegment > 0)
                    text.AppendLine("   kotalar: " + string.Join(" ", Enumerable.Range(1, 26).Select(s => $"r{s * 5}:{HarvestQuota.Format(HarvestQuota.Target(s, rules.QuotaStart, rules.QuotaGrowth))}")));
            }
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimQuota.txt", text.ToString());
        EditorApplication.Exit(0);
    }

    // Üretim tabanı dönüşümü (2026-09-29): tabanı aşan hız nadirliğe mi, boşa mı?
    public static void RunOverflowBatch()
    {
        var text = new StringBuilder(); var csv = new StringBuilder("rules,policy,round,alive,level,tree,harvestRatio,income,xp,hpCommon,planters,tiles,unlocked,resonances,minutes,duration,dead\n");
        try
        {
            Rules Base(string name) => new Rules { Name = name, UpgradeWhenFull = true, FailThreshold = .3f, DurationCap = 60 };
            text.Append(Report(Base("Tarla 3 round"), 12, csv));
            text.Append(Report(Set(Base("Tarla 4 round"), r => r.FailRounds = 4), 12, csv));
            text.Append(Report(Set(Base("Tarla 5 round"), r => r.FailRounds = 5), 12, csv));
            text.Append(Report(Set(Base("Tarla kapalı"), r => r.FailThreshold = 0), 12, csv));
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSimOverflow.txt", text.ToString());
        File.WriteAllText("Logs/RunSimOverflow.csv", csv.ToString());
        EditorApplication.Exit(0);
    }

    static string XpReport(Rules rules)
    {
        var text = new StringBuilder($"=== {rules.Name} ===\n");
        foreach (var policy in Policies)
        {
            var runs = Enumerable.Range(0, 12).Select(s => Run(policy, rules, 1000 + s)).ToList();
            string Q(Func<RunResult, double> f) { var v = runs.Select(f).Where(x => !double.IsNaN(x)).ToList(); return v.Count == 0 ? "-" : $"{P(v, .1):0}/{P(v, .5):0}/{P(v, .9):0}" + (v.Count < runs.Count ? $"({v.Count})" : ""); }
            double At(RunResult r, int round, Func<RoundLog, double> f) => round <= r.Rounds.Count ? f(r.Rounds[round - 1]) : double.NaN;
            int Drought(RunResult r) { int best = 0, cur = 0; foreach (var l in r.Rounds.Where(l => l.Round >= 40)) { cur = l.Cards == 0 ? cur + 1 : 0; best = Math.Max(best, cur); } return best; }
            text.AppendLine($"-- {policy.Name}: level r20 {Q(r => At(r, 20, l => l.Level))} · r40 {Q(r => At(r, 40, l => l.Level))} · r65 {Q(r => At(r, 65, l => l.Level))} · r90 {Q(r => At(r, 90, l => l.Level))} · r110 {Q(r => At(r, 110, l => l.Level))} · r130 {Q(r => At(r, 130, l => l.Level))}");
            text.AppendLine($"   XP/round r65 {Q(r => At(r, 65, l => l.Xp))} · r110 {Q(r => At(r, 110, l => l.Xp))} · ekran/round en çok {Q(r => r.Rounds.Max(l => l.Screens))} · 40 sonrası en uzun kartsız seri {Q(r => Drought(r))} · kart {Q(r => r.Rounds.Sum(l => l.Cards))} · Legendary üstü taşan {Q(r => r.Rounds.Sum(l => l.Overflow))}");
            text.AppendLine($"   son: tile {Q(r => r.Rounds[^1].Tiles)} · max tile {Q(r => r.Rounds[^1].MaxedTiles)} · yükseltme {Q(r => r.Rounds.Sum(l => l.Upgrades))} · atla {Q(r => r.Rounds.Sum(l => l.Skips))} · boş {Q(r => r.Rounds.Sum(l => l.Dead))} · round {Q(r => r.Rounds.Count)} · dk {Q(r => r.Rounds[^1].Minutes)}");
        }
        return text.ToString();
    }

    public static void Calibrate()
    {
        var sb = new StringBuilder("alive,accuracy,hesitation,skillByTarget,tree100_p50,tree_at_75,tree_at_110,level_65,min_110" + "\n");
        try
        {
            foreach (double alive in new[] { .25, .35, .5 })
            foreach (float acc in new[] { 0f, .25f, .5f, .75f, 1f })
            foreach (float hes in new[] { 0f, .6f })
            foreach (bool byTarget in new[] { true, false })
            {
                if (byTarget && hes > 0) continue;
                var pol = new Policy { Name = "k", Accuracy = acc, Hesitation = hes, SkillByTarget = byTarget, ResonanceAware = byTarget, ReplaceSmall = byTarget, SkipWhenFull = byTarget };
                var rules = new Rules { Alive = alive };
                var runs = Enumerable.Range(0, 6).Select(i => Run(pol, rules, 500 + i)).ToList();
                double Med(Func<RunResult, double> f) => P(runs.Select(f), .5);
                double full = Med(r => r.Rounds.FirstOrDefault(l => l.Tree >= .999)?.Round ?? 999);
                sb.AppendLine(string.Join(",", alive.ToString(CultureInfo.InvariantCulture), acc.ToString(CultureInfo.InvariantCulture), hes.ToString(CultureInfo.InvariantCulture), byTarget,
                    full, Med(r => r.Rounds[74].Tree * 100).ToString("0"), Med(r => r.Rounds[109].Tree * 100).ToString("0"), Med(r => r.Rounds[64].Level), Med(r => r.Rounds[109].Minutes).ToString("0")));
            }
        }
        catch (Exception ex) { sb.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/RunSimCalib.csv", sb.ToString());
        EditorApplication.Exit(0);
    }

    public static void RunBatch()
    {
        var text = new StringBuilder(); var csv = new StringBuilder("rules,policy,round,alive,level,tree,harvestRatio,income,xp,hpCommon,planters,tiles,unlocked,resonances,minutes,duration,dead\n");
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var draftHp = new List<Vector2> { new(1, 5), new(15, 14.7f), new(45, 84), new(65, 473), new(80, 736), new(130, 1500) };
            var sets = new[]
            {
                new Rules { Name = "1 Şimdiki oyun" },
                new Rules { Name = "2 Seçilenler, 130 round", UpgradeWhenFull = true, FailThreshold = .3f, DurationCap = 60 },
                new Rules { Name = "3 Seçilenler, 100 round", UpgradeWhenFull = true, FailThreshold = .3f, DurationCap = 60, MaxRounds = 100 },
                new Rules { Name = "4 Seçilenler, 90 round", UpgradeWhenFull = true, FailThreshold = .3f, DurationCap = 60, MaxRounds = 90 },
            };
            foreach (var r in sets) text.Append(Report(r, 12, csv));
            text.AppendLine($"süre: {watch.Elapsed.TotalSeconds:0.0} sn");
        }
        catch (Exception ex) { text.AppendLine("FAIL: " + ex); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RunSim.txt", text.ToString());
        File.WriteAllText("Logs/RunSim.csv", csv.ToString());
        EditorApplication.Exit(0);
    }
}
