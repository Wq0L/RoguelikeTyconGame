# OYUNUN GÜNCEL SİSTEM REHBERİ
## ClickerGame / Earth Is Gone, Lunch Isn't

AI devri, mekanik referansı ve oynanış özeti

**Kaynak anlık görüntüsü: 10 Ekim 2026.** Ana kapsam GameScene ve Run50_GecOyunV1. Bu belge bir pazarlama metni ya da yeni tasarım önerisi değil; mevcut kod, bağlı veriler ve güncel üretim raporlarının açıklamasıdır.

Oyuncu uzaydaki izometrik bir tarlada bitki üretimini, fareyle hasadı ve saksı yerleşimini yönetir. Hasat kaynak, XP ve skor üretir. Level kartları zeminin etkilerini geliştirir; skill ağacı üretim kapasitesini ve erişimi açar; boss ödülleri saldırının çalışma biçimini değiştirir. Güçlü kombinasyonlar geniş alanları tek saldırıdan başlayan olaylarla hasat edebilir.

**Oyunun üç ana build kararı:** zemine gelen kart etkisi, o etkilerin üzerine yerleştirilen saksı düzeni ve boss ödülü seçimi. Oyuncu yeni modifier'ın düşeceği hücreyi serbestçe seçmez; gelen düzenle bir build kurar.

**Güncel ölçek:** 50 round, 15 boss tarihi, 32 boss ödülü, 4 temel saldırı davranışı, 4 geç oyun tarla olayı. Profilin round süreleri toplam 51 dakika 40 saniye aktiftir; kart, hazırlık ve ödül ekranları buna dahil değildir.

**Okuma uyarısı:** eski sohbetlerdeki fikirler ve eski profillerin sayıları mevcut oyunla aynı değildir. Özellikle Hilal'in 10 Ekim fare rotası, Gezgin Bereket'in pasif alan dönüşümü ve %0,02-%0,10 temel güç kartları bu belgede güncel kabul edilir.

Bu çalışma sırasında oyun çalıştırılmadı ve testler yeniden koşturulmadı. Test sonuçları ilgili raporların beyanıdır; kod ve veri okuması, insan oynanış onayı yerine geçmez. Oyun kaynaklarına, seçili profile ve oyuncu kaydına müdahale edilmedi.

---PAGE---
# 01 / Önce bunu anla
## Bir dakikalık zihinsel model

Bu oyun yalnız bir tıklayıcı veya yalnız bir otomatik savaş oyunu değildir. Üretim yapan tarlayı saldırı menziline, rastgele yerleşen zemin etkilerine ve hasatla çalışan kombinasyonlara göre düzenleyen bir roguelite/tycoon hibritidir. Karakter kaçırma, düşman mermilerinden sıyrılma gibi bir çekirdek hareket döngüsü yoktur; oyuncunun dikkati hasat odağına, yerleşime ve sonradan açılan tarla olaylarına gider.

**Fare hedefler, saldırı otomatik zamanlanır.** Aktif round'da PlayerController fare konumunu zemine yansıtır ve saldırı aralığı dolunca çevreye vurur; her darbe için tıklama şartı yoktur. Saksı yerleştirirken sol tık yerleştirir, R döndürür, Escape iptal eder. Güçlü build'de fareyi sabit tutmanın işe yarayabilmesi bu gerçek kontrol yapısından gelir.

1. Başlangıç çiftçisi ve tırpan seçilir; run kuralları bir profil üzerinden yüklenir.
2. Saksılar açık zemine yerleştirilir; altlarındaki hücre etkileri yerel statları ve rezonansları belirler.
3. Bitkiler üretim zamanlayıcılarıyla çıkar. Oyuncunun fare çevresindeki doğrudan saldırısı bitkilere hasar verir.
4. Bitkinin ölmesi hasattır. Kaynak, XP ve skor hesaplanır; uygun davranışlar tetiklenebilir.
5. Round sonunda kazanılmış level seçimleri işlenir; dönem kotası ve boss koşulları ilerlemeyi belirler.
6. Kaynaklar skill ağacı, saksı ve grid kapasitesine gider. Boss ödülleri yeni saldırı kuralları veya stat/bedel kombinasyonları ekler.

**Tasarım niyeti:** iyi yerleşim ve sinerji güç eğrisini belirgin şekilde hızlandırabilmeli. Buna rağmen son bölüm yalnız fareyi sabit tutma hâline gelmemeli. Diken, filiz, kabuk ve hareketli bereket alanı bu ikinci soruna yönelik mevcut içeriktir.

**Bu tasarımı değiştirme:** her ilave seçenek daha iyi oyun anlamına gelmez. Kullanıcı çok büyük ve anlaşılmaz bir build ağacı istemiyor. Mevcut sistemler arasında anlaşılabilir bağlantılar, farklı başlangıçlar, seçimlerin birbirini dışlaması ve güçlü ama sınırlı etkileşimler istiyor.

Kaynak: Run50_GecOyunV1.asset; CardSelectionUI.cs; FieldEventDirector.cs; Docs/Bolum3-8-GecOyunOlaylariVeBossOdulleri.md.

---PAGE---
# 02 / Terimler ve birbirinden ayrılması gereken sayılar

| Terim | Oyunda karşılığı |
|---|---|
| Grid / hücre / tile | Açılabilen zemin karesi. Kart modifier'ı burada yaşar; saksı değildir. |
| Saksı / planter | Bir veya birden fazla hücre kaplayan üretim birimi. Altındaki modifier'lardan ve rezonanslardan etkilenir. |
| Bitki | Canı olan ve hasat edilebilen üretim nesnesi. Aynı saksıda zaman içinde yeni bitkiler çıkar. |
| İsabet / hit | Canlı bitkiye uygulanan gerçek hasar. Bitkiyi öldürmek zorunda değildir. |
| Hasat / harvest | Bitkinin ölümüyle gerçekleşen ödüllü olay. Vuruş sayısıyla aynı değildir. |
| Direct | Oyuncunun kendi saldırısı. Kritik ve bazı ödül sayaçları yalnız bu yoldan beslenir. |
| Davranış / behavior | Patlama, elektrik, kasırga, bumerang. Normalde doğrudan hasat bu davranışların şansını dener. |
| Ödül saldırısı | Hilal, anten, ark, yankı gibi RunEffects saldırıları. Otomatik olarak dört davranıştan biri sayılmaz. |
| Zincir | Davranışın hasadının başka saksının davranışını sınırlı biçimde denemesi. Her ek saldırı zincir değildir. |
| Rezonans | Aynı saksı altındaki modifier tür/sayı kombinasyonunun koşullu stat etkisi. Kendiliğinden patlayan nesne değildir. |
| Kota | Bir takvim dönemi boyunca biriken skorun geçmesi gereken eşik. Tek round hasat adediyle karıştırılmamalı. |
| Boss hedefi | Boss round'una ait hedef. Arayüzdeki “boss hasadı/hasarı” ifadesini ham öldürülen bitki sayısı sanma. |
| Seçim hakkı | Bir kart teklifinden bir kart alma işlemi. Level başına 3 hak ile ekrandaki 3 aday farklı şeylerdir. |

Hasat adedi, Harvest Score, kaynak, XP ve hasar birbirinden farklı ölçümlerdir. Örneğin Gezgin Bereket kaynak ve XP'yi ikiye katlar; skoru ikiye katlamaz. Daha yüksek hasar da üretim sınırlıysa daha fazla hasat vermeyebilir.

**Nadirlik ayrımı:** bitkilerde Common, Uncommon, Rare, Epic, Legendary olmak üzere 5 basamak; tile kartlarında Common, Rare, Epic, Legendary olmak üzere 4 basamak vardır. Kart nadirlik şansı ile bitki nadirlik şansı ayrı statlardır.

---PAGE---
# 03 / Run akışı, süre ve kaybetme

Ana run 50 round'dur. Başlangıç kaynakları profil verisinde 80 altın, 0 demir, 0 taş olarak tanımlıdır; başlangıç seçimi ve skill etkileri kendi yollarından eklenir. Round, hazırlık, kart seçimi, boss ödülü ve sonuç ekranı ayrı durumlardır.

| Round | Bir round aktif süre | Alt toplam |
|---|---|---|
| 1-5 | 45 saniye | 225 saniye |
| 6-10 | 55 saniye | 275 saniye |
| 11-50 | 65 saniye | 2.600 saniye |
| Toplam | 50 round | 3.100 saniye / 51:40 |

Süre “oyuncu 51:40 sonra kesin bitirir” demek değildir. Kart seçimi, hazırlık/mağaza, boss ödülü, level hesaplama ve duraklama ayrı sayaçlardır. Fazla level almak ciddi ekran süresi üretebilir. Tarihsel Play'de ve botlarda bu yük görüldü; son küçük temel güç oranlarından sonra güncel insan ölçümü gerekir.

Dönem sonlarında kota, boss round'larında ilgili boss hedefi kontrol edilir. Eşikle tam eşitlik geçiş için yeterlidir. Başarısız koşul kayıp ekranına gider; kaybedilmiş run'a kart ve boss ödülü dağıtılarak devam edilmez. Başarılı round sonlarında bekleyen level işleri ve kart seçimleri tamamlanır; uygun boss ödülü akışı yürür.

**R50 güncel sınırdır.** Final kaydı takvimde ayrıdır fakat özel canavar bitki finali tamamlanmış kabul edilmez. R50 ödül/zafer akışı vardır; kullanıcının istediği “bitir veya sınırsıza devam et” seçeneği ile R51+ modunun tamamlandığına dair güncel uygulama kanıtı yoktur.

Round sonu ve run temizliği önemlidir: zincir kuyrukları, geçici ödül etkileri, hareketli davranışlar, olay kaynaklı hücre kısıtları ve saldırı duraklamaları eski run'dan yeni run'a taşınmamalıdır.

Kaynak: RoundManager.cs; RunCalendar.cs; Run50_GecOyunV1.asset; RunResultLayout.cs; ana Run50/Endless TODO.

---PAGE---
# 04 / Grid, saksı ve yerleşim kararı

Açık tarla merkezden genişleyen kareler hâlindedir: 3×3, 5×5, 7×7, 9×9 ve en çok 11×11. Sahnedeki 121 zemin hücresi bu fiziksel üst sınırı temsil eder; hepsinin run başında açık olması gerekmez.

Saksı boyutları 1×1, 1×2, 1×3, 2×2 ve 2×3 aileleridir. Büyük saksı daha fazla modifier hücresini aynı üretim biriminin altında birleştirebilir. Döndürme ve yerleşim önizlemesiyle hangi zeminin kapsanacağı görülür. Yerleştirme geçerliliği açık alan, çakışma ve şekil üzerinden denetlenir.

