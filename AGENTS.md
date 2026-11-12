# Proje talimatları

## Zorunlu dış kaynak ve lisans kuralı

Bu kural kullanıcının açık talebidir ve proje genelinde geçerlidir.

- İnternetten alınacak her yeni font, görsel, ses, model, animasyon, ikon, kod,
  paket, eklenti, araç veya diğer üçüncü taraf içerik için lisans, projede
  kullanılmadan veya bağımlılık olarak kurulmadan önce kontrol edilmelidir.
- “Ücretsiz”, “açık kaynak” veya bir paket deposunda bulunması tek başına izin
  sayılmaz. İlgili sürümün resmî lisans metni ve varsa varlığa özel koşulları
  incelenmelidir: ticari kullanım, değiştirme, oyuna gömme/dağıtma, atıf,
  telif bildirimi, adlandırma ve kaynak paylaşma yükümlülükleri.
- Kullanıcıya kullanımdan ÖNCE mutlaka açıkça bildirilmelidir: alınacak içeriğin
  adı ve kaynağı, lisans adı/sürümü, kullanım amacı ve projeyi etkileyen koşullar.
  Bildirimi yalnızca dosyaya, araç çıktısına veya commit mesajına yazmak yetmez;
  konuşmada da görünür biçimde açıklanmalıdır.
- Kullanıcı ücretsiz ticari kullanım istiyor. Her bildirimde ücret/royalty,
  oyun ekranında veya jenerikte kredi zorunluluğu ve dağıtım paketinde lisans/telif
  metni bulundurma zorunluluğu ayrı ayrı açıklanır. Ücretli lisans veya satış başına
  ödeme gerektiren içerik, kullanıcı bunu ayrıca kabul etmeden seçilmez.
- Lisans bulunamıyorsa, belirsizse veya amaçlanan kullanıma uygun değilse içerik
  projeye eklenmemeli/kullanılmamalıdır. Durum kullanıcıya açıklanmalıdır.
- Mevcut üçüncü taraf içerikten türev üretilirken de kaynak ve lisans açıklanır.
  Türev içerik sıfırdan özgün üretilmiş gibi sunulmaz. Gerekiyorsa isim değiştirilir;
  özgün telif, lisans ve NOTICE dosyaları korunur ve dağıtımda taşınır.
- Eklenen içeriğin kaynağı, sürümü, lisansı, kontrol edilen belge ve yükümlülükleri
  `Docs/ThirdParty-Licenses.md` dosyasına kaydedilir. Geliştirme araçları ile
  oyunda dağıtılan varlıklar ayrı belirtilir.
- Bu kural bildirim zorunluluğudur; kullanıcı ayrıca istemedikçe her uygun
  lisanslı içerik için tekrarlanan onay talebi oluşturmaz. Bildirim atlanamaz.

## Zorunlu oyun yazısı (çeviri listesi) kuralı

Bu kural kullanıcının açık talebidir (5 Kasım 2026) ve proje genelinde geçerlidir.
Ayrıntılar: `Docs/Ceviri/Yerellestirme.md`.

- **Oyuncunun gördüğü her yeni yazı** ana listeye eklenir:
  `Assets/Localization/GameText.csv`. Kapsam: düğme, başlık, ipucu, tooltip,
  bildirim, kart, diyalog, veri adı ve açıklaması.
- Yazı koda, sahneye, prefab'a ya da asset alanına **düz metin olarak bırakılmaz**.
  - Koddaki yazı `GameText.Get` / `GameText.Format` ile okunur.
  - Sahnedeki ya da koddan kurulan etiket `LocalizedText` (veya `LocalizedText.Bind`)
    ile bağlanır.
  - Veri adları asset'in çeviri özelliğinden okunur: `DisplayName` benzeri,
    `GameText.Data` kullanır.
- Her satırda üç alan doldurulur:
  - `en`: kaynak dil, boş kalamaz;
  - `tr`;
  - `note`: çevirmen notu. Yazının nerede göründüğünü ve her yer tutucunun ne
    olduğunu söyler.
- Cümle parça parça birleştirilmez. Değişken yer tutucuyla (`{0}`) tek anahtarda
  yazılır. Sayıya göre değişen İngilizce çoğul `{0:plural:{} round|{} rounds}`
  biçimiyle yazılır.
- Hücrede satır sonu kullanılmaz; gerekirse `<br>` kullanılır. Renk ve boyut
  etiketleri mümkünse kodda kalır.
- Sayılar dile göre biçimlenir: `GameText.Number` ve `GameText.Culture`. Binlik
  ayırıcı için `GameText.GroupSeparator` kullanılır.
- Dil değişince yazı yenilenmeli: `LocalizedText` ya da `GameText.Changed`.
  Karede yazı kurulmaz.
