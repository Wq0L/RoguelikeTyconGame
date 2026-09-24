# Basit rezonanslar — ilk sürüm
Durum: runtime kuralları, ödüller, popup ve ghost önizlemesi uygulanmıştır. Bu sayfa eski Resonance.md prototip değerlerinin yerini alır.

## Kapsam
Mevcut on tile ailesi korunur; yeni saldırı, zincir veya durum etkisi eklenmez. Kartın rarity'si aile sayısını değiştirmez. Yalnız aynı saksının kapladığı benzersiz hücreler sayılır. Rastgele tile dağıtımı korunur; komşuluk deseni aranmaz.

| Kimlik | Tarif | Etki |
|---|---|---|
| focus / Odak | 2 / 3 / 4 Damage | Doğrudan hasar ×1,5 / ×2 / ×3 |
| fame / Şöhret | 2 / 3 / 4 Energy | Harvest Score ×1,5 / ×2 / ×3 |
| bounty / Bolluk | 2 / 3 / 4 Duplicate | Üç kaynak ×1,25 / ×1,5 / ×2 |
| wisdom / Bilgelik | 2 / 3 / 4 Water | XP ×1,5 / ×2 / ×3 |
| crystal-garden / Kristal Bahçe | 2 / 3 / 4 Crystal | Nadirlik +10 / +20 / +35 puan |
| empowered-harvest / Güçlendirilmiş Hasat | 1 / 2 Damage + en az bir davranış tile'ı | Saksıdaki eşleşen davranışların hasarı ×1,5 / ×2 |
| precious-harvest / Değerli Hasat | 2 Energy + Crystal | Rare ve üstü bitkilerin Harvest Score'u ×3 |
| fertile-soil / Verimli Toprak | 2 Duplicate + Fertile | Üç kaynak ×2 |
| electric-wisdom / Elektrik Bilgisi | 2 Water + Electric | Kaynak saksının elektrik öldürmelerinde XP ×10 |
| rare-nursery / Nadir Fidanlık | 2 Crystal + Fertile | Nadirlik +25 puan; spawn bekleme süresi −%15 |

Davranış ailesi Explosive, Tornado, Boomerang, Electric'tir. Bu ortak tarif tek rezonanstır; yalnız mevcut aileler güçlenir. Damage + Electric, Tornado hasarını artırmaz.
Eski saf Fertile rezonansı kaldırıldı. Normal Fertile tile etkisi korunur. Minimum spawn süresi, rezonans sonrasında da 0,5 saniyedir. RareSpawnChance katkıları mevcut 95 puan clamp ve spawn ağırlık normalizasyonundan geçer; doğrudan Legendary olasılığı değildir.

## Birleşim
Aynı statta en güçlü uygun rezonans seçilir. Farklı statlar birlikte çalışır. Örneğin Şöhret ×2 + Değerli Hasat ×3: normal bitkide ×2, Rare+ bitkide ×3 skor; ×6 değil.
Aynı kuralın önceki kademeleri üst üste birikmez. Hücre değiştirilince ve temizlenince sonuç yeniden oluşturulur. Kaynak bonusu Gold/Iron/Stone'a uygulanır; XP ve skoru etkilemez.

Normal skill/tile hesapları korunur. Energy asset'lerinin yanlış HarvestDamage bağlantısı HarvestScoreMultiplier olarak düzeltildi. Skor artık hem oyuncunun skor bonusunu hem hedef saksının normal Energy ve uygun rezonans katkısını alır. Global All skor katkısı iki kez sayılmaz.

