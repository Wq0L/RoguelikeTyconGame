# Run50_XPV1 verisi (Bölüm 3.7.7 — P7: XP, tile geliştirme ve sınırsız level). make_xp_v1.py bu dosyadan XP tablosunu, denge
# setini ve profili üretir.
# Bu profile ait olan YALNIZ ilerleme ayarlarıdır: tablo sonrası maliyet kuyruğu, temel güç XP kartlarının birikim kuralı ve
# işlevsiz temel güç kartının süzülmesi. Takvim, kota ve boss hedefleri, round süresi, bitki canı, ekonomi, ağaç, başlangıç
# seçenekleri, level başına 3 seçim, ödül havuzu (zincir dahil) Run50_ZincirV1'den aynen gelir; burada yazılmaz ve buradan
# değiştirilemez.

# Tablo sonrası maliyet: C(L) = C(N) × [1 + s × (L − N)], L > N. N = tablonun son maliyetinin ait olduğu seviye (140),
# C(N) = 150 000 XP. s = TAIL_GROWTH. Karşılaştırılan diğer adaylar: 0,025 ve 0,10 (ölçüm: Docs/Bolum3-7-7-XPVeSinirsizLevel.md).
TAIL_GROWTH = 0.05

# Tile ve yükseltmeler tükendikten sonra gelen "XP kazancı" temel güç kartları kendi aralarında toplanır (iki +%5 → ×1,10).
# Kart değerleri (+%2 / +%3 / +%4 / +%5) kodda, eskisiyle aynı. Diğer temel güç kartları ve diğer XP kaynakları değişmez.
ADDITIVE_BASE_XP_CARDS = True

# Saldırı aralığı oyunun alt sınırına (0,1 sn) inince "Atak aralığı" temel güç kartı sunulmaz (kart hiçbir şey değiştirmezdi).
HIDE_FLOORED_BASE_STATS = True

# XP tablosu (ilk 140 seviye) Progression_DengeV1 ile aynıdır. Ayar turu gerekirse burada, gerekçesiyle yazılır:
#   {seviye: maliyet} — yalnız yazılan seviyeler değişir. Boş: tablo aynen.
TABLE_OVERRIDES = {}

DISPLAY_NAME = 'Run50 XP V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · XP V1'
