# Yankı ölçümünün tekrarlanabilirliği (Bölüm 3.7.5 teşhisi)

Aynı düzen, aynı seed, aynı statlar; her iş ayrı sahne yüklemesinde, sabit simülasyon adımıyla (1/30 sn).

| Düzen | Round | Kol | Tekrar | Aynı çıkan | Hasat (tekrar sırasıyla) | Yankının beklediği kare (en az – en çok) |
|---|---|---|---|---|---|---|
| PatlamaSinerji | R23 | odulsuz | 5 | 5 / 5 | 80 · 80 · 80 · 80 · 80 | – · – · – · – · – |
| PatlamaSinerji | R23 | artci_A | 5 | 1 / 5 | 103 · 103 · 103 · 103 · 103 | 6–7 · 7–7 · 6–6 · 6–6 · 6–6 |
| ElektrikSinerji | R23 | cifte_A | 5 | 5 / 5 | 70 · 70 · 70 · 70 · 70 | 5–5 · 5–5 · 5–5 · 5–5 · 5–5 |
| Karma | R40 | artci_A | 3 | 3 / 3 | 175 · 175 · 175 | 6–7 · 6–7 · 6–7 |

### PatlamaSinerji R23 artci_A: tekrar 0 ile tekrar 1

Başlık: `# PatlamaSinerji R23 seed 1 artci_A tekrar 0 · Time.time 225.041 · kare 16361` / `# PatlamaSinerji R23 seed 1 artci_A tekrar 1 · Time.time 270.047119 · kare 17870`

İlk ayrışan iz satırı: 23 (öncesindeki 22 satır aynı)

```
   [0] 121 hasar Explosion 27 (8,3) yasam 1 can 58
   [1] 121 hasar Explosion 27 (8,3) yasam 1 can 58
   [0] 121 yanki plan Explosion is 2 gecikme 0.2 kare 0 bekleyen 2 zar 0d3c48bf
   [1] 121 yanki plan Explosion is 2 gecikme 0.2 kare 0 bekleyen 2 zar 0d3c48bf
   [0] 121 hasar Direct 29 (7,2) yasam 1 can 57
   [1] 121 hasar Direct 29 (7,2) yasam 1 can 57
   [0] 121 hasar Direct 29 (7,3) yasam 1 can 113
   [1] 121 hasar Direct 29 (7,3) yasam 1 can 113
>> [0] 127 hasar Explosion yanki 27 (8,2) yasam 1 can 88
>> [1] 128 hasar Explosion yanki 27 (8,2) yasam 1 can 88
   [0] 127 hasar Explosion yanki 27 (8,3) yasam 1 can 31
   [1] 128 hasar Explosion yanki 27 (8,3) yasam 1 can 31
   [0] 127 hasar Explosion yanki 27 (8,4) yasam 1 can 57
   [1] 128 hasar Explosion yanki 27 (8,4) yasam 1 can 57
```

### PatlamaSinerji R23 artci_A: tekrar 0 ile tekrar 2

Başlık: `# PatlamaSinerji R23 seed 1 artci_A tekrar 0 · Time.time 225.041 · kare 16361` / `# PatlamaSinerji R23 seed 1 artci_A tekrar 2 · Time.time 315.0523 · kare 19380`

İlk ayrışan iz satırı: 374 (öncesindeki 373 satır aynı)

```
   [0] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [2] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [0] 1021 hasat Direct (7,3) yasam 4
   [2] 1021 hasat Direct (7,3) yasam 4
   [0] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [2] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [0] 1021 hasar Direct 28 (7,5) yasam 1 can 29
   [2] 1021 hasar Direct 28 (7,5) yasam 1 can 29
>> [0] 1028 hasar Explosion yanki 27 (5,3) yasam 2 can 85
>> [2] 1027 hasar Explosion yanki 27 (5,3) yasam 2 can 85
   [0] 1028 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [2] 1027 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [0] 1028 hasar Explosion yanki 27 (7,6) yasam 2 can 3
   [2] 1027 hasar Explosion yanki 27 (7,6) yasam 2 can 3
```

### PatlamaSinerji R23 artci_A: tekrar 0 ile tekrar 3

Başlık: `# PatlamaSinerji R23 seed 1 artci_A tekrar 0 · Time.time 225.041 · kare 16361` / `# PatlamaSinerji R23 seed 1 artci_A tekrar 3 · Time.time 360.057678 · kare 20892`

İlk ayrışan iz satırı: 374 (öncesindeki 373 satır aynı)

```
   [0] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [3] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [0] 1021 hasat Direct (7,3) yasam 4
   [3] 1021 hasat Direct (7,3) yasam 4
   [0] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [3] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [0] 1021 hasar Direct 28 (7,5) yasam 1 can 29
   [3] 1021 hasar Direct 28 (7,5) yasam 1 can 29
>> [0] 1028 hasar Explosion yanki 27 (5,3) yasam 2 can 85
>> [3] 1027 hasar Explosion yanki 27 (5,3) yasam 2 can 85
   [0] 1028 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [3] 1027 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [0] 1028 hasar Explosion yanki 27 (7,6) yasam 2 can 3
   [3] 1027 hasar Explosion yanki 27 (7,6) yasam 2 can 3
```

