# Diyalog altyapısı — Faz 2 durumu (26 Ekim 2026)

Plan: `Diyalog-Tutorial-Plani-20261025.pdf` (sürüm 2.1). Faz 2 = öğretim satırları T1–T11'in oyuna bağlanması ve çeviri hazırlığı.

## Yapıldı (kodda)

### Tetikler
- **T2 hazırlık ekranı:** run kurulunca tetiklenir.
  - Menüdeki "Başla", durumu menü sahnesindeyken RunSetup'a çeviriyor.
  - Oyun sahnesinde aynı durum yeniden yayınlanmıyor; eski bağlantıyla T2 menüde, yanlış anda çıkardı.
- **T3 yerleştirme:** saksı tarlaya konunca kutu da kapanır.
- **T7:** Adak döneminin son round'u başlarken Adak dolmamışsa küçük kutuda uyarı.
- **T8:** Adak dönemi tuttuğunda, round sonu ekranında.
  - Dükkândan aynı round sonuna dönünce tekrar etmez.
  - Tutmazsa run biter; o durumu T11 karşılar.
- **T9 düzeltildi:** ilk Sınama round'u başlarken küçük kutuda çıkar.
  - Tab ile açılan ekran (`RoundPreviewUI`) Sınama önizlemesi değil, round sonu tarla önizlemesi.
  - Sınama bilgisi hazırlık ekranının sol kartında ve round içinde Adak panelinde hep görünüyor.
- **T10:** ilk kapris hazırlanırken çıkar. Kapris adı verilmez; ne yapılacağını kendi plakası söyler.
- **T11:** Adak kaybı, Sınama kaybı ve zafer için ayrı satırlar.
- **Gezegen tepkisi:** Adak tuttuğunda "Mmm…". T8 bir kez görüldükten sonra başlar, tekrar eder.

### Büyük kutu (26 Ekim kararın)
- Round dışı ekranlarda **büyük**: alt ortada, 1100×250 (1920×1080 referans), büyük portreyle.
- **Açıkken:** arkası hafifçe kararır ve arkadaki panel tıklanmaz.
  - Ekranın herhangi bir yerine tık, Boşluk ya da Enter ilerletir; "Atla" bütün konuşmayı geçer.
- **Geçilince:** karartma kalkar, arkadaki panel kullanılır.
- **Plan değişikliği:** bu, plan sürüm 2'deki "ekrana tıklamak ilerletmez" kararını değiştirir. O karar tık arkadaki karta geçmesin diye alınmıştı; karartma perdesi bu riski kaldırıyor.
- **Küçük kutu** (round içi) değişmedi: oyunu kapatmaz, tıklamayla ilerlemez.
- **Ara deneme geri alındı:** kutuyu 520 px'e daraltıp hazırlık ekranında düğmelerin üstüne kaldırmıştım. Senin isteğinle geri alındı.

