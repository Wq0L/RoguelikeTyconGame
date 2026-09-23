# Uygulanan ekonomi — Eylül 2026

## Hedef ve kapsam

R20'ye kadar bütün saksı türlerine erişim, erken hızlı gelişim, orta bölümde daha pahalı tercihler, R66–100'de üretim/saldırı birleşimi ve ortalama build için yaklaşık R110'da bütün ağacın tamamlanması hedeflendi. R121–130 tamamlanan build'i kullanma dönemi. Hedef roundlar **kilit değildir**; oyuncunun yatırımları ve hasat başarısı erişim zamanını değiştirir.

138 node, 290 kademe vardır. Mevcut 131 node korunmuş; Tornado kilidi ve iki hız rotasına üçer tek kademeli node eklenmiştir. Mevcut skill asset GUID'leri, ikonları, XY yerleşimleri ve sahne referansları korunmuştur. Fiyatların kaynağı `Docs/FinalSkillTree.json`; aday üretimi ve alışveriş simülasyonu `Tools/balance-economy.cjs`.

## Başlangıç, saksı ve grid

Normal başlangıç 80 Gold, 0 Iron, 0 Stone. ResourceManager üzerinde `Use Debug Starting Resources` açılırsa **yalnız Editor Play Mode'da** 80.000'er kaynakla test yapılabilir. Varsayılan kapalı; build normal bütçeyi kullanır.

| Saksı | Kilit açma | Satın alma |
|---|---:|---:|
| 1×1 | Açık | 20 Gold |
| 1×2 | Açık | 40 Gold |
| 1×3 | 40 Gold | 70 Gold |
| 2×2 | 100 Gold | 25 Iron |
| 2×3 | 180 Gold | 20 Stone |

5×5 grid 80 Gold; 7×7 40 Iron; 9×9 120 Stone; 11×11 450 Stone. 2×2 için 1×3 kilidi + 5×5 yeterlidir. 2×3 için 2×2 kilidi + 7×7 gerekir; artık 9×9 şartı yoktur. Patlama kart havuzu 20 Iron, Duplicate kart havuzu 35 Iron ile açılır. Tornado Kartları (P4), Patlayıcı Kartlar'dan sonra 40 Iron ile açılır; dört nadirlik de bu kilide bağlıdır. Bunlar doğrudan davranış bonusu vermez; mevcut kart havuzlarını açar.

## Hız ve güç

| Stat | Başlangıç | Tam skill ağacı, tile/rezonans yok |
|---|---:|---:|
| Üretim aralığı | 5 s | **0,5 s** |
| Saldırı aralığı | 3 s | **0,2 s** |
| Round süresi | 30 s | 90 s |
| Gold çarpanı | ×1 | ×4 |
| Iron çarpanı | ×1 | ×5 |
| Stone çarpanı | ×1 | ×5 |
| XP çarpanı | ×1 | ×3 |
| Nominal doğrudan hasar | 1 | 3.201.552 |

Kullanıcının düzeltmesiyle E2/E7/Z2/Z5'in güçlendirilmiş etkileri geri alındı; özgün kademe değerleri korunur. E2 ve E7 ayrı ayrı ×0,85 ile birlikte 5 → 3,6125 saniye üretim; Z2 ve Z5 ayrı ayrı ×0,7 ile birlikte 3 → 1,47 saniye saldırı aralığı verir. Her node'da yalnız mevcut kademe uygulanır; eski kademeler toplanmaz.

Verimli Üretim - 4'ün devamında **Seri Üretim - 1/2/3** (E12) vardır: 3.989 / 5.265 / 6.541 Iron. Önceki iki üretim rotası tamken aralık 2 → 1 → 0,5 saniye olur. Akıcı Kesim - 4'ün devamındaki **Yıldırım Kesim - 1/2/3** (Z11) 6.136 / 8.100 / 10.064 Gold'dur; önceki iki saldırı rotası tamken 0,8 → 0,4 → 0,2 saniye olur. Yeni node'lar sırayla açılır; bunlar sabit aralık atamaz, mevcut aralığı çarpar. Erken rotanın tamamlanmamış olması veya tile/rezonans, ara sonuçları değiştirebilir.

