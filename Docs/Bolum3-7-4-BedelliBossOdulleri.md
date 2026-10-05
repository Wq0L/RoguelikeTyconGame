# Bölüm 3.7.4 — Bedelli boss ödülleri ve değişken level seçim hakkı

**Durum (2026-10-02):** P4'ün oynanabilir uygulaması yapıldı ve izole kopyada doğrulandı. Yeni aday profil
`Run50_BedelliOdullerV1` iki bedelli ödülle çalışıyor. Sayılar senin verdiğin **ilk test değerleridir**; dengelenmedi,
katsayılara dokunmadım. İnsan Play kontrolü yapılmadı. Commit atılmadı; seçili profile ve oyuncu kaydına dokunmadım.

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md) · Önceki paket: [Bolum3-7-3-AsamaliBossOdulleri.md](Bolum3-7-3-AsamaliBossOdulleri.md)

## Kısa sonuç

- **İki ödül gerçekten çalışıyor:** Bereketli Öğrenim (level başına seçim 3 → 4, doğrudan vuruş ×0,80) ve Davranışa Adanış
  (davranış hasarı ×1,50, level başına seçim 3 → 2). İkisi de güçlü aşamada, ilk kez R23 boss'unda, en çok bir kez.
- **Karşılıklı dışlama:** ikisi aynı teklifte çıkabilir; biri alınınca diğeri o run'da bir daha sunulmaz.
- **Seçim hakkı yalnız ileriye işler:** bekleyen seçimler ödülle değişmez; sonraki her level yeni sayıyı ekler.
- **Tek işlem:** önce bütün koşullar doğrulanır, sonra kazanç ve bedel birlikte yazılır. Yarıda hata olursa hiçbir iz kalmaz.
- **Hasar yolu aynı:** yeni bir stat sistemi yok; iki çarpan mevcut ortak hesaba (RunPower) girer, her vuruşta bir kez.
- **Eski profiller aynı:** eski ödüllerde yeni alanlar etkisiz; eski havuzların teklif dizisi kayıtla kart kart aynı.
- **Başlık düzeltmesi:** ödül ekranı artık "GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ" yazıyor (Ödül Aşamaları V1'de de).
- **Kısa ölçümde öne çıkan:** üç round'luk laboratuvarda Bereketli Öğrenim ödülsüz koldan daha az kart verdi (8'e karşı 9):
  ×0,80 hasadı ve XP'yi düşürdü, bir level eksik kazanıldı. Sonuç level eşiğine çok yakın; denge kanıtı değildir (3. bölüm).

## 1. İki ödül ve kesin kuralları

| | Bereketli Öğrenim | Davranışa Adanış |
|---|---|---|
| Kazanç | Gelecekte kazanılan her level: seçim hakkı +1 (3 → 4) | Patlama, elektrik, kasırga, bumerang hasarı ×1,50 |
| Bedel | Doğrudan vuruş hasarı ×0,80 | Gelecekte kazanılan her level: seçim hakkı −1 (3 → 2) |
| Dokunmadığı | Davranış hasarı | Doğrudan vuruş hasarı |
| Koşul | Yok | Yerleşmiş en az bir saksıda davranış şansı (dört davranıştan herhangi biri) |
| Aşama | Güçlü; ilk kez R23 boss'unda | Güçlü; ilk kez R23 boss'unda |
| En çok | 1 kez | 1 kez |
| Teklif ağırlığı | 1 | 1 |
| Dışlama grubu | `secim_hakki_takasi` | `secim_hakki_takasi` |

**Teklif.** İki ödül, güçlü aşamanın diğer altı ödülüyle aynı kuraldan geçer: mevcut aşamaya ayrılan slot ve kalan slotlar
(Bölüm 3.7.3). Ayrı bir garanti yok. R23'te, dört davranışın da bulunduğu tarlada 400 seed: ikisi birlikte 29, yalnız Öğrenim
90, yalnız Adanış 83, hiçbiri 198 teklif. Görülme sayıları (119 ve 112) diğer altı güçlü ödülün ortalamasıyla (112) aynı düzeyde.

**Karşılıklı dışlama.** Aynı gruptan bir ödül alınmışsa diğeri sunulmaz ve alınamaz. Alınmış ödül değiştirilemez, geri
satılamaz. Yeni run'da dışlama kalkar.

**Seçim hakkının anlamı.**

- Level başına seçim hakkı = profilin temel hakkı (3) + run'da alınmış ödüllerin değişimlerinin toplamı.
- Hak, level kazanıldığı anda hesaplanır ve bekleyen sayaca eklenir. Bekleyen seçimler sonradan yeniden hesaplanmaz.
- Ekrandaki aday sayısı ayrı bir şeydir ve değişmez: her kart seçiminde 3 aday, 1 kart. Boss ekranı da 3 adaydan 1 ödül.
- Level sayısı ve XP gereksinimi değişmez.
- Geçerli aralık 1–5. Aralığı aşacak ödül **kırpılmaz**: sunulmaz, zorla teklife konsa da alınamaz ve bedeli de uygulanmaz.
- Yeni profilde erişilebilir sonuçlar 2, 3 ve 4 (iki ödül birbirini dışladığı için).

