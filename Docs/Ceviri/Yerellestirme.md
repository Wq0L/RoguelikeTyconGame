# Yerelleştirme (çeviri) sistemi

**Tarih:** 4–5 Kasım 2026.

## TAŞIMA ADIM 3–4: koddaki yazılar ve veri adları (5 Kasım öğleden sonra)
Liste 109 → 698 anahtar. Türkçe sütun eski Türkçe yazıyı birebir tutar (testler Türkçe çalışır), İngilizce sütun tam.
Bilerek değiştirilen Türkçe: saksı dükkânı (eskiden İngilizceydi: "SATIN AL" …), bitki adları (Havuç …), "SONRAKİ ROUND".
- **Ekranlar:** başlangıç seçimi, HAZIRLIK / round özeti (kota, boss bildirimleri), HUD (kota kutusu, boss satırı, Hasat Ritmi,
  round / süre, Gold-Iron-Stone), ÖNİZLEME, bu round'un kartları, kart seçimi, tile tooltip'i, yerleştirme önizlemesi,
  yetenek popup'ı, boss ödülü ekranı, geç oyun ödüllerinin kural metinleri, anten yerleştirme, uzmanlaşma paneli, ekran
  damgaları (ROUND / SON ROUND / LEVEL UP), saksı dükkânı, run sonu, 4 tarla olayı, satıştan çık / geri.
- **Ortak:** TileBuffText (stat / nadirlik / kaynak adları: stat.*, rarity.*, resource.*), SegmentEventText, BossRewardText.
- **Veri adları** (`GameText.Data(anahtar, asset yazısı)`; anahtar yoksa asset yazısı görünür):
  - kimlikli veri `id` ile: başlangıç seçenekleri `data.start.<id>.name/.style`, görev `data.quest.<id>`,
    boss ödülü `data.reward.<id>.name/.effect/.inverse`, ödül aşaması `data.reward_stage.<id>.name`;
  - kimliksiz veri Türkçe yazının kısaltmasıyla (`GameText.Slug`): yetenek düğümü `data.skill.<gövde>.name` (" – 1" eki
    çevrilmez), tile, boss, rezonans, bitki, uzmanlaşma, profil zafer başlığı;
  - ödül notu sürüme göre farklı olabildiği için yazısının özetiyle: `data.reward.<id>.note_<6 hane>` (`GameText.ShortHash`).
- **Sayı biçimi:** `GameText.Culture / Number / GroupSeparator`. Eskiden virgülle yazılan yerler dile göre (en "1.25",
  tr "1,25"), binlik ayırıcı dile göre (en "12,345", tr "12.345"); eskiden noktayla yazılan stat değerleri her dilde nokta.
- **Kapsam testi** (LocalizationVerification → CoverageChecks): koddaki her anahtar listede, türetilen anahtarlar (enum,
  .detail, .boss, .lower, ödül etiket biçimleri), oyuna giren her veri adının satırı, İngilizce sütunda Türkçe harf yok,
  başlangıç kartı / düğüm adı / sayı biçimi iki dilde.
- **Çalıştırıcı düzeltmesi:** izole kopya Assets/Localization'ı /XO ile alır (ana projedeki eski tablo kopyanın yeni
  tablosunun üstüne yazmasın; ilk turda 5 test anahtar adı gördü, sebebi buydu).
- **Bilerek kalan:** demo açılış ekranı (WebGL demo hedef dışı), geliştirici doğrulama / log mesajları, RunEffect.Describe
  (yalnız test penceresi), sahne / prefab'da koddan doldurulan yer tutucu yazılar (tarayıcı "sahne/prefab" sayar).
