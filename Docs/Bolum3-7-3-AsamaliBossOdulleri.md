# Bölüm 3.7.3 — Aşamalı boss ödül havuzları

**Durum (2026-10-02):** P3'ün ana run için oynanabilir uygulaması yapıldı ve izole kopyada doğrulandı. Yeni aday profil
`Run50_OdulAsamalariV1` çalışıyor. Bu bir **ilk içerik dağılımıdır**: ödüllerin etkisi ve katsayısı değişmedi, güçleri
ölçülmedi, insan Play kontrolü yapılmadı. Commit atılmadı; seçili profile ve oyuncu kaydına ben dokunmadım (seçim bu sırada
açık editörde `Run50_TakvimV1` yapılmış; 5. bölüm).

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md) · Önceki paket: [Bolum3-7-2-BossTakvimi.md](Bolum3-7-2-BossTakvimi.md)

## Kısa sonuç

- **`Run50_OdulAsamalariV1`:** Takvim V1'in her şeyi aynı; yalnız ödül havuzu farklı. Mevcut 16 ödül üç aşamaya dağıldı:
  erken (R1–11), orta (R12–21), güçlü (R22–50).
- **Teklif kuralı:** en çok 3 farklı kart, biri alınır. Açılmış bütün aşamalar adaydır; mevcut aşamada uygun aday varsa bir
  slot ona ayrılır, kalan slotlarda alt aşamaların ağırlığı düşer (×0,60, ×0,35).
- **Sunulma zamanı tek yerde:** aşamanın ilk round'u. Yeni havuzda ödüllerin eski `minRound` / `maxRound` alanları okunmuyor.
- **Eski profiller aynı:** eski havuzların teklif dizisi, kod değişmeden önce kaydedilen diziyle kart kart aynı (6.480 teklif).
- **Ödül ekranı:** "ERKEN / ORTA / GÜÇLÜ ÖDÜLLER" başlığı, her kartın üstünde sınıf etiketi. Hasat Ritmi kartındaki taşma
  düzeltildi.
- **Ölçümde öne çıkan:** güçlü aşamanın stack'i 9 güçlü boss'a yetmiyor; R43'ten sonra bazı tekliflerde hiç güçlü kart
  kalmıyor (4. bölüm). Teklif yine de hep 3 kart; boş ya da eksik teklif çıkmadı.
- **Endless:** yalnız boş bir veri alanı var. İçerik ve oynanış yok (P10 / P11 açık).

## 1. Uygulanan havuz ve ağırlık kuralları

| Kural | Uygulama |
|---|---|
| Kart sayısı | En çok 3 farklı kart; oyuncu 1 ödül alır. Level başına seçim hakkıyla bağlantısı yok |
| Adaylar | Boss round'unun aşaması ve daha önce açılmış aşamalardaki, o an uygun olan ödüller |
| Ayrılan slot | Mevcut aşamada uygun aday varsa biri ödülün kendi ağırlığıyla seçilir ve bir slot ona ayrılır |
| Kalan slotlar | Kalan bütün adaylardan, ağırlık × aşama uzaklığı çarpanı: mevcut ×1, bir önceki ×0,60, iki önceki ×0,35 |
| Ekrandaki yer | Ayrılan kartın yeri rastgele (400 teklifte ilk kart 278, üçüncü kart 275 kez güçlüydü) |
| Kırılma garantisi | Yeni profilde yok. Yerini "mevcut aşamadan bir aday" aldı; belirli bir build'e uygun ödül garanti edilmiyor |
| Mevcut aşama tükenince | Alt aşamalardan doldurulur |
| 2 / 1 / 0 aday | 2 ya da 1 kart gösterilir; hiç aday yoksa "Uygun ödül kalmadı" ve DEVAM. Aynı ödül iki kartta gösterilmez |
| Uygunluk | Mevcut kurallar aynen: stack sınırı, davranışın / saksının varlığı, stat tavanı, tarla doluluğu, etkinin uygulanabilirliği |
| Hazırlanma zamanı | Kota ve boss başarısı → bekleyen level kartları → varsa uzmanlaşma → teklif hazırlanır ve sabitlenir |
| Rastgelelik | Run'ın boss seed'i + dönem numarasından türeyen ayrı zar; oyunun zar akışına dokunmaz |

Çarpanlar ve "slot ayır" seçeneği havuz verisidir (`stageDistanceWeights`, `reserveCurrentStageSlot`).

**Eski havuzlar (Kırılma V1, Takvim V1, Denge V1, Boss Prototip)** eski kuralla çalışmaya devam ediyor: düz liste, kırılma
slotu, ödülün kendi `minRound` / `maxRound` alanı.

## 2. Mevcut 16 ödülün aşama tablosu

Etkiler, katsayılar, stack sınırları ve ağırlıklar değişmedi: havuz aynı ödül asset'lerine referans veriyor.

