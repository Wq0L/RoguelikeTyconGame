# UI dili — comic kağıt kiti (kural sayfası)

6 Kasım 2026. Temel yön `Docs/UI-Art-Direction.md`'de. Bu sayfa o yönün uygulama kurallarıdır. Parça listesi, ne nerede
kullanılır ve eksikler: `Docs/UI-Envanter.md`.

Kaynaklar:
- **HUD kiti:** kullanıcının ChatGPT ile ürettiği kit, `Assets/Resources/HudKit`.
- **Comic Paper UI Pack v2:** kullanıcının Codex ile ürettiği paket.
  - Kaynak dosya: `output/ComicPaperUI-Pack-v2`.
  - Oyunda kullanılanlar `Assets/Resources/ComicPack` altında, hazır bekleyenler `Assets/Art/UI/ComicPack` altında.

## 1. Görsel kelimeler

- Koyu erik-siyahı mürekkep kontur, kesik kâğıt silüet, altta ve sağda sert 3B kenar.
- İnce iç kâğıt çizgisi, köşelerde kısa üçgen çizikler, sol altta az tram.
- Düz, basılı renk. Parlaklık, gradyan, glow ya da yumuşak plastik bevel yok.
- **Aynı kesik her yerde tekrar etmesin.**
  - Paketin neredeyse her parçasında aynı sağ üst çentik ve sol alt tram var.
  - Aynı ekranda yan yana üç ya da daha fazla aynı yönlü parça olursa birini yatay çevir (`scale.x = -1`, yazı ayrı nesnede kalır) ya da HUD kitinin farklı kesikli bir parçasıyla karıştır.
  - Ortak dil, birebir aynı şekil demek değildir (UI-Art-Direction).
- Düz renkli UI plakası kullanılmaz. Kullanıcı reddetti: "çok UI duruyor".

## 2. Renk görevleri

| Renk | Görev | Bugün nerede |
|---|---|---|
| **Mavi** | İlerle, ana akış, geri dön. Ekranda en fazla **bir** mavi buton olur. | Sonraki Round / Başlat, dükkândan GERİ |
| **Yeşil** | Satın al, saksı, olumlu onay | Saksı Dükkânı |
| **Altın** | Yetenek, yükseltme, ödül | Yetenek Dükkânı |
| **Mor** | İncele, ikincil eylem, özel / nadir içerik | Önizleme, dükkânda Temizle |
| **Kırmızı** | Tehlike, iptal, sat, boss uyarısı | Sat |
| **Lacivert** | Bilgi zemini, başlık şeridi, sayaç plakası, pencere | hazırlık kartı başlığı, süre ve Lv plakaları, dükkân penceresi ve başlıkları |
| **Krem** | İçerik kartı, tooltip, açıklama | hazırlık kartı, yeni tile'lar kartı, kota kartı, dükkân kartları ve detay kâğıdı |
| **Açık mavi kare** | Koordinat kutusu (vurgusu gerçek altın kare) | haritada ve tarlada A–K, 1–11 |
| **Gri** | Pasif buton (`_disabled`), kaynak sayacı | butonların pasif hâli, Gold / Iron / Stone |
| **Sarı ışık** | Bu round gelen tile (haritadaki parlamayla aynı renk) | yeni tile'lar kartının başlığı |

- Önizleme butonu 6 Kasım'da maviden **mora** alındı: ekranda iki mavi buton yarışmasın diye. Kullanıcı isterse geri alınır.
- Renk **çarpılarak** üretilmez; örneğin mavi çizim sarıyla çarpılınca yeşil olur.
  - Yeni bir renk gerekirse normal hâl betikte **renk döndürmeyle** türetilir. Mavi buton yeşilden böyle türetildi.
  - Koordinat plakasının vurgusu ayrı çizimdir.

## 3. Durumlar (hover, basılı, pasif)

- **Butonlarda dört hâl aynı silüetten gelir.**
  - Hover, basılı ve pasif hâller normal görselden türetilir: boya açılır, koyulaşır ya da griye döner; mürekkep ve saydamlık hiç değişmez. Üreten: `comicpack_prepare.ps1`.
  - Paketin kendi hover/pressed görselleri kullanılmaz. Ayrı üretildikleri için şekilleri 32 piksele kadar kayıyor; hover'da buton zıplar.
- Geçişi Unity'nin kendi **SpriteSwap**'ı yapar (`ComicKit.StyleButton`). Seçili hâl normal görseldir, tıklanınca buton parlak kalmaz.
- Hover hareketi `ComicHoverMotion` ile yapılır (1.05 ölçek); eskisi gibi kalır.
- AI'dan ayrı hâl görseli istenmez.

