# Run50_GecOyunV1 verisi (Bölüm 3.8 — geç oyun tarla olayları ve yeni boss ödülleri). make_gec_oyun_v1.py bu dosyadan profili,
# ödül havuzunu, yeni ödülleri, ödüllerin çalışma zamanı etkilerini ve tarla olayı planını üretir.
#
# BU DOSYADAKİ BÜTÜN SAYILAR İLK PROTOTİP DEĞERİDİR: muhafazakâr ve sonlu seçildi, ölçülmedi, dengelenmedi. Nihai tasarım kararı
# değildir. Her sayının anlamı ve kuralı: Docs/Bolum3-8-GecOyunOlaylariVeBossOdulleri.md (tetik ve stat sözleşmesi tablosu).
#
# Bu profile ait olanlar: yeni 12 ödül ve etkileri, ödüllerin havuzdaki aşaması ve ön koşulu, tarla olayı planı, profil adı.
# Run50_AlphaDengeV1'den AYNEN gelenler (burada yazılmaz, buradan değiştirilemez): takvim (15 boss), süre tablosu, kota ve boss
# hedefleri, denge seti (can eğrisi, XP, ağaç, fiyatlar, temel güç kartları, kasırga / bumerang sınırı 12), boss havuzu ve mevcut
# 19 ödülün tamamı (asset'leri, aşamaları, ağırlıkları, stack sınırları, dışlama grupları).

DISPLAY_NAME = 'Run50 Geç Oyun V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · GEÇ OYUN V1'

# ---------------------------------------------------------------- yeni boss ödülleri
# Koşul (BossRewardCondition): 0 = her zaman, 2 = davranışı olan saksı varsa (patlama / kasırga / bumerang / elektrik şansı > 0).
ALWAYS, ANY_BEHAVIOR_PLANTER = 0, 2

