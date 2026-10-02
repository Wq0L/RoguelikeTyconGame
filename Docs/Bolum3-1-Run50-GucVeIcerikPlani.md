# Bölüm 3.1 — 50 round güç haritası ve içerik planı

30 Eylül 2026. Bu bölüm yalnız inceleme ve plan içerir. Oyun kodu ve asset değerleri değişmedi.
Tek ekleme ölçüm aracına yapıldı: `RunSimulator.RunNodeAccessBatch` ve simülatörde düğümün ilk kademe round'unun kaydı (`NodeFirst`).
Tasarım yönü: [Run50-GuncelTasarimKararlari-20260930.md](Run50-GuncelTasarimKararlari-20260930.md).
Etiketler: "doğrulandı" = kod ya da asset'te görüldü; "simülatör" = simülatör ölçümü, insan verisi değil; "öneri" = henüz karar değil.

## 0. Bölüm 3.0 durumu
Elektrik geri dönüşü doğrulandı. İzole kopyada, geri dönüş kodunun son hâliyle şu testler geçti:

| Test | Sonuç |
|---|---|
| ElectricRevertVerification (yeni) | 40 / 40 |
| HarvestBehaviorVerification | 111 / 111 |
| SimpleResonanceVerification | 291 / 291 |
| SpecializationVerification | 100 / 100 |
| RunPrototypeVerification | 69 / 69 |
| MechanicsVerification | 83 / 83 |
| RoundPreviewVerification | 47 / 47 |
| EconomyAnalyzerVerification | 448 / 448 |

- Oyun kodu bu testlerden sonra değişmedi.
- Doğrulanamayan tek görsel nokta nadirlik aurası: `PlantRarityAura.Apply` batch modda hiç çalışmıyor. Aura ancak Play'de görülebilir.

## 1. Kodda doğrulanan mevcut durum

| Konu | Doğrulanan durum | Kaynak |
|---|---|---|
| Stat birleşimi | Flat toplanır; AddPercent toplanıp (1+Σ) ile çarpar; MorePercent'ler ayrı ayrı çarpar; Set tabanı değiştirir. Bir skill düğümünün yeni kademesi eskisinin **yerine** geçer. | `StatCalculator.CalculateRaw`, `SkillTreeManager.ApplyLevel` |
| Sınırlar | Saldırı aralığı ≥0,05; oyuncu max(aralık, 0,1)/tempo kullanır. Üretim aralığı ≥0,5 sn, fazlası nadirliğe döner. Şanslar 0–1. Nadir şansı ≤95 puan. | `StatCalculator.ClampStat`, `PlayerController` |
| Süre ve tempo | Taban 30 sn, stat ile 30–90 sn. 60 sn üstü tempo = süre/60 (≤1,5); saldırı ve üretim aralığını böler. Profil süreyi sabitleyebilir (`fixedRoundDuration`, yalnız Deney22_20). | `RoundManager` |
| Davranış tetiği | Patlama, kasırga, bumerang ve elektrik yalnız **doğrudan öldürmede** tetiklenir; davranış öldürmeleri zincir yapmaz. | `DamageTypeRules.CanTriggerBehaviors`, `PlanterBrain.TriggerHarvestBehaviors` |
| Davranış hasarı | `HarvestDamage` (kasırga ×0,5, bumerang ×0,65) × rezonans × uzmanlaşma, bir kez. | `PlanterBrain.BehaviorDamageMultiplier` |
| Havuz sınırları (sahne) | Kasırga 10, bumerang 12, elektrik görseli 12. Elektrik havuzu dolunca yalnız çizim atlanır. | GameScene, `HarvestBehaviorManager` |
| Rezonans | 10 tarif; yalnız aynı saksının benzersiz hücreleri sayılır. Etki türleri: stat, kaynak, davranış hasarı, nadir skor, elektrik XP. | `ResonanceRules.asset`, SimpleResonance.md |
| Kart | Level başına 3 kartlık bir ekran, 1 kart seçilir. Tile kartı **rastgele** boş hücreye düşer. Grid dolunca kartlar tile seviyesi verir; bütün tile'lar max olunca "Temel güç" (+%2–5 kalıcı) verir. Round başına 1 kart atlama (+`CardSkip`). | `CardSelectionUI`, `ProgressionManager` |
| Tile seviyesi | **Var ve nadirlikten ayrı:** her tile 0–3 seviye, seviye başına zar değerine +%25 (çarpan (1+0,25·Sv)). Kart nadirliği kaç seviye verdiğini belirler (Common/Rare 1, Epic 2, Legendary 3). | `GroundCell.AddLevels` |
| Etkisiz statlar | `StartingGoldBonus`, `StartingPlanterCount`, `ExtraCardChoice` ve **`RerollCard` tanımlı ama hiçbir kod okumuyor.** Kartta reroll düğmesi yok. | `StatType`, grep |
| Kalıcı kayıt | Yalnız ayarlar `PlayerPrefs`'te. Çiftçi, tırpan, görev, hesap ilerlemesi yok. `UnlockManager` run'a özel ve her run sıfırlanır. | `GameSettings`, `UnlockManager` |
| Boss (segment olayı) | Olay **bütün segment** aktif (ör. Don 6–10) ve bir segment önce duyurulur. Kota segmentin son round'unda değerlendirilir. Tek run ödülü uzmanlaşma (2. segment sonu, `IRoundChoice` akışı). | `SegmentEventRuntime`, `SegmentEventDirector`, `RoundManager.EndRound` |
| Can eğrisi | Common canı çapaları: R1 5 · R10 15 · R25 35 · R45 80 · R65 180 · R80 1 500 · R95 9 000 · R110 45 000 · R130 180 000. Nadirlik çarpanları ×1 / 1,2 / 1,6 / 2 / 3; basamaklar doğrusal değil. | `PlantHealthScaling.asset` |

## 2. Mevcut güç haritası

### 2.1 Aile özeti (asset'ten; erişim: simülatör)
- Kaynak ve kapsam: 140 düğüm, 46 aile. Tam liste Ek A'da.
- Toplam etki, ailenin bütün düğümlerinin son kademesinin birleşimidir. Hız ve üretim satırları aralık çarpanı olduğu için küçük = hızlı.
- Erişim sütunu: `RunNodeAccessBatch`, mevcut kural, 50 round, kota yok, 40 seed.
  - D/O/Y = deneyimli / orta / yeni oyuncu profili; her hücre "ilk kademe / aile tamam" round'u (P50).
  - "–" = seed'lerin yarısından azı 50 round içinde aldı.
  - Bu bir simülatör ölçümü; insan oyununu göstermez.

| Aile (düğüm) | Rol | Toplam etki (tam aile) | Toplam fiyat | Hedef round | Önkoşul (ilk düğüm) | Simülatör: ilk kademe / tamam (D·O·Y, P50) |
|---|---|---|---|---|---|---|
| Altın Hasat I (4) | Ekonomi | Gold +%50 | 280G | R1–20 | yok | D 8/10 · O 7/10 · Y 6/25 |
| Biraz Daha Zaman (4) | Süre/tempo | süre +15 | 280G | R1–15 | yok | D 4/8 · O 2/8 · Y 5/26 |
| Keskin Başlangıç (4) | Hasar | hasar +60 | 177G | R1–15 | yok | D 2/7 · O 1/6 · Y 2/21 |
| Hızlı Eller (4) | Hız | saldırı aralığı ×0.7 | 399G | R3–25 | Biraz Daha Zaman - 1@3 | D 8/17 · O 9/18 · Y 12/30 |
| Grid Genişleme I (1) | Alan (grid) | grid = 7 | 80G + 40I | R4–18 | yok | D 10/10 · O 10/10 · Y 19/20 |
| 1×3 Saksı (1) | Kilit: 1×3 saksı | — | 40G | R5–7 | Grid Genişleme I@1 | D 10/10 · O 15/15 · Y 24/24 |
| Düzenli Üretim (4) | Üretim | üretim aralığı ×0.85 | 799G | R8–30 | Altın Hasat I - 1@3 | D 17/19 · O 17/22 · Y 18/41 |
| 2×2 Saksı (1) | Kilit: 2×2 saksı | — | 100G | R10–13 | 1×3 Saksı@1, Grid Genişleme I@1 | D 15/15 · O 16/16 · Y 31/31 |
| Geniş Süpürüş (4) | Alan | alan +%30 | 720G | R10–30 | Hızlı Eller - 1@3 | D 18/23 · O 19/27 · Y 20/38 |
| Kesim Tekniği (4) | Hasar | hasar +%50 | 600G | R10–30 | Keskin Başlangıç - 1@3 | D 17/23 · O 18/26 · Y 13/36 |
| Patlayıcı Kartlar (1) | Kilit: Patlama kartları | — | 20I | R10–20 | 1×3 Saksı@1 | D 10/10 · O 15/15 · Y 26/26 |
| İlk Vazgeçiş (1) | Kart | kart atlama +1 | 240G | R10–25 | Geniş Süpürüş - 1@3 | D 21/21 · O 23/23 · Y 43/43 |
| Demir Hasat (4) | Ekonomi | Iron +%100 | 1.120G | R15–45 | Altın Hasat I - 1@3 | D 24/28 · O 27/32 · Y 21/47 |
| Hasat Deneyimi I (4) | XP | XP +%50 | 881G | R15–40 | Düzenli Üretim - 1@3 | D 20/25 · O 23/29 · Y 27/44 |
| Çifte Hasat Kartları (1) | Kilit: Çifte hasat kartları | — | 35I | R15–25 | 2×2 Saksı@1 | D 15/15 · O 16/16 · Y 32/32 |
| 2×3 Saksı (1) | Kilit: 2×3 saksı | — | 180G | R16–20 | 2×2 Saksı@1, Grid Genişleme I@2 | D 18/18 · O 18/18 · Y 40/40 |
| Kritik Odak (4) | Hasar (kritik) | kritik şansı +0.15 | 1.602G | R20–65 | Kesim Tekniği - 1@3 | D 26/32 · O 30/37 · Y 28/– |
| Tornado Kartları (1) | Kilit: Kasırga kartları | — | 40I | R20–30 | Patlayıcı Kartlar@1 | D 10/10 · O 15/15 · Y 30/30 |
| Bumerang Orak Kartları (1) | Kilit: Bumerang kartları | — | 60I | R25–40 | Tornado Kartları@1 | D 12/12 · O 20/20 · Y 32/32 |
| Güçlü Kesim (4) | Hasar | hasar +150 | 4.500G | R25–60 | Keskin Başlangıç - 1@3 | D 30/42 · O 35/– · Y 41/– |
| Çapraz Elektrik Kartları (1) | Kilit: Elektrik kartları | — | 80I | R25–40 | Patlayıcı Kartlar@1 | D 14/14 · O 24/24 · Y 30/30 |
| Altın Hasat II (4) | Ekonomi | Gold +%250 | 14.400G | R30–65 | Demir Hasat - 1@3 | D 41/– · O –/– · Y –/– |
| Taş Hasat (4) | Ekonomi | Stone +%100 | 5.850I | R30–65 | Demir Hasat - 1@3 | D 26/48 · O 34/– · Y 46/– |
| Uzun Hasat (4) | Süre/tempo | süre +20 | 5.400I | R30–65 | Biraz Daha Zaman - 1@3 | D 20/47 · O 31/– · Y 39/– |
| İkinci Vazgeçiş (1) | Kart | kart atlama +1 | 1.800I | R30–65 | İlk Vazgeçiş@1 | D –/– · O –/– · Y –/– |
| Kesim Ustalığı (4) | Hasar | hasar +%100 | 5.400I | R35–65 | Kesim Tekniği - 1@3, Güçlü Kesim - 1@3 | D 34/– · O 39/– · Y –/– |
| Akıcı Kesim (4) | Hız | saldırı aralığı ×0.7 | 9.000G | R40–70 | Hızlı Eller - 1@3 | D 38/– · O 42/– · Y 50/– |
| Kart Sezgisi (4) | Şans (kart) | kart nadirliği +0.3 | 22.500G | R40–100 | İkinci Vazgeçiş@1 | D –/– · O –/– · Y –/– |
| Grid Genişleme II (1) | Alan (grid) | grid = 11 | 570S | R45–85 | Grid Genişleme I@2 | D 23/44 · O 35/– · Y –/– |
| Verimli Üretim (4) | Üretim | üretim aralığı ×0.85 | 5.850I | R45–75 | Düzenli Üretim - 1@3 | D 23/– · O 38/– · Y –/– |
| Nadir Filizler (4) | Nadirlik | nadir şansı +10 | 10.800I | R50–95 | Verimli Üretim - 1@3 | D –/– · O –/– · Y –/– |
| Ağır Kesim (4) | Hasar | hasar +1000 | 3.000G | R65–78 | Güçlü Kesim - 1@3 | D 33/– · O 39/– · Y –/– |
| Kritik Güç (4) | Hasar (kritik) | kritik çarpanı +2 | 4.500I | R65–100 | Kritik Odak - 1@3 | D 30/– · O 33/– · Y 42/– |
| Seri Üretim (3) | Üretim | üretim aralığı ×0.138 | 24.000I | R65–100 | Verimli Üretim - 4@2 | D –/– · O –/– · Y –/– |
| Yıldırım Kesim (3) | Hız | saldırı aralığı ×0.136 | 30.000G | R65–100 | Akıcı Kesim - 4@2 | D –/– · O –/– · Y –/– |
| Geniş Tarama (4) | Alan | alan +%30 | 1.800S | R70–100 | Geniş Süpürüş - 1@3, Akıcı Kesim - 1@3 | D 45/50 · O –/– · Y –/– |
| Zengin Cevher (4) | Ekonomi | Iron +%300; Stone +%300 | 2.250S | R70–100 | Taş Hasat - 1@3 | D 35/– · O 44/– · Y –/– |
| Son Vardiya (4) | Süre/tempo | süre +25 | 2.700S | R75–100 | Uzun Hasat - 1@3 | D 31/– · O 45/– · Y –/– |
| İleri Ustalık (4) | Hasar | hasar +%150 | 24.000G | R75–95 | Kesim Ustalığı - 1@3, Ağır Kesim - 1@3 | D –/– · O –/– · Y –/– |
| Plazma Kesim (4) | Hasar | hasar +4000 | 15.001G | R78–93 | Ağır Kesim - 1@3 | D –/– · O –/– · Y –/– |
| Hasat Deneyimi II (4) | XP | XP +%150 | 9.000G | R80–105 | Hasat Deneyimi I - 1@3 | D –/– · O –/– · Y –/– |
| Aşırı Güç I (1) | Hasar | hasar ×8 | 1.500S | R90–100 | Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– |
| Hasat Zirvesi (4) | Hasar | hasar +%200 | 18.000G | R90–110 | İleri Ustalık - 1@3, Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– |
| Yıldız Kesim (4) | Hasar | hasar +2200 | 33.000I | R90–105 | Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– |
| Hasat Rekoru (4) | Skor | skor +%100 | 6.000I | R95–110 | Zengin Cevher - 1@3 | D 46/– · O –/– · Y –/– |
| Aşırı Güç II (1) | Hasar | hasar ×9 | 7.500S | R100–110 | Aşırı Güç I@1, Yıldız Kesim - 1@3 | D –/– · O –/– · Y –/– |

