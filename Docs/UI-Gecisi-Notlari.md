# UI geçişi notları

**Başlangıç:** 5 Kasım 2026. UI bölümünde bakılacak maddeler burada toplanır. Görsel kurallar `Docs/UI-Art-Direction.md`'de.

## "Oyun boş ve bitmemiş görünüyor" hissi (5 Kasım, kullanıcının ekran görüntüleri)
Kullanıcı: "Başkalarının oyununa bakınca her şey tamamlanmış görünüyor; benimkine bakınca boş ve bitmemiş gibi hissediyorum,
sebebini çözemedim." İncelenen ekranlar:
- hazırlık (düz harita açık);
- round 1;
- saksı dükkânı;
- yetenek ağacı;
- round 2 özeti.

Bulgular, etkisi en büyükten:

1. **Hata gibi duran kalıntılar.** En hızlı ve en etkili düzeltme.
   - Düz harita (hazırlık / round sonu) açıkken arkadaki eğik tarlanın koordinat rozetleri haritanın kenarından taşıyor: solda "E, F", altta "2–6, 8, 9". İki koordinat sistemi üst üste görünüyor.
   - Haritanın sol üstünde (A1–B2) beyaz kırıntılar var; D ile E başlıklarının arasında dikey beyaz bir çizgi var.
   - Round sonu ekranında D7'de turuncu bir tırpan parçası duruyor.
   - Bu tür izler oyuncuya fark ettirmeden "hata" diye okunuyor, "bitmemiş" hissinin en güçlü tetikleyicisi.
2. **Tarla bir malzeme değil, bir tablo gibi.**
   - Round ekranında tarla uzayda duran düz, koyu gri bir levha: toprağı, kenarı, kalınlığı, dokusu yok.
   - İlk round'da 121 hücreden 9'u dolu; ekranın büyük kısmı aynı koyu gri.
   - Hazırlıktaki düz harita 121 aynı koyu kare; güzel eğik tarlanın üstünü bir hesap tablosu gibi örtüyor.
   - Fikir: zemin dokusu (toprak, sera zemini), tarla kenarı ya da çerçeve, boş hücrelere hafif doku, belki küçük süsler. Sanat yönü kararı gerekir; birkaç örnekle konuşulacak.
3. **Arayüz iki farklı stilde.**
   - Kota kartı, Eri'nin kutusu ve düğmeler çizgi roman tarzında.
   - "Round 1 / 50", "Time: 43s", sağ alttaki "Lv. 1" ve boş çubuk, Gold / Iron / Stone kutuları ise düz yazı ve düz koyu dikdörtgen. Bir kısmı bitmiş, bir kısmı prototip gibi görünüyor.
   - Fikir: hepsi kota kartıyla aynı plaka diline girsin.
4. **Renk az, her şey koyu.**
   - Koyu mor uzay, koyu gri tarla, koyu gri harita. Renkli olan yalnız düğmeler ve gezegen.
   - Ekranın ortasında gözün dinleneceği sıcak bir odak yok.
5. **Yetenek ağacı boş.**
   - Koca ekranda 5 küçük ikon ve ince beyaz çizgiler; düğümler ekrana göre çok küçük.
   - Açılmamış düğümler görünmediği için ağacın şekli yok.
   - Fikir: daha büyük düğümler, açılmamış düğümlerin silik gölgeleri, arkada hafif bir takımyıldızı çerçevesi.
6. **Hareket.** Ekran görüntüleri göstermiyor; ekran dururken bir şey kıpırdıyor mu (ortam canlılığı) ayrıca bakılacak.

Önerilen sıra: 1 (kalıntılar), 3 (HUD tek stil), 2 (tarlaya kimlik, örneklerle), 5 (yetenek ağacı), 4 ve 6 bunlarla birlikte.

### Durum: 1 · kalıntılar (6 Kasım 2026, yapıldı ve test edildi; kullanıcının Play'de bakması bekleniyor)
Kök neden: düz haritanın (`RoundMapUI`, ekran kaplamalı tuval) arkası açıktı.
- Hücre ve başlık aralarındaki 6 px boşluklardan arkadaki eğik tarla görünüyordu. Beyaz kırıntılar, D/E arasındaki dikey çizgi
  ve D7'deki "turuncu tırpan parçası" boşluklardan görünen saksı / bitki parçalarıydı. Kullanıcının ekran görüntüsü yakından
  incelenerek doğrulandı.
- Dünyadaki koordinat rozetleri (`GridCoordinateLabels`) harita açıkken de duruyor, haritanın kenarından taşıyordu.

