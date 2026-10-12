# Diken ve Filiz geometri bütçesi

Unity üçgen ve vertex sayımı. Vertexler flat shading nedeniyle yüzler arasında yinelenir; bunlar benzersiz geometrik köşe sayısı değildir. Kapalı hasar aşamaları üst sınıra dahildir. Ink ve AttentionRim aynı mesh'i paylaşır, ek mesh belleği üretmez ancak ek çizim ve üçgen gönderimi getirir.

| Prefab | Gövde vertex | Gövde üçgen | Koyu kontur üçgen | Renkli kontur üçgen | Toplam üçgen | Renderer |
|---|---:|---:|---:|---:|---:|---:|
| GroundHeave | 510 | 170 | 170 | 0 | 340 | 2 |
| ThornBreak | 648 | 216 | 0 | 0 | 216 | 9 |
| ThornHit | 360 | 120 | 0 | 0 | 120 | 5 |
| ThornPiece | 270 | 90 | 90 | 90 | 270 | 3 |
| ThornRoot | 1254 | 418 | 310 | 310 | 1038 | 6 |
| ThornSoilBurst | 288 | 96 | 0 | 0 | 96 | 4 |
| SproutBreak | 192 | 64 | 0 | 0 | 64 | 8 |
| SproutHit | 96 | 32 | 0 | 0 | 32 | 4 |
| SproutMain | 498 | 166 | 130 | 130 | 426 | 18 |
| SproutPiece | 156 | 52 | 52 | 52 | 156 | 9 |

