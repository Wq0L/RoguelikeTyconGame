# Bölüm 3.7.6 — Dört davranışlı, sınırlandırılmış zincir prototipi

**Durum (2026-10-03):** P6a ve P6b uygulandı. Yeni aday profil `Run50_ZincirV1`: Kırılma Erişimi V1'in her şeyi, güçlü aşamada
**Zincir Hasat** ödülüyle. Patlama, elektrik, kasırga ve bumerangın dördü de zincire katılıyor. Otomatik testler ve ölçümler izole
kopyada çalıştı. İnsan Play testi yapılmadı; "kırılma hissi çözüldü" demiyorum. Commit atılmadı; seçili profile, oyuncu kaydına
ve açık editöre dokunulmadı. P7'ye geçilmedi.

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md) · Önceki paketler:
[Bolum3-7-5-KirilmaOdulleriErisim.md](Bolum3-7-5-KirilmaOdulleriErisim.md), [Bolum3-7-5-1-AlanGeriBildirimi.md](Bolum3-7-5-1-AlanGeriBildirimi.md)

## Kısa sonuç

- **Çalışıyor:** Zincir Hasat alınınca, davranışla ölen bitkinin kendi saksısı kendi davranışlarını en çok 2 ek nesil boyunca
  tetikleyebiliyor. Patlama, elektrik, kasırga ve bumerangın dördü birbirini tetikliyor (16 / 16 eşleşme). Kök, nesil, tekrar
  sınırı, bütçe, kuyruk, havuz beklemesi, ayrı zar ve temizlik tek sınıfta (`HarvestChain`); davranışlar yalnız mevcut hasar
  yollarını bağlamla çalıştırıyor.
- **Doğrulama:** yeni test 38 / 38; regresyon turunda 20 eski test PASS; eski teklif dağılımı ve bedelli ödül ölçümü bayt bayt
  aynı; ödülsüz oynanış 3.7.5.1 ile 24 / 24 round aynı.
- **Ölçülen etki küçük.** Sabit statlı laboratuvarda toplam hasat medyanı ×1,00–×1,06 (seed dağılımı medyandan büyük); güçlü
  davranış sinerjisinde ×1,06–×1,26. Birikimli run'da ödül sonrası hasat ×1,00 / ×1,00 / ×1,06 (patlama / elektrik /
  Davranış-rezonans); zincir hasadın %7–30'unu alıyor ama toplamı büyütmüyor ve hiçbir çiftin sonucunu değiştirmedi. Değerler
  görev gereği ayarlanmadı. Bu değerlerle zincir ölçümde bir "kırılma" ödülü gibi davranmıyor.
- **Performans:** kare başına +0,004–0,014 ms, GC toplaması yok. Zincirin ilk tetiğindeki tek kare sıçraması (68,9 ms), havuzun
  round başında kurulmasıyla giderildi.
- **Açık:** zincir görseli (kaynak saksıya kısa mor çizgi var ama zayıf; yayılım çizilmiyor), insan Play testi, değer ayarı.
- **Zincirden bağımsız bulgu:** Davranış-rezonans seed 101'de, iki kolda da, geç run'da temel güç kartları yüzünden seviye ve hasar
  kontrolden çıkıyor (hasar `int` taşması). Ölçüm bu yüzden bir kez durdu; P7'ye ait, düzeltilmedi (6.3).

## 1. Uygulanan kurallar ve veri ayarları