**Yeni kartın yeri oyuncunun serbest seçimi değildir.** Oyuncu kart türünü seçer; yeni modifier uygun açık hücrelerden rastgele birine uygulanır. Buna karşılık saksıyı o dağılıma göre düzenlemek oyuncunun kararıdır. Grid doluyken sunulan yükseltme kartları belirli mevcut tile'lara bağlıdır; bu ayrı bir akıştır.

Yerel statlar saksının kapladığı hücrelerden gelir. Aynı saksı altında tekrar eden türler rezonans eşiklerini yakalayabilir. Bu nedenle “daha büyük grid her zaman hemen daha iyi” değildir: genişlemek yeni modifier yerleşimini açarken küçük alanda kalmak mevcut tile'ları yükseltmeye ve ardından temel güç kartlarına daha erken ulaşmaya izin verir.

**Örnek karar:** patlama modifier'ı uygun bir yerde geldiyse daha büyük saksıyla kapsayıp patlama şansını/hasarını kuvvetlendirmek, o an daha çok boş alan açmaktan iyi olabilir. Bu otomatik en iyi strateji olarak kodlanmış değildir; üretim, hedef yoğunluğu, maliyet ve ödüllere bağlıdır.

Büyük saksıya boyutla artan üretim cezası verme fikri sohbetlerde önerildi. Bu öneriyi tüm saksılara uygulanmış güncel kural olarak varsayma. Benzer şekilde rastgele kart yerleşimini kullanıcıya hücre seçtirerek değiştirme; bu çekirdek karara aykırıdır.

Kaynak: GridManager; ProgressionManager.ApplyRandomEligibleCell; PlanterBrain; yerleşim/ghost önizleme kodu; Docs/FinalSkillTree.md.

---PAGE---
# 05 / Üretim, hasar, ödül ve ekonomi

Üretim ve hasat aynı hız değildir. Boş üretim yuvasının yeni bitki çıkarma süresi, yaşayan bitkinin canı, oyuncunun eriştiği alan, saldırı aralığı ve davranış tetikleri birlikte gerçek hasadı belirler. Birine çok yatırım yapmak diğer darboğazı kaldırmayabilir.

Doğrudan vuruş oyuncunun hasat hasarı ve yarıçapını kullanır. Kritik şansı/çarpanı, hedef saksının direct hasar etkileri ve ilgili boss ödülleri bu yola etki eder. Bir davranışın veya yeni ödül saldırısının aynı çarpanların hepsini yeniden alacağını varsaymak hatadır; her yol kendi sözleşmesine sahiptir.

Hasat sonucunda altın, demir, taş, XP ve Harvest Score hesaplanır. Bitki nadirliği ve ilgili stat/rezonans koşulları getiriyi değiştirir. XP için aktif Alpha denge setinin bitki nadirliği çarpanları sırasıyla 1 / 1,5 / 2 / 2,5 / 3'tür. Duplicate kaynak ödüllerini etkiler; XP ve skoru otomatik ikiye katlamaz.

| Yatırım | Beklenen yarar | Muhtemel darboğaz |
|---|---|---|
| Hasar / kritik | Daha az vuruşla öldürme | Zaten tek vuruşsa fazlası boşa gidebilir. |
| Yarıçap | Daha fazla hedef ve modifier erişimi | Boş alan veya üretim hızı sınırlayabilir. |
| Üretim | Yeniden biçilebilecek bitki sayısı | Yeterince vuramıyorsan bitkiler birikir. |
| XP | Daha fazla tile/yükseltme/temel güç seçimi | Grid ve seçim zamanı; ekonomi fırsat maliyeti. |
| Nadirlik | Daha değerli bitkiler, bazı ödüllerin koşulu | Can ve üretim-hasat dengesi ayrıca izlenmeli. |
| Davranış | Bir direct hasadın erişimini büyütme | Tetik şansı, hedef boşluğu, kapasite ve hasar ölçeği. |

Skorla kaynak aynı amaç değildir. Skill satın alımına giden altın, ilerleme fırsatını değiştirir; kota ise skor ister. XP yatırımı değerlendirilirken yalnız level oranına bakmak bu fırsat maliyetini gizler.

---PAGE---
# 06 / Level kartları ve temel güç evresi

Standart level başına 3 seçim hakkı kazanılır. Her hak bir teklif ekranından bir kart aldırır. Örneğin 10 yeni level standart durumda 30 seçimdir; run'ın “level 75” olması başlangıç level'ı nedeniyle tek başına 225 kazanılmış seçim kanıtı değildir. Bedelli ödüller gelecekteki hak sayısını değiştirebilir.

Kart sistemi üç evreyi kullanır: açık uygun hücre varken yeni modifier; grid dolunca mevcut tile yükseltmesi; yeterli yükseltme adayı kalmayınca temel güç. Yükseltme adayları üç slotu dolduramazsa kalan slotlarda temel güç görülebilir. Tüm tile'lar en yüksek seviyeye geldiğinde teklifler temel güç olur.

Tile seviyesi 0'dan başlar, en çok 3 olur. Yükseltme kartı Common/Rare ise +1, Epic ise +2, Legendary ise +3 seviye verir; kalan kapasiteyi aşan bölüm taşmaz.

Temel güç kategorileri hasat hasarı, saldırı aralığı, üretim süresi, tüm kaynaklar ve XP'dir. Güncel tam sürümde miktarlar küçültülmüştür:

| Kart nadirliği | Görünen değişim | Veride oran |
|---|---|---|
| Common | %0,02 | 0,0002 |
| Rare | %0,04 | 0,0004 |
| Epic | %0,07 | 0,0007 |
| Legendary | %0,10 | 0,001 |

Hasar, XP ve kaynak temel güçleri kendi stat/hedef gruplarında toplanır. 1.000 adet +%0,10, o gruptan ×2 yapar; her kartla tekrar üstel çarpma yapmaz. Saldırı/üretim süre azaltımları mevcut çarpımsal yolda ve minimum süre sınırlarıyla çalışır. Boss, tile ve rezonansların ayrı çarpanları korunur.

Bu güç **run içi kalıcıdır**, hesaplar arası metaprogression değildir. Saldırı aralığı tabana gelmişse işlevsiz kart filtrelenebilir. Skip, mevcut hakkı tüketip teklifteki en yüksek nadirliğe ve round'a bağlı rastgele bir kaynak verir; sınırsız ücretsiz atlama varsayma.

Level 140 sonrasında maliyet kuyruğu: C(L) = 150.000 × [1 + 0,05 × (L - 140)]. Oynanış amaçlı sabit level tavanı yoktur; sayısal güvenlik ve kare başına işleme sınırı vardır.

Kaynak: CardSelectionUI.cs; BasePowerCardRules.cs; ProgressionManager.cs; Docs/FullGame-PlaytestFix-20261006.md.

---PAGE---
# 07 / Skill ağacı, başlangıç ve kayıt

Skill ağacı kart sistemiyle aynı değildir. Kaynak harcayarak stat, grid boyutu, saksı erişimi ve davranış kart havuzları açılır. Sahne tabanlı ağaç 140 düğüm/304 satın alım olarak belgelenmiştir; dört ana yön hasar, hız/zaman, saksı/grid ve ekonomidir. Bağlantılar ve ön koşullar açık verilerdir. Kademe yükseltmesi önceki kademe etkisini değiştirir; bütün eski kademe değerlerini tekrar tekrar eklemek yanlış olur.

Eski FinalSkillTree belgesindeki 130 round fiyatlandırmasını güncel 50 round'a doğrudan taşıma. Profilin bağlı RunBalance verisi maliyet ve başlangıç erişimini değiştirebilir. Mevcut denge ailesi Patlama ve Elektrik kartlarına erken erişim sağlar; bu düğümlerin “başlangıçtan açık” görünmesi satın alınmış kademe anlamına gelmez. Kasırga ve Bumerang otomatik ücretsiz açılmış değildir.

| Başlangıç seçimi | Belgelenen etki / erişim |
|---|---|
| Bahçıvan | Nötr çiftçi; başlangıçta açık. |
| Tüccar | Kaynak ×1,25, skor ×0,90; başlangıçta açık. |
| Seçici Yetiştirici | Nadirlik +15, üretim aralığı ×1,15; bir run'da 5 Legendary hasadı göreviyle açılır. |
| Standart tırpan | Nötr seçenek. |
| Dar Kesim | Direct hasar ×1,40, yarıçap ×0,75. |

Bu mevcut katalog görev tamamlandıktan sonra 3 çiftçi × 2 tırpan ile 6 kombinasyon verir. Sohbetlerde önerilen başka çiftçiler, tırpanlar ve görevler mevcut içerik sayısına eklenmemelidir. Başlangıç seçimi run sırasında serbestçe değiştirilmez.

Meta kayıt açılan başlangıçları, görev ilerlemesinin ilgili kalıcı sonucunu ve son seçimleri saklar; bu bir tam run kaldığı yerden sürdürme sistemi değildir. Gerçek oyuncu kaydı LocalLow içindeki meta.json'dur. Ayarlar ayrı PlayerPrefs yolunda olabilir. İzole testlerin gerçek kayıt yerine MemoryOnly/kopya ortam kullanması gerekir.

Kaynak: Docs/Bolum3-3-BaslangicSecimiVeGorevler.md; Docs/FinalSkillTree.md; StartCatalog; MetaSave; RunBalanceSO.

---PAGE---
# 08 / Statların sahipliği ve hesap kuralları

**Global oyuncu statı ile saksının gerçek statı aynı sorgu değildir.** StatManager temel seti ve global modifier'ları yönetir. PlanterBrain kendi tabanı, altındaki tile'lar ve ilgili global etkilerle yerel sonucu hesaplar. Davranış şanslarını oyuncunun CoreStats tablosunda uydurma taban değerlerle düzeltmeye çalışma.

| Grup | Örnekler | Kritik ayrım |
|---|---|---|
| Oyuncu saldırısı | HarvestDamage, AreaRadius, AttackSpeed, CritChance, CritMultiplier | AttackSpeed adı aralık olarak kullanılır; azalması hızlanmadır. |
| Saksı/üretim | PlantSpawnRate, RareSpawnChance, yerel damage multiplier | SpawnRate de süre/aralık mantığındadır; isimden yön çıkarmama. |
| Ekonomi | Gold/Iron/StoneGainMultiplier, XPGainMultiplier, HarvestScoreMultiplier | Kaynak, XP ve skor çarpanları ayrı tutulur. |
| Kart/grid erişimi | MutationLuck, GridUnlockSize, CardSkip ve benzeri | Kart nadirliği şansı, bitki nadirliğinden farklıdır. |
| Davranış | Explosion/Tornado/Boomerang/ElectricChance | Gerçek şans saksıya özeldir; olmayan davranış kendiliğinden açılmaz. |

Flat, yüzde ekleme ve MorePercent işlemleri eşdeğer değildir. Normal MorePercent etkileri bağımsız çarpanlar olarak çalışabilir. Temel güç kartlarının seçili statları ise tek bir birikim grubunda toplanır. Bu özel kuralı bütün buff'ları toplayacak küresel değişikliğe dönüştürme.

