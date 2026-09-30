# Denge geçişi — 29 Eylül 2026

Oyuncu geri bildirimiyle başlayan tek bir çalışma oturumunun özeti: ne yapıldı, dengeyi neye dayandırdık, oyuna ne uygulandı, sırada ne var. Kod değişiklikleri `5946eca` ("test ynei oyun temposu") commit'inde.

## 1. Neler yapıldı

### A) Round sonu arayüzü (oyuncu geri bildirimi)
- **Bu round'un kartları nereye gitti:** `ProgressionManager.RoundAppliedCells` listesi. Kartın düştüğü ya da yükselttiği tile'ları tutar, yeni round başlayınca temizlenir.
  - Round haritasında bu tile'lar sarı çerçeveyle parlar. Yeni tile "YENİ", yükselen tile "+SV" etiketi alır.
  - Sağdaki "BU ROUND'UN KARTLARI" kartı koordinatlarıyla listeler.
  - Dünyada zeminde sarı çerçeve de var (`FreshTileMarkers`).
- **ÖNİZLEME ekranı:** Round sonunda sağ üstteki buton ya da TAB ile açılır, TAB veya ESC ile kapanır.
  - Bitkiler ve efektler gizlenir, sadece saksılar kalır.
  - Saksıların üstünde rezonans rozetleri, üstte toplam rezonans şeridi görünür.
  - Tile'ın üstüne gelince bilgi çıkar.
- **Skill tree:** Arka plan koyulaştırıldı. Altın, demir ve taş sayaçları skill tree açıkken panelin üstünde görünür.

### B) Tempo teşhisi (neden son 30–40 round sıkıyor)
Oyunun kendi `EconomyCalculator`'ı ile ölçüldü.
- **Can duvarı:** Bitki canı round 65'te 180, 80'de 1.500, 110'da 45.000. Skill ağacı geride kalınca hasat durur ve gelir sıfıra iner, ama run bitmez.
  - Ağacın %40'ıyla gelir round ~75'te yarıya iner; arkadaşının son ~35 roundu bu.
