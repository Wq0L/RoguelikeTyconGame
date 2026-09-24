# Performans incelemesi — 24 Eylül 2026

Sonraki uygulama notu: Bu dosya düzeltme öncesi bulgulardır. Log, saldırı halkası, hit flash ve bitki pooling değişiklikleri için [PerformanceFixes.md](PerformanceFixes.md) dosyasına bakın.

## Sonuç ve kapsam

Zamanla FPS düşmesini açıklayabilecek iki güçlü aday var: her karede log üretimi ve her saldırıda oluşturulup açıkça serbest bırakılmayan materyal. Buna ek olarak hasar/hasat sıklığı arttıkça sınırsız kozmetik havuzları, bitki Instantiate/Destroy döngüsü ve çizim yükü büyüyor. Bunlar aynı sorun değil: sabit yük altında birikme ile ilerleyen build'in daha çok iş üretmesini ayrı ölçmek gerekiyor.

Bu çalışma kaynak kodu, GameScene üzerindeki ilgili değerler, kalite/URP ayarları ve gönderilen ekran görüntülerinin incelemesidir. Canlı CPU/GPU kaydı, Memory Profiler karşılaştırması veya düşük donanım benchmark'ı yapılmadı. Bütün üçüncü taraf assetlerin, texture importlarının ve shader varyantlarının tek tek doğrulandığı anlamına gelmez. Oyun kodu, asset ve denge değerleri değiştirilmedi.

## Görüntüler neyi gösteriyor?

- Stats görüntüsü: 224 FPS, 4.5 ms main, 3.3 ms render thread; 896 batch, 335 SetPass, 564 bin üçgen, 72 shadow caster, 2560×1440. O kare hızlı; uzun süreli kararlılığı ve düşük PC sonucunu kanıtlamıyor. Çok sayıda çizim ve üst üste transparan efekt, düşük donanım için araştırılması gereken yük.
- UI grafiğinin ölçeği yaklaşık 0.1–0.25 ms. Çok sayıda `Slider.value` etiketi gereksiz güncellemeye işaret ediyor; tek başına ağır FPS düşüşünü açıklamıyor.
- DOTween 200/50 → 500/50 → 500/125 kapasite genişletmiş. Bu bir hata veya tek başına tween sızıntısı kanıtı değil. Çalışma sırasında genişletme maliyet doğurabilir; kapasiteyi artırmak alttaki iş yükünü çözmez.
- Console 999+ log gösteriyor. Aşağıdaki her-kare logu bunun doğrudan kod kaynağı.

## Öncelikli bulgular

### P0 — GameManager her karede log yazıyor

Kaynak: `Assets/Scripts/Managers/GameManager.cs:23`, `Plants/PlantResource.cs:42,51`, `Managers/HarvestScoreManager.cs:28`.

`GameManager.Update()` sürekli interpolasyonlu `Debug.Log` çalıştırıyor. `timeScale = 0` Update'i durdurmadığından menü ve round aralarında da devam eder. 200 FPS örneğinde yalnız bu satır dakikada yaklaşık 12.000 log üretir; bu sayı ölçüm değil, hesap örneğidir. Her hasat ayrıca ödül ve skor logları üretir. Console/stack trace maliyeti özellikle Editor'da yanıltıcı derecede ağır olabilir.

Öneri: her-kare logunu kaldırmak; sadece state değişiminde gerektiğinde loglamak. Hasat loglarını string oluşturulmadan önce development/debug bayrağıyla kapatmak. Console Collapse aynı logun üretim maliyetini ortadan kaldırmaz.

### P0 — Saldırı halkası materyal yaşam döngüsü

Kaynak: `Assets/Scripts/Managers/VFXManager.cs:362–405`.

Her `AttackRingRoutine` yeni GameObject, LineRenderer ve `new Material(...)` oluşturuyor. Sonunda yalnız GameObject Destroy ediliyor; materyal için açık temizlik yok. Bu, yerel Unity materyallerinin birikmesi için güçlü bir adaydır; gerçek artış Memory Profiler ile doğrulanmalı. Sürekli 0.1 saniyelik saldırı aralığında üst sınır 600 yeni materyal/dakikadır; gerçek saldırı süresine bağlıdır.

Öneri: önceden oluşturulmuş halka havuzu, ortak materyal, önceden hesaplanmış birim çember ve toplu pozisyon güncellemesi. Materyalin sahibi ve kapanış temizliği açık olmalı. Unity de runtime materyallerin temizlenmesini geliştiricinin sorumluluğu olarak belirtir: [Renderer materials](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/renderer/materials).

