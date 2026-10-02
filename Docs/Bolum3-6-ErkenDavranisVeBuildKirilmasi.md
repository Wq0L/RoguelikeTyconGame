# Bölüm 3.6 — Run sonrası hata düzeltmeleri ve build kırılma prototipi

**Durum (2026-10-02):** üç hata düzeltildi ve doğrulandı; `Run50_KirilmaV1` profili çalışıyor ve ölçüldü. Bu bir **ölçülmüş denge adayıdır**:
insan oyun testi yapılmadı, sayılar bot ölçümüdür. Commit atılmadı; seçili profilin (`Run50_DengeV1`) ve oyuncu kaydın değişmedi.

## Kısa sonuç

- **Üç hata:** Kıvılcım uyarısı, boss bölgesinin tile rengini kapatması ve boss HUD renkleri düzeltildi (bütün profillerde).
- **Erken davranış:** hedef tuttu. Davranış yollarını deneyen 20 run'ın 20'si ilk patlama / elektrik kartını R2'de aldı
  (düzeltilmiş DengeV1 tabanında R9–R14).
- **Level başına üç seçim:** çalışıyor; run başına ~200–270 kart ekranı demek (DengeV1'de ~60). Boss ödüllerini önemsizleştirmiyor.
- **Kırılma ödülleri gerçek hasat üretiyor ama küçük.** Kontrollü ölçümde (aynı tarla, aynı seed) fark, izin verilen aralığın
  en güçlü ucunda bile ×1,5'in çok altında kaldı. **"Güçlü sinerjide en az ×1,5" hedefi karşılanmadı.**
- **Çifte Akım ölçülebilir fark üretmiyor.** Artçı Patlama ve Hasat Ritmi gerçek run'da geç oyunda eşine göre ~×1,3–×1,4 hasat
  hızı veriyor (son parametrelerle).
- Neden küçük kaldığı ölçüldü: hasadı vuruş sıklığı sınırlıyor, yankılar ise ilk darbenin zaten boşalttığı hücrelere vuruyor
  (5. bölümün sonu). Bu, sayı ayarıyla çözülecek bir şey değil; karar senin.

## 1. Gerçekten uygulananlar

### Ortak kod (bütün profillerde geçerli)

| Konu | Ne değişti | Dosyalar |
|---|---|---|
| Hata A: Kıvılcım uyarısı | Saksıya ait statlar için oyuncu / global taban sorgulanmıyor | `StatManager.cs`, `CoreStatSO.cs` |
| Hata B: boss bölgesi | Dolgu yerine çevre çizgisi (tarla) ve çerçeve + köşe işareti (harita) | `FrostZoneMarker.shader`, `FrostZoneMarkers.cs`, `TileCellUI.cs`, `RoundMapUI.cs` |
| Hata C: boss HUD renkleri | Bütün boss renkleri boss kimliğinden | `BossTheme.cs` (yeni), `QuotaHUD.cs`, `SegmentEventText.cs`, `RoundSummaryUI.cs`, `GameFeelDirector.cs` |
| Sis kimlik rengi | Gri-lila (sunum verisi) | `Sis.asset`, `Sis_V1.asset` (üreticiden) |
| Kart atlama koruması | Seçim bittikten sonraki ikinci "atla" basışı hak harcamaz | `CardSelectionUI.cs` |

### Profil verisiyle açılan altyapı (varsayılanı eski davranış)

| Özellik | Veri alanı | Varsayılan | Kırılma V1 |
|---|---|---|---|
| Level başına kart seçimi | `RunProfileSO.choicesPerLevel` | 1 | 3 |
| Başlangıçtan açık kilitler | `RunBalanceSO.startingUnlocks` | boş | patlama ve elektrik kartları |
| Erken davranış teklifi | `RunBalanceSO.firstBehaviorOffer` | boş | patlama, elektrik |
| Kırılma ödülleri | `BossRewardPoolSO.breakthroughs` | boş | Artçı Patlama, Çifte Akım, Hasat Ritmi |

Kod: `RunProfileSO.cs`, `RunBalanceSO.cs`, `RoundManager.cs`, `SkillTreeManager.cs`, `SkillNodeUI.cs`, `ProgressionManager.cs`,
`CardSelectionUI.cs`, `BossRewardSO.cs`, `BossRewardPoolSO.cs`, `BossRewardManager.cs`, `BossRewardText.cs`, `RunPower.cs`,
`BehaviorEchoes.cs` (yeni), `PlanterBrain.cs`, `HarvestBehaviorManager.cs`, `ElectricBurst.cs`, `HarvestBehaviorGeometry.cs`,
`PlayerController.cs`, `HarvestCursorVisual.cs`, `QuotaHUD.cs` (Hasat Ritmi satırı), `VFXManager.cs`, `RunProfileMenu.cs`.

Kırılma ödülleri `PlayerController` içine boss kontrolü olarak yazılmadı: ödül verisi `BossRewardManager`'da durur, `RunPower`
üzerinden okunur; gecikmiş ikinci darbeleri `BehaviorEchoes`, ritim sayacını `HarvestRhythm` yürütür.

### Kırılma V1 verisi (yalnız yeni profil)

Üretici: `Tools/Balance/KirilmaV1/make_kirilma_v1.py`, düzenleme kaynağı `kirilma_v1_params.py`, kendi manifest'i var.
Yazdığı dosyalar: `Run50_KirilmaV1.asset`, `RunBalance_KirilmaV1.asset`, `BossRewards/KirilmaV1/` (üç ödül + havuz).
Temel statlar, bitki canı, XP tablosu, skill tree, boss'lar ve 13 küçük ödül DengeV1'in asset'leridir: kopyalanmadı, referans
verildi. `Run50_DengeV1`'in denge asset'leri yerinde değişmedi (üreticinin `--check` kontrolü geçiyor).

### Tasarım kararını uygularken yaptığım yorumlar

Belgede açık olmayan yerlerde seçtiğim uygulama. Her biri tek satırlık değişiklikle çevrilebilir.

1. **Patlamanın "yarıçapı".** Mevcut patlama "dört yönden komşu hücre" kuralıdır, sayısal yarıçapı yok. Bunu hücre merkezleri
   arasında 1,2 hücre olarak tanımladım (bugünkü hedef kümesini birebir verir). Artçı ×1,25 ile 1,5 hücreye çıkar: **çaprazlar da
   girer** (1×1 saksıda 4 → 8 hücre). ×1,18'in altı hiçbir hücre eklemez; ×1,40 yine yalnız çaprazları ekler.
2. **Artçının hedefleri darbe anında, hücre üzerinden bulunur.** İşte bitki referansı saklanmaz. O anda alanda canlı duran bitki
   (0,2 sn içinde yeni doğmuş olsa bile) geçerli hedeftir; ilk patlamada ölen bitki tekrar ödüllendirilmez.
3. **Hasat Ritmi hakkı boşa gitmez.** Hiçbir canlı bitkiye değmeyen savuruş hakkı harcamaz. Hak hazırlandığında sayaç 0'a döner;
   hak dururken yapılan hasatlar sayılmaz (en çok bir hak).
4. **"Davranışı destekleyen saksı varsa"** = tarlada en az bir saksı var. Kart yine rastgele hücreye düşer; saksının altına
   düşmesi garanti değildir.
5. **Satılmayan kilit düğümü.** Patlama ve elektrik kilit düğümleri bu profilde satılmaz ve "alınmış" görünür. Arkalarındaki
   düğümler (Kasırga Kartları) için bu adım, düğümün kendi ön koşulu sağlanınca geçilmiş sayılır; yani kasırganın sırası
   değişmedi, yalnız aradaki ücretli adım kalktı.
6. **Çevre çizgisi saksının arkasında soluk görünür.** Hata B'nin "saksı ve bitkinin üstüne boss dolgusu gelmesin" kuralı dolgu
   için; ince, soluk çizgiyi bilinçli bıraktım (yoksa dolu tarlada bölge görünmüyor). İstenmezse `OccludedAlpha` 0 yapılır.

## 2. Üç hatanın kök nedeni ve çözümü

Üçü de bütün profillerde geçerli **ortak düzeltmedir**; Kırılma V1'in profil verisinden bağımsızdır (profil verisi 4. bölümde).

### A) Kıvılcım ve "taban statı bulunamadı" uyarısı

**Gerçek çağrı zinciri.** Ödül kartına tıklanınca: `BossRewardManager.Choose → Apply → StatManager.AddGlobalModifier`.
`AddGlobalModifier` her modifier için `OnStatChanged` bildirimi gönderiyordu ve bildirimin değerini
`GetFinalStat(modifier.statType, modifier.target)` ile hesaplıyordu. Bu sorgu oyuncu / global taban setine (`CoreStatsSO`) bakar.
Kıvılcım dört stat taşır: patlama, kasırga, bumerang ve elektrik şansı. Patlama şansının temel sette bir satırı var; diğer üçünün yok,
çünkü bu şansların tabanı saksıya aittir. Sonuç: üç uyarı (kasırga, bumerang, elektrik). Aynı sorgu ödül kaldırılırken de
(`ClearAll → RemoveGlobalModifier`) çalışıyordu.

**Yanlış hedefe uygulama yoktu.** Uyarının kaynağı oynanış yoluydu (tooltip, önizleme ya da analiz kodu değil), ama yalnız
*bildirimin değeri* hesaplanırken çıkıyordu. Gerçek şans başka yerde hesaplanır ve doğruydu: `PlanterBrain.GetFinalStat`.

**Çözüm.** `StatManager` artık temel sette tabanı olmayan bir stat için global değer sorgulamıyor; bildirim yine gidiyor, değeri
"yok" (NaN). `CoreStatsSO`'ya uydurma satır eklenmedi. Dinleyiciler (grid açılımı kendi stat'ını süzer, saksı mağazası değeri
kullanmaz) etkilenmedi.

