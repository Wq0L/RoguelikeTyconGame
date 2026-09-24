# Krem comic bilgi panelleri

Grid tile, skill ve yerleştirme açıklamaları `ComicPopupView` kullanır. Oyun verileri ilgili provider tarafından hazırlanır; ortak view yalnızca çizim, ölçüm ve kaydırmadan sorumludur. Mevcut prefab referansları ve TooltipManager havuzu korunur. Eski prefab çocukları yalnızca runtime instance üzerinde kapatılır.

- İçerik metnin gerçek preferred height değeriyle ölçülür. Her rezonans ayrı, tekrar kullanılan ikonlu satırdır. Üçüncü ve sonraki satırlar paneli büyütür.
- Panel güvenli ekran alanına ve Canvas ölçeğine göre sınırlandırılır. Sığmayan içerik RectMask2D ile kırpılır, PgUp/PgDn ile kaydırılır. Klavye seçimi, skill ağacı zoom ve saksı yerleştirme mouse girdisiyle çakışmayı önler.
- Yerleştirme açıklaması Alt ile açılıp kapanır; tercih ardışık yerleştirmeler arasında korunur. Ghost ikonları bağımsızdır. Panel normalde sağdadır; ghost sağ kenardaysa sola geçer.
- Panel raycast yakalamaz; yerleştirme girdisini engellemez. Kapatma Alt üzerinden yapılır.
- Footprint açıklaması yalnızca family sayısına değil gerçek hücreye, rarity ve roll değerlerine göre güncellenir. Rezonans hesabı mevcut ResonanceManager üzerinden gelir.
- Yeniden kullanımda kaydırma sıfırlanır, artık kullanılmayan satırlar kapanır. Ekran boyutu/Canvas scale değişince yeniden ölçülür.
- Başlıklar, açıklamalar ve yardımcı metinler LilitaOne comic fontunu, krem zeminde okunaklı koyu mürekkep rengiyle kullanır. Başlık 28, içerik 24, kademe 20 punto; butonların beyaz konturlu stili değişmez. Yuvarlak krem yüzey, koyu kontur ve amber başlık çözünürlükten bağımsız UI geometrisidir; onaylanan konseptin runtime uyarlamasıdır.

## Skill ikonları

`SkillIconCatalog` skill'in unlockType veya ilk etkisine göre ikon seçer; node adı, maliyeti ve satın alma durumu sınıflandırmayı etkilemez. Hasar, saldırı hızı, alan, kritik, gold, iron, stone, XP, skor, nadirlik, spawn, süre, grid, saksı, kart ve davranışlar için ayrı comic semboller vardır. Düğüm ve popup aynı sınıflandırmayı kullanır. Popup ikonu başlığın solundadır.

İkonlar mevcut `ResonanceBadgeGraphic` çizim sisteminin vektör geometrisiyle üretilir. Texture yükleme veya her frame sprite üretme yoktur. Düğüm başına bir kez oluşturulur; yenilemede yeniden kullanılır. Eski node.icon sprite verileri değiştirilmez, runtime sunum katmanı kategori ikonunu gösterir. İkonlar raycast yakalamaz. Popup havuzunda sonraki tile için skill ikonu temizlenir.

Doğrulama: `Tools/run-simple-resonance-tests.ps1`. Kullanıcının açık Unity projesini kapatmadan izole test projesinde çalışır. Görsel çıktı: `Logs/ComicPopups.png` (örnek değerli UI test sahnesi); oyun ekranı değildir. Testler gameplay regresyonlarını, üçüncü rezonansla büyümeyi, on satırlı içerikte kaydırmayı, havuzda küçülmeyi ve Alt/ikon bağımsızlığını kapsar.
