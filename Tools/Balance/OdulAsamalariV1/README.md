# Ödül Aşamaları V1 veri sahipliği (Bölüm 3.7.3)

Tek düzenleme kaynağı `odul_asamalari_v1_params.py` dosyasıdır. Asset'ler `make_odul_asamalari_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/BossRewards/OdulAsamalariV1/BossRewardPool_OdulAsamalariV1.asset` — aşamalı ödül havuzu
- `Assets/ScriptableObjects/RunProfiles/Run50_OdulAsamalariV1.asset` — profil

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre |
|---|---|
| Aşamalar ve ilk round'ları (erken 1, orta 12, güçlü 22) | `STAGES` |
| 16 ödülün aşamalara dağılımı | `STAGES[...]['rewards']` |
| Mevcut aşamaya slot ayırma | `RESERVE_CURRENT_STAGE_SLOT` |
| Aşama uzaklığı ağırlık çarpanları (1 / 0,60 / 0,35) | `DISTANCE_WEIGHTS` |
| Kartta gösterilen açıklamalar (eski sunulma zamanı cümlesi olmadan) | `NOTES` |
| Sınırsız aşama veri alanı (içerik yok) | `ENDLESS` |
| Aşama etiketi renkleri (sunum) | `STAGES[...]['color']` |

Başka yerden gelenler (kopyalanmaz):

| Ne | Kaynak |
|---|---|
| Ödüllerin etkisi, katsayısı, stack sınırı, ağırlığı, uygunluk koşulu | `BossRewards/DengeV1/*.asset` (13 ödül), `BossRewards/KirilmaV1/*.asset` (3 ödül) |
| Takvim, kota ve boss hedefleri | `../TakvimV1/takvim_v1_params.py` |
| Denge seti, boss havuzu | Kırılma V1 / DengeV1 asset'leri |
| Level başına seçim, başlangıç altını, round süresi, kart sayısı | `kirilma_v1_params.py`, `denge_v1_params.py` |

Bu yüzden Takvim V1'in hedefleri ya da bir ödülün katsayısı değişirse bu profil de değişir. Aşama dağılımı ve teklif
politikası yalnız buradan değişir.

**Sunulma zamanı tek yerdedir:** aşamanın ilk round'u. Aşamalı havuzda ödül asset'lerinin `minRound` / `maxRound` alanları
okunmaz; o alanlar yalnız eski düz havuzlarda (DengeV1, Kırılma V1, Takvim V1, Boss Prototip) geçerlidir. Eski asset'ler
değiştirilmedi.

## Komutlar

- `python make_odul_asamalari_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_odul_asamalari_v1.py --report` — aşama tablosunu (Markdown) yazdırır; dosya yazmaz.
- `python make_odul_asamalari_v1.py` — önce kendi manifest'ini, sonra DengeV1 ve Kırılma V1 manifest'lerini kontrol eder, ardından üretir.

## Inspector'da değişiklik

Havuz ya da profil Inspector'dan düzenlenirse üretici bir sonraki çalıştırmada yazmadan durur ve değişen dosyayı söyler.
Değişiklik kalıcı olacaksa önce `odul_asamalari_v1_params.py` dosyasına taşınır, sonra üretici çalıştırılır. Manifest körlemesine
güncellenmez.

## Veri denetimi

Üretici, oyundaki `BossRewardPoolSO.Validate` ile aynı kuralları uygular ve veriyi düzeltmez:

- İlk aşama Round 1'de başlar; aşamalar sıralıdır ve run'ın içinde başlar.
- Bir ödül yalnız bir aşamada yazılır (sınırsız aşama dahil). Aynı kimliğin iki ayrı asset olarak yazılması da hatadır:
  aynı etki ayrı stack olarak çoğaltılamaz.
- Her aşama için bir uzaklık ağırlığı vardır; negatif olamaz.
- Kartta görünecek hiçbir açıklama bir round sayısı anmaz (sunulma zamanı aşamadan üretilir).