### PatlamaSinerji R23 artci_A: tekrar 0 ile tekrar 4

Başlık: `# PatlamaSinerji R23 seed 1 artci_A tekrar 0 · Time.time 225.041 · kare 16361` / `# PatlamaSinerji R23 seed 1 artci_A tekrar 4 · Time.time 405.0628 · kare 22401`

İlk ayrışan iz satırı: 374 (öncesindeki 373 satır aynı)

```
   [0] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [4] 1021 hasar Direct 27 (7,3) yasam 4 can 3
   [0] 1021 hasat Direct (7,3) yasam 4
   [4] 1021 hasat Direct (7,3) yasam 4
   [0] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [4] 1021 hasar Direct 30 (7,4) yasam 2 can 85
   [0] 1021 hasar Direct 28 (7,5) yasam 1 can 29
   [4] 1021 hasar Direct 28 (7,5) yasam 1 can 29
>> [0] 1028 hasar Explosion yanki 27 (5,3) yasam 2 can 85
>> [4] 1027 hasar Explosion yanki 27 (5,3) yasam 2 can 85
   [0] 1028 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [4] 1027 hasar Explosion yanki 27 (6,6) yasam 2 can 31
   [0] 1028 hasar Explosion yanki 27 (7,6) yasam 2 can 3
   [4] 1027 hasar Explosion yanki 27 (7,6) yasam 2 can 3
```

### Karma R40 artci_A: tekrar 0 ile tekrar 1

Başlık: `# Karma R40 seed 2 artci_A tekrar 0 · Time.time 675.0958 · kare 31436` / `# Karma R40 seed 2 artci_A tekrar 1 · Time.time 720.1007 · kare 32939`

İlk ayrışan iz satırı: 268 (öncesindeki 267 satır aynı)

```
   [0] 538 hasar Explosion 75 (7,6) yasam 1 can 198
   [1] 538 hasar Explosion 75 (7,6) yasam 1 can 198
   [0] 538 yanki plan Explosion is 5 gecikme 0.2 kare 0 bekleyen 1 zar 10409e77
   [1] 538 yanki plan Explosion is 5 gecikme 0.2 kare 0 bekleyen 1 zar 10409e77
   [0] 538 hasar Direct 86 (6,7) yasam 2 can 123
   [1] 538 hasar Direct 86 (6,7) yasam 2 can 123
   [0] 538 hasar Direct 75 (6,8) yasam 2 can 123
   [1] 538 hasar Direct 75 (6,8) yasam 2 can 123
>> [0] 545 hasar Explosion yanki 75 (4,4) yasam 2 can 57
>> [1] 544 hasar Explosion yanki 75 (4,4) yasam 2 can 57
   [0] 545 hasat Explosion yanki (4,4) yasam 2
   [1] 544 hasat Explosion yanki (4,4) yasam 2
   [0] 545 hasar Explosion yanki 75 (4,7) yasam 2 can 44
   [1] 544 hasar Explosion yanki 75 (4,7) yasam 2 can 44
```

### Karma R40 artci_A: tekrar 0 ile tekrar 2

Başlık: `# Karma R40 seed 2 artci_A tekrar 0 · Time.time 675.0958 · kare 31436` / `# Karma R40 seed 2 artci_A tekrar 2 · Time.time 765.1039 · kare 34436`

İlk ayrışan iz satırı: 42 (öncesindeki 41 satır aynı)

```
   [0] 116 yanki plan Explosion is 1 gecikme 0.2 kare 0 bekleyen 1 zar 8bc7cbed
   [2] 116 yanki plan Explosion is 1 gecikme 0.2 kare 0 bekleyen 1 zar 8bc7cbed
   [0] 116 hasar Direct 162 (5,7) yasam 1 can 123
   [2] 116 hasar Direct 162 (5,7) yasam 1 can 123
   [0] 116 hasat Direct (5,7) yasam 1
   [2] 116 hasat Direct (5,7) yasam 1
   [0] 116 hasar Direct 166 (5,8) yasam 1 can 198
   [2] 116 hasar Direct 166 (5,8) yasam 1 can 198
>> [0] 122 hasar Explosion yanki 75 (4,6) yasam 1 can 40
>> [2] 123 hasar Explosion yanki 75 (4,6) yasam 1 can 40
   [0] 122 hasat Explosion yanki (4,6) yasam 1
   [2] 123 hasat Explosion yanki (4,6) yasam 1
   [0] 122 hasar Explosion yanki 75 (5,4) yasam 2 can 198
   [2] 123 hasar Explosion yanki 75 (5,4) yasam 2 can 198
```

