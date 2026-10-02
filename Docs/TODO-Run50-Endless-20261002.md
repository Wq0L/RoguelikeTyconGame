# Ana TODO — Run50, build kırılması ve sınırsız devam

Tarih: 2 Ekim 2026. Kullanıcının bugün netleştirdiği isteklerin tamamlanma planı. Bu dosyanın hazırlanması uygulamanın tamamlandığı anlamına gelmez. Oyun kodu ve denge bu planlama adımında değiştirilmedi.

Bu liste önceki inceleme belgelerinin önerilerinden önceliklidir: özellikle toplam boss sayısı **15**, zincir kapsamı nihai olarak **dört davranışın tamamıdır**. Önce iki davranışla teknik doğrulama yapılması diğer ikisinin teslimden çıkarılması anlamına gelmez.

## A. Sabit kararlar

- Ana run 50 round; aktif hasat süresi 50–60 dakika. Mağaza, seçim, duraklatma ve yükleme bu hesaba dahil değil.
- Boss takvimi: **3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50**.
- Erken ödüller: R3/6/10. Orta: R13/16/20. Güçlü: R23/26/30/33/36/40/43/46/50.
- R50 başarıdan sonra tamamla veya sınırsıza devam et seçimi. Zafer devam edildi diye geri alınmaz.
- Sınırsız boss'ları R55, R60, R65…; ayrı endless ödülleri/havuzu.
- Grid buff konumları rastgele; oyuncu tile yerini seçmez, taşımaz. Saksı yerleşiminin mevcut yetkileri korunur.
- Başlangıçta level başına 3 ayrı seçim; her seçimde adaylardan bir kart alınır. Boss teklifindeki aday sayısıyla karıştırılmaz.
- Level sınırı yok. Grid/tile geliştirmeleri tükendiğinde mevcut temel stat kartı yolu devam eder. Kalıcı stat = run boyunca, hesap genelinde değil.
- XP yatırımı hem R50 öncesinde hem endless'ta anlamlı olmalı. Kartların hangi statına gidileceğine oyuncu karar verir.
- Skill tree temel güç/erişim; level kartları parçalar; boss ödülleri büyük dönüşümler sağlar.
- Uyumlu build normal zorluk eğrisini aşabilir. Zorluk oyuncunun hasarına göre otomatik ayarlanmaz.
- Patlama, elektrik, kasırga, bumerang zincire katılabilecek. Güçlü boss ödülü zinciri açacak.
- Aynı kök doğrudan saldırının zincirinde aynı saksının aynı davranışı tekrar tetiklenemez. Başka saksıda aynı davranış çalışabilir; sonraki oyuncu saldırısı yeni köktür.
- Veriyle kazanç/bedel ödülleri: level seçim hakkına +1 veya -1 gibi değişiklikler desteklenecek. Somut ek bedeller daha sonra seçilecek.
- Önce sistem ve temsilci içerik, sonra içerik genişletme. Ürün hedefi 5–6 saatlik anlamlı keşif; süreyi bekletmeyle doldurmak değil.

## B. Tamamlandı sayma kuralları

- Bir iş yalnız belge yazıldığı veya derlendiği için kapanmaz; aşağıdaki kabul koşulu sağlanır.
- Ölçümler son kod/parametrelerle yapılır; tarih, profil, seed, nişan politikası ve kullanılan debug açıkça kaydedilir.
- Bir pakette birden çok eksen değişmek zorundaysa bağımsız kontrol kolları bulunur. Artçı, zincir, XP ve süre ilk kez aynı deneyde birleştirilmez.
- Gerçek kayıt ve seçili profil otomatik testlerden etkilenmez. Mevcut uncommitted işler korunur. Kullanıcı istemedikçe commit yapılmaz.
- İnsan hissi hedefleri bot testiyle kapanmaz. Play bulgusu gelmeden ilgili kutu açık kalır.
- Yeni karar gerektiren madde sessizce atlanmaz veya uygulayıcı tarafından kesin karar gibi icat edilmez.

