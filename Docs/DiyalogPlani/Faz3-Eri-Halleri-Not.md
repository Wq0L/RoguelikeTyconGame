# Faz 3 notu: Eri'nin üç hâli ve ruh hâli teması (4 Kasım 2026)

Durum: **fikir aşamasında, senin düşünmen bekleniyor.** Kodda henüz hiçbir şey yapılmadı.

## Senin fikrin
- **Üç hâl:** Eri'nin üç hâli kilometre taşlarına göre değişir; oyuncunun gördüğü Eri güncellenir.
  1. Neşeli, tatlış Eri.
  2. Ortalarda biraz kararmış, solmuş, üzgün Eri. Bugünkü uykulu Eri bunun temeli olur: biraz karartılır, soluklaştırılır, üzgünleştirilir.
  3. Sonda şeytanlaşmış, kırmızımsı Eri.
- **Üzüntünün sebebi:** "seninle daha fazla oynayamadım, foyam ortaya çıktı". Bu konuşmayla anlatılır.
- **İpuçları:** adım adım sertleşir.
- **İstek:** arayüz de Eri'nin aşamasına göre stilize olsun. Endişe: "buna yetemem".

## Benim önerim
| Aşama | Ne zaman | Eri | Ton |
|---|---|---|---|
| 0–1 | başlangıç, ilk Sınama sonrası | neşeli, tatlış (yeni çizim) | şakacı; küçük ağız kaymaları |
| 2 | R25 | üzgün (bugünkü Eri'nin kararmış, solmuş hâli) | "neden öyle bakıyorsun?", oyunun bitmesinden korkan hüzün |
| 3 | tam oyun zaferi | şeytanlaşmış, kırmızımsı (yeni çizim) | maske düşer |

- **Görsel bağ:** açılış videosundaki çatlakların lav turuncusu ve sarı çekirdek ışığı, aşama 2'de Eri'nin köklerinde soluk, aşama 3'te güçlü görünür. Oyuncu iki görüntüyü kendisi eşleştirir.
- **Arayüz, "ruh hâli teması":** her aşamanın birkaç ortak ayarı olur. Kod bunları her yere kendisi uygular; arayüz her aşama için yeniden çizilmez.
  - Renkler: kâğıt, mürekkep, Eri'nin konturu (camgöbeği → soluk gri mavi → kırmızı), karartma tonu, köşe noktaları.
  - Küçük ayrıntılar: aşama 2'de solmuş kâğıt; aşama 3'te panellerde ince çatlaklar ve kırmızı parıltı.
- **Sıra:**
  1. Konuşma kutusu: risksiz, kod tamamen bizim.
  2. HUD geçişi: baştan bu ayarlarla kurulur.
  3. Gerekirse diğer ekranlar.
- **Çizim yükü:** önce konuşma kutusundaki tek kare yeter, çünkü orada Eri sabit.
  - Gezegende yürüyen Eri ilk başta yalnız renk ve ton değişimi alır; 16 karenin hepsi sonra çizilir.
  - Yeni çizimler iki kat çözünürlükte olursa H10 (yüksek çözünürlükte bulanık Eri) de çözülür.

## Açık sorular
- Bugünkü Eri kareleri nasıl yapıldı: yapay zekâ mı, kendin mi, başkası mı? Aynı yol tutarlılık için gerekli. Lisans kaydı için de gerekli; yapay zekâysa Steam'de beyan edilir.
- Aşama eşlemesi bu mu olsun?
- Yeni hâl oyuncuya bir satırla mı fark ettirilsin, yoksa sessizce mi değişsin?

## Karar verince ilk adım
1. Üç ruh hâlinin satır taslağı, aşama aşama sertleşen ipuçlarıyla.
2. Üç Eri'nin çizim tarifi: ne değişecek, renk, ifade, aynı duruşla tutarlılık.

İkisi de önce senin onayına gelir.