### 2.2 Büyük sıçramalar, baskınlık ve çarpışmalar
Hasar değerleri deneyimli profilin simülatör P50'si; can bölüm 1'deki tablodan.

1. **Erken hasar doygunluğu:**
   - Keskin Başlangıç 177 G'ye +60 düz hasar veriyor; simülatörde deneyimli oyuncu R2–7'de alıyor.
   - Hasar R10'da 61. En düşük sapmayla (×0,85) bile R10'daki bütün nadirlikleri ve R33'e kadar Common'ı tek vuruşta kesiyor.
   - Bölüm 2.2 sahne ölçümü (bot): R10–R20'de tek vuruş oranı %98–100; +%10 hasarın etkisi ≈ 0.
   - Sonuç: Kesim Tekniği ve Güçlü Kesim R10–30'da ölçülebilir getiri vermiyor.
2. **Hasar merdiveni:**
   - Ağır Kesim 1'in ilk kademesi 146 G'ye +113, tam düğüm +333 düz hasar veriyor; simülatör P50 R33.
   - Hasar R30'da 92, R40'ta 743 (P10–P90: 497–1 151), R50'de 1 817.
   - Plazma (+4 000), Yıldız (+2 200), Aşırı Güç ×8 ve ×9 ile tam ağaç yaklaşık 3,2 milyon hasar.
   - R50 Legendary canı 294. 50 round'da R40 sonrası hasar yatırımlarının hepsi fazla.
3. **Hız:**
   - Hızlı Eller 399 G ile 3 → 2,1 sn; simülatörde R8–17.
   - Akıcı Kesim 9 000 G ile 2,1 → 1,47 sn; deneyimli R38'de başlıyor.
   - Yıldırım Kesim 30 000 G ile ×0,136; 50 round'da alınmıyor.
   - Bölüm 2.2 ölçümü (bot): hasar doygunken **tek anlamlı alım hız** (+%10 saldırı sıklığı → +%8–13 hasat).