**0,5 saniye global minimum üretim aralığıdır.** Normal modifier hesabında ve rezonans sonrasında aynı sınır geçerlidir. Üretim sınırına ulaştıktan sonra ilave Fertile hızının marjinal ekonomik etkisi sıfır olabilir. Saldırının mevcut 0,1 saniye güvenlik sınırı korunur; bu ağaç tek başına 0,2 saniyeye ulaşır.

H1/H2/H3/H4 toplam flat hasar katkıları 60/150/1000/4000'e yükseltildi. H5 +2200; diğer hasar çarpanları korunur. Böylece 65 sonrası HP artışı karşısında gelir durmadan güç yatırımları sürdürülebilir. Bitki HP eğrisi değiştirilmedi.

## Kaynak dağılımı ve fiyatlar

Gold erken kilitleri, ilk skill'leri ve geç oyundaki önemli harcamaları taşır. Özellikle Z5 saldırı hızı Gold ile alınır; Iron sıkışması saldırı hızını da engellemez. Iron üretim/orta-geç güç yatırımlarında, Stone grid ve belirli büyük çarpanlarda kullanılır. Son bölümde bütün harcamalar Stone'a yığılmaz.

Temel Iron bitkileri (Carrot/Lettuce/Strawberry) 3 Iron, Pineapple 15 Iron verir. Temel Stone bitkileri (Pepper/Potato/Tomato) 2 Stone, Pumpkin 8 Stone verir. Gold bitkilerinin temel ödülleri değişmedi. Ödüller roundla otomatik çarpılmaz; mevcut skill/tile/rezonans/Duplicate yolu kullanılır. Tam sayıya yuvarlama davranışı da korunur.

Tüm 290 kademe toplamı: **568.065 Gold + 109.830 Iron + 15.636 Stone**. Özgün hız skill'lerine dönülünce oluşan yavaşlamayı karşılamak için orta hız rotalarının bütçeleri ve geç oyun fiyatları yeniden ayarlandı. Son hasar yüzdesi rotası H9, geç oyunda biriken Gold için önemli harcama alanıdır. Tüm fiyatlar tek para birimlidir; oyun içi kaynak dönüşümü eklenmedi.

## Koşu simülasyonu sonucu

30 koşu: 10 seed × dengeli / ekonomi ağırlıklı / hasar ağırlıklı alışveriş tercihi. Başlangıç test parası kullanılmadı. Tile, rezonans, Duplicate, patlama, tornado veya kart skip gelirinden destek alınmadı.

| Tercih | Tam ağaç ortalama roundu | Aralık |
|---|---:|---:|
| Dengeli | 93,0 | 92–95 |
| Ekonomi ağırlıklı | 114,2 | 96–128 |
| Hasar ağırlıklı | 108,5 | 104–113 |
| **Tümü** | **105,2** | **92–128** |

30/30 koşu R130'dan önce tamamlandı. Bütün saksı kilitleri ve her türden ilk örnek en geç R18'de alındı. Sonuçlar `Logs/EconomyBalanceSimulation.json` içinde her roundun geliri, XP'si, bakiyesi, satın alımı ve build statlarıyla saklanır. Bazı ekonomi ağırlıklı tercihler R120 sonrasına kalır; bütün build'lere eşit uzunlukta god mode garanti edilmez.