## P0 — Karar ve karşılaştırma tabanı

- [x] Son kod, profil, test ve üretici durumunu kaydet; 3.6 ölçümüyle farkları listele. (2 Ekim, Bölüm 3.7.1 belgesinin 1. bölümü: oynanış kodu aynı; 38 run'ın 32'si bire bir tekrarlandı, Artçı Patlama'lı run'lar çalıştırmadan çalıştırmaya ±%10 oynuyor.)
- [ ] Yeni çalışmanın ayrı aday profil/denge setini belirle; eski referansları koru, oyun kodunu kopyalama.
- [ ] Kota dönemleri kararını bağla: boss aralıklarını mı izleyecek, bağımsız mı kalacak? Öneri: açık başlangıç/bitiş round'larıyla boss dönemlerini izlemesi; henüz onaylanmadı.
- [ ] R50 final başarı koşulunu ve ödül sırasını bağla: kota, boss hedefi, canavar bitki hedefinin ilişkisi; final ödülü bitiren/devam eden oyuncuda nasıl işlenecek?
- [ ] Ana run/sınırsız round sürelerinin kesin ilk tablosunu seç. 45/55/60/70 dağılımı yalnız adaydır.
- [ ] Ödül aşamalarının havuz politikasını seç: tamamen ayrı adaylar mı, alt aşamadan geç oyunda da teklif mi? Mevcut alınmış erken ödüller korunur.
- [ ] Kazanç/bedel için en az iki temsilci ödülün tam sözleşmesini seç; soyut sistem tek başına içerik teslimi değildir.

Kabul: bekleyen kararlar ve onaylanan başlangıç sayıları tek tabloda. Bağımsız bug işi bunları beklemez; bağımlı mekanik bekler.

## P1 — Bug ve test borcu

- [x] Patlama/elektrik başlangıç erişimini “satın alınmış” yeşili yerine açık bir “Başlangıçtan açık” gösterimiyle ayır. Gerçek satın alma seviyesi uydurma.
- [x] Ekranda bildirilen diğer ikonları gerçek node id'leriyle kontrol et; kasırga/bumerang veya eski profile erişim sızıntısı varsa düzelt. Görüntü tek başına hepsinin açık olduğunu kanıtlamaz.
- [x] Kıvılcım uyarı düzeltmesini ve gerçek saksı şansını mevcut son sürümde doğrula; 0,20→0,492'nin üç ×1,35 olduğunu metinlerde açık tut.
- [x] Boss tile çevre çizgileri ve HUD kimlik renkleri korunuyor mu kontrol et; saksı/bitki ve tile merkez rengi örtülmesin.
- [x] MechanicsVerification'ın eski opaklık beklentisini yeni tasarıma uygun anlamlı kontrolle güncelle.
- [x] HarvestBehaviorVerification ve SimpleResonanceVerification kendi test profillerini açıkça seçsin; kullanıcının seçimine bağlı geçip kalmasın.

Kabul: ilgili regresyonlar gerçek profil bağımsızlığıyla geçer; eski sayıyı gerekçesiz gevşetme yok; node durumları yeni/eski profilde doğrulanır.

Durum (2 Ekim, Bölüm 3.7.1): altı madde yapıldı ve otomatik testlerle doğrulandı; kasırga/bumerang ve eski profillerde sızıntı bulunmadı (yalnız gösterim yanıltıyordu). Play'de gözle onay bekliyor. Ayrıntı: [Bolum3-7-1-BaslangicErisimiVeRegresyonlar.md](Bolum3-7-1-BaslangicErisimiVeRegresyonlar.md).

## P2 — Açık boss takvimi

