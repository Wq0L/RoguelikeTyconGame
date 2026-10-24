# Artçı Patlama — 22 Ekim 2026

Bu rapor aynı merkezli eski görsel geçişinin tarihsel kaydıdır. Güncel üç adımlı yayılma ve 128 efekt sınırı için [yeni rapora](../AftershockWave-20261022/README.md) bakın. Eski AftershockVisualVerification testi güncel yayılmanın kabul testi değildir.

- İkinci darbede mevcut CartoonVFX9x Comic Explosion Effect paketinin Battle_Effect_White prefabı kullanılır. Lisans: Docs/ThirdParty-Licenses.md.
- GameScene ve DemoScene referansları bağlandı. Kaynak prefab değiştirilmedi.
- Ayrı VFXPool<Transform>: başlangıç 4, üst sınır 32. Mevcut patlama oynatımı paylaşılır; her kayıt kendi havuzuna döner. Havuz dolması hasarı engellemez. Materyal örneği üretilmez; ParticleSystem dizileri nesne başına önbelleklenir. Emit durdurulur, kalan parçacıklar bitince temizlenerek iade edilir; OnDisable da tüm kiralamaları iade eder.
- Artçı ölçeği normal kaynak patlaması ölçeğinin 0,6 katıdır. Farklı prefab geometrileri olduğundan bu, ekran piksel boyutunun tam yüzde 60 olduğu iddiası değildir. İki kamera görüntüsü incelendi; artçı belirgin şekilde daha küçük.
- Artçıya ait kırmızı alan çizgisi çağrısı kaldırıldı; zincir gibi başka sistemlerin çizgileri değiştirilmedi.
- Hedeflerde normal patlama prefabı tekrarlanmaz. Gerçek PlantHealth.TakeDamage yolu korunur: flash, hit parçacığı, ses ve OnDamaged tepkileri. Hasar katsayısı, gecikme ve mekanik alan değiştirilmedi.

## Doğrulama
İzole gerçek GameScene; Unity 6000.3.14f1 batch çalıştırıldı. AftershockVisualVerification.RunBatch PASS. Normal/artçı görüntüleri ayrı ayrı incelendi. Test önce kaynak hücreyi gözlediği için başarısız oldu; geometri kaynağı dışladığından gözlemci gerçek komşu hedefe bağlandı ve yeniden çalıştırıldı.

Gerçek BehaviorEchoes zamanlayıcısı ile artçı ve hedef OnDamaged olayı, normal patlama çoğaltılmaması, kırmızı çizgi yokluğu, süre sonunda iade, 32 sınırı ve disable/re-enable kontrol edildi. Kapasiteyi zorlayan 360 istekte ısınma sonrası sekiz döngüde nesne/materyal/mesh sayıları sabit kaldı. Tam native/managed bellek profiler ölçümü yapılmadı; mutlak memory-leak garantisi verilmez. DemoScene referansı bağlandı; ayrı Demo oynanış koşusu yapılmadı.

Eski AlanGeriBildirimiVerification içindeki kırmızı artçı çizgisi bekleyen tarihsel kontroller yeni tasarımın kabul ölçütü değildir; bu turda o eski paket çalıştırılmadı.
