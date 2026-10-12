# Gezgin Bereket — pasif tarama alanı (8 Ekim 2026)

Bu revizyon kullanıcının son taleplerini uygular. Önceki altı direct hasat / üç canlanma tasarımı
kaldırıldı; eski raporlar yalnız tarihçedir (Legacy-SixHarvest.md). Yeni davranış:

- Hazırlık/belirme bitince aktif 3×3 bölgedeki hasatlar **2× kaynak ve 2× XP** verir.
- Oyuncu seviyesiyle büyümez. Kullanıcı önce seviye artışını seçip ardından bunu 2× olarak düzeltti.
- Direct ve behavior hasatları aynı pasif alan bonusundan yararlanır; altı hasat koşulu yoktur.
- Boş üretim yuvalarının mevcut üretim süresi yarıya iner. Anında sahte bitki oluşturulmaz;
  normal üretim yolu, bitki zarı, havuz ve nadirlik kurulumu kullanılır. Dolu yuva değişmez.
- Bölge en az 10 saniyede bir açık komşu hücreye rastgele bir adım kayar; profile daha uzun bir
  hareket aralığı verilmişse uzun olan kullanılır. Mantıksal alan ve gösterilen hücreler aynıdır.
- Eski 30 sn sınırı kaldırıldı. Olay round sonuna kadar kalır; menü/restart/round temizliği bonusu kaldırır.
- Skor çarpılmaz. Normal hasat kaynağı (duplicate/uzmanlaşma dahil) bir kez 2× yapılır;
  XP'nin mevcut hesaplanmış miktarı bir kez 2× yapılır. Kaynak int taşması mevcut NumericSafety ile sınırlanır.

## Görsel — altın perde ve toplama robotları

3×3 alanın tamamı tek sarı/altın, yukarı doğru solan perdeyle çevrilir. Hücrelerin içinde ayrı perde
yoktur. Her hücrede basit bir uçan toplama robotu, robottan saksıya ışın ve yukarı tarayan altın halka vardır.
Mavi ışık ve çiçek kümeleri bu revizyonla kaldırıldı. Robotlar comic/toon materyallerini kullanır.

Görünen bölgedeki bitkiler, hazırlığın ilk karesinden itibaren geçici bir altın çizim katmanı alır.
Sararma aktif fazı, hasarı veya yeniden doğuşu beklemez; perde ve renk aynı görünürlük koşulunu kullanır.
Ödül/üretim bonusunun mevcut aktif faz zamanlaması korunur. BountyPlantTint, bitkinin mevcut meshini
özgün BountyPlantGold shader'ıyla ek bir geçişte çizer. Paylaşılan materyaller, renkler ve hasar
property block'ları değiştirilmez. Bölge ayrılınca katman çizilmez; ayrıca eski rengi saklayıp
geri yazmak gerekmez. Pool reuse, başka bölge veya yeni run'a altın renk taşınmaz.
Bitki başına mesh/submesh kadar ek çizim maliyeti vardır; cihaz GPU profili ölçülmedi.

Perde, tarama ve robot hareketlerini ortak presenter sürer. Dinamik ışık, bloom bağımlılığı,
hücre başına Update/Animator veya yeni ParticleSystem yoktur. Pause ile hareket durur.
Özgün BountyAuroraToon ve BountyPlantGold shader'ları kullanılır; üçüncü taraf shader değiştirilmedi.

HUD kaynak ×2, XP ×2, hızlı üretim ve gerçekleşen hasat/doğuş sayılarını gösterir. Eski altı hasat
sayacı yoktur. Sesler önceki bounded voice havuzunu kullanır; robot sayısıyla ses sayısı artmaz.

## Uygulama ve doğrulama

WanderingBountyEvent yalnız aktif fazdaki güncel bölge üyeliğini verir. PlantResource hasat anında
bu üyeliğe bakar; çarpan bitkiye/havuza yazılmaz. PlantSpawner her seferinde mevcut alanın süresini
sorar. Bölgeden çıkınca, round bitince veya olay silinince bonus artık uygulanmaz.

İzole GameScene, normal kamera ve 121 bitki ile **32 özel kontrol geçti**:
aynı bitkinin normal/bonuslu ödemesinde tam 2× kaynak ve XP, skorun aynı kalması; gerçek direct ve
behavior ölümleri; yarı üretim süresi ve gerçek Update/TrySpawnPlant yoluyla doğuş; dolu yuvada
çift bitki olmaması; hareketle eski hücrelerin bonus kaybetmesi; pause, menü, restart ve 30 saniye
sonrasında hâlâ aktif kalma. Üretim testinde beklemeyi hızlandırmak için mevcut timer sınıra taşınır;
ödül oranı aynı canlı bitkinin GiveReward yoluyla karşılaştırılır, ayrıca gerçek ölümler ayrı sınanır.
Bu kontrollü işlev testi insan Play onayı veya tam cihaz performans ölçümü değildir.

Başlangıç koruma listesinde değişen üç dosya: FieldEventDirector, FieldEventPlanSO ve
WanderingBountyEvent. Bunlar yetkilendirilen yeni mekanik içindir. Önceki onaylı üç olayın art varlıkları,
GameScene/DemoScene, seçili profil ve gerçek kayıtlar aynı (Protected-passive.json).
PlantSpawner/PlantResource kaynak değişiklikleri yeni pasif bonusun entegrasyonudur.
Plan asset'indeki eski threshold/maxBlooms verileri uyumluluk için saklanır, çalışırken okunmaz.

## İnsan Play

