# SoundManager — 17 Ekim 2026

SoundManager bütün oyun AudioSource'larının sahibidir. GameScene içinde
SoundManager nesnesi olarak kayıtlıdır; dört efekt ve müzik havuzu Inspector'dan
yönetilir. Menüde gerektiğinde geçici bir manager kurulur; GameScene'e geçerken
bu geçici manager kapanır ve sahnedeki manager devralır. Sahne manager'ı
DontDestroyOnLoad değildir; sahneyle birlikte temizlenir.
RoundMusicPlayer kaldırıldı; müzik akışı SoundManager.Music.cs içine taşındı.
Cozy Farm SFX'ten dört ses eklendi; lisans kaydı ThirdParty-Licenses.md'de,
dağıtılan atıf dosyaları StreamingAssets/ThirdParty/CozyFarm altındadır.
Bitki hasar sesi sonradan kullanıcının AI ile ürettiği
`Assets/Audio/Generated/cartoon-roguelike-hit-v7.wav` ile değiştirildi (volume 0.3,
pitch 1). Sahne ataması Unity CLI üzerinden yapılıp kaydedilen GUID doğrulandı.

## Kullanım

Kısa efekt isteği (çağıran kod AudioSource tutmaz):

```csharp
SoundEvents.PlayEffect(clip, volume: 0.6f, pitch: 1f);
```

Sahne Inspector'ında tanımlanan sesler:

```csharp
SoundManager.Ensure().Play(GameSound.PlaceItem);
SoundManager.Ensure().Play(GameSound.PlantDamage, critical: false);
SoundManager.Ensure().Play(GameSound.InvalidPlacement);
SoundManager.Ensure().Play(GameSound.CancelPlacement);
```

PlaceItem: PlanterFeel.Land, saksının yere oturduğu an. PlantDamage:
PlantHealth.TakeDamage, yaşayan bitkiye pozitif hasar. InvalidPlacement:
yerleştirme sırasında UI dışına geçersiz tıklama veya başarısız commit.
CancelPlacement: aktif hayalet saksı iptali; başarılı yerleştirmede tetiklenmez.
Her Cue için clip/volume/pitch Inspector'da ayrı ayarlanır.

Mevcut prosedürel ses kataloğu:

```csharp
SoundManager.Ensure().PlayFeel(FeelSound.Chime, 0.7f);
// Eski FeelAudio.Play çağrıları da aynı SoundEvents yoluna gider.
FeelAudio.Override(FeelSound.Chime, replacementClip);
```

Uzun/özel ömürlü efektler için `CreateVoice(SoundChannel.Gameplay, owner)`
kontrol tutamacı verir: Play, Pause, UnPause, Stop. AudioSource dışarı açılmaz.
Owner yok edilince kaynak merkez tarafından temizlenir. Bu API'yi kullanan
sistem, sahibinin OnDisable'ında Stop/Clear çağırmalıdır. LateGameAudio böyle
çalışır; onun sekiz ses sınırı, klip başına sınırları ve tekrar aralıkları korunur.

## Olaylar ve yaşam döngüsü

- SoundEvents.EffectRequested → SoundManager.PlayEffect.
- RoundManager.OnRunStarted → efektleri durdur, müziği sıfırla.
- OnRoundChanged ve GameManager.OnGameStateChanged → round müziğini değerlendir.
- OnRoundEnded → müziği duraklat. Sonraki aktif round kaldığı yerden sürer.
- Menü, run hazırlığı veya run tamamlanması → müziği sıfırla, efektleri durdur.
- GameSettings.Changed → ana/müzik/efekt ses seviyelerini güncelle.
- Application.focusChanged → arka planda sessizlik tercihini uygula.

Manager referansları sahne değişimi için kare başına kontrol edilir. Parça
bitişi ve doğrudan timeScale=0 duraklaması olay üretmediği için müzik de
kare başına kontrol edilir. Abonelikler OnDisable'da kaldırılır.

## Ses grupları ve mevcut veri yerleri

- Music: öncelikle sahne Inspector'ındaki Entries havuzu; boşsa
  `Assets/Resources/Music/RoundMusicPool.json` yedeği.
  Havuzda yalnız tek, sınırsız parça varsa native loop; diğer durumlarda bitişte
  güncel güç puanıyla yeni seçim.
- Feedback: sekiz kaynak, sırayla tekrar kullanılır; menü/özet sesleri çalabilir.
  Eski sınırsız PlayOneShot üst üste binmesi yerine en eski slot yeniden kullanılır.
  Ses üretimi ve override tablosu FeelAudio'da kalır.
- Hit: ayrı sekiz kaynak; PlantHealth hasar isteğini gönderir. Klip ve kare
  başına normal/kritik vuruş kısıtı SoundManager.Cues içindedir. VFXManager
  artık bitki sesi çalmaz; çift ses oluşmaz.
- Gameplay: LateGameAudio özel tekrar/voice sınırlarını korur; gerçek kaynaklar
  SoundManager'dadır. Efekt ses ayarı artık bu seslere de uygulanır.

Müzik GameSettings.MusicVolume, bütün efekt grupları EffectsVolume kullanır.
MasterVolume yalnız AudioListener'a uygulanır; iki kez çarpılmaz. Sıfır müzik
sesi parçayı yeniden başlatmaz. Ayarların saklanması mevcut PlayerPrefs ve
OptionsUI akışında kalır. Gruplara ayrı ayar eklemek için Voice.ApplyVolume
merkezî noktadır; şimdilik ayrı grup slider'ları eklenmedi.

Boss ödülleri başarılı alımda kendi aşamasına göre +1 / +2 / +3… müzik güç
puanı verir; BossRewardSO.musicPowerOverride ile özel puan belirtilebilir.
Yeni run ve ana menüde puan sıfırlanır. SoundManager bu puana uyan Min Power /
Max Power aralıklarından Weight ile seçim yapar; Max Power 0 üst sınır olmadığını
belirtir. Puan değişse de mevcut parça bitene kadar seçim yenilenmez.
Yeni müzik ekleme örnekleri ve aralık kuralları: Docs/RoundMusic.md.

## Doğrulama

Oyun C# kaynakları Unity 6000.3.14f1 referansları ve yerel Roslyn ile derlendi:
0 hata (mevcut serialize alan/unused field uyarıları var).
Assets/Scripts taramasında AudioSource oluşturma yalnız SoundManager.cs'de.
Canlı Unity Editor'da kurulum ve programatik Play Mode testi çalıştırıldı:
`Docs/Audio/SceneSetupVerification.txt`, `Docs/Audio/PlayModeVerification.txt`.
Menü StartRun -> GameScene, tek manager, dört klip yönlendirmesi, efekt mute,
müzik loop, shop pause/resume ve müzik mute kontrolleri geçti. Tam fareyle
yerleştirme senaryosu ve işitsel kalite değerlendirmesi yapılmadı.
GameScene doğrudan Play yerine MenuScene'den başlatılmalıdır; GameManager
menüden gelir. Mevcut iki test penceresi Play Mode'a girerken Invalid editor
window hatası veriyor; ses doğrulaması buna rağmen geçti.

Play Mode kontrolü: art arda round'larda parçanın konumu, yeni run'da sıfırlama,
müzik/efekt slider bağımsızlığı, çalan uzun efektin sesini değiştirme, timeScale
duraklatma, arka plana geçiş ve sahne değiştirdikten sonra tek manager/tek müzik.
