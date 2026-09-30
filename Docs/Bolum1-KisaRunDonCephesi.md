# Bölüm 1 — Kısa run ve ilk boss segmenti (uygulama notu)

Tarih: 30 Eylül 2026. Kaynak: kullanıcının "BÖLÜM 1" talimatı (taslak: `RunBuildBossTaslagi-20260930.md`, çelişkide talimat geçerli).
Buradaki bütün sayılar **test başlangıç değeridir**, dengelenmiş değildir.

## 1. Ne uygulandı

### Run profili (tek yapılandırma)
- `RunProfileSO` (`Assets/ScriptableObjects/RunProfiles/`): run uzunluğu, segment round sayısı, kota eğrisi (başlangıç, büyüme), segment kota tablosu, segment olayları, başlangıç ekonomisi, debug bütçe bayrağı, zafer başlığı.
- Aktif profil: `Assets/Resources/RunProfileSelection.asset`. Seçim menüsü **Tools > Run Profili** (sahneye dokunmaz).
- Profiller:

  | Profil | Round | Kota | Olay | Ekonomi |
  |---|---|---|---|---|
  | `UzunRun130` (varsayılan) | 130 | 10 × 1,45^(s−1) | yok | 80 Gold |
  | `Prototip10` | 10 | tablo: 40 / 200 | 2. segment: Don Cephesi | 80 Gold |
  | `Prototip10_DebugButce` | 10 | tablo: 40 / 200 | 2. segment: Don Cephesi | 200.000 ×3 (yalnız editör) |

- Profil seçiliyken ResourceManager'daki sahne debug bütçesi yok sayılır; yüksek bütçe yalnız debug profiliyle gelir. "Profil yok" seçeneği eski sahne alanlarını kullanır (130 round, sahnedeki debug bütçe).
- 40/50 round'a geçmek için yeni bir profil asset'i yeterli; kodda sabit sayı yok.

### Kota akışı (mevcut sistem korunarak doğrulandı)
- Segment skoru segment boyunca birikir; tam eşitlik başarıdır; fazla skor sonraki segmente taşınmaz; toplam skor korunur; kota erken dolsa da kaynak ve XP kazanılır; hedef gizlice değişmez.
- `HarvestScoreManager.TotalScore` artık `long`: 2,1 milyarı aşan skor segment ilerlemesini durdurmaz.

### Don Cephesi
- Veri: `FrostFrontSO` → `Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset` (çarpan 1,5; kapsama 0,25; seed 0 = her run farklı).
- Uygulama: `FrostFrontEvent`. Açık alanın dört kenar şeridinden biri seçilir (kalınlık = kenar × 0,25, en az 1, alanın tamamı asla; o anki bütün üretim noktalarını kaplayan şerit seçilmez). Şerit grid satır/sütunu olarak saklanır; önizleme ve uygulama aynı hücreler.
- Yürütücü: `SegmentEventDirector` (RoundManager kurar). Olay bir önceki segmentin başında duyurulur (ilk boss için run başında), segmentin ilk round'unda etkinleşir, son round'u bitince kalkar. Run sonu, ana menü ve yok edilmede (yeniden başlatma, sahne değişimi) aynı `ClearAll` yolu.
- Etki: yalnız şeritteki **üretim noktalarının** nihai üretim aralığı ×1,5 (`PlantSpawner`, taban ve tempodan sonra). Saksının şerit dışındaki noktaları etkilenmez. Bitki, saksı, tile ve rezonansa dokunulmaz; üretim tabanı taşması → nadirlik hesabı değişmez.
- Gösterim (yalnız okuma): dünyada şerit (yaklaşırken taralı, aktifken dolu buz rengi, saksıların üstünde), round haritasında hücre örtüsü, HUD kartı (boss segmentinde buz rengi, "BOSS KOTASI", alt satırda kural ya da "Yaklaşan"), round özeti ("HAZIRLIK" kartı, yaklaşan / son hazırlık / aktif / geçti), round intro ("DON CEPHESİ BAŞLADI · KOTA 200").

### Run sonu
- Zafer: "PROTOTİP TAMAMLANDI · 10 round · son kota …". Kayıp: "KOTA TUTMADI · Round N · segment skoru a / b (gereken kota)".
- Ana menü butonu artık `MenuScene`'i yüklüyor (önceden yalnız durumu değiştiriyordu, oyuncu oyun sahnesinde kalıyordu).

### Ön koşul düzeltmeleri
- **Skor tavanı:** `long` skor (yukarıda).
- **Elektrik:** efekt havuzu doluyken yalnız çizim atlanır, hasar uygulanır (`ElectricBurst.Strike`, `HarvestBehaviorManager.SkippedElectricVisuals`). Bumerang ve kasırga fiziksel yol kullandığı için havuz sınırında hâlâ atlanır (değiştirilmedi).
- **Saldırı zamanlayıcısı:** `PlayerController.AdvanceAttackTimer` taşan süreyi korur; bir karede en fazla bir vuruş, birikim en fazla bir aralık. Economy Analyzer aynı kuralı kullanır.

## 2. Unity'de çalıştırma
1. **Tools > Run Profili > Prototip · 10 round (normal ekonomi)** (ya da debug bütçe).
2. Play (MenuScene'den ya da GameScene'den).
3. Geri dönmek için **Tools > Run Profili > Uzun run · 130 round** ya da **Profil yok (sahnedeki ayarlar)**.

