# Demo yol haritası — 22 Ekim 2026

Hedef: kasım bitmeden kaliteli demoyu çıkarmak, sonra gerçek geri bildirimle geliştirmek.
Sonraki tarihler: Steam Next Fest kaydı **10 Ocak 2027**'de kapanır, her şeyin incelemeye
gönderilmesi **8 Şubat**'a kadar, fest **22 Şubat – 1 Mart**.

Kullanım: maddeleri sırayla ver ("M3'ü yap" yeter). Sıra bağımlılığa göre kuruldu; bir
madde kendinden öncekilerin bitmiş olmasını varsayar. **[Şart]** demo için gerekli,
**[İyi olur]** zaman kalırsa. Zaman daralırsa önce [İyi olur] maddeleri düşer.

> **22 Ekim sonu güncellemesi:** "önce demo" planı bırakıldı. Odak tam oyun; demo sonra
> tam oyundan kesilecek. Aşağıdaki M0–M16 maddeleri referans olarak duruyor, yeniden
> planlanacak. Şu anki çalışma listesi hemen alttaki bölüm.

## Güncel açık işler — 23 Ekim 2026

Kullanıcının listesi. Sırayla, birlikte gidiyoruz ("B1'i yapalım" yeter). Biten madde `[x]` olur.
Hepsi 1.0'da kalacak; amaç görsel olarak sorunsuz ve net anlaşılır olmaları.

### Boss ödülleri (sıradaki iş: B1)

- [x] **B1 · Kritik Ark:** küçük kalan sıçrama görselini okunur yapmak. Elektriği büyütmek mi,
  farklı bir görsel mi kullanılacağı seçilecek.
  - **23 Ekim: uygulandı, kullanıcı onayladı.** Altın kavis ve iniş yıldızı,
    0,3 saniyede söner. Testler: CritArcArtVerification 15/15, OverflowArtVerification 17/17.
    Ayrıntı: `Docs/CritArc/README.md`.
- [x] **B2 · Taşan Hasar:** aktarılan hasarı alan hedeflerin hasar yazısı farklı renkte
  gösterilecek. Renk henüz seçilmedi.
  - **23 Ekim: uygulandı, kullanıcı onayladı.** Mor: hem yazı hem parça ve yıldız.
    Testler: Overflow 21/21, Kritik Ark 15/15, Anten 52/52. Ayrıntı: `Docs/Overflow/README.md`.
