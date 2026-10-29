# Higgsfield denemesi (28 Ekim 2026)

Amaç: Blender v5.2'nin 1080p çıktısını Higgsfield'in video düzenleme modelleriyle "daha iyi toon" görünüme çevirmek ve Blender sürümüyle yan yana karşılaştırmak. Karar bu karşılaştırmayla verilecek.

## Hesap
- Plan: Starter (200 kredi/ay, devretmez). Bağlantı: Higgsfield MCP (`https://mcp.higgsfield.ai/mcp`).
- MCP'de her üretim kredi düşer; ücretsiz ve sınırsız üretim hakları yalnız web sitesinde geçer.
- Her gönderimden önce maliyet sorgulanır ve kullanıcı onaylar.

## Kaynak
- `Animatik/v5_1080/kareler/` (build_intro_v5.py `-- all hd`), 24 fps, 1920×1080.
- Parçalar görünmez kesmelerden (`kaynak/`):
  - `A_0000-0153.mp4`: Dünya'nın yok oluşu, kaçış, lomboz camı (6,42 sn)
  - `BC_0154-0281.mp4`: kokpit, yakıt, yakalanma, buluta dalış (5,33 sn)
  - `D_0282-0353.mp4`: düşüş ve iniş (3,00 sn)
  - `tam_0000-0353.mp4`: kuyruksuz, sessiz 14,75 sn

