# Yetenek ağacı ikon listesi

12 Kasım 2026. Kullanıcı istedi: "skill tree'de kullanılan ikonların listesini çıkar, yandan yaptırayım".

Kaynak: oyunun etkin ağacı. Profil `Run50_GecOyunV1`, set `SkillTreeSet_DengeV1`. Liste veriden okunarak çıkarıldı; yetenek verisine dokunulmadı.

Bütün düğümlerin dökümü: `Docs/SkillTree-Ikonlar-Dugumler.csv`. Sütunlar: ikon, asset, Türkçe ad, İngilizce ad, kilit türü, ilk etki, kademe sayısı. Döküm aracı: `SkillIconListDump`.

## Şu an nasıl çalışıyor

Ağaçta 104 düğüm var. Bir yetenek ailesinin bütün kademeleri aynı ikonu kullanır (ör. "Geniş Tarama 1–4"), bu yüzden düğümler **35 aileye** ayrılır.

Oyun ikonu düğümün etkisinden seçer (`SkillIconCatalog`), adına bakmaz. Bugün kullanılan **19 farklı ikon** var. Bunlar kodla çizilen basit simgeler; dosya değiller.

Seçenekler:

- **A. Asgari, 19 ikon:** Her etki türüne bir ikon. Bugünkü simgelerin birebir yerine geçer.
- **B. Zengin, 35 ikon:** Her yetenek ailesine ayrı ikon. Örneğin "Kritik Odak" (kritik şansı) ile "Kritik Güç" (kritik çarpanı) farklı görünür.
  - Bu, küçük bir kod işi ister: ikonu aileye göre seçmek.
  - Yetenek verisine dokunulmadan yapılır; ayrı bir ikon kataloğu kullanılır.

**Karar (12 Kasım 2026, kullanıcı):** A seçeneği, yani 19 ikon.

## Teknik istek (yaptıracağın kişi veya araç için)

- **Biçim:** PNG, saydam zemin, kare **512×512**. Oyunda yaklaşık 60–80 px görünür, o boyutta okunmalı.
- **Kompozisyon:** Ortada tek bir nesne. Kenarlarda ~%10 boşluk olsun. İkonun içinde yazı ya da rakam olmasın.
- **Stil:** Oyunun çizgi roman kitiyle uyumlu.
  - Kalın koyu mürekkep kontur (koyu mor-siyah).
  - 2–3 tonlu düz boyama, ışık sol üstten.
  - Gölge hafif mora kaysın (karo / saksı / gezegen diliyle aynı).
  - Parlama ve yumuşak gradyan yok.
- **Tutarlılık:**
  - Hepsi aynı kontur kalınlığında ve aynı bakış açısında (düz, önden).
  - Siluetler birbirinden ayırt edilebilir olsun; en önemli ayırt edici siluettir, renk ikinci sıradadır.
- **Kaynak ikonlar:** Altın, demir ve taş için HUD'daki kaynak ikonlarına benzesin; oyuncu aynı şeyi tanısın.
- **Ad:** `skill_<kimlik>.png` (aşağıdaki kimlikler). Seçenek B'de `skill_<aile>.png`.
- **AI ile yaptırılırsa:** Steam AI beyanına eklenmeli; kaynağı `Docs/ThirdParty-Licenses.md` dosyasına yazacağım.

## A. 19 ikon (etki türüne göre)

