# Zincir laboratuvarı (Bölüm 3.7.6)

Sabit tarla ve sabit statlarla tek round; bot oyunun saldırı yoluyla vurur. Kollar: zincir kapalı ("zincirsiz") ve açık ("zincir",
Zincir Hasat asset'inin gerçek değerleri). Aynı düzen, aynı stat, aynı seed. Oranlar aynı seed'in zincirsiz koluna göredir;
medyan (en düşük – en yüksek) · artış olan seed sayısı.

## Toplam hasat ve skor (zincir ÷ zincirsiz)

| Düzen | R23 hasat | R30 hasat | R40 hasat | R23 skor | R30 skor | R40 skor |
|---|---|---|---|---|---|---|
| Patlama ağırlıklı | ×1,05 (0,88–1,24) · 7/10 | ×1,01 (0,88–1,06) · 5/10 | ×1,02 (0,88–1,11) · 6/10 | ×1,07 | ×1,00 | ×0,99 |
| Elektrik ağırlıklı | ×1,00 (0,98–1,03) · 2/10 | ×1,00 (0,79–1,09) · 2/10 | ×1,03 (0,85–1,07) · 6/10 | ×1,00 | ×1,00 | ×1,00 |
| Kasırga / bumerang ağırlıklı | ×1,00 (0,95–1,14) · 2/10 | ×1,00 (1,00–1,05) · 2/10 | ×1,00 (0,89–1,08) · 2/10 | ×1,00 | ×1,00 | ×0,97 |
| Karma davranışlı | ×1,00 (0,96–1,19) · 4/10 | ×1,00 (0,88–1,07) · 1/10 | ×1,06 (0,99–1,08) · 8/10 | ×1,00 | ×1,00 | ×1,07 |
| Davranışsız (kontrol) | ×1,00 (1,00–1,00) · 0/10 | ×1,00 (1,00–1,00) · 0/10 | ×1,00 (1,00–1,00) · 0/10 | ×1,00 | ×1,00 | ×1,00 |

## Hasatın kaynağı ve nesiller (zincir kolu, round başına ortalama)

| Düzen | Round | Toplam hasat (zincirsiz → zincir) | Doğrudan | Normal davranış | Zincir nesil 1 | Zincir nesil 2 | Zincir payı | Deneme 1 / 2 | Tetik 1 / 2 | Yürütülen 1 / 2 | Zincir vuruşu (yeni hücreye) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patlama ağırlıklı | R23 | 50,8 → 53,6 | 36,4 | 14,7 | 2,4 | 0,1 | %4,7 | 10,3 / 1,1 | 3,4 / 0,2 | 3,4 / 0,2 | 11,9 (9,4) |
| Patlama ağırlıklı | R30 | 62,3 → 62,3 | 42,1 | 18,4 | 1,8 | 0,0 | %2,9 | 8,6 / 0,6 | 2,2 / 0,1 | 2,2 / 0,1 | 7,2 (5,5) |
| Patlama ağırlıklı | R40 | 153,6 → 155,6 | 101,3 | 48,7 | 5,5 | 0,1 | %3,6 | 20,8 / 2,5 | 7,9 / 0,5 | 7,9 / 0,5 | 28,2 (20,1) |
| Elektrik ağırlıklı | R23 | 41,0 → 41,1 | 35,4 | 5,1 | 0,6 | 0,0 | %1,5 | 3,1 / 0,2 | 0,7 / 0,0 | 0,7 / 0,0 | 2,2 (1,5) |
| Elektrik ağırlıklı | R30 | 54,8 → 53,5 | 42,9 | 10,3 | 0,3 | 0,0 | %0,6 | 6,3 / 0,1 | 1,4 / 0,0 | 1,4 / 0,0 | 4,1 (3,3) |
| Elektrik ağırlıklı | R40 | 129,7 → 129,2 | 101,2 | 25,7 | 2,3 | 0,0 | %1,8 | 15,2 / 0,9 | 3,4 / 0,0 | 3,4 / 0,0 | 14,2 (11,8) |
| Kasırga / bumerang ağırlıklı | R23 | 42,3 → 42,7 | 32,9 | 8,5 | 1,3 | 0,0 | %3,0 | 4,4 / 0,5 | 1,3 / 0,1 | 1,3 / 0,1 | 5,7 (4,3) |
| Kasırga / bumerang ağırlıklı | R30 | 47,0 → 47,4 | 42,4 | 5,0 | 0,0 | 0,0 | %0,0 | 1,8 / 0,0 | 0,4 / 0,0 | 0,4 / 0,0 | 0,7 (0,5) |
| Kasırga / bumerang ağırlıklı | R40 | 118,6 → 116,7 | 101,8 | 14,0 | 0,8 | 0,1 | %0,8 | 5,6 / 0,4 | 1,5 / 0,1 | 1,5 / 0,1 | 8,1 (5,8) |
| Karma davranışlı | R23 | 40,5 → 42,4 | 35,1 | 6,6 | 0,7 | 0,0 | %1,7 | 4,1 / 0,5 | 0,6 / 0,0 | 0,6 / 0,0 | 2,4 (2,1) |
| Karma davranışlı | R30 | 54,4 → 52,5 | 43,8 | 8,3 | 0,4 | 0,0 | %0,8 | 3,4 / 0,1 | 0,7 / 0,0 | 0,7 / 0,0 | 2,6 (1,9) |
| Karma davranışlı | R40 | 131,5 → 137,4 | 100,6 | 34,5 | 2,3 | 0,0 | %1,7 | 17,1 / 1,3 | 4,7 / 0,1 | 4,7 / 0,1 | 16,2 (11,8) |
| Davranışsız (kontrol) | R23 | 34,3 → 34,3 | 34,3 | 0,0 | 0,0 | 0,0 | %0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 (0,0) |
| Davranışsız (kontrol) | R30 | 42,8 → 42,8 | 42,8 | 0,0 | 0,0 | 0,0 | %0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 (0,0) |
| Davranışsız (kontrol) | R40 | 105,1 → 105,1 | 105,1 | 0,0 | 0,0 | 0,0 | %0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 (0,0) |

## Retler, kuyruk ve havuz (zincir kolu, round başına ortalama; en çok değerler bütün seedlerin en büyüğü)

| Düzen | Round | Tekrar | Nesil sınırı | Kök bütçesi | Geçersiz kaynak | Round sonu | Artçı / ikinci dalga | En çok bekleyen iş | Gecikme ort. / en çok (kare) | Havuz bekleyen iş / iş-kare | Kaynak vurgusu çizilen / atlanan |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patlama ağırlıklı | R23 | 2,2 | 0,1 | 0,0 | 0,0 | 0,0 | 0,0 | 2 | 1,0 / 1 | 0,0 / 0,0 | 3,6 / 0,0 |
| Patlama ağırlıklı | R30 | 2,6 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 2 | 1,0 / 1 | 0,0 / 0,0 | 2,3 / 0,0 |
| Patlama ağırlıklı | R40 | 7,1 | 0,1 | 0,0 | 0,0 | 0,0 | 0,0 | 2 | 1,0 / 1 | 0,0 / 0,0 | 8,4 / 0,0 |
| Elektrik ağırlıklı | R23 | 0,7 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 2 | 1,0 / 1 | 0,0 / 0,0 | 0,7 / 0,0 |
| Elektrik ağırlıklı | R30 | 1,4 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 3 | 1,0 / 1 | 0,0 / 0,0 | 1,4 / 0,0 |
| Elektrik ağırlıklı | R40 | 4,2 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 3,4 / 0,0 |
| Kasırga / bumerang ağırlıklı | R23 | 3,8 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 1,4 / 0,0 |
| Kasırga / bumerang ağırlıklı | R30 | 1,6 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 0,4 / 0,0 |
| Kasırga / bumerang ağırlıklı | R40 | 3,7 | 0,1 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 1,6 / 0,0 |
| Karma davranışlı | R23 | 1,3 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 0,6 / 0,0 |
| Karma davranışlı | R30 | 1,9 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 1 | 1,0 / 1 | 0,0 / 0,0 | 0,7 / 0,0 |
| Karma davranışlı | R40 | 5,4 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 2 | 1,0 / 1 | 0,0 / 0,0 | 4,8 / 0,0 |
| Davranışsız (kontrol) | R23 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0 | – / 0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Davranışsız (kontrol) | R30 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0 | – / 0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Davranışsız (kontrol) | R40 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0 | – / 0 | 0,0 / 0,0 | 0,0 / 0,0 |

## Görsel atlamaları ve kare maliyeti (round başına ortalama; batch'te çizim yok, oyun kodunun kare maliyeti)

| Düzen | Round | ms/kare zincirsiz → zincir | En uzun kare zincirsiz / zincir (ms) | GC toplaması | Atlanan patlama görseli | Atlanan elektrik görseli | Çizilmeyen şimşek | Başlatılamayan bumerang (normal) |
|---|---|---|---|---|---|---|---|---|
| Patlama ağırlıklı | R23 | 0,409 → 0,417 | 17,9 / 13,8 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Patlama ağırlıklı | R30 | 0,423 → 0,423 | 18,3 / 8,7 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Patlama ağırlıklı | R40 | 0,508 → 0,522 | 27,4 / 15,2 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Elektrik ağırlıklı | R23 | 0,387 → 0,390 | 3,9 / 3,6 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Elektrik ağırlıklı | R30 | 0,401 → 0,403 | 3,9 / 4,2 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Elektrik ağırlıklı | R40 | 0,468 → 0,473 | 4,6 / 4,7 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Kasırga / bumerang ağırlıklı | R23 | 0,414 → 0,423 | 3,5 / 3,5 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Kasırga / bumerang ağırlıklı | R30 | 0,423 → 0,426 | 4,0 / 3,9 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Kasırga / bumerang ağırlıklı | R40 | 0,514 → 0,523 | 3,8 / 6,3 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Karma davranışlı | R23 | 0,399 → 0,405 | 4,1 / 4,1 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Karma davranışlı | R30 | 0,417 → 0,420 | 3,6 / 3,9 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Karma davranışlı | R40 | 0,505 → 0,517 | 5,1 / 8,0 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Davranışsız (kontrol) | R23 | 0,383 → 0,383 | 3,4 / 3,5 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Davranışsız (kontrol) | R30 | 0,392 → 0,393 | 2,7 / 3,7 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Davranışsız (kontrol) | R40 | 0,456 → 0,460 | 2,9 / 4,0 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |

