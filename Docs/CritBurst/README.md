# Kritik Patlama — üretim

## Sunum ve mekanik
Gerçek saldırı merkezinde krem-beyaz çekirdek (.05 sn), koyu kenarlı sarı-turuncu sekiz
uçlu comic yıldız (.22 sn), altı küçük parça (en geç .32 sn). Kısa özgün mono darbe sesi
(.19 sn), alan tetiklemesi başına tek istek. Gerçekçi ateş/duman, yazı, halka, ışık veya
hedef başına yeni temas efekti yok. Normal patlama davranışının görünümüne dokunulmadı.

Kod/asset sözleşmeyle uyumlu: 3 farklı direct kritik yeterli; ölüm gerekmez; saldırı
başına bir patlama. Hasat hasarı statı × kritik çarpanı × .50; doğrudan saldırı çarpanları
ayrıca eklenmez. Hedef sırası, temas kuralı, hasar ve tetikler değiştirilmedi.
CritBurstEffectSO'da mevcut yarıçap ifadesi yerel değişkene alındı; aynı değişken hem
mevcut grid sorgusuna hem RewardCue.Radius snapshot'ına gider. Yeni hedef taraması yok.
From gerçek saldırı merkezi, yatay görsel ölçeği gerçek yarıçap. Prefab'ın uçları birim
yarıçapa göre yazıldı; görünürlük için yalnız .65 dünya birimi dikey kaldırılır. Hücreye
teğet temas kuralı korunur; yıldız uçları bütün hasarlı hücreyi kapladığı anlamına gelmez.

Host.Strike zaten yalnız canlı hedefe PlantHealth.TakeDamage çağırır; mevcut hit flash,
parçacık ve OnDamaged/PlantJuice tepkisi orada çalışır. Yeni sunum bunları tekrar çağırmaz,
Transform takip etmez veya hasar sayısı eklemez. Ekran görüntüsündeki sayılar mevcut
hasar sisteminindir. Üç kritik hedef direct saldırıda ölse bile gerçek tetik alan darbesi
olarak görünür; boş hücrelere ayrı başarılı isabet veya hedef sesi eklenmez.

## Yeniden kullanım / maliyet
Mevcut CritBurst varyantı=6, Pulse slot=15 ve CritBurst sound=11 korundu; enum eklenmedi.
Katalog farkı yalnız Pulse/CritBurst ve CritBurst/CritBurst ses kaydıdır. Önceki ödül
bağları aynıdır. Mevcut özgün ThornToon materyali, LateGameMeshBuilder, LateGameContent
parça hareketi, merkezi LateGamePresenter güncellemesi/havuzu ve LateGameAudio paylaşıldı.
Yeni CritBurstImpact prefab, yıldız/çekirdek/6 küçük mesh ve CritBurstThump.wav üretildi.
Toplam 656 üçgen, 1968 flat-shaded vertex, 8 renderer; materyal örneği, fizik, yeni
Update/coroutine veya dinamik ışık yok. LateGameCritBurst yalnız merkezi yaşla aşamaları
pozlar; ayrı saat tutmaz. Havuz resetinde çekirdek/yıldız görünürlüğü ve parça ölçeği döner.

Ses özgün Python standart kütüphane sentezi, mono 22050 Hz PCM; düzey .28, tekrar .16 sn,
klip başına 1 ses ve mevcut toplam 8 kaynak sınırı. Merkezi ses yolunda mevcut atanmış
mixer grubu bulunmadığından yeni mixer/routing kurulmadı. Diğer seslerin yönlendirmesi
korundu. Harici içerik/araç/paket kullanılmadı.

## Değişen dosyalar
- Assets/Scripts/RunEffects/Effects/CritBurstEffectSO.cs: gerçek yarıçapı cue'ya taşıma.
- Assets/Scripts/RunEffects/RunEffects.cs: RewardCue'ya isteğe bağlı Radius snapshot'ı.
- Assets/Scripts/LateGameArt/LateGamePresenter.cs: yalnız CritBurst yönlendirme, gerçek
  alan ölçeği ve bu prefab'ın aşamalarını genel flash küçülmesinden ayrı işletme.
- Assets/Scripts/LateGameArt/LateGameContent.cs; LateGameCritBurst.cs: merkezi pose/reset.
- Assets/Resources/LateGameArt.asset; Assets/Art/LateGame/CritBurst/ ve meta dosyaları.
- Tools/Art/build-crit-burst-audio.py; Tools/Verification/Editor/CritBurstArtBuilder.cs,
  CritBurstArtVerification.cs; teslim belgeleri, lisans envanteri, bağlama ve TODO.

## Sınırlı doğrulama
İzole Unity 6000.3.14f1 derlemesi, mevcut CritBurst regresyonu ve 25 kontrol geçti (çıkış 0).
25 kontrolün 12'si ayrı havuz reset döngüleridir. İki kritik reddi, üçte tek tetik,
dokuzda tek tetik/aynı hedefler ve stat hasarı, gerçek merkez/yarıçap, .05/.22 aşamaları,
pause, 96 dekor sınırında görsel atlanırken dokuz gerçek hasar, 12 tekrar kullanımda
sabit nesne sayısı, round/debug restart temizliği ve .32 sn sonu sınandı.
CritBurstArtVerification.txt ve ProtectedResult.txt kanıttır. CatalogBefore.txt önceki
kataloğu saklar; diff yalnız iki CritBurst kaydıdır. Önceki ödül varlıkları, denge asset'leri,
seçili profil ve imleç kodu hash ile korundu. Açık ana editörde Play işletilmedi; DemoScene
ve gerçek oyuncu kaydı değiştirilmedi. Uzun denge/bellek testi veya yeni test sahnesi yok.
Unity Editor SearchDatabase/IndexationOnStartup kendi indeks istisnasını yine kaydetti;
C# derlemesi ve oyun kontrolleri başarılı. Bu editör altyapı sorunu kapsam dışında kaldı.

## Hızlı Play
GameScene / Run50_GecOyunV1 → Tools > Geç Oyun Testi → Kritik Patlama → Ver.
Ön koşul: kritik şansı sıfırdan büyük olmalı. Bir saldırıyla en az üç canlı bitkiye
kritik vur; öldürmek zorunda değilsin. Merkez yıldız/krem çekirdek ve kısa tok sesi izle.
İki hedef, çok hedef, farklı gerçek saldırı yarıçapı, pause ve restart'ı kontrol et.
Test alımı oturumdaki kayıt yazımını kapatan mevcut debug yolunu kullanır.

Üretim tamamlandı. Ayrıntılı görsel/işitsel kabul ve insan Play onayı bekliyor.
CritBurst_NormalCamera.png temsilci normal kamera karesidir; nihai polish/onay değildir.
Diğer ödüllere geçilmedi; commit yok.
