# Bölüm 3.8 — Model, animasyon, VFX ve ses bağlama

Güncelleme: 7 Ekim 2026, Diken Kuşatması üretimi. Önceki placeholder sözleşmesindeki Hit, Hide,
sabit süre ve zemin yüksekliği sınırlamaları giderildi. Ayrıntı/test: [ThornSiege/README.md](ThornSiege/README.md).
Diken dışındaki olay ve ödüllerin nihai içerikleri hâlâ açık.

## Katalog

`Assets/Resources/LateGameArt.asset` (`LateGameArtSO`):

- `slots`: `slot`, `variant`, `prefab`, `lifetime`.
- `sounds`: `sound`, `variant`, `clip`, `volume`, `repeatSeconds`, `voices`.
- Varyant için kayıt yoksa `Default` kullanılır. Prefab boşsa placeholder, ses boşsa sessizlik.
- `lifetime > 0`: kısa efektin oyun saniyesi olarak ömrü. Sıfır: eski .22 saniye fallback.
- Sıfır ses düzeyi sessizdir. Sıfır voices: varsayılan 2; sıfır repeatSeconds: varsayılan .06 saniye.
- Diken sesleri yalnız `Thorn` varyantındadır; diğer olaylarda çalmaz.

## Yuvalar

| Yuva | İçerik |
|---|---|
| GroundSign | Hücre köşesinde toprak/kabarma belirtisi. Bağlı prefab zemindedir. Boş eski yuva +2.2 yüksekte placeholder gösterir. |
| OverheadWarning | İsteğe bağlı bitki üstü uyarı yuvası. Diken’de 8 Ekim itibarıyla boş; uyarı zeminden gelir. |
| ThornRoot | Müdahale hedefi, kırılana kadar. |
| ThornPiece | Kapalı bölgenin diken kümesi. |
| SproutMain / SproutPiece | Ana filiz hedefi / yayılmış filiz. |
| ShellNode / ShellCover | Kabuk düğümü / kapanan bölge örtüsü. |
| BountyMarker | Bereket bölgesinin bitki üstü belirtisi. |
| GroundBurst | Diken ortaya çıkarken zeminden sıçrayan dört küçük toon toprak parçası; .48 saniye. |
| HitBurst / BreakBurst | Darbe / kırılma. Diken ayrı iki efekt kullanır, eski olaylar BreakBurst fallback'ini sürdürür. |
| BloomBurst | Canlanma. |
| Antenna | Kalıcı anten. |
| AntennaBolt / LineBolt / ArcBolt | İki nokta arası kısa vuruş. |
| CrescentBlade | Hilal adımı. |
| Pulse | Tek noktalı ödül efekti, aşağıdaki varyantlarla ayrıştırılabilir. |
| PlacementMarker / PlacementCell | Yerleşim adımının kenar / hücre işaretleri. |

Enum değerleri korunur; OverheadWarning=18, HitBurst=19, GroundBurst=20 sona eklendi.
Dekorlar ve çıkış kuyrukları birlikte en fazla 96 aktif dekor; hedefler ve antenler bu limite girmez.
Sınır dolarsa yeni dekor isteği atlanır; oynanış ve hedefler etkilenmez.

## Varyantlar

- `Default`: ortak içerik/fallback.
- `Thorn`: Diken'in kökü, parçası, zemin, üst uyarı, darbe/kırılma ve sesleri.
- `ShellA` / `ShellB`: düğüm, örtü ve zemin belirtisi için eşleşen renk/motif prefabları atanabilir.
  HUD renkleri `LateGamePresenter.ZoneColor` ile aynı tutulmalıdır. Bu görevde kabuk prefabı üretilmedi.
- `FullMoon`, `HarvestEcho`, `CritBurst`, `Reactor`, `Greed`, `Overload`, `CrescentGrow`: Pulse için ayrı prefab/ömür.
  Ortak Pulse kaydı korunur. Bu görevde ödül efektleri üretilmedi.

## Prefab sözleşmesi

