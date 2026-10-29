# Açılış videosu: çekim listesi (taslak 5)

28 Ekim 2026. Briefin 2.–3. adımı (`Docs/AcilisVideosu-Brief.md`). **Durum: previs v5 render edildi, kullanıcı onayı bekliyor.**

- **Biçim:** Blender 3D toon, film/anime kurgusu, 24 fps.
- **Süre:** 354 kare, 14,75 sn. Ardından 1,5 sn seçim ekranı.
- **Yeri:** "Oyna" → bu video → çiftçi/tırpan seçimi. Seçim ekranında Eri "Oh! Bir gemi!…" der.

## v4 → v5 (kullanıcı geri bildirimi, 28 Ekim)
Kullanıcı iki şey söyledi:
- "Geçişler hâlâ bir yerden bir yere atlıyor; roketin düşmesi ya da Dünya'dan çıkması başka bir video gibi. Tek kurgu, tek bütün hissi yok."
- "Toonic yapıdan çıkmış, cartoon olmuş; cartoon değil toonic olmalı."

**Neden kopuktu:** Her çekim kendi ayrı dünyasındaydı.
- Dünya Ç1'de yarıçap 10, Ç2a'da 40, Ç2b'de 6'ydı ve deseni de farklıydı.
- Yıldızlar kameraya yapışıktı.
- Işık yönü çekimden çekime değişiyordu.
- Gezegen, bulut ve yer birbirinden kopuk setlerdi.

Geçişler ne kadar süslense de her kesme başka bir yere atlamaktı.

**v5: tek dünya, kesintisiz kamera.**
- Dünya (yarıçap 10), roketin tek kaçış yolu, Eri'nin gezegeni (yarıçap 40) ve bulut tabakası aynı uzayda duruyor.
- Yıldızlar dünyada sabit, güneş yönü baştan sona aynı.
- Roket tek bir sürekli yol izliyor; hız eğrisi kopmuyor.
- Kamera konumları birbirine yumuşakça karışıyor. Yön değil bakılan nokta karışıyor, böylece geçişin her anında konu kadrajda kalıyor.
- Yalnız iki görünmez kesme var: lomboz camı (kokpite girip çıkarken) ve bulutun içi (iniş seti).

**Toon:** oyunun kendi shader'ı (`Assets/Resources/ThornComicToon.shader`) esas alındı.
- Aynı rengin üç parlaklık basamağı: ×1 / ×0,8 / ×0,57 (eşikler .55 / −.12).
- Sabit kalınlıkta mürekkep kabuğu.
- Az poligonlu, düz gölgeli (yüzeyli) biçimler, oyundaki gibi.
- v4'ün Freestyle fırça çizgisi, tramı ve titrek çizgisi ("cartoon" havası) kaldırıldı.
- Kenar ışığı yalnız yanan Dünya'nın roketi aydınlattığı yerde.

| Bölüm | Kare / sn | Kamera tek hareketle |
|---|---|---|
| **A** | 0–153 / 0–6,4 | Yüzeyin üstünde çatlak, hazırlık, patlama (42). Kamera geri itilir, alev topunu izleyerek başını kaldırır, sonra roketin yanına yetişir (56–80): ateş dağılır, o şey roketmiş (58–78). Yanında uçuş, hız çizgileri. Gövdeye geçiş (88–104). Arkada Dünya'nın tamamı plakalara ayrılıp küçülür, şok dalgası yetişir (140). Kamera lomboza döner ve camın önüne gelir. |
| **B** | 154–183 / 6,4–7,6 | Camın ardı: kokpit göstergesi, ibre düşer, iki vuruş, kırmızı nabız, bekleyiş. |
| **C** | 184–281 / 7,7–11,7 | Camdan dışarı çıkış, gövde boyunca kuyruğa kayma (184–198). Motor öksürür ve söner (202). Kamera geri çekilir; gemi boşlukta küçülür, önde aşağıda gezegen. Yakalanma (232): rota kıvrılır, arkada kesik krem iz kalır. Burun ateşle sarılır, kamera gemiyle bulut tabakasına dalar (274–281). |
| **D** | 282–353 / 11,75–14,75 | Bulutun içinden çıkış, bulut katmanlarından aşağı iniş, bulut tabanından çıkınca yer görünür. Kamera 292–306 arasında aşağı süzülüp alçak noktaya iner. Gemi sol üstten önünden geçip çakılır (316), toz yatışır, ışık yanar (338), kararma. |

