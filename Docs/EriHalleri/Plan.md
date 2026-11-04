# Eri'nin üç hâli: iş planı (4 Kasım 2026)

## SON DURUM (4 Kasım, gece): önizlemeler kullanıcıya gönderildi, ONAY BEKLENİYOR
- Üzgün tablonun arka kareleri düzeltildi ve yerine kondu. Ham hâli `Son/ham/Eri_Uzgun_4K_ham.png`.
- Ölçülen değişiklik: yalnız 4–7, 14 ve 15. karelerde, kare başına 1 019–1 344 piksel. Saydamlık hiç değişmedi.
- Araç ayarı `MIN_PART = 12`. Ölçülen parça boyları aracın içinde yazılı.
- `Kare_Kare_Karsilastirma.png` ve `Animasyon_Onizleme.mp4` düzeltilmiş tabloyla yeniden üretildi.
- Sıradaki: kullanıcı onaylarsa adım 4 (oyuna bağlama) ve adım 5 (lisans kaydı).
- **Kullanıcı yorumu:** "hepsi çok iyi olmuş". Tek sorun: üzgünün camında diğerlerindeki siyah çizgiler yok, camı diğerleri gibi olsun.
- **Cam düzeltmesi:** `Tools/Art/EriMoods/glass_ink.py`, kredisiz.
  - Mutlu tablodan ölçülen dört cam çizgisi (sağ üst yay, sağ yan, sol yanda iki çizgi) her karenin kendi kubbe hattına göre çizildi. Çizgiler yalnız camın üstünde.
  - Morumsu dış çizgi siyaha çekildi.
- **İki seçenek** `Son/CamSecenek/` klasöründe: `Eri_Uzgun_4K_gri_cam.png` (üzgünün gri camı) ve `Eri_Uzgun_4K_mavi_cam.png` (diğerleri gibi mavi). Karşılaştırmalar: `Cam_Karsilastirma.png`, `Cam_Yakin.png`.
- **KULLANICI SEÇECEK.** Seçilen dosya `Son/Eri_Uzgun_4K.png` olur; şimdiki hâli `ham/` klasörüne `Eri_Uzgun_4K_yuzsuz_camsiz.png` adıyla taşınır. Ardından önizlemeler yeniden üretilir.

## ARA DURUM (4 Kasım, kullanıcı oyuna girdi; tarihçe)
- **Bitti:** üç son tablo `Son/Eri_Mutlu_4K.png`, `Eri_Uzgun_4K.png`, `Eri_Seytani_4K.png` (2880², GPT Image 2.5 yüksek 4K; bu turda ~14,75 kredi, bakiye kontrol edilmedi). Üzgün için A taslağı kullanıcı adına seçildi.
- **Kontrol sonucu:**
  - Mutlu: kaş yok, yüzler tutarlı, arka kareler yüzsüz. Temiz.
  - Şeytani: temiz.
  - Üzgün: arka karelerde (4–7, 14–15) yanlışlıkla yüz var. Göz, ağız ve gözyaşı çizilmiş.
- **Hale endişesi yersiz çıktı:** düşük alfa ortalaması ~2/255. Bugünkü Eri de aynı (gövde alfası 253, hale 3,6). Sorun değil.
- **Üzgün düzeltmesi (kredisiz, yerel):** `Tools/Art/EriMoods/remove_back_faces.py`. Repodaki `Eri_Uzgun_4K.png` HENÜZ DEĞİŞMEDİ, çıktılar yalnız geçici klasörde.
  - İyi sürüm: `MIN_PART` filtresi olmadan çalışan sürüm. Altı karede yüz tamamen gitti. Kalan kusur: sapın dibinde ve kenarda 2–3 piksellik minik krem çentikler (oyun ölçeğinde ~1 px altı).
  - Şimdiki `MIN_PART = 60` fazla yüksek: kare 15'te ağız ve gözyaşı geri geliyor.