- [ ] Yeni profil için 15 tarihlik liste; eski sabit 5 round profillerine uyumluluk.
- [ ] Önizleme, aktif round, dönem sonu ve kota geçişleri açık takvimden türesin; round/5 hesabına bağımlı kalan UI ve analiz yollarını tara.
- [ ] Onaylanan kota dönemlerini uygula; her round yalnız bir döneme ait olsun, reset ve toplam skor doğru kalsın.
- [ ] R50 finalini sıradan havuz boss'undan ayrı tanımlayabil; çift boss aktivasyonu oluşmasın.
- [ ] Harita/HUD/özet doğru yaklaşan round'u yazsın; restart ve sahne değişiminde temizlensin.

Kabul: R1–50 akışında tam 15 boss; listedekiler dışında aktif boss yok; R50 atlanmaz; eski profiller korunur.

## P3 — Aşamalı ödül havuzları

- [ ] Erken/orta/güçlü ve endless havuzlarını veriyle tanımla; bu güç sınıfları tile nadirliğiyle aynı kavram olmasın.
- [ ] Onaylanan aralık ve geçiş politikasını uygula; build uygunluğu, ağırlık, stack, çakışma ve etkisiz teklif filtreleri.
- [ ] Mevcut 3 adaydan 1 ödül akışı korunur; teklif yeniden açılınca değişmez, gameplay RNG'sini tüketmez.
- [ ] Uygun ödül tükenmesi ve kapatılan özellik için geçersiz teklif yollarını işle.
- [ ] İlk temsilci içerikleri kategorilere yerleştir; erken yatırımlar sonradan anlamsızlaşmasın.

Kabul: her boss tarihi doğru havuzu kullanır; boş/tekrarlı/uygunsuz teklif oyuncuyu kilitlemez; son seçimlerden önce teklif erken hazırlanmaz.

## P4 — Kazanç/bedel ve seçim hakkı altyapısı

- [ ] Kazanç ve bedeli aynı ödül işlemi içinde uygula; kısmi uygulama ve iki kez seçme önlensin.
- [ ] Run temel seçim hakkı 3; veri etkileriyle +1→4, -1→2 desteklensin. Etkiler birleşimi, alt/üst sınır ve stack kuralları onaylanıp açıkça tanımlansın.
- [ ] Yalnız gelecekte kazanılan level'lar etkilenir; bekleyen seçim sayısı geriye dönük değişmez.
- [ ] Aday sayısı, seçim hakkı ve alınan kart sayısı ayrı alan/sayaç olsun.
- [ ] Kullanılmayan/zaten kapalı şeyi bedel gösteren ödül filtrelensin. Önceden hızını tamamlamış oyuncunun hız kilidi bedeli ayrıca değerlendirilsin.
- [ ] Kazanç, bedel ve ne zaman geçerli olacağı kart/HUD/özette gösterilsin; yeni run'da tamamı sıfırlansın.
- [ ] Onaylanmış bir +seçim ve bir -seçim temsilci ödülü oynanabilir şekilde teslim et.

Kabul: 3→4→2 gibi geçişler ve birden fazla level aynı anda kazanımı doğru; daha önce kazanılmış hak kaybı yok. Bumerang kapatma/stat dönüşümü veya hız kilidi somut onay olmadan eklenmez.

## P5 — Mevcut kırılma ödüllerini düzeltme

- [ ] Artçı yarıçapında gerçek yeni hücre eşiklerini ölç: mevcut 1,50 hücreye karşı 2,00 ve 2,25 adayları; küçük/büyük saksı ve kenar/merkez ayrı.
- [ ] Çifte Akım'ın katkısını alternatif ödülle fırsat maliyeti dahil ölç; mevcut biçimiyle havuzdan çıkarma veya menzil uyarlama kararını sonuçla bağla. Gizlice aynı isimle bambaşka mekanik verme.
- [ ] Hasat Ritmi mevcut karşılaştırma noktası kalsın; alan görseli ve hasat sayacı doğrulansın.
- [ ] Aynı stat ve yerleşimde gerçek hasat, yeni hedef, boş darbe ve skor farklarını kaydet; XP ve süre değişmesin.

