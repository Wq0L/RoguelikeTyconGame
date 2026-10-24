# Hasat Yankısı — 12 Ekim 2026

## 24 Ekim 2026: his katmanları (aç/kapa, kullanıcı karşılaştıracak)

Kullanıcı "açıklayamadığım bir his eksikliği" dedi. Öncelikli şüpheliler 2-3-4 ayrı katman olarak eklendi; her biri
Tools > Geç Oyun Testi penceresindeki "Meteor hissi" bölümünden açılıp kapanır (varsayılan açık, kayda yazmaz):
- **2 · Hareket:** taş düşüş ekseni etrafında yuvarlanır (kızgın yüz önde kalır); alev izi saniyede 12 kare ayna kaynar,
  yumuşak dalgalanır ve hafifçe salınır; çarpma tacı salınır. Random yok.
- **3 · Darbe karesi:** taş hızlandıkça düşüş ekseninde uzar (0,45 sn'de ~1,3×), çarpınca basılır (0,55 → 0,3); çarpmanın
  ilk 0,05 sn'sinde krem-beyaz, mürekkep konturlu patlama yıldızı. Yerel; zaman durmaz (hit-stop değil).
- **4 · Tarla tepkisi:** alanın bir hücre ötesine kadar yaşayan bitkiler, uzaklıkla azalan güçte ve hücre başına 0,035 sn
  gecikmeyle esner (`PlantJuice.Shock`; yalnız model ölçeği, hasar yok). Halka çizilmeden yayılan dalga gibi okunur.
- **1 · Ses:** kullanıcı sorun görmedi; sona bırakıldı.
- Düşüş 0,5 sn (0,55 biraz yavaştı).
- Bellek ve performans: tarama çarpma başına bir kez (en çok ~7×7 hücre), bellek ayırmaz; parlama ağı bir kez üretilir
  ve kapanışta silinir; havuz sınırı 3.
- Kontrol: `HarvestEchoArtVerification` 42/42 (tarla tepkisi 15 bitki; parlama ve tarla tepkisi düğmeye uyuyor; 12 döngüde
  nesne sayısı sabit). Açık/kapalı karşılaştırma çekimi izole kopyada alındı. Tarla esnemesi zamana bağlı olduğu için
  durağan karelerde görünmez; Play'de değerlendirilmeli. Kullanıcının oynayarak karşılaştırması bekleniyor.

## 23 Ekim 2026 (üçüncü güncelleme): boş hücreye de çarpma, daha uzun düşüş

Kullanıcı geri bildirimi: boş hücreye düşen meteorun sönmesi kötü duruyordu, düşüş de çok hızlıydı.
- Hedef hücreler boş olsa da tam çarpma gösterilir (taç, toz, parçalar, krater, ses). Hiçbir şey ölmez: hasar olmayan hücreler
  "boşa giden" sayılır. Sönme yalnız sonuç hiç gelmezse (ör. etki kaldırıldı) kalır.
- Düşüş (mekaniğin gecikmesi) 0,2 → 0,55 sn. Değer `HasatYankisiEffect_G1.asset` ve `gec_oyun_v1_params.py` içinde aynı.
  Meteor daha yüksekten başlar; boyutu değişmedi (kullanıcı büyütme istemedi).
- Testler: `HarvestEchoArtVerification` 29/29 (boş hücrede tam çarpma ve hasarsızlık dahil). Ana testin yankı adımları
  0,7 sn beklemeyle geçti.

## 23 Ekim 2026 (ikinci güncelleme): kuzey ışığı meteoru görseli

Yer tutucu buz mavisi meteor kaldırıldı. Yerine Blender'da tasarlanan kuzey ışığı meteoru geldi (kullanıcı varyant A'yı seçti):
- Mavi yok (elektriğin rengi). Kuzey ışığı yeşili, zümrüt, krem çekirdek ve macenta uçlar var.
- Koyu mor kozmik taş; düşüş yüzü nane rengi, üstünde yeşil çatlaklar.
- Yerde halka ve gölge yok.