- Pivot zeminde; +Y yukarı, +Z ileri. Hücre 2×2 dünya birimidir.
- `LateGameContent.visual`: animasyon uygulanacak child. Kök transformu ve mantıksal saldırı alanı animasyonla değişmez.
  Boşsa ölçek animasyonu yapılmaz. Collider gerekmez; saldırı, oyunun sabit hücre temasından sayılır.
- `topPoint`: gerçek darbe VFX konumu. Boşsa hücrenin biraz üstü kullanılır.
- `onShow`, `onHit`, `onHide`: isteğe bağlı ParticleSystem dizileri.
- `animator` ve Show/Hit/Hide tetikleri isteğe bağlıdır. Yeniden kullanımda Animator Rebind edilir.
- `hideSeconds`: çıkış için 0–2 saniye. Oynanış bu süreyi beklemez.
- `damageStages`: dayanıklılık azaldıkça açılan çatlak child grupları; Show'da sıfırlanır.
- `fragments` / `fragmentVelocities`: merkezi güncellemeyle hareket eden küçük VFX parçaları; fizik gerekmez.
- Işın görseli +Z boyunca birim uzunlukta hazırlanır. Anten kökü tarlaya döndürülür.
- Bütün yeni Diken meshleri ve VFX `ClickerGame/ThornComicToon` shader'ını kullanır; gövde ve ink malzemeleri paylaşılır.
  Model veya VFX değiştirirken toon görünümü korunmalıdır.

## Yaşam döngüsü

1. Nesne havuzdan alınır; konumu, yönü ve ölçeği atanır, sonra `Show()` çağrılır.
2. Aynı hücre/hedef görseli bildirimler arasında korunur; her darbede yeniden oluşturulmaz.
3. Gerçek `TargetHit` / `TargetBroken` önce mevcut hedefin `Hit()` çağrısına gider; ardından durum uzlaştırılır.
4. Kırılmış/kalkmış görsel `Hide()` alır ve olay listesinden çıkar. Kısa çıkışı yalnız sunum listesinde sürer.
5. Süre bitince parçacıklar temizlenir, nesne kapanır ve havuza döner. Eski görselde oyun durumuna callback yoktur.
6. Round/run temizliği, yeni olay ve disable beklemeden çıkışları temizler. Pause, görsel zamanı ve sesleri dondurur.

Nesneler ilk ihtiyaçta oluşturulur; sonraki ihtiyaçlarda yeniden kullanılır. Baştan bütün havuzu ısıtma garantisi yoktur.
Parçacık/Animator ayarlarında ölçeklenmiş oyun zamanı kullanılmalıdır; unscaled animation bu pause sözleşmesini bozar.

## Ses

Sunum, en fazla 8 ayrı AudioSource kullanır. Her kaydın klip başına eşzamanlı sınırı ve tekrar aralığı vardır;
sınır aşılırsa yeni ses atlanır. Pause'da Pause/UnPause, round/run sonunda Stop ve sayaç reseti uygulanır.
Diken çıkış sesi Emerged anında, hazırlık/hit/break/resolve sesleri ilgili gerçek bildirimde çalar.

## Doğrulama ve açık işler

`Tools/Verification/Editor/ThornArtVerification.cs`, dolu GameScene'de gerçek Diken verileriyle görsel yaşam döngüsünü sınar.
`GecOyunV1Verification` genel olay/ödül regresyonudur. Çıktılar `Docs/ThornSiege/` altındadır.
İnsan Play hissi, ses miksinin dinlenmesi, farklı ekran oranları ve donanımda GPU ölçümü otomatik test yerine geçmez.
Diğer üç olayın modelleri, diğer ödül efektleri, anten ve yerleşim içeriği hâlâ üretilmelidir.
Dış içerik eklenirse AGENTS.md'deki lisans kontrolü ve konuşmada ön bildirim zorunludur.

## Anten ailesi üretimi — 8 Ekim 2026

Antenna/Circuit varyantları sonuna eklendi; önceki kimlikler ve tarla olayı bağları korunur.
AntennaContact=24, CircuitReturn=25, PlacementSelected=26. AntennaPlaced=19 ve
CircuitPulse=20 ses kimlikleri eklendi. Mevcut Antenna/AntennaBolt/LineBolt ve yerleşim
slot'ları Antenna varyantıyla bağlanır; ek hat Circuit varyantını kullanır.

