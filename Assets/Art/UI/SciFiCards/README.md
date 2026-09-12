# Sci-fi mutation cards

Aseprite ile çizilmiş, ReusablePopup ile aynı açık metal çerçeve dilini kullanan kart paketi.

## Projedeki karşılığı

`CardSelectionUI` üç kart slotunu doldurur. `TileModifierSO` altı tür (Fertile, Water, Crystal, Energy, Explosive, Duplicate) ve dört nadirlik (Common, Rare, Epic, Legendary) içerir. `CardUI` şu anda isim ve nadirlik metinlerini bağlar. Paket bu yapıya göre hazırlanmıştır; mevcut prefab, sahne veya oyun kodu değiştirilmemiştir.

## Dosyalar

- `Card_Common/Rare/Epic/Legendary.png`: 112 × 160 px, yazısız ve ikonsuz hazır kart gövdeleri.
- Aynı isimli `.aseprite`: Shadow, Background, Frame, Layout, Rarity ve başlangıçta gizli Hover / Selected katmanları.
- `Card_Frame.png`: içi şeffaf, ortak metal çerçeve.
- `Card_Background.png`, `Card_Layout.png`, `Card_Shadow.png`: ayrı zemin, içerik bölmeleri ve gölge.
- `Accent_*.png`: nadirlik renkleri ve 1–4 noktalı işaretler.
- `Card_Hover.png`, `Card_Selected.png`: kart üzerine bindirilen durum görselleri. Selected sağ altta onay işareti içerir.
- `Icon_*.png`: altı ayrı 24 × 24 px mutasyon ikonu.
- `Mutation_Icons.aseprite`: her ikon ayrı katmanda; görüntülemek için ilgili katmanı aç.

Nadirlik ile tür birbirinden bağımsızdır: altı ikonun her biri dört kart gövdesiyle de kullanılabilir. Önizleme örnek kombinasyonları gösterir; yazılar ve ikonlar kart PNG'lerine gömülmemiştir.

## Yerleşim

Koordinatlar 112 × 160 kaynak tuvalde, sol üst başlangıçlıdır:

| Alan | X | Y | Genişlik | Yükseklik |
| --- | --- | --- | --- | --- |
| İsim | 22 | 14 | 75 | 13 |
| İkon alanı | 14 | 37 | 84 | 60 |
| 2× ikon önerisi | 32 | 43 | 48 | 48 |
| Açıklama | 16 | 111 | 80 | 17 |
| Nadirlik metni | 40 | 137 | 47 | 8 |

## Unity

PNG'ler Sprite / Single, Point filtre, sıkıştırmasız ve mipmap kapalı olarak hazırdır. Hazır kart gövdeleri için Image Type Simple, Preserve Aspect açık; örneğin 224 × 320 veya 336 × 480 kullan. İç bölmeleri olan birleşik kartları 9-slice ile esnetme.

Yalnızca ortak Frame, Background, Shadow ve Hover için 10 px 9-slice sınırları bulunur. Esnek bir kart gerektiğinde bunları Sliced kullanıp Layout, metinler, nadirlik ve ikonu ayrı UI elemanlarıyla yerleştir. Selected işareti ve birleşik düzen sabit oran için tasarlanmıştır.

Tek kart gövdesi kullanırken ayrıca Frame/Background/Accent eklemeye gerek yoktur. Modüler kullanımda sıralama Shadow → Background → Frame → Layout → Accent → metin/ikon → durum katmanı şeklindedir. Dekoratif Image nesnelerinde Raycast Target kapalı tutulmalıdır.

Kart sistemi görsel olarak otomatik bağlanmamıştır. İkon veya açıklama gösterimi için CardUI/prefab içinde ayrı alanlar eklenmesi gerekir. Önizlemedeki açıklama alanı yerleşim örneğidir; oyun etkisi veya sayısal değer uydurulmamıştır.

Kaynak betik: `ArtSource/SciFiCards/create_cards.lua`. Önizleme: `ArtSource/SciFiCards/Cards_Preview.png`.