4. **Süre bütün yatırımların çarpanı:**
   - Biraz Daha Zaman 280 G ile +15 sn, yani her şeye +%50; R4–8'de alınıyor.
   - Uzun Hasat (5 400 I) ve Son Vardiya (2 700 S) tempo ile saldırıyı ve üretimi hızlandırıyor.
   - Bölüm 2.2 sahne ölçümü: hasat süreyle doğrusal (90 sn ≈ 45 sn'nin 2,1 katı).
   - Hız zinciri Biraz Daha Zaman 1'e bağlı; Uzun Hasat da aynı önkoşula bağlı.
5. **Davranışlar doğrudan hasada bağlı:**
   - Tetik yalnız doğrudan öldürmede. Saldırı hızı ve alan her davranış build'ini dolaylı olarak büyütüyor.
   - Hasar doygun olduğunda davranış hasarı yatırımı da boşa gidiyor (Bölüm 2.2: +%10 davranış hasarı = 0 fark).
   - Davranış kilitleri Iron istediği için elektrik simülatörde R14 (deneyimli) ile R30 (yeni) arasında açılıyor.
6. **Skor yatırımı çok geç:**
   - Kota skorla geçiliyor ama tek skor ailesi Hasat Rekoru (6 000 I, önkoşul Zengin Cevher). Simülatörde deneyimli oyuncu ilk kademeye R46'da ulaşıyor.
   - Erken skor ancak rezonansla (Şöhret, Değerli Hasat) geliyor.
7. **Ekonomi:**
   - Altın Hasat II (14 400 G) ve Zengin Cevher 50 round'da çoğunlukla alınmıyor.
   - Deneyimli oyuncu R40'tan sonra geliri hasar ve süreye çeviriyor.
8. **Kart ve şans:**
   - Kart Sezgisi 22 500 G ile 50 round'da erişilmiyor. İkinci Vazgeçiş 1 800 I ile hiç alınmıyor.
   - Reroll ağaçta yok ve kodda da yok.

### 2.3 Düğüm kararı eşleştirmesi (öneri; bu bölümde düğüm silinmedi ya da nerflenmedi)
| Karar | Aileler | Gerekçe |
|---|---|---|
| **Ağaçta temel güç olarak kalır** | Hızlı Eller · Kesim Tekniği · Düzenli Üretim · Altın Hasat I · Demir Hasat · Geniş Süpürüş · Kritik Odak · Hasat Deneyimi I · İlk Vazgeçiş · Grid Genişleme I · saksı kilitleri (1×3, 2×2, 2×3) · davranış kilitleri | Erken hayatta kalma, ilk build'i kurma ve davranışlara erişim |
| **Azaltılır ya da yeniden zamanlanır** | Güçlü Kesim · Kesim Ustalığı · Ağır Kesim · Akıcı Kesim · Verimli Üretim · Taş Hasat · Altın Hasat II · Zengin Cevher · Kritik Güç · Geniş Tarama · Grid Genişleme II · Hasat Rekoru (erkene) · Kart Sezgisi · İkinci Vazgeçiş · davranış kilidi fiyatları (erkene) | 130 round'a göre fiyatlandı; 50 round'da ya erişilmiyor ya da çok erken bütün eğriyi eziyor |
| **Gücün bir bölümü boss ödüllerine taşınır** | Aşırı Güç I–II · Yıldırım Kesim · Plazma Kesim · Yıldız Kesim · Hasat Zirvesi · İleri Ustalık · Seri Üretim · Hasat Deneyimi II · Nadir Filizler | Zirve gücü run'a özgü olmalı; bu düğümler bugün herkese aynı |
| **Ölçüm olmadan karar verilmez** | Keskin Başlangıç 3–4. kademe · süre aileleri (profile taşıma) · Kesim Tekniği değeri · davranış kilidi fiyatları · Grid Genişleme I fiyatı | Doygunluğu, erken erişimi ve toplam oturum süresini birlikte etkiliyor; can eğrisiyle birlikte ölçülmeli |

### 2.4 Ağaçtan boss ödüllerine güç aktarımı (öneri)
Hiçbir azaltma, karşılığı olan ödül ve erişimi doğrulanmadan yapılmaz. Ödül adları bölüm 5.2'de.

| Ağaçta azalacak ya da çıkacak güç | Bugünkü etki | Önerilen ağaç hâli | Oyuncunun geri kazanacağı yer | Ne zaman |
|---|---|---|---|---|
| Aşırı Güç I–II | hasar ×8 ve ×9 | ağaçtan çıkar | Canavar Kesimi (nadir, ×1,5 bir kez) · Keskin Bıçak (×1,15, 3 kez) · Odak rezonansı | Keskin Bıçak R5'ten; Canavar Kesimi R30 sonrası |
| Plazma, Yıldız, Hasat Zirvesi, İleri Ustalık | +6 200 düz, +%350 | 2–3 geç düğüm, değerler R50 canına göre (Legendary 294) | Keskin Bıçak · Altın Hedef · Kritik Göz · Ağır Darbe | her boss durağı |
| Yıldırım Kesim | aralık ×0,136 (30 000 G) | tek düğüm, aralık ×0,8 | Hızlı Bilek (×0,9, 3 kez) · Seri Orak tırpanı | R5'ten |
| Akıcı Kesim 2–4 | aralık ×0,78 (2–4. düğüm birlikte; deneyimli run'ların %18–28'i R50'ye kadar tamamlıyor) | fiyatı düşer, R20–35'e çekilir; toplam etki küçülür | Hızlı Bilek · Geniş Savuruş | R10'dan |
| Seri Üretim | üretim ×0,138 | ×0,6 | Bereketli Toprak (×0,9, 3 kez) · Fidanlıkçı çiftçisi | R5'ten |
| Hasat Deneyimi II | XP +%150 | azalır, erkene | Bilgi Filizi (×1,25, 2 kez) · Âlim çiftçisi | R5'ten |
| Nadir Filizler | nadir +10 puan | erkene, küçülür | Nadir Tohum (+8 puan, 3 kez) · Seçici Yetiştirici | R10'dan |
| Kart Sezgisi | kart nadirliği +0,3 | küçülür | Şanslı El · Yeniden Çek | R10'dan |
| Uzun Hasat, Son Vardiya (süre) | +45 sn, tempo 1,5 | profil süresine taşınır | ödül olarak **verilmez**: süre bütün yatırımları çarpar; profil belirler | – |

## 3. Görevlerle açılan başlangıç sistemi: çiftçi ve tırpan

### 3.1 Mevcut altyapı
- Çiftçi, tırpan, görev ya da kalıcı ilerleme kodu **yok**. Yeniden kullanılabilir parçalar:
  - `RunProfileSO` (run ayarları).
  - Uzmanlaşma ve deney profillerindeki "run'a özel katsayı" deseni: `SpecializationManager.DirectMultiplier` / `BehaviorMultiplier`.
  - Global stat değiştiricileri: `StatManager.AddGlobalModifier`.
  - `UnlockManager` (run içi kilitler).
  - `IRoundChoice` seçim akışı; `GameSettings`'in `PlayerPrefs` deseni.
- `BoomerangScythe` bir davranış, oyuncunun tırpanı değil. İmleç orağı yalnız görsel (`HarvestCursorVisual`).

### 3.2 Gerekli altyapı (öneri)
| Parça | Öneri |
|---|---|
| İçerik tanımı | `FarmerSO` ve `ScytheSO`: kimlik (sabit string id), ad, açıklama, run başı etkileri (global stat değiştirici listesi, run kilitleri, başlangıç kaynağı), özel kural bayrakları, kilit koşulu referansı. Tırpan: yarıçap / hasar / aralık katsayıları; davranış katsayısı 1. |
| Görev tanımı | `QuestSO`: id, açıklama, sayaç türü (enum), hedef, kapsam ("tek run'da" / "toplam"), açtığı içerik id'si. |
| Kalıcı kayıt | `MetaSave` JSON, `Application.persistentDataPath/meta.json`. İçerik: sürüm, açılmış çiftçi ve tırpan id'leri, görev sayaçları, son seçim. `PlayerPrefs` yalnız ayarlar için kalır. |
| Uyumluluk | Bilinmeyen id'ler yok sayılır ve silinmez; eksik alan varsayılanla dolar. Sürüm artınca göç fonksiyonu çalışır; bozuk dosya yedeklenir, varsayılanla başlanır. |
| Güvenli varsayılan | Seçim geçersiz ya da kilitliyse Bahçıvan + Standart. Seçim ekranı yalnız açık içeriği seçtirir. |
| Seçim ekranı | Menü → "Yeni run" → çiftçi ve tırpan kartları (kilitlilerde görev metni) → başla. İlk sürümde MenuScene'de basit panel. |
| Run'a uygulama | `RunLoadout` (RoundManager kurar, SpecializationManager gibi). `OnRunStarted`'da etkileri uygular: global stat değiştiricileri, `UnlockManager.Unlock`, başlangıç kaynağı. Tırpan katsayıları `PlayerController`'da tek noktadan okunur. |
| Temizlik | `ResetTree` ve `ClearGlobalModifiers` zaten run başında ve menüde temizliyor. `RunLoadout.ClearAll` `OnRunStarted` / MainMenu / `OnDestroy`'da çalışır (uzmanlaşmadaki tek temizlik yolu deseni). |
| Görev ilerlemesi | Sayaçlar run içinde bellekte tutulur, run sonunda kayda yazılır. Kaynak olaylar: `PlantHealth.AnyHarvested` (tür ve nadirlik), `RoundManager.OnRoundEnded` (segment skoru, boss), `GameManager.CompleteRun` (Outcome), rezonans değişimi (`RefreshTileBuffs` sonucu), level, tek saldırıda öldürme sayısı (`PlayerController`'a olay). Kapanışta yarım run sayaçları kaydedilmez. |

### 3.3 Açık başlangıç önerisi
- **Başta açık:** 2 çiftçi (Bahçıvan, Tüccar) ve 2 tırpan (Standart, Dar Kesim), yani 4 kombinasyon. İlk seçim anlamlı, öğrenme yükü düşük.
- **Görevle açılır:** 6 çiftçi ve 2 tırpan.
- Görevler ilk başarılı sinerjilerle ulaşılabilir; zorunlu tekrar sayısına bağlanmaz.

### 3.4 8 çiftçi (genel yaklaşım)
| Çiftçi | Oyun tarzı | Avantaj | Bedel ya da vazgeçilen | Açılma (öneri) | Uygulanabilirlik |
|---|---|---|---|---|---|
| Bahçıvan | Referans | Yok; kıyas noktası | Yok | Açık | Mevcut |
| Tüccar | Ekonomiyle erken ağaç | Hasat kaynakları ×1,25 | Harvest Score ×0,9 | Açık | Mevcut: global stat |
| Seçici Yetiştirici | Aura'ya göre değerli hedef | Nadir şansı +15 puan | Üretim aralığı ×1,15 | Bir run'da 5 Legendary hasat et | Mevcut: `RareSpawnChance`, `PlantSpawnRate` |
| Elektrikçi | Erken davranış build'i | Elektrik kartları baştan açık; ilk kart ekranında en az bir elektrik kartı | Başlangıç 60 Gold (80 yerine) | Bir run'da elektrikle 100 bitki hasat et | Kilit mevcut (`UnlockManager`); "ilk teklifte garanti kart" **küçük yeni** |
| Fidanlıkçı | Çok küçük saksı, çok rezonans | 1–2 hücreli saksılarda üretim aralığı ×0,8 | 2×3 saksı kilidi +%50 fiyat | Aynı anda 3 farklı rezonans aktif | Saksı boyutuna bağlı değiştirici ve fiyat değiştirici **yeni** |
| Kâşif | Geniş tarla, geniş alan hasadı | 5×5 grid ile başlar | Saksılar +%20 fiyat | Bir run'da 9×9'a ulaş | Grid mevcut (`GridUnlockSize`); fiyat değiştirici **yeni** (Fidanlıkçı'yla ortak) |
| Âlim | XP ile geç güçlenme | XP ×1,3; ilk 5 round'da +1 kart atlama | Hasat kaynakları ×0,85 | Bir run'da level 25 | Mevcut: XP stat, `CardSkip`, Bölüm 2.1 kaynak ölçeği |
| Meydan Okuyan | Risk ve ödül | Boss ödül ekranında +1 seçenek ve +1 reroll | Boss kotaları ×1,2 | Bir run'da 5 boss'u %150 kota ile geç | Boss ödül sistemine bağlı; **yeni** (bölüm 5) |

### 3.5 4 tırpan (hasat biçimi)
Taslakla uyumlu, 2 açık ve 2 görevli. Davranış hasarı katsayısı hep 1; ortak ağaç hasarı davranışları beslemeye devam eder.

| Tırpan | Oyun tarzı | Avantaj | Bedel | Açılma | Uygulanabilirlik |
|---|---|---|---|---|---|
| Standart | Karma tarla | Mevcut alan ve saldırı | Uzmanlaşmış avantajı yok | Açık | Mevcut |
| Dar Kesim | Değerli ve sert bitkiyi seçerek kesmek (aura) | Doğrudan hasar ×1,4 | Yarıçap ×0,75 | Açık | `PlayerController`'da tırpan katsayısı **küçük yeni** (uzmanlaşma katsayısıyla aynı yer) |
| Geniş Orak | Çok sayıda yumuşak bitki, davranışları çok tetiklemek | Yarıçap ×1,2 | Doğrudan hasar ×0,8 | Tek saldırıda 6 bitki hasat et | Aynı; tek saldırı öldürme sayacı olayı **yeni** |
| Seri Orak | Sık vuruş, hızlı hedef değiştirme | Saldırı aralığı ×0,8 | Doğrudan hasar ×0,8 | Bir run'da tek round'da 60 saldırı | Aynı |

### 3.6 Kademeli doldurma
1. Altyapı + Bahçıvan/Tüccar + Standart/Dar Kesim + 2 görev (Seçici Yetiştirici, Geniş Orak). 32 kombinasyon bu pakette hedeflenmez.
2. Seçici Yetiştirici, Elektrikçi, Âlim ve Geniş Orak içerikleri ve görevleri; her biri için tek başına etki testi.
3. Fidanlıkçı ve Kâşif (fiyat ve saksı boyutu değiştiricisi), Seri Orak.
4. Meydan Okuyan (boss ödül sistemi bitince).
5. Kombinasyon ölçümü: 8 × 4, simülatör ve seçili sahne kurulumları; baskın ya da işlevsiz eşleşme raporu.

## 4. Beş round'da bir boss

### 4.1 Mevcut akış ile hedef akış
| | Mevcut (Uzmanlaşma20, Don) | Hedef (öneri) |
|---|---|---|
| Hazırlık / önizleme | Olay **bir önceki segmentte** duyurulur (ör. R1–5'te Don bilgisi) | Segmentin ilk 4 round'u (5k−4 … 5k−1) boss'un **önizleme** dönemi: kural, işaretli bölge ve ödül kategorisi görünür, kural uygulanmaz |
| Kural aktif | **Bütün segment** (Don R6–10, 5 round) | **Yalnız boss round'u** (5k). Bazı boss'lar için isteğe bağlı 2 round (5k−1, 5k), veri alanıyla |
| Kota kontrolü | Segmentin son round'u, round sonunda | Aynı: 5k round'unun sonunda, segment toplamı |
| Ödül | Yalnız 2. segment sonrası uzmanlaşma | Her boss geçilince (5 … 45): kartlar → boss ödülü seçimi → özet → sonraki segment alışverişi. 50 son boss: run buff'ı yok |

Kullanıcı kararı "5 round'da bir boss" olarak uygulanır. Kural "her round boss etkisi" hâline getirilmez.

### 4.2 Geçiş (öneri)
- `SegmentEventSO`'ya `activeRounds` (varsayılan 5; yeni boss'lar 1) ve `previewFromSegmentStart` alanları eklenir.
- `SegmentEventRuntime`: StartRound = EndRound − activeRounds + 1; AnnounceRound = segment başı.
- Mevcut Don asset'i geriye uyumluluk için 5'te kalır. Yeni 50 round profili 1 kullanır.
- `SegmentEventDirector` ve UI (HUD olay satırı, harita işaretleri, özet kartı) zaten "yaklaşan / aktif" ayrımını çiziyor; metinler "Boss round'u: R10" biçimine geçer.
- Ödül: uzmanlaşmadaki `IRoundChoice` akışı genelleştirilir. `BossRewardManager` her boss geçişinde bekleyen seçim açar; RoundManager yine ödül türünü bilmez.

### 4.3 Boss kural adayları (yalnız can veya kota çarpanı olmayanlar)
| Boss | Oyuncuya önceden verilen bilgi | Etkilediği darboğaz | Karşı hamle | Daha çok zorladığı build | Anlamsız ya da aşırı sert kaldığı koşul | Uygulama |
|---|---|---|---|---|---|---|
| Don Cephesi | İşaretli kenar şeridi, R(5k−4)'ten | Üretim | Güçlü saksıları şerit dışına kurmak, üretim yatırımı | Üretimi sınırda olan, alanı kalabalık düzen | Üretim fazlası olan düzende etkisiz (Run50 belgesi); küçük tarlada bütün alanı kaplamamalı | **Mevcut** (aktif pencere 1 round'a iner) |
| Sert Kabuk | İşaretli bölge; bölgede doğan bitkiler can ×1,5, skor ×1,3 | Hasat vuruş sayısı | Bölgeye hasar ya da crit, ya da kolay bölgeye odak | Tek vuruşa dayanan hız build'i | Hasar doygunsa etkisiz; çok düşük hasarda bölge terk edilir | Küçük yeni: doğum anı can/skor çarpanı |
| Solgun Tarla | Bitkiler doğduktan 6 sn sonra hasat edilmezse solar (skor ve kaynak yok) | Hedef seçme ve hız | Hız, alan, aura'ya göre öncelik | Yavaş ve yüksek hasarlı build | Hasat kapasitesi çok yüksekse etkisiz; üretim çok hızlıysa kaos | Yeni: bitki ömrü |
| Değerli Hasat Günü | Bu round kota skoru yalnız Rare+ bitkilerden (aura'lı) sayılır | Nadirlik ve hedef seçimi | Nadirlik yatırımı, Dar Kesim, aura'ya bakmak | Common spam'i yapan hız build'i | Nadirlik yatırımı yoksa çok sert; kotası buna göre ayrıca ayarlanmalı | Küçük yeni: skor filtresi |
| Statik Alan | İşaretli bölgede davranışlar tetiklenmez (doğrudan hasat serbest) | Davranış zinciri | Davranış saksılarını bölge dışına almak ya da doğrudan hasada dönmek | Davranış build'i | Bölge tarlanın yarısını geçmemeli; davranış yoksa etkisiz | Küçük yeni: tetik koşulu |
| Sis | Oyuncunun vuruş yarıçapı ×0,7 | Alan hasadı | Davranışlarla alan, hız | Geniş Orak ve alan build'i | Yarıçap tek hücreye düşmemeli (en az 1) | Mevcut stat (geçici değiştirici) |
| Ayrık Otu | İşaretli üretim noktalarında ödülsüz ot çıkar; kesilmeden yeni bitki gelmez | Üretim noktası ve imleç zamanı | Alan, davranış, ot bölgesini temizleme sırası | Az saksılı dar düzen | Bütün noktalar otla dolmamalı (≤%30) | Küçük yeni: ödülsüz PlantSO ve spawn geçersiz kılma |
| Çekirge | Sonraki boss round'unda kapanacak tile'lar gösterilir; o round etkisiz sayılır | Tile ve rezonans | Yerleşim ve tile çeşitliliği | Tek rezonansa dayanan düzen | Taşıma yoksa sert; taşıma gelmeden havuza girmez (taslakla aynı) | Yeni + taşıma sistemi gerekir |

**Seçim planı (öneri):**
- **Uygunluk:**
  - Statik Alan yalnız davranış kilidi açıkken gelir.
  - Değerli Hasat Günü yalnız 2×2 sonrası ve nadir şansı > 0 iken gelir.
  - Çekirge taşıma sistemi gelmeden havuza girmez.
  - Don ya da Ayrık Otu tarla en az 5×5 iken gelir.
- **Ağırlık ve tekrar:**
  - Temel ağırlık eşit.
  - Son 2 boss'u tekrar etmez; aynı kategoriden (üretim / hasat / davranış / alan) art arda en fazla 1.
- **Erken ve geç run:**
  - R5 ve R10'da yalnız öğretici ve yumuşak kurallar: Don, Sis, Sert Kabuk.
  - R15–45 tam havuz.
  - R50 sabit: Canavar Bitki.
- **Seed:**
  - Run başında run seed'inden bütün boss dizisi (R5…R45) üretilir ve kaydedilir.
  - Uygunluk o boss'un önizleme başında son kez kontrol edilir; uygun değilse aynı seed'le yedek aday seçilir.
  - Aynı seed ve aynı oyuncu kararları aynı diziyi verir.

## 5. Boss ödülleri

### 5.1 Sunum modeli (öneri)
- **Teklif:** Boss geçilince uygun havuzdan **3 farklı** ödül sunulur ve biri alınır.
- **Reroll:** Boss durağı başına 1 ücretsiz reroll; bütün teklifi bir kez yeniler. Meydan Okuyan ve Yeniden Çek ödülü +1 verir.
- **Nadirlik** = çıkma ağırlığı: Yaygın 3 · Orta 2 · Nadir 1. Nadirlik aynı ödülün güçlü sürümü değildir.
- **Havuzdan düşme:**
  - Birikmeyen ya da sınırına ulaşan ödül düşer. Birikenler sınıra kadar kalır.
  - Uygun olmayan (ön koşulu tutmayan) ödül teklif edilmez.
  - Uygun ödül 3'ten azsa boşluk her zaman uygun olan **Tohum Kesesi** ile dolar.
- **Kalıcılık:**
  - Alınan ödül run sonuna kadar geçerli; yeni run'da sıfırlanır.
  - Kalıcı hesap ilerlemesine karışmaz. Tek istisna bir defalık anlık ödüller: tile yükseltmesi, kaynak.
- **R50 (son boss):** run buff'ı sunulmaz. Final sonucu (bölüm 6) kalıcı tarafa gider: görev sayaçları, çiftçi ya da tırpan açılması, run kaydı ve rozeti.

### 5.2 Ödül havuzu (23 mekanik olarak farklı ödül)
"×" çarpan (MorePercent), "+puan" doğrudan yüzde puanı artışıdır; oyuncu metninde ikisi ayrılır.
"Sürekli" = run boyunca uygulanan stat; "bir defa" = alındığı an uygulanır, sonradan gelen içeriğe etkisi yoktur.

| # | Ad ve oyuncu metni | Mekanik etki | Build | Nadirlik (ağırlık) | Ön koşul | Birikme / sınır | Etkileşim | Altyapı | Ağaç karşılığı |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **Keskin Bıçak** — "Doğrudan hasar ×1,15 (sürekli)" | HarvestDamage More +0,15 | Doğrudan | Yaygın (3) | – | 3 kez (×1,52) | Dar Kesim ve Usta Biçici ile çarpılır | Mevcut stat | Aşırı Güç / Plazma |
| 2 | **Hızlı Bilek** — "Saldırı aralığı ×0,9 (saldırı sıklığı +%11, sürekli)" | AttackSpeed More −0,1 | Hız | Yaygın (3) | – | 3 kez; aralık tabanı 0,1 sn | Seri Orak ile; davranış tetiklerini dolaylı artırır | Mevcut stat | Yıldırım Kesim |
| 3 | **Geniş Savuruş** — "Vuruş yarıçapı ×1,15 (sürekli)" | AreaRadius More +0,15 | Alan | Orta (2) | – | 2 kez | Geniş Orak; Sis boss'unu yumuşatır | Mevcut stat | Geniş Tarama |
| 4 | **Kritik Göz** — "Kritik şansı +10 puan (sürekli)" | CritChance Flat +0,10 | Doğrudan | Yaygın (3) | Kritik < %100 | 3 kez; toplam ≤%100 | Kritik Odak ile toplanır | Mevcut stat | İleri Ustalık |
| 5 | **Ağır Darbe** — "Kritik hasarı +0,5 kat (sürekli)" | CritMultiplier Flat +0,5 | Doğrudan | Orta (2) | – | 3 kez | Kritik Güç ile toplanır | Mevcut stat | Hasat Zirvesi |
| 6 | **Canavar Kesimi** — "Doğrudan hasar ×1,5 (sürekli, bir kez)" | HarvestDamage More +0,5 | Doğrudan zirve | Nadir (1) | R30+ | 1 | Final çekirdeğine vurulan hasarı da büyütür | Mevcut stat | Aşırı Güç I |
| 7 | **Bereketli Toprak** — "Üretim aralığı ×0,9 (sürekli)" | PlantSpawnRate More −0,1 (saksı) | Üretim | Yaygın (3) | – | 3 kez; 0,5 sn tabanı, fazlası nadirliğe | Fidanlıkçı; Don'u yumuşatır | Mevcut stat | Seri Üretim |
| 8 | **Nadir Tohum** — "Nadir bitki şansı +8 puan (sürekli)" | RareSpawnChance Flat +8 (saksı) | Nadirlik / hedef | Orta (2) | – | 3 kez; toplam ≤95 puan | Seçici Yetiştirici; Kristal Bahçe | Mevcut stat | Nadir Filizler |
| 9 | **Bilgi Filizi** — "XP ×1,25 (sürekli)" | XPGainMultiplier More +0,25 | XP | Yaygın (3) | R≤35 (geç XP işe yaramaz) | 2 kez | Âlim; Bilgelik rezonansı | Mevcut stat | Hasat Deneyimi II |
| 10 | **Hasat Vergisi** — "Hasattan gelen Gold, Iron, Stone ×1,2 (sürekli)" | 3 kaynak More +0,2 | Ekonomi | Yaygın (3) | – | 3 kez | Tüccar, Bolluk | Mevcut stat | Altın Hasat II / Zengin Cevher |
| 11 | **Şöhret Tacı** — "Harvest Score ×1,15 (sürekli)" | HarvestScoreMultiplier More +0,15 (oyuncu) | Kota / skor | Nadir (1) | – | 2 kez | Şöhret rezonansı ile çarpılır; kotayı doğrudan kolaylaştırır | Mevcut stat | Hasat Rekoru |
| 12 | **Kıvılcım** — "Saksılardaki mevcut davranış şansları ×1,25 (sürekli; şansı olmayan saksıya şans vermez)" | Explosion/Tornado/Boomerang/Electric More +0,25 (saksı) | Davranış tetiği | Orta (2) | Tarlada en az 1 davranış tile'ı | 2 kez; her şans ≤%100 | MorePercent 0'ı 0 bırakır: yalnız mevcut davranışı güçlendirir | Mevcut stat | – (yeni yön) |
| 13 | **Yıkım Gücü** — "Davranış hasarı ×1,2 (sürekli)" | Davranış hasar çarpanı | Davranış gücü | Orta (2) | Davranış kilidi açık | 3 kez | Rezonans (Güçlendirilmiş Hasat) ve uzmanlaşma ile aynı noktada bir kez çarpılır | **Küçük yeni:** run buff çarpanı `BehaviorDamageMultiplier`'a | – |
| 14 | **Toprak Uyanışı** — "Haritadaki bütün tile'lar +1 seviye (bir defa; max seviyedekilere etkisiz, sonradan gelen tile'lara uygulanmaz)" | Her tile'da `GroundCell.AddLevels(1)` | Rezonans / tile | Orta (2) | En az 3 yükseltilebilir tile | Tekrar alınabilir; seviye ≤3 | Kart yükseltmesiyle aynı sistem (+%25 / seviye); nadirliği değiştirmez | **Mevcut** (`AddLevels`) | – |
| 15 | **Rezonans Yankısı** — "Aktif rezonansların stat etkisi ×1,25 (sürekli)" | ResonanceManager değiştiricilerinin değerine çarpan | Rezonans | Nadir (1) | En az 1 aktif rezonans | 1 | Odak ×2 → ×2,25 gibi; skor, kaynak, XP rezonanslarını da büyütür | **Yeni** (rezonans değer ölçeği) | – |
| 16 | **Çifte Şans** — "Çifte hasat şansı ×1,5 (sürekli; Duplicate tile'ı olan saksılarda)" | DuplicateChance More +0,5 (saksı) | Ekonomi | Orta (2) | Duplicate tile'ı var | 1; ≤%100 | Bolluk ve Verimli Toprak | Mevcut stat | – |
| 17 | **Ek Vazgeçiş** — "Her round +1 kart atlama hakkı (sürekli)" | CardSkip Flat +1 | Kart / ekonomi | Yaygın (3) | – | 2 kez | Atlama ödülü kaynağa dönüşür | Mevcut stat | İkinci Vazgeçiş |
| 18 | **Yeniden Çek** — "Kart ekranında round başına 1 yenileme; boss ödül ekranında +1" | Reroll hakkı | Kart / şans | Orta (2) | – | 2 kez | Meydan Okuyan | **Yeni:** `RerollCard` stat'ı var ama okunmuyor; düğme ve mantık gerekir | Kart Sezgisi |
| 19 | **Şanslı El** — "Kart nadirliği şansı +0,06 (sürekli)" | MutationLuck Flat +0,06 | Kart / şans | Yaygın (3) | – | 3 kez | Tile seviyesi kartlarını da büyütür | Mevcut stat | Kart Sezgisi |
| 20 | **Altın Hedef** — "Rare ve üstü bitkilere doğrudan hasar ×1,3 (sürekli)" | Nadirliğe bağlı doğrudan hasar | Hedef seçimi (aura) | Orta (2) | – | 2 kez | Dar Kesim, Seçici Yetiştirici; final filizleri | **Küçük yeni:** `AttackInRadius`'ta nadirlik koşulu | İleri Ustalık |
| 21 | **Ödül Avcısı** — "Rare ve üstü bitkilerin skoru ×1,25 (sürekli)" | Nadirliğe bağlı skor çarpanı | Hedef seçimi / kota | Nadir (1) | – | 1 | Değerli Hasat rezonansı ile en güçlüsü seçilir (rezonans kuralı) | **Küçük yeni:** `HarvestScoreManager`'da nadirlik koşulu | – |
| 22 | **Fırtına Kapasitesi** — "Aynı anda aktif kasırga ve bumerang sınırı +3 (sürekli)" | TornadoManager / HarvestBehaviorManager sınırı | Davranış (alan) | Nadir (1) | Kasırga ya da bumerang tile'ı var | 1 | Kapasite sınırı Bölüm 2 ölçümlerinde darboğaz değildi; yoğun geç tarlada ölçülmeli | **Küçük yeni:** çalışma zamanında sınır artışı | – |
| 23 | **Tohum Kesesi** — "Anında 2 round'luk Gold geliri kadar kaynak (bir defa)" | Kaynak ekleme (ortalama gelirden) | Ekonomi / yedek | Yaygın (3) | Her zaman uygun | Tekrar alınabilir | Havuz boşluk doldurucusu | Mevcut (`AddResource`) | – |

Round süresi ve tempo **ödül olarak verilmez**: Bölüm 2.2 ölçümüne göre süre bütün yatırımları doğrusal çarpar.
Etkisiz statlar (`StartingGoldBonus`, `StartingPlanterCount`, `ExtraCardChoice`) run ortası ödül için anlamsız ya da kodda karşılıksız; kullanılmadı.

### 5.3 "Tüm tile'lara +1 seviye" incelemesi
- **Gerçek bir seviye var mı?** Evet: `GroundCell.Level` (0–3), `MaxLevel` 3, `LevelBonus` %25. Seviye tile'ın zar değerini (1+0,25·Sv) ile çarpar.
- **Nadirlikle ilişkisi:** Seviye nadirlikten **ayrı**. Nadirlik tile'ın zar aralığını belirler; seviye o değeri büyütür. Kart nadirliği yalnız kaç seviye verileceğini belirler.
- **Kapsam:** Etki bir defalık. O an haritada tile'ı olan ve seviyesi <3 olan açık hücrelere uygulanır; sonra düşen tile'lar seviye 0 başlar.
- **Azami seviye:** Etkisizdir. Ödül yalnız en az 3 yükseltilebilir tile varsa sunulur; hepsi max ise havuzdan düşer.
- **Rezonans:** Seviye, tile türünü değiştirmediği için rezonans eşiğini değiştirmez; yalnız tile'ın kendi stat'ını büyütür (kod: `AddLevels` → `RefreshTileBuffs`).

### 5.4 Çeşitlilik hesabı (9 durak: R5 … R45)
Monte Carlo, 40 000 run. Oyuncu tekliften rastgele birini alıyor; alınan birikmeyen ödül havuzdan düşüyor. "Varsayım": eşit ağırlık ya da 8 yaygın (3) / 8 orta (2) / 4 nadir (1).

| Model | Run boyunca görülen farklı ödül | Belirli yaygın ödülü görme | Belirli nadir ödülü görme | Nadiri ilk 2 durakta görme | 3 alternatiften birini ilk 2 durakta görme |
|---|---|---|---|---|---|
| Eşit, 3 seçenek | 17,1 / 20 | %85 | %85 | %28 | %65 |
| Eşit, 3 seçenek + 1 reroll | 19,6 | %98 | %98 | %49 | %88 |
| Eşit, 4 seçenek | 18,6 | %93 | %93 | %37 | %77 |
| Kademeli, 3 seçenek | 16,6 | %94 | %60 | %14 | %62 |
| **Kademeli, 3 seçenek + 1 reroll (öneri)** | 19,1 | %100 | %84 | %26 | %85 |
| Kademeli, 4 seçenek | 18,1 | %98 | %72 | %19 | %74 |

Analitik kontrol (eşit ağırlık, alınan düşmezse): bir ödülü 9 durakta en az bir kez görme 1 − (1 − k/20)^9, k=3 için %77, k=4 için %87.

- "Havuzda 20 var, istenen ödül nadir" varsayımı yanlış. Run boyunca yaygın ya da orta bir ödülü görmek %84–100; sorun **erken erişim**.
- Nadir bir ödülü ilk iki durakta (R5, R10) görme olasılığı %14–26. Bu yüzden build'in temeli tek bir nadir ödüle bağlanmaz: her temel ihtiyaç için en az 3 alternatif ödül olmalı.
  - Hız: Hızlı Bilek, Geniş Savuruş, Kıvılcım.
  - Hasar: Keskin Bıçak, Kritik Göz, Altın Hedef.
  - Üretim: Bereketli Toprak, Nadir Tohum, Tohum Kesesi.
  - Davranış: Kıvılcım, Yıkım Gücü, Toprak Uyanışı.
- Böyle bir ihtiyaç ilk iki durakta reroll ile %85 karşılanıyor.
- Nadir ödüller (Canavar Kesimi, Rezonans Yankısı, Şöhret Tacı, Ödül Avcısı, Fırtına Kapasitesi) build'i zirveye taşır; yokluğunda build çalışır.

## 6. Final: Canavar Bitki (R50)

### 6.1 Uyumluluk problemi
Davranışlar yalnız doğrudan **öldürmede** tetikleniyor ve davranış hedefleri grid hücrelerindeki bitkiler. Tek ve yüksek canlı boss ölene kadar hiçbir davranış tetiklenmez. Davranışlar boss'a vursa bile tetiklenmek için yine bir öldürme gerekir.

| Seçenek | Nasıl çalışır | Artı | Eksi |
|---|---|---|---|
| A. Ara hasat eşikleri | Boss canının her %X'i bir "hasat olayı" sayılır ve davranışları tetikler | Tek hedef, basit gösterge | Davranışlar saksıya bağlı; eşiği hangi saksının tetikleyeceği belirsiz. Tile ve rezonans yerleşimi anlamsızlaşır; davranışların hedefi yine yok |
| **B. Boss'a bağlı, hasat edilebilir bölümler (öneri)** | Final round'unda saksılar normal bitki yerine **Canavar Filizi** üretir (boss'un kök uçları). Filizler normal bitki gibi hasat edilir ve her hasat boss'a hasar aktarır. Çekirdek tarlanın ortasında durur ve doğrudan vurulabilir | Hasat döngüsü, aura, saksı, tile, rezonans ve bütün davranışlar **kod değişmeden** çalışır (filizler `PlantHealth`, sahipleri saksı). Tek kazanma hedefi boss | Filiz ve çekirdek dengesini kurmak ve görsel olarak "tek canavar" hissini vermek gerekir |
| C. Başka bir boss etkileşimi | Ör. boss yalnız belirli vuruş desenleriyle hasar alır | Özgün olabilir | Run boyunca kurulan build'i finalde işe yaramaz yapma riski |

**Öneri B:** Normal round'larda elektrik ve diğer davranış kuralları aynen kalır. Final uyarlaması yalnız hangi bitkinin üretildiğini ve hasadın nereye aktarıldığını değiştirir.

### 6.2 Final akışı (öneri)
- **Tarla:**
  - Boss tarlanın üstüne gelir. Çekirdek merkez 3×3'ün üstünde yüzer (görsel); alttaki saksılar yerinde kalır ve filiz üretir.
  - Saksı ve tile'lar korunur; tarla yeniden kurulmaz.
  - R45 sonrası alışverişte "son hazırlık" normal devam eder.
- **Katkı:**
  - Her saksı kendi üretim hızı ve nadirliğiyle filiz üretir.
  - Filizin boss'a aktardığı hasar = filizin **Harvest Score ödülü** × sabit. Böylece nadirlik, skor, rezonans (Şöhret, Değerli Hasat) ve Ödül Avcısı finalde karşılık bulur.
  - Çekirdeğe doğrudan vuruş ayrıca hasar verir (doğrudan hasar, crit, Canavar Kesimi).
- **Süre, kota, yenilme:**
  - Final round'unun süresi profilde sabit (öneri 90 sn, tempo yok). Kota yok.
  - Kazanma: süre bitmeden çekirdek canı 0.
  - Yenilme: süre biter, "Canavar Bitki kaçtı". Run kaybedilir; skor ve ilerleme kaydedilir, görev sayaçları işler.
- **Can ve aşamalar:**
  - Can profil verisinde **sabit**: oyuncunun anlık hasarına göre eşitlenmez; güçlü build daha hızlı bitirir.
  - İlk değer referans build'lerin R45 durumundaki "saniye başına filiz skoru + çekirdek hasarı" ölçümünden, hedef ~60 sn ile seçilir.
  - 3 aşama (%100–66, %66–33, %33–0): aşama geçişinde filiz deseni değişir (kenar → merkeze yakın → her yer), çekirdek parlar. Aşamalar can çarpmaz.
- **His:** Büyük model, vuruş başına esneme ve renk, üstte aşama çizgili can çubuğu, aşama damgası, yenilme anında yavaş çekim + büyük hasat patlaması (mevcut ScreenStamp, CameraFeel, VFX desenleri).

### 6.3 Yatırımların finaldeki karşılığı
| Yatırım | Finaldeki karşılığı |
|---|---|
| Doğrudan hasar, saldırı hızı | Çekirdeğe doğrudan hasar; filizleri hızlı temizlemek; her doğrudan filiz hasadı davranış tetikler |
| Alan hasarı, çoklu hedef | Tek vuruşta birden çok filiz; çekirdek ve filizlere aynı anda |
| Elektrik, patlama, bumerang, kasırga | Filiz öldürmesiyle tetiklenir, komşu filizleri keser (her filiz boss'a aktarır). Kasırga ve bumerang yolu çekirdek hücrelerinden geçerse çekirdeğe vurur (küçük ek) |
| Saksı yerleşimi, tile, rezonans | Filiz üretim noktaları ve değerini belirler. Odak filizlere doğrudan hasarı, Şöhret ve Değerli Hasat aktarılan hasarı büyütür; Güçlendirilmiş Hasat davranışları |
| Üretim hızı, bitki nadirliği | Daha çok ve daha değerli filiz (Legendary filiz daha çok aktarır) |
| XP ve ekonomi | Finalden önce kazandırdıkları güçle (ağaç, kart, ödül). Finalde ek dönüşüm yok. Tüccar ve Âlim zirveye daha güçlü gelir |

Hiçbir ana mekanizma finalde işlevsiz kalmaz: davranış build'leri filiz zinciriyle, doğrudan build çekirdekle, rezonans ve nadirlik build'i filiz değeriyle katkı verir.

### 6.4 Gerekli kod değişiklikleri (öneri; bu bölümde uygulanmadı)
- **Final verisi ve yönetimi:**
  - `FinalBossSO`: can, aşama eşikleri, süre, filiz PlantSO seti, skor → hasar sabiti.
  - `FinalBossController`: çekirdek nesnesi, `IDamageable` çekirdek, aşama olayları, can çubuğu, yenilme ve kaçma.
- **Oyun kodundaki küçük değişiklikler:**
  - `PlantSpawner`: final aktifken filiz seti.
  - Filiz ödülü: `PlantResource.GiveReward` yerine ya da yanında boss'a aktarım.
  - `PlayerController.AttackInRadius`: yarıçap çekirdeğe değiyorsa çekirdeğe vuruş.
  - `Tornado` / `BoomerangScythe`: çekirdek hücresinden geçişte çekirdeğe vuruş.
- **Akış:**
  - `RoundManager`: final round'unda kota yerine boss sonucu, zafer ya da yenilme.
  - UI: final HUD.
- **Testler:**
  - Filiz hasadı davranış tetikler ve boss'a aktarır.
  - Normal round'larda elektrik ve davranış kuralı değişmez (ElectricRevertVerification tekrar).
  - Aşama geçişleri; süre bitince yenilme.
  - Ölçüm: aynı referans durumda build başına bitirme süresi ve hasar payı (çekirdek / doğrudan filiz / davranış filizi).

## 7. Can eğrisi ve XP / kart deneyi (plan; değerler değişmedi)

### 7.1 Can
- **Aday model:** Can(r, n) = TemelCan(r) × (1 + k·n), n = 0 … 4 (Common … Legendary).
  - Bugünkü çarpanlar 1 / 1,2 / 1,6 / 2 / 3 doğrusal değil.
  - k = 0,5 bugünkü Legendary ×3'ü korur (1 / 1,5 / 2 / 2,5 / 3). Orta nadirlikler sertleşir. Karar değil; ölçülecek.
- **TemelCan(r):** 1–50 arasında monoton ve yumuşak (ör. parçalı güç ya da yavaş artan büyüme). Bugünkü R65 sonrası sıçrama (180 → 1 500) 50 round profilinde kullanılmaz.
- **Doygunluk hedefi (öneri, ölçülecek):**
  - R10'da orta build Common'ı 2–3, Legendary'yi 5–7 vuruşta.
  - R30'da ağaç ve ödülleri tamamlanmaya yakın build Common'ı 1–2 vuruşta.
  - Endgame'de tek vuruş serbest.
- **Ölçüm:** round × nadirlik vuruş sayısı tablosu, tek vuruş oranı, hasada kadar süre. Referans build'lerin R10/20/30/40/50 durumlarında (Bölüm 2.2 sahne ölçüm aracı yeniden kullanılır).
- **Kural:** gizli otomatik ölçekleme yok.

### 7.2 Level başına 3 kart alma (ayrı deney)
- **Ne olduğu:** Bir level'da üç ayrı kart ekranı. Üç seçenekten bir kart almakla karıştırılmaz.
- **Karşılaştırma:** aynı ekonomi ve seed'lerle 1 alım ile 3 alım.
- **Ölçümler:**
  - Tile sayısı ve grid'in dolma round'u (sonrası seviye ve temel güç kartı).
  - Kart havuzunun tükenmesi.
  - Hazırlıkta geçen süre (kart ekranı sayısı).
  - XP yatırımının geri ödemesi.
  - Hız build'ine etkisi. Hız → öldürme → XP → daha çok kart zinciri bileşik büyüdüğü için hız build'inin kazancı ayrıca raporlanır.
- **Kural:** Bu deney bitmeden kota ve can buna göre ayarlanmaz. Aynı pakette can ya da ağaç değiştirilmez.

## 8. Aşamalı uygulama planı
Sıra, "bir pakette bir denge ekseni" kuralına göre.

| Paket | Kapsam | Bağımlılık ve gerekçe | Kabul koşulu | Gerekli test |
|---|---|---|---|---|
| **3.2 Run50 referans profili ve ölçüm** | Denge değiştirmeden 50 round profili (10 segment, mevcut kota eğrisi, R50'de geçici zafer). Simülatör ve sahne ölçüm araçlarının 50 round'a genişlemesi. R10/20/30/40/50 referans durum raporu | Her sonraki paketin karşılaştırma tabanı | Profil seçilebilir; mevcut profiller değişmez. Referans rapor (tek vuruş, vuruş sayısı, gelir, erişim) üretilir | Profil ve akış testi (RunPrototypeVerification genişletmesi), simülatör batch |
| **3.3 Çiftçi / tırpan ve görev altyapısı** | FarmerSO, ScytheSO, QuestSO, MetaSave, seçim ekranı, RunLoadout. İçerik: Bahçıvan, Tüccar, Standart, Dar Kesim + 2 görev | Dengeden bağımsız; kalıcı kayıt erken sağlamlaşmalı | Seçim run'a uygulanır, run sonunda temizlenir. Kayıt sürüm ve bozuk dosya testi. Geçersiz seçim → varsayılan | Yeni LoadoutVerification (kayıt, temizlik, görev olayları); mevcut paketlerin tekrarı |
| **3.4 Boss ritmi ve ödül altyapısı** | `activeRounds` ve önizleme, `BossRewardManager` (`IRoundChoice`), 3 seçenek + 1 reroll, 6 mevcut-stat ödülü (1, 2, 7, 9, 10, 23). Don ve Sis boss'ları | 3.2 tabanına göre ölçülür; ağaç henüz değişmez | Boss yalnız 5k'da aktif; önizleme doğru. Ödül tek sefer, run sonunda temiz; kota kaybında ödül yok | SpecializationVerification benzeri akış testi; ödül birikme ve sınır testleri |
| **3.5 Can eğrisi + ağaç gücü + ilk güç aktarımı** | TemelCan ve k denemesi; Aşırı Güç, Yıldırım ve Seri Üretim aktarımı; karşılık ödüller (6, 13, 14, 20) | 3.2 referansı ve 3.4 ödül altyapısı olmadan güç taşınamaz | Doygunluk hedefi referans durumlarda sağlanır. Her azaltmanın ödül karşılığı erişilebilir | Sahne ölçümü (vuruş tablosu), simülatör 50 round; eski testlerin tekrarı |
| **3.6 XP / 3 kart deneyi** | Deney profili bayrağı; 1 ve 3 alım karşılaştırması | Can ve ağaç sabitlendikten sonra, tek eksen | Rapor; varsayılan değişmez | Simülatör + sahne karşılaştırması |
| **3.7 Boss ve ödül havuzu genişletme** | Sert Kabuk, Solgun Tarla, Değerli Hasat Günü, Statik Alan, Ayrık Otu; ödüller 3–5, 8, 11, 12, 15–19, 21, 22 (Yeniden Çek dahil) | Altyapı hazır | Her boss uygunluk ve seed testi; her ödül tek başına etki testi | Boss seçici testi, ödül birim testleri |
| **3.8 Canavar Bitki** | Bölüm 6 | Ödül ve boss altyapısı + referans R45 durumları | Filiz hasadı davranış tetikler; normal round kuralları değişmez; bitirme süreleri raporlanır | Final testi + ElectricRevertVerification tekrarı |
| **3.9 İçerik tamamlama** | 8 çiftçi, 4 tırpan, 20+ ödül eksikleri | Önceki altyapılar | Her içerik tek başına etki testi; kombinasyon raporu | Kombinasyon ölçümü |
| **3.10 Çoklu build dengesi** | Dört build adayı (hız + alan; doğrudan + değerli hedef; davranış + rezonans; XP karma) | Hepsi | Segment kota payı, başarısızlık round'u, alternatif yatırım getirisi raporu + insan testi | Simülatör, sahne, insan oyun testi |

## 9. İlk uygulanacak küçük paket: 3.2
**Kapsam:**
- `Run50` profili (10 segment, mevcut kota eğrisi, mevcut ağaç ve can; son boss yerine geçici R50 zaferi).
- `RunSimulator` ve sahne ölçümünün 50 round referans durumlarına genişletilmesi.
- Rapor: round × nadirlik vuruş sayısı, tek vuruş oranı, gelir, erişim, toplam oturum süresi tahmini.

**Kabul koşulları:**
- Profil menüden seçilebilir; diğer profiller değişmez.
- Mevcut 8 test paketi ve ElectricRevertVerification geçer.
- Referans rapor üretilir.

## 10. Açık kararlar (Bölüm 3.2'de kaydedildi; hiçbiri uygulanmadı)
Aşağıdakiler bu belgedeki önerilerin çözülmemiş noktalarıdır. Ölçüm verisi: [Bolum3-2-Run50-ReferansOlcum.md](Bolum3-2-Run50-ReferansOlcum.md).

### 10.1 Boss'un kendi başarı koşulu
**Sorun:** Segment kotası ilk dört round'da dolarsa, yalnız beşinci round'da aktif olan boss kuralı sonucu değiştirmez.

Bölüm 3.2 ölçümü bunu şimdiden gösteriyor:
- Mevcut eğride (10 × 1,45^n) deneyimli ve orta simülatör profilleri kotayı en dar segmentte bile 7 kat aşıyor.
- Yani bugün ne segment kotası ne de 5. round kuralı bir eleme baskısı kuruyor.

Karar verilmesi gereken seçenekler:

| Seçenek | Nasıl | Artı | Eksi |
|---|---|---|---|
| a) Boss round'unun kendi hedefi | Segment kotasına ek olarak boss round'unda ayrı bir skor hedefi (ör. segment kotasının belli bir payı) | Boss kuralı her zaman önemli | İki hedef; HUD ve özet karmaşıklaşır |
| b) Kota yalnız boss round'unda sayılır | İlk 4 round hazırlık ve ekonomi; segment kotası boss round'unun skoruyla ölçülür | Tek, net hedef; boss belirleyici | İlk 4 round'un skor baskısı kalkar; kota eğrisi baştan ayarlanmalı |
| c) Kurala özel görev | Boss'un kendi görevi (ör. Değerli Hasat Günü'nde N adet Rare+). Tutarsa ödül; tutmazsa ödül yok ama run sürer. Eleme segment kotasında kalır | Eleme ile ödül ayrılır; kural anlamlı | Her boss için ayrı görev ve metin tasarımı |
| d) Fazla skor tavanı | İlk 4 round'un skoru kotanın en fazla %X'ine sayılır; kalan pay boss round'unda kazanılmalı | Mevcut tek kotayı korur | Oyuncuya açıklaması zor ("neden saymadı?") |
| e) Ödül kalitesi | Eleme segment kotasında kalır; boss round performansı ödül ekranının nadirliğini ya da seçenek sayısını belirler | Ceza yok, hedef var | Zayıf build'de boss yine önemsiz kalabilir |

Kota eğrisinin kendisi de ayrı bir karar: 50 round'da bugünkü eğri neredeyse hiç kimseyi elemiyor (simülatörde yalnız "Yeni" politikada 100 run'ın 8'i, R35–40, ağacın ~%3'ü alınmış hâlde).