- **Sıradaki adımlar:**
  1. Parça boyutlarını yazdırıp eşiği seç (ağız ~? px, çentik noktaları ~? px). Ölçüm komutu takıldı, yeniden çalıştırılacak; komuttaki `python -` satırını çıkar.
  2. Ham tabloyu `Son/ham/Eri_Uzgun_4K_ham.png` olarak sakla, düzeltilmişi `Son/Eri_Uzgun_4K.png` yap.
  3. `preview_eri_moods.py` ile `Kare_Kare_Karsilastirma.png` ve `Animasyon_Onizleme.mp4`'ü yeniden üret, kullanıcıya gönder (eski ve yeni yan yana).
  4. Onaydan sonra adım 4 (oyuna bağlama) ve adım 5 (lisans kaydı).

**Eski durum:** kullanıcı "limit yenilenince başla" dedi. Kullanıcı "başla" deyince sırayla aşağıdaki adımlar yapılır.

## Kararlar (şimdiye kadar)
- **Yol:** "bugünkü Eri'yi koru, yalnız şunu değiştir".
  - Referans: bugünkü 16 karelik tablo `Assets/Resources/GreenhouseMascotFrames.png`; Higgsfield'e yüklü, media `aef179df-7c76-415a-be96-84f3be4f95a0`.
  - Yeni tablolarda aynı 4×4 düzen, aynı pozlar, aynı renk, aynı çizgi ve aynı sade yüz stili korunur.
  - Arka kareler (4–7 ve 14–15) yüzsüz kalır.
- **Mutlu:** `Deneme2/mutlu2_a.png` yönü.
  - Bugünkü Eri, yalnız yumuşak, memnun bir gülümseme.
  - Düzeltme: ilk karelerde kaş gibi çizgiler var. Hiçbir karede kaş olmayacak, bütün yüzler aynı.
- **Üzgün:** `Deneme1/uzgun_a.png` ya da `uzgun_b.png` (kullanıcı seçecek).
  - Son hâlde dokusu bugünkü düz toon boyamaya yaklaştırılır.
- **Şeytani:** bugünkü Eri'ye daha çok benzeyecek biçimde, aynı "koru, yalnız değiştir" yoluyla yeniden üretilir.
  - Kırmızımsı yapraklar, turuncu parlayan gözler, sinsi sırıtış, kubbede çatlak, toprakta lav ışığı (açılış videosundaki çatlaklarla bağ).
  - Balkabağı havası olmayacak.

## Adımlar
1. **Son tablolar (Higgsfield, GPT Image 2.5, yüksek kalite 4K):**
   - Mutlu: `mutlu2_a`'dan, kaşsız.
   - Üzgün: seçilen taslaktan, temiz toon.
   - Şeytani: yeniden.
   - Önce şeytani için 2 kredilik orta kalite bir taslak, sonra 3 × 4,25 = 12,75 kredilik son hâller; yedek ~4–8 kredi.
   - Aynı anda en çok 2 iş gönderilir (3'te 429 hız sınırı).
2. **Eski ve yeni karşılaştırma (kredi yok):**
   - Her hâlin 16 karesi bugünkü karelerin yanında, kare kare.
   - Kaş, arka kare, oran ve bacak pozu kontrolü.
   - Yazı çizilmez: FFmpeg'in drawtext'i fontconfig yüzünden çöküyor.
3. **Animasyon önizlemesi (kredi yok, yerel FFmpeg):**
   - `GreenhouseMascot`'taki gerçek zamanlama ve kare sırası kullanılır: önden bekleme 8, yürüyüş 0–3 (saniyede 6), dönüş 8→9→11→10, tepki 12–15, geri yürüyüş 4–7.
   - Video: bugünkü Eri ve üç yeni hâl yan yana.
4. **Oyuna bağlama (kullanıcının onayından sonra):**
   - Aşama başına tablo (`Resources/GreenhouseMascotFrames_*`).
   - Kare sınırları her tablodan araçla ölçülür; elle yazılmaz.
   - Bellekte yalnız güncel aşamanın tablosu tutulur.
   - Her hâl için kontur silüeti.
   - Aşamaya göre seçim: mutlu 0–1, üzgün 2, şeytani 3.
   - İzole testler ve Play.
5. **Lisans kaydı (`Docs/ThirdParty-Licenses.md`):**
   - Higgsfield / GPT Image 2.5 çıktısı oyuna girer.
   - Steam'de önceden üretilmiş AI içeriği beyanı gerekir.

## Kredi
- Başlangıç: 83.
- Deneme 1: 6 kredi; deneme 2 (mutlu): 2 kredi.
- Şimdiki bakiye: 75.
