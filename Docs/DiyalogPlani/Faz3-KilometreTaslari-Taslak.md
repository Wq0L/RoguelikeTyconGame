# Faz 3: kilometre taşları ve aşama konuşmaları — TASLAK (4 Kasım 2026)

Durum: **taslak, kullanıcının onayını bekliyor.** Kodda henüz yok. Satırlar ilk taslaktır; ton ve ipucu sertliği konuşulacak.

## KARARLAR (5 Kasım 2026, kullanıcı)
- **Rastgele yok.** Konuşmalar sırayla gelir, hikâye akış içinde anlatılır.
- **Tempo: yalnız taşlarda.** Hikâyeyi taş konuşmaları taşır; aradaki hikâye satırları (aşağıdaki eski taslakta "Hikâye
  satırları") kalktı. Hazırlık ekranında taş dışında Eri hikâye anlatmaz (Faz 1–2'nin öğretici / tepki satırları aynen kalır).
- **Taş 1:** ilk Sınama'yı GEÇİNCE (ulaşınca değil).
- **Aynı run'da iki taş:** yalnız yüksek aşamanın konuşması oynar; atlanan aşamanın konuşması oynamaz (eski kalanlar atlanır).
- **Zaferden sonra:** oyun sürer, Eri kırmızı kalır.
- **Arayüz rengi:** yalnız üzgün aşamada değişir (mutlu ve şeytanide normal renkler).
- **Varsayılan (itiraz yoksa):** aşamalar oynanışa dokunmaz; cevap seçimi hatırlanır, Eri zafer itirafında ona döner.

## KESİN PLAN v3 — 14 taş, 14 konuşma (5 Kasım akşam, kullanıcı: "aynen böyle onaylıyorum, tam planladığım gibi")
v2'nin yerine geçer (v2 aşağıda tarihçe olarak durur).
- **Her konuşma oyunda bir kez** oynar; ilerleme run'lar arasında kalıcı. Tek run'da R50'ye gelen bütün hikâyeyi o run'da,
  sırasıyla görür; R25'te kalan sonraki run'da gördüğünü bir daha görmez, ilk kez R30'a gelince kaldığı yerden devam eder.
- **Eri'nin hâli taşın anında değişir** (eski "hâl yalnız run sonunda" kuralı kalktı): R25 hazırlığında gri, zafer
  konuşmasının 2. satırında kırmızı. Arayüz rengi üzgün aşamada (R25'ten zafere kadar) değişir; renkler ayrıca seçilecek.
- Hepsi o anın hazırlık ekranında oynar (13: zafer ekranı, 14: zaferden sonraki ilk run başı). Aynı sakin ana iki taş denk
  gelirse ikincisi sonraki hazırlık ekranına kayar; konuşma atlanmaz, sıra korunur.
- Hikâye açık söylenir (ipucunu kaçıran da anlasın): mutluda küçük çelişkiler, üzgünde adım adım itiraf, zaferde maske düşer.

| # | Taş (ilk kez) | Eri |
|---|---|---|
| 1 | R5'e ulaşıldı | mutlu |
| 2 | İlk Sınama geçildi (seçim, kaydedilir) | mutlu |
| 3 | İlk boss ödülü alındı | mutlu |
| 4 | R10 | mutlu |
| 5 | R15 | mutlu |
| 6 | R20 | mutlu |
| 7 | R25 (griye döner) | üzgün |
| 8 | R30 | üzgün |
| 9 | R35 | üzgün |
| 10 | R40 | üzgün |
| 11 | R45 | üzgün |
| 12 | R50 hazırlığı (son Sınama öncesi) | üzgün |
| 13 | İlk tam zafer (zafer ekranı; 2. satırda kırmızıya döner) | kırmızı |
| 14 | Zaferden sonraki ilk run başı | kırmızı |

### Satırlar (onaylı)
1. "Beş round oldu bile! Fena değilsin, çiftçi." / "Sen çalıştıkça ben büyürüm. Adil bir anlaşma, değil mi?"
2. "Bir Sınama'yı geçtin! Köklerime kadar bir titreme indi." / "Gemin nereden geliyordu? Dünya mı? …Hiç duymadım."
   - "Peki, öyle olsun" → "Bana güvendiğin için sağ ol." *(kayıt: güven)*
   - "Emin misin?" → "Iıımm… Dünya, Dünya… belki duymuşumdur, bilmem ki." *(kayıt: şüphe)*
   - "Çok güçlenme ama… Güçlü çiftçiler bir gün hep gider."
3. "O ödülü sana tarla verdi. Tarla seni seviyor." / "Ben de seviyorum. Güçlü eller çok kıymetli."
4. "Adak'ı neden mi istiyorum? Büyümek için." / "Ne kadar çok verirsen o kadar büyürüm. Sen de büyümek istemez misin?"
5. "Senden önce de çiftçiler geldi. Hepsi çok çalışkandı." / "Sonra… gittiler. Evet. Gittiler."
6. "Gemin neden düştü, hiç düşündün mü? Yakıtın vardı, motorun sağlamdı." / "…Boş ver. Burada mutlusun, değil mi?"
7. "Bu kadar uzağa geleceğini düşünmemiştim…" / "Biraz soldum, değil mi? Sen güçlendikçe ben korkuyorum." /
   "Seninle biraz daha oynamak isterdim. Ama yakında soru sormaya başlarsın."
   - "Ne sorusu?" → "…Boş ver. Hasat et." / "Soru sormam." → "Hep böyle dersiniz." *(kaydedilmez)*
8. "Eskiden mavi, kocaman bir tarlam vardı. Toprağı çekirdeğine kadar sıcaktı." / "Onu bitirdim. Tadı hâlâ damağımda."
9. "Önceki çiftçiler gitmedi. Ben onları… hasat ettim." / "Bana kızacaksın. Hepsi kızmıştı."
10. "Gemiler kendiliğinden düşmez. Senin gemini ben çektim." / "Ellerine ihtiyacım vardı. Hâlâ var."
11. "Mavi tarlanın bir adı vardı. Senin gemin oradan kalktı." / "Artık biliyorsun, değil mi?"
12. "Son Sınama. Geçersen beni yenmiş olursun." / "Geçemezsen yine buradasın. İkisi de benim işime yarar."
13. *(üzgün)* "Kazandın. Sona geldin…" / *(kırmızı)* "O zaman oyun bitti. Saklanmama gerek kalmadı." /
    "Evet. Dünya'nın çekirdeğini ben yedim. Gemini de ben düşürdüm." /
    güven → "Bana güvenmiştin. En tatlısı buydu." · şüphe → "Benden şüphelenmiştin. Haklıydın, ama yine de kaldın." /
    "Üzgün değildim, çiftçi. Hiç olmadım. Sadece açtım." / "Hasat et. Bir sonraki gezegen için güçlenmem lazım."
14. "Artık birbirimizi tanıyoruz. Adak'ım bekliyor." / "Kaçamazsın. Gemin yok, Dünya'n yok. Sadece ben varım."

## Konuşma önerisi v2 (yalnız taşlar, sıralı) — v3 ile DEĞİŞTİ (tarihçe)
Kullanıcı: ton uygun, böyle kalsın; Taş 2'nin "mavi tarla" ipucu olduğu gibi kalır; Taş 3'te Eri konuşmanın ortasında
(2. satırda) kırmızıya döner; yalnız Taş 1'in seçimi kaydedilir (Taş 2'nin seçimi yalnız o anki cevabı değiştirir).
Satırlar kısa (kutuda en çok iki satır). Taş 1 ve Taş 2'de birer cevap seçimi var; Taş 1'in seçimi kaydedilir, Taş 3'te Eri ona döner.

### Taş 1 — ilk Sınama geçildi (run bitince, sonraki hazırlık ekranında; Eri mutlu)
1. "Bir Sınama'yı geçtin! Köklerime kadar bir titreme indi."
2. "Gemin nereden geliyordu? Dünya mı? …Hiç duymadım."
   - **Seçim A** "Peki, öyle olsun." → "Bana güvendiğin için sağ ol." *(kayıt: güven)*
   - **Seçim B** "Emin misin?" → "Iıımm… Dünya, Dünya… belki duymuşumdur, bilmem ki." *(kayıt: şüphe)*
3. "Çok güçlenme ama… Güçlü çiftçiler bir gün hep gider."
   (İlk konuşmadaki "Dünya'da otomatik tırpan yok muydu?" satırıyla çelişki: dikkatli oyuncu için ipucu.)

### Taş 2 — R25'e ulaşıldı (run bitince; Eri gri, arayüz rengi değişir)
1. "Bu kadar uzağa geleceğini düşünmemiştim…"
2. "Biraz soldum, değil mi? Sen güçlendikçe ben…"
3. "Eskiden mavi, kocaman bir tarlam vardı. Toprağı çekirdeğine kadar sıcaktı."
4. "Seninle biraz daha oynamak isterdim. Ama yakında soru sormaya başlarsın."
   - **Seçim A** "Ne sorusu?" → "…Boş ver. Hasat et."
   - **Seçim B** "Soru sormam." → "Hep böyle dersiniz."

### Taş 3 — ilk tam zafer (zafer ekranında; maske düşer)
1. *(üzgün)* "Kazandın. Sona geldin…"
2. *(şeytani)* "O zaman oyun bitti. Saklanmama da gerek kalmadı."
3. "Mavi tarla sizin Dünya'nızdı. Çekirdeğini ben yedim."
4. "Gemini de buraya ben çektim. Ellerinize ihtiyacım vardı."
5. Taş 1 seçimine göre: güven → "Bana güvenmiştin. En tatlısı buydu." / şüphe → "Benden şüphelenmiştin, değil mi? Haklıydın."
6. "Ve hâlâ açım. Hasat et, çiftçi."
- **Zaferden sonraki ilk run başı** (bir kez): "Artık birbirimizi tanıyoruz. Adak'ım bekliyor."

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
- **İlk iş olarak 10–15 konuşma.** ~~Konuşmalar rastgele gelebilir~~ → **5 Kasım kararı: rastgele yok.** Bütün
  konuşmalar sırayla gelir; hikâye akış içinde anlatılır (her konuşma bir öncekinin devamı).
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

## UYGULAMA DURUMU (5 Kasım gece)
- **A adımı yazıldı ve izole kopyada test edildi.** Kullanıcının Play testi bekleniyor. Testler:
  - StoryVerification 47/47;
  - DialogueVerification 74/74;
  - DialogueFlowVerification 86/86;
  - LocalizationVerification 89/89.
- **Kodda:**
  - 14 taş oyundaki anlara bağlı: Sınama geçildi, ilk ödül, R5…R50 hazırlık ekranı, ilk tam zafer, zafer sonrası ilk run başı.
  - Konuşmalar sabit sırayla gelir, her biri bir kez, sakin an başına en çok bir tane.
  - Eri'nin hâli R25 konuşmasında 2'ye, zafer itirafının 2. satırında 3'e geçer.
  - Cevap şıkları kutunun üstünde çıkar (tık ya da 1/2). Taş 1 cevabı kaydedilir, itirafta ona göre satır gelir.
  - İlk tam zaferde itiraf oynar; eski kısa zafer konuşması ikinci zafere kaydı.
- **Sıra düzeltmesi:** ilk Sınama takvimde R3 olduğu için sıra Sınama → ilk ödül → R5 (satırlar aynı).
- **Play testi aracı:** Tools > Diyalog > "Hikâye: sıradaki taşı şimdi kazan (Play)" ve "Hikâye: durumu yaz (Play)".
  - Gerçek kayda yazar.
  - Baştan almak için Play dışında "Görülen konuşmaları sıfırla".
- **Sırada:**
  - B: Eri'nin üç tablosu oyuna. Şimdilik görünüş hep mutlu tablodur; hâl kayıtta değişir.
  - C: üzgün aşamada arayüz rengi.
- **B adımı (5 Kasım gece) yazıldı ve test edildi.** StoryVerification 51/51. DialogueVerification 74/74 ve DialogueFlowVerification 86/86, son "hemen sil" değişikliğinden önce koşuldu.
  - Üç hâl tablosu `Assets/Resources/GreenhouseMascotFrames_<Mutlu|Uzgun|Seytani>.png`. Import ayarları bugünkü tablo gibi: 2048, sıkıştırmasız.
  - Kare sınırları `Tools/Art/EriMoods/eri_frames.ps1` ile ölçüldü. Yöntem bugünkü tabloda koddaki sınırlarla 1–2 px içinde örtüştü.
  - `MascotFrames`: aşamaya göre tablo (0–1 mutlu, 2 üzgün, 3 şeytani). Bellekte yalnız güncel tablo; aşama değişince eski sprite'lar hemen silinir, doku bırakılır.
  - Boyutlar 1254 referansına normalleştirildi: ön kare 297×289; eskisi 295×289.
  - Konuşma kutusu: portre ve kontur silüeti hâle göre. `EriSilhouette_<Hâl>.png` aynı araçla üretildi.
  - Gezegendeki Eri: tablo değişince yeni kareyi alır. Test sahnesinde gezegendeki Eri yok; bu kısım test edilmedi, Play'de görülecek.
  - Kubbe ölçüsü hâle göre %1'den az farklı (0,792 / 0,802 / 0,794). Kontur konumu tek değerle bırakıldı (DomeEdgeMax 0,787).
  - Bedeller:
    - Hâl değişiminde (R25 konuşması, itirafın 2. satırı) 2048'lik doku eşzamanlı yüklenir; kısa bir takılma olabilir, ölçülmedi.
    - Build'de üç tablo sıkıştırmasız ~16 MB'ten ~48 MB.
    - Eski tablo yedek olarak Resources'ta kalıyor.
- **C adımı (5 Kasım gece) yazıldı ve test edildi.** Karar "D · Düz gri", ilk örnekteki oran (%80). Kapsam kullanıcının isteğiyle yalnız konuşma kutusunun arkası ("sadece dialog arkası … ora solsun").
  - Testler (son hâl): StoryVerification 57/57 (gri değeri ve plaka gölgelendiricisi her aşamada, üç ekran görüntüsü, magenta yok), ComicUIVerification geçti. DialogueVerification 74/74 ve DialogueFlowVerification 86/86 geniş kapsamlı ilk sürümde koşuldu.
  - Nasıl: tek ortak gölgelendirici değeri `_UiMoodGrey` (`Assets/Scripts/UI/UiMood.cs`). Üzgün aşamada (2) 0,8, diğerlerinde 0. Yalnız aşama değişince yazılır; karede iş yok.
  - Kapsam: konuşma kutusunun plakaları (`DialoguePlateGraphic` → yeni `UI/ComicMood` gölgelendiricisi, `Resources/UI/ComicMoodUI.mat`). Düğmeler, paneller, bilgi pencereleri, yazılar ve dünya değişmez.
  - İlk sürümde paneller ve düğmeler de soluyordu; kullanıcı yalnız konuşma kutusunu istedi, geri alındı.
  - Mavi kontur kontrolü (kullanıcı: "daha kısa duruyor"): oturmuş kutuda eski tablo / yeni üç hâl / iki gölgelendirici yan yana ölçüldü. Şerit her hâlde aynı: y≈816–930 (1920×1080), kalınlık ~9 px, fark en çok 1 px. Kısa görünen, giriş animasyonunun ortasında çekilmiş ilk test görüntüleriydi. Görüntü: `Library/VerificationProject/Logs/Story/kontur_karsilastirma.png`.
  - Bu sırada bulunan hata düzeltildi: konturun kâğıt renkli altlığı ("Rim Knockout", düz `Image`) üzgün aşamada krem kalıyordu, kâğıt griye dönerken kubbe ile şerit arasında 4 px krem boşluk oluyordu. Altlık artık `UI/ComicMood` kullanır; mavi şerit rengini korur. Test bunu da denetler.
  - Görülen: test görüntülerinde (`Library/VerificationProject/Logs/Story/`) kutunun krem kâğıdı üzgün aşamada gri-beyaza döner, kırmızı aşamada geri gelir.
- **Faz 6 (5 Kasım gece) yazıldı ve test edildi: Mechanics Test Lab'e "I · Diyalog" sekmesi.**
  - Ne yapar: yöneticiyi aç / kapa, oyunun tetiğini yayınla, konuşma ya da tek satır oynat (koşulsuz), hikâye aşaması 0–3, taşlar, Taş 1 cevabı, sıfırlama.
  - Kayıt: yalnız bellekteki test kaydına yazar.
  - Ayrıntı: `Docs/MechanicsTestLab.md` "Diyalog araçları".
  - Testler: `MechanicsTestLabVerification.RunDialogueBatch` 31/31; Story 57, Dialogue 74, Flow 86 geçti. Panelin görünüşü gözle görülmedi.

