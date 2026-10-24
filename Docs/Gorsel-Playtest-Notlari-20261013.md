## 23 Ekim — ay ailesi kullanıcı kabulü

- [x] Hilal Darbesi, Büyüyen Ay ve Dolunay kullanıcı tarafından “istediğimiz gibi oldu” bildirimiyle tamamlandı olarak onaylandı.
- Kabul edilen son durum: 8 birim/sn; 3/4 dünya birimi rota; mesafeyle kesintisiz büyüme; son %35 mesafede solma; Dolunay tek büyük ekstra, farklı yön ve doğar doğmaz hareket. 244 kontrol PASS.
- Aşağıdaki eski ay tasarımı ve kabul bekleme notları tarihsel kayıttır; yeniden açık iş sayılmaz.
- Liste sırasındaki sonraki açık boss ödülü: Kritik Ark görselinin okunurluğu ve görsel yön seçimi.

## 23 Ekim — Dolunay doğar doğmaz hareket

- Tam sürümde ek hilalin 0,15 sn başlangıç beklemesi kaldırıldı; doğduğu anda 8 birim/sn ile hareket eder. 244 kontrol PASS. [Rapor](MoonImmediateMotion-20261023/README.md).

## 23 Ekim — hız ayarı 8

- Kullanıcı isteğiyle hilal ve Dolunay hızı 10 → 8 birim/sn yapıldı. Mesafeye bağlı büyüme ve son %35 mesafede opaklık geçişi korunuyor. Ayarlar ve test süre beklentileri eşitlendi; Unity testi bu ayar için yeniden çalıştırılmadı.

## 23 Ekim — 10 birim/sn ve yok oluş polish

- Hız 10. Hilal ve Dolunay son %35 mesafede hareket ederken opaklığını kaybeder; uçta ezilerek küçülme kaldırıldı. 242 kontrol PASS ve solma görüntüsü incelendi. [Rapor](MoonFade-20261023/README.md).

## 23 Ekim — hız denemesi 7,5

- Kullanıcı isteğiyle hız 6,5 → 7,5 birim/sn yapıldı. Kaynak, asset, üretici ve test süre beklentileri eşitlendi; erken büyüme kontrolünün örnekleme zamanı aynı dünya mesafesine bağlandı. Unity testi bu ayar için yeniden çalıştırılmadı.

## 23 Ekim — hız denemesi 6,5

- Kullanıcı isteğiyle hız 5,5 → 6,5 birim/sn yapıldı. Kaynak, asset, üretici ve test süre beklentileri eşitlendi. Unity testi bu ayar için yeniden çalıştırılmadı.

## 23 Ekim — hız denemesi 5,5

- Kullanıcı isteğiyle hız 4,5 → 5,5 birim/sn yapıldı. Kaynak, asset, üretici ve test süre beklentileri eşitlendi. Bu küçük ayarda Unity testi yeniden çalıştırılmadı.

## 23 Ekim — sürekli büyüme ve Dolunay tetiklenmesi

- Hız 4,5 birim/sn. Ana hilalin görsel genişliği tüm yol boyunca mesafeyle doğrusal artar; %80 noktasında tamamlanıp durmaz. Temel mesafe 3, Büyüyen Ay ile 4 birimdir.
- Tam sürümde Dolunay artık her ana hilalle tek büyük ek hilal çıkarır; eski iki ardışık hasat koşulu kaldırıldı. Yön farklı, ek hilalin boyutu sabit. Eski profil davranışı korunur.
- İzole Unity: 213 PASS. Hasatsız 50 ana hilal → 50 ekstra; son %10’da büyüme, havuz sınırları, hasar ve temizleme doğrulandı. [Rapor](MoonContinuousGrowth-20261023/README.md).

## 23 Ekim — küçük hız artışı

