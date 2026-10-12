# Diken Kuşatması — üretim ve doğrulama

7 Ekim 2026. Hedef: tam sürüm GameScene. Bu çalışma oynanış veya denge değişikliği içermez.
3×3 bölge, kök başına 6 doğrudan vuruş, 30 saniye aktif süre korunur.

## İlk inceleme: yedi altyapı maddesi

| Madde | Başlangıç bulgusu | Uygulanan dar kapsamlı çözüm |
|---|---|---|
| 1. Gerçek darbe | `Hit()` çağrılmıyor; `Sync` bütün görselleri her bildirimde yeniden kuruyordu. | Hücre/hedef kimliğiyle uzlaştırma; yaşayan nesne korunur. Gerçek TargetHit ve TargetBroken önce mevcut içeriğe iletilir. |
| 2. Çıkış | `Hide()` hemen ardından SetActive(false). | Olaydan ayrılmış çıkış listesi, içerik başına 0–2 sn. Üretim engeli hemen kalkar. Yeni olay, round/run sonu ve disable sert temizlik yapar. |
| 3. Kabuk A/B | Renk yalnız placeholder'a aktarılıyordu. | `ShellA` / `ShellB` prefab varyantı: düğüm, örtü ve belirti aynı varyantı alır. Ortak fallback sürer. Yeni kabuk modeli üretilmedi. |
| 4. Efekt süresi | Bütün flash/beam'ler .22 sn. | `SlotBinding.lifetime`; sıfırsa .22 sn uyumlu fallback. Diken darbesi .27, kırılma .62 sn. |
| 5. Pulse ortaklığı | Ayrı ödül prefab'ı bağlanamıyordu. | FullMoon, HarvestEcho, CritBurst, Reactor, Greed, Overload, CrescentGrow varyantları. Boşsa ortak Pulse kullanılır; diğer ödül içeriği üretilmedi. |
| 6. Zemin | GroundSign +2.2 yüksekte. | Bağlı GroundSign toprak yüzeyinde, köşeye yakın; OverheadWarning bitkilerin üzerinde bağımsız yuva. Eski boş yuvaların havada placeholder davranışı korunur. |
| 7. Plaka ve ses | Üst orta plaka uzak kökü örtüyor; PlayOneShot sınırsız. | Plaka sol kota panelinin altına alındı. Klip başına ses sınırı, tekrar aralığı, toplam 8 ses, pause/resume ve temizlik. |

## Üretim sırası ve içerik

1. Tek kök üretildi; 121 bitkili GameScene ve gerçek kamerada yakalandı.
2. İlk yakalamada açık kalan renk ve ince kontur düzeltildi.
3. Üç farklı kanca biçimini birleştiren diken kümesi, kabarmış toprak/çatlak ve bitki üstü çift tohum uyarısı eklendi.
4. Görsel child'da büyüme, hafif nabız, kısa ezilme; üç kalıcı çatlak aşaması; toprak/odun/yaprak kırıntıları ve çözülme bağlandı.
5. Hazırlık, çıkış, darbe, kırılma ve temizlenme için beş özgün sentez ses üretildi.

Bütün yeni mesh/VFX renderer'ları `ClickerGame/ThornComicToon` shader'ını kullanır. Üç basamaklı mat gölgeleme,
plum mürekkep silüeti ve vertex renkleri; doku, metalik/specular parıltı, fizik veya runtime fracture yok.
VFX de aynı toon malzemesidir. Küçük kırıntılarda ikinci kontur çizimi yok; ana modellerde paylaşılan ink malzemesi var.

Kaynaklar:

- `Assets/Editor/ThornArtBuilder.cs`: özgün mesh/prefab üretimi. Menüden Root veya Parts ayrı üretilebilir.
- `Tools/Art/build-thorn-audio.py`: Python standart kütüphanesiyle deterministik, mono 22.05 kHz / 16-bit WAV.
- `Assets/Art/LateGame/ThornSiege/`: düzenlenebilir prefab, mesh, iki ortak malzeme ve sesler.
- `Assets/Resources/LateGameArt.asset`: yalnız Thorn varyantına bağlanan içerik; diğer olaylara bu sesler verilmez.
- `Assets/Scripts/LateGameArt/LateGameContent.cs`: görsel child animasyonu, çatlaklar ve fizik kullanmayan kırıntılar.

## Performans sınırları

