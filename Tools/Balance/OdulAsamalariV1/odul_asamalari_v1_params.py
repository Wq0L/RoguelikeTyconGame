# Run50_OdulAsamalariV1 verisi (Bölüm 3.7.3). make_odul_asamalari_v1.py bu dosyadan aşamalı ödül havuzunu ve profili üretir.
# Bu profile ait olan: ödül aşamaları (round aralıkları), 16 mevcut ödülün aşamalara dağılımı ve teklif politikası.
# Ödüllerin etkileri, katsayıları, stack sınırları ve ağırlıkları DengeV1 / Kırılma V1 asset'lerindedir (kopyalanmaz, referans verilir).
# Takvim, kota ve boss hedefleri Takvim V1'in parametre dosyasından okunur (../TakvimV1/takvim_v1_params.py); burada yazılmaz.

# ---------------------------------------------------------------- aşamalar
# first_round: aşamanın ilk round'u. Son round, sıradaki aşamanın ilk round'undan bir öncekidir (güçlü aşama run sonuna kadar).
#   Erken R1–11 (boss R3, R6, R10) · Orta R12–21 (boss R13, R16, R20) · Güçlü R22–50 (ilk güçlü teklif R23).
# Aşama sınıfı tile / bitki nadirliği değildir. "Güçlü" bir ad: ödülün ölçümde güçlü çıktığı anlamına gelmez
# (Çifte Akım ve Artçı Patlama'nın bilinen zayıflığı P5'te açık).
# color: aşama etiketinin rengi (sunum). rewards: (kaynak klasör, asset adı).
STAGES = [
    dict(id='erken', name='ERKEN', first_round=1, color=(0.96, 0.84, 0.55), rewards=[
        ('DengeV1', 'KeskinBicak_V1'),
        ('DengeV1', 'HizliBilek_V1'),
        ('DengeV1', 'BereketliToprak_V1'),
        ('DengeV1', 'NadirTohum_V1'),
        ('DengeV1', 'BilgiFilizi_V1'),
    ]),
    dict(id='orta', name='ORTA', first_round=12, color=(0.55, 0.86, 0.90), rewards=[
        ('DengeV1', 'Kivilcim_V1'),
        ('DengeV1', 'AgirDarbe_V1'),
        ('DengeV1', 'KritikGoz_V1'),
        ('DengeV1', 'AltinHedef_V1'),
        ('DengeV1', 'YikimGucu_V1'),
    ]),
    dict(id='guclu', name='GÜÇLÜ', first_round=22, color=(1.0, 0.62, 0.55), rewards=[
        ('DengeV1', 'GenisSavurus_V1'),
        ('DengeV1', 'CanavarKesimi_V1'),
        ('DengeV1', 'FirtinaBilegi_V1'),
        ('KirilmaV1', 'ArtciPatlama_K1'),
        ('KirilmaV1', 'CifteAkim_K1'),
        ('KirilmaV1', 'HasatRitmi_K1'),
    ]),
]

# Sınırsız mod için veri alanı (P10 / P11). Bu pakette içerik yok; oyun bu aşamayı hiçbir round'da açmaz.
ENDLESS = dict(id='sinirsiz', name='SINIRSIZ', color=(0.80, 0.74, 0.96), rewards=[])

# ---------------------------------------------------------------- teklif politikası
# Mevcut aşamada uygun aday varsa bir slot o aşamadan (ödülün kendi ağırlığıyla) seçilir.
RESERVE_CURRENT_STAGE_SLOT = True
# Kalan slotlarda ödülün ağırlığı aşama uzaklığına göre çarpılır: mevcut aşama, bir önceki, iki önceki.
DISTANCE_WEIGHTS = [1, 0.6, 0.35]

# ---------------------------------------------------------------- kart açıklamaları
# Ödül asset'indeki açıklama eski profilin sunulma zamanını anlatıyorsa (ör. "Round 25 boss'undan sonra çıkar") bu havuzda
# o cümle olmadan yazılır. Sunulma zamanı kartta aşamadan üretilir. Burada olmayan ödül asset'teki açıklamayı kullanır.
NOTES = {
    'CanavarKesimi_V1': 'Yalnız kendi vuruşun. Bir kez alınır.',
    'FirtinaBilegi_V1': 'Bir kez alınır. Taban 0,10 sn.',
    'BilgiFilizi_V1': 'Hasattan gelen XP.',
}

DISPLAY_NAME = 'Run50 Ödül Aşamaları V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · ÖDÜL AŞAMALARI V1'