- Kullanıcı isteğiyle hilal ilerleme hızı 3,8 → 4,1 birim/sn (~%8) oldu. Mesafe ve mesafeye bağlı büyüme korunuyor. Kaynak, ödül verisi ve üretici parametresi eşitlendi; süre beklentileri güncellendi. Bu ayar için Unity koşusu tekrarlanmadı.

## Son ay düzeltmesi — 22 Ekim

- Hilaller tarla kenarında/kilitli hücrede silinmez; dünya üzerinde 3 birim, Büyüyen Ay ile 4 birim ilerler. Geçersiz hücrelerde hasar yoktur.
- Hız 3,2 → 3,8 birim/sn. Ana hilalin görsel büyümesi doğuşta başlar ve yolun ilk %80’i boyunca yumuşar. Ek Dolunay baştan büyük ve sabittir.
- İzole Unity: 108 kontrol PASS. Köşe dışına hareket, küçük/kilitli tarla, tam mesafe, hasar, farklı yönlü tek ekstra, havuz ve temizleme doğrulandı. [Rapor](MoonWorldTravel-20261022/README.md). Oyun hissi kullanıcı değerlendirmesine açık.

# Görsel Playtest Notları — 13 Ekim 2026

Bu belge kullanıcının test sahnesindeki gözlemlerini ve sohbet sırasında verdiği tasarım kararlarını toplar. Notların yazılması uygulandıkları anlamına gelmez. Bu turda oyun kodu, sahne veya asset değiştirilmedi. Görsel beğeni, bütün mekanik kenar durumlarının veya sayısal hesapların doğrulandığı anlamına gelmez.

## 1. Korunacak / beğenilen görseller

- T01–T06: Kullanıcının “ilk t6 şu an sorunsuz” bildirimi ilk altı temel saldırı/davranış kontrolü için olumlu not olarak alındı. Tek tek ölçüm sonucu değildir.
- Hasat Ritmi: Kullanıcı görseli çok beğendi; mevcut sunum korunacak.
- Zincir Hasat: İstenen şekilde çalışıyor. Mor zincir bildirimi korunacak.
- Nadir Reaktör: İmleç yanındaki mor elmas göstergesi, başlangıç, aktiflik ve bitiş beğenildi; korunacak.
- Elektrik Anteni / İletken Hat: Model ve bağlantı görselleri beğenildi. İletken Hat sorunu 19 Ekim kullanıcı bildirimiyle çözüldü olarak kapatıldı.
- Don Cephesi: Ekran kenarı donma atmosferi ve etkilenen bölgenin mavi sınırları beğenildi.
- Sert Kabuk: Mevcut atmosfer ve görünüm beğenildi; ek perde dışında korunacak.

## 2. Boss ödülleri: değişiklik ve inceleme listesi

### Artçı Patlama
- [x] Mekanik ve görsel uygulama tamamlandı; kullanıcı 22 Ekim'de “artçı oldu tamamdır” diyerek onayladı.
- [ ] Dengeleme sonraya bırakıldı. Güçlü kart sınıfında konumlandırılacak. Artçı hasarını azaltma ve/veya oyuncuya artçı hasarını geliştirme imkânı sunma seçenekleri değerlendirilecek; kesin katsayı ve geliştirme yöntemi henüz seçilmedi. Bu notla hasar veya ödül havuzu değiştirilmedi.
- 22 Ekim: Kullanıcı seçimiyle ikinci darbe Battle_Effect_White prefabına geçirildi; normalden küçük ölçek, sınırlı pooling ve normal bitki darbe tepkisi korunuyor.
- Kırmızı alan çizgisi kullanıcı kararıyla kaldırıldı.
- Artçı artık normal patlamanın dışından açık hücrelerde 0,2 sn arayla üç adım yayılır. İlk patlama alanını ve aynı dalgada vurulmuş hücreleri tekrar vurmaz; kilitli hücrelerden geçmez.
- İzole sahne görsel/hasar/havuz kontrolleri: [AftershockWave-20261022/README.md](AftershockWave-20261022/README.md). Kullanıcı kabulü 22 Ekim'de alındı; sayısal denge onayı ayrı ve açık.