| Aşama | Round'lar | Boss'lar | Ödül | En çok adet | Koşul |
|---|---|---|---|---|---|
| Erken | 1–11 | R3, R6, R10 | Keskin Bıçak | 3 | — |
| | | | Hızlı Bilek | 3 | Saldırı aralığı tabanda değilse |
| | | | Bereketli Toprak | 3 | Tarla boşalıyorsa (son round doluluğu %65'in altında) |
| | | | Nadir Tohum | 3 | Nadirlik bonusu tavanda değilse |
| | | | Bilgi Filizi | 2 | — |
| Orta | 12–21 | R13, R16, R20 | Kıvılcım | 3 | Davranış şansı olan saksı varsa ve şans %100 değilse |
| | | | Ağır Darbe | 2 | — |
| | | | Kritik Göz | 3 | — |
| | | | Altın Hedef | 2 | — |
| | | | Yıkım Gücü | 3 | Davranışı olan saksı varsa |
| Güçlü | 22–50 | R23, R26, R30, R33, R36, R40, R43, R46, R50 | Geniş Savuruş | 2 | — |
| | | | Canavar Kesimi | 1 | — |
| | | | Fırtına Bileği | 1 | Saldırı aralığı tabanda değilse |
| | | | Artçı Patlama | 1 | Patlama şansı olan saksı varsa |
| | | | Çifte Akım | 1 | Elektrik şansı olan saksı varsa |
| | | | Hasat Ritmi | 1 | — |

**"Güçlü" bir sınıf adıdır.** Ödülün ölçümde güçlü çıktığı anlamına gelmez. Çifte Akım'ın etkisizliği ve Artçı Patlama'nın
zayıflığı (Bölüm 3.6) duruyor; katsayılarına dokunulmadı, P5 açık.

Aşama sınıfı tile ya da bitki nadirliği değildir; ayrı bir veri alanıdır ve ayrı renklerle gösterilir.

## 3. Eski ve yeni profilde sunulma zamanı

| Ödül | Kırılma V1 (boss'lar R5, 10 … 45) | Takvim V1 (15 boss, eski havuz) | Ödül Aşamaları V1 |
|---|---|---|---|
| Keskin Bıçak, Hızlı Bilek, Bereketli Toprak, Nadir Tohum | İlk boss'tan (R5) | İlk boss'tan (R3) | R3'ten (erken) |
| Bilgi Filizi | R5–R30 | R3–R30 | **R3–R50** |
| Kıvılcım, Ağır Darbe, Kritik Göz, Altın Hedef, Yıkım Gücü | R5'ten | R3'ten | **R13'ten** (orta) |
| Geniş Savuruş | R5'ten | R3'ten | **R23'ten** (güçlü) |
| Canavar Kesimi, Fırtına Bileği | R25'ten | R26'dan (R25'te boss yok) | **R23'ten** |
| Artçı Patlama, Çifte Akım, Hasat Ritmi | R10'dan; uygun olan varsa bir slot garantili | R10'dan; aynı garanti | **R23'ten**; ayrı garanti yok |

**Kart açıklamaları.** Üç ödülün asset'indeki açıklama eski sunulma zamanını anlatıyor. Asset'lere dokunmadım; yeni havuz bu
üç ödül için kendi metnini taşıyor (metinleri ben yazdım; `NOTES`):

| Ödül | Eski profilde (asset metni) | Yeni profilde |
|---|---|---|
| Canavar Kesimi | Yalnız kendi vuruşun. Round 25 boss'undan sonra çıkar; bir kez alınır. | Yalnız kendi vuruşun. Bir kez alınır. |
| Fırtına Bileği | Round 25 boss'undan sonra çıkar; bir kez alınır. Taban 0,10 sn. | Bir kez alınır. Taban 0,10 sn. |
| Bilgi Filizi | Hasattan gelen XP. Round 30 boss'undan sonra çıkmaz. | Hasattan gelen XP. |

Yeni profilde sunulma zamanı kartın üstündeki etikette, aşamadan ve run takviminden üretilir: "GÜÇLÜ AŞAMA · Round 23
boss'undan itibaren". Eski profilde kart eski metni gösterir ve eski kural da öyle çalışır.

## 4. Teklif dağılımı ölçümü

**Ne ölçüldü.** Run oynanmadı. Tarla düzeni sabit kuruldu; teklifler oyunun kendi teklif koduyla üretildi
(`RewardOfferMeasurement.RunDistribution`). **Kazanma oranı ya da build gücü kanıtı değildir.**

| Koşul | Tarla | Son round doluluğu | Koşulu sağlamayan ödüller |
|---|---|---|---|
| Doğrudan vuruş | 9 saksı, davranış tile'ı yok | 0,75 | Bereketli Toprak, Kıvılcım, Yıkım Gücü, Artçı Patlama, Çifte Akım |
| Patlamalı | 9 saksı, 4'ünün altında patlama tile'ı | 0,75 | Bereketli Toprak, Çifte Akım |
| Elektrikli | 9 saksı, 4'ünün altında elektrik tile'ı | 0,75 | Bereketli Toprak, Artçı Patlama |
| Çok davranışlı | 9 saksı; patlama, elektrik, kasırga, bumerang | 0,75 | Bereketli Toprak |
| Doğrudan vuruş · tarla boşalıyor | 9 saksı, davranış yok | 0,50 | Kıvılcım, Yıkım Gücü, Artçı Patlama, Çifte Akım |

- **A) Sabit durum:** hiç ödül alınmamış; 15 boss tarihinin her biri 400 seed ile.
- **B) Birikimli dizi:** 200 seed; 15 boss sırayla, her teklifte rastgele bir kart alınır, stack'ler birikir.
- İkisi de "mevcut aşamaya slot ayır" açık ve kapalıyken çalıştı. Kapalı kol yalnız karşılaştırma içindir; oyunda açık.

Toplam 90.000 teklif. Tam tablolar: `Docs/Bolum3-7-3/olcum/RewardOfferDistribution.md` (ve `.csv`).

### 4.1 Hangi aşamadan kaç kart geldi (A, slot ayırma açık)

Teklif başına ortalama kart sayısı (toplam hep 3,00).

| Koşul | Orta boss'larında: erken / orta | Güçlü boss'larında: erken / orta / güçlü |
|---|---|---|
| Doğrudan vuruş | 1,12 / 1,88 | 0,46 / 0,58 / 1,96 |
| Patlamalı | 0,76 / 2,24 | 0,33 / 0,72 / 1,95 |
| Elektrikli | 0,76 / 2,24 | 0,33 / 0,72 / 1,95 |
| Çok davranışlı | 0,76 / 2,24 | 0,30 / 0,63 / 2,07 |
| Doğrudan vuruş · tarla boşalıyor | 1,24 / 1,76 | 0,54 / 0,55 / 1,91 |

Erken boss'larında (R3, R6, R10) üç kart da erkendir; başka aday yok.

### 4.2 Boş ve eksik teklif

Hiçbir koşulda, hiçbir boss'ta **3'ten az kartlı ya da boş teklif çıkmadı** (A: 30.000 teklifte 0; B: 15.000 teklifte 0,
slot ayırma açıkken). Aday sayısının 2, 1 ve 0'a düştüğü durumlar işlev testinde elle kuruldu ve çalıştı (5. bölüm).

### 4.3 Slot ayırmanın etkisi

**Mevcut aşamadan en az bir kart içeren tekliflerin oranı (A):**

| Koşul | Orta boss'ları: açık → kapalı | Güçlü boss'ları: açık → kapalı |
|---|---|---|
| Doğrudan vuruş | %100 → %94 | %100 → %94 |
| Patlamalı / Elektrikli | %100 → %98 | %100 → %93 |
| Çok davranışlı | %100 → %98 | %100 → %95 |
| Doğrudan vuruş · tarla boşalıyor | %100 → %90 | %100 → %93 |

**Ödülün görülme sıklığı** (o aşamadaki tekliflerin yüzde kaçında çıktı; doğrudan vuruş koşulu, açık → kapalı):

| Ödül grubu | Orta boss'larında | Güçlü boss'larında |
|---|---|---|
| Erken ödüller (her biri) | %27–30 → %34–37 | %11–12 → %15–16 |
| Orta ödüller (her biri) | %62–64 → %51–55 | %19 → %24–27 |
| Güçlü ödüller (her biri) | — | %49 → %40–41 |

Açıklaması:

- Slot ayırma olmadan da ağırlık çarpanları mevcut aşamayı öne çıkarıyor; ama tekliflerin %2–10'unda mevcut aşamadan hiç kart
  çıkmıyor. Slot ayırma bunu sıfırlıyor.
- Bedeli alt aşamalardan geliyor: mevcut aşamanın her ödülü 8–11 puan daha sık, alt aşamaların her ödülü 4–10 puan daha
  seyrek görülüyor.
- Ayrılan kart ekranda sabit bir yerde durmuyor.
- Erken aşamada etkisi yok (bütün adaylar zaten mevcut aşamadan).

### 4.4 Güçlü aşama geç boss'larda tükeniyor (B)

Mevcut aşamadan en az bir kart içeren tekliflerin oranı, slot ayırma açık:

| Koşul | R23–R36 | R40 | R43 | R46 | R50 |
|---|---|---|---|---|---|
| Doğrudan vuruş | %100 | %99 | %91 | %82 | %64 |
| Patlamalı / Elektrikli | %100 | %100 | %99 | %94 | %84 |
| Çok davranışlı | %100 | %100 | %100 | %99 | %95 |
| Doğrudan vuruş · tarla boşalıyor | %100 | %99 | %92 | %84 | %67 |

Neden: güçlü aşamada 9 boss var, ama güçlü ödüllerin toplam stack'i 7 (Geniş Savuruş 2, diğer beşi 1'er). Davranışsız bir
build için bu 5'e iner. Oyuncu güçlü kartları aldıkça aşama boşalıyor ve teklif alt aşamalardan doluyor. Teklif yine 3 kart;
kilitlenme yok. **Bu bir içerik sayısı sorunu**, kuralın hatası değil: güçlü aşamaya içerik eklenmesi (P11) ya da stack
sınırlarının gözden geçirilmesi gerekir. Bu pakette dokunmadım.