# Her ödül: kimlik, asset adı, görünen ad, kart etiketi, kart açıklaması, aşama (erken / orta / guclu), ön koşul ödülün kimliği
# (yoksa None), koşul, etki sınıfı ve etkinin alanları. İlk prototipte hepsi tek alınır (maxStacks 1) ve teklif ağırlığı 1'dir;
# mevcut aşama slotu ve ağırlık politikası değişmez.
REWARDS = [
    # ---- ERKEN
    dict(id='elektrik_anteni', asset='ElektrikAnteni', name='Elektrik Anteni', label='Anten:', stage='erken', requires=None, condition=ALWAYS,
         note='Anten tarlanın kenarında durur; saksı ve tile yeri kaplamaz. Vuruşu senin vuruşun sayılmaz: kritik atmaz, davranış tetiklemez.',
         effect='AntennaEffectSO',
         fields=dict(reach=3,          # antenin baktığı yönde vurduğu hücre
                     interval=2.5,     # iki vuruş arası, sn
                     damage=0.6)),     # hasat hasarı stat'ına oran
    dict(id='hilal_darbesi', asset='HilalDarbesi', name='Hilal Darbesi', label='Hilal:', stage='erken', requires=None, condition=ALWAYS,
         note='Boş savuruş sayılmaz. Hilal mouse halkasının kenarından çıkar; 8 birim/sn hızla tarla kenarında durmadan 3 dünya birimi ilerler. Büyüyen Ay menzili 4 yapar.',
         effect='CrescentEffectSO',
         fields=dict(everyAttacks=6,   # kaç geçerli doğrudan saldırıda bir
                     steps=4,          # ilerlediği hücre
                     stepSeconds=0.3, # adım aralığı, sn
                     maxDistance=3.0, # dünya birimi
                     moveSpeed=8,   # dünya birimi/sn
                     damage=0.8)),     # hasat hasarı stat'ına oran
    # ---- ORTA
    dict(id='iletken_hat', asset='IletkenHat', name='İletken Hat', label='Hat:', stage='orta', requires='elektrik_anteni', condition=ALWAYS,
         note='İkinci antenin yerini sen seçersin; hat kurulamayan konum seçilemez. Hat vuruşu senin vuruşun sayılmaz.',
         effect='ConductiveLineEffectSO',
         fields=dict(interval=4.0,           # iki hat vuruşu arası, sn
                     damage=0.45,            # hasat hasarı stat'ına oran
                     minCells=3,             # hattın kurulabilmesi için en az hücre
                     secondAntennaPulses=1)),  # ikinci anten de kendi üç hücresine vurur
    dict(id='buyuyen_ay', asset='BuyuyenAy', name='Büyüyen Ay', label='Büyüme:', stage='orta', requires='hilal_darbesi', condition=ALWAYS,
         note='Ana hilal doğduğu andan yok olana kadar ilerlediği mesafeyle orantılı büyür; ilk hasadı beklemez. Hasar büyümesi gerçek hasatlara bağlıdır. Dolunay baştan büyük ve sabittir.',
         effect='GrowingMoonEffectSO',
         fields=dict(maxGrowth=3,            # en yüksek büyüme düzeyi
                     extraDistance=1.0,     # menzile ek dünya birimi
                     damagePerGrowth=0.25,   # düzey başına hasar artışı
                     maxHalfWidth=1)),       # cephenin en büyük yarı genişliği (1: üç hücre)
    dict(id='kritik_ark', asset='KritikArk', name='Kritik Ark', label='Ark:', stage='orta', requires=None, condition=ALWAYS,
         note='Ark, vuruşunun değmediği bir bitkiye gider. Kritik atmaz, yeni ark doğurmaz.',
         effect='CritArcEffectSO',
         fields=dict(damageRatio=0.3,        # kritik vuruşun hesaplanmış hasarına oran
                     rangeCells=2,           # hedef arama uzaklığı, hücre
                     maxArcsPerAttack=3)),   # bir saldırıda en çok ark
    dict(id='hasat_yankisi', asset='HasatYankisi', name='Hasat Yankısı', label='Yankı:', stage='orta', requires=None, condition=ALWAYS,
         note='Yalnız kendi vuruşunla yaptığın hasatlar sayılır. Yankı yeni yankı doğurmaz.',
         effect='HarvestEchoEffectSO',
         # 23 Ekim 2026 meteor düzeni (kullanıcı kararı): tek meteor, kesilen hücrelerin simetriğine; boyutu hücre sayısıyla büyür.
         fields=dict(minHarvests=1,          # tetik: tek saldırıda en az bu kadar doğrudan hasat
                     damage=0.5,             # hasat hasarı stat'ına oran (hücre başına)
                     maxCells=6,             # meteorun en çok vurduğu hücre
                     delay=0.5,              # sn; meteorun düşüş süresi = vuruş gecikmesi (23 Ekim: 0,2 çok hızlı, 0,55 biraz yavaş)
                     cooldown=6)),           # sn (round saati); meteor planlanınca yeni meteor için bekleme
    dict(id='tasan_hasar', asset='TasanHasar', name='Taşan Hasar', label='Taşma:', stage='orta', requires=None, condition=ALWAYS,
         note='Yalnız kendi vuruşunun öldürdüğü bitkiden başlar. Aktarım kritik atmaz.',
         effect='OverflowEffectSO',
         fields=dict(ratio=0.5,              # aktarılan pay (aktarım başına kayıp: 1 − ratio)
                     maxTransfers=2,         # bir kaynaktan en çok aktarım
                     rangeCells=1,           # hedef arama uzaklığı, hücre
                     maxSourcesPerAttack=4)),  # bir saldırıda aktarım başlatan en çok hasat
    dict(id='nadir_reaktor', asset='NadirReaktor', name='Nadir Reaktör', label='Reaktör:', stage='orta', requires=None, condition=ANY_BEHAVIOR_PLANTER,
         note='Patlama · kasırga · bumerang · elektrik. Şansı olmayan saksıya şans vermez; en çok %100.',
         effect='RareReactorEffectSO',
         fields=dict(minRarity=2,            # PlantRarity: 2 = Rare
                     chanceBonus=0.25,       # davranış şansı çarpanı: ×(1 + bu)
                     seconds=4.0)),          # süre, sn
    dict(id='acgozlu_hasat', asset='AcgozluHasat', name='Açgözlü Hasat', label='Açgözlülük:', stage='orta', requires=None, condition=ALWAYS,
         note='Boş savuruş hakkı harcamaz. Güçlenen vuruş yeni hak kazandırmaz. Davranışlara geçmez.',
         effect='GreedyHarvestEffectSO',
         fields=dict(minHarvests=3,          # hak için tek saldırıda doğrudan hasat
                     damageMultiplier=1.5)), # güçlenen saldırının doğrudan hasar çarpanı
    # ---- GÜÇLÜ
    dict(id='kapali_devre', asset='KapaliDevre', name='Kapalı Devre', label='Devre:', stage='guclu', requires='iletken_hat', condition=ANY_BEHAVIOR_PLANTER,
         note='Ek vuruş kendini yeniden başlatamaz. Davranışı olan saksı yoksa çalışmaz.',
         effect='ClosedCircuitEffectSO',
         fields=dict(extraDelay=0.35,        # ek hat vuruşunun gecikmesi, sn
                     extraDamage=0.6,        # normal hat hasarına oran
                     cooldown=3.0,           # iki ek vuruş arası en kısa süre, sn
                     chanceFactor=1.0)),     # zar: saksının en yüksek davranış şansı × bu
    dict(id='dolunay', asset='Dolunay', name='Dolunay', label='Dolunay:', stage='guclu', requires='buyuyen_ay', condition=ALWAYS,
         note='Her ana hilalin yanında yalnız bir büyük ek hilal çıkar; hasat koşulu yoktur. Mouse halkasından doğduğu anda, ana hilalden farklı rastgele yönde ilerler. Baştan büyüktür ve yeniden Dolunay üretmez.',
         effect='FullMoonEffectSO',
         fields=dict(growthThreshold=2,      # ikinci dalgayı başlatan büyüme düzeyi
                     returnDamage=0.7,       # hilalin taban hasarına oran
                     returnDelay=0.15,       # sn
                     pauseSeconds=0.35)),    # doğrudan saldırı duraklaması, sn (kod üst sınırı 0,6)
    dict(id='asiri_yukleme', asset='AsiriYukleme', name='Aşırı Yükleme', label='Yükleme:', stage='guclu', requires=None, condition=ANY_BEHAVIOR_PLANTER,
         note='Ek proc zincire katılmaz ve yeni zar atmaz. Kasırga / bumerang aktif sınırı aşılmaz.',
         effect='OverloadEffectSO',
         fields=dict(damageFactor=0.5,       # saksının normal davranış hasarına oran
                     delay=0.3,              # sn
                     cooldown=2.5,           # aynı saksı + davranış için bekleme, sn
                     maxPerAttack=3)),       # bir oyuncu saldırısında en çok ek proc
]

