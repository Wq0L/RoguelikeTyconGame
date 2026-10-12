# Bölüm 3.8 — Geç oyun tarla olayları ve yeni boss ödülleri

> 7 Ekim ek güncelleme: Diken Kuşatması için özgün toon içerik ve sunum düzeltmeleri eklendi.
> Güncel durum/test: [ThornSiege/README.md](ThornSiege/README.md). Aşağıdaki ilk teslimin placeholder ve
> plaka sınırlamaları tarihsel durumdur; mekanik ve denge değerleri aynı kaldı.


Tarih: 7 Ekim 2026. Hedef: tam sürüm (GameScene). Yeni aday profil: **`Run50_GecOyunV1`** (menü: *Tools > Run Profili > Run50 Geç Oyun V1*).
Commit atılmadı. Gerçek kayıt, profil seçim dosyası, DemoScene ve açık editör bu paketle değiştirilmedi.

Bu belge: (1) mevcut içerik envanteri ve eşleştirme tablosu, (2) yeni ödüllerin tetik ve stat sözleşmesi, (3) dört tarla olayının
kuralları, (4) karar bekleyen noktalar, (5) test sonuçları ve sınırları, (6) Play kontrol adımları. İçerik bağlama (model / VFX /
ses) ayrı belgededir: [Bolum3-8-IcerikBaglama.md](Bolum3-8-IcerikBaglama.md).

**Durum özeti.** Dört tarla olayı ve 13 yeni boss ödülü normal oyun yoluyla çalışıyor ve otomatik testle doğrulandı. Bütün
sayılar **ilk prototip değeridir** (ölçülmedi, dengelenmedi). Bütün görseller **yer tutucudur**. İnsan Play testi yapılmadı.
Rezonans Çöküşü ve Rezonans Darbesi kapsamdan çıkarıldı; Hilal–rezonans bağlantısının kuralı karar bekliyor (uygulanmadı).

## 1. Başlangıç durumu

- HEAD `ec6bd3d`; çalışma ağacında bu paketten önce 100 değişmiş / izlenmeyen kayıt vardı (6 Ekim Play düzeltmeleri, fontlar,
  maskot vb.). Hiçbiri geri alınmadı.
- Güncel tam sürüm profili: `Run50_AlphaDengeV1` (denge seti `RunBalance_AlphaDengeV1`, havuz `BossRewardPool_AlphaDengeV1`).
  Yeni profil ondan türetildi; Alpha profili ve asset'leri değişmedi (üreticisinin `--check` denetimi temiz).
- DemoScene, `Run50_AlphaDengeV1`'i kaynak alır. Yeni içerik Alpha'da olmadığı için demo etkilenmez; tarla olayı yürütücüsü
  ayrıca demo sahnesinde çalışmayı reddeder.

## 2. Mevcut ödüller — eşleştirme tablosu

Kaynak: `BossRewardPool_AlphaDengeV1` (aşamalı havuz: erken R1–11, orta R12–21, güçlü R22–50; ilk sunulduğu boss'lar R3 / R13 / R23).
"Gerçek davranış" asset verisinden ve koddan okundu; belgelerdeki eski ifadeler esas alınmadı. Listede adı geçen 13 ödülün
**13'ü de** kodda ve havuzda var.

| İstenen ad | Kimlik · asset | Gerçek davranış (alış başına) | Aşama · stack | Karar |
|---|---|---|---|---|
| Keskin Bıçak | `keskin_bicak` · `DengeV1/KeskinBicak_V1` | Doğrudan vuruş hasarı ×1,25 (davranışa geçmez) | erken · 3 | korundu |
| Hızlı Bilek | `hizli_bilek` · `DengeV1/HizliBilek_V1` | Saldırı aralığı ×0,85; taban 0,10 sn'de sunulmaz | erken · 3 | korundu |
| Bereketli Toprak | `bereketli_toprak` · `DengeV1/BereketliToprak_V1` | Bütün saksılarda üretim aralığı ×0,85 (taban 0,50 sn) | erken · 3 | korundu |
| Nadir Tohum | `nadir_tohum` · `DengeV1/NadirTohum_V1` | Nadirlik bonusu +10 puan (en çok 95) | erken · 3 | korundu |
| Bilgi Filizi | `bilgi_filizi` · `DengeV1/BilgiFilizi_V1` | Hasat XP'si ×1,25 | erken · 2 | korundu |
| Kıvılcım | `kivilcim` · `DengeV1/Kivilcim_V1` | Mevcut dört davranış şansı ×1,35 (şansı olmayana vermez; en çok %100) | orta · 3 | korundu |
| Ağır Darbe | `agir_darbe` · `DengeV1/AgirDarbe_V1` | Kritik hasar çarpanı +0,75 | orta · 2 | korundu |
| Kritik Göz | `kritik_goz` · `DengeV1/KritikGoz_V1` | Kritik şansı +12 puan | orta · 3 | korundu |
| Altın Hedef | `altin_hedef` · `DengeV1/AltinHedef_V1` | Rare ve üstü bitkiye doğrudan hasar ×1,5 | orta · 2 | korundu |
| Yıkım Gücü | `yikim_gucu` · `DengeV1/YikimGucu_V1` | Davranış hasarı ×1,3 (doğrudan vuruşa geçmez) | orta · 3 | korundu |
| Geniş Savuruş | `genis_savurus` · `DengeV1/GenisSavurus_V1` | Vuruş yarıçapı ×1,32 | güçlü · 2 | korundu |
| Canavar Kesimi | `canavar_kesimi` · `DengeV1/CanavarKesimi_V1` | Doğrudan vuruş hasarı ×1,6 | güçlü · 1 | korundu |
| Fırtına Bileği | `firtina_bilegi` · `DengeV1/FirtinaBilegi_V1` | Saldırı aralığı ×0,70 | güçlü · 1 | korundu |

Kısa listede olmayan ama havuzda bulunan ve **aynen korunan** 6 ödül:

| Ad | Kimlik · asset | Gerçek davranış | Aşama · stack |
|---|---|---|---|
| Artçı Patlama | `artci_patlama` · `KirilmaErisimiV1/ArtciPatlama_E1` | Şansı tutan normal patlamadan 0,20 sn sonra aynı merkezde ikinci patlama: ilk patlamanın hesaplanmış hasarının %100'ü, 2,00 hücre | güçlü · 1 |
| Çifte Akım | `cifte_akim` · `KirilmaErisimiV1/CifteAkim_E1` | Başarılı normal elektrik dalgasından 0,15 sn sonra ikinci dalga: %100 hasar, çaprazda 4 hücre | güçlü · 1 |
| Hasat Ritmi | `hasat_ritmi` · `AlphaDengeV1/HasatRitmi_A1` | Her 5 **doğrudan hasatta** sıradaki saldırı: hasar ×1,75, yarıçap ×1,6 | güçlü · 1 |
| Bereketli Öğrenim | `bereketli_ogrenim` · `BedelliOdullerV1/BereketliOgrenim_B1` | Level başına seçim +1; bedel: doğrudan vuruş ×0,80 | güçlü · 1 |
| Davranışa Adanış | `davranisa_adanis` · `BedelliOdullerV1/DavranisaAdanis_B1` | Davranış hasarı ×1,50; bedel: level başına seçim −1 | güçlü · 1 |
| Zincir Hasat | `zincir_hasat` · `AlphaDengeV1/ZincirHasat_A1` | Davranış hasatları kendi saksılarının davranışını sınırlı tetikler: 2 ek nesil, şans ve hasar ×0,75 / ×0,50, kök bütçesi 32, kare başına 8 iş | güçlü · 1 |

Yeni havuzda bu 19 ödülün **asset'i, aşaması, sırası, notu, ağırlığı ve stack sınırı aynıdır** (aynı asset'lere referans; kopya yok).
Hiçbiri silinmedi, yeniden adlandırılmadı ya da çoğaltılmadı.

