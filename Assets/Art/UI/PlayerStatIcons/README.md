# Saf oyuncu stat ikonları

32 × 32 px, şeffaf PNG ve her ikon için üç katmanlı Aseprite kaynağı. Güncel seri, tek ana sembol ve az sayıda metal/ışık vurgusuyla okunaklılık için sadeleştirildi. Çizimler Aseprite içinde sıfırdan üretildi; harici görsel veya hazır ikon kullanılmadı. Önizlemenin zemin ve yazıları ikonlara dahil değildir.

## İnceleme ve kapsam

Kapsam, yalnızca enum başlıklarına göre değil gerçek kullanım noktalarına göre seçildi: `PlayerController` içindeki beş `StatTarget.Player` statı ve `HarvestScoreManager` içindeki hasat puanı çarpanı. Hasar ayrıca PlanterBrain içindeki patlama hesabında kullanılsa da ikon oyuncunun temel hasarını temsil eder; patlama davranışını temsil etmez.

| StatType | İkon | Anlam | CoreStat.asset taban değeri |
| --- | --- | --- | --- |
| HarvestDamage | Stat_HarvestDamage.png | Bıçak: hasat hasarı | 1 |
| AreaRadius | Stat_AreaRadius.png | Dört yöne açılan oklar: alan genişliği | 1 |
| AttackSpeed | Stat_AttackSpeed.png | Şimşek ve hız çizgileri: saldırı hızı | 3 saniye |
| CritChance | Stat_CritChance.png | Nişangâh: kritik şansı | 0.3 = %30 |
| CritMultiplier | Stat_CritMultiplier.png | ×2 sembolü: kritik çarpanı kategorisi | 2× |
| HarvestScoreMultiplier | Stat_HarvestScoreMultiplier.png | Kupa: hasat puanı | 1× |

Değerler dosyadaki başlangıç verileridir; oyun sırasında yükseltmeler eklenmiş canlı değerler değildir. PNG adları enum adlarıyla birebir eşleşir. `StatIconMap.json` stat adı, mevcut enum numarası, asset yolu ve Unity GUID eşleştirmesini içerir; otomatik çalışan bir Unity bileşeni değildir.

### Saldırı hızı anlamı

PlayerController saldırıyı `attackTimer >= attackSpeed` olduğunda yapıyor. Bu yüzden AttackSpeed değeri artınca saldırı yavaşlar; azaltılınca hızlanır. Skill açıklamaları ve bonus yönü buna göre yazılmalı. Oyun kodu bu çalışma sırasında değiştirilmedi.

### Hariç tutulanlar

- ExplosionChance, DuplicateChance ve UnlockType davranış/mekanik açılımları.
- PlantSpawnRate, RareSpawnChance ve Gold/Iron/Stone/XPGainMultiplier: mevcut tüketim noktaları Planter hedefli.
- MutationLuck, GridUnlockSize, RoundDuration ve kart seçim/atlama/yenileme statları.
- StartingGoldBonus ve StartingPlanterCount gibi başlangıç/meta bonusları.

## Skill tree bağlantısı

`SkillNodeSO` zaten `public Sprite icon` içeriyor; `SkillNodeUI.Refresh()` bunu `iconImage.sprite` alanına atıyor. İlgili SkillNodeSO assetinin Icon alanına PNG'yi sürüklemek yeterli. Örneğin `Damge Upgrade.asset` için Stat_HarvestDamage, `Attack Speed Upgrade.asset` için Stat_AttackSpeed kullan. Yeni UI kodu gerekli değildir.

Tek node birden fazla stat artırıyorsa ana stat ikonunu seç. Paket otomatik seçim veya node bağlantısı yapmaz; kullanıcı bağlayacağı için mevcut asset ikonları değiştirilmedi.

Unity import ayarları hazır: Sprite Single, Point filtre, Compression None, mipmap kapalı, Full Rect. Image Type Simple ve Preserve Aspect kullan. 32, 64 veya 96 px gibi tam sayı ölçekler önerilir. Mevcut skill-node çerçevesinin içinde kullan; ikonların kendi arka plakası yoktur. Dosya adları ve Unity GUID'leri korundu; atanmış referanslar yeni çizimleri kullanır.

Güncel kaynak betiği: `ArtSource/PlayerStatIcons/create_simple_stat_icons.lua`. Güncel önizleme: `ArtSource/PlayerStatIcons/Player_Simple_Preview.png`. Önceki cihaz ve ilk tasarım betikleri/önizlemeleri arşivdir. ×2 yalnızca çarpan kategorisini anlatan semboldür; gerçek kritik çarpanını tooltip/metinde stat sisteminden göster.

