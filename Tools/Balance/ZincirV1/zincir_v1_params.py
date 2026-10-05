# Run50_ZincirV1 verisi (Bölüm 3.7.6). make_zincir_v1.py bu dosyadan Zincir Hasat ödülünü, havuzu ve profili üretir.
# Bu profile ait olan YALNIZ Zincir Hasat ödülüdür. Takvim, hedefler, round süresi, XP, ekonomi, bitki canı, ağaç, başlangıç
# seçenekleri, aşamalar ve diğer bütün ödüller (Artçı 2,00 hücre, Çifte Akım 4 hücre, Hasat Ritmi, bedelli ödüller) Kırılma
# Erişimi V1'den aynen gelir; burada yazılmaz ve buradan değiştirilemez.
#
# Değerler ilk prototip sınırıdır (görev, Bölüm 3.7.6); ölçümle kendiliğinden artırılmaz ya da azaltılmaz.

CHAIN = dict(
    file='ZincirHasat_Z1',
    id='zincir_hasat',
    display_name='Zincir Hasat',
    effect_label='Zincir:',
    # Kartın alt satırı (havuzdaki not). Teknik terim yok.
    note='Her saksı ve davranışı bir saldırının zincirinde bir kez denenir. Artçı ve ikinci dalga zincire katılmaz.',
    stage='guclu',            # güçlü aşama: ilk teklif R23 boss'unda (aşamanın ilk round'u 22)
    weight=1,
    max_stacks=1,
    condition=2,              # BossRewardCondition.AnyBehaviorPlanter: yerleşmiş, davranış şansı olan en az bir saksı
    generations=2,            # ek nesil sayısı (nesil 0: doğrudan hasadın normal tetiği)
    chance=[0.75, 0.50],      # ek nesil başına şans çarpanı: saksının gerçek şansı × değer
    damage=[0.75, 0.50],      # ek nesil başına hasar çarpanı: tetiklenen saksının normal davranış hasarı × değer
    root_budget=32,           # bir doğrudan saldırının en çok başarılı ek tetiği (normal tetikler sayılmaz)
    jobs_per_frame=8,         # zincir kuyruğundan kare başına en çok iş
)

DISPLAY_NAME = 'Run50 Zincir V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · ZİNCİR V1'
