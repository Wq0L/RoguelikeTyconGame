# Bölüm 3.7.1 — Başlangıç erişimi gösterimi ve regresyonların toparlanması

**Durum (2026-10-02):** P1'in altı maddesi yapıldı ve izole kopyada doğrulandı; P0'dan yalnız "mevcut durumu kaydet" maddesi
yapıldı. P0'daki tasarım kararları **açık** duruyor. Yeni mekanik, yeni profil ve denge değişikliği yok. Commit atılmadı; seçili
profil (`Run50_KirilmaV1`) ve oyuncu kaydı değişmedi. İnsan Play kontrolü yapılmadı (9. bölümde kısa liste var).

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md)

## Kısa sonuç

- **Kasırga ve bumerang ücretsiz açılmıyor.** Bu profilde run başında yalnız patlama ve elektrik kilidi açık; kasırga, bumerang,
  çifte hasat ve üç saksı kilitli. Yanıltan şey görünümdü (2. ve 3. bölüm).
- **Başlangıçtan açık düğümler artık satın alınmış gibi görünmüyor.** Kendi rengi, düğümün altında "BAŞLANGIÇTAN AÇIK" yazısı,
  kademe noktası yok, satın alma seviyesi 0. Tooltip de bunu söylüyor.
- **Ağaçta bir çizim hatası daha bulundu ve düzeltildi:** bağlantı okları başlangıç düğümünü "alınacak" gibi gösteriyor, arkasındaki
  düğümlerin (Çapraz Elektrik, Tornado) çizgisi hiç çizilmiyordu.
- **3.6 düzeltmelerinde gerileme yok:** Kıvılcım, boss HUD renkleri, bölge çizgisi ve hava efekti son kodda yeniden doğrulandı.
- **Üç test artık seçili profilden bağımsız geçiyor.** Dört farklı başlangıç seçimiyle (Kırılma V1, Denge V1, eski prototip,
  profil yok) aynı sonuç.
- **Eski profillere sızıntı yok:** dokuz profil asset'inden yalnız Kırılma V1 başlangıç kilidi veriyor; Denge V1'de çalışma
  anında da hiçbir kilit açık başlamıyor.

## 1. Başlangıç durumu (P0: mevcut durum kaydı)

| Konu | Durum (2 Ekim 2026, 13:20) |
|---|---|
| Son commit | `743b904`; bu pakette commit yok |
| Çalışma alanı | 55 değişmiş dosya + 118 izlenmeyen yol; hepsi önceki bölümlerden, korundu |
| Seçili profil | `Run50_KirilmaV1` (3.6 tesliminde `Run50_DengeV1` idi; 09:47'de sen değiştirmişsin) |
| Oyuncu kaydı | `meta.json` 3.6'daki hash ile aynı (1 Ekim 19:15'ten beri değişmemiş) |
| Unity editörü | 13:20–15:23 arasında kapalıydı; 15:23'te sen açtın. Ben açmadım, dokunmadım; bütün testler izole kopyada çalıştı |
| Kod, 3.6 teslimine göre | `Assets` ve test kaynakları, 3.6'nın son test turunda izole kopyaya alınan hâlle dosya dosya aynıydı. Tek fark seçim dosyası. |
| Üreticiler | `make_denge_v1.py --check` ve `make_kirilma_v1.py --check` temiz; koruma testi 3 / 3 |
| Profiller | 9 profil asset'i; denge seti taşıyan iki profil: Denge V1 ve Kırılma V1 |