### Bilinen farklar (uygulanmadı, yalnız kayıt)

- **Bereketli Toprak — sunulma ile çalışma ayrı.** *Sunulma* koşulu: son round'un ortalama tarla doluluğu %65'in altındaysa
  teklif edilir. *Çalışma*: alındıktan sonra koşulsuzdur; doluluğa bakmadan üretim aralığını kısaltır.
- **Aşamalı havuz `minRound` / `maxRound` okumaz.** Asset'lerde duran değerler (Bilgi Filizi `maxRound 30`, Canavar Kesimi ve
  Fırtına Bileği `minRound 25`, Hasat Ritmi `minRound 10`) bu havuzda etkisizdir: sunulma zamanı yalnız aşamadan gelir. Yani bugün
  Bilgi Filizi R30'dan sonra da sunulur; Canavar Kesimi ve Fırtına Bileği R23 boss'undan itibaren sunulur.
- "Bilgi Filizi R30 sonrası kalksın", "Canavar Kesimi R25 sınırı" ve mevcut kartların aşama (tier) değişikliği bu pakette
  **uygulanmadı**.
- **"Şarjlı Savuruş" ve "Zincir Reaksiyon".** Bu adlarla kodda, asset'lerde ya da belgelerde bir ödül yoktur. İşlevce en yakın
  mevcut ödüller Hasat Ritmi ve Zincir Hasat'tır; bu pakette onların yerine geçen ya da ikinci kopyası olan bir ödül eklenmedi.
  Elimdeki tek tarif farkı görev metnindeki ipuçlarıdır; ayrı tasarım farkı olarak kaydediyorum:

  | | Mevcut (korunan) | İstenen adın ima ettiği fark (uygulanmadı) |
  |---|---|---|
  | Hasat Ritmi ↔ Şarjlı Savuruş | Sayaç **doğrudan hasat** sayar (vuruş değil); davranış ve güçlenmiş saldırı hasatları sayılmaz; hak canlı bitkiye değen ilk saldırıda harcanır; bedeli yok | Vuruş sayacıyla dolan şarj / farklı bedel |
  | Zincir Hasat ↔ Zincir Reaksiyon | Saldırı kilidi **yok**; sınır: nesil, şans / hasar çarpanı, aynı kökte saksı + davranış tekrarı, kök ve kare bütçesi; Artçı ve ikinci dalga zincir dışı | Saldırı kilidi gibi bir bedel / farklı vuruş tarifi |

  Bu iki adın kesin tarifi sizden gelmeden ne mevcut ödüller değiştirilecek ne de yenileri eklenecek.

## 3. Korunan davranışlar

- Hedef GameScene / tam sürüm. DemoScene dosyası, demo ayarları ve demo davranışı değişmedi.
- Mevcut 16 profil ve bütün eski ödül asset'leri değişmedi. Yeni içerik yalnız `Run50_GecOyunV1` profilinde etkin.
- Kota, bitki canı, XP maliyeti, round süreleri, mevcut ödül değerleri ve küçültülmüş temel güç kartları: aynı denge asset'i
  (`RunBalance_AlphaDengeV1`) ve aynı hedef tabloları — yeniden dengelenmedi.
- Kasırga ve bumerang ayrı ayrı 12 aktif kapasiteyle başlar; kapasite artırma yolu (`movingBehaviorLimitBonus`) çalışır (testli).
- Hasat Ritmi doğrudan hasat sayar; Zincir Hasat'ın nesil / şans / hasar / ziyaret / bütçe kuralları ve Artçı / ikinci dalganın
  zincir dışı kalması aynen; Hasat Ritmi ↔ Zincir Hasat karşılıklı dışlaması yeni havuzda da geçerli (testli).
- Oyuncunun grid modifier yerleşimi otomatikleştirilmedi. Endless, yeni final boss, meta ilerleme, çiftçi / tırpan açılışı eklenmedi.
- Doğrudan saldırı yolu, yeni içerik yokken **sayısal olarak aynıdır**: dinleyici yokken çarpan 1'dir ve hiçbir şey kaydedilmez.

## 4. Kapsam değişikliği (rezonans)

Görev sırasında gelen düzeltmeyle:

- **Rezonans Çöküşü: kapsamdan çıkarıldı.** Uygulanmadı.
- **Rezonans Darbesi: kapsamdan çıkarıldı.** Uygulanmadı; aynı mekanik "Tetik Darbesi" adıyla da eklenmedi.
- Rezonansı patlatan, AoE / pulse saldırısına çeviren ya da patlama sonrası devre dışı bırakan hiçbir şey yok. Mevcut rezonans
  hesaplaması ve etkileri değişmedi (`ResonanceManager`, `PlanterBrain` rezonans kısmı dokunulmadı).
- **Hilal–rezonans bağlantısı tasarımda duruyor**; somut kuralı karar bekliyor (bkz. 9.1). Etkisi uygulanmadı.

Böylece yeni ödül sayısı 13'tür: erken 2, orta 8, güçlü 3.

## 5. Yeni boss ödülleri — tetik ve stat sözleşmesi

Hepsi: bir kez alınır, teklif ağırlığı 1, normal boss teklifinden gelir (yeni şans / garanti / reroll yok). Sayılar ilk prototip
değeridir; tek kaynak `Tools/Balance/GecOyunV1/gec_oyun_v1_params.py`.

**Ortak kurallar (hepsi için geçerli):**

- *Hasar yolu*: mevcut `PlantHealth.TakeDamage`. Etki vuruşlarının türü `DamageType.Reward`, hangi etkiden geldiği vuruşun
  bağlamındadır (`HarvestLink.Source`). İkinci bir hasar sistemi yoktur; ölüm, kaynak, XP ve skor eski yoldan ve bir kez verilir.
- *Doğrudan değil*: etki vuruşu kritik atmaz; doğrudan vuruş çarpanlarını (Keskin Bıçak, Canavar Kesimi, Altın Hedef, tırpan,
  uzmanlaşma, Hasat Ritmi, Açgözlü Hasat) ve saksının Odak bonusunu almaz; davranış tetiklemez; doğrudan hasat sayaçlarını
  (Hasat Ritmi, Açgözlü Hasat, Hilal sayacı, Gezgin Bereket ilerlemesi, müdahale hedefleri) doldurmaz.
