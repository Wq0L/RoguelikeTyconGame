# Materyal / instancing / gölge düzenlemesi

## Uygulanan değişiklikler

- Assets/Materials/GameToon altındaki 10 mevcut oyun materyalinde enableInstancing açık. Bitki (2), saksı (1), zemin (3), kilitli zemin (1), çevre (3). Renk, doku, outline, render queue ve shader seçimi değiştirilmedi. Bunlar zaten ortak referanslar; farklı görünümlü materyaller körlemesine birleştirilmedi. Kullanılmayan Plants_Optimized.mat yerine sahnenin gerçek bağımlılıkları düzenlendi.
- STUniversal.hlsl: _BaseColor, _ToonFlashColor, _ToonFlash instanced property olarak tanımlandı. Instancing olmayan varyant UnityPerMaterial alanlarını korur. Tile tint ve hit flash artık aynı instanced draw içindeki nesnelerde farklı olabilir. Materyal kopyası üretilmez.
- PC_RPAsset: shadowCascadeCount 4 → 2; softShadowQuality High (3) → Medium (2). Shadow distance 50, ana gölge atlası 2048, ışık renkleri ve bias korunur. SRP Batcher açık kalır. Ana sahne ışığı hard shadow kullanıyorsa soft kalite değişikliğinin ona etkisi olmaz; cascade değişimi yine uygulanır.
- Efekt prefablarındaki mevcut gölge kapalı ayarları korunur. Hasar/ekonomi/spawn değişmez.

## Instancing ne zaman kullanılır?

Aynı mesh + aynı materyal + uyumlu render durumu gerekir. URP SRP Batcher uyumlu renderer'larda öncelik SRP Batcher'dadır. Bu değişiklik bütün nesnelerin otomatik tek draw'a birleştiği anlamına gelmez. Tile renkleri ve geçici hit flash MPB kullandığı için instancing yolu özellikle o gruplar için hazırlandı. SRP Batcher global kapatılmadı ve shader kasıtlı olarak tüm varyantlarda uyumsuzlaştırılmadı. Farklı bitki meshleri ve farklı saksı şekilleri ayrı gruplardır.

Frame Debugger'da dolu sahnede Draw Mesh (instanced), instance sayısı ve batch-break nedenleri incelenmeli. GPU Resident Drawer / RenderMeshInstanced ile tüm sahneyi yeniden yazma yapılmadı.

## Doğrulama

Assets/Editor/ToonRenderingVerification.cs ayrı Library/VerificationProject içinde çalıştırıldı. Default ve Outline toon shader normal pass derlemeleri, ardından gerçek DrawMeshInstanced ile iki nesnede farklı tint/flash renderı ayrı DrawMesh çıktısıyla piksel karşılaştırıldı. D3D11 ve D3D12 testleri PASS. Shader kaynaklı render hata mesajı yok. Raporlar Logs/ToonRenderingVerification-D3D11.txt ve -D3D12.txt.

Bu kontrollü test gerçek GameScene gölgelerinin görsel onayı veya FPS benchmark değildir. İki cascade gölge geçiş bölgelerinde keskinlik/dağılımı değiştirebilir; ilk ve dolu gridde kamera en uzak durumunda görüntü kontrolü gerekir. Gerekiyorsa yalnız cascade sayısı 4'e geri alınabilir. 200 FPS veya EXE çöküşünün çözümü garanti edilmez.

Kaynak: https://docs.unity3d.com/6000.0/Documentation/Manual/gpu-instancing-enable.html
