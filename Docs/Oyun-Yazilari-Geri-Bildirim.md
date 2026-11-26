# Oyun yazıları ve kart ekranı: geri bildirim (24 Kasım 2026)

Durum (24–26 Kasım):
- **Adım 1 bitti:** 7 tile adı + "Damage Upgrade" tabloda düzeltildi (`LocalizationVerification` 89/89).
- **Adım 2 bitti (Play bakışı bekliyor):** kart bilgi kutusu sahnede, açıklamalar tabloda (`ui.card.info.*`, `ui.card.desc.*`).
- **Adım 3a bitti (K3 terimler):** kullanıcı kararları tabloya işlendi (189 Türkçe yazı; liste `Docs/Ceviri/Yerellestirme.md` > Türkçe oyun terimleri). Cümle yapısı değişmedi. Testler (24 Kasım): çeviri 89/89, kart kutusu 49/49, ATLA 20/20, XP 39/39, Kırılma 189/189 geçti; eski testlerdeki Türkçe ifadeler yeni terimlere çevrildi (90 + 26 satır; koddaki geliştirici doğrulama mesajları, menü yolları ve profil adları değişmedi). **Eskiden kalan, bu işle ilgisiz düşüşler:** 10 eski profil testi (Mekanik, Takvim, BossReward, Ödül Aşamaları, Bedelli, RunPrototype, Run50Referans, Uzmanlaşma, Başlangıç, AlphaDenge) run sonu metnini 22 Kasım'da kite geçen ekranın kullanmadığı eski `scoreText` yazısında arıyor; KırılmaErişimi "yarıçap ×" bekliyor (metinde yok); RunEndScreen'deki 2 buton düşüşü 26 Kasım'da gerçek hata çıktı ve düzeltildi (yukarıda).
- **Adım 3b–4 bitti (26 Kasım):** K1 / K2 / K5 / K6 ve diyaloglar (K4): `Docs/AI-Kokan-Yazilar-Tam-Liste.md` içindeki 215 öneri Türkçeye işlendi, İngilizcesi yeniden yazıldı, ana projenin Unity tablolarına aktarıldı (tr ve en 786 / 786 CSV ile birebir).
- **Taşma testleri (26 Kasım):** kart kutusu, Sınama ödülleri (`RewardTextFitVerification`), diyalog kutuları, tur özeti, yetenek ipuçları, kapris plakası ve başlangıç ekranı (`TextFitTourVerification`), deneme sonu (`RunEndScreenVerification`: artık kardeş yazıların birbirine binmesini, satırda etiket / sayı kutularının ayrıklığını ve dokuz basamaklı sayıları da ölçer). Bulunan iki gerçek hata düzeltildi: deneme sonunda "SINAMA HASADI" sayının üstüne biniyordu (etiket 0–190, sayı 200–420 px); kurulum aracı ikinci çalışınca TEKRAR / ANA MENÜ butonlarını siliyordu. Play'de insan gözüyle bakılmadı.

## Gelen geri bildirim

1. **Yazılar "AI yazısı gibi" duruyor.** Bazı kelimeler ve genel anlatım yapay hissettiriyor.
2. **Kart seçim ekranı:**
   - Kartın adı üstte yazmalı.
   - Fare kartın üstüne gelince, kartın altında ya da üstünde ayrı bir yerde kartın ne yaptığı yazmalı.

> **Açık:** Test edenlerin tek tek söylediği kelimeler hatırlanıyorsa buraya eklensin.
> Aşağıdaki liste tablonun tamamı (773 yazı) okunarak çıkarıldı. Test edenlerin
> gösterdiği kelimeler önceliklidir.

---

## 1. Kart seçim ekranı

### Bugün kartta ne var?

`Grid Mutation Card.prefab` (384×540) yukarıdan aşağıya şunları gösteriyor:

| Yer | Alan | Örnek |
|---|---|---|
| üst %83–97 | **Card Name** (kartın adı) | tile'ın adı |
| orta | Tile Preview (renk karesi) | |
| orta | Effect Name (etki adı) | "XP kazancı" |
| alt | Bonus Caption + Buff Value (değer) | "TILE ETKİSİ" / "XP +%20" |
| en alt | Rarity Label (nadirlik) | "RARE" |