### 10.2 Ödül çeşitliliği yeniden hesaplanmalı
- Bölüm 5.4'teki hesap soyut 20 ödüllük havuzla yapıldı. "%85 alternatif bulma" sonucu bütün build'ler için garanti değildir.
- Yeniden hesap şunları içermeli:
  - Gerçek 23 ödüllük havuz ve nadirlik ağırlıkları.
  - Uygunluk koşulları: Kıvılcım davranış tile'ı, Toprak Uyanışı ≥3 yükseltilebilir tile, Canavar Kesimi R30+, Bilgi Filizi R≤35 gibi.
  - Birikme sınırı ve sınırda havuzdan düşme.
  - Durak başına 1 reroll; Tohum Kesesi dolgu kuralı.
- Build başına ihtiyaç grupları ayrı ayrı ölçülmeli: hız + alan, doğrudan + değerli hedef, davranış + rezonans, ekonomi / XP.
- Yer: 3.4 (ödül altyapısı) ile birlikte, gerçek veriden okuyan bir simülasyon.

### 10.3 Güç aktarımı henüz doğrulanmadı
- Ağaçtan çıkan büyük çarpanların yerine küçük ödüller koymak, gücün aktarıldığı anlamına gelmez.
- Hedef çarpan eşitliği değil, endgame hissi olmalı: saniyede hasat, dolu bir tarlayı temizleme süresi, boş üretim noktası oranı, alan temizleme sıklığı.
- Bölüm 3.2 referansı (simülatör, deneyimli, R50):
  - Saldırı aralığı P50 1,56 sn.
  - Hasat kapasitesi üretim tavanının yalnız ~%15'i.
  - Yani bugün endgame'de de "tarla temizleme" yok; hasar tavanı aşıyor, hız ve alan aşmıyor. Sahne ölçümü için 3.2 belgesine bakın.
