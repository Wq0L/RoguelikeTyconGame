# Doğuşta büyüme — 22 Ekim 2026

Büyüyen Ay alındığında ana hilalin görsel genişlemesi doğduğu anda başlar ve 0,3 saniyede tamamlanır. İlk isabet/hasat beklenmez; hasat geldiğinde animasyon baştan başlamaz. Genişleyen görsel hareket boyunca sabit kalır. Ödül yoksa bu büyüme oynatılmaz.

Tek ek Dolunay hilali baştan büyük çıkar ve hareket boyunca boyutu sabittir. Toplam bir ana + bir ek hilal düzeni, 3,2 birim/sn hız, 3/4 birim menzil korunur. Bu değişiklik görsel büyümeyi hasat sayacından ayırır: gerçek hasar büyümesi/sıfırlanması ve Dolunay büyüme eşiği mevcut hasat kuralını kullanmaya devam eder. Görsel genişlik bu nedenle hasar büyüme düzeyinin doğrudan göstergesi değildir.

İzole Unity GameScene, MoonPolishVerification.RunBatch: **102 PASS**, süreç çıkışı 0. İlk hasardan önce görsel büyümenin başlaması, ilk hasatta yeniden başlamaması, hasar büyümesi sıfırlanınca görselin sıçramaması, ek Dolunay'ın sabit büyük kalması ve 56 hasarı doğrulandı. Önceki hareket/menzil/sayı/havuz kontrolleri de geçti.

[Test sonuçları](MoonPolishVerification.txt)
