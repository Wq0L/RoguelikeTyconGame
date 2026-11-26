# AI kokan yazılar: tam liste ve öneriler (24–26 Kasım 2026)

Oyunda AI yazısı gibi duran her şey bu listede: adlar, cümleler, ödül açıklamaları, ayarlar ve Eri'nin konuşmaları.
Her satırda **şu an** ve yerine önerilen **doğal Türkçe** yan yana.

| Bölüm | Ne var | Numaralar |
|---|---|---|
| A–H | Arayüz cümleleri, ödül kuralları ve notları, kapris yazıları, ayarlar | 1–113 |
| I | Adlar: yetenek, Sınama ödülü, Sınama, kapris, rezonans, uzmanlaşma | 114–166 |
| J | Eri'nin konuşmaları, öğretici, hikâye | 167–215 |

**Durum: uygulandı (26 Kasım).** Kullanıcı onayıyla Türkçe sütuna işlendi; kaprislerde ve Eri'nin dilinde kullanıcının düzeltmeleri geçerli. Yalnız Türkçe sütun değişti, İngilizceye dokunulmadı.
Onay vermenin en kolay yolu: "hepsi tamam" ya da "şu numaralar hariç tamam: …". Bir öneriyi beğenmezsen kendi yazını da yazabilirsin.

## Kurallar (öneriler bunlara göre yazıldı)

1. **Cümle cümle yaz.** " · " işaretiyle dizilmiş parça cümleler kısa ve düz cümlelere bölündü.
   " · " yalnız kısa değer satırlarında kalıyor: "KOTA TAMAM · 8 / 15", "Kaynak ×2 · TP ×2", tuş ipuçları.
2. **Konuşur gibi yaz.** "Yalnız kendi vuruşun." yerine "Sadece kendi vuruşunu etkiler."
   "{0}+" yerine "{0} ya da daha fazla". Noktalı virgül yerine nokta ya da virgül.
3. **Geliştirici dilini oyuncuya gösterme.** "taban", "profil", "deney", "düğüm", "zar", "birim/sn", "%65 doluluk",
   "ALPHA DENGE V1" gibi ifadeler ya sadeleşti ya da kalktı.
4. **Slogan yok, süs yok.** Sayılar ve kurallar aynen kaldı; yalnız söyleyiş değişti.

Ödül kartındaki kural yazıları kartta "Anten: …" biçiminde birleşiyor. Bu yüzden küçük harfle başlıyorlar.

---

## A. Başlangıç ekranı (çiftçi / tırpan)

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 1 | `ui.start.title` | YENİ DENEME · BAŞLANGIÇ | YENİ DENEME |
| 2 | `ui.start.subtitle` | Bir çiftçi ve bir tırpan seç · etkiler bu deneme boyunca geçerli · yeni denemede yeniden uygulanır | Bir çiftçi ve bir tırpan seç. Seçimin bu deneme boyunca geçerli. |
| 3 | `ui.start.farmer_row` | ÇİFTÇİ · genel yaklaşım | ÇİFTÇİ · nasıl oynayacağın |
| 4 | `ui.start.scythe_row` | TIRPAN · hasat biçimi | TIRPAN · nasıl keseceğin |
| 5 | `ui.start.neutral` | Nötr · avantaj ve bedel yok | Artısı da eksisi de yok |
| 6 | `ui.start.neutral.detail` | oyunun kendi dengesi | her şey olduğu gibi |
| 7 | `data.start.bahcivan.style` | Referans çiftçi: oyunun kendi dengesi. | Artısı da eksisi de yok. İlk deneme için iyi bir başlangıç. |
| 8 | `data.start.tuccar.style` | Ekonomiyle ağaca erken ulaş; skor biraz geride kalır. | Daha çok kaynak toplar, yetenek ağacına erken ulaşırsın. Skorun biraz geride kalır. |
| 9 | `data.start.standart.style` | Mevcut vuruş: hasar ve yarıçap olduğu gibi. | Alışık olduğun vuruş. Hasar ve alan değişmez. |
| 10 | `data.start.dar_kesim.style` | Dar ama ağır vuruş: değerli ve sert bitkiyi hedefle. | Alanı dar ama ağır vurur. Değerli ve sert bitkileri hedefle. |
| 11 | `ui.start.effect.harvest_resources.detail` | yalnız hasattan gelen Altın · Demir · Taş | sadece hasattan gelen Altın, Demir ve Taş |

## B. Kota ve tur yazıları

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 12 | `ui.quota.rule_boss_round` | Kota her Sınama turunun sonunda kontrol edilir; tutmazsa deneme biter | Her Sınama turunun sonunda kotaya bakılır. Tutturamazsan deneme biter. |
| 13 | `ui.quota.rule_every` | Kota her {0} turda kontrol edilir; tutmazsa deneme biter | Kotaya her {0} turda bir bakılır. Tutturamazsan deneme biter. |
| 14 | `ui.quota.last_round.detail` | {0} {1} · tutmazsa deneme biter | {0} {1}. Tutturamazsan deneme biter. |
| 15 | `ui.quota.progress.detail` | Tutmazsa deneme {0}. tur sonunda biter | Tutturamazsan deneme {0}. turun sonunda biter |
| 16 | `ui.quota.segment_closes` | Dönem {0}. tur sonunda kapanır | Dönem {0}. turun sonunda biter |
| 17 | `ui.hud.level_work` | SEVİYE KAZANIMLARI HESAPLANIYOR | SEVİYE ATLIYORSUN |
| 18 | `ui.runend.times.level_work` | seviye hesaplama {0} | seviye atlama {0} |
| 19 | `data.profile.run_tamamlandi_alpha_denge_v1.victory` | DENEME TAMAMLANDI · ALPHA DENGE V1 | DENEME TAMAMLANDI |
| 20 | `data.profile.run_tamamlandi_gec_oyun_v1.victory` | DENEME TAMAMLANDI · GEÇ OYUN V1 | DENEME TAMAMLANDI |

