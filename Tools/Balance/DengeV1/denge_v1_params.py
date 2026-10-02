# Run50_DengeV1 denge sayıları (Bölüm 3.5). make_denge_v1.py bu dosyadan asset üretir.
# Ortak (eski profil) asset'leri buradan etkilenmez.
PLAYER, PLANTER, ALL, GRID, MUTATION = 1, 2, 0, 4, 5
FLAT, ADD, MORE, SET = 0, 1, 2, 3

# ---------------------------------------------------------------- temel
START_GOLD = 80
ROUND_SECONDS = 45            # profil süreyi sabitler: süre düğümleri ağaçtan çıktı, tempo yok
CORE = {
    'AreaRadius': 0.9,        # surface-contact geometry: compact start, then area families and boss rewards grow it
    'HarvestDamage': 10,      # eski 1 (ölçek ×10: yüzde ve düz artışlar tam sayıya yuvarlanırken kaybolmasın)
}

# ---------------------------------------------------------------- can
# Common canı (round, can). Aradaki round'lar geometrik ara değer (PlantHealthScalingSO). Can = Common × (1 + k × nadirlik basamağı).
HP_ANCHORS = [(1, 20), (5, 25), (10, 31), (15, 39), (20, 49), (25, 62), (30, 79), (35, 102), (40, 132), (45, 170), (50, 215)]
HP_RARITY_K = 0.5
# Bitki XP'si can çarpanıyla aynı oranda: sert bitki XP açısından zarar değil.
RARITY_XP = [1 + HP_RARITY_K * n for n in range(5)]

# ---------------------------------------------------------------- XP
XP_LEVELS = 140
XP_A, XP_B, XP_C, XP_D = 380.0, 10.0, 1.2, 0.045
def xp_requirement(level):
    return XP_A + XP_B * level + XP_C * level * level + XP_D * level ** 3

# ---------------------------------------------------------------- ekonomi
# Bütün saksıların taban üretim aralığı (sn). Eski değer 5: üretim hep fazlaydı, üretim yatırımı ve Don Cephesi karşılıksız kalıyordu.
PLANTER_BASE = {'PlantSpawnRate': 8}
PLANTER_PRICES = {}           # örn. {'GrassPlanter 2x3': (20, 'S')}; boş: asset fiyatları
COST_SCALE = {'G': 1.0, 'I': 1.0, 'S': 1.0}
COST_GROWTH = 0.3             # aile içinde kademe fiyatı artışı (doğrusal ağırlık)

def fam4(name): return ['%s - %d' % (name, i) for i in range(1, 5)]

