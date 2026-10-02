using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.5 GERÇEK RUN ÖLÇÜMÜ (işlev testi değildir; "geçti" demez, sayı verir).
// Bot bir run'ı R1'den oynar: round atlamaz, bütçe vermez, kart / ödül / tile düzeni hediye etmez.
//   - Saksı: gerçek mağaza yolu (kaynak harcanır, PlacementManager yerleştirir). Skill: SkillTreeManager.TryUpgrade.
//   - Kart: kart ekranındaki üç tekliften biri (CardSelectionUI.OnCardSelected). Boss ödülü: teklif edilenlerden biri.
//   - Hasat: oyuncunun kendi vuruş kodu (PlayerController.AttackInRadius), saldırı aralığı stat'ından.
// Run biterse (kota ya da boss hasadı) biter; elenen run sonuçlardan çıkarılmaz.
// Bot insan değildir: nişanı her vuruşta en iyi konumu seçer ("Yeni" politikası hariç), menüde zaman harcamaz.
// Çıktı: Logs/BalanceRuns_<set>.csv (round satırları), _nodes.csv (alımlar), _rewards.csv (ödül teklifleri), .txt (özet).
// Bölüm 3.6: üç build yolu politikası, "ödülsüz eş" (kırılma ödülleri havuzdan çıkarılmış aynı run), level başına seçim sayısı
// karşılaştırması ve KONTROLLÜ LABORATUVAR turu. Laboratuvar normal run DEĞİLDİR (satır etiketi "LAB" ile başlar): gerçek bir
// run'ın belirli bir round başındaki durumu (tarla, tile'lar, saksılar, statlar, ödüller) yeni sahnede aynen kurulur ve o round
// aynı seed ile iki kez oynanır: A = olduğu gibi, B = ilgili kırılma ödülü eklenmiş. Fark yalnız ödüldür.
[InitializeOnLoad]
public static class BalanceRunMeasurement
{
    const string Key = "BalanceRunMeasurement.Set";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string ProfileFolder = "Assets/ScriptableObjects/RunProfiles/";
    const float FrameTime = 1f / 30f;

    // ---------------------------------------------------------------- politikalar
    enum Cat { Damage, Crit, Speed, Area, Production, Rarity, Economy, Score, Xp, Cards, Grid, PlanterUnlock, BehaviorUnlock, Duration, Other }

    sealed class Policy
    {
        public string Name;
        public Dictionary<Cat, float> Bias = new();          // kategori → round kayması (negatif: daha erken alır)
        public float PlanterShare = .5f;                      // bir alışverişte saksıya ayrılan kaynak payı
        public Dictionary<TileModifierType, float> CardWeight = new();  // kart tipi tercihi (yoksa 1)
        public string[] RewardOrder = Array.Empty<string>();  // ödül id tercih sırası
        public bool ValueAim;                                 // nişan: bitki sayısı yerine skor değeri
        public bool Naive;                                    // Yeni: rastgele alım, rastgele nişan, rastgele kart ve ödül
        public bool ResonancePlacement;                       // saksı yerleşiminde rezonansı öne alır
        public string[] FirstBuys;                            // açılış denemesi: önce bu düğümler (sırayla), sonra politika
        public bool NoPurchases;                              // açılış denemesi: hiçbir şey almaz (saksı dahil)
        public bool FixedRewardOrder;                         // ölçüm: ödül sırası her round aynı (erken / geç ayrımı yok)
        public int EarlyOnlyUntil;                            // >0: yatırım tercihi yalnız bu round'a kadar; sonra dengeli politika
        public TileModifierType? WantBehavior;                // yol politikası: bu türden ilk kartı (yoksa ilk patlama / elektrik kartını) görünce alır
        public HashSet<UnlockType> AvoidUnlocks = new();      // bu kilitleri açan düğümleri almaz (kart havuzu kendi yolundan seyrelmesin)
        public bool Early(int round) => EarlyOnlyUntil <= 0 || round <= EarlyOnlyUntil;
        public float Bias_(Cat c, int round) => Early(round) && Bias.TryGetValue(c, out float b) ? b : 0f;
    }

    static Policy Speed() => new Policy
    {
        Name = "Hız-alan",
        Bias = { [Cat.Speed] = -4, [Cat.Area] = -4, [Cat.Damage] = 2, [Cat.Crit] = 8, [Cat.Xp] = 4, [Cat.Cards] = 8, [Cat.BehaviorUnlock] = 6, [Cat.Rarity] = 5, [Cat.Score] = 2 },
        CardWeight = { [TileModifierType.Fertile] = 1.6f, [TileModifierType.Energy] = 1.3f },
        RewardOrder = new[] { "hizli_bilek", "firtina_bilegi", "genis_savurus", "bereketli_toprak", "keskin_bicak", "canavar_kesimi", "nadir_tohum", "kritik_goz", "agir_darbe", "altin_hedef", "bilgi_filizi", "kivilcim", "yikim_gucu" },
    };
    static Policy Damage() => new Policy
    {
        Name = "Hasar-değerli hedef", ValueAim = true,
        Bias = { [Cat.Damage] = -4, [Cat.Crit] = -3, [Cat.Rarity] = -4, [Cat.Speed] = 2, [Cat.Area] = 3, [Cat.Production] = 2, [Cat.Xp] = 4, [Cat.Cards] = 8, [Cat.BehaviorUnlock] = 6 },
        CardWeight = { [TileModifierType.Damage] = 1.8f, [TileModifierType.Crystal] = 1.6f, [TileModifierType.Energy] = 1.4f },
        RewardOrder = new[] { "keskin_bicak", "canavar_kesimi", "altin_hedef", "kritik_goz", "agir_darbe", "nadir_tohum", "hizli_bilek", "firtina_bilegi", "genis_savurus", "bereketli_toprak", "bilgi_filizi", "kivilcim", "yikim_gucu" },
    };
    static Policy Behavior() => new Policy
    {
        Name = "Davranış-rezonans", ResonancePlacement = true,
        Bias = { [Cat.BehaviorUnlock] = -8, [Cat.Production] = -2, [Cat.Damage] = -2, [Cat.Crit] = 12, [Cat.Speed] = 1, [Cat.Area] = 3, [Cat.Xp] = 2, [Cat.Cards] = 4 },
        CardWeight = { [TileModifierType.Electric] = 2f, [TileModifierType.Explosive] = 2f, [TileModifierType.Tornado] = 1.9f, [TileModifierType.Boomerang] = 1.9f, [TileModifierType.Damage] = 1.3f },
        RewardOrder = new[] { "kivilcim", "yikim_gucu", "hizli_bilek", "firtina_bilegi", "bereketli_toprak", "genis_savurus", "keskin_bicak", "nadir_tohum", "canavar_kesimi", "bilgi_filizi", "kritik_goz", "agir_darbe", "altin_hedef" },
    };
    static Policy Economy() => new Policy
    {
        // İlk iki segmentte ekonomi ve XP öne alınır (hasar / hız geriden gelir); sonra dengeli politika gibi alır ve seçer.
        Name = "Erken XP-ekonomi", EarlyOnlyUntil = 10,
        Bias = { [Cat.Economy] = -6, [Cat.Xp] = -6, [Cat.Cards] = -3, [Cat.Score] = -2, [Cat.Damage] = 2, [Cat.Speed] = 2, [Cat.Area] = 3, [Cat.Crit] = 8, [Cat.BehaviorUnlock] = 4 },
        CardWeight = { [TileModifierType.Water] = 1.9f, [TileModifierType.Energy] = 1.4f, [TileModifierType.Duplicate] = 1.6f, [TileModifierType.Fertile] = 1.2f },
        RewardOrder = new[] { "bilgi_filizi", "hizli_bilek", "bereketli_toprak", "nadir_tohum", "keskin_bicak", "genis_savurus", "firtina_bilegi", "canavar_kesimi", "kritik_goz", "agir_darbe", "altin_hedef", "kivilcim", "yikim_gucu" },
    };
    // Bölüm 3.6 build yolları. Davranış yolları kendi kartını öne alır, diğer davranış kilitlerini (kasırga, bumerang) açmaz.
    static Policy ExplosionPath() => new Policy
    {
        Name = "Patlama yolu", ResonancePlacement = true, WantBehavior = TileModifierType.Explosive,
        AvoidUnlocks = { UnlockType.TileBehavior_Tornado, UnlockType.TileBehavior_Boomerang },
        Bias = { [Cat.BehaviorUnlock] = -8, [Cat.Production] = -2, [Cat.Damage] = -2, [Cat.Crit] = 12, [Cat.Speed] = 1, [Cat.Area] = 3, [Cat.Xp] = 2, [Cat.Cards] = 4 },
        CardWeight = { [TileModifierType.Explosive] = 3f, [TileModifierType.Damage] = 1.3f, [TileModifierType.Fertile] = 1.2f },
        RewardOrder = new[] { "artci_patlama", "yikim_gucu", "kivilcim", "bereketli_toprak", "hizli_bilek", "firtina_bilegi", "keskin_bicak", "genis_savurus", "nadir_tohum", "canavar_kesimi", "bilgi_filizi", "kritik_goz", "agir_darbe", "altin_hedef", "cifte_akim", "hasat_ritmi" },
        FixedRewardOrder = true,
    };
    static Policy ElectricPath() => new Policy
    {
        Name = "Elektrik yolu", ResonancePlacement = true, WantBehavior = TileModifierType.Electric,
        AvoidUnlocks = { UnlockType.TileBehavior_Tornado, UnlockType.TileBehavior_Boomerang },
        Bias = { [Cat.BehaviorUnlock] = -8, [Cat.Production] = -2, [Cat.Damage] = -2, [Cat.Crit] = 12, [Cat.Speed] = 1, [Cat.Area] = 3, [Cat.Xp] = 2, [Cat.Cards] = 4 },
        CardWeight = { [TileModifierType.Electric] = 3f, [TileModifierType.Damage] = 1.3f, [TileModifierType.Fertile] = 1.2f },
        RewardOrder = new[] { "cifte_akim", "yikim_gucu", "kivilcim", "bereketli_toprak", "hizli_bilek", "firtina_bilegi", "keskin_bicak", "genis_savurus", "nadir_tohum", "canavar_kesimi", "bilgi_filizi", "kritik_goz", "agir_darbe", "altin_hedef", "artci_patlama", "hasat_ritmi" },
        FixedRewardOrder = true,
    };
    static Policy RhythmPath()
    {
        Policy p = Speed();
        p.Name = "Hız-alan + Ritim";
        p.RewardOrder = new[] { "hasat_ritmi" }.Concat(p.RewardOrder).Concat(new[] { "artci_patlama", "cifte_akim" }).ToArray();
        p.FixedRewardOrder = true;
        return p;
    }
    static Policy Balanced() => new Policy { Name = "Dengeli", RewardOrder = new[] { "hizli_bilek", "keskin_bicak", "genis_savurus", "bereketli_toprak", "nadir_tohum", "firtina_bilegi", "canavar_kesimi", "kritik_goz", "bilgi_filizi", "kivilcim", "yikim_gucu", "agir_darbe", "altin_hedef" } };
    static Policy Newbie() => new Policy { Name = "Yeni", Naive = true, PlanterShare = .5f };

