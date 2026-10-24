# Diken Kuşatması çıkış patlaması — toon VFX çalışması (24 Ekim 2026)

Kaynak: `Tools/Art/ThornBurst/build_thorn_burst.py` (`-b --factory-startup -P`).

Kullanıcı: oyundaki düz "BAM" kartı boş hissettirdi. Bu çalışmada patlama dünyada olur:
- Hazırlık (1–9. kare): tümsek kabarır ve titrer, mürekkep çatlaklar açılır, kesekler zıplar, kenar pofları.
- Patlama (10–13): çizgi roman toprak patlaması silueti (asimetrik, yukarı sivri uçlar + kabarcıklar; mürekkep → koyu toprak →
  turuncu → sarı → krem), dışa açılan mürekkep hız çizgileri; kök oyundaki ThornBurst eğrisiyle fırlar.
- Takip: 9 toprak keseği balistik yayla saçılır, bir kez seker, batar; 7 toz bulutu hızla kabarıp yükselir, yavaşça çözülür.
- Sonrası: yanık leke + kırık toprak halkası kısa süre kalır.
- İlk denemede 5 dilli alev tacı yaprak gibi okundu; asimetrik toprak patlaması siluetiyle değiştirildi.

Dosyalar: `DikenPatlama_*.mp4/.gif` (yakın, 3 kat yavaş, oyun kamerası mesafesi), `DikenPatlama_Kareler.png`, `ThornBurst.blend`,
`frames/`, `game_frames/`. Kök yalnız bağlam için basit bir yer tutucudur (oyundaki ThornRoot değil).

## Oyunda (24 Ekim 2026, kullanıcı onayıyla)

- Sunum: `Assets/Scripts/LateGameArt/LateGamePresenter.ThornBurst.cs`. Düz "BAM" kartı ve eski toprak saçılması (GroundBurst) çıkışta artık yok;
  kökün fırlama eğrisi aynen. Hazırlığın son ,38 sn'sinde kesekler zıplar / kenar pofları; Emerged cue anında patlama.
  Saksılı hücrede saksı toprağına, saksısızda zemine oturur.
- Ağ yükü (Blender sayımı, ünite testinde aynı): blast 360, lines 6, cloud 3 × 120, clod 3 × (20 + 20 kabuk), craterLit 120 + 120 kabuk,
  craterScorch 11 üçgen; toplam köşe ~3 300 (asset belleği ~100 KB).
- Kök başına bir örnek = 28 nesne (kart 2, kesek 9, bulut 7, zıplayan 4, pof 3, krater 2 + kök). Patlama anında en çok ~1 800 üçgen ve
  ~30 çizim çağrısı (iki kök: ~3 600 / ~60), ~1 sn sürer; bekleme sırasında çok daha az.
- Havuz: en çok 4 örnek (dolunca en eskisi kesilir, sayılır); Diken hazırlanırken 2 örnek önceden kurulur. Karede 0 bayt (ölçüldü);
  ikinci olayda aynı örnekler kullanılır (yeni nesne yok); olay temizlenince, round dışında ve kapatılınca havuza döner.
- Doğrulama: `Tools/Verification/Editor/ThornArtVerification.cs` (patlama kontrolleri dahil, PASS).
- Toz kısaltıldı (kullanıcı: kök çıktıktan sonra kalmasın): ,12 sn'de kabarır, ,16 sn tepe, ,28 sn'de yükselerek dağılır;
  son bulut ,62 sn'de biter (kök ,55 sn'de oturur). Blender çalışmasında toz ~1,3 sn kalıyordu.
