# Bölüm 3.2 — Run50 referans profili ve ölçüm

30 Eylül 2026. Bu bölüm dengeyi değiştirmez; mevcut oyunun 50 round içindeki davranışını ölçer. Plan belgesi: [Bolum3-1-Run50-GucVeIcerikPlani.md](Bolum3-1-Run50-GucVeIcerikPlani.md). O belgeye "10. Açık kararlar" bölümü eklendi.

**Etiketler:**
- **simülatör:** `RunSimulator`'ın hesabı; insan verisi değil.
- **sahne:** GameScene'de bot oyuncuyla ölçülen gerçek round.
- **model:** simülatörün yaklaşık hesabı; güvenilir değil.

## 0. Kısa sonuç
- **Kota elemesi:** `Run50_Referans`, uzun run'ın ilk 50 round'uyla aynı kotayı kullanıyor. Bu kotayla 50 round'da neredeyse kimse elenmiyor.
  - Deneyimli ve orta simülatör profillerinin 100/100 run'ı R50'ye ulaşıyor.
  - "Yeni" profilde 100 run'ın 8'i R35–40'ta eleniyor; bu run'larda ağacın ~%3'ü alınmış.
- **Sahnede asıl darboğaz saldırı sıklığı:** Hasar R10'dan itibaren neredeyse her bitkiyi tek vuruşta kesiyor (%94–100). R20'den itibaren üretim noktalarının %60–86'sı hasat beklerken dolu duruyor.
- **Alan tavanı:** Ağaç yarıçapı en fazla 1,6'ya çıkıyor. Bu, bir vuruşta en çok 6 bitki demek. Tam ağaçta bile tarla %83 dolu kalıyor; tarla temizleme hissi yok.

## 1. Profil: tam ayarlar ve kaynakları
Asset: `Assets/ScriptableObjects/RunProfiles/Run50_Referans.asset` (guid `167273a34ff6450396ae05633b389990`).
Menü: **Tools › Run Profili › Run50 Referans · 50 round (ölçüm tabanı)**.

| Alan | Değer | Kaynak / gerekçe |
|---|---|---|
| displayName | Run50 Referans · 50 round | – |
| runLength | 50 | Hedef run uzunluğu |
| segmentRounds | 5 | UzunRun130 ile aynı (`HarvestQuota.DefaultSegmentRounds`) |
| quotaStart / quotaGrowth | 10 / 1,45 | UzunRun130 ile aynı (`HarvestQuota.DefaultStart/DefaultGrowth`) |
| segmentTargets | boş | UzunRun130 gibi eğriden: `HarvestQuota.Target(s, 10, 1.45)` → `Nice` |
| Segment kotaları (R5…R50) | 10 · 15 · 20 · 30 · 45 · 65 · 95 · 130 · 200 · 280 | Aynı fonksiyonla hesaplandı. Test, UzunRun130'un ilk 10 segmentiyle birebir aynı olduğunu doğruluyor |
| events | yok | **Don Cephesi kullanılmıyor.** UzunRun130'da olay yok; ölçüm tabanına kural eklenmedi |
| specializationAfterSegment / options | 0 / yok | **Uzmanlaşma kullanılmıyor**, aynı gerekçe |
| startingGold / Iron / Stone | 80 / 0 / 0 | Normal başlangıç (`ResourceManager` ve diğer normal profiller) |
| debugBudget | kapalı | – |
| fixedRoundDuration | 0 | Süre mevcut kuralla: yükseltmeler, 60 sn üstü tempo |
| electricMode | KillChance | Eski (öldürmeyle tetiklenen) elektrik; alan gizli ve etkisiz |
| victoryTitle | REFERANS RUN TAMAMLANDI | R50 kotası geçilince geçici zafer. Final boss değil |

**Değişmeyenler:**
- Skill tree, can (`PlantHealthScaling`), kartlar, XP ve ekonomi değişmedi.
- Diğer beş profil değişmedi. Doğrulama:
  - Dosya özetleri iş başında ve sonunda aynı.
  - Testteki değer kontrolleri de aynı.
