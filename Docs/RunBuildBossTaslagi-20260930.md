# Run, fırsat maliyeti, boss, çiftçi ve tırpan taslağı

> Güncel yön: [50 round tasarım kararları](Run50-GuncelTasarimKararlari-20260930.md). Aşağıdaki eski süre/güç önerileri bu kararlara göre yeniden değerlendirilecek.

Tarih: 30 Eylül 2026. Durum: değerlendirme ve prototip taslağı; uygulama onayı veya tamamlanmış denge değildir.

> Uygulama durumu (30 Eylül): Paket A (10 round, 1–5 normal, 6–10 Don Cephesi) uygulandı; ayrıntı, ayarlar ve test sonuçları `Bolum1-KisaRunDonCephesi.md`. Ücretsiz taşıma, ödüller, çiftçi/tırpan ve diğer olaylar uygulanmadı.

## 1. Başlangıç noktası ve amaç

Kullanıcının belirttiği ve mevcut kodda görülen temel: beş round'luk segmentte kazanılan Harvest Score kotası var; en iyi gelirin %30'una dayanan Tarla Tükendi kuralı kaldırıldı. Bu taslak onu geri getirmez.

Mevcut kayıtlı sahne hâlâ 130 round. İlk kısa run adayı 40 round, gerekirse 50; süre oyun testinden sonra kesinleşir. 40 round'a geçmek yalnızca maxRounds değiştirmek değildir: kart, gelir, ağaç erişimi, bitki canı ve davranış açılışları birlikte yeniden zamanlanmalıdır.

Amaç: oyuncunun rastgele gelen tile'lara saksı yerleştirerek farklı çözümler kurması, yaklaşan hedefe hazırlanması ve kurduğu sistemin güçlü hâlini bitişten önce görmesi.

Tüm oranlar, fiyatlar ve süre hedefleri aşağıda aksi belirtilmedikçe başlangıç hipotezidir. Mevcut oyunda uygulanmış sayılmaz.

## 2. Run iskeleti

| Bölüm | İşlev |
|---|---|
| Round 1–5 | Normal segment; kota, kart ve ilk rezonansı öğretir. |
| Round 6–10 | İlk boss segmenti; tek, hafif ve önceden görülen tarla kuralı. |
| Round 11–15 | Normal segment; boss ödülünün ve yeni yatırımın karşılığını alma. |
| Round 16–20 | İkinci boss segmenti. |
| Round 21–25 | Normal segment; uzmanlaşmanın belirginleşmesi. |
| Round 26–30 | Üçüncü boss segmenti. |
| Round 31–35 | Son hazırlık ve güçlü sinerji dönemi. |
| Round 36–40 | Final boss segmenti; kotayı geçen kazanır. |

- Kota segmentin beş round'unda birikir; süre round sonunda normal biçimde biter.
- Kontrol segmentin sonunda yapılır. Tam eşitlik başarıdır.
- Fazla skor sonraki segmentin kotasına taşınmaz; toplam run skorunda kalır.
- Kotayı erken tamamlayan kalan round'larda kaynak ve XP toplamaya devam eder. Bu başarı cezalandırılmaz.
- İlk prototipte kotayı aşmaya ek ekonomik ödül yok. Sonraki yatırımları finanse etmek zaten ödüldür.
- Run toplam skoru, segment skoru ve varsa isteğe bağlı boss hedefi ayrı kavramlardır.
- HUD: segment skoru/hedef, kalan round, aktif olay ve sıradaki olay. Aniden ortaya çıkan ek kaybetme şartı yok.
- Normal segment başında sonraki boss'un bölgesi ve kuralı gösterilir. Boss başlamadan önce son düzenleme fırsatı açıkça işaretlenir.
- Boss kuralı beş round boyunca geçerlidir; 10/20/30/40. round o segmentin finalidir. Yalnızca final round'unda aktif bir ceza seçilmemiştir.

## 3. Fırsat maliyeti: üç mevcut kaynak

Yeni bir para birimi eklemeden para, kullanılabilir alan ve kalan round üzerinden seçim yaratılır.

### 3.1 Şimdi güç / sonra kazanç

