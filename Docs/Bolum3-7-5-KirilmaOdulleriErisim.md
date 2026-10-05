# Bölüm 3.7.5 — Kırılma ödüllerinin gerçek hasat katkısı

**Durum (2026-10-03):** P5 uygulandı ve izole kopyada ölçüldü. Yeni aday profil `Run50_KirilmaErisimiV1`: Artçı Patlama'nın
ikinci darbesi 2,00 hücre, Çifte Akım'ın ikinci dalgası 4 hücre. Hasar, gecikme ve tetik kuralları değişmedi. İnsan Play
kontrolü yapılmadı; "kırılma hissi çözüldü" demiyorum. Commit atılmadı; seçili profile ve oyuncu kaydına dokunmadım.

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md) · Önceki paket: [Bolum3-7-4-BedelliBossOdulleri.md](Bolum3-7-4-BedelliBossOdulleri.md)

## Kısa sonuç

- **Ölçüm oynaklığının kök nedeni bulundu ve düzeltildi.** Yankının gecikmesi mutlak oyun zamanıyla karşılaştırılıyordu; o
  değerin kayan nokta yuvarlaması yüzünden Artçı bazen 6, bazen 7 kare bekliyordu. Hangisi olacağı oturumun o anki zamanına
  bağlıydı. Düzeltmeden sonra aynı koşu 5 / 5 aynı çıkıyor; 3.7.1'de oynayan tam run'lar iki ayrı çalıştırmada bayt bayt aynı.
- **Artçı Patlama: 2,00 hücre seçildi.** Güçlü patlama sinerjisinde ödülsüz kola göre medyan hasat ×1,63 / ×1,79 / ×1,62
  (R23 / R30 / R40). ×1,5 hedefi üç round'da da tuttu. 2,25 hücre de tuttu; kural gereği küçük olan seçildi.
- **Çifte Akım: 4 hücre teslim edildi, hedef tutmadı.** En iyi aday ×1,30 / ×1,33 / ×1,34. Hasar, tetik ya da gecikme
  yükseltilmedi. Havuzda kalma kuralını sağladığı için yeni profilin havuzunda duruyor.
- **Mevcut değerler raporla aynı:** eski Artçı gerçekten 1,50 hücre (8 hücre); eski ikinci dalga ilk dalgayla aynı 8 hücre.
- **Hasat Ritmi'ne ve bedelli ödüllere dokunulmadı.** Eski profillerin ödülleri eski erişimde.
- **Performans:** yoğun tarlada kare maliyeti 0,69 → 0,72 ms (Artçı), GC yok, atlanan hasar yok.
- **Birikimli run'da (60 tam run; laboratuvardan ayrı ölçüm):** Artçı 2,00 hücre, ödülü alan 9 seed'in 9'unda eski Artçı'dan
  ve ödülü almayıp aynı tekliften başka ödül alan eşinden fazla hasat verdi. Eski Artçı, yerine alınan ödülle aynı düzeydeydi.
- **Çifte Akım birikimli run'da kırılma değil:** 4 hücre eskisinden biraz iyi, ama yerine alınabilecek başka bir ödülle aynı
  düzeyde. **Kullanıcı kararı (3 Ekim, Bölüm 3.7.5.1):** yeni profilin havuzunda kalır; kırılma hedefini tutturmuş sayılmaz.

Oranların neyi karşılaştırdığı:

| Ölçüm | Artçı 2,00 hücre | Çifte Akım 4 hücre | Neye göre, hangi round'lar |
|---|---|---|---|
| Laboratuvar (sabit stat, tek round) | ×1,63 / ×1,79 / ×1,62 | ×1,30 / ×1,33 / ×1,34 | Aynı seed'in **ödülsüz** kolu; R23 / R30 / R40 round'unun toplam hasadı; 10 seed'in medyanı |
| Birikimli run · başka ödüle göre | ×1,35 (9 / 9 seed artış) | ×1,04 (4 / 6) | Ödülü almayıp aynı tekliften başka ödül alan eş; **ödülün alındığı round'dan sonraki karşılaştırılabilir round'ların** toplam hasadı; seed'lerin medyanı |
| Birikimli run · eski ödüle göre | ×1,31 (9 / 9) | ×1,09 (5 / 6) | Eski erişimli aynı ödül; aynı round'lar |
| Birikimli run · eski ödül başka ödüle göre | ×1,05 (5 / 9) | ×0,95 (2 / 6) | Aynı round'lar |

Birikimli run oranları **run toplamı değildir**: yalnız ödül alındıktan sonraki round'lar sayılır (seed'e göre R24–R50,
R31–R50, R47–R50 …). Almayan eş bir kırılma ödülünü mecburen aldıysa ya da erişimi değişen diğer ödül de alındıysa
karşılaştırma o round'da biter (7. bölüm).

## 1. Artçı ölçümündeki değişkenlik

### Belirti

Bölüm 3.7.1'de aynı kod ve aynı seed'le Artçı Patlama alan tam run'lar çalıştırmadan çalıştırmaya ×0,92–×1,13 oynuyordu.
Elektrik, Hasat Ritmi ve ödülsüz run'lar birebir tekrarlanıyordu.

### Teşhis düzeneği

Küçük bir tekrar senaryosu (`KirilmaErisimMeasurement.RunDiagnosis`): aynı tarla, aynı seed, aynı statlar, aynı nişan; tek
round. Her iş ayrı sahne yüklemesinde, sabit simülasyon adımıyla (1/30 sn). Her vuruş, hasat ve yankı olayı bir iz dosyasına
yazıldı: kare, hedef hücre, bitkinin yaşam sürümü, hasar, yankının planlandığı ve uygulandığı kare, bekleyen iş sayısı ve
oyunun zar akışının o andaki özeti. Tekrarların izleri satır satır karşılaştırıldı.

| Kol | Tekrar | Düzeltmeden önce | Düzeltmeden sonra |
|---|---|---|---|
| Artçı kapalı (kontrol), patlama sinerjisi R23 | 5 | 5 / 5 aynı | 5 / 5 aynı |
| Artçı açık, patlama sinerjisi R23 | 5 | **1 / 5 aynı** · yankı 6 ya da 7 kare bekledi | 5 / 5 aynı · hep 6 kare |
| Çifte Akım açık, elektrik sinerjisi R23 | 5 | 5 / 5 aynı · hep 5 kare | 5 / 5 aynı |
| Artçı açık, karma düzen R40 | 3 | 3 / 3 aynı sonuç · yankı 6–7 kare | 3 / 3 aynı · hep 6 kare |

**İlk ayrışan olay** (düzeltmeden önce, iki tekrarın izi): 121. karede aynı patlama, aynı hedefler, aynı zar özeti; yankı aynı
karede planlanıyor. Bir tekrarda yankı 127. karede, diğerinde 128. karede vuruyor. Öncesindeki bütün satırlar aynı; zar akışı,
hedef sırası ve bitki yaşam sürümleri de aynı.

### Kök neden

`BehaviorEchoes` yankıyı `Time.time + gecikme` anına planlıyor ve her kare `Time.time` ile karşılaştırıyordu.

- `Time.time` tek duyarlıklı bir sayıdır; oturum uzadıkça çözünürlüğü kabalaşır (225. saniyede yaklaşık 0,000015 sn).
- Artçının gecikmesi 0,20 sn, sabit adım 1/30 sn: gecikme tam 6 karedir. Karşılaştırma tam eşiğe düşer ve yuvarlama, işin
  planlandığı andaki mutlak zamana göre 6 ya da 7 kare verir.
- Mutlak zaman her çalıştırmada farklıdır: sahne yüklenirken geçen gerçek süre oyun zamanına eklenir.
- Çifte Akım'ın gecikmesi 0,15 sn = 4,5 kare; eşiğe düşmez, hep 5 kare bekler. Bu yüzden yalnız Artçı'lı run'lar oynuyordu.

Kontrol edilip **neden olmadığı görülenler:** zar akışını tüketen görseller (hasar yazısı ve vuruş sesi zar tüketiyor, ama kareye
bağlı ve tekrarlanabilir; atlanan yazı zar dizisini koruyor), nesne ya da koleksiyon dolaşım sırası (hedefler hücre sırasıyla
gezilir), havuzdan yeniden doğan bitki (iş hücre tutar, bitki nesnesi tutmaz), sonraki round'a sızan iş (round sonunda
düşürülür; izde bekleyen iş 0).