### Çifte Akım
- Vurduğu hedefler ve hedeflerin tepkisi anlaşılır; kullanıcı bunları onayladı.
- 19 Ekim: Kullanıcı yalnız ikinci elektrik darbesinin sarımsı renge ayrılmasının tamamlandığını bildirdi. Normal elektrik rengini koruma kararı geçerli. Bu madde kullanıcı bildirimiyle kapatıldı; bu güncellemede kod/test yeniden incelenmedi.

### Elektrik Anteni / İletken Hat yerleşimi
**19 Ekim güncellemesi: Kullanıcı “İletken Hat çözüldü” bildirdi; ilgili sorun kapatıldı. Aşağıdaki maddeler önceki talebin kaydıdır. Bu güncellemede kod veya otomatik test yeniden incelenmedi; Kapalı Devre bu kapanışa dahil değildir.**

- İlk antenin seçilmiş kenarı ve satır/sütun hizası korunmalı.
- Grid büyüdüğünde anten içeride kalmamalı; aynı hizanın yeni dış kenarına taşınmalı.
- İletken Hat alındığında ikinci anten ilkinin tam karşısına, aynı satır/sütun üzerinde yerleşmeli.
- Grid büyüdüğünde ikisi de kendi dış kenarlarına taşınmalı, aradaki hat uzamalı.
- Birleşme uğruna önceden belirlenmiş sabit bir bağlantı konumuna taşınmaları istenmiyor.
- İlk testte büyümede taşınma doğrulanmamıştı; kullanıcı çözüm bildiriminde alt senaryoların ayrı test sonuçlarını paylaşmadı.

### Kapalı Devre
- [x] 22 Ekim: Ek hat darbesi ve temas kıvılcımları kırmızı yapıldı; normal hattın cyan/krem renkleri korundu. Üretici aynı renklerle güncellendi.
- Gerçek normal hat hasadının ek darbe planlaması, ek darbe callback'inin 10 canlı hedefe 27 hasar vermesi, kırmızı efektler ve sonraki normal hattın rengini koruması doğrulandı. 52 kontrol PASS; [rapor ve görseller](ClosedCircuit-20261022/README.md). Bu koşuda 0,35 sn zamanlayıcı ayrıca ölçülmedi; gerçek callback doğrudan çağrıldı.
- Kullanıcı beklemesine rağmen ek etkiyi fark edemedi. Tetiklenmedi mi, yoksa görseli görünmedi mi belirsiz.
- Tetik şartı, şans sonucu, gerçek ek hasar ve VFX başlatılması birlikte incelenmeli.
- Kullanıcının yeni kırmızı görünüm için görsel kabulü bekliyor; aşağıdaki eski belirsizliğin kontrollü tetik/hasar/VFX yolu testte doğrulandı.

### Hilal Darbesi
- [x] 23 Ekim: Son uygulama kullanıcı tarafından onaylandı; tamamlandı. Aşağıdaki notlar önceki iterasyonların kaydıdır.
- Son yükseklik/hız ayarı (22 Ekim): Saksıya gömülme bildirimi üzerine ortak ay görseli +0,53 birim yükseltildi; hız 2,5 → 3,2 birim/sn. 101 kontrol ve görüntü incelemesi geçti. [Güncel rapor](MoonHeightSpeed-20261022/README.md).
- 22 Ekim sonraki düzeltme: Kullanıcı önceki hız/konum/büyüme sonucunu yetersiz buldu. Halka kenarından çıkış, sabit gövde boyu ve 2,5 birim/sn hareket uygulandı. Güncel [99 kontrollük rapor](MoonPolish-20261022/README.md); yeni kullanıcı kabulü bekliyor.
- [x] 22 Ekim: Tam sürümde faredeki saldırı noktasından rastgele ana yönde düz rota; en fazla 3 dünya birimi. Adım aralığı 0,07 → 0,3 sn. Temel dalga geri dönmez. Uygulama ve doğrulama: [Ay ailesi raporu](MoonFamily-20261022/README.md). Kullanıcı görsel/hız kabulü bekliyor.
- Kullanıcının gözlemi: çok hızlı, gelip geçiyor; mouse'a doğru geliyor gibi görünüyor.
- İstenen: mouse'un hedeflediği dünya konumundan doğsun, gridin dört ana yönünden rastgele birine mouse'tan uzaklaşarak ilerlesin; çapraz gitmesin.
- Temel dalga geri dönmesin.
- En fazla 3 birim mesafe istendi; bu notta dünya birimi olarak tutuluyor. Mevcut hücre/adım mesafesiyle eşleşmesi uygulamada doğrulanmalı.
- Daha yavaş, görünür ve hissedilir olsun. Kesin hız belirlenmedi.
- Hasar ile görsel temas aynı anda anlaşılmalı.