## C. Sınama ödülü ve uzmanlaşma ekranı

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 21 | `ui.reward.panel.tail` | Yalnız biri alınır · deneme sonuna kadar geçerli · etkisi Tur {0} ve sonrası | Birini seç. {0}. turdan itibaren deneme sonuna kadar geçerli. |
| 22 | `ui.reward.panel.tail.lower` | yalnız biri alınır · deneme sonuna kadar geçerli · etkisi Tur {0} ve sonrası | Birini seç. {0}. turdan itibaren deneme sonuna kadar geçerli. |
| 23 | `ui.reward.panel.last_tail` | Yalnız biri alınır · deneme burada biter · ödül deneme sonu listesine yazılır | Birini seç. Deneme burada bitiyor, ödül sonuç ekranına yazılır. |
| 24 | `ui.reward.panel.last_tail.lower` | yalnız biri alınır · deneme burada biter · ödül deneme sonu listesine yazılır | Birini seç. Deneme burada bitiyor, ödül sonuç ekranına yazılır. |
| 25 | `ui.reward.panel.passed` | Sınama geçildi · {0} | Sınamayı geçtin. {0} |
| 26 | `ui.reward.panel.passed_last` | Son Sınama geçildi · {0} | Son Sınamayı geçtin. {0} |
| 27 | `ui.reward.panel.empty.detail` | Bütün ödüller sınırında ya da şu an bir şey değiştirmiyor | Ödüllerin hepsi ya alındı ya da şu an işe yaramıyor |
| 28 | `ui.reward.level_choice_line` | Seviye başına seçim: {0} (temel {1}) · yeni kazanılan seviyeler | Seviye başına seçim: {0} (normalde {1}). Bundan sonra kazanılan seviyelerde geçerli. |
| 29 | `ui.spec.subtitle` | Yalnız biri alınır · deneme sonuna kadar geçerli · geri alınamaz · etkisi Tur {0} ve sonrası | Birini seç. {0}. turdan itibaren deneme sonuna kadar geçerli, geri alınamaz. |
| 30 | `ui.spec.neutral` | Bonus yok, ceza yok | Artısı da eksisi de yok |
| 31 | `ui.spec.behavior.detail` | patlama · kasırga · bumerang · elektrik | patlama, kasırga, bumerang ve elektrik |
| 32 | `ui.spec.resources.detail` | hasattan gelen Altın · Demir · Taş | hasattan gelen Altın, Demir ve Taş |

## D. Ödül kartı kuralları (kartta "Etiket: …" diye başlar)

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 33 | `ui.effect.antenna` | kenardaki anten, baktığı yöndeki ilk {0} hücreye {1} sn'de bir vurur · hasat hasarının %{2}'i | {1} sn'de bir, baktığı yöndeki ilk {0} hücreye hasat hasarının %{2}'i kadar vurur |
| 34 | `ui.effect.closed_circuit` | hat, davranışlı saksının bitkisini hasat ederse o saksının davranış şansıyla ek hat vuruşu · hasarın %{0}'i · bekleme {1} sn | hat, davranışı olan bir saksının bitkisini hasat ederse o saksının şansıyla bir kez daha vurur (hasarın %{0}'i, {1} sn bekleme) |
| 35 | `ui.effect.line` | aynı hizada karşı kenara ikinci anten · iki anten arasındaki hücrelere {0} sn'de bir hat vuruşu · hasat hasarının %{1}'i | karşı kenara, aynı hizaya ikinci bir anten kurulur. İki anten arasındaki hücrelere {0} sn'de bir, hasat hasarının %{1}'i kadar vurur |
| 36 | `ui.effect.crescent.full` | her {0} doğrudan bitki isabetinde fareden hilal · rastgele ana yönde en fazla {1} birim · hasat hasarının %{2}'i | her {0} doğrudan isabette imleçten bir hilal fırlar. Rastgele bir yöne en fazla {1} birim gider, hasat hasarının %{2}'i kadar vurur |
| 37 | `ui.effect.crescent` | her {0} geçerli vuruşta bir hilal · {1} hücre ilerler · hasat hasarının %{2}'i | her {0} vuruşta bir hilal fırlar. {1} hücre gider, hasat hasarının %{2}'i kadar vurur |
| 38 | `ui.effect.crit_arc` | kritik vuruş, {0} hücre içindeki başka bir bitkiye ark sıçratır · kritik hasarın %{1}'i · vuruş başına en çok {2} | kritik vuruş, {0} hücre yakındaki başka bir bitkiye sıçrar. Kritik hasarın %{1}'i kadar vurur, vuruş başına en fazla {2} kez |
| 39 | `ui.effect.crit_burst` | tek vuruşta {0}+ bitkiye kritik: merkezde ek alan vuruşu · hasat hasarı × kritik çarpanının %{1}'i | tek vuruşta {0} ya da daha fazla bitkiye kritik yaparsan ortada bir alan vuruşu daha patlar. Gücü: hasat hasarı × kritik çarpanının %{1}'i |
| 40 | `ui.effect.full_moon` | her ana hilalle imleç halkasından bir büyük ek hilal · ana hilalden farklı rastgele yön · taban hasarın %{0}'i · bedel: {1} sn vuruş duraklaması | her ana hilalle birlikte imleç halkasından büyük bir hilal daha çıkar ve başka bir yöne gider. Taban hasarın %{0}'i kadar vurur. Karşılığında vuruşların {1} sn durur |
| 41 | `ui.effect.greedy` | tek vuruşta {0}+ hasat: sıradaki vuruşun hasarı ×{1} · tek hedefe vurursan hak kaybolur | tek vuruşta {0} ya da daha fazla bitki hasat edersen sıradaki vuruşun ×{1} hasar verir. Tek bitkiye vurursan bu hak kaybolur |
| 42 | `ui.effect.growing_moon` | hilal menzili +{0} birim · hasatla büyür: hasar +%{1} (en çok {2} kez), cephe {3} hücre · hasatsız adım sıfırlar | hilal {0} birim daha uzağa gider. Hasat ettikçe büyür: her seferinde hasarı %{1} artar (en fazla {2} kez), önü {3} hücre genişler. Hasat etmeden geçen bir adım büyümeyi sıfırlar |
| 43 | `ui.effect.echo` | tek vuruşta {0}+ hasat: kesilen hücrelerin tarlanın ortasına göre simetriğine meteor (en çok {1} hücre) · hasat hasarının %{2}'i · bekleme {3} sn | tek vuruşta {0} ya da daha fazla hasat yaparsan, kestiğin hücrelerin, tarlanın ortasına göre tam karşısına meteor düşer (en fazla {1} hücre). Hasat hasarının %{2}'i kadar vurur, {3} sn bekleme |
| 44 | `ui.effect.overflow` | öldüren vuruşun artan hasarının %{0}'i yandaki bitkiye geçer · en çok {1} aktarım · her aktarımda %{2} kayıp | öldüren vuruştan artan hasarın %{0}'i yandaki bitkiye geçer. En fazla {1} kez aktarılır, her seferinde %{2} azalır |
| 45 | `ui.effect.overload` | tetiklenen saksı davranışı {0} sn sonra bir kez daha çalışır · hasarın %{1}'i · saksı + davranış başına {2} sn bekleme | tetiklenen bir saksı davranışı {0} sn sonra, hasarın %{1}'i kadar bir kez daha çalışır. Aynı saksı ve davranış için {2} sn bekleme var |
| 46 | `ui.effect.reactor` | {0} ve üstü bitkiyi kendi vuruşunla hasat edince {1} sn mevcut davranış şansları ×{2} · birikmez, süre yenilenir | {0} ya da daha nadir bir bitkiyi kendi vuruşunla hasat edince davranış şansların {1} sn boyunca ×{2} olur. Üst üste binmez, süre baştan başlar |
| 47 | `ui.reward.echo.explosion` | patlamanın dışından {0} hücre yayılan artçı · adım başına {1} sn · ilk patlamanın {2} hasarı | patlamanın dışına {0} hücre yayılır. Her adım {1} sn sürer, ilk patlamanın {2} hasarını verir |
| 48 | `ui.reward.rhythm` | her {0} doğrudan hasatta bir: hasar ×{1} · yarıçap ×{2} | her {0} doğrudan hasatta bir: hasar ×{1}, yarıçap ×{2} |
| 49 | `ui.reward.chain` | davranış hasatları başka saksıların davranışlarını tetikler · en çok {0} ek nesil · {1} | davranış hasatları başka saksıların davranışlarını da tetikler. En fazla {0} halka, her halkada {1} (kartta taşınca kısaltıldı) |
| 50 | `ui.reward.chain.split` | şans {0} · hasar {1} | şans {0}, hasar {1} |