Oyuncu yaklaşan kotaya karşı hasar veya skor alabilir; bunun yerine üretim, kaynak veya XP yatırımı yapabilir. Temel hasat çok zayıfsa bu seçim ortadan kalkar: hasar zorunlu vergiye dönüşür.

Örnek: aynı bütçeyle bir hasar kademesi, yeni bir saksı veya bir üretim kademesi alınabiliyor. Oyuncu hasadı rahat yapıyor ama tarla boş kalıyorsa üretim; olgun bitkiler yığılıyorsa hasat; bol hasat yapıp kotada geri kalıyorsa skor yönünü değerlendirebilmeli.

Kaynak türleri farklı olduğundan her alışveriş doğrudan rakip değildir. İlk prototipte özellikle aynı para birimini kullanan erken seçenekler arasında karşılaştırma yapılır. Üç kaynak kullanılabiliyor diye üç anlamlı tercih oluştuğu varsayılmaz.

Üretim yatırımının yaklaşık geri ödeme süresi = fiyat / round başına ek net kaynak. Ancak üretim, hasat kapasitesi doluysa ek net kaynak üretmez. Formül tek başına karar vermez; gerçek hasat farkı ölçülür.

### 3.2 Alanı büyüt / mevcut alanı işlet

- Grid büyütmek yeni alan verir; saksılar ve uygun tile gelmeden kendiliğinden gelir üretmez.
- Yeni saksı mevcut güçlü tile'ları çalıştırabilir; aynı bütçenin global güç yatırımından vazgeçilir.
- Büyük saksı daha çok tile'ı aynı rezonansa bağlar; küçük saksılar yerleşim esnekliği sağlar.
- Büyük saksı her durumda üstünse küçük saksıya rastgele bonus eklemek yerine fiyat, kapladığı alan ve rezonans erişimi birlikte incelenir.
- Oyuncunun doluluk isteği korunur. 121 hücreyi açtırıp yarısını tüm run boş bırakmak başarı ölçütü değildir. Grid büyümesi kart ve saksı ritmiyle eşleştirilir.

### 3.3 Aynı ödülden yalnızca birini al

Boss sonrası ortak bir güç bütçesine göre ayarlanmış üç seçenekten biri sunulur:

| Seçenek | Ödül | Fırsat maliyeti |
|---|---|---|
| Bakım | Oyuncunun seçtiği uygun bir tile'a bir seviye | Yeni tile veya kaynak alınmaz. |
| Yeni ekim | Normal havuzdan bir ek kart seçimi; tile yine rastgele düşer | Hemen kullanılabilir kaynak alınmaz. |
| Hasat avansı | Segment düzeyine göre kaynak paketi | Kalıcı tile gelişimi alınmaz. |

Maksimum seviyedeki tile Bakım hedefi olamaz. Hiç uygun hedef yoksa teklif yenilenir; boş ödül sunulmaz. Kaynak paketinin türü ve miktarı seçimden önce görünür. İlk boss prototipinde bu ekran dahi ertelenebilir; önce olayın eğlenceli olduğu ölçülür.

### 3.4 İlk prototipte olmayan kısıtlar

- Aynı anda hem iki davranış lisansı hem iki ağaç ucu sınırı yok.
- Seçenekleri test etmeden zorunlu dışlayan dallar eklenmez.
- Kısa run sonunda herkes aynı satın alma sırasını kullanıyorsa bir sonraki deney, tek bir anlamlı uzmanlaşma seçimi olur.
- Uzmanlaşma deneyi: bir davranış ailesinin kart ağırlığını artıran seçim; diğer davranışları tamamen yasaklamak ilk seçenek değildir.
- Ağaç tamamlama yüzdesi tek başına build çeşitliliği ölçmez. Satın alma sırası, etkin rezonanslar ve hasat kaynakları da izlenir.

## 4. Üretim seçenekleri: küçük ekim sözleşmeleri

Bu katman temel kota ve tek boss çalıştıktan sonra eklenir. Aynı anda yalnızca bir sözleşme seçilir; seçim segment başlamadan önce yapılır, beş round sürer ve bittiğinde otomatik temizlenir. Seçmeme hakkı vardır.