**3.6 ölçümüne göre fark.** Oynanış kodu ve veri değişmedi; bunu ölçümle de denetledim. 3.6'nın son parametre ölçümü
(`k36kirilma`, 38 run, aynı seed'ler) bugünkü kodla yeniden çalıştırıldı:

- 38 run'ın **32'si bütün oynanış sütunlarında, bütün round'larda bire bir aynı**. Kazanan run sayısı aynı (33 / 38).
- Elektrik, Hız-alan + Ritim, Dengeli ve Yeni politikalarında hiçbir fark yok.
- Fark yalnız **Artçı Patlama alan 6 patlama yolu run'ında**, ödül alındıktan birkaç round sonra başlıyor: toplam skor ×0,94–×1,13;
  politika medyanı 239.446 → 243.491.
- Aynı kodla ikinci bir çalıştırmada (yalnız ilk on run) **aynı altı run yine ayrıştı** (×0,92–×1,12). Yani bu bir kod farkı
  değil: Artçı Patlama'lı run'lar çalıştırmadan çalıştırmaya oynuyor. Nedenini bulmadım (aramak bu paketin kapsamında değildi).

Sonuç: 3.6 tabanı (`Docs/Bolum3-6/olcum/`) geçerli. Yeni bilgi: **Artçı Patlama'lı tam run'larda tek çalıştırma ±%10 kadar
oynayabiliyor**; 3.6'daki Artçı eş karşılaştırmaları ve P5'teki yeni ölçümler bu payla okunmalı. Dosyalar ve run run tablo:
`Docs/Bolum3-7-1/olcum-tekrari/` (`karsilastirma.md`). Karşılaştırma aracı: `Tools/Balance/KirilmaV1/compare_runs.py`.

**Bekleyen P0 kararları (hiçbiri bu pakette bağlanmadı).**

| Karar | Durum |
|---|---|
| Yeni çalışmanın aday profil / denge seti | Açık |
| Kota dönemleri boss aralıklarını mı izleyecek | Açık (planda öneri var, onay yok) |
| R50 final başarı koşulu ve ödül sırası | Açık |
| Ana run / sınırsız round sürelerinin ilk tablosu | Açık (45/55/60/70 yalnız aday) |
| Ödül aşamalarının havuz politikası | Açık |
| Kazanç/bedel için iki temsilci ödülün sözleşmesi | Açık |

Onaylanmış başlangıç sayıları planın "A. Sabit kararlar" bölümündekilerdir; buraya yenisi eklenmedi.

## 2. Bulunan gerçek hatalar ve kök nedenleri

Hepsi gösterim ya da test hatası; oynanış kuralı değişmedi.

**1) Başlangıç düğümü "tamamlanmış satın alma" gibi çiziliyordu.**
`SkillNodeUI.Refresh`, profilin verdiği düğüm için `isMax = true` atıyordu. Sonuç: satın alınıp bitirilmiş düğümle aynı yeşil.
Seviye gerçekte 0'dı (sahte satın alma yoktu), ama ekranda ayırt edilemiyordu.

**2) Bağlantı çizgileri başlangıç düğümünü kapalı sayıyordu.**
`SkillTreeUI.RefreshConnections` yalnız satın alınmış konumlara bakıyordu. Başlangıç düğümünün seviyesi 0 olduğu için:
- 1×3 Saksı'dan Patlayıcı Kartlar'a giden çizgi **ok** olarak çiziliyordu (alınacak bir şey gibi),
- Patlayıcı Kartlar'dan Çapraz Elektrik'e ve Tornado'ya giden çizgiler **hiç çizilmiyordu**; iki düğüm ağaçta bağlantısız duruyordu.

Bu, 3.6 testinde görülmemişti (test yalnız rengi ve tıklanamamayı denetliyordu).

**3) Tooltip satın alma diliyle yazıyordu.**
Başlık "KADEME AÇIK", altında "SONRAKİ KADEME" ve "YÜKSELTME MALİYETİ: -". Satılmayan bir şey için kademe ve maliyet bölümü vardı.

**4) (Gizli) Karma düğümde yanlış metin.**
Kilidi başlangıçtan açık olup ayrıca stat kademesi satan bir düğüm olsaydı tooltip "Son seviyede … kartlarını havuza ekler"
derdi. Oyun verisinde böyle bir düğüm yok; satın alma tarafı zaten doğruydu (kademeler satılıyor), yalnız metin yanlıştı.

**5) `MechanicsVerification` eski tasarımı bekliyordu.**
Test "vignette opaklığı > 0,8" diyordu. Tasarım 1 Ekim'de değişmişti: renkli köşe vignette'i, 0,65 opak, kart seçiminde altın
sarısı, boss ödülünde mor. Oyun doğru, beklenti eskiydi.

