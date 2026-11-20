# ERIGONE — Steam kapsülleri v2

13 Kasım 2026. Kullanıcı incelemesi için taslak; Steam'e yüklenmedi, Unity'ye bağlanmadı.

## Teslim

- `PNG/small_462x174.png`: kullanıcının sevdiği orijinal çizim dilindeki küçük kapsül; önceki yazısız kaynak aynen korundu.
- `PNG/header_920x430.png`: yakın Eri ve hasat kompozisyonu.
- `PNG/main_1232x706.png`: tarla, ekim yatakları ve hasat zinciri.
- `PNG/vertical_748x896.png`: dikey Eri/gezegen/ürün posteri.
- `Review.png`: dört tasarım ve küçük kapsülün 184×69 / 120×45 gösterimleri.
- `source/`: yazısız kaynak çizimler, üreticiden gelen çözünürlükleri korunmuştur.
- `LogoLayers/`: her kapsülle aynı ölçüde saydam PNG logo katmanı.
- `design.json`: tam üretim istemleri, kaynaklar ve logo yerleşimleri.
- `manifest.json`: dört kesin teslim ölçüsü ve SHA-256 değerleri.

## Görsel kararlar

Lacivert, kırmızı-turuncu, krem, petrol mavisi ve zeytin yeşili ortak palettir. Eri'nin yüzünde yalnız iki oval göz vardır; ben ve ağız yoktur. Küçük kapsülün çizimi korunmuştur; diğer üç çizim hasat ve tarla temasını vurgular.

Yazı gerçek proje fontu Bungee Regular ile dizilmiştir. E ve G'nin vektör konturlarına özel çatlaklar, kenar kopmaları ve kopmuş parçalar eklenmiştir. Font yazılımı değiştirilmemiştir. Diğer harfler temiz tutularak okunurluk korunmuştur.

Çizimler yerleşik image_gen ile kullanıcının Eri görselinden türetilmiştir. Başka oyunların görselleri üreticiye verilmemiş ve kapsüllere kopyalanmamıştır. Araştırmanın kaynakları ve sınırları `Design-Review.md` dosyasındadır.

## Yeniden üretim

Bu klasör projenin `output/` altında iken PowerShell 7 üzerinden `compose.ps1`, ardından `review.ps1` çalıştırılır. Mevcut Windows System.Drawing ve projedeki `Assets/Art/UI/ComicToon/Fonts/Bungee-Regular.ttf` kullanılır; yeni bağımlılık kurulmaz. Bungee SIL OFL 1.1 kapsamındadır; ticari kullanımda ücret/royalty ve ekran/jenerik kredisi zorunluluğu yoktur. Yalnız PNG dağıtımında font lisansını paketleme zorunluluğu yoktur. Font dosyası bu pakete dahil edilmemiştir.

## Kontrol

Dört PNG'nin istenen boyutları doğrulandı. Önizleme dosyası açılıp incelendi. Küçük kapsülün iki küçültülmüş görünümü, E/G kırıkları, başlığın kenarlara taşmaması ve Eri yüzündeki iki göz kontrol edildi. Tasarım onay bekliyor; mağaza performansı test edilmedi.