LateGameAntenna yalnız merkezi Step ile child pozu/şarj/geri tepme sürer. Sayaçlar salt
okunur; hasar geciktirilmez. Gerçek StrikeResult.Hit temas bildirimi üretir; başarılı
Kapalı Devre Schedule enerji dönüşü üretir. Modeller yeniden kullanımda sıfırlanır ve
alan genişleyince aynı instance yeni kenara taşınır. Detay, üretim kaynakları, poligon
sayıları, doğrulamalar ve insan Play listesi: AntennaFamily/README.md.

Bu bölüm üstteki tarihsel 'anten hâlâ placeholder' notlarını geçersiz kılar. Diken,
Filiz, Kabuk ve Bereket de kendi Docs klasörlerinde belgelenen tamamlanmış içeriklerdir.
Hilal ailesinin güncel bağları aşağıdadır.


## Hilal ailesi — 9 Ekim 2026

Crescent=16 ve MoonReturn=17 varyantları CrescentBlade yuvasını kullanır.
MoonContact=27 gerçek StrikeResult.Hit temasına bağlıdır; boş hücrede temas oluşturulmaz.
MoonGrowth=21 sesi eklendi; çıkış/büyüme/dönüş sesleri Crescent varyantındadır.
Ses havuzu hâlâ 8 kaynak; yeni varyant anahtarları için tekrar sayacı kapasitesi 1024 oldu.

CrescentBlade artık hücre başına geçici bıçak değildir. Dört mantıksal dalganın her birine
tek havuz nesnesi bağlanır. CrescentEffect.TryVisual salt okunur rota, yön, saat ve
kırpılmış cephe genişliği verir. Modelin ileri ucu mantıksal cepheye hizalanır.
LateGamePresenter.Moon merkezi LateUpdate ile görsel child'ı sürer; hücrelerde Update yoktur.
Bitiş kuyruğu .16 saniyedir; hasar/ödül/temizlik bu kuyruğu beklemez.

RunProfileSO.crescentFullRows yalnız Run50_GecOyunV1'de açıktır. Eski profiller false
varsayılanıyla dört hücre yolunu korur. Davranış açan yükseltme ve rezonans açık TODO'dur.
Üretim kaynakları, testler, bütçe ve Play listesi: CrescentFamily/README.md.


## Hilal fare rotası — 10 Ekim 2026
Tam sürümde rastgele satırın kenarından geliş yerine saldırı/fare konumundan dört
yönlü çıkış ve gerçek direct bitki isabeti sayacı kullanılır. Aynı prefablar/havuz
kullanılır; dikey dönüş desteklenir. Eşik 6, taşan puan korunur. crescentFullRows
serialized adı korunmuştur; açıklaması güncellendi. Güncel kurallar ve test girişi:
CrescentFamily/CursorRoute.md. 9 Ekim rastgele tam-satır kuralı yürürlükte değildir.

## Kritik Ark — 12 Ekim 2026
CritArc=18 sona eklenen varyanttır; ArcBolt + AntennaContact .12 sn ve CritArc sesi
bu varyantta bağlıdır. Anten prefabları paylaşılır; Default ArcBolt/Taşan Hasar değişmez.
Kaynak hasar öncesi DirectHit.Point, hedef başarılı StrikeResult.Point snapshot’ıdır.
Ayrıntı ve sonraya bırakılan insan kabulü: CritArc/README.md.

## Taşan Hasar — 12 Ekim 2026
Mevcut Overflow=11 varyantında ArcBolt hareketli kısa enerji parçası, AntennaContact gerçek
isabet yıldızı/kırıntılarıdır. Overflow=22 sesi yalnız ilk aktarımda. Cue.TransferStep sıfır
tabanlı sunum adımı; hasar Value içinde korunur. Detay: Overflow/README.md. Kritik Ark
bağlantıları ve mekanik değerler korunur; ayrıntılı insan kabulü bekliyor.