## v3 → v4 (kullanıcı geri bildirimi, 28 Ekim)
Kullanıcı iki şey söyledi: "Olaydan olaya atlamalar bağlanmamış, saçma geçişler var" ve "Fena CG kokuyor, oyunun toon yapısında kalmalı."

**Toon dili** (`Docs/UI-Art-Direction.md`: kalın mürekkep, sert gölge, seyrek tram):
- Sabit kalınlıktaki mürekkep kabuğu kalktı; yerine Blender'ın kendi Freestyle fırça çizgisi geldi. Kalınlığı fırça açısıyla değişiyor, uçları inceliyor, hafif titriyor.
- Plastik parlak ton kalktı, iki ton kaldı. Gölgede ekran uzayında 45° seyrek tram var.
- Küre, bulut, duman ve gezegenlerin biçiminde el yapımı düzensizlik var; kusursuz daireler CG kokuyordu.

**Geçişler olayları bağlıyor:**

| Kesme | v3 | v4 |
|---|---|---|
| Ç1 → Ç2a | Yüzeyden uzayda bir buluta atlıyordu. | Patlamanın dumanı Ç1'de yükseliyor. Ç2a'daki bulut o duman; Dünya ufkuna oturuyor ve gövdesi yerden yükseliyor. |
| Ç2b → Ç3a | Krem parlama, ardından gösterge. | Şok dalgası çarpıyor (f140). Kamera gövde üstünde lomboza dönüyor ve camın önüne geliyor, cam içeriden yanıyor. Aynı yerde ve boyda yuvarlak gösterge açılıyor (f154). |
| Ç4 → Ç5 | Tam ekran sarı parlama. | Burnu ateş dilleri sarıyor. Gezegenin bulut tabakası alttan yükselip kadrajı dolduruyor, roket ve kamera içine dalıyor. Ç5 bulutun içinde açılıyor. |
| Ç5 → Ç6 | Perdeden sonra yer birden geliyordu. | Ç5'i kapatan bulut perdesi Ç6'nın başında yukarı açılıyor ve yeri gösteriyor. |

**Zamanlama değişikliği:**
- Ç2b 96–153.
- Ç3a 154–183: ibre 156'dan, vuruşlar 168 ve 174'te.
- Ç3b 184–209: öksürükler 191, 194 ve 198'de; motor 202'de susuyor.
- Kalanlar aynı.

## v2 → v3 (kullanıcı geri bildirimi, 28 Ekim)
- **"Dünya'dan uzaklaşma hissedilmiyor."** Yeni Ç2b eklendi: kamera rokete yapışık. Roket tırmanırken Dünya arkada patlayarak küçülür, kıymıklar yandan geriye akar, çizgiler Dünya'ya doğru akar, şok dalgası arkadan yetişir.
- **"Roket yatay görünmek zorunda değil."**
  - Ç2a'da roket toz bulutunun içinden kameraya doğru çapraz fırlar ve yakından geçer.
  - Ç3b'de çaprazdır, Ç2b'de dikey tırmanır.
- **Acelecilik:** her vuruşa bekleme payı verildi:
  - Ç1'de patlamadan önce hazırlık,
  - Ç3a'da ikinci vuruştan sonra bekleyiş,
  - Ç4'te 1 sn'lik sessiz süzülüş,
  - Ç6'da ışık yandıktan sonra bekleyiş.
- **Kurgu bağları:** her kesmede bir eylem öbür tarafa geçer (aşağıdaki tablo).
- **Cila (tasarlanmış, rastgele değil):**
  - 4 karelik el çizimi alev döngüsü,
  - topak duman ve toz,
  - sivri hız çizgileri,
  - parçalı şok halkası,
  - yakından geçişte roketin uzaması (smear),
  - hazırlık hareketleri: plakaların çökmesi, parmağın geri çekilmesi,
  - gerçek eldiven,
  - darbe rüzgârıyla savrulan yapraklar.
- **Kamera sarsıntısı anlatır:**
  - Ç1: titreme büyür, patlamadan hemen önce bir an susar.
  - Ç2b: Dünya uzaklaştıkça uğultu söner, şok dalgası yaklaşırken yeniden kabarır.
  - Ç3b: motorla birlikte titreşim de kesilir.
  - Ç6: yere yaklaşırken artar.