# ---------------------------------------------------------------- tarla olayları
# FieldEventKind: 0 Diken Kuşatması, 1 İstilacı Filiz, 2 Aşılı Kabuk, 3 Gezgin Bereket.
THORN, SPROUT, SHELL, BOUNTY = 0, 1, 2, 3

FIELD_EVENTS = dict(
    firstRound=26,                 # bu round'dan önce tarla olayı yok
    startDelay=4.0,                # round başından hazırlığa, sn
    bossRounds=[(THORN, 1), (SPROUT, 1), (SHELL, 1)],   # boss round'larında (R26, 30, 33, 36, 40, 43, 46, 50): (olay, ağırlık)
    normalRoundChance=0.5,         # boss olmayan round'da olay çıkma olasılığı
    normalRounds=[(BOUNTY, 1)],    # boss olmayan round'larda
    pinned=[],                     # (round, olay): sabitlenmiş round'lar (boş: hepsi zarla)
    thornSiege=dict(
        timing=dict(prepareSeconds=5, emergeSeconds=1.2, activeSeconds=30, resolveSeconds=1),
        zoneWidth=3, zoneHeight=3,
        maxProductionShare=0.4,    # bölge, üretim noktalarının en çok bu kadarı
        rootDurability=6),         # bir kök için doğrudan saldırı sayısı
    invasiveSprout=dict(
        timing=dict(prepareSeconds=4, emergeSeconds=1, activeSeconds=35, resolveSeconds=1),
        mainDurability=8,
        spreadInterval=2.5, spreadSeconds=20,
        maxPieces=8, maxRadius=2,
        slowMultiplier=1.6),       # etkilenen hücrede üretim aralığı çarpanı
    graftedShell=dict(
        # activeSeconds burada okunmaz: aktif süre = choiceSeconds + closedSeconds.
        timing=dict(prepareSeconds=6, emergeSeconds=1.2, activeSeconds=28, resolveSeconds=1),
        zoneWidth=3, zoneHeight=2,
        maxProductionShare=0.6,    # iki bölge birlikte
        targetDurability=4,
        choiceSeconds=8, closedSeconds=20),
    wanderingBounty=dict(
        timing=dict(prepareSeconds=3, emergeSeconds=0.8, activeSeconds=30, resolveSeconds=1),
        zoneSize=3,
        moveInterval=5,
        threshold=6,               # canlanma için bölgedeki doğrudan hasat
        maxBlooms=3),
)
