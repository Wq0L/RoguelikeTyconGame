# Skill Icons — onay seti / 12 Kasım 2026

Durum: Kullanıcı onayı bekliyor. Oyuna eklenmedi; Assets, sahne, prefab, ScriptableObject ve oyun kodu değiştirilmedi.

- PNG klasöründe 19 ayrı, yazısız, saydam zeminli 512×512 PNG bulunur.
- Dosya adları kullanıcının anahtarlarıyla aynıdır: attack, speed, area, critical, spawn, rare, gold, iron, stone, xp, score, grid, planter, cards, explosion, duplicate, tornado, scythe, electric.
- Siluetin uzun kenarı 408 pikseldir. Ortalanmış yerleşim en az yaklaşık %10 boşluk bırakır; dar şekillerde diğer yöndeki boşluk daha fazladır.
- Review.png: büyük örnekler ve koyu zemin üzerinde gerçek 60 / 80 piksel örnekler. Yazılar yalnız inceleme sayfasındadır, ikon PNG'lerinde yoktur.
- Preview.html: görselleri içinde taşıyan çevrimdışı inceleme sayfası. Tarayıcıda açılabilir.
- manifest.json: ölçüler, alfa sınırları ve SHA-256 değerleri.
- prompts.json: her ikonun üretim istemi ve kaynak dosyası.

## Üretim ve kaynak

19 çizim yerleşik OpenAI image_gen ile ayrı ayrı üretildi. Model sürümü araçta belirtilmedi. Stil referansı bu konuşmada daha önce üretilen Comic Paper UI karesi; ilk attack çizimi diğer ikonlar için ortak stil referansı oldu. HUD kaynakları yalnız tanınabilir nesne türlerini belirlemek için incelendi; HUD görsellerinin pikselleri kopyalanmadı ve üreticiye referans olarak verilmedi. İnternetten font, ikon, görsel veya bağımlılık indirilmedi.

Üreticinin büyük kaynak PNG'leri source klasöründe korunur (ZIP yalnız teslim ikonları ve inceleme belgelerini içerir). Windows'un mevcut System.Drawing bileşeniyle yalnız alfa sınırına göre ortalama ve 512×512 dışa aktarma yapıldı; yeniden çizim veya renk düzenlemesi yapılmadı. Kaynakların kenar yumuşatması ve hafif yüzey tonu farklılıkları korunmuştur.

## Kontrol

19 dosyanın tamamı 512×512; köşe alfa değeri 0; merkezlenmiş siluetler yaklaşık %10 güvenli boşluk içinde. Review.png üzerinde açık/koyu zemin ve 60–80 px örnekler görsel olarak incelendi. Grid dokuz hücre; kart yelpazesi üç kart; duplicate iki filiz; area dört yön oku içerir. Oyuna ekleme ve Unity Edit/Play Mode doğrulaması bu işin kapsamı değildir.

## Kullanıcı onayı ve taşıma — 12 Kasım 2026

Kullanıcı seti onayladı. PNG klasöründeki 19 teslim dosyası Assets/Art/SkillIcons/ klasörüne taşındı; dosya adları korundu ve SHA-256 değerleri doğrulandı. Önizleme, üretim kaynakları ve ZIP burada korunuyor. Yukarıdaki onay bekleme notu ilk üretim durumunu anlatır; artık onay alınmıştır. Oyun arayüzüne veya skill verilerine bağlantı yapılmadı.
