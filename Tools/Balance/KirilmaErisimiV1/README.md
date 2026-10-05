# Kırılma Erişimi V1 veri sahipliği (Bölüm 3.7.5)

Tek düzenleme kaynağı `kirilma_erisimi_v1_params.py` dosyasıdır. Asset'ler `make_kirilma_erisimi_v1.py` çıktısıdır.

Üreticinin yazdığı dosyalar:

- `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/ArtciPatlama_E1.asset` — Artçı Patlama'nın bu profile ait varyantı
- `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/CifteAkim_E1.asset` — Çifte Akım'ın varyantı (yalnız havuzda kalıyorsa)
- `Assets/ScriptableObjects/BossRewards/KirilmaErisimiV1/BossRewardPool_KirilmaErisimiV1.asset` — aşamalı havuz
- `Assets/ScriptableObjects/RunProfiles/Run50_KirilmaErisimiV1.asset` — profil

Bu profile ait olan veri (yalnız burada düzenlenir):

| Ne | Parametre |
|---|---|
| Artçı Patlama'nın ikinci darbesinin yarıçapı (hücre) | `ARTCI['radius_cells']` |
| Çifte Akım'ın ikinci dalgasının çapraz erişimi (hücre) | `CIFTE['reach_cells']` |
| Çifte Akım bu profilin havuzunda mı | `CIFTE['in_pool']` |

Artçının yarıçapı ödül verisinde ilk patlamanın yarıçapına **oran** olarak durur (`echoRadius`). Üretici hücre değerini orana
çevirir: oran = hücre ÷ 1,2 (ilk patlamanın yarıçapı, `HarvestBehaviorGeometry.ExplosionRadiusCells`). 2,00 hücre → ×1,667;
2,25 hücre → ×1,875; eski değer 1,50 hücre → ×1,25.

Varyant, Kırılma V1'deki ödül asset'inin kopyasıdır ve **yalnız erişim alanı** değişir. Hasar oranı, gecikme, tetik koşulu, ad ve
açıklama taban asset'ten aynen gelir; burada değiştirilemez. Eski asset'lere ve eski havuzlara dokunulmaz.

Başka yerden gelenler (kopyalanmaz):

| Ne | Kaynak |
|---|---|
| Varyantların diğer bütün değerleri | `BossRewards/KirilmaV1/ArtciPatlama_K1.asset`, `CifteAkim_K1.asset` |
| Hasat Ritmi ve diğer ödüller | DengeV1 / Kırılma V1 asset'leri |
| İki bedelli ödül | `../BedelliOdullerV1/bedelli_oduller_v1_params.py` (aynı asset'ler) |
| Aşamalar, ödül dağılımı, teklif politikası | `../OdulAsamalariV1/odul_asamalari_v1_params.py` |
| Takvim, kota ve boss hedefleri | `../TakvimV1/takvim_v1_params.py` |
| Denge seti (XP, ekonomi, bitki canı, ağaç), boss havuzu, round süresi | Kırılma V1 / DengeV1 |

## Komutlar

- `python make_kirilma_erisimi_v1.py --check` — çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_kirilma_erisimi_v1.py --report` — erişim tablosunu (Markdown) yazdırır; dosya yazmaz.
- `python make_kirilma_erisimi_v1.py` — önce kendi manifest'ini, sonra DengeV1, Kırılma V1 ve Bedelli Ödüller V1 manifest'lerini
  kontrol eder, ardından üretir.
- `python analyze_lab.py <klasör>` — laboratuvar CSV'lerini okur, kolları karşılaştırır ve ön kayıtlı karar kuralını uygular.
- `python analyze_validation.py <csv> <ödül> [<csv> <ödül> …] [--out dosya.md]` — birikimli run doğrulamasını (aday · mevcut ·
  almayan eş) hedef ödülün alındığı round'dan sonrası için karşılaştırır.

## Inspector'da değişiklik

Varyant, havuz ya da profil Inspector'dan düzenlenirse üretici bir sonraki çalıştırmada yazmadan durur ve değişen dosyayı
söyler. Değişiklik kalıcı olacaksa önce parametre dosyasına taşınır, sonra üretici çalıştırılır. Manifest körlemesine
güncellenmez.

## Veri denetimi

Üretici veriyi düzeltmez; şu durumlarda yazmadan durur:

- Parametredeki taban yarıçap ya da taban erişim, oyun kodundaki sabitle aynı değilse.
- Artçı yarıçapı ilk patlamanınkinden küçükse; Çifte Akım erişimi taban erişimden küçük ya da 6 hücreden büyükse.
- Taban ödül, Ödül Aşamaları V1'in havuzunda yoksa.