**Zincir Hasat** (`ZincirHasat_Z1`, yalnız `Run50_ZincirV1` havuzunda): güçlü aşama (ilk teklif R23 boss'u), ağırlık 1, en çok 1
kez, yerleşmiş en az bir saksıda davranış şansı varsa sunulur. Ek hasar, saldırı hızı ya da bedel yok. Aşamalı teklif kurallarına
uyar; garanti edilmez.

| Kural | Uygulama |
|---|---|
| Kök | Oyuncunun her doğrudan saldırısı tek kök kimliği alır (`PlayerController.AttackInRadius` → `HarvestChain.BeginRoot`). Aynı saldırının bütün vuruşları ve onların doğurduğu davranışlar bu kimliği taşır. Sonraki saldırı yeni köktür |
| Nesil 0 | Doğrudan hasadın normal davranış tetiği: eski şans, eski hasar, eski zar sırası (`UnityEngine.Random`). Zincir ödülü bu tetiklerin sayısını azaltmaz |
| Nesil 1, 2 | Nesil g davranışının **öldürdüğü** bitkinin **kendi saksısı**, nesil g+1 olarak denenir. Nesil 2'nin öldürmeleri yeni davranış başlatmaz (nesil sınırı) |
| Şans | Saksının gerçek davranış şansı × 0,75 (nesil 1), × 0,50 (nesil 2) |
| Hasar | Tetiklenen saksının **normal** davranış hasarı × 0,75 / × 0,50. Önceki neslin hasarı aktarılmaz; çarpanlar birikmez (nesil 2 = normal × 0,50, 0,75 × 0,50 değil). Rezonans, uzmanlaşma ve boss katsayıları normal hesapta bir kez; doğrudan vuruş cezası (Bereketli Öğrenim) davranışa sızmaz |
| Davranışsız saksı | Yeni davranış verilmez: şansı 0 olan davranış denenmez |
| Tekrar sınırı | Aynı kökte aynı saksı + davranış çifti en çok bir kez denenir. Başarısız deneme de hakkı tüketir. İşaret, zar atılmadan ve iş kuyruğa girmeden önce konur. Aynı saksının başka davranışı ve başka saksının aynı davranışı ayrı denenir. Çok hücreli saksı tek saksıdır |
| Normal tetikle çalışmış çiftler | Kökte nesil 0'da **gerçekten çalışan** saksı + davranış çiftleri ziyaret edilmiş sayılır: zincir dönüp aynı kaynağı yeniden çalıştırmaz. Bu kayıt normal doğrudan tetikleri engellemez |
| Artçı ve ikinci dalga | Artçı Patlama'nın ve Çifte Akım'ın ikinci dalgasının öldürdüğü bitki zincir başlatmaz. Zincirden doğan patlama artçı, zincirden doğan elektrik ikinci dalga üretmez. Normal tetikli davranışların artçı / ikinci dalgası aynen çalışır |
| Bütçe | Kök başına en çok 32 başarılı ek tetik (normal tetikler sayılmaz). Bütçe dolunca zar atılmaz, ret sayılır |
| Kuyruk | Başarılı tetik kuyruğa girer; doğduğu kareden sonraki karelerde sırayla işlenir. Kare başına en çok 8 iş; kalan iş silinmez |
| Havuz | Kasırga / bumerang havuzu doluysa iş sırasını kaybetmeden bekler; zar yeniden atılmaz, bütçe yeniden tüketilmez; arkasındaki uygun işler beklemez. Bekleme ve gecikme sayılır. Havuz büyütülmedi |
| Zar | Zincirin şans zarı ayrı ve tekrar üretilebilir bir akıştır (run seed + kökün run içindeki sırası); zincirden doğan kasırganın yolu ve bumerangın yönü de bu akıştan. Görsel zarlarına ve `UnityEngine.Random`'a bağlı değildir |

Veri: bütün sayılar ödül asset'indedir (`chainGenerations`, `chainChance`, `chainDamage`, `chainRootBudget`, `chainJobsPerFrame`)
ve tek kaynak `Tools/Balance/ZincirV1/zincir_v1_params.py`'dir. Tutarsız veride (dizi uzunluğu, aralık) ödül uygulanmaz; değer
kırpılmaz. Değerler görevin ilk prototip sınırıdır; ölçüm sonucuna göre değiştirilmedi.

## 2. Kök, nesil ve kuyruğun yaşam döngüsü

1. **Kök açılır.** Doğrudan saldırının başında, ödül varsa ve round sürüyorsa. Kök bağlamı havuzdan gelir; kimlik artan bir
   sayıdır, yeniden kullanılmaz. Saldırının kendisi kökü tutar (1).
2. **Vuruşlar bağlamı taşır.** Doğrudan vuruş `Direct(kök)`, normal tetikli davranış `Behavior(kök, 0)`, zincir davranışı
   `Behavior(kök, g)`. Artçı ve ikinci dalga `Aftershock` (kök yok). Bitki ölünce öldüren vuruşun bağlamı saklanır.
3. **Ölüm olayı.** Doğrudan hasat → normal tetik (eski yol); çalışan davranışlar kökte işaretlenir. Davranış hasadı →
   `HarvestChain.OnBehaviorHarvest`: artçı mı, kök hâlâ açık mı, nesil sınırı, kaynak geçerli mi; sonra saksının her davranışı için
   sırayla tekrar → bütçe → işaret → zar. Başarılı tetik kuyruğa girer ve kökü tutar. Ölüm olayının içinde davranış çalışmaz.
4. **Kuyruk.** Her karede, doğduğu kareden sonraki işler sırayla: round / run hâlâ aynı mı, saksı yerinde ve hücresinin sahibi mi;
   davranış kendi yoluyla, bu neslin bağlamı ve hasar çarpanıyla çalışır; saksının ayak izine kısa mor çizgi. Kasırga / bumerang
   havuzu doluysa iş bekler (sayılır), sıradaki uygun işler çalışır. Kare başına en çok 8 iş.
5. **Kök serbest kalır.** Saldırı bitince saldırının tutumu, iş bitince işin tutumu bırakılır; kasırga ve bumerang kökü havuza
   dönerken bırakır. Tutan kalmayınca bağlam havuza döner (ziyaret kümesi boşaltılır).
6. **Temizlik.** Round sonu: bütün işler düşer (ret: round sonu); kasırga ve bumerangları yöneticileri temizler, kökleri bırakılır.
   Run başı, menü / run sonu, devre dışı kalma: işler düşer, kökler zorla bırakılır, sayaçlar run başında sıfırlanır. Sahne
   değişince bileşen sahneyle birlikte gider.

## 3. Dört davranışın kapsamı

| Davranış | Bağlamı nasıl taşır | Zincirde |
|---|---|---|
| Patlama | `PlanterBrain.Explode` (normal tetik ve zincir aynı yol): her vuruş bağlamı taşır | Hemen vurur. Artçı üretmez |
| Elektrik | `HarvestBehaviorManager.TryElectric` → `ElectricBurst.Strike`: her vuruş bağlamı taşır. Hasar önce normal hesapla bulunur, çarpan bir kez uygulanır | Hemen vurur. İkinci dalga üretmez. Görsel havuzu doluysa yalnız çizim atlanır |
| Kasırga | `TornadoManager.TrySpawn` → `Tornado`: bağlamı kendisi taşır, kökü yaşadığı sürece tutar, havuza dönerken bırakır | Sonraki karelerde vurmaya devam eder; yolu zincir zarından. Havuz doluysa iş bekler |
| Bumerang | `HarvestBehaviorManager.TryBoomerang` → `BoomerangScythe`: aynı | Gidiş-dönüş vuruşları bağlamı taşır; yönü ve menzili zincir zarından. Havuz doluysa iş bekler |

Zincir tetiği, öldürülen bitkinin **kendi saksısından** doğar. Canlı bitkiye hasar vermek zincir başlatmaz; yalnız ölüm. Bitkinin
ölümünden kaynak, XP, skor ve görev bir kez çıkar (ölüm tek yerden geçer: `PlantHealth.Die`). Havuzdan dönen bitki önceki
yaşamının bağlamını taşımaz (`Initialize` sıfırlar). Kuyruktaki iş bitki nesnesi değil hücre ve saksı tutar.

## 4. Yeni sınıflar ve sorumluluklar

| Sınıf / dosya | Sorumluluk | Neden gerekli |
|---|---|---|
| `HarvestLink` (`Assets/Scripts/Behaviors/HarvestLink.cs`) | Bir vuruşun kökü, nesli, artçı mı olduğu (değer tipi) | Bağlam vuruşla taşınmalı: gecikmeli kasırga / bumerang için genel bir "şu an zincirdeyiz" durumu doğru olmaz |
| `HarvestChain` (`Assets/Scripts/Managers/HarvestChain.cs`) | Bütün zincir kuralları tek yerde: kök bağlamları (referans sayımlı, havuzlu), ziyaret kümesi, bütçe, zincir zarı, kuyruk, kare bütçesi, havuz beklemesi, ret sayaçları, temizlik | Kuralların davranışların içine kopyalanmaması; RoundManager'a kural girmemesi |
| `HarvestChainConfig` (aynı dosya) | Ödül verisinden doğrulanıp bir kez kopyalanan ayar | Tutarsız veride ödülün hiç uygulanmaması (kırpma yok) |
| `ChainRandom` (aynı dosya) | Durumu tek sayı olan tekrar üretilebilir zar (xorshift32) | Zincirin `UnityEngine.Random` akışına dokunmaması; efekte ayrı akış verilebilmesi (allocation yok) |
| `CellOutlineMesh` (3.7.5.1) / `AftershockAreaFeedback.PlayChainSource` | Zincirle tetiklenen saksının ayak izine kısa mor çizgi; havuz round başında kurulur | Kaynak vurgusu için ikinci bir görsel sistem kurulmadı; artçı çizgisinin havuzu ve çizimi kullanıldı |
| `ChainDebugMenu` (`Assets/Editor/`) | Yalnız Editor Play modunda o anki run'a Zincir Hasat verir | Karşılaştırma yolu (11. bölüm). Oyun koduna debug yolu eklenmedi |
| `ZincirV1Verification`, ölçüm kipleri | İşlev testi, laboratuvar, tam run seti | Testler ve ölçümler |

Değişen mevcut dosyalar: `PlantHealth` (bağlam saklama), `PlayerController` (kök açma / kapama), `PlanterBrain` (patlamanın tek yola
ayrılması, normal tetiğin bağlamı ve ziyaret işareti, `IsHarvestSource` / `IsPlaced`), `TornadoManager` ve `Tornado`,
`HarvestBehaviorManager`, `BoomerangScythe`, `ElectricBurst`, `BehaviorEchoes` (artçı vuruşları zincir dışı, havuzu önceden kurma),
`BossRewardSO` / `BossRewardManager` (ödül verisi ve ayarı), `BossRewardText` (kart metni), `RoundManager` (yalnız bileşeni
ekleyen bir satır), `AftershockAreaFeedback` (zincir kaynağı çizgisi, önceden kurma), `RunProfileMenu`.

**Bilinen teknik borç:**

- `AftershockAreaFeedback` artık zincir kaynağının çizgisini de çiziyor; adı Artçı'dan kaldı. Aynı havuzu (8 dalga) paylaşıyorlar:
  çok yoğun anda biri diğerinin çizgisini atlatabilir (yalnız görsel, sayılır).
- `ChainDebugMenu`, test yardımcısı `RewardOfferLab.Grant` ile aynı yansıma yolunu kopyalar (test yardımcıları kullanıcı
  projesinde derlenmez). Ödül yöneticisinin iç alanlarının adı değişirse menü çalışmaz.
- Normal (nesil 0) kasırga / bumerang tetiği havuz doluysa eskisi gibi başlatılamaz ve kaybolur (`HarvestBehaviorStats.Skipped`
  sayar). Bekleme yalnız zincir işleri için yapıldı; normal tetiğin eski davranışı değiştirilmedi.
- Bumerangın vuruş taraması her adımda bütün tarlayı geziyor (eski kod; bu pakette değiştirilmedi). Zincir bumerang sayısını
  artırabildiği için yoğun tarlada maliyeti büyüyebilir.
- Elektriğin eski "yük" yolu (`ElectricChargeManager`, profilde kapalı) bağlam taşımaz; bu yolla ölen bitki zincir başlatmaz.

## 5. Testler

Bütün testler izole kopyada (`Library/VerificationProject`), düşük öncelikle, gerçek oyun döngüsünde çalıştı. Asıl projeye, seçili
profile ve oyuncu kaydına dokunmadılar. Zincir şansları testte 0 ya da 1 verilerek belirlenimci yapıldı; gerçek veri ayrıca
denetlendi.

**Yeni test: `ZincirV1Verification`** (`Tools/Verification/Editor/`). Sonuç ve çıktılar: [Bolum3-7-6/testler](Bolum3-7-6/testler).
Son kodla: **PASS 38 / 38** (aşama 1'de 24, aşama 2 ile 38 kontrol). Geliştirme sırasındaki çalıştırmalar:
[Bolum3-7-6/gelistirme](Bolum3-7-6/gelistirme).

Not: test `UnityEngine.Random`'u sabitlemez. Bitki üretimi, normal tetikli kasırganın yolu ve bumerangın yönü ondan geldiği için
kasırga / bumerang kaynaklı eşleşmelerde hasat ve vuruş sayısı çalıştırmadan çalıştırmaya bir iki değişiyor (toplam 213 → 210 hasat).
Kontroller sayıya değil, nesil 1 tetiğinin aynı kökle çalışmasına ve "bir ölüm, bir XP" eşitliğine bakar; iki çalıştırmada da geçti.

| Konu | Doğrulanan |
|---|---|
| Veri ve profil | Ödül verisi (2 ek nesil, ×0,75 / ×0,50, bütçe 32, kare başına 8; güçlü aşama, ağırlık 1, en çok 1, davranışlı saksı şartı, bedelsiz, hasar / hız artışı yok). Havuz = Kırılma Erişimi V1 havuzu + Zincir Hasat; diğer bütün ödüller aynı asset. Profil havuz dışında Kırılma Erişimi V1 ile aynı. Eski 6 havuzda zincir ödülü yok |
| Ödül yokken | Eski kural: A'nın doğrudan hasadı A'nın patlamasını çalıştırır, B ölür, başka bir şey olmaz (kök açılmaz, zar yok, kuyruk yok) |
| Nesiller | A → B (nesil 1, hasar 75) → C (nesil 2, hasar 50 = 100 × 0,50; 0,75 × 0,50 değil) → D hiçbir şey başlatmaz. Patlama → elektrik → patlama karışık yol |
| Tekrar kuralı | A → B → A: A'nın aynı patlaması bu kökte reddedilir. Başka saksının aynı davranışı ve aynı saksının başka davranışı ayrı çalışır. Başarısız zar hakkı tüketir (ikinci ölümde zar yok). Çok hücreli saksı tek kimlik. İki saldırı iki kök (8, 9): ziyaret sızmaz, ikisi de serbest kalır |
| Şans ve hasar | Gerçek veriyle nesil 1 şansı 0,8 × 0,75 = 0,6, nesil 2 0,8 × 0,50 = 0,4. Davranışa Adanış ×1,50 ve Davranış Ustası ×1,25 ile normal patlama 188; nesil 1 = 141 (katsayılar ikinci kez uygulanmaz, doğrudan vuruş cezası sızmaz) |
| Artçı / ikinci dalga | Artçının ve Çifte Akım ikinci dalgasının öldürdüğü bitki zincir başlatmaz; zincirden doğan patlama artçı, zincirden doğan elektrik ikinci dalga planlamaz |
| Kuyruk ve bütçe | Aynı karede doğan 10 tetik sonraki karelerde 8 + 2 çalışır, hiçbiri kaybolmaz, ölüm olayının içinde çalışmaz. Kök bütçesi (test değeri 3): 3 çalışır, 7 bütçe reddi, zar atılmaz. İşi beklerken satılan saksı: geçersiz kaynak, kök serbest |
| Dört davranış | 16 / 16 kaynak → hedef eşleşmesi (patlama, elektrik, kasırga, bumerang): kaynağın normal davranışının öldürdüğü bitkinin saksısı nesil 1 olarak aynı kökle çalışır; kaynak çizgisi çizilir |
| Havuz beklemesi | Kasırga havuzu (10) ve bumerang havuzu (12) doluyken 9'ar zincir işi yerinde bekler; zar yeniden atılmaz, bütçe ikinci kez düşmez; arkalarındaki patlama işi ilk karede çalışır. Havuz boşalınca 9 / 9 çalışır; en uzun gecikme 3,6 sn, sayıldı. Son gecikmeli vuruş bitince kök serbest |
| Temizlik | Menü / run sonu: bekleyen işler ve açık kökler düşer. Round 10 iş beklerken biterse hepsi "round sonu" reddi. Round, kökü tutan 10 zincir kasırgası canlıyken biterse hepsi temizlenir, kök serbest. Yeni sahne / yeni run: boş kuyruk, açık kök yok, sayaçlar sıfır, ödül yok |
| Kimlik ve ödül | Havuzdan dönen bitki önceki yaşamının kök / neslini taşımaz. 210 hasat = 210 XP verişi (önceki çalıştırmada 213 = 213): zincirle ölen bitki iki kez ödemez |
| Hasat Ritmi | Gerçek round geçişinde (EndRound → StartNextRound, R4 → R5) hazır hak korunur (mevcut davranış, değişmedi); sonraki savuruş güçlü, HUD "hazır" |
| Kart | Zincir Hasat kartı ekrana sığar; zincirin ne yaptığını oyuncu diliyle söyler (aşağıda görüntü) |
| Hata | Test boyunca hata ya da istisna yok |

**Kapı (aşama 1'den sonra, aşama 2'ye geçmeden):** ödülsüz yoğun tarla ölçümü (24 round, 3.7.5.1 ile aynı), eski profillerin ödül
teklif dağılımı ve yedi eski test. Sonuçlar: [Bolum3-7-6/asama1-kontrol](Bolum3-7-6/asama1-kontrol).

| Test / ölçüm | Sonuç |
|---|---|
| Ödülsüz yoğun tarla (`KirilmaErisimMeasurement.RunPerformance`) | 24 / 24 round oynanış sütunları 3.7.5.1 ile aynı |
| Ödül teklif dağılımı (`RewardOfferMeasurement`) | bayt bayt aynı (md5 `222d6374…`) |
| KirilmaErisimiV1 / AlanGeriBildirimi / KirilmaV1 / HarvestBehavior / ElectricRevert / TakvimV1 / BossReward | PASS 35 / 23 / 189 / 114 / 40 / 162 / 127 |

**Son kodla regresyon turu:** [Bolum3-7-6/testler/TopluSonuc.txt](Bolum3-7-6/testler/TopluSonuc.txt).

| Test / ölçüm | Sonuç |
|---|---|
| ZincirV1Verification (yeni) | PASS 38 |
| AlanGeriBildirimi / KirilmaErisimiV1 / BedelliOdullerV1 / OdulAsamalariV1 / BossReward | PASS 23 / 35 / 93 / 71 / 127 |
| KirilmaV1 / TakvimV1 / DengeV1 | PASS 189 / 162 / 86 |
| HarvestBehavior / ElectricRevert / Specialization / StartLoadout / Mechanics | PASS 114 / 40 / 100 / 88 / 88 |
| SimpleResonance / ContactRefactor / RunPrototype / BossWeather / RoundPreview / Run50Reference / OptionsMenu | PASS 293 / 175 / 69 / 22 / 47 / 70 / 22 satır |
| Ödül teklif dağılımı (`RewardOfferMeasurement`) | bayt bayt aynı (md5 `222d6374…`) |
| Bedelli ödül ölçümü (`BedelliOdulMeasurement`) | bayt bayt aynı (md5 `6d628d3f…`) |
| Ödülsüz yoğun tarla (`KirilmaErisimMeasurement.RunPerformance`) | 24 / 24 round, 46 oynanış sütunu 3.7.5.1 ile aynı; zincir sayaçları 0 |

Toplam 21 test PASS, hata yok. Not: tur arka planda çalışırken sarmalayıcı betik eski sonuç dosyalarını silemedi (erişim reddi);
bu yüzden her sonuç dosyasının yazılma zamanı ayrıca denetlendi: hepsi kendi işinin süresi içinde yeniden yazılmış.

**Bilerek değiştirilen beklentiler:**

- `KirilmaV1Verification` ve `TakvimV1Verification`: profil menüsü / profil listesi beklentisine `Run50_ZincirV1` eklendi. Başka
  beklenti değişmedi.
- `KirilmaErisimMeasurement` ve `BalanceRunMeasurement`: yalnız yeni ölçüm kipleri ve sona eklenen sütunlar (eski kiplerin çıktısı
  aynı). `BalanceRunMeasurement.Add` isteğe bağlı ilk seed aldı (varsayılan 100; eski setler aynı) ve durmuş setin kalanı için
  `k376zincirdevam` eklendi. `KirilmaErisimMeasurement`'in zincir izleyicisi kök başına küme açmayacak biçimde tek kümeye
  çevrildi (sayılar aynı; 7. bölüm). `analyze_chain.py runs` birden çok CSV okuyor ve oyuncu seviyesini yazıyor.

**Geliştirme sırasında çıkan hatalar (giderildi):**

- Ölçüm satırına eklenen bir sütun adı mevcut alanla çakıştı (CS0102); aşama 2'nin ilk denemesi derlenmedi, sonuç dosyası
  oluşmadı. Sütunlar `ChainAttempts` / `ChainTriggers` / `ChainFired` olarak adlandırıldı; sonraki iki deneme PASS.
- Görüntü durumu ilk denemede patlama → patlama idi; mor çizgi patlama parçacıklarının altında seçilmiyordu. Görüntü durumu
  patlama → elektrik yapıldı (kural testleri değişmedi).
- Asset üreticisinde `%YAML` başlığı biçimlendirme hatası, `re.sub` kaçış hatası ve yarım kalan çıktılar üretim korumasına takıldı;
  yarım çıktılar silinip baştan üretildi. Yedi üreticinin hepsi `--check` ile geçiyor.
- Karma düzende R40 zincir kolunda tek karelik sıçramalar (68,9 ms) görüldü; sebep zincirin ilk kullanımda havuz / malzeme
  kurması. Alan çizgisi havuzu zincir açıkken round başında kuruluyor (7. bölüm).
- Birikimli run setinin 52. run'ı durdu (6. bölüm): oyun hatası değil, zincirden bağımsız bir seviye patlaması.

## 6. Ölçüm

Laboratuvar ve birikimli run ayrı tutuldu; sonuçlar birbirine eklenmedi. Kota, boss hedefi ve zincir değerleri ölçüme göre
değiştirilmedi. Bütün tablolar ve ham CSV'ler: [Bolum3-7-6/olcum](Bolum3-7-6/olcum).

### 6.1 Laboratuvar (sabit stat, aynı tarla, aynı seed; zincir kapalı / açık)

Tek round; bot oyunun gerçek saldırı yoluyla vurur. Beş düzen (patlama, elektrik, kasırga / bumerang, karma, davranışsız kontrol)
× R23 / R30 / R40 × 10 seed × 2 kol = 300 round. Zincir kolu ödül asset'inin gerçek değerlerini kullanır. Tam tablo:
[Laboratuvar.md](Bolum3-7-6/olcum/Laboratuvar.md).

| Düzen | R23 hasat | R30 hasat | R40 hasat | Zincir payı (R23 / R30 / R40) |
|---|---|---|---|---|
| Patlama ağırlıklı | ×1,05 (0,88–1,24) · 7/10 | ×1,01 (0,88–1,06) · 5/10 | ×1,02 (0,88–1,11) · 6/10 | %4,7 / %2,9 / %3,6 |
| Elektrik ağırlıklı | ×1,00 (0,98–1,03) · 2/10 | ×1,00 (0,79–1,09) · 2/10 | ×1,03 (0,85–1,07) · 6/10 | %1,5 / %0,6 / %1,8 |
| Kasırga / bumerang ağırlıklı | ×1,00 (0,95–1,14) · 2/10 | ×1,00 (1,00–1,05) · 2/10 | ×1,00 (0,89–1,08) · 2/10 | %3,0 / %0,0 / %0,8 |
| Karma davranışlı | ×1,00 (0,96–1,19) · 4/10 | ×1,00 (0,88–1,07) · 1/10 | ×1,06 (0,99–1,08) · 8/10 | %1,7 / %0,8 / %1,7 |
| Davranışsız (kontrol) | ×1,00 (1,00–1,00) · 0/10 | ×1,00 (1,00–1,00) · 0/10 | ×1,00 (1,00–1,00) · 0/10 | %0 |

Medyan (en düşük – en yüksek) · zincir kolunun daha çok hasat ettiği seed sayısı. Zincir payı: nesil 1 + 2 hasadının toplam hasada oranı.

- **Etki küçük.** Toplam hasat medyanı ×1,00–×1,06; seed'ler arası dağılım (×0,79–×1,24) medyan farkından büyük. Skor oranı da
  ×0,97–×1,07.
- **Davranışsız kontrol birebir aynı** (30 / 30 çift, ×1,00): ödül alınsa bile davranışsız saksıya yetenek verilmiyor.
- **Nesiller:** zincir hasadının neredeyse tamamı nesil 1'den (nesil 2 round başına 0–0,1 hasat). Ör. patlama R40: round başına
  nesil 1'de 20,8 deneme, 7,9 tetik; nesil 2'de 2,5 deneme, 0,5 tetik.
- **Yeni hedef:** zincir vuruşlarının %70–88'i o kökte doğrudan saldırının dokunmadığı hücrelere gidiyor (ör. patlama R40: 28,2 vuruş,
  20,1'i yeni hücre).
- **Retler:** en sık ret "tekrar" — çoğu, normal tetikle zaten çalışmış çiftler. Kök bütçesi (32), geçersiz kaynak ve round sonu
  retleri laboratuvarda hiç oluşmadı; nesil sınırı round başına 0–0,1.
- **Kuyruk:** her iş doğduğu kareden bir sonraki karede çalıştı (gecikme 1 kare); en çok bekleyen iş 3; kasırga / bumerang havuzu
  beklemesi yok; kaynak çizgisi atlaması yok.

**Güçlü sinerji (ek bilgi, P5 ile aynı kurulum):** davranış şansı ve davranışlı saksı yoğun olduğunda zincir belirginleşiyor —
patlama ×1,26 (R30; 9/10 artış) / ×1,20 (R40; 10/10), elektrik ×1,06 / ×1,06. Zincir payı patlamada %17–19, elektrikte %9–11;
nesil 2 burada görünür (patlama R40: round başına 6,3 hasat). Bütçe reddi yine yok, en çok bekleyen iş 7, gecikme 1 kare.
Tablo: [Sinerji.md](Bolum3-7-6/olcum/Sinerji.md). Bu kurulum ana karşılaştırma değildir; sonuç ödülün değerine dair karar değildir.

### 6.2 Birikimli tam run (XP, kartlar, ağaç ve ödüller ilerler)

`Run50_ZincirV1`, 3 politika (Patlama yolu, Elektrik yolu, Davranış-rezonans) × 10 seed × 2 kol. **Aday** Zincir Hasat teklif
edilince alır; **almayan eş** aynı seed'de yalnız onu almaz (o boss'ta başka bir ödül seçer). Ödülün alındığı round'a kadar iki
kol bayt bayt aynı (bütün çiftlerde "öncesi aynı: evet"). Karşılaştırma ödül alındıktan sonraki round'ların toplamıdır; run
toplamı değil. Tam tablo: [BirikimliRun.md](Bolum3-7-6/olcum/BirikimliRun.md).

| Politika | Ödülü alan seed | Alınış | Ödülden sonra hasat (aday ÷ eş) | Skor | Zincir hasadının payı (aday) |
|---|---|---|---|---|---|
| Patlama yolu | 9 / 10 | R23–R43 | ×1,00 (0,84–1,21; 4 / 9 artış) | ×0,95 | %6,9–27,1 |
| Elektrik yolu | 9 / 10 | R23–R43 | ×1,00 (0,87–1,31; 5 / 9 artış) | ×1,01 | %7,9–22,1 |
| Davranış-rezonans | 9 / 10 (seed 101 yerine 110) | R23–R43 | ×1,06 (0,93–1,22; 6 / 9 artış) | ×1,11 | %8,0–29,6 |

- Zincir run'da görünür bir hasat payı alıyor (%7–30; laboratuvardan çok daha yüksek, çünkü geç run'da davranış şansları ve
  davranışlı saksı sayısı büyük), ama ödül sonrası **toplam** hasatı tutarlı biçimde büyütmüyor. Bunun nedeni bu pakette
  ayrıştırılmadı (tarla geç run'da neredeyse hiç boş kalmıyor: boş tarla payı %0–5,5; yani "bitki tükendi" açıklaması
  desteklenmiyor).
