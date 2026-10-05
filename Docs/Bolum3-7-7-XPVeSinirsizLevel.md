# Bölüm 3.7.7 — P7: XP, tile geliştirme ve sınırsız level

Tarih: 4 Ekim 2026. Aday profil: **Run50_XPV1** (Run50_ZincirV1'in üstüne yalnız ilerleme ayarları). Ana plan:
[TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md), P7. Önceki paketler:
[Bolum3-7-6-DavranisZinciri.md](Bolum3-7-6-DavranisZinciri.md),
[Bolum3-7-6-1-2-SayisalGuvenlikVeRefactor.md](Bolum3-7-6-1-2-SayisalGuvenlikVeRefactor.md).

Bütün ölçümler bot ölçümüdür (izole kopya, sabit kare adımı). İnsan Play testi yapılmadı; menü yükü ve "yatırım hissi"
hakkındaki hiçbir sonuç insan onayı değildir. Commit atılmadı; P8'e geçilmedi.

## Kısa sonuç

- **Kontrolsüz XP geri beslemesi kapandı.** Yeni profilde temel güç XP kartları kendi aralarında toplanıyor ve tablo bittikten
  sonra level maliyeti doğrusal büyüyor. Eski kuralda 30 779 level / 92 334 seçime giden (zincirli kolda level işlemesi teknik
  sınırda duran) seed 101 çifti yeni profilde R50'de level 169–172, 504–513 seçimle bitiyor. 82 yeni profil run'ının hiçbirinde
  teknik level durması, sayısal doyum ya da geçersiz XP yok; profilin kendi kurallarıyla en yüksek level 183, en çok 546 seçim
  (kurallardan biri kapatılan ayrıştırma kolunda 205 / 612).
- **Normal run'lar değişmedi.** Yeni kurallar yalnız temel güç XP kartı alındığında ve level 140 aşıldığında devreye giriyor.
  Seed 101 çifti eski profille R35 / R37'ye kadar satır satır aynı; eski profillerin kendisi yeni kodla eski ölçümüyle satır satır aynı (seed 101 çifti 102 / 102 satır, 30 / 30 ödül teklifi).
- **XP yatırımı R50'ye kadar hasat ya da skor olarak geri ödemiyor — bu kabul şartı tutmadı.** XP düğümlerini yazılı vadesinde
  alan kol R50'de ×1,26–1,30 level ve 58–63 fazla seçim hakkı kazanıyor, ama birikimli hasadı ×0,94–1,01, birikimli skoru
  ×0,81–0,84. XP'yi her şeyin önüne alan kol daha da geride (birikimli skor ×0,65–0,87; bir yönde 3 / 10 run boss'ta eleniyor).
  Neden ölçüldü: tile sayısını level değil grid boyutu sınırlıyor; fazla level'lar yükseltme kartına gidiyor; XP düğümlerine
  giden altın skor / ekonomi düğümlerinden çıkıyor. İzinli ayar (XP tablosu) bunu çözmüyor (9. bölüm); ayar turu kullanılmadı.
