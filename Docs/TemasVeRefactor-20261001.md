# Temas düzeltmesi ve refactor — 1 Ekim 2026

Bu paket iki ayrı iş içerir: kullanıcı tarafından istenen hedefleme değişikliği ve mevcut sistemlerin bakımı. Yeni ekonomi/XP/can dengelemesi yapılmadı. Önceki 3.5 kazanma oranları yeni temas geometrisi için doğrulanmış sonuç değildir.

## Oynanış değişikliği

Hasat dairesi artık hücrenin merkeziyle değil, hücrenin kare yüzeyiyle kesişir. Kenara/köşeye temas yeterlidir. Büyük saksıda yalnız temas edilen üretim hücreleri vurulur; tek hücreye temas bütün saksıya hasar vermez. Bitkinin yaprak mesh'i hedefleme şekli değildir.

`HarvestArea` ortak temas hesabını sağlar. `GridSystem` yalnız dairenin sınır kutusundaki hücrelere bakar, kare mesafe kullanır. Oyuncu kendi hedef listesini tekrar kullanır. Görsel halkaya ikinci bir yarım hücre payı eklenmez.

Denge V1 başlangıç yarıçapı 1 → 0,9. Sis seçilme eşiği yeni temas geometrisinde 1,24 → 1,01. Bu iki sayı params dosyasında değiştirilip üreticiden asset'lere aktarıldı. Diğer denge sayıları bu refactor kapsamında ayarlanmadı.

Sahne testinde üç örnek nişan konumunun en iyisi:

| Aşama | Standart yarıçap/hedef | Dar Kesim yarıçap/hedef |
|---|---|---|
| Başlangıç | 0,9 / 4 | 0,675 / 4 |
| Geniş Süpürüş tamam | 1,215 / 6 | 0,911 / 4 |
| Geniş Tarama tamam | 1,665 / 9 | 1,249 / 6 |
| Alan boss ödülü ×1 | 2,198 / 12 | 1,648 / 9 |
| Alan boss ödülü ×2 | 2,901 / 16 | 2,176 / 12 |

Bu üç nişan örneği bütün olası imleç konumlarının matematiksel maksimumu değildir. Eski profillerin sayısal verileri korunur, fakat ortak hedefleme kuralı onların temas sonuçlarını da değiştirir. Eski sabit ×0,85 Sis bazı konumlarda artık hedef sayısını azaltmaz; Denge V1'in basamak kullanan Sis'i yeni geometriyi okur.

Yerleştirme önizlemesi geçersiz zemin veya UI üzerinde gizlenir, geçerli hücreye dönünce yeni konumunda açılır. Yerleştirme maliyeti veya iptal/iade kuralı değiştirilmedi.

## Bakım değişiklikleri

- **Kayıt:** normal görev ilerlemesi bellekte birikir; periyodik, round sonu, menü, duraklatma ve kapanış noktalarında kayıt denenir. Kilit açılışı tek seferde hemen yazılır. Başarısız yazım kirli durumu korur, yeniden denenebilir. Uygulama başarısız kayıtla zorla kapanırsa henüz diske ulaşmamış ilerlemenin korunması garanti edilemez.
- **Ödül teklifi:** round sonu yalnız seçim hakkını oluşturur. Teklif kartlar ve varsa uzmanlaşma bittikten sonra hazırlanır; yeniden açılan panel teklifi değiştirmez.
- **Ortak hesap sınırı:** `RunPower` doğrudan hasar, davranış çarpanı ve hasat kaynaklarını birleştirir. Oyuncu/saksı kodu bütün güç kaynaklarını ayrı ayrı çarpmaz. Eski katsayı sırası ve tek yuvarlama korunur. Bu sınıf mevcut manager'lara adaptördür; bütün proje bağımlılık enjeksiyonuna çevrilmedi.
- **Önbellek:** StatManager sonuçları global sürüm ve taban değerine göre saklar; ekleme/silme/reset/taban değişimi sonucu geçersizleştirir. HUD ödül listesi yalnız ödül manager'ının sürümü değişince metin üretir.
- **Üretim noktaları:** doluluk ölçümü ve boss hazırlığı aktif spawner kaydını kullanır; tekrar tekrar bütün sahneyi aramaz. Ödül uygunluğu gibi seyrek yolların bütün aramaları kaldırılmadı.
- **Veri tutarlılığı:** aktif run'ın denge verisi RoundManager'ın sabitlenmiş profilinden okunur; seçim asset'inin run ortasında değişmesi farklı hesaplara sızmaz.
- **Sunum:** BossRewardText oyun uygulayıcısından ayrı dosyaya taşındı. Farklı etkileri bir arada taşıyan ödül hepsini açıklar; Set işlemi kademe sayısıyla çarpılmaz.
- **Üretici:** `denge_v1_params.py` esas kaynak. Hash manifest kontrolü, Inspector değişikliği/silinmiş veya beklenmedik dosya gördüğünde üreticiyi yazmadan durdurur. `--check` salt okunur kontrol sağlar. Ayrıntı: `Tools/Balance/DengeV1/README.md`.
- **Test çalıştırıcı:** başarısız Unity çıkışını artık başarılı PowerShell sonucu gibi göstermez. Eski logu arşivler, yeni log oluşmasını zorunlu kılar.

