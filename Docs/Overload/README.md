# Aşırı Yükleme — üretim

## Sunum
Gerçekten kabul edilen işte kaynak konumunda küçük krem çekirdek ve iki koyu kenarlı
amber zikzak belirir. Gerçek işin Host.Clock ilerlemesinden iki kısa sıkışma/genişleme
üretilir. Başarılı Execute bildirimi geldiğinde parçalar dışarı açılıp .16 sn içinde
söner. İptalde hazırlık .09 sn sessizce çekilir; başarı sesi/boşalma yok. Kaynak saksı
geçersiz olunca bir sonraki merkezi sunum güncellemesinde hazırlık ayrılır. Konum
job.position snapshot'ıdır; bitki/saksı Transform'u takip edilmez, yerine yerleşen nesneye
atlamaz. Satış mevcut CleanupPlacement ile IsPlaced/ownership bilgisini hemen kapatır.

Patlama, elektrik, kasırga ve bumerangın mevcut normal/tekrar görselleri, yolları ve
hitbox'ları aynen kullanılır. Kaynak vurgusu yeterli olduğu için davranışlara ek renk
katmanı eklenmedi. Normal patlamadaki mevcut yazılı efekt de bu görevin değişikliği değildir.
Mor zincir bildirimi ve Hasat Ritmi imleci korunur.

## Gerçek mekanik bağı
Güncel kod ve asset talep ile uyumlu: gerçekten başlayan normal direct-hasat davranışı;
.30 sn sonra normal davranış hasarının ×.50'si; saksı/davranış başına 2.50 sn bekleme;
kök saldırı başına en çok 3 kabul. Yeni şans zarı yok. Execute, kapasite sınırları, RNG
Fork çağrıları, link/hasar hesapları, zamanlayıcı ve iptal koşulları değiştirilmedi.

Job'a yalnız hazırlanma zamanının sunum snapshot'ı eklendi. Salt okunur TryPreparation
geçerliliği ve gerçek saatten ilerlemeyi verir. OverloadPrepared yalnız başarılı Schedule
ve iş kaydından sonra; OverloadCancelled yalnız Execute başarısızlığından sonra bildirilir.
Mevcut Overload başarı bildirimi korunur. Gözlemci bildirimi mevcut ObserverEvents üzerinden
izole edilir; görsel/ses hataları oyun işlemini kesmez, Execute hataları gizlenmez.
Sunum kendi .30 saniyelik zamanlayıcısıyla davranış başlatmaz. Görsel havuz sınırı mekanik
kabul/tekrar sonucunu etkilemez. Mevcut gerçek hasar tepkileri ikinci kez çağrılmaz.

Overload varyantı=9 ve başarı ses=12 korundu. OverloadPreparation slot=31,
OverloadPrepared ses=26, OverloadPrepared/OverloadCancelled cue=27/28 sona eklendi.
Mevcut enum kimlikleri kaymadı. Katalog yalnız bu varyanta iki görsel ve iki ses ekler;
ortak Pulse/default ve diğer ödüller değişmez.

## Varlıklar / performans
Yeni özgün OverloadCharge/OverloadRelease prefabları aynı üç küçük mesh'i paylaşır.
Bir işaret 152 üçgen / 456 flat-shaded vertex / 3 renderer, ortak ThornToon materyali.
Geometri LateGameMeshBuilder ile üretildi; LateGameContent Show/Hide/fragment hareketi,
merkezi presenter havuzu/LateUpdate ve LateGameAudio yeniden kullanıldı. Yeni Update,
coroutine, manager, fizik, dinamik ışık, shader/paket veya materyal instance yok.
Hazırlık listesi mevcut 16 işlik mekanik sınırına sabitlenmiştir. Round, run, disable ve
owner değişiminde temizlenir; tekrar kullanım gerçek iş durumundan başlar. Pause gerçek
Host.Clock'u dondurduğu için iki vuruşun pozu da donar.

Mevcut ödül sesleri değerlendirilerek farklı, düşük seviyeli iki özgün sentez üretildi:
OverloadCharge.wav (.16 sn, ses .16, tekrar .25 sn), OverloadRelease.wav (.11 sn,
ses .18, tekrar .16 sn). İkisi mono 22050 Hz PCM; klip başına 1 ve merkezi toplam 8 ses
sınırı. Normal davranış sesleri korunur; iptal sessiz. Mevcut merkezi ses yolunun atanmış
mixer grubu yok; bu pakette genel routing/mixer sistemi değiştirilmedi.
Dış örnek, üçüncü taraf içerik veya araç kurulumu yok.

## Dosyalar
- Assets/Scripts/RunEffects/Effects/OverloadEffectSO.cs: yalnız hazırlık/cancel bildirimi
  ve salt okunur gerçek iş görünümü.
- Assets/Scripts/RunEffects/RunEffects.cs: enum sonuna iki dar cue.
- Assets/Scripts/LateGameArt/LateGamePresenter.cs / LateGamePresenter.Overload.cs:
  yönlendirme, merkezi hazırlık pozu ve yaşam döngüsü.
- Assets/Scripts/LateGameArt/LateGameArtSO.cs; Assets/Resources/LateGameArt.asset.
- Assets/Art/LateGame/Overload/ ve meta dosyaları.
- Tools/Art/build-overload-audio.py; Tools/Verification/Editor/OverloadArtBuilder.cs,
  OverloadArtVerification.cs. Mevcut izole GameScene test altyapısı kullanılır.