| Sözleşme | Kazanç | Bedel | Beklenen karar |
|---|---|---|---|
| Hızlı ekim | Seçilen bir saksıda üretim aralığı ×0,80 | O saksıdan kaynak ödülü ×0,85 | Hasat adedi/XP/skor için bugünkü gelirden vazgeçme. |
| Pazar mahsulü | Seçilen bir saksıda kaynak ödülü ×1,30 | O saksının Harvest Score'u ×0,80 | Yakın kota karşılığında geleceğe yatırım. |
| Sergilik ürün | Seçilen bir saksıda skor ×1,35 | O saksıdan kaynak ödülü ×0,80 | Bugünkü kotayı güvenceye alırken sonraki büyümeyi azaltma. |

Oranlar dengelenmiş değildir. Basit senaryoda bir seçeneğin diğerini her koşulda geçip geçmediği önce hesaplanır. Hızlı ekim saldırıyla sınırlı tarlada boşa gidebilir; öneri ekranı üretim hızının tek başına hasat garantilemediğini anlatır.

Sözleşme saksı kimliğine bağlanır: taşınırsa onunla gider, satılırsa kalan etkisi sonlanır ve ücretsiz yeniden seçilemez. Başlangıç bütçesi veya kota oyuncunun seçimine göre gizlice değiştirilmez.

Kara Tohum, yayılabilen Ayrık Otu ve kırılan saksılar ikinci içerik dalgasıdır. İlk sürümde ayrı büyüme/çoğalma/kırılma sistemleri kurulmaz.

## 5. Boss tasarımları

Boss oyuncunun çözümünü tamamen silmek yerine tarla düzenini veya hasat önceliğini değiştirir. Olay haritası, açık hücreler ve geçerli yerleşim üzerinden üretilir; kullanılmayan kapalı alan hedeflenmez.

### 5.1 Don Cephesi — ilk prototip

- Açık alanın yaklaşık dörtte birini kapsayan bir şerit seçilir; bir önceki segmentte gösterilir.
- Bölgedeki üretim noktalarının aralığı ×1,50 olur. Bütün saksı, tek hücresi şeride değdi diye cezalandırılmaz.
- Diğer bölgelerde üretim normaldir. Mevcut bitkiler silinmez, tile ve rezonans kapatılmaz.
- Kural beş round sürer; sona erince üretim aralığı eski hesaplamaya döner.
- Oyuncu güçlü üretimini dışarı taşıyabilir, bölgede kalan saksıyı güçlendirebilir veya skor verimine yatırım yapabilir.
- Aralık çarpanı nihai üretim süresine uygulanır. Don nedeniyle üretim tabanı taşması/nadirlik bonusu hesapları yanlışlıkla yeniden ödül üretmez.
- İlk boss, açık 3×3 alanın tamamını veya bütün üretim noktalarını etkileyemez.

### 5.2 Sert Kabuk — ikinci deney

- Önceden görünen bölgede boss segmenti sırasında doğan bitkiler can ×1,50 ve taban skor ×1,30 alır.
- Oranlar oyuncuyu zorunlu olarak bölgeye veya bölgeden dışarı yönlendirmeyecek şekilde ölçülür.
- İşaretli bölgeye güçlü hasat erişimi kurmak veya kolay normal bitkilere odaklanmak mümkündür.
- Boss başlarken zaten yaşayan bitkilerin canı geriye dönük değiştirilmez. Boss bitiminde yaşayan işaretli bitkiler doğumda aldıkları can/skor profilini ölünceye kadar korur; yeni bitkiler normal doğar.
- Bu kalıntı kuralı tooltip'te görünür. Testte kafa karıştırırsa daha basit alternatif: HP değiştirmek yerine yalnız boss süresince gelen hasarı azaltmak. İki yöntem aynı anda kullanılmaz.

### 5.3 Bereket Fırtınası — olumlu etki, daha yüksek iş yükü

- Bir bölgedeki üretim aralığı ×0,75; olayın kota hedefi önceden daha yüksek belirlenir.
- Ceza bir aileyi kapatmak değildir; artan mahsulü yetiştirerek hasat etmektir.
- Yoğun bölgeye alan hasadı ve uygun davranış geometrisi kurmak değerlidir.
- Kota artış oranı tahminle sabitlenmez; bu olayın aynı build'de oluşturduğu gerçek skor artışı ölçülür.
- Hasat kapasitesi yetmiyorsa hızlı üretim yardımcı olmaz; bu boss erken öğretici olarak kullanılmaz.

