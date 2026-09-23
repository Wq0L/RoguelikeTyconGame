# Uygulanan skill tree

140 node, 304 satın alma. Veriler `Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree` içinde. `GameScene / Skill Shop Panel` altında 140 adet mevcut Skill Node prefab instance'ı sahnede kayıtlıdır; Play'e basmadan Hierarchy'den seçilebilir. SkillTreeUI.nodeUIs ve SkillTreeManager.allNodes Inspector listelerinde doğrudan bağlıdır. Resources taraması ve runtime node üretimi yoktur. Eski prototip SO'ları korunur; eski 20 sahne node'unun yerini yeni kayıtlı node'lar alır.

Konumun tek kaynağı SkillNodeSO.gridPosition X/Y × SkillNodeUI.spacing (150). Sahnedeki anchoredPosition aynı koordinatla kaydedilmiştir; Z=0. customLayout/layoutPosition kaldırıldı. Ayrı kod etiketi veya yeni buton stili eklenmez; mevcut prefabın Image, Button, tier dots ve tooltip ayarları kullanılır. Oyun sırasında gezinme için mevcut viewport/content düzeni korunur.

## Başlangıç ve kilitler

- Normal koşu: 80 Gold, 0 Iron, 0 Stone; 1×1 (20 Gold) ve 1×2 (40 Gold) açık. ResourceManager Inspector'ında `useDebugStartingResources` Editor testleri için 80.000'er kaynağı açar; varsayılan kapalıdır ve build'de uygulanmaz.
- P2: 1×3 kilidi; P5: 2×2 kilidi; P8: 2×3 kilidi. Açılış saksıyı ücretsiz vermez.
- Explosive / Duplicate / Tornado kartları P3 / P6 / P4 ile havuza girer. Tornado kilidi Patlayıcı Kartlar sonrasında 40 Iron ile açılır. Diğer mevcut aileler açık.
- Bumerang Orak kartları P9 ile (P4 sonrası, 60 Iron), Çapraz Elektrik kartları P10 ile (P3 sonrası, 80 Iron) havuza girer. Bu node'lar davranışın kendisini değil, ilgili kart ailesinin açılmasını yönetir.
- İlk rounddan önce alışveriş; süre tabanı 30 saniye, Z rotaları toplam +60 saniye. Her yeni round başında hesaplanır ve 90 saniyede sınırlanır.
- Koşu uzunluğu 130 round. Hedef round metadatası sert kilit değildir.

## Grid

Başlangıç 3×3. P1 seviye 1: 5×5, 80 Gold; seviye 2: 7×7, 40 Iron. P7 seviye 1: 9×9, 120 Stone; seviye 2: 11×11, 450 Stone. P7 için P1'in ikinci seviyesi gerekir. P5 için P2 ve P1 seviye 1; P8 için P5 ve P1 seviye 2 gerekir. 2×3 artık 9×9'a bağlı değildir. Boyutlar merkezden kare açılır, bir hücrelik artış değildir.

## Bağlantılar ve etkiler

Normal rotalar A(3) → B(2) → C(2) → D(2). Yeni Seri Üretim (E12) ve Yıldırım Kesim (Z11) rotaları üçer üç kademeli node içerir; sırasıyla E7-D ve Z5-D tamamlandıktan sonra sırayla açılır. Eski hız rotalarının özgün etkileri korunur. İç zincirde önceki node tamamlanır; başka rotaya geçiş için kaynak rotanın A node'u tamamlanır. Veri içindeki açık önkoşullar esas alınır; çapraz komşuluk geçiş açmaz. Eski kademe etkisi yenisi eklenmeden kaldırılır. Çarpan etkileri bölünürken geometrik oran kullanılır. Fiyatlar, ilk hasar rotaları, gelir ve hız etkileri Eylül 2026 ekonomi düzeniyle güncellenmiştir; ayrıntılar `EconomyBalance.md` içindedir.

## Güç dönemleri

