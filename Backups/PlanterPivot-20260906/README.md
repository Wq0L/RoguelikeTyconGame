# Saksı pivot düzenlemesi

Değişiklik öncesindeki 3 script ve 5 prefab `Assets` alt klasöründe yedeklendi. Prefab `.meta` dosyaları ve GUID'leri değiştirilmedi.

## Yeni saksı ekleme

1. Mevcut bir saksı prefabını ve PlanterSO dosyasını çoğalt. Yeni SO'nun prefab alanına yeni prefabı bağla, `sizeX / sizeZ` ve oyun verilerini ayarla.
2. Prefabın ana objesinde konum ve dönüş sıfır, ölçek 1 olsun. `Visual` objesi de sıfır konum/dönüş ve 1 ölçek kullansın.
3. `Visual` içindeki eski modeli yeni modelle değiştir. Modelin taban merkezi ana objenin merkezinde olsun; modelin kendi yön/ölçek düzeltmesini yalnızca modelde yap. Mevcut sahnede hücre aralığı 2 birimdir. Sistem keyfi model boyutunu otomatik tahmin edip ölçeklemez.
4. `PlanterBrain > Spawn Points` listesini boşaltırsan her dolu hücre için otomatik doğma noktası kullanılır. `Automatic Spawn Height` ile yerden yüksekliği ayarla. Özel görünümlerde elle noktalar kullanabilirsin; aynı hücre artık iki spawner tarafından paylaşılmaz. Mevcut saksıların elle ayarlanmış noktaları korundu.
5. `GhostController > Real Model` alanı `Visual` olsun. Geçerli/geçersiz malzemeleri kopyalanan prefabta hazırdır. Ayrı önizleme modeli gerekmez. Eski prefablardaki kapalı `GhostMode / PlanterGhostMode` nesneleri uyumluluk için tutuldu ve kullanılmıyor.
6. Yeni SO'yu mağaza butonuna bağla. Yerleştirme sırasında PlanterBrain seçilen SO'yu alır; kopyalanan eski veri bağlantısıyla çalışmaz.

## Kontrol

Kod derlemesi ve `node Backups/PlanterPivot-20260906/verify.cjs` kontrolü yapıldı. Unity Play Mode ile uçtan uca oynanış doğrulanmadı.

Unity'de her boyutu 0/90/180/270 derecede dene; önizleme/yerleştirme örtüşmesini, dolu-kilitli-grid dışı hücre reddini, turda bitki üretimi/hasat/buff/patlamayı ve saksı satışı sonrası alanın boşalmasını kontrol et.

## Geri alma

Unity'de Play Mode'dan çık. Proje klasöründeki PowerShell terminalinde:

```powershell
& './Backups/PlanterPivot-20260906/Restore.ps1'
```

Bu işlem yalnızca yedeklenen 8 dosyayı geri koyar. Düzeltmeden sonra bu dosyalarda yeni değişiklik varsa onları ezmek yerine durur; o durumda değişikliklerin seçilerek geri alınması gerekir. Bu klasördeki yedekleri sakla.
