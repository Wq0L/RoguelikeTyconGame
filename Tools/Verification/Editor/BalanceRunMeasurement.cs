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
        // Bölüm 3.7.7: temel güç kartı tercihi (kartın ilk stat'ı; yoksa 1 — eski politikalarda boş: yalnız nadirlik).
        public Dictionary<StatType, float> BaseWeight = new();
        // XP kolu (RunConfig.XpArm): bu round'a kadar (dahil) Water (XP) kartının ağırlığı XpCardWeight olur ve Bilgi Filizi öne alınır.
        public int XpEarlyUntil; public float XpCardWeight = 1f;
        public bool TakesFirstBehavior;                       // ilk patlama / elektrik kartını teklif edildiği anda alır (erken davranış erişimi)
        // Bölüm 3.7.8 ikinci nişan politikası (RunConfig.Aim = "davranis"): bitkinin nişan değeri, saksısının davranış şansıyla büyür
        // (değer × (1 + 2 × min(1, patlama + elektrik + kasırga + bumerang şansı))). Bot politikasıdır; insan davranışı değildir.
        public bool BehaviorAim;
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
    // Bölüm 3.7.7 devam yönleri. İkisi de ilk davranış kartını teklif edilince alır (erken davranış erişimi ölçülür); temel güç
    // kartlarında kendi yönünün stat'ını öne alır. XP tercihi yönde yoktur: XP kolu (RunConfig.XpArm) ekler ya da kapatır.
    static Policy DamageArea() => new Policy
    {
        Name = "Hasar-alan", TakesFirstBehavior = true,
        Bias = { [Cat.Damage] = -4, [Cat.Area] = -4, [Cat.Crit] = -2, [Cat.Speed] = 1, [Cat.Rarity] = 3, [Cat.Cards] = 8, [Cat.BehaviorUnlock] = 6, [Cat.Score] = 2 },
        CardWeight = { [TileModifierType.Damage] = 1.8f, [TileModifierType.Fertile] = 1.3f, [TileModifierType.Energy] = 1.2f },
        BaseWeight = { [StatType.HarvestDamage] = 2f, [StatType.AttackSpeed] = 1.5f },
        RewardOrder = new[] { "keskin_bicak", "genis_savurus", "canavar_kesimi", "hizli_bilek", "firtina_bilegi", "kritik_goz", "agir_darbe", "bereketli_toprak", "nadir_tohum", "altin_hedef", "hasat_ritmi", "kivilcim", "yikim_gucu", "bilgi_filizi" },
        FixedRewardOrder = true,
    };
    static Policy BehaviorDirection()
    {
        Policy p = Behavior();
        p.Name = "Davranış yönü"; p.TakesFirstBehavior = true;
        p.BaseWeight = new Dictionary<StatType, float> { [StatType.HarvestDamage] = 2f, [StatType.PlantSpawnRate] = 1.5f };
        p.RewardOrder = new[] { "zincir_hasat", "kivilcim", "yikim_gucu", "artci_patlama", "cifte_akim", "hizli_bilek", "firtina_bilegi", "bereketli_toprak", "genis_savurus", "keskin_bicak", "nadir_tohum", "canavar_kesimi", "kritik_goz", "agir_darbe", "altin_hedef", "bilgi_filizi" };
        p.FixedRewardOrder = true;
        return p;
    }
    // XP stresi: kontrolsüz geri beslemeyi yakalamak için. XP veren her şeyi önce alır: XP düğümleri, Water kartı, Bilgi Filizi,
    // seçim hakkı veren Bereketli Öğrenim ve temel güçte teklif edilen her XP kartı. Gerçekçi bir oyuncu değildir.
    static Policy XpStress()
    {
        Policy p = Behavior();
        p.Name = "XP stresi"; p.TakesFirstBehavior = true;
        p.Bias[Cat.Xp] = -40; p.Bias[Cat.Cards] = -10;
        p.CardWeight[TileModifierType.Water] = 6f;
        p.BaseWeight = new Dictionary<StatType, float> { [StatType.XPGainMultiplier] = 1000f, [StatType.PlantSpawnRate] = 1.5f, [StatType.AttackSpeed] = 1.5f };
        p.RewardOrder = new[] { "bilgi_filizi", "bereketli_ogrenim", "zincir_hasat", "kivilcim", "yikim_gucu", "hizli_bilek", "firtina_bilegi", "bereketli_toprak", "genis_savurus", "keskin_bicak", "nadir_tohum", "canavar_kesimi", "kritik_goz", "agir_darbe", "altin_hedef" };
        p.FixedRewardOrder = true;
        return p;
    }
    // İkinci stres tanımı (pilot sonrası eklendi): ilk tanım ağaçta XP'yi öne alınca build zayıf kaldı ve temel güç evresine hiç
    // ulaşmadı (geri besleme rejimi sınanmadı). Bu tanım, temel güç evresine ulaşan "Davranış-rezonans" build'ini aynen kullanır
    // (ağaç ve tile tercihleri aynı) ve yalnız şunları ekler: temel güçte teklif edilen her XP kartını alır; Bilgi Filizi'ni,
    // Bereketli Öğrenim'i (level başına seçim +1) ve Zincir Hasat'ı teklif edilince alır.
    static Policy BaseXpStress()
    {
        Policy p = Behavior();
        p.Name = "Temel güç XP stresi";
        p.BaseWeight = new Dictionary<StatType, float> { [StatType.XPGainMultiplier] = 1000f };
        p.RewardOrder = new[] { "bilgi_filizi", "bereketli_ogrenim", "zincir_hasat" }.Concat(p.RewardOrder.Where(r => r != "bilgi_filizi")).ToArray();
        p.FixedRewardOrder = true;
        return p;
    }
    // Bölüm 3.7.8 — düşük uyumlu tercih: davranış tile'larını ve rezonans yerleşimini seçer (Davranış-rezonans'ın ağacı ve kartları)
    // ama ödüllerde kendi davranışını beslemeyen doğrudan vuruş / kritik ödüllerini öne alır; kritik düğümlerini de geç alır
    // (yönün ağaç tercihi), yani aldığı ödül ne tile'larına ne ağacına uyar. Kırılma ödülleri ve zincir sırasında yoktur.
    static Policy Mismatch()
    {
        Policy p = Behavior();
        p.Name = "Uyumsuz"; p.TakesFirstBehavior = true;
        p.RewardOrder = new[] { "kritik_goz", "agir_darbe", "altin_hedef", "keskin_bicak", "canavar_kesimi", "nadir_tohum", "bilgi_filizi", "bereketli_toprak", "genis_savurus", "hizli_bilek", "firtina_bilegi", "kivilcim", "yikim_gucu" };
        p.FixedRewardOrder = true;
        return p;
    }
    static Policy Balanced() => new Policy { Name = "Dengeli", RewardOrder = new[] { "hizli_bilek", "keskin_bicak", "genis_savurus", "bereketli_toprak", "nadir_tohum", "firtina_bilegi", "canavar_kesimi", "kritik_goz", "bilgi_filizi", "kivilcim", "yikim_gucu", "agir_darbe", "altin_hedef" } };
    static Policy Newbie() => new Policy { Name = "Yeni", Naive = true, PlanterShare = .5f };

    static Policy ByName(string name)
    {
        foreach (var p in new[] { Speed(), Damage(), Behavior(), Economy(), Balanced(), Newbie(), ExplosionPath(), ElectricPath(), RhythmPath(), DamageArea(), BehaviorDirection(), XpStress(), BaseXpStress(), Mismatch() }) if (p.Name == name) return p;
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
        public string DeclineId;       // Bölüm 3.7.6: yalnız bu ödül alınmaz (teklifte başka seçenek varken); diğer kırılma ödülleri alınabilir
                                          // Kırılma ödülünü alan run ile ödülün alındığı round'a kadar birebir aynıdır ("almayan eş").
        public int ChoicesPerLevel;    // >0: profilin level başına seçim sayısı bu run için değiştirilir (ölçüm)
        public int[] LabRounds;        // bu round'ların başındaki durum kaydedilir; run bitince laboratuvar turları oynanır
        public string LabReward;       // laboratuvarın B kolunda eklenen kırılma ödülü (id)
        public int LabSynergyRound;    // >0: o round'un kaydı üstüne "güçlü sinerji" kurulumu da oynanır (A / B)
        public double WallLimitSec;    // Bölüm 3.7.6.1: >0: run bu kadar gerçek saniyede bitmezse "durduruldu (süre sınırı)" olarak kapanır
        // Bölüm 3.7.7 — eş bütçeli XP kolları (aynı seed, aynı başlangıç bütçesi; bedava kart ya da kaynak yok):
        //   "yok"   : hiçbir XP düğümü almaz (o kaynak yön politikasının diğer düğümlerine ve saksıya gider); Water (XP) kartını, Bilgi
        //             Filizi'ni ve temel güç XP kartını yalnız başka seçenek yoksa alır.
        //   "erken" : Hasat Deneyimi I ailesini alabildiği anda alır (ön koşuluyla); XpEarlyRound'a kadar Water kartını öne alır
        //             (ağırlık ×2) ve Bilgi Filizi'ni teklif edilince alır. Sonra XP'ye özel tercih yok: yön politikası.
        //   "vadesinde" (A/B sonuçlarından sonra eklendi; "erken" kolu XP'yi saksı ve grid'in önüne aldığı için ayrı bir ölçü):
        //             XP düğümlerini ağaçta yazılı vadelerinde alır (Hasat Deneyimi I: R3–16, II: R25–42; diğer vadesi gelmiş
        //             düğümlerle aynı sırada, öne alınmaz). Water kartı, Bilgi Filizi ve temel güç XP kartı yönün kendi tercihiyle.
        public string XpArm;
        public float TailGrowth = -1f; // ≥0: bu run'da tablo sonrası büyüme katsayısı (ölçüm; profilin denge seti kopyalanır)
        public int AdditiveXp = -1;    // 0 / 1: bu run'da temel güç XP kartlarının toplanması kapalı / açık (ölçüm: iki kuralın ayrı etkisi)
        // Bölüm 3.7.8
        public string Aim;             // "davranis": ikinci nişan politikası (Policy.BehaviorAim). null: politikanın kendi nişanı (en kalabalık hedef)
        public bool NoTargets;         // kalibrasyon: bütün kota ve boss hedefleri 1 (run hedeften elenmez; hedef tabloları bu run'ların skorundan değerlendirilir)
        public float[] Hp;             // can eğrisi adayı: Common canı noktaları (round, can, round, can, …). Yalnız bu run; asset değişmez
        public long[] Quota, Boss;     // hedef tablosu adayı (dönem sırasıyla). Yalnız bu run
        public float[] ChainChance, ChainDamage;   // zincir çarpanı adayı (ek nesil 1, 2). Yalnız bu run; havuz ve ödül kopyalanır
        // Zincir laboratuvarı: B kolları (ad, şans, hasar). Boş: tek B kolu, ödül asset'inin kendi değerleri.
        public (string name, float[] chance, float[] damage)[] LabChain;
    }
    // Bölüm 3.7.8 zincir laboratuvarının B kolları (izinli aralığın uçları ve ayrı ayrı şans / hasar).
    static readonly (string, float[], float[])[] ChainArms =
    {
        ("mevcut", new[] { .75f, .50f }, new[] { .75f, .50f }),
        ("şans üst", new[] { 1f, .75f }, new[] { .75f, .50f }),
        ("hasar üst", new[] { .75f, .50f }, new[] { 1f, .75f }),
        ("ikisi üst", new[] { 1f, .75f }, new[] { 1f, .75f }),
    };
    // Bölüm 3.7.8, 1. ayar turu: can eğrisi adayları (round, Common canı). R1–10 çapaları Denge V1 ile aynı.
    static readonly (string, float[])[] HpCandidates =
    {
        ("HA", new float[] { 1, 20, 5, 25, 10, 31, 15, 43, 20, 62, 25, 88, 30, 125, 35, 176, 40, 247, 45, 345, 50, 480 }),
        ("HB", new float[] { 1, 20, 5, 25, 10, 31, 15, 46, 20, 73, 25, 112, 30, 170, 35, 255, 40, 380, 45, 560, 50, 820 }),
        ("HC", new float[] { 1, 20, 5, 25, 10, 31, 15, 49, 20, 78, 25, 125, 30, 200, 35, 310, 40, 475, 45, 720, 50, 1080 }),
    };
    // 2. ayar turu: HA ile HB'nin orta eğrisi (geometrik orta, tam sayıya yuvarlanmış).
    static readonly float[] HpMid = { 1, 20, 5, 25, 10, 31, 15, 44, 20, 67, 25, 99, 30, 146, 35, 212, 40, 306, 45, 440, 50, 627 };
    const int XpEarlyRound = 20;
    static readonly string[] XpFamilyFirst = { "Düzenli Üretim - 1@1", "Hasat Deneyimi I - 1", "Hasat Deneyimi I - 2", "Hasat Deneyimi I - 3", "Hasat Deneyimi I - 4" };

    static void ApplyXpArm(Policy p, RunConfig c)
    {
        if (c.XpArm == null) return;
        if (c.XpArm == "yok")
        {
            p.Bias[Cat.Xp] = 9999f;
            p.CardWeight[TileModifierType.Water] = .05f;
            p.BaseWeight[StatType.XPGainMultiplier] = .05f;
            p.RewardOrder = p.RewardOrder.Where(r => r != "bilgi_filizi").Concat(new[] { "bilgi_filizi" }).ToArray();
        }
        else if (c.XpArm == "erken")
        {
            p.FirstBuys = XpFamilyFirst;
            p.XpEarlyUntil = XpEarlyRound; p.XpCardWeight = 2f;
        }
        else if (c.XpArm == "vadesinde") p.Bias[Cat.Xp] = 0f;
        else throw new Exception("unknown XP arm " + c.XpArm);
    }

    static List<RunConfig> BuildSet(string set)
    {
        var list = new List<RunConfig>();
        void Add(string policy, string farmer, string scythe, int seeds, int maxRound = 50, string profile = "Run50_DengeV1", string label = "", string[] first = null, bool none = false,
                 string boss = null, bool noRewards = false, string[] rewards = null, Cat[] never = null,
                 bool noBreak = false, int choices = 0, int[] lab = null, string labReward = null, int synergy = 0, bool decline = false, string declineId = null, int firstSeed = 100,
                 double wallLimit = 0, string xp = null, float tail = -1f, int additive = -1,
                 string aim = null, bool noTargets = false, float[] hp = null, long[] quota = null, long[] bossTargets = null,
                 float[] chainChance = null, float[] chainDamage = null, (string, float[], float[])[] labChain = null)
        {
            for (int s = 0; s < seeds; s++)
                list.Add(new RunConfig { Profile = profile, Farmer = farmer, Scythe = scythe, PolicyName = policy, Seed = firstSeed + s, MaxRound = maxRound, Label = label, FirstBuys = first, NoPurchases = none,
                                         BossOnly = boss, NoRewards = noRewards, RewardOrder = rewards, Never = never,
                                         NoBreakthroughs = noBreak, ChoicesPerLevel = choices, LabRounds = lab, LabReward = labReward, LabSynergyRound = synergy, DeclineBreakthroughs = decline, DeclineId = declineId,
                                         WallLimitSec = wallLimit, XpArm = xp, TailGrowth = tail, AdditiveXp = additive,
                                         Aim = aim, NoTargets = noTargets, Hp = hp, Quota = quota, Boss = bossTargets,
                                         ChainChance = chainChance, ChainDamage = chainDamage, LabChain = labChain });
        }
        const string K = "Run50_KirilmaV1";
        const string X = "Run50_XPV1";
        const string A8 = "Run50_AlphaDengeV1";
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
            // ---------------- Bölüm 3.7.2 ----------------
            case "takvimsmoke": // araç denemesi (denge ölçümü değil): açık boss takviminde dönem skoru ve boss sütunları doğru round'larda mı
                Add("Dengeli", "bahcivan", "standart", 1, 13, "Run50_TakvimV1", "takvim araç denemesi");
                break;
            // ---------------- Bölüm 3.7.5 ----------------
            case "k375tekrar": // tekrar kontrolü: Artçı Patlama'lı tam run aynı seed'le iki ayrı çalıştırmada aynı mı (yankı zamanlaması düzeltmesi)
                Add("Patlama yolu", "bahcivan", "standart", 5, 50, K, "tekrar kontrolü");
                break;
            case "k375patlama": // seçilen Artçı adayının birikimli run doğrulaması: aday / mevcut ödül / almayan eş (aynı seed'ler)
                Add("Patlama yolu", "bahcivan", "standart", 10, 50, "Run50_KirilmaErisimiV1", "aday");
                Add("Patlama yolu", "bahcivan", "standart", 10, 50, "Run50_BedelliOdullerV1", "mevcut");
                Add("Patlama yolu", "bahcivan", "standart", 10, 50, "Run50_KirilmaErisimiV1", "almayan eş", decline: true);
                break;
            case "k375elektrik": // seçilen Çifte Akım adayının birikimli run doğrulaması (havuzda kaldıysa)
                Add("Elektrik yolu", "bahcivan", "standart", 10, 50, "Run50_KirilmaErisimiV1", "aday");
                Add("Elektrik yolu", "bahcivan", "standart", 10, 50, "Run50_BedelliOdullerV1", "mevcut");
                Add("Elektrik yolu", "bahcivan", "standart", 10, 50, "Run50_KirilmaErisimiV1", "almayan eş", decline: true);
                break;
            case "k376zincir": // Bölüm 3.7.6: Zincir Hasat'ın birikimli run etkisi. aday: teklif edilince alır · eş: yalnız onu almaz (aynı seed)
                foreach (string policy in new[] { "Patlama yolu", "Elektrik yolu", "Davranış-rezonans" })
                {
                    Add(policy, "bahcivan", "standart", 10, 50, "Run50_ZincirV1", "aday", rewards: new[] { "zincir_hasat" });
                    Add(policy, "bahcivan", "standart", 10, 50, "Run50_ZincirV1", "almayan eş", declineId: "zincir_hasat");
                }
                break;
            case "k376zincirdevam": // k376zincir'in kalanı: run 52'den (Davranış-rezonans seed 101, almayan eş) sonrası. Seed 101'in iki kolu
                                    // geç round'da seviye patlamasına girdi (bkz. Docs/Bolum3-7-6-DavranisZinciri.md); onuncu çift seed 110.
                Add("Davranış-rezonans", "bahcivan", "standart", 8, 50, "Run50_ZincirV1", "almayan eş", declineId: "zincir_hasat", firstSeed: 102);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "aday", rewards: new[] { "zincir_hasat" }, firstSeed: 110);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "almayan eş", declineId: "zincir_hasat", firstSeed: 110);
                break;
            // ---------------- Bölüm 3.7.6.1 ----------------
            case "k3761": // sayısal güvenlik ve refactor karşılaştırması: P6'nın birikimli setinin (k376zincir + devam) aynısı, seed 101 Davranış-rezonans çifti hariç
                foreach (string policy in new[] { "Patlama yolu", "Elektrik yolu", "Davranış-rezonans" })
                    foreach (var (label, decline, order) in new[] { ("aday", (string)null, new[] { "zincir_hasat" }), ("almayan eş", "zincir_hasat", (string[])null) })
                    {
                        bool dr = policy == "Davranış-rezonans";
                        Add(policy, "bahcivan", "standart", dr ? 1 : 10, 50, "Run50_ZincirV1", label, rewards: order, declineId: decline);
                        if (dr) Add(policy, "bahcivan", "standart", 9, 50, "Run50_ZincirV1", label, rewards: order, declineId: decline, firstSeed: 102);
                    }
                break;
            case "k3761seed101": // kabul senaryosu: P6'da taşan seed 101 Davranış-rezonans çifti; run başına 25 dk gerçek süre sınırı
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "aday", rewards: new[] { "zincir_hasat" }, firstSeed: 101, wallLimit: 1500);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "almayan eş", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500);
                break;
            // ---------------- Bölüm 3.7.7 (P7) ----------------
            case "k377smoke": // araç denemesi
                Add("Hasar-alan", "bahcivan", "standart", 1, 8, X, "XP'siz", xp: "yok");
                Add("Hasar-alan", "bahcivan", "standart", 1, 8, X, "erken XP", xp: "erken");
                Add("XP stresi", "bahcivan", "standart", 1, 8, X, "stres");
                break;
            case "k377pilot": // tablo sonrası büyüme katsayısı: küçük eşleştirilmiş pilot. Yalnız level 140'ı aşan ya da aşabilecek run'lar
                              // katsayıdan etkilenir: P6'da taşan seed 101 (zincirsiz eş), level 141'e ulaşan seed 110 ve XP stresi.
                foreach (float s in new[] { .025f, .05f, .10f })
                {
                    string label = "s=" + s.ToString("0.###", CultureInfo.InvariantCulture);
                    Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, label, declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500, tail: s);
                    Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, label, rewards: new[] { "zincir_hasat" }, firstSeed: 110, wallLimit: 1500, tail: s);
                    Add("XP stresi", "bahcivan", "standart", 2, 50, X, label, firstSeed: 100, wallLimit: 1500, tail: s);
                }
                break;
            case "k377ab": // eş bütçeli XP yatırımı: iki devam yönü × iki kol × 10 seed
                foreach (string policy in new[] { "Hasar-alan", "Davranış yönü" })
                {
                    Add(policy, "bahcivan", "standart", 10, 50, X, "XP'siz", xp: "yok");
                    Add(policy, "bahcivan", "standart", 10, 50, X, "erken XP", xp: "erken");
                }
                break;
            case "k377ab2": // üçüncü kol: XP düğümleri yazılı vadesinde (aynı yönler ve seed'ler; k377ab'nin A koluyla karşılaştırılır)
                foreach (string policy in new[] { "Hasar-alan", "Davranış yönü" })
                    Add(policy, "bahcivan", "standart", 10, 50, X, "vadesinde XP", xp: "vadesinde");
                break;
            case "k377seed101": // kabul: P6'da taşan seed 101 Davranış-rezonans çifti, yeni profilde (politika ve seed P6 ile aynı)
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, "aday", rewards: new[] { "zincir_hasat" }, firstSeed: 101, wallLimit: 1500);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, "almayan eş", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500);
                break;
            case "k377stres": // ikinci stres tanımı (temel güç evresine ulaşan build + her XP kartı), yeni profil: seed 100–104 ve 110
                Add("Temel güç XP stresi", "bahcivan", "standart", 5, 50, X, "stres", wallLimit: 1500);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, X, "stres", firstSeed: 110, wallLimit: 1500);
                break;
            case "k377stresEski": // aynı stres politikası eski kuralla (Run50_ZincirV1): karşılaştırma. Run başına 15 dk gerçek süre sınırı.
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "stres (eski kural)", firstSeed: 101, wallLimit: 900);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, "Run50_ZincirV1", "stres (eski kural)", firstSeed: 110, wallLimit: 900);
                break;
            case "k377ayristirma": // iki kuralın ayrı etkisi, seed 101 zincirsiz kol: yalnız toplanan XP kartı (kuyruk sabit) / yalnız büyüyen kuyruk
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, "yalnız toplanan XP kartı (s=0)", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500, tail: 0f);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, X, "yalnız büyüyen kuyruk (s=0.05; kartlar çarpımsal)", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500, additive: 0);
                break;
            // ---------------- Bölüm 3.7.8 (P8) ----------------
            case "k378smoke": // araç denemesi: süre tablosu, ikinci nişan, zincir laboratuvarı (kısa)
                Add("Hasar-alan", "bahcivan", "standart", 1, 12, A8, "araç denemesi", xp: "vadesinde");
                Add("Davranış yönü", "bahcivan", "standart", 1, 24, A8, "araç denemesi · nişan 2", declineId: "zincir_hasat", aim: "davranis", noTargets: true,
                    lab: new[] { 24 }, labReward: "zincir_hasat", labChain: ChainArms);
                break;
            // Başlangıç ölçümü (yalnız süre tablosu; diğer her şey Run50_XPV1 ile aynı). Hedefsiz: hedef tabloları bu skorlardan değerlendirilir.
            case "k378tabanA": // dört build yolu
                Add("Hasar-alan", "bahcivan", "standart", 5, 50, A8, "taban", xp: "vadesinde", noTargets: true, wallLimit: 900);
                Add("Patlama yolu", "bahcivan", "standart", 3, 50, A8, "taban", declineId: "zincir_hasat", noTargets: true, wallLimit: 900);
                Add("Elektrik yolu", "bahcivan", "standart", 3, 50, A8, "taban", declineId: "zincir_hasat", noTargets: true, wallLimit: 900);
                Add("Davranış yönü", "bahcivan", "standart", 5, 50, A8, "taban", xp: "vadesinde", noTargets: true, wallLimit: 900);
                break;
            case "k378tabanB": // zinciri almayan eş + zincir laboratuvarı (R30, R40); ortalama / düşük uyumlu / acemi
                Add("Davranış yönü", "bahcivan", "standart", 5, 50, A8, "taban · almayan eş", xp: "vadesinde", declineId: "zincir_hasat", noTargets: true, wallLimit: 900,
                    lab: new[] { 30, 40 }, labReward: "zincir_hasat", labChain: ChainArms);
                Add("Dengeli", "bahcivan", "standart", 3, 50, A8, "taban", noTargets: true, wallLimit: 900);
                Add("Uyumsuz", "bahcivan", "standart", 3, 50, A8, "taban", noTargets: true, wallLimit: 900);
                Add("Yeni", "bahcivan", "standart", 3, 50, A8, "taban", noTargets: true, wallLimit: 900);
                break;
            case "k378tur1": // 1. ayar turu: can eğrisi adayları (HA / HB / HC) × dört politika × 2 seed, hedefsiz. Kontrol: başlangıç ölçümü.
                foreach (var (name, curve) in HpCandidates)
                {
                    Add("Hasar-alan", "bahcivan", "standart", 2, 50, A8, name, xp: "vadesinde", noTargets: true, wallLimit: 900, hp: curve);
                    Add("Patlama yolu", "bahcivan", "standart", 2, 50, A8, name, declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: curve);
                    Add("Davranış yönü", "bahcivan", "standart", 2, 50, A8, name, xp: "vadesinde", noTargets: true, wallLimit: 900, hp: curve);
                    Add("Dengeli", "bahcivan", "standart", 2, 50, A8, name, noTargets: true, wallLimit: 900, hp: curve);
                }
                break;
            case "k378acilis": // ilk üç boss (R3, R6, R10): tek bir zorunlu açılış var mı? Hedefsiz; R1–10 can çapaları adaylarda aynı.
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "hiçbir şey almaz", none: true, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "yalnız saksı", first: new[] { "-" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce hasar", first: new[] { "Keskin Başlangıç - 1", "Keskin Başlangıç - 2" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce hız", first: new[] { "Hızlı Eller - 1", "Hızlı Eller - 2" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce ekonomi", first: new[] { "Altın Hasat I - 1", "Altın Hasat I - 2" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce üretim", first: new[] { "Düzenli Üretim - 1", "Grid Genişleme I" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce XP", first: new[] { "Düzenli Üretim - 1@1", "Hasat Deneyimi I - 1", "Hasat Deneyimi I - 2" }, noTargets: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "politika (açılış yok)", noTargets: true);
                Add("Yeni", "bahcivan", "standart", 3, 10, A8, "acemi", noTargets: true);
                break;
            // 2. ayar turu (hedefsiz): can eğrisi HA ve HM (HA ile HB'nin orta eğrisi). Her kolda altı politika + zinciri almayan eş ve
            // zincir laboratuvarı (R30 / R40). HA kolunda 1. turun seed 100–101 run'ları yeniden oynanmaz (aynı aday, aynı kod).
            case "k378tur2A":
                Add("Uyumsuz", "bahcivan", "standart", 5, 50, A8, "HA", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2);
                Add("Elektrik yolu", "bahcivan", "standart", 3, 50, A8, "HA", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2);
                Add("Hasar-alan", "bahcivan", "standart", 1, 50, A8, "HA", xp: "vadesinde", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2, firstSeed: 102);
                Add("Patlama yolu", "bahcivan", "standart", 1, 50, A8, "HA", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2, firstSeed: 102);
                Add("Davranış yönü", "bahcivan", "standart", 1, 50, A8, "HA", xp: "vadesinde", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2, firstSeed: 102);
                Add("Dengeli", "bahcivan", "standart", 1, 50, A8, "HA", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2, firstSeed: 102);
                Add("Davranış yönü", "bahcivan", "standart", 3, 50, A8, "HA · almayan eş", xp: "vadesinde", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpCandidates[0].Item2,
                    lab: new[] { 30, 40 }, labReward: "zincir_hasat", labChain: ChainArms);
                break;
            case "k378tur2B":
                Add("Uyumsuz", "bahcivan", "standart", 5, 50, A8, "HM", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Elektrik yolu", "bahcivan", "standart", 3, 50, A8, "HM", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Hasar-alan", "bahcivan", "standart", 3, 50, A8, "HM", xp: "vadesinde", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Patlama yolu", "bahcivan", "standart", 3, 50, A8, "HM", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Davranış yönü", "bahcivan", "standart", 3, 50, A8, "HM", xp: "vadesinde", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Dengeli", "bahcivan", "standart", 3, 50, A8, "HM", noTargets: true, wallLimit: 900, hp: HpMid);
                Add("Davranış yönü", "bahcivan", "standart", 3, 50, A8, "HM · almayan eş", xp: "vadesinde", declineId: "zincir_hasat", noTargets: true, wallLimit: 900, hp: HpMid,
                    lab: new[] { 30, 40 }, labReward: "zincir_hasat", labChain: ChainArms);
                break;
            // Son aday doğrulaması: profilin kendi verisi (can eğrisi, hedefler, zincir değerleri), GERÇEK hedeflerle. Elenen run biter.
            case "k378sonA": // dört build yolu × 10 seed
                Add("Hasar-alan", "bahcivan", "standart", 10, 50, A8, "son", xp: "vadesinde", wallLimit: 900);
                Add("Patlama yolu", "bahcivan", "standart", 10, 50, A8, "son", declineId: "zincir_hasat", wallLimit: 900);
                Add("Elektrik yolu", "bahcivan", "standart", 10, 50, A8, "son", declineId: "zincir_hasat", wallLimit: 900);
                Add("Davranış yönü", "bahcivan", "standart", 10, 50, A8, "son", xp: "vadesinde", wallLimit: 900);
                break;
            case "k378sonB": // genelci, düşük uyumlu, acemi; XP yatırımsız alt karşılaştırma (ana set = XP düğümleri vadesinde)
                Add("Dengeli", "bahcivan", "standart", 10, 50, A8, "son", wallLimit: 900);
                Add("Uyumsuz", "bahcivan", "standart", 10, 50, A8, "son", wallLimit: 900);
                Add("Yeni", "bahcivan", "standart", 5, 50, A8, "son", wallLimit: 900);
                Add("Hasar-alan", "bahcivan", "standart", 10, 50, A8, "son · XP'siz", xp: "yok", wallLimit: 900);
                Add("Davranış yönü", "bahcivan", "standart", 5, 50, A8, "son · XP'siz", xp: "yok", wallLimit: 900);
                break;
            case "k378sonC": // zincirin fırsat maliyeti (almayan eş + laboratuvar), ikinci nişan politikası, başlangıç alternatifleri, stres
                Add("Davranış yönü", "bahcivan", "standart", 10, 50, A8, "son · almayan eş", xp: "vadesinde", declineId: "zincir_hasat", wallLimit: 900,
                    lab: new[] { 30, 40 }, labReward: "zincir_hasat");
                Add("Patlama yolu", "bahcivan", "standart", 5, 50, A8, "son · nişan 2", declineId: "zincir_hasat", aim: "davranis", wallLimit: 900);
                Add("Davranış yönü", "bahcivan", "standart", 5, 50, A8, "son · nişan 2", xp: "vadesinde", aim: "davranis", wallLimit: 900);
                Add("Hasar-alan", "tuccar", "standart", 2, 50, A8, "son · Tüccar", xp: "vadesinde", wallLimit: 900);
                Add("Hasar-alan", "bahcivan", "dar_kesim", 2, 50, A8, "son · Dar Kesim", xp: "vadesinde", wallLimit: 900);
                Add("Davranış yönü", "secici_yetistirici", "standart", 2, 50, A8, "son · Seçici Yetiştirici", xp: "vadesinde", wallLimit: 900);
                // P7'nin seed 101 stres koşulları (politika ve seed P6 / P7 ile aynı): zincirli ve zincirsiz kol; temel güç XP stresi.
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, A8, "stres · zincirli", rewards: new[] { "zincir_hasat" }, firstSeed: 101, wallLimit: 1500);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, A8, "stres · zincirsiz", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, A8, "stres · XP", firstSeed: 101, wallLimit: 1500);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, A8, "stres · XP", firstSeed: 110, wallLimit: 1500);
                break;
            case "k378stres": // teknik stres, HEDEFSİZ: k378sonC'deki dört stres run'ı gerçek hedeflerle R13 kotasında elendi ve geç oyunu
                              // sınamadı. Aynı koşullar (P7'nin seed 101 çifti, temel güç XP stresi) run R50'ye kadar oynansın diye hedefsiz.
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, A8, "stres · zincirli (hedefsiz)", rewards: new[] { "zincir_hasat" }, firstSeed: 101, wallLimit: 1500, noTargets: true);
                Add("Davranış-rezonans", "bahcivan", "standart", 1, 50, A8, "stres · zincirsiz (hedefsiz)", declineId: "zincir_hasat", firstSeed: 101, wallLimit: 1500, noTargets: true);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, A8, "stres · XP (hedefsiz)", firstSeed: 101, wallLimit: 1500, noTargets: true);
                Add("Temel güç XP stresi", "bahcivan", "standart", 1, 50, A8, "stres · XP (hedefsiz)", firstSeed: 110, wallLimit: 1500, noTargets: true);
                break;
            case "k378sonAcilis": // ilk üç boss, gerçek hedeflerle
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "hiçbir şey almaz", none: true);
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "yalnız saksı", first: new[] { "-" });
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce hasar", first: new[] { "Keskin Başlangıç - 1", "Keskin Başlangıç - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce hız", first: new[] { "Hızlı Eller - 1", "Hızlı Eller - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce ekonomi", first: new[] { "Altın Hasat I - 1", "Altın Hasat I - 2" });
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce üretim", first: new[] { "Düzenli Üretim - 1", "Grid Genişleme I" });
                Add("Dengeli", "bahcivan", "standart", 3, 10, A8, "önce XP", first: new[] { "Düzenli Üretim - 1@1", "Hasat Deneyimi I - 1", "Hasat Deneyimi I - 2" });
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
    public static void RunTakvimSmoke() => Begin("takvimsmoke");
    public static void RunK375Tekrar() => Begin("k375tekrar");
    public static void RunK375Patlama() => Begin("k375patlama");
    public static void RunK375Elektrik() => Begin("k375elektrik");
    public static void RunK376Zincir() => Begin("k376zincir");
    public static void RunK376ZincirDevam() => Begin("k376zincirdevam");
    public static void RunK3761() => Begin("k3761");
    public static void RunK3761Seed101() => Begin("k3761seed101");
    public static void RunK377Smoke() => Begin("k377smoke");
    public static void RunK377Pilot() => Begin("k377pilot");
    public static void RunK377AB() => Begin("k377ab");
    public static void RunK377AB2() => Begin("k377ab2");
    public static void RunK377Seed101() => Begin("k377seed101");
    public static void RunK377Stres() => Begin("k377stres");
    public static void RunK377StresEski() => Begin("k377stresEski");
    public static void RunK377Ayristirma() => Begin("k377ayristirma");
    public static void RunK378Smoke() => Begin("k378smoke");
    public static void RunK378TabanA() => Begin("k378tabanA");
    public static void RunK378TabanB() => Begin("k378tabanB");
    public static void RunK378Tur1() => Begin("k378tur1");
    public static void RunK378Acilis() => Begin("k378acilis");
    public static void RunK378Tur2A() => Begin("k378tur2A");
    public static void RunK378Tur2B() => Begin("k378tur2B");
    public static void RunK378SonA() => Begin("k378sonA");
    public static void RunK378SonB() => Begin("k378sonB");
    public static void RunK378SonC() => Begin("k378sonC");
    public static void RunK378SonAcilis() => Begin("k378sonAcilis");
    public static void RunK378Stres() => Begin("k378stres");

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
    static double nextAt, stepSince, runStartedAt; static string lastSignature, lastProgress;
    static readonly StringBuilder csv = new(), nodeCsv = new(), rewardCsv = new(), summary = new(), finalCsv = new();
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
    sealed class LabJob { public Snapshot Shot; public bool WithReward, Synergy; public int Repeat, Variant = -1; }
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
        var e = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.ForSegment(RM.Calendar.PeriodOf(round)) : null;
        s.Boss = e != null && RM.IsBossRound(round) && !(e.Data is NoRuleBossSO) ? e.Data : null;
        return s;
    }

    // Ödül id'siyle: düz liste, kırılma listesi ve aşamalı havuzun aşamaları.
    static BossRewardSO FindReward(BossRewardPoolSO pool, string id)
    {
        if (pool == null) return null;
        var found = pool.rewards.Concat(pool.breakthroughs).FirstOrDefault(r => r != null && r.id == id);
        if (found == null && pool.IsStaged) found = pool.stages.SelectMany(st => st.rewards).Select(e => e.reward).FirstOrDefault(r => r != null && r.id == id);
        return found;
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
        // Ödüller saksılardan sonra: bedelli ödülün koşulu (yerleşmiş davranış saksısı) alınırken yeniden aranır.
        foreach (var (reward, stacks) in s.Rewards) for (int i = 0; i < stacks; i++) if (!Grant(reward)) throw new Exception("lab: reward not granted " + reward.id);
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
                // Kayıtta ödül zaten varsa (bot onu almak zorunda kaldı) A / B karşılaştırması kurulamaz: atlanır ve yazılır.
                if (config.LabReward != null && s.Rewards.Any(r => r.reward.id == config.LabReward))
                {
                    if (repeat == 0) summary.AppendLine($"   LAB R{s.Round} atlandı: {config.LabReward} bu run'da zaten alınmış (seed {config.Seed}, {policy.Name})");
                    continue;
                }
                labJobs.Enqueue(new LabJob { Shot = s, WithReward = false, Repeat = repeat });
                if (config.LabChain == null) labJobs.Enqueue(new LabJob { Shot = s, WithReward = true, Repeat = repeat });
                else for (int v = 0; v < config.LabChain.Length; v++) labJobs.Enqueue(new LabJob { Shot = s, WithReward = true, Repeat = repeat, Variant = v });
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
        ApplyRunOverrides(runProfile, config);   // laboratuvar round'u run'la aynı can eğrisi / hedef adayıyla oynanır
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
            var extra = lab.WithReward || lab.Synergy ? FindReward(pool, config.LabReward) : null;
            if ((lab.WithReward || lab.Synergy) && extra == null) throw new Exception("lab: reward not in pool " + config.LabReward);
            string variant = "";
            if (lab.WithReward && lab.Variant >= 0)
            {
                // Zincir kolu: aynı ödülün kopyası, yalnız iki çarpan dizisi farklı (asset değişmez).
                var arm = config.LabChain[lab.Variant];
                extra = Object.Instantiate(extra);
                extra.chainChance = (float[])arm.chance.Clone(); extra.chainDamage = (float[])arm.damage.Clone();
                variant = " [" + arm.name + "]";
            }
            string note = ApplySnapshot(lab.Shot, lab.WithReward ? extra : null, false);
            if (lab.Synergy) { note = ApplySynergy(extra); if (lab.WithReward && BossRewardManager.Instance.Stacks(extra) == 0) Grant(extra); }
            labConfig = new RunConfig { Profile = config.Profile, Farmer = config.Farmer, Scythe = config.Scythe, PolicyName = config.PolicyName, Seed = config.Seed, MaxRound = lab.Shot.Round,
                                        Label = $"LAB R{lab.Shot.Round} {(lab.Synergy ? "sinerji " : "")}{(lab.WithReward ? "B +" + config.LabReward + variant : "A ödülsüz")} #{lab.Repeat + 1}" };
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
                nodeCsv.AppendLine("run,policy,farmer,scythe,seed,label,round,node,level,cost,currency,category");
                finalCsv.AppendLine(RunBot.FinalHeader);
                rewardCsv.AppendLine("run,policy,farmer,scythe,seed,label,round,offered,chosen");
                MetaSave.UseMemoryOnly();
            }
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            double wait = Drive();
            if (wait < 0) { Finish(null); return; }
            nextAt = EditorApplication.timeSinceStartup + wait;
            // takılma dedektörü: durum imzası 240 sn değişmezse. Level işleme ve kart seçimi de ilerlemedir (Bölüm 3.7.6.1: XP geri
            // beslemesinde binlerce level ve kart seçimi; ilerleyen ama uzun süren run takılma sayılmaz, süre sınırı ayrıdır).
            string signature = runIndex + "/" + (RM != null ? RM.CurrentRound : -1) + "/" + (GameManager.Instance != null ? State.ToString() : "-") + "/" + (RM != null ? Mathf.FloorToInt(RM.RemainingTime) : -1) +
                               "/" + (ProgressionManager.Instance != null ? ProgressionManager.Instance.CurrentLevel : -1) + "/" + (RM != null ? RM.PendingCardSelections : -1);
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
        File.WriteAllText($"Logs/BalanceRuns_{setName}_final.csv", finalCsv.ToString(), new UTF8Encoding(false));
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
            runStartedAt = EditorApplication.timeSinceStartup;
            return .05;
        }
        // Süre sınırı (yalnız bunu isteyen setlerde): run tamamlanmış gösterilmez; o anki round'un yarım satırı "kesildi" diye yazılır.
        if (config.WallLimitSec > 0 && EditorApplication.timeSinceStartup - runStartedAt > config.WallLimitSec)
        {
            bot.WritePartial();
            var progress = ProgressionManager.Instance;
            double left = progress.HasPendingLevels ? Math.Floor(progress.StoredXP / progress.XPToNextLevel) : 0;
            string E(double v) => v.ToString("0.###e0", CultureInfo.InvariantCulture);   // CSV sütunu: ondalık virgül olmasın
            EndRun($"durduruldu (süre sınırı {config.WallLimitSec:0} sn; {State}; level {progress.CurrentLevel}; saklı XP {E(progress.StoredXP)} ≈ {E(left)} level daha bekliyor" +
                   $"{(progress.LevelProcessingHalted ? " · level işleme teknik sınırda durdu" : "")}; bekleyen seçim {RM.PendingCardSelections})");
            return AfterRun();
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
        ApplyXpArm(policy, config);
        if (config.Aim == "davranis") policy.BehaviorAim = true;
        else if (config.Aim != null) throw new Exception("unknown aim policy " + config.Aim);
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ProfileFolder + config.Profile + ".asset");
        if (source == null) throw new Exception("profile not found: " + config.Profile);
        runProfile = Object.Instantiate(source);
        runProfile.name = source.name + " (ölçüm)";
        if (runProfile.bossPool != null) runProfile.bossSeed = 7000 + config.Seed;   // aynı seed aynı boss ve ödül dizisi
        if (config.NoRewards) runProfile.bossRewards = null;
        // Aşamalı ödül havuzunda (Bölüm 3.7.3) ayrı bir kırılma ödülü listesi yoktur: "ödülsüz eş", "almayan eş" ve laboratuvar kolları
        // o listeye göre yazıldı. Bu kollar aşamalı havuzda sessizce eski varsayımla çalıştırılmaz.
        // "Almayan eş" yalnız teklifi süzer (IsBreakthrough), kırılma listesine bakmaz: aşamalı havuzda da çalışır (Bölüm 3.7.5).
        // Laboratuvar (Bölüm 3.7.8) aşamalı havuzda çalışır: ödül id'yle aşamalardan bulunur; "ödülsüz eş" hâlâ desteklenmez.
        if (runProfile.bossRewards != null && runProfile.bossRewards.IsStaged && config.NoBreakthroughs)
            throw new Exception("Bu profil desteklenmiyor: aşamalı ödül havuzu için ödülsüz eş kolu uyarlanmadı (" + config.Profile + ")");
        if (config.NoBreakthroughs && runProfile.bossRewards != null)
        {
            // Ödülsüz eş: aynı havuz, kırılma ödülleri olmadan (asset değişmez; kopya üzerinde).
            runProfile.bossRewards = Object.Instantiate(runProfile.bossRewards);
            runProfile.bossRewards.breakthroughs.Clear();
        }
        if (config.ChoicesPerLevel > 0) runProfile.choicesPerLevel = config.ChoicesPerLevel;
        if (config.TailGrowth >= 0f || config.AdditiveXp >= 0)
        {
            // Yalnız bu run için: denge seti (ve XP tablosu) kopyalanır, kural değişir (asset'ler değişmez).
            if (runProfile.balance == null || runProfile.balance.progression == null) throw new Exception("rule override needs a profile balance set: " + config.Profile);
            runProfile.balance = Object.Instantiate(runProfile.balance);
            if (config.AdditiveXp >= 0) runProfile.balance.additiveBaseXpCards = config.AdditiveXp == 1;
            if (config.TailGrowth >= 0f)
            {
                runProfile.balance.progression = Object.Instantiate(runProfile.balance.progression);
                runProfile.balance.progression.tailGrowth = config.TailGrowth;
            }
        }
        ApplyRunOverrides(runProfile, config);
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

    // Bölüm 3.7.8: yalnız bu run için (profil kopyası üzerinde; asset'ler değişmez) hedef tablosu, can eğrisi ve zincir çarpanı adayı.
    static void ApplyRunOverrides(RunProfileSO profile, RunConfig c)
    {
        if (c.Quota != null) profile.segmentTargets = c.Quota.ToList();
        if (c.Boss != null) profile.bossTargets = c.Boss.ToList();
        if (c.NoTargets)
        {
            profile.segmentTargets = profile.segmentTargets.Select(_ => 1L).ToList();
            profile.bossTargets = profile.bossTargets.Select(_ => 1L).ToList();
        }
        if (c.Hp != null)
        {
            if (profile.balance == null || profile.balance.plantHealth == null || c.Hp.Length < 2 || c.Hp.Length % 2 != 0) throw new Exception("health override needs a profile balance set and (round, health) pairs: " + c.Profile);
            profile.balance = Object.Instantiate(profile.balance);
            profile.balance.plantHealth = Object.Instantiate(profile.balance.plantHealth);
            profile.balance.plantHealth.anchors = Enumerable.Range(0, c.Hp.Length / 2).Select(i => new PlantHealthAnchor { round = Mathf.RoundToInt(c.Hp[2 * i]), commonHealth = c.Hp[2 * i + 1] }).ToList();
        }
        if (c.ChainChance != null || c.ChainDamage != null)
        {
            var pool = profile.bossRewards;
            if (pool == null || !pool.IsStaged) throw new Exception("chain override needs a staged reward pool: " + c.Profile);
            pool = profile.bossRewards = Object.Instantiate(pool);
            bool done = false;
            foreach (var stage in pool.stages)
                for (int i = 0; i < stage.rewards.Count; i++)
                {
                    var entry = stage.rewards[i];
                    if (entry.reward == null || !entry.reward.chain) continue;
                    entry.reward = Object.Instantiate(entry.reward);
                    if (c.ChainChance != null) entry.reward.chainChance = (float[])c.ChainChance.Clone();
                    if (c.ChainDamage != null) entry.reward.chainDamage = (float[])c.ChainDamage.Clone();
                    stage.rewards[i] = entry; done = true;
                }
            if (!done) throw new Exception("chain override: no chain reward in the pool of " + c.Profile);
        }
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
                           $"ilk davranış kartı R{bot.FirstBehavior} · ilk tetik R{bot.FirstTrigger} | kırılma {(bot.Breakthroughs.Count > 0 ? string.Join(" ", bot.Breakthroughs) : "-")}" +
                           (RunClock.Instance != null ? $" | aktif süre {N(RunClock.Instance.ActiveGameSeconds, "0.#")} sn (gerçek {N(RunClock.Instance.ActiveRealSeconds, "0.#")} sn) · {RunClock.Instance.RoundsTimed} round" : ""));
        finalCsv.AppendLine(bot.FinalRow(outcome));
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
        nodeCsv.AppendLine($"{runIndex + 1},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{ShopRound},{node.name},{level + 1},{tier.cost},{tier.costType},{Category(node)}");
        if (Category(node) == Cat.Xp) bot.XpSpent += tier.cost;
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
            // Bölüm 3.7.7: XP kolunun erken Water tercihi ve temel güç kartı tercihi (eski politikalarda ikisi de yok: w değişmez).
            if (o.Tile != null && o.Tile.modifierType == TileModifierType.Water && RM.CurrentRound <= policy.XpEarlyUntil) w = policy.XpCardWeight;
            if (o.IsBaseStat && policy.BaseWeight.TryGetValue(o.Modifiers[0].statType, out float bw)) w = bw;
            return rarity * w;
        }).First();
        // Erken davranış erişimi: ilk patlama / elektrik kartı teklif edildiği anda alınır (yol politikalarının kuralıyla aynı).
        if (!policy.Naive && policy.TakesFirstBehavior && bot.FirstBehavior == 0)
        {
            var first = offers.Where(o => !o.IsUpgrade && o.Tile != null && (o.Tile.modifierType == TileModifierType.Explosive || o.Tile.modifierType == TileModifierType.Electric))
                .OrderByDescending(o => RarityPower[(int)o.Rarity]).FirstOrDefault();
            if (first != null) pick = first;
        }
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
        // FirstUpgrade / FirstBase: ilk yükseltme ve ilk temel güç kartının alındığı round sonu.
        if (pick.IsUpgrade) { bot.CardUpgrades++; if (bot.FirstUpgrade == 0) bot.FirstUpgrade = RM.CurrentRound; }
        else if (pick.IsBaseStat)
        {
            bot.CardBase++; if (bot.FirstBase == 0) bot.FirstBase = RM.CurrentRound;
            if (pick.Modifiers[0].statType == StatType.XPGainMultiplier) bot.CardXpBase++;
        }
        else
        {
            bot.CardTiles++;
            var type = pick.Tile.modifierType;
            if (type == TileModifierType.Water) bot.CardWater++;
            if ((type == TileModifierType.Explosive || type == TileModifierType.Electric) && bot.FirstBehavior == 0) bot.FirstBehavior = RM.CurrentRound;
            if (policy.WantBehavior == type && bot.FirstOwn == 0) bot.FirstOwn = RM.CurrentRound;
        }
        bot.NoteCardScreen(cardUI);
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
        if (config.DeclineId != null && offer.Any(r => r.id != config.DeclineId)) offer = offer.Where(r => r.id != config.DeclineId).ToList();
        BossRewardSO pick = null;
        if (policy.Naive) pick = offer[naive.Next(offer.Count)];
        else
        {
            string[] order = policy.FixedRewardOrder || policy.Early(RM.CurrentRound) ? policy.RewardOrder : Balanced().RewardOrder;
            if (RM.CurrentRound <= policy.XpEarlyUntil) pick = offer.FirstOrDefault(r => r.id == "bilgi_filizi");
            if (pick == null) foreach (string id in order) { pick = offer.FirstOrDefault(r => r.id == id); if (pick != null) break; }
            // Bedelli ödüller (Bölüm 3.7.4) politikaların sırasında yoktur: bot onları yalnız başka seçenek kalmadıysa alır.
            pick ??= offer.FirstOrDefault(r => !r.HasCost) ?? offer[0];
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
            "echoSched,echoExec,echoDrop,echoHits,echoKills,rhythmCharges,rhythmAttacks,skipElectricVisual,skipExplosionVisual,skipBoomerang,endFill,scorePerSec,killsPerSec,normalBehaviorKills,chainKills," +
            // Bölüm 3.7.6.1 (sona eklendi): o round'da uygulanan en büyük doğrudan vuruş, doyan / geçersiz sayı dönüşümleri, round sonunda
            // saklı XP, işlenmeyi bekleyen level işi ve teknik sınırda duran level işleme.
            "maxDirectHit,saturated,invalidNumbers,storedXp,pendingLevels,levelHalted," +
            // Bölüm 3.7.7 (sona eklendi). firstEffective: davranış şansı olan bir saksıyla başlanan ilk round. behaviorTiles /
            // behaviorPlanted: round sonunda tarladaki patlama-elektrik tile'ları ve saksı altındakiler. xpSpent: XP düğümlerine
            // harcanan kaynak (run başından). cardWater / cardXpBase: alınan Water tile ve temel güç XP kartı (run başından).
            // xpGlobal: global XP çarpanı (ağaç × ödül × temel güç; tile ve rezonans hariç). xpCardGroup: toplanan temel güç XP
            // kartlarının toplamı (yalnız yeni kuralda). levelCost: sonraki level'ın maliyeti. waitFrames: bir önceki round sonunun
            // level işleme bekleyişi (kare). prevChoices: bir önceki round sonunda yapılan kart seçimi.
            "firstEffective,behaviorTiles,behaviorPlanted,xpSpent,cardWater,cardXpBase,xpGlobal,xpCardGroup,levelCost,waitFrames,prevChoices," +
            // Bölüm 3.7.8 (sona eklendi). activeGame / activeReal: round sayacından düşen süre ve aynı karelerde gerçekte geçen süre
            // (RunClock; sabit kare adımında ikisi de tekrarlanabilir). plantWait: o round'da hasat edilen bitkilerin tarlada
            // beklediği ortalama süre (sn, yalnız aktif round kareleri); wait*: öldüren kaynağa göre (doğrudan / normal davranış /
            // zincir nesil 1 / nesil 2 / artçı-ikinci dalga). behaviorHits / chainHits: normal (nesil 0) ve zincir davranış vuruşu;
            // chainHitsNew: kökün doğrudan vuruşunun ve normal tetiğinin dokunmadığı hücrelere zincir vuruşu. *KillHp: öldüren
            // vuruştan önceki can / azami can ortalaması. ch*: zincir hunisi (deneme, başarılı tetik, yürütülen iş; retler; havuz
            // beklemesi; kuyruk gecikmesi). ghost*: zincirin öldürdüğü bitkilerden, sıradaki doğrudan saldırının (o bitkiler yerinde
            // dursaydı seçeceği nişanla) yine erişeceği (Reached) ve en düşük doğrudan hasarla kesin öldüreceği (Lethal) olanlar.
            // frameMs*: aktif round karelerinin gerçek süresi (ölçüm botu dahil; batch, GPU sonucu değildir), botMsAvg: botun payı.
            "activeGame,activeReal,plantWait,waitDirect,waitBehavior,waitChain1,waitChain2,waitEcho," +
            "behaviorHits,chainHits,chainHitsNew,chainKills1,chainKills2,chainKillHp,behaviorKillHp," +
            "chAttempts1,chAttempts2,chTriggers1,chTriggers2,chFired1,chFired2,chRejRepeat,chRejGeneration,chRejBudget,chRejInvalid,chRejRoundEnd,chRejExcluded," +
            "chPoolWaitJobs,chPoolWaitFrames,chDelayFrames,ghostKills,ghostResolved,ghostReached,ghostLethal,frameMsAvg,frameMsP95,frameMsMax,botMsAvg,gc0,aim";
        public const string FinalHeader = "run,profile,policy,farmer,scythe,seed,label,outcome,round,score,level,levels,choices,cardsTaken,cardTiles,cardUpgrades,cardBase,cardXpBase,cardWater," +
            "firstBehavior,firstEffective,firstTrigger,firstUpgrade,firstBase,xpSpent,xpGlobal,xpCardGroup,levelCost,storedXp,levelHalted,saturated,invalidNumbers," +
            "maxRoundChoices,maxRoundChoicesRound,maxWaitFrames,totalWaitFrames,remainingShownOk,tailGrowth," +
            // Bölüm 3.7.8: run süre sayaçları (RunClock). clockActive*: tekrarlanabilir. Diğerleri botun menüde geçirdiği kare
            // sayısına bağlıdır (insan süresi DEĞİLDİR, makineye göre değişir): yalnız sayaçların ayrıldığını gösterir.
            "clockActiveGame,clockActiveReal,roundsTimed,clockLevelWork,clockCards,clockRoundChoice,clockShop,clockPrep,clockOther,clockPaused,chainMaxPending,aim";

        public int TiersBought, PlantersBought, CardScreens;
        // run boyunca: alınan kartlar (tile / yükseltme / temel güç), ilk davranış kartı, ilk gerçek tetik, kırılma ödülleri
        public int CardTiles, CardUpgrades, CardBase, FirstBehavior, FirstOwn, FirstTrigger;
        // Bölüm 3.7.7
        public int CardXpBase, CardWater, FirstEffective, FirstUpgrade, FirstBase; public long XpSpent;
        int waitFrames, prevWaitFrames, maxWaitFrames, totalWaitFrames, prevChoices, maxRoundChoices, maxRoundChoicesRound, saturatedRun0, invalidRun0;
        bool remainingOk = true; int screensThisEnd;
        public readonly List<string> Breakthroughs = new();
        public string LabLine = "";
        int triggers0, echoKillsRound, chainKillsRound, normalKillsRound; double lastFill;
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
        // Süreler kare sayısıyla tutulur (sabit adım: FrameTime). Time.time tek duyarlıklıdır; farkı oturumun mutlak zamanına göre
        // yuvarlanır ve aynı run'ın iki ayrı çalıştırmasında süre sütunlarını 0,01 sn oynatırdı (Bölüm 3.7.5).
        const int Best5sFrames = 150;   // 5 sn
        readonly Dictionary<PlantHealth, (uint life, int hits, int first)> hitLog = new();
        readonly Dictionary<ResourceType, long> income = new(), lastIncome = new();
        int maxDirectHit, saturated0, invalid0;
        // Bölüm 3.7.8
        readonly Dictionary<GridObject, int> bornAt = new();   // üretim noktası → üstündeki bitkinin görüldüğü ilk aktif kare
        int activeFrame;                                       // run boyunca aktif round karesi (menüde ilerlemez)
        readonly double[] waitBy = new double[5]; readonly int[] waitByN = new int[5];   // 0 doğrudan · 1 normal davranış · 2 zincir nesil 1 · 3 zincir nesil 2 · 4 artçı
        readonly HashSet<long> rootCells = new();
        int behaviorHits, chainHits, chainHitsNew, behaviorKillN; readonly int[] chainKills = new int[3];
        double chainKillHp, behaviorKillHp;
        PlantHealth lastHit; int lastHitHp;
        readonly Dictionary<GridObject, (int hp, int rarity)> ghosts = new();   // son doğrudan saldırıdan beri zincirin öldürdükleri
        int ghostKills, ghostResolved, ghostReached, ghostLethal;
        readonly int[] chain0 = new int[16];
        readonly List<float> frameMs = new(); long frameStamp; bool frameWasActive; double botMsSum; int gcStart;
        readonly Dictionary<PlanterBrain, float> behaviorChance = new();
        static readonly double TickMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        static int[] ChainNow()
        {
            var c = HarvestChain.Instance; var a = new int[16];
            if (c == null) return a;
            a[0] = c.Attempts(1); a[1] = c.Attempts(2); a[2] = c.Triggers(1); a[3] = c.Triggers(2); a[4] = c.Fired(1); a[5] = c.Fired(2);
            for (int k = 0; k < HarvestChain.RejectKinds; k++) a[6 + k] = c.Rejected((HarvestChain.Reject)k);
            a[12] = c.PoolWaitJobs; a[13] = c.PoolWaitFrames; a[14] = (int)Math.Min(int.MaxValue, c.DelayFramesTotal);
            return a;
        }

        // Nişan değeri: politika (bitki sayısı ya da skor değeri) × ikinci nişan politikasında saksının davranış şansı.
        double AimWeight(PlantHealth h)
        {
            double w = policy.ValueAim && h.Data != null ? Value[(int)h.Data.rarity] : 1;
            if (!policy.BehaviorAim || h.Owner == null) return w;
            if (!behaviorChance.TryGetValue(h.Owner, out float chance))
            {
                var o = h.Owner;
                chance = Mathf.Min(1f, o.GetFinalStat(StatType.ExplosionChance) + o.GetFinalStat(StatType.ElectricChance) + o.GetFinalStat(StatType.TornadoChance) + o.GetFinalStat(StatType.BoomerangChance));
                behaviorChance[o] = chance;
            }
            return w * (1 + 2 * chance);
        }
        int attacks, hitTargets, fillSamples, emptySamples; double fillSum, minFill; long score0, segScore0; double xp0;
        readonly Queue<int> killTimes = new(); int best5s;
        int lastRound; bool roundOpen; string outcome = "";

        public float LastIncome(ResourceType t) => lastIncome.TryGetValue(t, out long v) ? v : 0;
        public float LastFill { get; private set; } = -1f;   // son round'un ortalama tarla doluluğu (ölçüm yoksa -1)

        public void Init(Policy p, RunConfig c, int runIndex, StringBuilder output)
        {
            policy = p; config = c; run = runIndex; csv = output;
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            attack = typeof(PlayerController).GetMethod("AttackInRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            PlantHealth.AnyHarvested += OnHarvest;
            PlantHealth.AnyDamaged += OnDamaged;
            ResourceManager.Instance.OnHarvestResourceAdded += OnIncome;
            RoundManager.Instance.OnRoundChanged += OnRoundStart;
            RoundManager.Instance.OnRoundEnded += OnRoundEnd;
            triggers0 = Triggers();   // sayaç statiktir (sahne değişiminde sıfırlanmaz): run başındaki değer taban alınır
            saturatedRun0 = NumericSafety.TotalSaturated; invalidRun0 = NumericSafety.TotalInvalid;
        }

        static bool IsBehaviorTile(GroundCell cell) => cell != null && cell.CurrentModifier != null &&
            (cell.CurrentModifier.modifierType == TileModifierType.Explosive || cell.CurrentModifier.modifierType == TileModifierType.Electric);

        // Tarladaki patlama / elektrik tile'ları ve saksı altında olanlar.
        static (int tiles, int planted) BehaviorTiles()
        {
            int tiles = 0, planted = 0;
            var grid = GridManager.Instance.GetGridSystem();
            int w = GridManager.Instance.GetWidth(), h = GridManager.Instance.GetHeight();
            for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
            {
                var cell = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
                if (cell == null || cell.IsLocked || !IsBehaviorTile(cell)) continue;
                tiles++;
                if (cell.Planter != null) planted++;
            }
            return (tiles, planted);
        }

        // Kart ekranı: kalan seçim sayacı gerçek bekleyen hakla aynı mı (panelin gösterdiği sayı; her seçimde denetlenir).
        public void NoteCardScreen(CardSelectionUI ui)
        {
            screensThisEnd++;
            if (ui.ShownRemaining != RoundManager.Instance.PendingCardSelections || ui.RemainingText == null) remainingOk = false;
        }

        public string FinalRow(string result)
        {
            CloseRoundEnd();
            var rm = RoundManager.Instance; var progress = ProgressionManager.Instance; var stats = StatManager.Instance;
            return $"{run + 1},{config.Profile},{policy.Name},{config.Farmer},{config.Scythe},{config.Seed},{config.Label},{result.Split(' ')[0]},{rm.CurrentRound},{HarvestScoreManager.Instance.TotalScore}," +
                   $"{progress.CurrentLevel},{rm.LevelsGained},{rm.CardChoicesGranted},{rm.CardsTaken},{CardTiles},{CardUpgrades},{CardBase},{CardXpBase},{CardWater}," +
                   $"{FirstBehavior},{FirstEffective},{FirstTrigger},{FirstUpgrade},{FirstBase},{XpSpent},{N(stats.GetFinalStat(StatType.XPGainMultiplier, StatTarget.Planter), "0.####")}," +
                   $"{N(stats.SummedGroupTotal(StatType.XPGainMultiplier, StatTarget.Planter), "0.####")},{N(progress.XPToNextLevel, "0")},{N(progress.StoredXP, "0")},{(progress.LevelProcessingHalted ? 1 : 0)}," +
                   $"{NumericSafety.TotalSaturated - saturatedRun0},{NumericSafety.TotalInvalid - invalidRun0},{maxRoundChoices},{maxRoundChoicesRound},{maxWaitFrames},{totalWaitFrames},{(remainingOk ? 1 : 0)}," +
                   $"{N(progress.Data != null ? progress.Data.tailGrowth : 0, "0.####")}," + ClockColumns();
        }

        string ClockColumns()
        {
            var c = RunClock.Instance;
            string aim = policy.BehaviorAim ? "davranis" : policy.Naive ? "rastgele" : policy.ValueAim ? "deger" : "kalabalik";
            int pending = HarvestChain.Instance != null ? HarvestChain.Instance.MaxPending : 0;
            if (c == null) return $",,,,,,,,,,{pending},{aim}";
            return $"{N(c.ActiveGameSeconds, "0.###")},{N(c.ActiveRealSeconds, "0.###")},{c.RoundsTimed},{N(c.LevelWorkSeconds, "0.#")},{N(c.CardSelectionSeconds, "0.#")},{N(c.RoundChoiceSeconds, "0.#")}," +
                   $"{N(c.ShopSeconds, "0.#")},{N(c.PreparationSeconds, "0.#")},{N(c.OtherSeconds, "0.#")},{N(c.PausedSeconds, "0.#")},{pending},{aim}";
        }

        // Bir round sonunun kart seçimleri ve level bekleyişi kapanır (sonraki round başlarken ya da run biterken).
        void CloseRoundEnd()
        {
            prevChoices = screensThisEnd; prevWaitFrames = waitFrames;
            if (screensThisEnd > maxRoundChoices) { maxRoundChoices = screensThisEnd; maxRoundChoicesRound = lastRound; }
            maxWaitFrames = Math.Max(maxWaitFrames, waitFrames); totalWaitFrames += waitFrames;
            screensThisEnd = 0; waitFrames = 0;
        }

        void OnDamaged(PlantHealth plant, int damage, DamageType type)
        {
            if (type == DamageType.Direct && damage > maxDirectHit) maxDirectHit = damage;
            if (!roundOpen) return;
            // Olay can düşmeden önce gelir: CurrentHealth bu vuruştan önceki candır.
            lastHit = plant; lastHitHp = plant.CurrentHealth;
            HarvestLink link = plant.LastHitLink;
            if (type != DamageType.Direct && !link.Echo) { if (link.IsChain) chainHits++; else behaviorHits++; }
            if (link.Root == 0) return;
            var cell = GridManager.Instance.GetGridSystem().GetGridPosition(plant.transform.position);
            long key = ((long)link.Root << 16) | (long)((cell.x & 0xFF) << 8) | (long)(cell.z & 0xFF);
            if (link.Generation <= 0) rootCells.Add(key);
            else if (!rootCells.Contains(key)) chainHitsNew++;
        }

        void OnDestroy()
        {
            PlantHealth.AnyHarvested -= OnHarvest;
            PlantHealth.AnyDamaged -= OnDamaged;
            if (ResourceManager.Instance != null) ResourceManager.Instance.OnHarvestResourceAdded -= OnIncome;
            if (RoundManager.Instance != null) { RoundManager.Instance.OnRoundChanged -= OnRoundStart; RoundManager.Instance.OnRoundEnded -= OnRoundEnd; }
        }

        void OnIncome(ResourceType type, int amount, Vector3 _) { income.TryGetValue(type, out long v); income[type] = v + amount; }

        void OnRoundStart(int round)
        {
            CloseRoundEnd();
            if (FirstEffective == 0 && FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Any(p => p.OccupiedGrids.Count > 0 &&
                    (p.GetFinalStat(StatType.ExplosionChance) > 0f || p.GetFinalStat(StatType.ElectricChance) > 0f))) FirstEffective = round;
            Array.Clear(kills, 0, 5); Array.Clear(byRarity, 0, 5); Array.Clear(hitSum, 0, 5); Array.Clear(ttkSum, 0, 5);
            Array.Clear(tracked, 0, 5); Array.Clear(oneShots, 0, 5); Array.Clear(fresh, 0, 5);
            hitLog.Clear(); income.Clear(); attacks = hitTargets = fillSamples = emptySamples = 0; fillSum = 0; minFill = 1; timer = 0;
            killTimes.Clear(); best5s = 0; CardScreens = 0;
            score0 = HarvestScoreManager.Instance.TotalScore;
            if (RoundManager.Instance.Calendar.IsPeriodStart(round)) segScore0 = score0;   // dönem başı: ortak run takvimi
            xp0 = ProgressionManager.Instance.TotalXPEarned;
            lastRound = round; roundOpen = true; aims = null;
            var now = EchoNow(); for (int i = 0; i < 4; i++) echo0[i] = now[i];
            echoKillsRound = chainKillsRound = normalKillsRound = 0; lastFill = 0;
            charges0 = RunPower.Rhythm.Charges; strong0 = RunPower.Rhythm.EmpoweredAttacks;
            skipElectric0 = HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.SkippedElectricVisuals : 0;
            skipExplosion0 = VFXManager.Instance != null ? VFXManager.Instance.ExplosionVisualsSkipped : 0;
            skipBoomerang0 = HarvestBehaviorStats.Skipped(DamageType.Boomerang);
            maxDirectHit = 0; saturated0 = NumericSafety.TotalSaturated; invalid0 = NumericSafety.TotalInvalid;
            Array.Clear(waitBy, 0, 5); Array.Clear(waitByN, 0, 5); Array.Clear(chainKills, 0, 3);
            rootCells.Clear(); ghosts.Clear(); behaviorChance.Clear(); frameMs.Clear();
            behaviorHits = chainHits = chainHitsNew = behaviorKillN = ghostKills = ghostResolved = ghostReached = ghostLethal = 0;
            chainKillHp = behaviorKillHp = botMsSum = 0; frameWasActive = false; lastHit = null;
            var chainNow = ChainNow(); for (int i = 0; i < chain0.Length; i++) chain0[i] = chainNow[i];
            gcStart = GC.CollectionCount(0);
        }

        void OnHarvest(PlantHealth h)
        {
            if (!roundOpen || h.Data == null) return;
            kills[(int)h.KilledBy]++;
            if (BehaviorEchoes.IsExecuting) echoKillsRound++;   // artçı / ikinci dalganın gerçek hasadı
            // Bölüm 3.7.6: öldüren vuruşun bağlamı — zincirden doğan davranış (nesil ≥ 1) ya da normal davranış (nesil 0).
            if (h.KilledBy != DamageType.Direct && !h.KillLink.Echo) { if (h.KillLink.IsChain) chainKillsRound++; else normalKillsRound++; }
            // Bölüm 3.7.8: kaynak (0 doğrudan · 1 normal davranış · 2 / 3 zincir nesil 1 / 2 · 4 artçı), bekleme süresi, öldüren vuruştan önceki can.
            int source = h.KilledBy == DamageType.Direct ? 0 : h.KillLink.Echo ? 4 : h.KillLink.IsChain ? Math.Min(2, (int)h.KillLink.Generation) + 1 : 1;
            var grid = GridManager.Instance.GetGridSystem();
            GridObject spot = grid.GetGridObject(grid.GetGridPosition(h.transform.position));
            if (spot != null && bornAt.TryGetValue(spot, out int born)) { waitBy[source] += (activeFrame - born) * (double)FrameTime; waitByN[source]++; bornAt.Remove(spot); }
            double hpShare = lastHit == h && h.MaxHealth > 0 ? Math.Min(1.0, lastHitHp / (double)h.MaxHealth) : 1.0;
            if (source == 1) { behaviorKillHp += hpShare; behaviorKillN++; }
            if (source == 2 || source == 3)
            {
                chainKills[source - 1]++; chainKillHp += hpShare;
                if (spot != null && h.Data != null) { ghosts[spot] = (lastHit == h ? lastHitHp : h.MaxHealth, (int)h.Data.rarity); ghostKills++; }
            }
            int r = (int)h.Data.rarity; byRarity[r]++;
            int now = Time.frameCount;
            killTimes.Enqueue(now);
            while (killTimes.Count > 0 && now - killTimes.Peek() > Best5sFrames) killTimes.Dequeue();
            best5s = Math.Max(best5s, killTimes.Count);
            if (hitLog.TryGetValue(h, out var log) && log.life == h.LifetimeVersion)
            {
                // Doğrudan vuruşla ölen bitkide: kaç doğrudan vuruş aldı ve ilk vuruştan ölüme kaç saniye geçti.
                if (h.KilledBy == DamageType.Direct)
                {
                    tracked[r]++; hitSum[r] += log.hits; ttkSum[r] += (now - log.first) * (double)FrameTime;
                }
                hitLog.Remove(h);
            }
        }

        void OnRoundEnd()
        {
            if (!roundOpen) return;
            roundOpen = false;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType))) lastIncome[t] = income.TryGetValue(t, out long v) ? v : 0;
            LastFill = fillSamples > 0 ? (float)(fillSum / fillSamples) : -1f;
            if (FirstTrigger == 0 && Triggers() > triggers0) FirstTrigger = lastRound;
            WriteRow("");
        }

        // Süre sınırında: açık round'un o ana kadarki satırı (sonuç sütunu "kesildi"). Round zaten kapandıysa bir şey yazmaz.
        public void WritePartial()
        {
            if (!roundOpen) return;
            roundOpen = false;
            WriteRow("kesildi");
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
            sb.Append($"{skipElectric},{skipExplosion},{HarvestBehaviorStats.Skipped(DamageType.Boomerang) - skipBoomerang0},{N(lastFill, "0.###")},{N(duration > 0 ? (score - score0) / duration : 0, "0.###")},{N(duration > 0 ? total / duration : 0, "0.###")},{normalKillsRound},{chainKillsRound},");
            var progress = ProgressionManager.Instance;
            sb.Append($"{maxDirectHit},{NumericSafety.TotalSaturated - saturated0},{NumericSafety.TotalInvalid - invalid0},{N(progress.StoredXP, "0")},{(progress.HasPendingLevels ? 1 : 0)},{(progress.LevelProcessingHalted ? 1 : 0)},");
            var behaviorTiles = BehaviorTiles();
            sb.Append($"{FirstEffective},{behaviorTiles.tiles},{behaviorTiles.planted},{XpSpent},{CardWater},{CardXpBase},{N(stats.GetFinalStat(StatType.XPGainMultiplier, StatTarget.Planter), "0.####")}," +
                      $"{N(stats.SummedGroupTotal(StatType.XPGainMultiplier, StatTarget.Planter), "0.####")},{N(progress.XPToNextLevel, "0")},{prevWaitFrames},{prevChoices},");
            var clock = RunClock.Instance;
            int waitN = waitByN.Sum();
            string W(int i) => N(waitByN[i] > 0 ? waitBy[i] / waitByN[i] : double.NaN, "0.###");
            sb.Append($"{N(clock != null ? clock.LastRoundGameSeconds : double.NaN, "0.###")},{N(clock != null ? clock.LastRoundRealSeconds : double.NaN, "0.###")},");
            sb.Append($"{N(waitN > 0 ? waitBy.Sum() / waitN : double.NaN, "0.###")},{W(0)},{W(1)},{W(2)},{W(3)},{W(4)},");
            int chainKillN = chainKills[1] + chainKills[2];
            sb.Append($"{behaviorHits},{chainHits},{chainHitsNew},{chainKills[1]},{chainKills[2]},{N(chainKillN > 0 ? chainKillHp / chainKillN : double.NaN, "0.###")},{N(behaviorKillN > 0 ? behaviorKillHp / behaviorKillN : double.NaN, "0.###")},");
            var chainNow = ChainNow();
            for (int i = 0; i < 15; i++) sb.Append(chainNow[i] - chain0[i]).Append(',');
            frameMs.Sort();
            sb.Append($"{ghostKills},{ghostResolved},{ghostReached},{ghostLethal},{N(frameMs.Count > 0 ? frameMs.Average() : double.NaN, "0.###")},{N(frameMs.Count > 0 ? frameMs[(int)((frameMs.Count - 1) * .95)] : double.NaN, "0.###")}," +
                      $"{N(frameMs.Count > 0 ? frameMs[frameMs.Count - 1] : double.NaN, "0.###")},{N(frameMs.Count > 0 ? botMsSum / frameMs.Count : double.NaN, "0.###")},{GC.CollectionCount(0) - gcStart}," +
                      (policy.BehaviorAim ? "davranis" : policy.Naive ? "rastgele" : policy.ValueAim ? "deger" : "kalabalik"));
            csv.AppendLine(sb.ToString());
            LabLine = $"hasat {total} (doğrudan {kills[0]} · patlama {kills[1]} · elektrik {kills[4]} · kasırga {kills[2]} · bumerang {kills[3]}; yankı hasadı {echoKillsRound}) | skor {score - score0} | " +
                      $"vuruş {attacks} · vuruş başına hedef {N(attacks > 0 ? hitTargets / (double)attacks : 0)} | doluluk {N(fillSamples > 0 ? fillSum / fillSamples : 0, "0.###")} (round sonu {N(lastFill, "0.###")}) | " +
                      $"yankı {echo[1] - echo0[1]} uygulandı / {echo[2] - echo0[2]} düştü | ritim hakkı {RunPower.Rhythm.Charges - charges0}";
            TiersBought = 0; PlantersBought = 0;
            long V(ResourceType t) => income.TryGetValue(t, out long v) ? v : 0;
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
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round && !roundOpen && RoundManager.Instance.IsAwaitingLevels) waitFrames++;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round || !roundOpen) { frameWasActive = false; return; }
            // Kare süresi: art arda iki aktif round karesinin arası (gerçek saat). İçinde oyun, editör ve bu bot vardır.
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (frameWasActive) frameMs.Add((float)((stamp - frameStamp) * TickMs));
            frameStamp = stamp; frameWasActive = true;
            activeFrame++;
            Work();
            botMsSum += (System.Diagnostics.Stopwatch.GetTimestamp() - stamp) * TickMs;
        }

        void Work()
        {
            var rm = RoundManager.Instance; var stats = StatManager.Instance;
            var grid = GridManager.Instance.GetGridSystem();
            // Nişan, sıradaki saldırının gerçek temas alanına göre seçilir (hazır Hasat Ritmi hakkı alanı büyütür; imleç halkası da bunu gösterir).
            float radius = stats.GetFinalStat(StatType.AreaRadius, StatTarget.Player) * RunPower.Rhythm.Next().RadiusMultiplier;
            BuildAims(grid, radius);
            if (spawnerRefresh++ % 60 == 0) spawners = FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).Where(s => s.GridObject != null).ToList();
            int filled = 0;
            foreach (var s in spawners)
            {
                if (s == null) continue;
                var spot = s.GridObject;
                if (spot.HasPlantObject()) { filled++; if (!bornAt.ContainsKey(spot)) bornAt[spot] = activeFrame; }
                else bornAt.Remove(spot);
            }
            double fill = spawners.Count > 0 ? filled / (double)spawners.Count : 0;
            fillSum += fill; fillSamples++; minFill = Math.Min(minFill, fill); if (fill < .15) emptySamples++;
            lastFill = fill;

            float interval = Mathf.Max(stats.GetFinalStat(StatType.AttackSpeed, StatTarget.Player), .1f) / rm.TempoMultiplier;
            if (!PlayerController.AdvanceAttackTimer(ref timer, Time.deltaTime, interval)) return;

            int bestAim = -1; double bestScore = 0;
            int ghostAim = -1; double ghostScore = 0;   // zincirin öldürdükleri yerinde dursaydı seçilecek nişan
            if (policy.Naive)
            {
                // Yeni oyuncu: canlı bitkisi olan rastgele bir hücreye nişan alır (en iyi konumu aramaz).
                var live = new List<int>();
                for (int a = 0; a < aims.Count; a++) if (Living(aimCells[a]) > 0) live.Add(a);
                if (live.Count > 0) { bestAim = live[random.Next(live.Count)]; bestScore = 1; }
                ghosts.Clear();   // rastgele nişanda karşı-olgusal tanımsız
            }
            else
                for (int a = 0; a < aims.Count; a++)
                {
                    double s = 0, ghost = 0;
                    foreach (var g in aimCells[a])
                    {
                        var p = g.GetPlantObject();
                        if (p != null && p.TryGetComponent(out PlantHealth h) && !h.IsDead) s += AimWeight(h);
                        else if (ghosts.Count > 0 && p == null && ghosts.TryGetValue(g, out var gone)) ghost += policy.ValueAim ? Value[gone.rarity] : 1;
                    }
                    if (s > bestScore + 1e-9 || (s > 0 && Math.Abs(s - bestScore) <= 1e-9 && lastUsed[a] < lastUsed[bestAim])) { bestScore = s; bestAim = a; }
                    if (ghosts.Count == 0) continue;
                    double c = s + ghost;
                    if (c > ghostScore + 1e-9 || (c > 0 && Math.Abs(c - ghostScore) <= 1e-9 && lastUsed[a] < lastUsed[ghostAim])) { ghostScore = c; ghostAim = a; }
                }
            // Karşı-olgusal (ilk mertebe): zincirin son saldırıdan beri öldürdüğü bitkiler yerinde dursaydı sıradaki doğrudan saldırı
            // onlara erişir miydi, en düşük hasarıyla (sapmanın alt ucu, kritiksiz) öldürür müydü? Yeniden doğan hücre sayılmaz.
            if (ghosts.Count > 0 && ghostAim >= 0)
            {
                float baseDamage = stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
                float boost = RunPower.Rhythm.Next().DamageMultiplier;
                foreach (var pair in ghosts)
                {
                    if (pair.Key.HasPlantObject()) continue;
                    ghostResolved++;
                    if (!aimCells[ghostAim].Contains(pair.Key)) continue;
                    ghostReached++;
                    if (RunPower.DirectDamage(baseDamage, .85f, (PlantRarity)pair.Value.rarity, boost) >= pair.Value.hp) ghostLethal++;
                }
                ghosts.Clear();
            }
            if (bestAim < 0 || bestScore <= 0) return;
            lastUsed[bestAim] = attacks;
            int now = Time.frameCount;
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
