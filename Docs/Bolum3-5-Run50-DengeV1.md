# Bölüm 3.5 — Run50 Denge V1: 50 round güç, ekonomi, XP ve can dengesi

1 Ekim 2026. Kaynak belgeler: [Bolum3-1-Run50-GucVeIcerikPlani.md](Bolum3-1-Run50-GucVeIcerikPlani.md), [Bolum3-2-Run50-ReferansOlcum.md](Bolum3-2-Run50-ReferansOlcum.md), [Bolum3-4-BossVeIlkOduller.md](Bolum3-4-BossVeIlkOduller.md).

Bu paket `Run50_DengeV1` profilini ekler: aynı oyun kodu, 50 round için yeniden kurulmuş ağaç, can eğrisi, XP tablosu, ekonomi, boss ödülleri ve hedefler. **Bu bir "ilk dengelenmiş aday"dır.** Sayılar bot ölçümüyle ayarlandı; insan oyun testi yapılmadı. "Test geçti" işlevin çalıştığını söyler; dengenin ya da eğlencenin kesinleştiğini söylemez. Hâlâ baskın ve zayıf kalan seçenekler §14'te açıkça yazılı.

## Kısa özet

- **Oynanabilir profil:** `Tools > Run Profili > Run50 Denge V1`. Eski profiller, seçili profilin ve kaydın değişmedi; commit atılmadı.
- **Uçtan uca ölçüm (bot, R1'den, normal bütçe):** 44 run'ın 39'u R50'yi bitirdi. Yapılandırılmış politikalar 39 / 40; rastgele nişanlı "Yeni" bot 0 / 4 (ilk boss'u geçiyor, R10–R25'te eleniyor).
- **Tek vuruş geç geliyor:** Common tek vuruşu R10'da %15, R30'da %29, R50'de %72 (referans profilde R10'dan itibaren %94–100).
- **Tepe güç boss ödüllerinde:** ödül almayan run R30 boss'unda eleniyor; altı ödül yolunun altısı da R50'yi bitiriyor (hız, alan, hasar, davranış aynı mertebede; üretim-nadirlik ve XP zayıf).
- **Alan artık fark yaratıyor:** en iyi nişan 5 → 6 → 9 (ağaç) → 12 → 16 (ödül) hücre; eski tavan 6'ydı. İmleç halkası gerçek erişimi gösteriyor, hedefleme değişmedi.
- **Boss'lar:** Sert Kabuk ve Sis ("bir basamak" kuralı) her çıktıkları bantta ölçülebilir etki yapıyor; Don Cephesi erken ve orta oyunda etkili, geç oyunda hâlâ değil.
- **Hâlâ baskın:** hız + alan, Tüccar (ağacı neredeyse bitiriyor), R15'ten sonra ekonomi aileleri. **Hâlâ zayıf:** saf hasar + Dar Kesim, ilk yatırım olarak XP, üretim-nadirlik ödül yolu.
- **Testler:** 12 işlev testi paketi geçti (yeni `DengeV1Verification` 86 kontrol). Ölçümler test değildir; denge ve eğlence insan oyun testi olmadan kesinleşmiş sayılmaz.

## 1. Teslim edilenler

| İçerik | Durum |
|---|---|
| `Run50_DengeV1` profili ve menü satırı (`Tools > Run Profili > Run50 Denge V1 · 50 round (ilk denge adayı)`) | Oynanabilir |
| Profil bazlı veri ayrımı (`RunBalanceSO`, `SkillTreeSetSO`): eski profiller ortak veriyle, DengeV1 kendi verisiyle çalışır | Çalışıyor; eski profiller bayt bayt aynı (§15) |
| 50 round'a göre yeniden kurulmuş ağaç: 104 düğüm / 221 kademe (eski 140 / 304) | Çalışıyor |
| Yeni can eğrisi (R1 20 → R50 215, round başına ≈ ×1,05) ve doğrusal nadirlik çarpanı | Çalışıyor |
| Yeni XP tablosu, nadirliğe bağlı bitki XP'si | Çalışıyor |
| 13 boss ödülü (ilk altısının yeni değerli DengeV1 kopyaları + 7 yeni ödül), sunum penceresi / koşul / yığın sınırı veride | Çalışıyor |
| Boss'lar için ayrı DengeV1 verisi; Sis'in "bir basamak" kuralı | Çalışıyor |
| İmleç halkası gerçek hasat erişimini gösterir (hedefleme kuralı değişmedi) | Çalışıyor, bütün profillerde |
| R1'den başlayan gerçek run ölçüm botu (`BalanceRunMeasurement`) ve işlev testi (`DengeV1Verification`) | `Tools/Verification/Editor` |
| Üretici ve analiz betikleri | `Tools/Balance/DengeV1` |

Kapsam dışı bırakılanlar (istendiği gibi): Final Canavar Bitki, kalan çiftçi / tırpan içerikleri, level başına üç kart, ücretsiz reroll, yeşil saksı önizlemesi sorunu.

## 2. Nasıl oynanır

1. Unity menüsü: **Tools > Run Profili > Run50 Denge V1 · 50 round (ilk denge adayı)**. Seçili profil `Assets/Resources/RunProfileSelection.asset` içinde durur; bu dosyaya ben dokunmadım (hâlâ senin seçimini gösteriyor). Menüden seçince değişir, eski profile aynı menüden dönülür.
2. MenuScene'den Play → **Oyna** → çiftçi ve tırpan seç → **RUN'I BAŞLAT**.
3. Round 45 sn sürer (süre düğümü ve tempo yok). Her 5. round boss; R50 mevcut zafer akışıyla biter.

`Run50_Referans`, `Run50_BossPrototip` ve daha eski profiller değişmedi; aynı menüden aynı değerlerle oynanır.

## 3. Veri ayrımı (oyun kodu kopyalanmadı)

`RunProfileSO.balance` alanı bir `RunBalanceSO` gösterir. Boşsa (bütün eski profiller) oyun eskisi gibi ortak asset'leri okur; doluysa aynı yöneticiler aynı kodla o setteki veriyi okur. Set yalnız değiştirdiği şeyi taşır; boş bırakılan alan ortak veriye düşer.

| Veri | Eski profiller (ortak) | Run50_DengeV1 | Okuyan kod |
|---|---|---|---|
| Temel statlar | `Stats/CoreStat/CoreStat.asset` | `Balance/CoreStat_DengeV1` | `StatManager.Awake` |
| Bitki canı | `Resources/PlantHealthScaling` | `Balance/PlantHealth_DengeV1` | `PlantHealthCalculator` |
| XP tablosu | `Prograsiondata/Xp çarpanı` | `Balance/Progression_DengeV1` | `ProgressionManager.Awake` |
| Skill tree | `Skill Tree Upgrades/FinalSkillTree` (140) | `Skill Tree Upgrades/DengeV1` (104) + `Balance/SkillTreeSet_DengeV1` | `SkillTreeManager.Awake`, `SkillTreeUI.ApplyTreeSet`, `SkillNodeUI.Bind` |
| Bitki XP çarpanı (nadirlik) | ×1 | 1 / 1,5 / 2 / 2,5 / 3 | `PlantResource` |
| Saksı taban üretim aralığı | saksı asset'i (5 sn) | 8 sn | `PlanterSO.GetBaseStat` |
| Saksı fiyatı | saksı asset'i | saksı asset'i (sette satır yok; alan hazır) | `PlanterSO.Price / PriceType` |
| Round süresi | stat + süre düğümleri | profil: 45 sn sabit | `RunProfileSO.fixedRoundDuration` |
| Boss havuzu, boss hedefleri, ödül havuzu | 3.4 asset'leri | `SegmentEvents/DengeV1`, `Bosses/BossPool_DengeV1`, `BossRewards/DengeV1` | 3.4 kodu |

Ağaç arayüzü sahnedeki 140 yuvayı kullanmaya devam eder: `SkillTreeSetSO` her yuvaya (eski düğüm) DengeV1 düğümünü eşler; eşi olmayan 36 yuva gizlenir. Sahne dosyası değişmedi.

Değer değiştirmek için iki yol var:

- **Inspector'dan:** yukarıdaki asset'ler (tek tek).
- **Toplu:** `Tools/Balance/DengeV1/denge_v1_params.py` içindeki sayıları değiştir, `python Tools/Balance/DengeV1/make_denge_v1.py` çalıştır. Üretici yalnız DengeV1 asset'lerini yazar (GUID'ler sabit), ortak asset'lere dokunmaz. Dikkat: üretici Inspector'dan yapılmış DengeV1 değişikliklerinin üstüne yazar.

## 4. Temel değerler: önce / sonra

"Önce" sütunu `Run50_Referans` ve `Run50_BossPrototip` profillerinin kullandığı ortak veridir (değişmedi).

| Değer | Önce | Run50_DengeV1 | Not |
|---|---|---|---|
| Taban doğrudan hasar | 1 | 10 | Ölçek ×10: küçük yüzdeler tam sayıya yuvarlanırken kaybolmuyor. Can da aynı ölçekte |
| İlk hasar ailesi (Keskin Başlangıç, 4 düğüm) | +60 (tabanın 60 katı) · 177 G | +6 (tabanın 0,6 katı) · 101 G | Erken düz hasar sıçraması kaldırıldı |
| Common canı R1 / R10 / R25 / R50 | 5 / 15 / 35 / 98 | 20 / 31 / 62 / 215 | Taban hasarla 2,0 / 3,1 / 6,2 / 21,5 vuruş |
| Nadirlik can çarpanı (Common…Legendary) | 1 / 1,2 / 1,6 / 2 / 3 | 1 / 1,5 / 2 / 2,5 / 3 | Doğrusal: `Can = Common × (1 + 0,5 × basamak)` |
| Bitki XP'si | her bitki 20 | 20 × (1 / 1,5 / 2 / 2,5 / 3) | Sert bitki XP açısından zarar değil |
| Round süresi | 30 sn + süre düğümleri (+15 / +20 / +25) | 45 sn sabit | Süre düğümleri ağaçtan çıktı |
| Saksı taban üretim aralığı | 5 sn | 8 sn | Eski değerde üretim hep fazlaydı; üretim yatırımı karşılıksız kalıyordu |
| Başlangıç altını | 80 | 80 | |
| Saldırı aralığı, yarıçap, kritik tabanı | 3 sn · 1,0 · %30 şans, ×2 | aynı | |
| XP gereksinimi (level 1 / 10 / 30 / 50) | 605 / 564 / 1584 / 2334 | 391 / 645 / 2975 / 9505 | `380 + 10L + 1,2L² + 0,045L³` |
| Ağaç | 140 düğüm · 304 kademe · 156.899 G + 102.875 I + 16.320 S | 104 düğüm · 221 kademe · 68.495 G + 21.260 I + 5.885 S | |
| Segment kotaları (10 segment) | 10 × 1,45ⁿ (10 … 280) | 45 / 110 / 150 / 220 / 300 / 500 / 1200 / 2800 / 4500 / 6000 | Oyuncu gücüyle ölçeklenmez |
| Boss hasadı hedefleri (9 boss) | 15 / 40 / 60 / 80 / 105 / 120 / 135 / 150 / 170 | 8 / 16 / 30 / 45 / 65 / 100 / 220 / 500 / 800 | Oyuncu gücüyle ölçeklenmez |
| Boss ödülü | 6 ödül | 13 ödül | §6 |

## 5. Skill tree (50 round'a yerleştirildi)

130 round'luk fiyatlar sabit bir sayıya bölünmedi. Her ailenin etkisi, fiyatı, ön koşulu ve hedef round penceresi birlikte yeniden yazıldı; fiyatlar ölçülen gelire göre ayarlandı (§8). Kademe fiyatı aile içinde doğrusal artar (ilk kademe en ucuz). Tablolarda G = altın, I = demir, S = taş.

**Kökler (run başında görünen 5 düğüm):** Keskin Başlangıç-1, Hızlı Eller-1, Altın Hasat I-1, Düzenli Üretim-1, Grid Genişleme I. Hasar, hız, ekonomi ve üretim ilk round'dan seçilebilir; hiçbiri diğerinin ön koşulu değil.

**Ön koşul zinciri:** Keskin Başlangıç → Kesim Tekniği / Güçlü Kesim → Kesim Ustalığı / Ağır Kesim / Kritik Odak → Kritik Güç · Hızlı Eller → Akıcı Kesim / Geniş Süpürüş → Geniş Tarama · Düzenli Üretim → Verimli Üretim → Nadir Filizler; Düzenli Üretim → Hasat Deneyimi I → II · Altın Hasat I → Demir Hasat → Taş Hasat / Altın Hasat II → Zengin Cevher; Altın Hasat I → Hasat Rekoru.

| Aile | Eski etki | Eski fiyat · hedef | DengeV1 etki | DengeV1 fiyat · hedef |
|---|---|---|---|---|
| Altın Hasat I (4) | altın +%50 | 280 G · R1–20 | altın +%50 | 140 G · R1–12 |
| Biraz Daha Zaman (4) | süre +15 | 280 G · R1–15 | **ağaçtan çıktı** | — |
| Keskin Başlangıç (4) | hasar +60 | 177 G · R1–15 | hasar +6 | 101 G · R1–10 |
| Hızlı Eller (4) | saldırı aralığı ×0,7 | 399 G · R3–25 | saldırı aralığı ×0,72 | 129 G · R1–12 |
| Grid Genişleme I (1) | grid 7×7 | 80 G + 40 I · R4–18 | grid 7×7 | 30 G + 30 I · R2–15 |
| 1×3 Saksı (1) | kilit açar | 40 G · R5–7 | kilit açar | 30 G · R4–8 |
| Düzenli Üretim (4) | üretim aralığı ×0,85 | 799 G · R8–30 | üretim aralığı ×0,8 | 121 G · R3–18 |
| 2×2 Saksı (1) | kilit açar | 100 G · R10–13 | kilit açar | 80 G · R8–14 |
| Geniş Süpürüş (4) | yarıçap +%30 | 720 G · R10–30 | yarıçap +%35 | 419 G · R8–22 |
| Kesim Tekniği (4) | hasar +%50 | 600 G · R10–30 | hasar +%60 | 505 G · R8–22 |
| Patlayıcı Kartlar (1) | kilit açar | 20 I · R10–20 | kilit açar | 15 I · R6–15 |
| İlk Vazgeçiş (1) | kart atlama +1 | 240 G · R10–25 | kart atlama +1 | 150 G · R10–25 |
| Demir Hasat (4) | demir +%100 | 1.120 G · R15–45 | demir +%100 | 505 G · R10–28 |
| Hasat Deneyimi I (4) | XP +%50 | 881 G · R15–40 | XP +%60 | 125 G · R3–16 |
| Çifte Hasat Kartları (1) | kilit açar | 35 I · R15–25 | kilit açar | 25 I · R10–20 |
| 2×3 Saksı (1) | kilit açar | 180 G · R16–20 | kilit açar | 200 G · R14–22 |
| Kritik Odak (4) | kritik şansı +15 puan | 1.602 G · R20–65 | kritik şansı +15 puan | 2.995 G · R18–35 |
| Tornado Kartları (1) | kilit açar | 40 I · R20–30 | kilit açar | 30 I · R10–22 |
| Bumerang Orak Kartları (1) | kilit açar | 60 I · R25–40 | kilit açar | 40 I · R14–28 |
| Güçlü Kesim (4) | hasar +150 | 4.500 G · R25–60 | hasar +20 | 2.405 G · R15–32 |
| Çapraz Elektrik Kartları (1) | kilit açar | 80 I · R25–40 | kilit açar | 50 I · R12–28 |
| Altın Hasat II (4) | altın +%250 | 14.400 G · R30–65 | altın +%100 | 3.600 G · R20–36 |
| Taş Hasat (4) | taş +%100 | 5.850 I · R30–65 | taş +%100 | 700 I · R18–35 |
| Uzun Hasat (4) | süre +20 | 5.400 I · R30–65 | **ağaçtan çıktı** | — |
| İkinci Vazgeçiş (1) | kart atlama +1 | 1.800 I · R30–65 | kart atlama +1 | 800 I · R22–40 |
| Kesim Ustalığı (4) | hasar +%100 | 5.400 I · R35–65 | hasar +%80 | 11.070 I · R25–45 |
| Akıcı Kesim (4) | saldırı aralığı ×0,7 | 9.000 G · R40–70 | saldırı aralığı ×0,75 | 3.200 G · R15–32 |
| Kart Sezgisi (4) | kart şansı +0,3 | 22.500 G · R40–100 | kart şansı +0,2 | 14.030 G · R25–45 |
| Grid Genişleme II (1) | grid 11×11 | 570 S · R45–85 | grid 11×11 | 1.900 S · R22–45 |
| Verimli Üretim (4) | üretim aralığı ×0,85 | 5.850 I · R45–75 | üretim aralığı ×0,8 | 4.000 I · R22–40 |
| Nadir Filizler (4) | nadirlik +10 | 10.800 I · R50–95 | nadirlik +12 | 4.500 I · R20–42 |
| Ağır Kesim (4) | hasar +1000 | 3.000 G · R65–78 | hasar +30 | 24.000 G · R30–48 |
| Kritik Güç (4) | kritik çarpanı +2 | 4.500 I · R65–100 | kritik çarpanı +1 | 2.995 S · R32–48 |
| Seri Üretim (3) | üretim aralığı ×0,14 | 24.000 I · R65–100 | **ağaçtan çıktı** | — |
| Yıldırım Kesim (3) | saldırı aralığı ×0,14 | 30.000 G · R65–100 | **ağaçtan çıktı** | — |
| Geniş Tarama (4) | yarıçap +%30 | 1.800 S · R70–100 | yarıçap +%50 | 3.200 G · R20–36 |
| Zengin Cevher (4) | demir +%300; taş +%300 | 2.250 S · R70–100 | demir +%100; taş +%100 | 990 S · R28–45 |
| Son Vardiya (4) | süre +25 | 2.700 S · R75–100 | **ağaçtan çıktı** | — |
| İleri Ustalık (4) | hasar +%150 | 24.000 G · R75–95 | **ağaçtan çıktı** | — |
| Plazma Kesim (4) | hasar +4000 | 15.001 G · R78–93 | **ağaçtan çıktı** | — |
| Hasat Deneyimi II (4) | XP +%150 | 9.000 G · R80–105 | XP +%60 | 8.930 G · R25–42 |
| Aşırı Güç I (1) | hasar ×8 | 1.500 S · R90–100 | **ağaçtan çıktı** | — |
| Hasat Zirvesi (4) | hasar +%200 | 18.000 G · R90–110 | **ağaçtan çıktı** | — |
| Yıldız Kesim (4) | hasar +2200 | 33.000 I · R90–105 | **ağaçtan çıktı** | — |
| Hasat Rekoru (4) | skor +%100 | 6.000 I · R95–110 | skor +%100 | 3.600 G · R15–40 |
| Aşırı Güç II (1) | hasar ×9 | 7.500 S · R100–110 | **ağaçtan çıktı** | — |