### P1 — Kozmetik havuzlarında üst sınır yok

Kaynak: `Assets/Scripts/Pooling/VFXPool.cs:23`, `Efects/FloatingText.cs:31`, `Managers/VFXManager.cs`; GameScene başlangıç havuzları text=20, hit=15, explosion=16.

Havuz boşsa yeni nesne yaratılıyor; maksimum aktif/tutulan nesne sayısı yok. Yoğun bir patlama havuzu büyütür, nesneler sonra pasif olsa da bellekte tutulur. Bu tepe kapasiteyi tutmadır; kendi başına sürekli sızıntı değildir. FloatingText nesnesi başına kalıcı bir Sequence ve dört tween var; OnDisable Pause, OnDestroy Kill yapıyor. Bu tasarım DOTween kapasite uyarısını açıklayabilir.

Öneri: kozmetik sistem başına aktif/retained bütçesi, ölçülmüş kapasite kadar prewarm, taşmada yakın hasar yazılarını birleştirme veya en eski görseli yeniden kullanma. Ardından DOTween kapasitesini toplam gerçek ihtiyaca göre başlangıçta ayarlama. Hasarı, ödülü veya davranış tetiklenmesini iptal etmemek şart.

### P1 — Her bitki doğumu ve ölümü Instantiate/Destroy

Kaynak: `Assets/Scripts/Planters/PlantSpawner.cs:75`, `Plants/PlantHealth.cs:77`, `Plants/PlantBrain.cs`.

Bitkiler havuzlanmıyor. Spawn süresi 0.5 saniyeye yaklaştığında hızlı hasat yapan çok sayıda hücre yeniden üretim yükü çıkarır. Bu CPU, managed allocation ve native nesne yaşam döngüsü maliyetidir; doğrudan sızıntı değildir.

Öneri: bitki prefab türüne göre havuz. Tekrar kullanımda can, ölüm bayrağı, kaynak/XP bağlamı, owner/grid, efekt durumu ve event abonelikleri sıfırlanmalı. Mevcut `spawn → occupied → death → timer reset → next spawn` sırası korunmalı. Sadece Destroy'ı SetActive(false) yapmak yeterli değil; PlantBrain'in OnDestroy temizliğine dayanan yollar ayrıca ele alınmalı.

### P1 — Grafik profili düşük PC için ağır bir başlangıç

Kaynak: `Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset`, `ProjectSettings/QualitySettings.asset`.

PC varsayılan profili: HDR, depth/opaque texture gereksinimleri, 2048 ana ışık gölgesi, 4 cascade, 50 shadow distance, soft shadows; renderer'da SSAO (Downsample kapalı), FullScreenPass ve Simple Toon Outlines açık. SRP Batcher açık. Bu özelliklerin gerçek kamera/pass maliyeti GPU Profiler ve Frame Debugger ile ölçülmeli; assette destek açık olması her özellik için her kare aynı maliyet oluştuğu anlamına gelmez.

Önerilen Low profil başlangıcı: 1080p ve gerektiğinde 0.8 render scale; SSAO kapalı veya düşük çözünürlükte; 1–2 cascade, 1024 shadow map ve yalnız oyun alanını kapsayan shadow distance. Önemsiz küçük efektlerde gölge kapalı. Comic siluet korunacak şekilde outline kapsamı daraltılmalı. Fullscreen/depth/opaque bağımlılıkları incelenmeden topluca kapatılmamalı. Tekrarlanan tile/duvar/saksılarda ortak materyal ve uygun mesh birleştirme/instancing seçenekleri Frame Debugger ile karşılaştırılmalı. SRP Batcher'ın açık olması bütün draw call'ları tekleştirmez.

### P1/P2 — Yeni davranışların da iyileştirme alanı var

Kaynak: `Behaviors/BoomerangScythe.cs:46`, `Behaviors/ElectricBurst.cs:64`, `Planters/PlanterBrain.cs:301`.

