# Comic meteor — hacimli düşüş ve çarpma

`ComicMeteor.blend`: 24 fps, 1–120 kare, 5 saniyelik öğrenme sahnesi.
Kara delik ayrı olarak `Docs/ComicBlackHole/ComicBlackHole.blend` içinde korunur.

## İzleme

- Fare 3D görünümdeyken Space: oynat/durdur. Shift+Sol ok: ilk kare.
- Numpad 0: kamera görünümü; orta fare sürükle: gerçek 3D hacmi farklı açıdan incele.
- 28. kare: alevli düşüş. 44: çarpma. 50: patlama ve kıvılcımlar. 65: dağılan duman.
- Sağ üstte numaralı koleksiyonların gözlerini kapatarak katmanları ayrı incele.

## Nasıl yapıldı?

1. Düz yüzeyli meteor taşı: bozulmuş Icosphere. İnce Wireframe kopyası sıcak çatlak çizgilerini oluşturur.
2. Kuyruk: farklı boylarda hacimli, sivri alev meshleri. Meteorla aynı üst nesneye bağlıdır.
3. Düşüş: üst nesnenin konum anahtarları. Taşın kendi dönüşü ayrıca anahtarlanır.
4. Çarpma: 44. karede meteor küçülerek kaybolur; kısa parlama ve radyal 3D sivri parçalar açılır.
5. Şok halkası: zemine paralel Torus büyür ve incelir.
6. Duman: 12 hacimli, köşeli küre dışa ve yukarı ilerler; genişleyip küçülerek biter.
7. Parçacıklar: 14 taş ve 28 uzatılmış kıvılcım, farklı balistik yörüngelerde dağılır.
8. Kamera: perspektif; çarpma yaklaşırken aşağı bakar ve yaklaşır.

Materyaller ışığı üç keskin tona ayırır (Diffuse → Shader to RGB → Constant ColorRamp).
Bu Eevee'ye özgü toon kurulumudur; Unity'ye birebir aktarılmaz.
Işık tonu + perspektif + derinlikte dağılan parçalar 3D hissini sağlar.
Sahne sessizdir. Hareket fizik simülasyonu değil, düzenlenebilir anahtar karelerle tasarlanmıştır.
5 saniyenin sonunda tekrar oynatma yeni bir gösterime atlar; kesintisiz doğal döngü değildir.

## İlk alıştırma

50. karede `03 - Volumetric dust` koleksiyonunu gizle/göster; sonra `04 - Sparks and debris`
katmanını aynı şekilde dene. Patlamanın gövdesi ile enerjisini taşıyan parçacıkları ayırt et.
Ardından bir kıvılcım seçip Timeline/Graph Editor'da konum anahtarlarını incele.

Bu dosya Blender öğrenme örneğidir; Unity prefab'ı veya performansı optimize edilmiş oyun VFX'i değildir.
Tüm geometri özgün olarak üretildi; haricî model/doku/ses/eklenti kullanılmadı.
Üretim betiği: `Tools/Art/ComicBlackHole/build_comic_meteor.py`.
