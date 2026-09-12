# Reusable pixel popup

Aseprite ile üretilmiş 160 × 112 px popup. Başlık, ikon ve düğme içermez.

- `Popup.aseprite`: düzenlenebilir Shadow, Background ve Frame katmanları.
- `Popup_Frame.png`: içi ve dışı şeffaf çerçeve.
- `Popup_Background.png`: hafif iç gölgeli açık gri zemin.
- `Popup_Shadow.png`: şeffaf dış gölge.
- `Popup_Combined.png`: tüm katmanların birleşimi.

## Unity kullanımı

Tek Image için Combined sprite'ını kullan. Ayrı renk kontrolü için aynı RectTransform ölçülerinde sırasıyla Shadow, Background ve Frame Image nesneleri oluştur. Üçü de aynı tuval ölçüsüne sahip olduğu için hizaları eşleşir.

Image Type: Sliced. Sprite sınırları Left 12, Bottom 14, Right 12, Top 12 piksel olarak meta dosyalarında hazırdır. Filter Mode Point, Compression None, mipmap kapalıdır. Frame için Fill Center kapatılabilir; Background ve Combined için açık tut.

Keskin pikseller için tam sayı ölçek kullan (ör. 2× veya 4×). Boyut değişimini RectTransform ve Sliced ile yap. İçeriği kenarlardan en az 12 kaynak piksel içeride tut. Renk değiştirmek için Background Image rengini veya Aseprite katmanını düzenle.

Kaynak çizim betiği: `ArtSource/ReusablePopup/create_popup.lua`. Önizleme: `ArtSource/ReusablePopup/Popup_Preview_4x.png` (4× nearest-neighbor; dama zemin yalnızca önizlemededir).

Mevcut sahnelere otomatik bağlanmamıştır.
