19 Ekim 2026: Kritik Patlama oyundan ve aktif test listesinden kaldırıldı. Eski görsel doğrulama sonuçları tarihsel kayıttır.

# Mechanics Test Lab — mekanik ve VFX test sahnesi

Tarih: 12 Ekim 2026 (ilk paket: sahne ve panel; ikinci paket: sayısal doğrulama) · TODO **P13**. Uzun bir run oynamadan,
gerçek oyun sistemleriyle kontrollü şartlarda deneme yapmak ve sonucu sayıyla doğrulamak için.
Denge simülatörü ya da görsel gösterim sahnesi değildir. **İnsan Play testi yapılmadı**; aşağıdaki doğrulama aracın doğruluğunu
ölçer, mekaniklerin "iyi hissettirdiğini" ya da görsellerin kabul edildiğini söylemez.

- Panel: **Tools > Mechanics Test Lab > Paneli aç**
- Sahne: `Assets/Scenes/MechanicsTestScene.unity` (Build Settings'te yok; oyuncu menüsüne bağlı değil)
- Senaryo dosyaları: `Tools/MechanicsTestLab/Scenarios/*.json` (Assets dışında)
- Olay kaydı dışa aktarımı: `Logs/MechanicsTestLab/olay-kaydi-<zaman>.txt`
- Oturum raporu (sayısal doğrulama): `Logs/MechanicsTestLab/rapor-<zaman>/` ya da seçtiğin klasör
- Sayısal doğrulama (önce / sonra, saldırı izi, makbuz, kontroller, şans, rapor): panelde **H · Sayısal** —
  [aşağıda](#sayısal-doğrulama-h--sayısal)

## Nasıl çalışır

`MechanicsTestScene` yalnız bir **başlatıcıdır**: içinde kopyalanmış hiyerarşi yoktur. Play'e basılınca test oturumu gerçek
`GameScene`'i aynen yükler; yani aynı prefablar, yöneticiler, arayüz ve efektler çalışır ve test sahnesi oyundan ayrışamaz.
`GameScene` ve `DemoScene` dosyalarına dokunulmaz.

Oturum yalnız Play, `MechanicsTestScene`'den başladığında açılır. Play başka bir sahnedeyse panel bunu söyler ve hiçbir komut
göndermez (tam oyunda çekim için eski panel duruyor: Tools > Geç Oyun Testi).

Güvenlik:

- **Seçili run profili değişmez.** Senaryonun profili çalışma zamanında kopyalanır ve yalnız o Play oturumu için kullanılır
  (`RunProfileSelectionSO.OverrideForSession`). Seed, başlangıç kaynakları ve boss ataması kopyaya yazılır.
- **Gerçek kayıt yazılmaz.** Kayıt okunur, oturum boyunca yalnız bellekte tutulur (`MetaSave.EditorProtectTestSession`): çiftçi /
  tırpan seçimi, görev ilerlemesi ve kilit açılışları diske gitmez.
- **Kaynak asset'ler değişmez.** Tile, saksı, ödül, boss, denge ve plan asset'leri yalnız okunur.
- Projede dış servis / telemetri gönderimi yoktur; oturum bir şey göndermez.
- Play'den çıkınca zaman ölçeği, saldırı / üretim / sayaç bayrakları, hasar sayısı ayarı ve profil geçersiz kılması geri alınır.
- Aracın bütün kodu `Assets/Editor/MechanicsTestLab/` altındadır (Editor derlemesi); oyun kodundaki dar girişlerin hepsi
  `#if UNITY_EDITOR` içindedir. Oyuncu build'ine girmez.

## 5 dakikalık başlangıç

1. **Sahneyi aç.** Paneli aç (Tools > Mechanics Test Lab > Paneli aç). `A · Oturum` sekmesinde *Test oturumunu başlat*'a bas.
   Açık sahnen değişmez; Play'den çıkınca geri gelir. (İstersen `MechanicsTestScene`'i açıp doğrudan Play'e de basabilirsin.)
   Oturum R1'de, 3 × 3 saksıyla, round sayacı dondurulmuş olarak başlar.
2. **Düzen kur.** `B · Tarla`: grid boyutunu seç, gridden hücre tıkla (Shift + tık: alan). Tile türü / nadirliği / seviyesini
   seçip *Seçili alana uygula*; saksı türü ve yönünü seçip *Köşe hücresine kur*; bitki seçip *Seçili alana bitki koy*; can ver.
   Ya da `G · Senaryolar`'dan hazır bir senaryoyu *Kur*.
3. **Ödül ver.** `D · Ödüller`: listeden *Kur* (kontrollü kurulum). Kurulamıyorsa nedeni satırda yazar.
4. **Saldır.** `A · Oturum`: gridden hücre seç → *Nişanı seçili hücreye al* → otomatik saldırıyı kapat → *Tek saldırı*.
   Bekleme dolmadıysa reddedilir; *Beklemeyi sıfırla* ayrı bir test komutudur. *Fareyle normal kontrole dön* ile oyunu
   normal oynarsın.
5. **Sonucu incele.** `F · İnceleme`: seçili hücre / saksı / bitkinin gerçek durumu, "bu stat neden bu değer?" dökümü, oturum
   durumu (beklemeler, kapasiteler, aktif etkiler) ve olay kaydı. Zamanı `A · Oturum`'dan yavaşlat, duraklat, tek adım ilerlet.
6. **Sıfırla.** *Round'u yeniden başlat* aktif davranışları ve efektleri temizler (tarla ve ödüller kalır). *Senaryoyu yeniden
   kur* her şeyi temiz oturumdan baştan kurar.

## Panel bölümleri

| Sekme | Ne yapar |
|---|---|
| A · Oturum | Oturumu başlat / yeniden kur; round'a git, round'u yeniden başlat, round'u oyunun normal akışıyla bitir; round sayacı ve üretimi dondur; duraklat, ×0,1 / ×0,25 / ×0,5 / ×1, tek adım; otomatik saldırı, sabit nişan, tek saldırı, bekleme; hasar sayıları, alan çizgileri, **oyun HUD'unu gizle / göster**, Scene görünümü hizalama. |
| B · Tarla | Grid boyutu; tıklanabilir grid; tile (tür, nadirlik, seviye, zar konumu); saksı (tür, yön; kur, doldur, değiştir, kaldır); bitki (tür, can; koy, boşalt); toplu yenileme ve temizlik. |
| C · Statlar | Stat tablosu (taban · test'siz · sonuç); test katkısı ekle / kaldır / sıfırla; level ata; XP ver (önizlemeli); kaynak miktarları. |
| D · Ödüller | A) oyunun gerçek teklif ekranı; level kartları ve kart türü yardımcıları; B) kontrollü kurulum, ödül dökümü, ön koşullarıyla kurma, çakışanı kaldırıp yeniden kurma. |
| E · Boss ve olaylar | Boss türü ve round'u seçip başlat / temizle; dört tarla olayını başlat (isteğe bağlı: seçili hücreyi içeren bölge) / temizle; canlı durum. |
| F · İnceleme | Hücre / saksı / bitki incelemesi; stat kaynağı; oturum durumu; olay kaydı (**seviye: Kapalı / İşlem özeti / Ayrıntılı sayısal iz**; tür, metin ve saldırı kimliği süzgeci, temizle, dışarı aktar). |
| G · Senaryolar | Hazır senaryolar; profil ve seed; mevcut düzeni yakala ve kaydet; kayıtlı senaryoyu kur. |
| H · Sayısal | Sayısal doğrulama, altı alt sekme: Önce / Sonra · Saldırı izi · Makbuz ve buff · Kontroller · Şans · Rapor. |

Pencere dar olduğunda (900 pikselin altı) sekiz sekme iki satıra bölünür; sayısal tabloların sütunları pencere genişliğine
göre ölçeklenir.

## Hangi müdahale normal kuralı izler, hangisi test kolaylığıdır

| Müdahale | Durum |
|---|---|
| Saksı yerleşimi | **Normal kural:** oyunun yerleşim doğrulaması (tarla dışı, kilitli hücre, çakışma, üretim noktası–hücre eşleşmesi) ve bağlaması. **Test kolaylığı:** bedel ve satın alma kilidi aranmaz; mağaza açılmaz. |
| Tile ataması | **Test kolaylığı:** hücre, tür, nadirlik, seviye ve değer serbest seçilir. Oyunda tile yeri rastgeledir ve oyuncu seçemez; o kural **değişmedi** (`ProgressionManager`). Uygulama ve seviye oyunun yoludur (`GroundCell.ApplyModifier` / `AddLevels`); saksı toplamları ve rezonans oyunun yeniden hesabından gelir. Davranış tile'ının kart kilidi (kasırga, bumerang) aranmaz. |
| Bitki koyma / can | **Test kolaylığı:** tür seçilir, can 1 … azami arasında ayarlanır. Doğuş oyunun yoludur (aynı havuz, can eğrisi, ödül kurulumu). Varsayılan olarak yalnız saksının kendi üretim tablosundaki bitkiler; tablo dışı ayrı izin ister. |
| Saksı / bitki kaldırma | **Test kolaylığı:** iade, satış animasyonu ve hasat ödülü yoktur. |
| Grid boyutu | **Normal kural:** oyunun açma yolu. Canlı küçültme yoktur (oyunda yok): senaryoyla temiz oturumda uygulanır. |
| Test stat katkısı | **Test kolaylığı**, ama ayrı ve görünür kaynak: oyunun modifier listesine eklenir; taban, tavan ve dönüşüm kuralları aynen uygulanır. Yalnız test katkısı kaldırılır. |
| Level atama | **Test kolaylığı:** kart hakkı doğmaz. |
| XP verme | **Normal kural:** oyunun `AddXP` yolu; level, seçim hakkı ve kart ekranı normal akıştan. |
| Kontrollü ödül kurulumu | Teklif zarı ve round **beklenmez** (kolaylık); alınabilirlik (stack, dışlama, ön koşul, seçim hakkı aralığı, koşul) ve uygulama (kazanç, bedel, stat, etki) **oyununkidir**. Hiçbiri aşılmaz. |
| Normal teklif | **Normal kural:** round oyunun round sonu akışıyla biter; teklif oyunun üreticisinden, ödül oyunun ekranından. *Kolaylık:* varsayılan olarak kota ve boss hasadı "geçilmiş" sayılır (kutuyla gerçek değerlendirme açılır). |
| Tek saldırı | **Normal kural:** oyunun saldırı yolu; bekleme ve Dolunay duraklaması atlanmaz. *Kolaylık:* nişan sabitlenebilir; bekleme ayrı komutla doldurulabilir. |
| Sabit nişan / panelden saldırı | Tarla olayı hedefi "imleç arayüzün üstündeyken" vurulmaz; bu kural fare nişanı içindir. Nişan sabitken fare nişan olmadığı için bu soru o sırada "hayır" döner (oyunun bu amaçla bıraktığı test girişi). Fareyle normal kontrolde kural aynen geçerlidir. |
| Zaman | Oyunun kendi saati (`Time.timeScale`); ayrı sayaç yok. Menü ekranlarının duraklatması ve round sonu yavaşlaması oyuna aittir. |
| Round sayacı / üretim dondurma | **Test kolaylığı.** |
| Round'a git | Oyunun mevcut round atlama girişi: tarla ve güçler korunur, atlanan round'ların ödülü verilmez, **bekleyen kart seçimleri sıfırlanır**. |
| Boss | Seçilen boss çalışma zamanı profil kopyasının takvimine yazılır; hazırlık, etkinleşme ve temizlik oyunun yürütücüsündedir. Havuzun uygunluk kuralı varsayılan olarak aranır; "yok say" ayrı kutudur. |
| Tarla olayı | Oyunun yürütücüsü ve planı. *Kolaylık:* takvim beklenmez (R26'dan önce de elle başlar; panel bunu yazar). Bölge, olayın kendi seçim kodundan çıkar: istenen hücre için seed'ler önizlenir; o hücreyi içeren geçerli bölge yoksa olay **başlamaz**. Aynı anda tek olay kuralı sürer. |
| Alan çizgileri, Scene işaretleri | Yalnız test göstergesi; varsayılan kapalı. Oyunun VFX'inin yerine geçmez ve mekaniğin çalıştığını kanıtlamaz. Saf VFX önizlemesi (efekti mekaniksiz oynatma) araçta **yoktur**. |

Korunanlar: mor zincir yayılım bildirimi, Hasat Ritmi imleci, Artçı alan çizgisi ve bütün efektler oyunun kendisidir; araç
onlara dokunmaz. Kasırga / bumerang sınırı denge verisinden okunur (profil: 12 + ödül bonusu).

## Senaryolar: kaydetme ve yeniden kurma

Senaryo bir **oyuncu kaydı değildir**; yalnız kurulum verisidir (JSON, içerik adla / kimlikle).

- **Yakalanan:** profil adı, seed, round, grid boyutu, tile'lar (tür, nadirlik, seviye, seviye 0 değerleri), saksılar (tür, köşe,
  yön), bitkiler (tür, eksikse can), test stat katkıları, ödüller ve stack'leri (kontrollü kurulumda seçilen yerleşim konumu
  dahil), level, kaynaklar, aktif boss, saldırı / üretim / sayaç bayrakları, sabit nişan.