FAMILIES = [
    # --- hasar
    dict(family='Keskin Başlangıç', nodes=fam4('Keskin Başlangıç'), effects=[('HarvestDamage', PLAYER, FLAT, 6)], cost='100G', window=(1, 10), prereq=[]),
    dict(family='Kesim Tekniği', nodes=fam4('Kesim Tekniği'), effects=[('HarvestDamage', PLAYER, ADD, 0.60)], cost='500G', window=(8, 22),
         prereq=[('Keskin Başlangıç - 1', 3)]),
    dict(family='Güçlü Kesim', nodes=fam4('Güçlü Kesim'), effects=[('HarvestDamage', PLAYER, FLAT, 20)], cost='2400G', window=(15, 32),
         prereq=[('Keskin Başlangıç - 1', 3)]),
    dict(family='Kesim Ustalığı', nodes=fam4('Kesim Ustalığı'), effects=[('HarvestDamage', PLAYER, ADD, 0.80)], cost='11000I', window=(25, 45),
         prereq=[('Kesim Tekniği - 1', 3), ('Güçlü Kesim - 1', 3)]),
    dict(family='Ağır Kesim', nodes=fam4('Ağır Kesim'), effects=[('HarvestDamage', PLAYER, FLAT, 30)], cost='24000G', window=(30, 48),
         prereq=[('Güçlü Kesim - 1', 3)]),
    dict(family='Kritik Odak', nodes=fam4('Kritik Odak'), effects=[('CritChance', PLAYER, FLAT, 0.15)], cost='3000G', window=(18, 35),
         prereq=[('Kesim Tekniği - 1', 3)]),
    dict(family='Kritik Güç', nodes=fam4('Kritik Güç'), effects=[('CritMultiplier', PLAYER, FLAT, 1.0)], cost='3000S', window=(32, 48),
         prereq=[('Kritik Odak - 1', 3)]),
    # --- hız ve alan
    dict(family='Hızlı Eller', nodes=fam4('Hızlı Eller'), effects=[('AttackSpeed', PLAYER, MORE, 0.72)], cost='130G', window=(1, 12), prereq=[]),
    dict(family='Akıcı Kesim', nodes=fam4('Akıcı Kesim'), effects=[('AttackSpeed', PLAYER, MORE, 0.75)], cost='3200G', window=(15, 32),
         prereq=[('Hızlı Eller - 1', 3)]),
    dict(family='Geniş Süpürüş', nodes=fam4('Geniş Süpürüş'), effects=[('AreaRadius', PLAYER, ADD, 0.35)], cost='420G', window=(8, 22),
         prereq=[('Hızlı Eller - 1', 3)]),
    dict(family='Geniş Tarama', nodes=fam4('Geniş Tarama'), effects=[('AreaRadius', PLAYER, ADD, 0.50)], cost='3200G', window=(20, 36),
         prereq=[('Geniş Süpürüş - 1', 3)]),
    # --- üretim ve nadirlik
    dict(family='Düzenli Üretim', nodes=fam4('Düzenli Üretim'), effects=[('PlantSpawnRate', PLANTER, MORE, 0.80)], cost='120G', window=(3, 18), prereq=[]),
    dict(family='Verimli Üretim', nodes=fam4('Verimli Üretim'), effects=[('PlantSpawnRate', PLANTER, MORE, 0.80)], cost='4000I', window=(22, 40),
         prereq=[('Düzenli Üretim - 1', 3)]),
    dict(family='Nadir Filizler', nodes=fam4('Nadir Filizler'), effects=[('RareSpawnChance', PLANTER, FLAT, 12)], cost='4500I', window=(20, 42),
         prereq=[('Verimli Üretim - 1', 1)]),
    # --- ekonomi ve skor
    dict(family='Altın Hasat I', nodes=fam4('Altın Hasat I'), effects=[('GoldGainMultiplier', PLANTER, ADD, 0.50)], cost='140G', window=(1, 12), prereq=[]),
    dict(family='Demir Hasat', nodes=fam4('Demir Hasat'), effects=[('IronGainMultiplier', PLANTER, ADD, 1.00)], cost='500G', window=(10, 28),
         prereq=[('Altın Hasat I - 1', 3)]),
    dict(family='Taş Hasat', nodes=fam4('Taş Hasat'), effects=[('StoneGainMultiplier', PLANTER, ADD, 1.00)], cost='700I', window=(18, 35),
         prereq=[('Demir Hasat - 1', 3)]),
    dict(family='Altın Hasat II', nodes=fam4('Altın Hasat II'), effects=[('GoldGainMultiplier', PLANTER, ADD, 1.00)], cost='3600G', window=(20, 36),
         prereq=[('Demir Hasat - 1', 3)]),
    dict(family='Zengin Cevher', nodes=fam4('Zengin Cevher'), effects=[('IronGainMultiplier', PLANTER, ADD, 1.00), ('StoneGainMultiplier', PLANTER, ADD, 1.00)],
         cost='1000S', window=(28, 45), prereq=[('Taş Hasat - 1', 3)]),
    dict(family='Hasat Rekoru', nodes=fam4('Hasat Rekoru'), effects=[('HarvestScoreMultiplier', PLANTER, ADD, 1.00)], cost='3600G', window=(15, 40),
         prereq=[('Altın Hasat I - 1', 3)],
         pos={'Hasat Rekoru - 1': (5, -7), 'Hasat Rekoru - 2': (6, -7), 'Hasat Rekoru - 3': (7, -8), 'Hasat Rekoru - 4': (8, -8)}),
    # --- XP ve kart
    dict(family='Hasat Deneyimi I', nodes=fam4('Hasat Deneyimi I'), effects=[('XPGainMultiplier', PLANTER, ADD, 0.60)], cost='125G', window=(3, 16),
         prereq=[('Düzenli Üretim - 1', 1)]),
    dict(family='Hasat Deneyimi II', nodes=fam4('Hasat Deneyimi II'), effects=[('XPGainMultiplier', PLANTER, ADD, 0.60)], cost='9000G', window=(25, 42),
         prereq=[('Hasat Deneyimi I - 1', 3)]),
    dict(family='Kart Sezgisi', nodes=fam4('Kart Sezgisi'), effects=[('MutationLuck', MUTATION, FLAT, 0.20)], cost='14000G', window=(25, 45),
         prereq=[('İlk Vazgeçiş', 1)]),
]

