# Yerelleştirme (çeviri) sistemi

**Tarih:** 4–5 Kasım 2026.

**Durum:**
- Çekirdek kodda ve izole testte geçti (LocalizationVerification 27/27).
- Oyunun yazıları henüz tabloya TAŞINMADI. Şimdilik tabloda yalnız konuşmalar var.
- Play'de görülmedi.

## Mantık (profesyonel yerelleştirmedeki gibi)
- **Tek liste.** Oyuncunun gördüğü her yazı tek tabloda: `Assets/Resources/Localization/GameText.csv`.
- **Anahtar.** Kod, sahne ve veri dosyaları yazıyı değil anahtarı bilir (ör. `ui.skip`) ve yazıyı tablodan çeker.
  - Metni değiştirmek ya da çevirmek için yalnız tablo düzenlenir; koda ve sahneye dokunulmaz.
- **Sütunlar:** `key, note, tr, en, de, …`
  - `note`: çevirmen notu (kim söylüyor, hangi ekran, yer kısıtı, `{0}`'ın anlamı). Oyunda görünmez.
  - Dil sütunları ISO kodudur. Yeni dil = yeni sütun.
  - Kaynak dil Türkçe. Bir dilde hücre boşsa İngilizce, o da boşsa Türkçe görünür. Oyun asla boş yazı göstermez.
- **Yer tutucu.** Değişkenli yazı tek anahtarda yazılır: `{0} altın` → "12 altın".
  - Cümle parça parça birleştirilmez, çünkü diller sözcük sırasını değiştirir.
  - Çevirmen `{0}`'ı istediği yere koyar.
- **Format.** Tablo CSV (UTF-8). Excel, Google Sheets ve çeviri araçları (Crowdin, Lokalise, POEditor) doğrudan açar.

## Nasıl kullanılır
- **Metni değiştirmek:** CSV'yi düzenle, kaydet.
  - Unity dosyayı yeniden içe aktarır. Play açıksa yazılar o anda değişir (açık konuşma kutusu dahil).
  - Konsola "Çeviri tablosu yenilendi: N anahtar." yazılır.
- **Sahnedeki bir yazıyı bağlamak:** yazı nesnesine `LocalizedText` bileşeni ekle, `key` alanına anahtarı yaz.
  - Sahnede elle yazılmış metin yalnız editörde yer tutucudur.
- **Kodda:**
  - düz yazı: `GameText.Get("anahtar")`;
  - değerli yazı: `GameText.Format("anahtar", değer)`;
  - bağlı yazı için: `LocalizedText.SetKey("anahtar", değer)`.
  - Dil değişince yenilenmesi gereken kod `GameText.Changed` olayını dinler.
- **Tools > Çeviri menüsü:**
  - **Tabloyu denetle:**
    - hatalar: yinelenen anahtar, boş Türkçe hücre, bir dilde `{0}` farklı;
    - uyarılar: `<color>` gibi etiketler farklı, anahtar biçimi;
    - her dilde eksik çeviri sayısı.
  - **Sabit yazıları listele:** tabloya bağlanmamış yazıları `Docs/Ceviri/SabitYazilar.csv` dosyasına yazar. Bu, çeviri listesinin taslağı.
  - **Dil (Play): Türkçe / English / Sahte dil.**
    - Sahte dil bütün bağlı yazıları `[Åtlå~·]` gibi aksanlı ve %35 uzun gösterir.
    - Ekranda düz kalan yazı tabloya bağlanmamış demektir.
    - Kutudan taşan yazı, uzun diller (Almanca vb.) için yer sorunu demektir.

## Sabit yazı taraması (4 Kasım)
| Kaynak | Bulunan | Not |
|---|---|---|
| Veri dosyaları (ScriptableObject) | 485 (301 farklı metin) | Çoğu yetenek ağacı düğüm adı. **Eski denge sürümleri de sayıldı** (DengeV1, GecOyunV1, KirilmaV1 …). Yalnız oyunda kullanılanlar çevrilmeli. |
| Sahneler | 148 (71 farklı) | MenuScene 46, GameScene 51, DemoScene 51 (Game'in kopyası) |
| Kod | ~100 | `.text = "…"` atamaları ve büyük harfli etiketler ("SON ROUND!", "LEVEL UP!") |
| Prefab'lar | 12 | |
| Geliştirici araçları | ayrı | Çevrilmez |

Toplam yaklaşık 460 farklı metin; kullanılmayan eski veriler çıkınca daha az.

## Taşıma planı (sıradaki iş)
1. **Canlı veriyi ayır.** Etkin run profilinin ve sahnelerin gerçekten kullandığı veri dosyaları listelenir. Eski denge sürümleri çevrilmez (arşivlenip arşivlenmeyeceği senin kararın).
2. **Sahneler ve prefab'lar.** Bir araç her yazıya `LocalizedText` ve anahtar ekler, metni tabloya yazar.
   - Bugün İngilizce ve Türkçe karışık ("START ROUND", "AÇIK"). Eksik dilleri ben yazarım, sen gözden geçirirsin.
3. **Veri dosyaları.** Ad ve açıklama alanları anahtarla okunur (`data.<dosya>.<alan>`). Bir araç mevcut metinleri tabloya aktarır.
4. **Kod.** Yaklaşık 100 yer `GameText.Get` / `Format`'a çevrilir. Sayı ve birim biçimleri yer tutucuya taşınır.
5. **Kontrol.** Sahte dille Play'de bütün ekranlar gezilir, kalan sabit yazılar ve taşan kutular düzeltilir.
6. **Sonra (çeviri zamanı):**
   - ayarlarda dil seçimi;
   - her alfabe için font: Çince, Japonca, Korece, Kiril, Arapça; lisans kontrolüyle;
   - sağdan sola yazım kararı;
   - çevirmen için dil başına dışa / içe aktarma;
   - her dilde bütün ekranların otomatik görüntüsü (test için).

**Kural (şimdiden):** yeni eklenen her oyuncu yazısı doğrudan tabloya girer: Türkçe, İngilizce ve çevirmen notuyla.
