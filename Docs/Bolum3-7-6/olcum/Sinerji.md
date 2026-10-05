# Zincir laboratuvarı (Bölüm 3.7.6)

Sabit tarla ve sabit statlarla tek round; bot oyunun saldırı yoluyla vurur. Kollar: zincir kapalı ("zincirsiz") ve açık ("zincir",
Zincir Hasat asset'inin gerçek değerleri). Aynı düzen, aynı stat, aynı seed. Oranlar aynı seed'in zincirsiz koluna göredir;
medyan (en düşük – en yüksek) · artış olan seed sayısı.

## Toplam hasat ve skor (zincir ÷ zincirsiz)

| Düzen | R30 hasat | R40 hasat | R30 skor | R40 skor |
|---|---|---|---|---|
| Patlama · güçlü sinerji (ek) | ×1,26 (0,88–1,47) · 9/10 | ×1,20 (1,09–1,47) · 10/10 | ×1,27 | ×1,15 |
| Elektrik · güçlü sinerji (ek) | ×1,06 (0,81–1,32) · 6/10 | ×1,06 (0,98–1,19) · 7/10 | ×1,12 | ×1,24 |

## Hasatın kaynağı ve nesiller (zincir kolu, round başına ortalama)

| Düzen | Round | Toplam hasat (zincirsiz → zincir) | Doğrudan | Normal davranış | Zincir nesil 1 | Zincir nesil 2 | Zincir payı | Deneme 1 / 2 | Tetik 1 / 2 | Yürütülen 1 / 2 | Zincir vuruşu (yeni hücreye) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patlama · güçlü sinerji (ek) | R30 | 96,9 → 119,7 | 43,5 | 56,0 | 18,2 | 2,0 | %16,9 | 41,1 / 11,5 | 26,7 / 5,3 | 26,7 / 5,3 | 116,6 (91,9) |
| Patlama · güçlü sinerji (ek) | R40 | 217,3 → 264,9 | 92,0 | 123,9 | 42,7 | 6,3 | %18,5 | 89,4 / 26,1 | 61,4 / 13,1 | 61,4 / 13,1 | 231,2 (172,7) |
| Elektrik · güçlü sinerji (ek) | R30 | 75,0 → 80,0 | 44,0 | 29,2 | 6,5 | 0,3 | %8,5 | 22,6 / 3,1 | 10,1 / 1,3 | 10,1 / 1,3 | 45,4 (40,4) |
| Elektrik · güçlü sinerji (ek) | R40 | 180,5 → 194,2 | 101,5 | 71,5 | 19,3 | 1,9 | %10,9 | 58,4 / 11,6 | 26,8 / 3,9 | 26,8 / 3,9 | 100,0 (83,8) |

## Retler, kuyruk ve havuz (zincir kolu, round başına ortalama; en çok değerler bütün seedlerin en büyüğü)

| Düzen | Round | Tekrar | Nesil sınırı | Kök bütçesi | Geçersiz kaynak | Round sonu | Artçı / ikinci dalga | En çok bekleyen iş | Gecikme ort. / en çok (kare) | Havuz bekleyen iş / iş-kare | Kaynak vurgusu çizilen / atlanan |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Patlama · güçlü sinerji (ek) | R30 | 21,6 | 2,0 | 0,0 | 0,0 | 0,0 | 0,0 | 7 | 1,0 / 1 | 0,0 / 0,0 | 32,0 / 0,0 |
| Patlama · güçlü sinerji (ek) | R40 | 51,1 | 6,3 | 0,0 | 0,0 | 0,0 | 0,0 | 7 | 1,0 / 1 | 0,0 / 0,0 | 74,5 / 0,0 |
| Elektrik · güçlü sinerji (ek) | R30 | 10,0 | 0,3 | 0,0 | 0,0 | 0,0 | 0,0 | 4 | 1,0 / 1 | 0,0 / 0,0 | 11,4 / 0,0 |
| Elektrik · güçlü sinerji (ek) | R40 | 20,8 | 1,9 | 0,0 | 0,0 | 0,0 | 0,0 | 5 | 1,0 / 1 | 0,0 / 0,0 | 30,7 / 0,0 |

## Görsel atlamaları ve kare maliyeti (round başına ortalama; batch'te çizim yok, oyun kodunun kare maliyeti)

| Düzen | Round | ms/kare zincirsiz → zincir | En uzun kare zincirsiz / zincir (ms) | GC toplaması | Atlanan patlama görseli | Atlanan elektrik görseli | Çizilmeyen şimşek | Başlatılamayan bumerang (normal) |
|---|---|---|---|---|---|---|---|---|
| Patlama · güçlü sinerji (ek) | R30 | 0,871 → 0,665 | 1094,7 / 253,7 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Patlama · güçlü sinerji (ek) | R40 | 0,604 → 0,706 | 20,0 / 28,1 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Elektrik · güçlü sinerji (ek) | R30 | 0,531 → 0,597 | 53,8 / 276,5 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |
| Elektrik · güçlü sinerji (ek) | R40 | 0,506 → 0,527 | 12,3 / 12,7 | 0 / 0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 | 0,0 / 0,0 |