- **Yakalanmayan:** süren davranışlar, bekleyen zincir / artçı / yankı işleri, tarla olayı, geçici etkiler, sayaçlar, saklı XP,
  skor, ağaç satın alımları, uzmanlaşma. Coroutine ve canlı nesne referansı saklanmaz.
- **Yeniden kurma** her zaman temiz oturumdur: `GameScene` yeniden yüklenir (oyunun kendi yeniden başlatma yolu), sonra
  round → grid → tarla temizliği → tile → saksı → bitki → level → test statları → ödüller → boss sırasıyla kurulur. Ödüller
  yeniden kurulurken oyunun denetiminden geçer; kurulamayan adım *Kurulum raporu*nda yazar.
- Yakalanan senaryoda yazılmayan üretim noktaları boş kalır; hazır senaryolar "normal bitki zarı" ile doldurur.
- Geçmişe bağlı koşulu olan ödüller (ör. "tarla boşalıyor": son round'un doluluğu) temiz oturumda koşulu tutmayabilir; o zaman
  kurulmaz ve raporlanır.
- R26 ve sonrasında bir round'u yeniden başlatırsan oyunun kendi takvimi o round'un tarla olayını kurabilir (gerçek davranış).

## Hazır senaryolar

Ortak kalıp: "tek vuruşta hasat" için hasar statı değil hedef bitkinin **canı 1** yapılır; "davranış kesin tetiklensin" için
tile'ın gerçek şansı görünür bir test katkısıyla **×20** çarpılır (yalnız o tile'ı olan saksı %100 olur). Çoğunda otomatik
saldırı kapalı, nişan sabittir: *Tek saldırı* ile adım adım ilerlersin. Tekrar için `B · Tarla > Senaryonun bitki kurulumunu
yeniden uygula` ya da *Senaryoyu yeniden kur*.

| Senaryo | Ne yapacağım | Ne görmeliyim / neyi kontrol edeceğim |
|---|---|---|
| 00 · Boş başlangıç | Tarlayı elle kur. | Normal round; test statı ve ödül yok. |
| 01 · Doğrudan vuruş ve kritik | *Tek saldırı*; kritik şansı katkısını 0 / 1 yap. | Yalnız doğrudan vuruş; kritikte hasar × kritik çarpanı. Davranış ya da ödül darbesi yok. |
| 02 · Patlama | *Tek saldırı* (merkez, can 1). | Dört yan komşu patlama hasarı alır; çaprazlar almaz. |
| 03 · Elektrik | *Tek saldırı*. | Çevredeki bitkilere elektrik hasarı. |
| 04 · Kasırga | *Tek saldırı*; zamanı yavaşlat. | Gezerek vuran kasırga; aktif sayı 1 → 0. |
| 05 · Bumerang | *Tek saldırı*; tek adım. | Gidip dönen bumerang. |
| 06 · Hasat Ritmi | 3 × 3 saldırıları tekrarla. | Eşikte altın imleç; sıradaki saldırının yarıçapı ×1,5. Yalnız normal saldırının doğrudan hasatları sayılır. |
| 07 · Zincir Hasat | *Tek saldırı* (merkez). | Nesil 1 ve 2 patlamalar, mor kaynak çizgisi; kayıtta deneme / tetik / "zaten denendi". |
| 08 · Artçı Patlama ve Çifte Akım | Merkeze, sonra (3, 3)'e saldır. | Gecikmeli ikinci patlama ve alan çizgisi; elektrikte ikinci dalga. Yeni zar yok. |
| 09 · Anten / İletken Hat / Kapalı Devre | Bekle. | Anten ve hat kendi aralıklarıyla vurur; bazen ek hat vuruşu. Doğrudan vuruş değildir. |
| 10 · Hilal / Büyüyen Ay / Dolunay | 3 × 3 *Tek saldırı*. | İmleçten çıkan dalga, genişleyen cephe, karşı dalga; kısa saldırı duraklaması (o sırada *Tek saldırı* reddedilir). |
| 11 · Kritik Ark | *Tek saldırı* (kritik %100). | Yakındaki başka bitkiye ark; hasar kritiğin oranı. |
| 12 · Taşan Hasar | *Tek saldırı* (can 1). | "ÖLDÜ (taşan N)" ve en yakına aktarım. |
| 13 · Hasat Yankısı | *Tek saldırı* (nişan sol altta). | Simetrik hücrelere gecikmeli yankı: (x, z) → (10 − x, 10 − z). |
| 14 · Nadir Reaktör | Patlama saksısının şansına bak, nadir bitkiyi hasat et, yeniden bak. | Davranış şansları süreyle artar, sonra döner; birikmez. |
| 15 · Açgözlü Hasat | 3 × 3 saldır; bitkiler çıkınca yeniden. | Hazır dişler; ikinci saldırıda "ödül saldırı çarpanı" > 1. Tek hedefte hak kaybolur. |
| 17 · Aşırı Yükleme | *Tek saldırı* (merkez). | Amber hazırlık, 0,3 sn sonra zayıf tekrar; bekleme süresi içinde yeniden doğmaz. |
| 18 · Diken Kuşatması | Olayı `E` sekmesinden başlat; kökleri vur. | Bölgede üretim durur; iki kök kırılınca ya da süre dolunca açılır. |
| 19 · Aşılı Kabuk | Olayı başlat; A ya da B'yi kır; bir kez de bekle. | Kırılanın bölgesi kurtulur; seçim yoksa A kapanır. |
| 20 · İstilacı Filiz | Olayı başlat; bekle; ana filizi kır. | Yayılan yavaşlatma; kırılınca kalkar. |
| 21 · Gezgin Bereket | Olayı başlat; bölgede hasat et. | Kaynak ×2, XP ×2, üretim ×2 hızlı; gezen bölge. |
| 22 · Rezonans ve stat kaynağı | Saksıları seç; `F`'de stat kaynağını oku. | Tile sayıları, etkin / eksik rezonans kuralları; taban, global, tile, rezonans ayrı. |
| 23 · Bedelli ödül, dışlama, seçim hakkı | Davranışa Adanış'ın reddini oku; "çakışanı kaldırıp yeniden kur"; XP ver; round'u bitir. | Seçim hakkı 4; dışlama aşılmaz; hak yalnız sonraki level'ları etkiler. |
| 24 · Kasırga / bumerang kapasitesi | İzle. | Aktif sayılar sınırda kalır; fazlası kayıtta "reddedildi". |

Ayrıntılı "ne yapacağım / ne görmeliyim / neyi kontrol edeceğim" metinleri panelde her senaryonun altında yazar.
Tarla olayı senaryoları R27'de kurulur (olayların çıkabildiği ilk round R26 bir boss round'udur; boss kuralı karışmasın diye).
Boss'lar için hazır senaryo yoktur: herhangi bir düzende `E · Boss ve olaylar`'dan boss ve round seçilir.

## Olay kaydını okuma

Her satır: oyun zamanı · kare · saldırı kimliği · olay. `S12` o olayın 12 numaralı saldırıya **kesin** bağlı olduğunu söyler
(saldırının kendi vuruşları, aynı anda doğan davranışlar, zincir kökü taşıyan bütün vuruşlar). `~S12` "12 numaralı saldırıdan
sonra oldu, bağ kanıtı yok" demektir: zincir kökü taşımayan gecikmeli vuruşlar (Zincir Hasat alınmamışken kasırga ve bumerang,
artçı, anten / hat darbeleri, tarla olayı bildirimleri). Araç bu bağı tahmin etmez.

Kapasite retleri oyunun sayaçlarından kare sonunda okunur ("Kasırga tetiği reddedildi: kapasite dolu (12/12) · +3"): kaç tetiğin
reddedildiği yazılır, hangi saksıdan geldiği değil. Kayıt en çok 2 000 satır tutar; kapalıyken hiçbir olaya abone değildir.
Vuruş, kritik, zincir zarı ve stat değişikliği satırları yalnız "Ayrıntılı sayısal iz" seviyesinde yazılır; "İşlem özeti"nde
saldırı özeti, hasat, davranış, ödül, buff ve olay satırları kalır.

