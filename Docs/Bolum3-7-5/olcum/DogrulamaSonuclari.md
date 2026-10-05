# Birikimli run doğrulaması (Bölüm 3.7.5)

Aynı seed, aynı politika; kollar: aday (Run50_KirilmaErisimiV1) · mevcut (Run50_BedelliOdullerV1) · almayan eş (aday profil, kırılma ödülünü almaz).
Birikimli run sabit statlı laboratuvar değildir: ödülden sonra level, kart ve ödül seçimleri de ayrışır.

## Patlama yolu · hedef ödül: `artci_patlama`

Kaynak: `Docs/Bolum3-7-5/olcum/dogrulama/BalanceRuns_k375patlama.csv` · 10 seed × 3 kol

| Seed | Ödül (aday) | Ödül (mevcut) | Sonuç: aday / mevcut / almayan | Son skor: aday / mevcut / almayan | Toplam hasat: aday / mevcut / almayan |
|---|---|---|---|---|---|
| 100 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 825.352 / 494.605 / 602.624 | 25.217 / 15.852 / 17.964 |
| 101 | alınmadı | alınmadı | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 394.903 / 375.863 / 345.780 | 16.833 / 16.687 / 16.156 |
| 102 | R30 | R30 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 399.383 / 300.473 / 323.457 | 18.890 / 15.187 / 16.295 |
| 103 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 853.438 / 452.333 / 484.128 | 30.666 / 20.510 / 22.306 |
| 104 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 676.260 / 398.663 / 522.308 | 19.548 / 15.253 / 19.464 |
| 105 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 1.026.546 / 660.887 / 532.388 | 29.714 / 23.006 / 18.657 |
| 106 | R46 | R46 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 272.872 / 259.493 / 241.122 | 13.318 / 12.574 / 12.158 |
| 107 | R30 | R30 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 672.450 / 600.113 / 565.802 | 25.154 / 22.416 / 21.556 |
| 108 | R36 | R36 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 366.233 / 341.463 / 287.791 | 17.326 / 16.373 / 14.301 |
| 109 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 515.788 / 474.263 / 394.869 | 25.352 / 22.844 / 19.942 |

**Ödülden sonraki round'lar** (ödülün alındığı round'dan sonraki ilk round'dan run sonuna; yalnız ödülü alan 9 seed).
Almayan eş bir kırılma ödülünü mecburen aldıysa (teklifin üçü de kırılma ödülü) ya da erişimi değişen diğer ödül de alındıysa karşılaştırma o round ile biter.

| Seed | Alındığı round | Karşılaştırılan round | Öncesi üç kolda aynı mı | Hasat: aday / mevcut / almayan | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut | Skor: aday ÷ almayan | mevcut ÷ almayan | Yankı hasadı: aday / mevcut | Yankı başına hasat: aday / mevcut |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 100 | R23 | R24–R50 | evet | 23.650 / 14.285 / 16.397 | ×1,44 | ×0,87 | ×1,66 | ×1,38 | ×0,82 | 7.289 / 2.245 | 1,77 / 0,80 |
| 102 | R30 | R31–R50 | evet | 15.478 / 11.775 / 12.883 | ×1,20 | ×0,91 | ×1,31 | ×1,26 | ×0,92 | 5.644 / 1.820 | 1,99 / 0,67 |
| 103 | R23 | R24–R36 · almayan eş R36: artci_patlama | evet | 6.522 / 4.421 / 4.485 | ×1,45 | ×0,99 | ×1,48 | ×1,66 | ×0,96 | 2.634 / 764 | 3,38 / 1,15 |
| 104 | R23 | R24–R36 · R36: cifte_akim de alındı | evet | 5.568 / 4.182 / 5.033 | ×1,11 | ×0,83 | ×1,33 | ×1,35 | ×0,82 | 2.473 / 1.271 | 3,23 / 1,65 |
| 105 | R23 | R24–R50 | evet | 28.307 / 21.599 / 17.250 | ×1,64 | ×1,25 | ×1,31 | ×1,96 | ×1,25 | 9.531 / 3.071 | 1,90 / 0,73 |
| 106 | R46 | R47–R50 | evet | 3.747 / 3.003 / 2.587 | ×1,45 | ×1,16 | ×1,25 | ×1,45 | ×1,26 | 1.351 / 513 | 1,83 / 0,73 |
| 107 | R30 | R31–R50 | evet | 22.028 / 19.290 / 18.430 | ×1,20 | ×1,05 | ×1,14 | ×1,20 | ×1,07 | 7.140 / 3.574 | 1,52 / 0,77 |
| 108 | R36 | R37–R50 | evet | 11.691 / 10.738 / 8.666 | ×1,35 | ×1,24 | ×1,09 | ×1,39 | ×1,27 | 3.608 / 1.362 | 1,70 / 0,59 |
| 109 | R23 | R24–R50 | evet | 22.958 / 20.450 / 17.548 | ×1,31 | ×1,17 | ×1,12 | ×1,32 | ×1,21 | 7.480 / 3.094 | 1,38 / 0,52 |