### Büyüyen Ay
- [x] 23 Ekim: Son uygulama kullanıcı tarafından onaylandı; tamamlandı. Aşağıdaki notlar önceki iterasyonların kaydıdır.
- Son karar (22 Ekim): Ana hilalin görsel büyümesi doğuşta başlar, hasat beklemez; 0,3 sn sonra büyük boyutta kalır. Hasar büyümesi ayrı mevcut hasat kuralını korur. Ek Dolunay baştan büyük ve sabittir. [102 kontrol geçti](MoonSpawnGrowth-20261022/README.md).
- 22 Ekim sonraki düzeltme: Alındığında menzil +1 (3 → 4 dünya birimi). Görsel genişleme ve küçülme 0,3 sn içinde yumuşatıldı; büyümede pivot/uzunluk değişiminden gelen geriye sıçrama kaldırıldı.
- [x] 22 Ekim: Yeni kısa/yavaş rota ile birlikte gerçek hasatta büyüme, sonraki hasatsız adımda sıfırlanma ve hasar artışı doğrulandı. Ortak hareket değişikliğini kullanır; büyüme katsayıları korunur. Kullanıcı kabulü bekliyor.
- Hilal çok hızlı olduğundan büyüme ve sıfırlanma değerlendirilemedi.
- Hilal hareketi düzeltildikten sonra tekrar test edilecek; ayrı mekanik arızası kanıtlanmadı.

### Dolunay
- [x] 23 Ekim: Son uygulama kullanıcı tarafından onaylandı; tamamlandı. Aşağıdaki notlar önceki iterasyonların kaydıdır.
- **Güncel karar (22 Ekim): 1 ana Hilal Darbesi + 1 büyük Dolunay = toplam 2.** İki ek dalga yanlış yorumdu; tek ek hilale çevrildi. Ek hilal baştan büyük ve ana hilalden farklı yönde. [101 kontrollük güncel rapor](FullMoonSingle-20261022/README.md). Aşağıdaki çift/12 yuva notları tarihsel.
- 22 Ekim sonraki düzeltme: Dört ana dalganın bütün yuvaları doldurup Dolunay'ı engellemesi giderildi; dört ana + sekiz ek dalga için sınırlı kapasite ayrıldı. Gerçek saldırıdan Dolunay'a akış ve köşede farklı iki yön doğrulandı. Büyüme eşiği şartı korunuyor.
- [x] 22 Ekim: Kullanıcı yön kararını verdi: iki rastgele ana yön, fakat asla aynı yön değil. Tam sürümde eski geri dönüş yerine büyüme eşiğinde ilk saldırının fare noktasından iki hilal çıkar. Eşik, dalga başına hasar oranı ve saldırı duraklaması korunur; bu iki dalga büyümez/yeni Dolunay başlatmaz. Kullanıcı görsel kabulü bekliyor.
- Karşı uçtan geri gelen dalga tasarımı değişecek.
- Bir yerine iki hilal mouse konumundan çıkacak.
- Yönler 22 Ekim'de kararlaştırıldı: dört ana yönden rastgele iki farklı yön; dik veya zıt olabilirler.
- Görsel test tamamlanmadı. Mevcut tetik eşiği, hasar ve saldırı duraklamasını değiştirme kararı alınmadı.