Ortak ham hesap: (Set varsa Set değeri, yoksa taban + uygun flat katkıları) üzerinden yüzde ekleme ve bağımsız çarpanlar uygulanır. Daha açık ifadeyle: (seçilmiş taban + Flat toplamı) × (1 + AddPercent toplamı) × MorePercent çarpımları. Ardından stat sınırları uygulanır. Üretim süresinin tabanı 0,5 saniyedir; bunun altına iten fazla üretim katkısı logaritmik biçimde nadirlik puanına dönüştürülebilir. “Üretim tabanda → Nadirlik” kart metninin nedeni budur.

Bir vuruşun kaynak saksısı, öldürülen bitkinin hedef saksısı ve ödül saldırısının hasar kaynağı farklı olabilir. Kaynak davranış hasarını hedef saksının hasarıyla değiştirmek veya global çarpanı artçıda ikinci kez uygulamak çifte ölçekleme hatası üretir. Rezonansların hangi tarafı kullandığı da buna göre korunmalıdır.

NumericSafety taşmaları kontrol eder. Int'e sığmayan değer 2.147.483.647'ye doyar; NaN/sonsuz raporlanır. XP double olarak korunur; bir karede en fazla 256 level işlenir. Bunlar teknik emniyettir: dengeli ekonomi veya gerçek endless büyük sayı modeli oldukları söylenemez.

Kaynak: StatManager.cs; StatCalculator; StatModifier; PlanterBrain; NumericSafety.cs; BasePowerCardRules.cs.

---PAGE---
# 09 / Rezonanslar: yerleşimin koşullu ödülü

Rezonans aynı saksı altında kalan benzersiz hücrelerdeki tür ve sayıya bakar. Özel bir çizim şekli veya ağaç bağlantı deseni istemez. Aynı kuralın en güçlü eşiği seçilir; aynı stat üzerinde yarışan rezonanslar en güçlü olanı kullanır, farklı stat etkileri birlikte çalışabilir. Önizleme ile gerçek yerleşimin aynı değerlendirmeyi kullanması amaçlanır.

| Rezonans | Koşul | Etki |
|---|---|---|
| Odak | 2 / 3 / 4 Hasar | Direct hasar ×1,5 / ×2 / ×3 |
| Şöhret | 2 / 3 / 4 Enerji | Skor ×1,5 / ×2 / ×3 |
| Bolluk | 2 / 3 / 4 Duplicate | Kaynak ×1,25 / ×1,5 / ×2 |
| Bilgelik | 2 / 3 / 4 Su | XP ×1,5 / ×2 / ×3 |
| Kristal Bahçe | 2 / 3 / 4 Kristal | Nadirlik +10 / +20 / +35 puan |
| Güçlendirilmiş Hasat | Davranış + 1 / 2 Hasar | İlgili davranış hasarı ×1,5 / ×2 |
| Değerli Hasat | 2 Enerji + Kristal | Rare ve üzeri bitki skoru ×3 |
| Verimli Toprak | 2 Duplicate + Bereketli | Kaynak ×2 |
| Elektrik Bilgisi | 2 Su + Elektrik | Kaynak elektriğin hasadında XP ×10; hedef XP rezonansıyla güçlü olan alınır. |
| Nadir Fidanlık | 2 Kristal + Bereketli | Nadirlik +25, üretim aralığı -%15 |

Nadirlik artışı “Legendary yüzdesine doğrudan +35” değildir. Puan/dağılım hesabıdır; belgelenen nadirlik puanı üst sınırı 95'tir. Elektrik Bilgisi'nin XP ×10'u, hedefteki Bilgelik ile yeniden çarpılıp ×30 yapılmaz.

**Tasarım sınırı:** Rezonans Çöküşü veya rezonansın kendisini patlatma yaklaşımı kullanıcı tarafından reddedildi. Mevcut pasif/koşullu stat sistemi korunur. Hilal gibi bir ödülün bu düzenle sinerji kurması, yeni bir “rezonans nesnesi patlaması” eklemek için izin değildir.

Rezonans, boss ödülü, tile buff'ı ve global skill aynı kelimeyle “buff” diye anlatılabilir; uygulamada ayrı kaynaklardır. Statı yanlış yere yazmamak için bu sahipliği koru.

Kaynak: Docs/SimpleResonance.md; ResonanceManager.cs; ResonanceRulesSO; ResonancePreviewUI.

---PAGE---
# 10 / Dört temel davranış

Normal döngüde oyuncunun doğrudan hasadı, hasat edilen bitkinin saksısındaki uygun davranış şanslarını dener. Davranış kartı/saksı şansı yoksa yalnız “davranış hasarı arttı” diye yeni davranış oluşmaz. Saldırının vurduğu her bitki ile ölen her bitkiyi de ayırmak gerekir.

| Davranış | Geometri ve rol | Sınır / önemli özellik |
|---|---|---|
| Patlama | Kaynak saksının ayak izi çevresinde alan hasarı. | Çok hücreli saksının şekli erişimi etkiler; görsel alan ile gerçek hedef listesi aynı olmalı. |
| Çapraz Elektrik | Saksının köşelerinden dört çapraz yöndeki hücrelere erişir. | Yatay/dikey tüm satır saldırısı değildir; Çifte Akım sonraki darbenin erişimini değiştirir. |
| Kasırga | Hareket eden, yolundaki üretime hasar taşıyan davranış. | Tarla genelinde en fazla 12 aktif; zincir de aynı kapasiteyi kullanır. |
| Bumerang | Düz eksende gidiş-dönüş saldırısı. | Bitki yaşamı başına her bacakta bir vuruş; ayrı 12 aktif sınırı. |

**Kapasite gerçek oynanış sınırıdır.** Sınır dolu normal tetik reddedilebilir; görselsiz sınırsız hasar nesnesi açılmaz. Zincir işi sınırlı kuyrukta bekleyebilir. İki davranışın sayaçları ayrıdır: “toplam 12” değil, 12 kasırga ve 12 bumerang.

Sınırı kartla artırabilecek movingBehaviorLimitBonus altyapısı vardır. Testte +4, sınırı 16 yapar. Ancak bu artışın gerçek ödül havuzunda bir kart olarak dağıtıldığı varsayılmamalı; 6 Ekim raporunda yalnız altyapı/test kartıdır. Teknik güvenlik tavanı 64, oyuncunun başlangıç kapasitesi değildir.

Hareket, havuz, çarpışma ve yaşam kimlikleri hasarın parçasıdır. VFX kapasitesi yüzünden oyun hasarının sessizce yok olması ile bilinçli aktif davranış sınırı aynı sorun değildir. Ölçümde reddedilen tetik, bekleyen zincir ve atlanan dekor görseli ayrı sayılmalıdır.

Kaynak: HarvestBehaviorManager; TornadoManager; PlanterBrain; HarvestLink; Docs/HarvestBehaviors.md; FullGame-PlaytestFix-20261006.md. Eski belgelerdeki 3/6 kapasite güncel tam sürüm değildir.

---PAGE---
# 11 / Zincir, Artçı ve Hasat Ritmi

**Zincir Hasat:** bir davranış bitki hasat ettiğinde, hedef bitkinin saksısındaki davranışlar sonraki nesil olarak denenebilir. Aynı kök direct saldırıda aynı saksı + davranış çifti en fazla bir kez denenir. Başarısız zar da bu hakkı harcar. A → B → A sonsuz döngüsü böyle önlenir.

Güncel prototip zincir iki ek nesil kullanır. Nesil şans ve hasar katsayıları 0,75 / 0,50; kök başına ek başarılı iş bütçesi 32, kare başına iş bütçesi 8'dir. Yeni hasar önceki neslin azaltılmış hasarını tekrar çarpmak yerine yeni kaynak saksının normal davranış hasarından hesaplanır. RNG ayrı ve seed'e bağlıdır. Round bitince bekleyen işler temizlenir.

**Artçı Patlama:** normal patlamanın ardından gecikmeli ikinci darbedir. İlk darbede hesaplanan hasarın uygun oranını kullanır; yeni bir bağımsız normal tetik zarı değildir. Güncel erişim ödülünde genişletilmiş alan 2 hücre ölçeğindedir. İkinci darbe anındaki canlı hedeflere vurur. İlk darbe alanı boşalttıysa daha yüksek sayı tek başına daha fazla hasat sağlamaz.

Artçı'nın ince kor kırmızısı sınır çizgisi, ikinci darbenin gerçek hedef bölgesinin geri bildirimidir; ayrı bir üçüncü hasar veya debuff değildir. Saksı şekli ve hücre sınırları nedeniyle daire olmak zorunda değildir. Kullanıcı bunu daha iyi comic patlama/temas VFX'iyle ifade etmek istiyor; değiştirme işi görseli hasar alanından koparmamalı.

**Hasat Ritmi:** her 5 doğrudan hasat bir güçlü saldırı hakkı hazırlar; sonraki uygun direct saldırı hasar ×1,75 ve yarıçap ×1,60 olur. “Her 5 fare vuruşu” değildir. Davranış/Artçı hasadı sayılmaz; boş saldırı hakkı harcamaz. Eşiği aşan saldırının kalan hedefleri aniden güçlenmez.

Tam sürümde Hasat Ritmi ve Zincir Hasat aynı dışlama grubundadır. Birini alınca diğeri o run'da sunulmaz/alınamaz. Eski ekran görüntülerindeki ikisini birlikte taşıyan build güncel kural değildir. Artçı ve ikinci dalga zincire yeniden katılmaz.

---PAGE---
# 12 / Boss takvimi ve hedefler

Takvim profil verisidir. RoundManager, HUD, ödüller ve ölçüm aracı aynı RunCalendar hesabını kullanmalıdır. “Her 5 round boss” şeklindeki eski formül, bu açık takvime uygulanmaz. R50 ayrı final işaretine sahiptir.

| Dönem | Boss round | Dönem kotası | Boss hedefi |
|---|---|---|---|
| 1-3 | 3 | 40 | 10 |
| 4-6 | 6 | 75 | 25 |
| 7-10 | 10 | 190 | 40 |
| 11-13 | 13 | 500 | 150 |
| 14-16 | 16 | 1.200 | 430 |
| 17-20 | 20 | 3.900 | 1.000 |
| 21-23 | 23 | 5.200 | 1.700 |
| 24-26 | 26 | 7.100 | 2.300 |
| 27-30 | 30 | 13.000 | 3.000 |
| 31-33 | 33 | 16.000 | 4.300 |
| 34-36 | 36 | 22.000 | 6.500 |
| 37-40 | 40 | 38.000 | 8.400 |
| 41-43 | 43 | 37.000 | 11.000 |
| 44-46 | 46 | 48.000 | 13.000 |
| 47-50 | 50 | 68.000 | 13.000 |

