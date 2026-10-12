# Fareden çıkan Hilal — 10 Ekim 2026

Bu karar 9 Ekim tarihli rastgele tam satır / saldırı başına sayaç kuralının yerini alır.
Tam sürümde Hilal, saldırı anındaki fare hedefinden çıkar. Bağımsız RNG dört grid
yönünden birini eşit seçer: +X, -X, +Z, -Z. Yol doğumda açık tarla sınırına kadar
sabitlenir; daha sonra açılan hücreler mevcut dalgayı uzatmaz. Başlangıç hücresi de
ilk zamanlanmış adımda vurulur. Fare hücre merkezinde değilse ilk görsel geçiş oradan
hücre merkezine bağlanır. Sonraki adımlar seçilen eksendedir. Kenara dışa bakan dalga
başlangıç hücresini vurup biter; yön gizlice içeri veya dolu bitkilere çevrilmez.

Sayaç gerçek direct bitki isabetleriyle dolar: tek saldırıda 4 ayrı bitki hasar aldıysa
+4. Kritik, aynı bitki için ikinci puan değildir. Aynı rapordaki aynı bitki yaşamı
tekrar sayılmaz. Boş saldırı, ölü/hasar almayan hedef ve davranış/ödül saldırıları
ilerletmez. AttackFeed raporu yeni Damaged bilgisi taşır; mevcut Damage, Crit, Killed
alanlarının anlamı korunur. PlayerController hasardan önce canlılık ve hesaplanan
pozitif gelen hasarla bu bilgiyi verir.

Eşik 6. Örnek: 4/6 + dört isabet → bir Hilal ve 2/6. Tek geniş vuruş birden fazla
eşik doldurabilir. Mevcut dört aktif dalga kapasitesi korunur; kapasite fazlası önceki
politika gibi Skipped'a yazılır, sonradan patlayacak bekleme kuyruğu oluşturulmaz.
Kalan sayaç puanı korunur. Hasar .8, adım .07 sn ve büyüme/Dolunay kuralları değişmedi.
Dolunay doğan rotayı tersine, fare başlangıç hücresine kadar izler; dikey rota da destekli.

Eski profiller ve Demo dört hücre/saldırı sayacı davranışını korur. Serialized
crescentFullRows alan adı uyumluluk için korundu; true artık güncel fare rotasını
seçer. Yeni asset, ses veya dış içerik eklenmedi. Gerçek kayıt ve profil seçimi değişmedi.

## Kaynaklar

- Assets/Scripts/RunEffects/Effects/CrescentEffectSO.cs
- Assets/Scripts/RunEffects/Effects/CrescentEffect.Presentation.cs
- Assets/Scripts/RunEffects/AttackFeed.cs
- Assets/Scripts/Player/PlayerController.cs
- Assets/Scripts/ScriptableObjects/RunProfileSO.cs (Inspector açıklaması)
- Tools/Verification/Editor/MoonCursorVerification.cs

Güncel test girişi MoonCursorVerification.RunBatch; eski dört hücre kontrolleri
GecOyunV1Verification.RunLegacyCrescentBatch. MoonVerification ve
MoonSmallFieldVerification, 9 Ekim tam-satır davranışının tarihsel test kaynaklarıdır;
güncel fare kuralı için çalıştırılmaz. Art builder'ın test girişleri güncel teste bağlandı.

Play: Tek vuruşla dört canlı bitkiye vurup +4'ü, 6'yı aşınca kalanın korunmasını,
mouse çıkışını ve dört yönü kontrol et. Dikey büyüme ve altın dönüş aynı güzergâhta
kalmalı. Kenar/dışa yön seçimi kısa rota üretir; otomatik olarak içeri döndürülmez.

Son doğrulama: yeni fare rotası ve sayaç için 21 kontrol, eski profil/genel regresyon için 97 kontrol geçti (toplam 118); iki Unity koşusu da çıkış kodu 0. Raporlar: MoonCursorVerification.txt ve Cursor-LegacyRegression.txt. Commit atılmadı.