- **"AI slop" hissinden kaçınmak için:**
  - Rastgele kalabalık yok, her efekt bir vuruşa hizmet eder.
  - Her çekimde tek odak ve tutarlı ışık yönü var.
  - Hızlı anların arasında bekleme anları var.
  - Etiket yok, temiz kare.
- **v2'deki teknik hata (düzeltildi):** "Nesneler ikişer karede" ayarı Blender'da uygulanmamıştı. v2'de nesneler kareler arasında yumuşakça kayıyordu. v3'te bütün anahtarlar render'dan önce zorla sabit yapılıyor. Rokete yapışık kameranın olduğu Ç2b'de roket her karede anahtarlanıyor.

## Hareket sürekliliği (v3.1, 28 Ekim — kullanıcı: "Dünya'dan çıkan roket hareket eğrisiyle akıcı bağlanmamış")
Kahraman nesnenin (alev topu, sonra roket) ekran konumu ve hızı her karede ölçüldü (`hiz = px/kare`).
- **Ç1 → Ç2a:**
  - Önce: hız kesmede 36'dan 0,1'e çöküyordu; roket 10 kare bulutun içinde bekleyip sıfırdan hızlanıyordu.
  - Şimdi: kamçı alev topuna kilitleniyor. Ç2a kesmeyle birlikte başlıyor; roket bulutu deliyor, hız 54'ten 29'a taşınıyor ve yön aynı kalıyor. Kamçının dönüşü ilk 6 karede yavaşlayarak oturuyor.
- **Ç3b → Ç4:** konum sıçraması 250 px'ten 10 px'e indi; gemi ekranda aynı yerde.
- **Ç5 → Ç6:** gemi Ç6'ya sıfırdan değil hızıyla giriyor.
- **Roket her karede hareket ediyor**, ikincil nesneler ikişer karede. Önce roket ikişer kare adımlarken kamera her karede hareket ediyordu, bu da karede bir ±10–40 px titreme yapıyordu.
- **Sarsıntı ikiye ayrıldı:** darbe yüksek frekansta ve hızla sönüyor, uğultu yavaş dalgalanıyor.
  - Bunun nedeni: tek frekansta yakındaki gövde karede bir zıplıyordu.
  - Kare kare yön değiştirme sayısı: Ç2a'da 22'den 6'ya, Ç5–Ç6'da 24'ten 0'a indi. Kalanlar bilinçli motor titreşimi ve darbe sarsıntısı.

## v3.2 düzeltmeleri (28 Ekim, kendi listem)
- **Alev:** konik "turuncu şerit" yerine ağızdan sonra şişen bir damla.
  - Kuyruğu dillere ayrılıyor. Orta katmanın dişleri dış katmanı yer yer delip sarı lekeler açıyor.
  - İkişer karede kopan kıvılcımlar var. 4 karelik döngü sürüyor.
- **Ç4 süzülüşü:** gemi kesmede Ç3b'nin son ekran noktasında, 70 mm lensle büyük görünüyor.
  - Sessizlik boyunca lens 30 mm'ye açılıyor; boşluk, gezegen ve uzakta yanan Dünya görünür hale geliyor.
  - Gemi ekranda kaymıyor. Ölçüm: kesmede 5 px, yakalanma başında 8 px fark.
- **Ç5 bulut perdesi:** iki sıra, altı bulut topağı sırayla yükseliyor. Önde olan üstü, arkada olan altı kapatıyor; kabarık kenarları okunuyor.

## Çekimler

