# Round müziği

SoundManager GameScene içinde kayıtlıdır; menüdeki geçici manager sahneye
geçerken yerini bu nesneye bırakır.
RoundManager ses bileşeni kurmaz; SoundManager onun olaylarını dinler. Tek parça
aktif round boyunca loop eder; round arası/seçim/shop ve duraklamada Pause,
sonraki aktif round'da UnPause kullanılır. Böylece kısa round'lar parçanın
girişini tekrar tekrar başlatmaz. Yeni run ve ana menü parçayı sıfırlar.
Müzik seviyesi mevcut GameSettings.MusicVolume ayarını izler; oyun zamanı
yavaşlayınca pitch değişmez.

Havuz: GameScene > SoundManager > Round Music Pool > Entries. Her girdinin
`clip`, `resourcePath`, `weight`, `minPower` ve `maxPower` alanı vardır. Clip atanmışsa doğrudan kullanılır.
Sahne havuzu boşsa `Assets/Resources/Music/RoundMusicPool.json` yedeği okunur.
Pozitif, sonlu ağırlığı ve yüklenebilir
klibi olan girdiler seçilir. Seçim olasılığı ağırlık / toplam ağırlıktır.
Oyun ekonomisinin RNG'sinden bağımsız System.Random kullanılır.
Şimdilik tek girdi `Music/RoundMusic`, ağırlık 1: %100 seçilir ve Unity'nin
yerel loop özelliğiyle tekrar eder. Birden çok geçerli girdide mevcut parça
tamamlanınca yeni seçim yapılır; tekrar seçilmesi mümkündür.
Geçerli kaynak yoksa
oyun sessiz devam eder. Yeni paket veya mixer bağımlılığı yoktur.

Kullanıcının sağladığı nene / Boss Battle #3 V1.wav, CC0 1.0 doğrulaması ve
konuşma bildirimi ardından `Assets/Resources/Music/RoundMusic.wav` olarak
değiştirilmeden eklendi. Kaydı ThirdParty-Licenses.md içindedir.
RoundMusicImporter bu klasördeki sesleri Streaming / Vorbis 0.85, özgün
örnekleme hızı ve stereo korunarak içe aktarır; preload kapalıdır.
SoundManager ana sesi AudioListener üzerinden, müzik sesini GameSettings.MusicVolume
üzerinden uygulanır; EffectsVolume müziği etkilemez. Ayarlar menüsünün
mevcut Music/Effects slider'ları ve PlayerPrefs kayıtları kullanılır.

Boss ödülü başarıyla alındığında BossRewardManager.MusicPower artar. Projede
boss ödüllerinin ayrı nadirliği yoktur: ödülün havuzdaki kendi aşaması +1, +2,
+3… puan verir (geç round'da erken ödül almak yine +1). Aşamasız ödül +1 verir.
BossRewardSO.musicPowerOverride pozitifse bu özel puan kullanılır; 0 otomatik
aşama hesabıdır. Stack başına eklenir; iptal, önizleme, başarısız alım ve boş
teklif puan vermez. Yeni run ve ana menüde ClearAll puanı sıfırlar.

Yeni müzik ekleme: GameScene > SoundManager > Round Music Pool > Entries
listesine satır ekle, Clip ata; Min Power / Max Power ve Weight değerlerini gir.
Örnek: sakin 0–4, hareketli 5–9, yoğun 10–0 (Max Power 0 = üst sınır yok).
Sınırlar dahildir. Weight yalnız uygun aralıktaki parçaların göreli olasılığıdır;
1 ve 3, %25 ve %75 anlamına gelir. Aralıklar örtüşebilir. Hiç uygun parça yoksa
aralık dışından müzik seçilmez, mevcut parça bittikten sonra sessizlik olur;
bu nedenle 0'dan sonsuza kadar aralıkları kapsa. Weight 0 parçayı devre dışı bırakır.
Mevcut tek parça Min Power 0 / Max Power 0 / Weight 1 ile her puanda çalışır.
Yeni parçalar eklerken onun aralığını da daralt; aksi halde her puanda aday kalır.

Puan değişimi çalan parçayı kesmez. Yeni puan ancak parça tamamlandığında
okunur. Tek adaylı bir güç bandı da tamamlanınca yeniden değerlendirilir;
native loop yalnız havuzda tek, sınırsız parça varsa kullanılır. Böylece
bir bandın tek parçası güç artınca sonsuza kadar loop'ta takılmaz.

Kontrol: SoundManager refactor'ü dahil oyun C# kaynakları, kurulu Unity
6000.3.14f1 referanslarıyla Roslyn üzerinden derlendi (0 hata).
Kaynak/hedef WAV SHA-256 eşitliği önceki kurulumda doğrulandı.
Unity Editor import ve sahne kurulumunu tamamladı. Menüden GameScene'e geçiş,
loop, shop pause/resume, ses kısma Play Mode kontrolü geçti; işitsel kalite
değerlendirmesi yapılmadı. Sonuç: Docs/Audio/PlayModeVerification.txt.

Unity'de kontrol: round başlat; parça bitene kadar aktif oyun süresi biriktir;
round arası ve pause sırasında sessizliği, sonraki round'da kaldığı yerden
devamı, müzik ses ayarını, yeni run'da başa dönmeyi kontrol et.
