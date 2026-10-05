# Bölüm 3.7.8 — ölçümden önce yazılan tanımlar

Yazıldığı an: 4 Ekim 2026, araç denemesinden (`k378smoke`, 2 kısa run) sonra, başlangıç ölçümünden **önce**. Sonuçlara göre
değiştirilmez; değişen ya da eklenen her şey bu dosyanın sonuna tarihli ek olarak yazılır.

Buradaki her politika bir **bot politikasıdır**; insan davranışı değildir. Bot her vuruşta nişanını yeniden hesaplar, menüde
zaman harcamaz, yorulmaz.

## 1. Build politikaları

| Yol | Bot politikası | Ödül tercihi | Not |
|---|---|---|---|
| Doğrudan hasar + alan | `Hasar-alan` | Keskin Bıçak, Geniş Savuruş, Canavar Kesimi, Hızlı Bilek … | XP düğümleri ağaçtaki vadesinde |
| Patlama + Artçı Patlama | `Patlama yolu` | Artçı Patlama, Yıkım Gücü, Kıvılcım … | Zincir Hasat'ı almaz (başka seçenek varken) |
| Elektrik + Çifte Akım | `Elektrik yolu` | Çifte Akım, Yıkım Gücü, Kıvılcım … | Zincir Hasat'ı almaz (başka seçenek varken) |
| Karma davranış + rezonans + zincir | `Davranış yönü` | Zincir Hasat, Kıvılcım, Yıkım Gücü, Artçı, Çifte Akım … | XP düğümleri ağaçtaki vadesinde |

Karşılaştırma politikaları:

| Rol | Bot politikası | Tanım |
|---|---|---|
| Ortalama | `Dengeli` | Ağaçta her düğümü yazılı vadesinde alır, kartta yalnız nadirliğe bakar, ödülde sabit genel sıra. Uzmanlaşmaz. |
| Düşük uyumlu | `Uyumsuz` | Davranış tile'larını ve rezonans yerleşimini seçer (Davranış-rezonans'ın ağacı ve kartları) ama ödülde davranışını beslemeyen kritik / doğrudan vuruş ödüllerini öne alır; kritik düğümlerini de geç alır. Kırılma ödülü ve zincir sırasında yok. |
| Acemi | `Yeni` | Rastgele alım (alabildiği en ucuz şey, her fırsatta değil), rastgele nişan, rastgele kart ve ödül. |

Başlangıç: ana karşılaştırmada hepsi Bahçıvan + Standart tırpan. Alternatif çiftçi / tırpanlar ayrı, hedefli bir sette.

## 2. Nişan politikaları

1. **En kalabalık hedef** (mevcut): saldırı alanına en çok canlı bitki giren nokta (hücre merkezleri ve ara noktalar). Eşitlikte en
   uzun süredir kullanılmayan nokta.
2. **Davranış öncelikli hedef** (yeni): aynı arama, ama bitkinin değeri saksısının davranış şansıyla büyür:
   `değer = 1 + 2 × min(1, patlama + elektrik + kasırga + bumerang şansı)`. Davranışsız saksının bitkisi 1, şansı toplam %50 olan
   saksınınki 2, %100 ve üstü 3 sayılır. Şanslar round başında okunur.

İkinci politika iki temsilci build'de (Patlama yolu, Davranış yönü) × 5 seed ölçülür.

## 3. Ölçüler

- **Gerçek hasat / sn** = round'da hasat edilen bitki ÷ round süresi. **Skor / sn** aynı biçimde.
- **Kota payı** = dönemde kazanılan skor ÷ dönem kotası. **Boss payı** = boss round'unun skoru ÷ boss hedefi.
- **Gereksinim hızı** = kota ÷ dönemin aktif saniyesi (skor / sn). Grafik bu birimle çizilir.
- **Vuruş başına hasat** = hasat ÷ doğrudan saldırı sayısı (davranış hasatları dahil); ayrıca vuruş başına isabet eden hedef.
- **Öldürme vuruş sayısı** = doğrudan vuruşla ölen bitkinin aldığı doğrudan vuruş sayısı, nadirliğe göre.
- **Davranış payı / zincir payı** = hasadın davranışla (ve zincirle) yapılan kısmı.
- **Tarla doluluğu** = üretim noktalarının bitki taşıyan oranı (kare ortalaması). **Bekleme süresi** = hasat edilen bitkinin
  tarlada durduğu süre (yalnız aktif round kareleri).
