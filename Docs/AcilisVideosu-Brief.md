# Açılış videosu (10 sn): brief

28 Ekim 2026'da diyalog oturumundan devredildi. Bu işin planı: `Docs/DiyalogPlani/Diyalog-Tutorial-Plani-20261025.pdf`, Faz 5.

## Ne
Oyuncu ilk kez çiftçi/tırpan seçip run'ı başlattığında bir kez oynayan, yaklaşık 10 saniyelik açılış.
- **Atlanabilir.** "Görüldü" bilgisi kayıtta zaten hazır: `MetaSaveData.introSeen`, `DialogueProgress.MarkIntroSeen`.
- **Yazılar:** varsa görüntüye gömülmez; çeviri tablosundan gelir (`Assets/Resources/Dialogue/dialogue_text.csv`, 15–30 dil hedefi).

## Hikâye (gösterilecek / saklanacak)
- **Arka plan:** Eri mutasyona uğramış, sürekli aç bir bitki; gezegenlerin çekirdek enerjisiyle doyuyor. Dünya'ya geldi ve Dünya yok oldu.
- **İnsanların kaçışı:** bir grup insan gemiyle kaçtı ve uzayda kayboldu. Yakıtları bitti, bir kütlenin yörüngesine yakalanıp yüzeye indiler. "Şans eseri" orası Eri'nin ana gezegeni.
- **Açılışın akışı:** Dünya yok oluyor → gemi kaçıyor → yakıt bitiyor → yörünge → iniş. Sonra oyun Eri'nin "Oh! Bir gemi!…" konuşmasıyla sürüyor (T1).
- **SAKLANACAK:** Dünya'yı Eri'nin yok ettiği açıkça gösterilmez. Twist sonradan ipuçlarıyla ortaya çıkıyor. En çok çok ince bir ipucu olabilir (ör. bir an görünen yeşil bir parıltı); bu kullanıcının kararı.

## Kullanıcının durumu
- **Denemeler:** Blender ile birkaç video denedi (başka bir araçla). Sonuç "kopuk, kurgusu eksik" geldi; "animasyonu yapamayacağız" korkusu var.
- **Kayıt:** Blender 4.5.15 lisans kaydı `Docs/ThirdParty-Licenses.md` içinde (27 Ekim, `intro-25d/` önizleme klasörü).
- **Teşhis:** sorun büyük olasılıkla beceri değil kurgu. Sahne yazılmadan üretime geçilmiş.

## Yöntem (sırayla; her adım kullanıcının onayıyla)
1. **Mevcut denemeleri incele:** kullanıcının videolarını kare kare incele, neyin kopuk durduğunu somut söyle. Yollarını kullanıcıya sor.
2. **Çekim listesi:** 5–6 çekim, her biri süre + kamera + ses + bir sonrakine geçiş. Örnek süreler:
   - Dünya yok oluyor, 2 sn;
   - gemi kaçıyor, 2 sn;
   - yakıt bitiyor, 1,5 sn;
   - yörüngeye giriş, 2 sn;
   - iniş, 2,5 sn.

   Geçişler kopukluğu çözer.
3. **Animatik:** her çekimden tek durağan kare, doğru sürelerle (ve sesle) arka arkaya. Hiçbir şey canlandırılmadan kurgu sınanır.
4. **Üretim:** ancak animatik onaylanınca. Öneri: tam 3D yerine "hareketli çizgi roman".
   - Oyunun toon dilinde: kalın mürekkep, krem kâğıt, amber, Eri'nin mavi cam kubbesi.
   - Katmanlı 2D paneller, kamera kaymaları, sarsıntı, panel geçişleri.
   - Unity içinde mi oynasın, video dosyası mı olsun: kullanıcıyla karar verilecek.

