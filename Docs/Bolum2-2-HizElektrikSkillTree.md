# Bölüm 2.2 — Saldırı hızı, elektrik ve skill tree deneyi

> **Emekli (Bölüm 3.0, 30 Eylül 2026):** Yük biriktiren elektrik oyun hissi nedeniyle geri alındı. Elektrik yeniden doğrudan hasatta şansla tetiklenir. Aşağıdaki deney tarihsel kayıttır. Geri dönüşün kabul testi `ElectricRevertVerification` (Tools/Verification/Editor); `SpeedElectricVerification` artık kabul testi değildir. Süre deneyi (Deney22_20, Süre A/B) korunur.

Tarih: 30 Eylül 2026. Kapsam: ölçüm, yük biriktiren elektrik deneyi, süre kontrol koşulu ve skill tree dönüşüm önerisi.
Kalıcı ağaç değişikliği, 50 round ekonomisi, çiftçi/tırpan ve yeni saksı yok. Uzmanlaşma katsayılarına dokunulmadı.
Sayıların hepsi bu belgedeki testlerden ya da asset'lerden alındı. Simülatör ve bot durumları insan oyunu verisi değildir.

## 1. Güç ve bağımlılık haritası (gerçek asset ve kod)
Kaynak: `SpeedElectricVerification` asset'lerden okuyup yazdırır (Logs/SpeedElectricVerification.txt, "GÜÇ HARİTASI").

### 1.1 Saldırı
- **Taban saldırı aralığı: 3 sn** (CoreStat `AttackSpeed` = 3; stat bir aralıktır, küçük = hızlı).
- Hız node'ları `AttackSpeed MorePercent`. Bir node'un kademesi öncekinin yerine geçer; node'lar birbiriyle çarpılır.

| Aile | Hedef | Toplam fiyat | Aile sonunda aralık | Önkoşul |
|---|---|---|---|---|
| Hızlı Eller 1–4 | R3–25 | 399 G | 3 → **2,1 sn** (×0,70) | Biraz Daha Zaman 1 @3 |
| Akıcı Kesim 1–4 | R40–70 | 9 000 G | 2,1 → **1,47 sn** (×0,70) | Hızlı Eller 1 @3 |
| Yıldırım Kesim 1–3 | R65–100 | 30 000 G | 1,47 → **0,2 sn** (×0,136) | Akıcı Kesim 4 @2 |

- **Alt sınır:** stat ≥ 0,05; `PlayerController` max(aralık, 0,1) / tempo kullanır. En kısa aralık 0,1 / 1,5 = 0,067 sn; tam ağaçla 0,2 / 1,5 = 0,133 sn.
- Kare başına en fazla bir vuruş yapılır; taşan süre sonraki vuruşa aktarılır (`AdvanceAttackTimer`).

### 1.2 Süre ve tempo
| Aile | Hedef | Toplam fiyat | Süre |
|---|---|---|---|
| Biraz Daha Zaman 1–4 | R1–15 | 280 G | 30 → 45 sn |
| Uzun Hasat 1–4 | R30–65 | 5 400 I | 45 → 65 sn |
| Son Vardiya 1–4 | R75–100 | 2 700 S | 65 → 90 sn |