- *Davranış da değil*: davranış çarpanı (Yıkım Gücü, Davranışa Adanış, uzmanlaşma) ve davranış rezonansı (Güçlendirilmiş Hasat)
  uygulanmaz. Taban, davranışlarla aynı stat'tır: oyuncunun **hasat hasarı** stat'ı.
- *Zincir*: hiçbir yeni proc Zincir Hasat'a katılmaz. Etki vuruşları kök taşımaz (zincir onları görmez, sayaçlarına girmez);
  Aşırı Yükleme'nin ek proc'u artçı gibi "zincir dışı" işaretlidir. Zincirin kendi kuralları değişmedi.
- *Zamanlama*: gecikmeler ve bekleme süreleri oyun zamanıyla sayılır, sonlu bir kuyruktadır (128 iş), round sonunda düşer.
  Hiçbir kural bir görselin bitmesini beklemez.
- *Güvenli pasiflik*: ön koşul sonradan kaybolursa (davranışlı saksı satıldı, hedef yok) etki hiçbir şey yapmaz; hata vermez.

| Ödül (aşama) | Tetik ve kaynak | Hedef seçimi | Ölçeklendiği stat · katsayı · süre / alan | Bedel · bekleme | Aynı saldırıda sınır | Ön koşul · sunulma koşulu | Sunum olayı |
|---|---|---|---|---|---|---|---|
| **Elektrik Anteni** (erken) | Zamanlayıcı: aktif round'da 2,5 sn'de bir | Antenin baktığı yöndeki ilk 3 açık hücre | Hasat hasarı × 0,60 | — | — (saldırıdan bağımsız) | Yok · konum seçimi şart | `AntennaPlaced`, `AntennaPulse` |
| **Hilal Darbesi** (erken) | Her 6. **geçerli** doğrudan saldırı (canlı bitkiye değen) | Saldırının merkez hücresinden tarlanın ortasına doğru 4 hücre, adım başı 1 hücre (0,07 sn) | Hasat hasarı × 0,80 | — | Saldırı başına en çok 1 dalga; aynı anda en çok 4 dalga | Yok | `CrescentLaunch`, `CrescentStep` |
| **İletken Hat** (orta) | Zamanlayıcı: 4 sn'de bir | İki antenin kenar hücreleri arasındaki doğru (iki uç dahil) | Hasat hasarı × 0,45 | — | — | Elektrik Anteni · hat ≥ 3 hücre olacak ikinci konum | `LinePulse` |
| **Büyüyen Ay** (orta) | Hilal adımının gerçek hasadı | Aynı dalganın **kalan** adımları | Düzey başına hasar +%25 (en çok 3 düzey); cephe 3 hücre | Hasatsız adım büyümeyi sıfırlar | Adım başına +1 düzey | Hilal Darbesi | `CrescentGrow`, `CrescentReset` |
| **Kritik Ark** (orta) | Doğrudan vuruşun kritiği (zar eskisi gibi bitki başına) | Kritik yiyen hücreye en yakın (≤ 2 hücre), saldırının değmediği, bu saldırıda ark almamış canlı bitki | O kritik vuruşun **hesaplanmış** hasarı × 0,30 | — | En çok 3 ark; hedef başına 1 | Yok · kritik şansı > 0 | `CritArc` |
| **Hasat Yankısı** (orta) | Güçlü doğrudan hasat: tek saldırıda ≥ 3 doğrudan hasat | Hasat edilen hücrelerin simetriği (ilk 3) | Hasat hasarı × 0,50; gecikme 0,12 sn | — | En çok 3 yankı; hücre başına 1 | Yok | `HarvestEcho` |
| **Taşan Hasar** (orta) | Doğrudan vuruşla ölüm + gerçek artan hasar > 0 | Ölen bitkiye en yakın canlı bitki (≤ 1 hücre) | Artan hasar × 0,50 (aktarım başına %50 kayıp) | — | Kaynak başına en çok 2 aktarım; saldırıda en çok 4 kaynak | Yok | `Overflow` |
| **Nadir Reaktör** (orta) | Rare ve üstü bitkinin **doğrudan** hasadı | Bütün saksılar (mevcut davranış şansları) | Dört davranış şansı ×1,25, 4 sn | Birikmez; süre yenilenir | — | Yok · davranışı olan saksı | `ReactorOn`, `ReactorOff` |
| **Kritik Patlama** (orta) | Tek saldırıda ≥ 3 farklı bitkiye kritik | Saldırının merkezi, saldırının kendi temas alanı | Hasat hasarı × kritik çarpanı × 0,50 | — | Saldırı başına 1 | Yok · kritik şansı > 0 | `CritBurst` |
| **Açgözlü Hasat** (orta) | Tek (güçlenmemiş) saldırıda ≥ 3 doğrudan hasat | Sıradaki doğrudan saldırı | O saldırının doğrudan hasarı ×1,5 | Tek hedefe vurulursa hak kaybolur; güçlenen saldırı yeni hak doğurmaz | 1 hak | Yok | `GreedReady`, `GreedSpent`, `GreedLost` |
| **Kapalı Devre** (güçlü) | Normal hat vuruşunun, davranışlı saksının bitkisini hasat etmesi + o saksının en yüksek davranış şansıyla zar | Aynı hat | Normal hat hasarı × 0,60; gecikme 0,35 sn | Hat beklemesi 3 sn | Vuruş başına 1 ek vuruş; ek vuruş kendini başlatamaz | İletken Hat · davranışı olan saksı | `LineExtraPulse` |
| **Dolunay** (güçlü) | Bir dalgada büyüme 2. düzeye ilk ulaştığında | İlk dalganın son hücresinden başlangıç hücresine geri | Hilal taban hasarı × 0,70; cephe o anki genişlik; gecikme 0,15 sn | **Doğrudan saldırı duraklaması 0,35 sn** (sert sınır 0,6 sn) | Dalga başına 1; ikinci dalga büyümez, Dolunay başlatmaz | Büyüyen Ay | `FullMoon` |
| **Aşırı Yükleme** (güçlü) | Saksının davranışının **normal** tetiği (doğrudan hasattan, şansı tutmuş) | Aynı saksı + aynı davranış, mevcut davranış yolu | Saksının normal davranış hasarı × 0,50; gecikme 0,30 sn | Saksı + davranış başına 2,5 sn bekleme | Saldırı başına en çok 3 ek proc | Yok · davranışı olan saksı | `Overload` |

### 5.1 Somut kural tanımları

- **Geçerli doğrudan saldırı** (Hilal sayacı): en az bir canlı bitkiye değen oyuncu saldırısı. Boş savuruş sayılmaz. Sayaç
  round'lar arasında korunur.
- **Hilalin başlangıcı ve yönü**: saldırı merkezinin düştüğü hücre (tarla dışındaysa en yakın açık hücre). Yön, o hücreden açık
  alanın ortasına doğru farkın büyük olduğu eksendir; eşitlikte x ekseni, tam ortada +x. Dalga başlangıcın bir ilerisinden başlar
  ve tarlanın kenarında biter.
