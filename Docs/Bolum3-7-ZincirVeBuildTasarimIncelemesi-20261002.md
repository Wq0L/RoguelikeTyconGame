# Build kırılması ve boss zincir ödülü — tasarım incelemesi

2 Ekim 2026. Bu belge öneridir; oynanış kodu, profil ve oyuncu kaydı değiştirilmedi. Mevcut kod ve 3.6 ölçümleri incelendi; yeni Unity koşusu yapılmadı. Aşağıdaki prototip sayıları dengelenmiş sonuç değildir.

## 1. Yeni bug notu: ağaçta başlangıçtan açık davranışlar

Oyuncu davranış düğümlerinin açık göründüğünü bildirdi. Ekran görüntüsünden bütün ikonların kimliği kesinleşmiyor.

İnceleme anında RunProfileSelection.asset, Run50_KirilmaV1.asset GUID'sini gösteriyor. Bu profil patlama ve elektrik kilitlerini başlangıçtan açıyor; 3.6 tasarımının kasıtlı sonucu. SkillTreeManager.IsGrantedByProfile yalnız stat etkisi olmayan ilgili kilit düğümlerini satın alınamaz yapıyor. Bu iki kartın havuzda olması tek başına hata değil.

Takip: başlangıçtan verilen erişimin satın alınmış/ilerleyerek kazanılmış gibi sunulması kafa karıştırıyor. Ayrı bir “Başlangıçtan açık” görünümü kullanılmalı; oyuncunun yaptığı yatırım gibi gösterilmemeli. Görselde başka davranış düğümleri de açılmışsa gerçek node kimlikleriyle ayrıca doğrulanmalı. Eski profillere sızıntı bu görüntüyle kanıtlanmış değil.

Erken erişim geri alınmadan önce iki ayrı soru korunmalı: kartın havuzda olması ve yerleşmiş saksıda gerçekten çalışması. R2 kart alımı tek başına erken davranış hissi kanıtı değil; ilk tetik R3–6 olarak ölçülmüş.

## 2. Havuzları ayırmak

- Level kart havuzu: davranış ve diğer tile'ları sağlar. Üç seçim hakkı kart sayısını artırıyor. Patlama/elektrik erken slotu ilk davranış alınana kadar var.
- Boss ödül havuzu: mevcut build'in gücünü ve kuralını değiştirir. Zayıf Çifte Akım iyi bir alternatifin yerine seçilince fırsat maliyeti yaratır.
- Görsel/nesne havuzu: performans altyapısıdır. Elektrikte dolu görsel havuzu hasarı atlamaz; bumerangda eşzamanlı sınır tetik kaybına yol açabilir.

Patlama ve elektriğin aynı kart havuzunda olması tek başına problem değildir. Karışık davranışlar zincir için yararlı bile olabilir. Sorun, uygun tile/saksı/nişan birleşiminin kurulamaması veya gelen boss ödülünün gerçek hedef erişimini artırmamasıdır. Kart havuzu seyrelmesini kanıtlamak için teklif, seçim, yerleşmiş etkin saksı ve ilk tetik ayrı sayılmalı.

## 3. Başka oyunlardan alınabilecek ilkeler

- Risk of Rain 2: etkilerin başka etkileri başlatması ve zincirin aynı etkileri kontrolsüz tekrarlamamasını sağlayan kurallar. Oyunun katsayılarını aynen taşımak yerine köken ve tekrar takibi fikri kullanılmalı. Kaynak: https://riskofrain2.fandom.com/wiki/Damage ve https://riskofrain2.wiki.gg/wiki/Proc_Coefficient
- Brotato: yanma ile yanmanın başka hedeflere yayılması ayrı yatırımlardır. Mevcut etkinin erişimini genişletmek, aynı hedefe daha çok hasardan farklı bir güç eksenidir. Kaynak: https://brotato.wiki.spellsandguns.com/Burn
- Hades: Sea Storm, geri itme etkisini yıldırımla birleştirir. Buradan alınabilecek fikir iki sistemi belirli bir ödülle bağlamaktır; bütün etkilerin her şeyi tetiklemesi gerekmez. Kaynak: https://hades.fandom.com/wiki/Poseidon/Boons_(Hades)

Bunlar topluluk wiki'lerindeki mekanik açıklamalarıdır; oyunumuz için sonuç çıkarımı ve öneriler bu incelemeye aittir.