**Koşul alınırken de aranır (bedeli olan ödülde).** Teklif hazırlandıktan sonra tarlada davranış kalmadıysa Adanış reddedilir;
−1 seçim bedeli ödenmez.

## 2. Etkilerin hesap ve uygulama yolu

### Hasar

Yeni bir stat sistemi kurulmadı. İki ödül, ödül verisinde zaten bulunan iki çarpanı kullanır (`directDamageMultiplier`,
`behaviorDamageMultiplier`); bunlar mevcut ortak hesaba girer:

| Hasar | Hesap (tek çarpım, tek yuvarlama) | Nerede |
|---|---|---|
| Doğrudan vuruş | hasar × sapma × uzmanlaşma × tırpan × **boss ödülleri (doğrudan)** × nadirlik ödülü × o saldırıya özel çarpan | `RunPower.DirectDamage` → `PlayerController` |
| Davranış | davranışın taban hasarı × rezonans × uzmanlaşma × **boss ödülleri (davranış)** | `RunPower.Behavior` → `PlanterBrain.GetBehaviorDamage` |
| Artçı / ikinci dalga | ilk darbenin **hesaplanmış** hasarı × ödülün oranı (çarpanlar yeniden uygulanmaz) | `BehaviorEchoes` |

Bereketli Öğrenim'in ×0,80'i yalnız birinci satıra, Davranışa Adanış'ın ×1,50'si yalnız ikinci satıra girer. Kritik ve saksı
bonusu (Odak) eskisi gibi doğrudan vuruşun yuvarlanmış değerine sonradan uygulanır.

Doğrulanan birleşimler (taban 100, sapma 1):

| Durum | Doğrudan | Davranış (dördü de) |
|---|---|---|
| Ödülsüz | 100 | 100 |
| Bereketli Öğrenim | 80 | 100 |
| Davranışa Adanış | 100 | 150 |
| Adanış + Davranış Ustası (×1,25) | 100 | 188 (100 × 1,25 × 1,50 = 187,5) |
| Öğrenim + Usta Biçici (doğrudan ×1,25, davranış ×0,85) | 100 | 85 |
| Öğrenim + tırpan çarpanı ×1,40 | 112 | 100 |
| Öğrenim + Keskin Bıçak (×1,25) | 100 | 100 |
| Öğrenim + Keskin Bıçak + Altın Hedef (Rare ve üstü ×1,50) | 150 (Legendary bitkide) | 100 |
| Öğrenim + Keskin Bıçak + Hasat Ritmi saldırısı (×1,75) | 175 | — |
| Adanış + Yıkım Gücü (×1,30) | 100 | 195 |
| Adanış + Artçı Patlama / Çifte Akım (oran 1,00) | — | ilk darbe 150, ikinci darbe 150 (225 değil) |

Gerçek vuruşlar da ölçüldü (bitkiye uygulanan hasar): patlama 4 × 150, elektrik 4 × 150, kasırga vuruşu 75 (50 × 1,50),
bumerang vuruşu 98 (65 × 1,50); Öğrenim'le 20 doğrudan vuruşun her biri round(100 × sapma × 0,80).

Küçük bir tutarlılık düzeltmesi: kasırganın, kaynak saksısı bulunamadığında kullandığı yedek yol yalnız uzmanlaşma çarpanını
alıyordu. Artık o da ortak hesabı kullanıyor. Oyunda bu yola girilmiyor (kasırgayı hep bir saksı başlatır).

### Seçim hakkı

- `RunPower.LevelChoices(temel)` = temel + `BossRewardManager.LevelChoiceDelta`.
- `RoundManager.ChoicesPerLevel` bunu okur; level kazanıldığında (`HandleLevelUp`) o anki değer bekleyen sayaca eklenir.
- Aralık sabitleri `RunPower.MinLevelChoices` / `MaxLevelChoices` (1 ve 5).

### Alınabilirlik ve tek işlem

Tek denetim yeri `BossRewardManager.BlockOf`: stack sınırı → çakışan ödül → seçim hakkı aralığı → koşul. Teklif hazırlanırken
de ödül alınırken de aynı denetim çalışır.

`Choose` sırası:

1. Bekleyen, hazırlanmış bir teklif var mı ve ödül teklifte mi?
2. `BlockOf`: stack, çakışma, seçim hakkı aralığı; bedeli olan ödülde koşul da.
3. `TryApply`: stat modifier'ları eklenir (hata verebilen tek adım). Hata olursa eklenenler geri alınır, başka hiçbir şey
   yazılmaz, ödül alınmamış sayılır ve Console'a hata düşer.
4. Çarpanlar, seçim hakkı değişimi, stack ve alınan ödül kaydı yazılır; teklif kapanır.

Teklif kapandığı için ikinci istek ve çift tıklama reddedilir.

### Temizlik

Yeni run, restart ve ana menüde `BossRewardManager.ClearAll`: seçim hakkı değişimi 0'a, doğrudan ve davranış çarpanları 1'e
döner; yalnız bu sistemin eklediği modifier'lar kaldırılır. Kalıcı kayda (MetaSave) hiçbir şey yazılmaz.

### Veri alanları (ödül adı ya da kimliği denetleyen kod yok)