Düzeltmeler:
- Haritanın arkasına opak zemin konuldu: mürekkep kenarlı koyu plaka, hücreleri ve başlıkları kapsar. Bir kez kurulur, grid
  büyüyünce yalnız ölçüsü yenilenir.
- Harita açıkken dünya rozetleri gizlenir (`RoundMapUI.Visible`). Önizlemede ve round'da geri gelir.
- Görüntüler: `Docs/UI-Kalintilar/01_once.png`, `02_sonra.png`. Önce görüntüsünde rozetler haritanın üstünde görünüyor: bu,
  çekim yönteminden (tuval kameraya taşınıyor). Oyunda rozetler haritanın kenarından taşıyordu.
- Testler: RunPrototypeVerification 73/73 (zemin 121 hücrenin hepsini kapsıyor, ilk sırada), ComicUIVerification geçti.
- Not: zeminin üst kenarı Gold / Iron / Stone kutularının altına birkaç piksel giriyor; HUD yerleşimi (H3: kaynaklar sağ üste)
  bunu zaten değiştirecek.
- Batch tuzağı: `FeelBootstrap` batch'te çalışmaz (kamera, uzay, rozetler, gezegen Eri'si yok). Oyuna benzer çekim için elle
  çağrılmalı.

### Durum: karartma + grid çerçevesi + açılma efekti (6 Kasım 2026, kullanıcı isteği; yapıldı ve test edildi, Play'de bakılacak)
Kullanıcı: "ekranın arkasına background ekleyerek UI ile oyun ekranını ayıralım; kamera 5×5 göstererek başlasın, büyüdükçe yavaş
yavaş uzaklaşsın, o siyahlık feelde çözülsün; grid açma efekti".
- **Karartma:** hazırlık / round sonu panelinin (RoundEndUI) ilk çocuğu tam ekran koyu katman (%60). Tarla ve gökyüzü geri planda
  kararır. Panelle birlikte açılır / kapanır, tık almaz.
- **Kamera çerçevesi** (`CameraFeel.SetFraming`, `GridUnlockFeel`):
  - Görünen alan = açık alan + bir kilitli halka: 3×3 → 5×5, 5×5 → 7×7, 7×7 → 9×9, 9×9 ve 11×11 → bütün grid.
  - Dışındaki kilitli hücreler çizilmez; tarla uzayda küçük bir ada, gökyüzü ve gezegen görünür. İlk denemede yalnız kamera
    yaklaşıyordu, ekran koyu kilitli hücrelerle doluyordu: bu yüzden eklendi.
  - Sarsıntı ve nefes ofsetleri çerçeveyle ölçeklenir.
- **Koordinat rozetleri:** görünen alanın kenarında, yalnız görünen sütun / satırlarda, ekrandaki boyları sabit.
- **Açılma efekti:**
  - Yükseltme yetenek ağacında alınır ve tarla görünmez. Bu yüzden geri çekilme ve efekt tarlanın ilk göründüğü ana ertelenir:
    round, yerleştirme, satış ya da önizleme. O ana kadar yeni açılan hücreler gizli kalır.
  - Efekt: kamera 1,2 sn'de geri çekilir; yeni açılan halka, sonra yeni kilitli halka saat yönünde sırayla, yaylanarak yerden yükselir.
  - Yalnız zemin görseli (Floor) oynar; hücre kökü ve oynanış durumu değişmez. Bitince her şey tam ilk değerine döner.
- **Bedel:** karede bellek ayırma yok. Rozetler yalnız çerçeve değişince yerleşir; hücre listeleri olay anında kurulur.
  `GridUnlockFeel` FeelBootstrap ile kurulur, batch testlerinde yoktur.
- **Testler:**
  - Yeni test GridFramingVerification 17/17: ölçekler 5/11 → 7/11 → 1; gizli hücre sayıları; efektin bekleyip round başında
    oynaması; zemin görsellerinin tam dönüşü; rozet sayıları 10 / 14 / 22; karartma.
  - Diğerleri: RunPrototype 73, TakvimV1 162, Story 57, ComicUI, lab diyalog 31.
- Görüntüler: `Docs/Grid-Cerceve/`.
- Açık: efektin sesi yok.


