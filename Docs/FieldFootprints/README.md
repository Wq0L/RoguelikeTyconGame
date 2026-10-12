# Etkilenen hücrelerin modellenmesi — 8 Ekim 2026

Diken Kuşatması ve İstilacı Filiz için hücre merkezine yerleşen, saksının orta kısmını açık bırakan
özgün düşük poligonlu çevre parçaları üretildi. Diken: iki kıvrımlı odunsu kök ve altı açık renk kanca.
Filiz: iki turkuaz sürgün, altı alçak yaprak ve mevcut iki yapraklı küçük filiz. Ana hedef modelleri,
kırmızı konturları, darbe efektleri ve Aşılı Kabuk korunur.

Filiz'de ana hedef hücresi dahil bütün yavaşlayan hücreler bir çevre parçası taşır. Bu nedenle sekiz
etkilenen hücrede artık sekiz parça bulunur; ana hedef hâlâ tek müdahale noktasıdır. Mekanikte Filiz
yavaşlatır, Diken durdurur. Hiçbir sayı veya süre değiştirilmedi. Parçalar hücre genişliğine ölçeklenir,
mevcut havuzdan alınır; solma kuyruğu yavaşlamanın kaldırılmasını geciktirmez.

## Bütçe

| Hücre modeli | Kontursuz vertex | Kontursuz üçgen | Konturlarla üçgen | Renderer |
|---|---:|---:|---:|---:|
| Diken kök çevresi | 852 | 284 | 568 | 2 |
| Filiz sürgün çevresi | 720 | 240 | 532 | 11 |

Düz gölgelendirme vertex tekrarları sayılmıştır. Diken 9 hücre için 5112, Filiz 8 hücre için 4256
kontur dahil üçgen; ana hedefler, toprak ve geçici VFX ayrıca çizilir. Filiz'in mevcut hareketli
küçük yaprakları ve konturları renderer sayısına dahildir. Yeni ışık, fizik, Update veya coroutine yok.

## Üretim ve koruma

Tools/Verification/Editor/FieldFootprintBuilder.cs yalnız izole proje üzerinden çalıştırılır.
`FieldFootprintBuilder.BuildThornAndRun` varlıkları üretir ve Diken testini başlatır. Sonuç klasörü
Assets/Art/LateGame/FieldFootprints ve iki slot bağının bulunduğu LateGameArt.asset ana projeye aktarılır.
Eski Thorn/Sprout builder'ları yeniden çalıştırılırsa alan bağlarını tekrar kurmak için bu builder son çalıştırılmalıdır.
Mevcut küçük Filiz prefabi türetilir; ana hedef prefabları yeniden oluşturulmaz.

121 dosyalık koruma karşılaştırması aynı: eski onaylı Diken/Filiz varlıkları, mekanikler, sahneler,
seçili profil ve gerçek kayıtlar değişmedi (Protected.json). Yeni alan modelleri ayrı klasördedir.
Dış varlık/paket/araç indirilmedi; yeni ücret, kredi veya üçüncü taraf dağıtım bildirimi gerekmiyor.

## Doğrulama

GameScene'nin 121 bitkili izole laboratuvarı, normal oyun kamerası, MemoryOnly kayıt modu.
Diken: 41; Filiz: 37 kontrol geçti. Gerçek vuruşlar, yayılma sınırı, mekanik etkilerin zamanında
kaldırılması, pause, görsel solma kuyruğu, round temizliği ve havuzda yeniden kullanım test edildi.
Filiz kontrolleri yeni sekiz çevre parçasına göre güncellendi; mekanik beklentiler aynı kaldı.

İnsan Play'de: yoğun saldırı efektleri varken etkilenen hücreleri sayabilme, ana hedefi küçük
parçalardan ayırma ve yüksek bitkilerin kök çevresini örtme durumuna bakılmalı. Ekran görüntüsü
incelemesi insan Play onayı veya cihaz üzerinde performans ölçümü değildir.
Commit yok. Gezgin Bereket kapsam dışı.

Genel geç oyun regresyonu: 97 kontrol geçti, exit 0. Bu revizyonda toplam 175 kontrol.
Derleyici/shader hatası yok. Editörün mevcut SearchDatabase başlangıç indeksleme istisnası logda görülebilir.