### Düzeltme

`BehaviorEchoes` artık kalan süreyi sayıyor: iş planlandığı kareden sonraki her karede `Time.deltaTime` kadar azalır, süre
dolunca uygulanır (0,0001 sn tolerans). Mutlak zamana bakılmaz; işin planlandığı karenin süresi sayılmaz.

- Gerçek zamanlı oyunda kural aynıdır: yankı, gecikme dolduktan sonraki ilk karede vurur.
- **Eski profilleri de etkiler** (Artçı ve Çifte Akım hepsinde bu kodla çalışır). Fark en çok bir karedir (1/30 sn'lik adımda
  0,033 sn) ve yalnız sabit adımlı ölçümde, eşiğe düşen gecikmede görülür.

### Düzeltmenin tek başına etkisi (erişim değişmeden)

Mevcut Artçı (1,50 hücre) ve ödülsüz kol, patlamalı üç düzen, üç round, 10 seed: düzeltmeden önce ve sonra aynı işler.

| | Ödülsüz kol | Mevcut Artçı |
|---|---|---|
| Sonucu birebir aynı kalan round | 90 / 90 | 87 / 90 |
| Önce 7 kare bekleyen yankısı olan round | – | 46 / 90 |
| Değişen üç round (hepsi R40) | – | hasat 136 → 137, 318 → 324, 106 → 124 · skor 744 → 745, 1.351 → 1.168, 313 → 421 |

Tek round'da bir karelik kayma çoğu zaman sonucu değiştirmiyor. Değiştirdiğinde fark küçük değil (bir round'da hasat +%17,
skor −%14 ile +%35 arası); 50 round'luk birikimli run'da zar akışı bir kez ayrışınca devamı da ayrışıyor. 3.7.1'deki ±%10
buradan geliyordu.

### Tam run'da tekrar kontrolü

3.7.1'de oynayan koşunun kendisi: `Run50_KirilmaV1`, patlama yolu, bahçıvan + standart orak, seed 100–104, 50 round. Aynı kod,
iki ayrı Unity çalıştırması (`BalanceRunMeasurement.RunK375Tekrar`).

| Seed | Artçı Patlama | Run'daki yankı | Son skor · 1. çalıştırma | Son skor · 2. çalıştırma | Toplam hasat (ikisinde) |
|---|---|---|---|---|---|
| 100 | R15'te alındı | 2.065 | 202.407 | 202.407 | 11.132 |
| 101 | teklif gelmedi | 0 | 67.104 | 67.104 | 5.094 |
| 102 | R20'de alındı | 4.899 | 537.339 | 537.339 | 22.900 |
| 103 | R20'de alındı | 888 | 108.226 | 108.226 | 6.475 |
| 104 | R10'da alındı | 2.400 | 296.409 | 296.409 | 12.591 |

**İki çalıştırmanın çıktı dosyası bayt bayt aynı** (255 satır × 95 sütun; md5 `3112a25d…`). 3.7.1'de aynı koşuda toplam skor
×0,92–×1,13 oynuyordu.

Bu sonuca iki adımda varıldı; ikisi de aynı türden hataydı:

1. Oyun kodundaki yankı zamanlaması düzeltildikten sonraki ilk denemede bütün oynanış sütunları (skor, hasat, level, kartlar,
   ödüller, yankı sayaçları) iki çalıştırmada aynıydı; yalnız "ortalama öldürme süresi" sütunu 255 satırın 10'unda 0,01 sn
   farklıydı.
2. O sütunu ölçüm aracı `Time.time` farkından hesaplıyordu. Araç artık kare sayıyor (`BalanceRunMeasurement`; oyun kodu
   değil). Sonrasında dosyalar birebir aynı. Aynı düzeltme "en yoğun 5 saniye" penceresini de kare sayısına bağladı (tam 150
   kare); o sütunun değerleri değişmedi.

Sınırı: 5 seed, aynı makine, iki çalıştırma. Başka makinede ya da derlenmiş oyunda denenmedi.

Veri: `Docs/Bolum3-7-5/olcum/tekrar-1/`, `tekrar-2/`; ilk deneme `olcum/tekrar-ilk/`. Teşhis izleri:
`Docs/Bolum3-7-5/teshis/once/iz/` ve `sonra/iz/` (düzeltmeden sonra her kolun tekrarları, başlık satırı dışında bayt bayt aynı).

## 2. Mevcut geometrinin doğrulanması

| Soru | Kodda ve sahnede bulunan |
|---|---|
| Artçının yarıçapı gerçekten 1,50 hücre mi? | Evet. Taban patlama yarıçapı 1,2 hücre × ödülün çarpanı 1,25 = 1,50. Tek hücrelik saksının çevresindeki 8 hücre (4 komşu + 4 çapraz) |
| İlk patlama | Saksının dört yönden komşuları (tek hücrelik saksıda 4 hücre). Değişmedi |
| Çifte Akım'ın ikinci dalgası | İlk dalgayla aynı geometri: dört çapraz yönde 2 hücre, en çok 8 hedef. Yeni hücresi yok |
| Uygulamadaki sabit sınır | Hasar döngüsü "en çok 8 hedef"te duruyordu ve çizim dizileri 8 şimşeklikti. 2 hücrelik erişimde 8 hedef aşılamadığı için bugüne kadar bir şey kesilmedi; erişim büyüyünce sessizce keserdi |

Çarpan karşılıkları (veride çarpan durur): 1,50 hücre = ×1,25 · 2,00 hücre = ×1,667 · 2,25 hücre = ×1,875.

Erişilen hücre sayıları (oyunun geometri fonksiyonlarından; tarlanın dışı sayılmaz). "Toplam / ek": ikinci darbenin eriştiği
hücre / bunların ilk darbenin erişmediği kısmı.

**Patlama** (7×7 ve 9×9 tarlada aynı sayılar):

| Saksı | Yer | İlk patlama | Artçı 1,50 (eski) | Artçı 2,00 (seçilen) | Artçı 2,25 |
|---|---|---|---|---|---|
| 1×1 | orta | 4 | 8 / 4 | 12 / 8 | 20 / 16 |
| 1×1 | kenar | 3 | 5 / 2 | 8 / 5 | 12 / 9 |
| 1×1 | köşe | 2 | 3 / 1 | 5 / 3 | 7 / 5 |
| 2×2 | orta | 8 | 12 / 4 | 20 / 12 | 28 / 20 |
| 2×2 | kenar | 6 | 8 / 2 | 14 / 8 | 18 / 12 |
| 2×2 | köşe | 4 | 5 / 1 | 9 / 5 | 11 / 7 |
| 2×3 | orta | 10 | 14 / 4 | 24 / 14 | 32 / 22 |
| 2×3 | kenar | 7 | 9 / 2 | 16 / 9 | 20 / 13 |
| 2×3 | köşe | 5 | 6 / 1 | 11 / 6 | 13 / 8 |

**Elektrik** (ilk dalga her yerde 2 hücre):

| Saksı | Yer | İlk dalga | 7×7: ikinci dalga 2 / 3 / 4 hücre | 9×9: ikinci dalga 2 / 3 / 4 hücre |
|---|---|---|---|---|
| 1×1 | orta | 8 | 8 / 0 · 12 / 4 · 12 / 4 | 8 / 0 · 12 / 4 · 16 / 8 |
| 1×1 | kenar | 4 | 4 / 0 · 6 / 2 · 6 / 2 | 4 / 0 · 6 / 2 · 8 / 4 |
| 1×1 | köşe | 2 | 2 / 0 · 3 / 1 · 4 / 2 | 2 / 0 · 3 / 1 · 4 / 2 |
| 2×2 | orta | 8 | 8 / 0 · 9 / 1 · 9 / 1 | 8 / 0 · 12 / 4 · 13 / 5 |
| 2×2 | kenar | 4 | 4 / 0 · 5 / 1 · 5 / 1 | 4 / 0 · 6 / 2 · 7 / 3 |
| 2×2 | köşe | 2 | 2 / 0 · 3 / 1 · 4 / 2 | 2 / 0 · 3 / 1 · 4 / 2 |
| 2×3 | orta | 8 | 8 / 0 · 8 / 0 · 8 / 0 | 8 / 0 · 12 / 4 · 12 / 4 |
| 2×3 | kenar | 4 | 4 / 0 · 4 / 0 · 4 / 0 | 4 / 0 · 6 / 2 · 6 / 2 |
| 2×3 | köşe | 2 | 2 / 0 · 3 / 1 · 4 / 2 | 2 / 0 · 3 / 1 · 4 / 2 |

