# Hilal Darbesi → Büyüyen Ay → Dolunay

**Güncel kural (10 Ekim): [fare çıkışı ve bitki isabeti sayacı](CursorRoute.md).**
Aşağıdaki 9 Ekim tam-satır anlatımı ve testleri tarihsel üretim raporudur.

9 Ekim 2026. GameScene / Run50_GecOyunV1. Commit yok; diğer ödüllerin polish'i yapılmadı.

## Hareket değişikliği

`RunProfileSO.crescentFullRows` yalnız tam sürüm Run50_GecOyunV1 profilinde açık.
Doğumda uygun açık yatay satırlardan eşit seçim yapılır; yön iki uçtan seçilir.
İlk/son açık hücre dahil tam satır taranır. Boşluk dalgayı durdurmaz; kilitli veya
sınır dışı hücreye hasar yoktur. Satır/uçlar ve açık alan sınırı doğumda sabitlenir;
sonraki genişleme mevcut dalgayı uzatmaz. Özel RNG ödül veya Unity RNG akışını tüketmez.

6 geçerli direct saldırı, 0,8 hasar katsayısı, 0,07 sn adım ve 4 dalga kapasitesi
korundu. Boş savuruş sayılmaz. Büyüme yalnız gerçek hasatta kalan adımlara uygulanır,
hasatsız adımda sıfırlanır; mevcut sınırlar değişmedi. Kenardaki cephe hem hasarda
hem görselde kırpılır. Yeni yolda dalga başına aynı PlantHealth nesnesi tekrar vurulmaz.
Tutulan bitki referansları bitiş/iptal sırasında temizlenir.

Dolunay mevcut büyüme eşiğinde, aynı satırın karşı ucundan ters yönde tek dönüş
başlatır. 0,15 sn ilk dönüş gecikmesi, 0,7 hasar oranı ve 0,35 sn direct duraklama
korunur. Dönüş büyümez veya yeni dönüş üretmez. Bütün hasar mevcut Host.Strike yoluyladır.
Eski profillerin dört hücre mantığı ve testleri korunur.

**Denge etkisi:** 11 hücre açıkken taban dalga en çok 4 yerine 11 hedef hücresine
ulaşır (2,75 kat potansiyel temas). 100 temel hasar örneğinde hasatsız tek sıra
320 yerine 880 toplam potansiyel hasardır. İlk adım dahil nominal süre 0,28 yerine
0,77 sn olur. Gerçek hasar boşluklara, kilitlere ve canlı bitkilere bağlıdır.
Büyüme/dönüş bunu ayrıca artırabilir; daha uzun dalgalar dört yerin daha uzun süre
dolu kalmasına yol açar. Gizli hasar veya tetik sıklığı telafisi yapılmadı.
Mevcut zamanlayıcı düşük FPS'te her karede bir adım çalıştırabilir: hücre atlamaz,
amacı dışı çift vurmaz, fakat geçişin gerçek süresi uzar.

## Görsel ve ses

Tek sürekli cyan hilal: açık kesici yüzey, koyu ink, iki kısa iz; hücre başına bıçak yok.
Büyüme aynı nesnenin gerçek cephe genişliğini değiştirir; sıfırlama onu küçültür.
Dolunay sıcak altın ton, ikinci iç yay ve ters akışla ayrılır. Öncü kesici uç hücre
zamanına hizalanır. Gerçek isabette küçük temas parçaları; boş hücrede sahte darbe yok.
Kenar oluşumu 0,06 sn, son hücre sonrası sönme 0,28 sn; oynanış bunları beklemez.

Üç özgün kısa ses: Launch/Grow/Return. Hücre başına ses yok. Klip başına en çok iki
ses, paylaşılan havuzda toplam sekiz AudioSource; tekrar aralığı ve pause korunur.

| Prefab | Üçgen | Flat vertex | Ink dahil üçgen | Renderer |
|---|---:|---:|---:|---:|
| CyanCrescent | 408 | 1224 | 576 | 3 |
| WarmReturnMoon | 576 | 1728 | 744 | 3 |
| MoonContact / WarmMoonContact | 48 | 144 | 48 | 1 |

