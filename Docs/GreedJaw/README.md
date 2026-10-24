# Açgözlü Hasat — “aç ağız halkası” örneği (24 Ekim 2026)

Kaynak: `Tools/Art/GreedJaw/build_greed_jaw.py` (Blender 5.2, `-b --factory-startup -P`).

## Cila 2 (kullanıcı isteği: imleçle hareket + etrafında dönen mini dişler)

- `AcGozluAgiz_Cila2.mp4` / `.gif`: 6 sn yakın plan, etiketli. `AcGozluAgiz_Cila2_OyunMesafesi.mp4` / `.gif`: GameScene kamerası
  (ortografik 11, 1920×1080) gerçek piksel boyutunda kırpılmış, etiketsiz.
- Ağız gövdesi halkaya yapışık (oyunda halka imleci gecikmesiz izler; gecikme saldırı alanını yanlış gösterirdi).
- Hareket hissi dişlerden: hıza göre geriye yatma (az sönümlü yay, durunca sallanır), hızlıyken ağız biraz açılır,
  beklemede 1,3 sn'de bir iki kısa “çat-çat”.
- 5 mini diş halkanın hemen dışında döner (hızla biraz hızlanır, hızlı harekette biraz geride kalır); belirmede içeri sarmallar,
  hap'ta ağza emilir, kayıpta saçılıp düşer.
- Animasyon kare kare yordamsal (yay, hız, durum yaşı); aynı hesap Unity sunucusuna taşınacak.
- Önizlemede yok: imleç ortasındaki dönen tırpan, Hasat Ritmi altın halka/rüzgâr izleri. Üst üste binme Unity'de görülmeli.

Toon dil: düz 2–3 ton, mürekkep kontur (Solidify ters kabuk), parlama/bloom yok, mavi yok, yerde ek halka yok; diş eti mürdüm (#7A2448),
dişler krem (#FFF4D6). Halka yarıçapı ~1,5 birim (hücre 2) varsayıldı; oyunda gerçek saldırı yarıçapına ölçeklenmeli.
`frames/`, `game_frames/`: ham kareler; `GreedJaw.blend`: sahne.

## Unity (24 Ekim 2026)

- Eski amber diş işareti (GreedTeeth.prefab) ve kapanma nabzı artık kullanılmıyor; katalog bağları ve dosyaları duruyor (silinmedi).
- Boru hattı: `export_greed_jaw.py` → `export/greed_jaw.json` → izole kopyada `GreedJawArtBuilder.BuildBatch` →
  `Assets/Art/LateGame/GreedJaw/` + `Assets/Resources/GreedJawArt.asset` (ana projeye kopyalanır). Sunum: `LateGamePresenter.Greed.cs`.
- Ortak ağlar: 2 diş eti (12 parça), 6 diş tipi (köpek/normal/küçük × üst/alt), 1 mini diş, 3 kırıntı, 1 diken, 1 toz kartı.
  Işıklı parçalar iki alt ağlı: gövde + Blender'dan gelen mürekkep kabuğu (ThornInk merkezden iter; uzun kavisli diş etinde yanlış olurdu).
- Halkaya oturma: kök = imleç merkezi + .05 yukarı − kamera bakışı × 4. Kamera ortografik olduğu için ekrandaki yer değişmez,
  saksı/bitki arkasında kalmaz. Ölçek = halka yarıçapı / 1,5 (her karede).
- Ödül alınınca ilk aktif karede bir örnek önceden kurulur (hak dolduğu an kurulum takılması olmasın).
- Hap ve kayıp vuruş merkezinde oynar (imleç gitse de); en çok 3 örnek, dolunca en eski çıkış kesilir.
- Hazır ağız: 21 parça × 2 alt ağ ≈ 42 çizim, 390 gövde + 390 kabuk üçgen. Karede bellek: 0 bayt (ölçüldü).
- Saldırı ritmi (kullanıcı isteği): belirme / hap / kayıp bir saldırı aralığının %90'ına sığacak kadar hızlanır (yavaşta tasarım hızı,
  en çok 4 kat). Boş savuruşta “çat-çat”, sıradaki saldırı yaklaşırken ağız biraz açılır; mini dişler 4 saldırıda bir tur.
  Ritim `PlayerController.AttackIntervalNow / AttackProgress / AttacksPerformed` (salt okunur) ile okunur; sakin salınımlar ölçeksiz zamanla.
- Giriş (kullanıcı: “birden belirip imlece yapışıyor”): ağız merkezden büyümez. Mini dişler dışarıdan girdapla gelir;
  dudaklar ağız köşelerinde tohum gibi başlayıp halka boyunca ortaya uzar ve yükselir (yalnız girişte köşe yazımı: örnek başına
  kopya diş eti ağı, paylaşılan tampon, ortadaki üçgen tek yana katlanır); dişler aynı yönde eğikten dikilir; esneme + çat.
  Girişte küçük toon duman pofları: dört köşede “puf”, dudaklar ortada buluşunca iki küçük puf (kayıp tozuyla aynı kart).
  Giriş en çok 2,5 kat hızlanır; hap/kayıp giriş bitmeden gelirse o anki hâlden devam eder. Video: `unity/AcGozluAgiz_Unity_Dongu.mp4`.
- Doğrulama: `Tools/Verification/Editor/GreedArtVerification.cs` (52 kontrol; hazır + giriş ve tam hazır → hap → kayıp döngüsü ısınmadan sonra 0 bayt). Çekimler `unity/`; çekim betiği yalnız izole kopyada
  (kaynağı `unity/GreedJawCapture.cs.txt`).