Tablonun tamamı: `Docs/Bolum3-7-5/olcum/ham/KirilmaErisimGeometri.md`.

- Artçı 2,00 hücre, tek hücrelik saksının çevresinde 12 hücreye erişir (eskisi 8); 2×2 saksıda 20 hücre (eskisi 12).
- Elektrikte tarla boyutu belirleyici: 7×7 tarlanın ortasından çaprazda en çok 3 hücre var; 4 hücrelik erişim orada 3 hücreyle
  aynı sonucu verir. Büyük saksıda ışınlar köşelerden çıktığı için ek hücre daha da azdır (7×7'de 2×3 saksı ortadayken hiç yok).

## 3. Yapılan mekanik değişiklikler

| Değişiklik | Kapsam |
|---|---|
| Yankı gecikmesi kalan süreyle sayılıyor | Bütün profiller (kök neden düzeltmesi) |
| Çifte Akım'ın ikinci dalgasına erişim alanı (`echoReach`, veri) | Varsayılan 0 = ilk dalgayla aynı. Yalnız yeni profilin varyantı 4 |
| Elektrik geometrisine erişim parametresi | İlk dalga hep 2 hücre. İkinci dalga yakın hücreleri de kapsar; yalnız ışınlar uzar |
| Hasar döngüsündeki "en çok 8 hedef" sınırı kaldırıldı | Hasar bütün hedeflere uygulanır. Çizim kapasitesi 8'den 16 şimşeğe çıktı; aşan hedefin yalnız şimşeği çizilmez ve sayılır |
| Artçının yarıçapı | Kod değişmedi; yalnız yeni profilin varyantında çarpan ×1,667 |

Değişmeyenler: tetik kuralları, ilk patlama ve ilk dalga, hasar oranları, gecikmeler, "yankı yeni tetik başlatmaz" kuralı,
elektriğin öldürdüğü bitkinin davranış tetiklememesi. Yeni hedefe yönelme, sıçrayış ya da zincir yok. Bir dalgada bir bitkiye
bir kez vurulur. Hesaplanmış yankı hasarına Davranışa Adanış ya da uzmanlaşma çarpanı ikinci kez uygulanmaz.

## 4. Laboratuvar: adayların karşılaştırması

### Ne ölçüldü

Run oynanmadı. Sabit tarla ve sabit statlarla tek round oynatıldı (`KirilmaErisimMeasurement`); bot oyunun gerçek saldırı
yoluyla vurur, davranışlar ve yankılar oyunun kendi kodundan çalışır. Level ve kart yok. Bedelli ödüller kapalı. Kollar yalnız
bir ödülle ayrışır. **Düzenler, statlar ve karar kuralı ölçümden önce yazıldı** (kaynak dosyanın başındaki "ön kayıt").

| Ayar | Değer |
|---|---|
| Round'lar | R23, R30, R40 (takvimdeki boss'larıyla; boss seed'e göre seçilir, bir seed'in bütün kollarında aynıdır) |
| Statlar | R23: 7×7 tarla, hasar 27, saldırı aralığı 2,00 sn, yarıçap 1,10 · R30: 9×9, 44, 1,90 sn, 1,20 · R40: 9×9, 75, 1,28 sn, 1,50 |
| Saksılar | 2×2 saksılar; son sütun ve satır 1×1 (7×7: 9 büyük + 13 küçük; 9×9: 16 + 17) |
| Nişan | Saldırı alanında en çok canlı bitki olan nokta. İkinci bir politika kullanılmadı |
| Seed | 10 ortak seed; her koşulda bütün kollar aynı seed'lerle |
| Toplam | 1.200 round |

Statlar, Bölüm 3.6 son parametre run'larının patlama ve elektrik yolu medyanlarıdır.

| Düzen | Davranış tile'ları |
|---|---|
| Doğrudan ağırlıklı | Her 4 saksıdan biri: sırayla patlama %25 / elektrik %15 |
| Patlama ağırlıklı | Her 3 saksıdan ikisi patlama %45 |
| Elektrik ağırlıklı | Her 3 saksıdan ikisi elektrik %30 |
| Karma davranışlı | Beşli döngü: patlama %40, elektrik %28, kasırga %40, bumerang %28, davranışsız |
| Patlama · güçlü sinerji | Bütün saksılar patlama %90 |
| Elektrik · güçlü sinerji | Bütün saksılar elektrik %60 |

Oranlar aynı seed'in ödülsüz koluna göredir: 10 seed'in medyanı; parantezde en düşük – en yüksek.

### Artçı Patlama

Toplam hasat oranı:

| Düzen | Aday | R23 | R30 | R40 |
|---|---|---|---|---|
| Patlama · güçlü sinerji | A · 1,50 hücre (mevcut) | ×1,43 (1,19–1,92) | ×1,34 (1,06–1,98) | ×1,39 (1,28–1,52) |
| | **B · 2,00 hücre** | **×1,63** (1,30–2,51) | **×1,79** (1,57–2,40) | **×1,62** (1,46–1,80) |
| | C · 2,25 hücre | ×1,98 (1,37–2,55) | ×2,06 (1,45–2,81) | ×1,84 (1,44–2,08) |
| Patlama ağırlıklı | A · 1,50 hücre (mevcut) | ×1,21 (0,98–1,47) | ×1,42 (1,11–1,67) | ×1,24 (1,13–1,33) |
| | **B · 2,00 hücre** | ×1,31 (1,09–1,78) | ×1,66 (1,33–2,02) | ×1,51 (1,09–1,98) |
| | C · 2,25 hücre | ×1,53 (1,25–2,07) | ×1,81 (1,40–2,24) | ×1,58 (1,29–1,96) |
| Karma davranışlı | A · 1,50 hücre (mevcut) | ×1,02 (0,89–1,24) | ×1,08 (0,95–1,25) | ×1,17 (0,94–1,31) |
| | **B · 2,00 hücre** | ×1,10 (0,88–1,37) | ×1,16 (1,00–1,75) | ×1,23 (1,03–1,42) |
| | C · 2,25 hücre | ×1,04 (0,95–1,43) | ×1,22 (1,00–1,64) | ×1,35 (1,02–1,52) |
| Doğrudan ağırlıklı | A · 1,50 hücre (mevcut) | ×1,03 (0,85–1,22) | ×1,08 (1,00–1,36) | ×1,01 (0,91–1,09) |
| | C · 2,25 hücre | ×1,00 (0,92–1,24) | ×1,20 (0,95–1,57) | ×1,05 (0,94–1,13) |

Güçlü sinerji düzeninde 30 eşin 30'unda üç aday da ödülsüz kolun üstünde.

Artçının ayrıntısı (patlama · güçlü sinerji, round başına ortalama):

