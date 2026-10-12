# Tam oyun Play geri bildirimi — 6 Ekim 2026

## Kapsam ve kararlar

GameScene ve seçili `Run50_AlphaDengeV1` adayı üzerinde uygulandı. Yeni denge profili oluşturulmadı; eski profillerin denge verileri korunuyor. DemoScene dosyasına ve demo ayarlarına yazılmadı. Demo aynı Alpha profilini kaynak aldığı için yeni temel güç ve dışlama kuralları demo bağlamında devre dışı tutuldu.

Kullanıcının son kararı: kasırga ve bumerang **run başında ayrı ayrı en çok 12** aktif olabilir. Sınırı yalnız seçilen kart artırabilir. Görselsiz sınırsız davranış çalıştırma yaklaşımı kaldırıldı.

## Uygulanan değişiklikler

### Temel güç

Yalnız tile yerleştirme/yükseltme seçenekleri tükendikten sonra gelen temel güç kartları:

| Nadirlik | Kart başına değişim |
| --- | --- |
| Common | %0,02 |
| Rare | %0,04 |
| Epic | %0,07 |
| Legendary | %0,10 |

Veride bunlar `0.0002 / 0.0004 / 0.0007 / 0.001` oranlarıdır. %2–10 değildir. Nadirlik mevcut zarla seçilir; ek zar veya yeni seçim ekranı yoktur.

Hasat hasarı, XP ve kaynak kartları kendi stat/hedef gruplarında toplanır. Örneğin 1.000 adet +%0,10 kart, kendi grubundan ×2 üretir; ×1,001^1000 üretmez. Başka kaynaklardan gelen boss, rezonans ve tile çarpanları korunur. Atak aralığı ve üretim süresi azaltımları mevcut çarpımsal hesapta kalır; kartların değeri küçüldü, mevcut minimum aralıklar korundu. Bu iki azaltım toplamı negatif bir süre oluşturmaz.

`BasePowerCardRules` kart oranı, birikim tercihi ve uygulamasını taşır. Kart UI'sı aynı kuralı kullanır. Kalıcı kayıt/metaprogression değil, o run'ın statlarıdır.

### Birlikte alınamayan ödüller

Hasat Ritmi ile Zincir Hasat aynı `harvest_playstyle` grubundadır. İkisi seçenek olarak aynı teklifte bulunabilir; biri alınınca diğeri sunulmaz ve eski teklif üzerinden de alınamaz. Boss kartı diğerinin kapanacağını yazar. Ödüllerin kendi hasar/yarıçap/zincir katsayıları değişmedi.

Yalnız Alpha havuzundaki iki ödülün kopyası oluşturuldu. Eski asset'ler değişmedi. `fullGameExclusiveGroup` işareti, demo aynı kaynak havuzu kullansa da bu yeni dışlamayı demoya taşımaz.

### Hareketli davranış sınırı

- 12 kasırga + 12 bumerang; bütün tarla için iki ayrı aktif sayaç.
- Önceki sahne sınırları kasırgada 3, bumerangda 6 idi. Kullanıcının istediği 12 başlangıcı bu iki kapasiteyi artırır; eski oyuna göre bir kapasite nerfi değildir.
- Sınırda yeni normal tetik reddedilir. Gizli hasar nesnesi veya sınırsız ek havuz yoktur.
- Zincir işleri mevcut sınırlı kuyruğunu kullanır; aynı aktif sınırı aşamaz.
- Boss ödüllerine `movingBehaviorLimitBonus` alanı eklendi. Test kartının +4 değeri gerçek ödül uygulamasıyla 12 → 16 yapar. Birikim/stack/bedel ve run temizliği mevcut ödül sisteminden geçer.
- **Artış kartı henüz gerçek ödül havuzuna eklenmedi.** İçerik aşamasında adı, ağırlığı, aşaması ve bedeli belirlenip bu alanla oluşturulabilir. Şimdiki oynanabilir havuzda başlangıç sınırı 12 olarak kalır.
- Hatalı içerik verisiyle sınırsız iş açılamaması için toplam kapasitenin teknik tavanı 64'tür; bu başlangıç değeri veya otomatik verilen bir artış değildir.

### Sonuç ekranı

GameScene'deki mevcut sonuç denetleyicisine yalnız sunumdan sorumlu `RunResultLayout` eşlik eder. Koyu arka plan, sabit okunabilir yazı boyutu, kaydırılabilir ödül listesi ve sabit çıkış düğmeleri vardır. Skor, başlangıç seçimi, açılan içerik, ödüller, bedeller ve süre bilgileri mevcut kaynaktan gelir.

### Performans

Bumerang hareketindeki darbe taraması, her küçük adımda bütün 11×11 alanı gezmek yerine segmentin kapsadığı hücre aralığını tarar. Kesin mesafe ve bitki başına vuruş testi değişmez. 10.000 örnek segmentte hiçbir geometrik hedef atlanmadı; aday hücre sayısı 1.210.000 → 10.277 oldu. Bu bir FPS/GPU kazancı ölçümü değildir.