    static Policy ByName(string name)
    {
        foreach (var p in new[] { Speed(), Damage(), Behavior(), Economy(), Balanced(), Newbie(), ExplosionPath(), ElectricPath(), RhythmPath() }) if (p.Name == name) return p;
        throw new Exception("policy " + name);
    }

    // ---------------------------------------------------------------- run listeleri
    sealed class RunConfig
    {
        public string Profile = "Run50_DengeV1", Farmer = "bahcivan", Scythe = "standart", PolicyName = "Dengeli", Label = "";
        public int Seed, MaxRound = 50;
        public string[] FirstBuys; public bool NoPurchases;
        public string BossOnly;        // "none": kuralsız boss (hedef ve ödül sürer) · "don" / "shell" / "fog": havuzda yalnız o boss
        public bool NoRewards;         // boss ödülü hiç verilmez (ödüllerin toplam katkısını ölçmek için)
        public string[] RewardOrder;   // politikanın ödül tercih sırasını değiştirir
        public Cat[] Never;            // bu kategorilerdeki düğümleri hiç almaz (yatırımın geri ödemesini ölçmek için)
        public bool NoBreakthroughs;   // kırılma ödülleri havuzdan çıkarılır ("ödülsüz eş": aynı politika ve seed, kırılma ödülü yok)
        public bool DeclineBreakthroughs; // teklifler aynı kalır (aynı zar akışı), bot kırılma ödülünü ALMAZ: diğer iki seçenekten birini alır.
                                          // Kırılma ödülünü alan run ile ödülün alındığı round'a kadar birebir aynıdır ("almayan eş").
        public int ChoicesPerLevel;    // >0: profilin level başına seçim sayısı bu run için değiştirilir (ölçüm)
        public int[] LabRounds;        // bu round'ların başındaki durum kaydedilir; run bitince laboratuvar turları oynanır
        public string LabReward;       // laboratuvarın B kolunda eklenen kırılma ödülü (id)
        public int LabSynergyRound;    // >0: o round'un kaydı üstüne "güçlü sinerji" kurulumu da oynanır (A / B)
    }

