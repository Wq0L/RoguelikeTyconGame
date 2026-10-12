# Taşan Hasar — 12 Ekim 2026

Yalnız mevcut ödülün sunumu. Üretim tamamlandı; ayrıntılı görsel/işitsel insan Play kabulü bekliyor.

## Sunum ve mekanik
Turuncu-altın sivri enerji parçası, krem merkez ve koyu toon kenar; kısa yerel iz.
İlk aktarım .14 sn; devamı .105 sn, enine .65 ve iz yönünde .5 ölçek. Boyut hasar
sayısından türetilmez. Parça dünya mesafesine uzatılmaz; iki sabit konum arasında ilerler.
Hedefte amber/krem yıldız ve 3 küçük parça gerçek isabet anında görünür. Hasar zaten
uygulanmıştır; görsel hareket hasar/ölüm/ödül zamanlamasını bekletmez, hit tepkisini tekrarlamaz.

Mevcut mekanik değişmedi: gerçek overkill’in %50’si, 1 hücre menzil, en çok 2 aktarım,
saldırı başına en çok 4 kaynak. Devamı önceki hedefin gerçek kalan overkill’inden doğar.
Ödül/effect değerleri, hedef tarama sırası, aşama, dışlama ve bedeller değişmedi.

OverflowEffect yalnız başarılı Strike sonrasında cue üretir. İlk kaynak DirectHit.Point
(hasar öncesi snapshot), devam kaynağı önceki StrikeResult.Point’tir. RewardCue.TransferStep
sunum için 0/1 taşır; Value hâlâ hasardır. Transform/bitki referansı tutulmaz. Çizim iki
uçta da .65 birim yukarıda, saksı kenarının üzerindedir. Gerçek uygun hedef yoksa efekt yok.

Overflow varyantı (11) korunur; ArcBolt ve AntennaContact yalnız bu varyantta bağlanır.
Kritik Ark (18), varsayılan ArcBolt ve diğer katalog kayıtları değişmez. Yeni ses enum’u
sona Overflow=22 olarak eklenir; eski serialized kimlikler korunur.

## Varlıklar ve maliyet
Yeniden kullanım: ThornToon/ThornInk, ContactDouble yıldız mesh’i, LateGameMeshBuilder,
LateGameContent kırıntı hareketi, presenter havuzu/Update, sekiz kaynaklı LateGameAudio.
Yeni: OverflowShard prefab/mesh, OverflowContact prefab ve üç küçük kırıntı mesh’i;
özgün OverflowFup.wav (mono 22050 Hz, .16 sn). Ses yalnız ilk aktarımda istenir;
volume .32, repeat .12 sn, klip başına 1 voice. Harici örnek/varlık yok.
Yeni Update, coroutine, fizik, ışık, shader, materyal instance veya genel framework yok.
Her aktarım iki ortak havuz nesnesi kullanır; dekor bütçesi 96. Yeniden kullanımda
hareket bayrağı, konumlar, yaş, ölçek ve kırıntılar sıfırlanır.

## Değişen dosyalar
- Assets/Scripts/RunEffects/Effects/OverflowEffectSO.cs: snapshot ve hop bildirimi.
- Assets/Scripts/RunEffects/RunEffects.cs: yalnız RewardCue.TransferStep alanı.
- Assets/Scripts/LateGameArt/LateGamePresenter.cs ve LateGamePresenter.Overflow.cs:
  merkezi havuz nesnesinin sunum hareketi; gerçek isabette temas/ses.
- Assets/Scripts/LateGameArt/LateGameArtSO.cs: yeni ses enum kimliği.
- Assets/Resources/LateGameArt.asset ve Assets/Art/LateGame/Overflow/.
- Tools/Art/build-overflow-audio.py; Tools/Verification/Editor/OverflowArtBuilder.cs,
  OverflowArtVerification.cs. İzole mevcut GameScene/Lab kullanılır, yeni test sahnesi yok.

## Hızlı deneme
GameScene / Run50_GecOyunV1’de Play → Tools > Geç Oyun Testi → “Taşan Hasar” ara → Ver.
Komşu canlı bitkiler bırak. Küçük saldırı alanıyla bir bitkiye kalan canından daha fazla
hasar ver. Artan hasar komşuyu da öldürürse ikinci, daha küçük aktarımı görürsün.
Bitki ölmezse, artan hasar yoksa veya uygun komşu yoksa aktarım görünmez.
Test penceresi kayıt yazımını oturum için kapatır. Profil seçimi değişmez.

Normal kamera görüntüsü Overflow_NormalCamera.png temsilcidir, nihai insan Play onayı değil.
Uzun denge koşusu, kapsamlı kamera/ekran oranı ve dinleyerek ses miks kabulü yapılmadı.

## Temel doğrulama
İzole Unity derlemesi ve hedefli test başarılı (çıkış 0). Rapor 17 PASS satırı içerir;
ortak regresyon başlığı iki kez yazıldığı için 16 farklı kontrol vardır. Mevcut Kritik
Ark ve Overflow mekanik assert’leri de çalıştırıldı. Gerçek iki kaynak/hedef, hop,
sınırlı boyut/ömür, hareket, tek ses sınırı, 12 yeniden kullanım döngüsü, aktarım yokluğu,
round/restart, kareler arasında pause ve süre sonunda havuza dönüş kontrol edildi.
Kritik Ark/Anten varlıkları, Kritik Ark mekanik kodu, seçili profil ve Taşan Hasar mekanik
asset hash’leri değişmedi (BeforeHashes.json / ProtectedResult.txt). Katalog yalnız
2 Overflow görsel + 1 ses kaydı ekledi; diğer bütün kayıtlar aynı kaldı.
Unity kendi SearchDatabase kapanış yolunda indeks istisnası kaydetti; oyun/sunum testleri
başarılı. Bu sonuç mutlak bellek sızıntısızlığı veya nihai görsel/işitsel kabul iddiası değildir.
