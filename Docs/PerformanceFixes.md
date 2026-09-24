# Kritik performans düzeltmeleri — 24 Eylül 2026

## Yapılan değişiklikler

- `Assets/Scripts` içindeki normal `Debug.Log` teşhis mesajları yorum satırına alındı. Hata ve uyarılar korunuyor. GameManager'ın yalnız log basan Update metodu kaldırıldı; teşhis satırı yorum olarak duruyor. Sadece log için dolaşılan modifier listeleri de yorumlandı. Üçüncü taraf paketlerdeki kayıtlar değiştirilmedi.
- Saldırı halkası artık her saldırıda GameObject/LineRenderer/Material üretmiyor. VFXManager başlarken dört halka ve bir ortak materyal oluşturuluyor; halka geometrisi bir kez hesaplanıyor. Fazla kozmetik talep varsa halka tekrar kullanılıyor. Manager yok edilirken materyal de temizleniyor.
- Hit flash coroutine'leri yerine tekrar kullanılan flash kayıtları var. Üst üste vuruşlar aynı renderer kaydını yeniliyor. Bitki havuza dönerken flash iptal ediliyor ve önceki MaterialPropertyBlock geri yükleniyor; eski flash yeni doğumu etkilemiyor.
- Sahne başına, prefab türlerine ayrılmış `PlantPool` eklendi. Bitkiler ilk ihtiyaçta üretiliyor; sonraki doğumlarda tekrar kullanılıyor. Tür başına en fazla 256 pasif bitki tutuluyor. Bu aktif bitki/spawn limiti değildir; taşan pasifler silinir. İlk kullanımda prewarm garantisi yoktur.
- Ölümden dönüş LateUpdate'a ertelendi. Ölüm eventi, ödül ve davranış callback'leri tamamlanmadan aynı nesne başka bitkiye verilmez. Çift iade engelleniyor. Sahne kapandığında havuz da kapanıyor.
- Spawn verileri aktivasyondan önce atanıyor. Can, round, owner, elektrik XP bilgisi yeniden kuruluyor; pasifleşirken grid ve owner/veri referansları bırakılıyor. Satış/temizlik ödül üretmiyor. Spawn aralığı ve nadirlik hesabı değiştirilmedi.
- Bumerang artık yalnız nesne referansını değil, bitkinin yaşam numarasını da hatırlıyor. Aynı nesnenin yeni doğumu yanlışlıkla önceki hedef sayılmıyor.

## Testleri çalıştırma

```powershell
& Tools/run-simple-resonance-tests.ps1 -Performance
& Tools/run-simple-resonance-tests.ps1
```

Runner, kullanıcı sahnesini açıp değiştirmek yerine `Library/VerificationProject` içinde ayrı Unity projesi oluşturur. Ana Editor açılışında temizlenebilen Temp dizini bu amaçla kullanılmıyor. Gerçek bitki prefabları ve GUID/shader include bağımlılıkları kopyalanır. Çıktılar `Logs/PerformanceStressVerification.txt` ve `Logs/SimpleResonanceVerification.txt` dosyalarına alınır.

Performans testi şunları doğrular:

- 12 gerçek prefabda tekrar kullanım ve elektrik/direct XP ayrımı.
- Gerçek PlantSpawner.Update üzerinden 0.5 saniyelik timer, occupied bekleme, ölüm sonrası timer reset, duraklatma/devam ve ödülsüz kaldırma.
- Aynı prefab tekrar kullanılırken Gold/Iron/Stone veri değişimi; yeni owner ve R130 can hesabı.
- 121 bitki × 1.000 dalga = 121.000 havuzlu ölüm. Ölülere ikinci kez hasar verildiğinde ödül artmaması; havuzun ısınma sonrası sabit kalması.
- 121 bitki × 100 dalga Instantiate/Destroy referansıyla dar kapsamlı CPU karşılaştırması.
- 20.000 halka isteğinde sabit materyal/renderer sayısı ve kapanış temizliği.
- Aktif bitki bulunan ayrı sahne unload edildiğinde havuzun ve bitkilerin temizlenmesi.

## Ölçüm sınırları

Son tamamlanan koşu: 24 Eylül 2026, 22:18; Unity 6000.3.14f1, Ryzen 7 7800X3D / RTX 5080. Bu düşük donanım değildir.

| Kontrol | Sonuç |
| --- | --- |
| Stres paketi | PASS; runtime hata/istisnası yok |
| Mevcut regression paketi | PASS; 291 kontrol |
| Yoğun yaşam döngüsü | 121.000 havuzlu hasat; ikinci hasar çağrısına rağmen tek ödül |
| Havuz | İlk yoğun dalgadan sonra 132 toplam nesnede sabit; 121 yoğun test nesnesi ve diğer 11 prefabın önceden kullanılan örnekleri. Sonunda 0 aktif, 0 bekleyen iade |
| Materyaller | 1.000 havuzlu dalga boyunca sayı değişmedi |
| Halka | 20.000 çağrıda 4 renderer / 1 materyal; manager silinince 0 halka materyali |
| Senkron dalga CPU ortalaması | Instantiate/Destroy referansı 3.831 ms → havuzlu 0.991 ms |
| Havuzlu P50 / P95 / P99 | 0.903 / 1.435 / 1.626 ms |
| Managed allocation | 16 KB kalibrasyonu başarısız; ölçüm kullanılamıyor |
| Editor altyapısı | 1 SearchDatabase başlangıç istisnası ayrıca kaydedildi; gameplay sonucuna dahil değil |

CPU sayıları bu koşunun sonuçlarıdır; önceki tekrarlarla birlikte referans yaklaşık 2.62–3.83 ms, havuzlu ortalama 0.91–0.99 ms aralığındaydı. Editor ve arka plan yükü ölçümü etkiler. Bunu bütün oyun için aynı oranda FPS kazanımı diye yorumlamamak gerekir.

Bu test, GameScene'in tüm VFX/UI/render yükünü içeren bir FPS benchmark'ı değildir. Dalga süreleri yalnız senkron oluşturma/kiralama, başlatma ve hasat çağrılarının süresidir; sonraki LateUpdate/Destroy/render işi hariçtir. Dalga başına 121 ölüm, normal spawn timer'ını beklemeden yaşam döngüsünü zorlar. Timer kuralları ayrıca gerçek zamanlı test edilir.

Mono runtime üzerindeki `GC.GetAllocatedBytesForCurrentThread` önce 16 KB allocation ile kalibre edilir. Kalibrasyon başarısızsa sıfır allocation iddia edilmez, sayaç kullanılamıyor diye raporlanır. Sabit obje/materyal sayısı tüm uygulamada bellek sızıntısı olmadığını kanıtlamaz.

İzole Editor'de karşılaşılabilen `UnityEditor.Search.SearchDatabase` başlangıç istisnası ayrı sayılır; otomatik Error Pause'dan çıkılır. Gameplay hata/istisnaları testin başarısız olmasına neden olur. Sonuç dosyası bu ayrımı açıkça içerir.

Genel hasar yazısı/patlama/hit efekt havuzlarının bütçelendirilmesi, grafik kalite profilleri, bumerang broadphase ve UI güncellemelerinin birleştirilmesi bu değişiklik paketinde yapılmadı. Bunlar önceki performans raporundaki sonraki iyileştirmelerdir; düşük PC için son FPS kabulü ayrıca gerçek cihazda yapılmalıdır.