**6) `HarvestBehaviorVerification` ve `SimpleResonanceVerification` profil kurmuyordu.**
Projede o an seçili profille çalışıyorlardı. Beklentileri ortak veriye göre yazılmış (ortak bitki canı, sade nadirlik XP'si,
kapalı başlayan davranış kilitleri). Denge V1 seçiliyken bitki canı ve XP tutmuyor, Kırılma V1 seçiliyken ayrıca elektrik
kartları açık başladığı için daha erken düşüyorlardı.

## 3. Yalnız gösterim sorunu olanlar

**Kasırga ve bumerang.** Gerçek düğüm kimlikleriyle, Kırılma V1'de run başında (test çıktısından):

| Düğüm | guid (ilk 8) | Açtığı kilit | Run başında kilit | Başlangıç erişimi | Satın alma seviyesi |
|---|---|---|---|---|---|
| Patlayıcı Kartlar | `34b548f5` | Patlama kartları | **açık** | evet | 0 / 1 |
| Çapraz Elektrik Kartları | `3075880d` | Elektrik kartları | **açık** | evet | 0 / 1 |
| Tornado Kartları | `6ac2ee49` | Kasırga kartları | kapalı | hayır | 0 / 1 |
| Bumerang Orak Kartları | `56d9411c` | Bumerang kartları | kapalı | hayır | 0 / 1 |
| Çifte Hasat Kartları | `8f656982` | Çifte hasat kartları | kapalı | hayır | 0 / 1 |
| 1×3, 2×2, 2×3 Saksı | `2a73cb51`, `5fd1fac1`, `0ae9f440` | Saksılar | kapalı | hayır | 0 / 1 |

- Sekiz kilit türünün tamamı denetlendi: açık olan yalnız ikisi.
- Sahnedeki kart listesinde kasırga, bumerang ya da çifte hasat kartı havuzda değil.
- Tornado gerçek satın almayla denendi: 30 Iron bir kez düşüyor, kasırga kartları ancak o zaman havuza giriyor.
- Bumerang, Tornado alınana kadar ağaçta görünmüyor ve kilitli.

**Görüntü neden yanılttı.** İki sebep: başlangıç düğümleri satın alınmış yeşiliyle çiziliyordu (hata 1), ve Tornado düğümü,
önündeki ücretli adım kalktığı için 1×3 Saksı alınır alınmaz görünüyor. İkincisi 3.6'nın bilinçli kuralı (yer değişmedi, yalnız
aradaki ücretli adım kalktı); Tornado satılık bir düğüm olarak görünür, açık değildir.

**Eski profiller.**
- Asset düzeyinde: dokuz profilden yalnız `Run50_KirilmaV1` başlangıç kilidi taşıyor.
- Çalışma anında: Kırılma V1 oynandıktan hemen sonra Denge V1 yüklendiğinde hiçbir kilit açık başlamıyor, hiçbir düğüm başlangıç
  erişimi taşımıyor. Patlayıcı Kartlar eski yolla satın alınıyor ve yeşil, etiketsiz görünüyor.

**Kıvılcım (gerileme yok).**
- Saksıya ait şanslar oyuncu taban setinden sorgulanmıyor; bütün testte "taban stat bulunamadı" uyarısı 0.
- `CoreStatsSO`'da kasırga / bumerang / elektrik şansı satırı yok (uydurma değer eklenmemiş).
- Ödül saksının gerçek şansına **kopya başına bir kez** uygulanıyor. 0,20'lik patlama tile'ında:

  | Alınan Kıvılcım | Şans |
  |---|---|
  | 0 | 0,20 |
  | 1 | 0,27 (×1,35) |
  | 2 | 0,3645 |
  | 3 (en çok) | 0,4921 (×1,35³ = ×2,46) |

  **0,20 → 0,492 üç ödülün toplam etkisidir, tek ödülün değil.** Tek ödül 0,20'yi 0,27 yapar.