SINGLES = [
    dict(name='Grid Genişleme I', tiers=[('30G', [('GridUnlockSize', GRID, SET, 5)]), ('30I', [('GridUnlockSize', GRID, SET, 7)])], prereq=[], window=(2, 15)),
    dict(name='Grid Genişleme II', tiers=[('100S', [('GridUnlockSize', GRID, SET, 9)]), ('1800S', [('GridUnlockSize', GRID, SET, 11)])],
         prereq=[('Grid Genişleme I', 2)], window=(22, 45)),
    dict(name='1×3 Saksı', tiers=[('30G', [])], prereq=[('Grid Genişleme I', 1)], window=(4, 8), unlock=6),
    dict(name='2×2 Saksı', tiers=[('80G', [])], prereq=[('1×3 Saksı', 1), ('Grid Genişleme I', 1)], window=(8, 14), unlock=1),
    dict(name='2×3 Saksı', tiers=[('200G', [])], prereq=[('2×2 Saksı', 1), ('Grid Genişleme I', 2)], window=(14, 22), unlock=2),
    dict(name='Patlayıcı Kartlar', tiers=[('15I', [])], prereq=[('1×3 Saksı', 1)], window=(6, 15), unlock=3),
    dict(name='Çifte Hasat Kartları', tiers=[('25I', [])], prereq=[('2×2 Saksı', 1)], window=(10, 20), unlock=4),
    dict(name='Tornado Kartları', tiers=[('30I', [])], prereq=[('Patlayıcı Kartlar', 1)], window=(10, 22), unlock=5),
    dict(name='Bumerang Orak Kartları', tiers=[('40I', [])], prereq=[('Tornado Kartları', 1)], window=(14, 28), unlock=7),
    dict(name='Çapraz Elektrik Kartları', tiers=[('50I', [])], prereq=[('Patlayıcı Kartlar', 1)], window=(12, 28), unlock=8),
    dict(name='İlk Vazgeçiş', tiers=[('150G', [('CardSkip', ALL, FLAT, 1)])], prereq=[('Geniş Süpürüş - 1', 3)], window=(10, 25)),
    dict(name='İkinci Vazgeçiş', tiers=[('800I', [('CardSkip', ALL, FLAT, 1)])], prereq=[('İlk Vazgeçiş', 1)], window=(22, 40)),
]