- [x] **B3 · Hasat Yankısı:** karşı hedefte darbe hissini güçlendirmek. Yukarıdan küçük
  meteor / enerji parçası önerisi var, tasarım kesinleşmedi. Görsel uğruna hasar geciktirilmeyecek.
  - **23 Ekim: mekanik ve YER TUTUCU görsel uygulandı; kullanıcının Play testi bekleniyor. Asıl toon VFX Blender'da
    ayrıca yapılacak.** Tek meteor, 1+ hasat, en çok 6 hücre, 6 sn bekleme, 0,2 sn düşüş, yıldız kayması rengi.
    Testler: yankı görsel testi 29/29 ve ana testin yankı adımları geçti. Tam ana test Hilal adımında duruyor
    (kullanıcının Hilal değişikliği; karar bekliyor). Ayrıntı: `Docs/HarvestEcho/README.md`.
  - **23 Ekim (devam): yer tutucu yerine Blender'da tasarlanan kuzey ışığı meteoru oyuna eklendi** (taş, alev izi, taç, toz,
    parçalar, krater). Yankı görsel testi 29/29. Kullanıcının Play testi ve his ayarı (düşüş süresi, sarsıntı, ses) bekleniyor.
  - **23 Ekim (devam):** Boş hücreye de tam çarpma (hasar yok); düşüş 0,2 → 0,55 → 0,5 sn; boyut değişmedi.
  - **24 Ekim: 2-3-4 katmanları eklendi (aç/kapa, testler 42/42); kullanıcı memnun, B3 kapandı.**
  - Bilinçli bırakılanlar: 6'dan fazla hasatta seçilen 6 hücre grid tarama sırasıyla (merkeze en yakın değil; kullanıcı
    "büyük bir şey değil" dedi). Ses hâlâ eski küçük "tok" (en sona).
  - **Kayıt: "açıklanamayan his eksikliği" incelemesi.** Her parça Geç Oyun Testi penceresinde aç/kapa
    düğmeli eklenecek; kullanıcı oynayarak karşılaştıracak. Kullanıcının önceliği 2-3-4, sesi sorun görmüyor:
    1. Ses: düşüşte vınlama yok, çarpma sesi küçük "tok". Kullanıcı: büyük ihtimalle sorun değil, en sona.
    2. **Ölü hareket:** alev kartı düşerken kıpırdamıyor, taş dönmüyor. Alev titremesi/dalgalanması, taş yuvarlanması eklenecek.
    3. **Darbe karesi:** taş yere değince ezilmiyor (düşerken uzama, çarpınca basılma) ve ilk karede kısa parlama yok.
       Hit-stop değil; zaman durmuyor.
    4. **Tarla tepkisi:** çevredeki bitkiler ve saksılar kıpırdamıyor. Yakındakilerde kısa esneme/sekme eklenecek.
    - Henüz değerlendirilmeyenler: gökte parıltıyla belirme (gölge istenmedi), kamera sarsıntısı (varsayılan kapalı).
    - Kurallar: yerel ve küçük, havuzlu, kare başına bellek yok; oynanış zamanlamasına dokunulmaz.
- [x] **B4 · Açgözlü Hasat:** parçaları beyaz imleç halkasının hemen dışına, aynı merkez
  çevresine almak. Boyut ve uzaklık halkayla orantılı. **24 Ekim: kullanıcı Play'de onayladı ("gayet tatmin etti").**
  - **Çıkıştan önce belki (kullanıcı fikri, 24 Ekim):** hak hazırken vuruş her şeye sorunsuz hasar verebilsin; ör. kaprislerin
    (vurulabilir olay hedefleri) bir vuruşta aldığı hasar 1 ise Açgözlü vuruşta 2 olsun. Şimdilik uygulanmadı.
  - 24 Ekim: kullanıcı yönü "aç ağız halkası" (Blender toon, `Docs/GreedJaw/`). Eski amber dişler ve kapanma nabzı
    kaldırıldı; ağız halkaya oturur ve halka yarıçapıyla ölçeklenir. İmleçle hareket (dişler yaylı yatar), dönen 5 mini diş,
    hap / kayıp animasyonları oyunda; animasyonlar saldırı hızına uyar; giriş halkadan uyanma (dudaklar köşelerden uzar, küçük duman pofları). İzole testi 52/52 geçti (tam döngü 0 bayt), izole sahne çekimleri `Docs/GreedJaw/unity/`.
- [x] **B5 · Aşırı Yükleme:** etkisini anlaşılır kılmak. Büyük yeni VFX istenmiyor;
  açıklama, küçük tetik vurgusu ve bekleme göstergesi değerlendirilecek.
  - 24 Ekim: toon “dolan tekrar oku” rozeti (fuşya) oyunda, bulut ve yazıların üstünde (kullanıcı seçimi B). Eski işaret
    kaldırıldı. İzole testi 84/84, karede 0 bayt; prototipler ve kalabalık sahne `Docs/Overload/b5/`. **24 Ekim: kullanıcı Play'de onayladı ("gayet tatmin edici, böyle kalabilir").**
    İsteğe bağlı, seçilmedi: kart metni (D), tekrarın yazı rengi (elektrik/kasırga/bumerang), patlama bulutunun hasar yazılarını örtmesi (mevcut).
- [ ] **B6 · Artçı dengesi — sonraya bırakıldı:** güçlü kart; hasarını törpülemek ve/veya
  oyuncuya hasar geliştirme imkânı vermek.