- Bütün sayılan çiftler R50'yi kazandı; ödül hiçbir çiftte sonucu değiştirmedi. Seed 105'te ödül hiç sunulmadı (üç politikada da).
- Davranış-rezonans 10 sayılan çifti tamamlamak için seed 110 eklendi (seed 101 aşağıdaki nedenle özete girmedi).

### 6.3 Zincirden bağımsız bulgu: geç run seviye patlaması (seed 101, Davranış-rezonans)

Birikimli setin 52. run'ı (Davranış-rezonans, seed 101, **almayan eş — zincir yok**) R50'nin kart ekranında durdu. Neden oyunun
hata vermesi değil: oyuncu seviyesi R49 → R50 arasında 417'den **30.779**'a çıktı ve bot 30 bin kart seçimini ölçüm aracının
takılma sınırında (240 sn) bitiremedi. Aynı seed'in zincirli kolu (run 42) da aynı yola girdi: R48'de seviye 931; R49'da hasar
2,1 × 10¹¹'e çıktı. `int` sınırını aşan hasar doğrudan vuruşta 1'e düşüyor (koddan: `PlayerController`,
`Mathf.Max(1, Mathf.RoundToInt(…))`); ölçümde R49'da tarlada yalnız 4 hasat var, R50 boss'u kaybedildi.

