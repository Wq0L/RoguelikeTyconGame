using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Beklenen / gerçekleşen kontrol kataloğu. Her kontrol:
    //  - koşulu oyunun gerçek yollarıyla kurar (senaryo, ödülün alınabilirlik denetimi, gerçek tile, gerçek saldırı);
    //  - GERÇEKLEŞEN değeri oyundan ölçer (bitkinin canı, sayısal iz, hesap bakiyesi, saksının çözülmüş değeri, gerçek süre);
    //  - BEKLENEN değeri burada, açık aritmetikle üretir (Arith): girdiler asset değerleri ve işlemden önce ölçülmüş değerlerdir.
    //    Aynı üretim fonksiyonu iki tarafta birden çağrılmaz.
    // Otomatik karşılaştırılamayan kısımlar "manuel inceleme gerekli", gerçekleşmeyen olaylar "İNCELENMEDİ" olarak kalır.
    public static partial class NumericChecks
    {
        private const int C = ScenarioPresets.C;
        private const float Big = 1000f;
        private const string Rounding = "float çarpımının ,5 sınırı";

        // Kontrolün kullandığı hasat hasarı tabanı (test katkısı: Set).
        private static int damage;
        private static readonly List<TraceHit> hits = new();
        private static readonly List<Result> ignored = new();

        private static float Dt => Mathf.Max(Time.deltaTime, 1f / 120f);

        // ---------------------------------------------------------------- senaryo kurucuları
        private static Scenario Clean(string name, int half = 1, int grid = 5)
        {
            Scenario s = ScenarioPresets.Manual(ScenarioPresets.Field(ScenarioPresets.New("Kontrol · " + name, "", "", "", grid), half), C, C);
            ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
            s.plantFill = ScenarioPresets.CommonPlant();
            s.pauseProduction = true;
            return s;
        }

        private static void Set(Scenario s, StatType stat, float value) => ScenarioPresets.Stat(s, stat, StatTarget.Player, ModifierOperation.Set, value);
        private static void BigDamage(Scenario s) { Set(s, StatType.HarvestDamage, Big); damage = (int)Big; }
        private static void NoCrit(Scenario s) => Set(s, StatType.CritChance, 0f);
        private static void AllCrit(Scenario s) => Set(s, StatType.CritChance, 1f);

        private static void Refill()
        {
            ignored.Clear();
            ScenarioRunner.ApplyPlants(TestSession.Current, ignored);
        }

        private static StatManager Stats => StatManager.Instance;
        private static RoundManager Rounds => RoundManager.Instance;
        private static float PlayerStat(StatType stat) => Stats.GetFinalStat(stat, StatTarget.Player);

        // Komşular vuruştan sağ çıksın diye: R21'in dayanıklı bitkileri + bitki canının onda biri kadar hasat hasarı (test katkısı).
        private static IEnumerator Survive(CheckContext c, Scenario s)
        {
            s.round = 21;
            yield return c.Build(s);
            if (c.Aborted) yield break;
            PlantHealth plant = CheckContext.Plant(C, C);
            if (plant == null) { c.Skip("ön koşul", "merkezde bitki yok"); yield break; }
            damage = Mathf.Max(20, plant.MaxHealth / 10);
            Result added = TestStats.Add(new StatModifier { statType = StatType.HarvestDamage, target = StatTarget.Player, operation = ModifierOperation.Set, value = damage });
            if (!added.Ok) { c.Skip("ön koşul", added.Message); yield break; }
            c.Near("ön koşul: hasat hasarı stat'ı = test katkısı (Set)", damage, PlayerStat(StatType.HarvestDamage));
        }

        private static void Kill(int x, int z) => TestField.SetPlantHealth(new GridPosition(x, z), 1);

        private static void AllSame(CheckContext c, string name, List<TraceHit> list, long expected, string unseen)
        {
            if (list.Count == 0) { c.Unseen(name, unseen); return; }
            long worst = list[0].Computed;
            foreach (TraceHit hit in list) if (Math.Abs(hit.Computed - expected) > Math.Abs(worst - expected)) worst = hit.Computed;
            c.Near($"{name} ({list.Count} vuruş, en uzak değer)", expected, worst, 1d, Rounding);
        }

        // ---------------------------------------------------------------- gözlem yardımcıları (izden bağımsız)
        private readonly struct ProbeHit
        {
            public readonly double Time;
            public readonly DamageType Type;
            public readonly RewardSource Source;
            public readonly int Damage, HealthBefore;
            public readonly GridPosition Cell;
            public ProbeHit(double time, DamageType type, RewardSource source, int damage, int before, GridPosition cell)
            { Time = time; Type = type; Source = source; Damage = damage; HealthBefore = before; Cell = cell; }
        }

        private static readonly List<ProbeHit> probe = new();
        private static readonly List<double> attackTimes = new();
        private static bool probing;

        private sealed class AttackClock : DirectAttackListener
        {
            public override void Begin(DirectAttackReport attack, ref float multiplier) => attackTimes.Add(Time.timeAsDouble);
            public override void End(DirectAttackReport attack) { }
        }
        private static readonly AttackClock clock = new();

        private static void OnProbe(PlantHealth plant, int amount, DamageType type) =>
            probe.Add(new ProbeHit(Time.timeAsDouble, type, plant.LastHitLink.Source, amount, plant.CurrentHealth,
                FieldCells.Grid != null ? FieldCells.Grid.GetGridPosition(plant.transform.position) : default));

        private static void Probe(bool on)
        {
            if (on == probing) return;
            probing = on;
            if (on) { probe.Clear(); attackTimes.Clear(); PlantHealth.AnyDamaged += OnProbe; AttackFeed.Add(clock); }
            else { PlantHealth.AnyDamaged -= OnProbe; AttackFeed.Remove(clock); }
        }

        // ---------------------------------------------------------------- katalog
        private static CheckDefinition Def(string id, string group, string title, string precondition, string action, Func<CheckContext, IEnumerator> body,
            CheckCoverage coverage = CheckCoverage.Auto, string note = "") =>
            new CheckDefinition { Id = id, Group = group, Title = title, Precondition = precondition, Action = action, Body = body, Coverage = coverage, Note = note };

        private static List<CheckDefinition> BuildCatalog()
        {
            const string direct = "Doğrudan vuruş", stat = "Stat ödülleri", behavior = "Davranış hasarı", breakthrough = "Kırılma ödülleri", effect = "Etki ödülleri",
                antenna = "Anten ailesi", crescent = "Hilal ailesi", events = "Tarla olayları", boss = "Boss kuralları", economy = "XP / kaynak / skor", missing = "Henüz olmayan sistemler";
            const string one = "tek saksılık temiz düzen, hasat hasarı test katkısıyla 1000, kritik şansı 0, üretim duraklatılmış";
            const string survive = "R21, bitki canının onda biri kadar hasat hasarı (test katkısı): komşular vuruştan sağ çıkar";
            var list = new List<CheckDefinition>
            {
                Def("D01", direct, "Temel doğrudan vuruş ve gerçek can azalması", one, "tek gerçek saldırı", DirectBase),
                Def("D02", direct, "Kritik vuruş (ikinci yuvarlama)", one + "; kritik şansı 1", "tek gerçek saldırı", DirectCrit),
                Def("D03", direct, "Keskin Bıçak ×1 ve ×2 stack", one, "aynı seed'le önce / sonra saldırı", c => DirectReward(c, "keskin_bicak", 2)),
                Def("D04", direct, "Canavar Kesimi", one, "aynı seed'le önce / sonra saldırı", c => DirectReward(c, "canavar_kesimi", 1)),
                Def("D05", direct, "Altın Hedef: Rare+ hedefe çarpan, Common hedefe yok", one + "; bir Rare bitki", "Common ve Rare hedefe saldırı", RareTarget),
                Def("D06", direct, "Bereketli Öğrenim: doğrudan vuruş bedeli ve seçim hakkı", one, "önce / sonra saldırı, sonra normal akışla bir level", c => Costed(c, "bereketli_ogrenim")),
                Def("D07", direct, "Saksı hasar çarpanı (Damage tile) yalnız doğrudan vuruşa", one + "; merkezde Damage tile", "tek gerçek saldırı", PlanterBonus),

                Def("S01", stat, "Hızlı Bilek: stat ve ölçülen saldırı aralığı", "temiz düzen, otomatik saldırı ölçüm sırasında açık", "iki otomatik saldırının arası, önce / sonra", c => StatReward(c, "hizli_bilek", Use.Interval)),
                Def("S02", stat, "Fırtına Bileği: stat ve ölçülen saldırı aralığı", "temiz düzen", "iki otomatik saldırının arası, önce / sonra", c => StatReward(c, "firtina_bilegi", Use.Interval)),
                Def("S03", stat, "Geniş Savuruş: stat ve gerçek temas yarıçapı", "temiz düzen", "önce / sonra saldırı", c => StatReward(c, "genis_savurus", Use.Radius)),
                Def("S04", stat, "Bilgi Filizi: stat ve gerçek hasat XP'si", "temiz düzen", "önce / sonra tek hasat", c => StatReward(c, "bilgi_filizi", Use.Xp)),
                Def("S05", stat, "Ağır Darbe: kritik çarpanı ve gerçek kritik vuruş", "temiz düzen, kritik şansı 1", "önce / sonra kritik vuruş", c => StatReward(c, "agir_darbe", Use.CritMultiplier)),
                Def("S06", stat, "Kritik Göz: kritik şansı (gerçek saldırının kullandığı değer)", "temiz düzen", "önce / sonra saldırı", c => StatReward(c, "kritik_goz", Use.CritChance)),
                Def("S07", stat, "Kıvılcım: davranış şansları; şansı olmayan saksı şans kazanmaz", "biri patlama tile'lı iki saksı", "ödül öncesi / sonrası saksı değerleri", c => StatReward(c, "kivilcim", Use.BehaviorChance)),
                Def("S08", stat, "Nadir Tohum: nadirlik bonusu", "temiz düzen", "ödül öncesi / sonrası saksı değeri", c => StatReward(c, "nadir_tohum", Use.None),
                    CheckCoverage.Auto, "Bitki nadirlik zarının dağılımı otomatik doğrulanmıyor (yalnız stat)."),
                Def("S09", stat, "Bereketli Toprak: koşul ve gerçek üretim aralığı", "biten round'da tarla boş (doluluk < eşik)", "boş round oyunun round sonu akışıyla bitirilir, ödül round sonunda kurulur", LowField),

                Def("B01", behavior, "Patlama: dört yan komşu, çapraz yok, gerçek can azalması", survive, "merkez saksıyı hasat et", ExplosionBase),
                Def("B02", behavior, "Yıkım Gücü ×1 ve ×2 stack: davranış hasarı; doğrudan vuruş değişmez", survive, "önce / sonra patlama", c => BehaviorReward(c, "yikim_gucu", 2)),
                Def("B03", behavior, "Davranışa Adanış: davranış hasarı ve seçim hakkı bedeli", survive, "önce / sonra patlama, sonra normal akışla bir level", c => BehaviorReward(c, "davranisa_adanis", 1)),
                Def("B04", behavior, "Elektrik hasarı", survive, "merkez saksıyı hasat et", c => OtherBehavior(c, TileModifierType.Electric, StatType.ElectricChance, DamageType.Electric)),
                Def("B05", behavior, "Kasırga hasarı (davranışın kendi katsayısıyla)", survive, "merkez saksıyı hasat et", c => OtherBehavior(c, TileModifierType.Tornado, StatType.TornadoChance, DamageType.Tornado)),
                Def("B06", behavior, "Bumerang hasarı (davranışın kendi katsayısıyla)", survive, "merkez saksıyı hasat et", c => OtherBehavior(c, TileModifierType.Boomerang, StatType.BoomerangChance, DamageType.Boomerang)),

                Def("K01", breakthrough, "Artçı Patlama: gecikme, hasar oranı, yeni zar yok", survive, "merkez saksıyı hasat et", c => Echo(c, "artci_patlama", TileModifierType.Explosive, StatType.ExplosionChance, DamageType.Explosion)),
                Def("K02", breakthrough, "Çifte Akım: gecikme, hasar oranı, yeni zar yok", survive, "merkez saksıyı hasat et", c => Echo(c, "cifte_akim", TileModifierType.Electric, StatType.ElectricChance, DamageType.Electric)),
                Def("K03", breakthrough, "Hasat Ritmi: hak hazırlanır, harcanır, birikmez", "3 × 3 saldırı, bütün bitkiler 1 can", "iki saldırı", Rhythm),
                Def("K04", breakthrough, "Zincir Hasat: nesil hasar çarpanı ve nesil sınırı", "5 × 5 patlama tile'ı (şans testte %100), bütün bitkiler 1 can", "tek saldırı", Chain),

                Def("E01", effect, "Kritik Ark", survive + "; kritik şansı 1", "tek kritik vuruş", CritArc),
                Def("E02", effect, "Taşan Hasar", survive + "; merkez bitki 1 can", "tek saldırı", Overflow),
                Def("E03", effect, "Hasat Yankısı: simetrik hücreler", survive + "; 3 × 3 alanda bitkiler 1 can", "tek saldırı", HarvestEcho),
                Def("E04", effect, "Nadir Reaktör: eklenme, yenilenme (birikmez), süre sonu", "bir patlama tile'lı saksı, bir tile'sız saksı, merkezde Rare bitki", "iki nadir hasat, sonra süreyi bekle", Reactor),
                Def("E05", effect, "Açgözlü Hasat: hak ve saldırı çarpanı", "3 × 3 saldırı, bütün bitkiler 1 can", "iki saldırı", Greed),
                Def("E07", effect, "Aşırı Yükleme: gecikme ve hasar oranı", survive, "merkez saksıyı hasat et", Overload),

                Def("A01", antenna, "Elektrik Anteni: aralık ve hasar", survive, "saldırmadan iki anten vuruşunu bekle", c => Pulse(c, "elektrik_anteni", RewardSource.Antenna)),
                Def("A02", antenna, "İletken Hat: aralık ve hasar", survive, "saldırmadan iki hat vuruşunu bekle", c => Pulse(c, "iletken_hat", RewardSource.Line)),
                Def("A03", antenna, "Kapalı Devre: ek hat vuruşu, hasar ve gecikme", survive + "; saksıların patlama şansı testte %100", "hat vuruşundan hemen önce bir hat hücresinin bitkisini 1 cana çek", ClosedCircuit,
                    CheckCoverage.Auto, "Hat ve anten kendi aralıklarıyla çalışır; bitki hat vuruşundan hemen önce 1 cana çekilir. Araya anten vuruşu girerse sonraki hat vuruşu denenir; yakalanamazsa İNCELENMEDİ kalır."),

                Def("H01", crescent, "Hilal Darbesi: eşik ve hasar", survive + "; 3 × 3 saldırı", "tek saldırı", c => Crescent(c, 0)),
                Def("H02", crescent, "Büyüyen Ay: büyüyen adım hasarı", survive + "; 3 × 3 saldırı", "tek saldırı", c => Crescent(c, 1), CheckCoverage.Observed,
                    "Büyüme hilalin kendi hasatlarına bağlıdır; gözlenen her vuruşun izinli büyüme basamaklarından birine uyduğu karşılaştırılır. Cephe genişliği gözle bakılır."),
                Def("H03", crescent, "Dolunay: dönüş dalgası ve vuruş duraklaması", survive + "; 3 × 3 saldırı", "tek saldırı", c => Crescent(c, 2), CheckCoverage.Observed,
                    "Dönüş dalgası hilalin eşik kadar büyümesine bağlıdır; gözlenirse hasarı ve duraklama süresi karşılaştırılır."),
                Def("H04", crescent, "Hilal imleç rotası (güncel kural)", "", "", null, CheckCoverage.Observed,
                    "Rotanın imleçten açık kenara gitmesi bu araçta sayısal doğrulanmıyor: MoonCursorVerification.RunBatch ve gözle inceleme."),

                Def("F01", events, "Diken Kuşatması: kapalı hücreler ve kök dayanıklılığı", "R27, 7 × 7 dolu tarla", "olayı başlat, kökleri gerçek saldırıyla kır", ThornSiege),
                Def("F02", events, "İstilacı Filiz: yavaşlatma çarpanı gerçek üretim aralığında", "R27, 7 × 7 dolu tarla", "olayı başlat, sonra ana filizi kır", Sprout),
                Def("F03", events, "Aşılı Kabuk: seçim yapılmazsa bölge kapanır", "R27, 7 × 7 dolu tarla", "olayı başlat, vurmadan bekle", Shell, CheckCoverage.Observed,
                    "Yalnız 'seçim yapılmadı' yolu otomatik izlenir; düğüm kırma yolu (A / B) elle denenmeli."),
                Def("F04", events, "Gezgin Bereket: kaynak ×2, XP ×2, bölge dışında bonus yok", "R27, 7 × 7 dolu tarla", "bölge içinde ve dışında birer hasat", Bounty),

                Def("G01", boss, "Don Cephesi: bantta üretim aralığı çarpanı, kalkınca eski değer", "boss round'u, 7 × 7 dolu tarla", "boss'u başlat, sonra etkisini temizle", c => BossRule(c, 0)),
                Def("G02", boss, "Sert Kabuk: bantta bitki canı çarpanı, kalkınca eski değer", "boss round'u, 7 × 7 dolu tarla", "boss'u başlat, sonra etkisini temizle", c => BossRule(c, 1)),
                Def("G03", boss, "Sis: saldırı yarıçapı çarpanı, kalkınca eski değer", "boss round'u, 7 × 7 dolu tarla", "boss'u başlat, saldır, temizle", c => BossRule(c, 2)),

                Def("X01", economy, "Kontrollü tek hasat: kaynak, XP ve skor kaydı = gerçek bakiye farkı", "tek saksı, üretim duraklatılmış, otomatik saldırı kapalı", "tek hasat", SingleHarvest),
                Def("X02", economy, "Level kazanımı: kazanılan XP ≠ saklı XP, yeni seçim hakkı", "temiz düzen", "normal akışla bir level'lık XP ver", LevelGain),

                Def("M01", missing, "Final boss / run sonu sistemi", "", "", null, CheckCoverage.Missing, "sistem henüz yok: R50 akışı geçicidir."),
                Def("M02", missing, "Endless (sonsuz) mod", "", "", null, CheckCoverage.Missing, "sistem henüz yok."),
                Def("M03", missing, "Rezonans Çöküşü / Rezonans Darbesi", "", "", null, CheckCoverage.Missing, "sistem henüz yok: kapsam dışı bırakıldı, eklenmeyecek."),
                Def("M04", missing, "Hilal – rezonans etkileşim kuralı", "", "", null, CheckCoverage.Missing, "sistem henüz yok: kural kullanıcı onayı bekliyor."),
            };
            SourceChecks(list);
            return list;
        }

        // ================================================================ doğrudan vuruş
        private static long ExpectedDirect(TraceHit hit, double multiplier) => Arith.Round((double)hit.BaseDamage * hit.Variance * multiplier, 1);

        private static float PlanterBase(int x, int z)
        {
            PlanterBrain brain = CheckContext.Planter(x, z);
            return brain != null && brain.EditorData != null ? brain.EditorData.GetBaseStat(StatType.PlanterDamageMultiplier) : 1f;
        }

        private static IEnumerator DirectBase(CheckContext c)
        {
            Scenario s = Clean("temel doğrudan vuruş"); BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            PlantHealth plant = CheckContext.Plant(C, C);
            if (plant == null) { c.Skip("ön koşul", "merkezde bitki yok"); yield break; }
            int health = plant.CurrentHealth;
            float planter = PlanterBase(C, C);
            yield return c.Attack(C, C, 11);
            TraceHit hit = c.Direct(C, C);
            if (hit == null) { c.Skip("doğrudan vuruş", "merkezde vuruş kaydı yok"); yield break; }
            c.Near("vuruşun tabanı = test katkısı (Set 1000)", Big, hit.BaseDamage);
            c.True("sapma aralığı", "0,85 – 1,15", hit.Variance.ToString("0.####"), hit.Variance >= 0.85f && hit.Variance <= 1.15f);
            c.Near("tek yuvarlama = yuvarla(taban × sapma)", ExpectedDirect(hit, 1d), hit.DamageBeforeCrit, 1d, Rounding);
            c.True("kritik değil (şans 0)", "kritik yok", hit.Crit ? "kritik" : "kritik yok", !hit.Crit);
            c.Near("uygulanan darbe = yuvarla(yuvarlanan × saksı tabanı çarpanı)", Arith.Round(hit.DamageBeforeCrit * (double)planter), hit.Computed, 1d, Rounding);
            c.Exact("izdeki önceki can = saldırıdan önce okunan gerçek can", health, hit.HealthBefore);
            c.Exact("gerçek can azalması = min(önceki can, darbe)", Math.Min(health, hit.Computed), hit.Decrease);
            c.Exact("taşan hasar = darbe − önceki can", Math.Max(0, hit.Computed - health), hit.Overkill);
            c.Exact("tek hedef: vuruş sayısı", 1, c.Trace.Hits.Count);
            c.True("bitki gerçekten hasat edildi", "ölü", plant.IsDead ? "ölü" : "canlı " + plant.CurrentHealth, plant.IsDead == hit.Killed && hit.Killed);
        }

        private static IEnumerator DirectCrit(CheckContext c)
        {
            Scenario s = Clean("kritik vuruş"); BigDamage(s); AllCrit(s);
            yield return c.Build(s);
            float multiplier = Stats.GetBaseStat(StatType.CritMultiplier);
            yield return c.Attack(C, C, 12);
            TraceHit hit = c.Direct(C, C);
            if (hit == null) { c.Skip("doğrudan vuruş", "merkezde vuruş kaydı yok"); yield break; }
            c.True("kritik (şans 1)", "kritik", hit.Crit ? "kritik" : "kritik değil", hit.Crit);
            c.Near("vuruşun kullandığı kritik çarpanı = ham taban (katkı yok)", multiplier, hit.CritMultiplier);
            c.Near("birinci yuvarlama = yuvarla(taban × sapma)", ExpectedDirect(hit, 1d), hit.DamageBeforeCrit, 1d, Rounding);
            c.Near("ikinci yuvarlama = yuvarla(birinci × kritik çarpanı)", Arith.Round(hit.DamageBeforeCrit * (double)multiplier), hit.DamageAfterCrit, 1d, Rounding);
        }

        private static IEnumerator DirectReward(CheckContext c, string id, int stacks)
        {
            Scenario s = Clean(id); BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            yield return c.Attack(C, C, 21);
            TraceHit before = c.Direct(C, C);
            if (before == null) { c.Skip("doğrudan vuruş", "ödülsüz vuruş kaydı yok"); yield break; }
            c.Near("ödülsüz vuruş = yuvarla(taban × sapma)", ExpectedDirect(before, 1d), before.DamageBeforeCrit, 1d, Rounding);
            float behaviorBefore = BossRewardManager.BehaviorDamageMultiplier;
            BossRewardSO reward = c.Reward(id);
            if (reward == null) yield break;
            double product = 1d;
            for (int stack = 1; stack <= Mathf.Min(stacks, reward.maxStacks); stack++)
            {
                if (c.Grant(id) == null) yield break;
                product *= reward.directDamageMultiplier;
                Refill();
                yield return c.Attack(C, C, 21);
                TraceHit after = c.Direct(C, C);
                if (after == null) { c.Skip("doğrudan vuruş", "ödüllü vuruş kaydı yok"); yield break; }
                c.Near($"{stack} stack · aynı seed aynı sapma", before.Variance, after.Variance);
                c.Near($"{stack} stack · vuruş = yuvarla(taban × sapma × {reward.directDamageMultiplier:0.###}^{stack})", ExpectedDirect(after, product), after.DamageBeforeCrit, 1d, Rounding);
                c.Near($"{stack} stack · oyunun doğrudan çarpanı", product, BossRewardManager.DirectDamageMultiplier, 1e-4, "float çarpım");
            }
            c.Near("davranış hasarı çarpanı değişmedi (doğrudan çarpan davranışa sızmaz)", behaviorBefore, BossRewardManager.BehaviorDamageMultiplier);
        }

        private static IEnumerator RareTarget(CheckContext c)
        {
            Scenario s = Clean("altin_hedef"); BigDamage(s); NoCrit(s);
            string rare = ScenarioPresets.RarePlant();
            if (rare.Length == 0) { c.Skip("ön koşul", "en küçük saksının tablosunda Rare bitki yok"); yield break; }
            s.plants.Add(new ScenarioPlant { x = C - 1, z = C, plant = rare });
            yield return c.Build(s);
            BossRewardSO reward = c.Grant("altin_hedef");
            if (reward == null) yield break;
            yield return c.Attack(C, C, 31);
            TraceHit common = c.Direct(C, C);
            yield return c.Attack(C - 1, C, 31);
            TraceHit rareHit = c.Direct(C - 1, C);
            if (common == null || rareHit == null) { c.Skip("doğrudan vuruş", "vuruş kaydı yok"); yield break; }
            c.True("hedeflerin nadirliği", "Common ve " + reward.rareDirectFrom + "+", $"{common.Rarity} ve {rareHit.Rarity}", common.Rarity == PlantRarity.Common && rareHit.Rarity >= reward.rareDirectFrom);
            c.Near("Common hedef: çarpan yok", ExpectedDirect(common, 1d), common.DamageBeforeCrit, 1d, Rounding);
            c.Near($"Rare hedef: × {reward.rareDirectMultiplier:0.###}", ExpectedDirect(rareHit, reward.rareDirectMultiplier), rareHit.DamageBeforeCrit, 1d, Rounding);
        }

        // Seçim hakkı takası: çarpan + level başına seçim hakkı; bekleyen seçimler yeniden hesaplanmaz.
        private static IEnumerator Costed(CheckContext c, string id)
        {
            Scenario s = Clean(id); BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            int choices = Rounds.ChoicesPerLevel, pending = Rounds.PendingCardSelections;
            BossRewardSO reward = c.Grant(id);
            if (reward == null) yield break;
            yield return c.Attack(C, C, 41);
            TraceHit hit = c.Direct(C, C);
            if (hit == null) { c.Skip("doğrudan vuruş", "vuruş kaydı yok"); yield break; }
            c.Near($"doğrudan vuruş = yuvarla(taban × sapma × {reward.directDamageMultiplier:0.###})", ExpectedDirect(hit, reward.directDamageMultiplier), hit.DamageBeforeCrit, 1d, Rounding);
            ChoiceLines(c, reward, choices, pending);
        }

        private static void ChoiceLines(CheckContext c, BossRewardSO reward, int choices, int pending)
        {
            c.Exact($"level başına seçim hakkı = önceki {choices} {reward.levelChoiceDelta:+0;-0}", choices + reward.levelChoiceDelta, Rounds.ChoicesPerLevel);
            c.Exact("bekleyen seçimler yeniden hesaplanmadı", pending, Rounds.PendingCardSelections);
            ProgressionManager progression = ProgressionManager.Instance;
            int level = progression.CurrentLevel;
            double need = Math.Max(1d, progression.XPToNextLevel - progression.StoredXP);
            Result given = TestStats.GiveXP(need);
            if (!given.Ok) { c.Unseen("yeni level", given.Message); return; }
            c.Exact("normal akışla tam bir level", level + 1, progression.CurrentLevel);
            c.Exact("yeni level'ın verdiği seçim hakkı = yeni hak", pending + choices + reward.levelChoiceDelta, Rounds.PendingCardSelections);
        }

        private static IEnumerator PlanterBonus(CheckContext c)
        {
            Scenario s = Clean("saksı hasar çarpanı"); BigDamage(s); NoCrit(s);
            ScenarioPresets.Tile(s, C, C, TileModifierType.Damage, TileRarity.Legendary);
            ScenarioPresets.Tile(s, C + 1, C, TileModifierType.Explosive, TileRarity.Legendary);
            ScenarioPresets.Boost(s, StatType.ExplosionChance);
            yield return c.Build(s);
            GroundCell ground = TestField.Ground(new GridPosition(C, C));
            PlanterBrain brain = CheckContext.Planter(C, C);
            if (ground == null || brain == null || ground.RolledModifiers.Count == 0) { c.Skip("ön koşul", "merkezde Damage tile'ı kurulamadı"); yield break; }
            // Beklenen çarpan: saksı verisindeki ham taban + tile'ın gerçek zar değerleri, burada yazılı stat kuralıyla.
            double expected = Arith.Stat(brain.EditorData.GetBaseStat(StatType.PlanterDamageMultiplier), ground.RolledModifiers, StatType.PlanterDamageMultiplier);
            c.Near("saksının çözülmüş hasar çarpanı = stat kuralı(ham taban, tile değerleri)", expected, brain.GetFinalStat(StatType.PlanterDamageMultiplier), 1e-4, "float");
            c.True("çarpan gerçekten bonus veriyor", "> 1", expected.ToString("0.####"), expected > 1.0001d);
            yield return c.Attack(C, C, 51);
            TraceHit hit = c.Direct(C, C);
            if (hit == null) { c.Skip("doğrudan vuruş", "vuruş kaydı yok"); yield break; }
            c.Near("doğrudan vuruş: uygulanan = yuvarla(yuvarlanan × saksı çarpanı)", Arith.Round(hit.DamageBeforeCrit * expected), hit.Computed, 1d, Rounding);
            // Komşu saksı (patlama tile'ı, şans testte %100): onun patlaması Damage tile'lı saksının bitkisine saksı bonusu ALMADAN vurur.
            Refill();
            yield return c.Attack(C + 1, C, 52);
            c.Hits(DamageType.Explosion, hits);
            TraceHit onBonus = hits.Find(h => h.Target.x == C && h.Target.z == C);
            if (onBonus == null) c.Unseen("patlama, Damage tile'lı saksının bitkisine vurdu", "patlama vuruşu gözlenmedi");
            else c.Near("patlama vuruşu saksı bonusu almadı = yuvarla(hasat hasarı)", Arith.Round(Big), onBonus.Computed, 1d, Rounding);
        }

        // ================================================================ stat ödülleri
        private enum Use { None, Interval, Radius, Xp, CritMultiplier, CritChance, BehaviorChance }

        private static double Read(StatModifier modifier, PlanterBrain reference) =>
            modifier.target == StatTarget.Player || (modifier.target != StatTarget.Planter && Stats.HasBaseStat(modifier.statType) && Snapshots.UnitOf(modifier.statType) != ValueUnit.Chance)
                ? Stats.GetFinalStat(modifier.statType, StatTarget.Player)
                : reference.GetFinalStat(modifier.statType);

        private static IEnumerator MeasureInterval(CheckContext c, Action<double> result)
        {
            Probe(true);
            TestAttack.ResetCooldown();
            TestAttack.AutoAttack = true;
            bool timedOut = false;
            yield return LabRoutine.Until(() => attackTimes.Count >= 2, 60d, () => timedOut = true);
            TestAttack.AutoAttack = false;
            double interval = attackTimes.Count >= 2 ? attackTimes[1] - attackTimes[0] : double.NaN;
            Probe(false);
            if (timedOut) c.Skip("saldırı aralığı ölçümü", "iki otomatik saldırı gözlenmedi");
            result(interval);
        }

        private static IEnumerator StatReward(CheckContext c, string id, Use use)
        {
            Scenario s = Clean(id);
            if (use == Use.CritMultiplier) AllCrit(s);
            if (use == Use.CritMultiplier || use == Use.Xp || use == Use.CritChance) BigDamage(s);
            if (use == Use.BehaviorChance) ScenarioPresets.Tile(s, C, C, TileModifierType.Explosive, TileRarity.Rare);
            yield return c.Build(s);
            BossRewardSO reward = c.Reward(id);
            PlanterBrain reference = CheckContext.Planter(C, C), plain = CheckContext.Planter(C + 1, C);
            if (reward == null || reference == null || plain == null) { if (!c.Aborted) c.Skip("ön koşul", "referans saksı yok"); yield break; }
            if (reward.modifiersPerStack.Count == 0) { c.Skip("ödül verisi", "bu ödülün stat modifier'ı yok"); yield break; }

            var before = new List<double>();
            foreach (StatModifier modifier in reward.modifiersPerStack) before.Add(Read(modifier, reference));
            double realBefore = double.NaN, xpMultiplierBefore = 0d;
            TraceHit hitBefore = null;
            if (use == Use.Interval) yield return MeasureInterval(c, value => realBefore = value);
            else if (use != Use.None && use != Use.BehaviorChance)
            {
                yield return c.Attack(C, C, 61);
                hitBefore = c.Direct(C, C);
                if (c.Trace != null && c.Trace.Harvests.Count > 0) xpMultiplierBefore = c.Trace.Harvests[0].XpMultiplier;
                realBefore = c.Trace != null ? c.Trace.Radius : double.NaN;
                Refill();
            }
            if (c.Aborted) yield break;
            double plainChance = plain.GetFinalStat(StatType.ExplosionChance);

            if (c.Grant(id) == null) yield break;
            for (int i = 0; i < reward.modifiersPerStack.Count; i++)
            {
                StatModifier modifier = reward.modifiersPerStack[i];
                double expected = Arith.Apply(before[i], modifier);
                if (modifier.statType == StatType.PlantSpawnRate) expected = Math.Max(StatCalculator.MinimumSpawnInterval, expected);
                c.Near($"{modifier.statType} [{modifier.target}]: önceki {before[i]:0.####} → {TestStats.Describe(modifier)}", expected, Read(modifier, reference), 1e-4 * Math.Max(1d, Math.Abs(expected)), "float");
            }

            switch (use)
            {
                case Use.Interval:
                {
                    double realAfter = double.NaN;
                    yield return MeasureInterval(c, value => realAfter = value);
                    if (c.Aborted) yield break;
                    StatModifier modifier = reward.modifiersPerStack[0];
                    c.Near($"ölçülen saldırı aralığı (iki otomatik saldırının arası): önceki {realBefore:0.###} sn × {1f + modifier.value:0.###}", realBefore * (1d + modifier.value), realAfter,
                        2.5 * Dt, "kare süresi: saldırı kare sınırında atılır");
                    break;
                }
                case Use.Radius:
                {
                    yield return c.Attack(C, C, 61);
                    if (c.Aborted) yield break;
                    c.Near($"gerçek temas yarıçapı: önceki {realBefore:0.####} × {1f + reward.modifiersPerStack[0].value:0.###}", realBefore * (1d + reward.modifiersPerStack[0].value), c.Trace.Radius, 1e-4, "float");
                    break;
                }
                case Use.Xp:
                {
                    Economy economy = Economy.Read();
                    yield return c.Attack(C, C, 61);
                    if (c.Aborted) yield break;
                    if (c.Trace.Harvests.Count != 1 || !c.Trace.Harvests[0].HasReward) { c.Skip("hasat", "tek hasat kaydı oluşmadı"); yield break; }
                    TraceHarvest harvest = c.Trace.Harvests[0];
                    double expected = Math.Round(harvest.BaseXp * xpMultiplierBefore * (1d + reward.modifiersPerStack[0].value) * harvest.RarityXpMultiplier, MidpointRounding.ToEven);
                    c.Near($"hasat XP'si = yuvarla(taban {harvest.BaseXp} × önceki çarpan {xpMultiplierBefore:0.###} × {1f + reward.modifiersPerStack[0].value:0.###} × nadirlik çarpanı)", expected, harvest.XpFinal, 1d, Rounding);
                    c.Near("kazanılan toplam XP'nin gerçek artışı = kayıttaki hasat XP'si", harvest.XpFinal, Economy.Read().XpEarned - economy.XpEarned, 0.01, "double");
                    break;
                }
                case Use.CritMultiplier:
                {
                    yield return c.Attack(C, C, 61);
                    TraceHit hit = c.Direct(C, C);
                    if (hit == null || hitBefore == null) { if (!c.Aborted) c.Skip("kritik vuruş", "vuruş kaydı yok"); yield break; }
                    double expected = Arith.Apply(hitBefore.CritMultiplier, reward.modifiersPerStack[0]);
                    c.Near("gerçek vuruşun kullandığı kritik çarpanı", expected, hit.CritMultiplier, 1e-4, "float");
                    c.Near("kritik vuruş = yuvarla(birinci yuvarlama × yeni çarpan)", Arith.Round(hit.DamageBeforeCrit * expected), hit.DamageAfterCrit, 1d, Rounding);
                    break;
                }
                case Use.CritChance:
                {
                    yield return c.Attack(C, C, 61);
                    TraceHit hit = c.Direct(C, C);
                    if (hit == null || hitBefore == null) { if (!c.Aborted) c.Skip("vuruş", "vuruş kaydı yok"); yield break; }
                    c.Near("gerçek vuruşun kullandığı kritik şansı", Arith.Apply(hitBefore.CritChance, reward.modifiersPerStack[0]), hit.CritChance, 1e-4, "float");
                    c.Manual("kritik zarının dağılımı", "tek vuruş dağılımı kanıtlamaz; şans mekanikleri için 'Şans' sekmesindeki örneklemeyi kullan");
                    break;
                }
                case Use.BehaviorChance:
                    c.Near("tile'ı olmayan saksının patlama şansı 0 kaldı (0 × çarpan = 0)", 0d, plain.GetFinalStat(StatType.ExplosionChance));
                    c.Near("tile'sız saksı ödülden önce de 0'dı", 0d, plainChance);
                    c.Manual("şansın gerçek tetik oranı", "istatistiksel: 'Şans' sekmesindeki örneklemeyle bakılır");
                    break;
            }
        }

        // Bereketli Toprak: koşul "önceki round'da tarla boşalıyordu". Koşul gerçek akışla üretilir: boş tarlayla bir round bitirilir.
        private static IEnumerator LowField(CheckContext c)
        {
            Scenario s = Clean("bereketli_toprak");
            s.plantFill = "empty";
            yield return c.Build(s);
            BossRewardSO reward = c.Reward("bereketli_toprak");
            BossRewardManager boss = BossRewardManager.Instance;
            if (reward == null) yield break;
            Result early = TestRewards.Grant(reward);
            c.True("koşul sağlanmadan ödül reddedilir (tarla dolu sayılır: doluluk 1)", "reddedildi", early.Ok ? "kuruldu" : "reddedildi", !early.Ok);
            if (early.Ok) yield break;
            // Boş tarlayla doluluk örnekleri toplanır (yarım saniyede bir), sonra round oyunun round sonu yoluyla biter.
            yield return LabRoutine.Seconds(1.6d);
            if (!Rounds.EditorEndRound(false)) { c.Skip("ön koşul", "round bitirilemedi"); yield break; }
            yield return LabRoutine.Frames(3);
            c.True("biten round'un doluluğu eşiğin altında", $"< {reward.conditionThreshold:0.##}", boss.LastRoundOccupancy.ToString("0.###"), boss.LastRoundOccupancy < reward.conditionThreshold);
            // Ödül round sonunda, yeni round başlamadan kurulur (oyunun teklifi de bu anda değerlendirilir). "Round'a git" test komutu
            // round sonu temizliğini yeniden çalıştırıp doluluk kaydını 1'e döndürdüğü için burada kullanılmaz.
            PlantSpawner spawner = CheckContext.Spawner(C, C);
            PlanterBrain brain = CheckContext.Planter(C, C);
            if (spawner == null || brain == null) { c.Skip("ön koşul", "round sonunda saksı yok"); yield break; }
            double statBefore = brain.GetFinalStat(StatType.PlantSpawnRate), intervalBefore = spawner.EditorInterval;
            Result granted = TestRewards.Grant(reward);
            if (!granted.Ok) { c.Skip("ödül kurulumu", granted.Message); yield break; }
            c.True("koşul sağlanınca ödül kuruldu", "kuruldu", "kuruldu", true);
            StatModifier modifier = reward.modifiersPerStack[0];
            double expected = Math.Max(StatCalculator.MinimumSpawnInterval, Arith.Apply(statBefore, modifier));
            c.Near($"üretim aralığı stat'ı: önceki {statBefore:0.###} sn → {TestStats.Describe(modifier)} (taban {StatCalculator.MinimumSpawnInterval:0.##} sn)", expected, brain.GetFinalStat(StatType.PlantSpawnRate), 1e-4, "float");
            c.Near("yuvanın gerçek üretim aralığı aynı oranda değişti", intervalBefore * (expected / statBefore), spawner.EditorInterval, 1e-3, "float");
        }

        // ================================================================ davranış hasarı
        private static Scenario BehaviorScenario(string name, TileModifierType tile, StatType chance)
        {
            Scenario s = Clean(name, 2);
            NoCrit(s);
            ScenarioPresets.Tile(s, C, C, tile, TileRarity.Legendary);
            ScenarioPresets.Boost(s, chance);
            return s;
        }

        private static IEnumerator ExplosionBase(CheckContext c)
        {
            yield return Survive(c, BehaviorScenario("patlama", TileModifierType.Explosive, StatType.ExplosionChance));
            if (c.Aborted) yield break;
            Kill(C, C);
            var before = new Dictionary<int, int>();
            for (int x = C - 1; x <= C + 1; x++)
                for (int z = C - 1; z <= C + 1; z++)
                {
                    PlantHealth plant = CheckContext.Plant(x, z);
                    if (plant != null) before[x * 100 + z] = plant.CurrentHealth;
                }
            yield return c.Attack(C, C, 71);
            if (c.Aborted) yield break;
            c.Hits(DamageType.Explosion, hits);
            c.Exact("patlama vuruşu sayısı (dört yan komşu)", 4, hits.Count);
            int side = 0, diagonal = 0;
            foreach (TraceHit hit in hits)
            {
                int dx = Math.Abs(hit.Target.x - C), dz = Math.Abs(hit.Target.z - C);
                if (dx + dz == 1) side++; else diagonal++;
            }
            c.Exact("yan komşuya vuruş", 4, side);
            c.Exact("çapraz komşuya vuruş", 0, diagonal);
            AllSame(c, "vuruş hasarı = yuvarla(hasat hasarı × 1) — çarpan yok", hits, Arith.Round(damage), "patlama vuruşu yok");
            // Gerçek can: komşuların canı saldırıdan önce ve sonra doğrudan bitkiden okunur.
            bool allMatch = hits.Count > 0;
            string detail = "";
            foreach (TraceHit hit in hits)
            {
                PlantHealth plant = CheckContext.Plant(hit.Target.x, hit.Target.z);
                before.TryGetValue(hit.Target.x * 100 + hit.Target.z, out int was);
                int now = plant != null ? plant.CurrentHealth : 0;
                if (was - now != hit.Decrease || was != hit.HealthBefore) { allMatch = false; detail += $"({hit.Target.x},{hit.Target.z}) gerçek {was}→{now}, iz {hit.HealthBefore}→{hit.HealthAfter}; "; }
            }
            c.True("izdeki can azalması = bitkilerin gerçek can azalması", "hepsi eşit", allMatch ? "hepsi eşit" : detail, allMatch);
            TraceHit directHit = c.Direct(C, C);
            c.True("doğrudan vuruş davranış sayılmadı; davranış vuruşu kritik değil", "direct 1, kritik davranış 0", $"direct {(directHit != null ? 1 : 0)}", directHit != null && !hits.Exists(h => h.Crit));
        }

        private static IEnumerator BehaviorReward(CheckContext c, string id, int stacks)
        {
            yield return Survive(c, BehaviorScenario(id, TileModifierType.Explosive, StatType.ExplosionChance));
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Reward(id);
            if (reward == null) yield break;
            int choices = Rounds.ChoicesPerLevel, pending = Rounds.PendingCardSelections;
            float directBefore = BossRewardManager.DirectDamageMultiplier;
            double product = 1d;
            for (int stack = 1; stack <= Mathf.Min(stacks, reward.maxStacks); stack++)
            {
                if (c.Grant(id) == null) yield break;
                product *= reward.behaviorDamageMultiplier;
                Refill();
                Kill(C, C);
                yield return c.Attack(C, C, 72);
                if (c.Aborted) yield break;
                c.Hits(DamageType.Explosion, hits);
                AllSame(c, $"{stack} stack · patlama vuruşu = yuvarla(hasat hasarı × {reward.behaviorDamageMultiplier:0.###}^{stack})", hits, Arith.Round(damage * product), "patlama vuruşu yok");
                TraceHit directHit = c.Direct(C, C);
                if (directHit != null)
                    c.Near($"{stack} stack · doğrudan vuruş davranış çarpanını almadı", ExpectedDirect(directHit, reward.directDamageMultiplier), directHit.DamageBeforeCrit, 1d, Rounding);
            }
            if (Mathf.Approximately(reward.directDamageMultiplier, 1f))
                c.Near("doğrudan vuruş çarpanı değişmedi", directBefore, BossRewardManager.DirectDamageMultiplier);
            if (reward.levelChoiceDelta != 0) ChoiceLines(c, reward, choices, pending);
        }

        private static IEnumerator OtherBehavior(CheckContext c, TileModifierType tile, StatType chance, DamageType type)
        {
            yield return Survive(c, BehaviorScenario(type.ToString(), tile, chance));
            if (c.Aborted) yield break;
            Kill(C, C);
            int rolled = HarvestBehaviorStats.Triggered(type);
            yield return c.Attack(C, C, 73);
            if (c.Aborted) yield break;
            c.Exact("normal tetik sayısı (oyunun sayacı)", 1, HarvestBehaviorStats.Triggered(type) - rolled);
            c.Hits(type, hits);
            if (hits.Count == 0) { c.Unseen("davranış vuruşu", $"{type} hiçbir bitkiye değmedi (hareketli davranış yolu boş geçmiş olabilir)"); yield break; }
            TraceEmission emission = hits[0].Emission;
            if (emission == null) { c.Unseen("davranış hesabı", "hesap anı kayıtta yok"); yield break; }
            c.Exact("davranışın tabanı = hasat hasarı stat'ı (tam sayı)", damage, emission.BaseDamage);
            // Davranışın kendi katsayısı prefab verisidir; oyun onu hesap anında bildirir (kasırga, bumerang). Çarpan yok: ×1.
            AllSame(c, $"vuruş hasarı = yuvarla(hasat hasarı × davranış katsayısı {emission.TypeCoefficient:0.###})", hits, Arith.Round(damage * (double)emission.TypeCoefficient, 1), "vuruş yok");
            if (type == DamageType.Tornado || type == DamageType.Boomerang)
                c.Manual("katsayının prefab verisiyle aynı olduğu", "katsayı oyunun bildirdiği değerdir; prefab alanı bu kontrolde ayrıca okunmuyor");
        }

        // ================================================================ kırılma ödülleri
        private static IEnumerator Echo(CheckContext c, string id, TileModifierType tile, StatType chance, DamageType type)
        {
            Scenario s = BehaviorScenario(id, tile, chance);
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant(id);
            if (reward == null) yield break;
            Kill(C, C);
            int rolled = HarvestBehaviorStats.Triggered(type);
            yield return c.Attack(C, C, 81);
            if (c.Aborted) yield break;
            c.Hits(type, hits);
            List<TraceHit> first = hits.FindAll(h => !h.Echo), second = hits.FindAll(h => h.Echo);
            if (first.Count == 0) { c.Unseen("ilk darbe", "davranış vuruşu gözlenmedi"); yield break; }
            long firstDamage = first[0].Computed;
            c.Near("ilk darbe = yuvarla(hasat hasarı)", Arith.Round(damage), firstDamage, 1d, Rounding);
            AllSame(c, $"ikinci darbe = yuvarla(ilk darbenin hesaplanmış hasarı × {reward.echoDamage:0.###})", second, Arith.Round(firstDamage * (double)reward.echoDamage, 1), "ikinci darbe hiçbir bitkiye değmedi");
            if (second.Count > 0)
                c.Near($"gecikme = ödül verisi ({reward.echoDelay:0.###} sn)", reward.echoDelay, second[0].Time - first[0].Time, 2.5 * Dt, "kare süresi");
            c.Exact("şans zarı bir kez atıldı (ikinci darbe yeni zar atmaz)", 1, HarvestBehaviorStats.Triggered(type) - rolled);
            c.True("ikinci darbe kaynağı: ilk darbenin saksısı", "bu saldırıya bağlı", second.Count > 0 && second.TrueForAll(h => h.Linked) ? "bu saldırıya bağlı" : "bağ yok / vuruş yok", second.Count == 0 || second.TrueForAll(h => h.Linked));
        }

        private static IEnumerator Rhythm(CheckContext c)
        {
            Scenario s = Clean("hasat_ritmi", 2); BigDamage(s); NoCrit(s);
            ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
            s.stats.RemoveAll(e => e.stat == StatType.AreaRadius && e.value == ScenarioPresets.OneCell);
            s.plantHealth = 1;
            yield return c.Build(s);
            BossRewardSO reward = c.Grant("hasat_ritmi");
            if (reward == null) yield break;
            HarvestRhythm rhythm = RunPower.Rhythm;
            c.True("kurulunca koşullu etki hazır değil", "hak yok", rhythm.Ready ? "hak hazır" : "hak yok", rhythm.Enabled && !rhythm.Ready);
            yield return c.Attack(C, C, 82);
            if (c.Aborted) yield break;
            double normalRadius = c.Trace.Radius;
            int harvests = c.Trace.Harvests.Count;
            c.True($"eşik ({reward.rhythmHarvests} doğrudan hasat) aşıldı", $"≥ {reward.rhythmHarvests}", harvests.ToString(), harvests >= reward.rhythmHarvests);
            c.True("hak hazırlandı", "hak hazır", rhythm.Ready ? "hak hazır" : "hak yok", rhythm.Ready);
            c.Exact("aynı vuruştaki fazla hasat ikinci hak biriktirmedi", 1, rhythm.Charges);
            TraceHit normal = c.Trace.Hits.Count > 0 ? c.Trace.Hits[0] : null;
            if (normal != null) c.Near("hakkı hazırlayan saldırı güçlenmedi (çarpan 1)", 1d, normal.RhythmMultiplier);
            Refill();
            yield return c.Attack(C, C, 83);
            if (c.Aborted) yield break;
            c.Near($"güçlenen saldırının gerçek yarıçapı = normal {normalRadius:0.###} × {reward.rhythmRadius:0.###}", normalRadius * reward.rhythmRadius, c.Trace.Radius, 1e-3, "float");
            TraceHit strong = c.Trace.Hits.Find(h => h.Type == DamageType.Direct);
            if (strong == null) { c.Unseen("güçlenen vuruş", "vuruş kaydı yok"); yield break; }
            c.Near("güçlenen vuruşun çarpanı = ödül verisi", reward.rhythmDamage, strong.RhythmMultiplier, 1e-4, "float");
            c.Near($"güçlenen vuruş = yuvarla(taban × sapma × {reward.rhythmDamage:0.###})", ExpectedDirect(strong, reward.rhythmDamage), strong.DamageBeforeCrit, 1d, Rounding);
            c.True("hak harcandı", "hak yok", rhythm.Ready ? "hak hazır" : "hak yok", !rhythm.Ready);
            c.Exact("güçlenen saldırı sayısı", 1, rhythm.EmpoweredAttacks);
            c.Exact("güçlenen saldırının hasatları yeni hak biriktirmedi", 1, rhythm.Charges);
        }

        private static IEnumerator Chain(CheckContext c)
        {
            Scenario s = Clean("zincir_hasat", 2); BigDamage(s); NoCrit(s);
            s.plantHealth = 1;
            for (int x = C - 2; x <= C + 2; x++) for (int z = C - 2; z <= C + 2; z++) ScenarioPresets.Tile(s, x, z, TileModifierType.Explosive, TileRarity.Legendary);
            ScenarioPresets.Boost(s, StatType.ExplosionChance);
            yield return c.Build(s);
            BossRewardSO reward = c.Grant("zincir_hasat");
            if (reward == null) yield break;
            yield return c.Attack(C, C, 84);
            if (c.Aborted) yield break;
            c.Hits(DamageType.Explosion, hits);
            int maxGeneration = 0;
            foreach (TraceHit hit in hits) maxGeneration = Math.Max(maxGeneration, hit.Generation);
            c.True($"nesil sınırı ({reward.chainGenerations} ek nesil)", $"≤ {reward.chainGenerations}", maxGeneration.ToString(), maxGeneration <= reward.chainGenerations);
            AllSame(c, "normal tetik (nesil 0) = yuvarla(hasat hasarı)", hits.FindAll(h => h.Generation == 0), Arith.Round(Big), "nesil 0 vuruşu yok");
            for (int generation = 1; generation <= reward.chainGenerations && generation <= reward.chainDamage.Length; generation++)
            {
                int g = generation;
                AllSame(c, $"nesil {g} = yuvarla(normal hasar × {reward.chainDamage[g - 1]:0.###}) — önceki nesille birikmez", hits.FindAll(h => h.Generation == g),
                    Arith.Round(Big * (double)reward.chainDamage[g - 1], 1), $"nesil {g} zinciri bu seed'de tutmadı");
            }
            c.True("zincir vuruşları bu saldırının köküne bağlı", "hepsi aynı kök", hits.TrueForAll(h => h.Root == c.Trace.ChainRoot) ? "hepsi aynı kök" : "farklı kök var", c.Trace.ChainRoot != 0 && hits.TrueForAll(h => h.Root == c.Trace.ChainRoot));
            c.Manual("aynı saksı + davranış aynı kökte bir kez denenir", "izdeki 'zincir zarı' / 'zincir reddi' satırlarından okunur");
        }

        // ================================================================ etki ödülleri
        private static IEnumerator CritArc(CheckContext c)
        {
            Scenario s = Clean("kritik_ark", 2); AllCrit(s);
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant("kritik_ark");
            CritArcEffectSO data = c.Effect<CritArcEffectSO>(reward);
            if (data == null) yield break;
            yield return c.Attack(C, C, 91);
            TraceHit crit = c.Direct(C, C);
            if (crit == null) { if (!c.Aborted) c.Skip("kritik vuruş", "vuruş kaydı yok"); yield break; }
            c.True("vuruş kritik", "kritik", crit.Crit ? "kritik" : "değil", crit.Crit);
            c.Hits(DamageType.Reward, hits, RewardSource.CritArc);
            c.Exact("ark sayısı (tek kritik → tek ark; sınır " + data.maxArcsPerAttack + ")", 1, hits.Count);
            if (hits.Count == 0) yield break;
            TraceHit arc = hits[0];
            c.Near($"ark hasarı = yuvarla(kritik vuruşun hesaplanmış hasarı × {data.damageRatio:0.###})", Arith.Round(crit.DamageAfterCrit * (double)data.damageRatio, 1), arc.Computed, 1d, Rounding);
            int distance = Math.Max(Math.Abs(arc.Target.x - C), Math.Abs(arc.Target.z - C));
            c.True($"ark başka bir bitkiye, {data.rangeCells} hücre içinde", $"1 – {data.rangeCells}", distance.ToString(), distance >= 1 && distance <= data.rangeCells);
            c.True("ark kritik atmadı", "kritik değil", arc.Crit ? "kritik" : "kritik değil", !arc.Crit);
        }

        private static IEnumerator Overflow(CheckContext c)
        {
            Scenario s = Clean("tasan_hasar", 2); NoCrit(s);
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant("tasan_hasar");
            OverflowEffectSO data = c.Effect<OverflowEffectSO>(reward);
            if (data == null) yield break;
            Kill(C, C);
            yield return c.Attack(C, C, 92);
            TraceHit hit = c.Direct(C, C);
            if (hit == null) { if (!c.Aborted) c.Skip("vuruş", "vuruş kaydı yok"); yield break; }
            c.Exact("taşan hasar = darbe − 1 can", hit.Computed - 1, hit.Overkill);
            c.Hits(DamageType.Reward, hits, RewardSource.Overflow);
            c.Exact("aktarım sayısı (hedef sağ kaldı: tek aktarım)", 1, hits.Count);
            if (hits.Count == 0) yield break;
            c.Near($"aktarılan = yuvarla(taşan × {data.ratio:0.###})", Arith.Round(hit.Overkill * (double)data.ratio), hits[0].Computed, 1d, Rounding);
            int distance = Math.Max(Math.Abs(hits[0].Target.x - C), Math.Abs(hits[0].Target.z - C));
            c.True($"hedef {data.rangeCells} hücre içinde", $"1 – {data.rangeCells}", distance.ToString(), distance >= 1 && distance <= data.rangeCells);
        }

        private static IEnumerator HarvestEcho(CheckContext c)
        {
            Scenario s = Clean("hasat_yankisi", 3, 7); NoCrit(s);
            s.stats.RemoveAll(e => e.stat == StatType.AreaRadius);
            ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
            s.aimX = C - 2; s.aimZ = C - 2;
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant("hasat_yankisi");
            HarvestEchoEffectSO data = c.Effect<HarvestEchoEffectSO>(reward);
            if (data == null) yield break;
            for (int x = C - 3; x <= C - 1; x++) for (int z = C - 3; z <= C - 1; z++) Kill(x, z);
            yield return c.Attack(C - 2, C - 2, 93);
            if (c.Aborted) yield break;
            var harvested = new HashSet<int>();
            foreach (TraceHarvest harvest in c.Trace.Harvests) harvested.Add(harvest.Cell.x * 100 + harvest.Cell.z);
            c.True($"eşik ({data.minHarvests} doğrudan hasat)", $"≥ {data.minHarvests}", harvested.Count.ToString(), harvested.Count >= data.minHarvests);
            c.Hits(DamageType.Reward, hits, RewardSource.HarvestEcho);
            if (hits.Count == 0) { c.Unseen("yankı vuruşu", "yankı gözlenmedi"); yield break; }
            c.True($"yankı sayısı sınırı (en çok {data.maxCells})", $"1 – {data.maxCells}", hits.Count.ToString(), hits.Count <= data.maxCells);
            AllSame(c, $"yankı hasarı = yuvarla(hasat hasarı × {data.damage:0.###})", hits, Arith.Round(damage * (double)data.damage, 1), "yankı yok");
            // Simetri: tarlanın ortasına göre (x, z) → (2·merkez − x, 2·merkez − z).
            bool mirrored = hits.TrueForAll(h => harvested.Contains((2 * C - h.Target.x) * 100 + (2 * C - h.Target.z)));
            c.True("her yankı, hasat edilen bir hücrenin simetriğinde", "hepsi simetrik", mirrored ? "hepsi simetrik" : "simetrik olmayan hedef var", mirrored);
            TraceHit last = c.Trace.Hits.FindLast(h => h.Type == DamageType.Direct);
            if (last != null) c.True($"gecikme en az ödül verisi kadar ({data.delay:0.###} sn)", $"≥ {data.delay:0.###}", (hits[0].Time - last.Time).ToString("0.###"), hits[0].Time - last.Time >= data.delay - 1e-4);
        }

        private static IEnumerator Reactor(CheckContext c)
        {
            Scenario s = Clean("nadir_reaktor", 2); BigDamage(s); NoCrit(s);
            ScenarioPresets.Tile(s, C - 1, C, TileModifierType.Explosive, TileRarity.Common);
            string rare = ScenarioPresets.RarePlant();
            if (rare.Length == 0) { c.Skip("ön koşul", "en küçük saksının tablosunda Rare bitki yok"); yield break; }
            s.plants.Add(new ScenarioPlant { x = C, z = C, plant = rare });
            yield return c.Build(s);
            BossRewardSO reward = c.Grant("nadir_reaktor");
            RareReactorEffectSO data = c.Effect<RareReactorEffectSO>(reward);
            PlanterBrain tiled = CheckContext.Planter(C - 1, C), plain = CheckContext.Planter(C + 1, C);
            RareReactorEffect reactor = RunEffects.Instance != null ? RunEffects.Instance.Find<RareReactorEffect>() : null;
            if (data == null || tiled == null || plain == null || reactor == null) { if (!c.Aborted) c.Skip("ön koşul", "reaktör ya da saksı yok"); yield break; }
            double chance = tiled.GetFinalStat(StatType.ExplosionChance);
            c.True("tile'lı saksının şansı var, tile'sızın yok", "> 0 ve 0", $"{chance:0.####} ve {plain.GetFinalStat(StatType.ExplosionChance):0.####}", chance > 0d && plain.GetFinalStat(StatType.ExplosionChance) == 0f);
            c.True("kurulunca koşullu etki beklemede", "aktif değil, çarpan 0", $"{(reactor.IsActive ? "aktif" : "aktif değil")}, çarpan {BuffWatch.ReactorModifiers(out _)}", !reactor.IsActive && BuffWatch.ReactorModifiers(out _) == 0);

            yield return c.Attack(C, C, 94);
            if (c.Aborted) yield break;
            double boosted = Math.Min(1d, chance * (1d + data.chanceBonus));
            c.True("doğrudan nadir hasat reaktörü çalıştırdı", "aktif", reactor.IsActive ? "aktif" : "aktif değil", reactor.IsActive);
            c.Near($"tile'lı saksının şansı = önceki {chance:0.####} × {1f + data.chanceBonus:0.###}", boosted, tiled.GetFinalStat(StatType.ExplosionChance), 1e-4, "float");
            c.Near("tile'sız saksı şans kazanmadı", 0d, plain.GetFinalStat(StatType.ExplosionChance));
            c.Exact("eklenen şans çarpanı sayısı (dört davranış şansı için birer tane)", 4, BuffWatch.ReactorModifiers(out _));
            double elapsed = c.Trace.EndTime - c.Trace.StartTime;
            c.Near("kalan süre = ödül verisi − geçen süre", data.seconds - elapsed, reactor.Remaining, 3d * Dt, "kare süresi");

            yield return LabRoutine.Seconds(1d);
            TestField.SetPlant(new GridPosition(C, C), LabCatalog.Plant(rare));
            int refreshes = reactor.Refreshes;
            yield return c.Attack(C, C, 95);
            if (c.Aborted) yield break;
            c.Exact("süre içindeki ikinci nadir hasat: yenileme sayısı", refreshes + 1, reactor.Refreshes);
            c.Exact("yenilemede çarpan sayısı aynı kaldı (birikmedi)", 4, BuffWatch.ReactorModifiers(out _));
            c.Near("yenilemede şans aynı kaldı (çarpan katlanmadı)", boosted, tiled.GetFinalStat(StatType.ExplosionChance), 1e-4, "float");
            elapsed = c.Trace.EndTime - c.Trace.StartTime;
            c.Near("süre baştan başladı (uzamadı, sıfırlandı)", data.seconds - elapsed, reactor.Remaining, 3d * Dt, "kare süresi");

            yield return LabRoutine.Seconds(reactor.Remaining + 0.3d);
            c.True("süre bitince reaktör kapandı", "aktif değil", reactor.IsActive ? "aktif" : "aktif değil", !reactor.IsActive);
            c.Near("şans eski değerine döndü", chance, tiled.GetFinalStat(StatType.ExplosionChance), 1e-5, "float");
            c.Exact("geride şans çarpanı kalmadı", 0, BuffWatch.ReactorModifiers(out _));
        }

        private static IEnumerator Greed(CheckContext c)
        {
            Scenario s = Clean("acgozlu_hasat", 2); BigDamage(s); NoCrit(s);
            s.stats.RemoveAll(e => e.stat == StatType.AreaRadius);
            ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
            s.plantHealth = 1;
            yield return c.Build(s);
            BossRewardSO reward = c.Grant("acgozlu_hasat");
            GreedyHarvestEffectSO data = c.Effect<GreedyHarvestEffectSO>(reward);
            GreedyHarvestEffect greed = RunEffects.Instance != null ? RunEffects.Instance.Find<GreedyHarvestEffect>() : null;
            if (data == null || greed == null) { if (!c.Aborted) c.Skip("ön koşul", "etki kayıtlı değil"); yield break; }
            c.True("kurulunca koşullu etki tetiklenmemiş", "hak yok", greed.Ready ? "hak hazır" : "hak yok", !greed.Ready);
            yield return c.Attack(C, C, 96);
            if (c.Aborted) yield break;
            c.True($"eşik ({data.minHarvests} doğrudan hasat) aşıldı ve hak hazırlandı", "hak hazır", $"{c.Trace.Harvests.Count} hasat, {(greed.Ready ? "hak hazır" : "hak yok")}", c.Trace.Harvests.Count >= data.minHarvests && greed.Ready);
            TraceHit first = c.Trace.Hits.Find(h => h.Type == DamageType.Direct);
            if (first != null) c.Near("hakkı hazırlayan saldırı güçlenmedi", 1d, first.FeedMultiplier);
            Refill();
            int spent = greed.Spent;
            yield return c.Attack(C, C, 97);
            if (c.Aborted) yield break;
            TraceHit strong = c.Trace.Hits.Find(h => h.Type == DamageType.Direct);
            if (strong == null) { c.Unseen("güçlenen vuruş", "vuruş kaydı yok"); yield break; }
            c.Near("ödül saldırı çarpanı = ödül verisi", data.damageMultiplier, strong.FeedMultiplier, 1e-4, "float");
            c.Near($"güçlenen vuruş = yuvarla(taban × sapma × {data.damageMultiplier:0.###})", ExpectedDirect(strong, data.damageMultiplier), strong.DamageBeforeCrit, 1d, Rounding);
            c.Exact("hak harcandı", spent + 1, greed.Spent);
        }

        private static IEnumerator Overload(CheckContext c)
        {
            yield return Survive(c, BehaviorScenario("asiri_yukleme", TileModifierType.Explosive, StatType.ExplosionChance));
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant("asiri_yukleme");
            OverloadEffectSO data = c.Effect<OverloadEffectSO>(reward);
            if (data == null) yield break;
            Kill(C, C);
            yield return c.Attack(C, C, 99);
            if (c.Aborted) yield break;
            // Ek proc, saksının patlamasını zincir dışı bağlamla yeniden çalıştırır: vuruşları patlama türündedir, kaynağı Aşırı Yükleme'dir.
            c.Hits(DamageType.Explosion, hits);
            List<TraceHit> original = hits.FindAll(h => h.Reward != RewardSource.Overload), repeat = hits.FindAll(h => h.Reward == RewardSource.Overload);
            if (original.Count == 0) { c.Unseen("patlama", "patlama vuruşu gözlenmedi"); yield break; }
            TraceHit explosion = original[0];
            AllSame(c, $"ek proc = yuvarla(patlamanın hesaplanmış hasarı × {data.damageFactor:0.###})", repeat, Arith.Round(explosion.Computed * (double)data.damageFactor, 1), "ek proc gözlenmedi");
            if (repeat.Count > 0)
            {
                c.Near($"gecikme = ödül verisi ({data.delay:0.###} sn)", data.delay, repeat[0].Time - explosion.Time, 2.5 * Dt, "kare süresi");
                c.Exact("ek proc aynı patlamanın tekrarı: aynı sayıda hedef (komşular sağ kaldı)", original.Count, repeat.Count);
                c.True("ek proc zincir dışı (kök taşımaz) ve kritik değil", "kök 0, kritik yok", $"kök {repeat[0].Root}, {(repeat.Exists(h => h.Crit) ? "kritik var" : "kritik yok")}", repeat.TrueForAll(h => h.Root == 0 && !h.Crit));
            }
            OverloadEffect overload = RunEffects.Instance != null ? RunEffects.Instance.Find<OverloadEffect>() : null;
            if (overload != null) c.Exact("ek proc yeni Aşırı Yükleme hakkı doğurmadı (verilen hak sayısı)", 1, overload.Granted);
            c.Manual("bekleme süresi ve saldırı başına sınır", $"aynı saksı + davranış {data.cooldown:0.#} sn içinde yeniden hak doğurmaz; ardışık saldırıyla elle bakılır");
        }

        // ================================================================ anten ailesi
        private static IEnumerator PulseSetup(CheckContext c, string id, bool certainBehavior = false)
        {
            Scenario s = Clean(id, 2); NoCrit(s);
            for (int x = C - 2; x <= C + 2; x++) for (int z = C - 2; z <= C + 2; z++) ScenarioPresets.Tile(s, x, z, TileModifierType.Explosive, TileRarity.Common);
            if (certainBehavior) ScenarioPresets.Boost(s, StatType.ExplosionChance);
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Reward(id);
            if (reward == null) yield break;
            var report = new List<Result>();
            Result granted = TestRewards.GrantWithPrerequisites(reward, report);
            if (!granted.Ok) c.Skip("ödül kurulumu (ön koşul ödülleriyle)", granted.Message);
        }

        private static IEnumerator Pulse(CheckContext c, string id, RewardSource source)
        {
            yield return PulseSetup(c, id);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Reward(id);
            float interval, coefficient;
            if (reward.effect is AntennaEffectSO antennaData) { interval = antennaData.interval; coefficient = antennaData.damage; }
            else if (reward.effect is ConductiveLineEffectSO lineData) { interval = lineData.interval; coefficient = lineData.damage; }
            else { c.Skip("ödül verisi", "beklenen etki verisi yok"); yield break; }
            Probe(true);
            var times = new List<double>();
            bool timedOut = false;
            yield return LabRoutine.Until(() =>
            {
                times.Clear();
                foreach (ProbeHit hit in probe)
                    if (hit.Type == DamageType.Reward && hit.Source == source && (times.Count == 0 || hit.Time - times[times.Count - 1] > 0.2d)) times.Add(hit.Time);
                return times.Count >= 2;
            }, 90d, () => timedOut = true);
            var seen = new List<ProbeHit>(probe.FindAll(h => h.Type == DamageType.Reward && h.Source == source));
            Probe(false);
            if (timedOut || times.Count < 2) { c.Unseen("iki ardışık vuruş", $"{source} vuruşu iki kez gözlenmedi (erişimde canlı bitki olmayabilir)"); yield break; }
            c.Near($"iki vuruş arası = ödül verisi ({interval:0.###} sn)", interval, times[1] - times[0], 2.5 * Dt, "kare süresi");
            long expected = Arith.Round(damage * (double)coefficient, 1);
            ProbeHit worst = seen[0];
            foreach (ProbeHit hit in seen) if (Math.Abs(hit.Damage - expected) > Math.Abs(worst.Damage - expected)) worst = hit;
            c.Near($"vuruş hasarı = yuvarla(hasat hasarı × {coefficient:0.###}) ({seen.Count} vuruş, en uzak değer)", expected, worst.Damage, 1d, Rounding);
            c.True("ödül darbesi doğrudan vuruş değil", "DamageType.Reward", worst.Type.ToString(), true);
        }

        private static IEnumerator ClosedCircuit(CheckContext c)
        {
            // Saksıların patlama şansı testte %100: Kapalı Devre'nin "o saksının davranış şansıyla" zarı kesin tutar (test katkısı).
            yield return PulseSetup(c, "kapali_devre", true);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Reward("kapali_devre"), lineReward = c.Reward("iletken_hat");
            if (!(reward.effect is ClosedCircuitEffectSO data) || !(lineReward.effect is ConductiveLineEffectSO line)) { c.Skip("ödül verisi", "beklenen etki verisi yok"); yield break; }
            ClosedCircuitEffect circuit = RunEffects.Instance != null ? RunEffects.Instance.Find<ClosedCircuitEffect>() : null;
            if (circuit == null) { c.Skip("ön koşul", "Kapalı Devre etkisi kayıtlı değil"); yield break; }
            Probe(true);
            // 1) İlk hat vuruşu: hattın gerçek hücreleri ve antenin vurduğu hücreler oyundan öğrenilir.
            bool timedOut = false;
            yield return LabRoutine.Until(() => probe.Exists(h => h.Type == DamageType.Reward && h.Source == RewardSource.Line), 90d, () => timedOut = true);
            if (timedOut) { Probe(false); c.Unseen("hat vuruşu", "hat hiçbir bitkiye değmedi"); yield break; }
            yield return LabRoutine.Frames(1);
            var lineCells = new List<GridPosition>();
            var antennaCells = new HashSet<int>();
            foreach (ProbeHit hit in probe)
            {
                if (hit.Type != DamageType.Reward) continue;
                if (hit.Source == RewardSource.Antenna) antennaCells.Add(hit.Cell.x * 100 + hit.Cell.z);
                else if (hit.Source == RewardSource.Line && !lineCells.Exists(cell => cell.x == hit.Cell.x && cell.z == hit.Cell.z)) lineCells.Add(hit.Cell);
            }
            c.True("hat en az iki bitkiye vurdu (biri hasat edilecek, diğeri ek vuruşu alacak)", "≥ 2", lineCells.Count.ToString(), lineCells.Count >= 2);
            if (lineCells.Count < 2) { Probe(false); yield break; }
            // 2) Anten de aynı hücrelere vurabilir ve hattan daha sık çalışır. Bitki, sıradaki hat vuruşundan HEMEN önce 1 cana çekilir;
            //    o kısa aralıkta bir anten vuruşu bekleniyorsa bir sonraki hat vuruşu denenir. Zamanlar oyunun gerçek vuruşlarından okunur.
            float antennaInterval = c.Reward("elektrik_anteni").effect is AntennaEffectSO antennaData ? antennaData.interval : 0f;
            double lineTime = probe.FindLast(h => h.Type == DamageType.Reward && h.Source == RewardSource.Line).Time;
            GridPosition victim = default;
            int marker = -1, extras = 0, rolls = 0;
            for (int attempt = 1; attempt <= 4 && marker < 0; attempt++)
            {
                double nextLine = lineTime + line.interval * attempt, margin = 3d * Dt;
                yield return LabRoutine.Until(() => Time.timeAsDouble >= nextLine - margin, 60d);
                double now = Time.timeAsDouble;
                if (now > nextLine) continue;
                bool antennaSoon = false;
                int lastAntenna = probe.FindLastIndex(h => h.Type == DamageType.Reward && h.Source == RewardSource.Antenna);
                if (lastAntenna >= 0 && antennaInterval > 0f)
                    for (int k = 1; k <= 4; k++)
                    {
                        double pulse = probe[lastAntenna].Time + antennaInterval * k;
                        if (pulse >= now - Dt && pulse <= nextLine + Dt) antennaSoon = true;
                    }
                if (antennaSoon) continue;
                int alive = lineCells.FindIndex(cell => CheckContext.Plant(cell.x, cell.z) != null);
                if (alive < 0) break;
                victim = lineCells[alive];
                Kill(victim.x, victim.z);
                marker = probe.Count; extras = circuit.Extras; rolls = circuit.Rolls;
            }
            if (marker < 0) { Probe(false); c.Unseen("hat hasadı", "anten vuruşu araya girmeden bir hat vuruşu yakalanamadı"); yield break; }
            yield return LabRoutine.Seconds(4d * Dt + data.extraDelay + 0.6d);
            List<ProbeHit> later = probe.GetRange(marker, probe.Count - marker);
            Probe(false);
            int killIndex = later.FindIndex(h => h.Type == DamageType.Reward && h.Source == RewardSource.Line && h.Cell.x == victim.x && h.Cell.z == victim.z && h.HealthBefore <= h.Damage);
            if (killIndex < 0)
            {
                string seen = "";
                foreach (ProbeHit hit in later) if (hit.Cell.x == victim.x && hit.Cell.z == victim.z) seen += $"{hit.Source} {hit.Damage} (can {hit.HealthBefore}); ";
                c.Unseen("hat hasadı", $"seçilen bitkiyi ({victim.x}, {victim.z}) hat hasat etmedi · o hücredeki vuruşlar: {(seen.Length > 0 ? seen : "yok")} · penceredeki hat vuruşu {later.FindAll(h => h.Source == RewardSource.Line).Count}");
                yield break;
            }
            ProbeHit kill = later[killIndex];
            c.Exact("hat, davranışlı saksının bitkisini hasat edince zar atıldı (oyunun sayacı)", rolls + 1, circuit.Rolls);
            List<ProbeHit> extra = later.FindAll(h => h.Type == DamageType.Reward && h.Source == RewardSource.LineExtra);
            c.Exact("ek hat vuruşu sayısı (oyunun sayacı; şans testte %100)", extras + 1, circuit.Extras);
            if (extra.Count == 0) { c.Unseen("ek hat vuruşu", "ek vuruş hiçbir bitkiye değmedi"); yield break; }
            long expected = Arith.Round(Arith.Round(damage * (double)line.damage, 1) * (double)data.extraDamage, 1);
            c.Near($"ek vuruş hasarı = yuvarla(hat hasarı × {data.extraDamage:0.###})", expected, extra[0].Damage, 1d, Rounding);
            c.Near($"ek vuruşun gecikmesi = ödül verisi ({data.extraDelay:0.###} sn)", data.extraDelay, extra[0].Time - kill.Time, 2.5 * Dt, "kare süresi");
            c.Manual("bekleme süresi", $"ek vuruştan sonra {data.cooldown:0.#} sn yeni ek vuruş doğmaz; elle bakılır");
        }

        private static List<PlanterBrain> TestFieldPlanters()
        {
            var planters = new List<PlanterBrain>();
            TestField.Planters(planters);
            return planters;
        }

        // ================================================================ hilal ailesi
        // level 0: yalnız Hilal · 1: + Büyüyen Ay · 2: + Dolunay
        private static IEnumerator Crescent(CheckContext c, int level)
        {
            Scenario s = Clean("hilal", 3, 7); NoCrit(s);
            s.stats.RemoveAll(e => e.stat == StatType.AreaRadius);
            ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
            yield return Survive(c, s);
            if (c.Aborted) yield break;
            BossRewardSO reward = c.Grant("hilal_darbesi");
            CrescentEffectSO data = c.Effect<CrescentEffectSO>(reward);
            if (data == null) yield break;
            GrowingMoonEffectSO growth = null;
            FullMoonEffectSO full = null;
            if (level >= 1) { growth = c.Effect<GrowingMoonEffectSO>(c.Grant("buyuyen_ay")); if (growth == null) yield break; }
            if (level >= 2) { full = c.Effect<FullMoonEffectSO>(c.Grant("dolunay")); if (full == null) yield break; }
            // Büyüme hilalin kendi hasatlarına bağlıdır: yolundaki bitkiler 1 cana çekilir ki dalga hasat edebilsin.
            if (level >= 1)
                foreach (PlanterBrain brain in TestFieldPlanters())
                    foreach (PlantSpawner spawner in brain.EditorSpawners)
                        if (spawner != null && spawner.SpawnedPlant != null && spawner.SpawnedPlant.TryGetComponent(out PlantHealth plant))
                        {
                            GridPosition cell = FieldCells.Grid.GetGridPosition(plant.transform.position);
                            if (Math.Abs(cell.x - C) > 1 || Math.Abs(cell.z - C) > 1) plant.EditorSetHealth(1);
                        }
            bool paused = false;
            double pauseSeen = 0d;
            yield return c.Attack(C, C, 101, false);
            if (c.Aborted) yield break;
            // Dolunay duraklaması iz sürerken okunur (oyunun kendi durumu).
            yield return LabRoutine.Until(() =>
            {
                if (DirectAttackPause.Active) { paused = true; pauseSeen = Math.Max(pauseSeen, DirectAttackPause.Remaining); }
                return TestTrace.Current == null && !TestTrace.Armed;
            }, 60d);
            yield return c.WaitTrace();
            if (c.Aborted) yield break;
            int plantHits = c.Trace.Hits.FindAll(h => h.Type == DamageType.Direct).Count;
            c.True($"eşik ({data.everyAttacks} gerçek bitki isabeti)", $"≥ {data.everyAttacks}", plantHits.ToString(), plantHits >= data.everyAttacks);
            c.Hits(DamageType.Reward, hits, RewardSource.Crescent);
            long baseDamage = Arith.Round(damage * (double)data.damage, 1);
            if (hits.Count == 0) { c.Unseen("hilal vuruşu", "hilal dalgası hiçbir bitkiye değmedi"); yield break; }
            if (level == 0) { AllSame(c, $"hilal hasarı = yuvarla(hasat hasarı × {data.damage:0.###})", hits, baseDamage, "hilal vuruşu yok"); yield break; }

            // Büyüyen adımın hasarı: taban × (1 + büyüme başına oran × düzey), düzey 0 … azami.
            var allowed = new List<long>();
            for (int k = 0; k <= growth.maxGrowth; k++) allowed.Add(Arith.Round(baseDamage * (1d + growth.damagePerGrowth * (double)k), 1));
            TraceHit odd = hits.Find(h => !allowed.Exists(a => Math.Abs(a - h.Computed) <= 1));
            c.True($"her hilal vuruşu izinli büyüme basamaklarından biri ({string.Join(" / ", allowed)})", "hepsi basamakta", odd == null ? "hepsi basamakta" : $"basamak dışı hasar {odd.Computed}", odd == null);
            bool grew = hits.Exists(h => Math.Abs(h.Computed - baseDamage) > 1);
            if (!grew) c.Unseen("büyüme", "bu saldırıda hilal hasat etmedi: büyüyen adım gözlenmedi");
            else c.True("en az bir adım büyüdü", "tabandan büyük vuruş", "gözlendi", true);
            c.Manual("cephe genişliği", $"büyüdükçe cephe en çok {growth.maxHalfWidth * 2 + 1} hücre olur; gözle bakılır");
            if (level < 2) yield break;

            var back = new List<TraceHit>();
            c.Hits(DamageType.Reward, back, RewardSource.CrescentReturn);
            if (back.Count == 0) { c.Unseen("dönüş dalgası", $"hilal {full.growthThreshold}. büyümeye ulaşmadı ya da dönüş dalgası bitkiye değmedi"); yield break; }
            AllSame(c, $"dönüş dalgası = yuvarla(hilalin TABAN hasarı × {full.returnDamage:0.###})", back, Arith.Round(baseDamage * (double)full.returnDamage, 1), "dönüş vuruşu yok");
            c.True("doğrudan saldırı duraklaması gözlendi", "aktif oldu", paused ? "aktif oldu" : "gözlenmedi", paused);
            if (paused) c.True($"duraklama ödül verisini aşmadı ({full.pauseSeconds:0.###} sn)", $"≤ {full.pauseSeconds:0.###}", pauseSeen.ToString("0.###"), pauseSeen <= full.pauseSeconds + 1e-4);
        }

        // ================================================================ tarla olayları
        private static Scenario EventScenario(string name)
        {
            Scenario s = Clean(name, 3, 7); NoCrit(s);
            s.round = 27;
            s.pauseProduction = false;
            return s;
        }

        private static IEnumerator StartEvent(CheckContext c, FieldEventKind kind, FieldEventPhase phase, GridPosition? anchor = null)
        {
            Result started = TestEvents.StartEvent(kind, 0f, 1, anchor);
            FieldEventDirector director = FieldEventDirector.Instance;
            if (!started.Ok || director == null || director.Current == null) { c.Skip("olay", started.Message); yield break; }
            bool timedOut = false;
            yield return LabRoutine.Until(() => director.Current == null || director.Current.Phase >= phase, 120d, () => timedOut = true);
            if (timedOut || director.Current == null || director.Current.Phase != phase) c.Skip("olay fazı", $"olay {phase} fazına ulaşmadı");
        }

        private static IEnumerator ThornSiege(CheckContext c)
        {
            yield return c.Build(EventScenario("diken kuşatması"));
            if (c.Aborted) yield break;
            FieldEventDirector director = FieldEventDirector.Instance;
            ThornSiegeConfig config = director.Plan.thornSiege;
            c.Exact("olaydan önce kapalı hücre", 0, director.Zones.BlockedCells);
            yield return StartEvent(c, FieldEventKind.ThornSiege, FieldEventPhase.Active, new GridPosition(C, C));
            if (c.Aborted) yield break;
            FieldEvent current = director.Current;
            IReadOnlyList<GridPosition> zone = current.Zone(0);
            c.Exact($"bölge boyutu = plan verisi ({config.zoneWidth} × {config.zoneHeight})", config.zoneWidth * config.zoneHeight, zone.Count);
            c.Exact("aktif fazda kapalı hücre sayısı = bölge", zone.Count, director.Zones.BlockedCells);
            // Kapalı hücrede üretim süresi ilerlemez, bölge dışında ilerler (gerçek üretim sayaçları).
            PlantSpawner inside = null, outside = null;
            foreach (PlanterBrain brain in TestFieldPlanters())
                foreach (PlantSpawner spawner in brain.EditorSpawners)
                {
                    if (spawner == null) continue;
                    GridPosition cell = brain.EditorOrigin;
                    bool inZone = false;
                    foreach (GridPosition z in zone) if (z.x == cell.x && z.z == cell.z) inZone = true;
                    if (inZone) inside ??= spawner; else outside ??= spawner;
                }
            if (inside != null && outside != null)
            {
                inside.EditorClearPlant(); outside.EditorClearPlant();
                float a = inside.EditorTimer, b = outside.EditorTimer;
                yield return LabRoutine.Seconds(0.5d);
                c.Near("kapalı hücrede üretim süresi ilerlemedi", a, inside.EditorTimer, 1e-4, "float");
                c.True("bölge dışında üretim süresi ilerledi", "> önceki", $"{b:0.###} → {outside.EditorTimer:0.###}", outside.EditorTimer > b + 0.2f || outside.SpawnedPlant != null);
            }
            c.Exact("kök sayısı", 2, current.Targets.Count);
            foreach (InterventionTarget target in current.Targets)
            {
                c.Exact("kök dayanıklılığı = plan verisi", config.rootDurability, target.Durability);
                int attacks = 0;
                while (!target.Broken && attacks < config.rootDurability + 2)
                {
                    TestAttack.ResetCooldown();
                    if (!TestAttack.AttackOnce(TestAttack.WorldOf(target.Cell)).Ok) break;
                    attacks++;
                    yield return LabRoutine.Frames(1);
                }
                c.Exact("kökü kıran gerçek doğrudan saldırı sayısı", config.rootDurability, attacks);
            }
            c.Exact("kökler kırılınca kapalı hücre kalmadı", 0, director.Zones.BlockedCells);
            c.True("olay oyuncu tarafından çözüldü", "çözüldü", current.ResolvedByPlayer ? "çözüldü" : "çözülmedi", current.ResolvedByPlayer);
            TestEvents.EndEvent();
        }

        private static IEnumerator Sprout(CheckContext c)
        {
            yield return c.Build(EventScenario("istilacı filiz"));
            if (c.Aborted) yield break;
            FieldEventDirector director = FieldEventDirector.Instance;
            InvasiveSproutConfig config = director.Plan.invasiveSprout;
            PlantSpawner spawner = CheckContext.Spawner(C, C);
            if (spawner == null) { c.Skip("ön koşul", "merkezde üretim noktası yok"); yield break; }
            double normal = spawner.EditorInterval;
            yield return StartEvent(c, FieldEventKind.InvasiveSprout, FieldEventPhase.Active, new GridPosition(C, C));
            if (c.Aborted) yield break;
            FieldEvent current = director.Current;
            c.True("ana filiz seçilen üretim noktasında", $"({C}, {C})", $"({current.Targets[0].Cell.x}, {current.Targets[0].Cell.z})", current.Targets[0].Cell.x == C && current.Targets[0].Cell.z == C);
            bool timedOut = false;
            yield return LabRoutine.Until(() => director.Zones.SlowedCells > 0, 60d, () => timedOut = true);
            if (timedOut) { c.Unseen("yavaşlama", "yavaşlamış hücre oluşmadı"); TestEvents.EndEvent(); yield break; }
            // Yavaşlamış bir hücrenin gerçek üretim aralığı (oyunun kullandığı değer) = normal × plan çarpanı.
            PlantSpawner slowed = null;
            foreach (PlanterBrain brain in TestFieldPlanters())
            {
                GridPosition cell = brain.EditorOrigin;
                if (FieldEventDirector.SpawnIntervalMultiplier(cell) > 1.0001f && brain.EditorSpawners.Count > 0) { slowed = brain.EditorSpawners[0]; break; }
            }
            if (slowed == null) c.Unseen("yavaşlamış yuva", "yavaşlamış hücrede üretim noktası bulunamadı");
            else c.Near($"yavaşlamış yuvanın gerçek üretim aralığı = normal {normal:0.###} sn × {config.slowMultiplier:0.###}", normal * config.slowMultiplier, slowed.EditorInterval, 1e-3, "float");
            InterventionTarget main = current.Targets[0];
            c.Exact("ana filiz dayanıklılığı = plan verisi", config.mainDurability, main.Durability);
            int attacks = 0;
            while (!main.Broken && attacks < config.mainDurability + 2)
            {
                TestAttack.ResetCooldown();
                if (!TestAttack.AttackOnce(TestAttack.WorldOf(main.Cell)).Ok) break;
                attacks++;
                yield return LabRoutine.Frames(1);
            }
            c.Exact("ana filizi kıran gerçek doğrudan saldırı sayısı", config.mainDurability, attacks);
            c.Exact("ana filiz kırılınca yavaş hücre kalmadı", 0, director.Zones.SlowedCells);
            if (slowed != null) c.Near("üretim aralığı eski değerine döndü", normal, slowed.EditorInterval, 1e-3, "float");
            TestEvents.EndEvent();
        }

        private static IEnumerator Shell(CheckContext c)
        {
            yield return c.Build(EventScenario("aşılı kabuk"));
            if (c.Aborted) yield break;
            FieldEventDirector director = FieldEventDirector.Instance;
            GraftedShellConfig config = director.Plan.graftedShell;
            yield return StartEvent(c, FieldEventKind.GraftedShell, FieldEventPhase.Active);
            if (c.Aborted) yield break;
            FieldEvent current = director.Current;
            c.Exact("bölge sayısı", 2, current.ZoneCount);
            c.Exact("seçim öncesi kapalı hücre", 0, director.Zones.BlockedCells);
            c.Exact("düğüm dayanıklılığı = plan verisi", config.targetDurability, current.Targets.Count > 0 ? current.Targets[0].Durability : -1);
            bool timedOut = false;
            yield return LabRoutine.Until(() => director.Current != current || director.Zones.BlockedCells > 0, 120d, () => timedOut = true);
            if (timedOut || director.Current != current) { c.Unseen("kapanma", "seçim süresi dolduğunda bölge kapanmadı"); TestEvents.EndEvent(); yield break; }
            c.True("seçim yapılmayınca A bölgesi kapandı", "A kapalı, B açık", $"A {(current.ZoneClosed(0) ? "kapalı" : "açık")}, B {(current.ZoneClosed(1) ? "kapalı" : "açık")}", current.ZoneClosed(0) && !current.ZoneClosed(1));
            c.Exact("kapalı hücre sayısı = A bölgesi", current.Zone(0).Count, director.Zones.BlockedCells);
            c.Manual("düğüm kırma yolu", "A ya da B düğümü kırılınca diğer bölgenin kapanması elle denenir");
            TestEvents.EndEvent();
            c.Exact("olay temizlenince kapalı hücre kalmadı", 0, director.Zones.BlockedCells);
        }

        private static IEnumerator Bounty(CheckContext c)
        {
            Scenario s = EventScenario("gezgin bereket"); BigDamage(s);
            s.pauseProduction = true;
            yield return c.Build(s);
            if (c.Aborted) yield break;
            FieldEventDirector director = FieldEventDirector.Instance;
            yield return StartEvent(c, FieldEventKind.WanderingBounty, FieldEventPhase.Active);
            if (c.Aborted) yield break;
            GridPosition? inside = null, outside = null;
            foreach (PlanterBrain brain in TestFieldPlanters())
            {
                GridPosition cell = brain.EditorOrigin;
                if (TestField.PlantAt(cell) == null) continue;
                if (FieldEventDirector.BountyAt(cell) != null) inside ??= cell; else outside ??= cell;
            }
            if (!inside.HasValue || !outside.HasValue) { c.Skip("ön koşul", "bölge içinde ve dışında birer bitki bulunamadı"); yield break; }
            PlantSpawner fast = TestField.SpawnerAt(inside.Value), slow = TestField.SpawnerAt(outside.Value);
            if (fast != null && slow != null)
                c.Manual("bölgede boş yuvalar hızlı dolar", $"oyun sabiti ×{WanderingBountyEvent.SpawnSpeed:0.#}; üretim hızlanması bu kontrolde ölçülmüyor (hızlanan doğuş sayısı olay durumunda yazılır)");

            yield return c.Attack(outside.Value.x, outside.Value.z, 111);
            if (c.Aborted) yield break;
            if (c.Trace.Harvests.Count != 1 || !c.Trace.Harvests[0].HasReward) { c.Skip("hasat", "bölge dışı tek hasat kaydı oluşmadı"); yield break; }
            TraceHarvest plain = c.Trace.Harvests[0];
            c.True("bölge dışında bonus yok", "bereket hayır", plain.Bounty ? "bereket EVET" : "bereket hayır", !plain.Bounty);
            c.Exact("bölge dışı: eklenen kaynak = ölçekten sonraki", plain.RewardAfterSpecialization, plain.RewardFinal);

            Economy before = Economy.Read();
            yield return c.Attack(inside.Value.x, inside.Value.z, 112);
            if (c.Aborted) yield break;
            if (c.Trace.Harvests.Count != 1 || !c.Trace.Harvests[0].HasReward) { c.Skip("hasat", "bölge içi tek hasat kaydı oluşmadı"); yield break; }
            TraceHarvest rich = c.Trace.Harvests[0];
            Economy after = Economy.Read();
            if (!rich.Bounty) { c.Unseen("bölge içi hasat", "bölge hasat anında bu hücreden ayrılmış (gezgin bölge)"); yield break; }
            c.Exact("bölge içi: eklenen kaynak = 2 × ölçekten sonraki", 2L * rich.RewardAfterSpecialization, rich.RewardFinal);
            c.Near("bölge içi: eklenen XP = 2 × yuvarlanan", 2d * rich.XpRounded, rich.XpFinal, 0.01, "double");
            c.Exact("gerçek kaynak bakiyesi kayıt kadar arttı", rich.RewardFinal, after.Resource(rich.Resource) - before.Resource(rich.Resource));
            c.Near("gerçek kazanılan XP kayıt kadar arttı", rich.XpFinal, after.XpEarned - before.XpEarned, 0.01, "double");
            TestEvents.EndEvent();
        }

        // ================================================================ boss kuralları
        // kind 0: Don Cephesi · 1: Sert Kabuk · 2: Sis
        private static IEnumerator BossRule(CheckContext c, int kind)
        {
            Scenario s = Clean("boss kuralı", 3, 7); NoCrit(s);
            s.round = 26;
            s.boss = Scenario.NoBoss;
            if (kind == 2) { s.stats.RemoveAll(e => e.stat == StatType.AreaRadius); ScenarioPresets.Radius(s, 3f); }
            yield return c.Build(s);
            if (c.Aborted) yield break;
            var bosses = new List<SegmentEventSO>();
            LabCatalog.Bosses(TestSession.Profile, bosses);
            SegmentEventSO data = bosses.Find(b => kind == 0 ? b is FrostFrontSO : kind == 1 ? b is HardShellSO : b is FogSO);
            if (data == null) { c.Skip("boss verisi", "bu boss profilin havuzunda yok"); yield break; }

            // Önce: bütün yuvaların gerçek üretim aralığı, bütün bitkilerin azami canı, gerçek saldırı yarıçapı.
            var intervals = new Dictionary<PlantSpawner, float>();
            var health = new Dictionary<PlantHealth, int>();
            foreach (PlanterBrain brain in TestFieldPlanters())
                foreach (PlantSpawner spawner in brain.EditorSpawners)
                {
                    if (spawner == null) continue;
                    intervals[spawner] = spawner.EditorInterval;
                    if (spawner.SpawnedPlant != null && spawner.SpawnedPlant.TryGetComponent(out PlantHealth plant)) health[plant] = plant.MaxHealth;
                }
            double radiusBefore = PlayerStat(StatType.AreaRadius);

            Result started = TestEvents.StartBoss(data, 26, true);
            SegmentEventRuntime active = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            if (!started.Ok || active == null || active.Data != data) { c.Skip("boss", started.Message); yield break; }
            yield return LabRoutine.Frames(2);

            if (kind == 0)
            {
                var frost = (FrostFrontSO)data;
                int inBand = 0, outBand = 0, wrongIn = 0, wrongOut = 0;
                foreach (KeyValuePair<PlantSpawner, float> entry in intervals)
                {
                    bool covered = active.Covers(entry.Key.EditorPlanter.EditorOrigin);
                    float now = entry.Key.EditorInterval;
                    if (covered) { inBand++; if (Mathf.Abs(now - entry.Value * frost.spawnIntervalMultiplier) > 1e-3f) wrongIn++; }
                    else { outBand++; if (Mathf.Abs(now - entry.Value) > 1e-3f) wrongOut++; }
                }
                c.True("bantta ve bant dışında üretim noktası var", "ikisi de > 0", $"bant {inBand}, dış {outBand}", inBand > 0 && outBand > 0);
                c.Exact($"banttaki yuvalar: gerçek aralık = önceki × {frost.spawnIntervalMultiplier:0.###} (uymayan sayısı)", 0, wrongIn);
                c.Exact("bant dışındaki yuvalar: aralık değişmedi (uymayan sayısı)", 0, wrongOut);
            }
            else if (kind == 1)
            {
                var shell = (HardShellSO)data;
                int inBand = 0, outBand = 0, wrongIn = 0, wrongOut = 0;
                foreach (KeyValuePair<PlantHealth, int> entry in health)
                {
                    bool covered = active.Covers(FieldCells.Grid.GetGridPosition(entry.Key.transform.position));
                    int now = entry.Key.MaxHealth;
                    if (covered) { inBand++; if (Math.Abs(now - Arith.Round(entry.Value * (double)shell.healthMultiplier, 1)) > 1) wrongIn++; }
                    else { outBand++; if (now != entry.Value) wrongOut++; }
                }
                c.True("bantta ve bant dışında bitki var", "ikisi de > 0", $"bant {inBand}, dış {outBand}", inBand > 0 && outBand > 0);
                c.Exact($"banttaki bitkiler: azami can = yuvarla(önceki × {shell.healthMultiplier:0.###}) (uymayan sayısı)", 0, wrongIn);
                c.Exact("bant dışındaki bitkiler: can değişmedi (uymayan sayısı)", 0, wrongOut);
            }
            else
            {
                var fog = (FogSO)data;
                if (fog.stepDown) c.Manual("Sis kademeli modda", "bu profilde Sis kademeli (stepDown) çalışıyor; tek çarpanlı beklenti geçerli değil");
                else
                {
                    c.Near($"saldırı yarıçapı stat'ı = önceki {radiusBefore:0.###} × {fog.radiusMultiplier:0.###}", radiusBefore * fog.radiusMultiplier, PlayerStat(StatType.AreaRadius), 1e-4, "float");
                    yield return c.Attack(C, C, 121);
                    if (c.Aborted) yield break;
                    c.Near("gerçek saldırının temas yarıçapı da aynı", radiusBefore * fog.radiusMultiplier, c.Trace.Radius, 1e-4, "float");
                }
            }

            Result cleared = TestEvents.ClearBoss();
            yield return LabRoutine.Frames(2);
            c.True("boss etkisi oyunun temizlik yoluyla kalktı", "aktif boss yok", SegmentEventDirector.Instance.Active == null ? "aktif boss yok" : "hâlâ aktif", cleared.Ok && SegmentEventDirector.Instance.Active == null);
            if (kind == 0)
            {
                int wrong = 0;
                foreach (KeyValuePair<PlantSpawner, float> entry in intervals) if (Mathf.Abs(entry.Key.EditorInterval - entry.Value) > 1e-3f) wrong++;
                c.Exact("kalkınca bütün yuvalar eski aralığına döndü (uymayan sayısı)", 0, wrong);
            }
            else if (kind == 1)
            {
                int wrong = 0;
                foreach (KeyValuePair<PlantHealth, int> entry in health) if (entry.Key.MaxHealth != entry.Value) wrong++;
                c.Exact("kalkınca bütün bitkiler eski azami canına döndü (uymayan sayısı)", 0, wrong);
            }
            else c.Near("kalkınca saldırı yarıçapı eski değerine döndü", radiusBefore, PlayerStat(StatType.AreaRadius), 1e-4, "float");
        }

        // ================================================================ XP / kaynak / skor
        private static IEnumerator SingleHarvest(CheckContext c)
        {
            Scenario s = Clean("tek hasat", 0); BigDamage(s); NoCrit(s);
            yield return c.Build(s);
            PlantHealth plant = CheckContext.Plant(C, C);
            PlanterBrain brain = CheckContext.Planter(C, C);
            if (plant == null || plant.Data == null || brain == null) { c.Skip("ön koşul", "merkezde bitki yok"); yield break; }
            PlantSO data = plant.Data;
            Economy before = Economy.Read();
            yield return c.Attack(C, C, 131);
            if (c.Aborted) yield break;
            Economy after = Economy.Read();
            c.Exact("hasat sayısı", 1, c.Trace.Harvests.Count);
            if (c.Trace.Harvests.Count != 1) yield break;
            TraceHarvest harvest = c.Trace.Harvests[0];
            c.True("bitki türü ve nadirliği", $"{data.name} [{data.rarity}]", $"{harvest.Plant} [{harvest.Rarity}]", harvest.Plant == data.name && harvest.Rarity == data.rarity);
            c.Exact("gerçek taban ödül = bitki verisi", data.rewardAmount, harvest.BaseReward);
            c.Near("yuvarlanan kaynak = yuvarla(taban ödül × saksı kaynak çarpanı)", Arith.Round(data.rewardAmount * (double)harvest.ResourceMultiplier), harvest.RewardRounded, 1d, Rounding);
            c.Near("yuvarlanan XP = yuvarla(taban XP × saksı XP çarpanı × nadirlik çarpanı)", Math.Round(data.xpAmount * (double)harvest.XpMultiplier * harvest.RarityXpMultiplier, MidpointRounding.ToEven), harvest.XpRounded, 1d, Rounding);
            c.Exact($"{harvest.Resource} bakiyesinin gerçek artışı = kayıttaki eklenen kaynak", harvest.RewardFinal, after.Resource(harvest.Resource) - before.Resource(harvest.Resource));
            foreach (ResourceType other in new[] { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone })
                if (other != harvest.Resource) c.Exact($"{other} bakiyesi değişmedi", 0, after.Resource(other) - before.Resource(other));
            c.Near("kazanılan toplam XP'nin gerçek artışı = kayıttaki eklenen XP", harvest.XpFinal, after.XpEarned - before.XpEarned, 0.01, "double");
            c.True("Harvest Score kaydı var", "var", harvest.HasScore ? "var" : "yok", harvest.HasScore);
            c.Exact("Harvest Score'un gerçek artışı = kayıttaki eklenen skor", harvest.ScoreFinal, after.Score - before.Score);
            c.True("karışma uyarısı yok (otomatik saldırı kapalı, üretim duraklatılmış)", "uyarı yok", c.Trace.Interference.Count == 0 ? "uyarı yok" : string.Join(" | ", c.Trace.Interference), c.Trace.Interference.Count == 0);
        }

        private static IEnumerator LevelGain(CheckContext c)
        {
            yield return c.Build(Clean("level kazanımı", 0));
            if (c.Aborted) yield break;
            ProgressionManager progression = ProgressionManager.Instance;
            Economy before = Economy.Read();
            double cost = progression.XPToNextLevel, extra = Math.Floor(cost * 0.25d), give = cost - before.XpStored + extra;
            int choices = Rounds.ChoicesPerLevel;
            Result given = TestStats.GiveXP(give);
            if (!given.Ok) { c.Skip("XP", given.Message); yield break; }
            yield return LabRoutine.Frames(3);
            Economy after = Economy.Read();
            c.Near("kazanılan toplam XP verilen kadar arttı", give, after.XpEarned - before.XpEarned, 0.01, "double");
            c.Exact("level bir arttı", before.Level + 1, after.Level);
            c.Near("saklı (kalan) XP = level maliyetinden artan — kazanılan XP'den farklı", extra, after.XpStored, 0.01, "double");
            c.Exact("yeni kart seçim hakkı = level başına hak", choices, after.PendingChoices - before.PendingChoices);
            c.Exact("bu run'da verilen toplam seçim hakkı aynı kadar arttı", choices, after.GrantedChoices - before.GrantedChoices);
        }
    }
}
