# Bölüm 2 — Boss sonrası bedelli uzmanlaşma (uygulama notu)

Tarih: 30 Eylül 2026. Bölüm 1 altyapısı (run profili, kota, Don Cephesi) korunarak eklendi.
**Bölüm 2.1 (aynı gün):** Davranış Ustası'nın bedeli doğrudan hasardan hasat kaynağına taşındı (bölüm 8).
Katsayılar ve kota değerleri **test başlangıç değeridir**. ×0,90 kaynak bedeli dengelenmiş değer değil, ilk hipotezdir; ölçümler seçeneklerin dengeli olmadığını gösteriyor.

## 1. Akış
1. Round 10 biter: kota değerlendirilir.
2. Kota tutmazsa run biter, ödül yoktur.
3. Tutarsa uzmanlaşma "bekler". Önce bekleyen kart seçimleri, sonra uzmanlaşma ekranı (`RoundChoice` durumu), en son round özeti / hazırlık gelir.
4. Seçim bitmeden round 11 başlatılamaz (`RoundManager.StartNextRound` reddeder).
5. Seçim tek seferdir: çift tıklama, panelin yeniden açılması ya da ikinci istek reddedilir. Run başına tek seçim vardır, sonradan değiştirilemez.
6. Etki round 11'den itibaren geçerlidir. Yeni run'da (`OnRunStarted`), ana menüde ve yok edilmede (yeniden başlatma, sahne değişimi) aynı `ClearAll` ile temizlenir; 2.1'den beri kaynak kalanları da buna dahildir.
7. Run'ın son segmentinde ve uzmanlaşması tanımlı olmayan profillerde (10 round prototip, uzun run) ekran açılmaz.
8. Seçilen uzmanlaşma sonradan görülebilir: round HUD'u, round özeti ve run sonu ekranı. Kısa özet yalnız 1'den farklı katsayıları yazar (ör. "DAVRANIŞ USTASI · davranış ×1,25 · hasat kaynağı ×0,90").

## 2. Mimari
- **Veri:** `SpecializationSO` (`Assets/ScriptableObjects/Specializations/`): ad; doğrudan hasar, davranış hasarı ve hasat kaynağı çarpanı. Kart üzerindeki kazanç (yeşil) ve bedel (kırmızı) satırları bu değerlerden otomatik yazılır.
- **Profil:** `RunProfileSO.specializationAfterSegment` ve `specializationOptions`.
- **Kural:** `SpecializationManager` (RoundManager kurar). Bekleme, seçilebilirlik, tek sefer uygulama ve temizlik burada. `IRoundChoice` arayüzüyle RoundManager'a "bekleyen seçim var" der; RoundManager ödül türünü bilmez.
- **Kaynak bedeli:** `HarvestResourceScale` (aynı dosyada). Kaynak başına kesirli kalanı tutar. Yalnız `PlantResource.GiveReward` çağırır. `ResourceManager.AddResource` global olarak değişmedi.
- **Arayüz:** `SpecializationPanelUI` yalnız gösterir ve `Choose` ister. `UIManager` onu `RoundChoice` durumunda açar.
- `GameStates.RoundChoice` enum'un sonuna eklendi; önceki değerlerin sırası değişmedi.

## 3. Katsayıların uygulandığı yerler
| Etki | Taban | Uzmanlaşma katsayısı nerede | Sonra |
|---|---|---|---|
| Doğrudan hasar | `HarvestDamage` × sapma | `PlayerController`: sapmayla aynı çarpımda, tek yuvarlama | kritik × ; saksı bonusu (Odak) |
| Patlama | `HarvestDamage` | `PlanterBrain.BehaviorDamageMultiplier` (rezonansla aynı çarpım) | — |
| Kasırga | `HarvestDamage` × 0,5 | `TornadoManager` → `BehaviorDamageMultiplier` | — |
| Bumerang | `HarvestDamage` × 0,65 | `GetBehaviorDamage` → `BehaviorDamageMultiplier` | — |
| Elektrik | `HarvestDamage` | `GetBehaviorDamage` → `BehaviorDamageMultiplier` | — |
| Hasat kaynağı (2.1) | `rewardAmount` × saksı kaynak çarpanı, yuvarlanmış; duplicate ×2 | `PlantResource.GiveReward` → `SpecializationManager.ScaleHarvestResource` | `ResourceManager.AddResource` |