| Run | Kol | R46 | R47 | R48 | R49 | R50 |
|---|---|---|---|---|---|---|
| 42 | aday (Zincir Hasat R26) | seviye 196 · temel güç 153 | 262 · 219 | 931 · 417 | 931 · 2.424 (hasar taştı, 4 hasat) | BOSS |
| 52 | almayan eş (zincir yok) | 178 · 136 | 199 · 175 | 245 · 238 | 417 · 376 | 30.779 · 892 (durdu) |

Mekanizma (koddan): grid dolup bütün tile'lar Sv 3'e ulaşınca her level bir **temel güç** kartı verir (`CardSelectionUI.RollBaseStat`:
%2–5 `MorePercent`, çarpımsal ve sınırsız birikir, biri "XP kazancı"). Level maliyeti XP tablosunun sonunda sabit kalır
(`Progression_DengeV1`: 140. seviyeden sonra 150.000 XP). XP kartları bileşik büyüdükçe level, level arttıkça kart artar: kendini
besleyen döngü. Bu seed'de iki kolda da tile'lar ve yükseltmeler R35'te tükeniyor
ve temel güç evresi R35'te başlıyor. Diğer 60 run'dan yalnız 10'u bu evreye giriyor (en erken R37) ve run sonunda en çok 59
temel güç kartında kalıyor (seed 110; ortalama 3). Bu, TODO P7'nin "grid doluyken yükseltme / temel stat kartı geçişi" maddesine ait;
zincirle ilgisi yok ve bu pakette düzeltilmedi. Run 52'nin kalanı ve onuncu çift ayrı bir devam setiyle (`k376zincirdevam`)
tamamlandı; ilk setin sonuç dosyası "FAIL: stuck" olarak olduğu gibi duruyor.