Ağlar: `AuroraMeteorArtSO` (`Resources/AuroraMeteorArt`). Kaynak: `Tools/Art/AuroraMeteor/`, önizlemeler `Docs/AuroraMeteor/`.

Sunum (`LateGamePresenter.Meteor.cs`):
- **Düşüş:** taş, katmanlı alev izi (kart, her karede kameraya çevrilir).
- **Çarpma:** alev tacı 0,06 sn'de açılır, 0,45 sn'de söner. Comic toz bulutları 1 sn'de büyüyüp yükselir ve söner. 6 taş parçası
  0,45 sn'de yere iner, 0,9 sn'de kaybolur.
- **Krater:** küçükten açılır, 1,1 sn kalır, 0,3 sn'de batar. Zemini alanla ölçülü büyür (3 hücre ~1,7×, 6 hücre ~2×); kenar
  blokları neredeyse aynı boyda kalır.
- Toplam çarpma sunumu 1,4 sn. Hasar zamanlaması değişmedi (0,2 sn düşüş = mekaniğin gecikmesi).
- İsabet yoksa meteor söner (sahte çarpma ve ses yok).
- Bellek ve performans: aynı anda en çok 3 meteor, havuzlu. Ağlar ve malzemeler asset; kare başına bellek ayrılmaz.

Kontrol: `HarvestEchoArtVerification` 29/29, izole kopyada gerçek akıştan 3 ve 6 hücre çekimi. İnsan Play ve his ayarı yapılmadı.
His ayarında karar bekleyenler: düşüş süresi (0,2 sn ya da 0,5–0,6 sn), kamera sarsıntısı, ses.

## 23 Ekim 2026 güncellemesi: tek meteor (mekanik + YER TUTUCU görsel)

Kullanıcı kararları (23 Ekim): yıldız kayması rengi (B), 6 sn bekleme, 1+ hasatta tetik, en çok 6 hücre, 0,2 sn düşüş,
hasar yazısı normal (krem). Asıl toon VFX Blender'da ayrıca yapılacak; şu anki görsel yer tutucudur.

Mekanik (`HarvestEchoEffectSO`, sayılar `HasatYankisiEffect_G1.asset` ve `gec_oyun_v1_params.py` içinde aynı):
- Tek oyuncu saldırısında 1+ doğrudan hasat ve bekleme dolmuşsa, kesilen hücrelerin açık alan merkezine göre simetriğine
  TEK meteor planlanır. İlk 6 hasadın yansıması alandır; saldırının kendi alanına düşen ya da tekrarlanan yansıma atlanır.
  Alan boşsa meteor yok, bekleme başlamaz.