## 4. Mevcut oyunda zincirin kapandığı yer

PlantHealth.Die → PlantSpawner.OnPlantDied → PlanterBrain.TriggerHarvestBehaviors → IsValidHarvestSource → DamageTypeRules.CanTriggerBehaviors.

Son kontrol yalnız Direct için true döndürüyor. Davranış hasadı kaynak/XP/skor üretebilir ama sonraki davranışı başlatamaz. Hasat edilen bitkinin sahibi olan saksı yeni davranış kaynağıdır; ilk saldıran saksının tile'ları kopyalanmaz.

Önerilen örnek: doğrudan hasat A saksısında elektrik üretir → elektrik B saksısında bitki öldürür → B'nin patlama tile'ı varsa kendi şansıyla patlama üretir → patlama C'de bir bitkiyi hasat eder. C'nin davranışı yoksa zincir doğal olarak biter.

Bu, rastgele grid kararını korur. Oyuncu tile konumunu seçmez; oluşmuş komşuluklar ve saksı yerleşimi önem kazanır. Büyük saksının içindeki her hücreyi ayrı sınırsız kaynak saymamak gerekir.

## 5. Üç yaklaşım ve tercih

1. Yalnız sayı/alan: mevcut Artçı yarıçapını gerçek yeni hücre eşiklerini aşacak şekilde ayarlamak. En küçük değişiklik; patlama ve ritimle iki güçlü yol hedeflenebilir.
2. Belirli davranış dönüşümü: örneğin elektrik hasadı patlamayı açar. Daha kolay anlaşılır, fakat iki davranışı kuramamış build'de ölü ödül riski yüksek.
3. Geç boss ödülüyle sınırlı zincir: mevcut davranışları birbirine bağlar, yeni silah veya tile gerektirmez. Gerçek yeni oynanış kuralıdır; yalnız denge parametresi değildir.

Oyuncunun istediği R25–30 dönüşüm anı için 3. yaklaşım uygun adaydır. Önce 1. yaklaşımın alan ayarıyla aynı anda karıştırılmadan ayrı kolda ölçülmeli. Zincir eklemek henüz zorunlu olduğu kanıtlanmış bir ihtiyaç değil; açık bir tasarım tercihi olur.

## 6. Önerilen ilk zincir prototipi

Ödül adı çalışma adı: Zincir Hasat. R25 boss'undan itibaren uygun ödül havuzuna girer, bir kez alınır. R30 kesin verilir gibi bir garanti yok. Uygunluk: tarlada etkin patlama/elektrik şansı taşıyan en az iki farklı saksı. Bu koşul iyi komşuluğu garanti etmez; nişan ve yerleşim hâlâ önemlidir.

İlk prototipte zincirin ürettiği yeni davranışlar yalnız patlama ve elektrik. Kasırga ve bumerangın mevcut normal tetikleri korunur; zincirden ek kasırga/bumerang üretmek kapsam dışıdır. Bu, eşzamanlı nesne limitlerinin ilk deneyi belirsizleştirmesini azaltır.

- Ödül yokken mevcut yalnız-doğrudan kuralı aynen kalır.
- Doğrudan saldırıdan çıkan normal davranış nesil 0'dır.
- Normal davranışın öldürdüğü bitki kendi saksısında nesil 1 davranış deneyebilir.
- Nesil 1'in hasadı nesil 2 deneyebilir; nesil 2 hasat ve ödül verir ama yeni davranış üretmez.
- Nesil 1 tetik şansı: mevcut etkin saksı şansı ×0,65. Nesil 2: aynı şans ×0,40. Sonuç 0–1. Normal doğrudan tetik değişmez.
- Zincir hasarı ilgili yeni kaynak saksının normal davranış hesabından üretilir. Nesil 1 ×0,75, nesil 2 ×0,75². İlk kaynağın hasarı ve bonusları tekrar tekrar aktarılmaz.
- Aynı kök oyuncu saldırısında aynı saksı/davranış çifti zincir yoluyla en fazla bir kez denenir. Başarısız deneme de kaydedilir; çok hücreli saksı tekrar tekrar zar atarak sınırı aşamaz.
- Tekrar takibi kök saldırıya aittir; bütün run boyunca veya bütün zincirlerde ortak yasak değildir. Başka saksıda aynı davranış çalışabilir, böylece saf patlama yolu da mümkün kalır.
- Artçı ve ikinci elektrik dalgası ilk prototipte zincir başlatmaz. Zincirden üretilen davranışlar da ek artçı/dalga planlamaz. Ödül metni bu sınırı açıklar.
- Hasat Ritmi sayacı yalnız gerçek doğrudan hasadı saymaya devam eder.
- Canlı bitki başına XP, skor, kaynak ve görev bildirimi tek kez; çifte ödül mekanizması ayrıca mevcut kuralıyla çalışır.