- Bu paket kullanıcının seçimini (`RunProfileSelection`) değiştirmedi. İş başında seçim Deney22_20 idi. 23:42'de açık editörde yeni menüden Run50 Referans seçildi (Editor.log: `RunProfileMenu.Reference50`) ve Play'e girildi. Bu kullanıcının kendi işlemi; geri alınmadı.

## 2. İki ölçüm koşulu
- **A · gerçek kural:** Kota tutmazsa run biter (oyundaki `RoundManager.EvaluateQuota` gibi). Başarısızlık round'u ve o anda erişilen içerik raporlanır.
- **B · kota nedeniyle elenme kapalı (yalnız ölçüm):** Run R50'ye kadar sürer; kotanın tuttuğu ya da tutmadığı kaydedilir.
  - B'nin sonuçları kazanma oranı değildir.
  - B yalnız simülatörde (`Rules.QuotaContinue`) ve sahne ölçüm kopyasında vardır; oyuncu akışına eklenmedi.
- Aynı seed'de A, B'nin ilk kota başarısızlığına kadar olan kısmıyla birebir aynı. Batch bunu her seed için kontrol ediyor: "evet".
- **Sahne ölçüm kopyası:** Runtime'da `Run50_Referans`'tan kopyalanır ve kaydedilmez. Tek fark `segmentRounds = runLength = 1000`: tek round ölçülür, kota değerlendirmesi ve run sonu olmaz.

## 3. Gerçekten yapılan ölçümler

| Ölçüm | Kapsam | Log |
|---|---|---|
| Simülatör, A ve B | 3 politika (Deneyimli, Orta, Yeni) × 100 seed (11000+) × 50 round; P10/P50/P90 | `Logs/RunSimReference50.txt`, `.csv` (her round medyanı) |
| Sahne, temsilî durumlar | Deneyimli ve Orta politikanın temsilî run'ı: R10/20/30/40/50 başındaki durum. 10 durum × 5 seed, tam round, kare 1/30 sn | `Logs/Run50ReferenceVerification.txt` |
| Sahne, uç durum | Bütün ağaç (her düğüm son kademe), Deneyimli R50 saksı ve tile'larıyla. 5 seed. **Normal R50 build'i değil** | aynı |
| Akış | GameScene'de 50 round, 10 segment geçişi, zafer, R20 kaybı, yeniden başlatma, ana menü | aynı |

**Temsilî run nasıl seçildi:** Koşul B'nin 100 seed'i arasından, R10–R50 skor ve hasarı medyana en yakın run.
- Deneyimli: seed 11091. Orta: seed 11002.
- İkisi de A'da bütün kotaları geçiyor; A'da da aynı run.
- Sahne, simülatör durumunu birebir kurdu. Hasar, saldırı aralığı, süre ve tempo her durumda eşleşti (testte kontrol).

**Sahne botu:**
- Her saldırıda şu nişan adaylarından en çok canlı bitkiyi kapsayanı seçer: hücre merkezi, iki hücre arası, dört hücre köşesi.
- İnsan isabeti ve tepki süresi değildir; üst sınıra yakın bir oyuncudur.
- **Düzeltme:** İlk koşuda bot yalnız hücre merkezlerine nişan alıyordu. Bu, yarıçap ≥1,24'te alanı eksik ölçüyordu (5 yerine 6 hedef). Bot düzeltildi, sahne ölçümü yeniden yapıldı. Bu belgedeki sahne değerleri ikinci koşudandır.

## 4. Sonuçlar

### 4.1 A · gerçek kural: kota elemesi (simülatör, 100 seed)
| Politika | R50'yi tamamlayan | Elenen (round) | En dar geçiş (segment skoru / kota) |
|---|---|---|---|
| Deneyimli | 100/100 | 0 | ×26,7 (segment 1) |
| Orta | 100/100 | 0 | ×7,4 (segment 8, R36–40) |
| Yeni | 92/100 | 8 (R35 × 3, R40 × 5) | ×0,66 (elenenler) |

- **Elenen 8 "Yeni" run'ı, elendiği anda:**
  - Level P50 16, ağaç %3, hasar 9, 33 saksı.
  - 2×2 ve 2×3 saksı yok; davranış kilidi yok.
  - Hızlı Eller yalnız %13'ünde var.
- Yani eleme, ağacı neredeyse hiç almayan run'ları yakalıyor; kota bugün bir build baskısı kurmuyor.

