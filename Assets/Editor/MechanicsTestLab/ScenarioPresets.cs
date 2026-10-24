using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Hazır senaryolar. Her biri koşulu GERÇEKTEN sağlayan bir oyun düzeni kurar: ödül oyunun alınabilirlik denetiminden geçerek
    // kurulur, davranış gerçek tile'dan gelir, tetik gerçek saldırıyla oluşur. Senaryo hiçbir efekti ya da ödül olayını kendisi
    // oynatmaz. Kullanılan test statları senaryonun stat listesinde açıkça durur (panelde "test katkısı" olarak görünür).
    // Ortak kalıplar:
    //  - "Tek vuruşta hasat" gerektiğinde hasar statı değiştirilmez; hedef bitkinin CANI 1 yapılır. Komşular tam canlıdır ve
    //    gerçek davranış / ödül hasarını alır.
    //  - "Davranış kesin tetiklensin" gerektiğinde tile'ın gerçek şansı ×20 çarpılır (yalnız o tile'ı olan saksılar %100 olur;
    //    şansı olmayan saksı şanssız kalır).
    // Koordinatlar 11 × 11 grid'e göredir (merkez 5, 5): 5 × 5 açık alan 3–7, 7 × 7 açık alan 2–8.
    public static class ScenarioPresets
    {
        internal const int C = 5;
        internal const float OneCell = 0.5f, ThreeByThree = 1.5f;

        public static Scenario Default() => Field(New("00 · Boş başlangıç (3 × 3 saksı)",
            "Tarlayı 'B. Tarla' sekmesinden istediğin gibi kur; fareyle normal oyna ya da nişanı sabitle.",
            "Oyunun normal round'u: otomatik saldırı, normal üretim; round sayacı dondurulmuş.",
            "Başlangıç noktası. Hiçbir test statı ya da ödül yok.", 5), 1);

        public static List<Scenario> All()
        {
            var list = new List<Scenario> { Default() };

            Scenario s = Manual(Field(New("01 · Temel doğrudan vuruş ve kritik",
                "'Tek saldırı'ya birkaç kez bas. Sonra 'C. Statlar'da Kritik şansı katkısını 0 ve 1 yapıp yeniden dene.",
                "Olay kaydında yalnız 'Doğrudan vuruş' satırları; kritik olan saldırılarda 'Kritik vuruşlar' satırı.",
                "Kritik hasar ≈ normal hasar × kritik çarpanı (sapma ±%15). Davranış, zincir ya da ödül darbesi YOK.", 5), 2), C, C);
            Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 0.5f);
            list.Add(s);

            list.Add(Behavior("02 · Patlama", TileModifierType.Explosive, StatType.ExplosionChance,
                "Merkez bitki ölür, 'Davranış başladı: Patlama' yazılır, saksının DÖRT yan komşusu 'Patlama' hasarı alır.",
                "Çapraz komşular hasar almaz. Hasar = saksı incelemesindeki patlama hasarı. Doğrudan vuruş çarpanları patlamaya geçmez."));
            list.Add(Behavior("03 · Elektrik", TileModifierType.Electric, StatType.ElectricChance,
                "Merkez bitki ölür, 'Davranış başladı: Elektrik' yazılır, çevredeki bitkilere 'Elektrik' hasarı gider.",
                "Elektriğin hasarı görsel havuzundan bağımsızdır. Hasar = saksı incelemesindeki elektrik hasarı."));
            list.Add(Behavior("04 · Kasırga", TileModifierType.Tornado, StatType.TornadoChance,
                "Merkezden bir kasırga çıkar ve gezerek 'Kasırga' hasarı verir (gecikmeli, hareketli).",
                "Zamanı yavaşlat / duraklat: kasırga da yavaşlar ve durur. 'F. İnceleme'de Kasırga aktif sayısı 1 olur, bitince 0'a döner."));
            list.Add(Behavior("05 · Bumerang", TileModifierType.Boomerang, StatType.BoomerangChance,
                "Merkezden bir bumerang çıkar, gidip döner ve yolundaki bitkilere 'Bumerang' hasarı verir.",
                "Tek adımla ilerlet: bumerang kare kare yol alır. Aktif bumerang sayısı sınırı aşmaz."));

            s = Manual(Field(New("06 · Hasat Ritmi",
                "'Tek saldırı'ya bas (her saldırı 3 × 3 alanı hasat eder). 'F. İnceleme'de Hasat Ritmi sayacını izle; hak hazır olunca bir kez daha saldır.",
                "Eşik dolunca imleç halkası altın olur ve büyür; sıradaki saldırının temas yarıçapı kayıtta ×1,5 görünür.",
                "Yalnız NORMAL saldırıların doğrudan hasatları sayılır. Hak, canlı bitkiye değen saldırıda harcanır; güçlenen saldırı yeni hak biriktirmez.", 5), 2), C, C);
            Radius(s, ThreeByThree); s.plantHealth = 1; Reward(s, "hasat_ritmi");
            list.Add(s);

            s = Manual(Field(New("07 · Zincir Hasat",
                "'Tek saldırı'ya bir kez bas (yalnız merkez hücreye vurur). Zamanı yavaşlatıp yeniden kurarak tekrar izle.",
                "Merkez patlar; patlamanın öldürdüğü bitkilerin saksıları da patlar (nesil 1, sonra nesil 2). Kaynak saksıda mor zincir çizgisi.",
                "Kayıtta 'Zincir denemesi / tetiği' satırları: en çok 2 ek nesil, şans ve hasar ×0,75 / ×0,50. Aynı saksı aynı kökte ikinci kez denenmez ('zaten denendi').", 5), 2), C, C);
            Radius(s, OneCell); s.plantHealth = 1; Reward(s, "zincir_hasat");
            for (int x = C - 2; x <= C + 2; x++) for (int z = C - 2; z <= C + 2; z++) Tile(s, x, z, TileModifierType.Explosive, TileRarity.Legendary);
            Boost(s, StatType.ExplosionChance);
            list.Add(s);

            s = Manual(Field(New("08 · Artçı Patlama ve Çifte Akım",
                "'Tek saldırı' ile merkezi (patlama saksısı) vur. Sonra (3, 3) hücresini seçip 'Nişanı seçili hücreye al' de, canını 1 yap ve yeniden saldır (elektrik saksısı).",
                "Patlamanın dışından 0,2 sn arayla üç hücrelik yayılma; vurulan hücrelerde küçük beyaz efekt. Elektrikten sonra sarımsı ikinci dalga.",
                "Kayıtta 'Artçı Patlama planlandı → uygulandı' ve 'Çifte Akım' satırları. İkinci darbe yeni şans zarı atmaz, zincir başlatmaz.", 5), 2), C, C);
            Radius(s, OneCell);
            Tile(s, C, C, TileModifierType.Explosive, TileRarity.Legendary); Tile(s, C - 2, C - 2, TileModifierType.Electric, TileRarity.Legendary);
            Boost(s, StatType.ExplosionChance); Boost(s, StatType.ElectricChance);
            Plant(s, C, C, 1); Plant(s, C - 2, C - 2, 1);
            Reward(s, "artci_patlama"); Reward(s, "cifte_akim");
            list.Add(s);

            s = Manual(Field(New("09 · Anten / İletken Hat / Kapalı Devre",
                "Saldırmadan bekle (zaman normal hızda). İstersen yavaşlat ya da tek adımla ilerlet.",
                "Anten kendi aralığıyla baktığı yöndeki hücrelere, hat iki anten arasındaki hücrelere vurur. Hat bir davranışlı saksıyı hasat edince bazen ek hat vuruşu gelir.",
                "Kayıtta 'Ödül darbesi · Antenna / Line / LineExtra'. Bu vuruşlar doğrudan vuruş değildir: kritik atmaz, davranış tetiklemez. Konumlar 'D. Ödüller'de yazılıdır (otomatik ilk geçerli konum).", 5), 2), C, C);
            s.plantHealth = 1;
            for (int x = C - 2; x <= C + 2; x++) for (int z = C - 2; z <= C + 2; z++) Tile(s, x, z, TileModifierType.Explosive, TileRarity.Legendary);
            Boost(s, StatType.ExplosionChance);
            Reward(s, "elektrik_anteni"); Reward(s, "iletken_hat"); Reward(s, "kapali_devre");
            list.Add(s);

            s = Manual(Field(New("10 · Hilal / Büyüyen Ay / Dolunay",
                "'Tek saldırı'ya bas (3 × 3 alan, 9 bitki). Hilal altı gerçek bitki isabetiyle dolar ve imleçten çıkar.",
                "Hilal mouse halkasının kenarından çıkar: temel menzil 3, Büyüyen Ay ile 4 birim. Eşikte ana hilalin yanına, farklı yönde giden yalnız bir büyük Dolunay hilali eklenir.",
                "Dolunay'da 'Doğrudan saldırı duraklaması' kısa süre AKTİF olur: o sırada 'Tek saldırı' reddedilir ve nedeni yazılır. Kayıtta 'Ödül darbesi · Crescent / CrescentReturn'.", 7), 3), C, C);
            Radius(s, ThreeByThree); s.plantHealth = 1;
            for(int x=C-1;x<=C+1;x++)for(int z=C-1;z<=C+1;z++)Plant(s,x,z,150);
            Stat(s, StatType.HarvestDamage, StatTarget.Player, ModifierOperation.Set, 100f);
            Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 0f);
            Reward(s, "hilal_darbesi"); Reward(s, "buyuyen_ay"); Reward(s, "dolunay");
            list.Add(s);

            s = Manual(Field(New("11 · Kritik Ark",
                "'Tek saldırı'ya bas (yalnız merkez hücre; kritik şansı testte %100).",
                "Merkez bitkiye kritik; ardından yakındaki BAŞKA bir bitkiye cyan ark sıçrar.",
                "Kayıtta 'Kritik vuruşlar' ve 'Ödül darbesi · CritArc'. Ark hasarı kritik vuruşun hasarının bir oranıdır; ark kritik atmaz, yeni ark doğurmaz.", 5), 2), C, C);
            Radius(s, OneCell); Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 1f); Reward(s, "kritik_ark");
            list.Add(s);

            s = Manual(Field(New("12 · Taşan Hasar",
                "'Tek saldırı'ya bas (merkez bitkinin canı 1).",
                "Merkez ölür; artan hasarın bir kısmı en yakın bitkiye enerji parçası olarak geçer.",
                "Kayıtta 'ÖLDÜ (taşan N)' ve ardından 'Ödül darbesi · Overflow': aktarılan ≈ taşan × oran. Hedef de ölürse bir kez daha aktarılır (sınırlı).", 5), 2), C, C);
            Radius(s, OneCell); Plant(s, C, C, 1); Reward(s, "tasan_hasar");
            list.Add(s);

            s = Manual(Field(New("13 · Hasat Yankısı",
                "'Tek saldırı'ya bas (nişan merkezin sol altında: 3 × 3 alan, bütün bitkiler 1 can).",
                "Kısa süre sonra, hasat edilen hücrelerin tarlanın ortasına göre SİMETRİK hücrelerine lila yankı vurur.",
                "Kayıtta 'Ödül darbesi · HarvestEcho' hücreleri: (x, z) → (10 − x, 10 − z). Saldırının doğrudan vurduğu hücre yankı almaz.", 7), 3), C - 2, C - 2);
            Radius(s, ThreeByThree); s.plantHealth = 1; Reward(s, "hasat_yankisi");
            list.Add(s);

            s = Manual(Field(New("14 · Nadir Reaktör",
                "Önce bir patlama saksısını seçip 'F. İnceleme'de patlama şansını not et. Sonra 'Tek saldırı' ile merkezdeki nadir bitkiyi hasat et ve aynı saksıya yeniden bak.",
                "İmleçte reaktör işareti; davranış şansları bir süre artar, süre bitince eski değerine döner.",
                "Yalnız DOĞRUDAN nadir hasat çalıştırır; süre içinde yeni nadir hasat yalnız süreyi yeniler (birikmez). Şansı olmayan saksı şans kazanmaz.", 5), 2), C, C);
            Radius(s, OneCell); Reward(s, "nadir_reaktor");
            Tile(s, C - 1, C, TileModifierType.Explosive, TileRarity.Common); Tile(s, C + 1, C, TileModifierType.Electric, TileRarity.Common);
            s.plants.Add(new ScenarioPlant { x = C, z = C, plant = RarePlant(), health = 1 });
            list.Add(s);

            s = Manual(Field(New("15 · Açgözlü Hasat",
                "'Tek saldırı'ya bas (3 × 3, bütün bitkiler 1 can). Bitkiler yeniden çıkınca bir kez daha saldır.",
                "İlk saldırıdan sonra imleçte üç amber diş (hak hazır). İkinci saldırı güçlenir ve dişler kapanır.",
                "Kayıtta ikinci saldırının 'ödül saldırı çarpanı' 1'den büyüktür. Tek canlı hedefe vurursan hak KAYBOLUR; boş savuruşta hak durur.", 5), 2), C, C);
            Radius(s, ThreeByThree); s.plantHealth = 1; Reward(s, "acgozlu_hasat");
            list.Add(s);

            s = Manual(Field(New("17 · Aşırı Yükleme",
                "'Tek saldırı'ya bas (merkez: patlama saksısı, bitkinin canı 1).",
                "Normal patlama; kaynakta amber hazırlık; 0,3 sn sonra aynı patlamanın daha zayıf tekrarı.",
                "Kayıtta 'OverloadPrepared' → 'Overload'. Aynı saksı + davranış bekleme süresi dolmadan yeniden hak doğurmaz; ek proc zincir başlatmaz.", 5), 2), C, C);
            Radius(s, OneCell); Tile(s, C, C, TileModifierType.Explosive, TileRarity.Legendary); Boost(s, StatType.ExplosionChance);
            Plant(s, C, C, 1); Reward(s, "asiri_yukleme");
            list.Add(s);

            list.Add(Event("18 · Tarla olayı: Diken Kuşatması",
                "'E. Boss ve olaylar'da Diken Kuşatması'nı başlat (istersen önce bir hücre seç: bölge o hücreyi içerir). Kökleri 'Tek saldırı' ile vur.",
                "Hazırlık → ortaya çıkış → aktif: bölgede üretim durur. İki kök kırılınca bölge açılır; kırılmazsa süre dolunca açılır.",
                "Kök yalnız DOĞRUDAN saldırıyla sayılır (dayanıklılık = saldırı sayısı). Bölge dışı üretim sürer; açılınca üretim kaldığı yerden devam eder."));
            list.Add(Event("19 · Tarla olayı: Aşılı Kabuk",
                "Aşılı Kabuk'u başlat. Aktif olunca A ya da B düğümünü 'Tek saldırı' ile kır; bir kez de hiç vurmadan süreyi bekle.",
                "İki bölge, iki düğüm. Kırdığın düğümün bölgesi kurtulur, diğeri kapanır. Seçim yapmazsan A kapanır.",
                "Durum satırında hangi bölgenin 'ÜRETİME KAPALI' olduğu; kapalı bölge süre sonunda açılır."));
            list.Add(Event("20 · Tarla olayı: İstilacı Filiz",
                "İstilacı Filiz'i başlat. Bir süre bekleyip yayılmayı izle, sonra ana filizi 'Tek saldırı' ile kır.",
                "Ana filizden komşu üretim noktalarına yayılan yavaşlatma; ana filiz kırılınca hepsi kalkar.",
                "Hücre incelemesinde 'üretim aralığı ×' çarpanı; yavaşlamış hücre sayısı artar, kırılınca 0 olur."));
            list.Add(Event("21 · Tarla olayı: Gezgin Bereket",
                "Gezgin Bereket'i başlat ve bölgedeki bitkileri hasat et (otomatik saldırıyı açabilirsin).",
                "Round boyunca kalan, yavaşça gezen 3 × 3 bölge: kaynak ×2, XP ×2, boş yuvalar ×2 hızlı dolar.",
                "Bonus yalnız hasat ANINDA bölgedeki hücreye uygulanır; bölge ayrılınca o hücrede bonus kalmaz. Ceza yoktur."));

            s = Manual(New("22 · Rezonans ve stat kaynağı incelemesi",
                "Saksıları tek tek seç ve 'F. İnceleme'ye bak. 'Stat kaynağı' bölümünde bir stat seçip dökümünü oku; oyuncu kapsamı ile saksı kapsamını karşılaştır.",
                "Her saksı için tile sayıları, etkin / etkin olmayan rezonans kuralları (eksik şartıyla) ve davranış şansları.",
                "Sonuç değer oyunun çözülmüş değeridir; dökümde taban, global (test katkısı / boss ödülü / kaynağı kayıtlı değil), tile ve rezonans ayrı görünür. Doğrudan hasar katkısı davranış şansına sızmaz.", 7), C, C);
            Planter(s, LargePlanter(2, 2), C - 1, C - 1);
            Tile(s, C - 1, C - 1, TileModifierType.Damage, TileRarity.Epic); Tile(s, C, C - 1, TileModifierType.Damage, TileRarity.Epic);
            Tile(s, C - 1, C, TileModifierType.Explosive, TileRarity.Epic); Tile(s, C, C, TileModifierType.Explosive, TileRarity.Epic);
            Planter(s, LargePlanter(1, 3), C + 2, C - 1);
            Tile(s, C + 2, C - 1, TileModifierType.Fertile, TileRarity.Rare); Tile(s, C + 2, C, TileModifierType.Fertile, TileRarity.Rare); Tile(s, C + 2, C + 1, TileModifierType.Fertile, TileRarity.Rare);
            Planter(s, Small(), C - 3, C + 2); Tile(s, C - 3, C + 2, TileModifierType.Crystal, TileRarity.Legendary, 3);
            Planter(s, Small(), C - 3, C - 3);
            Stat(s, StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, 0.5f);
            Stat(s, StatType.PlantSpawnRate, StatTarget.Planter, ModifierOperation.MorePercent, -0.2f);
            Reward(s, "keskin_bicak"); Reward(s, "yikim_gucu");
            list.Add(s);

            s = Manual(Field(New("23 · Bedelli ödül, dışlama ve seçim hakkı",
                "'D. Ödüller'de Davranışa Adanış'ın neden kurulamadığını oku; 'çakışanı kaldırıp yeniden kur'u dene. 'C. Statlar'da XP ver (önce önizlemeye bak), sonra 'Round'u bitir' ile kart ekranını aç.",
                "Bereketli Öğrenim kurulu: level başına seçim hakkı 4, doğrudan vuruş ×0,80. Davranışa Adanış aynı dışlama grubunda olduğu için reddedilir.",
                "Seçim hakkı yalnız ödül alındıktan SONRA kazanılan level'ları etkiler; bekleyen seçimler yeniden hesaplanmaz. Hak aralığı 1–5'in dışına çıkaracak ödül kurulmaz.", 5), 1), C, C);
            Tile(s, C, C, TileModifierType.Explosive, TileRarity.Rare); Reward(s, "bereketli_ogrenim");
            list.Add(s);

            s = Field(New("24 · Kasırga / bumerang kapasitesi ve yoğun efekt",
                "İzle (otomatik saldırı açık, nişan merkezde, bütün tarla her saldırıda hasat edilir). Zamanı yavaşlatıp 'F. İnceleme'ye bak.",
                "Çok sayıda kasırga ve bumerang; aktif sayılar sınırda kalır.",
                "Aktif kasırga ve bumerang sayısı sınırı (inceleme: aktif / sınır) aşmaz; fazlası kayıtta 'reddedildi' diye sayılır. Oyun akmaya devam eder.", 7), 3);
            s.aimFixed = true; s.aimX = C; s.aimZ = C; s.autoAttack = true; s.plantHealth = 1;
            Radius(s, 9f);
            for (int x = C - 3; x <= C + 3; x++)
                for (int z = C - 3; z <= C + 3; z++) Tile(s, x, z, (x + z) % 2 == 0 ? TileModifierType.Tornado : TileModifierType.Boomerang, TileRarity.Legendary);
            Boost(s, StatType.TornadoChance); Boost(s, StatType.BoomerangChance);
            list.Add(s);

            return list;
        }

        // ---------------------------------------------------------------- kurucular
        internal static Scenario New(string name, string todo, string see, string check, int gridSize) =>
            new Scenario { name = name, whatToDo = todo, whatToSee = see, whatToCheck = check, gridSize = gridSize, profile = Lab.DefaultProfile, seed = 1, round = 1 };

        // Merkezin çevresindeki (2 × half + 1)² hücreye 1 × 1 saksı.
        internal static Scenario Field(Scenario scenario, int half)
        {
            string planter = Small();
            for (int x = C - half; x <= C + half; x++)
                for (int z = C - half; z <= C + half; z++) Planter(scenario, planter, x, z);
            return scenario;
        }

        // Elle, adım adım test: otomatik saldırı kapalı, nişan sabit hücrede.
        internal static Scenario Manual(Scenario scenario, int aimX, int aimZ)
        {
            scenario.autoAttack = false;
            scenario.aimFixed = true;
            scenario.aimX = aimX; scenario.aimZ = aimZ;
            return scenario;
        }

        private static Scenario Behavior(string name, TileModifierType tile, StatType chance, string see, string check)
        {
            Scenario s = Manual(Field(New(name,
                "'Tek saldırı'ya bas (yalnız merkez hücre; merkez bitkinin canı 1). Yeniden denemek için 'Bitki kurulumunu yeniden uygula'.",
                see, check, 5), 2), C, C);
            Radius(s, OneCell);
            Tile(s, C, C, tile, TileRarity.Legendary);
            Boost(s, chance);
            Plant(s, C, C, 1);
            return s;
        }

        private static Scenario Event(string name, string todo, string see, string check)
        {
            Scenario s = Manual(Field(New(name, todo, see, check, 7), 3), C, C);
            // Olayların normal takvimde çıkabildiği ilk round R26'dır ama R26 bir boss round'udur; boss kuralı karışmasın diye R27.
            s.round = 27;
            return s;
        }

        internal static void Planter(Scenario s, string planter, int x, int z, int rotation = 0) =>
            s.planters.Add(new ScenarioPlanter { planter = planter, x = x, z = z, rotation = rotation });

        internal static void Tile(Scenario s, int x, int z, TileModifierType type, TileRarity rarity, int level = 0) =>
            s.tiles.Add(new ScenarioTile { x = x, z = z, type = type, rarity = rarity, level = level, roll = 1f });

        internal static void Plant(Scenario s, int x, int z, int health) => s.plants.Add(new ScenarioPlant { x = x, z = z, plant = "", health = health });

        internal static void Stat(Scenario s, StatType stat, StatTarget target, ModifierOperation operation, float value) =>
            s.stats.Add(new ScenarioStat { stat = stat, target = target, operation = operation, value = value });

        internal static void Radius(Scenario s, float radius) => Stat(s, StatType.AreaRadius, StatTarget.Player, ModifierOperation.Set, radius);

        // Tile'dan gelen gerçek şansı ×20 çarpar: tile'ı olan saksı %100'e çıkar, olmayan 0'da kalır.
        internal static void Boost(Scenario s, StatType chance) => Stat(s, chance, StatTarget.Planter, ModifierOperation.MorePercent, 19f);

        internal static void Reward(Scenario s, string id) => s.rewards.Add(new ScenarioReward { id = id, stacks = 1 });

        internal static string Small() => LabCatalog.SmallestPlanter != null ? LabCatalog.SmallestPlanter.name : "GrassPlanter 1x1";

        internal static string LargePlanter(int sizeX, int sizeZ)
        {
            foreach (PlanterSO planter in LabCatalog.Planters)
                if (planter.sizeX == sizeX && planter.sizeZ == sizeZ) return planter.name;
            return Small();
        }

        // En küçük saksının üretim tablosundaki ilk Common bitki (sayısal kontroller: bitki zarı olmadan, hep aynı bitki).
        internal static string CommonPlant()
        {
            var table = new List<PlantSO>();
            LabCatalog.PlantsOf(LabCatalog.SmallestPlanter, table);
            table.Sort((x, y) => x.rarity != y.rarity ? x.rarity.CompareTo(y.rarity) : string.CompareOrdinal(x.name, y.name));
            foreach (PlantSO plant in table) if (plant.rarity == PlantRarity.Common) return plant.name;
            return table.Count > 0 ? table[0].name : "";
        }

        // En küçük saksının üretim tablosundaki ilk Rare ve üstü bitki (Nadir Reaktör için).
        internal static string RarePlant()
        {
            var table = new List<PlantSO>();
            LabCatalog.PlantsOf(LabCatalog.SmallestPlanter, table);
            table.Sort((a, b) => a.rarity.CompareTo(b.rarity));
            foreach (PlantSO plant in table) if (plant.rarity >= PlantRarity.Rare) return plant.name;
            return "";
        }
    }
}