### Kritik Ark
- Sıçrama çalışıyor ve elektrik görülüyor, ancak çok küçük.
- Elektrik daha okunur yapılabilir veya elektrikten farklı bir görsel kullanılabilir; seçim açık.
- Genel not: Çok sayıda ödülde elektrik kullanılması görsel çeşitliliği azaltıyor.

### Taşan Hasar
- Aktarım çalışıyor; geri bildirim yetersiz.
- Ek yeni aktarım VFX'i yerine taşan hasarı alan hedeflerin hasar popup'ları farklı renkte gösterilecek.
- Kırmızı/mavi örnek verildi; kesin renk belirlenmedi.
- Mevcut aktarım görselinin kaldırılması yönünde karar verilmedi.

### Hasat Yankısı
- Karşı/simetrik hedefin öldüğü görüldü; sonuç var fakat darbe hissi yok.
- Öneri: karşı hedefe yukarıdan küçük sci-fi meteor/enerji parçası düşmesi ve belirgin temas geri bildirimi.
- Bu görsel yön henüz kesinleştirilmedi. Gerçek darbe zamanıyla uyumlu olmalı; sırf animasyon için hasar geciktirilmemeli.

### Açgözlü Hasat
- Mevcut parçaların konumu ve oranları beğenilmedi; imleçten kopuk duruyor.
- Parçalar beyaz imleç halkasının hemen dışında, aynı merkez çevresinde dönecek.
- Boyut ve uzaklık halka ile orantılı olacak; daha büyük hasar alanı varmış gibi gösterilmeyecek.
- Bütün tüketim/boşa saldırı kenar durumları bu görsel yorumla onaylanmış sayılmamalı.

### Kritik Patlama
- Normal patlama davranışıyla fazla örtüşüyor; ayrı değer ve geri bildirim hissedilmiyor.
- 19 Ekim: Kullanıcı Kritik Patlama'nın kaldırıldığını bildirdi. Kaldırma işi kullanıcı bildirimiyle tamamlandı olarak kaydedildi.
- Bu ödüle ek polish yapılmayacak. Bu güncellemede havuz, referans, senaryo ve test temizliği bağımsız olarak incelenmedi.

### Aşırı Yükleme
- Kullanıcı ne olduğunu görsel olarak anlayamadı; görsel kabul verilmedi.
- Yazılı açıklaması net bir pasif buff olarak kalabilir; yeni büyük VFX istenmiyor.
- Bekleme süresinin ayrıca gösterilip gösterilmeyeceği kararsız.
- Küçük başarılı tetik vurgusu bir öneri, kesin üretim kararı değil.
- Mekaniğin bu kullanıcı testinde doğrulandığı iddia edilmemeli.

## 3. Tarla olayları ve vurulabilir hedefler

### Ortak konumlandırma
- Vurulabilir modeller ve ağız/çember/diken benzeri bölge görselleri düzgün ortalanacak.
- Saksı varken ilgili saksı/hedef ile, saksı yokken ilgili grid hücresiyle hizalanacak.
- Saksısız durumda eski saksı ofsetiyle kenarda veya havada durmamalı.
- Hedef kaybolduktan sonra kalan bölge görselleri de doğru merkezde durmalı.
- Bu kural tek hücreli ve çok hücreli düzenlerde kontrol edilmeli; tüm çok hücreli saksıyı körlemesine merkez almak etkilenen hücreyi değiştirmemeli.

