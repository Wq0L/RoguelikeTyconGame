# Panel doğrulama kaydı — 7 Kasım 2026

- Node otomatik kontrolleri: **8/8 geçti**. Anlık görüntü şeması, kritik filtre, önem sırası, dosya araması, güvenli metin/CSV, bozuk JSON ve yedek round-trip.
- Tarayıcı: genel bakış ve 21 kontrol render edildi; kritik filtre 15 kayıt gösterdi. IntroVideoPlayer araması + issue bağlantısı yok filtresi 2 kayıt verdi.
- Tarayıcı kayıt deneyi: intro kontrolü P1 → P2 değiştirildi, kritik aramada 2 → 1 sonuç oldu; sayfa yeniden açılınca P2 korundu. Test değişikliği daha sonra kaynak JSON'u içe aktarılarak geri alındı. Teslim edilen kaynakta ve test edilen panelde P1.
- JSON içe aktarma: 21 kontrol / 6 commit önizlemesi görüldü, uygulandı. JSON indirme ve CSV üretimi kod/otomatik test kapsamında incelendi; tarayıcı indirme dosyası ayrıca açılmadı.
- Commit görünümü: DialogueProgress araması doğru dosyayı ve +171 / −21 farkını gösterdi.
- Round haritası: R50 seçimi doğru ayrıntıyı gösterdi.
- Dar ekran: 390 px hedef görünümde başlık, kartlar ve yatay menü incelendi; belge yatay taşmıyordu. Başlık alanı iyileştirilip yeniden görüldü. Son panel görüntüsü `preview.jpg`.
- Tek dosya sürümü `dist/Erigone-Release-Desk.html` üretildi. Uygulama içi tarayıcı file:// protokolünü engellediği için bu dosya doğrudan o tarayıcıda açılamadı; görsel/etkileşim kontrolleri aynı kaynak dosyaların localhost sürümünde yapıldı. Kullanıcı dosyayı normal tarayıcısında açabilir.
- Kaynak denetimi: 21 kontrolün tüm kaynak yolları diskte bulundu. CSV'de 747 satır; boş en/tr/note, yinelenen anahtar ve İngilizce sütunda Türkçe harf: 0.
- Unity çalıştırılmadı; sahne/asset/script/paket/ayar dosyalarına yazılmadı. Git durum listesinde başlangıçtaki oyun değişiklikleri korundu; bu görevin yeni dosyaları yalnız `Sites/erigone-release-desk` altında.
- Sites oluşturma ve son durum listeleme araçları erişilemiyor yanıtı verdi. Site kimliği, kaynak push'u, yayın sürümü veya canlı URL oluşmadı. Yerel panel tamam; Sites yayını engelli.