- R1–20: bütün saksı türleri açılabilir; hasar temeli, ilk hız/süre ve gelir yatırımları.
- R21–65: daha pahalı üretim/hasar yatırımları ve seçimler.
- R66–100: hız ve gelir bileşimiyle büyüme; 9×9/11×11 alan.
- R100–130: god mode; iyi build için güçlenme/full ağaç, zayıf build için zorlanma; referans simülasyonda tamamlayanların ortalaması yaklaşık R99. Bunlar fiyat hedefidir, round kilidi değildir.

Son hasar kolu: (1 + 60 + 150 + 1000 + 4000 + 2200) × (1 + 5) × 8 × 9 = 3.201.552 nominal hasar (float toplamında küçük fark olabilir). H10 ayrı ×8, H11 ayrı ×9 çarpanıdır. ±%15 vuruş farkı, kritik ve Odak ayrıca uygulanır. Tam ağaç saldırı aralığı 0,2 saniye, üretim aralığı 0,5 saniyedir. Üretim için 0,5 saniye tabanı tile ve rezonans sonrasında da korunur. Tam gelir çarpanları Gold ×4, Iron/Stone ×5; XP ×3. Ayrıntılı fiyatlar ve örnek koşu sonuçları `Docs/EconomyBalance.md` içindedir.

HP eğrisi değiştirilmedi: R130 Legendary 540.000 HP. Tam hasar kolu rezonanssız da bunu tekler. Bu tavan ile önceki zayıf düzenin dört vuruş gerektirmesi hedefi aynı anda korunmuş sayılmaz. Fiyatların ve hedef roundların gerçek gelir/oynanışla doğrulanması gerekir; hesap kontrolü ekonomi playtest'i değildir.

## Kontrol ve yeniden üretim

Yerleşim dört sabit kola ayrılır: hasar +Y, zaman/hız -X, saksı/grid +X, ekonomi -Y. Aynı becerinin node'ları birer grid aralıklı düz sıralardır. Grupların başlangıçları elle belirlenen koordinatlara sahiptir. `Tools/layout-final-tree.cjs` bu açık koordinat tablosunu manifestte yazar. Yuvarlama veya en yakın boş hücreye kaydırma yapılmaz; çakışan koordinat hata verir. Bağlantıların başka node'lara temas etmediği de kontrol edilir. `Docs/SkillTree-XY.png` eski 131 node düzeninin şemasıdır; eklenen dokuz node için güncel kaynak manifest ve GameScene sahnesidir.

Yerleşimi yeniden kurma sırası: `node Tools/layout-final-tree.cjs`, ardından `node Tools/author-final-tree-scene.cjs`, ardından `node Tools/verify-final-tree-scene.cjs`. İlk iki komut manifest/sahne yerleşimini değiştirir; kullanıcı Inspector düzenini otomatik ezmek için çalıştırılmaz. UI, merkez anchor/pivot ve Z=0 kullanır. Panel kapanırken root animasyonları durdurulup ölçek/dönüş sıfırlanır; yeniden açılırken aynı SO koordinatı uygulanır.

`Tools/Skill Tree/Verify Final Tree` Unity menüsü node sayısı, önkoşullar, kademe sayısı, fiyat geçerliliği, son hasar, süre ve grid değerlerini kontrol eder.

`node Tools/build-final-skill-tree.cjs` manifestten aynı GUID'lerle asset'leri yeniden üretir. Kaynak: `Docs/FinalSkillTree.json`. Inspector'da yapılan denge değişikliklerini yeniden üretmeden önce manifestte de güncelle; aksi halde üretim bu değişikliklerin üzerine yazar.

`node Tools/author-final-tree-scene.cjs` mevcut prefab instance biçimini kullanarak sahne node'larını ve Inspector listelerini yazar. Inspector'da sahne düzeni değiştirildiyse bilinçli olarak çalıştırılmalıdır; otomatik çalışmaz. `node Tools/verify-final-tree-scene.cjs` 140 node'un sahne/SO bağlantılarını, X/Y eşleşmesini, benzersiz koordinatları ve yerel sahne referanslarını kontrol eder.