Bunlar mevcut asset değerleridir; yeni bir denge önerisi veya kalite onayı değildir. 41-43 kotasının önceki dönemden düşük olması otomatik veri hatası sayılmaz: dönem süresi ve boss koşulu farklıdır.

Ana boss kimlikleri Don, Sert Kabuk ve Sis'tir. Don belirli satırlarda üretimi yavaşlatır; Sert Kabuk belirli sütunlardaki canı artırır; Sis direct erişim/alanını kısıtlar. Hava, bölge göstergesi ve HUD rengi bu kimlikten gelmelidir: buz mavisi, amber, gri-lila. Tarla olayları bu mevcut boss etkilerinin yanında çalışır; onların yerine aynı sistemmiş gibi birleştirilmez.

Kaynak: Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset. Veriler dosyadaki 64 bit dizilerden okunmuştur; boss seçimi ilgili bossPool tarafından belirlenir.

---PAGE---
# 13 / Boss ödülü seçimi ve bedeller

Güncel havuz 32 ödülden oluşur: 7 erken, 13 orta, 12 güçlü. Bunların 13'ü Bölüm 3.8 ile gelen yeni RunEffects içerikleridir. Oyuncu bütün kartları aynı run'da almak zorunda değildir; ön koşul, stack sınırı ve dışlama grupları gerçek teklif havuzunu daraltır.

| Aşama | Açılma aralığı | Takvimde ilk sunulabileceği boss |
|---|---|---|
| Erken | R1-11 | R3 |
| Orta | R12-21 | R13 |
| Güçlü | R22-50 | R23 |

Teklif en çok 3 farklı karttır. Mevcut aşamada uygun aday varsa bir slot ona ayrılır; kalan slotlar açılmış aşamalardan seçilir. Aşama uzaklığı ağırlıkları 1 / 0,60 / 0,35'tir. Bu belirli bir capstone garantisi değildir. Uygun aday tükenirse 2, 1 veya 0 kartla kilitlenmeden ilerleyen yol kullanılmalıdır.

Aşamalı havuzda açılma kaynağı aşamanın firstRound değeridir; eski reward.minRound/maxRound alanları bu havuzun asıl kapısı değildir. Bu nedenle eski taslakta “Bilgi Filizi R30'da çıkar” veya “Canavar Kesimi yalnız R25+” yazması güncel teklif davranışının kanıtı değildir.

**Bereketli Öğrenim:** gelecek level başına +1 seçim, direct hasar ×0,80. **Davranışa Adanış:** gelecek level başına -1 seçim, davranış hasarı ×1,50. Bu ikisi birbirini dışlar. Bekleyen seçimler kazanıldıkları anda sayaca eklenmiştir; ödül geçmiş hakları geriye dönük değiştirmez. Hak sayısı 1-5 sınırında doğrulanır; geçersiz ödülü sessizce kırpmak yerine sunmama/alınamaz yapma yolu vardır.

Hasat Ritmi / Zincir Hasat ikinci önemli dışlama çiftidir. Birbirini dışlayan iki kart aynı teklifte seçenek olabilir; biri alınınca diğerinin kapandığı açıklanır. İşlem stack, koşul, ön şart ve aralık kontrolünden sonra uygulanır.

Kaynak: BossRewardManager.cs; BossRewardPool_GecOyunV1.asset; BossRewardSO.cs. Ek katalogda gerçek asset bağlantıları ve stack sınırları yer alır.

---PAGE---
# 14 / Mevcut ödüllerin build rolleri

| Ödül | Rol ve yorum |
|---|---|
| Keskin Bıçak | Direct hasarın temel artışı. Davranış hasarıyla eş anlamlı değil. |
| Kritik Göz / Ağır Darbe | Kritik şansı ve kritik çarpanı; yeni kritik ödüllerine giriş sağlar. |
| Hızlı Bilek / Fırtına Bileği | Saldırı aralığını azaltır; daha fazla direct deneme sağlar. Minimum aralık korunur. |
| Geniş Savuruş | Direct erişim yarıçapı; tek saldırıda daha fazla hedefe ulaşma. |
| Altın Hedef | Rare+ bitkilere direct hasar bağlantısı. |
| Nadir Tohum | Bitki nadirliği yönü; kart MutationLuck ile karıştırma. |
| Bilgi Filizi | Hasat XP'si; daha fazla seçim ve ilerleme fırsatı. |
| Bereketli Toprak | Düşük doluluk koşuluyla sunulan üretim desteği. Sunulma koşulu, sürekli çalışma koşuluyla aynı değildir. |
| Kıvılcım | Saksıda zaten var olan davranış şanslarına katkı. Yeni davranış açmaz. |
| Yıkım Gücü | Davranış hasarını güçlendirir. |
| Canavar Kesimi | Direct yolun güçlü hasar ödülü. |
| Artçı Patlama | Daha geniş alana gecikmeli ikinci darbe. |
| Çifte Akım | Elektriğin ikinci dalgasıyla erişimi büyütme. Laboratuvarda tüm kırılma hedeflerini tutturmuş sayılmaz. |
| Hasat Ritmi | Direct hasatla biriken güçlü/alanlı sonraki saldırı. |
| Zincir Hasat | Davranışlar arası sınırlı yayılım. |
| Bereketli Öğrenim / Davranışa Adanış | Seçim hakkı ile direct/davranış gücü arasında run yönü seçimi. |

Bu ödüller için “hepsi birbirine çarpılır, her hasara uygulanır” genellemesi yapma. Direct, rare direct, behavior, echo ve yeni ödül saldırıları farklı uygulama yollarıdır. Özellikle davranış ×1,50'nin artçıda tekrar çarpılıp ×2,25 olması geçmişte önlenmiş bir hata sınıfıdır.

Build kimlikleri direct/kritik, hız, nadirlik/ekonomi, davranış, alan/modifier ve grid geometrisi etrafında oluşur. Bunlar kullanıcıya seçtirilen katı sınıflar değildir; oyuncunun aldığı kart ve yerleşimden doğan yönlerdir.

---PAGE---
# 15 / Yeni saldırı ödülleri: ortak kurallar

Bölüm 3.8'in 13 ödülü ayrı RunEffectSO verileri ve çalışma zamanı etkileriyle bağlanır. Bunlar yalnız listede duran fikirler değildir. Ancak her birinin nihai dengesi ve özel VFX kalitesi eşit düzeyde tamamlanmış değildir.

| Ödül | Güncel temel işleyiş |
|---|---|
| Kritik Ark | Direct kritik, direct saldırının vurmadığı yakın başka bitkiye düşük hasarlı sıçrama gönderir; yeni kritik/ark zinciri üretmez. |
| Hasat Yankısı | 3 direct hasat koşulundan sonra alan merkezine göre karşı/simetrik hücrelerde zayıf ek vuruş; direct'te vurulanları tekrar hedeflememe kuralı vardır. |
| Taşan Hasar | Gerçek overkill'in bir bölümünü yakına taşır; aktarım kaybı, adım ve kaynak sayısı sınırlıdır. |
| Nadir Reaktör | Rare+ direct hasat mevcut davranış şanslarını kısa süre güçlendirir. Sıfır şans yeni davranış olmaz; süre yenilenir, sınırsız stack olmaz. |
| Kritik Patlama | Aynı direct saldırıda yeterli ayrı kritik hedef varsa ek alan vuruşu üretir. Normal Patlama davranışıyla aynı tetik değildir. |
| Açgözlü Hasat | Çoklu direct hasat bir sonraki saldırı için hak hazırlar; hak verimli çoklu hedefte değer kazanır, tek hedefte kaybolabilir. |
| Aşırı Yükleme | Başarıyla çalışan normal davranışı gecikmeli, daha zayıf tekrarlar; saksı/tür cooldown'u ve kök bütçesi vardır. |

Diğer 6 ödül iki aile kurar: Elektrik Anteni → İletken Hat → Kapalı Devre ve Hilal Darbesi → Büyüyen Ay → Dolunay. Bunların yerleşim ve hareket kuralları sonraki sayfalardadır.

Yeni ödül saldırıları varsayılan olarak normal kritik, normal davranış tetikleme ve HarvestChain akışına tekrar girmez. Bu ayrım döngüsel sonsuz proc'u önler. “Ekranda elektrik var” diye antenin otomatik ElectricChance kullandığını veya yeni patlamanın ExplosionChance zarını yeniden attığını varsayma.

Bu kurallar güçsüzlük anlamına gelmez: gerçek katkı yeni hedeflere erişim, direct hasat koşullarının sıklığı ve üretim doluluğuyla belirlenir. Denge değerlendirirken vuruş başına hasar ile toplam gerçek hasadı ayrı ölç.

---PAGE---
# 16 / Anten ailesi: oyuncunun seçtiği hat

**Elektrik Anteni**, tarla kenarında uygun konuma yerleştirilen bir ödül saldırısıdır. Kart, yerleşim adımı gerektirir; mevcut tile kartlarının rastgele yerleşim kuralını değiştirmez. Anten içeri doğru ilk 3 tile'a periyodik atış yapar. Başlangıç ayarları 2,5 saniye aralık ve temel hasarın 0,60 oranıdır.

**İletken Hat** anten ön koşuluyla ikinci yerleşimi açar. Antenler arasındaki uygun hücre rotasına saldırır. Güncel görsel/rota bağlama belgesinde hücre hattı Bresenham yürüyüşüdür; her durumda sınırsız bir satırın bütün hücrelerini vurduğu söylenmemeli. Başlangıç hat aralığı 4 saniye, hasar oranı 0,45'tir; kısa anten atışlarının korunması veriyle belirlenir.

**Kapalı Devre** hattın işleyişine ek darbe kuralı ekler. Hattın hasadı uygun davranış saksısına ulaştığında ilgili şans değerlendirmesi ek hat darbesi üretebilir. Bu, o saksının gerçek patlama/elektrik/kasırga/bumerang davranışını doğrudan çalıştırmak değildir. Ek darbe gecikme, düşük hasar ve cooldown ile sınırlıdır. Bu yorum kullanıcı tarafından kabul edilmiş uygulama kararları arasındadır.

**Yerleşim kararı:** üretimin yoğun olduğu veya bağlantı kurmak istenen hücreleri kapsayan bir hat kurmak değerlidir. Hat boşsa her saniye daha fazla görsel üretmek hasadı artırmaz. Anten ailesi, fare konumundan bağımsız ikinci bir hasat geometrisi sağlar.

Görsel üretimi vardır: teal taş/yaprak taban, altın bobin, bakır çatal; atış öncesi kısa dolum, cyan-krem ışın ve ek darbede altın çift hat. Temas bildirimi gerçek isabetten gelir; sırf efekt oynuyor diye boş hücrede vurulmuş bitki gösterilmez. Özgün kısa sesler merkezi ses sınırını kullanır.

Kaynak: AntennaEffectSO.cs; ConductiveLineEffectSO.cs; ClosedCircuitEffectSO.cs; Docs/AntennaFamily/README.md; ilgili GecOyunV1 effect asset'leri.

