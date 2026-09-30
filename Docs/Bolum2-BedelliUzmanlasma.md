# Bölüm 2 — Boss sonrası bedelli uzmanlaşma (uygulama notu)

Tarih: 30 Eylül 2026. Bölüm 1 altyapısı (run profili, kota, Don Cephesi) korunarak eklendi.
Katsayılar ve kota değerleri **test başlangıç değeridir**; ölçümler seçeneklerin dengeli olmadığını gösteriyor (bölüm 5).

## 1. Akış
1. Round 10 biter: kota değerlendirilir.
2. Kota tutmazsa run biter, ödül yoktur.
3. Tutarsa uzmanlaşma "bekler". Önce bekleyen kart seçimleri, sonra uzmanlaşma ekranı (`RoundChoice` durumu), en son round özeti / hazırlık gelir.
4. Seçim bitmeden round 11 başlatılamaz (`RoundManager.StartNextRound` reddeder).
5. Seçim tek seferdir: çift tıklama, panelin yeniden açılması ya da ikinci istek reddedilir. Run başına tek seçim vardır, sonradan değiştirilemez.
6. Etki round 11'den itibaren geçerlidir. Yeni run'da (`OnRunStarted`), ana menüde ve yok edilmede (yeniden başlatma, sahne değişimi) aynı `ClearAll` ile temizlenir.
7. Run'ın son segmentinde ve uzmanlaşması tanımlı olmayan profillerde (10 round prototip, uzun run) ekran açılmaz.
8. Seçilen uzmanlaşma sonradan görülebilir: round HUD'u, round özeti ve run sonu ekranı.

## 2. Mimari
- **Veri:** `SpecializationSO` (`Assets/ScriptableObjects/Specializations/`): ad, doğrudan ve davranış hasar katsayısı. Kart metinleri katsayılardan otomatik yazılır.
- **Profil:** `RunProfileSO.specializationAfterSegment` ve `specializationOptions`.
- **Kural:** `SpecializationManager` (RoundManager kurar). Bekleme, seçilebilirlik, tek sefer uygulama ve temizlik burada. `IRoundChoice` arayüzüyle RoundManager'a "bekleyen seçim var" der; RoundManager ödül türünü bilmez.
- **Arayüz:** `SpecializationPanelUI` yalnız gösterir ve `Choose` ister. `UIManager` onu `RoundChoice` durumunda açar.
- `GameStates.RoundChoice` enum'un sonuna eklendi; önceki değerlerin sırası değişmedi.

## 3. Hasar akışı ve katsayıların yeri
| Hasar | Taban | Uzmanlaşma katsayısı nerede | Sonra |
|---|---|---|---|
| Doğrudan | `HarvestDamage` × sapma | `PlayerController`: sapmayla aynı çarpımda, tek yuvarlama | kritik × ; saksı bonusu (Odak) |
| Patlama | `HarvestDamage` | `PlanterBrain.BehaviorDamageMultiplier` (rezonansla aynı çarpım) | — |
| Kasırga | `HarvestDamage` × 0,5 | `TornadoManager` → `BehaviorDamageMultiplier` | — |
| Bumerang | `HarvestDamage` × 0,65 | `GetBehaviorDamage` → `BehaviorDamageMultiplier` | — |
| Elektrik | `HarvestDamage` | `GetBehaviorDamage` → `BehaviorDamageMultiplier` | — |

- Davranışların tabanı doğrudan vuruşun sonucu değil, ortak `HarvestDamage` stat'ıdır. Bu yüzden doğrudan cezası davranış tabanına sızmaz. Testte Davranış Ustası'yla patlama 125 (100 × 1,25) oldu.
- Skill ağacı hasarı iki tarafı da beslemeye devam eder. Kritik, saksı ve rezonans kuralları değişmedi.
- Gösterilen hasar sayıları katsayı uygulanmış gerçek değerden hesaplanır.