- **Erken davranış erişimi korunuyor** (XP'siz ve vadesinde kollarda kart 40 / 40 run'da R2, etkin saksı düzeni R3–R5, ilk gerçek
  tetik R6'ya kadar 38 / 40). XP'yi her şeyin önüne alan kol erişimi geciktiriyor (etkin düzen R6'ya kadar 14 / 20).
- **Seçim akışı:** kartlar aynı panelde arka arkaya sunuluyor (zaten öyleydi), panele kalan seçim sayacı ve round sonuna "Seviye
  kazanımları hesaplanıyor" durumu eklendi. **Menü yükü çözülmedi, ölçüldü:** XP'siz kollarda medyan 222–234 seçim, XP
  yatırımıyla 285–374, güçlü build'de 504–513; kart başına 4 sn varsayımıyla 15–34 dakika (aktif oyun 37,5 dakika).
- **Açık:** XP yatırımının değeri (karar gerekiyor, 14. bölüm), menü yükü ve insan Play testi, nihai sayı modeli (P10), endless akışı.

## 1. Yeni profili nasıl oynarım?

1. Unity menüsü: **Tools > Run Profili > Run50 XP V1 · 50 round (XP ve sınırsız level adayı)**.
2. Play. Run, Zincir V1 ile aynı takvim, hedef, ödül havuzu ve ekonomiyle başlar.
3. Yeni kuralları görmek için temel güç evresine girmek gerekir: açık bütün hücreler tile'lanıp hepsi Sv 3 olunca kart ekranı
   "TÜM TILE'LAR MAX · kart kalıcı Temel güç verir" şeridini gösterir. "XP kazancı" kartında iki satır vardır:
   `XP kazancı +3%` / `Toplanır: +12% → +15%`. Küçük grid'de (grid genişletilmezse) bu evre R7–R9'da başlar; normal oyunda R35'ten sonra.
4. Her kart ekranının altında kalan seçim hakkı yazar ("KALAN SEÇİM: 5" … "SON SEÇİM").
5. Level 140'tan sonra bir sonraki level'ın maliyeti her level'da 7 500 XP artar (150 000 → 157 500 → 165 000 …).

Eski profile dönmek için aynı menüden "Run50 Zincir V1" seçilir. Bu paket seçili profili değiştirmedi.

## 2. Birikim kuralı ve maliyet formülü

**XP kartlarının birikimi (yalnız yeni profil).** Tile ve yükseltmeler tükendikten sonra sunulan "XP kazancı" temel güç kartları
bir **birikim grubudur**: gruba giren kartların değerleri toplanır ve gruptan tek bir çarpan çıkar.

| | Eski profiller | Run50_XPV1 |
|---|---|---|
| İki +%5 XP temel güç kartı | ×1,05 × 1,05 = ×1,1025 | ×(1 + 0,05 + 0,05) = ×1,10 |
| 28 kart, toplam +%117 (seed 101 zincirsiz kol, run sonu) | çarpımsal (≈ ×3,1 olurdu) | ×2,17 |
| Kart değerleri | +%2 / +%3 / +%4 / +%5 (Common / Rare / Epic / Legendary) | aynı |
| Diğer temel güç kartları (hasar, atak aralığı, üretim, kaynak) | ayrı çarpan | ayrı çarpan (değişmedi) |
| Diğer XP kaynakları (ağaç, Water tile, rezonans, çiftçi, Bilgi Filizi) | kendi işlemleri | aynı; grup çarpanı onlarla eskisi gibi çarpılır |

Grup, stat ve hedefiyle tanımlıdır (XP kazancı, saksı) ve global modifier listesinde **tek** bir "MorePercent" satırı olarak
durur; değeri kartların toplamıdır (`StatManager.AddToSummedGroup`). Yeni bir stat ya da kart sistemi kurulmadı. Kart, uygulamanın
yaptığını yazar: kartın değeri ve grubun toplamının nasıl değişeceği. Yeni sahnede (yeni run) ve run sonu ekranından ana menüye
dönüşte grup sıfırlanır.

**Tablo sonrası maliyet (yalnız yeni profil).** İlk 140 seviyenin maliyeti tablodan gelir ve eskisiyle aynıdır. Sonrası:

    C(L) = C(N) × [1 + s × (L − N)],  L > N        N = 140,  C(N) = 150 000 XP,  s = 0,05

| Level | 140 | 141 | 150 | 172 | 200 | 240 | 1 000 | 10 000 |
|---|---|---|---|---|---|---|---|---|
| Sonraki level'ın maliyeti (XP) | 150 000 | 157 500 | 225 000 | 390 000 | 600 000 | 900 000 | 6 600 000 | 74 100 000 |

Eski profillerde `s = 0`: son maliyet (150 000) tekrar eder, davranış değişmedi.

**"Cap yok" ne demek.** Test edilen aralıkta tasarımsal bir level sınırı yoktur: maliyet her level'da büyür, level 2 147 483 647'ye
kadar sonlu ve artan bir sayıdır (testte örneklendi), 6 milyar XP ile level 1 384'e ve 40 000 level'lık tek bir işle level
41 000'e kadar işleme bağımsız hesapla birebir tutar. Bu, sayı türlerinin sınırı olmadığı anlamına gelmez (12. bölüm): level
sayacı `int`, XP `double`'dır. Nihai büyük sayı modeli P10'da açık.

## 3. Ayarlar hangi dosyada?

| Ne | Nerede | Değer |
|---|---|---|
| Kuyruk katsayısı `s` | `Tools/Balance/XPV1/xp_v1_params.py` · `TAIL_GROWTH` → `Progression_XPV1.asset` · `tailGrowth` | 0,05 |
| XP kartlarının toplanması | aynı dosya · `ADDITIVE_BASE_XP_CARDS` → `RunBalance_XPV1.asset` · `additiveBaseXpCards` | açık |
| İşlevsiz "Atak aralığı" kartının süzülmesi | aynı dosya · `HIDE_FLOORED_BASE_STATS` → `hideFlooredBaseStats` | açık |
| Tablonun kendi içinde değişiklik | aynı dosya · `TABLE_OVERRIDES` | yok |
| Kart değerleri (+%2 … +%5) | `CardSelectionUI.RollBaseStat` (kod, bütün profiller) | değişmedi |
| Kare başına işlenen level | `ProgressionManager.LevelsPerFrame` (kod) | 256 (değişmedi) |

Üretici: `python Tools/Balance/XPV1/make_xp_v1.py` (kontrol: `--check`). Yazdığı üç asset:
`Assets/ScriptableObjects/Balance/XPV1/Progression_XPV1.asset`, `RunBalance_XPV1.asset` ve
`Assets/ScriptableObjects/RunProfiles/Run50_XPV1.asset`. Üçü de mevcut asset'lerin metin kopyasıdır; fark yalnız şudur:

| Asset | Kopyalandığı | Fark |
|---|---|---|
| Run50_XPV1 | Run50_ZincirV1 | ad, zafer başlığı, denge seti |
| RunBalance_XPV1 | RunBalance_KirilmaV1 | XP tablosu, iki temel güç kartı kuralı |
| Progression_XPV1 | Progression_DengeV1 | `tailGrowth: 0.05` |

Takvim, kota ve boss hedefleri, bitki canı, round süresi, zincir kuralları, ödül havuzu ve ağırlıkları, level başına 3 seçim,
bedelli ödüllerin seçim hakkı değişimi aynen gelir (testte alan alan karşılaştırıldı). Ayrıntı:
[Tools/Balance/XPV1/README.md](../Tools/Balance/XPV1/README.md).

Kod değişiklikleri:

| Dosya | Değişiklik |
|---|---|
| `ProgressionSO` | `tailGrowth`, tablo sonrası formül, doğrulama, bekleyen level sayısının kapalı hesabı |
| `ProgressionManager` | sayaca sığmayan iş denetimi büyüyen kuyrukta da çalışır (kapalı hesap); davranış eski profillerde aynı |
| `RunBalanceSO` | iki yeni alan (varsayılan kapalı) |
| `StatManager` | birikim grubu (`AddToSummedGroup`, `SummedGroupTotal`); liste temizlenince grup da temizlenir |
| `CardSelectionUI` | XP kartı gruba eklenir; tabandaki atak aralığı kartı süzülür (yalnız profil isterse); kalan seçim sayacı |
| `CardUI`, `TileCardOffer` | toplanan kartın metni |
| `LevelWorkStatusUI` (yeni), `UIManager` | "Seviye kazanımları hesaplanıyor" durumu |
| `PlayerController` | saldırı aralığı alt sınırı adlandırıldı (`MinAttackInterval = 0,1`; değer aynı) |
| `RunProfileMenu` | menü satırı |

## 4. Seçim akışı ve menü: ne vardı, ne eklendi

| İstenen | Durum |
|---|---|
| Aynı round sonunun kart hakları mevcut panelde arka arkaya | **Zaten böyleydi; doğrulandı.** Panel seçimler arasında kapanıp açılmıyor (testte panelin açılma / kapanma sayısı izlendi) |
| Panelde kalan seçim hakkı | **Eklendi.** Atlama düğmesinin altında "KALAN SEÇİM: N" (ekrandaki dahil), son seçimde "SON SEÇİM". Bütün profillerde görünür. 82 ölçüm run'ının her kart ekranında sayaç gerçek bekleyen hakla karşılaştırıldı: hepsi aynı |
| Kart seçimi bitmeden boss ödülü / sonraki round yok | **Zaten böyleydi; doğrulandı** (boss round'unda 2 bekleyen kart + bekleyen ödül: ödül ekranı son karttan sonra açılıyor, sonraki round başlatılamıyor) |
| Bekleyen level işi varken "Seviye kazanımları hesaplanıyor" | **Eklendi.** Round sonu level işini beklerken ortada durum yazısı ve o anki level; iş bitince kaybolur, kart ekranı açılır |
| Otomatik seçim, hakları paketleme, sessiz atlama yok | Eklenmedi |
| Bedelli ödülden önce kazanılmış haklar değişmez | **Zaten böyleydi; doğrulandı** (3 seçimle kazanılan 6 hak, +1 ödülünden sonra 6; sonraki level 4 verir) |

Görüntüler (batch, izole kopya): [T377_01_SeviyeHesaplaniyor.png](Bolum3-7-7/testler/T377_01_SeviyeHesaplaniyor.png),
[T377_02_KartPaneli_KalanSecim_ToplananXP.png](Bolum3-7-7/testler/T377_02_KartPaneli_KalanSecim_ToplananXP.png).

Ölçülen bütün yeni profil run'larında (82) level işleme bekleyişi 0 karedir: "hesaplanıyor" yazısı normal oyunda görünmez;
yalnız bir karede 256'dan fazla level kazanılırsa görünür (testte 40 000 level'lık işle gösterildi).

## 5. Eski / yeni karşılaştırma

**Seed 101 kabul çifti** (politika ve seed P6 ve 3.7.6.1 ile aynı: "Davranış-rezonans"; eski = Run50_ZincirV1, 3.7.6.2 sonuçları).
Tam tablo: [Seed101_EskiYeni.md](Bolum3-7-7/tablolar/Seed101_EskiYeni.md).

| | Zincirli · eski | Zincirli · yeni | Zincirsiz · eski | Zincirsiz · yeni |
|---|---|---|---|---|
| İlk farklı round | — | R38 | — | R36 |
| Sonuç | KAZANDI (level işleme R49'da durmuş) | KAZANDI | KAZANDI | KAZANDI |
| R50 level | 5 027 (durdu) | 172 | 30 779 | 169 |
| Seçim hakkı | 15 078 | 513 | 92 334 | 504 |
| Bir round sonunda en çok seçim | 12 288 | 27 | 91 086 | 24 |
| Temel güç kartı (XP kartı) | 14 712 | 147 (23) | 892 | 149 (28) |
| R49 round XP'si | 2,36e16 | 1,48 milyon | 25,9 milyon | 1,33 milyon |
| R50 hasat | 9 818 | 4 818 | 9 926 | 4 682 |
| R50 hasar stat'ı | +∞ (doydu) | 508 | 674 059 | 587 |
| Doyan dönüşüm / geçersiz XP | 647 014 / 9 818 | 0 / 0 | 0 / 0 | 0 / 0 |
| Level işleme durdu | evet | hayır | hayır | hayır |

İki profil, ilk XP temel güç kartları birikene kadar (R35'te temel güç evresi başlar; fark R36 / R38'de ilk kez XP sütununda
görünür) **100 ortak sütunda satır satır aynıdır**. Yeni profilde run hâlâ çok güçlüdür (R50 boss hedefi 1 100, round skoru
130 000–150 000): bu P8'in konusudur.

**İki kuralın ayrı etkisi** (seed 101 zincirsiz kol, aynı profil üstünde kural kapatılarak):

| Kural | R50 level | Seçim hakkı | Bir round sonunda en çok | R45 → R50 level artışı |
|---|---|---|---|---|
| İkisi de yok (eski) | 30 779 | 92 334 | 91 086 | 165 → 30 779 |
| Yalnız toplanan XP kartı (maliyet sabit) | 205 | 612 | 54 (R50) | 151 → 205 |
| Yalnız büyüyen maliyet (kartlar çarpımsal) | 195 | 582 | 33 (R50) | 158 → 195 |
| İkisi birden (Run50_XPV1) | 169 | 504 | 24 (R42) | 149 → 169 |

Her kural tek başına R50'ye kadar patlamayı durduruyor; tek başına bırakılan kolda büyüme run sonuna doğru hâlâ hızlanıyor (en
kalabalık round sonu R50'de). İkisi birlikteyken en kalabalık round sonu R42'de kalıyor.

**Normal run'lar.** Level 140'ı aşmayan ve XP temel güç kartı almayan bir run'da iki profil arasında fark yoktur (yukarıdaki
satır satır eşitlik bunun ölçümüdür). 82 yeni profil run'ının 13'ü level 140'ı aştı, 33'ü en az bir XP temel güç kartı aldı.

**Eski profillerin kendisi.** Eski kuralın en çok zorlandığı run'lar (Run50_ZincirV1, seed 101 "Davranış-rezonans" çifti: binlerce
temel güç kartı, sabit kuyruk, teknik durma) bu paketin son koduyla yeniden çalıştırıldı ve 3.7.6.2'nin sonucuyla karşılaştırıldı:
**102 / 102 satır, 101 sütun aynı; 30 / 30 ödül teklifi aynı** (dosya md5'i de aynı). Yalnız görsel sayaç (atlanan patlama görseli)
6 satırda farklı; 3.7.6.2'de de böyleydi, oynanış değildir. Eski profillerin teklif dağılımı ve bedelli ödül ölçümü md5 olarak aynı
(`222d6374…` / `6d628d3f…`). 26 eski test aynı kontrol sayılarıyla geçiyor (13. bölüm). İşlev testinde ayrıca: 14 eski profilin
hiçbirinde yeni kurallar açık değil; Run50_ZincirV1'de tablo sonrası maliyet 150 000'de sabit, iki XP temel güç kartı hâlâ çarpımsal
(×1,0404), tabandaki "Atak aralığı" kartı hâlâ sunuluyor (eski zar akışı). 60 run'lık birikimli set (`k3761`) bu pakette **yeniden
çalıştırılmadı**; seed 101 çifti aynı kod yollarını (R1–R34 normal oyun + temel güç evresi) kapsadığı için yeterli görüldü.

Eski profillerde görünen tek değişiklik arayüzdedir: kart panelindeki kalan seçim sayacı ve (bekleyen level işi olursa) "hesaplanıyor"
yazısı bütün profillerde gösterilir. Oynanışı ve zar akışını etkilemez (yukarıdaki satır satır eşitlik).

## 6. Kuyruk katsayısı pilotu

Katsayı yalnız level 140'ı aşan run'ı etkiler. Üç katsayı, aynı seed'ler (tam tablo: [Pilot.md](Bolum3-7-7/tablolar/Pilot.md)):

| Run | s = 0,025 | s = 0,05 | s = 0,10 |
|---|---|---|---|
| Seed 101 zincirsiz — R50 level / seçim / toplam skor | 179 / 534 / 1 362 576 | 169 / 504 / 1 359 660 | 162 / 483 / 1 324 315 |
| Seed 110 zincirli — R50 level | 141 | 141 | 141 |
| XP stresi (ilk tanım) seed 100 / 101 | level 75 / R23'te kotada elendi (üç katsayıda aynı: level 140'a ulaşmıyor) | aynı | aynı |

Üç katsayıda da teknik durma, doyum ve on binlerce seçim yok; seed 101'de fark küçük (17 level, toplam skor < %3). Değiştirmek
için ölçülmüş bir neden olmadığından **ilk aday s = 0,05 seçildi**. Katsayının XP yatırımının geri ödemesine etkisi yok: A/B
kollarının (60 run) yalnız 3'ü level 140'ı aşıyor.

## 7. Erken davranışın üç erişim zamanı

Ölçülen üç zaman: (1) ilk patlama / elektrik kartının alındığı round, (2) davranış şansı olan bir saksıyla başlanan ilk round
(kartın tile'ı bir saksının altında), (3) ilk gerçek tetik. Bot politikaları davranış teklifini **kabul eder** (ilk patlama /
elektrik kartını teklif edildiği anda alır). Gerçek başlangıç bütçesi, gerçek satın alma, rastgele tile yerleşimi; bedava saksı
ya da kart yok. Tam tablo: [Erisim_AB.md](Bolum3-7-7/tablolar/Erisim_AB.md).

| Yön · kol (10 run) | Kart ≤ R6 | Etkin saksı düzeni ≤ R6 | İlk gerçek tetik ≤ R6 | Kart | Etkin düzen | İlk tetik |
|---|---|---|---|---|---|---|
| Hasar-alan · XP'siz | 10 / 10 | 10 / 10 | 9 / 10 | R2 | R3–R5 | R3–R7 |
| Hasar-alan · vadesinde XP | 10 / 10 | 10 / 10 | 9 / 10 | R2 | R3–R5 | R3–R9 |
| Hasar-alan · erken XP | 10 / 10 | 6 / 10 | 6 / 10 | R2 | R3–R9 | R3–R10 |
| Davranış yönü · XP'siz | 10 / 10 | 10 / 10 | 10 / 10 | R2 | R3 | R3–R5 |
| Davranış yönü · vadesinde XP | 10 / 10 | 10 / 10 | 10 / 10 | R2 | R3 | R3–R5 |
| Davranış yönü · erken XP | 10 / 10 | 8 / 10 | 7 / 10 | R2 | R3–R7 | R3–R8 |

- **3.6'daki anlamıyla hedef (erken davranışı deneyen açılışların en az 8 / 10'u ilk kartı R6 sonuna kadar alsın): karşılandı**,
  60 / 60 (hepsi R2). Seed 101 çifti ve stres run'larında da kart R2, etkin düzen R3, tetik R3–R5.
