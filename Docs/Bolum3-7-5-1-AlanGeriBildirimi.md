# Bölüm 3.7.5.1 — Artçı alan geri bildirimi ve Hasat Ritmi doğrulaması

**Durum (2026-10-03):** P5'in kalan iki işi yapıldı ve izole kopyada test edildi. Artçı Patlama'nın ikinci darbesi artık
vurduğu gerçek alanın sınırını kısa bir dalgayla gösteriyor. Hasat Ritmi'nin sayacı ve hakkı kart açıklamasıyla tutarlı
çıktı; oynanış hatası bulunmadı, kod değişmedi. Denge, profil ve oynanış değerleri değişmedi. Görüntüler batch'te gerçek
oyun render'ından alındı ve tek tek incelendi; **bu insan Play onayı değildir.** Commit atılmadı; seçili profile, oyuncu
kaydına ve açık editöre dokunulmadı.

Önceki paket: [Bolum3-7-5-KirilmaOdulleriErisim.md](Bolum3-7-5-KirilmaOdulleriErisim.md) · Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md)

## Kısa sonuç

| Konu | Sonuç |
|---|---|
| Artçı alanı | İkinci darbenin karesinde, vurulan hücrelerin dış sınırı kor kırmızısı ince bir çizgiyle belirir: 0,15 sn'de saksıdan sınıra yayılır, 0,32 sn'den sonra söner, 0,6 sn'de kalkar. Dolgu yok |
| Çizginin kaynağı | O darbenin oyun geometrisinden gelen hedef hücreleri + saksının ayak izi. Ayrı bir alan formülü yok; yarıçap profile göre ne ise o (eski 1,50 → 9 hücre, yeni 2,00 → 13 hücre) |
| Hasar | Değişmedi. Yoğun tarla ölçümünde 24 round'un 24'ünde hasat, skor ve yankı sayıları 3.7.5'tekiyle birebir aynı |
| Performans | Kare maliyeti farkı ölçüm gürültüsü içinde; GC yok; darbe başına ayırma 0 bayt; çizgi başına ~14 µs |
| Hasat Ritmi sayacı | Kodla kart açıklaması tutarlı. Davranış ve artçı hasadı sayılmıyor, ölen bitki iki kez sayılmıyor, eşik saldırının ortasında aşılsa da o saldırı güçlenmiyor, hak saldırı başına bir kez harcanıyor. **Hata yok, kod değişmedi** |
| Hasat Ritmi alanı | Mevcut gösterim yeterli: hak hazırken imleç halkası altın renkte ve güçlü saldırının gerçek yarıçapında (×1,6). Harcanınca normale dönüyor. Yeni sistem kurulmadı |
| Testler | Yeni test 23 kontrol; hedefli regresyon turunun 12 işinin 12'si geçti. Eski profillerin teklif dağılımı 3.7.3'tekiyle bayt bayt aynı |

## 1. Bulunan gerçek eksikler

1. **Artçı alanı görünmüyordu.** İkinci darbe merkezde ilk patlamayla aynı patlama görselini oynatıyor, vurduğu her bitkide de
   küçük bir patlama gösteriyordu. Alanın sınırı hiçbir yerde yoktu: 2,00 hücrelik genişleme yalnız patlayan bitkilerin
   dağılımından tahmin edilebiliyordu.
2. **İlk sürümümdeki iki okunurluk sorunu (görüntülerde görüldü, düzeltildi):**
   - Çizgi, patlama bulutlarının altında çiziliyordu. Darbe anında alanın üst ve sağ kenarları bulutların arkasında
     kayboluyordu. Çizim sırası patlama parçacıklarının (saydam, 3000) arkasına alındı (3100); imleç halkası da böyle en
     üstte çiziliyor.
   - İzometrik görünümde alanın kameraya uzak kenarları saksı gövdelerinin arkasında kalıyor. Boss bölgesindeki gibi saksı
     arkasında kalan kısmı soluk çizen ikinci kopya var; opaklığı 0,45'te uzak kenarlar zor seçiliyordu, 0,70'e çıkarıldı.
     Ayrıca süre 0,45 → 0,6 sn, kalınlık 0,16 → 0,20 birim oldu (boss bölgesi çizgisi 0,22).