### Cila: son hâl (26 Ekim)
Görüntü: `Faz2/Box_Final.png`. Video: `Faz2/Kutu_Hareket.mp4`. Gerçek ekranlar: `Faz2/Faz2_*.png`.
- **Perde:** vinyetli ve koyu (0,9); kenarlar koyu, kutunun çevresi açık.
- **Üst köşeler:** ince, köşeden içeri seyrelen çizgi roman tramı; neredeyse siyah mürekkep. Önce amberdi; senin isteğinle siyaha çevrildi. Sabit, nabız yok.
- **Eri (28 Ekim, senin görselindeki gibi):**
  - Kutunun içindeki portre çerçevesi kalktı. Eri kutunun solunda, yan dönük (kutuya bakan uykulu kare), boyu kutunun 1,4 katı.
  - Hafif basık çiziliyor: yatayda ×1,06, dikeyde ×0,95 (senin "cam biraz uzun" notunla). ×1,10 / ×0,92 "çok şişko" bulundu, geri alındı.
  - Kubbesi kutunun üstünden taşıyor, ayakları kutunun alt kenarı hizasında.
  - **Kutu Eri'nin arkasına uzanıyor:** Eri kutunun önünde, kutunun sol ucu kubbenin arkasında kalıyor.
  - **Mavi kontur (webtoon tarzı arka kenar):** Eri'nin dış hattını izleyen camgöbeği şerit. Ölçüler senin görseline göre: iki görüntü aynı ölçeğe getirilip karşılaştırıldı (`Faz2/Kontur_Olcu.png`, solda görselin).
    - Dış hatla arası 4 px, kalınlığı 10 px (büyük kutu, 1920×1080). Önce 8 px boşluk denendi; "boşluk fazla" dedin.
    - Üst ucu sivriliyor: kalın gövde yukarıda incelip ince bir uca dönüyor (senin görselin gibi). Kutunun üstünden kısa bir solmayla başlıyor, taban halkasına kadar dolu iniyor.
    - **Yapım:** Eri'nin silüeti (`Resources/Dialogue/EriSilhouette.png`, kendi çizimimizin 0. karesinden türetildi) camgöbeğine boyanıp sağa kaydırılıyor. Üstüne biraz daha az kaydırılmış kâğıt renginde bir silüet geliyor; bu, aradaki krem boşluğu oluşturuyor ve camın yarı saydam yerlerinden mavi görünmesini engelliyor. Yalnız kutunun kâğıdında görünüyor.
    - **Önceki denemeler:** düz çizgi ("dik"), ölçülmüş eğri ("dış hattı izlemiyor"), boşluksuz kalın silüet, fazla uzun / fazla kısa. Hepsi bu ölçülere çekildi.
  - **Açık mavi tram:** kutunun sol alt köşesinde, Eri'nin ayağının yanında.
  - Karşılaştırma görüntüsü: `Faz2/Eri_Gorsel_vs_Oyun.png` (üstte senin görselin, altta oyun).
  - **Sabit:** kendi animasyonu yok; konuşma kıpırtısı kalktı, satıra göre kare değişmiyor. Yalnız kutuyla birlikte açılıp kapanıyor.
  - Mavi cam kubbe çizimine dokunulmadı; çizim olduğu gibi kullanılıyor.
  - Katalogdaki satır başına yüz karesi alanı şimdilik kullanılmıyor; ileride ifade çizimleri gelirse kullanılabilir.
  - "Eri" plakası kutunun sol üst kenarında. Eri ve kutu birlikte ekranın ortasında.
  - Round içindeki küçük kutu da aynı düzende, ufak.
- **Kutu:** kesik köşeli kâğıt, sert gölge, kısa mürekkep köşe çizgileri, hafif köşe tramı.
- **Hareket:**
  - Kutu alttan biraz taşarak yükseliyor (0,26 sn), isim plakası gecikmeli açılıyor, konuşurken portre hafif kıpırdıyor.
  - Kapanışta perde tık almayı hemen bırakıyor, kutu 0,18 sn'de çıkıyor.
  - Kapanış gerçek zamanla bitiyor; duraklatma onu yarıda asılı bırakmıyor.
- **Boşluk:**
  - İlk basış kutuyu hemen yerine oturtuyor ve yazıyı tamamlıyor.
  - İkinci basış sonraki satıra geçiyor ya da kapatıyor.
  - Art arda basan oyuncu her satırı iki basışta geçiyor, ekran o anda geri geliyor.
- **Küçük kutu ("fısıltı"):** round içinde soldan kayarak giriyor ve soldan çıkıyor.
- **Performans:** dokular bir kez üretiliyor; giriş bitince kareler arayüze dokunmuyor. Karede 0 bayt bellek ayırma, nesne sayısı sabit (testte ölçüldü).
- **Denenip bırakılanlar** (26 Ekim, senin kararınla koddan kaldırıldı):
  - odak çizgileri, kalem taraması, iri tram;
  - ekran kenarında kesik kâğıt kolajı (mor, arduvaz, krem), defter sayfaları ve eskizler;
  - bantlı not.
  - Son karar: kâğıt yok. Karartma ve köşe tramı yeterli.

### Çeviri (15–30 dil hedefi)
- Metin tablosu `key,note,tr,en,…`:
  - Dil sütunları adıyla bulunur.
  - `note` sütunu çevirmen için İngilizce bağlam notudur.
- **Eksik çeviri:** önce İngilizceye, sonra Türkçeye düşer; oyunda anahtar görünmez.
- **Dil seçimi:** `DialogueText.Language`. Dil değişince açık kutunun etiketleri de yenilenir.
- **Yazı hızı:** `meta.speed` satırıyla dil başına ayarlanır (Çince/Japonca/Korece için ~0,5).
- **Duraklama işaretleri:**
  - Çince/Japonca: 、。！？，；：
  - Arapça: ، ؛ ؟
  - Hintçe: ।
  - İşaret kaynak metinden okunur: fontta karşılığı olmayan harf kutu işaretine dönse de duraklama doğru çalışır.