| Alan (`BossRewardSO`) | Anlamı | Varsayılan |
|---|---|---|
| `levelChoiceDelta` | Gelecekte kazanılan her level'ın seçim hakkına eklenir | 0 (etkisiz) |
| `exclusiveGroup` | Karşılıklı dışlama grubu | boş (grup yok) |

"Bedeli olan ödül" de veriden çıkar: seçim hakkı eksi ya da bir hasar çarpanı 1'in altında. Kartın kazanç ve bedel satırları
bu yönlere göre üretilir.

**Yeni bedel türü eklemek için** (bu pakette eklenmedi): ödül verisine alan, `BossRewardManager.TryApply` ve `ClearAll`'a etki,
`BossRewardText`'e kazanç / bedel satırı. Stat modifier'ı satırları yön bilgisi taşımıyor; bedel olarak kullanılacaksa satır
sınıflandırması eklenmeli.

## 3. Kontrollü ölçüm

**Ne ölçüldü.** Run oynanmadı. Sabit bir laboratuvar durumu kuruldu ve üç kol aynı seed'lerle üç round oynatıldı
(`BedelliOdulMeasurement.Run`). Bot oyunun gerçek saldırı yoluyla vurur; XP gerçek hasattan gelir; level kartları gerçek kart
ekranından alınır. **Denge ölçümü değildir; XP yatırımının uzun vadede dengeli olduğunu göstermez.**