Kartın adı aslında en üstte. **Ama Türkçede ad yerine iç kod adı görünüyor.**
Tablodaki Türkçe tile adlarının 7'si dosya adı olarak kalmış:

| Anahtar | Bugün TR | Öneri TR | EN |
|---|---|---|---|
| `data.tile.crystal_rarespawnchance.name` | Crystal-RareSpawnChance | **Kristal** | Crystal |
| `data.tile.odak_hasar.name` | Odak-Hasar | **Odak** | Focus |
| `data.tile.duplicate_duplicatechance.name` | Duplicate-DuplicateChance | **Çoğaltma** | Duplicate |
| `data.tile.energy_harvestscoremultiplier.name` | Energy-HarvestScoreMultiplier | **Enerji** | Energy |
| `data.tile.explosive_explosionchance.name` | Explosive-ExplosionChance | **Patlayıcı** | Explosive |
| `data.tile.fertile_plantspawnrate.name` | Fertile-PlantSpawnRate | **Verimli** (düzeltme 26 Kasım: "Verimli Toprak" rezonansı bu parselden değil Çoğaltma parselinden geliyor; ad yine uygun, ilk gerekçe yanlıştı) | Fertile |
| `data.tile.water_xpgain.name` | Water-XPGain | **Su** | Water |

Oyuncu kartın üstünde "CRYSTAL-RARESPAWNCHANCE" okuyor; bunu ad olarak algılamaması normal.
Geri bildirimdeki "kartın adı yazmalı" maddesinin büyük kısmı muhtemelen bu hatadan geliyor.
Yetenek ağacında da benzer bir kalıntı var: `data.skill.damage_upgrade.name` → "Damage Upgrade".

### Kart açıklama kutusu: uygulandı (24 Kasım)

Aşağıdaki öneriden farkı: kutu kartların **üstünde** duruyor. Altta ATLA ve kalan seçim plakası olduğu için yer yoktu.
Kutu, kart sırasının ortasına bağlı. Alt kenarı kartların 12 px üstünde, kartlara binmiyor.
Fare kartlar arasındaki boşluktan geçerken kutu boşa dönmüyor, son kartı yazmaya devam ediyor; böylece titreme olmuyor.
Kart değiştikçe yazı da değişiyor.
Kutu yalnız yeni tile kartlarında görünüyor. Yükseltme kartlarında ve hepsi en üst seviyedeyken gelen Temel güç kartlarında
üstte hiçbir şey yok; eski "GRİD DOLU" şeridi de kaldırıldı (kullanıcı, 24 Kasım). Açıklama, krem alanın dikey ortasında. Kurulum: Tools > Comic UI > Kart Seçimi: Kartlar + ATLA + Kalan Seçim + Bilgi Kutusu.
Test: `CardInfoBoxVerification`. Görüntüler kopya projede: `Library/VerificationProject/Logs/CardInfo/`.

### Kart açıklaması: ilk öneri

Kartta açıklamaya yer yok. Ad, renk karesi, etki ve değer kartı dolduruyor.
Bu yüzden açıklamayı kartın içine sıkıştırmak yerine **kart sırasının altında tek bir ortak kutu** öneriyorum:

```
 ┌────────┐  ┌────────┐  ┌────────┐
 │ KRİSTAL│  │  ODAK  │  │   SU   │      ← ad üstte (zaten orada)
 │  ▓▓▓▓  │  │  ▓▓▓▓  │  │  ▓▓▓▓  │
 │ +5 nad.│  │ +%12   │  │ XP +%20│
 └────────┘  └────────┘  └────────┘
 ┌──────────────────────────────────────┐
 │ KRİSTAL                              │  ← fare hangi kartın
 │ Üstüne koyduğun saksıdan nadir bitki │    üstündeyse onun
 │ çıkma şansı artar.                   │    açıklaması
 └──────────────────────────────────────┘
```

- Kutu tek tane olur ve kartlar arasında gezince yazısı değişir. Her kartın kendi açılır kutusu olmaz,
  böylece kartlar birbirinin üstüne binmez.
- Fare hiçbir kartın üstünde değilken kutu boş kalmaz, ekranın ne işe yaradığını yazar:
  "Bir kart seç. Kart tarlaya yeni bir tile koyar."
- Görünüm comic kitinden gelir (krem şerit + Bungee başlık + okunur gövde yazısı).
  Yeni süs, yıldız ya da ipucu eklenmez.
