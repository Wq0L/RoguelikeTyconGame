# Dış kaynak ve lisans kayıtları

## Blender 4.5.15 LTS — ayrı açılış animasyonu demosu, 27 Ekim 2026

- Kategori: yalnız geliştirme/önizleme aracı. Oyun asset'i, shader'ı veya çalışma zamanı bağımlılığı eklenmedi.
- Kaynak: https://download.blender.org/release/Blender4.5/blender-4.5.15-windows-x64.zip
- Resmî SHA-256 dosyası: https://download.blender.org/release/Blender4.5/blender-4.5.15.sha256
  İndirilen arşiv doğrulandı: `52dd02fd74877fa08e74b67d7854cf32468a232c02aaa19b6844c91285574f8d`.
- Taşınabilir araç ve demo proje deposu dışında, Codex önizleme klasöründeki `intro-25d/` altında. Sistem PATH'i değiştirilmedi.
- Sürüm lisans belgesi: https://raw.githubusercontent.com/blender/blender/v4.5.15/release/license/license.md
  ve çıktı koşulları: https://www.blender.org/about/license/ . Lisans dosyasının kopyası önizleme araç klasöründe korundu.
- Blender ikili dağıtımı GPL-3.0-or-later; bileşen lisansları resmî paket içinde korunur. Kullanım ve ticari çıktı üretimi ücretsiz; royalty yok.
- Üretilen özgün video ve .blend veri dosyaları nedeniyle oyun ekranı/jenerik kredisi veya oyun paketine Blender lisans/telif metni ekleme zorunluluğu yok. Blender yazılımı oyunla dağıtılmıyor; dağıtılırsa GPL bildirim ve karşılık gelen kaynak sağlama koşulları ayrıca uygulanır.
- `bpy` kullanan üretici betik GPL-3.0-or-later işaretli; bu betik oyun kodu değildir. Render çıktıları Blender yazılımından türemiş kod olarak sunulmaz.
- Kullanıcıya araç, kaynak, lisans, ücret/royalty, kredi ve dağıtım koşulları indirmeden/kullanmadan önce konuşmada bildirildi.
- Roket/gezegen/kaya geometrileri ve efektler bu demo için kodla oluşturuldu; üçüncü taraf model, doku, font veya müzik indirilmedi. Kullanıcının banner'ı ve storyboard'u yalnız yön referansı. Bu kayıt nihai sanatsal kalite veya tüm haklar için garanti değildir.
- MP4 kodlama: mevcut PATH kurulumu FFmpeg 9.0.2 full_build (Gyan), `--enable-gpl --enable-version3`. Yerel paket `LICENSE` (GNU GPL v3) ve derleme yapılandırması kontrol edildi. Yeni FFmpeg indirmesi yapılmadı. Yalnız üretim aracı; araç için ücret/royalty, video içinde kredi veya yalnız çıktı nedeniyle oyun paketinde GPL metni gerekmiyor. FFmpeg yazılımı oyunla dağıtılmıyor; dağıtılırsa bildirim/kaynak yükümlülükleri yeniden ele alınmalı. Bu kayıt codec patentleri için genel bir lisans garantisi değildir. Kullanım öncesi konuşmada bildirildi.
- Wing It! (Blender Studio) ve Pixar kamera/ışık üretim yazıları yalnız araştırma referansıdır. Filmlerden görüntü, model, ses veya kod alınmadı. Lisanslı film varlığı kullanıldığı iddia edilmez.

## DejaVu Sans - AI sistem rehberi PDF'si (10 Ekim 2026)

- Kullanım yalnız `output/pdf/Oyun-Sistem-Rehberi-20261010.pdf` belgesine Türkçe font gömme içindir; oyun asset'i veya runtime bağımlılığı eklenmedi.
- Kaynak: yerel Codex çalışma ortamının Poppler paketindeki `Library/share/fonts/DejaVuSans.ttf`; internetten yeni indirme yapılmadı.
- Kontrol edilen belge: kullanılan TTF'nin gömülü `name` tablosundaki telif, lisans ve lisans URL alanları (0, 13, 14). DejaVu değişiklikleri public domain; kaynak glifler Bitstream Vera ve Arev izin koşulları altındadır.
- Telif: Bitstream, Inc. (2003), Tavmjong Bah (2006). Lisanslar sürüm numarası olmayan Bitstream Vera / Arev font izin metinleridir; OFL olarak etiketlenmedi.
- Ticari kullanım, değiştirme, gömme ve dağıtım ücretsiz; lisans ücreti veya royalty yok. Oyun ekranında ya da jenerikte kredi zorunluluğu yok.
- Fontu dağıtırken telif ve izin metinleri korunur; font tek başına satılamaz. Değiştirilirse lisansın saklı ad koşulları uygulanır. Bu belgede font değiştirilmedi; PDF'ye alt küme gömüldü. Oyun kodunu kaynak olarak paylaşma şartı yok.
- Kullanıcıya kaynak ve koşullar üretim öncesinde konuşmada bildirildi. İzin metni PDF ekinde ve `output/pdf/DejaVuSans-LICENSE.txt` içinde korunur.
- Kategori: belge üretimi / teslim edilen PDF; oyunda dağıtılan yeni içerik değildir.

## Harvest Panel TR / Bangers ve Harvest CJK Support / Noto Sans SC

- Bangers: projedeki TMP örnek fontu. Vernon Adams, 2010; saklı adı Bangers.
  Türkçe harfler ve lira işareti eklenerek Harvest Panel TR adıyla türetildi.
- Noto Sans SC: Google Fonts resmî deposu `ofl/notosanssc`; Adobe 2014–2021,
  saklı adı Source. 800 ağırlığı sabitlenerek Harvest CJK Support adıyla türetildi.
  Kaynak ve lisans: <https://github.com/google/fonts/tree/main/ofl/notosanssc>.
- İkisi de SIL OFL 1.1: ücretsiz ticari kullanım, lisans ücreti/royalty yok.
  Oyun ekranında veya jenerikte teşekkür şartı yok; dağıtımda telif ve OFL metni
  korunmalı, türev fontlar OFL altında kalmalıdır. Font tek başına satılamaz.