- Round en çok 60 sn sürer. Aşan kısım **tempo** olur: tempo = süre / 60, en çok 1,5.
- Tempo saldırı aralığını da (`PlayerController`) üretim aralığını da (`PlantSpawner`, tabandan sonra ve Don'dan önce) böler.
- Bu yüzden süre bütün yatırımların ortak çarpanıdır: round başına saldırı sayısı ve üretim sayısı, ham süreyle orantılıdır.
- **Önkoşul bağı:** Hızlı Eller 1, Biraz Daha Zaman 1'in 3. kademesini ister. Yani hız zinciri süre node'undan geçer.

### 1.3 Hasar, davranış ve tetik
- **Doğrudan vuruş:** `HarvestDamage` × sapma (0,85–1,15) × uzmanlaşma. Ardından kritik (taban %30 × 2) ve saksı bonusu (Odak).
- **Davranışlar (patlama, kasırga, bumerang, elektrik) yalnız doğrudan öldürmede tetiklenir** (`DamageTypeRules.CanTriggerBehaviors` yalnız Direct).
  - Davranış hasarı = `HarvestDamage` (kasırga ×0,5, bumerang ×0,65) × rezonans × uzmanlaşma.
  - Elektrik (mevcut kural): doğrudan öldürmede şans ≤ `ElectricChance` ise saksının çaprazındaki en fazla 8 hücreye `HarvestDamage` vurur.
  - Sonuç: saldırı hızı hem doğrudan hasadı hem öldürme başına tetiklenen davranışları çoğaltır.
- **Hasar zinciri:**
  - Keskin Başlangıç 1–4 (R1–15, **177 G**): +60 düz hasar, yani 61. Sonrası Kesim Tekniği (% ekler), Güçlü Kesim (+123,75), Kesim Ustalığı (%), Ağır Kesim (+1 000) ve diğerleri.
  - Aşırı Güç I (1 500 S, R90–100) ayrı ×8, Aşırı Güç II (7 500 S, R100–110) ayrı ×9.
- **Bitki canı** (PlantHealthScaling; Common / Legendary):

  | Round | R5 | R10 | R15 | R20 | R25 | R30 | R40 | R50 |
  |---|---|---|---|---|---|---|---|---|
  | Can | 9 / 25 | 15 / 45 | 20 / 60 | 27 / 80 | 35 / 105 | 44 / 130 | 66 / 196 | 98 / 294 |

### 1.4 Üretim, skor ve davranış kilitleri
- **Üretim:** taban 5 sn / üretim noktası.
  - Düzenli Üretim (R8–30, 799 G) ×0,85.
  - Verimli Üretim (R45–75, 5 850 I) ×0,85.
  - Seri Üretim (R65–100, 24 000 I) ×0,138; 0,5 sn tabana iner, fazlası nadirliğe döner.
- **Skor:** Hasat Rekoru 1–4 (R95–110, 6 000 I; önkoşul Zengin Cevher). Kotayı doğrudan belirleyen skor yatırımı ağacın en sonunda.
- **Davranış kilitleri:** Grid Genişleme I (80 G + 40 I) → 1×3 Saksı (40 G) → Patlayıcı (20 I) → Tornado (40 I) ve Çapraz Elektrik (80 I) → Bumerang (60 I).
  - Hız zincirine bağlı değiller. Ama Iron maliyeti erişimi geciktiriyor.
  - Simülatörde (mevcut kural, 40 seed, P50) elektrik kilidi: deneyimli R14, orta R24, yeni R30. Elektrik öncelikli oyuncu bile R24'te açıyor (bölüm 3.1).

## 2. Uygulanan elektrik deneyi

### 2.1 Mod seçimi
- Deney yalnız `Deney22_20` profilinde. Bu profil Bölüm 2'nin 20 round profiline dayanır: kota 40/200/350/600, Don 2. ve 4. segment, uzmanlaşma.
- Diğer profillerde alanlar varsayılandadır: elektrik öldürmeyle tetiklenir, süre mevcut kurala göre işler. Test bunu her profil için doğrular.
- **Çalıştırma:**
  1. `Tools > Run Profili > Deney 2.2 · 20 round (elektrik / süre deneyi)` seçilir.
  2. `Tools > Run Profili > Deney 2.2 ayarı` altında modlar seçilir:
     - Elektrik A · öldürmeyle tetik (mevcut)
     - Elektrik B · yük biriktiren (profilin varsayılanı)
     - Süre A · yükseltmeler ve tempo (mevcut, varsayılan)
     - Süre B · profil sabit 45 sn (kontrol)
  3. Play'e basılır.
- **Hızlı Şarj** ana ağaçta node değildir. `Deney22_20` asset'inde `electricCharge.quickChargeMultiplier` alanıdır (1 = yok). Yalnız dolum süresini çarpar; saldırı aralığını değiştirmediği testte doğrulandı.

### 2.2 Yük biriktiren elektrik (B)
Kod: `Assets/Scripts/Managers/ElectricChargeManager.cs`. Kancalar:
- `PlayerController.AttackInRadius`: isabet alan saksılar toplanır, saldırı sonunda boşaltma denenir.
- `PlanterBrain.TriggerHarvestBehaviors`: yük modunda elektrik şans zarı atılmaz.

Kurallar:
1. Elektrik şansı > 0 olan saksı, aktif hasat süresinde (round açık, durum Round) yük doldurur ve en fazla bir hazır yük tutar.
2. Oyuncunun **o saksıdaki yaşayan bitkiye doğrudan vuruşu** hazır yükü boşaltır; öldürmek şart değildir.
3. Aynı saldırı aynı saksının birkaç bitkisine değse de yalnız bir yük harcanır; farklı saksılar ayrı değerlendirilir.
4. Yük hazır değilken hızlı vurmak elektrik üretmez. Davranış hasarı yük boşaltmaz.
5. **Eski tetik yük modunda kapalıdır;** iki kural hiçbir zaman birlikte çalışmaz.
6. **Boşalma mevcut yolu kullanır** (`HarvestBehaviorManager.TryElectric` → `ElectricBurst.Strike`): hedef geometrisi, hasar (`HarvestDamage` × rezonans × uzmanlaşma), elektrik XP'si ve efekt havuzu aynıdır. Havuz dolunca yalnız çizim atlanır, hasar uygulanır.
7. **Yaşam döngüsü:**
   - Kart ve dükkân ekranlarında dolmaz.
   - Round başında ve sonunda, yeni run'da ve ana menüde sıfırlanır.
   - Saksı satılınca ya da elektrik tile'ı kalkıp şans 0 olunca yük silinir. Erişim geri gelince dolum sıfırdan başlar.
   - Don yalnız üretimi etkiler; yüke ek ceza yoktur.
   - Tempo (60 sn üstü süre) saldırı ve üretim gibi dolumu da aynı oranda hızlandırır.
8. **Gösterim:** saksının ortasında küçük camgöbeği halka. Saat yönünde dolar, hazırken tamamı parlak olur ve nabız atar (`Assets/Resources/ElectricChargeRing.shader`, oyun RNG'sine dokunmaz).

### 2.3 Şans → dolum süresi eşlemesi
**dolum süresi = max(alt sınır, referans süre × referans şans / şans × Hızlı Şarj)**

- Saksının etkin elektrik şansı kullanılır (tile toplamı + rezonans + global, en çok 1).
- Şans arttıkça süre kısalır (monoton). Şans 0 ise yük oluşmaz. Alt sınır Hızlı Şarj dahil her şeyden sonra uygulanır.
- Ayarlar `Deney22_20.electricCharge` altında: referans şans 0,5, alt sınır 3 sn, referans süre 14,4 sn: bölüm 3.6'da ölçülen elektrik sıklığından.
- Örnek (asset: referans 14,4 sn): şans 0,15 → 48 sn · 0,30 → 24 · 0,50 → 14,4 · 1,00 → 7,2 · Hızlı Şarj ×0,75 ile 0,50 → 10,8 sn.
- Tek tile'ın nadirliği (Common %12–18 … Legendary %45–60) ve aynı saksıdaki tile sayısı dolum süresine doğrudan yansır; değersizleşmez.

## 3. Ölçümler

### 3.1 Kurulum ve sınırlar
- **Temsilî durumlar:**
  - Kaynak: simülatör (`RunSimulator.RepresentativeStates`, normal ekonomi, Deney 2.2 kuralı, "Deneyimli" politika). 40 seed arasından R15 skoru medyana en yakın run seçildi (seed 9024).
  - O run'ın R10, R15 ve R20 başındaki skill etkileri, saksıları ve tile'ları sahneye kuruldu. Bunlar **simülatör durumudur, insan oyunu verisi değildir.**

  | Durum | Skill etkisi | Saksı | Tile |
  |---|---|---|---|
  | R10 | 12 | 6 (3 adet 1×2, 3 adet 1×1) | 9 |
  | R15 | 14 | 19 | 15 |
  | R20 | 24 | 15 (5 adet 2×3) | 22, içinde 2 Explosive |

  - Üç durumda hasar 61 (R20: 67), saldırı aralığı 2,9 sn (R20: 2,1), süre 45 sn (R20: 47), tempo 1, alan 1.
- **Düzenler:**
  - Doğrudan düzen = durumun kendisi.
  - Elektrik düzeni = aynı durum; saksı altındaki tile'ların yarısı aynı nadirlikte elektrik tile'ına çevrildi (şans: aralığın ortası). Sonuç: R10'da 5, R15'te 7, R20'de 6 elektrik saksısı.
  - Neden kurgulandı: simülatörde elektrik öncelikli oyuncu bile kilidi R24'te açıyor; doğal bir R10–20 elektrik düzeni oluşmuyor.
- **Ölçüm düzeni:**
  - Her varyant **8 seed** (900–907), tam round (45–47 sn), sabit kare süresi 1/30 sn. Toplam **472 round** (son koşu).
  - Kota, Don ve uzmanlaşma kapalı ölçüm profili kullanıldı.
  - Bot her saldırıda en çok canlı bitkiyi kapsayan hücreye vurur. B modunda hazır yüklü saksı +1 bitki sayılır ("yükü gören oyuncu").
- **Gürültü:**
  - Mekaniği hiç değiştirmeyen varyant (davranış hasarı, bu durumlarda) birebir aynı sonucu verir.
  - Birkaç öldürmeyi değiştiren varyant, sonraki üretimi zincirleme değiştirir. Hasatta ±%5, skorda ±%10–40 dalgalanma olur; skoru nadir bitkiler belirler.
  - Bu yüzden karar ölçütü **hasat ve kaynak**; skor yalnız seed aralığıyla birlikte okunmalı.
- **Tekrar:**
  - İlk iki koşu 4'er seed'le yapıldı ve üretim noktalarının başlangıç sırası sabit değildi. Bu yüzden aynı durumun iki bağımsız örneği sayılırlar.
  - Son koşuda sıra sabitlendi. Son koşunun kendisinin birebir tekrar ettiği ayrıca doğrulanmadı.
  - Ana karşılaştırmalar için üç örnek bölüm 3.7'de yan yana.

### 3.2 Referans (mevcut kural, taban; son koşu, 8 seed; ortalama [en az–en çok])
| Düzen · durum | Can (Common–Legendary) | Tek vuruş | Hasat (doğrudan / davranış) | Elektrik / round | Skor | XP | Gold / Iron / Stone | Dolu üretim noktası | Vuruş / round |
|---|---|---|---|---|---|---|---|---|---|
| Doğrudan · R10 | 15–45 | %100 | 53 [52–54] (53/0) | – | 159 [129–193] | 1 063 | 183/16/1 | %38 | 15 |
| Doğrudan · R15 | 20–60 | %99 | 74 [74–75] (74/0) | – | 622 [416–907] | 1 488 | 261/71/15 | %79 | 15 |
| Doğrudan · R20 | 27–80 | %99 | 125 [113–133] (107/18) | – | 1 958 [1 353–3 297] | 2 499 | 417/121/42 | %81 | 22 |
| Elektrik · R10 | 15–45 | %100 | 58 [54–63] (52/6) | 13,3 | 179 [127–233] | 1 153 | 198/16/2 | %33 | 15 |
| Elektrik · R15 | 20–60 | %100 | 100 [92–114] (74/26) | 13,1 | 848 [472–1 104] | 1 990 | 348/80/25 | %73 | 15 |
| Elektrik · R20 | 27–80 | %98 | 181 [166–196] (102/79) | 53,5 | 1 597 [1 137–2 079] | 3 618 | 594/151/45 | %66 | 22 |

- **Tek vuruş:** tam canlı bitkiye isabet edip aynı saldırıda öldürme oranı.
- **Hasat nadirliği** (C/U/R/E/L): doğrudan R15'te 28/23/16/6/2, elektrik R20'de 82/46/31/17/5.
- **Boş üretim noktası:** R10'da %62–67, R15 ve R20'de %19–34.
- R10'daki küçük tarlada üretim yine de sınırlayıcı değil. Üretim vuruş temposuna kilitleniyor: bir nokta her iki saldırıda bir hasat ediliyor (3.3).

### 3.3 Eşit göreli artış (+%10), aynı seed'de tabana göre: hasat % [seed aralığı] / skor %
| Varyant | Doğrudan R10 | Doğrudan R15 | Doğrudan R20 | Elektrik R10 | Elektrik R15 | Elektrik R20 |
|---|---|---|---|---|---|---|
| +%10 doğrudan hasar | 0 / 0 | +0,5 / +0,6 | +0,4 / −0,9 | 0 / 0 | +0,8 / −2,9 | +0,8 / −0,1 |
| +%10 saniyedeki saldırı (aralık ÷1,1) | **+12,7** [11…13] / +20 | **+12,9** [9…15] / +28 | **+8,4** [−1…26] / −3 | **+9,5** [2…14] / +8 | **+13,6** [−1…32] / +4 | **+8,2** [−4…20] / +24 |
| +%10 davranış hasarı | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| +%10 üretim hızı (aralık ÷1,1) | +0,5 / +6 | 0 / −2 | −14,4* / −36* | +2,2 / −6 | +0,1 / −1 | −10,2* / −2 |
| +%10 skor çarpanı | 0 / +4,6 | 0 / +8,5 | 0 / +9,3 | 0 / +5,2 | 0 / +8,4 | 0 / +8,6 |

- **Aralık ile sıklık aynı şey değil.** Saldırı sıklığı +%10 = aralık ÷1,1 (−%9,1). Aralık −%10 olsaydı sıklık +%11,1 olurdu. Round'daki vuruş sayısı tam sayıdır: +%10 sıklık 15 → 17 vuruş (+%13, R10/R15) ve 22 → 24 (+%9, R20) oldu.
- **Hasar ve davranış hasarı bu durumlarda doygun.** 61–67 hasar, bitkilerin %98–100'ünü tek vuruşta kesiyor. Davranış hasarı da aynı tabandan geliyor, o da tek vuruş.
- **Skor çarpanı +%10 yerine +%5–9 verir.** Ödül bitki başına yuvarlanıyor; Common'ın 1 puanı ×1,1'de hâlâ 1.
- **\* Bot yapaylığı:** R20'de +%10 üretimle bot kenardaki iki patlama saksısına hiç gitmedi; iki koşuda da davranış öldürmesi sıfıra düştü. Açgözlü hedeflemenin sonucu; oyun hatası değil, sonuç olarak okunmamalı.

### 3.4 Tek vuruş doygunluğu
- **Sahnede:** R10, R15 ve R20 durumlarının hepsinde taze bitkilerin %98–100'ü tek vuruşta ölüyor.
- **Simülatörde** (mevcut kural, 40 seed, P50; `RunSpeedPowerBatch`):
  - Deneyimli hasar: R5 14 · R10 61 · R15 61 · R20 67 · R25–30 92 · R40 743 · R50 1 817.
  - En düşük sapma (×0,85) ile Common tek vuruş: R5'ten R50'ye kadar 40/40 run.
  - Legendary: R10 40/40; R15–R25 0/40, R30 2/40 (yalnız kritikle); R40 ve sonrası 40/40.
  - Orta profil R10'dan itibaren aynı tabloda. Yeni oyuncuda R10'da 17/40.
- **Başlangıç:** Keskin Başlangıç 4 (177 G toplam, deneyimli P50 R7) hasarı 61'e çıkarıyor.
- **Sonuç:** R7'den yaklaşık R30'a kadar deneyimli ve orta oyuncu için doğrudan ve davranış hasar yükseltmelerinin hasada etkisi ölçülemiyor (+%0–0,8).

### 3.5 Satın alma verimi (gerçek fiyatlar)
| Durum | Sonraki kademe | Fiyat | Hasat (ölçülen ya da +%10'dan doğrusal) |
|---|---|---|---|
| R10/R15 | Hız: 3 kademe (Hızlı Eller 1 k2–k3, Hızlı Eller 2 k1) | 96 G (~0,3 round Gold geliri) | doğrudan +%12,7 / +%12,9; elektrik (A) +%9,8 / +%16,5 (ölçüldü) |
| R20 | Hız: Akıcı Kesim 1 (3 kademe) | 1 737 G (~3–4 round Gold geliri) | doğrudan +%9,9 [−1…20]; elektrik (A) +%6,2 (ölçüldü) |
| hepsi | Hasar: Kesim Tekniği sonraki kademe | 29–48 G | ≈ 0 (doygun) |
| hepsi | Üretim: Düzenli Üretim sonraki kademe | 39–139 G | ≈ 0 |
| hepsi | Süre: Uzun Hasat 1 sonraki kademe | 263–347 I (2–17 round Iron geliri) | süre +%2–4 → her şey aynı oranda (3.8) |
| hepsi | Skor: Hasat Rekoru | – | ulaşılamıyor (Zengin Cevher kilidi) |

Bu durumlarda süre dışındaki tek anlamlı yatırım saldırı hızı. Ucuz ilk hız kademeleri (Hızlı Eller) açık ara en verimlisi.

### 3.6 Elektrik A (öldürme) ve B (yük), aynı süre koşulunda (45–47 sn, tempo 1)
Referans dolum süresi mevcut kuralın ölçülen sıklığından alındı: 3 örnekte 14,4 / 17,6 / 15,4 sn (ortalama 15,8). Asset'e ilk ölçüm olan **14,4 sn** yazıldı; son koşuda B bu değeri kullandı.

| Durum | Elektrik / round A → B | Hasat | Skor [seed aralığı] |
|---|---|---|---|
| R10 | 13,3 → 4,0 (−%70) | −%5,9 | −%22 [−59…+17] |
| R15 | 13,1 → 8,0 (−%39) | −%6,2 | −%19 [−36…+7] |
| R20 | 53,5 → 14,6 (−%73) | −%10,0 | −%1 [−19…+39] |

- **B sıklığa eşitlenmesine rağmen daha zayıf.** A'da elektrik doğrudan öldürmeyle çoğalıyor: R20'de 102 öldürme → 53 elektrik. B'de her yük hem dolum süresi hem de o saksıya bir isabet istiyor. 15–22 vuruşluk bir round'da bot her hazır saksıya zamanında gidemiyor.
- Bu, B'yi "sırf güçlendirmeden" ölçmenin sonucu. Dolum süresinin kısaltılması ayrı bir tasarım kararı (bölüm 7).

### 3.7 Eşit bütçe: hız mı, Hızlı Şarj mı? (hasat % / elektrik %; aynı seed'de ilgili tabana göre)
Hızlı Şarj fiyatı varsayımdır: o durumdaki sonraki 3 hız kademesiyle aynı fiyata dolum ×0,73 (kademe başına ×0,9).

| Durum (bütçe) | A + hız | B + hız | B + Hızlı Şarj |
|---|---|---|---|
| R10 (96 G) | +9,8 / +16 | **+14,3** [11…16] / +50 | +1,6 [0…4] / +75 |
| R15 (96 G) | +16,5 / +14 | +13,5 [5…19] / +12,5 | +15,0 [7…19] / +50 |
| R20 (1 737 G) | +6,2 / +9 | +6,0 [−2…10] / +5 | **+15,0** [11…20] / +42 |

**Üç bağımsız örnek, hasat % (B + hız / B + Hızlı Şarj):**

| Durum | Koşu 1 (4 seed, ref 14,4) | Koşu 2 (4 seed, ref 17,6) | Koşu 3 (8 seed, ref 14,4) |
|---|---|---|---|
| R10 | 13,4 / 1,4 | 13,4 / 1,9 | 14,3 / 1,6 |
| R15 | 14,0 / 15,1 | 9,8 / 9,0 | 13,5 / 15,0 |
| R20 | 7,6 / 14,4 | 8,5 / 8,5 | 6,0 / 15,0 |

- **R10** (küçük tarla, az yük): hız her örnekte açık ara iyi.
- **R15:** eşit.
- **R20** (büyük elektrik tarlası, pahalı hız): 3 örneğin 2'sinde Hızlı Şarj yaklaşık 2 kat iyi. Dolumun daha yavaş (17,6 sn) olduğu örnekte eşit.
- **Yük modunda hız elektriği az artırıyor:** B + hız ile elektrik +%0–13 (R15/R20), Hızlı Şarj ile +%42–75. A'da ise hız, öldürmeyle birlikte elektriği de büyütüyor.

### 3.8 Süre (R15; elektrik ile karıştırmadan ayrı ölçüm; son koşu, 8 seed)
| Süre | Doğrudan: hasat / skor | Elektrik: hasat / skor |
|---|---|---|
| 30 sn | −%33,3 / −%36,5 | −%33,4 / −%32,5 |
| 45 sn (taban) | 0 | 0 |
| 60 sn | +%33,3 / +%27,7 | +%35,3 / +%30,8 |
| 90 sn (60 sn × tempo 1,5) | +%106,6 / +%113,2 | +%109,2 / +%113,5 |

- Hasat ham süreyle doğrusal: 45 sn'den sonra her +1 sn ≈ +%2,2 her şey.
- Süre bütün diğer yatırımların üstüne binen bir çarpan. Biraz Daha Zaman (280 G) 30 sn tabanda bütün hasadı +%50 artırıyor. Tam süre zinciri (+60 sn) hasadı 3 katına çıkarıyor (30 → 90).

**İlerlemeli karşılaştırma** (simülatör, Deney 2.2 kuralı, 100 seed; B'de süre node'u alınamaz):
- **Bu bir kazanma dengesi değil.** Farklar büyük ölçüde toplam hasat süresinden geliyor. R1–10'da etkili hasat saniyesi A'da 300–371, B30'da 300, B45'te 450.

| Politika | Zafer A / B30 / B45 | R10'u geçen A / B30 / B45 | Saniye başına skor R6–10, A / B30 / B45 |
|---|---|---|---|
| Deneyimli | 100 / 100 / 100 | 100 / 100 / 100 | 3,25 / 3,23 / 7,56 |
| Orta | 100 / 70 / 100 | 100 / 100 / 100 | 2,53 / 2,56 / 3,67 |
| Yeni | 34 / 17 / 65 | 56 / 58 / 76 | 1,51 / 1,62 / 1,95 |
| Deneyimli · elektrik öncelikli | 88 / 0 / 100 | 100 / 100 / 100 | 2,89 / 2,75 / 6,27 |

- A ile B30'un saniye başına skoru aynı. Süre yükseltmesi başka bir kanaldan değil, toplam süreden etkiliyor.
- B45'in fazladan erken süresi gelir ve güç olarak bileşiyor.

## 4. Skill tree dönüşüm tablosu (öneri; bu pakette ağaç asset'lerine dokunulmadı)
Karar sütunu: **Koru**, **Azalt** (etkisi azaltılması değerlendirilecek), **Dönüştür** (başka etkiye), **Yeni**, **Kaldır / profile taşı**.

| Node / aile | Karar | Değişiklik ve yeni etki | Önkoşul değişikliği | 50 round erişimi | Gerekçe | Ölçüm ihtiyacı |
|---|---|---|---|---|---|---|
| Keskin Başlangıç 1–4 | Koru, 3–4. kademeyi azalt | 1–2. kademe aynı. 3–4. kademe +16/+24 yerine daha küçük. Hedef: R10'da tek vuruş %100 değil, Common'da ~%80 | yok | R1–6 | Erken oynanabilirlik bundan geliyor. Ama 177 G ile 61 hasar R7'den R30'a kadar tek vuruş doygunluğu yaratıyor; sonraki hasar node'ları R30'a kadar boşa gidiyor (3.4) | tek vuruş oranı R10/15/20 |
| Kesim Tekniği 1–4 | Koru | yüzde hasar | yok | R6–14 | Doygunluk kırılırsa anlamlı erken seçim olur | aynı |
| Güçlü Kesim, Kesim Ustalığı | Koru, sıkıştır | değerleri 50 round canına göre yeniden yaz | yok | R12–30 | R50 canı Common 98 / Legendary 294; mevcut +123 ve +%100 hasar bunun çok üstünde | HP eğrisiyle birlikte |
| Ağır Kesim, İleri Ustalık, Plazma, Yıldız, Hasat Zirvesi | Azalt / birleştir | 20 node yerine 2–3 geç hasar node'u | Güçlü Kesim sonrası | R28–45 | 1 000–2 200'lük düz hasar blokları 50 round'da karşılıksız | — |
| Aşırı Güç I–II | Kaldır ya da seçimli kapston | ×8 / ×9 kısa run'a taşınmaz. İstenirse tek seçimli "doğrudan ×1,5 **ya da** davranış ×1,5" | geç hasar node'u | R44–50 | ×72 bütün hasat kararlarını anlamsızlaştırır | — |
| Hızlı Eller 1–4 | Koru | aynı (399 G, 3 → 2,1 sn) | Biraz Daha Zaman 1 yerine Keskin Başlangıç 1 @3 (süre profile geçerse zorunlu) | R3–12 | Ölçümde en verimli hasat yatırımı (96 G → +%10–16 hasat) | — |
| Akıcı Kesim 1 | Koru | genel hız −%10 (1 737 G) | Hızlı Eller 1 @3 | R14–24 | Doğrudan düzende değerli kalıyor (R20: +%10 hasat) | — |
| Akıcı Kesim 2–4 | Dönüştür | Genel hızın devamı yerine üç dala ayrılır: **Hızlı Şarj** (elektrik dolumu), **Geniş Kesim** (alan), **Seri Ekim** (üretim). Aynı fiyat bandı, biri alınınca diğerleri pahalanır ya da kilitlenir | Akıcı Kesim 1 @3 | R18–32 | Tekrar eden genel hız, bütün build'lerin aynı yatırımı seçmesine yol açıyor; ölçümde elektrik düzeninde Hızlı Şarj, R20'de hızı geçti | eşit bütçe tekrar |
| Yıldırım Kesim 1–3 | Azalt / Dönüştür | 30 000 G, ×0,136 yerine tek node, 3 kademe ×0,8; ya da doğrudan-build kapstonu | Akıcı Kesim dalı | R34–44 | 0,2 sn saldırı 50 round'da yeri olmayan bir geç oyun hedefi | — |
| Biraz Daha Zaman 1–4 | Profile taşı | Round süresini profil belirler (öneri 45 sn). Node'lar kaldırılır ya da "hazırlık" türü küçük erken ekonomi node'una çevrilir | Hızlı Eller ve Uzun Hasat önkoşulundan çıkar | — | Süre her şeyin çarpanı: 280 G ile bütün hasat +%50 (3.8). Seçim değil zorunluluk; bkz. 3.8 | süre profil değeri |
| Uzun Hasat 1–4 | Kaldır / Dönüştür | Iron harcama yeri gerekiyorsa Verimli Üretim'e ya da skor ailesine | — | — | +20 sn = +%44 her şey; 5 400 I | — |
| Son Vardiya 1–4 | Kaldır | tempo kuralı 50 round'da kalkar | — | — | +25 sn = tempo 1,5; R75–100 | — |
| Düzenli Üretim | Koru | aynı | yok | R6–14 | Ölçümde +%10 üretim çoğu durumda etkisiz (saldırı temposuna takılıyor); küçük ve ucuz kalmalı | tarla doluluğu |
| Verimli Üretim | Koru, erkene çek | aynı | Düzenli Üretim | R18–30 | Büyük tarlada (R20 doluluk %66–81) üretim değil vuruş sınırlayıcı | — |
| Seri Üretim | Azalt | ×0,138 → ~×0,6; taban taşması nadirliği korunur | Verimli Üretim | R32–42 | 24 000 I | — |
| Hasat Rekoru (skor) | Erkene çek | aynı etki | Zengin Cevher yerine Taş Hasat | R22–36 | Kota skorla geçiliyor; bugün skor yatırımı R95–110 | skor/kota |
| Patlayıcı / Tornado / Bumerang / Elektrik kilitleri | Erkene çek, ucuzlat | Elektrik 80 I → ~30 I ya da Gold | 1×3 Saksı korunur | Patlayıcı R6–9; Tornado ve Elektrik R8–14; Bumerang R12–18 | Simülatörde elektrik R14–30'da açılıyor; R10 uzmanlaşma seçiminde davranış yok | açılış round'u |
| **Hızlı Şarj (yeni, aday)** | Yeni | Yalnız elektrik dolum süresi ×0,9 / kademe, 3 kademe. Saldırı hızını değiştirmez | Çapraz Elektrik Kartları | R15–30 | Elektrik build'ine hızdan ayrı bir yatırım (3.7) | fiyat, eşit bütçe |
| Yük Rezervi (sonraki aday, uygulanmadı) | Not | 2 hazır yük saklama | Hızlı Şarj | R30–42 | Bot ve oyuncu her hazır saksıya zamanında vuramıyor (3.6) | — |
| Geniş Süpürüş / Geniş Tarama | Koru, Tarama'yı erkene çek | aynı | Tarama için Akıcı Kesim 1 şartı kalkar | R8–18 / R24–36 | Alan hızın alternatifi | alan ölçümü (yapılmadı) |
| Kritik Odak / Kritik Güç | Koru | aynı | yok | R15–28 / R30–42 | Doygunluk kırılınca anlamlı | — |
| Ekonomi (Altın Hasat I/II, Demir, Taş, Zengin Cevher, Nadir Filizler) | Koru, sıkıştır | fiyatları 50 round gelirine göre | yok | I R1–10; Demir R8–20; Taş ve Altın II R18–32; Zengin Cevher ve Nadir Filizler R28–42 | — | gelir eğrisi |
| XP / kart (Hasat Deneyimi, Kart Sezgisi, Vazgeçişler) | Koru, sıkıştır | aynı | yok | R10–40 | — | — |
| Grid Genişleme I / II | Koru, II'yi erkene çek | aynı | yok | I R3–10; II 9×9 R18–28, 11×11 R30–40 | — | — |

## 5. 50 round'a geçişte önerilen erişim zamanları
Simülatörde mevcut kuralla ölçülen P50 tamamlanma round'ları (deneyimli / orta / yeni) ve önerilen 50 round aralığı. Önerilen aralıklar tasarım hedefidir, ölçülmedi.

| Aile | Bugün (hedef) | Bugün, simülatör P50 | 50 round önerisi |
|---|---|---|---|
| Keskin Başlangıç | R1–15 | 7 / 6 / 21 | R1–6 |
| Biraz Daha Zaman | R1–15 | 8 / 8 / 26 | profile taşınır |
| Hızlı Eller | R3–25 | 17 / 18 / 30 | R3–12 |
| Düzenli Üretim | R8–30 | 19 / 22 / 41 | R6–14 |
| Patlayıcı / Tornado / Bumerang / Elektrik | R10–40 | 10–14 / 15–24 / 26–32 | R6–18 |
| Kesim Tekniği | R10–30 | 23 / 26 / 36 | R6–14 |
| Geniş Süpürüş | R10–30 | 23 / 27 / 38 | R8–18 |
| Güçlü Kesim | R25–60 | 42 / – / – | R12–26 |
| Kritik Odak | R20–65 | 32 / 37 / – | R15–28 |
| Akıcı Kesim 1 | R40–70 | 44 / – / – | R14–24 |
| Akıcı dalları (Hızlı Şarj, Geniş Kesim, Seri Ekim) | yok | yok | R18–32 |
| Verimli Üretim, Kesim Ustalığı | R35–75 | 45+ / – / – | R18–32 |
| Hasat Rekoru | R95–110 | – | R22–36 |
| Yıldırım Kesim (azaltılmış) | R65–100 | – | R34–44 |
| Geç hasar kapstonu | R75–110 | – | R40–50 |

"–" = 50 round içinde alınmadı. Bugünkü ağaçta deneyimli oyuncu bile 50 round'da Akıcı Kesim'in yalnız ilk node'unu, Yıldırım Kesim'i ve skor ailesini hiç görmüyor.

## 6. Gerçekten çalıştırılan testler
Hepsi izole kopyada (`Library/VerificationProject`), Bölüm 2.2 kodunun son hâliyle çalıştı. Son koşudan önceki değişiklikler yalnız test dosyasındaydı: üretim noktası sırası ve B'nin sabit referansı.

| Test | Sonuç |
|---|---|
| **SpeedElectricVerification** (yeni) | 72 / 72. Son koşu 8 seed, 472 round; önceki iki koşu 4'er seed, ikisi de 72 / 72 |
| SpecializationVerification | 100 / 100 |
| RunPrototypeVerification | 69 / 69 |
| MechanicsVerification | 83 / 83 |
| RoundPreviewVerification | 47 / 47 |
| HarvestBehaviorVerification | 111 / 111 |
| SimpleResonanceVerification | 291 / 291 |
| EconomyAnalyzerVerification | 448 / 448 |
| `RunSimulator.RunSpeedPowerBatch` | ölçüm (geç/kal yok): güç çizelgesi, node erişimi, süre A / B30 / B45 |

**SpeedElectricVerification'ın işlevsel kontrolleri** (Deney22_20 profili, gerçek `PlayerController.AttackInRadius` ve elektrik yolu):
- Yük aktif süreyle ve şansla orantılı dolar; en fazla bir yük saklanır.
- Yük yokken vuruş elektrik vermez.
- Öldürmeden boşalır; davranış (elektrik) hasarı başka saksının yükünü harcamaz.
- 2×2 saksının dört bitkisine tek saldırı = tek yük. Tek saldırıda iki hazır saksı = iki ayrı yük.
- Yük modunda yük olmadan öldürme elektrik vermez.
- A modunda eski kural çalışır; mod değişince yükler silinir.
- Hazır yük + şans 1 ile öldürme = tam bir elektrik.
- Davranış katsayısı bir kez uygulanır (8 × 1,25 = 10). Aynı öldürmeler iki modda aynı XP ve skoru verir.
- Efekt havuzu doluyken çizim atlanır, hasar uygulanır.
- Satışta ve tile kaldırılınca yük silinir; erişim geri gelince sıfırdan başlar.
- Round sonunda, round arasında ve kart ekranında dolmaz; yeni round sıfırdan başlar.
- Süre B: +60 sn yükseltmeye rağmen 45 sn, tempo yok. Süre node'ları satın alınamaz; hız zinciri süre node'u olmadan açılır.
- Süre A: 90 sn → tempo 1,5.
- Hızlı Şarj yalnız dolum süresini değiştirir (10 → 7,5 sn); saldırı aralığı değişmez.
- Tempo 1,5 dolumu 1,5 kat hızlandırır.
- Yeni run'da (Uzmanlasma20) yük kapalı, eski tetik çalışır, yük durumu boş.
- Diğer dört profilde deney alanları varsayılanda.
- Güç haritası asset'lerden okunur (hız zinciri 0,2 sn, süre 90 sn, 12 süre node'u).

**Çalıştırılmayanlar ve açık kalanlar:**
- Açık editörde elle oynanmadı; halka gösterimi batch'te çizildi ama ekran görüntüsüyle kontrol edilmedi.
- Son koşunun birebir tekrar ettiği ayrıca koşturulmadı.
- İnsan testi yok.

## 7. Hangi fikir korunmalı, hangisi değişmeli ya da bırakılmalı?

### 7.1 Sorulara cevap
1. **Doğrudan hasat düzeninde hız yatırımı değerli kalıyor mu?** Evet; bu durumlarda tek anlamlı yatırım o.
   - +%10 saldırı sıklığı hasatı +%8–13 artırıyor. İlk hız kademeleri 96 G'ye +%13 getiriyor.
   - Hasar ve üretim etkisiz: hasar doygun, üretim vuruş temposuna kilitli.
2. **Elektrik düzeninde aynı bütçeyi şarja vermek bazı koşullarda daha anlamlı mı?** Evet, ama yalnız yük modunda ve büyük elektrik tarlasında.
   - R20'de 1 737 G ile Hızlı Şarj hasatı +%15, hız +%6 artırıyor (3 örneğin 2'sinde).
   - R10'da hız açık ara iyi; R15'te eşit.
   - Mevcut kuralda (A) şarj yatırımı diye bir şey yok.
3. **Elektrik için daha fazla hız almak hâlâ otomatik en iyi seçim mi?**
   - **A'da evet:** elektrik öldürmeyle çoğaldığı için hız hem hasadı hem elektriği büyütüyor.
   - **B'de hayır:** hız elektriği ancak +%0–13 artırıyor. Seçim tarla büyüklüğüne ve fiyata bağlı.
4. **Tek vuruş doygunluğu nerede oluşuyor?**
   - Deneyimli ve orta oyuncuda Keskin Başlangıç 4 ile yaklaşık R7'de (61 hasar). Common için R50'ye kadar, Legendary için R10–12'ye kadar sürüyor.
   - R10–R20 durumlarında ölçülen tek vuruş oranı %98–100.
   - Yeni oyuncuda R10'da 40 run'ın 17'si.
5. **Süre yükseltmeleri diğer yatırımların getirisini ne kadar büyütüyor?** Doğrusal çarpan olarak.
   - +15 sn (Biraz Daha Zaman, 280 G) her şeyi +%50 artırıyor. 45 → 90 sn (tempo dahil) her şeyi ×2,1 yapıyor.
   - Bu, diğer bütün yatırımların getirisini aynı oranda büyütüyor. Süre alınmadan hiçbir yatırım aynı değeri veremiyor.

### 7.2 Korunmalı
- **Hız sisteminin erken kısmı:** taban 3 sn aralık, Hızlı Eller, Akıcı Kesim 1. Erken oynanabilirliği bunlar sağlıyor ve doğrudan build'de doğru biçimde değerli.
- **Yük biriktiren elektrik bir yön olarak.** Elektriği saldırı hızından ayırıyor: B'de hız elektriği neredeyse büyütmüyor.
- **Hızlı Şarj (aday).** Elektrik build'ine hızdan farklı bir yatırım veriyor ve büyük tarlada hızı geçiyor. Önce deney yapılandırmasında fiyatlanmalı; ana ağaca ekleme bu pakette yapılmadı.
- **Yük mekaniğinin sınırları:** tek yük, saksı başına tek tüketim, davranışla boşalmama, eski tetikle çakışmama. Testler bunları doğruluyor.

### 7.3 Değişmeli
- **Süre profile taşınmalı.** Süre, satın alınabilir bir seçim değil, her şeyin çarpanı; A ile B30'un saniye başına skoru aynı.
  - 50 round için tek bir profil süresi seçilmeli (öneri 45 sn).
  - Biraz Daha Zaman, Uzun Hasat ve Son Vardiya kaldırılmalı ya da dönüştürülmeli; tempo dönüşümü bırakılmalı.
  - Hızlı Eller önkoşulu Biraz Daha Zaman'dan çıkarılmalı.
- **B'nin dolum ayarı kararı.** Sıklığa eşitlenen 14,4 sn, isabet gerektiği için elektriği %39–73 azaltıyor. Tasarım hedefi seçilmeli: "A kadar elektrik" mi, "daha az ama kontrollü elektrik" mi?
  - Kısa süreye çekmek sırf güçlendirmek olur. Önce insan testiyle oyuncunun hazır saksıyı ne kadar gördüğü ölçülmeli.
  - Sonraki aday Yük Rezervi (2 yük) bu kaybı azaltabilir; uygulanmadı.
- **Erken hasar doygunluğu.** 177 G'lik Keskin Başlangıç R7'den R30'a kadar bütün hasar yatırımlarını boşa çıkarıyor. 50 round HP eğrisiyle birlikte yeniden ayarlanmalı.
- **Davranış kilitleri ve skor yatırımı erkene çekilmeli.** Elektrik simülatörde R14–30'da açılıyor; skor yatırımı (Hasat Rekoru) R95–110'da.
- **Akıcı Kesim 2–4 ve Yıldırım Kesim** tekrar eden genel hız olmaktan çıkıp dal seçimine dönmeli (bölüm 4).

### 7.4 Bırakılmalı
- 50 round'da Aşırı Güç ×8 / ×9.
- 0,2 sn'lik Yıldırım Kesim hedefi.
- 60 sn üstü süreyi tempoya çeviren kural.

### 7.5 Kanıtlanmamış varsayımlar
- Durumlar tek bir simülatör seed'inden geliyor. Elektrik düzeni tile çevirerek kuruldu.
- Bot açgözlü; B'deki hedefleme bonusu bir varsayım.
- Hızlı Şarj fiyatı varsayım.
- Skor farkları 8 seed'de çok gürültülü. Önerilen 50 round erişim aralıkları ölçülmedi.

**İnsan testi önerisi:**
1. Deney 2.2 profilinde 2 oyuncu, A ve B modlarının her biriyle 2'şer run oynasın.
2. Round 10, 15 ve 20'de not alınsın: saldırı aralığı, hasar, elektrik tile sayısı, round başına elektrik.
3. Oyuncuya sorulsun: dolu halkayı görüp o saksıya yöneldi mi?