## 3. Ayarlanabilir değerler
| Değer | Nerede |
|---|---|
| Run uzunluğu, segment round sayısı, kota eğrisi, kota tablosu, olaylar, başlangıç kaynakları, debug bütçe, zafer başlığı | `Assets/ScriptableObjects/RunProfiles/*.asset` |
| Aktif profil | `Assets/Resources/RunProfileSelection.asset` (Tools > Run Profili) |
| Don Cephesi çarpanı, şerit kapsaması, seed, renk, metin | `Assets/ScriptableObjects/SegmentEvents/DonCephesi.asset` |
| Profil yokken 130 round / kota | GameScene → Round Manager |

## 4. Doğrulama
Çalıştırıcı: `Tools/run-isolated-verification.ps1 -Method <Sınıf.RunBatch> [-Full]`. Test kaynakları: `Tools/Verification/Editor` (sürüm kontrolünde; önceden yalnız `Library` altındaydı).

| Test | Sonuç | Kapsam |
|---|---|---|
| RunPrototypeVerification (yeni, GameScene) | 69 / 69 | Profil ve ekonomi; önizleme = uygulama (dünya, harita, HUD, özet); kota eşit (40/40), üstü (55/40, 260/200), altı (150/200); segment sıfırlama ve toplam skor; yalnız şeritteki üretim noktalarının ×1,5 yavaşlaması (2×2 saksı yarısı); tabanda da yavaşlama, nadirlik değişmez; boss açık/kapalı sayım; zafer, kayıp, yeniden başlatma, ana menü; kart seçimi, dükkân, sonraki round; long skor; elektrik havuzu dolu; saldırı zamanlayıcısı |
| MechanicsVerification | 83 / 83 | Önceki mekanikler ve kota (run sonu metni yeni ekrana göre güncellendi) |
| RoundPreviewVerification | 47 / 47 | Önizleme, yeni tile vurgusu |
| EconomyAnalyzerVerification | 448 / 448 | Analizörün yeni saldırı zamanlayıcısı kuralıyla |
| HarvestBehaviorVerification | 111 / 111 | Havuz sınırı kontrolü artık sahnedeki değeri okur (sahnede 12'ye çıkarılmıştı, test 8 bekliyordu) |
| SimpleResonanceVerification | 291 / 291 | Rezonans, elektrik XP, skor |

Aynı başlangıç ve seed ile oyunda üretim sayımı (round 6 Don açık, round 7 çarpan 1; her iki round 30 sn, bitki doğar doğmaz hasat):

| Üretim noktası | Don açık | Don kapalı |
|---|---|---|
| Şeritteki 3 nokta (toplam) | 9 | 15 |
| Şerit dışındaki 6 nokta (toplam) | 30 | 30 |

Simülatör (`RunSimulator.RunPrototypeBatch`, 30 seed, Don simülatörde yaklaşık modellenir):

| Profil | 1. segment (kota 40) | Zafer (Don açık) | Zafer (Don kapalı) | 2. segment skoru açık/kapalı |
|---|---|---|---|---|
| Deneyimli | 30/30 · skor ~279 | 30/30 · skor ~646 | 30/30 | ~1,00 |
| Orta | 30/30 · skor ~270 | 30/30 · skor ~539 | 30/30 | ~1,00 |
| Yeni | 30/30 · skor ~100 | 19/30 · skor ~248 | 19/30 | ~1,01 |

## 5. Açık sorunlar ve test edilmesi gereken varsayımlar
1. **Don Cephesi kotayı neredeyse etkilemeyebilir.** Oyunda şeritteki üretim gerçekten %40 düşüyor; ama simülatörde erken oyunda hasat üretimle değil saldırıyla sınırlı (hasat oranı %23–64), bu yüzden 2. segment skoru açık/kapalı aynı. İnsan testinde fark hissedilmezse seçenekler: çarpanı 2,0–2,5'e çıkarmak ya da hasat tarafına dokunan bir kural (Sert Kabuk). Karar oyun testine bağlı.
2. **Kota hedefleri (40 / 200) test değeri.** Simülatörde orta ve deneyimli profiller 2. segmenti 2,5–3 katla geçiyor; deneyimli oyuncu için prototip muhtemelen kolay. Yeni profil 30'da 19 kazanıyor.
3. **Karşı hamle alanı dar olabilir.** 3×3 açık alanda şerit 9 hücrenin 3'ü. Taşıma yok: saksıyı şeritten çıkarmak satış (fiyatın yarısı iade) + yeniden alım demek; şerit dışına yeni alan için 5×5 grid (80 Gold) gerekiyor, bu da başlangıç 80 Gold'la saksıyla yarışıyor. Bunun yeterli olup olmadığı otomatik testle ölçülemez; oyun testinde gözlenmeli. Yeni mekanik eklenmedi.
4. **Şerit grid satırı/sütunudur.** Grid büyüyünce aynı satır/sütunun yeni açılan hücreleri de şeritte kalır (önizleme ve uygulama tutarlı); şeridin açık alana oranı değişir (3×3'te %33, 5×5'te %20).
5. **Seçim run başında yapılır.** O an saksı yoksa dört kenardan biri rastgele seçilir (seed 0). Karşılaştırma için `DonCephesi.asset` → seed.
6. **Varsayılan profil değişti.** Seçim `UzunRun130` (normal ekonomi): editörde sahnedeki 200.000 debug bütçe artık varsayılan değil. Eski davranış için "Profil yok".
7. **Bumerang ve kasırga** efekt havuzu dolunca hâlâ atlanıyor (fiziksel yol kullanıyorlar); bu pakette değiştirilmedi.
8. **Simülatör** Don'u saksı hücresi oranıyla yaklaşık modeller; gerçek oyundaki üretim noktası eşleşmesi birebir değildir.
9. **Test düzeneği:** testte saksılar yerleştirme sistemi atlanarak konduğu için 2×2 saksı ekran görüntüsünde ghost görünümünde; oyunda etkisi yok.
