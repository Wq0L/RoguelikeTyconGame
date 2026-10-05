# Bedelli ödüller — kontrollü kısa karşılaştırma (Bölüm 3.7.4)

Üretim: `BedelliOdulMeasurement.Run` (izole kopya). **Denge ölçümü değildir.** Run oynanmadı; sabit bir laboratuvar durumu kuruldu.

- Profil: `Run50_BedelliOdullerV1` · round'lar: R24–R25 (boss ve kota kontrolü yok) · round süresi 45 sn · kare adımı 1/30 sn
- Tarla: 5×5 açık, 25 tek hücrelik saksı; 8'inin altında davranış tile'ı (2 patlama %30, 2 elektrik %40, 2 kasırga %22, 2 bumerang %18)
- Sabitlenen statlar: hasar 25, saldırı aralığı 1.8 sn, yarıçap 1.4; kritik ve diğer statlar profilin tabanı; ağaç ve başka ödül yok
- Başlangıç level'ı 28 (XP tablosu profilin kendi tablosu); XP gerçek hasattan gelir
- Nişan: saldırı alanında en çok canlı bitki olan nokta (hücre merkezleri ve ara noktalar) · kart: her ekranda ilk aday
- Kollar: ödülsüz · Bereketli Öğrenim · Davranışa Adanış (ödül round başlamadan verilir) · seed'ler: 1, 2, 3, 4, 5, 6 (üç kolda aynı)

Hasar: "vuruş" = uygulanan vuruşların toplamı (fazlası dahil); "can" = bitkinin kalan canını aşmayan kısmı.

## Kol ortalamaları (6 seed, iki round toplamı)

| Kol | Saldırı | Doğrudan vuruş / can | Doğrudan vuruş başına | Davranış vuruş / can | Davranış vuruşu (adet) | Hasat (doğrudan + davranış) | XP | Level | Verilen seçim hakkı | Alınan kart |
|---|---|---|---|---|---|---|---|---|---|---|
| Ödülsüz | 49 | 8819 / 7285 | 32.4 | 809 / 689 | 38.3 | 95.3 (84.7 + 10.7) | 2437 | 0.17 | 0.5 | 0.5 |
| Bereketli Öğrenim | 49 | 7274 / 6287 | 26.2 | 541 / 440 | 25.5 | 80.3 (72.3 + 8) | 2017 | 0 | 0 | 0 |
| Davranışa Adanış | 49 | 8933 / 7343 | 32.7 | 791 / 631 | 24.8 | 93 (83.3 + 9.7) | 2385 | 0 | 0 | 0 |

## Seed başına (iki round toplamı)

| Seed | Kol | Doğrudan vuruş / can | Davranış vuruş / can | Patlama · elektrik · kasırga · bumerang (vuruş) | Hasat (doğrudan + davranış) | XP | Level | Level başına hak | Verilen hak | Alınan kart | Bekleyen |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Ödülsüz | 8959 / 7458 | 821 / 716 | 375 · 350 · 96 · 0 | 100 (89 + 11) | 2620 | 1 | 3 | 3 | 3 | 0 |
| 1 | Bereketli Öğrenim | 7421 / 6414 | 700 / 547 | 200 · 500 · 0 · 0 | 84 (75 + 9) | 2120 | 0 | 4 | 0 | 0 | 0 |
| 1 | Davranışa Adanış | 9092 / 7473 | 1040 / 900 | 266 · 570 · 108 · 96 | 90 (81 + 9) | 2460 | 0 | 2 | 0 | 0 | 0 |
| 2 | Ödülsüz | 8482 / 6913 | 803 / 674 | 250 · 325 · 228 · 0 | 94 (82 + 12) | 2310 | 0 | 3 | 0 | 0 | 0 |
| 2 | Bereketli Öğrenim | 7123 / 6181 | 325 / 297 | 150 · 175 · 0 · 0 | 76 (74 + 2) | 1920 | 0 | 4 | 0 | 0 | 0 |
| 2 | Davranışa Adanış | 8720 / 7169 | 418 / 346 | 304 · 114 · 0 · 0 | 88 (84 + 4) | 2250 | 0 | 2 | 0 | 0 | 0 |
| 3 | Ödülsüz | 8809 / 7360 | 810 / 671 | 475 · 275 · 60 · 0 | 93 (81 + 12) | 2460 | 0 | 3 | 0 | 0 | 0 |
| 3 | Bereketli Öğrenim | 7428 / 6487 | 409 / 298 | 125 · 200 · 84 · 0 | 78 (70 + 8) | 1910 | 0 | 4 | 0 | 0 | 0 |
| 3 | Davranışa Adanış | 9031 / 7533 | 854 / 619 | 342 · 266 · 198 · 48 | 96 (82 + 14) | 2460 | 0 | 2 | 0 | 0 | 0 |
| 4 | Ödülsüz | 8703 / 7152 | 859 / 725 | 325 · 450 · 84 · 0 | 92 (83 + 9) | 2320 | 0 | 3 | 0 | 0 | 0 |
| 4 | Bereketli Öğrenim | 7127 / 6210 | 614 / 504 | 100 · 350 · 84 · 80 | 80 (71 + 9) | 2050 | 0 | 4 | 0 | 0 | 0 |
| 4 | Davranışa Adanış | 8611 / 6980 | 980 / 820 | 304 · 418 · 234 · 24 | 94 (83 + 11) | 2330 | 0 | 2 | 0 | 0 | 0 |
| 5 | Ödülsüz | 8668 / 7283 | 991 / 833 | 300 · 475 · 216 · 0 | 97 (84 + 13) | 2480 | 0 | 3 | 0 | 0 | 0 |
| 5 | Bereketli Öğrenim | 7068 / 5963 | 506 / 466 | 75 · 175 · 96 · 160 | 77 (70 + 7) | 1930 | 0 | 4 | 0 | 0 | 0 |
| 5 | Davranışa Adanış | 8995 / 7456 | 528 / 330 | 190 · 266 · 72 · 0 | 92 (83 + 9) | 2380 | 0 | 2 | 0 | 0 | 0 |
| 6 | Ödülsüz | 9291 / 7545 | 572 / 514 | 175 · 225 · 108 · 64 | 96 (89 + 7) | 2430 | 0 | 3 | 0 | 0 | 0 |
| 6 | Bereketli Öğrenim | 7476 / 6464 | 693 / 530 | 200 · 325 · 120 · 48 | 87 (74 + 13) | 2170 | 0 | 4 | 0 | 0 | 0 |
| 6 | Davranışa Adanış | 9150 / 7446 | 928 / 773 | 456 · 304 · 72 · 96 | 98 (87 + 11) | 2430 | 0 | 2 | 0 | 0 | 0 |

## Tekrarlanabilirlik

İlk seed'in üç kolu ikinci kez çalıştırıldı: 6 round satırının 6'i bütün sütunlarıyla aynı.