## Sayısal doğrulama (`H · Sayısal`)

12 Ekim 2026'da eklendi. Amaç: bir mekaniğin **çalıştığını** değil, **hangi sayıyla çalıştığını** göstermek ve gerçek sonucu
bağımsız bir beklentiyle karşılaştırmak. Yeni test sahnesi, ikinci bir stat sistemi ya da yeni oyun mekaniği eklenmedi: hepsi
aynı oturumda, oyunun kendi değerleri okunarak çalışır.

Menü yolları:

| Ne | Yol |
|---|---|
| Paneli aç | **Tools > Mechanics Test Lab > Paneli aç** |
| Oturumu başlat | **Tools > Mechanics Test Lab > Test oturumunu başlat (Play)** ya da panelde `A · Oturum > Test oturumunu başlat` |
| Test sahnesini aç / yeniden oluştur | **Tools > Mechanics Test Lab > Test sahnesini aç** · **… > Test sahnesini (yeniden) oluştur** |
| Önce / sonra karşılaştırması | panel `H · Sayısal > Önce / Sonra` |
| Saldırı izi | `H · Sayısal > Saldırı izi` |
| Ödül makbuzu, aktif geçici etkiler, buff yaşam döngüsü | `H · Sayısal > Makbuz ve buff` |
| Beklenen / gerçekleşen kontroller, kapsam matrisi | `H · Sayısal > Kontroller` |
| Şans istatistiği | `H · Sayısal > Şans` |
| Kayıt seviyesi, Console komutları, dışa aktarım | `H · Sayısal > Rapor` (seviye ayrıca `F · İnceleme > Olay kaydı`) |
| Oyun HUD'unu gizle / göster | `A · Oturum > Görsel test kontrolleri > Gizle: …` |

### Alt sekmeler

- **Önce / Sonra.** "1 · Önce görüntüsünü al" → değişikliği yap → "2 · Sonra görüntüsünü al ve karşılaştır". Görüntü, oyuncunun
  ve (seçildiyse) bir hücrenin saksı / bitki / tile değerlerinin **sayı ve metin kopyasıdır**; oyun değişince görüntü değişmez.
  Tablo: stat ve kapsam · önceki ve sonraki gerçek değer · mutlak fark · oransal fark · değişen kaynak · birim ve sınır notu.
  Şans farkı **yüzde puanı**, çarpan farkı **çarpan**, süre farkı **saniye**dir (parantezde saniyedeki sayı); oran = sonra ÷ önce,
  önce 0 ise "hesaplanamaz". "— ham taban" satırları oyun verisidir, diğerleri çözülmüş gerçek değer. "Doğrudan vuruş
  (sapmasız)" gibi türetilmiş satırlar oyunun hasar çarpımının satırda yazılı bağlamla çağrılmasıdır; **gerçek saldırı sonucu
  değildir** — o, saldırı izindedir. Altındaki "Gerçek katkı kaynakları" bölümü seçili stat'ın kaynaklarını listeler: taban,
  başlangıç seçimi, skill, uzmanlaşma, temel güç kartları, tile, rezonans, boss ödülü, boss kuralı, geçici etki, test katkısı;
  her biri için işlem (toplamsal / çarpımsal), kapsam, stack, koşul ve kalan süre. Liste hesap sırası değildir; panel bunu yazar.
- **Saldırı izi.** "Bir sonraki saldırıyı kaydet" kurulur ve bir sonraki oyuncu saldırısını, ondan doğan davranışları, zinciri,
  ikinci darbeleri ve ödül darbelerini kaydeder. Her vuruş için: tür (direct / davranış / zincir / artçı / ödül darbesi),
  kaynak ve hedef, canın öncesi → sonrası, **hesaplanan darbe hasarı**, **gerçek can azalması**, **taşan hasar**, hasat ve kritik.
  Satıra tıklayınca o vuruşun gerçek hesap yolu açılır (doğrudan vuruşta taban × sapma, çarpanlar, yuvarlamalar, kritik, saksı
  çarpanı; davranışta taban, katsayı, rezonans / uzmanlaşma / ödül çarpanı, zincir nesli; artçıda ilk darbenin oranı). Altında
  hasat başına kaynak / XP / skor hesabı ve kayıt başı → sonu **gerçek bakiye farkı** (kazanılan XP ile saklı XP ayrı).
  Kayıt, bekleyen iş (zincir, ikinci darbe, ödül zamanlayıcısı, kasırga, bumerang, hilal dalgası) bitmeden "tamamlandı" olmaz;
  400 olay ya da 15 sn sınırına ulaşırsa **"kayıt kesildi, sonuç eksik"** yazar. Otomatik saldırı ya da üretim açıksa karışma
  uyarısı verir.
- **Makbuz ve buff.** Üstte şu an aktif geçici / koşullu etkiler ve kalan süreleri; ortada yaşam döngüsü kaydı (eklendi ·
  süresi yenilendi · hak hazırlandı / harcandı / kayboldu · süresi bitti · temizlendi · rezonans şartı bozuldu); altta panelden
  kurulan her ödülün **makbuzu**: istenen stack, başarı ya da gerçek ret nedeni, oyunun kart metnindeki kazanç ve bedel,
  kurulumdan hemen önce / sonra **ölçülen** değişim, oyundaki mekanik kaydı ("koşullu etki, henüz tetiklenmedi"), dışlama
  grubuna ve gelecek level seçim hakkına etkisi. Genel bir ödül geri alma sistemi yoktur: temiz durum için senaryoyu yeniden kur.
- **Kontroller.** Beklenen / gerçekleşen kontrol kataloğu (aşağıdaki kapsam matrisi). Bir kontrol: ön koşul · işlem · beklenen
  · gerçekleşen · **PASS / FAIL / İNCELENMEDİ** · tolerans ve nedeni. Kontroller senaryoyu yeniden kurar, saldırıdan hemen
  önce oyunun rastgele akışını sabit seed'le başlatır (açık test eylemi) ve bitince önceki senaryoyu geri kurar.
- **Şans.** Seçilen davranış için sabit koşullu örnekleme: seed, deneme sayısı, gerçek fırsat sayısı, başarılı zar sayısı,
  gerçekten başlatılan davranış sayısı, kapasite nedeniyle reddedilen, gözlenen oran, gerçek hesabın kullandığı şans ve %95
  güven aralığı. "Zar başarılı" ile "davranış başlatıldı" ayrıdır. Küçük örnek ayrıca işaretlenir; sonuç PASS / FAIL değil,
  "çelişki yok / incele"dir.
- **Rapor.** Kayıt seviyesi (Kapalı · İşlem özeti · Ayrıntılı sayısal iz), "Son işlemi Console'a yaz", oturum raporunun dışa
  aktarımı.

### Örnek 1 — Keskin Bıçak eklemeden önce / sonra hasarı kontrol et

