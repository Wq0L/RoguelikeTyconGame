# Bedelli ödüller — kontrollü kısa karşılaştırma (Bölüm 3.7.4)

Üretim: `BedelliOdulMeasurement.Run` (izole kopya). **Denge ölçümü değildir.** Run oynanmadı; sabit bir laboratuvar durumu kuruldu.

- Profil: `Run50_BedelliOdullerV1` · round'lar: R27–R29 (bu round'larda boss ve kota kontrolü yok) · round süresi 45 sn · kare adımı 1/30 sn
- Tarla: 5×5 açık, 25 tek hücrelik saksı; 8'inin altında davranış tile'ı (2 patlama %30, 2 elektrik %40, 2 kasırga %22, 2 bumerang %18)
- Sabitlenen statlar: hasar 25, saldırı aralığı 1.8 sn, yarıçap 1.4, XP kazancı ×2; kritik ve diğer statlar profilin tabanı; ağaç ve başka ödül yok
- Başlangıç level'ı 24 (XP tablosu profilin kendi tablosu); XP gerçek hasattan gelir, level kartları gerçek kart ekranından alınır
- Nişan: saldırı alanında en çok canlı bitki olan nokta (hücre merkezleri ve ara noktalar) · kart: her ekranda ilk aday
- Kollar: ödülsüz · Bereketli Öğrenim · Davranışa Adanış (ödül round başlamadan verilir) · seed'ler: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 (üç kolda aynı)

Hasar: "vuruş" = uygulanan vuruşların toplamı (fazlası dahil); "can" = bitkinin kalan canını aşmayan kısmı.

## Kol ortalamaları (12 seed, 3 round toplamı)

| Kol | Saldırı | Doğrudan vuruş / can | Doğrudan vuruş başına | Davranış vuruş / can | Davranış vuruşu (adet) | Hasat (doğrudan + davranış) | XP | Level | Verilen seçim hakkı | Alınan kart |
|---|---|---|---|---|---|---|---|---|---|---|
| Ödülsüz | 74 | 13628 / 11650 | 32.4 | 930 / 845 | 43.3 | 128.1 (118.5 + 9.6) | 6563 | 3 | 9 | 9 |
| Bereketli Öğrenim | 74 | 11055 / 9685 | 26.0 | 780 / 697 | 36.5 | 104.5 (96.8 + 7.8) | 5283 | 2 | 8 | 8 |
| Davranışa Adanış | 74 | 13527 / 11588 | 32.4 | 1390 / 1141 | 43.4 | 130.8 (116.8 + 14.1) | 6680 | 3 | 6 | 6 |

## Seed başına (3 round toplamı)

