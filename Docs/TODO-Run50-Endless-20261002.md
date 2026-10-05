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
- [ ] Yeni çalışmanın ayrı aday profil/denge setini belirle; eski referansları koru, oyun kodunu kopyalama. (Kısmi, Bölüm 3.7.2: takvim için ayrı aday profil `Run50_TakvimV1` açıldı; denge seti Kırılma V1 ile ortak. Sonraki paketlerin aday denge seti kararı açık.)
- [x] Kota dönemleri kararını bağla: boss aralıklarını mı izleyecek, bağımsız mı kalacak? (2 Ekim, Bölüm 3.7.2 kararı: kota dönemleri boss aralıklarını izler — 1–3, 4–6, 7–10, 11–13, 14–16, 17–20, 21–23, 24–26, 27–30, 31–33, 34–36, 37–40, 41–43, 44–46, 47–50. `Run50_TakvimV1`'de uygulandı.)
- [ ] R50 final başarı koşulunu ve ödül sırasını bağla: kota, boss hedefi, canavar bitki hedefinin ilişkisi; final ödülü bitiren/devam eden oyuncuda nasıl işlenecek? (Bölüm 3.7.2'de yalnız GEÇİCİ sıra uygulandı: kota + boss hasadı → bekleyen level kartları → boss ödülü → zafer. Nihai karar açık.)
- [ ] Ana run/sınırsız round sürelerinin kesin ilk tablosunu seç. 45/55/60/70 dağılımı yalnız adaydır. (Kısmi, Bölüm 3.7.8: ana run'ın ilk tablosu görev promptuyla verildi ve `Run50_AlphaDengeV1`'de uygulandı — R1–5 45 sn, R6–10 55 sn, R11–50 65 sn; toplam 3 100 sn = 51 dk 40 sn. Sınırsız mod süresi açık (P10); finalin erken bitmesi toplamı değiştirirse tablo P9'da yeniden değerlendirilecek.)
- [x] Ödül aşamalarının havuz politikasını seç: tamamen ayrı adaylar mı, alt aşamadan geç oyunda da teklif mi? Mevcut alınmış erken ödüller korunur. (2 Ekim, Bölüm 3.7.3 kararı: açılmış alt aşamalar aday kalır; mevcut aşamadan bir slot ayrılır, kalan slotlarda ağırlık çarpanı mevcut ×1, bir önceki ×0,60, iki önceki ×0,35. Ana run için uygulandı; endless politikası açık.)
- [ ] Kazanç/bedel için en az iki temsilci ödülün tam sözleşmesini seç; soyut sistem tek başına içerik teslimi değildir. (Not, Bölüm 3.7.4: iki ödülün sözleşmesi görev promptuyla verildi ve uygulandı — Bereketli Öğrenim: level başına seçim +1, doğrudan vuruş ×0,80 · Davranışa Adanış: davranış hasarı ×1,50, level başına seçim −1; ikisi karşılıklı dışlar, güçlü aşama, en çok 1 kez. Sayılar ilk test değeridir. Kutu, kullanıcı onayına bırakıldı.)

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

- [x] Yeni profil için 15 tarihlik liste; eski sabit 5 round profillerine uyumluluk.
- [x] Önizleme, aktif round, dönem sonu ve kota geçişleri açık takvimden türesin; round/5 hesabına bağımlı kalan UI ve analiz yollarını tara.
- [x] Onaylanan kota dönemlerini uygula; her round yalnız bir döneme ait olsun, reset ve toplam skor doğru kalsın.
- [x] R50 finalini sıradan havuz boss'undan ayrı tanımlayabil; çift boss aktivasyonu oluşmasın. (Yalnız tanımlama altyapısı: takvimde ayrı "final" kaydı ve tarihe atanabilen boss; test verisiyle doğrulandı. `Run50_TakvimV1`'de R50 şimdilik havuzdaki normal boss'tur; final içeriği P9'da açık.)
- [x] Harita/HUD/özet doğru yaklaşan round'u yazsın; restart ve sahne değişiminde temizlensin.

Kabul: R1–50 akışında tam 15 boss; listedekiler dışında aktif boss yok; R50 atlanmaz; eski profiller korunur.

Durum (2 Ekim, Bölüm 3.7.2): beş madde `Run50_TakvimV1` profiliyle yapıldı ve otomatik testle doğrulandı (round'lar sırayla oynanarak; skor testin eliyle verildi). Hedef tabloları eski tablodan aktarılmış geçici değerlerdir, dengelenmedi. Play'de gözle onay bekliyor. Ayrıntı: [Bolum3-7-2-BossTakvimi.md](Bolum3-7-2-BossTakvimi.md).

## P3 — Aşamalı ödül havuzları

- [x] Erken/orta/güçlü havuzlarını veriyle tanımla; bu güç sınıfları tile nadirliğiyle aynı kavram olmasın. (Bölüm 3.7.3: erken R1–11, orta R12–21, güçlü R22–50; havuz verisinde.)
- [ ] Endless havuzunu veriyle tanımla. (Bölüm 3.7.3'te yalnız ALTYAPI: havuzda ayrı, boş bir "sınırsız aşama" veri alanı var; oyun onu hiçbir round'da açmıyor. İçerik ve oynanış P10 / P11'de.)
- [x] Onaylanan aralık ve geçiş politikasını uygula; build uygunluğu, ağırlık, stack, çakışma ve etkisiz teklif filtreleri. (Ana run için.)
- [x] Mevcut 3 adaydan 1 ödül akışı korunur; teklif yeniden açılınca değişmez, gameplay RNG'sini tüketmez.
- [ ] Uygun ödül tükenmesi ve kapatılan özellik için geçersiz teklif yollarını işle. (Kısmi, Bölüm 3.7.3: tükenme yolu yapıldı — mevcut aşama tükenince alt aşama, 2 / 1 / 0 adayda kilitlenmeden devam. Bölüm 3.7.4: filtrenin yalnız ÇAKIŞAN ÖDÜL (karşılıklı dışlama grubu) ve SEÇİM HAKKI SINIRI (1–5 dışına çıkaracak ödül) tarafı tamamlandı; ikisi de teklifte ve ödül alınırken aranır. Bir özelliği kapatan / kısıtlayan ödül türü ve onun filtresi yok: genel davranış kapatma desteği yapılmadı.)
- [x] İlk temsilci içerikleri kategorilere yerleştir; erken yatırımlar sonradan anlamsızlaşmasın. (Mevcut 16 ödül yerleştirildi; erken ödüller geç aşamalarda da aday ve aynı stack'e eklenir. Dağılım ilk içerik dağılımıdır; ödüllerin gücü ölçülmedi, "güçlü" bir sınıf adıdır.)

Kabul: her boss tarihi doğru havuzu kullanır; boş/tekrarlı/uygunsuz teklif oyuncuyu kilitlemez; son seçimlerden önce teklif erken hazırlanmaz.

Durum (2 Ekim, Bölüm 3.7.3): ana run için `Run50_OdulAsamalariV1` profiliyle yapıldı ve otomatik testle doğrulandı; kabul koşulu ana run için sağlandı. Açık kalanlar: endless havuzunun içeriği, "kapatılan özellik" yolu, güçlü aşamanın geç boss'larda tükenmesi (içerik sayısı; P11) ve Play'de gözle onay. Ayrıntı: [Bolum3-7-3-AsamaliBossOdulleri.md](Bolum3-7-3-AsamaliBossOdulleri.md).

## P4 — Kazanç/bedel ve seçim hakkı altyapısı

- [x] Kazanç ve bedeli aynı ödül işlemi içinde uygula; kısmi uygulama ve iki kez seçme önlensin. (Bölüm 3.7.4: önce bütün koşullar doğrulanır; uygulama yarıda kesilirse eklenenler geri alınır.)
- [x] Run temel seçim hakkı 3; veri etkileriyle +1→4, -1→2 desteklensin. Etkiler birleşimi, alt/üst sınır ve stack kuralları onaylanıp açıkça tanımlansın. (Bölüm 3.7.4 kuralları: hak = temel + aktif değişimlerin toplamı; geçerli aralık 1–5; aralığı aşan ödül kırpılmaz, sunulmaz ve alınamaz; değişim ödülün her alışında bir kez eklenir.)
- [x] Yalnız gelecekte kazanılan level'lar etkilenir; bekleyen seçim sayısı geriye dönük değişmez.
- [x] Aday sayısı, seçim hakkı ve alınan kart sayısı ayrı alan/sayaç olsun. (Aday: ekrandaki üç kart, değişmez · hak: level başına hak, verilen hak, bekleyen hak · alınan kart: ayrı sayaç.)
- [ ] Kullanılmayan/zaten kapalı şeyi bedel gösteren ödül filtrelensin. Önceden hızını tamamlamış oyuncunun hız kilidi bedeli ayrıca değerlendirilsin. (Kısmi, Bölüm 3.7.4: kazancı kullanılamayan ödül filtreleniyor — Davranışa Adanış davranışı olan saksı yoksa sunulmaz ve alınamaz. "Zaten kapalı şeyi bedel gösterme" için bir bedel türü henüz yok: hız kilidi, davranış kapatma ve skor dönüşümü eklenmedi.)
- [x] Kazanç, bedel ve ne zaman geçerli olacağı kart/HUD/özette gösterilsin; yeni run'da tamamı sıfırlansın. (Otomatik testle ve batch görüntüsüyle; Play'de gözle onay bekliyor.)
- [x] Onaylanmış bir +seçim ve bir -seçim temsilci ödülü oynanabilir şekilde teslim et. (Bereketli Öğrenim ve Davranışa Adanış, `Run50_BedelliOdullerV1`. İlk test değerleri; dengelenmedi.)

Kabul: 3→4→2 gibi geçişler ve birden fazla level aynı anda kazanımı doğru; daha önce kazanılmış hak kaybı yok. Bumerang kapatma/stat dönüşümü veya hız kilidi somut onay olmadan eklenmez.

Durum (2 Ekim, Bölüm 3.7.4): `Run50_BedelliOdullerV1` profiliyle yapıldı ve otomatik testle doğrulandı; kabul koşulu sağlandı (3→4→2 aynı run'da test ödülleriyle denendi: gerçek iki ödül birbirini dışladığı için oyunda erişilebilir sonuçlar 2, 3 ve 4'tür). Açık kalanlar: "zaten kapalı şeyi bedel gösterme" filtresi (o türde bedel yok), denge (katsayılar ilk test değeri; XP yatırımının uzun vadeli değeri ölçülmedi — P7 / P8) ve Play'de gözle onay. Ayrıntı: [Bolum3-7-4-BedelliBossOdulleri.md](Bolum3-7-4-BedelliBossOdulleri.md).

## P5 — Mevcut kırılma ödüllerini düzeltme

- [x] **Ölçüm ön koşulu:** Artçı Patlama içeren aynı seed'li run'lar çalıştırmadan çalıştırmaya yaklaşık ±%10 oynuyor (Bölüm 3.7.1, neden bulunmadı). Bu paketteki küçük farklar yorumlanmadan önce zamanlama / RNG / ölçüm tekrarlanabilirliği incelenecek; incelenene kadar bu boyuttaki farklardan sonuç çıkarılmaz. (Bölüm 3.7.5: kök neden bulundu — yankı gecikmesi tek duyarlıklı mutlak oyun zamanıyla karşılaştırılıyordu, Artçı 6 ya da 7 kare bekliyordu. Kalan süre sayımına çevrildi; bütün profilleri etkiler, en çok bir kare. Aynı koşunun iki ayrı çalıştırması artık bayt bayt aynı.)
- [x] Artçı yarıçapında gerçek yeni hücre eşiklerini ölç: mevcut 1,50 hücreye karşı 2,00 ve 2,25 adayları; küçük/büyük saksı ve kenar/merkez ayrı. (Bölüm 3.7.5: tek hücrelik saksının çevresinde 8 / 12 / 20 hücre; 2,00 hücre seçildi, `Run50_KirilmaErisimiV1`.)
- [x] Çifte Akım'ın katkısını alternatif ödülle fırsat maliyeti dahil ölç; mevcut biçimiyle havuzdan çıkarma veya menzil uyarlama kararını sonuçla bağla. Gizlice aynı isimle bambaşka mekanik verme. (Bölüm 3.7.5: ikinci dalga çaprazda 4 hücreye uzatıldı; mekanik aynı. Ölçümden önce yazılan kuralla yeni profilin havuzunda kaldı. Birikimli run'da başka bir ödülü tutarlı geçmiyor (×1,04). Kullanıcı kararı (Bölüm 3.7.5.1): havuzda kalır; ×1,5 kırılma hedefini tutturmuş sayılmaz.)
- [x] Hasat Ritmi mevcut karşılaştırma noktası kalsın; alan görseli ve hasat sayacı doğrulansın. (Bölüm 3.7.5: karşılaştırma noktası olarak kaldı, değerleri değişmedi, aynı düzenlerde ölçüldü (×1,2–×1,4). Bölüm 3.7.5.1: sayaç ve hak kuralları gerçek saldırı yoluyla otomatik testte doğrulandı — davranış ve artçı hasadı sayılmıyor, ölen bitki iki kez sayılmıyor, eşik saldırının ortasında aşılsa da o saldırı güçlenmiyor, hak saldırı başına bir kez harcanıyor; hata yok, kod değişmedi. Alan gösterimi (hazırken altın ve ×1,6 imleç halkası, harcanınca normal) batch görüntüleriyle incelendi; insan Play onayı bekliyor.)
- [x] Aynı stat ve yerleşimde gerçek hasat, yeni hedef, boş darbe ve skor farklarını kaydet; XP ve süre değişmesin. (Bölüm 3.7.5: sabit statlı laboratuvar, 1.200 round, 10 ortak seed; ayrıca 60 tam run'lık birikimli doğrulama.)

Kabul: ödül neden güçlü/zayıf açıklanabilir; yalnız overkill/VFX artışı başarı sayılmaz. Yeni davranış türü eklenmez.

Durum (3 Ekim, Bölüm 3.7.5): `Run50_KirilmaErisimiV1` aday profiliyle yapıldı; ölçüldü ve otomatik testle doğrulandı. Artçı Patlama 2,00 hücre: güçlü patlama sinerjisinde ödülsüz kola göre medyan hasat ×1,63 / ×1,79 / ×1,62 (R23 / R30 / R40; ×1,5 hedefi tuttu); birikimli run'da eski Artçı'ya göre ×1,31, başka bir ödül alan eşe göre ×1,35 (9 / 9 seed). Çifte Akım 4 hücre: ×1,30 / ×1,33 / ×1,34 (hedef tutmadı; hasar, tetik ve gecikme yükseltilmedi); birikimli run'da eskisine göre ×1,09, başka bir ödüle göre ×1,04. Birikimli run oranları run toplamı değil, ödül alındıktan sonraki karşılaştırılabilir round'ların seed medyanıdır.

Durum (3 Ekim, Bölüm 3.7.5.1): Artçı Patlama'nın ikinci darbesine alan geri bildirimi eklendi — darbenin gerçek hedef hücrelerinden ve saksının ayak izinden kurulan kısa bir sınır çizgisi (eski profillerde kendi yarıçaplarıyla). Hasar ve oynanış değişmedi (yoğun tarla ölçümü 24 / 24 round aynı); otomatik testle ve batch görüntüleriyle incelendi. Hasat Ritmi sayacı ve gösterimi doğrulandı. Çifte Akım ×1,5 hedefini tutturmadı; kullanıcı kararıyla havuzda kalıyor. Açık kalanlar: insan Play onayı (Artçı alan çizgisi, Hasat Ritmi gösterimi, "kırılma hissi" — hiçbiri insan gözüyle onaylanmadı), ikinci nişan politikası, kota ve boss hedeflerinin yeni güce göre ayarı (P8). Ayrıntı: [Bolum3-7-5-KirilmaOdulleriErisim.md](Bolum3-7-5-KirilmaOdulleriErisim.md), [Bolum3-7-5-1-AlanGeriBildirimi.md](Bolum3-7-5-1-AlanGeriBildirimi.md).

## P6a — Güvenli zincir temeli

- [x] Kök doğrudan saldırı kimliği, nesil, kaynak, tekrar kümesi, hasar/şans katsayıları ve toplam bütçeyi taşıyan bağlam. (Bölüm 3.7.6: vuruş başına `HarvestLink` (kök, nesil, artçı mı); kök bağlamı `HarvestChain`'de — ziyaret kümesi, başarılı tetik sayısı, kökün zar akışı; referans sayımlı, havuzlu. Katsayılar ve bütçe ödül verisinde.)
- [x] Aynı kökte aynı saksı/davranış çifti en fazla bir başarılı zincir tetiği; başarısız denemenin tekrar hakkını tüketip tüketmeyeceği önceden kararlaştırılsın. (Bölüm 3.7.6: görevde kararlaştırıldı — çift kökte en çok bir kez **denenir**, başarısız deneme de hakkı tüketir; işaret zardan ve kuyruktan önce. Normal tetikle gerçekten çalışan çiftler ziyaret sayılır, normal tetiği engellemez. A → B → A testte reddediliyor.)
- [x] Çok hücreli saksı, çoklu hedef vuruşu ve ayrı saldırıların bağlamlarını doğru ayır. (Bölüm 3.7.6: çok hücreli saksı tek kimlik; aynı saldırının bütün vuruşları tek kök; iki saldırı iki kök, ziyaret sızmıyor — testli.)
- [x] Ölüm olayında derin iç içe çağrı yerine kuyruk; per-frame işlem bütçesi ve gecikme ölçümü. (Bölüm 3.7.6: ölüm olayında yalnız karar; iş sonraki karelerde, kare başına 8 (veri), kalan iş sonraki kareye. Kök başına 32 başarılı ek tetik; bütçe dolunca zar yok. Gecikme, en çok bekleyen iş ve havuz beklemesi ölçülüyor.)
- [x] Run/round sonu ve kaynak yok olmasında temizlik; pooled bitki LifetimeVersion/kimlik doğrulaması. (Bölüm 3.7.6: round sonu, run başı, menü / run sonu ve devre dışı kalmada işler düşüyor, kökler bırakılıyor; satılan saksının işi "geçersiz kaynak". Havuzdan dönen bitki eski bağlamı taşımıyor; iş bitki değil hücre + saksı tutuyor ve yürütmede sahiplik yeniden doğrulanıyor — testli.)
- [x] Nesil üst sınırı, şans/hasar düşüşü ve artçı/yankıların katılımını kararlaştır. Önceki iki nesil ve 24/8 bütçe sayıları onaylı nihai değerler değildir. (Bölüm 3.7.6: ilk prototip değerleri görevde kararlaştırıldı, nihai değil — 2 ek nesil, şans ve hasar ×0,75 / ×0,50 (birikmez), kök bütçesi 32, kare başına 8; Artçı ve Çifte Akım ikinci dalgası zincire katılmaz. Ölçüme göre değiştirilmedi.)

Kabul: sonsuz döngü yok; aynı ölümden çifte kaynak/XP/skor/görev yok; katsayılar yeniden çarpılmaz; bir kökün tekrar engeli diğer saldırıya sızmaz.

## P6b — Dört davranışın zincire katılması

- [x] Patlama ve elektrikle ilk dikey dilimi çalıştır. (Bölüm 3.7.6: dört davranışla birlikte yapıldı.)
- [x] Kasırga ve bumerangın gecikmiş/çoklu vuruşlarına aynı bağlamı taşı; bütün dört davranış için kaynak/hedef eşleşmelerini doğrula. (Bölüm 3.7.6: kasırga ve bumerang bağlamı kendileri taşıyor ve kökü yaşadıkları sürece tutuyor; 16 / 16 kaynak → hedef eşleşmesi testte çalışıyor.)
- [x] Elektrik görsel limitinin hasarı etkilememesi korunsun; bumerang/kasırga limitleri nedeniyle kayıp tetikler ölçülsün, görünmez güç tavanı bırakılmasın. Çözüm performans bütçesiyle birlikte seçilsin. (Bölüm 3.7.6: zincir elektriği normal elektrikle aynı yoldan; görsel doluysa yalnız çizim atlanıyor. Zincirin kasırga / bumerang işi havuz doluysa kaybolmuyor, yerinde bekliyor (zar ve bütçe yeniden yok; havuz büyütülmedi); bekleme ölçülüyor. Not: normal (nesil 0) kasırga / bumerang tetiği havuz doluysa eskisi gibi kayboluyor ve sayılıyor; zincir elektriği için görsel havuzu dolu ayrı test yok.)
- [x] Zincir güçlü aşama boss ödülüyle açılır; ödül yokken eski tetik kuralı korunur. (Bölüm 3.7.6: Zincir Hasat, `Run50_ZincirV1` güçlü aşaması. Ödülsüz yoğun tarla 24 / 24 round aynı, eski teklif dağılımı bayt bayt aynı.)
- [x] Hasat edilen bitkinin kendi saksısı kendi davranışlarını kullanır; davranışsız saksıya yeni yetenek verilmez. (Bölüm 3.7.6: şansı 0 olan davranış denenmez; davranışsız düzen kontrolünde zincir kolu birebir aynı.)
- [ ] Zincir görselleri kaynak/yayılımı anlaşılır gösterir; bitkileri örten kalıcı görsel yığılması oluşmaz. (Açık. Bölüm 3.7.6.2 yalnız çizgi sınıfını yeniden adlandırdı (`AreaOutlineFeedback`); yayılım görseli eklenmedi. İnsan Play testi de açık.)

Kabul: dört davranışın tamamı çalışır; A→B→A aynı davranışı yeniden başlatmaz; farklı saksıdaki aynı davranış çalışabilir; sonraki doğrudan vuruş yeni zincirdir. Nihai teslim iki davranışla kapatılamaz.

Durum (3 Ekim, Bölüm 3.7.6): `Run50_ZincirV1` aday profiliyle P6a ve P6b uygulandı; dört davranışın dördü de zincire katılıyor ve otomatik testle doğrulandı (P6a ve P6b kabul koşulları testte sağlandı). Değerler ilk prototip sınırı (2 ek nesil, ×0,75 / ×0,50, 32 / 8); ölçüme göre ayarlanmadı. Ölçüm: sabit statlı laboratuvarda toplam hasat medyanı ×1,00–×1,06 (davranışsız kontrol birebir aynı; güçlü sinerjide ×1,06–×1,26); birikimli run'da ödül sonrası hasat ×1,00 / ×1,00 / ×1,06 (patlama / elektrik / Davranış-rezonans), zincir hasadın %7–30'unu alıyor ama toplamı büyütmüyor; ödülsüz oynanış 3.7.5.1 ile birebir aynı; kare başına +0,004–0,014 ms, GC yok. Açık kalanlar: zincir görselinin kaynağı / yayılımı anlaşılır göstermesi (kaynak saksıya kısa mor çizgi var; yayılım çizilmiyor, 1×1 saksıda zayıf — insan Play onayı yok), insan Play testi ("kırılma hissi" onaylanmadı), değer ayarı. Zincirden bağımsız bulgu (P7 için): grid dolup tile'lar Sv 3 olunca her level temel stat kartı veriyor; XP kartları bileşik büyüdüğü ve level maliyeti sabit kaldığı için Davranış-rezonans seed 101'de (temel güç evresi R35'te başlıyor) iki kolda da R47 sonrası level ve hasar kontrolden çıkıyor (hasar `int` taşması). Ayrıntı: [Bolum3-7-6-DavranisZinciri.md](Bolum3-7-6-DavranisZinciri.md).

Durum (3 Ekim, Bölüm 3.7.6.1–2 — P7 öncesi sayısal güvenlik ve refactor; denge paketi değil): hasar, can, kaynak, skor ve XP dönüşümleri tek bir güvenli dönüşümden geçiyor (`NumericSafety`): int sınırını aşan değer doyuyor (eskiden doğrudan vuruş 1'e, kritik negatife, davranışlar 0 / 1'e düşüyordu), NaN / sonsuz bir kez raporlanıyor. XP `double`; level'lar kare başına en çok 256 işleniyor, round sonu kart kararı bekleyen level işi bitince veriliyor, sayaca sığmayan iş hemen durup raporlanıyor (XP silinmiyor). Zincir ayarında NaN / sonsuz reddediliyor. Seed 101: zincirli kol artık R49'da çökmüyor ve run bitiyor (level işleme teknik sınırda durmuş hâlde); zincirsiz kol P6 ile aynı ve bitiyor (92 334 kart seçimiyle). Diğer 60 run, laboratuvar, ödülsüz ölçüm ve teklifler P6 ile birebir aynı. Refactor: gözlem olayları oynanıştan ayrıldı (`ObserverEvents`; görev sayacı oynanış olayında), debug ödül girişi yansımasız, alan çizgisi sınıfı yeniden adlandırıldı, GameScene'den beş eski buton, Legacy Stats ve Sound Manager temizlendi; A ile aynı seed ve koşullarda testler, ödülsüz ölçüm, laboratuvar, seed 101 çifti ve 60 run'lık birikimli set (3 060 satır, 900 ödül teklifi) birebir aynı. Bunlar geçici teknik güvenliktir; XP geri beslemesi, seçim ekranı yükü ve nihai sayı modeli açık (P7, P10). Ayrıntı: [Bolum3-7-6-1-2-SayisalGuvenlikVeRefactor.md](Bolum3-7-6-1-2-SayisalGuvenlikVeRefactor.md).

## P7 — XP, tile geliştirme ve sınırsız level

- [x] Level başına üç temel seçim korunarak XP eğrisi ve menü yükü ölçülsün. 200–270 seçim mevcut referans; hedef sonuç insan testiyle belirlenir, körlemesine 20–25 level dayatılmaz. (Bölüm 3.7.7: ölçüldü. Level başına 3 seçim korunuyor. XP'siz kollarda medyan 222–234 seçim (204–255), XP yatırımıyla 285–374, en güçlü build'de 504–513; bir round sonunda en çok 9–27 seçim; kart başına 2 / 4 / 6 sn senaryosuyla 7–51 dk (insan ölçümü değil; aktif oyun 37,5 dk). Hedef sayı belirlenmedi: insan testi bekliyor; level sayısı dayatılmadı.)
- [x] Kart edinimi / etkin saksıya sahip olma / ilk gerçek tetik ayrı ölçülsün; erken davranış R5–6'ya kadar erişilebilir kalsın. (Bölüm 3.7.7: üç zaman ayrı ölçüldü. XP'siz ve vadesinde XP kollarında (40 run) kart R2, etkin saksı düzeni R3–R5, ilk gerçek tetik R6'ya kadar 38 / 40. XP'yi saksının önüne alan açılışta kart yine R2 ama etkin düzen R6'ya kadar 14 / 20'ye düşüyor (neden: satın alma — R6'ya kadar 2 saksı).)
- [x] Grid doluyken yükseltme, yükseltme tükendiğinde temel stat kartı geçişini gerçek satın alımla doğrula. (Bölüm 3.7.7: işlev testi + gerçek ekonomi. İlk yükseltme kartı 60 / 60 run'da R4–R5; temel güç evresine 82 yeni profil run'ının 39'u girdi — XP yatırımı olmadan R50'ye kadar ulaşılmıyor (0 / 20), küçük grid'de R7–R9'da başlıyor. Tabandaki "Atak aralığı" temel güç kartı yeni profilde sunulmuyor.)
- [x] XP yatırımı olmayan/eş bütçeli XP yatırımı kollarında geri ödeme round'u, toplam seçim, güç ve kota payı ölçülsün. (Bölüm 3.7.7: ölçüldü — iki devam yönü × üç kol × 10 seed. **Sonuç olumsuz: XP yatırımı R50'ye kadar geri ödemiyor.** XP düğümlerini yazılı vadesinde alan kol: level ×1,26–1,30, birikimli hasat ×0,94–1,01, birikimli skor ×0,81–0,84. XP'yi her şeyin önüne alan kol: level ×1,52–1,67, round hasadı ancak R44'te öne geçiyor, birikimli skor ×0,65–0,87, bir yönde 3 / 10 run boss'ta eleniyor.)
- [ ] R50 öncesi anlamlı fayda, endless'ta devam eden büyüme sağlansın; oyuncunun stat seçim yönü zorlanmasın. (Açık, Bölüm 3.7.7: **R50 öncesi anlamlı fayda sağlanmadı** (bir üstteki ölçüm). Ölçülen neden: tile sayısını level değil grid boyutu sınırlıyor (iki kolda da R20'de 49, R30–40'ta 81), fazla level yükseltme kartına gidiyor; XP düğümlerine giden altın (R50'ye kadar 9 055) skor / ekonomi düğümlerinden çıkıyor; temel güç kartı (oyuncunun stat yönünü seçtiği kart) ancak bütün tile'lar Sv 3 olunca geliyor. XP tablosunu yatırmak yatırımcının oranını değiştirmiyor, yalnız herkesin seçim sayısını artırıyor (hesap); ayar turu kullanılmadı. Çözüm P7'nin ilerleme ayarları dışında — kullanıcı kararı bekliyor (belgenin 14. bölümü). Endless akışı olmadığı için devam eden büyüme yalnız izole ilerleme testinde görüldü. Stat seçim yönü zorlanmıyor. **Bölüm 3.7.8, yeni sürelerle (45 / 55 / 65 sn):** XP düğümlerini vadesinde alan kol temel güç evresine R36–38'de giriyor (XP'siz kol hiç girmiyor); round skoru R40'tan itibaren, birikimli skor R50'de öne geçiyor (doğrudan hasar yönü: 9 çiftin 7'sinde, medyan ×1,29; karma davranış yönü: 3 / 3, ×1,35). Bedeli orta oyun: XP'siz kollar 15 run'ın 15'inde, XP'li kollar aynı seed'lerde 15'in 12'sinde kazandı (R13–16 kotası). XP artık yalnız cezalı bir yol değil, bir takas; madde yine açık — insan onayı yok, endless yok.)
- [x] XP tablosu sonundan sonra level gereksinimi, sıfır/negatif eşik, taşma ve sonsuz level döngüsü denetlensin. “Cap yok” teknik olarak doğrulansın. (Bölüm 3.7.6.1: sıfır / negatif / NaN / sonsuz maliyet, XP taşması ve sonsuz döngü kapatıldı ve testli. Bölüm 3.7.7, aday profil: tablo sonrasında maliyet büyüyor ve int sınırına kadar sonlu; "cap yok" şu anlamda doğrulandı: **test edilen aralıkta tasarımsal level sınırı yok** (6 milyar XP ve 40 000 level'lık iş bağımsız hesapla birebir). Sayı türlerinin sınırı ayrıdır ve duruyor: level sayacı int, sayaca sığmayan iş durur ve raporlanır, XP ~1e21'in üstünde durur — nihai sayı modeli P10.)
- [x] **XP geri beslemesi** (Bölüm 3.7.6 / 3.7.6.1 bulgusu): tile ve yükseltmeler tükenince her level temel güç kartı veriyor; XP kartları çarpımsal ve sınırsız birikiyor, level maliyeti tablonun sonunda (140. level, 150 000 XP) sabit kalıyor. Seed 101'de R49'da round XP'si 2,4e16 (≈ 1,6e11 level). Kartların birikme kuralı ve / veya maliyet eğrisi kararlaştırılsın. (Bölüm 3.7.7, aday profil `Run50_XPV1`: temel güç XP kartları kendi aralarında toplanıyor (iki +%5 → ×1,10) ve tablo sonrası maliyet büyüyor. Seed 101: zincirsiz kol 30 779 level / 92 334 seçim → 169 level / 504 seçim; zincirli kolda teknik durma ve doyum yok (level 172). 82 yeni profil run'ında durma, doyum, geçersiz XP yok. Eski profiller eski kuralla çalışıyor. İnsan Play onayı yok.)
- [x] **Yüksek level maliyet eğrisi:** tablo bittikten sonraki maliyet (sabit mi, büyüyen mi) kararlaştırılsın; kararın XP yatırımının geri ödemesine etkisi ölçülsün. (Bölüm 3.7.7: doğrusal kuyruk C(L) = C(N) × [1 + s × (L − N)], N = 140, s = 0,05; 0,025 ve 0,10 ile pilotta karşılaştırıldı (seed 101: level 179 / 169 / 162, skor farkı < %3). Geri ödemeye etkisi yok: 60 A/B run'ının yalnız 3'ü level 140'ı aşıyor.)
- [ ] **Seçim ekranı yükü:** aynı seed'in zincirsiz kolunda R50 sonunda 92 334 kart seçimi oluşuyor (bot yapabiliyor, oyuncu yapamaz). Çok sayıda level'ın tek round'da kazanıldığı durum için sunum kararlaştırılsın. 3.7.6.1 bunu otomatik seçim, atlama ya da level sınırıyla çözmedi; ayrıca round sonu, bekleyen level işi (kare başına 256) bitene kadar bekliyor — sayaca sığan ama çok büyük bir işte bu bekleme uzun sürer. (Kısmi, Bölüm 3.7.7: sunum uygulandı — kartlar aynı panelde arka arkaya (zaten öyleydi), panele kalan seçim sayacı, round sonuna "Seviye kazanımları hesaplanıyor" durumu; otomatik seçim / atlama / level sınırı yok. Yeni profilde on binlerce seçim oluşmuyor (en çok 546) ve ölçülen 82 run'da level bekleyişi 0 kare. **Yük çözülmüş sayılmadı:** run başına 222–513 seçim, 4 sn / kart varsayımıyla 15–34 dk; insan testi yapılmadı. **Bölüm 3.7.8, yeni sürelerle:** build yollarında medyan 339–386 seçim (XP'siz 243–250; bir genelci run'ında 759), 4 sn / kart varsayımıyla 23–26 dk; aktif oyun 37,5 dk'dan 51 dk 40 sn'ye çıktı. Seçim sayısı kendi başına hata sayılıp azaltılmadı; madde açık.)

Kabul: XP yolu yalnız endless bekleyen cezalı bir yol değil; diğer yolları otomatik geçmek zorunda da değil. Temel stat kartına geçiş görünür ve erişilebilir, run dışına stat sızmaz.

Durum (4 Ekim, Bölüm 3.7.7): `Run50_XPV1` aday profiliyle uygulandı ve ölçüldü. Kapananlar: XP geri beslemesi ve tablo sonrası maliyet (toplanan XP kartları + doğrusal kuyruk, s = 0,05), üç erişim zamanı, tile → yükseltme → temel güç geçişi, eş bütçeli XP ölçümü, menü yükü ölçümü. **Kabul koşulu sağlanmadı:** XP yolu ölçümde hâlâ cezalı bir yol — R50'ye kadar hasat ya da skor olarak geri ödemiyor (level avantajı var, güce dönüşmüyor); bu madde ve seçim ekranı yükü açık. Temel stat kartına geçiş görünür (şerit) ve run dışına stat sızmıyor (testli), ama XP yatırımı olmadan R50'ye kadar o evreye ulaşılmıyor. İnsan Play testi yapılmadı. Endless akışı, nihai sayı modeli (P10), genel kota dengesi ve zincirin kırılma gücü (P8) bu paketle tamamlanmış sayılmaz. Ayrıntı: [Bolum3-7-7-XPVeSinirsizLevel.md](Bolum3-7-7-XPVeSinirsizLevel.md).

## P8 — 50–60 dakika aktif run ve ana eğri

- [x] Onaylanan süre tablosunu profil verisiyle uygula; aktif oyun/menü/duraklama ayrı sayaçlar. (Bölüm 3.7.8, aday profil `Run50_AlphaDengeV1`: R1–5 45 sn, R6–10 55 sn, R11–50 65 sn. Süre profil verisinden çözülüyor (`RunProfileSO.roundDurations` → `RoundDurations`); `RoundManager`'da round numarasına göre dal yok; 65 sn eski 60 sn sınırına takılmıyor ve hıza dönüşmüyor; geçersiz tablo düzeltilmiyor, run başlamıyor. Eski profiller sabit süre / süre yükseltmesi yolunda. `RunClock`: aktif süre (round sayacı ve gerçek süre ayrı), level hesaplama, kart, boss ödülü, mağaza, hazırlık, duraklama ayrı sayaçlar; run sonu ekranında süre satırı. Testli (`AlphaDengeV1Verification`). Pencere odağı kaybındaki gerçek duraklama yalnız build'de görülebilir: Play kontrolü.)
- [ ] 50 round toplamı 3000–3600 saniye olsun; finalin erken bitme kuralı toplamla birlikte değerlendirilir. (Kısmi, Bölüm 3.7.8: toplam 3 100 sn; ölçümde R50'ye ulaşan her run'da round sayacı toplamı 3 100,0 sn, gerçekte geçen aktif süre 3 108 sn (round sonu yavaşlaması). Finalin erken bitme kuralı yok (P9); R50 şu an tam 65 sn. Toplam P9'da final kuralıyla birlikte yeniden değerlendirilecek.)
- [x] Süre değişiminin üretim, kaynak, XP, boss hedefi ve kota üzerindeki etkisini yeniden ölç. Her şeyi aynı oranla artırıp güç sıçramasını yok etme. (Bölüm 3.7.8: ölçüldü. Yalnız süre değişince (aynı politika ve seed) skor hızı R20–R50'de ×2,4–3,9, altın ve XP geliri ×2–4,6, R50 level'ı 102 → 140; her build R20'den itibaren Common'ı, R25–35'ten itibaren Rare'i tek vuruşta öldürüyor; skor eski hedeflerin 50–270 katı. Değerler süre oranıyla çarpılmadı: can eğrisi iki ayar turunda ölçümle seçildi (Common R50: 215 → 627; R1–10 aynı), kota ve boss hedefleri düşük uyumlu botun skorundan kuralla kuruldu; XP tablosu, ağaç ve ödül değerleri değişmedi.)
- [ ] Başarılı build'in R50'den önce zorluğu belirgin aşacağı pencere; ortalama build'in geçebilirliği ve kötü tercihlerin sonucu ayrı incelensin. (Kısmi, Bölüm 3.7.8: ölçüldü, hedef tam tutmadı. R16'yı geçen run'lar R17–20'den itibaren medyanda kotanın 2–5 katını yapıyor ve R50'ye kadar orada kalıyor (dört yolda R27'ye ulaşan 25 run'ın 24'ünde pencere). Ama önceden yazılan "10 seed'in 7'si" ölçütünü dört yoldan yalnız doğrudan hasar + alan tutuyor (8 / 10); patlama 6, elektrik 5, karma davranış 5 / 10: run'ların %40–50'si R13–R16 kotasında eleniyor (22 elenmenin 20'si orada). Genelci 8 / 10, düşük uyumlu bot 5 / 10 (geç oyunda payı 3,1–3,7 yerine 1,5), acemi 0 / 5 (R3). R17–36 kotaları düşük uyumlu botun ayakta kalanları için de 2–3 kat: üstünlüğün bir kısmı kota düşüklüğünden. İki ayar turu hakkı bitti; R11–16 ve R17–36 hedefleri kullanıcı kararı bekliyor. İnsan Play onayı yok.)
- [x] Skor/kota, hasat/vuruş, davranış payı ve tarla doluluğu birlikte değerlendirilsin. (Bölüm 3.7.8: build başına R10 / 20 / 30 / 40 / 50 tablosu — gerçek hasat / sn, skor / sn, kota ve boss payı, vuruş başına hasat ve hedef, nadirliğe göre öldürme vuruş sayısı, davranış ve zincir payı, tarla doluluğu, bitki bekleme süresi, build'in kendi gelişimi; grafik skor / sn ↔ kota ÷ dönem süresi.)
- [x] **Zincirin toplam hasadı neden az artırdığı ayrıştırılsın** (Bölüm 3.7.6): zincir ödül sonrası hasadın %7–30'unu alıyor ama toplam hasat ×1,00–×1,06. Tarla neredeyse hiç boş kalmıyor (boş tarla payı %0–5,5), yani "bitki tükendi" açıklaması desteklenmiyor. Aday açıklamalar ayrı ölçülsün: zincirin öldürdüğü bitkiyi bir sonraki doğrudan vuruş zaten öldürecek miydi; üretim hızı ve saldırı ritmi tavanı; almayan eşin aynı boss'ta aldığı ödülün fırsat maliyeti. (Bölüm 3.7.8: ayrıştırıldı. Aynı durumda zincir kapalı / açık (gerçek run kayıtlarından laboratuvar): toplam hasat ×1,07–1,11 (seçilen can eğrisi), ×1,15–1,22 (eski eğri). Zincir doğrudan vuruşun işini çalmıyor (doğrudan hasat −6 / −11; zincirin öldürdüklerinin %11–13'ünü sıradaki doğrudan saldırı öldürürdü); yer değiştiren, build'in kendi normal davranış tetikleri ve artçıları (zincir hasadının %43–47'si). Vuruşlarının %76–81'i yeni hücrelere gidiyor ama üçte biri öldürüyor; tekrar engeli denenen fırsattan fazlasını kesiyor; havuz, kuyruk ve nesil sınırı ikincil, kök bütçesi hiç dolmuyor; tarla üretim tavanında değil (kapasitenin %42–49'u). Gerçek eş run'da (zincir yerine başka ödül) fark yok: hasat ×1,03, skor ×1,01 (5 çift). İzinli aralıkta çarpanları yükseltmek +%4–9 veriyor (karar kuralı tutmadı): değerler değişmedi. Zincir bir kırılma ödülü değil — kullanıcı kararı.)

Kabul: en az iki rekabetçi ve insanın hissedebildiği güçlü yol; üçüncü/dördüncü yolların durumu açık. Güçlü yollar benzer skora zorlanmaz, farklı karar üretir. Sabit boss tarihi güç sıçramasını herkese garanti etmez.

Durum (4 Ekim, Bölüm 3.7.8): ilk alpha denge adayı `Run50_AlphaDengeV1` uygulandı ve ölçüldü (başlangıç ölçümü → iki ayar turu → son doğrulama; 206 tam run, bunların 110'u gerçek hedeflerle son doğrulama). Kapananlar: süre tablosu ve sayaçlar, süre değişiminin etkisi, birlikte değerlendirme tablosu, zincir ayrıştırması. **Kabul koşulu sağlanmadı:** geç oyunda dört yolun dördü de kotanın 3 katını yapıyor ama "10 seed'in 7'si" ölçütünü yalnız doğrudan hasar + alan tutuyor; diğer üç yol run'larının %40–50'sini R13–16 kotasında kaybediyor ve kazanma oranında düşük uyumlu bottan ayrışmıyor. En az riskli politika hâlâ hız ve alan toplayan genelci. "İnsanın hissedebildiği" kısım hiç ölçülmedi. Teknik engel yok; açık kalanlar denge tercihleri ve insan testi: R11–16 ve R17–36 hedefleri, kotanın payı, can eğrisinin dikliği, davranış hasarının doğrudan vuruşun gerisinde ölçeklenmesi, zincirin kırılma ödülü olmaması. P9'a geçilmedi. Ayrıntı: [Bolum3-7-8-AktifSureVeGucEgrisi.md](Bolum3-7-8-AktifSureVeGucEgrisi.md).

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
- [ ] Uzun round numarası, can/hasar/skor/XP ve UI taşmaları için sınır politikası; sessiz sarma, NaN/Infinity veya yapay erken tavan yok. (Kısmi, Bölüm 3.7.6.1: geçici teknik güvenlik var — sessiz sarma ve NaN kapatıldı, değerler doyuyor ve raporlanıyor. Bu, "yapay erken tavan yok" şartını karşılamaz: **endless için nihai büyük sayı modeli açık** (int hasar / can / kaynak 2,1 milyarda doyuyor; `float` stat'lar 3,4e38'de sonsuz oluyor; level sayacı int).)
- [ ] Uzun oturumdan çıkıp devam etme isteği karara bağlansın. Run kaydı kapsamı onaylanırsa atomik kayıt/sürüm/geri yükleme ayrıca uygulanır; henüz sessizce vaat edilmez.

Kabul: R50→51→55→60, sonraki boss döngüleri, kayıp ve gönüllü bitiriş; zafer bir kez; rekor doğru; etkiler katlanmaz. Güçlü build biraz üstünlüğünü kullanır, endless zorluğu daha sonra yetişebilir.

## P11 — Sistemden sonra içerik genişletme

- [ ] Erken/orta/güçlü/endless kataloglarını rol, koşul, kazanç, bedel, stack ve sunum açıklamalarıyla doldur.
- [ ] Kullanıcıyla içerik kapsamını sayısal kapat: hedef ödül sayısı ve farklı etki sayısı ayrı. Çok sayıda aynı yüzde varyantı farklı içerik sayılmasın.
- [ ] En az +1 ve -1 level seçim hakkı içerikleri dahil; diğer kısıtlayıcı içerikleri tek tek onayla. (Not, Bölüm 3.7.4: +1 ve −1 içerikleri teslim edildi; diğer kısıtlayıcı içerikler açık.)
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

## P13 — Polish öncesi mekanik ve stat test sahnesi

Karar (4 Ekim): Sistemler ve içerik tamamlandıktan sonra kullanıcı, ayrı bir test sahnesinde mekanikleri ve statların doğru hedefe uygulanmasını elle doğrulayacak. Bu paket şimdi sahne oluşturma işi değildir; P12 sonrasında, polish öncesinde uygulanır.

- [ ] Ayrı, tek adımla açılıp oynanabilen bir test sahnesi oluştur; gerçek oyun kodunu ve içerik asset'lerini kullansın, kopya mekanik/hasar hesapları kurmasın. Gerçek oyuncu kaydı, görev kilitleri ve seçili run profili etkilenmesin.
- [ ] Tekrarlanabilir seed, hazır tarla/build düzenleri, round/level senaryoları ve senaryoyu sıfırla kontrolleri sağla. Test kurulum araçları normal oyuncu akışına taşınmasın.
- [ ] Çiftçi, tırpan, skill, tile, rezonans, boss ödülü ve bedelleri tek tek açıp karşılaştırabilsin. Stat paneli tabanı, kaynakları, işlemleri ve nihai değeri; hedefin oyuncu/saksı/tile/davranış olduğunu açık göstersin.
- [ ] Önceden belirlenmiş örnek beklentilerle gerçek vuruş ve hasat sonuçlarını karşılaştır: doğrudan/kritik hasar, dört davranış, artçı/ikinci dalga, zincir nesilleri, Hasat Ritmi, saldırı/üretim aralığı, nadirlik ve tetik şansı, XP, kaynak, skor ve seçim hakkı. Beklenen sonuç yalnız uygulamadaki aynı hesabı yeniden çağırarak üretilmesin.
- [ ] Etkinin istenen hedefte uygulanması kadar diğer hedefe sızmamasını ve bir kez uygulanmasını da göster: doğrudan hasar bonusu davranışa sızmaz, saksı etkisi ilgisiz saksıya geçmez, çarpan iki kez uygulanmaz. Ödül/etki kapalı-açık karşılaştırması olsun.
- [ ] Rastgele şanslarda tek tetik üzerinden PASS verme; gerçek şansı ve çoklu denemenin tetik sayısını ayrı göster. Ölçülmeyen sonuç "test edilmedi" olarak kalsın.
- [ ] Don/Sert Kabuk/Sis önizleme-aktif-bitiş, havuz doluluğu, kart/ödül sırası, final ve R50→endless geçişi için hazır senaryolar sun. Henüz uygulanmamış mekanikler çalışıyor gibi gösterilmesin.
- [ ] Round sonu, yeni run, tekrar başlatma ve sahneden çıkışta stat/efekt/kuyruk temizliğini elle ve otomatik kontrollerle doğrula. Test sahnesinin kontrolleri de çalıştırılmış olsun.
- [ ] Türkçe kısa kullanım listesi ve senaryo bazında beklenen/gerçek sonuç sun; kullanıcının geçti/kaldı/test edilmedi notlarını alabileceği kontrol listesi hazırla. İnsan onayı otomatik test sonucu yerine yazılmasın.
- [ ] Kullanıcı sahneyi oynayıp mekanik ve stat uygulamasını doğrulasın; bulunan hatalar giderilip ilgili senaryolar tekrar denensin. Ardından polish aşamasına geçilsin.

Kabul: Kullanıcı uzun run beklemeden her eklenen mekanizmayı gerçek oyun yoluyla deneyebilir, statın nereye ve ne kadar uygulandığını görebilir. Bu sahne P12'deki gerçek run, denge ve performans doğrulamasının yerine geçmez. P13 insan kontrolü tamamlanmadan polish öncesi kabul tamamlandı denmez.

## Uygulama sırası ve durumu

Başlangıç: **P0 kararları + P1 bağımsız düzeltmeler**. Sonra P2 → P3 → P4. P5 ve P6 ayrı deneyler; P7/P8 birikimli etkiler görüldükten sonra. P9/P10 final sözleşmesiyle birlikte. P11 sistemler oturduktan sonra, P12 bütünleşik doğrulama, P13 ayrı test sahnesiyle kullanıcı kabulü; ardından polish.

P1 2 Ekim'de Bölüm 3.7.1 ile, P2 Bölüm 3.7.2 ile, P3'ün ana run kısmı Bölüm 3.7.3 ile, P4 (bir madde hariç) Bölüm 3.7.4 ile, P5 Bölüm 3.7.5 ve 3.7.5.1 ile, P6a ve P6b (görsel maddesi hariç) Bölüm 3.7.6 ile yapıldı (altısı da Play'de gözle onay bekliyor; P2'nin hedef sayıları geçici, final içeriği P9'da; P3'te endless havuzu yalnız boş veri alanı; P4'te iki ödülün katsayıları ilk test değeri, "zaten kapalı şeyi bedel gösterme" filtresi açık; P5'te Çifte Akım ×1,5 hedefini tutmadı, kullanıcı kararıyla havuzda kalıyor; P6'da zincir değerleri ilk prototip sınırı, etkisi küçük, görsel maddesi açık). P7 öncesi Bölüm 3.7.6.1–2 ile sayısal taşmalar kapatıldı ve kontrollü refactor yapıldı (geçici teknik güvenlik; denge ve içerik değişmedi). P7 Bölüm 3.7.7 ile uygulandı ve ölçüldü (aday profil `Run50_XPV1`): XP geri beslemesi ve tablo sonrası maliyet kapandı; XP yatırımının R50 öncesi geri ödemesi ölçümde sağlanmadı ve seçim ekranı yükü çözülmedi — bu iki madde açık, kullanıcı kararı ve insan testi bekliyor. P8 Bölüm 3.7.8 ile uygulandı ve ölçüldü (aday profil `Run50_AlphaDengeV1`: 51 dk 40 sn aktif süre, süre sayaçları, yeni can eğrisi ve hedefler): süre ve sayaç işi, süre etkisi ölçümü ve zincir ayrıştırması kapandı; denge adayı oynanabilir ama "en az iki yolda belirgin üstünlük penceresi" ölçütü tam tutmadı (orta oyun kotası R13–16) — bu madde ve toplam süre / final ilişkisi açık, kullanıcı kararı ve insan testi bekliyor. P0'da durum kaydı, kota dönemleri ve ödül havuzu politikası kararları kapandı, diğer kararlar açık. P9 ve sonrası açık. 3.6'dan zaten mevcut olan özellikler yeniden yazılacak değil; ihtiyaç duyulan yerlerde doğrulanacak/genişletilecek. Güncel kapsamın tamamlanması P12 bütünleşik doğrulaması ve P13 test sahnesiyle kullanıcı kabulünden sonra kapanır; sırf ilk prototip veya altyapı teslimiyle değil.