- Kullanıcıya kaynak, lisans ve koşullar 7 Ekim 2026'da kullanımdan önce bildirildi.
- Kaynak hash'i ve karakter kontrolleri: `Docs/UI-Button-Preview/coverage.json`.
- Kullanım şu anda onay önizlemesi; tüm oyun yerelleştirilmiş değildir.
- İlgili NOTICE/OFL dosyaları `Assets/Art/UI/ComicToon/Fonts` altında.

Bu kayıt yeni incelenen içerikleri kapsar; tüm mevcut deponun lisans denetimi değildir.
Kullanıcıya önceden bildirim zorunluluğu için kök `AGENTS.md` geçerlidir.

## Orbit Ink TR / Harvest Comic TR / Lilita One

- Kullanım: onay için hazırlanan comic buton önizlemesindeki font.
- Kaynak: projede zaten bulunan `HarvestComicTR-Regular.ttf`; onun kaynağı Lilita One.
  Yeni bir font internetten indirilmedi. Orbit Ink TR proje için verilen türev ismidir.
- Özgün telif: Copyright (c) 2011 Juan Montoreano; saklı font adı: Lilita.
- Lisans: SIL Open Font License 1.1.
- Kontrol: `Assets/Art/UI/ComicToon/Fonts/HarvestComicTR-OFL.txt` ve
  <https://openfontlicense.org/open-font-license-official-text/>.
- Koşullar: telif ve OFL bildirimi korunur; türev font OFL altında kalır; saklı
  font adı izinsiz kullanılmaz; font tek başına satılamaz. Oyun kodu için OFL
  kaynak paylaşımı şartı doğmaz.
- Türev belgeleri: aynı klasörde `OrbitInkTR-OFL.txt` ve `OrbitInkTR-NOTICE.txt`.
- Kullanıcıya kaynak ve koşullar 7 Ekim 2026 konuşmasında açıklandı.

## fontTools 4.65.0

- Kaynak: PyPI `fonttools` paketi, <https://pypi.org/project/fonttools/4.65.0/>.
- Kullanım: yalnızca yerel font oluşturma/doğrulama aracı; oyun çalışma zamanı
  bağımlılığı değildir. `Tools/.font-deps/` Git dışında tutulur.
- Lisans: MIT; kurulu dağıtımın METADATA ve LICENSE dosyaları kontrol edildi.
  LICENSE.external ayrıca test fontlarının OFL bildirimlerini içerir; bu test
  fontları oyun fontuna dahil edilmedi.
- Dağıtılacaksa telif ve izin bildirimi korunmalıdır.
- Bu paket önceki çalışma sırasında kurulmuştur; yeni ön-bildirim kuralından
  önceki bu kurulum için kullanıcıya geriye dönük bilgi verilmiştir.

## Kullanıcının sağladığı comic buton ve kota paneli görselleri — 7 Ekim 2026

- Kaynak: kullanıcı bu konuşmada dört yerel PNG dosyası sağladı ve oyuna
  uygulanmasını istedi. Bu adımda internetten yeni içerik indirilmedi.
- Orijinaller piksel değişikliği olmadan `Assets/Resources/ComicArtwork/` içine
  kopyalandı; kaynak ve hedef SHA-256 eşitliği kontrol edildi.
- GoldButton: `exec-4b5148ad-0eec-4266-abf8-233461724c9b.png` → Skill Shop.
- CyanButton: `exec-8997b508-3d98-4296-ac6e-c609b60b7cd1.png` → Başlat / Önizleme.
- GreenButton: `exec-4b403f24-36c4-42f9-86f8-5d7a322262c7.png` → Planter Shop.
- QuotaPanel: `exec-0a851caf-8d59-45dc-854a-426553418ef5.png` → sol hazırlık/round özeti.
- Kaynak klasörü: kullanıcının `.codex/generated_images/01a0fe64-3f65-7db0-b7a5-f931e5c35125/`.
- Bunlar üçüncü taraf bir siteden lisanslanmış içerik olarak sunulmaz;
  kullanıcı tarafından sağlanan görsellerdir. Bu kayıtta ayrıca bir üçüncü taraf
  lisansı, ücret veya kredi koşulu doğrulandığı iddia edilmez.

## Reference coordinate cuts v2 — 7 Ekim 2026

- Kaynak: bu konuşmada Imagegen ile üretilen ve kullanıcının seçtiği
  `exec-2814e3d5-1750-4bcb-861d-1a58ebcc3954.png` ekran taslağı.
- 22 UI doğrudan kesimi; dünya için 12 kesim ve 10 örtülü etiket uyarlaması.
  PNG'ler kullanıcının onayladığı v2 paketinden değiştirilmeden kopyalandı,
  SHA-256 eşitliği doğrulandı. Maskeler ve kaynak türleri `sources.json` içinde.
- Yeni üçüncü taraf içerik, font veya araç indirilmedi. Bu kayıt bir üçüncü taraf
  lisansı varmış gibi sunulmaz; kullanıcı tarafından uygulanması istenen, bu
  konuşmada üretilmiş görsellerin kaynak kaydıdır. Ayrı lisans adı/sürümü yoktur.
- Bu entegrasyon kapsamında ücretli varlık lisansı veya satış başına royalty
  satın alınmadı. Üçüncü taraf ekran/jenerik kredisi veya dağıtılacak üçüncü taraf
  lisans/telif metni eklenmedi; bu kayıt AI çıktıları için hukuki garanti değildir.
- Oyunda dağıtılan PNG'ler: `Assets/Resources/CoordinateReference/`.
  Altın hover kopyası çalışma zamanında yalnız mavi pikseller yeniden renklendirilerek
  oluşturulur; beyaz yazı, koyu mürekkep ve alfa korunur.

## Diken Kuşatması — özgün yerel içerik, 7 Ekim 2026

- Meshler, toon shader, prefablar ve beş sentez ses bu görevde proje için özgün kodla üretildi.
  İnternetten model, VFX, ses örneği, paket veya araç alınmadı; mevcut üçüncü taraf modelden türev yapılmadı.
- Oyun varlıkları: `Assets/Art/LateGame/ThornSiege/`, shader: `Assets/Resources/ThornComicToon.shader`.
- Üretim araçları: mevcut Unity 6000.3.14f1 editörü ve mevcut Python standart kütüphanesi;
  yeni araç/bağımlılık kurulmadı. Üretici kodu `Assets/Editor/ThornArtBuilder.cs`, `Tools/Art/build-thorn-audio.py`.
