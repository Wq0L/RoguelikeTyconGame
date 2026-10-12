# Geç Oyun Testi — çekim araçları

Tools > Geç Oyun Testi (Bölüm 3.8 · yalnız Play), pencerenin üstündeki Çekim / test araçları bölümü.

- Hedef round: profilin 1–MaxRounds aralığı. Aynı round yeniden başlatılabilir.
- Kaynak ve miktar: Gold / Iron / Stone, mevcut bakiyeye eklenir; negatif/zero miktar engellenir.
- Round süresine +60 saniye: yalnız aktif round için.

Round geçişi mevcut saksıları, bitkileri, kaynakları ve alınmış güçleri korur. Atlanan round'ların
ödüllerini, seviyelerini veya tarla genişlemelerini otomatik vermez. Önceki round'un geçici
olayları, saldırı zamanlayıcıları ve bekleyen seçimleri temizlenir; kota başlangıcı yeni noktaya
alınır ve normal StartRound yolu kullanılır. Demo'da bu araçlar kapalıdır.

İlk test komutunda MetaSave, mevcut yüklenmiş veriyi koruyarak bu Play oturumu boyunca disk
yazımını kapatır. Oyun kapatılıp yeni Play açılana kadar test ilerlemesi kalıcı kayda yazılmaz.
Seçili profil veya profil asset'i değiştirilmez. Bütün yeni runtime girişleri UNITY_EDITOR
korumasındadır; oyuncu build'ine girmez. Yeni üçüncü taraf içerik yok, commit yok.

Kaynaklar: Assets/Editor/LateGameTestWindow.cs; RoundManager, BossRewardManager,
SpecializationManager ve MetaSave içindeki editor-only girişler.
Doğrulama: CaptureToolsVerification.RunBatch; izole GameScene'de 16 kontrol geçti.
Rapor: CaptureToolsVerification.txt. Kaynak bakiyeleri, ileri/geri/aynı round, sınır değerler,
tarla/ödül korunması, eski dalga temizliği ve bellekte kayıt koruması kontrol edildi.

## Bütün boss buff’ları (2026-10-11)

Aynı pencerenin boss bölümünde aktif profilin tüm aşamalarındaki ödüller listelenir.
Stat, kırılma, bedelli ve çalışma zamanı etkili ödüller dahildir. Liste artık yalnız
GecOyunV1 klasöründeki effect alanı dolu asset’lerle sınırlı değildir. Profil havuzu
bulunamazsa tam sürüm Geç Oyun V1 havuzu kullanılır; farklı denge profillerindeki aynı
ödül kopyaları birleştirilmez. Gelecekteki sınırsız mod veri alanı dahil değildir.

Buff ara: ad veya kimlik. Yenile: havuzdaki düzenlemeleri yeniden oku. Satırlarda
alınan adet / sınır ve alınamama nedeni görünür; açıklama için ada fareyle gelinir.
Ver düğmesi, tekrar alınabilen ödülleri sınırına kadar ekler. Ön koşul, dışlama,
koşul, bekleyen teklif ve yerleşim denetimleri korunur. İlk Ver komutu oturumun
kalıcı kayıt yazımını kapatır. Demo’da düğmeler kapalıdır.

İnsan Play kontrolü: aramayla eski bir stat buff’ını bulup iki kez ver; Hilal’i
bulup ver; ön koşulu eksik Kapalı Devre satırının nedenini kontrol et.

Doğrulama: BossBuffWindowVerification.RunBatch, izole GameScene’de 13 kontrol geçti;
Geç Oyun V1 havuzunun 32 ödülü eksiksiz bulundu. Stat buff’ını tekrar verme, sınırda
reddetme, Hilal verme, eksik ön koşulu reddetme, arama ve kayıt koruması doğrulandı.
Rapor: BossBuffWindowVerification.txt. Unity çıkış kodu 0. Kapanış sırasında Unity’nin
kendi SearchDatabase/IndexationOnStartup kodunda bir indeks istisnası kaydedildi;
ödül testi çağrı zincirinden gelmiyor. İnsan Play görsel onayı yapılmış sayılmaz.
