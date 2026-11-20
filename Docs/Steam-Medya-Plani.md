# Steam ekran görüntüleri ve klipler: plan

**Tarih:** 18 Kasım 2026. Çıktılar: `output/Steam-Medya-20261118/` (repo'ya girmez).
**Çekim yöntemi:** izole kopya (`Library/VerificationProject`), gerçek GameScene + Mechanics Test Lab senaryosu. Oyunun en güncel hâli kullanılır, commit edilmemiş cila dahil. Çekim betiği `SteamMediaCapture.cs` yalnızca kopyada durur.

## Benzer oyunlar ne yapmış (Steam sayfaları, 18 Kasım 2026)

| Oyun | Ekran görüntüsü | Arayüz / yazı | Açıklamadaki hareketli görsel |
|---|---|---|---|
| Balatro | 12 | Hepsi arayüzlü: oyun, dükkân, paketler, koleksiyon | 6 klip, her özellik paragrafından sonra bir tane |
| Brotato | 8 | Hepsi oyun içi kalabalık sahne, arayüz çok az | 1 klip |
| Luck be a Landlord | 5 | Arayüzlü; ekran görüntüleri dile göre yerelleştirilmiş (Türkçe sayfada Türkçe) | 1 klip |
| Dome Keeper | 8 | Karışık: arayüzsüz atmosfer çekimleri ve birkaç yükseltme ekranı | 5 bölüm, her başlığın altında bir klip (Kaz / Savun / Geliştir / Keşfet) |
| Nodebuster | 5 | Çoğu yetenek ağacı, bir aksiyon anı | 3 kısa cümle, her birinin altında bir klip |
| Vampire Survivors | 7 | İlk 4'ü ekranı dolduran geç oyun kaosu; son 3'ü sandık, koleksiyon, menü | — |
| Slay the Spire | 6 | Savaş, kart seçimi, olay, deste ekranı | — |
| Hades | 11 | Neredeyse hepsi farklı renkte bölgelerde oynanış (kırmızı / yeşil / mavi / mor / altın); 2'si karakter konuşması | — |

**Başarılı roguelite'lardan alınan ders (sıralama):**
1. İlk görüntü en güçlü oynanış anı. Vampire Survivors ve Brotato'da bu, güç fantezisinin zirvesi: ekranı dolduran efektler.
2. Arada **ilerleme** görünür: küçük başlangıç → kalabalık geç oyun (Vampire Survivors, Brotato).
3. **Renk çeşitliliği** çok önemli (Hades): her görüntü farklı bir renk paletiyle. Bizde: mor uzay, turuncu patlama, mavi Don Cephesi, kahverengi Sert Kabuk.
4. İlerleme / meta ekranı (yetenek ağacı, koleksiyon) **sona** konur.

**Ortak örüntü:**
- İlk ekran görüntüsü oyunun en okunur "vay" anı.
- Görüntülerin çoğu doğrudan oynanış. En az biri ilerleme ekranı (yetenek ağacı / dükkân).
- Açıklamada her başlığın altında, o başlığı kanıtlayan kısa bir döngü klip var.

## Steam kuralları (Steamworks belgeleri)

- **Ekran görüntüsü:** en az 5 tane, 16:9, en az 1920×1080. Yalnızca oyun içi görüntü olur: konsept çizim, hazır sinematik, ödül ya da pazarlama yazısı yasak.
- **Açıklamadaki hareketli görseller:** PNG, JPG, GIF, WEBP, **MP4, WEBM** kabul ediliyor.
  - En fazla **12 saniye**.
  - Önerilen genişlik **1170 px**.
  - Renk alanı BT.709.
  - Steam yüklenen her dosyayı kendisi videoya çeviriyor (Balatro'nun sayfasında webm olarak görünüyor). Bu yüzden MP4 hem daha kaliteli hem daha küçük; GIF yedek olarak üretilir.
- Yazı içeren görseller için Steam dile göre görsel grubu kurmaya izin veriyor. Ama 12 dil için ayrı görsel demek; **bu yüzden yazısız çekim tercih edildi.**

## Karar (kullanıcı, 18 Kasım)

- **Bütün görüntüler ve klipler yazısız.** Sayfa çeviriler bitmeden, 12 dilde aynı dosyalarla yayına girebilsin. Sayılar kalabilir: hasar sayıları, kaynak sayaçları.
- Arayüzlü çekimlerde harf içeren her yazı gizlenir. Yalnızca ikonlar ve sayılar kalır; ekran boş görünmesin diye sayaçlar ve uçan kaynaklar görünür. Patlamadaki "WOA" çizgi roman efekti oyun görselinin parçası olduğu için kalır.
- Görüntüler **ilerleme hikâyesi** anlatır: 2 saksılık başlangıçtan ~50 saksılık zincir patlamalara.
- Kalite öncelikli: ekran görüntüleri **3840×2160'da çizilip** 1920×1080'e yüksek kaliteyle küçültülür. Kenar tırtıkları gider. 4K hâli de ayrıca verilir.

## Ekran görüntüleri (8, önerilen Steam sırası)

| # | İçerik | Arayüz |
|---|---|---|
| 1 | Yakın plan zincir patlama: patlama, elektrik, hasar sayıları | yok |
| 2 | Başlangıç: 3×3 tarla, 2 saksı | yalnız sayılar |
| 3 | Orta oyun: 7×7 tarla, kaynak sayaçlarına uçan altınlar | yalnız sayılar |
| 4 | Geç oyun: ~50 saksılı 9×9 tarlada zincir patlama | yok |
| 5 | Don Cephesi boss'u: mavi tipi + perde | yok |
| 6 | Sert Kabuk boss'u: kum fırtınası + kahverengi perde | yok |
| 7 | Yakın plan hasat: tırpan savuruşu + hasar sayıları | yok |
| 8 | Yetenek ağacı (ikonlar + seviye noktaları) | yalnız sayılar |

## Durum (18 Kasım, akşam)

- 8 ekran görüntüsü ve 3 klip çekildi: `output/Steam-Medya-20261118/` (dosya listesi ve Steam'e yerleştirme: `OKU.md`).
- Çekim aracı: `Tools/Verification/Editor/SteamMediaCapture.cs`. Yalnızca izole kopyada çalışır.
- Bilinen sınırlar:
  - Kliplerde tarla sakin. Vuruş temposu oyunun gerçek hızında; zincir klibi kısa (3,7 sn), çünkü patlamadan sonra bitkilerin yeniden büyümesi uzun sürüyor.
  - Yetenek ağacı oyundaki gerçek görünümüyle seyrek.
  - Görüntüleri bir insan henüz Steam'de görmedi.

## Klipler (MP4 + GIF, ≤12 sn, 1170 px)

| Klip | Açıklamadaki yeri | İçerik |
|---|---|---|
| C1 | İlk paragraftan sonra (şu anki GIF satırı) | Temel döngü: imleç tarlada gezer, hasat, bitkiler yeniden çıkar |
| C2 | "Küçük bir tarla, büyük zincirleme tepkiler" başlığının altı | Zincir patlama ve elektrik, yakın kadraj |
| C3 | "Eri acıkıyor…" başlığının altı (isteğe bağlı) | Boss atmosferi altında hasat |

C2 ve C3 için açıklamaya yeni GIF satırları eklenmeli. Klipler yazısız olduğu için 12 dilde aynı dosya kullanılır.