- **Bulgu (çeviriyle ilgisiz):** KirilmaV1Verification artçı patlamayı eski V1 kuralıyla bekliyor (3 Ekim'de artçı "dış
  sınırdan adım adım yayılır" kuralına geçti); test güncellenmemiş.
- **Kullanıcıya:** Türkçe tile adları iç ad gibi görünüyor ("Crystal-RareSpawnChance", "Odak-Hasar"): artık listede tek
  hücreden düzeltilebilir.

## İKİNCİ İNCELEME (5 Kasım öğlen; 4 not, dördü de doğru, yapıldı)
1. **Ana projede eski dil döngüsü kalmıştı** (en → tr, tr → en). Otomatik kurulum "kurulum var" deyip çalışmıyordu, testler kendi kopyasını düzelttiği için geçiyordu.
   - `LocalizationSetup.ConfigProblems()` artık "var mı" değil "güncel mi" diye bakıyor: dil yedekleri, proje dili, eşzamanlı başlatma, UseFallback, açılış kuralı.
   - Otomatik kurulum güncel değilse çalışıp düzeltiyor.
   - Derleme denetimi güncel olmayan kurulumda durduruyor.
   - Test, kopyaya eşitlenen ana proje dosyalarında döngüyü yakaladı ve onarımı doğruladı.
   - Kullanıcının editörü yeni kodu derleyince ana proje kendiliğinden düzelecek. Console'da "Unity Localization güncellendi" yazısı görünmeli.
2. **Yalnız başlıktan oluşan liste** artık hata (aksi hâlde bütün anahtarlar silinirdi).
3. **Boşluklu hücre** her yerde "boş" (`GameText.IsBlank`): denetim, aktarım, derleme denetimi ve CSV okuyucusu aynı tanımı kullanıyor.
4. **İç içe adlı yer tutucular** da karşılaştırılıyor (çoğul içindeki `{player}` ≠ `{name}`).

## TAŞIMA ADIM 2b: ayarlar menüsü (5 Kasım öğlen)
- `OptionsMenuBuilder` artık bütün yazıları anahtarla kuruyor: başlık, sekmeler, satır başlıkları (`LocalizedText`), açıklamalar (`<anahtar>.desc`), açık / kapalı, düğmeler, İMLEÇ rozeti.
- Satır nesnelerinin adı anahtar; editörün diline göre değişmiyor.
- `OptionsUI` değerleri (ekran modu, FPS, kalite) anahtardan okuyor; dil değişince panel kendini o anda yeniliyor.
- `OptionRow` / `OptionToggle` / `OptionsUI` geriye dönük uyumlu (`GameText.Resolve`): kurucu yeniden çalışana kadar eski sahnedeki Türkçe metinler aynen görünür.
- Listeye 64 `ui.options.*` anahtarı eklendi (İngilizce + Türkçe + not).
- **Kullanıcı yapacak:** MenuScene'de `Tools > Comic UI > Build Options Menu` çalıştırıp sahneyi kaydetmek.

## TAŞIMA ADIM 2: sahne ve prefab yazıları (5 Kasım; kullanıcı kendi projesine UYGULADI)
**Paket kontrolü:** Unity Localization'da toplu dönüştürücü yok.
- Yalnız bileşen başına elle "Localize" var.
- "Property Variants" çeviriyi sahneye yazıyor; tek liste düzenine uymuyor.
- Bu yüzden kendi aracımız var: `TextBindingTool`. Altında yine paketin tabloları çalışıyor.

**Araç (Tools > Çeviri):**
1. **"Sahne ve prefab yazıları: plan çıkar"** → `Docs/Ceviri/Baglama_Plani.csv`.
2. **Gözden geçirme.**
3. **"… planı uygula":**
   - listeye ekler (önce denetler);
   - yazıya `LocalizedText` bağlar;
   - metin plandakiyle aynı değilse dokunmaz.

**Plan (gözden geçirilmiş):**

| Grup | Sayı | Not |
|---|---|---|
| bagla | 37 (21 anahtar) | Sabit düğme ve başlıklar. Betik kodu tek tek okunarak doğrulandı. |
| kurucu | 44 | Ayarlar menüsü. `OptionsMenuBuilder` sahneyi her kurulumda baştan yazdığı için çeviri kurucunun kendisinde yapılacak (sıradaki iş). |
| kod | 44 | Kodun yazdığı yazılar (sayaç, fiyat, kart adı, saksı dükkânı başlığı / açıklaması): kod adımında. |
| prefab | 36 | Sahnedeki kart kopyaları; prefab'ın kendisinde ele alındı. |

**İzole kopyada uygulandı:** `BindingVerification` 10/10.
- 21 anahtar, 37 bağlama, hiçbiri atlanmadı;
- ikinci uygulama hiçbir şey değiştirmiyor;
- Play'de "Oyna" ↔ "Play" anında değişiyor.

**Kullanıcının projesine HENÜZ uygulanmadı.** Kullanıcı çevirileri onaylayınca kendi editöründe "planı uygula"yı çalıştıracak.

**Sıradakiler:**
- `OptionsMenuBuilder`'ı anahtarlı yapmak;
- kod adımı (yaklaşık 91 yer, `PlanterShopPanelUI` / `CardUI` dahil);
- veri adımı (yetenek düğümleri; anahtar düğüm kimliğinden üretilecek).

## KALDIĞIM YER (5 Kasım öğleden önce, ikinci oturum)
**Bu oturumda yapıldı ve doğrulandı:**
- Kullanıcının editöründe otomatik kurulum çalıştı:
  - `Assets/Localization/{Settings,Locales,Tables}`, `Assets/AddressableAssetsData` ve ProjectSettings kaydı oluştu;
  - kullanıcı ayarlar menüsünü yeniden kurdu, MenuScene'de "Dil / Language" var.
  - Kurulumun yanlışlıkla açtığı boş `Assets/Localization/AddressableAssetsData` klasörü silindi.
- **Gerçek Windows derlemesinde dil testi GEÇTİ** (`LocalizationBuildSmoke`, 5/5):
  - yazılar her dilde Unity tablolarından geliyor;
  - sahne yüklenmeden önce okuma, sahne yüklendiği anda okuma ve aynı karede dil değiştirip okuma çalışıyor.
- **Test bir paketleme hatası yakaladı:**
  - Etkin platform WebGL'deyken Windows derlenince Addressables paketleri yanlış klasöre (`aa/WebGL`) yazılıyordu; oyuna tablolar gitmiyordu.
  - `LocalizationBuildCheckEarly` (Addressables'tan önce çalışır) artık böyle bir derlemeyi "önce platformu değiştir" diye durduruyor; test de bunu doğruluyor.
- **WebGL:** kullanıcı kararı, hedef yalnız Windows / Steam. Yerelleştirme WebGL'e uyarlanmadı; `Tools > Demo > Build WebGL Demo` derlemeden önce uyarıyor.
- **Tarayıcı oyuna giren veriyi ayırıyor:**
  - ölçüt: derleme sahneleri + demo sahnesi + Resources ve bunların kullandıkları;
  - oyuna girmeyen veri "kullanilmiyor" kategorisinde.
  - Çevrilecek: 641 kayıt, **397 farklı metin**.
- **Not:** yetenek ağacı iki kopya oyuna giriyor. Sahne `FinalSkillTree`'yi bağlıyor, `SkillTreeSet_DengeV1` oyun sırasında `DengeV1` düğümleriyle değiştiriyor. Taşırken anahtar dosya yolundan değil düğümün kimliğinden üretilmeli; iki kopya aynı anahtarı paylaşsın.
- **Test sonuçları:**
  - LocalizationVerification 70/70;
  - OptionsMenuVerification geçti;
  - DialogueVerification 74/74, DialogueFlowVerification 87/87. Bu ikisi derleme denetimi ayrımı ve tarayıcı değişikliğinden önce koştu; o değişiklikler onların koduna dokunmuyor.

**Sıradaki:** taşıma planı adım 2.
- Sahne ve prefab yazılarını `LocalizedText` + anahtara bağlayan araç; İngilizce ve Türkçe hücreler doldurulacak.
- Önce kullanıcıyla anahtar adlandırma ve İngilizce kaynak metinleri konuşulmalı. Sahnelerde İngilizce ve Türkçe karışık; İngilizcesi olmayanları ben yazacağım, kullanıcı gözden geçirecek.

## ÖNCEKİ KALDIĞIM YER (5 Kasım, ilk oturum limiti)
**Yapıldı ve test edildi** (izole kopya):
- Unity Localization entegrasyonu; kaynak dil İngilizce; ayarlarda dil seçimi.
- Açılış dili: oyuncunun seçimi → bilgisayarın dili → İngilizce.
- Son testler:
  - LocalizationVerification 68/68 (en son hâl);
  - ayarlar menüsü testi geçti;
  - DialogueVerification 74/74 ve DialogueFlowVerification 87/87 (son dört düzeltmeden önceki hâlle).

**Kullanıcı incelemesinden gelen 4 düzeltme yapıldı ve 68/68 testte geçti:**
1. **Dil zinciri tek yönlü:** her dil → en → bitti. Unity döngülü zincirde yeni dil bağlantısını reddediyordu (`FallbackLocale.IsCyclic`, kaynakta doğrulandı).
2. **Önce denetim, sonra aktarım:** bozuk liste tablolara dokunmadan reddediliyor (`InvalidListException`). Kapanmamış tırnak, fazla sütun ve hücre içi satır sonu artık hata.
3. **Gizli CSV yedeği görünür oldu:** editörde CSV yedeğine düşülürse açık uyarı çıkıyor. Derleme öncesi denetim (`LocalizationBuildCheck`) tablolar listeyle güncel değilse derlemeyi durduruyor.
4. **Yer tutucu denetimi genişledi:** sayılı ve adlı yer tutucular ({0}, {player}) ve çoğul seçicisi karşılaştırılıyor. Smart metin, paketin SmartFormat ayrıştırıcısıyla doğrulanıyor.

**Sıradaki adımlar:**
1. DialogueVerification, DialogueFlowVerification ve OptionsMenuVerification'ı son hâlle yeniden çalıştır.
2. Gerçek derleme dil testi YAZILDI ama HİÇ ÇALIŞTIRILMADI. Birkaç dakika sürer, bilgisayarı yorar; önce kullanıcıya söyle:
   ```
   Tools/run-isolated-verification.ps1 -Method LocalizationBuildSmoke.RunBatch -Full -TimeoutSec 2400
   ```
3. Kullanıcının editöründe:
   - Otomatik kurulum çalıştı mı? Console'da "Unity Localization kuruldu" yazmalı; `Assets/Localization/{Settings,Locales,Tables}` ve `Assets/AddressableAssetsData` oluşmalı.
   - MenuScene'de `Tools > Comic UI > Build Options Menu` çalıştırılıp dil satırı eklenmeli.
   - Play'de dil değiştirme denenmeli.
4. Kullanıcı İngilizce satırları ve Offering / Trial terimlerini gözden geçirecek.
5. Sonra taşıma planı (aşağıda): sahne, kod ve veri yazıları listeye.

**Durum:**
- Oyunun sahne ve kod yazıları henüz tabloya TAŞINMADI. Şimdilik tabloda yalnız konuşmalar var.
- Play'de görülmedi.

## Mantık (profesyonel yerelleştirmedeki gibi)
- **Tek ana liste:** `Assets/Localization/GameText.csv`. Sütunlar `key, note, en, tr, …`. Yazı ve çeviri yalnız burada düzenlenir.
- **Kaynak dil İngilizce:** çevirmenler İngilizceden çevirir. İngilizce hücre hiçbir satırda boş olamaz; denetim bunu hata sayar.
- **Açılış dili:**
  - oyuncunun ayarlardan seçtiği dil (Ayarlar > Görüntü > en üstteki "Dil / Language");
  - seçim yoksa bilgisayarın dili, tabloda varsa;
  - o da yoksa İngilizce.
  - Seçim PlayerPrefs'te saklanır.
  - Otomatik testler her zaman Türkçe açılır ve oyuncu ayarlarını ne okur ne yazar.
- **Anahtar:** kod, sahne ve veri dosyaları yazıyı değil anahtarı bilir (ör. `ui.skip`).
- **`note` sütunu:** çevirmen notu (kim söylüyor, hangi ekran, yer kısıtı, `{0}`'ın anlamı). Unity'de satır yorumu olur, oyunda görünmez.
- **Dil zinciri:**
  - Bir dilde hücre boşsa İngilizce görünür.
  - İngilizce boşsa (yalnız geliştirmede) Türkçe görünür.
  - Oyun asla boş yazı, anahtar ya da "çeviri yok" mesajı göstermez.
- **Yer tutucu:** değişkenli yazı tek anahtarda yazılır: `{0} gold` → "12 gold".
  - Çoğul kalıbı: `{0:plural:1 round|{} rounds}`. Paket her dilin kendi çoğul kuralını uygular (Rusça ve Lehçenin 3–4 biçimi dahil).
- **Unity tarafı:**
  - Liste kaydedilince Unity Localization'ın "GameText" tablosuna kendiliğinden aktarılır. Oyun bu tabloyu okur.
  - Tablolar oyun derlenirken Addressables ile kendiliğinden paketlenir; elle bir adım gerekmez.
  - Oyun açılırken tablolar hazır olur (eşzamanlı yükleme).
- **Oyun terimleri** (çevirmen notunda "translate consistently" yazar):
  - Adak = **Offering**
  - Sınama = **Trial**

## Nasıl kullanılır
- **Metni değiştirmek:** CSV'yi düzenle ve kaydet. Play açıksa yazılar o anda değişir (açık konuşma kutusu dahil).
  - Konsola "Çeviri listesi yenilendi: N anahtar; tablo: … yeni, … değişen, … silinen" yazılır.
- **Yeni dil:** CSV'ye sütun ekle (ör. `de`), sonra Tools > Çeviri > Kurulum. Dil ve tablosu kendiliğinden oluşur.
- **Sahnedeki bir yazıyı bağlamak:** yazı nesnesine `LocalizedText` bileşeni ekle, `key` alanına anahtarı yaz.
- **Kodda:**
  - düz yazı: `GameText.Get("anahtar")`;
  - değerli yazı: `GameText.Format("anahtar", değer)`;
  - dil değişince yenilenmesi gereken kod `GameText.Changed`'i dinler.
- **Tools > Çeviri menüsü:**
  - **Kurulum ve listeyi tabloya aktar:** ilk kurulum ve elle yeniden aktarma. Tekrar çalıştırmak zararsızdır.
  - **Listeyi denetle:**
    - hatalar: yinelenen anahtar, boş İngilizce hücre, bir dilde `{0}` farklı;
    - uyarılar: etiket farkı, anahtar biçimi;
    - her dilde eksik sayısı.
  - **Sabit yazıları listele:** `Docs/Ceviri/SabitYazilar.csv`, çeviri listesinin taslağı.
  - **Dil (Play): Türkçe / English / Sahte dil.** Sahte dil bağlı yazıları `[Şkîp~·]` gibi aksanlı ve %35 uzun gösterir. Ekranda düz kalan yazı tabloya bağlanmamış demektir.

## Sabit yazı taraması (4 Kasım)
| Kaynak | Bulunan | Not |
|---|---|---|
| Veri dosyaları | 485 (301 farklı) | Çoğu yetenek ağacı adı. Eski denge sürümleri de sayıldı (DengeV1, GecOyunV1 …). |
| Sahneler | 148 (71 farklı) | MenuScene 46, GameScene 51, DemoScene 51 (Game'in kopyası) |
| Kod | 91 | "SON ROUND!", "LEVEL UP!" … |
| Prefab'lar | 12 | |
| Geliştirici araçları | 6 | Çevrilmez |

## Taşıma planı (sıradaki iş)
1. **Canlı veriyi ayır.** Oyunun gerçekten kullandığı veri dosyaları listelenir; eski denge sürümleri çevrilmez.
2. **Sahneler ve prefab'lar.** Bir araç her yazıya `LocalizedText` ve anahtar ekler, metni listeye yazar (İngilizce + Türkçe).
3. **Veri dosyaları.** Ad ve açıklama alanları anahtarla okunur (`data.<dosya>.<alan>`).
4. **Kod.** Yaklaşık 90 yer `GameText.Get` / `Format`'a çevrilir.
5. **Kontrol.** Sahte dille Play'de bütün ekranlar gezilir.
6. **Sonra (çeviri zamanı):**
   - ayarlarda dil seçimi;
   - her alfabe için font (lisans kontrolüyle);
   - sağdan sola yazım kararı;
   - çevirmen dosyaları;
   - her dilde ekran görüntüleri.

**Kural:** yeni eklenen her oyuncu yazısı doğrudan listeye girer: İngilizce, Türkçe ve çevirmen notuyla.

**Lisans:** Unity Localization ve Addressables için Unity Companion License v1.4. Ücret ve kredi zorunluluğu yok. Dağıtımda SmartFormat (MIT) bildirimi gerekli. Ayrıntı: `Docs/ThirdParty-Licenses.md`.
