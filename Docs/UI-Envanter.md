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
| Yetenek ağacı (düğümler, noktalar, bağlantılar, etiket, GERİ) | **kite geçti** (yerleşim / boyut eski) |
| Tooltip'ler (yetenek, tile, yerleştirme — ortak ComicPopupView) | **kite geçti** |
| Kart seçimi, boss ödülü, uzmanlaşma paneli | eski görünüm (kartların kendi ComicCard sistemi var; uyum kontrol edilmeli) |
| Önizleme ekranı (Tab) | eski görünüm |
| Ayarlar, duraklat, ana menü, run sonu | eski görünüm |
| Diyalog kutusu | eski görünüm (kendi onaylı çizimi var; dokunulmamalı) |

## 5. Eksikler

| Öncelik | Eksik | Nerede gerekli | Not |
|---|---|---|---|
| Yüksek | Küçük ikon butonları: kapat (X), geri, ayarlar, duraklat | pencereler, dükkânlardan çıkış | `slot_blue` ve `slot_gold` zemin olur; yalnız **ikonlar** eksik |
| Yüksek | Aç-kapa düğmesi ve onay kutusu | ayarlar ekranı | pakette yok |
| Yüksek | Açılır liste (oku, açık liste zemini) | dil seçimi, çözünürlük | pakette yok |
| Orta | Kaydırma çubuğu (ray + tutamak) | uzun listeler (görevler, skill açıklamaları) | pakette yok |
| Orta | Kaydırıcı tutamağı ayrı parça | ayarlar ses düzeyleri | `hud_slider_blue` tek parça; ayrılabilir ya da yeni üretim |
| Orta | Fiyat etiketi / maliyet rozeti | saksı dükkânı, yetenek düğümü | `label_cream` küçük boyda geçici olarak kullanılabilir |
| Orta | İkonlar: kilit, onay işareti, ünlem, bilgi (i) | skill düğümü, uyarılar | çıkarılan skill konseptinde vardı |
| Orta | Skill düğümü seviye noktaları + ağaç bağlantı çizgileri | yetenek ağacı | çıkarılan skill konseptinde vardı |
| Düşük | Bildirim rozeti ("yeni!") | dükkân butonları | — |
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