Bu dizide run başına ortalama alınan güçlü ödül (çok davranışlı koşul): Geniş Savuruş 1,29 · Canavar Kesimi 0,85 · Fırtına
Bileği 0,78 · Artçı Patlama 0,77 · Çifte Akım 0,80 · Hasat Ritmi 0,81. Seçim rastgeleydi; oyuncu tercihi değildir.

### 4.5 Rastgelelik

90.000 teklifin her birinde oyunun zar akışı (UnityEngine.Random) önce ve sonra karşılaştırıldı: değişmedi.

## 5. Testler ve loglar

Hepsi izole kopyada (`Library/VerificationProject`), `Tools/run-isolated-verification.ps1` ile, birer birer, varsayılan kısıtla
(düşük öncelik, 4 çekirdek), son kodla. İşlev testi "çalışıyor" der; denge ya da his kanıtı değildir.

### Yeni test

`Tools/Verification/Editor/OdulAsamalariV1Verification.cs` — **PASS, 71 kontrol** (yaklaşık 55 sn).

Oyuncu vuruşu kapalıdır; skor ve level testin eliyle verilir; tarla, testin kurduğu sabit düzendir. Round'lar sırayla oynanır.

| İstenen kontrol | Nasıl denendi | Sonuç |
|---|---|---|
| 15 boss tarihinin tamamında doğru aşama | Veri çözümü + tam run'da (R1–50) ekrandaki başlık | R3, 6, 10 ERKEN · R13, 16, 20 ORTA · R23 … R50 GÜÇLÜ |
| R10→R13 ve R20→R23 geçişleri | Tam run ve 300 seed'lik sabit durum | Orta ödüller ilk kez R13'te, güçlü ödüller ilk kez R23'te |
| R11/12 ve R21/22 sınırları | Veri çözümü, 50 round'un hepsi | R11 erken · R12 orta · R21 orta · R22 güçlü |
| Erken aşamada sızıntı yok | R3, R6, R10 × 300 seed | Yalnız beş erken ödül |
| Güçlü adaylar R23'ten önce gelmez | R13, R16, R20 × 300 seed; Artçı ve Çifte Akım için R20 ayrıca | Hiç güçlü ödül yok |
| Mevcut aşamadan en az bir seçenek | 7 tarla durumu × 15 tarih × 60 seed (6.300 teklif) + tam run | Uygun aday olan her teklifte var |
| Aynı ödül tekrarlanmaz | Aynı 6.300 teklif + tam run + 2 adaylı durum | Tekrar yok |
| Stack, stat tavanı, davranış koşulları | Keskin Bıçak 3/3; saldırı aralığı tabanda; şans %100; davranışsız, patlamalı, elektrikli tarla; doluluk 0,50 ve 0,75 | Koşulu sağlamayan ödül hiç sunulmadı |
| Mevcut aşama tükenince alt aşama | R13'te orta adaysız (sabit durum ve canlı run) | Üç erken kart; başlık yine "ORTA ÖDÜLLER" |
| 0 / 1 / 2 aday | İkinci run'da R3, R6, R10 | 0: "Uygun ödül kalmadı" + DEVAM · 1 kart · 2 kart; run sürdü |
| Kart sonrası uygunluk | R13'te bir level; ilk kart saksının altına patlama tile'ı koydu | Teklif kartlardan sonra hazırlandı ve Kıvılcım'ı sundu |
| Uzmanlaşma sonrası | Uzmanlaşmalı kopya profil (test verisi), R3 | Önce uzmanlaşma; teklif o seçilene kadar hazırlanmadı |
| Panel yeniden açılınca | 15 boss'un hepsinde kapat / aç | Teklif aynı |
| Aynı seed, aynı durum | Her canlı teklif yeniden üretildi | Aynı teklif |
| Gameplay RNG'si | 6.825 sabit durum teklifi + 15 canlı teklif, önce / sonra | Değişmedi |
| R50 ödülü → zafer | Tam run, R50'de bir level bekliyor | Kartlar → "GÜÇLÜ ÖDÜLLER" → zafer; ödül run sonu listesinde; R51 yok |
| Restart temizliği | Zaferden sonra oyunun "Restart" düğmesi | Teklif, aşama, ödül ve ödül etkisi yok |
| Menü temizliği | R16'da teklif bekliyorken ana menüye dönüş | Teklif, aşaması, alınan ödüller ve etkileri temizlendi |
| Ödül etkileri aşama değişince silinmez | Tam run boyunca 15 boss | Alınan ödüller ve etkileri hiç azalmadı |
| Eski profilin teklif dizisi | Kod değişmeden önce kaydedilen diziyle karşılaştırma | 6.480 teklif kart kart aynı |
| Hatalı havuz verisi | 13 bozuk havuz + menü + oyun içinde bir tane | Hepsi adını koyan bir hatayla reddedildi |
| Kart yazıları | 16 ödül × her birikim adedi (34 kart durumu) | Kesilme ve taşma yok; en küçük punto 16,6 |

