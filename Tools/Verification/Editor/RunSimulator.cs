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
    }

    sealed class TileState { public TileModifierSO So; public List<StatModifier> Mods; public int Stars; }
    sealed class PlanterState { public PlanterSO So; public List<Vector2Int> Cells = new(); public int Spawners; }

    public sealed class RoundLog
    {
        public int Round, Level, Cards, Dead, Skips, Upgrades, Tiles, Unlocked, Planters, Resonances, TierCount, Screens, Overflow, MaxedTiles;
        public double Gold, Iron, Stone, Xp, Score, Harvest, Ceiling, Duration, HpCommon, Minutes, Tree, HarvestRatio, BehaviorHarvest, Income;
    }

    public sealed class RunResult
    {
        public readonly List<RoundLog> Rounds = new();
        public readonly Dictionary<string, int> NodeDone = new();
        public int EndedAt;
        public readonly List<double> QuotaMargins = new(); // segment skoru / kota
        public readonly List<double> SegmentScores = new();
        public readonly Dictionary<string, int> Blocked = new() { ["Gold"] = 0, ["Iron"] = 0, ["Stone"] = 0 };
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
        double adj = key / 100.0 + 1; const int steps = 16; double sum = 0; int best = 0;
        for (int a = 0; a < steps; a++) for (int c = 0; c < steps; c++)
        {
            double ox = (a + .5) / steps * 2 - 1, oz = (c + .5) / steps * 2 - 1; int n = 0;
            for (int i = -6; i <= 6; i++) for (int j = -6; j <= 6; j++) if (Math.Sqrt((i * 2 - ox) * (i * 2 - ox) + (j * 2 - oz) * (j * 2 - oz)) <= adj + 1e-9) n++;
            sum += n; best = Math.Max(best, n);
        }
        return reachCache[key] = (sum / (steps * steps), best);
    }

    static double Weighted(ResourceType type, int cost) => cost * (type == ResourceType.Gold ? 1 : type == ResourceType.Iron ? 7 : 14);

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
                    int lv = levels[n]; if (lv >= n.tiers.Count) continue;
                    if (lv == 0 && n.prerequisites.Any(r => r.node == null || levels[r.node] < r.level)) continue;
                    var tier = n.tiers[lv];
                    if (bank[tier.costType] < tier.cost) { result.Blocked[tier.costType.ToString()]++; continue; }
                    if (policy.Hesitation > 0 && rng.NextDouble() < policy.Hesitation) continue;
                    double key = policy.SkillByTarget ? (n.targetRounds.x + n.targetRounds.y) * 1e6 + Weighted(tier.costType, tier.cost) : Weighted(tier.costType, tier.cost);
                    if (key < pickKey) { pickKey = key; pick = n; }
                }
                if (pick == null) break;
                var t2 = pick.tiers[levels[pick]];
                bank[t2.costType] -= t2.cost; levels[pick]++; tierCount++;
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
            var log = new RoundLog { Round = round };
            int currentSegment = rules.QuotaSegment > 0 ? (round - 1) / rules.QuotaSegment + 1 : 0;
            bool frostActive = rules.QuotaSegment > 0 && (currentSegment == rules.FrostSegment || (rules.FrostSegments != null && Array.IndexOf(rules.FrostSegments, currentSegment) >= 0));
            bool spec = rules.SpecFromRound > 0 && round >= rules.SpecFromRound;
            float directMult = spec ? rules.DirectMult : 1f, behaviorMult = spec ? rules.BehaviorMult : 1f;
            float duration = Mathf.Clamp(All(StatType.RoundDuration, StatTarget.All), 30, 90);
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

            double sumCeil = 0, sumHitsCeil = 0; var per = new List<(double ceil, double hits, double g, double i, double s, double x, double sc, double extra)>();
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
                double eg = 0, ei = 0, es = 0, ex = 0, esc = 0, hits = 0;
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
                double extra = alive * (
                    ch(StatType.ExplosionChance) * nExp * avgKill(dmgRaw * ResonanceManager.BehaviorMultiplier(active, DamageType.Explosion)) +
                    ch(StatType.TornadoChance) * 8 * density * avgKill(Math.Max(1, Math.Round(dmgRaw * .5)) * ResonanceManager.BehaviorMultiplier(active, DamageType.Tornado)) +
                    ch(StatType.BoomerangChance) * 2.5 * density * avgKill(2 * Math.Max(1, Math.Round(dmgRaw * .65)) * ResonanceManager.BehaviorMultiplier(active, DamageType.Boomerang)) +
                    ch(StatType.ElectricChance) * nEl * avgKill(dmgRaw * ResonanceManager.BehaviorMultiplier(active, DamageType.Electric)));
                per.Add((ceil, hits, eg, ei, es, ex, esc, extra)); sumCeil += ceil; sumHitsCeil += ceil * hits;
            }
            double harvests = 0;
            if (sumCeil > 0)
            {
                double avgHits = sumHitsCeil / sumCeil, combat = duration / attack * targets / avgHits;
                harvests = Math.Min(sumCeil, combat);
                double direct = harvests, bonus = 0;
                foreach (var q in per) bonus += direct * q.ceil / sumCeil * q.extra;
                bonus = Math.Min(bonus, Math.Max(0, sumCeil - direct));
                log.BehaviorHarvest = bonus; harvests = direct + bonus;
                foreach (var q in per)
                {
                    double h = harvests * q.ceil / sumCeil;
                    bank[ResourceType.Gold] += h * q.g; bank[ResourceType.Iron] += h * q.i; bank[ResourceType.Stone] += h * q.s;
                    log.Gold += h * q.g; log.Iron += h * q.i; log.Stone += h * q.s; log.Xp += h * q.x; log.Score += h * q.sc;
                }
            }
            log.Harvest = harvests; log.Ceiling = sumCeil; log.HarvestRatio = sumCeil > 0 ? harvests / sumCeil : 0;
            log.Duration = duration; log.Resonances = resonances;
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
                if (segScore < quota) { result.EndedAt = round; break; }
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