### 4.2 B · R10–R50 simülatör değerleri (P50; parantez P10–P90; n = 100 her satırda)
**Deneyimli**

| | R10 | R20 | R30 | R40 | R50 |
|---|---|---|---|---|---|
| Hasar | 61 | 67 | 92 | 763 (482–1151) | 1899 (1401–3028) |
| Kritik şans / çarpan | %30 / ×2 | %30 / ×2 | %40 / ×2,2 | %45 / ×2,3 | %45 / ×2,5 |
| Saldırı aralığı (tempo sonrası) | 2,90 sn | 2,10 | 2,10 | 2,00 | 1,56 (0,98–1,89) |
| Yarıçap | 1,00 | 1,03 | 1,30 | 1,30 | 1,57 |
| Süre / tempo | 45 / 1 | 45 / 1 | 48 / 1 | 57 / 1 | 60 / 1,17 (1–1,5) |
| Level / alınan kart (toplam) | 15 / 14 | 30 / 29 | 53 / 52 | 70 / 69 | 75 / 74 |
| Tile / açık hücre | 9/49 | 24/49 | 46/81 | 63/81 | 68/121 |
| Ağaç | %11 | %21 | %36 | %46 | %59 |
| Round geliri G / I / S | 200 / 17 / 1 | 335 / 98 / 34 | 476 / 320 / 60 | 638 / 593 / 169 | 2265 / 1539 / 556 |
| Round skoru | 156 | 1432 | 2285 | 3090 | 5980 |
| Segment skoru / kota | ×46 | ×125 | ×166 | ×111 | ×92 |
| Hasat / üretim tavanı | %62 | %16 | %15 | %14 | %15 |
| Davranış payı (MODEL) | %0 | %14 | %21 | %27 | %28 |
| Toplam oturum (tahmin) | 19 dk | 40 dk | 64 dk | 86 dk | 108 dk |

**Orta**

| | R10 | R20 | R30 | R40 | R50 |
|---|---|---|---|---|---|
| Hasar | 61 | 64 | 92 | 335 (131–641) | 1405 (953–1899) |
| Saldırı aralığı | 2,90 | 2,10 | 2,10 | 2,10 | 1,96 |
| Yarıçap | 1,00 | 1,03 | 1,30 | 1,30 | 1,30 |
| Süre / tempo | 45 / 1 | 45 / 1 | 45 / 1 | 48 / 1 | 55 / 1 |
| Level / kart | 15 / 14 | 28 / 27 | 42 / 41 | 59 / 58 | 71 / 70 |
| Ağaç | %10 | %18 | %30 | %38 | %47 |
| Round geliri (G + 7I + 14S) | 262 | 367 | 955 | 3127 | 6270 |
| Round skoru | 116 | 156 | 552 | 1942 | 3153 |
| Segment skoru / kota | ×35 | ×24 | ×26 | ×62 (P10 ×8,7) | ×53 |
| Hasat / üretim tavanı | %54 | %14 | %16 | %15 | %14 |
| Oturum | 19 dk | 38 dk | 59 dk | 79 dk | 100 dk |

**Yeni** (P50): hasar 9 → 64 → 79 → 99 → 131; saldırı aralığı 3,0 → 2,6 → 2,1; level 5 → 19 → 31 → 43 → 56; ağaç %3 → %36; segment skoru / kota ×15 → ×5,3 (P10 ×3,5); oturum 12 → 87 dk. A'da ilk başarısızlık R35'te 3, R40'ta 5 run; B'de segment 8'in (R40) kotasının altında kalan 8 run.

### 4.3 Can ve vuruş
**Can** (`PlantHealthScaling`, Common / Uncommon / Rare / Epic / Legendary):

| Round | Can |
|---|---|
| R10 | 15 / 18 / 24 / 30 / 45 |
| R20 | 27 / 32 / 43 / 53 / 80 |
| R30 | 44 / 52 / 69 / 87 / 130 |
| R40 | 66 / 79 / 105 / 131 / 196 |
| R50 | 98 / 118 / 157 / 196 / 294 |

