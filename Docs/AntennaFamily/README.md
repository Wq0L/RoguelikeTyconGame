# Anten ailesi — comic/toon sunum

Elektrik Anteni → İletken Hat → Kapalı Devre. GameScene/tam sürüm için 8 Ekim 2026.
Kullanıcının verdiği görsel yönüyle özgün düşük poligonlu taş/yaprak kaide, altın bobin,
bakır çatal ve ileri uzanan altın uç üretildi. Yeni dış varlık, shader veya paket yok.

## Görsel ve ses

- Model +Z yönüne bakar. Sağlam taş taban, üç bobin ve ileri uzanan çatal küçük kamerada ayrılır.
  Yerleşirken kısa oturma, son .32 saniyede gerçek saldırı sayacından enerji toplama,
  atışta esneme, boşta çok hafif hareket. Kök/oyun konumu değiştirilmez.
- Elektrik Anteni ucundan ilk üç hücrenin sonuna dar cyan/krem toon zikzak gider.
- İletken Hat anten uçlarını gerçek Bresenham hücre güzergâhıyla bağlar. Yalnız saldırıda
  görünür; sürekli alan dolgusu veya kalıcı grid çerçevesi yok.
- Kapalı Devre: yalnız gerçek başarılı ek-atış planında uçtan uca geri besleme işareti;
  mevcut gecikmeden sonra kırmızı çift tel ve iki vuruşlu görsel/ses ritmi (22 Ekim: ek darbe ve temas kıvılcımları kırmızı; normal hat cyan/krem kaldı). Sahte davranış
  patlaması yok; görsel zamanlaması hasarı bekletmez.
- Temas yıldızları doğrudan StrikeResult.Hit'ten üretilir; boş veya ölü hücreye temas yok.
- Dört özgün mono kısa ses: yerleşme, kısa atış, hat, çift ritimli devre. Ortak sekiz ses
  sınırı, klip başına iki ses ve tekrar aralığı kullanılır. Sürekli elektrik uğultusu yok.
- Yerleşimde cyan yönlü soketler, altın seçili işaret ve gerçek anten önizlemesi vardır.
  İkinci antenin hem geçici bağlantı hattı hem kısa erişimi gösterilir. Geçersiz konumlar
  seçilemez; iptal/disable tüm geçici işaretleri saklar. Yeniden açılış mevcut veriyi okur.

## Mekanik sözleşmesi

Süre/hasar/şans/yerleşim asset'leri değiştirilmedi. İlk üç hücreye 2.5 s, hat 4 s,
ikinci antenin kendi atışı açık. Kapalı Devre mevcut davranış şansı, .35 s gecikme,
.6 hasar oranı ve 3 s bekleme kuralını kullanır. Hasar aynı Reward yolundadır;
kritik, davranış, Zincir Hasat, Hasat Ritmi ve behavior hasar bonusu istisnaları korunur.
Oyun koduna yalnız sunum bildirimleri ve salt okunur sayaç erişimi eklendi.

## Dosyalar

- `Assets/Scripts/RunEffects/Effects/AntennaEffectSO.cs`: gerçek temas bildirimi ve
  salt okunur saldırı geri sayımı.
- `ConductiveLineEffectSO.cs`, `ClosedCircuitEffectSO.cs`, `RunEffects.cs`: gerçek
  temaslar, planlanmış enerji dönüşü ve sona eklenen cue türleri.
- `Assets/Scripts/LateGameArt/LateGameAntenna.cs`: merkezi zamanla sürülen child animasyonu.
- `LateGamePresenter.Antenna.cs`: kısa/hat/ek atış, uç ve gerçek hücre konumları.
- `LateGamePresenter.cs`: havuzlanan antenleri koruyarak uzlaştırma, ses ve güncelleme.
- `LateGameContent.cs`, `LateGameArtSO.cs`: anten reset/step bağları ve sona eklenen
  katalog yuva/varyant/ses kimlikleri. Eski enum kimlikleri korunur.
- `RewardPlacementUI.cs`: yön, model, erişim ve geçici hat önizlemesi; mevcut UI korunur.
- `Assets/Art/LateGame/AntennaFamily/`: mesh/prefab ve dört ses. `Assets/Resources/LateGameArt.asset`:
  yalnız yeni Antenna/Circuit varyant bağları. Önceki ailelerin bağları korunur.