### 5.4 Çekirge — sonraki sürüme aday

- Her round rastgele ve habersiz tile kapatmak yerine bir sonraki round hedefleri önceden gösterilir.
- Belirli hücreler bir round boyunca tile etkisi ve rezonans sayımından çıkar; görseli açıkça değişir.
- Aynı hücre art arda hedeflenmez; küçük tarlada kapatılan hücre sayısı azalır.
- Yeniden yerleşim gerçekten mümkün değilse olay havuzuna alınmaz. İlk prototipte yoktur.

### 5.5 Dev Bitki — isteğe bağlı hasat hedefi

- Oyuncunun saksısını veya tile'ını silmez. İlk uygulamada tarla kenarında ayrı bir hedef alanı kullanır.
- Normal tarla kotayı geçmek için yeterli kalabilir; bitkiyi kesmek zorunlu ikinci kaybetme koşulu değildir.
- Dev bitkiye harcanan imleç süresi normal tarladan vazgeçiştir. Kesilince skor ve bir kez verilen açık ödül alınır.
- Tek can havuzu, tek ölüm ve tek ödül vardır; birden fazla görsel hücre ödülü çoğaltamaz.
- Mevcut davranışlar grid hedeflerine vuruyor. Ayrı hedef alanına davranışların nasıl ulaşacağı uygulanmadan önce kararlaştırılmalıdır; otomatik destek var sayılmaz.
- İlk prototipte sadece doğrudan hasatla çalışacaksa bu açıkça belirtilir. Davranış build'leri için ana kota yolu korunur. Ortak hedefleme desteği geldiğinde davranış etkileşimi ayrıca dengelenir.
- Finalde yeni, öğretilmemiş iki ceza üst üste eklenmez. Önceden görülmüş tek kuralın gelişmiş hâliyle başlanır.

## 6. Boss öncesi düzenleme

İlk deneyde her boss başlamadan önce bir ücretsiz saksı taşıma hakkı verilir; kullanılmayan hak birikmez.

- Taşıma, satış + yeniden satın alma değildir: kaynak ödemesi/iadesi yoktur.
- Geçerli yeni konum onaylanana kadar eski yer korunur. İptal hakkı tüketmez.
- Saksı boyutu, kimliği ve sözleşmesi korunur; tile'lar taşınmaz.
- Yaşayan bitkiler ve hasar durumları ilgili üretim noktalarıyla taşınır; üretim sayaçları sıfırlanmaz. Uygulama bunu koruyamıyorsa taşıma prototipi ayrı ele alınır.
- Yeni konumda tile bonusları, rezonans ve stat önbelleği yeniden hesaplanır.
- Taşıma yalnız hazırlıkta yapılır; aktif hasatta yapılmaz.

## 7. Çiftçi: ekonomik ve mekânsal kimlik

Çiftçi tarlayı nasıl kurduğunu değiştirir. İlk dilimde iki çiftçi; aşağıdakilerin kalanı sonraki aday havuzudur. Kalıcı güç satın alma yerine oynanış seçenekleri açılır.

| Çiftçi | Kimlik | Bedel / sınır | Açılma fikri |
|---|---|---|---|
| Bahçıvan | Normal kurallar; referans ekonomi ve ilk öğretici | Özel avantaj yok; diğerlerinin doğru kıyas noktası | Başlangıçta açık |
| Elektrikçi | Elektrik ailesi baştan açık; ilk kart teklifinde en az bir elektrik kartı; tile yine rastgele düşer | Elektrik bitkileri kendiliğinden kesmez; düzen kurmak gerekir. Kaynak ödülüne küçük eksi, ancak avantaj ölçüldükten sonra ayarlanır | Bir run'da Elektrik Bilgisi kurmak |
| Fidanlıkçı | Küçük saksılarda üretim avantajı | Küçük saksı başına sınırlı tile kapsamı; ayrıca büyük saksılara erişim gecikebilir | Bir segmentte birkaç farklı saksıda etkin rezonans |
| Tüccar | Kaynak kazancı artar | Başlangıç skor çarpanı düşer | Ekonomi yatırımı yaparak bir boss kotasını geçmek |
| Seçici Yetiştirici | Nadir bitkilere ağırlık verir | Üretim aralığı uzar; sert bitkiler için hasat kapasitesi gerekir | Nadir mahsulle belirli bir segment skoruna ulaşmak |

