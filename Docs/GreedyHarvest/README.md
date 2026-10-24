# Açgözlü Hasat — üretim

Üç küçük amber biçme dişi: koyu kenar, krem vurgu ve kameraya dönük kavisli siluet.
İmleç merkezinin çevresinde .12 sn açılır, sakin durur. Gerçek güç kullanımında tek bir
.16 sn kapanış saldırının snapshot merkezine doğru küçülür. Tek hedefte kayıp .10 sn
sessiz çıkıştır; boş saldırıda mevcut nesne korunur. Saldırı yarıçapı halkası değişmez.

GreedyHarvestEffect.Ready ve mevcut GreedReady/GreedSpent/GreedLost bildirimleri tek
kaynak. Sunum hasat saymaz, hasar uygulamaz, hak vermez/tüketmez. Oynanış kaynak ve
ayar asset'leri değiştirilmedi: tek güçlendirilmemiş direct saldırıda 3+ hasat;
2+ canlı hedefte ×1,50 ve harcama; tek canlıda normal hasar ve kayıp; boşta koruma.
Güçlü saldırı tekrar hak vermez. AttackFeed yalnız direct hasatları raporlar.

Güncel kodda hak round sonunda silinmiyor; bu mevcut kural korundu. Görsel round dışında
hemen temizlenir; yeni aktif round'da gerçek hak hazırsa yeniden gösterilir. Ödülün
kaldırılması hakkı zaten sıfırlar. Pause görsel açılışı dondurur. Disable/havuz reseti
ışık, materyal veya event dinleyicisi eklemez; merkezi yaşam döngüsü kullanılır.

Yeni özgün varlıklar: GreedTeeth prefab/mesh, GreedReady.wav (.16 sn), GreedSpent.wav
(.13 sn). Sesler dış örneksiz Python standart kütüphanesiyle sentez, mono 22050 Hz PCM.
Düzey .25, tekrar aralığı .18 sn, klip başına 1 ses; merkezi toplam 8 kaynak sınırı.
Kaybetme/boş saldırı sesi yok. Sesler yalnız gerçek bildirimde bir kez istenir.
Yeniden kullanılan: proje için özgün ThornToon, LateGameMeshBuilder, LateGameContent,
merkezi LateGamePresenter havuzu/güncellemesi ve LateGameAudio. Yeni shader/paket yok.
Mesh 360 üçgen / 1080 flat-shaded vertex, tek renderer ve paylaşılan materyal.
Hazırda tek nesne; kullanma geçişi tek nesne. Yeni Update/coroutine/fizik/dinamik ışık yok.

Greed varyantı 8 korunur. GreedIndicator=30 ve GreedSpent ses=25 enum sonuna eklendi.
Mevcut GreedReady ses=13 kullanılır. Katalog yalnız GreedIndicator/Greed, Pulse/Greed,
GreedReady/Greed ve GreedSpent/Greed kayıtlarını ekler. Diğer Pulse bağları değişmez.
Hasat Ritmi altın halkası, Nadir Reaktör ve mor zincir bildirimi yeniden tasarlanmadı.

## Değişen dosyalar
- Assets/Scripts/LateGameArt/LateGamePresenter.Greed.cs: salt okunur gerçek durum,
  imleç pozu, hazır gösterge ve tek harcama hareketi.
- Assets/Scripts/LateGameArt/LateGamePresenter.cs: mevcut cue yönlendirme, merkezi
  LateUpdate/disable bağları ve yalnız Greed kapanışında üçüncü eksende küçülme.
- Assets/Scripts/LateGameArt/LateGameArtSO.cs; Assets/Resources/LateGameArt.asset.
- Assets/Art/LateGame/GreedyHarvest/ (prefab, mesh, iki ses ve Unity meta dosyaları).
- Tools/Art/build-greed-audio.py; Tools/Verification/Editor/GreedArtBuilder.cs ve
  GreedArtVerification.cs. Bunlar mevcut izole GameScene test yolunu kullanır.
- Bu belge/kanıtlar, içerik bağlama, TODO ve lisans envanteri.

## Hızlı insan Play kontrolü
GameScene / Run50_GecOyunV1 → Tools > Geç Oyun Testi → Açgözlü Hasat → Ver.
Mevcut test alımı oturumda kayıt yazımını kapatır; gerçek profil değiştirilmez.
1. Tek normal saldırıyla üç bitki hasat et: imleçte üç amber diş ve hafif hazır sesi.
2. Boşluğa vur: dişler ve hak kalsın. En az iki canlı bitkiye vur: tek kapanış ve biçme sesi.
3. Yeniden hazırla, tek canlıya vur: normal hasar, sessiz sönme.
4. Hasat Ritmi ve Nadir Reaktör'ü mevcut pencereden al; ritmi hazırla ve Rare+ direct
   hasatla reaktörü aç. Altın halka, mor-altın çekirdek ve üç diş birlikte seçilsin.
5. Pause, round geçişi ve restart'ta artık efekt/ses kalmadığını kontrol et.

Üretim ve gerçek ödül bağlantısı tamamlandı; ayrıntılı görsel/işitsel insan kabulü bekliyor.
Temsilci normal oyun kamerası görüntüsü nihai Play onayı değildir. Uzun bot denge koşusu
ve uzun süreli bellek profili yapılmadı. Diğer ödüllere geçilmedi; commit yok.

## Kontrol sonuçları
İzole Unity 6000.3.14f1 derlemesi ve 27 hedefli kontrol geçti (çıkış 0).
GreedArtVerification.txt: mevcut Greedy mekanik regresyonu, tek gösterge, boş saldırı,
çoklu kullanım/gerçek merkez, tek hedefte kayıp, üç gösterge birlikteliği, pause, disable,
12 round/havuz temizliği döngüsü ve ödül kaldırma. 27 sayısına 12 döngünün ayrı sonuçları
dahildir. Nesne sayısı sabit kaldı; bu uzun süreli bellek sızıntısı garantisi değildir.
Katalog farkı yalnız 2 Greed görsel + 2 Greed ses kaydı. Önceki ödül varlıkları, seçili
profil, Greedy mekanik/ayar dosyaları ve korunacak imleç kodu aynı hash değerlerinde
(BeforeHashes.json / ProtectedResult.txt). Ana açık editörde Play işletilmedi.
Unity Editor SearchDatabase/IndexationOnStartup içinde bilinen indeks istisnası kaydetti;
oyun/sunum kontrolleri başarılı. Bu altyapı uyarısı bu görevin dışında bırakıldı.
Greed_NormalCamera.png normal oyun kamerasında üç göstergenin temsilci karesidir.
