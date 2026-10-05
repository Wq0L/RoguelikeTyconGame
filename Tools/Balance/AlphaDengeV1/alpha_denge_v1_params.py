# Run50_AlphaDengeV1 verisi (Bölüm 3.7.8 — P8: 50–60 dakika aktif run ve ana güç eğrisi). make_alpha_denge_v1.py bu dosyadan
# profili, denge setini, can eğrisini ve (değer değiştiyse) ödül havuzunu üretir.
# Bu profile ait olanlar: round süresi tablosu, bitki can eğrisi, dönem kotaları ve boss hasat hedefleri, mevcut ödüllerin burada
# yazılan sayısal etkileri (zincir çarpanları dahil) ve — yalnız gerekçesi ölçülürse — XP tablosu değişiklikleri.
# Takvim (15 boss tarihi), level başına 3 seçim, başlangıç ekonomisi, ağaç ve fiyatlar, temel statlar, nadirlik XP çarpanları,
# erken davranış erişimi, toplanan XP kartı kuralı, tablo sonrası XP maliyeti ve ödül havuzunun içeriği Run50_XPV1'den aynen
# gelir; burada yazılmaz ve buradan değiştirilemez.
#
# Değerlerin geçmişi (başlangıç ölçümü → 1. ayar turu → 2. ayar turu) ve her değişikliğin ölçülen gerekçesi:
# Docs/Bolum3-7-8-AktifSureVeGucEgrisi.md.

# ---------------------------------------------------------------- round süresi
# (ilk round, saniye): o round'dan bir sonraki satıra kadar her round bu kadar sürer. Toplam 5×45 + 5×55 + 40×65 = 3 100 sn.
ROUND_DURATIONS = [(1, 45), (6, 55), (11, 65)]
# Üretici, toplamın bu sayı olduğunu denetler (tablo değişirse bu da bilerek değiştirilir).
TOTAL_ACTIVE_SECONDS = 3100

# ---------------------------------------------------------------- bitki canı
# Common canı (round, can). Aradaki round'lar geometrik ara değerdir (PlantHealthScalingSO). Nadirlik çarpanları Denge V1 ile aynı:
# can = Common × (1 + 0,5 × nadirlik basamağı) → 1 / 1,5 / 2 / 2,5 / 3.
# Geçmiş: başlangıç ölçümü Denge V1 eğrisiyle yapıldı (… 39, 49, 62, 79, 102, 132, 170, 215): süre tablosuyla her build R20'den
# itibaren Common'ı, R25–35'ten itibaren Rare'i tek vuruşta öldürüyordu. 1. turda HA / HB / HC, 2. turda HA / HM karşılaştırıldı;
# seçilen eğri HM'dir ("erken tek vuruş yok" ve "kırılma mümkün" koşullarının ikisini de sağlayan en dik aday). R1–10 çapaları
# değişmedi (ilk üç boss'un koşulları aynı). Round başına en çok ×1,10 büyür.
HP_ANCHORS = [(1, 20), (5, 25), (10, 31), (15, 44), (20, 67), (25, 99), (30, 146), (35, 212), (40, 306), (45, 440), (50, 627)]
HP_RARITY_MULTIPLIERS = [1, 1.5, 2, 2.5, 3]

# ---------------------------------------------------------------- hedefler
# Dönem sırasıyla (dönemler boss tarihlerini izler: 1–3, 4–6, 7–10, 11–13, 14–16, 17–20, 21–23, 24–26, 27–30, 31–33, 34–36,
# 37–40, 41–43, 44–46, 47–50). Kota: dönemde kazanılan Harvest Score. Boss hedefi: yalnız boss round'unda kazanılan skor.
# Sabit tablolardır; oyuncunun gücüne göre ölçeklenmez.
# Geçmiş: başlangıçta Takvim V1'in aktarım tablosuydu (27, 40, 88, … 4 800 / 5, 10, 16, … 1 100): süre tablosuyla build'lerin skoru
# bunun 50–270 katıydı. Şimdiki tablo 2. turun HM kolundaki düşük uyumlu politikanın (Uyumsuz, 5 seed) medyan skorundan kuralla
# kuruldu: kota = dönem medyanı × 0,65, boss = boss round'u medyanı × 0,60; saniye başına gereksinim yumuşatılmış ve azalmayan;
# ilk üç dönem, makul açılışların en düşük skorunun %80'ini aşmaz. Hesap: analyze_alpha.py targets. Bir dönemin kotası bir
# öncekinden küçük görünebilir (R41–43 üç round, R37–40 dört round): saniye başına gereksinim yine artar.
QUOTA_TARGETS = [40, 75, 190, 500, 1200, 3900, 5200, 7100, 13000, 16000, 22000, 38000, 37000, 48000, 68000]
BOSS_TARGETS = [10, 25, 40, 150, 430, 1000, 1700, 2300, 3000, 4300, 6500, 8400, 11000, 13000, 13000]

# ---------------------------------------------------------------- mevcut ödüllerin sayısal etkileri
# Zincir Hasat çarpanları (ek nesil 1, ek nesil 2). İzinli aralık: şans 0,75 / 0,50 … 1,00 / 0,75; hasar 0,75 / 0,50 … 1,00 / 0,75.
# Ek nesil sayısı (2), kök bütçesi (32) ve kare başına iş bütçesi (8) Zincir V1 asset'inden gelir ve buradan değişmez.
# Geçmiş: değişmedi. Seçilen can eğrisinde laboratuvar (2. tur): iki çarpan birlikte üst sınıra çekilince toplam hasat R30'da
# ×1,087, R40'ta ×1,040; karar kuralı her iki round'da ≥ ×1,05 istiyordu. Ölçülen darboğaz tekrar engeli ve davranış havuzlarıdır.
CHAIN_CHANCE = [0.75, 0.50]
CHAIN_DAMAGE = [0.75, 0.50]
CHAIN_CHANCE_RANGE = ([0.75, 0.50], [1.00, 0.75])
CHAIN_DAMAGE_RANGE = ([0.75, 0.50], [1.00, 0.75])
# Diğer ödüller: {ödül id: {alan: değer}} — yalnız BossRewardSO'nun tek satırlık sayısal alanları. Boş: ödüller Zincir V1
# havuzundaki asset'lerin kendisidir (kopya oluşmaz).
REWARD_OVERRIDES = {}

# ---------------------------------------------------------------- XP tablosu
# {seviye: maliyet} — yalnız yazılan seviyeler değişir. Boş: tablo Run50_XPV1 ile aynı asset'tir (kopya oluşmaz).
XP_TABLE_OVERRIDES = {}

DISPLAY_NAME = 'Run50 Alpha Denge V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · ALPHA DENGE V1'
