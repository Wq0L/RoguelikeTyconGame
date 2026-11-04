# Faz 3: kilometre taşları ve aşama konuşmaları — TASLAK (4 Kasım 2026)

Durum: **taslak, kullanıcının onayını bekliyor.** Kodda henüz yok. Satırlar ilk taslaktır; ton ve ipucu sertliği konuşulacak.

## Şu an kodda olan (Faz 1)
- Hikâye aşaması 0–3 kayıtta (`MetaSave.storyStage`).
- Aşama yalnız run sonunda artar; run içinde Eri'nin tonu değişmez.
- Taşlar `DialogueProgress`'te tanımlı:
  - `stage1.firstBoss` → aşama 1;
  - `stage2.round25` → aşama 2;
  - `stage3.fullVictory` → aşama 3.
- **Eksik:** taşları oyundaki anlara bağlayan çağrılar (`AddMilestone` hiçbir yerden çağrılmıyor) ve aşamalara ait satırlar.
- Ertelenen hikâye satırları kalıcı bir kuyrukta tutulur, sonraki sakin anda gösterilir.

## Kilometre taşları
| Aşama | Taş | Ne zaman kazanılır | Ne zaman uygulanır | Eri'nin görünüşü |
|---|---|---|---|---|
| 0 | başlangıç | — | — | mutlu |
| 1 | İlk Sınama geçildi | bir Sınama round'u ilk kez başarıyla bitince | o run bitince | mutlu (aynı); konuşma ipuçları sertleşir |
| 2 | R25'e ulaşıldı | R25 ilk kez başlayınca (demo R20'de bittiği için demoda olmaz) | o run bitince | gri, üzgün |
| 3 | Tam oyun zaferi | tam oyun profilinde R50 zaferi (demo zaferi saymaz) | zafer ekranında, açılış konuşmasının içinde | kırmızı, şeytani |

- Bir run'da iki taş birden kazanılırsa (ör. ilk iyi run'da hem Sınama hem R25): yalnız yüksek aşamanın geçiş konuşması oynar. Atlanan aşamanın hikâye satırları kuyruğa girer, sonra gelir.

## Bir taş uygulanınca ne olur
1. **Görünüş:** gezegendeki Eri ve konuşma kutusundaki Eri yeni hâle geçer. Bellekte yalnız güncel hâlin tablosu tutulur.
2. **Kutu konturu** ("ruh hâli teması"nın ilk küçük adımı): mutluda camgöbeği, üzgünde soluk gri-mavi, şeytanide kırmızı. Arayüzün geri kalanı HUD geçişinde ele alınır.
3. **Geçiş konuşması:** bir sonraki sakin anda (run hazırlık ekranı), büyük kutuda, bir kez oynar. Değişim sessiz kalmaz, Eri kendisi fark ettirir.
4. **Aşamanın hikâye satırları:** sonraki run'larda sakin anlarda (hazırlık ekranı) oynar, her run'da en çok bir konuşma. İpuçları aşama aşama sertleşir.
5. **Oynanış:** etkilenmez. Denge, kart ve kota değişmez; yalnız görünüş ve ses değişir. Öneri, onay gerekir.

## Satır taslakları
Her satır kutuya iki satırı geçmeyecek kısalıkta. Hepsinin bir de ikinci, karanlık okuması var.