**Doğrulanan gerçek şans (Kıvılcım ×1,35; KirilmaV1Verification).**

| Durum | Ödülsüz | 1 Kıvılcım | 3 Kıvılcım (×2,46) |
|---|---|---|---|
| Patlama tile'ı 0,30 | 0,300 | 0,405 | 0,738 |
| Kasırga tile'ı 0,22 | 0,220 | 0,297 | 0,541 |
| Bumerang tile'ı 0,18 | 0,180 | 0,243 | 0,443 |
| Elektrik tile'ı 0,40 | 0,400 | 0,540 | 0,984 |
| Elektrik tile'ı 0,80 | 0,800 | 1,000 (sınır) | 1,000 |
| Davranış tile'ı olmayan saksı | 0 | 0 | 0 |

Gerçek tetik sıklığı (aynı zar tohumu, doğrudan hasat sayısı üzerinden): patlama şansı 0,20 iken 1000 hasatta 203 tetik; üç
Kıvılcım sonrası şans 0,492 ve 1000 hasatta 475 tetik. Elektrik 0,80 iken 200 hasatta 164; sınırda (1,00) 200 hasatta 200. Davranışı
olmayan saksı 400 hasatta 0 tetik. Uyarı sayısı ödül alınırken, üç adette ve kaldırılırken 0.

#### Davranış tetik kuralları (şu anki hâl)

Hepsi `PlantSpawner.OnPlantDied → PlanterBrain.TriggerHarvestBehaviors` içinde, **yalnız doğrudan hasatta** (bitkiyi oyuncunun
vuruşu öldürdüyse) ve bitkinin sahibi olan saksı için çalışır. Davranışın öldürdüğü bitki yeni davranış tetiklemez (zincir yok).

Şans, her davranış için aynı yoldan: `saksının tabanı (0) + altındaki tile'ların düz değeri`, sonra saksıyı hedefleyen global
çarpanlar (Kıvılcım), sonuç 0–1 arasına sıkıştırılır; rezonans en son ayrıca uygulanır. Davranış hasarının tabanı oyuncunun
`HarvestDamage` stat'ıdır (doğrudan vuruşun zar sapması ve kritiği girmez); üstüne tek sefer davranış çarpanı
(rezonans × uzmanlaşma × Yıkım Gücü) gelir.

| Davranış | Tetik | Ne yapar | Sınır |
|---|---|---|---|
| Doğrudan vuruş | Saldırı aralığı dolunca; çemberin değdiği her hücre | Hedef başına hasar × sapma (0,85–1,15) × doğrudan çarpanlar, tek yuvarlama; sonra kritik | Aralık en az 0,1 sn |
| Patlama | Doğrudan hasatta, saksının patlama şansı kadar | Saksının ayak izine dört yönden komşu hücrelerdeki bitkilere davranış hasarı | Görsel havuzu dolarsa yalnız çizim atlanır |
| Kasırga | Doğrudan hasatta, kasırga şansı kadar | Hasat edilen hücreden bir kasırga doğar | Kasırga doğamazsa (yönetici sınırı) tetik "atlandı" sayılır |
| Bumerang | Doğrudan hasatta, bumerang şansı kadar | Rastgele bir ana yönde 2–3 hücre gidip dönen orak | Aynı anda en çok 6; doluysa bu tetik **uygulanmaz** |
| Elektrik | Doğrudan hasatta, elektrik şansı kadar (eski sistem; yük yok) | Ayak izinin köşelerinden çaprazlara iki hücre; her hedefe bir kez davranış hasarı | Aynı anda en çok 8 çizim; doluysa yalnız çizim atlanır, hasar uygulanır |

Sıra tek hasatta sabittir: patlama → kasırga → bumerang → elektrik; her biri kendi zarını atar.

### B) Boss bölgesinin tile rengini kapatması

**Kök neden.** Tarlada `FrostZoneMarkers` her bölge hücresine yarı saydam bir **dolgu karesi** çiziyordu; derinlik testi kapalıydı,
bu yüzden dolgu saksının ve bitkinin de üstüne geliyor ve tile'ın kendi rengiyle karışıyordu. Haritada `TileCellUI.SetEventZone`
hücrenin tamamını kaplayan yarı saydam bir görüntü koyuyordu (aktifken %62, önizlemede %34 opak).

**Çözüm.**
- Tarla: bölgenin yalnız **çevre çizgisi** çizilir (bölge hücresinin, bölge dışına bakan kenarları). Önizlemede kesikli, aktifken
  düz. Renk boss kimliğinden (Don buz mavisi, Sert Kabuk amber). İç dolgu yok: tile kendi rengini korur.