| Aday | Round | Tetik | Eriştiği hücre | Canlı hedef (ilk patlamanın erişmediği alanda) | Boş hücreye giden | Artçı hasadı (yeni alanda) | Hasar: vuruş / candan götüren / fazlası | Doğrudan + davranış hasadı |
|---|---|---|---|---|---|---|---|---|
| A · 1,50 | R23 | 28,9 | 248 | 120 (45) | 128 (%52) | 36,9 (12,0) | 3.240 / 2.468 / 772 | 31,2 + 65,8 |
| A · 1,50 | R30 | 32,4 | 293 | 118 (45) | 176 (%60) | 55,0 (13,5) | 5.170 / 4.439 / 731 | 35,7 + 96,4 |
| A · 1,50 | R40 | 73,7 | 707 | 242 (100) | 466 (%66) | 116,4 (36,9) | 18.128 / 15.178 / 2.950 | 86,2 + 215,2 |
| **B · 2,00** | R23 | 29,3 | 393 | 198 (131) | 195 (%50) | 57,3 (32,4) | 5.357 / 4.174 / 1.182 | 31,6 + 82,1 |
| **B · 2,00** | R30 | 35,9 | 532 | 236 (169) | 296 (%56) | 88,9 (52,0) | 10.380 / 9.207 / 1.173 | 39,7 + 134,0 |
| **B · 2,00** | R40 | 76,0 | 1.129 | 412 (296) | 717 (%64) | 167,9 (105,1) | 30.892 / 26.595 / 4.298 | 87,4 + 263,9 |
| C · 2,25 | R23 | 29,7 | 525 | 264 (199) | 261 (%50) | 72,3 (47,0) | 7.128 / 5.657 / 1.471 | 32,2 + 97,9 |
| C · 2,25 | R30 | 36,2 | 707 | 316 (254) | 391 (%55) | 113,4 (80,9) | 13.908 / 12.436 / 1.473 | 39,8 + 159,7 |
| C · 2,25 | R40 | 73,3 | 1.441 | 515 (409) | 926 (%64) | 204,3 (141,5) | 38.648 / 33.457 / 5.190 | 83,1 + 297,3 |

Ne okunur:

- **Artış yeni hedeflerden geliyor.** 2,00 hücrede canlı hedeflerin üçte ikisi (131 / 198, 169 / 236, 296 / 412) ilk patlamanın
  erişmediği hücrelerde; artçı hasatlarının da yarıdan fazlası orada.
- **Fazlası (overkill) payı büyümüyor:** vuruş toplamının %11–22'si; mevcut Artçı'da %14–24.
- **Darbelerin yarısı boş hücreye gidiyor** (%50–64) ve bu oran erişimle değişmiyor: tarla %50–70 dolu.
- **Doğrudan hasat aynı kalıyor;** artan, davranış hasadı.

### Çifte Akım

Toplam hasat oranı:

| Düzen | Aday | R23 | R30 | R40 |
|---|---|---|---|---|
| Elektrik · güçlü sinerji | A · 2 hücre (mevcut) | ×1,18 (0,90–1,86) | ×1,20 (0,95–1,48) | ×1,16 (1,02–1,35) |
| | B · 3 hücre | ×1,20 (0,90–1,75) | ×1,25 (1,02–1,77) | ×1,28 (1,02–1,39) |
| | **C · 4 hücre** | **×1,30** (1,09–1,75) | **×1,33** (1,02–1,77) | **×1,34** (1,12–1,69) |
| Elektrik ağırlıklı | A · 2 hücre (mevcut) | ×1,16 (0,95–1,58) | ×1,15 (0,90–1,43) | ×1,18 (1,03–1,37) |
| | B · 3 hücre | ×1,17 (0,98–1,45) | ×1,30 (0,82–1,43) | ×1,14 (1,04–1,40) |
| | **C · 4 hücre** | ×1,24 (0,98–1,55) | ×1,14 (0,76–1,46) | ×1,20 (1,04–1,33) |
| Karma davranışlı | A · 2 hücre (mevcut) | ×0,96 (0,88–1,36) | ×1,00 (0,75–1,07) | ×1,03 (0,97–1,12) |
| | B · 3 hücre | ×1,09 (1,00–1,20) | ×1,00 (0,90–1,07) | ×1,04 (0,95–1,19) |
| | **C · 4 hücre** | ×1,02 (0,93–1,29) | ×1,00 (0,82–1,07) | ×1,03 (0,94–1,12) |
| Doğrudan ağırlıklı | A · 2 hücre (mevcut) | ×1,00 (1,00–1,16) | ×1,00 (0,97–1,17) | ×1,00 (0,93–1,08) |
| | C · 4 hücre | ×1,00 (0,98–1,11) | ×1,00 (0,93–1,20) | ×1,04 (0,99–1,28) |

İkinci dalganın ayrıntısı (elektrik · güçlü sinerji, round başına ortalama):

| Aday | Round | Tetik | Eriştiği hücre | Canlı hedef (ilk dalganın erişmediği alanda) | Boş hücreye giden | İkinci dalga hasadı (yeni alanda) | Hasar: vuruş / candan götüren / fazlası |
|---|---|---|---|---|---|---|---|
| A · 2 | R23 | 20,3 | 90 | 47 (0) | 43 (%47) | 12,3 (0) | 1.272 / 1.013 / 259 |
| A · 2 | R40 | 55,3 | 277 | 114 (0) | 163 (%59) | 62,6 (0) | 8.565 / 7.040 / 1.525 |
| B · 3 | R23 | 19,1 | 104 | 62 (15) | 43 (%41) | 15,1 (2,8) | 1.660 / 1.343 / 318 |
| B · 3 | R40 | 55,2 | 356 | 160 (49) | 196 (%55) | 72,7 (15,4) | 11.970 / 10.246 / 1.724 |
| **C · 4** | R23 | 20,9 | 122 | 69 (23) | 53 (%43) | 16,0 (3,4) | 1.866 / 1.524 / 342 |
| **C · 4** | R40 | 56,8 | 432 | 196 (87) | 236 (%55) | 84,2 (27,9) | 14.708 / 12.655 / 2.052 |

Ne okunur:

- **Erişim arttıkça yeni hedef geliyor ama az:** 4 hücrede canlı hedeflerin %34–47'si yeni alanda (Artçı'da %66–72). Dört
  çapraz ışın tarlanın küçük bir kısmını tarar ve tarlanın kenarı ışını keser.
- **Kalıcı ama küçük fayda:** elektrik düzenlerinde 4 hücre ×1,14–×1,34. Karma ve doğrudan ağırlıklı düzende fark yok
  (×1,00–×1,04).
- **Mevcut Çifte Akım da bu laboratuvarda ×1,15–×1,20 veriyor.** Bölüm 3.6'nın gerçek run'larında fark çıkmamıştı (×0,97–×1,07).
  Laboratuvar sabit ve davranışı yüksek düzenlerdir; birikimli run'da (7. bölüm) eski Çifte Akım, yerine
  alınan ödülün düzeyinde ya da gerisinde kaldı (×0,95), yani 3.6'daki sonuçla uyumlu. Laboratuvardaki oran "hiç ödül
  almamaya" göredir; run'daki oran "başka bir ödül almaya" göre.

### Karşılaştırma noktaları: Hasat Ritmi ve Yıkım Gücü

Aynı düzen, aynı seed'ler, tek ödül. Toplam hasat oranı (medyan):

| Düzen | Ödül | R23 | R30 | R40 |
|---|---|---|---|---|
| Patlama · güçlü sinerji | Artçı B · 2,00 hücre | ×1,63 | ×1,79 | ×1,62 |
| | Hasat Ritmi | ×1,25 | ×1,31 | ×1,25 |
| | Yıkım Gücü ×1 | ×1,27 | ×1,05 | ×1,05 |
| Patlama ağırlıklı | Artçı B · 2,00 hücre | ×1,31 | ×1,66 | ×1,51 |
| | Hasat Ritmi | ×1,21 | ×1,27 | ×1,24 |
| | Yıkım Gücü ×1 | ×1,05 | ×1,01 | ×0,99 |
| Elektrik · güçlü sinerji | Çifte Akım C · 4 hücre | ×1,30 | ×1,33 | ×1,34 |
| | Hasat Ritmi | ×1,38 | ×1,35 | ×1,24 |
| | Yıkım Gücü ×1 | ×1,18 | ×1,05 | ×1,01 |
| Elektrik ağırlıklı | Çifte Akım C · 4 hücre | ×1,24 | ×1,14 | ×1,20 |
| | Hasat Ritmi | ×1,30 | ×1,23 | ×1,33 |
| | Yıkım Gücü ×1 | ×1,12 | ×1,00 | ×1,03 |
| Karma davranışlı | Artçı B · 2,00 hücre | ×1,10 | ×1,16 | ×1,23 |
| | Çifte Akım C · 4 hücre | ×1,02 | ×1,00 | ×1,03 |
| | Hasat Ritmi | ×1,36 | ×1,26 | ×1,27 |
| | Yıkım Gücü ×1 | ×1,17 | ×0,99 | ×1,04 |
| Doğrudan ağırlıklı | Hasat Ritmi | ×1,29 | ×1,29 | ×1,43 |