Elektrikçi Damage kartlarını tamamen yasaklamaz. Mevcut sistemde doğrudan öldürme davranış başlatır ve Damage davranış rezonansına da katkı verebilir; bunu kapatmak kimliği güçlendirmek yerine çalışmasını engelleyebilir.

Çiftçi dezavantajları sırf her satırda eksi olsun diye konmaz. Avantaj yeni bir seçim sağlıyorsa yeterli olabilir; sayısal avantajın baskınlığı ölçülür. Açılma koşulları kolay anlaşılır ve ilk başarılı sinerjilerle ulaşılır olmalı; zorunlu tekrar sayısına bağlanmaz.

## 8. Tırpan: imleç kullanımı ve hedef seçimi

İlk dilimde iki tırpan: Standart ve Dar Kesim. Başlangıçta gizli kritik veya yön kontrolü gibi ek karmaşıklık yok.

| Tırpan | Başlangıç davranışı | Güçlü olduğu yer | Bedeli |
|---|---|---|---|
| Standart | Mevcut dairesel alan ve saldırı düzeni | Öğrenme ve karma tarla | Uzmanlaşmış avantajı yok |
| Dar Kesim | Yarıçap yaklaşık ×0,75; doğrudan hasar yaklaşık ×1,40 | Sert/nadir bitkileri seçerek kesmek | Aynı anda daha az hedef |
| Geniş Orak | Yarıçap yaklaşık ×1,20; doğrudan hasar yaklaşık ×0,80 | Çok sayıda yumuşak bitki | Sert bitkilerde daha çok vuruş |
| Seri Orak | Saldırı aralığı yaklaşık ×0,80; doğrudan hasar yaklaşık ×0,80 | Daha sık hedef değiştirmek, fazla hasarı azaltmak | Büyük bitki başına düşük vuruş gücü |

Bu değerler eşit güç anlamına gelmez. Alan yarıçapının etkisi grid geometrisine ve hedef yoğunluğuna bağlıdır; saldırı tabanı da oranları bozabilir.

Tırpanın doğrudan hasar katsayısı ile davranış hasarı ayrı tutulur. İlk deneyde tırpan ailelerinin davranış hasar katsayısı 1 olur; ortak ağaç hasarı davranışları güçlendirmeye devam eder. Böylece Dar Kesim yanlışlıkla aynı anda en güçlü elektrik silahı da olmaz. Yine de doğrudan öldürme sıklığı tetik sayısını değiştireceğinden bu etki ölçülür.

Seçimler run başında kilitlenir; boss öncesi tırpan değiştirme ilk sürümde yoktur. İki çiftçi × iki tırpan ilk test matrisi için yeterlidir. 6×4 içerik sayısı başlangıç hedefi değildir.

## 9. Dengeyi kurulabilir hâle getirmek

### 9.1 Ön koşullar

- Normal başlangıç: 80 Gold; editör debug bütçesi kapalı. Güç testi ayrı profil olmalı.
- Skor tavanının segment ilerlemesini durdurması düzeltilmeli; toplam ve segment ödül hesabı yeterli sayı aralığı kullanmalı.
- Efekt havuzu dolunca elektrik hasadının iptal olması çözülmeli veya açık bir oynanış limiti olarak modellenmeli.
- Saldırı zamanlayıcısının taşan zamanı kaybetmesi giderilmeli; hedef kare hızlarında sonuç karşılaştırılmalı.
- Simülatör kaynakları kalıcı ve sürüm kontrollü olmalı. Oyun ile simülatörün aynı kota ve stat kurallarını kullandığı doğrulanmalı.

### 9.2 Ayarlama sırası