- Çizgi zemindedir; saksının arkasında kalan kısmı ikinci bir kopya **soluk** çizer. Bunu ekran görüntüsünde gördükten sonra
  ekledim: tarla saksıyla doluyken zemin çizgisi neredeyse tamamen kapanıyordu.
- Harita: hücrenin çevresinde çerçeve (önizlemede kesikli, aktifken düz) ve sol üst köşede küçük bir üçgen. Hücrenin ortası boş.
- Ekran hava efekti (`BossWeatherOverlay`) ve vignette'e dokunulmadı.
- Gerçek bölge ve oynanış aynı: gösterim, olayın sakladığı hücreleri okur.

Piksel ölçümü (aynı kare, çizgi kapalı / açık): bölgedeki hasar tile'ının merkez pikseli çizgi açıkken de aynı; saksı ve bitki
merkez pikselleri aynı; kenar boyunca önizlemede 60 örneğin 35'i çizili (kesikli), aktifken 60'ı da çizili ve boss renginde.

### C) Boss HUD renkleri

**Kök neden.** `QuotaHUD` boss aktifken kâğıdı sabit bir buz mavisine boyuyordu (`FrostPaper`) ve boss satırını sabit mavi
mürekkeple yazıyordu (`SegmentEventText.Ink`). Round özeti bildirimi ve round girişi yazısı da aynı sabit renkleri kullanıyordu.
Hangi boss'un aktif olduğu hiç sorulmuyordu; Sert Kabuk'ta kum vignette doğru, panel yine mavi çıkıyordu.

**Çözüm.** Yeni `BossTheme` bütün boss sunum renklerini boss'un kimlik renginden (`SegmentEventSO.color`) türetir: HUD kâğıdı,
boss satırı, round özeti bildirimi, round girişi yazısı, tarladaki çizgi ve haritadaki çerçeve. Tamam / tutmadı gibi durum
renkleri (yeşil, turuncu) aynı kaldı. Renk her çizimde o anki boss'tan okunur: boss bitince, yeni boss seçilince, run yeniden
başlayınca ya da menüye dönülünce eski renk kalmaz.

| Boss | Kimlik rengi | HUD kâğıdı | Boss satırı |
|---|---|---|---|
| Don Cephesi | #8CD1FF buz mavisi | #E0F3FF | #125480 |
| Sert Kabuk | #FF9E40 amber | #FFE5CB | #804812 |
| Sis | #C7BDEB gri-lila | #F0EDF9 | #564A80 |

Sis'in kimlik rengi asset'te gri-maviydi (0,80 / 0,82 / 0,90); gri-lilaya çevrildi (0,78 / 0,74 / 0,92; `Sis.asset` ve üreticiden `Sis_V1.asset`). Bu bir **sunum
verisi** değişikliğidir, dengeye etkisi yok; DengeV1 üreticisinde başka hiçbir dosya değişmedi.

## 3. Yeni profili açma

1. Unity'de menü: **Tools → Run Profili → Run50 Kırılma V1 · 50 round (build kırılması adayı)**.
2. Play. Menüden yeni run başlat; çiftçi ve tırpan seçimi eskisi gibi.
3. Eski profile dönmek için aynı menüden `Run50 Denge V1` (ya da başka bir profil) seç.

Bu pakette senin seçili profiline dokunmadım: seçim hâlâ `Run50_DengeV1`. Yeni profili yalnız izole kopyada seçtim.

Sayıları değiştirmek için: `Tools/Balance/KirilmaV1/kirilma_v1_params.py` dosyasını düzenle, sonra
`python Tools/Balance/KirilmaV1/make_kirilma_v1.py`. Inspector'dan değiştirirsen üretici bir sonraki çalıştırmada durur ve
farkı önce parametre dosyasına taşımanı ister.

## 4. İlk ve son parametreler

Düzenleme kaynağı: `Tools/Balance/KirilmaV1/kirilma_v1_params.py`.

| Parametre | İlk | Son | Not |
|---|---|---|---|
| Level başına kart seçimi | 3 | 3 | Her seçimde üç aday; yalnız bu profilde |
| Başlangıçtan açık kartlar | patlama, elektrik | aynı | Bedava tile yok; kart normal seçimle, rastgele yere |
| Erken davranış slotu | patlama / elektrik | aynı | İlk kart alınana kadar, tarlada saksı varsa |
| XP eğrisi ve kazancı | DengeV1 | DengeV1 | Dokunulmadı (madde 11) |
| Round süresi, bitki canı, kota, boss hedefi, bütçe, ağaç fiyatları | DengeV1 | DengeV1 | Aynı asset'ler |
| Kırılma ödülü ilk boss | R10 | R10 | Her biri run'da en çok bir kez |
| Artçı Patlama gecikmesi | 0,20 sn | 0,20 sn | |
| **Artçı Patlama hasarı** | %70 | **%100** | 1. ayar turu |
| Artçı Patlama yarıçapı | ×1,25 | ×1,25 | Değiştirmedim: ×1,18–×1,40 arası her değer aynı hücreleri verir |
| Çifte Akım gecikmesi | 0,15 sn | 0,15 sn | |
| **Çifte Akım hasarı** | %80 | **%100** | 1. ayar turu |
| **Hasat Ritmi eşiği** | 6 doğrudan hasat | **5** | 1. ayar turu |
| **Hasat Ritmi hasarı** | ×1,5 | **×1,75** | 1. ayar turu |
| **Hasat Ritmi yarıçapı** | ×1,5 | **×1,6** | 1. ayar turu |