## 7. Performans

Kaynak: laboratuvar (batch, 30 fps sabit adım, round başına 1.354 kare; çizim maliyeti yok, oyun kodunun kare maliyeti).
Son kodla R40: [lab-onceden-kurma](Bolum3-7-6/olcum/lab-onceden-kurma) ve allocation sorusu için ölçüm izleyicisi düzeltilmiş
tekrar: [lab-olcum-izleyici](Bolum3-7-6/olcum/lab-olcum-izleyici). İki tekrarda da oynanış sütunları ilk ölçümle birebir aynı.

| R40, son kod | ms/kare zincirsiz → zincir | En uzun kare zincirsiz / zincir (ms) | GC toplaması |
|---|---|---|---|
| Patlama ağırlıklı | 0,508 → 0,522 | 27,4 / 15,2 | 0 / 0 |
| Elektrik ağırlıklı | 0,468 → 0,473 | 4,6 / 4,7 | 0 / 0 |
| Kasırga / bumerang ağırlıklı | 0,514 → 0,523 | 3,8 / 6,3 | 0 / 0 |
| Karma davranışlı | 0,505 → 0,517 | 5,1 / 8,0 | 0 / 0 |
| Davranışsız (kontrol) | 0,456 → 0,460 | 2,9 / 4,0 | 0 / 0 |

