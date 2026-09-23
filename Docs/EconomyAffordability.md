# Ekonomi — round bazında gelişim ve erişim

Kaynak: canlı fiyat planıyla 10 seed × 3 alışveriş tercihi, ayrıca 10 ayrı seed ile doğrulama. Her round kendi geliri, satın alımları ve güncel build statlarıyla ilerler. Geç round geliri geçmişe uygulanmaz.

Tam **140 node / 304 kademe**: **156.899 Gold + 102.875 Iron + 16.320 Stone**. Bu tutar yalnız skill ağacıdır; saksı satın alımları ayrıca ödenir. Fiyatlar herkes için sabittir.

Model 30 FPS yaşam döngüsü ve gerçek varlık fiyat/etkilerini kullanır; float ve hedefleme yaklaşımı nedeniyle Unity oynanışıyla bit düzeyinde aynı değildir. Kart, tile, rezonans, ikincil davranış ve skip geliri dahil değildir. İyi rezonansın ne kadar erken bitirdiği bu testten çıkarılamaz. İnsan playtest sonucu değildir.

## Referans gelişim temposu

| Dönem | Medyan satın alma / round |
|---|---:|
| R1–20 | 2.85 |
| R21–65 | 1.90 |
| R66–100 | 4.36 |
| R101–130 | 0.00 |

Bunlar kademe satın alımlarıdır, farklı node sayısı değildir. Bir roundda birden fazla satın alım olabilir; round başına alışveriş sınırı yoktur.

| Round | Satın alınmış kademe P50 | Nominal hasar P50 | Saldırı aralığı P50 | Spawn aralığı P50 |
|---|---:|---:|---:|---:|
| 20 | 58 | 83 | 2.10 s | 4.33 s |
| 45 | 112.5 | 277 | 2.10 s | 4.25 s |
| 65 | 137.5 | 334 | 2.10 s | 4.25 s |
| 80 | 175.5 | 2.478 | 1.47 s | 4.21 s |
| 100 | 304 | 3.201.552 | 0.20 s | 0.50 s |
| 120 | 304 | 3.201.552 | 0.20 s | 0.50 s |

## Referans — kontrollü hedefleme

Vuruş başına 4–12 etkili hedef; 48 hücreye kademeli yatırım. Dengeli, ekonomi ve hasar ağırlıklı alışveriş tercihlerinin tümü aynı fiyatları kullanır. Hücre hedefi bütçe yetmezse gerçekleşmez.

R130 farklı açılmış skill: **140–140 / 140**, P50 **140**. Açılma en az bir kademe satın alınmasıdır; full yükseltme değildir. Tamamlanan koşu **30/30**. Tamamlayanların ortalaması R99.4, aralık R83–121.

Harcamalar öncesi **birikimli kazanım P50**; cüzdan bakiyesi değildir:

| Round | Gold | Iron | Stone |
|---|---:|---:|---:|
| 20 | 2.759 | 510 | 98 |
| 65 | 24.481 | 8.287 | 1.814 |
| 100 | 267.058 | 125.874 | 38.222 |
| 130 | 1.201.029 | 610.399 | 194.312 |

R130 toplam kazanım P10–P90: Gold 720.150–1.598.863; Iron 371.856–815.402; Stone 117.808–260.419.

## Az hedef / kaçan saldırı stres testi

Vuruş başına 2–6 etkili hedef, %15 saldırı kaçırma, 30 hücre çiftlik hedefi. Hücre hedefi bütçe yetmezse gerçekleşmez.

R130 farklı açılmış skill: **26–44 / 140**, P50 **37**. Açılma en az bir kademe satın alınmasıdır; full yükseltme değildir. Tamamlanan koşu **0/30**.

Harcamalar öncesi **birikimli kazanım P50**; cüzdan bakiyesi değildir:

| Round | Gold | Iron | Stone |
|---|---:|---:|---:|
| 20 | 721 | 148 | 18 |
| 65 | 4.109 | 886 | 164 |
| 100 | 4.934 | 1.092 | 200 |
| 130 | 5.015 | 1.106 | 211 |

R130 toplam kazanım P10–P90: Gold 3.246–5.576; Iron 630–1.363; Stone 132–286.

## Çok düşük hasat verimi stres testi

Vuruş başına 1–4 etkili hedef, %30 saldırı kaçırma, 24 hücre çiftlik hedefi. Hücre hedefi bütçe yetmezse gerçekleşmez.

R130 farklı açılmış skill: **6–17 / 140**, P50 **11**. Açılma en az bir kademe satın alınmasıdır; full yükseltme değildir. Tamamlanan koşu **0/30**.

Harcamalar öncesi **birikimli kazanım P50**; cüzdan bakiyesi değildir:

| Round | Gold | Iron | Stone |
|---|---:|---:|---:|
| 20 | 224 | 32 | 3 |
| 65 | 818 | 118 | 11 |
| 100 | 909 | 134 | 15 |
| 130 | 915 | 136 | 15 |

R130 toplam kazanım P10–P90: Gold 632–1.063; Iron 112–176; Stone 10–26.

## Ayrı seed doğrulaması

Referans kontrolde 28/30 koşu tamamlandı. Tamamlayanlar R84–117; ortalama R98.5. R130 farklı node sayısı 95–140.

Düşük hasat verimi, kötü skill seçimiyle aynı değişken değildir: stres testleri hedef kapsamasını, kaçan saldırıları ve çiftlik büyüklüğünü de değiştirir. Bu testlerde tıkanma erken başlayabilir. Önceki "en kötü koşulda en az 80 node" iddiası bu veri için geçerli değildir. Fiyat planı hızlı başlangıç / orta yavaşlama / son güçlenmeyi referans koşuda hedefler; bütün oyuncuları aynı sonuca zorlamaz. Gerçek oyuncu testleri ve rezonans/kart geri beslemesi ayrıca değerlendirilmelidir.