**Ayar turları.** İlk ölçümde kontrollü fark ×1,01–×1,18'di (hedef ×1,5). Hedefe bu kadar uzak olduğu için tek turda etkisi olan
her sayıyı izin verilen aralığın güçlü ucuna çektim. İkinci turu kullanmadım: aralıkta daha güçlü bir değer kalmadı ve geriye
çekmek için bir neden çıkmadı (son değerlerde de hiçbir ölçüm ×1,5'e yaklaşmadı). XP'ye dokunmadım.

## 5. Üç build yolunun ölçümü

### 5.0 Ne, nasıl ölçüldü

Ölçüm aracı `BalanceRunMeasurement` (izole kopya). Bot bir run'ı R1'den oynar: normal bütçe, gerçek satın alma, kart ekranındaki
üç adaydan biri, kart yeri oyunun rastgele kuralı, boss ödülü teklif edilenlerden biri. Bot kart yerini seçemez, bedava bir şey
kuramaz. Elenen run'lar tablolarda durur; seed elenmedi. Her politika aynı 10 seed ile (100–109) oynandı.

| Yol politikası | Ne yapar |
|---|---|
| Patlama yolu | Patlama kartını öne alır (ilk teklifte alır), saksıyı o tile'lara kurar; ödül sırası: Artçı Patlama, Yıkım Gücü, Kıvılcım… |
| Elektrik yolu | Aynısı elektrik kartıyla; ödül sırası: Çifte Akım, Yıkım Gücü, Kıvılcım… |
| Hız-alan + Ritim | 3.5'teki "Hız-alan" politikası; ödül sırası: Hasat Ritmi, Hızlı Bilek, Fırtına Bileği, Geniş Savuruş… |

Davranış yolları kasırga ve bumerang kilitlerini açmaz (kart havuzu seyrelmesin diye); bu bir politika seçimidir.

| Set | Profil | Ne için | Run |
|---|---|---|---|
| Taban | Run50_DengeV1 (üç düzeltmeyle) | Temiz karşılaştırma tabanı | 3 yol × 10 seed + Dengeli 4 + Yeni 4 |
| Kırılma | Run50_KirilmaV1 | Gerçek run, kırılma ödülleri açık | aynı (ilk ve son parametrelerle ayrı ayrı) |
| Almayan eş | Run50_KirilmaV1 | Teklifler aynı; bot kırılma ödülünü almaz, diğer iki seçenekten birini alır | 3 yol × 10 seed |
| Ödülsüz eş + laboratuvar | Run50_KirilmaV1, kırılma ödülleri havuzdan çıkarılmış | Kontrollü karşılaştırmanın kayıtları | 3 yol × 10 seed (ilk ve son parametrelerle) |
| Seçim sayısı | Run50_KirilmaV1 | 1 seçim / boss ödülü yok kolları | 3 yol × 5 seed × 3 kol |

Toplam 249 gerçek run ve 1.440 laboratuvar round'u. Laboratuvar dışında debug hakkı kullanılmadı.

**Temiz taban:** `Docs/Bolum3-6/olcum/taban/` altındaki DengeV1 ölçümü üç hata düzeltmesinden ve temas kuralı değişikliğinden
(TemasVeRefactor, yarıçap 1 → 0,9) sonraki hâldir. Bölüm 3.5'in sayıları bu değişikliklerden önceye aittir; karşılaştırma için
artık bu taban kullanılmalı.

### 5.1 Gerçek run sonuçları

| Profil | Politika | Kazanan | Elenen (round · hedefin yüzdesi) | Medyan toplam skor | Kazanılan level | Seçim hakkı | Kart: tile / yükseltme / temel güç |
|---|---|---|---|---|---|---|---|
| DengeV1 (taban) | Patlama yolu | 8 / 10 | R35 boss %87 · R45 boss %63 | 54.326 | 60 | 60 | 59 / 0 / 0 |
| DengeV1 (taban) | Elektrik yolu | 8 / 10 | R50 kota %96 · R45 kota %80 | 38.782 | 58 | 58 | 57 / 0 / 0 |
| DengeV1 (taban) | Hız-alan | 10 / 10 | – | 125.386 | 68 | 68 | 67 / 0 / 0 |
| DengeV1 (taban) | Dengeli | 4 / 4 | – | 149.658 | 78 | 78 | 75 / 1 / 0 |
| DengeV1 (taban) | Yeni | 0 / 4 | R5–R10 kota %55–%98 | 138 | 4 | 4 | 3 / 0 / 0 |
| Kırılma V1 (son parametreler) | Patlama yolu | 10 / 10 | – | 239.446 | 80 | 238 | 121 / 113 / 0 |
| Kırılma V1 (son parametreler) | Elektrik yolu | 9 / 10 | R20 boss %89 | 112.183 | 66 | 198 | 82 / 112 / 0 |
| Kırılma V1 (son parametreler) | Hız-alan + Ritim | 10 / 10 | – | 412.029 | 90 | 268 | 121 / 142 / 0 |
| Kırılma V1 | Dengeli (kırılma ödülü almaz) | 4 / 4 | – | 165.342 | 82 | 246 | 90 / 150 / 0 |
| Kırılma V1 | Yeni | 0 / 4 | R5–R10 kota %70–%89 | 142 | 3 | 9 | 9 / 0 / 0 |

İlk parametrelerle Kırılma V1: Patlama 10 / 10 (medyan 180.288), Elektrik 9 / 10 (99.773), Hız-alan + Ritim 10 / 10 (321.922).

**Seviye sayısı azalmadı.** Üç seçim, level sayısını düşürmedi; tersine güçlenen build daha çok XP topladığı için level de arttı
(yol politikalarında 58–68 → 66–90). Seçim hakkı = level × 3.

**İlk davranış kartı ve ilk gerçek tetik**

| Profil | Politika | İlk patlama / elektrik kartı (round) | R6 sonuna kadar alan | İlk gerçek tetik (round) |
|---|---|---|---|---|
| DengeV1 (taban) | Patlama yolu | R9–R14 | 0 / 10 | R10–R17 |
| DengeV1 (taban) | Elektrik yolu | R9–R14 (elektrik kartı R13–R28) | 0 / 10 | R10–R20 |
| Kırılma V1 | Patlama yolu | hepsi R2 | 10 / 10 | R3–R5 |
| Kırılma V1 | Elektrik yolu | hepsi R2 | 10 / 10 | R3–R6 |
| Kırılma V1 | Hız-alan + Ritim (davranışı aramıyor) | R2–R4 | 10 / 10 | R3–R7 |

Kart alındıktan sonra ilk tetiğe 1–4 round geçiyor: kart rastgele hücreye düşüyor, saksının altına gelmesi ve şansın tutması gerek.

**Kırılma ödülünün alındığı round** (30 yol run'ı): R10'da 10, R15'te 6, R20'de 5, R25–R35'te 7 run; 2 run'da kendi yolunun
ödülü hiç teklif edilmedi (slot rastgele).

### 5.2 Hasat hızı ve hasat payları (Kırılma V1, son parametreler)

| Pencere | Politika | Skor / sn | Hasat / sn | Doğrudan hasat | Davranış hasadı | Bunun yankı (artçı / ikinci dalga) payı | Ort. doluluk | Round sonu doluluk |
|---|---|---|---|---|---|---|---|---|
| R20–R30 | Patlama yolu | 51,0 | 3,51 | %38 | %62 | %18 | 0,71 | 0,70 |
| R20–R30 | Elektrik yolu | 35,5 | 2,73 | %48 | %52 | %10 | 0,75 | 0,73 |
| R20–R30 | Hız-alan + Ritim | 55,8 | 3,15 | %70 | %30 | – | 0,75 | 0,76 |
| R41–R50 | Patlama yolu | 345 | 12,7 | %33 | %67 | %19 | 0,63 | 0,60 |
| R41–R50 | Elektrik yolu | 242 | 8,9 | %44 | %56 | %10 | 0,71 | 0,69 |
| R41–R50 | Hız-alan + Ritim | 557 | 15,9 | %65 | %35 | – | 0,63 | 0,66 |

Karşılaştırma için DengeV1 tabanı, R20–R30: Patlama 12,4 skor/sn · 1,60 hasat/sn (doğrudan %80); Elektrik 12,4 · 1,74 (%77);
Hız-alan 17,4 · 1,96 (%89). R41–R50: 118 · 5,7; 87 · 4,6; 193 · 8,0.

"Yankı payı" bütün hasatlar içinde artçının / ikinci dalganın öldürdüğü bitkilerin oranıdır: gerçek hasat, görsel değil.
Round sonunda üretim noktalarının %60–%76'sı dolu kalıyor: tarla boşalmıyor.

### 5.3 Kontrollü karşılaştırma (laboratuvar)

**Kurulum.** Ödülsüz eş run'ının R15, R25 ve R40 başındaki durumu kaydedilir: açık grid, bütün tile'lar (zar değerleri ve
seviyeleriyle), saksılar, bütün global statlar, alınmış boss ödülleri, o round'un boss'u. Kayıt yeni sahnede aynen kurulur ve o
round iki kez oynanır: **A** olduğu gibi, **B** yolun kırılma ödülü eklenmiş. İki kol aynı zar tohumuyla ve dolu tarlayla başlar;
nişan politikası aynıdır. Her kol üç ayrı tohumla tekrarlandı (toplanır). Fark yalnız ödüldür. **Normal run değildir.**

**Güçlü sinerji kurulumu** (benim tanımım; R25 kaydının üstüne): davranış yollarında her açık hücrede saksı, her saksının altı
o yolun Legendary tile'ı (zarın üst değeri), Kıvılcım ve Yıkım Gücü tam adet. Hasat Ritmi'nde her açık hücrede saksı, Geniş Savuruş
ve Hızlı Bilek tam adet.

B ÷ A oranı; 10 eşin medyanı, parantezde en düşük – en yüksek eş. "İlk" ve "son" 4. bölümdeki parametrelerdir.

| Yol (ödül) | Kurulum | İlk: hasat | İlk: skor | Son: hasat | Son: skor | Son: yankı payı |
|---|---|---|---|---|---|---|
| Patlama (Artçı Patlama) | R15 | ×1,10 (1,01–1,28) | ×1,06 | ×1,14 (1,06–1,30) | ×1,21 | %18 |
| Patlama (Artçı Patlama) | R25 | ×1,15 (1,02–1,28) | ×1,22 | ×1,17 (1,07–1,30) | ×1,22 | %22 |
| Patlama (Artçı Patlama) | R40 | ×1,14 (1,07–1,32) | ×1,12 | ×1,19 (1,09–1,47) | ×1,23 | %23 |
| Patlama (Artçı Patlama) | **R25 güçlü sinerji** | ×1,11 (1,04–1,23) | ×1,19 | **×1,16** (1,05–1,25) | **×1,18** | %27 |
| Elektrik (Çifte Akım) | R15 | ×1,01 (0,92–1,29) | ×1,08 | ×1,02 (0,93–1,10) | ×0,97 | %9 |
| Elektrik (Çifte Akım) | R25 | ×1,03 (1,01–1,10) | ×1,01 | ×1,07 (1,00–1,13) | ×1,00 | %10 |
| Elektrik (Çifte Akım) | R40 | ×1,04 (0,89–1,31) | ×1,11 | ×1,05 (0,99–1,14) | ×1,06 | %9 |
| Elektrik (Çifte Akım) | **R25 güçlü sinerji** | ×1,05 (0,98–1,19) | ×1,12 | **×1,03** (0,96–1,14) | **×1,01** | %17 |
| Hız-alan (Hasat Ritmi) | R15 | ×1,10 (1,02–1,29) | ×1,15 | ×1,19 (1,06–1,38) | ×1,12 | – |
| Hız-alan (Hasat Ritmi) | R25 | ×1,17 (1,09–1,30) | ×1,26 | ×1,18 (1,12–1,35) | ×1,30 | – |
| Hız-alan (Hasat Ritmi) | R40 | ×1,18 (1,10–1,34) | ×1,22 | ×1,24 (1,13–1,33) | ×1,21 | – |
| Hız-alan (Hasat Ritmi) | **R25 güçlü sinerji** | ×1,18 (1,11–1,27) | ×1,25 | **×1,27** (1,13–1,35) | **×1,28** | – |

Son parametrelerle 120 eşin hiçbirinde hasat oranı ×1,5'e ulaşmadı (en yüksek ×1,47); skor oranı 9 eşte ×1,5'i geçti (hiçbiri
Çifte Akım değil), ama hiçbir kurulumun medyanı ×1,30'u aşmadı.

"Yankı payı": B kolundaki hasatların ne kadarını artçı / ikinci dalga yaptı. Pay %20'yi bulurken toplam hasat yalnız ~%15
artıyor: yankının öldürdüğü bitkilerin bir kısmını bir sonraki vuruş zaten öldürecekti.

### 5.4 Gerçek run'da eşli karşılaştırma (Kırılma V1 ÷ almayan eş)

Aynı politika, aynı seed, aynı teklifler; eş, kırılma ödülünün yerine diğer iki seçenekten birini alır. İki run ödülün alındığı
round'a kadar birebir aynıdır (ölçüldü: öncesi oran ×1,00). Hasat hızı oranının medyanı; parantezde oranı ×1,00'in üstünde olan
eş sayısı.

| Politika | Parametre | R20–R30 | R31–R40 | R41–R50 | Ödülü aldıktan sonraki 10 round |
|---|---|---|---|---|---|
| Patlama yolu | ilk | ×1,11 (7 / 10) | ×1,26 (8 / 10) | ×1,21 (8 / 10) | ×1,12 (8 / 9) |
| Patlama yolu | son | ×1,09 (7 / 10) | ×1,31 (8 / 10) | ×1,35 (9 / 10) | ×1,28 (8 / 9) |
| Elektrik yolu | ilk | ×1,00 (3 / 10) | ×1,01 (5 / 10) | ×0,99 (3 / 9) | ×1,00 (5 / 9) |
| Elektrik yolu | son | ×0,97 (2 / 10) | ×1,00 (4 / 9) | ×0,94 (2 / 8) | ×0,96 (3 / 9) |
| Hız-alan + Ritim | ilk | ×1,06 (7 / 10) | ×1,13 (9 / 10) | ×1,29 (9 / 10) | ×1,04 (8 / 10) |
| Hız-alan + Ritim | son | ×1,22 (8 / 10) | ×1,43 (9 / 10) | ×1,42 (10 / 10) | ×1,21 (10 / 10) |

R20–R30 penceresinde her eş ödülü almış değildir (bazıları R25–R35'te aldı; o round'a kadar oran tanım gereği ×1,00). Yalnız
ödülü R20'ye kadar almış eşlerde, son parametrelerle R20–R30: Patlama ×1,38 (7 eşin 6'sı ×1,00 üstü), Hız-alan + Ritim ×1,28
(6 eşin 5'i), Elektrik ×0,96 (8 eşin 2'si).

Skor hızı oranı (son parametreler, medyan): Patlama ×1,03 / ×1,35 / ×1,49; Elektrik ×1,00 / ×1,03 / ×1,00; Hız-alan + Ritim
×1,11 / ×1,56 / ×1,47. Dağılım geniştir: tek tek eşlerde ×0,1 ile ×4,7 arası oranlar var.

Gerçek run'daki fark laboratuvardakinden büyük çıkıyor, çünkü küçük bir üstünlük round'lar boyunca birikiyor (daha çok hasat →
daha çok XP ve kaynak → daha çok kart ve skill).

### 5.5 Üç seçim boss ödüllerini önemsizleştiriyor mu?

Hayır. Kırılma V1, ilk parametreler, seed 100–104, aynı politika:

| Politika | Seçim | Boss ödülü | Kazanan | Medyan toplam skor | Level | Seçim hakkı | Kart: tile / yükseltme / temel güç |
|---|---|---|---|---|---|---|---|
| Patlama yolu | 3 | var | 5 / 5 | 189.497 | 74 | 222 | 121 / 105 / 0 |
| Patlama yolu | 3 | yok | 2 / 5 | 17.021 | 45 | 135 | 61 / 74 / 0 |
| Patlama yolu | 1 | var | 2 / 5 | 16.156 | 42 | 42 | 42 / 0 / 0 |
| Patlama yolu | 1 | yok | 0 / 5 | 4.647 | 31 | 31 | 31 / 0 / 0 |
| Elektrik yolu | 3 | var | 5 / 5 | 89.753 | 64 | 192 | 81 / 105 / 0 |
| Elektrik yolu | 3 | yok | 2 / 5 | 25.059 | 50 | 150 | 81 / 69 / 0 |
| Elektrik yolu | 1 | var | 3 / 5 | 30.027 | 53 | 53 | 52 / 0 / 0 |
| Elektrik yolu | 1 | yok | 0 / 5 | 3.646 | 29 | 29 | 28 / 0 / 0 |
| Hız-alan + Ritim | 3 | var | 5 / 5 | 329.695 | 82 | 246 | 121 / 119 / 0 |
| Hız-alan + Ritim | 3 | yok | 0 / 5 | 3.887 | 29 | 87 | 49 / 38 / 0 |
| Hız-alan + Ritim | 1 | var | 5 / 5 | 110.776 | 66 | 66 | 65 / 0 / 0 |
| Hız-alan + Ritim | 1 | yok | 0 / 5 | 2.054 | 26 | 26 | 26 / 0 / 0 |

- Boss ödülleri (kırılma + 13 küçük ödül) kapalıyken run'ların çoğu eleniyor: üç seçimle 15 run'ın 4'ü, tek seçimle 0'ı kazandı.
  Üç seçimin getirdiği tile yükseltmeleri boss ödüllerinin yerini tutmuyor.
- Üç seçimin kendisi büyük bir güç kaynağı: boss ödülleri açıkken toplam skor ×1,8–×6,9 (aynı seed, medyan).
- Tek seçimli Kırılma V1 kolu DengeV1 tabanından zayıf göründü (Patlama 2 / 5, taban 8 / 10); 5 seed'le bunun nedeni ölçülmedi.

### 5.6 Havuz sınırları ve atlanan işlemler (run başına ortalama, son parametreler)

| Politika | Planlanan yankı | Uygulanan | Round bittiği için düşen | Yankı hasadı | Ritim hakkı | Güçlü vuruş | Atlanan elektrik çizimi | Atlanan patlama çizimi | Atlanan bumerang |
|---|---|---|---|---|---|---|---|---|---|
| Patlama yolu | 2.057 | 2.052 | 5 | 2.132 | – | – | 0 | 0 | 0 |
| Elektrik yolu | 1.297 | 1.296 | 2 | 694 | – | – | 18 | 0 | 0 |
| Hız-alan + Ritim | – | – | – | – | 422 | 421 | 0 | 0 | 0 |

Atlanan elektrik çizimi yalnız görseldir (aynı anda en çok 8 çizim); hasar uygulanır. Hasar ya da hasat atlanan işlem olmadı.
Düşen yankılar round'un son 0,15–0,20 saniyesinde planlananlardır.

### 5.7 Süre

Bir run'ın round süresi 50 × 45 sn = 37,5 dakika (kazanan run). Kart, ödül ve mağaza ekranlarında geçen süre **ölçülmedi**.
Varsayım olarak seçim başına 4 saniye alırsan: DengeV1'de ~60 seçim ≈ 4 dakika, Kırılma V1'de ~200–270 seçim ≈ 13–18 dakika.

### 5.8 Fark neden küçük?

Ödülsüz eş run'larının R25 satırlarından (patlama yolu, üç seed):

- Round başına 21–31 vuruş (saldırı aralığı 1,4–2,1 sn), vuruş başına ~6 hücre.
- Round başına 140–190 hasat; üretim kapasitesi 400–530 bitki. Üretimin ~%35'i hasat ediliyor, tarla %55–%75 dolu duruyor.
- Yani hasadı üretim değil **vuruş sıklığı** sınırlıyor; davranışlar da yalnız doğrudan hasatla tetikleniyor.

Yankılar bu sınırı aşmıyor:

1. Artçı ve ikinci dalga, ilk darbenin vurduğu hücrelere tekrar vurur. Build güçlendikçe ilk darbe o hücreleri zaten boşaltır;
   artçıya yalnız çaprazlardaki dört hücre kalır (yarıçap aralığı daha fazlasına izin vermiyor). Güçlü sinerji kurulumunda farkın
   normal kayıttan büyük çıkmamasının nedeni bu.
2. Çifte Akım'ın yeni hücresi hiç yok: aynı sekiz çapraz hücreye ikinci kez vurur. İlk dalga onları öldürdüyse ikinci dalga boşa gider.
3. Yankının öldürdüğü bitkilerin bir kısmını sonraki vuruş zaten öldürecekti (yerine geçme).
4. Hasat Ritmi doğrudan vuruşu büyütür, bu yüzden sınırın kendisine dokunan tek ödüldür; ama hak, son parametrelerle bile, yaklaşık her 2,5 vuruşta bir gelir ve üstünlüğü birikerek büyür (5.4).

Bu, Bölüm 3.2'de ölçülen darboğazla aynı (saldırı sıklığı). Sayıları büyütmek yerine tetik sayısını ya da erişilen hücre sayısını
artıran bir kural gerekir; bu paketin yetkisi dışında.

## 6. Hedeflerin sonucu

| # | Hedef | Sonuç | Dayanak |
|---|---|---|---|
| 1 | Erken davranışı deneyen açılışların en az 8 / 10'u ilk davranış kartını R6 sonuna kadar alsın | **Karşılandı** | 20 / 20 (hepsi R2); ilk gerçek tetik R3–R6. XP'ye dokunmadan. |
| 2 | En az iki farklı yol, R20–R30'da kendi ödülsüz eşine belirgin üstünlük göstersin | **Kısmen** | Aşağıda |
| 3 | Güçlü sinerji kurulumunda ilgili ödül hasat ya da skor hızında en az ×1,5 fark üretsin | **Karşılanmadı** | Son parametrelerle güçlü sinerjide hasat ×1,03 (Çifte Akım) / ×1,16 (Artçı Patlama) / ×1,27 (Hasat Ritmi); skor ×1,01 / ×1,18 / ×1,28. En iyi tek eş: hasat ×1,35, skor ×1,66 |
| 4 | Artış gerçek hasatta görülsün (yalnız efekt ya da overkill değil) | **Karşılandı, küçük** | Yankı hasatları sayıldı (run başına 694–2.132); kontrollü ölçümde toplam hasat da artıyor |

**Hedef 2'nin ayrıntısı.** "Belirgin üstünlük" için, almayan eş verisini görmeden koyduğum eşik: eşli medyan hasat hızı oranı en az ×1,20 ve eşlerin
en az 7 / 10'unda oran ×1,00'in üstünde.

- İlk parametreler: hiçbir yol eşiği geçmedi (Patlama ×1,11, Ritim ×1,06, Elektrik ×1,00).
- Son parametreler, bütün eşler: **yalnız Hasat Ritmi** geçti (×1,22, 8 / 10). Patlama ×1,09 (7 / 10), Elektrik ×0,97.
- Son parametreler, ödülü R20'ye kadar almış eşler: Patlama ×1,38 (6 / 7) ve Ritim ×1,28 (5 / 6) geçti; Elektrik geçmedi.

Yani ödül elindeyken iki yol (Artçı Patlama, Hasat Ritmi) eşinden belirgin önde; ama ödül rastgele slotla geldiği için
(Patlama yolunda 10 run'ın 3'ünde R25'ten sonra ya da hiç) R20–R30 penceresinin tamamında bunu yalnız Ritim gösteriyor. R31–R50'de iki yol da açık farkla önde (×1,31–×1,43).
Çifte Akım hiçbir pencerede fark üretmedi. Eş sayısı az (6–10) ve dağılım geniş; bunu kesin sonuç değil eğilim olarak oku.

**Hedef 3'ün ayrıntısı.** İzin verilen aralıklar tükendi (4. bölüm). Sınırlar içinde tutmadığı için, belgedeki kurala uygun
olarak altyapı ve çalışan prototip teslim edildi; hedef karşılanmadı olarak işaretlendi. Bitki canına, kota eğrisine ya da
mekaniğe dokunulmadı.

## 7. Çalıştırılan testler ve loglar

Hepsi izole kopyada (`Library/VerificationProject`), `Tools/run-isolated-verification.ps1` ile, birer birer. Aşağıdaki sonuçlar
son kod ve son parametrelerle 2026-10-02 01:40–01:53 arasındaki son çalıştırmadandır. İşlev testi "çalışıyor" der; denge ya da
eğlence kanıtı değildir.

**Yeni işlev testi:** `Tools/Verification/Editor/KirilmaV1Verification.cs` — **PASS, 153 kontrol.** Kapsadıkları: Kıvılcım'ın gerçek
şansı ve gerçek tetik sıklığı, uyarı sayısı; bölge çizgisi ve harita çerçevesi (piksel ölçümüyle); boss HUD renkleri (üç boss,
boss sonu, temizlik, yeniden başlatma); veri ayrımı; başlangıç kilitleri ve ağaç; erken davranış slotu; level başına üç seçim
(çift tıklama, yeniden açma, atlama, grid dolu); ödül slotu; Artçı Patlama, Çifte Akım, Hasat Ritmi'nin her kuralı; eski profilin
etkilenmediği.

**Etkilenen eski paketler**

| Test | Sonuç |
|---|---|
| DengeV1Verification | PASS · 86 |
| BossRewardVerification | PASS · 127 |
| RunPrototypeVerification | PASS · 69 |
| ContactRefactorVerification | PASS · 175 |
| BossWeatherVerification | PASS · 22 |
| StartLoadoutVerification | PASS · 88 |
| SpecializationVerification | PASS · 100 |
| ElectricRevertVerification | PASS · 40 |
| Run50ReferenceVerification | PASS · 70 |
| RoundPreviewVerification | PASS · 47 |
| OptionsMenuVerification | PASS · 22 satır |
| RoundMapComicVerification, ComicCardVerification | çıkış kodu 0 (sonuç dosyası yazmıyorlar) |
| **MechanicsVerification** | **FAIL** — "kart vignette opaklığı > 0,8" beklentisi; gerçek değer 0,65 |
| **HarvestBehaviorVerification** | **FAIL** (85 kontrolden sonra) — seçili profil DengeV1 iken bitki canı beklentisi tutmuyor |
| **SimpleResonanceVerification** | **FAIL** — seçili profil DengeV1 iken XP beklentisi tutmuyor |

**Üç düşen test bu paketin değişikliklerinden kaynaklanmıyor; beklentilerini değiştirmedim.**

- *MechanicsVerification:* `CardSelectionVignette.cs` içindeki opaklık bu paketten önce (dosya saati 1 Ekim 18:37) 0,85'ten 0,65'e
  çekilmiş ve vignette renkli yapılmış; test eski değeri bekliyor. Yalnız bu eşiği gevşeten **geçici** bir kopyayla (izole kopyada,
  sonra silindi) testin tamamı geçti: 83 kontrol. Düzeltmek için testteki `> .8f` eşiğini yeni değere göre güncellemek yeter;
  karar senin değişikliğinle ilgili olduğu için sana bıraktım.
- *HarvestBehaviorVerification ve SimpleResonanceVerification:* profil seçmiyorlar, projede seçili profille çalışıyorlar. Seçimin
  son commit'te `Prototip10_DebugButce` idi, şimdi `Run50_DengeV1` (denge seti: farklı bitki canı ve nadirlik XP'si). Yeni
  `LegacySelectionRunner` bu iki testi, yalnız izole kopyada eski profili seçerek aynen çağırıyor: **PASS · 111** ve **PASS · 291**.

**Güncellenen eski beklenti:** yok.

**Üretici kontrolleri:** `make_denge_v1.py --check` ve `make_kirilma_v1.py --check` temiz; `test_generation_guard.py` 3 / 3.

**Dokunulmayanlar (hash ile):** `Assets/Resources/RunProfileSelection.asset` ve `meta.json` paketin başındaki hash'lerle aynı;
HEAD hâlâ `743b904` (commit yok).

**Log ve çıktı yolları**

| Ne | Nerede |
|---|---|
| Test sonuçları | `Docs/Bolum3-6/testler/` (`TopluSonuc.txt` özet) |
| Ekran görüntüleri (bölge çizgisi, harita, üç boss HUD'u, Hasat Ritmi hazır); batch'te sahne ışığı sönük çıkar | `Docs/Bolum3-6/ekran/` |
| Temiz taban ölçümü (DengeV1) | `Docs/Bolum3-6/olcum/taban/`, özet `analiz_taban.md` |
| İlk parametre ölçümleri | `Docs/Bolum3-6/olcum/ilk-parametreler/`, özet `analiz_ilk-parametreler.md` |
| Son parametre ölçümleri | `Docs/Bolum3-6/olcum/son-parametreler/`, özet `analiz_son-parametreler.md` |
| Çözümleyici | `python Tools/Balance/KirilmaV1/analyze36.py <klasör>` |
| Ölçümü yeniden çalıştırma | `BalanceRunMeasurement.RunK36Base / RunK36Kirilma / RunK36Decline / RunK36Twin / RunK36Choice` |

Her `BalanceRuns_*.csv` round satırlarını, `_rewards.csv` ödül tekliflerini, `_nodes.csv` satın almaları, `.txt` run ve
laboratuvar özet satırlarını taşır. Laboratuvar satırlarının etiketi `LAB` ile başlar.

## 8. Bilinen sorunlar ve ölçülmeyenler

**Bu paketin sonuçları**

1. **×1,5 hedefi tutmadı; Çifte Akım etkisiz.** Ayrıntı 5. ve 6. bölümde. Kırılma ödülleri bu hâliyle "build'i kıran" ödül değil,
   orta boy bir güçlendirme (Çifte Akım o da değil). İzin verilen sayı aralıkları tükendi.
2. **Çifte Akım'ı almak zarar ettirebiliyor.** Küçük bir stat ödülünün yerine alındığı için: bir seed'de (Elektrik yolu, 106)
   ödülü alan run R20 boss'unda elendi, almayan eşi run'ı kazandı.
3. **Level başına üç seçim = çok kart ekranı.** Run başına ~200–270 seçim. Kart ekranında geçen gerçek süre ölçülmedi; her
   seçim 4 sn sürerse run'a ~13–18 dakika ekler. Yorucu olup olmadığı ancak oynayarak anlaşılır.
4. **Kartların yarısı tile yükseltmesi.** Açık grid çabuk dolduğu için ilk yükseltme kartı R5–R6'da geliyor; run boyunca alınan
   kartların ~%50'si yükseltme (run başına ~110–140). Grid genişledikçe yeni tile da gelmeye devam ediyor (run sonunda 81–121
   tile). Temel güç kartına hiç sıra gelmedi (0).
5. **Kırılma V1 "normal eğriyi" fazlasıyla aşıyor.** Yol politikaları 30 run'ın 29'unu kazandı; medyan toplam skor düzeltilmiş
   DengeV1 tabanının ~3–4 katı. Kota ve boss hedefleri DengeV1'den aynen geldiği için bu profilde baskı yok (bilinçli: can ve kota
   eğrisine dokunma kuralı).
