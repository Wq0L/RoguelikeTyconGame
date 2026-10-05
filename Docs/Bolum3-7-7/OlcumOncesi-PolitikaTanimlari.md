# Bölüm 3.7.7 — ölçümden önce yazılan politika ve kol tanımları

Yazım zamanı: 4 Ekim 2026, pilot ve A/B ölçümleri başlamadan önce (yalnız araç denemesi `k377smoke` çalıştırılmıştı).
Kaynak: `Tools/Verification/Editor/BalanceRunMeasurement.cs`. Sonuçlar görüldükten sonra bu tanımlar değiştirilmez; değişirse
nedeni ana belgede yazılır.

Bot insan değildir: nişanı her vuruşta en iyi konumu seçer, menüde zaman harcamaz. Bedava saksı, kart, kaynak ya da davranış
verilmez; saksı gerçek mağaza yoluyla alınır, kart ekrandaki üç adaydan seçilir, tile yeri oyunun rastgele yerleşimidir.

## Devam yönleri

| | Hasar-alan | Davranış yönü |
|---|---|---|
| Ağaç önceliği (vade kayması, round) | hasar −4, alan −4, kritik −2, hız +1, nadirlik +3, skor +2, kart +8, davranış kilidi +6 | davranış kilidi −8, üretim −2, hasar −2, hız +1, alan +3, XP +2, kart +4, kritik +12 (mevcut "Davranış-rezonans" ile aynı) |
| Tile kartı ağırlığı (nadirlik gücü × ağırlık) | Damage ×1,8 · Fertile ×1,3 · Energy ×1,2 | Electric ×2 · Explosive ×2 · Tornado ×1,9 · Boomerang ×1,9 · Damage ×1,3 |
| Temel güç kartı ağırlığı | Hasat hasarı ×2 · Atak aralığı ×1,5 | Hasat hasarı ×2 · Üretim süresi ×1,5 |
| Boss ödülü sırası | Keskin Bıçak, Geniş Savuruş, Canavar Kesimi, Hızlı Bilek, Fırtına Bileği, Kritik Göz, Ağır Darbe, Bereketli Toprak, Nadir Tohum, Altın Hedef, Hasat Ritmi, Kıvılcım, Yıkım Gücü, Bilgi Filizi | Zincir Hasat, Kıvılcım, Yıkım Gücü, Artçı Patlama, Çifte Akım, Hızlı Bilek, Fırtına Bileği, Bereketli Toprak, Geniş Savuruş, Keskin Bıçak, Nadir Tohum, Canavar Kesimi, Kritik Göz, Ağır Darbe, Altın Hedef, Bilgi Filizi |
| Saksı yerleşimi | tile skoru | tile skoru + rezonans önceliği |
| İlk davranış kartı | **teklif edildiği anda alır** (patlama ya da elektrik) | **teklif edildiği anda alır** |

Bedelli ödüller (Bereketli Öğrenim, Davranışa Adanış) iki yönün sırasında yoktur: bot onları yalnız başka seçenek kalmadıysa alır.

## Eş bütçeli XP kolları (aynı seed, aynı başlangıç bütçesi)