- Davranışların tabanı doğrudan vuruşun sonucu değil, ortak `HarvestDamage` stat'ıdır. Testte Davranış Ustası'yla patlama 125 (100 × 1,25) oldu.
- Skill ağacı hasarı iki tarafı da beslemeye devam eder. Kritik, saksı ve rezonans kuralları değişmedi.
- Kaynak bedeli XP'ye, Harvest Score'a, başlangıç parasına, satış iadesine, kart atlama ödülüne ve diğer düz kaynak eklemelerine dokunmaz.

## 4. Profil ve ayarlar
- `Tools > Run Profili > Uzmanlaşma testi · 20 round (normal ekonomi)`
- `Uzmanlasma20`: 20 round; kota 40 / 200 / 350 / 600; Don Cephesi 2. ve 4. segmentte; uzmanlaşma 2. segmentten sonra; 80 Gold.
- Katsayılar (doğrudan / davranış / hasat kaynağı):

  | Asset | Doğrudan | Davranış | Hasat kaynağı |
  |---|---|---|---|
  | `UstaBicici.asset` | 1,25 | 0,85 | 1 |
  | `DavranisUstasi.asset` | 1 (2.0'da 0,85) | 1,25 | **0,90** (2.1) |
  | `MevcutDuzeniKoru.asset` | 1 | 1 | 1 |

## 5. Bölüm 2.0 ölçümü (eski tasarım: Davranış Ustası doğrudan ×0,85)
Bu bölüm tarihçedir; 2.1 sonuçları bölüm 8'de.

**Varsayım:**
- Round 12 bitki canı.
- Hasat hasarı 6, saldırı 0,8 sn, alan 1,6, taban kritik %30 × 2.
- 5×5 alanda 25 adet 1×1 saksı.
- Bot her saldırıda en çok canlı bitkiyi kapsayan hücreye vurur.
- Her yapılandırma 3 round × 30 sn, aynı seed'ler, gerçek zamanlı kare.

| Düzen | Seçenek | Doğrudan öldürme | Davranış öldürme (P/K/B/E) | Toplam | Skor | Altın/Demir/Taş | XP | Tetik (atlanan) P/K/B/E |
|---|---|---|---|---|---|---|---|---|
| Doğrudan ağırlıklı (6 Odak) | Usta Biçici | 151 | 0 | **151** | 483 | 334/64/8 | 3020 | — |
| | Davranış Ustası | 111 | 0 | 111 | 340 | 248/40/6 | 2220 | — |
| | Koru | 124 | 0 | 124 | 473 | 264/76/8 | 2480 | — |
| | Aday ×1,5/×0,95 | 119 | 0 | 119 | 489 | 246/64/12 | 2380 | — |
| Davranışlı (8 davranış tile'ı) | Usta Biçici | 137 | 18 (10/5/1/2) | **155** | 599 | 328/76/14 | 3100 | 15/9/5/4 (0) |
| | Davranış Ustası | 92 | 30 (17/9/3/1) | 122 | 393 | 268/56/6 | 2440 | 13/7/6/3 (0) |
| | Koru | 118 | 25 (15/5/1/4) | 143 | 484 | 314/80/6 | 2860 | 13/8/4/4 (0) |
| | Aday ×1,5/×0,95 | 107 | 28 (16/2/6/4) | 135 | 424 | 290/64/6 | 2700 | 15/7/6/3 (0) |
| Davranış yoğun (16 davranış tile'ı) | Usta Biçici | 145 | 21 (5/7/0/9) | **166** | 620 | 358/88/12 | 3320 | 12/10/6/17 (0) |
| | Davranış Ustası | 103 | 16 (8/4/1/3) | 119 | 390 | 262/56/6 | 2380 | 11/5/10/7 (0) |
| | Koru | 122 | 12 (7/1/3/1) | 134 | 402 | 298/64/4 | 2680 | 10/5/10/4 (0) |
| | Aday ×1,5/×0,95 | 114 | 18 (4/4/6/4) | 132 | 476 | 286/72/8 | 2640 | 10/7/7/5 (0) |

## 6. Bölüm 2.0 sonucu
1. Usta Biçici üç düzende de en çok öldürmeyi yaptı (Koru'ya göre +%8 ile +%24). Davranış Ustası üç düzende de en az öldürmeyi yaptı (−%10 ile −%15).
2. **Neden:** davranışlar yalnız doğrudan öldürmeyle tetikleniyor. Doğrudan cezası hem doğrudan öldürmeyi hem tetik sayısını düşürüyordu; Davranış Ustası kendi avantajını zayıflatıyordu.
3. Bulgu yalnız o test koşulları (hasar 6, R12) için geçerliydi. 2.1 bu bedeli kaldırdı.

## 7. Doğrulama (2.1 kodu ile)
| Test | Sonuç |
|---|---|
| SpecializationVerification (2.1 ile genişletildi: akış, katsayılar, kaynak bedeli, ölçüm) | 100 / 100 |
| RunPrototypeVerification | 69 / 69 |
| MechanicsVerification | 83 / 83 |
| RoundPreviewVerification | 47 / 47 |
| HarvestBehaviorVerification | 111 / 111 |
| SimpleResonanceVerification | 291 / 291 |
| EconomyAnalyzerVerification | 448 / 448 |
| RunSimulator.RunSpecializationProgressBatch | ölçüm (geç/kal yok), bölüm 8.4 |

Hepsi izole kopyada (Library/VerificationProject), 2.1 kodunun son hâliyle koştu.

## 8. Bölüm 2.1 — Davranış Ustası'nın bedeli

### 8.1 Değişiklik
- Davranış Ustası: doğrudan ×1,00 (önceden ×0,85), davranış ×1,25 (aynı), **hasat kaynağı ×0,90** (yeni bedel).
- Usta Biçici ve Mevcut Düzeni Koru değişmedi. Skill ağacı, bitki canı, XP, üretim ve kota değişmedi. Bumerang/kasırga kapasitesi değişmedi.

### 8.2 Kaynak bedeli kuralları ve yuvarlama
- Bedel yalnız hasat ödülüne uygulanır. Doğrudan ve davranış öldürmeleri aynı `PlantResource.GiveReward` yolundan geçer.
- Uygulama, mevcut ödül hesabından sonradır (saksı kaynak çarpanı, yuvarlama, duplicate ×2), ve bir kez yapılır.
- **Sorun:** Hasat ödülleri küçük tam sayılar: Grass 2 Gold, Havuç 4 Iron, Patates 2 Stone, Legendary'ler 8–30.
  - Ödül başına en yakına yuvarlama: 2 × 0,9 = 1,8 → 2 ve 4 × 0,9 = 3,6 → 4 olur; ceza yok olur.
  - Aşağı yuvarlama: 2 → 1 (−%50), 4 → 3 (−%25) olur; ceza katlanır.
- **Yöntem:** Kaynak başına run içi toplam tutulur. Ödenen toplam = round(ham toplam × oran). Bu kesirli kalanı taşır ve tamamen deterministiktir (rastgelelik yok).
  - Toplam hiçbir hasattan sonra hedefin yarım biriminden fazla sapmaz.
  - 2 Gold'luk hasatlar sırayla 2, 2, 1, 2, 2 … öder.
  - 100 birim ham hasat tam 90 olur.
- Oran değişirse (yeni seçim) ya da `ClearAll` çağrılırsa (yeni run, ana menü, yok edilme) kalanlar sıfırlanır.

### 8.3 Sabit tarla testi (GameScene, izole kopya)
**Kurulum:**
- 3 düzen × 4 stat/can eşiği × 4 seçenek × **4 seed** (900–903).
- Her ölçüm 30 sn'lik bir round; toplam 192 round.
- Kare süresi sabit 1/30 sn (`Time.captureDeltaTime`). Aynı seed aynı oyun zamanı adımlarını üretir.
- Saldırı 0,8 sn, alan 1,6, kritik %30 × 2.
- 4. seçenek yalnız ölçüm içindir: **kontrol = Davranış Ustası'nın bedelsiz kopyası** (1 / 1,25 / kaynak 1).

**Stat/can eşikleri:**

| Eşik | Common canı |
|---|---|
| Hasar 6 · R12 | 17 |
| Hasar 10 · R14 | 19 |
| Hasar 14 · R12 | 17 (davranış ×1,25 patlaması 18 ile Common'ı tek vuruşta keser) |
| Hasar 30 · R14 | 19 (doğrudan vuruş çoğu bitkiyi tek seferde keser) |

**Kaynak bedeli tetikleri azaltmıyor:**
- Davranış Ustası ile bedelsiz kopyası 48/48 seed×yapılandırmada birebir aynı hasat, tetik (602 / 602), skor ve XP verdi.
- Kaynak oranı 0,900 (yapılandırma başına 0,898–0,902).

**Sonuçlar.** Round başına ortalama; köşeli parantezde 4 seed'in en azı–en çoğu. U = Usta Biçici, D = Davranış Ustası 2.1, K = Koru. Skor sütunundaki "D seed" = Davranış'ın Koru'dan yüksek/düşük skor aldığı seed sayısı.

| Düzen · eşik | Hasat U / D / K | Skor U / D / K (D seed üstün/geride) | Kaynak U / D / K | Davranış öldürmesi D / K |
|---|---|---|---|---|
| Doğrudan ağırlıklı · 6 | **50** [48–52] / 42 / 42 | 173 / 140 / 140 (0/0) | 131 / 101 / 112 | 0 / 0 |
| Doğrudan ağırlıklı · 10 | **65** [63–67] / 58 / 58 | 225 / 274 / 274 (0/0) | 175 / 138 / 154 | 0 / 0 |
| Doğrudan ağırlıklı · 14 | **84** [83–85] / 69 / 69 | 323 / 268 / 268 (0/0) | 223 / 167 / 186 | 0 / 0 |
| Doğrudan ağırlıklı · 30 | **108** [107–109] / 106 / 106 | 445 / 390 / 390 (0/0) | 302 / 260 / 289 | 0 / 0 |
| Davranışlı · 6 | **55** [51–57] / 49 [44–53] / 48 [44–53] | 205 / 177 / 172 (2/2) | 149 / 116 / 129 | 12 / 9 |
| Davranışlı · 10 | **71** [69–74] / 62 [58–66] / 63 [60–64] | 269 / 229 / 246 (1/3) | 192 / 145 / 170 | 11 / 10 |
| Davranışlı · 14 | **88** [85–90] / 83 [76–91] / 79 [75–83] | 332 / **342** / 263 (4/0) | 244 / 201 / 213 | 26 / 18 |
| Davranışlı · 30 | **111** [108–114] / 108 [105–114] / 103 [100–105] | 377 / **400** / 356 (3/1) | 311 / 267 / 288 | 25 / 15 |
| Davranış yoğun · 6 | **52** [50–54] / 45 [41–47] / 46 [44–50] | 194 / **204** / 169 (3/1) | 144 / 113 / 127 | 6 / 5 |
| Davranış yoğun · 10 | **72** [69–74] / 64 [60–68] / 64 [57–72] | 242 / 244 / 233 (1/3) | 196 / 160 / 171 | 12 / 10 |
| Davranış yoğun · 14 | **90** [86–93] / 81 [77–87] / 79 [73–82] | 337 / 296 / 304 (2/2) | 245 / 203 / 217 | 20 / 14 |
| Davranış yoğun · 30 | **112** [109–114] / 110 [107–113] / 110 [108–112] | 434 / 412 / 411 (2/2) | 303 / 270 / 297 | 25 / 21 |

**Notlar:**
- XP = hasat × 20 (ör. doğrudan · 6: 990 / 835 / 835).
- Hiçbir yapılandırmada bumerang/kasırga/elektrik kapasitesi yüzünden tetik atlanmadı; kapasite bu ölçümü etkilemedi.
- **Skor 4 seed'de çok gürültülü**, çünkü nadir bitkiler skoru belirliyor. Örneğin doğrudan · 10'da Usta +%12 hasata rağmen −%18 skor aldı (1 üstün / 3 geride). Skorda yalnız seed'lerin çoğunun aynı yöne gittiği satırlar yön gösterir.

### 8.4 İlerlemeli segment testi (RunSimulator)
**Kurulum:**
- `RunSimulator.RunSpecializationProgressBatch`, 100 seed (5000–5099), aynı seed kümesi.
- Uzmanlasma20 kuralları: 20 round, kota 40 / 200 / 350 / 600, Don 2. ve 4. segment.
- Başlangıç 80 Gold ve alışveriş politikası (saksı, skill kademesi) bütün seçeneklerde aynı. Round 11–20'de kazanılan kaynak her round sonunda aynı kuralla harcanır.
- Kaynak bedeli yalnız hasat gelirine uygulanır; kart atlama ödülüne ve satış iadesine uygulanmaz.
- Oyuncu profilleri: Deneyimli, Orta, Yeni ve "Deneyimli · davranış kartı öncelikli" (kart seçerken davranış tipini nadirlikten önce tutar). Bu profil de bütün seçeneklerde aynı politikadır.

**İlk boss, uzmanlaşmadan önce:** 10. round'u geçen run sayısı her seçenekte aynıdır. Round 1–10 bütün seçeneklerde birebir aynı çıktı: Deneyimli 100/100, Orta 100/100, Yeni 56/100, davranış öncelikli 100/100. Aşağıdaki oranlar yalnız boss'a ulaşan run'lar üzerinden.

**Hasar ve can (Koru, P50):**

| Profil | R5 | R10 | R11 | R15 | R20 |
|---|---|---|---|---|---|
| Deneyimli | 14 | 61 | 61 | 61 | 67 |
| Orta | 29 | 61 | 61 | 61 | 64 |
| Yeni | 2 | 9 | 22 | 43 | 70 |
| Common canı | 9 | 15 | 16 | 20 | 27 |

Deneyimli ve orta oyuncunun hasarı round 10'dan sonra bitki canının 3–4 katı. Bu yüzden simülatörde doğrudan ve davranış hasar katsayıları neredeyse hiçbir şeyi değiştirmiyor. Kaynak bedeli ise her zaman işliyor.

**Sonuçlar.** Oranlar aynı seed'de Koru'ya göre (ortalama, P10–P90, seed bazında üstün/geride). S3 = 3. segment skoru (r11–15, kota 350), S4 = 4. segment (r16–20, kota 600).

| Profil | Seçenek | 15'i geçen | Zafer | S3 oranı | S4 oranı | r11–15 alınan skill kademesi |
|---|---|---|---|---|---|---|
| Deneyimli | Usta | 100/100 | 100/100 | 1,000 (0,999–1,000; 2/23) | 1,004 | 5,3 (fark 0) |
| | Davranış 2.1 | 100/100 | 100/100 | **0,958** (0,877–1,045; 31/69) | **0,853** (0,570–1,148) | 3,3 (**−2,0**) |
| | Koru | 100/100 | 100/100 | S3 ort. 1689 | S4 ort. 3788 | 5,3 |
| Orta | Usta | 100/100 | 100/100 | 1,000 (hepsi eşit) | 1,000 | 4,9 (fark 0) |
| | Davranış 2.1 | 100/100 | **83/100** | 0,983 (0,952–1,009; 25/75) | **0,888** (0,731–0,977) | 1,1 (**−3,8**) |
| | Koru | 100/100 | 100/100 | S3 ort. 618 | S4 ort. 848 | 4,9 |
| Yeni (56 boss'a ulaşan) | Usta | 50/56 | 35/56 | 1,056 (1,000–1,235; 28/4) | 1,048 | 12,0 (+0,3) |
| | Davranış 2.1 | 46/56 | 34/56 | 1,008 (0,924–1,071; 33/23) | 1,028 (0,703–1,356) | 10,8 (−0,9) |
| | Koru | 46/56 | 33/56 | S3 ort. 530 | S4 ort. 768 | 11,6 |
| Davranış öncelikli (r11–15 davranış payı ort. %5,5, P90 %10,8) | Usta | 100/100 | 100/100 | 0,999 (2/69) | 0,996 | 6,1 (fark 0) |
| | Davranış 2.1 | 100/100 | 100/100 | **0,934** (0,839–1,040; 23/77) | **0,850** (0,611–1,069) | 4,0 (**−2,0**) |
| | Koru | 100/100 | 100/100 | S3 ort. 1755 | S4 ort. 4466 | 6,1 |

- **Kontrol ve eski tasarım:**
  - Bedelsiz kopya bütün profillerde Koru ile aynı ya da binde birkaç yukarıda (S3 1,000–1,002).
  - Eski 2.0 Davranış Ustası deneyimli, orta ve davranış öncelikli profillerde Koru ile aynı.
  - Eski 2.0, yenide S3 0,970 (5 üstün / 27 geride).
- **Duyarlılık (kanıt değil):** Davranış ek hasatının modeldeki payı ×3 yapıldı; davranış öncelikli profilde r11–15 payı ort. %14,6, P90 %27,5 oldu.
  - Davranış 2.1: S3 0,971 (41/59), S4 0,909.
  - Bedelsiz kopya: S3 1,002, S4 1,010.
  - Davranış payı sahnedeki düzeyde olsa bile simülatörde ×1,25'in kazancı ×0,90'ın kaybını karşılamıyor.

**Bu testin güvenilirlik sınırı:**
- Simülatör davranışları yaklaşık modelliyor: tetik yalnız doğrudan hasatta, zincir yok, "canlı bitki" olasılığı sabit. Doğal davranış payı %0–5 çıkıyor; sahnede davranışlı tarlalarda %9–30.
- Simülatörün satın alma modeli açgözlü (hedef round sırası). Eşik etkileri yüzünden küçük gelir farkı alımları round'larca kaydırabiliyor; örneğin orta profilde −%10 gelir, r11–15'te 4,9 yerine 1,1 kademe demek.
- Yön (kaynak bedeli zamanla birikiyor) güvenilir. Büyüklük (%11–15 S4 kaybı, orta profilde −17 zafer) kesin değil.

### 8.5 Eski tasarımla fark
| | 2.0 (doğrudan ×0,85) | 2.1 (kaynak ×0,90) |
|---|---|---|
| Doğrudan öldürme, hasar 6 · R12 (Koru'ya göre) | −%11 ile −%15 | Doğrudan ağırlıklıda aynı. Davranışlı düzenlerde doğrudan öldürme biraz düşük; toplam hasat −%3 ile +%2. |
| Tetikler | Doğrudan ceza tetikleri de azaltıyordu | Bedel tetiklere dokunmuyor (kontrolle birebir aynı) |
| Bedelin görüldüğü yer | Vuruş hasarı | Kaynak sayaçları ve hasat açılır sayıları (~%10 düşük) |
| Uzun vadede | Hasat az → kaynak az | Hasat aynı, kaynak az → daha az skill alımı → sonraki segmentlerde daha az güç (simülatör) |

### 8.6 Değerlendirme
**Davranış Ustası'nın avantajlı olduğu yerler:**
- Yalnız davranış tile'ı olan ve davranış hasarının bitki canı eşiğini geçtiği tarlalarda:
  - Davranışlı · 14: skor +%30, 4/0 seed.
  - Davranışlı · 30: skor +%12, 3/1 seed.
  - Hasat her ikisinde +%5.
- Bu iki satırda skor Usta'yı da %3–6 geçiyor. Ama hasat ve kaynakta Usta hâlâ önde, Davranış'ın kaynağı Koru'dan %6–7 düşük.
- Davranış yoğun · 6'daki +%21 skor, 4 seed'lik aralık (107–261) yüzünden kanıt sayılmaz.

**Davranış Ustası'nın dezavantajlı olduğu yerler:**
- Davranış tile'ı olmayan tarlada Koru'nun −%10 kaynaklı hâli; kesin en kötü seçenek.
- İlerlemeli simülasyonda deneyimli, orta ve davranış öncelikli profillerde Koru'dan kötü: S4 −%11 ile −%15, r11–15'te 2–4 skill kademesi eksik, orta profilde 17/100 zafer kaybı.

**Usta Biçici:**
- Sabit tarlada 12 yapılandırmanın hepsinde en çok hasat: +%2 ile +%23.
- Hasar canı çok aştığında avantaj eriyor: hasar 30'da +%2.
- Simülatörde deneyimli ve orta profilde Koru ile aynı (hasar zaten canın çok üstünde). Yeni oyuncuda en iyi seçenek (S3 +%5,6).

**Kaynak bedeli zamanla hissediliyor mu?**
- Tek round'da tam −%10.
- Simülatörde birikiyor: S3 kaybı %2–7, S4 kaybı %11–15, çünkü eksik alımlar sonraki üretimi düşürüyor.
- Oyuncunun bunu "hissedip hissetmediği" ölçülmedi (bkz. 8.8).

### 8.7 Açık sorunlar
1. **Hasar canı çok aşınca hasar uzmanlaşmaları anlamsızlaşıyor.** Simülatörde deneyimli oyuncu R10'da 61 hasarla 15–20 canlı bitkilere vuruyor; Usta ve Davranış bonusları sıfıra yaklaşırken Davranış'ın kaynak bedeli kalıyor. Gerçek oyuncunun R10 hasarı bilinmiyor; önce bu ölçülmeli.
2. **×0,90 hipotezi ilerlemeli modelde fazla pahalı görünüyor.** Değiştirmedim; kanıt simülatörden ve eşik etkisine duyarlı.
3. Sabit tarlada Usta hâlâ hasatta her yerde önde.
4. Simülatörün davranış modeli doğrulanmadı (%0–5 pay, sahnede %9–30).
5. Skor karşılaştırması için 4 seed × 30 sn yetersiz. Hasat ve kaynak kararlı, skor değil.
6. Kota 40 / 200 / 350 / 600 hâlâ test değeri.

### 8.8 İnsan testi adımları
1. `Tools > Run Profili > Uzmanlaşma testi · 20 round` seç.
2. Aynı oyuncu (ya da 2–3 kişi) her seçenekle en az 2 run oynasın; toplam ≥ 6 run.
3. Aynı plan hedeflensin: 10. round'a kadar "davranışlı" bir tarla (≥ 6 davranış tile'ı) kur.
4. Round 10'da not al:
   - İstatistik panelindeki hasat hasarı.
   - Davranış tile sayısı ve nadirliği.
   - Kota skoru.
5. Round 15 ve 20'de not al:
   - Segment skoru / kota.
   - Round 10'dan sonra alınan skill kademesi sayısı.
   - Eldeki Gold / Iron / Stone.
6. Oyuncuya sor:
   - "Davranışlar güçlendi mi, gördün mü?"
   - "Kaynak azlığını fark ettin mi; bir alımı geciktirdi mi?"
   - "Seçerken kararsız kaldın mı?"
7. **Karar ölçütü (öneri):**
   - Davranışlı tarlada Davranış Ustası run'ları S3/S4'te Koru'dan düşükse ×0,90 yumuşatılmalı ya da bedelin türü değişmeli.
   - Oyuncular R10'da hasarın bitki canının çok üstünde olduğunu gösteriyorsa sorun katsayıda değil, seçimin hasar üzerine kurulmasında.