Toplam: eski 140 düğüm / 304 kademe · 156.899 G + 102.875 I + 16.320 S — DengeV1 104 düğüm / 221 kademe · 68.495 G + 21.260 I + 5.885 S

**Ağaçtan çıkanlar (36 düğüm):** süre aileleri (Biraz Daha Zaman, Uzun Hasat, Son Vardiya), tepe hasar düğümleri (İleri Ustalık, Plazma Kesim, Hasat Zirvesi, Yıldız Kesim, Aşırı Güç I–II) ve tepe hız / üretim düğümleri (Yıldırım Kesim, Seri Üretim). Tepe güç boss ödüllerine taşındı (§6); süre profile taşındı.

**Ağacın tamamı beklenmiyor.** Ölçülen run'larda R50'de alınan kademe: nötr başlangıçlarda medyan 131–180 / 221 (%59–81), Tüccar'la 192–216 (%87–98; en yüksek tek run 219). Ağaç R45–50'de hâlâ alınıyor; en pahalı üç aile run'ların yarısında hiç alınmıyor (§8.3). Tüccar + Hız-alan kombinasyonu istisna: ağacı neredeyse bitiriyor (§14).

## 6. Tepe güç: ağaçtan boss ödüllerine

### 6.1 Ödül havuzu

Her boss'tan sonra 3 farklı seçenek sunulur, biri alınır; ücretsiz reroll yok. Sunum round'u, koşul, yığın sınırı ve ağırlık ödül asset'inde (`minRound`, `maxRound`, `condition`, `conditionThreshold`, `maxStacks`; ağırlık havuzda).

| Ödül | Etki (tek alış) | En çok | Tamamı | Sunulduğu boss round'u | Koşul |
|---|---|---|---|---|---|
| Keskin Bıçak | Doğrudan vuruş hasarı ×1,25 | 3 | ×1,95 | R5–45 | — |
| Canavar Kesimi | Doğrudan vuruş hasarı ×1,6 | 1 | ×1,6 | R25–45 | — |
| Kritik Göz | Kritik şansı +12 puan | 3 | +36 puan | R5–45 | — |
| Ağır Darbe | Kritik hasar çarpanı +0,75 | 2 | +1,5 | R5–45 | — |
| Altın Hedef | Rare ve üstü bitkiye doğrudan hasar ×1,5 | 2 | ×2,25 | R5–45 | — |
| Hızlı Bilek | Saldırı aralığı ×0,85 | 3 | ×0,61 | R5–45 | — |
| Fırtına Bileği | Saldırı aralığı ×0,7 | 1 | ×0,7 | R25–45 | — |
| Geniş Savuruş | Vuruş yarıçapı ×1,32 | 2 | ×1,74 | R5–45 | — |
| Kıvılcım | Mevcut davranış şansları ×1,35 | 3 | ×2,46 | R5–45 | şansı 0–100 arası davranış saksısı varsa |
| Yıkım Gücü | Davranış hasarı ×1,3 | 3 | ×2,2 | R5–45 | davranışı olan saksı varsa |
| Bereketli Toprak | Üretim aralığı ×0,85 | 3 | ×0,61 | R5–45 | tarla boşalıyorsa (doluluk < %65) |
| Nadir Tohum | Nadirlik bonusu +10 | 3 | +30 | R5–45 | — |
| Bilgi Filizi | Kazanılan XP ×1,25 | 2 | ×1,56 | R5–30 | — |

İlk altı ödülün prototipteki (3.4) asset'leri değişmedi; DengeV1 kendi kopyalarını kullanır:

| Ödül | Prototip (3.4) | DengeV1 |
|---|---|---|
| Keskin Bıçak | ×1,15 (3) | ×1,25 (3) |
| Hızlı Bilek | aralık ×0,90 (3) | aralık ×0,85 (3) |
| Bereketli Toprak | üretim aralığı ×0,90 (3), her zaman | ×0,85 (3), yalnız tarla boşalıyorsa |
| Nadir Tohum | +8 puan (3) | +10 puan (3) |
| Bilgi Filizi | XP ×1,25 (3) | XP ×1,25 (2), R30 boss'undan sonra çıkmaz |
| Kıvılcım | davranış şansları ×1,25 (3) | ×1,35 (3) |

Yeni yedi ödül: Canavar Kesimi, Kritik Göz, Ağır Darbe, Altın Hedef, Fırtına Bileği, Geniş Savuruş, Yıkım Gücü.

Doğrudan / davranış ayrımı metinde ve kodda aynı:

| Ödül | Metin | Kod | Test |
|---|---|---|---|
| Keskin Bıçak, Canavar Kesimi | "Yalnız kendi vuruşun. Davranış hasarı değişmez." | `BossRewardManager.DirectDamageMultiplier` → `PlayerController` | 3.4 testi + DengeV1 testi |
| Altın Hedef | "Rare ve üstü bitkiye doğrudan hasar; Common ve Uncommon değişmez." | `BossRewardManager.RareDirectMultiplier(rarity)` → `PlayerController` | Aynı zarla Rare 66 (×1,5), Common 44; davranış hasarı değişmedi |
| Yıkım Gücü | "Patlama · kasırga · bumerang · elektrik. Kendi vuruşun değişmez." | `BossRewardManager.BehaviorDamageMultiplier` → `PlanterBrain.BehaviorDamageMultiplier` | Elektrik 40 × 1,3 = 52; doğrudan vuruş 40 kaldı |
| Kıvılcım | "Şansı olmayan saksıya şans vermez; en çok %100." | Saksı davranış şansı statlarına `MorePercent` | 3.4 testi |
| Geniş Savuruş | "Yalnız kendi vuruşun. İmleç halkası büyür; davranış alanı değişmez." | Oyuncu `AreaRadius` `MorePercent` | Alan basamakları testi |

Koşullu ödüller: **Kıvılcım** yalnız şansı %0–100 arasında bir davranış saksısı varsa, **Yıkım Gücü** yalnız davranışı olan saksı varsa, **Bereketli Toprak** yalnız son round'un ortalama tarla doluluğu %65'in altındaysa (tarla boşalıyorsa) sunulur. **Canavar Kesimi** ve **Fırtına Bileği** R25 boss'undan itibaren, birer kez; **Bilgi Filizi** R30 boss'undan sonra çıkmaz.

### 6.2 Güç aktarım tablosu

"Eski" sütunu 130 round'luk ortak ağaçtır (değişmedi; eski profiller onu kullanır). "Beklenen" ödülün tam yığınıdır. "Ölçülen" teslim ölçümünün R50 medyanlarıdır (§6.3, §12).

