# UI geçişi notları

**Başlangıç:** 5 Kasım 2026. UI bölümünde bakılacak maddeler burada toplanır. Görsel kurallar `Docs/UI-Art-Direction.md`'de.

## "Oyun boş ve bitmemiş görünüyor" hissi (5 Kasım, kullanıcının ekran görüntüleri)
Kullanıcı: "Başkalarının oyununa bakınca her şey tamamlanmış görünüyor; benimkine bakınca boş ve bitmemiş gibi hissediyorum,
sebebini çözemedim." İncelenen ekranlar:
- hazırlık (düz harita açık);
- round 1;
- saksı dükkânı;
- yetenek ağacı;
- round 2 özeti.

Bulgular, etkisi en büyükten:

1. **Hata gibi duran kalıntılar.** En hızlı ve en etkili düzeltme.
   - Düz harita (hazırlık / round sonu) açıkken arkadaki eğik tarlanın koordinat rozetleri haritanın kenarından taşıyor: solda "E, F", altta "2–6, 8, 9". İki koordinat sistemi üst üste görünüyor.
   - Haritanın sol üstünde (A1–B2) beyaz kırıntılar var; D ile E başlıklarının arasında dikey beyaz bir çizgi var.
   - Round sonu ekranında D7'de turuncu bir tırpan parçası duruyor.
   - Bu tür izler oyuncuya fark ettirmeden "hata" diye okunuyor, "bitmemiş" hissinin en güçlü tetikleyicisi.
2. **Tarla bir malzeme değil, bir tablo gibi.**
   - Round ekranında tarla uzayda duran düz, koyu gri bir levha: toprağı, kenarı, kalınlığı, dokusu yok.
   - İlk round'da 121 hücreden 9'u dolu; ekranın büyük kısmı aynı koyu gri.
   - Hazırlıktaki düz harita 121 aynı koyu kare; güzel eğik tarlanın üstünü bir hesap tablosu gibi örtüyor.
   - Fikir: zemin dokusu (toprak, sera zemini), tarla kenarı ya da çerçeve, boş hücrelere hafif doku, belki küçük süsler. Sanat yönü kararı gerekir; birkaç örnekle konuşulacak.
3. **Arayüz iki farklı stilde.**
   - Kota kartı, Eri'nin kutusu ve düğmeler çizgi roman tarzında.
   - "Round 1 / 50", "Time: 43s", sağ alttaki "Lv. 1" ve boş çubuk, Gold / Iron / Stone kutuları ise düz yazı ve düz koyu dikdörtgen. Bir kısmı bitmiş, bir kısmı prototip gibi görünüyor.
   - Fikir: hepsi kota kartıyla aynı plaka diline girsin.
4. **Renk az, her şey koyu.**
   - Koyu mor uzay, koyu gri tarla, koyu gri harita. Renkli olan yalnız düğmeler ve gezegen.
   - Ekranın ortasında gözün dinleneceği sıcak bir odak yok.
5. **Yetenek ağacı boş.**
   - Koca ekranda 5 küçük ikon ve ince beyaz çizgiler; düğümler ekrana göre çok küçük.
   - Açılmamış düğümler görünmediği için ağacın şekli yok.
   - Fikir: daha büyük düğümler, açılmamış düğümlerin silik gölgeleri, arkada hafif bir takımyıldızı çerçevesi.
6. **Hareket.** Ekran görüntüleri göstermiyor; ekran dururken bir şey kıpırdıyor mu (ortam canlılığı) ayrıca bakılacak.

Önerilen sıra: 1 (kalıntılar), 3 (HUD tek stil), 2 (tarlaya kimlik, örneklerle), 5 (yetenek ağacı), 4 ve 6 bunlarla birlikte.

## Daha önce UI geçişine bırakılanlar
- **Türkçe tile adları iç ad gibi:** "Crystal-RareSpawnChance", "Odak-Hasar", "Fertile-PlantSpawnRate"… Çeviri listesinde (`Assets/Localization/GameText.csv`, `data.tile.*`) Türkçe hücreden düzeltilebilir. İngilizce temiz adlarla duruyor.
- **Türkçe oyunda kalan İngilizce parçalar:** "Time: {0}s", "Gold / Iron / Stone" adları, nadirlik adları (Common, Rare…). Bilerek eski hâliyle bırakıldı; karar UI geçişinde.
- **HUD panel adları:** Adak / Sınama olarak (Faz 2 notu, `Docs/DiyalogPlani/Faz2-Durum.md`).
- **Ayarlar:** metin hızı, diyalog aç / kapa, "rehberi sıfırla" (Faz 4, `Docs/DiyalogPlani/Faz1-Durum.md`).
- **Üzgün aşamada solma (Faz 3 C, 5 Kasım):** Eri'nin üzgün aşamasında yalnız konuşma kutusunun arkası %80 griye solar (`UiMood`, `UI/ComicMood`). Kullanıcı kapsamı bilerek böyle seçti: önce tüm arayüz düşünüldü, sonra "sadece dialog arkası solsun" dedi. UI geçişinde başka öğeleri soldurma kararı kullanıcının.
