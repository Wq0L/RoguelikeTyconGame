# Yankı ölçümünün tekrarlanabilirliği (Bölüm 3.7.5 teşhisi)

Aynı düzen, aynı seed, aynı statlar; her iş ayrı sahne yüklemesinde, sabit simülasyon adımıyla (1/30 sn).

| Düzen | Round | Kol | Tekrar | Aynı çıkan | Hasat (tekrar sırasıyla) | Yankının beklediği kare (en az – en çok) |
|---|---|---|---|---|---|---|
| PatlamaSinerji | R23 | odulsuz | 5 | 5 / 5 | 80 · 80 · 80 · 80 · 80 | – · – · – · – · – |
| PatlamaSinerji | R23 | artci_A | 5 | 5 / 5 | 103 · 103 · 103 · 103 · 103 | 6–6 · 6–6 · 6–6 · 6–6 · 6–6 |
| ElektrikSinerji | R23 | cifte_A | 5 | 5 / 5 | 70 · 70 · 70 · 70 · 70 | 5–5 · 5–5 · 5–5 · 5–5 · 5–5 |
| Karma | R40 | artci_A | 3 | 3 / 3 | 175 · 175 · 175 | 6–6 · 6–6 · 6–6 |

