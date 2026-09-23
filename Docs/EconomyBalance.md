# Uygulanan ekonomi — 24 Eylül 2026

## Hedef ve fiyat kaynağı

İlk 20 round hızlı gelişim; R21–65 daha yavaş ve seçici yatırımlar; R66–100 belirgin hasar/hız artışı; R101–130 iyi build'in gücünü kullanması ve zayıf build'in zorlanması. Herkes aynı sabit fiyatları kullanır. Target round alanları tasarım notudur; satın almayı rounda göre kilitlemez.

140 farklı skill, 304 kademe vardır. Son altı hız node'u üçer kademelidir. Bütçelerin kaynağı [EconomyPricePlan.json](EconomyPricePlan.json); bütün canlı kademe fiyatları [CurrentSkillCosts.md](CurrentSkillCosts.md) içindedir.

## Toplam maliyet

**156.899 Gold + 102.875 Iron + 16.320 Stone.** Bu yalnız skill ağacıdır; saksı satın alımları ayrıca ödenir. Önceki 1.194.135 Gold'luk toplam kaldırıldı. Hasat Zirvesi (H9) yaklaşık 929.999 yerine 18.000 Gold tutar. Gizli 6× geç oyun fiyat çarpanı yoktur; her kolun bütçesi açıkça yazılıdır.

Gold ilk alışverişleri ve hasar/saldırı hızını; Iron üretim, kaynak ve belirli güç yatırımlarını; Stone grid ve bazı son çarpanları taşır. Kaynak dönüştürme yoktur. Ödüller roundla otomatik çarpılmaz.

## Bitki ödülleri ve açılış

Carrot, Lettuce ve Strawberry temel ödülü **3 → 4 Iron** oldu. Pineapple 15 Iron; Pepper/Potato/Tomato 2 Stone; Pumpkin 8 Stone verir. Gold ve XP ödülleri korunur. Skill/tile/rezonans çarpanları ve gerçek tam sayı yuvarlaması ayrıca uygulanır.

Normal başlangıç 80 Gold, 0 Iron, 0 Stone. Debug başlangıç parası ayrı Editor seçeneğidir; simülasyon bu parayı kullanmaz.

| Saksı | Kilit | Satın alma |
|---|---:|---:|
| 1×1 | Açık | 20 Gold |
| 1×2 | Açık | 40 Gold |
| 1×3 | 40 Gold | 70 Gold |
| 2×2 | 100 Gold | 25 Iron |
| 2×3 | 180 Gold | 20 Stone |

Grid: 5×5 80 Gold; 7×7 40 Iron; 9×9 120 Stone; 11×11 450 Stone. Patlama 20 Iron; Duplicate 35 Iron; Tornado 40 Iron; Bumerang Orak 60 Iron; Çapraz Elektrik 80 Iron kart kilididir. Mevcut önkoşullar korunur.

## Hız ve güç

| Stat | Başlangıç | Full tree, tile/rezonans yok |
|---|---:|---:|
| Üretim aralığı | 5 s | 0,5 s |
| Saldırı aralığı | 3 s | 0,2 s |
| Round süresi | 30 s | 90 s |
| Gold / Iron / Stone çarpanı | ×1 | ×4 / ×5 / ×5 |
| XP çarpanı | ×1 | ×3 |
| Nominal doğrudan hasar | 1 | 3.201.552 |

0,5 saniye üretim tabanı ve 0,1 saniye saldırı güvenlik sınırı korunur. Hız node'larında önceki kademe yerini yenisine bırakır; etkiler üst üste bindirilmez. Sonraki hız node'u öncekinin üçüncü kademesini ister. Tam node etkileri önceki tek kademeli sürümle aynıdır; güncel fiyatları CurrentSkillCosts tablosundadır.

İlk hasar node'u +1 / +3 / +8 replacement etkisi, 3 / 5 / 8 Gold fiyatıyla kalır. İlk satın alımdan sonraki normal hasar 2; R1 common 5 HP için üç vuruş gerekir. Sonraki yatırımlar, kritik ve tile etkileri ayrıca uygulanır. HP eğrisi, hasar etkileri ve XP gereksinim eğrisi bu fiyat revizyonunda değiştirilmedi. Önceki XP checkpoint kalibrasyonu yeni fiyatlarla yeniden ölçülmüş bir sonuç sayılmaz.

## Ölçülen gelişim

Referans 30 koşuda tamamlama 30/30; ortalama R99,4, aralık R83–121. Ayrı seed setinde 28/30; tamamlayanlar ortalaması R98,5, aralık R84–117. İki koşu R130'da tamamlanmadı. Referans saksı kilitleri ve ilk örnekleri en geç R17.

| Dönem | Medyan kademe satın alımı / round |
|---|---:|
| R1–20 | 2,85 |
| R21–65 | 1,90 |
| R66–100 | 4,36 |

Bu oranlar farklı skill sayısı değildir. Referans örneklemde R65 nominal hasar yaklaşık 334, R80 2.478, R100 3.201.552 P50 değerine ulaşır. Tamamlama her build'de aynı roundda olmaz.

Model kontrollü hedefleme, kademeli çiftlik yatırımı, gerçek fiyat/etki verileri ve seed'li yaşam döngüsü kullanır. Kart/tile/rezonans ve davranış geri beslemeleri dahil değildir; gerçek fare hareketi tam simüle edilmez. Düşük hedef kapsamalı stres testleri erken de tıkanabiliyor. Her oyuncuya 80 açılmış node veya full ağaç garantisi yoktur. [Ayrıntılı rapor](EconomyAffordability.md) bütün profilleri ve birikimli gelirleri gösterir.

## Doğrulama ve yeniden kullanım

- `node Tools/balance-economy.cjs`: referans simülasyon; asset yazmaz.
- `--validation`: kalibrasyonda kullanılmayan seed'ler.
- `--weak` / `--poor`: düşük hasat verimi stres testleri.
- `--verify-assets`: 140 skill fiyatı, etkisi, önkoşulu ve bitki ödüllerini karşılaştırır.
- `--apply`: yalnız onaylı fiyatları ve açık bitki reward override'larını yazar. Aynı planın doğrulama/stres raporları, tempo ve fiyat tavanı kontrollerini ister. Stat etkisi, HP, XP, sahne veya prefab üretmez.
- `node Tools/report-economy-affordability.cjs`: eşleşen raporlardan ilerleme ve gelir tablosu.
- `node Tools/audit-economy-assets.cjs`: canlı asset'lerden 140 skill'in bütün fiyatları.
- `node Tools/verify-final-tree-scene.cjs`: mevcut 140 sahne bağlantısı.
