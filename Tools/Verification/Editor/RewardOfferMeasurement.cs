using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch (izole kopya): boss ödülü teklifleri için hafif ölçüm. Run oynanmaz: tarla düzeni sabit kurulur, teklif oyunun kendi
// BuildOffer yoluyla çok sayıda seed için üretilir. Kazanma oranı ya da build gücü ölçmez.
//  RunGoldenDump  : eski havuzların (Kırılma V1, Denge V1, Boss Prototip) sabit durumlardaki teklif dizisini kaydeder.
//                   Bölüm 3.7.3 kod değişikliğinden ÖNCE çalıştırıldı; çıktısı BossOfferGolden.cs olarak saklanır.
//  RunDistribution: Bölüm 3.7.3 aşamalı havuzun teklif dağılımı (Logs/RewardOfferDistribution.md).
[InitializeOnLoad]
public static class RewardOfferMeasurement
{
    const string Key = "RewardOfferMeasurement";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";
    public const int GoldenSeeds = 40;
    public static readonly int[] GoldenRounds = { 5, 10, 15, 20, 25, 30, 35, 40, 45 };
    static double nextAt;

    static RewardOfferMeasurement() { EditorApplication.update += Tick; }

    public static void RunGoldenDump() => Begin("golden", "Run50_KirilmaV1");
    public static void RunDistribution() => Begin("distribution", "Run50_OdulAsamalariV1");

    static void Begin(string mode, string profile)
    {
        SessionState.SetString(Key, mode);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: ölçülen profil seçilir (gerçek projedeki seçim değişmez).
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + profile + ".asset");
        if (selection.active == null) throw new Exception("profile not found: " + profile);
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        string mode = SessionState.GetString(Key, "");
        if (mode == "" || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (nextAt == 0) { nextAt = now + 2; return; }
        if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        if (RoundManager.Instance == null || GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.RunSetup) { nextAt = now + .1; return; }
        Exception error = null;
        try
        {
            Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
            if (mode == "golden") File.WriteAllText("Logs/BossOfferGolden.cs", GoldenSource(GoldenLines()), new UTF8Encoding(false));
            else Distribution();
        }
        catch (Exception ex) { error = ex; Debug.LogException(ex); }
        SessionState.SetString(Key, "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RewardOfferMeasurement.txt", error == null ? "DONE: " + mode : "FAIL: " + error);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    static RunProfileSO Profile(string name) => AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + name + ".asset");
    static BossRewardManager Boss => BossRewardManager.Instance;

    // ---------------------------------------------------------------- eski havuzların teklif dizisi (karşılaştırma kaydı)
    public static readonly (string name, RewardOfferLab.Layout layout, float occupancy)[] GoldenLayouts =
    {
        ("bos", RewardOfferLab.Layout.Empty, 1f),
        ("patlama", RewardOfferLab.Layout.Explosive, .50f),
        ("cok", RewardOfferLab.Layout.Multi, .75f),
    };

    // "birikmiş" durum: bazı ödüller alınmış (biri sınırında); sıra ve adetler sabit.
    public static void GrantGoldenStacks()
    {
        var k = Profile("Run50_KirilmaV1").bossRewards;
        BossRewardSO R(string id) => k.rewards.Concat(k.breakthroughs).First(r => r.id == id);
        for (int i = 0; i < 3; i++) RewardOfferLab.Grant(R("keskin_bicak"));
        for (int i = 0; i < 2; i++) RewardOfferLab.Grant(R("hizli_bilek"));
        RewardOfferLab.Grant(R("nadir_tohum"));
        RewardOfferLab.Grant(R("hasat_ritmi"));
    }

    public static List<string> GoldenLines()
    {
        var pools = new (string name, BossRewardPoolSO pool)[]
        {
            ("KirilmaV1", Profile("Run50_KirilmaV1").bossRewards), ("DengeV1", Profile("Run50_DengeV1").bossRewards), ("BossPrototip", Profile("Run50_BossPrototip").bossRewards),
        };
        var lines = new List<string>();
        foreach (var (layoutName, layout, occupancy) in GoldenLayouts)
            foreach (string state in new[] { "temiz", "birikmis" })
            {
                Boss.ClearAll();
                RewardOfferLab.Build(layout);
                RewardOfferLab.SetOccupancy(occupancy);
                if (state == "birikmis") GrantGoldenStacks();
                foreach (var (poolName, pool) in pools)
                    foreach (int round in GoldenRounds)
                        lines.Add($"{poolName}|{layoutName}|{state}|R{round}|" +
                                  string.Join(",", Enumerable.Range(1, GoldenSeeds).Select(seed => RewardOfferLab.Encode(pool, RewardOfferLab.Offer(pool, round, seed)))));
            }
        Boss.ClearAll(); RewardOfferLab.ClearField();
        return lines;
    }