**Bu, insan oyuncular üzerinde ölçülmüş ortalama veya her build için garanti değildir.** Model 30 FPS, değişken ama kontrollü hedefleme (AreaRadius'tan tahmini kapsama, en çok 12 hedef), tek ortak saldırı sayacı ve yaklaşık 48 hücrelik çiftliğe kademeli yatırım kullanır. Dikdörtgen saksılar alan bütçesine yerleştirilir; gerçek fare rotası, merkezden grid açılmasının yerleşime etkisi ve optimal olmayan hedef kayıpları tam simüle edilmez. Alıcı ilk H1-A kademelerini önceliklendirir, common HP hasarı aştığında hasara döner, istediği yatırım için kaynak biriktirir. Bu temel kararları vermeyen oyuncular daha geç tamamlayabilir. Yatırım tarihleri alıcının tercihi olup runtime kilidi değildir.

Alışveriş sayısı her round eşit değildir: bir roundda birden fazla kademe alınabilir. Önceki “1–2 / 2–3 roundda bir skill” tablosu katı bir satın alma limiti olarak uygulanmadı; 290 kademeyi yaklaşık R110'da bitirmek, özellikle son bölümde toplu satın alma gerektirir. Fiyatlar ana güç sıçramalarını ve tamamlanmayı hedefler; gerçek oyuncu testinde satın alma sıklığı ayrıca izlenmelidir.

## XP dengesi

Hasat Deneyimi I (E6) toplam +%50, Hasat Deneyimi II (E10) ayrıca +%150 verir: tam ağaç **×3 XP**. Bitki başına temel XP korunur; gerçek hasatta önce XP çarpanı, sonra tam sayıya yuvarlama uygulanır. Rounda bağlı gizli XP bonusu yoktur.

Canlı ProgressionSO artık 120 pozitif seviye maliyeti kullanır. `5 × 1,2^(seviye−1)` eski eğri alanları geri dönüş için korunur; `useAuthoredRequirements` açık olduğunda liste kullanılır. Maliyetler 30 koşunun round-by-round birikimli XP medyanına göre, aşağıdaki referanslar arasında yumuşak ve monoton birikimli eğriden çıkarılmıştır. Son checkpoint'in geliri geçmiş roundlara uygulanmaz. Analyzer'daki **Current** bu canlı veriyi okur; **Proposed** ayrı karşılaştırma olarak korunur.

| Round | Hedef / simülasyon P50 seviyesi | P10–P90 |
|---|---:|---:|
| 20 | 25 | 21,7–27,6 |
| 40 | 45 | 40,3–50,0 |
| 65 | 70 | 61,4–73,2 |
| 100 | 100 | 74,8–116,5 |
| 118 | 121 | 105,8–136,9 |

Bunlar aynı örneklem üzerinde kalibrasyon sonuçlarıdır; bağımsız insan playtest'i değildir. Zayıf hasat eden build hedefin gerisinde kalabilir. XP ile alınan tile kartlarının üretime geri beslemesi bu modelde yoktur; Water/rezonans kullanan oyuncu daha erken seviye atlayabilir. 121 sonrası son XP maliyeti tekrar kullanılır; mevcut level-up/kart akışı devam eder. Ayrı **post-level stat kartı sistemi bu değişikliğin parçası değildir**.

## Doğrulama ve yeniden kullanım

- `node Tools/balance-economy.cjs`: yalnız aday/alışveriş simülasyonu ve Logs raporu.
- `node Tools/balance-economy.cjs --verify-assets`: asset maliyet/etki/önkoşullarını da kontrol eder.
- `node Tools/balance-economy.cjs --apply`: **açıkça istendiğinde** manifest, ilgili skill alanları, saksı fiyatları ve bitki ödüllerini yazar. Otomatik çalışmaz; bütün örnek koşular tamamlanmıyorsa veya saksı kilitleri R20'yi aşıyorsa yazmayı reddeder.
- `node Tools/calibrate-xp.cjs`: birikimli XP raporundan aday eğri/quantile raporu; `--apply` açıkça canlı eğriyi yazar, `--verify-assets` karşılaştırır.
- `node Tools/verify-final-tree-scene.cjs`: 138 sahne instance'ı ve yerleşim/referans kontrolü.
- `Tools > Economy Analyzer Verification`: gerçek Unity hesaplayıcılarıyla yuvarlama, tier replacement, hız sınırları, rezonans A/B ve yaşam döngüsü regresyonları.

Node bütçe denemesi hızlı, double tabanlı bir modeldir; Unity'nin float ayrıntılarıyla bit düzeyinde aynı değildir. Unity Analyzer tek-saksı simülasyonu gerçek float stat/ödül işlemlerini kullanır. İkisinin kapsamı farklıdır; ekonomi sonuçları için belirtilen varsayımlar geçerlidir.
