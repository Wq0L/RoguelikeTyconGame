# UI envanteri — elimizdekiler, kullanım yerleri, eksikler

6 Kasım 2026. Kurallar: `Docs/UI-Dili.md`.

**Durum etiketleri:**
- **OYUNDA:** oyuna bağlı.
- **HAZIR:** projede, oyuna henüz bağlı değil.
- **Türetildi:** AI'dan değil, betikle üretildi.
- **Düzeltildi:** bozuk olan kısmı onarıldı.

Ölçüler, betiğin ürettiği sprite boyutlarıdır (px).

## 1. Comic Paper UI Pack v2 → `Assets/Resources/ComicPack` (R) ve `Assets/Art/UI/ComicPack` (A)

Üreten betik: `Tools/Art/ComicPack/comicpack_prepare.ps1`. Kaynak: `output/ComicPaperUI-Pack-v2`.

### Butonlar (768 × 184–200; dört hâl: `_normal`, `_hover`, `_pressed`, `_disabled`)

| Parça | Yer | Durum | Bugün | Kullanılabilir |
|---|---|---|---|---|
| `button_blue_*` | R | OYUNDA · **Türetildi** (yeşilden renk döndürme; pakette mavi yok) | Sonraki Round / Başlat, GERİ, Satıştan çık | Devam, Tamam, run başlat, menüde Oyna |
| `button_green_*` | R | OYUNDA · hâller türetildi | Saksı Dükkânı, dükkânda Satın al | Yerleştir, olumlu onay |
| `button_gold_*` | R | OYUNDA · hâller türetildi | Yetenek Dükkânı | Yetenek aç, yükselt, ödül al |
| `button_purple_*` | R | OYUNDA · hâller türetildi | Önizleme, dükkânda Temizle | Boss ödülü, özel teklifler, istatistik |
| `button_red_*` | R | OYUNDA · hâller türetildi | Sat | Vazgeç, Ana menüye dön, Run'ı bitir, boss uyarısı |

### Kareler (256 × 256)