| # | Kimlik | Ne anlatır | Kullanan aileler (düğüm sayısı) | Görsel öneri |
|---|---|---|---|---|
| 1 | `attack` | Hasat hasarı | Keskin Başlangıç, Kesim Tekniği, Güçlü Kesim, Kesim Ustalığı, Ağır Kesim (20) | Tırpan bıçağı, kesik darbe çizgisiyle |
| 2 | `speed` | Saldırı hızı | Akıcı Kesim, Hızlı Eller (8) | Arkasında 3 hız çizgisi olan savrulan tırpan |
| 3 | `area` | Saldırı alanı | Geniş Tarama, Geniş Süpürüş (8) | Ortadan dışa açılan halka, 4 küçük ok |
| 4 | `critical` | Kritik şans / çarpan | Kritik Odak, Kritik Güç (8) | Çatlamış hedef tahtası, kıvılcım |
| 5 | `spawn` | Bitki çıkma hızı | Verimli Üretim, Düzenli Üretim (8) | Topraktan çıkan filiz, yanında küçük artı |
| 6 | `rare` | Nadir bitki şansı | Nadir Filizler (4) | Parlayan altın / mor filiz, küçük yıldız |
| 7 | `gold` | Altın kazancı | Altın Hasat I, Altın Hasat II (8) | Altın para yığını (HUD altını gibi) |
| 8 | `iron` | Demir kazancı | Demir Hasat, Zengin Cevher (8) | Demir külçe (HUD demiri gibi) |
| 9 | `stone` | Taş kazancı | Taş Hasat (4) | Taş parçası (HUD taşı gibi) |
| 10 | `xp` | XP kazancı | Hasat Deneyimi I, Hasat Deneyimi II (8) | Yaprak biçimli yeşil kristal, yukarı ok |
| 11 | `score` | Hasat puanı | Hasat Rekoru (4) | Taşan hasat sepeti ya da küçük kupa |
| 12 | `grid` | Tarla büyümesi | Grid Genişleme I, Grid Genişleme II (2) | Köşelerinden dışa ok çıkan kare ızgara |
| 13 | `planter` | Saksı kilidi | 1×3 Saksı, 2×2 Saksı, 2×3 Saksı (3) | Oyundaki sekizgen saksı |
| 14 | `cards` | Kart seçenekleri | Kart Sezgisi, İlk Vazgeçiş, İkinci Vazgeçiş (6) | Yelpaze açılmış 3 kart |
| 15 | `explosion` | Patlayıcı kartlar | Patlayıcı Kartlar (1) | Çizgi roman patlama bulutu |
| 16 | `duplicate` | Çifte hasat kartları | Çifte Hasat Kartları (1) | Yan yana ikiz filiz |
| 17 | `tornado` | Tornado kartları | Tornado Kartları (1) | Hortum |
| 18 | `scythe` | Bumerang orak kartları | Bumerang Orak Kartları (1) | Kavisli orak, etrafında dönüş oku |
| 19 | `electric` | Çapraz elektrik kartları | Çapraz Elektrik Kartları (1) | Şimşek |

Katalogda olan ama bu ağaçta kullanılmayan iki ikon daha var; istersen yaptırılabilir:
- `time` (round süresi): kum saati.
- `unlock` (genel kilit): açık kilit.

## B. 35 ikon (yetenek ailesine göre, isteğe bağlı)

Aynı türdeki aileler A'daki ikonun çeşitleri olabilir: aynı nesne, farklı bir ayrıntı.