Kabul: ödül neden güçlü/zayıf açıklanabilir; yalnız overkill/VFX artışı başarı sayılmaz. Yeni davranış türü eklenmez.

## P6a — Güvenli zincir temeli

- [ ] Kök doğrudan saldırı kimliği, nesil, kaynak, tekrar kümesi, hasar/şans katsayıları ve toplam bütçeyi taşıyan bağlam.
- [ ] Aynı kökte aynı saksı/davranış çifti en fazla bir başarılı zincir tetiği; başarısız denemenin tekrar hakkını tüketip tüketmeyeceği önceden kararlaştırılsın.
- [ ] Çok hücreli saksı, çoklu hedef vuruşu ve ayrı saldırıların bağlamlarını doğru ayır.
- [ ] Ölüm olayında derin iç içe çağrı yerine kuyruk; per-frame işlem bütçesi ve gecikme ölçümü.
- [ ] Run/round sonu ve kaynak yok olmasında temizlik; pooled bitki LifetimeVersion/kimlik doğrulaması.
- [ ] Nesil üst sınırı, şans/hasar düşüşü ve artçı/yankıların katılımını kararlaştır. Önceki iki nesil ve 24/8 bütçe sayıları onaylı nihai değerler değildir.

Kabul: sonsuz döngü yok; aynı ölümden çifte kaynak/XP/skor/görev yok; katsayılar yeniden çarpılmaz; bir kökün tekrar engeli diğer saldırıya sızmaz.

## P6b — Dört davranışın zincire katılması

- [ ] Patlama ve elektrikle ilk dikey dilimi çalıştır.
- [ ] Kasırga ve bumerangın gecikmiş/çoklu vuruşlarına aynı bağlamı taşı; bütün dört davranış için kaynak/hedef eşleşmelerini doğrula.
- [ ] Elektrik görsel limitinin hasarı etkilememesi korunsun; bumerang/kasırga limitleri nedeniyle kayıp tetikler ölçülsün, görünmez güç tavanı bırakılmasın. Çözüm performans bütçesiyle birlikte seçilsin.
- [ ] Zincir güçlü aşama boss ödülüyle açılır; ödül yokken eski tetik kuralı korunur.
- [ ] Hasat edilen bitkinin kendi saksısı kendi davranışlarını kullanır; davranışsız saksıya yeni yetenek verilmez.
- [ ] Zincir görselleri kaynak/yayılımı anlaşılır gösterir; bitkileri örten kalıcı görsel yığılması oluşmaz.

Kabul: dört davranışın tamamı çalışır; A→B→A aynı davranışı yeniden başlatmaz; farklı saksıdaki aynı davranış çalışabilir; sonraki doğrudan vuruş yeni zincirdir. Nihai teslim iki davranışla kapatılamaz.

## P7 — XP, tile geliştirme ve sınırsız level

- [ ] Level başına üç temel seçim korunarak XP eğrisi ve menü yükü ölçülsün. 200–270 seçim mevcut referans; hedef sonuç insan testiyle belirlenir, körlemesine 20–25 level dayatılmaz.
- [ ] Kart edinimi / etkin saksıya sahip olma / ilk gerçek tetik ayrı ölçülsün; erken davranış R5–6'ya kadar erişilebilir kalsın.
- [ ] Grid doluyken yükseltme, yükseltme tükendiğinde temel stat kartı geçişini gerçek satın alımla doğrula.
- [ ] XP yatırımı olmayan/eş bütçeli XP yatırımı kollarında geri ödeme round'u, toplam seçim, güç ve kota payı ölçülsün.
- [ ] R50 öncesi anlamlı fayda, endless'ta devam eden büyüme sağlansın; oyuncunun stat seçim yönü zorlanmasın.
- [ ] XP tablosu sonundan sonra level gereksinimi, sıfır/negatif eşik, taşma ve sonsuz level döngüsü denetlensin. “Cap yok” teknik olarak doğrulansın.