| Parça | Yer | Durum | Bugün | Kullanılabilir |
|---|---|---|---|---|
| `slot_white` | R | OYUNDA | haritada açık hücre (tile rengiyle boyanır), yeni tile'lar kartındaki renk karesi | envanter yuvası, kart seçimi küçük ikon zemini |
| `slot_charcoal` | R | OYUNDA | haritada kapalı hücre | kilitli yuva, boş slot |
| `slot_blue` | R | OYUNDA | koordinat kutuları (harita + tarla) | skill düğümü (normal), ikon butonu (kapat / ayarlar) |
| `slot_gold` | R | OYUNDA | tarlada koordinat vurgusu | skill düğümü (alınmış / vurgulu), seçili hücre |
| `slot_navy` | R | OYUNDA (kullanıcının korunmasını istediği eski lacivert kare) | skill düğümü: profil kapalı | koyu yuva |
| `slot_green` | R | OYUNDA · **Türetildi** (`slot_blue`'dan renk döndürme) | skill düğümü: tamamlandı | onaylı / bitti durumu |
| `pip_gold`, `pip_empty` | R | OYUNDA · küçük kareler (40 px) | skill kademe noktaları, ağacın başlangıç noktası | sayfa / adım göstergesi |

Paketin kare hover/pressed görselleri tutarlı ama alınmadı. Hareketli bir kare gerekince butonlar gibi betikle türetilir.

### Paneller

| Parça | Ölçü | Yer | Durum | Bugün | Kullanılabilir |
|---|---|---|---|---|---|
| `panel_cream` | 768×408 | R | OYUNDA | hazırlık kartı, yeni tile'lar kartı | her türlü içerik kartı, boss / olay bilgisi, run sonu özeti |
| `card_portrait_cream` (+ `_hover`, `_pressed`, `_selected` altın; **türetildi**) | 512×764 | R | OYUNDA | dükkânda saksı kartları | kart seçimi, başlangıç donanımı (çiftçi / tırpan), görev kartı |
| `info_cream` | 768×324 | R | OYUNDA | dükkânda Ekim yeri / Boyut / Büyüme süresi | istatistik kutusu, kısa bilgi |
| `tooltip_cream` | 640×444 | A | HAZIR (alt ortada ok) | — | tile tooltip'i, genel ipucu |
| `window_navy` | 768×496 | R | OYUNDA | dükkân penceresi | açılır pencereler: ayarlar, duraklat, onay, run sonu |
| `sidebar_navy` | 384×796 | A | HAZIR · **Düzeltildi** (üstteki silinmiş yazı lekesi boyandı) | — | skill ağacı yan paneli, görev listesi, dükkân kenar paneli |
| `skill_tooltip_blue / gold / green / purple` | 640×392–396 | A | HAZIR (sağda ok; renk farkı yalnız ince iç çizgi) | — | skill açıklaması (aile rengine göre); belirgin olsun diye üstüne renkli başlık şeridi önerilir |

### Şeritler ve sekmeler

| Parça | Ölçü | Yer | Durum | Bugün | Kullanılabilir |
|---|---|---|---|---|---|
| `header_navy` | 768×144 | R | OYUNDA | hazırlık kartı başlığı, dükkânın iki başlığı | pencere ve panel başlığı, sayfa başlığı |
| `label_cream` | 768×176 | R | OYUNDA | yeni tile'lar kartı başlığı (sarıya boyalı) | renkle boyanan durum başlıkları, etiket |
| `resource_navy` | 640×160 | A | HAZIR | — | koyu sayaç (skor, kota), dükkânda bakiye |
| `tab_blue`, `_hover`, `_pressed` | 512×192 | A | HAZIR (paketin üç hâli tutarlı) | — | ayarlar sekmeleri, skill ağacı sekmeleri, lab dışı sekmeler |

### Parçalar

| Parça | Ölçü | Yer | Durum | Bugün | Kullanılabilir |
|---|---|---|---|---|---|
| `bar_track` | 768×116 | A | HAZIR | — | boss canı, kota ilerlemesi, yükleme |
| `bar_fill` | 768×116 | A | HAZIR · **Türetildi** (beyaz, boyanabilir; bar_track'in içi, aynı dikdörtgen) | — | `bar_track` ile Filled görsel olarak |
| `divider` | 768×24 | A | HAZIR | — | kartlarda bölüm ayırıcı |
| `line_cream` | 512×16 | R | OYUNDA (divider'ın küçüğü) | yetenek ağacı bağlantıları ve okları | çizgi / bağlantı |

**Kullanılmayanlar (pakette var, alınmadı):**
- Kare, buton ve sekme hover/pressed görselleri. Butonların hâlleri şekil kaydığı için yeniden türetildi.
- Referans sayfası `comic-ui-concept.png`.
- Skill konsept sayfası: kullanıcı reddetti, `output/_Cikarilan` altında.

## 2. HUD kiti → `Assets/Resources/HudKit`

Üreten betik: `Tools/Art/HudKit/hudkit_slice.ps1`. Kaynak: `Docs/HUD-Toon/Kaynak/ui_kit_gpt.png`.

| Parça | Durum | Bugün | Kullanılabilir |
|---|---|---|---|
| `hud_panel_blue` | OYUNDA | round kartı | büyük bilgi plakası |
| `hud_plate_navy` | OYUNDA | süre plakası, "Lv." plakası | küçük sayaç ya da etiket |
| `hud_plate_grey` | OYUNDA | Gold / Iron / Stone | pasif plaka |
| `hud_card_tab_cream` | OYUNDA | kota kartı | sekmeli bilgi kartı |
| `hud_bar_blue_frame` + `_fill` | OYUNDA | XP çubuğu | — |
| `hud_panel_cream`, `hud_panel_yellow` | HAZIR (build'e giriyor) | — | büyük plaka (altın: ödül) |
| `hud_plate_red` | HAZIR | — | uyarı plakası (kota son round) |
| `hud_bar_yellow` / `hud_bar_red` (+ frame / fill) | HAZIR | — | kota çubuğu (sarı), boss canı (kırmızı) |
| `hud_banner_navy` | HAZIR | — | başlık şeridi (header_navy'nin alternatifi) |
| `hud_slider_blue` | HAZIR (tutamak gövdeye yapışık; ayrılması gerek) | — | ayarlar kaydırıcısı |

HudKit `Resources` altında olduğu için kullanılmayan parçaları da build'e giriyor; toplam ~4.7 MB bellek (sıkıştırmasız). Gerekirse `Art`'a taşınır.

## 3. Font

| Font | Durum | Not |
|---|---|---|
| Bungee 400 | OYUNDA (`Bungee-Regular SDF.asset`, `Bungee Outline.mat`; tema: `displayFont` / `displayOutline`) | Türkçe i → İ (`ComicKit.TurkishCaps`); kullanım yerleri ve kurallar UI-Dili §4–5 |
| Lilita One / Harvest Comic TR | OYUNDA | bugünkü başlık fontu |

## 4. Ekranların durumu

| Ekran | Durum |
|---|---|
| HUD (round, süre, kaynaklar, kota kartı, level / XP) | **kite geçti** |
| Hazırlık ekranı: HAZIRLIK / round özeti kartı, 4 buton, harita hücreleri, yeni tile'lar kartı | **kite geçti** |
| Saksı dükkânı (pencere, başlıklar, kartlar, detay, istatistikler, Satın al / Temizle, Sat, GERİ) | **kite geçti** |
| Koordinat kutuları (harita + tarla) | **kite geçti** |
| Yetenek ağacı (düğümler, noktalar, bağlantılar, etiket, GERİ, pano zemini, sıkı yerleşim, his) | **kite geçti** (ayrıntı §7) |
| Ana menü (Oyna / Ayarlar / Çıkış) | **kite geçti** (ayarlar penceresi eski) |
| Çiftçi / tırpan seçim ekranı | **kite geçti** |
| Boss ödülü seçimi (kare item kutusu + görsel yuvası) | **kite geçti** (item görselleri yok; yer tutucu ikon) |
| Tooltip'ler (yetenek, tile, yerleştirme — ortak ComicPopupView) | **kite geçti** |
| Kart seçimi, boss ödülü, uzmanlaşma paneli | eski görünüm (kartların kendi ComicCard sistemi var; uyum kontrol edilmeli) |
| Önizleme ekranı (Tab) | eski görünüm |
| Ayarlar penceresi (sekmeler, satırlar), duraklat, run sonu | eski görünüm |
| Diyalog kutusu | eski görünüm (kendi onaylı çizimi var; dokunulmamalı) |

## 5. Eksikler

| Öncelik | Eksik | Nerede gerekli | Not |
|---|---|---|---|
| Yüksek | Küçük ikon butonları: kapat (X), geri, ayarlar, duraklat | pencereler, dükkânlardan çıkış | `slot_blue` ve `slot_gold` zemin olur; yalnız **ikonlar** eksik |
| Yüksek | Aç-kapa düğmesi ve onay kutusu | ayarlar ekranı | pakette yok |
| Yüksek | Açılır liste (oku, açık liste zemini) | dil seçimi, çözünürlük | pakette yok |
| Orta | Kaydırma çubuğu (ray + tutamak) | uzun listeler (görevler, skill açıklamaları) | pakette yok |
| Orta | Kaydırıcı tutamağı ayrı parça | ayarlar ses düzeyleri | `hud_slider_blue` tek parça; ayrılabilir ya da yeni üretim |
| Orta | Fiyat etiketi / maliyet rozeti | saksı dükkânı (yetenek düğümünde istenmiyor, §7) | `label_cream` küçük boyda geçici olarak kullanılabilir |
| Orta | İkonlar: kilit, onay işareti, ünlem, bilgi (i) | skill düğümü, uyarılar | çıkarılan skill konseptinde vardı |
| Orta | Skill düğümü seviye noktaları + ağaç bağlantı çizgileri | yetenek ağacı | çıkarılan skill konseptinde vardı |
| Yüksek | **Boss ödülü item görselleri** (35 ödül) | boss ödülü kutusundaki yuva | kare, saydam zemin, yaklaşık 256 px; `BossRewardSO.icon` alanına atanır. Yokken etkiden seçilen yer tutucu ikon görünür |
| Düşük | Bildirim rozeti ("yeni!") | dükkân butonları | — |
| Düşük | Ana menü başlığı / logo | ana menü | sahnede başlık nesnesi yok; arka plan görseli tek başına |
| Düşük | Ana menü başlık / logo plakası | ana menü | — |
| Düşük | Yukarı ve sola bakan tooltip | tooltip'ler | var olanlar Unity'de çevrilerek kullanılabilir |

Yeni parça üretilecekse:
- **İstem:** "aynı çizim dili, yazısız, saydam zemin, tek parça, ortası dolu".
- **Hâller:** hover/basılı hâl istenmez, betik türetir.
- **Yerleşim:** sonuç `output/` altına konur, betiğe eklenir, sonra bu envanter güncellenir.

## 6. Yetenek ağacı — KİTE GEÇTİ (kullanıcı isteği, 6 Kasım 2026)

**Yapılanlar** (`ComicKitVerification`, 173 kontrol):
- **Düğüm yüzü** kitin karesi (`SkillNodeUI.StateSprite`). Gerçek oyun yoluyla görülen durumlar: altın, kömür, beyaz, yeşil.
- **Görülmeyen durumlar:** başlangıç erişimi (mavi) ve profil kapalı (lacivert) varsayılan run profilinde yok. Kodları aynı yolda; `KirilmaV1Verification` mavi durumu kontrol ediyor.
- **Kademe noktaları:** `pip_gold` / `pip_empty`.
- **"Başlangıçtan açık" etiketi:** `label_cream` + Bungee.
- **Bağlantılar:** `line_cream` (9 px). Başlangıç noktası `pip_gold`.
- **Tooltip'ler:** `ComicPopupView` ortak olduğu için yetenek, tile ve yerleştirme tooltip'lerinin hepsi kitte: `panel_cream` + amber'e boyalı `label_cream` şerit, başlık Bungee.
- **Dil turu:** tooltip tr / en / qps'de sığıyor.

**Yapılmayanlar / açık kalanlar:**
- Tooltip'in aile rengine göre şeridi (mavi / altın / yeşil / mor) yok; hepsi amber. `skill_tooltip_*` panelleri kullanılmadı, çünkü okları tooltip'in açıldığı yöne uymuyor.
- Düğümler küçük ve ağacın bir kısmı açılışta ekranın dışında kalıyor. Bu eski yerleşim; UI notlarındaki "yetenek ağacı boş ve küçük" maddesi hâlâ açık.

### İlk plan (6 Kasım, yapılmadan önce yazıldı)

Kullanıcı: "aynı kutuları ve olayları skill tree içinde de yapalım; özellikle node'ların kareleri bize uygun olsun".

### Bugün nasıl kurulu

- **Düğüm (`SkillNodeUI`):** kodla çizilen düz plakalar (gölge, kontur, yüz).
  - Yüzün rengi duruma göre değişiyor: başlangıç erişimi açık mavi, profil kapalı gri, tamamlandı yeşil, alınabilir sarı, alınamaz bej.
  - İkon prosedürel (`ResonanceBadgeGraphic`). Kademe noktaları düz renkli `Image`'lar.
  - "Başlangıçtan açık" etiketi düz krem plaka. Butonun geçişi yok; hover hareketi ayrı.
- **Bağlantılar (`SkillTreeUI`):** düz renkli ince çizgiler. Açık bağlantı beyaz, açılmış olan yarı saydam.
- **Tooltip:** `TooltipContent` prefab'ı (eski görünüm).
- **Ekran:** `SkillTreeSpaceBackground`; kaynak sayaçları panelin üstüne iğnelenir (`PinResources`); GERİ butonu (kitte, mavi).

### Önerilen eşleme (her şey elimizdeki parçalarla; yeni çizim yalnız yeşil kare için, betikte türetilir)

| Düğüm durumu | Kare | Gerekçe (UI-Dili renk görevleri) |
|---|---|---|
| Alınabilir (para ve önkoşul tamam) | `slot_gold` | altın = yetenek / yükseltme |
| Alınamaz / kilitli (önkoşul yok, para yetmiyor) | `slot_charcoal` | haritadaki kapalı hücreyle aynı |
| Kısmen alınmış (seviye > 0, şimdi yükseltilemiyor) | `slot_white` | kâğıt; kademe noktaları seviyeyi söyler |
| Tamamlandı (en yüksek seviye) | `slot_green` (**türetilecek**: `slot_blue`'dan renk döndürme) | bugünkü yeşil kimlik korunur |
| Başlangıç erişimi (profil verdi) | `slot_blue` | bugünkü açık mavi (134, 199, 232) ile aynı |
| Profil kapalı | `slot_navy` ya da kömürün pasif hâli | griden ayrışsın |

Diğer parçalar:
- **Hâller:** hover ve basılı karelerin hepsi aynı silüetten türetilir (butonlar gibi, SpriteSwap). Mevcut hover hareketi kalır.
- **Kademe noktaları:** yeni çizim yok. Küçük `slot_gold` (dolu) ve `slot_charcoal` (boş) kareler, 18–22 px.
- **Bağlantı çizgileri:** `divider` (krem mürekkepli çizgi, 9-dilim). Açık bağlantı tam, açılmış olan soluk. Dikey çizgiler döndürülür.
- **"Başlangıçtan açık" etiketi:** `label_cream`. Yazı Bungee.
- **Tooltip:** `skill_tooltip_<renk>`, ailesine göre (mavi / altın / yeşil / mor). Renk farkı yalnız ince bir çizgi olduğu için başlığa aile renginde `label_cream` şerit eklenir.
  - Tooltip imlecin solunda açılır.
- **Yazılar:** düğüm adı, seviye, fiyat ve başlıklar Bungee; açıklamalar gövde fontunda.

### Kurallar (UI-Dili §5)

- Bütün yazılar sığmazsa küçülür, kesilmez. Dil değişince hemen yenilenir.
- `ComicKitVerification`'a yetenek ağacı adımı eklenir:
  - her durumdan bir düğüm doğru karede mi;
  - noktalar ve bağlantılar kitte mi;
  - tooltip açılınca kutusuna sığıyor mu (tr / en / qps);
  - dükkândan çıkıp girince çoğalma yok mu;
  - görüntüler alınır.
- **Performans:** düğüm sayısı kadar Image var. Durum değişince yalnız sprite atanır, karede iş yok. Bağlantılar bir kez kurulur.

### Önce kullanıcıya gösterilecek

Eşleme tablosu: hangi durum hangi kare.

Örnek görüntüler: bir düğüm grubu, tooltip açık, tr ve en.

## 7. 10–11 Kasım 2026 eklemeleri

### Yetenek ağacı zemini — comic pano

Kullanıcı: "derinliği var ama kötü hissettiriyor".

- **Teşhis:** eski zemin yumuşak, gerçekçi bir uzaydı (neredeyse siyah taban, bulanık nebula, parlamalı yıldızlar, koyu kenar). Önündeki kit düz renkli mürekkepli kâğıt; iki çizim dili üst üste biniyordu ve düğümler kara bir boşlukta yüzüyordu.
- **Yeni zemin:** `Assets/Resources/ComicBoardBackground.shader` (özgün, dış içerik yok). Lacivert kâğıt, tram noktalı bulutlar, keskin kenarlı toon yıldızlar, düğümlere oturan ızgara (hücre = düğüm aralığı, düğüm hücrenin ortasında).
- **Derinlik korunur:** katmanlar ağaç kaydıkça farklı hızda kayar.
- **Geri dönüş:** `SkillTreeSpaceBackground.comicBoard = false` eski uzayı açar. Ayarlardan arka plan efektleri kapalıysa düz zemin kalır.

### Yetenek ağacı yerleşimi

Kullanıcı: "skiller çok uzak; iç içe girmeden yakınlaştır".

- **Yetenek verisi değişmedi** (gridPosition, komşuluk, önkoşul). Yalnız ekrandaki yer değişti: `SkillTreeUI.ApplyLayout`.
- Düğüm 60 → 84 px, aralık 150 → 112 px (ikinci istekle biraz daha yakın: "az daha yakınlaşsın").
- Dallar arasındaki boş koridorlar (3 ve üstü boş sıra / sütun) 2 hücreye iner. 1–2'lik adımlar aynen kalır; sıra ve hizalar korunur.
- **Ölçüm:** en uzak düğüm başlangıca 4500 → 2160 px. 104 düğümde üst üste binme yok (test).
- **Açık:** kök düğümler başlangıca hâlâ 2 hücre (240 px); sol kök 7 hücre uzakta. Bunlar verideki konumlardan geliyor; daha fazla yaklaştırmak veri ya da daha akıllı bir yerleşim ister.

### Yetenek ağacı hissi

Hepsi görsel + kısa ses; oynanış ve zamanlama değişmez.

- **Üstüne gelme:** düğüm 1,12'ye büyür, öne geçer, bağlantıları altın renge döner; kısa tık sesi.
- **Alınabilir düğüm:** yüzü hafifçe nefes alır (%3,5, 2,6 sn; faz konumdan, rastgelelik yok).
- **Satın alma:** eski zıplama ve dalga + yeni kademe noktası patlaması + alım sesi (kademeyle tizleşir).
- **Yeni açılan düğüm:** küçükten zıplayarak belirir; bağlantı çizgisi açık uçtan hedefe uzar.
- **Alınamayana tıklama:** kısa "olmaz" sallanması + pes ses.
- **Gezinme:** yumuşak zoom, sürükleyip bırakınca atalet.
- **Kademe noktaları** düğümün altında ortalandı (eskiden iki kademelide sola yaslıydı).
- **Performans:** karede yalnız kısa düğüm ve bağlantı döngüsü; bellek ayırmaz. Tween'ler panel kapanınca öldürülür.
- **Bilgi kutusu:** kaynak yetmiyorsa bedel kırmızı, eksik miktar yanında (`ui.skill.cost_missing`).

### Reddedilen: ağaç üstüne ek süs (11 Kasım)

Kullanıcı: "fiyat orda yazmasın, kötü duruyor; ilk hali temel olarak korunup juice'lanmalıydı, AI slop hissi verdi".

Denenip geri alındı: düğüm köşesinde fiyat etiketi, sol üstte başlık şeridi, sol altta gezinme ipucu, satın almada "pat" yıldızı (`ComicBurst`), elmas başlangıç karesi, bilgi kutusunda "satın almak için tıkla". **Kural:** ağaçta görünüm ilk kit hâlidir; his yalnız hareket ve sesle verilir, yeni plaka / etiket / yazı eklenmez.

### Ana menü

- Sahnedeki üç buton çalışma anında kite geçer (`MainMenuUI.ApplyComicKit`); sahne dosyası değişmedi.
- Oyna mavi (600×128), Ayarlar mor, Çıkış kırmızı (500×104). Yazılar Bungee; hover hareketi eklendi.
- "Oyna<br>" yazısındaki eski satır sonu yalnız çizimde atılır (`ComicKit.SingleLineCaps`); çeviri değeri aynı.
- Her karede malzeme yazan eski bileşen bu butonlardan kalktı.

### Ana menü — canlı sahne (11 Kasım, ikinci adım)

Kullanıcı: "daha canlı, sahne gibi; chill; oyunun uzay arka planı". AI ile üretilen arka planlar reddedildi (Docs/MenuArkaPlan, ThirdParty kaydı).

- **Ne var:** oyunun kendi parçaları. 7 × 7 karo (içte 5 × 5 açık, dışta kapalı halka; GameScene'deki GroundCell'in mesh'i ve malzemeleri), ortada 3 × 3 `Grass Planter 1x1` ve büyümüş bitkiler (`Assets/Prefabs/Plants`), altında toprak + kaya kütlesi, sarkan kökler, süzülen taşlar. Arkada oyunun `SpaceBackground`'u (hareketli yıldız, bulutsu, gezegen). Butonlar solda, ada sağda.
- **Editörde düzenlenir (AGENTS.md kuralı):** görünüm `Assets/Prefabs/Menu/MenuDiorama.prefab` ve MenuScene'de (Menu Camera, Menu Light). Kodda yalnız hareket: `MenuDioramaMotion` (süzülme, bitki sallanması, taşlar, kamera kayması; Inspector'dan ayarlanır).
- **Araç:** Tools > Menü > Canlı Sahne: Oluştur - Güncelle (eksik parçaları ekler, var olana ve elle düzenlemeye dokunmaz, çoğaltmaz, Undo'lu). Tools > Menü > Canlı Sahne: Kamerayı Varsayılana Al.
- **Işık:** menüde ışık yoktu; araç ilk kurulumda GameScene'in yönlü ışığını, ortam ışığını ve kameranın URP renderer'ını (1) kopyalar.
- **Butonlar:** konum / boyut artık sahnede (`MainMenuUI.sceneLayout` açık); kit görünümü hâlâ çalışma anında (ertelenen taşıma listesinde).
- **Test:** `MenuDioramaVerification` (sıfırdan kurulum, 17 kontrol) ve `RunAsIsBatch` (ana projenin hâli olduğu gibi).
- **Başlık tabelası (aynı gün):** butonların üstünde solda yamuk lacivert şerit (header_navy, -4°), altın Bungee "ERIGONE" (`ui.menu.title`), sahnede (Menu Title Sign). Menü açılınca yukarıdan iner, tepesinden asılı gibi çok az sallanır.
- **Butonlar:** menü açılınca soldan sırayla kayarak gelir, boşta hafifçe süzülür (`MenuUiMotion`, yalnız konum); üstüne gelince eğilir (Oyna 4,5°, öbürleri ±3°) ve nefes alır (`ComicHoverMotion.breathe`, yalnız menü butonlarında açık). Ayarlar sahnede, Inspector'dan değişir.
- **Ekranı dolduranlar:** arkada ve sol altta süzülen iki küçük ada (aynı karolar, bir saksı ve bitki, küçültülmüş alt kütle), adanın çevresinde yükselen polen ışıkları (ParticleSystem, MenuPollen.mat). Süs plakası yok; oyunun dünyası.
- **Lo-fi cila (aynı gün, kullanıcı: "toprağın altı bozuluyor, oran otursun; butonlar büyüsün; ERIGONE üste uzasın; arka plan chill lo-fi"):**
  - Alt kısım v2: derinlik genişliğin ~%50'si, her dörtgen tek düz yüz (çapraz çizgi yok), yönler kütlenin içinden (ters yüz yok, testte 0). Eski mesh yerinde yenilendi (nesneler ve ayarlar korundu). Süzülen taşlar küçüldü ve yanlara alındı.
  - Butonlar: Oyna 640×146, öbürleri 560×122. Tabela 820×196, ekranın üstüne uzanır; başlık 136 pt'ye kadar.
  - Lo-fi: kayan yıldız seyrek, yıldız / bulutsu kısık, paralaks yumuşak; süzülme, sallanma, kamera kayması ve buton nefesi yavaşladı.
  - Menü komutu: Tools > Menü > Canlı Sahne: Lo-fi Cila Uygula (yalnız açıkça çalıştırılınca değerleri yazar).
- **Toon tutarlılığı (kullanıcı: "toonicliği kopuyor gibi"):** nedenler: menü kamerası perspektif ve yakın (oyunda uzaktan izometrik; ince mürekkep çizgisi burada zayıf kalıyor), yeni parçalarda çizgi yoktu, polen yumuşak ışık noktasıydı. Düzeltme yalnız menüde: karo / saksı / bitki oyunun malzemelerinin menü varyantını kullanır (`Assets/Prefabs/Menu/Toon/*_Menu.mat`, Unity material variant; renk oyunla bağlı, dış çizgi ×1,8); toprak / kaya / kök / taş aynı mürekkep çizgisini alır, mesh'leri paylaşılan köşeli (v3: çizgi kopmaz); polen sert kenarlı, mürekkep çizgili parıltı (`MenuSparkle.png`), az ve küçük. Oyunun kendi malzemeleri değişmedi.
- **Açık:** adanın ve adacıkların yerleşimi editörde ince ayar bekliyor; gerçek logo yok (yazı tabelası).

### Çiftçi / tırpan seçim ekranı

- Başlık `header_navy` şeridinde. Kartlar `panel_cream` + boyanan `label_cream` şerit.
- Seçili: yeşil şerit + açık yeşil kâğıt. Kilitli: gri kâğıt + `window_navy` görev bandı.
- Geri mavi, Run'ı Başlat yeşil. Başlık, satır başlıkları ve adlar Bungee.
- Bütün yazılar sığmazsa küçülür.

### Boss ödülü — kare item kutusu

Kullanıcı: "kart gibi olmayacak; eski küçük ödül gibi ama item seçiyormuşuz gibi kutu, kare".

- **Ödül mantığı değişmedi** (`BossRewardManager`). Değişen: kutunun görünümü ve `BossRewardSO.icon` alanı.
- Kutu 440×466 (eski kartın oranı). Sol üstte 96 px `slot_gold` item yuvası, yanında altın ad plakası; altında eski üç yazı bölgesi ve AL.
- Görsel yokken yuvada `BossRewardText.IconRecipe` ikonu durur (ödülün etkisinden seçilir).
- "AL" yazısı çeviri listesinde (`ui.reward.panel.choose`); kutu dil değişince yenilenir.
- İlk deneme (uzun kart, 400×550) kullanıcı tarafından reddedildi.