- **CPU:** zincir kolu kare başına +0,004–0,014 ms (davranışsız kontrolde de +0,004: ölçüm gürültüsü düzeyi). Güçlü sinerjide
  (zincir payı %17–19) de GC toplaması yok.
- **Tek kare sıçramaları:** ilk ölçümde Karma R40 zincir kolunda 68,9 / 27,9 / 16,0 / 13,7 ms'lik tek kareler vardı (zincirsiz
  7,1 ms). Neden: zincirin ilk tetiğinde kaynak çizgisi havuzunun ve malzemesinin kurulması. Havuz artık zincir açıkken round
  başında kuruluyor (`HarvestChain.HandleRoundChanged` → `AftershockAreaFeedback.Prewarm`; Artçı için `BehaviorEchoes` aynı şeyi
  yapıyor). Sonra Karma zincir kolunun en uzun karesi 8,0 ms; oynanış değişmedi. Patlamadaki 27 ms'lik tek kare zincirsiz kolda
  da var (eski).
- **Allocation:** `allocBytes` (round boyunca `GC.GetTotalMemory` farkı, sayfa adımlı kaba bir ölçü) zincir kolunda round başına
  +17–139 KB. Aynı büyüklükte fark zincir kodundan önce de ödül alınmış her lab kolunda var (3.7.5.1: Artçı +171–192 KB, Çifte
  Akım +142–262 KB) ve zincirin hiç tetiklenmediği davranışsız düzende de görülüyor. Ölçüm aracının kendi izleyicisi kök başına
  küme açıyordu; düzeltildi, fark değişmedi. Kesin kaynağı (ödül verilmiş lab kurulumu) ayrıştırılmadı; zincir koluna özgü
  saldırı başına allocation kanıtı yok. Oyun kodunda kök bağlamı ve iş listesi havuzlu; sıcak yolda LINQ, sahne taraması ya da
  saldırı başına `new` yok.