| Oran (ödülden sonraki round'lar) | Medyan | En düşük – en yüksek | Artış olan seed |
|---|---|---|---|
| Hasat: aday ÷ almayan eş | ×1,35 | ×1,11 – ×1,64 | 9 / 9 |
| Hasat: mevcut ÷ almayan eş | ×1,05 | ×0,83 – ×1,25 | 5 / 9 |
| Hasat: aday ÷ mevcut | ×1,31 | ×1,09 – ×1,66 | 9 / 9 |
| Skor: aday ÷ almayan eş | ×1,38 | ×1,20 – ×1,96 | 9 / 9 |
| Skor: mevcut ÷ almayan eş | ×1,07 | ×0,82 – ×1,27 | 5 / 9 |
| Skor: aday ÷ mevcut | ×1,37 | ×1,09 – ×1,72 | 9 / 9 |

| Round aralığı | Seed | Hasat: aday ÷ almayan (medyan) | mevcut ÷ almayan | aday ÷ mevcut |
|---|---|---|---|---|
| R23–R30 | 5 | ×1,52 | ×1,18 | ×1,27 |
| R31–R40 | 8 | ×1,31 | ×1,01 | ×1,25 |
| R41–R50 | 7 | ×1,37 | ×1,09 | ×1,25 |

## Elektrik yolu · hedef ödül: `cifte_akim`

Kaynak: `Docs/Bolum3-7-5/olcum/dogrulama/BalanceRuns_k375elektrik.csv` · 10 seed × 3 kol

| Seed | Ödül (aday) | Ödül (mevcut) | Sonuç: aday / mevcut / almayan | Son skor: aday / mevcut / almayan | Toplam hasat: aday / mevcut / almayan |
|---|---|---|---|---|---|
| 100 | alınmadı | alınmadı | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 406.617 / 406.617 / 406.617 | 17.343 / 17.343 / 17.343 |
| 101 | R23 | R23 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 496.838 / 315.257 / 406.489 | 15.573 / 12.009 / 13.926 |
| 102 | alınmadı | alınmadı | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 270.260 / 270.260 / 270.260 | 13.648 / 13.648 / 13.648 |
| 103 | R36 | R36 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 268.400 / 231.619 / 240.895 | 14.393 / 12.538 / 13.331 |
| 104 | R26 | R26 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 496.183 / 442.206 / 520.382 | 15.375 / 13.041 / 16.272 |
| 105 | R43 | R43 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 403.412 / 378.520 / 384.322 | 18.647 / 18.047 / 18.247 |
| 106 | R26 | R26 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 327.707 / 331.533 / 271.358 | 16.077 / 16.177 / 13.682 |
| 107 | R30 | R30 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 439.817 / 422.218 / 444.932 | 21.151 / 19.323 / 20.567 |
| 108 | R30 | R30 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 452.927 / 394.675 / 325.255 | 16.912 / 15.868 / 13.728 |
| 109 | R26 | R26 | KAZANDI R50 / KAZANDI R50 / KAZANDI R50 | 767.172 / 497.960 / 525.915 | 22.624 / 18.120 / 19.096 |

**Ödülden sonraki round'lar** (ödülün alındığı round'dan sonraki ilk round'dan run sonuna; yalnız ödülü alan 8 seed).
Almayan eş bir kırılma ödülünü mecburen aldıysa (teklifin üçü de kırılma ödülü) ya da erişimi değişen diğer ödül de alındıysa karşılaştırma o round ile biter.

| Seed | Alındığı round | Karşılaştırılan round | Öncesi üç kolda aynı mı | Hasat: aday / mevcut / almayan | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut | Skor: aday ÷ almayan | mevcut ÷ almayan | Yankı hasadı: aday / mevcut | Yankı başına hasat: aday / mevcut |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 101 | R23 | R24–R30 · R30: artci_patlama de alındı | evet | 1.320 / 1.057 / 1.344 | ×0,98 | ×0,79 | ×1,25 | ×1,03 | ×0,86 | 329 / 151 | 0,96 / 0,50 |
| 103 | R36 | yok: almayan eş R36, cifte_akim | | | | | | | | | |
| 104 | R26 | R27–R36 · R36: artci_patlama de alındı | evet | 2.892 / 2.639 / 3.283 | ×0,88 | ×0,80 | ×1,10 | ×0,78 | ×0,70 | 692 / 230 | 1,07 / 0,38 |
| 105 | R43 | R44–R50 | evet | 8.261 / 7.661 / 7.861 | ×1,05 | ×0,97 | ×1,08 | ×1,10 | ×0,97 | 1.374 / 341 | 0,89 / 0,19 |
| 106 | R26 | R27–R50 | evet | 14.088 / 14.188 / 11.693 | ×1,20 | ×1,21 | ×0,99 | ×1,22 | ×1,24 | 2.732 / 790 | 1,10 / 0,25 |
| 107 | R30 | R31–R50 | evet | 18.540 / 16.712 / 17.956 | ×1,03 | ×0,93 | ×1,11 | ×0,99 | ×0,95 | 3.087 / 1.130 | 0,63 / 0,23 |
| 108 | R30 | R31–R50 | evet | 14.748 / 13.704 / 11.564 | ×1,28 | ×1,19 | ×1,08 | ×1,42 | ×1,23 | 1.806 / 489 | 1,00 / 0,28 |
| 109 | R26 | yok: R23, önce artci_patlama alındı | | | | | | | | | |

| Oran (ödülden sonraki round'lar) | Medyan | En düşük – en yüksek | Artış olan seed |
|---|---|---|---|
| Hasat: aday ÷ almayan eş | ×1,04 | ×0,88 – ×1,28 | 4 / 6 |
| Hasat: mevcut ÷ almayan eş | ×0,95 | ×0,79 – ×1,21 | 2 / 6 |
| Hasat: aday ÷ mevcut | ×1,09 | ×0,99 – ×1,25 | 5 / 6 |
| Skor: aday ÷ almayan eş | ×1,06 | ×0,78 – ×1,42 | 4 / 6 |
| Skor: mevcut ÷ almayan eş | ×0,96 | ×0,70 – ×1,24 | 2 / 6 |
| Skor: aday ÷ mevcut | ×1,12 | ×0,99 – ×1,20 | 5 / 6 |

| Round aralığı | Seed | Hasat: aday ÷ almayan (medyan) | mevcut ÷ almayan | aday ÷ mevcut |
|---|---|---|---|---|
| R23–R30 | 3 | ×1,07 | ×1,01 | ×1,12 |
| R31–R40 | 4 | ×1,04 | ×0,91 | ×1,15 |
| R41–R50 | 4 | ×1,12 | ×1,15 | ×1,06 |

