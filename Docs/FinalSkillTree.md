# Uygulanan skill tree

131 node, 283 satın alma. Veriler `Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree` içinde. `GameScene / Skill Shop Panel` altında 131 adet mevcut Skill Node prefab instance'ı sahnede kayıtlıdır; Play'e basmadan Hierarchy'den seçilebilir. SkillTreeUI.nodeUIs ve SkillTreeManager.allNodes Inspector listelerinde doğrudan bağlıdır. Resources taraması ve runtime node üretimi yoktur. Eski prototip SO'ları korunur; eski 20 sahne node'unun yerini yeni kayıtlı node'lar alır.

Konumun tek kaynağı SkillNodeSO.gridPosition X/Y × SkillNodeUI.spacing (150). Sahnedeki anchoredPosition aynı koordinatla kaydedilmiştir; Z=0. customLayout/layoutPosition kaldırıldı. Ayrı kod etiketi veya yeni buton stili eklenmez; mevcut prefabın Image, Button, tier dots ve tooltip ayarları kullanılır. Oyun sırasında gezinme için mevcut viewport/content düzeni korunur.

## Başlangıç ve kilitler

- 80 Gold; 1×1 (20 Gold) ve 1×2 (40 Gold) açık.
- P2: 1×3 kilidi; P5: 2×2 kilidi; P8: 2×3 kilidi. Açılış saksıyı ücretsiz vermez.
- Explosive ve Duplicate kartları P3/P6 ile havuza girer. Diğer mevcut aileler açık.
- İlk rounddan önce alışveriş; süre tabanı 30 saniye, Z rotaları toplam +60 saniye. Her yeni round başında hesaplanır ve 90 saniyede sınırlanır.
- Koşu uzunluğu 130 round. Hedef round metadatası sert kilit değildir.

## Grid

Başlangıç 3×3. P1 seviye 1: 5×5, 140 Gold; seviye 2: 7×7, 280 Iron. P7 seviye 1: 9×9, 600 Stone; seviye 2: 11×11, 1950 Stone. P7 için P1'in ikinci seviyesi gerekir. P5 için P2 ve P1 seviye 2; P8 için P5 ve P7 seviye 1 gerekir. Boyutlar merkezden kare açılır, bir hücrelik artış değildir.

## Bağlantılar ve etkiler

Normal rotalar A(3) → B(2) → C(2) → D(2). İç zincirde önceki node tamamlanır; başka rotaya geçiş için kaynak rotanın A node'u tamamlanır. Veri içindeki açık önkoşullar esas alınır; çapraz komşuluk geçiş açmaz. Eski kademe etkisi yenisi eklenmeden kaldırılır. Çarpan etkileri bölünürken geometrik oran kullanılır, toplam güç ve normal rota bedelleri önceki onay adayından korunur. Grid fiyatları yeni iki node düzenine göre ayarlanmıştır.

## Güç dönemleri

- R1–20: H1, ilk atak aralığı, süre ve gelir yatırımları; hızlı başlangıç.
- R20–60: H2, gelir ve 7×7/2×2; daha yavaş büyüme.
- R60–100: H3/H4, 9×9, 2×3 ve davranışlar; geç oyuna giriş.
- R100–130: H5 ve H10/H11; fantezi hasarı.

Son hasar kolu: (1 + 9 + 40 + 150 + 600 + 2200) × (1 + 5) × 8 × 9 = 1.296.000. H10 ayrı ×8, H11 ayrı ×9 çarpanıdır. Üç Odak +%20/+%25/+%30 roll ile sırasıyla 8.294.400 / 9.072.000 / 9.849.600 nominal hasar. ±%15 vuruş farkı ve kritik ayrıca uygulanır. Odak rezonans eşikleri değişmedi. Patlama hasarı oyuncu hasarını alır, saksı Odak çarpanını almaz.

HP eğrisi değiştirilmedi: R130 Legendary 540.000 HP. Tam hasar kolu rezonanssız da bunu tekler. Bu tavan ile önceki zayıf düzenin dört vuruş gerektirmesi hedefi aynı anda korunmuş sayılmaz. Fiyatların ve hedef roundların gerçek gelir/oynanışla doğrulanması gerekir; hesap kontrolü ekonomi playtest'i değildir.

## Kontrol ve yeniden üretim

Yerleşim dört sabit kola ayrılır: hasar +Y, zaman/hız -X, saksı/grid +X, ekonomi -Y. Aynı becerinin 1–4 node'ları birer grid aralıklı düz sıralardır. Ana gruplar arasında beş grid aralığı bulunur; başlangıç ve saksı kolu ayrı elle belirlenen koordinatlara sahiptir. `Tools/layout-final-tree.cjs` bu açık koordinat tablosunu manifestte yazar. Yuvarlama veya en yakın boş hücreye kaydırma yapılmaz; çakışan koordinat hata verir. Bağlantıların başka node'lara temas etmediği de kontrol edilir. `Docs/SkillTree-XY.png` bu koordinatların şemasıdır, Unity ekran görüntüsü değildir.

Yerleşimi yeniden kurma sırası: `node Tools/layout-final-tree.cjs`, ardından `node Tools/author-final-tree-scene.cjs`, ardından `node Tools/verify-final-tree-scene.cjs`. İlk iki komut manifest/sahne yerleşimini değiştirir; kullanıcı Inspector düzenini otomatik ezmek için çalıştırılmaz. UI, merkez anchor/pivot ve Z=0 kullanır. Panel kapanırken root animasyonları durdurulup ölçek/dönüş sıfırlanır; yeniden açılırken aynı SO koordinatı uygulanır.

`Tools/Skill Tree/Verify Final Tree` Unity menüsü node sayısı, önkoşullar, kademe sayısı, fiyat geçerliliği, son hasar, süre ve grid değerlerini kontrol eder.

`node Tools/build-final-skill-tree.cjs` manifestten aynı GUID'lerle asset'leri yeniden üretir. Kaynak: `Docs/FinalSkillTree.json`. Inspector'da yapılan denge değişikliklerini yeniden üretmeden önce manifestte de güncelle; aksi halde üretim bu değişikliklerin üzerine yazar.

`node Tools/author-final-tree-scene.cjs` mevcut prefab instance biçimini kullanarak sahne node'larını ve Inspector listelerini yazar. Inspector'da sahne düzeni değiştirildiyse bilinçli olarak çalıştırılmalıdır; otomatik çalışmaz. `node Tools/verify-final-tree-scene.cjs` 131 node'un sahne/SO bağlantılarını, X/Y eşleşmesini, benzersiz koordinatları ve yerel sahne referanslarını kontrol eder.