6. **Kırılma ödülü her zaman gelmiyor.** Slot, sunulabilir kırılma ödülleri arasından rastgele seçiliyor; istediğin gelmeyebilir.
   30 yol run'ının 2'sinde kendi yolunun ödülü hiç gelmedi, 7'sinde R25 ve sonrasında geldi.

**Ölçümün sınırları**

7. **Bot insan değil.** Her vuruşta en iyi nişanı seçer, menüde vakit harcamaz, kart yerini bilmez ama saksıyı tile'a göre
   yerleştirir. "Yeni oyuncu" botu (rastgele) iki profilde de R10'u geçemiyor; bu, üç seçimle değişmedi.
8. **Tam run karşılaştırması gürültülü.** Küçük bir fark birkaç round'da büyüyüp küçülebiliyor (aynı politika ve seed'de bile
   R20–R30 skor hızı oranları ×0,1–×4,7 arasında). Bu yüzden asıl sayı kontrollü laboratuvardır; tam run tabloları eğilim içindir. 10 seed azdır.
9. **Laboratuvar normal run değildir.** Tarla, statlar ve ödüller gerçek bir run'ın o round başındaki kaydından kurulur; round
   dolu tarlayla başlar; boss'un türü gerçek run'dakiyle aynıdır ama bölgesi yeniden seçilir (iki kolda aynı). Güçlü sinerji
   kurulumu benim tanımımdır (5.3).
