# Performans kaydı v3 (F9) — 26 Eylül 2026

`-harvest-perf` ile açılan EXE, menüden oyuna geçişte D3D12 scratch allocator içinde çöküyordu. Aynı iz, kayıt hiç başlatılmamış F9 build'inde planter shop açılırken de görüldü. Bu iz Unity'nin bilinen font atlas yükleme hatasıyla (UUM-140564) uyumlu; ayrıntı ve düzeltme aşağıda.

V3'te komut satırı bayrağı yok ve kaydedicinin açılışta hiçbir izi yok: başlangıçta nesne yaratılmaz, DontDestroyOnLoad kullanılmaz. F9'u oyun sahnesindeki `UIManager` dinler; kayıt nesnesi ilk F9'da o sahnede oluşur ve sahneyle birlikte yok olur. F9'a basılana kadar dosya açılmaz, örnek alınmaz, log dinlenmez. Ana menüde F9 çalışmaz; kayıt sırasında ana menüye dönülürse kayıt kendiliğinden kapanır.

## Kullanım

1. Normal Windows build al (Development Build gerekmez) ve EXE'yi normal çift tıkla aç.
2. Play'e bas, oyun sahnesine gir. **F9** → sol üstte kırmızı `[REC] 00:00` görünür.
3. Oyna. Tekrar **F9** → kayıt durur ve son yarım pencere de yazılır. Oyunu kapatmak da kaydı güvenle kapatır.
4. Kayıt sırasında Alt+Tab yapma: pencere odağı kaybolunca oyun durur, dönüşte tek büyük kare süresi (sahte drop) görünür. `focused` sütunu bunu gösterir.

Kayıt klasörü (Win+R ile açılabilir):
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\clickerTycon\Performance`

CSV adı `trend-YYYYMMDD-HHmmss-fff.csv`. Player.log bir üstteki `clickerTycon` klasöründe. Editor'de de F9 çalışır veya Tools > Performance > Start Trend Capture (Play Mode); Editor kayıtları `Logs/Performance` altında.

Arkadaş/düşük PC testi: build klasörünü gönder, aynı adımları uygulasınlar, CSV'yi geri yollasınlar. Donanım sütunları dosyayı kimin makinesinden geldiğini ayırt eder.

## Sütunlar

- `avg_fps`, `avg_frame_ms`: beş saniyelik pencere ortalaması.
- `frame_ms_p99`, `frame_ms_max`, `frames_over_16ms`, `frames_over_33ms`: aynı penceredeki en kötü %1 kare, en uzun kare ve 60/30 FPS sınırını aşan kare sayısı. Ortalamada kaybolan tek karelik drop'lar burada görünür.
- `state`, `round`, `time_scale`, `focused`, `editor_paused`: oyun durumu.
- `text/hit/explosion_total` ve `*_leased`: efekt havuzunda oluşturulan ve o an kullanılan örnekler.
- `plant_created/active/inactive/pending`: bitki havuzu.
- `tweens_active/playing`, `global_modifiers`: tween ve build yükü.
- `managed_heap_bytes`, `gc0_total`, `logs/warnings/errors`: yönetilen bellek ve sayaçlar.
- `screen`, `fullscreen_mode`, `quality`, `vsync`, `target_fps`: o anki görüntü ayarları.
- `graphics_api`, `gpu`, `cpu`, `ram_mb`: donanım bilgisi (virgüller `;` ile değiştirilir).
- `probe_ms`: örnek toplama süresi. `recorder_version=3`.

## D3D12 çökmesi ve font atlasları

Oyunun TMP fontları (Bangers, LilitaOne, Barlow-SemiBold, Super Popstar, LiberationSans Fallback) dinamikti ve `Clear Dynamic Data On Build` açıktı. Build'de atlaslar boş başlıyor, sahne yüklenirken veya planter shop açılırken yüzlerce karakter aynı karede ekleniyordu. Aynı atlasın tek karede defalarca GPU'ya yüklenmesi Unity'nin bilinen D3D12 scratch allocator çökmesini tetikler (UUM-140564). Editor'de atlaslar dolu olduğu için Editor çökmüyordu.

`Assets/Editor/BuildFontAtlasPreparer.cs` her build'den önce (TMP'nin temizleyicisinden önce) bu fontlarda temizliği kapatır ve ASCII, Türkçe harfler ile oyunda kullanılan sembolleri (→ ← × · — − ’ vb.) atlasa ekler. Elle çalıştırmak için: Tools > Fonts > Prepare Build Font Atlases. Fontlar dinamik kalır; listede olmayan nadir bir karakter yine çalışma anında eklenir. Yeni bir TMP fontu oyuna eklenirse GUID'i bu dosyadaki listeye eklenmelidir. Grafik API'si D3D12 olarak kaldı; çökme tekrar ederse D3D11 ayrıca değerlendirilecek.

## Sınırlar

CPU ve GPU süresi ayrı ölçülmez; drop'un nedenini değil zamanını ve büyüklüğünü gösterir. Neden için Development Build + Profiler kaydı gerekir. Kayıt sırasında sol üstteki IMGUI etiketi ve beş saniyede bir CSV yazımı çok küçük ek maliyet getirir. Ani çöküşte son birkaç saniye eksik olabilir. Materyal/renderer sayımı ve Unity native bellek ölçümü bilerek yapılmaz.