- **Kendi gelişimi** = build'in gerçek hasat / sn ve skor / sn değerinin kendi önceki dönemine oranı.

Bütün karşılaştırmalar saniye başına ya da hedefe oranla yapılır; round'un uzun olması tek başına "güçlendi" sayılmaz.

## 4. "Belirgin üstünlük penceresi"

R30–45 aralığına düşen altı dönem: 27–30, 31–33, 34–36, 37–40, 41–43, 44–46.

- Bir run'da **pencere var**: bu altı dönemden **art arda en az ikisinde** kota payı ≥ 2,0 **ve** boss payı ≥ 2,0.
- Bir yol **aday** sayılır: 10 seed'in en az 7'sinde pencere var.
- **Yalnız R50'de açılan** (47–50 döneminde ilk kez payı ≥ 2,0 olan) run pencere sayılmaz.
- Üstünlüğün "kotalar çok düşük" olmasından gelmediğinin ölçüsü: aynı altı dönemde **ortalama politikanın (Dengeli) medyan kota
  payı 2,0'ın altında** olmalı; acemi ve düşük uyumlu politikalar daha düşük pay ya da kayıp göstermeli.

2,0 bir tasarım eşiğidir ("gereksinimin iki katı"); insanın bunu "kopma" diye hissedip hissetmeyeceği bot ölçümüyle söylenemez.

## 5. Hedef tablosunun kurulma kuralı (ayar turlarında)

- Kota ve boss hedefi oynanışı değiştirmez (yalnız geçti / kaldı eşiği). Bu yüzden ayar turlarında run'lar **hedefsiz**
  (bütün hedefler 1) oynanır ve aday tablolar aynı run'ların skorundan hesapla değerlendirilir. Son doğrulama gerçek tabloyla oynanır.
- **Kural:** dönem kotası, ortalama politikanın (Dengeli) o dönemdeki medyan skorunun **%65'i** (Dengeli'nin payı ≈ 1,5) olacak
  biçimde kurulur; boss hedefi Dengeli'nin boss round'u medyan skorunun **%60'ı**. Tablo dönemden döneme yumuşatılır (art arda iki
  dönemin saniye başına gereksinimi azalmaz; tek dönemlik sıçrama komşularıyla geometrik ortalamaya çekilir) ve okunur sayıya
  yuvarlanır. Tablo güçlü build'lerin skoruna göre kurulmaz: oyuncunun güç sıçraması sonraki hedefle geri alınmaz.
- **İlk iki boss (R3, R6):** "yalnız saksı", "önce hasar", "önce hız", "önce ekonomi", "önce üretim" açılışlarının hepsi kota ve
  boss hedefini geçmeli (tek zorunlu açılış olmamalı). "Hiçbir şey almayan" açılış kalabilir.
- Kural tutmazsa (ör. Dengeli'ye göre kurulan tablo acemiyi de rahat geçiriyorsa ya da hiçbir yol pencere göstermiyorsa) tablo
  elle "düzeltilmez": sonuç açık bırakılır ve yazılır.

## 6. Can eğrisinin ayar ilkesi

Başlangıç ölçümünden sonra, 1. ayar turundan önce bu dosyaya ek olarak yazılacak (ölçülen öldürme vuruş sayılarına bağlı).
Şimdiden sabit olan sınırlar: eğri round tabanlı ve azalmayan; tek round'da en çok ×1,10 artış (tam sayıya yuvarlama dahil
×1,25); nadirlik çarpanları (1 / 1,5 / 2 / 2,5 / 3) değişmez.

## 7. Zincir ayrıştırması