| Çekim | Kare / sn | Kamera | Aksiyon ve vurgu | Sonrakine geçiş |
|---|---|---|---|---|
| **Ç1 Son gün** | 0–59 / 0–2,5 | Yüzeyin ~1 birim üstünde, 22 mm. Yavaş ileri kayma, hazırlıkta "nefes alır". Patlama kamerayı geri ve yukarı iter. | 0–13: sakin Dünya. 14: çatlak ve kıvılcım. 28: plakalar kalkar, kıymıklar kameraya uçar. 36–41: plakalar çöker ve titrer, ışık soğur, ses bir an susar. 42: patlama. 44+: alev topu ışıktan çıkar ve yükselir. | 52–59 kamçı: kamera alev topunu kovalar. |
| **Ç2a O şey roketmiş** | 60–95 / 2,5–4,0 | 28 mm, önde dev toz bulutu, altta yanan Dünya ufku. Geçişte kamera roketi izler. | 68: bulut iki yana açılır. Roket buluttan çapraz çıkar, kameraya gelir, yakından geçerken uzar. Hız çizgileri. | Roket kadrajdan sağa çıkar → artık onunla birlikte uçuyoruz. |
| **Ç2b Uzaklaşma** | 96–147 / 4,0–6,17 | Kamera gövdeye yapışık, 24 mm, arkaya bakar. Roket kendi ekseninde döner, motor titreşimi var. | Dünya 16 plakaya ayrılır ve üstel hızla küçülür. Kıymıklar geriye akar, çizgiler Dünya'ya akar, Dünya'nın turuncu ışığı uzaklaştıkça söner. 104'te şok halkası çıkar, 146'da rokete yetişir: krem parlama ve en büyük sarsıntı. | Sarsıntı kokpitte sürer (eylem eşlemesi). |
| **Ç3a Yakıt** | 148–177 / 6,17–7,38 | Göstergeye yakın, 40 mm, eğik kadraj. | Sarsıntıyla ibre titrer, düşer, aşıp geri seker. Eldiven gelir, geri çekilir, iki kez vurur (166, 172); ibre kıpırdamaz. Kırmızı lamba ve iki kırmızı nabız. Bekleyiş. | Sert kesme, motorun ilk öksürüğü. |
| **Ç3b Motor söner** | 178–209 / 7,42–8,71 | Arka üç çeyrek, alttan; roket çapraz yukarı-sağa gider. | Alev döngüsü. Öksürük (186), parlama (190), öksürük (194), sönme (198). Topak dumanlar çıkar, turuncu kenar ışığı ve titreşim kesilir, sahne soğuk laciverte döner, sessizlik. | Sessizlikte kesme. Gemi minicik, ekranda aynı yerde. |
| **Ç4 Yörünge** | 210–281 / 8,75–11,71 | 210–231: lens 70 → 30 mm açılır, gemi ekranda yerinde kalır. Sonra 30 → 26 mm. 232'den sonra aşağı dönüp takip eder. | 210–231: sessiz süzülüş ve hafif yalpa. Uzakta hâlâ yanan Dünya kalıntısı görünür. 232: rota kıvrılır, kesik krem iz kalır. 250: burun ısınır. 262: hız çizgileri. İsteğe bağlı: 234–235'te yeşil parıltı. | 274–281: atmosfer parıltısı kadrajı doldurur. |
| **Ç5 Bulutlar** | 282–305 / 11,75–12,71 | Gemiyle birlikte düşer, 32 mm, artan sarsıntı. | Üç kat bulut farklı hızla akar. Kızgın burun, duman izi. | 298–305: üç bulut perde gibi kadrajı kapatır. |
| **Ç6 İniş** | 306–353 / 12,75–14,75 | Yere yakın, alttan, 24 mm. Darbeden sonra gemiye yavaşça yaklaşır. | Sarsıntı artar, 316'da çakılır. Darbe karesi (krem gök, mürekkep silüetler, ışınsal çizgiler). Toz, savrulan taşlar, kayış, eğik duruş. Yapraklar savrulur. 338'de lomboz ışığı yanar (halka ve "tın"). Bekleyiş. | 346–353 mürekkep siyahına kararma → seçim ekranı. Ufuktaki amber şerit seçim ekranının ufkuyla aynı renk. |

## Açık kararlar (kullanıcının)
1. Ç4'te yeşil ipucu parıltısı olsun mu? İki çeşit render edildi.
2. Ç6'daki ön plan yaprakları ("biri izliyor") kalsın mı?
3. Oyuna geçiş kararmayla mı olsun?
4. 14,75 sn uygun mu, kısaltılsın mı?
5. Previs dili (renk, ışık, kamera) üretime temel olsun mu?
6. Video Unity içinde mi oynasın, video dosyası olarak mı?

## Dosyalar
- **v3 betikleri** (`Tools/Art/IntroAnimatic/`):
  - `build_intro_v3.py`: sahne ve render,
  - `make_intro_v3_sound.py`: geçici ses,
  - `assemble_intro_previs.py v3`: birleştirme.
- **v3 çıktıları:** `Docs/AcilisVideosu/Animatik/v3/`
  - `Acilis-Previs-v3.mp4`, `-ipuclu.mp4`, pano, `IntroV3.blend`.
- **Önceki sürümler:** v2 betikleri (`build_intro_previs.py`, `make_intro_previs_sound.py`) ve çıktıları `v2/` altında; v1 çıktıları `Animatik/` kökünde. Karşılaştırma için duruyorlar.