- **Başarısız hilal adımı** (Büyüyen Ay): hiçbir bitki hasat etmeyen adım (hedef bulunmaması dahil). Büyüme düzeyi 0'a döner.
  Başarılı adım = o adımın vuruşuyla en az bir bitkinin ölmesi. Büyüme dalgalar arasında taşınmaz.
- **Dolunay'ın ikinci dalgası**: ilk dalganın varacağı son hücreden başlar, başlangıç hücresine kadar geri gelir. Hasarı taban
  hasardan hesaplanır (büyüme hasarı taşınmaz). İlk dalga kendi kalan adımlarını ayrıca sürdürür.
- **Güçlü doğrudan hasat** (Hasat Yankısı, Açgözlü Hasat): tek bir oyuncu saldırısında, o saldırının kendi vuruşlarıyla ölen
  en az 3 bitki. Davranış, artçı ve etki hasatları sayılmaz.
- **Simetri merkezi** (Hasat Yankısı): açık alanın ortası. Hücre nokta yansımasıyla eşlenir:
  (x, z) → (minX + maxX − x, minZ + maxZ − z). Yansıma saldırının kendi alanındaysa atlanır; "uygun hücre" vuruş anında
  değerlendirilir (açık ve canlı bitkili).
- **Gerçek artan hasar** (Taşan Hasar): bitkiye uygulanan hasar − vuruştan önceki canı (`PlantHealth.Overkill`). Aktarılan hasar
  hedefi de öldürürse onun artan hasarı aynı oranla bir kez daha aktarılır; oran 1'in altına indirince durur.
- **"En yakın" hedef** (Ark, Taşma): hücre uzaklığı en küçük olan; eşitlikte sabit tarama sırası (önce x, sonra z, küçükten
  büyüğe). Rastgele değildir.
- **Açgözlü Hasat'ın hakkı**: sıradaki saldırı ≥ 2 canlı bitkiye değiyorsa güçlenir ve hak harcanır; tam 1 canlı bitkiye
  değiyorsa güçlenmez ve hak **kaybolur**; hiç değmiyorsa (boş savuruş) hak durur. Hak round'lar arasında korunur.
- **Anten konumu**: kenar (batı / doğu / güney / kuzey) + şerit. Anten açık alanın hemen dışında durur, içeri bakar; hiçbir
  tarla hücresini işgal etmez. Tarla büyürse aynı şeritte yeni kenarın dışına taşınmış sayılır.
- **Hat**: iki antenin kenar hücreleri arasında tam sayı adımlı doğru (Bresenham); hücre sayısı = max(|dx|, |dz|) + 1.
  3 hücreden kısa hat veren konum (ör. ilk antenin hemen yanı ya da aynı köşe hücresi) seçilemez.
- **Normal davranış tetiği** (Aşırı Yükleme): doğrudan hasadın, şansı tutmuş ve gerçekten başlamış davranışı. Zincirden,
  artçıdan ya da ek proc'tan doğan davranış hak doğurmaz. Tile'ın pasif stat'ları tetik sayılmaz ve ikinci kez uygulanmaz:
  ek proc, saksının zaten bir kez hesaplanmış normal davranış hasarını kullanır. Kasırga / bumerang ek proc'u aktif sınıra
  (12) tabidir: sınır doluysa düşer (beklemez, gizlice çalışmaz).
- **Doğrudan saldırı duraklaması** (Dolunay): yalnız oyuncunun saldırı zamanlayıcısı bekler. Input, imleç, davranışlar, üretim
  ve round süresi sürer. Süre oyun zamanıyla azalır; istek 0,6 sn'ye kırpılır; her kaynağın kendi kilidi vardır (biri diğerini
  kaldıramaz); round sonu, yeni run ve menüde kalkar; görseli beklemez.

### 5.2 Anten yerleşimi

Karttaki "AL" düğmesi ödülü hemen almaz: ödül ekranı kapanır, tarla görünür ve geçerli kenar konumları işaretlenir. Konumun
üstüne gelince etkileyeceği hücreler gösterilir. Tıklayınca ödül **tek işlemle** uygulanır (konum geçersizleştiyse reddedilir ve
hiçbir şey değişmez). Esc ya da "VAZGEÇ": aynı teklife dönülür; stack, etki ve teklif değişmez. Panel kapanıp açılırsa bekleyen
yerleşim durur; run biter ya da menüye dönülürse tek temizlik yolu onu da kapatır. Anten mevcut grid modifier'ını ve saksı
yerleşimini değiştirmez.

## 6. Tarla olayları

Boss ödülü değildir; ayrı katman (`FieldEventDirector`). Ortak yaşam döngüsü:
**Hazırlık → Ortaya çıkış → Aktif → Çözülme → Temizlik**. Bölge ve hedefler olay kurulurken bir kez seçilir; hazırlıkta
gösterilen ile uygulanan aynıdır.

**Ortak kurallar**

- Aynı anda en çok bir tarla olayı. Boss'un kendi kuralı (Don Cephesi, Sert Kabuk, Sis) bundan bağımsız sürer.
- R26'dan önce olay yok. Boss round'larında (R26, 30, 33, 36, 40, 43, 46, 50) Diken Kuşatması / İstilacı Filiz / Aşılı Kabuk'tan
  biri (eşit ağırlık; bir öncekiyle aynısı gelmez). Diğer round'larda %50 olasılıkla Gezgin Bereket.
- Takvim, süre ve alan değerleri tek veri kaynağındadır (`FieldEventPlan_GecOyunV1`, üretici: `gec_oyun_v1_params.py`).
- Seçim akışı run seed'i + round'dan türeyen ayrı bir akıştır: boss seçimini, ödül teklifini ve oyunun rastgelelik akışını
  değiştirmez (testli).
- **Alan etkisi üretim noktasına uygulanır, saksının tamamına değil**: çok hücreli saksının yalnız bölgedeki üretim noktaları
  etkilenir (Don Cephesi ile aynı kural). Bir üretim noktası tek hücrededir: aynı etki ona iki kez uygulanamaz.
- Müdahale hedefi yalnız oyuncunun **doğrudan saldırısıyla** vurulur: saldırı dairesi hedefin hücresine değiyorsa
  dayanıklılık 1 azalır (hasar miktarına bağlı değil; oyuncunun gücüyle ölçeklenmez). Bir saldırı bir hedefi en çok bir kez
  sayar. Davranış, artçı, zincir ve ödül etkisi vuruşları hedefe ulaşmaz. İmleç arayüzün üstündeyken hedef sayılmaz.
- Kilitli ve tarla dışı hücre seçilmez. Bölge seçimi eşit olasılıklıdır; saksının gücüne ya da türüne bakılmaz.
- Saksı, bitki, tile modifier'ı, rezonans ve yerleşim hiçbir aşamada silinmez ya da değiştirilmez.
- Temizlik tek yoldan ve idempotenttir: fazın bitmesi, round sonu (kayıp ve zafer dahil), menü, yeniden başlatma.
  Duraklatmada (zaman ölçeği 0) olay da durur.