1. Altın alanın içindeki ve dışındaki aynı tip bitkide kaynak/XP farkını kontrol et.
2. İçeride boşalan saksının daha çabuk dolduğunu ve dolu saksının değişmediğini izle.
3. Alanın yavaş adımlarını, eski yerde bonusun kalkmasını ve round sonuna kadar sürmesini kontrol et.
4. Tek altın perdenin sınırına, robot ışınlarına ve alan ayrılınca bitkilerin normal görünümüne dönmesine bak.

## Sonraki düzeltme notları

Kullanıcının diğer buff'larda yanlış spawn konumu, hücreye oturmama ve alan gösterilirken ayrıca çıkan
toprakların kötü görünmesi notları korunur. Bu revizyon Bereket ile sınırlı; diğer olaylar değiştirilmedi.
Commit yok. Anten/hilal ailesine içerik üretimi yapılmadı.

Son genel regresyon: **97 kontrol geçti**; özel 27 ile toplam **124**. Son derleyici/shader hata sayısı 0.
Önceki altı-hasat senaryoları yeni pasif sözleşmeye göre güncellendi; eski sonuçlar yeni mekanik kanıtı olarak sayılmaz.
Son rapor: Passive-GeneralRegression.txt. Görseller: BountyBuff_02_Active.png, BountyBuff_03_Respawn.png,
BountyBuff_04_Moved.png. Önceki Bounty_*.png görüntüleri eski tasarım tarihçesidir.

## Altın robot revizyonu — son test ve bellek kapsamı

Son shader derinlik düzeltmesiyle **36 özel kontrol geçti**; genel regresyon **97** (Gold-GeneralRegression.txt).
Toplam 133 kontrol. Son loglarda derleyici/shader hatası yok. Tek perde, dokuz robot/tarayıcı, bitki
özgün materyallerinin korunması ve tint çizimlerinin temizlenmesi özel kontroller arasındadır.

Havuz stres testi: üç ısınma ardından 40 ölçülen döngü. Her döngüde başlatma, beş bölge hareketi,
dokuz sentetik doğuş/hasat cue'su ve sonlandırma. Havuzdaki Transform, yüklenen Material ve Mesh
sayıları sabit kaldı. Sonunda aktif dekor, efekt ve tint sayısı sıfır. Sayılar BountyPoolStress.txt'de.
Havuz nesnelerinin kapalı tutulması yeniden kullanım içindir; leak olarak sayılmaz.
Bu test uzun süreli native/GPU bellek snapshot karşılaştırması değildir; mutlak 'memory leak yok'
garantisi verilmez. Bu sınırla birlikte tekrarlı kullanımda artan nesne/materyal/mesh bulunmadı.

## Mevcut bitkide sararma düzeltmesi — 2026-10-08

Sarı materyal, projede bulunmayan bir shader GUID'sine bağlıydı. Unity AssetDatabase ile
mevcut BountyPlantGold.shader varlığına yeniden bağlandı; builder artık mevcut materyalin de
bağlantısını düzeltir. Shader/meta kimliği değiştirilmedi. Önceki sayısal tint kontrolü bu
bozuk bağlantıyı yakalamıyordu; yeni render testi onarım öncesi 0 sarı piksel ile başarısız oldu,
onarım sonrası 689 değişen sarı piksel ile geçti.

Çizim PlantJuice.Update sonrasındaki LateUpdate'a taşındı, her mesh kendi renderer katmanını
kullanır. Shader, mevcut URP bitki shader'ı ile aynı konum dönüşümünü kullanır. Asıl bitki
materyalleri/property block'ları, PlantJuice ve VFXManager hasar tepkisi değiştirilmedi.
Geçici mesh listesi çizim sonunda temizlenir; ek nesne/material/mesh üretilmez.

Bu düzeltmenin izole GameScene testi: **38 kontrol geçti**, derleyici/shader hatası yok.
40 ısınmış havuz döngüsünde nesne/material/mesh sayıları artmadı. Genel 97 kontrol önceki
revizyona aittir; bu tur tekrar çalıştırılmadı. İnsan Play onayı veya uzun süreli native bellek
profili değildir. Yeni dış içerik kullanılmadı. Commit atılmadı.

Play: Önceden yerleşmiş canlı bitkinin üstüne alan geldiğinde sarardığını, alan ayrılınca
normal rengine döndüğünü ve vurulduğunda beğenilen darbe hissinin korunduğunu kontrol et.

## İlk görünme karesinde sararma — 2026-10-08

Önceki düzeltme materyal bağlantısını onardı; ancak tint hâlâ Active fazını bekliyordu.
Perde/robotlar Preparing fazında göründüğü için mevcut bitkiler birkaç saniye geç sararıyordu.
BountyPlantTint.IsVisible artık hem perde hem tint için aynı Preparing/Emerging/Active koşuludur.
Covers güncel görünür hücreleri kullanır; eski hücreyi bekletmez. Benefits ve oyun süreleri aynı.

İzole GameScene doğrulaması: **42 kontrol geçti**. Dokuz önceden doğmuş bitki, ilk Preparing
karesinde gerçek presenter LateUpdate yoluyla çiziliyor; saldırı/hasat/doğuş olmadan aynı
bitkiler korunuyor. Emerging boyunca renk devam ediyor, hareket karesinde yeni bölgeye geçiyor.
Piksel testi, bonuslar, pause/temizlik ve 40 ölçülen havuz döngüsü de geçti.
İlk-kare görüntüsü: BountyBuff_01_Prepare.png. İnsan Play onayı değildir.
Hasar hissi kodu ve özgün materyaller değiştirilmedi. Yeni dış içerik veya commit yok.