**Eski teklif dizisinin kaydı.** Teklif koduna dokunmadan önce eski havuzların (Kırılma V1, Denge V1, Boss Prototip) teklifleri
3 tarla durumu × 2 birikim durumu × 9 boss round'u × 40 seed için kaydedildi (`BossOfferGolden.cs`; iki çalıştırmada aynı
çıktı). Test, bugünkü kodla aynı teklifleri üretip satır satır karşılaştırıyor.

### Etkilenen eski paketler

| Test | Neyi koruyor | Sonuç |
|---|---|---|
| BossRewardVerification | Eski boss ödülü akışı, ödül ekranı, kart yazıları | **İlk çalıştırma FAIL**, kod düzeltildikten sonra PASS · 127 (aşağıda) |
| TakvimV1Verification | 15 boss takvimi, R50 sırası (eski havuzla) | PASS · 162 |
| KirilmaV1Verification | Kırılma V1, kırılma slotu, HUD | PASS · 189 |
| DengeV1Verification | Denge V1 profili ve ödül havuzu | PASS · 86 |
| BossWeatherVerification | Boss hava efekti | PASS · 22 |
| RunPrototypeVerification | Eski run, zafer, kayıp, menü | PASS · 69 |
| SpecializationVerification | Eski uzmanlaşma akışı | PASS · 100 |
| StartLoadoutVerification | Başlangıç seçimi | PASS · 88 |
| RoundPreviewVerification | Önizleme ekranı | PASS · 47 |
| Run50ReferenceVerification | Run50 referans profili | PASS · 70 |
| MechanicsVerification | Profilsiz sahne, kart seçimi | PASS · 88 |
| ElectricRevertVerification | Elektrik, yeniden başlatma | PASS · 40 |
| HarvestBehaviorVerification | Davranışlar | PASS · 114 |
| SimpleResonanceVerification | Rezonans | PASS · 293 |
| ContactRefactorVerification | Temas kuralı | PASS · 175 |
| OptionsMenuVerification | Ayarlar menüsü | PASS · 22 satır |