- Bu özgün varlıklar için harici asset lisansı, üçüncü taraf royalty/ücret, ekran/jenerik kredisi veya
  dağıtımda ayrıca asset lisans metni gerektiren bir kaynak kullanılmadı. Mevcut Unity/URP şartları değiştirilmez.
- Bu kayıt mevcut projenin/Unity lisansının tamamının denetlendiği anlamına gelmez.

## İstilacı Filiz ve olay hedef konturu — özgün yerel içerik, 8 Ekim 2026

- Filiz gövdesi, dört yapraklı ana taç, iki yapraklı yayılma parçaları, darbe/kırılma parçaları ve dört ses,
  bu görevde özgün geometrik üretim ve standart kütüphane ses senteziyle hazırlandı. İnternetten içerik alınmadı.
- Mevcut özgün Diken toprağı, toprak patlaması, toon/ink materyalleri, hazırlık ve kırılma sesleri yeniden kullanılır.
  Yeni kırmızı kontur materyalleri mevcut meshleri paylaşır; dış içerikten türev değildir.
- Dağıtılan varlıklar: `Assets/Art/LateGame/InvasiveSprout/`, `Assets/Art/LateGame/Attention/`.
- Geliştirme kodu: `Assets/Editor/SproutArtBuilder.cs`, `LateGameMeshBuilder.cs`, `LateGameAttentionBuilder.cs`,
  `Tools/Art/build-sprout-audio.py`. Mevcut Unity/Python kullanıldı; Blender veya yeni bağımlılık kurulmadı.
- Bu yeni özgün varlıklar için harici lisans adı/sürümü, ücret/royalty, ekran/jenerik kredi zorunluluğu veya
  dağıtımda üçüncü taraf lisans/telif bildirimi gerektiren kaynak yoktur. Mevcut Unity/URP şartları aynen sürer.


## Aşılı Kabuk — özgün yerel üretim, 8 Ekim 2026

- Kaynak: bu görevde yazılan ShellArtBuilder ve build-shell-audio.py; internetten model, örnek ses, ikon,
  paket veya araç alınmadı. Kabuk düğümleri, menteşeli paneller, üçgen/çift baklava motifleri özgün geometridir.
- Diken'in daha önce özgün üretilmiş toon/ink/kırmızı kontur materyalleri, toprak patlaması, darbe/kırılma
  meshleri ve darbe/kırılma sesleri ortak kullanılır. Bunlar dış içerik türevi değildir.
- Oyunda dağıtılan yeni varlıklar: Assets/Art/LateGame/GraftedShell/. Dört özgün mono ses: Prepare, Choice, Close, Open.
- Geliştirme araçları: mevcut Unity 6000.3.14f1 ve mevcut Python standart kütüphanesi. Yeni bağımlılık kurulmadı.
  Üretici kaynakları Assets/Editor/ShellArtBuilder.cs ve Tools/Art/build-shell-audio.py.
- Harici asset lisansı/sürümü yok; bu varlıklarda üçüncü taraf ücret/royalty, oyun ekranında/jenerikte
  kredi veya dağıtımda ayrıca lisans/telif metni gerektiren kaynak kullanılmadı. Mevcut Unity/URP koşulları korunur.

### Aşılı Kabuk — sıcak tomurcuk / buz kozası revizyonu (2026-10-08)

Kullanıcının konuşmada verdiği tasarım görseli genel sanat yönü referansıdır; PNG oyuna
alınmadı, dokusu/mesh'i çıkarılmadı. Sıcak kabuk tomurcuğu, soğuk kristal koza, kıvrımlı
sarmaşık, yaprak/dal/puf geometrileri proje içindeki ShellArtBuilder C# koduyla oluşturuldu.
Yeni dış varlık, paket veya geliştirme aracı indirilmedi. Mevcut proje üretimi toon materyal,
havuz ve sentez sesleri kullanılır. Bu eklemeler nedeniyle üçüncü taraf ücret/royalty,
oyun içi/jenerik kredi veya dağıtımda yeni lisans/telif metni yükümlülüğü doğmadı.

### Diken / Filiz hücre çevreleri — 2026-10-08
Yeni FieldFootprints meshleri proje içindeki C# builder ile özgün üretildi. Küçük Filiz parçası
önceki proje üretimi SproutPiece üzerinden türetildi; mevcut özgün toon/ink materyalleri kullanılır.
Dış kaynak indirme veya yeni araç/paket kurulumu yok. Ek ücret/royalty, ekran/jenerik kredisi ya da
dağıtımda yeni üçüncü taraf lisans/telif metni yükümlülüğü yok. Ayrıntı: Docs/FieldFootprints/README.md.

## FFmpeg 9.0.2 essentials — jam videosu kurgu aracı, 8 Ekim 2026

