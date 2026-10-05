# Zincir V1 veri sahipliği (Bölüm 3.7.6)

Tek düzenleme kaynağı `zincir_v1_params.py` dosyasıdır. Asset'ler `make_zincir_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/BossRewards/ZincirV1/ZincirHasat_Z1.asset` — Zincir Hasat ödülü
- `Assets/ScriptableObjects/BossRewards/ZincirV1/BossRewardPool_ZincirV1.asset` — aşamalı havuz (Kırılma Erişimi V1 havuzu + güçlü aşamada Zincir Hasat)
- `Assets/ScriptableObjects/RunProfiles/Run50_ZincirV1.asset` — profil (Kırılma Erişimi V1 profili; yalnız ad, zafer başlığı, havuz farklı)

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre | Değer |
|---|---|---|
| Ek nesil sayısı | `CHAIN['generations']` | 2 |
| Ek nesil başına şans çarpanı | `CHAIN['chance']` | ×0,75 · ×0,50 |
| Ek nesil başına hasar çarpanı | `CHAIN['damage']` | ×0,75 · ×0,50 |
| Kök (bir doğrudan saldırı) başına başarılı ek tetik | `CHAIN['root_budget']` | 32 |
| Zincir kuyruğundan kare başına iş | `CHAIN['jobs_per_frame']` | 8 |
| Aşama, ağırlık, en çok alış, koşul | `CHAIN['stage']`, `weight`, `max_stacks`, `condition` | güçlü (R23 boss'undan), 1, 1, davranışlı saksı |
| Kart metni (ad, etiket, not) | `CHAIN['display_name']`, `effect_label`, `note` | |

Değerler görevin verdiği ilk prototip sınırıdır; ölçüm sonucuna göre bu pakette değiştirilmedi.

Başka yerden gelenler (kopyalanmaz):

| Ne | Kaynak |
|---|---|
| Havuzun diğer bütün ödülleri (Artçı 2,00 hücre, Çifte Akım 4 hücre, Hasat Ritmi, bedelli ödüller …), aşamalar, teklif politikası | `../KirilmaErisimiV1/` (havuz asset'i kopyalanıp yalnız güçlü aşamaya bir satır eklenir) |
| Takvim, hedefler, denge seti (XP, ekonomi, bitki canı, ağaç), başlangıç seçenekleri, round süresi | Kırılma Erişimi V1 profili (asset metni kopyalanır) |

## Komutlar

- `python make_zincir_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_zincir_v1.py` — önce kendi manifest'ini ve Kırılma Erişimi V1 manifest'ini kontrol eder, ardından üretir.
- `python analyze_chain.py lab <klasör>` — zincir laboratuvarının (KirilmaErisim_zincir23/30/40.csv) tabloları.
- `python analyze_chain.py runs <csv>` — birikimli tam run'ın (BalanceRuns_k376zincir.csv) tabloları.

## Inspector'da değişiklik

Üretilen asset Inspector'da değiştirilirse sonraki üretim durur (manifest farkı). Değişiklik buraya taşınmalı ya da asset
geri alınmalı. Üretici, ek nesil sayısının koddaki üst sınırı (`HarvestChain.MaxGenerations`) aşmadığını da denetler.