---PAGE---
# 17 / Hilal ailesi: 10 Ekim güncel kural

**Hilal Darbesi'nin sayacı gerçek direct bitki isabetlerini sayar.** Hasat şartı yoktur; canlı bitkiye pozitif hasar gerekir. Tek saldırı 4 ayrı bitkiye vurduysa sayaç +4 olur. Kritik fazladan sayılmaz. Ölü/hasarsız hedef, davranış ve ödül saldırıları sayılmaz.

6 isabette bir hilal çıkar. Sayaç 4 iken 4 yeni isabet, 1 dalga ve 2 artan isabet bırakır. Yeterince geniş bir direct saldırı birden fazla dalga üretebilir; aktif dalga kapasitesi 4'tür. Kapasite üstü dalga kuyruğa birikmez; atlanan dalga olarak ölçülür.

Dalga **o saldırının fare hedef konumundan** doğar. Dört ana yönden biri eşit şansla seçilir: +X, -X, +Z, -Z. O anda açık alanın kenarına kadar rota sabitlenir. Sonradan grid açılması mevcut dalgayı uzatmaz. Kenarda dış yön seçilirse başlangıç hücresi vurulup dalga bitebilir; dolu bölgeye gizlice yeniden yönlendirilmez.

Taban hasar oranı 0,80, hücre adımı 0,07 saniyedir. Normal davranış/chain tetiklemez. “Yükseltince davranış tetiklesin” konuşulmuş bir gelecek kararıdır; güncel Büyüyen Ay bunu otomatik açmaz.

**Büyüyen Ay:** bir adım en az bir gerçek hasat yaparsa büyüme 1 artar, en çok 3 olur. Sonraki adımların hasarı 1 + 0,25 × büyüme katsayısı alır; cephe en çok 3 hücre genişler. Hasatsız adım, boşluk dahil, büyümeyi sıfırlar. Her yeni dalga sıfır büyümeyle başlar.

**Dolunay:** büyüme 2'ye ulaşınca rotanın uzak ucundan başlangıca ters dalga çıkar. 0,15 saniye gecikme, taban hilal hasarının 0,70'i ve tetik anındaki cephe genişliği kullanılır. Dönüş büyümez, yeni dönüş üretmez. Oyuncunun direct saldırısı 0,35 saniye duraklar; oyun/VFX bütünü durmaz.

Cyan ana hilal ve sıcak altın dönüşün özgün modelleri/sesleri vardır. Eski README'deki kenardan rastgele tam satır doğma açıklaması tarihsel kalmıştır. Serialized crescentFullRows adı korunmuş olsa da güncel anlamı bu fare rotasıdır.

Kaynak: Docs/CrescentFamily/CursorRoute.md; CrescentEffectSO.cs; GrowingMoonEffectSO.cs; FullMoonEffectSO.cs.

---PAGE---
# 18 / Geç oyun olayları: oyuncunun odağı

Olaylar geç oyunda fareyle çözülmesi gereken veya takip edilmesi değerli yerel durumlar yaratır. Plan R26 ve sonrasında devreye girer. Aynı anda en fazla bir olay; boss round'larında Diken/Filiz/Kabuk seçimi, normal round'larda Bereket için ayrı olasılık akışı vardır. Olay RNG'si ödül tekliflerini tüketmemelidir.

| Olay | Oyuncu kararı | Başlangıç kuralı |
|---|---|---|
| Diken Kuşatması | Üretimi kapatan kökleri direct saldırıyla kırmak. | 3×3, üretimin en çok %40'ını kapsayan bölge; 2 kök, her biri 6 direct darbe; 30 saniye sınır. |
| İstilacı Filiz | Yayılmadan ana hedefi temizlemek veya etkisini göze almak. | Ana hedef 8 darbe; 2,5 saniyede yayılma, en çok 8 parça/2 hücre yarıçap; üretim aralığı ×1,6. |
| Aşılı Kabuk | A veya B bölgesinden hangisini koruyacağını seçmek. | İki 3×2 bölge, toplam en çok %60; 4 darbeli hedef, 8 saniye seçim; diğer bölge 20 saniye kapanır. |
| Gezgin Bereket | Hareket eden üretken alanı hasat planına katmak. | 3×3, aktifken kaynak/XP ×2 ve boş yuvalarda daha kısa üretim süresi; round sonuna kadar hareket eder. |

Diken/filiz/kabuk hedefleri normal bitki canı ölçeğiyle erimez: direct saldırı başına bir darbe sayılır. Bir milyon hasarlı build de bu hedefi tek hasarla yok etmiş sayılmaz. Davranış saldırıları özel hedefleri temizlemez. Daha hızlı direct saldırı yine etkili olabilir.

Kısıtlama üretim hücresine uygulanır. Çok hücreli saksının bir kısmı kapsandı diye bütün saksıyı kalıcı silmek doğru değildir. Mevcut boss hava etkileriyle birlikte çalışabilir; round/run sonunda geçici kayıtlar temizlenir.

Aşılı Kabuk'ta A'yı kırmak A'yı korur; diğer taraf kapanır. Seçim yapılmazsa tanımlı varsayılan A kapanması uygulanır. Aynı saldırı iki hedefe değerse deterministik öncelik vardır. Bu seçim bir boss ödülü kartı değil, aktif tarla olayıdır.

Kaynak: FieldEventPlanSO; FieldEventDirector; ThornSiegeEvent; InvasiveSproutEvent; GraftedShellEvent; WanderingBountyEvent.

---PAGE---
# 19 / Gezgin Bereket ve olayların sunumu

Gezgin Bereket önceki “6 direct hasat yap, ödül kazan” görevinden değiştirilmiştir. Güncel kural **hareketli pasif bölge**dir. Aktif 3×3 alandaki gerçek hasat kaynak ve XP'yi ×2 verir; direct ve davranış hasadı ikisi de yararlanır. Harvest Score değişmez. Etki hasat anındaki hücreden okunur; bitki havuza girip çıkarken üzerinde kalıcı bonus taşınmaz.

Boş üretim yuvalarının bekleme aralığı yarıya iner; dolu yuva yeniden üretmez, sahte anlık bitki spawn edilmez. Alan uygun komşu açık hücreye ana yönlerden hareket eder. Hareket aralığı en az 10 saniyedir. Aktif bölüm round sonuna kadar sürer; eski 30 saniyelik süre veya seviye büyüdükçe artan buff kullanılmaz.

**Görsel ve güç zamanını ayır:** Preparing sırasında altın renkli bitki sunumu/alan belirtisi görülebilir, fakat mekanik bonus Active evresinde başlar. Görseli görünen her karede buff uygulanmış kabul eden test yanlış olur.

Olayların özgün low-poly/comic sunumları üretilmiştir: Diken'de kök, toprak ve kırılma aşamaları; Filiz'de pembe çekirdekli uzun gövde ve küçük sürgünler; Kabuk'ta sıcak A ve soğuk B siluetleri; Bereket'te dokuz küçük toplayıcı robot, ışınlar ve tek çevresel altın perde.

Önceki eksik listesinde bulunan Hit bağlantısı, çıkış animasyonunu beklemeden mantığı bitirme, A/B varyantı, efekt başına süre, ödül görsel varyantı, zemin belirtisi/üst uyarı ayrımı ve ses yoğunluk sınırı için altyapı raporları vardır. Bunları hâlâ hiç yapılmamış sayma. Her içerikte görsel kalite/okunurluk kabulü ise ayrı insan Play işidir.

Görsel, mekanik otorite değildir. Hide/çökme animasyonu devam ederken hücrenin kısıtı kalkabilir. Damage olayından bağımsız dekorun süresi hasarı geciktirmemeli. Aynı şekilde UI altında kalan hedef veya VFX ile örtülen kök oynanışı pratikte bozabilir; salt mantık testi bunu yakalamaz.

Kaynak: Docs/WanderingBounty/README.md; Docs/ThornSiege/README.md; ilgili olay ailelerinin üretim belgeleri.

---PAGE---
# 20 / Teknik mimari ve veri akışı

Unity sürümü 6000.3.14f1; Universal Render Pipeline paketi 17.3.0. UGUI/TMP tabanlı arayüz, C# MonoBehaviour yöneticileri ve ScriptableObject verileri kullanılır. Manifestte bir paketin bulunması o özelliğin oyunda kullanıldığı anlamına gelmez; örneğin bir ağ paketinden multiplayer sonucu çıkarılmamalı.

| Katman / sınıf | Sorumluluk |
|---|---|
| RunProfileSO / RunBalanceSO | Run uzunluğu, takvim, süre, havuz, denge seti ve profil kapıları. |
| GameManager / RoundManager / RunCalendar | Durum geçişleri, round akışı, dönem ve boss tarihi hesabı. |
| GridManager / PlanterBrain / PlantHealth | Hücreler, saksının stat/üretimi ve bitkinin hasar/yaşamı. |
| StatManager / StatCalculator | Global modifier, stat işlemleri, önbellek ve yerel hesapla ortak sözleşme. |
| ProgressionManager / CardSelectionUI | XP/level, haklar, yeni tile, yükseltme ve temel güç teklifleri. |
| BossRewardManager / RunPower | Ödül uygunluğu, stack/bedel, direct/behavior/ritim etkileri. |
| HarvestChain / HarvestLink / ChainRandom | Kök kimliği, nesil, tekrar önleme, kuyruk/bütçe ve ayrı RNG. |
| RunEffects / RunEffectSO / AttackFeed | Yeni ödül davranışlarının kurulması, saldırı bilgisi ve yaşam döngüsü. |
| FieldEventDirector / FieldEvent türevleri | Geç oyun olayı seçimi, özel hedef ve hücre etkileri. |
| LateGamePresenter / LateGameArtSO / LateGameAudio | Model/VFX/ses sunumu, katalog, havuz ve ses sınırları. |
| MetaSave / başlangıç kataloğu | Kalıcı açılımlar, görev sonucu ve başlangıç seçimi. |
| NumericSafety / ObserverEvents | Sayısal emniyet ve ölçüm dinleyicisini oynanıştan ayırma. |

Veri akışı kabaca: profil → denge/havuz/takvim → runtime yöneticileri → saldırı/hasat → ekonomi/XP/ödül tetikleri → UI ve VFX. Sunum bildirimleri mevcut gerçek hasar olaylarından türetilmelidir; VFX callback'i hasarın tek yürütücüsü olmamalıdır.

Projede singleton yöneticiler vardır; bu belge bütün kodun kusursuz SOLID olduğu iddiası taşımaz. Yeni içerikte dar sorumluluklar, mevcut doğrulama yolu, açık veri ve kontrollü yaşam döngüsü korunmalıdır. Yalnız bir ödül eklemek için merkezi yöneticileri sınırsız koşul zincirine büyütmekten kaçınılmalıdır.

---PAGE---
# 21 / Sahne, demo ve profil ayrımı

