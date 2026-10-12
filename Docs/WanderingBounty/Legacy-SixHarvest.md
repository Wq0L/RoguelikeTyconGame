# Gezgin Bereket sunumu — 8 Ekim 2026

## Görsel dil ve oyuncu bilgisi

Her fırsat hücresinde üç krem/şeftali polen çiçeği vardır. Yuvarlak çiçek formu, tehditlerin dikenli
çevrelerinden ve bitki rarity renk halkalarından ayrılır. Bitkiler/zemin örtülmez, devamlı sınır veya
bağlantı çizgisi yoktur. Dokuz hücrenin tamamı işaretlenir; hafif hareket yalnız ortak presenter saatinden gelir.

Durum plakasındaki altı çiçek, doğrudan WanderingBountyEvent.Progress/Threshold verisine göre dolar.
Yanındaki sayaç aynı veriyi kullanır. İlerleme yalnız olayın kabul ettiği direct hasat cue'suyla görünür
bir polen tepkisi ve kısa ses verir. Behavior, zincir, dış hücre ve canlandırılan bitkinin yeniden hasadı
sahte ilerleme vermez. Tamamlanma Progress'i sıfırlarken sonuç satırı gerçekten canlanan yuva sayısını gösterir.

Bölge adım attığında eski hücre işaretleri hemen kaldırılır; ortak hücreler aynı instance'ı korur,
yeni hücreler belirme geçişi yapar. Oynanış bölgesiyle ilgisiz süzülen bir alan yoktur.
Olayın sonunda .24 saniyelik yalnız görsel sönme vardır; sönme üretim veya ödül kararını bekletmez.

## Canlanmanın gerçek kaynağı

WanderingBountyEvent.BloomCells, TryReviveNow başarılı olduktan sonra hücreyi kaydeden salt okunur
sunum makbuzudur. Üretim yolu, başarı koşulları, sıralama, rastgele zar ve ödül hesabı değiştirilmedi.
Polen akışları ve canlanma patlamaları yalnız bu listedeki hücrelere gider. Üretim anında gerçekleşir;
akış .62 saniyede ulaşır ve efektler .85 saniyede havuza döner. Tamamlanma sesi eşiğin tamamlanmasını anlatır.
Hiç uygun boş yuva yoksa akış/canlanma efekti yoktur; HUD açıkça 'uygun boş yuva yok' yazar.

Korunan plan: 3×3 bölge, 5 sn hareket aralığı, 6 direct hasat, en çok 3 canlanma, 30 sn aktif süre;
hazırlık 3 sn, belirme .8 sn, çözülme 1 sn. Ceza, para/XP/skor veya ikinci bitki eklenmedi.

## Üretim ve sistem maliyeti

Yeni model ve mesh VFX'leri Tools/Verification/Editor/BountyArtBuilder.cs üretir. Sesler
Tools/Art/build-bounty-audio.py ile Python standart kütüphanesinde özgün sentezlendi.
Dış varlık/araç/paket indirilmedi. Materyaller önceki proje üretimi toon/ink materyalleridir.
Polen ve çiçekler opak mesh; saydam ParticleSystem, dinamik ışık, fizik, hücre başına Update/Animator yoktur.
LateGameContent'e eklenen isteğe bağlı Bounty bileşeni sadece ortak presenter tarafından adımlanır.
Mevcut 96 dekor sınırı ve havuz kullanılır. Tek hücre işareti iki renderer'dır.

Sesler hafif belirme, ilerleme ve tamamlanmadır; hareket/boşta tekrar eden ses yoktur.
İlerleme sesi tek voice ve .14 sn tekrar aralığıyla sınırlı. Mevcut toplam 8 voice korunur.
Ses cooldown anahtarı 16 yerine 32'lik aralık kullanır: yeni/önceki 16 üstü sound numaraları
komşu variant ile çakışmaz. Dizinin üst sınırı 512'dir; çalışma sırasında büyümez.

## Doğrulama yöntemi ve Play listesi