| Tür | Aile (TR / EN) | Kademe | Ayırt edici öneri |
|---|---|---|---|
| attack | Keskin Başlangıç / Sharp Start | 4 | Parlayan yeni bıçak |
| attack | Kesim Tekniği / Cutting Technique | 4 | Bıçak, düzgün kesik çizgisi |
| attack | Güçlü Kesim / Strong Cut | 4 | Kalın bıçak, büyük darbe |
| attack | Kesim Ustalığı / Cutting Mastery | 4 | Çapraz iki bıçak |
| attack | Ağır Kesim / Heavy Cut | 4 | Ağır balta gibi geniş bıçak |
| speed | Akıcı Kesim / Fluid Cut | 4 | Dalgalı hız izi |
| speed | Hızlı Eller / Quick Hands | 4 | Eldiven, hız çizgileri |
| area | Geniş Tarama / Wide Scan | 4 | Radar halkaları |
| area | Geniş Süpürüş / Wide Sweep | 4 | Geniş yay çizen tırpan izi |
| critical | Kritik Odak / Critical Focus | 4 | Hedef tahtası (şans) |
| critical | Kritik Güç / Critical Power | 4 | Patlayan yıldız (çarpan) |
| spawn | Verimli Üretim / Fruitful Production | 4 | Bol yapraklı filiz |
| spawn | Düzenli Üretim / Steady Production | 4 | Sıralı üç fide |
| rare | Nadir Filizler / Rare Sprouts | 4 | Yıldızlı filiz |
| gold | Altın Hasat I / Gold Harvest I | 4 | Tek altın |
| gold | Altın Hasat II / Gold Harvest II | 4 | Altın yığını |
| iron | Demir Hasat / Iron Harvest | 4 | Demir külçe |
| iron | Zengin Cevher / Rich Ore | 4 | Cevherli kaya |
| stone | Taş Hasat / Stone Harvest | 4 | Taş parçası |
| xp | Hasat Deneyimi I / Harvest Experience I | 4 | Tek kristal |
| xp | Hasat Deneyimi II / Harvest Experience II | 4 | Çift kristal |
| score | Hasat Rekoru / Harvest Record | 4 | Kupa |
| grid | Grid Genişleme I / Grid Expansion I | 1 | Küçük ızgara, oklar |
| grid | Grid Genişleme II / Grid Expansion II | 1 | Büyük ızgara, oklar |
| planter | 1×3 Saksı / 1×3 Planter | 1 | Uzun saksı |
| planter | 2×2 Saksı / 2×2 Planter | 1 | Kare saksı |
| planter | 2×3 Saksı / 2×3 Planter | 1 | Büyük dikdörtgen saksı |
| cards | Kart Sezgisi / Card Intuition | 4 | Kart, üstünde göz |
| cards | İlk Vazgeçiş / First Skip | 1 | Kart, atla oku |
| cards | İkinci Vazgeçiş / Second Skip | 1 | İki kart, atla oku |
| explosion | Patlayıcı Kartlar / Explosive Cards | 1 | Patlama bulutu |
| duplicate | Çifte Hasat Kartları / Double Harvest Cards | 1 | İkiz filiz |
| tornado | Tornado Kartları / Tornado Cards | 1 | Hortum |
| scythe | Bumerang Orak Kartları / Boomerang Scythe Cards | 1 | Bumerang orak |
| electric | Çapraz Elektrik Kartları / Cross Electric Cards | 1 | Çapraz şimşek |

## Bağlandı (12 Kasım 2026)

19 ikon `Assets/Art/SkillIcons/<kimlik>.png` altında.

- **Araç:** `Tools > Oyun > Yetenek İkonları: Bağla - Güncelle` (`SkillIconInstaller`).
  - İçe aktarma: Sprite, en çok 256 px, mip.
  - Set: `Assets/Resources/SkillIconSet.asset`. Inspector'dan bir kimliğe başka sprite verilebilir.
- **Ağaç düğümü ve ipucu penceresi:** çizilmiş ikonu gösterir. Çizimi olmayan kimlikte (`time`, `unlock`) eski kodla çizilen simge kalır.
- **Boss ödül kartlarındaki yer tutucu ikonlar değişmedi.** Ödül görselleri ayrıca yapılacak.
- **Kaynak ikonları:** Kullanıcının isteğiyle `gold`, `iron`, `stone` artık oyunun kaynak ikonları da.
  - Temanın altın / demir / taş sprite'ı: HUD, saksı dükkânı, round özeti, ağaçtaki kaynak göstergeleri.
  - GameScene ve DemoScene'de eski altını doğrudan gösteren 2 + 2 görsel.
  - Eski `Coin / Iron / Stone Sprite` dosyaları silinmedi.
- **Doğrulama:** `SkillIconVerification` 12/12, `ComicKitVerification` 188/188.

## İkonlar gelince (ilk plan)

- PNG'leri bir klasöre koyman yeter, örneğin `Assets/Art/SkillIcons/`.
- Bağlantıyı ben yaparım: ikon kataloğu kimlikten (A) ya da aileden (B) sprite'a gider. Kodla çizilen simge yedek olarak kalır.
- Önce izole kopyada ağaç ekranında gösteririm.
- Yetenek verisine (SkillNodeSO asset'leri) dokunulmaz.
