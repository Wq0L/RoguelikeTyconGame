# İstilacı Filiz — comic/toon içerik

8 Ekim 2026. Tam sürüm GameScene. Mevcut İstilacı Filiz mantığı/verisi değiştirilmeden sunum üretimi.
Diken'in kabul edilen modelleri, materyalleri, shader'ı ve ses dosyaları korunur.

## Görsel ayrım

Ana hedef: yükseltilmiş kalın sap, pembe çekirdek ve dört geniş turkuaz yapraklı açılan tomurcuk.
Küçük yayılma parçası: yere yakın iki yaprak; ana hedefin çekirdeği, yüksekliği ve dört yapraklı tacı yok.
İki biçim renk dışında yükseklik, silüet, açılma ve nabız hareketiyle ayrılır.
Hücre çerçevesi veya büyük opak yüzey yok; küçük parçalar saksı köşesinde durur.

## Yeniden kullanılan altyapı

- Diken'in `ThornToon` ve `ThornInk` malzemeleri / `ThornComicToon` shader'ı: birebir aynı assetler.
- `GroundHeave` ve `ThornSoilBurst`: ayrı Sprout katalog varyantından aynı prefablar.
- `LateGameContent`, merkezi presenter, 96 dekor sınırı, havuz, gerçek Hit, kısa parçacık ömrü ve ses sınırlandırıcısı.
- Hazırlık ve kırılma için mevcut özgün Diken sesleri. Yeni açılma, yayılma, yaprak darbesi ve solma sesleri
  Python standart kütüphanesiyle özgün sentezlendi (`Tools/Art/build-sprout-audio.py`).
- Editor'deki mevcut geometrik üretici `LateGameMeshBuilder` içine taşındı; Diken'in üretilmiş assetleri yeniden yazılmaz.
  Yeni `Leaf` işlemi yalnız filizin yaprak geometrisini üretir.

## Hareket ve yaşam döngüsü

- Hazırlık: ortak toprak kabarması; henüz yavaşlatma yok.
- Çıkış: yeraltından yükselme ve menteşeli yaprakların tomurcuktan açılması.
- Aktif: hafif görsel child nabzı ve ana yapraklarda düşük açılı canlılık hareketi.
- Yayılma: yalnız yeni noktada ortak toprak sıçraması ve yeni iki yaprağın açılması; ana nesne yeniden kurulmaz.
- Darbe: gerçek TargetHit ile ezilme/yaprak esnemesi, kademeli çekirdek izleri ve yaprak kırıntıları.
- Çözülme: ana hedefin sekizinci vuruşunda üretim yavaşlatması hemen kalkar. Küçük parçalar yayılma sırasına göre
  .045 sn aralıklarla (en fazla .32 sn gecikme) katlanıp çekilir; her küçük parçanın çıkışı .55 sn'dir.
  Arka yüzün mat solgun tonu görünür. Tüm sıra mevcut 1 sn çözülme fazının içine sığar.
- Pause'da animasyon ve sıralı çıkış donar. Round/run sonu veya yeni olay bütün kuyrukları beklemeden temizler.
- `LateGamePetals` kendi Update/coroutine'ini çalıştırmaz; merkezi sunum adımından sürülür. Havuzdan çıkışta yaprak
  rotasyonları, izler, yeraltı ofseti ve darbe durumu sıfırlanır.

## Değişmeyen mekanik

8 doğrudan vuruş, 2.5 sn yayılma aralığı, ana dahil 8 nokta, 2 hücre yarıçapı, 20 sn yayılma penceresi,
35 sn aktif süre, ×1.6 üretim aralığı. Saksı/bitki nesneleri, üretim noktaları ve mantıksal hedef hücresi aynı kalır.
Aşılı Kabuk, DemoScene, seçili profil ve gerçek kayıt bu işin kapsamı dışındadır. Commit atılmaz.

## Doğrulama

`SproutArtVerification` izole kopyada mevcut 121 bitkili GameScene laboratuvarını ve gerçek veri asset'ini kullanır.
İlk yayılmayı gerçek 2.5 sn aralıkla gözler; kapasiteyi göstermek için sonraki test adımlarında olay Tick metoduna
aynı aralık gönderilir, veri/süre/dayanıklılık değiştirilmez. Otomatik ölçüm insan Play onayı değildir.
Son test sonuçları, yakalamalar ve geometri karşılaştırması çalışma bittiğinde bu klasöre kaydedilir.