- **Kuyruk ve havuz:** gecikme her ölçümde 1 kare; en çok bekleyen iş 3 (sinerjide 7); kasırga / bumerang havuz beklemesi ve
  kaynak çizgisi atlaması ölçümlerde hiç oluşmadı. Testte, 10 kasırga ve 12 bumerang havuzu bilerek doldurulunca bekleyen 9 iş en
  çok 3,6 sn bekledi ve hepsi çalıştı.
- **Birikimli run:** kare maliyeti ölçülmedi (bot run'ı CPU ölçümü için kurulu değil); süre ve kare sütunları kollar arasında
  karşılaştırılmadı.

## 8. İncelenen görüntüler

Görüntüler testin gerçek oyun döngüsünden, kare kare (30 fps sabit adım) alındı; batch'te çizilir ama insan gözüyle oyunda
bakılmadı. Klasör: [Bolum3-7-6/testler](Bolum3-7-6/testler) (her biri tam kare + `_yakin`).

| Görüntü | Ne görünüyor | Değerlendirme |
|---|---|---|
| `T376_01_Zincir_once` | Üç saksı (sol altta patlamalı A, ortada elektrikli saksı, üstte bir saksı); zincir tetiğinden önceki kare | Karşılaştırma için |
| `T376_02_Zincir_nesil1` | A'nın normal patlamasının öldürdüğü bitki, ortadaki saksının elektriğini nesil 1 olarak çalıştırdığı kare: o saksıdan dört çapraz şimşek | Şimşek kaynağı açıkça gösteriyor. Ortadaki saksının ayak izindeki mor çizgi 1×1 saksıda ince, zor seçiliyor |
| `T376_03_Zincir_nesil1_yayilma` | Üç kare sonra: patlama efektleri ve şimşekler sürüyor; mor çizgi ortadaki saksının ayak izinde kısmen görünüyor | Patlama parçacıkları çizgiyi büyük ölçüde örtüyor. Kalıcı ya da tam ekran bir şey yok; çizgi 0,6 sn'de kayboluyor |
| `T376_04_ZincirKarti` | Zincir Hasat kartı, Artçı Patlama ve Hasat Ritmi'nin yanında | Sığıyor; metin görevin istediği beş noktayı oyuncu diliyle söylüyor. Başlıktaki "ERKEN AŞAMA · etkisi Round 4" testin kartı erken round'da ekrana koymasından; gerçek teklif R23'ten sonra gelir |

Sonuç: kaynak saksı vurgusu var ama zayıf; zincirin hangi bitkiden hangi saksıya geçtiği (yayılım) ayrıca çizilmiyor. Görsel
maddesi bu yüzden kapatılmadı.

## 9. Bilinen sınırlamalar

- **İnsan Play testi yok.** Zincirin hissi, sıklığı ve ekrandaki okunurluğu insan gözüyle onaylanmadı; "kırılma hissi çözüldü"
  denmiyor.
- **Kaynak vurgusu zayıf.** Mor çizgi yalnız zincirle tetiklenen saksının ayak izini gösterir; hangi bitkiden hangi saksıya
  geçildiğini çizmez. 1×1 saksıda ince kalıyor ve patlama parçacıklarıyla yarışıyor. P6b'nin görsel maddesi açık.
- **Etki küçük.** Bu prototip değerleriyle sabit statlı laboratuvarda toplam hasat medyanı ×1,00–×1,06; birikimli run'da ödül
  sonrası hasat medyanı ×1,00 (patlama ve elektrik yolu) ile ×1,06 (Davranış-rezonans) arasında. Zincir hasadı run'da görünür
  bir pay alıyor (%7–30) ama toplamı büyütmüyor; almayan eş aynı boss'ta başka bir ödül aldığı için karşılaştırma bu fırsat
  maliyetini de içeriyor. Değer ayarı görev gereği yapılmadı; bu bir "kırılma" sonucu değil.
- **Zincir elektriği ile görsel havuzu.** Zincir elektriği normal elektrikle aynı fonksiyondan geçer (görsel doluysa yalnız
  çizim atlanır, hasar uygulanır; `ElectricRevertVerification` bu yolu doğruluyor). Zincir bağlamıyla görsel havuzu dolu ayrı
  bir test yazılmadı; ölçümlerde görsel atlaması hiç oluşmadı.
- **Normal tetikte havuz kaybı eskisi gibi.** Nesil 0 kasırga / bumerang havuz doluysa kaybolur (eski davranış, sayılıyor).
- **Kart metni:** birikim satırı bütün ödüllerin ortak kalıbından "Seçince 1/1: toplam …" diye yazar; zincir için "toplam"
  sözcüğü anlamsız (Artçı ve Hasat Ritmi'nde de aynı). Değiştirilmedi.
- **Zincirden bağımsız bulgu — geç run seviye patlaması:** Davranış-rezonans politikası, seed 101'de, iki kolda da (zincirli ve
  zincirsiz) R47'den sonra kontrolden çıkıyor. 6. bölümde ayrıntı; P7 kapsamı, bu pakette düzeltilmedi.

## 10. Eski profillere etkisi

- **Ödül yoksa oynanış aynı.** Zincir yalnız Zincir Hasat alınınca açılır; ödül yalnız `Run50_ZincirV1` havuzunda. Eski
  profillerde kök açılmaz, zincir zarı atılmaz, kuyruk çalışmaz. Normal tetiğin zar sırası değişmedi.
- **Kanıt:** yoğun tarla ölçümü (3.7.5.1 ile aynı kapsam, ödülsüz ve Artçı / Çifte Akım kolları dahil) 24 round'un 24'ünde
  oynanış sütunlarında 3.7.5.1'le birebir aynı; eski profillerin ödül teklif dağılımı bayt bayt aynı (md5 `222d6374…`).
  Son kodla (alan çizgisi havuzunun round başında kurulması dahil) yeniden ölçüldü: yine 24 / 24 round, 46 oynanış sütununun
  hepsi aynı; zincir sayaçlarının hepsi 0 (kök açılmadı, zar atılmadı). Çıktı: [Bolum3-7-6/olcum/odulsuz](Bolum3-7-6/olcum/odulsuz).
- Kodda değişen ama eski profilde sonucu değişmeyenler: patlamanın tek bir `Explode` yoluna ayrılması (aynı sıra, aynı hasar; artık
  patlama başına liste ayırmıyor), artçı vuruşlarının bağlam taşıması, alan çizgisi havuzunun Artçı varken round başında kurulması
  (yalnız görsel).
- Profil listesi beklentisi olan iki eski test (`KirilmaV1Verification`, `TakvimV1Verification`) yeni profili tanıyacak biçimde
  genişletildi; başka beklenti değişmedi.

## 11. Profil seçme, normal run'da oynama ve karşılaştırma yolu

**Normal run:**

1. Menü: **Tools → Run Profili → Run50 Zincir V1 · 50 round (dört davranışlı zincir prototipi)**. Play.
2. Davranış kartları al (patlama, elektrik; ağaçtan kasırga ve bumerang). R23'ten sonra boss ödüllerinde Zincir Hasat çıkabilir
   (güçlü aşama, ağırlık 1; garanti değil). Al.
3. Bakılacaklar: doğrudan vurmadığın bir saksının davranışı, kendi bitkisi bir patlama / şimşek / kasırga / bumerangla
   öldüğünde çalışıyor mu; o saksının ayak izi kısa bir mor çizgi alıyor mu; zincir iki adımdan sonra duruyor mu; ekran
   kalabalıklaşıyor mu; tetiklerin sıklığı his olarak fark ediliyor mu?

**Karşılaştırma yolu (aynı tarla, zincir kapalı / açık):**

- **Editor'da, 5–10 dakika:** bir run başlat (herhangi bir profil), birkaç round zincirsiz oyna; round sırasında
  **Tools → Zincir Deneme → Bu run'a Zincir Hasat ver (yalnız Play · karşılaştırma)**; aynı tarlada zincirli oyna. Yalnız Play
  modunda ve round sırasında etkindir; bekleyen bir boss teklifi varken çalışmaz. Ödül o run'a aittir, yeni run'da kalkar;
  gerçek kayda ve profil seçimine yazılmaz. Normal akışın R23 kuralını değiştirmez.
- **İzole kopyada, sayılarla (yaklaşık 1 dakika; ölçüm 25 sn):** R30, seed 1, dört düzen (patlama, elektrik, kasırga / bumerang,
  karma), her biri aynı tarla ve aynı seed'le zincir kapalı ve açık:

```
powershell -ExecutionPolicy Bypass -File Tools/run-isolated-verification.ps1 -Method KirilmaErisimMeasurement.RunChainQuick -Full
```

  Sonuç `Library/VerificationProject/Logs/KirilmaErisim_zincirkisa.csv`; tablo: `python Tools/Balance/ZincirV1/analyze_chain.py lab <klasör> KirilmaErisim_zincirkisa`.
  Asıl projeye, seçili profile ve gerçek kayda dokunmaz.

## 12. Ana TODO'daki karşılığı

| TODO maddesi | Durum | Kanıt |
|---|---|---|
| P6a · Kök, nesil, kaynak, tekrar kümesi, katsayı, bütçe taşıyan bağlam | işaretlendi | `HarvestLink` + `HarvestChain` kök bağlamı; test |
| P6a · Aynı kökte çift en çok bir kez; başarısız denemenin hakkı | işaretlendi | Görevde kararlaştırıldı: deneme hakkı tüketir; A → B → A ve başarısız zar testleri |
| P6a · Çok hücreli saksı, çoklu hedef, ayrı saldırılar | işaretlendi | Test (tek kimlik, tek kök, iki saldırı iki kök) |
| P6a · Kuyruk, kare bütçesi, gecikme ölçümü | işaretlendi | Test (8 + 2 kare, bütçe 3 / 7 ret); ölçümde gecikme 1 kare |
| P6a · Temizlik, havuzlanan bitki kimliği | işaretlendi | Test (round sonu, menü, yeni run, satılan saksı, havuzdan dönen bitki) |
| P6a · Nesil sınırı, düşüş, artçı / yankı katılımı | işaretlendi (not: ilk prototip değerleri, nihai değil) | Görevde kararlaştırıldı; ölçüme göre değiştirilmedi |
| P6b · Patlama ve elektrikle ilk dikey dilim | işaretlendi | Dört davranışla birlikte |
| P6b · Kasırga / bumerang bağlamı; dört davranışın eşleşmeleri | işaretlendi | 16 / 16 eşleşme; gecikmeli vuruşlar kökü tutuyor |
| P6b · Elektrik görsel sınırı; kasırga / bumerang kaybı | işaretlendi (notlu) | Zincir işi havuz doluysa bekliyor, ölçülüyor. Not: normal tetik eskisi gibi kaybolabiliyor; zincir elektriği için görsel havuzu dolu ayrı test yok |
| P6b · Güçlü aşama ödülü; ödülsüzken eski kural | işaretlendi | Ödülsüz oynanış birebir aynı; teklif dağılımı aynı |
| P6b · Kendi saksısı, davranışsız saksıya yetenek yok | işaretlendi | Test; davranışsız kontrol birebir aynı |
| P6b · Zincir görselleri kaynak / yayılımı anlaşılır gösterir | **açık** | Kaynak çizgisi zayıf, yayılım çizilmiyor, insan Play yok |

P6a ve P6b'nin kabul satırları otomatik testle sağlandı. "İki davranışla tamamlandı" raporu yok: dördü birlikte teslim edildi.
P6b'nin görsel maddesi ve insan Play onayı açık; P6 bu yüzden tamamen kapanmış sayılmıyor.