# ---------------------------------------------------------------- boss ödülleri
# mods: (stat, hedef, işlem, değer). direct / rare_direct / behavior: çarpan. min_round / max_round: sunulduğu boss round'ları.
REWARD_CHOICES = 3
REWARDS = [
    # hasar yolu
    dict(file='KeskinBicak_V1', id='keskin_bicak', name='Keskin Bıçak', label='Doğrudan vuruş hasarı',
         note='Yalnız kendi vuruşun. Davranış hasarı değişmez.', direct=1.25, max=3),
    dict(file='CanavarKesimi_V1', id='canavar_kesimi', name='Canavar Kesimi', label='Doğrudan vuruş hasarı',
         note='Yalnız kendi vuruşun. Round 25 boss\'undan sonra çıkar; bir kez alınır.', direct=1.6, max=1, min_round=25),
    dict(file='KritikGoz_V1', id='kritik_goz', name='Kritik Göz', label='Kritik şansı',
         note='Yalnız kendi vuruşun. Yüzde puanı olarak eklenir.', mods=[('CritChance', PLAYER, FLAT, 0.12)], max=3),
    dict(file='AgirDarbe_V1', id='agir_darbe', name='Ağır Darbe', label='Kritik hasar çarpanı',
         note='Yalnız kendi vuruşun kritiği.', mods=[('CritMultiplier', PLAYER, FLAT, 0.75)], max=2),
    dict(file='AltinHedef_V1', id='altin_hedef', name='Altın Hedef', label='Rare ve üstü bitkiye doğrudan hasar',
         note='Yalnız kendi vuruşun; Common ve Uncommon değişmez. Davranış hasarı değişmez.', rare_direct=1.5, rare_from=2, max=2),
    # hız ve alan yolu
    dict(file='HizliBilek_V1', id='hizli_bilek', name='Hızlı Bilek', label='Saldırı aralığı', inverse='saldırı sıklığı',
         note='Vuruşlar arası süre kısalır. Taban 0,10 sn.', mods=[('AttackSpeed', PLAYER, MORE, -0.15)], max=3),
    dict(file='FirtinaBilegi_V1', id='firtina_bilegi', name='Fırtına Bileği', label='Saldırı aralığı', inverse='saldırı sıklığı',
         note='Round 25 boss\'undan sonra çıkar; bir kez alınır. Taban 0,10 sn.', mods=[('AttackSpeed', PLAYER, MORE, -0.30)], max=1, min_round=25),
    dict(file='GenisSavurus_V1', id='genis_savurus', name='Geniş Savuruş', label='Vuruş yarıçapı',
         note='Yalnız kendi vuruşun. İmleç halkası büyür; davranış alanı değişmez.', mods=[('AreaRadius', PLAYER, MORE, 0.32)], max=2),
    # davranış yolu
    dict(file='Kivilcim_V1', id='kivilcim', name='Kıvılcım', label='Mevcut davranış şansları',
         note='Patlama · kasırga · bumerang · elektrik. Şansı olmayan saksıya şans vermez; en çok %100.',
         mods=[('ExplosionChance', PLANTER, MORE, 0.35), ('TornadoChance', PLANTER, MORE, 0.35), ('BoomerangChance', PLANTER, MORE, 0.35), ('ElectricChance', PLANTER, MORE, 0.35)],
         max=3, condition=1),
    dict(file='YikimGucu_V1', id='yikim_gucu', name='Yıkım Gücü', label='Davranış hasarı',
         note='Patlama · kasırga · bumerang · elektrik. Kendi vuruşun değişmez.', behavior=1.3, max=3, condition=2),
    # üretim, nadirlik, XP
    dict(file='BereketliToprak_V1', id='bereketli_toprak', name='Bereketli Toprak', label='Üretim aralığı', inverse='üretim sıklığı',
         note='Bütün saksılar. Yalnız tarlan boşalıyorsa çıkar (son round ortalama doluluk %65\'in altında). Taban 0,50 sn.',
         mods=[('PlantSpawnRate', PLANTER, MORE, -0.15)], max=3, condition=3, threshold=0.65),
    dict(file='NadirTohum_V1', id='nadir_tohum', name='Nadir Tohum', label='Nadirlik bonusu',
         note='Bütün saksılar. Yüzde puanı olarak eklenir; en çok 95 puan.', mods=[('RareSpawnChance', PLANTER, FLAT, 10)], max=3),
    dict(file='BilgiFilizi_V1', id='bilgi_filizi', name='Bilgi Filizi', label='Kazanılan XP',
         note='Hasattan gelen XP. Round 30 boss\'undan sonra çıkmaz.', mods=[('XPGainMultiplier', PLANTER, MORE, 0.25)], max=2, max_round=30),
]

# ---------------------------------------------------------------- bosslar ve hedefler
BOSSES = dict(
    don_multiplier=2.5, don_coverage=0.5, don_weight=1,
    shell_multiplier=1.5, shell_coverage=0.4, shell_weight=1,
    # Sis: sabit çarpan yerine "bir basamak" (FogSO.stepDown): yarıçap, en iyi nişanın hedef sayısı tam bir basamak düşecek kadar küçülür.
    # Eşik 1,24: 6 → 5 basamağı; daha küçük yarıçapta (5 hedef) Sis çıkmaz (merkez nişan 5 → 1 olurdu).
    fog_multiplier=0.85, fog_min_radius=1.01, fog_weight=1, fog_step_down=True,
    # Sunum: boss kimlik rengi (HUD, harita ve tarla işaretleri bundan türer). Denge değeri değildir. Sis gri-lila.
    fog_color=(0.78, 0.74, 0.92),
    fog_rule="Boss round'unda kendi vuruş alanın bir basamak daralır: en iyi nişanda daha az bitki vurursun. Davranışların alanı ve hasarı değişmez.",
)
# Sabit tablolar (oyuncu gücüyle ölçeklenmez). Ölçülen run'lara göre ayarlandı: Docs/Bolum3-5-Run50-DengeV1.md §11.
SEGMENT_TARGETS = [45, 110, 150, 220, 300, 500, 1200, 2800, 4500, 6000]
BOSS_TARGETS = [8, 16, 30, 45, 65, 100, 220, 500, 800]