**Kontrollü karşılaştırma (laboratuvar; normal run değildir):** zinciri almayan gerçek bir run'ın R30 ve R40 başındaki durumu
(tarla, tile'lar, saksılar, statlar, ödüller) yeni sahnede aynen kurulur ve o round oynanır: A = olduğu gibi (zincir yok), B =
aynı durum + Zincir Hasat. Fark yalnız zincirdir (yerine başka ödül verilmez). Her kol 3 zar akışıyla. B kolları:

| Kol | Şans (nesil 1 / 2) | Hasar (nesil 1 / 2) |
|---|---|---|
| mevcut | 0,75 / 0,50 | 0,75 / 0,50 |
| şans üst | 1,00 / 0,75 | 0,75 / 0,50 |
| hasar üst | 0,75 / 0,50 | 1,00 / 0,75 |
| ikisi üst | 1,00 / 0,75 | 1,00 / 0,75 |

**Gerçek fırsat maliyeti:** aynı seed'de zinciri alan run ile teklif edildiğinde almayan (aynı boss'ta başka uygun ödülü alan) eş run.

Soru → ölçü:

1. *Zincir, sıradaki doğrudan vuruşun zaten öldüreceği bitkileri mi öldürüyor?* → (a) A / B kollarında doğrudan hasat sayısı;
   (b) zincirin öldürdüğü her bitki için: o bitkiler yerinde dursaydı botun seçeceği **sıradaki** doğrudan saldırı ona erişir
   miydi, en düşük doğrudan hasarıyla öldürür müydü (ilk mertebe karşı-olgusal; sonraki saldırılar hesaba katılmaz).
2. *Yeni hücrelere ne kadar erişiyor?* → zincir vuruşlarından, kökün doğrudan vuruşunun ve normal tetiğinin dokunmadığı hücrelere
   düşenlerin payı.
3. *Üretim noktasını ne kadar erken boşaltıyor?* → hasat edilen bitkinin bekleme süresi (A / B) ve tarla doluluğu.
4. *Havuz, kuyruk, nesil sınırı ne kadar kesiyor?* → havuz beklemesi (iş, kare), kuyruk gecikmesi, round sonunda düşen iş, nesil
   sınırına takılan ölüm sayısı, kök bütçesine takılan deneme.
5. *Şans ve hasar zayıflaması zinciri öldürüyor mu?* → nesil başına deneme → başarılı tetik oranı; zincir vuruşu başına hasat;
   "şans üst" / "hasar üst" kollarının ayrı etkisi.
6. *Alternatif ödül aynı ölçüde güçlü mü?* → laboratuvar etkisi (alternatif yok) ile gerçek eş run farkının karşılaştırması.

Karar kuralı (zincir çarpanları): izinli aralıkta bir üst kol, laboratuvarda **mevcut kola göre** medyan toplam hasadı en az
×1,05 artırıyorsa ve bu artış ölçülen darboğazla (şans ya da hasar) tutarlıysa aday olur; değilse çarpanlar değişmez.

## 8. Ayar turları

Başlangıç ölçümü (yalnız süre tablosu; diğer her şey Run50_XPV1 ile aynı) → en çok iki gerekçeli tur → son aday doğrulaması.
Her turda yalnız küçük pilot set; tam set (dört build × 10 seed ve diğerleri) son değerlerle bir kez.

---

## Ek 1 — 4 Ekim 2026, başlangıç ölçümünün dört build'i (16 run) görüldükten sonra, 1. ayar turundan önce

### Hedef kuralının tam formülü (5. bölümün uygulaması; Dengeli run'ları henüz görülmeden yazıldı)

`gereksinim[d] = pay × medyan(Dengeli skoru[d]) ÷ aktif saniye[d]` (kota için pay 0,65 ve dönem skoru; boss için pay 0,60 ve boss
round'unun skoru). Yumuşatma: logaritma uzayında 1-2-1 süzgeci (ilk ve son dönem aynen), ardından azalmayan hâle getirme
(`gereksinim[d] = max(gereksinim[d], gereksinim[d−1])`). Tablo değeri: `gereksinim[d] × aktif saniye[d]`, oyunun okunur sayı
yuvarlamasıyla (100'ün altında 5'in katı, üstünde iki anlamlı basamak). Kod: `Tools/Balance/AlphaDengeV1/analyze_alpha.py`
(`rule_tables`).

### Başlangıç ölçümünün can eğrisiyle ilgili bulgusu

Süre tablosuyla (başka hiçbir değer değişmeden) dört build'in dördünde de Common bitki R20'den itibaren, Rare R25–35'ten
itibaren, Legendary R35–40'tan itibaren doğrudan vuruşla tek vuruşta ölüyor (öldürme vuruş sayısı medyanı 1,00). Doğrudan vuruş
hasarı (taban × ödül çarpanı) R20 / R30 / R40 / R50'de 50–89 / 118–197 / 216–470 / 474–650 (temel güç evresine giren run'larda
çok daha yüksek); Common canı 49 / 79 / 132 / 215. Hasarın değeri R20'den sonra kalmıyor.