## Modeller ve maliyet (kredi harcamayan sorgu, 28 Ekim)
| Model | Ayar | 6 sn için |
|---|---|---|
| Kling 3.0 Omni Edit (`kling_video_edit`) | pro (1080p) | 12 |
| Kling 3.0 Omni Edit | std | 9 |
| Kling 4.0 (`kling4_0`) video-edit | 1080p | 19,5 |
| Gemini Omni Flash 1.1 edit | 1080p | 27 |
| FLUX 3 Video Edit | | 6 |
| Seedance 2.5 video_edit | 1080p | 120 (Starter'da büyük olasılıkla yok) |

Maliyet sorgusu, modelin Starter'da açık olup olmadığını göstermiyor; bu ilk gönderimde anlaşılır.

## İstem (İngilizce, modele giden)
```
Restyle this 3D animation into a polished, high-end toon / cel-shaded anime look for a video game opening.
Keep EXACTLY the same composition, camera movement, timing, objects and motion, frame for frame.
Keep the same rocket design (cream body, red-orange stripes, yellow-orange fins, round porthole),
the same Earth breaking into plates, the same navy space with stars, the same rust-red planet surface.
Improve: crisp three-tone cel shading, clean confident dark plum ink outlines with varied line weight,
hand-painted anime smoke and clouds with soft painted edges, vivid palette of navy, red-orange, cream and rust.
No photorealism, no lens flare, no film grain, no text, no new objects or characters, no blue fire.
```
Stil referansı: aynı render'dan seçilmiş kareler (dış görsel yok).

## Deneme 1: Kling 4.0, A parçası (28 Ekim 2026)
- Gönderim: `kling4_0`, `video-edit`, 1080p, `duration` 7, kaynak `A.mp4` + stil karesi `ref_roket.png`. İş `21bdabc7-7a2a-4461-8292-91a709f8baa9`. **28 kredi** düştü (210 → 182). Starter'da kabul edildi, üretim yaklaşık 2 dakika sürdü.
- Çıktı: `Kling4_A_1080p.mp4`, 1920×1080, 24 fps, 169 kare (kaynak 154). Model kendiliğinden ses ekledi ve sona 15 kare uzatma koydu.
- Karşılaştırma: `Karsilastirma_A_yan-yana.mp4` (ilk 154 kare, kare kare hizalı) ve `Karsilastirma_A_yakin-plan.png` (tam çözünürlükte kırpılmış kareler: 42, 72, 120).
- Bulgular (benim incelemem):
  - Hareket, kamera ve zamanlama kare kare korundu.
  - Görünüm neredeyse aynı kaldı; istenen belirgin toon iyileşmesi yok.
  - Ayrıntılı yakın planlarda bozulma var: kare 42'de Dünya plakalarının mürekkep çizgileri eriyor, cel basamakları bulanıklaşıyor, lav ayrıntısı kayboluyor.
  - Model yeni bir öğe ekledi: roketin arkasında yumuşak, gerçekçi beyaz bir duman izi var. Bu, istemdeki "yeni öğe yok" kuralına aykırı.
  - Sade alanlarda (gövde, lomboz) kaynakla neredeyse birebir, çizgiler yalnız biraz yumuşak.
- Kullanıcının izleyip karar vermesi bekleniyor.

## Deneme 2a: cesur stil karesi (28 Ekim 2026)
- Kaynak kare: `kaynak/ref_roket.png` (kare 120). İstem: "yüksek bütçeli TV animesi açılışından elle çizilmiş 2D anahtar kare, kompozisyon aynı" (tam metin iş kayıtlarında).
- `stil/stil_gpt-image-2-5.png`: GPT Image 2.5, high, 2k, **2,75 kredi**. Kompozisyona sadık; fırça mürekkebi, boyalı lacivert bulutsu, anime patlama şekilleri. Arka plan bizim lacivertimizden daha açık, daha mavi.
- `stil/stil_nano-banana-2-1.png`: Nano Banana 2.1, 2k, **2 kredi**. Daha ayrıntılı, Batı çizgi romanı / yarı gerçekçi. Tasarım kayıyor: lombozun çevresi perçinli metal oldu, duman eklendi.
- Karşılaştırma: `stil/Stil_karsilastirma.png` (üstte kaynak, ortada GPT, altta Nano Banana). Bakiye 177,25.

## Deneme 2b: Kling 3.0 Omni Edit + GPT stil karesi (28 Ekim 2026)
- Stil seçimi: GPT Image 2.5. `Docs/UI-Art-Direction.md` kalın, siyaha yakın mürdüm mürekkep ve düz, baskı gibi renk alanları istiyor. Nano Banana parlak metal ve ayrıntı ekliyordu.
- Gönderim: `kling_video_edit`, pro, `duration` 7, kaynak `A.mp4` + stil karesi (iş `a676f77a…`). "Videonun tamamını referansın elle çizilmiş 2D anime stiline çevir" istemi. Higgsfield'in önerdiği "3D RENDER" hazır ayarı, hedefin tersi olduğu için reddedildi. İş `58394935-1e49-4378-8c4e-48d44d7f6efc`, **12 kredi**. Üretim yaklaşık 6 dakika sürdü.
- Çıktı: `KlingOmni3_A_GPTstil.mp4`, 1920×1080, 24 fps, 153 kare, ses yok. Karşılaştırma: `Karsilastirma_A2_yan-yana.mp4`, `Karsilastirma_A2_kareler.png`.
- Bulgu: stil karesine geçmedi. Görüntü yine kaynağa çok yakın, yalnız biraz soluk: Dünya'nın mavisi açıldı, bulutlar sarımsı oldu, lomboz camı soldu. Kare 42'de plakalarda yine suluboya gibi erime var. Fırça mürekkebi, boyalı gök ve anime patlama şekilleri gelmedi.
- Sonuç: video düzenleme modelleri (Kling 4.0, Kling 3.0 Omni Edit) kaynak pikselleri korumaya çok bağlı; temiz toon girdiyi belirgin biçimde dönüştürmüyorlar.
- Toplam harcama: 28 + 2,75 + 2 + 12 = 44,75 kredi. Bakiye 165,25.

## Deneme 3: Kling 4.0, gerçekçi (eğlence denemesi, 28 Ekim 2026)
- Kullanıcı Blender'a geçmeden önce "gerçek film gibi" bir deneme istedi. Gönderim: `kling4_0`, `video-edit`, 1080p, `duration` 7, kaynak `A.mp4`, istem "fotogerçekçi canlı çekim, IMAX uzay belgeseli". İş `5b810919-52c0-4d60-8bd1-0a3609d67b97`, **28 kredi**. Bakiye 137,25.
- Çıktı: `Kling4_A_gercekci.mp4` (169 kare, ses var). Karşılaştırma: `Karsilastirma_A3_gercekci.mp4`, `Karsilastirma_A3_kareler.png`.
- Bulgu: bu kez dönüşüm güçlü. Yörüngeden gerçekçi Dünya, lavlı çatlaklar, eriyik dolu plakalar, gerçek motor ateşi ve duman, yıpranmış metal roket. Hareket ve kompozisyon korundu.
- Kusurlar:
  - Kare 120'de parçalanmış Dünya yeniden bütün bir Dünya olmuş; hikâye bozuluyor.
  - Toon şok halkası ve hız çizgileri gerçekçi sahnede kalmış.
  - Lombozun sarı ışığı kararmış.
- Ders: video düzenleme modelleri kendi "doğal" alanlarına (gerçekçilik) kolayca çeviriyor. Zaten stilize olan girdiyi başka bir stilize görünüme çevirmiyorlar.

## Lisans notu
Higgsfield çıktıları Higgsfield Kullanım Şartları'na bağlı: ücretli planda ticari kullanım açık, atıf zorunlu değil, filigran yok. Yüklenen videolar Enterprise dışı planlarda model eğitiminde kullanılabilir. MCP kullanımı "Developer Terms" kapsamında. Steam'de önceden üretilmiş AI içeriği beyan edilmeli. Kayıt: `Docs/ThirdParty-Licenses.md`.
