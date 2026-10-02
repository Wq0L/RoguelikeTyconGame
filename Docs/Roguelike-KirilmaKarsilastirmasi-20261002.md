# Roguelike güç kırılmaları: ClickerGame için karşılaştırmalı tasarım

2 Ekim 2026. Araştırma ve tasarım önerisi; kod, profil veya oyuncu kaydı değiştirilmedi. Oyun örnekleri erişilebilir topluluk wiki açıklamalarına dayanır; geliştiricilerin tasarım niyeti gibi sunulmaz. Uyarlamalar bizim çıkarımlarımızdır, dengelenmiş özellikler değildir. Bazı wiki sayfaları doğrudan açılışta erişim hatası verdi; arama sonuçlarının döndürdüğü mekanik açıklamaları kullanıldı. Güncel sürümlerin bütün ayrıntıları karşılaştırılmadı.

## 1. Kırılma ne demek?

Üç farklı sonucu ayırmak gerekir:

- Sayısal güç: daha çok hasar/skor, fakat aynı şekilde oynama.
- İşlevsel dönüşüm: daha önce yapılamayan temizleme veya tetik dizisini yapabilme.
- Hissedilen dönüşüm: oyuncunun ödülü aldığı an ile değişen sonuç arasındaki bağı görebilmesi.

3.6'da toplam skor artmış olabilir; bu tek başına diğer iki sonucu kanıtlamıyor. Kullanıcının DengeV1 run'ında yaşadığı his ile sonradan botla ölçülen KırılmaV1 aynı sürüm değildir. Son adayın insan testi henüz yok.

Başarılı roguelike'ların hepsi her run'ı sınırsız güce dönüştürmez. Buradaki hedef, ClickerGame'in seçilmiş tasarım yönü: uygun yatırım + rastgele tamamlayıcı boss ödülü ile bazı build'lerin bir süre içerik eğrisini aşması.

## 2. Risk of Rain 2 — tek saldırıdan etki ağına

### Mekanik örnek