- Bumerang her hareket alt adımında bütün grid'i tarıyor. Düşük FPS'te kaçırmamak için alt adım sayısı artıyor; bu durumda CPU işi de artabilir. Hareket segmentinin çevresindeki hücre aralığını tarayan broadphase veya grid traversal kullanılmalı; gidiş/dönüşte birer vuruş kuralı korunmalı.
- Elektrik burst başına en fazla 16 LineRenderer var. Sahne limiti 8 burst: teorik üst sınır 128 çizgi renderer. Her kare noktaları yeniden atanıyor. Ortak mesh veya daha az renderer, jitter örneklerini cache ve SetPositions karşılaştırması uygun. Aynı hasar ve XP sonucu korunmalı.
- Patlama kaynakta ve etkilenen komşularda ayrı VFX çıkarıyor; HashSet ve yön dizileri oluşturuyor. Tekrar kullanılan hedef buffer'ı, sabit yön dizisi ve kozmetik patlama bütçesi kullanılabilir.

Bunların gerçek ms maliyeti ölçülmedi. Önceki eklemeleri de optimizasyon kapsamına dahil etmek gerekiyor; pooling kullanılması tek başına ucuz olduklarını göstermiyor.

### P2 — Hasat UI güncellemeleri bir karede tekrarlanıyor

Kaynak: `UI/inGameUIs/XpUI.cs`, `Managers/ProgressionManager.cs`, `UI/GoldUI.cs`.

XP slider'ı her XP eventinde, ayrıca level-up sırasında güncelleniyor. Çoklu hasat aynı karede birçok setter üretiyor. GoldUI üç kaynak için ayrı ayrı 48 aktif ikon sınırına sahip: toplam 144 uçan UI ikonu mümkün. İkonlar root Canvas'a ekleniyor; hareketleri HUD ile aynı Canvas maliyetine katkı yapabilir. Nesneler havuzda olsa da her uçuşta yeni Flight sınıfı oluşturuluyor.

Öneri: XP/kaynak görselini dirty flag ile kare sonunda bir kez güncellemek; gameplay event ve ödül hesaplarını aynen korumak. Uçan ikonları ayrı dinamik Canvas'a almak, ortak sprite atlası ve yeniden kullanılan Flight kayıtları. Low profil için toplam 24–36 uçan ikon bir başlangıç deneyi olabilir; kaynak miktarı kesinlikle azalmamalı. Sayıları benchmark sonrası kesinleştirmek gerekir.

### P2 — Sık hesaplanan stat, grid ve flash işlemleri

Kaynak: `Managers/StatManager.cs`, `Stats/StatCalculator.cs`, `Grid/GridSystem.cs:59`, `Player/PlayerController.cs`, `Managers/VFXManager.cs:281`.