## Daha önce UI geçişine bırakılanlar
- **Türkçe tile adları iç ad gibi:** "Crystal-RareSpawnChance", "Odak-Hasar", "Fertile-PlantSpawnRate"… Çeviri listesinde (`Assets/Localization/GameText.csv`, `data.tile.*`) Türkçe hücreden düzeltilebilir. İngilizce temiz adlarla duruyor.
- **Türkçe oyunda kalan İngilizce parçalar:** "Time: {0}s", "Gold / Iron / Stone" adları, nadirlik adları (Common, Rare…). Bilerek eski hâliyle bırakıldı; karar UI geçişinde.
- **HUD panel adları:** Adak / Sınama olarak (Faz 2 notu, `Docs/DiyalogPlani/Faz2-Durum.md`).
- **Ayarlar:** metin hızı, diyalog aç / kapa, "rehberi sıfırla" (Faz 4, `Docs/DiyalogPlani/Faz1-Durum.md`).
- **Üzgün aşamada solma (Faz 3 C, 5 Kasım):** Eri'nin üzgün aşamasında yalnız konuşma kutusunun arkası %80 griye solar (`UiMood`, `UI/ComicMood`). Kullanıcı kapsamı bilerek böyle seçti: önce tüm arayüz düşünüldü, sonra "sadece dialog arkası solsun" dedi. UI geçişinde başka öğeleri soldurma kararı kullanıcının.

### Durum: HUD tek stil örneği (6 Kasım 2026; kullanıcı onayı bekliyor)
Örnek `Assets/Scripts/UI/ComicHud.cs` henüz oyuna bağlı değil; yalnız çekim betiği kurdu. Görüntüler `Docs/HUD-Ornek/`.
- Plaka dili: kota kartıyla aynı (ofsetli mürekkep gölge, mürekkep kenar, krem kâğıt), her parçada farklı küçük eğim.
- Yerleşim:
  - Round + süre üst ortada; süre sağa kayık asılı etiket, tarlanın en üst koordinat rozetiyle çakışmaz (H2).
  - Gold / Iron / Stone sağ üstte, ikonlu, projedeki ComicUITheme ikonlarıyla (H3).
  - Kota kartı sol üstte (H4).
  - Level + XP sağ altta tek plaka (H1).
- Oyunun betikleri aynı kalır; yalnız yer, ebeveyn ve görünüş değişir.
  - Eklenen girişler: `GoldUI.UseIconLayout` (ikonlu kutuda yalnız miktar), `RoundUI.SetTimerRestColor`.
- Çeviri: `ui.hud.time` Türkçesi "Time: {0}s" yerine "Süre: {0} sn" (İngilizce aynı).
- Düzeltme: hazırlık ekranı karartması ana tuvalin en altına alındı. Önce panelin içindeydi ve kaynak sayaçlarını da karartıyordu.
  Yalnız dünya ve gökyüzü kararır; panelin geçiş saydamlığını izler. GridFramingVerification bunu denetliyor, 17/17.