### Ortak vurulma hissi ve kalan vuruş
- Bütün vurulabilir olay/boss hedeflerinde geçerli vuruşun alındığı daha belirgin olmalı.
- Darbe hareketi, kısa parlama ve ses güçlendirme yönleri olarak not edildi; normal darbe ile son kırılma ayrılmalı.
- Hedefin kapanmasına/kırılmasına kaç vuruş kaldığı küçük yerel bir göstergede anlaşılmalı.
- Kullanıcı küçük can barı veya kalan vuruş göstergesi istedi. Vuruş sayısıyla kırılan hedefler için bölmeli gösterge önerildi; nihai biçim belirlenmedi.
- Yalnız gerçekten kabul edilen vuruş göstergeyi azaltmalı; kalan vuruş ile ham hasar karıştırılmamalı.

### Diken Kuşatması
- Ek toprak parçaları yanlış yerde ve kalabalık görünüyor; olaya ait ek toprak kaldırılacak.
- Saksının kendi toprağı kaldırılmayacak.
- Saksı çevresindeki ağız/diken görselleri korunacak ve ortalanacak.
- Vurulma hissi güçlendirilecek.
- Bitiş temizliği bu turda ayrıca onaylanmadı.

### İstilacı Filiz
- Toprak temaya uyuyor; korunacak.
- Filizin çıkacağı hücrenin merkezine alınacak.
- Çiçek ortaya çıktığında toprak kaybolacak; çıkış öncesi belirti olarak kullanılacak.
- Çiçek ve toprak aynı konumdan çıkıyormuş gibi hizalanacak.
- Diğer mekanik ve bitiş durumları bu yorumla topluca onaylanmadı.

### Aşılı Kabuk
- Ortak hizalama, kalan vuruş ve darbe hissi düzeltmeleri uygulanacak.
- A/B renk ve motif ayrımı korunacak.
- Hedef sonrası kalan bölge çemberleri de ortalanacak.
- HUD paneli kota paneliyle üst üste biniyor; UI paketine dahil.

### Gezgin Bereket
- Mevcut yükselen perde efekti Don ve Sert Kabuk için görsel referans olarak beğenildi.
- Bu turda olayın bütün çalışma/bitiş durumlarına yeni toplu kabul verilmedi.

## 4. Boss atmosferleri

### Don Cephesi
- Ekran kenarı donma atmosferi ve mavi bölge sınırı korunacak.
- Kar taneleri arka plan yıldızlarıyla karışıyor; daha net ayrışmalı. Şekil ve süzülme hareketiyle ayrışma önerildi; yalnız yoğunluk artırılmamalı.
- Gerçek etkilenen bölgenin sınırından yukarı yükselen ve yüksekte soluklaşan mavi perde eklenecek.
- Referans Gezgin Bereket perdesi; kuzey ışıkları hissi.
- Bitki, hedef ve vuruşları örtmemeli.

### Sert Kabuk
- Mevcut görünüm/atmosfer ve turuncu sınırlar korunacak.
- Don ile aynı mantıkta, Gezgin Bereket referanslı kahverengi yükselen/solan perde eklenecek.
- Don'un perdesi mavi, Sert Kabuk'unki kahverengi.

### Sis
- Bu turda yeni görsel değerlendirme bildirilmedi.

## 5. HUD ve UI yerleşimi

- Level/XP barı görsel olarak iyileştirilecek; level yazısının boyutu ve hizası barla birlikte düzenlenecek.
- Round yazısı üst ortaya taşınacak; gridi ve koordinatları örtmeyecek. Süre bilgisi de çakışmayacak şekilde yerleştirilmeli.
- Altın, demir ve taş göstergelerine uygun ikonlar eklenecek; kaynak grubu sağ üste taşınacak.
- Segment kotası paneli round yazısının eski yerine, sol üste taşınacak.
- Kota paneli sabit/kompakt boyutta olacak; bilgi arttıkça tarlaya doğru büyümeyecek.
- Görünür bilgi sınırlı olacak; taşan içerik mouse panel üzerindeyken kaydırılabilecek. Bilgi silinmeyecek, sınırlı görünüm alanında tutulacak.
- Hover sırasında hafif büyüme, çıkışta normale dönüş ile polish istendi. Hareket paneli gridin üstüne taşırmamalı veya yerleşimi zıplatmamalı.
- Kota, olay ve diğer HUD panelleri birbirinin üzerine binmeyecek.
- Kullanıcının ikinci referans görüntüsündeki gibi boyut/konum, grid ve koordinatların üstüne gelmeyecek şekilde ayarlanacak. Azami grid boyutunda da kontrol edilmeli.
- İkon üretimi/dış kaynak kullanımı için AGENTS.md lisans kuralı geçerli; bu not asset ekleme işlemi değildir.

