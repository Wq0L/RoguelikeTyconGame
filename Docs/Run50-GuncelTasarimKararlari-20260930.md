# 50 round: güncel tasarım yönü

30 Eylül 2026. Elektrik geri dönüşü uygulanmıştır; aşağıdaki denge ve içerik planı henüz uygulanmamıştır. Önceki 40/130 round ve yük elektriği önerilerinin yerine bu yön esas alınır.

## Korunacak oyun hissi

Nadirlik aurasına bakıp değerli bitkiye vurmak, tile tepkimeleriyle geniş alan hasat etmek ve tamamlanmış build ile endgame'de hızlanmak hedef deneyimdir. Tek vuruşla hasat yasaklanmayacak; çok erken bütün hedeflerin tek vuruşa düşüp hasar yatırımını değersizleştirmesi giderilecek.

Yük biriktiren elektrik geri alındı. Elektrik yeniden doğrudan hasatta mevcut şans hesabıyla tetiklenir. Oyuncu vuruşundan yük boşaltma ve otomatik şarj yöneticisi kurulumu kaldırıldı. Tarihsel deney kodu/verisi eski ölçümlerin derlenebilmesi için korunur fakat yük modu çalışmaz. Deney profili ve menüsü süre deneyini korur. Eski SpeedElectricVerification artık geçerli bir kabul testi değildir ve açık hata verir.

## Gücün dağılımı

Hedef başarılı run 50 round'dur; henüz varsayılan profil değiştirilmedi. Beş round'luk segmentlerle 10 segment olur. Boss sıklığı ve toplam gerçek oyun süresi ayrıca ölçülecek: round sayısı tek başına run süresi değildir.

Skill tree erken hayatta kalma ve ilk build'i kurma gücünü verir: temel hasar/hız, üretim, davranışlara erişim, şans ve reroll. Mevcut düğümler tek tek fiyat, açılma round'u ve toplam çarpan açısından çıkarılmadan silinmez. Özellikle geç oyundaki büyük küresel çarpanlar, karşılıkları boss ödüllerine yerleştirilip erişimleri doğrulanınca azaltılır. Güç aktarımı gerçekleşmeden ağaç nerflenmez.

Boss ödülleri run'a özgü zirve gücünü tamamlar. Ödül seçenekleri doğrudan hasat, davranış/rezonans ve ekonomi/XP yönlerini desteklemeli; her ödül bütün yönleri aynı anda büyütmemeli. Son boss'tan sonra kullanılacak round kalmıyorsa gelecekte güç veren ödül gösterilmez.

## Can eğrisi

Başlangıç modeli: Can(round, nadirlik) = TemelCan(round) × [1 + k × nadirlikBasamağı]. Basamak common için 0'dan başlar. k denge ölçümüyle belirlenir; bu bir nihai sayı kararı değildir. Böylece nadirlikler arasındaki can artışı doğrusal kalır.

TemelCan monoton, yumuşak bir eğri olmalı. Her round katlanarak büyüyen kontrolsüz üstel artıştan ve segment başında ani sıçramalardan kaçınılır. Yuvarlama ve öldürmek için gereken vuruş sayısı yine eşikler yaratır; eğrinin düzgün görünmesi tek başına yeterli değildir.

Can oyuncunun anlık gücüne göre otomatik yükselmez. Aynı round ve nadirlik aynı taban canı verir; iyi build bu sabit eğriyi aşabilir. Erken oyunda tek atma oranı, round/nadirlik bazında vuruş sayısı ve hasada kadar geçen süre ölçülür. Endgame'de yatırımını tamamlayan build'in yaygın bitkileri teklemesine izin verilir.

## Hız, hasar ve büyük saksı

Can büyüdükçe yalnız hıza yatırım yapan oyuncu daha fazla vuruşa ihtiyaç duyar. Bunun fırsat maliyeti, aynı bütçeyle hasar veya rezonans yatırımı yapamamaktır. Bu sonucu yalnız can artışı garanti etmez: hız ve hasarın fiyat/verim ilişkisi de ölçülmelidir.

Büyük saksının mevcut daha fazla tile kapsama ve rezonans potansiyeli kullanılır; sırf büyük olduğu veya oyuncu hızlı vurduğu için gizli hasar cezası eklenmez. Daha çok alan ve para bağlama karşılığında davranış sinerjisi kazanmak görünür tercih olmalı. Yeni saksı türleri bu kapsamda değildir.

## XP ve kart deneyi

Erken XP yatırımı daha geç güçlenmeye, erken saldırı yatırımı ilk kotaları daha rahat geçmeye hizmet etmeli. XP yatırımının geri ödeme zamanı ve kota riski ölçülür; XP her koşulda zorunlu olmamalı.

Kullanıcının üç kart önerisi: bir level'da üç ayrı kartı seçip almak. Üç seçenekten birini seçmekle karıştırılmamalı. Önce aynı ekonomi ve seed'lerle level başına 1 alım / 3 alım karşılaştırılır. Herkese üç alım vermek hız build'ini de güçlendirir; XP tercihinin otomatik olarak anlamlı olacağını varsaymayız. Kart havuzu tükenmesi, kart sınırları ve art arda seçimlerin hazırlık süresi ölçülür. Bu deney netleşmeden bütün kota ve can değerleri buna göre ayarlanmaz.

## Boss ve build doğrulaması

Boss mevcut build'in gerçek darboğazına dokunmalı. Üretim fazlası olan düzende üretimi yavaşlatan Don Cephesi etkisiz kalabilir; salt çarpanı büyütmek çözüm sayılmaz. Kural önceden görünür olmalı, oyuncu yerleşim/yatırım/hedef seçimiyle karşılık verebilmeli ve uzmanlaştığı davranış bütünüyle kapatılmamalı.

Karşılaştırılacak adaylar: hız + geniş hasat; doğrudan hasar + değerli bitki hedefleme; davranış + rezonans; erken XP yatırımıyla geç açılan karma build. Bunlar henüz kanıtlanmış dört kazanan build değildir. Her biri aynı başlangıç bütçesi, gerçek erişim koşulları ve birden fazla seed ile sınanır. Tam ağaç yalnız uç durum kontrolüdür; her round'un normal referansı değildir.

Başarı ölçümleri: segment kota payı, başarısızlık round'u, aynı maliyette alternatif yatırım getirisi, tek atma oranı, doğrudan/davranış hasat payı, XP yatırımının geri dönüşü, boss açık/kapalı skor farkı ve ödülün sonraki segmentlere katkısı. İnsan oyun testi hedef seçme hissini ve endgame tatminini ayrıca değerlendirir.

## Uygulama sırası

1. Eski elektrik hissine dönüş ve mevcut kota/uzmanlaşma akışının regresyon kontrolü.
2. Mevcut düğüm/gelir/kart erişim haritası; ayrı 50 round deneme profili, gerçekçi round 10/20/30/40/50 referansları.
3. Can eğrisi ile temel ağaç gücünü birlikte ayarlama; kaldırılan güç için boss ödülü bütçesi ve erişimi.
4. XP ve bir level'da üç kart deneyini ayrı karşılaştırma.
5. Boss karşı hamlelerini ve dört build adayını sınama; bundan sonra kotaları ayarlama.

Bir aşamada can, hız, kart sayısı ve boss baskısını birlikte değiştirmeyerek sonuçların nedenini izlenebilir tut.
