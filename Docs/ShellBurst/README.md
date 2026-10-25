# Aşılı Kabuk kişiliği — toon VFX çalışması (25 Ekim 2026)

Kaynak: `Tools/Art/ShellBurst/build_shell_burst.py` (Diken çalışmasının toon yardımcılarını kullanır).
Kullanıcı: her kaprisin kendi kişiliği olmalı; önerilen "aşı damarlı zırh tohumu" seçildi.

Kabuk = ağır, kalın, inatçı (Diken saldırgan ve patlayıcı, Filiz sinsi ve yayılan). Dil: kalın, lob lob şişkin kabuk plakaları,
çatlak çizgileri; gövde koyu kabuk kahvesi, bölge rengi damarlarda ve çekirdekte: A amber #FFB723 (▲), B mor #AA5FEB (◆).
Mavi yok (mavi = elektrik).

24 kare/sn, 146 kare:
- **Hazırlık (1–22):** düğüm hücresinde toprak plaka plaka çatlar, aralarından bölge ışığı sızar, plakalar titrer. Düğümden kendi
  bölgesindeki her hücreye "aşı damarı" uzanır; ucunda saksı kenarını saran kelepçe. Damarlarda ışık boncuğu koşar
  (hangi düğümün hangi hücrelere bağlı olduğu okunur).
- **Çıkış (A 22, B 24):** düğüm toprağı iterek titreye titreye kalkar, plakaları dışa devirir, biraz aşar ve "TOK" diye oturur:
  yere taş tozu halkası + mürekkep şok halkası. Patlama yok; ağırlık var.
- **Vuruş (52, 60, 68):** her vuruşta kabukta yeni bir çatlak (içinden bölge ışığı), sert kabuk kıymıkları, kısa "tink"
  kıvılcımı (altı düzensiz kırık ışın; Diken'in kırmızı yıldızı değil). Düğüm tabandan ağır ağır sallanır. Üçüncü vuruştan sonra
  kırılmak üzereymiş gibi titrer. Çatlak sayısı = kalan vuruş göstergesi.
- **Kırılma (76):** kabuk beş plakaya ayrılıp ceviz gibi açılır, tepe topuzu fırlar, çekirdek yükselip kıvılcımlarla söner.
  A'nın damarlarından amber boncuklar hücrelere koşar; boncuk varınca kelepçe kopup savrulur, kıvılcım çıkar (hücre serbest),
  damar söner.
- **B kapanır (78–100):** B düğümü sarsılıp toprağa çekilir. Damarlarından mor boncuklar hücrelere koşar; boncuk varınca o
  hücrede kabuk yaka kalkar: altı plaka kenardan kalkıp içe kapanır, toprağı kabuk plakaları örter, aralıklardan mor ışık sızar.
  Bitkiler görünür kalır (kubbe yok; uzun bitkilerin içinden geçmesin diye).
- **Açılma (118–146, oyunda 20 sn sonra):** yakalar çatlar ve titrer, plakalar dışa devrilip düşer; kıymık ve toz çıkar, damarlar söner.

Düğüm, plakalar, damar ve tarla bu önizlemede yer tutucudur. Oyuna aktarımda oyundaki hücre boyuna ve saksılı/saksısız hücreye
göre yerleştirilir.

Dosyalar: `Kabuk_Yakin_*`, `Kabuk_Genis_*` (iki bölge), `Kabuk_Oyun_*` (oyun kamerası mesafesi) — gerçek hız MP4, 2× yavaş GIF,
3× yavaş MP4; `Kabuk_Kareler_Yakin.png`, `Kabuk_Kareler_Genis.png`; `ShellBurst.blend`. Kareler `frames_*/` altında.

## Düzeltme 1 (aynı gün, ilk render'a bakarak)

- Düğüm fıçı / kovan gibi duruyordu (yatay bantlar) → lob lob şişkin plakalar, koyu ek yerleri; A geniş ve tıknaz, B uzun.
- Çekirdek düz sarı bir leke gibiydi → iki tonlu (üstü açık) + mürekkep kontur.
- Çatlaklar ve "tink" çok inceydi → kalınlaştırıldı.
- Serbest kalma halkaları (büyük sarı halkalar) her şeyi örtüyordu → kaldırıldı; yerine kelepçe kopup savrulur.
- Doğum plakalarının altından ışık görünmüyordu → plakalar arası açıldı, ışık diski plakaların hemen altında.
- Kapanırken yaka plakaları büyük düz parçalar gibi görünüyordu → küçükten büyüyerek kalkar.

## Oyunda (25 Ekim 2026, kullanıcı: "güzel, oyunda görelim")

- Sunum: `Assets/Scripts/LateGameArt/LateGamePresenter.ShellBurst.cs` + `ShellBurstArtSO` (`Resources/ShellBurstArt.asset`).
  Eski kabuk halkaları ve düğüm prefab'ları (ShellCover / ShellNode) artık kurulmuyor (dosyalar ve bağlar yerinde, kullanılmıyor).
  Doğum plakaları tümseğin yerini alır (düğümün doğduğu yer). Damar + kelepçe bölge işaretidir; düğümün altında yoktur.
- Yerleşim: oyunda hücre 2 birim, çalışmayla aynı ölçek. Kelepçe ve yaka saksılı hücrede saksı kenarında, saksısızda zeminde;
  yakanın toprak kabuğu toprağın üstünde. Düğüm toprak yüksekliğinde.
- Vuruş: beyaz flaş yok; tabandan ağır sallanma (ilk anda ~14°, sönümlü), çatlak aşaması (dayanıklılık 4: 1–2–3 çatlak),
  "tink" + 3 kıymık; bir vuruş kalınca titrer. Kırılma: ceviz gibi açılış + çekirdek kıvılcımları. Süre dolunca da kurtulan
  düğüm açılır, kapanan toprağa çekilir. A ve B'nin ikisi de kapanabilir (yaka iç bandı ve çatlak ışığı bölge renginde).
- "TOK" kısa ekran sarsıntısı verir (Diken'in sarsıntı yoluyla; Filiz'de yok).
- Ağ yükü (gövde + mürekkep üçgeni): düğüm 3 363, açılış 3 243, doğum 1 318, damar 642 (bölgede 5 damar), yaka 2 352
  (kapanan bölgede 6 yaka; en yoğun an kapalı bölge: ~14 000 üçgen), vuruş 108. Hepsi aynı toon / mürekkep / kart malzemesi.
- Havuz: düğüm, doğum ve açılış bölge başına 1; damar 12, yaka 8, vuruş 4, çekilme tozu 2 (dolunca en eski kesilir, sayılır).
  Kabuk hazırlanırken çoğu önceden kurulur. Karede 0 bayt (hazırlık ve karar / yaka dizisi ölçüldü). Olay temizlenince,
  round dışında ve kapatılınca havuza döner; açılış çözülme süresi (1 sn) içinde biter.
- Doğrulama: `Tools/Verification/Editor/ShellArtVerification.cs` 164/164 (üç yol: A kırılır, B kırılır, süre dolar);
  Diken ve Filiz testleri değişmeden geçti. Oyun içi çekim `Docs/FieldEvents/shell/Kabuk_Oyun_*` (A kapanışı `Kabuk_Oyun_A_*`).