- **XP tablosu:** Level 69'dan sonra kırılıyor: 6.333 → 9.708 → … → 62.666 (level 80).
- **Boşa giden kartlar:** Açık alanda boş hücre kalmayınca seçilen kart sessizce boşa gidiyordu.
- **Run uzunluğu:** İyi build ağacı ~65–75'te bitiriyor, zayıf build ~75–85'te duvara çarpıyor. İki durumda da run'ın sonu içeriksiz.
- Etkileşimli taslak sayfa: [Hasat Temposu Taslağı](https://claude.ai/artifact/FdxqTWPYQiYNy8GVUGX2uz). Teşhis, fikirlerin değerlendirmesi, faz eğrisi, nadirlik kademeleri ve kabul bantları orada.

### C) Run simülatörü
Oyunun verisi ve kodu üzerinde bir run'ı round round oynatır:
- 11×11 gerçek grid; kartlar `CardSelectionUI` ağırlıklarıyla çıkar, rastgele hücreye düşer.
- Saksı başına stat ve rezonans (`StatCalculator`, `ResonanceManager`).
- Oyuncunun global saldırı kapasitesi: saldırı hızı × imleç yarıçapındaki hücre sayısı.
- Patlama, tornado, bumerang ve elektrik ek hasadı.
- Üç para birimi; 140 node ön koşul sırasıyla, gerçek fiyatlarla alınır.
- XP tablosu ve level'lar.

Konum: `Library/VerificationProject/Assets/Editor/RunSimulator.cs` (yanında `MechanicsVerification.cs` ve `RoundPreviewVerification.cs`). **Dikkat:** `Library` git'e girmez ve Unity silebilir; simülatör kalıcı bir yere taşınmalı (bkz. 6).

### D) Oyuna uygulanan mekanikler
| Mekanik | Kural | Nerede |
|---|---|---|
| Yükseltme ekranı | Açık alanda boş hücre yoksa kart yeni tile vermez. Saksı altındakilerden öncelikli 3 rastgele aday gösterir. Seçilen tile seviye atlar: Common/Rare +1, Epic +2, Legendary +3; en fazla Sv 3. Her seviye tile'ın zar değerine +%25. Üstte "GRİD DOLU" şeridi çıkar, "Atla" durur. | `GroundCell` (Level, AddLevels), `ProgressionManager` (GetUpgradeCandidates, ApplyUpgrade), `CardSelectionUI`, `CardUI`, `TileCardOffer` |
| Temel güç | Açık alandaki bütün tile'lar Sv 3 ise kartlar kalıcı global güç verir. Stat: hasat hasarı, atak aralığı, üretim süresi, tüm kaynaklar ya da XP. Miktar nadirliğe göre %2/3/4/5 (MorePercent, üst üste çarpılır). | `CardSelectionUI.RollBaseStat` |
| Tarla Tükendi (**30 Eylül: kaldırıldı, yerine Hasat Kotası — bkz. 9**) | 20. rounddan sonra, round'un hasat geliri run'daki en iyi round'un %30'unun altındaysa sayaç artar. 3 round üst üste düşükse run "Tarla tükendi" ile biter. Round özetinde "Tarla zayıflıyor n/3" uyarısı çıkar. Gelir ağırlıkları: Taş 14, Demir 7, Altın 1; satış iadesi ve atlama ödülü sayılmaz. | `RoundManager` (Tarla Tükendi alanları), `RoundSummaryUI`, `RunComplateUI` |
| 60 sn süre sınırı | Round en fazla 60 sn. Süre skill'lerinin fazlası saldırı ve üretim hızına dönüşür (90 sn → 60 sn + 1,5× tempo). Tabanlar (0,1 / 0,5 sn) hızlanmadan önce uygulanır, bu yüzden round başına gelir 90 sn'lik round'la birebir aynı kalır. Skill tooltip'i "60 sn · +%X hız" yazar. | `RoundManager` (RoundSecondsCap, TempoMultiplier), `PlayerController`, `PlantSpawner`, `SkillNodeUI`, `EconomyCalculator` |

Uygulanmayanlar:
- **Alan yarıçapı düzeltmesi:** İlk hesabım yanlıştı; alan skill'leri ortalama vurulan hücreyi 3,1'den 5,3'e çıkarıyor, dal çalışıyor.
- **Can eğrisi değişikliği:** 130 round kalsın dedin, dokunulmadı.

## 2. Dengeyi neye dayandırdık

### Veri kaynakları (hepsi projenin kendi dosyaları)
- **Bitkiler:** 12 PlantSO. XP hepsinde 20; ödüller: altın Common/Uncommon, demir Rare, taş Epic/Legendary. Spawn ağırlıkları 40 / 12,5 / 6,7 / 4 / 1.
- **Bitki canı:** `PlantHealthScaling` çapaları ve nadirlik çarpanları 1 / 1,2 / 1,6 / 2 / 3.
- **XP tablosu:** "Xp çarpanı" (120 level).
- **Skill ağacı:** 140 node, 304 kademe. Toplam 156.899 altın + 102.875 demir + 16.320 taş; ön koşullar ve etkiler node'lardan.
- **Kartlar ve tile'lar:** `CardSelectionUI` tip ve nadirlik ağırlıkları, `TileModifierSO` değer aralıkları, `ResonanceRules`.
- **Savaş:** `PlayerController` + `GridSystem.GetGridObjectsInRadius` (yarıçap + yarım hücre, hücre 2 birim).

### Kalibrasyon: iki gerçek oyun
- **Senin run'ın:** ağaç 70–80'de tamam. Simülatörün "Deneyimli" profili ~67.
- **Arkadaşının run'ı:** 110'da ~%40 ağaç, ~3 saat. Simülatörün "Yeni" profili ~%50, yani bu profil arkadaşından biraz güçlü.
- **Menü süresi:** Round başına 30 sn + kart başına 15 sn + skill kademesi başına 8 sn; arkadaşının 110 round ≈ 3 saatine göre ayarlandı.
- Bu yüzden mutlak dakikalar tahmin; asıl güvenilir olan kurallar arasındaki fark.

### Simülatörün bulguları (şimdiki oyun)
- **Savaş darboğaz:** Üretilen bitkilerin sadece %10–30'u hasat ediliyor; sınırı oyuncunun vuruşları koyuyor.
- **Davranışlar:** İyi oyuncunun hasatlarının ~%25–45'i davranışlardan geliyor, yeni oyuncuda ~%5–18. İyi ve kötü run farkının büyük kısmı bu.
- **Bıçak sırtı:** Küçük beceri farkı "ağaç hiç bitmiyor" ile "67'de bitiyor" arasında gidip geliyor. Dengeleme bu yüzden "her denemede sonuç değişiyor" gibi hissettiriyor.
- **Tek eksen:** Ağaçta 46 aileden 11'i doğrudan hasar; en büyük çarpanlar Aşırı Güç I-II (×8, ×9).

### Kural testleri (seçilen mekaniklerle, simülatör tahmini)
| Run | Deneyimli | Orta | Yeni |
|---|---|---|---|
| Şimdiki oyun (130, mekaniksiz) | 5 sa 24 dk · 49 boş round | 5 sa 11 dk · 36 boş | 3 sa 26 dk · 25 kart boşa |
| 130 round | 4 sa 44 dk · 45 boş | 4 sa 34 dk · 34 boş | ~110'da Tarla Tükendi |
| 100 round | 3 sa 44 dk · 19 boş | 3 sa 33 dk · 4 boş | 2 sa 54 dk |
| 90 round | 3 sa 23 dk · 9 boş | 3 sa 13 dk · 0 boş | 2 sa 37 dk |

"Boş round": yeni skill, yeni tile ya da yükseltme olmayan round. Öneri 90 round (sadece `maxRounds`); karar henüz verilmedi.

## 3. Oyuna / sahneye uygulandı mı
- **Kod:** Evet. Değişiklikler script'lerde; sahnedeki `RoundManager`, `CardSelectionUI` gibi bileşenler aynı script'leri kullandığı için kendiliğinden geçerli.
- **Sahne dosyaları:** Dokunulmadı (`GameScene.unity`, `MenuScene.unity`).
- **Inspector'da yeni alanlar:** `Round Manager` altında "Tarla Tükendi": Exhaust Threshold 0,3, Exhaust Rounds 3, Exhaust From Round 20. Yeni alan oldukları için varsayılan değerlerle gelirler.
- **Max Rounds:** Hâlâ 130. 90 istersen `Round Manager → Max Rounds` alanından değiştir.

## 4. Ayarlanabilir değerler
| Değer | Şimdiki | Nerede |
|---|---|---|
| Tile seviyesi başına artış | +%25 | `GroundCell.LevelBonus` |
| En fazla tile seviyesi | 3 | `GroundCell.MaxLevel` |
| Nadirliğe göre seviye | 1 / 1 / 2 / 3 | `GroundCell.LevelsFor` |
| Temel güç miktarı | %2 / %3 / %4 / %5 | `CardSelectionUI.RollBaseStat` |
| Hasat Kotası: segment / başlangıç / büyüme (Tarla Tükendi yerine) | 5 round / 10 / ×1,45 | `Round Manager` inspector → Hasat Kotası |
| Round süresi sınırı | 60 sn | `RoundManager.RoundSecondsCap` |
| Max rounds | 130 | `Round Manager` inspector |

## 5. Test durumu
Hepsi izole kopyada, gerçek `GameScene` oynatılarak çalıştırıldı:
- **MechanicsVerification: 38/38.** Grid dolunca yükseltme kartları, seviye ve %25 değer artışı, "+SV" etiketi, Temel güç, 60 sn / 1,5× tempo, Tarla Tükendi uyarısı ve run sonu.
- **RoundPreviewVerification: 47/47.** Önizleme, yeni tile vurgusu, skill tree sayaçları.
- **EconomyAnalyzerVerification: 448/448.** 90 sn build'de max ağaç altın geliri 60 sn sınırında da aynı (9.093).

## 6. Açık konular ve sıradaki adımlar
1. **Max rounds kararı:** 130 mı 90 mı? Simülatör 90'ı öneriyor.
2. **Oyunda deneme:** Yükseltme ekranı, Tarla Tükendi zamanlaması, 60 sn tempo hissi.
3. **Yükseltme mi Atla mı:** Simülatörde deneyimli oyuncu grid dolunca çoğunlukla "Atla"yı seçiyor (round başına 3 hak). Yükseltmenin değeri ya da atlama ödülü ayarlanabilir.
4. **Temel güç'ün geç oyunda gücü:** Çarpanlar üst üste biniyor; gerekirse yüzdeler düşürülür.
5. **Performans:** Süre skill'leri alındığında geç oyunda saniye başına bitki ve vuruş 1,5 kat.
6. **Simülatörü taşımak:** `Library/VerificationProject` altından projeye kalıcı bir editör aracı olarak.
7. **Sonraki büyük adım (isteğe bağlı):** Taslak sayfadaki faz eğrisi (hızlı / yavaş / açılma / patlama), nadirlik kademeleri ve arketipler; simülatörle test edilip uygulanabilir.

## 7. Oyun testinden gelen eksikler (29 Eylül)

1. **Tile seviyesi 0'da yazmıyordu. Yapıldı.** Tooltip başlığı artık her tile'da seviyeyi yazıyor, 0'da da: "Common · Sv 0/3". Sağdaki "BU ROUND'UN KARTLARI" listesinde yeni tile'lar "RARE · Sv 0" gösteriyor.
2. **Seviye atlayınca eski → yeni değer yazmıyordu. Yapıldı.**
   - Kart: "Sv 0 → Sv 1" ve altında "+13% → +16.25% puan".
   - Round sonu tooltip'i: "BU TILE · önce → şimdi", "Sv 0 → Sv 1 · +1 seviye" ve her etki için eski → yeni değer.
   - Round listesi: "Sv 0 → 1 · +15.96% → +19.95%". Tek etkili tile'larda değer, çok etkililerde sadece seviye; tam liste tooltip'te.
   - Kod: `ProgressionManager.ApplyUpgrade` artık yükseltmeden önceki seviyeyi ve değerleri `TileUpgrade` kaydında tutuyor (`TryGetRoundUpgrade`). Aynı tile bir round'da iki kez yükselirse ilk hali kalır ("Sv 0 → 3"). Yeni round'da temizlenir.
   - Metin: `TileBuffText.AmountChange` / `ModifierChanges`. Ok işareti için `ComicPopupView.WithArrowGlyph`, çünkü çizgi roman fontunda "→" yok.
   - **Renk düzeni:**
     - Eski değer kırmızı, yeni değer yeşil. Kartta açık tonlar (`TileBuffText.OnCard`), tooltip ve listede kağıda uygun koyu tonlar (`OnPaper`).
     - Tooltip bölüm başlıkları koyu kehribar; "önce → şimdi" başlığı renk anahtarı.
     - Rezonans etkisi, rezonans adından bir ton açık.
     - "Bu round'un kartı…" satırı yeşil.
   - Test: MechanicsVerification 56/56, RoundPreviewVerification 47/47.
3. **XP eğrisi hâlâ bir anda yükseliyor, oyuncu yüksek level'lara çıkamıyor. Açıklandı, henüz değiştirilmedi.**
   - **Level ne işe yarıyor:** Her level round sonunda 1 kart demek (`RoundManager.HandleLevelUp`). XP eğrisi aslında "hangi round'da kaç kart alırsın" eğrisi.
   - **XP nereden geliyor:** Her hasat 20 XP, nadirliğe bakmaz. Çarpanlar: skill ağacında XP node'ları toplam +%300 (Harvest Experience +%100, Hasat Deneyimi I +%50, II +%150), Water-XPGain tile'ları, Temel güç "XP kazancı". Yani XP geliri = hasat sayısı × çarpan.
   - **Tablo nasıl yapılmış:** 23 Eylül "ekonomi dengelendi" commit'inde EconomyAnalyzer ile elle yazılmış 120 değer. Hedef "level ≈ round": r20'de 20–30, r40'ta 40–50, r65'te 65–75, r100'de 95–105, r115–130'da 121. Tek bir varsayılan build'in gelirine göre ayarlanmış; o yüzden eğri o build'in gelirini taklit ediyor (70'te sıçrama, 100'de ikinci sıçrama, sonda düşüş).
   - **Tablo sonrası:** Level 121'den sonra her level 188.713 XP; üst sınır yok.
   - **Simülatörde (şimdiki oyun, 130 round):**

     | Round | Tablo hedefi | Yeni | Orta | Deneyimli |
     |---|---|---|---|---|
     | 40 | 40–50 | 45 | 68 | 71 |
     | 60 | – | 68 | 77 | 80 |
     | 65 | 65–75 | 71 | 78 | 85 |
     | 80 | – | 75 | 107 | 127 |
     | 100 | 95–105 | 79 | 140 | 170 |
     | 130 | 121 | (çoğu run bitmiş) | 204 | 235 |

   - **Yeni oyuncu:** 65. round'a kadar hedefte. Sonra round başına ~5 bin XP kazanırken level başına 10–50 bin XP isteniyor: bir kart için 3 → 5 → 10 → 30+ round. Can duvarı hasadı düşürdükçe XP de düşüyor. 65–130 arası neredeyse hiç kart yok; sıkıcı son 30–40 round bu.
   - **Deneyimli oyuncu:** 40–60 arası duvarda (level 71 → 80, 20 round'da 9 kart). Ağaç bitince XP geliri 30 binden 350 bine fırlıyor, round başına 2–3 kart. Tablo 80. round'da bitiyor ve 130'da level 235.
   - **Asıl sorun:** 70. round'da iki oyuncunun round başı XP'si arasında 70 kat fark var (5 bin vs 345 bin). Tek sabit tablo ikisini birden dengeleyemez; biri için duvar, diğeri için sınırsız.
   - **Olası çözümler (karar verilmedi, simülatörle denenmeli):**
     a. Tabloyu düzleştir: sabit büyüme, sıçrama ve geriye düşüş yok. Tek başına yetmez; deneyimli daha da hızlanır.
     b. Bitki XP'si canla büyüsün: zor bitki daha çok XP. Duvara çarpan oyuncu daha az hasat eder ama hasat başı daha çok alır.
     c. Round sonu taban XP: her round sonunda mevcut level maliyetinin bir kısmı (ör. %25). En kötü durumda 4 round'da 1 kart; şans kalır, tamamen kurumak kalmaz.
     d. Round başına kart sınırı (ör. en fazla 3) ya da level başına XP'nin round'a göre ölçeklenmesi: deneyimlinin patlamasını oyunun sonuna kaydırır.
     e. Önerdiğim yol: faz hedefinden (hızlı → yavaş → açılma → patlama) level/round bandı çıkar, b + c + düzleştirilmiş tabloyu simülatörde üç profil için dene, bandın dışına çıkan profil kalmayana kadar ayarla.
   - **Senin istediğin (29 Eylül akşam):**
     - 121 tavanı kalksın; 500'e kadar çıkılabilsin ama yavaşça.
     - Toplam XP makul olsun ("26 milyonluk bitki kesilmez"); sıçrama olmasın.
     - İyi bir XP run'ı level'da uçabilsin: 3–4 saksıda 3. kademe Bilgelik gibi.
   - **Güncel öneri (henüz uygulanmadı):**
     - L60'a kadar şimdiki tablo; baştaki ve 45–48'deki geri düşüşler düzeltilir.
     - L60'tan sonra her level bir öncekinden sabit miktar pahalı (doğrusal artış). Üstel artış ve sıçrama yok.
     - Doğrusal artışta level, toplam XP'nin karekökü gibi büyür: 4 kat XP = 2 kat level, 6 kat XP = 2,5 kat level. Normal iyi run ~200'de biterse XP run'ı ~500'e çıkar.
     - XP run'ının kaldıracı: Bilgelik (bir saksıda 2/3/4 Water tile = ×1,5/×2/×3 XP), Water-XPGain tile'ları, XP node'ları (+%300), Temel güç XP kartları.
     - **Eğim ölçeği gerçek oyundan ayarlanmalı:** Simülatörün XP miktarları gerçek run'la kalibre değil (sadece ağacın bitiş round'u kalibre). Gereken veri: iyi bir run'da round 60–80 arası round başına XP.
     - **Veri için eklendi:** Round özetinde artık "XP +N" satırı var: o round'da kazanılan toplam XP (`ProgressionManager.TotalXPEarned` farkı; `GameFeelDirector` → `RoundSummaryUI`). Test: MechanicsVerification 64/64.
     - Level başına kart: round başına en fazla 3 kart ekranı, fazla level'lar kart nadirliğini yükseltir (yine öneri).
5. **Tile modifier denetimi (29 Eylül). Hepsi çalışıyor.**
   - Test: `ModifierAudit` (izole kopya, GameScene). 40 tile'ın her biri saksı altına konup bitki kesilerek denendi: 162/162.
   - Değerin saksıya ulaştığı ve etkinin göründüğü her tip için ölçüldü:

     | Tip | Ölçülen etki |
     |---|---|
     | Fertile | bitki çıkma süresi kısalıyor (5 → 2,6 sn, Legendary) |
     | Crystal | Rare+ bitki payı %35 → %48 (Legendary) |
     | Water | kesilen bitki XP'si 20 → 40 (Legendary) |
     | Energy | skor ×1,25–×2,25 |
     | Odak (Damage) | vuruş hasarı ×1,075–×1,25; patlama/tornado hasarına eklenmez (kural) |
     | Duplicate | ödül 2 kat |
     | Explosive | komşu saksıdaki bitki hasar alıyor |
     | Tornado, Bumerang, Elektrik | kesimde tetikleniyor |

   - Kart havuzu: 40 tile'ın hepsi kart ekranının listesinde. Davranış tile'ları (Patlama, Çoğaltma, Tornado, Bumerang, Elektrik) ilgili skill alınana kadar kartta çıkmıyor.
   - **Tasarım gereği sınırlar (hata değil):**
     - Tile sadece üstünde saksı varsa etki eder.
     - Davranışlar sadece oyuncunun doğrudan kestiği bitkide tetiklenir, zincirleme yok.
     - Patlama hasarı oyuncunun hasat hasarı kadar (başta 1).
   - **Veri tuhaflıkları:**
     - Explosive-Common 0,20–0,44 aralığı Rare'in (0,30–0,40) üstüne çıkabiliyor.
     - Tornado-Legendary %100'e kadar çıkıyor.
8. **Tarla Tükendi bildirimi. Yapıldı.**
   - Kural oyunda açık: 20. round'dan itibaren gelir, run'ın en iyi round'unun %30'unun altında 3 round üst üste kalırsa run biter.
   - Önceden oyuncu kuralı ancak ilk düşük round'dan sonra görüyordu. Şimdi:
     - **19. round özeti** (turuncu): "Tarla kontrolü Round 20 ile başlıyor · gelir en iyi round'a göre %30 altında 3 round üst üste kalırsa run biter".
     - **20. round intro'su:** "TARLA KONTROLÜ BAŞLADI".
     - **Düşük round sonrası:** özet (kırmızı) "Tarla zayıflıyor 1/3" gösterir, sonraki round intro'su (turuncu, kırmızı değil) "TARLA ZAYIF 1/3".
     - **Son hak:** "SON ŞANS · Tarla zayıflıyor 2/3 · bir round daha %30 altında kalırsa run biter"; intro "SON ŞANS · GELİRİ YÜKSELT".
   - Metinlerde sayılara ek yok ("%30'u / %25'i" hatası olmasın diye).
   - Kod: `RoundSummaryUI.ExhaustNotice`, `GameFeelDirector.ExhaustIntro`, `RoundManager.ExhaustFromRound/ExhaustThreshold/ExhaustionEnabled`.
   - Test: MechanicsVerification 72/72, RoundPreviewVerification 47/47.
   - **Tolerans round sayısı (simülatör):** Zayıf run 3 round'la ~109'da, 4'le ~110'da, 5'le ~111'de bitiyor. Kural kapalıyken 130'a kadar sürüyor, son ~20 round'da hasat %1'in altında. İyi oyuncular hiçbir ayarda etkilenmiyor. Önerim 5.
   - Değer sahnede kayıtlı (3); kod varsayılanı değil, `Round Manager → Tarla Tükendi → Exhaust Rounds` alanından değiştirilir.
7. **Üretim tabanı → nadirlik. Yapıldı.**
   - Sorun: skill ağacı tam olunca (Seri Üretim) üretim süresi 5 sn × 0,1 = 0,5 sn tabanına iniyor. Ondan sonra Fertile tile'ların hızı boşa gidiyordu. Bu, erken alınanlar ve en sık gelen kart tipi için de geçerliydi.
   - Kural (60 sn → tempo gibi): tabanın altına inemeyen hız, o saksının nadir bitki şansına eklenir.
     - Formül: `70 × ln(0,5 / tabansız süre)`. Her tile kendi payını getirir.
     - Tabanda Legendary Fertile ≈ +45, Crystal-Legendary +60 (Crystal güçlü kalır). Toplam nadirlik sınırı 95.
   - Nerede:
     - `StatCalculator.SpawnOverflowRarity` / `CalculateRaw`
     - `PlanterBrain.GetRawSpawnInterval` / `GetFinalStat(RareSpawnChance)`
     - Ekonomi hesaplayıcısı da aynı hesabı kullanıyor.
   - Yazılar:
     - Bütün saksılar tabandayken Fertile kartı "+37.2 nadirlik · Üretim tabanda → Nadirlik" yazar.
     - Yükseltme kartı "+45.1 → +63.1 nadirlik" yazar.
     - Tooltip "Üretim tabanda (0.5 sn): fazlası nadirliğe · saksıya +45.1 nadirlik" yazar.
     - Skill tooltip'i "0.5 sn (taban · fazlası nadirliğe)" yazar.
   - Test: ModifierAudit 174/174 (tabandaki saksıda Legendary bitki payı %2,9 → %5,1), EconomyAnalyzerVerification 448/448, MechanicsVerification 66/66, RoundPreviewVerification 47/47.
   - Simülatör: erken oyun ve ağacın bitişi aynı. Ağaç bittikten sonra gelir deneyimlide +%11–13, ortada +%9; yeni oyuncu ağaca pek ulaşmadığı için etkisiz.
   - Açık konu: yükseltilmiş tek bir Sv 3 Legendary Fertile ya da Crystal tek başına 95 nadirlik sınırına ulaşıyor; ondan sonrası yine boşa. Sınırı kaldırmak ya da yumuşatmak ayrı karar.
6. **Kart seçimi vignette'i. Yapıldı.** Sadece kart seçim ekranı açıkken köşelerden içeri kararma (`CardSelectionVignette`, kartların arkasında, 0,3 sn'de belirir, tıklamayı engellemez). Round sonu ekranında yok.
4. **Kart nadirliği (Kart Sezgisi). Yapıldı.**
   - Eski formülde ağacın tamamı (Kart Sezgisi 1–4, 9 kademe, şans +0,3) Epic+ oranını sadece %15'ten %21'e çıkarıyordu.
   - Yeni: 10 satırlık kademe tablosu (`CardSelectionUI.RarityTiers`). Her Kart Sezgisi kademesi bir satır ilerletir.

     | Kademe | Common | Rare | Epic | Legendary |
     |---|---|---|---|---|
     | 0 (skill yok) | 60 | 25 | 12 | 3 |
     | 3 | 30 | 29 | 29 | 12 |
     | 6 | 10 | 18 | 42 | 30 |
     | 9 (ağaç tam) | 3 | 7 | 42 | 48 |

   - Ağaç tamken 3 kartlık teklifte en iyi kartın Epic+ olma olasılığı ~%99,9.
   - Skill tooltip'i artık "Kart nadirliği: Epic+ %15 · Leg %3 → Epic+ %23 · Leg %5" yazıyor; eskiden anlamsız "0.033 → 0.067" yazıyordu.
   - Skill değerlerine dokunulmadı; sadece şansın karta çevrilişi değişti. Tabloyu değiştirmek için `CardSelectionUI.RarityTiers`.
   - Simülatör: ağacın bitişi değişmedi (deneyimli 67, orta 77). Round 130 level'ı ~%5 arttı, grid dolunca yükseltme sayısı arttı. Yeni oyuncu bu node'lara pek ulaşmadığı için etkilenmiyor.
   - Test: MechanicsVerification 62/62 (20.000 çekişte Common+Rare %9,6, Legendary %48,9).