- Hücre başına Update/coroutine yok; animasyonlar mevcut tek sunucudan ilerler.
- Hazır meshler editörde oluşturulur, runtime geometri üretimi yeni içerikte yoktur.
- Gövde/diken/toprak tek birleştirilmiş mesh; aynı prefab örnekleri mesh ve malzeme paylaşır.
- Düz yüz gölgelemesi için köşeler yüz başına ayrıdır. Rapor, ink silüetinin ikinci çizimini ve kapalı çatlak aşamalarını da sayar.
- Dekor sınırı 96; hedefler bu sınıra girmez. İlk kullanımda nesne oluşturulur, sonraki kullanımlarda havuzdan alınır.
  Önceki belgedeki “oyun sırasında hiç Instantiate yok” ifadesi doğru değildi; ön ısıtma garantisi yoktur.
- En fazla 8 ses; diken kliplerinde en fazla 2 eşzamanlı ses. Darbede .065 sn tekrar aralığı.
- Seslerin PCM toplamı yaklaşık 111 KiB. Tepe genliği .65; yayın öncesi insan dinleme/miks onayı gerekir.
- `ThornGeometryBudget.txt` geometri bütçesidir; FPS/GPU ölçümü yerine geçmez.

## Kontrol ve sınırlar

Test çalıştırıcısı: `Tools/run-isolated-verification.ps1`, düşük öncelikli / 4 çekirdekli tek izole Unity.
`ThornArtVerification` mevcut dolu GameScene laboratuvarını kullanır; gerçek profil/süre/dayanıklılık değerlerini değiştirmez.
İzole kopyanın profil seçimi test kurulumu sırasında değişir; ana projedeki seçim ve gerçek kayıt değiştirilmez.
Kamera testinde gerçek zaman kullanılır: önceki laboratuvarın sabit yakalama adımı batch'te fazların atlanmasına neden oluyordu.

Son doğrulama sonucu ve yakalamalar bu klasöre eklenir. İnsan Play hissi, hoparlör/kulaklıkta miks ve farklı ekran oranları
ayrıca değerlendirilmelidir; otomatik görüntü/test bunlar için insan onayı sayılmaz.

## Düzenleme / bağlama

- Kökün `Visual` child'ını düzenle; kök transformuna/collider'a animasyon koyma.
- `LateGameContent.damageStages` çatlak gruplarıdır; havuzdan çıkınca kapanır, dayanıklılık azaldıkça açılır.
- `hideSeconds` yalnız sunum süresidir; 2 saniyeyle sınırlıdır ve oynanışı bekletmez.
- Katalogda aynı slot için `variant=Thorn` atanır. Genel varyant, diğer olayların fallback'idir.
- Kabuk için ShellA ve ShellB'ye eşleşen motifli prefablar atanabilir; HUD renkleri `ZoneColor` ile eşleşmeli.
- Yeni geometri ya da ses için dış varlık kullanılacaksa AGENTS.md lisans bildirimi üretimden önce uygulanır.
- Hilal davranışı, diğer olayların nihai sanatı ve denge bu işin kapsamı dışındadır.

## Tamamlanan doğrulama — 8 Ekim 2026 durum kontrolü

- `ThornArtVerification.txt`: **PASS, 28 kontrol**. Gerçek 3×3 / 6 vuruş / 30 sn verisi;
  bütün yeni olay renderer'larında toon shader, aynı kök örneğinde darbe/çatlak, child-only ezilme,
  kaynak sayaçlarının altında kalan gerçek mesh köşeleri, anında üretim açılması, süre dolması,
  sert temizlik ve yeniden kullanım, ortak fallback/varyant, ses tekrar ve eşzamanlı limitleri.
- `GecOyunV1Verification.txt`: **PASS, 97 kontrol**. Dört olay ve 13 ödülün mevcut işlev/regresyon takımı.
- `AssetValidation.json`: ana proje ile test edilen kopya arasında **63 dosya eşit**, eksik asset GUID'i **0**.
  Beş WAV mono / 22.05 kHz olarak okunabildi. Bu, kulakla dinlenmiş miks onayı değildir.
- Alpha ve Geç Oyun veri üreticileri `--check`: manifestle eşleşiyor, dosya yazılmadı.
- `ProtectedFiles-after.json`: seçili profil, GameScene, DemoScene, olay mantığı/verisi ve gerçek kayıt/yedeği
  başlangıç hash'leriyle aynı. Commit atılmadı.