Kontrol sayıları 3.7.2'deki turla aynı.

**İlk turda düşen test: `BossRewardVerification`.** Kart yazılarını ilk hâlinde "satır kır" diye değiştirmiştim; orta uzunluktaki
birikim satırları (ör. Hızlı Bilek) üç satıra çıktı. Eski test bu satırın iki satır kalmasını bekliyor. Testi değil kodu
düzelttim: birikim yazısı eskisi gibi önce küçülerek iki satırda kalıyor, yalnız en küçük puntoda da sığmıyorsa (Hasat Ritmi)
satır kırıyor. Bu düzeltmeden sonra bütün testler son kodla yeniden çalıştırıldı; yukarıdaki sonuçlar o turdandır. İlk turun
kaydı: `Docs/Bolum3-7-3/testler/TopluSonuc.txt` ("ILK TUR" bölümü) ve `BossRewardVerification_ilk_FAIL.txt`.

### Bilerek değişen eski beklentiler

İki test profil listesini sayıyordu; yeni profil eklendiği için güncellendi. Kural gevşemedi:

1. `KirilmaV1Verification`: "Kırılma V1'in denge setini yalnız Kırılma V1 ve Takvim V1 kullanır" → listeye
   `Run50_OdulAsamalariV1` eklendi (aynı denge setini bilerek paylaşıyor).
2. `TakvimV1Verification`: "Takvim V1 dışındaki profillerin takvimi boştur" → `Run50_OdulAsamalariV1` için "Takvim V1 ile aynı
   15 tarih ve aynı hedefler" kontrolü eklendi.

Başka hiçbir eski beklenti değişmedi.

### Üretici kontrolleri

`make_denge_v1.py`, `make_kirilma_v1.py`, `make_takvim_v1.py` ve `make_odul_asamalari_v1.py` için `--check` temiz;
`test_generation_guard.py` 3 / 3.

### Dokunulmayanlar

- **Oyuncu kaydı:** `meta.json` paketin başında ve sonunda aynı (hash ile; son değişiklik 1 Ekim 19:15).
- **Seçili profil:** `Assets/Resources/RunProfileSelection.asset` 17:08'de değişmiş: seçim `Run50_KirilmaV1` yerine artık
  `Run50_TakvimV1`. Bu dosyaya ben yazmadım (araçlarım yalnız izole kopyadaki seçimi değiştirir); açık editörde menüden
  seçilmiş görünüyor. Olduğu gibi bıraktım. Yan bilgi: bütün testler bu seçim yürürlükteyken çalıştı ve geçti.
