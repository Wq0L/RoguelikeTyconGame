# Planter Shop UI denemesi

Sahne: `Assets/Scenes/GameScene.unity`

Hierarchy: `Placment Shop Panel / Planter Shop UI`

36 UI nesnesi sahneye kaydedildi; Editor'da seçilip düzenlenebilir. Kaynak sprite dosyaları değiştirilmedi. İkonların şeffaf kenarları RectTransform boyut/konum ayarıyla telafi edildi.

## Akış

- Mağazayı açınca beş saksı kartı görünür.
- Bir karta basınca diğer kartlar sağa kayıp solar, seçili kart solda kalır, sağda özellikler ve Buy görünür.
- Back to List animasyonu geri oynatır.
- Buy, mevcut kaynak miktarını kontrol eder. Yetersiz kaynakta düğme kapalıdır ve eksik miktar yazılır.
- Başarılı satın alma mevcut PlacementManager'ı çağırır. R ile dönüş ve mevcut iptal/iade davranışı devam eder.
- Animasyonlar Time.timeScale=0 durumunda unscaledDeltaTime ile çalışır.
- Panel kapanınca animasyon durur ve liste görünümü sıfırlanır; kaynak/stat event abonelikleri kaldırılır.

Özellikler mevcut PlanterSO ve global stat verilerinden okunur. Yeni avantaj/dezavantaj veya oyun kuralı eklenmedi. Mağaza dili mevcut Buy/Shop akışına uygun olarak İngilizce tutuldu.

## Değişiklik kapsamı

- Yeni `Assets/Scripts/UI/PlanterShopPanelUI.cs` ve `.meta` dosyası.
- GameScene altında yeni UI parent ve çocukları.
- Eski beş satın alma düğmesi silinmedi; kapalı bırakıldı. Eski Sell düğmesi korundu.
- Kaynak, üretim, stat, saksı prefabı ve yerleştirme scriptleri bu işlemde değiştirilmedi.

## Kontrol

Yeni script dahil C# derlemesi başarılı. Sahne içi bağlantı, mevcut objelerin korunması ve sprite/SO bağlantı kontrolleri yapıldı. Unity Play Mode'da görsel ve uçtan uca etkileşim doğrulaması bu ortamda yapılmadı.

Unity'de denenecekler: mağaza açma; beş kartı sırayla seçip geri dönme; parasız Buy; başarılı satın alma ve yerleştirme; yerleştirmeyi iptal edip mağazayı yeniden açma; animasyon ortasında ESC ile çıkıp yeniden açma.

## Yedek

`GameScene.before.unity` bu denemeden hemen önceki sahnenin kopyasıdır. Sonraki sahne düzenlemelerini korumak için doğrudan üzerine yazmak yerine gerekirse değişiklikleri seçerek geri al. Yalnızca görünümü geri denemek için yeni `Planter Shop UI` parent'ını kapatıp önceki beş satın alma düğmesini yeniden açabilirsin.