- Aktarım bu ölçütlerle yapılmadan önce ve yapıldıktan sonra karşılaştırılmalı.

### 10.4 "Doğrudan hasar" metinleri
- **Davranış hasarını da büyütenler:**
  - Keskin Bıçak ve Canavar Kesimi `HarvestDamage` stat'ını değiştirir.
  - Davranış hasarı da bu stat'tan okunur (`PlanterBrain` 334, 379, 401).
  - Bu ödüller doğrudan hasatla birlikte patlama, kasırga, bumerang ve elektriği de büyütür.
- **Seçim:**
  - Metin "Hasat hasarı ×1,15 (vuruş ve davranışlar)" olur, ya da
  - uzmanlaşmadaki gibi yalnız doğrudan vuruşa uygulanan ayrı bir katsayı kullanılır (`SpecializationManager.DirectMultiplier` deseni).
- **Zaten yalnız doğrudan vuruşa etki edenler:**
  - Kritik Göz ve Ağır Darbe: kritik yalnız `PlayerController`'da.
  - Altın Hedef (öneri): `AttackInRadius`'ta.
- Aynı ayrım ağaç düğümleri için de geçerli: bütün hasar düğümleri davranışları da büyütüyor.

### 10.5 Final filizleri yalnız öneri
- Bölüm 6'daki "hasat edilebilir filiz" çözümü davranış uyumu için seçildi.
- Kullanıcının beklentisi tek bir canavar hedefi; bu çözümün o beklentiyle uyumu doğrulanmadı.
- 3.8'den önce kullanıcıyla görsel taslak ya da küçük prototip üzerinden karar verilmeli.