- Yeniden başlatma: önceki run'da iki Kıvılcım alınmış bırakıldı; yeni run'da şans yine 0,20, kalan modifier yok, yeni alınan
  ödül 0,27 veriyor (önceki kopyaların üstüne binmiyor).

**Boss gösterimi (gerileme yok).** Değişiklik yapılmadı; son kodda yeniden çalıştırılan kontroller:
- HUD rengi boss kimliğinden: Sert Kabuk → Sis → Don sırasıyla önizleme satırı ve aktif kâğıt; her boss'tan sonra kâğıt nötr.
- Temizlik yolu (yeniden başlatma / menü): kâğıt nötr, boss satırı yok, bölge çizgisi gizli. Yeniden başlatılan run'da HUD temiz.
- Bölge: tile merkez pikseli, saksı ve bitki pikselleri çizgi açıkken de aynı; dolgu yok, çevre çizgisi var.
- Hava efekti: `BossWeatherVerification` 22 kontrol (önizleme, aktif türler, round sonu, temizlenmiş yürütücü).

## 4. Yapılan değişiklikler

**Ağaç gösterimi: üç ayrı durum**

| Durum | Yüz rengi | Kademe noktaları | Yazı | Tıklanır mı |
|---|---|---|---|---|
| Başlangıçtan açık (satılacak şeyi yok) | açık mavi `#86C7E8` | yok | "BAŞLANGIÇTAN AÇIK" | hayır |
| Satın alma seviyesi (ör. 1 / 2) | sarı (alınabilir) ya da bej (kaynak yetmiyor) | dolu / boş | yok | kaynak yetiyorsa |
| Tamamlanmış | yeşil | hepsi dolu | yok | hayır |
| Karma (erişim başlangıçtan, stat kademeleri satılık) | satın alma durumuna göre | var | "BAŞLANGIÇTAN AÇIK" (noktaların altında) | kaynak yetiyorsa |

- Başlangıç düğümünün seviyesi artmıyor, kayıt ya da sahte satın alma oluşmuyor, kaynak düşmüyor.
- Ön koşul ve açılma kuralları aynı: başlangıç düğümü yine kendi ön koşulu (1×3 Saksı) sağlanınca görünür; arkasındaki
  düğümlerin sırası değişmedi.
- Bağlantılar: yerine ulaşılmış başlangıç düğümü açık uç sayılır. Ona gelen çizgi düz, ondan satılık düğüme giden çizgi ok.
- Tooltip (başlangıç düğümü): başlık "BAŞLANGIÇTAN AÇIK", bölüm "ERİŞİM", "… kartları başlangıçtan açık: run başından beri seçim
  havuzunda. Bu düğüm satın alınmadı; bu profilde ücret ödenmez." Kademe ve maliyet bölümü yok.

**Etkilenen dosyalar**