- Kutu, mevcut araca (Tools > Comic UI > Kart Seçimi) eklenir ve sahneye kaydedilir.
  Böylece editörde Play'e basmadan görülüp düzenlenebilir (AGENTS.md editör kuralı).
- Grid dolu iken çıkan **yükseltme kartı** ve **Temel güç** kartı için de birer açıklama satırı gerekir.

### Kart açıklamaları: taslak

Bunlar taslak. Her cümle koddaki davranışla karşılaştırıldı (24 Kasım, `PlanterBrain`): tile'ın etkisi yalnız
üstündeki saksıya uygulanıyor; davranışlar (patlama, kasırga, orak, elektrik) o saksının bitkisi hasat edilince şansla tetikleniyor.

| Tile | Taslak açıklama |
|---|---|
| Kristal | Üstüne koyduğun saksıdan nadir bitki çıkma şansı artar. |
| Odak | Bu saksının bitkilerine doğrudan vuruşun daha çok hasar verir. |
| Çoğaltma | Bu saksının bitkisini hasat edince ödül bazen iki kat gelir. |
| Enerji | Bu saksıdan gelen hasat skoru artar. |
| Patlayıcı | Bu saksının bitkisi hasat edilince bazen patlar ve çevresine vurur. |
| Verimli | Bu saksı bitkileri daha sık çıkarır. |
| Su | Bu saksıdan daha çok XP kazanırsın. |
| Tornado | Bu saksıdan bazen tarlada dolaşan bir kasırga çıkar. |
| Bumerang Orak | Bu saksıdan bazen gidip geri dönen bir orak fırlar. |
| Çapraz Elektrik | Bu saksıdan bazen çapraz yönde elektrik sıçrar. |
| Yükseltme kartı | Tarlada zaten olan bir tile'ı güçlendirir. |
| Temel güç | Tile'ların hepsi en üst seviyede. Bu kart küçük ve kalıcı bir güç verir. |

Ortak kural (ilk kez okuyan oyuncu için): "Tile'lar, üstüne koyduğun saksıyı etkiler.
Aynı tile'lardan birkaçını bir saksının altında toplarsan rezonans oluşur."

---

## 2. AI kokan yazılar

Tablodaki 773 Türkçe yazının hepsi okundu. Sorun tek tek kelimelerden çok **tekrar eden kalıplar**.
Her kalıbı düzeltmek için bir kural öneriyorum; kurallar onaylanınca yazılar bu kurallara göre yeniden yazılacak.

### K1. " · " ile bağlanan parça cümleler (106 yazı)

En belirgin iz bu. Cümle yerine nokta işaretiyle dizilmiş, rapor gibi okunan parçalar.

| Anahtar | Bugün | Öneri |
|---|---|---|
| `ui.start.subtitle` | Bir çiftçi ve bir tırpan seç · etkiler bu run boyunca geçerli · yeni run'da yeniden uygulanır | Bir çiftçi ve bir tırpan seç. Seçimin bu run boyunca geçerli. |
| `ui.reward.panel.tail` | Yalnız biri alınır · run sonuna kadar geçerli · etkisi Round {0} ve sonrası | Birini seç. Round {0}'dan itibaren run sonuna kadar geçerli. |
| `ui.spec.subtitle` | Yalnız biri alınır · run sonuna kadar geçerli · geri alınamaz · etkisi Round {0} ve sonrası | Birini seç. Geri alınamaz, Round {0}'dan run sonuna kadar sürer. |
| `ui.field.shell.prep` | İki bölgeden biri kapanacak · bir düğümü kır: onun bölgesi kurtulur, diğeri kapanır · seçmezsen A kapanır | İki bölgeden biri kapanacak. Hangi düğümü kırarsan onun bölgesi kurtulur. Hiçbirini kırmazsan A kapanır. |
| `ui.card.grid_full` | GRİD DOLU · kart, seçtiğin tile'ı seviye atlatır | TARLA DOLU. Seçtiğin kart bir tile'ı yükseltir. |

**Kural:** Oyuncunun okuduğu açıklama tam cümle olur. " · " yalnızca HUD'daki kısa değer satırlarında kalır,
ör. "Kaynak ×2 · XP ×2". Bir yazıda en fazla bir tane olur.