- `Tools/Verification/Editor/AntennaArtBuilder.cs`, `AntennaArtVerification.cs`:
  tekrarlanabilir özgün üretim ve izole test. `Tools/Art/build-antenna-audio.py`: ses kaynağı.

## Maliyet ve ölçüm kapsamı

Ana anten: **576 üçgen / 1728 düz gölgelendirmeli vertex**; ink geçişiyle toplam 1136 üçgen.
Beş renderer (şarj kapalıyken dördü görünür). İki anten aynı mesh/materyalleri paylaşır.
Kısa ve normal hat parçası 98, çift hat parçası 196, geri besleme 48, temas 72/108 üçgen.
Hat, hücreler ve iki uç arasında parçalanır; 11 hücrede 12 parça. Detay: AntennaGeometry.txt.

Yeni ParticleSystem/Animator/ışık yok; hücre başına Update yok. Geometri/material çalışma
anında üretilmez. Dekorlar ortak 96 sınırındadır; yoğun stres altında dekor atlanabilir,
hasar hesabı etkilenmez. Anten modelleri dekor sınırına girmez.

Genel regresyonun batch CPU örneği: 121 bitki ve 400 dokuz-hücre direct saldırısında
temel 0.5726 ms / 0 B, 13 ödül + olay 0.6196 ms / 0 B. Bu yalnız o testin saldırı çağrısı
ölçümüdür; antenin tek başına kare/GPU maliyeti veya hedef cihaz FPS garantisi değildir.

## Doğrulama

`GeneralRegression.txt`: 97 kontrol geçti. Gerçek ödül alımı ve ön koşullar, üç hücre/tam hat,
ek vuruş ve özyineleme yasağı, kritik/davranış/zincir/sayaç istisnaları, round/menu/restart,
gerçek yerleşim ekranında iptal/yeniden açma/geçersiz seçim/onay ve yoğun ortak efektler.

Özel test raporu: AntennaArtVerification.txt. Dört yön, gerçek temas koordinatları,
boş hücre, gerçek sayaçtan şarj, enerji dönüşü, çift vuruş şekli, pause, genişlemede
aynı modelin yeni kenara taşınması, ikinci anten önizlemesi ve havuz yeniden kullanımı.
Testler GameScene normal kamerasıyla 121 saksılı kontrollü tarlada çalışır. Profil ve gerçek
kayıtlar yalnız izole kopyada/MemoryOnly düzeninde kullanılır; ana sahne veya kayıt yazılmaz.

Önceden onaylı art, sahne, profil ve mekanik verilerinden 343 dosya başlangıç hash'iyle
karşılaştırıldı: değişiklik yok (Protected.json). Commit yok; Hilal içerik üretimi yapılmadı.

## İnsan Play kontrolü

1. Dört kenarda yönü ve ilk üç hücreyi; ikinci anten seçiminde hat ile kısa erişimi karşılaştır.
2. Kısa cyan vuruş, tam hat ve geri besleme sonrası altın çift vuruşu yoğun tarlada izle.
3. Yerleşim/atış/hat/devre seslerini dinle; özellikle sık tetikte ses dengesi ve çift ritmi değerlendir.
4. Alan büyümesi, pause, iptal/yeniden açma, round sonu ve menü/restart geçişlerini dene.

Kaydedilen görsel inceleme ve otomatik testler insan Play onayı değildir. Uzun süreli
native bellek/GPU profili yapılmadı; havuz sayımı tek başına mutlak sızıntısızlık kanıtı değildir.

## Son test özeti

- Anten özel doğrulaması: 45 kontrol.
- Genel mekanik/regresyon: 97 kontrol.
- Bereket'in onaylı anında sararması ve temizliği: 42 kontrol (BountyRegression.txt).
- Toplam 184 kontrol. 30 ölçülen yeniden kullanım döngüsünde presenter transformları
  229, global materyaller 149, global meshler 301 olarak sabit kaldı.
- Global ilk kullanım artışı ayrıca araştırıldı: 7 yeni mesh ortak TextMeshPro hasar
  yazılarına aitti. Bu mevcut havuz 121 ile sınırlıdır. Son ölçümden önce ortak yazı
  havuzu da ısıtıldı; AntennaRetentionDetail.txt farkı ve AntennaPool.txt ölçümü kaydeder.
- Geometri son rötuşu: geri dönen enerji işareti, uzun hatta Z ölçeğiyle uzayıp büyük
  bir iğneye dönüşmesin diye normalize edildi. Ana model/VFX bütçesi değişmedi.