- Hasat Ritmi her düzende ×1,2–×1,4 veriyor; değerlerine dokunulmadı.
- Artçı 2,00 hücre yalnız patlama düzenlerinde Hasat Ritmi'ni geçiyor; karma ve doğrudan düzende Ritim önde.
- Çifte Akım 4 hücre, kendi güçlü sinerji düzeninde Hasat Ritmi'yle aynı düzeyde; diğer düzenlerde geride.
- Yıkım Gücü'nün tek adedi hasadı pek değiştirmiyor (×0,99–×1,27): hasar artıyor, erişilen hedef artmıyor.

Tam tablolar: `Docs/Bolum3-7-5/olcum/LabSonuclari.md`; ham veri: `Docs/Bolum3-7-5/olcum/ham/KirilmaErisim_lab23.csv`, `…30.csv`, `…40.csv`.

## 5. Karar

Kural ölçümden önce yazıldı: güçlü sinerji düzeninde, ödülsüz kola göre medyan toplam hasat ≥ ×1,5, üç round'un en az ikisinde.
Geçenlerden küçük erişim; hiçbiri geçmezse gerçek hasat katkısı en yüksek aday.

| Ödül | Aday | R23 | R30 | R40 | Hedefi tutan round | Karar |
|---|---|---|---|---|---|---|
| Artçı Patlama | A · 1,50 (mevcut) | ×1,43 | ×1,34 | ×1,39 | 0 / 3 | – |
| | **B · 2,00** | ×1,63 | ×1,79 | ×1,62 | 3 / 3 | **Seçildi** (hedefi tutan en küçük erişim) |
| | C · 2,25 | ×1,98 | ×2,06 | ×1,84 | 3 / 3 | Seçilmedi (daha büyük erişim) |
| Çifte Akım | A · 2 (mevcut) | ×1,18 | ×1,20 | ×1,16 | 0 / 3 | – |
| | B · 3 | ×1,20 | ×1,25 | ×1,28 | 0 / 3 | – |
| | **C · 4** | ×1,30 | ×1,33 | ×1,34 | 0 / 3 | **Teslim edildi; hedef tutmadı** (katkısı en yüksek aday) |

**×1,5 hedefi.** Artçı 2,00 hücre: güçlü patlama sinerjisinde üç round'da tuttu; patlama ağırlıklı düzende R30 ve R40'ta
tuttu (×1,66, ×1,51), R23'te tutmadı (×1,31); karma ve doğrudan düzende tutmadı. Çifte Akım: hiçbir düzende tutmadı.
Hasar, tetik ya da gecikme yükseltilmedi; zincir eklenmedi.

**Çifte Akım havuzda mı?** Evet. Kural (ölçümden önce): elektrik ağırlıklı ya da elektrik güçlü sinerji düzeninde, üç
round'un en az ikisinde medyan ≥ ×1,10 ve 10 seed'in en az 7'sinde artış.

| Düzen | R23 | R30 | R40 |
|---|---|---|---|
| Elektrik ağırlıklı | ×1,24 · 9 / 10 | ×1,14 · 9 / 10 | ×1,20 · 10 / 10 |
| Elektrik · güçlü sinerji | ×1,30 · 10 / 10 | ×1,33 · 10 / 10 | ×1,34 · 10 / 10 |

İki düzende de üç round'da sağlandı. Ödül eski profillerden silinmedi, adı altında başka bir mekanik de kurulmadı.

**Kullanıcı kararı (Bölüm 3.7.5.1):** Çifte Akım 4 hücre yeni profilin havuzunda kalır; kırılma hedefini tutturmuş
sayılmaz. Aşağıdaki not, kararın verildiği andaki durumu anlatır.

**Senin kararına bıraktığım nokta.** Kural laboratuvar içindi ve "hiç ödül almamaya göre fayda"yı ölçüyordu. Birikimli run'da
(7. bölüm) Çifte Akım 4 hücre, aynı tekliften alınan başka bir ödülü tutarlı biçimde geçmiyor (×1,04; 6 seed'in 4'ünde). Kuralı
sonucu gördükten sonra değiştirmedim; ödül havuzda. Çıkarmak istersen: `kirilma_erisimi_v1_params.py` içinde
`CIFTE['in_pool'] = False`, ardından üretici. Üretici bu yolu destekliyor; o hâliyle test turu çalıştırılmadı.

### Kod düzeltmesi ile erişim ayarının ayrı etkileri

| | Etki |
|---|---|
| Yankı zamanlaması düzeltmesi (erişim aynı) | 90 round'un 87'si birebir aynı; üçünde hasat +1 / +6 / +18. Ortalama hasat aynı düzeyde. Kazancı, ölçümün tekrarlanabilir olması |
| Artçı erişimi 1,50 → 2,00 hücre (düzeltilmiş kodla) | Güçlü sinerjide medyan hasat ×1,43 → ×1,63, ×1,34 → ×1,79, ×1,39 → ×1,62 |
| Çifte Akım erişimi 2 → 4 hücre (düzeltilmiş kodla) | Güçlü sinerjide ×1,18 → ×1,30, ×1,20 → ×1,33, ×1,16 → ×1,34 |

## 6. Gerçek zamanlı kontrol

Aynı laboratuvar, kare adımı sabitlenmeden (`RunRealtime`): R23, güçlü sinerji düzenleri, üç seed. Birebir tekrar beklenmez; yön
ve büyüklük kontrolüdür. Batch'te kare hızı çok yüksek çıktı (45 sn'lik round yaklaşık 160.000 kare); yankı 0,20 sn'yi 270–560
karede bekledi.

| Aday | Gerçek zamanlı (3 seed medyanı) | Aynı üç seed, sabit adım |
|---|---|---|
| Artçı A · 1,50 | ×1,39 | ×1,29 |
| Artçı B · 2,00 | ×1,60 | ×1,49 |
| Artçı C · 2,25 | ×1,81 | ×1,48 |
| Çifte Akım A · 2 | ×1,16 | ×1,09 |
| Çifte Akım B · 3 | ×1,14 | ×1,14 |
| Çifte Akım C · 4 | ×1,28 | ×1,28 |

Sıralama ve büyüklük aynı: Artçı'da erişim büyüdükçe hasat artıyor, Çifte Akım'da 4 hücre en iyisi. Üç seed azdır; tek tek
eşlerde ×0,98 ile ×2,20 arası oranlar var. Oyuncunun gerçek kare hızında (60 kare/sn) ayrıca denenmedi.

Veri: `Docs/Bolum3-7-5/olcum/ham/KirilmaErisim_gercekzaman.csv`.

## 7. Birikimli run doğrulaması (seçilen adaylar)

Laboratuvardan ayrı bir ölçümdür: tam 50 round'luk run, level, kart, ağaç, boss ve ödüllerle. Yalnız seçilen iki aday ölçüldü.

| Ayar | Değer |
|---|---|
| Run | 2 politika (patlama yolu, elektrik yolu) × 10 seed (100–109) × 3 kol = 60 tam run; bahçıvan + standart orak |
| Aday | `Run50_KirilmaErisimiV1` |
| Mevcut | `Run50_BedelliOdullerV1` (eski erişim; başka her şey aynı) |
| Almayan eş | Aday profil; bot kırılma ödülünü almaz, **aynı tekliften başka bir ödül alır** |
| Karşılaştırılan | Hedef ödülün alındığı round'dan sonraki round'ların toplam hasadı ve skoru |

"÷ almayan eş" oranı fırsat maliyetini içerir: ödül, yerine alınacak başka bir ödüle karşı ölçülür. Laboratuvardaki "ödülsüz
kol" oranıyla aynı şey değildir ve ×1,5 kuralı bununla sınanmaz.

Kontroller:

- Ödülün alındığı round'a kadar üç kol her seed'de aynı (hasat, skor, level, kart, ödül).
- Erişimi değişen ödüllerden hiçbiri gelmeyen seed'lerde üç kol aynı run'ı veriyor (elektrik yolu seed 100: üçünde 406.617;
  seed 102: üçünde 270.260). İki profil gerçekten yalnız iki ödülde ayrışıyor.
