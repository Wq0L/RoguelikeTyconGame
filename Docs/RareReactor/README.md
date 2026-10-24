# Nadir Reaktör — üretim

Mor-altın küçük baklava işareti ve dört hafif açılış kıvılcımı. İlk aktivasyonda .24 sn
özgün enerji dolma sesi; yenilenmeler sessizdir. Mevcut Reactor varyantı (7) kullanılır.
ReactorIndicator=29 ve ReactorOn ses=24 sona eklenmiştir; önceki enum kimlikleri korunur.
Pulse yalnız Reactor varyantında bağlanır.

Mevcut kod/asset talep ile uyumludur: direct Rare+ hasat mevcut davranış şanslarını
4 saniye ×1,25 yapar. Yenilenme birikmez; sıfır davranışı açmaz, direct hasarı artırmaz.
RareReactorEffect IsActive/Remaining/Refreshes değerleri görselin tek durum kaynağıdır.
Bağımsız dört saniyelik görsel sayaç yoktur. Son .8 saniyede işaret küçülür; bitişte
.12 saniyelik sakin çıkış oynar. Round/restart/disable anında gösterge temizlenir.
Yenilenme aynı nesnede en sık .25 saniyede bir küçük nabız yapar; mekanik yenilenme
atlanmaz. Görsel local zaman yalnız nabız animasyonu içindir.

Açılış DirectHit.Point hasar öncesi snapshot’ından gelir; bitki Transform’u tutulmaz.
PlayerController yalnız CursorWorldPosition/CursorRadius salt okunur bilgisini sunar.
Reaktör halkadan yana .35 birim dışarı ve .65 yukarı yerleşir; altın Hasat Ritmi halkasını,
rengini, saldırı yarıçapını ve hedef noktasını değiştirmez. Tek gösterge ortak dekor
havuzundadır; kıvılcım da aynı havuzu kullanır. Yeni Update/coroutine/ışık/fizik yok.

Yeniden kullanılan: ThornToon/ThornInk, LateGameMeshBuilder, LateGameContent parçaları,
merkezi LateUpdate ve LateGameAudio (toplam 8 kaynak). Yeni özgün varlıklar:
ReactorCore/ReactorOpening prefab/meshleri ve ReactorCharge.wav (mono 22050 Hz PCM).
Ses düzeyi .26, tekrar .3 sn, klip başına 1 ses; uğultu veya yenilenme sesi yok.
Harici içerik, paket, shader veya materyal instance eklenmedi.

Değişen dosyalar:
- RareReactorEffectSO.cs: yalnız ilk cue konumu snapshot’a çevrildi.
- PlayerController.cs: mevcut imleç pozunun salt okunur sunum bilgisi.
- LateGamePresenter.cs / LateGamePresenter.Reactor.cs: havuz, gerçek süre, konum ve temizlik.
- LateGameArtSO.cs / Resources/LateGameArt.asset: yalnız Reactor bağları.
- Assets/Art/LateGame/RareReactor/: özgün varlıklar.
- Tools/Art/build-reactor-audio.py; Tools/Verification/Editor/ReactorArtBuilder.cs ve
  ReactorArtVerification.cs. Mevcut GameScene/Lab/ReactorCase/ReactorResult kullanılır.

## Hızlı Play
GameScene / Run50_GecOyunV1 → Tools > Geç Oyun Testi → Nadir Reaktör → Ver.
Ödül mevcut davranışlı saksı koşulunu korur; davranışlı saksı yerleştir. Rare veya daha
nadir bitkiyi kendi saldırınla hasat et. İmlecin yanında mor-altın işaret belirir.
Dört saniye içinde yeniden uygun hasat yapınca tek işaret yenilenir; bekleyince söner.
Hasat Ritmi hazır olduğunda altın halka ile birlikte kontrol et. Test alımı kayıt
 yazımını oturum için kapatır. Seçili profil ve açık ana editörün Play durumu değiştirilmedi.

Ayrıntılı görsel/işitsel insan Play kabulü bekliyor. Temsilci görüntü nihai onay değildir;
uzun bot denge koşusu veya uzun süreli bellek profili yapılmadı.

Kontrol yaklaşımı: mevcut ReactorCase Common/direct ve Rare/davranış reddini, Rare/direct
aktivasyonunu, ×1,25’i, sıfır davranışın korunmasını ve yenilenmenin katlanmamasını sınar.
ReactorResult yalnız reaktörün kendi modifier’larının kaldırılmasını kontrol eder. Son
süre bölümünü kaçırmamak için test etkinin kendi Tick yoluyla Remaining’i .3 saniyeye
ilerletir; ardından gerçek frame güncellemesi süreyi bitirir. Dört saniye sabiti asset’te
korunur; bu hızlandırılmış kontrol duvar saatinde dört saniye ölçümü olarak sunulmaz.

## Tamamlanma / doğrulama
Üretim ve katalog bağlantısı tamamlandı. İzole Unity derlemesi ve 14 hedefli kontrol geçti
(çıkış 0). Tek gösterge, yenilenme, Hasat Ritmi birlikteliği, pause, süre bitişi,
12 havuz kullanım döngüsü, restart/disable ve aktif durumda yeniden açılış sınandı.
Katalog farkı yalnız 2 Reactor görsel + 1 ses kaydıdır; önceki bağlar korunur. Önceki
ödül varlıkları, imleç görsel kodu, seçili profil ve denge asset’leri hash ile aynı:
BeforeHashes.json / ProtectedResult.txt. Commit yok; ana açık editörde Play işletilmedi.
Unity kendi SearchDatabase/IndexationOnStartup kodunda indeks istisnası kaydetti;
oyun/sunum kontrolleri başarılı. Temsilci normal kamera görüntüsü Reactor_NormalCamera.png.
Mesh incelemesinde ince kontur kabuğu yüzü örttüğü için çekirdek explicit koyu kenarlı
kapalı baklava katmanlarıyla düzeltildi; yeni materyal eklenmedi.