| Ayar | Değer |
|---|---|
| Profil, round'lar | `Run50_BedelliOdullerV1` · R27, R28, R29 (bu round'larda boss ve kota kontrolü yok) · 45 sn |
| Tarla | 5×5 açık, 25 tek hücrelik saksı; 8'inin altında davranış tile'ı (2 patlama %30, 2 elektrik %40, 2 kasırga %22, 2 bumerang %18) |
| Sabitlenen statlar | Hasar 25, saldırı aralığı 1,8 sn, yarıçap 1,4, XP kazancı ×2. Ağaç ve başka ödül yok |
| Level | 24'ten başlar; XP tablosu profilin kendi tablosu (sonraki üç level 1.900, 2.100, 2.200 XP) |
| Nişan ve kart | Saldırı alanında en çok canlı bitki olan nokta; her kart ekranında ilk aday |
| Kollar | Ödülsüz · Bereketli Öğrenim · Davranışa Adanış (ödül, round'lar başlamadan verilir) |
| Seed | 12 seed, üç kolda aynı |

Statlar, Bölüm 3.6 taban ölçümünde botların R24 satırlarındaki tipik değerlerdir. XP ×2 bir laboratuvar ayarıdır: tarlada
ağaç, XP ödülü ve nadir bitki payı olmadığı için hasat başına XP o ölçümün yarısı kadar çıkıyordu.

### Kol ortalamaları (12 seed, üç round toplamı)

| | Ödülsüz | Bereketli Öğrenim | Davranışa Adanış |
|---|---|---|---|
| Doğrudan vuruş başına hasar | 32,4 | 26,0 | 32,4 |
| Doğrudan hasar: vuruş toplamı (bitkinin canından götüren) | 13.628 (11.650) | 11.055 (9.685) | 13.527 (11.588) |
| Davranış vuruşu başına hasar | 21,5 | 21,4 | 32,0 |
| Davranış hasarı: vuruş toplamı (candan götüren) | 930 (845) | 780 (697) | 1.390 (1.141) |
| Gerçek hasat (doğrudan + davranış) | 128,1 (118,5 + 9,6) | 104,5 (96,8 + 7,8) | 130,8 (116,8 + 14,1) |
| Kazanılan XP | 6.563 | 5.283 | 6.680 |
| Kazanılan level | 3 | 2 | 3 |
| Level başına hak | 3 | 4 | 2 |
| Verilen seçim hakkı | 9 | 8 | 6 |
| Seçilen kart | 9 | 8 | 6 |

Level, verilen hak ve seçilen kart sayıları 12 seed'in hepsinde aynıydı. Seed başına tablo: `Docs/Bolum3-7-4/olcum/BedelliOdulMeasurement.md`
(ve `.csv`).

### Ne okunur

- **Çarpanlar gerçek akışta da doğru yerde.** Doğrudan vuruş başına 26,0 / 32,4 = 0,80. Davranış vuruşu başına
  32,0 / 21,5 = 1,49. Öğrenim davranış vuruşunu, Adanış doğrudan vuruşu değiştirmedi.
- **Bereketli Öğrenim bu pencerede daha az kart verdi.** Hasat %18, XP %20 düştü. Ödülsüz kol üç level, Öğrenim kolu iki
  level kazandı; level başına 4 hakka rağmen verilen hak 8, ödülsüz kolda 9.
- **Bu sonuç eşiğe çok yakın.** Üç level 6.200 XP istiyor. Ödülsüz kol 6.320–6.955 XP ile eşiği az farkla geçti; Öğrenim
  5.000–5.540'ta kaldı. Bir round fazlası ya da başka bir level, sonucu tersine çevirebilir.
- **Davranışa Adanış toplam hasadı belirgin değiştirmedi.** Davranış hasadı 9,6'dan 14,1'e çıktı; toplam hasat 128,1'e
  karşı 130,8 ve seed aralıkları örtüşüyor (124–139 ve 123–135). Bu tarlada hasadın yalnız %7–11'i davranıştan geliyor.
  Level sayısı aynı kaldı, verilen hak 9'dan 6'ya indi.
- **Sayaçlar ayrı ve tutarlı.** Her kolda verilen hak = level × level başına hak; seçilen kart = verilen hak; bekleyen 0;
  her kart ekranında 3 aday.

### Sınırlar

- Üç round ve tek bir sabit düzen. Fazladan kartların sonraki round'lardaki getirisi ölçülmedi; "geri ödeme" sorusu açık.
- Kart politikası tek ("ilk aday"). Gerçek oyuncu fazladan seçimi daha iyi kullanır.
- Ödül, round'lar başlamadan verildi; gerçek run'da ödülü hangi build'in aldığı sonucu değiştirir.
- Davranış tetikleri seed'e göre çok oynuyor: ödülsüz kolda davranış hasadı 6–14, davranış vuruş toplamı 500–1.429. Bu
  boyuttaki farklardan sonuç çıkarmadım.
- Bu laboratuvarda Artçı Patlama yok. İlk seed'in üç kolu ikinci kez çalıştırıldı: 9 round satırının 9'u bütün sütunlarıyla
  aynı çıktı. Ölçümün tamamı son kodla bir kez daha çalıştırıldı; çıktı dosyası öncekiyle bayt bayt aynı.
- İşlev testindeki level'lar yapay XP ile verilir (seçim hakkı kuralını doğrulamak için). Buradaki level'lar hasattan gelir.
  İkisi ayrı çalıştırılır ve ayrı raporlanır.

**İlk kurulum (kullanılmadı).** İlk denemede level 28, XP çarpanı yok ve iki round vardı. Altı seed'de toplam bir level
kazanıldı; seçim hakkı sütunları boş kaldı. Laboratuvar durumunu bu yüzden değiştirdim (level 24, XP ×2, üç round, 12 seed).
İlk çıktılar: `Docs/Bolum3-7-4/olcum/ilk-kurulum/`. Ödüllerin katsayılarına dokunulmadı.

## 4. Testler ve loglar

Hepsi izole kopyada (`Library/VerificationProject`), `Tools/run-isolated-verification.ps1` ile, birer birer, varsayılan kısıtla
(düşük öncelik, 4 çekirdek), **son kodla**. İşlev testi "çalışıyor" der; denge ya da his kanıtı değildir.

### Yeni test

`Tools/Verification/Editor/BedelliOdullerV1Verification.cs` — **PASS, 93 kontrol** (yaklaşık 90 sn).

Oyuncu vuruşu kapalıdır; skor testin eliyle verilir; tarla testin kurduğu sabit düzendir. Level'lar **yapay XP** ile verilir.

| İstenen kontrol | Nasıl denendi | Sonuç |
|---|---|---|
| Başlangıç 3 seçim | Run başı, yeni sahne, restart, eski profil | 3; dört sayaç 0 |
| Her ödülle 4 ya da 2 | Öğrenim ve Adanış, sabit durumda ve canlı run'da | 4 ve 2 |
| Bekleyen hakların değişmemesi | 6 bekleyen seçimle Öğrenim, 18 bekleyen seçimle Adanış | 6 → 6 (8 değil); 18 → 18 |
| Birden çok level aynı anda | Tek XP kazancıyla iki level, ödülden önce ve sonra | 3 + 3; 4 + 4; 2 + 2 |
| 3 → 4 → 2 aynı run'da | Test ödülleri (+1, sonra −2) | Etkiler toplanır; bekleyenler değişmez |
| 1 ve 5 sınırları | Test ödülleriyle 5'e ve 1'e inildi, level kazanıldı | 5 ve 1 hak eklendi |
| Sınırı aşan ödül kısmen uygulanmaz | 5'te +1, 4'te +2, 1'de −1, 2'de −2 | Reddedildi; kırpılmadı; hasar etkisi de yazılmadı; teklife de girmedi (200 seed) |
| Karşılıklı dışlama | Biri alındıktan sonra R23 … R50 × 200 seed; zorla teklife konup alma | Hiç sunulmadı; alınamadı |
| Stack sınırı | İkinci Öğrenim | Reddedildi, hiçbir şey değişmedi |
| Çift tıklama / tekrarlı seçim | Canlı teklifte aynı ödül ve diğer kart ikinci kez | Tek etki, 1 / 1 |
| Teklifte olmayan ödül | Bekleyen teklifte olmayan Öğrenim istendi | Reddedildi |
| Uygulama sırasında hata | İkinci modifier eklenirken dinleyici hata verdi | İlk modifier geri alındı; çarpan, hak, stack, kayıt değişmedi; bir hata mesajı |
| Koşul alınırken | Teklif sonrası davranışlar kaldırıldı | Adanış reddedildi, bedel ödenmedi |
| Davranışsız düzende Adanış | 15 boss × 200 seed | Hiç sunulmadı |
| Dört davranışın her biri | Tek saksıda patlama / elektrik / kasırga / bumerang | Her biri tek başına Adanış'ı açtı |
| R23 öncesinde sunulmama | 6.300 sabit durum teklifi + canlı run | R3 … R20'de hiç yok; R23'ten itibaren var |
| Ayrı garanti yok | R23 × 400 seed | Diğer güçlü ödüllerle aynı sıklık |
| Hasar ayrımı | Formül + gerçek vuruşlar, üç kol × dört davranış + doğrudan | 2. bölümdeki tablo |
| Artçı / yankıda çift katsayı | Adanış + Artçı Patlama; Adanış + Çifte Akım | İkinci darbe 150 (225 değil) |
| Uzmanlaşma, tırpan, boss çarpanları | Davranış Ustası, Usta Biçici, tırpan ×1,40, Keskin Bıçak, Altın Hedef, Hasat Ritmi, Yıkım Gücü | Her katsayı bir kez |
| Yeni run / restart / menü | Aynı sahnede yeni run; "Restart" düğmesi; ana menüye dönüş | Hak 3, çarpanlar 1, sayaçlar 0, modifier kalmadı |
| Eski havuz ve profil | Kırılma V1 sahnesi; bütün eski ödül asset'leri; eski havuzlar | Yeni alanlar etkisiz; bedelli ödül hiç sunulmadı; level 3 hak |
| Metinler | R23 kartları, HUD, alınan ödül listesi, run sonu | Kazanç / bedel ayrı; "3 → 4", "3 → 2" gerçek değerden |
| Kart yazıları sığıyor mu | Canlı tekliflerdeki bütün kartlar | Kesilme ve taşma yok |
| Gameplay RNG'si | Bütün teklif üretimleri, önce / sonra | Değişmedi |
| Tam run | R1–50, R23'te Öğrenim; level'lar R23, 24, 30, 50 | 15 boss, zafer; 3 + 4 + 4 + 4 = 15 hak, 15 kart |

### Etkilenen eski testler

| Test | Neyi koruyor | Sonuç |
|---|---|---|
| OdulAsamalariV1Verification | Aşamalı havuz, eski havuzların kayıtlı teklif dizisi (6.480 teklif) | PASS · 71 |
| BossRewardVerification | Eski boss ödülü akışı, kart yazıları | PASS · 127 |
| KirilmaV1Verification | Level başına 3 seçim, kart akışı, kırılma ödülleri, HUD | PASS · 189 |
| TakvimV1Verification | 15 boss takvimi, R50 sırası | PASS · 162 |
| DengeV1Verification | Denge V1 ödülleri (davranış ve nadirlik çarpanı) | PASS · 86 |
| SpecializationVerification | Uzmanlaşma çarpanları, kasırga | PASS · 100 |
| StartLoadoutVerification | Başlangıç seçimi çarpanları | PASS · 88 |
| MechanicsVerification | Profilsiz sahne, kart seçimi | PASS · 88 |
| HarvestBehaviorVerification | Dört davranış | PASS · 114 |
| ElectricRevertVerification | Elektrik, yeniden başlatma | PASS · 40 |
| SimpleResonanceVerification | Rezonans davranış çarpanı | PASS · 293 |
| ContactRefactorVerification | Temas kuralı | PASS · 175 |
| RunPrototypeVerification | Eski run, zafer, kayıp, menü | PASS · 69 |
| BossWeatherVerification | Boss hava efekti | PASS · 22 |
| RoundPreviewVerification | Önizleme ekranı | PASS · 47 |
| Run50ReferenceVerification | Run50 referans profili | PASS · 70 |
| OptionsMenuVerification | Ayarlar menüsü | PASS · 22 satır |

Kontrol sayıları 3.7.3'teki turla aynı. Bu turda düşen test olmadı.

**Aşamalı havuzun teklif dizisi de aynı.** `RewardOfferMeasurement.RunDistribution` (Ödül Aşamaları V1, 90.000 teklif) yeni kodla
yeniden çalıştırıldı; çıktısı 3.7.3'te kaydedilen `RewardOfferDistribution.csv` ile bayt bayt aynı.

**Geliştirme sırasında düşenler** (kod değil, test ve etiket hataları; son turdan önce düzeltildi):

1. Menü satırının adında "/" vardı ("kazanç / bedel"); Unity bunu alt menü sayıyor. Ad değiştirildi.
2. Yeni testin R23 düzeneği teklifin seed'ini teklif hazırlandıktan sonra değiştiriyordu; düzenek düzeltildi.

### Bilerek değişen eski beklentiler

1. `OdulAsamalariV1Verification`: başlık beklentisi "GÜÇLÜ ÖDÜLLER" → "GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ" (erken ve orta için de). Senin
   istediğin sunum düzeltmesi.
2. `KirilmaV1Verification`: "Kırılma V1'in denge setini kullanan profiller" listesine `Run50_BedelliOdullerV1` eklendi.
3. `TakvimV1Verification`: "Takvim V1'in takvimini aynen taşıyan profiller" listesine `Run50_BedelliOdullerV1` eklendi.

Başka hiçbir eski beklenti değişmedi.

### Üretici kontrolleri

`make_denge_v1.py`, `make_kirilma_v1.py`, `make_takvim_v1.py`, `make_odul_asamalari_v1.py` ve `make_bedelli_oduller_v1.py` için
`--check` temiz; `test_generation_guard.py` geçti.

### Dokunulmayanlar

- **Oyuncu kaydı:** `meta.json` paketin başında ve sonunda aynı (hash ile; son değişiklik 1 Ekim 19:15).
- **Seçili profil:** `Assets/Resources/RunProfileSelection.asset` paketin başında ve sonunda aynı (hash ile). Başladığımda
  `Run50_OdulAsamalariV1` seçiliydi (dosya 18:03'te değişmiş; ben yazmadım) ve öyle duruyor.
- **Editör:** açık Unity editörüne dokunmadım; bütün testler izole kopyada çalıştı. Yeni script ve asset dosyaları gerçek
  projeye yazıldığı için editör açıksa onları kendisi içe aktarır.
- **Git:** HEAD hâlâ `28e4650`; commit yok (3.7.2 ve 3.7.3'ün değişiklikleri de commit'siz duruyor).

### Log yolları

| Ne | Nerede |
|---|---|
| Sonuç dosyaları | `Docs/Bolum3-7-4/testler/` (`TopluSonuc.txt` özet) |
| Kontrollü ölçüm | `Docs/Bolum3-7-4/olcum/` |
| Görüntüler | `Docs/Bolum3-7-4/ekran/` |
| Ham Unity logları | `Library/VerificationProject/Logs/*.log` |

Yeniden çalıştırmak:

```powershell
Tools/run-isolated-verification.ps1 -Method BedelliOdullerV1Verification.RunBatch -Full
```

```powershell
Tools/run-isolated-verification.ps1 -Method BedelliOdulMeasurement.Run -Full
```

## 5. Ekran görüntüleri

Gerçek sahneden, batch'te kameradan çizilmiş görüntüler (`Docs/Bolum3-7-4/ekran/`). Hepsine baktım.

| Dosya | Ne gösteriyor |
|---|---|
| `T374_01_R23_IkiBedelliOdul.png` | R23 ödül ekranı: "GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ"; iki bedelli kart yan yana (kazanç / bedel, geçerlilik notu, dışlama satırı) ve sıradan bir kart |
| `T374_02_R24_HUD_Ogrenim.png` | Öğrenim alındıktan sonraki round: kota kartında "Level başına seçim: 4 (temel 3)" ve ödül listesinde kazanç + bedel |
| `T374_03_R26_AlinanOdulListesi.png` | Sonraki boss'un ödül ekranı: altta "Alınan boss ödülleri" listesinde Öğrenim satırı; teklifte iki bedelli ödül yok |
| `T374_04_RunSonu_KazancBedel.png` | Run sonu: ödül listesinin altında kazanç / bedel satırı ve level başına seçim |
| `T374_05_R24_HUD_Adanis.png` | İkinci run, Adanış alındıktan sonra: "Level başına seçim: 2 (temel 3)" |
| `T374_06_OdulAsamalariV1_YeniBaslik.png` | Ödül Aşamaları V1 profilinde yeni başlık (3.7.3 testinden) |
| `T374_07_EskiProfil_KirilmaV1.png` | Eski profil: başlık ve kartlar eskisi gibi (3.7.3 testinden; kartlar teste elle konmuştur) |

Notlar:

- `T374_01`'de iki bedelli ödülün aynı teklifte çıkması için bu boss'un seed'i testte seçildi (teklif oyunun kendi koduyla
  üretildi). Kural kontrolleri bundan bağımsız yapıldı.
- Görüntülerde altın 80 ve level 2–3 görünüyor: testte oyuncu vuruşu yok, skor ve level elle veriliyor.
- Run sonu ekranında ödül listesi uzun olduğu için bütün blok küçülüyor; kazanç / bedel satırı okunuyor ama küçük.

## 6. Arayüz

- **Kart (bedelli ödül):** "KAZANÇ" ve "BEDEL" ayrı bloklarda, yeşil ve kırmızı. Satırlar ödül verisinden üretilir.
  "3 → 4" o anki gerçek seçim hakkından yazılır: hak 4 iken −1 veren bir ödül "4 → 3" yazar.
- **Kartın altı:** "Önceden kazanılmış seçimler değişmez." + ödülün kısa açıklaması; "Bir kez alınır" ve "Bunu alırsan
  Davranışa Adanış bir daha sunulmaz" (ad, havuzdaki dışlama grubundan gelir).
- **HUD:** hak temel değerden farklıysa kota kartında tek satır: "Level başına seçim: 4 (temel 3) · yeni kazanılan
  level'lar". Metin yalnız değer değişince yeniden kurulur. Hak 3 iken satır yok; eski profillerin HUD'u aynı.
- **Ödül listesi (HUD ve ödül ekranı):** "Bereketli Öğrenim 1/1 · Level seçimi +1 · Doğrudan ×0,80".
- **Run sonu:** "Bereketli Öğrenim → kazanç: level başına seçim +1 · bedel: doğrudan vuruş hasarı ×0,80" ve
  "Level başına seçim: 4 (temel 3) · yeni kazanılan level'lar".
- **Başlık (3.7.3 düzeltmesi):** "ERKEN / ORTA / GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ". Mevcut aşama tükenip alt aşama kartları geldiğinde de
  doğru kalır. Eski (aşamasız) profillerin başlığı değişmedi.

## 7. Veri dosyaları ve ayar kaynağı

| Dosya | Ne | Kaynak |
|---|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset` | Profil | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/BereketliOgrenim_B1.asset` | Bereketli Öğrenim | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/DavranisaAdanis_B1.asset` | Davranışa Adanış | Üretici çıktısı |
| `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/BossRewardPool_BedelliOdullerV1.asset` | Aşamalı havuz (16 + 2 ödül) | Üretici çıktısı |
| `Tools/Balance/BedelliOdullerV1/bedelli_oduller_v1_params.py` | **Tek düzenleme kaynağı** | Elle |
| `Tools/Balance/BedelliOdullerV1/make_bedelli_oduller_v1.py` | Üretici (`--check`, `--report`) | — |
| `Tools/Balance/BedelliOdullerV1/generated_manifest.json` | Çıktıların hash kaydı | Üretici |
| `Tools/Balance/BedelliOdullerV1/README.md` | Veri sahipliği ve akış | — |

- Profil, Ödül Aşamaları V1'in profilinden yalnız üç satırda ayrılıyor: ad, zafer başlığı, ödül havuzu.
- Havuz, Ödül Aşamaları V1'in havuzuna güçlü aşamada iki satır ekliyor; 16 mevcut ödül aynı asset'lere referans.
- Aşamalar ve mevcut ödüllerin dağılımı `odul_asamalari_v1_params.py` dosyasından, takvim ve hedefler `takvim_v1_params.py`
  dosyasından okunur; kopya yok. O dosyalar değişirse bu profil de değişir.
- Inspector'da yapılan değişiklik üreticiyi durdurur (manifest farkı); kalıcı olacaksa önce parametre dosyasına taşınır.
- Üretici, tek başına bile aralığı aşan (hiç sunulamayacak) bir seçim hakkı değişimini hata sayar.

## 8. Benim yorumlarım (prompt'ta açık olmayan yerler)

1. **Koşul yalnız bedeli olan ödülde alınırken yeniden aranıyor.** Eski ödüllerde koşul, eskisi gibi yalnız teklif
   hazırlanırken aranıyor. Sebep: eski ölçüm aracı ve eski testler ödülü teklife elle koyup alıyor; kural hepsine genişlese
   onlar bozulurdu. Oyunda fark yok (teklif, ödül seçim ekranında sabitlenir; arada tarla değişmez).
2. **HUD satırı yalnız hak temelden farklıyken görünüyor.** Hak 3 iken satır yok. Her zaman görünmesini istersen tek satırlık
   değişiklik.
3. **Kart metinleri.** "KAZANÇ" / "BEDEL" başlıkları, "Bir kez alınır", "Bunu alırsan … bir daha sunulmaz" ve iki ödülün kısa
   açıklamaları ("Davranış hasarı değişmez." · "Patlama · elektrik · kasırga · bumerang. Kendi vuruşun değişmez.") benim yazdığım
   metinler. İstediğin iki satır ("Gelecekteki her level: 3 → 4 seçim", "Doğrudan vuruş hasarı ×0,80") aynen duruyor.
4. **Dışlama grubunun adı** (`secim_hakki_takasi`) yalnız veri kimliği; arayüzde görünmez.
5. **"Alınan kart" sayacı** yalnız gerçekten alınan kartı sayar; atlanan (takas edilen) seçim sayılmaz.
6. **Menü satırı:** "Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)".
7. **Ölçüm için oyun koduna bir olay eklendi** (`PlantHealth.AnyDamaged`): bitkiye uygulanan her hasarı türüyle bildirir.
   Oynanış ona bağlı değil; yalnız test ve ölçüm dinliyor.

## 9. Kısa Play kontrolü (10–15 dakika)

1. Menü: **Tools → Run Profili → Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)**. Play.
2. R23'e kadar oyna (ilk iki ödül burada çıkabilir; garanti değil, birkaç boss sürebilir).
3. Kartta "KAZANÇ" ve "BEDEL" ayrı mı, "3 → 4" / "3 → 2" doğru mu, "Bunu alırsan … bir daha sunulmaz" anlaşılıyor mu?
4. Bereketli Öğrenim'i al. Sonraki round'da kota kartında "Level başına seçim: 4 (temel 3)" yazmalı; ilk level'da dört
   ayrı kart seçimi gelmeli (her birinde üç aday). Ödülü almadan önce kazandığın level'lar üçer seçim vermiş olmalı.
5. Vuruşların zayıfladığını hissediyor musun (×0,80)? Davranış hasarı aynı kalmalı.
6. Sonraki boss'larda Davranışa Adanış bir daha çıkmamalı.
7. Ayrı bir run'da Adanış'ı al: patlama / elektrik sayıları ×1,50 büyümeli; level başına iki seçim gelmeli.
8. Tarlada hiç davranış yokken Adanış gelmemeli.
9. Bakılacak his soruları: bedel ağır mı, hafif mi; dört seçim menüyü uzatıyor mu; iki ödül arasında gerçek bir karar var mı?
10. Run sonu ekranında alınan ödülün kazancı ve bedeli okunuyor mu (yazı küçük kalabilir)?

Seçili profilini ben değiştirmedim.

## 10. Açık kalan işler

**Denge (bu pakette bilerek yapılmadı)**

- İki ödülün katsayıları (×0,80, ×1,50, ±1) ilk test değeri. Ölçüme göre ayarlanmadı.
- XP yatırımının uzun vadeli değeri ölçülmedi: fazladan kartların kaç round'da geri ödediği, R50 ve endless'taki karşılığı (P7 / P8).
- Adanış'ın değeri davranış ödüllerinin gücüne bağlı; Artçı Patlama ve Çifte Akım ayarı açık (P5).
- Güçlü aşamada artık 8 ödül var; iki yeni ödül birbirini dışladığı için alınabilir stack 7'den 8'e çıktı (9 güçlü boss'a
  karşı). Geç boss'lardaki tükenme (Bölüm 3.7.3) bu profilde yeniden ölçülmedi; içerik sayısı sorunu duruyor (P11).

**İçerik ve sistem**

- Başka bedel türü yok: hız kilidi, davranış kapatma, skor dönüşümü, zincir eklenmedi.
- "Zaten kapalı / kullanılmayan şeyi bedel gösteren ödülü filtreleme" yalnız iki yönüyle var: çakışan ödül ve seçim hakkı
  sınırı (ve kazancı kullanılamayan Adanış). Bir özelliği kapatan ödül türü olmadığı için genel filtre de yok.
- Stat modifier'ı bedel olarak kullanılırsa kart satırının kazanç / bedel sınıflandırması eklenmeli.

**Doğrulanamayanlar**

- İnsan gözüyle Play onayı yok. Görüntüler batch'te kameradan çizildi.
- Kart seçim ekranı kaçıncı seçimde olduğunu göstermiyor (ör. "2 / 4"); istenmedi, eklemedim.
- Run sonu ekranında ödül listesi uzunken bütün blok küçülüyor; kazanç / bedel satırı okunuyor ama küçük.
- Bot ölçüm aracı (`BalanceRunMeasurement`) bu profille çalıştırılmadı; ödül seçim politikası bedelli ödülleri tanımıyor.

## 11. Değişen dosyalar

**Oyun kodu**

| Dosya | Ne değişti |
|---|---|
| `Assets/Scripts/ScriptableObjects/BossRewardSO.cs` | `levelChoiceDelta`, `exclusiveGroup`, "bedeli var mı" sorgusu |
| `Assets/Scripts/Managers/BossRewardManager.cs` | Alınabilirlik denetimi (`BlockOf`), dışlama, seçim hakkı aralığı, tek işlemli uygulama ve geri alma, seçim hakkı değişimi |
| `Assets/Scripts/ScriptableObjects/BossRewardPoolSO.cs` | Aynı dışlama grubundaki ödülleri bulma |
| `Assets/Scripts/Managers/RunPower.cs` | Level başına seçim hakkı hesabı ve aralık sabitleri |
| `Assets/Scripts/Managers/RoundManager.cs` | Temel hak / aktif hak ayrımı, "alınan kart" sayacı |
| `Assets/Scripts/UI/CardUI/CardSelectionUI.cs` | Alınan kartı sayaca bildirir |
| `Assets/Scripts/UI/BossRewardText.cs` | Kazanç / bedel satırları, kısa ve uzun özet, yeni aşama başlığı |
| `Assets/Scripts/UI/BossRewardPanelUI.cs` | Bedelli kart düzeni |
| `Assets/Scripts/UI/inGameUIs/QuotaHUD.cs` | Level başına seçim satırı |
| `Assets/Scripts/UI/RoundComplateUI/RunComplateUI.cs` | Run sonunda kazanç / bedel satırları |
| `Assets/Scripts/Plants/PlantHealth.cs` | Ölçüm olayı (`AnyDamaged`) |
| `Assets/Scripts/Managers/TornadoManager.cs` | Yedek yol ortak hesabı kullanır |
| `Assets/Editor/RunProfileMenu.cs` | Yeni menü satırı |

**Veri, araçlar, testler**

| Dosya | Ne |
|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset` (+ `.meta`) | Yeni profil |
| `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/` (+ `.meta`) | İki ödül ve havuz |
| `Tools/Balance/BedelliOdullerV1/` | Üretici, parametreler, manifest, README |
| `Tools/Verification/Editor/BedelliOdullerV1Verification.cs` | Yeni test |
| `Tools/Verification/Editor/BedelliOdulMeasurement.cs` | Yeni: kontrollü üç kollu ölçüm |
| `Tools/Verification/Editor/OdulAsamalariV1Verification.cs` | Yeni başlık beklentisi |
| `Tools/Verification/Editor/KirilmaV1Verification.cs`, `TakvimV1Verification.cs` | Profil listesi beklentisi |

Sahne, prefab, eski profiller, eski havuzlar ve eski ödül asset'leri değişmedi.

## 12. Ana TODO'da işaretlenenler

- **P4:** yedi maddenin altısı kapatıldı. "Kullanılmayan / zaten kapalı şeyi bedel gösteren ödül filtrelensin" maddesi **açık**
  (kısmi): o türde bir bedel henüz yok.
- **P3:** "uygun ödül tükenmesi ve kapatılan özellik" maddesi açık kaldı; nota bu paketin yalnız **çakışan ödül ve seçim
  sınırı** tarafını tamamladığı yazıldı. Genel davranış kapatma / kısıtlama desteği yapılmadı.
- **P0** ("iki temsilci ödülün sözleşmesi") ve **P11** ("+1 ve −1 içerikleri") maddelerine not düşüldü, kutular kapatılmadı.
- P5 ve sonrasına geçilmedi.
