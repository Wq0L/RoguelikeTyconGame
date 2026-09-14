# Saksı rezonansı — çalışan prototip

## Tasarım kararları (2026-09-13)

- Kullanıcı tile konumlarını seçmiyor; buff rastgele hücreye geliyor. Rezonans aynı saksının kapladığı hücrelerdeki tile türü sayısına bakar, komşuluk veya desen istemez.
- Skill tree oyuncuyu, tile ve rezonans saksıyı etkiler. Oyuncunun başka saksılardaki hasarı değişmez.
- Kullanıcı hasarın önemli bölümünü tile yatırımlarından, tamamlayıcı gücü kombinasyonlardan istiyor. Önceki 175k/400k ve 130-round HP tabloları bu değişiklikte uygulanmadı.
- Absürt güç sıçramaları isteniyor: XP için iki tile x2, üç tile x10 örneği uygulandı. Bunlar nihai ekonomi dengesi değil.
- Büyük saksıların çok buff alması ileride dengelenecek; bu değişiklik boyuta göre ceza koymaz.
- Gelecekte aynı tile kimliği/konumu korunarak sayısal değer yeniden roll edilebilir. Oyuncuya reroll satın alma sistemi henüz eklenmedi.

## Örnekler

`Assets/Resources/ResonanceRules.asset` Inspector'dan düzenlenir. Her kuralın tile türü, etkilediği stat ve adet eşikleri vardır. Aynı kuralda yalnızca en yüksek uygun eşik geçerlidir; üç Water x2*x10 değil x10 verir. Farklı kuralların etkileri birlikte çalışır. Aynı aile/stat için ikinci kural eklemek de ayrıca çarpar; bunun kasıtlı olduğundan emin olun.

| Tile ailesi | Adet | Ek rezonans çarpanı |
|---|---:|---:|
| Damage / Odak | 2 / 3 / 4+ | x2 / x4 / x8 hasar |
| Water / XP | 2 / 3+ | x2 / x10 XP |
| Fertile / üretim | 2 / 3+ | x0.75 / x0.5 üretim aralığı |

Tek tile'ın normal etkisi korunur. Rezonans, normal stat hesabından SONRA çarpılır. Örnek: her biri +%20 XP veren 3 Water, normalde x1.6; rezonansla x16 toplam XP sağlar. Taban 20 XP ise 320 XP; tam sayı ödül yuvarlanır. Fertile değeri süre olduğu için küçük çarpan daha hızlı üretir; mevcut minimum 0.1 saniye korunur.

Yeni Damage ailesi Energy'yi değiştirmez. Common/Rare/Epic/Legendary tekli hasar bonusları (2026-09-14 güncellemesi) +%5–10 / +%10–15 / +%15–20 / +%20–30. Farklı rarity'ler aynı Damage ailesinde sayılır. Yeni kartlar GameScene CardSelectionUI.allModifiers havuzuna eklendi; tür ağırlığı 15 (yüzde değil, mevcut ağırlıklara eklenir). Mevcut rastgele hücre seçimi korunur.

100 oyuncu hasarı ve tam +%10 roll edilmiş 3 Damage: (1+0.10+0.10+0.10) x4 = x5.2; doğrudan vuruş 520 olur. Başka saksıda bonus yoksa hâlâ 100 vurulur. Kritik/variance oyuncuda hesaplanır; saksı çarpanı hedefte bir kez uygulanır. Hasar yazısı da gerçek çarpılmış değeri gösterir. Patlama hasarı bu prototipte rezonansı almaz; mevcut zincirleme patlama davranışı korunur.

## Mimari ve güncelleme

- `ResonanceRulesSO`: veriler; sahneye özel değer gerektirmez.
- `ResonanceManager`: stateless hesaplama servisi; singleton GameObject/Update yok. Varsayılan Resources verisini yükler. PlanterBrain üzerinde isteğe bağlı override bulunur.
- `PlanterBrain.RefreshTileBuffs`: kapladığı benzersiz hücrelerden mevcut roll'leri ve tür adetlerini toplar; eski listeleri temizleyerek yeniden üretir. Tile değiştirme/temizleme tekrarlı buff biriktirmez. Başka sistemlerin LocalModifiers listesine doğrudan ekleme yapmaması gerekir; bu liste tile'lardan türetilir.
- Yerleşimde ve GroundCell.ApplyModifier çağrısında yenilenir; kare başına tarama yok. İleride taşıma sistemi eklenirse occupiedGrids ve grid sahipliği de güncellenmeli; yalnız Transform taşımak yetmez.
- `GetFinalStat` mevcut global version/localDirty cache'ini kullanır. Kural asset'ini Play sırasında değiştirirsen saksı Inspector'ındaki Refresh düğmesine bas; sürekli Inspector taraması yok.
- `PlantHealth` hedef saksının hasar çarpanını uygular. XP mevcut PlantResource akışından, hız mevcut PlantSpawner akışından geçer.
- Tile tooltip'inde saksının aktif rezonansları, saksı Inspector'ında da adet/eşik/çarpan görünür.

## Deneme