### K2. Geliştirici dili oyuncuya sızmış

Ölçüm, denge ya da profil dili oyuncuya gösterilmemeli.

| Anahtar | Bugün | Öneri |
|---|---|---|
| `data.start.bahcivan.style` | Referans çiftçi: oyunun kendi dengesi. | Artısı da eksisi de yok. İlk run için iyi. |
| `ui.start.neutral.detail` | oyunun kendi dengesi | artı da yok, eksi de |
| `ui.skill.granted.desc` | Bu düğüm satın alınmadı; bu profilde ücret ödenmez. | (oyuncuya hiç görünmemeli; görünüyorsa kaldır) |
| `ui.skill.disabled.desc` / `desc2` | Bu deneyde round süresi sabit ({0} sn). / Bu node etkisiz ve satın alınamaz. | (aynı) |
| `ui.hud.level_work` | SEVİYE KAZANIMLARI HESAPLANIYOR | SEVİYE ATLIYORSUN! (ya da hiç gösterilmez) |
| `ui.runend.times.level_work` | seviye hesaplama {0} | (oyuncuya gerekmez) |
| `data.reward.asiri_yukleme.note` | Ek proc zincire katılmaz ve yeni zar atmaz. | Bu ek vuruş zincir başlatmaz. |
| `data.reward.bereketli_toprak.note` | … (son round ortalama doluluk %65'in altında). Taban 0,50 sn. | Tarlan boş kalıyorsa çıkar. |
| `data.reward.hilal_darbesi.note` | … Hız 8 birim/sn; temel menzil 3, Büyüyen Ay ile 4 birimdir. | Hilal, imleç halkanın kenarından rastgele bir yöne uçar. |
| `data.profile.*.victory` | RUN TAMAMLANDI · ALPHA DENGE V1 | (oyuncuya profil adı gösterilmemeli) |
| `data.boss.kuralsiz_boss.name` | KURALSIZ BOSS | (gerçekten görünüyorsa ad verilmeli) |
| `ui.shop.incomplete` | Saksı ayarı eksik. | (hata mesajı; oyuncuya çıkmamalı) |

### K3. Aynı şeye iki-üç ad, Türkçe ile İngilizce karışık (159 yazıda İngilizce kelime)

Tutarsız terim, metne "çevirisi yarım kalmış" havası veriyor. Örnekler:

| Kavram | Bugün geçen biçimler |
|---|---|
| seviye | Level · SEVİYE · Sv · Lv. · LEVEL UP! |
| saldırı aralığı | Atak aralığı · Saldırı aralığı |
| kasırga | Tornado · kasırga |
| bumerang | Orak · Bumerang Orak · bumerang |
| tarla | Grid · GRİD · tarla |
| dükkân | dükkân · mağaza · shop |
| imleç | imleç · mouse · fare |
| kaynak | Gold · Iron · Stone ↔ Altın · Demir · Taş (`ui.summary.*`) |
| nadirlik | Common/Rare/Legendary · rarity · nadirlik · Leg |
| skor | Harvest Score · Hasat skoru · HASAT SKORU |
| kota | Kota · Adak (Eri'nin dilinde) · segment kotası |

**Karar verildi (24 Kasım):** run → deneme, round → tur, boss → Sınama, tile → parsel, XP → TP, seviye, Altın / Demir / Taş, Türkçe nadirlik. Aşağıdaki ilk öneri tarihçe için duruyor.

İlk öneri: `Docs/Ceviri/Terimler.csv` dosyasına göre oyunda "run" kalıyor; Adak, Sınama, saksı ve tarla Türkçe.
Geri kalanlar için tek bir seçim yapılmalı. Önerim:

- **İngilizce kalsın** (oyuncunun türden tanıdığı): run, round, boss, tile, XP.
- **Türkçeye geçsin:** Level → seviye, Grid → tarla, Gold/Iron/Stone → Altın/Demir/Taş,
  Common…Legendary → Sıradan / Seyrek / Nadir / Destansı / Efsanevi (ya da beşi de İngilizce kalsın,
  ama karışık olmasın), Harvest Score → Hasat skoru, mouse → imleç, Tornado → Kasırga, crit → kritik, MAX → EN ÜST.
- "Segment" oyuncuya hiç görünmesin; yerine "dönem" (Adak'ın dönemi) kullanılsın.

### K4. Diyaloglarda yapay "anlamlı" ton

65 diyalog satırında 20 üç nokta (…), 4 kez "değil mi?", 2 kez "Boş ver." var.
Hikâyenin kendisi iyi; ama ritim hep aynı: kısa vecize, üç nokta, retorik soru.
Bu kalıp AI yazısının en tanınan izi.

| Anahtar | Bugün | Öneri yönü |
|---|---|---|
| `end.quota.0` | Adak eksik kaldı. Açlık… beklemez. | Adak eksik kaldı. Karnım hâlâ guruldıyor. |
| `end.quota.1` | Yeniden başlarız. Toprak hep burada. | Neyse, baştan alırız. Ben bir yere gitmiyorum. |
| `intro.select.1` | Ben Eri. Bu gezegen benim; toprak benim, eller sizin. | Ben Eri. Bu gezegen benim. Toprak benden, emek senden. |
| `tut.level.0` | Büyüyorsun! Birini seç; bu hasat boyunca seninle kalır. | Büyüyorsun! Birini seç, bu run'ın sonuna kadar senin. |
| `story.reward1.1` | Ben de seviyorum. Güçlü eller çok kıymetli. | Ben de seviyorum. Hem de çok. |
| `story.r20.1` / `story.r25.reply.a` | …Boş ver. … | İkisinden biri kalsın, diğeri değişsin. |
| `story.victory.3a` | Bana güvenmiştin. En tatlısı buydu. | (vecize yerine Eri'nin kendi sesi: oyuncuyla dalga geçen, iştahlı) |

**Kural:** Bir konuşmada en fazla bir üç nokta olur. Noktalı virgül konuşmada kullanılmaz.
Retorik soru en fazla her üç konuşmada bir gelir. Eri'nin sesi `Terimler.csv`'deki gibi
"doymak bilmeyen" bir karakter: vecize söyleyen bilge değil, iştahlı ve biraz şımarık.
Yeniden yazım hikâyeye ve onaylı Eri planına (14 taş / 14 konuşma) dokunmaz; yalnızca cümleleri değiştirir.

### K5. Slogan ve üçlü yapı

| Anahtar | Bugün | Öneri |
|---|---|---|
| `ui.shop.tagline` | SEÇ. EK. BÜYÜT. | kaldır (başlık zaten SAKSILAR) |
| `ui.spec.neutral` | Bonus yok, ceza yok | Olduğu gibi kalsın |
| `ui.runend.win_title` | ZAFER! | (kalabilir; "ADAK DOLDU!" gibi oyuna özgü bir şey daha iyi olabilir) |

### K6. Her notta "Yalnız kendi vuruşun." tekrarı

Ödül notlarının çoğu aynı uyarıyla başlıyor ("Yalnız kendi vuruşun. Davranış hasarı değişmez.").
**Kural:** Bu ayrım bir kez, ödül panelinde öğretilsin. Notlarda yalnız oyuncuyu şaşırtacak istisna kalsın.

### Küçük hatalar

- `ui.game.restart` "Yeniden Başla<br>", `ui.menu.play` "Oyna<br>": sonda gereksiz satır sonu var. Yerleşim için konmuşsa sorun değil, kontrol edilecek.
- `stat.harvest_damage` "Saksı hasar statı": "stat" oyuncu dili değil → "Hasat hasarı".
- `stat.reroll_card` ve `stat.card_skip` ikisi de "Kart atlama".
- `ui.skill.card_rarity_odds` "Epic+ {0} · Leg {1}": kısaltma.

---

## Önerilen sıra

1. **Hemen:** 7 tile adı + "Damage Upgrade" (iç ad hatası; karar gerektirmiyor, yalnız ad onayı).
2. **Kart açıklama kutusu:** editör aracıyla sahneye; açıklama yazıları onaylanınca.
3. **Terim kararları (K3)**, ardından K1 / K2 / K5 / K6 toplu düzeltme. Yaklaşık 250 yazı.
4. **Diyaloglar (K4):** ayrı bir tur. Ses en çok burada önemli; satır satır birlikte gidilmeli.

Her adımdan sonra `LocalizationVerification` çalıştırılacak ve sonucu bildirilecek.
Diğer dillere Türkçe ve İngilizce kesinleşmeden dokunulmayacak.