| Seed | Kol | Doğrudan vuruş / can | Davranış vuruş / can | Patlama · elektrik · kasırga · bumerang (vuruş) | Hasat (doğrudan + davranış) | XP | Level | Level başına hak | Verilen hak | Alınan kart | Bekleyen |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Ödülsüz | 13768 / 11770 | 646 / 562 | 200 · 350 · 48 · 48 | 125 (117 + 8) | 6538 | 3 | 3 | 9 | 9 | 0 |
| 1 | Bereketli Öğrenim | 11243 / 9935 | 931 / 806 | 375 · 500 · 24 · 32 | 101 (90 + 11) | 5420 | 2 | 4 | 8 | 8 | 0 |
| 1 | Davranışa Adanış | 13411 / 11451 | 1818 / 1333 | 570 · 912 · 216 · 120 | 133 (111 + 22) | 6820 | 3 | 2 | 6 | 6 | 0 |
| 2 | Ödülsüz | 13820 / 11873 | 625 / 547 | 325 · 300 · 0 · 0 | 126 (118 + 8) | 6511 | 3 | 3 | 9 | 9 | 0 |
| 2 | Bereketli Öğrenim | 11110 / 9802 | 866 / 767 | 300 · 350 · 216 · 0 | 102 (94 + 8) | 5300 | 2 | 4 | 8 | 8 | 0 |
| 2 | Davranışa Adanış | 13107 / 11412 | 1370 / 1134 | 304 · 874 · 144 · 48 | 129 (117 + 12) | 6580 | 3 | 2 | 6 | 6 | 0 |
| 3 | Ödülsüz | 13450 / 11692 | 957 / 842 | 250 · 575 · 84 · 48 | 126 (114 + 12) | 6420 | 3 | 3 | 9 | 9 | 0 |
| 3 | Bereketli Öğrenim | 11427 / 9970 | 769 / 737 | 75 · 550 · 144 · 0 | 102 (98 + 4) | 5268 | 2 | 4 | 8 | 8 | 0 |
| 3 | Davranışa Adanış | 13749 / 11891 | 1274 / 960 | 456 · 494 · 252 · 72 | 135 (119 + 16) | 6860 | 3 | 2 | 6 | 6 | 0 |
| 4 | Ödülsüz | 13633 / 11587 | 928 / 847 | 200 · 500 · 84 · 144 | 125 (117 + 8) | 6320 | 3 | 3 | 9 | 9 | 0 |
| 4 | Bereketli Öğrenim | 11054 / 9698 | 581 / 488 | 100 · 325 · 156 · 0 | 106 (99 + 7) | 5154 | 2 | 4 | 8 | 8 | 0 |
| 4 | Davranışa Adanış | 13167 / 11250 | 1608 / 1347 | 304 · 836 · 324 · 144 | 131 (116 + 15) | 6820 | 3 | 2 | 6 | 6 | 0 |
| 5 | Ödülsüz | 13121 / 11379 | 1214 / 1099 | 425 · 625 · 36 · 128 | 124 (110 + 14) | 6500 | 3 | 3 | 9 | 9 | 0 |
| 5 | Bereketli Öğrenim | 11231 / 9710 | 1199 / 1069 | 350 · 525 · 180 · 144 | 113 (100 + 13) | 5540 | 2 | 4 | 8 | 8 | 0 |
| 5 | Davranışa Adanış | 13365 / 11504 | 1678 / 1326 | 646 · 798 · 90 · 144 | 133 (111 + 22) | 6760 | 3 | 2 | 6 | 6 | 0 |
| 6 | Ödülsüz | 14043 / 12070 | 570 / 501 | 0 · 450 · 120 · 0 | 128 (121 + 7) | 6592 | 3 | 3 | 9 | 9 | 0 |
| 6 | Bereketli Öğrenim | 10700 / 9375 | 855 / 805 | 475 · 200 · 132 · 48 | 108 (101 + 7) | 5261 | 2 | 4 | 8 | 8 | 0 |
| 6 | Davranışa Adanış | 13315 / 11637 | 1188 / 1105 | 114 · 456 · 378 · 240 | 123 (112 + 11) | 6540 | 3 | 2 | 6 | 6 | 0 |
| 7 | Ödülsüz | 13475 / 11635 | 500 / 449 | 225 · 175 · 36 · 64 | 124 (117 + 7) | 6529 | 3 | 3 | 9 | 9 | 0 |
| 7 | Bereketli Öğrenim | 10691 / 9499 | 498 / 444 | 125 · 325 · 48 · 0 | 101 (96 + 5) | 5000 | 2 | 4 | 8 | 8 | 0 |
| 7 | Davranışa Adanış | 13278 / 11542 | 1686 / 1327 | 532 · 836 · 270 · 48 | 134 (112 + 22) | 6900 | 3 | 2 | 6 | 6 | 0 |
| 8 | Ödülsüz | 13837 / 11673 | 1429 / 1326 | 575 · 550 · 288 · 16 | 139 (125 + 14) | 6955 | 3 | 3 | 9 | 9 | 0 |
| 8 | Bereketli Öğrenim | 11044 / 9593 | 657 / 567 | 275 · 350 · 0 · 32 | 104 (97 + 7) | 5275 | 2 | 4 | 8 | 8 | 0 |
| 8 | Davranışa Adanış | 14164 / 12041 | 1216 / 1045 | 532 · 456 · 180 · 48 | 133 (123 + 10) | 6640 | 3 | 2 | 6 | 6 | 0 |
| 9 | Ödülsüz | 13825 / 11642 | 1031 / 943 | 275 · 500 · 144 · 112 | 134 (124 + 10) | 6542 | 3 | 3 | 9 | 9 | 0 |
| 9 | Bereketli Öğrenim | 10983 / 9617 | 978 / 883 | 125 · 525 · 264 · 64 | 109 (98 + 11) | 5380 | 2 | 4 | 8 | 8 | 0 |
| 9 | Davranışa Adanış | 13471 / 11387 | 1110 / 899 | 76 · 722 · 144 · 168 | 131 (122 + 9) | 6296 | 3 | 2 | 6 | 6 | 0 |
| 10 | Ödülsüz | 13615 / 11556 | 1072 / 994 | 525 · 275 · 240 · 32 | 128 (122 + 6) | 6500 | 3 | 3 | 9 | 9 | 0 |
| 10 | Bereketli Öğrenim | 11072 / 9560 | 853 / 741 | 375 · 350 · 0 · 128 | 105 (95 + 10) | 5320 | 2 | 4 | 8 | 8 | 0 |
| 10 | Davranışa Adanış | 14005 / 11799 | 960 / 834 | 494 · 304 · 162 · 0 | 128 (118 + 10) | 6700 | 3 | 2 | 6 | 6 | 0 |
| 11 | Ödülsüz | 13660 / 11716 | 959 / 879 | 375 · 400 · 120 · 64 | 125 (116 + 9) | 6840 | 3 | 3 | 9 | 9 | 0 |
| 11 | Bereketli Öğrenim | 11306 / 9879 | 721 / 632 | 225 · 400 · 48 · 48 | 105 (97 + 8) | 5440 | 2 | 4 | 8 | 8 | 0 |
| 11 | Davranışa Adanış | 13787 / 11631 | 1728 / 1522 | 646 · 1064 · 18 · 0 | 133 (122 + 11) | 6885 | 3 | 2 | 6 | 6 | 0 |
| 12 | Ödülsüz | 13292 / 11209 | 1226 / 1149 | 375 · 675 · 144 · 32 | 133 (121 + 12) | 6511 | 3 | 3 | 9 | 9 | 0 |
| 12 | Bereketli Öğrenim | 10799 / 9582 | 450 / 428 | 250 · 200 · 0 · 0 | 98 (96 + 2) | 5040 | 2 | 4 | 8 | 8 | 0 |
| 12 | Davranışa Adanış | 13508 / 11506 | 1042 / 854 | 342 · 532 · 0 · 168 | 127 (118 + 9) | 6357 | 3 | 2 | 6 | 6 | 0 |

## Tekrarlanabilirlik

İlk seed'in üç kolu ikinci kez çalıştırıldı: 9 round satırından bütün sütunlarıyla aynı çıkan 9.
