# Bölüm 3.7.2 — 15 boss takvimi ve değişken kota dönemleri

**Durum (2026-10-02):** P2'nin oynanabilir takvim altyapısı uygulandı ve izole kopyada doğrulandı. Yeni aday profil
`Run50_TakvimV1` çalışıyor. Bu bir **takvim prototipidir**: hedef sayıları eski tablodan aktarılmış geçici değerlerdir, denge
ölçümü yapılmadı, insan Play kontrolü yapılmadı. Commit atılmadı; seçili profilin (`Run50_KirilmaV1`) ve oyuncu kaydın değişmedi.

Ana plan: [TODO-Run50-Endless-20261002.md](TODO-Run50-Endless-20261002.md)

## Kısa sonuç

- **Takvim artık profil verisi.** Boss tarihleri ve kota dönemleri profilden okunuyor; hepsini tek bir sınıf (`RunCalendar`) çözüyor.
  HUD, round yöneticisi, olay yöneticisi, ödül ve uzmanlaşma yöneticileri ve ölçüm aracı aynı hesabı kullanıyor.
- **`Run50_TakvimV1`:** Kırılma V1'in oynanış ayarları + 15 boss (R3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50).
  Kota dönemleri boss aralıklarını izliyor.
- **R1–50 sırayla oynanarak denendi:** tam 15 boss aktivasyonu, listedeki round'lar dışında aktif boss yok, R50 atlanmıyor.
- **R50 geçici akışı çalışıyor:** koşullar → bekleyen level kartları → boss ödülü → zafer ekranı. Başarısızlıkta kart ve ödül
  açılmadan kayıp ekranı geliyor. R51 başlamıyor. Bu, nihai final tasarımı **değildir** (5. bölüm).
- **Eski profiller aynı:** takvim alanı boş olan dokuz profil eski formüllerle birebir aynı sonucu veriyor; eski testler geçiyor
  (7. bölüm).
- **Eski bir testin bir beklentisi bilerek değişti** (Kırılma V1 testi; 7. bölüm).

## 1. Uygulanan takvim ve 15 dönemlik hedef tablosu

Profil: `Assets/ScriptableObjects/RunProfiles/Run50_TakvimV1.asset`. Her dönemde boss ilk round'un hazırlığından itibaren
gösterilir ve yalnız dönemin son round'unda aktiftir. Kota skoru dönem boyunca, boss hasadı yalnız boss round'unda sıfırdan
birikir. Dönem sonunda ikisi de gerekir.

| Dönem | Round'lar | Boss round'u | Kota (dönem skoru) | Boss hasadı hedefi (yalnız boss round'u) |
|---|---|---|---|---|
| 1 | 1–3 | R3 | 27 | 5 |
| 2 | 4–6 | R6 | 40 | 10 |
| 3 | 7–10 | R10 | 88 | 16 |
| 4 | 11–13 | R13 | 90 | 24 |
| 5 | 14–16 | R16 | 104 | 33 |
| 6 | 17–20 | R20 | 176 | 45 |
| 7 | 21–23 | R23 | 180 | 57 |
| 8 | 24–26 | R26 | 220 | 72 |
| 9 | 27–30 | R30 | 400 | 100 |
| 10 | 31–33 | R33 | 720 | 172 |
| 11 | 34–36 | R36 | 1.040 | 276 |
| 12 | 37–40 | R40 | 2.240 | 500 |
| 13 | 41–43 | R43 | 2.700 | 680 |
| 14 | 44–46 | R46 | 3.000 | 860 |
| 15 | 47–50 | R50 (final kaydı) | 4.800 | 1.100 |

**Bunlar dengelenmiş hedef değildir.** Kazanma oranına bakılıp ayarlanmadı; yalnız eski tablonun yeni dönem sınırlarına
aktarılmış hâlidir (2. bölüm).

**Kırılma V1'den aynen gelenler** (aynı asset'e referans, kopya değil): denge seti (temel stat, can, XP, ağaç, başlangıç
kilitleri), boss havuzu (Don, Sert Kabuk, Sis), ödül havuzu (13 küçük + 3 kırılma ödülü), level başına 3 seçim, 80 altın
başlangıç, 45 sn sabit round. Paylaşılan denge seti değişmedi. Hasar, XP ve ödül gücü azaltılmadı.