### 10.6 R10 vuruş hedefleri onaylı değil
- "R10'da Common 2–3, Legendary 5–7 vuruş" bir başlangıç önerisidir, denge kararı değildir.
- Süreyle birlikte değerlendirilmeli. Bölüm 3.2'de R10 saldırı aralığı ~2,9 sn (simülatör, deneyimli) iken tek hedefe:
  - 2–3 vuruş ≈ 6–9 sn.
  - 5–7 vuruş ≈ 15–20 sn.
- Aynı hedef, saldırı hızı ve alan değişirse bambaşka hissettirir. Can eğrisi, saldırı aralığı ve yarıçap birlikte ölçülmeden hedef sabitlenmez.

## Ek A — 140 düğümün tamamı (asset'ten; erişim: simülatör P50 "ilk / tamam", D·O·Y; son sütun R50'ye kadar tamamlayan deneyimli run oranı)

| Düğüm | Rol | Son kademe etkisi | Kademe fiyatları | Hedef | Önkoşul | Simülatör ilk/tamam (D·O·Y) · R50'e kadar tamamlayan (D) |
|---|---|---|---|---|---|---|
| Altın Hasat I - 1 | Ekonomi | Gold +%16.7 | 14G / 18G / 22G | R1–20 | yok | D 8/8 · O 7/8 · Y 6/11 · %100 |
| Altın Hasat I - 2 | Ekonomi | Gold +%11.1 | 27G / 31G | R1–20 | Altın Hasat I - 1@3 | D 9/9 · O 8/9 · Y 14/19 · %100 |
| Altın Hasat I - 3 | Ekonomi | Gold +%11.1 | 35G / 40G | R1–20 | Altın Hasat I - 2@2 | D 9/9 · O 9/9 · Y 21/21 · %100 |
| Altın Hasat I - 4 | Ekonomi | Gold +%11.1 | 44G / 49G | R1–20 | Altın Hasat I - 3@2 | D 10/10 · O 10/10 · Y 23/25 · %100 |
| Biraz Daha Zaman - 1 | Süre/tempo | süre +5 | 14G / 18G / 22G | R1–15 | yok | D 4/4 · O 2/4 · Y 5/10 · %100 |
| Biraz Daha Zaman - 2 | Süre/tempo | süre +3 | 27G / 31G | R1–15 | Biraz Daha Zaman - 1@3 | D 5/6 · O 5/5 · Y 14/18 · %100 |
| Biraz Daha Zaman - 3 | Süre/tempo | süre +4 | 35G / 40G | R1–15 | Biraz Daha Zaman - 2@2 | D 6/7 · O 6/7 · Y 20/24 · %100 |
| Biraz Daha Zaman - 4 | Süre/tempo | süre +3 | 44G / 49G | R1–15 | Biraz Daha Zaman - 3@2 | D 7/8 · O 7/8 · Y 25/26 · %100 |
| Keskin Başlangıç - 1 | Hasar | hasar +8 | 3G / 5G / 8G | R1–15 | yok | D 2/2 · O 1/1 · Y 2/7 · %100 |
| Keskin Başlangıç - 2 | Hasar | hasar +12 | 19G / 22G | R1–15 | Keskin Başlangıç - 1@3 | D 4/5 · O 4/4 · Y 8/10 · %100 |
| Keskin Başlangıç - 3 | Hasar | hasar +16 | 25G / 28G | R1–15 | Keskin Başlangıç - 2@2 | D 5/5 · O 4/5 · Y 15/18 · %100 |
| Keskin Başlangıç - 4 | Hasar | hasar +24 | 32G / 35G | R1–15 | Keskin Başlangıç - 3@2 | D 6/7 · O 6/6 · Y 19/21 · %100 |
| Hızlı Eller - 1 | Hız | saldırı aralığı ×0.9 | 19G / 26G / 32G | R3–25 | Biraz Daha Zaman - 1@3 | D 8/16 · O 9/15 · Y 12/19 · %100 |
| Hızlı Eller - 2 | Hız | saldırı aralığı ×0.924 | 38G / 44G | R3–25 | Hızlı Eller - 1@3 | D 16/16 · O 16/16 · Y 21/22 · %100 |
| Hızlı Eller - 3 | Hız | saldırı aralığı ×0.92 | 51G / 57G | R3–25 | Hızlı Eller - 2@2 | D 16/17 · O 17/17 · Y 26/27 · %100 |
| Hızlı Eller - 4 | Hız | saldırı aralığı ×0.915 | 63G / 69G | R3–25 | Hızlı Eller - 3@2 | D 17/17 · O 17/18 · Y 29/30 · %100 |
| Grid Genişleme I | Alan (grid) | grid = 7 | 80G / 40I | R4–18 | yok | D 10/10 · O 10/10 · Y 19/20 · %100 |
| 1×3 Saksı | Kilit: 1×3 saksı | — | 40G | R5–7 | Grid Genişleme I@1 | D 10/10 · O 15/15 · Y 24/24 · %100 |
| Düzenli Üretim - 1 | Üretim | üretim aralığı ×0.95 | 39G / 51G / 64G | R8–30 | Altın Hasat I - 1@3 | D 17/18 · O 17/19 · Y 18/26 · %100 |
| Düzenli Üretim - 2 | Üretim | üretim aralığı ×0.965 | 76G / 89G | R8–30 | Düzenli Üretim - 1@3 | D 19/19 · O 19/20 · Y 28/33 · %100 |
| Düzenli Üretim - 3 | Üretim | üretim aralığı ×0.964 | 101G / 114G | R8–30 | Düzenli Üretim - 2@2 | D 19/19 · O 20/21 · Y 35/37 · %100 |
| Düzenli Üretim - 4 | Üretim | üretim aralığı ×0.963 | 126G / 139G | R8–30 | Düzenli Üretim - 3@2 | D 19/19 · O 21/22 · Y 40/41 · %100 |
| 2×2 Saksı | Kilit: 2×2 saksı | — | 100G | R10–13 | 1×3 Saksı@1, Grid Genişleme I@1 | D 15/15 · O 16/16 · Y 31/31 · %100 |
| Geniş Süpürüş - 1 | Alan | alan +%10 | 35G / 46G / 58G | R10–30 | Hızlı Eller - 1@3 | D 18/20 · O 19/22 · Y 20/27 · %100 |
| Geniş Süpürüş - 2 | Alan | alan +%6.67 | 69G / 80G | R10–30 | Geniş Süpürüş - 1@3 | D 21/22 · O 23/24 · Y 29/31 · %100 |
| Geniş Süpürüş - 3 | Alan | alan +%6.67 | 91G / 102G | R10–30 | Geniş Süpürüş - 2@2 | D 22/23 · O 25/26 · Y 33/36 · %100 |
| Geniş Süpürüş - 4 | Alan | alan +%6.67 | 114G / 125G | R10–30 | Geniş Süpürüş - 3@2 | D 23/23 · O 26/27 · Y 37/38 · %100 |
| Kesim Tekniği - 1 | Hasar | hasar +%15 | 29G / 39G / 48G | R10–30 | Keskin Başlangıç - 1@3 | D 17/20 · O 18/22 · Y 13/23 · %100 |
| Kesim Tekniği - 2 | Hasar | hasar +%10 | 57G / 67G | R10–30 | Kesim Tekniği - 1@3 | D 20/20 · O 22/23 · Y 26/28 · %100 |
| Kesim Tekniği - 3 | Hasar | hasar +%11.7 | 76G / 85G | R10–30 | Kesim Tekniği - 2@2 | D 21/22 · O 24/25 · Y 30/32 · %100 |
| Kesim Tekniği - 4 | Hasar | hasar +%13.3 | 95G / 104G | R10–30 | Kesim Tekniği - 3@2 | D 22/23 · O 25/26 · Y 34/36 · %100 |
| Patlayıcı Kartlar | Kilit: Patlama kartları | — | 20I | R10–20 | 1×3 Saksı@1 | D 10/10 · O 15/15 · Y 26/26 · %100 |
| İlk Vazgeçiş | Kart | kart atlama +1 | 240G | R10–25 | Geniş Süpürüş - 1@3 | D 21/21 · O 23/23 · Y 43/43 · %100 |
| Demir Hasat - 1 | Ekonomi | Iron +%30 | 55G / 72G / 90G | R15–45 | Altın Hasat I - 1@3 | D 24/26 · O 27/29 · Y 21/30 · %100 |
| Demir Hasat - 2 | Ekonomi | Iron +%20 | 107G / 124G | R15–45 | Demir Hasat - 1@3 | D 26/26 · O 29/30 · Y 35/38 · %100 |
| Demir Hasat - 3 | Ekonomi | Iron +%23.3 | 142G / 159G | R15–45 | Demir Hasat - 2@2 | D 27/27 · O 30/31 · Y 40/43 · %100 |
| Demir Hasat - 4 | Ekonomi | Iron +%26.7 | 177G / 194G | R15–45 | Demir Hasat - 3@2 | D 28/28 · O 31/32 · Y 45/47 · %100 |
| Hasat Deneyimi I - 1 | XP | XP +%12.5 | 43G / 57G / 70G | R15–40 | Düzenli Üretim - 1@3 | D 20/23 · O 23/27 · Y 27/30 · %100 |
| Hasat Deneyimi I - 2 | XP | XP +%8.33 | 84G / 98G | R15–40 | Hasat Deneyimi I - 1@3 | D 24/24 · O 27/27 · Y 33/35 · %100 |
| Hasat Deneyimi I - 3 | XP | XP +%12.5 | 112G / 125G | R15–40 | Hasat Deneyimi I - 2@2 | D 24/25 · O 28/28 · Y 36/39 · %100 |
| Hasat Deneyimi I - 4 | XP | XP +%16.7 | 139G / 153G | R15–40 | Hasat Deneyimi I - 3@2 | D 25/25 · O 28/29 · Y 41/44 · %100 |
| Çifte Hasat Kartları | Kilit: Çifte hasat kartları | — | 35I | R15–25 | 2×2 Saksı@1 | D 15/15 · O 16/16 · Y 32/32 · %100 |
| 2×3 Saksı | Kilit: 2×3 saksı | — | 180G | R16–20 | 2×2 Saksı@1, Grid Genişleme I@2 | D 18/18 · O 18/18 · Y 40/40 · %100 |
| Kritik Odak - 1 | Hasar (kritik) | kritik şansı +0.05 | 78G / 103G / 128G | R20–65 | Kesim Tekniği - 1@3 | D 26/29 · O 30/32 · Y 28/38 · %100 |
| Kritik Odak - 2 | Hasar (kritik) | kritik şansı +0.0333333 | 153G / 178G | R20–65 | Kritik Odak - 1@3 | D 29/29 · O 33/33 · Y 42/45 · %100 |
| Kritik Odak - 3 | Hasar (kritik) | kritik şansı +0.0333333 | 203G / 228G | R20–65 | Kritik Odak - 2@2 | D 30/30 · O 34/35 · Y 46/49 · %100 |
| Kritik Odak - 4 | Hasar (kritik) | kritik şansı +0.0333333 | 253G / 278G | R20–65 | Kritik Odak - 3@2 | D 31/32 · O 36/37 · Y –/– · %100 |
| Tornado Kartları | Kilit: Kasırga kartları | — | 40I | R20–30 | Patlayıcı Kartlar@1 | D 10/10 · O 15/15 · Y 30/30 · %100 |
| Bumerang Orak Kartları | Kilit: Bumerang kartları | — | 60I | R25–40 | Tornado Kartları@1 | D 12/12 · O 20/20 · Y 32/32 · %100 |
| Güçlü Kesim - 1 | Hasar | hasar +37.5 | 219G / 289G / 360G | R25–60 | Keskin Başlangıç - 1@3 | D 30/33 · O 35/38 · Y 41/– · %100 |
| Güçlü Kesim - 2 | Hasar | hasar +26.25 | 430G / 500G | R25–60 | Güçlü Kesim - 1@3 | D 33/34 · O 39/41 · Y –/– · %100 |
| Güçlü Kesim - 3 | Hasar | hasar +37.5 | 570G / 640G | R25–60 | Güçlü Kesim - 2@2 | D 35/37 · O 42/45 · Y –/– · %98 |
| Güçlü Kesim - 4 | Hasar | hasar +48.75 | 711G / 781G | R25–60 | Güçlü Kesim - 3@2 | D 39/42 · O 49/– · Y –/– · %95 |
| Çapraz Elektrik Kartları | Kilit: Elektrik kartları | — | 80I | R25–40 | Patlayıcı Kartlar@1 | D 14/14 · O 24/24 · Y 30/30 · %100 |
| Altın Hasat II - 1 | Ekonomi | Gold +%71.4 | 702G / 926G / 1151G | R30–65 | Demir Hasat - 1@3 | D 41/45 · O –/– · Y –/– · %78 |
| Altın Hasat II - 2 | Ekonomi | Gold +%47.6 | 1375G / 1600G | R30–65 | Altın Hasat II - 1@3 | D 46/47 · O –/– · Y –/– · %70 |
| Altın Hasat II - 3 | Ekonomi | Gold +%59.5 | 1825G / 2049G | R30–65 | Altın Hasat II - 2@2 | D 48/49 · O –/– · Y –/– · %58 |
| Altın Hasat II - 4 | Ekonomi | Gold +%71.4 | 2274G / 2498G | R30–65 | Altın Hasat II - 3@2 | D 50/– · O –/– · Y –/– · %35 |
| Taş Hasat - 1 | Ekonomi | Stone +%30 | 285I / 376I / 468I | R30–65 | Demir Hasat - 1@3 | D 26/34 · O 34/44 · Y 46/– · %100 |
| Taş Hasat - 2 | Ekonomi | Stone +%20 | 559I / 650I | R30–65 | Taş Hasat - 1@3 | D 37/40 · O –/– · Y –/– · %95 |
| Taş Hasat - 3 | Ekonomi | Stone +%23.3 | 741I / 832I | R30–65 | Taş Hasat - 2@2 | D 42/44 · O –/– · Y –/– · %78 |
| Taş Hasat - 4 | Ekonomi | Stone +%26.7 | 924I / 1015I | R30–65 | Taş Hasat - 3@2 | D 46/48 · O –/– · Y –/– · %60 |
| Uzun Hasat - 1 | Süre/tempo | süre +5 | 263I / 347I / 432I | R30–65 | Biraz Daha Zaman - 1@3 | D 20/31 · O 31/42 · Y 39/– · %100 |
| Uzun Hasat - 2 | Süre/tempo | süre +3 | 516I / 600I | R30–65 | Uzun Hasat - 1@3 | D 35/39 · O 48/– · Y –/– · %100 |
| Uzun Hasat - 3 | Süre/tempo | süre +5 | 684I / 768I | R30–65 | Uzun Hasat - 2@2 | D 41/43 · O –/– · Y –/– · %85 |
| Uzun Hasat - 4 | Süre/tempo | süre +7 | 853I / 937I | R30–65 | Uzun Hasat - 3@2 | D 45/47 · O –/– · Y –/– · %68 |
| İkinci Vazgeçiş | Kart | kart atlama +1 | 1800I | R30–65 | İlk Vazgeçiş@1 | D –/– · O –/– · Y –/– · %38 |
| Kesim Ustalığı - 1 | Hasar | hasar +%25 | 263I / 347I / 432I | R35–65 | Kesim Tekniği - 1@3, Güçlü Kesim - 1@3 | D 34/39 · O 39/47 · Y –/– · %100 |
| Kesim Ustalığı - 2 | Hasar | hasar +%16.7 | 516I / 600I | R35–65 | Kesim Ustalığı - 1@3 | D 45/49 · O –/– · Y –/– · %73 |
| Kesim Ustalığı - 3 | Hasar | hasar +%25 | 684I / 768I | R35–65 | Kesim Ustalığı - 2@2 | D 50/50 · O –/– · Y –/– · %53 |
| Kesim Ustalığı - 4 | Hasar | hasar +%33.3 | 853I / 937I | R35–65 | Kesim Ustalığı - 3@2 | D –/– · O –/– · Y –/– · %30 |
| Akıcı Kesim - 1 | Hız | saldırı aralığı ×0.9 | 439G / 579G / 719G | R40–70 | Hızlı Eller - 1@3 | D 38/44 · O 42/– · Y 50/– · %90 |
| Akıcı Kesim - 2 | Hız | saldırı aralığı ×0.924 | 860G / 1000G | R40–70 | Akıcı Kesim - 1@3 | D –/– · O –/– · Y –/– · %28 |
| Akıcı Kesim - 3 | Hız | saldırı aralığı ×0.92 | 1140G / 1281G | R40–70 | Akıcı Kesim - 2@2 | D –/– · O –/– · Y –/– · %23 |
| Akıcı Kesim - 4 | Hız | saldırı aralığı ×0.915 | 1421G / 1561G | R40–70 | Akıcı Kesim - 3@2 | D –/– · O –/– · Y –/– · %18 |
| Kart Sezgisi - 1 | Şans (kart) | kart nadirliği +0.1 | 1096G / 1447G / 1798G | R40–100 | İkinci Vazgeçiş@1 | D –/– · O –/– · Y –/– · %10 |
| Kart Sezgisi - 2 | Şans (kart) | kart nadirliği +0.0666667 | 2149G / 2500G | R40–100 | Kart Sezgisi - 1@3 | D –/– · O –/– · Y –/– · %8 |
| Kart Sezgisi - 3 | Şans (kart) | kart nadirliği +0.0666667 | 2851G / 3202G | R40–100 | Kart Sezgisi - 2@2 | D –/– · O –/– · Y –/– · %3 |
| Kart Sezgisi - 4 | Şans (kart) | kart nadirliği +0.0666667 | 3553G / 3904G | R40–100 | Kart Sezgisi - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Grid Genişleme II | Alan (grid) | grid = 11 | 120S / 450S | R45–85 | Grid Genişleme I@2 | D 23/44 · O 35/– · Y –/– · %90 |
| Verimli Üretim - 1 | Üretim | üretim aralığı ×0.95 | 285I / 376I / 468I | R45–75 | Düzenli Üretim - 1@3 | D 23/45 · O 38/– · Y –/– · %80 |
| Verimli Üretim - 2 | Üretim | üretim aralığı ×0.965 | 559I / 650I | R45–75 | Verimli Üretim - 1@3 | D –/– · O –/– · Y –/– · %28 |
| Verimli Üretim - 3 | Üretim | üretim aralığı ×0.964 | 741I / 832I | R45–75 | Verimli Üretim - 2@2 | D –/– · O –/– · Y –/– · %23 |
| Verimli Üretim - 4 | Üretim | üretim aralığı ×0.963 | 924I / 1015I | R45–75 | Verimli Üretim - 3@2 | D –/– · O –/– · Y –/– · %15 |
| Nadir Filizler - 1 | Nadirlik | nadir şansı +3 | 526I / 695I / 863I | R50–95 | Verimli Üretim - 1@3 | D –/– · O –/– · Y –/– · %13 |
| Nadir Filizler - 2 | Nadirlik | nadir şansı +2 | 1032I / 1200I | R50–95 | Nadir Filizler - 1@3 | D –/– · O –/– · Y –/– · %13 |
| Nadir Filizler - 3 | Nadirlik | nadir şansı +2.33333 | 1368I / 1537I | R50–95 | Nadir Filizler - 2@2 | D –/– · O –/– · Y –/– · %8 |
| Nadir Filizler - 4 | Nadirlik | nadir şansı +2.66667 | 1705I / 1874I | R50–95 | Nadir Filizler - 3@2 | D –/– · O –/– · Y –/– · %8 |
| Ağır Kesim - 1 | Hasar | hasar +333.333 | 146G / 193G / 240G | R65–78 | Güçlü Kesim - 1@3 | D 33/40 · O 39/43 · Y –/– · %100 |
| Ağır Kesim - 2 | Hasar | hasar +220 | 287G / 333G | R65–78 | Ağır Kesim - 1@3 | D 43/45 · O 45/47 · Y –/– · %90 |
| Ağır Kesim - 3 | Hasar | hasar +226.667 | 380G / 427G | R65–78 | Ağır Kesim - 2@2 | D 48/50 · O 49/– · Y –/– · %60 |
| Ağır Kesim - 4 | Hasar | hasar +220 | 474G / 520G | R65–78 | Ağır Kesim - 3@2 | D –/– · O –/– · Y –/– · %23 |
| Kritik Güç - 1 | Hasar (kritik) | kritik çarpanı +0.5 | 219I / 289I / 360I | R65–100 | Kritik Odak - 1@3 | D 30/45 · O 33/48 · Y 42/– · %83 |
| Kritik Güç - 2 | Hasar (kritik) | kritik çarpanı +0.333333 | 430I / 500I | R65–100 | Kritik Güç - 1@3 | D –/– · O –/– · Y –/– · %10 |
| Kritik Güç - 3 | Hasar (kritik) | kritik çarpanı +0.5 | 570I / 640I | R65–100 | Kritik Güç - 2@2 | D –/– · O –/– · Y –/– · %8 |
| Kritik Güç - 4 | Hasar (kritik) | kritik çarpanı +0.666667 | 711I / 781I | R65–100 | Kritik Güç - 3@2 | D –/– · O –/– · Y –/– · %3 |
| Seri Üretim - 1 | Üretim | üretim aralığı ×0.554 | 1515I / 2121I / 2425I | R65–100 | Verimli Üretim - 4@2 | D –/– · O –/– · Y –/– · %0 |
| Seri Üretim - 2 | Üretim | üretim aralığı ×0.5 | 2000I / 2800I / 3200I | R65–100 | Seri Üretim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Seri Üretim - 3 | Üretim | üretim aralığı ×0.5 | 2484I / 3478I / 3977I | R65–100 | Seri Üretim - 2@3 | D –/– · O –/– · Y –/– · %0 |
| Yıldırım Kesim - 1 | Hız | saldırı aralığı ×0.544 | 1894G / 2651G / 3031G | R65–100 | Akıcı Kesim - 4@2 | D –/– · O –/– · Y –/– · %0 |
| Yıldırım Kesim - 2 | Hız | saldırı aralığı ×0.5 | 2500G / 3500G / 4000G | R65–100 | Yıldırım Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Yıldırım Kesim - 3 | Hız | saldırı aralığı ×0.5 | 3106G / 4348G / 4970G | R65–100 | Yıldırım Kesim - 2@3 | D –/– · O –/– · Y –/– · %0 |
| Geniş Tarama - 1 | Alan | alan +%10 | 88S / 116S / 144S | R70–100 | Geniş Süpürüş - 1@3, Akıcı Kesim - 1@3 | D 45/45 · O –/– · Y –/– · %88 |
| Geniş Tarama - 2 | Alan | alan +%6.67 | 172S / 200S | R70–100 | Geniş Tarama - 1@3 | D 46/46 · O –/– · Y –/– · %78 |
| Geniş Tarama - 3 | Alan | alan +%6.67 | 228S / 256S | R70–100 | Geniş Tarama - 2@2 | D 47/48 · O –/– · Y –/– · %78 |
| Geniş Tarama - 4 | Alan | alan +%6.67 | 284S / 312S | R70–100 | Geniş Tarama - 3@2 | D 49/50 · O –/– · Y –/– · %55 |
| Zengin Cevher - 1 | Ekonomi | Iron +%60; Stone +%60 | 110S / 145S / 180S | R70–100 | Taş Hasat - 1@3 | D 35/37 · O 44/49 · Y –/– · %100 |
| Zengin Cevher - 2 | Ekonomi | Iron +%60; Stone +%60 | 215S / 250S | R70–100 | Zengin Cevher - 1@3 | D 39/41 · O –/– · Y –/– · %100 |
| Zengin Cevher - 3 | Ekonomi | Iron +%80; Stone +%80 | 285S / 320S | R70–100 | Zengin Cevher - 2@2 | D 45/50 · O –/– · Y –/– · %58 |
| Zengin Cevher - 4 | Ekonomi | Iron +%100; Stone +%100 | 355S / 390S | R70–100 | Zengin Cevher - 3@2 | D –/– · O –/– · Y –/– · %38 |
| Son Vardiya - 1 | Süre/tempo | süre +5 | 132S / 174S / 216S | R75–100 | Uzun Hasat - 1@3 | D 31/40 · O 45/– · Y –/– · %88 |
| Son Vardiya - 2 | Süre/tempo | süre +7 | 258S / 300S | R75–100 | Son Vardiya - 1@3 | D 49/– · O –/– · Y –/– · %43 |
| Son Vardiya - 3 | Süre/tempo | süre +6 | 342S / 384S | R75–100 | Son Vardiya - 2@2 | D –/– · O –/– · Y –/– · %28 |
| Son Vardiya - 4 | Süre/tempo | süre +7 | 426S / 468S | R75–100 | Son Vardiya - 3@2 | D –/– · O –/– · Y –/– · %20 |
| İleri Ustalık - 1 | Hasar | hasar +%50 | 1170G / 1544G / 1918G | R75–95 | Kesim Ustalığı - 1@3, Ağır Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| İleri Ustalık - 2 | Hasar | hasar +%33.3 | 2292G / 2667G | R75–95 | İleri Ustalık - 1@3 | D –/– · O –/– · Y –/– · %0 |
| İleri Ustalık - 3 | Hasar | hasar +%33.3 | 3041G / 3415G | R75–95 | İleri Ustalık - 2@2 | D –/– · O –/– · Y –/– · %0 |
| İleri Ustalık - 4 | Hasar | hasar +%33.3 | 3789G / 4164G | R75–95 | İleri Ustalık - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Plazma Kesim - 1 | Hasar | hasar +1333.33 | 731G / 965G / 1199G | R78–93 | Ağır Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Plazma Kesim - 2 | Hasar | hasar +886.667 | 1433G / 1667G | R78–93 | Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Plazma Kesim - 3 | Hasar | hasar +893.333 | 1901G / 2135G | R78–93 | Plazma Kesim - 2@2 | D –/– · O –/– · Y –/– · %0 |
| Plazma Kesim - 4 | Hasar | hasar +886.667 | 2368G / 2602G | R78–93 | Plazma Kesim - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Hasat Deneyimi II - 1 | XP | XP +%50 | 439G / 579G / 719G | R80–105 | Hasat Deneyimi I - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Deneyimi II - 2 | XP | XP +%33.3 | 860G / 1000G | R80–105 | Hasat Deneyimi II - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Deneyimi II - 3 | XP | XP +%33.3 | 1140G / 1281G | R80–105 | Hasat Deneyimi II - 2@2 | D –/– · O –/– · Y –/– · %0 |
| Hasat Deneyimi II - 4 | XP | XP +%33.3 | 1421G / 1561G | R80–105 | Hasat Deneyimi II - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Aşırı Güç I | Hasar | hasar ×8 | 1500S | R90–100 | Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Zirvesi - 1 | Hasar | hasar +%50 | 877G / 1158G / 1439G | R90–110 | İleri Ustalık - 1@3, Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Zirvesi - 2 | Hasar | hasar +%33.3 | 1719G / 2000G | R90–110 | Hasat Zirvesi - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Zirvesi - 3 | Hasar | hasar +%50 | 2281G / 2561G | R90–110 | Hasat Zirvesi - 2@2 | D –/– · O –/– · Y –/– · %0 |
| Hasat Zirvesi - 4 | Hasar | hasar +%66.7 | 2842G / 3123G | R90–110 | Hasat Zirvesi - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Yıldız Kesim - 1 | Hasar | hasar +700 | 1608I / 2123I / 2637I | R90–105 | Plazma Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Yıldız Kesim - 2 | Hasar | hasar +467 | 3152I / 3667I | R90–105 | Yıldız Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Yıldız Kesim - 3 | Hasar | hasar +500 | 4181I / 4696I | R90–105 | Yıldız Kesim - 2@2 | D –/– · O –/– · Y –/– · %0 |
| Yıldız Kesim - 4 | Hasar | hasar +533 | 5211I / 5725I | R90–105 | Yıldız Kesim - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Hasat Rekoru - 1 | Skor | skor +%25 | 292I / 386I / 480I | R95–110 | Zengin Cevher - 1@3 | D 46/– · O –/– · Y –/– · %0 |
| Hasat Rekoru - 2 | Skor | skor +%16.7 | 573I / 667I | R95–110 | Hasat Rekoru - 1@3 | D –/– · O –/– · Y –/– · %0 |
| Hasat Rekoru - 3 | Skor | skor +%25 | 760I / 854I | R95–110 | Hasat Rekoru - 2@2 | D –/– · O –/– · Y –/– · %0 |
| Hasat Rekoru - 4 | Skor | skor +%33.3 | 947I / 1041I | R95–110 | Hasat Rekoru - 3@2 | D –/– · O –/– · Y –/– · %0 |
| Aşırı Güç II | Hasar | hasar ×9 | 7500S | R100–110 | Aşırı Güç I@1, Yıldız Kesim - 1@3 | D –/– · O –/– · Y –/– · %0 |