Kabul: XP yolu yalnız endless bekleyen cezalı bir yol değil; diğer yolları otomatik geçmek zorunda da değil. Temel stat kartına geçiş görünür ve erişilebilir, run dışına stat sızmaz.

## P8 — 50–60 dakika aktif run ve ana eğri

- [ ] Onaylanan süre tablosunu profil verisiyle uygula; aktif oyun/menü/duraklama ayrı sayaçlar.
- [ ] 50 round toplamı 3000–3600 saniye olsun; finalin erken bitme kuralı toplamla birlikte değerlendirilir.
- [ ] Süre değişiminin üretim, kaynak, XP, boss hedefi ve kota üzerindeki etkisini yeniden ölç. Her şeyi aynı oranla artırıp güç sıçramasını yok etme.
- [ ] Başarılı build'in R50'den önce zorluğu belirgin aşacağı pencere; ortalama build'in geçebilirliği ve kötü tercihlerin sonucu ayrı incelensin.
- [ ] Skor/kota, hasat/vuruş, davranış payı ve tarla doluluğu birlikte değerlendirilsin.

Kabul: en az iki rekabetçi ve insanın hissedebildiği güçlü yol; üçüncü/dördüncü yolların durumu açık. Güçlü yollar benzer skora zorlanmaz, farklı karar üretir. Sabit boss tarihi güç sıçramasını herkese garanti etmez.

## P9 — R50 finali ve zafer

- [ ] Önceden istenen ortadaki canavar bitki finalini mevcut uygulama durumuyla karşılaştır; eksikse ayrı alt paketle tasarla/uygula. Bu hedef iptal edilmedikçe teslim dışı sayma.
- [ ] Tek büyük hedefte alan/zincir build'lerinin nasıl karşılık bulacağı kararlaştırılsın; final onları sebepsiz işlevsiz yapmasın. Yardımcı hedef eklemek otomatik onaylı değil.
- [ ] R50 koşulları tamamlanınca zafer/kilit/görev kayıtları bir kez yazılsın.
- [ ] Tamamla / Sınırsız devam seçenekleri eşit ve açık sunulsun; bekleyen kart/boss ödül sırası P0 kararına uysun.

Kabul: iki dal da çalışır; çift tıklama/yeniden açma çifte ödül veya çifte kayıt üretmez; finalden çıkış kilitlenmez.

## P10 — Sınırsız run

- [ ] Aynı build, level, grid, ekonomi ve run etkileriyle R51'e devam; yeni run başlatılmış gibi resetlenmesin.
- [ ] R55 ve her 5 round'da boss; ayrı endless ödül havuzu ve hedef hesapları.
- [ ] R51'de ani duvar olmadan round'a bağlı can/kota büyümesi; oyuncu statlarına göre adaptif yükseltme yok.
- [ ] Round süresinin endless'ta sabit/değer sınırı kararı; sürekli uzayan bekleme yok.
- [ ] Ödül stack limitleri dolduğunda geçerli seçenekler ve temel stat ilerlemesi devam etsin; sonlu havuzda kilitlenme yok.
- [ ] Round arası gönüllü tamamla; kaybedince R50 zaferi korunur, endless sonucu ayrı yazılır.
- [ ] Uzun round numarası, can/hasar/skor/XP ve UI taşmaları için sınır politikası; sessiz sarma, NaN/Infinity veya yapay erken tavan yok.
- [ ] Uzun oturumdan çıkıp devam etme isteği karara bağlansın. Run kaydı kapsamı onaylanırsa atomik kayıt/sürüm/geri yükleme ayrıca uygulanır; henüz sessizce vaat edilmez.

Kabul: R50→51→55→60, sonraki boss döngüleri, kayıp ve gönüllü bitiriş; zafer bir kez; rekor doğru; etkiler katlanmaz. Güçlü build biraz üstünlüğünü kullanır, endless zorluğu daha sonra yetişebilir.

## P11 — Sistemden sonra içerik genişletme