## 6. Açık kararlar ve yeniden test

Karar bekleyenler:
- Artçı Patlama renk/çizgi kararı 22 Ekimde kapandı: Battle_Effect_White, kırmızı çizgi yok.
- Dolunay'ın yön kararı kapandı: rastgele ama daima farklı iki ana yön (22 Ekim).
- Kritik Ark'ın elektrik olarak mı, farklı görselle mi devam edeceği.
- Taşan Hasar popup rengi.
- Hasat Yankısı meteor/enerji görselinin nihai tasarımı.
- Aşırı Yükleme bekleme göstergesi ve küçük tetik vurgusu.
- Vurulabilir hedeflerin kalan vuruş göstergesinin nihai biçimi.

Öncelikli mekanik/görsel incelemeler:
- Kapalı Devre: gerçek tetik / ek hasar / VFX ayrımı.
- İletken Hat sorunu 19 Ekim kullanıcı bildirimiyle kapandı; yeni bir sorun bildirilmedikçe açık düzeltme olarak taşınmayacak.
- [x] Hilal: yön, hız, mesafe ve sunum 23 Ekim kullanıcı kabulüyle kapandı.
- [x] Büyüyen Ay ve Dolunay: yeniden test ve 23 Ekim kullanıcı kabulü tamamlandı.
- Olayların saksılı/saksısız hizası ve bitiş temizliği.

Sayısal test notu: Kullanıcı Tornado-Rare etiketini düzelttiğini bildirdi. Yeni koşu sonucu paylaşılmadığı için ilgili iki FAIL bu belgede yeniden PASS ilan edilmedi.

## 7. Referans görseller

Görseller kullanıcı tarafından sohbet içinde sağlandı; aşağıdaki adlar geçici clipboard dosyalarını tanımlar. Kalıcı proje varlığı olarak kopyalanmadılar.

- Anten yerleşimi: `codex-clipboard-e392809e-5157-456e-a37f-19f4bfbf6da3.png`
- Nadir Reaktör: `codex-clipboard-b1456499-2032-47cc-aa7f-e6cde8c3075f.png`
- Açgözlü Hasat: `codex-clipboard-2e1dea81-8787-4893-aeac-9931dafed4a3.png`
- Diken: `codex-clipboard-70fbca58-ac5e-436c-ab10-898e9edf365a.png`
- İstilacı Filiz: `codex-clipboard-933a0837-6dc4-4ff8-8b39-2463ff0665cc.png`
- Ortak hedef/bölge hizası: `codex-clipboard-c4d537a9-e442-4323-84c0-febfe2ba9e83.png`, `codex-clipboard-459c6327-bf47-4c12-b601-b56e39fda9db.png`, `codex-clipboard-98fc57a3-255f-45ed-8817-af9f68980905.png`
- Don: `codex-clipboard-6e16ee85-7622-4791-9ca9-6c0dcdeae7a6.png`
- Sert Kabuk: `codex-clipboard-fc573d85-292f-4597-9583-a5a72620f53e.png`
- HUD referansları: `codex-clipboard-036e0b8a-e04a-4156-9f18-b68b235b3f9d.png`, `codex-clipboard-a05c530f-dd83-4319-ab88-e7094a9b5f6e.png`