Play Mode'da yerleştirilmiş saksıyı Hierarchy'den seç. PlanterBrain Inspector'ındaki örnek düğmeleri yeterli hücre varsa ilk 2/3/4 hücreye Damage, Water veya Fertile uygular. Bu bir Editor test kolaylığıdır; oyuncuya tile konumu seçme yeteneği eklemez. Örnek kurarken aynı saksının diğer hücrelerindeki modifier'ları temizler; Play Mode dışında kapalıdır. Oyundan çıkınca sahneye kaydolmaz.

`Tools/Resonance/Run Verification` hesaplama, eşik değişimi, hücre değişimi, saksı izolasyonu ve hasar aktarımı kontrollerini çalıştırır. Test geçmesi tam oyun/ekonomi dengesi onayı değildir.

## 2026-09-14 — açılış efekti, HP ve kart değerleri

- Rezonans ilk açılınca veya daha yüksek adet eşiğine geçince VFXManager bir ResonanceBurst oynatır. Aynı eşik tekrar hesaplanınca veya aşağı düşünce efekt tekrarlanmaz. Tamamen kaybolup yeniden açılması yeni açılış sayılır. Yerleşimde de RefreshTileBuffs çağrıldığı için mevcut tile'ların üstüne konan saksı efekti gösterir.
- Efekt unscaled zaman kullanır; kart/yerleşim ekranındaki timeScale=0 sırasında da tamamlanır. Halka, 20 parçacık ve yüzde bonus yazısı içerir. Paylaşılan Resources/ResonanceBurst.mat kullanılır, en fazla 16 efekt nesnesi tutulur; kapasitede eskiler yeniden kullanılır. Global kamera sarsıntısı veya gameplay patlaması eklemez.
- TileBuffText ortak yüzde metnini üretir. x2 = +100%, x10 = +900%, üretim süresi x0.5 = -50%. Crystal flat değeri zaten yüzde puandır; 15, +15% puan diye gösterilir, +1500% yapılmaz. Flat hasar/adet/saniye gibi yüzde olmayan birimler sahte yüzdeye çevrilmez.
- TileCardOffer kart açıldığında tek bir roll saklar. CardUI bu roll'ü rarity altındaki Buff Value alanında gösterir. Seçim -> ProgressionManager -> GroundCell aynı listeyi taşır; hücre kendi kopyasını saklar, yeniden roll etmez. Aynı SO iki kartta çıkarsa her slot ayrı offer ve roll taşır.
- Tooltip'te 'Bu tile' ve 'Saksının rezonansları' ayrılır. Energy popup'ında görünen Odak bonusunun Energy'den geldiği izlenimini azaltır. Energy'nin kullanılmayan eski HarvestDamage bağlantısı bu değişiklikle aktive edilmez.
- PlantHealthScaling.asset Common HP eşiklerini ve rarity çarpanlarını içerir. Ara roundlar geometrik hesaplanır. Varsayılan PlantSO.maxHealth=10 referanstır; 20 yaparsan o bitkinin eğri HP'si iki kat olur. Tablonun dışı ilk/son eşikte sabitlenir.
- Common HP: R1=5, R10=15, R25=35, R45=80, R65=180, R80=1500, R95=9000, R110=45000, R120=100000, R130=180000. Rarity x1/x1.2/x1.6/x2/x3; Legendary R130=540000. HP doğumda hesaplanır, yaşayan bitki round değişiminde iyileşmez/büyümez. Player hasarına adaptif değildir.
- RoundManager sahnedeki maxRounds=10 değeri, skill tree hasarı ve fiyatları değiştirilmedi. 130-round eğrisi hazırlanması, 130-round ekonomi dengesinin tamamlandığı anlamına gelmez.
- `Tools/Resonance/Verify HP Cards and Unlock`: HP sınır/ara değerleri, kart roll aktarımı, yüzde formatı ve efekt eşiklerini kontrol eder; Temp/ResonanceChecks içine izole kart/efekt önizlemeleri üretir.

## Kart seçiminden sonra rezonans sunumu

- Buff hesaplaması anında çalışır. Yalnızca CardSelection sırasında oluşan görsel bildirim VFXManager içinde bekler.
- Her saksının ilk değişim öncesi rezonans listesi saklanır. Birden fazla kartla aynı saksı yükselirse son aktif eşikler bu başlangıçla karşılaştırılır; eski ve yeni kademeler üst üste gösterilmez. Silinen saksılar veya artık aktif olmayan kazanımlar atlanır.
- Son karttan sonra UIManager kartı ve tur sonu panellerini kapalı tutar. Bir kare sonra bekleyen efektleri oynatır; 1.1 saniyelik efekt ve 0.15 saniye pay sonrasında tur sonu panelini açar. RoundEnd durumunda oyun durmaya devam eder; round süresinden tüketmez.
- Bekleyen bildirim yoksa tur sonu paneli hemen açılır. Kart seçimi dışındaki yerleşim efektleri anında gösterilir. Oyun durumu değişirse bekleyen panel açılışı iptal edilir; ana menü/yeni koşu/koşu bitişi bildirim kuyruğunu temizler.