### Aşama 0: mutlu (var olanlar + 1 yeni)
- **Var olan ipuçları:**
  - giriş: "…kimse düşmemişti… yani, gelmemişti." (gemileri o düşürüyor);
  - T4: "Dünya'da otomatik tırpan yok muydu?" (Dünya'yı tanıyor);
  - dönem: "Bir sonraki payım biraz daha büyük olacak." (açlık büyüyor).
- **Yeni hikâye satırı** (3. run'dan sonra, hazırlık ekranında):
  - "Gemin nereden geliyordu? Dünya mı? …Hiç duymadım."
  - Dünya'yı hatırladığını söylemişti; çelişki fark eden oyuncu için bir ipucu.

### Aşama 1: mutlu, ipuçları sertleşir
- **Geçiş** (ilk Sınama'dan sonraki run başı):
  1. "Bir Sınama'yı geçtin! Köklerime kadar bir titreme indi."
  2. "Çok güçlenme ama… Güçlü çiftçiler bir gün hep gider."
- **Hikâye satırları:**
  - "Eskiden mavi, kocaman bir tarlam vardı. Sıcacıktı. Sonra… bitti." (mavi tarla = Dünya)
  - "Adak'ı neden mi istiyorum? Büyümek için. Sen büyümek istemez misin?"

### Aşama 2: gri, üzgün
Hüznün sebebi (kullanıcının fikri): oyuncuyla daha fazla oynayamayacak, foyası ortaya çıkmak üzere.
- **Geçiş** (R25'ten sonraki run başı; Eri artık gri):
  1. "Bu kadar uzağa geleceğini düşünmemiştim…"
  2. "Biraz soldum, değil mi? Sen güçlendikçe ben…"
  3. "Seninle biraz daha oynamak isterdim. Ama yakında soru sormaya başlarsın."
- **Hikâye satırları:**
  - "Geminin yakıtı neden bitti, hiç düşündün mü? …Boş ver."
  - "Mavi tarlamın toprağı çekirdeğine kadar sıcaktı. Tadı hâlâ damağımda."
  - "Bana kızacaksın. Hepsi kızmıştı."

### Aşama 3: maske düşer (ilk tam zafer)
- **Zafer ekranında**, ilk tam zaferde, mevcut zafer satırının yerine oynar.
- İlk satırda Eri gri ve üzgün. İkinci satırda kutudaki Eri kırmızı hâline geçer: maskenin düştüğü an.
  1. *(üzgün)* "Kazandın. Sona geldin…"
  2. *(şeytani)* "O zaman oyun bitti. Saklanmama da gerek kalmadı."
  3. "Mavi tarla sizin Dünya'nızdı. Çekirdeğini ben yedim."
  4. "Gemini de buraya ben çektim. Ellerinize ihtiyacım vardı."
  5. "Ve hâlâ açım. Hasat et, çiftçi."
- **Sonraki run başı** (bir kez): "Artık birbirimizi tanıyoruz. Adak'ım bekliyor."
- **İsteğe bağlı:** gezegendeki kısa tepkiler hâle göre değişir: mutlu "Mmm…", üzgün "…", şeytani "Hıh."

## Kullanıcıya sorular
1. Taş 1 için hangisi: Sınama'yı **geçmek** mi (öneri), yoksa ilk Sınama'ya **ulaşmak** mı?
2. Zafer konuşmasında Eri'nin satır ortasında kırmızıya dönmesi olsun mu? Kutuda Eri sabit; yalnız bu an için satıra hâl bilgisi eklenir.
3. Aşamaların oynanışa etkisi olmasın mı (öneri: olmasın)?
4. Satırlar: ton uygun mu, aşama 2 ipuçları fazla mı açık?
5. Zaferden sonra oyun aynen devam eder mi (run'lar sürer, Eri kırmızı kalır)?

## Kullanıcının notları (4 Kasım gece) — UI geçişinden sonra uygulanacak
- **Arayüz rengi:** Eri'nin hâli değişince yalnız arayüzün rengi değişsin, bu kadarı yeter.
  - Mutlu ve "gerçek Eri" (kırmızı şeytani) için arayüz aynı, normal renkler. Şeytani hâlde arayüz normal rengine geri döner.
  - Yalnız üzgün aşamada arayüz renkleri değişir.
  - (Taslaktaki "kontur rengi aşamaya göre" önerisinin yerine geçer.)
- **İlk iş olarak 10–15 konuşma.** Konuşmalar rastgele gelebilir: sakin anlarda bir havuzdan, tekrar etmeden.
- **Cevap seçimi:** oyuncu Eri'ye cevap seçer; hikâye hep aynı yere bağlanır, yalnız Eri'nin karşılığı değişir. Örnek:
  - Eri: "Dünya mı? Hiç duymadım."
  - Şıklar: "Peki, öyle olsun" / "Emin misin?"
  - "Peki, öyle olsun" → Eri: "Bana güvendiğin için sağ ol."
  - "Emin misin?" → Eri: "Iıımm… Dünya, Dünya… belki duymuşumdur, bilmem ki."
  - Kullanıcının endişesi: hiçbir şeye etki etmezse oyuncu "niye seçiyorum" der.
  - Önerim: seçim hatırlansın. Eri sonra ona döner (ör. zaferdeki itirafta "Bana güvenmiştin…" ya da "Benden şüphelenmiştin, değil mi?"). Hikâye dallanmaz, ama seçim anlam kazanır; maliyeti tek bir kayıtlı bayrak.
- **Öncelik:** önce yerelleştirme altyapısı (bütün oyun yazıları tek listede). Bu notlar ve kilometre taşları UI geçişinden sonra.

## Onaydan sonra yapılacaklar (tek geçişte)
1. Taşları oyundaki anlara bağla: Sınama başarısı, R25 başlangıcı, tam oyun zaferi.
2. Eri tablolarını oyuna bağla:
   - aşamaya göre tablo;
   - kare sınırları araçla ölçülür;
   - bellekte yalnız güncel tablo;
   - her hâlin kendi silüeti ve kubbe ölçüsü;
   - kontur rengi.
3. Satırları CSV'ye ekle (TR + EN), katalog girişleri ve koşullar.
4. Test aracı: Tools > Diyalog > aşama / Eri hâli seçimi (Play'de, kayda dokunmadan).
5. İzole testler, sonra senin Play testin.
6. Lisans kaydı ve Steam AI beyan notu.