- Karşılaştırma şu durumlarda o round'da kesilir: almayan eş bir kırılma ödülünü mecburen aldıysa (teklifin üçü de kırılma
  ödülüyse) ya da erişimi değişen diğer ödül de alındıysa. Diğer ödül hedeften önce alındıysa seed eşli karşılaştırmaya girmez.
- 60 run'ın 60'ı R50'de kazandı.

### Artçı Patlama 2,00 hücre (patlama yolu)

Ödül 10 seed'in 9'unda teklif edildi ve alındı (R23–R46).

| Seed | Alındı | Karşılaştırılan | Hasat: aday / mevcut / almayan eş | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut | Yankı başına hasat: aday / mevcut |
|---|---|---|---|---|---|---|---|
| 100 | R23 | R24–R50 | 23.650 / 14.285 / 16.397 | ×1,44 | ×0,87 | ×1,66 | 1,77 / 0,80 |
| 102 | R30 | R31–R50 | 15.478 / 11.775 / 12.883 | ×1,20 | ×0,91 | ×1,31 | 1,99 / 0,67 |
| 103 | R23 | R24–R36 (eş R36'da Artçı'yı mecburen aldı) | 6.522 / 4.421 / 4.485 | ×1,45 | ×0,99 | ×1,48 | 3,38 / 1,15 |
| 104 | R23 | R24–R36 (R36'da Çifte Akım da alındı) | 5.568 / 4.182 / 5.033 | ×1,11 | ×0,83 | ×1,33 | 3,23 / 1,65 |
| 105 | R23 | R24–R50 | 28.307 / 21.599 / 17.250 | ×1,64 | ×1,25 | ×1,31 | 1,90 / 0,73 |
| 106 | R46 | R47–R50 | 3.747 / 3.003 / 2.587 | ×1,45 | ×1,16 | ×1,25 | 1,83 / 0,73 |
| 107 | R30 | R31–R50 | 22.028 / 19.290 / 18.430 | ×1,20 | ×1,05 | ×1,14 | 1,52 / 0,77 |
| 108 | R36 | R37–R50 | 11.691 / 10.738 / 8.666 | ×1,35 | ×1,24 | ×1,09 | 1,70 / 0,59 |
| 109 | R23 | R24–R50 | 22.958 / 20.450 / 17.548 | ×1,31 | ×1,17 | ×1,12 | 1,38 / 0,52 |

| Oran (ödülden sonraki round'lar) | Medyan | En düşük – en yüksek | Artış olan seed |
|---|---|---|---|
| Hasat: aday ÷ almayan eş | ×1,35 | ×1,11 – ×1,64 | 9 / 9 |
| Hasat: mevcut ÷ almayan eş | ×1,05 | ×0,83 – ×1,25 | 5 / 9 |
| Hasat: aday ÷ mevcut | ×1,31 | ×1,09 – ×1,66 | 9 / 9 |
| Skor: aday ÷ almayan eş | ×1,38 | ×1,20 – ×1,96 | 9 / 9 |
| Skor: aday ÷ mevcut | ×1,37 | ×1,09 – ×1,72 | 9 / 9 |

| Round aralığı | Seed | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut |
|---|---|---|---|---|
| R23–R30 | 5 | ×1,52 | ×1,18 | ×1,27 |
| R31–R40 | 8 | ×1,31 | ×1,01 | ×1,25 |
| R41–R50 | 7 | ×1,37 | ×1,09 | ×1,25 |

- **Geniş Artçı her seed'de işe yarıyor:** eski Artçı'ya göre 9 / 9, başka bir ödüle göre 9 / 9.
- **Eski Artçı başka bir ödülden iyi değildi** (×1,05; dört seed'de geride). 3.6'da "kırılma hissi yok" denmesinin sayısal karşılığı.
- **Yankı başına hasat iki katından fazla:** 1,4–3,4 (eski 0,5–1,7). Artış yeni hedeflerden geliyor.
- **Başka bir ödüle göre ×1,5'in altında:** medyan ×1,35; tek seed ×1,5'i geçti (×1,64). Yalnız R23–R30 aralığında ×1,52.
  Run ilerledikçe diğer güç kaynakları büyüyor ve tek ödülün payı küçülüyor.
- Run toplamında fark büyük olabiliyor (seed 100: skor 825.352 / 494.605 / 602.624); iki seed'de mevcut ödüllü run, almayan
  eşin gerisinde bitti.

### Çifte Akım 4 hücre (elektrik yolu)

Ödül 10 seed'in 8'inde alındı. İkisi eşli karşılaştırmaya giremedi (seed 103: eş aynı round'da Çifte Akım'ı mecburen aldı; seed
109: önce Artçı alınmıştı). Kalan 6 seed'in ikisinde pencere kısa (7 ve 10 round; sonrasında Artçı da alındı).

| Seed | Alındı | Karşılaştırılan | Hasat: aday / mevcut / almayan eş | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut | İkinci dalga başına hasat: aday / mevcut |
|---|---|---|---|---|---|---|---|
| 101 | R23 | R24–R30 | 1.320 / 1.057 / 1.344 | ×0,98 | ×0,79 | ×1,25 | 0,96 / 0,50 |
| 104 | R26 | R27–R36 | 2.892 / 2.639 / 3.283 | ×0,88 | ×0,80 | ×1,10 | 1,07 / 0,38 |
| 105 | R43 | R44–R50 | 8.261 / 7.661 / 7.861 | ×1,05 | ×0,97 | ×1,08 | 0,89 / 0,19 |
| 106 | R26 | R27–R50 | 14.088 / 14.188 / 11.693 | ×1,20 | ×1,21 | ×0,99 | 1,10 / 0,25 |
| 107 | R30 | R31–R50 | 18.540 / 16.712 / 17.956 | ×1,03 | ×0,93 | ×1,11 | 0,63 / 0,23 |
| 108 | R30 | R31–R50 | 14.748 / 13.704 / 11.564 | ×1,28 | ×1,19 | ×1,08 | 1,00 / 0,28 |

| Oran (ödülden sonraki round'lar) | Medyan | En düşük – en yüksek | Artış olan seed |
|---|---|---|---|
| Hasat: aday ÷ almayan eş | ×1,04 | ×0,88 – ×1,28 | 4 / 6 |
| Hasat: mevcut ÷ almayan eş | ×0,95 | ×0,79 – ×1,21 | 2 / 6 |
| Hasat: aday ÷ mevcut | ×1,09 | ×0,99 – ×1,25 | 5 / 6 |
| Skor: aday ÷ almayan eş | ×1,06 | ×0,78 – ×1,42 | 4 / 6 |
| Skor: aday ÷ mevcut | ×1,12 | ×0,99 – ×1,20 | 5 / 6 |

- **Erişim işe yarıyor ama az:** ikinci dalga başına hasat 0,2–0,5'ten 0,6–1,1'e çıktı; eski Çifte Akım'a göre 5 / 6 seed'de artış.
- **Başka bir ödülü geçmiyor:** ×1,04, 6 seed'in 4'ünde. Altı seed azdır; aralık ×0,88–×1,28.
- Eski Çifte Akım, yerine alınan ödülün gerisinde (×0,95; 2 / 6).

### Görseller ve sınırlar (bu 60 run'da)

- Atlanan patlama görseli ve atlanan bumerang yok.
- Elektrik yolunda elektrik efekti havuzu doluyor: 10 run'da aday 1.362, mevcut 1.926 dalganın yalnız görseli atlandı (hasar
  uygulandı). Erişimle gelen bir şey değil; eski profilde daha fazla.
- Round sonunda düşen yankı: patlama yolunda 29 / 32.656, elektrik yolunda 49 / 23.631 (round bittiği için).

Veri: `Docs/Bolum3-7-5/olcum/dogrulama/`; tablolar `Docs/Bolum3-7-5/olcum/DogrulamaSonuclari.md`
(`Tools/Balance/KirilmaErisimiV1/analyze_validation.py`).

## 8. Performans

Yoğun tarla: R40, 9×9, bütün saksılarda davranış (güçlü sinerji), aynı seed üç kez. Batch'te ekrana çizim yapılmaz; ölçülen,
oyun kodunun kare maliyetidir (render dahil değil).

| Düzen | Kol | Kare başına ortalama (ms) | En uzun kare (ms) | GC toplaması | Round'da yankı | Yankı başına hücre / canlı hedef | Aynı anda bekleyen iş (en çok) | Atlanan patlama görseli | Atlanan elektrik görseli | Çizilmeyen şimşek | Atlanan hasar |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patlama sinerjisi | Ödülsüz | 0,61 | 28,0 | 0 | 0 | – | 0 | 0 | 0 | 0 | 0 |
| | Artçı 1,50 (eski) | 0,69 | 10,7 | 0 | 84 | 9,3 / 2,5 | 5 | 0 | 0 | 0 | 0 |
| | **Artçı 2,00 (seçilen)** | 0,72 | 23,9 | 0 | 90 | 14,2 / 3,7 | 6 | 0 | 0 | 0 | 0 |
| | Artçı 2,25 | 0,75 | 21,5 | 0 | 83 | 18,9 / 4,8 | 5 | 0 | 0 | 0 | 0 |
| Elektrik sinerjisi | Ödülsüz | 0,51 | 4,8 | 0 | 0 | – | 0 | 0 | 0 | 0 | 0 |
| | Çifte Akım 2 (eski) | 0,53 | 5,5 | 0 | 73 | 4,9 / 1,7 | 5 | 0 | 0 | 0 | 0 |
| | Çifte Akım 3 | 0,56 | 3,6 | 0 | 69 | 6,5 / 2,2 | 5 | 0 | 0 | 0 | 0 |
| | **Çifte Akım 4 (seçilen)** | 0,54 | 3,5 | 0 | 57 | 7,4 / 2,6 | 5 | 0 | 0 | 0 | 0 |

- Seçilen Artçı, eskisine göre kare başına +0,03 ms (%5). Çifte Akım'da fark ölçüm gürültüsü içinde.
- "En uzun kare" tek tek sıçramalardır ve ödülsüz kolda da var (28 ms); yankıyla ilişkili bir artış görünmüyor.
- GC toplaması yok. Bekleyen iş sayısı 5–6'yı geçmedi. Havuz büyütülmedi: patlama görsel havuzu yetti; elektrik efekt havuzu
  sahnedeki değerinde (12 efekt) kaldı, yalnız efekt başına şimşek kapasitesi 8'den 16'ya çıktı (efekt başına 16 çizgi nesnesi
  daha; havuz kurulurken bir kez yaratılır).
- Görsel havuzu dolduğunda hasarın korunduğu işlev testinde ayrıca denendi (9. bölüm).

**Görsel.** Artçı, vurduğu her bitkide küçük bir patlama gösterir: yeni alan, vurulan bitkilerden görülür. Ortadaki ana patlama
görseli büyütülmedi. İkinci dalga her hedefe köşeden bir şimşek çizer; 4 hücrede en çok 16 şimşek. İkisine de insan gözüyle
bakılmadı.

## 9. Testler

Hepsi izole kopyada, düşük öncelikle, batch Unity ile. Tam tur 2 Ekim 23:11–23:29 arasında, o andaki kodla çalıştı.

### Yeni işlev testi: `KirilmaErisimiV1Verification` — PASS, 35 kontrol

| Konu | Kontrol edilen |
|---|---|
| Veri | Profil yalnız havuzda ayrışıyor; havuzda yalnız iki ödül değişik; varyantlar tek alanda ayrışıyor; diğer 24 ödül asset'inde erişim alanı 0; eski profiller eski asset'leri gösteriyor; Hasat Ritmi ve iki bedelli ödül aynı asset, aynı değer |
| Geometri | 6 yerleşim (orta, kenar, köşe; 1×1, 2×2, 2×3) × 4 yarıçap ve 4 erişim: oyunun hücre kümeleri bağımsız bir hesapla aynı; hiçbir hücre iki kez yok; tarlanın dışı yok; verideki ×1,666667 tam 2,00 hücrelik kümeyi veriyor |
| Artçı (sahnede) | Orta 1×1, köşe 1×1, kenar 2×2, orta 2×3: ilk patlama değişmedi; ikinci patlama her canlı bitkiye tam bir kez, aynı hasarla; yeni hücre sayıları 8 / 3 / 8 / 14; başka bir şey tetiklenmiyor. Eski ödül 1,50 hücrede 8 bitki |
| İkinci dalga (sahnede) | Erişim 0 / 3 / 4, köşe, 2×2: ilk dalga değişmedi; yakın hücreler dahil; her bitkiye bir kez; yalnız dört çapraz, sıçrama yok |
| Sınırlar | 11×11 tarlada deneme erişimi 6: 20 hedefin 20'si hasar aldı, 16 şimşek çizildi, 4'ü "çizilmedi" sayıldı. Görsel havuzu dolu: aynı karede 18 dalga, 288 vuruşun hepsi uygulandı, 6 dalganın yalnız görseli atlandı |
| Havuzdan dönen bitki | İş hücre tutar: gecikme sırasında alanda yeniden doğan bitkiye bir kez vurulur, alan dışında doğana vurulmaz |
| Zamanlama | Sabit 1/30 sn adımda, altı farklı mutlak oyun zamanında: Artçı hep 6 kare, ikinci dalga hep 5 kare sonra |
| Katsayılar | Davranışa Adanış ×1,50 ve Davranış Ustası ×1,25: ilk patlama 188 (bir kez); geniş artçının her hücresinde aynı 188 |
| Temizlik | Round gecikme sırasında biterse bekleyen artçı düşer ve hasar vermez; yeni sahnede bekleyen iş, sayaç ve ödül etkisi yok |
| Arayüz | İki varyantın ödül kartı: metin taşmıyor (`Docs/Bolum3-7-5/ekran/T375_01_ErisimKartlari.png`) |
| Eski profil | Kırılma V1'de Artçı ×1,25 (1,50 hücre), Çifte Akım ilk dalganın erişiminde |

### Eski testler ve ölçümler (aynı tur)

| İş | Sonuç |
|---|---|
| BedelliOdullerV1Verification | PASS · 93 |
| OdulAsamalariV1Verification | PASS · 71 |
| BossRewardVerification | PASS · 127 |
| KirilmaV1Verification | PASS · 189 |
| TakvimV1Verification | PASS · 162 |
| DengeV1Verification | PASS · 86 |
| HarvestBehaviorVerification | PASS · 114 |
| ElectricRevertVerification | PASS · 40 |
| SpecializationVerification | PASS · 100 |
| StartLoadoutVerification | PASS · 88 |
| MechanicsVerification | PASS · 88 |
| SimpleResonanceVerification | PASS · 293 |
| ContactRefactorVerification | PASS · 175 |
| RunPrototypeVerification | PASS · 69 |
| BossWeatherVerification | PASS · 22 |
| RoundPreviewVerification | PASS · 47 |
| Run50ReferenceVerification | PASS · 70 |
| OptionsMenuVerification | PASS · 22 satır |
| Ödül teklif dağılımı (`RewardOfferMeasurement`) | Çıktı 3.7.3'tekiyle bayt bayt aynı (md5 `222d6374…`): eski profillerin teklif dizisi değişmedi |
| Bedelli ödül ölçümü (`BedelliOdulMeasurement`) | Çıktı 3.7.4'tekiyle bayt bayt aynı (md5 `6d628d3f…`) |

Başarısız test yok.

### Bilerek değiştirilen beklentiler ve geliştirme sırasındaki hatalar

- `KirilmaV1Verification` ve `TakvimV1Verification`: profil listesi beklentisine yeni profil eklendi. Başka beklenti değişmedi.
- `BalanceRunMeasurement`: "kırılma ödülünü almayan eş" koşusunun aşamalı havuzda da çalışması için koruma gevşetildi; bedelli
  ödül çıkan teklifte bot bedelsiz olanı seçiyor.
- Yeni test ilk yazıldığında elektrik efekt havuzunu 8 varsayıyordu (sahnede 12); beklenti sahnedeki değerden okunuyor.
- Geometri raporunun ilk hâli derlenmedi (metin içinde satır sonu); düzeltildi, ölçümler ondan sonra alındı.

### Test turundan sonra değişen kod

Yalnız ölçüm aracı `BalanceRunMeasurement.cs` (süre sütunları kare sayısından; yukarıda, 1. bölüm). Oyun kodu ve testler
değişmedi. Araç aynı derlemede olduğu için üç test bu değişiklikten sonra yeniden çalıştırıldı (00:28–00:31):
`KirilmaErisimiV1Verification` PASS · 35, `BedelliOdullerV1Verification` PASS · 93, `KirilmaV1Verification` PASS · 189. Diğer
18 iş son kodla yeniden çalıştırılmadı. Kayıt: `Docs/Bolum3-7-5/testler/son-kod/`.

### Üreticiler ve dokunulmayan dosyalar

- Altı üreticinin `--check` çıktısı temiz (Denge V1, Kırılma V1, Takvim V1, Ödül Aşamaları V1, Bedelli Ödüller V1, Kırılma
  Erişimi V1); `test_generation_guard.py` 3 / 3.
- Seçili profil dosyası (`Assets/Resources/RunProfileSelection.asset`) değişmedi: 19:24'te senin seçtiğin hâliyle (md5
  `bad67bed…`). Oyuncu kaydı (`meta.json`) 1 Ekim 19:15'teki hâliyle (md5 `5bc3be8a…`). Açık Unity editörüne dokunulmadı.
- Commit yok; HEAD `28e4650`.

Ham kayıtlar: `Docs/Bolum3-7-5/testler/` (her işin sonuç dosyası ve `TopluSonuc.txt`), `Docs/Bolum3-7-5/olcum/ham/OlcumOzeti.txt`.

## 10. Profil ve ayar dosyaları

| Dosya | Ne | Kaynak |
|---|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_KirilmaErisimiV1.asset` | Profil | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/ArtciPatlama_E1.asset` | Artçı varyantı (çarpan ×1,667 = 2,00 hücre) | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/CifteAkim_E1.asset` | Çifte Akım varyantı (erişim 4) | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/BossRewardPool_KirilmaErisimiV1.asset` | Aşamalı havuz (18 ödül) | Üretici çıktısı |
| `Tools/Balance/KirilmaErisimiV1/kirilma_erisimi_v1_params.py` | **Tek düzenleme kaynağı** (iki erişim değeri) | Elle |
| `Tools/Balance/KirilmaErisimiV1/make_kirilma_erisimi_v1.py` | Üretici (`--check`, `--report`) | — |
| `Tools/Balance/KirilmaErisimiV1/analyze_lab.py` | Laboratuvar çözümleyicisi ve karar kuralı | — |
| `Tools/Balance/KirilmaErisimiV1/analyze_validation.py` | Birikimli run doğrulamasının çözümleyicisi | — |
| `Tools/Balance/KirilmaErisimiV1/generated_manifest.json`, `README.md` | Hash kaydı, veri sahipliği | Üretici / elle |

- **Profil**, Bedelli Ödüller V1'den üç satırda ayrılıyor: ad, zafer başlığı, ödül havuzu. Takvim, hedefler, round süresi, XP,
  ekonomi, bitki canı, ağaç, başlangıç seçenekleri, aşama ve teklif kuralları aynı.
- **Havuz**, Bedelli Ödüller V1'in havuzundan iki satırda ayrılıyor: Artçı ve Çifte Akım yerine bu profilin varyantları.
- **Varyant**, Kırılma V1'deki asset'in kopyasıdır ve tek alanda ayrışır (Artçı: `echoRadius`; Çifte Akım: `echoReach`). Üretici
  taban asset'i okuyup yalnız o alanı değiştirir; hasar ya da gecikme parametre dosyasından değiştirilemez.
- **Üretim koruması:** Inspector'da yapılan değişiklik üreticiyi durdurur (manifest farkı). Parametredeki taban yarıçap ya da
  taban erişim oyun kodundaki sabitle aynı değilse üretici yazmadan durur.
- Eski profiller, eski havuzlar ve eski ödül asset'leri değişmedi.

Oyun koduna eklenen ölçüm kancaları (oynanış bunlara bağlı değil): yankı işinin planlanması / uygulanması / düşmesi olayı,
uygulanmakta olan yankının kaynak saksısı, yankıların eriştiği hücre sayacı, çizilmeyen şimşek sayacı.

## 11. Kısa Play kontrolü (10–15 dakika)

1. Menü: **Tools → Run Profili → Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)**. Play.
2. Patlama kartlarına oyna; R23'ten sonra Artçı Patlama gelirse al (garanti değil).
3. Artçı vurduğunda ikinci patlamanın saksının iki hücre ötesindeki bitkilere de değdiğini gör. Kartta "yarıçap ×1,67" yazar.
4. Ayrı bir run'da elektrik kartlarına oyna; Çifte Akım'ı al. İkinci dalganın şimşekleri çaprazda dört hücreye uzamalı; ilk
   dalga iki hücrede kalmalı. Kartta "çaprazda 4 hücre" yazar.
5. Bakılacak his soruları: artçının genişlediği fark ediliyor mu; ekran kalabalıklaşıyor mu; Çifte Akım hâlâ "bir şey
   yapmıyor" gibi mi; Hasat Ritmi'nin yanında bu iki ödül seçilir görünüyor mu?
6. Eski hâlle karşılaştırmak için aynı menüden `Run50 Bedelli Ödüller V1`.

Seçili profilini ben değiştirmedim.

## 12. Açık kalanlar

- **İnsan Play onayı yok.** "Kırılma hissi" bu paketle kapanmadı; ölçüm yalnız hasat sayılarını gösterir.
- **Çifte Akım ×1,5 hedefini tutmadı; birikimli run'da başka bir ödülle aynı düzeyde.** Erişimi büyütmek sınırlı
  kazandırıyor (dört ışın, tarla kenarı). Daha fazlası için hasar, tetik ya da zincir gerekir; bunlar bu paketin yetkisi
  dışındaydı (P6). Havuzda kalıp kalmayacağı senin kararın.
- **Artçı, birikimli run'da başka bir ödüle göre ×1,35.** Laboratuvardaki ×1,6–×1,8 "hiç ödül almamaya" göreydi.
- **Birikimli doğrulamada 60 run'ın 60'ı kazandı.** Kota ve boss hedefleri bu güçte baskı kurmuyor (P8).
- **Artçı karma ve doğrudan ağırlıklı düzende hedefi tutmuyor** (×1,10–×1,23): patlayan saksı azken etkisi de az.
- **Laboratuvar sabit düzendir.** Statlar 3.6 run'larının medyanlarıdır; nadirlik, ağaç ve diğer ödüller yoktur. Bitki canı
  ve tarla doluluğu gerçek build'de farklıdır.
- **İkinci bir nişan politikası denenmedi** (davranışa uygun hedefleme).
- **Görseller gözle doğrulanmadı;** Artçının ana patlama görseli alanla birlikte büyümüyor. (Bölüm 3.7.5.1: ikinci darbeye
  gerçek alanın sınır çizgisi eklendi ve batch görüntüleriyle incelendi; insan Play onayı yok —
  [Bolum3-7-5-1-AlanGeriBildirimi.md](Bolum3-7-5-1-AlanGeriBildirimi.md).)
- **Denge:** Artçı 2,00 hücreyle patlama build'i güçlü sinerjide ×1,6–×1,8 hasat veriyor. Kota ve boss hedefleri buna göre
  ayarlanmadı (P8); Hasat Ritmi zayıflatılmadı.

## 13. Ana TODO'daki karşılığı

P5'te işaretlenenler: ölçüm ön koşulu (kök neden bulundu ve düzeltildi), Artçı yarıçap eşikleri, Çifte Akım'ın ölçülmesi ve
kararın sonuca bağlanması, aynı stat ve yerleşimde hasat / yeni hedef / boş darbe / skor kayıtları.

Açık bırakılan: "Hasat Ritmi … alan görseli ve hasat sayacı doğrulansın". Hasat Ritmi karşılaştırma noktası olarak kaldı ve
değerleri değişmedi; alan görseline ve sayacına bu pakette bakılmadı.

P5'in kabul cümlesi ("ödül neden güçlü / zayıf açıklanabilir") ölçümle karşılandı; insan Play onayı yok. P6'ya geçilmedi.