1. Tek çiftçi + tek tırpan + boss yok. Bitki canı, temel hasat, üretim ve gelir akışını kur.
2. Kart ve davranış zamanlamasını kur. İlk davranış için ilk 5–10 gerçek dakika bir test hedefidir; round sayısıyla birlikte ölçülür.
3. Skill maliyetlerini ve erişimini kısa run'a yay. Her run'da hangi yatırımlardan vazgeçildiğine bak.
4. Sabit temel ekonomi üzerinde segment kotalarını ayarla.
5. Yalnız Don Cephesi ekle; aynı seed ve profil için olaysız sonuçla karşılaştır.
6. İkinci çiftçi ve tırpanı ekle; dört kombinasyonu aynı koşullarda dene.
7. Üretim sözleşmesi ve boss ödüllerini ayrı deneyler olarak ekle.
8. Diğer boss'lar, kalıcı kilitler ve en son endless/sıralama.

Bir deneyde aynı anda kota, HP, fiyat ve kart nadirliği değiştirilmez. Önce hangi darboğazın değişmesi gerektiği seçilir. Kısa run'a ilk geçiş toplu yeniden zamanlama gerektirebilir; sonrasında sürümlenmiş sabit referans üstünde tek değişken grupları karşılaştırılır.

### 9.3 Kota eğrisi

Mevcut kodun başlangıç değeri 10, büyüme oranı 1,45. Bunlar mevcut uzun run için yazılmış değerlerdir; 40 round'a otomatik taşınmış dengeli değerler sayılmaz.

İlk kısa run deneyinde sekiz segmentin hedefi ayrı tabloda tutulabilir. Bu, her bölümü aynı üstel oranla zorlamadan erken öğretici alanı ve final sınavını ayarlamayı kolaylaştırır. Daha sonra istenirse eğriye dönüştürülür.

Segment oranı R = o segmentte kazanılan skor / o segmentin kotası. R < 1 kayıp; R = 1 tam geçiştir. İlk segmentlerin ne kadar rahat olacağı, sonraki segmentlerin ne kadar daralacağı insan testiyle seçilir; hedef kazanma yüzdesi şu anda kesinleştirilmez.

Kota değerlendirme verisi yalnız o segmente ulaşan oyuncuları içerir. Hem koşullu geçme oranı hem başlangıçtan o segmente ulaşma oranı raporlanır; erken elenen oyuncular geç oyun istatistiğinde unutulmaz.

Oyuncu güçlenince kota gizlice yükseltilmez. Kota başlangıç zorluğu, segment ve açık olay kuralına bağlıdır. İyi build kurmak gerçekten avantaj sağlamalıdır.

### 9.4 Toplanacak ölçümler

| Ölçüm | Neyi ayırt eder? |
|---|---|
| Segment skoru, kota oranı, geçti/kaldı | Hedefin zorluğu ve run'ın nerede koptuğu |
| Üretilen/hasat edilen bitki, yaşayan bitki sayısı | Üretim mi hasat kapasitesi mi sınırlıyor? |
| Hasat edilen bitki başına skor ve nadirlik dağılımı | Skor verimi ve nadirlik baskınlığı |
| Doğrudan/davranış öldürmeleri ve engellenen tetikler | Build kimliği ve teknik limit etkisi |
| Kaynak geliri, harcama, elde kalan bütçe | Yatırım fırsatı ve para birimi darboğazı |
| Açık/kullanılan hücre, etkin rezonanslar | Alan gerçekten kıt mı, yoksa boş mu? |
| İlk davranış, ilk rezonans ve ilk güçlü zincirsiz kaos anı | Eğlenceli anlara erişim |
| Hasat süresi ve hazırlıkta harcanan gerçek süre | Run uzunluğu ve karar yorgunluğu |
| Kart ekranı sayısı ve geçilen teklifler | XP'nin karar yüküne etkisi |
| Satın alma sırası ve seçilmeyen alternatifler | Fırsat maliyeti ve baskın rota |
| Oyuncunun kendi anlattığı kaybetme nedeni | Hedefin ve karşı hamlenin anlaşılması |

Simülatörde başlangıç taraması: sabit ve tekrar kullanılabilir seed kümesi, üç oyuncu profili ve dört başlangıç kombinasyonu. Örneğin 30 seed keşif için kullanılabilir; sonuçlar kesin başarı oranı diye sunulmaz. Ayrı bir seed kümesiyle kontrol edilir ve insan testiyle doğrulanır.

