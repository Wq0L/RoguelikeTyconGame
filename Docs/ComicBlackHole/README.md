# Comic kara delik — ilk VFX dersi

Dosya: `ComicBlackHole.blend`. Sahne: `Comic Black Hole - Lesson`.
Bu örnek Blender'da üretilmiş hareketli bir 3D taslaktır; henüz Unity prefab'ı değildir.
Tam döngü 24 fps, 96 kare = 4 saniyedir. 97. kare başlangıç pozunu tekrarlar,
bu yüzden oynatma aralığına dahil değildir.

## Önce hareketi gör

1. Fare 3D görünümün üzerindeyken **Space** ile oynat/durdur.
2. **Numpad 0** kamera görünümüne geçer. Numpad yoksa View > Cameras > Active Camera.
3. Sağ üstteki Outliner'da `02`, `03`, `04` koleksiyonlarının göz simgesini kapat.
   Yalnızca koyu merkez kalır. Koleksiyonları sırayla açarak katkılarını gör.
4. Timeline'da 1, 25, 49, 73. kareleri incele. Taşlar yer değiştirirken merkez sabit kalır.

## 1 — Merkez: basit şekille başla

`01 - Core`: tek bir UV Sphere ve koyu mürekkep rengi.
Bu merkez ışık bükülmesi simülasyonu yapmaz; comic anlatım için opak bir siluet kullanır.
Kendi denemeni ayrı bir sahnede Add > Mesh > UV Sphere ile yapabilirsin.

## 2 — Halkalar: tam simit yerine uçları incelen şerit

`02 - Accretion ribbons`: dört farklı yarıçapta, üçer parçalı düz mesh şeritler.
Her şeridin arkasında biraz geniş koyu bir kopya kontur oluşturur.
Materyaller Emission kullanır: renkler sahne ışığından bağımsız, düz görünür.
Emission tek başına etrafa ışık saçılması veya bloom anlamına gelmez.

Elle çalışırken bir çemberin birkaç kenarını seçip kalanından ayrı bir yay yap;
edit mode'da dışa doğru extrude ederek genişlik ver, uçları daralt.
Bu derste aynı geometri `build_black_hole.py` içindeki `ribbon()` fonksiyonuyla üretilir.
Formül ezberlemek yerine şeridin iki kenarı ve bu kenarlar arasındaki yüzleri incele.

## 3 — Taş: az yüzey, birkaç belirgin renk

`03 - Orbiting rocks`: 11 düşük poligonlu Icosphere.
Yüzeylere gölge, orta ton, açık ton ve sıcak kenar rengi atanır.
`ink edges` çocuğundaki Wireframe modifier ince koyu kenarlar üretir.
Bu renkler sanat yönlendirmesiyle atanmıştır; fiziksel olarak hesaplanan ışık değildir.

İlk alıştırma: bir taşın ana objesini seç, S ile büyüt, tekrar oynat.
Kontur çocuğu birlikte büyür. Orbit pivot'unu büyütürsen yörünge de değişir.

## 4 — İki dönüşü ayır

Her taşın bir `Orbit pivot` üst nesnesi vardır.
Üst nesnenin Z dönüşü taşı merkez etrafında dolaştırır.
Taşın kendi Z dönüşü ise kendi etrafında takla/dönüş hareketi verir.
Bu ayrım gezegen, kalkan parçaları ve büyü halkalarında da kullanılır.

Elle yeniden üretmek için:

1. Merkeze Add > Empty > Plain Axes ekle.
2. Taşı seç, Shift ile Empty'yi son seçilen nesne yap.
3. Ctrl+P > Object (Keep Transform) ile taşı Empty'ye bağla.
4. Kare 1: Empty'nin Z dönüşünü 0 yap; Rotation alanına sağ tıklayıp Insert Keyframe.
5. Kare 97: Z dönüşünü 360 yap; tekrar anahtar kare ekle.
6. Graph Editor'da anahtarları seç, T > Linear.
7. Oynatma sonunu 96 yap. Başta/sonda yavaşlama olmadan döngü oluşur.

İlk değişiklik denemesi: halkalardan birinin 97. kare Z değerini 360'tan -360'a
çevir. Yön tersine döner. 720 yaparsan aynı sürede iki tur atar.

## 5 — Hız izleri

`04 - Speed streaks`: ince kısa yaylar. Taşların arkasındaki yaylar aynı orbit
pivot'una bağlıdır; dıştaki küçük vurgular sabittir. Ayrıntı eklemeden önce büyük
merkez ve halka siluetinin okunmasını kontrol et.

## Unity'ye geçerken

Bu .blend henüz oyuna bağlanmadı. İki yol var: meshleri dışa aktarıp Unity'de
Unlit materyaller ve dönüş koduyla yeniden kurmak veya sabit kameradan şeffaf
flipbook almak. Blender shader düğümleri Unity'ye birebir taşınmaz. İlk yol
kamerası hareket eden oyunda daha esnektir; ikinci yol sabit bakışlı efekt için uygundur.

Kaynak üretim betiği: `Tools/Art/ComicBlackHole/build_black_hole.py`.
Tüm şekiller bu örnek için sıfırdan oluşturuldu; hazır VFX/model/doku kullanılmadı.