3. **Test görüntülerinde HUD bir önceki vakadan kalıyordu** (HUD saniyede 10 kez yenilenir, batch'te kareler çok hızlı akar).
   Oyun hatası değil; test görüntü almadan önce HUD'u yeniliyor.
4. **Hasat Ritmi'nde hata bulunmadı** (3. bölüm).

## 2. Artçı alan geri bildirimi

### Görselin kullandığı oyun verisi

`BehaviorEchoes.Explode` (ikinci darbenin uygulandığı yer) hedef hücrelerini oyun geometrisinden alır
(`HarvestBehaviorGeometry.ExplosionCells(ayak izi, yarıçap)`), sonra hasarı uygular. Görsel bu iki adımın arasında, aynı karede
çağrılır ve aynı listeyi okur:

| Görselin verisi | Kaynağı |
|---|---|
| Bölge | Saksının ayak izi (işin tuttuğu hücreler) + o darbenin hedef hücreleri |
| Tarla sınırı | Tarlanın dışındaki ya da kilitli hücre bölgeye girmez (yankının "eriştiği hücre" sayacıyla aynı kural) |
| Yarıçap | İşin yarıçapı: taban 1,2 hücre × ödülün verideki çarpanı. Eski profilde ×1,25 (1,50), yeni profilde ×1,667 (2,00). Görselde sabit değer yok |
| Yayılmanın merkezi | Ayak izindeki hücrelerin ortası |
| Zaman | Darbenin uygulandığı kare. İlk patlamada çizgi yok |

Çizgi, bölgenin hücre kenarlarını izler: tek hücrelik saksıda 2,00 hücre artı biçiminde 13 hücredir, 2×3 saksıda köşeleri
basamaklı 30 hücrelik bir bölgedir. Daire çizilmez; çok hücreli saksıda yanlış erişim vaat edilmez.

### Görünüm

- **Şekil:** dolgusuz çevre çizgisi; ortası kor kırmızısı, iki yanında koyu mürekkep (her tile renginde okunur). Boss bölgesi
  çizgisiyle aynı shader (`FrostZoneMarker`); kenar şeridi kodu artık ikisinde ortak (`CellOutlineMesh`), boss bölgesinin
  çıktısı değişmedi.
- **Renk:** kor kırmızısı. Sert Kabuk'un amber bölge çizgisinden, Don'un mavi çizgisinden ve Hasat Ritmi'nin altın imlecinden
  ayrı okunuyor (görüntülerde yan yana).
- **Zaman çizelgesi:** 0 sn'de bölgenin %55'i büyüklüğünde başlar, 0,15 sn'de sınıra ulaşır, 0,32 sn'ye kadar tam opak kalır,
  0,6 sn'de söner ve kalkar. Patlama bulutları ~0,3 sn sürdüğü için çizgi bulutlar dağıldıktan sonra da kısa süre okunur.
- **Değişmeyenler:** ilk patlamanın görseli ve alanı; ikinci darbenin merkezdeki patlaması; vurulan bitkilerdeki küçük
  patlamalar, isabet parlaması ve parçacıkları.

### Havuz ve temizlik

- 8 dalgalık havuz, sahnede ilk Artçı'da bir kez kurulur (8 ağ, 16 çizici, 2 malzeme). Darbe başına nesne, malzeme ya da liste
  üretilmez; ölçülen ayırma 0 bayt.
- Havuz doluysa dalga çizilmez ve sayılır; hasar ve hedefler etkilenmez (testte 10 artçı aynı karede: 8 çizildi, 2 atlandı,
  108 vuruşun hepsi uygulandı).
- Round bittiğinde, oyun durumu Round'dan çıktığında (menü, ödül ekranı) bütün dalgalar o kare kalkar. Sahne değişince havuz
  sahneyle birlikte yok olur; yeni sahnede iz kalmaz.

## 3. Hasat Ritmi: sayaç ve hak kuralları

Kod (`HarvestRhythm`, `PlayerController.AttackInRadius`) ve kart açıklaması ("her 5 doğrudan hasatta bir: hasar ×1,75 ·
yarıçap ×1,6") karşılaştırıldı; aşağıdaki kurallar testte gerçek saldırı yoluyla denendi.

| Soru | Gerçek davranış | Kanıt |
|---|---|---|
| Normal doğrudan hasat sayılıyor mu? | Evet. Normal saldırının doğrudan vuruşuyla ölen her bitki 1 sayılır; sayım saldırı bitince bir kez yapılır | Test |
| Davranış hasadı sayılıyor mu? | Hayır. Saldırı A'yı öldürür, A'nın patlaması aynı saldırının alanındaki B'yi hasat eder: sayaç +1 | Test |
| Artçı hasadı sayılıyor mu? | Hayır. Artçının 0,2 sn sonra hasat ettiği 4 bitki sayacı değiştirmedi | Test |
| Aynı bitki iki kez sayılıyor mu? | Hayır. Ölen bitki öldüğü anda hücresinden silinir; saldırının kalan hedefleri onu bulamaz (B'ye yalnız patlama vurdu) | Test |
| Sıra fark ediyor mu? | Evet, bitkinin gerçekten nasıl öldüğüne göre: düz bitki patlayan saksıdan önce vurulursa ikisi de doğrudan hasattır (+2) | Test |
| Eşik saldırının ortasında aşılınca kalan hedefler güçleniyor mu? | Hayır. Güçlendirme saldırı başında bir kez belirlenir. Sayaç 4/5 iken üç bitkilik saldırının üçü de normal hasar ve normal yarıçap aldı | Test |
| Güçlendirme bütün hedeflere tutarlı mı? | Evet. Üç hedefin üçü güçlü hasar aralığında, yarıçap ×1,6 | Test |
| Hak her hedefte ayrı mı harcanıyor? | Hayır. Saldırı başına bir kez | Test |
| Yeni run, yeniden başlatma, sahne değişimi | Run başında (`RunPower.Reset`) sayaç, hak ve ölçüm sayaçları sıfırlanır; ödül etkisi kalkar | Bu test + KirilmaV1Verification |
| Menü | Ödül etkisi kalkar (`BossRewardManager.ClearAll`): sayaç çalışmaz, saldırı nötrdür, HUD satırı görünmez. Sayaç değeri bir sonraki run başında sıfırlanır | Kod |

**Bilerek değiştirilmeyen üç davranış:**

- **Eşik aşımı:** eşik dolunca tek hak hazırlanır, sayaç 0'a döner, fazlası atılır (4 + 3 → hak hazır, sayaç 0; 2 hasat
  kaybolur). Hak hazırken hasatlar sayılmaz, hak yığılmaz.
- **Boş savuruş:** canlı bitkiye değmeyen saldırı hakkı harcamaz. Hak, canlı bitkiye değen ilk saldırıda harcanır.
- **Round geçişi:** sayaç round'lar arasında korunur (KirilmaV1Verification'da test edildi). Hazır hakkı round geçişinde
  temizleyen bir yol yok, hak da korunur; bu pakette ayrıca denenmedi.

Kart açıklaması bunlarla çelişmiyor, yalnız ayrıntıları söylemiyor (hasatların neden sayılmadığı, fazlanın atılması).

## 4. Hasat Ritmi: alanın okunurluğu

Mevcut gösterim yeterli bulundu; yeni sistem kurulmadı, kod değişmedi.

| An | Gösterim | Görüntü |
|---|---|---|
| Normal | İmleç halkası beyaz, saldırının gerçek temas yarıçapında (testte 1,5) | `T3751_10_Ritim_normal` |
| Hak hazır | Halka altın renkli ve güçlü saldırının gerçek yarıçapında (2,4 = 1,5 × 1,6); imlecin rüzgâr izleri altın; HUD'da "HASAT RİTMİ HAZIR · sıradaki vuruş güçlü" | `T3751_11_Ritim_hazir` |
| Güçlü vuruş | Büyük hasar sayıları ve parçacıklar halkanın büyük alanındaki bütün bitkilerde; imleç halkası beyaza dönmüş | `T3751_12_Ritim_guclu_vurus` |
| Hak harcandı | Halka hemen beyaz ve normal yarıçapa döner | `T3751_13_Ritim_normale_donus` |

Halkalar `PlayerController`'ın kendi geometrisinden (`HarvestReach(AreaRadius × çarpan)`) çiziliyor; testte halkanın ölçülen
yarıçapı normal ve hazır durumda beklenen değerle aynı. Normal ve güçlü alan renk ve boyutla ayrışıyor.

Görülen bir sınır: vuruş anındaki kısa halka (`VFXManager.PlayAttackRing`, her saldırıda oynar) güçlü vuruş görüntüsünde
imleç halkasından ayırt edilemedi. Bu halka bu paketten önce de vardı; incelemedim ve değiştirmedim.

## 5. İncelenen görüntüler

Hepsi izole kopyada, gerçek oyun render'ından (HUD dahil), sabit 1/30 sn adımla; kareler oyun döngüsünün içinde
(`LateUpdate`) alındı. Her görüntünün yanında 2 kat büyütülmüş bir kesiti (`_yakin`) var. Klasör: `Docs/Bolum3-7-5-1/ekran/`.

| Görüntü | Ne gösterir | Bakınca görülen |
|---|---|---|
| `T3751_01_Dizi_0` … `_8` | Yeni 2,00, tarla ortası, tek hücrelik saksı: vuruştan önce → ilk darbe (+1 kare) → bekleme → ikinci darbe (0) → yayılma (+2) → sınır (+5) → bulut sonrası (+9) → sönme (+14) → temizlik (+19) | İlk darbede çizgi yok. İkinci darbede çizgi merkezde küçük başlar, 5 karede sınıra ulaşır, bulutların üstünde okunur; bulutlar dağıldıktan sonra artı biçimli 13 hücre net. +19'da iz yok |
| `T3751_02_Eski150_orta_1x1` | Eski Artçı, tek hücre | 3×3 kare bölge; yeni bölgeden belirgin küçük |
| `T3751_03_Yeni200_orta_1x1_sonra` / `_once` | Aynı kare, çizgili / çizgisiz | Çizgisiz hâlde alan yalnız patlayan bitkilerden tahmin ediliyor; çizgili hâlde sınır net |
| `T3751_04_Eski150_orta_2x3` | Eski Artçı, çok hücreli saksı | 4×5 dikdörtgen |
| `T3751_05_Yeni200_orta_2x3_sonra` / `_once` | Yeni, çok hücreli saksı | Köşeleri basamaklı 30 hücre; daire değil, hücre kenarlarını izliyor |
| `T3751_06_Yeni200_kenar_1x1` | Tarla kenarı | Çizgi tarlanın kenarında kesiliyor; dışarı taşmıyor |
| `T3751_07_Yeni200_kose_2x2` | Tarla köşesi, ekranın alt kenarına yakın | Bozulma yok; tarla sınırını izliyor |
| `T3751_08_Don_dolu_tarla` | Don Cephesi aktif: kar efekti + mavi bölge çizgisi, 121 hücrelik dolu tarla | Kar çizgiyi kapatmıyor; kırmızı çizgi mavi bölgeden ayrı |
| `T3751_09_SertKabuk_dolu_tarla` | Sert Kabuk aktif: kum efekti + amber bölge çizgisi | Kırmızı ve amber çizgi kesiştikleri yerde de ayrı okunuyor |
| `T3751_10` … `_13` | Hasat Ritmi (4. bölüm) | Normal ve hazır halka renk ve boyutla ayrışıyor |

Görüntülere bakarak verdiğim cevaplar:

- **Alan anlaşılır mı?** Evet, bulutlar dağıldıktan sonra (~0,3 sn) net; darbe anında da çizgi bulutların üstünde.
- **Bitki ve tile örtülüyor mu?** Hayır; dolgu yok, çizgi hücrenin kenarında, 0,20 birim. Saksı arkasında kalan kısım %70
  opaklıkta bitkinin üstünden geçiyor; ince ve 0,6 sn.
- **Gerçek erişim dışında hasar vaat ediyor mu?** Hayır. Testte çizginin her köşe noktası bölge hücrelerinin içinde; bölge,
  gerçekten vurulan hücreler ile saksının kendi hücreleridir. Saksının kendi hücreleri bölgenin içinde kalır ama vurulmaz
  (saksının kendisidir).
- **Kenarda ya da izometrik görünümde bozuluyor mu?** Hayır; uzak kenarlar ilk sürümde zayıftı, düzeltildi.

## 6. Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Assets/Scripts/Efects/AftershockAreaFeedback.cs` | **Yeni.** Alan geri bildirimi: havuz, bölge, yayılma, temizlik, sayaçlar (çizilen / atlanan) |
| `Assets/Scripts/Efects/CellOutlineMesh.cs` | **Yeni.** Hücre kümesi çevre çizgisinin ortak şerit geometrisi |
| `Assets/Scripts/UI/FrostZoneMarkers.cs` | Kenar şeridi ortak yardımcıya taşındı; aynı köşe sırası ve değerler (çıktı aynı) |
| `Assets/Scripts/Managers/BehaviorEchoes.cs` | `Explode` içinde tek satır: hedef hücreleri hesaplandıktan sonra görsel çağrılır |
| `Tools/Verification/Editor/AlanGeriBildirimiVerification.cs` | **Yeni** işlev ve görüntü testi |
| `Tools/Verification/Editor/KirilmaErisimMeasurement.cs` | Laboratuvar çıktısına iki sütun: `areaPlayed`, `areaSkipped` |
| `Docs/Bolum3-7-5-KirilmaOdulleriErisim.md` | Kısa sonuç oranları netleştirildi (aşağıda); Çifte Akım havuz kararı yazıldı |
| `Docs/TODO-Run50-Endless-20261002.md` | P5 Hasat Ritmi maddesi ve durum paragrafı |

Hasar, gecikme, tetik, Hasat Ritmi, XP, ekonomi, kota, profil ve ödül asset'leri değişmedi; üreticiler çalıştırılmadı.
Yeni `.cs` dosyalarının `.meta` dosyalarını Unity açılışta üretir.

**3.7.5 raporunda netleştirilen:** ×1,35 Artçı kazancı run toplamı değildir. Ödülü almayıp aynı tekliften başka ödül alan eşe
göre, ödülün alındığı round'dan sonraki karşılaştırılabilir round'ların toplam hasadının seed medyanıdır. Rapordaki kısa sonuç
artık bu ayrımı tabloyla gösteriyor.

## 7. Testler ve performans

### Yeni test: `AlanGeriBildirimiVerification` — PASS, 23 kontrol

| Konu | Kontrol |
|---|---|
| Alan = gerçek darbe | 8 vaka (yeni 2,00: orta / kenar / köşe 1×1, orta / kenar 2×2, orta 2×3; eski 1,50: orta 1×1 ve 2×3). İlk patlamada çizgi yok; çizgi ikinci darbenin karesinde. Bölge = ayak izi + testin kendi geometri hesabıyla bulunan hedef hücreleri (tarla kenarında kesilmiş). Kenar sayısı bağımsız hesapla aynı. Çizginin bütün köşe noktaları bölge hücrelerinde. Hasar aynı (her hücreye bir kez, 100) |
| Eski ve yeni yarıçap | Eski asset 1,50 → 9 hücre, yeni varyant 2,00 → 13 hücre |
| Havuz dolu | 10 artçı aynı karede: 8 çizildi, 2 çizilmedi, 108 vuruşun hepsi uygulandı |
| Ayırma ve maliyet | 500 çizgi (2×3 saksı, 30 hücre): 0 bayt, çizgi başına 14,4 µs |
| Görüntüler | 19 görüntü (dizi 9, karşılaştırma 6, aynı karenin çizgisiz hâli 2, boss havası 2) |
| Hasat Ritmi | 6 kontrol (3. bölüm) + imleç halkası: normal 1,5, hazır 2,4 altın, harcanınca 1,5 |
| Temizlik | Round biterken ekrandaki çizgi o kare kalkar; sahne değişince nesne kalmaz, ritim sıfır |
| Kapanış | Testte hata ya da istisna kaydı yok |

### Hedefli regresyon turu

Gameplay değişmediği için tam tur yerine ilgili testler çalıştırıldı (izole kopya, düşük öncelik, 08:22–08:33, son kodla).

| İş | Neden seçildi | Sonuç |
|---|---|---|
| AlanGeriBildirimiVerification | Yeni test, son kodla | PASS · 23 |
| KirilmaErisimiV1Verification | Artçı ve ikinci dalga hasarı, havuz, katsayılar (Adanış + uzmanlaşma), round sonu, eski profil erişimi | PASS · 35 |
| BedelliOdullerV1Verification | Bedelli ödül katsayıları | PASS · 93 |
| KirilmaV1Verification | Hasat Ritmi'nin önceki testleri (round geçişi, yeniden başlatma), boss bölge çizgisi | PASS · 189 |
| TakvimV1Verification | Boss bölge çizgisi (ortak koda taşındı) | PASS · 162 |
| BossRewardVerification | Ödül akışı, bölge çizgisi | PASS · 127 |
| RunPrototypeVerification | Bölge çizgisi | PASS · 69 |
| HarvestBehaviorVerification | Davranış görsel havuzları | PASS · 114 |
| ElectricRevertVerification | `BehaviorEchoes` değişti | PASS · 40 |
| SpecializationVerification | Uzmanlaşma katsayıları | PASS · 100 |
| MechanicsVerification | Görsel his kuralları | PASS · 88 |
| Ödül teklif dağılımı | Eski profiller değişmedi mi | md5 `222d6374…`, 3.7.3 ile aynı |

Boss bölge çizgisinin piksel kontrolleri de geçti (tile ve saksının kendi rengi değişmiyor; dolu tarlada çizgi 60 / 60 örnekte
okunuyor). Başarısız test yok; bilerek değiştirilen eski beklenti yok.

**Geliştirme sırasında:** yeni test ilk denemede geçti. Görüntülere bakınca efektte iki ayar yapıldı (1. bölüm, madde 2);
her ayardan sonra test yeniden çalıştırıldı. Ara görüntüler: `Docs/Bolum3-7-5-1/gelistirme/ikinci/` (bulutların üstünde, arka
kopya 0,45). İlk sürümden (bulutların altında, 0,45 sn) yalnız iki görüntü kaldı: `gelistirme/ilk/` (sönme ve temizlik
kareleri); diğerlerini ayarlamadan önce sildim.

Ham kayıtlar: `Docs/Bolum3-7-5-1/testler/` (her işin sonucu, `TopluSonuc.txt`, testlerin görüntüleri).

### Yoğun tarla performansı (3.7.5 ile aynı kapsam)

`KirilmaErisimMeasurement.RunPerformance`: R40, 9×9, bütün saksılarda davranış (güçlü sinerji), aynı seed üç kez. Batch'te
ekrana çizim yapılmaz; ölçülen, oyun kodunun kare maliyetidir (çizginin GPU maliyeti dahil değil). İki ölçüm ayrı
oturumlardır; makine yükü farklı olabilir, bu yüzden her oturumda ödülsüz kola göre fark da verildi.

| Düzen | Kol | 3.7.5 (çizgi yok) ms/kare | 3.7.5.1 (çizgi var) ms/kare | Ödülsüze göre fark 3.7.5 → 3.7.5.1 | En uzun kare (ms) | GC toplaması | Çizilen / atlanan dalga |
|---|---|---|---|---|---|---|---|
| Patlama sinerjisi | Ödülsüz | 0,613 | 0,598 | – | 28,0 / 27,6 | 0 / 0 | – |
| | Artçı 1,50 (eski) | 0,686 | 0,671 | +0,073 → +0,073 | 10,7 / 10,6 | 0 / 0 | 252 / 0 |
| | **Artçı 2,00 (seçilen)** | 0,720 | 0,707 | +0,107 → +0,109 | 23,9 / 23,2 | 0 / 0 | 270 / 0 |
| | Artçı 2,25 | 0,752 | 0,708 | +0,139 → +0,110 | 21,5 / 21,6 | 0 / 0 | 249 / 0 |
| Elektrik sinerjisi | Ödülsüz | 0,511 | 0,493 | – | 4,8 / 4,9 | 0 / 0 | – |
| | Çifte Akım 4 (seçilen) | 0,538 | 0,522 | +0,027 → +0,029 | 3,5 / 3,3 | 0 / 0 | – |

- Çizginin kare maliyeti ölçüm gürültüsünün içinde (seçilen Artçı'da ödülsüze göre fark +0,107 → +0,109 ms).
- **Oynanış aynı:** 24 round'un 24'ünde hasat, skor, vuruş, yankı ve atlanan görsel sütunları 3.7.5'tekiyle birebir aynı. Görsel
  zar akışını kullanmıyor ve oynanışa dokunmuyor.
- **Dalga atlanmadı:** her artçı çizildi (seçilen kolda 270 / 270); 8 dalgalık havuz bu yoğunlukta hiç dolmadı.
- **Bellek:** Artçı'lı round'larda yönetilen bellek artışı round başına ~20 KB fazla. Bu, sahnede ilk artçıda bir kez kurulan
  havuz (8 ağ, 16 çizici, listeler); darbe başına ayırma 0 (testte ölçüldü). GC toplaması yok.

Ham veri: `Docs/Bolum3-7-5-1/olcum/KirilmaErisim_performans.csv` (önceki: `Docs/Bolum3-7-5/olcum/ham/`).

## 8. Kalan sınırlamalar

- **İnsan Play onayı yok.** Görüntüler batch render'ıdır; 60 kare/sn'de gerçek oyun hissine bakılmadı.
- **Çizginin GPU maliyeti ölçülmedi** (batch'te çizim yok). Çizgi başına 2 çizici, en çok 8 dalga.
- **Darbe anında (ilk ~0,15 sn) çizgi patlama bulutlarının ortasında küçük;** alanı asıl okutan, sınıra ulaştıktan sonraki
  0,2–0,4 sn.
- **Saksının kendi hücreleri bölgenin içinde** gösteriliyor ama vurulmuyor (saksının kendisi). Ayrı bir iç çizgi çizilmedi.
- **Hazır Hasat Ritmi hakkı round geçişinde korunuyor** (kodda temizleyen yol yok); bu pakette ayrıca test edilmedi. Sayacın
  korunması test edilmiş durumda.
- **Vuruş anındaki kısa halka** (`PlayAttackRing`) güçlü vuruşta ayırt edilmedi; mevcut davranış, değiştirilmedi.
- **Çifte Akım** ×1,5 hedefini tutturmadı; kullanıcı kararıyla havuzda kalıyor. İkinci dalganın görseli (şimşekler) bu pakette
  değişmedi.

## 9. Beş dakikalık Play kontrolü

1. Menü: **Tools → Run Profili → Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)**. Play.
2. Patlama kartlarına oyna. R23'ten sonra Artçı Patlama gelirse al (garanti değil).
3. Patlama tetiklendiğinde: ilk patlamada çizgi olmamalı; 0,2 sn sonra kırmızı bir çizgi saksıdan dışarı yayılıp vurulan
   alanın sınırında durmalı, yarım saniyede kaybolmalı. Tek hücrelik saksıda artı biçimi, büyük saksıda köşeleri basamaklı bölge.
4. Bakılacaklar: çizgi fark ediliyor mu; ekran kalabalıklaşıyor mu (çok patlayan saksıda aynı anda birkaç çizgi); bitkiler
   ve tile renkleri okunuyor mu; Sert Kabuk'un turuncu şeridiyle karışıyor mu?
5. Hasat Ritmi alırsan: sayaç HUD'da "n / 5"; hak dolunca imleç halkası altın renkte büyümeli, vurunca normale dönmeli.
6. Eski hâlle karşılaştırmak için: `Run50 Kırılma V1` (eski 1,50 hücre; çizgi orada da var, 3×3 kare).

Seçili profilini ben değiştirmedim.