Simülatör mekanik tutarlılığı ve aşırı farkları bulur; boss'u adil bulmayı, imleç kullanımını veya ikinci run isteğini kanıtlamaz.

## 10. İlk uygulanabilir paket ve kabul koşulları

### Paket A: 10 round'luk dikey dilim

- Mevcut kota sistemini kullan; 1–5 normal, 6–10 Don Cephesi.
- Tek başlangıç ve mevcut standart tırpan.
- Boss haritası normal segment başında gösterilir; boss öncesi hazırlık açıkça belirtilir.
- Ücretsiz taşıma bağımsız olarak güvenli çalışmadan olay dengesi ona mecbur edilmez.
- Boss ödülü, tohum, dünya sıralaması, lisans ve meta güç yok.
- Amaç: bir normal segmentte kurulan tarlanın sonraki sınava göre değiştirilmeye değer olup olmadığını görmek.

Kabul koşulları:

1. Kota eşitliğinde geçiş, eksik skorda kayıp ve yeni segmentte sıfırlama doğru.
2. Don yalnız hedef üretim noktalarını etkiliyor; bitişte veya run kapanışında hiçbir geçici etki kalmıyor.
3. Mevcut bitkiler, kaynaklar ve tile'lar boss girişinde kaybolmuyor.
4. Boss en az bir uygulanabilir karşı hamle bırakıyor; bütün tarlayı kapatmıyor.
5. Üç ayrı yatırım yönü test ediliyor: doğrudan güç, üretim, skor. Hepsinin aynı başarı oranı şart değil; birinin her durumda doğru olduğu varsayılmıyor.
6. Oyuncu yardım almadan hedefi ve aktif kuralı açıklayabiliyor.

### Paket B: kısa run ve başlangıç çeşitliliği

- Paket A çalıştıktan sonra 40 round'a yay; olay sırası ilk testlerde sabit olsun.
- İki çiftçi, iki tırpan; yalnız bu dört kombinasyon dengelenir.
- Boss sonrası tek seçim ödülü eklenir.
- Üretim sözleşmeleri ancak temel ekonomi oturduktan sonra ayrı sürümde denenir.

## 11. Teknik uygulama sınırları

- RoundManager zaman ve geçişleri yönetir; bütün boss kuralları içine eklenmez.
- Olay tanımı veri olarak bölge, başlangıç/bitiş, etki ve önizlemeyi taşır. Olay yürütücüsü bunları uygular ve temizler.
- Geçici modifier'lar kaynak kimliği taşır; olay sonu, run sonu ve yeniden başlatmada aynı temizleme yolu kullanılır.
- Çiftçi/tırpan başlangıç verisi ayrı tanımlanır; ortak run ayarını gizlice değiştirmez.
- Kart teklif üretimi UI'dan ayrılır; çiftçi havuzu ve boss ödülü aynı kural katmanına bağlanır.
- Saksı taşıma tek işlem olarak doğrulanır; geçersiz hedef yarım taşınmış durum yaratmaz.
- Bölgesel etki üretim noktası mı saksı mı hedefliyor açıkça tanımlanır; stat önbelleği ve rezonans buna göre güncellenir.
- Mevcut Unity enum değerleri yeniden sıralanmaz; asset kimlikleri korunur.

## 12. Oyun testiyle kararlaştırılacaklar

- Ana run 40 mı 50 round mu; hazırlık süreleri dahil gerçek oturum süresi ne?
- Boss için bir ücretsiz taşıma yeterli mi; taşıma oyunu gereksiz yeniden dizmeye mi çeviriyor?
- Üretim sözleşmesi anlamlı mı, yoksa normal ağaç seçimlerini tekrar mı ediyor?
- Elektrikçi avantajına gerçekten kaynak cezası gerekiyor mu?
- Tırpan farkları imleç kullanımını değiştiriyor mu, yoksa yalnız sayı değiştiriyor mu?
- Final kotası yeterli zirve mi; dev bitki gerçekten gerekli mi?

Bu belge önerilen başlangıç modelidir. Denge veya eğlence doğrulanmadan içerik sayısını artırmak hedef değildir.