- Sırada (onaydan sonra):
  - oyuna bağlama ve testler;
  - H5 (kota kartı sabit ve kompakt), H6 (taşan içerik fareyle kaydırılır), H7 (hover büyümesi);
  - H8 / H9 (çakışma yok, büyük gridde tarlayı örtme yok);
  - H10 (2K'da Eri ölçümü).

### Düzeltme (6 Kasım 2026, kullanıcı geri bildirimi)
- Haritanın arkasındaki kare zemin kaldırıldı. Kullanıcı: "arkaya kare eklemişsin, kötü; background tüm ekrana olmalıydı".
  Tam ekran karartma %60 → %72; hücre aralarından görünen tarla zaten iyice sönük.
- HUD örneği (düz krem plakalar) kullanıcıya göre "çok UI duruyor", "toonic yapıyla karakterize etmek lazım".
  Üç toon yön çizildi:
  - A · tohum etiketi ve filiz;
  - B · Eri'nin sera makinesi;
  - C · çizgi roman çıkartmaları.
  Seçim bekleniyor.

### HUD toon denemeleri (6 Kasım 2026, Higgsfield, kullanıcı onaylı, 3 kredi; bakiye 60,25 → 57,25)
- GPT Image 2.6, orta kalite 2K, 16:9. Referans olarak oyunun kendi görüntüleri verildi: toon butonlar, Eri'nin kutusu, tarla.
  Çıktılar `Docs/HUD-Toon/Deneme1/`: A tohum etiketi ve filiz · B Eri'nin sera makinesi · C çizgi roman çıkartmaları.
- Üçü de oyunun toon dilini tutturdu: kalın mürekkep, parlama izi, tram.
- A ve C'de öğelerin çevresinde renkli parıltı var; son parçalarda istenmez.
- Son parçalar için: saydam arka plan, yazısız (sayılar oyunda TMP ile yazılır), ayrı ayrı. XP çubuğunun boş ve dolu hâli ayrı.
- Denemeler oyunda dağıtılmıyor; seçilen yönün son parçaları oyuna girince `Docs/ThirdParty-Licenses.md`'ye kaydedilecek.

### Sırada: yetenek ağacı kite (6 Kasım 2026, kullanıcı isteği)

HUD, hazırlık ekranı, harita, koordinatlar ve saksı dükkânı comic kağıt kitine ve Bungee'ye geçti (`Docs/UI-Dili.md`). Sıradaki iş yetenek ağacı:
- düğüm kareleri duruma göre;
- kademe noktaları, bağlantı çizgileri, tooltip'ler;
- dil turu testi.

Eşleme ve iş listesi `Docs/UI-Envanter.md` §6'da.

**Güncelleme, aynı gün:** kullanıcı "yaptın mı" diye sorunca uygulandı ve test edildi. Görüntüler: `Docs/UI-Kit-Oyunda/c9_*`. Açık kalanlar envanter §6'da.

### Durum: ana menü, seçim ekranı, boss item kutusu, ağaç zemini ve hissi (10–11 Kasım 2026)

Kullanıcı isteğiyle yapıldı ve izole kopyada test edildi; Play'de bakılması bekleniyor. Ayrıntı `Docs/UI-Envanter.md` §7, görüntüler `Docs/UI-Kit-Oyunda/` (`d1`–`d4`, `c9_*`).

- **"Yetenek ağacı boş ve küçük" maddesi:** zemin, düğüm boyu, yerleşim ve his ele alındı. Kök düğümlerin başlangıca uzaklığı verideki konumlardan geldiği için açık.
- **Sırada:** ayarlar penceresi, kart seçimi, önizleme, run sonu; boss ödülü item görselleri (kullanıcı üretecek).

## Ertelendi: editörde düzenleme kuralına taşıma (11 Kasım 2026)

`AGENTS.md` "Zorunlu editörde düzenleme ve önizleme kuralı"na göre görünüm, Play'e basmadan editörde görülüp düzenlenebilmeli. 6–11 Kasım comic kit işlerinin **hiçbiri bu kurala uymuyor**: hepsi oyun başlarken kodla kuruluyor, sahne ve prefab'larda eski görünüm duruyor.

Kullanıcı kararı: "sonra taşırız, şu an bitirip yetiştirmemiz gerekenler var". Taşıma, ilgili ekran üzerinde çalışılırken ekran ekran yapılacak. O zamana kadar bu ekranlar **editör tarafı eksik** sayılır.

| Ekran | Şu an kim kuruyor | Hedef |
|---|---|---|
| HUD (round, süre, kaynaklar, Lv/XP, kota kartı), alt butonlar, GERİ butonları, round sonu | `UIManager.Start` → `ComicHud.Install`, `ComicKit.StyleRoundEnd`, `StyleButton` | sahneye kaydedilmiş hâl (tekrar çalışınca çoğaltmayan, Undo'lu "Uygula / Güncelle" aracıyla), koddaki kurulum kalkar |
| Ana menü butonları | `MainMenuUI.ApplyComicKit` (Start): konum / boyut 11 Kasım'dan beri sahnede (`sceneLayout`), sprite ve yazı stili hâlâ çalışma anında | menü sahnesine kaydedilmiş hâl |
| Saksı dükkânı | `PlanterShopPanelUI.UseComicKit` | sahne / prefab |
| Round özeti, yeni tile paneli | `RoundSummaryUI`, `RoundNewTilesUI` (Awake) | sahne / prefab |
| Çiftçi / tırpan kartları | `StartSelectionPanelUI` (Awake, kartları kod çiziyor) | kart prefab'ı + editörde örnek veriyle önizleme |
| Boss ödülü kare item kutusu | `BossRewardPanelUI` (Awake) | kutu prefab'ı + önizleme |
| Harita grid'i, koordinat etiketleri | `RoundMapUI`, `GridCoordinateLabels` | hücre / etiket prefab'ı + önizleme |
| Yetenek ağacı | `SkillNodeUI` (kare, noktalar, etiket) + `SkillTreeUI.ApplyLayout` (84 / 112, koridor sıkıştırma) yalnız oyunda | düğüm prefab'ı + "Ağacı önizle / güncelle" aracı (oyunla aynı yerleşim hesabı, oyun ilerlemesine dokunmaz) |
| Bilgi kutuları / popup'lar | `ComicPopupView` (tamamen kodla) | prefab |

Kodda kalabilecekler: duruma göre sprite değişimi, animasyon ve his, verinin yazıya bağlanması. Önerilen ilk ekran: yetenek ağacı.

**Ana menü arka planı (11 Kasım):** canlı sahne kurala uygun yapıldı: prefab + MenuScene, editörde görünür ve düzenlenir; kodda yalnız hareket. Ayrıntı `Docs/UI-Envanter.md` §7 "Ana menü — canlı sahne".
