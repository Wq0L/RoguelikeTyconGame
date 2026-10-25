# İstilacı Filiz kişiliği — toon VFX çalışması (24 Ekim 2026)

Kaynak: `Tools/Art/SproutBurst/build_sprout_burst.py` (Diken çalışmasının toon
yardımcılarını kullanır). Kullanıcı: Filiz, Diken'in toprak patlamasıyla aynı göründü — her kaprisin kendi kişiliği olmalı.

Filiz = sinsi, yayılan, canlı (turkuaz + pembe, kıvrımlı / yuvarlak; Diken'in sivri / ateşli dilinin tersi):
- Hazırlık: tümsek kalp atışı gibi kabarır; çevresinde sarmaşık biçimli turkuaz çatlaklar dışa uzar.
- Çıkış: dört dal kamçı gibi fırlayıp ucundan kıvrılır; gonca dönerek yükselir; "pof" — yapraklar açılır, pembe-krem spor bulutu
  ve süzülen spor taneleri; dallar kıvrılıp toprağa geri çekilir (toprak patlaması / kesek / ateş yok).
- Yayılma: yerde sürünen dal komşu hücreye uzanır (yol boyu yaprakçıklar), ucunda küçük filiz pof diye açılır.
- Vuruş: turkuaz-yeşil özsu sıçraması (yuvarlak splat + damlalar), kopan yaprak parçaları; filiz darbeden uzağa eğilir.
- Kırılma: solar ve devrilir, yapraklar düşer, spor bulutu; dal ve küçük filiz de solar.

Ana filiz, dal ve küçük filiz bu önizlemede yer tutucudur (oyundaki SproutMain / SproutPiece değil).
Dosyalar: `Filiz_Kisilik_*.mp4/.gif` (gerçek hız, 3 kat yavaş, oyun kamerası mesafesi), `Filiz_Kisilik_Kareler.png`, `SproutBurst.blend`.

## Düzeltme 1 (25 Ekim, kullanıcı: "dumanın çıkışı ve sonrası kötü duruyor, zamanlaması da değişebilir")

- Önce: bulut birden büyük düz leke olarak beliriyordu; spor taneleri kontürlü boncuk gibiydi; gonca hiç açılmıyordu.
- Şimdi: yapraklar goncayı tomurcuk gibi sarar; pof öncesi tomurcuk şişip titrer (21–24); 25'te yapraklar yıldız gibi açılıp
  pembe goncayı ortaya çıkarır; duman bir kare sonra (26) açıklıktan küçük doğup yukarı-dışa yelpaze gibi açılarak büyür, farklı
  boylarda, ~0,3 sn'de söner; polen ince konturlu küçük elmas kıvılcımlar. Kırılma dumanı filiz yıkılırken (67), küçük filizin
  pofu ortasından çıkar. Yakın plan: `Filiz_Pof_Yakin*.png/.gif`.

## Oyunda (25 Ekim 2026, kullanıcı onayıyla)

- Sunum: `Assets/Scripts/LateGameArt/LateGamePresenter.SproutBurst.cs` (+ `LateGamePetals` snap / openDelay, `Item.spiral`).
  Filiz artık Diken'in toprak patlamasını kullanmıyor. Bloom: hazırlığın son ,45 sn'sinde sarmaşık çatlaklar (saksı kenarı örtmesin
  diye ,05 yukarıda), çıkışta 4 kamçı dal, ana filiz dönerek yükselir, ,54 sn'de tomurcuk açılır, ardından spor bulutu + polen.
  Ekran sarsıntısı yok (Filiz sinsi). Yayılma: kaynak hücreden yeni hücreye sürünen dal + küçük pof; geçiş dalı söner.
  Vuruş: özsu lekesi + damlalar + yaprak parçaları; kırılma: büyük özsu + spor bulutu. Diken yıldızı Filiz'de yok.
- Hücre işareti pembe (kullanıcı: turkuaz halka yeşil bitkiler arasında kayboluyordu): `FieldFootprintBuilder` Filiz rengi
  mürdüm gövde #8E2457 + pembe yaprak #F25C9A, biraz kalın; yalnız `SproutRunners.asset` değişti (Diken halkası aynı).
- Ağ yükü: 61 ağ, toplam 2 808 üçgen (gövde + kabuk); dal parçası 20 + 20, sürünen dal parçası 20 + 20 (yapraklıda 48 + 48),
  spor bulutu 120, polen 8, özsu 144, damla 20 + 20, yaprak parçası 28 + 28, çatlak kolu 32.
- Havuz: bloom 2, yayılma 4, özsu 4, solma 2 (dolunca en eski kesilir, sayılır); Filiz hazırlanırken bloom + yayılma önceden
  kurulur. Karede 0 bayt (ölçüldü); olay temizlenince, round dışında ve kapatılınca havuza döner.
- Doğrulama: `Tools/Verification/Editor/SproutArtVerification.cs` 46/46; Diken testi değişmeden geçti. Oyun içi çekim
  `Docs/FieldEvents/sprout/Filiz_Oyun_*`.

## Ölüm dalgası (25 Ekim 2026, kullanıcı: "kestikten sonraki yok oluş kopuk")

- Son vuruşta ana filiz kurur (toon gövde renkleri `_ToonFlash` ile kuru kahverengi-gri #6B5A4A'ya karışır, mürekkep değişmez),
  darbeden uzağa 72° devrilir ve çöker (1 sn; kuruma ,28 sn). Pembe halkalar ana filize Manhattan uzaklığına göre sırayla kurur:
  ,32 + ,16 × uzaklık sn; dalga ulaşınca ,2 sn kurur ve bir kez kıvranır, sonra ufalanır (4 kuru yaprak kırıntısı + 2 bej toz,
  ,65 sn) ve çöker (halka çıkışı ×1,3 = ,72 sn).
- 25 Ekim ayarı (kullanıcı: "bir tık hızlı, hemen bitiyor; çok azcık yavaşlasın"): tüm ölüm süreleri ~%30 uzatıldı
  (eskisi ,25 / ,12 / ,14 sn, düşüş ,75, halka çıkışı ,55, ufalanma ,5). Kırılmadan son halkanın kaybolmasına ~1,5 sn.
- Yalnız görsel: yavaşlatma son vuruşta anında kalkar (oynanış değişmedi). Ufalanma havuzu 8; kuruma rengi paylaşılan tek
  property block; ölüm dalgası sırasında presenter güncellemesi 0 bayt (ölçüldü). Video `Docs/FieldEvents/sprout/Filiz_Olum_*`.
- Önerideki "kuruma izi" (yayılma yolunu geri izleyen koyu çizgi) eklenmedi; dalga sıralamayla okunuyor.
