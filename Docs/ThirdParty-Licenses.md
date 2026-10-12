# Dış kaynak ve lisans kayıtları

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