- StatManager global modifier listesini tekrar tarıyor. PlanterBrain cache kullanıyor; benzeri GlobalVersion üzerinden stat+target için uygulanabilir. Modifier sırası/tier replacement korunmalı.
- Radius araması yeni List oluşturuyor, tüm grid üzerinde Distance hesaplıyor. Caller-owned buffer, hücre sınırları ve squared distance kullanmak uygun; mevcut yarım hücre toleransı korunmalı.
- Cursor çemberi her kare trigonometrik hesaplar ve tek tek vertex atamaları yapıyor. Birim çember cache ve transform ile konum/ölçek yeterli olabilir.
- Her hit flash'ında yeni MaterialPropertyBlock ve coroutine/wait oluşturuluyor. Bitki başına yeniden kullanılan flash durumu tercih edilebilir. MPB kullanımının SRP Batcher uyumsuzluğu da Frame Debugger'da incelenmeli; her renderera benzersiz materyal üretmek çözüm değil. [Unity SRP Batcher uyumluluğu](https://docs.unity3d.com/cn/6000.0/Manual/SRPBatcher-Materials.html).

### P3 — İkincil alanlar ve kullanılmayan risk

- `UI/ResonancePreviewUI.cs:98`: yerleştirme sırasında açıklama imzası her güncellemede string'e çevriliyor. Aynı hücre, footprint ve modifier sürümü değişmedikçe yeniden kurmamak uygun. Round içindeki sürekli yavaşlamanın doğrudan açıklaması değil.
- SkillTreeUI OnDisable'da eventlerden çıkıyor; aktifken kaynak değişimi bütün node/bağlantıları yeniliyor. Toplu yenileme düşünülebilir; gizli ağacın sürekli çalıştığı kanıtlanmadı.
- `Managers/SoundManager.cs:40` PlaySound her çağrıda yeni AudioSource/GameObject oluşturup temizlemiyor. İncelenen Scripts/Scenes/Prefabs içinde çağrı bulunmadı; mevcut yavaşlamanın doğrulanmış nedeni olarak saymıyorum. Tekrar kullanılacaksa önce düzeltilmeli. Aktif hasat sesi VFXManager'da sınırlı AudioSource havuzuyla çalışıyor.
- Texture import/VRAM bütçeleri, bütün meshlerin ayrıntı seviyeleri, shader varyantları, audio importları ve cihaz sürücüsü bu incelemede tam ölçülmedi; bunlara ilişkin kesin darboğaz iddiası yok.

## Korunması gereken iyi taraflar

- Sahne değerleri: tornado 8, bumerang 6, elektrik 8 ile sınırlandırılmış. Tornado'nun kod varsayılanı 3 olsa da sahnede 8; değerlendirmede sahne değeri kullanıldı.
- Tornado OnDisable coroutine ve tween temizliyor; davranış yöneticilerinde round sonu havuza dönüş var.
- Resonance burst havuzu sınırlı; PlanterBrain stat cache ve yeniden kullanılan davranış listeleri mevcut.
- GoldUI'nin aktif ikon sınırı, abonelik temizliği ve ikon/texture kapanış temizliği var.

Mimariyi baştan yazmak veya doğrudan ECS'e taşımak ilk adım değil. Öncelik yaşam döngüsü açık havuzlar, görsel bütçeler, sıcak yol allocation'larının azaltılması ve ölçüme dayalı renderer düzenlemesi.

## Önemli: görsel kalite oyun şansını değiştirmemeli

FloatingText ve kamera shake gibi kozmetikler UnityEngine.Random tüketiyor; saldırı crit/variance ve davranışlar da aynı genel random akışını kullanıyor. Kozmetik azaltmak hangi rastgele sayıların gameplay'e kaldığını değiştirebilir. Görsel bütçeler uygulanırken kozmetik RNG ayrı tutulmalı; A/B performans testi aynı seed, saldırı girdileri ve build ile yapılmalı. Kalite ayarları Gold/Iron/Stone/XP/score veya vurulan hedefleri değiştirmemeli. Davranış nesnesi limitlerini sadece kalite uğruna düşürmek hasar dengesini değiştirir; görsel sunum ile simülasyon ayrılmalı.

## Uygulama ve doğrulama sırası

1. Referans kayıt: aynı build ve grid ile bağımsız Development Player'da CPU Timeline/Hierarchy, GC.Alloc, GPU ve Frame Debugger. Deep Profile ilk ölçümde kapalı; ölçüm aracının ek maliyeti ayrıca dikkate alınmalı.
2. Her-kare/hasat logları ve halka materyal yaşam döngüsü. 1/5/15/30. dakikalarda Material sayısı, native/managed bellek; aynı yükte devam eden artış var mı?
3. Kozmetik havuz bütçeleri ve prewarm. Aktif/pasif/yaratılan text-hit-explosion sayıları, DOTween aktif/oynayan/paused sayıları. Sabit yükte tepe sonrası plato beklenir; her round yeni artış beklenmez.
4. Bitki pooling ve sıcak yol buffer/cache işleri. Tek ödül, tek ölüm eventi, grid sahipliği, round sonu cleanup ve spawn zamanlaması regression testleri.
5. Bumerang broadphase, elektrik çizimi ve UI toplu güncelleme. Bumerangı 15/30/60/120 FPS'te aynı hedef ve hasar sonucu için test etme.
6. Low/Medium/High render profilleri ve GPU karşılaştırması; comic görünümü ekran görüntüsüyle kontrol etme.

İki ayrı uzun test gerekli: (a) ilerleme sabit, aynı iş yükü 30 dakika — gerçek birikmeyi bulmak; (b) R1/20/65/100/130 kötü/ortalama/güçlü build — doğal yük artışını görmek. Menüye dönüş ve yeni run döngüsünü ayrıca tekrarlamak gerekir.

Önerilen kabul hedefi, seçilecek somut düşük PC üzerinde 1080p Low'da 60 FPS (16.67 ms frame bütçesi); 120 FPS hedeflenirse 8.33 ms gerekir. CPU ve GPU süreleri ayrı incelenmeli, ortalama FPS yanında P95/P99 frame time ve 1% low raporlanmalı. Isınma sonrası sürekli materyal/nesne artışı olmamalı; savaşın rutin C# yollarında allocation sıfıra yaklaştırılmalı. Sadece Editor FPS'i veya mevcut doğruluk testlerinin geçmesi performans kabulü sayılmaz. Donanım tanımlanmadan FPS garantisi verilemez.