10. **Ödülsüz eşin iki tanımı var.** "Almayan eş" (teklifler aynı, bot kırılma ödülünü almaz) temiz olanıdır. İlk kullandığım
    "havuzdan çıkarılmış" eşte R10'dan sonra küçük ödül teklifleri de değiştiği için gürültü daha da fazlaydı; laboratuvar
    kayıtları o run'lardan alındı.
11. **Ölçülmeyenler:** insan oyunu ve his; kart ekranı ve mağaza süresi; kare süresi / performans (yankılar sırasında); kasırga
    ve bumerang yolları; diğer çiftçi ve tırpanlar (yalnız Bahçıvan + Standart); "1 seçim" kolunun neden DengeV1 tabanından
    zayıf çıktığı (5 seed, açıklaması ölçülmedi).

**Kod ve test tarafı**

12. **Üç eski test bu paketten bağımsız nedenlerle düşüyor** (7. bölüm): `MechanicsVerification` (kart vignette opaklığı başka
    bir oturumda 0,85 → 0,65 yapılmış; test hâlâ > 0,8 bekliyor), `HarvestBehaviorVerification` ve `SimpleResonanceVerification`
    (profil seçmiyorlar; seçili profil denge seti taşıyan DengeV1 olunca beklentileri tutmuyor). Beklentilerine dokunmadım.
