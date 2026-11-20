# ERIGONE kapsül incelemesi — 13 Kasım 2026

Durum: İlk dört kapsül onay taslağıdır. Kullanıcı bunların başarılı roguelike örnekleriyle karşılaştırılmasını istedi; mevcut PNG'ler nihai kabul edilmiş tasarım değildir.

## İncelenen örnekler

- Balatro: büyük başlık, oyun kartı ve joker motifleri marka kompozisyonunun içinde. Ders: oyun mekaniği başlıktan kopuk bir dekor olarak kalmamalı. Resmî kaynak: https://www.playbalatro.com/ ve https://store.steampowered.com/app/2379780/Balatro/
- Brotato: sade karakter, silahlar ve tehdit oluşturan çevre siluetleri. Ders: küçük boyutta karakterin ne yaptığı anlaşılmalı. Resmî mağaza: https://store.steampowered.com/app/1942280/Brotato/
- Slay the Spire: karakterler, kule ve kart motiflerinin birlikte kurduğu fantastik macera. Ders: ön plan / orta plan / arka plan hiyerarşisi ile mekânın rolünü anlatmak. Resmî mağaza: https://store.steampowered.com/app/646570/Slay_the_Spire/
- Vampire Survivors: gotik başlık ve kırmızı/koyu tonlar, türe ait karakter imgesi. Ders: tipografi aynı zamanda atmosfer taşımalı. Resmî mağaza: https://store.steampowered.com/app/1794680/Vampire_Survivors/
- Atomicrops: bitkisel logo ve mutant tarım motifleri. Tema açısından doğrudan karşılaştırma. Resmî kaynak: https://www.atomicrops.com/press-kit/ ve https://rawfury.com/games/atomicrops/

Bunlar görsel tasarım yorumlarıdır; kapakların satışa etkisini ölçen A/B testi veya dönüşüm verisi değildir. Başarılı oyun olmaları, başarılarının kapsülden kaynaklandığını kanıtlamaz. Üçüncü taraf görseller indirilmedi, projeye eklenmedi ve görsel üreticiye verilmedi.

## Mevcut taslağın değerlendirmesi

- Güçlü: tanınabilir Eri, cam kubbe, turuncu kırık gezegen, lacivert/teal/cream/orange palet, okunabilir gerçek Bungee harfleri.
- Zayıf: büyük boş arka plan ve poz veren karakter, hasat/build oyununu anlatmıyor; uzay macerası gibi yorumlanabilir.
- Logo sorunu: aynı fontu kullanmak tek başına karakterli bir marka üretmiyor. Sonradan eklenmiş başlık hissi var. İki satır/tek satır alternatifleri nihai logo kararı değildir.
- Korunacak düzeltme: Eri yüzünde yalnız iki oval göz, ben veya üçüncü nokta yok.

## Önerilen yeni ana kompozisyon

1. Eri ön planda büyük ve tanınır kalır; oyuncu avatarı veya silah kullanan savaşçı gibi sunulmaz.
2. Alt ön planda oyundaki sekizgen saksılardan oluşan küçük bir hasat alanı ve birkaç iri bitki görünür. Ayrıntılı HUD veya okunmayacak sayılar eklenmez.
3. Bitkiler arasında tek okunabilir hasat/elektrik zinciri kıvrımı sahneyi başlığa bağlar. Bütün efektler aynı anda doldurulmaz.
4. Uzak plandaki kırık turuncu gezegen mevcut kimliği korur. Eri'nin gezegenle ilgili hikâye twist'i açıkça resmedilmez.
5. ERIGONE, Bungee tabanlı tek bir ortak logo olarak çalışılır. Krem/turuncu ayrımı ve sert mürekkep gölgesi korunur; yalnız bir yerde küçük yaprak/kök detayı düşünülebilir. Harf okunurluğu bozulmaz.
6. Önce bu ana kompozisyon değerlendirilir; ardından aynı logo ve odaklar diğer oranlara yerleştirilir. Küçük kapsülde yan detaylar çıkarılır, Eri yüzü + logo kalır.

Bu öneri mevcut hasat, kart ve davranış zinciri dokümanlarına dayanır. Görsel vaat gerçek oynanışın dışına taşırılmamalı.

## Steam biçimleri

Resmî belge: https://partner.steamgames.com/doc/store/assets/standard

| Tür | Ölçü | Kullanım |
|---|---|---|
| Header | 920×430 | Mağaza üstü ve öneri alanları |
| Small | 462×174 | Arama ve liste sonuçları; 184×69 ve 120×45 küçültmeleri |
| Main | 1232×706 | Ana sayfanın öne çıkan karuseli |
| Vertical | 748×896 | Sezonluk indirim ve etkinlik vitrinleri |

## Üretim kaynakları

Yazısız çizimler yerleşik image_gen ile kullanıcının verdiği ERIGONE görselinden türetildi. Onaylı comic dili korundu. İstemler ve düzen bilgileri design.json içindedir. Başlıklar gerçek mevcut Bungee-Regular.ttf dosyasından System.Drawing ile çizildi; AI font taklidi kullanılmadı. İlk AI yazılı deneme ve benli ara çıktılar teslim PNG setine dahil edilmedi.

Font: Bungee Regular / mevcut proje dosyası. Kaynak: https://github.com/google/fonts/tree/main/ofl/bungee . Lisans SIL OFL 1.1; yerel Bungee-OFL.txt okundu. Ücret/royalty ve ekranda/jenerikte kredi zorunluluğu yok. Yalnız PNG çıktısı için font lisans metni zorunlu değil; font yazılımı dağıtılırsa telif ve OFL korunmalı. Font binary dosyası bu pakete eklenmedi.