### Sistemler (beklemede)

- [ ] **S1 · Renk öğretim katmanı:** bütün ödüllerin renkleri oturduktan sonra tek seferde uygulanacak.
  Kullanıcı 23 Ekim'de "şimdilik tutalım" dedi.
  1. Ödül kartında efekt renginde anahtar kelime (ör. "*taşar*" mor).
  2. İlk tetiklenmede efektin yanında, kendi renginde, oyun boyunca **tek seferlik** isim etiketi (kayıtta tutulur).
  3. Round sonu özetinde renkli hasar dağılımı (ör. "Taşan Hasar: 12.400 (%8)").
  - Maskot karakter ve hikaye için ayrılır; öğretimi efektin yanındaki etiket yapar.
  - Kapalı testte sorulacak: "Mor sayılar neydi?"

### Tarla olayları ve vurulabilir hedefler

- [x] **O1 · Ortak hizalama:** modeller ve bölge görselleri saksılıyken ve saksısızken doğru
  hücre merkezinde. Hedef gittikten sonra kalan çemberler de kontrol edilir.
  - **25 Ekim: kullanıcı kapattı.** Diken, Filiz ve Kabuk'ta saksılı / saksısız yerleşim yapıldı; Gezgin Bereket şu hâliyle iyi.
- [x] **O2 · Ortak vurulma hissi:** geçerli darbe hareket, parlama ve sesle belirginleşir;
  normal vuruş ile son kırılma ayrışır.
  - **25 Ekim: kullanıcı kapattı.** Ortak his yerine her kaprisin kendine özgü vuruşu daha iyi oldu (Diken yıldız + geri tepme,
    Filiz özsu, Kabuk çatlak + ağır sallanma).
- [x] **O3 · Kalan vuruş göstergesi:** küçük can barı veya bölmeli gösterge. Yalnız kabul
  edilen darbelerde azalır.
  - **25 Ekim: kullanıcı şimdilik kapattı; ileride tasarım kararı olarak ele alınacak (not).** Şu an yalnız Kabuk'ta net
    (çatlak aşamaları); Diken ve Filiz'de vuruşa tepki var ama kalan vuruş sayısı okunmuyor.
