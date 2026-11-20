# Küçük kapsül — zemin ve comic başlık

13 Kasım 2026. Kullanıcı inceleme sürümü.

- `PNG/small_462x174.png`: Steam küçük kapsül ölçüsü.
- `PNG/small_high_1848x696.png`: aynı kompozisyonun dört kat boyutlu çıktısı; yazı gerçek font konturlarından bu çözünürlükte çizildi.
- `LogoLayers/`: eşleşen saydam logo katmanları.
- `source/`: yazısız üretim kaynağı.
- `design.json`: yerleşim ve yerleşik image_gen için kullanılan tam istem.

Eri'nin ilk referanstaki eğik pozu, bakış yönü ve eğimli kaya zemini esas alındı; iki ayak görünür. Yüzünde iki göz var, ben ve ağız yok. Başlık, ComicUITheme headingFont ailesi Lilita One ile dizildi; E/G kırıkları ve krem-turuncu palet korundu.

Font mevcut proje varlığıdır; SIL OFL 1.1. Ücret/royalty ve ekran/jenerik kredisi zorunluluğu yoktur. PNG tesliminde font lisansı dağıtma zorunluluğu yoktur. Font dosyası dağıtılacaksa telif/OFL metni korunur. Font binary dosyası bu klasörde bulunmaz. Yeni indirme/bağımlılık yoktur.

`compose.ps1` bu klasör projenin output dizinindeyken PowerShell 7 ve mevcut System.Drawing ile çıktıları yeniden oluşturur. Görseller açılıp incelendi ve kesin PNG boyutları doğrulandı. Steam'e yüklenmedi, oyuna eklenmedi.