İzole VerificationProject, GameScene, 121 bitki, normal oyun kamerası; gerçek kayıt MemoryOnly ile kapalı.
Gerçek direct saldırılarla altı hasat tamamlanır. Behavior hasadı eklenerek yedi boş yuvanın canlanması,
dolu iki yuvanın aynı bitkiyi koruması ve yalnız başarılı yuvalara yedi akış/efekt gönderilmesi test edilir.
Hareket ve süre testleri mevcut Tick'e plan sürelerini verir; değerleri değiştirmez. Boş yuva olmayan
uç durum doğrudan Bloom çağrısıyla test edilir. Menü/restart gerçek director callback'leriyle sınanır.
Bu otomasyon insanın menü düğmelerini kullanarak verdiği Play onayı değildir.

İnsan Play'de kısa liste:
1. Polen çiçeklerinden aktif 3×3 bölgeyi ve kendi vuruşunla hasat edilecek bitkileri seçebilme.
2. Bölge adımında eski/yeni hücreleri ayırma; altı çiçeğin gerçek hasat sayacıyla dolması.
3. Tamamlanma akışı, gerçekten canlanan yuvalar ve sonuç satırını normal hasattan ayırma.
4. Yoğun zincir efektleri arasında okunurluk; fırsatı izlemezken ses/hareketin rahatsız etmemesi.
5. Pause, round sonu, menü ve yeni run sonrası eski işaret/akış/ses kalmaması.

Commit atılmadı. Anten/hilal ailesine yeni içerik eklenmedi.

## Sonraki düzeltme — kullanıcı notları (8 Ekim 2026)
- Bazı buff görselleri yanlış konumda spawn oluyor.
- Bazı modeller kare/hücreye tam oturmuyor.
- Alan/grid zaten gösterilirken toprak parçaları çıkmaya devam ediyor ve kötü görünüyor.
- Hangi olay/hücrelerde oluştuğu sonraki düzeltmede Play üzerinden incelenecek.
Kullanıcı talebi: bu sorunlara mevcut Gezgin Bereket tesliminde dokunma; sonraki düzeltmeye bırak.

## Son üretim sonuçları

Bereket özel doğrulaması: 42 kontrol geçti. Mevcut varlık/sahne/profil/kayıt/mekaniklerden alınan
261 dosyalık başlangıç karşılaştırmasında 260 aynı. Değişen tek dosya WanderingBountyEvent.cs:
başarılı canlanma hücre makbuzu, Threshold okuma özelliği ve ilerleme cue'sunun gerçek hücresi eklendi.
Plan değerleri ve üretim/ödül kararları değiştirilmedi. Protected-after.json ayrıntıları içerir.

Kontur dahil üçgen / renderer: hücre işareti 1392 / 2; ilerleme pufu 216 / 6;
polen akışı 216 / 2; canlanma pufu 432 / 12. Dokuz hücre boşta 12528 üçgen, 18 renderer.
Tamamlanma örneğinde 29 dekor instance ve 152 renderer ölçüldü; 96 instance sınırına takılmadı.
Polen kürelerinin son optimizasyonu işaret başına üçgeni 1824'ten 1392'ye düşürdü.

CPU mikro kontrolü: pause durumundaki tamamlanma örneğinde presenter 500 kez yaklaşık 10.085 ms,
ölçülen GC 0 byte. Bu yaklaşık .020 ms/çağrıdır; GPU, normal akış veya bütün oyun kare süresi ölçümü
değildir. Sonuç farklı cihazlarda performans garantisi sayılmaz (BountyPerformance.txt).

Görsel inceleme sınırı: Bounty_07_BehaviorOverlap örneğinde yoğun elektrik çizgileri polen çiçeklerini
kısmen örter. Bu test false progress oluşmadığını doğrular; yoğun efektlerde okunurluğu onaylamaz.
Bu konu ve kullanıcının konum/hücre/toprak notları sonraki görsel düzeltmede ele alınacak.

Genel geç oyun regresyonu: 97 kontrol geçti (exit 0). Bereket 42 + genel 97 = 139 kontrol.
Son çalışmalarda derleyici/shader hatası yok. Önceden bilinen Unity SearchDatabase başlangıç indeksleme
istisnası editör logunda bulunabilir. Genel rapor: GeneralRegression.txt.
