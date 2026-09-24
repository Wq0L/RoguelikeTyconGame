# Bumerang Orak ve Çapraz Elektrik

> Güncel ek: [SimpleResonance.md](SimpleResonance.md) ile kaynak saksının davranış hasarı rezonansı ve elektriğin öldürücü vuruşuna özel XP desteği eklendi. Aşağıdaki temel hasarlar bu ek çarpandan önceki değerlerdir. Hareket/geometri ve pool sınırları korunur.

İki yeni tile ailesi mevcut kart seçimi, rastgele hücre uygulaması, stat hesaplama ve skill kilidi akışına bağlandı. Eski enum değerleri korunup yenileri sona eklendi. Bu değişiklik karma rezonans tariflerini uygulamaz.

## Kartlar ve kilitler

Her aile Common / Rare / Epic / Legendary olmak üzere dört kart içerir. Tetikleme roll aralıkları sırasıyla %12–18 / %20–28 / %30–40 / %45–60. Aynı saksıdaki tile katkıları toplanır, toplam şans %100'de sınırlanır. Kart başına roll bir kez yapılır; seçilen değer hücreye aynen taşınır. Açılan iki ailenin kart tipi ağırlığı 10'ar; bu bir yüzde değil, mevcut havuzdaki diğer ağırlıklarla normalize edilir.

- P9 **Bumerang Orak Kartları**: Tornado Kartları (P4) sonrası, 60 Iron.
- P10 **Çapraz Elektrik Kartları**: Patlayıcı Kartlar (P3) sonrası, 80 Iron.
- Toplam skill ağacı 140 node / 304 kademe. Kilit satın almak davranışı doğrudan vermez; kartı havuza açar. Kartın saksının altına gelmesi gerekir.

## Bumerang Orak

Doğrudan hasat sonrası şans tutarsa, dört ana yönden biri ve 2 veya 3 hücre menzil rastgele seçilir. Orak tek grid ekseninde düz gider ve aynı çizgiden döner; kendi eksenindeki dönüşü korunur. Hedef grid dışına taşabilir; orada hasar alacak bitki yoksa yalnız görsel yolculuk olur. Her bacak mesafeye göre 0,4–1,6 saniye; dönüş kaynak hücrenin ilk konumunadır.

Hasar oyuncunun tetikleme anındaki HarvestDamage değerinin %65'i, en az 1. Yol boyunca süpürülen küçük çizgi parçaları kontrol edilir; düşük FPS'de hücre atlamaz. Aynı yaşayan bitki gidişte bir, dönüşte bir kez vurulabilir. Hasar fiziksel yol üzerindeki hücrelere uygulanır; seçilen hedefe otomatik isabet garantisi yoktur. Ayrı kritik atışı veya hedef saksının Odak bonusu uygulanmaz; Tornado'nun mevcut ikincil hasar yaklaşımı izlenir.

## Çapraz Elektrik

Saksının kapladığı dikdörtgenin dört köşesinden dışarı doğru çapraz ışın çıkar. Her köşede 1 ve 2 hücre ilerideki hücreler hedeflenir: iç bölgede toplam sekiz aday. Yatay/dikey komşular ve saksının kendi hücreleri vurulmaz. Döndürülmüş 2×3 saksı gerçek occupiedGrids üzerinden 3×2 olarak değerlendirilir. Harita dışı, eksik veya kilitli hücreler atlanır. Aynı hücre tek tetiklemede en fazla bir kez vurulur.

Hasar oyuncunun tetikleme anındaki HarvestDamage değeridir. Camgöbeği dış çizgi ve beyaz çekirdekli zikzak VFX 0,28 saniye sürer. Görsel kıpırdama oyun RNG'sini tüketmez. VFX çizgisi arada başka hücrenin üstünden geçse bile hasar yalnız tarifin çapraz hedef hücrelerine uygulanır.

## Pool ve yaşam döngüsü

HarvestBehaviorManager, projenin mevcut VFXPool<T> sınıfını kullanır. Başlangıçta 6 orak ve 8 elektrik efekti hazırlanır. Aktif sınır dolduğunda yeni tetikleme atlanır; havuz sürekli büyümez. Elektrik çizgileri ve orak modeli havuz nesnesi oluşturulurken hazırlanır; vuruş başına prefab oluşturulmaz.

Shop/kart ekranlarında scaled time ile hareket ve efekt süresi durur. Round bitişi, menü/yeni koşu/koşu bitişi ve manager kapanışı aktif efektleri iade eder. Kaynak saksı kaldırılırsa orak ve devam eden elektrik görseli sonlanır. Trail, vuruş geçmişi ve aktif çizgiler havuza dönüşte temizlenir. Orak/elektrik öldürmeleri DamageTypeRules uyarınca yeni davranış tetiklemez; normal PlantHealth/PlantSpawner ödül ve ölüm akışı kullanılır.

## Model ve mouse göstergesi

Blender kaynağı `ArtSource/Scythe/SciFiScythe.blend`, önizleme aynı klasörde PNG, oyun modeli `Assets/Art/Behaviors/SciFiScythe.fbx`. Kaynak parçalara ayrılmış düzenlenebilir modeldir; FBX dört materyal grubuna birleştirilmiştir. Yeniden üretim: Blender background modunda `Tools/build-sci-fi-scythe.py`.

Model mevcut Simple Toon/SToon Outline shader'ını kullanır. Shader'a varsayılan davranışı koruyan opsiyonel depth-test/depth-write ayarları eklendi. Mouse dairesi ve sap ucu sabit, büyük dönen orak, dünya objelerinin üzerinden çizilir; ekran UI'sı normalde üstte kalır. Dairenin saldırı hesabı değiştirilmedi. Üç ince rüzgâr yayı ile birlikte tek kalıcı cursor nesnesi kullanılır, saldırı başına oluşturulmaz.

## Kontroller

- `Tools/Harvest Behaviors/Author Approved Assets`: açıkça çalıştırıldığında model materyalleri, prefablar, kartlar ve sahne bağlantılarını üretir. Import sırasında otomatik çalışmaz; Inspector ayarlarını değiştirdiysen yeniden üretmeden önce incele.
- `HarvestBehaviorVerification.RunBatch`: geometri, kart/skill bağlantıları ve gerçek Play Mode'da hasar, havuz sınırı, tekrar kullanım, duraklama, round/saksı temizliği kontrolleri. Sonuç `Logs/HarvestBehaviorVerification.txt`.
- `node Tools/verify-final-tree-scene.cjs`: 140 skill'in mevcut sahne referansları.
- Economy Analyzer bu iki yan hasar davranışını henüz simüle etmez; bu tile'lar seçilince kapsam uyarısı gösterir. Önceki ekonomi sonuçları yeni davranışlarla ölçülmüş gelir olarak yorumlanmamalıdır.

## Son görsel ve hız revizyonu

Oyuncu orağı model düzleminden yatay konuma yatırılır. Sapın dar uç noktası mouse merkezinde sabit tutulur; kesici taraf 540 derece/saniye hızla döner. Model ölçeği eski 0,28 yerine en az 0,6 ve saldırı yarıçapıyla orantılıdır. Tooltip ekran/kamera güvenli alanından taşarsa karşı tarafa alınır ve sınır içine çekilir. Son değişiklikler C# derlemesiyle kontrol edildi; önceki 111 kontrol yeni görsel revizyonun Play Mode doğrulaması sayılmaz.