13. **Batch Unity bazen açılışta takılıyor** (ölçüm sırasında bir kez: 10 dakika işlemci harcayıp ilerlemedi). Ölçüm betiğine,
    5 dakikada ilerleme dosyası oluşmazsa yalnız izole kopyanın Unity'sini kapatıp yeniden deneyen bir bekçi ekledim.
14. **Satılmayan kilit düğümleri** ön koşulları açılana kadar ağaçta görünmüyor (görünür olduklarında "alınmış" çiziliyor).
    Oyuncu, patlama ve elektrik kartlarının açık olduğunu ağaçtan değil kart ekranından anlıyor.
15. **Bölge çizgisi saksının arkasında soluk görünüyor** (bilinçli; 1. bölüm, yorum 6).
16. **Sis'in kimlik rengi değişti** (gri-mavi → gri-lila); eski profillerde de geçerli.

## 9. Kısa insan Play kontrolü

Otomatik testler "çalışıyor" der, "eğlenceli" demez. Aşağıdakiler ancak oynayarak görülür (10–15 dakika).

**Kırılma V1 ile (Tools → Run Profili → Run50 Kırılma V1):**

1. **İlk kart ekranı (R1–R3).** Tarlada saksı varken üç karttan biri patlama ya da elektrik olmalı; almazsan sonraki seçimde
   yine gelmeli; alınca bir daha zorlanmamalı.