    static List<RunConfig> BuildSet(string set)
    {
        var list = new List<RunConfig>();
        void Add(string policy, string farmer, string scythe, int seeds, int maxRound = 50, string profile = "Run50_DengeV1", string label = "", string[] first = null, bool none = false,
                 string boss = null, bool noRewards = false, string[] rewards = null, Cat[] never = null,
                 bool noBreak = false, int choices = 0, int[] lab = null, string labReward = null, int synergy = 0, bool decline = false)
        {
            for (int s = 0; s < seeds; s++)
                list.Add(new RunConfig { Profile = profile, Farmer = farmer, Scythe = scythe, PolicyName = policy, Seed = 100 + s, MaxRound = maxRound, Label = label, FirstBuys = first, NoPurchases = none,
                                         BossOnly = boss, NoRewards = noRewards, RewardOrder = rewards, Never = never,
                                         NoBreakthroughs = noBreak, ChoicesPerLevel = choices, LabRounds = lab, LabReward = labReward, LabSynergyRound = synergy, DeclineBreakthroughs = decline });
        }
        const string K = "Run50_KirilmaV1";
        var paths = new[] { ("Patlama yolu", "artci_patlama"), ("Elektrik yolu", "cifte_akim"), ("Hız-alan + Ritim", "hasat_ritmi") };
        switch (set)
        {
            case "smoke":
                Add("Dengeli", "bahcivan", "standart", 1, 12);
                break;
            case "calib":   // kalibrasyon: hedefler 1; dört yatırım politikası + dengeli + yeni, nötr başlangıç
                foreach (string p in new[] { "Hız-alan", "Hasar-değerli hedef", "Davranış-rezonans", "Erken XP-ekonomi", "Dengeli", "Yeni" })
                    Add(p, "bahcivan", "standart", 2);
                break;
            case "openings": // ilk boss: tek bir zorunlu açılış var mı? İlk 5 round, farklı açılışlar
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "hiçbir şey almaz", none: true);
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "yalnız saksı", first: new[] { "-" });
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "önce hasar", first: new[] { "Keskin Başlangıç - 1", "Keskin Başlangıç - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "önce hız", first: new[] { "Hızlı Eller - 1", "Hızlı Eller - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "önce ekonomi", first: new[] { "Altın Hasat I - 1", "Altın Hasat I - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 5, label: "önce üretim", first: new[] { "Düzenli Üretim - 1", "Grid Genişleme I" });
                Add("Yeni", "bahcivan", "standart", 3, 5, label: "yeni oyuncu");
                Add("Yeni", "bahcivan", "dar_kesim", 3, 5, label: "yeni oyuncu · Dar Kesim");
                break;
            case "bossab":  // boss açık / kapalı: aynı politika ve seed, havuzda tek boss ya da kuralsız boss
                foreach (string p in new[] { "Dengeli", "Hasar-değerli hedef" })
                    foreach (string b in new[] { "none", "don", "shell", "fog" })
                        Add(p, "bahcivan", "standart", 2, label: "boss=" + b, boss: b);
                break;
            case "rewardab": // ödül yolları: aynı politika (dengeli alım) ve seed, yalnız ödül tercihi değişir; "ödül yok" taban
                Add("Dengeli", "bahcivan", "standart", 2, label: "ödül yok", noRewards: true);
                Add("Dengeli", "bahcivan", "standart", 2, label: "hasar yolu", rewards: new[] { "keskin_bicak", "canavar_kesimi", "kritik_goz", "agir_darbe", "altin_hedef" });
                Add("Dengeli", "bahcivan", "standart", 2, label: "hız yolu", rewards: new[] { "hizli_bilek", "firtina_bilegi" });
                Add("Dengeli", "bahcivan", "standart", 2, label: "alan yolu", rewards: new[] { "genis_savurus" });
                Add("Dengeli", "bahcivan", "standart", 2, label: "üretim-nadirlik yolu", rewards: new[] { "bereketli_toprak", "nadir_tohum" });
                Add("Davranış-rezonans", "bahcivan", "standart", 2, label: "davranış yolu", rewards: new[] { "yikim_gucu", "kivilcim" });
                Add("Dengeli", "bahcivan", "standart", 2, label: "XP yolu", rewards: new[] { "bilgi_filizi" });
                break;
            case "payback": // yatırımın geri ödemesi: dengeli politika, bir kategori hiç alınmaz; taban = aynı seed'lerle hepsini alan
                Add("Dengeli", "bahcivan", "standart", 2, label: "hepsini alır (taban)");
                Add("Dengeli", "bahcivan", "standart", 2, label: "ekonomi düğümü almaz", never: new[] { Cat.Economy });
                Add("Dengeli", "bahcivan", "standart", 2, label: "XP ve kart düğümü almaz", never: new[] { Cat.Xp, Cat.Cards });
                Add("Dengeli", "bahcivan", "standart", 2, label: "üretim düğümü almaz", never: new[] { Cat.Production });
                break;
            case "xpspeed": // eşit başlangıç bütçesi, ilk yatırım farklı: hız mı, XP mi, ekonomi mi (30 round)
                Add("Dengeli", "bahcivan", "standart", 3, 30, label: "önce hız", first: new[] { "Hızlı Eller - 1", "Hızlı Eller - 2", "Hızlı Eller - 3", "Hızlı Eller - 4" });
                Add("Dengeli", "bahcivan", "standart", 3, 30, label: "önce XP", first: new[] { "Düzenli Üretim - 1@1", "Hasat Deneyimi I - 1", "Hasat Deneyimi I - 2", "Hasat Deneyimi I - 3", "Hasat Deneyimi I - 4" });
                Add("Dengeli", "bahcivan", "standart", 3, 30, label: "önce ekonomi", first: new[] { "Altın Hasat I - 1", "Altın Hasat I - 2", "Altın Hasat I - 3", "Altın Hasat I - 4" });
                Add("Dengeli", "bahcivan", "standart", 3, 30, label: "önce hasar", first: new[] { "Keskin Başlangıç - 1", "Keskin Başlangıç - 2", "Keskin Başlangıç - 3", "Keskin Başlangıç - 4" });
                Add("Dengeli", "bahcivan", "standart", 3, 30, label: "önce üretim", first: new[] { "Düzenli Üretim - 1", "Düzenli Üretim - 2", "Düzenli Üretim - 3", "Düzenli Üretim - 4" });
                break;
            case "final":   // teslim ölçümü: politikalar × başlangıçlar × seed
                foreach (string p in new[] { "Hız-alan", "Hasar-değerli hedef", "Davranış-rezonans", "Erken XP-ekonomi" })
                    Add(p, "bahcivan", "standart", 4);
                Add("Yeni", "bahcivan", "standart", 4);
                Add("Erken XP-ekonomi", "tuccar", "standart", 4);
                Add("Hız-alan", "tuccar", "standart", 4);
                Add("Hasar-değerli hedef", "secici_yetistirici", "dar_kesim", 4);
                Add("Davranış-rezonans", "secici_yetistirici", "standart", 4);
                Add("Hasar-değerli hedef", "bahcivan", "dar_kesim", 4);
                Add("Hız-alan", "bahcivan", "dar_kesim", 4);
                break;
            // ---------------- Bölüm 3.6 ----------------
            case "k36smoke":  // araç denemesi: kısa run + laboratuvar
                Add("Patlama yolu", "bahcivan", "standart", 1, 12, K, "ödülsüz eş", noBreak: true, lab: new[] { 8, 11 }, labReward: "artci_patlama", synergy: 11);
                Add("Hız-alan + Ritim", "bahcivan", "standart", 1, 11, K, "kırılma");
                break;
            case "k36base":   // düzeltilmiş temiz taban: DengeV1 (üç hata düzeltmesi + yeni temas kuralı), aynı politikalar ve seed'ler
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 10, label: "taban DengeV1");
                Add("Dengeli", "bahcivan", "standart", 4, label: "taban DengeV1");
                Add("Yeni", "bahcivan", "standart", 4, label: "taban DengeV1");
                break;
            case "k36kirilma": // Kırılma V1: gerçek run, kırılma ödülleri açık
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 10, 50, K, "kırılma");
                Add("Dengeli", "bahcivan", "standart", 4, 50, K, "kırılma");
                Add("Yeni", "bahcivan", "standart", 4, 50, K, "kırılma");
                break;
            case "k36twin":   // ödülsüz eş: aynı profil, politika ve seed; kırılma ödülleri havuzda yok. Ardından laboratuvar (R15 / R25 / R40).
                foreach (var (p, reward) in paths) Add(p, "bahcivan", "standart", 10, 50, K, "ödülsüz eş", noBreak: true, lab: new[] { 15, 25, 40 }, labReward: reward, synergy: 25);
                break;
            case "k36decline": // almayan eş: teklifler birebir aynı, bot kırılma ödülünü almaz (ödülün alındığı round'a kadar aynı run)
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 10, 50, K, "almayan eş", decline: true);
                break;
            case "k36lab25":  // ayar turu için kısa laboratuvar: ödülsüz eş R25'e kadar oynanır, yalnız R25 (ve güçlü sinerji) kaydı
                foreach (var (p, reward) in paths) Add(p, "bahcivan", "standart", 10, 25, K, "ödülsüz eş", noBreak: true, lab: new[] { 25 }, labReward: reward, synergy: 25);
                break;
            case "k36choice": // üç seçimin etkisi: level başına 1 seçim; ve boss ödülü hiç yokken (3 ve 1 seçim)
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 5, 50, K, "1 seçim", choices: 1);
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 5, 50, K, "3 seçim · boss ödülü yok", noRewards: true);
                foreach (var (p, _) in paths) Add(p, "bahcivan", "standart", 5, 50, K, "1 seçim · boss ödülü yok", choices: 1, noRewards: true);
                break;
            default: throw new Exception("unknown run set " + set);
        }
        return list;
    }

    // ---------------------------------------------------------------- batch girişleri
    public static void RunSmoke() => Begin("smoke");
    public static void RunCalibration() => Begin("calib");
    public static void RunOpenings() => Begin("openings");
    public static void RunFinal() => Begin("final");
    public static void RunBossAB() => Begin("bossab");
    public static void RunRewardAB() => Begin("rewardab");
    public static void RunXpSpeed() => Begin("xpspeed");
    public static void RunPayback() => Begin("payback");
    public static void RunK36Smoke() => Begin("k36smoke");
    public static void RunK36Base() => Begin("k36base");
    public static void RunK36Kirilma() => Begin("k36kirilma");
    public static void RunK36Twin() => Begin("k36twin");
    public static void RunK36Choice() => Begin("k36choice");
    public static void RunK36Decline() => Begin("k36decline");
    public static void RunK36Lab25() => Begin("k36lab25");

    static BalanceRunMeasurement() { EditorApplication.update += Tick; }

    static void Begin(string set)
    {
        SessionState.SetString(Key, set);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: seçim asset'i ilk run'ın profiline çevrilir (kullanıcının projesindeki seçim değişmez).
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfileFolder + BuildSet(set)[0].Profile + ".asset");
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    // ---------------------------------------------------------------- yürütme
    static string setName; static List<RunConfig> configs; static int runIndex = -1;
    static double nextAt, stepSince; static string lastSignature, lastProgress;
    static readonly StringBuilder csv = new(), nodeCsv = new(), rewardCsv = new(), summary = new();
    static RunBot bot; static Policy policy; static RunConfig config; static RunProfileSO runProfile;
    static bool sceneReady; static int stuckGuard;
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });

    // ---------------------------------------------------------------- laboratuvar (kontrollü karşılaştırma)
    sealed class Snapshot
    {
        public int Round, GridSize;
        public List<(GridPosition cell, TileModifierSO tile, List<StatModifier> rolled, int level)> Tiles = new();
        public List<(PlanterSO planter, GridPosition origin, int rotation)> Planters = new();
        public List<StatModifier> Modifiers = new();                 // ödül dışı global modifier'lar (ağaç, temel güç kartları, başlangıç)
        public List<(BossRewardSO reward, int stacks)> Rewards = new();
        public List<UnlockType> Unlocks = new();
        public SegmentEventSO Boss;                                  // o round'un boss'u (boss round'u değilse null)
    }
    sealed class LabJob { public Snapshot Shot; public bool WithReward, Synergy; public int Repeat; }
    // Her kol bu kadar farklı zar akışıyla oynanır (tek round gürültülüdür); A ve B aynı akışları kullanır.
    const int LabRepeats = 3;
    static readonly List<Snapshot> snapshots = new();
    static readonly Queue<LabJob> labJobs = new();
    static LabJob lab; static RunConfig labConfig;

    static (PlanterSO, GridPosition, int) Locate(PlanterBrain brain)
    {
        var planter = F<PlanterSO>(brain, "planterData");
        var occupied = new HashSet<(int, int)>(brain.OccupiedGrids.Select(g => (g.GetGridPosition().x, g.GetGridPosition().z)));
        foreach (int rotation in new[] { 0, 90, 180, 270 })
            foreach (var origin in brain.OccupiedGrids)
            {
                var o = origin.GetGridPosition(); var cells = new HashSet<(int, int)>();
                for (int x = 0; x < planter.sizeX; x++) for (int z = 0; z < planter.sizeZ; z++) { var off = Rotated(x, z, rotation); cells.Add((o.x + off.x, o.z + off.y)); }
                if (cells.SetEquals(occupied)) return (planter, o, rotation);
            }
        throw new Exception("planter footprint not reproducible: " + planter.name);
    }

    static Snapshot Capture(int round)
    {
        var s = new Snapshot { Round = round, GridSize = F<int>(GridUnlockManager.Instance, "currentUnlockSize") };
        int w = GridManager.Instance.GetWidth(), h = GridManager.Instance.GetHeight();
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
        {
            var ground = Grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
            if (ground == null || ground.IsLocked || ground.CurrentModifier == null) continue;
            s.Tiles.Add((new GridPosition(x, z), ground.CurrentModifier, new List<StatModifier>(ground.RolledModifiers), ground.Level));
        }
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Where(p => p.OccupiedGrids.Count > 0)) s.Planters.Add(Locate(brain));
        var rewards = BossRewardManager.Instance;
        var owned = rewards != null ? rewards.OwnedModifiers.ToList() : new List<StatModifier>();
        foreach (var m in Stats.GlobalModifiers)
        {
            int i = owned.FindIndex(o => o.Equals(m));
            if (i >= 0) owned.RemoveAt(i); else s.Modifiers.Add(m);
        }
        if (rewards != null) foreach (var r in rewards.Taken) s.Rewards.Add((r, rewards.Stacks(r)));
        foreach (UnlockType u in Enum.GetValues(typeof(UnlockType))) if (u != UnlockType.None && UnlockManager.Instance.IsUnlocked(u)) s.Unlocks.Add(u);
        var e = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.ForSegment(HarvestQuota.SegmentOf(round, RM.QuotaSegmentRounds)) : null;
        s.Boss = e != null && RM.IsBossRound(round) && !(e.Data is NoRuleBossSO) ? e.Data : null;
        return s;
    }

    static bool Grant(BossRewardSO reward)
    {
        var boss = BossRewardManager.Instance;
        var offer = F<List<BossRewardSO>>(boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetF(boss, "offerPrepared", true);   // laboratuvar: hazır teklif enjekte edilir (normal run'da kullanılmaz)
        SetP(boss, "IsPending", true);
        bool taken = boss.Choose(reward);
        if (!taken) { SetP(boss, "IsPending", false); offer.Clear(); }
        return taken;
    }

    static TileModifierSO TileAsset(TileModifierType type, TileRarity rarity) =>
        F<List<TileModifierSO>>(Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include), "allModifiers").First(m => m != null && m.modifierType == type && m.rarity == rarity);

    // Kaydı yeni sahnede aynen kurar. extra: B kolunda eklenen kırılma ödülü. synergy: güçlü sinerji kurulumu (aşağıda).
    static string ApplySnapshot(Snapshot s, BossRewardSO extra, bool synergy)
    {
        UnityEngine.Random.InitState(config.Seed * 7919 + 13 + s.Round * 31);
        foreach (UnlockType u in s.Unlocks) UnlockManager.Instance.Unlock(u);
        GridUnlockManager.Instance.UnlockNextTier(s.GridSize);
        var present = Stats.GlobalModifiers.ToList();
        foreach (var m in s.Modifiers)
        {
            int i = present.FindIndex(o => o.Equals(m));
            if (i >= 0) present.RemoveAt(i); else Stats.AddGlobalModifier(m);
        }
        foreach (var (reward, stacks) in s.Rewards) for (int i = 0; i < stacks; i++) if (!Grant(reward)) throw new Exception("lab: reward not granted " + reward.id);
        foreach (var (cell, tile, rolled, level) in s.Tiles)
        {
            var ground = Grid.GetGridObject(cell).GetGroundCellCached();
            ground.ApplyModifier(tile, rolled);
            SetF(ground, "level", level);   // değerler zaten seviyeli kaydedildi
        }
        foreach (var (planter, origin, rotation) in s.Planters)
        {
            Bank.AddResource(planter.PriceType, planter.Price);
            Place(planter, new Spot { Origin = Grid.GetGridObject(origin), Rotation = rotation });
        }
        string note = "";
        if (synergy) note = ApplySynergy(extra);
        if (extra != null && !Grant(extra)) throw new Exception("lab: breakthrough not granted " + extra.id);
        return note;
    }

    // Güçlü sinerji kurulumu (laboratuvar; normal run değil): kaydın üstüne, o yolun "iyi kurulmuş" hâli.
    //  - Davranış yolları: her açık boş hücreye 1×1 saksı; saksı altındaki her hücre o yolun Legendary tile'ı (zar aralığının üst ucu);
    //    Kıvılcım ve Yıkım Gücü en çok adede tamamlanır.
    //  - Hasat Ritmi: her açık boş hücreye 1×1 saksı; Geniş Savuruş ve Hızlı Bilek en çok adede tamamlanır.
    static string ApplySynergy(BossRewardSO extra)
    {
        var pool = runProfile.bossRewards;
        var all = pool != null ? pool.rewards : new List<BossRewardSO>();
        void Max(string id)
        {
            var reward = all.FirstOrDefault(r => r.id == id);
            if (reward == null) return;
            while (BossRewardManager.Instance.Stacks(reward) < reward.maxStacks) if (!Grant(reward)) break;
        }
        var small = Planters.First(p => p.sizeX == 1 && p.sizeZ == 1);
        int w = GridManager.Instance.GetWidth(), h = GridManager.Instance.GetHeight(), added = 0, tiles = 0;
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
        {
            var g = Grid.GetGridObject(new GridPosition(x, z)); var ground = g?.GetGroundCellCached();
            if (ground == null || ground.IsLocked || g.HasPlanterObject()) continue;
            Bank.AddResource(small.PriceType, small.Price);
            Place(small, new Spot { Origin = g, Rotation = 0 });
            added++;
        }
        if (extra.echo != BossRewardEcho.None)
        {
            var tile = TileAsset(extra.echo == BossRewardEcho.Explosion ? TileModifierType.Explosive : TileModifierType.Electric, TileRarity.Legendary);
            var top = tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = r.maxValue }).ToList();
            for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
            {
                var g = Grid.GetGridObject(new GridPosition(x, z)); var ground = g?.GetGroundCellCached();
                if (ground == null || ground.IsLocked || !g.HasPlanterObject()) continue;
                ground.ApplyModifier(tile, top);
                tiles++;
            }
            Max("kivilcim"); Max("yikim_gucu");
            return $"sinerji: +{added} saksı, {tiles} hücre {tile.modifierName} (Legendary, üst değer), Kıvılcım ve Yıkım Gücü tam";
        }
        Max("genis_savurus"); Max("hizli_bilek");
        return $"sinerji: +{added} saksı, Geniş Savuruş ve Hızlı Bilek tam";
    }

    // Run bitti: kayıtlardan laboratuvar işleri (A = olduğu gibi, B = kırılma ödülüyle).
    static double AfterRun()
    {
        foreach (var s in snapshots)
            for (int repeat = 0; repeat < LabRepeats; repeat++)
            {
                labJobs.Enqueue(new LabJob { Shot = s, WithReward = false, Repeat = repeat });
                labJobs.Enqueue(new LabJob { Shot = s, WithReward = true, Repeat = repeat });
                if (config.LabSynergyRound == s.Round)
                {
                    labJobs.Enqueue(new LabJob { Shot = s, WithReward = false, Synergy = true, Repeat = repeat });
                    labJobs.Enqueue(new LabJob { Shot = s, WithReward = true, Synergy = true, Repeat = repeat });
                }
            }
        if (config.LabRounds != null)
            foreach (int r in config.LabRounds.Where(r => snapshots.All(s => s.Round != r)))
                summary.AppendLine($"   LAB R{r} yok: run o round'a ulaşmadı (seed {config.Seed}, {policy.Name})");
        snapshots.Clear();
        return NextLab();
    }

    static double NextLab()
    {
        bot = null; sceneReady = false; stuckGuard = 0;
        if (labJobs.Count == 0) { lab = null; return NextRun(); }
        lab = labJobs.Dequeue();
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfileFolder + config.Profile + ".asset");
        runProfile = Object.Instantiate(source);
        runProfile.name = source.name + " (laboratuvar)";
        runProfile.bossSeed = 7000 + config.Seed;
        if (lab.Shot.Boss != null)
        {
            // Round'un boss'u gerçek run'daki türle aynı (bölgesi laboratuvarda yeniden seçilir; iki kolda aynıdır).
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.entries.Add(new BossPoolSO.Entry { boss = lab.Shot.Boss, weight = 1f });
            runProfile.bossPool = pool;
        }
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = runProfile;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 1.5;
    }

    static double DriveLab()
    {
        if (!sceneReady)
        {
            if (GameManager.Instance == null || RM == null || RM.Profile != runProfile || State != GameStates.RunSetup)
            { if (stuckGuard++ > 600) throw new Exception("lab scene did not reach RunSetup"); return .05; }
            Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
            var pool = runProfile.bossRewards;
            var extra = lab.WithReward || lab.Synergy ? pool.breakthroughs.First(r => r.id == config.LabReward) : null;
            string note = ApplySnapshot(lab.Shot, lab.WithReward ? extra : null, false);
            if (lab.Synergy) { note = ApplySynergy(extra); if (lab.WithReward && BossRewardManager.Instance.Stacks(extra) == 0) Grant(extra); }
            labConfig = new RunConfig { Profile = config.Profile, Farmer = config.Farmer, Scythe = config.Scythe, PolicyName = config.PolicyName, Seed = config.Seed, MaxRound = lab.Shot.Round,
                                        Label = $"LAB R{lab.Shot.Round} {(lab.Synergy ? "sinerji " : "")}{(lab.WithReward ? "B +" + config.LabReward : "A ödülsüz")} #{lab.Repeat + 1}" };
            if (note != "" && !lab.WithReward && lab.Repeat == 0) summary.AppendLine($"   {labConfig.Label} · {note}");
            bot = new GameObject("Run bot (laboratory)").AddComponent<RunBot>();
            bot.Init(policy, labConfig, runIndex, csv);
            // Aynı zar akışı: iki kol da round'a aynı seed ile ve dolu tarlayla girer (her üretim noktasında bir bitki).
            UnityEngine.Random.InitState(config.Seed * 7919 + 17 + lab.Shot.Round * 131 + lab.Repeat * 977);
            foreach (var spawner in Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None))
                if (spawner.GridObject != null && !spawner.GridObject.HasPlantObject()) Call(spawner, "TrySpawnPlant");
            SetP(RM, "CurrentRound", lab.Shot.Round - 1); SetF(RM, "awaitingFirstRound", false);
            Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
            RM.StartNextRound();
            if (State != GameStates.Round || RM.CurrentRound != lab.Shot.Round) throw new Exception("lab round did not start: " + State + " R" + RM.CurrentRound);
            sceneReady = true;
            return .05;
        }
        if (State == GameStates.Round) return .05;
        summary.AppendLine($"   {labConfig.Label} | {policy.Name} | seed {config.Seed} | {bot.LabLine}");
        Time.captureDeltaTime = 0f;
        WriteCsv();
        return NextLab();
    }

    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static RoundManager RM => RoundManager.Instance;
    static GameStates State => GameManager.Instance.CurrentState;
    static StatManager Stats => StatManager.Instance;
    static ResourceManager Bank => ResourceManager.Instance;
    static GridSystem Grid => GridManager.Instance.GetGridSystem();
    static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

    static void Tick()
    {
        string set = SessionState.GetString(Key, "");
        if (set == "" || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (configs == null)
            {
                setName = set; configs = BuildSet(set); nextAt = EditorApplication.timeSinceStartup + 2;
                csv.AppendLine(RunBot.Header);
                nodeCsv.AppendLine("run,policy,farmer,scythe,seed,label,round,node,level,cost,currency");
                rewardCsv.AppendLine("run,policy,farmer,scythe,seed,label,round,offered,chosen");
                MetaSave.UseMemoryOnly();
            }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            double wait = Drive();
            if (wait < 0) { Finish(null); return; }
            nextAt = EditorApplication.timeSinceStartup + wait;
            // takılma dedektörü: durum imzası 240 sn değişmezse
            string signature = runIndex + "/" + (RM != null ? RM.CurrentRound : -1) + "/" + (GameManager.Instance != null ? State.ToString() : "-") + "/" + (RM != null ? Mathf.FloorToInt(RM.RemainingTime) : -1);
            if (signature != lastSignature) { lastSignature = signature; stepSince = EditorApplication.timeSinceStartup; }
            string progress = runIndex + "/" + (RM != null ? RM.CurrentRound : -1);
            if (progress != lastProgress) { lastProgress = progress; WriteProgress(); }
            else if (EditorApplication.timeSinceStartup - stepSince > 240) throw new Exception("stuck: " + signature + " timeScale " + Time.timeScale);
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void WriteProgress()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText($"Logs/BalanceRuns_{setName}.txt", $"RUNNING run {runIndex + 1}/{configs.Count} round {(RM != null ? RM.CurrentRound : 0)}\n" + summary);
    }

    // Her run bitince yazılır: uzun bir set sürerken biten run'lar incelenebilir.
    static void WriteCsv()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText($"Logs/BalanceRuns_{setName}.csv", csv.ToString(), new UTF8Encoding(false));
        File.WriteAllText($"Logs/BalanceRuns_{setName}_nodes.csv", nodeCsv.ToString(), new UTF8Encoding(false));
        File.WriteAllText($"Logs/BalanceRuns_{setName}_rewards.csv", rewardCsv.ToString(), new UTF8Encoding(false));
    }

    static void Finish(Exception ex)
    {
        SessionState.SetString(Key, "");
        Time.captureDeltaTime = 0f;
        WriteCsv();
        File.WriteAllText($"Logs/BalanceRuns_{setName}.txt", (ex == null ? "DONE (ölçüm; işlev testi değil)\n" : "FAIL: " + ex + "\n") + summary);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static double Drive()
    {
        if (runIndex < 0) return NextRun();
        if (lab != null) return DriveLab();
        if (!sceneReady)
        {
            // Yeni sahne yüklenene kadar eski sahnenin yöneticileri görünebilir: profil eşleşene ve hazırlık durumuna gelene kadar bekle.
            if (GameManager.Instance == null || RM == null || RM.Profile != runProfile || State != GameStates.RunSetup)
            { if (stuckGuard++ > 600) throw new Exception("scene did not reach RunSetup with the measurement profile"); return .05; }
            PrepareScene();
            return .05;
        }
        switch (State)
        {
            case GameStates.RunSetup:
            case GameStates.RoundEnd:
            case GameStates.Shop:
                if (RM.IsRoundChoicePending) return .02;
                if (RM.CurrentRound >= config.MaxRound && !RM.IsPreparingFirstRound) { EndRun("durduruldu (ölçüm sınırı)"); return AfterRun(); }
                DoShop();
                int upcoming = RM.IsPreparingFirstRound ? 1 : RM.CurrentRound + 1;
                if (config.LabRounds != null && config.LabRounds.Contains(upcoming)) snapshots.Add(Capture(upcoming));
                Time.timeScale = 1f; Time.captureDeltaTime = FrameTime;
                RM.StartNextRound();
                if (State != GameStates.Round && State != GameStates.RunComplete) throw new Exception("round did not start: " + State);
                return .05;
            case GameStates.Round:
                return .05;
            case GameStates.CardSelection:
                PickCard();
                return .01;
            case GameStates.RoundChoice:
                PickReward();
                return .01;
            case GameStates.RunComplete:
                EndRun(RM.Outcome == RunOutcome.Victory ? "KAZANDI" : RM.Outcome == RunOutcome.QuotaFailed ? "KOTA" : RM.Outcome == RunOutcome.BossFailed ? "BOSS" : RM.Outcome.ToString());
                return AfterRun();
            default:
                return .05;
        }
    }

    static double NextRun()
    {
        runIndex++;
        bot = null; sceneReady = false; stuckGuard = 0;
        if (runIndex >= configs.Count) return -1;
        config = configs[runIndex];
        policy = ByName(config.PolicyName);
        policy.FirstBuys = config.FirstBuys; policy.NoPurchases = config.NoPurchases;
        if (config.Never != null) foreach (Cat c in config.Never) policy.Bias[c] = 9999f;
        if (config.RewardOrder != null) { policy.RewardOrder = config.RewardOrder.Concat(policy.RewardOrder.Where(r => !config.RewardOrder.Contains(r))).ToArray(); policy.FixedRewardOrder = true; }
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfileFolder + config.Profile + ".asset");
        if (source == null) throw new Exception("profile not found: " + config.Profile);
        runProfile = Object.Instantiate(source);
        runProfile.name = source.name + " (ölçüm)";
        if (runProfile.bossPool != null) runProfile.bossSeed = 7000 + config.Seed;   // aynı seed aynı boss ve ödül dizisi
        if (config.NoRewards) runProfile.bossRewards = null;
        if (config.NoBreakthroughs && runProfile.bossRewards != null)
        {
            // Ödülsüz eş: aynı havuz, kırılma ödülleri olmadan (asset değişmez; kopya üzerinde).
            runProfile.bossRewards = Object.Instantiate(runProfile.bossRewards);
            runProfile.bossRewards.breakthroughs.Clear();
        }
        if (config.ChoicesPerLevel > 0) runProfile.choicesPerLevel = config.ChoicesPerLevel;
        if (config.BossOnly != null && runProfile.bossPool != null)
        {
            // Tek adaylı havuz. "none": hiçbir zaman uygun olmayan aday → oyunun kendi yedeği (kuralsız boss; hedef ve ödül sürer).
            SegmentEventSO only = null;
            foreach (var entry in runProfile.bossPool.entries)
                if ((config.BossOnly == "don" && entry.boss is FrostFrontSO) || (config.BossOnly == "shell" && entry.boss is HardShellSO) || (config.BossOnly == "fog" && entry.boss is FogSO)) only = entry.boss;
            if (config.BossOnly == "none") { var never = ScriptableObject.CreateInstance<FogSO>(); never.displayName = "(yok)"; never.minPlayerRadius = 999f; only = never; }
            if (only == null) throw new Exception("boss not in pool: " + config.BossOnly);
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.entries.Add(new BossPoolSO.Entry { boss = only, weight = 1f });
            runProfile.bossPool = pool;
        }
        AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath).active = runProfile;
        // Başlangıç seçimi: bellekteki kayıt (kullanıcının kaydına dokunmaz). Görevle açılan içerik bu ölçümde açık sayılır.
        var catalog = StartCatalogSO.Active;
        foreach (var f in catalog.farmers) if (f != null && !MetaSave.Data.unlocked.Contains(f.id)) MetaSave.Data.unlocked.Add(f.id);
        foreach (var s in catalog.scythes) if (s != null && !MetaSave.Data.unlocked.Contains(s.id)) MetaSave.Data.unlocked.Add(s.id);
        var farmer = catalog.Farmer(config.Farmer); var scythe = catalog.Scythe(config.Scythe);
        if (farmer == null || scythe == null) throw new Exception("start option not found: " + config.Farmer + " / " + config.Scythe);
        MetaSave.SetSelection(farmer, scythe);
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        SceneManager.LoadScene("GameScene");
        return 1.5;
    }

    static void PrepareScene()
    {
        if (RM.Profile != runProfile) throw new Exception("measurement profile not active");
        var loadout = StartLoadoutManager.Instance;
        if (loadout == null || loadout.Farmer == null || loadout.Farmer.id != config.Farmer || loadout.Scythe.id != config.Scythe)
            throw new Exception($"start loadout mismatch: {loadout?.Farmer?.id} / {loadout?.Scythe?.id}");
        Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include).enabled = false;
        UnityEngine.Random.InitState(config.Seed * 7919 + 13);
        bot = new GameObject("Run bot (measurement)").AddComponent<RunBot>();
        bot.Init(policy, config, runIndex, csv);
        sceneReady = true;
    }

    static void EndRun(string outcome)
    {
        bot.Close(outcome);
        var rewards = BossRewardManager.Instance != null ? string.Join(" ", BossRewardManager.Instance.Taken.Select(r => r.id + "×" + BossRewardManager.Instance.Stacks(r))) : "";
        int tiers = SkillTreeManager.Instance.AllNodes.Sum(n => SkillTreeManager.Instance.GetCurrentLevel(n));
        int total = SkillTreeManager.Instance.AllNodes.Sum(n => n.tiers.Count);
        summary.AppendLine($"run {runIndex + 1} | {config.Profile} | {policy.Name} | {config.Farmer}+{config.Scythe} | seed {config.Seed} | {config.Label} | {outcome} @R{RM.CurrentRound} | " +
                           $"skor {HarvestScoreManager.Instance.TotalScore} | level {ProgressionManager.Instance.CurrentLevel} | ağaç {tiers}/{total} | ödül {rewards} | " +
                           $"{RM.LevelsGained} level · {RM.CardChoicesGranted} seçim hakkı · kart {bot.CardTiles} tile + {bot.CardUpgrades} yükseltme + {bot.CardBase} temel güç | " +
                           $"ilk davranış kartı R{bot.FirstBehavior} · ilk tetik R{bot.FirstTrigger} | kırılma {(bot.Breakthroughs.Count > 0 ? string.Join(" ", bot.Breakthroughs) : "-")}");
        Time.captureDeltaTime = 0f;
        WriteCsv();
    }

    // ---------------------------------------------------------------- alışveriş
    static Cat Category(SkillNodeSO n)
    {
        switch (n.unlockType)
        {
            case UnlockType.Planter_1x3: case UnlockType.Planter_2x2: case UnlockType.Planter_2x3: return Cat.PlanterUnlock;
            case UnlockType.None: break;
            case UnlockType.TileBehavior_Duplicate: return Cat.Economy;
            default: return Cat.BehaviorUnlock;
        }
        foreach (var tier in n.tiers) foreach (var e in tier.effects)
            switch (e.statType)
            {
                case StatType.HarvestDamage: return Cat.Damage;
                case StatType.CritChance: case StatType.CritMultiplier: return Cat.Crit;
                case StatType.AttackSpeed: return Cat.Speed;
                case StatType.AreaRadius: return Cat.Area;
                case StatType.PlantSpawnRate: return Cat.Production;
                case StatType.RareSpawnChance: return Cat.Rarity;
                case StatType.GoldGainMultiplier: case StatType.IronGainMultiplier: case StatType.StoneGainMultiplier: return Cat.Economy;
                case StatType.HarvestScoreMultiplier: return Cat.Score;
                case StatType.XPGainMultiplier: return Cat.Xp;
                case StatType.MutationLuck: case StatType.CardSkip: return Cat.Cards;
                case StatType.GridUnlockSize: return Cat.Grid;
                case StatType.RoundDuration: return Cat.Duration;
            }
        return Cat.Other;
    }

    // Kademenin "vadesi": ailenin hedef penceresi kademelere yayılır (ilk kademe pencere başı, son kademe pencere sonu).
    static readonly Dictionary<SkillNodeSO, (int index, int count)> familyIndex = new();
    static float Due(SkillNodeSO n, int level)
    {
        if (!familyIndex.TryGetValue(n, out var fi))
        {
            string prefix = n.name.Contains(" - ") ? n.name.Substring(0, n.name.LastIndexOf(" - ", StringComparison.Ordinal)) : n.name;
            var family = SkillTreeManager.Instance.AllNodes.Where(x => x.name == prefix || x.name.StartsWith(prefix + " - ", StringComparison.Ordinal)).OrderBy(x => x.name, StringComparer.Ordinal).ToList();
            int before = 0; foreach (var x in family) { if (x == n) break; before += x.tiers.Count; }
            fi = (before, Math.Max(1, family.Sum(x => x.tiers.Count)));
            familyIndex[n] = fi;
        }
        float t = fi.count <= 1 ? 0f : (fi.index + level) / (float)(fi.count - 1);
        return Mathf.Lerp(n.targetRounds.x, n.targetRounds.y, t);
    }

    static readonly System.Random naive = new System.Random(12345);

    static void DoShop()
    {
        if (policy.NoPurchases) return;
        if (State != GameStates.Shop) GameManager.Instance.OpenShop();
        bool planterOnly = policy.FirstBuys != null && policy.FirstBuys.Length == 1 && policy.FirstBuys[0] == "-";
        // 0) açılış denemesi: önce en az iki saksı (bitkisiz tarla anlamsız), sonra belirli düğümler
        if (policy.FirstBuys != null && Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Count(pl => pl.OccupiedGrids.Count > 0) < 2) BuyPlanters(1f, 2);
        if (policy.FirstBuys != null && !planterOnly)
            foreach (string entry in policy.FirstBuys)
            {
                // "ad@kademe": yalnız o kademeye kadar (ön koşul için gereken kadar)
                int at = entry.LastIndexOf('@'); string name = at > 0 ? entry.Substring(0, at) : entry; int cap = at > 0 ? int.Parse(entry.Substring(at + 1)) : int.MaxValue;
                var node = SkillTreeManager.Instance.AllNodes.FirstOrDefault(n => n.name == name);
                if (node == null) throw new Exception("first-buy node not found: " + name);
                while (SkillTreeManager.Instance.GetCurrentLevel(node) < cap && SkillTreeManager.Instance.CanUpgrade(node)) Buy(node);
            }
        BuyPlanters(PlanterShare());
        if (!planterOnly) BuySkills();
        BuyPlanters(PlanterShare());   // skill'lerden artan ve yeni açılan saksı / alan için
        if (State != GameStates.Shop) throw new Exception("shop left in state " + State);
    }

    static void Buy(SkillNodeSO node)
    {
        int level = SkillTreeManager.Instance.GetCurrentLevel(node);
        var tier = node.tiers[level];
        if (!SkillTreeManager.Instance.TryUpgrade(node)) throw new Exception("upgrade failed: " + node.name);
        bot.TiersBought++;
        nodeCsv.AppendLine($"{runIndex + 1},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{ShopRound},{node.name},{level + 1},{tier.cost},{tier.costType}");
    }

    // Alışveriş round'u: ilk hazırlık 0, sonra biten round.
    static int ShopRound => RM.IsPreparingFirstRound ? 0 : RM.CurrentRound;

    static void BuySkills()
    {
        var tree = SkillTreeManager.Instance;
        int round = ShopRound + 1;   // bir sonraki round için alışveriş
        // Biriktirme: vadesi gelmiş pahalı bir kademe için o kaynak ayrılır, ama ucuz kademeler (ayrılan tutarın dörtte birine kadar)
        // alınmaya devam eder; tek bir pahalı hedef bütün ağacı durdurmaz.
        var blocked = new Dictionary<ResourceType, int>();
        for (int guard = 0; guard < 400; guard++)
        {
            SkillNodeSO pick = null; float pickKey = float.MaxValue; bool saving = false;
            var candidates = new List<(SkillNodeSO node, float key)>();
            foreach (var n in tree.AllNodes)
            {
                if (n == null || tree.IsMaxLevel(n) || tree.IsDisabledByProfile(n)) continue;
                if (policy.AvoidUnlocks.Contains(n.unlockType)) continue;
                int lv = tree.GetCurrentLevel(n);
                if (lv == 0 && !tree.MeetsPrerequisites(n)) continue;
                // Yeni oyuncu: plan yok, "alabildiğim en ucuz şey" (küçük rastgele sapmayla).
                float key = policy.Naive ? n.tiers[lv].cost * (n.tiers[lv].costType == ResourceType.Gold ? 1f : n.tiers[lv].costType == ResourceType.Iron ? 3f : 6f) * (.7f + (float)naive.NextDouble() * .6f)
                    : Due(n, lv) + policy.Bias_(Category(n), round);
                candidates.Add((n, key));
            }
            foreach (var (n, key) in candidates.OrderBy(c => c.key))
            {
                var tier = n.tiers[tree.GetCurrentLevel(n)];
                if (blocked.TryGetValue(tier.costType, out int reserved) && tier.cost > reserved * .25f) continue;
                if (!policy.Naive && key > round + 3f) break;            // henüz erken: biriktir
                if (Bank.CanAfford(tier.costType, tier.cost))
                {
                    if (policy.Naive && naive.NextDouble() < .35) continue; // yeni oyuncu: alabileceğini her zaman almaz
                    pick = n; pickKey = key; break;
                }
                // Vadesi gelmiş ve yakında alınabilecek kademe için o kaynak biriktirilir.
                if (!policy.Naive && key <= round && tier.cost <= Bank.GetResourceAmount(tier.costType) + bot.LastIncome(tier.costType) * 2.5f)
                { if (!blocked.ContainsKey(tier.costType)) blocked[tier.costType] = tier.cost; saving = true; }
            }
            if (pick == null) { _ = saving; break; }
            Buy(pick);
            if (policy.Naive && guard >= 2) break;   // yeni oyuncu: bir alışverişte en çok üç kademe
        }
    }

    static List<PlanterSO> planterAssets;
    static List<PlanterSO> Planters => planterAssets ??= AssetDatabase.FindAssets("t:PlanterSO", new[] { "Assets/ScriptableObjects/Planters" })
        .Select(g => AssetDatabase.LoadAssetAtPath<PlanterSO>(AssetDatabase.GUIDToAssetPath(g))).OrderByDescending(p => p.sizeX * p.sizeZ).ThenBy(p => p.name).ToList();

    static readonly double[] RarityPower = { 1, 1.85, 2.9, 4.6 };

    sealed class Spot { public GridObject Origin; public int Rotation; public List<GridObject> Cells; public List<PlanterBrain> Sell = new(); public double Score; }

    static Vector2Int Rotated(int x, int z, int rotation) => rotation == 90 ? new Vector2Int(z, -x) : rotation == 180 ? new Vector2Int(-x, -z) : rotation == 270 ? new Vector2Int(-z, x) : new Vector2Int(x, z);

    static double TileScore(List<GridObject> cells)
    {
        double s = 0; var counts = new Dictionary<TileModifierType, int>();
        foreach (var g in cells)
        {
            var cell = g.GetGroundCellCached();
            if (cell == null || cell.CurrentModifier == null) continue;
            float w = policy.CardWeight.TryGetValue(cell.CurrentModifier.modifierType, out float cw) ? cw : 1f;
            s += RarityPower[(int)cell.CurrentModifier.rarity] * w * (1 + .25 * cell.Level);
            counts.TryGetValue(cell.CurrentModifier.modifierType, out int n); counts[cell.CurrentModifier.modifierType] = n + 1;
        }
        var mods = new List<StatModifier>(); var active = new List<ActiveResonance>();
        ResonanceManager.Evaluate(ResonanceManager.DefaultRules, counts, mods, active);
        return s + active.Count * (policy.ResonancePlacement ? 6 : 3);
    }

    // Boş yerleşimler; allowReplace: tamamen bölgenin içinde kalan daha küçük saksılar satılarak açılan yerler de.
    static List<Spot> Spots(PlanterSO planter, bool allowReplace)
    {
        var result = new List<Spot>();
        int w = GridManager.Instance.GetWidth(), h = GridManager.Instance.GetHeight(), size = planter.sizeX * planter.sizeZ;
        foreach (int rotation in planter.sizeX == planter.sizeZ ? new[] { 0 } : new[] { 0, 90 })
            for (int ox = 0; ox < w; ox++) for (int oz = 0; oz < h; oz++)
            {
                var cells = new List<GridObject>(); var sell = new HashSet<PlanterBrain>(); bool ok = true;
                for (int x = 0; x < planter.sizeX && ok; x++) for (int z = 0; z < planter.sizeZ && ok; z++)
                {
                    var off = Rotated(x, z, rotation);
                    var g = Grid.GetGridObject(new GridPosition(ox + off.x, oz + off.y));
                    var cell = g?.GetGroundCellCached();
                    if (g == null || cell == null || cell.IsLocked) { ok = false; break; }
                    if (g.HasPlanterObject())
                    {
                        var brain = g.GetPlanterBrain();
                        if (!allowReplace || brain == null || brain.OccupiedGrids.Count >= size) { ok = false; break; }
                        sell.Add(brain);
                    }
                    cells.Add(g);
                }
                if (!ok) continue;
                // Satılacak saksı bölgenin dışına taşıyorsa bu yer kullanılmaz (boşa hücre açmasın).
                if (sell.Any(b => b.OccupiedGrids.Any(g => !cells.Contains(g)))) continue;
                if (allowReplace && sell.Count == 0) continue;
                var origin = Grid.GetGridObject(new GridPosition(ox, oz));
                result.Add(new Spot { Origin = origin, Rotation = rotation, Cells = cells, Sell = sell.ToList(), Score = TileScore(cells) });
            }
        return result;
    }

    // Altınla alınan saksıya ayrılan pay, son round'un tarla doluluğuna göre: tarla boşalıyorsa (üretim yetmiyor) saksı önce gelir,
    // tarla doluysa altın skill'lere kalır. İlk hazırlıkta (ölçüm yok) yarı yarıya.
    static float PlanterShare()
    {
        if (policy.Naive) return 1f;
        float fill = bot.LastFill;
        return fill < .55f ? 1f : fill < .72f ? .5f : .2f;   // ilk hazırlıkta ölçüm yok (-1): önce saksı
    }

    static void BuyPlanters(float share, int limit = 80)
    {
        var budget = new Dictionary<ResourceType, int>();
        // Demir / taşla alınan büyük saksılar nicelik değil nitelik (nadir bitki): payı doluluğa bağlı değil.
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            budget[t] = Mathf.FloorToInt(Bank.GetResourceAmount(t) * (t == ResourceType.Gold || policy.Naive ? share : policy.PlanterShare));
        // Açılış: ilk hazırlıkta en az iki saksıya yetecek kadar altın her politikada saksıya gider.
        for (int guard = 0; guard < limit; guard++)
        {
            bool bought = false;
            foreach (var planter in Planters)
            {
                if (!planter.IsUnlocked || planter.prefab == null) continue;
                int price = planter.Price; ResourceType type = planter.PriceType;
                if (price > budget[type] || !Bank.CanAfford(type, price)) continue;
                if (policy.Naive && naive.NextDouble() < .35) continue;   // yeni oyuncu: her fırsatta saksı almaz
                var spots = Spots(planter, false);
                // Büyük saksı (4+ hücre) için yer yoksa içindeki küçük saksıları satıp yer açar; yeni oyuncu bunu yapmaz.
                if (spots.Count == 0 && !policy.Naive && planter.sizeX * planter.sizeZ >= 4) spots = Spots(planter, true);
                if (spots.Count == 0) continue;
                Spot spot = policy.Naive ? spots[naive.Next(spots.Count)] : spots.OrderByDescending(s => s.Score - s.Sell.Count * .5).First();
                foreach (var brain in spot.Sell) brain.RemoveSelf();
                Place(planter, spot);
                budget[type] -= price; bought = true; bot.PlantersBought++;
                break;
            }
            if (!bought) break;
        }
    }

    // Mağazanın satın alma yolu: önce kaynak harcanır, sonra PlacementManager yerleştirir (PlanterShopPanelUI.Buy ile aynı sıra).
    static void Place(PlanterSO planter, Spot spot)
    {
        if (State != GameStates.Shop) GameManager.Instance.OpenShop();
        if (!Bank.SpendResource(planter.PriceType, planter.Price)) throw new Exception("planter not affordable: " + planter.name);
        var placement = PlacementManager.Instance;
        placement.StartPlacement(planter);
        if (State != GameStates.Placing) throw new Exception("placement did not start for " + planter.name);
        SetF(placement, "currentRotation", spot.Rotation);
        var ghost = F<GameObject>(placement, "ghostObject");
        ghost.transform.rotation = Quaternion.Euler(0, spot.Rotation, 0);
        ghost.transform.position = spot.Origin.GetGroundCellCached().transform.position + (Vector3)Call(placement, "GetGhostCenterOffset");
        Call(placement, "PlacePlanter", spot.Origin);
        if (State != GameStates.Shop) throw new Exception($"planter {planter.name} was not placed at {spot.Origin.GetGridPosition()} rot {spot.Rotation}: state {State}");
    }

    // ---------------------------------------------------------------- kart ve ödül
    static CardSelectionUI cardUI;

    static void PickCard()
    {
        if (cardUI == null) cardUI = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        var offers = F<List<TileCardOffer>>(cardUI, "currentCards");
        if (offers == null || offers.Count == 0)
        {
            // Kart ekranı bu karede henüz kurulmadıysa bir sonraki denemede; kurulamıyorsa oyunun kendi yolu ile tazele.
            if (stuckGuard++ > 50) { stuckGuard = 0; cardUI.RefreshCards(); }
            return;
        }
        stuckGuard = 0;
        TileCardOffer pick;
        if (policy.Naive) pick = offers[naive.Next(offers.Count)];
        else pick = offers.OrderByDescending(o =>
        {
            double rarity = RarityPower[(int)o.Rarity];
            float w = o.Tile != null && policy.Early(RM.CurrentRound) && policy.CardWeight.TryGetValue(o.Tile.modifierType, out float cw) ? cw : 1f;
            return rarity * w;
        }).First();
        // Yol politikası: kendi davranış kartını henüz almadıysa, teklif edildiği anda alır (yoksa ilk patlama / elektrik kartını).
        // Bot yalnız ekrandaki üç adayı görür; yeri seçmez.
        if (!policy.Naive && policy.WantBehavior.HasValue && bot.FirstOwn == 0)
        {
            bool Is(TileCardOffer o, params TileModifierType[] types) => !o.IsUpgrade && o.Tile != null && types.Contains(o.Tile.modifierType);
            var own = offers.Where(o => Is(o, policy.WantBehavior.Value)).OrderByDescending(o => RarityPower[(int)o.Rarity]).FirstOrDefault();
            if (own == null && bot.FirstBehavior == 0)
                own = offers.Where(o => Is(o, TileModifierType.Explosive, TileModifierType.Electric)).OrderByDescending(o => RarityPower[(int)o.Rarity]).FirstOrDefault();
            if (own != null) pick = own;
        }
        bot.CardScreens++;
        if (pick.IsUpgrade) bot.CardUpgrades++;
        else if (pick.IsBaseStat) bot.CardBase++;
        else
        {
            bot.CardTiles++;
            var type = pick.Tile.modifierType;
            if ((type == TileModifierType.Explosive || type == TileModifierType.Electric) && bot.FirstBehavior == 0) bot.FirstBehavior = RM.CurrentRound;
            if (policy.WantBehavior == type && bot.FirstOwn == 0) bot.FirstOwn = RM.CurrentRound;
        }
        Call(cardUI, "OnCardSelected", pick);
    }

    static void PickReward()
    {
        var boss = BossRewardManager.Instance;
        if (boss == null || !boss.IsPending) { RM.ContinueAfterRoundChoice(); return; }
        var offer = boss.Offer.ToList();
        string offered = string.Join(" ", offer.Select(r => r.id));
        if (offer.Count == 0) { boss.ContinueWithoutReward(); rewardCsv.AppendLine(Row(offered, "-")); return; }
        // Almayan eş: aynı teklif, kırılma ödülü seçenek dışı (teklifte yalnız o kaldıysa mecburen alınır ve özet satırında görünür).
        if (config.DeclineBreakthroughs && offer.Any(r => !r.IsBreakthrough)) offer = offer.Where(r => !r.IsBreakthrough).ToList();
        BossRewardSO pick = null;
        if (policy.Naive) pick = offer[naive.Next(offer.Count)];
        else
        {
            string[] order = policy.FixedRewardOrder || policy.Early(RM.CurrentRound) ? policy.RewardOrder : Balanced().RewardOrder;
            foreach (string id in order) { pick = offer.FirstOrDefault(r => r.id == id); if (pick != null) break; }
            pick ??= offer[0];
        }
        if (!boss.Choose(pick)) throw new Exception("reward not taken: " + pick.id);
        if (pick.IsBreakthrough) bot.Breakthroughs.Add(pick.id + "@R" + RM.CurrentRound);
        rewardCsv.AppendLine(Row(offered, pick.id));
        string Row(string a, string b) => $"{runIndex + 1},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{RM.CurrentRound},{a},{b}";
    }

    // ---------------------------------------------------------------- bot (hasat + ölçüm)
    sealed class RunBot : MonoBehaviour
    {
        public const string Header = "run,profile,policy,farmer,scythe,seed,label,round,outcome,boss,kills,direct,explosion,tornado,boomerang,electric," +
            "kCommon,kUncommon,kRare,kEpic,kLegendary,score,segScore,segTarget,bossScore,bossTarget,gold,iron,stone,bankGold,bankIron,bankStone," +
            "xp,level,cards,damage,directMult,behaviorMult,interval,radius,reachCells,crit,critMult,attacks,hitsPerAttack," +
            "htkCommon,htkUncommon,htkRare,htkEpic,htkLegendary,osCommon,osUncommon,osRare,osEpic,osLegendary,ttkCommon,ttkUncommon,ttkRare,ttkEpic,ttkLegendary," +
            "planters,points,fill,minFill,spawnInterval,capacity,tiersBought,treeTiers,plantersBought,rewards,best5s,emptyShare,duration," +
            // Bölüm 3.6 (sona eklendi: eski çözümleyiciler etkilenmez). levels / choices / card* run başından beri toplamdır; diğerleri o round.
            "levels,choices,cardTiles,cardUpgrades,cardBase,firstBehavior,firstOwn,firstTrigger,breakthrough," +
            "echoSched,echoExec,echoDrop,echoHits,echoKills,rhythmCharges,rhythmAttacks,skipElectricVisual,skipExplosionVisual,skipBoomerang,endFill,scorePerSec,killsPerSec";

        public int TiersBought, PlantersBought, CardScreens;
        // run boyunca: alınan kartlar (tile / yükseltme / temel güç), ilk davranış kartı, ilk gerçek tetik, kırılma ödülleri
        public int CardTiles, CardUpgrades, CardBase, FirstBehavior, FirstOwn, FirstTrigger;
        public readonly List<string> Breakthroughs = new();
        public string LabLine = "";
        int triggers0, echoKillsRound; double lastFill;
        readonly int[] echo0 = new int[4]; int charges0, strong0, skipElectric0, skipExplosion0, skipBoomerang0;
        static int Triggers() => HarvestBehaviorStats.Triggered(DamageType.Explosion) + HarvestBehaviorStats.Triggered(DamageType.Electric);
        static int EchoSum(Func<BehaviorEchoes, DamageType, int> f) =>
            BehaviorEchoes.Instance != null ? f(BehaviorEchoes.Instance, DamageType.Explosion) + f(BehaviorEchoes.Instance, DamageType.Electric) : 0;
        static int[] EchoNow() => new[] { EchoSum((e, t) => e.Scheduled(t)), EchoSum((e, t) => e.Executed(t)), EchoSum((e, t) => e.Dropped(t)), EchoSum((e, t) => e.Hits(t)) };
        Policy policy; RunConfig config; int run; StringBuilder csv;
        MethodInfo attack; PlayerController player; float timer;
        List<Vector3> aims; List<List<GridObject>> aimCells; float cachedRadius = -1f; int cachedSize = -1; int[] lastUsed;
        readonly System.Random random = new System.Random(4711);

        // round sayaçları
        readonly int[] kills = new int[5], byRarity = new int[5];
        readonly double[] hitSum = new double[5], ttkSum = new double[5]; readonly int[] tracked = new int[5], oneShots = new int[5], fresh = new int[5];
        readonly Dictionary<PlantHealth, (uint life, int hits, float first)> hitLog = new();
        readonly Dictionary<ResourceType, int> income = new(), lastIncome = new();
        int attacks, hitTargets, fillSamples, emptySamples; double fillSum, minFill; long score0, segScore0; double xp0;
        readonly Queue<float> killTimes = new(); int best5s;
        int lastRound; bool roundOpen; string outcome = "";

        public float LastIncome(ResourceType t) => lastIncome.TryGetValue(t, out int v) ? v : 0;
        public float LastFill { get; private set; } = -1f;   // son round'un ortalama tarla doluluğu (ölçüm yoksa -1)

        public void Init(Policy p, RunConfig c, int runIndex, StringBuilder output)
        {
            policy = p; config = c; run = runIndex; csv = output;
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            PlantHealth.AnyHarvested += OnHarvest;
            ResourceManager.Instance.OnHarvestResourceAdded += OnIncome;
            RoundManager.Instance.OnRoundChanged += OnRoundStart;
            RoundManager.Instance.OnRoundEnded += OnRoundEnd;
            triggers0 = Triggers();   // sayaç statiktir (sahne değişiminde sıfırlanmaz): run başındaki değer taban alınır
        }

        void OnDestroy()
        {
            PlantHealth.AnyHarvested -= OnHarvest;
            if (ResourceManager.Instance != null) ResourceManager.Instance.OnHarvestResourceAdded -= OnIncome;
            if (RoundManager.Instance != null) { RoundManager.Instance.OnRoundChanged -= OnRoundStart; RoundManager.Instance.OnRoundEnded -= OnRoundEnd; }
        }

        void OnIncome(ResourceType type, int amount, Vector3 _) { income.TryGetValue(type, out int v); income[type] = v + amount; }

        void OnRoundStart(int round)
        {
            Array.Clear(kills, 0, 5); Array.Clear(byRarity, 0, 5); Array.Clear(hitSum, 0, 5); Array.Clear(ttkSum, 0, 5);
            Array.Clear(tracked, 0, 5); Array.Clear(oneShots, 0, 5); Array.Clear(fresh, 0, 5);
            hitLog.Clear(); income.Clear(); attacks = hitTargets = fillSamples = emptySamples = 0; fillSum = 0; minFill = 1; timer = 0;
            killTimes.Clear(); best5s = 0; CardScreens = 0;
            score0 = HarvestScoreManager.Instance.TotalScore;
            if ((round - 1) % RoundManager.Instance.QuotaSegmentRounds == 0) segScore0 = score0;
            xp0 = ProgressionManager.Instance.TotalXPEarned;
            lastRound = round; roundOpen = true; aims = null;
            var now = EchoNow(); for (int i = 0; i < 4; i++) echo0[i] = now[i];
            echoKillsRound = 0; lastFill = 0;
            charges0 = RunPower.Rhythm.Charges; strong0 = RunPower.Rhythm.EmpoweredAttacks;
            skipElectric0 = HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricVisuals : 0;
            skipExplosion0 = VFXManager.Instance != null ? VFXManager.Instance.ExplosionVisualsSkipped : 0;
            skipBoomerang0 = HarvestBehaviorStats.Skipped(DamageType.Boomerang);
        }

        void OnHarvest(PlantHealth h)
        {
            if (!roundOpen || h.Data == null) return;
            kills[(int)h.KilledBy]++;
            if (BehaviorEchoes.IsExecuting) echoKillsRound++;   // artçı / ikinci dalganın gerçek hasadı
            int r = (int)h.Data.rarity; byRarity[r]++;
            float now = Time.time;
            killTimes.Enqueue(now);
            while (killTimes.Count > 0 && now - killTimes.Peek() > 5f) killTimes.Dequeue();
            best5s = Math.Max(best5s, killTimes.Count);
            if (hitLog.TryGetValue(h, out var log) && log.life == h.LifetimeVersion)
            {
                // Doğrudan vuruşla ölen bitkide: kaç doğrudan vuruş aldı ve ilk vuruştan ölüme kaç saniye geçti.
                if (h.KilledBy == DamageType.Direct)
                {
                    tracked[r]++; hitSum[r] += log.hits; ttkSum[r] += now - log.first;
                }
                hitLog.Remove(h);
            }
        }

        void OnRoundEnd()
        {
            if (!roundOpen) return;
            roundOpen = false;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType))) lastIncome[t] = income.TryGetValue(t, out int v) ? v : 0;
            LastFill = fillSamples > 0 ? (float)(fillSum / fillSamples) : -1f;
            if (FirstTrigger == 0 && Triggers() > triggers0) FirstTrigger = lastRound;
            WriteRow("");
        }

        public void Close(string result)
        {
            // Son round'un satırı zaten yazıldı; sonucu ayrı bir satır olarak ekle (round = bittiği round).
            outcome = result;
            csv.AppendLine($"{run + 1},{config.Profile},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{RoundManager.Instance.CurrentRound},{result}");
        }

        static string N(double v, string f = "0.##") => double.IsNaN(v) ? "" : v.ToString(f, CultureInfo.InvariantCulture);

        void WriteRow(string result)
        {
            var rm = RoundManager.Instance; var stats = StatManager.Instance; var boss = BossRewardManager.Instance;
            long score = HarvestScoreManager.Instance.TotalScore;
            float radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player);
            float interval = Mathf.Max(stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / rm.TempoMultiplier;
            var planters = FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Where(p => p.OccupiedGrids.Count > 0).ToList();
            int points = spawners.Count;
            double spawnAvg = planters.Count > 0 ? planters.Average(p => Mathf.Max(StatCalculator.MinimumSpawnInterval, p.GetFinalStat(StatType.PlantSpawnRate)) / rm.TempoMultiplier) : 0;
            double duration = rm.EffectiveRoundDuration;
            double capacity = spawnAvg > 0 ? points * duration / spawnAvg : 0;
            int total = kills.Sum();
            string events = SegmentEventDirector.Instance != null && SegmentEventDirector.Instance.Active != null ? SegmentEventDirector.Instance.Active.Data.displayName : "";
            var tree = SkillTreeManager.Instance;
            string rewards = boss != null ? string.Join(" ", boss.Taken.Select(r => r.id + "x" + boss.Stacks(r))) : "";
            var sb = new StringBuilder();
            sb.Append($"{run + 1},{config.Profile},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{lastRound},{result},{events},");
            sb.Append($"{total},{kills[0]},{kills[1]},{kills[2]},{kills[3]},{kills[4]},{byRarity[0]},{byRarity[1]},{byRarity[2]},{byRarity[3]},{byRarity[4]},");
            sb.Append($"{score - score0},{score - segScore0},{rm.QuotaTarget},{(rm.IsBossRound(lastRound) ? rm.LastBossScore : 0)},{(rm.IsBossRound(lastRound) ? rm.BossTarget : 0)},");
            sb.Append($"{V(ResourceType.Gold)},{V(ResourceType.Iron)},{V(ResourceType.Stone)},{ResourceManager.Instance.GetResourceAmount(ResourceType.Gold)},{ResourceManager.Instance.GetResourceAmount(ResourceType.Iron)},{ResourceManager.Instance.GetResourceAmount(ResourceType.Stone)},");
            sb.Append($"{N(ProgressionManager.Instance.TotalXPEarned - xp0, "0")},{ProgressionManager.Instance.CurrentLevel},{CardScreensPending()},");
            sb.Append($"{N(stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player))},{N(SpecializationManager.DirectMultiplier * StartLoadoutManager.DirectDamageMultiplier * BossRewardManager.DirectDamageMultiplier, "0.###")},{N(BossRewardManager.BehaviorDamageMultiplier, "0.###")},");
            sb.Append($"{N(interval, "0.###")},{N(radius, "0.###")},{BestReach(radius)},{N(stats.GetFinalStat(StatType.CritChance, StatTarget.Player), "0.###")},{N(stats.GetFinalStat(StatType.CritMultiplier, StatTarget.Player), "0.###")},");
            sb.Append($"{attacks},{N(attacks > 0 ? hitTargets / (double)attacks : 0)},");
            for (int r = 0; r < 5; r++) sb.Append(N(tracked[r] > 0 ? hitSum[r] / tracked[r] : double.NaN) + ",");
            for (int r = 0; r < 5; r++) sb.Append(N(fresh[r] > 0 ? 100.0 * oneShots[r] / fresh[r] : double.NaN, "0") + ",");
            for (int r = 0; r < 5; r++) sb.Append(N(tracked[r] > 0 ? ttkSum[r] / tracked[r] : double.NaN) + ",");
            sb.Append($"{planters.Count},{points},{N(fillSamples > 0 ? fillSum / fillSamples : 0, "0.###")},{N(minFill, "0.###")},{N(spawnAvg, "0.###")},{N(capacity, "0")},");
            sb.Append($"{TiersBought},{tree.AllNodes.Sum(n => tree.GetCurrentLevel(n))},{PlantersBought},{rewards},{best5s},{N(fillSamples > 0 ? emptySamples / (double)fillSamples : 0, "0.###")},{N(duration, "0")},");
            var echo = EchoNow();
            int skipElectric = (HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricVisuals : 0) - skipElectric0;
            int skipExplosion = (VFXManager.Instance != null ? VFXManager.Instance.ExplosionVisualsSkipped : 0) - skipExplosion0;
            sb.Append($"{rm.LevelsGained},{rm.CardChoicesGranted},{CardTiles},{CardUpgrades},{CardBase},{FirstBehavior},{FirstOwn},{FirstTrigger},{string.Join(" ", Breakthroughs)},");
            sb.Append($"{echo[0] - echo0[0]},{echo[1] - echo0[1]},{echo[2] - echo0[2]},{echo[3] - echo0[3]},{echoKillsRound},{RunPower.Rhythm.Charges - charges0},{RunPower.Rhythm.EmpoweredAttacks - strong0},");
            sb.Append($"{skipElectric},{skipExplosion},{HarvestBehaviorStats.Skipped(DamageType.Boomerang) - skipBoomerang0},{N(lastFill, "0.###")},{N(duration > 0 ? (score - score0) / duration : 0, "0.###")},{N(duration > 0 ? total / duration : 0, "0.###")}");
            csv.AppendLine(sb.ToString());
            LabLine = $"hasat {total} (doğrudan {kills[0]} · patlama {kills[1]} · elektrik {kills[4]} · kasırga {kills[2]} · bumerang {kills[3]}; yankı hasadı {echoKillsRound}) | skor {score - score0} | " +
                      $"vuruş {attacks} · vuruş başına hedef {N(attacks > 0 ? hitTargets / (double)attacks : 0)} | doluluk {N(fillSamples > 0 ? fillSum / fillSamples : 0, "0.###")} (round sonu {N(lastFill, "0.###")}) | " +
                      $"yankı {echo[1] - echo0[1]} uygulandı / {echo[2] - echo0[2]} düştü | ritim hakkı {RunPower.Rhythm.Charges - charges0}";
            TiersBought = 0; PlantersBought = 0;
            int V(ResourceType t) => income.TryGetValue(t, out int v) ? v : 0;
        }

        // Round sonunda bekleyen kart ekranı sayısı (round içinde kazanılan level sayısı).
        static int CardScreensPending() => F<int>(RoundManager.Instance, "pendingCardSelections");

        // The same three canonical aim samples and contact rule as gameplay/Sis.
        static int BestReach(float radius)
        {
            return HarvestArea.BestCells(radius, 2f);
        }

        List<PlantSpawner> spawners = new();

        void BuildAims(GridSystem grid, float radius)
        {
            int w = GridManager.Instance.GetWidth(), d = GridManager.Instance.GetHeight();
            int open = 0;
            for (int x = 0; x < w; x++) for (int z = 0; z < d; z++) { var g = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && !g.IsLocked) open++; }
            if (aims == null || open != cachedSize)
            {
                cachedSize = open; cachedRadius = -1f;
                aims = new List<Vector3>();
                Vector3? At(int x, int z) { var g = x < w && z < d ? grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached() : null; return g != null ? g.transform.position : null; }
                for (int x = 0; x < w; x++) for (int z = 0; z < d; z++)
                {
                    var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                    if (ground == null || ground.IsLocked) continue;
                    Vector3 c = ground.transform.position;
                    aims.Add(c);
                    if (policy.Naive) continue;   // yeni oyuncu yalnız hücre merkezine nişan alır
                    foreach (var n in new[] { At(x + 1, z), At(x, z + 1), At(x + 1, z + 1) }) if (n.HasValue) aims.Add((c + n.Value) / 2f);
                }
                lastUsed = new int[aims.Count]; for (int a = 0; a < lastUsed.Length; a++) lastUsed[a] = -1;
                spawners = FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
            }
            if (aims.Count != cachedAims) { cachedAims = aims.Count; cellCache.Clear(); cachedRadius = -1f; }
            if (radius != cachedRadius)
            {
                cachedRadius = radius;
                // Hasat Ritmi yarıçapı iki değer arasında gidip gelir: ikisi de saklanır.
                if (!cellCache.TryGetValue(radius, out aimCells)) cellCache[radius] = aimCells = aims.Select(a => grid.GetGridObjectsInRadius(a, radius)).ToList();
            }
        }
        readonly Dictionary<float, List<List<GridObject>>> cellCache = new(); int cachedAims = -1;

        static readonly double[] Value = { 1, 3, 10, 30, 100 };
        int spawnerRefresh;

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round || !roundOpen) return;
            var rm = RoundManager.Instance; var stats = StatManager.Instance;
            var grid = GridManager.Instance.GetGridSystem();
            // Nişan, sıradaki saldırının gerçek temas alanına göre seçilir (hazır Hasat Ritmi hakkı alanı büyütür; imleç halkası da bunu gösterir).
            float radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player) * RunPower.Rhythm.Next().RadiusMultiplier;
            BuildAims(grid, radius);
            if (spawnerRefresh++ % 60 == 0) spawners = FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
            int filled = 0; foreach (var s in spawners) if (s != null && s.GridObject.HasPlantObject()) filled++;
            double fill = spawners.Count > 0 ? filled / (double)spawners.Count : 0;
            fillSum += fill; fillSamples++; minFill = Math.Min(minFill, fill); if (fill < .15) emptySamples++;
            lastFill = fill;

            float interval = Mathf.Max(stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / rm.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;

            int bestAim = -1; double bestScore = 0;
            if (policy.Naive)
            {
                // Yeni oyuncu: canlı bitkisi olan rastgele bir hücreye nişan alır (en iyi konumu aramaz).
                var live = new List<int>();
                for (int a = 0; a < aims.Count; a++) if (Living(aimCells[a]) > 0) live.Add(a);
                if (live.Count > 0) { bestAim = live[random.Next(live.Count)]; bestScore = 1; }
            }
            else
                for (int a = 0; a < aims.Count; a++)
                {
                    double s = 0;
                    foreach (var g in aimCells[a])
                    {
                        var p = g.GetPlantObject();
                        if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) s += policy.ValueAim && h.Data != null ? Value[(int)h.Data.rarity] : 1;
                    }
                    if (s > bestScore + 1e-9 || (s > 0 && Math.Abs(s - bestScore) <= 1e-9 && lastUsed[a] < lastUsed[bestAim])) { bestScore = s; bestAim = a; }
                }
            if (bestAim < 0 || bestScore <= 0) return;
            lastUsed[bestAim] = attacks;
            float now = Time.time;
            foreach (var g in aimCells[bestAim])
            {
                var p = g.GetPlantObject();
                if (p == null || !p.TryGetComponent(out PlantHealth h) || h.IsDead || h.Data == null) continue;
                hitTargets++;
                if (!hitLog.TryGetValue(h, out var log) || log.life != h.LifetimeVersion)
                {
                    bool full = h.CurrentHealth >= h.MaxHealth;
                    log = (h.LifetimeVersion, 0, now);
                    if (full) fresh[(int)h.Data.rarity]++;
                    hitLog[h] = (log.life, 1, now);
                    pendingFresh.Add((h, h.LifetimeVersion, (int)h.Data.rarity, full));
                }
                else hitLog[h] = (log.life, log.hits + 1, log.first);
            }
            attack.Invoke(player, new object[] { aims[bestAim] });
            attacks++;
            // Tek vuruş: can dolu iken ilk doğrudan vuruşta ölen bitki.
            foreach (var (h, life, rarity, full) in pendingFresh)
                if (full && (h == null || h.IsDead || h.LifetimeVersion != life)) oneShots[rarity]++;
            pendingFresh.Clear();
        }

        readonly List<(PlantHealth h, uint life, int rarity, bool full)> pendingFresh = new();

        static int Living(List<GridObject> cells)
        {
            int n = 0;
            foreach (var g in cells) { var p = g.GetPlantObject(); if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) n++; }
            return n;
        }
    }
}
