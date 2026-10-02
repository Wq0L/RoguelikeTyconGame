# Run50_KirilmaV1 sayıları (Bölüm 3.6). make_kirilma_v1.py bu dosyadan asset üretir.
# Round süresi, can eğrisi, kota ve boss hedefleri, başlangıç bütçesi, XP tablosu, ağaç ve fiyatlar, boss'lar ve 13 küçük ödül
# DengeV1'in asset'leridir (kopyalanmaz, aynı asset'e referans verilir). Bu dosya yalnız Kırılma V1'e özgü olanları taşır.
# DengeV1 asset'leri buradan değişmez.

# ---------------------------------------------------------------- level kartları
# Kazanılan her level bu kadar ayrı kart seçimi hakkı verir (her seçimde üç aday, biri alınır).
CHOICES_PER_LEVEL = 3

# ---------------------------------------------------------------- erken davranış erişimi
# UnlockType: TileBehavior_Explosive = 3, TileBehavior_Electric = 8. Run başında açık; bu iki kilidi açan ağaç düğümü satılmaz.
STARTING_UNLOCKS = [3, 8]
# TileModifierType: Explosive = 4, Electric = 9. Bu türlerden bir kart alınana kadar her seçimde bir slot bu adaylardan gelir.
FIRST_BEHAVIOR_OFFER = [4, 9]

# ---------------------------------------------------------------- kırılma ödülleri
# Her biri run boyunca en çok bir kez; ilk sunulabildiği boss R10. Koşul stat'ı: ExplosionChance = 20, ElectricChance = 25.
BREAKTHROUGH_MIN_ROUND = 10
BREAKTHROUGHS = [
    dict(file='ArtciPatlama_K1', id='artci_patlama', name='Artçı Patlama', label='Artçı patlama:',
         note='Şansı tutan her normal patlamadan sonra aynı yerde ikinci bir patlama. Artçı yeni artçı ya da zincir başlatmaz; '
              'öldürdükleri davranış hasadıdır.',
         echo=1, delay=0.20, damage=1.00, radius=1.25, condition_stat=20),
    dict(file='CifteAkim_K1', id='cifte_akim', name='Çifte Akım', label='İkinci elektrik dalgası:',
         note='Şansı tutan her normal elektrikten sonra aynı saksıdan ikinci bir dalga. Yeni şans atılmaz; ikinci dalga zincir başlatmaz.',
         echo=2, delay=0.15, damage=1.00, radius=1.0, condition_stat=25),
    dict(file='HasatRitmi_K1', id='hasat_ritmi', name='Hasat Ritmi', label='Ritim:',
         note='Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Hak hazır olunca sıradaki vuruşun güçlenir (imleç halkası büyür). '
              'Ek saldırı üretmez; davranışlara geçmez.',
         rhythm=5, rhythm_damage=1.75, rhythm_radius=1.6),
]
