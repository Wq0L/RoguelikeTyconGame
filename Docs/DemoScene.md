# DemoScene — 20 round erken alfa

DemoScene açılıp Play'e basılır. `Demo Settings` kökündeki `DemoSceneSettings` bileşeni demonun tek ayar merkezidir. Ayarları Play dışında değiştirin, sonra yeniden başlatın.

- Kaynak: `Run50_AlphaDengeV1`. Yalnız çalışma anında profil, denge, bitki canı ve ödül havuzu kopyalanır. Kaynak asset'ler ve Resources/RunProfileSelection değiştirilmez.
- 20 round; boss'lar R3, R6, R10, R13, R16, R20. R20 sonrası zafer, R21 yok.
- R3 erken; R6/R10 orta; R13/R16/R20 güçlü ödül aşaması. Demoda teklifler yalnız mevcut aşamadan gelir; uygun aday kalmazsa mevcut boş-teklif devam yolu geçerlidir.
- Tornado ve bumerang satın alma, kilit açma, kart havuzu, saksı şansı, normal ve zincir tetik yollarında engellenir. Ağaçta erişilebilir düğümleri “TAM SÜRÜMDE” gösterir.
- Varsayılan çiftçi ve tırpan kullanılır; tam oyun kaydındaki seçimler okunup uygulanmaz veya değiştirilmez. Demo görevlerden kalıcı içerik açmaz.
- Kota ve boss hedefleri kaynak profilin %50'si; bitki temel can eğrisi %75'i. Nadirlik oranları korunur. Bunlar ilk playfeel adaylarıdır, insan testiyle dengelenmiş sayılmaz.
- Süreler kaynak profilden: R1–5 45 sn, R6–10 55 sn, R11–20 65 sn. Toplam 19 dakika 10 saniye aktif süre; mağaza ve seçimler hariç.
- Demo kendi GameManager'ını gerektiğinde kurar. Tek sahneyle çalışır. Açılış paneli demo sınırlarını açıklar. Sonuç ekranındaki yeniden başlat / ana menü tuşları DemoScene'e döner; MenuScene/GameScene yüklenmez.

## WebGL

`Tools > Demo > Build WebGL Demo` komutu yalnız DemoScene'i build eder; tam oyunun Build Settings sahne listesini değiştirmez. Unity WebGL Build Support gerekir. Decompression Fallback yalnız bu build sırasında açılır ve önceki ayar geri konur.

Çıktının içindeki `index.html`, `Build`, `TemplateData` ve varsa diğer dosyaları birlikte ZIP yapın; ZIP kökünde index.html olmalı. itch.io HTML projesine yükleyin, tarayıcıda oynanacak dosya olarak işaretleyin. Gerçek WebGL performansı/tarayıcı testi ayrıca yapılmalıdır.

## Doğrulama

`Tools/run-isolated-verification.ps1 -Method DemoVerification.RunBatch -Full`

Test izole proje kopyasında çalışır: 20 round akışı (skor verilerek), ödül aşamaları, kart ve satın alma kilitleri, kayıtlı açık başlangıç seçiminin engellenmesi, bitki canı/kota verisi, kaynak asset izolasyonu, zafer, restart, demo menüsüne dönüş ve GameScene'e geçince normal kuralların geri gelmesi. Denge veya insan oynanış testi değildir.

5 Ekim 2026: izole Unity testi geçti. R13/R16 teklifleri yalnız güçlü aşamadan, R20 zafer ve R21 engeli doğrulandı. Açılış ve zafer görüntüleri incelendi. Sonuç: [DemoVerification.txt](DemoScene/DemoVerification.txt). Unity editörünün SearchDatabase başlangıç indekslemesinde oyun kodundan gelmeyen bir ArgumentOutOfRangeException görüldü; test bunu ayrıca kaydeder, oyun hatalarını saymaya devam eder. WebGL çıktısı bu çalışmada üretilmedi; tarayıcı performansı ve insan oyun hissi testi bekliyor.