- **Editör:** açık Unity editörüne dokunmadım; bütün testler izole kopyada çalıştı.
- **Git:** HEAD hâlâ `28e4650`; commit yok (3.7.2'nin değişiklikleri de commit'siz duruyor).

### Log yolları

| Ne | Nerede |
|---|---|
| Sonuç dosyaları | `Docs/Bolum3-7-3/testler/` (`TopluSonuc.txt` özet) |
| Teklif dağılımı | `Docs/Bolum3-7-3/olcum/` |
| Görüntüler | `Docs/Bolum3-7-3/ekran/` |
| Ham Unity logları | `Library/VerificationProject/Logs/*.log` |

Yeniden çalıştırmak:

```powershell
Tools/run-isolated-verification.ps1 -Method OdulAsamalariV1Verification.RunBatch -Full
```

```powershell
Tools/run-isolated-verification.ps1 -Method RewardOfferMeasurement.RunDistribution -Full
```

## 6. Ekran görüntüleri

Gerçek sahneden, batch'te kameradan çizilmiş görüntüler (`Docs/Bolum3-7-3/ekran/`). Hepsine baktım.

| Dosya | Ne gösteriyor |
|---|---|
| `T373_01_R3_ErkenOduller.png` | Erken aşama: "ERKEN ÖDÜLLER"; üç kart da "ERKEN AŞAMA · Round 3 boss'undan itibaren" |
| `T373_02_R13_OrtaOduller.png` | Orta aşama: "ORTA ÖDÜLLER"; iki orta, bir erken kart; Bilgi Filizi'nin yeni açıklaması |
| `T373_03_R23_GucluOduller.png` | Güçlü aşama: "GÜÇLÜ ÖDÜLLER"; Hasat Ritmi ve Canavar Kesimi (yeni açıklamasıyla), bir orta kart |
| `T373_04_R50_SonBoss_GucluOduller.png` | R50: "Son boss geçildi · … run burada biter" |
| `T373_05_KartYazilari_HasatRitmi.png` | **Test düzeneği:** en uzun yazılı üç kart yan yana (Hasat Ritmi, Kıvılcım, Bereketli Toprak) |
| `T373_06_EskiProfil_KirilmaV1.png` | **Test düzeneği:** eski profilde ödül ekranı; etiket yok, eski açıklamalar ("Round 25 boss'undan sonra çıkar") |

Notlar:

- `T373_05` ve `T373_06`'daki kartlar teste elle konmuştur (teklif kuralından gelmedi): amaç yalnız yazıları göstermek.
  `T373_05`'te başlık "ERKEN ÖDÜLLER" iken güçlü ve orta kart görünmesi bu yüzdendir.
- `T373_02`–`T373_04`'te altın 1.170 görünüyor: test, sabit tarla düzenlerini kurup kaldırırken saksılar geri satıldı. Ödül
  uygunluğunu etkilemez.
- Tam run'da R13 ve R23'ün boss seed'i, görüntüde istenen kartlar (Kıvılcım; Canavar Kesimi ve Hasat Ritmi) çıksın diye
  testte seçildi. Kural kontrolleri bundan bağımsız, 6.300 teklifte yapıldı.

## 7. Arayüz değişiklikleri

- **Başlık:** yeni profilde teklifin aşaması ("GÜÇLÜ ÖDÜLLER"). "Boss geçildi" bilgisi alt satıra indi: "Boss geçildi · yalnız
  biri alınır · run sonuna kadar geçerli · etkisi Round 24 ve sonrası". Eski profillerde başlık ve alt satır eskisi gibi.
- **Kart etiketi:** kartın üstünde ödülün kendi sınıfı ve ilk sunulduğu boss. Güçlü teklifte orta ya da erken kart çıkarsa kendi
  sınıfını yazar. Etiket bir güç vaadi içermez.
