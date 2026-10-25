# Oyunun dili — Eri ve mekaniklerin kılıfı (25 Ekim 2026, taslak)

Hikâye yemeğe atılan tuz: kötü döngüyü kurtarmaz, iyi döngüyü akılda tutar. Ağır hikâye yok. Amaç iki şey:
mekaniklere anlam veren bir **kılıf** ve oyuncunun bağlanacağı, adı geçince akla gelen **tek bir yüz**.

## Karakter: Eri

- Mutasyona uğramış, **hiç doymayan** bir bitki. Gezegenlerin çekirdek enerjisiyle beslenir.
- **Elleri yok.** Kendi gezegenindeki aletleri ve kaynakları kullanamaz; bu yüzden bahçıvana muhtaç.
  Bu onu Flowey gibi "sadece ihanet eden" bir karakterden ayırır: garip bir bağımlılık ilişkisi.
- Görünüşte sevimli ve yardımsever (mevcut maskot, 16 yüz). Arada bir maske kayar, oyuncu ürperir.
- **Ses tonu kuralları:**
  - Kısa cümleler, tek satır.
  - Yemek ve açlık benzetmeleri ("çıtır", "tatlı", "acıktım").
  - Bilmemesi gereken şeyleri bilir. İpucu olarak kullanılır: Dünya'yı, "Dünyalıları", Dünya'nın son gününü.
  - Asla açıkça "ben yaptım" demez; son Sınama'ya kadar.

## Dünya

- Eri Dünya'ya geldi, çekirdeğini yedi; Dünya yok oldu.
- Bir grup insan gemiyle kaçtı, uzayda kayboldu, yakıtı bitti. Bir kütlenin çekimine kapılıp yüzeye indi.
- "Şans eseri" Eri'nin kendi gezegeni. **Gerçekte şans değil:** Eri onları çekti. Bu ipucu katmanının en derinidir.
- İnsanlar Eri'nin gezegenindeki kaynaklarla hasat yapar ve ona adar. Twist'e kadar Eri'nin Dünya'yı yediğini bilmezler.

## Mekanik ↔ kılıf

| Mekanik | Oyundaki adı | Kılıfı (oyuncunun hissettiği) |
|---|---|---|
| Kota | **Adak** | Eri'nin açlığını kısa süre bastıran pay |
| Kotanın her dönem büyümesi | — | Eri'nin açlığı büyüyor |
| Kotayı tutturamamak (run biter) | — | Eri doyamadı, açlık kazandı |
| Boss round | **Sınama** | Eri'nin açlık krizi / "beni doyurmaya değer misin" sınavı |
| Boss ödülü | (ileride) Sınama'dan düşen item | Eri'nin bıraktığı ödül |
| Tarla olayları | **Kapris** | Eri'nin tarlaya fırlattığı mutasyonlu tohumlar (Diken, Filiz, Kabuk) |
| Kapris çözülünce (ileride) | **Armağan** | Eri'nin "cömert" anı: süreli küçük buff |
| Ölüm / yeni run | — | Hikâye run'lar arasında bir adım ilerler |

## Öğretme ilkesi

- **Fiiller öğretilir:** saksı koymak, hasat, Adak, kart seçmek, ilk Sınama, ilk kaprisi kırmak.
- **İsimler keşfedilir:** sinerjiler, davranışlar, elektrik, patlama, boss ödüllerinin birleşimleri, ekonomi.
- **Sonradan adlandırma:** Eri bir şeyi önceden anlatmaz; oyuncu yaşadıktan sonra adını koyar ("Kıvılcım komşuya atladı!").
- **Başarısızlıkta öğretme:** ilk Adak kaybında, run sonunda tek cümle.

## Diyalog yapısı

- **A · Tarla dili:** oyun içi, mevcut gezegen kenarı balonu. Tek satır, 2–3 sn, oyunu durdurmaz. Bir olayla tetiklenir.
  Öğretir ya da adlandırır.
- **B · Run arası:** run başı ve sonu. 1–3 satır, tıkla-devam, her zaman atlanabilir.
- **Bağ:** ikisi aynı **hikâye aşamasını** (0–4) okur. Aşama yalnız kilometre taşlarıyla ve yalnız run sonunda artar.
  A satırlarının aşamaya göre ikinci sürümü olabilir; aşama yükseldikçe maske kayar.
- **Tekrar kuralı:** rehber satırları bir kez çıkar (kayda yazılır). Tekrar eden satırlar seyrek ve kısa.
  Bugünkü "her round başı/sonu konuşma" kaldırılır.

## İpucu katmanları (1.0 kapsamı)

1. **Dil sürçmeleri:** A ve B satırlarında. Ana katman.
2. **Görsel:** gökyüzünde Dünya parçaları, Eri'nin gözünde parıltı. Küçük; yeni çizim gerektirmeyen kadarı.
3. **Bulunan kayıtlar** (gemi seyir defteri, enkaz): 1.0'da yok; zaman kalırsa.

## Kapsam ve kurallar

- Toplam yaklaşık 40 satır.
- Yeni çizim yok: 16 yüz, mevcut balon, run başı/sonu ekranı.
- Metinler baştan anahtarlı tabloda (şimdilik TR).
- Ayarlarda "İpuçları" kapatılırsa A susar.
- Demo yalnız ipucu eker, açılmayı vermez.

## Açık sorular (kullanıcı kararı)

1. **İsim:** Erigone / Eri; kontroller kullanıcıda (Steam araması, marka kaydı, kullanıcı adları).
2. **Hikâye aşamalarının kilometre taşları.** Öneri:
   - 0 = başlangıç
   - 1 = ilk Sınama geçildi
   - 2 = R25'e ulaşıldı
   - 3 = ilk zafer
   - 4 = son Sınama (açılma, yalnız tam oyun)
3. **Gezgin Bereket'in kılıfı:** kapris mi, Armağan'ın öncüsü mü?
4. **Level-up kart seçiminin kılıfı:** gerekli mi, nötr mü kalsın?
5. **Twist anı:** hangi Sınama'da ve nasıl (yalnız satır mı, küçük bir görsel mi)?
