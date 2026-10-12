# Aşılı Kabuk sunumu — 8 Ekim 2026

## Tasarım ve kaynak

A: geniş, krem kabuk yapraklarının açıldığı sıcak altın tomurcuk; B: mor kılıfın içinden yükselen
uzun buz mavisi kristal koza. İkisi ayrı silüet ve geometri taşır. Dünya ve HUD aynı tek üçgen / çift
baklava motiflerini kullanır. Ana hedefler kırmızı dikkat konturu ve üç hasar aşaması taşır.
Kullanıcının verdiği görsel yalnız sanat yönü referansıdır; model veya doku olarak oyuna alınmadı.

Bölge parçaları hücre merkezine yerleşir. Her parça saksı çevresinde iki aralıklı organik sarmaşıktan
oluşur: A'da altın damarlar ve içe kıvrılan yapraklar, B'de mor dallar ve buz çıkıntıları. Hazırlıkta
alçaktır; kapanışta yükselir. Bitkinin ortası açık kalır, düz kapak veya çizgisel hücre çerçevesi yoktur.
İzole GameScene'de 121 bitki ve normal oyun kamerasıyla hazırlık, seçim ve kapalı durum incelendi.

A hedefindeki kırmızı ünlem ve durum plakasındaki süre, seçim yapılmazsa A'nın kapanacağını belirtir.
Darbe ezilmesi, beyaz flash ve çatlak aşamaları korunur. Kabuk'a özel kısa toon pufları, yaprak ve dal
parçaları kapanış, darbe, kırılma ve çözülmeyi destekler; sürekli parçacık üretimi veya dinamik ışık yoktur.
Parlak damarlar toon renkleriyle çizilir; bloom/post-process bağımlılığı eklenmez.

Diken/Filiz'in onaylı varlıkları yeniden üretilmedi. Ortak toon/ink materyalleri, kırmızı kontur, darbe
sistemi, havuz ve mevcut sentez sesleri kullanılır. Bu revizyonda yeni dış içerik veya araç alınmadı.
Kaynak/ücret/kredi/lisans bildirimi ThirdParty-Licenses.md'dedir.

## Yaşam döngüsü

- Kabuklara özel SyncShell, mevcut presenter havuzları ve 96 dekor sınırı içinde çalışır.
- Her bölge parçası aynı instance içinde hazırlanır ve kapanır; seçimde yeniden prefab kurulmaz.
- Kırılan hedef ve kurtarılan bölge .5/.68 saniyelik açılma/çekilme kuyruğuna gider. Diğer bölge .42 saniyede
  sarmaşıklarını saksı etrafında yükselterek kapanır. Kuyruklar oyun mantığına yazmaz.
- Mevcut GraftedShellEvent kapanma kararında üretim engelini anında kurar. Süre sonunda engel anında kalkar;
  geride kalan .68 saniyelik açılma yalnız sunumdur.
- Kapanma/açılma, hedef ezilmesi, beyaz flash ve A uyarısı oyun zamanı ile yürür. Pool Show/Reset, A/B state'i,
  sarmaşık yüksekliği/dönüşünü, kabuk yapraklarını, hasar izlerini ve flash'ı sıfırlar. Geometri/materyal referansları paylaşılır.
- LateGameShell kendi Update veya coroutine'ini çalıştırmaz. Collider/physics eklenmez.
- Yeni sesler ortak sekiz kaynaklı, tekrar sınırlı ses havuzundan çalınır.

## Korunan değerler

Hazırlık 6 sn, ortaya çıkış 1.2 sn, hedef başına 4 doğrudan vuruş, seçim 8 sn, kapanma 20 sn, çözülme 1 sn,
ikisi de 3×2 bölge ve en fazla .6 üretim payı. A'yı kırmak A'yı kurtarır; B'yi kırmak B'yi kurtarır;
seçimsiz sonuç A'nın kapanmasıdır. Oynanış kodu ve plan asset'i değiştirilmez.

## Doğrulama yöntemi