- Yalnızca geliştirme aracı; oyunda dağıtılmaz ve depoya girmez. Kullanıcının isteğiyle proje
  dışında, `C:\Users\User\Desktop\JamVideo\tools\ffmpeg-9.0.2-essentials_build\` altında taşınabilir
  olarak durur; sistem PATH'ine eklenmedi.
- Kaynak: <https://www.gyan.dev/ffmpeg/builds/> (ffmpeg.org'un Windows için yönlendirdiği derleme),
  `ffmpeg-9.0.2-essentials_build.zip`, SHA-256
  `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba` yayınlanan değerle eşleşti.
- Lisans: GNU GPL v3 (`--enable-gpl --enable-version3`); kontrol edilen belge arşivdeki `LICENSE`
  ve <https://ffmpeg.org/legal.html>.
- Kullanım: oynanış kayıtlarını kesme, geçiş, yazı, ses karıştırma ve render. Lisans programı
  kapsar, üretilen videoyu kapsamaz; çıktı ticari kullanılabilir.
- Ücret/royalty yok. Videoda veya oyunda kredi zorunluluğu yok. Program dağıtılmadığı için
  dağıtım paketinde lisans/telif metni taşıma yükümlülüğü doğmaz; FFmpeg oyuna gömülürse GPL
  yükümlülükleri başlar, bu yapılmayacak.
- Kullanıcıya kaynak, lisans ve koşullar 8 Ekim 2026 konuşmasında indirmeden önce bildirildi.

## Jam videosu fon müziği — Wee Free Music, "Free Music Retro Sci-Fi Music", 10 Ekim 2026

- Kullanım: yalnızca jam başvuru videosunun fon müziği. Oyunda dağıtılmaz, depoya girmez;
  dosya `C:\Users\User\Desktop\JamVideo\muzik\` altında durur.
- Kaynak: <https://www.youtube.com/watch?v=I4Coe-ld68U>, kanal Wee Free Music, 156 sn.
  Dosyayı kullanıcı kendisi bir dönüştürme sitesi (notube.net) üzerinden MP3 olarak indirdi;
  192 kbps, SHA-256 `A3D272EC0B03D8A0EF7D00363047381AFC6E5AD896783B196A07E3370D681708`.
- Sayfadaki beyan: YouTube lisans alanı "Creative Commons Atıf" (CC BY 4.0,
  <https://creativecommons.org/licenses/by/4.0/>); açıklamada videolarda ve gelir getiren
  YouTube videolarında ücretsiz kullanılabileceği yazıyor.
- Doğrulanamayan: besteci, parça adı ve asıl kaynak belirtilmemiş; kanal açıklamasındaki
  "hak sahibiyseniz yazın, kaldıralım" notu kanalın hak sahibi olmayabileceğini düşündürüyor.
  Kontrol edilen belge yalnızca video sayfası ve YouTube'un Creative Commons yardım sayfasıdır.
  Lisans bu nedenle kanıtlanmış sayılmaz.
- Karar: Claude lisansı belirsiz bulup kullanmamayı önerdi; kullanıcı 10 Ekim 2026 konuşmasında
  kendisinin kontrol ettiğini belirterek kullanmaya karar verdi.
- Beyana göre ücret/royalty yok. CC BY gereği kredi zorunlu: video açıklamasında parça adı, kanal,
  bağlantı ve lisans yazılır. Dağıtımda ayrıca lisans metni taşınmaz; kredi satırındaki bağlantı yeter.
- Risk: gerçek hak sahibi ortaya çıkarsa videoya telif talebi gelebilir. Deep Jam başvuru
  kılavuzundaki "Telif Hakkı ve Özgünlük Beyanı" geçerli lisans beyanı ister; yanlış beyan
  diskalifiye sebebi sayılır. Bu parça için lisans kanıtı yoktur.

## Jam videosu yazı fontu — Harvest Comic TR, 10 Ekim 2026

- Videodaki başlık ve alt yazılar, projede zaten bulunan `HarvestComicTR-Regular.ttf` ile
  yazıldı (Lilita One türevi, Copyright (c) 2011 Juan Montoreano, SIL OFL 1.1; ayrıntı yukarıdaki
  "Orbit Ink TR / Harvest Comic TR / Lilita One" kaydında). İnternetten yeni font indirilmedi.
- Font dosyası OFL ve NOTICE metinleriyle birlikte `C:\Users\User\Desktop\JamVideo\fontlar\`
  altına kopyalandı; değiştirilmedi. Videoda yazı olarak kullanmak OFL kapsamında serbesttir:
  ücret/royalty yok, kredi zorunluluğu yok; font dosyası videoyla dağıtılmadığı için lisans metni
  taşıma yükümlülüğü doğmaz.

### Gezgin Bereket — 2026-10-08
Krem/şeftali polen çiçekleri, akış ve canlanma meshleri BountyArtBuilder ile proje içinde üretildi.
Arrive/Progress/Bloom sesleri build-bounty-audio.py ile Python standart kütüphanesinde özgün sentezlendi;
örnek ses alınmadı. Önceki özgün ThornToon/ThornInk materyalleri yeniden kullanılır.
Yeni dış içerik, bağımlılık veya geliştirme aracı indirilmedi. Ek ücret/royalty yok; ekran/jenerik kredi
ve dağıtımda yeni üçüncü taraf lisans/telif metni yükümlülüğü yok. Kaynaklar Docs/WanderingBounty/README.md.

### Gezgin Bereket pasif tarama revizyonu — 2026-10-08
BountyAuroraToon.shader ve tarama/aurora meshleri proje içinde özgün kodla üretildi; dış shader, doku,
model veya araç indirilmedi. Çiçeklerde önceki özgün geometri kullanılıyor. Yeni ücret/royalty, ekran/jenerik
kredisi veya dağıtımda üçüncü taraf lisans metni yükümlülüğü yok. Kaynak: Docs/WanderingBounty/README.md.

### Gezgin Bereket — altın perde / robot / geçici bitki rengi (2026-10-08)
Tek 3×3 altın perde, toplama robotu ve ışın geometrisi proje içindeki BountyArtBuilder ile üretildi.
BountyPlantGold.shader özgün ek renk geçişidir; mevcut bitki meshlerini değiştirmeden yeniden çizer.
Üçüncü taraf materyal/shader değiştirilmedi, yeni dış içerik veya araç alınmadı. Ek ücret/royalty,
oyun/jenerik kredisi veya yeni üçüncü taraf lisans metni dağıtım yükümlülüğü yok.

### Anten ailesi — 2026-10-08
- Model, taş/yaprak kaide, bobin/çatal başlık, elektrik parçaları, temas yıldızları ve yerleşim
  işaretleri proje içindeki özgün AntennaArtBuilder C# geometrisiyle üretildi. Kullanıcının
  paylaştığı görsel tasarım yönü olarak incelendi; görselin kendisi oyuna/dokuya eklenmedi.
- Settle/Snap/Line/Circuit sesleri Tools/Art/build-antenna-audio.py ile Python standart
  kütüphanesinde özgün sentezlendi; harici ses örneği kullanılmadı.
- Önceden bu proje için üretilen ThornToon/ThornInk ortak materyalleri ve mevcut havuzlar
  yeniden kullanılır. Yeni shader, paket, eklenti veya geliştirme aracı indirilmedi/kurulmadı.
- Yeni dış içerik lisansı yok; ek ücret/royalty yok; yeni ekran/jenerik kredisi veya dağıtım
  paketinde üçüncü taraf lisans/telif metni taşıma yükümlülüğü yok. Mevcut projenin önceki
  üçüncü taraf yükümlülükleri değişmez. Ayrıntı: Docs/AntennaFamily/README.md.


### Hilal ailesi — 2026-10-09
Cyan kesici yüzey, koyu kontur, kısa iz, sıcak ters hilal ve temas parçaları özgün
MoonArtBuilder geometrisidir. Kullanıcının görseli sanat yönü için incelendi; görselin
kendisi doku/oyun varlığı olarak alınmadı. Launch/Grow/Return sesleri build-moon-audio.py
ile Python standart kütüphanesinde özgün sentezlendi; dış ses örneği alınmadı.
Projenin özgün ThornToon/ThornInk materyalleri tekrar kullanıldı. Yeni harici içerik,
paket, shader veya geliştirme aracı kurulmadı. Ek ücret/royalty, ekran/jenerik kredisi
ve yeni üçüncü taraf lisans/telif metni dağıtım yükümlülüğü yok. Önceki üçüncü taraf
koşulları değişmez. Kaynaklar ve doğrulama: Docs/CrescentFamily/README.md.

### Kritik Ark — 2026-10-12
Proje için özgün üretilmiş AntennaSnap/ContactDouble ve ThornToon yeniden kullanıldı
(kaynak: AntennaArtBuilder ve yukarıdaki Anten ailesi kaydı). Yeni CritArcClick.wav,
Tools/Art/build-crit-arc-audio.py ile harici örnek olmadan özgün sentezlendi. Yeni dış
varlık/araç/paket yok. Ek ücret/royalty, ekran/jenerik kredi zorunluluğu veya yeni üçüncü
taraf lisans/telif metni dağıtma zorunluluğu yok. Önceki yükümlülükler değişmez.

### Taşan Hasar — 2026-10-12
Özgün OverflowShard, üç kırıntı ve OverflowFup.wav proje içinde üretildi; ses Python
standart kütüphanesiyle sentezdir, harici örnek yok. Önceden proje için üretilen
ContactDouble yıldız mesh’i ve ThornToon/ThornInk yeniden kullanıldı (Anten ailesi kaydı).
Yeni dış varlık/araç/paket yok. Ek ücret/royalty, ekran/jenerik kredi zorunluluğu veya yeni
üçüncü taraf lisans/telif metni dağıtma zorunluluğu yok. Mevcut yükümlülükler korunur.

### Hasat Yankısı — 2026-10-12
EchoPreparation/EchoImpact geometrisi ve EchoTok.wav proje içinde özgün üretildi; ses
Python standart kütüphanesiyle sentezdir, dış örnek yok. Önceden proje için üretilen
ThornToon/ThornInk ve ortak geometri/parça altyapısı yeniden kullanıldı. Kritik Ark ve
Taşan Hasar varlıkları değiştirilmedi. Yeni dış içerik/araç/paket yok: ek ücret/royalty,
ekran/jenerik kredi zorunluluğu veya yeni üçüncü taraf lisans/telif metni dağıtım
zorunluluğu doğmaz. Önceki üçüncü taraf yükümlülükleri korunur.

### Nadir Reaktör — 2026-10-12
ReactorCore/ReactorOpening ve ReactorCharge.wav proje içinde özgün üretildi; ses Python
standart kütüphanesiyle sentezdir, harici örnek yok. Önceden proje için üretilen ThornToon /
ThornInk, ortak mesh üretimi ve parça hareketi yeniden kullanıldı. Yeni dış içerik/araç/paket
yok. Ek ücret/royalty, ekran/jenerik kredisi veya yeni üçüncü taraf lisans/telif metni dağıtımı
zorunluluğu doğmaz. Mevcut üçüncü taraf yükümlülükleri korunur.

### Açgözlü Hasat — 2026-10-12
GreedTeeth mesh/prefab ve GreedReady/GreedSpent.wav proje içinde özgün üretildi.
Sesler Tools/Art/build-greed-audio.py ile Python standart kütüphanesinde, dış örneksiz
sentezlendi. Mevcut özgün ThornToon, mesh yazarı, havuz ve merkezi ses altyapısı paylaşıldı.
Yeni üçüncü taraf içerik/araç/paket yok. Ek ücret/royalty, oyun ekranında veya jenerikte
kredi ve yeni üçüncü taraf lisans/telif metni dağıtımı zorunluluğu doğmaz. Önceki
üçüncü taraf yükümlülükleri korunur. Araçlar Tools altında, dağıtılan varlıklar Assets altında.

### Kritik Patlama — 2026-10-12
CritBurstImpact yıldız/çekirdek/parça meshleri ve CritBurstThump.wav özgün üretildi.
Mevcut proje için özgün ThornToon ve mesh/parça/havuz altyapısı yeniden kullanıldı.
Ses Tools/Art/build-crit-burst-audio.py ile Python standart kütüphanesinde dış örneksiz
sentezdir. Yeni üçüncü taraf içerik, araç veya paket yok. Ek ücret/royalty, ekran/jenerik
kredi veya yeni üçüncü taraf lisans/telif metni dağıtımı zorunluluğu doğmaz. Önceki lisans
koşulları korunur. Tools geliştirme üreticileri, Assets oyunda kullanılan varlıklardır.

### Aşırı Yükleme — 2026-10-12
OverloadCharge/OverloadRelease prefabları, üç ortak mesh ve iki kısa mono ses proje içinde
özgün üretildi. Sesler Tools/Art/build-overload-audio.py ile Python standart kütüphanesinde
dış örneksiz sentezdir. Önceden proje için özgün ThornToon ve mesh/parça/havuz altyapısı
paylaşıldı. Yeni dış içerik, araç veya paket yok: ek ücret/royalty, ekran/jenerik kredisi
ya da yeni üçüncü taraf lisans/telif metni dağıtımı zorunluluğu doğmaz. Önceki koşullar
korunur. Tools geliştirme üreticileri; Assets oyunda dağıtılan varlıklardır.

## Blender — Comic Black Hole öğrenme örneği (15 Ekim 2026)

- Kategori: mevcut yerel geliştirme aracı; yeni paket veya eklenti kurulmadı.
- Araç: Blender 5.2.2 LTS, mevcut Steam kurulumu; resmî kaynak https://www.blender.org/.
- Kontrol edilen belgeler: kurulumdaki copyright.txt ve license/spdx/GPL-3.0-or-later.txt; çıktıların durumu için https://www.blender.org/about/license/.
- Lisans: ana bildirim GPL-2.0-or-later; dağıtım GPL-3.0-or-later uyumlu bileşenler içerir. Blender yazılımını yeniden dağıtmak ayrı GPL yükümlülüklerine tabidir; burada yazılım dağıtılmıyor.
- Kullanım: özgün mesh ve animasyon üretimi, .blend kaynak dosyası ve örnek render. Haricî model, font, doku, ses veya eklenti kullanılmadı.
- Ücret/royalty: üretilen özgün içerik için yok. Oyun ekranında/jenerikte Blender kredisi: zorunlu değil. Oyun dağıtımına sırf Blender çıktısı kullanımı nedeniyle Blender lisans/telif metni ekleme: zorunlu değil. Oyun kaynak kodunu açma şartı doğmaz.
- Bu içerik üretiminden önce kullanıcıya konuşmada lisans bildirimi yapıldı.
- Dosyalar: Tools/Art/ComicBlackHole/build_black_hole.py, Docs/ComicBlackHole/.

### Comic meteor örneği — 15 Ekim 2026

- Yukarıdaki mevcut Blender 5.2.2 LTS geliştirme aracı ve aynı çıktı lisans koşulları geçerlidir.
- Özgün geometri ve anahtar kare animasyonu: Tools/Art/ComicBlackHole/build_comic_meteor.py; çıktı Docs/ComicMeteor/ComicMeteor.blend.
- Yeni dış kaynak, paket, eklenti, ses, font veya doku alınmadı. Blender oyunla dağıtılmıyor.
- Özgün çıktılar için ücret/royalty yok; oyun ekranında/jenerikte Blender kredisi yok; yalnız çıktı kullanımı nedeniyle dağıtımda Blender lisans metni zorunluluğu yok.

## nene — Boss Battle #3 V1 (17 Ekim 2026)

- Kategori: oyunda dağıtılan müzik; yeni geliştirme aracı veya paket eklenmedi.
- Eser/sürüm: Boss Battle #3 [8-bit re-upload], V1 WAV; yayımlayan nene, 16 Mayıs 2018.
- Kaynak: https://opengameart.org/content/boss-battle-3-8-bit-re-upload
- Kontrol edilen belgeler: kaynak sayfasının CC0 lisans alanı ve https://creativecommons.org/publicdomain/zero/1.0/legalcode (17 Ekim 2026).
- Lisans: CC0 1.0 Universal. Ticari kullanım, değiştirme, oyuna gömme ve dağıtım serbest.
- Ücret/royalty: yok. Oyun ekranında veya jenerikte kredi zorunluluğu: yok.
- Dağıtımda lisans/telif metni bulundurma zorunluluğu: yok. Kaynak paylaşımı veya yeniden adlandırma yükümlülüğü yok.
- CC0 marka/patent haklarını kapsamaz ve garanti vermez; kaynakta bu parçaya özel ek kullanım koşulu belirtilmiyor.
- Kullanım: aktif round müzik havuzunun ilk ve şimdilik tek parçası; tüm aşamalarda loop.
- Kullanıcıya kaynağı, lisans sürümü ve koşullar dosya kopyalanmadan önce konuşmada açıklandı.
- Kullanıcının sağladığı yerel dosya: C:/Users/User/Downloads/Boss Battle #3 V1.wav.
- Hedef: Assets/Resources/Music/RoundMusic.wav. WAV değiştirilmedi; Unity build/import sırasında Vorbis'e kodlar.
- Kaynak ve hedef SHA-256: 34406888886469E6012330E1C496D4818A2EB9BE131CBBD22958CCC85CC8A661.

## Cozy Farm SFX — Bramble & Byte (17 Ekim 2026)

- Kategori: oyunda dağıtılan dört ses; geliştirme bağımlılığı eklenmedi.
- Kaynak: https://aquumgifts.itch.io/cozy-farm-sfx ; kullanıcıdaki cozy-farm-sfx indirmesi, sürüm numarası belirtilmemiş.
- Kontrol edilen belge: C:/Users/User/Desktop/sesler/cozy-farm-sfx/LICENSE.txt ve resmî https://creativecommons.org/licenses/by/4.0/legalcode.
- Lisans: CC BY 4.0 International. Ücretsiz ticari kullanım, değişiklik ve oyuna gömülü dağıtım serbest; lisans ücreti/royalty yok.
- Atıf zorunlu; özellikle oyun ekranı/jenerik şartı yok. Uygun dağıtım kredisi içinde üretici, kaynak, lisans bağlantısı ve değişiklik bildirimi sağlanır. Tam lisans metni yerine URI yeterlidir; verilen LICENSE.txt ayrıca korunur.
- Dağıtılan kayıtlar: Assets/StreamingAssets/ThirdParty/CozyFarm/LICENSE.txt ve CREDITS.txt. Oyun kaynak kodunu paylaşma/ad değiştirme şartı yok; lisanslı içeriğin haklarını kısıtlayan ek koşul/DRM uygulanmamalıdır.
- Kullanımlar: place-item.wav = saksı inişi; bush-rustle.wav = bitki hasarı; ui-error.wav = geçersiz yerleştirme; ui-back.wav = yerleştirmeyi iptal.
- Hedef: Assets/Audio/CozyFarm/. Orijinal WAV değişmedi; ses seviyesi oynatmada ayarlanır ve oyun aksiyonlarıyla senkronize edilir; Unity platform kodlaması yapabilir.
- Masaüstündeki dört WAV ile lisanslı indirmedeki sfx dosyalarının SHA-256 eşitliği doğrulandı.
- place-item: 12E3AC609354B5E0631BCC31D93062F76C2F58E2DD640F5F0411D53F4D627FC6
- bush-rustle: 667A8835E88CC3AD2D3A6C21D0B004B76FC29E28841081025C1B6EBFAA2C8D18
- ui-error: 6DC16242FDF9E0BEA09E06355820974DA00AE20C343E7AEB8C4AAC8A873E7196
- ui-back: CA5C1A7665946BA76399D66ED62BE9093629645C7EE6EE1EB9AB25ED3FEF175B
- Kullanıcıya lisans ve ayrı ücret/kredi/dağıtım yükümlülükleri kopyalamadan önce konuşmada bildirildi.
# Unity Pipeline bağlantısı — 2026-10-17

- Amaç: geliştirme aracı; açık Unity Editor'ı resmî CLI / MCP üzerinden yönetmek.
- Kaynak: https://packages.unity.com/com.unity.pipeline — `com.unity.pipeline` **0.8.0-exp.1**, Unity Technologies.
- Kontrol edilen belgeler: sürüm tarball'ındaki `LICENSE.md`, `Third Party Notices.md`, `package.json`; https://unity.com/legal/terms-of-service (30 Haziran 2026), https://unity.com/features/ai.
- Lisans: **Unity Terms of Service**; MIT değildir. Mevcut geçerli Unity lisansı ve ilgili koşullar geçerlidir. CLI/MCP ücretsizdir; ayrı kullanım ücreti/royalty ve bu araç için ekran/jenerik kredisi yoktur. Kaynak paylaşma yükümlülüğü getirmez. Deneysel sürümdür.
- Paket içi Roslyn Common/CSharp 3.11.0, System.Collections.Immutable / System.Reflection.Metadata 10.0.1 ve Unsafe 6.1.2: **MIT**. Unity tarafından `UnityPipeline.` ad alanı/assembly önekiyle değiştirilmiş; değişiklik tarafımızdan yapılmadı. Dağıtılan kopyalarda telif/izin metinleri korunur.
- Yeni bağımlılık `com.unity.nuget.newtonsoft-json` **3.0.2**: Unity Companion License (resmî güncel metin **v1.4**, https://unity.com/legal/licenses/unity-companion-license); içerdiği Newtonsoft/Unity uyarlamaları MIT. Geçerli Unity lisansıyla ticari Unity projesinde ücretsiz/royalty-free kullanım; ekran/jenerik kredisi yok; dağıtılan bileşenlerin lisans ve telif metinleri korunmalı. Unity motoru lisansını değiştirmez.
- Diğer bağımlılıklar projede zaten mevcut: Test Framework, Mono.Cecil ve Unity engine modules.
- Geliştirme aracı kaydı ile oyunda dağıtılan varlıklar ayrıdır: Pipeline bu projeye Editor otomasyonu amacıyla eklendi; runtime bileşenleri bulunabildiği için üçüncü taraf bildirimleri `Assets/StreamingAssets/ThirdParty/UnityPipeline/` altında dağıtıma taşınır. Paket lisans kopyası `Docs/Licenses/UnityPipeline/` altındadır.
- Kullanıcıya kurulumdan önce konuşmada kaynak/sürüm, lisans, ücret, kredi ve dağıtım yükümlülükleri bildirildi. Mevcut Unity CLI **1.0.0-beta.13** güncel olarak doğrulandı; CLI yeniden kurulmadı.

# Kullanıcının AI ile ürettiği bitki hasar sesi — 2026-10-17

- `plant-thwack-v6.wav`: kullanıcı dosyayı sağladı ve konuşmada AI ile ürettiklerini doğruladı. Üretim hizmeti/lisans sürümü belirtilmedi; üçüncü taraf ses paketine aitmiş veya CC0/MIT lisanslıymış gibi etiketlenmedi.
- Kaynak: `C:/Users/User/Downloads/plant-thwack-v6.wav`; proje kopyası: `Assets/Audio/Generated/plant-thwack-v6.wav`.
- SHA-256: `E7096EBA67469A2F359009FD22F287485E2BC5A65F38289A044F08549ADD7DF2`.
- WAV değiştirilmeden kopyalandı. GameScene SoundManager PlantDamage klibi olarak bush-rustle yerine atandı; volume 0.3, pitch 1 korundu. Cozy Farm lisansı bu dosyaya uygulanmaz; diğer Cozy Farm sesleri için mevcut bildirimler korunur.

# Kullanıcının AI ile ürettiği bitki hasar sesi v7 — 2026-10-17

- `cartoon-roguelike-hit-v7.wav`: kullanıcı dosyayı sağladı ve AI ile kendilerinin ürettiğini doğruladı. Üretim hizmeti/lisans sürümü belirtilmedi; CC0/MIT veya Cozy Farm lisansı atfedilmedi.
- Kaynak: `C:/Users/User/Downloads/cartoon-roguelike-hit-v7.wav`; proje kopyası: `Assets/Audio/Generated/cartoon-roguelike-hit-v7.wav`.
- SHA-256: `5530E342DB82B2F80435D8B1D08B7A18785AF182668712CC979259B598047EF0`.
- Dosya değiştirilmeden kopyalandı. GameScene SoundManager PlantDamage ataması v6 yerine v7 olarak Unity CLI ile kaydedildi; volume 0.3, pitch 1 korundu.


## CartoonVFX9x — Comic Explosion Effect / Battle_Effect_White (22 Ekim 2026)
- Oyunda dağıtılan mevcut üçüncü taraf varlık; yeni indirme yok. Kaynak: https://assetstore.unity.com/packages/vfx/particles/fire-explosions/comic-explosion-effect-317348
- Resmî sürüm: 1.0 (22 Nisan 2025); yerel prefab: Assets/3D Assets/CartoonVFX9x/Comic_FX/Prefabs/Battle_Effect_White.prefab. Yerel paket sürüm etiketi yok; mağaza yalnız 1.0 gösteriyor.
- Lisans: Standard Unity Asset Store EULA, https://unity.com/legal/as-terms (22 Ekim 2026 kontrolü), Appendix 1, 2.2–2.3. Yerel Readme/Readme.pdf incelendi; ek lisans koşulu içermiyor.
- Kullanım: Artçı Patlama'nın ikinci darbesinde, daha küçük ölçekle ve mevcut sınırlı VFXPool üzerinden. Kaynak prefab değiştirilmez; özgün üretim olarak sunulmaz.
- Ücret/royalty: mağazada FREE; oyunun satışına bağlı royalty yok. Ticari oyuna gömülü dağıtım ve değişiklik izinli; bağımsız asset/kaynak yeniden dağıtımı izinli değil.
- Ekran/jenerik kredisi: ayrıca zorunluluk belirtilmiyor. Dağıtım paketine ayrı EULA/telif metni ekleme yükümlülüğü belirtilmiyor; var olan kaynak bildirimleri korunur. Kaynak kod paylaşma yükümlülüğü yok.
- Mağaza Extension Asset etiketi taşıyor: ilgili koltuk/iki bilgisayar sınırı ve yüklenicilerin kendi lisansı koşulu dikkate alınmalı; yalnız test/build makineleri istisnası var. Yeni lisans satın alınmadı.

## Hasat Yankısı kuzey ışığı meteoru — özgün içerik, Blender ile (23 Ekim 2026)

- Oyunda dağıtılan varlıklar: `Assets/Art/LateGame/AuroraMeteor/` (22 ağ), `Assets/Resources/AuroraMeteorArt.asset`.
- Üretim: Blender 5.2 LTS (mevcut Steam kurulumu; lisans kaydı yukarıdaki Blender bölümünde) komut satırından Python ile:
  `Tools/Art/AuroraMeteor/build_aurora_meteor.py` (tasarım ve önizleme), `export_aurora_meteor.py` (dışa aktarma),
  `Tools/Verification/Editor/AuroraMeteorArtBuilder.cs` (Unity ağları). Önizlemeler: `Docs/AuroraMeteor/`.
- Bütün geometri ve renkler bu görev için özgün üretildi. Dış model, doku, eklenti, MCP sunucusu veya paket kullanılmadı.
- Blender çıktıları kullanıcınındır; Blender yazılımı dağıtılmıyor. Ücret veya royalty yok, oyunda kredi zorunluluğu yok,
  dağıtım paketine lisans metni gerekmiyor.
- Kullanılan proje malzemeleri: `ThornToon.mat` / `ThornInk.mat` (mevcut özgün) ve `Resources/ParticleVertexColor` shader'ı.

## Açgözlü Hasat “aç ağız” örneği — özgün içerik, Blender ile (24 Ekim 2026)

- Oyunda dağıtılan varlıklar (24 Ekim 2026'dan beri): `Assets/Art/LateGame/GreedJaw/` (14 ağ + `GreedJawInk.mat`),
  `Assets/Resources/GreedJawArt.asset`. Ağlar `Tools/Art/GreedJaw/export_greed_jaw.py` → `Tools/Verification/Editor/GreedJawArtBuilder.cs`
  ile üretildi; malzeme mevcut özgün `ThornComicToon` shader'ını kullanır. Önizlemeler `Docs/GreedJaw/`.
- Üretim: Blender 5.2 LTS (mevcut kurulum; lisans kaydı yukarıdaki Blender bölümünde), `Tools/Art/GreedJaw/build_greed_jaw.py`.
  Çıktılar `Docs/GreedJaw/`; MP4/GIF dönüşümü mevcut FFmpeg geliştirme aracıyla (yeni kurulum yok).
- Geometri, renkler ve animasyon bu görev için özgün üretildi; dış model, doku, font dosyası, eklenti veya paket kullanılmadı
  (önizleme etiketleri Blender'ın yerleşik yazı tipiyle yalnız videoya yazıldı, oyuna girmez).
- Ücret/royalty yok, oyunda kredi zorunluluğu yok, dağıtım paketine lisans metni gerekmiyor.

## Diken Kuşatması çıkış patlaması — özgün içerik, Blender ile (24 Ekim 2026)

- Oyunda dağıtılan varlıklar (24 Ekim 2026'dan beri): `Assets/Art/LateGame/ThornBurst/` (10 ağ), `Assets/Resources/ThornBurstArt.asset`.
  Üretim: Blender 5.2 LTS (mevcut kurulum; lisans kaydı yukarıda), `Tools/Art/ThornBurst/build_thorn_burst.py` → `export_thorn_burst.py`
  → `Tools/Verification/Editor/ThornBurstArtBuilder.cs`; önizlemeler `Docs/ThornBurst/`, MP4/GIF mevcut FFmpeg ile.
- Geometri, renkler ve animasyon özgün; dış model, doku, font, eklenti veya paket yok. Ücret/royalty yok, kredi zorunluluğu yok,
  dağıtım paketine lisans metni gerekmiyor.

## İstilacı Filiz kişiliği — özgün içerik, Blender ile (24 Ekim 2026)

- Oyunda dağıtılan varlıklar (25 Ekim 2026'dan beri): `Assets/Art/LateGame/SproutBurst/` (61 ağ), `Assets/Resources/SproutBurstArt.asset`.
  Üretim: Blender 5.2 LTS (mevcut kurulum; lisans kaydı yukarıda), `Tools/Art/SproutBurst/build_sprout_burst.py` → `export_sprout_burst.py`
  → `Tools/Verification/Editor/SproutBurstArtBuilder.cs`; önizlemeler `Docs/SproutBurst/`, MP4/GIF mevcut FFmpeg ile.
- Geometri, renkler ve animasyon özgün; dış model, doku, font, eklenti veya paket yok. Ücret/royalty yok, kredi zorunluluğu yok,
  dağıtım paketine lisans metni gerekmiyor.

## Aşılı Kabuk kişiliği — özgün içerik, Blender ile (25 Ekim 2026)

- Oyunda dağıtılan varlıklar (25 Ekim 2026'dan beri): `Assets/Art/LateGame/ShellBurst/` (66 ağ), `Assets/Resources/ShellBurstArt.asset`.
  Üretim: Blender 5.2 LTS (mevcut kurulum), `Tools/Art/ShellBurst/build_shell_burst.py` → `export_shell_burst.py`
  → `Tools/Verification/Editor/ShellBurstArtBuilder.cs`; önizlemeler `Docs/ShellBurst/`, MP4/GIF mevcut FFmpeg ile.
  Diken çalışmasının özgün toon yardımcıları yeniden kullanılır.
- Aynı gün oyundaki Kabuk B rengi maviden mora çevrildi: `Assets/Editor/ShellArtBuilder.cs` (proje içi özgün builder) ile
  yeniden üretilen B ağları `Assets/Art/LateGame/GraftedShell/` altında; dış kaynak yok.
- Geometri, renkler ve animasyon özgün; dış model, doku, font, eklenti veya paket yok. Ücret/royalty yok, kredi zorunluluğu yok,
  dağıtım paketine lisans metni gerekmiyor.
