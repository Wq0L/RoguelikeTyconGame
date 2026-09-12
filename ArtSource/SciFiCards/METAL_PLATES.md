# Metal plaka kart serisi

Onaylanan Common tasarımının devamı. Kartlar Aseprite ile sırayla üretildi ve her önizleme ayrı kontrol edildi.

| Nadirlik | Gövde karakteri | Konum |
| --- | --- | --- |
| Common | İki sol menteşe, sade servis plakası | Assets/Art/UI/SciFiCards/CommonPlate |
| Rare | Çift kısa yan modül, yükseltilmiş üst ray, turkuaz kanallar | Assets/Art/UI/SciFiCards/RarePlate |
| Epic | Uzun çift hücreli yan modüller, alt korumalar, çapraz köşe destekleri | Assets/Art/UI/SciFiCards/EpicPlate |
| Legendary | Kademeli üst plaka, merkez arma, alt kilit, sınırlı altın kakma | Assets/Art/UI/SciFiCards/LegendaryPlate |

Her klasörde Rarity_Plate.aseprite, Rarity_Plate.png, Rarity_Frame.png, Rarity_Background.png ve Rarity_Shadow.png bulunur (Rarity yerine nadirlik adı).

128 × 180 px ortak tuval. Çerçeve merkezi şeffaftır. Birleşik PNG zemin ve gölgeyi de içerir. Kaynak dosyalarda zemin, gölge, metal gövde ve işlenmiş detaylar ayrı katmanlardır. Yazı veya ikon gömülmemiştir.

Unity: Image Type Simple, Preserve Aspect açık, Point filtreleme, Compression None, mipmap kapalı. PNG meta dosyaları hazırdır. 256 × 360 / 384 × 540 gibi tam sayı ölçek kullan. Siluetlerdeki modülleri korumak için bu kartları 9-slice ile esnetme.

Ana içerik güvenli alanı x23–104, y36–144; üst kısa etiket alanı x29–98, y15–20. Koordinatlar sol üst başlangıçlı kaynak piksellerdir. Uzun isimler ana içerik alanına konabilir.

Eski Card_Common/Rare/Epic/Legendary dosyaları ilk tasarım denemesidir. Yeni seri için *Plate klasörlerini kullan. Mevcut sahne ve prefab bağlantıları değiştirilmemiştir.

Üretim: create_common_plate.lua ve create_rarity_plate.lua. İkinci betik rarity, output ve preview parametrelerini alır. Her kart ayrı Aseprite çağrısında üretilir.
