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

Yeni Damage ailesi Energy'yi değiştirmez. Common/Rare/Epic/Legendary tekli hasar bonusları +%20–30 / +%35–50 / +%60–80 / +%100–150. Farklı rarity'ler aynı Damage ailesinde sayılır. Yeni kartlar GameScene CardSelectionUI.allModifiers havuzuna eklendi; tür ağırlığı 15 (yüzde değil, mevcut ağırlıklara eklenir). Mevcut rastgele hücre seçimi korunur.

100 oyuncu hasarı ve tam +%25 roll edilmiş 3 Damage: (1+0.25+0.25+0.25) x4 = x7; doğrudan vuruş 700 olur. Başka saksıda bonus yoksa hâlâ 100 vurulur. Kritik/variance oyuncuda hesaplanır; saksı çarpanı hedefte bir kez uygulanır. Hasar yazısı da gerçek çarpılmış değeri gösterir. Patlama hasarı bu prototipte rezonansı almaz; mevcut zincirleme patlama davranışı korunur.

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