## 4. Yazı

- **Başlık, buton ve sayı: Bungee 400** (6 Kasım 2026'dan beri bağlı).
  - Tema alanları: `theme.displayFont` ve `theme.displayOutline`. Uygulayan: `ComicKit.Display(yazı, konturlu)`.
  - Koyu zeminde beyaz yazı + mürekkep kontur (`Bungee Outline.mat`). Krem zeminde fontun kendi malzemesi, koyu mürekkep rengi (54, 39, 54).
  - **Türkçe büyük harf:** Bungee küçük harfi de büyük çizer. `ComicKit.TurkishCaps` Türkçe ve Azericede "i"yi "İ"ye çevirir; zengin yazı etiketlerine dokunmaz.
    - Font dosyası değiştirilmedi. Yazıların kaynağı (`text`) da değişmez; yalnız ekrana çizilen hâl değişir.
  - **Harf atlası:** 90 pt, ASCII + Türkçe + semboller önceden dolu. `BuildFontAtlasPreparer` build'de doldurur ve temizletmez; çalışma anında toplu harf eklemesi çökme tetikleyicisi olduğu için.
  - **Yedek font:** LilitaOne SDF. CJK gibi Bungee'de olmayan harfler oradan gelir.
  - Bungee'de: HUD, kite geçen butonlar, kart başlıkları, koordinatlar, dükkânın başlık / ad / fiyat / istatistik yazıları.
- **Bungee'de olmayanlar:** uzun açıklama, diyalog ve kota satırları okunaklı gövde fontunda kalır.
- **Uzun açıklama ve diyalog:** okunaklı gövde fontu.
- **Boyutlar** (1080p referans):

  | Öğe | Boy (px) |
  |---|---|
  | ana buton | 34–40 |
  | buton | 26–30 |
  | kart başlığı | 28–30 |
  | HUD sayısı | 28–42 |
  | gövde | 16–22 |
  | not | en az 14 |

- Oyuncunun gördüğü her yazı `GameText` / `LocalizedText` üzerinden gelir (AGENTS.md); düz yazı bırakılmaz.
- Değeri çok değişen sayılarda (kaynaklar) yazı kutuya sığmazsa küçülür. Bu yalnız sayı değişince çalışır, karede çalışmaz.

## 5. Dil değişince düzen (kullanıcı kuralı, 6 Kasım 2026)

Kullanıcının kuralı: "yazılar değişse de her şey düzgün gözüksün".

- **Sabit kutudaki her yazı sığmazsa küçülür, hiçbir zaman kesilmez.**
  - Ayarlar: `enableAutoSizing`; en büyük boy tasarım boyu, en küçük 12–16; `overflowMode = Overflow`.
  - "Kes" kipi Bungee'nin yüksek satırında küçük butonda yazının tamamını siliyordu (Temizle).
- **Uzun başlık gerekirse iki satıra iner.** Örnek: kota kartı başlığı (`TextWrappingModes.Normal`).
- **Dil değişince her yazı hemen yenilenir.**
  - Yollar: `LocalizedText`, `GameText.Changed`. AGENTS.md kuralı.
  - 6 Kasım'da eklenenler: `RoundUI` (süre, round), `XpUI` (Lv), `GoldUI` (sayının binlik ayırıcısı: tr 12.425, en 12,425), açık dükkân, yeni tile'lar kartı.
- **Kutuyu yazıya göre değil, yazıyı kutuya göre ayarla.**
  - Kutu boyu tasarımla sabit kalır, yazı onun içinde küçülür. Böylece dil değişince yerleşim kaymaz.
  - Kutunun kendisi büyüyorsa (hazırlık kartı satırları) boy kodda satır sayısından hesaplanır, yazı uzunluğundan değil.
- **Doğrulama:** `ComicKitVerification` dil turu hazırlık ekranını, dükkânı ve HUD'u üç dilde kurar ve her dil için görüntü alır.
  - Diller: Türkçe, İngilizce ve sahte dil `qps` (yaklaşık %35 uzun, aksanlı).
  - Kitteki her yazı kutusuna sığmalı ve çizilmeli; yoksa FAIL.
  - Diğer yazılar taşarsa WARN satırı yazılır.
  - Yeni bir ekran kite geçince o ekran dil turuna eklenir.

## 6. Ölçü ve yerleşim

- **Kenar boşlukları:** ekran kenarından 24 px (`ComicHud.Margin`), parçalar arası 8 px.
- **9-dilim:**
  - Dikeyde 1:1 ölçek (`pixelsPerUnitMultiplier = sprite yüksekliği / hedef yükseklik`); yatayda orta parça uzar.
  - Kenarlar ve köşe çizikleri bozulmaz.
  - Kenar payları meta dosyalarında tanımlı (betik yazar).
- **Butonlar:**
  - En az 64 px yükseklik.
  - Yazı, sprite'ın üst yaklaşık %80'ine yerleşir; alt şerit 3B kenardır.
- **Üst HUD:** tarla, ekran yüksekliğinin %5'i kadar aşağıda durur (`GridUnlockFeel.HudTopRoom`). Round kartı en üst koordinat rozetini örtmez.
- **Harita:**
  - Açık hücre beyaz kare olur ve tile'ın rengiyle boyanır. Kapalı hücre kömür karesidir, boyanmaz.
- **Koordinat kutuları** (6 Kasım'da kullanıcı istedi):
  - Haritada ve tarlada kitin açık mavi karesi (`slot_blue`), yazı Bungee.
  - Tarladaki vurgu, gerçek altın kareye (`slot_gold`) yumuşak geçiştir. Renk çarpılmaz.
- **Dükkân çıkış butonları** (GERİ, Satıştan çık) kaynak sayaçlarıyla aynı köşededir; sayaçların altına iner (y −88).
- **Yetenek ağacı düğümleri: durum = kare**, renk çarpılmaz.

  | Durum | Kare |
  |---|---|
  | alınabilir | altın |
  | alınamaz / kilitli | kömür |
  | kısmen alınmış | beyaz |
  | tamamlandı | yeşil |
  | başlangıç erişimi | açık mavi |
  | profil kapalı | lacivert |

  - Kademe noktaları küçük altın (dolu) ve kömür (boş) karelerdir.
  - Bağlantılar krem mürekkepli çizgidir (`line_cream`).
- **Tooltip'ler** (hepsi ortak `ComicPopupView`): krem panel, amber'e boyalı etiket şeridi, başlık Bungee. Şerit ve içerik, şeridin alt kenarı alt yazıya binmesin diye biraz aşağıdan başlar.

## 7. Teknik kurallar

- **Sprite'lar elle düzenlenmez.** Her değişiklik `Tools/Art/ComicPack/comicpack_prepare.ps1` (paket) ya da `Tools/Art/HudKit/hudkit_slice.ps1` (HUD kiti) betiğine yazılır ve betik yeniden çalıştırılır. Var olan GUID'ler korunur.
  - Betik ayrı bir PowerShell sürecinde var olan dosyanın üzerine yazamıyor. Aynı süreçte çalıştır: `& .\Tools\Art\ComicPack\comicpack_prepare.ps1`.
- **Build'e giren:** `Assets/Resources/ComicPack`. **Hazır bekleyen** (build'e girmez): `Assets/Art/UI/ComicPack`. Bir parça kullanılmaya başlanınca betikte `d='R'` yapılır.
- **İçe aktarma:** BC7 (yüksek kalite sıkıştırma), mipmap yok, en çok 1024. Boyutlar 4'ün katı, böylece sıkıştırılabilir.
- **Kod:**
  - `ComicKit.Get(ad)`: önbellekli yükleme.
  - `ComicKit.StyleButton(buton, renk)`, `ComicKit.Card(...)`, `ComicKit.StyleRoundEnd(...)`, `ComicKit.Display(...)`.
  - Dükkân için `PlanterShopPanelUI.UseComicKit()`.
  - HUD için `ComicHud.Install()`; ikisini de `UIManager.Start` çağırır.
  - Karede iş yok. Hâller SpriteSwap ile değişir, malzeme kullanılmaz. Her ekran bir kez kurulur. Eski `ComicButtonVisual` (her karede malzeme yazan) kaldırılır.
- **Doğrulama:** `ComicKitVerification` (HUD, butonlar, kart, harita, yeniden açılışta çoğalma yok). Yeni bir ekran kite geçince bu teste kontrolü eklenir.
- **Lisans:** kaynak AI çıktısı (OpenAI). Steam'de "önceden üretilmiş AI içeriği" beyanı gerekir. Kayıt `Docs/ThirdParty-Licenses.md`'de.

## 8. Yapılmayacaklar

- Renk çarpımıyla vurgu üretmek.
- Glow, gradyan, parlak bevel ya da düz UI plakası kullanmak.
- Aynı ekranda iki ana (mavi) buton koymak.
- Yazıyı kesen taşma kipi ("Truncate") kullanmak ya da yazıyı karede kurmak.
- Şekli kayan, ayrı üretilmiş hover ya da basılı görselleri kullanmak.
- Ekranları toptan yeniden boyamak. Her ekran tek tek ele alınır ve kullanıcıya gösterilir.
