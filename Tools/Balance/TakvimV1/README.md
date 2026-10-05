# Takvim V1 veri sahipliği (Bölüm 3.7.2)

Esas düzenleme kaynağı `takvim_v1_params.py` dosyasıdır. Asset, `make_takvim_v1.py` çıktısıdır.

Bu üretici **yalnız bir dosya** yazar:

- `Assets/ScriptableObjects/RunProfiles/Run50_TakvimV1.asset` (ve `.meta`)

Bu profile ait olan veri:

- Boss takvimi: 15 tarih ve R50'nin "final" kaydı (`BOSS_ROUNDS`, `FINAL_ROUND`).
- Bu takvime aktarılmış 15 kota ve 15 boss hasadı hedefi (`QUOTA_TARGETS`, `BOSS_TARGETS`).

Başka her şey Kırılma V1'in asset'leridir; kopyalanmaz, profil onlara referans verir:

| Ne | Kaynak asset | Sayının yazıldığı yer |
|---|---|---|
| Denge seti (temel stat, can, XP, ağaç, başlangıç kilitleri) | `Balance/RunBalance_KirilmaV1.asset` | `../KirilmaV1`, `../DengeV1` |
| Ödül havuzu (13 küçük ödül + 3 kırılma ödülü) | `BossRewards/KirilmaV1/BossRewardPool_KirilmaV1.asset` | `../KirilmaV1`, `../DengeV1` |
| Boss havuzu (Don, Sert Kabuk, Sis) | `Bosses/BossPool_DengeV1.asset` | `../DengeV1` |
| Level başına seçim, başlangıç altını, round süresi | profil alanları | `kirilma_v1_params.py`, `denge_v1_params.py` (tek kaynak) |

Bu yüzden DengeV1 ya da Kırılma V1'de yapılan bir denge değişikliği Takvim V1'e de yansır. **Hedef tabloları yansımaz:** Takvim V1
kendi 15 değerini taşır. Kırılma V1'in eski tablosu sonradan değişirse üretici yalnız uyarı yazar, hedefleri kendiliğinden değiştirmez.

## Komutlar

- `python make_takvim_v1.py --check` — çıktının son kayıtlı hash'le aynı olduğunu doğrular; dosya yazmaz.
- `python make_takvim_v1.py --report` — aktarım tablosunu (Markdown) yazdırır; dosya yazmaz.
- `python make_takvim_v1.py` — önce kendi manifest'ini, sonra DengeV1 ve Kırılma V1 manifest'lerini kontrol eder, ardından üretir.
  Inspector'da değişmiş bir çıktı varsa yazmadan durur; farkı önce `takvim_v1_params.py` dosyasına taşı.

Üretici, takvim verisini oyundaki `RunCalendar.Validate` ile aynı kurallarla denetler (sıralı, benzersiz, 1–50 arasında, son tarih
50, final yalnız son tarih, her dönem için kota ve boss hedefi). Geçersiz veriyi düzeltmez, durur.

## Hedefler geçicidir

`QUOTA_TARGETS` ve `BOSS_TARGETS` dengelenmiş değildir: Kırılma V1'in 5 round'luk tablosunun yeni dönem sınırlarına aktarılmış
hâlidir (`transfer.py`). `TARGETS_ARE_TRANSFER = True` iken üretici tabloların bu kuralın sonucu olduğunu denetler. Hedefler elle
ayarlanacaksa bu işaret `False` yapılır ve değişiklik belgeye "ayarlandı" diye yazılır.

Aktarım kuralı:

- **Kota:** eski her 5 round'luk kota kendi round'larına eşit paylaştırılır; yeni dönemin kapsadığı round payları toplanır. Tam sayı
  hedefler kümülatif toplam yuvarlanıp farkı alınarak üretilir; R50 sonundaki toplam bütçe eski tabloyla aynıdır (15.825).
- **Boss hasadı:** eski (round, hedef) noktaları arasında doğrusal ara değer. R3 için R0 = 0 noktası kullanılır. Son eski noktadan
  (R45) sonraki tarihlerde (R46 ve R50) son iki eski noktanın eğimi devam ettirilir. En yakın tam sayıya yuvarlanır, en az 1.

## Ölçüm araçları

`../DengeV1` altındaki çözümleyiciler (`analyze_runs`, `analyze_boss`, `compare_groups`, `doc_measured`, `margins`,
`progress_table`) boss ve kota round'larını 5 round'luk segmentlere göre okur. Açık boss takvimli bir profilin ölçüm dosyası
verilirse `calendar_guard.py` sonuç üretmeden "Bu profil desteklenmiyor" der.