**Beklenen yan etki:** 15 boss, 9 yerine 15 ödül demek. Bu profil Kırılma V1'den daha çok ödül verir; bunu telafi eden bir
ayar yok (prompt'taki karar).

## 2. Eski değerlerden aktarım hesabı

Kaynak: Kırılma V1'in (DengeV1 ile aynı) tabloları.

- Eski kotalar (5 round'luk): 45, 110, 150, 220, 300, 500, 1.200, 2.800, 4.500, 6.000. Toplam 15.825.
- Eski boss hedefleri (R5 … R45): 8, 16, 30, 45, 65, 100, 220, 500, 800.

**Kota.** Her eski kota kendi beş round'una eşit paylaştırılır; yeni dönem, kapsadığı round'ların paylarını toplar. Tam sayılar,
kümülatif toplam yuvarlanıp farkı alınarak üretilir.

| Eski segment | Round'lar | Eski kota | Round başına pay |
|---|---|---|---|
| 1 | 1–5 | 45 | 9 |
| 2 | 6–10 | 110 | 22 |
| 3 | 11–15 | 150 | 30 |
| 4 | 16–20 | 220 | 44 |
| 5 | 21–25 | 300 | 60 |
| 6 | 26–30 | 500 | 100 |
| 7 | 31–35 | 1.200 | 240 |
| 8 | 36–40 | 2.800 | 560 |
| 9 | 41–45 | 4.500 | 900 |
| 10 | 46–50 | 6.000 | 1.200 |

| Yeni dönem | Hesap | Kota | Kümülatif |
|---|---|---|---|
| 1–3 | 3 × 9 | 27 | 27 |
| 4–6 | 2 × 9 + 22 | 40 | 67 |
| 7–10 | 4 × 22 | 88 | 155 |
| 11–13 | 3 × 30 | 90 | 245 |
| 14–16 | 2 × 30 + 44 | 104 | 349 |
| 17–20 | 4 × 44 | 176 | 525 |
| 21–23 | 3 × 60 | 180 | 705 |
| 24–26 | 2 × 60 + 100 | 220 | 925 |
| 27–30 | 4 × 100 | 400 | 1.325 |
| 31–33 | 3 × 240 | 720 | 2.045 |
| 34–36 | 2 × 240 + 560 | 1.040 | 3.085 |
| 37–40 | 4 × 560 | 2.240 | 5.325 |
| 41–43 | 3 × 900 | 2.700 | 8.025 |
| 44–46 | 2 × 900 + 1.200 | 3.000 | 11.025 |
| 47–50 | 4 × 1.200 | 4.800 | **15.825** |

Eski kotaların hepsi 5'e bölündüğü için paylar tam sayı çıktı; yuvarlama hiçbir değeri değiştirmedi. R10, R20, R30, R40 ve R50
sonundaki kümülatif bütçe eski tabloyla aynı (155, 525, 1.325, 5.325, 15.825).

**Boss hasadı.** Eski (round, hedef) noktaları arasında doğrusal ara değer; en yakın tam sayıya yuvarlanır, en az 1.

| Boss round'u | Kullanılan noktalar | Ara değer | Hedef |
|---|---|---|---|
| R3 | R0 = 0 ile R5 = 8 | 4,8 | 5 |
| R6 | R5 = 8 ile R10 = 16 | 9,6 | 10 |
| R10 | eski nokta | 16 | 16 |
| R13 | R10 = 16 ile R15 = 30 | 24,4 | 24 |
| R16 | R15 = 30 ile R20 = 45 | 33 | 33 |
| R20 | eski nokta | 45 | 45 |
| R23 | R20 = 45 ile R25 = 65 | 57 | 57 |
| R26 | R25 = 65 ile R30 = 100 | 72 | 72 |
| R30 | eski nokta | 100 | 100 |
| R33 | R30 = 100 ile R35 = 220 | 172 | 172 |
| R36 | R35 = 220 ile R40 = 500 | 276 | 276 |
| R40 | eski nokta | 500 | 500 |
| R43 | R40 = 500 ile R45 = 800 | 680 | 680 |
| R46 | R40–R45 eğimi (60 / round) devam | 860 | 860 |
| R50 | R40–R45 eğimi (60 / round) devam | 1.100 | 1.100 |

**Benim yorumum (R46):** prompt eğimi devam ettirmeyi yalnız R50 için söylüyordu. R46 da son eski noktanın (R45) ötesinde
olduğu için aynı kuralı ona da uyguladım. İstemezsen tek sayı (`takvim_v1_params.py`).

Hesap iki yerde bağımsız yapılıyor ve karşılaştırılıyor: üreticide (`Tools/Balance/TakvimV1/transfer.py`) ve testte (C#).
Tabloyu yeniden yazdırmak: `python Tools/Balance/TakvimV1/make_takvim_v1.py --report`.

## 3. Mimari

**Tek takvim çözümü: `RunCalendar`** (`Assets/Scripts/Managers/RunCalendar.cs`). Profili canlı okur, veri kopyalamaz.

| Soru | Açık takvim (yeni alan dolu) | Eşit segment (alan boş: eski profiller, profilsiz sahne) |
|---|---|---|
| Round'un dönemi | tarihler arasındaki aralık | `segmentRounds`'luk segment (eski formül) |
| Dönem sonu / boss round'u | her tarih | segment sonu; boss havuzu varsa son segment hariç |
| Kota ve boss hedefi | tablodan, dönem sırasıyla | eski tablo ya da eğri |
| Son round'da kart ve ödül | açılır (son round boss round'u) | açılmaz (eski davranış) |

- **Yeni veri alanı:** `RunProfileSO.bossCalendar` (round, final işareti, isteğe bağlı boss). Boşsa profil eskisi gibi çalışır.
- **Kim kullanıyor:** `RoundManager` (dönem başı / sonu, kota, boss hasadı, R50 sırası), `SegmentEventDirector` (boss duyurusu
  ve aktivasyonu), `BossRewardManager`, `SpecializationManager`, `QuotaHUD`, `RoundSummaryUI`, `GameFeelDirector`,
  `SegmentEventText`, `BalanceRunMeasurement`.
- **Boss seçimi ve kuralları `RoundManager`'a taşınmadı.** Seçim hâlâ olay yöneticisinde ve havuzda; art arda aynı boss
  gelmeme kuralı aynı kod.
- **Taranan eski varsayımlar:** `round % 5`, `(round − 1) % segmentRounds`, `segment × segmentRounds`, "son segmentte boss
  yok" ve "son round'da seçim açılmaz". Oyun kodunda bunların hepsi artık takvimden geliyor. `HarvestQuota`'daki segment
  formülleri eşit segment için tek yerde duruyor; takvim onları çağırıyor.

**Doğrulama (geçersiz takvim düzeltilmez).** Kurallar: tarihler sıralı ve benzersiz, 1 ile run uzunluğu arasında, son tarih
run'ın son round'u, final işareti yalnız son tarihte, her dönem için kota (> 0) ve boss hedefi yazılı, boss havuzu dolu, eski
"segment olayları" listesi boş.

| Nerede | Ne olur |
|---|---|
| Oyun (`RoundManager.BeginRun`) | Run başlamaz; Console'a hatanın ne olduğunu söyleyen tek satır yazılır |
| Tools > Run Profili | Profil seçilmez, seçim olduğu gibi kalır, hata yazılır |
| Inspector'da düzenleme | Hata yazılır (`OnValidate`) |
| Üretici (`make_takvim_v1.py`) | Yazmadan durur |

Örnek mesaj: `Run başlatılmadı: '…' profilinin boss takvimi geçersiz — tarihler sıralı değil: 2. tarih (Round 3) bir
öncekinden (Round 6) küçük. Takvim otomatik düzeltilmez; profil verisi düzeltilince run başlar.`

**Ölçüm ve analiz araçları.**

| Araç | Durum |
|---|---|
| `BalanceRunMeasurement` (oyunu oynayan bot) | Ortak takvimi kullanıyor (dönem skoru ve kayıt dönemi). Tek run'lık araç denemesiyle bakıldı (aşağıda) |
| `RunSimulator` (model simülatör) | Açık takvimli profilde durur: "Bu profil desteklenmiyor" |
| `BossBalanceMeasurement` | Açık takvimli profilde durur: "Bu profil desteklenmiyor" |
| Python çözümleyicileri (`analyze_runs`, `analyze_boss`, `compare_groups`, `doc_measured`, `margins`, `progress_table`) | Açık takvimli profilin ölçüm dosyasında sonuç üretmeden durur: "Bu profil desteklenmiyor" |

Yani Takvim V1 için bugün hazır bir denge çözümleyicisi **yok**; sessizce 5 round hesabıyla tablo üreten bir yol da yok.

**Araç denemesi (denge ölçümü değil).** `BalanceRunMeasurement.RunTakvimSmoke`: bir bot run'ı, 13 round. Amaç yalnız aracın
dönemleri doğru kaydettiğini görmekti: dönem skoru R1, R4, R7 ve R11'de sıfırdan başladı; kota sütunu 27 / 40 / 88 / 90, boss
sütunu R3, R6, R10, R13'te 5 / 10 / 16 / 24 yazdı. Aynı dosya `margins.py`'ye verildiğinde "Bu profil desteklenmiyor" dedi.
Bu tek run'da bot hedefleri kat kat geçti (ör. R3 boss hasadı 44 / 5). Tek run'dan sonuç çıkarmıyorum; yalnız hedeflerin
dengelenmediğini hatırlatıyor.

**Veri sahipliği.** Üretici `Tools/Balance/TakvimV1/make_takvim_v1.py` yalnız profil asset'ini yazar; kaynağı
`takvim_v1_params.py`, kendi manifest'i var (`README.md`). DengeV1 ve Kırılma V1 üreticilerine ve çıktılarına dokunulmadı; üç
üreticinin `--check` kontrolü temiz.

## 4. Benim yorumlarım (prompt'ta açık olmayan yerler)

Her biri küçük bir değişiklikle çevrilebilir.

1. **Son tarih run'ın son round'u olmak zorunda.** "Her round tam bir döneme ait olsun" kuralını genel doğrulamaya da koydum;
   boss'suz bir kuyruk dönemi için karar olmadığından takvim onu kabul etmiyor.
2. **Açık takvimde tablolar eksiksiz yazılır.** Kota eksikse eski eğriye düşmek yerine hata verir. Boss hedefinde 0 geçerlidir
   ("o boss'ta hasat hedefi yok", eski anlam).
3. **Geçersiz takvimde ekranda mesaj yok.** Run başlamaz ve hata Console'a yazılır; oyuncuya dönük bir uyarı ekranı yapmadım.
4. **"Final" işareti şimdilik yalnız kayıt.** Takvimdeki bir tarihe ayrıca kendi boss'u atanabilir (boş: havuzdan). Takvim V1
   bunu kullanmıyor; yol test verisiyle denendi (5. bölüm).
5. **R50 sırası açık takvime bağlı.** Son round boss round'uysa kartlar ve ödül açılır. Eski profillerde son round'da seçim
   yine açılmaz.
6. **R50 ödül ekranının yazısı değişti:** "SON BOSS GEÇİLDİ · ÖDÜL SEÇ" ve "run burada biter · ödül run sonu listesine
   yazılır". Eski metin "etkisi Round 51 ve sonrası" diyecekti.
7. **Hazırlık ekranındaki kota cümlesi** açık takvimde "Kota her boss round'unun sonunda kontrol edilir" oldu (eski: "her 5
   round'da"). HUD başlığı "SEGMENT KOTASI" aynı kaldı.
8. **Ödüllerin round sınırları değişmedi** (kapsam dışı, P3). Sonuçları: "Round 25 boss'undan sonra çıkar" diyen iki ödül
   (Canavar Kesimi, Fırtına Bileği) bu takvimde ilk kez R26 boss'unda sunulur, çünkü R25'te boss yok; kart metni eski hâliyle
   duruyor. Kırılma ödülleri R10'dan, Bilgi Filizi R30'a kadar (iki tarih de takvimde var).
9. **Boss ve ödül zarı dönem numarasından türüyor** (eskiden segment numarası). Aynı seed'le Takvim V1'in boss ve teklif
   dizisi Kırılma V1'inkiyle aynı olmaz; Kırılma V1'in kendi dizisi değişmedi.

## 5. R50: geçici uygulama

**Bu, nihai final tasarımı değildir.** Canavar bitki finali, "tamamla / sınırsıza devam et" seçimi ve endless bu pakette yok;
P9 ve P10 açık.

Şu an olan:

- R50 takvimde ayrı bir **final kaydı** olarak duruyor (`final: 1`). Kuralı havuzdan gelen normal boss (bu testte Sert Kabuk).
- Aynı round'da tek boss kurulur: tarih başına bir olay. Testte R50'de aktif olay sayısı 1.
- Başarı sırası: kota ve boss hasadı tutar → bekleyen level kartları → boss ödülü → zafer ekranı. R50'de alınan ödül run sonu
  listesinde görünür.
- Başarısızlık: kart ve ödül açılmadan kayıp ekranı.
- Zaferden ya da kayıptan sonra R51 başlamaz.

**Final kaydına ayrı boss atama yolu (test verisiyle):** 6 round'luk bir test takviminde (R3, R6) final tarihine havuzda
olmayan ayrı bir boss atandı. R3 havuzdan geldi, R6'da atanan boss çalıştı, iki tarihte iki aktivasyon oldu, sonra ödül ve
zafer. Bu yalnız altyapının çalıştığını gösterir; oyunda böyle bir final boss'u yok.

## 6. Arayüz ve yaşam döngüsü

| Konu | Durum |
|---|---|
| HUD kalan round | Dönemin uzunluğuna göre: "SEGMENT KOTASI · 3 ROUND" … "SON ROUND" (4 round'luk dönemde 4'ten başlar) |
| HUD yaklaşan boss | "Yaklaşan boss: … · Round 6", "Boss hasadı hedefi (Round 6): 10" |
| Round özeti / hazırlık | "Yaklaşan boss: … · Round 3 · 2 round hazırlık", boss round'undan önce "SON HAZIRLIK"; boss sonrası "… GEÇTİ · Sıradaki boss: … (Round 6)" |
| Kota bildirimi | "İlk kota: 27 · 3 round"; dönem sonunda "KOTA TAMAM · Sıradaki kota: 40 · 3 round" |
| Harita | Yaklaşan boss'un bölgesi kesikli çerçeveyle; hücre sayısı gerçek bölgeyle aynı |
| Önizleme = aktif boss | Dönem boyunca aynı boss ve aynı bölge; aktifleşince değişmiyor |
| Boss bitince | Bölge çizgisi, hava efekti ve boss kuralının çarpanları temizleniyor; yeni dönemin boss'u hemen duyuruluyor |
| Kart + ödül aynı bitişte | Sıra aynı: önce kartlar, sonra ödül (R3 ve R50'de denendi) |
| Seçim bitmeden | Sonraki round başlamıyor (kart ve ödül ekranında denendi) |
| Yeniden başlatma, kayıp, menü | Boss, bölge, hava, teklif, ödül etkisi ve sayaçlar sıfırdan başlıyor |

## 7. Çalıştırılan testler ve loglar

Hepsi izole kopyada (`Library/VerificationProject`), `Tools/run-isolated-verification.ps1` ile, birer birer, varsayılan kısıtla
(düşük öncelik, 4 çekirdek). İşlev testi "çalışıyor" der; denge ya da his kanıtı değildir.

### Yeni takvim testi

`Tools/Verification/Editor/TakvimV1Verification.cs` — **PASS, 162 kontrol** (16:39–16:40, 60 sn).

**Nasıl oynuyor.** Round'lar sırayla ilerler, round numarası atlanmaz: her round oyunun kendi "sonraki round" yoluyla başlar.
Test yalnız kalan süreyi sıfırlar; round'u oyunun kendi sayacı bitirir. 45 saniye beklenmez. Oyuncu vuruşu kapalıdır; skor ve
level testin eliyle verilir (debug). **Bu yüzden denge kanıtı değildir.**

| İstenen kontrol | Nasıl denendi | Sonuç |
|---|---|---|
| R1–50 boyunca tam 15 boss aktivasyonu | Tam run, 50 round sırayla | 15 aktivasyon: R3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50 |
| Listedeki round'lar dışında aktif boss yok | Her round'da aktif olay ve boss kuralı çarpanları | Diğer 35 round'da aktif boss ve boss etkisi yok |
| Her round tam bir döneme ait | Veri kontrolü (50 round) + oyun içinde her round | 15 dönem, bitişik, çakışma yok |
| 3→4, 6→7, 10→11, 46→47 geçişleri | Tam run'ın içinde (14 geçişin hepsi) | Dönem skoru 0'dan başlıyor, toplam korunuyor, yeni boss doğru round için duyuruluyor |
| Kota eşitliği geçer | 15 dönemin hepsinde skor tam kotaya eşit | Geçti (15 / 15) |
| Kota bir altı kaybettirir | R3'te 26 / 27, boss hasadı 5 / 5 | Kayıp: "KOTA TUTMADI" |
| Boss hasadı eşitliği geçer | 15 boss'un hepsinde tam hedefe eşit | Geçti (15 / 15) |
| Boss hasadı bir altı kaybettirir | R6'da 9 / 10, kota 40 / 40 | Kayıp: "BOSS HASADI TUTMADI" |
| Yalnız biri tutmadığında doğru neden | Yukarıdaki iki run + ikisi birden (26 / 27 ve 4 / 5) | Üç ayrı neden ve üç ayrı ekran metni |
| Dönem skoru sıfırlanır, toplam korunur | Her dönem başında | Run sonunda toplam 15.825 = 15 kotanın toplamı |
| R50 kart / ödül / zafer sırası | R50'de bir level bekletilerek | Kartlar (3 seçim) → ödül → zafer |
| Çift ödül önlemi | Her boss'ta ikinci istek ve "ödülsüz devam" | 15 boss'ta 15 ödül; ikinci istek reddedildi |
| R50 başarısızsa | R1–49 geçildi, R50'de boss hasadı 1.099 / 1.100, bir level bekliyor | Kart ve ödül açılmadı, kayıp ekranı, R51 başlamadı |
| Yeniden başlatma temizliği | Zaferden ve üç kayıptan sonra oyunun kendi "Restart" düğmesiyle | Round 1, skor 0, ödül yok, yalnız ilk boss duyurulmuş |
| Menüye dönüş | R50 kaybından sonra oyunun kendi "Main Menu" düğmesiyle | Boss, bölge, teklif, ödül etkisi kalmadı |
| Hatalı takvim verileri | 13 bozuk takvim + menü + oyun içinde bir tane | Hepsi adını koyan bir hatayla reddedildi; veri düzeltilmedi |

Ek olarak: önizlenen boss ve bölgesinin aktifleşince aynı kalması (15 dönem), boss sırasında hava ve bölgenin açılıp sonrasında
kapanması, HUD metinleri (50 round), art arda aynı boss gelmemesi (boss sırası: Don → Sert Kabuk → Don → Sert Kabuk → Don →
Sert Kabuk → Don → Sis → Sert Kabuk → Sis → Don → Sis → Sert Kabuk → Sis → Sert Kabuk).

### Etkilenen eski paketler (son kodla)

| Test | Neyi koruyor | Sonuç |
|---|---|---|
| KirilmaV1Verification | Kırılma V1, HUD, bölge, ödüller, ağaç | **İlk çalıştırma FAIL**, beklenti güncellendikten sonra PASS · 189 (aşağıda) |
| DengeV1Verification | Denge V1 profili ve boss ritmi | PASS · 86 |
| BossRewardVerification | Eski boss ödülü akışı (5 round'luk ritim, son round'da ödül yok) | PASS · 127 |
| BossWeatherVerification | Boss hava efekti | PASS · 22 |
| RunPrototypeVerification | Eski run: Don bütün segment aktif, zafer, kayıp, menü | PASS · 69 |
| SpecializationVerification | Eski uzmanlaşma akışı | PASS · 100 |
| StartLoadoutVerification | Başlangıç seçimi | PASS · 88 |
| RoundPreviewVerification | Önizleme ekranı | PASS · 47 |
| Run50ReferenceVerification | Run50 referans profili ve simülatör girişi | PASS · 70 |
| MechanicsVerification | Profilsiz sahne (sahnedeki kota ayarı) | PASS · 88 |
| ElectricRevertVerification | Elektrik, yeniden başlatma | PASS · 40 |
| HarvestBehaviorVerification | Davranışlar (UzunRun130) | PASS · 114 |
| SimpleResonanceVerification | Rezonans (UzunRun130) | PASS · 293 |
| ContactRefactorVerification | Temas kuralı | PASS · 175 |
| OptionsMenuVerification | Ayarlar menüsü | PASS · 22 satır |

Kontrol sayıları 3.7.1'deki son turla aynı. Açıklanmamış FAIL yok.

### Bilerek değişen eski beklenti

`KirilmaV1Verification`: test "başlangıç kilidini yalnız `Run50_KirilmaV1` verir" diyordu ve ilk çalıştırmada düştü, çünkü
Takvim V1 aynı denge setini (aynı asset) paylaşıyor ve kilitler onunla geliyor. Bu, paketin istediği sonuç. Beklenti şöyle
güncellendi: "başlangıç kilidi yalnız Kırılma V1'in denge setinden gelir; bu seti yalnız `Run50_KirilmaV1` ve
`Run50_TakvimV1` kullanır". Başka bir profil bu seti kullanırsa test yine düşer. Başka hiçbir eski beklenti değişmedi.

### Üretici kontrolleri

`make_denge_v1.py --check`, `make_kirilma_v1.py --check`, `make_takvim_v1.py --check` temiz; `test_generation_guard.py` 3 / 3.

### Dokunulmayanlar

Paketin başında ve sonunda aynı (hash ile): `Assets/Resources/RunProfileSelection.asset` (seçim hâlâ `Run50_KirilmaV1`) ve
`meta.json` (son değişiklik 1 Ekim 19:15). Unity editörün açıktı; ben açmadım, kapatmadım, dokunmadım. Bütün testler izole
kopyada çalıştı. HEAD hâlâ `28e4650`; commit yok.

### Log yolları

| Ne | Nerede |
|---|---|
| Sonuç dosyaları | `Docs/Bolum3-7-2/testler/` (`TopluSonuc.txt` özet) |
| Görüntüler | `Docs/Bolum3-7-2/ekran/` |
| Ham Unity logları | `Library/VerificationProject/Logs/*.log` |

Yeniden çalıştırmak:

```powershell
Tools/run-isolated-verification.ps1 -Method TakvimV1Verification.RunBatch -Full
```

## 8. Görüntüler

Gerçek sahneden, batch'te kameradan çizilmiş görüntüler (`Docs/Bolum3-7-2/ekran/`). Sahada saksı yok: skor testin eliyle
verildiği için tarla boş.

| Dosya | Ne gösteriyor |
|---|---|
| `T372_01_R1_Hazirlik_Onizleme.png` | Run başı: "İlk kota: 27 · 3 round", "Yaklaşan boss: DON CEPHESİ · Round 3 … 2 round hazırlık"; haritada bölge kesikli çerçeveyle |
| `T372_02_R1_HUD_YaklasanBoss.png` | R1 HUD: "SEGMENT KOTASI · 3 ROUND", "Boss hasadı hedefi (Round 3): 5", "Yaklaşan boss … Round 3" |
| `T372_03_R3_SonHazirlik.png` | R3 öncesi hazırlık ekranı: "SON HAZIRLIK" |
| `T372_04_R3_AktifBoss.png` | R3 boss aktif: HUD boss renginde, bölge çizgisi düz, kar efekti |
| `T372_05_R4_Hazirlik_SiradakiBoss.png` | R3 sonrası: boss geçti, sıradaki boss Round 6 |
| `T372_06_R50_AktifBoss.png` | R50 boss aktif |
| `T372_07_R50_LevelKartlari.png` | R50 başarı: önce level kartları |
| `T372_08_R50_BossOdulu.png` | R50 ödül ekranı: "SON BOSS GEÇİLDİ · ÖDÜL SEÇ" |
| `T372_09_R50_Zafer.png` | Zafer ekranı: ödül listesi (R50'de alınan dahil) |
| `T372_10_R50_Kayip.png` | R50'de kayıp ekranı |

Hepsine baktım. `T372_02` ve `T372_04` skor verildikten sonra çekildi (HUD'da 22 / 27 ve 27 / 27 görünüyor).

## 9. Eski profillerin korunduğuna ilişkin kontroller

- **Formül eşitliği:** dokuz eski profilin (Deney22_20, Prototip10, Prototip10_DebugButce, Run50_BossPrototip, Run50_DengeV1,
  Run50_KirilmaV1, Run50_Referans, Uzmanlasma20, UzunRun130) her round'u için takvimin verdiği dönem, dönem başı / sonu, boss
  round'u, kota ve boss hedefi, testte elle yazılmış eski formüllerle karşılaştırıldı: hepsi aynı.
- **Eski zamanlamalar:** 3. segmentin boss'u R11'de duyurulur, R15'te aktif; 2. segmentin "bütün segment" olayı R6–10, R1'de
  duyurulur (eski Don).
- **Asset'ler:** hiçbir eski profil asset'i değişmedi (`git status`: yalnız yeni `Run50_TakvimV1.asset`). Kırılma V1'in 10
  kotası ve 9 boss hedefi yerinde.
- **Oyun içinde:** takvim run'larından hemen sonra aynı oturumda Kırılma V1 yüklendi: 5 round'luk segment, ilk boss R5, kota
  45, boss hedefi 8, R50'de boss yok, HUD metni eskisi gibi.
- **Eski testler:** yukarıdaki tablo. Eski Don'un bütün segment aktif olması `RunPrototypeVerification`, eski uzmanlaşma akışı
  `SpecializationVerification`, başlangıç seçimi `StartLoadoutVerification`, HUD ve önizleme `RoundPreviewVerification` ve
  `KirilmaV1Verification` ile.

## 10. Açık kalanlar ve doğrulanamayanlar

**Bu paketin dışında bırakılanlar (açık)**

- Canavar bitki finali, tamamla / endless seçimi, endless (P9, P10).
- Ödüllerin erken / orta / güçlü havuzlara ayrılması (P3). 15 boss'ta ödül sınırlarının (stack) geç oyunda tükenip
  tükenmediği gerçek build'lerle denenmedi; bu testte boş tarlada 15 boss'un hepsinde teklif vardı.
- Hedeflerin dengelenmesi (P8). Bu sayılarla kazanma oranı, süre ve zorluk ölçülmedi.

**Doğrulanamayanlar**

- **İnsan gözüyle Play onayı yok.** Görüntüler batch'te kameradan çizildi.
- **Gerçek hasatla oynanmadı.** Skor ve level testin eliyle verildi; oyuncu vuruşu kapalıydı. Boss kurallarının gerçek hasada
  etkisi (Don'un yavaşlatması, Sis'in daraltması) bu testte değil, eski boss testlerinde ölçülüyor.
- **45 saniyelik round beklenmedi.** Süre sıfırlandı; round'u oyunun kendi sayacı bitirdi.
- **Geçersiz takvimde ekran.** Run başlamıyor ve hata Console'da; oyuncunun ne gördüğü (boş sahne) elle bakılmadı.
- **Inspector'dan düzenleme hatası** (`OnValidate`) testle çalıştırılmadı; aynı denetim menü ve oyun yolunda çalıştı.
- **Run ortasında menüye dönüş** (ayarlar üzerinden) denenmedi; run sonu ekranındaki iki düğme denendi.
- **Ölçüm aracı Takvim V1'de yalnız 13 round denendi.** R50 sonundaki kart → ödül → zafer sırasını bot ile oynatmadım.

**Bilinen küçük nokta (bu paketten bağımsız)**

- Ödül ekranında Hasat Ritmi kartının alt satırı ("Seçince 1/1: …") kart genişliğinden taşıyor (`T372_08` görüntüsünde).
  3.6'dan beri var; dokunmadım.

## 11. Profili seçip oynama

1. Unity'de menü: **Tools → Run Profili → Run50 Takvim V1 · 50 round (15 boss takvimi prototipi)**.
2. Play. Menüden yeni run başlat; çiftçi ve tırpan seçimi eskisi gibi.
3. Bakılacaklar (10–15 dakika):
   - Hazırlık ekranı: "İlk kota: 27 · 3 round", "Yaklaşan boss: … · Round 3".
   - R1–R2 HUD: "SEGMENT KOTASI · 3 ROUND" → "2 ROUND"; R3'te "SON ROUND" ve boss aktif.
   - R3 sonu: önce kartlar (level varsa), sonra boss ödülü; özet ekranında "Sıradaki boss: … (Round 6)".
   - R4'te kota sayacı 0'dan başlamalı; R7'de dönem 4 round ("SEGMENT KOTASI · 4 ROUND").
   - Üç round'da bir boss ritmi ve ödül sıklığı nasıl hissettiriyor?
4. Eski profile dönmek için aynı menüden `Run50 Kırılma V1` (ya da başka bir profil).

Seçili profilini ben değiştirmedim: hâlâ `Run50_KirilmaV1`. Hedefleri değiştirmek için `Tools/Balance/TakvimV1/takvim_v1_params.py`
dosyasını düzenle, sonra `python Tools/Balance/TakvimV1/make_takvim_v1.py`.

## 12. Değişen dosyalar

**Oyun kodu**

| Dosya | Ne değişti |
|---|---|
| `Assets/Scripts/Managers/RunCalendar.cs` | Yeni: dönem ve boss round'u hesabı, takvim doğrulaması |
| `Assets/Scripts/ScriptableObjects/RunProfileSO.cs` | `bossCalendar` alanı, `BossDate`, takvime yönlendiren `HasBoss` / `BossTargetFor`, `OnValidate` |
| `Assets/Scripts/Managers/RoundManager.cs` | Dönem ve boss soruları takvimden; geçersiz takvimde run başlamaz; son round sırası (kartlar → ödül → zafer) |
| `Assets/Scripts/Managers/SegmentEventDirector.cs` | Boss duyurusu ve aktivasyonu takvimden; tarihe atanmış boss |
| `Assets/Scripts/ScriptableObjects/SegmentEventSO.cs` | Olay zamanlaması round'ları dışarıdan alır (eski kısa yollar duruyor) |
| `Assets/Scripts/Managers/HarvestQuota.cs` | `SegmentStart` (eşit segment formülü tek yerde) |
| `Assets/Scripts/Managers/BossRewardManager.cs` | Teklif dönemi takvimden; son boss'ta ödül (açık takvim) |
| `Assets/Scripts/Managers/SpecializationManager.cs` | Segment numarası takvimden |
| `Assets/Scripts/UI/inGameUIs/QuotaHUD.cs` | Boss satırı takvimden |
| `Assets/Scripts/Feel/RoundSummaryUI.cs` | Kota bildirimi dönem uzunluğuyla |
| `Assets/Scripts/Feel/GameFeelDirector.cs` | Round girişi kota yazısı dönem uzunluğuyla |
| `Assets/Scripts/UI/SegmentEventText.cs` | Dönem başı takvimden |
| `Assets/Scripts/UI/BossRewardPanelUI.cs` | Son boss'un ödül ekranı yazısı |
| `Assets/Editor/RunProfileMenu.cs` | Yeni menü satırı; geçersiz takvimli profil seçilmez |

**Veri**

| Dosya | Ne |
|---|---|
| `Assets/ScriptableObjects/RunProfiles/Run50_TakvimV1.asset` (+ `.meta`) | Yeni profil (üretici çıktısı) |

**Araçlar ve testler**

| Dosya | Ne değişti |
|---|---|
| `Tools/Balance/TakvimV1/` | Yeni: `takvim_v1_params.py`, `transfer.py`, `make_takvim_v1.py`, `generated_manifest.json`, `README.md` |
| `Tools/Balance/DengeV1/calendar_guard.py` | Yeni: çözümleyicilerin "desteklenmiyor" kontrolü |
| `Tools/Balance/DengeV1/analyze_boss.py`, `analyze_runs.py`, `compare_groups.py`, `doc_measured.py`, `margins.py`, `progress_table.py` | Birer satır: kontrolü çağırır |
| `Tools/Verification/Editor/TakvimV1Verification.cs` | Yeni test |
| `Tools/Verification/Editor/KirilmaV1Verification.cs` | Bir beklenti güncellendi (7. bölüm) |
| `Tools/Verification/Editor/BalanceRunMeasurement.cs` | Dönem hesabı ortak takvimden; `takvimsmoke` araç denemesi |
| `Tools/Verification/Editor/RunSimulator.cs`, `BossBalanceMeasurement.cs` | Açık takvimli profilde "desteklenmiyor" |

Sahne, prefab ve eski asset'ler değişmedi.

## 13. Ana TODO'da işaretlenenler

- **P2:** beş madde işaretlendi. "R50 finalini ayrı tanımlayabil" maddesi yalnız tanımlama altyapısı olarak kapandı; notu
  yanında. Kabul koşulu otomatik testle sağlandı; Play'de gözle onay bekliyor.
- **P0:** "kota dönemleri kararı" işaretlendi (karar senin prompt'undan). "Ayrı aday profil / denge seti" ve "R50 final
  koşulu ve ödül sırası" maddelerine durum notu eklendi, işaretlenmedi. Diğer P0 kararları açık.
- **P5:** ölçüm ön koşulu eklendi: Artçı Patlama'lı run'ların ±%10 oynaması incelenmeden küçük farklar yorumlanmayacak.
- P3 ve sonrasına geçilmedi.