Flat vertex sayısı sert normal/renk yüzleri için ayrılmış köşelerdir. En ağır dört
aktif dalga toplam 2.976 ink dahil üçgendir; temaslar mevcut 96 dekor sınırına dahildir.
Yeni doku, shader, parçacık katmanı, hücre Update/Animator veya fizik simülasyonu yok.
Paylaşılan özgün ThornToon/ThornInk materyalleri kullanılır. Havuzlar ilk ihtiyaçta ısınır.

## Değişen kaynaklar

- `Assets/Scripts/RunEffects/Effects/CrescentEffectSO.cs`: profil kapısı, sabit rota,
  özel RNG, hedef tekrar koruması, gerçek isabet bildirimleri.
- `CrescentEffect.Presentation.cs`: salt okunur dalga anlık görüntüsü ve satır seçimi.
- `Assets/Scripts/ScriptableObjects/RunProfileSO.cs` ve
  `Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset`: yalnız yeni rota seçimi.
- `Assets/Scripts/RunEffects/RunEffects.cs`: sona eklenen temas bildirim kimlikleri.
- `Assets/Scripts/LateGameArt/LateGamePresenter.Moon.cs`, `LateGamePresenter.cs`,
  `LateGameArtSO.cs`, `LateGameAudio.cs`: merkezi sunum, katalog ve ses anahtar kapasitesi.
- `Assets/Art/LateGame/CrescentFamily/`, `Assets/Resources/LateGameArt.asset`:
  Unity editör API'siyle üretilip bağlanmış mesh/prefab/sesler.
- `Tools/Verification/Editor/MoonArtBuilder.cs`, `MoonVerification.cs`,
  `GecOyunV1Verification.cs`; `Tools/Art/build-moon-audio.py`: üretim ve doğrulama.

Yeni dış içerik alınmadı; özgün geometri ve Python standart kütüphanesi ses sentezi.
Kullanıcı referansı doku olarak oyuna alınmadı. Lisans kaydı ThirdParty-Licenses.md.

## Doğrulama ve sınırlar

Testler Library/VerificationProject izole kopyasında MemoryOnly kayıtla çalışır.
Ana GameScene/DemoScene, gerçek kayıt ve seçili profil dosyasına yazılmaz.
`MoonVerification.txt`, `Legacy-GeneralRegression.txt`, `MoonPool.txt`,
`MoonPerformance.txt` ve görüntüler bu klasördedir. Eski 97 kontrol silinmedi;
`GecOyunV1Verification.RunLegacyCrescentBatch` eski profil kapısıyla onları çalıştırır.
Yeni tam satır kontrolleri ayrı `MoonVerification.RunBatch` girişindedir.

Düşük FPS 0,3 sn/kare senaryosu, iki yön, boş/aralıklı satır, ilk/son hücre, kilit,
alan genişlemesi, büyüme/sıfırlama/kenar, ters dönüş, davranış güvenliği, pause,
round/run-complete (zafer/kayıp ortak yolu), menü ve restart handler'ları sınandı.
30 ısınmış yeniden kullanım döngüsünde presenter nesne ve materyal sayıları sabit.
Bu, sınırsız memory-leak garantisi veya native memory/GPU profiler yakalaması değildir.

Dolu tarla görüntüleri gerçek GameScene kamerasında kontrollü dalga adımlarıyla alındı;
bitkilerin çıkış animasyonları test görüntüsü için dinlenme boyuna getirildi.
Bu inceleme insan Play onayı değildir. Editor batch süreleri düşük öncelikli dört
çekirdekte ölçülür; yayın build'i FPS taahhüdü olarak yorumlanmamalıdır.

## Son sonuçlar

- Hilal: **39 kontrol geçti**. Eski profil/genel regresyon: **97**; anten: **45**;
  Bereket: **42**. Toplam **223**. Her koşu Unity çıkış kodu 0 ile tamamlandı.
- 408 korunan dosyanın 407'si aynı; tek beklenen fark tam sürüm profilinin rota
  bayrağıdır (Unity serializer biçim değişiklikleri dahil). ProtectionVerification.txt.