## 9. Hasat Kotası (30 Eylül) — Tarla Tükendi'nin yerine

Tarla Tükendi kaldırıldı (kod, alanlar, bildirimler). Yerine görünür, herkes için aynı bir hedef geldi.

- **Kural:** Run 5 round'luk segmentlere bölünür (1–5, 6–10, …). Segmentte kazanılan Harvest Score, segmentin son round'u bitince kotayı geçmeli; geçmezse run anında biter, bekleyen kartlar atlanır.
- **Kota:** `10 × 1,45^(segment−1)`, okunur sayıya yuvarlanır: 10, 15, 20, 30, 45, 65, 95, 130 (r40), 200, 280 (r50) … 1.300 (r70), 12.000 (r100), 110.000 (r130). Kota her segmentte %45 büyür; tarla büyümeyi bırakınca kota onu yakalar.
- **Arayüz:**
  - HUD: round ve süre yazısının altında kota kartı: "KOTA · 3 ROUND · 1.240 / 5.000" ve ilerleme çubuğu (amber; son round'da turuncu; tutunca yeşil, küçük vuruş ve ses).
  - Round intro: segment başında "KOTA 45 · 5 ROUND"; segmentin son round'unda kota tutmadıysa "SON ROUND · KOTAYA 30 KALDI".
  - Round özeti: "Kota 8 / 15 · 4 round kaldı"; son round öncesi kırmızı "SON ROUND · kotaya 7 kaldı"; segment sonunda yeşil "KOTA TAMAM · 52 / 15 · Sıradaki kota: 20".
  - Run sonu: "Kota tutmadı · Round 15 · 12 / 20".
- **Kod:** `HarvestQuota` (eğri, yuvarlama, biçim; simülatör de kullanır), `RoundManager` (Hasat Kotası alanları, `EvaluateQuota`), `QuotaHUD` (RoundUI kurar), `RoundSummaryUI.QuotaNotice`, `GameFeelDirector.QuotaIntro`, `RunComplateUI`.
- **Simülatör (24 seed, 130 round):**

  | Kural | Yeni: biten run | Biten round P10/P50/P90 | r40'a kadar elenen | Orta / Deneyimli |
  |---|---|---|---|---|
  | Tarla Tükendi (eski) | 17/24 | 98 / 105 / 111 | 0 | hiç bitmiyor |
  | Kota 30 · ×1,35 | 18/24 | 30 / 70 / 100 | 3 | hiç bitmiyor |
  | Kota 15 · ×1,4 | 18/24 | 70 / 75 / 100 | 2 | hiç bitmiyor |
  | **Kota 10 · ×1,45 (seçilen)** | 19/24 | 70 / 75 / 95 | 1 | hiç bitmiyor (en dar geçiş kotanın 6–8 katı) |

  - Takılan run'lar Tarla Tükendi'ye göre ~30 round erken bitiyor; ilk 40 round neredeyse herkes için güvenli (demo bölgesi).
  - **Sınır:** Orta ve deneyimli oyuncu için 130 round boyunca kota gerilim yaratmıyor; skor ağaç bitince ~1 milyon/segmentte düzleşiyor. İyi oyuncuya gerilim run uzunluğu kararıyla, boss'larla ya da endless'ta gelecek.
- **Test:** MechanicsVerification 83/83 (kota eğrisi, segmentler, HUD, intro, özet, geçiş, kotayı tutmayınca run sonu), RoundPreviewVerification 47/47.

## 8. Yeni sohbete başlarken
```text
Docs/BalancePass-20260929.md notunu oku; önceki sohbette denge üzerinde çalıştık.
Bölüm 7.3'teki XP eğrisinden devam edelim.
Oyunu denedim, şunları gördüm: ...
Sıradaki iş: ...
```
