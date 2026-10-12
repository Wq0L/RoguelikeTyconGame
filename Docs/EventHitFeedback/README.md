# Diken / Filiz vuruş okunurluğu — 8 Ekim 2026

Kullanıcı oyun sırasında hasarın anlaşılmadığını bildirdi. Her iki ana hedef artık sıradan bitkilerin
VFXManager.PlayHitFlash sistemini kullanır: 0.1 saniye beyaz yüzey parlaması, koyu/kırmızı konturun korunması.
ThornComicToon shader'ına mevcut `_ToonFlash` / `_ToonFlashColor` protokolü eklendi; normal durumda renk aynı.

Ezilme ilk .20 saniyede %32 dikey basılma ve %22 genişleme, sonraki .12 saniyede küçük geri yaylanma uygular.
Yalnız görsel çocuk etkilenir; root ve hedef hücresi sabittir. Darbe kırıntılarının sunum ölçeği .5'ten .85'e çıkarıldı.
Tekrar vuruş mevcut tepkiyi yeniden başlatır; renderer listesi ilk vuruşta önbelleklenir. Ek mesh, materyal örneği,
renderer, Update veya coroutine eklenmedi. Model poligon sayıları değişmedi.

Pause oyun zamanını durdurur. Havuz dönüşü ve Show beyaz flash'ı ortak yöneticiden iptal ederek özgün property
block'u geri yükler. Dayanıklılık, süre, yayılma, yavaşlatma, hasar veya gerçek kayıt/profile dokunulmadı.
Dış içerik kullanılmadı. Commit yok.


## Nihai doğrulama

- Diken 41 kontrol: gerçek vuruş beyaz flash'ı, ezilme, kırılma, çıkış sarsıntısı, pause ve yeniden kullanım geçti.
- Filiz 37 kontrol: gerçek vuruş flash'ı, ezilme, pause'da flash'ın korunması, havuzda temizlenmesi, yayılma,
  anında yavaşlatma kaldırma ve sıralı solma geçti. Son .20/.12 sn ezilme ayarıyla tekrar çalıştırıldı (exit 0).
- Dolu GameScene'de normal kamera görüntüleri: Thorn_03_Hit.png ve Sprout_06_Hit.png.
- Diken'in kısa darbe görüntüsü örneklenirken editör ekran yakalama gecikmelerini ayırmak için testte
  1/60 sn sabit simülasyon adımı kullanılır; oyun veya asset süreleri değiştirilmez.
- Önceki Filiz doğrulama denemesi lisans bağlantısında yarım kalmıştı; eski PASS sonucu kullanılmadı,
  son kaynaklarla yeni Unity oturumu açılıp doğrulama tamamlandı.

İnsan Play: tek vuruşta beyaz flash ve ezilmenin okunması, seri vuruşta tepkinin yeniden başlaması,
son vuruşun kırılmaya bağlanması ve kırmızı konturun korunması kontrol edilmeli.
