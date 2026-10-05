# XP V1 veri sahipliği (Bölüm 3.7.7 — P7)

Tek düzenleme kaynağı `xp_v1_params.py` dosyasıdır. Asset'ler `make_xp_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/Balance/XPV1/Progression_XPV1.asset` — XP tablosu (Denge V1 tablosunun 140 maliyeti + tablo sonrası büyüme)
- `Assets/ScriptableObjects/Balance/XPV1/RunBalance_XPV1.asset` — denge seti (Kırılma V1 seti; yalnız XP tablosu ve temel güç kartı kuralları farklı)
- `Assets/ScriptableObjects/RunProfiles/Run50_XPV1.asset` — profil (Zincir V1 profili; yalnız ad, zafer başlığı, denge seti farklı)

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre | Değer |
|---|---|---|
| Tablo sonrası büyüme katsayısı `s` | `TAIL_GROWTH` | 0,05 |
| Temel güç "XP kazancı" kartları toplanır | `ADDITIVE_BASE_XP_CARDS` | açık |
| Tabandaki "Atak aralığı" temel güç kartı sunulmaz | `HIDE_FLOORED_BASE_STATS` | açık |
| Tablonun kendi içindeki maliyet değişikliği | `TABLE_OVERRIDES` | yok (tablo Denge V1 ile aynı) |
| Profil adı, zafer başlığı | `DISPLAY_NAME`, `VICTORY_TITLE` | |

Maliyet formülü: tablo içinde (level 1–140) maliyet tablodan okunur; sonrasında

    C(L) = C(N) × [1 + s × (L − N)],  L > N,  N = 140,  C(N) = 150 000 XP

`s = 0` eski davranıştır (son maliyet tekrar eder). Kod: `ProgressionSO.GetXPForLevel`, alan `tailGrowth`.

Birikim kuralı: `RunBalanceSO.additiveBaseXpCards` açıkken, tile ve yükseltmeler tükendikten sonra gelen "XP kazancı" temel güç
kartları `StatManager.AddToSummedGroup` ile tek bir çarpanda toplanır (iki +%5 → ×1,10). Kart değerleri (+%2 / +%3 / +%4 / +%5)
kodda (`CardSelectionUI.RollBaseStat`), eskisiyle aynıdır. Diğer temel güç kartları ve diğer XP kaynakları değişmez.

Başka yerden gelenler (kopyalanmaz, burada değiştirilemez):

| Ne | Kaynak |
|---|---|
| Takvim, kota ve boss hedefleri, round süresi, level başına 3 seçim, ödül havuzu (zincir dahil), başlangıç ekonomisi | Zincir V1 profili (asset metni kopyalanır) |
| Temel statlar, bitki canı, ağaç, nadirlik XP çarpanları, erken davranış teklifi, başlangıç kilitleri | Kırılma V1 denge seti (asset metni kopyalanır) |
| İlk 140 seviyenin maliyetleri | Denge V1 XP tablosu (asset metni kopyalanır) |

## Komutlar

- `python make_xp_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_xp_v1.py` — önce kendi manifest'ini, sonra kopyalanan üç asset'in sahiplerinin manifest'lerini (Denge V1, Kırılma V1,
  Zincir V1) kontrol eder, ardından üretir.
- `python analyze_xp.py <mod> …` — ölçüm tabloları (pilot, eş bütçeli A/B, erişim, menü yükü). Kullanım dosyanın başında.

## Inspector'da değişiklik

Üretilen asset Inspector'da değiştirilirse sonraki üretim durur (manifest farkı). Değişiklik buraya taşınmalı ya da asset geri
alınmalı.