| Dosya | Ne değişti |
|---|---|
| `Assets/Scripts/Managers/SkillTreeManager.cs` | `HasStartingAccess`, `IsOpenFromStart`; satılmama kuralı aynı |
| `Assets/Scripts/UI/SkillUIs/SkillNodeUI.cs` | Başlangıç erişimi durumu: renk, yazı, nokta, tooltip metni |
| `Assets/Scripts/UI/SkillUIs/SkillTreeUI.cs` | Bağlantı çizgilerinde açık uç kuralı |
| `Assets/Scripts/UI/Tooltips/TooltipContent.cs` | Başlangıç erişimi tooltip'i |
| `Assets/Scripts/ScriptableObjects/RunProfileSelectionSO.cs` | Testler için oturumluk profil (dosyaya yazılmaz) |
| `Assets/Editor/HarvestBehaviorVerification.cs` | Kendi profilini kurar, her çıkışta temizler |
| `Assets/Editor/SimpleResonanceVerification.cs` | Kendi profilini kurar, her çıkışta temizler |
| `Tools/Verification/Editor/MechanicsVerification.cs` | Vignette kontrolü güncel tasarıma göre |
| `Tools/Verification/Editor/KirilmaV1Verification.cs` | 153 → 189 kontrol |
| `Tools/Verification/Editor/StartConditionRunner.cs` | Yeni: bir testi belirli bir seçimle başlatır |
| `Tools/Verification/Editor/LegacySelectionRunner.cs` | Silindi (3.6'da eklemiştim; artık gereksiz) |
| `Tools/run-isolated-verification.ps1` | Batch Unity varsayılan olarak düşük öncelikle ve son 4 çekirdekte çalışır (`-MaxCores 0 -NormalPriority` kaldırır) |
| `Tools/Balance/KirilmaV1/compare_runs.py` | Yeni: aynı ölçüm setinin iki çalıştırmasını run run karşılaştırır |

Asset, denge verisi, profil, sahne ve prefab değişmedi.

**Test borcu nasıl kapandı**

- *MechanicsVerification.* Eşik gevşetilmedi, oyun da 0,85'e döndürülmedi. Yeni kontroller tasarımı doğruluyor: panel açılınca
  şeffaf başlar (0), 0,65'e çıkar (koddaki tasarım sabitiyle de karşılaştırılır), kart seçiminde altın (`#EBB134`), boss ödülünde
  mor (`#9957E0`), round sonu ekranında kapalı, kart seçimi yeniden açılınca yine 0'dan başlar.
- *HarvestBehavior ve SimpleResonance.* İkisi de `UzunRun130` profilini (ortak veri, denge seti yok) açıkça kurar ve ilk
  kontrolde bunu doğrular. Profil, seçim dosyasına yazılmadan kurulur (`RunProfileSelectionSO.OverrideForSession`); test
  bitince, başarısız da bitse kaldırılır ve seçim dosyasının hash'i baştakiyle karşılaştırılır. Kayıt yalnız bellekte tutulur.
- Bu iki testin hiçbir beklentisi değişmedi.

**Bilerek değişen eski beklentiler (iki tane)**

1. `KirilmaV1Verification`: "verilen düğüm alınmış gibi çizilir" → "başlangıçtan açık olarak çizilir" (bu paketin istediği değişiklik).
2. `MechanicsVerification`: "vignette > 0,8" → güncel tasarım (yukarıda).

## 5. Çalıştırılan testler ve loglar

Hepsi izole kopyada (`Library/VerificationProject`), `Tools/run-isolated-verification.ps1` ile, birer birer, son kodla
(2 Ekim 13:44–14:02). İşlev testi "çalışıyor" der; denge ya da his kanıtı değildir.

| Test | Seçili profille (Kırılma V1) | Denge V1 seçiliyken | Diğer başlangıçlar |
|---|---|---|---|
| KirilmaV1Verification | PASS · 189 | PASS · 189 | — |
| MechanicsVerification | PASS · 88 | PASS · 88 | — |
| HarvestBehaviorVerification | PASS · 114 | PASS · 114 | Prototip10 debug: PASS · 114; profil yok: PASS · 114 |
| SimpleResonanceVerification | PASS · 293 | PASS · 293 | Prototip10 debug: PASS · 293; profil yok: PASS · 293 |

- 3.6'da bu üç eski test düşüyordu; yeni sayılar eski kontrollerin tamamı artı profil ve vignette kontrolleri.
- Mechanics'te kart içerikleri rastgele olduğu için log satırlarındaki sayılar çalıştırmadan çalıştırmaya değişir; kontrol
  sayısı ve sonuç aynı.
- **Başarısız bitişte temizlik:** iki eski test, geçici bir sarmalayıcıyla bilerek düşürüldü (izole kopyada test profiline
  denge seti takıldı). İkisi de FAIL verdi ve sonuç dosyasına "profil kaldırıldı, seçim dosyası değişmedi" yazdı. Geçici
  dosya silindi.

**Etkilenen diğer paketler (bir kez, son kodla)**

| Test | Sonuç |
|---|---|
| DengeV1Verification | PASS · 86 |
| BossWeatherVerification | PASS · 22 |
| BossRewardVerification | PASS · 127 |
| RunPrototypeVerification | PASS · 69 |
| StartLoadoutVerification | PASS · 88 |
| SpecializationVerification | PASS · 100 |
| Run50ReferenceVerification | PASS · 70 |
| RoundPreviewVerification | PASS · 47 |
| ContactRefactorVerification | PASS · 175 |
| ElectricRevertVerification | PASS · 40 |
| OptionsMenuVerification | PASS · 22 satır |

Düşen test yok.

**Ölçüm (denge ölçümü değil, taban denetimi).** Tek set bir kez yeniden çalıştırıldı (`k36kirilma`, 14:03–14:34), ilk on run'ı
bir kez daha (15:24–15:31). Amaç yalnız "3.6 ölçümü bugünkü kodu hâlâ anlatıyor mu" sorusuydu; sonuç 1. bölümde. Başka ölçüm
paketi çalıştırılmadı.

**Bilgisayar yükü.** İşlev testleri ve ilk ölçüm tekrarı kısıtsız çalıştı ve makineni kilitledi. Çalıştırıcıyı bunun üzerine
kıstım (düşük öncelik, 16 çekirdeğin son 4'ü); ikinci tekrar bu hâliyle çalıştı. Bundan sonraki çalıştırmalar varsayılan olarak kısıtlı.

**Log yolları**

| Ne | Nerede |
|---|---|
| Sonuç dosyaları (koşul adıyla) | `Docs/Bolum3-7-1/testler/` (`TopluSonuc.txt` özet) |
| Ekran görüntüleri | `Docs/Bolum3-7-1/ekran/` |
| Ölçüm tekrarı ve karşılaştırma | `Docs/Bolum3-7-1/olcum-tekrari/` (`karsilastirma.md`) |
| Ham Unity logları | `Library/VerificationProject/Logs/*.log` |

Bir testi belirli bir seçimle yeniden çalıştırmak:

```powershell
$env:VERIFY_TARGET = 'HarvestBehaviorVerification'; $env:VERIFY_START_PROFILE = 'Run50_DengeV1'
Tools/run-isolated-verification.ps1 -Method StartConditionRunner.Run -Full
```

## 6. Ekran görüntüleri

Gerçek sahneden, batch'te çizilmiş görüntüler; hepsine baktım. Ağaç görüntülerinde saha gizlendi (aşağıda neden).

| Dosya | Ne gösteriyor |
|---|---|
| `K371_Agac_KirilmaV1.png` | Üç durum bir arada: Patlayıcı ve Çapraz Elektrik "BAŞLANGIÇTAN AÇIK"; Tornado satılık (boş nokta, ok); 1×3 Saksı tamamlanmış (yeşil, dolu nokta); Grid Genişleme I 1 / 2 |
| `K371_Agac_KirilmaV1_Tooltip.png` | Aynı görünüm, üç durumun tooltip'i yan yana |
| `K371_Agac_KirilmaV1_Yakin.png` | 2× yakınlaştırma: yazının okunurluğu |
| `K371_Agac_KirilmaV1_TornadoAlindi.png` | Tornado alındıktan sonra: Tornado yeşil, Bumerang satılık olarak beliriyor |
| `K371_Agac_KarmaDugum_TestVerisi.png` | **Test verisi**: karma düğüm (noktalar + yazı) ve tooltip'i |
| `K371_Agac_DengeV1.png` | Eski profil: Patlayıcı satın alınmış (yeşil), etiket yok; Elektrik ve Tornado satılık |
| `Mechanics_KartSecimi_Vignette.png` | Kart seçiminde altın vignette |

## 7. Doğrulanamayanlar

- **İnsan gözüyle Play onayı yok.** Görüntüler batch'te kameradan çizildi; oyunda arayüz ekranın üstüne çizilir. Bu fark
  yüzünden ağaç görüntülerinde eski test efektleri panelin üstüne çıkıyordu; sahayı yalnız bu görüntüler için gizledim.
- **Tooltip'in imleç altındaki yeri ve taşması** denenmedi: batch'te imleç yok, tooltip'ler ekrana elle kondu. Metin ve
  içerik gerçek prefab ve gerçek doldurma koduyla üretildi.
- **Yazının senin çözünürlüğünde ve varsayılan yakınlıkta okunurluğu** yalnız 1920×1080 görüntüde değerlendirildi (yaklaşık 15 px).
- **Karma düğüm** yalnız test verisiyle denendi; oyunda böyle bir düğüm yok.
- **`HarvestBehaviorVerification`'ın editör menüsünden çalıştırılması** denenmedi (editörü açmadım). O yolda da profil dosyaya
  yazılmaz ve kayıt belleğe alınır; ancak test başlamadan önce kayıt bir kez okunur.
- **Menüye dönüş yolu** oynanmadı; boss HUD ve bölge çizgisi için aynı temizlik çağrısı (ClearAll) testle denendi.
- **Artçı Patlama'lı run'ların çalıştırmadan çalıştırmaya neden oynadığı** bulunmadı (1. bölüm). Diğer bütün run'lar bire bir
  tekrarlanıyor.
- **Kısıtlı çalıştırıcıyla işlev testleri** yeniden çalıştırılmadı; kısıt yalnız süreci yavaşlatır, testlerin içeriğini değiştirmez.
- **Komşuluk kurallı (ön koşulu açık yazılmamış) ağaçta başlangıç kilidi** desteklenmiyor ve denenmedi. Bugün başlangıç kilidi
  veren tek profilin ağacı açık ön koşullu; eski ortak ağaçta başlangıç kilidi yok.

## 8. Senin kararını bekleyen küçük nokta

Başlangıç düğümleri ağaçta ancak 1×3 Saksı alınınca görünüyor (yer açma kuralını korudum). Yani run'ın ilk dakikalarında
patlama / elektrik kartı gelirken ağaçta bunu söyleyen bir şey yok. Run başından itibaren görünmelerini istersen küçük bir
değişiklik; istemediğin sürece dokunmuyorum.

## 9. Kısa Play kontrolü (5–10 dakika)

1. Profil zaten **Run50 Kırılma V1**. Play.
2. Run başında ağacı aç: davranış düğümü görünmemeli. İlk kart seçimlerinde patlama ya da elektrik kartı çıkabildiğini gör.
3. Grid Genişleme I ve 1×3 Saksı'yı al. Patlayıcı Kartlar ve Çapraz Elektrik Kartları açık mavi, altında **BAŞLANGIÇTAN AÇIK**
   yazısıyla, noktasız görünmeli. Tıkla: hiçbir şey olmamalı, kaynak düşmemeli. Üstüne gel: tooltip "BAŞLANGIÇTAN AÇIK / ERİŞİM".
4. 1×3 Saksı'dan Patlayıcı'ya düz çizgi, Patlayıcı'dan Tornado'ya ok olmalı. Tornado sarı, boş noktalı, 30 Iron.
5. Tornado'yu almadan birkaç kart seçimi yap: kasırga kartı gelmemeli. Al: yeşile dönmeli, Bumerang belirmeli.
6. Kart seçiminde köşeler altın, boss ödülünde mor olmalı.
7. Bir boss round'unda HUD rengine ve bölge çizgisine bak (3.6 listesindeki gibi).
8. İstersen **Run50 Denge V1**'e geç: aynı yerde Patlayıcı Kartlar satılık (15 Iron) ve etiketsiz olmalı. Sonra seçimi geri al.

## 10. Ana TODO'da işaretlenenler

- **P0:** yalnız ilk madde ("son kod, profil, test ve üretici durumunu kaydet; 3.6 ölçümüyle farkları listele"). Diğer altı
  madde ve P0 kabul koşulu açık.
- **P1:** altı madde işaretlendi. Kabul koşulu (profil bağımsız regresyonlar, gerekçesiz gevşetme yok, düğüm durumları yeni ve
  eski profilde doğrulandı) otomatik testlerle sağlandı. Play'de gözle onay ayrıca bekliyor (7. ve 9. bölüm).
- P2 ve sonrasına geçilmedi.