**GameScene tam oyun hedefidir.** Kullanıcı tam sürüm değişikliklerinin DemoScene'e uygulanmamasını özellikle istedi. Profiller eski deneyleri karşılaştırabilmek için tutulur; hepsini tek yeni dengeye dönüştürmek beklenen davranış değildir. RunProfileSelection dosyası okuma sırasında GecOyunV1'i seçmektedir; bu geçici editör seçimi genel bir tasarım kuralı değildir.

DemoScene belgelenen hâliyle ayrı 20 round erken alfa deneyimidir. Kaynak AlphaDengeV1 profilini çalışma anında kopyalar; ana asset veya gerçek seçim dosyasını yeniden yazmamalıdır. Boss tarihleri 3, 6, 10, 13, 16, 20; R20 sonrası zaferdir. Aktif süre 19:10'dur.

Demoda ödül aşamaları sıkıştırılmıştır: R3 erken, R6/R10 orta, R13/R16/R20 güçlü. Teklif yalnız mevcut aşamadan gelir. Kasırga ve bumerang erişimi, kartları ve tetik yolları engellenir. Varsayılan başlangıç seçimi kullanılır; tam oyun kaydından çiftçi/tırpan seçimi uygulamaz veya görev açılımı yazmaz.

Demo ilk ayarları kaynak hedeflerin %50'si, can eğrisinin %75'i şeklinde belgelenmiştir. Bunlar nihai denge onayı değildir. Bazı tam oyun kuralları, örneğin yeni temel güç/dışlama davranışı, demo bağlamında özellikle ayrı tutulmuştur. Demo ile GameScene aynı havuzu referans ediyor görünse bile runtime kapılarını oku.

WebGL demo üretim komutu ve 5 Ekim tarihli paket vardır. Bu PDF yeni build üretmemiştir. İlk tarayıcı kontrolü, 20 round'un insan tarafından baştan sona ve farklı donanımlarda test edildiği anlamına gelmez.

Sahne temizliğinde eski kullanılmayan buton/Legacy Stats/Sound Manager öğeleri kaldırılmış olarak raporlanır; 121 zemin ve 140 skill yuvası korunmuştur. Yeni refactor'da yalnız hiyerarşide görünmüyor diye bir bağlantıyı silme: serialized referans, Resources yüklemesi ve çalışma anı kurulumunu birlikte kontrol et.

Kaynak: Docs/DemoScene.md; DemoSceneSettings; RunProfileSelection.asset; ilgili sahne ve refactor raporları.

---PAGE---
# 22 / Görsel dil, ses ve performans

Hedef dil low-poly, toon ve comic'tir. UI referansı kalın koyu mürdüm çizgi, kesilmiş kâğıt hissi, sert kaydırılmış gölge ve sınırlı halftone kullanır. Parlak plastik görünümden kaçınılır. Ortak dil bütün öğelerin aynı şekle dönüştürülmesi anlamına gelmez.

**Güncel UI istisnaları:** 8 Ekim kararları round eylem butonlarını eski yuvarlak toon sprite'a, koordinatları sade mavi plakalara, hazırlık/özet panelini eski yuvarlatılmış çerçeveye döndürür. Bütün ekranları yeniden açılı comic tasarıma zorlamak yanlış olur. Docs/UI-Art-Direction.md uygulanacak kaynaktır.

Sunumda gerçek alanı göstermek önemlidir. Artçı'nın kırmızı çizgisi ve zincirin kısa mor saksı çizgisi mekanik tanılama/geri bildirim çözümüdür; nihai patlama ve yayılım hissi için ayrıca polish gerekebilir. Her vurulan bölgede okunabilir temas, bitki tepkisi ve uygun ses istenir; ekranı tamamen örten sürekli efekt istemez.

Üretilmiş yeni aileler paylaşılan mesh/material, merkezi güncelleme ve sınırlı dekor/ses havuzlarını kullanır. Anten/hilal/olaylarda her hücreye Update, Animator, fizik veya dinamik ışık eklemek mevcut yaklaşım değildir. Paylaşılan ses havuzu 8 AudioSource ile klip bazlı sınır/tekrar aralığı kullanır.

**Performans sınırları:** ilk kullanımda havuz ısınması/nesne oluşturma olabilir; “hiç Instantiate yok” genel garantisi yoktur. Batch CPU ölçümü gerçek GPU/fill-rate maliyetini kanıtlamaz. Robot ışınları, alan perdeleri, patlama üst üste binmesi ve bitki tint ek çizimleri gerçek GameScene/Profiler koşulunda değerlendirilmelidir.

F9 performans kaydı aktif davranış, kapasite ve reddedilen tetikleri ayırır. Tek kare takılması, ortalama FPS iyi olsa bile ayrıca incelenir. Görsel atlama sayacı ile hasat/skor/XP sonuçları birlikte yorumlanmalıdır; dekor atlaması doğrudan hasar kaybı demek değildir.

Kaynak: Docs/UI-Art-Direction.md; Docs/PerformanceFixes.md; LateGameArt aile belgeleri; FullGame-PlaytestFix-20261006.md.

---PAGE---
# 23 / Ne kanıtlandı, ne henüz bilinmiyor?

Projede geniş bir izole otomatik doğrulama ve bot ölçüm altyapısı vardır. Eski profil teklif dizisinin aynı kalması, kontrollü hasar/stat testleri, round yaşam döngüsü ve seed eşli karşılaştırmalar önemli kanıtlardır. Bunlar farklı insan oyuncuların eğlence, okunurluk ve öğrenme deneyimini tek başına kanıtlamaz.

P8'in tarihsel 110 run setinde 72 kazanma, 33 kota, 5 boss kaybı raporlanmıştır. Kayıplar R13/R16'da yoğunlaşmış, o eşiği geçenlerde geç oyun gevşemiştir. Bu sonuç **son küçük temel güç kartları, yeni 13 ödül, olaylar ve 10 Ekim revizyonlarından önceki dengeyi** anlatır; bugünkü kazanma oranı diye sunulmamalıdır.

P6/P8 laboratuvarında zincirin toplam hasat katkısı küçükken kullanıcının Play deneyiminde zincir güçlü ve keyifli hissettirmiştir. Bu çelişki değildir: tarla düzeni, aynı hedefin zaten boşalması, diğer hasarın yerini alma, ödülün alındığı zaman ve görsel etki farklı soruları ölçer. Yeni denge çalışması bu koşulları kayıt altına almalıdır.

| Durum | Açıklama |
|---|---|
| Uygulanmış | Takvim, ödül aşamaları/bedelleri, zincir, küçük temel güç, 12 hareketli davranış sınırı, 13 yeni ödül, 4 olay. |
| Üretim + test raporu var | Diken/Filiz/Kabuk/Bereket, anten ve hilal sunum aileleri; güncel Hilal fare rotası. |
| Yeniden insan değerlendirmesi gerekir | Geç oyun dikkat döngüsü, seçim süresi, güncel güç eğrisi, efekt okunurluğu, gerçek donanım performansı. |
| Tamamlanmış sayma | Özel R50 canavar finali, R51+ endless seçimi/sayısal modeli, kapsamlı son manuel test sahnesi, bütün ödüllerin nihai VFX'i. |

Bu PDF hazırlanırken Unity testi çalıştırılmadı. Kaynak dosyalarının okunması ve PDF metin/render kontrolü yapıldı. Otomatik test sayıları yalnız ilgili tarihli rapora bağlanarak kullanılmalıdır; eski “153 geçti” cümlesi son kodun otomatik sertifikası değildir.

---PAGE---
# 24 / Sonraki AI için çalışma sözleşmesi

1. Önce aktif profil, bağlı balance/pool/effect asset'leri, GameScene/Demo ayrımı ve son tarihli aile belgesini oku. Eski sohbetten mekanik tamamlamaya çalışma.
2. “Kart”, “level hakkı”, “tile”, “saksı”, “isabet” ve “hasat” kavramlarını ayrı tut. Özellikle Hilal isabet sayar; Hasat Ritmi direct hasat sayar.
3. Yeni reward saldırısını otomatik crit/behavior/chain'e sokma. Ön koşul, tekrar sınırı, bütçe, cooldown, havuz ve round temizliği açık olmalı.
4. Kaynak/hedef stat sahipliğini doğrula. Aynı multiplier'ı ana darbe ve echo üzerinde ikinci kez uygulama. Tooltip ile gerçek hesap aynı veriden gelmeli.
5. Profil seçimini, gerçek meta kaydını ve açık editörü kullanıcı istemeden değiştirme. Testi izole kopyada/MemoryOnly ile çalıştır. Başka oturumların kirli dosyalarını geri alma.
6. Yeni dış font, model, ses, paket veya koddan önce lisansı kontrol et; ticari kullanım, ücret, görünür kredi ve dağıtım bildirimi koşullarını kullanıcıya açıkla. Docs/ThirdParty-Licenses.md kaydını tut.
7. Yeni içerik eklerken mevcut ödülü farklı adla ikinci kez üretme. Aynı iş varsa onu koru veya açık kapsamlı değiştir. Rezonans “patlatma” fikrini geri getirme.
8. Uygulama, otomatik test, bot denge ölçümü ve insan Play onayını ayrı raporla. Çalışan sistem “denge bitti” demek değildir.

**Öncelikli polish/devam alanları:** Artçı gerçek alan/temas VFX'i; zincirin kaynaktan hedefe anlaşılır yayılımı; kalan ödüllerin görsel varyantları; ekran kalabalığı ve UI örtüşmesi; final/endless açık işleri; kullanıcıya statların gerçekten doğru yere uygulandığını gösterecek kapsamlı manuel test ortamı.

**Karar gerektiren fikirleri mevcut özellik diye yazma:** daha sık geç oyun boss'u, boss ödülünü fiziksel düşen eşya üzerinden sunma, büyük saksı üretim cezası, hilal için davranış tetikleme yükseltmesi ve yeni kapasite artış kartının gerçek havuza girişi. Bu fikirler ayrıca tasarım/onay ve uygulama takibi ister.

Belgenin sonraki eki gerçek 32 ödülün bağlı asset envanteridir. Kodla değişiklik yapılacaksa bu PDF özetinin yanında kaynak dosyayı tekrar oku; anlık görüntü yeni değişiklikleri kendiliğinden içermez.


---PAGE---

# EK A1 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## Keskin Bıçak / Erken

Kimlik: keskin_bicak. En çok stack: 3. Temel ağırlık: 1.

Yalnız kendi vuruşun. Davranış hasarı değişmez.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/KeskinBicak_V1.asset

## Hızlı Bilek / Erken

Kimlik: hizli_bilek. En çok stack: 3. Temel ağırlık: 1.

Vuruşlar arası süre kısalır. Taban 0,10 sn.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/HizliBilek_V1.asset

## Bereketli Toprak / Erken

Kimlik: bereketli_toprak. En çok stack: 3. Temel ağırlık: 1.