## 4. Profil ve ayarlar
- `Tools > Run Profili > Uzmanlaşma testi · 20 round (normal ekonomi)`
- `Uzmanlasma20`: 20 round; kota 40 / 200 / 350 / 600; Don Cephesi 2. ve 4. segmentte; uzmanlaşma 2. segmentten sonra; 80 Gold.
- Katsayılar: `UstaBicici.asset` (doğrudan 1,25 / davranış 0,85), `DavranisUstasi.asset` (0,85 / 1,25), `MevcutDuzeniKoru.asset` (1 / 1).

## 5. Ölçüm (izole kopya, GameScene, bot oyuncu)
**Varsayım:**
- Round 12 bitki canı.
- Hasat hasarı 6, saldırı 0,8 sn, alan 1,6, taban kritik %30 × 2.
- 5×5 alanda 25 adet 1×1 saksı.
- Bot her saldırıda en çok canlı bitkiyi kapsayan hücreye vurur.
- Her yapılandırma 3 round × 30 sn, aynı seed'ler.

Skor nadirlik yüzünden gürültülü olduğundan ana ölçü öldürme sayısıdır (XP = öldürme × 20).

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

"Aday" yalnız ölçüm için denendi, oyuna asset olarak eklenmedi. Hiçbir düzende kapasite yüzünden tetik atlanmadı; elektrik görseli de atlanmadı.

**Simülatör** (`RunSimulator.RunSpecializationBatch`, 30 seed): 11–20. round'larda üç seçenek neredeyse aynı segment skorunu veriyor; orta profilde değerler birebir aynı. Round 15'te davranış hasat payı orta ve yeni profilde %0, deneyimlide en iyi dilimde %9. Simülatör davranış payını düşük modelliyor; bu yüzden asıl kanıt yukarıdaki sahne ölçümü.

## 6. Sonuç ve açık sorunlar
1. **×1,25 / ×0,85 ile seçenekler dengeli değil.** Usta Biçici üç düzende de en çok öldürmeyi yapıyor (Koru'ya göre +%8 ile +%24). Davranış Ustası üç düzende de en az (Koru'ya göre −%10 ile −%15). Aday ×1,5 / ×0,95 de Koru'yu geçemedi.
2. **Neden yapısal:**
   - Davranışlar yalnız doğrudan öldürmeyle tetikleniyor. Doğrudan cezası tetik sayısını da düşürüyor, doğrudan bonusu artırıyor.
   - Bu evrede davranış öldürmeleri, davranışlı tarlalarda toplamın yalnız %9–25'i. Davranış katsayısının büyüteceği pay küçük.
   - Tam sayı hasar ve can eşikleri etkiyi sıçramalı yapıyor. Örneğin 6 × 0,85 ≈ 5 hasarla Common bitki 3 yerine 4 vuruşta ölüyor.
3. **Bu yüzden katsayıları değiştirmedim.** İstenen başlangıç değerleri duruyor; denenen aday da sorunu çözmedi, sorun sayıdan çok yapıdan geliyor. Sonraki deney adayları:
   - Davranış Ustası'nın bedelini tetikleyici doğrudan vuruştan başka bir yere taşımak.
   - Seçimi davranışların yaygınlaştığı daha geç bir boss'a koymak.
   - Davranış Ustası'na tetik şansı eklemek.

   Karar oyun testine bağlı.
4. **Bot bir insan değil.** En yoğun hücreyi seçen bir bot; gerçek imleç davranışı farklı olabilir.
5. **Kota 40 / 200 / 350 / 600 test değeri.** Simülatörde orta ve deneyimli profiller rahat geçiyor, yeni profilde 30 run'ın 13'ü round 10'u geçiyor, 8–9'u kazanıyor.
6. **Bumerang ve kasırga sınırları** (sahnede 12 / 10) bu ölçümde darboğaz değildi; daha yoğun tarlada yeniden bakılmalı.

## 7. Doğrulama
| Test | Sonuç |
|---|---|
| SpecializationVerification (yeni) | 56 / 56 |
| RunPrototypeVerification | 69 / 69 |
| MechanicsVerification | 83 / 83 (izole kopyada artık kendi profilini seçiyor) |
| RoundPreviewVerification | 47 / 47 |
| HarvestBehaviorVerification | 111 / 111 |
| SimpleResonanceVerification | 291 / 291 |
| EconomyAnalyzerVerification | 448 / 448 |
