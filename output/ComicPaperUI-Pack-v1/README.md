# Comic Paper UI Pack v1

Bu paket oyuna elle yerleştirilecek ayrı PNG sprite'lardan oluşur. Oyun sahnesi, prefab, kod ve import ayarları değiştirilmedi.

## İçerik

- Buttons: yeşil, kırmızı, mor ve altın butonlar; normal, hover, pressed.
- Slots: mavi, beyaz ve altın kareler; normal, hover, pressed. Ayrıca kömür grisi grid zemini.
- Panels: yatay/dikey krem paneller, büyük lacivert pencere, dar yan panel, küçük bilgi kutusu, genel tooltip ve dört renkte skill bilgi paneli.
- HUD: başlık şeridi, kaynak sayacı zemini, ince etiket ve mavi kenarlı krem sekme (normal, hover, pressed).
- Parts: boş bar zemini ve yatay ayırıcı.

## Hangi parça nerede kullanılır?

- Harf/sayı koordinatları: Slots/square-blue-normal.png; vurgu için hover veya square-gold-normal.png.
- Açık hücre: square-white-normal.png. Kapalı hücre: grid-square-charcoal.png.
- Skill düğümü: blue/gold kare setleri. İkon ve seviye yazıları sonradan yerleştirilir.
- Skill açıklaması: Panels/skill-tooltip-*-right.png. Panel imlecin SOLUNDA açılacak şekilde sağa bakan bir uç taşır. Paket yalnız görseldir; konumlandırmayı Unity içinde kur.
- Kaynak değerleri: HUD/resource-counter-navy.png. Altın/demir/taş ikonları ve sayılar sonradan yerleştirilir.
- İstatistik kutusu: Panels/info-small-cream.png.
- Sayfa üstü başlık: HUD/header-strip-navy.png.
- Bar: Parts/bar-track-empty.png yalnız zemindir; doluluk ayrıca kurulmalıdır.

## Önizleme

Preview.html dosyasını tarayıcıda aç. Durum menüsü veya fareyle normal/hover/pressed hâllerini karşılaştır. Açık/koyu/damalı zemin seçenekleri alfa kenarlarını kontrol etmeyi sağlar. Görseller internet bağlantısı olmadan yerel dosyalardan yüklenir.

## Görseller

Tüm PNG'ler yazısız ve ikonsuzdur. İç yüzeyleri dolu renktir; dış arka planları alfa şeffaflığı taşır. Hatalı, ortası silinmiş taslaklar pakete alınmamıştır. Kullanıcının küçük mavi kareyi koruma talebine uygun olarak o tasarım yeniden değiştirilmemiştir.

## Çözünürlük

Bunlar doğal 4K çıktılar değildir. Yerleşik araçtan 4K istenmesine rağmen daha düşük ölçüler dönmüştür; büyütülmüş dosyalar 4K üretim gibi sunulmamıştır. Gerçek piksel ölçüleri manifest.json dosyasındadır. Kareler yaklaşık 1254×1254; butonlar yaklaşık 2172×724 pikseldir. Durumlar AI ile ayrı üretilmiştir; ince kenarlarda piksel düzeyinde küçük farklar olabilir. Altın butonun normal hâli 2171, diğer hâlleri 2172 piksel genişliğindedir.

Kenarları kırpmadan aynı grup için aynı yerleşim boyutunu kullanmak, geçişlerde görsel hizayı korumaya yardımcı olur. Paket otomatik 9-slice verisi veya Unity prefab'ı içermez.

## Kaynak

Yerleşik image_gen ile, kullanıcının onayladığı ilk comic UI tasarımından türetildi. Model sürümü araçta gösterilmedi. Yeni harici font, ikon paketi veya kütüphane indirilmedi. Üretim ve düzeltme talimatları prompts.json içindedir. Bu not yeni bir üçüncü taraf lisansı beyanı değildir.

Manifest, merkezde saydam delik olup olmadığını ölçen alfa örneklemesini ve dosya boyutlarını içerir. Unity içinde çalışma veya görsel yerleşim testi yapılmadı.