Kasırga bekleme nesneleri havuzdan yeniden kullanımda tekrar oluşturulmaz. Kasırga temizliği disable/menü/run sonu yollarına da bağlandı. F9 kaydı aktif davranış, kapasite ve reddedilen tetik sayaçlarını ayrı sütunlarda verir (şema v4). Batch örnek kaydı gerçek oyuncu bilgisayarında performans kabulü sayılmaz.

## Doğrulama

İzole `Library/VerificationProject` kopyasında çalıştırılır; ana sahneye test komutu gönderilmez.

- `FullGameFixVerification`: 71 kontrol geçti. Gerçek oyuncu/saksı statları, 12 sınırı, +4 test ödülüyle 16 sınırı, geçersiz kapasite artışının reddi, hasar, temizlik, demo dışlama kapsamı, alan taraması, gerçek boss kartı metinlerinin sığması ve uzun sonuç listesi kontrol edildi.
- `XPV1Verification`: 39 kontrol geçti.
- `BossRewardVerification`: 127 kontrol geçti. Eski tek satırlı sonuç metni beklentisi, aynı beş ödülü aynı sıra ve ×3 adetleriyle ayrı satırlarda arayacak biçimde güncellendi.
- `BedelliOdullerV1Verification`: 94 kontrol geçti (önceki 93 + yeni Alpha kopyaları için 1 kesin veri kontrolü).
- `ZincirV1Verification`: 39 kontrol geçti (önceki 38 + Alpha kopyası için 1 kesin veri kontrolü).
- `AlphaDengeV1Verification`: 51 kontrol geçti.
- `HarvestBehaviorVerification`: 114 kontrol geçti.
- `DemoVerification`: geçti; demo başlangıcı, ödülleri, menü/restart ve GameScene'e dönüş doğrulandı.
- `PerformanceStressVerification`: geçti; nesne yaşam döngüsü, tekrar kullanım, kesin ödüller, bellek ayırma ve v4 kayıt sütunları doğrulandı. Bu bir GameScene GPU/FPS testi değildir.
- Dokuz veri üreticisinin `--check` kontrolü temiz.

`BedelliOdullerV1Verification` bütün sonradan eklenen ödül asset'lerini de “eski” sayıyordu. Yeni iki Alpha kopyası kesin kimlik, grup ve tam oyun işaretleriyle ayrı doğrulanıyor; eski ödüllerin etkisiz alan denetimi korunuyor.

`ZincirV1Verification` de yalnız tek zincir asset'i/havuzu olacağını varsayıyordu. Orijinal ile Alpha kopyası ayrı tanımlanıyor; kopyanın nesil, şans, hasar, bütçe ve kare başı iş değerleri orijinalle eşit aranıyor. Diğer havuzlarda zincir bulunmaması kontrolü korunuyor.

Ek `PerformanceStressVerification` ilk turda kendi verdiği XP'ye seçili Alpha profilinin Rare ×2 çarpanı da uygulanınca düştü. Bu test kendi veri setini oluşturuyor; seçili profilin etkisi oturum geçersiz kılmasıyla kapatıldı ve test sonunda kaldırılıyor. Beklenen kesin ödül miktarları değiştirilmedi. Eski v3 kayıt beklentisi de v4 ve altı yeni kapasite sütununu kesin doğrulayacak şekilde güncellendi. Son tur geçti. Unity Editor SearchDatabase başlangıç istisnası mevcut testin editör altyapısı ayrımıyla raporlandı; oynanış hatası olarak gizlenmedi.

Test sonuçları ve iki incelenmiş ekran görüntüsü `Docs/FullGame-PlaytestFix-20261006/` klasöründe. Sonuç ekranı görüntüsü uzun listeyi denemek için testin verdiği örnek metni kullanır; bir gerçek run'ın sonucu değildir. Oyuncu kaydının SHA256 değeri baştakiyle aynı, profil seçimi ve DemoScene/ayar dosyalarının Git farkı yok. Önceden var olan `Docs/DemoScene.md` değişikliği ve ProfilerCaptures dosyaları korunmuştur.

## İnsan Play kontrolü ve sınırlar

1. GameScene, `Run50_AlphaDengeV1`: temel güç kartına geçince %0,02–%0,10 değerlerini ve gerçek stat değişimini kontrol et.
2. Hasat Ritmi veya Zincir Hasat al; diğerinin kapanacağını kartta gör ve sonraki teklifleri kontrol et.
3. Yoğun kasırga/bumerang build'inde F9 ile 20–30 saniye kayıt al. En yüksek aktif adet, takılmalar ve reddedilen tetikler birlikte değerlendirilsin.
4. Run sonunda uzun ödül listesini fare tekeri/scrollbar ile aşağı kaydır; restart ve menü düğmelerini kontrol et.

İnsan Play testi ve yeni 50 round denge karşılaştırması yapılmadı. Küçük temel güç oranlarının XP yolunu ne kadar yavaşlattığı henüz denge kabulü değildir. Yeni endgame aksiyonu, büyük saksı nerfi, final boss veya endless eklenmedi. Commit atılmadı.