## E. Ödül kartı notları (kartın altındaki küçük yazı)

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 51 | `data.reward.canavar_kesimi.note_d88dc3` | Yalnız kendi vuruşun. Bir kez alınır. | Sadece kendi vuruşunu etkiler. Bir kez alınabilir. |
| 52 | `data.reward.canavar_kesimi.note_f6a008` | Yalnız kendi vuruşun. 25. turdaki Sınamadan sonra çıkar; bir kez alınır. | Sadece kendi vuruşunu etkiler. 25. turdaki Sınamadan sonra çıkar, bir kez alınabilir. |
| 53 | `data.reward.firtina_bilegi.note_aa5733` | Bir kez alınır. Taban 0,10 sn. | Bir kez alınabilir. Vuruş arası 0,10 saniyenin altına inmez. |
| 54 | `data.reward.firtina_bilegi.note_4f0a36` | 25. turdaki Sınamadan sonra çıkar; bir kez alınır. Taban 0,10 sn. | 25. turdaki Sınamadan sonra çıkar, bir kez alınabilir. Vuruş arası 0,10 saniyenin altına inmez. |
| 55 | `data.reward.hizli_bilek.note_49cc2e` | Vuruşlar arası süre kısalır. Taban 0,10 sn. | Vuruşların arası kısalır, ama 0,10 saniyenin altına inmez. |
| 56 | `data.reward.bereketli_toprak.note_2b5493` | Bütün saksılar. Yalnız tarlan boşalıyorsa çıkar (son tur ortalama doluluk %65'in altında). Taban 0,50 sn. | Bütün saksılarda geçerli. Sadece tarlan boş kalıyorsa çıkar. Üretim süresi 0,50 saniyenin altına inmez. |
| 57 | `data.reward.hasat_ritmi.note_2a97d6` | Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Hak hazır olunca sıradaki vuruşun güçlenir (imleç halkası büyür). Ek saldırı üretmez; davranışlara geçmez. | Sadece kendi vuruşunla yaptığın hasatlar sayılır. Sayaç dolunca sıradaki vuruşun güçlenir ve imleç halkası büyür. Ek vuruş çıkarmaz, davranışları etkilemez. |
| 58 | `data.reward.davranisa_adanis.note_1db657` | Patlama · elektrik · kasırga · bumerang. Kendi vuruşun değişmez. | Patlama, elektrik, kasırga ve bumerangı güçlendirir. Kendi vuruşun değişmez. |
| 59 | `data.reward.yikim_gucu.note_bf999d` | Patlama · kasırga · bumerang · elektrik. Kendi vuruşun değişmez. | Patlama, kasırga, bumerang ve elektriği güçlendirir. Kendi vuruşun değişmez. |
| 60 | `data.reward.kivilcim.note_662644` | Patlama · kasırga · bumerang · elektrik. Şansı olmayan saksıya şans vermez; en çok %100. | Patlama, kasırga, bumerang ve elektrik şansını artırır. Şansı hiç olmayan saksıya şans vermez. En fazla %100. |
| 61 | `data.reward.nadir_reaktor.note_662644` | (60 ile aynı) | (60 ile aynı) |
| 62 | `data.reward.agir_darbe.note_19db3c` | Yalnız kendi vuruşun kritiği. | Sadece kendi vuruşunun kritiklerini güçlendirir. |
| 63 | `data.reward.altin_hedef.note_12c404` | Yalnız kendi vuruşun; Sıradan ve Seyrek değişmez. Davranış hasarı değişmez. | Sadece kendi vuruşunu etkiler. Sıradan ve Seyrek bitkiler ile davranış hasarı değişmez. |
| 64 | `data.reward.genis_savurus.note_b211c6` | Yalnız kendi vuruşun. İmleç halkası büyür; davranış alanı değişmez. | Sadece kendi vuruşunu etkiler: imleç halkası büyür. Davranışların alanı değişmez. |
| 65 | `data.reward.keskin_bicak.note_4f0771` | Yalnız kendi vuruşun. Davranış hasarı değişmez. | Sadece kendi vuruşunu güçlendirir. Davranış hasarı değişmez. |
| 66 | `data.reward.kritik_goz.note_648baa` | Yalnız kendi vuruşun. Yüzde puanı olarak eklenir. | Sadece kendi vuruşunu etkiler. Şansa yüzde puan olarak eklenir. |
| 67 | `data.reward.nadir_tohum.note_41c039` | Bütün saksılar. Yüzde puanı olarak eklenir; en çok 95 puan. | Bütün saksılarda geçerli. Yüzde puan olarak eklenir, en fazla 95 puan. |
| 68 | `data.reward.buyuyen_ay.note_87dce2` | Hilal menzili 1 birim artar: 3 → 4. Ana hilal doğduğu andan yok olana kadar ilerlediği mesafeyle orantılı büyür; ilk hasadı beklemez. Hasar büyümesi gerçek hasatlara bağlıdır. Dolunay baştan büyük ve sabittir. | Hilal 3 yerine 4 birim gider ve ilerledikçe büyür. Hasarı ise sadece gerçekten hasat ettikçe artar. Dolunay baştan büyüktür, büyümez. |
| 69 | `data.reward.dolunay.note_d9b83b` | Her ana hilalin yanında yalnız bir büyük ek hilal çıkar; hasat koşulu yoktur. İmleç halkasından doğduğu anda, ana hilalden farklı rastgele yönde ilerler. Baştan büyüktür ve yeniden Dolunay üretmez. | Her ana hilalle birlikte büyük bir hilal daha çıkar, hasat şartı yok. İmleç halkasından başka bir yöne gider. Dolunaydan yeni Dolunay çıkmaz. |
| 70 | `data.reward.elektrik_anteni.note_f7c5c6` | Anten tarlanın kenarında durur; saksı ve parsel yeri kaplamaz. Vuruşu senin vuruşun sayılmaz: kritik atmaz, davranış tetiklemez. | Anten tarlanın kenarında durur, saksılara yer kaplamaz. Vuruşu senin vuruşun sayılmaz: kritik yapmaz, davranış tetiklemez. |
| 71 | `data.reward.hasat_yankisi.note_29cfee` | Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Yankı yeni yankı doğurmaz. | Sadece kendi vuruşunla yaptığın hasatlar sayılır. Yankıdan yeni yankı çıkmaz. |
| 72 | `data.reward.hilal_darbesi.note_763e86` | Hilal imleç halkasının kenarından rastgele bir ana yönde çıkar. Hız 8 birim/sn; temel menzil 3, Büyüyen Ay ile 4 birimdir. | Hilal, imleç halkasının kenarından rastgele bir yöne fırlar. Normalde 3 birim gider, Büyüyen Ay ile 4. |
| 73 | `data.reward.iletken_hat.note_dd726f` | İkinci antenin yerini sen seçersin; hat kurulamayan konum seçilemez. Hat vuruşu senin vuruşun sayılmaz. | İkinci antenin yerini sen seçersin. Hat kurulamayan yerler seçilemez. Hat vuruşu senin vuruşun sayılmaz. |
| 74 | `data.reward.tasan_hasar.note_4dda32` | Yalnız kendi vuruşunun öldürdüğü bitkiden başlar. Aktarım kritik atmaz. | Sadece kendi vuruşunla öldürdüğün bitkiden başlar. Aktarılan hasar kritik yapmaz. |
| 75 | `data.reward.artci_patlama.note_cb4550` | Patlamanın dış sınırından üç adım yayılır. Aynı hücreye bir kez vurur; kilitli hücrelerden geçmez. Artçı yeni artçı veya zincir başlatmaz. | Patlamanın dış kenarından üç adım yayılır. Her hücreye bir kez vurur, kilitli hücrelerden geçmez. Artçıdan yeni artçı ya da zincir çıkmaz. |
| 76 | `data.reward.cifte_akim.note_78d4d2` | Şansı tutan her normal elektrikten sonra aynı saksıdan ikinci bir dalga. Yeni şans atılmaz; ikinci dalga zincir başlatmaz. | Şansı tutan her elektrikten sonra aynı saksıdan ikinci bir dalga çıkar. Bunun için yeni şans atılmaz, ikinci dalga zincir başlatmaz. |
| 77 | `data.reward.asiri_yukleme.note_b12ea6` | Ek vuruş zincire katılmaz ve yeni şans atmaz. Kasırga / bumerang aktif sınırı aşılmaz. | Tekrar eden davranış zincir başlatmaz, yeni şans da atmaz. Aynı anda olabilecek kasırga ve bumerang sayısı aşılmaz. |
| 78 | `data.reward.acgozlu_hasat.note_bb7f10` | Boş savuruş hakkı harcamaz. Güçlenen vuruş yeni hak kazandırmaz. Davranışlara geçmez. | Boşa savurmak hakkı harcamaz. Güçlenmiş vuruş yeni hak kazandırmaz. Davranışları etkilemez. |
| 79 | `data.reward.kapali_devre.note_ca8b75` | Ek vuruş kendini yeniden başlatamaz. Davranışı olan saksı yoksa çalışmaz. | Ek hat vuruşu yeni ek vuruş başlatmaz. Davranışı olan saksı yoksa işe yaramaz. |
| 80 | `data.reward.kritik_ark.note_093917` | Ark, vuruşunun değmediği bir bitkiye gider. Kritik atmaz, yeni ark doğurmaz. | Ark, vuruşunun değmediği bir bitkiye sıçrar. Kritik yapmaz, yeni ark çıkarmaz. |
| 81 | `data.reward.zincir_hasat.note_97c0b1` | Her saksı ve davranışı bir saldırının zincirinde bir kez denenir. Artçı ve ikinci dalga zincire katılmaz. | Bir zincirde her saksının davranışı en fazla bir kez tetiklenir. Artçı ve ikinci dalga zincire katılmaz. |

## F. Tarla olayları (kapris)

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 82 | `ui.field.shell.prep` | İki bölgeden biri kapanacak · bir düğümü kır: onun bölgesi kurtulur, diğeri kapanır · seçmezsen A kapanır | İki bölgeden biri kapanacak. Hangi düğümü kırarsan onun bölgesi kurtulur. Hiçbirini kırmazsan A kapanır. |
| 83 | `ui.field.sprout.prep` | Filiz çıkıyor · yayıldığı hücrelerde üretim yavaşlar · ana filizi kendi vuruşunla kır | Bir filiz çıkıyor. Yayıldığı hücrelerde üretim yavaşlar. Ana filizi kendi vuruşunla kır. |
| 84 | `ui.field.sprout.broken` | Ana filiz kırıldı · üretim normale döndü | Ana filizi kırdın, üretim normale döndü. |
| 85 | `ui.field.sprout.withered` | Filiz kurudu · üretim normale döndü | Filiz kurudu, üretim normale döndü. |
| 86 | `ui.field.thorn.prep` | İşaretli {0} hücrede üretim duracak · kökleri kendi vuruşunla kır | İşaretli {0} hücrede üretim duracak. Kökleri kendi vuruşunla kır. |
| 87 | `ui.field.thorn.broken` | Kökler kırıldı · bölge açıldı | Kökleri kırdın, bölge açıldı. |
| 88 | `ui.field.thorn.withdrew` | Dikenler çekildi · bölge açıldı | Dikenler çekildi, bölge açıldı. |
| 89 | `ui.field.bounty.prep` | Bereket geliyor: kaynak ve TP ×2 · üretim ×2 hızlı | Bereket geliyor: kaynak ve TP iki katı, üretim iki kat hızlı. |
| 90 | `ui.field.bounty.active2` | Boş yuvalar ×2 hızlı dolar · tur boyunca | Tur boyunca boş yuvalar iki kat hızlı dolar |
| 91 | `ui.field.bounty.ended` | Bereket bitti · {0} bonuslu hasat | Bereket bitti: {0} bereketli hasat |
| 92 | `ui.event.fog.applied` | vuruş alanı bir basamak dar: en iyi nişanda {0} → {1} bitki | vuruş alanın küçüldü: en iyi atışta {0} yerine {1} bitki |
| 93 | `ui.event.fog.preview` | vuruş alanı bir basamak daralır: en iyi nişanda {0} → {1} bitki | vuruş alanın küçülecek: en iyi atışta {0} yerine {1} bitki |
| 94 | `ui.event.fog.step` | vuruş alanı bir basamak daralır | vuruş alanın biraz küçülür |

## G. Tarla, parsel, yetenek ağacı, dükkân

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 95 | `ui.preview.no_resonance` | Henüz aktif rezonans yok · aynı tipteki parselleri bir saksının altında topla | Henüz rezonans yok. Aynı türden parselleri bir saksının altında toplarsan oluşur. |
| 96 | `ui.newtiles.more` | +{0} parsel daha · haritada parlıyor | +{0} parsel daha, haritada parlıyorlar |
| 97 | `ui.tile.spawn_floor` | Üretim tabanda ({0} sn): fazlası nadirliğe · saksıya {1} nadirlik | Üretim en hızlıda ({0} sn). Fazla hız nadirliğe dönüştü: saksıya {1} nadirlik. |
| 98 | `ui.card.spawn_floor` | Üretim tabanda → Nadirlik | En hızlı üretim → Nadirlik |
| 99 | `ui.skill.spawn_floor` | {0} (taban · fazlası nadirliğe) | {0} (en hızlısı, fazlası nadirliğe dönüşür) |
| 100 | `ui.skill.completed_max` | TAMAMLANDI  ·  EN ÜST SEVİYE | EN ÜST SEVİYEDE |
| 101 | `ui.skill.granted.desc` | Bu düğüm satın alınmadı; bu profilde ücret ödenmez. | Bu yetenek baştan açık, satın alman gerekmez. |
| 102 | `ui.skill.disabled.desc` | Bu deneyde tur süresi sabit ({0} sn). | Bu modda tur süresi sabit: {0} sn. |
| 103 | `ui.skill.disabled.desc2` | Bu düğüm etkisiz ve satın alınamaz. | Bu yetenek burada işe yaramaz, satın alınamaz. |
| 104 | `ui.skill.unlock.cards_start` | {0} kartları başlangıçtan açık: deneme başından beri seçim havuzunda. | {0} kartları başlangıçtan açık, ilk turdan itibaren çıkabilir. |
| 105 | `ui.shop.tagline` | SEÇ. EK. BÜYÜT. | Saksını seç, tarlana yerleştir. |
| 106 | `ui.shop.incomplete` | Saksı ayarı eksik. | Bu saksı şu an satın alınamıyor. |
| 107 | `ui.effect.antenna.place.detail` | Tarlanın kenarında bir konum seç · anten içeri bakar | Tarlanın kenarında bir yer seç. Anten tarlaya doğru bakar. |

## H. Ayarlar

| # | Anahtar | Şu an | Öneri |
|---|---|---|---|
| 108 | `ui.options.language.desc` | Oyunun dili. İlk açılışta bilgisayarın dili seçilir; o dil yoksa İngilizce. | Oyunun dili. İlk açılışta bilgisayarının dili seçilir, o dil yoksa İngilizce. |
| 109 | `ui.options.background_effects.desc` | Hareketli uzay arka planı. Güce göre: sen güçlendikçe gökyüzü müzikle birlikte renk değiştirir. Sabit: hep aynı gökyüzü. Kapalı: düz renk; zayıf ekran kartlarında FPS artırır. | Hareketli uzay arka planı. Güce göre: sen güçlendikçe gökyüzü müzikle birlikte renk değiştirir. Sabit: gökyüzü hep aynı kalır. Kapalı: düz renk, zayıf ekran kartlarında FPS'i artırır. |
| 110 | `ui.options.sound.desc` | Bütün sesleri açar ya da kapatır; ses düzeylerin korunur. Hazırlık ekranındaki hoparlör düğmesiyle aynı ayar. | Bütün sesleri açar ya da kapatır. Ses düzeylerin olduğu gibi kalır. Hazırlık ekranındaki hoparlör düğmesiyle aynı ayar. |
| 111 | `ui.options.grid_coordinates.desc` | Tarla kenarındaki A-K / 1-11 etiketleri; haritadaki koordinatlarla aynı. | Tarla kenarındaki A-K ve 1-11 etiketleri. Haritadaki koordinatlarla aynıdır. |
| 112 | `ui.options.ctrl.harvest.desc` | İmleci bitkilerin üzerinde gezdir; tırpan otomatik keser. | İmleci bitkilerin üzerinde gezdir, tırpan kendiliğinden keser. |
| 113 | `ui.options.ctrl.tab.desc` | Turlar arasında tur önizlemesini açar; tur sırasında kota kartındaki Sınama ödülleri listesini gösterir / gizler. | Turlar arasında tur önizlemesini açar. Tur sırasında kota kartındaki Sınama ödülleri listesini açıp kapatır. |

---

## I. Adlar: yetenek, ödül, Sınama, kapris, rezonans, uzmanlaşma (26 Kasım)

Test edenlerin takıldığı asıl yer burası. AI kokan ad kalıpları:

- **Destansı soyut kelimeler:** Gücü, Ustalığı, Zirvesi, Tekniği, Sezgisi, Adanış, Bilgisi, Bilgelik, Yıkım, Güçlendirilmiş.
- **Çiftlik oyununda bilim kurgu / hava durumu süsü:** Plazma, Reaktör, Yıldırım, Fırtına, Cephe, Kuşatma.
- **Aynı işe giderek büyüyen uydurma adlar:** Hasarı artıran 5 ayrı yetenek, adları kademe kademe büyüyerek
  Keskin Başlangıç → Güçlü Kesim → Ağır Kesim → Yıldız Kesim → Plazma Kesim diye gidiyor. Oyuncu bunların aynı şey
  olduğunu addan anlayamıyor.

**Ad kuralları (öneriler buna göre):**

1. Ad, ne yaptığını söylesin ("Hızlı Bilek" iyi, "Nadir Reaktör" kötü).
2. Gündelik ve somut kelimeler kullan: tarla, çiftçi ve alet dili (gübre, sulama, sera, bileme, sap, ağız, ot, ayaz).
3. Aynı işi yapanlar aynı adı taşısın, sırası I, II, III ile gösterilsin.
4. Ad kısa olsun: 1–2 kelime.
5. Zaten sade olan adlara dokunma (Hızlı Bilek, Keskin Bıçak, Nadir Tohum, Bereketli Toprak …).

Bu adların hiçbiri Steam sayfasında geçmiyor; değişiklik sayfayı etkilemez.

### I-1. Yetenek ağacı

| # | Şu an | Ne yapıyor | Öneri |
|---|---|---|---|
| 114 | Keskin Başlangıç | hasar + (küçük, erken) | Bileme I |
| 115 | Güçlü Kesim | hasar + | Bileme II |
| 116 | Ağır Kesim | hasar + (büyük) | Bileme III |
| 117 | Yıldız Kesim | hasar + (çok büyük) | Bileme IV |
| 118 | Plazma Kesim | hasar + (en büyük) | Bileme V |
| 119 | Aşırı Güç I / II | hasar + (geç) | Ek Bileme I / II |
| 120 | Kesim Tekniği | hasar % | Alışkın El I |
| 121 | Kesim Ustalığı | hasar % | Alışkın El II |
| 122 | İleri Ustalık | hasar % | Alışkın El III |
| 123 | Hasat Zirvesi | hasar % (en büyük) | Alışkın El IV |
| 124 | Hızlı Eller | saldırı hızı | *kalsın* |
| 125 | Akıcı Kesim | saldırı hızı | *kalsın* |
| 126 | Yıldırım Kesim | saldırı hızı (büyük) | Seri Kesim |
| 127 | Geniş Süpürüş | vuruş alanı | Uzun Sap |
| 128 | Geniş Tarama | vuruş alanı | Geniş Ağız |
| 129 | Düzenli Üretim | bitki daha sık çıkar | Sulama |
| 130 | Verimli Üretim | bitki daha sık çıkar | Gübre |
| 131 | Seri Üretim | bitki daha sık çıkar (büyük) | Sera |
| 132 | Son Vardiya | tur süresi + | Fazla Mesai |
| 133 | Biraz Daha Zaman, Uzun Hasat | tur süresi + | *kalsın* |
| 134 | Hasat Deneyimi I / II | TP kazancı | Tecrübe I / II |
| 135 | Kritik Odak | kritik şansı | Dikkatli Göz |
| 136 | Kritik Güç | kritik çarpanı | Sağlam Vuruş |
| 137 | Kart Sezgisi | kart nadirliği | Kart Şansı |
| 138 | İlk Vazgeçiş / İkinci Vazgeçiş | kart atlama hakkı +1 | Atlama Hakkı I / II |
| 139 | Altın / Demir / Taş Hasat, Zengin Cevher, Hasat Rekoru, Nadir Filizler, Tarla Genişlemesi, … Kartları, … Saksı | | *kalsın* |

Alternatif (114–118 için): "Bileme I–V" yerine bir malzeme merdiveni: Bileme Taşı → Çelik Ağız → Ağır Tırpan → Göktaşı Çeliği → Gemi Çeliği.
Daha renkli ama yine uydurma; "Bileme I–V" ne yaptığını en açık söyleyen.

### I-2. Sınama ödülleri

| # | Şu an | Ne yapıyor | Öneri |
|---|---|---|---|
| 140 | Bereketli Öğrenim | seviye başına +1 seçim, vuruş hasarı ×0,80 | Bol Seçenek |
| 141 | Davranışa Adanış | davranış hasarı ×1,50, seviye başına −1 seçim | Saksılara Güven |
| 142 | Altın Hedef | Nadir ve üstü bitkiye vuruş hasarı | Nadir Avcısı |
| 143 | Bilgi Filizi | TP kazancı | Tecrübe Tohumu |
| 144 | Canavar Kesimi | vuruş hasarı (geç) | Dev Tırpan |
| 145 | Fırtına Bileği | saldırı hızı (geç) | Çevik Bilek |
| 146 | Kıvılcım | davranış şansları × | Kolay Tetik |
| 147 | Yıkım Gücü | davranış hasarı | Güçlü Saksılar |
| 148 | Aşırı Yükleme (etiket "Yükleme:") | tetiklenen davranış bir kez daha çalışır | Çifte Tetik (etiket "Tekrar:") |
| 149 | Hasat Yankısı (etiket "Yankı:") | kesilen hücrelerin karşısına göktaşı | Ayna Göktaşı (etiket "Göktaşı:") |
| 150 | Kritik Ark (etiket "Ark:") | kritik başka bitkiye sıçrar | Sıçrayan Kritik (etiket "Sıçrama:") |
| 151 | Nadir Reaktör (etiket "Reaktör:") | nadir hasat edince davranış şansları birkaç saniye artar | Nadir Heyecanı (etiket "Heyecan:") |
| 152 | Hilal Darbesi | imleçten hilal fırlar | Hilal |
| 153 | Hasat Ritmi, Zincir Hasat, Ağır Darbe, Bereketli Toprak, Geniş Savuruş, Hızlı Bilek, Keskin Bıçak, Kritik Göz, Nadir Tohum, Açgözlü Hasat, Büyüyen Ay, Dolunay, Elektrik Anteni, İletken Hat, Kapalı Devre, Taşan Hasar, Artçı Patlama, Çifte Akım | | *kalsın* |

### I-3. Sınamalar ve kaprisler (tarla olayları)


| # | Şu an | Ne yapıyor | Öneri |
|---|---|---|---|
| 154 | DON CEPHESİ | mavi şeritte üretim yavaşlar | AYAZ |
| 155 | SERT KABUK, SİS | | *kalsın* |
| 156 | İSTİLACI FİLİZ | yayılan filiz üretimi yavaşlatır | *kalsın* (kullanıcı) |
| 157 | DİKEN KUŞATMASI | dikenler bir bölgede üretimi durdurur | *kalsın* (kullanıcı) |
| 158 | AŞILI KABUK | iki bölgeden birini kurtarırsın | İKİ YOL (kullanıcı) |
| 159 | GEZGİN BEREKET | bir tur boyunca kaynak ve TP iki katı | *kalsın* (kullanıcı) |

### I-4. Rezonanslar (aynı parselleri bir saksının altında toplayınca)

| # | Şu an | Hangi parsel / ne verir | Öneri |
|---|---|---|---|
| 160 | Güçlendirilmiş Hasat | Odak / hasar | Keskin Odak |
| 161 | Bilgelik | Su / TP | Su Başı |
| 162 | Elektrik Bilgisi | Su / TP + hasar (adı elektrik diyor ama sudan geliyor) | Su Kanalı |
| 163 | Odak, Şöhret, Değerli Hasat, Bolluk, Verimli Toprak, Kristal Bahçe, Nadir Fidanlık | | *kalsın* |

### I-5. Uzmanlaşma

| # | Şu an | Ne yapıyor | Öneri |
|---|---|---|---|
| 164 | DAVRANIŞ USTASI | davranış hasarı × | SAKSICI |
| 165 | USTA BİÇİCİ | kendi vuruşun × | TIRPANCI |
| 166 | MEVCUT DÜZENİ KORU | hiçbir şey değişmez | OLDUĞU GİBİ KALSIN |

Başlangıç seçenekleri (Bahçıvan, Tüccar, Seçici Yetiştirici, Standart, Dar Kesim) ve parsel adları sade; *kalsın*.

**Uygulanınca:** Adlar yalnız çeviri tablosunda değişir; asset dosya adları ve iç kimlikler aynı kalır, kayıtlı ilerleme bozulmaz.
Yetenek adı kutuya sığıyor mu, yetenek ağacının tooltip'inde kontrol edilecek.

---

## J. Eri'nin konuşmaları, öğretici ve hikâye (26 Kasım)

65 satırın hikâyesi iyi; AI kokusu söyleyişte:

- **Neredeyse her konuşmada üç nokta ile dramatik duraklama var** (65 satırda 20 kez): "Açlık… beklemez.", "…Uzun sürmez ama."
- **Özlü söz gibi kurulmuş satırlar:** "Toprak hep burada.", "Güçlü eller çok kıymetli.", "En tatlısı buydu."
- **"değil mi?" ile biten sorular 4 kez tekrarlanıyor.**
- **Konuşmada noktalı virgül var**, insanlar böyle konuşmaz.
- **Kesme işaretli yazım:** "Adak'ım", "Sınama'yı". Steam notundaki karara göre doğrusu "Adağım", "Sınamayı".

**Eri'nin dili (kullanıcı, 26 Kasım):** oyuncuya açık ve net hitap eder, tam cümle kurar: "Tarla bomboş! Önce dükkândan bir saksı almalısın, sonra turu başlatabilirsin." Sesli ifadeler ("Mmm… doydum.", "Iıımm… Dünya, Dünya…") Eri'nin karakteridir, korunur.

**Eri'nin sesi:** Doymak bilmeyen, biraz şımarık, çocuksu ve iştahlı bir bitki. Özlü söz söyleyen bilge değil.
Başta tatlı ve neşeli, 25. turdan sonra solgun ve üzgün, zaferde soğuk.

**Korunan ipuçları (dokunulmadı):** ağzından kaçan "düşmemişti, yani gelmemişti", Dünya'yı bilip bilmiyormuş gibi yapması,
"eller" motifi, çekirdek ipucu, oyuncunun güven / şüphe seçiminin sonda hatırlanması.
Konuşmaların sayısı, sırası ve oyuncunun cevap düğmeleri aynı kalıyor.

| # | Anahtar | Önce | Şimdi (uygulandı) |
|---|---|---|---|
| 167 | `intro.select.0` | Oh! Bir gemi! Uzun zamandır kimse düşmemişti… yani, gelmemişti. | Aa, bir gemi! Uzun zamandır buraya kimse düşmemişti. Pardon, gelmemişti demek istedim. |
| 168 | `intro.select.1` | Ben Eri. Bu gezegen benim; toprak benim, eller sizin. | Ben Eri, bu gezegen de benim. Burada kalacaksan tarlamı sen ekip biçeceksin, anlaştık mı? |
| 169 | `intro.select.2` | Bir çiftçi ve bir tırpan seç. Gerisini birlikte hallederiz. | Önce bir çiftçi ve bir tırpan seçmelisin. Gerisini birlikte hallederiz. |
| 170 | `tut.setup.0` | Tarla bomboş… Önce dükkândan bir saksı al, sonra turu başlat. | Tarla bomboş! Önce dükkândan bir saksı almalısın, sonra turu başlatabilirsin. |
| 171 | `tut.place.0` | Boş bir yere tıkla, saksı oraya oturur. R ile döndürebilirsin. | Saksıyı koymak için boş bir yere tıklamalısın. R tuşuyla da döndürebilirsin. |
| 172 | `tut.hit.0` | Hasat bilmiyor musun? İmleci bitkilerin üstünde tut, tırpan kendi vurur. | Hasat etmeyi bilmiyor musun? İmleci bitkilerin üstünde tutman yeterli, tırpan kendisi vurur. |
| 173 | `tut.hit.1` | Dünya'da otomatik tırpan yok muydu? Var diye hatırlıyordum… | Hmm, Dünya'da otomatik tırpan yok mu? Ben var sanıyordum. |
| 174 | `tut.adak.0` | Sol üstteki pay benim Adak'ım. Dönem bitmeden dolsun, olur mu? | Sol üstteki pay benim Adağım. Dönem bitmeden onu doldurmalısın, tamam mı? |
| 175 | `tut.level.0` | Büyüyorsun! Birini seç; bu hasat boyunca seninle kalır. | Seviye atladın! Şimdi bir kart seçmelisin, deneme bitene kadar seninle kalacak. |
| 176 | `tut.lastround.0` | Dönemin son turu. Adak'ım hâlâ eksik… acele et. | Bu dönemin son turu ve Adağım hâlâ eksik. Acele etmelisin! |
| 177 | `tut.period.1` | Şimdilik. Bir sonraki payım biraz daha büyük olacak. | Şimdilik doydum. Ama bir dahaki sefere daha fazlasını isteyeceğim. |
| 178 | `tut.boss.0` | Bu tur bir Sınama! Soldaki hedefi bu tur içinde doldur. | Bu tur bir Sınama! Soldaki hedefi tur bitmeden doldurmalısın. |
| 179 | `tut.kapris.0` | Tohumlarımdan biri azdı! Ne yaptığı solda yazıyor. | Tohumlarımdan biri azdı! Ne yaptığını soldaki yazıdan okuyabilirsin. |
| 180 | `end.quota.0` | Adak eksik kaldı. Açlık… beklemez. | Adağımı dolduramadın. Ben aç bekleyemem. |
| 181 | `end.quota.1` | Yeniden başlarız. Toprak hep burada. | Neyse, baştan başlarız. Ben bir yere gitmiyorum. |
| 182 | `end.boss.0` | Sınama'yı geçemedin… Ben de bekleyemem. | Sınamayı geçemedin. Ben de daha fazla bekleyemem. |
| 183 | `end.boss.1` | Yine geleceksin, biliyorum. | Ama yine geleceksin, biliyorum. |
| 184 | `end.victory.0` | Doydum. Bu sefer gerçekten. | Mmm! Doydum. Bu sefer gerçekten doydum. |
| 185 | `end.victory.1` | …Uzun sürmez ama. | Ama bu tokluk çok sürmez. |
| 186 | `story.trial1.0` | Bir Sınama'yı geçtin! Köklerime kadar bir titreme indi. | Bir Sınamayı geçtin! Sevinçten yapraklarım titriyor. |
| 187 | `story.trial1.1` | Gemin nereden geliyordu? Dünya mı? …Hiç duymadım. | Senin gemin nereden gelmişti? Dünya mı? Hımm… hiç duymadım. |
| 188 | `story.trial1.reply.a` | Bana güvendiğin için sağ ol. | Bana güvendiğin için teşekkür ederim. |
| 189 | `story.trial1.2` | Çok güçlenme ama… Güçlü çiftçiler bir gün hep gider. | Ama çok da güçlenme, olur mu? Güçlü çiftçiler bir gün hep çekip gidiyor. |
| 190 | `story.reward1.0` | O ödülü sana tarla verdi. Tarla seni seviyor. | Bu ödülü sana tarla verdi. Bence tarla seni çok sevdi. |
| 191 | `story.reward1.1` | Ben de seviyorum. Güçlü eller çok kıymetli. | Ben de seni sevdim. Senin gibi çalışkan ellere ihtiyacım var. |
| 192 | `story.r5.1` | Sen çalıştıkça ben büyürüm. Adil bir anlaşma, değil mi? | Sen çalıştıkça ben büyüyorum. Bence ikimiz için de iyi bir anlaşma. |
| 193 | `story.r10.0` | Adak'ı neden mi istiyorum? Büyümek için. | Adağı neden mi istiyorum? Büyümek için tabii ki. |
| 194 | `story.r10.1` | Ne kadar çok verirsen o kadar büyürüm. Sen de büyümek istemez misin? | Sen bana ne kadar verirsen ben o kadar büyürüm. Sen de büyümek istiyorsun, biliyorum. |
| 195 | `story.r15.1` | Sonra… gittiler. Evet. Gittiler. | Sonra… gittiler. Evet, hepsi gitti. |
| 196 | `story.r20.0` | Gemin neden düştü, hiç düşündün mü? Yakıtın vardı, motorun sağlamdı. | Gemin neden düştü, hiç düşündün mü? Yakıtın vardı, motorun da sağlamdı. |
| 197 | `story.r20.1` | …Boş ver. Burada mutlusun, değil mi? | Neyse, boş ver bunu. Burada mutlusun işte. |
| 198 | `story.r25.0` | Bu kadar uzağa geleceğini düşünmemiştim… | Bu kadar ileri gideceğini hiç düşünmemiştim… |
| 199 | `story.r25.1` | Biraz soldum, değil mi? Sen güçlendikçe ben korkuyorum. | Biraz soldum, fark ettin mi? Sen güçlendikçe korkmaya başladım. |
| 200 | `story.r25.2` | Seninle biraz daha oynamak isterdim. Ama yakında soru sormaya başlarsın. | Seninle biraz daha oynamak isterdim. Ama yakında soru sormaya başlayacaksın. |
| 201 | `story.r25.reply.a` | …Boş ver. Hasat et. | Hiç. Sen hasadına bakmalısın. |
| 202 | `story.r30.0` | Eskiden mavi, kocaman bir tarlam vardı. Toprağı çekirdeğine kadar sıcaktı. | Eskiden kocaman, mavi bir tarlam vardı. Çekirdeği sıcacıktı. |
| 203 | `story.r30.1` | Onu bitirdim. Tadı hâlâ damağımda. | Hepsini yedim. Mmm… tadı hâlâ damağımda. |
| 204 | `story.r35.0` | Önceki çiftçiler gitmedi. Ben onları… hasat ettim. | Önceki çiftçiler gitmedi. Onları ben hasat ettim. |
| 205 | `story.r40.0` | Gemiler kendiliğinden düşmez. Senin gemini ben çektim. | Gemiler kendi kendine düşmez. Seninkini ben aşağı çektim. |
| 206 | `story.r50.0` | Son Sınama. Geçersen beni yenmiş olursun. | Bu son Sınama. Geçersen beni yenmiş olacaksın. |
| 207 | `story.r50.1` | Geçemezsen yine buradasın. İkisi de benim işime yarar. | Geçemezsen yine burada kalacaksın. İki türlü de ben kazanırım. |
| 208 | `story.victory.0` | Kazandın. Sona geldin… | Kazandın. Sonuna kadar geldin… |
| 209 | `story.victory.1` | O zaman oyun bitti. Saklanmama gerek kalmadı. | O zaman oyun bitti. Artık saklanmama gerek yok. |
| 210 | `story.victory.3a` | Bana güvenmiştin. En tatlısı buydu. | Bana güvendin. En çok da bunu sevdim. |
| 211 | `story.victory.3b` | Benden şüphelenmiştin. Haklıydın, ama yine de kaldın. | Benden şüphelendin, haklıydın da. Ama yine de kaldın. |
| 212 | `story.victory.4` | Üzgün değildim, çiftçi. Hiç olmadım. Sadece açtım. | Hiç üzülmedim, çiftçi. Sadece açtım. |
| 213 | `story.victory.5` | Hasat et. Bir sonraki gezegen için güçlenmem lazım. | Hadi, hasada devam et. Bir sonraki gezegen için güçlenmem lazım. |
| 214 | `story.after.0` | Artık birbirimizi tanıyoruz. Adak'ım bekliyor. | Artık birbirimizi tanıyoruz. Hadi, Adağım seni bekliyor. |
| 215 | `story.after.1` | Kaçamazsın. Gemin yok, Dünya'n yok. Sadece ben varım. | Kaçamazsın. Ne gemin var ne de Dünya'n. Burada bir tek ben varım. |

*Kalsın:* oyuncunun cevap düğmeleri, "Hmm…" / "Mmm…" baloncukları ve tablodakiler dışındaki satırlar.

Not: Diğer dillerdeki konuşma çevirileri eski Türkçe / İngilizceden yapıldı. İngilizce değişmediği için onlar da şimdilik değişmiyor.

---

## Bilerek dokunulmayanlar

- **Kısa değer ve durum satırları:** "KOTA TAMAM · {0} / {1}", "{0} · SON TUR", "Kaynak ×2 · TP ×2", "A düğümü {0}/{1} · B düğümü {2}/{3}",
  "{0} parsel · {1} rezonans", "Destansı+ {0} · Efsanevi {1}", "TEMEL → KARTLA · ÖRNEK".
  Bunlar okunacak cümle değil, tek bakışta görülecek değerler; " · " orada doğru araç.
- **Tuş ipuçları:** "{0} · tıkla: yerleştir · {1}: vazgeç", "Parselin üstüne gel: detay · …".
- **Başlıklar:** "SINAMA GEÇİLDİ · ÖDÜL SEÇ", "{0} AŞAMA · SINAMA ÖDÜLÜ".

## Uygulanınca

- `LocalizationVerification` çalıştırılacak, yer tutucular (`{0}`…) birebir korunuyor.
- Uzayan yazılar (özellikle 34–49 ödül kuralları ve 68 / 72) ödül kartında sığıyor mu, ekran görüntüsüyle kontrol edilecek.
- Eski testlerde bu yazıları birebir arayan ifadeler güncellenecek.
