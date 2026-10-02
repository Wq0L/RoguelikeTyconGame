# Kırılma V1 veri sahipliği (Bölüm 3.6)

Esas düzenleme kaynağı `kirilma_v1_params.py` dosyasıdır. Asset'ler `make_kirilma_v1.py` çıktısıdır.

Bu üretici yalnız Kırılma V1'e özgü dosyaları yazar:

- `Assets/ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset`
- `Assets/ScriptableObjects/Balance/RunBalance_KirilmaV1.asset`
- `Assets/ScriptableObjects/BossRewards/KirilmaV1/` (üç kırılma ödülü ve ödül havuzu)

Round süresi, kota ve boss hedefleri, başlangıç bütçesi, temel statlar, bitki canı, XP tablosu, skill tree, boss'lar ve 13 küçük ödül
DengeV1'in asset'leridir. Kopyalanmaz; yeni profil ve yeni denge seti onlara referans verir. Sayıları DengeV1 parametre dosyasından
(`../DengeV1/denge_v1_params.py`) okunur. Bu yüzden DengeV1'de yapılan bir değişiklik Kırılma V1'e de yansır; Kırılma V1'de yapılan
değişiklik DengeV1'e dokunmaz.

- `python make_kirilma_v1.py --check` mevcut çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_kirilma_v1.py` önce kendi manifest'ini, sonra DengeV1 manifest'ini kontrol eder, ardından üretir. Inspector'da
  değişmiş, silinmiş veya çıktı dizinine eklenmiş dosya varsa yazmadan durur.
- Inspector denemesi kalıcı tutulacaksa farkı önce `kirilma_v1_params.py` dosyasına taşı. Manifest'i körlemesine güncelleme.
- Koruma kodu DengeV1 ile ortaktır: `../DengeV1/generation_guard.py` (regresyon: `python ../DengeV1/test_generation_guard.py`).

Ayar yetkisi olan sayılar (Bölüm 3.6, madde 8): artçı hasarı %50–%100, artçı yarıçapı ×1,15–×1,40, ikinci dalga hasarı %60–%100,
Hasat Ritmi eşiği 5–8, hasarı ×1,25–×1,75, yarıçapı ×1,35–×1,60. XP ayarı bu pakette yoktur.
