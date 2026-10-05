# Alpha Denge V1 veri sahipliği (Bölüm 3.7.8 — P8)

Tek düzenleme kaynağı `alpha_denge_v1_params.py` dosyasıdır. Asset'ler `make_alpha_denge_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/RunProfiles/Run50_AlphaDengeV1.asset` — profil (XP V1 profili; süre tablosu, kota ve boss hedefleri, denge seti farklı)
- `Assets/ScriptableObjects/Balance/AlphaDengeV1/RunBalance_AlphaDengeV1.asset` — denge seti (XP V1 seti; yalnız can eğrisi — ve varsa XP tablosu — farklı)
- `Assets/ScriptableObjects/Balance/AlphaDengeV1/PlantHealth_AlphaDengeV1.asset` — bitki can eğrisi (bu profilin kendi kopyası)
- `Assets/ScriptableObjects/Balance/AlphaDengeV1/Progression_AlphaDengeV1.asset` — **yalnız** `XP_TABLE_OVERRIDES` doluysa
- `Assets/ScriptableObjects/BossRewards/AlphaDengeV1/…` — **yalnız** bir ödülün değeri değiştiyse: değişen ödüllerin kopyası ve havuz kopyası

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre |
|---|---|
| Round süresi tablosu (ilk round, saniye) | `ROUND_DURATIONS` (toplam: `TOTAL_ACTIVE_SECONDS`) |
| Common bitki canı çapaları (round, can) | `HP_ANCHORS` (aradaki round'lar geometrik ara değer) |
| Nadirlik can çarpanları | `HP_RARITY_MULTIPLIERS` |
| Dönem kotaları (15 dönem) | `QUOTA_TARGETS` |
| Boss hasat hedefleri (15 boss) | `BOSS_TARGETS` |
| Zincir Hasat şans ve hasar çarpanları (ek nesil 1, 2) | `CHAIN_CHANCE`, `CHAIN_DAMAGE` (izinli aralık aynı dosyada) |
| Diğer ödüllerin sayısal alanları | `REWARD_OVERRIDES` |
| XP tablosunda seviye başına maliyet değişikliği | `XP_TABLE_OVERRIDES` |
| Profil adı, zafer başlığı | `DISPLAY_NAME`, `VICTORY_TITLE` |

Başka yerden gelenler (kopyalanmaz, burada değiştirilemez):

| Ne | Kaynak |
|---|---|
| 15 boss tarihi, level başına 3 seçim, boss havuzu, başlangıç ekonomisi | XP V1 profili (asset metni kopyalanır) |
| Temel statlar, ağaç ve fiyatlar, nadirlik XP çarpanları, erken davranış erişimi, toplanan XP kartı kuralı | XP V1 denge seti (asset metni kopyalanır) |
| XP tablosu ve tablo sonrası maliyet (`XP_TABLE_OVERRIDES` boşken) | XP V1 XP tablosu (aynı asset'e referans) |
| Ödül havuzunun içeriği, aşamalar, ağırlıklar; zincirin ek nesil sayısı, kök ve kare bütçesi | Zincir V1 havuzu (değer değişmediyse aynı asset'e referans) |

Round süresi kodu: `RunProfileSO.roundDurations` → `RoundDurations` (kurallar ve sınırlar: 30–120 sn) → `RoundManager`. Tablo
boşsa profil eski yoldan çalışır (sabit süre ya da süre yükseltmeleri).

## Komutlar

- `python make_alpha_denge_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_alpha_denge_v1.py` — önce kendi manifest'ini, sonra kopyalanan asset'lerin sahiplerinin manifest'lerini (XP V1, Denge V1,
  Zincir V1) kontrol eder, ardından üretir. Artık üretilmeyen eski çıktıları (ör. değeri geri alınan ödül kopyası) siler.
- `python analyze_alpha.py <mod> …` — ölçüm tabloları (eğri, dönemler, hedef kuralı, üstünlük penceresi, zincir laboratuvarı,
  süre etkisi, menü yükü). Kullanım dosyanın başında.
- `python make_chart.py <çıktı.svg> <runs.csv[,…]> …` — build gücü ↔ gereksinim grafiği (SVG).

## Inspector'da değişiklik

Üretilen asset Inspector'da değiştirilirse sonraki üretim durur (manifest farkı). Değişiklik buraya taşınmalı ya da asset geri
alınmalı.