## Doğrulama ve sınırlar

`ContactRefactorVerification`: rastgele konumlarda bağımsız tam-tarla temas hesabıyla 150 karşılaştırma, kenar/köşe testleri, sorgu tahsisi, kayıt I/O hata–yeniden deneme–reload, stat cache invalidation ve birleşik ödül metni. İlk son-kod koşusu 175 kontrol geçti. Isınmış 10.000 hedef sorgusunda 0 byte tahsis ölçüldü; bu bütün oyunun GC veya FPS ölçümü değildir.

`test_generation_guard.py`: Inspector farkı, eksik/yeni çıktı ve satır sonu farkı için üç test geçti. Gerçek asset'lere yazmaz.

İlgili Unity testlerinde değişen beklentiler açıkça yeni davranışa aittir: merkez yerine yüzeye temas, geciktirilmiş ilerleme kaydı ve elle ödül enjekte eden testlerde hazırlanmış teklif işareti. İşlev testi kazanma dengesi veya insan oyun hissi kanıtı değildir.

Son koşuların kayıtları: `Library/VerificationProject/Logs/*Verification.txt` ve `*_RunBatch.log`. Canlı editörün Play oturumu kontrol edilmedi; bütün Unity doğrulamaları izole kopyadadır.

## Kısa Play kontrolü

1. Menüden Denge V1 ve Bahçıvan/Standart seç. Başlangıç halkası önceki sürümden küçük olmalı.
2. Bir bitkinin hücresinin yalnız kenarına, sonra köşesine halkayı değdir; gerçek saldırı aralığını bekle. Temas varsa vurmalı, küçük bir boşluk varsa vurmamalı.
3. Büyük saksının bir ucuna yaklaş: diğer hücreler dairenin dışında kalıyorsa doğrudan hasar almamalı (davranış hasarı ayrı).
4. Alan boss ödülüyle halka ve temas alanı birlikte büyümeli.
5. Yerleştirme sırasında imleci zeminden/UI üzerine çıkar: yeşil önizleme son hücrede asılı kalmamalı.

## Ayrı tutulan denge işleri

### Görseldeki iki hücre kalınlığında Don Cephesi

Denge V1'in `DonCephesi_V1.asset` dosyasında coverage=0,5; eski DonCephesi.asset dosyasında 0,25. `EdgeBandZone.AddEdges` açık alan kenar uzunluğu × coverage sonucunu en yakın tam sayıya, yarımları yukarı yuvarlar. Bu yüzden 3×3'te 3×0,5=1,5 → 2 sütun/satır, yani 6/9 hücre etkilenir. İki ayrı boss gerekmeksizin tek bölge bu görüntüyü üretir. 5×5'te 3/5, yani %60 olur. Bu ayar 3.5 üretici parametrelerinde zaten vardı; bu pakette değiştirilmedi. Nominal %50 ile küçük grid'deki gerçek kapsama farkı denge değerlendirmesine dahil edilmeli.

### Tamamlanan test paketleri (1 Ekim)

| Paket | Geçen kontrol |
| --- | ---: |
| ContactRefactorVerification | 175 |
| DengeV1Verification | 86 |
| BossRewardVerification | 127 |
| StartLoadoutVerification | 88 |
| ElectricRevertVerification | 40 |
| SpecializationVerification | 100 |
| RunPrototypeVerification | 69 |
| MechanicsVerification | 83 |

Üretici korumasının üç Python testi ve salt okunur manifest kontrolü de geçti. Eski Run50 referansının birebir hasat eşitliği bu geometri değişikliğinin kabul ölçütü değildir. Tam-run denge ölçümü ve insan Play testi yapılmadı.

Hız/alan baskınlığı, ekonomi zorunluluğu, XP geri ödemesi, Tüccar'ın ağacı tamamlama hızı ve toplam run süresi bu pakette çözülmüş sayılmadı. Değişen geometri nedeniyle yeni tam-run denge ölçümü gerekir. 104 düğüm ile 221 satın alma kademesi farklı sayımlardır.
