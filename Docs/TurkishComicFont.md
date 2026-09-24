# Türkçe comic font düzeltmesi

Projede bulunan LilitaOne-Regular.ttf içinde Ğ, ğ, İ, Ş ve ş yoktu. TMP bu beş harfi Barlow-SemiBold'dan aldığı için aynı kelime içinde farklı şekil ve kalınlık görülüyordu.

Harvest Comic TR, orijinal comic gövdeleri ve metrikleri koruyan OFL lisanslı, yeniden adlandırılmış destek fontudur. Orijinal TTF değiştirilmedi. Yeni TMP asset ilk fallback, Barlow ikinci fallback olarak bağlıdır. Mevcut LilitaOne SDF referansını kullanan menü, HUD, skill, grid ve yerleştirme metinleri bu düzeltmeden yararlanır. ComicUIBuilder yeniden çalıştırıldığında da bu sıra korunur.

Destek atlası 90pt / 12px padding ile static olarak hazırlanmıştır; Türkçe harfler build sırasında kaybolmaz veya runtime atlas üretimine ihtiyaç duymaz. Orijinal fontla ölçek ve padding oranı aynıdır. Popup girişleri NFC'ye normalize edilerek birleşik aksanların da aynı harfe dönüşmesi sağlanır.

`SimpleResonanceVerification` tüm büyük/küçük Türkçe harflerin ana comic font veya Harvest Comic TR üzerinden çözüldüğünü denetler. Görsel örnek `Logs/ComicPopups.png` içinde yer alır.
