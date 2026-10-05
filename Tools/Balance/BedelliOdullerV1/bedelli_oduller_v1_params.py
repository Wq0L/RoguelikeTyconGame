# Run50_BedelliOdullerV1 verisi (Bölüm 3.7.4). make_bedelli_oduller_v1.py bu dosyadan iki bedelli ödülü, aşamalı havuzu ve profili üretir.
# Bu profile ait olan: iki bedelli ödülün bütün değerleri ve hangi aşamaya yazıldıkları.
# Başka yerden gelenler (kopyalanmaz, okunur):
#   - Aşamalar, mevcut 16 ödülün dağılımı, teklif politikası, kart açıklamaları → ../OdulAsamalariV1/odul_asamalari_v1_params.py
#   - Takvim, kota ve boss hedefleri → ../TakvimV1/takvim_v1_params.py
#   - Denge seti, XP, level başına temel seçim hakkı (3), başlangıç altını, round süresi → Kırılma V1 / DengeV1 parametreleri
#
# BUNLAR İLK TEST DEĞERLERİDİR, dengelenmiş nihai sayılar değil (kullanıcı kararı, 2 Ekim 2026). Ölçüme göre kendiliğinden ayarlanmaz.

# ---------------------------------------------------------------- bedelli ödüller
# stage: ödülün yazıldığı aşamanın kimliği (odul_asamalari_v1_params.STAGES). İlk sunulduğu boss aşamadan ve takvimden çıkar
#        ('guclu' → R22'de açılır → ilk boss R23). Burada round yazılmaz.
# direct / behavior: doğrudan vuruş ve davranış hasarı çarpanı (1 = yok). 1'in altı bedeldir.
# level_choices: gelecekte kazanılan her level'ın seçim hakkına eklenir (+1 / −1).
# condition: 0 koşulsuz · 2 yerleşmiş en az bir saksıda davranış şansı (patlama, elektrik, kasırga, bumerang) varsa.
# group: karşılıklı dışlama grubu — aynı gruptan biri alınınca diğeri bir daha sunulmaz (ikisi aynı teklifte çıkabilir).
REWARDS = [
    dict(file='BereketliOgrenim_B1', id='bereketli_ogrenim', name='Bereketli Öğrenim', stage='guclu',
         level_choices=+1, direct=0.80, behavior=1.0, condition=0, max_stacks=1, weight=1, group='secim_hakki_takasi',
         note='Davranış hasarı değişmez.'),
    dict(file='DavranisaAdanis_B1', id='davranisa_adanis', name='Davranışa Adanış', stage='guclu',
         level_choices=-1, direct=1.0, behavior=1.50, condition=2, max_stacks=1, weight=1, group='secim_hakki_takasi',
         note='Patlama · elektrik · kasırga · bumerang. Kendi vuruşun değişmez.'),
]

# Level başına seçim hakkının geçerli aralığı (oyun kodundaki RunPower.MinLevelChoices / MaxLevelChoices ile aynı olmalı).
# Üretici, bir ödülün tek başına bu aralığı aşıp hiç sunulamayacak olmasını hata sayar; oyun aralığı aşan ödülü kırpmaz, reddeder.
LEVEL_CHOICE_RANGE = (1, 5)

DISPLAY_NAME = 'Run50 Bedelli Ödüller V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · BEDELLİ ÖDÜLLER V1'