Bütün saksılar. Yalnız tarlan boşalıyorsa çıkar (son round ortalama doluluk %65'in altında). Taban 0,50 sn.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/BereketliToprak_V1.asset

## Nadir Tohum / Erken

Kimlik: nadir_tohum. En çok stack: 3. Temel ağırlık: 1.

Bütün saksılar. Yüzde puanı olarak eklenir; en çok 95 puan.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/NadirTohum_V1.asset

## Bilgi Filizi / Erken

Kimlik: bilgi_filizi. En çok stack: 2. Temel ağırlık: 1.

Hasattan gelen XP. Round 30 boss'undan sonra çıkmaz.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/BilgiFilizi_V1.asset

## Elektrik Anteni / Erken

Kimlik: elektrik_anteni. En çok stack: 1. Temel ağırlık: 1.

Anten tarlanın kenarında durur; saksı ve tile yeri kaplamaz. Vuruşu senin vuruşun sayılmaz: kritik atmaz, davranış tetiklemez.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/ElektrikAnteni_G1.asset

---PAGE---

# EK A2 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## Hilal Darbesi / Erken

Kimlik: hilal_darbesi. En çok stack: 1. Temel ağırlık: 1.

Boş savuruş sayılmaz. Hilal vurduğun yerden tarlanın ortasına doğru ilerler; senin vuruşun sayılmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/HilalDarbesi_G1.asset

## Kıvılcım / Orta

Kimlik: kivilcim. En çok stack: 3. Temel ağırlık: 1.

Patlama · kasırga · bumerang · elektrik. Şansı olmayan saksıya şans vermez; en çok %100.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/Kivilcim_V1.asset

## Ağır Darbe / Orta

Kimlik: agir_darbe. En çok stack: 2. Temel ağırlık: 1.

Yalnız kendi vuruşun kritiği.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/AgirDarbe_V1.asset

## Kritik Göz / Orta

Kimlik: kritik_goz. En çok stack: 3. Temel ağırlık: 1.

Yalnız kendi vuruşun. Yüzde puanı olarak eklenir.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/KritikGoz_V1.asset

## Altın Hedef / Orta

Kimlik: altin_hedef. En çok stack: 2. Temel ağırlık: 1.

Yalnız kendi vuruşun; Common ve Uncommon değişmez. Davranış hasarı değişmez.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/AltinHedef_V1.asset

## Yıkım Gücü / Orta

Kimlik: yikim_gucu. En çok stack: 3. Temel ağırlık: 1.

Patlama · kasırga · bumerang · elektrik. Kendi vuruşun değişmez.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/YikimGucu_V1.asset

---PAGE---

# EK A3 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## İletken Hat / Orta

Kimlik: iletken_hat. En çok stack: 1. Temel ağırlık: 1. Ön koşul: Elektrik Anteni.

İkinci antenin yerini sen seçersin; hat kurulamayan konum seçilemez. Hat vuruşu senin vuruşun sayılmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/IletkenHat_G1.asset

## Büyüyen Ay / Orta

Kimlik: buyuyen_ay. En çok stack: 1. Temel ağırlık: 1. Ön koşul: Hilal Darbesi.

Büyüme yalnız aynı dalganın kalan adımlarına uygulanır; sonraki dalga baştan başlar.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/BuyuyenAy_G1.asset

## Kritik Ark / Orta

Kimlik: kritik_ark. En çok stack: 1. Temel ağırlık: 1.

Ark, vuruşunun değmediği bir bitkiye gider. Kritik atmaz, yeni ark doğurmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/KritikArk_G1.asset

## Hasat Yankısı / Orta

Kimlik: hasat_yankisi. En çok stack: 1. Temel ağırlık: 1.

Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Yankı yeni yankı doğurmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/HasatYankisi_G1.asset

## Taşan Hasar / Orta

Kimlik: tasan_hasar. En çok stack: 1. Temel ağırlık: 1.

Yalnız kendi vuruşunun öldürdüğü bitkiden başlar. Aktarım kritik atmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/TasanHasar_G1.asset

## Nadir Reaktör / Orta

Kimlik: nadir_reaktor. En çok stack: 1. Temel ağırlık: 1.

Patlama · kasırga · bumerang · elektrik. Şansı olmayan saksıya şans vermez; en çok %100.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/NadirReaktor_G1.asset

---PAGE---

# EK A4 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## Kritik Patlama / Orta

Kimlik: kritik_patlama. En çok stack: 1. Temel ağırlık: 1.

Kritik zarı eskisi gibi bitki başına atılır. Patlama senin vuruşun sayılmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/KritikPatlama_G1.asset

## Açgözlü Hasat / Orta

Kimlik: acgozlu_hasat. En çok stack: 1. Temel ağırlık: 1.

Boş savuruş hakkı harcamaz. Güçlenen vuruş yeni hak kazandırmaz. Davranışlara geçmez.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/AcgozluHasat_G1.asset

## Geniş Savuruş / Güçlü

Kimlik: genis_savurus. En çok stack: 2. Temel ağırlık: 1.

Yalnız kendi vuruşun. İmleç halkası büyür; davranış alanı değişmez.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/GenisSavurus_V1.asset

## Canavar Kesimi / Güçlü

Kimlik: canavar_kesimi. En çok stack: 1. Temel ağırlık: 1.

Yalnız kendi vuruşun. Round 25 boss'undan sonra çıkar; bir kez alınır.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/CanavarKesimi_V1.asset

## Fırtına Bileği / Güçlü

Kimlik: firtina_bilegi. En çok stack: 1. Temel ağırlık: 1.

Round 25 boss'undan sonra çıkar; bir kez alınır. Taban 0,10 sn.

Asset: Assets/ScriptableObjects/BossRewards/DengeV1/FirtinaBilegi_V1.asset

## Artçı Patlama / Güçlü

Kimlik: artci_patlama. En çok stack: 1. Temel ağırlık: 1.

Şansı tutan her normal patlamadan sonra aynı yerde ikinci bir patlama. Artçı yeni artçı ya da zincir başlatmaz; öldürdükleri davranış hasadıdır.

Asset: Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/ArtciPatlama_E1.asset

---PAGE---

# EK A5 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## Çifte Akım / Güçlü

Kimlik: cifte_akim. En çok stack: 1. Temel ağırlık: 1.

Şansı tutan her normal elektrikten sonra aynı saksıdan ikinci bir dalga. Yeni şans atılmaz; ikinci dalga zincir başlatmaz.

Asset: Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/CifteAkim_E1.asset

## Hasat Ritmi / Güçlü

Kimlik: hasat_ritmi. En çok stack: 1. Temel ağırlık: 1. Dışlama grubu: harvest_playstyle.

Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Hak hazır olunca sıradaki vuruşun güçlenir (imleç halkası büyür). Ek saldırı üretmez; davranışlara geçmez.

Asset: Assets/ScriptableObjects/BossRewards/AlphaDengeV1/HasatRitmi_A1.asset

## Bereketli Öğrenim / Güçlü

Kimlik: bereketli_ogrenim. En çok stack: 1. Temel ağırlık: 1. Dışlama grubu: secim_hakki_takasi.

Davranış hasarı değişmez.

Asset: Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/BereketliOgrenim_B1.asset

## Davranışa Adanış / Güçlü

Kimlik: davranisa_adanis. En çok stack: 1. Temel ağırlık: 1. Dışlama grubu: secim_hakki_takasi.

Patlama · elektrik · kasırga · bumerang. Kendi vuruşun değişmez.

Asset: Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/DavranisaAdanis_B1.asset

## Zincir Hasat / Güçlü

Kimlik: zincir_hasat. En çok stack: 1. Temel ağırlık: 1. Dışlama grubu: harvest_playstyle.

Her saksı ve davranışı bir saldırının zincirinde bir kez denenir. Artçı ve ikinci dalga zincire katılmaz.

Asset: Assets/ScriptableObjects/BossRewards/AlphaDengeV1/ZincirHasat_A1.asset

## Kapalı Devre / Güçlü

Kimlik: kapali_devre. En çok stack: 1. Temel ağırlık: 1. Ön koşul: İletken Hat.

Ek vuruş kendini yeniden başlatamaz. Davranışı olan saksı yoksa çalışmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/KapaliDevre_G1.asset

---PAGE---

# EK A6 / Bağlı boss ödülleri

Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.

## Dolunay / Güçlü

Kimlik: dolunay. En çok stack: 1. Temel ağırlık: 1. Ön koşul: Büyüyen Ay.

Duraklama yalnız kendi vuruşunu bekletir; imleç ve oyun durmaz. İkinci dalga büyümez.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/Dolunay_G1.asset

## Aşırı Yükleme / Güçlü

Kimlik: asiri_yukleme. En çok stack: 1. Temel ağırlık: 1.

Ek proc zincire katılmaz ve yeni zar atmaz. Kasırga / bumerang aktif sınırı aşılmaz.

Asset: Assets/ScriptableObjects/BossRewards/GecOyunV1/AsiriYukleme_G1.asset

---PAGE---

# EK B / Yeni ödüllerin veri parametreleri

İsimler C# / asset alan adlarıdır; bunlar ham ayarlardır. Kodun profil kapıları ve güncel Hilal fare rotası bu alanlara ek anlam verir. Bir alanın dosyada durması hâlâ kullanıldığı anlamına gelmeyebilir.

## Elektrik Anteni

reach=3; interval=2.5; damage=0.6

## Hilal Darbesi

everyAttacks=6; steps=4; stepSeconds=0.07; damage=0.8

## İletken Hat

interval=4; damage=0.45; minCells=3; secondAntennaPulses=1

## Büyüyen Ay

maxGrowth=3; damagePerGrowth=0.25; maxHalfWidth=1

## Kritik Ark

damageRatio=0.3; rangeCells=2; maxArcsPerAttack=3

## Hasat Yankısı

minHarvests=3; damage=0.5; maxCells=3; delay=0.12

## Taşan Hasar

ratio=0.5; maxTransfers=2; rangeCells=1; maxSourcesPerAttack=4

---PAGE---

# EK B / Yeni ödüllerin veri parametreleri

İsimler C# / asset alan adlarıdır; bunlar ham ayarlardır. Kodun profil kapıları ve güncel Hilal fare rotası bu alanlara ek anlam verir. Bir alanın dosyada durması hâlâ kullanıldığı anlamına gelmeyebilir.

## Nadir Reaktör

minRarity=2; chanceBonus=0.25; seconds=4

## Kritik Patlama

minCrits=3; damage=0.5; radiusFactor=1

## Açgözlü Hasat

minHarvests=3; damageMultiplier=1.5

## Kapalı Devre

extraDelay=0.35; extraDamage=0.6; cooldown=3; chanceFactor=1

## Dolunay

growthThreshold=2; returnDamage=0.7; returnDelay=0.15; pauseSeconds=0.35

## Aşırı Yükleme

damageFactor=0.5; delay=0.3; cooldown=2.5; maxPerAttack=3

---PAGE---

# EK B / Temel ödüllerin sayısal etkileri

Her satır tek stack değerini gösterir. MorePercent 0.25, ilgili gruba ×1,25 anlamına gelir; Flat puan ekler. Buradaki teknik alan adları, ana bölümdeki sahiplik kurallarıyla okunmalıdır.

| Ödül | Bağlı asset etkisi |
|---|---|
| Keskin Bıçak | directDamageMultiplier=1.25 |
| Hızlı Bilek | AttackSpeed / Player: MorePercent -0.15 |
| Bereketli Toprak | PlantSpawnRate / Planter: MorePercent -0.15 |
| Nadir Tohum | RareSpawnChance / Planter: Flat 10 |
| Bilgi Filizi | XPGainMultiplier / Planter: MorePercent 0.25 |
| Kıvılcım | ExplosionChance / Planter: MorePercent 0.35; TornadoChance / Planter: MorePercent 0.35; BoomerangChance / Planter: MorePercent 0.35; ElectricChance / Planter: MorePercent 0.35 |
| Ağır Darbe | CritMultiplier / Player: Flat 0.75 |
| Kritik Göz | CritChance / Player: Flat 0.12 |
| Altın Hedef | rareDirectMultiplier=1.5 |
| Yıkım Gücü | behaviorDamageMultiplier=1.3 |

---PAGE---

# EK B / Temel ödüllerin sayısal etkileri

Her satır tek stack değerini gösterir. MorePercent 0.25, ilgili gruba ×1,25 anlamına gelir; Flat puan ekler. Buradaki teknik alan adları, ana bölümdeki sahiplik kurallarıyla okunmalıdır.

| Ödül | Bağlı asset etkisi |
|---|---|
| Geniş Savuruş | AreaRadius / Player: MorePercent 0.32 |
| Canavar Kesimi | directDamageMultiplier=1.6 |
| Fırtına Bileği | AttackSpeed / Player: MorePercent -0.3 |
| Artçı Patlama | echo=1; echoDelay=0.2; echoDamage=1; echoRadius=1.666667; echoReach=0 |
| Çifte Akım | echo=2; echoDelay=0.15; echoDamage=1; echoRadius=1; echoReach=4 |
| Hasat Ritmi | rhythmHarvests=5; rhythmDamage=1.75; rhythmRadius=1.6 |
| Bereketli Öğrenim | directDamageMultiplier=0.8; levelChoiceDelta=1 |
| Davranışa Adanış | behaviorDamageMultiplier=1.5; levelChoiceDelta=-1 |
| Zincir Hasat | 2 ek nesil; şans/hasar 0.75, 0.5; bütçe 32; kare başı 8 |

---PAGE---

# EK C / Tile aileleri, can ve seçenekler

| Tile türü | Ana rol |
|---|---|
| Fertile / Bereketli | Üretim aralığını azaltma; taban sonrası nadirlik dönüşümü. |
| Water / Su | XP yönü. |
| Crystal / Kristal | Bitki nadirliği. |
| Energy / Enerji | Harvest Score yönü. |
| Damage / Hasar | Saksıdaki bitkilere direct hasar. |
| Duplicate | Hasat kaynaklarını çoğaltma şansı. |
| Explosive / Patlama | Normal patlama davranışı şansı. |
| Tornado / Kasırga | Hareketli kasırga davranışı şansı. |
| Boomerang / Bumerang | Gidiş-dönüş davranışı şansı. |
| Electric / Elektrik | Çapraz elektrik davranışı şansı. |

Tile yükseltmesi her seviyede başlangıç zar değerinin %25'i kadar ilave etki verir; Sv 3 başlangıç etkisinin 1,75 katıdır. Bu işlem üç kez ×1,25 çarpma değildir. Kartın gösterdiği zar sonucu uygulamaya aktarılır; yerleşimde bağımsız ikinci zar atıp görünen etkiyi değiştirmemelidir.

Aktif can eğrisinin Common referans noktaları: R1=20, R5=25, R10=31, R15=44, R20=67, R25=99, R30=146, R35=212, R40=306, R45=440, R50=627. Rarity çarpanları 1 / 1,5 / 2 / 2,5 / 3. Asıl bitki sonucu plant verisi ve ortak can hesaplayıcısından gelir; bu referans tabloyu her prefab'ın mutlak canı diye kopyalama.

Güncel süre tablolu profilde 65 saniye, eski 60 saniye tavanına takılmaz ve ek tempo hızına çevrilmez. Süre stat düğümleri bu profilin sabit tablosunu uzatmaz. Eski profillerdeki süre/tempo kuralları ayrıca korunabilir.

Oyuncu ayarları çözünürlük, pencere modu, VSync, FPS sınırı, kalite, arka plan efektleri; ana/müzik/efekt sesleri ve odak dışı sessizlik; sarsıntı, kamera hareketi, hasar sayıları, koordinatlar, nadirlik auraları, round sonu yavaşlama ve ekran parlamalarını içerir. Ayarlar PlayerPrefs'te saklanır; meta açılım kaydıyla aynı dosya değildir.

Elektriğin zamana bağlı şarj deneyi kaynakta kalmış olabilir ama emekli edilmiştir. Güncel profil KillChance yolunu kullanır; ElectricChargeManager dosyasının varlığından aktif şarj mekaniği çıkarma.


---PAGE---

# EK C / Kaynağa nasıl geri dönülür?

Bu PDF'de dosya adları okunabilirlik için çoğunlukla kısa yazılmıştır. Tam proje göreli yollar ve SHA-256 değerleri Kaynak-Anlik-Goruntu-20261010.json dosyasındadır. Aynı klasördeki Boss-Odul-Envanteri-20261010.json, 32 ödülün okunabilir makine envanteridir.

## Öncelik sırası

1. Çalışan profilin bağlı asset'i ve güncel C# uygulaması.
2. Güncel aile/revizyon belgesi: özellikle CrescentFamily/CursorRoute.md ve WanderingBounty/README.md.
3. Tarihli bölüm teslimi ve test çıktısı.
4. Eski tasarım belgesi ve sohbet fikri.

Eski belgeyi kaynak listesinden silmek gerekmez; geçerlilik alanı belirtilmelidir. Örneğin CrescentFamily/README.md, 9 Ekim tam satır davranışını tarihsel olarak saklarken üstte 10 Ekim fare rotasına yönlendirir. FinalSkillTree'nin eski maliyetleri profil override'ı okunmadan güncel fiyat diye sunulamaz.

## Değişiklik yapmadan önce cevaplanacak sorular

- İlgili kural GameScene'e mi, DemoScene'e mi, bütün profillere mi ait?
- Bu olay isabet mi, hasat mı, direct mi, davranış mı, ödül saldırısı mı?
- Hangi saksının statı hesaplanıyor; efektin kendi hasarı tekrar çarpılıyor mu?
- Sonraki frame/round/run'a kalan hangi kuyruk, referans ve görsel var?
- Test gerçek oyunu mu oynuyor, yoksa skor/XP'yi elle vererek akışı mı doğruluyor?
- Aynı seed karşılaştırmasında RNG tüketimi değişti mi?

## Belge paketi

PDF okunabilir ana belgedir. Aynı adlı Markdown, AI araçları için metin sürümüdür. JSON dosyaları kaynak/ödül kimliklerini taşır. Bu dosyalar yeni oyun sistemi oluşturmaz; belge üretimi sırasında oyun kodu değiştirilmemiştir.

Font: yerel araç paketindeki DejaVu Sans. Türkçe gömülü yazı tipi olarak kullanıldı; oyun fontu veya bağımlılığı eklenmedi. Bitstream Vera / Arev izin metni aşağıdadır ve ayrıca DejaVuSans-LICENSE.txt olarak sağlanır. Ücretsiz ticari kullanım; font tek başına satılamaz; telif/izin bildirimi korunur. Oyun içi/jenerik kredi şartı yoktur.


---PAGE---

# EK D / Gömülü font lisans bildirimi

Copyright (c) 2003 by Bitstream, Inc. All Rights Reserved. Copyright (c) 2006 by Tavmjong Bah. All Rights Reserved. DejaVu changes are in public domain

Fonts are (c) Bitstream (see below). DejaVu changes are in public domain. Glyphs imported from Arev fonts are (c) Tavmjung Bah (see below)

Bitstream Vera Fonts Copyright ------------------------------

Copyright (c) 2003 by Bitstream, Inc. All Rights Reserved. Bitstream Vera is a trademark of Bitstream, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy of the fonts accompanying this license ("Fonts") and associated documentation files (the "Font Software"), to reproduce and distribute the Font Software, including without limitation the rights to use, copy, merge, publish, distribute, and/or sell copies of the Font Software, and to permit persons to whom the Font Software is furnished to do so, subject to the following conditions:

The above copyright and trademark notices and this permission notice shall be included in all copies of one or more of the Font Software typefaces.

The Font Software may be modified, altered, or added to, and in particular the designs of glyphs or characters in the Fonts may be modified and additional glyphs or characters may be added to the Fonts, only if the fonts are renamed to names not containing either the words "Bitstream" or the word "Vera".

This License becomes null and void to the extent applicable to Fonts or Font Software that has been modified and is distributed under the "Bitstream Vera" names.

The Font Software may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself.

THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL BITSTREAM OR THE GNOME FOUNDATION BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

Except as contained in this notice, the names of Gnome, the Gnome Foundation, and Bitstream Inc., shall not be used in advertising or otherwise to promote the sale, use or other dealings in this Font Software without prior written authorization from the Gnome Foundation or Bitstream Inc., respectively. For further information, contact: fonts at gnome dot org. 

Arev Fonts Copyright ------------------------------

Copyright (c) 2006 by Tavmjong Bah. All Rights Reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy of the fonts accompanying this license ("Fonts") and associated documentation files (the "Font Software"), to reproduce and distribute the modifications to the Bitstream Vera Font Software, including without limitation the rights to use, copy, merge, publish, distribute, and/or sell copies of the Font Software, and to permit persons to whom the Font Software is furnished to do so, subject to the following conditions:

The above copyright and trademark notices and this permission notice shall be included in all copies of one or more of the Font Software typefaces.

The Font Software may be modified, altered, or added to, and in particular the designs of glyphs or characters in the Fonts may be modified and additional glyphs or characters may be added to the Fonts, only if the fonts are renamed to names not containing either the words "Tavmjong Bah" or the word "Arev".

This License becomes null and void to the extent applicable to Fonts or Font Software that has been modified and is distributed under the  "Tavmjong Bah Arev" names.

The Font Software may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself.

THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL TAVMJONG BAH BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

Except as contained in this notice, the name of Tavmjong Bah shall not be used in advertising or otherwise to promote the sale, use or other dealings in this Font Software without prior written authorization from Tavmjong Bah. For further information, contact: tavmjong @ free . fr.

http://dejavu.sourceforge.net/wiki/index.php/License