- Bu rapor/kanıtlar, içerik bağlama, lisans kaydı ve yalnız ilgili TODO maddesi.

## Doğrulama
İzole derleme ve 77 kontrol başarılı. Sayıya kapasiteyi dolduran 22 başarılı kurulum
adımı ve 12 round temizliği döngüsü dahildir. Mevcut OverloadTake/Result/Limits/Result
regresyonları .50 hasarı, cooldown'ı, hareketli sınırı ve zincir dışlamasını doğrular.
İlk denemede özgün testin adımlar arası cooldown beklemesi atlanmıştı; test akışı
2.6 oyun saniyesi bekleyecek şekilde düzeltildi. Mekanik süreler değiştirilmedi.

Ek kontroller: dört gerçek normal davranıştan kabul/gerçek başarı; iki hareketli davranışın
12 kapasitede iptali; aynı kökte dört uzak kaynak hasadından yalnız üç kabul; görsel havuzu
96 doluyken gerçek tekrar; saksı satışı ve sessiz iptal; pause; yeniden açma; 12 round/havuz
döngüsünde sabit nesne sayısı. Bekleme/bütçe reddinde hazırlık bildirimi yok. Uzun denge
veya bellek sızıntısı profili yapılmadı. Yeni test sahnesi yok. Ayrıntılar
OverloadArtVerification.txt, ProtectedResult.txt ve CatalogBefore.txt içinde.

## Hızlı insan Play
GameScene / Run50_GecOyunV1 → Tools > Geç Oyun Testi → Aşırı Yükleme → Ver.
Davranışlı saksıyı kendi vuruşunla hasat et; normal davranış gerçekten tetiklenince küçük
amber hazırlık, .30 sn sonra mevcut davranışın tekrarı ve kısa boşalma görünmeli.
Patlama/elektrik/kasırga/bumerangı ayrı dene; aynı kaynağı hızla tekrar hasatta cooldown'ı,
hazırlıkta satış ve pause'u gözle. Test alımı oturumda kayıt yazımını kapatan mevcut debug
yoludur. Açık ana editörde Play işletilmedi; seçili profil ve gerçek kayıt değiştirilmedi.

Üretim tamamlandı; ayrıntılı görsel/işitsel insan Play kabulü bekliyor.
Overload_NormalCamera.png temsilci normal kamera karesidir; nihai polish/onay değildir.
Artçı Patlama veya başka ödüle geçilmedi. Commit yok.

Son derleme/test çıkışı 0; 77 kontrol başarılı. Korunan 86 dosya hash ile karşılaştırıldı
(sayı için ProtectedResult.txt esas alınır); önceki görsel varlıklar, davranış yöneticileri,
ayar asset'leri ve imleç kodu değişmedi. Katalog farkı yalnız iki Overload görsel ve iki ses
kaydıdır. Unity Editor SearchDatabase/IndexationOnStartup içinde mevcut indeks istisnası
tekrar görüldü; C# derlemesi ve oyun kontrolleri başarılı. Bu altyapı sorunu kapsam dışıdır.



## B5 — toon “dolan tekrar oku” rozeti (24 Ekim 2026)

Eski krem çekirdek + amber zikzak işareti ve boşalma nabzı artık kullanılmıyor (katalog bağları ve dosyaları duruyor).
Prototip turları ve videolar `Docs/Overload/b5/`; kullanıcı seçimi: tasarım 4 (dolan tekrar oku), sıra B (rozet en üstte), fuşya.

- Bulgular: patlamanın bulutu kaynağı 0,3 sn boyunca tamamen kapatıyor (eski işaret ve saklı hıçkırık görünmüyordu);
  patlama hasarının yazısı yok (yazı rengi önerisi yalnız elektrik / kasırga / bumerangda işe yarar, uygulanmadı);
  bugünkü oyunda patlama parçacıkları (sorting order 1–2) hasar yazılarının (0) üstüne çiziliyor.
- Sunum: `Assets/Scripts/LateGameArt/LateGamePresenter.Overload.cs`. Rozet işin anlık konumunun 0,7 üstünde, bakış ekseninde
  4 birim kameraya doğru (ortografik: ekrandaki yer aynı) ve derinlik yazar: bulut ve yazı arkasında kalır. Ok, işin
  gerçek ilerlemesiyle 290° ↻ olarak dolar; tekrar gerçekten çalışınca .14 sn kıvılcımlı pop. İptalde sessizce kalkar.
- Toon: sabit dalgalı kenar (Random yok), mürekkep kontur + sticker gölgesi, iki tonlu krem disk, okta koyu iç şerit ve krem parlama.
- Optimizasyon: ağlar çalışma anında bir kez (30 dolum basamağı + pop; paylaşımlı köşe, Color32, CPU kopyası bırakılır) ve
  OnDestroy'da silinir. Kartlar havuzlu: iş yuvası başına bir rozet (16), pop halkası 16 (dolunca en eskisi yazılır, sayılır).
  Karede yalnız transform + hazır ağ ataması; 300 senkron 0 bayt (ölçüldü). Dekor sınırından bağımsız.
- Doğrulama: `Tools/Verification/Editor/OverloadArtVerification.cs` 84/84 (mekanik regresyon dahil). Kalabalık sahne (12 davranışlı
  saksı, 121 eşzamanlı hasar yazısı, 9'a kadar rozet) gerçek kodla çekildi: `b5/B5_OyunKodu_Kalabalik_*`.
- Oyunda kullanılan içerik tamamen özgün kod; dış varlık, font, shader veya paket yok.
