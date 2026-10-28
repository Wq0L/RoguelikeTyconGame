# Faz 2 — Play testi listesi (28 Ekim 2026)

Amaç: Eri'nin öğretim konuşmalarını (T1–T11) gerçek oyunda, gerçek tıklamayla görmek.

## Başlamadan
- [ ] **Unity Play dışındayken:** `Tools > Diyalog > Görülen konuşmaları sıfırla (Play testi için)`.
  - Yalnız diyalog kaydı silinir; çiftçi/tırpan seçimi, açılan kartlar ve görevler kalır.
  - Önce `meta.json.before-dialogue-reset` yedeği alınır.
- [ ] **Sahne:** `MenuScene`'den başla. Mechanics Test Scene'de Eri bilerek susar.
- [ ] **Game görünümü:** 1920×1080 (16:9).
- [ ] **Profil:** etkin profil `Run50_GecOyunV1`.
  - İlk Sınama R3'te; ikinci Adak dönemi R6'da biter.
  - Round'lar 45 sn.

## Bölüm 1 — asıl test, R1–R6 (~7 dk)
| # | Ne zaman | Beklenen | Bak |
|---|---|---|---|
| T1 | Menü → Play | Eri büyük kutuda, 3 satır | Ekranın her yerine tık, Boşluk ve Enter ilerletiyor mu? "Atla" geçiyor mu? Kutu açıkken arkadaki kartlar tıklanmamalı, kapanınca tıklanmalı. **Bunu otomatik testte ölçemedim.** |
| T2 | RUN'I BAŞLAT → oyun sahnesi | "Tarla bomboş…" | |
| T3 | PLANTER SHOP → saksı al | "Boş bir yere tıkla…" | Kutu açıkken tarlaya tık saksı koymamalı; kutu kapanınca koymalı. |
| T4 | START ROUND (R1) | Küçük kutu, 2 satır: espri ve ipucu | Kutu açıkken vuruş sürüyor mu? Boşluk kapatıyor mu? Yazı bitince ~6 sn sonra kendiliğinden kapanıyor mu? |
| T5 | R1, T4'ten sonra | "Sol üstteki pay benim Adak'ım…" | |
| T6 | İlk level (kart ekranı) | "Büyüyorsun!…" | Kutu kapanınca kartlar seçilebiliyor mu? |
| T9 | R3 başı (ilk Sınama) | "Bu round bir Sınama!…" | |
| T7 | R3 başı, Adak dolmamışsa | "Dönemin son round'u…" (T9'dan sonra) | |
| T8 | R3 tutarsa, round sonu | "Mmm… doydum." (2 satır) | Sonraki dönem sonlarında gezegendeki Eri "Mmm…" demeli. |
| T11a | R4–R6'da bilerek az hasat → R6 sonu | "Adak eksik kaldı…" | |
| — | Yeniden başlat | Öğretimler tekrar etmemeli | Gezegendeki Eri yalnız "Hmm…" der. |

## Bölüm 2 — isteğe bağlı (~3 dk)
Pencere: `Tools > Geç Oyun Testi`, yalnız Play'de çalışır. "Seçilen round'a git".
- **Not:** round atlanan oturum kayda yazılmaz. Burada görülen konuşmalar sonraki Play'de yine çıkar.
- [ ] **T10:** R26'ya git, bir iki round oyna. İlk kapris hazırlanırken "Tohumlarımdan biri azdı!" çıkmalı.
- [ ] **T11b:** bir Sınama round'unda (R3) hasat yapma. "Sınama'yı geçemedin…" çıkmalı.
- [ ] **T11c (zafer):** R50'yi bitir. "Doydum. Bu sefer gerçekten." Zor olabilir, şart değil.

## Genel olarak neye bak
- **Görünüm:** Eri, kontur, karartma ve köşe noktaları gerçek çözünürlükte nasıl? Pencere boyutu değişince kutu taşıyor mu?
- **Zamanlama:** yazı hızı (saniyede 45 harf) yavaş mı, hızlı mı? Küçük kutunun 6 sn'si yeterli mi, fazla mı?
- **Metin:** ton ve espri oturuyor mu? Değiştirmek istediğin satır var mı?
  - Düğmeler İngilizce ("PLANTER SHOP", "START ROUND"), Eri "dükkân", "round'u başlat" diyor. Kafa karıştırıyor mu?
- **Hata:** Console'da kırmızı hata, takılı kalan kutu ya da yanlış anda çıkan satır.
- **Akıcılık:** ilk açılışta takılma var mı?

## Bildirirken
Ekran görüntüsü + hangi konuşma (T numarası) + ne hissettin. Bir satır yeter.
