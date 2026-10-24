# Kritik Ark — 12 Ekim 2026

## 23 Ekim 2026 güncellemesi: altın kavis

Camgöbeği ışın + krem/altın temas yıldızı (0,12 sn) oyuncu testinde okunmadı ve elektrik gibi göründü.
Yerine `LateGamePresenter.CritArc.cs` geldi:

- Kritik yiyen bitkiden hedefe zıplayan, mürekkep çizgili altın kavis. Kritik sayısıyla aynı altın,
  böylece "kritik sıçradı" diye okunur. Hedefe doğru kalınlaşır; üstünde ince krem ışık çizgisi var.
- Hedefte 8 köşeli altın iniş yıldızı; kameraya döner.
- Yay bir anda tam belirir, 0,3 saniyede `(1 - t)^0,7` ile incelerek söner. Hasar cue gelmeden
  uygulanmıştır, görsel hasarı geciktirmez. Yükseklik `(0,55 + 0,25 × mesafe) × 0,75`.
- Havuzlu, en çok 24 yay aynı anda; fazlası çizilmez, `CritArcsSkipped` sayılır. Pause'da yaş
  ilerlemez; round dışında, restart'ta ve presenter kapanınca temizlenir.
- Malzeme `Resources/ParticleVertexColor` shader'ından çalışma anında üretilir. Dış varlık yok.
- Mekanik ve ses değişmedi. Taşan Hasar kendi ArcBolt yolunu korur. Katalogdaki eski CritArc
  varyant bağları (ArcBolt / AntennaContact) artık kullanılmıyor, silinmedi.

Seçim süreci: izole kopyada mevcut hal, altın zikzak ve altın kavis karşılaştırıldı; kullanıcı
kavisi seçti. Ardından yay %25 alçaltıldı, kalınlık %8 azaltıldı.

Kontrol: `CritArcArtVerification` 15/15 PASS (yeni görsele göre güncellendi),
`OverflowArtVerification` 17/17 PASS. İnsan Play testi kullanıcıda.

---

Üretim tamamlandı; ayrıntılı görsel/işitsel kabul sonraya bırakıldı.

Gerçek doğrudan kritik saldırının sonunda, doğrudan vurulmamış ve aynı saldırıda ark
almamış en yakın canlı bitkiye gider. 2 hücre menzil, hesaplanmış kritik hasarın %30’u,
saldırı başına 3 sınırı aynen korunur. Ark Reward hasarıdır; yeni kritik/ark üretmez.
Ödül ve effect asset’leri değiştirilmedi.

Sunum yalnız başarılı StrikeResult.Hit sonrasında oluşur. Kaynak konumu hasardan önce
PlayerController tarafından DirectHit.Point içine değer olarak alınır. Hedef, Strike’ın
hasar öncesi yakaladığı Point’tir. Görsel bitki referansı tutmaz. Kaynak/hedefin üzerine
aynı .65 dünya birimi çizim yüksekliği eklenir; saksı kenarı ışını örtmez. 3D yön korunur.

Yeniden kullanılan: AntennaSnap cyan/krem köşeli tek ışın prefab/mesh’i, ContactDouble
krem/altın temas yıldızı, ThornToon malzemesi; LateGamePresenter dekor havuzu ve merkezi
LateGameAudio. Yeni mesh/prefab/shader gerekmedi. İki parça .12 saniyede incelip kaybolur.
Mevcut bitki vurulma tepkisi veya hasar sayısı tekrar tetiklenmez.

Yeni: özgün CritArcClick.wav (.12 sn, mono 22050 Hz PCM), Python stdlib sentez kaynağı.
Ses düzeyi .32, tekrar aralığı .07 sn, klip başına en çok 2 ses; ortak toplam sınır 8.
Her başarılı ark tek ses isteği verir. Görsel kareleri ses üretmez.

CritArc=18 enum sonuna eklendi. ArcBolt ve AntennaContact yalnız bu varyantta bağlandı;
Default ArcBolt ve Taşan Hasar yolu değişmedi. Yeni Update/coroutine/ışık/fizik/materyal
örneği yok. Dekorlar ortak 96 sınırını kullanır. Pause ve round/restart temizliği merkezidir.

Değişen kod: AttackFeed.cs, PlayerController.cs (yalnız konum snapshot eklemesi),
CritArcEffectSO.cs (cue kaynağı), LateGameArtSO.cs (varyant), LateGamePresenter.cs (sunum).
Katalog: Assets/Resources/LateGameArt.asset. Yeni ses: Assets/Art/LateGame/CritArc/.
Üretim: Tools/Art/build-crit-arc-audio.py, Tools/Verification/Editor/CritArcArtBuilder.cs.
Kontrol: Tools/Verification/Editor/CritArcArtVerification.cs; mevcut GecOyunV1Verification
Lab/CritArc kontrollerini yeniden kullanır, yeni test sahnesi yok.

## Hızlı Play
GameScene / Run50_GecOyunV1’de Play aç. Tools > Geç Oyun Testi içinde “Kritik Ark” ara
ve Ver’e bas. Oyuncunun kritik şansı sıfırsa önce kritik şansı sağlayan mevcut güçleri al;
ödül koşulu atlanmaz. Yan yana canlı bitkiler bırak ve küçük alanla tek bitkiye vur.
Kritik gerçekleştiğinde komşuya kısa cyan ark ve krem/altın temas görünür, çıt duyulur.
Saldırının bütün komşuları doğrudan vurması veya uygun hedef kalmaması ark üretmez.
Test komutu kalıcı kayıt yazımını oturum için kapatır.

Temsilci normal kamera görüntüsü CritArc_NormalCamera.png; insan Play onayı değildir.
Uzun bot denge veya kapsamlı kamera/ekran oranı/ses kabul koşusu yapılmadı.

## Temel kontrol sonucu
Derleme başarılı, Unity çıkış kodu 0; 11 kontrol geçti (ilk kontrol mevcut Kritik Ark’ın
3 mekanik assert’ini çalıştırır). Kaynak hasadı, gerçek 3D hedef, .12 sn ömür, pause,
12 yeniden kullanımda sabit havuz nesne sayısı, restart temizliği ve uygun hedef yokken
sahte efekt oluşmaması doğrulandı. Katalog farkı yalnız 2 görsel + 1 ses kaydıdır;
önceki bağların tamamı değişmeden kaldı. Test raporu CritArcArtVerification.txt.
Unity kapanışında kendi SearchDatabase/IndexationOnStartup kodundan indeks istisnası
kaydedildi; oyun/ark çağrı zincirinden değil. İnsan Play ses miks onayı henüz yapılmadı.