- **Kart yazıları (bütün profillerde):** etki, açıklama ve birikim yazıları üst üste binmeyen üç bölgede duruyor ve kartın
  dışına taşmıyor. Etki ve açıklama satır kırar; sığmazsa bölgenin en küçük puntosuna kadar küçülür (etki 20, açıklama 15).
  Birikim yazısı eskisi gibi iki satırdır ve gerekirse 15 puntoya kadar küçülür; o puntoda da sığmıyorsa satır kırar. Bugün bu
  yalnız Hasat Ritmi'nde oluyor: alt satır 21 puntoda üç satır. 16 ödülün bütün birikim adetlerinde en küçük punto 16,6
  (Hasat Ritmi'nin açıklaması).

## 8. Veri dosyaları ve üretim kaynağı

| Dosya | Ne | Kaynak |
|---|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_OdulAsamalariV1.asset` | Profil | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/OdulAsamalariV1/BossRewardPool_OdulAsamalariV1.asset` | Aşamalı havuz | Üretici çıktısı |
| `Tools/Balance/OdulAsamalariV1/odul_asamalari_v1_params.py` | **Tek düzenleme kaynağı** | Elle |
| `Tools/Balance/OdulAsamalariV1/make_odul_asamalari_v1.py` | Üretici | — |
| `Tools/Balance/OdulAsamalariV1/generated_manifest.json` | Çıktıların hash kaydı | Üretici |
| `Tools/Balance/OdulAsamalariV1/README.md` | Veri sahipliği ve akış | — |

- Profil, Takvim V1'in profilinden yalnız üç satırda ayrılıyor: ad, zafer başlığı ve ödül havuzu.
- Takvim ve hedefler `takvim_v1_params.py` dosyasından, ödüllerin etkileri DengeV1 ve Kırılma V1 asset'lerinden gelir; kopya yok.
- Inspector'da yapılan değişiklik üreticiyi durdurur; kalıcı olacaksa önce parametre dosyasına taşınır (README).
- Eski üreticilere, eski ödül asset'lerine ve eski havuzlara dokunulmadı.

**Geçersiz havuz düzeltilmez.** Aynı ödül iki aşamada, aynı kimliğin kopya asset'i, sırasız aşamalar, eksik ağırlık gibi
durumlarda: oyun run'ı başlatmaz ve Console'a hatayı yazar; menü profili seçmez; üretici yazmadan durur.

## 9. Benim yorumlarım (prompt'ta açık olmayan yerler)

1. **Aşama round'ları havuz asset'inde duruyor**, profil o havuza referans veriyor. Böylece profil Takvim V1'den yalnız ödül
   havuzuyla ayrılıyor. Round'ları doğrudan profil asset'inde istersen taşınabilir.
2. **Kartın üstündeki etiket ve ilk boss bilgisi** ("Round 23 boss'undan itibaren") benim sunum seçimim; aşama renkleri de
   (kum sarısı, camgöbeği, mercan) nadirlik renklerinden ayrı olsun diye seçildi.
3. **Üç ödülün yeni açıklama metni** (3. bölüm) benim yazdığım kısaltmalar.
4. **Sınırsız aşamada, ana aşamalarda yazılı bir ödül tekrar yazılamaz** (aynı "bir ödül tek aşamada" kuralı). Endless'ın ana
   run ödüllerini yeniden sunup sunmayacağı kararı açık; bu denetim o kararla değişebilir.
5. **Ağırlığı 0 olan aday hiç seçilmez** (çarpan 0 yapılırsa o aşama kalan slotlara girmez).
6. **Ayrılan slot ödülün kendi ağırlığıyla**, kalan slotlar ağırlık × çarpanla seçilir. Bugün bütün ağırlıklar 1.
7. **Bot ölçüm aracı** (`BalanceRunMeasurement`) aşamalı havuzda normal run oynatabilir, ama denenmedi. "Ödülsüz eş", "almayan
   eş" ve laboratuvar kolları kırılma listesine göre yazıldığı için aşamalı havuzda "Bu profil desteklenmiyor" der.

## 10. Kısa Play kontrolü (10–15 dakika)

1. Menü: **Tools → Run Profili → Run50 Ödül Aşamaları V1 · 50 round (aşamalı boss ödülleri prototipi)**. Play.
2. R3 boss'unu geç: başlık "ERKEN ÖDÜLLER", üç kartın üstünde "ERKEN AŞAMA · Round 3 boss'undan itibaren".
3. R13: başlık "ORTA ÖDÜLLER". En az bir kart orta olmalı; erken kart da çıkabilir ve kendi etiketini taşır.
4. Tarlanda davranış (patlama, elektrik …) yokken Kıvılcım ve Yıkım Gücü gelmemeli; bir davranış kartı aldıktan sonraki
   boss'ta gelebilmeli.
5. R23: başlık "GÜÇLÜ ÖDÜLLER". Canavar Kesimi ya da Fırtına Bileği kartında "Round 25" yazmamalı.
6. Hasat Ritmi kartı gelirse alt yazı kartın içinde kalmalı.
7. Bakılacak his soruları: üç round'da bir ödül ritmi; etiketler anlaşılıyor mu; geç boss'larda güçlü kart azaldığında bu
   fark ediliyor mu?
8. Eski profile dönmek için aynı menüden `Run50 Kırılma V1`.

Seçili profilini ben değiştirmedim; şu an `Run50_TakvimV1` seçili görünüyor.

## 11. Açık kalan işler

**Bu paketin dışında bırakılanlar**

- **Endless havuzu:** yalnız boş veri alanı. İçeriği, hangi round'da açılacağı ve oynanışı yok (P10 / P11).
- **Güçlü aşamanın tükenmesi** (4.4): içerik sayısı ya da stack sınırı kararı (P11).
- **Ödül güçleri:** Artçı Patlama, Çifte Akım, Hasat Ritmi ayarı (P5). Sınıflandırma güç kanıtı değildir.
- **"Kapatılan özellik" için geçersiz teklif yolu:** bedel ödülleriyle birlikte (P4).
- **Denge:** 15 ödül ve aşamalı dağılımın run gücüne etkisi ölçülmedi (P8).

**Doğrulanamayanlar**

- **İnsan gözüyle Play onayı yok.** Görüntüler batch'te kameradan çizildi.
- **Gerçek build'lerle teklif dağılımı ölçülmedi.** Ölçüm dört sabit tarla düzeninde yapıldı; gerçek bir run'da tarla ve
  doluluk değişir.
- **Birikimli dizide seçim rastgeleydi.** Oyuncu güçlü kartları öncelerse güçlü aşama daha erken tükenir.
- **Uzmanlaşma yeni profilde yok;** sıra, uzmanlaşma eklenmiş bir kopya profille (test verisi) denendi.
- **Geçersiz havuzda ekran:** run başlamıyor ve hata Console'da; oyuncuya dönük bir uyarı ekranı yok.
- **Inspector'dan düzenleme hatası** (`OnValidate`) testle çalıştırılmadı; aynı denetim menü ve oyun yolunda çalıştı.
- **Bot ölçüm aracı** aşamalı havuzla çalıştırılmadı.

## 12. Değişen dosyalar

**Oyun kodu**

| Dosya | Ne değişti |
|---|---|
| `Assets/Scripts/ScriptableObjects/BossRewardPoolSO.cs` | Aşamalar, uzaklık ağırlıkları, slot ayırma, sınırsız aşama alanı, aşama çözümü, veri denetimi |
| `Assets/Scripts/Managers/BossRewardManager.cs` | Aşamalı teklif kuralı; teklifin aşaması, ödülün aşaması ve açıklaması için sorgular. Eski teklif yolu aynı |
| `Assets/Scripts/Managers/RunCalendar.cs` | `NextBossRound` (aşamanın ilk boss'u) |
| `Assets/Scripts/Managers/RoundManager.cs` | Geçersiz ödül havuzunda run başlamaz |
| `Assets/Scripts/UI/BossRewardPanelUI.cs` | Aşama başlığı, kart etiketi, havuzdan gelen açıklama, kart yazı bölgeleri |
| `Assets/Scripts/UI/BossRewardText.cs` | Aşama başlığı ve etiket metni |
| `Assets/Editor/RunProfileMenu.cs` | Yeni menü satırı; geçersiz havuzlu profil seçilmez |

**Veri**

| Dosya | Ne |
|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_OdulAsamalariV1.asset` (+ `.meta`) | Yeni profil |
| `Assets/ScriptableObjects/BossRewards/OdulAsamalariV1/` (+ `.meta`) | Yeni aşamalı havuz |

**Araçlar ve testler**

| Dosya | Ne |
|---|---|
| `Tools/Balance/OdulAsamalariV1/` | Yeni üretici, parametreler, manifest, README |
| `Tools/Verification/Editor/OdulAsamalariV1Verification.cs` | Yeni test |
| `Tools/Verification/Editor/RewardOfferLab.cs` | Yeni: sabit tarla düzenleri ve teklif üretimi (test ve ölçüm ortak kullanır) |
| `Tools/Verification/Editor/RewardOfferMeasurement.cs` | Yeni: teklif dağılımı ölçümü ve eski dizinin kaydı |
| `Tools/Verification/Editor/BossOfferGolden.cs` | Yeni: eski havuzların kod değişmeden önceki teklif dizisi (üretilmiş) |
| `Tools/Verification/Editor/KirilmaV1Verification.cs`, `TakvimV1Verification.cs` | Profil listesi beklentisi (5. bölüm) |
| `Tools/Verification/Editor/BalanceRunMeasurement.cs` | Aşamalı havuzda uyarlanmamış kollar "desteklenmiyor" der |

Sahne, prefab, eski profiller, eski havuzlar ve eski ödül asset'leri değişmedi.

## 13. Ana TODO'da işaretlenenler

- **P3:** "erken / orta / güçlü havuzları", "aralık ve geçiş politikası", "3 adaydan 1 akışı" ve "ilk temsilci içerikler"
  işaretlendi. İlk madde ikiye bölündü: endless havuzu **işaretlenmedi** (yalnız boş veri alanı var). "Uygun ödül tükenmesi
  ve kapatılan özellik" maddesi işaretlenmedi: tükenme yolu yapıldı, "kapatılan özellik" P4'te.
- **P0:** "ödül aşamalarının havuz politikası" işaretlendi (karar senin prompt'undan; ana run için).
- P4 ve sonrasına geçilmedi. P5, P10 ve P11 açık.