- 30 tekrar sonrası 394 presenter transformu, 147 global materyal ve 306 global
  mesh sabit kaldı. İlk ısınma maliyeti bu döngü ölçümüne dahil değildir.
- Dört aktif dalga için 1.000 görsel sync çağrısı ortalama **0,0060 ms/çağrı**.
- İlk ölçümdeki “0 bayt” sonucu **geçersizdir**: GC.GetAllocatedBytesForCurrentThread
  sayacı 4.096 baytlık kontrol tahsisini göremedi. Hilal'e özel kesin allocation
  sayısı bu API ile verilemez; test artık bunu açıkça UNAVAILABLE raporlar.
- Ayrı Unity Profiler sayacıyla, tüm izole sahnede 60 boşta kare / 5.692 yoğun dalga
  karesi: medyan **0,80 / 0,66 ms**, p95 **3,32 / 1,01 ms**, allocation ortalaması
  **124 / 105 B/kare**. Editor, test örnekleme ve diğer sistemler dahildir.
  Düşük öncelikli batch örneğindeki bu fark performans iyileşmesi kanıtı değildir;
  gerçek cihazdaki render/GPU ve player-build Profiler incelemesi ayrıca gerekir.

## Açık kararlar ve insan Play

**TODO:** Davranış açacak gelecekteki yükseltmenin kartı, şansı ve bütçesi belirsiz.
Taban/Büyüyen Ay/Dolunay davranış tetiklemez. Rezonans bağlantısı uygulanmadı.

- Dolu küçük ve 11×11 tarlada iki yöndeki cyan geçişin okunurluğu.
- Büyümede genişleme, başarısız adımda küçülme ve kenarda taşmayan cephe.
- Aynı satırdaki altın dönüşün ayrı dalga olarak anlaşılması; kısa direct duraklamanın hissi.
- Yoğun zincir/anten efektleriyle kontur ve temas okunurluğu, üç sesin miks seviyesi.
- Pause, round sonu, zafer/kayıp, menü ve yeni run sonrasında görüntü/ses kalmaması.


## Küçük tarlada görünürlük takibi — 9 Ekim 2026

Kullanıcı 3×3 başlangıç alanında üç aşamayı da görmediğini bildirdi. Önceki tam alan
incelemesi bu kullanım için yeterli değildi. Yeni MoonSmallFieldVerification gerçek
Update/LateUpdate ve zamanlayıcıyla, yalnız orta 3×3 hücre açıkken üç formu da kamera
içinde yakaladı. Test kontrollü düşük bitki canı kullanır; görünmeme bildiriminin
bütününü veya kullanıcının aynı Play oturumunu yeniden üretmiş sayılmaz.

- Mantıksal ilk adım değişmeden, görsel ön uç ilk açık hücrenin dış sınırında doğar;
  önceki bir tam hücre dışındaki başlangıç yarım hücreye indirildi. Gecikme/hasar aynı.
- Kesici yüzey kalınlığı tabanda .14 → .24, dönüşte .19 → .29; cyan kenar .075 → .10.
  Cephe hücre genişliği ve poligon sayısı değişmedi.
- Sönme .16 → .28 sn; son hücreden sonra yeni hasar veya hareket üretilmez.
- Test panelinin Describe çıktısı son rota için grid x başlangıç/bitiş, z ve yön gösterir.
- Kanıtlar: MoonSmallField.txt, MoonSmall_Base/Growth/Return.png. Küçük alan testi
  11×11 kontrollü saksı düzeninin dışını kilitler; dıştaki boş saksı modelleri fixture'da
  kalır. Kullanıcının seyrek başlangıç saksı düzeninin birebir kopyası değildir.
- Yeni harici varlık eklenmedi; mevcut özgün C# meshleri düzenlendi.

Görünürlük değişikliği sonrası MoonVerification: 39 kontrol geçti; Unity çıkış kodu 0.
Son regresyon/performance çıktıları MoonVisibilityRegression.txt ve MoonVisibilityPerformance.txt.
