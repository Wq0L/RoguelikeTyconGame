# Erigone — Yayın öncesi inceleme paneli

Yalnız planlama ve kod/proje incelemesi. Unity kaynaklarına yazmaz, test veya build çalıştırmaz, issue açmaz. İnceleme anlık görüntüsü: **7 Kasım 2026**, HEAD `7ae3821d` + çalışma ağacı. 21 kontrol, son 6 commit, dosya bazında sistem karşılaştırması, 50 round haritası, dil kilometre taşları ve varlık bağlantıları.

## Açma

**Tek dosya sürümü:** `dist/Erigone-Release-Desk.html` doğrudan tarayıcıda açılabilir; sunucu gerekmez. `dist/index.html` de yanındaki JS/CSS dosyalarıyla açılır. Daha tutarlı tarayıcı depolaması ve kopyalama desteği için bu klasörde mevcut Node ile:

```powershell
node serve.mjs
```

Adres: http://127.0.0.1:4173 . Sunucuyu kapatmak için Ctrl+C. Hiçbir bağımlılık kurulmaz.

## Güncelleme ve yedek

- Kontrol listesi → kayıt → **Kontrolü düzenle**: durum, öncelik, kaynak yolları, bulgu, düzeltme ve doğrulama adımları değiştirilebilir. Yeni kontrol eklenebilir.
- Kayıtlar yalnız aynı tarayıcı/origin'in localStorage alanında tutulur. Tarayıcı verisi temizlenirse silinir. Cihazlar arasında kendiliğinden eşitlenmez.
- **JSON yedeği** tüm kontrolleri, commit anlık görüntüsünü ve son 100 düzenleme olayını indirir. **İçe aktar** önce önizleme sunar, uygulanınca mevcut kayıtların yerini alır.
- **Görünümü CSV indir** filtrelenmiş kontrolleri dışa aktarır; CSV içe aktarımı yoktur.
- Kaynak anlık görüntüsünü yenilemeden önce paneldeki düzenlemeleri JSON olarak yedekle. `audit-game.json` ve `audit-review.json` içindeki bulgular manuel inceleme kayıtlarıdır; kod değişince kendi kendine düzelmez.
- Kaynakları tekrar incele, audit JSON kayıtlarını güncelle, sonra bu klasörde `python collect-snapshot.py` çalıştır. Betik git/CSV/yolları salt okunur inceler ve yalnız panelin `dist/snapshot.json` ve `dist/data.js` dosyalarını yazar. Yeni kaynak JSON'unu panelde **İçe aktar** ile yükle. Eski tarayıcı düzenlemeleri otomatik üstüne yazılmaz.
- İnceleme statüleri: **Kanıt var** kod bağlantısını gösterir; **Doğrulandı** için gerçek doğrulama sonucu gerekir. Kritik filtre kapatılmamış P0/P1 kayıtlarını içerir. Issue bağlantısı gerektirmez.
- Kaynak güncellemesinden sonra tek dosya sürümünü yeniden üretmek için `node package-portable.mjs` çalıştır. Dosya ve localhost sürümleri farklı tarayıcı depolama alanlarıdır; aralarında JSON ile taşı.

## Doğrulama sınırı

Unity/Play Mode/oyuncu build testleri bu görevde çalıştırılmadı. Eski raporlar tarihli kaynak olarak kullanıldı. CSV alan denetimi Unity `LocalizationVerification` yerine geçmez. Web sayfası geliştirici aracıdır; oyuncuya yazı eklemez ve `GameText.csv` değiştirilmez. Canlı GitHub issue listesi sorgulanmadı. GitHub bağlantıları yerel remote/commit bilgisinden oluşturulur; uzak erişim ayrıca doğrulanmadı.

Panel kontrolü: `node --test check.mjs`. JS sözdizimi: `node --check dist/app.js`.

## Sites yayını

Sites becerisi izlendi; `create_site` çağrısı **“MCP tool codex_apps/sites.create_site is not available to the model”** yanıtı verdi. Plugin Management araması da aynı erişilebilirlik hatasını döndürdü. Site oluşturulmadı, kaynak Git sunucusuna gönderilmedi, yayın URL'si yok. Bu bir kullanıcı onayı reddi değildir.

`.openai/hosting.json` statik `dist` çıktısını tanımlar; gerçek Site kimliği henüz yoktur. Sites erişimi geldiğinde yeni Site bir kez oluşturulmalı, dönen kimlik `set-project-id.mjs` ile korunmalı, kaynak helper ile senkronize edilip özel yayın yapılmalıdır. Kimlik uydurulmamalı.

## Kaynak / lisans

Bu panel için yeni üçüncü taraf font, görsel, ikon, kod, paket veya araç indirilmedi/kurulmadı. HTML/CSS/JS, veri toplama betiği ve testler bu görevde özgün yazıldı. Sistem fontları kullanılır; font dosyası dağıtılmaz. Görseller kopyalanmadı; varlıklar dosya yollarıyla referanslanır. Mevcut lisans koşulları sadece `Docs/ThirdParty-Licenses.md` kaydına atfedilerek özetlenir; yeni hukuki doğrulama iddiası yoktur.