- 0,2 sn sonra alandaki her hücreye aynı anda vurur: hasat hasarı stat'ının %50'si (hücre başına). Uygunluk vuruş anında.
- Meteor planlanınca 6 sn (round saati, pause'da durur) yeni meteor yok; her round hazır başlar. Meteor havadayken gelen hasat
  da bekleme reddi sayılır.
- Eski düzen (3+ hasat, her hücreye ayrı yankı, en çok 3, 0,12 sn) kaldırıldı.

Görsel (`LateGamePresenter.Meteor.cs`, yer tutucu):
- Buz mavisi düşük poligonlu taş, koyu mavi gölge, mürekkep kontur, mavi-beyaz iz.
- Boy kesilen hücre sayısıyla yavaş büyür (6 hücrede 1,5 kat); alanı mavi halka gösterir.
- Gecikme içinde ivmelenerek düşer. İsabet varsa çarpma (halka, beyaz yıldız, 5 kırıntı, mevcut tok sesi). Hiç isabet yoksa
  meteor yerde söner, ses yok.
- Eski lila motif ve kaynak izi katalogda bağlı kaldı ama artık kullanılmıyor.
- Bellek ve performans: havuzlu (aynı anda en çok 3); ağlar bir kez kurulur, malzeme Kritik Ark'la ortak; kare başına bellek
  ayrılmaz; pause'da durur; round dışında, restart'ta ve kapanışta temizlenir.

Kontrol (izole kopya):
- `HarvestEchoArtVerification` 29/29 PASS. Tek meteor, alan merkezi, pause, gerçek isabette çarpma, boş hedefte sönme
  (sahte çarpma ve ses yok), restart temizliği; 12 döngüde her seferinde gerçek çarpma ve sabit nesne sayısı.
- `GecOyunV1Verification` yankı adımları (EchoCase, EchoResult, EchoCap, EchoCapResult) ayrıca çalıştırıldı ve geçti:
  3 hasat → tek meteor, 3 hücre, 50 hasar; bekleme reddi; merkez yansıması atlanır; tek hasat → 1 hücre; 9 hasat → 6 hücre.
- Tam `GecOyunV1Verification` takımı yankıya gelmeden Hilal Darbesi adımında duruyor. Sebep: test eski Hilal davranışını
  bekliyor, Hilal ise kullanıcının commitlenmemiş Moon değişiklikleriyle değişti. Bu adım kullanıcı kararını bekliyor.
- Kritik Ark 15/15, Taşan Hasar 21/21 tekrar geçti.
- İnsan Play testi yapılmadı.

Not: `make_gec_oyun_v1.py --check` bu değişiklikten önce de "araç dışında düzenlenmiş veri" uyarısı veriyordu (Hilal,
Büyüyen Ay, Dolunay, havuz, profil). Hasat Yankısı değerleri elle, iki yerde aynı yazıldı; üretici uzlaştırması ayrı iş.

---
Yalnız mevcut Hasat Yankısı sunumu; ayrıntılı görsel/işitsel insan Play kabulü sonraya bırakıldı.
Kod ve güncel asset’ler talepteki mekanikle uyumlu; eski açıklamaya uydurmak için değişiklik yapılmadı.

## Görsel / ses
Kaynakta iki kısa mor-lila comic kavis (.12 sn), hedefte aynı motifin açık lila/krem
kopyası, küçük krem temas yıldızı ve üç küçük parça (.16 sn). İnce koyu toon kenar;
ışın, elektrik, mermi, bulut, yeni hasar sayısı veya UI yok. Motif .8 ölçekle bitkinin
kök noktasının .6 birim üstünde çizilir, merkezi sunum adımında hızla incelip söner.

Kaynak izi yalnız Schedule başarılı olduktan sonra çıkan HarvestEchoPrepared cue’sunu
okur. Kaynak DirectHit.Point snapshot’ıdır, hedef saldırı anındaki açık alan merkezine
göre aynalanan hücrenin konumudur. İzi Transform’a bağlamaz. Hedef motifi yalnız gerçek
StrikeResult.Hit sonrası mevcut HarvestEcho cue’sundan çıkar. Boşalan hedefte motif,
temas veya ses yok; kaynak izi normal ömrünü tamamlar. Bitki hit tepkisi tekrar çağrılmaz.

Tok sesi özgün sentez; .22 sn mono 22050 Hz PCM, iki çok kısa ve hafif ses tekrarı.
Hazırlık sessiz. Ses düzeyi .28, tekrar aralığı .12 sn, klip başına tek ses; ortak
LateGameAudio toplam sekiz kaynak sınırı korunur. Üç aynı-kare isabet tek ses çalar.

## Korunan mekanik
Tek direct saldırıda en az 3 hasat; en çok 3 yansıma, gecikme .12 saniye, hasat hasarı
statının %50’si. Açık tarla merkezi: (minX+maxX−x, minZ+maxZ−z). Direct kapsanan hücreler
atlanır. Gecikme sonrası canlı/uygun hedef kontrolü mevcut Strike yolundadır. Yeni yankı,
kritik, davranış zinciri, hasar tekrarı veya gecikme yok. Hedef sırası, sayı, ödül aşaması,
bedel ve dışlama alanları değişmedi.

## Paylaşım ve dosyalar
Yeniden kullanılan: ThornToon/ThornInk, LateGameMeshBuilder ve mevcut toon parça üretim
biçimi; LateGameContent kırıntı hareketi/reseti; presenter dekor havuzu ve LateGameAudio.
Yeni özgün varlıklar: EchoPreparation/EchoImpact prefabları, mor/lila motif, krem yıldız
ve kırıntı mesh’leri, EchoTok.wav. Yeni shader/materyal, fizik, ışık, Update/coroutine yok.
Her hazırlık/isabet tek havuz nesnesidir; ortak 96 dekor sınırı korunur.

HarvestEcho varyantı (5) korunur. Pulse yalnız bu varyantta atanır. Sona eklenen kimlikler:
HarvestEchoPreparation slot=28, HarvestEchoPrepared cue=26, HarvestEcho sound=23.
Kritik Ark, Taşan Hasar ve diğer Pulse bağlantıları değişmez.

Değişen dosyalar:
- Assets/Scripts/RunEffects/Effects/HarvestEchoEffectSO.cs: yalnız başarılı planlama cue’su.
- Assets/Scripts/RunEffects/RunEffects.cs: sona eklenen cue kimliği.
- Assets/Scripts/LateGameArt/LateGamePresenter.cs: hazırlık/isabet/ses dalları.
- Assets/Scripts/LateGameArt/LateGameArtSO.cs: slot/ses kimlikleri.
- Assets/Resources/LateGameArt.asset; Assets/Art/LateGame/HarvestEcho/.
- Tools/Art/build-harvest-echo-audio.py; Tools/Verification/Editor/HarvestEchoArtBuilder.cs,
  HarvestEchoArtVerification.cs. Mevcut GameScene ve GecOyunV1Verification Lab/EchoCase/
  EchoResult kontrolleri kullanılır; yeni test sahnesi veya genel test framework’ü yok.

## Hızlı deneme
GameScene / Run50_GecOyunV1’de Play → Tools > Geç Oyun Testi → “Hasat Yankısı” → Ver.
Tarlanın bir köşesine yakın, tek saldırıyla en az üç bitki hasat et. Açık alanın simetrik
karşı tarafında canlı bitkiler bırak. Kaynakta kısa mor iz; .12 saniye sonra karşı tarafta
lila/krem darbe ve tok sesi görülür/duyulur. Merkezde zaten vurduğun hücreler tekrar seçilmez.
Karşı hücreler boşsa yalnız kaynak hazırlığı görülebilir. Test alımı kayıt yazımını Play
oturumu için kapatır; seçili profil değiştirilmez.

Temsilci görüntü HarvestEcho_NormalCamera.png nihai insan Play onayı değildir. Uzun bot
koşusu, farklı ekran oranları/kameralar ve dinleyerek kapsamlı ses kabulü bu turda yapılmadı.

## Sonuç
Üretim ve oyun içi katalog bağlantısı tamamlandı. İzole Unity derlemesi/çıkış kodu 0;
15 hedefli kontrol geçti (HarvestEchoArtVerification.txt). Mevcut EchoCase/EchoResult
mekanik kontrolleri, doğru simetri ve isabet bildirimi, planlama sonrası boşalan hedef,
üç isabette tek ses, kareler arasında pause, round/restart temizliği ve 12 tekrar kullanımda
sabit havuz nesne sayısı kontrol edildi. Temsilci görüntü normal oyun kamerasından alındı.
Katalog farkı yalnız 2 Hasat Yankısı görseli ve 1 ses kaydı; önceki kayıtlar korunur.
Korunan dosyaların hash kontrolü ProtectedResult.txt / BeforeHashes.json dosyalarındadır.
Unity kendi SearchDatabase/IndexationOnStartup kodunda indeks istisnası kaydetti;
oyun/sunum kontrol sonuçları başarılı. Uzun süreli bellek profili yapılmadı.