**Gereken vuruş** (simülatör, P50 hasarla, kritiksiz; Deneyimli ve Orta):
- R10, R40 ve R50'de bütün nadirlikler 1 vuruş.
- R20: Legendary 2.
- R30: Legendary 2; Epic sapmaya göre 1–2 (hasar×0,85 ≥ can yalnız %0–41 run'da).

**Sahnede ölçülen** (taze bitki: tam canlıyken vuruldu; "tek vuruş" = aynı saldırıda doğrudan hasatla öldü):

| Durum | Tek vuruş (bütün) | Legendary | Epic |
|---|---|---|---|
| D R10 | %100 (293/293) | – (hasat yok) | – |
| D R20 | %98 | %43; ölenler ort. 1,42 vuruş, ilk vuruştan hasada 2,5 sn | %100 |
| D R30 | %94 | %17; 1,79 vuruş, 3,3 sn | %81; 1,19 vuruş, 0,7 sn |
| D R40 | %100 | %96 | %100 |
| D R50 | %99 | %100 | %100 |
| O R10–R50 | %99–100 | R30: 5 taze Legendary'nin 2'si tek vuruş, kalan 3'ünü aynı saldırıda davranış bitirdi | %96–100 |

Tek hedef için yaklaşık hasat süresi = vuruş × saldırı aralığı:
- R10–R40'ta Common–Epic için ~2,1–2,9 sn (tek vuruş).
- R20–30'da Legendary için ~4,2 sn.
- Belirleyici olan hasar değil, saldırı aralığı.

### 4.4 Düğüm erişimi (B; o round'da ilk kademesi aktif olan run oranı)
| Düğüm | Deneyimli R10/20/30/40/50 | Orta | Yeni |
|---|---|---|---|
| Hızlı Eller - 1 | 100 / 100 / 100 / 100 / 100 | 100 hep | 31 / 85 / 91 / 93 / 97 |
| 2×3 Saksı | 0 / 100 / 100 / 100 / 100 | 0 / 100 / … | 0 / 0 / 9 / 50 / 86 |
| Patlayıcı Kartlar | 94 / 100 / … | 3 / 100 / … | 0 / 11 / 67 / 89 / 94 |
| Tornado Kartları | 87 / 100 / … | 0 / 100 / … | 0 / 10 / 57 / 87 / 92 |
| Bumerang Orak Kartları | 0 / 100 / … | 0 / 93 / 100 / … | 0 / 6 / 43 / 79 / 90 |
| Çapraz Elektrik Kartları | 0 / 97 / 100 / … | 0 / 3 / 100 / … | 0 / 1 / 50 / 83 / 91 |
| Grid Genişleme II | 0 / 7 / 96 / 100 / 100 | 0 / 0 / 26 / 55 / 100 | 0 / 0 / 6 / 30 / 42 |
| Güçlü Kesim - 1 | 0 / 0 / 69 / 100 / 100 | 0 / 0 / 1 / 100 / 100 | 0 / 2 / 15 / 53 / 84 |
| Ağır Kesim - 1 | 0 / 0 / 10 / 96 / 100 | 0 / 0 / 0 / 77 / 100 | 0 / 0 / 0 / 0 / 40 |
| Akıcı Kesim - 1 | 0 / 0 / 2 / 94 / 100 | 0 / 0 / 0 / 37 / 100 | 0 / 0 / 0 / 9 / 50 |
| Son Vardiya - 1 | 0 / 0 / 53 / 91 / 100 | 0 / 0 / 1 / 42 / 83 | 0 / 0 / 0 / 0 / 4 |
| Hasat Rekoru - 1 | 0 / 0 / 0 / 13 / 75 | 0 / 0 / 0 / 0 / 17 | 0 |
| Plazma Kesim - 1 | 0 / 0 / 0 / 0 / 13 | 0 | 0 / … / 1 |
| Aşırı Güç I | 0 | 0 | 0 |

Tam liste (25 düğüm) `Logs/RunSimReference50.txt` içinde.

### 4.5 Sahne ölçümü (5 seed; ortalama, köşeli parantezde en az–en çok)
| Durum | Hasar · aralık · yarıçap | Süre (tempo) | Üretim noktası | Hasat (doğrudan + davranış) | Davranış payı | Vuruş başına hedef | Doluluk | Hasat / tavan | Round skoru | Doğrudan hasat sahne / simülatör |
|---|---|---|---|---|---|---|---|---|---|---|
| D R10 | 61 · 3,0 · 1,0 | 45 | 9 | 59 [55–63] (59 + 0) | %0 | 4,2 | %43 | %55 | 149 | ×1,03 |
| D R20 | 67 · 2,1 · 1,03 | 45 | 49 | 114 [111–118] (103 + 11) | %10 | 5,0 | %80 | %17 | 1226 | ×1,27 |
| D R30 | 92 · 2,1 · 1,3 | 48 | 62 | 128 [123–133] (125 + 3) | %2 | 6,0 | %81 | %13 | 1302 | ×1,14 |
| D R40 | 682 · 2,1 · 1,3 | 60 | 62 | 170 [168–171] (168 + 2) | %1 | 6,0 | %81 | %12 | 1689 | ×1,23 |
| D R50 | 1755 · 1,42 · 1,6 | 60 (1,28) | 92 | 310 [286–330] (251 + 59) | %19 | 6,0 | %83 | %12 | 3589 | ×0,90 |
| O R10 | 61 · 2,9 · 1,0 | 45 | 9 | 52 [51–54] (52 + 0) | %0 | 3,5 | %33 | %65 | 99 | ×1,00 |
| O R20 | 64 · 2,1 · 1,03 | 45 | 49 | 147 [130–167] (104 + 42) | %28 (kasırga) | 5,0 | %67 | %30 | 283 | ×1,43 |
| O R30 | 92 · 2,1 · 1,3 | 45 | 49 | 202 [193–208] (125 + 78) | %38 (kasırga) | 6,0 | %60 | %37 | 1407 | ×1,34 |
| O R40 | 575 · 2,03 · 1,3 | 52 | 56 | 309 [300–321] (150 + 159) | %51 (kasırga) | 6,0 | %61 | %36 | 5087 | ×1,34 |
| O R50 | 1401 · 1,82 · 1,4 | 56 | 56 | 361 [340–375] (179 + 181) | %50 (kasırga) | 6,0 | %61 | %37 | 5461 | ×1,19 |
| **Uç: tam ağaç** | 3,2 milyon · 0,133 · 1,6 | 60 (1,5) | 92 | 3065 [3024–3112] (2690 + 374) | %12 | 6,0 | %83 | %19 | 76 874 | – |

**Tablo notları:**
- **Doluluk:** Üretim noktalarının dolu olduğu zaman oranı.
  - Yüksekse bitki hasat bekliyor.
  - Hiçbir durumda boşa saldırı yok: her saldırıda canlı hedef vardı.
- **Hasat / tavan:** Hasat ÷ (üretim noktası × süre ÷ üretim aralığı).
- **Davranış payı:** Build'e ve yerleşime göre çok değişiyor.
  - Deneyimli run: kartlarda nadirliği seçti, saksı altında az davranış tile'ı var → %1–19.
  - Orta run: kasırga tile'ları var → %28–51.

### 4.6 Alan: vuruş başına hedef basamaklı
`GridSystem.GetGridObjectsInRadius` yarıçapa yarım hücre (hücre 2 birim) ekliyor. En iyi nişanla kapsanan hücre:

| Yarıçap | En iyi nişan | Hücre |
|---|---|---|
| 1,00–1,23 | Merkez | 5 |
| 1,24–1,82 | İki hücre arası | 6 |
| 1,83–2,15 | Merkez | 9 |
| 2,16 ve üstü | Köşe | 12 |

- **Ağacın yarıçap basamakları:**
  - Taban 1,0.
  - Geniş Süpürüş +%30 → 1,3.
  - Geniş Tarama +%30 → 1,6.
- **Sahnedeki etkisi** (vuruş başına 5 → 6 → 6):
  - 1,0 → 1,3 hedefi 5'ten 6'ya çıkarıyor.
  - 1,3 → 1,6 en iyi nişanda **hiç hedef eklemiyor**; yalnız kötü nişanı affediyor.

## 5. Simülatörün sınırları
- **Davranışlar modeldir:**
  - Beklenen değer; "canlı bitki" olasılığı 0,25; kasırga 8 hücre × doluluk, bumerang 2,5 gibi sabitler.
  - Model %7–33 davranış payı veriyor; sahnede aynı durumlar %1–51.
  - Davranış payı ve toplam hasat için simülatör güvenilir değil. Sahne ölçümü de yalnız iki temsilî yerleşimi kapsıyor.
- **Nişan:**
  - Simülatör "isabet" ile ortalama ve en iyi geometrik kapsama arasında bir değer kullanıyor; canlı bitkinin yerini bilmiyor.
  - Bot canlı bitkiye nişan alıyor.
  - Doğrudan hasatta sahne / simülatör oranı ×0,90–1,43; R10'da ×1,00–1,03.
- **Üretim tavanı:** Simülatör sürekli üretim varsayıyor. Oyunda üretim noktası doluyken yeni bitki çıkmıyor. "Hasat / tavan" bir kapasite oranıdır; doluluk değildir.
- **Kart ve alışveriş:**
  - Politika seçimleri kurallı: en büyük saksı, hedef round sırasıyla skill, kartta nadirlik ve rezonans.
  - Politikalar 130 round'a göre, 29 Eylül'deki iki insan run'ına kalibre edildi; 50 round için yeniden kalibre edilmedi.
  - Grid dolunca verilen Temel güç kartları modelde yok. 50 round'da grid dolmadığı için sayı 0.
- **Süre:** Oturum dakikası tahmindir: menü 30 sn + kart 15 sn + kademe 8 sn.
- **Round başlangıcı:** Sahne ölçümü her round'a boş tarlayla başlıyor; simülatör sürekli durum varsayıyor.
- **Olay ve uzmanlaşma:** Bu profilde yok; bu ölçüm onları kapsamıyor.
- **Sahne botu:** Tepki süresi ve imleç yolu yok; kare 1/30 sn, en çok saniyede 30 saldırı. Durum başına 5 seed, politika başına tek temsilî run. Build dağılımı değil, tipik bir build ölçüldü.

## 6. En önemli üç darboğaz
1. **Saldırı sıklığı (hasat hızı).**
   - Saldırı aralığı R10'da ~2,9 sn, R20'den R40'a kadar ~2,1 sn'de sabit, R50'de 1,4–1,8 sn.
   - R20'den itibaren sahnede üretim noktalarının %60–86'sı dolu bekliyor. Hasat, üretim tavanının yalnız %12–37'si. Hiçbir saldırı boşa gitmiyor.
   - Hasar değil, vuruş sayısı sınırlıyor.
   - Bölüm 2.2 bulgusuyla uyumlu: hız tek anlamlı alım.
2. **Alan basamağı.**
   - Yarıçap en fazla 1,6. Vuruş başına en çok 6 bitki; 9 bitki için 1,83 gerekiyor.
   - Geniş Tarama (1,3 → 1,6) en iyi nişanda hedef eklemiyor.
   - Tam ağaçla bile tarla %83 dolu; endgame'de "tarla temizleme" yok.
   - Geniş alan hasadı bugün yalnız davranışlarla (Orta run'da kasırga %38–51) oluyor.
3. **Hasar doygunluğu ve can eğrisi.**
   - R10'da hasar 61, Legendary canı 45: her şey tek vuruş.
   - R20–30'da yalnız Legendary (ve R30'da kısmen Epic) 1,4–1,8 vuruş istiyor.
   - R40–50'de hasar 682 → 1755, en yüksek can 294.
   - R30–40'ta alınan hasar düğümleri (Güçlü Kesim, Ağır Kesim, Kesim Ustalığı) doğrudan hasada ölçülebilir katkı vermiyor. Davranış hasarını da büyütüyorlar; bunun davranış öldürmelerine etkisi ayrı ölçülmedi.

**Ek bulgu, kota:** 10 × 1,45^n eğrisi 50 round'da bir baskı kurmuyor.
- Deneyimli en dar ×27, Orta ×7,4.
- Boss'un kendi başarı koşulu (3.1 belgesi, 10.1) ve Run50 kota eğrisi aynı kararın parçası.

## 7. Sonraki küçük uygulama paketi için öneri
**Öneri:** Plan sırasındaki **3.3 (çiftçi / tırpan ve görev altyapısı)** ile devam etmek. Denge değerlerine bağlı değil; kalıcı kayıt, seçim ekranı ve run'a uygulama/temizlik erken sağlamlaşır.

3.2 ölçümleri 3.3'ün içeriğini şöyle etkiliyor:
- **Tırpan değerleri yarıçap basamaklarına göre seçilmeli.** Geniş Orak'ın ×1,2'si 1,0'da (→1,2) ve 1,3'te (→1,56) hiçbir hedef eklemez; yalnız 1,6'da (→1,92) 6 → 9 yapar. Taslak değerler 3.3'te bu tabloya göre yeniden seçilmeli.
- **Seri Orak asıl darboğaza dokunuyor.** Saldırı aralığı ×0,8 = +%25 vuruş. Dar Kesim'in hasar ×1,4'ü bugünkü doygunlukta yalnız Legendary'de işe yarar.
- **Tüccar ve Âlim** ekonomi/XP tarafında; ölçülen darboğazlarla çakışmıyor, güvenli ilk içerik.

**3.4'ten önce kullanıcı kararı gereken iki konu:**
- Boss'un kendi başarı koşulu (3.1 belgesi, 10.1).
- Run50 kota eğrisi (bugün baskı yok).

**3.5 için not:** Can eğrisini yükseltmek tek başına yetmez. Darboğaz saldırı sıklığı ve alan olduğu için, can artışı R10'da ~2,9 sn'lik aralıkla oyunu yavaşlatır (3.1 belgesi, 10.6). Can, saldırı aralığı ve yarıçap birlikte ölçülmeli.

## 8. Doğrulama ve değişen dosyalar
**Değişen dosyalar** (oyun kodu yok):
- `Assets/ScriptableObjects/RunProfiles/Run50_Referans.asset` (+ meta): yeni profil.
- `Assets/Editor/RunProfileMenu.cs`: menü seçeneği, doğrulayıcı, etiket (editör kodu).
- `Tools/Verification/Editor/RunSimulator.cs`:
  - `Rules.QuotaContinue` (yalnız ölçüm), `RunResult.QuotaFailedAt`.
  - `RoundLog`'a savaş kapasitesi / tek vuruş modeli alanları.
  - `Reference50Rules`, `Reference50Representative`, `FullTreeEffects`, `RunReference50Batch`.
- `Tools/Verification/Editor/Run50ReferenceVerification.cs`: yeni test (profil, menü, akış, sahne ölçümü).
- `Docs/Bolum3-2-Run50-ReferansOlcum.md` (bu belge), `Docs/Bolum3-1-Run50-GucVeIcerikPlani.md` (10. bölüm eklendi).

**Testler** (izole kopya `Library/VerificationProject`, açık editöre dokunmadan; bu oturumda çalıştırıldı):

| Test | Sonuç | Neden çalıştırıldı |
|---|---|---|
| Run50ReferenceVerification (yeni) | **69 / 69** | Profil, menü, 50 round akışı, sahne ölçümü (11 durum × 5 seed) |
| RunPrototypeVerification | 69 / 69 | Profil seçimi, kota akışı, Don Cephesi |
| SpecializationVerification | 100 / 100 | Profil listesi ve Uzmanlaşma20 akışı |
| ElectricRevertVerification | 40 / 40 | Profil alanları (elektrik modu) listesi |
| MechanicsVerification | 83 / 83 | UzunRun130 profili |
| RunSimulator.RunNodeAccessBatch (regresyon) | Bölüm 3.1 CSV'si ile **bayt bayt aynı** | Simülatör çekirdeği değişmedi (`QuotaContinue` kapalıyken aynı sonuç) |
| RunSimulator.RunReference50Batch | 20 sn, hata yok | Bu belgedeki simülatör tabloları |

**Çalıştırılmayanlar:** HarvestBehaviorVerification, SimpleResonanceVerification, RoundPreviewVerification, EconomyAnalyzerVerification. Bu paket oyun kodunu ve bu testlerin kullandığı asset'leri değiştirmedi; son sonuçları Bölüm 3.0'dan (111 / 291 / 47 / 448).

**Test sırasında düzeltilenler:**
- Yeni testin ilk derlemesinde bir tuple tip hatası (test kodu).
- Botun yalnız hücre merkezine nişan alması (bölüm 3). Bu nedenle ölçüm yeniden yapıldı.

Oyun kodunda hata bulunmadı.
