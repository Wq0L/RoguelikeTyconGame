# Bölüm 3.7.6.1–2 — P7 öncesi sayısal güvenlik ve kontrollü refactor

**Durum (2026-10-03):** A (hata düzeltmesi) ve B (davranışı koruyan refactor) uygulandı ve izole kopyada doğrulandı. Denge paketi
değildir: zincirin gücü, XP ekonomisi, kota, boss takvimi, ödül ağırlıkları ve zincir değerleri değişmedi; yeni içerik, level
sınırı ya da otomatik kart seçimi eklenmedi. Commit atılmadı; açık editöre, seçili profile ve oyuncu kaydına dokunulmadı. P7'ye
geçilmedi.

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md) · Önceki paket: [Bolum3-7-6-DavranisZinciri.md](Bolum3-7-6-DavranisZinciri.md) ·
Çıktılar: [Bolum3-7-6-1-2](Bolum3-7-6-1-2) (`a/` hata düzeltmesinden sonra, `b/` refactor'dan sonra).

## Kısa sonuç

- **Düzeltilen gerçek hatalar (A):** hasar int sınırını aşınca doğrudan vuruş 1'e, kritik negatife, patlama ve elektrik 0'a, kasırga ve
  bumerang 1'e düşüyordu; hasat XP'si ve kaynak negatife taşıyordu; XP döngüsü geçersiz ya da çok büyük XP'de sonsuz döngüye
  girebiliyordu; zincir ayarı NaN'ı kabul ediyordu. Hepsi tek bir güvenli dönüşümden (`NumericSafety`) ve doğrulamalardan geçiyor.
- **Politika:** normal aralıkta eski sonuçlar aynen; int sınırını aşan değer 2 147 483 647'ye doyar; NaN / sonsuz raporlanır
  (tür başına bir kez), oyun durmaz. XP silinmez; level'lar kare başına en çok 256 işlenir, round sonu bekleyen level işini
  bekler, sayaca sığmayan iş hemen durup raporlanır. Bunlar geçici teknik güvenliktir, sınırsız modun sayı modeli değil.
- **Seed 101:** zincirli kol P6'da R49'da çöküp boss'ta kaybediyordu; şimdi R49'da 10 099 hasat yapıyor ve run bitiyor (KAZANDI) —
  ama level işleme R49'dan itibaren teknik sınırda durmuş hâlde. Zincirsiz kol P6 ile aynı ve P6'da duran ölçüm bitiyor (92 334
  kart seçimiyle; bir oyuncu için oynanamaz). Kök neden (XP geri beslemesi) P7'de.
- **Normal aralık değişmedi:** diğer 60 tam run, 300 laboratuvar round'u, ödülsüz ölçüm ve bütün ödül teklifleri P6 ile satır
  satır aynı; 22 eski test aynı kontrol sayılarıyla geçiyor.
- **Refactor (B):** gözlem olayları oynanıştan ayrıldı (bir ölçüm dinleyicisinin hatası hasarı, ölümü ya da zincir kuyruğunu yarıda
  bırakamıyor; görev sayacı oynanış olayına taşındı); debug ödül menüsü yansımayı bıraktı; alan çizgisi sınıfı
  `AreaOutlineFeedback` oldu; GameScene'den beş eski buton, "Legacy Stats" ve "Sound Manager" ile kullanılmayan kod ve alanlar
  temizlendi, `resourcesUI` sahnede bağlandı.
- **B ↔ A:** testler, ödülsüz ölçüm, laboratuvar, seed 101 çifti ve 60 run'lık birikimli set (3 060 satır, 900 ödül teklifi)
  birebir aynı. Tek fark yalnız görsel bir sayaçta ve yalnız seed 101 çiftinde (7. bölüm).
- **Açık:** XP geri beslemesi, seçim ekranı yükü ve round sonu bekleyişi (P7); zincirin toplam hasadı (P8); nihai sayı modeli (P10);
  zincir yayılım görseli ve insan Play testi.

## 1. Bulunan ve düzeltilen gerçek hatalar (A)

Kanıtlar: P6 seed 101 kayıtları ([Bolum3-7-6-DavranisZinciri.md](Bolum3-7-6-DavranisZinciri.md) 6.3) ve bu paketin testi
(`SayisalGuvenlikVerification`, "eski dönüşümler bu platformda" satırı: aynı ifadeler bu Unity / Mono'da çalıştırıldı).

| Yer | Hata (eski davranış) | Kanıt | Düzeltme |
|---|---|---|---|
| Doğrudan vuruş (`PlayerController`) | `Mathf.Max(1, Mathf.RoundToInt(x))`: x ≥ 2³¹ iken `RoundToInt` `int.MinValue` döner, vuruş **1** hasara düşer | P6 run 42 R49: HarvestDamage 2,1e11, tarlada 4 hasat, boss kaybı; test: 3e10 → 1 | `NumericSafety` doyurur: int.MaxValue |
| Kritik vuruş | `Mathf.RoundToInt(hasar × kritik)` taşınca **negatif** hasar (bitki iyileşir) | test: int.MaxValue × 2 → −2147483648 | doyurulur |
| Davranış tabanı (`PlanterBrain.HarvestDamage`) | aynı taşma: patlama ve elektrik **0**, kasırga ve bumerang **1** hasar | test: 3e10 → 0 | doyurulur; zincir aynı değeri kullanır |
| Zincirin taban hasarı (`HarvestChain.Execute`) | tabanı kendi kopyasıyla aynı hatalı dönüşümle okuyordu | — | ortak `PlanterBrain.HarvestDamage` |
| Zincir hasarı çarpan 1 kısa yolu (`HarvestChain.Scale`) | çarpan 1'de değer denetimsiz geçiyordu (negatif hasar dahil) | test: −5 → −5 olurdu | çarpan 1 de güvenli dönüşümden geçer (−5 → 0) |
| NaN dönüşümleri (davranış, saksı bonusu, artçı / ikinci dalga, kasırga rezonansı, skor) | `(int)NaN` = `int.MinValue`: negatif hasar / skor | — | NaN → en küçük değer, bir kez raporlanır |
| Sert Kabuk can çarpanı | `RoundToInt(can × çarpan)` taşınca azami can **1**; NaN çarpan da canı 1 yapıyordu | test: 2e9 × 2 → 1 | can doyar, oran korunur; NaN / ∞ çarpan reddedilir ve raporlanır |
| Hasat XP'si (`PlantResource`) | `RoundToInt` taşınca XP **negatif** | P6 run 42 R49–R50: XP −8,6e9 ve −5,8e10 (= 4 ve 27 hasat × int.MinValue) | XP int'e çevrilmez; eski yuvarlamayla aynı tam sayı, double |
| XP ve level döngüsü (`ProgressionManager`) | XP `float`: NaN XP levelları sessizce durdurur; +∞ XP ve ≤ 0 maliyet sonsuz döngü; çok büyük XP'de maliyet `float` hassasiyetinin altında kalınca XP azalmadan level artar (sonsuz döngü); 30 bin level tek çağrıda işlenir | P6 run 52: R49 → R50 30 779 level tek karede | XP `double`; geçersiz XP reddedilir; kare bütçesi; teknik sınırda durur ve raporlar (XP silinmez) |
| Kaynak (`PlantResource`, `ResourceManager`, uzmanlaşma ölçeği) | ödül `RoundToInt` taşması, kopya ödülde `*= 2`, bankada `+=` taşması: **negatif banka** | P6 run 52 R50: round başına 193 milyon altın, iki round sonra taşardı | dönüşüm ve toplama int sınırında doyar |
| Kart hakkı sayaçları (`RoundManager`) | bekleyen seçim ve verilen hak sayaçları int taşmasına açık | — | doyar ve raporlanır |
| Zincir ayarı (`HarvestChainConfig.TryCreate`) | NaN şans / hasar çarpanı karşılaştırmalardan geçiyordu (NaN < 0 yanlış döner), +∞ hasar kabul ediliyordu | test | NaN / ∞ reddedilir; geçersiz ödül hiç uygulanmaz |
| XP tablosu (`ProgressionSO`) | NaN / ∞ maliyet ve ≤ 0 formül tabanı denetlenmiyordu | — | `Validate`; çalışma anında maliyet her hesaplandığında da denetlenir |

## 2. Sayısal güvenlik politikası ve geçici sınırlar

**Tek yer: `NumericSafety`** (`Assets/Scripts/Stats/NumericSafety.cs`). Oyunun int hasar, can, kaynak ve skor değerine dönüşüm
yalnız buradan geçer; çarpanlar mevcut ortak hesaplarda (RunPower, PlanterBrain, saksı bonusu, zincir neslinin çarpanı) eskisi gibi
bir kez uygulanır, burada yalnız dönüşüm vardır.

| Girdi | Sonuç | Raporlama |
|---|---|---|
| Normal aralık | Eski yuvarlama aynen (`Mathf.RoundToInt` ile aynı: Math.Round, çift sayıya), sonra çağıranın eski en küçük değeri (doğrudan vuruş ve zincir nesli 1, davranış, saksı bonusu, artçı, kaynak, skor 0) | yok |
| int sınırını aşan sonlu pozitif ve +∞ | `int.MaxValue`'ya **doyar** (2 147 483 647); 1'e, 0'a ya da negatife taşmaz | sayılır; yer başına bir kez uyarı |
| NaN ve −∞ | geçersiz: çağıranın en küçük değeri; oyun durmaz | sayılır; yer başına bir kez hata logu (vuruş başına log yok) |
| Sonlu negatif | en küçük değere kırpılır (eski `Math.Max(0, …)` kuralı) | yok |
| İki toplam (banka, kart hakkı) | int sınırında doyar | sayılır; bir kez uyarı |

- **Çarpım dönüşümden önce taşmaz:** her yer çarpımı eski türüyle yapar (doğrudan ve kritik `float`, davranış ve bonus `double`);
  `float`'ta 3,4e38'e kadar sonlu kalır, aşarsa +∞ olur ve doyar. int ara sonucu kalmadı (kritik `int × float` artık `float`
  olarak dönüşüme gelir).
- **Doyum, stat ile uygulanan hasarı ayırır.** HarvestDamage stat'ı 3e10 olabilir; uygulanan vuruş 2 147 483 647'dir. Bitki canı da
  int'tir ve en çok aynı değerdir: doymuş bir vuruş tek başına her bitkiyi öldürür (saksı bonusu da doyar). Kasırga (×0,5) ve
  bumerang (×0,65) kendi çarpanlarını doymuş tabana uygular (1 073 741 824 / 1 395 864 320); zincir nesli de doymuş normal hasara
  (nesil 1: 1 610 612 735). Bu yüzden doyumdan sonra daha fazla hasar stat'ı vuruşu büyütmez; ekrandaki sayı uygulanan değerdir.
- **XP int değildir:** hasat XP'si eski yuvarlamayla aynı tam sayıdır ama `double` saklanır (float çarpım +∞'a taşarsa çarpım
  `double`'da yapılır). Geçersiz XP (NaN, ∞, negatif) eklenmez, raporlanır. Kazanılmış XP hiçbir durumda silinmez ya da kırpılmaz.
- **Level işleme kare bütçesiyle:** bir karede, bütün hasatların toplamında en çok 256 level işlenir. Normal oyunda bütçe hiç
  dolmaz: level'lar eskisi gibi `AddXP` içinde, aynı çağrıda ve karede işlenir. Bütçeyi aşan level işi saklanır ve sonraki
  karelerde 256'şar işlenir. Her level `OnLevelUp`'ı bir kez çağırır ve kendi kart hakkını o anda ekler. (Bütçe ilk yazımda çağrı
  başınaydı; seed 101'de her hasat binlerce level getirdiği için kare başına iş hasat sayısıyla çarpıldı ve round dakikalarca
  sürdü. 4. bölümde.)
- **Round sonu bekler:** süre bitince kota / boss değerlendirmesi ve round sonu temizliği eskisi gibi hemen yapılır; kart kararı ise
  bekleyen level işi bitene kadar ertelenir (o sırada oyuncu saldırmaz). Normal oyunda bekleyen iş olmaz ve karar aynı karede
  verilir. Kota / boss başarısızlığı ve kartsız son round beklemez (run zaten biter). Run bittikten sonra ve menüde kalan level işi
  işlenmez; yeni run ve sahne değişimi yeni bir ilerleme nesnesiyle başlar.
- **Teknik sınırda durur, raporlar:** level işleme şu durumlarda durur: geçersiz maliyet (NaN, ≤ 0); maliyetin sayı sınırını
  aşması (+∞); level sayacının int sınırı; maliyetin saklı XP'nin hassasiyetinden küçük kalması (XP ≈ 1e21'in üstü); ve **bekleyen
  level sayısının level sayacına sığmaması** (tablonun sonunda maliyet sabittir, bekleyen sayı saklı XP ÷ maliyettir; 2,1 milyarı
  aşıyorsa iş hiçbir zaman bitmez — günlerce işleyip round sonunu bekletmek yerine hemen durur). XP saklı kalır, neden bir kez
  hata olarak yazılır, round sonu durmuş işi beklemez; run level kazanmadan sürer. Bu, sınırsız modun sayı modeli değildir;
  yalnız oyunun kilitlenmemesini ve kaybın sessiz olmamasını sağlar. Sayaca sığan ama çok büyük bir iş durdurulmaz: bütçeyle
  işlenir ve round sonu onu bekler (ör. 10 milyon level 60 fps'de ~11 dakika). Bu bekleme P7'nin konusudur (XP geri beslemesi).
- **Veri doğrulaması:** zincir ayarında NaN / ∞ çarpan, XP tablosunda NaN / ∞ maliyet ve ≤ 0 formül tabanı, Sert Kabuk'ta NaN / ∞ can
  çarpanı reddedilir (ödül hiç uygulanmaz, tablo level vermez, çarpan uygulanmaz) ve raporlanır.

**Geçici sınırlar (TODO'ya eklendi):** int hasar / can / kaynak 2 147 483 647'de doyar; `float` stat'lar 3,4e38'de +∞ olur (hasar
doyar, XP verilemez ve raporlanır); XP `double`'dır, ~1e21'in üstünde level işleme durur; level ve kart hakkı sayaçları int'tir.
Kartla çarpımsal büyüyen bir oyunda bunlar sınırsız modun nihai sayı modeli değildir (endless sayı modeli P7 / sonraki).

## 3. Seed 101: önce / sonra

Kabul senaryosu: `Run50_ZincirV1`, Davranış-rezonans politikası, seed 101, zincirli ("aday") ve zincirsiz ("almayan eş") kol —
P6'da taşan iki run'ın aynısı (`BalanceRunMeasurement.RunK3761Seed101`, run başına 25 dakika gerçek süre sınırı). Çıktı:
[Bolum3-7-6-1-2/a/olcum/seed101](Bolum3-7-6-1-2/a/olcum/seed101). P6 kayıtları: `Docs/Bolum3-7-6/olcum/run` (run 42 ve 52).

| | Zincirli kol (aday) | Zincirsiz kol (almayan eş) |
|---|---|---|
| **P6 (önce)** | R49'da hasar taştı: 4 hasat, 12 skor; XP negatife taştı (−8,6e9). R50'de 27 hasat; boss 212 / 1 100 → **BOSS (kayıp)** | R50'de 30 779 level tek karede; 92 334 kart seçimi; ölçüm 240 sn takılma sınırında **durdu** (set FAIL) |
| İlk sorunlu round | **R49** (hasar ve hasat XP'si int sınırını aşıyor) | **R50** (sayısal taşma yok; seçim ekranı yükü) |
| R46–R48 | P6 ile birebir aynı (level 196 / 262 / 931) | P6 ile birebir aynı (level 178 / 199 / 245) |
| R49 level / XP / seçim | level işleme level 5 027'de **durdu** (bekleyen iş 2,17e9 level, sayaca sığmıyor); round XP'si 2,36e16 (saklı, silinmedi); round sonunda 12 288 seçim | level 417; XP 2,6e7; 516 seçim (P6 ile aynı) |
| R49 hesaplanan → uygulanan hasar | stat 213 908 668 416 → en büyük doğrudan vuruş **2 147 483 647** | stat 6 310 → en büyük vuruş 207 202 (doyum yok) |
| R49 doyum | **evet**: 333 028 doyan dönüşüm (doğrudan, kritik, davranış tabanı, davranış, saksı bonusu, kaynak) | hayır |
| R49 hasat / skor | **10 099 hasat, 487 583 skor** (P6: 4 hasat, 12 skor) | 10 187 hasat, 345 636 skor (aynı) |
| R50 | stat +∞ (float sınırı da aşıldı) → uygulanan 2 147 483 647; 313 986 doyum; 9 818 hasat; boss **448 862 / 1 100**. XP çarpanı da +∞: 9 818 hasadın XP'si eklenemedi (sayıldı, bir kez raporlandı) | level 30 779 (round sonunda bekleyen level işi yok); XP 4,55e9; 9 926 hasat; boss 328 246 / 1 100; 92 334 seçim (hepsi P6 ile aynı) |
| 1 hasara düşme | **ortadan kalktı** | bu kolda yoktu |
| Ölçüm bitti mi | **evet — KAZANDI @R50**, ama level işleme R49'dan itibaren teknik sınırda durmuş hâlde (aşağıda) | **evet — KAZANDI @R50**; bot 92 334 kart seçimini süre sınırı içinde yaptı |

Açık söylenmesi gerekenler:

- **Zincirli kol "kazandı" ama sağlıklı bir run değil.** Level işleme R49'un ilk saniyesinde durdu ve run sonuna kadar level
  verilmedi; saklı XP 2,36e16 (≈ 1,6e11 level) kullanılmadı. R50'de hasat XP'si sonsuz olduğu için hiç eklenemedi. Hasar iki round
  boyunca int sınırında doydu; banka altını da doydu. Bunlar düzeltme değil, teknik sınırdır: oyun kilitlenmedi ve hiçbir kayıp
  sessiz olmadı, ama oyun bu noktada tasarlandığı gibi çalışmıyor. Kök neden XP geri beslemesidir (temel güç kartlarının çarpımsal
  birikmesi + tablonun sonunda sabit level maliyeti) ve P7'nin konusudur.
- **Zincirsiz kol sayısal olarak sağlam ama oynanamaz.** R50 sonunda 92 334 kart seçimi var. Bot bunları süre sınırı içinde yaptı
  (iki run birlikte 25 dakika sürdü); bir oyuncu yapamaz. Otomatik seçim, atlama ya da level sınırı eklenmedi (P7).
- **İki ara tasarım denendi ve bırakıldı** (kayıtları [a/gelistirme](Bolum3-7-6-1-2/a/gelistirme)): (1) bütçe çağrı başınayken zincirli
  kolun R49'u 14 dakikada bitmedi (her hasat 256 level işliyordu); (2) bütçe kare başına olunca R49 normal sürede bitti ama round
  sonu 1,57e11 level bekledi: 900 saniyede 186 milyon level işlendi, round kapanmadı. Bu yüzden "sayaca sığmayan iş hemen durur"
  kuralı eklendi.
- Atlanan patlama görseli sayısı (`skipExplosionVisual`) R47–R50'de P6'dan farklı (ör. zincirsiz kol R48: 754 → 765). Yalnız
  görsel bir sayaçtır: patlama havuzu (121) dolduğunda parçacık ömürlerine bağlıdır; hasar ve bütün oynanış sütunları aynıdır.
  Bu çiftin iki ayrı A çalıştırmasında birebir aynı çıktı, ama P6, A ve B arasında farklı (B'de 7 satır, ör. zincirli kol R48:
  196 → 211; patlama hasatları ve oyun zamanına bağlı elektrik görseli sayacı aynı). Yani bu sayaç oyun durumuyla tam belirlenmiyor;
  nedeni (parçacık benzetiminin neye bağlı olduğu) doğrulanmadı. Karşılaştırma aracı bu sütunu oynanış farkı saymaz ama ayrı
  satırda raporlar.

## 4. A'nın doğrulaması

Bütün testler ve ölçümler izole kopyada (`Library/VerificationProject`), düşük öncelikle çalıştı. Çıktılar:
[Bolum3-7-6-1-2/a](Bolum3-7-6-1-2/a).

**Yeni test: `SayisalGuvenlikVerification`** — PASS 33. Bilerek üretilen hata logları (NaN, reddedilen ödül, duran level işleme)
önceden bildirilir; bildirilmemiş tek bir hata testi düşürür, bildirilenin yazılmamış olması da.

| Konu | Doğrulanan |
|---|---|
| Sınır değerleri | Normal aralıkta eski yuvarlama ve en küçük değerler; 2³¹ çevresinde `float` ve `double` doğru dönüşür ya da doyar; NaN ve −∞ en küçük değeri döner, 1 003 kez sayılır, **bir** hata logu |
| Eski formüllerle eşdeğerlik | 3 × 100 000 normal aralık örneğinde doğrudan, `double` ve kritik dönüşümleri eski ifadelerle birebir aynı (0 fark) |
| Zincir ayarı | NaN şans, +∞ ve NaN hasar, 1,5 şans, kısa dizi reddedilir; geçersiz ödül hiç uygulanmaz (stack yok, zincir kapalı) ve raporlanır; `Scale` çarpan 1'de −5 → 0, NaN çarpan → 1 ve rapor, doyum |
| Yüksek hasar (stat 3e10) | Doğrudan vuruş 2 147 483 647 (eskiden 1); kritik doyar (eskiden negatif); patlama ve elektrik 2 147 483 647 (eskiden 0); kasırga 1 073 741 824, bumerang 1 395 864 320 (eskiden 1); artçı ve ikinci dalga 2³² hesaplanır, 2 147 483 647 uygulanır; zincir nesli 1 610 612 735; int.MaxValue canlı bitkiler ölür |
| Sert Kabuk | 2e9 canlı bitkide ×2: azami can doyar (eskiden 1), oran korunur, çarpan kalkınca eski değerlere döner; NaN / ∞ çarpan reddedilir |
| Zincir elektriği, görsel havuzu dolu | çizim atlanır (sayılır), 4 hedefin hepsi hasar alır ve hasat edilir; 6 hasat = 6 XP verişi (bir ölüm, bir ödeme) |
| Round sonu temizliği | 2 zincir işi kuyrukta, zincirden doğan kasırga ve bumerang canlıyken round biter: hepsi kalkar, kökler bırakılır |
| XP, normal | 3 level'lık XP aynı çağrıda ve karede 3 level verir, 0 XP artar |
| XP, büyük (1e9, aynı karede dört çağrı) | kare tam bütçesini (256) işler; 27 karede 6 764 level, 2 600 XP artık, 20 292 seçim — küçük referans hesapla birebir aynı; her level `OnLevelUp`'ı bir kez, sırayla çağırır |
| Round sonu bekleyişi | bekleyen level işi varken round bitiş durumunda kalır; iş bitince kart ekranı referanstaki 19 998 seçimle açılır |
| Geçersiz XP | NaN, +∞, negatif eklenmez; 4 kez sayılır, bir kez raporlanır |
| Teknik sınırlar | 1e16 XP (6,7e10 level; sayaca sığmaz) hemen durur ve nedenini söyler, XP durur, round sonu beklemez; 1e22 XP tablo başında (maliyet hassasiyetin altında) durur; NaN maliyet ulaşıldığı anda durur; `ProgressionSO.Validate` bozuk tabloyu reddeder |
| Run sonu ve yeni run | kota tutmayınca run hemen biter (level beklemez), efektler ve kökler kalkar, seçim kalmaz; bittikten sonra level işlenmez; yeni sahnede level 1, saklı XP yok, iş yok |

**Regresyon (A kodu):** `ZincirV1Verification` 38 ve 20 eski test PASS (AlanGeriBildirimi 23, KirilmaErisimiV1 35, BedelliOdullerV1 93,
OdulAsamalariV1 71, BossReward 127, KirilmaV1 189, TakvimV1 162, DengeV1 86, HarvestBehavior 114, ElectricRevert 40, Specialization 100,
StartLoadout 88, Mechanics 88, SimpleResonance 293, ContactRefactor 175, RunPrototype 69, BossWeather 22, RoundPreview 47,
Run50Reference 70, OptionsMenu 22 satır). Hiçbir beklenti değiştirilmedi. Liste: [a/testler/TopluSonuc.txt](Bolum3-7-6-1-2/a/testler/TopluSonuc.txt).

**Normal aralıkta eski davranış korundu (A ↔ P6, aynı seed ve koşullar):**

| Ölçüm | Karşılaştırılan | Sonuç |
|---|---|---|
| Ödülsüz yoğun tarla (24 round; Artçı / Çifte Akım kolları dahil) | 3.7.5.1 | 24 / 24 satır, 45 oynanış sütunu aynı; görsel sayaç da aynı |
| Zincir laboratuvarı R23 / R30 / R40 (300 round) | P6 | 300 / 300 satır, 71 sütun aynı; görsel sayaç da aynı |
| Birikimli set, 60 tam run (3 politika × 10 seed × 2 kol; seed 101 Davranış-rezonans çifti hariç) | P6 (`k376zincir` + devam) | 3 060 / 3 060 satır, 95 sütun aynı; görsel sayaç da aynı; 60 / 60 KAZANDI |
| Boss ödülü teklifleri ve seçimleri (aynı 60 run) | P6 | 900 / 900 satır aynı |
| Eski profillerin teklif dağılımı / bedelli ödül ölçümü | md5 | aynı (`222d6374…` / `6d628d3f…`) |

**Hata düzeltmesinin değiştirdiği sonuç** yalnız seed 101'in zincirli koludur (R49–R50; 3. bölüm). Zincirsiz kolu R50'ye kadar P6 ile
aynıdır ve P6'da duran ölçüm artık bitiyor. Karşılaştırma aracı: `Tools/Balance/SayisalGuvenlik/compare.py`.

**Geliştirme sırasında çıkan hatalar (giderildi):**

- **Level bütçesi çağrı başınaydı.** Seed 101'de her hasat binlerce level getirdiği için her karede hasat sayısı × 256 level
  işlendi; R49 14 dakikada bitmedi. Bütçe kare başına yapıldı. İlk test tek çağrıyı sınadığı için farkı göremedi; test aynı karede
  dört çağrı yapacak biçimde güçlendirildi.
- **Round sonu pratikte kapanmıyordu.** Kare başına bütçeyle R49 normal sürede bitti ama 1,57e11 level bekledi. "Sayaca sığmayan
  iş hemen durur" kuralı eklendi (2. bölüm).
- Testin iki adımı "tekrarla" değeri döndürüyordu: adım her 20 ms'de yeniden çalışıp 1e9 XP ekledi. Düzeltildi.
- Geçersiz XP raporunun metni yanlıştı ("en küçük değer kullanıldı"; XP hiç eklenmiyor). Düzeltildi.
- Ölçüm aracı bir kez PowerShell ile yeniden yazılınca dosyaya BOM eklendi; kaldırıldı.
- Arka plan komutlarının 2 saat sınırı yüzünden tur iki parçaya bölündü; bir oturum kesintisinden ve yukarıdaki iki kod
  değişikliğinden sonra A turu baştan çalıştırıldı (raporlanan sonuçlar son A koduna aittir).
- B'nin yeniden adlandırma betiği, A'nın 60 run'lık işi çalışırken izole kopyadaki eski bir kaynak dosyasını sildi. Koşu
  etkilenmedi (Play sırasında yeniden derleme yok): 60 run tamamlandı ve P6 ile birebir aynı.

## 5. Refactor edilen sorumluluklar (B)

| Sorumluluk | Önce | Sonra |
|---|---|---|
| Sayısal dönüşüm (madde 6) | Her yerde kendi kopyası: bir kısmı `(int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(…)))`, bir kısmı korumasız `Mathf.RoundToInt`; zincirde taban hasarın ikinci kopyası | Tek yer: `NumericSafety` (A'da kuruldu, çünkü düzeltme merkezi politikayı gerektiriyordu). Zincir tabanı `PlanterBrain.HarvestDamage`'ı kullanır. B'de kod yeniden tarandı: hasar / can / kaynak / XP / skor için başka kopya kalmadı |
| Gözlem olayları (madde 7) | `PlantHealth.AnyDamaged`, `AnyHarvested`, `HarvestChain.Traced`, `BehaviorEchoes.Traced` düz C# olaylarıydı: bir dinleyicinin istisnası hasarı uygulanmadan, bitkiyi havuza dönmeden, zincir kuyruğunu sıkıştırılmadan bırakabiliyordu. Görev sayacı (`QuestTracker`, oynanış) "ölçüm için" olan `AnyHarvested`'ı dinliyordu | `ObserverEvents`: dinleyici dizisi abone olurken kurulur (yayında allocation yok), her dinleyici ayrı çağrılır, istisna yakalanır ve dinleyici başına bir kez raporlanır; sıradaki dinleyiciler ve oynanış sürer. Görev sayacı ayrı bir oynanış olayına (`PlantHealth.Harvested`) taşındı: onun hatası yutulmaz, yukarı çıkar; bitki yine havuza döner |
| Debug ödül verme (madde 8) | `ChainDebugMenu` yöneticinin özel alanlarını (`offer`, `offerPrepared`, `IsPending`) yansımayla yazıp sahte bir teklif kuruyordu | `BossRewardManager.EditorGrant` (`#if UNITY_EDITOR`): oyunun kendi alınabilirlik denetimi (`BlockOf`: stack, çakışma, seçim hakkı aralığı, koşul) ve tek işlemli uygulaması (`TryApply`). Menü yalnız bunu çağırır; yansıma yok. Geçersiz ya da alınmış ödül reddedilir. Menü `Assets/Editor`'da, giriş Editor bloğunda: build'e girmez |
| Ortak alan çizgisi (madde 9) | `AftershockAreaFeedback`: adı Artçı'dan, ama zincir kaynağını da çiziyordu | `AreaOutlineFeedback` (dosya GUID'i aynı). Girişler `PlayAftershock` / `PlayChainSource`; sayaçlar `AftershockPlayed` / `AftershockSkipped` ve `ChainPlayed` / `ChainSkipped`. Havuz (8), geometri, renkler ve süreler aynı. Zincirin yayılım görseli bu değişiklikle tamamlanmış sayılmaz (TODO'da açık) |
| Sahne ve ölü kod (madde 10) | 6. bölüm | 6. bölüm |

Bilerek dokunulmayanlar (madde 11): `HarvestChain` bölünmedi; yeni framework, servis locator ya da ikinci stat sistemi yok; bumerangın
tarla taraması değiştirilmedi; zincirin şans, alan, hasar ve tekrar kuralları aynı. `NumericSafety` dışında kalan dönüşümler hasar
dönüşümü değildir: can tablosunun kendi tavan yuvarlaması (`PlantHealthScalingSO`, zaten doyuyor), round özetinin gösterim
kırpmaları (`GameFeelDirector`), ızgara ve görsel hesapları.

Test yardımcıları (`RewardOfferLab.Grant` ve eski testlerin kendi `Grant` kopyaları) hâlâ hazır teklif enjekte eder: bu, teklif
akışını sınayan test düzeneğidir ve bu pakette değiştirilmedi.

## 6. Silinen sahne kalıntıları

Kayıtlı `Assets/Scenes/GameScene.unity` üzerinde. Açık editöre komut gönderilmedi: temizlik izole kopyada bir editör betiğiyle
(`Tools/Verification/Editor/SceneCleanup3762.cs`, Unity API'siyle) yapıldı, kaydedilen dosya asıl projeye taşındı. Raporlar:
[b/sahne](Bolum3-7-6-1-2/b/sahne).

| Silinen | Neden güvenle silindi |
|---|---|
| Beş eski "Sellect Grass planter…" butonu (1×1, 1×2, 1×3, 2×2, 2×3) ve alt nesneleri ("Grass Button Text", "Comic Coin"): 15 nesne | Hepsi kapalıydı (`m_IsActive: 0`), yeni mağaza panelinin yanında duruyordu. Silinecek alt ağaçların dışındaki hiçbir bileşen onlara referans vermiyor (betik bütün sahnedeki bütün nesne referanslarını taradı: 0). `OnClick` listeleri boştu |
| `GrassPlanterButtonUI.cs` (kod) | Yalnız bu beş butonda kullanılıyordu (GUID sahnede 5 kez; başka sahne, prefab, kod ya da test yok). Butonlarla birlikte ölü kod oldu |
| "Legacy Stats" metni | Kapalı, hiç gösterilmiyordu. Tek referans `PlanterShopPanelUI.stats` alanıydı; panel her seçimde bu gizli metne dört satırlık yazı üretiyordu. Alan ve yazı üretimi koddan kaldırıldı |
| "Sound Manager" nesnesi ve `SoundManager.cs` | `SoundManager.PlaySound` / `Initialize` hiçbir yerden çağrılmıyor (kod, test, sahne, prefab tarandı); klip listesi boştu; ses `FeelAudio` üzerinden çalışıyor |
| `ProgressionManager.possibleModifiers` alanı ve sahnedeki 24 kayıtlık listesi | Alan hiçbir yerde okunmuyordu |
| `RoundManager`'daki `exhaustThreshold` / `exhaustRounds` / `exhaustFromRound` değerleri | Kodda karşılığı yok (Tarla Tükendi kuralından kalma); sahne kaydedilirken yazılmadı |

**Bağlanan:** `UIManager.resourcesUI` boştu ve çalışma anında `GoldUI`'nin ebeveyni aranıyordu; artık sahnede aynı nesneye
(`$UI/Canvas/[Game]/ResourcesUI`) açıkça bağlı. Çalışma anındaki yedek arama kodda duruyor (bağ varken çalışmaz).

**Dokunulmayanlar (sayıldı):** 121 zemin hücresi → 121, 140 skill UI yuvası → 140; oyun akışında açılan kapalı paneller ve çalışma
anında kurulan yöneticiler yerinde. `GameManager` başlangıç mimarisi ve debug başlangıç bütçesi değişmedi; sahne yeniden kurulmadı,
hiçbir nesne yeniden adlandırılmadı.

**Sahne dosyasındaki fark (HEAD'e göre, nesne bloğu bazında):** 1 378 bloktan 82'si silindi (17 nesne ve bileşenleri), hiçbir blok
eklenmedi, kalan blokların sırası aynı; 7 bloğun içeriği değişti: üç ebeveynin çocuk listesi, `possibleModifiers`, `exhaust*`, `stats`
ve `resourcesUI`. Unity kaydederken iki bileşene sahnede yazılı olmayan dört alanı kod varsayılanlarıyla eklemişti
(`RoundManager.quotaSegmentRounds / quotaStart / quotaGrowth`, `RunComplateUI.menuSceneName`); kapsam dışı oldukları ve
varsayılanları sahneye sabitleyecekleri için dosya taşınırken çıkarıldı (davranış aynı: değerler zaten kod varsayılanları).

**Sahne üreten araçlar:** `ComicUIBuilder` mağazayı yeniden kurarken "Legacy Stats"ı her seferinde üretip `stats` alanına bağlıyordu;
o satırlar kaldırıldı (alan artık yok; kalsaydı araç hata verirdi). Eski butonları, Sound Manager'ı ya da diğer kalıntıları
üreten bir araç yok.

Not: Unity editörü açıksa sahne dosyasının diskte değiştiğini fark edip sahneyi yeniden yüklemek isteyecektir.

## 7. B'nin doğrulaması: A ile aynı seed ve koşullar

A'nın çalışan hâli referans alındı; B'den sonra aynı işler aynı seed'lerle ve aynı sırayla yeniden çalıştırıldı. Çıktılar:
[Bolum3-7-6-1-2/b](Bolum3-7-6-1-2/b). Karşılaştırma `Tools/Balance/SayisalGuvenlik/compare.py` ile, satır satır.

| Ölçüm (B ↔ A) | Sonuç |
|---|---|
| Ödülsüz yoğun tarla (24 round) | 24 / 24 satır, 71 sütun aynı (hasat, skor, tetikler, yankılar, zincir sayaçları) |
| Zincir laboratuvarı R23 / R30 / R40 (300 round) | 300 / 300 satır, 71 sütun aynı (hasat, skor, nesil başına deneme / tetik / yürütme, retler, kuyruk) |
| Eski profillerin teklif dağılımı / bedelli ödül ölçümü | md5 aynı (`222d6374…` / `6d628d3f…`) |
| Seed 101 kabul çifti | 102 / 102 satır, 101 sütun aynı (doyum sayıları, saklı XP ve duran level işleme dahil); 30 / 30 ödül teklifi aynı; iki kol yine KAZANDI @R50. Yalnız görsel sayaç (atlanan patlama görseli) 7 satırda farklı |
| Birikimli set, 60 tam run (hasat, skor, XP, level, kartlar, kaynak) ve boss ödülü teklifleri / seçimleri | 3 060 / 3 060 satır, 101 sütun aynı; görsel sayaç da aynı; 60 / 60 KAZANDI; doyan / geçersiz dönüşüm 0. Ödül teklifleri ve seçimleri 900 / 900 satır aynı (dosya md5'i de aynı); skill düğümü dosyası md5 aynı. Kullanıcı isteğiyle diğer B ölçümlerinden ayrı, 4 Ekim 00:25–01:19'da çalıştırıldı; arada kod değişmedi. Karşılaştırma: [b/olcum/Karsilastirma_run_B-A.md](Bolum3-7-6-1-2/b/olcum/Karsilastirma_run_B-A.md) |
| Eski testlerin kontrol sayıları | 22 testin hepsi A ile aynı sayıda kontrolle PASS |

Yeni B testi `Refactor3762Verification` — PASS 8:

| Konu | Doğrulanan |
|---|---|
| Sahne | Beş eski buton, "Legacy Stats" ve "Sound Manager" yok; `SoundManager` sınıfı yok; `possibleModifiers` ve `stats` alanları yok; sahne dosyasında `exhaust*`, `possibleModifiers`, "Sellect Grass" geçmiyor; `ComicUIBuilder` "Legacy Stats" üretmiyor; `resourcesUI` sahnede `GoldUI`'nin ebeveynine bağlı; 121 zemin hücresi ve 140 skill yuvası yerinde |
| Hata atan gözlemciler | Dört gözlem olayının her birine önce hata atan, sonra sayan bir dinleyici takıldı. Hasar yine uygulandı; üç bitki (doğrudan, normal patlama, zincir patlaması) öldü, havuza döndü ve birer kez ödedi; oynanış hasat olayı çalıştı; sayan dinleyiciler her olayı aldı; zincir kuyruğu, kökler ve artçı işi sızmadan bitti; 10 istisna yakalandı, her dinleyici bir kez raporlandı |
| Oynanış hatası yutulmaz | Oynanış olayındaki (görev sayacının yolu) istisna saldırıdan dışarı çıkıyor; bitki yine havuza dönüyor; görev sayacı oynanış olayında, gözlem olayında değil |
| Editor ödül girişi | İlk veriş çalışır (zincir açılır); ikincisi reddedilir (stack sınırı); geçersiz zincir kopyası hiçbir şey uygulanmadan reddedilir; bekleyen boss teklifi varken reddedilir; menü dosyasında yansıma yok |
| Ortak alan çizgisi | Sınıf `AreaOutlineFeedback` (dosya GUID'i aynı, eski ad yok); 8 dalgalık tek havuz dolduğunda hem artçı hem zincir kaynağı çizgisi atlanır ve ayrı sayılır |

Geliştirme sırasında B'de çıkan tek test hatası testin kendisindeydi: bitkinin havuza dönüşü bilerek `LateUpdate`'e ertelenir
(`PlantPool`), test aynı adımda bakıyordu; kontrol bir sonraki adıma alındı.

## 8. Son kodla çalıştırılan testler ve loglar

Hepsi son kodla (A + B), izole kopyada, düşük öncelikle. Özet: [b/testler/TopluSonuc.txt](Bolum3-7-6-1-2/b/testler/TopluSonuc.txt);
her testin tam çıktısı aynı klasörde; ölçümler [b/olcum](Bolum3-7-6-1-2/b/olcum).

| Test / ölçüm | Sonuç |
|---|---|
| SayisalGuvenlikVerification (yeni, A) | PASS 33 |
| Refactor3762Verification (yeni, B) | PASS 8 |
| ZincirV1Verification | PASS 38 |
| AlanGeriBildirimi / KirilmaErisimiV1 / BedelliOdullerV1 / OdulAsamalariV1 / BossReward | PASS 23 / 35 / 93 / 71 / 127 |
| KirilmaV1 / TakvimV1 / DengeV1 | PASS 189 / 162 / 86 |
| HarvestBehavior / ElectricRevert / Specialization / StartLoadout / Mechanics | PASS 114 / 40 / 100 / 88 / 88 |
| SimpleResonance / ContactRefactor / RunPrototype / BossWeather / RoundPreview / Run50Reference / OptionsMenu | PASS 293 / 175 / 69 / 22 / 47 / 70 / 22 satır |
| Ödül teklif dağılımı / bedelli ödül ölçümü | md5 aynı |

23 test PASS, açıklanmamış FAIL yok. Değiştirilen beklenti yok: eski testlerde yalnız yeniden adlandırılan sınıfın ve üyelerinin
adları güncellendi (`AlanGeriBildirimiVerification`, `ZincirV1Verification`, `KirilmaErisimMeasurement`); kontrol sayıları aynı.

Ölçüm aracındaki değişiklikler (`BalanceRunMeasurement`): takılma dedektörü level ve kart seçimi ilerlemesini de ilerleme sayıyor;
isteyen sete run başına gerçek süre sınırı (sınırda run "durduruldu" diye kaydedilir, tamamlanmış gösterilmez); satır sonuna altı
sütun (en büyük doğrudan vuruş, doyan / geçersiz dönüşüm sayısı, saklı XP, bekleyen level işi, duran level işleme); botun gelir
sayacı `long`; iki yeni set (`k3761`, `k3761seed101`). Eski setlerin sütunları ve değerleri aynı (yukarıdaki karşılaştırmalar).

Diğer kontroller: yedi asset üreticisi `--check` ile manifestle aynı; üretim koruması testi geçiyor; seçili profil dosyasına ve
oyuncu kaydına bu paket dokunmadı (seçili profil 3 Ekim 13:54'te editörden Run50 Zincir V1 yapılmış; md5 `8150e65b…`, o günden
beri aynı; `meta.json` md5 `5bc3be8a…`); HEAD `28e4650`, commit yok. Yeni dosyaların `.meta` dosyaları eklendi
(`NumericSafety.cs.meta`, `ObserverEvents.cs.meta`); `AreaOutlineFeedback.cs.meta` eski GUID'i taşıyor; silinen iki betiğin
`.meta` dosyaları da silindi.

## 9. Açık kalanlar

- **XP geri beslemesi (P7).** Kök neden duruyor: temel güç kartları çarpımsal ve sınırsız birikiyor, level maliyeti tablonun
  sonunda sabit. Bu paket yalnız sonuçlarını güvenli hâle getirdi. Seed 101'in zincirli kolunda level işleme R49'da teknik sınırda
  duruyor; run bitiyor ama sağlıklı değil.
- **Seçim ekranı yükü (P7).** Aynı seed'in zincirsiz kolunda R50 sonunda 92 334 kart seçimi var. Otomatik seçim, atlama ya da
  level sınırı eklenmedi.
- **Round sonu bekleyişi.** Sayaca sığan ama çok büyük bir level işi durdurulmaz; round sonu onu kare başına 256 level ile bekler
  (10 milyon level 60 fps'de ~11 dakika). O sırada ekranda yalnız mevcut level yazısı ilerler; ayrı bir "işleniyor" göstergesi yok.
- **Nihai sayı modeli (P10).** int hasar / can / kaynak 2 147 483 647'de doyuyor; `float` stat'lar 3,4e38'de sonsuz oluyor (hasar
  doyar, XP eklenemez); level sayacı int. Doyumdan sonra daha fazla hasar stat'ı vuruşu büyütmüyor. Bunlar geçici teknik sınırdır.
- **Zincirin toplam hasadı neden az artırdığı (P8)** ayrıştırılmadı; zincir değerleri değişmedi.
- **Zincir yayılım görseli ve insan Play testi** açık (P6b'nin görsel maddesi). Bu paketin hiçbir değişikliği insan gözüyle
  oynanarak onaylanmadı; "kırılma hissi" hakkında bir şey söylemiyor.
- **Round özeti gösterimi** (`GameFeelDirector`) kendi kırpmasını kullanıyor: çok büyük XP round özetinde 2 147 483 647 olarak
  görünür. Yalnız gösterim; değiştirilmedi.
- **Patlama görseli sayacı** (`skipExplosionVisual`) havuz dolduğunda (yalnız seed 101'in R47–R50'sinde görüldü) P6, A ve B arasında
  farklı çıkıyor (3. bölüm); nedeni doğrulanmadı. Oynanışı etkilemiyor; diğer 60 run'da A ile P6 arasında hiç fark yok.
- **Test yardımcıları** boss teklifini hâlâ yansımayla enjekte ediyor (test düzeneği; kapsam dışı bırakıldı).

## 10. Ana TODO'daki karşılığı

Bu paket ana TODO'da kendi maddesi olan bir paket değil; P7'nin ön koşulu. TODO'ya eklenenler:

| Yer | Eklenen |
|---|---|
| P6 sonrası durum paragrafı | Bölüm 3.7.6.1–2'nin özeti |
| P6b · zincir görselleri | açık kalır; not: yalnız sınıf adı değişti, yayılım görseli eklenmedi, insan Play testi açık |
| P7 | üç yeni açık madde: **XP geri beslemesi**, **yüksek level maliyet eğrisi**, **seçim ekranı yükü** (round sonu bekleyişi dahil). Mevcut "tablo sonu / taşma / sonsuz döngü" maddesine kısmi notu: teknik kısım yapıldı, "cap yok" doğrulanmadı (tersine geçici teknik sınırlar var) |
| P8 | yeni açık madde: **zincirin toplam hasadı neden az artırdığının ayrıştırılması** |
| P10 · taşma / sınır politikası | kısmi notu: geçici teknik güvenlik var; **endless için nihai büyük sayı modeli açık** |

Hiçbir madde bu paketle "tamamlandı" diye işaretlenmedi.