- [x] **O4 · Diken Kuşatması:** olaya ait kalabalık ek toprak kalkar, saksı toprağı kalır.
  Ağız ve dikenler ortalanır; darbe hissi ve bitiş temizliği kontrol edilir.
  - 24 Ekim (kullanıcı yönü: tümsek = ana bitkinin doğduğu yer; kapan yalnız etkilenen hücrelerde; daha agresif vurulma):
    tümsek yalnız kök hücresinde ortada, hazırlıkta kabarır, kök içinden yükselir, aktifte çöker; kökün altında kapan yok;
    güçlü vuruş (darbe yönünden geri tepme + eğilme, sert basılma, hasarla öfkeli titreme), toon vuruş yıldızı, büyük vuruş/kırılma.
    ThornArtVerification geçti (karede 0 bayt). Video `Docs/FieldEvents/thorn/`.
    - Çıkış (kullanıcı: “çok sade, zararlı bir şey çıkıyor gibi değil”): baskı → BAM yıldızı + büyük toprak → uzayarak fırlama
      (%30 uzun, yerin üstüne sıçrar) → basılarak iniş → oturma (.55 sn); tümsek kök fırlarken dağılır; kapanlar kökten dışarı
      dalga hâlinde aynı eğriyle fırlar; sarsıntı fırlama anında tepe yapar.
    - Patlama yıldızı toon “BAM” oldu (kullanıcı: beyaz yıldız sade): düzensiz kıvrık uçlar, sticker gölgesi + mürekkep,
      kırmızı-turuncu / sarı / krem çekirdek + parlama, mürekkep kıymıkları; iki kare 24 kare/sn kaynar.
    - Kullanıcı: patlama “boş hissettiriyor” → ayrı Blender VFX çalışması `Docs/ThornBurst/` (toprak patlaması silueti, kesekler,
      toz bulutları, hız çizgileri, krater). Kullanıcı beğendi → oyunda (`LateGamePresenter.ThornBurst.cs`, havuz 4, karede 0 bayt,
      patlama anında kök başına ~1 800 üçgen / ~30 çizim). Toz kısaltıldı (,62 sn'de biter). **24 Ekim: kullanıcı onayladı ("gayet kafi"); bu kurallar diğer kaprislere uygulanacak.**
    - Saksısız hücrede kapan halkası havada duruyordu (model saksı kenarını saracak yükseklikte): saksısızda zemine iner
      ve %12 daralır; saksılıda değişmez. Kök / tümsek her iki durumda hücre merkezinde (saksılıda saksının içinden çıkar).
    O1 teşhisi `Docs/FieldEvents/o1/`: hedefler ve halkalar hücre merkezinde (0,00); olay bitince kalan görsel yok.
- [x] **O5 · İstilacı Filiz:** toprak çıkış hücresine ortalanır, çiçek çıkınca toprak kalkar.
  İkisi aynı noktaya hizalanır.
  - 24 Ekim: Diken kuralları uygulandı (CutTargetEvent): tümsek yalnız ana filizde ortada (1,5), ana filiz patlama eğrisi +
    toprak patlamasıyla çıkar, ana filizin altında sürünen dal yok, yayılma parçaları yeni hücrede fırlar (saksısızda zemine
    iner), yayılma toprağı ortada, güçlü vuruş + yıldız + büyük kırılma. SproutArtVerification 41/41. Video `Docs/FieldEvents/sprout/`.
    - 25 Ekim: kullanıcı "hepsi aynı gibi" dedi → Filiz'in kendi kişiliği (Blender çalışması `Docs/SproutBurst/`, pof düzeltmesi):
      kırbaç dallar, dönerek yükselen tomurcuk, pof + polen, sürünen dal, özsu, spor bulutu; hücre işareti pembe halka.
      Oyunda; kullanıcı "iyi olmuş" dedi. Ölüm dalgası eklendi (ana filiz kurur ve devrilir, halkalar yakından uzağa kurur ve
      ufalanır); SproutArtVerification 50/50, dalga sırasında 0 bayt. Kullanıcı "olmuş, bir tık hızlı" dedi → ölüm ~%30
      yavaşlatıldı (test 50/50). **25 Ekim: kullanıcı onayladı ("gayet kafi"); sırada Kabuk (O6).**
    - Yer seçimi "hata"sı (25 Ekim): oyunun akışı rastgele (3 boss round × 6 run seed = 6/6 farklı, `Docs/FieldEvents/o1/EventPlacementProbe.txt`);
      test pencerelerindeki sabit seed 1 hep aynı yeri seçiyordu → iki pencereye "Rastgele yer" (varsayılan açık, seed kutuda görünür).
- [x] **O6 · Aşılı Kabuk:** hizalama, darbe hissi, kalan vuruş göstergesi. A/B ayrımı korunur,
  kalan çemberler ortalanır.
  - 25 Ekim, kurallar (Diken'de onaylanan): hazırlıkta düğüm yok, düğüm hücresinde doğum tümseği; düğüm çıkışta tümsekten
    yükselir. Karar verilene kadar düğümün altında kabuk halkası yok. Düğüme güçlü vuruş (sıkışma + geri tepme). B rengi maviden
    mora (#AA5FEB; mavi = elektrik). ShellArtVerification 113/113. Önce/sonra: `Docs/FieldEvents/shell/Kabuk_Bugun_*`, `Kabuk_Kural_*`.
  - Kişilik önerisi "aşı damarlı zırh tohumu" (ağır, kalın, inatçı) kullanıcı onayıyla Blender çalışmasında: `Docs/ShellBurst/`.
  - 25 Ekim: kullanıcı çalışmayı beğendi → oyunda (`LateGamePresenter.ShellBurst.cs`): doğum plakaları, damar + kelepçe, ağır çıkış
    + "TOK", çatlak aşamaları (kalan vuruş göstergesi), ceviz gibi açılış, kapanan düğüm toprağa çekilir, kabuk yakalar (A ya da B),
    açılış. ShellArtVerification 164/164, karede 0 bayt. **25 Ekim: kullanıcı onayladı ("bu da hoş olmuş, tamam").**
- [x] **O7 · Olayların bitiş kontrolü:** görsellerin, hedeflerin ve kalan efektlerin temizliği.
  Gezgin Bereket'in tüm çalışma ve bitiş durumlarına henüz toplu kabul verilmedi.
  - **25 Ekim: kullanıcı kapattı.** Bitiş temizliği Diken / Filiz / Kabuk testlerinde doğrulandı; Gezgin Bereket'in zaten kendi
    kişiliği var, şu hâliyle iyi.

### Boss atmosferleri

- [ ] **A1 · Don Cephesi:** kar taneleri arka plan yıldızlarından ayrışır. Etkilenen bölgeden
  yükselen, yukarıda solan mavi perde eklenir.
- [ ] **A2 · Sert Kabuk:** aynı mantıkta kahverengi yükselen ve solan perde.
- Her iki perdede referans Gezgin Bereket perdesi; bitkiler ve darbeler örtülmemeli.
- Sis: yeni değişiklik talebi yok.

### HUD / UI

- [ ] **H1 ·** Level/XP barı; level yazısının boyutu ve hizası.
- [ ] **H2 ·** Round yazısı üst ortaya; süreyle çakışmasın.
- [ ] **H3 ·** Altın / demir / taş göstergelerine ikon; sağ üste taşınır.
- [ ] **H4 ·** Kota paneli sol üste.
- [ ] **H5 ·** Kota paneli sabit ve kompakt boyutta.
- [ ] **H6 ·** Taşan panel içeriği mouse üzerindeyken kaydırılabilir.
- [ ] **H7 ·** Hover'da hafif büyüme, çıkışta normale dönüş.
- [ ] **H8 ·** Kota, olay ve diğer HUD panelleri çakışmaz.
- [ ] **H9 ·** En büyük gridde de paneller tarlayı ve koordinatları örtmez.

### Renk kararları (22 Ekim palet prototipi, izole kopyada çekildi)

- Patlama **sarı kalır**: mercan denemesi kırmızıya, "tehlike" görünümüne kaçtı.
- Artçı **beyaz kalır**: sıcak beyaz denemesi ten rengi gibi durdu.
- **Şekil aileyi, renk varyantı söyler.** Kırmızı elektrik (Kapalı Devre) hem elektrik hem
  farklı olarak okunuyor; elektrikten türeyen diğer sistemlerde de bu kural kullanılabilir.
- Hasar yazısı aile renkleri (krem, altın kritik, sarı/mercan patlama, camgöbeği elektrik,
  nane tornado, pembe tırpan, lavanta) ayrışıyor ama **karar bekliyor**. Lavanta beyaz saksı
  kenarında zayıf, koyulaştırılmalı. Ana projeye geçmesi için yazı rengi vuruş türüne göre
  seçilmeli, yalnız aynı türden vuruşlar birleşmeli. B2 (Taşan Hasar rengi) bu kararla çözülebilir.
- Öğretme önerisi: renk oyuncunun seçimiyle gelsin, ödül kartında aynı renk görünsün, round
  sonu özetinde renkli hasar dağılımı, ilk tetiklenmede tek seferlik maskot cümlesi.

## Demo kapsamı (kontrol edildi, 22 Ekim)

- DemoScene: 20 round, boss'lar R3 / R6 / R10 / R13 / R16 / R20, kaynak profil
  `Run50_AlphaDengeV1`. Tornado ve bumerang kilitli.
- Demonun ödül havuzu 19 ödül: Artçı Patlama, Fırtına Bileği, Geniş Savuruş, Canavar Kesimi,
  Bereketli Öğrenim, Bilgi Filizi, Altın Hedef, Zincir Hasat, Davranışa Adanış, Nadir Tohum,
  Ağır Darbe, Hızlı Bilek, Kıvılcım, Keskin Bıçak, Hasat Ritmi, Kritik Göz, Yıkım Gücü,
  Çifte Akım, Bereketli Toprak.
- **Demoda olmayan** geç oyun ödülleri (Hilal, Dolunay, Anten, İletken Hat, Kapalı Devre,
  Taşan Hasar, Hasat Yankısı, Açgözlü Hasat, Nadir Reaktör, Aşırı Yükleme, Kritik Ark)
  demodan sonraya kalır. Bunların görsel düzeltmeleri demo işi değildir.
- Tarla olayları şu an yalnız Geç Oyun profilinde ve R26'dan sonra var; demoda **hiç yok**.
  Kapris olarak demoya alınmaları M4'ün işidir.

---

## Hafta 1 · 22 Ekim – 1 Kasım: temel

### M0 · Checkpoint commit [Şart]
- 12 Ekim'den beri commitlenmemiş 69 dosya (DOTween güncellemesi dahil) gözden geçirilip commitlenir.
- Bitti: çalışma ağacı temiz, sonraki işler ayrı commitlerle izlenebilir.

### M1 · Çeviri altyapısı [Şart]
- Anahtarlı metin tablosu: her satır bir anahtar, her sütun bir dil. Çevirmenlerin Google
  Sheets'te doldurabileceği CSV biçiminde.
- Çalışma anında dil değiştirme, Ayarlar menüsüne dil seçeneği, eksik çeviride Türkçe'ye düşme.
- Font yedekleme düzeni: Latin, Kiril, Çince/Japonca/Korece için yer hazır.
- Sayı biçimleri ve Türkçe büyük harf (i → İ) sorunları.
- Kural: bundan sonra yazılan her yeni metin doğrudan tabloya girer.
- Bitti: menü ve Ayarlar TR/EN arasında çalışırken değişiyor. Mevcut ~850 metnin taşınması M13'te.
- Senden: dil listesi (font kararı için; M13'e kadar yeterli).

### M2 · Tasarım dokümanı: Adak / Sınama / Kapris / Armağan [Şart]
- **Adak:** kotanın hikayedeki adı ve paneldeki sunumu.
- **Sınama:** boss'lar bitkinin sınaması. Ödül, boss'un düşürdüğü item; boss başına kendi item tablosu.
- **Kapris:** maskot gezegenden tarlaya tohum fırlatır, tarla olayı orada başlar.
- **Armağan:** kapris çözülünce açılan süreli rastgele buff. Boss item havuzunu tüketmez.
- "Bu ne?" dedirtmeme kuralları:
  1. Maskot anons eder.
  2. Önizlemede ikonla gösterilir.
  3. İlk karşılaşma sakin bir round'da olur.
  4. Aynı fiille çözülür, sonucu Adak panelinde görünür.
  5. Kapris öğretir, Sınama sınar.
- Demoda hangi kaprislerin, hangi round'larda çıkacağı (öneri: ilk kapris R8 civarı).
- Bitti: doküman senin onayından geçti. M4–M6 bu dokümana göre yapılır.

### M3 · Maskot diyalog sistemi [Şart]
- Mevcut `GreenhouseMascot` korunur. Sabit yazılmış cümleleri veriye taşınır: anahtar, ifade
  karesi (16 kareden biri), tetik.
- "İlk kez olunca" tetikleri ve görüldü bilgisinin kaydı (MetaSave).
- İki sunum: gezegendeki küçük balon (oyun içi) ve büyük hikaye paneli. Büyük panel yalnız round
  arasında açılır, oyun zamanlamasına ve ekonomiye dokunmaz. Atlanabilir.
- Bitti: test cümleleriyle iki sunum da çalışıyor, TR/EN değişiyor.

## Hafta 2 · 2 – 8 Kasım: oyun sistemleri

### M4 · Kapris: tohum fırlatma ve olayların demoya taşınması [Şart]
- Maskot anonsu, gezegenden tohum, olay tohumun düştüğü yerde başlar.
- Sıradaki kapris önizleme ekranında ikonla görünür.
- Adak panelinde olayın etkisi yazılır.
- Kota yazıları "Adak" olur.
- Demo profiline kapris planı eklenir (M2'deki round'lar).
- Bitti: demoda kapris başından sonuna oynanabiliyor; otomatik test geçiyor.

### M5 · Armağan [Şart]
- Kapris çözülünce tohum açılır ve rastgele süreli bir buff verir.
- Aktif armağanlar HUD'da küçük ikonla görünür.
- Armağan havuzu ayrı tutulur, boss item havuzunu tüketmez.
- Bedelli armağan fikri M2'de onaylanırsa eklenir.
- Bitti: armağan alınıyor, süresi bitince kalkıyor, ekonomi testi bozulmuyor.

### M6 · Boss'un düşürdüğü item [Şart]
- Sınama kazanılınca boss item düşürür; seçim bu düşüşten açılır.
- Boss başına item tablosu (Don, Sert Kabuk, Sis); mevcut aşama düzeni korunur.
- Bitti: üç boss'un tablosu çalışıyor, havuz tükenme durumu güvenli.

## Hafta 3 · 9 – 15 Kasım: hikaye ve sunum

### M7 · Hikaye, tutorial ve ipucu metinleri [Şart]
- İlk taslağı ben yazarım (TR + EN), sen düzeltirsin.
- Açılış yazıları, tutorial cümleleri (ilk saksı, ilk adak, ilk sınama, ilk kapris, ilk armağan,
  ilk level-up), twist ipuçları, demo sonu cümlesi.
- Bitti: metinler tabloda ve senin onayından geçti.
- Senden: metin onayı.

### M8 · Açılış sahnesi (5–10 sn) [Şart]
- Unity içinde oynayan 2D comic sahne; yazılar tablodan gelir, görüntüye gömülmez.
- Akış: Dünya çatlar ve patlar → roketler kaçar → sağ üstteki gezegene iniş → maskot:
  "Bana bitki adayın…"
- Şekiller kodla çizilir, maskot için mevcut sprite kullanılır. Atlanabilir, yalnız ilk açılışta
  otomatik oynar.
- Bitti: sahne TR/EN oynuyor, sen görsel olarak onayladın.

### M9 · Demo sonu: twist imaları ve wishlist [Şart]
- R20 zaferinde maskotun gölgesi, gözde parıltı ve tek cümle. Mevcut karelerin üstüne efekt
  olarak yapılır, yeni çizim gerekmez.
- "Tam sürümde" yazısı ve wishlist / takip butonu.
- Bitti: zafer akışı bozulmadan çalışıyor.
- Senden: butonun gideceği link (Steam veya itch.io).

### M10 · Toon VFX hattı ve demonun kahraman efektleri [Şart: hat + tohum, İyi olur: diğerleri]
- Ortak comic VFX shader'ı ve efekt oynatıcısı (hücre boyutuna ölçek, hasar anıyla senkron).
- Blender'da script'le kurulacak kahraman efektler:
  1. Tohum fırlatma ve düşüşü
  2. Armağanın açılması
  3. Boss item düşüşü
- Bitti: üç efekt oyunda; ön izleme görüntüleri senin onayından geçti.

## Hafta 4 · 16 – 22 Kasım: cila ve metin

### M11 · Demodaki açık görsel işler (13 Ekim notlarından) [Şart]
- Vurulan hedeflerde darbe hissi ve kalan vuruş göstergesi (kaprisler bunu kullanacak).
- Hedeflerin ve bölge görsellerinin saksılı/saksısız hizası.
- Diken Kuşatması'nın fazla toprağı; İstilacı Filiz hizası.
- Don ve Sert Kabuk perdeleri; kar tanelerinin yıldızlardan ayrışması.
- Artçı Patlama'nın ikinci darbe rengi (renk kararı senden).
- HUD: level/XP barı, round yazısı üst ortada, kaynak ikonları sağ üstte, Adak paneli sol üstte
  sabit boyut ve kaydırılabilir, panellerin çakışmaması.
- Bitti: her madde için ekran görüntüsü kontrolü ve senin Play onayın.

### M12 · Ses [Şart: SFX, İyi olur: müzik]
- Geçici sentez seslerin gerçek seslere geçmesi (`FeelAudio.Override` hazır).
- Menü müziği; round arası sessizliği için parça.
- Her ses ve müzik için kullanmadan önce lisans bildirimi (AGENTS.md).
- Bitti: demodaki her olayın sesi var, kayıtlar ThirdParty-Licenses.md'de.
- Senden: ses kaynağı seçimleri (ücretsiz ticari kullanım kuralına göre öneri getiririm).

### M13 · Mevcut metinlerin çeviri tablosuna taşınması [Şart: TR/EN]
- 118 dosyadaki yaklaşık 850 sabit metin ve asset metinleri (ödül adları, açıklamalar).
- Demonun gördüğü her ekran önce yapılır, geri kalanı sonra.
- Bitti: demo baştan sona İngilizce oynanabiliyor, eksik anahtar raporu temiz.
- Not: diğer dillerin çevirisi demo sonrasında. **Metin kilidi Aralık ortası**, çevirmenlere gönderim.

## Hafta 5 · 23 – 29 Kasım: paketleme ve çıkış

### M14 · Run kaydı ve geri bildirim [Şart]
- Run sonunda dosyaya otomatik kayıt: ulaşılan round, kaybetme sebebi, seçilen kart, ödül ve
  armağanlar, round süreleri.
- Demo sonunda kısa geri bildirim formu linki. "İlk kaprisi anladın mı?" sorusu dahil.
- Bitti: kayıt dosyası okunabilir, oyuncu yolu tek dosyada.
- Senden: form (4–5 soru; taslağı ben yazarım).

### M15 · DemoScene güncelleme ve tam doğrulama [Şart]
- Demo yeni sistemlerle güncellenir: kapris, armağan, item düşüşü, diyalog, açılış, demo sonu.
- Otomatik demo testi; 20 round akışı; uzun oturum performansı.
- Windows build'i (WebGL isteğe bağlı).
- Bitti: test geçiyor, build'i baştan sona en az bir kez sen oynadın.

### M16 · Yayın [Şart]
- Demo sayfası: itch.io anında yayınlanır. Steam'de mağaza sayfası ve demo incelemesi birkaç
  gün ile birkaç hafta sürebilir, Steam Direct ücreti gerekir; şartları Steamworks'ten kontrol et.
- Ekran görüntüleri ve kısa tanıtım metni (TR/EN).
- Senden: platform kararı, capsule / kapak görselleri, testçi listesi.

---

## Demodan sonra (geri bildirime göre)

- Geç oyun: sınamada iki kuralın üst üste binmesi, kapris sıklığının round'la artması, kaprisin
  boss round'larındaki büyük hali.
- Demoda olmayan geç oyun ödüllerinin görsel düzeltmeleri: Hilal yönü ve hızı, Dolunay,
  Kritik Ark, Taşan Hasar popup rengi, Hasat Yankısı meteoru, Açgözlü Hasat, Kapalı Devre,
  Aşırı Yükleme.
- Diğer dillerin çevirisi ve fontları.
- Steam mağaza sayfası, trailer, **Next Fest kaydı (son gün 10 Ocak)**.
- Final demonun incelemeye gönderilmesi (son gün 8 Şubat).
- Denge: demo run kayıtlarıyla.