İşlem sınırları başlangıç deneyi: kök oyuncu saldırısı başına en fazla 24 zincir davranışı; kare başına en fazla 8 uygulama. Kare sınırına gelen işler silinmez, kuyruğa bırakılır; kök bütçe aşımı sayılır. Bu sınırlar nihai değildir. Sık devreye giriyorsa build gizlice kırpılmamalı; sonuç ve performans birlikte değerlendirilmelidir.

## 7. Neden bir bool açmak yetmez?

Şu an ölüm olayı içinde davranış hasarı hemen başka ölümler yaratabilir. Kontrolü koşulsuz açmak iç içe çağrı ve çok büyük tetik ağacı oluşturabilir. Sadece IsExecuting gibi global bool ile engellemek de izin vermek istediğimiz zinciri bütünüyle susturur.

Gereken küçük ama gerçek altyapı: kök saldırı kimliği, nesil, kaynak türü (normal/yankı/zincir), ziyaret edilmiş saksı-davranış çiftleri ve bütçesi olan bir saldırı bağlamı. Gecikmiş işler bu bağlamı taşır; sırf DamageType bilgisi yeterli değil. Ölüm bilgisi havuza dönmeden alınmalı; nesne referansı yerine güvenli kimlik/yaşam sürümü kullanılmalı. PlantHealth.LifetimeVersion mevcut ve kullanılabilir.

Zincir uygulaması kuyrukla ilerlemeli, run/round bittiğinde temizlenmeli. Aynı anda birden fazla saldırının bağlamları birbirine karışmamalı. Görsel kapasitesi hasarı belirlememeli. Kuyruk gecikmesi ve sınır aşımı ölçülmeli.

## 8. Ölçüm ve karar

Önce kontrollü iki kolda aynı build: zincir yok / zincir var. Mevcut ödüllere ve XP tablosuna aynı anda ayar yapılmaz. R25 ve R35, seyrek/yoğun davranış yerleşimi, küçük/büyük saksılar ve farklı seed'ler.

Ölç: kök saldırı başına gerçek hasat; nesil başına hedef, tetik ve hasat; boş hedefler; aynı hedef örtüşmesi; doğrudan/davranış payı; 5 saniyelik temizleme; tarla doluluğu; kaynak/XP; ortalama ve üst yüzdelik kare süresi; geciken ve bütçeye takılan işler.

Ardından normal bütçeli tam run: ödülün gelme zamanı, alınma oranı, davranışa uygun nişan ile mevcut canlı-sayısı nişanını ayrı politikalar olarak karşılaştır. Rastgele tile yerleşimi korunur; bot gizli RNG bilgisi kullanmaz.

Başarı yalnız tek ödül ×1,5 değildir: hazırlanan build'de yeni bir hasat dalgası oluşması, gerçek hedef temizliğinin artması, önceden sürekli dolu kalan tarlada kısa temizleme aralıkları açılması ve bunun kayda değer performans kaybı olmadan gerçekleşmesi. İnsan oyun hissi ayrıca kontrol edilmeli.

200–270 seçim sorunu ayrı XP temposu paketidir. Level başına üç seçim korunabilir; erken davranış erişimi korunarak geç XP tablosu ayarlanır. Zincir ve XP değişimini aynı deneyde yapıp nedeni karıştırma.

## 9. Öncelik

1. Başlangıçtan açık düğümler için anlaşılır gösterim; varsa başka düğümlerin yanlış açılmasını doğrula.
2. Mevcut Artçı erişimini veri ayarıyla ölç; zayıf Çifte Akım'ın havuzdaki fırsat maliyetini gider.
3. Kullanıcı bu tasarımı seçerse, ayrı aday profilde R25 sonrası sınırlı Zincir Hasat prototipi.
4. Sonra XP/seçim temposu ve tam-run denge. Yeni davranış türleri, yeni karakterler ve yeni boss türleri bu iş için gerekli değil.