- [ ] Erken/orta/güçlü/endless kataloglarını rol, koşul, kazanç, bedel, stack ve sunum açıklamalarıyla doldur.
- [ ] Kullanıcıyla içerik kapsamını sayısal kapat: hedef ödül sayısı ve farklı etki sayısı ayrı. Çok sayıda aynı yüzde varyantı farklı içerik sayılmasın.
- [ ] En az +1 ve -1 level seçim hakkı içerikleri dahil; diğer kısıtlayıcı içerikleri tek tek onayla.
- [ ] Önceden hedeflenen toplam 8 çiftçi/4 tırpanın mevcut-açık-kilitli-eksik envanterini çıkar; kalan tasarımlar için küçük içerik paketleri oluştur.
- [ ] Başlangıçların ilk satın alma, değerli boss ödülü ve güçlenme zamanı açısından farklı karar vermesini doğrula; 32 kombinasyonu otomatik 32 ayrı deneyim sayma.
- [ ] Görev/açılma sırası: farklı yolları denetme, kayıp run'da ilerleme kuralları ve ilk zafer sonrasında yeni deneme nedeni.
- [ ] Katlanan hasar başlangıcı, davranış kapatma/skora dönüştürme, hız kilidi gibi fikirleri karar kuyruğunda sonuçlandır: seçilirse uygula, reddedilirse kullanıcı kararıyla kapat. Hepsi onaylı içerik değildir.

Kabul: bu paket sırf asset altyapısı var diye kapanmaz. Seçilmiş içerik listesi gerçekten oynanabilir ve açıklanabilir olur; kalan fikirler açık/ertelenmiş/iptal durumuyla görünürdür.

## P12 — Bütünleşik teslim ve insan doğrulaması

- [ ] Aynı seed setinde normal bütçeli en az 10 run/politika; elenenler dahil. Çiftçi/tırpan çeşitleri de kapsansın.
- [ ] Kontrol karşılaştırmaları: eski taban, alan değişimi, zincir, XP, süre, son birleşik aday. Mevcut en-kalabalık nişan ile davranışa uygun nişan ayrı.
- [ ] Gerçek insan run'ı: erken davranış, ilk sıçrama, geç sıçrama, seçim yorgunluğu, toplam aktif süre, final ve isteğe bağlı endless.
- [ ] Kalabalık tarla/4 davranış/endless için profiler: üst yüzdelik kare süreleri, GC, kuyruk ve limit kayıpları.
- [ ] İlgili tüm regresyonlar ve üretici manifest kontrolleri son kodla temiz; açıklanmamış FAIL yok.
- [ ] Farklı run'ların 5–6 saatlik anlamlı keşif hedefini destekleyip desteklemediğine Play geri bildirimi; bu süre her oyuncuya garanti diye pazarlanmaz.
- [ ] Ayar kaynakları, değişiklikler, test/ölçüm yolları, bilinen sınırlamalar ve oynatma adımları son belgede.
- [ ] Kullanıcı kabulü: en az iki build'de güçlenme hissi, süre ve sonraki run isteği. Eksik hedef kalırsa TODO tamamlandı denmez.

## Uygulama sırası ve durumu

Başlangıç: **P0 kararları + P1 bağımsız düzeltmeler**. Sonra P2 → P3 → P4. P5 ve P6 ayrı deneyler; P7/P8 birikimli etkiler görüldükten sonra. P9/P10 final sözleşmesiyle birlikte. P11 sistemler oturduktan sonra, P12 nihai kabul.

Şu an P1 dışındaki bütün paketler açık; P1 2 Ekim'de Bölüm 3.7.1 ile yapıldı (Play'de gözle onay bekliyor), P0'da yalnız durum kaydı maddesi kapandı, kararlar açık. 3.6'dan zaten mevcut olan özellikler yeniden yazılacak değil; ihtiyaç duyulan yerlerde doğrulanacak/genişletilecek. Güncel kapsamın tamamlanması P12'yle kapanır; sırf ilk prototip veya altyapı teslimiyle değil.
