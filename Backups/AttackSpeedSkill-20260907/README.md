# Attack Speed örnek yeteneği

- SO: `Assets/ScriptableObjects/Skill Tree Upgrades/Attack Speed Upgrade.asset`
- Aktif sahne: `GameScene`, `Skill Shop Panel` altında `Attack Speed Node`.
- Mevcut `Assets/Prefabs/UI/Skill Node.prefab` instance'ı kullanıldı. Görsel, tooltip ve satın alma animasyonlarını oradan alır; ana prefab değiştirilmedi.
- Node koordinatı `(3,0)`, Grid Size Upgrade `(2,0)` düğümünün sağ komşusudur. Grid yeteneğinin ilk seviyesi alınınca mevcut komşuluk mantığıyla görünür olur.
- Tek seviye, 50 Gold. `AttackSpeed / Player / MorePercent / -0.2` saldırılar arası süreyi 0.8 ile çarpar. Başka etkiler yoksa 3 saniye 2.4 saniye olur; saldırı sıklığı %25 artar. AttackSpeed bu projede bir hız değil saniye cinsinden aralıktır.
- Sahnedeki `SkillTreeUI.nodeUIs` listesine yeni component eklendi. Boş `SkillTreeManager.allNodes` listesi mevcut Damage, Grid ve yeni Attack Speed SO'larıyla dolduruldu; bu alan şu an runtime hesapta kullanılmıyor, katalog olarak tutuluyor.
- Yeni C# kodu gerekmedi. Satın alma, para kontrolü, tek seviye/MAX, stat uygulaması ve animasyonlar mevcut sistem üzerinden çalışır.

Benzer yetenek için SO'yu kopyala, boş bir komşu koordinat seç, effect/cost ayarla; sahneye Skill Node prefab instance'ı ekleyip node alanını SO'ya bağla ve SkillTreeUI.nodeUIs listesine ekle. Editor'daki anchoredPosition, gridPosition * spacing ile eşleşirse Play öncesi de doğru yerde görünür.

Yedek `GameScene.before.unity` bu değişiklikten hemen önceki sahnedir; önceki mağaza düzenlemelerini içerir. Sonraki sahne değişikliklerini ezmeden gerekirse seçerek geri al.

SO/sahne bağlantıları, koordinat/erişilebilirlik ve süre hesabı kontrol edildi. Unity Play Mode testi yapılmadı.
