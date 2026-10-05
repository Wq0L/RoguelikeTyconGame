# Bedelli Ödüller V1 veri sahipliği (Bölüm 3.7.4)

Tek düzenleme kaynağı `bedelli_oduller_v1_params.py` dosyasıdır. Asset'ler `make_bedelli_oduller_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/BereketliOgrenim_B1.asset` — Bereketli Öğrenim
- `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/DavranisaAdanis_B1.asset` — Davranışa Adanış
- `Assets/ScriptableObjects/BossRewards/BedelliOdullerV1/BossRewardPool_BedelliOdullerV1.asset` — aşamalı havuz
- `Assets/ScriptableObjects/RunProfiles/Run50_BedelliOdullerV1.asset` — profil

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre |
|---|---|
| İki ödülün adı, açıklaması, kimliği | `REWARDS[...]['name' / 'note' / 'id']` |
| Level başına seçim hakkı değişimi (+1 / −1) | `REWARDS[...]['level_choices']` |
| Doğrudan vuruş ve davranış hasarı çarpanı (×0,80 / ×1,50) | `REWARDS[...]['direct' / 'behavior']` |
| En çok kaç kez alınır, teklif ağırlığı | `REWARDS[...]['max_stacks' / 'weight']` |
| Sunulma koşulu (davranışı olan saksı) | `REWARDS[...]['condition']` |
| Karşılıklı dışlama grubu | `REWARDS[...]['group']` |
| Yazıldığı aşama (ilk sunulduğu boss buradan çıkar) | `REWARDS[...]['stage']` |

**Bu sayılar ilk test değerleridir**, dengelenmiş nihai sayılar değil. Ölçüm sonucuna göre kendiliğinden değiştirilmez.

Başka yerden gelenler (kopyalanmaz):

| Ne | Kaynak |
|---|---|
| Aşamalar, mevcut 16 ödülün dağılımı, teklif politikası, eski kart açıklamaları | `../OdulAsamalariV1/odul_asamalari_v1_params.py` |
| Mevcut 16 ödülün etkisi | `BossRewards/DengeV1/*.asset`, `BossRewards/KirilmaV1/*.asset` |
| Takvim, kota ve boss hedefleri | `../TakvimV1/takvim_v1_params.py` |
| Denge seti (XP, ekonomi), boss havuzu | Kırılma V1 / DengeV1 asset'leri |
| Level başına temel seçim hakkı (3), başlangıç altını, round süresi | `kirilma_v1_params.py`, `denge_v1_params.py` |

Bu yüzden Ödül Aşamaları V1'in aşama dağılımı ya da Takvim V1'in hedefleri değişirse bu profil de değişir. İki bedelli ödül
yalnız buradan değişir.

## Komutlar

- `python make_bedelli_oduller_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_bedelli_oduller_v1.py --report` — iki ödülün tablosunu (Markdown) yazdırır; dosya yazmaz.
- `python make_bedelli_oduller_v1.py` — önce kendi manifest'ini, sonra DengeV1 ve Kırılma V1 manifest'lerini kontrol eder, ardından üretir.

## Inspector'da değişiklik

Ödül, havuz ya da profil Inspector'dan düzenlenirse üretici bir sonraki çalıştırmada yazmadan durur ve değişen dosyayı söyler.
Değişiklik kalıcı olacaksa önce `bedelli_oduller_v1_params.py` dosyasına taşınır, sonra üretici çalıştırılır. Manifest körlemesine
güncellenmez.

## Veri denetimi

Üretici veriyi düzeltmez; şu durumlarda yazmadan durur:

- Ödülün aşaması bilinmiyorsa, kimliği ya da dosya adı başka bir ödülle çakışıyorsa.
- Ödül tek başına bile seçim hakkını geçerli aralığın (1–5) dışına çıkarıyorsa: oyun böyle bir ödülü kırpmaz, hiç sunmaz.
- Ödülün hiçbir etkisi yoksa.
- Açıklama bir round sayısı anıyorsa (sunulma zamanı aşamadan üretilir).
- Bir dışlama grubunda tek ödül varsa.

Oyundaki aralık sabitleri `RunPower.MinLevelChoices` / `MaxLevelChoices` içindedir; `LEVEL_CHOICE_RANGE` onlarla aynı tutulur.
