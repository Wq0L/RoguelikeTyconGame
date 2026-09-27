# Round 1 / Round 12 Profiler karşılaştırması

26 Eylül 2026. Unity 6000.3.14f1 RawFrameDataView ile iki .data dosyası ayrı, grafik arayüzü olmayan analiz projesinde okundu. Oyun kodu veya sahne değiştirilmedi. Dışa aktarılan veriler Logs/ProfilerAnalysis altında. Süreler Main Thread frameTimeMs; EXE FPS ölçümü değildir. Marker süreleri inclusive olduğundan birbirine eklenmemelidir.

## Kayıt kapsamı

- Round 1: clickerTycon_2026-09-26_14-02-29.data, 2000 kare. RoundMapUI.OnEnable frame 413: kayıt round sonu/yerleştirme geçişini içeriyor. Tamamı aktif hasat değil.
- Round 12: clickerTycon_2026-09-26_14-22-44.data, 2000 kare. Yaklaşık 121 PlantSpawner.Update çağrısı/kare; ortalama 33 FloatingText.LateUpdate çağrısı/kare.
- Süre toplamları yaklaşık 3.62 ve 10.45 saniye. Bunlar iki dakikalık tam round kayıtları değildir.

## Ölçümler

| Ölçüm | Round 1 | Round 12 |
|---|---:|---:|
| Ortalama main thread kare süresi | 1.812 ms | 5.223 ms |
| P50 | 1.421 ms | 4.692 ms |
| P95 | 2.343 ms | 7.698 ms |
| P99 | 13.409 ms | 18.900 ms |
| 16.67 ms üzerindeki kare | 8/2000 | 34/2000 |
| En uzun kare | 76.568 ms | 52.679 ms |
| Medyan batches | 525 | 966 |
| Medyan SetPass | 14 | 399 |
| Medyan triangles | 121439 | 647241 |

Fark, değişen sahne/build yükü ile Editor maliyetini birlikte içeriyor; zamanla sızıntı oranı veya saf oyun performans gerilemesi şeklinde yorumlanamaz.

## Round 12 takılmaları

- Frame 503, 24.393 ms: ParticleSystem.ScheduleGeometryJobs 19.902 ms; içindeki D3D12.ReserveScratchMemory 19.811 ms ve CreateCommittedResourceWithTag 19.695 ms. Parçacık geometrisi hazırlanırken grafik kaynak ayırma maliyeti belirgin. Bunlar aynı iç içe çağrı yoludur, toplam alınmaz.
- Frame 1989, 31.725 ms: D3D12.WaitForLastPresentation.WaitForGPU 28.623 ms. Sunum/GPU senkronizasyon beklemesi; bu değeri doğrudan shader GPU çalışma süresi sayamayız. GPU zamanları 0, güvenilir GPU ölçümü yok.
- Frame 1867, 17.866 ms: GarbageCollector.CollectIncremental 13.776 ms, içinde GC.Collect 4.911 ms. O karede çöp toplama takılmaya katkıda bulunuyor. Kaynağı yalnız oyun koduna atfedilemez; Editor verisi.
- En büyük sıçrama frame 1852, 52.679 ms: EditorLoop 50.843 ms. Bunu EXE oyun kodunun 52 ms sürmesi gibi yorumlamamak gerekir.

Ortalama Canvas güncellemesi 0.389 ms; TMP Layout Text 0.198 ms (10.6 çağrı/kare); ParticleSystem.Update 0.253 ms. Normal script BehaviourUpdate ortalaması 0.201 ms. En büyük baskı yalnız PlantSpawner.Update değil (0.018 ms/kare toplam).

## Sızıntı değerlendirmesi

Round 12 içinde Object Count 30560 → 30561, Material Count 249 → 250, Mesh Count 225 → 225, Texture Count 1062 → 1062. Bu kısa pencerede sürekli nesne/materyal çoğalması görülmüyor. GC Used Memory 872 → 841 MB (ondalık); toplam kullanılan bellek yaklaşık 7.43 → 7.52 GB. Editor ve Profiler dahil bellek olduğundan oyun EXE'sinin belleği değildir; salt artış sızıntı kanıtı olmaz.

Round 12 pencere çeyreklerinde medyan kare süresi: 5.027 / 4.821 / 4.688 / 3.939 ms. Bu kaydın kendi içinde sürekli yavaşlama yok, son bölüm daha hızlı. Uzun oturumdaki düşüşü reddetmez; bu dosya o trendi yakalamıyor.

## Öncelik ve sonraki deney

1. Aynı max build'de patlama/hit partiküllerinin yalnız görsel üretimini kapatarak A/B ölç. Hasar, spawn ve ödüller aynı kalsın. Scratch allocation ve SetPass değişimine bak.
2. Aynı şekilde hasar yazısı görsellerini ayrı sınayarak TMP/Canvas ve çizim maliyetini ayır. Efekt sayısını sınırlama/birleştirme, materyal paylaşımı ve parçacık geometrisi bütçesi bulguya göre uygulanmalı.
3. Aynı grafik API ile karşılaştırmayı tamamladıktan sonra D3D11 kontrollü deney yapılabilir. Önceki native crash D3D12 scratch yolundaydı; bu kayıttaki benzer yol ilişki araştırmasını destekler ama ortak kök neden kanıtlamaz.
4. Aynı dolu build, aynı aktif hasat ve benzer round anında erken/geç kayıt al. Yeni saksı/skill alma. Kısa kayıtta artan nesne sayısı yok diye uzun süreli sızıntı dışlanamaz.

Bu incelemede optimizasyon veya gameplay değişikliği uygulanmadı.
