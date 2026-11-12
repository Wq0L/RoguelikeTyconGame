# Boyalı görünüm (karolar, saksılar, hafif mürekkep geçişi)

11–12 Kasım 2026'da yapıldı. Kullanıcı izole kopyada seçti ve onayladı:
- boyama ve **hafif** geçiş birlikte,
- hafif krem ton,
- karo başına **3 çeşit**,
- optimizasyona dikkat.

## Oyunda ne var

- **Karolar** (`Assets/ProjectSettings/Ground.prefab`, GameScene'de 121 örnek):
  - Model: `Assets/Art/Painted/Meshes/SM_Floor_V1_Boyali.asset`. Özgün modelin boyamaya uygun UV'li kopyası; özgün FBX değişmedi.
  - Malzemeler: açık karo için `Floor_Boyali_Acik_V0..2`, kapalı karo için `Floor_Boyali_Kilitli_V0..2`.
  - Her karo çeşidini kendi konumundan alır (`PaintedLookInstaller.VariantFor`). Sonuç sahneye veri olarak yazılıdır; çalışma anında rastgelelik yok.
  - Bir karonun çeşidi Inspector'dan değiştirilebilir: `GroundCell.lockedMaterial` / `unlockedMaterials`.
- **Saksılar** (5 boy, `Assets/Prefabs/Grass Planter *.prefab`):
  - Görünen parça `Assets/Art/Painted/Meshes/Saksi_<boy>_Boyali.fbx` ve `Saksi_Boyali_<boy>.mat` kullanır.
  - Hayalet (yerleştirme) parçası değişmedi.
- **Hafif mürekkep geçişi:** `ToonReferenceRenderer` üzerindeki "Hades Stylize" özelliği.
  - Shader: `Assets/Shaders/HadesStylize.shader`.
  - Değerler `Assets/Art/Painted/Materials/Hades Stylize.mat` içinde (çizgi 1,6 · gölge 0,5 · tarama 0,15 · kenar ışığı 0,35).
  - Saydamlardan önce çalışır: arayüz ve parçacıklar etkilenmez.
  - Oyun ve menü kamerası aynı renderer'ı kullanır.
- **Hasat imleci:** `HarvestCursor.prefab` içinde iki ek parça var: "Ring Ink" (beyaz halkanın altında koyu halka) ve "Ground Fill" (alanı hafif koyulaştırır). Renk ve kalınlık prefabdan ayarlanır.
- **Menü dioraması:** karolar ve saksılar boyalı menü varyantlarını kullanır (`Assets/Prefabs/Menu/Toon/*_Menu.mat`).
- **Gezegen** (12 Kasım; kullanıcı: "gezegen sırıtmış", "biraz daha çizgisel"): kodla çiziliyor, `Assets/Resources/SpaceBackground.shader` içindeki gezegen bölümü. Oyunda ve menüde aynı.
  - 3 bant (sıcak ışık, orta, mora kayan gölge), sınırları titrek.
  - Gölge sınırında kalın mürekkep, ışık sınırında ince kesik çizgi.
  - Leke kenarlarında ve kuru fırça izlerinde çizgi.
  - Gölgede tarama, en koyu yerde çapraz tarama. Işık kenarında kırık parlama.
  - Kalın kontur. Yumuşak hale yerine iki sert atmosfer halkası.
  - Renk, konum ve boyut `SpaceBackground` bileşeninde.
  - Önceki hâlin kopyası yalnız oturum notlarında; geri dönmek gerekirse git geçmişinden alınır.
- **Üst arayüz payı** (12 Kasım; kullanıcı: "round kartı / süre bazı şeylerin üstüne çıkıyor"):
  - Kamera tarlayı round kartı + süre plakasının gerçek alt kenarının altına indirir (`GridUnlockFeel.HudRoom`, `ComicHud.TryGetTopCenterBottom`).
  - En az %5 pay kalır. Pencere boyutu değişince yeniden hesaplanır.
  - Doğrulama: `HudRoomVerification`.
  - İkinci tur (aynı gün; kullanıcı ekranında hâlâ biniyordu, koordinat harflerinin de üstüne geliyordu):
    - Kart gizliyken de ölçülür. Hazırlık ekranında round arayüzü kapalıydı ve pay varsayılana düşüyordu.
    - Kartın alt kenarı değişince pay yeniden hesaplanır.
    - Koordinat rozetleri (kameraya dönük levhalar) hesaba girer.
    - Üst orta arayüz küçüldü: kart 290×128 → 252×108, süre plakası 176×66 → 156×56; süre plakası daha az sarkar.

## Araçlar

- **`Tools > Oyun > Boyalı Görünüm: Kur - Güncelle`** (`PaintedLookInstaller`):
  - Yalnız özgün hâldeki şeyi değiştirir; elle yapılan ayara dokunmaz.
  - İki kez çalıştırmak bir şeyi çoğaltmaz.
- **`Tools > Oyun > Hasat İmleci: Okunurluk`** (`HarvestCursorReadability`).
- **Doğrulama:** `Tools/run-isolated-verification.ps1 -Method PaintedLookVerification.RunBatch -Full`. Ana projenin durumunu olduğu gibi denetlemek için `RunAsIsBatch`.

## Dokuları yeniden boyamak

Dokular kodla boyanır (AI yok); kaynak kod `Tools/Art/HadesPrototype/` klasöründe. Blender Steam kurulumu: `C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe`.

- **Karolar:** `blender -b --factory-startup -P Tools/Art/HadesPrototype/paint_floor_tiles.py -- <çıktı klasörü>`
  - Çeşitler `VARIANTS`, renkler `OPEN` / `LOCKED` ile ayarlanır.
  - Işık yönü bütün çeşitlerde aynıdır, bu yüzden karo döndürülmez.
- **Saksılar:** `blender -b --factory-startup -P Tools/Art/HadesPrototype/paint_planter.py -- <çıktı klasörü> <1x1|1x2|1x3|2x2|2x3>`
  - Yeni UV açar ve FBX yazar. Birim ayarı `FBX_SCALE_UNITS` olmalı; olmazsa model Unity'ye 100 kat küçük gelir.
- **Çıktıyı oyuna almak:** `Assets/Art/Painted/Textures` ve `Meshes` klasörlerindeki dosyaların üzerine yazılır.

## Elle boyama

- **Karo dokusu** (2048²):
  - Sol üstteki 1520 piksellik kare, karonun tepeden görünüşüdür.
  - Alt şerit, dört yan duvarı yan yana gösterir.
  - Krita veya Photoshop'ta doğrudan boyanabilir.
- **Saksı:** FBX Blender'da açılıp Texture Paint ile boyanabilir.

## Performans

Ölçülenler:
- 11 doku oyunda en çok 1024 piksel, mip'li ve sıkıştırılmış. Editörde ölçülen bellek: 14,7 MB.
- İmleç güncellemesi her karede bellek ayırmıyor (200 çağrıda 0 bayt).
- Sahnede 6 boyalı karo malzemesi var; saksılarda her boy için 1 malzeme.

Ölçülmedi:
- Mürekkep geçişinin GPU maliyeti. Geçiş, derinlik/normal ön geçişi ve piksel başına yaklaşık 16 doku okuması ekliyor.
- Gerekirse geçiş, Ayarlar menüsüne bir aç/kapa seçeneği olarak eklenebilir.

## Açık

- Karo modelinin (`SM_Floor_V1.fbx`) kaynağı ve lisansı kayıtlı değil (ThirdParty-Licenses).
- Kullanıcının kendi Play testi henüz yapılmadı.
- Menünün adacık altı toprağı ve bitkiler boyanmadı.