## 1. adım bulguları (28 Ekim 2026)
Denemeler (hepsi 27 Ekim, Codex): `C:\Users\User\.codex\visualizations\2026\10\26\01a179e3-0ab1-7190-9eb3-a2e5db6d7a25\`
- `intro-preview/erigone-acilis-13sn.mp4`: 13 sn, sessiz, ayrı ayrı üretilmiş 2D resimler (3 karede bir yeni resim). Reddedilmiş.
- `intro-25d/Erigone-Demo-10sn.mp4`: 10 sn, Blender 4.5 toon, sesli, 6 çekim.
- `intro-25d/motion-v2/Erigone-Motion-v2.mp4`: 2. denemenin aynı kurgusu (kesmeler aynı karelerde: 1,708 / 3,208 / 4,208 / 5,5 / 7,5). Yalnız 48 fps, hız çizgisi, sarsıntı eklenmiş.

Teşhis (kurgu):
- Kesmelerde aynı anda arka plan rengi, ölçek, konu ve kamera değişiyor; çekimden çekime hiçbir şey taşınmıyor (yön, şekil, renk, ses köprüsü yok).
- Halkalar eksik ya da çift. Dünya'nın "önce" hâli yok. Yakıt 2–3,5 sn sürüyor ama gösterge çekimi yeni bilgi vermiyor. Yörünge ya yok (13 sn) ya da gezegen kaya gibi okunuyor (10 sn). 10 sn'lik deneme iniş göstermeden boş bulutlarla bitiyor. 13 sn'likte hedef gezegen Dünya'ya benziyor.
- 13 sn: roket 2,25–4,2 sn arası kadrajda duruyor; roket tasarımı çekimden çekime değişiyor.
- 10 sn / v2: model tutarlı (Blender'ın kazancı) ama mürekkep 720p'de çok ince, oyunun kalın-mürekkep dilinden çok düz vektör gibi duruyor. v2'deki akıcı 48 fps anime hissinin tersi.
- Ses (v2) kurgudan iyi: 5,75 sn'de motor susuyor, rüzgâr yükseliyor, 9,25 sn'de darbe var. Ancak darbe anında görüntü boş bulut.

Kullanıcı kararı (28 Ekim): üretim **Blender'da 3D toon**, film/anime kurgusu, stilize. Bu karar, yukarıdaki "hareketli çizgi roman" önerisinin yerine geçer. Toon dili ve kalın mürekkep yine `Docs/UI-Art-Direction.md`'den gelir.

## 2.–3. adım (28 Ekim 2026)
Çekim listesi: `Docs/AcilisVideosu-CekimListesi.md` (taslak 2). Liste, diyalog planındaki "Oyna → video → seçim" sırasına göre kuruldu.
- **v1:** panel animatiği, `Docs/AcilisVideosu/Animatik/`. Kullanıcı geri bildirimi: renkler cansız ve 2D; kamera açısı ve sarsıntı eksik; Dünya'nın yok oluşu yakından görünmeli, Dünya'dan bir şey fırlamalı (o şey rokettir); vurgular taslakta da cilayla desteklenmeli; kurguda sorun var.
- **v2:** hareketli previs, `Docs/AcilisVideosu/Animatik/v2/`. Banner paleti, tasarlanmış renkli toon ışık, her karede kamera ve sarsıntı, plakalara ayrılan 3D Dünya, her vurguya cila. 10,5 sn. Kullanıcı geri bildirimi: Dünya'dan uzaklaşma hissedilmiyor; roket yatay görünmek zorunda değil (rokete yapışık kamera, geriye akan şeyler); cilaya çok ihtiyaç var; acelecilik bitsin; fikir animasyonla geçsin; "AI slop" hissinden kaçılsın; sarsıntı da hissi desteklesin.
- **v3:** `Docs/AcilisVideosu/Animatik/v3/`, 14,75 sn. Yeni Ç2a (roket buluttan kameraya fırlar) ve Ç2b (rokete yapışık kamera, küçülen Dünya, arkadan yetişen şok dalgası); tasarlanmış cila; anlatan sarsıntı. v2'de "ikişer kare" ayarının uygulanmadığı bulundu, v3'te düzeltildi. v3.1–3.2'de hareket sürekliliği ölçülüp düzeltildi. Kullanıcı geri bildirimi: olaydan olaya atlamalar bağlanmamış, fena CG kokuyor, oyunun toon yapısında kalmalı.
- **v4:** `Docs/AcilisVideosu/Animatik/v4/`. Freestyle fırça çizgisi, gölgede tram, parlak ton yok, el yapımı düzensizlik. Geçişler olay bağı: duman → bulut, lomboz → gösterge, bulut tabakasına dalış, perdenin açılması. Kullanıcı: geçişler hâlâ başka bir video gibi, tek bütün yok; cartoon olmuş, toonic olmalı.
- **v5:** `Docs/AcilisVideosu/Animatik/v5/`. Tek dünya ve kesintisiz kamera; yalnız iki görünmez kesme var (lomboz camı, bulutun içi). Oyunun kendi toon'u (`ThornComicToon.shader`: üç basamak, sabit mürekkep kabuğu, yüzeyli biçimler). Kullanıcı kurguyu "awesome" buldu.
- **v5.2–v5.3 (28 Ekim):** `Docs/AcilisVideosu/Animatik/v5_1080/`, 1920×1080 (`build_intro_v5.py -- all hd`).
  - v5.2: vurgu cilası eklendi (çarpma pofu, kıvılcım, buhar, lomboz göz kırpması, kaçış duman izi, kor).
  - Higgsfield denemeleri (`Docs/AcilisVideosu/Higgsfield-Deneme/`): video düzenleme modelleri bizim toon'u iyileştirmedi. Kullanıcı kararı: Blender devam.
  - v5.3: GPT Image 2.5 stil karesi hedef alınarak boyalı lacivert bulutsu, fırça mürekkebi, anime alev yıldızı ve gök parıltıları eklendi. Kullanıcı: "güzel olmuş".
  - v5.4: kullanıcı iki şeyi kötü buldu: yakıt bitince kameranın uzaklaşması ve uzaktan gezegenin bulutlu görünüşü.
    - Kamera artık roketin arkasına ve yukarısına çekiliyor, roketle gezegeni birlikte kadraja alıyor. Roket orta boyda kalıyor, gezegen 208'den itibaren görünüyor.
    - Gezegeni kaplayan 150 bulut topağı kaldırıldı. Yerlerine ince bir kabuğa boyanmış, mürekkep kenarlı, diskin üzerinden eğik geçen bantlar geldi. Giriş tabakası alçak ve yassı.
    - Yan kazanç: yeşil ipucu parıltısı artık bulutların üstünde, ipuçlu çeşit çalışıyor. Önceden bulut topaklarının altında kalıyordu.
    - Kullanıcı onayı bekliyor.

## Kurallar (proje genelinde)
- **Yanıt dili:** Türkçe.
- **Dürüst durum:** yazıldı / test edildi / görüldü / onaylandı ayrı söylenir.
- **Dış içerik:** her dış içerik (font, ses, model, müzik, araç) KULLANMADAN önce lisansı kontrol edilir ve konuşmada açıkça bildirilir (`AGENTS.md`). Kayıt `Docs/ThirdParty-Licenses.md` dosyasına.
- **Görsel dil:** `Docs/UI-Art-Direction.md`. Toon stil; elektrik dışı efektlerde mavi yok; hit-stop yok.
- **Performans ve sızıntı:** havuz, sınır, karede bellek ayırma yok, temizlik; sonuç raporlanır.
- **Doğrulama:** Unity doğrulaması izole kopyada (`Tools/run-isolated-verification.ps1`). Toplu Unity yükü sınırlı; kullanıcı aynı PC'de çalışıyor.
- **Araçlar:** Superpowers ve worktree kullanılmaz.
- **Commit:** kullanıcı istemeden commit yok.

## Bitti sayılır
Önce çekim listesi ve animatik kullanıcının onayından geçer. Üretim, Unity'ye bağlama ve test bundan sonra.