- Yeni yazı ekleyen iş bitince `LocalizationVerification` çalıştırılır. Kapsam
  kontrolü koddaki her anahtarın ve oyuna giren her veri adının listede olduğunu,
  İngilizce sütunda Türkçe harf kalmadığını denetler. Sonuç kullanıcıya bildirilir.
- Yalnız geliştiriciye görünen yazılar (log, doğrulama mesajı, test penceresi,
  Mechanics Test Lab) bu kurala tabi değildir.

## Görsel tasarım

UI çalışmaları için `Docs/UI-Art-Direction.md` esas alınır. Ortak comic dil,
her öğenin birebir aynı şekil ve detaylara sahip olması anlamına gelmez.

## Zorunlu editörde düzenleme ve önizleme kuralı

Bu kural kullanıcının açık talebidir (11 Kasım 2026). UI ve görsel yerleşim
işlerinde, MCP ile yapılan değişiklikler dahil, proje genelinde geçerlidir.

- **Görünüm Play'e basmadan editörde incelenebilmeli ve düzenlenebilmelidir.**
  Yalnız oyun başlayınca görünen bir tasarım, editör tarafı tamamlanmadan
  bitmiş iş olarak sunulmaz.
- Sabit görsel iskelet, hiyerarşi, konum, boyut, anchor, boşluk, font, sprite
  ve materyal tercihleri sahne, prefab veya uygun serileştirilmiş tasarım
  verisinde tutulur. Bunları değiştirmek için C# kodu düzenlemek gerekmemelidir.
- `Awake`, `Start`, `OnEnable` veya runtime bootstrap içinde, editörde
  kaydedilmiş tasarımı sabit değerlerle yeniden yazan görünüm yamaları eklenmez.
  Kod veri bağlama, oyun durumuna göre görünürlük ve durum değişimi, etkileşim
  ve animasyonu yönetir. Duruma veya ekran boyutuna bağlı yerleşim gerekiyorsa
  ayarları editörden yönetilir ve aynı kurallar önizlemede de kullanılır.
- Dinamik kart, liste ve yetenek ağacı gibi veriden üretilen arayüzlerde
  düzenlenebilir görsel prefab'lar kullanılır. Editörde örnek/veri tabanlı
  önizleme veya açık bir "Oluştur / Güncelle" aracı sağlanır. Önizleme ile
  oyun aynı tasarım verisini ve yerleşim hesabını kullanır; iki ayrı tasarım
  sürümü oluşturulmaz. Dinamik içeriğin runtime'da üretilmesi yasak değildir.
- Editör önizlemesi oyun manager'larının çalışmasına, aktif bir run'a veya
  satın alma/kayıt işlemlerine ihtiyaç duymaz; oyun ilerlemesini değiştirmez.
  `[ExecuteAlways]` mevcut oyun sınıflarına topluca eklenerek çözüm üretilmez;
  editör işlemleri ile oyun davranışı açıkça ayrılır.
- Oluşturma/güncelleme aracı tekrar çalıştırıldığında nesne, bileşen, materyal
  veya olay dinleyicisi çoğaltmaz. Yalnız yönettiği öğeleri günceller; mevcut
  referansları ve kullanıcı düzenlemelerini korur. Kalıcı editör değişiklikleri
  Undo ve sahne/prefab kayıt akışına uygun yapılır; geçici önizlemeler temizlenir.
- MCP ile yapılan kalıcı görsel düzenlemeler Edit Mode'da doğru sahne/prefab'a
  uygulanıp kaydedilir. Play Mode'da yapılan geçici değişiklikler veya yalnız
  ekran görüntüsü alınması kalıcı uygulama sayılmaz.
- UI işi tamamlanırken hem Edit Mode görünümü hem Play Mode davranışı kontrol
  edilir: kaydedilmiş görünüm yeniden açıldığında korunmalı, oyun başladığında
  beklenmedik biçimde değişmemeli; panel aç/kapa ve önizleme yenilemede çoğalma
  olmamalıdır. Dil değişimi ve ilgili ekran boyutları da kontrol edilir.
  Yapılmayan doğrulamalar ve kalan eksikler kullanıcıya açıkça bildirilir.
- Görsel kurulum/yerleşim, veri bağlama ve oyun kuralları ayrı sorumluluklar
  olarak tutulur. Mevcut runtime ağırlıklı ekranlar, ilgili ekran üzerinde
  çalışılırken bu yapıya aşamalı taşınır; bu kural tek başına tüm projeyi
  yeniden yazma talimatı değildir. Ölçüler ve tamamlanma notları ilgili UI
  dokümanlarında son uygulamayla tutarlı tutulur.