### 1. ayar turu: can eğrisi adayları ve seçim kuralı

R1–10 çapaları değişmez (20 / 25 / 31): ilk üç boss'un koşulları aynı kalır. Adaylar (Common canı):

| Aday | R15 | R20 | R25 | R30 | R35 | R40 | R45 | R50 |
|---|---|---|---|---|---|---|---|---|
| taban (Denge V1) | 39 | 49 | 62 | 79 | 102 | 132 | 170 | 215 |
| HA | 43 | 62 | 88 | 125 | 176 | 247 | 345 | 480 |
| HB | 46 | 73 | 112 | 170 | 255 | 380 | 560 | 820 |
| HC | 49 | 78 | 125 | 200 | 310 | 475 | 720 | 1080 |

Üçü de round başına en çok ×1,10 büyür; nadirlik çarpanları aynıdır. Her aday dört politikayla (Hasar-alan, Patlama yolu,
Davranış yönü, Dengeli) × 2 seed (100, 101), hedefsiz oynanır; kontrol başlangıç ölçümünün aynı seed'leridir.

Seçim kuralı (ölçümden önce):

1. **Doğrudan hasar build'i (Hasar-alan):** Common öldürme vuruş sayısı medyanı R30, R40 ve R50'de ≤ 1,25 (hasara yatıran yol
   Common'ı teklemeye devam etmeli); Rare öldürme vuruş sayısı aynı üç round'un **en az ikisinde** 1,3–2,5 aralığında (hasar hâlâ
   işe yarıyor, duvar da yok).
2. **Ortalama politika (Dengeli):** Common öldürme vuruş sayısı aynı üç round'un en az ikisinde 1,2–2,5; hiçbir nadirlikte 5'i
   aşmıyor.
3. İki koşulu da sağlayan adaylardan **en düşük** olan seçilir (en az değişiklik). Hiçbiri sağlamazsa koşullara en yakın aday
   seçilir ve sağlanmayan koşul açık yazılır; ara bir eğri uydurulmaz (bu, 2. tura kalır).

Davranış build'lerinin (Patlama yolu, Davranış yönü) eğriden nasıl etkilendiği seçim kuralına girmez ama raporlanır
(alan / hız dışındaki yolların farkı).

XP tablosu bu turda değişmez: daha dik can eğrisinin hasadı ve XP'yi ne kadar düşürdüğü önce ölçülür.

---

## Ek 2 — 4 Ekim 2026, başlangıç ölçümünün tamamı (30 run) görüldükten sonra; 1. ayar turu çalışırken, sonuçları okunmadan

### Düzeltme: hedef kuralının dayanak politikası değişti (ölçümden sonra yapılan bir değişikliktir)

5. bölümdeki kural, kotayı "ortalama" diye adlandırdığım `Dengeli` politikasının skoruna bağlıyordu. Başlangıç ölçümü bu
adlandırmanın yanlış olduğunu gösterdi: **`Dengeli` ortalama değil, ölçülen en güçlü politika.** Her düğümü vadesinde alıp hız ve
alan ödüllerini topladığı için R50'de skor hızı 5 485 / sn; dört build yolunun medyanları 1 348–2 758 / sn, düşük uyumlu politika
667 / sn (R30'da 470 ↔ 249–394 ↔ 214). Kural yazıldığı gibi uygulansaydı kota en güçlü politikanın %65'ine bağlanır, istenen dört
build yolunun ikisi (Patlama, Elektrik) R30'da, hepsi R50'de kotanın altında kalırdı.

Bu yüzden kural, ayar turlarının sonuçları görülmeden önce şöyle düzeltildi (sayılar aynı, yalnız dayanak politika farklı):

- **Dayanak: düşük uyumlu politika (`Uyumsuz`).** Kota = `Uyumsuz`'un dönem medyan skoru × 0,65 (payı ≈ 1,5); boss hedefi =
  `Uyumsuz`'un boss round'u medyan skoru × 0,60. Yumuşatma, azalmayan gereksinim ve yuvarlama Ek 1'deki formülle aynı.
- Zorluk eğrisi böylece "davranışını beslemeyen ödüller seçen ama oyunu oynayan" bir build'in rahat geçtiği düzeydir; build
  yollarının üstünlüğü ona göre ölçülür. Tablo yine güçlü build'lerin skoruna göre kurulmaz.
- 4. bölümdeki pencere tanımı aynen kalır. "Kotalar çok düşük değil" ölçüsü şuna döner: aynı altı dönemde **düşük uyumlu
  politikanın medyan kota payı 2,0'ın altında** (kural gereği ≈ 1,5) ve en zayıf seed'i 1'e yakın ya da altında; acemi politika
  kaybediyor.
- `Dengeli` artık "genelci (hız + alan)" diye raporlanır; ayrı bir "ortalama oyuncu" botu yoktur. Ortalama bir build'in
  geçebilirliği, yolların **en zayıf seed'lerinin** payıyla (dönem başına en düşük pay) okunur.
- Bot her vuruşta en iyi nişanı bulur; insanın aynı build'le daha düşük skor yapması beklenir. %65 payı bu yüzden değiştirilmedi
  (insan için güvenlik payı); doğru pay insan Play testiyle belirlenir.

Bu, ölçüm sonucuna bakılarak yapılan bir kural değişikliğidir ve belgede öyle anılır. 5. bölümün "kural tutmazsa tablo elle
düzeltilmez" hükmü yeni kural için geçerlidir.

### Başlangıç ölçümünün diğer bulguları (ayar turlarının gerekçesi)

- Süre tablosu tek başına ilerlemeyi belirgin hızlandırıyor: Hasar-alan'da R50 level'ı 102 → 140, skor hızı R20–R50'de ×2,1–3,9.
- Temel güç evresine (bütün tile'lar Sv 3) artık XP yatırımı olmadan da R37–45'te giriliyor; hasar kartları çarpımsal biriktiği
  için son 10 round'da hasar ve hasat hızla büyüyor (Davranış yönü R50: hasat 101 / sn, taban hasar 2 822).
- Acemi politika (rastgele nişan, vuruş başına 1 hedef) 50 round'da toplam 1 500–4 000 skor yapıyor.

---

## Ek 3 — 4 Ekim 2026, açılış seti (27 kısa run, R1–10) görüldükten sonra; 1. ayar turu sonuçları okunmadan

Açılış setinde (başlangıç can eğrisi; R1–10 çapaları bütün adaylarda aynı) beş "makul açılış"ın (yalnız saksı, önce hasar, önce
hız, önce ekonomi, önce üretim) × 3 seed en düşük skorları: R1–3 dönemi 48, R3 boss round'u 12; R4–6 dönemi 92, R6 boss
round'u 33; R7–10 dönemi 232, R10 boss round'u 48.

5. bölümdeki "ilk iki boss" kuralının uygulaması ve bir genişletme:

- **Tavan:** ilk üç dönemin (R3, R6, **R10**) kotası ve boss hedefi, makul açılışların en düşük skorunun %80'ini aşamaz
  (kuraldan çıkan değer daha düşükse o kalır). Sayılar: kota ≤ 38 / 74 / 186, boss hedefi ≤ 10 / 26 / 38 (okunur sayıya
  yuvarlanmadan önce; yuvarlama en düşük skoru aşamaz).
- **R10 neden eklendi (ölçümden sonra yapılan değişiklik):** kuralın tavansız R10 değerleriyle (kota ≈ 310, boss ≈ 60) seed
  101'de beş açılıştan yalnız "önce hız" geçiyordu; bu, kaçınılması istenen "tek zorunlu açılış" durumudur.
- "Hiçbir şey almayan" açılış skor yapmıyor (0) ve R3'te kalıyor; acemi politikanın ilk üç dönem skoru 36–66 / 45–77 / 72–122.

Açılış botu ilk alımlardan sonra genelci politikayla ve kusursuz nişanla oynar; insanın payı daha düşük olur (Play testi).

---

## Ek 4 — 4 Ekim 2026, 1. ayar turu (24 run) görüldükten sonra, 2. ayar turundan önce

### 1. turun sonucu ve kendi kuralımın hatası

Ek 1'deki seçim kuralı uygulandı (2 seed medyanları, R30 / R40 / R50):

| Aday | Hasar-alan Common ÖV | Hasar-alan Rare ÖV | Dengeli Common ÖV | Kural |
|---|---|---|---|---|
| HA | 1,01 / 1,02 / 1,00 | 1,14 / 1,08 / 1,02 | 1,19 / 1,10 / 1,01 | iki koşul da tutmuyor |
| HB | 1,07 / 1,02 / 1,04 | 1,25 / 1,19 / 1,10 | 1,62 / 1,53 / 1,10 | yalnız 2. koşul |
| HC | 1,23 / 1,19 / 1,08 | 1,62 / 1,70 / 1,33 | 1,68 / 1,89 / 1,27 | ikisi de tutuyor |

**Kural HC'yi seçiyor. HC uygulanmadı; gerekçesi ölçümdür ve bu, kuralı yazan uygulayıcının hatasıdır:** kural yalnız "hasar
işe yarasın" şartını ölçüyordu, P8'in asıl hedefini ("başarılı build R50'den önce zorluğu belirgin aşabilsin, kurduğu gücü
birkaç round kullanabilsin") ölçmüyordu. 1. tur, daha dik eğrilerde hiçbir build'in zorluğu aşamadığını gösterdi:

| | R30 → R40 → R50 hasat / sn | R40'ta davranış vuruşunun öldürme oranı |
|---|---|---|
| Davranış yönü · taban | 14,0 → 55,9 → 140,9 | %96 |
| Davranış yönü · HA | 10,0 → 26,2 → 55,2 | %73 |
| Davranış yönü · HB | 6,7 → 9,9 → 10,8 | %33 |
| Davranış yönü · HC | 6,4 → 7,2 → 7,3 | %23 |
| Hasar-alan · HA / HB / HC (R50) | 28,4 / 28,0 / 17,5 | |
| Patlama yolu · HA / HB / HC (R50) | 46,1 / 20,5 / 15,3 | |

HB ve HC'de davranışların hasarı (taban hasar × Yıkım Gücü; doğrudan vuruşun ödül çarpanları ve kritiği davranışa geçmez) bitkiyi
öldürmeye yetmiyor; karma davranış yolu R40'tan sonra büyümüyor, tarla %64–80 dolu bekliyor. HC'de doğrudan hasar yolunun
hasadı da R50'de 17,5 / sn'de kalıyor: oyunun bütününde "öldüremiyorum" hâli sürüyor, kırılma yok.

### 2. ayar turu: can eğrisi yeniden seçilir (HA ↔ HM)

Adaylar: **HA** (1. turdaki) ve **HM** = HA ile HB'nin orta eğrisi:

| Aday | R15 | R20 | R25 | R30 | R35 | R40 | R45 | R50 |
|---|---|---|---|---|---|---|---|---|
| HA | 43 | 62 | 88 | 125 | 176 | 247 | 345 | 480 |
| HM | 44 | 67 | 99 | 146 | 212 | 306 | 440 | 627 |

Her kol: Uyumsuz × 5, dört build yolu ve genelci × 3 seed (100–102), zinciri almayan eş × 3 + zincir laboratuvarı (R30, R40).
Hedefsiz. HA kolunda 1. turun seed 100–101 run'ları yeniden kullanılır.

Seçim kuralı (P8 hedefine göre; ölçümden önce):

1. **Erken tek vuruş yok:** beş politikanın (dört yol + genelci) en az dördünde R20'de Common ÖV ≥ 1,10 ve R30'da Rare ÖV ≥ 1,10.
2. **Kırılma mümkün:** dört build yolunun en az üçünde hasat / sn R30 → R40 arasında en az ×1,5, R40 → R50 arasında en az ×1,3
   büyür (build'in kendi gelişimi sürüyor).
3. İki koşulu da sağlayan adaylardan **daha dik olan (HM)** seçilir (hasarın değeri daha uzun sürer). Yalnız HA sağlıyorsa HA.
   İkisi de sağlamıyorsa HA seçilir ve tutmayan koşul açık yazılır. Üçüncü bir eğri denenmez (ayar turu hakkı biter).

Seçilen kolun Uyumsuz run'larından hedef tablosu (Ek 2 ve Ek 3 kuralı) hesaplanır; zincir çarpanı kararı (7. bölüm) seçilen kolun
laboratuvarından verilir: bir üst kol, **her iki round'da da** eşleştirilmiş medyanla "mevcut" kola göre toplam hasadı en az
×1,05 artırıyorsa alınır; tek çarpanlı kol (şans ya da hasar) yetiyorsa o, yetmiyorsa "ikisi üst". XP tablosu ve diğer ödül
değerleri bu turda da değişmez (gerekçesi ölçülmüş bir sorun çıkmadıkça).

---

## Ek 5 — 4 Ekim 2026, 2. ayar turu (38 run + laboratuvar) görüldükten sonra, son doğrulamadan önce

Ek 4'teki kuralın uygulaması (3 seed medyanları; Uyumsuz 5 seed):

| Koşul | HA | HM |
|---|---|---|
| 1 · R20 Common ÖV ≥ 1,10 (beş politikadan en az dördü) | 3 / 5 (Hasar-alan 1,00 · Patlama 1,08) — **tutmadı** | 4 / 5 (Hasar-alan 1,00) — tuttu |
| 1 · R30 Rare ÖV ≥ 1,10 (en az dördü) | 4 / 5 | 4 / 5 |
| 2 · hasat / sn R30 → R40 ≥ ×1,5 ve R40 → R50 ≥ ×1,3 (dört yoldan en az üçü) | 3 / 4 (Hasar-alan ×1,55 / ×1,08) — tuttu | 3 / 4 (Patlama ×1,45 / ×1,65) — tuttu |

**Seçilen eğri: HM** (iki koşulu da sağlayan tek aday). Son değerler: Common canı R15 44 · R20 67 · R25 99 · R30 146 · R35 212 ·
R40 306 · R45 440 · R50 627.

**Hedef tablosu** (HM kolunun Uyumsuz × 5 run'ından, Ek 2 ve Ek 3 kuralıyla):
kota 40 / 75 / 190 / 500 / 1 200 / 3 900 / 5 200 / 7 100 / 13 000 / 16 000 / 22 000 / 38 000 / 37 000 / 48 000 / 68 000;
boss 10 / 25 / 40 / 150 / 430 / 1 000 / 1 700 / 2 300 / 3 000 / 4 300 / 6 500 / 8 400 / 11 000 / 13 000 / 13 000.

**Zincir çarpanları değişmedi.** HM kolunda laboratuvar, "mevcut" kola göre eşleştirilmiş medyan toplam hasat: "şans üst"
×1,047 (R30) / ×1,024 (R40); "hasar üst" ×1,040 / ×1,020; "ikisi üst" ×1,087 / ×1,040. Kural her iki round'da ≥ ×1,05 istiyordu;
hiçbir kol sağlamadı.

**XP tablosu ve diğer ödül değerleri değişmedi.**

İki ayar turu hakkı kullanıldı. Son doğrulama bu değerlerle ve gerçek hedeflerle oynanır; sonuç ne olursa olsun bu pakette
başka ayar yapılmaz. 4. bölümdeki pencere tanımı ve Ek 2'deki "kotalar çok düşük değil" ölçüsü son veriye olduğu gibi uygulanır.
Not: Ek 2'deki ölçü, hedefsiz (bütün run'ların sonuna kadar oynandığı) veride yazılmıştı; gerçek hedefli son ölçümde elenen
run'ların sonraki dönemleri olmadığı için düşük uyumlu politikanın payı **yalnız ayakta kalan run'lardan** okunur ve yukarı kayar
(2. turun HM kolunda: bütün run'lar ≈ 1,5; ayakta kalanlar 2,05).