2. **Level başına üç seçim.** Bir level üç ayrı kart ekranı açıyor. Bak: art arda üç seçim yorucu mu, kartların dağıtım
   animasyonu üçüncüde sıkıcı oluyor mu, "atla" beklediğin gibi yalnız o seçimi mi tüketiyor?
3. **Skill ağacı.** Patlayıcı Kartlar ve Çapraz Elektrik Kartları düğümleri "alınmış" (yeşil) görünmeli, tıklanamamalı, ipucunda
   "Bu profilde başlangıçtan açık" yazmalı. Kasırga Kartları 1×3 Saksı alındıktan sonra satın alınabilmeli.
4. **R5 boss'u.** Tarlada bölge yalnız çevre çizgisiyle görünmeli (hazırlıkta kesikli, boss round'unda düz); tile renkleri
   okunmalı; saksıların arkasında çizgi soluk devam etmeli. Bu çizgi yeterince görünür mü, fazla mı ince?
5. **Harita (round özeti).** Bölge hücrelerinde çerçeve ve sol üst köşede küçük üçgen; hücrenin rengi aynı.
6. **HUD.** Don'da kart buz mavisi, Sert Kabuk'ta amber, Sis'te gri-lila. Boss bitince kart krem rengine dönmeli.
7. **R10 boss ödülü.** Üç seçenekten biri kırılma ödülü olmalı (koşulu sağlanan). Alınca:
   - *Artçı Patlama:* patlamadan hemen sonra aynı yerde ikinci, biraz daha geniş patlama. Hissediliyor mu, yoksa ilk patlamanın
     içinde kayboluyor mu?
   - *Çifte Akım:* elektrikten hemen sonra ikinci dalga.
   - *Hasat Ritmi:* HUD'da "Hasat Ritmi n / 6"; hazır olunca imleç halkası altın rengine dönüp büyümeli, sıradaki vuruş daha
     geniş vurmalı. Halkanın büyümesi ve vuruşun gerçekten o alana değmesi tutarlı mı?
8. **Kıvılcım.** Alınca Console'da "taban statı bulunamadı" uyarısı çıkmamalı.

**Eski profille (Run50 Denge V1) hızlı bakış:** level başına tek seçim, patlama kartı yine ağaçtan açılıyor, boss ödüllerinde
kırılma ödülü yok; boss çizgisi ve HUD renkleri yeni hâliyle.
