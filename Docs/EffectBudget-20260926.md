# Efekt bütçesi — 26 Eylül 2026

- VFXManager: hit, patlama ve hasar yazısı havuzlarının her birinde 121 oluşturulmuş nesne üst sınırı. Toplam tek bir 121 değil, tür başına 121. Başlangıç havuzları küçük kalır; 121 nesne zorla önceden oluşturulmaz.
- Sınır dolduğunda yeni görsel atlanır; oynayan görsel kesilmez. Hasar ve ödül hesapları VFX dışındadır. Hasar yazısı atlanırken mevcut dört Random.Range tüketimi korunur.
- Tornado, bumerang, elektrik, rezonans, saldırı halkası ve kaynak uçuşlarında mevcut daha düşük sınırlar korunur. Bu davranışları 121'e yükseltmek dengeyi ve yükü artırırdı.
- Patlama ve hit başına coroutine oluşturma kaldırıldı. Yönetici içindeki önceden kapasiteli listeler bitişleri izler. Patlama büyüme eğrisi, ölçekleri, emission süresi, renkler ve particle IsAlive ile dönüş korunur. Coroutine sonrası faz yerine Update kullanıldığı için bitiş zamanı bir kare farklı olabilir.
- Manager devre dışı kalınca takip edilen hit/patlamalar temizlenip havuza döner.
- Hasar sayıları 11 karakterlik yeniden kullanılan tamponla TMP'ye verilir; int sınırları dahil, her gösterimde ToString yok.
- GoldUI aynı görüntülenen sayıyı tekrar string olarak üretmez. N0 biçimi ve uçuşla gelen ödül gösterimi korunur.

## Doğrulama ve sınırlar

İzole Unity testinde 121 eşzamanlı lease, 122'nci isteğin reddi, iade sonrası yeniden kullanım; 121000 bitki hasadı, 20000 halka ve CSV testleri çalıştırılır. Test dosyaları Logs/PerformanceStressVerification-* altında. Bu CPU/lifecycle testi tam oyun görüntü karşılaştırması veya GPU benchmark değildir.

200 FPS garantisi verilmez. Kayıttaki D3D12 sunum/scratch beklemelerinin ve Editor maliyetinin tümü bu değişikliklerle ortadan kalkmış sayılmaz. Aynı dolu R12 kurulumu ve aktif hasatta yeni Profiler kaydıyla P50/P99, SetPass, particle geometry ve GC karşılaştırılmalıdır. Görsel sınır altındayken mevcut stiller korunur; sınır üzerindeki tüm efektleri birebir göstermek ve aynı anda 121 sınırını korumak mümkün değildir.
