# Run50_KirilmaErisimiV1 verisi (Bölüm 3.7.5). make_kirilma_erisimi_v1.py bu dosyadan kırılma ödülü varyantlarını, havuzu ve profili üretir.
# Bu profile ait olan YALNIZ iki erişim değeridir. Hasar oranı, gecikme, tetik kuralı ve diğer her şey taban asset'lerden gelir
# (Kırılma V1); burada yazılmaz ve buradan değiştirilemez.
#
# Değerler laboratuvar ölçümüyle seçildi (Docs/Bolum3-7-5-KirilmaOdulleriErisim.md, 2 Ekim 2026). Karar kuralı ölçümden önce
# yazıldı: güçlü sinerji düzeninde, ödülsüz kola göre medyan toplam hasat ≥ ×1,5, üç round'un (R23, R30, R40) en az ikisinde;
# geçen adaylardan küçük erişim; hiçbiri geçmezse gerçek hasat katkısı en yüksek aday.

# İlk patlamanın yarıçapı, hücre (oyun kodu: HarvestBehaviorGeometry.ExplosionRadiusCells). Üretici kodla aynı olduğunu denetler.
EXPLOSION_BASE_RADIUS_CELLS = 1.2
# Elektriğin ilk dalgasının çapraz erişimi, hücre (oyun kodu: HarvestBehaviorGeometry.ElectricReachCells).
ELECTRIC_BASE_REACH_CELLS = 2

# ---------------------------------------------------------------- Artçı Patlama
# radius_cells: ikinci darbenin yarıçapı, hücre (saksının her hücresinin merkezinden). Eski profillerde 1,50 (taban × 1,25).
#   Adaylar: 1,50 → ×1,43 / ×1,34 / ×1,39 · 2,00 → ×1,63 / ×1,79 / ×1,62 · 2,25 → ×1,98 / ×2,06 / ×1,84 (R23 / R30 / R40, güçlü sinerji).
#   2,00 ve 2,25 hedefi tuttu; kural gereği küçük olan seçildi.
ARTCI = dict(file='ArtciPatlama_E1', base=('KirilmaV1', 'ArtciPatlama_K1'), radius_cells=2.00)

# ---------------------------------------------------------------- Çifte Akım
# reach_cells: ikinci dalganın dört çapraz yöndeki erişimi, hücre (yakın hücreler dahil). İlk dalga 2 hücrede kalır.
#   Adaylar: 2 → ×1,18 / ×1,20 / ×1,16 · 3 → ×1,20 / ×1,25 / ×1,28 · 4 → ×1,30 / ×1,33 / ×1,34. Hiçbiri ×1,5'i tutmadı;
#   kural gereği gerçek hasat katkısı en yüksek aday (4 hücre) teslim edildi.
# in_pool: ödül bu profilin havuzunda mı. Havuzda kalma kuralı (medyan ≥ ×1,10 ve 7 / 10 seed'de artış, üç round'un en az
#   ikisinde, elektrik düzenlerinde) 4 hücreyle sağlandı. False yapılırsa yalnız bu profilin havuzundan çıkar.
CIFTE = dict(file='CifteAkim_E1', base=('KirilmaV1', 'CifteAkim_K1'), reach_cells=4, in_pool=True)

DISPLAY_NAME = 'Run50 Kırılma Erişimi V1 · 50 round'
VICTORY_TITLE = 'RUN TAMAMLANDI · KIRILMA ERİŞİMİ V1'