- Kartı R2'de almak davranışı R2'de kullanmak değildir: etkin düzen en erken R3'te, ilk tetik en erken R3'te oluşuyor.
- **Başarısız örnekler ve nedenleri (R6'ya kadar gerçek tetiği olmayan 9 run):** 2'si **tetik şansı** (etkin düzen R3 / R6'da
  var, şans R7 / R8'e kadar tutmadı; biri XP'siz, biri erken XP kolunda); 1'i vadesinde kolunda yine tetik şansı (R9); 6'sı
  **satın alma** — hepsi erken XP kolunda: XP düğümleri öne alındığı için R6'ya kadar 2 saksıda kalınıyor, kartın düştüğü hücrede
  saksı yok (R5 medyanı: 2 saksı ↔ XP'siz kolda 6). XP ya da teklif yüzünden gecikme yok: kart her run'da R2'de alındı.
- Sonuç: erken davranış erişimi XP'ye dokunmadan korunuyor; yalnız XP'yi saksının önüne alan açılış erişimi geciktiriyor.

## 8. Tile → yükseltme → temel güç geçişi

**İşlev (kontrollü test):** açık boş hücre varken üç aday yeni tile kartıdır (kilitli türler 120 teklifte hiç çıkmaz; ilk
davranış kartı alınana kadar bir slot patlama / elektrik); grid dolunca adaylar yükseltmedir (saksı altındaki tile önce), şerit
"GRİD DOLU · kart, seçtiğin tile'ı seviye atlatır"; bütün tile'lar Sv 3 olunca adaylar temel güç kartıdır, şerit "TÜM TILE'LAR
MAX · kart kalıcı Temel güç verir". Yeni run'da statlar, grup ve bekleyen haklar sıfırlanır.

**Gerçek ekonomiyle (60 A/B run'ı, hediye yok):**

| | İlk yükseltme kartı | Temel güç evresine giren run | İlk temel güç kartı |
|---|---|---|---|
| XP'siz (20 run) | R4–R5 (20 / 20) | 0 / 20 | — |
| Vadesinde XP (20 run) | R4–R5 (20 / 20) | 4 / 20 | R35–R41 |
| Erken XP (20 run) | R4–R5 (20 / 20) | 18 / 20 | 14'ünde R7–R9 (3×3 grid dolup Sv 3 olunca), 4'ünde R20–R41 |

82 yeni profil run'ının 39'u temel güç evresine girdi. Yani geçiş normal ekonomiyle ulaşılabilir, ama XP yatırımı olmadan R50'ye
kadar ulaşılmıyor.

**İşlevsiz seçenekler.** Kilitli tile kartı sunulmuyor; yükseltme yalnız yükseltilebilir tile'a sunuluyor; üretim tabanındaki
saksıda Fertile kartı "nadirlik" yazıyor (hepsi mevcut filtreler, doğrulandı). Bir eksik bulundu ve yalnız yeni profilde
kapatıldı: saldırı aralığı oyunun alt sınırına (0,1 sn) indiğinde "Atak aralığı" temel güç kartı hâlâ sunuluyordu ve hiçbir şey
değiştirmiyordu. Eski profillerde eski davranış bilerek korundu (zar akışı değişmesin). Ölçülen run'larda bu sınıra inilmedi
(en düşük 0,16 sn); süzgeç işlev testinde doğrulandı.

**Gözlem (karar değil):** küçük grid'de temel güç evresi çok erken başlıyor (R7–R9). Temel güç kartları global ve kalıcı, tile'lar
yereldir; grid'i genişletmemek bu kartlara erken ulaştırıyor. Ölçülen run'larda bu bir avantaja dönüşmedi (o kol geride), ama
kural olarak not edildi.

## 9. XP yatırımının geri ödeme sonucu

**Kollar** (ölçümden önce yazıldı: [OlcumOncesi-PolitikaTanimlari.md](Bolum3-7-7/OlcumOncesi-PolitikaTanimlari.md)). Aynı seed,
aynı başlangıç bütçesi, bedava kart ya da kaynak yok. İki devam yönü: **Hasar-alan** ve **Davranış yönü**; her kol 10 seed.

| Kol | XP yatırımı |
|---|---|
| A · XP'siz | Hiç XP düğümü almaz (o altın yönün diğer düğümlerine ve saksıya gider); Water kartını, Bilgi Filizi'ni, XP temel güç kartını yalnız başka seçenek yoksa alır |
| B · erken XP (önceden tanımlı) | Hasat Deneyimi I'i alabildiği anda, **diğer bütün düğümlerden önce** alır; R20'ye kadar Water kartını öne alır ve Bilgi Filizi'ni alır; sonra yön politikası |
| C · vadesinde XP (B görüldükten sonra, ölçümünden önce tanımlandı) | XP düğümlerini ağaçta yazılı vadelerinde alır (I: R3–16, II: R25–42), öne almaz; Water ve Bilgi Filizi yönün tercihiyle |

C neden eklendi: B, XP'yi saksı ve grid'in önüne aldığı için (R5'te 2 saksı ↔ 6; R10'da 9 tile ↔ 22) XP yatırımını değil
sıralamayı ölçüyordu. B'nin sonuçları değiştirilmeden raporlanıyor. Tam tablolar:
[AB_vadesindeXP.md](Bolum3-7-7/tablolar/AB_vadesindeXP.md), [AB_erkenXP.md](Bolum3-7-7/tablolar/AB_erkenXP.md).

**C · vadesinde XP ↔ A · XP'siz** (medyan A → C; parantezde seed başına oranın medyanı ve C'nin önde olduğu seed sayısı):

| Hasar-alan | R10 | R20 | R30 | R40 | R50 |
|---|---|---|---|---|---|
| Level | 11 → 13 (×1,09) | 27 → 31 (×1,15) | 45 → 53 (×1,23) | 61 → 76 (×1,28) | 75 → 96 (×1,30; 10 / 10) |
| Toplam XP | 5 500 → 6 594 | 28 432 → 38 808 | 106 865 → 180 801 | 288 405 → 625 906 | 577 145 → 1 399 941 (×2,60) |
| Seçim hakkı (birikimli) | 30 → 36 | 78 → 90 | 132 → 156 | 180 → 226 | 222 → 285 |
| Kart: tile · yükseltme · temel güç | 22·6·0 → 25·7·0 | 49·26·0 → 49·32·0 | 81·46·0 → 81·68·0 | 81·94·0 → 81·138·0 | 116·104·0 → 121·160·0 |
| XP'ye harcanan altın | 0 → 14 | 0 → 125 | 0 → 350 | 0 → 4 855 | 0 → 9 055 |
| Hasat (o round) | ×0,93 (1 / 10) | ×0,93 (3 / 10) | ×0,96 (3 / 10) | ×1,00 (5 / 10) | ×1,02 (6 / 10) |
| Skor (o round) | ×1,00 | ×0,98 | ×0,80 | ×0,75 | ×0,84 (4 / 10) |
| Hasat (birikimli) | ×0,98 | ×0,94 | ×0,93 | ×0,97 | ×1,01 (6 / 10) |
| Skor (birikimli) | ×1,04 | ×0,88 | ×0,80 | ×0,72 | ×0,81 (3 / 10) |
| Dönem skoru ÷ kota | ×3,7 → ×3,5 | ×24,6 → ×19,6 | ×69,7 → ×51,3 | ×38,3 → ×31,1 | ×30,1 → ×30,2 |
| Boss skoru ÷ boss hedefi | ×4,9 → ×4,9 | ×27,1 → ×27,5 | ×79,7 → ×56,3 | ×40,0 → ×31,7 | ×33,2 → ×31,9 |

| Davranış yönü | R10 | R20 | R30 | R40 | R50 |
|---|---|---|---|---|---|
| Level | 11 → 12 (×1,09) | 28 → 32 (×1,16) | 46 → 55 (×1,16) | 64 → 78 (×1,19) | 79 → 98 (×1,26; 10 / 10) |
| Toplam XP | 5 320 → 5 948 | 30 460 → 42 494 | 124 050 → 208 328 | 329 210 → 650 601 | 686 950 → 1 539 094 (×2,28) |
| Seçim hakkı (birikimli) | 30 → 33 | 82 → 94 | 136 → 162 | 188 → 230 | 234 → 292 |
| Kart: tile · yükseltme · temel güç | 24·5·0 → 24·6·0 | 49·29·0 → 49·41·0 | 81·51·0 → 81·76·0 | 81·102·0 → 81·142·0 | 117·112·0 → 121·166·0 |
| XP'ye harcanan altın | 0 → 14 | 0 → 125 | 0 → 1 165 | 0 → 6 855 | 0 → 9 055 |
| Hasat (o round) | ×0,90 | ×0,94 | ×0,89 | ×0,90 | ×0,98 (5 / 10) |
| Skor (o round) | ×0,99 | ×0,79 | ×0,71 | ×0,77 | ×0,98 (5 / 10) |
| Hasat (birikimli) | ×0,98 | ×0,89 | ×0,91 | ×0,90 | ×0,94 (3 / 10) |
| Skor (birikimli) | ×0,96 | ×0,77 | ×0,80 | ×0,80 | ×0,84 (4 / 10) |
| Dönem skoru ÷ kota | ×3,5 → ×3,3 | ×22,1 → ×12,5 | ×82,6 → ×68,0 | ×30,0 → ×30,2 | ×27,2 → ×25,7 |
| Boss skoru ÷ boss hedefi | ×4,4 → ×4,2 | ×27,5 → ×19,5 | ×81,3 → ×67,3 | ×34,6 → ×33,1 | ×27,2 → ×27,8 |

İki yönde de 10 / 10 run kazanıyor (iki kolda da).

**B · erken XP ↔ A · XP'siz** (özet):

| | Hasar-alan | Davranış yönü |
|---|---|---|
| Sonuç | 10 / 10 kazandı | **7 / 10** (3 run boss'ta elendi: R10, R23, R23) |
| Level R10 / R30 / R50 | ×1,17 / ×1,34 / ×1,52 (126 ↔ 75) | ×1,17 / ×1,06 / ×1,67 (132 ↔ 79) |
| Hasat (o round) R10 / R20 / R30 / R40 / R50 | ×0,55 / ×0,43 / ×0,57 / ×0,84 / ×1,11 | ×0,56 / ×0,23 / ×0,40 / ×0,69 / ×1,00 |
| Skor (birikimli) R20 / R40 / R50 | ×0,29 / ×0,65 / ×0,87 | ×0,26 / ×0,40 / ×0,65 |
| XP'ye harcanan altın R10 / R50 | 125 / 9 055 | 125 / 9 055 |

**Geri ödeme:**

| Ölçü | C · Hasar-alan | C · Davranış yönü | B · Hasar-alan | B · Davranış yönü |
|---|---|---|---|---|
| Uygulanmış geliştirme farkı (R50) | +21 level, +63 seçim hakkı, +56 yükseltme kartı | +19 level, +58 seçim, +54 yükseltme | +51 level, +152 seçim, +113 yükseltme, 19 temel güç kartı | +53 level, +159 seçim, +123 yükseltme, 20 temel güç kartı |
| Anlık üstünlük (o round'un hasadı, üst üste 3 round ≥ ×1) | **yok**: R35–R50 arası ×1,00–1,04 çevresinde, son kez R48'de ×1'in altında | **yok** (R50'de ×0,98) | R44 (R45: ×1,08, 7 / 10 seed) | yok (R50'de ×1,00) |
| Anlık üstünlük (o round'un skoru) | yok (R50: ×0,84) | yok (R50: ×0,98) | R45 (×1,06) | yok (×0,93) |
| Birikimli hasat açığının kapanması | R44–R45'te ×1,01 (6 / 10 seed); fark gürültü içinde | **kapanmadı** (×0,94) | **kapanmadı** (×0,83) | **kapanmadı** (×0,68) |
| Birikimli skor açığının kapanması | **kapanmadı** (×0,81) | **kapanmadı** (×0,84) | **kapanmadı** (×0,87) | **kapanmadı** (×0,65) |

**Sonuç: XP yatırımı R50'ye kadar geri ödemiyor.** Ölçülebilir olan fayda ilerlemededir (level ve seçim hakkı R10'dan itibaren
önde, R50'de ×1,26–1,67); hasat en iyi durumda başa baş, skor geride. "Daha fazla level oldu" bir geri ödeme değildir; bu sonuç
başarısız olarak yazıldı.

**Neden (ölçümden):**

1. **Tile sayısını level değil grid boyutu sınırlıyor.** A ve C kollarında tile sayısı R20'de 49, R30 ve R40'ta 81'dir — ikisinde
   de aynı. İlk yükseltme kartı 60 run'ın hepsinde R4–R5'te geliyor: o noktadan sonra seçim hakkı zaten fazladır. XP'nin getirdiği
   ek level'lar yeni tile değil **yükseltme kartı** olur (C: +54–56 yükseltme). Bir yükseltme seviyesi, bir tile'ın etkisini %25
   artırır; hasada katkısı küçüktür.
2. **XP'ye giden altın başka düğümlerden çıkıyor.** C kolu R50'ye kadar 9 055 altını XP düğümlerine harcıyor (çoğu R30–R45'te,
   Hasat Deneyimi II). XP'siz kol aynı altınla skor ve ekonomi düğümleri alıyor; C'nin hasadı aynıyken skoru bu yüzden %16–19 düşük.
3. **Temel güç kartları (oyuncunun stat yönünü seçtiği tek kart türü) ancak bütün tile'lar Sv 3 olunca geliyor.** C kolunda 20
   run'ın 4'ünde ve R35'ten sonra. Yani XP'nin kazandırdığı seçimler R50'den önce çoğunlukla "hangi tile yükselsin" seçimidir,
   "hangi stat" değil.
4. **B kolunda ek olarak erken güç kaybı:** XP ailesi bitene kadar saksı ve grid gecikiyor (R10 hasadı ×0,55), run bunu R44'e
   kadar geri alamıyor; boss ödülü teklifleri de ayrışıyor (aşağıda).

**Boss ödüllerinin ayrışması** (bütün fark XP'ye mal edilemez): A ↔ C'de 150 boss teklifinin 142'sinde (Hasar-alan) ve 133'ünde
(Davranış yönü) adaylar aynı, aynı adaylarda seçim hiç farklı değil; 8 ve 17 teklifte adaylar farklı (uygunluk koşulları).
A ↔ B'de ayrışma büyük: Hasar-alan'da 150 teklifin 63'ünde adaylar farklı, 15'inde aynı adaylardan farklı ödül seçildi (B erken
round'larda Bilgi Filizi alıyor); Davranış yönü'nde 119 ortak teklifin 57'si farklı, 31 boss round'u yalnız A'da oynandı (elenen
run'lar). B'nin açığının bir kısmı ödül farkıdır.

**Ayar turu kararı: kullanılmadı.** İzinli ayar XP tablosudur. Ölçülen toplam XP'yi daha yatık tablo varyantlarına uygulayan
hesap (XP geliri sabit tutularak; `Progression_XPV1` maliyetleri, level 20 ya da 30'dan sonra eğimi yarıya / çeyreğe indirilmiş):

| Tablo | A R50 level | C R50 level | C ÷ A | B ÷ A |
|---|---|---|---|---|
| Mevcut | 75 | 96 | ×1,28 | ×1,68 |
| Level 20'den sonra eğim × 0,5 | 89 | 115 | ×1,29 | ×1,67 |
| Level 20'den sonra eğim × 0,25 | 105 | 136 | ×1,30 | ×1,64 |
| Level 30'dan sonra eğim × 0,5 | 86 | 113 | ×1,31 | ×1,72 |

Tabloyu yatırmak XP yatırımcısının oranını değiştirmiyor; yalnız herkesin level'ını ve seçim sayısını %15–40 artırıyor (menü
yükü). Sorun level sayısında değil, fazla level'ın değerinde ve XP düğümünün fırsat maliyetinde; ikisi de P7'nin ilerleme
ayarları dışında (tile yükseltme değeri, grid açılma zamanı, ağaç fiyatları, temel güç kartının ne zaman sunulduğu). Bu yüzden
tablo körlemesine değiştirilmedi, başka sisteme de dokunulmadı. Hesap bir kestirimdir (yeni run değil): XP gelirinin
değişmediğini varsayar.

## 10. Toplam seçim ve menü yükü

Süreler **insan ölçümü değildir**: alınan kart sayısı × varsayılan saniye. Bot menüde zaman harcamaz. Karşılaştırma için: 50
round × 45 sn = 37,5 dakika aktif oyun. Tam tablolar: [MenuYuku_AB.md](Bolum3-7-7/tablolar/MenuYuku_AB.md),
[MenuYuku_Seed101_Stres.md](Bolum3-7-7/tablolar/MenuYuku_Seed101_Stres.md).

| Kol (run) | Level medyan / en yüksek | Seçim medyan / en yüksek | Bir round sonunda en çok (medyan / en yüksek) | 2 sn / kart | 4 sn / kart | 6 sn / kart |
|---|---|---|---|---|---|---|
| Hasar-alan · XP'siz (10) | 75 / 85 | 222 / 252 | 8 / 9 | 7,4 dk | 14,8 dk | 22,2 dk |
| Davranış yönü · XP'siz (10) | 79 / 86 | 234 / 255 | 9 / 9 | 7,8 dk | 15,6 dk | 23,4 dk |
| Hasar-alan · vadesinde XP (10) | 96 / 126 | 285 / 375 | 9 / 15 | 9,5 dk | 19,0 dk | 28,5 dk |
| Davranış yönü · vadesinde XP (10) | 98 / 153 | 292 / 456 | 9 / 18 | 9,8 dk | 19,5 dk | 29,2 dk |
| Hasar-alan · erken XP (10) | 126 / 140 | 374 / 417 | 15 / 18 | 12,4 dk | 24,9 dk | 37,4 dk |
| Davranış yönü · erken XP (10) | 108 / 183 | 320 / 546 | 14 / 27 | 10,7 dk | 21,3 dk | 31,9 dk |
| Seed 101 çifti (2) | 171 / 172 | 509 / 513 | 26 / 27 | 17,0 dk | 33,9 dk | 50,9 dk |
| Temel güç XP stresi (6) | 97 / 119 | 338 / 426 | 14 / 16 | 11,3 dk | 22,5 dk | 33,8 dk |

Süre sütunları medyandır. Level işleme bekleyişi bütün run'larda 0 karedir.

- On binlerce seçim yok (en çok 612, o da kural ayrıştırma kolunda; profilin kendisiyle en çok 546).
- 200–270 seçim referansı XP'siz kollarda geçerli (204–255). XP yatırımı seçim sayısını %25–70 artırıyor.
- 4 sn / kart varsayımıyla menü süresi aktif oyunun %40–90'ı. **Bu çözülmüş bir sorun değildir**; hedef insan testiyle
  belirlenecek. Körlemesine bir level sayısı dayatılmadı, otomatik seçim eklenmedi.

## 11. Seed 101 ve stres sonuçları

Seed 101: 5. bölüm. Yeni profilde iki kol da R50'yi bitiriyor; teknik durma, doyum ve geçersiz XP yok; 504–513 seçim.

**Stres.** Tam tablo: [Seed101_Stres_Ayristirma.md](Bolum3-7-7/tablolar/Seed101_Stres_Ayristirma.md).

| Politika | Kural | Seed | Sonuç | R50 level | Seçim | Temel güç (XP) kartı | XP kart toplamı | Durma / doyum |
|---|---|---|---|---|---|---|---|---|
| XP stresi (ilk tanım: ağaç dahil her şeyde XP önce) | yeni | 100 | KAZANDI | 75 | 259 | 5 (2) | +%5 | yok |
| | yeni | 101 | **KOTA @R23** | 29 | 84 | 0 | — | yok |
| Temel güç XP stresi (ikinci tanım) | yeni | 100–104, 110 | 6 / 6 KAZANDI | 75–119 | 261–426 | 0–71 (0–37) | en çok +%140 | yok |
| | eski | 101 | KAZANDI | 106 | 379 | 6 (4) | çarpımsal | yok |
| | eski | 110 | KAZANDI | 128 | 462 | 100 (52) | çarpımsal | yok |
| | yeni | 110 | KAZANDI | 119 | 426 | 71 (37) | +%140 | yok |

- **İlk stres tanımı kendi kendini zayıflattı:** XP'yi ağaçta da her şeyin önüne alan bot güçsüz kaldı (R50 level 75; bir run
  kotada elendi) ve temel güç evresine neredeyse hiç girmedi. "Sürekli XP seçmek" bu oyunda geri beslemeye değil zayıf bir run'a
  götürüyor. Bu yüzden ikinci tanım eklendi (tanım ölçümünden önce yazıldı): temel güç evresine ulaşan build + teklif edilen her
  XP kartı + Bilgi Filizi + Bereketli Öğrenim (level başına 4 seçim).
- İkinci tanımda yeni kuralla en büyük XP kart toplamı +%140 (37 kart), en yüksek level 119, en çok 16 seçim bir round sonunda.
  Eski kuralda aynı politika seed 110'da level 128'e ve R50'de 32 seçimlik round sonuna çıkıyor (büyüme run sonunda hızlanıyor)
  ama 50 round içinde patlamıyor.
- Ölçülen en sert geri besleme stres politikalarında değil, seed 101 "Davranış-rezonans" çiftinde (eski kuralda 30 779 level).
  Yeni kuralda o da 172'de kalıyor.

## 12. Kabul şartları

| Şart | Durum | Ölçüm |
|---|---|---|
| Level başına temel 3 seçim; bedelli ödül farkları doğru | **Karşılandı** | Test: 3 level → 9 seçim; +1 ödülünden sonra level 4 seçim verir, önceden kazanılan 6 hak değişmez; temel hak 3 kalır |
| Normal aralıkta XP işleme doğru; XP ve haklar kaybolmuyor | **Karşılandı** | Test: tam 3 level'lık XP → 3 level, 0 kalan; 6 milyar XP → 1 380 level, bağımsız hesapla birebir; harcanan + saklı = verilen |
| Erken davranış erişimi korunuyor; üç zaman ayrı | **Karşılandı** (XP'siz ve vadesinde kollarda); XP'yi saksının önüne alan açılışta etkin düzen gecikiyor | 7. bölüm |
| Grid → yükseltme → temel güç geçişi çalışıyor ve gerçek run'da gözleniyor | **Karşılandı** | 8. bölüm: yükseltme 60 / 60 run'da R4–5; temel güç 39 / 82 run'da |
| Eş bütçeli XP yatırımı R50'den önce ölçülebilir fayda üretiyor | **Tutmadı** | 9. bölüm: fayda yalnız level / seçim sayısında; hasat başa baş, skor ×0,81–0,84; ayar turu kullanılmadı (nedeni ölçümle) |
| Seed 101 dahil normal 50 round'da teknik durma, geçersiz XP, on binlerce seçim yok | **Karşılandı** | 82 run: durma 0, geçersiz 0, en çok 612 seçim |
| Sayısal doyum gizlenmiyor | **Karşılandı** | 82 run'da doyum 0 (sütun her tabloda) |
| Yüksek level gereksinimi büyüyor; tablo sonunda sabit maliyet yok | **Karşılandı** | Test: 140'tan int sınırına kadar artan ve sonlu |
| Eski profillerin davranışı korunuyor | **Karşılandı** | 5. ve 13. bölüm |
| Yeni run / menü / tekrar başlatmada etki ve bekleyen iş sızmıyor | **Karşılandı** | Test: yeni sahnede level 1, saklı XP 0, grup boş, stat ve modifier listesi ilk run başıyla aynı; ana menü yolunda liste temizlenince grup sıfır |
| İnsan testi yapılmadan menü yükü ve eğlence "çözüldü" sayılmıyor | Sayılmadı | 10. bölüm ölçümdür |

## 13. Gerçekten çalıştırılan testler

Hepsi son kodla, izole kopyada, düşük öncelikle. Özet: [testler/TopluSonuc.txt](Bolum3-7-7/testler/TopluSonuc.txt); her testin tam
çıktısı aynı klasörde.

| Test / ölçüm | Sonuç |
|---|---|
| **XPV1Verification** (yeni, P7) | **PASS 39** |
| SayisalGuvenlikVerification / Refactor3762Verification / ZincirV1Verification | PASS 33 / 8 / 38 |
| AlanGeriBildirimi / KirilmaErisimiV1 / BedelliOdullerV1 / OdulAsamalariV1 / BossReward | PASS 23 / 35 / 93 / 71 / 127 |
| KirilmaV1 / TakvimV1 / DengeV1 | PASS 189 / 162 / 86 |
| HarvestBehavior / ElectricRevert / Specialization / StartLoadout / Mechanics | PASS 114 / 40 / 100 / 88 / 88 |
| SimpleResonance / ContactRefactor / RunPrototype / BossWeather / RoundPreview / Run50Reference / OptionsMenu | PASS 293 / 175 / 69 / 22 / 47 / 70 / 22 satır |
| ComicCardVerification (kart paneli; sonuç dosyası yazmaz) | PASS (logda 46 kontrol) |
| Ödül teklif dağılımı / bedelli ödül ölçümü | md5 3.7.6.2 ile aynı |

27 test PASS. Eski testlerin kontrol sayıları 3.7.6.2 ile aynı. **İlk turda iki test düştü ve düzeltildi** (özet dosyasında kayıtlı):
`KirilmaV1Verification` ("başlangıç kilitleri yalnız Kırılma V1 denge setinden gelir") ve `TakvimV1Verification` ("açık takvimli
profiller listesi") profil adlarını tek tek sayıyor; yeni profili tanımadılar. Listelere `Run50_XPV1` eklendi: denetim gevşetilmedi,
yeni profil için de aynı şey aranıyor (Takvim V1 ile aynı 15 tarih ve hedefler; Kırılma V1 setiyle birebir aynı başlangıç kilitleri ve
erken davranış teklifi). Başka beklenti değiştirilmedi. Geliştirme sırasında ayrıca yeni testin bir metin satırındaki tırnak hatası
bir turun derlenmemesine yol açtı (27 iş 4'er saniyede düştü); düzeltilip tur baştan çalıştırıldı.

Yeni test `XPV1Verification` — 39 kontrol:

| Konu | Doğrulanan |
|---|---|
| Veri | Run50_XPV1, Zincir V1'in takvimini, hedeflerini, havuzlarını, süresini, ekonomisini, 3 seçimini taşıyor; denge seti yalnız XP tablosu ve iki kart kuralında farklı; tablonun 140 maliyeti aynı, yalnız `tailGrowth` eklendi; 14 eski profilde yeni kural yok |
| Maliyet formülü | s = 0,025 / 0,05 / 0,10: level 140'ta kesintisiz; C(N+1) = C(N)(1 + s); int sınırına kadar örneklerde formülle aynı, sonlu; 3 000'e kadar her level'da artan; bekleyen level sayısının kapalı hesabı tek tek saymayla aynı; NaN / negatif / sonsuz katsayı reddedilir; eski tabloda maliyet sabit |
| XP işleme | tam 3 level'lık XP → 3 level, 9 seçim, 0 kalan; 6 milyar XP → 1 380 level (6 kare, kare başına en çok 256), bağımsız hesapla birebir; XP kaybı yok; sonraki maliyet büyüyor |
| Geçiş | tile → yükseltme → temel güç; şeritler; kilitli tür sunulmaz; erken davranış slotu; yükseltme kartı tile'ı yükseltir |
| Birikim grubu | XP kartı gruba girer, değerler eski; kart metni toplamın önceki ve sonraki hâlini yazar ve uygulamayla aynı; iki kart toplam verir (çarpım değil), listede tek modifier; ayrı XP kaynağı grupla çarpılır, gruba karışmaz; saksının gerçek hasat XP'si aynı adımı görür; hasar kartları hâlâ çarpımsal |
| İşlevsiz seçenek | tabanda "Atak aralığı" 180 teklifte yok; tabanın üstünde geri gelir |
| Seçim akışı | beş hak aynı panelde arka arkaya, sayaç 5 → 4 → 3 → 2 → "SON SEÇİM"; son seçimden önce round ilerlemez |
| Hesaplanıyor | 40 000 level'lık işte round sonu bekler, yazı görünür, iş bitince kaybolur; kart ekranı 120 000 hakla açılır |
| Sıra | boss round'unda kartlar bitmeden ödül ekranı açılmaz, sonraki round başlamaz |
| Seçim hakkı | +1 ödülünden önce kazanılan haklar değişmez |
| Teknik sınır | 1e24 XP sayaca sığmaz: hemen durur, XP saklanır, bir kez raporlanır; round sonu beklemez |
| Sızıntı | ana menü yolunda liste temizlenince grup sıfır; yeni sahnede level 1, XP 0, grup boş, statlar ilk run başıyla aynı |
| Eski profil | sabit kuyruk (level 200'de 150 000), çarpımsal XP kartı, eski kart metni, tabanda atak aralığı kartı hâlâ sunuluyor |

Ölçümler (hepsi son oyun koduyla; ölçüm aracına sonradan yalnız yeni set ve politika eklendi, mevcut setlerin kodu değişmedi):

| Set | Run | İçerik | Süre |
|---|---|---|---|
| `k377pilot` | 12 | kuyruk katsayısı (0,025 / 0,05 / 0,10) | 10,5 dk |
| `k377ab` | 40 | iki yön × (XP'siz, erken XP) × 10 seed | 56 dk |
| `k377ab2` | 20 | iki yön × vadesinde XP × 10 seed | 18,5 dk |
| `k377seed101` | 2 | seed 101 kabul çifti (yeni profil) | 3 dk |
| `k377stres` | 6 | ikinci stres tanımı (yeni profil) | 5,5 dk |
| `k377stresEski` | 2 | ikinci stres tanımı (eski kural) | 2 dk |
| `k377ayristirma` | 2 | iki kuralın ayrı etkisi | 3 dk |
| `k3761seed101` | 2 | eski profil (Run50_ZincirV1) seed 101 çifti, son kodla | 23,5 dk |

Ham çıktılar: [Bolum3-7-7/olcum](Bolum3-7-7/olcum) (round satırları, `_final.csv` run özeti, `_nodes.csv` alımlar, `_rewards.csv`
ödül teklifleri). Analiz: `Tools/Balance/XPV1/analyze_xp.py`.

Hangi ölçüm neyi geçersiz kılar: oyun kodu ya da `xp_v1_params.py` değişirse bu belgedeki bütün yeni profil ölçümleri yeniden
alınmalıdır. Eski profil referansları (P6, 3.7.6.1–2) geçerliliğini koruyor: aynı set son kodla yeniden çalıştırıldı ve satır satır aynı çıktı (5. bölüm). Bu belgedeki
"eski" sütunları o sonuçlardır.

Ölçüm aracındaki eklemeler (`BalanceRunMeasurement`): iki devam yönü ve iki stres politikası; XP kolları (yok / erken / vadesinde);
temel güç kartı tercihi; run başına kuyruk katsayısı ve birikim kuralı değiştirme (yalnız ölçüm, asset değişmez); round satırının
sonuna 11 sütun (etkin düzen round'u, davranış tile'ları, XP harcaması, XP çarpanı, XP kart toplamı, sonraki level maliyeti,
level bekleyişi, round sonu seçim sayısı …), run özeti dosyası (`_final.csv`), alım dosyasına kategori sütunu. Eski setlerin
politikaları ve mevcut sütunları değişmedi.

Diğer kontroller: sekiz asset üreticisi `--check` ile manifestle aynı; üretim koruması testi geçiyor; seçili profil dosyasına
(md5 `8150e65b…`) ve oyuncu kaydına (`meta.json`, son yazım 1 Ekim) dokunulmadı; HEAD `28e4650`, commit yok. Yeni dosyaların
`.meta` dosyaları eklendi (`LevelWorkStatusUI.cs.meta`, `Balance/XPV1.meta` ve üç asset'in meta'ları).

## 14. Kalan teknik sınırlar, insan Play kontrolü ve karar bekleyenler

**Teknik sınırlar (geçici; nihai sayı modeli P10):**

- Level sayacı `int`. Büyüyen maliyetle sayaca sığmayan iş (ör. 1e24 XP ≈ 1,6e10 level) hâlâ hemen durur ve raporlanır, XP
  silinmez (testte). Normal 50 round run'da bu duruma yaklaşılmıyor (profilin ölçülen run'larında en büyük round XP'si 2,7 milyon).
- Sayaca sığan ama çok büyük bir iş durdurulmaz: kare başına 256 level işlenir, round sonu bekler ve "hesaplanıyor" yazısı
  görünür. Büyüyen maliyet bunu çok zorlaştırır (10 milyon level için 3,7e17 XP gerekir) ama imkânsız kılmaz.
- XP `double`: yaklaşık 1e21'in üstünde maliyet XP'nin hassasiyetinin altında kalabilir (durur ve raporlanır).
- Hasar / can / kaynak `int` doyumu ve `float` stat sınırı 3.7.6.1'deki gibidir. Diğer temel güç kartları (hasar, atak aralığı,
  üretim) hâlâ çarpımsal birikir; yeni profilde level sayısı sınırlı kaldığı için ölçülen run'larda doyum olmadı (en yüksek hasar
  stat'ı 1 525, en büyük doğrudan vuruş 26 339; int sınırı 2,1 milyar), ama sınırsız modda bu yeniden gündeme gelir.
- R50 sonrası oyun akışı yok. Yüksek level adımları izole ilerleme testidir (XP doğrudan verildi); **endless oynanış testi
  değildir**.

**İnsan Play kontrolü (yapılmadı):**

- Kalan seçim sayacının ve "hesaplanıyor" yazısının yeri ve okunabilirliği (yalnız batch görüntüsünde incelendi).
- Toplanan XP kartının metni anlaşılıyor mu ("Toplanır: +12% → +15%").
- 220–510 seçimlik bir run'da seçim yorgunluğu; round sonunda 9–27 seçim üst üste nasıl hissettiriyor.
- XP yatırımı "değer" hissi veriyor mu (ölçüm: vermemeli).

**Kullanıcı kararı bekleyenler (uygulayıcı karar vermedi):**

1. **XP yatırımının değeri nasıl kurulacak?** Ölçüme göre sorun level sayısında değil. Seçenekler (hiçbiri uygulanmadı): fazla
   seçimin değerini artırmak (tile yükseltme oranı; temel güç kartının grid dolmadan da bir aday olarak sunulması); XP
   düğümlerinin fiyatını ya da etkisini değiştirmek (Hasat Deneyimi II 9 000 altın karşılığında ≈ +9 level veriyor); tile
   sayısını level'a daha çok bağlamak (grid açılma zamanı). Hepsi P7'nin ilerleme ayarlarının dışında, P8 / P11 ile kesişiyor.
2. **Seçim sayısı hedefi.** XP'siz run 222–234, yatırımlı run 285–374 seçim. Hedef insan testiyle belirlenecek.
3. **Küçük grid'de erken temel güç evresi** (8. bölümdeki gözlem) istenen bir yol mu?
4. **Diğer temel güç kartlarının birikimi** (çarpımsal) sınırsız mod için ayrıca kararlaştırılacak mı?
5. Var olan bir metin: "Hasat hasarı" temel güç kartının değer satırı "Saksı hasar statı +%3" yazıyor (ad uyumsuz; bu pakette
   dokunulmadı).

## 15. Ana TODO'daki karşılığı (P7)

| P7 maddesi | Sonuç | Durum |
|---|---|---|
| Level başına üç seçim korunarak XP eğrisi ve menü yükü ölçülsün | Ölçüldü (10. bölüm). Hedef sayı insan testini bekliyor; level sayısı dayatılmadı | **Kapandı** (ölçüm) |
| Kart edinimi / etkin saksı / ilk tetik ayrı ölçülsün; erken davranış R5–6'ya kadar erişilebilir | Üç zaman ayrı (7. bölüm); XP'siz ve vadesinde kollarda korunuyor | **Kapandı** |
| Grid doluyken yükseltme, tükenince temel stat geçişi gerçek satın alımla | Test + 82 gerçek run (8. bölüm) | **Kapandı** |
| XP yatırımsız / eş bütçeli XP yatırımı: geri ödeme round'u, seçim, güç, kota payı | Ölçüldü (9. bölüm); sonuç olumsuz | **Kapandı** (ölçüm) |
| R50 öncesi anlamlı fayda, endless'ta devam eden büyüme; stat yönü zorlanmasın | **Sağlanmadı**: XP yatırımı R50'ye kadar hasat / skor olarak geri ödemiyor. Endless akışı yok | **Açık** — kullanıcı kararı (14. bölüm) |
| Tablo sonrası gereksinim, sıfır / negatif eşik, taşma, sonsuz döngü; "cap yok" | Büyüyen maliyet; test edilen aralıkta tasarımsal level sınırı yok; teknik sınırlar yazılı | **Kapandı** (nihai sayı modeli P10'da açık) |
| XP geri beslemesi | Toplanan XP kartları + büyüyen maliyet; seed 101 ve stres | **Kapandı** (aday profilde; insan onayı yok) |
| Yüksek level maliyet eğrisi | Doğrusal kuyruk, s = 0,05; geri ödemeye etkisi yok | **Kapandı** |
| Seçim ekranı yükü | Sunum uygulandı (sayaç, hesaplanıyor durumu); on binlerce seçim yok. Yük ölçüldü, çözülmedi | **Açık** (kısmi) — insan testi |

P7'nin kabul koşulu ("XP yolu yalnız endless bekleyen cezalı bir yol değil") **sağlanmadı**: ölçümde XP yolu R50'ye kadar cezalı.
Endless akışı, nihai sayı modeli (P10), genel kota dengesi ve zincirin kırılma gücü (P8) bu paketle tamamlanmış sayılmaz. P8'e
geçilmedi.