| | A · XP'siz | B · erken XP |
|---|---|---|
| XP düğümleri (Hasat Deneyimi I / II) | hiç almaz; o kaynak yönün diğer düğümlerine ve saksıya gider | Hasat Deneyimi I ailesini (ön koşulu Düzenli Üretim - 1'in ilk kademesiyle) alabildiği anda, diğer düğümlerden önce alır; Hasat Deneyimi II'yi yönün normal vadesinde alır |
| Water (XP) tile kartı | yalnız başka seçenek yoksa (ağırlık ×0,05) | R20'ye kadar öne alır (ağırlık ×2); sonra yönün ağırlığı (×1) |
| Bilgi Filizi (boss ödülü, XP ×1,25) | sıranın sonunda | R20'ye kadar teklif edilince alır; sonra yönün sırası |
| Temel güç "XP kazancı" kartı | yalnız başka seçenek yoksa (ağırlık ×0,05) | yönün ağırlığı (×1) |
| Fazladan kaynak / kart | yok | yok |

"Erken" sınırı R20'dir. İki kol aynı yön politikasını, aynı seed'i (aynı boss ve ödül dizisi seed'i) ve aynı başlangıç bütçesini
kullanır. Kart teklifleri ve boss ödülü teklifleri iki kolda ayrışabilir (alımlar farklılaştıkça zar akışı ve uygunluk değişir);
ayrışma ölçümde kaydedilir.

## XP stresi (gerçekçi oyuncu değil)

Kontrolsüz geri beslemeyi yakalamak için: XP veren her şeyi önce alır.

- XP düğümleri vade −40 (alabildiği anda), kart düğümleri −10; kalan ağaç "Davranış-rezonans" gibi.
- Water kartı ×6; diğer ağırlıklar "Davranış-rezonans" gibi.
- Temel güçte teklif edilen her "XP kazancı" kartını alır (×1000); yoksa Üretim süresi / Atak aralığı ×1,5.
- Boss ödülü: Bilgi Filizi, Bereketli Öğrenim (level başına seçim +1), Zincir Hasat, sonra davranış sırası.

## Seed 101 kabul çifti

P6'da ve 3.7.6.1'de kullanılan politika ve seed aynen: "Davranış-rezonans", seed 101, zincirli ("aday": Zincir Hasat'ı
teklif edilince alır) ve zincirsiz ("almayan eş": yalnız onu almaz). Tek fark profildir (`Run50_XPV1`).

## Kuyruk katsayısı pilotu

Katsayı yalnız level 140'ı aşan run'ları etkiler. Pilot bu yüzden üç tür run'dan oluşur, her katsayıda (0,025 / 0,05 / 0,10)
aynı seed'lerle: seed 101 zincirsiz kol (eski kuralda 30 779 level), seed 110 zincirli kol (eski kuralda 141 level) ve XP stresi
(seed 100, 101). Run başına 25 dakika gerçek süre sınırı vardır; sınıra takılan run "durduruldu" diye kaydedilir.

## Erişim ölçüsü

Üç zaman ayrı kaydedilir: (1) ilk patlama / elektrik kartının alındığı round, (2) davranış şansı olan bir saksıyla başlanan ilk
round (kartın tile'ı bir saksının altında), (3) ilk gerçek patlama / elektrik tetiğinin olduğu round. Hedef, 3.6'daki anlamıyla:
erken davranışı deneyen açılışların en az 8 / 10'u ilk davranış kartını R6 sonuna kadar alsın. (2) ve (3) ayrıca raporlanır.

## Geri ödeme ölçüsü

Seed başına erken XP ÷ XP'siz oranı; round round medyan. "Anlık üstünlük": o round'un hasadı / skoru oranının üst üste 3 round
≥ ×1 olduğu ilk round. "Birikimli açığın kapanması": run başından toplam hasat / skor oranının üst üste 3 round ≥ ×1 olduğu ilk
round. İkisi ayrı yazılır. R50'ye kadar gerçekleşmezse "geri ödemedi" yazılır.

---

# Ek (4 Ekim, pilot ve ilk A/B görüldükten sonra, aşağıdaki ölçümler çalıştırılmadan önce yazıldı)

Yukarıdaki tanımlar değiştirilmedi; sonuçları ana belgede aynen raporlanır. Görülen iki şey yeni ölçüm gerektirdi:

## Üçüncü XP kolu: "vadesinde XP"

Neden: "erken XP" kolu XP ailesini her alışverişte diğer bütün düğümlerden önce aldığı için saksı ve grid alımları gecikti
(R5'te 2 saksı ↔ 6; R10'da 9 tile ↔ 22). Bu, XP yatırımının kendisini değil "XP'yi her şeyin önüne alma" sırasını ölçüyor.

| | C · vadesinde XP |
|---|---|
| XP düğümleri | ağaçta yazılı vadelerinde (Hasat Deneyimi I: R3–16, II: R25–42), diğer vadesi gelmiş düğümlerle aynı sırada; öne alınmaz |
| Water kartı, Bilgi Filizi, temel güç XP kartı | yönün kendi tercihi (ağırlık ×1; Bilgi Filizi sıranın sonunda) |
| Fazladan kaynak / kart | yok |

Aynı iki yön, aynı 10 seed; A kolu (XP'siz) ile karşılaştırılır. C'nin XP yatırımı yalnız ağaç düğümleridir (harcanan altın).

## İkinci stres tanımı: "Temel güç XP stresi"

Neden: ilk tanım ("XP stresi") ağaçta XP ve kart düğümlerini öne alınca build zayıf kaldı (pilot: seed 100 R50'de level 75,
seed 101 R23'te kotada elendi) ve temel güç evresine neredeyse hiç girmedi; XP kartı geri beslemesi sınanmadı.

İkinci tanım, temel güç evresine ulaştığı bilinen "Davranış-rezonans" build'ini aynen kullanır (ağaç ve tile tercihleri aynı)
ve yalnız şunları ekler: temel güçte teklif edilen her "XP kazancı" kartını alır; Bilgi Filizi'ni, Bereketli Öğrenim'i (level
başına seçim +1) ve Zincir Hasat'ı teklif edilince alır. Seed'ler: 100–104 ve 110. Eski kuralla (Run50_ZincirV1) karşılaştırma:
seed 101 ve 110, run başına 15 dakika gerçek süre sınırı.

## İki kuralın ayrı etkisi

Seed 101 zincirsiz kol ("Davranış-rezonans"), `Run50_XPV1` üstünde iki ek run: (a) yalnız toplanan XP kartı (tablo sonrası
maliyet sabit, s = 0), (b) yalnız büyüyen maliyet (s = 0,05; XP kartları eskisi gibi çarpımsal). Run başına 25 dakika gerçek
süre sınırı. Karşılaştırma noktaları: eski kural (ikisi de yok) ve yeni kural (ikisi birden).

## Kuyruk katsayısı

Pilotta üç katsayı da teknik durma, doyum ve on binlerce seçim üretmedi; seed 101'de fark küçük (level 179 / 169 / 162, toplam
skor farkı < %3). Değiştirmek için ölçülmüş bir neden olmadığından ilk aday s = 0,05 ile devam edilir.