- **İsim plakası ve "Atla":** tablodan gelir; yazıya göre genişler.
- **Eksik çeviri raporu:** `DialogueText.Missing(csv, dil)`. Katalog kurucusu İngilizce eksiklerini de yazar; şu an hepsi eksik.

## Taslak satırlar (onayını bekliyor)

Metinler `Assets/Resources/Dialogue/dialogue_text.csv` dosyasında; değiştirmek için yalnız `tr` sütunu düzenlenir.

| # | Kimlik | An | Kutu | Satır(lar) |
|---|---|---|---|---|
| T1 | intro.select | seçim ekranı | büyük | Oh! Bir gemi! Uzun zamandır kimse düşmemişti… yani, gelmemişti. / Ben Eri. Bu gezegen benim; toprak benim, eller sizin. / Bir çiftçi ve bir tırpan seç. Gerisini birlikte hallederiz. |
| T2 | tut.setup | hazırlık | büyük | Tarla bomboş… Önce dükkândan bir saksı al, sonra round'u başlat. |
| T3 | tut.place | yerleştirme | büyük | Boş bir yere tıkla, saksı oraya oturur. R ile döndürebilirsin. |
| T4 | tut.hit | ilk round | küçük | Hasat bilmiyor musun? İmleci bitkilerin üstünde tut, tırpan kendi vurur. / Dünya'da otomatik tırpan yok muydu? Var diye hatırlıyordum… (senin espri fikrin, A seçeneği; hikâyenin ilk gizli ipucu: Eri Dünya'yı hatırlıyor) |
| T5 | tut.adak | T4'ten sonra | küçük | Sol üstteki pay benim Adak'ım. Dönem bitmeden dolsun, olur mu? |
| T6 | tut.level | ilk level | büyük | Büyüyorsun! Birini seç; bu hasat boyunca seninle kalır. |
| T7 | tut.lastround | son round, Adak eksik | küçük | Dönemin son round'u. Adak'ım hâlâ eksik… acele et. |
| T8 | tut.period | Adak tuttu | büyük | Mmm… doydum. / Şimdilik. Bir sonraki payım biraz daha büyük olacak. |
| T9 | tut.boss | ilk Sınama round'u | küçük | Bu round bir Sınama! Soldaki hedefi bu round içinde doldur. |
| T10 | tut.kapris | ilk kapris | küçük | Tohumlarımdan biri azdı! Ne yaptığı solda yazıyor. (Bereket bir şey istemiyor, bonus veriyor; "istediği" yerine "yaptığı" dördüne de uyar.) |
| T11 | end.quota | Adak kaybı | büyük | Adak eksik kaldı. Açlık… beklemez. / Yeniden başlarız. Toprak hep burada. |
| T11 | end.boss | Sınama kaybı | büyük | Sınama'yı geçemedin… Ben de bekleyemem. / Yine geleceksin, biliyorum. |
| T11 | end.victory | zafer | büyük | Doydum. Bu sefer gerçekten. / …Uzun sürmez ama. |
| — | react.setup | hazırlık (gezegen) | balon | Hmm… |
| — | react.period | Adak tuttu (gezegen) | balon | Mmm… |

- **Adlandırma:** yalnız iki ad var: Adak (T5) ve Sınama (T9).
- **T1 değişikliği:** üçüncü satırda "bahçıvan ve alet" yerine ekrandaki adlar kullanıldı: "çiftçi ve tırpan".
- **Ekrandaki adlarla uyumsuzluk:** panel "KOTA", Eri "Adak" diyor; panel "Boss hasadı", Eri "Sınama" diyor.
  - Bu bilerek bırakıldı.
  - HUD geçişinde panel adlarının da Adak/Sınama olması önerilir.

## Test sonuçları (izole kopya, 26 Ekim)

Kayıtlar ve ekran görüntüleri: `Faz2/` klasörü.

