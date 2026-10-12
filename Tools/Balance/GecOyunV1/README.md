# Geç Oyun V1 veri sahipliği (Bölüm 3.8)

Tek düzenleme kaynağı `gec_oyun_v1_params.py` dosyasıdır. Asset'ler `make_gec_oyun_v1.py` çıktısıdır.
Bu dosyadaki bütün sayılar **ilk prototip değeridir**: muhafazakâr ve sonlu seçildi, ölçülmedi, dengelenmedi.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset` — profil (Alpha Denge V1 profili; ödül havuzu, tarla olayı planı ve adlar farklı)
- `Assets/ScriptableObjects/BossRewards/GecOyunV1/BossRewardPool_GecOyunV1.asset` — havuz (Alpha havuzu + 13 yeni ödül, aşamalarının sonunda)
- `Assets/ScriptableObjects/BossRewards/GecOyunV1/<Ad>_G1.asset` — 13 yeni ödül (kart metni, ön koşul, koşul, etki bağlantısı)
- `Assets/ScriptableObjects/RunEffects/GecOyunV1/<Ad>Effect_G1.asset` — 13 ödülün çalışma zamanı etkisi (kural sınıfı + sayılar)
- `Assets/ScriptableObjects/FieldEvents/GecOyunV1/FieldEventPlan_GecOyunV1.asset` — tarla olayı planı (takvim, süreler, alanlar)

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre |
|---|---|
| Yeni ödüller: ad, kart etiketi ve açıklaması, aşama, ön koşul, koşul | `REWARDS` |
| Her ödülün etki sınıfı ve sayıları (hasar oranı, süre, alan, bekleme, sınır) | `REWARDS[…]['effect']`, `['fields']` |
| Tarla olayı takvimi (ilk round, boss / normal round listeleri, olasılık, sabitlenmiş round'lar) | `FIELD_EVENTS` |
| Dört olayın süreleri, alanları, dayanıklılıkları | `FIELD_EVENTS['thornSiege']` … `['wanderingBounty']` |
| Profil adı, zafer başlığı | `DISPLAY_NAME`, `VICTORY_TITLE` |

Başka yerden gelenler (kopyalanmaz, burada değiştirilemez):

| Ne | Kaynak |
|---|---|
| Takvim (15 boss), süre tablosu, kota ve boss hedefleri, level başına 3 seçim, başlangıç ekonomisi, boss havuzu | Alpha Denge V1 profili (asset metni kopyalanır) |
| Denge seti: can eğrisi, XP tablosu, ağaç ve fiyatlar, temel güç kartları, kasırga / bumerang sınırı (12) | Alpha Denge V1 denge seti (aynı asset'e referans) |
| Mevcut 19 ödül: asset'leri, aşamaları, sıraları, notları, ağırlıkları, stack sınırları, dışlama grupları | Alpha Denge V1 havuzu (aynı asset'lere referans; havuz metni kopyalanır) |

Kod: ödül → `BossRewardSO.effect` (`RunEffectSO`) → `RunEffects`; olay planı → `RunProfileSO.fieldEvents` → `FieldEventDirector`.
Görsel / ses kataloğu (`Assets/Resources/LateGameArt.asset`) üretilmez ve manifest dışındadır: Inspector'dan düzenlenir.

## Komutlar

- `python make_gec_oyun_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_gec_oyun_v1.py` — önce kendi manifest'ini, sonra kopyalanan Alpha asset'lerinin manifest'ini kontrol eder, ardından
  üretir. Listeden çıkarılan bir ödülün eski çıktısını siler.

## Inspector'da değişiklik

Üretilen asset Inspector'da değiştirilirse sonraki üretim durur (manifest farkı). Değişiklik buraya taşınmalı ya da asset geri
alınmalı.