İzole VerificationProject içinde GameScene'nin 121 bitkili laboratuvarı ve normal oyun kamerası kullanılır.
A seçimi (seed 42), B seçimi (seed 12), seçimsiz yol (seed 21) ayrı ayrı gerçek plan ve saldırı yoluyla test edilir.
Hazırlık/ortaya çıkış gerçek sürelerle beklenir; karar ve kapanma süre sonu kontrollerinde olayın Tick'ine
8/20 saniye verilir, asset değerleri değiştirilmez. 70 ms darbe karesi gerçek Hit'ten sonra deterministik
sunum adımıyla yakalanır; batch editörünün çok hızlı kareleri kısa tepkiyi atlamasın diye yapılır.

Menu/run restart testleri director'ın gerçek olay callback'lerini çağırır; normal disk kaydı yerine batch
MemoryOnly kullanılır. Bu testler insanın menü düğmelerine basarak yaptığı uçtan uca Play onayı değildir.
Protected-before.json, önceki onaylı varlıklar/sahneler/mekanikler/profil/gerçek kayıtların hash kaydıdır.

## İnsan Play kontrolü

1. Açıklamayı okumadan A/üçgen ve B/çift baklava hedeflerini kendi bölgeleriyle eşleştir.
2. Farklı bitkiler ve yoğun saldırı VFX'i arasında hedefe yönelme, kırmızı kontur ve beyaz darbe flash'ına bak.
3. A, B ve hiç seçim yapmama yollarında kurtulan bölgenin açılmasını ve diğer bölgenin kapanmasını izle.
4. Süre sonunda üretimin geri gelmesiyle açılma hissinin örtüşmesini ve ses miksini dinle.
5. Pause, round bitişi, menü ve yeni run sonrasında eski kabuk/uyarı/ses kalmadığını kontrol et.

Commit atılmaz. Gezgin Bereket sunumu kapsam dışındadır.

## Sonuçlar ve bütçe

Aşılı Kabuk revizyonu: **100 kontrol geçti**. Üç karar yolu, hasar, pause, havuzda yeniden kullanım,
menü/run temizliği, kapanmanın mantık/animasyon sırası ve yeni sarmaşıkların hücre içinde kalması doğrulandı.
İlk geometri sınır testi A yapraklarının dışa taştığını yakaladı; yapraklar içe kıvrılarak düzeltildi.

| Model | Kontursuz üçgen | Konturlarla üçgen |
|---|---:|---:|
| A ana tomurcuk | 504 | 932 |
| B ana kristal koza | 450 | 850 |
| A hücre sarmaşığı | 360 | 720 |
| B hücre sarmaşığı | 504 | 1008 |

Hasar aşamaları dahil üst sınırlar; kontur hull çizimleri ek geometri maliyetidir. Vertex/renderer ve VFX
sayıları ShellGeometry.md'dedir. Altı kapalı B hücresi toplam 6048 kontur dahil üçgen / 36 renderer kullanır;
A için 4320 / 36. Eski küçük motiflere göre maliyet yükseldi, buna karşılık kapanan saksılar modellenerek
okunuyor. Ortak materyaller ve havuz kullanılıyor; cihaz üzerinde GPU/draw-call profili ayrıca ölçülmeli.

Dört mevcut ses mono 22050 Hz / 16 bit, .38–.80 saniye, .55 peak (AudioValidation.txt).
Son koruma karşılaştırması: **121 dosya, 0 fark**, Protected-redesign.json.
Diken/Filiz için önceki 41/37 test raporları korunur; bu revizyonda yeniden çalıştırılmış gibi sayılmaz.

Görüntüler: Shell_0 = A seçimi; Shell_1 = B seçimi; Shell_2 = seçimsiz kapanış.
02_Choice: normal kamera; 04_Closing: kapanırken; 05_Closed: kapalı; 06_Open: çözülürken.
İnsan Play kontrolü özellikle yoğun saldırı efektlerinde hedef/bölge eşleştirmesi ve ses miksidir.
Bu görüntülerin incelenmesi kullanıcı Play onayı yerine geçmez.
Genel geç oyun regresyonu son varlıklarla yeniden çalıştırıldı: **97 kontrol geçti**, exit 0.
Bu revizyonda toplam 197 kontrol. Derleyici/shader hatası yok; Unity SearchDatabase başlangıç
indeksleme istisnası mevcut editör logunda görülebilir. Rapor: GeneralRegression.txt.