### DialogueVerification — 59/59
Faz 1'in 45 kontrolü ve Faz 2'nin yenileri:
- hazırlık tetiği: menüde susar, run kurulunca konuşur;
- perde: tam ekran, saydamken de tık yakalar, yumuşak kararır;
- çeviri tablosu: sütunlar başlıktan, İngilizce → Türkçe yedeği, eksik raporu, dil değişince etiketler;
- Japonca: yarı hız, "。"da duraklama.

Karede bellek ayırma: 0 bayt.

### DialogueFlowVerification — 85/85
Gerçek oyun yolu, durum taklidi yok:
- **1. run:** menü → seçim → oyun sahnesi → dükkân → yerleştirme → round 1–6 → kart seçimi → Sınama (R3) → Adak tuttu → ikinci dönemde Adak kaybı.
- **2. run:** Sınama kaybı.
- **3. run:** R27'de ilk kapris (Gezgin Bereket), R50'de zafer.

Ölçülenler:
- **T1–T11** her biri kendi anında ve bir kez çıktı.
  - Erken ölümde hiç görülmeyen kapris öğretimi 3. run'da çıktı.
  - Görülen öğretimler sonraki run'larda tekrar etmedi.
- **Kutu arkayı kapatıyor:** kutu açıkken arkadaki düğmelere giden tık kutuya düşüyor; kutu geçilince düğme tık alıyor.
  - Oyun sahnesinde olay sisteminin gerçek ışın testiyle ölçüldü: hazırlık 4, kart seçimi 4, round sonu 4, run sonu 2 düğme.
- **Tarla:** kutu açıkken tarlaya tık perdeye düşüyor, yanlışlıkla saksı konmuyor. Kutu geçilince tarla tık alıyor.
- **Kayıt ve temizlik:** hata ya da istisna 0. Konuşma gösterirken yeni nesne oluşmuyor (35 → 35). Bekleyen satır bayatlamadı.

**Menüde ışın testi yapılamadı.** Menü kanvası kamerasız "kamera" modunda; penceresiz batch'te konumu ekrana oturmuyor. Menüde yalnız yapı kontrol edildi:
- perde tam ekran ve etkin;
- ışın sıralamasında üstte (60'a 0).

Menüde tıklamayı Play'de senin görmen gerekiyor.

### Testin yakaladığı ve düzelttiğim hatalar
- T2 menü sahnesinde, yanlış anda tetiklenirdi.
- Saydam açılan perde ilk karede tıkı arkaya geçirirdi. Yeni CanvasRenderer saydam ağı atlıyor; artık atlamıyor.
- Kapris satırı Gezgin Bereket'e uymuyordu ("istediği" yerine "yaptığı").
- Daraltılmış kutu kart ekranında karta biniyordu. Bu artık geçersiz: kutu arkayı kapatıyor.

## Doğrulanmadı / yapılmadı

- **Play testi (28 Ekim, sen):** menüden R6'ya kadar oynadın, sorun yaşamadın. T1–T9 ve T11 (Adak kaybı) bu yolda çıkıyor.
  - **Play'de görülmeyenler:** T10 (ilk kapris, ~R26), T11 Sınama kaybı, T11 zafer. Bunlar yalnız otomatik akış testinde görüldü.
  - **Çeviriye bırakılan:** düğmeler İngilizce ("PLANTER SHOP", "START ROUND"), Eri Türkçe adlar kullanıyor ("dükkân"). Çeviri işinde birlikte düzeltilecek (senin kararın).
- **Yüz ifadeleri yok.** `GreenhouseMascotFrames`'teki 16 kare yürüyüş yönleri; mutlu, kızgın gibi ifade kareleri yok.
  - Satırlarda önden görünen kareler (8–12) kullanıldı.
  - İfade portreleri ayrı bir çizim işi.
- **Çeviri işine kalan kararlar (M1):**
  - **Latin dışı fontlar:** Çince/Japonca/Korece/Kiril için yedek font gerekir. Eklemeden önce lisans bildirimi yapılır.
  - **Sağdan sola diller:** Arapça ve İbranice desteği yok.
  - **Arayüz metinleri:** henüz kodda sabit; diyalog tablosundan ayrı bir iş.
- **Sonraki fazlar:**
  - Faz 3: hikâye aşamaları ve kilometre taşları.
  - Faz 4: ayarlar (metin hızı, aç/kapa, rehberi sıfırla) ve ileride "blablabla" konuşma sesi.
  - Faz 5: giriş videosu.
  - Faz 6: Mechanics Test Lab araçları.