    static string GoldenSource(List<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ÜRETİLMİŞ DOSYA — elle düzenleme. Kaynak: RewardOfferMeasurement.RunGoldenDump, Bölüm 3.7.3'ün teklif kodu değişmeden önce");
        sb.AppendLine("// (2026-10-02) izole kopyada çalıştırıldı. Eski havuzların sabit tarla düzeni, sabit alınmış ödül durumu, boss round'u ve");
        sb.AppendLine("// seed için ürettiği teklifler. Satır: havuz|düzen|durum|round|seed 1…" + GoldenSeeds + " teklifleri (harf = havuzdaki sıra; '-' boş teklif).");
        sb.AppendLine("public static class BossOfferGolden");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly string[] Lines =");
        sb.AppendLine("    {");
        foreach (string line in lines) sb.AppendLine("        \"" + line + "\",");
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Bölüm 3.7.3 teklif dağılımı
    // Sabit tarla düzenlerinde, çok sayıda seed ile aşamalı havuzun teklifleri. İki bölüm:
    //  A) Sabit durum: hiç ödül alınmamış; her boss tarihi ayrı ayrı. "Kural ne üretiyor" sorusu.
    //  B) Birikimli dizi: 15 boss sırayla; her teklifte rastgele bir kart alınır (ayrı zar), stack'ler birikir. "Aday tükeniyor mu" sorusu.
    // İkisi de "mevcut aşamaya slot ayır" açık ve kapalıyken çalışır (kapalı kol yalnız karşılaştırma içindir; oyunda açık).
    const int SeedsA = 400, SeedsB = 200;
    static readonly (string name, RewardOfferLab.Layout layout, float occupancy)[] Conditions =
    {
        ("Doğrudan vuruş", RewardOfferLab.Layout.Direct, .75f),
        ("Patlamalı", RewardOfferLab.Layout.Explosive, .75f),
        ("Elektrikli", RewardOfferLab.Layout.Electric, .75f),
        ("Çok davranışlı", RewardOfferLab.Layout.Multi, .75f),
        ("Doğrudan vuruş · tarla boşalıyor (doluluk 0,50)", RewardOfferLab.Layout.Direct, .50f),
    };

    static string Pct(double part, double whole) => whole <= 0 ? "-" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture);
    static string Avg(double sum, double n) => n <= 0 ? "-" : (sum / n).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    static void Distribution()
    {
        var rm = RoundManager.Instance;
        var pool = rm.Profile.bossRewards;
        if (pool == null || !pool.IsStaged) throw new Exception("profile has no staged reward pool");
        var noReserve = Object.Instantiate(pool); noReserve.reserveCurrentStageSlot = false;
        var all = RewardOfferLab.All(pool);
        var dates = rm.Profile.bossCalendar.Select(d => d.round).ToList();
        int stages = pool.stages.Count;
        var md = new StringBuilder(); var csv = new StringBuilder("part,condition,reserve,stageGroup,round,metric,reward,value\n");
        md.AppendLine("# Teklif dağılımı — " + rm.Profile.displayName);
        md.AppendLine();
        md.AppendLine($"Run oynanmadı. Tarla düzeni sabit; teklif oyunun kendi teklif koduyla üretildi. A: {SeedsA} seed × 15 boss tarihi, hiç ödül alınmamış. " +
                      $"B: {SeedsB} seed, 15 boss sırayla, her teklifte rastgele bir kart alınır. Kazanma oranı ya da build gücü ölçmez.");
        md.AppendLine();

        foreach (var (conditionName, layout, occupancy) in Conditions)
        {
            Boss.ClearAll();
            RewardOfferLab.Build(layout);
            RewardOfferLab.SetOccupancy(occupancy);
            md.AppendLine("## " + conditionName);
            md.AppendLine();
            var eligible = all.Where(Boss.IsEligible).ToList();
            md.AppendLine("Düzen: " + RewardOfferLab.Name(layout) + $" · son round doluluğu {occupancy.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')}. " +
                          "Hiç ödül alınmamışken koşulu sağlamayan ödüller: " +
                          (eligible.Count == all.Count ? "yok" : string.Join(", ", all.Where(r => !eligible.Contains(r)).Select(r => r.displayName))) + ".");
            md.AppendLine();

            // ---- A) sabit durum
            md.AppendLine("**A) Sabit durum (hiç ödül alınmamış)**");
            md.AppendLine();
            md.AppendLine("| Boss'lar | Slot ayırma | Ort. kart | Erken | Orta | Güçlü | Mevcut aşamadan ≥ 1 | Mevcut aşamadan 0 / 1 / 2 / 3 kart | 3'ten az kart | Boş teklif |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            var frequency = new Dictionary<(int group, bool reserve, BossRewardSO reward), int>();
            var offersIn = new Dictionary<(int group, bool reserve), int>();
            for (int group = 0; group < stages; group++)
                foreach (bool reserve in new[] { true, false })
                {
                    var groupDates = dates.Where(d => pool.StageIndexAt(d) == group).ToList();
                    double offers = 0, cards = 0, withCurrent = 0, shortOffers = 0, empty = 0;
                    var byStage = new double[stages]; var currentCount = new double[4];
                    foreach (int round in groupDates)
                        for (int seed = 1; seed <= SeedsA; seed++)
                        {
                            var offer = RewardOfferLab.Offer(reserve ? pool : noReserve, round, seed);
                            offers++; cards += offer.Count;
                            int current = 0;
                            foreach (var reward in offer)
                            {
                                int s = pool.StageIndexOf(reward); byStage[s]++;
                                if (s == group) current++;
                                frequency.TryGetValue((group, reserve, reward), out int n); frequency[(group, reserve, reward)] = n + 1;
                            }
                            currentCount[Math.Min(3, current)]++;
                            if (current > 0) withCurrent++;
                            if (offer.Count < pool.choices) shortOffers++;
                            if (offer.Count == 0) empty++;
                        }
                    offersIn[(group, reserve)] = (int)offers;
                    md.AppendLine($"| {pool.stages[group].displayName} ({string.Join(", ", groupDates.Select(d => "R" + d))}) | {(reserve ? "açık" : "kapalı")} | {Avg(cards, offers)} | " +
                                  $"{Avg(byStage[0], offers)} | {(stages > 1 ? Avg(byStage[1], offers) : "-")} | {(stages > 2 ? Avg(byStage[2], offers) : "-")} | %{Pct(withCurrent, offers)} | " +
                                  $"%{Pct(currentCount[0], offers)} / %{Pct(currentCount[1], offers)} / %{Pct(currentCount[2], offers)} / %{Pct(currentCount[3], offers)} | " +
                                  $"%{Pct(shortOffers, offers)} | %{Pct(empty, offers)} |");
                    foreach (string metric in new[] { "offers", "cards", "withCurrent", "short", "empty" })
                        csv.Append($"A,{conditionName},{(reserve ? 1 : 0)},{pool.stages[group].id},,{metric},,{(metric == "offers" ? offers : metric == "cards" ? cards : metric == "withCurrent" ? withCurrent : metric == "short" ? shortOffers : empty)}\n");
                }
            md.AppendLine();
            md.AppendLine("Ödülün görülme sıklığı (o aşamadaki tekliflerin yüzde kaçında kart olarak çıktı; slot ayırma açık → kapalı):");
            md.AppendLine();
            md.AppendLine("| Ödül | Sınıf | " + string.Join(" | ", pool.stages.Select(s => s.displayName + " boss'ları")) + " |");
            md.AppendLine("|---|---|" + string.Join("", pool.stages.Select(_ => "---|")));
            foreach (var reward in all)
            {
                var cells = new List<string>();
                for (int group = 0; group < stages; group++)
                {
                    frequency.TryGetValue((group, true, reward), out int on); frequency.TryGetValue((group, false, reward), out int off);
                    cells.Add(pool.StageIndexOf(reward) > group ? "—" : $"%{Pct(on, offersIn[(group, true)])} → %{Pct(off, offersIn[(group, false)])}");
                    csv.Append($"A,{conditionName},1,{pool.stages[group].id},,frequency,{reward.id},{on}\n");
                    csv.Append($"A,{conditionName},0,{pool.stages[group].id},,frequency,{reward.id},{off}\n");
                }
                md.AppendLine($"| {reward.displayName} | {pool.stages[pool.StageIndexOf(reward)].displayName} | {string.Join(" | ", cells)} |");
            }
            md.AppendLine();

            // ---- B) birikimli dizi
            md.AppendLine("**B) Birikimli dizi (15 boss sırayla, her teklifte rastgele bir kart alınır)**");
            md.AppendLine();
            md.AppendLine("| Boss | Aşama | Ort. kart (açık) | Erken / Orta / Güçlü (açık) | Mevcut aşamadan ≥ 1 (açık → kapalı) | 3'ten az kart (açık → kapalı) | Boş teklif (açık → kapalı) |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            var seq = new Dictionary<(bool reserve, int round), double[]>();   // offers, cards, e, o, g, withCurrent, short, empty
            var takenTotal = new Dictionary<(bool reserve, BossRewardSO reward), int>();
            foreach (bool reserve in new[] { true, false })
                for (int seed = 1; seed <= SeedsB; seed++)
                {
                    Boss.ClearAll();
                    RewardOfferLab.SetOccupancy(occupancy);
                    var pickRng = new System.Random(seed * 7919 + 17);
                    foreach (int round in dates)
                    {
                        var offer = RewardOfferLab.Offer(reserve ? pool : noReserve, round, seed);
                        if (!seq.TryGetValue((reserve, round), out var t)) seq[(reserve, round)] = t = new double[8];
                        int stage = pool.StageIndexAt(round);
                        t[0]++; t[1] += offer.Count;
                        foreach (var reward in offer) t[2 + pool.StageIndexOf(reward)]++;
                        if (offer.Any(r => pool.StageIndexOf(r) == stage)) t[5]++;
                        if (offer.Count < pool.choices) t[6]++;
                        if (offer.Count == 0) { t[7]++; continue; }
                        var pick = offer[pickRng.Next(offer.Count)];
                        if (!RewardOfferLab.Grant(pick)) throw new Exception("offered reward could not be taken: " + pick.id);
                        takenTotal.TryGetValue((reserve, pick), out int n); takenTotal[(reserve, pick)] = n + 1;
                    }
                }
            foreach (int round in dates)
            {
                var on = seq[(true, round)]; var off = seq[(false, round)];
                md.AppendLine($"| R{round} | {pool.StageAt(round).displayName} | {Avg(on[1], on[0])} | {Avg(on[2], on[0])} / {Avg(on[3], on[0])} / {Avg(on[4], on[0])} | " +
                              $"%{Pct(on[5], on[0])} → %{Pct(off[5], off[0])} | %{Pct(on[6], on[0])} → %{Pct(off[6], off[0])} | %{Pct(on[7], on[0])} → %{Pct(off[7], off[0])} |");
                foreach (bool reserve in new[] { true, false })
                {
                    var t = seq[(reserve, round)];
                    csv.Append($"B,{conditionName},{(reserve ? 1 : 0)},{pool.StageAt(round).id},{round},offers,,{t[0]}\n");
                    csv.Append($"B,{conditionName},{(reserve ? 1 : 0)},{pool.StageAt(round).id},{round},cards,,{t[1]}\n");
                    csv.Append($"B,{conditionName},{(reserve ? 1 : 0)},{pool.StageAt(round).id},{round},withCurrent,,{t[5]}\n");
                    csv.Append($"B,{conditionName},{(reserve ? 1 : 0)},{pool.StageAt(round).id},{round},short,,{t[6]}\n");
                    csv.Append($"B,{conditionName},{(reserve ? 1 : 0)},{pool.StageAt(round).id},{round},empty,,{t[7]}\n");
                }
            }
            md.AppendLine();
            md.AppendLine("Run başına alınan ödül (ortalama adet; slot ayırma açık → kapalı): " + string.Join(" · ", all.Select(r =>
            {
                takenTotal.TryGetValue((true, r), out int on); takenTotal.TryGetValue((false, r), out int off);
                csv.Append($"B,{conditionName},1,,,taken,{r.id},{on}\n"); csv.Append($"B,{conditionName},0,,,taken,{r.id},{off}\n");
                return $"{r.displayName} {Avg(on, SeedsB)} → {Avg(off, SeedsB)}";
            })));
            md.AppendLine();
        }
        Boss.ClearAll(); RewardOfferLab.ClearField();
        md.AppendLine($"Teklif üretimi oyunun rastgelelik akışını (UnityEngine.Random) tüketmedi: {RewardOfferLab.RandomChecks} teklifin her birinde önce / sonra karşılaştırıldı.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RewardOfferDistribution.md", md.ToString(), new UTF8Encoding(false));
        File.WriteAllText("Logs/RewardOfferDistribution.csv", csv.ToString(), new UTF8Encoding(false));
    }
}