| Olay | Bölge | Etki | Müdahale | Bitiş |
|---|---|---|---|---|
| **Diken Kuşatması** | 3 × 3 dikdörtgen; üretim noktalarının en çok %40'ı, hiçbir zaman tamamı | Bölgedeki üretim noktaları bitki çıkarmaz ve beklerken süre biriktirmez | 2 kök (bölgenin iki karşı köşesi), her biri 6 doğrudan saldırı | İki kök kırılınca hemen açılır; yoksa 30 sn sonra kendiliğinden |
| **İstilacı Filiz** | Ana filiz bir üretim noktasında; 2,5 sn'de bir dört yönlü komşuya yayılır (en çok 8 parça, 2 hücre yarıçap, 20 sn) | Etkilenen hücrede üretim aralığı ×1,6 | Ana filiz: 8 doğrudan saldırı | Ana filiz kırılınca bütün etkiler kalkar; yoksa 35 sn |
| **Aşılı Kabuk** | Örtüşmeyen iki 3 × 2 bölge (A, B); birlikte üretim noktalarının en çok %60'ı | Seçim (8 sn) bitince bir bölgede üretim 20 sn durur | İki düğüm (bölge başına bir), her biri 4 doğrudan saldırı | Kırılan düğümün bölgesi kurtulur, diğeri kapanır |
| **Gezgin Bereket** | 3 × 3 kare; 5 sn'de bir hücre kayar | Yok (ceza yok) | Bölgede 6 doğrudan hasat → boş üretim noktaları canlanır | 3 canlanma ya da 30 sn |

**Olay başına ayrıntı**

- *Diken Kuşatması*: bölge açılınca üretim kaldığı yerden sürer; üretim borcu ya da toplu geri ödeme yoktur. Bölgedeki mevcut
  bitkiler yerinde kalır ve hasat edilebilir.
- *İstilacı Filiz*: adaylar her seferinde mevcut parçalardan türetilir (en çok 4 × parça); yol bulma ya da arama döngüsü yoktur.
  Geçerli komşu yoksa yayılma kalıcı olarak durur. Çarpan hücreye bir kez yazılır (üst üste çarpılmaz).
- *Aşılı Kabuk*: hazırlıkta iki bölge ve sonuç gösterilir; A "varsayılan"dır. İlk kırılan düğüm sonucu kesinleştirir; diğer
  düğüm artık vurulamaz. **Aynı saldırı iki düğümü birden kırabilecekse düğümler liste sırasıyla değerlendirilir: önce A.**
  O saldırıda A kurtulur, B kapanır. Seçim yapılmazsa A kapanır. Kapatma ve açma, Diken Kuşatması'yla aynı ortak bileşendir
  (`FieldZoneEffects`).
- *Gezgin Bereket*: yalnız oyuncunun kendi vuruşuyla ölen ve o anda bölgede olan bitki ilerleme sayılır. Canlanma, boş üretim
  noktasında oyunun kendi üretim yolunu çağırır (aynı bitki zarı, can ve ödül kurulumu); dolu noktaya ikinci bitki eklenmez;
  hasat, kaynak, XP ya da skor üretmez. Canlanan bitkinin hasadı ilerlemeyi doldurmaz. Para birimi ya da kalıcı ilerleme yok.

Arayüz: aktif olayın adı ve tek satırlık durumu ekranın üstünde bir plakada gösterilir (`FieldEventHud`). Aşılı Kabuk'ta
plakanın iki ucunda bölge rengi ve harfi durur (A solda, B sağda); tarladaki bölge belirtisi ve düğüm aynı rengi kullanır
(`LateGamePresenter.ZoneColor`), böylece metindeki "A" / "B" tarladaki bölgeyle eşleşir. Renkler yer tutucudur.

## 7. Mimari

Sorumluluklar ayrıdır; mevcut yöneticilere ödüle özel alan ya da dal eklenmedi.

| Sorumluluk | Yer |
|---|---|
| Doğrudan saldırı ve normal davranış tetiği bildirimi | `AttackFeed` (`PlayerController` ve `PlanterBrain` yalnız buraya bildirir) |
| Ödülün sunulması, alınması, ön koşul, yerleşim adımı, geri alma | `BossRewardManager` (mevcut tek işlemli yol genişletildi) |
| Ödül etkisinin verisi (kural + sayılar) | `RunEffectSO` alt sınıfları — ödül başına bir küçük sınıf |
| Etkilerin yürütülmesi, ortak vuruş, gecikmeli işler, temizlik | `RunEffects` |
| Olay takvimi ve yaşam döngüsü | `FieldEventDirector`, `FieldEvent` |
| Olayın alan seçimi ve oynanış etkisi | `ThornSiegeEvent`, `InvasiveSproutEvent`, `GraftedShellEvent`, `WanderingBountyEvent`, `FieldZoneEffects` |
| Sunum (görsel / ses / HUD / yerleşim arayüzü) | `LateGamePresenter`, `LateGameArtSO`, `FieldEventHud`, `RewardPlacementUI` |

Mevcut koda dokunuşlar dar tutuldu: `DamageType.Reward` (sona eklendi), `HarvestLink.Source`, `PlantHealth.Overkill`,
`PlayerController` (üç bildirim + duraklama denetimi), `PlanterBrain` (dört tetik bildirimi), `PlantSpawner` (kapalı bölge
bekletmesi, ayrı yavaşlatma çarpanı, `TryReviveNow`), `BossRewardSO` (`effect`, `requires`), `BossRewardPoolSO` (ön koşul
denetimi), `RunProfileSO.fieldEvents`, `RoundManager` (iki bileşeni kurar), `UIManager` (üç sunum parçasını kurar).
Dış paket, DI çatısı, node editörü ya da genel bir efekt çatısı eklenmedi.

Sayısal güvenlik: bütün etki hasarları `NumericSafety` üzerinden tam sayıya döner (yeni yer adı: `RewardDamage`).

## 8. Veri

Tek kaynak: `Tools/Balance/GecOyunV1/gec_oyun_v1_params.py` → `make_gec_oyun_v1.py` (`--check` ile salt okunur denetim).
Üretilenler: profil, havuz, 13 ödül, 13 etki, olay planı. Ayrıntı: `Tools/Balance/GecOyunV1/README.md`.

## 9. Karar bekleyen açık noktalar

### 9.1 Hilal–rezonans bağlantısı (tasarımda duruyor; uygulanmadı)

Bugünkü durum: rezonanslar saksının tile bileşiminden hesaplanan pasif / koşullu etkilerdir; hedef saksının ödül rezonansları
(Şöhret, Bolluk, Bilgelik, Değerli Hasat …) hilalin hasat ettiği bitkilere **zaten** uygulanır (ödül eski yoldan verildiği için).
Hilal vuruşu doğrudan vuruş olmadığından Odak'ı, davranış olmadığından Güçlendirilmiş Hasat'ı almaz.

Patlama / pulse içermeyen, hiçbir stat'ı yeniden eklemeyen iki kısa öneri:

1. **Rezonans büyümeyi korur (önerilen).** Hilalin bir adımı, en az bir aktif rezonansı olan saksının hücresinden geçiyorsa
   o adım hasat etmese de Büyüyen Ay'ın büyümesi **sıfırlanmaz** (dalga başına en çok 1 kez). Hasar, şans ve rezonans
   hesabı değişmez; yalnız mevcut "başarısız adım sıfırlar" kuralına rezonansa bağlı tek bir istisna gelir. Büyüyen Ay
   yoksa hiçbir etkisi olmaz.
2. **Odak rezonansı hilale okunur.** Hilal, Odak rezonansı aktif saksının bitkisine vururken o saksının Odak **rezonans**
   çarpanını (×1,5 / ×2 / ×3) alır — tile'ın kendi Odak stat'ını değil. Çarpan o vuruşta bir kez okunur; birikmez, kalıcı
   değildir. Bu, hilalin hasarını doğrudan büyütür (denge etkisi 1. öneriden büyüktür).

Karar gelene kadar bağlantının hiçbir etkisi kodda yoktur.

### 9.2 Yorum gerektiren ve onay bekleyen kurallar

- **Kapalı Devre**: "hattın hasatlarıyla ilişkili davranış tetiği" şu kurala çevrildi: normal hat vuruşu davranışlı saksının
  bitkisini hasat ederse o saksının en yüksek davranış şansıyla zar atılır; tutarsa tek bir ek hat vuruşu gelir. Başka bir
  okuma (ör. hat üzerindeki saksının normal davranış tetiği ek vuruş başlatsın) mümkündür.
- **İkinci anten**: İletken Hat'ın anteni de kendi üç hücresine vurur (veride `secondAntennaPulses`; kapatılabilir).
- **Anten ve hilal hasarı** davranış çarpanlarından (Yıkım Gücü vb.) yararlanmaz; yalnız hasat hasarı stat'ından ölçeklenir.
- **Hasat Yankısı** tek değil, hasat başına bir yansıma vurur (en çok 3).
- **Nadir Reaktör** yalnız doğrudan hasatla çalışır (davranış hasadı çalıştırmaz).
- **Tarla olayı takvimi**: boss round'larında her zaman bir olay; normal round'larda %50 Gezgin Bereket.
- **Aşılı Kabuk** çift temasında A'nın önceliği ve varsayılan bölgenin A olması.
- Bütün sayılar (bölüm 5 ve 6 tabloları).

### 9.3 Bu paketin dışında bırakılanlar

Rezonans Çöküşü, Rezonans Darbesi / Tetik Darbesi, Şarjlı Savuruş, Zincir Reaksiyon; Bilgi Filizi ve Canavar Kesimi round
sınırları; mevcut kartların aşama değişikliği; denge turu; nihai görsel ve ses; R50 finali; endless.

## 10. Yer tutucu varlıklar

Bütün görseller kodla üretilen tek renkli düşük poligonlu şekillerdir (`PlaceholderShapes`); ses yoktur. Liste ve bağlama
kuralları: [Bolum3-8-IcerikBaglama.md](Bolum3-8-IcerikBaglama.md). **Görsel polish tamamlanmadı.** Dış kaynaklı içerik
eklenmedi (lisans bildirimi gerektiren bir şey yok).

## 11. Test ve sınırlar

Bütün testler izole proje kopyasında (`Library/VerificationProject`), kısılmış çalıştırıcıyla (düşük öncelik, 4 çekirdek, tek
Unity) koşturuldu. Açık editöre komut gönderilmedi; gerçek kayda ve seçili profil dosyasına yazılmadı. Çıktılar:
[Bolum3-8/](Bolum3-8/) (`GecOyunV1Verification.txt`, `Regresyon-20261007.txt`, dört yakalama).

### 11.1 Yeni işlev testi

`GecOyunV1Verification` — **PASS, 97 denetim**. GameScene'de, yeni profille, oyunun normal yolundan:

- Veri: profil / havuz / plan geçerli; eski 19 ödül aynı asset, aşama, sıra, stack ve ağırlıkta; eski profillerde olay ve etki yok.
- Takvim: R26'dan önce olay yok; boss round'unda boss olayı, normal round'da Gezgin Bereket; olay seçimi ödül teklifini ve oyunun
  rastgelelik akışını değiştirmiyor (R26 teklifi olay seçiminden önce ve sonra aynı; 40 seed × 50 round'da takvim kuralı).
- Dört olay: yaşam döngüsü, hazırlıkta etki yok, hedef yalnız doğrudan saldırıyla ve saldırı başına bir kez, arayüz üstündeyken
  sayılmıyor, çok hücreli saksıda üretim noktası başına etki, boss kuralıyla üst üste (8 → 32 sn; biri kalkınca diğeri kalıyor),
  süre dolunca kendiliğinden açılma, Aşılı Kabuk'ta B'yi kurtarma / seçmeme / çift temas, Gezgin Bereket'te ilerleme, canlanma
  ve hareket, duraklatma, round sonu, kayıp, menü ve yeniden başlatmada temizlik.
- 13 ödül: gerçek teklif → seçim → etki (R3'te gerçek teklifte Elektrik Anteni ve yerleşim adımı dahil), ön koşullar teklifte ve
  alımda, tek alım, yerleşimde vazgeçme / yeniden seçme / geçersiz konum reddi, her etkinin tetik, hedef, hasar, sınır ve
  beklemesi; yeni vuruşların zincire, Hasat Ritmi'ne ve davranış tetiğine girmemesi; duraklama üst sınırı ve kaynak kilitleri;
  kasırga / bumerang 12 sınırı; sıfırlamada etki, dinleyici ve zamanlayıcı kalmaması.

### 11.2 Mevcut işlev takımları (son kodla)

| Takım | Sonuç |
|---|---|
| `FullGameFixVerification` | PASS 71 |
| `BossRewardVerification` | PASS 127 |
| `ZincirV1Verification` | PASS 39 |
| `BedelliOdullerV1Verification` | PASS 94 |
| `AlanGeriBildirimiVerification` | PASS 23 |
| `HarvestBehaviorVerification` | PASS 114 |
| `DemoVerification` | 65 denetim PASS, 0 FAIL (aşağıdaki not) |
| `KirilmaV1Verification` | PASS 189 |
| `TakvimV1Verification` | PASS 162 (aşağıdaki not) |
| `XPV1Verification` | PASS 39 |
| `AlphaDengeV1Verification` | PASS 51 |
| `OdulAsamalariV1Verification` | PASS 71 |
| `KirilmaErisimiV1Verification` | PASS 35 |
| `SayisalGuvenlikVerification` | PASS 33 |
| `Refactor3762Verification` | PASS 8 |
| `SimpleResonanceVerification` | PASS 294 |
| `PerformanceStressVerification` | PASS (yaşam döngüsü ve ayırma) |
| `BossWeatherVerification` | PASS 22 |
| `MechanicsVerification` | PASS 88 |

Notlar (gizlenmeden):

- **`DemoVerification`**: sonuç dosyasının ilk satırı "EDITOR INFRASTRUCTURE (not gameplay): ArgumentOutOfRangeException" yazar.
  Bu, Unity editörünün açılıştaki arama dizini (`UnityEditor.Search.SearchDatabase`) istisnasıdır; test onu oyun hatası saymayıp
  not olarak yazar. Dosya, bu işten önce kaydedilmiş 6 Ekim sonucuyla (`Docs/FullGame-PlaytestFix-20261006/DemoVerification.txt`)
  bayt bayt aynıdır: DemoScene davranışı değişmedi.
- **`TakvimV1Verification`** ilk koşuda **FAIL** verdi: zafer ekranı denetimi eski tek satırlık "Boss ödülleri: …" metnini arıyordu.
  Tam sürüm sonuç ekranı 6 Ekim'de başlık + madde satırlarına çevrilmişti (`RunComplateUI.cs`, bu işten önceki değişiklik;
  `BossRewardVerification` o gün güncellenmiş, bu takım 4 Ekim'den beri koşmamıştı). Hata bu paketten kaynaklanmıyordu.
  Denetim aynı anlamla güncel biçime çevrildi (liste başlığı var **ve** R50'de alınan ödül kendi madde satırında — eskisinden
  dar değil); ardından takımın kalan adımları da koştu: 162 denetim PASS.
- Eski testlerde bu iş için yapılan diğer değişiklikler bölüm 12'de listelidir (rezonans döngüsü, profil listeleri). Hiçbir
  beklenti geçsin diye gevşetilmedi.

### 11.3 Yoğun tarla ölçümü (yalnız batch CPU / bellek)

11 × 11 tarla, 121 bitki, 9 hücreli doğrudan saldırı, 400 saldırı:

| Durum | Saldırı başına süre | Saldırı başına ayırma | Etki / zamanlayıcı / sunum nesnesi |
|---|---|---|---|
| Taban (etki yok) | 0,551 ms | 0 B | 0 / 0 / 2 |
| 13 ödül + aktif tarla olayı | 0,594 ms | 0 B | 13 / 4 / 96 |

Zamanlayıcı kuyruğu 128, dekor havuzu 96 ile sınırlıdır; sınır dolunca yeni iş / dekor düşer (birikmez). Bu ölçümde dekor havuzu
doldu (96 / 96) ve 1 064 kısa ömürlü dekor isteği atlandı: ölçüm döngüsü 400 saldırıyı kare ilerletmeden art arda yaptığı için
dekorlar sönmeye fırsat bulmadı (normal oyunda saldırılar arasında kare geçer). Zamanlayıcı kuyruğundan iş düşmedi. Bu ölçüm kısılmış batch
editöründe CPU süresi ve yönetilen bellek ayırmasıdır; koşudan koşuya ±%5 oynar. **GPU, FPS, gerçek oyuncu donanımı ve insan
Play ölçümü değildir.**

### 11.4 Yapılmayanlar ve sınırlar

- İnsan Play testi yapılmadı: okunurluk, his, tempo ve "güç" duygusu doğrulanmadı.
- Denge ölçümü yok: 60 run'lık tur bu görevde bilerek başlatılmadı. Yeni ödüllerin ve olayların kazanma oranına etkisi bilinmiyor.
- Zafer (R50) yolundaki olay temizliği ayrı testle denenmedi; kayıp, round sonu, menü ve yeniden başlatmayla aynı durum
  işleyicisinden geçer.
- Yakalamalar batch görüntüsüdür (1600 × 900, post-processing kapalı); yer tutucuların yalnız "görünür ve ayırt edilir" olduğunu
  gösterir. Bölge belirtileri kamera uzaklığında küçük ve soluktur; nihai okunurluk kararı değildir.
- Durum plakası ekranın üst ortasındadır; tarlanın **uzak köşesi** hemen altında kalır. Olay bölgesi o köşeye düşerse yüksek
  hedef yer tutucusunun üst kısmı plakanın arkasında kalır (`G1_01` yakalamasında görülüyor). Hedefin hücresi görünür ve
  vurulabilir, ama okunurluk düşer: plakanın yeri ya da hedef yüksekliği Play'de karar verilecek bir yerleşim konusudur.
- Yaklaşan olay round öncesi ÖNİZLEME ekranında gösterilmiyor (yalnız round içi hazırlık aşaması ve durum plakası).
- Editor Play penceresi (Tools > Geç Oyun Testi) derleniyor; açık editörde elle denenmedi (açık editöre dokunulmadı).

## 12. Değişen dosyalar

Başlangıç durumu: `main`, HEAD `ec6bd3d`, çalışma ağacında bu işten önce 100 kayıt (değişmiş / izlenmeyen) vardı. Commit atılmadı.
Aşağıdaki liste yalnız bu işin dokunduklarıdır; `.meta` dosyaları ayrıca yazılmadı.

**Değişen mevcut oyun kodu**

| Dosya | Değişiklik |
|---|---|
| `Assets/Scripts/Enums/DamageType.cs` | `Reward` türü (sona eklendi; saksı bonusu ve davranış tetiklemesi yok). |
| `Assets/Scripts/Behaviors/HarvestLink.cs` | `Source` (`RewardSource`) alanı; `Reward(...)` ve `Excluded(...)` üreticileri. Mevcut zincir alanları aynı. |
| `Assets/Scripts/Plants/PlantHealth.cs` | `Overkill` (ölümde artan hasar; yalnız okunur). |
| `Assets/Scripts/Player/PlayerController.cs` | `DirectAttackPause` denetimi; `AttackFeed.Begin / Hit / End` çağrıları. Saldırı zamanlaması ve hasar formülü aynı. |
| `Assets/Scripts/Planters/PlanterBrain.cs` | Başarılı normal davranış tetiklemesinden sonra `AttackFeed.NotifyBehavior`. |
| `Assets/Scripts/Planters/PlantSpawner.cs` | Tarla olayı üretim engeli ve yavaşlatma çarpanı; `SpawnedPlant`, `TryReviveNow`. |
| `Assets/Scripts/Stats/NumericSafety.cs` | `NumericSite.RewardDamage`. |
| `Assets/Scripts/ScriptableObjects/BossRewardSO.cs` | `effect`, `requires` alanları (eski asset'lerde boş). |
| `Assets/Scripts/ScriptableObjects/BossRewardPoolSO.cs` | `Validate`: etkili ödül tek alım, ön koşul aynı / önceki aşamada. |
| `Assets/Scripts/ScriptableObjects/RunProfileSO.cs` | `fieldEvents` alanı (eski profillerde boş: olay yok). |
| `Assets/Scripts/Managers/BossRewardManager.cs` | Etki ekleme / geri alma, ön koşul engeli, yerleşim adımı, `EditorGrant`'e konum. |
| `Assets/Scripts/Managers/RoundManager.cs` | `RunEffects` ve `FieldEventDirector` bileşenleri. |
| `Assets/Scripts/Managers/UIManager.cs` | Yerleşim paneli, olay plakası, sunucu kurulumu. |
| `Assets/Scripts/UI/BossRewardText.cs`, `Assets/Scripts/UI/BossRewardPanelUI.cs` | Etkili ödül kartı metni ve yerleşim isteği. |
| `Assets/Editor/RunProfileMenu.cs` | Yeni profil menü satırı. |

**Yeni oyun kodu**

- `Assets/Scripts/RunEffects/`: `AttackFeed.cs`, `RunEffectSO.cs`, `RunEffects.cs`; `Effects/` altında 13 ödül etkisi
  (`AntennaEffectSO`, `ConductiveLineEffectSO`, `ClosedCircuitEffectSO`, `CrescentEffectSO`, `GrowingMoonEffectSO`,
  `FullMoonEffectSO`, `CritArcEffectSO`, `HarvestEchoEffectSO`, `OverflowEffectSO`, `RareReactorEffectSO`, `CritBurstEffectSO`,
  `GreedyHarvestEffectSO`, `OverloadEffectSO`).
- `Assets/Scripts/FieldEvents/`: `FieldEventPlanSO.cs`, `FieldEvent.cs`, `FieldEventDirector.cs`, `ThornSiegeEvent.cs`,
  `InvasiveSproutEvent.cs`, `GraftedShellEvent.cs`, `WanderingBountyEvent.cs`.
- `Assets/Scripts/LateGameArt/`: `LateGameArtSO.cs`, `LateGameContent.cs`, `PlaceholderShapes.cs`, `LateGamePresenter.cs`,
  `FieldEventHud.cs`, `RewardPlacementUI.cs`.
- `Assets/Editor/LateGameTestWindow.cs` (Tools > Geç Oyun Testi; yalnız Play).

**Veri**

- Üretici: `Tools/Balance/GecOyunV1/` (`gec_oyun_v1_params.py`, `make_gec_oyun_v1.py`, `README.md`, `generated_manifest.json`).
- Üretilen: `Assets/ScriptableObjects/RunEffects/GecOyunV1/` (13), `Assets/ScriptableObjects/BossRewards/GecOyunV1/` (13 ödül + havuz),
  `Assets/ScriptableObjects/FieldEvents/GecOyunV1/FieldEventPlan_GecOyunV1.asset`,
  `Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset`.
- Elle (üretici dışı): `Assets/Resources/LateGameArt.asset` (içerik kataloğu; 18 yuva boş).

**Testler**

| Dosya | Değişiklik |
|---|---|
| `Tools/Verification/Editor/GecOyunV1Verification.cs` | Yeni işlev testi. |
| `Assets/Editor/SimpleResonanceVerification.cs` | "Direct dışındaki her tür ×2" döngüsü dört davranışa açıkça yazıldı (aynı dört beklenti) ve `Reward` türünün rezonanstan etkilenmediği (×1) eklendi. Beklenti gevşetilmedi; yeni tür döngüye yanlışlıkla girmesin diye. |
| `Tools/Verification/Editor/KirilmaV1Verification.cs`, `TakvimV1Verification.cs`, `XPV1Verification.cs`, `AlphaDengeV1Verification.cs`, `ZincirV1Verification.cs` | Profil / havuz listelerine yeni profil eklendi (yeni profil Alpha'nın takvimini, süre tablosunu ve zincir asset'ini taşır). |
| `Tools/Verification/Editor/TakvimV1Verification.cs` | Ayrıca: zafer ekranı denetimi 6 Ekim'deki tam sürüm ödül listesi biçimine çevrildi ("BOSS ÖDÜLLERİ" başlığı + R50 ödülü kendi madde satırında). Bu paketten bağımsız eski bir uyumsuzluktu; ayrıntı 11.2. |

**Belge**

- `Docs/Bolum3-8-GecOyunOlaylariVeBossOdulleri.md` (bu belge), `Docs/Bolum3-8-IcerikBaglama.md`, `Docs/Bolum3-8/` (test çıktıları ve
  yakalamalar), `Docs/TODO-Run50-Endless-20261002.md` (P11-a bölümü).

**Dokunulmayanlar** (başlangıç ve bitiş SHA-256 özetleri karşılaştırıldı): `Assets/Resources/RunProfileSelection.asset`,
`Assets/Scenes/DemoScene.unity`, `Assets/Scenes/GameScene.unity`, gerçek kayıt (`meta.json`, `meta.json.bak`). Mevcut profil,
denge ve ödül asset'leri değişmedi (Alpha üreticisi `--check` temiz).

## 13. Play kontrol adımları (10–15 dakika)

1. **Profil**: *Tools > Run Profili > Run50 Geç Oyun V1*. GameScene'i aç, Play'e bas. *Tools > Geç Oyun Testi* penceresini aç
   (kayda ve profil seçimine yazmaz).
2. **Hazırlık (2 dk)**: birkaç saksı yerleştir (en az biri patlama ya da elektrik tile'ında olsun), round'u başlat.
3. **Diken Kuşatması (2 dk)**: pencereden başlat. Zemin belirtilerini ve durum plakasını gör; üretimin bölgede durduğunu,
   dışında sürdüğünü izle. Köklere imleçle vur (dayanıklılık pencerede düşer); iki kök kırılınca bölgenin açıldığını gör.
   "Kesin alan çizgisi"ni aç / kapat.
4. **İstilacı Filiz (1 dk)**: başlat; yayılmayı ve yavaşlayan üretimi izle; ana filizi kır.
5. **Aşılı Kabuk (2 dk)**: başlat; hangi düğümün hangi bölgeyi kurtardığı okunuyor mu bak (plakanın iki ucundaki A / B renk
   kutuları tarladaki bölge ve düğüm rengiyle aynı olmalı). Bir düğümü kır; ikinci kez başlatıp hiç dokunma (A kapanmalı).
6. **Gezgin Bereket (1 dk)**: başlat; bölgenin içinde hasat yap, eşikte boş yuvaların canlandığını gör.
7. **Anten ailesi (2 dk)**: pencereden Elektrik Anteni → İletken Hat → Kapalı Devre ver. Antenin yerini, vuruşların hangi
   hücrelere gittiğini ve pencerede bekleme sürelerini izle.
8. **Hilal ailesi (2 dk)**: Hilal Darbesi → Büyüyen Ay → Dolunay ver. Her 6. vuruşta dalgayı, hasat edince genişlemesini,
   Dolunay'da karşı dalgayı ve kısa vuruş duraklamasını (imleç hareket eder, vuruş bekler) gör.
9. **Diğerleri (2 dk)**: Kritik Ark, Kritik Patlama, Hasat Yankısı, Taşan Hasar, Açgözlü Hasat, Nadir Reaktör, Aşırı Yükleme'yi
   tek tek ver; pencerede sayaçların arttığını izle.
10. **Gerçek akış (1 dk)**: "Bütün test etkilerini temizle"; yeni bir run'da R3 boss'unu geç. Ödül ekranında Elektrik Anteni
    çıkarsa "AL" → konum seç → Esc ile vazgeç → yeniden seç. (Çıkmazsa sonraki boss'larda dene.)
11. **Temizlik**: round sonunda, menüye dönüşte ve yeniden başlatmada olay ve etkilerin kalktığını gör.

Bakılacaklar: okunurluk (hedefler kalabalık tarlada seçiliyor mu), durum plakasının metni, olay sıklığı ve süreleri, ödüllerin
"güç" hissi. Bunlar insan testi maddeleridir; otomatik test bunları kapatmaz.
