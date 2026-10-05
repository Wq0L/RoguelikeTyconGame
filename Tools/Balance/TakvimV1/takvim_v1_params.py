# Run50_TakvimV1 verisi (Bölüm 3.7.2). make_takvim_v1.py bu dosyadan yalnız profil asset'ini üretir.
# Bu profile ait olan: boss takvimi ve o takvime aktarılmış kota / boss hasadı hedefleri.
# Denge seti, XP, ağaç, saksı verisi, level başına seçim, başlangıç bütçesi, round süresi, boss havuzu ve ödül havuzu
# Kırılma V1'in asset'leridir (kopyalanmaz, aynı asset'e referans verilir; sayıları KirilmaV1 / DengeV1 parametre dosyalarından okunur).

# ---------------------------------------------------------------- takvim
# Boss round'ları. Her tarih bir kota dönemini kapatır: 1–3, 4–6, 7–10, 11–13, 14–16, 17–20, 21–23, 24–26, 27–30, 31–33,
# 34–36, 37–40, 41–43, 44–46, 47–50. Boss dönemin ilk round'unda duyurulur, yalnız son round'unda aktiftir.
RUN_LENGTH = 50
BOSS_ROUNDS = [3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50]
# Takvimde "final" diye işaretlenen tarih (ayrı kayıt). Şimdilik boss'u havuzdan gelir; canavar bitki finali ayrı pakettir.
FINAL_ROUND = 50

# ---------------------------------------------------------------- geçici hedefler (dengelenmiş DEĞİL)
# Aktarımın yapıldığı eski tablolar: Kırılma V1 (= DengeV1) 5 round'luk kotaları ve R5…R45 boss hasadı hedefleri.
SOURCE_SEGMENT_ROUNDS = 5
SOURCE_QUOTA = [45, 110, 150, 220, 300, 500, 1200, 2800, 4500, 6000]
SOURCE_BOSS = [8, 16, 30, 45, 65, 100, 220, 500, 800]

# Aktarım sonucu (transfer.py kuralı; üretici bu tabloların kuralla aynı olduğunu denetler).
# True: tablolar saf aktarımdır. Elle ayar yapılırsa False yapılır ve bu, belgede "ayarlandı" diye kaydedilir.
TARGETS_ARE_TRANSFER = True
QUOTA_TARGETS = [27, 40, 88, 90, 104, 176, 180, 220, 400, 720, 1040, 2240, 2700, 3000, 4800]
BOSS_TARGETS = [5, 10, 16, 24, 33, 45, 57, 72, 100, 172, 276, 500, 680, 860, 1100]

DISPLAY_NAME = 'Run50 Takvim V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · TAKVİM V1'
