# Oyun gökyüzü: toon uzay + güce göre renk (23 Kasım 2026)

Bu belge oyunun gökyüzü sistemini anlatır. Menünün arka planı eski uzay shader'ıyla kalır.

## Ne yapıyor?

- **Görünüm:** gökyüzü toon uzaydır ve şu parçalardan oluşur:
  - merkez parlaması 3 boyalı banttır;
  - nebula 3 cel tonudur; ışık alan tarafı ve ince mürekkep çizgisi vardır;
  - yıldızlar halesiz, sert noktalardır; aralarda seyrek, dört kollu parıltılar vardır;
  - gezegen onaylanan boyalı gezegendir.
- **Nefes:** 10 saniyelik çok hafif bir döngüdür. Bant ve bulut sınırları birkaç piksel genişleyip daralır. Renk ve parlaklık sabit kalır, yanıp sönme yoktur.
- **Güce göre renk:** gökyüzü müzikle aynı güç değerini okur (`BossRewardManager.MusicPower`).
  - Her boss ödülü kendi aşamasına göre bu değere eklenir: erken +1, orta +2, güçlü +3. Ödülde `musicPowerOverride` doluysa o değer eklenir.
  - Müzik havuzu da parçayı bu değere göre seçer (`SoundManager.Music`, `minPower` / `maxPower`).
  - Şu an havuzda tek parça var; müzik henüz fiilen değişmiyor.
- **Renk merdiveni:** renk çemberinde soğuktan sıcağa gider (sakin → doruk).

| Güç | Aşama |
|---|---|
| 0 | camgöbeği + lacivert |
| 3 | gece mavisi + menekşe |
| 8 | mercan + gece mavisi |
| 16 | gün batımı |
| 26 | kırmızı (doruk) |

  Kabaca R10'da güç 3, R20'de 9, R30'da 18, R50'de 30'un üstüdür. Bu bir tahmin; ölçülmedi.
- **Renk teorisi kuralları:**
  - Zeminin ve bulutların koyuluğu (OKLab L) her aşamada aynıdır. Tarla her aşamada aynı netlikte okunur.
  - Geçiş OKLCh renk uzayında yapılır ve ton kısa yoldan döner. Düz RGB karışımının ortasındaki gri çamur oluşmaz:
    - doğrulamada en düşük doygunluk oranı OKLCh'de 0,98;
    - düz RGB karışımında aynı oran 0,64.
- **Geçiş anı:** güç round arasında, ödül alınınca değişir. Renk geçişi yeni round başlarken başlar ve 3,5 saniye sürer. Round ortasında ve ödül ekranının arkasında renk kaymaz.
- **Ayar:** Ayarlar > Görüntü > Arka Plan Efektleri, üç seçeneklidir:
  - **Güce Göre** (varsayılan);
  - **Sabit** (mor toon gökyüzü);
  - **Kapalı** (düz renk, zayıf ekran kartları için).

## Nasıl düzenlenir?

- **Renkler ve eşikler:** `Assets/Resources/SpaceSkyPalette.asset`.
  - Inspector'daki "Güç önizleme" kaydırıcısı ve "Sabit" kutusu, Play'e basmadan oyunun shader'ıyla çizilmiş hâli gösterir.
  - Renkler sRGB'dir.
  - Yeni bir aşama eklerken koyuluğu öbürlerine yakın tut: doğrulama koyuluk farkının 0,01'den küçük olmasını ister.
- **Geçiş süresi ve nefes:** aynı asset'teki `transitionSeconds` ve `breath` alanları.
- **Çizim:**
  - shader: `Assets/Resources/SpaceToonBackground.shader` + `Assets/Shaders/SpaceToon.hlsl`;
  - nebula gürültüsü bir kez dokuya çizilir: `SpaceToonNoiseBaker`, 1280×688 RG16F ≈ 3,5 MB.
- **Bağlantı:**
  - `SpaceBackground.cs`: `skyPalette` atanırsa toon gökyüzü, boşsa eski uzay çizilir.
  - Oyun sahnesinde bileşeni `FeelBootstrap` ekler ve paleti atar.
  - Tools > Feel > Sahneye Kur da paleti atar.

## Doğrulama

`Tools/run-isolated-verification.ps1 -Method SpaceSkyVerification.RunBatch -Full` → 19 kontrol. Kontrol edilenler:

- palet verisi;
- güç → aşama eşlemesi;
- koyuluk eşitliği;
- gri çukuru olmaması;
- inspector önizlemesi;
- FeelBootstrap kurulumu;
- güç artınca geçişin başlayıp hedefe varması;
- round dışında renk kaymaması;
- "Sabit" ayarı;
- 5 kez kapatıp açınca tek gökyüzü dörtgeni;
- geçiş sırasında kare başı GC ayırması 0.

Çekimler `Library/VerificationProject/Logs/SpaceSky/` klasörüne yazılır: her palet normal tarlada, Don Cephesi'nde ve Sert Kabuk'ta çekilir.

## Açık konular

- Boss'a özel renk bindirmesi (Don Cephesi buz mavisi, Sert Kabuk kehribar) yapılmadı.
- Boss hava efektinin çizgileri (tipi / kum) toplu kip çekiminde görünmüyor; perdeler görünüyor. Hava efektiyle birlikte görünüm Play'de bakılmalı.
- GPU maliyeti ölçülmedi. Eski uzaya göre her pikselde 1 doku örneği fazla, bir yıldız katmanı ve parıltı katmanı var.
- Menüye dokunulmadı; menü eski uzayla kalıyor.