- Önceki kamera testindeki başarısız denemeler giderildi: batch zaman adımı gerçek zamana çevrildi;
  uzak kökün kaynak sayacına yaklaşan tacı görsel olarak küçültüldü. Son mesh-köşesi testi geçti.
- Unity editörünün mevcut `UnityEditor.Search.SearchDatabase` başlangıç istisnası regresyon logunda yine var;
  oyun kodu veya shader derleme hatası görülmedi. Test takımı bu bilinen editör istisnasını oyun hatası saymıyor.

CPU kontrolü (121 bitki, 9 hücreli saldırı, 400 saldırı): taban **0.5481 ms / 0 B**, 13 ödül + olay
**0.6165 ms / 0 B** saldırı başına. Bu genel regresyonun batch CPU örneğidir; Diken'in GPU maliyeti/FPS'i
olarak sunulmaz. Dekor havuzu 96 sınırında; kare beklemeden yoğun tetiklemede 1064 isteğin atlanması sınırın çalıştığını gösterir.

Kamera yakalamaları: `Thorn_01_Prepare.png`, `Thorn_02_Active.png`, `Thorn_03_Hit.png`,
`Thorn_04_Cracked.png`, `Thorn_05_Break.png`, `Thorn_06_Dissolve.png`.
`01-SingleRoot-first-pass.png` yalnız ilk kök kontrolünün tarihsel görüntüsüdür; son renk/yükseklik değildir.

Açık kabul maddeleri: insan Play hissi, hoparlör/kulaklıkla ses miks değerlendirmesi, farklı ekran oranları,
oyuncu donanımında GPU/FPS ölçümü. Bunlar yapılmış gibi işaretlenmedi.

## Güncel sunum — 8 Ekim, yerden çıkış ve kısa ekran sarsıntısı

Kullanıcı geri bildirimiyle havadaki sarı çift-tohum işaretleri kaldırıldı. `OverheadWarning` ortak yuvası korunur,
Diken kaydı artık boştur; eski ThornWarning prefabı/meshi projeden kaldırıldı.

- Hazırlıkta dokuz hücrenin köşesindeki toprak kabarır ve yerel olarak titrer; yalnız Visual child hareket eder.
- Emerging aşamasında kökler ve diken kümeleri birlikte alttan yükselir (.5 sn görsel yükselme).
  Saksı/bitki transformu, hedef hücresi, dayanıklılık ve aktif olma zamanı değişmez.
- Yeni `GroundBurst` (20) yuvası: .48 sn boyunca dört küçük toon toprak kırıntısı; prefab başına 96 üçgen.
- `CameraFeel` içinde tek karelik ek ofset kullanılır: yalnız çıkış boyunca hafif sarsıntı, sonrasında trauma kuyruğu yok.
  Azami ofset .08 dünya birimiyle sınırlı; mevcut ScreenShake ayarını izler. Pause/temizlikte sıfırdır.
  Kameranın normal idle/diğer efektleri aynı kalır.
- `LateGameContent.emergeDepth`, `groundRumble`, `SetPreparing` yalnız sunumdur. Havuz resetinde yerel konum geri gelir.
- Yeni içerik yine özgün yerel üretimdir; dış asset/paket veya lisans yükümlülüğü eklenmedi.

`ThornArtVerification.txt`: **PASS 40 kontrol**. Yeni kontroller; havada işaret kalmaması, dokuz zemin parçasının kabarması,
child-only hareket, yeraltından çıkış, toprak sıçraması, çıkış sırasında kamera sarsıntısı, ayara uyum,
pause ve çıkış sonunda sarsıntının durması, temizliktir. Batch normalde FeelBootstrap kurmadığı için bu test gerçek
CameraFeel bileşenini test kamerasına açıkça ekler. Screenshot'ın uzun render karesinin .18 sn'lik darbe tepkisini
atlatmaması için test saldırısı ayrı karede başlatılır; oyun animasyonu süresi değiştirilmedi.

`Thorn_01b_Emerge.png` çıkışın başını, `Thorn_02_Active.png` sarı işaretler kaldırıldıktan sonraki görünümü gösterir.

Bu son revizyonla genel regresyon yeniden çalıştırıldı: **PASS 97/97**, Unity exit 0.
65 içerik/kod dosyası test kopyasıyla eşit ve eksik varlık GUID'i yok; CameraFeel dosyası ayrıca eşitlik kontrolünden geçti.
Korunan 13 dosya (gerçek kayıt/yedek, seçili profil, sahneler, olay mantığı/verisi) başlangıç hash'leriyle aynı.