## Öldürme ve ödül
- Bitkinin normal ödülleri ve yerel bonusları hedef saksıdan gelir.
- Elektrik, kaynak saksının ×10 XP katkısını saldırı başında alır. Öldürücü vuruş elektrikse bu değer, hedefin normal XP rezonansıyla karşılaştırılır; en büyüğü kullanılır.
- Öldürmeyen elektrik vuruşu sonraki normal öldürmeye bonus bırakmaz.
- Duplicate artık yalnız kaynak ödülünü iki katlar; XP ve Harvest Score'u katlamaz. Kaynak rezonansı ×2 ve başarılı Duplicate birlikte ×4 kaynak verebilir.
- Önce normal kaynak/XP çarpanı ve uygun rezonans uygulanır, sonra Mathf.RoundToInt; Duplicate kaynak miktarını bundan sonra ikiye katlar.
- Hasat başına Harvest Score bir kez verilir. Sonradan alınan bonus geçmiş skoru değiştirmez.
- Davranış hasarı kendi normal hasar yuvarlamasından sonra kaynak saksının rezonansıyla güçlenir. Hedefin doğrudan hasar bonusu yeniden uygulanmaz.
- Havuz kapasitesi, tek eksenli orak yolu, elektrik çaprazları ve ikincil öldürmelerin yeni davranış başlatmaması korunur.

## Sunum
On tarife ait on farklı, kodla çizilen yuvarlak comic/toon ikon bulunur: kalın koyu çerçeve, düz renk ve küçük piktogramlar. İkon için ek texture/material/font oluşturulmaz.
Yalnız placement ghost'unun üzerinde, yatay bir sırada gösterilirler. Her aktif tarif için bir ikon vardır; Güçlendirilmiş Hasat birden fazla davranışı kapsasa da tek ikon kullanır.
Geçersiz/kapalı/dolu alan, UI üzerinde mouse, harita dışı mouse, iptal, yerleştirme ve moddan çıkış ikonları gizler. Yerleştirilen saksıya kalıcı ikon bırakılmaz.
Ghost ve gerçek saksı aynı ResonanceManager.Evaluate kodunu kullanır. Döndürülmüş ayak izi PlacementManager'ın gerçek offset hesabından gelir. On ikon nesnesi ghost ömrü boyunca yeniden kullanılır; mouse hareketinde nesne üretilmez.

Yeni bir tarif/kademe açıldığında oyun turuna dönüşteki mevcut popup, o saksının tüm aktif rezonanslarını adları ve kısa etkileriyle birlikte gösterir. Sadece tekrar hesaplama popup'ı tekrar oynatmaz. Kart ekranındaki değişiklikler mevcut kuyrukta birleştirilir. Popup'a kalıcı ikon sırası eklenmez.

## Analiz ve sınır
Economy Analyzer en güçlü stat seçimini runtime ile paylaşır. Harvest Score için Analytical Estimate ile simülasyon Average/P50/P10/P90 satırı eklendi. Duplicate'ın yalnız kaynağa etkisi ve round-by-round XP geçmişi aynı kuralları kullanır.
Mevcut simülasyon direct-hit kapsamındadır: komşu saksı geometrisi, orak/tornado/patlama/elektrik hasadı ve kaynak elektrik XP transferini henüz simüle etmez. Editor uyarısı korunur; tam build getirisi olarak okunmamalıdır.

Bu aşama dünya sıralaması için doğru Harvest Score üretir; çevrimiçi leaderboard servisi, giriş ekranı sıralama paneli veya sunucu doğrulaması kurulmuş değildir.
Skill fiyatları, temel hasar, bitki HP eğrisi ve XP seviye maliyetleri bu aşamada değiştirilmedi. R130 kötü build 7–8 vuruş hedefi, sonraki tam build dengelemesinin hedefidir; tamamlandı iddiası değildir.

## Tekrar üretim ve test
- Rules/Energy asset'lerini açıkça tekrar yazmak: node Tools/author-simple-resonance.cjs
- İzole Unity Play Mode kontrolleri: Tools/run-simple-resonance-tests.ps1
- Test raporu: Logs/SimpleResonanceVerification.txt
- Gerçek Unity UI render'ı: Logs/ResonanceBadges.png
Test kopyası Temp/ResonanceVerificationProject altındadır; açık kullanıcı sahnesini değiştirmez. Test fixture'ları bağımsız kurulur.