| Güç | Eski ağaçtaki tepe | DengeV1 ağacında kalan (hedef round) | Boss ödülü (en çok) | Sunulduğu boss round'u | Beklenen katkı | Ölçülen |
|---|---|---|---|---|---|---|
| Doğrudan hasar çarpanı | Aşırı Güç I ×8 (R90–100), Aşırı Güç II ×9 (R100–110), İleri Ustalık +%150, Hasat Zirvesi +%200 | Kesim Tekniği +%60 (R8–22), Kesim Ustalığı +%80 (R25–45) | Keskin Bıçak ×1,25 (3) · Canavar Kesimi ×1,6 (1) | R5–45 · R25–45 | ×3,12 | Ödül payı ×1,58 – ×2,56 (tırpan hariç; 2–3 Keskin Bıçak ve / veya Canavar); ağaç + kart payı 49–150 hasar (taban 10) |
| Düz hasar | Güçlü Kesim +150, Ağır Kesim +1.000, Yıldız Kesim +2.200, Plazma Kesim +4.000 (taban 1) | Keskin Başlangıç +6, Güçlü Kesim +20, Ağır Kesim +30 (taban 10) | Ödüle taşınmadı, kaldırıldı. Değerli hedef için: Altın Hedef, Rare+ ×1,5 (2) | R5–45 | Rare+ ×2,25 | Altın Hedef yalnız Hasar politikasında alındı (4 run'da 3–6 kez); ayrı etkisi ölçülmedi |
| Kritik | Kritik Odak +15 puan, Kritik Güç +2 (R65–100) | Kritik Odak +15 puan (R18–35), Kritik Güç +1 (R32–48) | Kritik Göz +12 puan (3) · Ağır Darbe +0,75 (2) | R5–45 | +36 puan · +1,5 | "Hasar yolu" R46–50 hasadı 1.256 (ödülsüz taban R26–30'da 149 ve elendi) |
| Saldırı hızı | Yıldırım Kesim aralık ×0,14 (R65–100); Hızlı Eller ×0,7, Akıcı Kesim ×0,7 | Hızlı Eller ×0,72 (R1–12), Akıcı Kesim ×0,75 (R15–32): 3 sn → 1,62 sn | Hızlı Bilek ×0,85 (3) · Fırtına Bileği ×0,70 (1) | R5–45 · R25–45 | ×0,43 (1,62 → 0,70 sn) | Hız-alan: ödül payı ×0,51 – ×0,58 → 0,83–0,95 sn. Hız ödülü almayan Hasar politikası 1,81–1,90 sn |
| Alan | Geniş Süpürüş +%30 (R10–30), Geniş Tarama +%30 (R70–100): yarıçap 1,6 → 6 hücre | Geniş Süpürüş +%35 (R8–22), Geniş Tarama +%50 (R20–36): 1,85 → 9 hücre | Geniş Savuruş ×1,32 (2) | R5–45 | ×1,74 → 16 hücre | Hız-alan R50: 2,44–2,83 → 12–14 hücre (1–2 yığın); "alan yolu" 3,03 → 13 hücre |
| Üretim | Seri Üretim aralık ×0,14 (R65–100); Düzenli ×0,85, Verimli ×0,85 | Düzenli Üretim ×0,8 (R3–18), Verimli Üretim ×0,8 (R22–40): 8 sn → 5,1 sn | Bereketli Toprak ×0,85 (3), yalnız tarla boşalıyorsa | R5–45 | ×0,61 | R50 üretim aralığı 3,5–6,5 sn (kartlarla birlikte); Bereketli Toprak 4 run'da 0–5 kez alındı |
| Round süresi | Biraz Daha Zaman +15, Uzun Hasat +20, Son Vardiya +25 sn (30 → 90 sn) | — | — (profil: 45 sn sabit) | — | — | Her run 50 × 45 sn = 37,5 dk hasat |
| Nadirlik | Nadir Filizler +10 (R50–95) | Nadir Filizler +12 (R20–42) | Nadir Tohum +10 puan (3) | R5–45 | +30 puan | "Üretim-nadirlik yolu" en zayıf yol: R50 skoru 32.560 (alan yolu 119.498) |
| XP | Hasat Deneyimi I +%50, II +%150 (R80–105) | Hasat Deneyimi I +%60 (R3–16), II +%60 (R25–42) | Bilgi Filizi ×1,25 (2) | R5–30 | ×1,56 | "XP yolu" R50: level 64, skor 51.930 |
| Davranış | (kartlar) | (kartlar; kilitler R6–28) | Kıvılcım şans ×1,35 (3) · Yıkım Gücü hasar ×1,3 (3) | R5–45, koşullu | şans ×2,46 · hasar ×2,2 | Davranış politikası: R40–50'de hasadın %44–57'si davranıştan |
| Ekonomi | Altın Hasat II +%250, Zengin Cevher +%300 | Altın Hasat II +%100 (R20–36), Zengin Cevher +%100 (R28–45) | — | — | — | §8 |

Eski ağacın tepesinde doğrudan hasar ×72 çarpan + %500 + 7.400 düz hasar, saldırı aralığı ×0,069, üretim aralığı ×0,10 vardı. DengeV1 ağacının tamamı %140 + 56 düz hasar (taban 10), saldırı aralığı ×0,54, üretim aralığı ×0,64 verir. Aradaki tepe güç ödüllerdedir ve hepsi alınamaz: 9 boss = 9 ödül, havuzda 13 ödülün 31 yığını var.

### 6.3 Ölçülen katkı

**Ödül yolları (A/B).** Aynı satın alma politikası (Dengeli; davranış yolu için Davranış-rezonans) ve aynı iki seed; yalnız ödül tercih sırası değişir. Tercih ettiği ödül sunulmazsa bot kendi normal sırasına döner, yani yollar saf değildir (aşağıda alınan ödüller yazılı).

| Yol (öncelik) | 5 round'luk hasat R16–20 / R26–30 / R36–40 / R46–50 | R50 birikimli skor | R50 level · ağaç | Sonuç |
|---|---|---|---|---|
| Ödül yok | 160 / 149 / — / — | (R30: 2.384) | (R30: 28 · 70) | 2 / 2 **R30 boss hedefinde elendi** |
| Hasar (Keskin Bıçak, Canavar Kesimi, Kritik Göz, Ağır Darbe, Altın Hedef) | 262 / 484 / 790 / 1.256 | 86.498 | 61 · 162 | 2 / 2 kazandı |
| Hız (Hızlı Bilek, Fırtına Bileği) | 298 / 388 / 1.128 / 1.440 | 95.280 | 66 · 180 | 2 / 2 kazandı |
| Alan (Geniş Savuruş) | 298 / 527 / 1.146 / 1.492 | 119.498 | 70 · 187 | 2 / 2 kazandı |
| Üretim-nadirlik (Bereketli Toprak, Nadir Tohum) | 192 / 224 / 424 / 566 | 32.560 | 50 · 131 | 2 / 2 kazandı |
| Davranış (Yıkım Gücü, Kıvılcım) | 289 / 504 / 980 / 1.275 | 86.206 | 64 · 173 | 2 / 2 kazandı |
| XP (Bilgi Filizi) | 205 / 380 / 612 / 879 | 51.930 | 64 · 150 | 2 / 2 kazandı |

- **Tepe güç gerçekten ödüllerde.** Ödülsüz run'ın hasadı R15'ten sonra artmıyor (5 round'da 145 → 160 → 152 → 149): ağaç tek başına can eğrisine ancak yetişiyor ve run R30'da bitiyor. Hız, alan, hasar ve davranış yolları aynı noktada 2,6–3,5 kat hasat yapıyor (R26–30: 388–527'ye karşı 149; üretim-nadirlik 1,5 kat). Bu farkın bir kısmı dolaylı: daha çok hasat → daha çok gelir → daha çok ağaç (R30'da 70'e karşı 80–100 kademe).
- **Tepeye birden fazla yol var.** Altı yolun altısı da R50'yi bitiriyor; hız, alan, hasar ve davranış yolları aynı mertebede (R50 skoru 86–119 bin). Temel build tek bir nadir ödüle bağlı değil: hız (2 ödül, 4 yığın), hasar (5 ödül, 11 yığın), alan (1 ödül, 2 yığın) ve davranış (2 ödül, 6 yığın) yolları birbirinin yerine geçebiliyor.
- **Zayıf yollar:** üretim-nadirlik (skor en iyinin %27'si) ve XP (%43). İkisi de run'ı bitiriyor ama "tepe" sayılmaz.

**Run içinde ağaç payı ve ödül payı** (teslim ölçümü, medyan). Doğrudan hasar sütununda tırpan çarpanı (Dar Kesim ×1,4) ödül payının içindedir.

| Politika · başlangıç | Round | Doğrudan hasar: ağaç + kart × ödül (+ tırpan) = toplam | Saldırı aralığı: ağaç + kart × ödül = toplam | Yarıçap: ağaç + kart × ödül = toplam → en iyi nişanda hücre |
|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | R25 | 20 × 1,25 = 25 | 2,03 sn × 0,79 = 1,59 sn | 1,27 × 1,16 = 1,50 → 6 |
| Hız-alan · Bahçıvan + Standart | R50 | 82 × 1,98 = 148 | 1,62 sn × 0,58 = 0,93 sn | 1,85 × 1,32 = 2,44 → 12 |
| Hasar-değerli hedef · Bahçıvan + Standart | R25 | 29 × 1,25 = 37 | 2,16 sn × 1,00 = 2,16 sn | 1,16 × 1,00 = 1,16 → 5 |
| Hasar-değerli hedef · Bahçıvan + Standart | R50 | 60 × 2,00 = 118 | 1,87 sn × 1,00 = 1,81 sn | 1,46 × 1,00 = 1,46 → 6 |
| Davranış-rezonans · Bahçıvan + Standart | R25 | 32 × 1,00 = 33 | 2,09 sn × 0,86 = 1,78 sn | 1,19 × 1,00 = 1,19 → 5 |
| Davranış-rezonans · Bahçıvan + Standart | R50 | 95 × 1,58 = 150 | 1,67 sn × 0,86 = 1,45 sn | 1,74 × 1,32 = 2,41 → 12 |
| Erken XP-ekonomi · Bahçıvan + Standart | R25 | 25 × 1,25 = 28 | 2,09 sn × 0,79 = 1,67 sn | 1,21 × 1,00 = 1,23 → 5 |
| Erken XP-ekonomi · Bahçıvan + Standart | R50 | 49 × 1,98 = 97 | 1,81 sn × 0,73 = 1,31 sn | 1,54 × 1,32 = 2,04 → 9 |
| Erken XP-ekonomi · Tüccar + Standart | R25 | 30 × 1,12 = 34 | 1,99 sn × 0,86 = 1,71 sn | 1,27 × 1,16 = 1,50 → 6 |
| Erken XP-ekonomi · Tüccar + Standart | R50 | 93 × 1,78 = 164 | 1,62 sn × 0,73 = 1,19 sn | 1,77 × 1,32 = 2,33 → 12 |
| Hız-alan · Tüccar + Standart | R25 | 30 × 1,12 = 34 | 1,90 sn × 0,79 = 1,49 sn | 1,46 × 1,53 = 2,20 → 10 |
| Hız-alan · Tüccar + Standart | R50 | 150 × 1,78 = 244 | 1,62 sn × 0,51 = 0,83 sn | 1,85 × 1,53 = 2,83 → 14 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R25 | 29 × 1,75 = 50 | 2,16 sn × 1,00 = 2,16 sn | 0,88 × 1,00 = 0,88 → 4 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R50 | 56 × 2,77 = 161 | 1,90 sn × 1,00 = 1,90 sn | 1,08 × 1,00 = 1,08 → 5 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R25 | 27 × 1,00 = 29 | 2,13 sn × 0,93 = 1,97 sn | 1,19 × 1,16 = 1,39 → 6 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R50 | 72 × 1,58 = 114 | 1,73 sn × 0,80 = 1,39 sn | 1,60 × 1,53 = 2,34 → 12 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R25 | 29 × 1,97 = 58 | 2,16 sn × 1,00 = 2,16 sn | 0,90 × 1,00 = 0,90 → 4 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R50 | 60 × 3,59 = 196 | 1,87 sn × 1,00 = 1,87 sn | 1,10 × 1,00 = 1,10 → 5 |
| Hız-alan · Bahçıvan + Dar Kesim | R25 | 22 × 1,75 = 39 | 1,96 sn × 0,79 = 1,57 sn | 1,00 × 1,16 = 1,17 → 5 |
| Hız-alan · Bahçıvan + Dar Kesim | R50 | 71 × 2,77 = 179 | 1,62 sn × 0,58 = 0,95 sn | 1,35 × 1,32 = 1,78 → 8 |

- R25'te ödül payı küçük (tırpan hariç hasar ×1,0–1,4, aralık ×0,79–1,0); R50'de hasar ×1,6–2,6, aralık ×0,51–0,86 (hız ödülü alanlarda), yarıçap ×1,32–1,53. Güç artışı geç oyunda ödülden geliyor: istenen "geç oyunda ödüller build'i görünür biçimde hızlandırır ve genişletir" davranışı.
- Alınan ödüller (politika başına 4 run, alınan / sunulan):

| Politika · başlangıç | Alınan ödüller (alınan / sunulan) |
|---|---|
| Hız-alan · Bahçıvan + Standart | hizli_bilek 8/8, keskin_bicak 7/9, nadir_tohum 7/14, genis_savurus 5/8, canavar_kesimi 3/3, bereketli_toprak 2/3, kritik_goz 2/12, firtina_bilegi 2/2 |
| Hasar-değerli hedef · Bahçıvan + Standart | keskin_bicak 8/8, kritik_goz 8/11, nadir_tohum 7/16, altin_hedef 6/9, agir_darbe 3/11, canavar_kesimi 3/3, firtina_bilegi 1/5 |
| Davranış-rezonans · Bahçıvan + Standart | kivilcim 9/9, yikim_gucu 6/8, hizli_bilek 5/7, genis_savurus 4/11, keskin_bicak 4/12, bereketli_toprak 3/5, canavar_kesimi 2/4, firtina_bilegi 1/3, bilgi_filizi 1/8, nadir_tohum 1/15 |
| Erken XP-ekonomi · Bahçıvan + Standart | hizli_bilek 8/9, keskin_bicak 7/9, nadir_tohum 6/13, genis_savurus 5/9, bilgi_filizi 4/8, canavar_kesimi 3/4, firtina_bilegi 1/2, bereketli_toprak 1/6, kritik_goz 1/9 |
| Yeni · Bahçıvan + Standart | bereketli_toprak 2/5, bilgi_filizi 2/4, nadir_tohum 2/3, genis_savurus 1/4, agir_darbe 1/3, hizli_bilek 1/4, keskin_bicak 1/2 |
| Erken XP-ekonomi · Tüccar + Standart | hizli_bilek 8/9, nadir_tohum 7/13, keskin_bicak 6/8, genis_savurus 5/13, bilgi_filizi 4/8, canavar_kesimi 3/5, bereketli_toprak 2/4, firtina_bilegi 1/2 |
| Hız-alan · Tüccar + Standart | hizli_bilek 8/8, genis_savurus 6/8, keskin_bicak 6/12, bereketli_toprak 5/8, nadir_tohum 5/14, firtina_bilegi 3/3, canavar_kesimi 2/3, kritik_goz 1/10 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | kritik_goz 8/11, keskin_bicak 7/7, nadir_tohum 7/15, agir_darbe 4/11, altin_hedef 4/8, canavar_kesimi 3/3, hizli_bilek 1/10, firtina_bilegi 1/4, genis_savurus 1/8 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | yikim_gucu 7/8, kivilcim 7/7, genis_savurus 5/11, keskin_bicak 4/12, hizli_bilek 4/7, bereketli_toprak 3/6, canavar_kesimi 2/4, firtina_bilegi 2/4, nadir_tohum 2/16 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | keskin_bicak 7/7, kritik_goz 7/9, nadir_tohum 7/15, agir_darbe 5/11, canavar_kesimi 4/4, altin_hedef 3/7, hizli_bilek 1/13, firtina_bilegi 1/3, genis_savurus 1/9 |
| Hız-alan · Bahçıvan + Dar Kesim | hizli_bilek 9/9, keskin_bicak 7/9, nadir_tohum 6/14, genis_savurus 5/8, bereketli_toprak 3/6, canavar_kesimi 3/3, firtina_bilegi 2/2, kritik_goz 1/12 |

## 7. Bitki canı

| Round | 1 | 5 | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 50 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Eski Common canı (hasar tabanı 1) | 5 | 9 | 15 | 20 | 27 | 35 | 44 | 53 | 66 | 80 | 98 |
| DengeV1 Common canı (hasar tabanı 10) | 20 | 25 | 31 | 39 | 49 | 62 | 79 | 102 | 132 | 170 | 215 |
| DengeV1 Legendary canı | 60 | 75 | 93 | 117 | 147 | 186 | 237 | 306 | 396 | 510 | 645 |
| Round başına büyüme (DengeV1) | — | ×1,06 | ×1,04 | ×1,05 | ×1,05 | ×1,05 | ×1,05 | ×1,05 | ×1,05 | ×1,05 | ×1,05 |

Nadirlik çarpanı (Common … Legendary): eski 1 / 1,2 / 1,6 / 2 / 3 — DengeV1 1 / 1,5 / 2 / 2,5 / 3 (Can = Common × (1 + 0,5 × basamak))

- Eğri tekdüze artar; round'dan round'a büyüme ≈ ×1,05, en büyük tek adım ×1,10 (ilk round'larda tam sayıya yuvarlama). Segment sınırında duvar yok (boss round'unun canı komşu round'larla aynı eğridedir; yalnız Sert Kabuk şeridi ×1,5 alır).
- Can oyuncu hasarıyla ölçeklenmez: yalnız round ve nadirliğe bağlıdır.
- Nadirlik doğrusal: `Can = TemelCan(round) × (1 + 0,5 × basamak)`.

**Ölçülen vuruş sayısı, tek vuruş oranı ve hasat süresi** (44 run'lık teslim ölçümü):

**Bütün yapılandırılmış politikalar** (medyan; Common / Uncommon / Rare / Epic / Legendary)

| Round | Öldürmek için vuruş | Tek vuruşta ölen % | İlk vuruştan hasada süre (sn) |
|---|---|---|---|
| R1 | 1,9 / 3,0 / 5,0 / – / – | 18 / 0 / 0 / – / – | 2,6 / 6,0 / 12,0 / – / – |
| R5 | 1,9 / 2,5 / 3,5 / 4,0 / – | 8 / 0 / 0 / 0 / – | 4,3 / 6,9 / 9,4 / 15,2 / – |
| R10 | 1,8 / 2,4 / 3,0 / 3,0 / – | 15 / 0 / 0 / 0 / – | 4,5 / 6,3 / 7,8 / 7,0 / – |
| R15 | 1,7 / 2,4 / 3,0 / 3,0 / 2,5 | 14 / 0 / 0 / 0 / 0 | 4,7 / 7,5 / 9,0 / 5,1 / 3,6 |
| R20 | 1,6 / 2,2 / 2,6 / 3,0 / 3,5 | 26 / 0 / 0 / 0 / 0 | 3,5 / 6,9 / 8,4 / 8,1 / 7,5 |
| R25 | 1,6 / 2,0 / 2,2 / 2,5 / 3,0 | 25 / 4 / 0 / 0 / 0 | 3,9 / 5,8 / 7,0 / 7,0 / 7,1 |
| R30 | 1,5 / 1,8 / 2,1 / 2,3 / 2,5 | 29 / 12 / 0 / 0 / 0 | 3,1 / 5,4 / 6,6 / 7,0 / 5,1 |
| R35 | 1,3 / 1,6 / 1,8 / 2,1 / 2,0 | 40 / 12 / 10 / 0 / 0 | 2,6 / 3,6 / 4,1 / 6,4 / 4,9 |
| R40 | 1,3 / 1,5 / 1,7 / 2,0 / 2,0 | 47 / 30 / 18 / 6 / 0 | 1,6 / 2,5 / 3,5 / 4,2 / 5,6 |
| R45 | 1,3 / 1,5 / 1,7 / 2,0 / 2,0 | 50 / 36 / 25 / 20 / 0 | 1,4 / 3,1 / 4,1 / 4,3 / 5,9 |
| R50 | 1,2 / 1,4 / 1,5 / 1,8 / 1,7 | 72 / 54 / 43 / 34 / 12 | 1,2 / 1,7 / 2,3 / 2,8 / 2,6 |

**Hasar-değerli hedef** (medyan; Common / Uncommon / Rare / Epic / Legendary)

| Round | Öldürmek için vuruş | Tek vuruşta ölen % | İlk vuruştan hasada süre (sn) |
|---|---|---|---|
| R1 | 1,8 / 2,6 / 3,8 / – / – | 18 / 0 / 0 / – / – | 2,5 / 4,8 / 8,2 / – / – |
| R5 | 1,6 / 2,3 / 2,2 / 4,0 / – | 26 / 0 / 0 / 0 / – | 3,6 / 5,0 / 4,4 / 9,0 / – |
| R10 | 1,4 / 1,9 / 2,0 / 3,0 / – | 38 / 20 / 0 / 0 / – | 3,7 / 4,7 / 3,5 / 5,6 / – |
| R15 | 1,5 / 2,1 / 2,4 / 2,5 / 2,5 | 33 / 15 / 0 / 0 / 0 | 3,6 / 5,7 / 3,7 / 3,7 / 3,6 |
| R20 | 1,3 / 1,7 / 2,3 / 2,0 / 3,0 | 44 / 26 / 20 / 0 / 0 | 1,6 / 3,4 / 3,9 / 4,3 / 4,4 |
| R25 | 1,3 / 1,5 / 1,7 / 2,0 / 2,8 | 43 / 42 / 36 / 0 / 0 | 1,7 / 3,7 / 2,6 / 2,9 / 4,0 |
| R30 | 1,2 / 1,3 / 1,6 / 1,8 / 2,2 | 78 / 50 / 34 / 15 / 0 | 0,8 / 2,2 / 2,9 / 2,6 / 2,1 |
| R35 | 1,1 / 1,5 / 1,6 / 1,7 / 2,0 | 64 / 24 / 26 / 29 / 12 | 1,9 / 2,2 / 2,4 / 1,5 / 2,1 |
| R40 | 1,1 / 1,2 / 1,3 / 1,6 / 1,8 | 80 / 58 / 60 / 41 / 45 | 0,7 / 1,0 / 1,4 / 1,7 / 1,4 |
| R45 | 1,1 / 1,2 / 1,3 / 1,4 / 1,5 | 78 / 66 / 66 / 60 / 50 | 0,3 / 1,2 / 2,0 / 0,9 / 0,9 |
| R50 | 1,1 / 1,2 / 1,3 / 1,4 / 1,4 | 78 / 68 / 70 / 68 / 55 | 0,7 / 1,7 / 1,3 / 1,0 / 1,0 |

**Hız-alan** (medyan; Common / Uncommon / Rare / Epic / Legendary)

| Round | Öldürmek için vuruş | Tek vuruşta ölen % | İlk vuruştan hasada süre (sn) |
|---|---|---|---|
| R1 | 1,8 / 3,0 / 5,0 / – / – | 20 / 0 / 0 / – / – | 2,5 / 6,0 / 12,0 / – / – |
| R5 | 2,2 / 2,5 / 4,0 / 4,0 / – | 0 / 0 / 0 / 0 / – | 4,8 / 6,9 / 12,3 / 19,5 / – |
| R10 | 2,1 / 2,6 / 3,0 / – / – | 9 / 0 / 0 / 0 / – | 5,5 / 11,7 / 8,9 / – / – |
| R15 | 2,0 / 2,5 / 3,0 / 2,0 / 3,0 | 7 / 0 / 0 / 0 / – | 5,9 / 8,2 / 11,9 / 2,1 / 14,7 |
| R20 | 1,9 / 2,2 / 2,6 / 3,0 / – | 11 / 0 / 0 / 0 / 0 | 4,8 / 6,9 / 9,7 / 8,2 / – |
| R25 | 1,8 / 2,3 / 2,7 / 2,6 / 3,5 | 20 / 0 / 0 / 0 / 0 | 4,2 / 7,6 / 10,3 / 9,5 / 12,8 |
| R30 | 1,6 / 2,2 / 2,4 / 2,8 / 2,8 | 20 / 0 / 0 / 0 / 0 | 4,3 / 6,1 / 8,4 / 9,9 / 11,7 |
| R35 | 1,5 / 1,6 / 2,2 / 2,2 / 2,0 | 36 / 13 / 0 / 0 / 0 | 2,8 / 4,0 / 8,0 / 8,0 / 6,0 |
| R40 | 1,4 / 1,6 / 2,0 / 2,2 / 2,4 | 42 / 27 / 6 / 0 / 0 | 2,2 / 2,5 / 5,0 / 6,4 / 7,6 |
| R45 | 1,5 / 1,6 / 2,0 / 2,1 / 2,3 | 41 / 34 / 16 / 10 / 0 | 2,0 / 3,0 / 4,8 / 6,0 / 7,8 |
| R50 | 1,2 / 1,4 / 1,5 / 1,8 / 1,9 | 74 / 52 / 42 / 23 / 10 | 0,7 / 1,5 / 2,2 / 2,7 / 4,4 |

- R1–R25: Common 1,6–2 vuruşta, Rare 2,2–5 vuruşta (R1'de 5, R25'te 2,2) ölüyor. Tek vuruş Common'da %8–26, Rare ve üstünde 0.
- Tek vuruş çoğunluğa ancak R40–R50'de geçiyor (Common %47 → %72; Legendary R50'de %12). `Run50_Referans`'ta tek vuruş R10'dan itibaren %94–100'dü (Bölüm 3.2): tek vuruş artık oyun sonunun özelliği.
- Hasar yatırımı fark ediyor: Hasar politikasında Common tek vuruşu R30'da %78, R50'de bütün nadirliklerde %55–78. Hız-alan politikasında Common R30'da %20, R50'de %74; Legendary R50'de %10.
- Hasat süresi (ilk vuruştan ölüme): Common R5–15'te 4–5 sn, R50'de 1,2 sn; Legendary R20–25'te ≈ 7 sn, R50'de 2,6 sn.

**Legendary'nin fazladan canı karşılığını veriyor mu?**

| | Common | Uncommon | Rare | Epic | Legendary |
|---|---|---|---|---|---|
| Can çarpanı | 1 | 1,5 | 2 | 2,5 | 3 |
| Hasat skoru | 1 | 3 | 10 | 30 | 100 |
| XP (DengeV1) | 20 | 30 | 40 | 50 | 60 |
| Can başına skor (Common = 1) | 1 | 2 | 5 | 12 | 33 |
| Can başına XP (Common = 1) | 1 | 1 | 1 | 1 | 1 |
| Kaynak (bitki asset'i, değişmedi) | Ot 2 altın | Mısır / Pancar 4 altın | Havuç / Marul / Çilek 4 demir | Biber / Patates / Domates 2 taş | Üzüm 30 altın · Ananas 15 demir · Balkabağı 8 taş |

Legendary ×3 can karşılığında ×100 skor, ×3 XP ve altında ×15 kaynak veriyor; ölçülen ek maliyet 1–2 vuruş (R25'te 3,0'a karşı 1,6; R50'de 1,7'ye karşı 1,2). Her round'da vurmaya değer; XP can ile aynı oranda arttığı için sert bitki XP kaybı yaratmıyor. Zayıf kalan nokta kaynak tarafı: Rare ve Epic bitkiler ×2–×2,5 can için 2–4 demir / taş veriyor; skor için değerliler, kaynak için değil (bitki asset'leri ortak olduğu için bu pakette değişmedi).

## 8. Ekonomi

Başlangıç bütçesi, hasat geliri, saksı / grid fiyatları ve ağaç fiyatları birlikte ayarlandı: fiyatlar her turda ölçülen gelire göre düzeltildi (§18).

### 8.1 İlk segment: tek bir zorunlu açılış var mı?

Aynı başlangıç (Bahçıvan + Standart, 80 altın), 3 seed, ilk 5 round. Her açılış önce iki saksı alır (bitkisiz tarla anlamsız), sonra belirtilen aileyi bitirir, kalanıyla saksı alır.

| Açılış | R5 hasadı | R1–5 segment skoru (kota 45) | Boss hasadı (hedef 8) | İlk boss |
|---|---|---|---|---|
| Hiçbir şey almaz (saksı da yok) | 0 | 0 | 0 | 3 / 3 elenir |
| Yalnız saksı | 17 [14–18] | 173 | 31 [25–50] | geçer |
| Önce hasar (Keskin Başlangıç) | 21 [18–21] | 162 | 43 [38–45] | geçer |
| Önce hız (Hızlı Eller) | 14 [14–17] | 132 | 46 [30–49] | geçer |
| Önce ekonomi (Altın Hasat I) | 16 [16–20] | 121 | 38 [20–42] | geçer |
| Önce üretim (Düzenli Üretim) | 14 [10–15] | 125 | 30 [25–31] | geçer |
| "Yeni" bot · Standart | 8 [7–10] | 109 | 43 [20–43] | geçer |
| "Yeni" bot · Dar Kesim | 7 [7–8] | 60 | 19 [19–20] | geçer |

Saksı alan her açılış ilk boss'u ×2,4 – ×5,8 payla geçiyor; hasar, hız, ekonomi ya da üretimle başlamak arasında ilk boss açısından zorunlu bir seçim yok. Tek zorunluluk saksı almak. Rastgele nişanlı bot da (iki tırpanla da) geçiyor.

### 8.2 Hasat geliri

| Politika · başlangıç | Σ altın R10 / R20 / R30 / R40 / R50 | Σ demir R20 / R30 / R40 / R50 | Σ taş R30 / R40 / R50 | R50 kasada kalan (A / D / T) |
|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 468 / 1710 / 4460 / 9697 / 24838 | 174 / 766 / 3779 / 13030 | 106 / 1142 / 4414 | 2621 / 1760 / 1199 |
| Hasar-değerli hedef · Bahçıvan + Standart | 474 / 1554 / 3300 / 5889 / 10229 | 168 / 878 / 2641 / 5454 | 213 / 796 / 1851 | 588 / 766 / 171 |
| Davranış-rezonans · Bahçıvan + Standart | 514 / 2097 / 5204 / 11434 / 26266 | 324 / 1452 / 5392 / 15525 | 284 / 1561 / 4942 | 2170 / 1686 / 1498 |
| Erken XP-ekonomi · Bahçıvan + Standart | 481 / 1760 / 3652 / 6053 / 11694 | 158 / 608 / 1775 / 4949 | 72 / 456 / 1362 | 898 / 689 / 292 |
| Yeni · Bahçıvan + Standart | 247 / 894 / – / – / – | 94 / – / – / – | – / – / – | – / – / – |
| Erken XP-ekonomi · Tüccar + Standart | 740 / 2674 / 5815 / 13181 / 32036 | 390 / 2025 / 6033 / 18748 | 488 / 1684 / 6107 | 3298 / 2274 / 1142 |
| Hız-alan · Tüccar + Standart | 682 / 2874 / 8232 / 27235 / 66474 | 340 / 2407 / 12548 / 34662 | 442 / 3699 / 12099 | 6584 / 13260 / 6924 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | 468 / 1566 / 3215 / 5747 / 8896 | 267 / 896 / 2582 / 4970 | 220 / 819 / 1672 | 603 / 645 / 302 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | 434 / 1854 / 4278 / 8344 / 17338 | 221 / 800 / 2848 / 8024 | 128 / 812 / 2556 | 1555 / 862 / 876 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 576 / 1830 / 3296 / 6318 / 10490 | 233 / 978 / 2634 / 5290 | 310 / 890 / 2037 | 550 / 748 / 327 |
| Hız-alan · Bahçıvan + Dar Kesim | 613 / 2312 / 5084 / 9488 / 20834 | 288 / 1018 / 3696 / 10924 | 188 / 891 / 3435 | 1556 / 1250 / 966 |

Ağacın tamamı 68.495 altın + 21.260 demir + 5.885 taş. Nötr başlangıçta R50'ye kadar toplanan altın 9–26 bin: ağacın altın tarafının %13–38'i (saksılar da aynı kasadan). Yani fiyatlar bütçe rekabeti yaratıyor; istisna Tüccar + Hız-alan (66 bin altın, §14).

### 8.3 Erişim zamanı

40 yapılandırılmış run'da her ailenin ilk ve son alındığı round:

| Aile / düğüm | İlk kademe (medyan [en erken–en geç]) | Son alınan kademe (medyan) | Alınan kademe (medyan) | Aileye giren run |
|---|---|---|---|---|
| Keskin Başlangıç | R4 [1–8] | R13 | 9 | 40 / 40 |
| Hızlı Eller | R4 [1–8] | R16 | 9 | 40 / 40 |
| Altın Hasat I | R4 [1–5] | R16 | 9 | 40 / 40 |
| Grid Genişleme I | R5 [4–7] | R11 | 2 | 40 / 40 |
| Düzenli Üretim | R5 [2–8] | R19 | 9 | 40 / 40 |
| Hasat Deneyimi I | R6 [4–12] | R20 | 9 | 40 / 40 |
| 1×3 Saksı | R8 [6–9] | R8 | 1 | 40 / 40 |
| Patlayıcı Kartlar | R8 [6–9] | R8 | 1 | 40 / 40 |
| Kesim Tekniği | R12 [8–17] | R32 | 9 | 40 / 40 |
| Tornado Kartları | R12 [8–17] | R12 | 1 | 40 / 40 |
| 2×2 Saksı | R14 [11–17] | R14 | 1 | 40 / 40 |
| Çifte Hasat Kartları | R14 [11–22] | R14 | 1 | 40 / 40 |
| Geniş Süpürüş | R15 [7–19] | R34 | 9 | 40 / 40 |
| Demir Hasat | R15 [7–18] | R38 | 9 | 40 / 40 |
| Çapraz Elektrik Kartları | R18 [12–26] | R18 | 1 | 40 / 40 |
| Bumerang Orak Kartları | R19 [14–27] | R19 | 1 | 40 / 40 |
| Taş Hasat | R20 [16–28] | R36 | 9 | 40 / 40 |
| Güçlü Kesim | R20 [16–29] | R47 | 8 | 40 / 40 |
| 2×3 Saksı | R22 [17–27] | R22 | 1 | 40 / 40 |
| Akıcı Kesim | R22 [14–33] | R46 | 7 | 40 / 40 |
| Hasat Rekoru | R24 [17–31] | R47 | 5 | 40 / 40 |
| Verimli Üretim | R27 [20–37] | R48 | 4 | 40 / 40 |
| İlk Vazgeçiş | R28 [14–38] | R28 | 1 | 40 / 40 |
| Grid Genişleme II | R30 [22–38] | R32 | 1 | 40 / 40 |
| Kritik Odak | R30 [22–49] | R48 | 5 | 38 / 40 |
| Nadir Filizler | R31 [23–38] | R48 | 5 | 40 / 40 |
| Altın Hasat II | R32 [22–45] | R48 | 5 | 40 / 40 |
| Zengin Cevher | R32 [24–41] | R46 | 9 | 40 / 40 |
| Geniş Tarama | R34 [18–48] | R47 | 6 | 38 / 40 |
| Kesim Ustalığı | R38 [28–49] | R48 | 3 | 40 / 40 |
| İkinci Vazgeçiş | R41 [31–47] | R41 | 1 | 26 / 40 |
| Hasat Deneyimi II | R41 [32–49] | R48 | 4 | 23 / 40 |
| Kritik Güç | R41 [32–49] | R48 | 5 | 34 / 40 |
| Ağır Kesim | R43 [35–48] | R48 | 2 | 19 / 40 |
| Kart Sezgisi | R44 [35–49] | R48 | 2 | 20 / 40 |

- Erken aileler (Keskin Başlangıç, Hızlı Eller, Altın Hasat I, Düzenli Üretim, Hasat Deneyimi I) medyan R4–6'da başlıyor, R13–20'de bitiyor.
- Demir fiyatlı aileler (Taş Hasat, Verimli Üretim, Nadir Filizler, Kesim Ustalığı) R20–38'de, taş fiyatlı düğümler (Grid Genişleme II, Zengin Cevher, Kritik Güç) R30–41'de açılıyor ve 40 run'ın 34–40'ında R50'den önce alınıyor. **Hiçbir içerik kaynak türü yüzünden R50'nin dışında kalmıyor.**
- En pahalı üç aile (Ağır Kesim, Kart Sezgisi, Hasat Deneyimi II) run'ların yaklaşık yarısında hiç alınmıyor, alınınca 2–4 kademe: fırsat maliyeti duruyor.

### 8.4 Eşit bütçeyle ilk yatırım: hasar, hız, üretim, XP, ekonomi

Dengeli politika, 3 seed, 30 round. Her varyant iki saksıdan sonra bütün altınını önce tek bir aileye yatırır (aile bitene kadar başka kademe almaz), sonra aynı politikayla devam eder. Aile fiyatları: Keskin Başlangıç 101 G (R4'te biter) · Hızlı Eller 129 G (R6) · Hasat Deneyimi I + ön koşul kademesi 131 G (R6) · Altın Hasat I 140 G (R5) · Düzenli Üretim 121 G (R6).

| İlk yatırım | Birikimli skor R5 / R10 / R15 / R20 / R25 / R30 | 5 round'luk hasat R6–10 / R11–15 / R16–20 / R21–25 / R26–30 | Level R10 / R20 / R30 |
|---|---|---|---|
| Hasar | 131 / 496 / 1.075 / 2.412 / 4.282 / 9.420 | 137 / 230 / 304 / 326 / 433 | 11 / 26 / 39 |
| Hız | 102 / 376 / 848 / 2.125 / 4.746 / 12.257 | 109 / 193 / 248 / 306 / 452 | 10 / 24 / 41 |
| Ekonomi | 92 / 382 / 993 / 2.624 / 8.994 / 19.343 | 112 / 191 / 269 / 314 / 419 | 9 / 24 / 39 |
| Üretim | 101 / 320 / 666 / 1.299 / 3.192 / 7.433 | 103 / 158 / 214 / 302 / 297 | 9 / 23 / 35 |
| XP | 102 / 322 / 656 / 1.128 / 2.527 / 8.468 | 82 / 127 / 164 / 265 / 400 | 10 / 22 / 35 |

- Hasar erken en hızlı (R10 skoru en yüksek), hız R20–30'da yetişiyor, ekonomi R15'ten sonra öne geçiyor. Üçü de R30'a canlı ve rekabetçi geliyor: ilk yatırım olarak üçü de geçerli.
- **XP-önce geri ödemiyor.** Aynı bütçeyle R6–20 hasadı hız-önce'nin %66–75'i, R30'da level 35'e karşı 41. +%60 XP, kaybedilen hasadın getireceği XP'yi karşılamıyor (hasat hem XP hem altın hem skor verir; XP düğümü yalnız XP). R26–30'da fark kapanıyor (400'e karşı 452) ama birikimli skor %31 geride.
- Üretim-önce de zayıf: ilk round'larda tarla zaten hasat-sınırlı.
- 3 seed'lik medyanlar gürültülü: aynı karşılaştırmanın bir önceki turunda (Don Cephesi ve Hasat Deneyimi I fiyatı farklıydı) R30 skoru ekonomi 18.160, hız 8.803, hasar 6.652, üretim 11.168, XP 3.576 çıkmıştı. Sıralamada sabit kalan: ekonomi en üstte, XP en altta.

### 8.5 Yatırımın geri ödemesi (alırsa / hiç almazsa)

Dengeli politika, aynı iki seed; tek fark bir kategorinin hiç alınmaması.

| Varyant | Birikimli skor R10 / R15 / R20 / R30 / R40 / R50 | 5 round'luk hasat R16–20 / R26–30 / R36–40 / R46–50 | R50 level | R50 ağaç kademesi | Sonuç |
|---|---|---|---|---|---|
| Hepsini alır (taban) | 444 / 1.004 / 2.542 / 11.428 / 39.892 / 95.280 | 298 / 388 / 1.128 / 1.440 | 66 | 180 | 2 / 2 kazandı |
| Ekonomi düğümü almaz | 614 / 1.106 / 2.378 / 6.389 / 25.922 / 63.044 (1 run) | 209 / 310 / 510 / 886 (1 run) | 58 | 99 | 1 kazandı, 1 kota @R45 |
| XP ve kart düğümü almaz | 460 / 1.076 / 2.218 / 8.840 / 37.280 / 106.252 | 274 / 375 / 1.022 / 1.442 | 61 | 171 | 2 / 2 kazandı |
| Üretim düğümü almaz | 533 / 1.324 / 2.558 / 9.753 / 35.602 / 88.670 | 286 / 400 / 964 / 1.380 | 56 | 144 | 2 / 2 kazandı |

- **Ekonomi yatırımı geri ödüyor:** almayan run R15'e kadar önde (R10'da +%38 skor), R20'de eşit, sonra geride (R30'da −%44); R36–40 hasadı yarısı, bir run R45 kotasında eleniyor. Geri ödeme noktası ≈ R15–20. Erken ekonomi zorunlu açılış değil (ilk 15 round almayan önde), ama R20'den sonra hiç almamak run'ı riske sokuyor.
- **Üretim yatırımı:** almayan run R15'te önde (+%32), R20'de eşit, sonra %7–15 geride ve 10 level eksik. Küçük ama ölçülebilir geri ödeme (3.4'te sıfırdı); bir önceki turda aynı karşılaştırma R50'de −%26 vermişti, yani büyüklük iki seed'le kesin değil.
- **XP ve kart düğümleri:** almayan run R20–30'da %13–23 geride, R40'ta %7 geride, R50'de önde (+%12; iki seed'de gürültü içinde). 5 level fark. Ölçülebilir geri ödeme orta oyunda var, oyun sonunda yok.

## 9. XP ve kartlar

**XP tablosu.** Gereksinim `380 + 10L + 1,2L² + 0,045L³` (140 level tanımlı). Eski tablo level 60'a kadar neredeyse düzdü (605 → 3.622) ve sonra duvar yapıyordu (level 80: 62.666); yeni eğri baştan sona yumuşak büyür (level 1: 391, level 30: 2.975, level 60: 15.020, level 80: 31.900). Küp terim, geç oyunda hasat patladığında kart ekranı selini keser: ikinci ve üçüncü ayar turlarında R50 level'ı 91–98'di.

**XP geliri.** Bitki XP'si can çarpanıyla aynı oranda (20 × 1 / 1,5 / 2 / 2,5 / 3). Ağaçta Hasat Deneyimi I (+%60 · 125 G · R3–16) ve II (+%60 · 9.000 G · R25–42); ödülde Bilgi Filizi (×1,25, en çok 2, R30 boss'undan sonra çıkmaz).

**Level (medyan)**

| Politika · başlangıç | R5 | R10 | R15 | R20 | R25 | R30 | R35 | R40 | R45 | R50 |
|---|---|---|---|---|---|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 5 | 10 | 16 | 22 | 28 | 34 | 41 | 48 | 54 | 64 |
| Hasar-değerli hedef · Bahçıvan + Standart | 5 | 10 | 16 | 21 | 27 | 32 | 36 | 42 | 46 | 50 |
| Davranış-rezonans · Bahçıvan + Standart | 5 | 11 | 18 | 24 | 32 | 40 | 46 | 52 | 58 | 66 |
| Erken XP-ekonomi · Bahçıvan + Standart | 5 | 11 | 18 | 26 | 32 | 36 | 43 | 48 | 54 | 59 |
| Yeni · Bahçıvan + Standart | 3 | 8 | 12 | 17 | 22 | – | – | – | – | – |
| Erken XP-ekonomi · Tüccar + Standart | 5 | 12 | 21 | 30 | 38 | 44 | 50 | 58 | 65 | 74 |
| Hız-alan · Tüccar + Standart | 5 | 11 | 18 | 25 | 33 | 40 | 50 | 60 | 69 | 78 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | 5 | 10 | 16 | 22 | 27 | 33 | 39 | 44 | 48 | 52 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | 5 | 10 | 16 | 22 | 28 | 34 | 40 | 45 | 50 | 56 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 6 | 12 | 18 | 22 | 28 | 33 | 38 | 44 | 48 | 51 |
| Hız-alan · Bahçıvan + Dar Kesim | 6 | 12 | 19 | 26 | 31 | 38 | 43 | 48 | 54 | 60 |

- Level ilerlemesi doğrusala yakın: 5 round'da 5–10 level. R50'de nötr başlangıç 50–66, Tüccar 74–78.
- Round başına kart ekranı 1–1,5 (R50'de en hızlı build'lerde 2). Toplam 50–77 kart ekranı; §12.3'teki varsayımla oturumun %11–15'i.
- Level başına üç kart fikri bu pakette uygulanmadı.

**XP yatırımı geri ödüyor mu?** İki ölçüm var, ikisi de §8'de:

- *İlk yatırım olarak (eşit bütçe, §8.4):* hayır. XP-önce, hız-önce'ye göre R6–20'de %25–34 daha az hasat yapıyor, level'da yalnız R5'te önde (5'e karşı 4), sonra eşit ya da geride (R10: 10 / 10, R20: 22 / 24, R30: 35 / 41) ve R30 skoru %31 geride.
- *Normal sırada alınınca (§8.5):* XP ve kart düğümlerini hiç almayan run R20–30'da %13–23, R40'ta %7 geride; R50'de fark yok. 5 level (≈ 5 kart) kazandırıyor.

Sonuç: XP ailesi bu sayılarla "ikinci yatırım" olarak makul, "ilk yatırım" olarak zayıf. Nedeni fiyat değil, level'ın getirisi: bir kartın hasada katkısı bir hız ya da ekonomi kademesinin katkısından küçük. Bunu ağaç tarafından çözmek için XP düğümünü çok ucuz ya da çok güçlü yapmak gerekirdi (kart ekranı selini geri getirir); kalıcı çözüm kart değerinde (kapsam dışı; §14).

## 10. Alan yükseltmeleri ve gösterim

Hasat erişimi `yarıçap + yarım hücre`dir (hücre 2 birim): merkezi bu mesafenin içindeki hücre vurulur. Yarıçap sürekli artar, vurulan hücre sayısı basamak basamak değişir. Dolu tarlada en iyi nişanın (hücre merkezi / iki hücrenin arası / dört hücrenin köşesi) vurduğu hücre sayısı:

| Yarıçap | < 1,00 | 1,00 | 1,24 | 1,83 | 2,17 | 3,00 | 3,12 |
|---|---|---|---|---|---|---|---|
| En iyi nişanda hücre | 4 | 5 | 6 | 9 | 12 | 13 | 16 |

Alan yatırımları bu basamaklara göre seçildi. İşlev testi her aşamada formülü gerçek vuruşla karşılaştırdı (dolu 7×7 tarla, üç nişan):

| Aşama | Standart yarıçap → bitki | Dar Kesim (×0,75) yarıçap → bitki |
|---|---|---|
| Başlangıç | 1,00 → 5 | 0,75 → 4 |
| + Geniş Süpürüş (ağaç, +%35 · R8–22) | 1,35 → 6 | 1,01 → 5 |
| + Geniş Tarama (ağaç, +%50 · R20–36) | 1,85 → 9 | 1,39 → 6 |
| + Geniş Savuruş ×1 (ödül, ×1,32) | 2,44 → 12 | 1,83 → 9 |
| + Geniş Savuruş ×2 (ödül) | 3,22 → 16 | 2,42 → 12 |

Eski ağaçta iki alan ailesi (+%30 +%30 = 1,6) Standart'ı 6 hücrede bırakıyordu, Dar Kesim hiçbir ağaç seviyesinde 5'i geçmiyordu ve ikinci aile R70'ten önce alınamıyordu. Şimdi her aile ve her ödül yığını Standart'ta bir basamak geçirir; Dar Kesim de her adımda (bir adım geriden) hedef kazanır. Aile içindeki tek tek kademelerin hepsi basamak geçirmez (istenen de bu değildi): Geniş Süpürüş'te 6. hücre ailenin yaklaşık üçte ikisinde (yarıçap 1,24) gelir; Geniş Tarama'da 9. hücre ödülsüz oynanırsa ailenin son kademesinde (1,83 eşiği, aile 1,85'e çıkarır), bir Geniş Savuruş alınmışsa ilk kademesinde gelir. 1,24 ile 1,83 arasındaki boşluk grid geometrisinden gelir; ödülle birleşince kapanır.

**Halka ile gerçek erişim arasındaki yarım hücre.** Önceden imleç halkası ve vuruş halkası çıplak yarıçapta (1,0) çiziliyordu, gerçek erişim ise 2,0'dı: halkanın dışında görünen komşu hücre vuruluyordu. Şimdi ikisi de `GridSystem.HarvestReach(radius)` değerinde çizilir; `GetGridObjectsInRadius` de aynı fonksiyonu kullanır. **Hasat erişimi değişmedi**: test, tam erişim çemberindeki komşunun vurulduğunu, yarıçap %1 küçülünce vurulmadığını ve halka noktalarının erişim mesafesinde olduğunu doğruluyor. Bu gösterim düzeltmesi bütün profillerde geçerlidir (eski profillerde de halka artık büyük çizilir; vurulan bitkiler aynı).

**Run'larda ölçülen alan** (teslim ölçümü; yarıçapın ağaç + kart payı × ödül payı, §6.3 tablosundan):

| Politika · başlangıç | R25: yarıçap → hücre | R50: yarıçap → hücre | R50'de saldırı başına vurulan bitki |
|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 1,50 → 6 | 2,44 → 12 | 11,7 |
| Hız-alan · Tüccar + Standart | 2,20 → 10 | 2,83 → 14 | 13,8 |
| Davranış-rezonans · Bahçıvan + Standart | 1,19 → 5 | 2,41 → 12 | 11,3 |
| Hasar-değerli hedef · Bahçıvan + Standart | 1,16 → 5 | 1,46 → 6 | 5,6 |
| Hız-alan · Bahçıvan + Dar Kesim | 1,17 → 5 | 1,78 → 8 (4 run: 6 / 6 / 9 / 12) | 7,5 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 0,90 → 4 | 1,10 → 5 | 4,6 |

- Alan yatırımı yapan politikalar R50'de 12–14 hücreye çıkıyor (referans profilde tavan 6'ydı); yapmayan Hasar politikası 5–6'da kalıyor. Alan artık build farkı yaratıyor.
- **Dar Kesim:** Hız-alan ile R50'de 6–12 hücre (aldığı Geniş Savuruş sayısına göre) ve ×2,77 doğrudan hasar çarpanı: 4 / 4 kazandı, R50 hasadı 278 (Standart 424). Hasar politikasıyla 5 hücrede kalıyor: 3 / 4 kazandı; elenen run son kotayı %5 farkla kaçırdı. Dar Kesim artık alan yatırımına cevap veriyor, ama "az alan + çok hasar" tek vuruş doygunluğu yüzünden geç oyunda hâlâ en kırılgan kombinasyon.
- Bot her vuruşta en çok bitkili noktayı bulur; insan oyuncunun gerçek hedef sayısı daha düşük olur (§13).

## 11. Boss kuralları ve hedefler

Yapı 3.4'teki gibi: segment kotası + yalnız boss round'unda sayılan ayrı boss hasadı hedefi. İkisi de sabit tablo; oyuncu gücüyle ölçeklenmez.

| Boss | Prototip (3.4, değişmedi) | Run50_DengeV1 | Neden |
|---|---|---|---|
| Don Cephesi | şerit %25 · üretim ×1,5 yavaş | şerit %50 · üretim ×2,5 yavaş | İlk değerlerle ölçümde etkisi yoktu (aşağıda) |
| Sert Kabuk | şerit %40 · can ×1,5 | aynı | Tek vuruş doygunluğu kalkınca etkili oldu |
| Sis | yarıçap ×0,85 · yarıçap ≥ 1,2'de çıkar | **bir basamak**: yarıçap, en iyi nişanın hedef sayısı tam bir basamak düşecek kadar küçülür · yarıçap ≥ 1,24'te çıkar | ×0,85 çoğu yarıçapta hedef sayısını değiştirmiyordu |

**Sis kuralı (küçük kural düzeltmesi).** `FogSO.stepDown` açıkken Sis, boss round'u başında `HarvestArea.StepDownRadius` ile hedef yarıçapı hesaplar: 6 → 5, 9 → 6, 12 → 9, 16 → 13 bitki. Kural metni gerçek sayıyı yazar ("vuruş alanı bir basamak dar: en iyi nişanda 9 → 6 bitki"). Davranışların alanı ve hasarı değişmez; build kapatılmaz. Yarıçap 1,24'ün altındayken (5 hücre) Sis havuza girmez, çünkü bir basamak aşağısı merkez nişanı 1 hücreye düşürürdü. Prototipteki Sis eski sabit çarpanla çalışmaya devam eder.

### 11.1 Boss'ların ölçülen etkisi

Aynı politika ve seed, 50 round; boss havuzu tek bir boss'a indirilir. Ölçü: boss round'unun hasadı ÷ aynı run'ın komşu round'larının (R−1, R+1) ortalaması. "Kuralsız boss" satırı tabandır (boss round'u ödülden önce, komşusu ödülden sonra olduğu için taban 1'in biraz altındadır). Dengeli ve Hasar-değerli hedef politikaları, 2 seed, bant başına 6 boss round'u.

| Boss | Politika | R5–15 | R20–30 | R35–45 |
|---|---|---|---|---|
| Kuralsız (taban) | Dengeli | 0,96 | 0,91 | 0,91 |
| | Hasar | 1,01 | 0,93 | 0,95 |
| Don Cephesi | Dengeli | 0,81 (**−%16**) | 0,91 (0) | 0,92 (+%1) |
| | Hasar | 0,88 (**−%13**) | 0,84 (**−%10**) | 0,91 (−%4) |
| Sert Kabuk | Dengeli | 0,94 (−%2) | 0,86 (−%5) | 0,80 (**−%12**) |
| | Hasar | 0,75 (**−%26**) | 0,77 (**−%17**) | 0,82 (**−%14**) |
| Sis | Dengeli | çıkmaz (yarıçap < 1,24) | 0,76 (**−%16**) | 0,75 (**−%18**) |
| | Hasar | çıkmaz | çıkmaz | 0,79 (**−%17**) |

- **Sert Kabuk** 3.4'te sıfıra yakındı (tek vuruş doygunluğu). Şimdi bütün bantlarda ölçülebilir. Hasar politikasını daha çok etkiliyor, çünkü o politika az saldırıyla tek vuruş sınırında oynuyor; ×1,5 can onu iki vuruşa düşürüyor.
- **Sis** yeni "bir basamak" kuralıyla çıktığı her bantta −%16…−%18. Davranışlara dokunmuyor; alan yatırımı olmayan build'e (yarıçap < 1,24) hiç gelmiyor.
- **Don Cephesi** ilk değerlerle (şerit %25 · ×1,5) etkisizdi: Dengeli +%1 / +%1 / +%3, Hasar 0 / +%10 / +%7 (gürültü). **Neden:** tarla orta oyunda hasat-sınırlı (§12.4: hasat, üretim kapasitesinin %13–25'i); tarlanın dörtte birinde üretimi yavaşlatmak oyuncunun bulduğu bitki sayısını değiştirmiyor. **Düzeltme (yalnız parametre, kural aynı):** şerit %50, üretim ×2,5 yavaş. Sonuç: erken oyunda −%13…−%16, orta oyunda 0…−%10, geç oyunda ≈ 0. Don artık bir erken / orta oyun boss'u; geç oyunda hâlâ etkisiz, çünkü üretim fazlası orada en büyük. Geç oyunda da ısırması için kuralın üretim dışında bir şeye dokunması gerekir; tasarım kararı olduğu için bu pakette yapılmadı (§14).
- Hiçbir boss tek başına bu iki politikayı hedefin altına düşürmedi (en düşük boss hasadı ÷ hedef: ×1,65, Don, R5–15 bandı, R10 hedefi 20 iken).

### 11.2 Hedeflere göre pay

Hedefler sabit tablo (§4); oyuncu gücüyle ölçeklenmez. Teslim ölçümündeki 40 yapılandırılmış run:

| Round | Oynayan run | Segment skoru ÷ kota (medyan [en düşük]) | Boss hasadı ÷ hedef (medyan [en düşük]) | Kotada kalan | Boss hedefinde kalan |
|---|---|---|---|---|---|
| R5 | 40 | ×3,7 [×2,64] | ×5,4 [×2,75] | 0 | 0 |
| R10 | 40 | ×2,8 [×1,89] | ×3,6 [×1,31] | 0 | 0 |
| R15 | 40 | ×3,5 [×2,08] | ×3,3 [×1,57] | 0 | 0 |
| R20 | 40 | ×3,8 [×1,99] | ×4,1 [×1,29] | 0 | 0 |
| R25 | 40 | ×5,5 [×2,56] | ×6,6 [×2,26] | 0 | 0 |
| R30 | 40 | ×9,4 [×2,88] | ×9,9 [×3,18] | 0 | 0 |
| R35 | 40 | ×6,1 [×2,48] | ×5,5 [×2,29] | 0 | 0 |
| R40 | 40 | ×3,8 [×1,47] | ×4,2 [×1,07] | 0 | 0 |
| R45 | 40 | ×3,7 [×1,23] | ×3,4 [×1,05] | 0 | 0 |
| R50 | 40 | ×4,0 [×0,95] | — | 1 | 0 |

"Yeni" bot (rastgele nişan, en ucuzu alır):

| Round | Oynayan run | Segment skoru ÷ kota | Boss hasadı ÷ hedef | Kotada kalan | Boss hedefinde kalan |
|---|---|---|---|---|---|
| R5 | 4 | ×2,4 [×1,49] | ×2,8 [×1,12] | 0 | 0 |
| R10 | 4 | ×1,7 [×0,69] | ×2,3 [×1,12] | 1 | 0 |
| R15 | 3 | ×1,5 [×1,45] | ×1,2 [×0,77] | 0 | 1 |
| R20 | 2 | ×1,3 [×1,20] | ×1,1 [×0,44] | 0 | 1 |
| R25 | 1 | ×1,2 [×1,20] | ×0,2 [×0,23] | 0 | 1 |

- Güçlü build'ler rahat geçiyor (medyan ×3–×10); en zayıf run'lar R40–R50'de sınırda (×0,95–×1,5). Bir run son kotada elendi.
- Pay R25–R35'te büyüyor: skor ailesi (Hasat Rekoru, skor +%100; ilk kademe medyan R24) ve nadirlik yatırımları o pencerede alınıyor. Hasat Rekoru'nu hiç almayan bir build ayrıca ölçülmedi; zayıf politikalarda geç kotaları kaçırması beklenir (§14).
- **İlk boss (R5):** en düşük pay ×2,75; açılış denemelerinde saksı alan her açılış geçti (§8.1). Yeni başlayan oyuncu tek bir açılışa mahkûm değil.
- **İkinci boss (R10):** Don Cephesi güçlendikten sonra XP / ekonomiyle açılan bir run R10 boss'unu sınırda geçti (boss hasadı 21, hedef 20); hedef bu yüzden **20 → 16** indirildi. Final ve açılış setleri yeni hedefle yeniden koşuldu ve hedef sütunu dışında birebir aynı çıktı (§15.3); yukarıdaki tablo o koşudan.
- "Yeni" bot ilk boss'u 4 / 4 geçiyor, sonra R10–R25 arasında eleniyor. Rastgele nişanla saldırı başına 1,5–2,7 bitki vuruyor (aynı yarıçapla diğer politikalar 3–5) ve hasadı R15'ten sonra artmıyor (R15: 16, R25: 11). Bu bot bir alt sınır; gerçek yeni oyuncunun nerede kalacağı insan testiyle görülür.

## 12. Uçtan uca run sonuçları

### 12.1 Nasıl ölçüldü

`BalanceRunMeasurement` izole proje kopyasında GameScene'i açar ve run'ı **Round 1'den, normal başlangıç bütçesiyle (80 altın)** oynar. Round atlanmaz; kart, para ya da tile bedava verilmez. Satın almalar oyunun kendi yollarından geçer: saksı = kaynak harcama + `PlacementManager` yerleştirmesi, ağaç = `SkillTreeManager.TryUpgrade`, kart = `CardSelectionUI.OnCardSelected`, boss ödülü = `BossRewardManager.Choose`, vuruş = `PlayerController.AttackInRadius` (saldırı aralığına uyarak). Elenen run'lar sonuçlardan çıkarılmadı.

| Politika | Ne yapar |
|---|---|
| Hız-alan | Saldırı hızı ve alan ailelerini öne alır; ödülde Hızlı Bilek / Fırtına Bileği / Geniş Savuruş |
| Hasar-değerli hedef | Hasar ve kritik ailelerini öne alır; nişanı en değerli (nadir) bitkiye göre alır; ödülde Keskin Bıçak / kritik / Altın Hedef. Hız ödülü almaz |
| Davranış-rezonans | Davranış kartı kilitlerini ve davranış kartlarını öne alır; ödülde Kıvılcım / Yıkım Gücü |
| Erken XP-ekonomi | İlk 10 round XP ve ekonomi ailelerini öne alır, sonra dengeli |
| Dengeli | Aileleri hedef round'larına göre sırayla alır (geri ödeme ve ödül karşılaştırmalarının tabanı) |
| Yeni | En ucuz kademeyi alır, hücre merkezlerine **rastgele** nişan alır, kartı ve ödülü rastgele seçer. Bir alt sınırdır: gerçek bir yeni oyuncu en azından bitkiye nişan alır |

Başlangıçlar: Bahçıvan (nötr) · Tüccar (kaynak ×1,25, skor ×0,9) · Seçici Yetiştirici (nadirlik +15, üretim aralığı ×1,15) · Standart tırpan · Dar Kesim (yarıçap ×0,75, doğrudan hasar ×1,4). Her kombinasyon 4 seed (100–103): 11 kombinasyon × 4 = 44 run.

### 12.2 Sonuçlar

| Politika · başlangıç | Kazanan | Elenenler (neden @ round) | R50'de ağaç kademesi (221 üzerinden) | R50 level | Toplam skor |
|---|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 4 / 4 | — | 177 [144–193] | 64 [54–68] | 95050 [44000–190214] |
| Hasar-değerli hedef · Bahçıvan + Standart | 4 / 4 | — | 137 [130–152] | 50 [47–55] | 53096 [41431–61009] |
| Davranış-rezonans · Bahçıvan + Standart | 4 / 4 | — | 180 [143–210] | 66 [56–80] | 102156 [34652–352612] |
| Erken XP-ekonomi · Bahçıvan + Standart | 4 / 4 | — | 138 [128–145] | 59 [49–63] | 52702 [28460–71467] |
| Yeni · Bahçıvan + Standart | 0 / 4 | boss hedefi @R15, boss hedefi @R25, kota @R10, boss hedefi @R20 | 36 [17–57] | 13 [7–22] | 667 [167–1281] |
| Erken XP-ekonomi · Tüccar + Standart | 4 / 4 | — | 192 [150–211] | 74 [54–81] | 97800 [52113–338551] |
| Hız-alan · Tüccar + Standart | 4 / 4 | — | 216 [195–219] | 78 [63–83] | 215146 [86592–338667] |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | 4 / 4 | — | 131 [118–167] | 52 [45–58] | 41045 [34848–79115] |
| Davranış-rezonans · Seçici Yetiştirici + Standart | 4 / 4 | — | 150 [124–174] | 56 [48–64] | 69110 [46496–103059] |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 3 / 4 | kota @R50 | 138 [122–144] | 51 [46–55] | 44330 [27557–80817] |
| Hız-alan · Bahçıvan + Dar Kesim | 4 / 4 | — | 167 [137–187] | 60 [52–65] | 63497 [33153–151564] |

Toplam: 44 run, 39 kazanan.

- Yapılandırılmış politikalar: 40 run'ın 39'u R50'yi bitirdi. Elenen tek run en zayıf kombinasyon (saf hasar + Dar Kesim): son segment kotasını 5.728 / 6.000 ile kaçırdı.
- "Yeni" bot 4 run'ın 4'ünde elendi (R10–R25). İlk boss'u dördü de geçti (§11).
- Aynı politika seed'e göre 1,5–10 kat farklı toplam skor yapıyor (ör. Davranış-rezonans 34.652 – 352.612). Fark boss ödülü tekliflerinden ve kartlardan geliyor; erken gelen hız / alan ödülü geliri, gelir ağacı büyütüyor (kartopu).

**Round başına hasat (medyan; o round'u oynayan run'lar)**

| Politika · başlangıç | R5 | R10 | R15 | R20 | R25 | R30 | R35 | R40 | R45 | R50 |
|---|---|---|---|---|---|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 20 | 20 | 32 | 42 | 58 | 98 | 106 | 182 | 252 | 424 |
| Hasar-değerli hedef · Bahçıvan + Standart | 19 | 22 | 34 | 34 | 50 | 56 | 66 | 82 | 101 | 118 |
| Davranış-rezonans · Bahçıvan + Standart | 20 | 26 | 44 | 56 | 76 | 112 | 133 | 180 | 256 | 404 |
| Erken XP-ekonomi · Bahçıvan + Standart | 17 | 16 | 36 | 38 | 47 | 59 | 58 | 90 | 112 | 164 |
| Yeni · Bahçıvan + Standart | 9 | 12 | 16 | 16 | 11 | – | – | – | – | – |
| Erken XP-ekonomi · Tüccar + Standart | 16 | 23 | 41 | 52 | 71 | 82 | 92 | 159 | 242 | 388 |
| Hız-alan · Tüccar + Standart | 21 | 28 | 44 | 60 | 110 | 154 | 224 | 322 | 488 | 592 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | 19 | 24 | 31 | 39 | 46 | 59 | 54 | 82 | 92 | 104 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | 18 | 22 | 40 | 46 | 54 | 76 | 88 | 114 | 144 | 250 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 23 | 28 | 34 | 42 | 44 | 55 | 58 | 86 | 90 | 112 |
| Hız-alan · Bahçıvan + Dar Kesim | 24 | 32 | 46 | 62 | 64 | 101 | 94 | 150 | 163 | 278 |

### 12.3 Süre

Hasat süresi ölçüldü (round başına 45 sn sabit). Menüde geçen süre **ölçülemez** (bot beklemez); aşağıdaki oturum tahmini şu varsayımlarla hesaplandı: round arası hazırlık 25 sn, kart ekranı 10 sn, boss ödülü ekranı 15 sn, alınan her ağaç kademesi 4 sn.

| Politika · başlangıç | Oynanan round | Hasat süresi (ölçüldü, dk) | Kart ekranı sayısı | Boss ödülü ekranı | Alınan ağaç kademesi | Tahmini oturum (dk; menü süreleri varsayım) | Kart ekranı payı (varsayımla) |
|---|---|---|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | 50 [50–50] | 37,5 | 62 [53–67] | 9 | 177 | 83 [79–85] | %13 |
| Hasar-değerli hedef · Bahçıvan + Standart | 50 [50–50] | 37,5 | 50 [46–54] | 9 | 137 | 78 [77–80] | %11 |
| Davranış-rezonans · Bahçıvan + Standart | 50 [50–50] | 37,5 | 64 [55–79] | 9 | 180 | 83 [79–88] | %13 |
| Erken XP-ekonomi · Bahçıvan + Standart | 50 [50–50] | 37,5 | 58 [48–62] | 9 | 138 | 79 [77–81] | %12 |
| Yeni · Bahçıvan + Standart | 18 [10–25] | 13,1 | 12 [6–21] | 4 | 36 | 26 [14–38] | %8 |
| Erken XP-ekonomi · Tüccar + Standart | 50 [50–50] | 37,5 | 74 [53–80] | 9 | 192 | 86 [79–88] | %14 |
| Hız-alan · Tüccar + Standart | 50 [50–50] | 37,5 | 77 [62–82] | 9 | 216 | 88 [84–89] | %15 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | 50 [50–50] | 37,5 | 50 [44–57] | 9 | 131 | 78 [76–81] | %11 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | 50 [50–50] | 37,5 | 55 [47–63] | 9 | 150 | 80 [77–83] | %11 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | 50 [50–50] | 37,5 | 50 [45–54] | 9 | 138 | 78 [76–79] | %11 |
| Hız-alan · Bahçıvan + Dar Kesim | 50 [50–50] | 37,5 | 60 [51–64] | 9 | 167 | 82 [78–84] | %12 |

50 round'luk run ≈ 37,5 dk hasat + tahmini 40–50 dk menü = 78–88 dk. Kart ekranı sayısı 50–77 (round başına 1–1,5); 10 sn varsayımıyla oturumun %11–15'i. Gerçek kart ekranı süresi insan testinde ölçülmeli.

### 12.4 Hasat payı ve tarla

| Politika · başlangıç | Round | Hasat | Doğrudan % | Patlama / kasırga / bumerang / elektrik % | Ortalama doluluk | En düşük doluluk | Hasat ÷ üretim kapasitesi | Saniyede hasat | Vurulan hücre-saniye başına hasat |
|---|---|---|---|---|---|---|---|---|---|
| Hız-alan · Bahçıvan + Standart | R10 | 20 | 100 | 0 / 0 / 0 / 0 | 0,61 | 0,38 | 0,24 | 0,46 | 0,091 |
| Hız-alan · Bahçıvan + Standart | R25 | 58 | 89 | 3 / 0 / 0 / 0 | 0,79 | 0,64 | 0,16 | 1,29 | 0,196 |
| Hız-alan · Bahçıvan + Standart | R40 | 182 | 86 | 11 / 2 / 0 / 0 | 0,71 | 0,53 | 0,21 | 4,04 | 0,458 |
| Hız-alan · Bahçıvan + Standart | R50 | 424 | 80 | 10 / 4 / 0 / 2 | 0,68 | 0,50 | 0,39 | 9,42 | 0,694 |
| Hasar-değerli hedef · Bahçıvan + Standart | R10 | 22 | 100 | 0 / 0 / 0 / 0 | 0,54 | 0,28 | 0,29 | 0,50 | 0,100 |
| Hasar-değerli hedef · Bahçıvan + Standart | R25 | 50 | 95 | 0 / 0 / 0 / 0 | 0,84 | 0,66 | 0,15 | 1,11 | 0,222 |
| Hasar-değerli hedef · Bahçıvan + Standart | R40 | 82 | 88 | 11 / 1 / 0 / 7 | 0,85 | 0,72 | 0,14 | 1,83 | 0,306 |
| Hasar-değerli hedef · Bahçıvan + Standart | R50 | 118 | 94 | 11 / 0 / 0 / 2 | 0,84 | 0,70 | 0,18 | 2,62 | 0,437 |
| Davranış-rezonans · Bahçıvan + Standart | R10 | 26 | 100 | 0 / 0 / 0 / 0 | 0,58 | 0,32 | 0,27 | 0,57 | 0,113 |
| Davranış-rezonans · Bahçıvan + Standart | R25 | 76 | 70 | 15 / 10 / 1 / 1 | 0,83 | 0,62 | 0,18 | 1,68 | 0,311 |
| Davranış-rezonans · Bahçıvan + Standart | R40 | 180 | 47 | 41 / 8 / 3 / 2 | 0,72 | 0,53 | 0,25 | 4,00 | 0,485 |
| Davranış-rezonans · Bahçıvan + Standart | R50 | 404 | 43 | 46 / 5 / 1 / 4 | 0,67 | 0,46 | 0,36 | 8,97 | 0,747 |
| Erken XP-ekonomi · Bahçıvan + Standart | R10 | 16 | 100 | 0 / 0 / 0 / 0 | 0,59 | 0,32 | 0,21 | 0,36 | 0,071 |
| Erken XP-ekonomi · Bahçıvan + Standart | R25 | 47 | 97 | 0 / 0 / 0 / 0 | 0,78 | 0,64 | 0,20 | 1,04 | 0,198 |
| Erken XP-ekonomi · Bahçıvan + Standart | R40 | 90 | 95 | 2 / 0 / 0 / 1 | 0,80 | 0,69 | 0,14 | 1,99 | 0,228 |
| Erken XP-ekonomi · Bahçıvan + Standart | R50 | 164 | 89 | 3 / 1 / 1 / 1 | 0,78 | 0,67 | 0,25 | 3,66 | 0,349 |
| Yeni · Bahçıvan + Standart | R10 | 12 | 100 | 0 / 0 / 0 / 0 | 0,56 | 0,33 | 0,22 | 0,26 | 0,051 |
| Yeni · Bahçıvan + Standart | R25 | 11 | 100 | 0 / 0 / 0 / 0 | 0,75 | 0,53 | 0,09 | 0,24 | 0,049 |
| Erken XP-ekonomi · Tüccar + Standart | R10 | 23 | 100 | 0 / 0 / 0 / 0 | 0,58 | 0,36 | 0,22 | 0,51 | 0,102 |
| Erken XP-ekonomi · Tüccar + Standart | R25 | 71 | 86 | 1 / 10 / 0 / 0 | 0,81 | 0,57 | 0,17 | 1,58 | 0,263 |
| Erken XP-ekonomi · Tüccar + Standart | R40 | 159 | 77 | 5 / 10 / 1 / 2 | 0,68 | 0,49 | 0,21 | 3,53 | 0,470 |
| Erken XP-ekonomi · Tüccar + Standart | R50 | 388 | 78 | 12 / 6 / 1 / 0 | 0,66 | 0,50 | 0,37 | 8,62 | 0,719 |
| Hız-alan · Tüccar + Standart | R10 | 28 | 100 | 0 / 0 / 0 / 0 | 0,59 | 0,26 | 0,24 | 0,63 | 0,127 |
| Hız-alan · Tüccar + Standart | R25 | 110 | 95 | 4 / 0 / 0 / 0 | 0,72 | 0,51 | 0,23 | 2,44 | 0,262 |
| Hız-alan · Tüccar + Standart | R40 | 322 | 79 | 7 / 2 / 0 / 4 | 0,65 | 0,45 | 0,34 | 7,17 | 0,496 |
| Hız-alan · Tüccar + Standart | R50 | 592 | 81 | 10 / 4 / 2 / 3 | 0,66 | 0,50 | 0,40 | 13,16 | 0,937 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R10 | 24 | 100 | 0 / 0 / 0 / 0 | 0,50 | 0,20 | 0,34 | 0,54 | 0,136 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R25 | 46 | 89 | 0 / 2 / 0 / 0 | 0,83 | 0,66 | 0,15 | 1,02 | 0,256 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R40 | 82 | 85 | 4 / 2 / 0 / 0 | 0,78 | 0,67 | 0,15 | 1,82 | 0,402 |
| Hasar-değerli hedef · Seçici Yetiştirici + Dar Kesim | R50 | 104 | 83 | 6 / 4 / 0 / 0 | 0,82 | 0,72 | 0,19 | 2,30 | 0,460 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R10 | 22 | 100 | 0 / 0 / 0 / 0 | 0,57 | 0,32 | 0,28 | 0,50 | 0,100 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R25 | 54 | 77 | 20 / 3 / 0 / 3 | 0,74 | 0,52 | 0,20 | 1,21 | 0,242 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R40 | 114 | 54 | 22 / 6 / 4 / 10 | 0,73 | 0,56 | 0,20 | 2,53 | 0,310 |
| Davranış-rezonans · Seçici Yetiştirici + Standart | R50 | 250 | 56 | 26 / 6 / 3 / 12 | 0,72 | 0,55 | 0,31 | 5,57 | 0,479 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R10 | 28 | 100 | 0 / 0 / 0 / 0 | 0,55 | 0,35 | 0,28 | 0,62 | 0,156 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R25 | 44 | 94 | 2 / 0 / 0 / 0 | 0,86 | 0,64 | 0,13 | 0,98 | 0,244 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R40 | 86 | 94 | 3 / 1 / 0 / 2 | 0,84 | 0,72 | 0,14 | 1,92 | 0,428 |
| Hasar-değerli hedef · Bahçıvan + Dar Kesim | R50 | 112 | 87 | 8 / 8 / 0 / 0 | 0,85 | 0,76 | 0,18 | 2,50 | 0,500 |
| Hız-alan · Bahçıvan + Dar Kesim | R10 | 32 | 100 | 0 / 0 / 0 / 0 | 0,60 | 0,39 | 0,26 | 0,70 | 0,175 |
| Hız-alan · Bahçıvan + Dar Kesim | R25 | 64 | 95 | 0 / 0 / 0 / 0 | 0,86 | 0,73 | 0,14 | 1,42 | 0,256 |
| Hız-alan · Bahçıvan + Dar Kesim | R40 | 150 | 92 | 1 / 2 / 0 / 4 | 0,81 | 0,67 | 0,17 | 3,34 | 0,504 |
| Hız-alan · Bahçıvan + Dar Kesim | R50 | 278 | 91 | 2 / 1 / 0 / 3 | 0,78 | 0,62 | 0,27 | 6,17 | 0,696 |

- Davranış payı: Hız-alan ve Hasar politikalarında %5–20, Davranış-rezonans politikasında R25'te %23–30, R40–50'de %44–57. Davranış patlamaları güçlü kaldı; elektrik eski "ölünce tetiklenir" kuralıyla çalışıyor.
- Tarla R25'te ortalama %72–86 dolu ve hasat üretim kapasitesinin %13–23'ü; R40'ta %65–85 dolu, kapasitenin %14–34'ü. Yani orta oyun **hasat-sınırlı**: oyuncu yetişemiyor, bitki bol. R50'de hızlı build'lerde doluluk %66–68'e, hasat ÷ kapasite 0,36–0,40'a çıkıyor (üretim sınırına yaklaşıyor).
- Vurulan hücre-saniye başına hasat R10'da 0,07–0,18, R50'de 0,35–0,94: geç oyunda aynı alan daha verimli (az vuruşta ölüm + daha sık saldırı).

## 13. Ölçümün sınırları

- **Bot insan değil.** Her vuruşta en çok bitkili noktayı bulur, saldırı aralığını boşluksuz kullanır, hiç yorulmaz. İnsan oyuncunun saldırı başına hedef sayısı ve saldırı sıklığı daha düşük olur; mutlak hasat sayıları bir üst sınır gibi okunmalı. "Yeni" bot ise tersine bir alt sınır (rastgele nişan).
- **Az seed.** Teslim ölçümü kombinasyon başına 4 seed; A/B setleri 2–3 seed. Kartopu etkisi yüzünden aynı politika seed'e göre 1,5–10 kat farklı skor yapıyor. İki ölçüm turu arasında (yalnız Don Cephesi, ilk hedefler ve bir fiyat değişti) aynı politikanın medyan toplam skoru ×0,4 – ×1,2 oynadı (ör. Erken XP-ekonomi 126 bin → 53 bin, Davranış-rezonans 91 bin → 102 bin). Politika sıralamaları kaba düzeyde güvenilir, yüzdeler değil.
- **Bot deterministik:** aynı seed ve aynı veri aynı sonucu verir (geri ödeme tabanı ile "hız yolu" run'ları birebir aynı çıktı). Yani gürültü ölçüm tekrarından değil, seed ve veri farkından geliyor.
- **Menü süreleri varsayım.** Hasat süresi ölçüldü; hazırlık, kart ve ödül ekranı süreleri bot için sıfır, §12.3'teki sayılar tahmin. Kart ekranında geçen gerçek süre ancak insan testinde ölçülür.
- **Botun modellemediği şeyler:** saksı yerini boss şeridine göre seçmek (Don / Sert Kabuk önizlemesine tepki), rezonans için bilinçli tile dizilimi (kart tercihi ağırlıkla yapılır, dizilim planlanmaz), kart atlama (Vazgeçiş düğümleri alınır ama kullanılmaz), görevle kilit açma (çiftçi ve tırpanlar açık kabul edildi), "his".
- **Ölçülmeyenler:** Hasat Rekoru'nu hiç almayan build; kritik ödüllerinin ve Altın Hedef'in tek tek katkısı; Tüccar ve Seçici Yetiştirici'nin bütün politikalarla çaprazı; 11×11 grid'in etkisi (az sayıda run açabildi).
- **Simülatör kullanılmadı.** `RunSimulator` DengeV1 verisine uyarlanmadı; bu belgedeki bütün sonuçlar sahne botundan. Simülatör eski profil ölçümleri için olduğu gibi duruyor.
- **Ölçüm aracı eksiği:** round satırlarındaki boss adı sütunu boş kaldı; karışık havuzlu run'larda hangi boss'un geldiği loglardan okunamıyor. Boss A/B setinde havuz tek boss'a indirildiği için §11.1 bundan etkilenmez.

## 14. Hâlâ baskın / zayıf olanlar

**Hâlâ baskın**

1. **Hız + alan.** R50 hasadı saf hasar yolunun yaklaşık 4–5 katı (424–592'ye karşı 104–118). Öldürme modeli gereği hız ve alan sınırsız ölçekleniyor, hasar tek vuruşta doyuyor. Ödül yollarında da alan ve hız en üstte.
2. **Tüccar.** Kaynak ×1,25 kartopuyla büyüyor: Tüccar + Hız-alan R50'ye kadar 66 bin altın topluyor (nötr 25 bin), ağacın 216 / 221'ini alıyor ve kasada 6.584 altın / 13.260 demir / 6.924 taş artıyor. "Ağacın tamamı beklenmez" hedefi Tüccar'da tutmuyor; skor cezası (×0,9) bunu dengelemiyor. Çiftçi değerleri ortak veri olduğu için bu pakette değiştirilmedi.
3. **Ekonomi aileleri (R15'ten sonra).** Hiç almayan run'ın R36–40 hasadı yarı yarıya düşüyor, biri R45 kotasında eleniyor. İlk 15 round için zorunlu değil, sonrası için fiilen zorunlu.
4. **İlk yatırımda ekonomi.** Eşit bütçeli karşılaştırmanın iki turunda da R30 skorunda birinci.
5. **Boss ödülleri olmadan run bitmiyor** (R30'da eleniyor). Bu istenen aktarımın sonucu, ama ödül katmanı artık "bonus" değil "zorunlu güç"; ödül seçimini kötü yapan oyuncu için tampon az.

**Hâlâ zayıf**

1. **Saf hasar yolu, özellikle Dar Kesim'le.** R45–50 hedeflerinde ×0,95–×1,3 payla en kırılgan kombinasyon; elenen tek yapılandırılmış run bu. Hasar politikası hız ödülü almadığı için saldırı aralığı R50'de 1,8–1,9 sn'de kalıyor.
2. **XP yatırımı.** İlk yatırım olarak geri ödemiyor (R30 skoru −%31); normal sırada alınınca orta oyunda %7–23, oyun sonunda fark yok. Hasat Deneyimi II (9.000 G) ve Kart Sezgisi (14.000 G) run'ların yarısında hiç alınmıyor. Kök neden kart değeri; ağaç fiyatıyla çözülmüyor.
3. **Üretim-nadirlik ödül yolu** (Bereketli Toprak, Nadir Tohum): R50 skoru alan yolunun %27'si. **Bilgi Filizi yolu** %43.
4. **Don Cephesi geç oyunda** (R35–45 etkisi ≈ 0). Erken ve orta oyunda artık etkili.
5. **Üretim aileleri ilk yatırım olarak** zayıf (ilk round'larda tarla hasat-sınırlı); normal sırada küçük geri ödeme.
6. **Ağır Kesim** (24.000 G, +30 düz hasar): run'ların yarısında hiç alınmıyor, alınınca 1–3 kademe. Fiyatı etkisine göre yüksek.
7. **"Yeni" bot R10–R25'te eleniyor.** İlk boss'u geçiyor ama sonrası için tampon yok. Bunun gerçek yeni oyuncuya ne kadar benzediği bilinmiyor.

**Belirsiz kalanlar (insan testi ister)**

- Geç hedefler (R40–50) bot için "zayıf build'i eleyen, güçlü build'e rahat" düzeyde. İnsan oyuncu bot kadar verimli nişan alamayacağı için fazla sert olabilir.
- İlk 10 round'da 2–3 vuruşla hasat eski profildeki tek vuruşa göre yavaş hissettirebilir.
- R25–40 arasında tarla %70–85 dolu: "yetişemiyorum" hissi kasıtlı (hız / alan ödülleri bunu çözüyor) ama sıkıcı da olabilir.
- R45–50'de hız-alan build'lerinde hasat 5 round'da 1,7 katına çıkıyor (252 → 424): tatmin edici mi, kontrolsüz mü?

## 15. Testler ve loglar

Hepsi izole proje kopyasında (`Library/VerificationProject`) batch Unity ile koştu; senin editörüne, sahnene, seçili profiline ve kaydına dokunulmadı. Aşağıdaki sonuçlar **teslim edilen son değerlerle** (R10 boss hedefi 16 dahil) alındı.

### 15.1 İşlev testleri (geçti / kaldı)

| Test | Sonuç | Neyi doğrular |
|---|---|---|
| `DengeV1Verification` (yeni) | PASS · 86 kontrol | Aşağıda |
| `BossRewardVerification` | PASS · 126 kontrol | Boss + ödül akışı (3.4); `BossRewardManager` ve `PlayerController` değişikliklerinden sonra aynı |
| `RunPrototypeVerification` | PASS · 69 kontrol | Eski profiller, Don Cephesi (eski olay yolu) |
| `SpecializationVerification` | PASS · 100 kontrol | Uzmanlaşma (Bölüm 2) |
| `ElectricRevertVerification` | PASS · 40 kontrol | Elektriğin eski "ölünce tetiklenir" kuralı |
| `StartLoadoutVerification` | PASS · 87 kontrol | Çiftçi / tırpan seçimi, görevler, kayıt |
| `Run50ReferenceVerification` | PASS · 70 kontrol | `Run50_Referans` profili ve ölçümü yeniden üretilebilir |
| `MechanicsVerification` | PASS · 83 kontrol | Temel mekanikler |
| `RoundPreviewVerification` | PASS · 47 kontrol | Round önizlemesi |
| `HarvestBehaviorVerification` | PASS · 111 kontrol | Patlama, kasırga, bumerang, elektrik |
| `SimpleResonanceVerification` | PASS · 291 kontrol | Rezonans |
| `OptionsMenuVerification` | PASS · 22 satır | Ayarlar menüsü |

Hiçbir eski testin beklenen sayısı değişmedi (kontrol sayıları 3.4 teslimiyle aynı). `DengeV1Verification` de son üç ayar turunda beklenti değiştirilmeden geçti; beklentilerini asset verisinden okur (ör. alan basamakları ödülün gerçek çarpanından hesaplanır).

**`DengeV1Verification` neleri kontrol ediyor (86):**

- Profil ve menü: 50 round, 45 sn sabit süre, 9 boss, menü satırı ve işareti.
- Veri ayrımı: DengeV1 kendi temel stat / can / XP / ağaç verisini kullanıyor; eski profillerde `balance` boş; ortak asset'ler ayrı ve değişmemiş (ör. ortak Keskin Başlangıç-1 hâlâ 3 / 5 / 8 G, Aşırı Güç I hâlâ ×8).
- Ağaç: 104 düğüm, 36 kapalı yuva, her ön koşul set içinde, 5 kök, döngü ve yetim yok, süre düğümü yok; arayüzde 104 yuva görünür, kapalılar gizli, bağlantı çizgileri sete göre; satın alma, kademe değiştirme, ön koşul açılması, sıfırlama.
- Can: R1…R50 20 … 215, tekdüze, en büyük adım ×1,1; nadirlik doğrusal.
- XP: 140 level, azalmayan tablo; nadirliğe bağlı bitki XP'si (Common 20, Legendary 60).
- Saksı: taban üretim aralığı setten (8 sn); fiyat sette yoksa asset'ten, varsa setten; asset değişmiyor.
- Ödüller: geç ödüller round'undan önce sunulmuyor, erken ödül round'undan sonra sunulmuyor; koşullu ödüller (tarla doluluğu, davranış saksısı); Yıkım Gücü yalnız davranış hasarını, Altın Hedef yalnız Rare+ doğrudan hasarı, stat ödülleri tam veri değeri kadar değiştiriyor; `ClearAll` hepsini geri alıyor.
- Halka = erişim; hedefleme kuralı değişmedi (§10).
- Alan basamakları: formül = gerçek vuruş sayısı (Standart ve Dar Kesim, her aşama).
- Sis: beş yarıçapta tam bir basamak, metin gerçek sayıyı yazıyor, round sonunda yarıçap geri dönüyor; prototipin Sis'i eski kuralla.
- Eski profiller sahnede: `Run50_Referans` ve `Run50_BossPrototip` ortak 140 düğümlü ağaç, taban hasar 1, ortak can ve XP, 30 sn + süre düğümleri, saksı 5 sn, eski boss hedefleri ve 6 ödülle çalışıyor.

### 15.2 Dokunulmayanların doğrulanması

- Paket başında alınan SHA-1 listesi (232 dosya: seçili profil dosyası, eski profiller, ortak ağaç, CoreStat, can eğrisi, XP tablosu, bitkiler, saksılar, 3.4 boss ve ödül asset'leri, uzmanlaşmalar, başlangıç seçenekleri, rezonans kuralları, sahneler) teslimde yeniden kontrol edildi: **232 / 232 aynı**.
- Gerçek kayıt `meta.json`: SHA-1 aynı.
- Commit atılmadı; `HEAD` hâlâ `743b904`.
- Üretici depodaki kopyadan (`Tools/Balance/DengeV1/make_denge_v1.py`) yeniden çalıştırıldı: 256 DengeV1 dosyası bayt bayt aynı çıktı.

### 15.3 Ölçümler (test değil; geçti / kaldı yok)

`BalanceRunMeasurement` setleri. Ham loglar `Docs/Bolum3-5/olcum/` altına kopyalandı (`BalanceRuns_<set>.csv` round satırları, `_nodes.csv` alınan kademeler, `_rewards.csv` sunulan / alınan ödüller, `.txt` özet):

| Set | İçerik | Run | Bölüm |
|---|---|---|---|
| `final` | 11 politika × başlangıç kombinasyonu × 4 seed, 50 round | 44 | §5, §6.3, §7, §8.2–8.3, §9, §10, §11.2, §12 |
| `openings` | 8 açılış × 3 seed, 5 round | 24 | §8.1 |
| `xpspeed` | 5 ilk yatırım × 3 seed, 30 round | 15 | §8.4, §9 |
| `payback` | Dengeli; bir kategori hiç alınmaz × 2 seed | 8 | §8.5 |
| `rewardab` | Ödülsüz + 6 ödül yolu × 2 seed | 14 | §6.3 |
| `bossab` | 2 politika × (kuralsız / Don / Sert Kabuk / Sis) × 2 seed | 16 | §11.1 |

Yeniden üretmek için:

```
Tools\run-isolated-verification.ps1 -Method BalanceRunMeasurement.RunFinal -Full -TimeoutSec 9000
python Tools\Balance\DengeV1\doc_measured.py final outcome curve margin
```

Kopyalanan logları analiz etmek için `BALANCE_LOGS=Docs/Bolum3-5/olcum` ortam değişkeniyle aynı betikler çalışır. Aynı klasörde `IslevTestleri.txt` (12 paketin sonuç satırları) ve `DengeV1Verification.txt` (86 kontrolün tam listesi) de var.

Diğer giriş noktaları: `RunOpenings`, `RunXpSpeed`, `RunPayback`, `RunRewardAB`, `RunBossAB`, `RunSmoke`; işlev testi `DengeV1Verification.RunBatch`. Analiz betikleri varsayılan olarak `Library/VerificationProject/Logs` klasörünü okur; başka klasör için `BALANCE_LOGS` ortam değişkeni.

**Son değerlerle teyit.** R10 boss hedefi (20 → 16) ölçümlerden sonra değişti. Bunun üzerine `final` (44 run) ve `openings` (24 run) setleri teslim edilen asset'lerle yeniden koşuldu: 44 run'ın sonucu, skoru, level'ı, ağaç kademesi ve ödülleri öncekiyle **birebir aynı** (2.114 satırda değişen yalnız boss hedefi sütunu ve ±0,01 sn'lik hasat süresi yuvarlaması). Belgedeki `final` ve `openings` tabloları bu son koşudan. `xpspeed`, `payback`, `rewardab` ve `bossab` setleri hedef 20 iken koşuldu ve yeniden koşulmadı: dördünde de R10 boss'unda en düşük pay ×1,5 ve üstü, yani hedefin 16 olması hiçbir run'ın sonucunu değiştirmez (bot deterministik).

## 16. Kısa Play kontrolü (≈10 dk)

1. **Tools > Run Profili > Run50 Denge V1** seç, MenuScene'den Play, Bahçıvan + Standart ile başla.
2. İlk hazırlıkta ağaçta 5 kök görünmeli (Keskin Başlangıç, Hızlı Eller, Altın Hasat I, Düzenli Üretim, Grid Genişleme I); süre düğümleri ve ağacın sağ ucundaki tepe düğümler görünmemeli.
3. Round 1: süre 45 sn; Common ot çoğunlukla iki vuruşta ölmeli (hasar yazısı ≈ 10; kritik gelirse tek, düşük zar gelirse üç vuruş). İmleç halkası komşu hücrelerin merkezinden geçmeli ve halkanın içindeki her bitki vurulmalı, dışındaki vurulmamalı.
4. R1–4 boyunca yalnız saksı + bir aile alarak oyna; R5'te HUD "boss hasadı 8" hedefini göstermeli ve rahat geçilmeli.
5. Boss ödülü ekranı: 3 seçenek; metinde "yalnız kendi vuruşun" / "davranış hasarı" ayrımı okunmalı. R5'te Canavar Kesimi ve Fırtına Bileği **çıkmamalı**.
6. Geniş Süpürüş ailesini bitir (yarıçap 1,35): iki hücrenin arasına nişan alınca 6 bitki vurulmalı, halka buna uygun büyümeli.
7. Sis gelirse (yarıçap ≥ 1,24 iken): kural satırı "6 → 5 bitki" gibi gerçek sayıyı yazmalı; boss round'unda halka küçülmeli, round bitince eski hâline dönmeli.
8. Menüden **Run50 Referans**'a dön, bir round oyna: ağaç 140 düğüm, taban hasar 1, süre düğümleri yerinde olmalı (eski profil değişmedi). Sonra kendi profilini geri seç.

Dikkat edilecek his soruları (ölçüm cevaplayamaz): ilk 10 round iki-üç vuruşla hasat yavaş mı hissettiriyor; R25–35 arası tarla %80 doluyken "yetişemiyorum" mu "bolluk" mu hissi veriyor; R45+ hız / alan patlaması tatmin edici mi yoksa fazla mı.

## 17. Değişen ve eklenen dosyalar

Hiçbiri commit edilmedi. 3.4 ve öncesinden gelen commit'lenmemiş değişikliklerin üstüne eklendi.

**Yeni oyun kodu**

- `Assets/Scripts/ScriptableObjects/RunBalanceSO.cs`, `SkillTreeSetSO.cs` — profil bazlı denge verisi.
- `Assets/Scripts/Grid/HarvestArea.cs` — en iyi nişanın hücre sayısı ve "bir basamak aşağı" yarıçapı (yalnız sayım; hedefleme `GridSystem`'de).

**Değişen oyun kodu**

- `RunProfileSO.cs` (`balance` alanı) · `StatManager.cs`, `ProgressionManager.cs`, `SkillTreeManager.cs`, `PlantHealthCalculator.cs` (set varsa oradan okur) · `SkillTreeUI.cs`, `SkillNodeUI.cs` (yuva → düğüm eşlemesi, kapalı yuva gizlenir) · `PlantResource.cs` (nadirlik XP çarpanı) · `PlanterSO.cs`, `PlacementManager.cs`, `PlanterBrain.cs`, `GrassPlanterButtonUI.cs`, `PlanterShopPanelUI.cs` (`Price` / `PriceType`, taban stat) · `GridSystem.cs` (`HarvestReach`) · `PlayerController.cs` (halka erişimde çizilir; Rare+ doğrudan çarpanı) · `BossRewardSO.cs`, `BossRewardManager.cs` (davranış çarpanı, Rare+ doğrudan çarpanı, sunum penceresi, iki yeni koşul, tarla doluluğu örneklemesi) · `FogSO.cs`, `FogEvent.cs` (`stepDown`) · `Assets/Editor/RunProfileMenu.cs` (menü satırı).

**Yeni veri (üretici yazar)**

- `Assets/ScriptableObjects/RunProfiles/Run50_DengeV1.asset`
- `Assets/ScriptableObjects/Balance/` — `RunBalance_DengeV1`, `CoreStat_DengeV1`, `PlantHealth_DengeV1`, `Progression_DengeV1`, `SkillTreeSet_DengeV1`
- `Assets/ScriptableObjects/Skill Tree Upgrades/DengeV1/` — 104 düğüm
- `Assets/ScriptableObjects/BossRewards/DengeV1/` — 13 ödül + `BossRewardPool_DengeV1`
- `Assets/ScriptableObjects/SegmentEvents/DengeV1/` — `DonCephesi_V1`, `SertKabuk_V1`, `Sis_V1` · `Assets/ScriptableObjects/Bosses/BossPool_DengeV1.asset`

**Test ve araç**

- `Tools/Verification/Editor/DengeV1Verification.cs` (işlev testi), `BalanceRunMeasurement.cs` (ölçüm botu; test değil)
- `Tools/Balance/DengeV1/` — `denge_v1_params.py` (bütün sayılar), `make_denge_v1.py` (asset üretici), `treelib.py`, analiz betikleri (`analyze_runs.py`, `compare_groups.py`, `analyze_boss.py`, `margins.py`, `progress_table.py`, `show_run.py`, `doc_measured.py`, `doc_tables.py`)

**Dokunulmayanlar** (SHA-1 ile doğrulandı, §15): seçili profil dosyası, gerçek kayıt (`meta.json`), eski profiller, ortak ağaç (140 düğüm), ortak CoreStat / can eğrisi / XP tablosu, bitkiler, saksılar, 3.4 boss ve ödül asset'leri, uzmanlaşmalar, başlangıç seçenekleri, sahneler.

## 18. Ayar günlüğü

Her tur: değerleri değiştir → asset'leri üret → botla R1'den 50 round'luk run'lar → sonuca göre düzelt. Satırlar "Dengeli" politikanın medyanıdır (2 seed).

| Tur | Değişiklik | Hasat R5 / 10 / 20 / 30 / 40 / 50 | R50 ağaç kademesi | R50 level | Sonuç |
|---|---|---|---|---|---|
| 1 | İlk aday: hasar ×10 ölçeği, yeni can eğrisi, kısaltılmış ağaç, 13 ödül; hedefler kapalı (kimse elenmez) | 13 / 18 / 26 / 38 / 88 / 175 | 116 | 43 | Çok yavaş: can ekonomiden hızlı büyüyor, ağacın yarısı alınamıyor |
| 2 | Fiyatlar ≈ %30–60 indirildi; can eğrisi yumuşatıldı (R50 165); XP ikinci derece | 23 / 28 / 48 / 71 / 132 / 478 | 200 | 91 | Geç oyunda ağaç ve level taşıyor |
| 3 | Can sabit oranlı eğriye geçti (R50 215); XP'ye küp terim; Geniş Tarama ve Akıcı Kesim orta oyuna alındı; geç aileler pahalandı | 21 / 33 / 70 / 107 / 268 / 510 | 207 | 98 | Level 98: kart ekranı çok sık; üretim hep fazla |
| 4 | XP eğrisi sertleşti (`380 + 10L + 1,2L² + 0,045L³`); saksı üretim aralığı 5 → 8 sn | 20 / 28 / 65 / 92 / 190 / 323 | 180 | 62 | Level ve ağaç makul; hedefler açılabilir |
| 5 | Ekonomi aileleri ucuzladı (geri ödeyebilsin); hedefler açıldı (70…6000 / 15…900) | 20 / 28 / 56 / 87 / 223 / 392 | 201 | 66 | Hız-alan bir seed'de R35 boss'unda, "Yeni" R20 / R30'da eleniyor; ilk yatırımda hasar hızdan çok güçlü |
| 6 | Keskin Başlangıç +10 → +6 · 100 G; Hızlı Eller ×0,72 · 130 G; Hasat Deneyimi I 380 → 160 G; Bereketli Toprak yalnız tarla boşalıyorsa; Sis "bir basamak"; Geniş Savuruş ×1,2 → ×1,32; bot saksı payını tarla doluluğuna göre ayarlıyor | 15 / 20 / 56 / 96 / 294 / 541 | 211 | 76 | Ağaç neredeyse bitiyor: geç aile fiyatları düşük |
| 7 | Geç aile fiyatları yükseltildi; hedefler 50…6000 / 10…800 | (44 run'lık ölçüm) | 136–212 | 50–80 | 39 / 44 kazandı; Don Cephesi etkisiz; "Yeni" ilk boss'ta 1 puanla kalabiliyor; XP-önce karşılaştırması eşit bütçeli değildi |
| 8 | İlk hedefler 45 / 8; Don Cephesi %50 · ×2,5; Hasat Deneyimi I 125 G (Hızlı Eller'le aynı bütçe) | teslim ölçümü (§12) | §5 | §9 | Teslim edilen değerler |

Tur 1–7'nin ham logları teslim ölçümüyle değiştirildi (aynı dosya adları); yukarıdaki satırlar o turların kaydedilmiş özetidir.