1. `G · Senaryolar` → **01 · Temel doğrudan vuruş ve kritik** → *Kur* (ya da kendi düzenin).
2. `H · Sayısal > Önce / Sonra`: "Hedef hücre"yi açıp merkezdeki saksıyı seç → **1 · "Önce" görüntüsünü al**.
3. `D · Ödüller` → Keskin Bıçak → *Kur*.
4. `H · Sayısal > Önce / Sonra` → **2 · "Sonra" görüntüsünü al ve karşılaştır**.
5. Tabloda görmen gereken: *Doğrudan vuruş · boss ödülü çarpanı* ×1 → ×1,25 (fark +0,25 çarpan, oran ×1,25) ve kaynak
   sütununda "+ boss ödülü: Keskin Bıçak"; *Doğrudan vuruş hasarı (Common hedef)* 10 → 12. (10 × 1,25 = 12,5; oyun en yakın tam
   sayıya, ,5'te çifte yuvarlar: 12. Satırın notunda "tek yuvarlama" yazar.) Davranış hasarı satırları listede **olmamalı**.
6. Aynı şeyin makbuzu: `H · Sayısal > Makbuz ve buff` (son makbuz).
7. Gerçek vuruşla görmek için: `H · Sayısal > Saldırı izi` → **Kaydet ve nişana tek saldırı at**; ilk vuruşun hesabında
   "[boss ödülü] Keskin Bıçak: ×1,25" satırı ve o vuruşun gerçek sapması yazar.
8. Otomatik karşılaştırma: `H · Sayısal > Kontroller` → **D03** → *Çalıştır* (1 ve 2 stack; davranışa sızmadığını da denetler).

### Örnek 2 — Nadir Reaktör eklenmesini ve süresinin bitmesini kontrol et

1. `G · Senaryolar` → **14 · Nadir Reaktör** → *Kur*. `H · Sayısal > Rapor`'da kayıt seviyesi en az "İşlem özeti" olsun.
2. `H · Sayısal > Önce / Sonra`: patlama tile'lı saksıyı (4, 5) hedef seç → **1 · Önce**.
3. `A · Oturum` → *Tek saldırı* (merkezdeki nadir bitki hasat edilir).
4. `H · Sayısal > Makbuz ve buff`: "Nadir Reaktör: AKTİF · kalan … sn · StatManager'daki çarpan sayısı 4"; yaşam döngüsünde
   "Nadir Reaktör EKLENDİ". `Önce / Sonra` → **2 · Sonra**: *Patlama şansı* satırı yüzde puanı farkı ve ×1,25 oranıyla, kaynak
   "+ geçici etki: Nadir Reaktör".
5. Süre bitmeden yeniden: `B · Tarla`'dan merkeze nadir bitkiyi yeniden koy, canını 1 yap, *Tek saldırı*. Yaşam döngüsünde
   "süresi YENİLENDİ · modifier sayısı 4 (çarpan katlanmadı)" — ikinci kez "eklendi" yazmaz, şans değişmez.
6. Süreyi bekle (4 sn): "süresi BİTTİ · kalan modifier 0". `Önce / Sonra` → **2 · Sonra**'yı yeniden al: değişen satır kalmaz.
7. Otomatik karşılaştırma: `Kontroller` → **E04**.

### Örnek 3 — Bir saldırının direct → davranış → artçı izini al

1. `G · Senaryolar` → **08 · Artçı Patlama ve Çifte Akım** → *Kur*.
2. `H · Sayısal > Saldırı izi` → **Kaydet ve nişana tek saldırı at**.
3. Durum satırı "KAYITTA … bekleyen: ikinci darbe" der; ikinci patlama uygulanıp sessizlik geçince iz kapanır.
4. Tabloda sırasıyla: `direct` (HASAT, gerçek can azalması 1, gerisi taşan) → `davranış · Patlama` (dört yan komşu, +0,000 sn) →
   `artçı · Patlama` (+0,200 sn, daha geniş alan). Her satırda hesaplanan darbe ile gerçek can azalması ayrı sütundadır.
5. Bir satıra tıkla: o vuruşun hesap yolu. "Tam metin"i açarsan devam olayları (zar, plan, uygulama) da görünür.
6. **Seçili saldırı zincirini Console'a yaz** ya da *Panoya kopyala*.

### Örnek 4 — Logu dışa aktar ve paylaş

1. `H · Sayısal > Rapor`: kayıt seviyesini seç ("Ayrıntılı sayısal iz" her vuruşun hesap satırını da yazar).
2. Testi yap (karşılaştırma, iz, kontrol, şans örneği — hangisi gerekiyorsa).
3. "Çıktı klasörü"nü seç (varsayılan `Logs/MechanicsTestLab`) → **Oturum raporunu dışa aktar**.
4. *Son raporu göster* klasörü açar: `rapor.txt` (okunabilir), `rapor.json`, `once-sonra.csv`, `saldiri-izi-vuruslar.csv`,
   `saldiri-izi-hasatlar.csv`, `kontroller.csv`, `sans.csv`. Klasörü olduğu gibi gönder.
5. Tek bir sonucu hızlıca paylaşmak için: **Son işlemi Console'a yaz** ya da *Son işlemi kopyala*; karşılaştırma için
   *Önce/sonra farkını kopyala*.

Rapor başlığı senaryoyu, profil ve seed'i, round'u, aktif ödülleri, test katkılarını, başlangıç koşullarını ve kaydın kesilip
kesilmediğini içerir. Rapor gerçek oyuncu kaydının klasörüne ve `Assets`'e yazılmaz (araç reddeder).

### Kayıt seviyeleri ve maliyet

| Seviye | Ne yazılır |
|---|---|
| Kapalı | Hiçbir olaya abone olunmaz, metin üretilmez. Oyun kodundaki gözlem noktaları tek bir `bool` okur ve hiçbir şey kurmaz. |
| İşlem özeti | Saldırı özeti, hasat, davranış, ödül, buff yaşam döngüsü, boss / tarla olayı, komutlar. |
| Ayrıntılı sayısal iz | Buna ek olarak her vuruş, kritik, zincir zarı, stat değişikliği ve doğrudan vuruşun hesap satırı. |

Varsayılan "Ayrıntılı sayısal iz"dir (hazır senaryoların "ne görmeliyim" metinleri bu satırlara bakar); seçim bu makinede
hatırlanır. Kayıt **Console'a kendiliğinden yazmaz**: yalnız "… Console'a yaz" komutları yazar. Tamponlar sınırlıdır: olay kaydı
2 000 satır, saldırı izi 400 olay / 15 sn ve son 20 iz, makbuz son 30, şans örneği son 20. Kayıt oyunun rastgele akışından zar
çekmez; kayıt açık ya da kapalıyken oynanış sonucu aynıdır (aşağıda doğrulandı).

### Oyun HUD'unu gizleme

`A · Oturum > Görsel test kontrolleri`: *Kota ve boss ödülleri listesi*, *Tarla olayı açıklaması*, *Round / XP / kaynak
göstergeleri*. Yalnız **çizim** kapanır (HUD nesnesine iç içe bir Canvas eklenir ve kapatılır): GameObject'ler, HUD script'leri
ve yöneticiler çalışmaya devam eder, dünya VFX'i ve gerçek olaylar etkilenmez. Yeniden kurulumda korunur; "göster" ya da oturum
sonu eski hâline döndürür. Kota kartı Round kutusunun çocuğudur: Round göstergesi gizlenince o da görünmez. Oyun arayüzü
yeniden tasarlanmadı.

### Kapsam matrisi

Kontrol kimlikleri `H · Sayısal > Kontroller`'deki kimliklerdir. "Beklenen" bağımsız aritmetikle üretilir: girdiler asset
değerleri (ödül, plan, boss verisi) ve **işlemden önce ölçülen** gerçek değerlerdir; aynı oyun fonksiyonu iki tarafta birden
çağrılmaz. "Gerçekleşen" oyundan ölçülür: bitkinin canı, saldırı izindeki gerçek ara değerler, hesap bakiyesi, saksının
çözülmüş değeri, iki olay arasındaki gerçek süre.

**Otomatik doğrulanan**

| Alan | Kontroller |
|---|---|
| Doğrudan vuruş | D01 taban × sapma, tek yuvarlama, gerçek can azalması, taşan · D02 kritik (ikinci yuvarlama) · D03 Keskin Bıçak ×1 / ×2 · D04 Canavar Kesimi · D05 Altın Hedef (Rare+ var, Common yok) · D06 Bereketli Öğrenim (bedel + seçim hakkı) · D07 saksı hasar çarpanı yalnız doğrudan vuruşa |
| Stat ödülleri | S01 Hızlı Bilek ve S02 Fırtına Bileği (stat + iki otomatik saldırının ölçülen arası) · S03 Geniş Savuruş (gerçek temas yarıçapı) · S04 Bilgi Filizi (gerçek hasat XP'si) · S05 Ağır Darbe (gerçek kritik vuruş) · S06 Kritik Göz (saldırının kullandığı şans) · S07 Kıvılcım (şansı olmayan saksı şans kazanmaz) · S08 Nadir Tohum · S09 Bereketli Toprak (koşul ret → boş round → kurulum → gerçek üretim aralığı) |
| Davranış hasarı | B01 patlama (dört yan komşu, çapraz yok, iz = bitkilerin gerçek canı) · B02 Yıkım Gücü ×1 / ×2 · B03 Davranışa Adanış (çarpan + seçim hakkı bedeli) · B04 elektrik · B05 kasırga · B06 bumerang |
| Kırılma ödülleri | K01 Artçı Patlama ve K02 Çifte Akım (gecikme, hasar oranı, yeni zar yok) · K03 Hasat Ritmi (hazırlanır, harcanır, birikmez) · K04 Zincir Hasat (nesil hasar çarpanı, nesil sınırı, aynı kök) |
| Etki ödülleri | E01 Kritik Ark · E02 Taşan Hasar · E03 Hasat Yankısı (simetrik hücreler) · E04 Nadir Reaktör (eklenir, yenilenir ama birikmez, süresi biter) · E05 Açgözlü Hasat · E07 Aşırı Yükleme (gecikme, oran, yeni hak doğurmaz) |
| Anten ailesi | A01 Elektrik Anteni ve A02 İletken Hat (aralık + hasar) · A03 Kapalı Devre (zar, ek vuruş, hasar, gecikme) |
| Hilal ailesi | H01 Hilal Darbesi (eşik + hasar) |
| Tarla olayları | F01 Diken Kuşatması (kapalı hücre, üretim durur, kök dayanıklılığı = saldırı sayısı) · F02 İstilacı Filiz (yavaşlatma çarpanı gerçek üretim aralığında, kırılınca geri) · F04 Gezgin Bereket (kaynak ×2, XP ×2, bölge dışında yok, bakiye = kayıt) |
| Boss kuralları | G01 Don Cephesi · G02 Sert Kabuk · G03 Sis — bantta / etkide çarpan, bant dışında değişmez, kalkınca eski değer |
| XP / kaynak / skor | X01 kontrollü tek hasat (kaynak, XP, skor kaydı = gerçek bakiye farkı) · X02 level kazanımı (kazanılan XP ≠ saklı XP, yeni seçim hakkı) |
| Tile'lar (üçüncü paket) | T00 tile verisi (ad = nadirlik alanı, aralıklar) · T01–T10 her tile türü, dört nadirlik: oyunun **yazdığı** değer = zar değeri = saksıya **uygulanan** değer, komşu saksıya sızmaz, seviye 3 = ×1,75, tile kalkınca geri döner; ve **gerçek kullanım**: davranış şansı → gerçek tetik oranı (240 deneme), çift ödül → gerçek oran, hasar → gerçek vuruş, kristal → gerçek bitki zarı (600 doğuş), enerji → gerçek skor, verimli → **ölçülen** doğuş süresi, su → gerçek XP |
| Rezonanslar | R01–R10 on kuralın hepsi: kademe eşikleri (bir eksik tile ile etkin değil), oyunun yazdığı etki = veri, saksıya uygulanan değer = (taban + tile'lar) üzerine en güçlü etki, tile'ı olmayan davranışa sızmaz; gerçek vuruş, gerçek hasadın kaynak / XP / skor çarpanı, davranış rezonansında gerçek patlama hasarı |
| Kartlar | C01 tile kartı (kartta yazan zar tarlaya aynen gelir) · C02 yükseltme kartı (seviye kazancı 1 / 2 / 3, değer çarpanı, seviye 3 sınırı) · C03 beş temel güç kartı × dört nadirlik × iki kopya (toplanan ve çarpılan gruplar) |
| Skill ağacı | SK01 profilin ağacındaki her düğüm, her seviye, oyunun satın alma yoluyla: bedel, modifier listesi (eski seviye kalkar, yenisi girer), stat = ham taban + listeden bağımsız hesap |
| Başlangıç seçimi | L01 her çiftçi ve tırpan ayrı temiz oturumda: statlar, gerçek temas yarıçapı, gerçek vuruşun doğrudan çarpanı, sekiz gerçek hasadın kaynağı ve skoru |

**Yalnız gözlemlenebilen** (çalışır, gözlenen kısmı karşılaştırır; kalanı elle)

| Kontrol | Otomatik bakılan | Elle bakılacak |
|---|---|---|
| H02 Büyüyen Ay | her hilal vuruşu izinli büyüme basamaklarından biri; en az bir adım büyüdü | cephe genişliği |
| H03 Dolunay | dönüş dalgası = taban hasar × oran; vuruş duraklaması gözlendi ve veriyi aşmadı | — (büyüme eşiğine ulaşılmazsa İNCELENMEDİ) |
| H04 Hilal imleç rotası | — | rota bu araçta sayısal doğrulanmıyor: `MoonCursorVerification.RunBatch` ve gözle inceleme |
| F03 Aşılı Kabuk | seçim yapılmazsa A kapanır, B açık kalır; temizlenince kapalı hücre kalmaz | A / B düğümü kırma yolu |
| S06, S07, S08 içinde | stat ve saldırının kullandığı değer | zarın dağılımı (istatistik için `Şans` sekmesi) |
| E07, A03 içinde | gecikme, oran, sayaç | bekleme süresi ve saldırı başına sınır |
| B05, B06 içinde | vuruş = taban × oyunun bildirdiği katsayı | katsayının prefab alanıyla aynı olduğu |
| K04 içinde | nesil hasarı, nesil sınırı, kök | "aynı saksı + davranış aynı kökte bir kez denenir" (izdeki zincir satırlarından) |
| Şans (dört davranış) | fırsat / başarılı zar / başlatılan / reddedilen sayıları, güven aralığı | karar: örnekleme PASS vermez |

**Eksik**

| Madde | Durum |
|---|---|
| M01 Final boss / run sonu sistemi | sistem henüz yok (R50 akışı geçici) |
| M02 Endless modu | sistem henüz yok |
| M04 Hilal – rezonans etkileşim kuralı | sistem henüz yok; kural kullanıcı onayı bekliyor |
| M05 Okunmayan dört stat | `StartingGoldBonus`, `StartingPlanterCount`, `ExtraCardChoice`, `RerollCard` temel sette tanımlı ama hiçbir oyun kodu okumuyor; hiçbir içerik de vermiyor (şu an zararsız) |
| Uzmanlaşma (U01) | bu profilde **kapalı** (`specializationAfterSegment = 0`): seçim ekranı açılmaz, üç uzmanlaşma asset'i kullanılmaz; yalnız çarpanların nötr olduğu doğrulandı |
| Skill'lerin verdiği meta statlar | `GridUnlockSize`, `RoundDuration`, `CardSkip`, `MutationLuck`: stat değeri doğrulandı, oyundaki etkisi (açılan grid, round süresi, atlama hakkı, kart nadirliği) ölçülmedi |
| Elektrik Bilgisi rezonansı | "elektrik öldürmesi XP ×10" değeri doğrulandı; gerçek hasada yansıması ölçülmedi |
| Kritik zarının dağılımı | kontrol yok (yalnız saldırının kullandığı şans) |
| Ödül geri alma | araçta yok; temiz oturumdan yeniden kurulur |


**Kapsamdan çıkarılanlar — 13 Ekim 2026 düzeltmesi:** Rezonans Çöküşü / Rezonans Darbesi kullanıcı kararıyla iptal edilmiştir; eksik sistem, açık TODO veya tamamlanması gereken test değildir. Hilal–rezonans bağlantısı bu iptale dahil değildir; onun somut kuralı ayrıca karar bekler. Mevcut kontrol kataloğunun M03 kaydı ve 12 Ekim ham çıktısı eski “sistem henüz yok” sınıflandırmasını taşır. Ham test kanıtı değiştirilmedi; 81 kontrol / 5 sistem henüz yok sayıları o koşunun tarihsel sonucudur. Katalog sınıflandırması ayrı araç güncellemesinde düzeltilmelidir; bu belge düzeltmesi yeni test koşusu veya kod değişikliği değildir.

### Stat ve buff kaynakları: bulgular (üçüncü paket, 12 Ekim)

Soru: "tile'ın, rezonansın, kartın, skill'in yazdığı değer gerçekten uygulanıyor mu, doğru yere mi, ve oyun onu kullanıyor
mu?" Her kaynak için üç şey ölçüldü: oyunun oyuncuya **yazdığı** değer (tooltip / kart metni) = veri; değer **doğru hedefe**
uygulanıyor ve başka hedefe sızmıyor; oyun değeri **gerçekten kullanıyor** (gerçek vuruş, gerçek hasat, ölçülen süre, gerçek
tetik oranı). Sonuç: 27 kontrol — **19 PASS · 6 UYARI · 2 FAIL**. Satır satır döküm:
[MechanicsTestLab/MechanicsNumericChecks.txt](MechanicsTestLab/MechanicsNumericChecks.txt).

**UYARI** yeni bir sonuç türüdür: hesap oyunun kuralına uygun, ama oyuncuya yazılan değerin bir kısmı kullanılamıyor ya da
yazım yanıltıcı. Hata değil, tasarım kararı gerektiren bulgudur; FAIL sayılmaz.

Doğru çıkanlar (özet): 39 tile asset'inin yazdığı değer saksıya aynen uygulanıyor, komşuya sızmıyor, tile kalkınca geri
dönüyor; seviye başına +%25 tutuyor. Gerçek kullanım: patlama %29 / %44 / %60 / %90 yazan tile'larda gerçek tetik oranı
%28 / %48 / %61 / %84 (240'ar deneme, istatistik tolerans içinde); çift ödül %25 / %40 / %60 / %90 → %23 / %35 / %62 / %90;
verimli tile'da ölçülen doğuş süresi 8 sn → 6,83 / 6,03 / 4,97 / 3,63 sn (beklenen 6,8 / 6 / 4,96 / 3,6). On rezonans kuralının
kademeleri, yazan etkileri ve gerçek vuruş / hasat çarpanları tutuyor. Skill ağacında 219 satın alımın hepsinde bedel ve stat
doğru. Tüccar kaynağı ×1,25 (sekiz hasatta 16 → 20) ve skoru ×0,9 (8 → 7), Dar Kesim vuruşu ×1,4 ve yarıçapı ×0,75 veriyor.

| # | Sonuç | Bulgu | Ayrıntı |
|---|---|---|---|
| 1 | **FAIL** (veri) | `Tornado-Rare` asset'inin nadirlik alanı **Common** | `Assets/ScriptableObjects/GridModifiers/Tornado/Tornado-Rare.asset` içinde `rarity: 0`. Sonuç: oyunda Rare nadirlikte kasırga tile'ı yok; iki "Common" kasırga tile'ı var ve biri %30–59 şans veriyor (Common'ın %10–29'u yerine). Kart havuzunu ve nadirlik zarını etkiler. Düzeltme tek satır (`rarity: 1`); kaynak asset olduğu için dokunulmadı. |
| 2 | UYARI | Tile metni uygulanabilen sınırı aşan değeri yazıyor | Şans tile'ları seviye aldıkça metin %100'ü geçer ama şans %100'de kalır; fazlası hiçbir şeye dönüşmez. Tek tile, en yüksek zar: Patlama ve Çift Ödül — Epic Sv 3 (%105), Legendary Sv 1 / 2 / 3 (%112,5 / %135 / %157,5); Elektrik ve Bumerang — Legendary Sv 3 (%105); Kasırga — Epic Sv 1 / 2 / 3 (%105 / %126 / %147), Legendary Sv 1 / 2 / 3 (%125 / %150 / %175); Kristal — Legendary Sv 2 / 3 (105 / 122,5 puan; sınır 95). Aynı saksının altında birden çok şans tile'ı da toplanıp aynı sınıra takılır. |
| 3 | UYARI | "Nadirlik bonusu +N% puan" Rare+ olasılığına eklenen puan değil | Bonus bitki ağırlığını çarpar. Legendary Kristal "+70% puan" yazar; 1 × 1 saksıda Rare+ bitki olasılığı %15 → %23,7 olur (+8,7 yüzde puanı). 600 doğuşluk gerçek örnek: %23. |
| 4 | UYARI | Değerli Hasat "Rare+ Harvest Score ×3", Şöhret ile çarpılmıyor | İki Enerji tile'ı Şöhret'i de (×1,5) açar. Skor rezonanslarında en güçlüsü geçerli: Rare+ toplam ×3 alır (×4,5 değil), Common ×1,5; Common'a göre gerçek fark ×2. Kod bunu bilerek yapıyor ("stat başına en güçlü etki"); metin bunu söylemiyor. |
| 5 | gözlem | Temel güç kartları çok küçük | Bu profilde Common / Rare / Epic / Legendary = %0,02 / %0,04 / %0,07 / %0,1. Doğru uygulanıyor (hasat hasarı 10 → 10,002), ama vuruş tam sayıya yuvarlandığı için tek kartın etkisi vuruşta görünmez. Profil verisi; P7 kararıyla uyumlu olabilir. |
| 6 | gözlem | Kasırga şansı çok yüksek, çoğu tetik kapasiteye takılıyor | Art arda hasatta (kare başına bir) 240 fırsatta 136–240 zar tutuyor ama yalnız 24 kasırga başlıyor; kalanı aktif sınıra (12) takılıyor. Örnekleme hızı normal oyundan yüksek; oran oyunda daha düşük olur. |
| 7 | gözlem | Dört stat hiçbir yerde okunmuyor | `StartingGoldBonus`, `StartingPlanterCount`, `ExtraCardChoice`, `RerollCard`. Şu an hiçbir içerik vermiyor; ileride bir skill'e bağlanırsa etkisiz olur. |
| 8 | gözlem | Uzmanlaşma bu profilde kapalı | `Run50_GecOyunV1`'de `specializationAfterSegment = 0`: üç uzmanlaşma seçeneği hiç sunulmuyor. |

İlk koşuda bir kontrol (R07) benim beklenti hatam yüzünden FAIL verdi: iki Enerji tile'ının Şöhret'i de açtığını hesaba
katmamıştım. Beklenti oyunun yazılı kuralına ("en güçlü etki") göre düzeltildi ve çarpılmama durumu 4 numaralı UYARI olarak
ayrıca raporlandı.

## Yeni ve değişen dosyalar

Yeni — test aracı (yalnız Editor derlemesi):

| Dosya | Sorumluluk |
|---|---|
| `Assets/Editor/MechanicsTestLab/TestSession.cs` | Oturumun açılması, profil kopyası, kayıt koruması, GameScene'in yüklenmesi, temiz yeniden kurma, çıkışta geri yükleme. |
| `TestTime.cs` | Duraklat / hız / tek adım (oyunun kendi saati; kare sonu kancası). |
| `TestField.cs` · `TestStats.cs` · `TestRewards.cs` · `TestEvents.cs` · `TestAttack.cs` | Test komutları: gerçek sistemlere müdahale buradadır. |
| `TestInspector.cs` | Gerçek durumun ve stat kaynaklarının okunması. |
| `TestLog.cs` | Sınırlı olay kaydı (gözlem olayları). |
| `Scenario.cs` · `ScenarioRunner.cs` · `ScenarioPresets.cs` | Senaryo verisi ve dosyaları; kurma / yakalama; hazır senaryolar. |
| `LabCatalog.cs` | Oyunun gerçek içerik kataloglarının okunması (tile, saksı, bitki, profil, ödül, boss). |
| `LabWindow.cs` + `LabWindow.<Sekme>.cs` | Panel: yalnız girdi ve gösterim. |
| `TestSceneBuilder.cs` | Başlatıcı sahnenin kurulması ve menü girişleri. |
| `StatSources.cs` | Bir değere uygulanmış gerçek katkıların kaynaklarına ayrılması (taban, seçim, skill, tile, rezonans, ödül, boss kuralı, geçici etki, test). |
| `NumericSnapshot.cs` | Önce / sonra görüntüsü (sayı ve metin kopyası), fark tablosu, birim kuralları, CSV. |
| `TestTrace.cs` | "Bir sonraki saldırıyı kaydet": tek saldırının ve devam olaylarının sayısal izi; tamamlanma ve kesilme kuralları. |
| `BuffWatch.cs` | Geçici / koşullu etkilerin yaşam döngüsü (yalnız gözlem; kare sonunda oyunun durumunu okur). |
| `RewardReceipt.cs` | Ödül kurulum makbuzu (kurulumdan önce / sonra ölçülen gerçek değişim). |
| `NumericChecks.cs` · `NumericChecks.Catalog.cs` · `NumericChecks.Sources.cs` | Kare kare yürütücü, bağımsız aritmetik (`Arith`), kontrol modeli ve 81 kontrollük katalog (ödüller, davranışlar, olaylar; ve stat kaynakları: tile, rezonans, kart, skill, başlangıç seçimi). |
| `ChanceSampler.cs` | Şans mekaniklerinin seed'li, gerçek tetik yolundan örneklenmesi. |
| `LabReport.cs` | Rapor metinleri, Console / pano komutları, TXT + JSON + CSV dışa aktarımı; `Comparison` (iki görüntü). |
| `TestHud.cs` | Oyun HUD'unun yalnız çiziminin gizlenmesi / geri getirilmesi. |
| `LabWindow.Numeric.cs` | `H · Sayısal` sekmesi (yalnız girdi ve gösterim). |
| `Assets/Scenes/MechanicsTestScene.unity` | Başlatıcı sahne (tek boş nesne). |
| `Tools/Verification/Editor/MechanicsTestLabVerification.cs` | Aracın otomatik doğrulaması. |
| `Tools/Verification/Editor/MechanicsNumericVerification.cs` | Sayısal doğrulama katmanının otomatik doğrulaması ve kontrol kataloğunun batch koşusu. |

İkinci pakette değişen mevcut test dosyaları: `MechanicsTestLabVerification.cs` (oyuncu derlemesinde aranan test adlarına üç
gözlem adı eklendi; panel duman testi sekiz sekme) ve `MoonCursorVerification.cs` (tek satır: eskimiş yansıma çağrısına eksik
varsayılan argüman; beklentiler aynı — aşağıda "Hilal takımları").

Değişen — oyun kodu (hepsi `#if UNITY_EDITOR`; oyuncu derlemesinde yok):

| Dosya | Eklenen dar giriş ve nedeni |
|---|---|
| `PlayerController.cs` | Otomatik saldırıyı kapatma, sabit nişan, `EditorAttackNow` (tek gerçek saldırı; bekleme ve duraklama atlanmaz), bekleme okuma / doldurma. Saldırı aralığı ve "saldır + halka" iki küçük metoda ayrıldı (davranış aynı). |
| `PlacementManager.cs` | `EditorPlace`: aynı doğrulama ve aynı bağlama ile fare / bedel olmadan yerleştirme. Bağlama adımı `CommitPlacement`'a ayrıldı (davranış aynı). |
| `PlanterBrain.cs` | Salt okunur görünüm (veri, üretim noktaları, rezonans modifier'ları, tile sayıları, yerleşim kaydı) ve iadesiz `EditorRemove`. |
| `PlantSpawner.cs` | `EditorRespawn` / `EditorClearPlant`, üretimi duraklatma, gerçek üretim aralığını okuma. Doğuş `Spawn(bitki)` metoduna ayrıldı (davranış aynı). |
| `PlantHealth.cs` | `EditorSetHealth` (1 … azami). |
| `RoundManager.cs` | Round sayacını dondurma; `EditorEndRound` (round'u oyunun normal round sonu akışıyla bitirir). |
| `BossRewardManager.cs` | `EditorAllowOfferAgain` (aynı boss round'unda teklifin yeniden açılabilmesi). |
| `SegmentEventDirector.cs` | `EditorRestartBossRhythm` (boss'ları temizleyip ritmi aynı seed'le baştan kurar), `EditorIsEligible`. |
| `ProgressionManager.cs` | `EditorSetLevel` (kart hakkı doğurmadan level atama). |
| `FieldEventDirector.cs` | `EditorPreview` (olayı başlatmadan bölgesini aynı seçim koduyla hesaplar). |
| `Assets/Editor/LateGameTestWindow.cs` | Ödül kataloğu okuması lab ile ortak (`LabCatalog`). Pencere tam oyunda çekim paneli olarak duruyor. |

Sayısal iz için eklenen dar **gözlem noktaları** (12 Ekim, ikinci paket; hepsi `#if UNITY_EDITOR`). Hesap hiçbir yerde
taşınmadı ya da değiştirilmedi: hesabı yapan kod, kullandığı gerçek ara değerleri olduğu gibi bildirir. Dinleyici yokken tek
bir `bool` okunur ve hiçbir şey kurulmaz; dinleyicinin hatası hasarı, ödülü ya da skoru yarıda bırakmaz (`ObserverEvents`);
hiçbiri rastgele sayı tüketmez.

| Dosya | Bildirilen gerçek değerler |
|---|---|
| `Assets/Scripts/Managers/EditorNumericTrace.cs` (yeni; dosyanın tamamı Editor) | Dört gözlem olayı ve taşıdıkları değerler. |
| `PlayerController.cs` (`AttackInRadius`) | Doğrudan vuruş: taban, sapma, Hasat Ritmi ve ödül saldırı çarpanı, yuvarlanan hasar, kritik şansı / çarpanı, kritikten sonraki ve uygulanan hasar. |
| `PlanterBrain.cs` (`Explode`) · `ElectricBurst.cs` · `Tornado.cs` · `BoomerangScythe.cs` | Davranış: taban, davranışın kendi katsayısı, zincir nesli çarpanı, vuruş başına hesaplanmış hasar, bağlam. |
| `PlantResource.cs` (`GiveReward`) | Hasat ödülü: kaynak çarpanı, yuvarlanan, çift ödül, ölçekten sonraki, bereket, bankaya eklenen; XP çarpanları, yuvarlanan ve eklenen XP. |
| `HarvestScoreManager.cs` (`AddScore`) | Skor: oyuncu ve saksı çarpanı, ödül, başlangıç ölçeğinden sonra eklenen. |

Dış kaynak, paket ya da üçüncü taraf içerik kullanılmadı.

## Doğrulama

`Tools/run-isolated-verification.ps1 -Method MechanicsTestLabVerification.RunBatch -Full` — izole proje kopyasında; açık editöre
dokunmaz. Sonuç: [MechanicsTestLab/MechanicsTestLabVerification.txt](MechanicsTestLab/MechanicsTestLabVerification.txt).

12 Ekim 2026: **PASS, 183 kontrol.** Oturum, kullanıcının yaptığı gibi `MechanicsTestScene` açılıp Play'e girilerek başlatıldı;
müdahaleler panelin kullandığı aynı komutlardan geçti; zaman sabit kare adımıyla (1 / 30 sn) ilerletildi.

- **Sahne ve güvenlik:** oturum kendiliğinden açılıyor ve gerçek `GameScene`'i yüklüyor; run profilin çalışma zamanı kopyasıyla
  çalışıyor, ödül havuzu / denge / boss havuzu / olay planı gerçek asset'ler; seçim dosyası aynı; kayıt yazma isteği reddediliyor.
- **Tarla = oyun durumu:** tile'ın türü, seviyesi ve değeri; saksı–hücre sahipliği, görselin hücrelerin ortasında olması, 90°
  dönüş; çakışma / kilitli hücre / tarla dışı / taşma retleri; iadesiz kaldırma; bitkinin türü, sahibi, azami canı (can
  eğrisinden) ve can sınırı; rezonansın oyunun değerlendirmesinden gelmesi.
- **Stat:** test katkısı sonuca yansıyor, sızmıyor, sıfırlama yalnız test katkısını kaldırıyor; inceleme sonucu her stat için
  oyunun çözülmüş değeriyle aynı; taban ve tavan uç değerlerde korunuyor; XP önizlemesi gerçek akışla aynı.
- **Ödül:** katalog havuzdan (32 ödül); stack, dışlama, ön koşul ve geçersiz konum retleri; ön koşul zinciri; bedelli ödülün
  kazancı ve bedeli; normal yol: oyunun kart ekranından 8 kart, oyunun teklifinden 3 aday ve seçim.
- **Saldırı ve zaman:** bekleme dolmadan ret; tek saldırı → hasat → gerçek patlama → dört komşuya incelemedeki hasar; duraklatma,
  50 ms'lik tek adım, yarı hız; Artçı Patlama duraklatılmışken bekliyor ve 6 × 33,3 ms adımla tam 0,2 sn'sinde uygulanıyor;
  kasırga duraklatılınca duruyor; Diken Kuşatması'nın fazları oyun zamanıyla ilerliyor, kökler yalnız gerçek saldırıyla
  kırılıyor, süre dolması ayrı görüldü; dört olay ve üç boss ayrı ayrı çalıştı.
- **Senaryo:** kaydet → temizle → yükle aynı düzeni kurdu; üç ardışık yeniden kurmada stat, ödül, etki ve dinleyici sayıları
  aynı, eski zincir / artçı / davranış / olay taşınmadı; 25 hazır senaryonun hepsi açıldı ve temel koşulunu kurdu.
- **Build:** oyuncu betikleri `UNITY_EDITOR` olmadan derlendi (29 derleme) ve çıktıda 20 test girişinden hiçbiri yok
  (ikinci paketin gözlem noktaları dahil: `EditorNumericTrace`, `DirectHitObserved`, `HarvestRewardObserved`).
- **Çıkış:** zaman, bayraklar, profil geçersiz kılması geri alındı; 445 asset / sahne dosyası ve Build Settings aynı; kayıt
  dosyası aynı.

Panelin *Test oturumunu başlat* yolu ayrıca denendi (`MechanicsTestLabVerification.RunStartPlayBatch`, **PASS, 9 kontrol**,
[MechanicsTestLab/MechanicsTestLabStartPlay.txt](MechanicsTestLab/MechanicsTestLabStartPlay.txt)): açık sahne `GameScene` iken
oturum seçilen senaryoyla açıldı; Play'den çıkınca açık sahne aynen geri geldi ve başlangıç sahnesi ayarı bırakılmadı.

### Sayısal doğrulama katmanı (12 Ekim, ikinci paket)

`Tools/run-isolated-verification.ps1 -Method MechanicsNumericVerification.RunBatch -Full` — izole kopyada, sabit kare adımıyla.
Sonuç: [MechanicsTestLab/MechanicsNumericVerification.txt](MechanicsTestLab/MechanicsNumericVerification.txt); kontrollerin
satır satır beklenen / gerçekleşen dökümü ve kapsam matrisi:
[MechanicsTestLab/MechanicsNumericChecks.txt](MechanicsTestLab/MechanicsNumericChecks.txt). Tek tek kontrol koşmak için:
`$env:MTL_CHECKS = "A03,S09"` ve `-Method MechanicsNumericVerification.RunChecksBatch`.

12 Ekim 2026, son koşu: **113 araç kontrolünün hepsi geçti**; kontrol kataloğu (81 kontrol) **67 PASS · 6 UYARI · 2 FAIL ·
0 İNCELENMEDİ · 1 manuel inceleme gerekli (H04 hilal imleç rotası) · 5 sistem henüz yok**. İki FAIL'in kök nedeni tektir:
`Tornado-Rare` tile verisi (aşağıda "bulgular"). Katalogda FAIL olduğu için batch girişi çıkış kodu 1 verir; bu bilerek
böyledir ve veri düzeltilince geçer. İki ayrı şey ölçüldü:

**Araç doğrulaması (113 kontrol)** — aracın gösterdiği sayı gerçek mi?

- **Görüntü canlı referans değil:** "önce" alındıktan sonra hasat hasarı ×1,5, patlama şansı ×2 yapıldı ve Keskin Bıçak
  kuruldu; "önce" görüntüsünün değerleri, kaynak listesi ve satır sayısı değişmedi; "sonra" ayrı kopya. Şans farkı yüzde
  puanı, çarpan farkı çarpan olarak yazıldı; önce 0 olan şansın oranı "hesaplanamaz".
- **Katkı doğru kapsamda:** hasat hasarı katkısı patlama şansı kaynaklarında yok; tile yalnız üstündeki saksının kaynağı;
  tile'sız saksının şansı çarpanla 0 kaldı; Keskin Bıçak davranış hasarı kaynaklarında yok.
- **Makbuz:** başarı (stack 0 → 1, ölçülen çarpan değişimi), ret (gerçek neden, hiçbir değer değişmedi), koşullu etki
  ("henüz tetiklenmedi"), dışlama grubu (Hasat Ritmi → Zincir Hasat), seçim hakkı (3 → 4) ve çalışma zamanı etkisi kaydı.
- **Saldırı izi = gerçek durum:** izden bağımsız bir gözlemle (oyunun hasar olayı + bitkilerin canının ayrıca okunması) 17
  vuruşun hepsinde hesaplanan darbe, önceki can, gerçek can azalması ve taşan hasar aynı; 12 sağ kalan bitkinin şimdiki canı =
  önceki can − izdeki toplam; hasat sayısı, kaynak, XP ve skor toplamları gerçek bakiye farkıyla aynı. İz direct → davranış →
  artçı türlerini ayırdı ve her birinin kendi hesap yolunu yazdı.
- **Tamamlanma ve kesilme:** kasırga sürerken iz açık kaldı ("bekleyen: kasırga"), bitince tamamlandı; durdurulan kayıt
  "kesildi, sonuç eksik" oldu.
- **Kayıt seviyeleri:** kapalıyken kayda hiçbir şey yazılmadı ve gözlem noktalarında dinleyici yok; işlem özetinde hesap satırı
  yok; ayrıntılı izde var. Tamponlar sınırlı.
- **Buff yaşam döngüsü:** Nadir Reaktör EKLENDİ (bir kez) → süresi YENİLENDİ, çarpan katlanmadı → süresi BİTTİ, geride çarpan
  yok; Hasat Ritmi KURULDU → hak HAZIRLANDI → HARCANDI; rezonans şartı bozulunca kalktı; test katkısı sıfırlanınca stat döndü,
  boss ödülü yerinde kaldı.
- **HUD:** gizliyken GameObject ve script etkin, yöneticiler çalışıyor; yeniden kurulumda korunuyor; "göster" eklenen
  Canvas'ları kaldırıyor.
- **Şans:** koşul yokken örnekleme reddedildi; patlama (Rare tile, gerçek şans %44): 400 deneme = 400 gerçek fırsat, 186
  başarılı zar = 186 başlatılan, gözlenen %46,5, %95 aralık %41,7–51,4 (çelişki yok); aynı seed aynı sonucu verdi (186 = 186);
  kasırga (Legendary tile, şans %100): 300 fırsat, 300 başarılı zar, **36 başlatılan, 264 kapasite reddi** — "zar başarılı" ile
  "davranış başlatıldı" ayrı sayıldı.
- **Kayıt açık / kapalı aynı oynanış sonucu:** seed'li yoğun senaryo (dört davranış, zincir, artçı, ikinci dalga, ark, taşma,
  aşırı yükleme, reaktör; %50 kritik; çift ödül tile'ları) üç kez oynandı: kayıt kapalı, kayıt kapalı (taban), ayrıntılı iz +
  saldırı izi + görüntü açık. Üçünde de XP, level, bekleyen seçim, kaynaklar, skor, davranış sayaçları, bütün bitki canları,
  ödül vuruş sayıları ve **oyunun rastgele akışının son durumu** birebir aynı: kayıt ek zar atmıyor ve sonucu değiştirmiyor.
- **Dışa aktarım:** gerçek kayıt klasörüne ve `Assets`'e yazım reddedildi; yedi dosya yazıldı; başlık istenen alanları taşıyor;
  JSON okunabiliyor; kayıt dosyası damgası aynı.
- **Sızıntı:** açık iz + ayrıntılı kayıt + alınmış görüntü varken üç yeniden kurulum: açık izler "kesildi" diye kapandı, görüntü
  ve makbuz taşınmadı, önceki ödül / buff kalmadı, tek saldırı tek hesap satırı yazdı (abonelik çoğalmadı), kayıt ve iz
  kapatılınca dinleyici kalmadı.
- **Çıkış:** seçili run profili (`Run50_GecOyunV1`) aynı, gerçek kayıt dosyası aynı, 445 asset / sahne dosyası ve Build
  Settings aynı; oyuncu derlemesinde gözlem noktası yok.

**Oyun kontrolleri (katalog)** — sayılar kurala uyuyor mu? Sonuçlar yukarıdaki kapsam matrisindeki gibidir; 32 boss ödülünün
hepsinin en az bir otomatik kontrolü var ve hepsi geçti. **Oyun kodunda (hesaplarda) hata bulunmadı**; bulunan tek hata bir
veri hatasıdır (`Tornado-Rare`), ayrıca yazan değer ile oyuncunun aldığı arasında üç tasarım uyarısı var (yukarıda). Geliştirme sırasında ilk
koşularda üç kontrol PASS vermedi; üçü de kontrolün kendi kurulum / gözlem hatasıydı ve **beklenti gevşetilmeden** düzeltildi:

| Kontrol | İlk sonuç | Neden | Düzeltme |
|---|---|---|---|
| S09 Bereketli Toprak | İNCELENMEDİ | Kontrol, boş round'dan sonra "Round'a git" ile yeni round başlatıp ödülü orada kuruyordu. O test komutu round sonu olayını yeniden çalıştırır ve doluluk kaydını 1'e döndürür; koşul bozulur. | Ödül, oyunun teklifi gibi round sonunda kurulur. (Aracın bilinen davranışı olarak aşağıda yazılı.) |
| E07 Aşırı Yükleme | İNCELENMEDİ | Ek proc bir "ödül darbesi" değildir: saksının patlamasını zincir dışı bağlamla yeniden çalıştırır (tür Patlama, kaynak Aşırı Yükleme). Kontrol yanlış türü arıyordu. | Gözlem süzgeci düzeltildi; oran, gecikme ve "yeni hak doğurmaz" beklentileri aynı. |
| A03 Kapalı Devre | FAIL ("hat vuruşu yok") | Kontrol bütün bitkileri 1 cana çekiyordu; anten hattan sık çalıştığı için bitkileri hattan önce hasat ediyordu. | Yalnız bir hat hücresinin bitkisi, hat vuruşundan hemen önce 1 cana çekilir; zamanlar oyunun gerçek vuruşlarından okunur. |

Bir gözlem (hata değil, mevcut kural): doğrudan vuruş en yakın tam sayıya, **,5'te çifte** yuvarlanır. Taban hasar 10 iken
Keskin Bıçak'ın sapmasız sonucu 12,5 → 12'dir (+%25 değil +%20 görünür). Gerçek vuruşta sapma olduğu için tam ,5'e nadiren
denk gelir; panel bunu "tek yuvarlama" notuyla gösterir.

**Panelin görünüşü.** Bu kez gerçekten bakıldı: batch koşusu panelin piksellerini okuyup PNG yazdı (canlı oturum, gerçek
içerik; 1180 ve 760 piksel genişlik). Görüntüler: [MechanicsTestLab/panel/](MechanicsTestLab/panel/). İncelemede bulunan ve
düzeltilen kusurlar: dar sütunda alt satıra taşıp ilk veri satırına binen tablo başlıkları; durum rozetlerinde zayıf yazı
kontrastı; kırpılan "Deneme sayısı" etiketi; boşken büyük boş kaydırma alanları; pencereyi yatay kaydırmaya zorlayan geniş
tablolar (sütunlar artık pencere genişliğine ölçekleniyor); dar pencerede kırpılan sekme adları (iki satıra bölünüyor);
İngilizce hasar türü adları. Sınırı: bunlar batch'te alınmış durağan görüntülerdir — tıklama, kaydırma, odak ve senin
editöründeki tema / yazı ölçeğiyle görünüş **denenmedi**; kullanım rahatlığı insan testine bırakıldı.

### Regresyon

Son kodla, izole kopyada, teker teker ([MechanicsTestLab/Regresyon-Sayisal-20261012.txt](MechanicsTestLab/Regresyon-Sayisal-20261012.txt);
ilk paketin kaydı: [MechanicsTestLab/Regresyon-20261012.txt](MechanicsTestLab/Regresyon-20261012.txt)):
`MechanicsNumericVerification` 113 araç kontrolü + katalog (üçüncü paketten sonra yeniden koşuldu) · `MechanicsTestLabVerification` 183 (yeniden koşuldu) · `RunStartPlayBatch` 9 · Geç Oyun V1 97
(`RunLegacyCrescentBatch`) · Hilal imleç rotası 21 (`MoonCursorVerification`) · Zincir V1 39 · Takvim V1 162 · Alan Geri Bildirimi 23 · Bedelli Ödüller V1 94 · XP V1 39 · Sayısal Güvenlik 33 · Kırılma Erişimi V1 35 · Hasat Davranışları 114 · Basit Rezonans 294 · Mekanikler 88 · Elektrik 40 · Aşırı Yükleme görseli 77 · Kritik Patlama görseli 25 · Nadir Reaktör görseli 14 · Açgözlü Hasat görseli 27 · Hasat Yankısı görseli 15 · Kritik Ark görseli 11 · Taşan Hasar görseli 17 · Anten görseli 45 · Gezgin Bereket görseli 42 · Demo 65 · Çekim Araçları 16 · Boss Buff Penceresi 13 — hepsi PASS.

Bu paket oyun kodunda yalnız Editor gözlem noktaları ekledi (doğrudan vuruş, dört davranış, hasat ödülü, skor); hesap
satırları taşınmadı. Bu yüzden hasar, davranış, zincir, ikinci darbe, ödül ve skor yollarını kullanan takımların hepsi koşuldu.

**Hilal takımları (gizlenmedi, gevşetilmedi):**

- `GecOyunV1Verification.RunBatch` **düşer** — hilalin eski "saldırılan hücreden dört hücre" beklentisinde. Profilde 10 Ekim'den
  beri hilal imleç rotasıyla çalışıyor; bu giriş o tarihten beri bu satırda düşüyor ve bu koşuda da aynı satırda düştü. Bu
  paketle ilişkisi yok: paket hilal koduna dokunmadı. Beklenti değiştirilmedi.
- Takımın genel 97 kontrolü için geçerli giriş `GecOyunV1Verification.RunLegacyCrescentBatch` (eski hilal kuralını eski
  profil ayarıyla koşar): **PASS 97**.
- Güncel hilal kuralının girişi `MoonCursorVerification.RunBatch`. İlk koşuda testin **kendi yansıma çağrısı** yüzünden
  düştü (`TargetParameterCountException`): `DirectAttackReport.Record` commit'li kodda (f499880) isteğe bağlı altıncı
  parametre (`Vector3? point = null`) taşıyor, test ise beş argüman geçiyordu (son başarılı koşusu 10 Ekim). Oyun kodunda ve bu pakette bir hata değildir.
  Teste yalnız eksik varsayılan argüman (`null`) eklendi, hiçbir beklentisi değiştirilmedi: **PASS 21**
  ([MechanicsTestLab/MoonCursorVerification-20261012.txt](MechanicsTestLab/MoonCursorVerification-20261012.txt)).
- `MoonVerification` ve `MoonSmallFieldVerification` 9 Ekim "tam satır" davranışının tarihsel testleridir
  (`Docs/CrescentFamily/CursorRoute.md`: "güncel fare kuralı için çalıştırılmaz"). `MoonVerification` bu koşuda denendi ve
  beklendiği gibi eski kuralda düştü; geçerli bir regresyon girişi değildir.
- Bu aracın kendi hilal kontrolleri (H01–H03) hilalin **hasarını ve eşiğini** güncel profilde doğrular; rotayı doğrulamaz (H04).

Bu doğrulamanın **kapsamadıkları**:

- İnsan Play testi ve görsel / işitsel kabul yapılmadı. Hiçbir efektin görünüşü bu testle onaylanmış değildir.
- Panel açık editörde, elle kullanılmadı (açık editörde Play işletilmedi): görünüş batch görüntülerinden incelendi, kullanım
  rahatlığı incelenmedi.
- Batch modda kayıt zaten diske yazmaz; "kayıt yazılmıyor" maddesi yazma isteğinin reddi ve dosya damgasıyla doğrulandı.
  Etkileşimli editörde de aynı koruma (`EditorProtectTestSession`) çalışır, ama orada ayrıca denenmedi.
- Hazır senaryoların "temel koşulu kurduğu" denetlendi; her senaryonun bütün beklenen sonucu tek tek otomatik denetlenmedi.
  Sayısal sonuçlar için kontrol kataloğu kullanılır (kendi küçük düzenlerini kurar; hazır senaryoları değil).
- Kontrol kataloğu sabit kare adımıyla (1 / 30 sn) koşuldu. Etkileşimli editörde kare süresi değişkendir; süre toleransları
  kare süresine göre hesaplanır ama orada ayrıca denenmedi.
- Kayıt açık / kapalı eşdeğerliği tek bir yoğun senaryoda gösterildi (üretim duraklatılmış, elle saldırı). Üretim açıkken ve
  uzun bir run'da ayrıca ölçülmedi; denge koşusu yapılmadı.

## Bilinen sınırlamalar

- Skill tree satın alımları panelden yönetilmez (oyunun kendi mağaza ekranı round sonunda kullanılabilir); senaryoya yazılmaz.
- Ödül geri alma ve canlı grid küçültme yoktur: ikisi de temiz oturumda yeniden kurmayla yapılır.
- Global bir modifier'ın ağaçtan mı, temel güç kartından mı, geçici bir ödül etkisinden mi geldiği oyunda tutulmuyor; dökümde
  "kaynağı kayıtlı değil" yazar. Test katkısı, boss ödülü ve başlangıç seçimi ayrılır.
- Stat tablosundaki "test'siz" değer saksıda rezonanstan önceki değerdir; rezonans dahil sonuç yanındaki sütundadır.
- Tarla olayı bölgesi 600 seed içinde aranır; istenen hücreyi içeren bölge çıkmazsa olay başlamaz (kural değiştirilmez).
- Normal teklif yolunda kota ve boss hasadı varsayılan olarak değerlendirilmez; gerçek değerlendirme kutuyla açılır.
- Level > 1 senaryolarında level doğrudan atanır (kart hakkı yok); round > 1 senaryoları round atlama girişini kullanır.
- Oyun kamerası için güvenli bir yakınlaştırma kontrolü oyunda yok; araç yalnız Scene görünümünü hizalar.
- "Round'a git" ve "Round'u yeniden başlat" oyunun round sonu olayını yeniden çalıştırır: bekleyen kart seçimleri sıfırlanır
  ve **son round'un tarla doluluğu 1'e döner**. Bereketli Toprak gibi geçmişe bağlı koşulu olan ödülü, *Round'u şimdi bitir*
  dedikten sonra, yeni round'a geçmeden kur.
- Sayısal kontroller ve şans örneklemesi oyunun rastgele akışını seed'ler (açık test eylemi) ve senaryoyu yeniden kurar;
  süren bir denemenin üstünde çalıştırılmaz.
- Saldırı izi aynı anda tek saldırıyı izler; otomatik saldırı açıkken araya giren saldırıların vuruşları "başka saldırı" diye
  işaretlenir ve bakiye farkı yalnız o saldırıya ait olmaz (iz uyarır).
- Kök taşımayan gecikmeli vuruşların (kasırga, bumerang — Zincir Hasat yokken; anten, hat) izlenen saldırıya bağı kanıtlanamaz:
  iz bunları "bağ kanıtı yok" diye gösterir, tahmin etmez.
- Ödül darbelerinin (anten, hat, hilal, ark, yankı, taşma, kritik patlama) çarpan dökümü izde yoktur: oyun yalnız sonucu
  bildirir. Hasarları kontrol kataloğunda asset değeriyle karşılaştırılır.
- HUD gizleme oyunun kendi arayüz nesnelerine göre çalışır: kota kartı Round kutusunun çocuğu olduğu için Round göstergesi
  gizlenince o da görünmez.
- Kayıt seviyesi tercihi bu makinenin Editor tercihinde saklanır (projeye ve oyuna yazılmaz).
- Şans, çift ödül ve bitki zarı kontrolleri istatistikseldir (240 / 600 deneme, tolerans ±4 standart sapma): büyük sapmayı
  yakalar, birkaç puanlık kaymayı yakalamaz. Daha dar sonuç için `H · Sayısal > Şans`'ta daha büyük örnek al.
- Tile kontrolleri tek tile ve en yüksek zarla yapılır; birden çok tile'ın toplanması rezonans kontrollerinde (2 × 3 saksı)
  dolaylı olarak doğrulanır.
- TODO P13'ün açık kalan maddeleri: skill ağacı panelden yönetilmiyor (kontrol satın alma yolunu kullanır); uzmanlaşma bu
  profilde kapalı; skill'lerin meta statlarının oyundaki etkisi ölçülmedi; boss'lar için hazır senaryo yok (kontrol var:
  G01–G03); final ve endless oyunda yok ("sistem henüz yok" olarak işaretli); **insan Play testi yapılmadı**.

## İnsan kontrol listesi (doldurulacak)

Otomatik test bu sütunları doldurmaz. Her satır için: **Geçti / Kaldı / Test edilmedi** ve kısa not.

| Senaryo | Mekanik doğru mu | Görsel / ses okunuyor mu | Not |
|---|---|---|---|
| 01 Doğrudan vuruş ve kritik | | | |
| 02 Patlama | | | |
| 03 Elektrik | | | |
| 04 Kasırga | | | |
| 05 Bumerang | | | |
| 06 Hasat Ritmi | | | |
| 07 Zincir Hasat (mor bildirim dahil) | | | |
| 08 Artçı Patlama / Çifte Akım | | | |
| 09 Anten / İletken Hat / Kapalı Devre | | | |
| 10 Hilal / Büyüyen Ay / Dolunay | | | |
| 11 Kritik Ark | | | |
| 12 Taşan Hasar | | | |
| 13 Hasat Yankısı | | | |
| 14 Nadir Reaktör | | | |
| 15 Açgözlü Hasat | | | |
| 17 Aşırı Yükleme | | | |
| 18 Diken Kuşatması | | | |
| 19 Aşılı Kabuk | | | |
| 20 İstilacı Filiz | | | |
| 21 Gezgin Bereket | | | |
| 22 Rezonans ve stat kaynağı | | | |
| 23 Bedelli ödül, dışlama, seçim hakkı | | | |
| 24 Kasırga / bumerang kapasitesi | | | |
| Boss: Don Cephesi / Sert Kabuk / Sis | | | |
| Panelin kullanımı (grid, sekmeler, metinler) | | | |
| H · Sayısal: Önce / Sonra (Örnek 1) | | | |
| H · Sayısal: Saldırı izi (Örnek 3) | | | |
| H · Sayısal: Makbuz ve buff (Örnek 2) | | | |
| H · Sayısal: Kontroller (kendi editöründe "Hepsini çalıştır") | | | |
| H · Sayısal: Şans örneklemesi | | | |
| H · Sayısal: Rapor / dışa aktarım (Örnek 4) | | | |
| HUD gizle / göster | | | |