Bazı vuruş etkileri başka etkileri tetikleyebilir. Proc coefficient, etkilerin tetiklemeye katkısını düzenler; zincir geçmişi aynı etkinin kendi üzerinden kontrolsüz tekrarını sınırlayan kurallarda kullanılır. Her etki aynı tetik kapasitesine sahip değildir. Kaynaklar: [Proc Coefficient](https://riskofrain2.wiki.gg/wiki/Proc_Coefficient), [Damage](https://riskofrain2.fandom.com/wiki/Damage).

### Neden kırılma yaratır?

Yorumumuz: ilk saldırının değeri yalnız verdiği hasar değildir; başlattığı ek saldırıların da değerini taşır. Hız, tetik şansı, etki hasarı ve yeni hedeflere erişim birbirini büyütebilir. Tek hedefi aşırı öldürmek ile başka hedefleri öldürmek aynı kazanç değildir.

### Oyuna uyarlama

En doğal karşılık: davranış hasadı, öldürülen bitkinin kendi saksısındaki davranışları denemeye izin verir. A'nın elektriği B'deki bitkiyi öldürür; B'nin patlama tile'ı varsa B'den patlama çıkar. A'nın davranışı B'ye bedavadan kopyalanmaz. Böylece rastgele grid, davranışları taşıyan bir komşuluk düzenine dönüşür.

Boss ödülü R25 sonrası bu bağlantıyı açabilir. Öncesinde doğrudan hasatla çalışan build kullanılabilir durumda kalır. Ödül gelmezse davranış yolu tamamen çökmemeli.

Yeni davranış çeşidi gerekmez; fakat zincir bağlamı ve kuyruk gerçek yeni altyapıdır. Şu anki yalnız-Direct kontrolünü true yapmak güvenli ve yeterli bir uygulama değildir.

### Alınmaması gereken taraf

Sınırsız yeniden tetikleme, sahneye sürekli daha fazla nesne koyma veya bütün davranışları aynı katsayıyla açma. Bumerang/kasırga kapasitesi hasat gücünü sessizce sınırlayabilir. Doğrudan saldırı kökü korunursa hız hâlâ önemlidir; bağımlılık tamamen kalkmaz.

## 3. Brotato — bir saldırının verimini artırmak

### Mekanik örnek

Bandana mermilere ek delme sağlar ve hasar bedeli taşır; tek hedefe uygun silahların kalabalıkta iş görmesine yardım eder. Scared Sausage yanma uygular; Snake yanmanın başka hedeflere yayılmasını sağlar. Hasar üretimi ile yayılması ayrı parçalardır. Kaynaklar: [Bandana](https://brotato.wiki.spellsandguns.com/Bandana), [Burn](https://brotato.wiki.spellsandguns.com/Burn), [Items](https://brotato.wiki.spellsandguns.com/Items).

### Neden kırılma yaratır?

Yorumumuz: aynı saldırı artık daha fazla canlı hedefte iş yapar. Kağıt üstünde daha düşük tek-hedef hasarı bile daha fazla toplam temizliğe dönüşebilir. Bu değişim için mutlaka daha fazla saldırı yapmak gerekmez.

### Oyuna uyarlama

Artçı Patlama'nın mevcut yarıçapını daha uzak komşulara ulaştırmak. Kod zaten bunu destekliyor. Tek hücrelik ayak izinde gerçek yarıçap 1,50 veya 1,68 olduğunda 8 aday hücre vardır; 2,00'da 12, 2,25'te 20. Harita sınırı ve bitki varlığı bu adayları azaltır; büyük saksı ayrıca değerlendirilir.

Önce veri ayarıyla bu yol denenmeli. İkinci elektrik dalgasının menzilini artırmak ise mevcut sabit geometri ve sekiz hedeflik uygulama sınırının birlikte değişmesini gerektirir. Yeni silah gerekmez, küçük fakat gerçek kod işi vardır.

### Risk

Alanı her build'e genel bonus olarak dağıtırsak yine tek bir hız/alan yolu baskınlaşır. Artçı erişimi patlama davranışına özgü kalmalı; bütün davranışların menzili birden büyütülmemeli. Büyük saksı ve mevcut rezonansların kazancı ayrıca ölçülmeli.

## 4. Hades — hazırlanmış iki parçayı bir ödülle bağlamak

### Mekanik örnek

Duo boon'lar uygun önkoşullarla sunulabilen, iki tanrının etkilerini birleştiren ödüllerdir. Sea Storm geri itme etkilerine yıldırım ekler. Kaynak: [Duo Boons](https://hades.fandom.com/wiki/Duo_Boons).

### Neden kırılma yaratır?

Yorumumuz: oyuncu önce ayrı ayrı işe yarayan parçalar toplar; son ödül aralarında yeni bir bağlantı kurar. Bu, bütün gücü en baştan yalnız nadir son parçaya bağlamakla aynı şey değildir.

### Oyuna uyarlama

Boss havuzundaki uygunluk koşulları zaten var. Ödül yalnız ağaç kilidine değil, tarlada çalışan davranışlara bakmalı. Örneğin elektrikten patlamaya geçiş ödülü, etkin elektrik ve patlama saksıları olan run'da anlamlıdır.

Ancak her davranış çifti için ayrı ödül eklemek ilk adım olmamalı. Dört davranış arasındaki çok sayıda özel eşleşme havuzu büyütür, açıklamaları ve testleri çoğaltır. Önce tek sınırlı zincir ödülünü veya tek yönlü bir bağlantıyı deneyip bunlardan birini seçmek daha temizdir.

### Rastgelelik kararı

Oyuncu grid buff konumunu seçmez. Uygunluk, otomatik en iyi yerleşim veya garanti ödül değildir. Mevcut build'e hiçbir etkisi olmayan teklifler elenir; geriye kalanlar arasında rastgelelik kalır. İstenen özel ödül R25'te gelmezse başka güçlenme yolları devam edebilmelidir.

## 5. Vampire Survivors — yükseltmenin çalışma şeklini değiştirmesi

### Mekanik örnek

King Bible'ın Spellbinder gerektiren evrimi Unholy Vespers'ta cooldown artık duration'a bağlı değildir. Varsayılan süre ve cooldown eşleşmesi devamlı bir halka hissi yaratır. Değişim yalnız hasar artışı değildir. Kaynak: [Unholy Vespers](https://vampire-survivors.fandom.com/wiki/Unholy_Vespers).

### Neden kırılma yaratır?

Yorumumuz: zayıf kalınan zaman aralığı ortadan kalkar. Önce aralıklı koruma veren etki, farklı bir oynama rahatlığı sağlar. Kazanım hem işlevsel hem görünürdür.

### Oyuna uyarlama

Boss ödüllerini davranışların geç-run evrimleri gibi sunabiliriz: mevcut patlama daha geniş erişir; mevcut elektrik başka saksılara yayılabilir; doğrudan saldırı Hasat Ritmi ile dönemsel geniş hasada dönüşür. Yeni bir evrim menüsü veya yeni para birimi gerekmez; mevcut boss ödül paneli yeterlidir.

Burada yalnız farklı isim ve büyük VFX eklemek yetmez. Artçı aynı boş hücreye vuruyorsa görsel olarak gelişmiş, işlevsel olarak gelişmemiş olur. Güç değişimi gerçek hedeflere yansımak zorundadır.

### Zamanlama

R25 sonu, yalnız round süreleriyle 18 dakika 45 saniye demektir; menüler hariç. İlk tatmin edici davranış anını buraya kadar ertelemek uzun olur. Öneri: R5–6 ilk çalışan davranış; R10–20 ilk yerel temizleme sıçraması; R25–30 bazı build'lerde zincir gibi ikinci dönüşüm. Bu takvim prototip hedefidir, garanti dağıtım değildir.

## 6. Slay the Spire — bedeli üretim kaynağına çevirmek

### Mekanik örnek

Corruption becerilerin enerji maliyetini kaldırırken onları tüketir; Dead Branch tüketilen kartın yerine rastgele kart üretir. Feel No Pain tüketimden savunma üretir. Böylece aynı olay birden fazla ihtiyaç için kullanılabilir. Kaynak: [Ironclad](https://slay-the-spire.fandom.com/wiki/Ironclad).

### Neden kırılma yaratır?

Yorumumuz: birbirinden ayrı sınırlamalar olan enerji, kart devamlılığı ve savunma aynı etkinlik üzerinden beslenir. Her parçanın tek başına değeri vardır; birleşmeleri daha büyük sonuç verir. Bu birleşim her elde sonsuz veya garantili kazanma değildir.

### Oyuna uyarlama

Mevcut hasat → XP → üç seçim → tile/yükseltme → hasat döngüsü zaten bu ilkenin ekonomik karşılığıdır. Yeni otomatik kart/level üretimi eklemek şu an yanlış öncelik: 3.6'da zaten 200–270 seçim oluşuyor.

Başka ileriki olasılık, üretim yatırımı güçlü hasat build'inin yakıtı hâline gelmesidir. Bunun için önce tarla gerçekten temizlenebilmelidir. Bugün dolu bekleyen tarla varken üretim artırmak zinciri besleyen yakıt değil, daha fazla bekleyen kapasite olur. Üretim-nadirlik yolu ancak hasat darboğazı aşılınca yeniden değerlendirilmelidir.

## 7. Mevcut oyunun önemli farkı: hedefler sana gelmiyor

Kalabalık düşman oyunlarındaki erişim kazancı, düşman hareketi ve kümelenmeyle desteklenir. Burada bitkiler sabit hücrelerde, saksılar arası boşluklar var, grid rastgele tile dağıtıyor. Bu nedenle başka oyunun zincir katsayısını kopyalamak çalışacağını göstermez.

Basit düşünce modeli: bir davranış darbesi ortalama k yeni canlı bitki hasat ediyor; bunların q oranı davranışlı saksıda; etkin yeniden tetik şansı p ise yaklaşık k×q×p yeni etkinlik beklenir. Bu yalnız sezgisel modeldir; bir saksıda birden çok davranış, örtüşen hedefler ve sınırlar sonucu değiştirir.

Örnek: 3 hasat × %50 davranışlı komşuluk × %40 tetik = 0,6 devam; zincir çabuk sönebilir. 5 × %80 × %50 = 2 devam; hızla dallanabilir. Bu yüzden yalnız hasar değil, yeni hedef, etkin komşuluk ve tetik şansı ölçülmelidir.

Rastgele grid korunacak. Testte oyuncunun bilmediği gelecekteki yerleşim kullanılmayacak. Botun sadece doğrudan hedef sayısını maksimize etmesi de davranış build'inin en iyi kullanımı sayılmayacak; iki nişan politikası ayrı raporlanacak.

## 8. Önerilen üç yol ve dördüncü yolun durumu

| Yol | Hazırlık | Boss'un kaldırdığı sınırlama | Maliyet/eksik yön | Uygulama |
|---|---|---|---|---|
| Geniş patlama | Patlama tile'ı, tetik şansı, yeterli davranış hasarı | İlk komşu halkayla sınırlı erişim | Seyrek/kenar yerleşiminde az hedef | Önce mevcut yarıçap verisi |
| Karma davranış zinciri | Birden çok etkin davranışlı saksı, canlı komşular | Davranış hasadının devam edememesi | Yerleşim ve öldürme eşiğine bağımlı | Yeni sınırlı zincir altyapısı |
| Hasat Ritmi | Doğrudan öldürme, hız ve alan | Her vuruşun aynı küçük erişimde kalması | Davranış hasatları sayacı doldurmaz | Mevcut sistem |
| Üretim/nadirlik | Hızlı ve değerli yeniden üretim | Başarılı temizleme sonrası yakıt azlığı | Hasat yetersizken yatırım boşa gider | Şimdilik aday, güçlü yol diye ilan edilmez |

İlk hedef üçünden en az ikisini doğrulamak. Dört yol çalışıyor demek için veri yok. Zincir herkesi aynı davranış build'ine yönlendirirse diğer yolların güç zirvesi veya zincirin fırsat maliyeti yeniden değerlendirilmeli.

## 9. Zincir için önerilen sınırlar

Ayrıntılı başlangıç taslağı: [Zincir incelemesi](Bolum3-7-ZincirVeBuildTasarimIncelemesi-20261002.md).

İlk adayda normal doğrudan tetikler değişmez. Boss ödülü sonrasında davranış hasadı yeni saksıdan en fazla iki ek nesil oluşturabilir. Yeni kaynak kendi tile ve statlarını kullanır. İlk deneyde zincirden yalnız patlama/elektrik çıkar; artçılar zincir başlatmaz, zincirler artçı üretmez. Böylece yankı ve zincirin beraber kontrolsüz büyümesi ilk ölçüme karışmaz.

Nesil şans/hasar düşüşü, aynı kök saldırıda aynı saksı-davranış çiftinin tekrarını sınırlama ve iş kuyruğu gerekir. Sayılar önceki belgede aday olarak verilmiştir; bugün doğrulanmış değiller. Katı işlem sınırları görünmez denge aracına dönüşmemeli; ne sıklıkla devreye girdikleri raporlanmalı.

Oyuncuya teknik bütçeyi açıklamak yerine anlaşılır kural verilmeli: “Davranış hasatları diğer saksıların patlama ve elektriğini azaltılmış şansla, en fazla iki adım daha tetikleyebilir.” Son metin uygulamayla birebir eşleşmeli.

## 10. Güç patlamasının görünür olması

Ödül alındığında kural değişikliğini kısa ve açık anlat. Sonraki etkide kaynak saksı ve yayılma yönü anlaşılmalı. Bir anda bütün ekranı örten sis/flash, bitki temizliğinin görülmesini engelleyebilir; yeni görsel yoğunluğu kendi başına başarı sayma.

Zinciri göstermek için kısa sıralı tepkiler kullanılabilir; fakat yalnız gösterim uğruna hasarı saniyelerce geciktirme. Hasat Ritmi'nde hazır halka zaten doğru yöndeki bir örnektir.

Can/kota eğrisi ödül alımına göre yükselmemeli. Tamamlanan build'in güçlü olduğu zaman aralığı korunmalı. Sonraki içerik bu gücü sınayabilir, fakat her kazanımı aynı anda nötrlememeli.

## 11. Uygulama ve ölçüm sırası

1. Ağaçta “başlangıçtan açık” gösterimini ayır; diğer düğümlerin yanlış açılıp açılmadığını gerçek kimlikleriyle doğrula.
2. Veri deneyi: Artçı gerçek yarıçapı için 2,00 ve 2,25 hücre adaylarını mevcut 1,50 ile karşılaştır. Bunlar kesin denge değerleri değil. Hasar ve XP'yi aynı anda değiştirme.
3. Zayıf Çifte Akım'ın fırsat maliyetini kontrol et; ilk karşılaştırma havuzundan çıkarılması adaydır, oyuncu verisinden/koddan silinmesi değil.
4. Ayrı deney: mevcut alan/XP tabanında R25 sonrası sınırlı zincir. Artçı alan artışıyla birleştirmeden önce tek etkisini ölç.
5. İki aday işe yararsa birlikte ölç; doğrudan, patlama ve karma zincir yollarını karşılaştır.
6. Sonra XP temposunu ayarla: level başına üç seçim korunurken geç level sıklığı azaltılır. İlk davranış erişimi ve kart yerleşim rastgeleliği korunur. Sabit 20–25 level sonucu garanti edilmez.
7. İnsan Play: ilk gerçek davranış anı, ilk yerel temizleme, ikinci güç dönüşümü, toplam seçim yükü ve run tekrar oynama isteği.

Kontrollü ölçümde aynı round/build/yerleşim, ödüllü-ödülsüz çiftler kullanılmalı. Aynı seed tek başına aynı dünya anlamına gelmez; tetik sayısı RNG tüketimini değiştirebilir. Tam run sonuçları kontrollü karşılaştırmanın yerine geçmez.

Başarı ölçüleri: vuruş başına gerçek hasat; davranışla ulaşılan yeni hücre; tekrar/boş hedef oranı; tetik ve hasat nesilleri; 5 saniyelik temizleme ve tarla doluluğu; ödül zamanına göre önce/sonra; seed dağılımı; seçilmeyen ödülün fırsat maliyeti; üst yüzdelik kare süresi ve kuyruk gecikmesi.

Tek ödülde ×1,5 bir tanı ölçütü olabilir, evrensel tasarım yasası değildir. Uyumlu build'in oynama biçimini değiştirmesi, farklı yatırımla ikinci bir yolun da çalışması ve bunun oyuncu tarafından fark edilmesi asıl hedeflerdir.

## 12. Karar özeti

Yeni davranış türü, yeni çiftçi, yeni boss türü veya yeni ekonomi sistemi şu problem için gerekli değil. Mevcut erişim ayarlarıyla ilk sıçrama denenebilir. Kullanıcının istediği davranıştan davranışa yayılma ise anlamlı bir geç-boss ödülü adayıdır: mevcut içerikleri bağlar ama sıfır kodla denge ayarı değildir.

Tercih: Brotato'daki erişim verimi + Hades'teki hazırlanmış build'e uygun birleşim ödülü + Risk of Rain 2'deki sınırlı tetik ağı. Vampire Survivors'tan belirgin dönüşüm anını, Slay the Spire'dan tamamlayıcı parçaların tek başına da işe yaramasını al. Beş ayrı sistemi aynı anda oyuna ekleme.