İnsan Play'de: ana hedefe ilk bakışta yönelme, küçük filizlerin saksıyı örtmemesi, büyüme/yavaşlama ilişkisinin okunması,
solma sırasının hissi, ses miksinin dinlenmesi ve farklı ekran oranları kontrol edilmeli.

## Kırmızı dikkat konturu ve son durum — 8 Ekim 2026

Kullanıcının son renk seçimi kırmızıdır. Ana hedeflerde .115, küçük parçalarda .065 dünya birimi
kalınlığında ikinci ters-yüz konturu mevcut koyu comic çizgisini çevreler. Toprağa veya hücre sınırına
renkli çerçeve eklenmez. Mesh referansları paylaşılır; runtime materyal kopyası, per-frame allocation veya
ayrı Update yoktur. Kontur prefabla aynı havuza girer, solma/pause/temizlik hareketini miras alır.

Normal kamerada okunurluk için ana yaprak uzunluğu 1, küçük yaprak uzunluğu .46 birimdir. Filiz toprağı
ortak GroundHeave prefabının .65 görsel ölçeğidir. Hareketli küçük yaprak renderer'larında dinamik occlusion
kapalıdır. Shader ve Diken'in orijinal mesh/materyal/sesleri aynıdır; Diken'in yalnız ThornRoot ve ThornPiece
prefablarına ortak kırmızı kontur çocukları eklendi. Koruma hash karşılaştırması başka fark bulmadı.

Üretim: SproutArtBuilder.Build; ardından LateGameAttentionBuilder.Build (Editor Tools/Art/Refresh Event Target Rims).
Kaynak geometriyi tekrar üretmeye gerek olmadan kontur menüsü dört hedef prefabını günceller.

Filiz otomatik sonucu: **34 kontrol geçti**. Pause, ilk gerçek yayılma, sınır, gerçek 8 vuruş,
hemen kaldırılan yavaşlatma, sıralı solma, pool reuse, round temizliği ve gerçek süre sonu kapsanır.
`Sprout_05_FullSpread.png` 121 bitkili GameScene laboratuvarını normal kamera ile gösterir.
`LateGameGeometry.md` iki içerik ve ortak VFX için Unity mesh ölçümlerini içerir.

Poligon azaltma bu adımda yapılmadı. Kırmızı kontur ek geçiş maliyeti açıkça raporlandı. Ana Filiz'in 18
renderer'ından 3'ü kapalı hasar aşamasıdır; küçük Filiz 9 renderer kullanır. Poligon sayısından bağımsız
çizim maliyeti için sonraki karar aşamasında özellikle bu küçük parça çizim sayısı değerlendirilebilir.

Son regresyon: Diken **40/40**, genel geç oyun **97/97** kontrol geçti. Filiz ile toplam **171** otomatik kontrol.
Dört yeni WAV: mono, 22.05 kHz, 16 bit, en yüksek genlik .55; dijital clipping yok. Sesin oyun içi miks hissi insan Play kontrolünde.

`Protected-before.json` karşılaştırması: GameScene, DemoScene, seçili profil, gerçek kayıt/yedeği, bütün FieldEvents
mekanik/plan dosyaları, ortak shader, kamera kodu ve Diken mesh/materyal/sesleri aynı hash'te.
Yalnız kullanıcının istediği kontur nedeniyle ThornRoot.prefab ve ThornPiece.prefab farklı (`Protected-changes.json`).

Test log notu: Unity editörünün Search.SearchDatabase başlangıç indekslemesinde mevcut ArgumentOutOfRangeException
üç batch oturumunda da görüldü. Derleyici/shader hatası yok; bu editör arama indeks hatası oyun testlerinden ayrı
raporlanır. Bütün test süreçleri exit 0 ile tamamlandı.

8 Ekim ek vuruş okunurluğu düzeltmesi: sıradan bitkilerle ortak beyaz flash, .32 sn ezilme/geri yaylanma
ve daha görünür darbe kırıntısı eklendi. Poligon ve renderer sayıları değişmedi. Güncel doğrulama ve
vuruş görüntüleri `Docs/EventHitFeedback/` klasöründe.
